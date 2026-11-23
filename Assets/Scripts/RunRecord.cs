using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// One leaderboard run, timed by the site so times can't be faked.
///
/// Flow (API from the website thread, 2026-11-23):
///   Begin pressed  -> POST /api/runs/start {nickname}            -> {runId, token, startedAt}
///   Warden dies    -> POST /api/runs/finish {runId, token, deaths, clientTimeMs} -> {runMs, deaths, rank}
/// clientTimeMs is our own wall clock since start (pauses and deaths included); the board
/// uses the server's time. Deaths are Game.falls. Quitting to the menu throws the run away.
/// If start can't reach the site the player still plays, unranked. Statics survive the
/// scene reloads that deaths and checkpoints use.
public static class RunRecord
{
    public const int MinNickLength = 2, MaxNickLength = 16;
    const string NickPref = "Emberclad.LastNickname";

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
    static double startedAt;

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
        return true;
    }

    [Serializable] class StartBody { public string nickname; }
    [Serializable] class StartReply { public string runId; public string token; public double startedAt; }
    [Serializable] class FinishBody { public string runId; public string token; public int deaths; public int clientTimeMs; }
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
                PlayerPrefs.SetString(NickPref, nickname); PlayerPrefs.Save();
                BeginClock();
                done(true, null);
                yield break;
            }
            if (code == 400 || code == 409 || code == 429)
            {
                string msg = ErrorOf(req);
                if (code == 409 && string.IsNullOrEmpty(msg)) msg = "That name is already on the leaderboard. Pick another.";
                if (code == 429 && string.IsNullOrEmpty(msg)) msg = "Too many runs from here. Try again later.";
                done(false, string.IsNullOrEmpty(msg) ? "That name can't be used." : msg);
                yield break;
            }
            // Offline or the site is down: play anyway, unranked.
            Debug.LogWarning("Leaderboard start failed: " + req.error + " (" + code + ")");
            runId = token = null; Ranked = false;
            BeginClock();
            done(true, "The leaderboard can't be reached, so this run won't be ranked.");
        }
    }

    static void BeginClock()
    {
        Active = true;
        startedAt = Time.realtimeSinceStartupAsDouble;
        Game.falls = 0;
    }

    public static void Abandon() { Active = false; Ranked = false; runId = token = null; }

    public static float Elapsed { get { return Active ? (float)(Time.realtimeSinceStartupAsDouble - startedAt) : 0f; } }

    /// The Bone Warden is dead: stop the clock and send the run.
    public static void Finish(MonoBehaviour host, int deaths)
    {
        if (!Active) { SaveHeading = "RUN NOT SAVED"; ResultText = "No name was entered for this run."; return; }
        int ms = Mathf.RoundToInt(Elapsed * 1000f);
        LastClientMs = ms;
        Active = false; Finished = true;
        if (!Ranked) { SaveHeading = "RUN NOT SAVED"; ResultText = "The leaderboard couldn't be reached."; return; }
        if (Cheats.On) { SaveHeading = "RUN NOT SAVED"; ResultText = "Cheats were on."; Ranked = false; return; }
        Runner.StartCoroutine(SendFinish(ms, deaths));   // survives the scene reload if R is pressed quickly
    }

    static IEnumerator SendFinish(int ms, int deaths)
    {
        SaveHeading = "SAVING RUN..."; ResultText = Nickname;
        string json = JsonUtility.ToJson(new FinishBody { runId = runId, token = token, deaths = deaths, clientTimeMs = ms });
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
