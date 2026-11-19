using UnityEngine;
using UnityEngine.UI;

public partial class Hud
{
    RectTransform hotbar;
    Image[] itemEdges = new Image[4];
    Image potionIcon;
    Text potionCount, equippedLabel;
    string lastLabel;
    float labelShow;

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
            if (i < 2)
            {
                var icon = Img(i == 0 ? "k_sword" : "ui_potion", well.transform, c, new Vector2(0, 3), new Vector2(62, 67), Color.white, c);
                icon.preserveAspect = true;
                if (i == 0) icon.rectTransform.localRotation = Quaternion.Euler(0, 0, -25);
                else
                {
                    potionIcon = icon;
                    potionCount = Txt("3", well.transform, c, new Vector2(27, -27), new Vector2(35, 26), 22, Cream, TextAnchor.MiddleCenter, c);
                }
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
        for (int i = 0; i < 4; i++)
        {
            itemEdges[i].color = i == focus ? (i < 2 ? Gold : new Color(.55f, .32f, .22f)) : new Color(.31f, .27f, .19f, .85f);
            float size = i == focus && QuickItems.Selecting ? 1.07f : 1;
            itemEdges[i].rectTransform.localScale = Vector3.Lerp(itemEdges[i].rectTransform.localScale, Vector3.one * size, 1 - Mathf.Exp(-18 * Time.unscaledDeltaTime));
        }
        potionCount.text = p.potions.ToString();
        potionIcon.color = p.potions > 0 ? Color.white : new Color(.4f, .4f, .4f, .65f);
        string label = QuickItems.Selecting && focus > 1 ? "Empty" : focus == 0 ? "Sword" : "Health Potion";
        if (label != lastLabel) { lastLabel = label; equippedLabel.text = label; labelShow = 2.2f; }
        if (QuickItems.Selecting) labelShow = Mathf.Max(labelShow, .6f);
        labelShow -= Time.unscaledDeltaTime;
        float a = Mathf.Clamp01(labelShow / .35f);
        var col = Cream; col.a = a; equippedLabel.color = col;
    }
}
