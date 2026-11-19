using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ForestScenery : MonoBehaviour
{
    readonly List<Mesh> meshes = new List<Mesh>();
    Material material;
    static Color C(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out var c); return QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c; }

    sealed class Ink
    {
        public readonly List<Vector3> vertices = new List<Vector3>();
        public readonly List<int> indices = new List<int>();
        public readonly List<Color> colors = new List<Color>();

        public void Poly(Color c, params Vector2[] p)
        {
            int start = vertices.Count;
            foreach (var v in p) { vertices.Add(v); colors.Add(c); }
            var ring = new List<int>();
            float area = 0;
            for (int i = 0; i < p.Length; i++) { ring.Add(i); var b = p[(i + 1) % p.Length]; area += p[i].x * b.y - b.x * p[i].y; }
            if (area < 0) ring.Reverse();
            int guard = p.Length * p.Length;
            while (ring.Count > 2 && guard-- > 0)
            {
                bool cut = false;
                for (int j = 0; j < ring.Count; j++)
                {
                    int a = ring[(j + ring.Count - 1) % ring.Count], b = ring[j], d = ring[(j + 1) % ring.Count];
                    if (Cross(p[b] - p[a], p[d] - p[b]) <= .000001f) continue;
                    bool inside = false;
                    foreach (int k in ring)
                    {
                        if (k == a || k == b || k == d) continue;
                        if (Cross(p[b] - p[a], p[k] - p[a]) >= 0 && Cross(p[d] - p[b], p[k] - p[b]) >= 0 && Cross(p[a] - p[d], p[k] - p[d]) >= 0) { inside = true; break; }
                    }
                    if (inside) continue;
                    indices.Add(start + a); indices.Add(start + b); indices.Add(start + d); ring.RemoveAt(j); cut = true; break;
                }
                if (!cut) break;
            }
        }
        static float Cross(Vector2 a, Vector2 b) { return a.x * b.y - a.y * b.x; }
        public void Stroke(Color c, float width, params Vector2[] p)
        {
            for (int i = 1; i < p.Length; i++)
            {
                Vector2 n = new Vector2(-(p[i] - p[i - 1]).y, (p[i] - p[i - 1]).x).normalized * width * .5f;
                Poly(c, p[i - 1] + n, p[i - 1] - n, p[i] - n, p[i] + n);
            }
        }
        public void Oval(Color c, float x, float y, float w, float h, int count = 18)
        {
            var p = new Vector2[count];
            for (int i = 0; i < count; i++) { float a = i * Mathf.PI * 2 / count; p[i] = new Vector2(x + Mathf.Cos(a) * w, y + Mathf.Sin(a) * h); }
            Poly(c, p);
        }
        public void Gradient(Color top, Color bottom, float yTop, float yBottom)
        {
            int start = vertices.Count;
            vertices.Add(new Vector3(-35, yTop)); vertices.Add(new Vector3(-35, yBottom));
            vertices.Add(new Vector3(180, yBottom)); vertices.Add(new Vector3(180, yTop));
            colors.Add(top); colors.Add(bottom); colors.Add(bottom); colors.Add(top);
            indices.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
        }
        public void Band(Color color, Func<float, float> top, Func<float, float> bottom, float from = -35, float to = 180)
        {
            for (float x = from; x < to; x += .4f)
            {
                float end = Mathf.Min(to, x + .4f);
                Poly(color, new Vector2(x, top(x)), new Vector2(x, bottom(x)), new Vector2(end, bottom(end)), new Vector2(end, top(end)));
            }
        }
    }

    void Finish(string name, Ink ink, int order, float parallax = 0, string layer = "Background")
    {
        var go = new GameObject(name); go.transform.SetParent(transform, false);
        var mesh = new Mesh { name = name };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(ink.vertices); mesh.SetColors(ink.colors); mesh.SetTriangles(ink.indices, 0); mesh.RecalculateBounds(); meshes.Add(mesh);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.sortingLayerName = layer; renderer.sortingOrder = order;
        if (parallax > 0) { var p = go.AddComponent<Parallax>(); p.factor = parallax; }
    }

    public static void Build(Transform parent)
    {
        var go = new GameObject("The Hollow Wood"); go.transform.SetParent(parent, false);
        go.AddComponent<ForestScenery>().Draw();
    }

    void Limb(Ink ink, Vector2 start, Vector2 control, Vector2 end, float baseWidth, Color bark, Color light, Color edge, bool outline)
    {
        const int steps = 14;
        var left = new Vector2[steps + 1];
        var right = new Vector2[steps + 1];
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            Vector2 c = (1 - t) * (1 - t) * start + 2 * (1 - t) * t * control + t * t * end;
            Vector2 tangent = ((1 - t) * (control - start) + t * (end - control)).normalized;
            Vector2 n = new Vector2(-tangent.y, tangent.x);
            float w = baseWidth * Mathf.Lerp(1, .08f, t);
            left[i] = c + n * w; right[i] = c - n * w;
        }
        var shape = new Vector2[(steps + 1) * 2];
        for (int i = 0; i <= steps; i++) { shape[i] = left[i]; shape[steps + 1 + i] = right[steps - i]; }
        ink.Poly(bark, shape);
        if (outline) { ink.Stroke(edge, .035f, left); ink.Stroke(edge, .035f, right); }
        for (int i = 0; i < steps; i++)
            ink.Poly(light, left[i], left[i + 1], Vector2.Lerp(left[i + 1], right[i + 1], .20f), Vector2.Lerp(left[i], right[i], .20f));
    }

    void Leaves(Ink ink, float x, float y, float w, float h, int seed, Color foliage, Color light, Color edge, bool outline)
    {
        var crown = new Vector2[54];
        for (int j = 0; j < crown.Length; j++)
        {
            float a = j * Mathf.PI * 2 / crown.Length;
            float r = 1 + .12f * Mathf.Sin(a * 5 + seed) + .07f * Mathf.Sin(a * 9 + seed * .7f);
            crown[j] = new Vector2(x + Mathf.Cos(a) * w * r, y + Mathf.Sin(a) * h * r);
        }
        ink.Poly(foliage, crown);
        if (outline) { var closed = new List<Vector2>(crown); closed.Add(crown[0]); ink.Stroke(edge, .038f, closed.ToArray()); }
        var cap = new Vector2[30];
        for (int j = 0; j < 19; j++)
        {
            float a = .12f + j * (Mathf.PI - .24f) / 18;
            float r = 1 + .12f * Mathf.Sin(a * 5 + seed) + .07f * Mathf.Sin(a * 9 + seed * .7f);
            cap[j] = new Vector2(x + Mathf.Cos(a) * w * r * .91f, y + Mathf.Sin(a) * h * r * .88f);
        }
        for (int j = 0; j < 11; j++)
        {
            float t = j / 10f;
            cap[j + 19] = new Vector2(x + Mathf.Lerp(-.88f, .88f, t) * w, y + (.03f + .12f * Mathf.Sin(t * 17 + seed)) * h);
        }
        ink.Poly(Color.Lerp(foliage, light, outline ? .34f : .16f), cap);
        if (!outline) return;
        for (int j = 0; j < 3; j++)
        {
            float xx = x + (j - 1) * w * .42f, yy = y + h * (.20f + .1f * Mathf.Sin(seed + j));
            ink.Stroke(Color.Lerp(foliage, light, .48f), .035f,
                new Vector2(xx - .13f, yy), new Vector2(xx - .06f, yy + .055f), new Vector2(xx + .02f, yy + .035f), new Vector2(xx + .1f, yy + .09f));
        }
    }

    void Tree(Ink ink, float x, float y, float height, float width, int seed, Color bark, Color light, Color foliage, bool outline)
    {
        float bend = Mathf.Sin(seed * 3.7f) * height * .14f;
        Color edge = outline ? C("090d16") : bark;
        Vector2 foot = new Vector2(x, y), tip = new Vector2(x + bend, y + height);
        Limb(ink, foot, new Vector2(x - bend * .5f, y + height * .54f), tip, width * .85f, bark, light, edge, outline);
        for (int j = 0; j < 3; j++)
        {
            float side = j % 2 == 0 ? -1 : 1;
            Vector2 root = foot + new Vector2(side * width * (1.7f + .35f * j), -.035f + .025f * j);
            Limb(ink, foot + Vector2.up * (.36f + .1f * j), foot + new Vector2(side * width * .5f, .02f), root, width * .27f, bark, light, edge, outline);
        }
        for (int j = 0; j < 4; j++)
        {
            float side = (j + seed) % 2 == 0 ? -1 : 1;
            float t = .42f + .12f * j;
            Vector2 a = Vector2.Lerp(foot, tip, t) + Vector2.left * bend * .22f;
            Vector2 end = a + new Vector2(side * height * (.23f + .045f * Mathf.Sin(seed + j)), height * (.23f + .04f * j));
            Limb(ink, a, a + new Vector2(side * height * .18f, .08f * height), end, width * (.25f - .025f * j), bark, light, edge, outline);
            if (j < 2 && outline)
                Limb(ink, Vector2.Lerp(a, end, .64f), end + new Vector2(-side * .18f, -.1f), end + new Vector2(side * .48f, .12f), width * .08f, bark, light, edge, true);
        }
        if (outline)
        {
            for (int j = 0; j < 4; j++)
            {
                float t = .13f + .16f * j;
                float xx = x + bend * t * t - bend * (1 - t) * t + width * .2f;
                float yy = y + height * t;
                ink.Stroke(Color.Lerp(edge, bark, .4f), .023f, new Vector2(xx + .07f, yy), new Vector2(xx, yy + .23f), new Vector2(xx + .025f, yy + .45f));
            }
        }
        if (seed % 7 == 0) return;
        for (int j = 0; j < 5; j++)
        {
            float ox = (j - 2) * height * .145f;
            float oy = height * (1 - .10f * Mathf.Abs(j - 2)) + .13f * Mathf.Sin(seed + j * 2.4f);
            Leaves(ink, x + bend + ox, y + oy, height * (.19f + .015f * Mathf.Sin(seed * 2 + j)), height * .145f, seed + j * 3,
                Color.Lerp(foliage, bark, j % 2 == 0 ? .035f : 0), light, edge, outline);
        }
    }

    void Ruin(Ink ink, float x, float y, float scale)
    {
        Color stone = C("303b50"), edge = C("101420"), face = C("74858e");
        for (int i = 0; i < 2; i++)
        {
            float xx = x + (i == 0 ? -1 : 1) * scale;
            ink.Poly(edge, new Vector2(xx - .38f * scale, y), new Vector2(xx - .32f * scale, y + 2.7f * scale), new Vector2(xx + .30f * scale, y + 2.8f * scale), new Vector2(xx + .4f * scale, y));
            ink.Poly(stone, new Vector2(xx - .25f * scale, y), new Vector2(xx - .23f * scale, y + 2.65f * scale), new Vector2(xx + .24f * scale, y + 2.7f * scale), new Vector2(xx + .27f * scale, y));
            ink.Stroke(face, .05f * scale, new Vector2(xx - .19f * scale, y + .1f), new Vector2(xx - .19f * scale, y + 2.6f * scale));
            for (int j = 1; j < 6; j++) ink.Stroke(edge, .035f * scale, new Vector2(xx - .25f * scale, y + j * .43f * scale), new Vector2(xx + .25f * scale, y + j * .43f * scale));
        }
        var arc = new Vector2[13];
        for (int i = 0; i < arc.Length; i++) { float a = i * Mathf.PI / 12; arc[i] = new Vector2(x + Mathf.Cos(a) * scale, y + (2.65f + Mathf.Sin(a) * 1.05f) * scale); }
        ink.Stroke(edge, .48f * scale, arc); ink.Stroke(stone, .34f * scale, arc);
        ink.Stroke(C("315943"), .07f, new Vector2(x - scale, y + 2.8f * scale), new Vector2(x - 1.13f * scale, y + 1.6f * scale), new Vector2(x - .9f * scale, y + .4f * scale));
    }

    void Draw()
    {
        material = new Material(Shader.Find("Sprites/Default"));
        var sky = new Ink();
        sky.Band(C("130c2d"), x => 20, x => -15); sky.Gradient(C("130c2d"), C("493070"), 5, -3);
        sky.Oval(C("655087"), 10, 3.2f, .85f, .85f, 40); sky.Oval(C("e9e5bd"), 10, 3.2f, .60f, .60f, 40); sky.Oval(C("26183e"), 10.24f, 3.39f, .56f, .56f, 40);
        var stars = new System.Random(322);
        for (int i = 0; i < 160; i++)
        {
            float x = -30 + (float)stars.NextDouble() * 205;
            float y = 1.2f + (float)stars.NextDouble() * 5;
            float radius = i % 11 == 0 ? .022f : .009f;
            sky.Oval(i % 11 == 0 ? C("e6dbf1") : C("807699"), x, y, radius, radius, 6);
        }
        Finish("Violet dusk", sky, -100, .90f);
        var far = new Ink();
        far.Band(C("27294b"), x => .5f + Mathf.Sin(x * .12f) * .9f + Mathf.Sin(x * .31f) * .25f, x => -10);
        for (int i = 0; i < 65; i++)
        {
            float x = -30 + i * 2.85f, h = 2.1f + Mathf.Sin(i * 2.7f) * .9f;
            Tree(far, x, -.7f, h, .22f, i, C("17253b"), C("30445b"), C("16263a"), false);
        }
        Finish("Distant sleeping woods", far, -90, .52f);
        var castle = new Ink();
        float summit = 1.15f;
        castle.Poly(C("292b49"), new Vector2(33, -8), new Vector2(38, -.8f), new Vector2(42, .1f), new Vector2(44.6f, summit), new Vector2(47.4f, summit + .08f), new Vector2(49, .2f), new Vector2(52, -.6f), new Vector2(57, -8));
        castle.Poly(C("343950"), new Vector2(38, -.8f), new Vector2(42, .1f), new Vector2(44.6f, summit), new Vector2(45.5f, summit), new Vector2(43.5f, -.3f), new Vector2(42.8f, -1.5f), new Vector2(39, -3));
        castle.Poly(C("20243c"), new Vector2(45.4f, summit), new Vector2(47.4f, summit + .08f), new Vector2(49, .2f), new Vector2(52, -.6f), new Vector2(48, -3), new Vector2(46.1f, -.3f));
        castle.Poly(C("24283e"), new Vector2(44.6f, summit), new Vector2(44.6f, summit + .56f), new Vector2(47.5f, summit + .56f), new Vector2(47.5f, summit));
        for (int i = 0; i < 5; i++)
        {
            float x = 44.65f + i * .55f, h = .65f + (i == 2 ? .70f : (i % 2) * .25f);
            castle.Poly(C("24283e"), new Vector2(x, summit), new Vector2(x, summit + h), new Vector2(x + .36f, summit + h), new Vector2(x + .36f, summit));
            castle.Poly(C("171c32"), new Vector2(x - .08f, summit + h), new Vector2(x + .15f, summit + h + .35f), new Vector2(x + .44f, summit + h));
            castle.Stroke(C("52556a"), .028f, new Vector2(x + .05f, summit + .1f), new Vector2(x + .05f, summit + h - .04f));
            castle.Stroke(C("98835c"), .028f, new Vector2(x + .18f, summit + h - .28f), new Vector2(x + .18f, summit + h - .15f));
        }
        Finish("Mountain and distant keep", castle, -91, .62f);
        var middle = new Ink();
        middle.Band(C("102c2c"), x => -.8f + .5f * Mathf.Sin(x * .18f), x => -12);
        for (int i = 0; i < 59; i++)
        {
            float x = -27 + i * 3.5f + Mathf.Sin(i * 3.1f) * .85f, y = -1.0f + .3f * Mathf.Sin(x * .18f);
            Tree(middle, x, y, 4.4f + .9f * Mathf.Sin(i * 1.7f), .45f, i + 91, C("192e32"), C("355052"), C("12342e"), false);
        }
        Finish("Mist between the trunks", middle, -82, .20f);
        var bank = new Ink();
        bank.Band(C("0b171b"), x => World.Height(x) - .68f, x => -15);
        bank.Band(C("1d3d29"), x => World.Height(x) - .76f, x => -15);
        Finish("Continuous moss bank", bank, -70);
        var woods = new Ink();
        for (int i = 0; i < 32; i++)
        {
            float x = -20 + i * 6.1f + Mathf.Sin(i * 7.3f) * 1.5f;
            if (Mathf.Abs(x - 55) < 3 || Mathf.Abs(x - 121) < 3) continue;
            Tree(woods, x, World.Height(x) - .84f, 5.3f + Mathf.Sin(i * 1.8f) * 1.0f, .46f + .12f * Mathf.Sin(i), i + 23, C("292326"), C("605448"), C("0b251d"), true);
        }
        Ruin(woods, 55, World.Height(55) - .82f, .95f); Ruin(woods, 121, World.Height(121) - .82f, 1.15f);
        Finish("Rooted oaks and forgotten gates", woods, -65);
        var floor = new Ink();
        floor.Band(C("101f18"), x => World.Height(x) - 1.17f, x => -15);
        floor.Band(C("2d4326"), x => World.Height(x) - 1.23f, x => World.Height(x) - 4.5f);
        floor.Band(C("465b30"), x => World.Height(x) - 1.4f, x => World.Height(x) - 1.65f + .07f * Mathf.Sin(x));
        floor.Band(C("3a3028"), x => World.Height(x) - 2.05f + .12f * Mathf.Sin(x * .47f), x => World.Height(x) - 3.28f + .15f * Mathf.Sin(x * .35f));
        floor.Band(C("665237"), x => World.Height(x) - 2.18f + .1f * Mathf.Sin(x * .47f), x => World.Height(x) - 3.13f + .12f * Mathf.Sin(x * .35f));
        for (int i = 0; i < 32; i++)
        {
            float x = -20 + i * 6.1f + Mathf.Sin(i * 7.3f) * 1.5f;
            if (Mathf.Abs(x - 55) < 3 || Mathf.Abs(x - 121) < 3) continue;
            Color shadow = C("08141c"); shadow.a = .42f;
            floor.Poly(shadow,
                new Vector2(x - .6f, World.Height(x - .6f) - 1.23f),
                new Vector2(x + .5f, World.Height(x + .5f) - 1.23f),
                new Vector2(x + 2.2f, World.Height(x + 2.2f) - 4.5f),
                new Vector2(x + .4f, World.Height(x + .4f) - 4.5f));
        }
        var rng = new System.Random(7118);
        for (int i = 0; i < 650; i++)
        {
            float x = -25 + (float)rng.NextDouble() * 200, y = -1.5f - (float)rng.NextDouble() * 2.8f;
            float yy = y + World.Height(x);
            if (y > -2 || y < -3.3f)
                floor.Stroke(C("1b3020"), .025f, new Vector2(x - .07f, yy + .06f), new Vector2(x, yy), new Vector2(x + .06f, yy + .10f));
            else floor.Oval(i % 3 == 0 ? C("9b8356") : C("493c2c"), x, yy, .035f + (float)rng.NextDouble() * .09f, .022f, 7);
        }
        Finish("Winding earthen trail", floor, -60);
        var front = new Ink();
        front.Band(C("081711"), x => World.Height(x) - 4.48f + .15f * Mathf.Sin(x * .7f), x => -16);
        for (int i = 0; i < 180; i++)
        {
            float x = -25 + i * 1.15f, y = World.Height(x) - 4.6f;
            float h = .2f + .22f * Mathf.Abs(Mathf.Sin(i * 7.7f));
            front.Poly(C("081711"), new Vector2(x - .16f, y), new Vector2(x - .22f, y + h), new Vector2(x, y + .12f), new Vector2(x + .12f, y + h * .8f), new Vector2(x + .16f, y));
        }
        Finish("Near bracken", front, 0, 0, "Foreground");
    }

    void OnDestroy()
    {
        foreach (var mesh in meshes) if (mesh != null) Destroy(mesh);
        if (material != null) Destroy(material);
    }
}
