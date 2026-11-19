using UnityEngine;
using UnityEngine.UI;

public class MenuFireWisp : MaskableGraphic
{
    readonly Vector2[] history = new Vector2[220];
    Vector2 position = new Vector2(.18f, .58f), velocity = new Vector2(.08f, .045f), target;
    float clock, nextTarget, sampleClock;
    int count, cursor;
    System.Random random;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
        random = new System.Random();
        PickTarget();
    }

    void PickTarget()
    {
        target = new Vector2(.09f + (float)random.NextDouble() * .82f, .14f + (float)random.NextDouble() * .73f);
        nextTarget = clock + 3 + (float)random.NextDouble() * 4;
    }

    void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
        clock += dt;
        if (clock > nextTarget || Vector2.Distance(position, target) < .09f) PickTarget();
        Vector2 to = target - position;
        Vector2 tangent = new Vector2(-to.y, to.x).normalized;
        Vector2 desired = to.normalized * .105f + tangent * Mathf.Sin(clock * .8f) * .045f;
        velocity = Vector2.Lerp(velocity, desired, 1 - Mathf.Exp(-dt * .65f));
        position += velocity * dt;
        position.x = Mathf.Clamp(position.x, .04f, .96f);
        position.y = Mathf.Clamp(position.y, .07f, .94f);
        sampleClock += dt;
        while (sampleClock >= .025f)
        {
            sampleClock -= .025f;
            cursor = (cursor + 1) % history.Length;
            history[cursor] = position;
            count = Mathf.Min(count + 1, history.Length);
        }
        SetVerticesDirty();
    }

    Vector2 Point(Vector2 p)
    {
        Rect r = rectTransform.rect;
        return new Vector2(r.xMin + p.x * r.width, r.yMin + p.y * r.height);
    }

    void Glow(VertexHelper vh, Vector2 p, Vector2 radius, Color tint)
    {
        int n = vh.currentVertCount;
        vh.AddVert(p, tint, Vector2.zero);
        Color edge = new Color(tint.r, tint.g, tint.b, 0);
        for (int i = 0; i <= 16; i++)
        {
            float a = i * Mathf.PI / 8;
            vh.AddVert(p + new Vector2(Mathf.Cos(a) * radius.x, Mathf.Sin(a) * radius.y), edge, Vector2.zero);
            if (i > 0) vh.AddTriangle(n, n + i, n + i + 1);
        }
    }

    void Ribbon(VertexHelper vh, Vector2 a, Vector2 b, float width, Color tint)
    {
        Vector2 d = (b - a).normalized;
        Vector2 n = new Vector2(-d.y, d.x) * width;
        int index = vh.currentVertCount;
        vh.AddVert(a - n, tint, Vector2.zero); vh.AddVert(a + n, tint, Vector2.zero);
        vh.AddVert(b + n, tint, Vector2.zero); vh.AddVert(b - n, tint, Vector2.zero);
        vh.AddTriangle(index, index + 1, index + 2); vh.AddTriangle(index, index + 2, index + 3);
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        float unit = Mathf.Min(rectTransform.rect.width / 1920f, rectTransform.rect.height / 1080f);
        float appear = Mathf.Clamp01(clock);
        for (int i = count - 1; i > 0; i--)
        {
            float age = i / (float)history.Length;
            float strength = Mathf.Pow(1 - age, 1.7f) * appear;
            Vector2 p = Point(history[(cursor - i + history.Length) % history.Length]);
            Vector2 q = Point(history[(cursor - i + 1 + history.Length) % history.Length]);
            Glow(vh, p, Vector2.one * (5 + strength * 14) * unit, new Color(1, .74f, .25f, strength * .045f));
            Ribbon(vh, p, q, (.25f + strength * 1.5f) * unit, new Color(1, .87f, .49f, strength * .65f));
            for (int strand = 0; strand < 2; strand++)
            {
                float wave = Mathf.Sin(i * .12f - clock * 1.3f + strand * Mathf.PI);
                Vector2 offset = new Vector2(wave * 8, Mathf.Cos(i * .09f + strand * 3) * 6) * unit * (age + .15f);
                Glow(vh, p + offset, Vector2.one * 2.5f * unit, new Color(1, .91f, .59f, strength * .27f));
            }
            if (i % 4 == 0)
            {
                float seed = Mathf.Repeat(i * .618034f, 1);
                p += new Vector2(Mathf.Sin(i * 7.1f) * 38 * age, age * 50 + Mathf.Cos(i * 2.3f) * age * 22) * unit;
                Glow(vh, p, Vector2.one * (1.5f + seed * 2.5f) * unit, new Color(1, .89f, .54f, strength * (.4f + .3f * Mathf.Sin(clock * 3 + i))));
            }
        }
        Vector2 head = Point(position);
        float pulse = 1 + .12f * Mathf.Sin(clock * 2.7f);
        Glow(vh, head, Vector2.one * 52 * unit * pulse, new Color(1, .79f, .31f, .16f * appear));
        Glow(vh, head, new Vector2(19, 25) * unit * pulse, new Color(1, .86f, .48f, .65f * appear));
        Glow(vh, head, new Vector2(5, 10) * unit, new Color(1, .98f, .79f, appear));
        Glow(vh, head, new Vector2(16, 1.5f) * unit, new Color(1, .96f, .7f, .7f * appear));
    }
}
