using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public partial class Hud
{
    RectTransform introPanel;
    Text introHeading, introNextLabel;
    Button introNext;
    float introOpenedAt;
    // The panel pauses the game, so it must never be able to stay up forever.
    const float MaxIntroSeconds = 25f;
    public bool IntroOpen { get; private set; }

    void BuildIntro()
    {
        introPanel = Full("First steps", root);
        var shade = introPanel.gameObject.AddComponent<Image>(); shade.color = new Color(.025f, .025f, .035f, .66f); shade.raycastTarget = true;
        // The whole backdrop is a button, so a click anywhere that is not the Begin
        // plate still dismisses. Going through the EventSystem rather than polling the
        // mouse means gamepad and touch get the same behaviour for free.
        introPanel.gameObject.AddComponent<Button>().onClick.AddListener(CloseIntro);
        // Built to the same recipe as a teaching card -- same plate, same gold
        // uppercase heading, same key caps down the left -- because this is the first
        // lesson the game gives and it was the one screen that looked like it came
        // from somewhere else.
        Vector2 TC = new Vector2(.5f, 1);
        const float W = 500f;
        var box = Rect("Stone guide", introPanel, center, Vector2.zero, new Vector2(W, 330), center);
        box.gameObject.AddComponent<MenuPlateGraphic>().raycastTarget = true;
        introHeading = Txt("", box, TC, new Vector2(0, -24), new Vector2(W - 60, 44), 32, Ink, TextAnchor.MiddleCenter, TC, false);
        if (menuTitleFont != null) introHeading.font = menuTitleFont;
        // Just the keys: they say everything the panel needs to.
        KeyBlock(box, -86f, new[] { new[] { "W", "A", "S", "D" }, new[] { "SPACE" }, new[] { "F" } }, new[] { "Move", "Jump", "Roll" });
        introNext = IntroButton("Begin", box, new Vector2(0, -236), CloseIntro, TC);
        introNextLabel = introNext.GetComponentsInChildren<Text>()[0];
        introPanel.gameObject.SetActive(false);
    }

    Button IntroButton(string label, Transform parent, Vector2 position, UnityEngine.Events.UnityAction action, Vector2? anchor = null)
    {
        Vector2 a = anchor ?? center;
        var rt = Rect(label, parent, a, position, new Vector2(300, 72), a);
        var plate = rt.gameObject.AddComponent<MenuPlateGraphic>(); plate.raycastTarget = true;
        var text = Txt(label, rt, center, Vector2.zero, new Vector2(248, 58), 28, Cream, TextAnchor.MiddleCenter, null, false);
        var marker = Txt("•", rt, center, new Vector2(-116, 0), new Vector2(20, 25), 18, Gold, TextAnchor.MiddleCenter, null, false);
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
        introHeading.text = "FIND YOUR FOOTING";
        // The keys are key caps now, so the body is left with the one thing the caps
        // cannot say.
        introNextLabel.text = "Begin";
        MenuFocusButton.KeyboardFocus = false;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    void CloseIntro()
    {
        if (!IntroOpen) return;
        IntroOpen = false; introPanel.gameObject.SetActive(false);
        PlayerPrefs.SetInt("Emberclad.IntroSeen", 1); PlayerPrefs.Save();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        game.MenuPause(false);
    }

    void TickIntro()
    {
        if (!IntroOpen) return;
        if (game == null || game.state != Game.State.Playing || Time.unscaledTime - introOpenedAt >= MaxIntroSeconds) { CloseIntro(); return; }
        if (Time.unscaledTime < introOpenedAt + .25f) return;
        var keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (keyboard.escapeKey.wasPressedThisFrame) { CloseIntro(); return; }
        if ((keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame) && (EventSystem.current == null || EventSystem.current.currentSelectedGameObject == null)) { CloseIntro(); return; }
        // Anywhere at all. The quarter-second guard above keeps the click that started
        // the run from dismissing the panel on the frame it opens.
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) { CloseIntro(); return; }
        if (keyboard.tabKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame)
        {
            MenuFocusButton.KeyboardFocus = true;
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null) EventSystem.current.SetSelectedGameObject(introNext.gameObject);
        }
    }
}
