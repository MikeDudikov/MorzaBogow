using System;
using System.IO;
using UnityEngine;

namespace MorzaBogow.Editor.AIAssets
{
    /// <summary>What the pipeline asks Unity to import (schema morza-bogow/unity-import@1, written by Tools/asset_pipeline/unity).</summary>
    [Serializable]
    public sealed class AIAssetRequest
    {
        public string schema, jobId, assetId, displayName, category, shipClass, outputFolder, forward;
        public LodFile[] lods = Array.Empty<LodFile>();
        public TextureFile[] textures = Array.Empty<TextureFile>();
        public FlatMaterial[] materials = Array.Empty<FlatMaterial>();
        public bool normalised, forwardChecked, rigged, humanoid;
        public string[] teamColour = Array.Empty<string>();
        public string[] animations = Array.Empty<string>();
        public Dimensions dimensions = new Dimensions();
        public Budget budget = new Budget();
        public int textureSize = 1024;
        public string reportPath, resultPath, previewPath, provenanceJson;

        /// <summary>The brief's texturing.tint: linear 0..1 multipliers of the base colour map (empty: white, the map as it is).</summary>
        public float[] tint = Array.Empty<float>();

        /// <summary>The base colour a material with a colour map gets: the tint, or white.</summary>
        public Color MapTint => tint != null && tint.Length == 3 ? new Color(tint[0], tint[1], tint[2]) : Color.white;

        public static AIAssetRequest Parse(string json) => JsonUtility.FromJson<AIAssetRequest>(json);

        public static AIAssetRequest Load(string path) => Parse(File.ReadAllText(path));
    }

    [Serializable] public sealed class LodFile { public string file; public float screenRelativeHeight; }

    [Serializable] public sealed class TextureFile { public string file, role; }

    /// <summary>A material without maps: sRGB base colour, metal and smoothness (blockouts, flat-coloured models).</summary>
    [Serializable] public sealed class FlatMaterial { public string name; public float[] baseColor = { 0.7f, 0.7f, 0.7f }; public float metallic, smoothness = 0.5f; }

    [Serializable] public sealed class Dimensions { public float length, width, height, draft; }

    [Serializable] public sealed class Budget { public int triangles, textureSize, materials, renderers, texelDensity; }

    /// <summary>Outcome of a batch call, read by the pipeline even when Unity exits with an error.</summary>
    [Serializable] public sealed class AIAssetResult { public bool ok; public string prefab, error; }

    /// <summary>What the validator measured; the pipeline decides on it (Tools/asset_pipeline/validation/report.py).</summary>
    [Serializable]
    public sealed class AIAssetReport
    {
        public string assetId, prefab, category, shipClass, preview;
        public Check[] checks = Array.Empty<Check>();
        public LodInfo[] lods = Array.Empty<LodInfo>();
        public MaterialInfo[] materials = Array.Empty<MaterialInfo>();
        public TextureInfo[] textures = Array.Empty<TextureInfo>();
        public int materialCount, rendererCount;
        public BoundsInfo bounds = new BoundsInfo();
        public float texelDensity;
        public bool forwardChecked, meshLodsGenerated;
    }

    [Serializable]
    public sealed class Check
    {
        public string id, severity, message;
        public bool ok;

        public Check() { }

        public Check(string id, bool ok, string severity, string message)
        {
            this.id = id;
            this.ok = ok;
            this.severity = severity;
            this.message = message ?? string.Empty;
        }
    }

    [Serializable] public sealed class LodInfo { public int level, triangles, renderers; public float screenRelativeHeight; }

    [Serializable] public sealed class MaterialInfo { public string name, shader; public bool baseMap, normalMap, maskMap; }

    [Serializable] public sealed class TextureInfo { public string path, type; public int maxSize; public bool sRGB; }

    [Serializable] public sealed class BoundsInfo { public float[] min = new float[3], max = new float[3], size = new float[3]; }
}
