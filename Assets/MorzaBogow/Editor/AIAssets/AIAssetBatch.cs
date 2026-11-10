using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MorzaBogow.Presentation;
using MorzaBogow.Simulation;
using UnityEditor;
using UnityEngine;

namespace MorzaBogow.Editor.AIAssets
{
    /// <summary>
    /// Batch entry points for the asset pipeline:
    /// Unity.exe -batchmode -projectPath P -executeMethod MorzaBogow.Editor.AIAssets.AIAssetBatch.Import -aiRequest request.json -logFile L -quit
    /// Import brings the files in and builds the prefab, then validates; Validate measures an existing prefab again.
    /// Both write the report and a result file the pipeline reads, and exit with 0 (done) or 2 (error).
    /// </summary>
    public static class AIAssetBatch
    {
        public const string DimensionsFile = "Tools/asset_pipeline/config/project_dimensions.json";

        public static void Import() => Run(false);

        public static void Validate() => Run(true);

        static void Run(bool validateOnly)
        {
            var result = new AIAssetResult();
            AIAssetRequest request = null;
            var code = 0;
            try
            {
                var path = Argument("-aiRequest") ?? throw new ArgumentException("Brak -aiRequest <plik.json>.");
                request = AIAssetRequest.Load(path);
                var prefab = validateOnly ? $"{request.outputFolder}/{request.assetId}.prefab" : AIAssetImporter.Import(request);
                var report = AIAssetValidator.ValidatePrefab(prefab, request);
                File.WriteAllText(request.reportPath, JsonUtility.ToJson(report, true));
                result.ok = true;
                result.prefab = prefab;
                var failed = report.checks.Where(c => !c.ok).Select(c => c.id).ToList();
                Debug.Log($"[AIAssets] {request.assetId}: raport {request.reportPath}; niespełnione: {(failed.Count == 0 ? "brak" : string.Join(", ", failed))}");
            }
            catch (Exception error)
            {
                Debug.LogError($"[AIAssets] {error}");
                result.ok = false;
                result.error = $"{error.GetType().Name}: {error.Message}";
                code = 2;
            }
            if (request != null && !string.IsNullOrEmpty(request.resultPath))
                File.WriteAllText(request.resultPath, JsonUtility.ToJson(result, true));
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        static string Argument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        /// <summary>The pipeline's copy of the ship sizes, generated from the simulation's catalogue.</summary>
        public static string DimensionsJson()
        {
            string Number(double value) => value.ToString("0.0###", CultureInfo.InvariantCulture);
            var text = new StringBuilder();
            text.Append("{\n");
            text.Append("  \"_comment\": \"Generated from ShipCatalog (Assets/MorzaBogow/Runtime/Simulation/Definitions.cs) by Morza Bogow > Assety AI > Eksportuj wymiary okretow. ");
            text.Append("length = 2 x Radius; beam and empty draft in metres; weapon posts in order; ram = CanRam. ");
            text.Append("The EditMode test AIAssets_DimensionsFileMatchesTheShipCatalogue keeps it equal to the catalogue.\",\n");
            text.Append("  \"ships\": {\n");
            var classes = Enum.GetValues(typeof(ShipClass)).Cast<ShipClass>().ToList();
            for (var i = 0; i < classes.Count; i++)
            {
                var ship = ShipCatalog.Get(classes[i]);
                var slots = string.Join(", ", ship.Slots.Select(s => $"\"{s}\""));
                text.Append($"    \"{classes[i]}\": {{ \"length\": {Number(ship.Radius * 2)}, \"beam\": {Number(ship.Beam)}, \"draft\": {Number(ship.Draft)}, ");
                text.Append($"\"slots\": [{slots}], \"ram\": {(ship.CanRam ? "true" : "false")} }}{(i + 1 < classes.Count ? "," : string.Empty)}\n");
            }
            text.Append("  }\n}\n");
            return text.ToString();
        }

        /// <summary>
        /// The game's ship of a class for the asset pipeline: -executeMethod ...ExportShipBlockout -aiShip Ram -aiOut folder -aiId ship_ram.
        /// Writes {id}_LOD0..2.obj, {id}.mtl and {id}.json (files, triangles, paints).
        /// </summary>
        public static void ExportShipBlockout()
        {
            var code = 0;
            try
            {
                var shipClass = (ShipClass)Enum.Parse(typeof(ShipClass), Argument("-aiShip") ?? throw new ArgumentException("Brak -aiShip <klasa>."));
                var output = Argument("-aiOut") ?? throw new ArgumentException("Brak -aiOut <folder>.");
                var id = Argument("-aiId") ?? $"ship_{shipClass.ToString().ToLowerInvariant()}";
                Directory.CreateDirectory(output);
                ShipExport.WriteMtl(Path.Combine(output, id + ".mtl"));
                var files = new System.Collections.Generic.List<string>();
                var triangles = new System.Collections.Generic.List<int>();
                for (var detail = 0; detail < GreekShips.Details; detail++)
                {
                    var file = $"{id}_LOD{detail}.obj";
                    triangles.Add(ShipExport.WriteObj(shipClass, detail, Path.Combine(output, file), id + ".mtl"));
                    files.Add(file);
                }
                File.WriteAllText(Path.Combine(output, id + ".json"), ShipExport.Manifest(id, shipClass, files, triangles));
                Debug.Log($"[AIAssets] eksport {shipClass} → {output}: {string.Join("/", triangles)} trójkątów");
            }
            catch (Exception error)
            {
                Debug.LogError($"[AIAssets] {error}");
                code = 2;
            }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        /// <summary>HDRP materials in the Greek palette, as the battle scene's builder makes them, for previews and exports.</summary>
        public static PlaceholderMaterials ShipMaterials()
        {
            Material Lit(string name, Color colour, float metallic, float smoothness)
            {
                var material = new Material(Shader.Find("HDRP/Lit")) { name = name };
                material.SetColor("_BaseColor", colour);
                material.SetFloat("_Metallic", metallic);
                material.SetFloat("_Smoothness", smoothness);
                UnityEditor.Rendering.HighDefinition.HDShaderUtils.ResetMaterialKeywords(material);
                return material;
            }
            var wood = Lit("wood", GreekShips.Palette.Wood, 0f, 0.3f);
            return new PlaceholderMaterials
            {
                Wood = wood,
                Timber = wood,
                Bronze = Lit("bronze", GreekShips.Palette.Bronze, 0.85f, 0.55f),
                Pitch = Lit("pitch", GreekShips.Palette.Pitch, 0f, 0.35f),
                Ochre = Lit("ochre", GreekShips.Palette.Ochre, 0f, 0.25f),
                Paint = Lit("paint", GreekShips.Palette.Paint, 0f, 0.3f),
                PlayerSail = Lit("sail", new Color(0.12f, 0.22f, 0.6f), 0f, 0.2f),
                EnemySail = Lit("sail_enemy", new Color(0.55f, 0.08f, 0.08f), 0f, 0.2f),
            };
        }

        /// <summary>A ship of a class exactly as the battle builds it (ShipVisual), rowing at cruise, outside any battle.</summary>
        public static Presentation.ShipVisual ShipAsInBattle(ShipClass shipClass, int owner, PlaceholderMaterials materials)
        {
            var world = new World(1);
            var ship = world.SpawnShip(owner, shipClass, 0, 0);
            var view = Presentation.ShipVisual.Create(ship, materials);
            for (var i = 0; i < 12; i++) view.Animate(0.7f, i * 0.25f, 0.25f);
            return view;
        }

        /// <summary>PNG previews of every class as the battle shows it: -executeMethod ...PreviewShips -aiOut folder.</summary>
        public static void PreviewShips()
        {
            var output = Argument("-aiOut") ?? Path.Combine(Directory.GetParent(Application.dataPath).FullName, "artifacts-log", "ship-previews");
            Directory.CreateDirectory(output);
            var materials = ShipMaterials();
            var code = 0;
            var index = 0;
            foreach (ShipClass shipClass in Enum.GetValues(typeof(ShipClass)))
            {
                var view = ShipAsInBattle(shipClass, index++ % 2, materials);
                foreach (var decal in view.GetComponentsInChildren<UnityEngine.Rendering.HighDefinition.WaterDecal>(true)) decal.gameObject.SetActive(false);
                if (!AIAssetPreview.RenderObject(view.gameObject, Path.Combine(output, $"{shipClass}.png"), -140f, out var why))
                {
                    Debug.LogError($"[AIAssets] podgląd {shipClass}: {why}");
                    code = 2;
                }
                if (view != null) UnityEngine.Object.DestroyImmediate(view.gameObject);
            }
            Debug.Log($"[AIAssets] podglądy okrętów: {output}");
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        /// <summary>
        /// The comparison of ships on one base model under the same light (-aiOut folder, -aiShip class): eight of each side
        /// in their own looks seen from the strategy camera, three of them up close, and the same eight of the player's side in
        /// the class's plain look for reference.
        /// </summary>
        public static void PreviewLooks()
        {
            var output = Argument("-aiOut") ?? Path.Combine(Directory.GetParent(Application.dataPath).FullName, "artifacts-log", "ship-looks");
            var shipClass = Enum.TryParse(Argument("-aiShip") ?? "Ram", out ShipClass parsed) ? parsed : ShipClass.Ram;
            Directory.CreateDirectory(output);
            var materials = ShipMaterials();
            var world = new World(1);
            var code = 0;
            GameObject Fleet(string name, int[] sides, bool plain, int count)
            {
                var root = new GameObject(name);
                var beam = (float)ShipCatalog.Get(shipClass).Beam;
                for (var row = 0; row < sides.Length; row++)
                for (var i = 0; i < count; i++)
                {
                    var ship = world.SpawnShip(sides[row], shipClass, 0, 0);
                    var view = ShipVisual.Create(ship, materials, plain ? null : ShipLookComposer.Compose(ship.Id, ship.Class, ship.Owner));
                    foreach (var decal in view.GetComponentsInChildren<UnityEngine.Rendering.HighDefinition.WaterDecal>(true)) decal.gameObject.SetActive(false);
                    view.Animate(0.35f, 1f, 0.5f);
                    view.transform.SetParent(root.transform, false);
                    view.transform.localPosition = new Vector3(i * beam * 1.9f, 0f, -row * view.Length * 1.25f);
                }
                return root;
            }
            void Shoot(GameObject group, string file, float pitch, float yaw, bool orthographic, int width, int height)
            {
                if (!AIAssetPreview.RenderGroup(group, Path.Combine(output, file), pitch, yaw, orthographic, width, height, out var why))
                {
                    Debug.LogError($"[AIAssets] porównanie {file}: {why}");
                    code = 2;
                }
                UnityEngine.Object.DestroyImmediate(group);
            }
            var name = shipClass.ToString().ToLowerInvariant();
            Shoot(Fleet("Fleets", new[] { 0, 1 }, false, 8), $"{name}_floty_kamera_strategiczna.png", 52f, -150f, true, 1600, 1000);
            Shoot(Fleet("Close", new[] { 0 }, false, 3), $"{name}_zblizenie.png", 30f, -125f, false, 1600, 900);
            Shoot(Fleet("Plain", new[] { 0 }, true, 8), $"{name}_bez_wariantow.png", 52f, -150f, true, 1600, 600);
            Debug.Log($"[AIAssets] porównanie wariantów: {output}");
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        [MenuItem("Morza Bogów/Assety AI/Eksportuj wymiary okrętów")]
        public static void ExportDimensions()
        {
            var path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, DimensionsFile);
            File.WriteAllText(path, DimensionsJson());
            Debug.Log($"[AIAssets] Wymiary okrętów zapisane: {DimensionsFile}");
        }
    }
}
