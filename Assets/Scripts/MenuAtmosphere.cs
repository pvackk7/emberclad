using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class MenuAtmosphere : MaskableGraphic
{
    public bool paintedTavern;
    struct Spark { public float x, speed, life, phase, size; }
    readonly Spark[] sparks = new Spark[34];
    float clock, nextStar = 8, starAge = 3;
    Vector2 starOrigin;
    Material backdropMaterial;
    Image backdrop;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
        for (int i = 0; i < sparks.Length; i++)
        {
            float seed = Mathf.Repeat(i * 0.618034f, 1);
            sparks[i] = new Spark { x = seed, speed = 0.15f + seed * 0.12f, life = seed, phase = i * 2.37f, size = 0.0008f + seed * 0.0008f };
        }
        backdrop = transform.parent.GetComponent<Image>();
        var shader = Shader.Find("Emberclad/LivingMenu");
        if (shader != null && backdrop != null)
        {
            backdropMaterial = new Material(shader);
            backdrop.material = backdropMaterial;
        }
    }

    void Update()
    {
        clock += Time.unscaledDeltaTime;
        starAge += Time.unscaledDeltaTime;
        if (clock >= nextStar)
        {
            starAge = 0;
            starOrigin = paintedTavern ? new Vector2(Random.Range(0.45f, 0.60f), Random.Range(0.78f, 0.88f)) : new Vector2(Random.Range(0.28f, 0.67f), Random.Range(0.81f, 0.95f));
            nextStar = clock + Random.Range(14f, 27f);
        }
        if (backdropMaterial != null)
        {
            backdropMaterial.SetFloat("_MenuTime", clock);
            backdropMaterial.SetFloat("_PaintedTavern", paintedTavern ? 1 : 0);
        }
        SetVerticesDirty();
    }

    protected override void OnDestroy()
    {
        if (backdrop != null) backdrop.material = null;
        if (backdropMaterial != null) Destroy(backdropMaterial);
        base.OnDestroy();
    }

    Vector2 Point(float x, float y)
    {
        Rect r = rectTransform.rect;
        if (paintedTavern)
        {
            x += .014f;
            y -= .065f;
        }
        return new Vector2(r.xMin + x * r.width, r.yMin + y * r.height);
    }

    void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
    {
        int n = vh.currentVertCount;
        vh.AddVert(a, color, Vector2.zero); vh.AddVert(b, color, Vector2.zero);
        vh.AddVert(c, color, Vector2.zero); vh.AddVert(d, color, Vector2.zero);
        vh.AddTriangle(n, n + 1, n + 2); vh.AddTriangle(n, n + 2, n + 3);
    }

    void Glow(VertexHelper vh, Vector2 p, Vector2 radius, Color tint)
    {
        int n = vh.currentVertCount;
        vh.AddVert(p, tint, Vector2.zero);
        Color edge = new Color(tint.r, tint.g, tint.b, 0);
        for (int i = 0; i <= 32; i++)
        {
            float angle = i * Mathf.PI / 16;
            vh.AddVert(p + new Vector2(Mathf.Cos(angle) * radius.x, Mathf.Sin(angle) * radius.y), edge, Vector2.zero);
            if (i > 0) vh.AddTriangle(n, n + i, n + i + 1);
        }
    }

    void Shape(VertexHelper vh, Vector2[] points, Color tint)
    {
        int start = vh.currentVertCount;
        Vector2 middle = Vector2.zero;
        foreach (var p in points) middle += p;
        middle /= points.Length;
        vh.AddVert(Point(middle.x, middle.y), tint, Vector2.zero);
        foreach (var p in points) vh.AddVert(Point(p.x, p.y), tint, Vector2.zero);
        for (int i = 0; i < points.Length; i++) vh.AddTriangle(start, start + i + 1, start + (i + 1) % points.Length + 1);
    }

    void Flame(VertexHelper vh, float x, float y, float width, float height, float phase, Color tint)
    {
        float sway = Mathf.Sin(clock * 4.1f + phase) * .005f + Mathf.Sin(clock * 7.3f + phase * 2) * .002f;
        height *= 1 + Mathf.Sin(clock * 5.7f + phase) * .10f;
        var outline = new[] {
            new Vector2(x - width * .64f, y), new Vector2(x - width, y + height * .22f),
            new Vector2(x - width * .72f, y + height * .50f), new Vector2(x - width * .68f + sway * .4f, y + height * .73f),
            new Vector2(x - width * .20f, y + height * .54f), new Vector2(x + sway * .8f, y + height * .77f),
            new Vector2(x + width * .21f + sway, y + height), new Vector2(x + width * .09f + sway * .7f, y + height * .66f),
            new Vector2(x + width * .75f, y + height * .43f), new Vector2(x + width, y + height * .20f),
            new Vector2(x + width * .55f, y) };
        var curve = new Vector2[outline.Length * 4];
        for (int i = 0; i < outline.Length; i++)
        {
            Vector2 a = outline[(i + outline.Length - 1) % outline.Length], b = outline[i];
            Vector2 c = outline[(i + 1) % outline.Length], d = outline[(i + 2) % outline.Length];
            for (int j = 0; j < 4; j++)
            {
                float t = j / 4f;
                curve[i * 4 + j] = .5f * (2 * b + (-a + c) * t + (2 * a - 5 * b + 4 * c - d) * t * t + (-a + 3 * b - 3 * c + d) * t * t * t);
            }
        }
        Shape(vh, curve, tint);
    }

    void Smoke(VertexHelper vh)
    {
        int start = vh.currentVertCount;
        for (int i = 0; i <= 28; i++)
        {
            float t = i / 28f;
            float x = (paintedTavern ? .077f : .103f) + t * .025f + Mathf.Sin(t * 8 - clock * .75f) * t * .010f + Mathf.Sin(t * 16 - clock * 1.2f) * t * .003f;
            float y = (paintedTavern ? 1.015f : .786f) + t * .23f;
            float width = .003f + t * .017f;
            float alpha = Mathf.Pow(1 - t, 1.4f) * .30f;
            vh.AddVert(Point(x - width, y), new Color(.36f, .39f, .31f, 0), Vector2.zero);
            vh.AddVert(Point(x, y), new Color(.36f, .39f, .31f, alpha), Vector2.zero);
            vh.AddVert(Point(x + width, y), new Color(.36f, .39f, .31f, 0), Vector2.zero);
            if (i == 0) continue;
            int n = start + i * 3;
            vh.AddTriangle(n - 3, n - 2, n); vh.AddTriangle(n - 2, n + 1, n);
            vh.AddTriangle(n - 2, n - 1, n + 1); vh.AddTriangle(n - 1, n + 2, n + 1);
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        Smoke(vh);
        float flicker = 0.9f + Mathf.Sin(clock * 7.3f) * 0.06f + Mathf.Sin(clock * 12.7f) * 0.04f;
        Glow(vh, Point(0.822f, 0.145f), new Vector2(r.width * 0.125f * flicker, r.height * 0.073f), new Color(1, 0.47f, 0.08f, 0.18f * flicker));
        Glow(vh, Point(0.822f, 0.25f), new Vector2(r.width * 0.055f, r.height * 0.13f), new Color(1, 0.53f, 0.12f, 0.08f * flicker));
        Glow(vh, Point(paintedTavern ? .305f : .056f, paintedTavern ? .505f : .397f), new Vector2(r.width * 0.033f, r.height * 0.045f), new Color(1, 0.61f, 0.20f, 0.13f * flicker));
        if (!paintedTavern)
        {
            Flame(vh, .810f, .209f, .013f, .071f, 2, new Color(.82f, .39f, .13f));
            Flame(vh, .834f, .207f, .017f, .085f, 4, new Color(.90f, .48f, .18f));
            Flame(vh, .821f, .206f, .022f, .116f, 0, new Color(.96f, .57f, .22f));
            Flame(vh, .822f, .206f, .014f, .076f, 1, new Color(1, .72f, .34f));
            Flame(vh, .822f, .207f, .007f, .042f, 3, new Color(1, .86f, .55f));
        }
        foreach (var s in sparks)
        {
            float life = Mathf.Repeat(s.life + clock * s.speed, 1);
            float x = 0.821f + (s.x - 0.5f) * 0.045f + Mathf.Sin(clock * 1.8f + s.phase + life * 4) * life * 0.016f;
            float y = 0.213f + life * (0.15f + s.x * 0.09f);
            float alpha = Mathf.Sin(life * Mathf.PI) * 0.9f;
            Vector2 p = Point(x, y), size = new Vector2(r.width * s.size * (1 - life * 0.6f), r.height * s.size * 1.8f);
            Quad(vh, p + new Vector2(-size.x, 0), p + new Vector2(0, -size.y), p + new Vector2(size.x, 0), p + new Vector2(0, size.y), new Color(1, 0.56f + s.x * 0.35f, 0.15f, alpha));
        }
        if (starAge < 0.85f)
        {
            Vector2 head = Point(starOrigin.x + starAge * 0.20f, starOrigin.y - starAge * 0.10f);
            Vector2 tail = head - new Vector2(r.width * 0.065f, -r.height * 0.0325f);
            Vector2 normal = new Vector2(r.width * 0.00045f, r.height * 0.0016f);
            int n = vh.currentVertCount;
            Color bright = new Color(0.87f, 0.94f, 1, Mathf.Sin(starAge / 0.85f * Mathf.PI));
            vh.AddVert(head + normal, bright, Vector2.zero); vh.AddVert(head - normal, bright, Vector2.zero);
            vh.AddVert(tail, new Color(0.7f, 0.85f, 1, 0), Vector2.zero); vh.AddTriangle(n, n + 1, n + 2);
        }
    }
}
