using UnityEngine;
using UnityEngine.UI;

/// The level bar on the knight's card, and the floating "press E" prompt that any
/// interactable in the world can raise above itself.
public partial class Hud
{
    CanvasGroup promptCg;
    Vector2 promptBase;

    void BuildPrompt()
    {
        var C = new Vector2(.5f, .5f);
        promptRT = Rect("Interact prompt", root, C, Vector2.zero, new Vector2(320, 100));
        promptKey = Img("ui_key", promptRT, C, new Vector2(0, 26), new Vector2(54, 54), Color.white, C);
        promptKeyText = Txt("E", promptRT, C, new Vector2(0, 30), new Vector2(54, 54), 27, new Color(.2f, .14f, .14f), TextAnchor.MiddleCenter, C, false);
        promptLabel = Txt("", promptRT, C, new Vector2(0, -16), new Vector2(320, 34), 22, Cream, TextAnchor.MiddleCenter, C);
        promptCg = Group(promptRT, 0f);
    }

    /// Called every frame by whatever the knight is standing next to. Stops being
    /// called the moment he steps away, and the prompt fades itself out.
    public void ShowInteractPrompt(Vector2 worldPos, string label)
    {
        if (promptRT == null) return;
        var cam = Camera.main; if (cam == null) return;
        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, cam.WorldToScreenPoint(World.Project(worldPos)), null, out local)) return;
        promptBase = local;
        if (promptLabel.text != label) promptLabel.text = label;
        // Read the binding live, so a player who rebinds Interact sees their key.
        string key = MenuSettings.Binding(12).ToString().ToUpperInvariant();
        if (promptKeyText.text != key) promptKeyText.text = key;
        promptFrame = Time.frameCount;
    }

    void TickPrompt(float dt)
    {
        if (promptRT == null) return;
        // One frame of slack: the pickup's Update and this one are not ordered.
        bool on = game != null && game.state == Game.State.Playing && !IntroOpen && !PauseMenuOpen && Time.frameCount - promptFrame <= 1;
        promptFade = Mathf.MoveTowards(promptFade, on ? 1f : 0f, dt * 8f);
        promptCg.alpha = promptFade;
        if (promptFade <= 0f) return;
        float bob = Mathf.Sin(Time.unscaledTime * 4.4f) * 5f;
        promptRT.anchoredPosition = promptBase + new Vector2(0, bob + (1f - promptFade) * -18f);
        float s = .9f + promptFade * .1f;
        promptRT.localScale = new Vector3(s, s, 1);
    }

    void TickLevel(float dt)
    {
        if (xpFill == null || game == null) return;
        float target = game.LevelProgress;
        if (xpShown < 0f) xpShown = target;
        // On a level-up the bar would otherwise run backwards through the middle.
        // Drop it to empty and let it fill, which is what the eye expects.
        if (target < xpShown - .3f) xpShown = 0f;
        xpShown = Mathf.Lerp(xpShown, target, 1f - Mathf.Exp(-9f * dt));
        xpFill.sizeDelta = new Vector2(HpW * Mathf.Clamp01(xpShown), 8);
        if (shownLevel != game.Level)
        {
            shownLevel = game.Level;
            levelText.text = "LV " + shownLevel;
            levelPunch = 1f;
        }
        levelPunch = Mathf.MoveTowards(levelPunch, 0f, dt * 2f);
        levelText.color = Color.Lerp(Gold, Color.white, levelPunch);
        float s = 1f + levelPunch * .2f;
        levelText.rectTransform.localScale = new Vector3(s, s, 1);
    }
}
