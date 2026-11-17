using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class EmbercladMenu : MonoBehaviour
{
    enum Page { Hidden, Title, Pause, Options, Controls }

    sealed class Choice
    {
        public string action;
        public RectTransform hit;
        public Text text;
        public CanvasGroup accent;
        public float glow;
    }

    static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
    static readonly Color Warm = new Color(0.94f, 0.77f, 0.54f);
    static readonly Color Dim = new Color(0.59f, 0.49f, 0.46f);
    static readonly Color Ember = new Color(1f, 0.49f, 0.27f);
    static readonly Color Night = new Color(0.045f, 0.032f, 0.055f);

    readonly List<Choice> titleChoices = new List<Choice>();
    readonly List<Choice> pauseChoices = new List<Choice>();
    readonly List<Choice> optionsChoices = new List<Choice>();
    readonly List<Choice> controlsChoices = new List<Choice>();

    Game game;
    Canvas canvas;
    Font font;
    Page page;
    bool fromPause;
    int selected;
    GameObject backdrop, titleScreen, pauseScreen, optionsScreen, controlsScreen;
    Text musicText, effectsText, notice;
    RectTransform sigil;
    float sigilTime;

    RectTransform MakeRect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        return rt;
    }

    RectTransform Fill(string name, Transform parent)
    {
        var rt = MakeRect(name, parent, Center, Vector2.zero, Vector2.zero);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    Image Rectangle(string name, Transform parent, Vector2 position, Vector2 size, Color color)
    {
        var img = MakeRect(name, parent, Center, position, size).gameObject.AddComponent<Image>();
        img.sprite = null;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    Text Lettering(string value, Transform parent, Vector2 position, Vector2 size, int sizePx, Color color)
    {
        var go = MakeRect("Lettering", parent, Center, position, size).gameObject;
        var label = go.AddComponent<Text>();
        label.font = font;
        label.fontSize = sizePx;
        label.text = value;
        label.color = color;
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;
        var shade = go.AddComponent<Shadow>();
        shade.effectColor = new Color(0.015f, 0.009f, 0.02f, 0.72f);
        shade.effectDistance = new Vector2(1.5f, -2.5f);
        return label;
    }

    static Texture2D Ring()
    {
        var texture = new Texture2D(128, 128, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < 128; y++)
        {
            for (int x = 0; x < 128; x++)
            {
                float dx = (x - 63.5f) / 64f;
                float dy = (y - 63.5f) / 64f;
                float radius = Mathf.Sqrt(dx * dx + dy * dy);
                float width = 0.020f + 0.004f * Mathf.Sin(x * 0.41f + y * 0.33f);
                float ring = Mathf.Clamp01((radius - 0.66f + width) * 65f) * Mathf.Clamp01((0.66f + width - radius) * 65f);
                texture.SetPixel(x, y, new Color(0.58f, 0.24f, 0.14f, ring * 0.78f));
            }
        }
        texture.Apply();
        return texture;
    }

    void Sigil(Transform parent, Vector2 position, float scale)
    {
        var rt = MakeRect("Ember Sigil", parent, Center, position, new Vector2(320, 320) * scale);
        var ring = rt.gameObject.AddComponent<RawImage>();
        ring.texture = Ring();
        ring.color = Color.white;
        ring.raycastTarget = false;
        Rectangle("Ember Core", rt, Vector2.zero, new Vector2(5, 230) * scale, new Color(0.97f, 0.39f, 0.19f, 0.85f));
        var top = Rectangle("Upper Ember", rt, new Vector2(0, 104) * scale, new Vector2(17, 17) * scale, Ember);
        top.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
        var bottom = Rectangle("Lower Ember", rt, new Vector2(0, -104) * scale, new Vector2(13, 13) * scale, Ember);
        bottom.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
        sigil = rt;
    }

    Choice AddChoice(List<Choice> collection, Transform parent, string action, string label, float y)
    {
        var hit = MakeRect(action, parent, Center, new Vector2(0, y), new Vector2(570, 76));
        var accentTransform = MakeRect("Selection Flourish", hit, Center, Vector2.zero, new Vector2(570, 76));
        var group = accentTransform.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0;
        Rectangle("Left Stroke", accentTransform, new Vector2(-205, 0), new Vector2(106, 2), Warm);
        Rectangle("Right Stroke", accentTransform, new Vector2(205, 0), new Vector2(106, 2), Warm);
        var left = Rectangle("Left Diamond", accentTransform, new Vector2(-263, 0), new Vector2(9, 9), Warm);
        var right = Rectangle("Right Diamond", accentTransform, new Vector2(263, 0), new Vector2(9, 9), Warm);
        left.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
        right.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
        var text = Lettering(label, hit, Vector2.zero, new Vector2(390, 65), 38, Dim);
        var choice = new Choice { action = action, hit = hit, text = text, accent = group };
        collection.Add(choice);
        return choice;
    }

    GameObject Screen(string name)
    {
        return Fill(name, transform).gameObject;
    }

    void MakeTitle()
    {
        titleScreen = Screen("Title Screen");
        Sigil(titleScreen.transform, new Vector2(0, 286), 0.95f);
        Lettering("EMBERCLAD", titleScreen.transform, new Vector2(0, 270), new Vector2(1400, 140), 103, Warm);
        AddChoice(titleChoices, titleScreen.transform, "play", "PLAY", 57);
        AddChoice(titleChoices, titleScreen.transform, "options", "OPTIONS", -45);
        AddChoice(titleChoices, titleScreen.transform, "exit", "EXIT", -147);
    }

    void MakePause()
    {
        pauseScreen = Screen("Pause Screen");
        Lettering("PAUSED", pauseScreen.transform, new Vector2(0, 265), new Vector2(1000, 125), 87, Warm);
        Rectangle("Pause Divider", pauseScreen.transform, new Vector2(0, 180), new Vector2(300, 2), new Color(Warm.r, Warm.g, Warm.b, 0.44f));
        AddChoice(pauseChoices, pauseScreen.transform, "resume", "RESUME", 65);
        AddChoice(pauseChoices, pauseScreen.transform, "options", "OPTIONS", -37);
        AddChoice(pauseChoices, pauseScreen.transform, "return", "RETURN TO MENU", -139);
    }

    void MakeOptions()
    {
        optionsScreen = Screen("Options Screen");
        Lettering("OPTIONS", optionsScreen.transform, new Vector2(0, 280), new Vector2(1150, 120), 85, Warm);
        musicText = AddChoice(optionsChoices, optionsScreen.transform, "music", "MUSIC  100%", 94).text;
        effectsText = AddChoice(optionsChoices, optionsScreen.transform, "effects", "EFFECTS  100%", 4).text;
        AddChoice(optionsChoices, optionsScreen.transform, "controls", "CONTROLS", -86);
        AddChoice(optionsChoices, optionsScreen.transform, "back", "BACK", -176);
        Lettering("LEFT / RIGHT TO ADJUST SOUND", optionsScreen.transform, new Vector2(0, -280), new Vector2(900, 48), 22, Dim);
    }

    void MakeControls()
    {
        controlsScreen = Screen("Controls Screen");
        Lettering("CONTROLS", controlsScreen.transform, new Vector2(0, 290), new Vector2(1120, 120), 84, Warm);
        string[] info =
        {
            "MOVE    W A S D",
            "ATTACK    J / LEFT MOUSE",
            "HEAVY ATTACK    K / RIGHT MOUSE",
            "DODGE    SHIFT",
            "JUMP    SPACE",
            "EMBER MAGIC    L"
        };
        for (int i = 0; i < info.Length; i++)
            Lettering(info[i], controlsScreen.transform, new Vector2(0, 155 - i * 60), new Vector2(1250, 51), 30, new Color(0.83f, 0.76f, 0.7f));
        AddChoice(controlsChoices, controlsScreen.transform, "back", "BACK", -256);
    }

    void MakeBackdrop()
    {
        backdrop = Screen("Painted Tavern");
        backdrop.transform.SetAsFirstSibling();
        Texture2D painting = Resources.Load<Texture2D>("Menu/TitleBackground");
        if (painting != null)
        {
            var image = Fill("Background Painting", backdrop.transform).gameObject.AddComponent<RawImage>();
            image.texture = painting;
            image.raycastTarget = false;
            var fitter = image.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = (float)painting.width / painting.height;
        }
        else
        {
            var image = Fill("Fallback Forest", backdrop.transform).gameObject.AddComponent<Image>();
            image.sprite = Art.S("bg_mid");
            image.color = new Color(0.44f, 0.33f, 0.38f, 1);
            image.raycastTarget = false;
        }
        var shadow = Fill("Tint", backdrop.transform).gameObject.AddComponent<Image>();
        shadow.color = painting != null ? new Color(0.035f, 0.024f, 0.05f, 0.10f) : new Color(0.03f, 0.023f, 0.05f, 0.48f);
        shadow.raycastTarget = false;
    }

    public void Initialize(Game owner)
    {
        game = owner;
        font = Resources.Load<Font>("Fonts/GameFont") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        MakeBackdrop();
        var veil = Fill("Gameplay Veil", transform).gameObject.AddComponent<Image>();
        veil.color = new Color(Night.r, Night.g, Night.b, 0.82f);
        veil.raycastTarget = false;
        pauseVeil = veil.gameObject;
        MakeTitle();
        MakePause();
        MakeOptions();
        MakeControls();
        notice = Lettering("", transform, new Vector2(0, -410), new Vector2(1300, 56), 26, Warm);
        Hide();
    }

    GameObject pauseVeil;

    List<Choice> Active()
    {
        switch (page)
        {
            case Page.Title: return titleChoices;
            case Page.Pause: return pauseChoices;
            case Page.Options: return optionsChoices;
            case Page.Controls: return controlsChoices;
            default: return titleChoices;
        }
    }

    void SetPage(Page next)
    {
        page = next;
        canvas.enabled = next != Page.Hidden;
        if (next == Page.Hidden) return;
        backdrop.SetActive(!fromPause);
        pauseVeil.SetActive(fromPause);
        titleScreen.SetActive(next == Page.Title);
        pauseScreen.SetActive(next == Page.Pause);
        optionsScreen.SetActive(next == Page.Options);
        controlsScreen.SetActive(next == Page.Controls);
        selected = 0;
        notice.text = "";
        RefreshSettings();
        foreach (var choice in Active()) choice.glow = 0;
    }

    public void ShowTitle()
    {
        fromPause = false;
        SetPage(Page.Title);
    }

    public void ShowPause()
    {
        fromPause = true;
        SetPage(Page.Pause);
    }

    public void Hide()
    {
        page = Page.Hidden;
        if (canvas != null) canvas.enabled = false;
    }

    void RefreshSettings()
    {
        if (musicText != null) musicText.text = "MUSIC    " + Mathf.RoundToInt(Sfx.MusicLevel * 100f) + "%";
        if (effectsText != null) effectsText.text = "EFFECTS    " + Mathf.RoundToInt(Sfx.EffectsLevel * 100f) + "%";
    }

    void GoBack()
    {
        if (page == Page.Controls) SetPage(Page.Options);
        else if (page == Page.Options) SetPage(fromPause ? Page.Pause : Page.Title);
        else if (page == Page.Pause) game.ResumeFromMenu();
    }

    void Adjust(float delta)
    {
        if (page != Page.Options) return;
        var action = Active()[selected].action;
        if (action == "music") Sfx.SetMusicLevel(Mathf.Round((Sfx.MusicLevel + delta) * 20f) / 20f);
        if (action == "effects") Sfx.SetEffectsLevel(Mathf.Round((Sfx.EffectsLevel + delta) * 20f) / 20f);
        RefreshSettings();
    }

    void Activate()
    {
        var current = Active();
        if (selected < 0 || selected >= current.Count) return;
        string action = current[selected].action;
        Sfx.Play("ui_start", 0.25f, 0);
        switch (action)
        {
            case "play": game.BeginFromMenu(); break;
            case "resume": game.ResumeFromMenu(); break;
            case "options": SetPage(Page.Options); break;
            case "controls": SetPage(Page.Controls); break;
            case "back": GoBack(); break;
            case "return": game.ReturnToMenu(); break;
            case "music":
            case "effects": Adjust(0.05f); break;
            case "exit":
#if UNITY_WEBGL && !UNITY_EDITOR
                notice.text = "CLOSE THE BROWSER TAB TO EXIT";
#elif UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
                break;
        }
    }

    public void Tick()
    {
        if (page == Page.Hidden) return;
        var keys = Keyboard.current;
        var mouse = Mouse.current;
        var choices = Active();
        if (keys != null)
        {
            if (keys.escapeKey.wasPressedThisFrame || keys.backspaceKey.wasPressedThisFrame)
            {
                GoBack();
                return;
            }
            if (keys.upArrowKey.wasPressedThisFrame || keys.wKey.wasPressedThisFrame)
                selected = (selected + choices.Count - 1) % choices.Count;
            if (keys.downArrowKey.wasPressedThisFrame || keys.sKey.wasPressedThisFrame)
                selected = (selected + 1) % choices.Count;
            if (keys.leftArrowKey.wasPressedThisFrame || keys.aKey.wasPressedThisFrame) Adjust(-0.05f);
            if (keys.rightArrowKey.wasPressedThisFrame || keys.dKey.wasPressedThisFrame) Adjust(0.05f);
        }

        if (mouse != null)
        {
            Vector2 cursor = mouse.position.ReadValue();
            for (int i = 0; i < choices.Count; i++)
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(choices[i].hit, cursor)) continue;
                if (mouse.delta.ReadValue().sqrMagnitude > 0.1f || mouse.leftButton.wasPressedThisFrame)
                    selected = i;
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    Activate();
                    return;
                }
                break;
            }
        }
        if (keys != null && (keys.enterKey.wasPressedThisFrame || keys.numpadEnterKey.wasPressedThisFrame || keys.spaceKey.wasPressedThisFrame))
            Activate();
        else if (Game.autopilot && GameInput.Confirm && page == Page.Title)
            game.BeginFromMenu();
    }

    void Update()
    {
        if (page == Page.Hidden) return;
        float dt = Time.unscaledDeltaTime;
        sigilTime += dt;
        if (sigil != null) sigil.localScale = Vector3.one * (1f + Mathf.Sin(sigilTime * 1.5f) * 0.006f);
        var current = Active();
        for (int i = 0; i < current.Count; i++)
        {
            var choice = current[i];
            choice.glow = Mathf.MoveTowards(choice.glow, selected == i ? 1f : 0f, dt * 6f);
            choice.accent.alpha = choice.glow;
            choice.text.color = Color.Lerp(Dim, Warm, choice.glow);
            choice.hit.localScale = Vector3.one * (1f + 0.035f * choice.glow);
        }
    }
}