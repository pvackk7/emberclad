using UnityEngine;
using UnityEngine.UI;

// Elden Ring-style menu highlight: no stone plate, just a soft ember glow behind the focused item.
// Inherits MenuPlateGraphic so MenuFocusButton drives it through the same `focus` value.
public class MenuGlowGraphic : MenuPlateGraphic
{
    static readonly Color Ember = new Color(0.478f, 0.306f, 0.153f); // #7A4E27
    const int Segments = 48;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        // Geometry is always emitted (alpha 0 when idle) so the item stays clickable.
        Rect r = rectTransform.rect;
        Vector2 c = r.center;
        float rx = r.width * 0.5f, ry = r.height * 0.5f;
        Color core = Ember, mid = Ember, edge = Ember;
        core.a = 0.55f * focus; mid.a = 0.18f * focus; edge.a = 0f;
        vh.AddVert(c, core, Vector2.zero);
        for (int ring = 0; ring < 2; ring++)
        {
            float k = ring == 0 ? 0.45f : 1f;
            for (int i = 0; i < Segments; i++)
            {
                float a = i * Mathf.PI * 2f / Segments;
                vh.AddVert(c + new Vector2(Mathf.Cos(a) * rx * k, Mathf.Sin(a) * ry * k), ring == 0 ? mid : edge, Vector2.zero);
            }
        }
        for (int i = 0; i < Segments; i++)
        {
            int a0 = 1 + i, a1 = 1 + (i + 1) % Segments;
            int b0 = a0 + Segments, b1 = a1 + Segments;
            vh.AddTriangle(0, a0, a1);
            vh.AddTriangle(a0, b0, b1);
            vh.AddTriangle(a0, b1, a1);
        }
        Color gold = new Color(.94f, .76f, .53f, focus * .75f);
        float span = Mathf.Min(54, r.width * .12f);
        Stroke(vh, new Vector2(r.xMin + 12, c.y), span, gold);
        Stroke(vh, new Vector2(r.xMax - 12 - span, c.y), span, gold);
        Diamond(vh, new Vector2(r.xMin + 7, c.y), gold);
        Diamond(vh, new Vector2(r.xMax - 7, c.y), gold);
    }

    void Stroke(VertexHelper vh, Vector2 p, float width, Color tint)
    {
        int n = vh.currentVertCount;
        vh.AddVert(p + new Vector2(0, -.7f), tint, Vector2.zero);
        vh.AddVert(p + new Vector2(0, .7f), tint, Vector2.zero);
        vh.AddVert(p + new Vector2(width, .7f), tint, Vector2.zero);
        vh.AddVert(p + new Vector2(width, -.7f), tint, Vector2.zero);
        vh.AddTriangle(n, n + 1, n + 2); vh.AddTriangle(n, n + 2, n + 3);
    }

    void Diamond(VertexHelper vh, Vector2 p, Color tint)
    {
        int n = vh.currentVertCount;
        vh.AddVert(p + Vector2.left * 3, tint, Vector2.zero);
        vh.AddVert(p + Vector2.up * 3, tint, Vector2.zero);
        vh.AddVert(p + Vector2.right * 3, tint, Vector2.zero);
        vh.AddVert(p + Vector2.down * 3, tint, Vector2.zero);
        vh.AddTriangle(n, n + 1, n + 2); vh.AddTriangle(n, n + 2, n + 3);
    }
}
