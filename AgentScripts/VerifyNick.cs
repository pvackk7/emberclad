using System.Reflection;
using UnityEngine;
public static class VerifyNick
{
    static readonly BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    public static string last = "";
    public static string S() => $"state={Game.I.state} saved={RunRecord.HasSavedRun}({RunRecord.SavedNickname}) active={RunRecord.Active} ranked={RunRecord.Ranked} nick={RunRecord.Nickname} xp={Game.I.Experience} lv={Game.I.Level} kills={Game.runKills} deaths={Game.falls} playS={RunRecord.Elapsed:0.0} last={last}";
    public static string Clean()
    {
        foreach (var k in new[] { "Emberclad.Run.Nickname", "Emberclad.Run.Id", "Emberclad.Run.Token", "Emberclad.Run.PlayMs", "Emberclad.Run.Kills", "Emberclad.Run.Deaths" }) PlayerPrefs.DeleteKey(k);
        PlayerPrefs.SetInt("Emberclad.XP", 500); PlayerPrefs.Save();
        RunRecord.DevBase = "http://localhost:8788"; return S();
    }
    public static string Start() { RunRecord.DevBase = "http://localhost:8788"; Game.I.StartCoroutine(RunRecord.Start("EdKnight", (ok, m) => last = "start ok=" + ok + " " + m)); return "started"; }
    public static string Resume() { RunRecord.DevBase = "http://localhost:8788"; Game.I.StartCoroutine(RunRecord.Resume(m => last = "resume " + m)); return "resuming"; }
    public static string Play() { typeof(Game).GetMethod("Reload", F).Invoke(Game.I, new object[] { true }); return "reload"; }
    public static string Earn()
    {
        typeof(Game).GetProperty("Experience").SetValue(Game.I, 960); Game.runKills = 17;
        RunRecord.Sync(); return S();
    }
    public static string Die() { var p = Game.I.player; p.invuln = 0; p.TakeHit(new Hit { dmg = 99999, dir = 1 }); return S(); }
    public static string Menu() { Game.I.ReturnToMenu(); return "menu"; }
    public static string MenuLabel()
    {
        var hud = Game.I.hud; var mc = (Transform)typeof(Hud).GetField("menuContent", F).GetValue(hud);
        string t = ""; foreach (var tx in mc.GetComponentsInChildren<UnityEngine.UI.Text>()) t += tx.text + "|"; return t;
    }
}
