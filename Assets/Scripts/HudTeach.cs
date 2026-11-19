using UnityEngine;
using UnityEngine.UI;

/// The teaching card: one short lesson, shown once, never pausing the game.
///
/// The rule it exists to serve is that the player is told a thing and then left to
/// get on with it. The card names the keys and one reason to care, then fades on its
/// own while the fight carries on around it. Nothing waits for a click, nothing gates
/// on the player actually using what they were shown, and after the third lesson the
/// card never appears again.
public partial class Hud
{
    RectTransform teachCard;
    CanvasGroup teachCg;
    float teachT, teachDur;
    // Where the card comes to rest, and how far below that it starts.
    const float TeachY = 152f, TeachRise = 72f;
    // One width for every lesson, so cards do not change shape between rounds, and
    // one margin that the heading, the rows and the body all line up against.
    const float CardW = 520f, CardPad = 30f;
    const float MaxTeachSeconds = 30f;

    /// The lesson's condition was met: let the card leave now (its usual lift-and-fade) instead of
    /// sitting out the rest of its timer.
    public void DismissTeach() { if (teachCard != null) teachT = Mathf.Min(teachT, .4f); }

    /// Death, victory or a scene change: the card goes at once.
    public void ClearTeach() { if (teachCard != null) { Destroy(teachCard.gameObject); teachCard = null; } }

    /// keys/labels are parallel: each pair becomes one row of key caps and a caption.
    /// Key names are the defaults KeyRow understands, so they re-map to whatever the
    /// player has actually bound.
    public void TeachCard(string title, string body, string[] keys, string[] captions, float dur = 7f)
    {
        if (root == null) return;
        if (game != null && game.state != Game.State.Playing) return;   // never over the death or victory screen
        if (teachCard != null) Destroy(teachCard.gameObject);
        if (TouchControls.Enabled) TouchLesson(ref keys, ref captions);
        // Hard ceiling on any card's life, whatever the caller asked for.
        dur = Mathf.Clamp(dur, 1f, MaxTeachSeconds);

        int rows = keys == null ? 0 : keys.Length;
        Vector2 BC = new Vector2(.5f, 0), TL = new Vector2(0, 1), TC = new Vector2(.5f, 1);

        // Height is settled at the end, once the body has been measured. Everything
        // inside hangs off the top edge, so growing the card downward cannot shift
        // any of it.
        teachCard = Rect("Lesson", root, BC, new Vector2(0, TeachY - TeachRise), new Vector2(CardW, 10f), BC);
        teachCg = Group(teachCard, 0f);

        // Drawn with the plate every other panel uses rather than a nine-sliced
        // sprite stretched to fit: the sprite's corners scaled with the card and
        // turned it into a lozenge, which is why a lesson never looked like it came
        // from the same game as the screen that introduced it.
        var plate = Full("Plate", teachCard);
        plate.gameObject.AddComponent<MenuPlateGraphic>().raycastTarget = false;

        var heading = Txt(title.ToUpperInvariant(), teachCard, TC, new Vector2(0, -18), new Vector2(CardW - CardPad * 2, 40), 29, Gold, TextAnchor.MiddleCenter, TC);
        if (menuTitleFont != null) heading.font = menuTitleFont;

        // Keys only: the explanations were cut (the user found them unnecessary). The body
        // parameter is kept so callers compile, but nothing is drawn for it.
        float y = -64f;
        if (rows > 0)
        {
            var rowKeys = new string[rows][];
            for (int i = 0; i < rows; i++) rowKeys[i] = new[] { keys[i] };
            y -= KeyBlock(teachCard, y, rowKeys, captions);
        }
        teachCard.sizeDelta = new Vector2(CardW, -y + 18f);

        teachT = teachDur = dur;
        Sfx.Play("mana", .42f, 0, 1.35f);
    }

    void TickTeach(float dt)
    {
        if (teachCard == null) return;
        if (game != null && game.state != Game.State.Playing) { ClearTeach(); return; }
        teachT -= dt;
        float age = teachDur - teachT;

        // Flies up into place rather than developing out of nothing. Cubic ease-out
        // covers most of the distance in the first tenth of a second and then settles,
        // which is what makes it read as arriving. The old fade took six sevenths of a
        // second just to reach full opacity.
        float rise = 1f - Mathf.Pow(1f - Mathf.Clamp01(age / .32f), 3f);
        float fadeIn = Mathf.Clamp01(age / .15f);
        float fadeOut = Mathf.Clamp01(teachT / .40f);

        teachCg.alpha = Mathf.Min(fadeIn, fadeOut);
        // Keeps lifting on the way out, so it leaves the way it came in.
        teachCard.anchoredPosition = new Vector2(0, TeachY - TeachRise * (1f - rise) + (1f - fadeOut) * 30f);

        if (teachT <= 0f) { Destroy(teachCard.gameObject); teachCard = null; }
    }
}
