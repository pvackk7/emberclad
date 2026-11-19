using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// Loot lying in the world, taken with the Interact key.
///
/// It advertises itself rather than waiting to be discovered: it hops out of
/// whatever dropped it, settles, breathes a warm pool of light, and raises a key
/// prompt the moment the knight is close enough to take it. A player should never
/// have to walk around mashing E at scenery to find out what is interactive.
public class Pickup : MonoBehaviour
{
    public enum Kind { Shield }

    static readonly List<Pickup> all = new List<Pickup>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { all.Clear(); }

    public Kind kind;
    public float reach = 1.7f;
    Vector2 ground, hop;
    float z, vz, clock, focus;
    SpriteRenderer body, glow;
    SortingGroup group;
    bool settled;

    static string SpriteFor(Kind k) { return k == Kind.Shield ? "s_shield" : "ui_coin"; }
    static string LabelFor(Kind k) { return k == Kind.Shield ? "Take Shield" : "Take"; }

    public static Pickup Drop(Kind kind, Vector2 at, int facing)
    {
        var go = new GameObject(kind + " pickup");
        var p = go.AddComponent<Pickup>();
        p.kind = kind; p.ground = at;
        p.group = go.AddComponent<SortingGroup>();
        // Ground pool first so it sits under the item and reads as light cast on
        // the dirt, not as a halo stuck to the sprite.
        p.glow = Art.New("fx_glow", go.transform, new Vector3(0, .12f, 0), -2, "Default", true);
        p.glow.color = new Color(1f, .74f, .34f, 0f);
        p.body = Art.New(SpriteFor(kind), go.transform, new Vector3(0, .3f, 0), 1);
        p.z = .35f; p.vz = 4.6f; p.hop = new Vector2(facing * 1.7f, 0);
        go.transform.position = World.Project(at);
        return p;
    }

    void OnEnable() { if (!all.Contains(this)) all.Add(this); }
    void OnDisable() { all.Remove(this); }

    void Update()
    {
        float dt = Time.deltaTime; if (dt <= 0) return;
        clock += dt;
        if (!settled)
        {
            // Tossed clear of the corpse, then a couple of dead little bounces.
            vz -= 24f * dt; z += vz * dt;
            ground += hop * dt; hop *= 1f - Mathf.Min(1f, 4.5f * dt);
            if (z <= 0f)
            {
                z = 0f; vz = -vz * .32f;
                Fx.Dust(ground, 2, .45f);
                if (Mathf.Abs(vz) < 1.1f) { vz = 0f; settled = true; Sfx.Play("step", .4f, .15f, .8f); }
            }
            transform.position = World.Project(ground);
            group.sortingOrder = Art.YOrder(ground.y);
        }

        // Only the nearest pickup lights up, so two on the floor never both claim
        // the prompt and leave you guessing which one E would take.
        var p = Game.I != null ? Game.I.player : null;
        bool near = false;
        if (p != null && !p.dead && settled && Game.I.state == Game.State.Playing)
        {
            float d = Vector2.Distance(p.pos, ground);
            near = d < reach;
            if (near) foreach (var o in all) if (o != this && o.settled && Vector2.Distance(p.pos, o.ground) < d) { near = false; break; }
        }
        focus = Mathf.MoveTowards(focus, near ? 1f : 0f, dt * 7f);

        float breathe = Mathf.Sin(clock * 2.5f);
        body.transform.localPosition = new Vector3(0, .3f + z + breathe * .055f + focus * .06f, 0);
        body.transform.localRotation = Quaternion.Euler(0, 0, breathe * 4.5f);
        body.transform.localScale = Vector3.one * (1f + focus * .09f);
        glow.color = new Color(1f, .74f, .34f, (.3f + breathe * .07f + focus * .34f) * .55f);
        glow.transform.localScale = new Vector3(1.7f, 1.05f, 1) * (1f + breathe * .05f + focus * .3f);

        if (near)
        {
            Game.I.hud.ShowInteractPrompt(ground + new Vector2(0, 1.35f), LabelFor(kind));
            GameInput.ClaimInteract();
            if (MenuSettings.Pressed(12)) Take();
        }
    }

    void Take()
    {
        Sfx.Play("pickup", .8f, 0, .85f);
        Fx.Dust(ground, 7, .85f);
        Fx.Ring(ground + new Vector2(0, .3f), 1.4f, new Color(1f, .82f, .42f, .8f), .45f);
        Game.I.OnPickup(kind);
        Destroy(gameObject);
    }
}
