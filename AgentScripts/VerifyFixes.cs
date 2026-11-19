using System.Reflection;
using UnityEngine;

// Play-mode checks for the XP persistence, level damage scaling and tutorial fixes. Run step by step over the Pipeline.
public static class VerifyFixes
{
    static readonly BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static object Call(object o, string m, params object[] a) => o.GetType().GetMethod(m, F).Invoke(o, a);

    public static string Formulas()
    {
        return $"L1 dealt={Game.DamageDealtScaleFor(1)} taken={Game.DamageTakenScaleFor(1)} | L3 {Game.DamageDealtScaleFor(3)}/{Game.DamageTakenScaleFor(3)} | L5 {Game.DamageDealtScaleFor(5)}/{Game.DamageTakenScaleFor(5)} | L20 {Game.DamageDealtScaleFor(20)}/{Game.DamageTakenScaleFor(20)}";
    }

    public static string State()
    {
        var g = Game.I; if (g == null) return "no game";
        return $"state={g.state} xp={g.Experience} level={g.Level} prefsXP={PlayerPrefs.GetInt("Emberclad.XP", -1)} hp={g.player.hp}/{g.player.maxHp} dead={g.player.dead} falls={Game.falls} lesson={g.LessonActive} intro={g.hud.IntroOpen}";
    }

    public static string Start() { Game.I.StartFromMenu(); return State(); }

    /// Kill every enemy on screen with a real player hit, through the normal reward path.
    public static string KillAll()
    {
        int n = 0; int before = Game.I.Experience;
        foreach (var a in Actor.All.ToArray())
        {
            var e = a as Enemy; if (e == null || e.dead || e.Invulnerable) continue;
            e.TakeHit(new Hit { dmg = 9999, knock = 1, dir = 1, from = Game.I.player }); n++;
        }
        return $"killed={n} xpGain={Game.I.Experience - before} " + State();
    }

    /// Measure scaling on a live hit: player lands a 10-dmg blow, enemy lands a 20-dmg blow.
    public static string MeasureHits()
    {
        var g = Game.I; string r = "";
        foreach (var a in Actor.All.ToArray())
        {
            var e = a as Enemy; if (e == null || e.dead || e.Invulnerable) continue;
            int hp = e.hp; e.TakeHit(new Hit { dmg = 10, dir = 1, from = g.player }); r += $"enemyLost={hp - e.hp} ";
            break;
        }
        var p = g.player; int php = p.hp;
        typeof(Player).GetField("invuln", F)?.SetValue(p, 0f);
        p.TakeHit(new Hit { dmg = 20, dir = 1 }); r += $"playerLost={php - p.hp} (level {g.Level})";
        return r;
    }

    public static string SetXp(int xp)
    {
        typeof(Game).GetProperty("Experience").SetValue(Game.I, xp);
        Call(Game.I, "MarkRewardsDirty");
        return State();
    }

    public static string Die()
    {
        var p = Game.I.player;
        typeof(Player).GetField("invuln", F)?.SetValue(p, 0f);
        p.TakeHit(new Hit { dmg = 99999, dir = 1 });
        return State();
    }

    public static string Restart() { Game.checkpoint = 0; Call(Game.I, "Reload", true); return "reloading"; }

    public static string ShowEmberLesson() { Game.I.OnPickup(Pickup.Kind.Ember); return State() + " teach=" + TeachUp(); }

    public static string TeachUp()
    {
        var f = typeof(Hud).GetField("teachCard", F); var t = (Object)f.GetValue(Game.I.hud);
        var tt = typeof(Hud).GetField("teachT", F).GetValue(Game.I.hud);
        return t != null ? $"visible(t={tt:0.0})" : "none";
    }

    public static string SelectSlot(int s)
    {
        var prop = typeof(QuickItems).GetProperty("Selected", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (prop != null && prop.CanWrite) prop.SetValue(null, s);
        else typeof(QuickItems).GetField("Selected", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.SetValue(null, s);
        return "selected=" + QuickItems.Selected;
    }
}
public static class VerifyFixes2
{
    public static string Auto(bool on) { Game.autopilot = on; Game.debugSpeed = on ? 2f : 1f; return "auto=" + on + " " + VerifyFixes.State() + " enemies=" + Enemies(); }
    public static int Enemies() { int n = 0; foreach (var a in Actor.All) if (a is Enemy e && !e.dead) n++; return n; }
}
