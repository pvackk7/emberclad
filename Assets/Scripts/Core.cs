using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;

/// Loads the sprite atlas + map generated into Resources/Art and hands out sprites/materials.
public static class Art
{
    static Dictionary<string, Sprite> sprites;
    public static Material Mat, AddMat;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { sprites = null; Mat = null; AddMat = null; mats.Clear(); addMats.Clear(); }

    public static void Load()
    {
        if (sprites != null && Mat != null) return;
        sprites = new Dictionary<string, Sprite>();
        Mat = new Material(Resources.Load<Shader>("Shaders/SpriteFlash"));
        AddMat = new Material(Resources.Load<Shader>("Shaders/SpriteAdd"));
        var atlas = Resources.Load<Texture2D>("Art/atlas");
        var map = Resources.Load<TextAsset>("Art/atlas_map");
        var ci = CultureInfo.InvariantCulture;
        foreach (var raw in map.text.Split('\n'))
        {
            var p = raw.Trim().Split(' ');
            if (p.Length < 9) continue;
            int x = int.Parse(p[1]), y = int.Parse(p[2]), w = int.Parse(p[3]), h = int.Parse(p[4]);
            float px = float.Parse(p[5], ci), py = float.Parse(p[6], ci), ppu = float.Parse(p[7], ci), b = float.Parse(p[8], ci);
            Texture2D tex = x < 0 ? Resources.Load<Texture2D>("Art/" + p[0]) : atlas;
            if (tex == null) { Debug.LogError("Missing texture for " + p[0]); continue; }
            Rect r = x < 0 ? new Rect(0, 0, tex.width, tex.height) : new Rect(x, y, w, h);
            sprites[p[0]] = Sprite.Create(tex, r, new Vector2(px, py), ppu, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
            sprites[p[0]].name = p[0];
        }
    }

    static readonly Dictionary<Texture, Material> mats = new Dictionary<Texture, Material>(), addMats = new Dictionary<Texture, Material>();

    /// One material per texture: keeps sprite batching from ever pairing a sprite with the wrong texture.
    public static Material MatFor(Sprite s, bool add)
    {
        Load();
        if (s == null) return add ? AddMat : Mat;
        var d = add ? addMats : mats; var tex = s.texture;
        if (!d.TryGetValue(tex, out var m) || m == null) { m = new Material(add ? AddMat : Mat); m.mainTexture = tex; d[tex] = m; }
        return m;
    }

    public static Sprite S(string name)
    {
        Load();
        if (sprites.TryGetValue(name, out var s)) return s;
        Debug.LogError("Missing sprite " + name);
        return null;
    }

    public static SpriteRenderer New(string sprite, Transform parent, Vector3 localPos, int order = 0, string layer = "Default", bool add = false)
    {
        var go = new GameObject(sprite);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = S(sprite);
        sr.sharedMaterial = MatFor(sr.sprite, add);
        sr.sortingLayerName = layer;
        sr.sortingOrder = order;
        return sr;
    }

    public static int YOrder(float y) { return Mathf.RoundToInt(-y * 100f); }
}

/// One-shot sound effects + looping music/ambience.
public static class Sfx
{
    static Dictionary<string, AudioClip> clips;
    static AudioSource[] pool; static int next;
    static AudioSource music, drums, amb;
    public static float drumsTarget;

    public static void Init(GameObject host)
    {
        clips = new Dictionary<string, AudioClip>();
        foreach (var c in Resources.LoadAll<AudioClip>("Sfx")) clips[c.name] = c;
        pool = new AudioSource[14];
        for (int i = 0; i < pool.Length; i++) { pool[i] = host.AddComponent<AudioSource>(); pool[i].playOnAwake = false; }
        music = Loop(host, "music", 0.32f); drums = Loop(host, "music_drums", 0f); amb = Loop(host, "ambience", 0.5f);
        drumsTarget = 0;
    }

    static AudioSource Loop(GameObject host, string clip, float vol)
    {
        var a = host.AddComponent<AudioSource>();
        a.loop = true; a.volume = vol; a.playOnAwake = false;
        if (clips.TryGetValue(clip, out var c)) { a.clip = c; a.Play(); }
        return a;
    }

    public static void Tick(float dt)
    {
        if (drums == null) return;
        drums.volume = Mathf.MoveTowards(drums.volume, drumsTarget, dt * 0.5f);
        if (music.isPlaying && drums.isPlaying && Mathf.Abs(music.timeSamples - drums.timeSamples) > 2000) drums.timeSamples = music.timeSamples;
    }

    public static void MusicVolume(float v) { if (music != null) music.volume = v; }

    public static void Play(string name, float vol = 1f, float pitchVar = 0.08f, float pitch = 1f)
    {
        if (clips == null || !clips.TryGetValue(name, out var c)) return;
        var a = pool[next]; next = (next + 1) % pool.Length;
        a.pitch = pitch + Random.Range(-pitchVar, pitchVar);
        a.volume = vol;
        a.clip = c; a.Play();
    }
}

/// Keyboard / mouse / gamepad input, with a simulation hook used for automated play-testing.
public static class GameInput
{
    public static bool simActive; public static Vector2 simMove;
    static HashSet<string> pending = new HashSet<string>(), current = new HashSet<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { simActive = false; simMove = Vector2.zero; pending.Clear(); current.Clear(); }

    public static void Sim(string action) { pending.Add(action); }
    public static void Tick() { var t = current; current = pending; pending = t; pending.Clear(); }

    public static Vector2 Move
    {
        get
        {
            if (simActive) return simMove;
            Vector2 v = Vector2.zero; var k = Keyboard.current;
            if (k != null)
            {
                if (k.aKey.isPressed || k.leftArrowKey.isPressed) v.x -= 1;
                if (k.dKey.isPressed || k.rightArrowKey.isPressed) v.x += 1;
                if (k.wKey.isPressed || k.upArrowKey.isPressed) v.y += 1;
                if (k.sKey.isPressed || k.downArrowKey.isPressed) v.y -= 1;
            }
            var g = Gamepad.current;
            if (g != null) { var s = g.leftStick.ReadValue(); if (s.sqrMagnitude > 0.1f) v = s; }
            if (v.sqrMagnitude > 1) v.Normalize();
            return v;
        }
    }

    static bool K(System.Func<Keyboard, bool> f) { var k = Keyboard.current; return k != null && f(k); }
    static bool G(System.Func<Gamepad, bool> f) { var g = Gamepad.current; return g != null && f(g); }
    static bool M(System.Func<Mouse, bool> f) { var m = Mouse.current; return m != null && f(m); }

    public static bool Attack => current.Contains("attack") || K(k => k.jKey.wasPressedThisFrame || k.zKey.wasPressedThisFrame) || M(m => m.leftButton.wasPressedThisFrame) || G(g => g.buttonWest.wasPressedThisFrame);
    public static bool Heavy => current.Contains("heavy") || K(k => k.kKey.wasPressedThisFrame || k.xKey.wasPressedThisFrame) || M(m => m.rightButton.wasPressedThisFrame) || G(g => g.buttonNorth.wasPressedThisFrame);
    public static bool Magic => current.Contains("magic") || K(k => k.lKey.wasPressedThisFrame || k.cKey.wasPressedThisFrame || k.eKey.wasPressedThisFrame) || G(g => g.buttonEast.wasPressedThisFrame);
    public static bool Jump => current.Contains("jump") || K(k => k.spaceKey.wasPressedThisFrame) || G(g => g.buttonSouth.wasPressedThisFrame);
    public static bool Dodge => current.Contains("dodge") || K(k => k.leftShiftKey.wasPressedThisFrame || k.rightShiftKey.wasPressedThisFrame) || G(g => g.rightShoulder.wasPressedThisFrame);
    public static bool Restart => current.Contains("restart") || K(k => k.rKey.wasPressedThisFrame) || G(g => g.startButton.wasPressedThisFrame);
    public static bool Confirm => current.Contains("confirm") || K(k => k.enterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame || k.jKey.wasPressedThisFrame) || M(m => m.leftButton.wasPressedThisFrame) || G(g => g.buttonSouth.wasPressedThisFrame || g.startButton.wasPressedThisFrame);
    public static bool Help => current.Contains("help") || K(k => k.tabKey.wasPressedThisFrame || k.hKey.wasPressedThisFrame || k.pKey.wasPressedThisFrame);
}

/// Pooled sprite particles: sparks, dust, bones, flames, leaves, glows.
public class Fx : MonoBehaviour
{
    public static Fx I;
    class P
    {
        public SpriteRenderer sr; public Transform t; public bool active;
        public Vector2 vel; public float grav, life, max, s0, s1, rotV, floorY, drag, sx, sy; public Color c0, c1;
    }
    readonly List<P> pool = new List<P>();

    void Awake() { I = this; }

    P Get()
    {
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i].sr == null) { pool.RemoveAt(i); i--; continue; }
            if (!pool[i].active) return pool[i];
        }
        var go = new GameObject("p"); go.transform.SetParent(transform, false);
        var p = new P { sr = go.AddComponent<SpriteRenderer>(), t = go.transform };
        p.sr.sortingLayerName = "Default";
        pool.Add(p); return p;
    }

    public static SpriteRenderer Emit(string sprite, Vector2 pos, Vector2 vel, float life, float s0, float s1, Color c0, Color c1,
        float rot = 0, float rotV = 0, float grav = 0, bool add = false, int order = 3000, float floorY = -999, float drag = 0,
        Transform parent = null, float sx = 1, float sy = 1)
    {
        if (I == null) return null;
        var p = I.Get();
        p.active = true; p.sr.enabled = true; p.sr.sprite = Art.S(sprite); p.sr.sharedMaterial = Art.MatFor(p.sr.sprite, add);
        p.sr.sortingOrder = order; p.sr.flipX = false;
        p.t.SetParent(parent != null ? parent : I.transform, false);
        p.t.localPosition = pos; p.t.localRotation = Quaternion.Euler(0, 0, rot);
        p.vel = vel; p.life = 0; p.max = life; p.s0 = s0; p.s1 = s1; p.c0 = c0; p.c1 = c1; p.rotV = rotV; p.grav = grav; p.floorY = floorY; p.drag = drag; p.sx = sx; p.sy = sy;
        p.t.localScale = new Vector3(s0 * sx, s0 * sy, 1); p.sr.color = c0;
        return p.sr;
    }

    void Update()
    {
        float dt = Time.deltaTime; if (dt <= 0) return;
        for (int i = 0; i < pool.Count; i++)
        {
            var p = pool[i];
            if (p.sr == null) { pool.RemoveAt(i); i--; continue; }
            if (!p.active) continue;
            p.life += dt;
            if (p.life >= p.max) { p.active = false; p.sr.enabled = false; if (p.t.parent != transform) p.t.SetParent(transform, false); continue; }
            float k = p.life / p.max;
            p.vel.y -= p.grav * dt;
            if (p.drag > 0) p.vel *= Mathf.Exp(-p.drag * dt);
            Vector3 lp = p.t.localPosition; lp += (Vector3)(p.vel * dt);
            if (lp.y < p.floorY) { lp.y = p.floorY; p.vel.y = -p.vel.y * 0.45f; p.vel.x *= 0.6f; p.rotV *= 0.5f; }
            p.t.localPosition = lp;
            if (p.rotV != 0) p.t.Rotate(0, 0, p.rotV * dt);
            float s = Mathf.Lerp(p.s0, p.s1, 1 - (1 - k) * (1 - k));
            p.t.localScale = new Vector3(s * p.sx, s * p.sy, 1);
            p.sr.color = Color.Lerp(p.c0, p.c1, k * k);
        }
    }

    static readonly Color Clear = new Color(1, 1, 1, 0);
    public static Color A(Color c, float a) { c.a = a; return c; }

    public static void HitSpark(Vector2 p, float size, Color tint)
    {
        Emit(Random.value < 0.5f ? "fx_spark" : "fx_spark2", p, Vector2.zero, 0.13f, size * 0.7f, size * 1.5f, tint, A(tint, 0), Random.Range(0, 360f));
        Emit("fx_glow", p, Vector2.zero, 0.16f, size * 2.2f, size * 3.2f, A(tint, 0.7f), A(tint, 0), 0, 0, 0, true);
        int n = 4 + (int)(size * 3);
        for (int i = 0; i < n; i++)
        {
            Vector2 d = Random.insideUnitCircle.normalized * Random.Range(5f, 11f) * size;
            Emit("fx_soft", p, d, Random.Range(0.14f, 0.26f), 0.28f * size, 0.02f, Color.Lerp(Color.white, tint, 0.5f), tint, 0, 0, 12, true, 3001, -999, 5);
        }
    }

    public static void Dust(Vector2 p, int n, float size = 1f, float spread = 1f)
    {
        var c = new Color(0.86f, 0.82f, 0.66f, 0.75f);
        for (int i = 0; i < n; i++)
        {
            Vector2 v = new Vector2(Random.Range(-1.6f, 1.6f) * spread, Random.Range(0.2f, 1.1f));
            Emit("fx_puff", p + new Vector2(Random.Range(-0.2f, 0.2f) * spread, 0.05f), v, Random.Range(0.3f, 0.5f), 0.25f * size, 0.6f * size, c, A(c, 0), Random.Range(0, 360f), Random.Range(-90, 90f), 0, false, Art.YOrder(p.y) + 2, -999, 3);
        }
    }

    public static void Ring(Vector2 p, float size, Color c, float life = 0.3f)
    {
        Emit("fx_ring", p, Vector2.zero, life, size * 0.3f, size, c, A(c, 0), 0, 0, 0, false, Art.YOrder(p.y) - 3, -999, 0, null, 1, 0.42f);
    }

    public static void Bones(Vector2 ground, float h, int n, float scale, int dir)
    {
        for (int i = 0; i < n; i++)
        {
            string s = "fx_bone" + Random.Range(1, 4);
            Vector2 v = new Vector2(Random.Range(-3f, 3f) + dir * Random.Range(1f, 4f), Random.Range(4f, 9f));
            Emit(s, ground + new Vector2(Random.Range(-0.2f, 0.2f), h * Random.Range(0.3f, 1f)), v, Random.Range(0.9f, 1.4f), scale, scale, Color.white, Clear, Random.Range(0, 360f), Random.Range(-720f, 720f), 26, false, Art.YOrder(ground.y) + 1, ground.y + Random.Range(-0.15f, 0.15f));
        }
    }

    public static void Flames(Vector2 p, int n, float size)
    {
        for (int i = 0; i < n; i++)
        {
            Vector2 v = new Vector2(Random.Range(-1.2f, 1.2f), Random.Range(1f, 3.4f)) * size;
            Emit("fx_flame" + Random.Range(1, 4), p + Random.insideUnitCircle * 0.25f * size, v, Random.Range(0.25f, 0.5f), 0.5f * size, 0.1f * size, Color.white, new Color(1, 0.4f, 0.2f, 0), Random.Range(-15, 15f), 0, -1, false, 3002, -999, 1.5f);
        }
        Emit("fx_glow", p, Vector2.zero, 0.3f, 2.4f * size, 3.6f * size, new Color(1, 0.6f, 0.2f, 0.6f), new Color(1, 0.4f, 0.1f, 0), 0, 0, 0, true);
    }

    public static void Leaves(Vector2 p, int n)
    {
        for (int i = 0; i < n; i++)
            Emit("fx_leaf" + Random.Range(1, 3), p + Random.insideUnitCircle * 0.3f, new Vector2(Random.Range(-2.5f, 2.5f), Random.Range(1.5f, 4.5f)), Random.Range(0.7f, 1.2f), 0.9f, 0.8f, Color.white, Clear, Random.Range(0, 360f), Random.Range(-400, 400f), 9, false, 3000, -999, 1.5f);
    }
}
