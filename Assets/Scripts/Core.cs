using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// Builds every sprite the game uses (storybook art, the potion, the shield and the coin) and hands out sprites/materials.
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
        // The old packed atlas and painted backgrounds were fully replaced by the
        // storybook set, so they are no longer loaded (that alone was ~100 MB of RAM).
        StorybookArt.Load(sprites);
        CharacterArt.Load(sprites);
        var coin = Resources.Load<Texture2D>("Art/ui_coin");
        if (coin != null)
        {
            var s = Sprite.Create(coin, new Rect(0, 0, coin.width, coin.height), new Vector2(.5f, .5f), 300, 0, SpriteMeshType.FullRect);
            s.name = "ui_coin"; sprites["ui_coin"] = s;
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

    /// Like S, but quiet when the sprite is missing (optional art).
    public static Sprite TryS(string name) { Load(); sprites.TryGetValue(name, out var s); return s; }

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
        AudioListener.volume = MenuSettings.Volume("Master");
        if (music != null) music.volume = 0.32f * MenuSettings.Volume("Music");
        if (amb != null) amb.volume = 0.5f * MenuSettings.Volume("Effects");
        if (drums == null || music == null) return;
        drums.volume = Mathf.MoveTowards(drums.volume, drumsTarget * MenuSettings.Volume("Music"), dt * 0.5f);
        if (music.isPlaying && drums.isPlaying && Mathf.Abs(music.timeSamples - drums.timeSamples) > 2000) drums.timeSamples = music.timeSamples;
    }

    public static void MusicVolume(float v) { if (music != null) music.volume = v; }

    public static void Play(string name, float vol = 1f, float pitchVar = 0.08f, float pitch = 1f)
    {
        if (clips == null || !clips.TryGetValue(name, out var c)) return;
        var a = pool[next]; next = (next + 1) % pool.Length;
        a.pitch = pitch + Random.Range(-pitchVar, pitchVar);
        a.volume = vol * MenuSettings.Volume("Effects");
        a.clip = c; a.Play();
    }
}

/// Keyboard / mouse / gamepad input, with a simulation hook used for automated play-testing.
public static class GameInput
{
    public static bool simActive; public static Vector2 simMove;
    static HashSet<string> pending = new HashSet<string>(), current = new HashSet<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { simActive = false; simMove = Vector2.zero; pending.Clear(); current.Clear(); interactClaimed = -10; }

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
                if (MenuSettings.Held(0)) v.x -= 1;
                if (MenuSettings.Held(1)) v.x += 1;
                if (MenuSettings.Held(2)) v.y += 1;
                if (MenuSettings.Held(3)) v.y -= 1;
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

    static bool Primary => MenuSettings.Pressed(4) || M(m => m.leftButton.wasPressedThisFrame) || G(g => g.buttonWest.wasPressedThisFrame);
    static bool HeavyIn => MenuSettings.Pressed(5) || M(m => m.rightButton.wasPressedThisFrame) || G(g => g.buttonNorth.wasPressedThisFrame);
    // Interact drinks too -- unless something nearby (a pickup, the campfire) wants
    // that key this frame or last. Their Updates and the player's aren't ordered.
    public static int interactClaimed = -10;
    public static void ClaimInteract() { interactClaimed = Time.frameCount; }
    static bool InteractIn => MenuSettings.Pressed(12) && Time.frameCount - interactClaimed > 1;
    static bool SwordOut => QuickItems.Selected == 0;
    // The selected item decides what the buttons do, wheel open or not. Sword out:
    // light and heavy attacks. Potion out: no attacks at all -- light, heavy or
    // interact all drink it.
    public static bool Attack => !QuickItems.BlockActions && (current.Contains("attack") || (SwordOut && Primary));
    public static bool UsePotion => current.Contains("potion") || (!QuickItems.BlockActions && !SwordOut && (Primary || HeavyIn || InteractIn));
    public static bool Heavy => current.Contains("heavy") || (!QuickItems.BlockActions && SwordOut && HeavyIn);
    public static bool Magic => current.Contains("magic") || MenuSettings.Pressed(6) || G(g => g.buttonEast.wasPressedThisFrame);
    public static bool Jump => current.Contains("jump") || MenuSettings.Pressed(7) || G(g => g.buttonSouth.wasPressedThisFrame);
    public static bool Dodge => current.Contains("dodge") || MenuSettings.Pressed(8) || G(g => g.rightShoulder.wasPressedThisFrame);
    public static bool Guard => current.Contains("guard") || MenuSettings.Held(13) || G(g => g.leftShoulder.isPressed);
    public static bool Restart => current.Contains("restart") || MenuSettings.Pressed(10) || G(g => g.startButton.wasPressedThisFrame);
    public static bool LightCampfire => current.Contains("lightCampfire") || K(k => k.eKey.wasPressedThisFrame) || G(g => g.buttonNorth.wasPressedThisFrame);
    public static bool Confirm => current.Contains("confirm") || K(k => k.enterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame || k.jKey.wasPressedThisFrame) || M(m => m.leftButton.wasPressedThisFrame) || G(g => g.buttonSouth.wasPressedThisFrame || g.startButton.wasPressedThisFrame);
    public static bool Help => current.Contains("help") || MenuSettings.Pressed(9);
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
        if (parent == null) { float elevation = World.Height(pos.x); pos.y += elevation; if (floorY > -900) floorY += elevation; }
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

    /// Impact: a white-hot pinpoint, one thin flash streak across the blow, and fine
    /// sparks thrown the way the hit travelled. No cartoon star -- it should read
    /// as metal and bone meeting, gone in a tenth of a second.
    public static void HitSpark(Vector2 p, float size, Color tint, int dir = 0)
    {
        Color hot = Color.Lerp(Color.white, tint, 0.25f);
        // Tight core flash and a soft bloom behind it.
        Emit("fx_glow", p, Vector2.zero, 0.07f, size * 0.9f, size * 1.4f, hot, A(tint, 0), 0, 0, 0, true, 3002);
        Emit("fx_glow", p, Vector2.zero, 0.14f, size * 1.6f, size * 2.4f, A(tint, 0.35f), A(tint, 0), 0, 0, 0, true, 3000);
        // A single razor streak crossing the blow at a slight angle.
        float baseAng = dir == 0 ? Random.Range(0f, 180f) : (dir > 0 ? 0f : 180f);
        float slashAng = baseAng + Random.Range(55f, 80f) * (Random.value < 0.5f ? 1 : -1);
        Emit("fx_soft", p, Vector2.zero, 0.09f, size * 0.55f, size * 0.75f, hot, A(tint, 0), slashAng, 0, 0, true, 3003, -999, 0, null, 7f, 0.22f);
        // Fine sparks: thin streaks aligned to their flight, flung in a cone along the hit.
        int n = 5 + (int)(size * 4);
        for (int i = 0; i < n; i++)
        {
            float ang = (dir == 0 ? Random.Range(0f, 360f) : baseAng + Random.Range(-55f, 55f)) * Mathf.Deg2Rad;
            Vector2 v = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * Random.Range(6f, 12f) * size;
            float life = Random.Range(0.1f, 0.2f);
            Emit("fx_soft", p, v, life, 0.16f * size, 0.04f * size, hot, A(tint, 0), Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg, 0, 9, true, 3001, -999, 6, null, 3.2f, 0.45f);
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
