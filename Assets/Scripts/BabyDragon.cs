using System.Collections.Generic;
using UnityEngine;

/// A baby dragon, drawn rather than imported.
///
/// There is no dragon in the art set, so he is built out of flat vector shapes the way
/// the forest is: a handful of small meshes on their own transforms, then animated by
/// moving the transforms instead of rebuilding geometry. That is what lets him actually
/// fly -- wings beating on their own rhythm, body rising and falling a beat behind them,
/// tail trailing the motion. A sprite bobbing up and down just hovers; the wings are
/// what sell it.
public sealed class BabyDragon : MonoBehaviour
{
    // The project renders in linear colour space, so an sRGB literal handed straight to
    // a mesh comes out washed pale -- which is exactly how the first pass of this dragon
    // turned up pink. Everything here goes through Ink, the way the forest does it.
    static Color Ink(byte r, byte g, byte b)
    {
        Color c = new Color32(r, g, b, 255);
        return QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c;
    }
    static readonly Color Hide = Ink(92, 48, 104);
    static readonly Color HideDark = Ink(58, 28, 68);
    static readonly Color Belly = Ink(226, 180, 108);
    static readonly Color Membrane = Ink(150, 62, 86);
    static readonly Color MembraneDark = Ink(104, 42, 62);
    static readonly Color Horn = Ink(238, 224, 190);
    static readonly Color Eye = Ink(250, 208, 70);

    readonly List<Object> owned = new List<Object>();
    Transform body, head, snout, wingL, wingR, tail, mouthGlow;
    SpriteRenderer glow;
    float clock, flap, breath;
    public float Breathing;            // 0..1, driven by the Enemy while winding up a shot
    public int Facing = -1;

    T Keep<T>(T o) where T : Object { owned.Add(o); return o; }
    void OnDestroy() { foreach (var o in owned) if (o != null) Destroy(o); }

    public static BabyDragon Attach(Transform parent)
    {
        var go = new GameObject("Baby dragon");
        go.transform.SetParent(parent, false);
        var d = go.AddComponent<BabyDragon>();
        d.Build();
        return d;
    }

    // ---- drawing -------------------------------------------------------------------

    /// A closed polygon from a point list, as one flat-coloured mesh on its own transform.
    Transform Shape(string name, Transform parent, int order, Color fill, params Vector2[] pts)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var mesh = Keep(new Mesh { name = name });
        var verts = new Vector3[pts.Length];
        var cols = new Color[pts.Length];
        for (int i = 0; i < pts.Length; i++) { verts[i] = pts[i]; cols[i] = fill; }
        var tris = new int[(pts.Length - 2) * 3];
        for (int i = 0; i < pts.Length - 2; i++) { tris[i * 3] = 0; tris[i * 3 + 1] = i + 1; tris[i * 3 + 2] = i + 2; }
        mesh.vertices = verts; mesh.colors = cols; mesh.triangles = tris; mesh.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = Keep(new Material(Shader.Find("Sprites/Default")));
        mr.sortingLayerName = "Default";
        mr.sortingOrder = order;
        return go.transform;
    }

    /// Convex blob: an ellipse the shape list can be built from.
    static Vector2[] Oval(float cx, float cy, float rx, float ry, int steps = 18)
    {
        var p = new Vector2[steps];
        for (int i = 0; i < steps; i++)
        {
            float a = i * Mathf.PI * 2f / steps;
            p[i] = new Vector2(cx + Mathf.Cos(a) * rx, cy + Mathf.Sin(a) * ry);
        }
        return p;
    }

    /// A bat-style wing: out along the arm, then a scalloped trailing edge back to the
    /// shoulder. Drawn from the shoulder outwards so the fan triangulation stays valid.
    static Vector2[] Wing(float span, float rise)
    {
        return new[]
        {
            new Vector2(0f, 0f),
            new Vector2(.16f * span, .62f * rise),
            new Vector2(.52f * span, .96f * rise),
            new Vector2(.90f * span, .80f * rise),
            new Vector2(.72f * span, .50f * rise),      // scallop
            new Vector2(.86f * span, .34f * rise),
            new Vector2(.60f * span, .12f * rise),      // scallop
            new Vector2(.66f * span, -.06f * rise),
            new Vector2(.34f * span, -.10f * rise),
            new Vector2(.10f * span, -.14f * rise),
        };
    }

    void Build()
    {
        // Everything is drawn facing left, then mirrored by Facing.
        tail = Shape("Tail", transform, 0, HideDark,
            new Vector2(.06f, .08f), new Vector2(.44f, .20f), new Vector2(.78f, .12f),
            new Vector2(.96f, .34f), new Vector2(1.06f, .06f), new Vector2(.94f, -.10f),
            new Vector2(.74f, -.02f), new Vector2(.42f, .04f), new Vector2(.06f, -.10f));

        wingR = Shape("Wing far", transform, 1, MembraneDark, Wing(-.62f, .62f));
        wingR.localPosition = new Vector3(.10f, .14f, 0);

        body = Shape("Body", transform, 3, Hide, Oval(0f, 0f, .38f, .33f));
        Shape("Belly", body, 4, Belly, Oval(-.06f, -.11f, .24f, .18f));
        Shape("Haunch", body, 2, HideDark, Oval(.14f, -.14f, .16f, .15f));
        Shape("Leg", body, 2, HideDark,
            new Vector2(-.16f, -.26f), new Vector2(.00f, -.28f), new Vector2(.04f, -.46f),
            new Vector2(-.08f, -.50f), new Vector2(-.20f, -.42f));
        Shape("Toes", body, 3, Horn,
            new Vector2(-.20f, -.44f), new Vector2(-.02f, -.48f), new Vector2(-.04f, -.54f), new Vector2(-.22f, -.50f));

        head = Shape("Head", transform, 6, Hide, Oval(0f, 0f, .27f, .24f));
        head.localPosition = new Vector3(-.34f, .24f, 0);
        // A real muzzle rather than a beak: longer, with a jaw under it.
        snout = Shape("Snout", head, 7, Hide,
            new Vector2(-.10f, .08f), new Vector2(-.44f, .02f), new Vector2(-.52f, -.08f),
            new Vector2(-.46f, -.16f), new Vector2(-.12f, -.16f));
        Shape("Nostril", snout, 9, HideDark, Oval(-.42f, -.03f, .028f, .022f));
        Shape("Jaw", head, 5, HideDark,
            new Vector2(-.10f, -.12f), new Vector2(-.44f, -.14f), new Vector2(-.40f, -.22f), new Vector2(-.12f, -.24f));
        Shape("Horn", head, 5, Horn,
            new Vector2(.06f, .18f), new Vector2(.40f, .46f), new Vector2(.30f, .24f), new Vector2(.18f, .12f));
        Shape("Horn small", head, 5, Horn,
            new Vector2(-.06f, .20f), new Vector2(.12f, .42f), new Vector2(.16f, .16f));
        Shape("Frill", head, 4, MembraneDark,
            new Vector2(.14f, .02f), new Vector2(.40f, .06f), new Vector2(.34f, -.14f), new Vector2(.12f, -.12f));
        Shape("Eye", head, 8, Eye, Oval(-.13f, .05f, .07f, .085f));
        Shape("Pupil", head, 9, Ink(28, 16, 30), Oval(-.145f, .05f, .028f, .062f));
        Shape("Glint", head, 10, Color.white, Oval(-.165f, .085f, .024f, .03f));

        wingL = Shape("Wing near", transform, 8, Membrane, Wing(-.78f, .78f));
        wingL.localPosition = new Vector3(-.02f, .16f, 0);

        // The breath lives at the snout tip and is pure light.
        mouthGlow = new GameObject("Breath").transform;
        mouthGlow.SetParent(head, false);
        mouthGlow.localPosition = new Vector3(-.42f, -.07f, 0);
        glow = Art.New("fx_glow", mouthGlow, Vector3.zero, 10, "Default", true);
        glow.color = new Color(1f, .55f, .15f, 0f);
        glow.transform.localScale = Vector3.one * (.5f / Mathf.Max(.01f, glow.sprite.bounds.size.y));
    }

    // ---- animation -----------------------------------------------------------------

    public Vector3 MouthWorld { get { return mouthGlow != null ? mouthGlow.position : transform.position; } }

    public void Tick(float dt, bool fleeing)
    {
        clock += dt;
        // Wings beat faster than anything else moves, because that is what a small
        // winged thing looks like holding station in the air.
        flap += dt * (fleeing ? 26f : 13f);
        breath = Mathf.MoveTowards(breath, Breathing, dt * 4f);

        float beat = Mathf.Sin(flap);
        float open = beat * 64f;
        // The far wing trails the near one slightly, which is what reads as depth.
        if (wingL != null) wingL.localRotation = Quaternion.Euler(0, 0, -18f + open);
        if (wingR != null) wingR.localRotation = Quaternion.Euler(0, 0, -10f + Mathf.Sin(flap - .45f) * 52f);

        // The body answers the downstroke a fraction late, so it rides the beat rather
        // than moving in lockstep with it.
        float lift = Mathf.Sin(flap - .9f) * .055f;
        if (body != null) body.localPosition = new Vector3(0, lift, 0);
        if (head != null)
        {
            head.localPosition = new Vector3(-.30f, .22f + lift * 1.25f, 0);
            head.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(clock * 1.7f) * 4f - breath * 16f);
        }
        if (tail != null) tail.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(clock * 2.3f - 1.1f) * 13f + lift * 40f);
        if (snout != null) snout.localScale = new Vector3(1f + breath * .12f, 1f + breath * .1f, 1f);

        if (glow != null)
        {
            float g = breath * (.75f + Mathf.Sin(clock * 30f) * .25f);
            glow.color = new Color(1f, .5f + breath * .25f, .15f, g);
            glow.transform.localScale = Vector3.one * (.5f / Mathf.Max(.01f, glow.sprite.bounds.size.y)) * (.6f + breath * 1.1f);
        }

        transform.localScale = new Vector3(Facing >= 0 ? -1.15f : 1.15f, 1.15f, 1f);
    }

    public void SetAlpha(float a)
    {
        foreach (var mr in GetComponentsInChildren<MeshRenderer>())
        {
            var f = mr.GetComponent<MeshFilter>();
            if (f == null || f.sharedMesh == null) continue;
            var cols = f.sharedMesh.colors;
            for (int i = 0; i < cols.Length; i++) cols[i].a = a;
            f.sharedMesh.colors = cols;
        }
        if (glow != null) { var c = glow.color; c.a *= a; glow.color = c; }
    }
}
