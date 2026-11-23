using UnityEngine;

/// Forest skeleton: walks at the player, telegraphs, lunges with its dagger. Variants differ by stats only.
public class Enemy : Actor
{
    public enum Kind { Grunt, Runt, Brute, Boss, Dragon }
    public enum St { Spawn, Chase, Windup, Strike, Recover, Hurt, Air, Down, Stagger, LeapPrep, Leap, Summon, Guard, Evade }
    [System.NonSerialized] public bool rewardGranted;
    public Kind kind; public St st = St.Spawn;
    public static int tokens;
    Rig rig; float t, speed, windup, range, cd, slotY, hurtDur, walkPhase, spinCur, recoverDur, sideBias;
    int dmg, hitCount, phase, lastDir = 1; bool hasToken, struck, dying, bounced; Vector2 leapFrom, leapTo;
    float faceT; // seconds left on the shocked face after a hit
    public bool hasShield;
    Transform shield;

    /// Strips the shield off the corpse so the one lying on the ground is visibly
    /// the one it was carrying, not a second copy.
    public void ReleaseShield()
    {
        if (shield != null) { Destroy(shield.gameObject); shield = null; }
        hasShield = false;
    }
    float defenseCd, decisionTime;
    Vector2 evadeDirection;
    bool strikeFx;
    public bool IsBoss => kind == Kind.Boss;
    /// The dragon cannot be hit at all: he is in the air, well out of reach, and the
    /// round he appears in is about moving out of his line rather than trading blows.
    public bool Invulnerable => kind == Kind.Dragon;
    BabyDragon dragon;

    // Perch state. Kept apart from the walking skeletons' state machine entirely,
    // because none of chase / windup / strike means anything to something that never
    // comes down.
    float perchT, shotCd, fleeT = -1f, breathT, homeY;

    /// His escort is dead. He loses his nerve, scrabbles backwards and is gone.
    public void Flee()
    {
        if (fleeT >= 0f) return;
        fleeT = 0f;
        Sfx.Play("tele", .55f, 0, 1.6f);
        Fx.Emit("fx_puff", Center, new Vector2(0, 1.5f), .5f, .5f, 1f, new Color(.82f, .82f, .88f, .8f), new Color(.82f, .82f, .88f, 0));
    }

    /// shield: null leaves it to chance as before, true or false pins it. The fixed
    /// rosters pin it, so "two shield skeletons" means two, every time.
    public static Enemy Create(Kind kind, Vector2 at, bool? shield = null)
    {
        var go = new GameObject("Skeleton_" + kind); var e = go.AddComponent<Enemy>();
        e.kind = kind; e.pos = at;
        switch (kind)
        {
            case Kind.Runt: e.Stats(42, 3.7f, 9, 0.78f, 0.30f, 1.15f, 0.28f); break;
            case Kind.Brute: e.Stats(115, 2.3f, 18, 1.42f, 0.50f, 1.7f, 0.5f); break;
            case Kind.Boss: e.Stats(540, 2.1f, 18, 2.15f, 0.8f, 2.3f, 0.8f); break;
            case Kind.Dragon: e.Stats(9999, 0f, 11, 1.0f, 0f, 0f, 0.34f); break;
            default: e.Stats(64, 2.9f, 12, 1.0f, 0.36f, 1.35f, 0.34f); break;
        }
        e.BaseSetup(e.scale * 1.05f);
        if (kind == Kind.Dragon) e.dragon = BabyDragon.Attach(e.visual);
        else e.rig = Rig.Skeleton(e.visual, kind == Kind.Brute, kind == Kind.Boss, kind != Kind.Grunt && kind != Kind.Runt);
        e.hasShield = kind != Kind.Boss && kind != Kind.Dragon && (shield ?? Random.value < .25f);
        if (e.hasShield)
        {
            e.shield = Rig.Part(e.rig.body, "s_shield", new Vector2(.32f, .30f), 10);
            e.hp += 12; e.maxHp = e.hp;
        }
        e.defenseCd = Random.Range(.5f, 1.4f);
        e.CacheRenderers();
        if (kind == Kind.Runt) foreach (var r in e.rends) r.color = new Color(0.86f, 0.96f, 0.84f, 1);
        e.slotY = Random.Range(-0.55f, 0.55f); e.sideBias = Random.value < 0.5f ? -1 : 1;
        e.facing = Game.I.player.pos.x >= at.x ? 1 : -1;
        e.ApplyTransform(0.01f);
        Fx.Dust(at, 6, 1.1f * e.scale, 1.5f);
        return e;
    }

    // Every enemy's walk scaled together (user, 2026-11-23): x4.9/2.9 so a regular skeleton walks exactly with the knight.
    public const float SpeedScale = 4.9f / 2.9f;
    void Stats(int hp_, float speed_, int dmg_, float scale_, float windup_, float range_, float radius_)
    {
        hp = maxHp = hp_; speed = speed_ * SpeedScale * Random.Range(0.9f, 1.1f); dmg = dmg_; scale = scale_; windup = windup_; range = range_; radius = radius_;
    }

    protected override void OnDisable() { base.OnDisable(); ReleaseToken(); }
    void ReleaseToken() { if (hasToken) { hasToken = false; tokens = Mathf.Max(0, tokens - 1); } }
    // Any state other than tumbling (Air) or lying (Down) stands the skeleton back up, so a
    // tumble interrupted by a hit or a non-Air landing can never leave it walking sideways.
    void Go(St s) { st = s; t = 0; if (s != St.Air && s != St.Down) spinCur = 0; }

    void Update()
    {
        float dt = Time.deltaTime; if (dt <= 0) return;
        if (kind == Kind.Dragon) { TickDragon(dt); return; }
        t += dt; if (cd > 0) cd -= dt; defenseCd -= dt; decisionTime -= dt;
        var p = Game.I.player; float extraY = 1f;
        rig.rate = 24f;
        switch (st)
        {
            case St.Spawn:
                {
                    float k = Mathf.Clamp01(t / 0.6f); float c1 = 1.70158f, c3 = c1 + 1; float e = 1 + c3 * Mathf.Pow(k - 1, 3) + c1 * Mathf.Pow(k - 1, 2);
                    extraY = Mathf.Max(0.01f, e);
                    if (Random.value < dt * 25) Fx.Dust(pos, 1, 0.8f * scale, 1.5f);
                    if (t > 0.75f) { Go(St.Chase); cd = Random.Range(0.3f, 1.4f); }
                    break;
                }
            case St.Chase: Chase(p, dt); break;
            case St.Guard:
                if (t > .65f) { Go(St.Chase); cd = .08f; }
                break;
            case St.Evade:
                pos += evadeDirection * Mathf.Lerp(5.3f, 1.2f, Mathf.Clamp01(t / .3f)) * dt;
                if (t > .3f) { Go(St.Chase); cd = .12f; }
                break;
            case St.Windup:
                if (t < windup * .65f)
                {
                    Vector2 aim = p.pos + (p.Dodging ? p.RollDirection * .5f : Vector2.zero);
                    facing = aim.x >= pos.x ? 1 : -1;
                    pos.y = Mathf.MoveTowards(pos.y, aim.y, dt * 1.8f);
                }
                if (t >= windup) { Go(St.Strike); struck = false; strikeFx = false; rig.SnapArm(30, -135); kb = new Vector2(facing * (IsBoss ? 7f : 8.5f), (p.pos.y - pos.y) * 2f); Sfx.Play("swing3", 0.35f, 0.1f, IsBoss ? 0.7f : 1.1f); }
                break;
            case St.Strike:
                if (!struck && t >= .045f && t <= .23f)
                {
                    float dx = (p.pos.x - pos.x) * facing;
                    if (!strikeFx) Fx.Emit(IsBoss ? "fx_slash_big" : "fx_slash", new Vector2(0.75f, 0.8f), Vector2.zero, 0.14f, 0.9f, 1.15f, new Color(0.9f, 1f, 0.8f, 0.9f), new Color(0.6f, 1f, 0.6f, 0), -20, 0, 0, false, 30, -999, 0, visual);
                    strikeFx = true;
                    if (dx > -0.3f && dx < range + 0.45f && Mathf.Abs(p.pos.y - pos.y) < 0.55f + radius * 0.4f && p.z < 1.1f * scale)
                        struck = p.RollProtected || p.TakeHit(new Hit { dmg = dmg, knock = IsBoss ? 9f : 4f, launch = IsBoss ? 5f : 0, dir = facing, from = this });
                }
                if (t > 0.3f) { Go(St.Recover); recoverDur = Random.Range(.32f, .58f) * (IsBoss ? 1.2f : 1f); }
                break;
            case St.Recover:
                if (t > recoverDur) { ReleaseToken(); cd = Random.Range(.22f, .65f); Go(St.Chase); }
                break;
            case St.Hurt:
                if (t > hurtDur) Go(St.Chase);
                break;
            case St.Air:
                spinCur += 520f * dt * -lastDir;
                break;
            case St.Down:
                if (t > 0.55f) { spinCur = Mathf.Lerp(spinCur, 0, 1 - Mathf.Exp(-14 * dt)); if (t > 0.85f) { spinCur = 0; Go(St.Chase); } }
                break;
            case St.Stagger:
                if (t > 1.4f) { Go(St.Chase); cd = 0.3f; }
                break;
            case St.LeapPrep:
                if (t > 0.55f) { Go(St.Leap); leapFrom = pos; leapTo = p.pos + new Vector2(0, 0.05f); Sfx.Play("jump", 0.6f, 0, 0.6f); Fx.Dust(pos, 8, 1.6f, 2); }
                break;
            case St.Leap:
                {
                    float k = Mathf.Clamp01(t / 0.75f);
                    pos = Vector2.Lerp(leapFrom, leapTo, k); z = Mathf.Sin(k * Mathf.PI) * 3.4f;
                    if (k >= 1)
                    {
                        z = 0; Fx.Ring(pos, 5.5f, new Color(0.8f, 1f, 0.7f, 0.9f), 0.4f); Fx.Ring(pos, 3.4f, new Color(1, 1, 1, 0.7f), 0.28f); Fx.Dust(pos, 14, 1.6f, 3.5f);
                        CameraRig.I.Shake(0.5f, 0.4f); Sfx.Play("boom", 0.8f, 0.03f, 0.8f);
                        Vector2 d = p.pos - pos; d.y *= 1.8f;
                        if (d.magnitude < 2.5f && p.z < 0.7f) p.TakeHit(new Hit { dmg = 16, knock = 8f, launch = 7f, dir = d.x >= 0 ? 1 : -1, from = this });
                        Go(St.Recover); recoverDur = 1.1f;
                    }
                    break;
                }
            case St.Summon:
                if (!struck && t > 0.6f) { struck = true; Game.I.SpawnAdds(phase == 1 ? 4 : 5, pos); }
                if (Random.value < dt * 20) CameraRig.I.Shake(0.08f, 0.1f);
                if (t > 1.5f) { Go(St.Chase); cd = 0.6f; }
                break;
        }
        if (st != St.Leap) Physics(dt);
        Animate(dt, p);
        if (shield != null)
        {
            shield.localPosition = Vector3.Lerp(shield.localPosition, new Vector3(st == St.Guard ? .43f : .26f, st == St.Guard ? .40f : .25f, 0), 1 - Mathf.Exp(-20 * dt));
            shield.localRotation = Quaternion.Euler(0, 0, st == St.Guard ? -12 : 12);
        }
        rig.Apply(dt);
        ApplyTransform(extraY);
    }

    void Chase(Player p, float dt)
    {
        if (p.dead) return;
        float dxp = p.pos.x - pos.x;
        if (!IsBoss && defenseCd <= 0 && decisionTime <= 0 && Mathf.Abs(dxp) < 2.5f && Mathf.Abs(p.pos.y - pos.y) < .8f)
        {
            decisionTime = .3f;
            if (Random.value < (p.Attacking ? .65f : .28f))
            {
                facing = dxp >= 0 ? 1 : -1;
                defenseCd = Random.Range(1.4f, 2.7f);
                if (hasShield && Random.value < .75f) Go(St.Guard);
                else
                {
                    evadeDirection = new Vector2(-facing * .5f, Random.value < .5f ? -1 : 1).normalized;
                    if (pos.y < World.MinY + .6f) evadeDirection.y = Mathf.Abs(evadeDirection.y);
                    if (pos.y > World.MaxY - .6f) evadeDirection.y = -Mathf.Abs(evadeDirection.y);
                    Go(St.Evade);
                }
                return;
            }
        }
        facing = dxp >= 0 ? 1 : -1;
        float side = Mathf.Abs(dxp) < 0.3f ? sideBias : -Mathf.Sign(dxp);
        Vector2 target = p.pos + new Vector2(side * range * 0.78f, slotY * (hasToken || tokens < Game.I.maxTokens ? .35f : 1f) + Mathf.Sin(Time.time * 2.8f + sideBias * 3) * .16f);
        if (tokens >= Game.I.maxTokens && cd <= 0) target.x += side * 1.2f;   // hang back a little while others attack
        Vector2 d = target - pos; float dist = d.magnitude;
        Vector2 mv = dist > 0.12f ? d / dist * speed : Vector2.zero;
        for (int i = 0; i < All.Count; i++)
        {
            var o = All[i] as Enemy; if (o == null || o == this || o.dead) continue;
            Vector2 sep = pos - o.pos; float sd = sep.magnitude, min = radius + o.radius + 0.25f;
            if (sd < min && sd > 0.001f) mv += sep / sd * (min - sd) * 6f;
        }
        mv.y *= 0.75f;
        pos += mv * dt;
        walkPhase += dt * mv.magnitude * 3.4f / Mathf.Max(0.6f, scale);
        float adx = Mathf.Abs(dxp), ady = Mathf.Abs(p.pos.y - pos.y);
        if (IsBoss)
        {
            int want = hp < maxHp * 0.33f ? 2 : hp < maxHp * 0.66f ? 1 : 0;
            if (want > phase) { phase = want; Go(St.Summon); struck = false; Sfx.Play("roar", 0.9f); Flash(new Color(0.7f, 1f, 0.6f), 0.5f); return; }
            if (cd <= 0 && adx > 3.6f) { Go(St.LeapPrep); Sfx.Play("tele", 0.5f, 0, 0.7f); Flash(new Color(1f, 0.4f, 0.3f), 0.4f); return; }
        }
        if (cd <= 0 && adx < range * 0.98f && ady < 0.42f && tokens < Game.I.maxTokens && Game.I.state == Game.State.Playing)
        {
            tokens++; hasToken = true; Go(St.Windup);
            Flash(new Color(1f, 0.35f, 0.25f), windup * 0.8f);
            if (kind != Kind.Runt) Sfx.Play("tele", 0.3f, 0.05f, kind == Kind.Grunt ? 1.2f : 0.8f);
        }
    }

    /// Jaren's pounce: a brief stun without real damage.
    public void Distract(float dur)
    {
        if (dead || IsBoss || st == St.Spawn || st == St.Air || st == St.Down) return;
        ReleaseToken(); Go(St.Hurt); hurtDur = dur; Flash(Color.white, 0.12f);
    }

    /// Caught a parried swing square on the shield: reels back, wide open.
    public void Parried(int dir)
    {
        if (dead || st == St.Spawn || st == St.Air || st == St.Down) return;
        ReleaseToken(); Go(St.Stagger); defenseCd = 3;
        kb = new Vector2(dir * 2.5f, 0); Flash(Color.white, 0.14f); Sfx.Play("bone_break", .5f);
    }

    /// Sits still, lobs a coal every couple of seconds, and never touches the ground.
    /// Physics is deliberately not run here: gravity would pull him out of the tree.
    void TickDragon(float dt)
    {
        var p = Game.I.player;
        perchT += dt;
        if (p != null) facing = p.pos.x >= pos.x ? 1 : -1;
        if (dragon != null) dragon.Facing = facing;

        if (fleeT >= 0f)
        {
            // Beats hard, climbs, and is gone over the treetops.
            fleeT += dt;
            pos.x -= facing * 4.2f * dt;
            z += 2.6f * dt;
            if (dragon != null) { dragon.Breathing = 0f; dragon.SetAlpha(Mathf.Clamp01(1f - fleeT / .9f)); }
            if (Random.value < dt * 10f) Fx.Dust(pos + new Vector2(0, z), 1, .4f);
            if (fleeT > .95f) { dead = true; Destroy(gameObject); return; }
        }
        else
        {
            // Holds station above the clearing, drifting on a slow figure of eight
            // around wherever the roster put him.
            if (homeY == 0f) homeY = pos.y;
            z = PerchHeight + Mathf.Sin(perchT * 1.3f) * .22f;
            pos.y = homeY + Mathf.Sin(perchT * .8f) * .12f;

            shotCd -= dt;
            bool canShoot = p != null && !p.dead && Game.I.state == Game.State.Playing && Mathf.Abs(p.pos.x - pos.x) < 18f;

            // A visible wind-up: he rears back and the mouth lights before anything
            // comes out, so the shot can be read and walked away from.
            if (canShoot && shotCd < WindupTime)
            {
                breathT = Mathf.Clamp01((WindupTime - shotCd) / WindupTime);
                if (dragon != null) dragon.Breathing = breathT;
            }
            else { breathT = 0f; if (dragon != null) dragon.Breathing = 0f; }

            if (shotCd <= 0f && canShoot)
            {
                shotCd = Random.Range(1.9f, 2.8f);
                Vector2 muzzle = pos + new Vector2(facing * .45f, 0f);
                float muzzleZ = z + .22f;
                EmberShot.Spawn(muzzle, muzzleZ, p.pos, this);
                Fx.Flames(new Vector2(muzzle.x, muzzle.y + muzzleZ), 3, .55f);
                Sfx.Play("cast", .45f, 0, 1.5f);
                if (dragon != null) dragon.Breathing = 0f;
            }
        }
        if (dragon != null) dragon.Tick(dt, fleeT >= 0f);
        ApplyTransform();
    }

    public const float PerchHeight = 2.15f;
    const float WindupTime = .55f;

    public override bool TakeHit(Hit h)
    {
        if (dead || (st == St.Spawn && t < 0.35f) || st == St.Summon || st == St.Leap) return false;
        if (Invulnerable)
        {
            // Rings off the branch so the swing reads as reaching him and failing,
            // rather than as the hit box quietly missing.
            Fx.HitSpark(Center + new Vector2(-h.dir * .3f, 0), .7f, new Color(.72f, .82f, 1f), h.dir);
            Sfx.Play("hit_heavy", .4f, .03f, 1.6f);
            return false;
        }
        // TEMPORARY: one swing kills. Also walks straight through a raised shield,
        // otherwise the shield-bearers would still soak a light hit.
        bool oneShot = Cheats.On && h.from is Player;
        if (oneShot) h.dmg = hp;
        bool frontal = h.from != null && (h.from.pos.x - pos.x) * facing > 0;
        bool guardBreak = hasShield && st == St.Guard && frontal && h.heavy;
        if (hasShield && st == St.Guard && frontal && !h.heavy && z < .1f && !oneShot)
        {
            Fx.HitSpark(Center + new Vector2(facing * .3f, 0), .65f, new Color(.8f, .9f, 1));
            Sfx.Play("hit_heavy", .45f, .03f, 1.4f);
            if (t < .16f && h.from is Player attacker) attacker.Parried(facing);
            else { kb = new Vector2(h.dir * .8f, 0); t += .12f; }
            return false;
        }
        hp -= h.dmg; lastDir = h.dir; faceT = h.heavy ? .45f : .32f;
        if (h.from is Player && Game.I != null && Game.I.hud != null) Game.I.hud.ShowDamage(this, h.dmg);
        Flash(Color.white, 0.14f);
        Vector2 sp = Center + new Vector2(-h.dir * radius * 0.6f, Random.Range(-0.2f, 0.3f) * scale);
        Fx.HitSpark(sp, h.heavy ? 1.05f : 0.8f, new Color(1, 0.9f, 0.6f), h.dir);
        Sfx.Play(h.heavy ? "hit_heavy" : "hit" + Random.Range(1, 4), h.heavy ? 0.75f : 0.6f, 0.12f);
        Sfx.Play("bone_hit", 0.3f, 0.2f);
        Fx.Bones(pos, 1.2f * scale, 1, 0.7f, h.dir);
        if (hp <= 0)
        {
            dead = true; dying = true; ReleaseToken(); Game.I.OnEnemyKilled(this);
            if (IsBoss) { BossDeath(); return true; }
            st = St.Air; t = 0; vz = Mathf.Max(h.launch, 6.5f); z = Mathf.Max(z, 0.01f); kb = new Vector2(h.dir * Mathf.Max(h.knock, 5f), 0);
            return true;
        }
        if (guardBreak)
        {
            ReleaseToken(); Go(St.Stagger); defenseCd = 3;
            kb = new Vector2(h.dir * 2, 0); Sfx.Play("bone_break", .5f);
            return true;
        }
        if (IsBoss)
        {
            hitCount++; kb = new Vector2(h.dir * 0.8f, 0);
            if (hitCount >= 14 && (st == St.Chase || st == St.Recover || st == St.Windup)) { hitCount = 0; ReleaseToken(); Go(St.Stagger); Sfx.Play("bone_break", 0.6f); CameraRig.I.Shake(0.2f, 0.2f); }
            return true;
        }
        if (kind == Kind.Brute && !h.heavy)
        {
            kb = new Vector2(h.dir * h.knock * 0.4f, 0);
            if (st == St.Chase || st == St.Recover) { Go(St.Hurt); hurtDur = 0.16f; }
            return true;
        }
        ReleaseToken();
        kb = new Vector2(h.dir * h.knock, 0);
        if (h.launch > 0) { Go(St.Air); vz = h.launch * (kind == Kind.Brute ? 0.7f : 1f); z = Mathf.Max(z, 0.01f); bounced = false; }
        else if (st == St.Air) { vz = Mathf.Max(vz, 4.2f); }
        else { Go(St.Hurt); hurtDur = Mathf.Min(h.stun, .23f); }
        return true;
    }

    protected override void OnLand(float v)
    {
        if (st != St.Air) return;
        Fx.Dust(pos, 4, scale);
        if (dying) { Explode(); return; }
        if (!bounced && v < -7f) { bounced = true; vz = 3.2f; z = 0.01f; Sfx.Play("land", 0.4f); return; }
        Go(St.Down); spinCur = 90 * -lastDir * facing; Sfx.Play("bone_hit", 0.4f);
    }

    void Explode()
    {
        Fx.Bones(pos, 1.2f * scale, 7 + (int)(scale * 3), scale, lastDir);
        Fx.Emit("s_head", pos + new Vector2(0, 1.0f * scale), new Vector2(lastDir * Random.Range(1f, 3.5f), Random.Range(5f, 8f)), 1.3f, scale * 0.9f, scale * 0.9f, Color.white, new Color(1, 1, 1, 0), 0, Random.Range(-500f, 500f), 26, false, Art.YOrder(pos.y) + 1, pos.y + 0.2f);
        Fx.Emit("fx_puff", pos + new Vector2(0, 0.6f * scale), Vector2.zero, 0.3f, 0.8f * scale, 2.0f * scale, new Color(0.9f, 0.95f, 0.85f, 0.8f), new Color(0.9f, 0.95f, 0.85f, 0));
        Sfx.Play("bone_break", 0.6f, 0.15f, 1.1f / Mathf.Sqrt(scale));
        CameraRig.I.Shake(0.1f * scale, 0.15f);
        Destroy(gameObject);
    }

    void BossDeath()
    {
        st = St.Stagger; t = -100;
        Game.I.OnBossDead(this);
    }

    public void BossBurst(bool final)
    {
        Vector2 c = pos + new Vector2(Random.Range(-0.8f, 0.8f), Random.Range(0.3f, 2.6f));
        Fx.HitSpark(c, 1.6f, new Color(0.8f, 1f, 0.6f)); Fx.Bones(pos, 2.5f, 4, 1.2f, Random.value < 0.5f ? -1 : 1); Sfx.Play("bone_break", 0.6f, 0.2f); CameraRig.I.Shake(0.2f, 0.2f);
        Flash(Color.white, 0.15f);
        if (final) { scale = 1.6f; Explode(); Fx.Ring(pos, 7f, new Color(1, 1, 0.8f, 0.9f), 0.5f); Sfx.Play("boom", 0.9f, 0, 0.7f); CameraRig.I.Shake(0.6f, 0.6f); }
    }

    void Animate(float dt, Player p)
    {
        var r = rig; float tt = Time.time + walkPhase;
        r.aSpin = spinCur; r.spinOff = Vector2.zero; r.squash = Vector2.one; r.off = Vector2.zero; r.aBody = 0; r.aHead = 0; r.aJaw = 0; r.aLegF = 0; r.aLegB = 0;
        r.aArmF = 18; r.aWeapon = -75; r.aArmB = -10; r.aCape = -8 + Mathf.Sin(Time.time * 2.4f) * 6;
        float chatter = -Mathf.Abs(Mathf.Sin(Time.time * 11f + slotY * 9)) * 16f;
        switch (st)
        {
            case St.Spawn: r.aArmF = 165; r.aArmB = 160; r.aWeapon = 10; r.aJaw = chatter; r.aHead = Mathf.Sin(Time.time * 20) * 6; break;
            case St.Chase:
                {
                    float s = Mathf.Sin(walkPhase);
                    r.aLegF = s * 34; r.aLegB = -s * 34; r.aArmB = s * 30; r.aArmF = 28 - s * 14; r.aWeapon = -78 + s * 8;
                    r.off = new Vector2(0, Mathf.Abs(Mathf.Cos(walkPhase)) * 0.05f); r.aBody = -8 + s * 2; r.aHead = s * 5; r.aJaw = chatter * 0.6f;
                    break;
                }
            case St.Windup:
                {
                    float sh = Mathf.Sin(Time.time * 60) * 2.5f * Mathf.Clamp01(t / windup);
                    r.aArmF = 212 + sh; r.aWeapon = 45 + sh; r.aBody = 12; r.aHead = -8; r.aArmB = 40; r.aJaw = -18; r.squash = new Vector2(1.04f, 0.96f); r.aLegF = -14; r.aLegB = 16;
                    break;
                }
            case St.Strike:
                r.rate = 60; r.aArmF = 30; r.aWeapon = -135; r.aBody = -18; r.aArmB = -50; r.aLegF = 26; r.aLegB = -28; r.aJaw = -20; r.off = new Vector2(0.08f, 0);
                break;
            case St.Guard: r.aBody = 8; r.aArmB = -60; r.aArmF = 85; r.aWeapon = -25; r.aLegF = -15; r.aLegB = 20; break;
            case St.Evade: r.aBody = -18; r.aHead = 12; r.squash = new Vector2(1.08f, .88f); r.aLegF = 35; r.aLegB = -35; break;
            case St.Recover: r.aArmF = 24; r.aWeapon = -110; r.aBody = -8; r.aJaw = chatter * 0.4f; break;
            case St.Hurt:
                r.rate = 40; r.aBody = 18; r.aHead = 22; r.aArmF = -40; r.aWeapon = -60; r.aArmB = -70; r.aJaw = -24; r.aLegF = -14; r.aLegB = 18;
                r.off = new Vector2(Mathf.Sin(Time.time * 70) * 0.02f, 0);
                break;
            case St.Air: r.aArmF = 140; r.aArmB = -120; r.aLegF = 40; r.aLegB = -50; r.aJaw = -26; r.aWeapon = 20; break;
            case St.Down: r.spinOff = new Vector2(0, -0.42f * Mathf.Abs(spinCur) / 90f); r.aArmF = 60; r.aArmB = -40; r.aJaw = -10; break;
            case St.Stagger: r.squash = new Vector2(1.08f, 0.8f); r.aBody = -26; r.aHead = -18 + Mathf.Sin(Time.time * 6) * 6; r.aArmF = -10; r.aWeapon = -150; r.aArmB = -20; r.aJaw = -22; break;
            case St.LeapPrep: r.squash = new Vector2(1.14f, 0.8f); r.aArmF = 190; r.aArmB = 170; r.aWeapon = 0; r.aBody = -10; r.aJaw = -20; break;
            case St.Leap: r.squash = new Vector2(0.94f, 1.08f); r.aArmF = 200; r.aArmB = 190; r.aWeapon = -170; r.aLegF = 40; r.aLegB = -30; r.aJaw = -24; break;
            case St.Summon: r.aArmF = 170 + Mathf.Sin(Time.time * 40) * 6; r.aArmB = 175; r.aWeapon = 0; r.aHead = 16; r.aBody = 8; r.aJaw = -28; r.off = new Vector2(Mathf.Sin(Time.time * 55) * 0.03f, 0); break;
        }
        // Pain face: sockets blow wide, pupils shrink to pinpricks, the jaw drops and
        // rattles. Holds a beat after every hit (bosses too, who never flinch) and
        // stays on while staggered or sent flying.
        if (faceT > 0) faceT -= dt;
        bool pain = faceT > 0 || st == St.Hurt || st == St.Stagger || dying;
        if (r.headSr != null && r.headHurt != null) r.headSr.sprite = pain ? r.headHurt : r.headNormal;
        if (pain) r.aJaw = Mathf.Min(r.aJaw, -34 + Mathf.Sin(Time.time * 48) * 4);
    }
}
