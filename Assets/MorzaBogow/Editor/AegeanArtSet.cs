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
            set.Props = Prefabs("prop_").Where(prefab => !prefab.name.StartsWith("prop_timber_", System.StringComparison.Ordinal)).ToArray();
            MakeReadable(set.Rocks.Concat(set.Pines).Concat(set.Cypresses));
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            Debug.Log($"[AegeanArt] {set.Rocks.Length} rocks, {set.TimberStacks.Length} timber stacks, {set.Pines.Length} pines, {set.Cypresses.Length} cypresses, {set.Props.Length} props in {AssetPath}");
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

        /// <summary>
        /// The models the battle merges into one mesh per shore or wood (rocks, pines, cypresses) imported with their
        /// meshes readable: a player cannot combine meshes it may not read and drops them with an error.
        /// </summary>
        public static void MakeReadable(System.Collections.Generic.IEnumerable<GameObject> prefabs)
        {
            foreach (var prefab in prefabs)
            {
                if (prefab == null) continue;
                foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh == null) continue;
                    var path = AssetDatabase.GetAssetPath(filter.sharedMesh);
                    if (!(AssetImporter.GetAtPath(path) is ModelImporter importer) || importer.isReadable) continue;
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                }
            }
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
