using System.Collections.Generic;
using UnityEngine;

namespace MorzaBogow.Presentation
{
    /// <summary>
    /// The mast giving way when the hull is far gone (docs/art/DESTRUCTION.md), the ship's part of the destruction
    /// slice. At the partial collapse (DamagePlan.PartialFrom of the hull lost) the mast breaks a little above the deck,
    /// where it leaves the mast partner, and goes over the side the ship lists to (its breaches' side): the yard and the
    /// sail come down with it, the sail slack, and two lines of the rigging still hold the wreckage alongside, so it moves
    /// with the hull and nothing is moved twice. Shown at once when the ship was out of sight; mended with the hull in
    /// dock. Presentation only: the game's hull decides when, nothing here feeds back.
    /// </summary>
    public sealed partial class ShipVisual
    {
        /// <summary>The share of hull left below which the mast is down, and above which a dock's repair puts it back.</summary>
        public const float MastBreaksBelow = 1f - DamagePlan.PartialFrom, MastMendedAbove = MastBreaksBelow + 0.1f;

        /// <summary>How long the mast takes to come down, in game seconds.</summary>
        public const float MastFallSeconds = 1.5f;

        Transform mastStump, fallenRig;
        readonly List<Transform> mastRopes = new List<Transform>();
        readonly List<Mesh> mastMeshes = new List<Mesh>();
        Vector3 mastPivot, mastAxis, mastFallWay, mastTop;
        float mastFall = -1f, mastAngle;
        int conditionFrame = -100;
        bool mastBreakPending, mastSplashPending;
        static GreekMesh ropeUnit;

        public bool MastBroken => fallenRig != null;
        public bool MastFalling => fallenRig != null && mastFall < MastFallSeconds;
        public Transform FallenMast => fallenRig;
        public Transform MastStump => mastStump;
        /// <summary>The way the mast fell, in the model's frame (across the side it went over).</summary>
        public Vector3 MastFallWay => mastFallWay;
        /// <summary>Where the masthead is now, in the model's frame.</summary>
        public Vector3 MastheadNow => fallenRig != null ? mastPivot + fallenRig.localRotation * (mastTop - mastPivot) : parts.MastFoot + Vector3.up * parts.Form.MastHeight;

        /// <summary>The stage the hull's damage gives the ship (DamagePlan), for the views and tests that read it.</summary>
        public DestructionStage DamageStage => DamagePlan.StageOf(1f - hullFraction, false, !MastFalling);

        /// <summary>Once, when the mast has just broken: where it snapped, in the world, for splinters (BattleController).</summary>
        public bool TakeMastBreak(out Vector3 at)
        {
            at = fallenRig != null ? model.TransformPoint(mastPivot) : Vector3.zero;
            if (!mastBreakPending) return false;
            mastBreakPending = false;
            return true;
        }

        /// <summary>Once, when the fallen masthead has struck the water: where, in the world.</summary>
        public bool TakeMastSplash(out Vector3 at)
        {
            at = fallenRig != null ? model.TransformPoint(MastheadNow) : Vector3.zero;
            if (!mastSplashPending) return false;
            mastSplashPending = false;
            return true;
        }

        /// <summary>From SetCondition: the mast goes or comes back with the hull; seen falling only if the ship was in sight.</summary>
        void UpdateMast(float fraction)
        {
            var frame = Time.frameCount;
            var seen = frame - conditionFrame <= 1;
            conditionFrame = frame;
            if (rig == null || parts == null || parts.Form.MastHeight <= 0.5f) return;
            if (fallenRig == null && fraction < MastBreaksBelow) BreakMast(seen);
            else if (fallenRig != null && fraction >= MastMendedAbove) RestoreMast();
        }

        void BreakMast(bool animate)
        {
            var form = parts.Form;
            var hash = DestructionHash.Mix((uint)ShipId, 4099);
            // As GreekShips.BuildRig raises it: a tapering pole from its step to the masthead, a bowed yard near the top.
            var foot = parts.MastFoot;
            var height = form.MastHeight;
            var radius = 0.012f * form.Length + 0.08f;
            mastTop = foot + Vector3.up * height;
            var yardY = mastTop.y - height * (form.CrowsNest ? 0.16f : 0.08f);
            var yardZ = foot.z + radius * 1.5f;
            var half = form.SailWidth * 0.5f + 0.4f;

            // It snaps where the partner holds it, a little above the deck, and goes over the side the ship lists to.
            var snap = DestructionHash.Range(hash, 1, 0.75f, 1.5f);
            mastPivot = foot + Vector3.up * snap;
            var side = Mathf.Abs(listSide) > 0.5f ? Mathf.Sign(listSide) : DestructionHash.Unit(hash, 2) < 0.5f ? -1f : 1f;
            mastFallWay = new Vector3(side, 0f, DestructionHash.Range(hash, 3, -0.35f, 0.35f)).normalized;
            mastAxis = Vector3.Cross(Vector3.up, mastFallWay).normalized;
            var upper = height - snap;
            // Past the level until its head is in the water, its heel on the gunwale.
            mastAngle = 90f + Mathf.Atan2(Mathf.Max(0.3f, mastPivot.y + 0.3f), upper) * Mathf.Rad2Deg;
            var rSnap = Mathf.Lerp(radius, radius * 0.6f, snap / height);

            var stump = new GreekShips.MeshBuilder(null);
            stump.Tube(GreekMaterial.Wood, new List<Vector3> { foot, mastPivot }, radius, rSnap, 8);
            Splinters(stump, mastPivot, rSnap, Vector3.up, hash);
            var stumpMesh = stump.Build(GreekMaterial.Wood);
            mastMeshes.Add(stumpMesh.Mesh);
            mastStump = AttachPiece(model, "Mast stump", stumpMesh);

            var spars = new GreekShips.MeshBuilder(null);
            spars.Tube(GreekMaterial.Wood, new List<Vector3> { Vector3.zero, mastTop - mastPivot }, rSnap, radius * 0.6f, 8);
            Splinters(spars, Vector3.zero, rSnap, Vector3.down, hash + 1u);
            spars.Tube(GreekMaterial.Wood, new List<Vector3>
            {
                new Vector3(-half, yardY - 0.15f, yardZ) - mastPivot, new Vector3(0f, yardY + 0.12f, yardZ) - mastPivot, new Vector3(half, yardY - 0.15f, yardZ) - mastPivot,
            }, radius * 0.55f, radius * 0.35f, 6);
            if (form.CrowsNest)
            {
                spars.Tube(GreekMaterial.Wood, new List<Vector3> { mastTop + Vector3.down * 0.75f - mastPivot, mastTop + Vector3.down * 0.15f - mastPivot }, radius * 3.4f, radius * 3.8f, 6);
                spars.Tube(GreekMaterial.Ochre, new List<Vector3> { mastTop + Vector3.down * 0.2f - mastPivot, mastTop + Vector3.down * 0.1f - mastPivot }, radius * 3.9f, radius * 3.9f, 6);
            }
            var sparsMesh = spars.Build(GreekMaterial.Wood, GreekMaterial.Ochre);
            mastMeshes.Add(sparsMesh.Mesh);
            fallenRig = new GameObject("Fallen mast").transform;
            fallenRig.SetParent(model, false);
            fallenRig.localPosition = mastPivot;
            AttachPiece(fallenRig, "Spars", sparsMesh);
            rig.gameObject.SetActive(false);
            // The sail on its yard comes down with it.
            if (sail != null) sail.SetParent(fallenRig, true);
            // Two lines of the rigging, forestay and backstay, still hold it alongside; the rest are gone with the mast.
            ropeUnit ??= RopeUnit();
            for (var k = 0; k < 4; k++) mastRopes.Add(AttachPiece(model, "Loose rope", ropeUnit));

            mastFall = animate ? 0f : MastFallSeconds;
            mastBreakPending = animate;
            mastSplashPending = false;
            PoseMast();
        }

        /// <summary>A dock's repair: a new mast is stepped, the yard and sail go back up, the wreckage is cleared.</summary>
        void RestoreMast()
        {
            if (sail != null)
            {
                sail.SetParent(model, false);
                sail.localPosition = parts.SailTop;
                sail.localRotation = Quaternion.identity;
                sail.localScale = new Vector3(parts.SailSize.x, parts.SailSize.y, 0.2f);
            }
            foreach (var rope in mastRopes) Discard(rope);
            mastRopes.Clear();
            Discard(mastStump);
            Discard(fallenRig);
            mastStump = fallenRig = null;
            foreach (var mesh in mastMeshes) DiscardMesh(mesh);
            mastMeshes.Clear();
            if (rig != null) rig.gameObject.SetActive(true);
            mastFall = -1f;
            mastBreakPending = mastSplashPending = false;
        }

        /// <summary>From AnimateSail while the mast is down: its fall in game time, then it lies still in the hull's frame.</summary>
        void AnimateMast(float delta)
        {
            if (mastFall >= MastFallSeconds) return;
            mastFall = Mathf.Min(MastFallSeconds, mastFall + delta);
            PoseMast();
            if (mastFall >= MastFallSeconds) mastSplashPending = true;
        }

        void PoseMast()
        {
            var u = Mathf.Clamp01(mastFall / MastFallSeconds);
            // Slow at first, then faster: it tips about where it snapped.
            var rotation = Quaternion.AngleAxis(mastAngle * u * u, mastAxis);
            fallenRig.localPosition = mastPivot;
            fallenRig.localRotation = rotation;
            if (sail != null && sail.parent == fallenRig)
            {
                // The sail goes slack as the yard comes down and lies over the side, half under water.
                sail.localPosition = parts.SailTop - mastPivot;
                sail.localRotation = Quaternion.identity;
                sail.localScale = new Vector3(parts.SailSize.x, parts.SailSize.y * Mathf.Lerp(1f, 0.75f, u), Mathf.Lerp(0.2f, 0.03f, u));
            }
            var form = parts.Form;
            var head = MastheadNow;
            var ends = new[] { new Vector3(0f, GreekShips.Sheer(form, 1f), GreekShips.Z(form, 1f)), new Vector3(0f, GreekShips.Sheer(form, 0.05f), GreekShips.Z(form, 0.05f)) };
            for (var k = 0; k < ends.Length && 2 * k + 1 < mastRopes.Count; k++)
            {
                // Slack: sagging between the ship's end and the masthead.
                var a = ends[k];
                var middle = (a + head) * 0.5f + Vector3.down * (0.12f * Vector3.Distance(a, head) * u);
                Span(mastRopes[2 * k], a, middle);
                Span(mastRopes[2 * k + 1], middle, head);
            }
        }

        /// <summary>A rope of unit length along z laid between two points of the model.</summary>
        static void Span(Transform rope, Vector3 a, Vector3 b)
        {
            var along = b - a;
            rope.localPosition = a;
            rope.localRotation = along.sqrMagnitude > 1e-8f ? Quaternion.LookRotation(along, Mathf.Abs(along.normalized.y) > 0.99f ? Vector3.forward : Vector3.up) : Quaternion.identity;
            rope.localScale = new Vector3(1f, 1f, Mathf.Max(0.01f, along.magnitude));
        }

        static GreekMesh RopeUnit()
        {
            var b = new GreekShips.MeshBuilder(null);
            b.Tube(GreekMaterial.Rope, new List<Vector3> { Vector3.zero, Vector3.forward }, 0.035f, 0.035f, 4);
            return b.Build(GreekMaterial.Rope);
        }

        /// <summary>The torn fibres of a snapped pole: thin slivers round its rim, standing out along the way given.</summary>
        static void Splinters(GreekShips.MeshBuilder b, Vector3 centre, float radius, Vector3 along, uint hash)
        {
            const int count = 7;
            for (var k = 0; k < count; k++)
            {
                var angle = k * Mathf.PI * 2f / count + DestructionHash.Range(hash, 10 + k, -0.3f, 0.3f);
                var rim = centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius * 0.75f;
                var tip = rim + along * DestructionHash.Range(hash, 20 + k, 0.12f, 0.4f) + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.04f;
                b.Tube(GreekMaterial.Wood, new List<Vector3> { rim, tip }, radius * 0.22f, 0.004f, 3);
            }
        }

        void OnMastDestroy()
        {
            foreach (var mesh in mastMeshes) DiscardMesh(mesh);
            mastMeshes.Clear();
        }
    }
}
