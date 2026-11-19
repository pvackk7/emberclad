using UnityEngine;
using UnityEngine.UI;

/// The knight's health: a strip of live coals in a weathered iron channel, with a flame
/// burning at the leading edge that travels left as the coals are consumed.
///
/// No health is stored here. Hud pushes the real value in every frame through SetHealth();
/// this class only decides how the journey to that value looks. Damage, healing and death
/// are inferred from the value itself, so no gameplay code has to announce them -- the one
/// thing it cannot infer is *why* health left, which is what NoteEmberSpend() is for.
///
/// Everything is vertex colour on the default UI material: one draw call, no textures, no
/// custom shader, no per-frame allocation. That keeps it cheap in a browser build and makes
/// it resolution- and CanvasScaler-independent, because all geometry is derived from the
/// local rect.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class EmberHealthBar : MaskableGraphic
{
    // The coal channel, in the local rect's own units. The rect pivots top-left, so the
    // rect spans x 0..320, y -52..0 and these line up with the old bar's exact position.
    public const float ChX = 12f, ChTop = -24f, ChW = 302f, ChH = 15f;
    const float ChBot = ChTop - ChH;
    const int Cells = 48;         // coal cells across the full bar
    const int MaxFlecks = 30;     // hard ceiling on sparks + ash + smoke, all pooled

    static readonly Color PlateBase = new Color(.125f, .106f, .098f, 1f);
    static readonly Color PlateLit = new Color(.255f, .223f, .196f, 1f);
    static readonly Color PlateDark = new Color(.052f, .045f, .045f, 1f);
    static readonly Color Bead = new Color(.345f, .263f, .176f, 1f);
    static readonly Color Recess = new Color(.035f, .029f, .031f, 1f);
    static readonly Color CoalCold = new Color(.255f, .052f, .012f, 1f);
    static readonly Color CoalMid = new Color(.76f, .250f, .032f, 1f);
    static readonly Color CoalHot = new Color(1f, .590f, .110f, 1f);
    static readonly Color Seam = new Color(1f, .800f, .345f, 1f);
    static readonly Color Ash = new Color(.250f, .222f, .212f, 1f);
    static readonly Color AshLit = new Color(.50f, .375f, .305f, 1f);

    /// Set by Hud while the bar is fully covered by the menu: the state still ticks, the
    /// mesh does not get rebuilt.
    public bool hidden;

    float value = 1f;          // the real health fraction, pushed in by Hud
    float burn = 1f;           // the displayed edge, chasing `value`
    float hold;                // beat before the burn front starts travelling after a hit
    float ashEdge, ashAge = 99f;
    bool ashIsEmber;           // the chunk that just left was spent, not taken
    float shove, shoveVel;     // flame flinch, a spring
    float healSurge, igniteFlash, gulp, relight;
    float emberWindow;         // NoteEmberSpend() was called this recently
    float deathT = -1f;        // >= 0 once the flame has started going out
    float clock, stepT, glowSmooth = 1f;
    float lastValue = 1f;      // previous frame's real value, for edge-detecting damage and healing
    int frame;

    struct Fleck { public float x, y, vx, vy, age, life, size; public byte kind; }
    readonly Fleck[] flecks = new Fleck[MaxFlecks];
    int fleckCursor;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    // ---- integration surface -------------------------------------------------------

    /// The single source of truth, pushed in every frame. Everything else is derived.
    public void SetHealth(float normalized)
    {
        value = Mathf.Clamp01(normalized);
    }

    /// Tell the bar that health about to leave was spent, not lost, so the next drop
    /// burns to ash instead of flinching. Harmless if no drop follows.
    public void NoteEmberSpend()
    {
        emberWindow = .3f;
    }

    /// Where the flame is burning, in world space, so the HUD can fly things at it.
    public Vector3 HeadWorldPoint()
    {
        return rectTransform.TransformPoint(new Vector3(ChX + burn * ChW, ChBot + 5f, 0f));
    }

    /// A siphoned ember just landed on the bar: brighten the coals and kick a spark
    /// or two off the head. The health it carried arrives separately, through hp.
    public void SiphonHit()
    {
        igniteFlash = Mathf.Max(igniteFlash, .5f);
        healSurge = Mathf.Max(healSurge, .5f);
        float x = ChX + burn * ChW, y = ChBot + 5f;
        for (int i = 0; i < 2; i++)
        {
            int s = frame * 17 + i * 211 + Mathf.FloorToInt(clock * 61f);
            Emit(0, x + Hash(s) * 4f, y, Hash(s * 3) * 8f, 20f + Rand01(s) * 14f, .45f + Rand01(s * 5) * .25f, .9f + Rand01(s * 7) * .8f);
        }
    }

    /// Levelling up refills the bar outright, so the whole strip takes light again.
    public void Restore()
    {
        healSurge = 1f; relight = 1f; igniteFlash = .85f;
        float y = ChBot + 5f;
        for (int i = 0; i < 12; i++)
        {
            int s = i * 313 + frame;
            Emit(0, ChX + Rand01(s * 7) * ChW, y, Hash(s * 3) * 9f, 20f + Rand01(s) * 22f, .6f + Rand01(s * 5) * .45f, 1f + Rand01(s * 11) * 1.1f);
        }
    }

    // ---- simulation ----------------------------------------------------------------

    void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
        clock += dt;
        emberWindow = Mathf.Max(0f, emberWindow - dt);

        // Stepped timing. The flame silhouette is rebuilt on these beats and held in
        // between, the way a drawn fire cycles through frames -- continuous wobble is
        // what makes procedural fire read as a sine wave.
        float crit = Critical;
        float step = Mathf.Lerp(1f / 13f, 1f / 8f, crit);
        if (healSurge > 0f) step *= .62f;
        stepT += dt;
        int guard = 0;
        while (stepT >= step && guard++ < 4) { stepT -= step; frame++; }

        float before = burn;
        if (value < burn - .0005f)
        {
            // Burning backward. Hold for a beat so the hit registers, then travel at a
            // rate scaled to the size of the bite, so a big hit does not crawl.
            if (hold > 0f) hold -= dt;
            else burn = Mathf.MoveTowards(burn, value, dt * Mathf.Max(.55f, (burn - value) * 4.5f));
        }
        else if (value > burn + .0005f)
        {
            burn = Mathf.MoveTowards(burn, value, dt * Mathf.Max(1.6f, (value - burn) * 5f));
        }
        else burn = value;

        DetectEvents(before, dt);

        // Springs and decays.
        shoveVel += (-shove * 150f - shoveVel * 13f) * dt;
        shove += shoveVel * dt;
        healSurge = Mathf.Max(0f, healSurge - dt * 1.9f);
        igniteFlash = Mathf.Max(0f, igniteFlash - dt * 7f);
        gulp = Mathf.Max(0f, gulp - dt * 3.4f);
        relight = Mathf.Max(0f, relight - dt * 2.2f);
        ashAge += dt;

        if (deathT >= 0f) deathT += dt;

        TickFlecks(dt);

        float wantGlow = Alive ? Mathf.Lerp(.42f, 1f, Mathf.Clamp01(burn * 2.4f)) * (1f - crit * .55f) : 0f;
        glowSmooth = Mathf.Lerp(glowSmooth, wantGlow + healSurge * .8f, 1f - Mathf.Exp(-9f * dt));

        if (Alive) Breathe(dt, crit);

        if (!hidden) SetVerticesDirty();
    }

    bool Alive { get { return value > 0f || deathT < 0f; } }
    float Critical { get { return value <= 0f ? 1f : Mathf.InverseLerp(.22f, .06f, value); } }

    void DetectEvents(float before, float dt)
    {
        float delta = burn - before;

        if (delta < -.0008f)
        {
            // Coals going out. Sparks fly from the burn front; how they fly is what tells
            // a hit apart from an ember being spent.
            float fx = ChX + burn * ChW, fy = ChBot + 5f;
            int n = ashIsEmber ? 2 : 3;
            float rate = -delta * 190f;
            if (Rand01(frame * 71 + Mathf.FloorToInt(clock * 97f)) < Mathf.Clamp01(rate))
                for (int i = 0; i < n; i++)
                {
                    int s = frame * 13 + i * 97;
                    if (ashIsEmber) Emit(1, fx + Hash(s) * 3f, fy, Hash(s * 3) * 7f, 9f + Rand01(s) * 8f, .5f + Rand01(s * 5) * .3f, 1.1f + Rand01(s * 7) * .8f);
                    else Emit(0, fx + Hash(s) * 3f, fy, 7f + Hash(s * 3) * 13f, 16f + Rand01(s) * 16f, .42f + Rand01(s * 5) * .3f, 1f + Rand01(s * 7) * 1.1f);
                }
        }

        if (value <= 0f && deathT < 0f)
        {
            // Death: let the burn front finish travelling, then let the flame go out and
            // leave a wisp of smoke behind it.
            if (burn <= .004f)
            {
                deathT = 0f;
                float x = ChX + 2f, y = ChBot + 4f;
                for (int i = 0; i < 7; i++)
                    Emit(2, x + Hash(i * 31) * 5f, y + i * 1.3f, Hash(i * 17) * 4f, 11f + Rand01(i * 53) * 9f, 1.25f + Rand01(i * 7) * .7f, 3.2f + Rand01(i * 29) * 2.6f);
                for (int i = 0; i < 5; i++)
                    Emit(1, x + Hash(i * 13) * 4f, y, Hash(i * 41) * 9f, 7f + Rand01(i * 23) * 7f, .55f, 1.2f);
            }
        }
        else if (value > 0f && deathT >= 0f) { deathT = -1f; relight = 1f; healSurge = .7f; }
    }

    /// Hud pushes the value during Update, so by LateUpdate a real step in health has
    /// definitely landed. Edge-detect it here rather than inferring it from the gap,
    /// which would re-trigger every frame while the burn front is still travelling.
    void LateUpdate()
    {
        if (value < lastValue - .0005f)
        {
            if (ashAge > .08f)
            {
                ashEdge = burn;
                ashAge = 0f;
                ashIsEmber = emberWindow > 0f;
            }
            else ashEdge = Mathf.Max(ashEdge, burn);

            hold = ashIsEmber ? .05f : .10f;
            if (ashIsEmber) { igniteFlash = 1f; gulp = 1f; }
            else { shoveVel += 26f; igniteFlash = .45f; }
            emberWindow = 0f;
        }
        else if (value > lastValue + .0005f && value > 0f)
        {
            healSurge = 1f;
            float x = ChX + burn * ChW, y = ChBot + 5f;
            for (int i = 0; i < 4; i++)
            {
                int s = frame * 29 + i * 61;
                Emit(0, x + Hash(s) * 6f, y, Hash(s * 3) * 6f, 22f + Rand01(s) * 14f, .5f + Rand01(s * 5) * .25f, 1.1f + Rand01(s * 7) * .9f);
            }
        }
        lastValue = value;
    }

    void Breathe(float dt, float crit)
    {
        // A slow idle drift of embers off a healthy bar, thinning out as it gets desperate.
        float chance = (.55f + healSurge * 2.4f) * (1f - crit * .8f) * Mathf.Clamp01(burn * 3f);
        if (Rand01(Mathf.FloorToInt(clock * 61f) * 7 + frame) < chance * dt * 6f)
        {
            int s = frame * 101 + Mathf.FloorToInt(clock * 53f);
            float x = ChX + burn * ChW + Hash(s) * 4f;
            Emit(0, x, ChBot + 6f, Hash(s * 3) * 5f, 13f + Rand01(s) * 11f, .55f + Rand01(s * 5) * .4f, .8f + Rand01(s * 11) * .8f);
        }
    }

    void Emit(byte kind, float x, float y, float vx, float vy, float life, float size)
    {
        fleckCursor = (fleckCursor + 1) % MaxFlecks;
        ref Fleck f = ref flecks[fleckCursor];
        f.kind = kind; f.x = x; f.y = y; f.vx = vx; f.vy = vy;
        f.age = 0f; f.life = life; f.size = size;
    }

    void TickFlecks(float dt)
    {
        for (int i = 0; i < MaxFlecks; i++)
        {
            ref Fleck f = ref flecks[i];
            if (f.age >= f.life) continue;
            f.age += dt;
            f.x += f.vx * dt;
            f.y += f.vy * dt;
            if (f.kind == 0) { f.vy -= f.vy * 1.1f * dt; f.vx -= f.vx * 2.2f * dt; f.vx += Noise(f.y * .18f + clock * 2.1f, i + 3) * 26f * dt; }
            else if (f.kind == 1) { f.vy -= 9f * dt; f.vx -= f.vx * 1.4f * dt; }
            else { f.vy -= f.vy * .55f * dt; f.vx += Noise(f.y * .1f + clock * .9f, i + 11) * 9f * dt; }
        }
    }

    // ---- noise ---------------------------------------------------------------------

    /// Unsigned on purpose: signed overflow is undefined in the C++ IL2CPP emits, and
    /// this is the one place in the bar that relies on wrapping arithmetic.
    static float Hash(int n)
    {
        unchecked
        {
            uint x = (uint)n;
            x ^= x << 13;
            x *= 0x9E3779B1u;
            x ^= x >> 15;
            x *= 0x85EBCA6Bu;
            x ^= x >> 13;
            return (x & 0x7FFFFFFFu) / 1073741823.5f - 1f;
        }
    }
    static float Rand01(int n) { return Hash(n) * .5f + .5f; }
    static float Noise(float x, int seed)
    {
        int i = Mathf.FloorToInt(x);
        float f = x - i; f = f * f * (3f - 2f * f);
        return Mathf.Lerp(Hash(i + seed * 7919), Hash(i + 1 + seed * 7919), f);
    }

    // ---- drawing -------------------------------------------------------------------

    void Quad(VertexHelper vh, float x0, float y0, float x1, float y1, Color c) { Quad(vh, x0, y0, x1, y1, c, c); }

    void Quad(VertexHelper vh, float x0, float y0, float x1, float y1, Color left, Color right)
    {
        if (x1 <= x0) return;
        int n = vh.currentVertCount;
        vh.AddVert(new Vector3(x0, y0), left, Vector2.zero);
        vh.AddVert(new Vector3(x0, y1), left, Vector2.zero);
        vh.AddVert(new Vector3(x1, y1), right, Vector2.zero);
        vh.AddVert(new Vector3(x1, y0), right, Vector2.zero);
        vh.AddTriangle(n, n + 1, n + 2);
        vh.AddTriangle(n, n + 2, n + 3);
    }

    void Diamond(VertexHelper vh, float cx, float cy, float rx, float ry, Color c)
    {
        int n = vh.currentVertCount;
        vh.AddVert(new Vector3(cx - rx, cy), c, Vector2.zero);
        vh.AddVert(new Vector3(cx, cy + ry), c, Vector2.zero);
        vh.AddVert(new Vector3(cx + rx, cy), c, Vector2.zero);
        vh.AddVert(new Vector3(cx, cy - ry), c, Vector2.zero);
        vh.AddTriangle(n, n + 1, n + 2);
        vh.AddTriangle(n, n + 2, n + 3);
    }

    void Glow(VertexHelper vh, float cx, float cy, float rx, float ry, Color inner, int seg)
    {
        if (inner.a <= .002f || rx <= 0f || ry <= 0f) return;
        int n = vh.currentVertCount;
        Color outer = new Color(inner.r, inner.g, inner.b, 0f);
        vh.AddVert(new Vector3(cx, cy), inner, Vector2.zero);
        for (int i = 0; i <= seg; i++)
        {
            float a = i * Mathf.PI * 2f / seg;
            vh.AddVert(new Vector3(cx + Mathf.Cos(a) * rx, cy + Mathf.Sin(a) * ry), outer, Vector2.zero);
            if (i > 0) vh.AddTriangle(n, n + i, n + i + 1);
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        float crit = Critical;
        bool dead = deathT >= 0f;
        float outT = dead ? Mathf.Clamp01(deathT / .38f) : 0f;

        Frame(vh);
        Coals(vh, crit, dead);
        AshTrail(vh);
        if (!dead || outT < 1f) Fire(vh, crit, outT);
        Flecks(vh);
    }

    /// Dark weathered iron: a plate, a lit top bevel, a sunken channel with a bronze bead,
    /// and a riveted cap at each end. Hand-cut nicks along the top keep it from reading
    /// as a rounded rectangle.
    void Frame(VertexHelper vh)
    {
        const float x0 = ChX - 6f, x1 = ChX + ChW + 6f, yTop = ChTop + 6f, yBot = ChBot - 6f;
        Quad(vh, x0, yTop, x1, yBot, PlateBase);
        Quad(vh, x0, yTop, x1, yTop - 2f, PlateLit);
        Quad(vh, x0, yBot + 2f, x1, yBot, PlateDark);

        // Weathering: a few asymmetric bites out of the lit edge, fixed so the metal
        // looks beaten rather than animated.
        for (int i = 0; i < 7; i++)
        {
            float u = Rand01(i * 977 + 11);
            float nx = x0 + 10f + u * (ChW - 14f);
            float w = 2.5f + Rand01(i * 331) * 7f;
            Quad(vh, nx, yTop - Rand01(i * 71) * .8f, nx + w, yTop - 2f, PlateBase);
        }

        Quad(vh, ChX - 1.5f, ChTop + 1.5f, ChX + ChW + 1.5f, ChBot - 1.5f, Bead);
        Quad(vh, ChX - .5f, ChTop + .5f, ChX + ChW + .5f, ChBot - .5f, Recess);

        float capMid = (ChTop + ChBot) * .5f;
        for (int s = 0; s < 2; s++)
        {
            float cx = s == 0 ? x0 + 3f : x1 - 3f;
            Quad(vh, cx - 3f, yTop - 1f, cx + 3f, yBot + 1f, PlateLit);
            Quad(vh, cx - 2f, yTop - 2f, cx + 2f, yBot + 2f, PlateBase);
            Diamond(vh, cx, capMid, 2.2f, 3.2f, Bead);
            Diamond(vh, cx, capMid + .5f, 1.1f, 1.6f, PlateLit);
        }
    }

    /// The coal bed, drawn as one jagged quad strip. The gradient is anchored to the
    /// fill rather than to the bar, so the hottest coals always sit under the flame; the
    /// notched top and bottom edges are anchored to the bar, so the hand-cut silhouette
    /// stays put instead of sliding around as health changes.
    void Coals(VertexHelper vh, float crit, bool dead)
    {
        if (burn <= .0008f) return;
        float dim = (1f - crit * .52f) * (dead ? Mathf.Clamp01(1f - deathT / .9f) : 1f);
        float lift = 1f + healSurge * .45f + igniteFlash * .35f;

        int last = Mathf.Clamp(Mathf.CeilToInt(burn * Cells), 1, Cells);
        int first = vh.currentVertCount;
        int cols = 0;
        for (int i = 0; i <= last; i++)
        {
            float u = Mathf.Min((float)i / Cells, burn);
            float px = ChX + u * ChW;
            // Low-frequency noise groups neighbouring columns into lumps of uneven
            // width; a fixed stride would give the strip a picket-fence rhythm.
            float lumpN = Noise(i * .52f, 4242);
            float yTop = ChTop - .5f - Rand01(i * 613 + 5) * 1.5f - (lumpN * .5f + .5f) * 2.0f;
            float yBot = ChBot + .5f + Rand01(i * 829 + 3) * 1.4f + (Noise(i * .47f, 1717) * .5f + .5f) * 1.8f;

            float g = Mathf.Clamp01(u / Mathf.Max(burn, .0001f));
            Color baseCol = g < .55f ? Color.Lerp(CoalCold, CoalMid, g / .55f)
                                     : Color.Lerp(CoalMid, CoalHot, (g - .55f) / .45f);
            float flick = 1f + .19f * Noise(frame * .5f + i * 1.15f, i + 1);
            // Reach of the flame's heat, and each lump's own stubbornness: without the
            // second one the strip grades evenly and stops reading as separate coals.
            float near = Mathf.Exp(-(burn - u) * 30f);
            float lump = .60f + .50f * (lumpN * .5f + .5f);
            baseCol = Color.Lerp(baseCol, Seam, near * .58f);
            float k = dim * flick * lift * lump;

            Color edge = new Color(Mathf.Min(1f, baseCol.r * k * .44f), Mathf.Min(1f, baseCol.g * k * .34f), Mathf.Min(1f, baseCol.b * k * .34f), 1f);
            Color mid = Color.Lerp(baseCol, Seam, .18f + near * .46f);
            mid = new Color(Mathf.Min(1f, mid.r * k), Mathf.Min(1f, mid.g * k), Mathf.Min(1f, mid.b * k), 1f);

            float yU = Mathf.Lerp(yTop, yBot, .30f), yL = Mathf.Lerp(yTop, yBot, .68f);
            vh.AddVert(new Vector3(px, yTop), edge, Vector2.zero);
            vh.AddVert(new Vector3(px, yU), mid, Vector2.zero);
            vh.AddVert(new Vector3(px, yL), mid, Vector2.zero);
            vh.AddVert(new Vector3(px, yBot), edge, Vector2.zero);
            cols++;
            if (cols >= 2)
            {
                int a = first + (cols - 2) * 4, b = first + (cols - 1) * 4;
                for (int r = 0; r < 3; r++)
                {
                    vh.AddTriangle(a + r, a + r + 1, b + r + 1);
                    vh.AddTriangle(a + r, b + r + 1, b + r);
                }
            }
            if (u >= burn) break;
        }

        // Dark seams every few cells, so the strip reads as separate coals rather than
        // one continuous ribbon of light.
        for (int i = 2; i < Cells; i++)
        {
            if (Rand01(i * 733 + 17) > .44f) continue;   // irregular spacing, not every nth
            float u = (float)i / Cells;
            if (u >= burn) break;
            float px = ChX + u * ChW;
            float w = .8f + Rand01(i * 251) * 1.5f;
            float t = ChTop - .7f - Rand01(i * 149) * 2.3f;
            float b = ChBot + .7f + Rand01(i * 193) * 2.1f;
            Quad(vh, px - w, t, px, b, new Color(.05f, .016f, .007f, .60f + Rand01(i * 97) * .32f));
        }
    }

    /// What is left where coals used to be: a bright line of ignition, then grey ash that
    /// crumbles away. Ember spending burns whiter and leaves more ash than a hit does.
    void AshTrail(VertexHelper vh)
    {
        float span = ashEdge - burn;
        if (span <= .0015f || ashAge > .95f) return;
        float fade = 1f - ashAge / .95f;
        fade *= fade;
        int from = Mathf.FloorToInt(burn * Cells);
        for (int i = from; i < Cells; i++)
        {
            float u0 = (float)i / Cells, u1 = (float)(i + 1) / Cells;
            if (u1 <= burn) continue;
            if (u0 >= ashEdge) break;
            float a = Mathf.Max(u0, burn), b = Mathf.Min(u1, ashEdge);
            float xa = ChX + a * ChW, xb = ChX + b * ChW;
            float near = Mathf.Exp(-(a - burn) * 22f);
            Color c = Color.Lerp(Ash, AshLit, Rand01(i * 457) * .7f);
            c = Color.Lerp(c, ashIsEmber ? Seam : CoalMid, near * (.35f + igniteFlash * .6f));
            c.a = fade * (ashIsEmber ? 1f : .92f) * (.58f + Rand01(i * 311) * .42f);
            float shrink = (1f - fade) * 2.6f;
            Quad(vh, xa, ChTop - 1.5f - shrink, xb - .7f, ChBot + 1.5f + shrink, c);
        }
    }

    /// The flame at the leading edge. The silhouette is rebuilt only on the stepped beat
    /// and held in between, which is what makes it read as drawn rather than interpolated.
    void Fire(VertexHelper vh, float crit, float outT)
    {
        float x = ChX + burn * ChW;
        float baseY = ChBot + 3f;
        float scale = Mathf.Lerp(1f, .40f, crit) * (1f - outT) * (1f + healSurge * .55f + relight * .3f);
        if (burn <= .0008f && outT <= 0f) scale *= .5f;

        // Critical fire stalls: some beats it almost gives up altogether.
        if (crit > .15f)
        {
            float dip = Noise(frame * .9f, 991) * .5f + .5f;
            if (dip < .34f * crit + .1f) scale *= Mathf.Lerp(1f, .34f, crit);
        }
        float bright = Mathf.Lerp(1f, .40f, crit) * (1f + healSurge * .6f + igniteFlash * .5f) * (1f - outT * .55f);
        float h = 25f * scale * (1f + gulp * .25f);
        float w = 4.6f * scale * (1f + gulp * .5f - healSurge * .12f);
        float lean = shove * .055f;

        // Warm illumination only, at very low alpha so the panel underneath never looks
        // blurred. Rising heat bands used to sit above this and read as floating dashes
        // rather than shimmer, so there are none.
        float gl = glowSmooth * (1f - outT);
        Glow(vh, x, baseY + h * .32f, 23f * scale + 4f, 16f * scale + 4f, new Color(1f, .47f, .11f, .062f * gl), 14);
        Glow(vh, x, baseY + h * .2f, 12f * scale + 2.5f, 9.5f * scale + 2.5f, new Color(1f, .68f, .25f, .125f * gl), 12);
        Quad(vh, ChX, ChTop + 4.5f, x, ChTop + 1.5f, new Color(1f, .55f, .16f, 0f), new Color(1f, .62f, .2f, .13f * gl));

        // Three stacked tongues plus a couple of licks that come and go on the beat.
        Tongue(vh, x, baseY, w * 1.35f, h * 1.02f, lean, 1, new Color(.93f, .22f, .03f), new Color(.72f, .10f, .02f), .80f * bright);
        Tongue(vh, x, baseY, w * 1.0f, h * .86f, lean * .9f, 2, new Color(1f, .47f, .06f), new Color(.95f, .24f, .03f), .92f * bright);
        Tongue(vh, x, baseY, w * .62f, h * .60f, lean * .8f, 3, new Color(1f, .80f, .18f), new Color(1f, .49f, .07f), .95f * bright);
        Tongue(vh, x, baseY + 1f, w * .30f, h * .33f, lean * .6f, 4, new Color(1f, .96f, .72f), new Color(1f, .82f, .33f), .95f * bright);

        for (int i = 0; i < 2; i++)
        {
            float r = Rand01(frame * 37 + i * 193);
            if (r < .45f) continue;
            float off = Hash(frame * 53 + i * 71) * w * 2.2f;
            Tongue(vh, x + off, baseY + h * .18f, w * .34f * r, h * .5f * r, lean * 1.4f, 10 + i,
                   new Color(1f, .62f, .10f), new Color(.95f, .28f, .03f), .7f * bright);
        }

        // The moment it goes out: one last hot pinch of light.
        if (outT > 0f && outT < 1f)
        {
            float pop = Mathf.Sin(Mathf.Clamp01(outT / .3f) * Mathf.PI);
            Glow(vh, x, baseY + 3f, 9f * pop, 9f * pop, new Color(1f, .93f, .66f, .5f * pop), 10);
        }
    }

    void Tongue(VertexHelper vh, float cx, float baseY, float halfW, float h, float lean, int seed, Color cBase, Color cTip, float alpha)
    {
        if (h <= .25f || halfW <= .05f || alpha <= .004f) return;
        const int Seg = 8;
        int first = vh.currentVertCount;
        for (int i = 0; i <= Seg; i++)
        {
            float t = (float)i / Seg;
            float shape = Mathf.Pow(1f - t, .85f) * (.72f + .52f * Mathf.Sin(t * Mathf.PI));
            float sway = Noise(t * 2.4f + frame * .79f, seed) * halfW * 1.3f * t * t;
            float px = cx + lean * t * t * h + sway;
            float py = baseY + t * h;
            float wl = halfW * shape * (.78f + .44f * (Noise(t * 3.1f + frame * .71f, seed * 3 + 1) * .5f + .5f));
            float wr = halfW * shape * (.78f + .44f * (Noise(t * 2.9f + frame * .67f, seed * 5 + 2) * .5f + .5f));
            Color c = Color.Lerp(cBase, cTip, t);
            c.a = alpha * (1f - t * .3f);
            vh.AddVert(new Vector3(px - wl, py), c, Vector2.zero);
            vh.AddVert(new Vector3(px + wr, py), c, Vector2.zero);
            if (i == 0) continue;
            int n = first + i * 2;
            vh.AddTriangle(n - 2, n, n - 1);
            vh.AddTriangle(n - 1, n, n + 1);
        }
    }

    void Flecks(VertexHelper vh)
    {
        for (int i = 0; i < MaxFlecks; i++)
        {
            Fleck f = flecks[i];
            if (f.age >= f.life) continue;
            float k = f.age / f.life;
            if (f.kind == 2)
            {
                float s = f.size * (1f + k * 2.6f);
                Glow(vh, f.x, f.y, s, s * 1.15f, new Color(.42f, .40f, .40f, .30f * (1f - k) * Mathf.Min(1f, k * 5f)), 8);
            }
            else if (f.kind == 1)
            {
                float s = f.size * (1f - k * .5f);
                Color c = Color.Lerp(AshLit, Ash, k);
                c.a = (1f - k) * .85f;
                Diamond(vh, f.x, f.y, s * .8f, s, c);
            }
            else
            {
                float s = f.size * (1f - k * .62f);
                Color c = k < .45f ? Color.Lerp(new Color(1f, .95f, .70f), new Color(1f, .58f, .11f), k / .45f)
                                   : Color.Lerp(new Color(1f, .58f, .11f), new Color(.62f, .13f, .02f), (k - .45f) / .55f);
                c.a = 1f - k * k;
                Glow(vh, f.x, f.y, s * 2.6f, s * 2.6f, new Color(1f, .6f, .2f, c.a * .16f), 8);
                Diamond(vh, f.x, f.y, s * .75f, s * 1.5f, c);
            }
        }
    }
}
