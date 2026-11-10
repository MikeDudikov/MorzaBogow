using System.IO;
using MorzaBogow.Presentation;
using MorzaBogow.Simulation;
using UnityEditor;
using UnityEngine;

namespace MorzaBogow.Editor
{
    /// <summary>
    /// Pictures of a home port's buildings as PortBuildings raises them (the 4th-century-BC rework), for a look at the
    /// result: the whole port from the strategy camera's angle and close, the School close, rising and in ruins, the
    /// houses and the workshop. Written to artifacts-log/buildings, never into Assets; the scene's own materials are only
    /// read. Batch mode with a graphics device (without -nographics), no game window:
    /// -executeMethod MorzaBogow.Editor.BuildingPreview.PreviewPort
    /// </summary>
    public static class BuildingPreview
    {
        public static void PreviewPort()
        {
            var folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "artifacts-log", "buildings");
            var failed = 0;
            try
            {
                Directory.CreateDirectory(folder);
                var materials = Materials();
                failed += Shot(Port(materials, null, 1f, false), Path.Combine(folder, "port-strategic.png"), 45f, 30f, true) ? 0 : 1;
                failed += Shot(Port(materials, null, 1f, false), Path.Combine(folder, "port-close.png"), 24f, -130f, false) ? 0 : 1;
                failed += Shot(Port(materials, PortBuilding.SageSchool, 1f, false), Path.Combine(folder, "school-close.png"), 18f, -125f, false) ? 0 : 1;
                foreach (var progress in new[] { 0.3f, 0.5f, 0.7f, 0.9f })
                    failed += Shot(Port(materials, PortBuilding.SageSchool, progress, false), Path.Combine(folder, $"school-rising-{Mathf.RoundToInt(progress * 100f)}.png"), 20f, -125f, false) ? 0 : 1;
                failed += Shot(Port(materials, PortBuilding.SageSchool, 1f, true), Path.Combine(folder, "school-ruin.png"), 20f, -125f, false) ? 0 : 1;
                failed += Shot(Port(materials, PortBuilding.Store, 1f, false), Path.Combine(folder, "houses-close.png"), 18f, -130f, false) ? 0 : 1;
                failed += Shot(Port(materials, PortBuilding.Store, 0.55f, false), Path.Combine(folder, "houses-rising-55.png"), 18f, -130f, false) ? 0 : 1;
                failed += Shot(Port(materials, PortBuilding.Workshop, 1f, false), Path.Combine(folder, "workshop-close.png"), 18f, -130f, false) ? 0 : 1;
                Debug.Log($"[Buildings] previews in {folder}: {(failed == 0 ? "all written" : failed + " failed")}");
            }
            catch (System.Exception error)
            {
                Debug.LogError($"[Buildings] {error}");
                failed++;
            }
            if (Application.isBatchMode) EditorApplication.Exit(failed == 0 ? 0 : 1);
        }

        /// <summary>
        /// The resource sites as the battle builds them (ResourceSiteVisual): the silver mine with its washery and the
        /// timber yard with its log stacks, and the School caught halfway through its fall.
        /// -executeMethod MorzaBogow.Editor.BuildingPreview.PreviewSites
        /// </summary>
        public static void PreviewSites()
        {
            var folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "artifacts-log", "buildings");
            var failed = 0;
            try
            {
                Directory.CreateDirectory(folder);
                var materials = Materials();
                foreach (var (type, id) in new[] { (ResourceType.Silver, 11), (ResourceType.Wood, 12) })
                {
                    var node = new ResourceNode { Id = id, Type = type, Stock = 1000, Capacity = 1000 };
                    var site = ResourceSiteVisual.Create(node, materials);
                    failed += Shot(site.gameObject, Path.Combine(folder, $"site-{type.ToString().ToLowerInvariant()}.png"), 38f, 20f, false) ? 0 : 1;
                }
                // The School halfway through its fall: sunk and leaning, the roof down, dust and tiles in the air.
                var settlement = new Settlement { Id = 3, Owner = 0, HasWorkshop = true, IsTown = true, HasSageSchool = true, HasDock = true };
                var land = new GameObject("Port preview");
                var buildings = new PortBuildings(land.transform, materials, settlement, 1f, null, null);
                buildings.Update(settlement, false, 0f);
                buildings.Update(settlement, true, 0f);
                buildings.Update(settlement, true, PortBuildings.CollapseSeconds * 0.55f);
                for (var i = buildings.Root.childCount - 1; i >= 0; i--)
                {
                    var child = buildings.Root.GetChild(i);
                    if (!child.name.StartsWith("SageSchool") && !child.name.StartsWith("Collapse") && !child.name.StartsWith("Falling")) Object.DestroyImmediate(child.gameObject);
                }
                failed += Shot(land, Path.Combine(folder, "school-collapse.png"), 18f, -125f, false) ? 0 : 1;
            }
            catch (System.Exception error)
            {
                Debug.LogError($"[Buildings] {error}");
                failed++;
            }
            if (Application.isBatchMode) EditorApplication.Exit(failed == 0 ? 0 : 1);
        }

        /// <summary>The battle scene's materials for buildings: the palette of the previews and the shore's limestone, read only.</summary>
        static PlaceholderMaterials Materials()
        {
            var materials = AIAssets.AIAssetBatch.ShipMaterials();
            materials.Limestone = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Shore/Shore_Limestone.mat");
            materials.Meadow = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Shore/Shore_Meadow.mat");
            var timber = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Placeholders/Placeholder_Timber.mat");
            if (timber != null) materials.Timber = timber;
            materials.Flash = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Placeholders/Placeholder_Flash.mat");
            materials.Neutral = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Placeholders/Placeholder_Neutral.mat");
            Material Placeholder(string name) => AssetDatabase.LoadAssetAtPath<Material>($"Assets/Art/Placeholders/Placeholder_{name}.mat");
            materials.Rock = Placeholder("Rock");
            materials.MineDark = Placeholder("MineDark");
            materials.SilverOre = Placeholder("SilverOre");
            materials.Foliage = Placeholder("Foliage");
            materials.OliveLeaves = Placeholder("OliveLeaves");
            materials.Sand = Placeholder("Sand");
            // Read only: the collapse's dust is a copy of the spray material, never the asset itself.
            materials.Spray = Placeholder("Spray");
            return materials;
        }

        /// <summary>
        /// A home port with every building over flat ground; with only given, that building alone: standing, rising to
        /// progress (below 1) or in ruins. The store always stands in a port, so it is left out unless it is the one shown.
        /// </summary>
        static GameObject Port(PlaceholderMaterials materials, PortBuilding? only, float progress, bool ruined)
        {
            var root = new GameObject("Port preview");
            var settlement = new Settlement { Id = 3, Owner = 0, HasWorkshop = true, IsTown = true, HasSageSchool = true, HasDock = true };
            if (only != null && progress < 1f)
            {
                switch (only.Value)
                {
                    case PortBuilding.Workshop: settlement.HasWorkshop = false; settlement.ProjectKind = ProjectKind.Workshop; break;
                    case PortBuilding.Town: settlement.IsTown = false; settlement.ProjectKind = ProjectKind.Town; break;
                    case PortBuilding.SageSchool: settlement.HasSageSchool = false; settlement.ProjectKind = ProjectKind.SageSchool; break;
                    case PortBuilding.Dock: settlement.HasDock = false; settlement.ProjectKind = ProjectKind.Dock; break;
                }
                settlement.Project = new BuildOrder { TotalTicks = 1000, DoneTicks = Mathf.RoundToInt(progress * 1000f), Started = true };
            }
            var buildings = new PortBuildings(root.transform, materials, settlement, 1f, null, null);
            buildings.Update(settlement, ruined, 0f);

            // The store's houses rise as the store does not: show them in their courses at a share by staging by hand.
            if (only == PortBuilding.Store && progress < 1f)
            {
                var store = buildings.Root.Find(nameof(PortBuilding.Store));
                foreach (Transform piece in store)
                {
                    var socle = piece.Find("Socle");
                    if (socle == null) continue;
                    var courses = new System.Collections.Generic.List<Transform>();
                    foreach (Transform part in socle)
                    {
                        if (part.name.StartsWith("Course")) courses.Add(part);
                        else part.gameObject.SetActive(false);
                    }
                    var walls = Mathf.Clamp01((progress - PortBuildings.WallsFrom) / (PortBuildings.RoofFrom - PortBuildings.WallsFrom));
                    for (var i = 0; i < courses.Count; i++) courses[i].gameObject.SetActive(i < Mathf.CeilToInt(walls * courses.Count));
                    var roof = piece.Find("Roof");
                    if (roof != null) roof.gameObject.SetActive(progress >= PortBuildings.RoofFrom);
                    var scaffold = piece.Find("Scaffolding");
                    if (scaffold != null) scaffold.gameObject.SetActive(true);
                }
            }

            var bounds = new Bounds();
            var first = true;
            foreach (PortBuilding kind in System.Enum.GetValues(typeof(PortBuilding)))
            {
                if (only != null && kind != only) continue;
                var box = buildings.Footprint(kind);
                if (box.size == Vector3.zero) continue;
                if (first) bounds = box; else bounds.Encapsulate(box);
                first = false;
            }
            if (only != null)
            {
                // Everything but the building shown (and its ruin) goes, hidden parts included, so the frame fits it.
                for (var i = buildings.Root.childCount - 1; i >= 0; i--)
                {
                    var child = buildings.Root.GetChild(i);
                    var keep = child.gameObject.activeSelf && (child.name == only.ToString() || child.name.StartsWith(only + " "));
                    if (!keep) Object.DestroyImmediate(child.gameObject);
                }
            }

            // Ground well beyond the frame, so nothing sunk below it shows past its edge; not framed (a backdrop).
            var ground = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(ground.GetComponent<Collider>());
            ground.name = AIAssets.AIAssetPreview.Backdrop + " ground";
            ground.transform.SetParent(root.transform, false);
            ground.transform.localPosition = new Vector3(bounds.center.x, 1.99f, bounds.center.z);
            ground.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ground.transform.localScale = new Vector3(bounds.size.x + 80f, bounds.size.z + 80f, 1f);
            var meadow = materials.Meadow != null ? materials.Meadow : materials.Limestone;
            if (meadow != null) ground.GetComponent<MeshRenderer>().sharedMaterial = meadow;
            return root;
        }

        static bool Shot(GameObject root, string path, float pitch, float yaw, bool orthographic)
        {
            var done = AIAssets.AIAssetPreview.RenderGroup(root, path, pitch, yaw, orthographic, 1600, 1000, out var why);
            Debug.Log($"[Buildings] preview {Path.GetFileName(path)}: {(done ? "ok" : why)}");
            if (root != null) Object.DestroyImmediate(root);
            return done;
        }
    }
}
