using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// Damage, told where it happened.
///
/// A corner counter saying HITS tells you that something is going well without ever
/// saying what. A number over the skull you are actually hitting says how hard that
/// blow landed, and because a combo keeps adding into the same number instead of
/// starting a new one, the running total climbs and swells while you keep the
/// pressure on. Swing into a crowd and every skull shows a different figure, which
/// is the whole point of the falloff that feeds this.
public partial class Hud
{
    RectTransform damageLayer;

    class DamageTag
    {
        public Text text; public RectTransform rt;
        public Enemy target; public int total;
        public float hold, punch; public Vector2 at; public bool active;
    }
    readonly List<DamageTag> damageTags = new List<DamageTag>();

    // How long a tag keeps accepting more damage before it lets go and drifts off.
    const float ComboWindow = .85f, DriftOut = .55f;

    void BuildDamage() { damageLayer = Full("Damage numbers", root); }

    /// Called from Enemy.TakeHit for blows the player landed.
    public void ShowDamage(Enemy e, int amount)
    {
        if (damageLayer == null || e == null || amount <= 0) return;

        DamageTag tag = null;
        foreach (var t in damageTags) if (t.active && t.target == e && t.hold > 0f) { tag = t; break; }
        if (tag == null)
        {
            foreach (var t in damageTags) if (!t.active) { tag = t; break; }
            if (tag == null)
            {
                tag = new DamageTag();
                tag.text = Txt("0", damageLayer, new Vector2(.5f, .5f), Vector2.zero, new Vector2(260, 70), 38, Ink, TextAnchor.MiddleCenter, new Vector2(.5f, .5f));
                tag.rt = tag.text.rectTransform;
                damageTags.Add(tag);
            }
            tag.total = 0;
        }

        tag.active = true; tag.target = e; tag.total += amount;
        tag.hold = ComboWindow;
        tag.punch = 1f;
        tag.text.enabled = true;
        tag.text.text = tag.total.ToString();
        tag.at = HeadPoint(e);
    }

    Vector2 HeadPoint(Enemy e)
    {
        var cam = Camera.main;
        if (cam == null || e == null) return Vector2.zero;
        Vector3 world = e.transform.position + new Vector3(0, .95f + e.scale * .55f, 0);
        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, cam.WorldToScreenPoint(world), null, out local);
        return local;
    }

    void TickDamage(float dt)
    {
        if (damageLayer == null) return;
        for (int i = 0; i < damageTags.Count; i++)
        {
            var t = damageTags[i];
            if (!t.active) continue;
            t.hold -= dt;

            // Pinned over the skull while the combo is live; once it stops, the number
            // lets go and floats, so a dying enemy does not drag its total off-screen.
            bool live = t.hold > 0f && t.target != null && !t.target.dead;
            if (live) t.at = HeadPoint(t.target);
            float rise = t.hold > 0f ? 0f : -t.hold * 58f;
            t.rt.anchoredPosition = t.at + new Vector2(0, rise);

            t.punch = Mathf.MoveTowards(t.punch, 0f, dt * 5.5f);
            // The number grows and heats as the total climbs, so a long chain reads as
            // one escalating figure rather than a series of little ones.
            float heat = Mathf.Clamp01(t.total / 80f);
            t.text.fontSize = Mathf.RoundToInt(Mathf.Lerp(34f, 62f, heat));
            float fade = t.hold > 0f ? 1f : Mathf.Clamp01(1f + t.hold / DriftOut);
            var c = Color.Lerp(Ink, new Color(1f, .40f, .20f), heat);
            c.a = fade;
            t.text.color = c;
            float s = (1f + t.punch * .45f) * (.9f + fade * .1f);
            t.rt.localScale = new Vector3(s, s, 1);

            if (t.hold < -DriftOut) { t.active = false; t.text.enabled = false; t.target = null; }
        }
    }
}
