using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public partial class Hud
{
    RectTransform introPanel;
    Text introHeading, introBody, introNextLabel, introCount;
    Button introNext;
    int introPage;
    float introOpenedAt;
    public bool IntroOpen { get; private set; }

    void BuildIntro()
    {
        introPanel = Full("First steps", root);
        var shade = introPanel.gameObject.AddComponent<Image>(); shade.color = new Color(.025f, .025f, .035f, .62f); shade.raycastTarget = true;
        var box = Rect("Stone guide", introPanel, center, Vector2.zero, new Vector2(780, 380), center);
        box.gameObject.AddComponent<MenuPlateGraphic>().raycastTarget = true;
        introCount = Txt("", box, center, new Vector2(0, 146), new Vector2(600, 28), 19, Gold, TextAnchor.MiddleCenter, null, false);
        introHeading = Txt("", box, center, new Vector2(0, 96), new Vector2(700, 60), 36, Cream, TextAnchor.MiddleCenter, null, false);
        if (menuTitleFont != null) introHeading.font = menuTitleFont;
        introBody = Txt("", box, center, new Vector2(0, -3), new Vector2(680, 140), 27, Cream, TextAnchor.MiddleCenter, null, false);
        introBody.horizontalOverflow = HorizontalWrapMode.Wrap; introBody.lineSpacing = 1.18f;
        IntroButton("Skip", box, new Vector2(-166, -123), CloseIntro);
        introNext = IntroButton("Next", box, new Vector2(166, -123), NextIntro);
        introNextLabel = introNext.GetComponentsInChildren<Text>()[0];
        introPanel.gameObject.SetActive(false);
    }

    Button IntroButton(string label, Transform parent, Vector2 position, UnityEngine.Events.UnityAction action)
    {
        var rt = Rect(label, parent, center, position, new Vector2(286, 67), center);
        var plate = rt.gameObject.AddComponent<MenuPlateGraphic>(); plate.raycastTarget = true;
        var text = Txt(label, rt, center, Vector2.zero, new Vector2(230, 55), 27, Cream, TextAnchor.MiddleCenter, null, false);
        var marker = Txt("◆", rt, center, new Vector2(-110, 0), new Vector2(20, 25), 12, Gold, TextAnchor.MiddleCenter, null, false);
        var button = rt.gameObject.AddComponent<MenuFocusButton>(); button.targetGraphic = plate; button.Configure(plate, text, marker); button.onClick.AddListener(action);
        return button;
    }

    public void BeginIntro()
    {
        if (PlayerPrefs.GetInt("Emberclad.IntroSeen", 0) != 0) return;
        IntroOpen = true; introPage = 0; introOpenedAt = Time.unscaledTime;
        introPanel.gameObject.SetActive(true); introPanel.SetAsLastSibling();
        game.MenuPause(true); ShowIntroPage();
    }

    void ShowIntroPage()
    {
        introCount.text = (introPage + 1) + " / 3";
        if (introPage == 0)
        {
            introHeading.text = "Find your footing";
            introBody.text = $"Move: {MenuSettings.Binding(0)} / {MenuSettings.Binding(1)} / {MenuSettings.Binding(2)} / {MenuSettings.Binding(3)}\nJump: {MenuSettings.Binding(7)}     Dodge: {MenuSettings.Binding(8)}\nRoll through attacks. You are vulnerable when landing.";
        }
        else if (introPage == 1)
        {
            introHeading.text = "Hold your ground";
            introBody.text = $"Attack: {MenuSettings.Binding(4)}     Heavy strike: {MenuSettings.Binding(5)}\nAttacks and rolls spend stamina. Pause to recover.\nHeavy attacks break shields. Ember Burst: {MenuSettings.Binding(6)}.";
        }
        else
        {
            introHeading.text = "The forest awaits";
            introBody.text = $"Hold {MenuSettings.Binding(11)} + move mouse: up for sword, right for potions.\nRelease {MenuSettings.Binding(11)} to equip; click to use.\nThree potions per life. Each restores 25% health.";
        }
        introNextLabel.text = introPage == 2 ? "Begin" : "Next";
        MenuFocusButton.KeyboardFocus = false;
        EventSystem.current.SetSelectedGameObject(null);
    }

    void NextIntro() { if (!IntroOpen) return; if (++introPage > 2) CloseIntro(); else ShowIntroPage(); }

    void CloseIntro()
    {
        if (!IntroOpen) return;
        IntroOpen = false; introPanel.gameObject.SetActive(false);
        PlayerPrefs.SetInt("Emberclad.IntroSeen", 1); PlayerPrefs.Save();
        EventSystem.current.SetSelectedGameObject(null); game.MenuPause(false);
    }

    void TickIntro()
    {
        if (!IntroOpen || Time.unscaledTime < introOpenedAt + .25f) return;
        var keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (keyboard.escapeKey.wasPressedThisFrame) { CloseIntro(); return; }
        if ((keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame) && EventSystem.current.currentSelectedGameObject == null) NextIntro();
        if (keyboard.tabKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame)
        {
            MenuFocusButton.KeyboardFocus = true;
            if (EventSystem.current.currentSelectedGameObject == null) EventSystem.current.SetSelectedGameObject(introNext.gameObject);
        }
    }
}
