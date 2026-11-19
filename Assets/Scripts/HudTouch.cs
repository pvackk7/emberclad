using UnityEngine;
using UnityEngine.UI;

/// The HUD's side of the touch layout (TouchControls draws the buttons themselves):
/// moving the few HUD pieces that would sit under a thumb, tappable item wells and
/// prompt, the pause entry point, and touch wording for the lessons and key rows.
/// None of it runs on a desktop.
public partial class Hud
{
    bool touchLayout, layoutPortrait;
    TouchControls touchControls;
    Text deathPrompt, victoryPrompt;
    Vector2 goBase = new Vector2(-70, 60);

    // Desktop placements, remembered so the layout can switch back on a hybrid device.
    Vector2 hotbarDesk, cinderDesk, bossDesk, rewardDesk; Vector2 cinderAnchorDesk, rewardAnchorDesk, bossAnchorDesk, hotbarAnchorDesk;
    TextAnchor rewardAlignDesk; bool deskSaved;

    void TickTouchLayout()
    {
        if (touchControls == null) touchControls = TouchControls.Create(this, game);
        bool want = TouchControls.Enabled, portrait = Screen.height > Screen.width;
        // Re-flow on rotation too: upright, the top of the screen is too narrow for the landscape spots.
        if (want != touchLayout || (want && portrait != layoutPortrait)) { layoutPortrait = portrait; ApplyTouchLayout(want); }
    }

    /// The desktop corners are where the thumbs go on a phone: the item cross (bottom left)
    /// sits under the stick and the cinder plate (bottom right) under the buttons. On touch
    /// they move to the top right and top centre; nothing else about them changes.
    void ApplyTouchLayout(bool on)
    {
        touchLayout = on;
        if (!deskSaved)
        {
            deskSaved = true;
            if (hotbar != null) { hotbarDesk = hotbar.anchoredPosition; hotbarAnchorDesk = hotbar.anchorMin; }
            if (cinderPlate != null) { cinderDesk = cinderPlate.anchoredPosition; cinderAnchorDesk = cinderPlate.anchorMin; }
            if (bossPanel != null) { bossDesk = bossPanel.anchoredPosition; bossAnchorDesk = bossPanel.anchorMin; }
            if (rewardGainText != null) { var rt = rewardGainText.rectTransform; rewardDesk = rt.anchoredPosition; rewardAnchorDesk = rt.anchorMin; rewardAlignDesk = rewardGainText.alignment; }
        }
        Vector2 TR = new Vector2(1, 1), TC = new Vector2(.5f, 1), TL = new Vector2(0, 1);
        bool up = on && layoutPortrait;   // upright: everything stacks below the knight card
        if (hotbar != null)
        {
            Place(hotbar, on ? TR : hotbarAnchorDesk, up ? new Vector2(-180, -330) : on ? new Vector2(-180, -212) : hotbarDesk);
            hotbar.localScale = Vector3.one * (on ? .86f : 1f);
        }
        if (equippedLabel != null)
        {
            // At the right edge the item name reads leftward from the bottom well instead of off screen.
            var rt = equippedLabel.rectTransform;
            rt.pivot = on ? new Vector2(1, .5f) : new Vector2(0, .5f);
            rt.anchoredPosition = on ? new Vector2(-62, -98) : new Vector2(62, -98);
            equippedLabel.alignment = on ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
        }
        if (cinderPlate != null)
        {
            // Pivoted on its bottom-right corner, so this centres it a little below the top edge.
            Place(cinderPlate, up ? TL : on ? TC : cinderAnchorDesk, up ? new Vector2(240, -244) : on ? new Vector2(106, -86) : cinderDesk);
        }
        if (bossPanel != null)
        {
            Place(bossPanel, on ? TC : bossAnchorDesk, up ? new Vector2(0, -470) : on ? new Vector2(0, -186) : bossDesk);
            bossPanel.localScale = Vector3.one * (on ? .8f : 1f);
        }
        if (rewardGainText != null)
        {
            var rt = rewardGainText.rectTransform;
            Place(rt, up ? TL : on ? TC : rewardAnchorDesk, up ? new Vector2(388, -258) : on ? new Vector2(180, -100) : rewardDesk);
            rewardGainText.alignment = up ? TextAnchor.UpperLeft : on ? TextAnchor.UpperCenter : rewardAlignDesk;
        }
        goBase = on ? new Vector2(-470, 120) : new Vector2(-70, 60);
        if (deathPrompt != null) deathPrompt.text = on ? "Tap to rise" : "Press  R  to rise";
        if (victoryPrompt != null) victoryPrompt.text = on ? "Tap to return to the menu" : "Press  R  to return to the menu";
    }

    static void Place(RectTransform rt, Vector2 anchor, Vector2 pos)
    {
        // Keep the pivot where it was (the plates are pivoted on a corner); only the anchor moves.
        rt.anchorMin = rt.anchorMax = anchor; rt.anchoredPosition = pos;
    }

    /// The pause button: the same menu Escape opens.
    public void OpenPauseMenu()
    {
        if (titleOn || IntroOpen || game == null || game.state != Game.State.Playing) return;
        ShowHelp(false);
        pauseMenu = true; titleOn = true; titlePanel.gameObject.SetActive(true); titlePanel.SetAsLastSibling(); game.MenuPause(true); MenuPage(0);
    }

    /// Which item well (0 sword, 1 potion, 2 embers) is under this screen point, or -1.
    public int HotbarSlotAt(Vector2 screen)
    {
        if (hotbar == null || !hotbar.gameObject.activeInHierarchy) return -1;
        float scale = root.lossyScale.x * hotbar.localScale.x;
        int best = -1; float bestD = 62f * scale;   // the 94-unit wells, with a forgiving margin
        for (int i = 0; i < itemEdges.Length; i++)
        {
            if (itemEdges[i] == null) continue;
            float d = Vector2.Distance(screen, itemEdges[i].rectTransform.position);
            if (d < bestD) { bestD = d; best = i; }
        }
        return best;
    }

    public bool PromptVisible => promptCg != null && promptCg.alpha > .5f;
    public string PromptText => promptLabel != null ? promptLabel.text : "";
    public bool PromptHit(Vector2 screen)
    {
        if (!PromptVisible) return false;
        var rt = promptRT;
        return RectTransformUtility.RectangleContainsScreenPoint(rt, screen, null);
    }

    /// What a key cap should say on a touch screen.
    static string TouchKey(string key)
    {
        switch (key)
        {
            case "W": case "A": case "S": case "D": return "STICK";
            case "SPACE": return "JUMP";
            case "F": return "ROLL";
            case "J": case "G": case "E": case "R": return "TAP";
            case "K": case "SHIFT": return "HOLD";
            case "L": return "FLICK";
            case "TAB": return "II";
            default: return key;
        }
    }

    /// The lessons were written for a keyboard. On touch each row says which button and how.
    static void TouchLesson(ref string[] keys, ref string[] captions)
    {
        if (keys == null || captions == null) return;
        var k = (string[])keys.Clone(); var c = (string[])captions.Clone();
        for (int i = 0; i < k.Length && i < c.Length; i++)
        {
            switch (k[i] + "|" + c[i])
            {
                case "J|Light attack": k[i] = "TAP"; c[i] = "Attack button"; break;
                case "K|Heavy attack": k[i] = "HOLD"; c[i] = "Attack button for a heavy"; break;
                case "SPACE|Jump": k[i] = "JUMP"; c[i] = "Jump button"; break;
                case "K|Heavy in the air": k[i] = "HOLD"; c[i] = "Attack while in the air"; break;
                case "SHIFT|Hold to guard": k[i] = "HOLD"; c[i] = "The shield button to guard"; break;
                case "G|Hold, aim down": k[i] = "TAP"; c[i] = "The ember in your items"; break;
                case "L|Cast": k[i] = "FLICK"; c[i] = "Attack upward, or tap it"; break;
                case "G|Hold, aim up for the blade": k[i] = "TAP"; c[i] = "The sword to take it back"; break;
                default: k[i] = TouchKey(k[i]); break;
            }
        }
        keys = k; captions = c;
    }
}
