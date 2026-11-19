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

    /// keys/labels are parallel: each pair becomes one row of key caps and a caption.
    /// Key names are the defaults KeyRow understands, so they re-map to whatever the
    /// player has actually bound.
    public void TeachCard(string title, string body, string[] keys, string[] captions, float dur = 7f)
    {
        if (root == null) return;
        if (teachCard != null) Destroy(teachCard.gameObject);

        int rows = keys == null ? 0 : keys.Length;
        float height = 92f + rows * 44f + (string.IsNullOrEmpty(body) ? 0f : 44f);
        Vector2 BC = new Vector2(.5f, 0), TL = new Vector2(0, 1), TC = new Vector2(.5f, 1);

        teachCard = Rect("Lesson", root, BC, new Vector2(0, 152), new Vector2(780, height), BC);
        teachCg = Group(teachCard, 0f);
        // Rounded and soft rather than the hard-edged stone plate the menus use: this
        // thing sits over the fight, so it should feel like a card laid down on top of
        // the scene instead of a slab cut out of it.
        Vector2 C = new Vector2(.5f, .5f);
        Img("ui_round", teachCard, C, new Vector2(0, -7), new Vector2(788, height + 6), new Color(0f, 0f, 0f, .30f), C, true);
        Img("ui_round", teachCard, C, Vector2.zero, new Vector2(780, height), new Color(.52f, .40f, .28f, .62f), C, true);
        Img("ui_round", teachCard, C, Vector2.zero, new Vector2(770, height - 10), new Color(.072f, .058f, .070f, .95f), C, true);
        Img("ui_round", teachCard, C, new Vector2(0, (height - 10) * .25f), new Vector2(758, (height - 10) * .5f), new Color(.19f, .15f, .17f, .40f), C, true);

        var heading = Txt(title.ToUpperInvariant(), teachCard, TC, new Vector2(0, -18), new Vector2(720, 40), 30, Gold, TextAnchor.MiddleCenter, TC);
        if (menuTitleFont != null) heading.font = menuTitleFont;

        float y = -70f;
        for (int i = 0; i < rows; i++)
        {
            KeyRow(teachCard, TL, new Vector2(62, y), new[] { keys[i] }, captions[i], .62f, 23);
            y -= 44f;
        }
        if (!string.IsNullOrEmpty(body))
            Txt(body, teachCard, TC, new Vector2(0, y - 6), new Vector2(712, 42), 21, Cream, TextAnchor.MiddleCenter, TC);

        teachT = teachDur = dur;
        Sfx.Play("mana", .42f, 0, 1.35f);
    }

    void TickTeach(float dt)
    {
        if (teachCard == null) return;
        teachT -= dt;
        float gone = 1f - Mathf.Clamp01(teachT / teachDur);
        // Snap on, hold, drift off. The fade-out is the slower half so it never
        // vanishes mid-read.
        teachCg.alpha = Mathf.Clamp01(Mathf.Min(gone * 6f, teachT * 1.6f));
        float pop = 1f + Mathf.Max(0f, .10f - gone * 1.2f);
        teachCard.localScale = new Vector3(pop, pop, 1);
        if (teachT <= 0f) { Destroy(teachCard.gameObject); teachCard = null; }
    }
}
