using UnityEngine;
using UnityEngine.UI;

/// The stone plate behind panels and menu buttons.
///
/// Rounded rather than cut square: every panel in the game is drawn with this, so the
/// corner radius here is what stops the HUD reading as a stack of hard rectangles laid
/// over the forest. Still two flat layers -- a warm rim and a dark face -- because the
/// rim is also what MenuFocusButton brightens through `focus`.
[RequireComponent(typeof(CanvasRenderer))]
public class MenuPlateGraphic : MaskableGraphic
{
    public float focus;

    const int CornerSegments = 5;

    void RoundRect(VertexHelper vh, Rect r, float radius, Color tint)
    {
        if (r.width <= 0f || r.height <= 0f) return;
        radius = Mathf.Max(0f, Mathf.Min(radius, Mathf.Min(r.width, r.height) * .5f));
        int first = vh.currentVertCount;
        vh.AddVert(r.center, tint, Vector2.zero);

        // The fan has to walk the outline in one direction. Quadrant k covers angles
        // k*90 to (k+1)*90, so the corner it bends around is the one that quadrant
        // points at: right-top, left-top, left-bottom, right-bottom. Pairing the
        // quadrants with corners in any other order folds the fan through the middle
        // of the rect and draws a bowtie.
        int ring = 0;
        for (int k = 0; k < 4; k++)
        {
            float cx = (k == 0 || k == 3) ? r.xMax - radius : r.xMin + radius;
            float cy = (k == 0 || k == 1) ? r.yMax - radius : r.yMin + radius;
            for (int i = 0; i <= CornerSegments; i++)
            {
                float a = (k * 90f + i * (90f / CornerSegments)) * Mathf.Deg2Rad;
                vh.AddVert(new Vector2(cx + Mathf.Cos(a) * radius, cy + Mathf.Sin(a) * radius), tint, Vector2.zero);
                ring++;
            }
        }
        for (int i = 0; i < ring; i++)
            vh.AddTriangle(first, first + 1 + i, first + 1 + (i + 1) % ring);
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        // Small panels get a proportionally smaller radius so a narrow button does not
        // turn into a lozenge.
        float radius = Mathf.Min(18f, Mathf.Min(r.width, r.height) * .32f);
        RoundRect(vh, r, radius, Color.Lerp(new Color(.39f, .32f, .24f, .7f), new Color(.79f, .61f, .38f, .9f), focus));
        RoundRect(vh, new Rect(r.xMin + 2, r.yMin + 2, r.width - 4, r.height - 4), radius - 2f, new Color(.045f, .035f, .055f, .92f));
    }
}
