using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Bootstraps the level at runtime and runs the flow: title -> encounters -> boss -> shrine -> victory.
[DefaultExecutionOrder(-100)]
public class Game : MonoBehaviour
{
    public static Game I;
    public enum State { Title, Playing, Dead, Victory }
    public State state = State.Title;
    public Player player; public Companion khalid; public Hud hud;
    public int maxTokens = 2;
    public int Cinders { get; private set; }
    public int Experience { get; private set; }
    public bool HasShield { get; private set; }

    // Levelling: each one costs a flat step more than the last, so early levels
    // come fast enough to feel like progress and later ones still mean something.
    public const int XpBase = 120, XpStep = 90;
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
    bool rewardsDirty;
    float rewardSaveTimer;
    static bool sessionRewardsLoaded;
    static int sessionCinders, sessionExperience;
    static bool sessionShield;

    void CacheRewards()
    {
        sessionCinders = Cinders;
        sessionExperience = Experience;
        sessionShield = HasShield;
        sessionRewardsLoaded = true;
    }

    void SaveRewards()
    {
        CacheRewards();
        if (!rewardsDirty) return;
        PlayerPrefs.SetInt("Emberclad.Cinders", Cinders);
        PlayerPrefs.SetInt("Emberclad.Experience", Experience);
        PlayerPrefs.SetInt("Emberclad.Shield", HasShield ? 1 : 0);
        PlayerPrefs.Save();
        rewardsDirty = false;
    }

    void OnApplicationPause(bool paused) { if (paused) SaveRewards(); }
    void OnApplicationQuit() { SaveRewards(); }
    void OnDestroy() { SaveRewards(); }
    public static int checkpoint; public static bool skipTitle; public static float runTime; public static int falls, runKills, runBestCombo;

    class Enc { public float x; public string[] waves; public Enc(float x, string w) { this.x = x; waves = w.Split('|'); } }
    readonly Enc[] encs =
    {
        new Enc(16f, "G3"),
        new Enc(37f, "G2 R1|G3 R1"),
        new Enc(59f, "R3|G2 B1|G3 R1"),
        new Enc(83f, "G1 R2|G2 B1|B1 R3"),
        new Enc(106f, "G2 R2|B1 G2|R2 G2"),
        new Enc(129f, "X1"),
    };
    int encIdx, wave; bool inEnc, spawning, bossDead, paused, slowmo; float waveDelay, hitStop, stateT, campfirePrompt;
    readonly List<Enemy> alive = new List<Enemy>(); Enemy boss;
    public bool CanUseItems => state == State.Playing && !paused && player != null && !player.dead && hud != null && !hud.IntroOpen;
    public bool InCombat => inEnc;
    public int EncounterIndex => encIdx;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { autopilot = false; debugSpeed = 1f; checkpoint = 0; skipTitle = false; runTime = 0; falls = 0; runKills = 0; runBestCombo = 0; sessionRewardsLoaded = false; }

    void Awake()
    {
        if (!sessionRewardsLoaded)
        {
            sessionCinders = PlayerPrefs.GetInt("Emberclad.Cinders", 0);
            sessionExperience = PlayerPrefs.GetInt("Emberclad.Experience", 0);
            sessionShield = PlayerPrefs.GetInt("Emberclad.Shield", 0) == 1;
            sessionRewardsLoaded = true;
        }
        Cinders = sessionCinders;
        Experience = sessionExperience;
        HasShield = sessionShield;
        I = this; Time.timeScale = 1; Enemy.tokens = 0; Actor.All.Clear();
        Application.targetFrameRate = 60;
        Art.Load();
        var cam = Camera.main;
        cam.orthographic = true; cam.orthographicSize = 4.5f; cam.backgroundColor = new Color(0.12f, 0.24f, 0.26f, 1); cam.clearFlags = CameraClearFlags.SolidColor;
        var rig = cam.GetComponent<CameraRig>(); if (rig == null) rig = cam.gameObject.AddComponent<CameraRig>();
        if (cam.GetComponent<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
        new GameObject("Fx").AddComponent<Fx>();
        Sfx.Init(gameObject);
        World.Build(new GameObject("World").transform, cam.transform);
        encIdx = Mathf.Clamp(checkpoint, 0, encs.Length - 1);
        float startX = checkpoint > 0 ? encs[encIdx].x - 7f : 0.5f;
        player = Player.Create(new Vector2(startX, -2.6f));
        khalid = Companion.Create(new Vector2(startX - 1.1f, -2.3f));
        hud = new GameObject("HUD").AddComponent<Hud>(); hud.Build(this);
        rig.SnapTo(startX + 1.4f);
        if (encIdx >= 3) maxTokens = 3;
        if (skipTitle) { state = State.Playing; player.SkipIntro(); hud.HideTitle(true); }
        else { state = State.Title; hud.ShowTitle(); }
    }

    public void MenuPause(bool value) { paused = value; Time.timeScale = value ? 0 : 1; }
    public void StartFromMenu() { state = State.Playing; stateT = 0; hud.HideTitle(true); player.Begin(); Sfx.Play("ui_start", 0.6f, 0); hud.BeginIntro(); }
    public void ReturnToMenu() { SaveRewards(); skipTitle = false; checkpoint = 0; Time.timeScale = 1; SceneManager.LoadScene(SceneManager.GetActiveScene().name); }
    public void HitStop(float d) { hitStop = Mathf.Max(hitStop, d); }

    void Update()
    {
        if (rewardsDirty && (rewardSaveTimer -= Time.unscaledDeltaTime) <= 0) SaveRewards();
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
                TickEncounters();
                break;
            case State.Dead:
                if (stateT > 1.2f && (GameInput.Restart || GameInput.Confirm)) Reload(true);
                break;
            case State.Victory:
                if (stateT > 1.5f && GameInput.Restart) { checkpoint = 0; runTime = 0; falls = 0; runKills = 0; runBestCombo = 0; Reload(true); }
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
            else if (bossDead && state == State.Playing && Vector2.Distance(player.pos, (Vector2)World.shrine.position) < 1.8f)
            {
                campfirePrompt -= Time.deltaTime; GameInput.ClaimInteract();
                if (campfirePrompt <= 0) { hud.Banner("E / Y — LIGHT THE CAMPFIRE", 2.5f); campfirePrompt = 2f; }
                if (GameInput.LightCampfire) Victory();
            }
            else campfirePrompt = 0;
            return;
        }
        if (spawning) return;
        alive.RemoveAll(e => e == null || e.dead);
        if (alive.Count > 0) return;
        if (encs[encIdx].waves[0] == "X1") return;   // boss fight ends through OnBossDead
        waveDelay -= Time.deltaTime;
        if (waveDelay > 0) return;
        wave++;
        if (wave < encs[encIdx].waves.Length) StartCoroutine(SpawnWave(encs[encIdx].waves[wave], encs[encIdx].x + 2f));
        else EndEncounter();
    }

    void BeginEncounter()
    {
        inEnc = true; wave = 0; var e = encs[encIdx]; float center = e.x + 2f;
        CameraRig.I.Lock(center); Sfx.drumsTarget = 0.5f; hud.HideGo();
        if (encIdx >= 3) maxTokens = 3;
        if (e.waves[0] == "X1") StartCoroutine(BossIntro(center));
        else { StartCoroutine(SpawnWave(e.waves[0], center)); if (encIdx == 0) hud.Banner("SKELETONS!", 1.6f); }
    }

    IEnumerator SpawnWave(string spec, float center)
    {
        spawning = true; waveDelay = 0.9f;
        yield return new WaitForSeconds(wave == 0 ? 0.5f : 0.2f);
        Sfx.Play("spawn", 0.7f);
        int n = 0; float hw = CameraRig.I.HalfW;
        foreach (var part in spec.Split(' '))
        {
            var kind = part[0] == 'R' ? Enemy.Kind.Runt : part[0] == 'B' ? Enemy.Kind.Brute : Enemy.Kind.Grunt;
            int count = int.Parse(part.Substring(1));
            for (int i = 0; i < count; i++)
            {
                Vector2 p = Vector2.zero;
                for (int tries = 0; tries < 12; tries++)
                {
                    int side = (n % 2 == 0) ? 1 : -1; if (tries > 5) side = -side;
                    p = new Vector2(center + side * Random.Range(2.5f, hw - 1.2f), Random.Range(World.MinY + 0.2f, World.MaxY - 0.1f));
                    if (Mathf.Abs(p.x - player.pos.x) > 2.6f) break;
                }
                alive.Add(Enemy.Create(kind, p)); n++;
                yield return new WaitForSeconds(0.14f);
            }
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
        khalid.Cheer(1.5f);
    }

    public void OnEnemyKilled(Enemy e)
    {
        if (e == null || !e.dead || e.rewardGranted) return;
        e.rewardGranted = true;
        player.kills++; runKills++;
        int cinders = e.IsBoss ? 250 : e.kind == Enemy.Kind.Brute ? 30 : e.kind == Enemy.Kind.Runt ? 5 : 10;
        int xp = e.IsBoss ? 500 : e.kind == Enemy.Kind.Brute ? 60 : e.kind == Enemy.Kind.Runt ? 10 : 20;
        int before = Level;
        Cinders += cinders;
        Experience += xp;
        CacheRewards();
        if (!rewardsDirty) rewardSaveTimer = 2f;
        rewardsDirty = true;
        if (Level > before) SaveRewards();
        if (hud != null)
        {
            // Spray scales with what the thing was worth: a runt is a handful of
            // coins, the warden is a fountain.
            float weight = e.IsBoss ? 1f : e.kind == Enemy.Kind.Brute ? .55f : e.kind == Enemy.Kind.Runt ? 0f : .25f;
            hud.BurstCinders(e.transform.position + new Vector3(0, .9f, 0), cinders, weight);
            hud.ShowRewards(cinders, xp);
            if (Level > before) { hud.Banner("LEVEL " + Level, 2f); Sfx.Play("mana", .7f, 0, 1.15f); }
        }
        // Shield-bearers leave the shield behind. It is the only thing they had.
        if (e.hasShield && !HasShield)
        {
            Pickup.Drop(Pickup.Kind.Shield, e.pos, e.pos.x < player.pos.x ? -1 : 1);
            e.ReleaseShield();
        }
    }

    public void OnPickup(Pickup.Kind kind)
    {
        if (kind == Pickup.Kind.Shield)
        {
            HasShield = true;
            CacheRewards();
            if (!rewardsDirty) rewardSaveTimer = 2f;
            rewardsDirty = true;
            if (player != null) player.EquipShield();
            if (hud != null) hud.Banner("SHIELD RECOVERED", 2.2f);
        }
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
        hud.Banner("LIGHT THE CAMPFIRE", 2.6f); hud.ShowGo(); khalid.Cheer(2f);
    }

    void Victory()
    {
        state = State.Victory; stateT = 0; player.Celebrate(); khalid.Cheer(60f);
        Sfx.Play("victory", 0.8f, 0); Sfx.Play("boom", 0.6f, 0, 1.4f);
        World.shrine.GetComponent<EndCampfire>().Light();
        Vector2 s = (Vector2)World.shrine.position + new Vector2(0, .45f);
        Fx.Ring(s + new Vector2(0, -.4f), 9f, new Color(1, 0.85f, 0.5f, 0.9f), 0.8f); CameraRig.I.Shake(0.3f, 0.5f);
        hud.HideGo(); hud.Banner("CAMPFIRE LIT", 2f); StartCoroutine(CampfireVictory());
    }

    IEnumerator CampfireVictory()
    {
        yield return new WaitForSeconds(2.2f);
        hud.ShowVictory(runTime, runKills, runBestCombo, falls);
    }

    public void OnPlayerDead()
    {
        SaveRewards();
        state = State.Dead; stateT = 0; falls++; Sfx.drumsTarget = 0; Sfx.Play("defeat", 0.7f, 0);
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
        else if (bossDead) { Vector2 d = new Vector2(136.5f, -2.5f) - player.pos; mv = d.magnitude > 0.3f ? d.normalized : Vector2.zero; if (d.magnitude < .4f) GameInput.Sim("lightCampfire"); }
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
        return $"state={state} enc={encIdx} inEnc={inEnc} wave={wave} alive={alive.Count} hp={player.hp} mana={player.mana} px={player.pos.x:F1} py={player.pos.y:F1} st={player.st} kills={runKills} t={runTime:F1} tokens={Enemy.tokens}";
    }
}
