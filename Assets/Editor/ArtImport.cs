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
