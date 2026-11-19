using System.Collections.Generic;
using UnityEngine;

public static class CharacterArt
{
    public static void Load(Dictionary<string, Sprite> sprites)
    {
        var knight = Resources.Load<Texture2D>("Art/Characters/KnightParts");
        if (knight != null)
        {
            Add(sprites, knight, "k_head", 0, .64f, new Vector2(.5f, .10f));
            Add(sprites, knight, "k_torso", 1, .78f, new Vector2(.5f, .15f));
            Add(sprites, knight, "k_arm", 2, .64f, new Vector2(.5f, .85f));
            Add(sprites, knight, "k_leg", 3, .55f, new Vector2(.40f, .85f));
            Add(sprites, knight, "k_cape", 4, .95f, new Vector2(.86f, .94f));
            Add(sprites, knight, "k_sword", 5, 1.18f, new Vector2(.5f, .20f));
        }
        // Same bottle, liquid pushed from crimson to ember orange-red. The original
        // crimson art is untouched on disk and is still the fallback.
        var potion = Single("Art/Characters/EmberPotion") ?? Single("Art/Characters/HealthPotion");
        if (potion != null) sprites["ui_potion"] = potion;
        var shield = Resources.Load<Texture2D>("Art/Characters/SkeletonShield");
        if (shield != null)
        {
            Rect bounds = Bounds(shield, new RectInt(0, 0, shield.width, shield.height));
            sprites["s_shield"] = Sprite.Create(shield, bounds, new Vector2(.5f, .5f), bounds.height / .78f, 0, SpriteMeshType.FullRect);
        }
        var fox = Resources.Load<Texture2D>("Art/Characters/KhalidParts");
        if (fox != null)
        {
            Add(sprites, fox, "p_head", 0, .50f, new Vector2(.35f, .19f));
            Add(sprites, fox, "p_head_blink", 1, .50f, new Vector2(.35f, .19f));
            Add(sprites, fox, "p_head_happy", 2, .50f, new Vector2(.35f, .19f));
            Add(sprites, fox, "p_body", 3, .46f, new Vector2(.5f, .20f));
            Add(sprites, fox, "p_tail", 4, .38f, new Vector2(.90f, .50f), 0, 80);
            Add(sprites, fox, "p_leg", 5, .26f, new Vector2(.48f, .82f), 128);
        }
        var skeleton = Resources.Load<Texture2D>("Art/Characters/SkeletonParts");
        if (skeleton != null)
        {
            AddSkeleton(sprites, skeleton, "s_head", 0, .52f, new Vector2(.49f, .14f));
            AddSkeleton(sprites, skeleton, "s_head_red", 1, .52f, new Vector2(.49f, .14f));
            AddSkeleton(sprites, skeleton, "s_jaw", 2, .16f, new Vector2(.36f, .80f));
            AddSkeleton(sprites, skeleton, "s_torso", 3, .56f, new Vector2(.5f, .14f));
            AddSkeleton(sprites, skeleton, "s_arm", 4, .54f, new Vector2(.5f, .89f));
            AddSkeleton(sprites, skeleton, "s_leg", 5, .48f, new Vector2(.40f, .89f));
            AddSkeleton(sprites, skeleton, "s_dagger", 6, .72f, new Vector2(.5f, .19f));
            AddSkeleton(sprites, skeleton, "s_helm", 7, .36f, new Vector2(.48f, .13f));
            AddSkeleton(sprites, skeleton, "s_antlers", 8, .48f, new Vector2(.5f, .05f));
            AddSkeleton(sprites, skeleton, "s_cloak", 9, .88f, new Vector2(.83f, .91f));
            AddSkeleton(sprites, skeleton, "s_head_hurt", 10, .52f, new Vector2(.49f, .14f));
            AddSkeleton(sprites, skeleton, "s_head_red_hurt", 11, .52f, new Vector2(.49f, .14f));
        }
    }

    static void AddSkeleton(Dictionary<string, Sprite> sprites, Texture2D sheet, string name, int cell, float height, Vector2 pivot)
    {
        int cw = sheet.width / 4, ch = sheet.height / 3;
        Rect rect = Bounds(sheet, new RectInt(cell % 4 * cw, (2 - cell / 4) * ch, cw, ch));
        var sprite = Sprite.Create(sheet, rect, pivot, rect.height / height, 0, SpriteMeshType.FullRect);
        sprite.name = name;
        sprites[name] = sprite;
    }

    static void Add(Dictionary<string, Sprite> sprites, Texture2D sheet, string name, int cell, float height, Vector2 pivot, int trimLeft = 0, int extraRight = 0)
    {
        int cw = sheet.width / 3, ch = sheet.height / 2;
        int x = cell % 3 * cw + trimLeft, y = (1 - cell / 3) * ch;
        int w = Mathf.Min(cw - trimLeft + extraRight, sheet.width - x);
        Rect rect = Bounds(sheet, new RectInt(x, y, w, ch));
        var sprite = Sprite.Create(sheet, rect, pivot, rect.height / height, 0, SpriteMeshType.FullRect);
        sprite.name = name; sprites[name] = sprite;
    }

    public static Sprite Single(string resource)
    {
        var texture = Resources.Load<Texture2D>(resource);
        if (texture == null) return null;
        return Sprite.Create(texture, Bounds(texture, new RectInt(0, 0, texture.width, texture.height)), new Vector2(.5f, 0), 100, 0, SpriteMeshType.FullRect);
    }

    static Rect Bounds(Texture2D texture, RectInt region)
    {
        var pixels = texture.GetPixels32();
        int left = region.xMax, right = region.xMin, bottom = region.yMax, top = region.yMin;
        for (int y = region.yMin; y < region.yMax; y++) for (int x = region.xMin; x < region.xMax; x++)
        {
            if (pixels[y * texture.width + x].a < 128) continue;
            left = Mathf.Min(left, x); right = Mathf.Max(right, x); bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y);
        }
        if (right < left || top < bottom) return new Rect(region.x, region.y, region.width, region.height);
        left = Mathf.Max(region.xMin, left - 2); bottom = Mathf.Max(region.yMin, bottom - 2);
        right = Mathf.Min(region.xMax - 1, right + 2); top = Mathf.Min(region.yMax - 1, top + 2);
        return new Rect(left, bottom, right - left + 1, top - bottom + 1);
    }
}
