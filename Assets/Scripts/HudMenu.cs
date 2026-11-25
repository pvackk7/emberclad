using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

public partial class Hud
{
    RectTransform menuContent;
    GameObject menuBackdrop;
    GameObject menuWisp;
    Image menuShade;
    bool pauseMenu, loading, awaitingBindingRelease;
    MenuDragonFlight menuDragon;
    int page, binding = -1;
    float loadUntil;
    Text bindingLabel;
    Button firstMenuButton;
    Font menuTitleFont, menuButtonFont;
    readonly Vector2 center = new Vector2(0.5f, 0.5f);
    public bool PauseMenuOpen => pauseMenu;

    void BuildMenu()
    {
        menuTitleFont = Resources.Load<Font>("Fonts/Cinzel-Bold");
        menuButtonFont = Resources.Load<Font>("Fonts/Cinzel-SemiBold");
        if (menuButtonFont == null) menuButtonFont = menuTitleFont;
        gameObject.AddComponent<GraphicRaycaster>();
        if (EventSystem.current == null)
        {
            var events = new GameObject("Menu Events", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
        titlePanel = Full("Menu", root); titleCg = Group(titlePanel, 0);
        var bg = Full("Backdrop", titlePanel).gameObject.AddComponent<Image>();
        menuBackdrop = bg.gameObject;
        bg.raycastTarget = false;
        // The menu backdrop is the blue night-haze painting. The old tavern painting
        // (Menu/TitleBackground) is no longer shown here; it stays in the project only
        // because the forest's moon still samples its texture.
        var painting = Resources.Load<Texture2D>("Menu/MenuBackground");
        if (painting != null)
        {
            bg.sprite = Sprite.Create(painting, new Rect(0, 0, painting.width, painting.height), new Vector2(0.5f, 0.5f), 100);
            bg.color = Color.white;
            var fit = bg.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = (float)painting.width / painting.height;
        }
        else bg.color = new Color(0.04f, 0.035f, 0.05f, 1);
        menuShade = Full("Shade", titlePanel).gameObject.AddComponent<Image>();
        menuShade.color = new Color(0.02f, 0.03f, 0.04f, 0.08f);
        // The distant dragon: over the sky backdrop and shade, under the wisp, title and buttons.
        var dragonFrames = MenuDragonFlight.LoadFrames();
        if (dragonFrames.Length > 0)
        {
            var dragonGo = new GameObject("MenuDragon", typeof(RectTransform), typeof(Image));
            dragonGo.transform.SetParent(titlePanel, false);
            var dragon = dragonGo.AddComponent<MenuDragonFlight>();
            menuDragon = dragon;
            dragon.wingFrames = dragonFrames;
            dragon.showWith = menuBackdrop;
        }
        menuWisp = Full("Wandering fire ember", titlePanel).gameObject;
        // The yellow fire wisp was removed at the user's request (2026-11-23); MenuFireWisp.cs is unused. The empty object stays so the show/hide calls keep working.
        menuContent = Full("Content", titlePanel);
        MenuPage(0);
    }

    Button MenuButton(string label, float y, UnityEngine.Events.UnityAction action)
    {
        // Elden Ring-style item: plain serif caps, no plate; a soft ember glow appears behind the focused item.
        var rt = Rect("Item " + label, menuContent, center, new Vector2(0, y), new Vector2(460, 64), center);
        var im = rt.gameObject.AddComponent<MenuGlowGraphic>();
        im.raycastTarget = true;
        var caption = Txt(label.ToUpperInvariant(), im.transform, center, new Vector2(0, 1), new Vector2(450, 60), 34, Cream, TextAnchor.MiddleCenter, null, false);
        if (menuButtonFont != null) caption.font = menuButtonFont;
        var shadow = caption.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(0.05f, 0.055f, 0.05f, 0.8f); shadow.effectDistance = new Vector2(0, -2);
        var marker = Txt("", im.transform, center, Vector2.zero, new Vector2(10, 10), 14, Gold, TextAnchor.MiddleCenter, null, false);
        var b = im.gameObject.AddComponent<MenuFocusButton>(); b.targetGraphic = im;
        b.idleColor = new Color(0.78f, 0.77f, 0.75f, 1f);   // soft white; focus brightens to pure white
        b.focusColor = new Color(1f, 1f, 1f, 1f);
        b.focusScale = 1.025f;
        b.Configure(im, caption, marker);
        b.onClick.AddListener(action); return b;
    }

    void MenuPage(int next)
    {
        PlayerPrefs.Save();
        page = next; binding = -1;
        menuBackdrop.SetActive(!pauseMenu);
        menuWisp.SetActive(!pauseMenu);
        menuShade.color = new Color(0.02f, 0.03f, 0.04f, pauseMenu ? 0.68f : 0.08f);
        foreach (Transform child in menuContent) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        titleCg.blocksRaycasts = true; titleCg.interactable = true;
        string heading = next == 0 ? "EMBERCLAD" : next == 1 ? "OPTIONS" : next == 2 ? "SOUND" : "CONTROLS";
        if (next == 0 && !pauseMenu)
        {
            var title = Rect("Forged Emberclad title", menuContent, center, new Vector2(0, 260), new Vector2(1050, 350), center);
            title.gameObject.AddComponent<MenuTitle>();
        }
        else
        {
            var title = Txt(next == 0 ? "PAUSED" : heading, menuContent, center, new Vector2(0, 350), new Vector2(1000, 100), 54, Cream, TextAnchor.MiddleCenter, null, true);
            if (menuTitleFont != null) title.font = menuTitleFont;
        }
        Button first = null;
        if (next == 0)
        {
            // A knight mid-run owns this browser's Play button until the Warden falls.
            first = MenuButton(pauseMenu ? "Resume" : RunRecord.HasSavedRun ? "Continue" : "Play", 30, () => { if (pauseMenu) ClosePause(); else PlayOrContinue(); });
            MenuButton("Options", -75, () => MenuPage(1));
            MenuButton(pauseMenu ? "Return to Menus" : "Exit", -180, () => { if (pauseMenu) game.ReturnToMenu(); else ExitGame(); });
        }
        else if (next == 1)
        {
            first = MenuButton("Sound", 30, () => MenuPage(2));
            if (TouchControls.Enabled)
            {
                // Key bindings mean nothing on a phone; fullscreen does.
                fullscreenLabel = MenuButton(FullscreenLabel(), -75, ToggleFullscreen).GetComponentsInChildren<Text>()[0];
                fullscreenHint = Txt("", menuContent, center, new Vector2(0, -128), new Vector2(1300, 40), 22, Ink);
                if (menuButtonFont != null) fullscreenHint.font = menuButtonFont;
            }
            else MenuButton("Controls", -75, () => MenuPage(3));
            MenuButton("Back", -200, () => MenuPage(0));
        }
        else if (next == 2)
        {
            string[] names = { "Master", "Music", "Effects" };
            for (int i = 0; i < names.Length; i++)
            {
                string name = names[i]; float y = 170 - i * 110;
                var label = Txt(name, menuContent, center, new Vector2(0, y + 35), new Vector2(550, 40), 28, Cream);
                var rail = Img("ui_white", menuContent, center, new Vector2(0, y), new Vector2(500, 12), Dark, center);
                var slider = rail.gameObject.AddComponent<Slider>(); rail.raycastTarget = true;
                var handle = Img("ui_round", rail.transform, center, Vector2.zero, new Vector2(24, 32), Gold, center);
                slider.handleRect = handle.rectTransform; slider.targetGraphic = handle; slider.minValue = 0; slider.maxValue = 1; slider.value = MenuSettings.Volume(name);
                label.text = name + "  " + Mathf.RoundToInt(slider.value * 100) + "%";
                slider.onValueChanged.AddListener(v => { MenuSettings.SetVolume(name, v); AudioListener.volume = MenuSettings.Volume("Master"); label.text = name + "  " + Mathf.RoundToInt(v * 100) + "%"; });
            }
            first = MenuButton("Back", -240, () => { PlayerPrefs.Save(); MenuPage(1); });
        }
        else
        {
            for (int i = 0; i < MenuSettings.Actions.Length; i++)
            {
                int n = i;
                // Tightened to fit fourteen rows without running into the hint below.
                var b = MenuButton(MenuSettings.Actions[i] + "     " + MenuSettings.Binding(i), 272 - i * 39, () => { binding = n; awaitingBindingRelease = true; bindingLabel.text = "Press a key · Escape cancels"; });
                b.GetComponent<RectTransform>().sizeDelta = new Vector2(540, 38);
                foreach (var label in b.GetComponentsInChildren<Text>()) if (label.text != "•") label.fontSize = 23;
                if (first == null) first = b;
            }
            bindingLabel = Txt("Duplicate keys swap automatically", menuContent, center, new Vector2(0, -300), new Vector2(900, 40), 22, Ink);
            MenuButton("Back", -355, () => MenuPage(1));
        }
        firstMenuButton = first;
        MenuFocusButton.KeyboardFocus = false;
        EventSystem.current.SetSelectedGameObject(null);
    }

    Text fullscreenLabel, fullscreenHint;

    static string FullscreenLabel()
    {
        var s = WebBridge.FullscreenState();
        return s == WebBridge.Fullscreen.On || s == WebBridge.Fullscreen.HomeScreenApp ? "Fullscreen: On"
             : s == WebBridge.Fullscreen.Off ? "Fullscreen: Off" : "Fullscreen";
    }

    void ToggleFullscreen()
    {
        var s = WebBridge.FullscreenState();
        // iPhone Safari has no fullscreen for pages; a Home Screen web app is the way there.
        if (s == WebBridge.Fullscreen.Unsupported) { if (fullscreenHint != null) fullscreenHint.text = "On iPhone: tap Share, then Add to Home Screen, and play from the icon."; return; }
        if (s == WebBridge.Fullscreen.HomeScreenApp) { if (fullscreenHint != null) fullscreenHint.text = "Already fullscreen: you are playing from the Home Screen."; return; }
        WebBridge.ToggleFullscreen();
    }

    void BeginLoading()
    {
        loading = true; loadUntil = Time.unscaledTime + 1.6f;
        if (menuDragon != null) menuDragon.PassNow();   // the big dragon sweeps through while the forest loads
        foreach (Transform child in menuContent) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        Txt("Entering the forest…", menuContent, center, new Vector2(0, -250), new Vector2(1000, 70), 38, Ink);
        if (!string.IsNullOrEmpty(unrankedNote)) Txt(unrankedNote, menuContent, center, new Vector2(0, -410), new Vector2(1500, 50), 24, Ink);
        EventSystem.current.SetSelectedGameObject(null);
    }

    void ClosePause()
    {
        pauseMenu = false; titleOn = false; titleCg.blocksRaycasts = false; titleCg.interactable = false;
        ShowHelp(false); game.MenuPause(false);
    }

    void TickMenu()
    {
        if (IntroOpen) return;
        if (TickNickname()) return;
        if (fullscreenLabel != null) { string l = FullscreenLabel().ToUpperInvariant(); if (fullscreenLabel.text != l) fullscreenLabel.text = l; }
        if (loading)
        {
            if (Time.unscaledTime >= loadUntil) { loading = false; titleCg.blocksRaycasts = false; titleCg.interactable = false; game.StartFromMenu(); }
            return;
        }
        var keyboard = Keyboard.current;
        if (titleOn && binding < 0 && keyboard != null)
        {
            if (Mouse.current != null && Mouse.current.delta.ReadValue().sqrMagnitude > 1) MenuFocusButton.KeyboardFocus = false;
            if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame || keyboard.tabKey.wasPressedThisFrame)
            {
                MenuFocusButton.KeyboardFocus = true;
                if (EventSystem.current.currentSelectedGameObject == null && firstMenuButton != null) EventSystem.current.SetSelectedGameObject(firstMenuButton.gameObject);
            }
        }
        if (binding >= 0 && keyboard != null)
        {
            if (awaitingBindingRelease)
            {
                foreach (var key in keyboard.allKeys) if (key.isPressed) return;
                awaitingBindingRelease = false;
                return;
            }
            if (keyboard.escapeKey.wasPressedThisFrame) { MenuPage(3); return; }
            foreach (var key in keyboard.allKeys) if (key.wasPressedThisFrame) { MenuSettings.Bind(binding, key.keyCode); MenuPage(3); break; }
            return;
        }
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            if (titleOn) { if (page != 0) MenuPage(page == 1 ? 0 : 1); else if (pauseMenu) ClosePause(); }
            else if (game.state == Game.State.Playing)
            {
                // The help overlay and this menu share one pause; only one may be up at a time.
                ShowHelp(false);
                pauseMenu = true; titleOn = true; titlePanel.gameObject.SetActive(true); titlePanel.SetAsLastSibling(); game.MenuPause(true); MenuPage(0);
            }
        }
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")] static extern void EmbercladExitToSite();
#endif

    void ExitGame()
    {
        PlayerPrefs.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_WEBGL
        EmbercladExitToSite();   // back to the website's own Play screen (Plugins/WebGL/EmbercladSite.jslib)
#else
        Application.Quit();
#endif
    }
}
