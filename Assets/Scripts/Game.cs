using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Bootstraps the level at runtime and runs the flow: title -> encounters -> boss -> victory.
[DefaultExecutionOrder(-100)]
public class Game : MonoBehaviour
{
    public static Game I;
    public enum State { Title, Playing, Dead, Victory }
    public State state = State.Title;
    public Player player; public Hud hud;   // Jaren (the fox companion) removed at the user's request
    public int maxTokens = 2;
    public int Cinders { get; private set; }
    public int Experience { get; private set; }
    public bool HasShield { get; private set; }
    public bool HasEmbers { get; private set; }
    public bool HasSlam { get; private set; }

    // Levelling: each one costs a flat step more than the last, so early levels
    // come fast enough to feel like progress and later ones still mean something.
    // L2 at 250 XP, L3 at 950, L4 at 2100. XP is kept across deaths and runs (saved with the cinders), so the
    // knight keeps climbing past the old one-run ceiling of level 3.
    public const int XpBase = 250, XpStep = 450;
    public static int XpForLevel(int level) { return XpBase + XpStep * (level - 1); }
    /// Current level, and XP already earned toward the next one.
    void LevelFromXp(out int level, out int intoLevel)
    {
        level = 1; intoLevel = Experience;
        while (intoLevel >= XpForLevel(level)) { intoLevel -= XpForLevel(level); level++; }
    }
    public int Level { get { LevelFromXp(out int lv, out _); return lv; } }
    /// 0..1 across the current level, for the bar on the knight's card.
    public float LevelProgress { get { LevelFromXp(out int lv, out int left); return Mathf.Clamp01((float)left / XpForLevel(lv)); } }
    /// Each level past the first: +10% damage dealt (up to double) and 6% less damage taken (down to half).
    public static float DamageDealtScaleFor(int level) { return Mathf.Min(2f, 1f + .10f * (level - 1)); }
    public static float DamageTakenScaleFor(int level) { return Mathf.Max(.5f, 1f - .06f * (level - 1)); }
    public float DamageDealtScale => DamageDealtScaleFor(Level);
    public float DamageTakenScale => DamageTakenScaleFor(Level);
    // Last known XP for this play session. A scene reload (death restart, return to menu) restores from here
    // as well as PlayerPrefs, so progress survives even where browser storage is blocked (private windows).
    static int sessionExperience = -1;
    // A new key on purpose: builds where XP reset every run still wrote it under "Emberclad.Experience", and
    // test sessions left huge values there. Persistent progression starts clean from this build.
    const string XpKey = "Emberclad.XP";

    /// A new nickname is a new knight: level 1 again. Progress belongs to the name.
    public static void ResetKnightProgress()
    {
        sessionExperience = 0; PlayerPrefs.SetInt(XpKey, 0); PlayerPrefs.Save();
        if (I != null) { I.Experience = 0; I.rewardsDirty = false; }
    }

    /// Resuming a knight: take the site's saved XP if it is further along than this browser's.
    public static void RestoreKnightXp(int xp)
    {
        if (xp <= sessionExperience && (I == null || xp <= I.Experience)) return;
        sessionExperience = Mathf.Max(sessionExperience, xp); PlayerPrefs.SetInt(XpKey, sessionExperience); PlayerPrefs.Save();
        if (I != null) I.Experience = Mathf.Max(I.Experience, xp);
    }
    bool rewardsDirty;
    float rewardSaveTimer;

    void MarkRewardsDirty() { if (!rewardsDirty) rewardSaveTimer = 2f; rewardsDirty = true; }

    void SaveRewards()
    {
        sessionExperience = Mathf.Max(sessionExperience, Experience);
        if (!rewardsDirty) return;
        PlayerPrefs.SetInt("Emberclad.Cinders", Cinders);
        PlayerPrefs.SetInt(XpKey, Experience);
        PlayerPrefs.SetInt("Emberclad.Shield", HasShield ? 1 : 0);
        PlayerPrefs.SetInt("Emberclad.Embers", HasEmbers ? 1 : 0);
        PlayerPrefs.SetInt("Emberclad.Slam", HasSlam ? 1 : 0);
        PlayerPrefs.Save();
        rewardsDirty = false;
    }

    void OnApplicationPause(bool paused) { if (paused) { SaveRewards(); RunRecord.Sync(); } }
    void OnApplicationQuit() { SaveRewards(); RunRecord.Sync(); }
    void OnDestroy() { SaveRewards(); }
    public static int checkpoint; public static bool skipTitle; public static float runTime; public static int falls, runKills, runBestCombo;

    class Enc { public float x; public string[] waves; public Enc(float x, params string[] waves) { this.x = x; this.waves = waves; } }

    // Every round is a written roster, not a dice roll: the same skeletons in the same
    // places every run, so a round can be learned and the difficulty curve actually
    // means something. Each token is KIND:dx,dy offset from the encounter's centre.
    //   G regular   S regular with a shield   R small   B helmeted   X the ember sniper
    // One round per clearing, strung along the path, so clearing one earns you ground
    // and the GO arrow that points at the next.
    readonly Enc[] encs =
    {
        new Enc(16f,  "G:-7,0.5 G:-4.4,-0.45 G:5,0.4 G:7.4,-0.5"),            // 1: four regulars, no shields
        new Enc(38f,  "B:6,0 R:-5.5,0.45"),                                   // 2: a helmet and a small one
        new Enc(60f,  "S:-6.5,0.3 S:6,-0.25 R:-3.4,-0.5 R:3.6,0.55"),         // 3: two shields, two small
        new Enc(82f,  "G:-6,0.4 G:6.4,-0.3 R:-3.4,-0.5 R:4,0.55 B:8,0"),      // 4: two regular, two small, a helmet
        // The baby dragon is parked, not deleted: BabyDragon.cs and EmberShot.cs are
        // still here and still compile, and putting "X:11,1.6" back on this line is the
        // whole of bringing him back.
        new Enc(104f, "S:-6,0.35 S:6,-0.3 B:-8.5,0.15 B:8.5,0.2"),          // 5: two shields, two helmets
        new Enc(129f, "BOSS"),
    };
    int encIdx, wave; bool inEnc, spawning, bossDead, paused, slowmo; float waveDelay, hitStop, stateT;
    // While a tutorial drop is on the floor the next round waits for it. The timeout
    // only exists so a pickup that somehow cannot be reached can never deadlock a run.
    Pickup tutorialDrop; float tutorialHold;
    /// The one place the tutorial takes the player by the hand: the wheel is not
    /// discoverable by flailing, so the round after the ember waits until they have
    /// actually held the key, picked the ember out of it, and put the blade back.
    public enum Lesson { None, EquipEmber, BackToBlade }
    Lesson lesson; float lessonT;
    public bool LessonActive { get { return lesson != Lesson.None; } }
    // Each step of the wheel lesson gives up after this long, so a player who never touches the wheel
    // is not left with a card on screen and a round that will not continue.
    const float EquipEmberTimeout = 30f, BackToBladeTimeout = 20f;

    void TickLesson()
    {
        if (lesson == Lesson.None || hud == null) return;
        lessonT += Time.deltaTime;
        if (lesson == Lesson.EquipEmber && QuickItems.Selected == 2)
        {
            lesson = Lesson.BackToBlade; lessonT = 0f;
            Sfx.Play("mana", .5f, 0, 1.4f);
            hud.TeachCard("Ember In Hand", null, new[] { "G" }, new[] { "Hold, aim up for the blade" }, BackToBladeTimeout);
        }
        else if (lesson == Lesson.BackToBlade && QuickItems.Selected == 0)
        {
            EndLesson();
            Sfx.Play("mana", .55f, 0, 1.55f);
        }
        else if (lessonT >= (lesson == Lesson.EquipEmber ? EquipEmberTimeout : BackToBladeTimeout)) EndLesson();
    }

    /// Done (or timed out): the card goes as soon as its condition is met rather than sitting out its timer.
    void EndLesson()
    {
        lesson = Lesson.None; lessonT = 0f;
        if (hud == null) return;
        hud.DemoHotbar(false);
        hud.DismissTeach();
    }
    readonly List<Enemy> alive = new List<Enemy>(); Enemy boss;
    public bool CanUseItems => state == State.Playing && !paused && player != null && !player.dead && hud != null && !hud.IntroOpen;
    public bool InCombat => inEnc;
    public int EncounterIndex => encIdx;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { autopilot = false; debugSpeed = 1f; checkpoint = 0; skipTitle = false; runTime = 0; falls = 0; runKills = 0; runBestCombo = 0; sessionExperience = -1; }

    void Awake()
    {
        Cinders = PlayerPrefs.GetInt("Emberclad.Cinders", 0);
        // XP and level persist: a death restart sends the knight back to the start of the trail, but he keeps
        // everything he has earned. Take whichever is higher of the saved value and this session's.
        Experience = Mathf.Max(0, Mathf.Max(PlayerPrefs.GetInt(XpKey, 0), sessionExperience));
        sessionExperience = Experience;
        HasShield = PlayerPrefs.GetInt("Emberclad.Shield", 0) == 1;
        HasEmbers = PlayerPrefs.GetInt("Emberclad.Embers", 0) == 1;
        HasSlam = PlayerPrefs.GetInt("Emberclad.Slam", 0) == 1;
        I = this; Time.timeScale = 1; Enemy.tokens = 0; Actor.All.Clear();
        // No frame cap: -1 lets WebGL run on requestAnimationFrame (the display's own rate) instead of a
        // setTimeout-driven 60 that stutters below it, and desktop builds sync to the monitor (60/120 Hz).
        QualitySettings.vSyncCount = Application.isEditor ? 0 : 1;
        Application.targetFrameRate = -1;
        Art.Load();
        var cam = Camera.main;
        cam.orthographic = true; cam.orthographicSize = CameraRig.ViewHalfHeight; cam.backgroundColor = new Color(0.12f, 0.24f, 0.26f, 1); cam.clearFlags = CameraClearFlags.SolidColor;
        var rig = cam.GetComponent<CameraRig>(); if (rig == null) rig = cam.gameObject.AddComponent<CameraRig>();
        if (cam.GetComponent<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
        new GameObject("Fx").AddComponent<Fx>();
        Sfx.Init(gameObject);
        World.Build(new GameObject("World").transform, cam.transform);
        encIdx = Mathf.Clamp(checkpoint, 0, encs.Length - 1);
        float startX = checkpoint > 0 ? encs[encIdx].x - 7f : 0.5f;
        player = Player.Create(new Vector2(startX, -2.6f));
        hud = new GameObject("HUD").AddComponent<Hud>(); hud.Build(this);
        rig.SnapTo(startX + 1.4f);
        if (encIdx >= 3) maxTokens = 3;
        if (skipTitle) { state = State.Playing; player.SkipIntro(); hud.HideTitle(true); }
        else { state = State.Title; hud.ShowTitle(); }
    }

    public void MenuPause(bool value) { paused = value; Time.timeScale = value ? 0 : 1; }
    public bool Paused => paused;
    public void StartFromMenu() { state = State.Playing; stateT = 0; hud.HideTitle(true); player.Begin(); Sfx.Play("ui_start", 0.6f, 0); hud.BeginIntro(); }
    public void ReturnToMenu() { RunRecord.Abandon(); SaveRewards(); skipTitle = false; checkpoint = 0; Time.timeScale = 1; SceneManager.LoadScene(SceneManager.GetActiveScene().name); }
    public void HitStop(float d) { hitStop = Mathf.Max(hitStop, d); }

    void Update()
    {
        if (rewardsDirty && (rewardSaveTimer -= Time.unscaledDeltaTime) <= 0) SaveRewards();
        RunRecord.Tick((state == State.Playing && !paused) || state == State.Dead);
        if (autopilot) Autopilot();
        GameInput.Tick();
        QuickItems.Tick(this);
        float udt = Time.unscaledDeltaTime; stateT += udt;
        Sfx.Tick(udt);
        if (hitStop > 0) hitStop -= udt;
        // Pause always wins: a hit-stop landing as the menu opened used to keep the game crawling underneath it.
        Time.timeScale = paused ? 0f : hitStop > 0 ? 0.02f : (slowmo ? 0.25f : 1f) * debugSpeed;

        switch (state)
        {
            case State.Playing:
                // Help (Tab) toggles its own pause, but never while the Esc menu is up:
                // Tab is also the menu's navigation key, and toggling here un-paused the game behind the menu.
                if (!hud.IntroOpen && !hud.PauseMenuOpen && GameInput.Help) { paused = !paused; hud.ShowHelp(paused); }
                if (paused) break;
                runTime += Time.deltaTime;
                TickLesson();
                TickEncounters();
                break;
            case State.Dead:
                if (stateT > 1.2f && (GameInput.Restart || GameInput.Confirm)) { checkpoint = 0; Reload(true); }   // death sends you back to the very start; run timer and death count carry on
                break;
            case State.Victory:
                // Each leaderboard name can only finish once, so a new run goes back through the menu for a new name.
                if (stateT > 1.5f && GameInput.Restart) { runTime = 0; falls = 0; runKills = 0; runBestCombo = 0; ReturnToMenu(); }
                break;
        }
    }

    void Reload(bool skip)
    {
        SaveRewards();
        skipTitle = skip; Time.timeScale = 1;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void TickEncounters()
    {
        if (player.maxCombo > runBestCombo) runBestCombo = player.maxCombo;
        if (!inEnc)
        {
            if (encIdx < encs.Length && player.pos.x > encs[encIdx].x - 2f && !bossDead) BeginEncounter();
            return;
        }
        if (spawning) return;
        alive.RemoveAll(e => e == null || e.dead);
        // The sniper can never be killed, so he cannot be what the round is waiting on.
        // Once his escort is down he runs, and the round ends when he is gone.
        int fighters = 0;
        foreach (var e in alive) if (!e.Invulnerable) fighters++;
        if (fighters > 0) return;
        if (alive.Count > 0)
        {
            foreach (var e in alive) e.Flee();
            return;
        }
        if (encs[encIdx].waves[0] == "BOSS") return;   // boss fight ends through OnBossDead
        if (tutorialDrop != null)
        {
            tutorialHold -= Time.deltaTime;
            if (tutorialHold > 0f) return;
            tutorialDrop = null;
        }
        if (lesson != Lesson.None) return;
        waveDelay -= Time.deltaTime;
        if (waveDelay > 0) return;
        wave++;
        if (wave < encs[encIdx].waves.Length) StartCoroutine(SpawnWave(encs[encIdx].waves[wave], encs[encIdx].x + 2f));
        else EndEncounter();
    }

    void BeginEncounter()
    {
        inEnc = true; wave = 0; var e = encs[encIdx]; float center = e.x + 2f;
        // The front of the fight is held, the way back is not: you can retreat as far as
        // you like and the camera follows, but the round has to be cleared before the
        // path opens up again.
        // The Bone Warden is fought inside one screen: the camera stops and the frame edges become walls until he dies.
        if (e.waves[0] == "BOSS") CameraRig.I.Lock(center); else CameraRig.I.HoldForward(center);
        Sfx.drumsTarget = 0.5f; hud.HideGo();
        if (encIdx >= 3) maxTokens = 3;
        if (e.waves[0] == "BOSS") StartCoroutine(BossIntro(center));
        else
        {
            StartCoroutine(SpawnWave(e.waves[0], center));
            if (encIdx == 0) { hud.Banner("SKELETONS!", 1.6f); StartCoroutine(TeachBlade()); }
            // The slam has no pickup to hang off, so it is taught as its round opens.
            else if (encIdx == 3 && !HasSlam) StartCoroutine(TeachSlam());
        }
    }

    /// The first lesson has no pickup to hang off, so it rides in behind the banner
    /// while the first two grunts are still walking in. Shown once per profile.
    IEnumerator TeachBlade()
    {
        if (PlayerPrefs.GetInt("Emberclad.TaughtBlade", 0) != 0) yield break;
        yield return new WaitForSeconds(1.7f);
        if (hud == null || state != State.Playing) yield break;   // died first: teach it on the next attempt instead
        hud.TeachCard("The Blade", null, new[] { "J", "K" }, new[] { "Light attack", "Heavy attack" });
        PlayerPrefs.SetInt("Emberclad.TaughtBlade", 1); PlayerPrefs.Save();
    }

    /// The fourth round is the only one whose lesson is a technique rather than a
    /// thing on the floor, so it arrives with the wave instead of before it.
    IEnumerator TeachSlam()
    {
        yield return new WaitForSeconds(.9f);
        HasSlam = true;
        if (!rewardsDirty) rewardSaveTimer = 2f;
        rewardsDirty = true;
        if (hud == null || state != State.Playing) yield break;
        hud.TeachCard("Ground Slam", null, new[] { "SPACE", "K" }, new[] { "Jump", "Heavy in the air" }, 6f);
    }

    IEnumerator SpawnWave(string spec, float center)
    {
        spawning = true; waveDelay = 0.9f;
        yield return new WaitForSeconds(wave == 0 ? 0.5f : 0.2f);
        Sfx.Play("spawn", 0.7f);
        float laneY = (World.MinY + World.MaxY) * .5f;
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        foreach (var token in spec.Split(' '))
        {
            if (token.Length == 0) continue;
            int colon = token.IndexOf(':');
            if (colon < 0) continue;
            char code = token[0];
            string[] xy = token.Substring(colon + 1).Split(',');
            float dx = float.Parse(xy[0], inv), dy = float.Parse(xy[1], inv);

            Enemy.Kind kind = code == 'R' ? Enemy.Kind.Runt
                            : code == 'B' ? Enemy.Kind.Brute
                            : code == 'X' ? Enemy.Kind.Dragon
                            : Enemy.Kind.Grunt;
            bool shield = code == 'S';

            Vector2 at = new Vector2(center + dx, Mathf.Clamp(laneY + dy, World.MinY + .15f, World.MaxY - .1f));
            alive.Add(Enemy.Create(kind, at, shield));
            yield return new WaitForSeconds(0.14f);
        }
        spawning = false;
    }

    public void SpawnAdds(int count, Vector2 around)
    {
        Sfx.Play("spawn", 0.7f); float center = encs[encs.Length - 1].x + 2f, hw = CameraRig.I.HalfW;
        alive.RemoveAll(e => e == null || e.dead);
        count = Mathf.Min(count, Mathf.Max(0, 4 - alive.Count));
        for (int i = 0; i < count; i++)
        {
            Vector2 p = new Vector2(center + (i % 2 == 0 ? 1 : -1) * Random.Range(2.5f, hw - 1.2f), Random.Range(World.MinY + 0.2f, World.MaxY - 0.1f));
            alive.Add(Enemy.Create(Enemy.Kind.Runt, p));
        }
    }

    IEnumerator BossIntro(float center)
    {
        spawning = true; Sfx.MusicVolume(0.2f);
        yield return new WaitForSeconds(0.8f);
        CameraRig.I.Shake(0.25f, 1.2f); Sfx.Play("spawn", 0.9f, 0, 0.7f);
        yield return new WaitForSeconds(0.5f);
        boss = Enemy.Create(Enemy.Kind.Boss, new Vector2(center + 4.2f, -2.5f)); alive.Add(boss);
        yield return new WaitForSeconds(0.5f);
        Sfx.Play("roar", 1f); CameraRig.I.Shake(0.3f, 0.8f);
        hud.Banner("THE BONE WARDEN", 2.4f); hud.ShowBoss(boss);
        Sfx.MusicVolume(0.34f);
        spawning = false;
    }

    void EndEncounter()
    {
        inEnc = false; encIdx++; checkpoint = Mathf.Max(checkpoint, encIdx);
        CameraRig.I.Unlock(); Sfx.drumsTarget = 0f; hud.ShowGo();
        Sfx.Play("mana", 0.5f, 0, 1.2f);
    }

    /// Health siphoned off a corpse, paid out one mote at a time as the HUD's embers
    /// land on the bar. The player's hp stays the single source of truth.
    public void GrantSiphon(int amount)
    {
        if (player == null || player.dead || amount <= 0) return;
        player.hp = Mathf.Min(player.maxHp, player.hp + amount);
    }

    public void OnEnemyKilled(Enemy e)
    {
        if (e == null || !e.dead || e.rewardGranted) return;
        e.rewardGranted = true;
        player.kills++; runKills++;
        int cinders = e.IsBoss ? 250 : e.kind == Enemy.Kind.Brute ? 30 : e.kind == Enemy.Kind.Runt ? 5 : 10;
        int xp = e.IsBoss ? 500 : e.kind == Enemy.Kind.Brute ? 60 : e.kind == Enemy.Kind.Runt ? 10 : 20;
        // Siphon: a sip of life off the corpse, scaled to how formidable it was. Kept
        // deliberately tiny -- a grunt is worth a fiftieth of the bar. It is a reward
        // for the kill, not a way to sustain through a fight.
        int siphon = e.IsBoss ? 15 : e.kind == Enemy.Kind.Brute ? 4 : e.kind == Enemy.Kind.Runt ? 1 : 2;
        int before = Level;
        Cinders += cinders;
        Experience += xp;
        sessionExperience = Mathf.Max(sessionExperience, Experience);
        MarkRewardsDirty();
        if (Level > before) { SaveRewards(); RunRecord.Sync(); }   // a new level is saved (and sent) straight away
        if (hud != null)
        {
            // Spray scales with what the thing was worth: a runt is a handful of
            // coins, the warden is a fountain.
            float weight = e.IsBoss ? 1f : e.kind == Enemy.Kind.Brute ? .55f : e.kind == Enemy.Kind.Runt ? 0f : .25f;
            hud.BurstCinders(e.transform.position + new Vector3(0, .9f, 0), cinders, weight);
            hud.BurstSiphon(e.transform.position + new Vector3(0, .9f, 0), siphon, weight);
            hud.ShowRewards(cinders, xp);
            if (Level > before)
            {
                // A level is a full restock: the coals go back to the end of the bar
                // and the belt is refilled, so levelling is the pacing beat that lets
                // you spend health on Embers again.
                player.hp = player.maxHp;
                player.potions = 3;
                hud.Banner("LEVEL " + Level, 2f);
                hud.LevelRestore();
                Sfx.Play("mana", .7f, 0, 1.15f);
            }
        }
        TutorialDrop(e);
        // Shield-bearers leave the shield behind. It is the only thing they had. Not in
        // the tutorial encounter though: a second shield off a random guard mid-round
        // pre-empts the scripted drop the next round is built around, and the player
        // ends up with two on the floor.
        if (e.hasShield && !HasShield && encIdx > 1)
        {
            Pickup.Drop(Pickup.Kind.Shield, e.pos, e.pos.x < player.pos.x ? -1 : 1);
            e.ReleaseShield();
        }
    }

    public void OnPickup(Pickup.Kind kind)
    {
        tutorialDrop = null;
        if (!rewardsDirty) rewardSaveTimer = 2f;
        rewardsDirty = true;
        if (kind == Pickup.Kind.Shield)
        {
            HasShield = true;
            if (player != null) player.EquipShield();
            if (hud != null) hud.TeachCard("Shield", null, new[] { "SHIFT" }, new[] { "Hold to guard" });
        }
        else if (kind == Pickup.Kind.Ember)
        {
            HasEmbers = true;
            if (hud != null)
            {
                lesson = Lesson.EquipEmber; lessonT = 0f;
                hud.DemoHotbar(true);
                hud.TeachCard("Ember Burst", null, new[] { "G", "L" }, new[] { "Hold, aim down", "Cast" }, EquipEmberTimeout);
            }
        }
    }

    /// True when this corpse was the last thing standing in the round.
    bool LastOfWave(Enemy e)
    {
        if (spawning) return false;
        foreach (var other in alive) if (other != null && !other.dead && other != e) return false;
        return true;
    }

    /// The tutorial rounds hand over the gear the next round is about. Only ever drops
    /// what the player does not already have, so a returning knight is not handed a
    /// second shield or re-taught something they know.
    void TutorialDrop(Enemy e)
    {
        if (!LastOfWave(e)) return;
        int dir = e.pos.x < player.pos.x ? -1 : 1;
        if (encIdx == 0 && !HasShield) tutorialDrop = Pickup.Drop(Pickup.Kind.Shield, e.pos, dir);
        else if (encIdx == 1 && !HasEmbers) tutorialDrop = Pickup.Drop(Pickup.Kind.Ember, e.pos, dir);
        if (tutorialDrop != null) tutorialHold = 30f;
    }

    public void OnBossDead(Enemy b) { StartCoroutine(BossDeathSeq(b)); }

    IEnumerator BossDeathSeq(Enemy b)
    {
        hud.HideBoss(); Sfx.drumsTarget = 0f;
        foreach (var e in alive) if (e != null && !e.dead) e.TakeHit(new Hit { dmg = 9999, knock = 6, launch = 8, dir = e.pos.x > b.pos.x ? 1 : -1, heavy = true });
        slowmo = true;
        for (int i = 0; i < 7; i++) { if (b != null) b.BossBurst(false); yield return new WaitForSecondsRealtime(0.28f); }
        slowmo = false;
        if (b != null) b.BossBurst(true);
        yield return new WaitForSecondsRealtime(0.8f);
        bossDead = true; inEnc = false; encIdx = encs.Length; CameraRig.I.Unlock();
        RunRecord.Finish(this, falls);   // stops the leaderboard clock and sends the run
        if (state == State.Playing) Victory();
    }

    void Victory()
    {
        state = State.Victory; stateT = 0; player.Celebrate();
        Sfx.Play("victory", 0.8f, 0); Sfx.Play("boom", 0.6f, 0, 1.4f);
        Fx.Ring(player.pos, 9f, new Color(1, 0.85f, 0.5f, 0.9f), 0.8f); CameraRig.I.Shake(0.3f, 0.5f);
        hud.HideGo(); hud.Banner("THE BONE WARDEN FALLS", 2f); StartCoroutine(BossVictory());
    }

    IEnumerator BossVictory()
    {
        yield return new WaitForSeconds(2.2f);
        hud.ShowVictory(runTime, runKills, runBestCombo, falls);
    }

    public void OnPlayerDead()
    {
        MarkRewardsDirty(); SaveRewards();   // nothing earned before the fatal blow is lost to the restart
        lesson = Lesson.None; lessonT = 0f;
        state = State.Dead; stateT = 0; falls++; Sfx.drumsTarget = 0; Sfx.Play("defeat", 0.7f, 0);
        RunRecord.Sync();
        hud.ShowDeath();
    }

    // ---- test hooks (used by automated play-testing through the Unity Pipeline) ----
    public static bool autopilot; public static float debugSpeed = 1f; float apT;
    void Autopilot()
    {
        GameInput.simActive = true; Vector2 mv = Vector2.zero;
        if (state == State.Title) { GameInput.Sim("confirm"); return; }
        if (state == State.Dead) { GameInput.Sim("restart"); return; }
        if (state != State.Playing) { GameInput.simMove = mv; return; }
        Enemy near = null; float bd = 99;
        foreach (var a in Actor.All) { var e = a as Enemy; if (e == null || e.dead) continue; float d = Vector2.Distance(e.pos, player.pos); if (d < bd) { bd = d; near = e; } }
        if (near != null)
        {
            Vector2 d = near.pos - player.pos; float want = near.IsBoss ? 1.7f : 1.0f;
            if (Mathf.Abs(d.x) > want) mv.x = Mathf.Sign(d.x); else if (player.facing != (d.x >= 0 ? 1 : -1)) mv.x = Mathf.Sign(d.x) * 0.3f;
            if (Mathf.Abs(d.y) > 0.25f) mv.y = Mathf.Sign(d.y);
            if (Mathf.Abs(d.x) < want + 0.5f && Mathf.Abs(d.y) < 0.6f)
            {
                apT -= Time.unscaledDeltaTime;
                if (apT <= 0) { apT = 0.13f; float r = Random.value; GameInput.Sim(r < 0.74f ? "attack" : r < 0.86f ? "heavy" : r < 0.93f ? "magic" : r < 0.97f ? "dodge" : "jump"); }
            }
        }
        else if (bossDead) { mv = Vector2.zero; }
        else
        {
            mv.x = 1;
            if (player.hp <= player.maxHp * .65f && player.potions > 0) GameInput.Sim("potion");
        }
        GameInput.simMove = mv;
    }
    public void DebugJump(int enc) { checkpoint = enc; Reload(true); }
    public string DebugInfo()
    {
        alive.RemoveAll(e => e == null);
        return $"state={state} enc={encIdx} inEnc={inEnc} wave={wave} alive={alive.Count} hp={player.hp} castsLeft={(player.hp - 1) / player.EmberCost} px={player.pos.x:F1} py={player.pos.y:F1} st={player.st} kills={runKills} t={runTime:F1} tokens={Enemy.tokens}";
    }
}
