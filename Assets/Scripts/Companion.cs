using UnityEngine;

/// Khalid the red panda: follows the knight, pounces on skeletons to distract them, and digs up berries after fights.
public class Companion : Actor
{
    enum St { Follow, Pounce, Return, Fetch, Dig, Happy }
    St st = St.Follow; float t, phase, blinkT = 2f, idleT, pounceCd = 5f, sideT; int side = -1; Vector2 jumpFrom, jumpTo, fetchAt; Enemy target;
    Transform body, tail; Transform[] legs = new Transform[4]; SpriteRenderer head; Vector2 vel; bool moving; float happyT; int berries;

    public static Companion Create(Vector2 at)
    {
        var go = new GameObject("Khalid"); var c = go.AddComponent<Companion>();
        c.pos = at; c.radius = 0.2f; c.scale = 0.95f; c.BaseSetup(0.75f);
        var v = c.visual;
        c.legs[1] = Rig.Part(v, "p_leg", new Vector2(-0.13f, 0.19f), 0); c.legs[3] = Rig.Part(v, "p_leg", new Vector2(0.23f, 0.19f), 0);
        c.body = new GameObject("body").transform; c.body.SetParent(v, false); c.body.localPosition = new Vector3(0, 0.16f, 0);
        c.tail = Rig.Part(c.body, "p_tail", new Vector2(-0.30f, 0.24f), 1);
        Art.New("p_body", c.body, Vector3.zero, 2);
        c.legs[0] = Rig.Part(v, "p_leg", new Vector2(-0.22f, 0.19f), 3); c.legs[2] = Rig.Part(v, "p_leg", new Vector2(0.14f, 0.19f), 3);
        c.head = Art.New("p_head", c.body, new Vector3(0.30f, 0.26f, 0), 4);
        c.CacheRenderers(); c.ApplyTransform();
        return c;
    }

    public void Cheer(float dur) { happyT = dur; Sfx.Play("khalid_happy", 0.5f); }

    /// Called after an encounter: run ahead, dig, and pop out a berry for the knight.
    public void Fetch()
    {
        var p = Game.I.player;
        fetchAt = p.pos + new Vector2(p.facing * 1.6f, Random.Range(-0.4f, 0.4f));
        fetchAt.y = Mathf.Clamp(fetchAt.y, World.MinY + 0.3f, World.MaxY - 0.2f);
        st = St.Fetch; t = 0;
    }

    void Update()
    {
        float dt = Time.deltaTime; if (dt <= 0) return;
        var p = Game.I.player; t += dt; if (happyT > 0) happyT -= dt; if (pounceCd > 0) pounceCd -= dt;
        moving = false;
        switch (st)
        {
            case St.Follow:
                {
                    sideT += dt;
                    if (sideT > 0.5f && -p.facing != side) { side = -p.facing; sideT = 0; }
                    if (-p.facing == side) sideT = 0;
                    float back = Game.I.InCombat ? 1.7f : 1.0f;
                    MoveTo(p.pos + new Vector2(side * back, 0.32f), dt, 0.3f);
                    if (!moving) facing = p.pos.x + p.facing * 2 >= pos.x ? 1 : -1;
                    if (Vector2.Distance(pos, p.pos) > 13f) { pos = p.pos + new Vector2(side, 0.3f); Fx.Dust(pos, 4); }
                    if (Game.I.InCombat && pounceCd <= 0 && !p.dead)
                    {
                        target = NearestEnemy(4.5f);
                        if (target != null) { st = St.Pounce; t = 0; jumpFrom = pos; pounceCd = Random.Range(5.5f, 8f); Sfx.Play("khalid", 0.45f); facing = target.pos.x >= pos.x ? 1 : -1; }
                        else pounceCd = 1f;
                    }
                    break;
                }
            case St.Pounce:
                {
                    if (target == null || target.dead) { st = St.Follow; z = 0; break; }
                    float k = Mathf.Clamp01(t / 0.38f);
                    jumpTo = target.pos + new Vector2(0, -0.05f);
                    pos = Vector2.Lerp(jumpFrom, jumpTo, k); z = Mathf.Sin(k * Mathf.PI) * 1.3f + k * 0.9f * target.scale;
                    if (k >= 1)
                    {
                        target.Distract(1.3f); Fx.HitSpark(target.Center + new Vector2(0, 0.4f), 0.7f, new Color(1, 0.7f, 0.4f)); Sfx.Play("hit2", 0.35f, 0.1f, 1.5f);
                        st = St.Return; t = 0; jumpFrom = pos; jumpTo = pos + new Vector2(-facing * 1.6f, 0.3f); vz = 0;
                        jumpTo.y = Mathf.Clamp(jumpTo.y, World.MinY, World.MaxY);
                    }
                    break;
                }
            case St.Return:
                {
                    float k = Mathf.Clamp01(t / 0.4f); float z0 = 0.9f;
                    pos = Vector2.Lerp(jumpFrom, jumpTo, k); z = Mathf.Lerp(z0, 0, k) + Mathf.Sin(k * Mathf.PI) * 0.8f;
                    if (k >= 1) { z = 0; st = St.Follow; Fx.Dust(pos, 2, 0.6f); }
                    break;
                }
            case St.Fetch:
                MoveTo(fetchAt, dt, 0.15f, 7f);
                if (!moving || t > 3f) { st = St.Dig; t = 0; Sfx.Play("khalid", 0.4f); }
                break;
            case St.Dig:
                if (Random.value < dt * 22) Fx.Dust(pos + new Vector2(facing * 0.35f, 0), 1, 0.8f, 1.6f);
                if (t > 0.9f)
                {
                    Pickup.Spawn(pos + new Vector2(facing * 0.45f, 0), true);
                    st = St.Happy; t = 0; Cheer(1.4f);
                }
                break;
            case St.Happy:
                if (t > 1.3f) st = St.Follow;
                break;
        }
        World.ClampLane(ref pos);
        Animate(dt);
        ApplyTransform();
    }

    Enemy NearestEnemy(float maxDist)
    {
        Enemy best = null; float bd = maxDist;
        for (int i = 0; i < All.Count; i++)
        {
            var e = All[i] as Enemy; if (e == null || e.dead || e.IsBoss || e.st != Enemy.St.Chase) continue;
            float d = Vector2.Distance(e.pos, pos); if (d < bd) { bd = d; best = e; }
        }
        return best;
    }

    void MoveTo(Vector2 target, float dt, float stop, float maxSpeed = 6.2f)
    {
        Vector2 d = target - pos; float dist = d.magnitude;
        if (dist < stop) { vel = Vector2.Lerp(vel, Vector2.zero, 1 - Mathf.Exp(-12 * dt)); idleT += dt; return; }
        float sp = Mathf.Min(maxSpeed, 1.5f + dist * 3.2f);
        vel = Vector2.Lerp(vel, d / dist * sp, 1 - Mathf.Exp(-10 * dt));
        pos += vel * dt; moving = true; idleT = 0;
        if (Mathf.Abs(vel.x) > 0.3f) facing = vel.x > 0 ? 1 : -1;
    }

    void Animate(float dt)
    {
        float tt = Time.time; float sp = vel.magnitude;
        blinkT -= dt; if (blinkT < -0.12f) blinkT = Random.Range(2f, 5f);
        bool happy = happyT > 0 || st == St.Happy;
        head.sprite = Art.S(happy ? "p_head_happy" : (blinkT < 0 || st == St.Dig ? "p_head_blink" : "p_head"));
        float bodyRot = 0, bob = 0, tailA = 12 + Mathf.Sin(tt * 2.2f) * 9, headA = Mathf.Sin(tt * 1.3f) * 4; float[] la = { 0, 0, 0, 0 };
        if (st == St.Pounce || st == St.Return) { bodyRot = st == St.Pounce ? 18 : -10; la[0] = la[1] = -50; la[2] = la[3] = 55; tailA = -25; headA = -8; }
        else if (st == St.Dig) { bodyRot = -20; tailA = 40 + Mathf.Sin(tt * 18) * 14; la[2] = Mathf.Sin(tt * 30) * 50; la[3] = -Mathf.Sin(tt * 30) * 50; headA = -18; bob = -0.03f; }
        else if (moving && sp > 0.4f)
        {
            phase += dt * (6f + sp * 2.4f); float s = Mathf.Sin(phase);
            la[0] = s * 42; la[1] = s * 30 + 10; la[2] = -s * 42; la[3] = -s * 30 - 10;
            bob = Mathf.Abs(Mathf.Cos(phase)) * 0.07f; bodyRot = s * 5; tailA = -6 + Mathf.Sin(phase * 0.5f) * 10; headA = -s * 4;
        }
        else if (happy) { bob = Mathf.Abs(Mathf.Sin(tt * 9)) * 0.14f; tailA = 20 + Mathf.Sin(tt * 16) * 22; headA = Mathf.Sin(tt * 9) * 8; bodyRot = 6; }
        else { bob = Mathf.Sin(tt * 2.4f) * 0.01f; if (idleT > 2.5f) { bodyRot = 14; la[0] = la[1] = 30; headA += -10; tailA = 30 + Mathf.Sin(tt * 3) * 12; } }
        float k = 1 - Mathf.Exp(-22 * dt);
        body.localPosition = Vector3.Lerp(body.localPosition, new Vector3(0, 0.16f + bob, 0), k);
        body.localRotation = Quaternion.Slerp(body.localRotation, Quaternion.Euler(0, 0, bodyRot), k);
        tail.localRotation = Quaternion.Slerp(tail.localRotation, Quaternion.Euler(0, 0, tailA), k);
        head.transform.localRotation = Quaternion.Slerp(head.transform.localRotation, Quaternion.Euler(0, 0, headA), k);
        for (int i = 0; i < 4; i++) legs[i].localRotation = Quaternion.Slerp(legs[i].localRotation, Quaternion.Euler(0, 0, la[i]), k);
    }
}

/// Healing berry. Pops out of the ground, bobs, heals on touch.
public class Pickup : MonoBehaviour
{
    Vector2 pos; float z, vz, t; SpriteRenderer sr, glow, shadow;

    public static void Spawn(Vector2 at, bool pop)
    {
        var go = new GameObject("Berry"); var p = go.AddComponent<Pickup>();
        at.y = Mathf.Clamp(at.y, World.MinY + 0.1f, World.MaxY - 0.1f);
        p.pos = at; p.vz = pop ? 7f : 4f; p.z = 0.05f;
        p.shadow = Art.New("fx_shadow", go.transform, Vector3.zero, 0); p.shadow.transform.localScale = Vector3.one * 0.5f;
        p.glow = Art.New("fx_glow", go.transform, Vector3.zero, 0, "Default", true); p.glow.color = new Color(1, 0.5f, 0.5f, 0.5f);
        p.sr = Art.New("fx_berry", go.transform, Vector3.zero, 0);
        go.transform.position = at;
        Sfx.Play("pickup", 0.25f, 0, 0.8f);
    }

    void Update()
    {
        float dt = Time.deltaTime; if (dt <= 0) return; t += dt;
        if (z > 0 || vz > 0) { vz -= 26 * dt; z += vz * dt; if (z < 0) { z = 0; vz = 0; } }
        float bob = z + 0.12f + Mathf.Sin(t * 4) * 0.06f;
        int o = Art.YOrder(pos.y);
        sr.transform.localPosition = new Vector3(0, bob, 0); sr.sortingOrder = o; shadow.sortingOrder = o - 2; glow.sortingOrder = o - 1;
        glow.transform.localPosition = new Vector3(0, bob + 0.2f, 0); glow.transform.localScale = Vector3.one * (1.1f + Mathf.Sin(t * 5) * 0.15f);
        var p = Game.I.player;
        if (t > 0.35f && !p.dead && Vector2.Distance(p.pos, pos) < 0.7f && p.hp < p.maxHp)
        {
            p.Heal(25); Sfx.Play("pickup", 0.6f); Fx.HitSpark(pos + new Vector2(0, 0.4f), 0.7f, new Color(1, 0.6f, 0.7f));
            Destroy(gameObject);
        }
    }
}
