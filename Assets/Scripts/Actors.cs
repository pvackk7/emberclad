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
    // Skull face swap for skeletons (only when a hurt face sprite exists).
    public SpriteRenderer headSr; public Sprite headNormal, headHurt;
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
        r.cape = Part(r.body, "k_cape", new Vector2(-0.14f, 0.53f), 0);
        r.armB = Part(r.body, "k_arm", new Vector2(0.20f, 0.50f), 1);
        r.legB = Part(r.spin, "k_leg", new Vector2(0.13f, -0.13f), 2);
        r.legF = Part(r.spin, "k_leg", new Vector2(-0.13f, -0.13f), 3);
        r.torso = Part(r.body, "k_torso", new Vector2(0, -0.04f), 4);
        r.head = Part(r.body, "k_head", new Vector2(0.01f, 0.55f), 5);
        r.armF = Part(r.body, "k_arm", new Vector2(-0.19f, 0.50f), 7);
        r.weapon = Part(r.armF, "k_sword", new Vector2(0, -0.43f), 6);
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
        r.headSr = r.head.GetComponent<SpriteRenderer>(); r.headNormal = r.headSr.sprite;
        r.headHurt = Art.TryS(redEyes ? "s_head_red_hurt" : "s_head_hurt");
        r.jaw = Part(r.head, "s_jaw", new Vector2(-0.03f, 0.03f), 5);
        // The skull runs -0.073 to 0.447 around the head pivot and the helm's own pivot
        // sits 13% up its height. 0.46 left it hovering clear of the bone; 0.25 dropped
        // the brim over the eye sockets and swallowed the face. 0.34 puts the brim about
        // seven tenths up the skull, which is where a helmet actually sits.
        if (helm) Part(r.head, "s_helm", new Vector2(0f, 0.34f), 7);
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
    public Vector2 WeaponTip(float len) { return World.Unproject(weapon.TransformPoint(new Vector3(0, len, 0))); }
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
        transform.position = World.Project(pos);
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
    public enum St { Intro, Normal, Attack, Heavy, Slam, Cast, Dodge, Hurt, Dead, Win, Drink, Guard }
    public St st = St.Intro;
    public const float MaxStamina = 100, LightCost = 18, HeavyCost = 34, RollCost = 28;
    public int potions = 3;
    public bool CanDrink => !dead && st == St.Normal && z <= .01f && potions > 0 && hp < maxHp;
    public bool TryDrink()
    {
        if (!CanDrink) return false;
        st = St.Drink; t = 0; hitDone = false; buf = ""; vel = Vector2.zero;
        return true;
    }
    public float stamina = MaxStamina;
    float staminaDelay;
    public bool Attacking => st == St.Attack || st == St.Heavy;
    public bool RollProtected => st == St.Dodge && t >= .02f && t < .28f;
    public bool SpendStamina(float amount)
    {
        if (stamina < amount) { buf = ""; return false; }
        stamina -= amount; staminaDelay = .8f; return true;
    }
    public void Parried(int direction)
    {
        if (dead || st == St.Win) return;
        stamina = Mathf.Max(0, stamina - 12); staminaDelay = 1;
        st = St.Hurt; t = -.12f; buf = ""; combo = 0; spinCur = 0;
        kb = new Vector2(direction * 3, 0); vel = Vector2.zero;
        Fx.HitSpark(Center, .7f, new Color(.75f, .85f, 1));
    }
    /// An Ember Burst is paid for in blood, not charges: one eighth of the knight's
    /// maximum, and never the last of it. A full bar is therefore worth exactly
    /// seven casts, and the eighth is refused rather than fatal.
    public int EmberCost { get { return Mathf.Max(1, Mathf.CeilToInt(maxHp / 8f)); } }
    public bool HasEmbers { get { return Game.I != null && Game.I.HasEmbers; } }
    public bool CanCast { get { return !dead && HasEmbers && hp > EmberCost; } }
    public int comboHits; public float comboHitsT; public int maxCombo; public int kills;
    public bool usedJump, usedDodge, usedAttack, usedHeavy, usedMagic;
    float t; int combo; bool hitDone, air; float comboReset; string buf = ""; float bufT; int airSwings;
    Vector2 vel, dodgeDir; float dodgeCd;
    Rig rig; SpriteRenderer drinkBottle, emberOrb, emberOrbCore; float walkPhase, lastStep, spinCur, introT;
    Vector3 bottleBaseScale, orbBaseScale; int lastQuickSelected; float swapT = 999f; bool potionOut, emberOut;
    SpriteRenderer swordSprite, shieldSprite; bool grabDone = true;
    /// Fraction of incoming damage the recovered shield soaks.
    public const float ShieldSoak = .25f;
    /// Stamina a blocked hit costs while holding guard; a heavy blow costs more but still lands for nothing.
    public const float BlockCost = 14f, HeavyBlockCost = 26f;
    /// Raise the shield and block within this window of a hit landing: a free parry that staggers the attacker instead of costing stamina.
    public const float ParryWindow = .16f;
    // The shield hangs off the back arm, so when he faces right it sits behind the
    // torso. Mirroring him to face left puts that arm nearest the viewer, so the
    // shield has to come forward to in front of everything, sword included.
    const int ShieldBackOrder = 2, ShieldFrontOrder = 9;
    public bool Guarding => st == St.Guard;
    /// Beat of a hotbar swap at which the reaching fist closes on the new item.
    const float GrabAt = .13f;
    // ---- held-flask tuning. These four are the whole pose; nudge and re-play. ----
    const float FlaskY = -.58f;    // how far down the arm it hangs. Less negative = higher up the forearm.
    const float FlaskTilt = -8f;   // resting lean in degrees. 0 = dead upright.
    const float HoldArm = 18f;     // arm angle while carrying. ~12 is the idle sword arm, so this is "at his side".
    const int FlaskOrder = 6;      // same layer as the sword: behind the arm, so the gauntlet closes over it.
    const float OrbY = -.60f;      // the ember rides a touch lower than the flask, out past the knuckles.
    // In front of everything, the raised shield (9) included: a light source held in
    // the fist has nothing in the rig that should occlude it.
    const int OrbOrder = 10;

    static readonly float[] Dur = { 0.26f, 0.26f, 0.30f, 0.46f }, HitT = { 0.07f, 0.07f, 0.09f, 0.15f }, Range = { 1.35f, 1.35f, 1.55f, 1.75f }, Knock = { 3f, 3f, 4.5f, 10f }, Stop = { 0.045f, 0.045f, 0.055f, 0.11f };
    static readonly int[] Dmg = { 7, 7, 9, 15 };

    public static Player Create(Vector2 at)
    {
        var go = new GameObject("Player"); var p = go.AddComponent<Player>();
        p.pos = at; p.maxHp = p.hp = 100; p.radius = 0.32f;
        p.scale = 1.3f;
        p.BaseSetup(1.3f); p.rig = Rig.Knight(p.visual);
        // The flask hangs off the same fist the sword does -- one hand, one item --
        // so a swap reads as the knight genuinely trading one for the other rather
        // than sprouting a second arm's worth of gear.
        p.drinkBottle = Art.New("ui_potion", p.rig.armF, new Vector3(0, FlaskY, 0), FlaskOrder);
        p.drinkBottle.transform.localScale = Vector3.one * (.35f / p.drinkBottle.sprite.bounds.size.y);
        p.bottleBaseScale = p.drinkBottle.transform.localScale;
        p.drinkBottle.enabled = false;
        // Embers show as a live coal held in the same fist: an additive glow with a
        // hotter core inside it, both counter-rotated so they stay put as the arm swings.
        p.emberOrb = Art.New("fx_glow", p.rig.armF, new Vector3(0, OrbY, 0), OrbOrder, "Default", true);
        p.emberOrb.transform.localScale = Vector3.one * (.46f / p.emberOrb.sprite.bounds.size.y);
        p.orbBaseScale = p.emberOrb.transform.localScale;
        p.emberOrb.color = new Color(1f, .44f, .10f, .70f);
        p.emberOrb.enabled = false;
        p.emberOrbCore = Art.New("fx_glow", p.emberOrb.transform, Vector3.zero, OrbOrder + 1, "Default", true);
        p.emberOrbCore.transform.localScale = Vector3.one * .40f;
        p.emberOrbCore.color = new Color(1f, .93f, .64f, .92f);
        p.emberOrbCore.enabled = false;
        p.swordSprite = p.rig.weapon.GetComponent<SpriteRenderer>();
        p.lastQuickSelected = QuickItems.Selected;
        if (Game.I != null && Game.I.HasShield) p.EquipShield();
        p.CacheRenderers();
        var glow = Art.New("fx_glow", p.rig.torso, new Vector3(0.04f, 0.32f, 0), 8, "Default", true);
        glow.color = new Color(1, 0.6f, 0.2f, 0.35f); glow.transform.localScale = Vector3.one * 0.5f;
        p.ApplyTransform();
        return p;
    }

    /// Straps the recovered shield to the off-hand, where it stays. The sword hand
    /// is already spoken for by the sword-or-flask swap, so this arm was the free one.
    public void EquipShield()
    {
        if (shieldSprite != null) return;
        shieldSprite = Art.New("s_shield", rig.armB, new Vector3(0, -.34f, 0), 2);
        shieldSprite.transform.localScale = Vector3.one * 1.05f;
    }

    public void Begin() { if (st == St.Intro) introT = 0.9f; }
    public void SkipIntro() { st = St.Normal; }
    public void Celebrate() { if (!dead) { st = St.Win; t = 0; } }
    public bool Busy => st != St.Normal;
    public bool Dodging => st == St.Dodge;
    public Vector2 RollDirection => dodgeDir;

    void Update()
    {
        float dt = Time.deltaTime; if (dt <= 0) return;
        rig.rate = 30f;
        if (QuickItems.Selected != lastQuickSelected) { lastQuickSelected = QuickItems.Selected; swapT = 0; grabDone = false; }
        swapT += dt;
        // For the first beat of a swap the fist is empty, because he is reaching
        // down to his belt. Hiding both items until it closes is what sells the
        // grab -- otherwise the new one just teleports into his hand.
        bool reaching = swapT < GrabAt;
        if (!grabDone && !reaching)
        {
            grabDone = true;
            Sfx.Play("pickup", .3f);
            Fx.Dust(pos + new Vector2(facing * .18f, -.1f), 2, .45f);
        }
        // Mid-swing the sword is the only thing that belongs in his hand, so the
        // flask waits until he is back on his feet. Attacking with the potion
        // equipped therefore draws the blade instead of swinging thin air.
        potionOut = QuickItems.Selected == 1 && (st == St.Normal || st == St.Hurt);
        emberOut = QuickItems.Selected == 2 && (st == St.Normal || st == St.Hurt);
        bool flask = (st == St.Drink && t < .65f) || (potionOut && !reaching);
        bool orb = st == St.Cast || (emberOut && !reaching);
        drinkBottle.enabled = flask;
        if (emberOrb != null) { emberOrb.enabled = orb; emberOrbCore.enabled = orb; }
        if (swordSprite != null) swordSprite.enabled = !flask && !orb && !reaching && st != St.Drink;
        PoseOrb(dt);
        if (shieldSprite != null) shieldSprite.sortingOrder = facing < 0 || st == St.Guard ? ShieldFrontOrder : ShieldBackOrder;
        if (invuln > 0) invuln -= dt;
        if (dodgeCd > 0) dodgeCd -= dt;
        staminaDelay = Mathf.Max(0, staminaDelay - dt);
        if (staminaDelay <= 0 && st == St.Normal) stamina = Mathf.Min(MaxStamina, stamina + 25 * dt);
        if (comboReset > 0) { comboReset -= dt; if (comboReset <= 0) combo = 0; }
        if (comboHitsT > 0) { comboHitsT -= dt; if (comboHitsT <= 0) comboHits = 0; }
        bool playing = Game.I.state == Game.State.Playing;
        if (QuickItems.BlockActions) buf = "";
        if (playing && !QuickItems.BlockActions && st != St.Drink && st != St.Dead && st != St.Intro && st != St.Win)
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
                if (playing && GameInput.UsePotion) { TryDrink(); break; }
                if (playing && !QuickItems.BlockActions && shieldSprite != null && GameInput.Guard && z <= 0.001f) { st = St.Guard; t = 0; buf = ""; break; }
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
            case St.Drink:
                vel = Vector2.zero;
                if (!hitDone && t >= .55f)
                {
                    hitDone = true; potions--; Heal(Mathf.CeilToInt(maxHp * .25f));
                    Sfx.Play("pickup", .6f);
                }
                if (t >= .85f) st = St.Normal;
                break;
            case St.Dodge:
                {
                    float p = t / 0.36f;
                    pos += dodgeDir * Mathf.Lerp(9.5f, 2.5f, Mathf.Clamp01(p)) * dt;
                    if (Random.value < dt * 30) Fx.Dust(pos, 1, 0.7f);
                    if (p >= 1) { st = St.Normal; dodgeCd = 0.30f; vel = dodgeDir * 3f; spinCur = 0; }
                    break;
                }
            case St.Hurt:
                vel = Vector2.zero;
                if (t > 0.28f) st = St.Normal;
                break;
            case St.Guard:
                // Shield up: a slow shuffle, no turning, and the roll still works as
                // the way out so a guard never traps him against a heavy.
                Locomote(move, dt, .45f, false);
                if (buf == "D" && dodgeCd <= 0) { StartDodge(move); break; }
                if (!playing || !GameInput.Guard || stamina <= 0) st = St.Normal;
                break;
            case St.Dead: vel = Vector2.zero; break;
            case St.Win: vel = Vector2.zero; break;
        }
        Physics(dt);
        Animate(dt);
        rig.Apply(dt);
        PoseFlask();
        ApplyTransform();
        if (invuln > 0 && st != St.Dodge && !dead) SetAlpha(Mathf.Sin(Time.time * 50f) > 0 ? 1f : 0.45f); else SetAlpha(1f);
    }

    void Locomote(Vector2 move, float dt, float mul, bool turn = true)
    {
        Vector2 target = new Vector2(move.x * 4.9f, move.y * 3.4f) * mul;
        vel = Vector2.Lerp(vel, target, 1 - Mathf.Exp(-18f * dt));
        pos += vel * dt;
        if (turn && Mathf.Abs(move.x) > 0.1f) facing = move.x > 0 ? 1 : -1;
    }

    void Face(Vector2 move) { if (Mathf.Abs(move.x) > 0.1f) facing = move.x > 0 ? 1 : -1; }

    void StartDodge(Vector2 move)
    {
        if (!SpendStamina(RollCost)) return;
        buf = ""; st = St.Dodge; t = 0; usedDodge = true;
        dodgeDir = move.sqrMagnitude > 0.05f ? move.normalized : new Vector2(facing, 0);
        dodgeDir.y *= 0.7f; Face(move);
        if (z > 0) vz = Mathf.Min(vz, 0);
        Sfx.Play("dodge", 0.55f); Fx.Dust(pos, 5);
    }

    void StartAttack(Vector2 move)
    {
        if (z > .05f && airSwings >= 3) { buf = ""; return; }
        if (!SpendStamina(LightCost)) return;
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
            if (st != St.Attack) return;
            SlashFx(c, air);
            if (n > 0) { Game.I.HitStop(air ? 0.04f : Stop[c]); CameraRig.I.Shake(c == 3 ? 0.22f : 0.08f, c == 3 ? 0.25f : 0.12f); }
            if (c == 3 && !air) { Fx.Ring(pos + new Vector2(facing * 1.1f, 0), 2.6f, new Color(1, 0.95f, 0.8f, 0.8f)); Fx.Dust(pos + new Vector2(facing * 1.1f, 0), 6, 1.2f, 2); CameraRig.I.Shake(0.12f, 0.15f); }
        }
        bool canCancel = hitDone && t >= hitT + 0.17f;
        if (canCancel && buf == "D" && dodgeCd <= 0) { StartDodge(move); return; }
        if (canCancel && buf == "H" && !air) { StartHeavy(move); return; }
        if (canCancel && buf == "M") { StartCast(move); return; }
        if (canCancel && buf == "A" && stamina >= LightCost && (air || combo < 3))
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
        // Coming down out of a jump is a technique, not a button. Until the forest has
        // taught it, a heavy in the air is something the knight does not know how to do,
        // and it costs nothing to try.
        bool airborne = z > 0.3f;
        if (airborne && !(Game.I != null && Game.I.HasSlam)) { buf = ""; return; }
        if (!SpendStamina(HeavyCost)) return;
        buf = ""; Face(move); usedHeavy = true; combo = 0; hitDone = false; t = 0;
        if (airborne) { st = St.Slam; vz = -22f; Sfx.Play("swing_heavy", 0.6f); return; }
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
            float span = radius + e.radius;
            if (d.magnitude > span) continue;
            // Standing on top of the impact hurts a great deal more than catching its edge.
            float near = Mathf.Lerp(.5f, 1f, 1f - Mathf.Clamp01(d.magnitude / Mathf.Max(span, .01f)));
            var h = new Hit { dmg = Mathf.Max(1, Mathf.RoundToInt(dmg * near)), knock = 5f, launch = launch, stun = 0.6f, dir = d.x >= 0 ? 1 : -1, heavy = true, from = this };
            if (e.TakeHit(h)) { n++; OnLanded(e); }
        }
        Fx.Ring(pos, radius * 2.2f, new Color(1, 0.8f, 0.5f, 0.9f)); Fx.Ring(pos, radius * 1.4f, new Color(1, 1, 1, 0.7f), 0.22f);
        Fx.Dust(pos, 10, 1.3f, 2.5f); CameraRig.I.Shake(0.35f, 0.3f); Sfx.Play("boom", 0.6f, 0.05f, 1.15f);
        if (n > 0) Game.I.HitStop(0.12f);
    }

    void StartCast(Vector2 move)
    {
        buf = ""; Face(move); usedMagic = true;
        // Nothing to spend before the ember is found: stay silent rather than
        // playing a failure sting for an ability the player has never been given.
        if (!HasEmbers) return;
        if (!CanCast) { Sfx.Play("tele", 0.4f, 0, 0.6f); Fx.Emit("fx_puff", Center + new Vector2(facing * 0.5f, 0), new Vector2(0, 1), 0.3f, 0.3f, 0.6f, new Color(0.5f, 0.5f, 0.5f, 0.7f), new Color(0.5f, 0.5f, 0.5f, 0)); Game.I.hud.PulseEmber(); return; }
        st = St.Cast; t = 0; hitDone = false; combo = 0;
        // Tell the bar this health is being spent rather than taken, so the strip
        // ignites and crumbles to ash instead of flinching -- then spend it.
        Game.I.hud.MarkEmberSpend();
        hp = Mathf.Max(1, hp - EmberCost);
        Fx.Flames(Center + new Vector2(facing * 0.4f, 0), 3, 0.5f);
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
            float dy = Mathf.Abs(e.pos.y - pos.y);
            if (dy > depth + e.radius * 0.5f) continue;
            if (e.z < zLo || e.z > zHi) continue;
            // Hit is a struct, so each target gets its own copy and its own number.
            var hh = h;
            hh.dmg = Mathf.Max(1, Mathf.RoundToInt(h.dmg * Directness(dx, dy, range, depth, e.radius)));
            if (e.TakeHit(hh)) { n++; OnLanded(e); }
            if (st == St.Hurt) break;
        }
        return n;
    }

    /// How squarely a blow landed: 1 through the middle of the swing, falling to 0.45 at
    /// the fringes. Without this a swing into a crowd deals one flat number to everything
    /// it touches, and position stops mattering the moment there is more than one body.
    static float Directness(float dx, float dy, float range, float depth, float radius)
    {
        float sweet = range * .45f;                                              // where the blade bites
        float along = Mathf.Clamp01(Mathf.Abs(dx - sweet) / Mathf.Max(range, .01f));
        float side = Mathf.Clamp01(dy / Mathf.Max(depth + radius * .5f, .01f));
        return Mathf.Lerp(.45f, 1f, Mathf.Clamp01(1f - along * .8f - side * .6f));
    }

    public void OnLanded(Enemy e)
    {
        comboHits++; comboHitsT = 2.2f; if (comboHits > maxCombo) maxCombo = comboHits;
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
        if (dead || invuln > 0 || RollProtected || st == St.Intro || st == St.Win) return false;
        if (Game.I.state != Game.State.Playing) return false;
        // Judge "in front" by which way the blow travels, not where the attacker stands:
        // enemies lunge into their swing and can slide past his centre mid-strike,
        // which made a blocked hit read as coming from behind and go straight through.
        bool frontal = h.from == null || (h.dir != 0 ? h.dir * facing < 0 : (h.from.pos.x - pos.x) * facing > 0);
        if (st == St.Guard && shieldSprite != null && frontal)
        {
            Vector2 sp = Center + new Vector2(facing * .45f, .1f);
            if (t < ParryWindow)
            {
                // Timed right as the shield goes up: a free parry. No stamina spent, and the attacker reels.
                staminaDelay = .8f;
                Fx.HitSpark(sp, 1.2f, new Color(1f, .95f, .6f)); Fx.Ring(sp, 1.1f, new Color(1f, .9f, .6f, .85f), .24f);
                Sfx.Play("bone_break", .6f, .03f, 1.25f); CameraRig.I.Shake(.16f, .14f); Game.I.HitStop(.08f);
                if (h.from is Enemy attacker) attacker.Parried(facing);
                return false;
            }
            // Held. The hit rings off the shield and only costs stamina -- heavy blows cost more but still land for nothing.
            stamina = Mathf.Max(0, stamina - (h.heavy ? HeavyBlockCost : BlockCost)); staminaDelay = .8f;
            kb = new Vector2(h.dir * h.knock * .35f, 0);
            Fx.HitSpark(sp, h.heavy ? 1f : .8f, new Color(.8f, .9f, 1f)); Fx.Ring(sp, .8f, new Color(.85f, .9f, 1f, .6f), .18f);
            Sfx.Play("hit_heavy", .5f, .03f, 1.4f); CameraRig.I.Shake(h.heavy ? .12f : .08f, h.heavy ? .12f : .08f);
            return false;
        }
        spinCur = 0; dodgeDir = Vector2.zero; vel = Vector2.zero; buf = ""; dodgeCd = .3f;
        int dmg = shieldSprite != null ? Mathf.Max(1, Mathf.RoundToInt(h.dmg * (1f - ShieldSoak))) : h.dmg;
        hp -= dmg; Flash(new Color(1, 0.3f, 0.25f), 0.25f);
        if (shieldSprite != null) Fx.Ring(Center, .9f, new Color(.85f, .9f, 1f, .55f), .22f);
        kb = new Vector2(h.dir * h.knock, 0); if (h.launch > 0) { vz = h.launch; z = Mathf.Max(z, 0.01f); }
        CameraRig.I.Shake(0.25f, 0.25f); Game.I.HitStop(0.08f); Sfx.Play("hurt", 0.7f);
        Fx.HitSpark(Center, 0.75f, new Color(1, 0.55f, 0.42f), h.dir);
        comboHits = 0; combo = 0;
        if (hp <= 0) { hp = 0; dead = true; st = St.Dead; t = 0; vz = 6; z = 0.01f; kb = new Vector2(h.dir * 5f, 0); Game.I.OnPlayerDead(); }
        else { st = St.Hurt; t = 0; invuln = 0.55f; }
        return true;
    }

    static float Ease(float x) { x = Mathf.Clamp01(x); return x * x * (3 - 2 * x); }

    /// Keeps the flask sitting in the fist like a held object: the arm's own spin
    /// is cancelled out so it stays upright instead of swinging around like a
    /// decal stuck to the forearm, it squeezes once as the hand closes on it,
    /// and it tips toward the mouth on the way down his throat.
    /// Keeps the held ember upright as the arm swings, breathes it on two offset
    /// rates so it never looks like a plain sine, and lets it shed the odd flame.
    void PoseOrb(float dt)
    {
        if (emberOrb == null || !emberOrb.enabled) return;
        float tt = Time.time;
        emberOrb.transform.localRotation = Quaternion.Inverse(rig.armF.localRotation);
        float wob = 1f + Mathf.Sin(tt * 7.3f) * .07f + Mathf.Sin(tt * 11.9f) * .04f;
        emberOrb.transform.localScale = orbBaseScale * wob * (st == St.Cast ? 1.35f : 1f);
        emberOrb.color = new Color(1f, .40f + .11f * Mathf.Sin(tt * 9.1f), .09f, .70f);
        emberOrbCore.transform.localScale = Vector3.one * .40f * (1f + Mathf.Sin(tt * 13.7f) * .10f);
        // Fx.Flames throws its own wide glow, so keep both the rate and the size low or
        // the knight ends up standing in a floodlight.
        if (Random.value < dt * 5.5f) Fx.Flames(World.Unproject(emberOrb.transform.position), 1, .21f);
    }

    void PoseFlask()
    {
        if (drinkBottle == null || !drinkBottle.enabled) return;
        float tilt = st == St.Drink ? Mathf.Lerp(FlaskTilt, -68f, Ease(t / .3f)) : FlaskTilt;
        drinkBottle.transform.localRotation = Quaternion.Inverse(rig.armF.localRotation) * Quaternion.Euler(0, 0, tilt);
        float grip = swapT < GrabAt ? 0f : (1f - Ease(Mathf.Clamp01((swapT - GrabAt) / .18f))) * .22f;
        drinkBottle.transform.localScale = bottleBaseScale * (1f + grip);
        drinkBottle.transform.localPosition = new Vector3(0, FlaskY, 0);
    }

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
                if (swapT < GrabAt)
                {
                    // Reach: the hand drops back and low, to the belt, and he
                    // dips into it rather than just rotating a limb.
                    float k = Ease(swapT / GrabAt);
                    r.aArmF = Mathf.Lerp(r.aArmF, 54f, k);
                    r.aBody = Mathf.Lerp(r.aBody, 7f, k);
                    r.aHead = Mathf.Lerp(r.aHead, 6f, k * .8f);
                    r.aArmB = Mathf.Lerp(r.aArmB, -22f, k * .6f);
                }
                else if (potionOut)
                {
                    // Lift out of the grab and just carry it at his side. Raising it
                    // to the chest read as winding up to throw the thing, and swung
                    // the arm back far enough to bury it in the cloak.
                    float k = Ease(Mathf.Clamp01((swapT - GrabAt) / .3f));
                    r.aArmF = Mathf.Lerp(54f, HoldArm + br * 2f, k);
                }
                else if (swapT < GrabAt + .3f)
                {
                    // Same lift coming back the other way, settling into guard.
                    float k = Ease((swapT - GrabAt) / .3f);
                    r.aArmF = Mathf.Lerp(54f, r.aArmF, k);
                    r.aWeapon = Mathf.Lerp(-30f, r.aWeapon, k);
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
            case St.Drink:
                r.aArmF = -132; r.aWeapon = -100; r.aArmB = -14; r.aHead = -12; r.aBody = 4;
                break;
            case St.Hurt:
                {
                    // Two beats. The blow snaps him back -- head thrown, arms flung, armour
                    // compressing -- then he hunches over it and gathers himself.
                    if (t < .07f)
                    {
                        r.rate = 70;
                        r.aBody = 24; r.aHead = 30; r.aArmF = -58; r.aWeapon = -120; r.aArmB = -100; r.aLegF = -24; r.aLegB = 26;
                        r.squash = new Vector2(1.1f, .88f); r.off = new Vector2(-.07f, -.02f); r.aCape = 35;
                    }
                    else
                    {
                        float b = Ease((t - .07f) / .21f);
                        r.rate = 26;
                        r.aBody = Mathf.Lerp(14, 4, b); r.aHead = Mathf.Lerp(-12, 0, b);
                        r.aArmF = Mathf.Lerp(-20, 8, b); r.aWeapon = Mathf.Lerp(-95, -60, b); r.aArmB = Mathf.Lerp(-40, -10, b);
                        r.aLegF = Mathf.Lerp(-20, -4, b); r.aLegB = Mathf.Lerp(28, 8, b);
                        r.squash = Vector2.Lerp(new Vector2(1.05f, .93f), Vector2.one, b);
                        r.off = new Vector2(Mathf.Sin(t * 90) * .012f * (1 - b), -.03f * (1 - b));
                    }
                    break;
                }
            case St.Guard:
                {
                    // Shield arm swings up across the chest, he leans in behind it and
                    // the sword drops back out of the way.
                    float k = Ease(t / .12f);
                    r.rate = 40;
                    r.aArmB = Mathf.Lerp(-8, -75, k); r.aArmF = Mathf.Lerp(12, 38, k); r.aWeapon = Mathf.Lerp(-55, -20, k);
                    r.aBody = 7 * k; r.aHead = 4 * k; r.off = new Vector2(0, br * 0.01f);
                    if (vel.magnitude > 0.6f)
                    {
                        walkPhase += dt * (4f + vel.magnitude * 1.5f);
                        float sw = Mathf.Sin(walkPhase);
                        r.aLegF = sw * 24; r.aLegB = -sw * 24;
                    }
                    else { r.aLegF = -6; r.aLegB = 10; }
                    r.squash = new Vector2(1.03f, .98f);
                    break;
                }
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

    // The wave is a line down one lane, and the lane has to mean something. The whole
    // playfield is only 2.55 units deep (World.MinY..MaxY), so the old 1.0 depth
    // tolerance reached better than nine tenths of it -- fired from anywhere it swept
    // every enemy on screen regardless of where they stood. 0.42 plus half a body is
    // about one body of slack: enough to forgive a near miss, not enough to aim with
    // your eyes shut. HeightSlack keeps it off anything launched well overhead.
    const float Reach = 0.8f, LaneDepth = 0.42f, HeightSlack = 1.6f;

    public static void Spawn(Vector2 at, int dir, float height)
    {
        var go = new GameObject("EmberWave"); var w = go.AddComponent<EmberWave>();
        w.pos = at; w.dir = dir; w.h = Mathf.Min(height, 1.2f);
        w.sr = Art.New("fx_wave", go.transform, new Vector3(0, 0.7f + w.h, 0), 0); w.sr.flipX = dir < 0;
        w.glow = Art.New("fx_glow", go.transform, new Vector3(0, 0.6f + w.h, 0), 0, "Default", true);
        w.glow.color = new Color(1, 0.55f, 0.2f, 0.55f); w.glow.transform.localScale = Vector3.one * 3.2f;
        go.transform.position = World.Project(at);
    }

    void Update()
    {
        float dt = Time.deltaTime; if (dt <= 0) return;
        life += dt; pos.x += dir * 12.5f * dt;
        transform.position = World.Project(pos);
        float s = 1.0f + Mathf.Sin(life * 40) * 0.08f + life * 0.5f;
        sr.transform.localScale = new Vector3(s, s, 1); sr.sortingOrder = Art.YOrder(pos.y) + 5; glow.sortingOrder = sr.sortingOrder + 1;
        if (Random.value < dt * 70) Fx.Flames(pos + new Vector2(-dir * 0.4f, 0.5f + h + Random.Range(-0.3f, 0.3f)), 1, 0.55f);
        if (Random.value < dt * 40) Fx.Emit("fx_soft", pos + new Vector2(0, 0.1f), new Vector2(-dir * 2, Random.Range(1f, 3f)), 0.4f, 0.2f, 0.02f, new Color(1, 0.8f, 0.3f), new Color(1, 0.3f, 0.1f, 0), 0, 0, 0, true);
        for (int i = Actor.All.Count - 1; i >= 0; i--)
        {
            var e = Actor.All[i] as Enemy; if (e == null || e.dead || hit.Contains(e)) continue;
            if (Mathf.Abs(e.pos.x - pos.x) > Reach + e.radius) continue;
            if (Mathf.Abs(e.pos.y - pos.y) > LaneDepth + e.radius * 0.5f) continue;
            if (Mathf.Abs(e.z - h) > HeightSlack) continue;
            hit.Add(e);
            var hh = new Hit { dmg = 20, knock = 6f, launch = 5f, stun = 0.7f, dir = dir, heavy = true, from = Game.I.player };
            if (e.TakeHit(hh)) { Game.I.player.OnLanded(e); Fx.Flames(e.Center, 5, 0.9f); Game.I.HitStop(0.03f); }
        }
        float a = Mathf.Clamp01((0.75f - life) / 0.15f);
        sr.color = new Color(1, 1, 1, a);
        if (life > 0.75f || World.OutOfView(pos.x, 2f)) { Fx.Flames(pos + new Vector2(0, 0.6f), 4, 0.8f); Destroy(gameObject); }
    }
}
