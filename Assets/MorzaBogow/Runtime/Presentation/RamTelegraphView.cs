using System;
using System.Collections.Generic;
using MorzaBogow.Presentation.Hud;
using MorzaBogow.Simulation;
using UnityEngine;

namespace MorzaBogow.Presentation
{
    /// <summary>
    /// A ram's called charge, drawn on the sea for both sides (docs/UZBROJENIE.md, Taran): from the moment the charge is
    /// called (RamPhase.Telegraph) a lane runs from the rammer's bow to its target and fills like a fuse as the telegraph
    /// runs out (Ship.TelegraphTicks against Rules.RamTelegraphSeconds); once full, the blow may land and the lane
    /// pulses. A ring round the target beats faster as the moment nears. The call itself flashes at the bow. Own rams in
    /// the side's colour, the enemy's in the enemy's, as the interface shows them. Nothing is drawn for a rammer the
    /// player cannot see. Reads the simulation only.
    /// </summary>
    public sealed class RamTelegraphView
    {
        /// <summary>Overlays lie this far above the still water, clear of the swell.</summary>
        public const float Height = 1.2f;

        /// <summary>Ticks from the call until a blow counts (World.StepRamMovement).</summary>
        public static int TelegraphTicksTotal => (int)Math.Ceiling(Rules.RamTelegraphSeconds * Rules.TickRate);

        /// <summary>How far the telegraph has run: 0 at the call, 1 when the charge may strike.</summary>
        public static float Fuse(Ship ship) => Mathf.Clamp01(1f - (float)ship.TelegraphTicks / TelegraphTicksTotal);

        public static bool Charging(Ship ship) => ship.Order == OrderKind.Ram && ship.RamState == RamPhase.Telegraph;

        sealed class Lane
        {
            public GameObject Root;
            public Transform Back, Fill, Ring;
            public bool Own;
            public float FuseShown;
        }

        readonly PlaceholderMaterials materials;
        readonly Transform holder;
        readonly Dictionary<int, Lane> lanes = new Dictionary<int, Lane>();
        readonly Dictionary<int, RamPhase> lastPhase = new Dictionary<int, RamPhase>();
        readonly List<int> gone = new List<int>();

        public RamTelegraphView(PlaceholderMaterials materials)
        {
            this.materials = materials;
            holder = new GameObject("Ram telegraphs").transform;
        }

        public Transform Root => holder;

        /// <summary>Charges drawn in the last frame.</summary>
        public int Shown { get; private set; }

        /// <summary>Calls flashed so far (tests and tools).</summary>
        public int Calls { get; private set; }

        public bool ShowsFor(int shipId) => lanes.TryGetValue(shipId, out var lane) && lane.Root.activeSelf;

        /// <summary>The fuse as drawn for this rammer, 0 to 1; -1 when nothing is drawn.</summary>
        public float FuseShownFor(int shipId) => ShowsFor(shipId) ? lanes[shipId].FuseShown : -1f;

        public bool ShownAsOwn(int shipId) => ShowsFor(shipId) && lanes[shipId].Own;

        /// <param name="flash">Called with the bow's position when a charge is called in sight.</param>
        public void Update(World world, int faction, IReadOnlyDictionary<int, ShipVisual> views, float time, Action<Vector3> flash)
        {
            Shown = 0;
            foreach (var pair in lanes) pair.Value.Root.SetActive(false);
            foreach (var ship in world.Ships)
            {
                var charging = Charging(ship);
                var was = lastPhase.TryGetValue(ship.Id, out var phase) && phase == RamPhase.Telegraph && ship.Order == OrderKind.Ram;
                lastPhase[ship.Id] = ship.Order == OrderKind.Ram ? ship.RamState : RamPhase.Approach;
                if (!charging) continue;
                if (!world.CanSee(faction, ship) || !views.TryGetValue(ship.Id, out var view) || view == null || !view.gameObject.activeInHierarchy) continue;
                var target = world.FindShip(ship.TargetId);
                if (target == null || !views.TryGetValue(target.Id, out var aim) || aim == null) continue;

                var forward = Quaternion.Euler(0f, view.transform.eulerAngles.y, 0f) * Vector3.forward;
                var bow = view.transform.position + forward * view.Length * 0.5f;
                bow.y = Height;
                var end = aim.transform.position;
                end.y = Height;
                var offset = end - bow;
                offset.y = 0f;
                var length = Mathf.Max(0.5f, offset.magnitude - aim.Beam * 0.5f);
                if (!was)
                {
                    Calls++;
                    flash?.Invoke(bow);
                }

                var own = ship.Owner == faction;
                var lane = LaneFor(ship.Id, own);
                lane.Root.SetActive(true);
                lane.Root.transform.SetPositionAndRotation(bow, Quaternion.LookRotation(offset.sqrMagnitude > 1e-4f ? offset.normalized : forward));
                var width = Mathf.Max(3f, view.Beam * 0.9f);
                var fuse = Fuse(ship);
                lane.FuseShown = fuse;
                var armed = fuse >= 1f;
                var pulse = armed ? 1f + 0.25f * Mathf.Abs(Mathf.Sin(time * 12f)) : 1f;
                lane.Back.localScale = new Vector3(width, 1f, length);
                lane.Fill.localScale = new Vector3(width * 0.6f * pulse, 1f, Mathf.Max(0.01f, length * fuse));
                lane.Fill.gameObject.SetActive(fuse > 0f);

                // The ring round the target, only where the player sees it, beats faster as the blow nears.
                var seen = world.CanSee(faction, target) && aim.gameObject.activeInHierarchy;
                lane.Ring.gameObject.SetActive(seen);
                if (seen)
                {
                    var beat = 1f + 0.12f * Mathf.Sin(time * Mathf.Lerp(4f, 16f, fuse));
                    var radius = aim.Length * 0.62f * beat;
                    lane.Ring.SetPositionAndRotation(new Vector3(aim.transform.position.x, Height + 0.05f, aim.transform.position.z), Quaternion.identity);
                    lane.Ring.localScale = new Vector3(radius, 1f, radius);
                }
                Shown++;
            }

            gone.Clear();
            foreach (var id in lastPhase.Keys) if (world.FindShip(id) == null) gone.Add(id);
            foreach (var id in gone)
            {
                lastPhase.Remove(id);
                if (lanes.TryGetValue(id, out var lane)) { UnityEngine.Object.Destroy(lane.Root); lanes.Remove(id); }
            }
        }

        Lane LaneFor(int shipId, bool own)
        {
            if (lanes.TryGetValue(shipId, out var lane) && lane.Own == own) return lane;
            if (lane != null) UnityEngine.Object.Destroy(lane.Root);
            var colour = own ? HudPalette.Ally : HudPalette.Enemy;
            lane = new Lane { Own = own, Root = new GameObject($"Ram telegraph {shipId}") };
            lane.Root.transform.SetParent(holder, false);
            lane.Back = Strip(lane.Root.transform, "Charge lane", OverlayMeshes.WithAlpha(colour, 0.22f), 0f);
            lane.Fill = Strip(lane.Root.transform, "Charge fuse", OverlayMeshes.WithAlpha(colour, 0.85f), 0.02f);
            // The ring goes with the lane but is placed in world space round the target.
            lane.Ring = RingMarker.Create("Ram target", OverlayMeshes.Colour(materials?.TargetRing, OverlayMeshes.WithAlpha(colour, 0.85f)), lane.Root.transform, 1f);
            lanes[shipId] = lane;
            return lane;
        }

        Transform Strip(Transform parent, string name, Color colour, float lift)
        {
            var strip = new GameObject(name);
            strip.transform.SetParent(parent, false);
            strip.transform.localPosition = new Vector3(0f, lift, 0f);
            strip.AddComponent<MeshFilter>().sharedMesh = OverlayMeshes.Lane;
            var renderer = strip.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var material = OverlayMeshes.Colour(materials?.Selection, colour);
            if (material != null) renderer.sharedMaterial = material;
            return strip.transform;
        }

        public void Destroy()
        {
            if (holder != null) UnityEngine.Object.Destroy(holder.gameObject);
            lanes.Clear();
            lastPhase.Clear();
        }
    }
}
