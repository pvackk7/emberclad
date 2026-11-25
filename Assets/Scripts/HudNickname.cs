using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// The nickname page shown when Play is pressed (instead of an account). A name is a knight:
/// once entered it is this browser's run until the Bone Warden falls, so while a knight is
/// mid-run Play skips this page and continues it (see RunRecord).
/// Typing is read straight off Keyboard.current, like the old account page, because the
/// project uses the new Input System only. A phone has no Keyboard device and only opens
/// its on-screen keyboard for a real, focused text field, so on touch devices in the web
/// build an invisible DOM <input> is laid exactly over the drawn field (WebBridge): the
/// tap lands on it, the native keyboard opens, and its text is mirrored in here.
public partial class Hud
{
    bool nickOpen, nickBusy;
    string nickValue = "";
    Text nickDisplay, nickMessage;
    Image nickBox;
    float nickCaretT;
    Keyboard nickKeyboard;
    bool nickWeb; int nickWebVersion = -1; Rect nickWebRect;
    static readonly Color NickHint = new Color(0.86f, 0.85f, 0.83f, 0.45f);

    /// Play: continue the knight that is mid-run, or ask for a new name.
    void PlayOrContinue()
    {
        if (!RunRecord.HasSavedRun) { OpenNicknamePage(); return; }
        if (nickBusy) return;
        nickBusy = true;
        foreach (Transform child in menuContent) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        Txt("Riding as " + RunRecord.SavedNickname + "…", menuContent, center, new Vector2(0, -250), new Vector2(1000, 70), 38, Ink);
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
        nickMessage = Txt("", menuContent, center, new Vector2(0, 15), new Vector2(1100, 44), 22, Ink, TextAnchor.MiddleCenter, null, false);
        if (menuButtonFont != null) nickMessage.font = menuButtonFont;

        var begin = MenuButton("Begin", -60, SubmitNickname);
        MenuButton("Back", -140, CloseNicknamePage);
        firstMenuButton = begin;
        MenuFocusButton.KeyboardFocus = false;
        EventSystem.current.SetSelectedGameObject(null);

        NickSubscribe(true);
        nickWeb = WebBridge.Web && TouchControls.Enabled;
        if (nickWeb) { nickWebVersion = -1; nickWebRect = NickFieldRect(); WebBridge.TextOpen(nickWebRect, RunRecord.MaxNickLength); }
        RefreshNickname();
    }

    /// The drawn field in screen pixels, a little taller than drawn so it is easy to hit.
    Rect NickFieldRect()
    {
        if (nickBox == null) return new Rect();
        var corners = new Vector3[4]; nickBox.rectTransform.GetWorldCorners(corners);   // overlay canvas: world = screen pixels
        var r = UnityEngine.Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        float pad = r.height * .35f;
        return new Rect(r.x, r.y - pad, r.width, r.height + pad * 2);
    }

    void CloseNickWeb() { if (nickWeb) { WebBridge.TextClose(); nickWeb = false; } }

    /// Mirror the native field into the page, filtered by the same rules as typed keys.
    void TickNickWeb()
    {
        var rect = NickFieldRect();
        if (rect != nickWebRect) { nickWebRect = rect; WebBridge.TextPlace(rect); }   // rotation, resize
        int version = WebBridge.TextVersion();
        if (version != nickWebVersion)
        {
            nickWebVersion = version;
            string raw = WebBridge.TextValue(), clean = "";
            foreach (char c in raw)
                if (c >= ' ' && c != (char)127 && RunRecord.AllowedChar(c) && clean.Length < RunRecord.MaxNickLength) clean += c;
            if (clean != raw) WebBridge.TextSet(clean);
            if (clean != nickValue) { nickValue = clean; nickCaretT = 0; if (nickMessage != null) nickMessage.text = ""; }
        }
        if (WebBridge.TextTakeSubmit()) SubmitNickname();
    }

    void CloseNicknamePage()
    {
        CloseNickWeb();
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
        if (nickWeb && WebBridge.TextFocused()) return;   // the native field already has this key
        nickValue += c; nickCaretT = 0;
        if (nickWeb) WebBridge.TextSet(nickValue);
        if (nickMessage != null) nickMessage.text = "";
        RefreshNickname();
    }

    /// Runs from TickMenu while the page is open; returns true so the menu skips its own input.
    bool TickNickname()
    {
        if (!nickOpen) return false;
        if (nickWeb && !nickBusy) TickNickWeb();
        if (!nickOpen) return true;   // the native field's Go key started the run
        var keyboard = Keyboard.current;
        if (keyboard == null) { TickNickCaret(); return true; }
        if (nickKeyboard != keyboard) NickSubscribe(true);
        TickNickCaret();
        if (nickBusy) return true;
        if (nickWeb && WebBridge.TextFocused()) return true;   // keys belong to the native field
        if (keyboard.escapeKey.wasPressedThisFrame) { CloseNicknamePage(); return true; }
        if (keyboard.backspaceKey.wasPressedThisFrame && nickValue.Length > 0) { nickValue = nickValue.Substring(0, nickValue.Length - 1); if (nickWeb) WebBridge.TextSet(nickValue); }
        if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) SubmitNickname();
        return true;
    }

    void TickNickCaret()
    {
        nickCaretT += Time.unscaledDeltaTime;
        if (nickCaretT >= .53f) nickCaretT = 0;
        RefreshNickname();
    }

    void RefreshNickname()
    {
        if (nickDisplay == null) return;
        bool caretOn = nickCaretT < .265f;
        // On a touch screen an empty field says what to do with it, until the keyboard is up.
        if (nickWeb && nickValue.Length == 0 && !WebBridge.TextFocused()) { nickDisplay.text = "Tap here to type a name"; nickDisplay.color = NickHint; return; }
        nickDisplay.color = Cream;
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
            CloseNickWeb();
            NickSubscribe(false);
            nickOpen = false;
            unrankedNote = message;
            BeginLoading();
        }));
    }

    /// Set when the run starts unranked (site unreachable); shown on the loading screen.
    string unrankedNote;
}
