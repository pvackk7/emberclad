using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// The nickname page shown when Play is pressed (instead of an account). A name is a knight:
/// once entered it is this browser's run until the Bone Warden falls, so while a knight is
/// mid-run Play skips this page and continues it (see RunRecord).
/// Typing is read straight off Keyboard.current, like the old account page, because the
/// project uses the new Input System only.
public partial class Hud
{
    bool nickOpen, nickBusy;
    string nickValue = "";
    Text nickDisplay, nickMessage;
    Image nickBox;
    float nickCaretT;
    Keyboard nickKeyboard;

    /// Play: continue the knight that is mid-run, or ask for a new name.
    void PlayOrContinue()
    {
        if (!RunRecord.HasSavedRun) { OpenNicknamePage(); return; }
        if (nickBusy) return;
        nickBusy = true;
        foreach (Transform child in menuContent) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        Txt("Riding as " + RunRecord.SavedNickname + "…", menuContent, center, new Vector2(0, -250), new Vector2(1000, 70), 38, Gold);
        StartCoroutine(RunRecord.Resume(message =>
        {
            nickBusy = false;
            if (!RunRecord.HasSavedRun) { OpenNicknamePage(); return; }   // it was finished elsewhere
            unrankedNote = message;
            BeginLoading();
        }));
    }

    void OpenNicknamePage()
    {
        nickOpen = true; nickCaretT = 0;
        nickValue = "";   // a finished name is spent, so start empty
        foreach (Transform child in menuContent) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        titleCg.blocksRaycasts = true; titleCg.interactable = true;
        menuShade.color = new Color(0.02f, 0.03f, 0.04f, 0.45f);

        var heading = Txt("NAME YOUR KNIGHT", menuContent, center, new Vector2(0, 250), new Vector2(1200, 100), 54, Cream, TextAnchor.MiddleCenter, null, true);
        if (menuTitleFont != null) heading.font = menuTitleFont;

        nickBox = Img("ui_white", menuContent, center, new Vector2(0, 80), new Vector2(560, 60), FieldFocus, center);
        nickDisplay = Txt("", nickBox.transform, center, Vector2.zero, new Vector2(510, 50), 28, Cream, TextAnchor.MiddleCenter, null, false);
        nickMessage = Txt("", menuContent, center, new Vector2(0, 15), new Vector2(1100, 44), 22, Gold, TextAnchor.MiddleCenter, null, false);
        if (menuButtonFont != null) nickMessage.font = menuButtonFont;

        var begin = MenuButton("Begin", -60, SubmitNickname);
        MenuButton("Back", -140, CloseNicknamePage);
        firstMenuButton = begin;
        MenuFocusButton.KeyboardFocus = false;
        EventSystem.current.SetSelectedGameObject(null);

        NickSubscribe(true);
        RefreshNickname();
    }

    void CloseNicknamePage()
    {
        NickSubscribe(false);
        nickOpen = false;
        MenuPage(0);
    }

    void NickSubscribe(bool on)
    {
        if (nickKeyboard != null) { nickKeyboard.onTextInput -= OnNickChar; nickKeyboard = null; }
        var keyboard = Keyboard.current;
        if (on && keyboard != null) { keyboard.onTextInput += OnNickChar; nickKeyboard = keyboard; }
    }

    void OnNickChar(char c)
    {
        if (!nickOpen || c < ' ' || c == (char)127) return;
        if (nickBusy) return;
        if (!RunRecord.AllowedChar(c) || nickValue.Length >= RunRecord.MaxNickLength) return;
        nickValue += c; nickCaretT = 0;
        if (nickMessage != null) nickMessage.text = "";
        RefreshNickname();
    }

    /// Runs from TickMenu while the page is open; returns true so the menu skips its own input.
    bool TickNickname()
    {
        if (!nickOpen) return false;
        var keyboard = Keyboard.current;
        if (keyboard == null) return true;
        if (nickKeyboard != keyboard) NickSubscribe(true);
        nickCaretT += Time.unscaledDeltaTime;
        if (nickCaretT >= .53f) nickCaretT = 0;
        RefreshNickname();
        if (nickBusy) return true;
        if (keyboard.escapeKey.wasPressedThisFrame) { CloseNicknamePage(); return true; }
        if (keyboard.backspaceKey.wasPressedThisFrame && nickValue.Length > 0) nickValue = nickValue.Substring(0, nickValue.Length - 1);
        if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) SubmitNickname();
        return true;
    }

    void RefreshNickname()
    {
        if (nickDisplay == null) return;
        bool caretOn = nickCaretT < .265f;
        nickDisplay.text = nickValue + (caretOn ? "|" : " ");
    }

    void SubmitNickname()
    {
        if (!nickOpen || nickBusy) return;
        string clean = RunRecord.Clean(nickValue);
        if (clean.Length == 0) { nickMessage.text = "Enter a name first."; return; }
        if (!RunRecord.Validate(clean, out string error)) { nickMessage.text = error; return; }
        nickBusy = true; nickMessage.text = "Checking the name...";
        // The run (and its clock) starts when the site accepts the name.
        StartCoroutine(RunRecord.Start(clean, (ok, message) =>
        {
            nickBusy = false;
            if (!ok) { if (nickMessage != null) nickMessage.text = message; return; }
            NickSubscribe(false);
            nickOpen = false;
            unrankedNote = message;
            BeginLoading();
        }));
    }

    /// Set when the run starts unranked (site unreachable); shown on the loading screen.
    string unrankedNote;
}
