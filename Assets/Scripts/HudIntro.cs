using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public partial class Hud
{
    RectTransform introPanel;
    Text introHeading, introBody, introNextLabel, introCount;
    Button introNext;
    float introOpenedAt;
    public bool IntroOpen { get; private set; }

    void BuildIntro()
    {
        introPanel = Full("First steps", root);
        var shade = introPanel.gameObject.AddComponent<Image>(); shade.color = new Color(.025f, .025f, .035f, .66f); shade.raycastTarget = true;
        // The whole backdrop is a button, so a click anywhere that is not the Begin
        // plate still dismisses. Going through the EventSystem rather than polling the
        // mouse means gamepad and touch get the same behaviour for free.
        introPanel.gameObject.AddComponent<Button>().onClick.AddListener(CloseIntro);
        // Taller than it needs to be on purpose: the old 380 left the buttons sitting
        // on the bottom rim with nothing under them, which is what made them look cut off.
        var box = Rect("Stone guide", introPanel, center, Vector2.zero, new Vector2(880, 440), center);
        box.gameObject.AddComponent<MenuPlateGraphic>().raycastTarget = true;
        introCount = Txt("", box, center, new Vector2(0, 178), new Vector2(600, 28), 19, Gold, TextAnchor.MiddleCenter, null, false);
        introHeading = Txt("", box, center, new Vector2(0, 124), new Vector2(780, 64), 42, Cream, TextAnchor.MiddleCenter, null, false);
        if (menuTitleFont != null) introHeading.font = menuTitleFont;
        introBody = Txt("", box, center, new Vector2(0, 10), new Vector2(760, 180), 26, Cream, TextAnchor.MiddleCenter, null, false);
        introBody.horizontalOverflow = HorizontalWrapMode.Wrap; introBody.lineSpacing = 1.34f;
        // One way out, not two. Skip and Begin did the same thing and asking which one
        // you meant was the only decision the panel ever offered.
        introNext = IntroButton("Begin", box, new Vector2(0, -132), CloseIntro);
        introNextLabel = introNext.GetComponentsInChildren<Text>()[0];
        Txt("or click anywhere to continue", box, center, new Vector2(0, -196), new Vector2(700, 28), 19, new Color(.60f, .54f, .47f), TextAnchor.MiddleCenter, null, false);
        introPanel.gameObject.SetActive(false);
    }

    Button IntroButton(string label, Transform parent, Vector2 position, UnityEngine.Events.UnityAction action)
    {
        var rt = Rect(label, parent, center, position, new Vector2(330, 78), center);
        var plate = rt.gameObject.AddComponent<MenuPlateGraphic>(); plate.raycastTarget = true;
        var text = Txt(label, rt, center, Vector2.zero, new Vector2(270, 62), 30, Cream, TextAnchor.MiddleCenter, null, false);
        var marker = Txt("◆", rt, center, new Vector2(-130, 0), new Vector2(20, 25), 12, Gold, TextAnchor.MiddleCenter, null, false);
        var button = rt.gameObject.AddComponent<MenuFocusButton>(); button.targetGraphic = plate; button.Configure(plate, text, marker); button.onClick.AddListener(action);
        return button;
    }

    public void BeginIntro()
    {
        if (PlayerPrefs.GetInt("Emberclad.IntroSeen", 0) != 0) return;
        IntroOpen = true; introOpenedAt = Time.unscaledTime;
        introPanel.gameObject.SetActive(true); introPanel.SetAsLastSibling();
        game.MenuPause(true); ShowIntroPage();
    }

    void ShowIntroPage()
    {
        // One page, and only the things no fight can teach: where the legs are. Weapons,
        // the shield and Embers are each introduced by their own round in the forest.
        introCount.text = "";
        introHeading.text = "Find your footing";
        introBody.text = $"Move: {MenuSettings.Binding(0)} / {MenuSettings.Binding(1)} / {MenuSettings.Binding(2)} / {MenuSettings.Binding(3)}\nJump: {MenuSettings.Binding(7)}     Roll: {MenuSettings.Binding(8)}\nRoll through a strike. You are vulnerable as you land.";
        introNextLabel.text = "Begin";
        MenuFocusButton.KeyboardFocus = false;
        EventSystem.current.SetSelectedGameObject(null);
    }

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
        if ((keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame) && EventSystem.current.currentSelectedGameObject == null) { CloseIntro(); return; }
        // Anywhere at all. The quarter-second guard above keeps the click that started
        // the run from dismissing the panel on the frame it opens.
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) { CloseIntro(); return; }
        if (keyboard.tabKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame)
        {
            MenuFocusButton.KeyboardFocus = true;
            if (EventSystem.current.currentSelectedGameObject == null) EventSystem.current.SetSelectedGameObject(introNext.gameObject);
        }
    }
}
