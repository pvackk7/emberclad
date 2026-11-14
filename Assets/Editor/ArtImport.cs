using UnityEditor;
using UnityEngine;

// Import settings for generated art/audio under Assets/Resources.
public class ArtImport : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Resources/Art/")) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Default;
        ti.npotScale = TextureImporterNPOTScale.None;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.filterMode = FilterMode.Bilinear;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.maxTextureSize = 4096;
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
