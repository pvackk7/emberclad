using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// Screen-space HUD built in code: health, embers, prompts, title / death / victory screens.
public partial class Hud : MonoBehaviour
{
    Text rewardGainText;
    float rewardGainUntil;
    public void ShowRewards(int cinders, int xp)
    {
        rewardGainText.text = $"+{xp} XP";
        rewardGainUntil = Time.unscaledTime + 2f;
    }
    Game game; Font font; RectTransform root;
    EmberHealthBar hpBar;
    RectTransform bossFill, bossPanel, goArrow, titlePanel, deathPanel, victoryPanel, helpPanel, bannerRT, fade;
    RectTransform staminaFill, xpFill;
    Image staminaImage;
    Text levelText;
    float xpShown = -1, levelPunch; int shownLevel = -1;
    RectTransform promptRT; Text promptLabel; Image promptKey; Text promptKeyText;
    int promptFrame = -9; float promptFade;
    Text bannerText, fpsText;
    float fpsAccum, fpsWorst; int fpsFrames; bool fpsOn = false; Image fadeImg; CanvasGroup titleCg, bannerCg, deathCg, victoryCg;
    float manaPulse, bannerT, bannerDur, goT; bool goOn, titleOn; Enemy boss; float bossShown = 1;
    const float HpW = 302, BossW = 820;   // HpH moved to EmberHealthBar.ChH with the rest of the channel

    static readonly Color Dark = new Color(0.045f, 0.035f, 0.055f, 0.86f), Gold = new Color(0.94f, 0.76f, 0.53f, 1);
    // All game text is white Roman capitals (Cinzel): Ink for headings and values, Cream (soft white) for
    // captions and body text. Gold is kept for frames, handles and highlights, not for words.
    static readonly Color Ink = new Color(0.98f, 0.97f, 0.95f, 1), Cream = new Color(0.86f, 0.85f, 0.83f, 1);
    static readonly Color Stamina = new Color(.36f, .62f, .95f), StaminaLow = new Color(.24f, .36f, .62f);

    RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
    {
        var go = new GameObject(name, typeof(RectTransform)); var rt = (RectTransform)go.transform; rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = anchor; rt.pivot = pivot ?? anchor; rt.anchoredPosition = pos; rt.sizeDelta = size; return rt;
    }
    Image Img(string sprite, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color c, Vector2? pivot = null, bool sliced = false)
    {
        var rt = Rect(sprite, parent, anchor, pos, size, pivot); var im = rt.gameObject.AddComponent<Image>();
        im.sprite = Art.S(sprite); im.color = c; im.raycastTarget = false; if (sliced) im.type = Image.Type.Sliced; return im;
    }
    Text Txt(string s, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize, Color c, TextAnchor align = TextAnchor.MiddleCenter, Vector2? pivot = null, bool outline = true)
    {
        var rt = Rect("Text", parent, anchor, pos, size, pivot); var t = rt.gameObject.AddComponent<Text>();
        t.font = font; t.text = s; t.fontSize = fontSize; t.color = c; t.alignment = align; t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        if (outline) { var o = rt.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0.14f, 0.09f, 0.1f, 1); o.effectDistance = new Vector2(2.5f, -2.5f); var sh = rt.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0.14f, 0.09f, 0.1f, 1); sh.effectDistance = new Vector2(0, -5); }
        return t;
    }
    CanvasGroup Group(RectTransform rt, float a) { var g = rt.gameObject.AddComponent<CanvasGroup>(); g.alpha = a; g.blocksRaycasts = false; g.interactable = false; return g; }
    RectTransform FramedPanel(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var rt = Rect(name, parent, anchor, pos, size);
        rt.gameObject.AddComponent<MenuPlateGraphic>().raycastTarget = false;
        return rt;
    }
    RectTransform Full(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform)); var rt = (RectTransform)go.transform; rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; return rt;
    }

    /// Row of key caps + label. Returns total width.
    ///
    /// captionWidth is the room the caption actually has before it would run off
    /// whatever panel this row sits on. The caption wraps inside it rather than
    /// overflowing, which is the only thing stopping a long label from printing
    /// out past the edge of its own card.
    float KeyRow(Transform parent, Vector2 anchor, Vector2 pos, string[] keys, string label, float scale, int fontSize, float captionWidth = 600f)
    {
        float x = pos.x;
        foreach (var originalKey in keys)
        {
            string k = originalKey;
            string[] defaults = { "A", "D", "W", "S", "J", "K", "L", "SPACE", "F", "TAB", "R", "G", "E", "SHIFT" };
            int bindingIndex = System.Array.IndexOf(defaults, originalKey);
            if (TouchControls.Enabled)
            {
                // On a touch screen the caps name the on-screen control; W A S D collapse into one STICK cap.
                if (originalKey == "A" || originalKey == "S" || originalKey == "D") { if (System.Array.IndexOf(keys, "W") >= 0) continue; }
                k = TouchKey(originalKey);
            }
            else if (bindingIndex >= 0) k = MenuSettings.Binding(bindingIndex).ToString().ToUpperInvariant();
            bool wide = k.Length > 2; float w = (wide ? 150 : 64) * scale, h = 64 * scale;
            Img(wide ? "ui_keywide" : "ui_key", parent, anchor, new Vector2(x, pos.y), new Vector2(w, h), Color.white, new Vector2(0, 0.5f));
            Txt(k, parent, anchor, new Vector2(x + w / 2, pos.y + 5 * scale), new Vector2(w, h), (int)((wide ? 24 : 32) * scale), new Color(0.2f, 0.14f, 0.14f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), false);
            x += w + 6 * scale;
        }
        var t = Txt(label, parent, anchor, new Vector2(x + 8 * scale, pos.y), new Vector2(captionWidth, 64 * scale), fontSize, Cream, TextAnchor.MiddleLeft, new Vector2(0, 0.5f));
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        return x + 8 * scale + t.preferredWidth - pos.x;
    }

    /// A block of key rows centred on its parent, each row's caps and caption read as one
    /// unit and the block sits in the middle of the card instead of hugging the left edge.
    /// Returns the block's height.
    float KeyBlock(Transform parent, float top, string[][] keys, string[] labels, float rowGap = 46f)
    {
        var block = Rect("Keys", parent, new Vector2(.5f, 1), new Vector2(0, top), new Vector2(1, 1), new Vector2(0, 1));
        float widest = 0f;
        for (int i = 0; i < keys.Length; i++)
            widest = Mathf.Max(widest, KeyRow(block, new Vector2(0, 1), new Vector2(0, -21f - i * rowGap), keys[i], labels[i], .62f, 22, 380f));
        block.anchoredPosition = new Vector2(-widest * .5f, top);
        return keys.Length * rowGap;
    }

    public void Build(Game g)
    {
        game = g; font = Resources.Load<Font>("Fonts/Cinzel-Bold");
        var canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
        var sc = gameObject.AddComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1536, 864); sc.matchWidthOrHeight = 0.5f; // 1920x1080 / 1.25: the whole HUD draws 25% larger
        root = (RectTransform)transform;
        Vector2 TL = new Vector2(0, 1), TR = new Vector2(1, 1), TC = new Vector2(0.5f, 1), C = new Vector2(0.5f, 0.5f), RC = new Vector2(1, 0.5f);

        // --- player panel ---
        // Embers moved to the hotbar, so the panel loses that row. The portrait sets
        // the floor of the left column, so it shrinks too or the labels have nowhere
        // to move up into.
        var pp = FramedPanel("Knight panel", root, TL, new Vector2(28, -28), new Vector2(515, 146));
        Img("ui_round", pp, TL, new Vector2(14, -14), new Vector2(80, 76), new Color(0.2f, 0.13f, 0.14f, 1), null, true);
        Img("k_head", pp, TL, new Vector2(20, -18), new Vector2(68, 66), Color.white);
        cardName = Txt(CardName(), pp, TL, new Vector2(132, -13), new Vector2(330, 34), 24, Ink, TextAnchor.MiddleLeft);   // the run's nickname, KNIGHT if none
        cardName.resizeTextForBestFit = true; cardName.resizeTextMinSize = 14; cardName.resizeTextMaxSize = 24;
        // Health is a strip of coals with a flame at the leading edge. EmberHealthBar draws
        // its own iron frame, so the plate and the two flat fill rects that used to live
        // here are gone; its channel constants put the bar exactly where they sat, and the
        // taller rect is only headroom for the flame and sparks.
        hpBar = Rect("Health", pp, TL, new Vector2(126, -34), new Vector2(320, 52)).gameObject.AddComponent<EmberHealthBar>();
        Img("ui_round", pp, TL, new Vector2(132, -94), new Vector2(HpW + 12, 20), Dark, null, true);
        staminaImage = Img("ui_white", pp, TL, new Vector2(138, -98), new Vector2(HpW, 12), Stamina);
        staminaFill = staminaImage.rectTransform;
        Txt("STAMINA", pp, TL, new Vector2(20, -92), new Vector2(108, 26), 16, Cream, TextAnchor.MiddleLeft);
        levelText = Txt("LV 1", pp, TL, new Vector2(20, -120), new Vector2(108, 26), 18, Ink, TextAnchor.MiddleLeft);
        Img("ui_round", pp, TL, new Vector2(132, -122), new Vector2(HpW + 12, 14), Dark, null, true);
        xpFill = Img("ui_white", pp, TL, new Vector2(138, -125), new Vector2(0, 8), new Color(0.88f, 0.81f, 0.69f, 1)).rectTransform;

        rewardGainText = Txt("", root, TR, new Vector2(-32, -60), new Vector2(360, 40), 25, Cream, TextAnchor.UpperRight);
        BuildCinders();
        BuildSiphon();
        BuildDamage();
        BuildPrompt();

        // --- boss bar ---
        bossPanel = Rect("Boss", root, new Vector2(0.5f, 0), new Vector2(0, 130), new Vector2(BossW + 40, 90), TC);   // bottom centre, clear of the knight card and PAUSED
        Txt("THE BONE WARDEN", bossPanel, TC, new Vector2(0, 0), new Vector2(800, 40), 30, new Color(0.88f, 0.68f, 0.55f, 1));
        Img("ui_round", bossPanel, TC, new Vector2(0, -44), new Vector2(BossW + 12, 36), new Color(0.05f, 0.04f, 0.05f, 1), null, true);
        bossFill = Img("ui_white", bossPanel, TC, new Vector2(-BossW / 2, -50), new Vector2(BossW, 24), new Color(0.68f, 0.28f, 0.26f, 1), new Vector2(0, 1)).rectTransform;
        bossPanel.gameObject.SetActive(false);

        fpsText = Txt("", root, TR, new Vector2(-14, -10), new Vector2(260, 30), 22, new Color(0.65f, 1f, 0.72f, 0.95f), TextAnchor.UpperRight, new Vector2(1, 1));
        fpsText.enabled = fpsOn;

        // --- go arrow ---
        goArrow = Img("ui_arrow", root, RC, new Vector2(-70, 60), new Vector2(180, 120), Color.white).rectTransform;
        Txt("GO!", goArrow, C, new Vector2(-14, 4), new Vector2(160, 60), 40, new Color(0.25f, 0.14f, 0.1f), TextAnchor.MiddleCenter, null, false);
        goArrow.gameObject.SetActive(false);

        // --- banner ---
        bannerRT = Rect("Banner", root, C, new Vector2(0, 220), new Vector2(1400, 140)); bannerCg = Group(bannerRT, 0);
        bannerText = Txt("", bannerRT, C, Vector2.zero, new Vector2(1400, 140), 84, Ink);

        BuildMenu(); BuildOverlays(C); BuildHelp(C);

        fade = Full("Fade", root); fadeImg = fade.gameObject.AddComponent<Image>(); fadeImg.color = Color.black; fadeImg.raycastTarget = false;

        BuildIntro();
        BuildHotbar();
        introPanel.SetAsLastSibling();
    }

    void BuildOverlays(Vector2 C)
    {
        deathPanel = Full("Death", root); deathCg = Group(deathPanel, 0);
        var d = deathPanel.gameObject.AddComponent<Image>(); d.color = new Color(0.12f, 0.02f, 0.03f, 0.6f); d.raycastTarget = false;
        Txt("YOU FELL", deathPanel, C, new Vector2(0, 90), new Vector2(1400, 160), 130, new Color(1, 0.45f, 0.4f));
        deathPrompt = Txt(TouchControls.Enabled ? "Tap to rise" : "Press  R  to rise", deathPanel, C, new Vector2(0, -50), new Vector2(1400, 60), 40, Ink);
        victoryPanel = Full("Victory", root); victoryCg = Group(victoryPanel, 0);
        var v = victoryPanel.gameObject.AddComponent<Image>(); v.color = new Color(0.04f, 0.08f, 0.06f, 0.55f); v.raycastTarget = false;
    }

    void BuildHelp(Vector2 C)
    {
        helpPanel = Full("Help", root);
        var d = helpPanel.gameObject.AddComponent<Image>(); d.color = new Color(0.03f, 0.07f, 0.08f, 0.7f); d.raycastTarget = false;
        Txt("PAUSED", helpPanel, C, new Vector2(0, 300), new Vector2(1000, 120), 90, Ink);
        // The same plate as every other panel, instead of a stretched sprite.
        var box = Rect("Controls", helpPanel, C, new Vector2(0, -10), new Vector2(1000, 380), C); Vector2 L = new Vector2(0, 1);
        box.gameObject.AddComponent<MenuPlateGraphic>().raycastTarget = false;
        // Two even columns of keys and one-word captions; no explanations.
        string[][] leftKeys = { new[] { "W", "A", "S", "D" }, new[] { "SPACE" }, new[] { "F" }, new[] { "SHIFT" } };
        string[] leftLabels = { "Move", "Jump", "Roll", "Guard" };
        string[][] rightKeys = { new[] { "J" }, new[] { "K" }, new[] { "L" }, new[] { "G" } };
        string[] rightLabels = { "Attack", "Heavy", "Ember", "Item wheel" };
        for (int i = 0; i < 4; i++)
        {
            KeyRow(box, L, new Vector2(110, -64 - i * 78), leftKeys[i], leftLabels[i], 0.85f, 30, 300);
            KeyRow(box, L, new Vector2(590, -64 - i * 78), rightKeys[i], rightLabels[i], 0.85f, 30, 300);
        }
        Txt("Press  TAB  to resume", helpPanel, C, new Vector2(0, -260), new Vector2(1000, 60), 40, Ink);
        helpPanel.gameObject.SetActive(false);
    }

    // On the website, Play was already pressed on the page, so the first thing shown is the name prompt.
    // After that (Back, or returning from a run) the normal main menu is used.
    static bool webNickShown;
    public void ShowTitle()
    {
        titleOn = true; titlePanel.gameObject.SetActive(true); titleCg.alpha = 1;
        if (Application.platform == RuntimePlatform.WebGLPlayer && !webNickShown) { webNickShown = true; PlayOrContinue(); }
    }
    public void HideTitle(bool instant) { titleOn = false; if (instant) titleCg.alpha = 0; }
    // A lesson left on screen sits right where the pause card's footer goes, so it
    // steps aside for as long as the game is paused rather than printing through it.
    public void ShowHelp(bool on)
    {
        helpPanel.gameObject.SetActive(on);
        if (teachCard != null) teachCard.gameObject.SetActive(!on);
    }
    public void ShowGo() { goOn = true; goT = 0; goArrow.gameObject.SetActive(true); }
    public void HideGo() { goOn = false; goArrow.gameObject.SetActive(false); }
    /// Flash the ember slot: the cast was refused for want of health.
    public void PulseEmber() { manaPulse = 1; }
    /// Levelling restocked the knight: relight the whole strip of coals.
    public void LevelRestore() { if (hpBar != null) hpBar.Restore(); }
    /// Health leaving for an Ember ability burns to ash instead of flinching like a wound.
    /// Announced rather than inferred, because the bar cannot tell the two apart by value.
    public void MarkEmberSpend() { if (hpBar != null) hpBar.NoteEmberSpend(); }
    public void Banner(string s, float dur) { bannerText.text = s; bannerT = dur; bannerDur = dur; }
    public void ShowBoss(Enemy b) { boss = b; bossShown = 1; bossPanel.gameObject.SetActive(true); }
    public void HideBoss() { boss = null; bossPanel.gameObject.SetActive(false); }
    public void ShowDeath() { deathOn = true; ClearTeach(); DemoHotbar(false); }
    bool deathOn, victoryOn;

    Text victoryResult, victorySaved, cardName;
    static string CardName() { return string.IsNullOrEmpty(RunRecord.Nickname) ? "KNIGHT" : RunRecord.Nickname.ToUpperInvariant(); }
    public void ShowVictory(float time, int kills, int bestCombo, int falls)
    {
        victoryOn = true; Vector2 C = new Vector2(0.5f, 0.5f);
        Txt("FOREST CLEARED", victoryPanel, C, new Vector2(0, 300), new Vector2(1600, 160), 120, Ink);
        var box = Img("ui_round", victoryPanel, C, new Vector2(0, -20), new Vector2(820, 300), Dark, null, true).rectTransform;
        int m = (int)(time / 60), s = (int)(time % 60);
        string shownTime = RunRecord.LastClientMs > 0 ? RunRecord.FormatMs(RunRecord.LastClientMs) : $"{m}:{s:00}";
        string[] labels = { "Run time", "Skeletons shattered", "Best combo", "Deaths" };
        string[] values = { shownTime, kills.ToString(), bestCombo + " hits", falls.ToString() };
        for (int i = 0; i < 4; i++)
        {
            Txt(labels[i], box, new Vector2(0, 1), new Vector2(60, -40 - i * 62), new Vector2(400, 50), 36, Cream, TextAnchor.MiddleLeft);
            Txt(values[i], box, new Vector2(1, 1), new Vector2(-60, -40 - i * 62), new Vector2(300, 50), 38, Ink, TextAnchor.MiddleRight);
        }
        victorySaved = Txt(RunRecord.SaveHeading, victoryPanel, C, new Vector2(0, -192), new Vector2(1200, 50), 40, Ink);
        victoryResult = Txt(RunRecord.ResultText, victoryPanel, C, new Vector2(0, -232), new Vector2(1500, 44), 28, Cream);
        victoryPrompt = Txt(TouchControls.Enabled ? "Tap to return to the menu" : "Press  R  to return to the menu", victoryPanel, C, new Vector2(0, -290), new Vector2(1200, 60), 36, Cream);
    }

    void Update()
    {
        TickTouchLayout();
        bool introWasOpen = IntroOpen;
        TickIntro();
        if (!introWasOpen) TickMenu();
        if (victoryResult != null && victoryResult.text != RunRecord.ResultText) victoryResult.text = RunRecord.ResultText;
        if (victorySaved != null && victorySaved.text != RunRecord.SaveHeading) victorySaved.text = RunRecord.SaveHeading;
        if (cardName != null) { string n = CardName(); if (cardName.text != n) cardName.text = n; }
        if (Time.unscaledTime > rewardGainUntil) rewardGainText.text = "";
        float dt = Time.unscaledDeltaTime; var p = game.player;
        TickHotbar();
        // fade in
        if (fadeImg.color.a > 0) { var c = fadeImg.color; c.a = Mathf.MoveTowards(c.a, 0, dt * 1.6f); fadeImg.color = c; if (c.a <= 0) fade.gameObject.SetActive(false); }
        if (fpsText != null)
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f1Key.wasPressedThisFrame) { fpsOn = !fpsOn; fpsText.enabled = fpsOn; }
            if (fpsOn)
            {
                fpsAccum += dt; fpsFrames++; if (dt > fpsWorst) fpsWorst = dt;
                if (fpsAccum >= 0.25f)
                {
                    float avg = fpsFrames / fpsAccum;
                    fpsText.text = string.Format("{0:0} fps   {1:0.0} ms   min {2:0}", avg, 1000f * fpsAccum / fpsFrames, 1f / Mathf.Max(fpsWorst, 0.0001f));
                    fpsText.color = avg >= 55 ? new Color(0.65f, 1f, 0.72f, 0.95f) : avg >= 30 ? new Color(1f, 0.88f, 0.5f, 0.95f) : new Color(1f, 0.55f, 0.5f, 0.95f);
                    fpsAccum = 0; fpsFrames = 0; fpsWorst = 0;
                }
            }
        }
        // health -- p.hp stays the single source of truth; the bar only animates toward it
        hpBar.SetHealth(Mathf.Clamp01((float)p.hp / p.maxHp));
        // Behind the opaque title menu the bar keeps its state but stops rebuilding its
        // mesh. The pause menu is only a shade, so the flame has to keep burning there.
        hpBar.hidden = titleOn && !pauseMenu && titleCg.alpha > 0.99f;
        staminaFill.sizeDelta = new Vector2(HpW * Mathf.Clamp01(game.player.stamina / Player.MaxStamina), 12);
        staminaImage.color = game.player.stamina < Player.RollCost ? StaminaLow : Stamina;
        TickLevel(dt);
        TickCinders(dt);
        TickSiphon(dt);
        TickTeach(dt);
        TickDamage(dt);
        TickPrompt(dt);
        // embers -- drawn by the hotbar now; this only decays the kick PulseEmber gives it
        manaPulse = Mathf.MoveTowards(manaPulse, 0, dt * 3);
        // boss
        if (boss != null) { bossShown = Mathf.Lerp(bossShown, Mathf.Clamp01((float)boss.hp / boss.maxHp), 1 - Mathf.Exp(-12 * dt)); bossFill.sizeDelta = new Vector2(BossW * bossShown, 24); }
        // go arrow
        if (goOn) { goT += dt; goArrow.anchoredPosition = goBase + new Vector2(Mathf.Sin(goT * 7) * 16, 0); if (goT > 5f && !game.InCombat) HideGo(); }
        // banner
        if (bannerT > 0)
        {
            bannerT -= dt; float k = 1 - bannerT / bannerDur; bannerCg.alpha = Mathf.Clamp01(Mathf.Min(k * 8, (1 - k) * 4));
            float s = 1 + Mathf.Max(0, 0.4f - k * 3); bannerRT.localScale = new Vector3(s, s, 1);
        }
        else bannerCg.alpha = 0;
        // overlays
        titleCg.alpha = Mathf.MoveTowards(titleCg.alpha, titleOn ? 1 : 0, dt * 3);
        // The menu's animated backdrop rebuilds thousands of vertices a frame. Fully
        // faded out it is invisible, so switch it off instead of paying for it in play.
        if (!titleOn && titleCg.alpha <= 0 && titlePanel.gameObject.activeSelf) titlePanel.gameObject.SetActive(false);
        deathCg.alpha = Mathf.MoveTowards(deathCg.alpha, deathOn ? 1 : 0, dt * 1.5f);
        victoryCg.alpha = Mathf.MoveTowards(victoryCg.alpha, victoryOn ? 1 : 0, dt * 1.2f);
    }
}
