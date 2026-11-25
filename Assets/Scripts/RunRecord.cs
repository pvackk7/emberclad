using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// One knight's run on the leaderboard.
///
/// A nickname is a knight. Entering one starts a run that belongs to that name until the Bone
/// Warden falls: exiting, dying or closing the tab never frees it, and Play comes straight back
/// to the same knight. Everything earned under the name (XP/level, felled, deaths, play time)
/// is kept in PlayerPrefs and saved to the site as it plays.
///   Name entered   -> POST /api/runs/start {nickname}             -> {runId, token}   (joins the board at once)
///   Play again     -> POST /api/runs/resume {runId, token}        -> saved progress
///   While playing  -> POST /api/runs/progress {..stats, playMs}   every PingSeconds, and on deaths/level-ups/exits
///   Warden dies    -> POST /api/runs/finish {..stats, clientTimeMs} -> {runMs, deaths, rank}; the name is released
/// Run time is play time (menus and time away don't count). If the site can't be reached the
/// knight still plays and syncs when it can. Statics survive the scene reloads deaths use.
public static class RunRecord
{
    public const int MinNickLength = 2, MaxNickLength = 16;
    const string NickPref = "Emberclad.LastNickname";
    // The knight currently riding. Cleared only when the Warden falls.
    const string RunNickPref = "Emberclad.Run.Nickname", RunIdPref = "Emberclad.Run.Id", RunTokenPref = "Emberclad.Run.Token";
    const string RunPlayPref = "Emberclad.Run.PlayMs", RunKillsPref = "Emberclad.Run.Kills", RunDeathsPref = "Emberclad.Run.Deaths";
    const float PingSeconds = 10f;

    /// A knight is mid-run on this browser: Play continues it instead of asking for a name.
    public static bool HasSavedRun { get { return !string.IsNullOrEmpty(PlayerPrefs.GetString(RunNickPref, "")); } }
    public static string SavedNickname { get { return PlayerPrefs.GetString(RunNickPref, ""); } }

    // WebGL is served from the site, so it uses the same origin. The editor talks to the
    // local Worker (wrangler dev); desktop builds use the live site.
    public static string LiveBase = "https://emberclad.org";
    public static string DevBase = "http://localhost:8787";
    static string Base
    {
        get
        {
            if (Application.platform == RuntimePlatform.WebGLPlayer) return "";
            return Application.isEditor ? DevBase : LiveBase;
        }
    }

    public static string Nickname { get; private set; }
    public static bool Active { get; private set; }       // a run is being timed
    public static bool Ranked { get; private set; }       // the site accepted the start
    static string runId, token;

    public static string ResultText { get; private set; } = "";
    public static string SaveHeading { get; private set; } = "";   // RUN SAVED / SAVING RUN... / RUN NOT SAVED, shown above ResultText
    public static bool Finished { get; private set; }
    public static int LastClientMs { get; private set; }

    public static string LastNickname { get { return PlayerPrefs.GetString(NickPref, ""); } }

    static bool Allowed(char c) { return char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.' || c == ' '; }
    public static bool AllowedChar(char c) { return Allowed(c); }

    public static string Clean(string raw) { return (raw ?? "").Trim(); }

    public static bool Validate(string nick, out string error)
    {
        error = null;
        if (nick.Length < MinNickLength || nick.Length > MaxNickLength) { error = "Names are 2 to 16 characters."; return false; }
        if (!char.IsLetterOrDigit(nick[0])) { error = "Start the name with a letter or a number."; return false; }
        foreach (char c in nick) if (!Allowed(c)) { error = "Use letters, numbers, spaces, _ . or -"; return false; }
        if (!NameFilter.IsAllowed(nick, out error)) return false;   // the site re-checks this; see NameFilter
        return true;
    }

    [Serializable] class StartBody { public string nickname; }
    [Serializable] class StartReply { public string runId; public string token; public double startedAt; }
    [Serializable] class FinishBody { public string runId; public string token; public int deaths; public int kills; public int xp; public int level; public int clientTimeMs; }
    [Serializable] class ProgressBody { public string runId; public string token; public int deaths; public int kills; public int xp; public int level; public int playMs; }
    [Serializable] class ResumeBody { public string runId; public string token; }
    [Serializable] class ResumeReply { public string nickname; public int xp; public int level; public int kills; public int deaths; public int playMs; public bool finished; }
    [Serializable] class FinishReply { public int runMs; public int deaths; public int rank; }
    [Serializable] class ErrorReply { public string error; }

    /// Begin was pressed. Calls done(ok, message): ok=false with a message keeps the player on
    /// the name page (name taken, bad name, rate limit); ok=true starts the game, with a
    /// message if the run will not be ranked (site unreachable).
    public static IEnumerator Start(string nickname, Action<bool, string> done)
    {
        Nickname = nickname; Finished = false; ResultText = ""; SaveHeading = "";
        string json = JsonUtility.ToJson(new StartBody { nickname = nickname });
        using (var req = Post("/api/runs/start", json))
        {
            yield return req.SendWebRequest();
            long code = req.responseCode;
            if (req.result == UnityWebRequest.Result.Success && code == 200)
            {
                var reply = JsonUtility.FromJson<StartReply>(req.downloadHandler.text);
                runId = reply.runId; token = reply.token; Ranked = !string.IsNullOrEmpty(runId);
                PlayerPrefs.SetString(NickPref, nickname);
                NewKnight(nickname);
                done(true, null);
                yield break;
            }
            if (code == 400 || code == 409 || code == 429)
            {
                string msg = ErrorOf(req);
                if (code == 409 && string.IsNullOrEmpty(msg)) msg = "That name already belongs to another knight. Pick another.";
                if (code == 429 && string.IsNullOrEmpty(msg)) msg = "Too many runs from here. Try again later.";
                done(false, string.IsNullOrEmpty(msg) ? "That name can't be used." : msg);
                yield break;
            }
            // Offline or the site is down: play anyway, unranked.
            Debug.LogWarning("Leaderboard start failed: " + req.error + " (" + code + ")");
            runId = token = null; Ranked = false;
            NewKnight(nickname);
            done(true, "The leaderboard can't be reached, so this run won't be ranked.");
        }
    }

    /// A fresh knight: nothing carries over from the last name.
    static void NewKnight(string nickname)
    {
        Nickname = nickname;
        PlayerPrefs.SetString(RunNickPref, nickname);
        PlayerPrefs.SetString(RunIdPref, runId ?? ""); PlayerPrefs.SetString(RunTokenPref, token ?? "");
        playMs = 0; Game.falls = 0; Game.runKills = 0; Game.runBestCombo = 0;
        Game.ResetKnightProgress();
        SaveLocal();
        Active = true;
    }

    /// Play was pressed with a knight mid-run: pick up where it left off. The site's copy wins
    /// where it is further along (another tab, or local storage that was partly lost).
    public static IEnumerator Resume(Action<string> done)
    {
        Nickname = SavedNickname; Finished = false; ResultText = ""; SaveHeading = "";
        runId = PlayerPrefs.GetString(RunIdPref, ""); token = PlayerPrefs.GetString(RunTokenPref, "");
        playMs = PlayerPrefs.GetInt(RunPlayPref, 0);
        Game.falls = PlayerPrefs.GetInt(RunDeathsPref, 0); Game.runKills = PlayerPrefs.GetInt(RunKillsPref, 0);
        Active = true; Ranked = !string.IsNullOrEmpty(runId);
        if (!Ranked) { done("This knight isn't on the leaderboard (the site couldn't be reached when it was named)."); yield break; }
        string json = JsonUtility.ToJson(new ResumeBody { runId = runId, token = token });
        using (var req = Post("/api/runs/resume", json))
        {
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success && req.responseCode == 200)
            {
                var r = JsonUtility.FromJson<ResumeReply>(req.downloadHandler.text);
                if (r.finished) { Clear(); done(null); yield break; }   // finished elsewhere: the menu will ask for a new name
                playMs = Mathf.Max(playMs, r.playMs);
                Game.falls = Mathf.Max(Game.falls, r.deaths); Game.runKills = Mathf.Max(Game.runKills, r.kills);
                Game.RestoreKnightXp(r.xp);
                SaveLocal();
                done(null); yield break;
            }
            if (req.responseCode == 403 || req.responseCode == 404)
            {
                // The site no longer knows this knight (released after a month idle): keep playing it, unranked.
                Ranked = false; runId = token = null;
                PlayerPrefs.SetString(RunIdPref, ""); PlayerPrefs.SetString(RunTokenPref, ""); PlayerPrefs.Save();
                done("This knight is no longer on the leaderboard, so this run won't be ranked."); yield break;
            }
            Debug.LogWarning("Leaderboard resume failed: " + req.error + " (" + req.responseCode + ")");
            done("The leaderboard can't be reached right now. Progress is kept here and sent when it can be.");
        }
    }

    static int playMs;
    static float pingT, playCarry;

    /// Called every frame by Game. Play time only runs while actually in the forest (death
    /// screens count, menus and pauses don't); a progress ping goes out every PingSeconds.
    public static void Tick(bool playing)
    {
        if (!Active) return;
        if (playing)
        {
            playCarry += Time.unscaledDeltaTime * 1000f;
            int whole = (int)playCarry; playMs += whole; playCarry -= whole;
            pingT += Time.unscaledDeltaTime;
            if (pingT >= PingSeconds) Sync();
        }
    }

    /// Save locally and send progress now (deaths, level-ups, leaving to the menu).
    public static void Sync()
    {
        pingT = 0f;
        if (!Active) return;
        SaveLocal();
        if (!Ranked || string.IsNullOrEmpty(runId)) return;
        var g = Game.I;
        string json = JsonUtility.ToJson(new ProgressBody { runId = runId, token = token, deaths = Game.falls, kills = Game.runKills,
            xp = g != null ? g.Experience : 0, level = g != null ? g.Level : 1, playMs = playMs });
        Runner.StartCoroutine(SendProgress(json));
    }

    static IEnumerator SendProgress(string json)
    {
        using (var req = Post("/api/runs/progress", json))
        {
            yield return req.SendWebRequest();
            if (req.responseCode == 409) { Debug.Log("Leaderboard: run already finished on the site."); }
        }
    }

    static void SaveLocal()
    {
        PlayerPrefs.SetInt(RunPlayPref, playMs); PlayerPrefs.SetInt(RunKillsPref, Game.runKills); PlayerPrefs.SetInt(RunDeathsPref, Game.falls);
        Game.SaveSpot();
        PlayerPrefs.Save();
    }

    /// The run is over: the name is released and the next Play asks for a new one.
    static void Clear()
    {
        foreach (var k in new[] { RunNickPref, RunIdPref, RunTokenPref, RunPlayPref, RunKillsPref, RunDeathsPref }) PlayerPrefs.DeleteKey(k);
        Game.ClearSpot();
        PlayerPrefs.Save();
    }

    /// Leaving to the menu no longer throws the run away: it is saved and continues on the next Play.
    public static void Abandon() { if (Active) Sync(); Active = false; }

    public static float Elapsed { get { return playMs / 1000f; } }

    /// The Bone Warden is dead: stop the clock and send the run.
    public static void Finish(MonoBehaviour host, int deaths)
    {
        if (!Active) { SaveHeading = "RUN NOT SAVED"; ResultText = "No name was entered for this run."; return; }
        int ms = playMs;
        LastClientMs = ms;
        Active = false; Finished = true;
        Clear();
        if (!Ranked) { SaveHeading = "RUN NOT SAVED"; ResultText = "The leaderboard couldn't be reached."; return; }
        if (Cheats.On) { SaveHeading = "RUN NOT SAVED"; ResultText = "Cheats were on."; Ranked = false; return; }
        var g = Game.I;
        Runner.StartCoroutine(SendFinish(ms, deaths, Game.runKills, g != null ? g.Experience : 0, g != null ? g.Level : 1));   // survives the scene reload if R is pressed quickly
    }

    static IEnumerator SendFinish(int ms, int deaths, int kills, int xp, int level)
    {
        SaveHeading = "SAVING RUN..."; ResultText = Nickname;
        string json = JsonUtility.ToJson(new FinishBody { runId = runId, token = token, deaths = deaths, kills = kills, xp = xp, level = level, clientTimeMs = ms });
        using (var req = Post("/api/runs/finish", json))
        {
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success && req.responseCode == 200)
            {
                var r = JsonUtility.FromJson<FinishReply>(req.downloadHandler.text);
                SaveHeading = "RUN SAVED";
                ResultText = Nickname + "  ·  Rank #" + r.rank + "  ·  " + FormatMs(r.runMs) + "  ·  " + r.deaths + (r.deaths == 1 ? " death" : " deaths");
            }
            else
            {
                string msg = ErrorOf(req);
                SaveHeading = "RUN NOT SAVED";
                ResultText = string.IsNullOrEmpty(msg) ? "The leaderboard couldn't be reached." : msg;
                Debug.LogWarning("Leaderboard finish failed: " + req.error + " (" + req.responseCode + ") " + msg);
            }
        }
        runId = token = null; Ranked = false;
    }

    sealed class RunnerBehaviour : MonoBehaviour { }
    static RunnerBehaviour runner;
    static MonoBehaviour Runner
    {
        get
        {
            if (runner == null)
            {
                var go = new GameObject("Leaderboard requests");
                UnityEngine.Object.DontDestroyOnLoad(go);
                runner = go.AddComponent<RunnerBehaviour>();
            }
            return runner;
        }
    }

    static UnityWebRequest Post(string path, string json)
    {
        var req = new UnityWebRequest(Base + path, "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.timeout = 10;
        return req;
    }

    static string ErrorOf(UnityWebRequest req)
    {
        try { var e = JsonUtility.FromJson<ErrorReply>(req.downloadHandler?.text ?? ""); return e?.error; }
        catch { return null; }
    }

    public static string FormatMs(int ms)
    {
        int total = Mathf.Max(0, ms / 1000), cs = (ms % 1000) / 10;
        int h = total / 3600, m = (total / 60) % 60, s = total % 60;
        return (h > 0 ? h + ":" + m.ToString("00") : m.ToString()) + ":" + s.ToString("00") + "." + cs.ToString("00");
    }
}
