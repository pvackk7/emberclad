using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// Screen-space HUD built in code: health, embers, prompts, title / death / victory screens.
public class Hud : MonoBehaviour
{
    Game game; Font font; RectTransform root;
    RectTransform hpFill, hpTrail, bossFill, bossPanel, goArrow, combo, promptPanel, titlePanel, deathPanel, victoryPanel, helpPanel, bannerRT, fade;
    Image[] embers = new Image[3]; Text comboText, bannerText, pressText; Image fadeImg; CanvasGroup titleCg, promptCg, bannerCg, deathCg, victoryCg;
    float hpShown = 1, trail = 1, comboPunch, manaPulse, bannerT, bannerDur, goT, promptT; bool goOn, titleOn; int lastCombo, promptIdx = -1, shownPrompt = -1; Enemy boss; float bossShown = 1;
    const float HpW = 340, BossW = 820;

    static readonly Color Dark = new Color(0.10f, 0.08f, 0.10f, 0.78f), Cream = new Color(0.98f, 0.94f, 0.84f, 1), Gold = new Color(1f, 0.82f, 0.42f, 1);

    class Prompt { public string[] keys; public string text; public System.Func<bool> start, done; }
    List<Prompt> prompts;

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
    RectTransform Full(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform)); var rt = (RectTransform)go.transform; rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; return rt;
    }

    /// Row of key caps + label. Returns total width.
    float KeyRow(Transform parent, Vector2 anchor, Vector2 pos, string[] keys, string label, float scale, int fontSize)
    {
        float x = pos.x;
        foreach (var k in keys)
        {
            bool wide = k.Length > 2; float w = (wide ? 150 : 64) * scale, h = 64 * scale;
            Img(wide ? "ui_keywide" : "ui_key", parent, anchor, new Vector2(x, pos.y), new Vector2(w, h), Color.white, new Vector2(0, 0.5f));
            Txt(k, parent, anchor, new Vector2(x + w / 2, pos.y + 5 * scale), new Vector2(w, h), (int)((wide ? 24 : 32) * scale), new Color(0.2f, 0.14f, 0.14f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), false);
            x += w + 6 * scale;
        }
        var t = Txt(label, parent, anchor, new Vector2(x + 8 * scale, pos.y), new Vector2(600, 64 * scale), fontSize, Cream, TextAnchor.MiddleLeft, new Vector2(0, 0.5f));
        return x + 8 * scale + t.preferredWidth - pos.x;
    }

    public void Build(Game g)
    {
        game = g; font = Resources.Load<Font>("Fonts/GameFont");
        var canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
        var sc = gameObject.AddComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;
        root = (RectTransform)transform;
        Vector2 TL = new Vector2(0, 1), TR = new Vector2(1, 1), BL = Vector2.zero, BC = new Vector2(0.5f, 0), TC = new Vector2(0.5f, 1), C = new Vector2(0.5f, 0.5f), RC = new Vector2(1, 0.5f);

        // --- player panel ---
        var pp = Img("ui_round", root, TL, new Vector2(28, -28), new Vector2(560, 150), Dark, null, true).rectTransform;
        Img("ui_round", pp, TL, new Vector2(14, -14), new Vector2(122, 122), new Color(0.32f, 0.2f, 0.16f, 1), null, true);
        Img("k_head", pp, TL, new Vector2(18, -20), new Vector2(112, 107), Color.white);
        Txt("KNIGHT", pp, TL, new Vector2(152, -12), new Vector2(200, 34), 24, Gold, TextAnchor.MiddleLeft);
        Img("ui_round", pp, TL, new Vector2(150, -50), new Vector2(HpW + 12, 40), new Color(0.05f, 0.04f, 0.05f, 1), null, true);
        hpTrail = Img("ui_white", pp, TL, new Vector2(156, -56), new Vector2(HpW, 28), new Color(1, 0.9f, 0.5f, 1)).rectTransform;
        hpFill = Img("ui_white", pp, TL, new Vector2(156, -56), new Vector2(HpW, 28), new Color(0.9f, 0.27f, 0.25f, 1)).rectTransform;
        Img("ui_white", pp, TL, new Vector2(156, -56), new Vector2(HpW, 9), new Color(1, 1, 1, 0.22f));
        Img("ui_heart", pp, TL, new Vector2(136, -48), new Vector2(46, 44), Color.white);
        for (int i = 0; i < 3; i++) embers[i] = Img("ui_ember", pp, TL, new Vector2(190 + i * 50, -94), new Vector2(46, 46), Color.white);
        Txt("EMBERS", pp, TL, new Vector2(346, -100), new Vector2(160, 34), 20, new Color(1, 0.75f, 0.45f, 0.9f), TextAnchor.MiddleLeft);
        // Khalid tag
        var kp = Img("ui_round", root, TL, new Vector2(28, -186), new Vector2(230, 70), Dark, null, true).rectTransform;
        Img("p_head", kp, TL, new Vector2(8, -4), new Vector2(68, 62), Color.white);
        Txt("Khalid", kp, TL, new Vector2(84, -16), new Vector2(140, 38), 26, Cream, TextAnchor.MiddleLeft);

        // --- boss bar ---
        bossPanel = Rect("Boss", root, TC, new Vector2(0, -40), new Vector2(BossW + 40, 90));
        Txt("THE BONE WARDEN", bossPanel, TC, new Vector2(0, 0), new Vector2(800, 40), 30, new Color(0.85f, 1f, 0.75f, 1));
        Img("ui_round", bossPanel, TC, new Vector2(0, -44), new Vector2(BossW + 12, 36), new Color(0.05f, 0.04f, 0.05f, 1), null, true);
        bossFill = Img("ui_white", bossPanel, TC, new Vector2(-BossW / 2, -50), new Vector2(BossW, 24), new Color(0.55f, 0.85f, 0.4f, 1), new Vector2(0, 1)).rectTransform;
        bossPanel.gameObject.SetActive(false);

        // --- combo counter ---
        combo = Rect("Combo", root, TR, new Vector2(-60, -220), new Vector2(400, 120));
        comboText = Txt("", combo, C, Vector2.zero, new Vector2(400, 120), 64, Gold, TextAnchor.MiddleRight);
        Txt("HITS", combo, C, new Vector2(0, -56), new Vector2(400, 40), 26, Cream, TextAnchor.MiddleRight);
        combo.gameObject.SetActive(false);

        // --- go arrow ---
        goArrow = Img("ui_arrow", root, RC, new Vector2(-70, 60), new Vector2(180, 120), Color.white).rectTransform;
        Txt("GO!", goArrow, C, new Vector2(-14, 4), new Vector2(160, 60), 40, new Color(0.25f, 0.14f, 0.1f), TextAnchor.MiddleCenter, null, false);
        goArrow.gameObject.SetActive(false);

        // --- legend (always visible) ---
        var lg = Img("ui_round", root, BL, new Vector2(24, 24), new Vector2(1010, 56), new Color(0.1f, 0.08f, 0.1f, 0.6f), null, true).rectTransform;
        float lx = 16;
        (string[], string)[] legend = { (new[] { "WASD" }, "Move"), (new[] { "SPACE" }, "Jump"), (new[] { "SHIFT" }, "Dodge"), (new[] { "J" }, "Attack"), (new[] { "K" }, "Heavy"), (new[] { "L" }, "Ember"), (new[] { "TAB" }, "Help") };
        foreach (var (keys, label) in legend) lx += KeyRow(lg, new Vector2(0, 0.5f), new Vector2(lx, 0), keys, label, 0.56f, 22) + 20;
        lg.sizeDelta = new Vector2(lx + 4, 56);

        // --- contextual prompt ---
        promptPanel = Img("ui_round", root, BC, new Vector2(0, 110), new Vector2(900, 104), Dark, null, true).rectTransform; promptCg = Group(promptPanel, 0);

        // --- banner ---
        bannerRT = Rect("Banner", root, C, new Vector2(0, 220), new Vector2(1400, 140)); bannerCg = Group(bannerRT, 0);
        bannerText = Txt("", bannerRT, C, Vector2.zero, new Vector2(1400, 140), 84, Gold);

        BuildTitle(C, BC); BuildOverlays(C); BuildHelp(C);

        fade = Full("Fade", root); fadeImg = fade.gameObject.AddComponent<Image>(); fadeImg.color = Color.black; fadeImg.raycastTarget = false;

        var p = game.player;
        prompts = new List<Prompt>
        {
            new Prompt { keys = new[] { "W", "A", "S", "D" }, text = "Move  (up / down walks into the depth of the path)", start = () => true, done = () => p.pos.x > 4.5f },
            new Prompt { keys = new[] { "SPACE", "SHIFT" }, text = "Jump  and  Dodge-roll through danger", start = () => p.pos.x > 4.5f, done = () => (p.usedJump && p.usedDodge) || game.EncounterIndex > 0 || game.InCombat },
            new Prompt { keys = new[] { "J" }, text = "Attack!  Mash it to chain a 4-hit combo", start = () => game.InCombat, done = () => p.maxCombo >= 6 || game.EncounterIndex > 0 },
            new Prompt { keys = new[] { "K" }, text = "Heavy smash launches skeletons.  Jump + K to slam!", start = () => game.EncounterIndex > 0, done = () => p.usedHeavy && promptT > 4f },
            new Prompt { keys = new[] { "L" }, text = "Ember Burst: spend an ember, burn the whole line", start = () => game.EncounterIndex > 0 && game.InCombat, done = () => p.usedMagic && promptT > 3f },
            new Prompt { keys = new string[0], text = "Khalid digs up berries after a fight. Grab them to heal!", start = () => game.EncounterIndex > 1, done = () => promptT > 6f },
        };
    }

    void BuildTitle(Vector2 C, Vector2 BC)
    {
        titlePanel = Full("Title", root); titleCg = Group(titlePanel, 0);
        var dim = titlePanel.gameObject.AddComponent<Image>(); dim.color = new Color(0.03f, 0.07f, 0.08f, 0.62f); dim.raycastTarget = false;
        var logo = Art.S("ui_logo"); Img("ui_logo", titlePanel, C, new Vector2(0, 250), new Vector2(logo.rect.width, logo.rect.height) * 0.95f, Color.white);
        Txt("A forest. A rusted knight. A red panda named Khalid.", titlePanel, C, new Vector2(0, 80), new Vector2(1400, 50), 34, Cream);
        var box = Img("ui_round", titlePanel, C, new Vector2(0, -120), new Vector2(1180, 290), Dark, null, true).rectTransform;
        Vector2 L = new Vector2(0, 1);
        KeyRow(box, L, new Vector2(50, -66), new[] { "W", "A", "S", "D" }, "Move", 0.85f, 32);
        KeyRow(box, L, new Vector2(50, -146), new[] { "SPACE" }, "Jump", 0.85f, 32);
        KeyRow(box, L, new Vector2(50, -226), new[] { "SHIFT" }, "Dodge-roll", 0.85f, 32);
        KeyRow(box, L, new Vector2(640, -66), new[] { "J" }, "Attack (mash for combos)", 0.85f, 32);
        KeyRow(box, L, new Vector2(640, -146), new[] { "K" }, "Heavy smash / air slam", 0.85f, 32);
        KeyRow(box, L, new Vector2(640, -226), new[] { "L" }, "Ember Burst (magic)", 0.85f, 32);
        pressText = Txt("Press  ENTER  to begin", titlePanel, C, new Vector2(0, -330), new Vector2(1200, 70), 50, Gold);
        Txt("Arrow keys + Z / X / C also work  ·  Mouse: left = attack, right = heavy  ·  Gamepad supported", titlePanel, C, new Vector2(0, -410), new Vector2(1600, 40), 24, new Color(1, 1, 1, 0.7f));
    }

    void BuildOverlays(Vector2 C)
    {
        deathPanel = Full("Death", root); deathCg = Group(deathPanel, 0);
        var d = deathPanel.gameObject.AddComponent<Image>(); d.color = new Color(0.12f, 0.02f, 0.03f, 0.6f); d.raycastTarget = false;
        Txt("YOU FELL", deathPanel, C, new Vector2(0, 90), new Vector2(1400, 160), 130, new Color(1, 0.45f, 0.4f));
        Txt("Khalid nudges you awake...", deathPanel, C, new Vector2(0, -30), new Vector2(1400, 60), 38, Cream);
        Txt("Press  R  to rise again at the last checkpoint", deathPanel, C, new Vector2(0, -120), new Vector2(1400, 60), 42, Gold);
        victoryPanel = Full("Victory", root); victoryCg = Group(victoryPanel, 0);
        var v = victoryPanel.gameObject.AddComponent<Image>(); v.color = new Color(0.04f, 0.08f, 0.06f, 0.55f); v.raycastTarget = false;
    }

    void BuildHelp(Vector2 C)
    {
        helpPanel = Full("Help", root);
        var d = helpPanel.gameObject.AddComponent<Image>(); d.color = new Color(0.03f, 0.07f, 0.08f, 0.7f); d.raycastTarget = false;
        Txt("PAUSED", helpPanel, C, new Vector2(0, 300), new Vector2(1000, 120), 90, Gold);
        var box = Img("ui_round", helpPanel, C, new Vector2(0, -20), new Vector2(1180, 450), Dark, null, true).rectTransform; Vector2 L = new Vector2(0, 1);
        KeyRow(box, L, new Vector2(50, -66), new[] { "W", "A", "S", "D" }, "Move", 0.85f, 32);
        KeyRow(box, L, new Vector2(50, -146), new[] { "SPACE" }, "Jump", 0.85f, 32);
        KeyRow(box, L, new Vector2(50, -226), new[] { "SHIFT" }, "Dodge-roll (invincible)", 0.85f, 32);
        KeyRow(box, L, new Vector2(640, -66), new[] { "J" }, "Attack  (J J J J combo)", 0.85f, 32);
        KeyRow(box, L, new Vector2(640, -146), new[] { "K" }, "Heavy / launcher / air slam", 0.85f, 32);
        KeyRow(box, L, new Vector2(640, -226), new[] { "L" }, "Ember Burst", 0.85f, 32);
        Txt("Tips:  launch with K, then jump and keep hitting J to juggle.\nEmbers refill as you land hits.  Khalid pounces on skeletons to stun them.", box, L, new Vector2(50, -300), new Vector2(1080, 110), 28, Cream, TextAnchor.UpperLeft);
        Txt("Press  TAB  to resume", helpPanel, C, new Vector2(0, -310), new Vector2(1000, 60), 40, Gold);
        helpPanel.gameObject.SetActive(false);
    }

    public void ShowTitle() { titleOn = true; titleCg.alpha = 1; }
    public void HideTitle(bool instant) { titleOn = false; if (instant) titleCg.alpha = 0; }
    public void ShowHelp(bool on) { helpPanel.gameObject.SetActive(on); }
    public void ShowGo() { goOn = true; goT = 0; goArrow.gameObject.SetActive(true); }
    public void HideGo() { goOn = false; goArrow.gameObject.SetActive(false); }
    public void PulseMana() { manaPulse = 1; }
    public void Banner(string s, float dur) { bannerText.text = s; bannerT = dur; bannerDur = dur; }
    public void ShowBoss(Enemy b) { boss = b; bossShown = 1; bossPanel.gameObject.SetActive(true); }
    public void HideBoss() { boss = null; bossPanel.gameObject.SetActive(false); }
    public void ShowDeath() { deathOn = true; }
    bool deathOn, victoryOn;

    public void ShowVictory(float time, int kills, int bestCombo, int falls)
    {
        victoryOn = true; Vector2 C = new Vector2(0.5f, 0.5f);
        Txt("FOREST CLEARED", victoryPanel, C, new Vector2(0, 300), new Vector2(1600, 160), 120, Gold);
        Txt("The shrine burns again. Khalid approves.", victoryPanel, C, new Vector2(0, 190), new Vector2(1400, 60), 36, Cream);
        var box = Img("ui_round", victoryPanel, C, new Vector2(0, -20), new Vector2(820, 300), Dark, null, true).rectTransform;
        int m = (int)(time / 60), s = (int)(time % 60);
        string[] labels = { "Time", "Skeletons shattered", "Best combo", "Falls" };
        string[] values = { $"{m}:{s:00}", kills.ToString(), bestCombo + " hits", falls.ToString() };
        for (int i = 0; i < 4; i++)
        {
            Txt(labels[i], box, new Vector2(0, 1), new Vector2(60, -40 - i * 62), new Vector2(400, 50), 36, Cream, TextAnchor.MiddleLeft);
            Txt(values[i], box, new Vector2(1, 1), new Vector2(-60, -40 - i * 62), new Vector2(300, 50), 38, Gold, TextAnchor.MiddleRight);
        }
        Txt("Press  R  to play again", victoryPanel, C, new Vector2(0, -250), new Vector2(1200, 60), 44, Gold);
    }

    void SetPrompt(Prompt p)
    {
        for (int i = promptPanel.childCount - 1; i >= 0; i--) Destroy(promptPanel.GetChild(i).gameObject);
        float w = KeyRow(promptPanel, new Vector2(0, 0.5f), new Vector2(30, 0), p.keys, p.text, 0.9f, 32);
        promptPanel.sizeDelta = new Vector2(w + 70, 104);
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime; var p = game.player;
        // fade in
        if (fadeImg.color.a > 0) { var c = fadeImg.color; c.a = Mathf.MoveTowards(c.a, 0, dt * 1.6f); fadeImg.color = c; if (c.a <= 0) fade.gameObject.SetActive(false); }
        // health
        float hp = Mathf.Clamp01((float)p.hp / p.maxHp);
        hpShown = Mathf.Lerp(hpShown, hp, 1 - Mathf.Exp(-18 * dt)); trail = hp > trail ? hp : Mathf.MoveTowards(trail, hp, dt * 0.35f);
        hpFill.sizeDelta = new Vector2(HpW * hpShown, 28); hpTrail.sizeDelta = new Vector2(HpW * trail, 28);
        // embers
        manaPulse = Mathf.MoveTowards(manaPulse, 0, dt * 3);
        for (int i = 0; i < 3; i++)
        {
            bool on = i < p.mana; embers[i].sprite = Art.S(on ? "ui_ember" : "ui_ember_off");
            float s = 1 + (on ? Mathf.Sin(Time.unscaledTime * 5 + i) * 0.05f : 0) + manaPulse * 0.35f; embers[i].rectTransform.localScale = new Vector3(s, s, 1);
        }
        // combo
        if (p.comboHits >= 3)
        {
            if (!combo.gameObject.activeSelf) combo.gameObject.SetActive(true);
            if (p.comboHits != lastCombo) { comboPunch = 1; comboText.text = p.comboHits.ToString(); }
            comboPunch = Mathf.MoveTowards(comboPunch, 0, dt * 6); float s = 1 + comboPunch * 0.45f; combo.localScale = new Vector3(s, s, 1);
            comboText.color = Color.Lerp(Gold, new Color(1, 0.45f, 0.25f), Mathf.Clamp01(p.comboHits / 30f));
        }
        else if (combo.gameObject.activeSelf) combo.gameObject.SetActive(false);
        lastCombo = p.comboHits;
        // boss
        if (boss != null) { bossShown = Mathf.Lerp(bossShown, Mathf.Clamp01((float)boss.hp / boss.maxHp), 1 - Mathf.Exp(-12 * dt)); bossFill.sizeDelta = new Vector2(BossW * bossShown, 24); }
        // go arrow
        if (goOn) { goT += dt; goArrow.anchoredPosition = new Vector2(-70 + Mathf.Sin(goT * 7) * 16, 60); if (goT > 5f && !game.InCombat) HideGo(); }
        // banner
        if (bannerT > 0)
        {
            bannerT -= dt; float k = 1 - bannerT / bannerDur; bannerCg.alpha = Mathf.Clamp01(Mathf.Min(k * 8, (1 - k) * 4));
            float s = 1 + Mathf.Max(0, 0.4f - k * 3); bannerRT.localScale = new Vector3(s, s, 1);
        }
        else bannerCg.alpha = 0;
        // overlays
        titleCg.alpha = Mathf.MoveTowards(titleCg.alpha, titleOn ? 1 : 0, dt * 3);
        if (titleOn) pressText.color = new Color(Gold.r, Gold.g, Gold.b, 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 4));
        deathCg.alpha = Mathf.MoveTowards(deathCg.alpha, deathOn ? 1 : 0, dt * 1.5f);
        victoryCg.alpha = Mathf.MoveTowards(victoryCg.alpha, victoryOn ? 1 : 0, dt * 1.2f);
        // prompts
        bool playing = game.state == Game.State.Playing;
        if (playing && promptIdx < prompts.Count)
        {
            if (promptIdx < 0) promptIdx = 0;
            if (promptIdx < prompts.Count)
            {
                var pr = prompts[promptIdx];
                if (shownPrompt != promptIdx) { if (pr.start()) { shownPrompt = promptIdx; promptT = 0; SetPrompt(pr); } }
                else { promptT += dt; if ((promptT > 1.5f && pr.done()) || promptT > 16f) promptIdx++; }
            }
        }
        bool showPrompt = playing && shownPrompt == promptIdx && promptIdx < prompts.Count;
        promptCg.alpha = Mathf.MoveTowards(promptCg.alpha, showPrompt ? 1 : 0, dt * 3);
    }
}
