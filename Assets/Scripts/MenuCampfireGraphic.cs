using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class MenuCampfireGraphic : MaskableGraphic
{
    float clock = 4.7f;
    const float CenterX = .842f;
    const int Steps = 36;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    void Update()
    {
        clock += Time.unscaledDeltaTime;
        SetVerticesDirty();
    }

    Vector2 Point(float x, float y)
    {
        Rect r = rectTransform.rect;
        return new Vector2(r.xMin + x * r.width, r.yMin + y * r.height);
    }

    float LogEdge(float x)
    {
        return .100f + .041f * Mathf.Clamp01(1 - Mathf.Abs(x - CenterX) / .046f);
    }

    void Tongue(VertexHelper vh, float offset, float width, float height, float phase, Color tint, float lift = 0)
    {
        int first = vh.currentVertCount;
        float pulse = .88f + .12f * Mathf.Sin(clock * 4.7f + phase) + .07f * Mathf.Sin(clock * 8.3f + phase * 1.7f);
        float baseY = .100f + lift;
        for (int i = 0; i <= Steps; i++)
        {
            float t = (float)i / Steps;
            float wave = Mathf.Sin(t * 7.5f - clock * 5.3f + phase);
            float curl = Mathf.Sin(t * 12f - clock * 3.7f + phase * 2);
            float x = CenterX + offset * (1 - t * .2f) + t * t * (.010f * wave + .005f * curl);
            float w = width * Mathf.Pow(1 - t, .8f) * (1 + .20f * Mathf.Sin(t * 12 - clock * 6 + phase));
            float y = baseY + t * height * pulse;
            float left = x - w, right = x + w;
            vh.AddVert(Point(left, Mathf.Max(y, LogEdge(left))), tint, Vector2.zero);
            vh.AddVert(Point(right, Mathf.Max(y, LogEdge(right))), tint, Vector2.zero);
            if (i == 0) continue;
            int n = first + i * 2;
            vh.AddTriangle(n - 2, n, n - 1);
            vh.AddTriangle(n - 1, n, n + 1);
        }
    }

    void Drop(VertexHelper vh, float phase)
    {
        float life = Mathf.Repeat(clock * .7f + phase, 1);
        float size = Mathf.Sin(life * Mathf.PI) * .006f;
        float x = CenterX + Mathf.Sin(phase * 13 + clock * 1.7f) * .017f;
        float y = .27f + life * .105f;
        int n = vh.currentVertCount;
        Color tint = new Color(1, .43f + .15f * (1 - life), .08f, 1 - life * life);
        vh.AddVert(Point(x - size, y), tint, Vector2.zero);
        vh.AddVert(Point(x - size * .6f, y + size * 1.4f), tint, Vector2.zero);
        vh.AddVert(Point(x + size * .35f, y + size * 3), tint, Vector2.zero);
        vh.AddVert(Point(x + size, y + size * .6f), tint, Vector2.zero);
        vh.AddVert(Point(x, y - size * .7f), tint, Vector2.zero);
        vh.AddTriangle(n, n + 1, n + 2);
        vh.AddTriangle(n, n + 2, n + 3);
        vh.AddTriangle(n, n + 3, n + 4);
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Color rim = new Color(.92f, .23f, .035f);
        Color orange = new Color(1, .43f, .055f);
        Color gold = new Color(1, .75f, .10f);
        Color core = new Color(1, .92f, .33f);
        Tongue(vh, -.024f, .018f, .15f, 2.1f, rim);
        Tongue(vh, .023f, .019f, .18f, 4.3f, rim);
        Tongue(vh, -.002f, .027f, .245f, .4f, rim);
        Tongue(vh, -.024f, .015f, .14f, 2.1f, orange);
        Tongue(vh, .023f, .016f, .17f, 4.3f, orange);
        Tongue(vh, -.002f, .024f, .232f, .4f, orange);
        Tongue(vh, -.016f, .014f, .12f, 3.7f, gold);
        Tongue(vh, .012f, .017f, .17f, 1.3f, gold);
        Tongue(vh, -.001f, .015f, .115f, 5.2f, core);
        Drop(vh, .1f);
        Drop(vh, .65f);
    }
}
