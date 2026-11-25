using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// Create-account / sign-in page shown when Play is pressed.
///
/// The project is set to "Input System Package (New)" only, which disables
/// Input.inputString and makes the legacy uGUI InputField unreliable, so the text
/// fields here are driven straight off Keyboard.current the way the rest of this
/// menu already reads input.
public partial class Hud
{
    class AccountInput
    {
        public string Label;
        public bool Masked;
        public bool UsernameRules;
        public string Value = "";
        public Text Display;
        public Image Box;
    }

    readonly List<AccountInput> accountFields = new List<AccountInput>();
    bool accountOpen, accountCreate, accountBusy;
    int accountFocus;
    Text accountMessage;
    float accountCaretT;
    Keyboard accountKeyboard;

    static readonly Color FieldIdle = new Color(0.05f, 0.04f, 0.06f, 0.88f);
    static readonly Color FieldFocus = new Color(0.12f, 0.09f, 0.07f, 0.94f);

    /// Play was pressed. An already signed-in player goes straight through.
    void PlayPressed()
    {
        if (Account.SignedIn) { BeginLoading(); return; }
        OpenAccountPage(!Account.AnyAccounts);
    }

    void OpenAccountPage(bool create)
    {
        accountOpen = true; accountCreate = create; accountBusy = false;
        accountFocus = 0; accountCaretT = 0;
        accountFields.Clear();
        accountMessage = null;

        foreach (Transform child in menuContent) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        titleCg.blocksRaycasts = true; titleCg.interactable = true;
        menuBackdrop.SetActive(!pauseMenu);
        menuWisp.SetActive(!pauseMenu);
        menuShade.color = new Color(0.02f, 0.03f, 0.04f, 0.45f);

        var heading = Txt(create ? "CREATE ACCOUNT" : "SIGN IN", menuContent, center, new Vector2(0, 330), new Vector2(1000, 100), 54, Cream, TextAnchor.MiddleCenter, null, true);
        if (menuTitleFont != null) heading.font = menuTitleFont;

        var blurb = Txt(create ? "Choose a name and a password to begin your journey." : "Welcome back, emberclad.", menuContent, center, new Vector2(0, 250), new Vector2(1100, 50), 24, Ink, TextAnchor.MiddleCenter, null, false);
        if (menuButtonFont != null) blurb.font = menuButtonFont;

        float y = create ? 150 : 120;
        AddAccountField("Username", y, false, true);
        AddAccountField("Password", y - 110, true, false);
        if (create) AddAccountField("Confirm password", y - 220, true, false);

        float afterFields = create ? y - 300 : y - 190;
        accountMessage = Txt("", menuContent, center, new Vector2(0, afterFields), new Vector2(1100, 44), 22, Ink, TextAnchor.MiddleCenter, null, false);
        if (menuButtonFont != null) accountMessage.font = menuButtonFont;

        var submit = MenuButton(create ? "Create Account" : "Sign In", afterFields - 70, Submit);
        MenuButton(create ? "I Already Have An Account" : "Create An Account", afterFields - 150, () => OpenAccountPage(!create));
        MenuButton("Back", afterFields - 230, CloseAccountPage);

        firstMenuButton = submit;
        MenuFocusButton.KeyboardFocus = false;
        EventSystem.current.SetSelectedGameObject(null);

        Subscribe(true);
        RefreshAccountFields();
    }

    void AddAccountField(string label, float y, bool masked, bool usernameRules)
    {
        var field = new AccountInput { Label = label, Masked = masked, UsernameRules = usernameRules };

        var caption = Txt(label.ToUpperInvariant(), menuContent, center, new Vector2(-270, y + 42), new Vector2(540, 28), 19, Ink, TextAnchor.MiddleLeft, null, false);
        if (menuButtonFont != null) caption.font = menuButtonFont;

        field.Box = Img("ui_white", menuContent, center, new Vector2(0, y), new Vector2(560, 56), FieldIdle, center);
        field.Box.raycastTarget = true;
        field.Display = Txt("", field.Box.transform, center, Vector2.zero, new Vector2(510, 46), 26, Cream, TextAnchor.MiddleLeft, null, false);

        int index = accountFields.Count;
        var click = field.Box.gameObject.AddComponent<Button>();
        click.transition = Selectable.Transition.None;
        click.onClick.AddListener(() => { accountFocus = index; accountCaretT = 0; RefreshAccountFields(); });

        accountFields.Add(field);
    }

    void CloseAccountPage()
    {
        Subscribe(false);
        accountOpen = false;
        accountFields.Clear();
        accountMessage = null;
        MenuPage(0);
    }

    void Subscribe(bool on)
    {
        var keyboard = Keyboard.current;
        if (accountKeyboard != null) { accountKeyboard.onTextInput -= OnAccountChar; accountKeyboard = null; }
        if (on && keyboard != null) { keyboard.onTextInput += OnAccountChar; accountKeyboard = keyboard; }
    }

    void OnAccountChar(char c)
    {
        if (!accountOpen || accountBusy) return;
        if (c < ' ' || c == (char)127) return;                     // control characters arrive here too
        if (accountFocus < 0 || accountFocus >= accountFields.Count) return;
        var field = accountFields[accountFocus];

        if (field.UsernameRules)
        {
            bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_';
            if (!ok) return;
            if (field.Value.Length >= Account.MaxUserLength) return;
        }
        else if (field.Value.Length >= 64) return;

        field.Value += c;
        RefreshAccountFields();
    }

    void TickAccount()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (accountKeyboard != keyboard) Subscribe(true);           // device hot-swap

        accountCaretT += Time.unscaledDeltaTime;
        if (accountCaretT >= 0.53f) { accountCaretT = 0; RefreshAccountFields(); }

        if (keyboard.escapeKey.wasPressedThisFrame) { CloseAccountPage(); return; }

        if (keyboard.backspaceKey.wasPressedThisFrame && accountFocus >= 0 && accountFocus < accountFields.Count)
        {
            var field = accountFields[accountFocus];
            if (field.Value.Length > 0) { field.Value = field.Value.Substring(0, field.Value.Length - 1); RefreshAccountFields(); }
        }

        bool forward = keyboard.tabKey.wasPressedThisFrame && !keyboard.leftShiftKey.isPressed && !keyboard.rightShiftKey.isPressed;
        bool back = keyboard.tabKey.wasPressedThisFrame && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
        if (keyboard.downArrowKey.wasPressedThisFrame) forward = true;
        if (keyboard.upArrowKey.wasPressedThisFrame) back = true;
        if (forward || back)
        {
            int n = accountFields.Count;
            accountFocus = ((accountFocus + (forward ? 1 : -1)) % n + n) % n;
            accountCaretT = 0;
            RefreshAccountFields();
        }

        if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
        {
            if (accountFocus < accountFields.Count - 1) { accountFocus++; accountCaretT = 0; RefreshAccountFields(); }
            else Submit();
        }
    }

    void RefreshAccountFields()
    {
        bool caretOn = accountCaretT < 0.265f;
        for (int i = 0; i < accountFields.Count; i++)
        {
            var field = accountFields[i];
            bool focused = i == accountFocus;
            field.Box.color = focused ? FieldFocus : FieldIdle;
            string shown = field.Masked ? new string('*', field.Value.Length) : field.Value;
            if (shown.Length == 0 && !focused) shown = field.Masked ? "" : "";
            field.Display.text = shown + (focused && caretOn ? "|" : "");
        }
    }

    void Submit()
    {
        if (accountBusy || !accountOpen) return;
        string user = accountFields.Count > 0 ? accountFields[0].Value : "";
        string pass = accountFields.Count > 1 ? accountFields[1].Value : "";
        string confirm = accountFields.Count > 2 ? accountFields[2].Value : "";

        accountBusy = true;
        string error;
        bool ok = accountCreate
            ? Account.TryCreate(user, pass, confirm, out error)
            : Account.TrySignIn(user, pass, out error);
        accountBusy = false;

        if (!ok)
        {
            if (accountMessage != null) accountMessage.text = error;
            // Wrong password should not leave the old one sitting in the box.
            if (!accountCreate && accountFields.Count > 1) { accountFields[1].Value = ""; accountFocus = 1; }
            accountCaretT = 0;
            RefreshAccountFields();
            return;
        }

        Subscribe(false);
        accountOpen = false;
        accountFields.Clear();
        accountMessage = null;
        BeginLoading();
    }

    void OnDisable() { Subscribe(false); }
}
