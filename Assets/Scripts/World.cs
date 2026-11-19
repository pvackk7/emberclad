using UnityEngine;

/// Level geometry: lane bounds, obstacle circles and the procedural dressing of the forest.
public static class World
{
    public const float MinY = -3.85f, MaxY = -1.3f, Tile = 20.48f;
    public static float MinX = -2.5f, MaxX = 139.5f, lockMin = -999, lockMax = 999;
    public const float CampX = 136.5f;

    public static Transform shrine;

    public static float Height(float x)
    {
        return x * .066f + .37f * Mathf.Sin(x * .082f) + .12f * Mathf.Sin(x * .17f);
    }

    public static Vector2 Project(Vector2 p) { return new Vector2(p.x, p.y + Height(p.x)); }
    public static Vector2 Unproject(Vector2 p) { return new Vector2(p.x, p.y - Height(p.x)); }

    public static void ClampLane(ref Vector2 p) { p.y = Mathf.Clamp(p.y, MinY, MaxY); p.x = Mathf.Clamp(p.x, MinX - 3, MaxX + 3); }

    public static void Constrain(Actor a)
    {
        bool player = a is Player;
        a.pos.y = Mathf.Clamp(a.pos.y, MinY, MaxY);
        if (player) a.pos.x = Mathf.Clamp(a.pos.x, Mathf.Max(MinX, lockMin), Mathf.Min(MaxX, lockMax));
        else a.pos.x = Mathf.Clamp(a.pos.x, MinX - 1, MaxX + 1);
        if (a.z > 0.9f) return;
        Vector2 separation = a.pos - new Vector2(CampX, -1.45f);
        separation.y *= 2;
        float distance = separation.magnitude;
        if (distance > .0001f && distance < .7f + a.radius)
        {
            Vector2 push = separation * ((.7f + a.radius - distance) / distance);
            push.y *= .5f;
            a.pos += push;
        }
    }

    public static bool OutOfView(float x, float margin)
    {
        return Mathf.Abs(x - CameraRig.I.transform.position.x) > CameraRig.I.HalfW + margin;
    }

    public static void Build(Transform root, Transform cam)
    {
        lockMin = -999; lockMax = 999;
        ForestScenery.Build(root);
        var vignette = Art.New("fx_vignette", cam, new Vector3(0, 0, 9), 100, "Foreground");
        vignette.color = new Color(1, 1, 1, .22f);
        vignette.gameObject.AddComponent<ViewportOverlay>();
        shrine = EndCampfire.Create(root, new Vector2(CampX, -1.55f)).transform;
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
    void Apply(Vector2 o) { transform.position = new Vector3(x + o.x, World.Height(x) + o.y, -10); }

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

[DefaultExecutionOrder(210)]
public class ViewportOverlay : MonoBehaviour
{
    Camera cam;
    SpriteRenderer sr;
    float height, aspect;

    void Awake()
    {
        cam = GetComponentInParent<Camera>();
        sr = GetComponent<SpriteRenderer>();
    }

    void LateUpdate()
    {
        if (cam == null || sr.sprite == null) return;
        float h = cam.orthographicSize * 2f;
        if (height == h && aspect == cam.aspect) return;
        height = h;
        aspect = cam.aspect;
        Vector2 size = sr.sprite.bounds.size;
        transform.localScale = new Vector3((h * aspect + 0.04f) / size.x, (h + 0.04f) / size.y, 1);
    }
}
