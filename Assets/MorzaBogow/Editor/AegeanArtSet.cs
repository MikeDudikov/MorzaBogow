using System.IO;
using System.Linq;
using MorzaBogow.Presentation;
using UnityEditor;
using UnityEngine;

namespace MorzaBogow.Editor
{
    /// <summary>
    /// Fills the art set the battle draws from (AegeanArt in Resources) with the asset pipeline's finished prefabs in
    /// Art/Generated: rock_* for the shores, prop_timber_*, veg_pinus_* and veg_cupressus_* for the timber yards, in the
    /// order of their paths so the same land gets the same rocks and trees. Run after a pipeline job is ready:
    /// -executeMethod MorzaBogow.Editor.AegeanArtSet.RefreshBatch
    /// </summary>
    public static class AegeanArtSet
    {
        public const string Folder = "Assets/MorzaBogow/Art/Resources";
        public static string AssetPath => $"{Folder}/{AegeanArt.ResourcePath}.asset";
        public const string Generated = "Assets/MorzaBogow/Art/Generated";

        [MenuItem("Morza Bogów/Zasoby/Odśwież zestaw egejski")]
        public static AegeanArt Refresh()
        {
            Directory.CreateDirectory(Folder);
            var set = AssetDatabase.LoadAssetAtPath<AegeanArt>(AssetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<AegeanArt>();
                AssetDatabase.CreateAsset(set, AssetPath);
            }
            set.Rocks = Prefabs("rock_");
            set.TimberStacks = Prefabs("prop_timber_");
            set.Pines = Prefabs("veg_pinus_");
            set.Cypresses = Prefabs("veg_cupressus_");
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            Debug.Log($"[AegeanArt] {set.Rocks.Length} rocks, {set.TimberStacks.Length} timber stacks, {set.Pines.Length} pines, {set.Cypresses.Length} cypresses in {AssetPath}");
            return set;
        }

        public static void RefreshBatch()
        {
            var code = 0;
            try { Refresh(); }
            catch (System.Exception error)
            {
                Debug.LogError($"[AegeanArt] {error}");
                code = 1;
            }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        /// <summary>The prefabs whose file name starts so, each in its own job folder, by path.</summary>
        public static GameObject[] Prefabs(string prefix) =>
            AssetDatabase.FindAssets("t:Prefab", new[] { Generated })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => Path.GetFileName(path).StartsWith(prefix, System.StringComparison.Ordinal))
                .OrderBy(path => path, System.StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .Where(prefab => prefab != null)
                .ToArray();
    }
}
