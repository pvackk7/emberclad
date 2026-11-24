using System.Reflection;
using UnityEngine;
public static class VerifyTut
{
    static readonly BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static string Teach() { var h = Game.I.hud; var c = (Object)typeof(Hud).GetField("teachCard", F).GetValue(h); return c != null ? $"card(t={(float)typeof(Hud).GetField("teachT", F).GetValue(h):0.0})" : "none"; }
    static string Lesson() => typeof(Game).GetField("lesson", F).GetValue(Game.I) + " lt=" + ((float)typeof(Game).GetField("lessonT", F).GetValue(Game.I)).ToString("0.0") + " demo=" + typeof(Hud).GetField("hotbarDemo", F).GetValue(Game.I.hud);
    public static string S() => $"state={Game.I.state} lesson={Lesson()} teach={Teach()} sel={QuickItems.Selected}";
    public static string Pickup() { Game.I.OnPickup(global::Pickup.Kind.Ember); return S(); }
    public static string Sel(int s) { typeof(QuickItems).GetProperty("Selected").GetSetMethod(true).Invoke(null, new object[] { s }); return S(); }
    public static string FastForward(double secs) { typeof(Game).GetField("lessonT", F).SetValue(Game.I, (float)secs); return S(); }
    public static string Card() { Game.I.hud.TeachCard("Test", null, new[] { "J" }, new[] { "x" }, 999f); return S(); }
    public static string Die() { var p = Game.I.player; p.invuln = 0; p.TakeHit(new Hit { dmg = 99999, dir = 1 }); return S(); }
}
