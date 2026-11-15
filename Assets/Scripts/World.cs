using System.Collections.Generic;
using UnityEngine;

/// Level geometry: lane bounds, obstacle circles and the procedural dressing of the forest.
public static class World
{
    public const float MinY = -3.85f, MaxY = -1.3f, Tile = 20.48f;
    public static float MinX = -2.5f, MaxX = 139.5f, lockMin = -999, lockMax = 999;
    struct Ob { public Vector2 c; public float r; }
    static readonly List<Ob> obs = new List<Ob>();
    public static Transform shrine; static System.Random rng;

    static float R(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }
    static bool Chance(float p) { return rng.NextDouble() < p; }

    public static void ClampLane(ref Vector2 p) { p.y = Mathf.Clamp(p.y, MinY, MaxY); p.x = Mathf.Clamp(p.x, MinX - 3, MaxX + 3); }

    public static void Constrain(Actor a)
    {
        bool player = a is Player;
        a.pos.y = Mathf.Clamp(a.pos.y, MinY, MaxY);
        if (player) a.pos.x = Mathf.Clamp(a.pos.x, Mathf.Max(MinX, lockMin), Mathf.Min(MaxX, lockMax));
        else a.pos.x = Mathf.Clamp(a.pos.x, MinX - 1, MaxX + 1);
        if (a.z > 0.9f) return;
        for (int i = 0; i < obs.Count; i++)
        {
            Vector2 d = a.pos - obs[i].c; d.y *= 2f;
            float min = obs[i].r + a.radius, m = d.magnitude;
            if (m < min && m > 0.0001f) { Vector2 push = d / m * (min - m); push.y *= 0.5f; a.pos += push; }
        }
    }

    public static bool OutOfView(float x, float margin)
    {
        return Mathf.Abs(x - CameraRig.I.transform.position.x) > CameraRig.I.HalfW + margin;
    }

    static SpriteRenderer Prop(Transform root, string sprite, float x, float y, float scale = 1f, bool flip = false, float sway = 0, bool sorted = true)
    {
        var sr = Art.New(sprite, root, new Vector3(x, y, 0), sorted ? Art.YOrder(y) : 0);
        sr.transform.localScale = new Vector3(flip ? -scale : scale, scale, 1);
        if (sway > 0) { var s = sr.gameObject.AddComponent<Sway>(); s.amp = sway; s.speed = R(1.1f, 2.2f); s.phase = R(0, 6.28f); }
        return sr;
    }

    static void Obstacle(float x, float y, float r) { obs.Add(new Ob { c = new Vector2(x, y), r = r }); }

    static Transform Layer(Transform root, string sprite, float factor, float y, int order, string layer, float from, float to, bool followY, float tileW = -1, bool altFlip = false)
    {
        var go = new GameObject("Layer_" + sprite); go.transform.SetParent(root, false);
        var p = go.AddComponent<Parallax>(); p.factor = factor; p.baseY = y; p.followY = followY;
        float t = tileW > 0 ? tileW : Tile;
        int i = 0;
        // altFlip mirrors every other copy so a one-off painted scene (not an authored seamless
        // tile) meets itself edge-to-edge instead of cutting hard where one copy ends and the next begins.
        for (float x = from; x < to; x += t) { var sr = Art.New(sprite, go.transform, new Vector3(x, 0, 0), order, layer); if (altFlip && (i & 1) == 1) sr.flipX = true; i++; }
        return go.transform;
    }

    public const float TileMid = 19.745f, TileCanopy = 12.63f, TileGround = 16.71f;
    public const float FloorTop = -0.92f;
    const float BackMin = -1.28f, BackMax = -1.00f;

    public static void Build(Transform root, Transform cam)
    {
        rng = new System.Random(20261114); obs.Clear(); lockMin = -999; lockMax = 999;
        float len = MaxX + 14;
        var sky = Art.New("bg_sky", cam, new Vector3(0, 0, 10), -100, "Background");
        sky.transform.localScale = new Vector3(600, 4.2f, 1);
        Layer(root, "bg_mid", 0.35f, 1.34f, -80, "Background", -TileMid, len * 0.68f + 2 * TileMid, true, TileMid);
        var ground = new GameObject("Ground").transform; ground.SetParent(root, false);
        for (float x = -2 * TileGround; x < len + 2 * TileGround; x += TileGround) Art.New("ground", ground, new Vector3(x, FloorTop, 0), -60, "Background");
        var fringe = new GameObject("Fringe").transform; fringe.SetParent(root, false);
        for (float x = -2 * TileGround; x < len + 2 * TileGround; x += TileGround) Art.New("bg_ground", fringe, new Vector3(x, -0.55f, 0), 0, "Foreground");
        Layer(root, "bg_canopy", 0.08f, 5.15f, -10, "Foreground", -TileCanopy, len * 0.95f + 2 * TileCanopy, true, TileCanopy);
        var vig = Art.New("fx_vignette", cam, new Vector3(0, 0, 9), 100, "Foreground");
        vig.transform.localScale = new Vector3(4.6f, 3.6f, 1);
        // --- light shafts ---
        var rays = new GameObject("Rays"); rays.transform.SetParent(root, false);
        var rp = rays.AddComponent<Parallax>(); rp.factor = 0.2f; rp.baseY = 0; rp.followY = false;
        for (float x = -4; x < len * 0.8f + 10; x += R(4.5f, 8f))
        {
            var r = Art.New("fx_ray", rays.transform, new Vector3(x, 5.2f, 0), -5, "Foreground", true);
            r.transform.localRotation = Quaternion.Euler(0, 0, R(-24, -15)); r.transform.localScale = new Vector3(R(1.2f, 2.6f), R(1.9f, 2.3f), 1);
            var pu = r.gameObject.AddComponent<Pulse>(); pu.baseColor = new Color(1, 0.95f, 0.75f, R(0.16f, 0.3f)); pu.speed = R(0.3f, 0.7f); pu.phase = R(0, 6.28f); pu.amount = 0.35f;
        }
        var back = new GameObject("BackRow").transform; back.SetParent(root, false);
        string[] trunks = { "pr_trunk_a", "pr_trunk_b", "pr_trunk_c", "pr_trunk_d" };
        float[] grove = { -6f, 16f, 41f, 69f, 95f, 127f, 145f };
        foreach (var gx in grove)
        {
            int n = 1;
            for (int i = 0; i < n; i++)
                Prop(back, trunks[rng.Next(trunks.Length)], gx + i * R(2.2f, 3.4f) + R(-1f, 1f), R(BackMin, BackMax), R(0.9f, 1.08f), Chance(0.5f));
        }
        float[] ruinPillars = { 50.5f, 61f, 122f, 140.5f };
        foreach (var x in ruinPillars) Prop(back, Chance(0.5f) ? "pr_pillar_tall" : "pr_pillar_mid", x, R(BackMin, BackMax), R(0.95f, 1.05f), Chance(0.5f));
        float[] walls = { 56f, 132f, -8.5f };
        foreach (var x in walls) Prop(back, "pr_wall", x, R(BackMin, BackMax), 1.05f, Chance(0.5f));
        string[] shrubs = { "pr_bush_a", "pr_bush_b", "pr_bush_c", "pr_fern_a", "pr_fern_b" };
        float[] thickets = { -7f, 2f, 13f, 27f, 35f, 52f, 68f, 73f, 91f, 99f, 112f, 126f, 139f };
        foreach (var tx in thickets)
        {
            int n = 2 + rng.Next(3);
            for (int i = 0; i < n; i++)
                Prop(back, shrubs[rng.Next(shrubs.Length)], tx + R(-2.2f, 2.2f), R(BackMin, BackMax), R(0.85f, 1.1f), Chance(0.5f), 1.4f);
        }
        for (int i = 0; i < 4; i++) { Prop(back, trunks[i % 3], MinX - 2.2f - i * 0.5f, R(BackMin, BackMax), 1.1f); Prop(back, trunks[(i + 1) % 3], MaxX + 2.6f + i * 0.5f, R(BackMin, BackMax), 1.1f, true); }
        // --- obstacles in the lane (with collision) ---
        var lane = new GameObject("Lane").transform; lane.SetParent(root, false);
        void Rock(float x, float y) { Prop(lane, "pr_rock_a", x, y, R(0.9f, 1.1f), Chance(0.5f)); Obstacle(x, y + 0.12f, 0.55f); }
        void Stump(float x, float y) { Prop(lane, "pr_stump", x, y); Obstacle(x, y + 0.1f, 0.48f); }
        void Log(float x, float y) { Prop(lane, "pr_log", x, y); Obstacle(x - 0.55f, y + 0.1f, 0.4f); Obstacle(x + 0.1f, y + 0.1f, 0.4f); Obstacle(x + 0.7f, y + 0.1f, 0.4f); }
        void Pillar(float x, float y) { Prop(lane, "pr_pillar_broken", x, y); Obstacle(x, y + 0.1f, 0.5f); }
        Rock(8.5f, -1.75f); Stump(25f, -3.5f); Log(30.5f, -1.65f); Rock(46.5f, -3.6f); Pillar(52.5f, -1.7f); Pillar(73.5f, -3.55f); Rock(76.5f, -1.7f);
        Stump(96.5f, -1.75f); Log(100.5f, -3.6f); Rock(115.5f, -3.55f); Pillar(120.5f, -1.65f); Pillar(124.5f, -3.6f);
        // --- shrine at the end ---
        var sh = Prop(lane, "pr_shrine", 136.5f, -1.55f, 1.0f); shrine = sh.transform; Obstacle(136.5f, -1.45f, 0.7f);
        for (float cx = -3; cx < MaxX + 3; cx += R(3.2f, 7.5f))
        {
            if (Chance(0.22f)) continue;
            int n = 2 + rng.Next(4);
            bool backBand = Chance(0.55f);
            for (int i = 0; i < n; i++)
            {
                float x = cx + R(-2.4f, 2.4f);
                float y = backBand ? R(-2.45f, -1.5f) : R(-3.85f, -3.25f);
                double k = rng.NextDouble();
                if (k < 0.44) Prop(lane, Chance(0.5f) ? "pr_grass_a" : (Chance(0.5f) ? "pr_grass_b" : "pr_grass_c"), x, y, R(0.8f, 1.15f), Chance(0.5f), 5f);
                else if (k < 0.62) Prop(lane, Chance(0.5f) ? "pr_rock_c" : "pr_rock_d", x, y, R(0.8f, 1.05f), Chance(0.5f), 6f);
                else if (k < 0.74) Prop(lane, Chance(0.5f) ? "pr_fern_a" : "pr_fern_b", x, y, R(0.7f, 0.95f), Chance(0.5f), 3f);
                else if (k < 0.82)
                {
                    var m = Prop(lane, "pr_shrooms", x, y, R(0.8f, 1.1f), Chance(0.5f));
                    var g = Art.New("fx_glow", m.transform, new Vector3(0, 0.25f, 0), m.sortingOrder + 1, "Default", true);
                    g.transform.localScale = Vector3.one * 1.3f; var pu = g.gameObject.AddComponent<Pulse>(); pu.baseColor = new Color(0.4f, 0.95f, 1f, 0.38f); pu.speed = R(1f, 2f); pu.phase = R(0, 6); pu.amount = 0.4f;
                }
                else if (k < 0.89) Prop(lane, "pr_rock_b", x, y, R(0.7f, 1f), Chance(0.5f));
                else if (k < 0.93) Prop(lane, "pr_chest", x, y, R(0.9f, 1.05f), Chance(0.5f));
                else Prop(lane, "pr_bones", x, y, R(0.8f, 1f), Chance(0.5f));
            }
        }
        root.gameObject.AddComponent<Ambient>();
    }
}

[DefaultExecutionOrder(200)]
public class Parallax : MonoBehaviour
{
    public float factor, baseY; public bool followY;
    void LateUpdate()
    {
        var c = CameraRig.I.transform.position;
        transform.position = new Vector3(c.x * factor, baseY + (followY ? c.y : 0), 0);
    }
}

public class Sway : MonoBehaviour
{
    public float amp = 4, speed = 1.5f, phase;
    void Update()
    {
        float t = Time.time * speed + phase;
        transform.localRotation = Quaternion.Euler(0, 0, (Mathf.Sin(t) + Mathf.Sin(t * 2.3f) * 0.35f) * amp);
    }
}

public class Pulse : MonoBehaviour
{
    public Color baseColor = Color.white; public float speed = 1, phase, amount = 0.3f; SpriteRenderer sr;
    void Awake() { sr = GetComponent<SpriteRenderer>(); }
    void Update() { var c = baseColor; c.a *= 1 - amount + amount * Mathf.Sin(Time.time * speed + phase); sr.color = c; }
}

/// Fireflies, dust motes and falling leaves drifting around the camera.
public class Ambient : MonoBehaviour
{
    float fly, leaf;
    void Update()
    {
        float dt = Time.deltaTime; if (dt <= 0) return;
        var c = CameraRig.I.transform.position; float hw = CameraRig.I.HalfW + 1;
        fly -= dt; leaf -= dt;
        if (fly <= 0)
        {
            fly = Random.Range(0.12f, 0.3f);
            Vector2 p = new Vector2(c.x + Random.Range(-hw, hw), Random.Range(-4f, 2.5f));
            var col = Random.value < 0.6f ? new Color(1f, 0.95f, 0.55f, 0.9f) : new Color(0.7f, 1f, 0.7f, 0.8f);
            Fx.Emit("fx_soft", p, new Vector2(Random.Range(-0.4f, 0.4f), Random.Range(-0.1f, 0.5f)), Random.Range(2f, 4f), Random.Range(0.1f, 0.22f), 0.05f, new Color(col.r, col.g, col.b, 0.0f) + new Color(0, 0, 0, col.a), new Color(col.r, col.g, col.b, 0), 0, 0, 0, true, 3500);
        }
        if (leaf <= 0)
        {
            leaf = Random.Range(0.5f, 1.2f);
            Vector2 p = new Vector2(c.x + Random.Range(-hw, hw + 2), 4.8f);
            Fx.Emit("fx_leaf" + Random.Range(1, 3), p, new Vector2(Random.Range(-1.6f, -0.3f), Random.Range(-1.4f, -0.8f)), Random.Range(5f, 7f), Random.Range(0.7f, 1.1f), 0.8f, Color.white, new Color(1, 1, 1, 0.6f), Random.Range(0, 360f), Random.Range(-140f, 140f), 0.05f, false, 3400);
        }
    }
}

[DefaultExecutionOrder(100)]
public class CameraRig : MonoBehaviour
{
    public static CameraRig I; public Camera cam;
    float x, velX, look, lockCenter, shakeAmp, shakeT, shakeDur; bool locked;
    public float HalfW => cam.orthographicSize * cam.aspect;

    void Awake() { I = this; cam = GetComponent<Camera>(); }

    public void Shake(float amp, float dur) { if (amp >= shakeAmp * (shakeDur > 0 ? shakeT / shakeDur : 0)) { shakeAmp = amp; shakeT = dur; shakeDur = dur; } }
    public void Lock(float center) { locked = true; lockCenter = center; World.lockMin = center - HalfW + 0.5f; World.lockMax = center + HalfW - 0.5f; }
    public void Unlock() { locked = false; World.lockMin = -999; World.lockMax = 999; }
    public void SnapTo(float px) { x = Clamp(px); velX = 0; Apply(Vector2.zero); }
    float Clamp(float v) { return Mathf.Clamp(v, World.MinX + HalfW - 1.2f, World.MaxX - HalfW + 1.2f); }
    void Apply(Vector2 o) { transform.position = new Vector3(x + o.x, o.y, -10); }

    void LateUpdate()
    {
        var p = Game.I.player; float dt = Time.deltaTime;
        if (p != null && dt > 0)
        {
            look = Mathf.Lerp(look, p.facing * 1.4f, 1 - Mathf.Exp(-2.5f * dt));
            float target = locked ? lockCenter : Clamp(p.pos.x + look);
            x = Mathf.SmoothDamp(x, target, ref velX, locked ? 0.45f : 0.22f, 40f, dt);
        }
        Vector2 o = Vector2.zero;
        if (shakeT > 0)
        {
            shakeT -= Time.unscaledDeltaTime; float k = Mathf.Clamp01(shakeT / shakeDur);
            o = Random.insideUnitCircle * shakeAmp * k;
        }
        Apply(o);
    }
}
