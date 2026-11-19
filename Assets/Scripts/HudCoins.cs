using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// Cinders: the spray of grave-coins a corpse gives up, and the plate they land in.
///
/// The coins are pure theatre -- nothing physical drops in the world. They burst
/// out of the body, hang, then get pulled into the counter in the bottom-right,
/// each one adding its share as it arrives. A runt coughs up a handful; the boss
/// throws a fountain. The size of the spray is the readout.
public partial class Hud
{
    RectTransform cinderPlate, coinLayer;
    static readonly Vector2 PlateSize = new Vector2(212, 60);
    // Coin icon centre, relative to the plate's bottom-right corner. The burst
    // aims here too, so the spray lands on the icon rather than beside it.
    static readonly Vector2 CoinSlot = new Vector2(-(PlateSize.x - 38), PlateSize.y / 2);
    Text cinderText;
    int cinderShown;
    float platePunch, plateFlash;

    class Coin
    {
        public RectTransform rt; public Image img;
        public Vector2 from, ctrl, to;
        public float t, delay, dur, spin, spin0, scale;
        public int value; public bool active;
    }
    readonly List<Coin> coins = new List<Coin>();

    void BuildCinders()
    {
        // Coins fly under the plate so they vanish *behind* it as they land.
        coinLayer = Full("Coin burst", root);
        var BR = new Vector2(1, 0);
        // One quiet row: coin on the left, count on the right, nothing else.
        // The icon says what the number is; a caption would only repeat it.
        cinderPlate = FramedPanel("Cinders", root, BR, new Vector2(-34, 34), PlateSize);
        cinderPlate.pivot = BR;
        Img("ui_coin", cinderPlate, BR, CoinSlot, new Vector2(40, 40), Color.white, new Vector2(0.5f, 0.5f));
        cinderText = Txt("0", cinderPlate, BR, new Vector2(-20, PlateSize.y / 2), new Vector2(140, 40), 32, Gold, TextAnchor.MiddleRight, new Vector2(1, 0.5f));
    }

    Coin FreeCoin()
    {
        foreach (var c in coins) if (!c.active) return c;
        var n = new Coin();
        // Centre anchor on purpose: coins then live in the same origin-at-screen-centre
        // space that ScreenPointToLocalPointInRectangle hands back for the corpse.
        n.img = Img("ui_coin", coinLayer, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34, 34), Color.white, new Vector2(0.5f, 0.5f));
        n.rt = n.img.rectTransform;
        coins.Add(n); return n;
    }

    /// worldPos: where the body fell. amount: cinders owed. weight: 0 runt .. 1 boss.
    public void BurstCinders(Vector3 worldPos, int amount, float weight)
    {
        if (coinLayer == null || amount <= 0) return;
        var cam = Camera.main;
        if (cam == null) return;
        Vector2 screen = cam.WorldToScreenPoint(worldPos);
        Vector2 from;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out from)) return;
        Vector2 to = cinderPlate.anchoredPosition + CoinSlot
                   + new Vector2(root.rect.width / 2, -root.rect.height / 2);

        int n = Mathf.Clamp(Mathf.RoundToInt(5 + amount * .5f), 5, 40);
        float spread = 95f + weight * 190f;     // how wide the spray throws
        float hang = .34f + weight * .42f;      // how long it lingers before being pulled in
        for (int i = 0; i < n; i++)
        {
            var c = FreeCoin();
            c.active = true; c.img.enabled = true;
            c.value = amount / n + (i < amount % n ? 1 : 0);
            c.from = from + new Vector2(Random.Range(-26f, 26f), Random.Range(-18f, 18f));
            // Control point thrown up and out: the arc peaks away from the body,
            // which is what makes it read as "spewing" rather than draining.
            float ang = Random.Range(20f, 160f) * Mathf.Deg2Rad;
            float r = spread * Random.Range(.45f, 1.15f);
            c.ctrl = c.from + new Vector2(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r * 1.25f + 70f);
            c.to = to + new Vector2(Random.Range(-10f, 10f), Random.Range(-8f, 8f));
            c.delay = Random.Range(0f, .13f + weight * .1f);
            c.dur = hang + Random.Range(0f, .3f) + i * (.012f - weight * .004f);
            c.t = 0f;
            c.spin0 = Random.Range(0f, 360f);
            c.spin = Random.Range(-420f, 420f) * (1f + weight);
            c.scale = Random.Range(.8f, 1.25f) * (1f + weight * .35f);
            c.rt.anchoredPosition = c.from;
        }
        Sfx.Play("pickup", .22f + weight * .3f, .12f, 1.25f - weight * .3f);
    }

    static float EaseIn(float x) { return x * x; }
    static float EaseOut(float x) { return 1f - (1f - x) * (1f - x); }

    void TickCinders(float dt)
    {
        if (cinderPlate == null) return;
        bool landed = false;
        foreach (var c in coins)
        {
            if (!c.active) continue;
            if (c.delay > 0f) { c.delay -= dt; continue; }
            c.t += dt / c.dur;
            if (c.t >= 1f)
            {
                c.active = false; c.img.enabled = false;
                cinderShown += c.value; landed = true;
                continue;
            }
            // Quadratic bezier: body -> thrown wide -> counter. Slow out of the
            // corpse, fast into the plate, so the pull at the end has some snap.
            float k = Mathf.Lerp(EaseOut(c.t), EaseIn(c.t), c.t);
            Vector2 a = Vector2.Lerp(c.from, c.ctrl, k), b = Vector2.Lerp(c.ctrl, c.to, k);
            c.rt.anchoredPosition = Vector2.Lerp(a, b, k);
            c.rt.localRotation = Quaternion.Euler(0, 0, c.spin0 + c.spin * c.t);
            float pop = Mathf.Min(1f, c.t * 7f);                 // flick into existence
            float sink = 1f - Mathf.Max(0f, (c.t - .82f) / .18f); // shrink into the plate
            float s = c.scale * pop * sink;
            c.rt.localScale = new Vector3(s, s, 1);
            c.img.color = new Color(1, 1, 1, Mathf.Min(1f, c.t * 9f));
        }
        if (landed)
        {
            platePunch = Mathf.Min(1f, platePunch + .5f);
            plateFlash = 1f;
            cinderText.text = cinderShown.ToString("N0");
        }
        platePunch = Mathf.MoveTowards(platePunch, 0f, dt * 4.5f);
        plateFlash = Mathf.MoveTowards(plateFlash, 0f, dt * 3.2f);
        float ps = 1f + platePunch * .085f;
        cinderPlate.localScale = new Vector3(ps, ps, 1);
        cinderText.color = Color.Lerp(Gold, Color.white, plateFlash * .75f);

        // Anything that changed the purse without a burst (loading a save, a
        // refund, a debug key) still has to agree with the plate.
        if (game != null && cinderShown != game.Cinders && !AnyCoinFlying())
        {
            cinderShown = game.Cinders;
            cinderText.text = cinderShown.ToString("N0");
        }
    }

    bool AnyCoinFlying()
    {
        foreach (var c in coins) if (c.active) return true;
        return false;
    }
}
