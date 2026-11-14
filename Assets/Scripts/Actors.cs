using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public struct Hit
{
    public int dmg; public float knock; public float launch; public float stun; public int dir; public bool heavy; public Actor from;
}

/// Paper-doll rig: sprites parented in a small hierarchy and posed procedurally every frame.
public class Rig
{
    public Transform spin, body, head, torso, armF, armB, legF, legB, weapon, cape, jaw;
    public Vector3 spinBase, bodyBase;
    public float aArmF, aArmB, aLegF, aLegB, aWeapon, aHead, aBody, aCape, aJaw, aSpin;
    public Vector2 off, spinOff, squash = Vector2.one;
    public float rate = 30f;
    float cArmF, cArmB, cLegF, cLegB, cWeapon, cHead, cBody, cCape, cJaw; Vector2 cOff, cSquash = Vector2.one;

    public static Transform Part(Transform parent, string sprite, Vector2 pos, int order)
    {
        return Art.New(sprite, parent, pos, order).transform;
    }

    public static Rig Knight(Transform visual)
    {
        var r = new Rig();
        r.spinBase = new Vector3(0, 0.6f, 0);
        r.spin = new GameObject("spin").transform; r.spin.SetParent(visual, false); r.spin.localPosition = r.spinBase;
        r.bodyBase = new Vector3(0, -0.26f, 0);
        r.body = new GameObject("body").transform; r.body.SetParent(r.spin, false); r.body.localPosition = r.bodyBase;
        r.cape = Part(r.body, "k_cape", new Vector2(-0.14f, 0.41f), 0);
        r.armB = Part(r.body, "k_arm", new Vector2(0.11f, 0.36f), 1);
        r.legB = Part(r.spin, "k_leg", new Vector2(0.08f, -0.26f), 2);
        r.legF = Part(r.spin, "k_leg", new Vector2(-0.09f, -0.26f), 3);
        r.torso = Part(r.body, "k_torso", new Vector2(0, -0.04f), 4);
        r.head = Part(r.body, "k_head", new Vector2(0.01f, 0.38f), 5);
        r.armF = Part(r.body, "k_arm", new Vector2(-0.03f, 0.36f), 7);
        r.weapon = Part(r.armF, "k_sword", new Vector2(0, -0.33f), 6);
        return r;
    }

    public static Rig Skeleton(Transform visual, bool helm, bool boss, bool redEyes)
    {
        var r = new Rig();
        r.spinBase = new Vector3(0, 0.6f, 0);
        r.spin = new GameObject("spin").transform; r.spin.SetParent(visual, false); r.spin.localPosition = r.spinBase;
        r.bodyBase = new Vector3(0, -0.21f, 0);
        r.body = new GameObject("body").transform; r.body.SetParent(r.spin, false); r.body.localPosition = r.bodyBase;
        if (boss) r.cape = Part(r.body, "s_cloak", new Vector2(-0.10f, 0.52f), 0);
        r.armB = Part(r.body, "s_arm", new Vector2(0.10f, 0.36f), 1);
        r.legB = Part(r.spin, "s_leg", new Vector2(0.07f, -0.21f), 2);
        r.legF = Part(r.spin, "s_leg", new Vector2(-0.07f, -0.21f), 3);
        r.torso = Part(r.body, "s_torso", new Vector2(0, -0.05f), 4);
        r.head = Part(r.body, redEyes ? "s_head_red" : "s_head", new Vector2(0.02f, 0.46f), 6);
        r.jaw = Part(r.head, "s_jaw", new Vector2(-0.03f, 0.03f), 5);
        if (helm) Part(r.head, "s_helm", new Vector2(0f, 0.46f), 7);
        if (boss) Part(r.head, "s_antlers", new Vector2(-0.01f, 0.60f), 7);
        r.armF = Part(r.body, "s_arm", new Vector2(-0.07f, 0.36f), 9);
        r.weapon = Part(r.armF, "s_dagger", new Vector2(0, -0.40f), 8);
        return r;
    }

    public void Apply(float dt)
    {
        float k = 1 - Mathf.Exp(-rate * dt);
        cArmF = Mathf.Lerp(cArmF, aArmF, k); cArmB = Mathf.Lerp(cArmB, aArmB, k);
        cLegF = Mathf.Lerp(cLegF, aLegF, k); cLegB = Mathf.Lerp(cLegB, aLegB, k);
        cWeapon = Mathf.Lerp(cWeapon, aWeapon, k); cHead = Mathf.Lerp(cHead, aHead, k);
        cBody = Mathf.Lerp(cBody, aBody, k); cCape = Mathf.Lerp(cCape, aCape, k); cJaw = Mathf.Lerp(cJaw, aJaw, k);
        cOff = Vector2.Lerp(cOff, off, k); cSquash = Vector2.Lerp(cSquash, squash, k);
        armF.localRotation = Quaternion.Euler(0, 0, cArmF);
        armB.localRotation = Quaternion.Euler(0, 0, cArmB);
        legF.localRotation = Quaternion.Euler(0, 0, cLegF);
        legB.localRotation = Quaternion.Euler(0, 0, cLegB);
        weapon.localRotation = Quaternion.Euler(0, 0, cWeapon - cArmF - cBody);
        head.localRotation = Quaternion.Euler(0, 0, cHead);
        if (cape != null) cape.localRotation = Quaternion.Euler(0, 0, cCape);
        if (jaw != null) jaw.localRotation = Quaternion.Euler(0, 0, cJaw);
        body.localPosition = bodyBase + (Vector3)cOff;
        body.localRotation = Quaternion.Euler(0, 0, cBody);
        spin.localPosition = spinBase + (Vector3)spinOff;
        spin.localRotation = Quaternion.Euler(0, 0, aSpin);
        spin.localScale = new Vector3(cSquash.x, cSquash.y, 1);
    }

    /// Sets arm + weapon directly (no smoothing) for snappy swings.
    public void SnapArm(float arm, float wpn) { aArmF = cArmF = arm; aWeapon = cWeapon = wpn; }
    public Vector2 WeaponTip(float len) { return weapon.TransformPoint(new Vector3(0, len, 0)); }
}

/// Anything standing on the ground plane: has a ground position, a height (z), a shadow and a sorted visual.
public class Actor : MonoBehaviour
{
    public static readonly List<Actor> All = new List<Actor>();
    public const float Gravity = 30f;
    public Vector2 pos; public float z, vz; public Vector2 kb;
    public float radius = 0.35f; public int hp = 10, maxHp = 10; public int facing = 1; public bool dead; public float invuln;
    public float scale = 1f;
    [System.NonSerialized] public Transform visual;
    protected SpriteRenderer shadow; protected SortingGroup group; protected SpriteRenderer[] rends;
    float flashT, flashDur; Color flashCol; MaterialPropertyBlock mpb;
    static readonly int FlashId = Shader.PropertyToID("_Flash"), FlashColId = Shader.PropertyToID("_FlashColor");

    protected virtual void OnEnable() { All.Add(this); }
    protected virtual void OnDisable() { All.Remove(this); }

    protected void BaseSetup(float shadowSize)
    {
        group = gameObject.AddComponent<SortingGroup>();
        group.sortingLayerName = "Default";
        shadow = Art.New("fx_shadow", transform, Vector3.zero, -20);
        shadow.transform.localScale = Vector3.one * shadowSize;
        visual = new GameObject("visual").transform; visual.SetParent(transform, false);
        mpb = new MaterialPropertyBlock();
    }

    protected void CacheRenderers() { rends = visual.GetComponentsInChildren<SpriteRenderer>(true); }

    public void Flash(Color c, float dur) { flashCol = c; flashT = dur; flashDur = dur; }

    protected void ApplyTransform(float extraScaleY = 1f)
    {
        transform.position = new Vector3(pos.x, pos.y, 0);
        visual.localPosition = new Vector3(0, z, 0);
        visual.localScale = new Vector3(facing * scale, scale * extraScaleY, 1);
        group.sortingOrder = Art.YOrder(pos.y);
        float s = Mathf.Clamp01(1 - z * 0.22f);
        var c = shadow.color; c.a = s; shadow.color = c;
        if (flashDur > 0)
        {
            flashT -= Time.deltaTime;
            float f = Mathf.Clamp01(flashT / flashDur);
            for (int i = 0; i < rends.Length; i++)
            {
                if (rends[i] == null) continue;
                rends[i].GetPropertyBlock(mpb); mpb.SetFloat(FlashId, f); mpb.SetColor(FlashColId, flashCol); rends[i].SetPropertyBlock(mpb);
            }
            if (flashT <= 0) flashDur = 0;
        }
    }

    protected void SetAlpha(float a)
    {
        for (int i = 0; i < rends.Length; i++) if (rends[i] != null) { var c = rends[i].color; c.a = a; rends[i].color = c; }
    }

    protected void Physics(float dt)
    {
        pos += kb * dt; kb *= Mathf.Exp(-9f * dt);
        if (z > 0 || vz > 0)
        {
            vz -= Gravity * dt; z += vz * dt;
            if (z <= 0) { float v = vz; z = 0; vz = 0; OnLand(v); }
        }
        World.Constrain(this);
    }

    protected virtual void OnLand(float v) { }
    public virtual bool TakeHit(Hit h) { return false; }
    public Vector2 Center => new Vector2(pos.x, pos.y + z + 0.7f * scale);
}

public class Player : Actor
{
    public enum St { Intro, Normal, Attack, Heavy, Slam, Cast, Dodge, Hurt, Dead, Win }
    public St st = St.Intro;
    public int mana = 3; public const int MaxMana = 3;
    public int comboHits; public float comboHitsT; public int maxCombo; public int kills;
    public bool usedJump, usedDodge, usedAttack, usedHeavy, usedMagic;
    float t; int combo; bool hitDone, air; float comboReset; string buf = ""; float bufT; int airSwings;
    Vector2 vel, dodgeDir; float dodgeCd, manaRegen; int hitsForMana;
    Rig rig; float walkPhase, lastStep, spinCur, introT, blink;

    static readonly float[] Dur = { 0.26f, 0.26f, 0.30f, 0.46f }, HitT = { 0.07f, 0.07f, 0.09f, 0.15f }, Range = { 1.35f, 1.35f, 1.55f, 1.75f }, Knock = { 3f, 3f, 4.5f, 10f }, Stop = { 0.045f, 0.045f, 0.055f, 0.11f };
    static readonly int[] Dmg = { 7, 7, 9, 15 };

    public static Player Create(Vector2 at)
    {
        var go = new GameObject("Player"); var p = go.AddComponent<Player>();
        p.pos = at; p.maxHp = p.hp = 100; p.radius = 0.32f;
        p.BaseSetup(1.0f); p.rig = Rig.Knight(p.visual); p.CacheRenderers();
        var glow = Art.New("fx_glow", p.rig.torso, new Vector3(0.04f, 0.32f, 0), 8, "Default", true);
        glow.color = new Color(1, 0.6f, 0.2f, 0.35f); glow.transform.localScale = Vector3.one * 0.5f;
        p.ApplyTransform();
        return p;
    }

    public void Begin() { if (st == St.Intro) introT = 0.9f; }
    public void SkipIntro() { st = St.Normal; }
    public void Celebrate() { if (!dead) { st = St.Win; t = 0; } }
    public bool Busy => st != St.Normal;
    public bool Dodging => st == St.Dodge;

    void Update()
    {
        float dt = Time.deltaTime; if (dt <= 0) return;
        rig.rate = 30f;
        if (invuln > 0) invuln -= dt;
        if (dodgeCd > 0) dodgeCd -= dt;
        if (comboReset > 0) { comboReset -= dt; if (comboReset <= 0) combo = 0; }
        if (comboHitsT > 0) { comboHitsT -= dt; if (comboHitsT <= 0) comboHits = 0; }
        if (mana < MaxMana) { manaRegen += dt; if (manaRegen > 11f) { manaRegen = 0; GainMana(); } }
        bool playing = Game.I.state == Game.State.Playing;
        if (playing && st != St.Dead && st != St.Intro && st != St.Win)
        {
            if (GameInput.Attack) { buf = "A"; bufT = 0.28f; }
            if (GameInput.Heavy) { buf = "H"; bufT = 0.28f; }
            if (GameInput.Magic) { buf = "M"; bufT = 0.28f; }
            if (GameInput.Jump) { buf = "J"; bufT = 0.18f; }
            if (GameInput.Dodge) { buf = "D"; bufT = 0.22f; }
        }
        if (bufT > 0) { bufT -= dt; if (bufT <= 0) buf = ""; }
        t += dt;
        Vector2 move = playing ? GameInput.Move : Vector2.zero;

        switch (st)
        {
            case St.Intro:
                vel = Vector2.zero;
                if (introT > 0) { introT -= dt; if (introT <= 0) { st = St.Normal; Fx.Dust(pos, 4); } }
                break;
            case St.Normal:
                Locomote(move, dt, 1f);
                if (buf == "J" && z <= 0.001f) { buf = ""; vz = 9.8f; z = 0.01f; airSwings = 0; usedJump = true; Sfx.Play("jump", 0.5f); Fx.Dust(pos, 3); }
                else if (buf == "D" && dodgeCd <= 0) StartDodge(move);
                else if (buf == "A") StartAttack(move);
                else if (buf == "H") StartHeavy(move);
                else if (buf == "M") StartCast(move);
                break;
            case St.Attack: TickAttack(move, dt); break;
            case St.Heavy: TickHeavy(move, dt); break;
            case St.Slam:
                vel = Vector2.zero;
                if (z <= 0 && !hitDone) { hitDone = true; SlamImpact(14, 1.9f, 6.5f); t = 0; }
                if (hitDone && t > 0.28f) st = St.Normal;
                break;
            case St.Cast: TickCast(dt); break;
            case St.Dodge:
                {
                    float p = t / 0.36f;
                    pos += dodgeDir * Mathf.Lerp(11.5f, 3.5f, p) * dt;
                    if (Random.value < dt * 30) Fx.Dust(pos, 1, 0.7f);
                    if (p >= 1) { st = St.Normal; dodgeCd = 0.12f; vel = dodgeDir * 3f; spinCur = 0; }
                    break;
                }
            case St.Hurt:
                vel = Vector2.zero;
                if (t > 0.28f) st = St.Normal;
                break;
            case St.Dead: vel = Vector2.zero; break;
            case St.Win: vel = Vector2.zero; break;
        }
        Physics(dt);
        Animate(dt);
        rig.Apply(dt);
        ApplyTransform();
        if (invuln > 0 && st != St.Dodge && !dead) SetAlpha(Mathf.Sin(Time.time * 50f) > 0 ? 1f : 0.45f); else SetAlpha(1f);
    }

    void Locomote(Vector2 move, float dt, float mul)
    {
        Vector2 target = new Vector2(move.x * 4.9f, move.y * 3.4f) * mul;
        vel = Vector2.Lerp(vel, target, 1 - Mathf.Exp(-18f * dt));
        pos += vel * dt;
        if (Mathf.Abs(move.x) > 0.1f) facing = move.x > 0 ? 1 : -1;
    }

    void Face(Vector2 move) { if (Mathf.Abs(move.x) > 0.1f) facing = move.x > 0 ? 1 : -1; }

    void StartDodge(Vector2 move)
    {
        buf = ""; st = St.Dodge; t = 0; usedDodge = true;
        dodgeDir = move.sqrMagnitude > 0.05f ? move.normalized : new Vector2(facing, 0);
        dodgeDir.y *= 0.7f; Face(move);
        if (z > 0) vz = Mathf.Min(vz, 0);
        Sfx.Play("dodge", 0.55f); Fx.Dust(pos, 5);
    }

    void StartAttack(Vector2 move)
    {
        buf = ""; Face(move); usedAttack = true;
        air = z > 0.05f;
        if (air)
        {
            if (airSwings >= 3) return;
            airSwings++; vz = Mathf.Max(vz, 3.2f); combo = airSwings % 2;
        }
        st = St.Attack; t = 0; hitDone = false; comboReset = 0;
        if (!air) { kb = new Vector2(facing * (combo == 3 ? 5.5f : 3.2f), move.y * 1.5f); if (combo == 3) { vz = 4.5f; z = 0.01f; } }
        Sfx.Play(combo == 3 ? "swing_heavy" : "swing" + (1 + combo % 3), 0.6f);
    }

    void TickAttack(Vector2 move, float dt)
    {
        int c = air ? combo : Mathf.Min(combo, 3);
        float dur = air ? 0.24f : Dur[c], hitT = air ? 0.06f : HitT[c];
        vel = Vector2.Lerp(vel, Vector2.zero, 1 - Mathf.Exp(-14f * dt));
        if (air) pos += new Vector2(move.x * 3.2f, move.y * 2.2f) * dt;
        if (!hitDone && t >= hitT)
        {
            hitDone = true;
            var h = new Hit { dmg = air ? 6 : Dmg[c], knock = air ? 2.5f : Knock[c], launch = air ? 5.5f : (c == 3 ? 6.5f : 0), stun = 0.4f, dir = facing, heavy = c == 3 && !air, from = this };
            int n = Strike(air ? 1.4f : Range[c], 0.8f, 0.45f, h, air ? z - 1.3f : -1f, air ? z + 1.6f : 1.5f);
            SlashFx(c, air);
            if (n > 0) { Game.I.HitStop(air ? 0.04f : Stop[c]); CameraRig.I.Shake(c == 3 ? 0.22f : 0.08f, c == 3 ? 0.25f : 0.12f); }
            if (c == 3 && !air) { Fx.Ring(pos + new Vector2(facing * 1.1f, 0), 2.6f, new Color(1, 0.95f, 0.8f, 0.8f)); Fx.Dust(pos + new Vector2(facing * 1.1f, 0), 6, 1.2f, 2); CameraRig.I.Shake(0.12f, 0.15f); }
        }
        bool canCancel = hitDone && t >= hitT + 0.09f;
        if (canCancel && buf == "D" && dodgeCd <= 0) { StartDodge(move); return; }
        if (canCancel && buf == "H" && !air) { StartHeavy(move); return; }
        if (canCancel && buf == "M") { StartCast(move); return; }
        if (canCancel && buf == "A" && (air || combo < 3))
        {
            if (!air) combo++;
            StartAttack(move); return;
        }
        if (t >= dur)
        {
            st = St.Normal;
            if (!air) { if (combo >= 3) combo = 0; else { combo++; comboReset = 0.42f; } }
        }
    }

    void StartHeavy(Vector2 move)
    {
        buf = ""; Face(move); usedHeavy = true; combo = 0; hitDone = false; t = 0;
        if (z > 0.3f) { st = St.Slam; vz = -22f; Sfx.Play("swing_heavy", 0.6f); return; }
        st = St.Heavy; kb = new Vector2(facing * 2.5f, 0);
        Sfx.Play("swing_heavy", 0.7f, 0.05f, 0.9f);
    }

    void TickHeavy(Vector2 move, float dt)
    {
        vel = Vector2.zero;
        if (!hitDone && t >= 0.22f)
        {
            hitDone = true;
            var h = new Hit { dmg = 16, knock = 3.5f, launch = 10.5f, stun = 0.6f, dir = facing, heavy = true, from = this };
            int n = Strike(1.8f, 0.9f, 0.5f, h, -1f, 1.8f);
            SlashFx(4, false);
            Vector2 gp = pos + new Vector2(facing * 1.15f, 0);
            Fx.Ring(gp, 3.2f, new Color(1, 0.8f, 0.5f, 0.9f)); Fx.Dust(gp, 8, 1.3f, 2.2f);
            CameraRig.I.Shake(n > 0 ? 0.3f : 0.16f, 0.28f); Sfx.Play("boom", 0.45f, 0.05f, 1.3f);
            if (n > 0) Game.I.HitStop(0.12f);
        }
        if (hitDone && t > 0.36f && buf == "D" && dodgeCd <= 0) { StartDodge(move); return; }
        if (hitDone && t > 0.40f && buf == "J") { st = St.Normal; return; }
        if (t >= 0.56f) st = St.Normal;
    }

    void SlamImpact(int dmg, float radius, float launch)
    {
        int n = 0;
        for (int i = All.Count - 1; i >= 0; i--)
        {
            var e = All[i] as Enemy; if (e == null || e.dead) continue;
            Vector2 d = e.pos - pos; d.y *= 1.8f;
            if (d.magnitude > radius + e.radius) continue;
            var h = new Hit { dmg = dmg, knock = 5f, launch = launch, stun = 0.6f, dir = d.x >= 0 ? 1 : -1, heavy = true, from = this };
            if (e.TakeHit(h)) { n++; OnLanded(e); }
        }
        Fx.Ring(pos, radius * 2.2f, new Color(1, 0.8f, 0.5f, 0.9f)); Fx.Ring(pos, radius * 1.4f, new Color(1, 1, 1, 0.7f), 0.22f);
        Fx.Dust(pos, 10, 1.3f, 2.5f); CameraRig.I.Shake(0.35f, 0.3f); Sfx.Play("boom", 0.6f, 0.05f, 1.15f);
        if (n > 0) Game.I.HitStop(0.12f);
    }

    void StartCast(Vector2 move)
    {
        buf = ""; Face(move); usedMagic = true;
        if (mana <= 0) { Sfx.Play("tele", 0.4f, 0, 0.6f); Fx.Emit("fx_puff", Center + new Vector2(facing * 0.5f, 0), new Vector2(0, 1), 0.3f, 0.3f, 0.6f, new Color(0.5f, 0.5f, 0.5f, 0.7f), new Color(0.5f, 0.5f, 0.5f, 0)); Game.I.hud.PulseMana(); return; }
        st = St.Cast; t = 0; hitDone = false; mana--; manaRegen = 0; combo = 0;
        Sfx.Play("cast", 0.7f);
    }

    void TickCast(float dt)
    {
        vel = Vector2.zero;
        if (z > 0) vz = Mathf.Max(vz, -2f);
        if (t < 0.16f && Random.value < dt * 60) Fx.Flames(Center + new Vector2(facing * 0.45f, 0.0f), 1, 0.5f);
        if (!hitDone && t >= 0.16f)
        {
            hitDone = true; kb = new Vector2(-facing * 3f, 0);
            EmberWave.Spawn(pos + new Vector2(facing * 0.6f, 0), facing, z);
            CameraRig.I.Shake(0.14f, 0.2f); Sfx.Play("boom", 0.5f, 0.05f, 1.5f);
        }
        if (t >= 0.42f) st = St.Normal;
    }

    int Strike(float range, float depth, float back, Hit h, float zLo, float zHi)
    {
        int n = 0;
        for (int i = All.Count - 1; i >= 0; i--)
        {
            var e = All[i] as Enemy; if (e == null || e.dead) continue;
            float dx = (e.pos.x - pos.x) * facing;
            if (dx < -back || dx > range + e.radius) continue;
            if (Mathf.Abs(e.pos.y - pos.y) > depth + e.radius * 0.5f) continue;
            if (e.z < zLo || e.z > zHi) continue;
            if (e.TakeHit(h)) { n++; OnLanded(e); }
        }
        return n;
    }

    public void OnLanded(Enemy e)
    {
        comboHits++; comboHitsT = 2.2f; if (comboHits > maxCombo) maxCombo = comboHits;
        hitsForMana++; if (hitsForMana >= 9) { hitsForMana = 0; GainMana(); }
    }

    public void GainMana()
    {
        if (mana >= MaxMana) return;
        mana++; Sfx.Play("mana", 0.4f); Game.I.hud.PulseMana();
    }

    public void Heal(int amount)
    {
        hp = Mathf.Min(maxHp, hp + amount); Flash(new Color(0.6f, 1f, 0.6f), 0.3f);
        for (int i = 0; i < 6; i++) Fx.Emit("ui_heart", Center + Random.insideUnitCircle * 0.5f, new Vector2(Random.Range(-0.5f, 0.5f), Random.Range(1f, 2.2f)), 0.7f, 0.5f, 0.2f, Color.white, new Color(1, 1, 1, 0));
    }

    void SlashFx(int c, bool airborne)
    {
        // crescent arcs parented to the visual so they follow the lunge and mirror with facing
        Color white = Color.white, clear = new Color(1, 1, 1, 0), orange = new Color(1, 0.75f, 0.4f, 1);
        if (airborne) { Fx.Emit("fx_slash", new Vector2(0.75f, 0.7f), Vector2.zero, 0.13f, 1.0f, 1.25f, white, clear, c == 0 ? -25 : 25, 0, 0, false, 30, -999, 0, visual, 1, c == 0 ? 1 : -1); return; }
        switch (c)
        {
            case 0: Fx.Emit("fx_slash", new Vector2(0.8f, 0.72f), Vector2.zero, 0.13f, 1.05f, 1.3f, white, clear, -22, 0, 0, false, 30, -999, 0, visual, 1, 1); break;
            case 1: Fx.Emit("fx_slash", new Vector2(0.8f, 0.78f), Vector2.zero, 0.13f, 1.05f, 1.3f, white, clear, 24, 0, 0, false, 30, -999, 0, visual, 1, -1); break;
            case 2:
                Fx.Emit("fx_slash", new Vector2(0.75f, 0.62f), Vector2.zero, 0.15f, 1.3f, 1.6f, white, clear, 0, 0, 0, false, 30, -999, 0, visual, 1.15f, 0.5f);
                Fx.Emit("fx_slash", new Vector2(-0.6f, 0.66f), Vector2.zero, 0.15f, 1.1f, 1.4f, new Color(1, 1, 1, 0.6f), clear, 180, 0, 0, false, -5, -999, 0, visual, 1.1f, 0.45f);
                break;
            case 3: Fx.Emit("fx_slash_big", new Vector2(0.95f, 0.85f), Vector2.zero, 0.17f, 1.3f, 1.65f, white, clear, -30, 0, 0, false, 30, -999, 0, visual, 1, 1); break;
            default: Fx.Emit("fx_slash_big", new Vector2(1.0f, 0.9f), Vector2.zero, 0.2f, 1.4f, 1.8f, orange, new Color(1, 0.5f, 0.2f, 0), -35, 0, 0, false, 30, -999, 0, visual, 1, 1); break;
        }
    }

    protected override void OnLand(float v)
    {
        if (st == St.Slam) return;
        Fx.Dust(pos, 3, 0.8f); Sfx.Play("land", 0.35f);
    }

    public override bool TakeHit(Hit h)
    {
        if (dead || invuln > 0 || st == St.Dodge || st == St.Intro || st == St.Win) return false;
        if (Game.I.state != Game.State.Playing) return false;
        hp -= h.dmg; Flash(new Color(1, 0.3f, 0.25f), 0.25f);
        kb = new Vector2(h.dir * h.knock, 0); if (h.launch > 0) { vz = h.launch; z = Mathf.Max(z, 0.01f); }
        CameraRig.I.Shake(0.25f, 0.25f); Game.I.HitStop(0.08f); Sfx.Play("hurt", 0.7f);
        Fx.HitSpark(Center, 0.8f, new Color(1, 0.5f, 0.4f));
        comboHits = 0; combo = 0;
        if (hp <= 0) { hp = 0; dead = true; st = St.Dead; t = 0; vz = 6; z = 0.01f; kb = new Vector2(h.dir * 5f, 0); Game.I.OnPlayerDead(); }
        else { st = St.Hurt; t = 0; invuln = 0.9f; }
        return true;
    }

    static float Ease(float x) { x = Mathf.Clamp01(x); return x * x * (3 - 2 * x); }

    void Animate(float dt)
    {
        var r = rig; float tt = Time.time; float br = Mathf.Sin(tt * 2.6f);
        r.aSpin = 0; r.spinOff = Vector2.zero; r.squash = Vector2.one; r.aBody = 0; r.off = Vector2.zero; r.aHead = 0; r.aArmB = -8;
        r.aCape = -6 + Mathf.Sin(tt * 3.1f) * 6 - Mathf.Clamp(Mathf.Abs(vel.x) * 7f, 0, 45) - Mathf.Clamp(-vz * 3f, -20, 50);
        r.aLegF = 0; r.aLegB = 0;
        switch (st)
        {
            case St.Intro:
                {
                    float k = introT > 0 ? Ease(1 - introT / 0.9f) : 0;
                    r.aSpin = Mathf.Lerp(88, 0, k); r.spinOff = new Vector2(Mathf.Lerp(-0.2f, 0, k), Mathf.Lerp(-0.42f, 0, k));
                    r.aArmF = Mathf.Lerp(60, 12, k); r.aWeapon = Mathf.Lerp(-150, -55, k); r.aHead = Mathf.Lerp(-10, 0, k); r.aCape = -30;
                    break;
                }
            case St.Normal:
                if (z > 0.02f)
                {
                    bool up = vz > 0;
                    r.aLegF = up ? 40 : 10; r.aLegB = up ? -30 : -12; r.aArmF = up ? 40 : 110; r.aWeapon = up ? -40 : -20; r.aArmB = up ? -40 : -80;
                    r.squash = up ? new Vector2(0.92f, 1.1f) : new Vector2(1.02f, 0.98f); r.aBody = up ? -6 : 4;
                }
                else if (vel.magnitude > 0.6f)
                {
                    walkPhase += dt * (5.5f + vel.magnitude * 1.9f);
                    float s = Mathf.Sin(walkPhase);
                    r.aLegF = s * 42; r.aLegB = -s * 42; r.aArmB = s * 34; r.aArmF = 14 - s * 16; r.aWeapon = -62 - s * 6;
                    r.off = new Vector2(0, Mathf.Abs(Mathf.Cos(walkPhase)) * 0.05f); r.aBody = -7; r.aHead = 2;
                    if (Mathf.Floor(walkPhase / Mathf.PI) != lastStep) { lastStep = Mathf.Floor(walkPhase / Mathf.PI); Fx.Dust(pos - new Vector2(facing * 0.2f, 0), 1, 0.55f); Sfx.Play("step", 0.25f, 0.2f); }
                }
                else
                {
                    r.aArmF = 12 + br * 3; r.aWeapon = -55 + br * 2.5f; r.aArmB = -6 - br * 2; r.off = new Vector2(0, br * 0.014f); r.aHead = br * 1.5f;
                }
                break;
            case St.Attack:
                {
                    int c = air ? combo : Mathf.Min(combo, 3); float hitT = air ? 0.06f : HitT[c], dur = air ? 0.24f : Dur[c];
                    float a0, w0, a1, w1;
                    if (air) { if (c == 0) { a0 = 200; w0 = 40; a1 = 30; w1 = -140; } else { a0 = 10; w0 = -160; a1 = 170; w1 = 0; } }
                    else if (c == 0) { a0 = 205; w0 = 45; a1 = 35; w1 = -135; }
                    else if (c == 1) { a0 = 15; w0 = -155; a1 = 175; w1 = 5; }
                    else if (c == 2) { a0 = 150; w0 = 80; a1 = 70; w1 = -100; }
                    else { a0 = 225; w0 = 60; a1 = 25; w1 = -145; }
                    r.rate = 45;
                    float w = 0.035f;
                    if (t < hitT - w) { r.aArmF = a0; r.aWeapon = w0; r.aBody = 8; r.squash = new Vector2(0.96f, 1.04f); }
                    else if (t < hitT + w) { float k = (t - (hitT - w)) / (2 * w); r.SnapArm(Mathf.Lerp(a0, a1, k), Mathf.Lerp(w0, w1, k)); r.aBody = -14; r.off = new Vector2(0.06f, 0); }
                    else
                    {
                        float k = Ease((t - hitT - w) / Mathf.Max(0.01f, dur - hitT - w));
                        r.aArmF = Mathf.Lerp(a1, 20, k * 0.5f); r.aWeapon = Mathf.Lerp(w1, -60, k * 0.5f); r.aBody = Mathf.Lerp(-14, 0, k); r.off = new Vector2(Mathf.Lerp(0.06f, 0, k), 0);
                        if (c == 2) r.squash = new Vector2(1.08f, 0.94f);
                        if (c == 3) r.squash = new Vector2(1.1f, 0.9f);
                    }
                    r.aArmB = -50; r.aLegF = 22; r.aLegB = -26;
                    break;
                }
            case St.Heavy:
                {
                    r.rate = 40;
                    if (t < 0.18f) { float k = t / 0.18f; r.aArmF = 230; r.aWeapon = 70; r.aBody = 12; r.squash = new Vector2(1 + 0.1f * k, 1 - 0.14f * k); r.aArmB = 60; }
                    else if (t < 0.26f) { float k = (t - 0.18f) / 0.08f; r.SnapArm(Mathf.Lerp(230, 20, k), Mathf.Lerp(70, -150, k)); r.aBody = -18; r.squash = new Vector2(0.94f, 1.08f); r.off = new Vector2(0.1f, 0); }
                    else { float k = Ease((t - 0.26f) / 0.3f); r.aArmF = 20; r.aWeapon = -150 + 60 * k; r.aBody = -18 + 18 * k; r.squash = new Vector2(1.08f - 0.08f * k, 0.92f + 0.08f * k); }
                    r.aLegF = 28; r.aLegB = -30;
                    break;
                }
            case St.Slam:
                r.aArmF = hitDone ? 20 : 190; r.aWeapon = hitDone ? -150 : -180; r.aBody = hitDone ? -16 : -30; r.aLegF = 40; r.aLegB = 30; r.aArmB = 120;
                r.squash = hitDone ? new Vector2(1.14f, 0.86f) : new Vector2(0.9f, 1.12f);
                break;
            case St.Cast:
                {
                    bool pre = t < 0.16f;
                    r.aArmB = pre ? -60 : 95; r.aArmF = pre ? 40 : -30; r.aWeapon = pre ? -40 : -120; r.aBody = pre ? 10 : -12; r.aLegF = 18; r.aLegB = -22;
                    r.squash = pre ? new Vector2(1.05f, 0.95f) : new Vector2(0.97f, 1.03f); r.rate = 40;
                    break;
                }
            case St.Dodge:
                {
                    float p = Mathf.Clamp01(t / 0.36f);
                    r.aSpin = -360f * Ease(p); r.squash = new Vector2(0.82f, 0.82f); r.spinOff = new Vector2(0, -0.12f + Mathf.Sin(p * Mathf.PI) * 0.18f);
                    r.aLegF = 70; r.aLegB = 50; r.aArmF = 60; r.aArmB = 60; r.aWeapon = -20; r.aHead = 10; r.aCape = -60;
                    break;
                }
            case St.Hurt:
                r.aBody = 16; r.aHead = 14; r.aArmF = -30; r.aWeapon = -90; r.aArmB = -70; r.aLegF = -16; r.aLegB = 20; r.squash = new Vector2(0.95f, 1.05f);
                break;
            case St.Dead:
                spinCur = Mathf.Lerp(spinCur, 90, 1 - Mathf.Exp(-10 * dt));
                r.aSpin = spinCur; r.spinOff = new Vector2(-0.1f, -0.42f * spinCur / 90f);
                r.aArmF = -50; r.aWeapon = -120; r.aArmB = -60; r.aLegF = -20; r.aLegB = 15; r.aHead = 12;
                break;
            case St.Win:
                {
                    float hop = Mathf.Abs(Mathf.Sin(t * 5f)) * 0.12f;
                    r.aArmF = 172; r.aWeapon = 4; r.aArmB = 150; r.off = new Vector2(0, hop); r.aHead = -6; r.aLegF = hop * 100; r.aLegB = -hop * 100;
                    break;
                }
        }
    }
}

/// The knight's magic: a wave of ember fire that rolls forward through the swarm.
public class EmberWave : MonoBehaviour
{
    Vector2 pos; int dir; float life, h; SpriteRenderer sr, glow; readonly HashSet<Actor> hit = new HashSet<Actor>();

    public static void Spawn(Vector2 at, int dir, float height)
    {
        var go = new GameObject("EmberWave"); var w = go.AddComponent<EmberWave>();
        w.pos = at; w.dir = dir; w.h = Mathf.Min(height, 1.2f);
        w.sr = Art.New("fx_wave", go.transform, new Vector3(0, 0.7f + w.h, 0), 0); w.sr.flipX = dir < 0;
        w.glow = Art.New("fx_glow", go.transform, new Vector3(0, 0.6f + w.h, 0), 0, "Default", true);
        w.glow.color = new Color(1, 0.55f, 0.2f, 0.55f); w.glow.transform.localScale = Vector3.one * 3.2f;
        go.transform.position = at;
    }

    void Update()
    {
        float dt = Time.deltaTime; if (dt <= 0) return;
        life += dt; pos.x += dir * 12.5f * dt;
        transform.position = pos;
        float s = 1.0f + Mathf.Sin(life * 40) * 0.08f + life * 0.5f;
        sr.transform.localScale = new Vector3(s, s, 1); sr.sortingOrder = Art.YOrder(pos.y) + 5; glow.sortingOrder = sr.sortingOrder + 1;
        if (Random.value < dt * 70) Fx.Flames(pos + new Vector2(-dir * 0.4f, 0.5f + h + Random.Range(-0.3f, 0.3f)), 1, 0.55f);
        if (Random.value < dt * 40) Fx.Emit("fx_soft", pos + new Vector2(0, 0.1f), new Vector2(-dir * 2, Random.Range(1f, 3f)), 0.4f, 0.2f, 0.02f, new Color(1, 0.8f, 0.3f), new Color(1, 0.3f, 0.1f, 0), 0, 0, 0, true);
        for (int i = Actor.All.Count - 1; i >= 0; i--)
        {
            var e = Actor.All[i] as Enemy; if (e == null || e.dead || hit.Contains(e)) continue;
            if (Mathf.Abs(e.pos.x - pos.x) > 0.8f + e.radius || Mathf.Abs(e.pos.y - pos.y) > 1.0f + e.radius * 0.5f) continue;
            hit.Add(e);
            var hh = new Hit { dmg = 20, knock = 6f, launch = 5f, stun = 0.7f, dir = dir, heavy = true, from = Game.I.player };
            if (e.TakeHit(hh)) { Game.I.player.OnLanded(e); Fx.Flames(e.Center, 5, 0.9f); Game.I.HitStop(0.03f); }
        }
        float a = Mathf.Clamp01((0.75f - life) / 0.15f);
        sr.color = new Color(1, 1, 1, a);
        if (life > 0.75f || World.OutOfView(pos.x, 2f)) { Fx.Flames(pos + new Vector2(0, 0.6f), 4, 0.8f); Destroy(gameObject); }
    }
}
