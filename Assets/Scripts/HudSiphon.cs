using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// Siphon: the scrap of life a corpse gives back.
///
/// Built like the cinder burst, with two differences that matter. The motes are
/// embers, and they fly to the *leading edge of the health bar* -- re-aimed every
/// frame, so they curve in after the coals they are feeding instead of at a fixed
/// spot. And they outnumber the health they carry: a grunt is worth two points but
/// throws six embers, so a kill sparkles far more than it heals. That gap is the
/// whole point. It has to feel like a reward without being a way to sustain.
public partial class Hud
{
    RectTransform siphonLayer;

    class Mote
    {
        public RectTransform rt; public Image img;
        public Vector2 from, ctrl;
        public float t, delay, dur, scale, pitch;
        public int value; public bool active;
    }
    readonly List<Mote> motes = new List<Mote>();

    void BuildSiphon() { siphonLayer = Full("Siphon embers", root); }

    Mote FreeMote()
    {
        foreach (var m in motes) if (!m.active) return m;
        var n = new Mote();
        n.img = Img("ui_ember", siphonLayer, new Vector2(.5f, .5f), Vector2.zero, new Vector2(26, 26), Color.white, new Vector2(.5f, .5f));
        n.rt = n.img.rectTransform;
        motes.Add(n); return n;
    }

    /// Where the flame is burning right now, in the same centre-origin space the
    /// motes fly through. Recomputed per frame so they track the edge as it moves.
    Vector2 SiphonTarget()
    {
        if (hpBar == null) return Vector2.zero;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, hpBar.HeadWorldPoint());
        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out local);
        return local;
    }

    /// worldPos: where the body fell. amount: health owed. weight: 0 runt .. 1 boss.
    public void BurstSiphon(Vector3 worldPos, int amount, float weight)
    {
        if (siphonLayer == null || amount <= 0) return;
        var cam = Camera.main;
        if (cam == null) return;
        Vector2 from;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, cam.WorldToScreenPoint(worldPos), null, out from)) return;

        int n = Mathf.Clamp(3 + Mathf.RoundToInt(amount * 1.6f), 4, 20);
        for (int i = 0; i < n; i++)
        {
            var m = FreeMote();
            m.active = true; m.img.enabled = true;
            // Fewer points than motes, so most carry nothing but light.
            m.value = amount / n + (i < amount % n ? 1 : 0);
            m.from = from + new Vector2(Random.Range(-22f, 22f), Random.Range(-16f, 16f));
            float ang = Random.Range(35f, 145f) * Mathf.Deg2Rad;
            float r = (70f + weight * 120f) * Random.Range(.5f, 1.2f);
            m.ctrl = m.from + new Vector2(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r + 60f);
            m.delay = Random.Range(0f, .1f) + i * .028f;
            m.dur = .52f + Random.Range(0f, .22f);
            m.t = 0f;
            m.scale = Random.Range(.75f, 1.2f) * (1f + weight * .4f);
            // Each ember in the volley lands a semitone higher than the last. It is
            // the cheapest trick there is and it is the reason a kill feels good.
            m.pitch = 1.12f + i * .055f;
            m.rt.anchoredPosition = m.from;
        }
    }

    void TickSiphon(float dt)
    {
        if (siphonLayer == null) return;
        Vector2 to = SiphonTarget();
        foreach (var m in motes)
        {
            if (!m.active) continue;
            if (m.delay > 0f) { m.delay -= dt; continue; }
            m.t += dt / m.dur;
            if (m.t >= 1f)
            {
                m.active = false; m.img.enabled = false;
                if (game != null) game.GrantSiphon(m.value);
                if (hpBar != null) hpBar.SiphonHit();
                Sfx.Play("mana", .17f, 0, Mathf.Min(m.pitch, 2.4f));
                continue;
            }
            // Thrown wide out of the corpse, then hauled in hard: the back half of
            // the curve accelerates, which is what makes the arrival land.
            float k = Mathf.Lerp(EaseOut(m.t), EaseIn(m.t), m.t);
            Vector2 a = Vector2.Lerp(m.from, m.ctrl, k), b = Vector2.Lerp(m.ctrl, to, k);
            m.rt.anchoredPosition = Vector2.Lerp(a, b, k);
            float pop = Mathf.Min(1f, m.t * 8f);
            float sink = 1f - Mathf.Max(0f, (m.t - .80f) / .20f);
            float flick = 1f + Mathf.Sin(m.t * 34f + m.pitch * 9f) * .13f;
            float s = m.scale * pop * sink * flick;
            m.rt.localScale = new Vector3(s, s, 1);
            m.img.color = new Color(1f, .86f - m.t * .18f, .55f - m.t * .3f, Mathf.Min(1f, m.t * 10f));
        }
    }
}
