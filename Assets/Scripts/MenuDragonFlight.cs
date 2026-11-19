using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// The princess's dragon over the main menu, flying one choreographed loop so a player
/// who only spends a few seconds here still sees it up close:
///
///   (the menu opens straight on 2, the big pass)
///   1  far   dives out under the bottom-left edge, swelling as it comes at the viewer
///   2  close swoops up past the camera (underside view) and out over the top
///   3  close comes straight back down the right side and out under the bottom
///   4  far   a slow full crossing, rising in from the right edge and passing behind the
///            title, out the left edge
///   5  far   back in from the left, a quick run right, a U-turn back, then straight into 1
///
/// 1-3 run back to back. Positions, speeds, sizes and pauses jitter a little each lap so
/// it never plays identically. Far legs are arc-length Catmull-Rom splines with the
/// 16-frame side-view flap (Resources/Menu/Dragon); rotation follows the heading, turns
/// bank and squash it, and reversing rolls through edge-on instead of snapping the mirror.
/// Close legs use the 8-frame underside flap (Resources/Menu/Dragon/Close) on their own
/// Image, sized so the wings span 72-88% of the screen at the closest point.
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public sealed class MenuDragonFlight : MonoBehaviour
{
    public Sprite[] wingFrames;
    public Sprite[] closeFrames;
    /// Hidden whenever this is (the menu backdrop is switched off for the in-game pause menu).
    public GameObject showWith;

    const float WingFps = 11f;               // full cycle ~1.45 s: big, slow wings
    const float Width = 156f;                // canvas units at scale 1 (~195 px at 1080p)
    const float OutsideMargin = 230f;
    const float MaxPitch = 38f;
    const int GlideFrame = 5;                // wings level

    const float CloseWingFps = 10f;
    static readonly Vector2 CloseSpan = new Vector2(.72f, .88f);     // peak wingspan, fraction of screen width
    const float CloseWidestFrame = 1f;       // the widest close frame spans its whole texture width

    enum Leg { OpeningDive, CloseUp, CloseDown, Crossing, LoopAndDive }

    readonly List<Vector2> controlPoints = new List<Vector2>();
    readonly List<Vector2> samples = new List<Vector2>();
    readonly List<float> distances = new List<float>();
    RectTransform rect, area, closeRect;
    Image image, closeImage;
    Leg next = Leg.CloseUp;           // opens on the big pass straight away
    bool flying, closeActive, closeDown;
    float traveled, routeLength, speed, accel, scale, grow = 1f, growPow = 2f, awayFor, phase, facing = 1f, rotation, lastAngle, turnRate, glideTimer, coastFor;
    float closeTime, closeLength, closeSize, closeX0, closeX1, closeHang, closeSway, closePhase, closeWingPhase, closeMirror;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        area = transform.parent as RectTransform;
        image = GetComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        image.enabled = false;
        awayFor = Random.Range(.5f, .9f);    // the menu has barely faded in when it shows up
        phase = Random.Range(0f, 16f);
    }

    // Sized in Start, not Awake: Awake runs inside AddComponent, before the frames are assigned.
    void Start()
    {
        float aspect = 1f;
        if (wingFrames != null && wingFrames.Length > 0 && wingFrames[0] != null)
            aspect = wingFrames[0].rect.height / wingFrames[0].rect.width;
        rect.sizeDelta = new Vector2(Width, Width * aspect);

        if (closeFrames == null || closeFrames.Length == 0) closeFrames = LoadCloseFrames();
        if (closeFrames.Length > 0 && area != null)
        {
            // Its own Image right above the distant one: over the backdrop, under the title and buttons.
            var go = new GameObject("MenuDragonOverhead", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(area, false);
            go.transform.SetSiblingIndex(transform.GetSiblingIndex() + 1);
            closeRect = (RectTransform)go.transform;
            closeRect.anchorMin = closeRect.anchorMax = closeRect.pivot = new Vector2(.5f, .5f);
            closeImage = go.GetComponent<Image>();
            closeImage.raycastTarget = false;
            closeImage.preserveAspect = true;
            closeImage.enabled = false;
        }
    }

    void OnDisable()
    {
        if (closeImage != null) closeImage.enabled = false;
    }

    void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 20f);
        if (showWith != null && !showWith.activeInHierarchy)
        {
            image.enabled = false;
            if (closeImage != null) closeImage.enabled = false;
            return;
        }

        if (closeActive) { UpdateClose(dt); return; }

        if (!flying)
        {
            image.enabled = false;
            awayFor -= dt;
            if (awayFor <= 0f) BeginLeg();
            return;
        }

        speed += accel * dt;
        traveled += speed * dt;
        if (traveled >= routeLength)
        {
            flying = false;
            image.enabled = false;
            return;
        }
        image.enabled = true;

        Vector2 here = AtDistance(traveled);
        Vector2 heading = AtDistance(Mathf.Min(routeLength, traveled + 10f)) - AtDistance(Mathf.Max(0f, traveled - 10f));
        if (heading.sqrMagnitude < .0001f) heading = new Vector2(facing, 0);
        heading.Normalize();

        // How hard it is turning (degrees per second, smoothed), for bank and wing effort.
        float angle = Mathf.Atan2(heading.y, heading.x) * Mathf.Rad2Deg;
        float rawTurn = Mathf.DeltaAngle(lastAngle, angle) / Mathf.Max(dt, .0001f);
        lastAngle = angle;
        turnRate = Mathf.Lerp(turnRate, Mathf.Clamp(rawTurn, -240f, 240f), 1 - Mathf.Exp(-4f * dt));

        Wings(dt, heading);

        // Facing rolls through edge-on over a quarter second instead of mirroring in one frame.
        float wantFacing = heading.x < -.12f ? -1f : heading.x > .12f ? 1f : facing >= 0 ? 1f : -1f;
        facing = Mathf.MoveTowards(facing, wantFacing, dt * 4.5f);
        float side = Mathf.Sign(facing == 0 ? wantFacing : facing);

        float pitch = Mathf.Atan2(heading.y, Mathf.Max(.2f, Mathf.Abs(heading.x))) * Mathf.Rad2Deg;
        pitch = Mathf.Clamp(pitch, -MaxPitch, MaxPitch);
        // Turning tilts the nose into the curve a little more, the way a bank would read from below.
        float bank = Mathf.Clamp(turnRate * .06f, -14f, 14f);
        float want = side * pitch + bank;
        rotation = Mathf.LerpAngle(rotation, want, 1 - Mathf.Exp(-5f * dt));
        rect.localRotation = Quaternion.Euler(0, 0, rotation);

        float squash = 1f - Mathf.Min(.14f, Mathf.Abs(turnRate) / 240f * .14f);
        // Dives swell toward the end as it comes at the viewer.
        float u = traveled / routeLength;
        float s = scale * Mathf.Lerp(1f, grow, Mathf.Pow(u, growPow));
        float bob = Mathf.Sin(phase / 16f * Mathf.PI * 2f) * 3.3f * s;
        rect.anchoredPosition = here + new Vector2(0, bob);
        rect.localScale = new Vector3(Mathf.Max(.18f, Mathf.Abs(facing)) * side * s, s * squash, 1f);
    }

    void Wings(float dt, Vector2 heading)
    {
        if (wingFrames == null || wingFrames.Length == 0) return;
        int n = wingFrames.Length;
        // Glide on dives, and now and then coast on the level for a second or so.
        bool diving = heading.y < -.38f;
        glideTimer -= dt;
        if (coastFor > 0f) coastFor -= dt;
        else if (glideTimer <= 0f)
        {
            glideTimer = Random.Range(3f, 7f);
            if (Mathf.Abs(heading.y) < .25f && Random.value < .35f) coastFor = Random.Range(.7f, 1.4f);
        }
        bool glide = diving || coastFor > 0f;

        if (glide)
        {
            // Finish the stroke into the level-wing pose, then hold it.
            float target = Mathf.Ceil((phase - GlideFrame) / n) * n + GlideFrame;
            phase = Mathf.MoveTowards(phase, target, dt * WingFps);
        }
        else
        {
            float effort = 1f + Mathf.Clamp01(heading.y) * .45f + Mathf.Min(1f, Mathf.Abs(turnRate) / 160f) * .2f;
            phase += dt * WingFps * effort;
        }
        if (phase > 1000f * n) phase -= 1000f * n;
        image.sprite = wingFrames[Mathf.FloorToInt(phase) % n];
    }

    // ---- The loop -------------------------------------------------------------------

    void BeginLeg()
    {
        if (area == null) { enabled = false; return; }
        float w = area.rect.width * .5f, h = area.rect.height * .5f;
        if (w < 2f || h < 2f) { awayFor = .3f; return; }

        // Since 2026-11-23 the small far dragon is gone (the user asked for it removed): only the
        // big close passes fly, up then down, over and over. The far legs below are kept unused.
        if (closeImage == null) { awayFor = 1f; return; }
        Leg leg = next == Leg.CloseDown ? Leg.CloseDown : Leg.CloseUp;
        switch (leg)
        {
            case Leg.OpeningDive:
                FarRoute(leg, w, h);
                next = Leg.CloseUp;
                awayFor = Random.Range(.35f, .6f);
                break;
            case Leg.CloseUp:
                BeginClose(false);
                next = Leg.CloseDown;
                awayFor = Random.Range(.3f, .55f);
                break;
            case Leg.CloseDown:
                BeginClose(true);
                next = Leg.CloseUp;
                awayFor = Random.Range(.6f, 1.1f);
                break;
            case Leg.Crossing:
                FarRoute(leg, w, h);
                next = Leg.LoopAndDive;
                awayFor = Random.Range(.6f, 1.2f);
                break;
            default:
                FarRoute(leg, w, h);
                next = Leg.CloseUp;
                awayFor = Random.Range(.35f, .6f);
                break;
        }
    }

    void FarRoute(Leg leg, float w, float h)
    {
        float R(float a, float b) => Random.Range(a, b);
        float edge = w + OutsideMargin;
        controlPoints.Clear();
        grow = 1f; growPow = 2f; accel = 0f;

        // Shared tail of legs 1 and 5: bend down near the left edge and dive out the bottom.
        void Dive(float jx)
        {
            controlPoints.Add(new Vector2(-w * (.86f + jx), -h * R(.36f, .4f)));
            controlPoints.Add(new Vector2(-w * (.87f + jx), -h * R(.56f, .6f)));
            controlPoints.Add(new Vector2(-w * (.82f + jx), -h * R(.76f, .8f)));
            controlPoints.Add(new Vector2(-w * (.77f + jx), -h));
            controlPoints.Add(new Vector2(-w * (.72f + jx), -h - OutsideMargin * 1.2f));
        }

        switch (leg)
        {
            case Leg.OpeningDive: // 1 on its own: in from the left edge, straight into the dive
            {
                float jx = R(-.03f, .03f);
                controlPoints.Add(new Vector2(-edge, -h * R(0f, .1f)));
                controlPoints.Add(new Vector2(-w * (.84f + jx), -h * R(.22f, .28f)));
                controlPoints.Add(new Vector2(-w * (.75f + jx), -h * R(.52f, .58f)));
                controlPoints.Add(new Vector2(-w * (.69f + jx), -h * R(.78f, .82f)));
                controlPoints.Add(new Vector2(-w * (.65f + jx), -h));
                controlPoints.Add(new Vector2(-w * (.62f + jx), -h - OutsideMargin * 1.2f));
                scale = R(.95f, 1.05f);
                speed = R(200f, 230f);
                accel = 70f;
                grow = R(1.8f, 2.1f);
                growPow = 2.2f;
                break;
            }
            case Leg.Crossing: // 4: rises in from the right edge and passes behind the title, out the left
            {
                float y = h * R(.7f, .78f);
                controlPoints.Add(new Vector2(edge, h * R(.3f, .42f)));
                controlPoints.Add(new Vector2(w * R(.72f, .8f), h * R(.56f, .64f)));
                controlPoints.Add(new Vector2(w * R(.52f, .58f), y + h * R(.02f, .07f)));
                controlPoints.Add(new Vector2(w * R(.15f, .3f), y + h * R(-.03f, .03f)));
                controlPoints.Add(new Vector2(-w * R(.2f, .35f), y + h * R(-.03f, .04f)));
                controlPoints.Add(new Vector2(-w * R(.6f, .7f), y + h * R(-.02f, .05f)));
                controlPoints.Add(new Vector2(-edge, y + h * R(0f, .06f)));
                scale = R(.9f, 1.05f);
                speed = R(115f, 140f);
                break;
            }
            default: // 5 then 1: quick run right, U-turn, back left, dive out the bottom
            {
                float y = h * R(.12f, .24f), turnX = -w * R(.36f, .46f), jx = R(-.03f, .03f);
                controlPoints.Add(new Vector2(-edge, y));
                controlPoints.Add(new Vector2(-w * .75f, y + h * R(-.03f, .02f)));
                controlPoints.Add(new Vector2(turnX - w * .12f, y - h * R(0f, .04f)));
                controlPoints.Add(new Vector2(turnX + w * R(.03f, .07f), y - h * R(.12f, .18f)));   // round the U-turn
                controlPoints.Add(new Vector2(turnX - w * R(.02f, .06f), -h * R(.2f, .28f)));
                controlPoints.Add(new Vector2(-w * R(.68f, .74f), -h * R(.26f, .32f)));
                Dive(jx);
                scale = R(.85f, .95f);
                speed = R(220f, 250f);
                accel = 25f;
                grow = R(1.8f, 2.1f);
                growPow = 5f;   // only the dive at the very end swells
                break;
            }
        }

        BuildArcLengthTable();
        traveled = 0f;
        Vector2 first = AtDistance(0f), ahead = AtDistance(12f);
        facing = ahead.x >= first.x ? 1f : -1f;
        lastAngle = Mathf.Atan2(ahead.y - first.y, ahead.x - first.x) * Mathf.Rad2Deg;
        turnRate = 0f;
        rotation = 0f;
        flying = routeLength > 1f;
        rect.anchoredPosition = first;
    }

    // ---- Close passes (2 and 3) -------------------------------------------------------

    /// The loading screen (after Play) is only ~1.6 s long, so it calls this to send the big
    /// dragon straight through: up if it is idle (or down if an up pass just finished), sped up
    /// so the whole pass fits before the level starts. A pass already in the air is left alone.
    public void PassNow()
    {
        if (closeImage == null || area == null || closeActive) return;
        flying = false; image.enabled = false;
        bool down = next == Leg.CloseDown;
        BeginClose(down);
        closeLength = 1.45f;
        next = down ? Leg.CloseUp : Leg.CloseDown;
        awayFor = .4f;
    }

    void BeginClose(bool down)
    {
        float w = area.rect.width;
        closeActive = true;
        closeDown = down;
        closeTime = 0f;
        closeLength = Random.Range(2.5f, 2.9f);
        // Sized so the widest wing pose spans the chosen fraction of the screen at the closest point.
        closeSize = w * Random.Range(CloseSpan.x, CloseSpan.y) / CloseWidestFrame;
        closeRect.sizeDelta = new Vector2(closeSize, closeSize);
        // 2 rises just left of centre; 3 drops down the right side.
        float from = down ? w * Random.Range(.17f, .23f) : -w * Random.Range(.02f, .07f);
        closeX0 = from;
        closeX1 = from + w * Random.Range(-.04f, .04f);
        closeHang = Random.Range(.3f, .5f);
        closeSway = Random.Range(.01f, .022f) * w;
        closePhase = Random.Range(0f, Mathf.PI * 2f);
        closeWingPhase = Random.Range(0f, closeFrames.Length);
        closeMirror = Random.value < .5f ? -1f : 1f;
        image.enabled = false;
        UpdateClose(0f);
        closeImage.enabled = true;
    }

    void UpdateClose(float dt)
    {
        float h = area.rect.height;
        closeTime += dt;
        float t = Mathf.Clamp01(closeTime / closeLength);
        // Fast in, a slower wingbeat as it passes closest, fast out.
        float travel = t + closeHang * Mathf.Sin(2f * Mathf.PI * t) / (2f * Mathf.PI);
        // Biggest just past the middle, as it passes nearest the viewer.
        float k = .8f + .2f * Mathf.Sin(Mathf.PI * Mathf.Pow(t, .85f));
        float size = closeSize * k;
        float halfTall = size * .5f;
        float yFrom = -h * .5f - halfTall, yTo = h * .5f + halfTall;
        float y = closeDown ? Mathf.Lerp(yTo, yFrom, travel) : Mathf.Lerp(yFrom, yTo, travel);
        float drift = Mathf.Lerp(closeX0, closeX1, Mathf.SmoothStep(0f, 1f, t));
        float sway = Mathf.Sin(t * Mathf.PI * 1.7f + closePhase) * closeSway;
        closeRect.anchoredPosition = new Vector2(drift + sway, y);

        // Lean into the drift; the outside wing foreshortens a little, like a bank.
        float dx = (closeX1 - closeX0) * 6f * t * (1f - t) / closeLength + Mathf.Cos(t * Mathf.PI * 1.7f + closePhase) * closeSway * Mathf.PI * 1.7f / closeLength;
        float dy = (h + size) * (1f + closeHang * Mathf.Cos(2f * Mathf.PI * t)) / closeLength;
        float lean = Mathf.Clamp(-Mathf.Atan2(dx, Mathf.Max(1f, dy)) * Mathf.Rad2Deg, -9f, 9f);
        // Coming back down, it flies head first: the sprite is turned around.
        closeRect.localRotation = Quaternion.Euler(0, 0, closeDown ? 180f - lean : lean);
        float bankSquash = 1f - Mathf.Abs(lean) / 9f * .06f;
        closeRect.localScale = new Vector3(closeMirror * bankSquash, 1f, 1f) * k;

        // Wings beat on their own clock, a little slower at the closest point.
        float pace = Mathf.Lerp(1f, .82f, Mathf.Sin(Mathf.PI * t));
        closeWingPhase += dt * CloseWingFps * pace;
        int n = closeFrames.Length;
        closeImage.sprite = closeFrames[((int)Mathf.Floor(closeWingPhase) % n + n) % n];

        if (t >= 1f)
        {
            closeActive = false;
            closeImage.enabled = false;
            closeRect.localScale = Vector3.one;
        }
    }

    // ---- Spline helpers ---------------------------------------------------------------

    void BuildArcLengthTable()
    {
        samples.Clear(); distances.Clear(); routeLength = 0f;
        const int perSpan = 24;
        for (int s = 0; s < controlPoints.Count - 1; s++)
            for (int k = 0; k < perSpan; k++) AddSample(Catmull(s, k / (float)perSpan));
        AddSample(controlPoints[controlPoints.Count - 1]);
    }

    void AddSample(Vector2 p)
    {
        if (samples.Count > 0) routeLength += Vector2.Distance(samples[samples.Count - 1], p);
        samples.Add(p); distances.Add(routeLength);
    }

    Vector2 Catmull(int i, float t)
    {
        Vector2 a = controlPoints[Mathf.Max(0, i - 1)], b = controlPoints[i], c = controlPoints[i + 1];
        Vector2 d = controlPoints[Mathf.Min(controlPoints.Count - 1, i + 2)];
        float t2 = t * t, t3 = t2 * t;
        return .5f * (2f * b + (c - a) * t + (2f * a - 5f * b + 4f * c - d) * t2 + (-a + 3f * b - 3f * c + d) * t3);
    }

    Vector2 AtDistance(float distance)
    {
        if (samples.Count == 0) return Vector2.zero;
        distance = Mathf.Clamp(distance, 0f, routeLength);
        int i = distances.BinarySearch(distance);
        if (i < 0) i = ~i;
        if (i <= 0) return samples[0];
        if (i >= samples.Count) return samples[samples.Count - 1];
        return Vector2.Lerp(samples[i - 1], samples[i], Mathf.InverseLerp(distances[i - 1], distances[i], distance));
    }

    /// Loads the cleaned flap frames from Resources/Menu/Dragon (dragon_flap_00..15).
    public static Sprite[] LoadFrames() => Load("Menu/Dragon/dragon_flap_");

    /// Loads the underside close-pass frames from Resources/Menu/Dragon/Close (dragon_close_00..07).
    public static Sprite[] LoadCloseFrames() => Load("Menu/Dragon/Close/dragon_close_");

    static Sprite[] Load(string prefix)
    {
        var list = new List<Sprite>();
        for (int i = 0; i < 64; i++)
        {
            var tex = Resources.Load<Texture2D>(prefix + i.ToString("00"));
            if (tex == null) break;
            var s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
            s.name = tex.name;
            list.Add(s);
        }
        return list.ToArray();
    }
}
