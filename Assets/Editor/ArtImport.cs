using UnityEditor;
using UnityEngine;

// Import settings for generated art/audio under Assets/Resources.
public class ArtImport : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Resources/Art/") && !assetPath.StartsWith("Assets/Resources/Menu/")) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Default;
        ti.textureShape = TextureImporterShape.Texture2D;
        ti.npotScale = TextureImporterNPOTScale.None;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.filterMode = FilterMode.Bilinear;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.maxTextureSize = 4096;
        // Only the two character sheets are read on the CPU (to trim their padding).
        // A readable texture keeps a second full copy in memory, so nothing else should be.
        ti.isReadable = assetPath.StartsWith("Assets/Resources/Art/Characters/");
        // The coin is a 1254 px painting shown at ~50 px in the HUD. Without mipmaps the
        // GPU point-samples a handful of its pixels and the face turns to grit, so it
        // gets a Kaiser-filtered (sharp) mip chain and trilinear sampling.
        // The menu dragon's 192 px frames are drawn ~40-80 px wide: same treatment.
        if (assetPath.Contains("/ui_coin") || assetPath.StartsWith("Assets/Resources/Menu/Dragon/"))
        {
            ti.mipmapEnabled = true;
            // Box mips for the coin: Kaiser sharpening brought the grit back at HUD size.
            ti.mipmapFilter = assetPath.Contains("/ui_coin") ? TextureImporterMipFilter.BoxFilter : TextureImporterMipFilter.KaiserFilter;
            ti.filterMode = FilterMode.Trilinear;
        }
    }

    void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith("Assets/Resources/Sfx/")) return;
        var ai = (AudioImporter)assetImporter;
        ai.forceToMono = true;
        var s = ai.defaultSampleSettings;
        bool longClip = assetPath.Contains("music") || assetPath.Contains("ambience");
        s.loadType = longClip ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
        s.compressionFormat = longClip ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM;
        s.quality = 0.6f;
        ai.defaultSampleSettings = s;
    }
}
