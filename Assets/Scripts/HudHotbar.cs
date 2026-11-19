using UnityEngine;
using UnityEngine.UI;

public partial class Hud
{
    RectTransform hotbar;
    Image[] itemEdges = new Image[4];
    Image potionIcon, emberIcon;
    Text emberLocked;
    Text potionCount, equippedLabel;
    string lastLabel;
    float labelShow;
    // While the wheel is being taught, each well lights in turn. A static highlight
    // says "look here"; a cycle says "there are several of these and you choose".
    bool hotbarDemo; float demoClock;
    public void DemoHotbar(bool on) { hotbarDemo = on; demoClock = 0f; }

    void BuildHotbar()
    {
        Vector2 c = new Vector2(.5f, .5f);
        hotbar = Rect("Equipment hotbar", root, Vector2.zero, new Vector2(184, 205), new Vector2(310, 310), c);
        Vector2[] positions = { new Vector2(0, 98), new Vector2(98, 0), new Vector2(0, -98), new Vector2(-98, 0) };
        for (int i = 0; i < 4; i++)
        {
            var edge = Img("ui_round", hotbar, c, positions[i], new Vector2(94, 94), new Color(.31f, .27f, .19f), c, true);
            itemEdges[i] = edge;
            var well = Img("ui_round", edge.transform, c, Vector2.zero, new Vector2(86, 86), new Color(.075f, .065f, .055f, .94f), c, true);
            if (i == 0)
            {
                var icon = Img("k_sword", well.transform, c, new Vector2(0, 3), new Vector2(62, 67), Color.white, c);
                icon.preserveAspect = true;
                icon.rectTransform.localRotation = Quaternion.Euler(0, 0, -25);
            }
            else if (i == 1)
            {
                potionIcon = Img("ui_potion", well.transform, c, new Vector2(0, 3), new Vector2(62, 67), Color.white, c);
                potionIcon.preserveAspect = true;
                potionCount = Txt("3", well.transform, c, new Vector2(27, -27), new Vector2(35, 26), 22, Cream, TextAnchor.MiddleCenter, c);
            }
            else if (i == 2)
            {
                // Embers live here now instead of on the knight panel. No count and no
                // charge pips: what limits a cast is health, and the health bar is
                // already on screen saying how many are left.
                emberIcon = Img("ui_ember", well.transform, c, new Vector2(0, 3), new Vector2(58, 62), Color.white, c);
                emberIcon.preserveAspect = true;
                // Reads as a plain empty well until the ember is picked up, so the
                // tutorial's third round actually hands the player something new.
                emberLocked = Txt("—", well.transform, c, Vector2.zero, new Vector2(60, 50), 25, new Color(.42f, .39f, .32f), TextAnchor.MiddleCenter, c, false);
            }
            else Txt("—", well.transform, c, Vector2.zero, new Vector2(60, 50), 25, new Color(.42f, .39f, .32f), TextAnchor.MiddleCenter, c, false);
        }
        // Item name sits beside the bottom slot and only shows itself while the
        // selection is changing, then fades, so the cross stays unlabelled at rest.
        equippedLabel = Txt("Sword", hotbar, c, new Vector2(62, -98), new Vector2(360, 30), 20, Cream, TextAnchor.MiddleLeft, new Vector2(0, .5f));
    }

    void TickHotbar()
    {
        if (hotbar == null) return;
        hotbar.gameObject.SetActive(game.CanUseItems);
        if (!game.CanUseItems) return;
        var p = game.player;
        int focus = QuickItems.Selecting && QuickItems.Hovered >= 0 ? QuickItems.Hovered : QuickItems.Selected;
        demoClock += Time.unscaledDeltaTime;
        int demoSlot = hotbarDemo ? Mathf.FloorToInt(demoClock * 2.4f) % QuickItems.Filled : -1;
        for (int i = 0; i < 4; i++)
        {
            Color lit = i == 2 ? new Color(.96f, .58f, .22f) : i < 2 ? Gold : new Color(.55f, .32f, .22f);
            Color want = i == focus ? lit : new Color(.31f, .27f, .19f, .85f);
            float size = i == focus && QuickItems.Selecting ? 1.07f : 1;
            if (i == demoSlot && !QuickItems.Selecting)
            {
                float beat = Mathf.Repeat(demoClock * 2.4f, 1f);
                float glow = Mathf.Sin(beat * Mathf.PI);
                want = Color.Lerp(want, new Color(1f, .83f, .42f), glow);
                size = 1f + glow * .11f;
            }
            itemEdges[i].color = want;
            itemEdges[i].rectTransform.localScale = Vector3.Lerp(itemEdges[i].rectTransform.localScale, Vector3.one * size, 1 - Mathf.Exp(-18 * Time.unscaledDeltaTime));
        }
        potionCount.text = p.potions.ToString();
        potionIcon.color = p.potions > 0 ? Color.white : new Color(.4f, .4f, .4f, .65f);

        // Embers. The icon goes cold once there is not enough health left to pay for a
        // cast, and takes the kick PulseEmber gives it when one is refused.
        bool unlocked = p.HasEmbers;
        emberIcon.enabled = unlocked;
        emberLocked.enabled = !unlocked;
        bool canCast = p.CanCast;
        emberIcon.sprite = Art.S(canCast ? "ui_ember" : "ui_ember_off");
        emberIcon.color = canCast ? Color.white : new Color(.52f, .44f, .40f, .7f);
        float pulse = 1 + manaPulse * .30f + (canCast ? Mathf.Sin(Time.unscaledTime * 4.5f) * .035f : 0);
        emberIcon.rectTransform.localScale = new Vector3(pulse, pulse, 1);

        string label = QuickItems.Selecting && (focus > 2 || (focus == 2 && !unlocked)) ? "Empty"
                     : focus == 0 ? "Sword" : focus == 1 ? "Health Potion" : "Ember Burst";
        if (label != lastLabel) { lastLabel = label; equippedLabel.text = label; labelShow = 2.2f; }
        if (QuickItems.Selecting) labelShow = Mathf.Max(labelShow, .6f);
        labelShow -= Time.unscaledDeltaTime;
        float a = Mathf.Clamp01(labelShow / .35f);
        var col = Cream; col.a = a; equippedLabel.color = col;
    }
}
