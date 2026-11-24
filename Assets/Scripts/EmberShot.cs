using UnityEngine;

/// A coal lobbed down from the sniper's branch.
///
/// It is aimed at where the knight was standing when it left the hand and never
/// corrects, which is the whole point: the counter to the sniper is to keep moving,
/// not to out-trade him. It arcs down to body height over the flight so it reads as
/// thrown rather than fired.
public class EmberShot : MonoBehaviour
{
    Vector2 pos, vel;
    float z, dz, life, spin;
    SpriteRenderer core, glow;
    Actor from;

    const float Speed = 9.5f, MaxLife = 3.2f, HitRadius = .62f;

    public static void Spawn(Vector2 at, float height, Vector2 target, Actor shooter)
    {
        var go = new GameObject("EmberShot");
        var s = go.AddComponent<EmberShot>();
        s.pos = at; s.z = height; s.from = shooter;

        Vector2 d = target - at;
        float flight = Mathf.Max(d.magnitude, .01f) / Speed;
        s.vel = d.normalized * Speed;
        // Falls from the branch to roughly chest height over exactly the time it takes
        // to cross, so the arc lands with the shot instead of before or after it.
        s.dz = (.55f - height) / flight;

        s.glow = Art.New("fx_glow", go.transform, Vector3.zero, 0, "Default", true);
        s.glow.color = new Color(1f, .52f, .16f, .7f);
        s.glow.transform.localScale = Vector3.one * (.9f / Mathf.Max(.01f, s.glow.sprite.bounds.size.y));
        s.core = Art.New("ui_ember", go.transform, Vector3.zero, 1, "Default", true);
        s.core.transform.localScale = Vector3.one * (.42f / Mathf.Max(.01f, s.core.sprite.bounds.size.y));
        go.transform.position = World.Project(at);
        Sfx.Play("swing1", .3f, 0, 1.5f);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        life += dt;
        pos += vel * dt;
        z = Mathf.Max(0f, z + dz * dt);
        spin += dt * 420f;

        Vector2 ground = World.Project(pos);
        transform.position = new Vector3(ground.x, ground.y + z, 0f);
        core.sortingOrder = Art.YOrder(pos.y) + 6;
        glow.sortingOrder = core.sortingOrder - 1;
        core.transform.localRotation = Quaternion.Euler(0, 0, spin);
        float flicker = 1f + Mathf.Sin(life * 31f) * .13f;
        glow.transform.localScale = Vector3.one * (.9f / Mathf.Max(.01f, glow.sprite.bounds.size.y)) * flicker;
        if (Random.value < dt * 26f) Fx.Flames(pos + new Vector2(0, z), 1, .22f);

        var p = Game.I != null ? Game.I.player : null;
        if (p != null && !p.dead)
        {
            Vector2 d = p.pos - pos; d.y *= 1.7f;
            if (d.magnitude < HitRadius + p.radius && Mathf.Abs(p.z - z) < 1.3f)
            {
                p.TakeHit(new Hit { dmg = 11, knock = 3.2f, stun = .35f, dir = vel.x >= 0 ? 1 : -1, from = from });
                Burst();
                return;
            }
        }
        if (life > MaxLife || z <= 0f || World.OutOfView(pos.x, 3f)) Burst();
    }

    void Burst()
    {
        Fx.Flames(pos + new Vector2(0, Mathf.Max(z, .1f)), 3, .5f);
        Fx.Ring(pos, .9f, new Color(1f, .62f, .25f, .7f), .22f);
        Destroy(gameObject);
    }
}
