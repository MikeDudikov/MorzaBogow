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

        /// <summary>
        /// The School at each stage of its damage (docs/art/DESTRUCTION.md), shelled from the harbour: whole, scarred,
        /// local failures, partly collapsed, caught falling into ruin and settled in it; close under neutral light and from
        /// the strategy camera's angle.
        /// -executeMethod MorzaBogow.Editor.BuildingPreview.PreviewSchoolDamage
        /// </summary>
        public static void PreviewSchoolDamage()
        {
            var folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "artifacts-log", "destruction");
            var failed = 0;
            try
            {
                Directory.CreateDirectory(folder);
                var materials = Materials();
                foreach (var (name, share) in new[] { ("intact", 0f), ("surface", 0.2f), ("local", 0.45f), ("partial", 0.85f) })
                    failed += Shot(DamagedSchool(materials, share, -1f), Path.Combine(folder, $"school-{name}.png"), 20f, -125f, false) ? 0 : 1;
                failed += Shot(DamagedSchool(materials, 0.85f, -1f), Path.Combine(folder, "school-partial-strategic.png"), 45f, 30f, true) ? 0 : 1;
                failed += Shot(DamagedSchool(materials, 0.85f, -1f), Path.Combine(folder, "school-partial-back.png"), 22f, 55f, false) ? 0 : 1;
                failed += Shot(DamagedSchool(materials, 0.45f, 0.7f), Path.Combine(folder, "school-falling.png"), 20f, -125f, false) ? 0 : 1;
                failed += Shot(DamagedSchool(materials, 0.45f, 6f), Path.Combine(folder, "school-ruin.png"), 20f, -125f, false) ? 0 : 1;
                failed += Shot(DamagedSchool(materials, 0.45f, 6f), Path.Combine(folder, "school-ruin-strategic.png"), 45f, 30f, true) ? 0 : 1;
                Debug.Log($"[Buildings] destruction previews in {folder}: {(failed == 0 ? "all written" : failed + " failed")}");
            }
            catch (System.Exception error)
            {
                Debug.LogError($"[Buildings] {error}");
                failed++;
            }
            if (Application.isBatchMode) EditorApplication.Exit(failed == 0 ? 0 : 1);
        }

        /// <summary>
        /// A whole home port under fire (docs/art/DESTRUCTION.md): shelled at its harbour side down to 60% and to 25% of its
        /// hull, and in ruins; from the strategy camera's angle and close.
        /// -executeMethod MorzaBogow.Editor.BuildingPreview.PreviewPortDamage
        /// </summary>
        public static void PreviewPortDamage()
        {
            var folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "artifacts-log", "destruction");
            var failed = 0;
            try
            {
                Directory.CreateDirectory(folder);
                var materials = Materials();
                foreach (var (name, share, ruined) in new[] { ("shelled-40", 0.4f, false), ("shelled-75", 0.75f, false), ("ruin", 1f, true) })
                {
                    failed += Shot(DamagedPort(materials, share, ruined), Path.Combine(folder, $"port-{name}-strategic.png"), 45f, 30f, true) ? 0 : 1;
                    failed += Shot(DamagedPort(materials, share, ruined), Path.Combine(folder, $"port-{name}-close.png"), 26f, -130f, false) ? 0 : 1;
                }
                Debug.Log($"[Buildings] port damage previews in {folder}: {(failed == 0 ? "all written" : failed + " failed")}");
            }
            catch (System.Exception error)
            {
                Debug.LogError($"[Buildings] {error}");
                failed++;
            }
            if (Application.isBatchMode) EditorApplication.Exit(failed == 0 ? 0 : 1);
        }

        /// <summary>A home port with every building, shelled from the harbour at its front buildings, every fall over.</summary>
        static GameObject DamagedPort(PlaceholderMaterials materials, float share, bool ruined)
        {
            var root = new GameObject("Port damage preview");
            var settlement = new Settlement { Id = 3, Owner = 0, HasWorkshop = true, IsTown = true, HasSageSchool = true, HasDock = true };
            var buildings = new PortBuildings(root.transform, materials, settlement, 1f, null, null);
            buildings.Update(settlement, false, 0f);
            // Bolts and stones on the store, the workshop and the houses nearest the harbour.
            for (var i = 0; i < 14; i++)
            {
                var at = buildings.Root.TransformPoint(new Vector3(-2.5f - (i % 4) * 1.6f, 2f, -7.5f + i * 1.15f));
                buildings.Struck(settlement, at, at + new Vector3(90f, 0f, (i % 3 - 1) * 20f), WeaponType.HeavyCatapult, 10 + i, false);
            }
            settlement.Hull = settlement.MaxHull * (1f - share);
            buildings.Update(settlement, false, 0f);
            for (var i = 0; i < 40 && buildings.Collapsing; i++) buildings.Update(settlement, false, 0.25f);
            if (ruined)
            {
                settlement.Hull = 0;
                buildings.Update(settlement, true, 0f);
                for (var i = 0; i < 40 && buildings.Collapsing; i++) buildings.Update(settlement, true, 0.25f);
            }
            for (var i = buildings.Root.childCount - 1; i >= 0; i--)
            {
                var child = buildings.Root.GetChild(i);
                if (child.name.StartsWith("Collapse") || child.name.StartsWith("Fragments") || child.name.StartsWith("Falling")) Object.DestroyImmediate(child.gameObject);
            }
            var box = new Bounds(buildings.Root.TransformPoint(new Vector3(-6.75f, 2f, 0f)), new Vector3(16f, 1f, 19f));
            var ground = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(ground.GetComponent<Collider>());
            ground.name = AIAssets.AIAssetPreview.Backdrop + " ground";
            ground.transform.SetParent(root.transform, false);
            ground.transform.localPosition = new Vector3(box.center.x, 1.99f, box.center.z);
            ground.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ground.transform.localScale = new Vector3(box.size.x + 80f, box.size.z + 80f, 1f);
            var meadow = materials.Meadow != null ? materials.Meadow : materials.Limestone;
            if (meadow != null) ground.GetComponent<MeshRenderer>().sharedMaterial = meadow;
            return root;
        }

        /// <summary>
        /// What a whole port's fall into ruin costs on this machine's CPU in the editor (informative only): every building
        /// at once, frame by frame at 60 frames a second. Five runs; medians and the worst frame in the log.
        /// -executeMethod MorzaBogow.Editor.BuildingPreview.MeasurePortDamage
        /// </summary>
        public static void MeasurePortDamage()
        {
            var materials = Materials();
            var first = new System.Collections.Generic.List<double>();
            var launch = new System.Collections.Generic.List<double>();
            var frames = new System.Collections.Generic.List<double>();
            var worst = 0.0;
            var triangles = 0;
            var renderers = 0;
            var inAirMost = 0;
            for (var run = 0; run < 5; run++)
            {
                var root = new GameObject("Measure");
                var settlement = new Settlement { Id = 3 + run, Owner = 0, HasWorkshop = true, IsTown = true, HasSageSchool = true, HasDock = true };
                var buildings = new PortBuildings(root.transform, materials, settlement, 1f, null, null);
                buildings.Update(settlement, false, 0f);
                var watch = System.Diagnostics.Stopwatch.StartNew();
                settlement.Hull = settlement.MaxHull * 0.85;
                buildings.Update(settlement, false, 0f);
                first.Add(watch.Elapsed.TotalMilliseconds);
                watch.Restart();
                settlement.Hull = 0;
                buildings.Update(settlement, true, 0f);
                launch.Add(watch.Elapsed.TotalMilliseconds);
                for (var i = 0; i < 400 && buildings.Collapsing; i++)
                {
                    inAirMost = System.Math.Max(inAirMost, StructureDamageView.BodiesInAir);
                    watch.Restart();
                    buildings.Update(settlement, true, 1f / 60f);
                    var ms = watch.Elapsed.TotalMilliseconds;
                    frames.Add(ms);
                    worst = System.Math.Max(worst, ms);
                }
                triangles = 0;
                renderers = 0;
                foreach (var filter in buildings.Root.GetComponentsInChildren<MeshFilter>(false))
                {
                    if (filter.sharedMesh == null) continue;
                    triangles += filter.sharedMesh.triangles.Length / 3;
                    renderers++;
                }
                foreach (PortBuilding kind in System.Enum.GetValues(typeof(PortBuilding)))
                    foreach (var view in buildings.DamagesOf(kind)) view.Forget();
                foreach (var owned in root.GetComponentsInChildren<OwnedMeshes>(true))
                    if (owned.Meshes != null)
                        foreach (var mesh in owned.Meshes.ToArray()) if (mesh != null) Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(root);
            }
            double Median(System.Collections.Generic.List<double> list)
            {
                list.Sort();
                return list.Count == 0 ? 0 : list[list.Count / 2];
            }
            Debug.Log($"[Destruction] whole port, 5 runs, editor CPU: every building's elements built and its first scars {Median(first):0.0} ms; " +
                      $"the fall into ruin started {Median(launch):0.0} ms; a frame of the fall median {Median(frames):0.00} ms, worst {worst:0.0} ms ({frames.Count} frames); " +
                      $"most pieces in the air {inAirMost}; the ruin at rest: {renderers} renderers, {triangles} triangles");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>
        /// What the School's destruction costs on this machine's CPU in the editor (informative only; the build decides):
        /// building its elements, a local failure, the fall into ruin frame by frame at 60 frames a second, the rubble at
        /// rest. Ten runs; medians and the worst frame in the log.
        /// -executeMethod MorzaBogow.Editor.BuildingPreview.MeasureSchoolDamage
        /// </summary>
        public static void MeasureSchoolDamage()
        {
            var materials = Materials();
            var model = new System.Collections.Generic.List<double>();
            var local = new System.Collections.Generic.List<double>();
            var launch = new System.Collections.Generic.List<double>();
            var frames = new System.Collections.Generic.List<double>();
            var worst = 0.0;
            var triangles = 0;
            var renderers = 0;
            for (var run = 0; run < 10; run++)
            {
                var root = new GameObject("Measure");
                var settlement = new Settlement { Id = 3 + run, Owner = 0, HasWorkshop = true, IsTown = true, HasSageSchool = true, HasDock = true };
                var buildings = new PortBuildings(root.transform, materials, settlement, 1f, null, null);
                buildings.Update(settlement, false, 0f);
                var watch = System.Diagnostics.Stopwatch.StartNew();
                settlement.Hull = settlement.MaxHull * 0.9;
                buildings.Update(settlement, false, 0f);
                model.Add(watch.Elapsed.TotalMilliseconds);
                watch.Restart();
                settlement.Hull = settlement.MaxHull * 0.5;
                buildings.Update(settlement, false, 0f);
                local.Add(watch.Elapsed.TotalMilliseconds);
                for (var i = 0; i < 300 && buildings.Collapsing; i++) buildings.Update(settlement, false, 1f / 60f);
                watch.Restart();
                settlement.Hull = 0;
                buildings.Update(settlement, true, 0f);
                launch.Add(watch.Elapsed.TotalMilliseconds);
                for (var i = 0; i < 300 && buildings.Collapsing; i++)
                {
                    watch.Restart();
                    buildings.Update(settlement, true, 1f / 60f);
                    var ms = watch.Elapsed.TotalMilliseconds;
                    frames.Add(ms);
                    worst = System.Math.Max(worst, ms);
                }
                var view = buildings.DamageOf(PortBuilding.SageSchool);
                triangles = 0;
                renderers = 0;
                foreach (var filter in view.Frame.GetComponentsInChildren<MeshFilter>(false))
                {
                    if (filter.sharedMesh == null) continue;
                    triangles += filter.sharedMesh.triangles.Length / 3;
                    renderers++;
                }
                foreach (var owned in root.GetComponentsInChildren<OwnedMeshes>(true))
                    if (owned.Meshes != null)
                        foreach (var mesh in owned.Meshes.ToArray()) if (mesh != null) Object.DestroyImmediate(mesh);
                view.Forget();
                Object.DestroyImmediate(root);
            }
            double Median(System.Collections.Generic.List<double> list)
            {
                list.Sort();
                return list.Count == 0 ? 0 : list[list.Count / 2];
            }
            Debug.Log($"[Destruction] School, 10 runs, editor CPU: elements built and first scars {Median(model):0.0} ms; local failure {Median(local):0.0} ms; " +
                      $"fall into ruin started {Median(launch):0.0} ms; a frame of the fall median {Median(frames):0.00} ms, worst {worst:0.0} ms " +
                      $"({frames.Count} frames); at rest {renderers} renderers, {triangles} triangles");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>
        /// Ships at the stages of their damage (docs/art/DESTRUCTION.md), side by side under the same light: whole, holed
        /// and listing, the mast caught falling, the mast down over the side; a ram and a catapult ship.
        /// -executeMethod MorzaBogow.Editor.BuildingPreview.PreviewShipDamage
        /// </summary>
        public static void PreviewShipDamage()
        {
            var folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "artifacts-log", "destruction");
            var failed = 0;
            try
            {
                Directory.CreateDirectory(folder);
                var materials = AIAssets.AIAssetBatch.ShipMaterials();
                foreach (var shipClass in new[] { ShipClass.Ram, ShipClass.Bomber })
                {
                    GameObject Row()
                    {
                        var row = new GameObject($"{shipClass} damage");
                        var world = new World(1);
                        var stages = new[] { (1f, -1f), (0.65f, -1f), (0.3f, 0.6f), (0.3f, 9f) };
                        for (var i = 0; i < stages.Length; i++)
                        {
                            var (hull, fall) = stages[i];
                            var ship = world.SpawnShip(i % 2, shipClass, 0, 0);
                            var view = ShipVisual.Create(ship, materials, ShipLookComposer.Compose(ship.Id, ship.Class, ship.Owner));
                            foreach (var decal in view.GetComponentsInChildren<UnityEngine.Rendering.HighDefinition.WaterDecal>(true)) decal.gameObject.SetActive(false);
                            view.SetCondition(1f, ship.ModuleHp);
                            if (hull < 1f)
                            {
                                // Shot through low on the port side, as from a ship on that beam.
                                for (var k = 0; k < 3; k++) view.Hit(view.Model.TransformPoint(new Vector3(-3f, 0.5f + k * 0.4f, -3f + k * 3f)), 1.4f, k == 0);
                                view.StickArrows(view.Model.TransformPoint(new Vector3(-20f, 4f, 0f)), 9);
                            }
                            view.SetCondition(hull, ship.ModuleHp);
                            var t = 0f;
                            for (var k = 0; k < 60 && (fall < 0f ? k < 2 : t < fall); k++, t += 0.1f) view.Animate(0.2f, t, 0.1f);
                            view.transform.SetParent(row.transform, false);
                            view.transform.localPosition = new Vector3(i * view.Length * 1.15f, 0f, 0f);
                            view.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                        }
                        return row;
                    }
                    failed += Shot(Row(), Path.Combine(folder, $"ship-{shipClass.ToString().ToLowerInvariant()}-stages.png"), 24f, -150f, false) ? 0 : 1;
                    failed += Shot(Row(), Path.Combine(folder, $"ship-{shipClass.ToString().ToLowerInvariant()}-stages-strategic.png"), 50f, -150f, true) ? 0 : 1;
                }
                Debug.Log($"[Buildings] ship damage previews in {folder}: {(failed == 0 ? "all written" : failed + " failed")}");
            }
            catch (System.Exception error)
            {
                Debug.LogError($"[Buildings] {error}");
                failed++;
            }
            if (Application.isBatchMode) EditorApplication.Exit(failed == 0 ? 0 : 1);
        }

        /// <summary>
        /// A port shelled at its School from the harbour down to a share of its hull, every fall over; then, ruinAt not
        /// negative, fallen and caught that many game seconds into its fall. Only the School, its damage and the dust stay.
        /// </summary>
        static GameObject DamagedSchool(PlaceholderMaterials materials, float share, float ruinAt)
        {
            var root = new GameObject("School damage preview");
            var settlement = new Settlement { Id = 3, Owner = 0, HasWorkshop = true, IsTown = true, HasSageSchool = true, HasDock = true };
            var buildings = new PortBuildings(root.transform, materials, settlement, 1f, null, null);
            buildings.Update(settlement, false, 0f);
            var box = buildings.Footprint(PortBuilding.SageSchool);
            for (var i = 0; i < 6; i++)
            {
                var at = buildings.Root.TransformPoint(new Vector3(box.max.x - 0.5f, 2f, box.center.z - 3f + i * 1.2f));
                buildings.Struck(settlement, at, at + new Vector3(90f, 0f, 10f), WeaponType.HeavyCatapult, 10 + i, false);
            }
            settlement.Hull = settlement.MaxHull * (1f - share);
            buildings.Update(settlement, false, 0f);
            for (var i = 0; i < 40 && buildings.Collapsing; i++) buildings.Update(settlement, false, 0.25f);
            if (ruinAt >= 0f)
            {
                settlement.Hull = 0;
                buildings.Update(settlement, true, 0f);
                for (var t = 0f; t < ruinAt; t += 0.1f) buildings.Update(settlement, true, Mathf.Min(0.1f, ruinAt - t));
            }
            // The dust and fragments only while it falls: still pictures would freeze them in the air.
            var dust = ruinAt > 0f && ruinAt < PortBuildings.CollapseSeconds;
            for (var i = buildings.Root.childCount - 1; i >= 0; i--)
            {
                var child = buildings.Root.GetChild(i);
                var particles = child.name.StartsWith("Collapse") || child.name.StartsWith("Fragments") || child.name.StartsWith("Falling");
                if (!child.name.StartsWith("SageSchool") && !(dust && particles)) Object.DestroyImmediate(child.gameObject);
            }
            var ground = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(ground.GetComponent<Collider>());
            ground.name = AIAssets.AIAssetPreview.Backdrop + " ground";
            ground.transform.SetParent(root.transform, false);
            ground.transform.localPosition = new Vector3(box.center.x, 1.99f, box.center.z);
            ground.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ground.transform.localScale = new Vector3(box.size.x + 80f, box.size.z + 80f, 1f);
            var meadow = materials.Meadow != null ? materials.Meadow : materials.Limestone;
            if (meadow != null) ground.GetComponent<MeshRenderer>().sharedMaterial = meadow;
            return root;
        }

        /// <summary>Lists every renderer of a silver site with its materials, to see what draws each part of it.</summary>
        public static void ListSiteMaterials()
        {
            var materials = Materials();
            var site = ResourceSiteVisual.Create(new ResourceNode { Id = 11, Type = ResourceType.Silver, Stock = 1000, Capacity = 1000 }, materials);
            foreach (var renderer in site.GetComponentsInChildren<Renderer>(true))
            {
                var names = string.Join(", ", System.Array.ConvertAll(renderer.sharedMaterials, m => m == null ? "null" : m.name));
                Debug.Log($"[Sites] {renderer.transform.parent?.name}/{renderer.name} ({renderer.GetType().Name}): {names}");
            }
            Object.DestroyImmediate(site.gameObject);
            if (Application.isBatchMode) EditorApplication.Exit(0);
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
