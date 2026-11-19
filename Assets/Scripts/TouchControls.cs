using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem.EnhancedTouch;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TPhase = UnityEngine.InputSystem.TouchPhase;

/// On-screen controls for phones and tablets. Built only on touch devices; on a desktop
/// nothing here is created and every query answers "no", so keyboard and mouse play is
/// exactly what it was.
///
/// Layout (landscape, like most mobile action games):
///  - left half: a floating stick that appears under the thumb wherever it lands;
///  - right thumb: a big Attack button in an arc with Jump, Roll and (once found) Shield;
///    tap Attack for a light swing, hold it for a heavy (a slam in the air), flick it
///    upward for an Ember Burst;
///  - anywhere else on the right: tap to swing, swipe to roll that way;
///  - the item cross is the desktop one, and its wells are tappable: tap to equip, tap
///    the equipped one again to use it (drink / cast);
///  - an Interact pill only exists while something nearby can be taken, and the floating
///    prompt above the thing is tappable too;
///  - a pause button top right; tap anywhere to rise after a death.
///
/// Every button feeds the same action numbers the keyboard bindings use
/// (MenuSettings.Actions), so all of the game's MenuSettings.Pressed/Held checks hear them.
[DefaultExecutionOrder(-200)]   // before Game (-100) and the actors, so a press lands in the same frame
public class TouchControls : MonoBehaviour
{
    public const int Attack = 4, Heavy = 5, Magic = 6, Jump = 7, Dodge = 8, Restart = 10, Interact = 12, Guard = 13;
    const int ActionCount = 16;

    public static TouchControls I { get; private set; }
    static bool detected, touchDevice, portraitDismissed;
    static readonly int[] pressedFrame = NewFrames();
    static readonly bool[] held = new bool[ActionCount];
    static float lastTouch = -10f;
    static Vector2 swipeDir; static int swipeFrame = -1;

    static int[] NewFrames() { var a = new int[ActionCount]; for (int i = 0; i < a.Length; i++) a[i] = -1; return a; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        I = null; detected = touchDevice = portraitDismissed = false; lastTouch = -10f; swipeFrame = -1;
        for (int i = 0; i < ActionCount; i++) { pressedFrame[i] = -1; held[i] = false; }
    }

    /// True on phones and tablets (decided once), or after the first real touch on a hybrid laptop.
    public static bool Enabled
    {
        get { if (!detected) { detected = true; touchDevice = WebBridge.IsTouchDevice(); } return touchDevice; }
    }
    /// The touch layout is up and steering the knight right now.
    public static bool Active => I != null && I.playing;
    public static bool Pressed(int action) => action >= 0 && action < ActionCount && pressedFrame[action] == Time.frameCount;
    public static bool Held(int action) => action >= 0 && action < ActionCount && held[action];
    /// Browsers can fake mouse clicks after a tap. While fingers are in use, the mouse is ignored for gameplay.
    public static bool SuppressMouse => touchDevice && Time.unscaledTime - lastTouch < 1.5f;
    public static Vector2 Move
    {
        get
        {
            if (I == null || !I.playing) return Vector2.zero;
            // A swipe-roll steers the roll for the frame it fires, unless the stick is already pointing.
            if (swipeFrame == Time.frameCount && I.move.sqrMagnitude < .01f) return swipeDir;
            return I.move;
        }
    }

    static void Fire(int action) { if (action >= 0 && action < ActionCount) pressedFrame[action] = Time.frameCount; }

    // ---- tuning (canvas units: the HUD's 1536 x 864 reference) --------------------
    const float StickTravel = 100f, StickBase = 230f, StickKnob = 96f, DeadZone = .16f;
    const float HeavyHold = .22f;           // hold Attack this long for a heavy
    const float FlickMin = 70f;             // upward flick on Attack for an Ember Burst
    const float TapSlop = 34f, TapTime = .32f, SwipeMin = 80f, SwipeTime = .45f;
    const float LeftZone = .45f, TopBand = .78f;   // stick lives in the left 45%, below the knight card
    // Where the idle stick rests; nearer the edge when upright so it clears Jump and Roll.
    static Vector2 StickHome => Screen.height > Screen.width ? new Vector2(150, 215) : new Vector2(250, 215);

    static readonly Color Rim = new Color(.31f, .27f, .19f, .92f), RimLit = new Color(.94f, .76f, .53f, 1f),
        Well = new Color(.075f, .065f, .055f, .74f), Cream = new Color(.89f, .83f, .75f, 1f), Gold = new Color(.94f, .76f, .53f, 1f);

    class Btn
    {
        public RectTransform rt; public Image rim, well, icon; public Text label;
        public float size; public bool down, visible = true; public float press;
    }

    class Finger
    {
        public int role; public Btn btn; public Vector2 start, last; public float t0;
        public bool heavyDone, flicked;
    }
    const int RoleNone = 0, RoleStick = 1, RoleButton = 2, RoleZone = 3, RoleRestart = 4, RoleOther = 5, RolePortrait = 6;

    Game game; Hud hud;
    Canvas canvas; RectTransform root, buttons;
    CanvasGroup buttonsCg;
    Font font, labelFont;
    Btn attack, jump, roll, guard, pause;
    Image heavyRing;
    RectTransform interactPill; Text interactLabel; Image interactRim; CanvasGroup interactCg;
    RectTransform stickBase, stickKnob; CanvasGroup stickCg;
    RectTransform portraitPanel;
    readonly Dictionary<int, Finger> fingers = new Dictionary<int, Finger>();
    readonly HashSet<int> seen = new HashSet<int>();
    readonly List<int> gone = new List<int>();
    int stickFinger = -1; Vector2 stickCenter, stickKnobPos;
    bool playing, built, attackHeld; float attackHoldT;
    Vector2 move;

    public static TouchControls Create(Hud hud, Game game)
    {
        var go = new GameObject("Touch controls", typeof(RectTransform));
        var tc = go.AddComponent<TouchControls>();
        tc.hud = hud; tc.game = game;
        return tc;
    }

    void Awake() { I = this; }

    void OnDestroy()
    {
        if (I == this) I = null;
        for (int i = 0; i < ActionCount; i++) held[i] = false;
    }

    // ---- building ------------------------------------------------------------------

    void Build()
    {
        built = true;
        EnhancedTouchSupport.Enable();
        canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9;   // under the HUD canvas (10), so menus, cards and fades cover it
        var sc = gameObject.AddComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1536, 864); sc.matchWidthOrHeight = .5f;
        root = (RectTransform)transform;
        font = Resources.Load<Font>("Fonts/GameFont");
        labelFont = Resources.Load<Font>("Fonts/EmbercladMenu"); if (labelFont == null) labelFont = font;

        buttons = Full("Buttons", root); buttonsCg = buttons.gameObject.AddComponent<CanvasGroup>();
        buttonsCg.blocksRaycasts = false; buttonsCg.interactable = false;
        Vector2 BR = new Vector2(1, 0), TR = new Vector2(1, 1), BL = Vector2.zero;

        // Stick first, so the buttons draw over it if a thumb wanders.
        stickBase = Rect("Stick", buttons, BL, StickHome, new Vector2(StickBase, StickBase));
        stickCg = stickBase.gameObject.AddComponent<CanvasGroup>();
        Img("ui_round", stickBase, Vector2.zero, new Vector2(StickBase, StickBase), new Color(.05f, .045f, .04f, .38f), true);
        Img("fx_ring", stickBase, Vector2.zero, new Vector2(StickBase * 1.06f, StickBase * 1.06f), new Color(.94f, .76f, .53f, .55f));
        stickKnob = Img("ui_round", stickBase, Vector2.zero, new Vector2(StickKnob, StickKnob), new Color(.89f, .83f, .75f, .8f), true).rectTransform;
        Img("ui_round", stickKnob, Vector2.zero, new Vector2(StickKnob - 14, StickKnob - 14), new Color(.31f, .27f, .19f, .9f), true);

        attack = MakeButton("Attack", BR, new Vector2(-190, 172), 176, "k_sword", null);
        attack.icon.rectTransform.localRotation = Quaternion.Euler(0, 0, -25);
        heavyRing = Img("fx_ring", attack.rt, Vector2.zero, new Vector2(214, 214), new Color(1f, .62f, .25f, .95f));
        heavyRing.type = Image.Type.Filled; heavyRing.fillMethod = Image.FillMethod.Radial360; heavyRing.fillOrigin = (int)Image.Origin360.Top;
        heavyRing.fillClockwise = true; heavyRing.fillAmount = 0;
        jump = MakeButton("Jump", BR, new Vector2(-398, 100), 122, null, "JUMP");
        roll = MakeButton("Roll", BR, new Vector2(-376, 282), 108, null, "ROLL");
        guard = MakeButton("Guard", BR, new Vector2(-196, 382), 104, "s_shield", null);
        pause = MakeButton("Pause", TR, new Vector2(-64, -60), 80, null, "II");
        pause.label.fontSize = 34;

        // Above Roll, inside the right thumb's arc and clear of the item cross.
        interactPill = Rect("Interact", buttons, BR, new Vector2(-440, 414), new Vector2(232, 74));
        interactCg = interactPill.gameObject.AddComponent<CanvasGroup>(); interactCg.alpha = 0;
        interactRim = Img("ui_round", interactPill, Vector2.zero, new Vector2(232, 74), RimLit, true);
        Img("ui_round", interactPill, Vector2.zero, new Vector2(222, 64), Well, true);
        interactLabel = Txt("TAKE", interactPill, Vector2.zero, new Vector2(214, 60), 24, Cream, labelFont);

        portraitPanel = Full("Turn sideways", root);
        var shade = portraitPanel.gameObject.AddComponent<Image>(); shade.color = new Color(.02f, .02f, .03f, .82f); shade.raycastTarget = false;
        Txt("TURN YOUR DEVICE SIDEWAYS", portraitPanel, new Vector2(0, 40), new Vector2(1400, 80), 46, Gold, labelFont);
        Txt("Emberclad plays best in landscape.\nTap to keep playing upright.", portraitPanel, new Vector2(0, -60), new Vector2(1400, 120), 28, Cream, font);
        portraitPanel.gameObject.SetActive(false);
    }

    Btn MakeButton(string name, Vector2 anchor, Vector2 pos, float size, string sprite, string text)
    {
        var b = new Btn { size = size };
        b.rt = Rect(name, buttons, anchor, pos, new Vector2(size, size));
        b.rim = Img("ui_round", b.rt, Vector2.zero, new Vector2(size, size), Rim, true);
        b.well = Img("ui_round", b.rt, Vector2.zero, new Vector2(size - 10, size - 10), Well, true);
        if (sprite != null)
        {
            b.icon = Img(sprite, b.rt, new Vector2(0, 2), new Vector2(size * .62f, size * .66f), Color.white);
            b.icon.preserveAspect = true;
        }
        if (text != null) b.label = Txt(text, b.rt, new Vector2(0, 2), new Vector2(size, size * .6f), Mathf.RoundToInt(size * .22f), Cream, labelFont);
        return b;
    }

    RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform)); var rt = (RectTransform)go.transform; rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(.5f, .5f); rt.anchoredPosition = pos; rt.sizeDelta = size; return rt;
    }

    RectTransform Full(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform)); var rt = (RectTransform)go.transform; rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; return rt;
    }

    Image Img(string sprite, Transform parent, Vector2 pos, Vector2 size, Color c, bool sliced = false)
    {
        var rt = Rect(sprite, parent, new Vector2(.5f, .5f), pos, size); var im = rt.gameObject.AddComponent<Image>();
        im.sprite = Art.S(sprite); im.color = c; im.raycastTarget = false; if (sliced) im.type = Image.Type.Sliced; return im;
    }

    Text Txt(string s, Transform parent, Vector2 pos, Vector2 size, int fontSize, Color c, Font f)
    {
        var rt = Rect("Text", parent, new Vector2(.5f, .5f), pos, size); var t = rt.gameObject.AddComponent<Text>();
        t.font = f; t.text = s; t.fontSize = fontSize; t.color = c; t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        var sh = rt.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(.05f, .04f, .04f, .85f); sh.effectDistance = new Vector2(0, -2);
        return t;
    }

    // ---- per frame -----------------------------------------------------------------

    void Update()
    {
        if (!Enabled)
        {
            // A touchscreen laptop: switch the layout on at the first real touch.
            if (ETouchAnyDown()) touchDevice = true; else return;
        }
        if (!built) Build();
        if (game == null || hud == null) return;

        bool wasPlaying = playing;
        playing = game.state == Game.State.Playing && !game.Paused && !hud.IntroOpen && !hud.PauseMenuOpen
                  && game.player != null && !game.player.dead;
        bool portrait = Screen.height > Screen.width * 1.05f;
        bool showPortrait = playing && portrait && !portraitDismissed;
        if (showPortrait) playing = false;
        if (wasPlaying && !playing) ReleaseAll();

        ReadTouches(showPortrait);
        if (!playing) { move = Vector2.zero; attackHeld = false; }
        Draw(showPortrait);
    }

    static bool ETouchAnyDown()
    {
        var ts = UnityEngine.InputSystem.Touchscreen.current;
        return ts != null && ts.primaryTouch.press.isPressed;
    }

    void ReleaseAll()
    {
        fingers.Clear(); stickFinger = -1; move = Vector2.zero; attackHeld = false;
        for (int i = 0; i < ActionCount; i++) held[i] = false;
        foreach (var b in new[] { attack, jump, roll, guard, pause }) if (b != null) b.down = false;
    }

    float Scale => canvas != null && canvas.scaleFactor > 0 ? canvas.scaleFactor : 1f;

    void ReadTouches(bool portrait)
    {
        seen.Clear();
        float now = Time.unscaledTime;
        foreach (var t in ETouch.activeTouches)
        {
            int id = t.touchId; Vector2 pos = t.screenPosition; var ph = t.phase;
            bool ended = ph == TPhase.Ended || ph == TPhase.Canceled;
            seen.Add(id); lastTouch = now;
            if (!fingers.TryGetValue(id, out var f))
            {
                // A touch we are meeting for the first time, possibly already over (a very quick tap).
                f = Begin(id, ended ? t.startScreenPosition : pos, portrait);
                fingers[id] = f;
            }
            if (ended) { End(id, f, pos); fingers.Remove(id); }
            else Hold(id, f, pos);
        }
        // Fingers the system stopped reporting without an end (app switch, rotation).
        gone.Clear();
        foreach (var kv in fingers) if (!seen.Contains(kv.Key)) gone.Add(kv.Key);
        foreach (var id in gone) { var f = fingers[id]; End(id, f, f.last, true); fingers.Remove(id); }
    }

    Finger Begin(int id, Vector2 pos, bool portrait)
    {
        var f = new Finger { start = pos, last = pos, t0 = Time.unscaledTime };
        if (portrait) { f.role = RolePortrait; return f; }
        if (!playing)
        {
            f.role = game.state == Game.State.Dead || game.state == Game.State.Victory ? RoleRestart : RoleNone;
            return f;
        }
        var b = HitButton(pos);
        if (b != null)
        {
            f.role = RoleButton; f.btn = b; b.down = true; b.press = 1f;
            if (b == jump) Fire(Jump);
            else if (b == roll) Fire(Dodge);
            else if (b == guard) held[Guard] = true;
            else if (b == attack) { attackHeld = true; attackHoldT = 0; }
            return f;
        }
        if (interactCg.alpha > .5f && Contains(interactPill, pos)) { Fire(Interact); f.role = RoleOther; return f; }
        int slot = hud.HotbarSlotAt(pos);
        if (slot >= 0) { TapSlot(slot); f.role = RoleOther; return f; }
        if (hud.PromptHit(pos)) { Fire(Interact); f.role = RoleOther; return f; }
        if (pos.x < Screen.width * LeftZone)
        {
            if (stickFinger < 0 && pos.y < Screen.height * TopBand)
            {
                f.role = RoleStick; stickFinger = id;
                stickCenter = ClampStick(pos); stickKnobPos = pos; UpdateStick(pos);
            }
            return f;
        }
        f.role = RoleZone;
        return f;
    }

    void Hold(int id, Finger f, Vector2 pos)
    {
        f.last = pos;
        if (f.role == RoleStick) { UpdateStick(pos); return; }
        if (f.role != RoleButton || f.btn != attack || f.heavyDone || f.flicked) return;
        float age = Time.unscaledTime - f.t0;
        Vector2 d = (pos - f.start) / Scale;
        attackHoldT = age;
        if (d.y > FlickMin && age < .4f && game.HasEmbers) { f.flicked = true; attackHeld = false; Fire(Magic); return; }
        // Heavy is the sword's; with the flask or embers out every press does the same thing.
        if (age >= HeavyHold && QuickItems.Selected == 0 && d.magnitude < 60f) { f.heavyDone = true; attackHeld = false; Fire(Heavy); }
    }

    void End(int id, Finger f, Vector2 pos, bool lost = false)
    {
        float age = Time.unscaledTime - f.t0;
        Vector2 d = (pos - f.start) / Scale;
        switch (f.role)
        {
            case RoleStick:
                if (stickFinger == id) { stickFinger = -1; move = Vector2.zero; }
                break;
            case RoleButton:
                f.btn.down = false;
                if (f.btn == guard) held[Guard] = false;
                else if (f.btn == attack)
                {
                    attackHeld = false;
                    if (!lost && !f.heavyDone && !f.flicked) Fire(Attack);
                }
                else if (f.btn == pause && !lost && HitButton(pos) == pause) hud.OpenPauseMenu();
                break;
            case RoleZone:
                if (lost) break;
                if (age < TapTime && d.magnitude < TapSlop) Fire(Attack);
                else if (age < SwipeTime && d.magnitude > SwipeMin) { swipeDir = d.normalized; swipeFrame = Time.frameCount; Fire(Dodge); }
                break;
            case RoleRestart:
                if (!lost && age < .8f) Fire(Restart);
                break;
            case RolePortrait:
                if (!lost) portraitDismissed = true;
                break;
        }
    }

    /// Tap a well to equip it; tap the one already in hand to use it.
    void TapSlot(int slot)
    {
        if (slot >= QuickItems.Filled) return;
        if (QuickItems.Selected != slot) { QuickItems.Select(slot); return; }
        if (slot == 1) GameInput.Sim("potion");
        else if (slot == 2) GameInput.Sim("magic");
        else Fire(Attack);
    }

    Vector2 ClampStick(Vector2 pos)
    {
        float r = StickBase * .5f * Scale;
        pos.x = Mathf.Clamp(pos.x, r, Mathf.Max(r, Screen.width * LeftZone));
        pos.y = Mathf.Clamp(pos.y, r, Mathf.Max(r, Screen.height - r));
        return pos;
    }

    void UpdateStick(Vector2 pos)
    {
        float travel = StickTravel * Scale;
        Vector2 off = pos - stickCenter;
        // Past the rim the base follows the thumb, so a long drag never runs out of stick.
        if (off.magnitude > travel) { stickCenter = pos - off.normalized * travel; off = pos - stickCenter; }
        stickKnobPos = stickCenter + off;
        Vector2 v = off / travel;
        float m = v.magnitude;
        move = m < DeadZone ? Vector2.zero : v / m * Mathf.Clamp01((m - DeadZone) / (1f - DeadZone));
    }

    Btn HitButton(Vector2 pos)
    {
        Btn best = null; float bestD = float.MaxValue;
        foreach (var b in new[] { attack, jump, roll, guard, pause })
        {
            if (b == null || !b.visible) continue;
            Vector2 c = RectTransformUtility.WorldToScreenPoint(null, b.rt.position);
            float r = b.size * .5f * Scale * 1.18f;   // a little larger than drawn: thumbs are wide
            float dist = Vector2.Distance(pos, c);
            if (dist < r && dist / r < bestD) { best = b; bestD = dist / r; }
        }
        return best;
    }

    static bool Contains(RectTransform rt, Vector2 pos) => RectTransformUtility.RectangleContainsScreenPoint(rt, pos, null);

    // ---- drawing -------------------------------------------------------------------

    void Draw(bool showPortrait)
    {
        float dt = Time.unscaledDeltaTime;
        portraitPanel.gameObject.SetActive(showPortrait);
        buttonsCg.alpha = Mathf.MoveTowards(buttonsCg.alpha, playing ? 1f : 0f, dt * 6f);
        bool any = buttonsCg.alpha > 0f;
        if (buttons.gameObject.activeSelf != any) buttons.gameObject.SetActive(any);
        if (!any) return;

        var p = game.player;
        // The Attack button wears whatever is in hand, because that is what it will do.
        int sel = QuickItems.Selected;
        if (sel == 0) { attack.icon.sprite = Art.S("k_sword"); attack.icon.color = Color.white; attack.icon.rectTransform.localRotation = Quaternion.Euler(0, 0, -25); }
        else
        {
            attack.icon.rectTransform.localRotation = Quaternion.identity;
            if (sel == 1) { attack.icon.sprite = Art.S("ui_potion"); attack.icon.color = p != null && p.potions > 0 ? Color.white : new Color(.4f, .4f, .4f, .65f); }
            else { bool can = p != null && p.CanCast; attack.icon.sprite = Art.S(can ? "ui_ember" : "ui_ember_off"); attack.icon.color = can ? Color.white : new Color(.52f, .44f, .4f, .7f); }
        }
        float charge = attackHeld && sel == 0 ? Mathf.Clamp01((attackHoldT - .06f) / (HeavyHold - .06f)) : 0f;
        heavyRing.fillAmount = Mathf.MoveTowards(heavyRing.fillAmount, charge, dt * (charge > heavyRing.fillAmount ? 30f : 8f));

        guard.visible = game.HasShield;
        foreach (var b in new[] { attack, jump, roll, guard, pause })
        {
            if (b.rt.gameObject.activeSelf != b.visible) b.rt.gameObject.SetActive(b.visible);
            b.press = Mathf.MoveTowards(b.press, 0, dt * 6f);
            float s = b.down ? .92f : 1f + b.press * .04f;
            b.rt.localScale = Vector3.Lerp(b.rt.localScale, new Vector3(s, s, 1), 1 - Mathf.Exp(-30f * dt));
            b.rim.color = b.down ? RimLit : Rim;
        }
        // Guard reads as held for as long as the thumb is on it.
        guard.rim.color = held[Guard] ? RimLit : guard.rim.color;

        // Interact only exists while something can be interacted with.
        bool canInteract = playing && hud.PromptVisible;
        interactCg.alpha = Mathf.MoveTowards(interactCg.alpha, canInteract ? 1f : 0f, dt * 8f);
        if (canInteract)
        {
            string label = hud.PromptText; label = string.IsNullOrEmpty(label) ? "INTERACT" : label.ToUpperInvariant();
            if (interactLabel.text != label) interactLabel.text = label;
            float glow = .5f + .5f * Mathf.Sin(Time.unscaledTime * 5f);
            interactRim.color = Color.Lerp(Rim, RimLit, .55f + glow * .45f);
            interactPill.localScale = Vector3.one * (1f + glow * .03f);
        }

        // Stick: a faint ghost where thumbs usually rest; the real thing follows the thumb.
        float sc = Scale;
        if (stickFinger >= 0)
        {
            stickBase.anchoredPosition = stickCenter / sc;
            stickKnob.anchoredPosition = (stickKnobPos - stickCenter) / sc;
            stickCg.alpha = Mathf.MoveTowards(stickCg.alpha, 1f, dt * 10f);
        }
        else
        {
            stickBase.anchoredPosition = Vector2.Lerp(stickBase.anchoredPosition, StickHome, 1 - Mathf.Exp(-12f * dt));
            stickKnob.anchoredPosition = Vector2.Lerp(stickKnob.anchoredPosition, Vector2.zero, 1 - Mathf.Exp(-20f * dt));
            stickCg.alpha = Mathf.MoveTowards(stickCg.alpha, .32f, dt * 4f);
        }
    }
}
