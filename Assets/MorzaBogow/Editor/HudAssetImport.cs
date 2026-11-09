using UnityEditor;
using UnityEngine;

namespace MorzaBogow.Editor
{
    /// <summary>
    /// Import settings for the battle interface's graphics (design-tokens.json, spriteImport): no mipmaps, sRGB,
    /// alpha kept, clamp and bilinear, uncompressed so icons stay crisp at 1440p. Cursors import as cursors;
    /// ship portraits are capped to the size they are shown at.
    /// </summary>
    public sealed class HudAssetImport : AssetPostprocessor
    {
        const string Root = "Assets/MorzaBogow/UI/Resources/Hud/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root)) return;
            var importer = (TextureImporter)assetImporter;
            var cursor = assetPath.Contains("/Cursors/");
            importer.textureType = cursor ? TextureImporterType.Cursor : TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.isReadable = cursor;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = assetPath.Contains("/Art/") ? 512 : 1024;
        }
    }
}
