using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// Loads the storybook sprite set listed in Resources/Art/Storybook/manifest
/// (one line per sprite: name pivotX pivotY pixelsPerUnit).
public static class StorybookArt
{
    public static void Load(Dictionary<string, Sprite> sprites)
    {
        var manifest = Resources.Load<TextAsset>("Art/Storybook/manifest");
        if (manifest == null) { Debug.LogError("Missing Resources/Art/Storybook/manifest"); return; }
        var ci = CultureInfo.InvariantCulture;
        foreach (string line in manifest.text.Split('\n'))
        {
            var p = line.Trim().Split(' ');
            if (p.Length != 4) continue;
            var texture = Resources.Load<Texture2D>("Art/Storybook/" + p[0]);
            if (texture == null) { Debug.LogError("Missing storybook sprite " + p[0]); continue; }
            var pivot = new Vector2(float.Parse(p[1], ci), float.Parse(p[2], ci));
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), pivot, float.Parse(p[3], ci), 0, SpriteMeshType.FullRect);
            sprite.name = p[0]; sprites[p[0]] = sprite;
        }
    }
}
