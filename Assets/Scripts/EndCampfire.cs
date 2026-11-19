using UnityEngine;
using UnityEngine.Rendering;

public class EndCampfire : MonoBehaviour
{
    SpriteRenderer glow;
    bool lit;
    float clock, nextFlame, nextSpark;

    public static EndCampfire Create(Transform parent, Vector2 position)
    {
        var go = new GameObject("Unlit campfire");
        go.transform.SetParent(parent, false); go.transform.position = position;
        var fire = go.AddComponent<EndCampfire>();
        var group = go.AddComponent<SortingGroup>(); group.sortingOrder = Art.YOrder(position.y);
        var artRoot = new GameObject("Rooted campfire").transform;
        artRoot.SetParent(go.transform, false); artRoot.localPosition = new Vector3(0, World.Height(position.x), 0);
        var logs = Art.New("pr_log", artRoot, Vector3.zero, 0);
        logs.transform.localScale = new Vector3(.8f, .8f, 1);
        logs.color = new Color(.62f, .7f, .68f, 1);
        fire.glow = Art.New("fx_glow", artRoot, new Vector2(0, .55f), 1, "Default", true);
        fire.glow.enabled = false;
        return fire;
    }

    public void Light()
    {
        if (lit) return;
        lit = true; gameObject.name = "Lit campfire"; glow.enabled = true;
        Sfx.Play("cast", .7f, 0, .8f);
    }

    void Update()
    {
        if (!lit || Time.deltaTime <= 0) return;
        float dt = Time.deltaTime;
        clock += dt; nextFlame -= dt; nextSpark -= dt;
        float ignition = Mathf.Clamp01(clock * 1.5f);
        float pulse = .8f + .12f * Mathf.Sin(clock * 5.7f) + .08f * Mathf.Sin(clock * 11.3f);
        glow.color = new Color(1, .42f, .08f, .3f * pulse * ignition);
        glow.transform.localScale = new Vector3(2.1f, 1.6f, 1) * pulse;
        if (nextFlame <= 0)
        {
            nextFlame = Random.Range(.035f, .075f);
            var origin = new Vector2(Random.Range(-.42f, .42f), .38f);
            float life = Random.Range(.55f, 1.05f);
            var flame = Fx.Emit("fx_flame" + Random.Range(1, 4), origin, new Vector2(Random.Range(-.16f, .16f), Random.Range(.35f, .7f)), life, Random.Range(.35f, .65f) * ignition, .04f, new Color(1, .77f, .38f, .9f), new Color(1, .22f, .03f, 0), Random.Range(-12, 12), Random.Range(-22, 22), 0, false, 2, -999, 0, glow.transform.parent, .8f, 1.4f);
            // TryGetComponent, not "GetComponent() ?? Add": in the editor a missing component comes back
            // as a fake-null object that ?? doesn't catch, so Begin() threw on every flame.
            if (flame != null)
            {
                if (!flame.TryGetComponent(out CampfireCurl curl)) curl = flame.gameObject.AddComponent<CampfireCurl>();
                curl.Begin(life);
            }
        }
        if (nextSpark <= 0)
        {
            nextSpark = Random.Range(.08f, .19f);
            Fx.Emit("fx_glow", new Vector2(Random.Range(-.4f, .4f), .65f), new Vector2(Random.Range(-.35f, .35f), Random.Range(.7f, 1.5f)), Random.Range(.8f, 1.7f), Random.Range(.025f, .055f), 0, new Color(1, .8f, .3f, .9f), new Color(1, .3f, .02f, 0), 0, 0, 0, true, 3, -999, 0, glow.transform.parent);
        }
    }
}

/// Sideways drift for one campfire flame. The flame is a pooled Fx particle, so the
/// curl must stop exactly when that flame dies or it would push whatever effect reuses it.
public class CampfireCurl : MonoBehaviour
{
    float age, life, phase;
    public void Begin(float lifetime) { age = 0; life = lifetime; phase = Random.Range(0, Mathf.PI * 2); enabled = true; }
    void LateUpdate()
    {
        age += Time.deltaTime;
        if (age >= life) { enabled = false; return; }
        transform.localPosition += Vector3.right * Mathf.Sin(age * 6 + phase) * Time.deltaTime * .12f;
    }
}
