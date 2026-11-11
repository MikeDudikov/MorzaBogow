using System;
using System.Collections.Generic;
using UnityEngine;

namespace MorzaBogow.Presentation
{
    /// <summary>
    /// The port's works by the water and a bridgehead's camp (docs/art/DESTRUCTION.md, docs/art/HistoricalConstraints.md):
    /// <list type="bullet">
    /// <item>the dock as a ship shed of the kind found at Zea (R-ZEA: the type, not its size): a ramp of stone slabs with two
    /// timber ways running down into the sea between two colonnades of limestone columns, a low tiled roof over them, a
    /// wall closing the landward end. It stands beside the war line's slipway, never over it, and beside the jetty, off the
    /// porters' path. Zea's sheds were 5.81 m wide inside, narrower than the game's ships (up to 10.9 m), so no ship lies in
    /// it: ships are mended at the berth, under the shear legs;</item>
    /// <item>the shear legs on the jetty's end, leaning over that berth, with their fall and windlass;</item>
    /// <item>a bridgehead's field camp: a palisade of stakes round its plot with a gate toward the harbour, two ridge tents
    /// of linen, a boat drawn up and laid keel up on trestles, a hearth, crates, amphorae and sacks.</item>
    /// </list>
    /// Each rises by assembly from the project's progress (no scaling) and breaks element by element with the
    /// settlement's hull (PortBuildings.Breakables).
    /// </summary>
    public sealed partial class PortBuildings
    {
        /// <summary>
        /// The ship shed's place: from u ShedFrom (ashore, clear of a hull on the war line's slipway with its scaffolding)
        /// down to ShedTo (its ramp under water), its middle line at z ShedZ, ShedWidth across over its colonnades.
        /// </summary>
        public const float ShedFrom = 12f, ShedTo = 24.4f, ShedZ = -6f, ShedWidth = 6f;

        /// <summary>Where the roof ends seaward, the eaves' height over the sea, the roof's pitch and overhang, the columns.</summary>
        const float ShedRoofTo = 22.4f, ShedEave = 4f, ShedPitch = 18f, ShedOverhang = 0.3f, ShedBeam = 0.45f;
        const float ShedColumnX = 2.75f, ShedColumnRadius = 0.24f, ShedRampDepth = -1.1f;
        const int ShedColumns = 5;

        /// <summary>
        /// Land that buildings may take and nothing else may (ShipyardProps keeps off it): the ship shed's place and the
        /// way to it, the town wall's front with its gate towers, the porters' pile by the store door. In u toward the port
        /// and z along the shore.
        /// </summary>
        public static bool Reserved(float u, float z) =>
            u >= ShedFrom - 1.5f && u <= ShedTo && z <= ShedZ + ShedWidth * 0.5f + 1f && z >= ShedZ - ShedWidth * 0.5f - 1.5f
            || u >= -0.6f && u <= 2.4f && Mathf.Abs(z) <= PlotHalfWidth + 1.2f
            || Mathf.Abs(u - 12.5f) <= 1.6f && Mathf.Abs(z - 3f) <= 1.6f;

        // ---------- The ship shed ----------

        static float ShedLength => ShedTo - ShedFrom;
        static float ShedRoofLength => ShedRoofTo - ShedFrom;
        /// <summary>The roof's middle along the shed's frame (whose origin is the middle of the whole ramp).</summary>
        static float ShedRoofMiddle => -ShedLength * 0.5f + ShedRoofLength * 0.5f;
        static float ShedRise => ShedWidth * 0.5f * Mathf.Tan(ShedPitch * Mathf.Deg2Rad);

        /// <summary>The ground under a point of the shed's frame: its z runs toward the port, its x along the shore.</summary>
        float ShedGround(float x, float z) => Ground((ShedFrom + ShedTo) * 0.5f + z, ShedZ - toward * x);

        /// <summary>The ramp's top at a point along it: from the ground at its head down into the sea.</summary>
        float ShedRampTop(float z)
        {
            var land = ShedGround(0f, -ShedLength * 0.5f);
            return Mathf.Lerp(land, ShedRampDepth, Mathf.Clamp01((z + ShedLength * 0.5f) / ShedLength)) + 0.14f;
        }

        /// <summary>A column's axis: two rows over the ramp's sides, from near the back wall to under the roof's seaward end.</summary>
        static (float x, float z) ShedColumnAt(float side, int k) =>
            (side * ShedColumnX, -ShedLength * 0.5f + 1f + k * (ShedRoofLength - 2f) / (ShedColumns - 1));

        /// <summary>The top of a column's footing: just over the ground ashore, just over the water where it stands in the sea.</summary>
        float ShedFooting(float x, float z) => Mathf.Max(ShedGround(x, z) + 0.15f, 0.3f);

        /// <summary>
        /// The dock as a ship shed after those at Zea: the ramp is laid first, then the columns go up a pair at a time from
        /// the land toward the sea, the back wall, the beams; the roof from 80%. One mesh once it stands.
        /// </summary>
        void AddShipShed(Structure item)
        {
            var piece = NewPiece(item, "Ship shed", (ShedFrom + ShedTo) * 0.5f, ShedZ, ShedLength, ShedWidth, ShedEave + ShedRise + 0.4f);
            // At the sea's level, its z toward the port: the ramp runs down along it into the water.
            piece.Root.localPosition = Local(piece.U, piece.Z, 0f);
            piece.Root.localRotation = Quaternion.LookRotation(Local(1f, 0f, 0f));
            var body = Builder();
            ShedRamp(body, false);
            piece.Body = Attach(piece.Root, "Ramp", body.Build(GreekMaterial.Stone, GreekMaterial.Wood));

            var all = Builder();
            for (var k = 0; k < ShedColumns; k++)
            {
                var c = Builder();
                foreach (var target in new[] { c, all })
                    foreach (var side in new[] { -1f, 1f })
                    {
                        var (x, z) = ShedColumnAt(side, k);
                        ShedFootingBlock(target, x, z);
                        ShedShaft(target, x, z);
                    }
                piece.Courses.Add(Attach(piece.Body, $"Columns {k}", c.Build(GreekMaterial.Stone)));
            }
            var wall = Builder();
            foreach (var target in new[] { wall, all }) ShedBackWall(target, false);
            piece.Courses.Add(Attach(piece.Body, "Back wall", wall.Build(GreekMaterial.Stone)));
            var beams = Builder();
            foreach (var target in new[] { beams, all })
            {
                foreach (var side in new[] { -1f, 1f }) ShedArchitrave(target, side, -ShedLength * 0.5f, -ShedLength * 0.5f + ShedRoofLength);
                for (var k = 0; k < ShedColumns; k++) ShedTie(target, k);
            }
            piece.Courses.Add(Attach(piece.Body, "Beams", beams.Build(GreekMaterial.Stone, GreekMaterial.Wood)));
            piece.Whole = Attach(piece.Body, "Colonnades", all.Build(GreekMaterial.Stone, GreekMaterial.Wood));

            var roof = Builder();
            var tiles = Builder();
            TiledGable(tiles, ShedWidth, ShedRoofLength, ShedEave, GreekMaterial.Stone, ShedOverhang, ShedPitch);
            var made = tiles.Build(GreekMaterial.Terracotta, GreekMaterial.Stone, GreekMaterial.Wood);
            roof.Append(made, new Vector3(0f, 0f, ShedRoofMiddle), Quaternion.identity);
            Discard(made.Mesh);
            piece.Roof = Attach(piece.Root, "Roof", roof.Build(GreekMaterial.Terracotta, GreekMaterial.Stone, GreekMaterial.Wood));
            ShedScaffold(piece);
            AddBreakable(item, piece, "Dock.Shed/", BuildShipShedModel, ShedRest, FootprintDistance(piece));
        }

        /// <summary>The ramp of stone slabs, deep enough to meet the slope under it, and its two timber ways; the footings too if asked.</summary>
        void ShedRamp(GreekShips.MeshBuilder b, bool footings)
        {
            var land = ShedGround(0f, -ShedLength * 0.5f);
            var drop = land - ShedRampDepth;
            var turn = Quaternion.Euler(Mathf.Atan2(drop, ShedLength) * Mathf.Rad2Deg, 0f, 0f);
            var middle = new Vector3(0f, (land + ShedRampDepth) * 0.5f, 0f);
            var run = Mathf.Sqrt(ShedLength * ShedLength + drop * drop) * 0.5f;
            b.Box(GreekMaterial.Stone, middle + turn * new Vector3(0f, -0.6f, 0f), new Vector3(ShedColumnX - 0.15f, 0.6f, run), turn);
            foreach (var x in new[] { -0.8f, 0.8f })
                b.Box(GreekMaterial.Wood, middle + turn * new Vector3(x, 0.07f, 0f), new Vector3(0.12f, 0.07f, run), turn);
            if (!footings) return;
            for (var k = 0; k < ShedColumns; k++)
                foreach (var side in new[] { -1f, 1f })
                {
                    var (x, z) = ShedColumnAt(side, k);
                    ShedFootingBlock(b, x, z);
                }
        }

        /// <summary>A column's footing: a squared block from the bed up to just over the ground or the water.</summary>
        void ShedFootingBlock(GreekShips.MeshBuilder b, float x, float z)
        {
            var top = ShedFooting(x, z);
            b.Box(GreekMaterial.Stone, new Vector3(x, top - 1.5f, z), new Vector3(0.42f, 1.5f, 0.42f), Quaternion.identity);
        }

        /// <summary>A plain limestone shaft, a little tapering, with a square cap under the beam it carries.</summary>
        void ShedShaft(GreekShips.MeshBuilder b, float x, float z)
        {
            var foot = ShedFooting(x, z);
            var top = ShedEave - ShedBeam;
            const float r = ShedColumnRadius;
            b.Lathe(GreekMaterial.Stone, new[] { (0f, r), (top - foot - 0.16f, r * 0.86f), (top - foot - 0.14f, r * 1.1f) }, 12, new Vector3(x, foot, z));
            b.Box(GreekMaterial.Stone, new Vector3(x, top - 0.07f, z), new Vector3(0.33f, 0.07f, 0.33f), Quaternion.identity);
        }

        /// <summary>The landward end wall up to the eaves, and over it the gable in the wall's stone if asked (the roof brings its own).</summary>
        void ShedBackWall(GreekShips.MeshBuilder b, bool gable)
        {
            var z = -ShedLength * 0.5f + 0.25f;
            var ground = Mathf.Min(ShedGround(-ShedWidth * 0.5f, z), ShedGround(ShedWidth * 0.5f, z));
            b.Box(GreekMaterial.Stone, new Vector3(0f, (ShedEave + ground - Sink) * 0.5f, z), new Vector3(ShedWidth * 0.5f, (ShedEave - ground + Sink) * 0.5f, 0.25f), Quaternion.identity);
            if (gable) Gable3(b, GreekMaterial.Stone, ShedWidth, ShedEave, ShedRise, z - 0.25f, z + 0.25f);
        }

        /// <summary>A colonnade's beam from z0 to z1 on its columns' caps, under the eaves.</summary>
        static void ShedArchitrave(GreekShips.MeshBuilder b, float side, float z0, float z1) =>
            b.Box(GreekMaterial.Stone, new Vector3(side * ShedColumnX, ShedEave - ShedBeam * 0.5f, (z0 + z1) * 0.5f), new Vector3(0.3f, ShedBeam * 0.5f, (z1 - z0) * 0.5f), Quaternion.identity);

        /// <summary>A timber tie across the shed between a pair of columns, under the roof.</summary>
        static void ShedTie(GreekShips.MeshBuilder b, int k) =>
            b.Box(GreekMaterial.Wood, new Vector3(0f, ShedEave - 0.12f, ShedColumnAt(1f, k).z), new Vector3(ShedColumnX, 0.11f, 0.11f), Quaternion.identity);

        /// <summary>Poles outside the colonnades with two ledgers each side, and stakes and cords marking out the shed on the shore.</summary>
        void ShedScaffold(Piece piece)
        {
            var poles = Builder();
            var from = ShedColumnAt(1f, 0).z;
            var to = ShedColumnAt(1f, ShedColumns - 1).z;
            for (var k = 0; k < ShedColumns; k++)
                foreach (var side in new[] { -1f, 1f })
                {
                    var (x, z) = ShedColumnAt(side, k);
                    var px = x + side * 0.65f;
                    var foot = ShedFooting(px, z) - 0.3f;
                    var tall = ShedEave + 1.2f - foot;
                    poles.Box(GreekMaterial.Wood, new Vector3(px, foot + tall * 0.5f, z), new Vector3(0.08f, tall * 0.5f, 0.08f), Quaternion.identity);
                }
            foreach (var side in new[] { -1f, 1f })
                foreach (var level in new[] { 2.2f, ShedEave + 0.3f })
                    poles.Box(GreekMaterial.Wood, new Vector3(side * (ShedColumnX + 0.65f), level, (from + to) * 0.5f), new Vector3(0.05f, 0.06f, (to - from) * 0.5f + 0.2f), Quaternion.identity);
            piece.Scaffold = Attach(piece.Root, "Scaffolding", poles.Build(GreekMaterial.Wood));

            var footing = Builder();
            for (var k = 0; k < ShedColumns; k++)
                foreach (var side in new[] { -1f, 1f })
                {
                    var (x, z) = ShedColumnAt(side, k);
                    var ground = ShedFooting(x, z) - 0.15f;
                    footing.Box(GreekMaterial.Wood, new Vector3(x, ground + 0.3f, z), new Vector3(0.05f, 0.35f, 0.05f), Quaternion.identity);
                }
            foreach (var side in new[] { -1f, 1f })
            {
                var y = ShedFooting(side * ShedColumnX, from) + 0.45f;
                footing.Box(GreekMaterial.Rope, new Vector3(side * ShedColumnX, y, (from + to) * 0.5f), new Vector3(0.015f, 0.015f, (to - from) * 0.5f), Quaternion.identity);
            }
            piece.Footing = Attach(piece.Root, "Footing", footing.Build(GreekMaterial.Wood, GreekMaterial.Rope));
        }

        /// <summary>Where a fallen piece of the shed comes to rest: on the ramp between the colonnades, on the shore or the sea bed outside.</summary>
        float ShedRest(Vector3 point)
        {
            if (Mathf.Abs(point.x) <= ShedColumnX - 0.15f && Mathf.Abs(point.z) <= ShedLength * 0.5f) return ShedRampTop(point.z);
            return ShedGround(point.x, point.z);
        }

        /// <summary>
        /// The ship shed of AddShipShed taken apart: the ramp with the columns' footings stays (the foundation); each column
        /// stands on its footing and topples outward; the back wall slumps into its stone; the beams lie span by span on
        /// the columns (the first on the back wall); the ties across between a pair of columns; the roof in bays a side on
        /// the beams under its eave, those at the landward end on the back wall too.
        /// </summary>
        (StructureGraph, ElementShape[]) BuildShipShedModel()
        {
            var graph = new StructureGraph();
            var shapes = new List<ElementShape>();
            int Add(string id, ElementRole role, BuildMaterial material, DetachMethod detach, GreekMesh mesh, Vector3 outward, string joint, ElementShape shape, params int[] supports) =>
                AddElement(graph, shapes, id, role, material, detach, mesh, outward, joint, shape, supports);

            var ramp = Builder();
            ShedRamp(ramp, true);
            var ground = Add("ramp", ElementRole.Foundation, BuildMaterial.Stone, DetachMethod.Fixed, ramp.Build(GreekMaterial.Stone, GreekMaterial.Wood), Vector3.zero,
                "stone slabs and timber ways down into the sea, the footings in the bed", new ElementShape());

            var columns = new int[2, ShedColumns];
            var number = 0;
            foreach (var side in new[] { 1f, -1f })
                for (var k = 0; k < ShedColumns; k++)
                {
                    var (x, z) = ShedColumnAt(side, k);
                    var foot = ShedFooting(x, z);
                    var height = ShedEave - ShedBeam - foot;
                    var b = Builder();
                    ShedShaft(b, x, z);
                    var shape = new ElementShape
                    {
                        Centre = new Vector3(x, foot + height * 0.5f, z), Size = new Vector3(2f * ShedColumnRadius, height, 2f * ShedColumnRadius),
                        Radius = ShedColumnRadius, Base = new Vector3(x, foot, z),
                    };
                    var made = Add($"col.{(side > 0f ? "a" : "b")}.{k}", ElementRole.LoadBearing, BuildMaterial.Stone, DetachMethod.Topple, b.Build(GreekMaterial.Stone),
                        new Vector3(side, 0f, 0f), "a single limestone shaft on its footing", shape, ground);
                    graph[made].Column = 200 + number++;
                    graph[made].Primary = true;
                    columns[side > 0f ? 0 : 1, k] = made;
                }

            // The back wall with its gable, dry ashlar: it slumps into rubble and blocks.
            var wallZ = -ShedLength * 0.5f + 0.25f;
            var wallGround = Mathf.Min(ShedGround(-ShedWidth * 0.5f, wallZ), ShedGround(ShedWidth * 0.5f, wallZ));
            var wall = Builder();
            ShedBackWall(wall, true);
            var wallHash = DestructionHash.Mix((uint)seed, 900);
            var wallFoot = new Vector3(0f, wallGround, wallZ);
            var wallShape = new ElementShape { Centre = new Vector3(0f, (ShedEave + wallGround) * 0.5f, wallZ), Size = new Vector3(ShedWidth, ShedEave - wallGround, 0.5f), Base = wallFoot };
            var stump = Builder();
            JaggedWall(stump, wallFoot, false, ShedWidth, 0.5f, DestructionHash.Range(wallHash, 1, 0.5f, 1.4f), wallHash, GreekMaterial.Stone, GreekMaterial.Stone);
            wallShape.Stump = stump.Build(GreekMaterial.Stone);
            wallShape.Heap = StoneHeaps(wallFoot, false, ShedWidth, 0.5f, ShedRest, wallHash);
            var backWall = Add("wall.back", ElementRole.Wall, BuildMaterial.Stone, DetachMethod.Crumble, wall.Build(GreekMaterial.Stone), Vector3.back,
                "dry ashlar across the shed's head", wallShape, ground);
            graph[backWall].Primary = true;

            // The beams span by span: from the back wall to the first column, between columns, and the last over-sailing the sea.
            var spans = new List<(int index, float side, float z0, float z1)>();
            var roofFrom = -ShedLength * 0.5f;
            var roofTo = roofFrom + ShedRoofLength;
            foreach (var side in new[] { 1f, -1f })
            {
                var row = side > 0f ? 0 : 1;
                var name = side > 0f ? "a" : "b";
                for (var j = -1; j < ShedColumns; j++)
                {
                    var z0 = j < 0 ? roofFrom : ShedColumnAt(side, j).z;
                    var z1 = j + 1 < ShedColumns ? ShedColumnAt(side, j + 1).z : roofTo;
                    var b = Builder();
                    ShedArchitrave(b, side, z0, z1);
                    var holds = j < 0 ? new[] { backWall, columns[row, 0] } : j + 1 < ShedColumns ? new[] { columns[row, j], columns[row, j + 1] } : new[] { columns[row, j] };
                    var made = Add($"beam.{name}.{j + 1}", ElementRole.Beam, BuildMaterial.Stone, DetachMethod.Drop, b.Build(GreekMaterial.Stone), new Vector3(side, 0f, 0f),
                        j < 0 ? "set into the back wall and on the first cap" : "on the caps", new ElementShape { Radius = 0.3f }, holds);
                    spans.Add((made, side, z0, z1));
                }
            }
            for (var k = 0; k < ShedColumns; k++)
            {
                var b = Builder();
                ShedTie(b, k);
                Add($"tie.{k}", ElementRole.Decor, BuildMaterial.Timber, DetachMethod.Drop, b.Build(GreekMaterial.Wood), Vector3.up, "pinned across the pair of caps",
                    new ElementShape { Radius = 0.11f }, columns[0, k], columns[1, k]);
            }

            // The roof in bays a side, on the beams under its eave; those at the head on the back wall as well.
            const int bays = 4;
            var slope = ShedPitch * Mathf.Deg2Rad;
            var run = ShedWidth * 0.5f + ShedOverhang;
            var thick = 0.1f / Mathf.Cos(slope);
            float TopAt(float x) => ShedEave + ShedRise - Mathf.Abs(x) * Mathf.Tan(slope) + 0.12f + thick;
            var reach = ShedRoofLength * 0.5f + ShedOverhang;
            foreach (var side in new[] { 1f, -1f })
                for (var k = 0; k < bays; k++)
                {
                    // In the roof's own frame (TiledGable's), then moved to where the roof lies on the shed.
                    var z0 = -reach + k * 2f * reach / bays;
                    var z1 = z0 + 2f * reach / bays;
                    var zc = (z0 + z1) * 0.5f + ShedRoofMiddle;
                    var bay = Builder();
                    RoofBay(bay, ShedWidth, ShedRoofLength, ShedEave, ShedOverhang, ShedPitch, side, z0, z1, side > 0f, k == bays - 1);
                    var bayMesh = bay.Build(GreekMaterial.Terracotta, GreekMaterial.Wood);
                    var placed = Builder();
                    placed.Append(bayMesh, new Vector3(0f, 0f, ShedRoofMiddle), Quaternion.identity);
                    Discard(bayMesh.Mesh);
                    var hash = DestructionHash.Mix((uint)seed, 910 + k + (side > 0f ? 0 : 10));
                    var shape = new ElementShape
                    {
                        EaveEnd = new Vector3(side * run, TopAt(run), zc), RidgeEnd = new Vector3(0f, TopAt(0f), zc),
                        EaveHinge = new Vector3(side * ShedWidth * 0.5f, ShedEave, zc), InnerHinge = new Vector3(0f, ShedEave + ShedRise, zc),
                    };
                    var inside = Builder();
                    var inX = side * ShedWidth * 0.18f;
                    TileHeap(inside, new Vector3(inX, ShedRest(new Vector3(inX, 0f, zc)), zc), ShedWidth * 0.18f, reach * 0.36f, 0.3f, new Vector3(side * ShedColumnX, ShedEave * 0.6f, zc), hash);
                    shape.Heap = inside.Build(GreekMaterial.Terracotta, GreekMaterial.Wood);
                    var outside = Builder();
                    var outX = side * (ShedWidth * 0.5f + 0.9f);
                    TileHeap(outside, new Vector3(outX, ShedRest(new Vector3(outX, 0f, zc)), zc), 0.6f, reach * 0.36f, 0.3f, new Vector3(side * ShedColumnX, 0.6f, zc), hash + 1u);
                    shape.HeapOut = outside.Build(GreekMaterial.Terracotta, GreekMaterial.Wood);
                    var supports = new List<int>();
                    var bayFrom = z0 + ShedRoofMiddle;
                    var bayTo = z1 + ShedRoofMiddle;
                    foreach (var (index, beamSide, from, to) in spans)
                        if (beamSide == side && Mathf.Min(to, bayTo) - Mathf.Max(from, bayFrom) > 0.1f) supports.Add(index);
                    if (k == 0) supports.Add(backWall);
                    var made = Add($"roof.{(side > 0f ? "a" : "b")}.{k}", ElementRole.Roof, BuildMaterial.Ceramic, DetachMethod.CaveIn,
                        placed.Build(GreekMaterial.Terracotta, GreekMaterial.Wood), new Vector3(side, 0f, 0f), "pan and cover tiles on rafters over the beams", shape, supports.ToArray());
                    graph[made].Primary = true;
                }
            return (graph, shapes.ToArray());
        }

        // ---------- The shear legs ----------

        static readonly Vector3 ShearApex = new Vector3(24.2f, 9.4f, -1.8f);
        static readonly float[] ShearFeet = { 20.4f, 22.4f };

        /// <summary>The shear legs on the jetty's end: the windlass and bollard first, then the two legs, then the fall.</summary>
        void AddShearLegs(Structure item)
        {
            var piece = new Piece { U = 20.7f, Z = -1.8f, W = 8.4f, D = 1.6f, H = 9.5f };
            piece.Root = new GameObject("Shear legs").transform;
            piece.Root.SetParent(item.Root.transform, false);
            item.Pieces.Add(piece);
            var foot = Builder();
            ShearFoot(foot);
            piece.Body = Attach(piece.Root, "Windlass", foot.Build(GreekMaterial.Wood, GreekMaterial.Rope));
            var all = Builder();
            var legs = Builder();
            foreach (var target in new[] { legs, all })
                for (var i = 0; i < ShearFeet.Length; i++) ShearLeg(target, i);
            piece.Courses.Add(Attach(piece.Body, "Legs", legs.Build(GreekMaterial.Wood)));
            var fall = Builder();
            foreach (var target in new[] { fall, all }) ShearFall(target);
            piece.Courses.Add(Attach(piece.Body, "Fall", fall.Build(GreekMaterial.Rope, GreekMaterial.Wood)));
            piece.Whole = Attach(piece.Body, "Standing", all.Build(GreekMaterial.Wood, GreekMaterial.Rope));
            AddBreakable(item, piece, "Dock.Legs/", BuildShearLegsModel, ShearRest, FootprintDistance(piece));
        }

        Vector3 ShearPoint(Vector3 at) => Local(at.x, at.z, at.y);

        /// <summary>The foot beam on the jetty, the bollard for the backstay and the windlass beside it.</summary>
        void ShearFoot(GreekShips.MeshBuilder b)
        {
            b.Box(GreekMaterial.Wood, Local(21.4f, -1.75f, JettyTop + 0.1f), new Vector3(1.4f, 0.1f, 0.18f), Quaternion.identity);
            var bollard = Local(17.2f, -1.75f, JettyTop);
            b.Box(GreekMaterial.Wood, bollard + Vector3.up * 0.3f, new Vector3(0.2f, 0.35f, 0.2f), Quaternion.identity);
            var drum = Local(18.6f, -1.75f, JettyTop + 0.55f);
            b.Tube(GreekMaterial.Wood, new List<Vector3> { drum + Vector3.forward * 0.55f, drum - Vector3.forward * 0.55f }, 0.22f, 0.22f, 8);
            foreach (var end in new[] { -0.6f, 0.6f })
                b.Box(GreekMaterial.Wood, drum + new Vector3(0f, -0.3f, end), new Vector3(0.08f, 0.32f, 0.06f), Quaternion.identity);
            b.Tube(GreekMaterial.Rope, new List<Vector3> { drum + new Vector3(0f, 0.22f, 0f), drum + new Vector3(0f, 0.22f, 0.3f) }, 0.24f, 0.24f, 8);
        }

        void ShearLeg(GreekShips.MeshBuilder b, int i) =>
            b.Tube(GreekMaterial.Wood, new List<Vector3> { Local(ShearFeet[i], -1.75f, JettyTop), ShearPoint(ShearApex) }, 0.2f, 0.15f, 6);

        /// <summary>The backstay to the bollard, the fall from the apex and its block.</summary>
        void ShearFall(GreekShips.MeshBuilder b)
        {
            var apex = ShearPoint(ShearApex);
            var bollard = Local(17.2f, -1.75f, JettyTop);
            b.Tube(GreekMaterial.Rope, new List<Vector3> { apex, bollard + Vector3.up * 0.5f }, 0.045f, 0.045f, 4);
            b.Tube(GreekMaterial.Rope, new List<Vector3> { apex, apex + Vector3.down * 4.6f }, 0.04f, 0.04f, 4);
            b.Box(GreekMaterial.Wood, apex + Vector3.down * 4.8f, new Vector3(0.18f, 0.25f, 0.14f), Quaternion.identity);
        }

        /// <summary>Where a fallen leg or the fall comes to rest: on the jetty's deck, else on the sea bed beside it.</summary>
        float ShearRest(Vector3 point)
        {
            var u = point.x * toward;
            if (u >= 14f && u <= 23.5f && Mathf.Abs(point.z) <= 2f) return JettyTop;
            return Ground(u, point.z);
        }

        /// <summary>The shear legs taken apart: the windlass and bollard on the deck stay; the two legs go over; the fall drops with them.</summary>
        (StructureGraph, ElementShape[]) BuildShearLegsModel()
        {
            var graph = new StructureGraph();
            var shapes = new List<ElementShape>();
            int Add(string id, ElementRole role, BuildMaterial material, DetachMethod detach, GreekMesh mesh, Vector3 outward, string joint, ElementShape shape, params int[] supports) =>
                AddElement(graph, shapes, id, role, material, detach, mesh, outward, joint, shape, supports);
            var foot = Builder();
            ShearFoot(foot);
            var ground = Add("foot", ElementRole.Foundation, BuildMaterial.Timber, DetachMethod.Fixed, foot.Build(GreekMaterial.Wood, GreekMaterial.Rope), Vector3.zero,
                "spiked to the jetty's deck", new ElementShape());
            var legs = new int[ShearFeet.Length];
            for (var i = 0; i < ShearFeet.Length; i++)
            {
                var b = Builder();
                ShearLeg(b, i);
                var at = Local(ShearFeet[i], -1.75f, JettyTop);
                legs[i] = Add($"leg.{i}", ElementRole.LoadBearing, BuildMaterial.Timber, DetachMethod.Topple, b.Build(GreekMaterial.Wood), new Vector3(0f, 0f, -1f),
                    "stepped in the foot beam, lashed at the head", new ElementShape { Radius = 0.2f, Base = at }, ground);
                graph[legs[i]].Column = 300 + i;
                graph[legs[i]].Primary = true;
            }
            var fall = Builder();
            ShearFall(fall);
            Add("fall", ElementRole.Decor, BuildMaterial.Timber, DetachMethod.Drop, fall.Build(GreekMaterial.Rope, GreekMaterial.Wood), new Vector3(0f, 0f, -1f),
                "rove through the block at the head", new ElementShape { Radius = 0.18f }, legs);
            return (graph, shapes.ToArray());
        }

        // ---------- A bridgehead's camp ----------

        /// <summary>The camp's palisade round its plot: from u CampBack to CampFront, z within CampSide, the gate within CampGate.</summary>
        const float CampBack = -11.2f, CampFront = -0.6f, CampSide = 6.8f, CampGate = 1.6f;

        /// <summary>A bridgehead's field camp: the palisade, two tents, and the gear of a camp by the shore.</summary>
        void AddCamp(Structure item)
        {
            AddPalisade(item);
            AddTent(item, "Tent", -8f, 3.6f);
            AddTent(item, "Tent", -8f, -3.6f);
            AddCampGear(item);
        }

        /// <summary>The palisade's stretches, each up to 2.4 m: (name, from, to) in (u, z).</summary>
        static IEnumerable<(string name, float u0, float z0, float u1, float z1)> PalisadeLines()
        {
            yield return ("back", CampBack, -CampSide, CampBack, CampSide);
            yield return ("south", CampBack, -CampSide, CampFront, -CampSide);
            yield return ("north", CampBack, CampSide, CampFront, CampSide);
            yield return ("gate.south", CampFront, -CampSide, CampFront, -CampGate);
            yield return ("gate.north", CampFront, CampGate, CampFront, CampSide);
        }

        /// <summary>One stretch of stakes, pointed and set side by side in the ground, a rail along their inside.</summary>
        void PalisadeStretch(GreekShips.MeshBuilder b, float u0, float z0, float u1, float z1, uint hash)
        {
            var length = Mathf.Sqrt((u1 - u0) * (u1 - u0) + (z1 - z0) * (z1 - z0));
            var count = Mathf.Max(2, Mathf.RoundToInt(length / 0.24f));
            for (var i = 0; i < count; i++)
            {
                var t = (i + 0.5f) / count;
                var u = Mathf.Lerp(u0, u1, t);
                var z = Mathf.Lerp(z0, z1, t);
                var tall = 2f + DestructionHash.Range(hash, 60 + i, 0f, 0.35f);
                const float r = 0.09f;
                b.Lathe(GreekMaterial.Wood, new[] { (0f, r), (tall, r * 0.9f), (tall + 0.28f, 0f) }, 5, Local(u, z, Ground(u, z) - 0.4f));
            }
            var mid = Local((u0 + u1) * 0.5f, (z0 + z1) * 0.5f, 0f);
            var along = Local(u1 - u0, z1 - z0, 0f).normalized;
            var inward = -Vector3.Cross(Vector3.up, along).normalized;
            if (Vector3.Dot(inward, Local((CampBack + CampFront) * 0.5f, 0f, 0f) - mid) < 0f) inward = -inward;
            var rail = mid + inward * 0.14f;
            rail.y = Ground((u0 + u1) * 0.5f, (z0 + z1) * 0.5f) + 1.35f;
            b.Box(GreekMaterial.Wood, rail, new Vector3(0.05f, 0.05f, length * 0.5f), Quaternion.LookRotation(along));
        }

        /// <summary>The palisade, standing as one mesh in the settlement's own frame; it breaks stretch by stretch.</summary>
        void AddPalisade(Structure item)
        {
            var piece = new Piece { U = (CampBack + CampFront) * 0.5f, Z = 0f, W = CampFront - CampBack + 0.4f, D = 2f * CampSide + 0.4f, H = 2.7f };
            piece.Root = new GameObject("Palisade").transform;
            piece.Root.SetParent(item.Root.transform, false);
            item.Pieces.Add(piece);
            var b = Builder();
            var line = 0;
            foreach (var (name, u0, z0, u1, z1) in PalisadeLines())
            {
                var length = Mathf.Sqrt((u1 - u0) * (u1 - u0) + (z1 - z0) * (z1 - z0));
                var stretches = Mathf.Max(1, Mathf.CeilToInt(length / 2.4f));
                for (var i = 0; i < stretches; i++)
                    PalisadeStretch(b, Mathf.Lerp(u0, u1, i / (float)stretches), Mathf.Lerp(z0, z1, i / (float)stretches),
                        Mathf.Lerp(u0, u1, (i + 1) / (float)stretches), Mathf.Lerp(z0, z1, (i + 1) / (float)stretches), DestructionHash.Mix((uint)seed, 1000 + line * 50 + i));
                line++;
            }
            piece.Body = Attach(piece.Root, "Stakes", b.Build(GreekMaterial.Wood));
            AddBreakable(item, piece, "Store.Palisade/", BuildPalisadeModel, point => Ground(point.x * toward, point.z), FootprintDistance(piece));
        }

        /// <summary>The palisade taken apart: the ground it stands in, and each stretch of stakes, which goes over outward as one.</summary>
        (StructureGraph, ElementShape[]) BuildPalisadeModel()
        {
            var graph = new StructureGraph();
            var shapes = new List<ElementShape>();
            int Add(string id, ElementRole role, BuildMaterial material, DetachMethod detach, GreekMesh mesh, Vector3 outward, string joint, ElementShape shape, params int[] supports) =>
                AddElement(graph, shapes, id, role, material, detach, mesh, outward, joint, shape, supports);
            var middle = Local((CampBack + CampFront) * 0.5f, 0f, 0f);
            var ground = Add("ground", ElementRole.Foundation, BuildMaterial.Timber, DetachMethod.Fixed, null, Vector3.zero, "the trench the stakes stand in",
                new ElementShape { Centre = middle, Size = new Vector3(CampFront - CampBack, 1f, 2f * CampSide) });
            var line = 0;
            var number = 0;
            foreach (var (name, u0, z0, u1, z1) in PalisadeLines())
            {
                var length = Mathf.Sqrt((u1 - u0) * (u1 - u0) + (z1 - z0) * (z1 - z0));
                var stretches = Mathf.Max(1, Mathf.CeilToInt(length / 2.4f));
                for (var i = 0; i < stretches; i++)
                {
                    var a = (u: Mathf.Lerp(u0, u1, i / (float)stretches), z: Mathf.Lerp(z0, z1, i / (float)stretches));
                    var c = (u: Mathf.Lerp(u0, u1, (i + 1) / (float)stretches), z: Mathf.Lerp(z0, z1, (i + 1) / (float)stretches));
                    var b = Builder();
                    PalisadeStretch(b, a.u, a.z, c.u, c.z, DestructionHash.Mix((uint)seed, 1000 + line * 50 + i));
                    var mu = (a.u + c.u) * 0.5f;
                    var mz = (a.z + c.z) * 0.5f;
                    var foot = Local(mu, mz, Ground(mu, mz));
                    var outward = foot - middle;
                    outward.y = 0f;
                    outward = outward.sqrMagnitude > 1e-4f ? outward.normalized : Vector3.right;
                    var made = Add($"stakes.{name}.{i}", ElementRole.Wall, BuildMaterial.Timber, DetachMethod.Topple, b.Build(GreekMaterial.Wood), outward,
                        "stakes set in a trench, a rail pegged along them", new ElementShape { Radius = 0.12f, Base = foot }, ground);
                    graph[made].Column = 400 + number++;
                    graph[made].Primary = true;
                }
                line++;
            }
            return (graph, shapes.ToArray());
        }

        const float TentLength = 3f, TentWidth = 2.4f, TentHeight = 1.9f;

        /// <summary>A ridge tent of linen along the shore, its two poles and ridge pole, guy ropes to pegs; its open end toward +z.</summary>
        void AddTent(Structure item, string name, float u, float z)
        {
            var piece = NewPiece(item, name, u, z, TentWidth + 1.2f, TentLength + 0.8f, TentHeight + 0.3f);
            // On a slope the tent stands on a levelled platform of dry stone, its floor at the highest ground under it, so the
            // hill never comes up through the cloth (TentPegs lays the platform).
            var top = Ground(u, z);
            for (var corner = 0; corner < 4; corner++)
                top = Mathf.Max(top, Ground(u + ((corner & 1) == 0 ? -0.5f : 0.5f) * piece.W, z + ((corner & 2) == 0 ? -0.5f : 0.5f) * piece.D));
            piece.Root.localPosition = Local(u, z, top);
            var b = Builder();
            TentPegs(b);
            foreach (var end in new[] { -1f, 1f }) TentPole(b, end);
            TentRidge(b);
            foreach (var side in new[] { -1f, 1f }) TentSide(b, side);
            TentBack(b);
            piece.Body = Attach(piece.Root, "Tent", b.Build(GreekMaterial.Linen, GreekMaterial.Wood, GreekMaterial.Rope));
            // What falls inside lies on the platform; outside it, on the hill.
            var rest = PieceRest(piece, 0.01f, TentWidth + 0.6f, TentLength + 0.6f);
            AddBreakable(item, piece, $"Store.{name}.{item.Pieces.Count - 1}/", () => BuildTentModel(rest), rest, FootprintDistance(piece));
        }

        static void TentPole(GreekShips.MeshBuilder b, float end) =>
            b.Tube(GreekMaterial.Wood, new List<Vector3> { new Vector3(0f, 0f, end * (TentLength * 0.5f - 0.1f)), new Vector3(0f, TentHeight + 0.12f, end * (TentLength * 0.5f - 0.1f)) }, 0.05f, 0.045f, 5);

        static void TentRidge(GreekShips.MeshBuilder b) =>
            b.Tube(GreekMaterial.Wood, new List<Vector3> { new Vector3(0f, TentHeight, -TentLength * 0.5f), new Vector3(0f, TentHeight, TentLength * 0.5f) }, 0.04f, 0.04f, 5);

        /// <summary>One side of the canvas from the ridge down to its pegged foot.</summary>
        static void TentSide(GreekShips.MeshBuilder b, float side)
        {
            var a = b.Vertex(new Vector3(0f, TentHeight + 0.03f, -TentLength * 0.5f), new Vector2(0f, 0f));
            var c = b.Vertex(new Vector3(0f, TentHeight + 0.03f, TentLength * 0.5f), new Vector2(TentLength, 0f));
            var d = b.Vertex(new Vector3(side * TentWidth * 0.5f, 0.12f, TentLength * 0.5f), new Vector2(TentLength, 2f));
            var e = b.Vertex(new Vector3(side * TentWidth * 0.5f, 0.12f, -TentLength * 0.5f), new Vector2(0f, 2f));
            b.Quad(GreekMaterial.Linen, a, c, d, e, new Vector3(side, 0.6f, 0f));
            // The inside of the cloth, seen through the open end.
            b.Quad(GreekMaterial.Linen, a, e, d, c, new Vector3(-side, -0.6f, 0f));
        }

        /// <summary>The closed end at -z.</summary>
        static void TentBack(GreekShips.MeshBuilder b)
        {
            var z = -TentLength * 0.5f;
            var a = b.Vertex(new Vector3(-TentWidth * 0.5f, 0.12f, z), new Vector2(0f, 0f));
            var c = b.Vertex(new Vector3(TentWidth * 0.5f, 0.12f, z), new Vector2(TentWidth, 0f));
            var top = b.Vertex(new Vector3(0f, TentHeight + 0.03f, z), new Vector2(TentWidth * 0.5f, TentHeight));
            b.Triangle(GreekMaterial.Linen, a, c, top, Vector3.back);
            b.Triangle(GreekMaterial.Linen, a, top, c, Vector3.forward);
        }

        /// <summary>The levelled platform of dry stone, pegs along both feet and guy ropes from the poles' heads to pegs beyond the ends.</summary>
        static void TentPegs(GreekShips.MeshBuilder b)
        {
            b.Box(GreekMaterial.Stone, new Vector3(0f, -1.02f, 0f), new Vector3(TentWidth * 0.5f + 0.3f, 1f, TentLength * 0.5f + 1.05f), Quaternion.identity);
            foreach (var side in new[] { -1f, 1f })
                for (var i = 0; i < 4; i++)
                {
                    var z = -TentLength * 0.5f + i * TentLength / 3f;
                    b.Box(GreekMaterial.Wood, new Vector3(side * (TentWidth * 0.5f + 0.05f), 0.1f, z), new Vector3(0.025f, 0.12f, 0.025f), Quaternion.identity);
                }
            foreach (var end in new[] { -1f, 1f })
            {
                var head = new Vector3(0f, TentHeight + 0.08f, end * (TentLength * 0.5f - 0.1f));
                var peg = new Vector3(0f, 0.05f, end * (TentLength * 0.5f + 0.9f));
                b.Tube(GreekMaterial.Rope, new List<Vector3> { head, peg }, 0.015f, 0.015f, 3);
                b.Box(GreekMaterial.Wood, peg, new Vector3(0.03f, 0.1f, 0.03f), Quaternion.identity);
            }
        }

        /// <summary>
        /// A tent taken apart: the pegs and ropes stay in the ground; the poles go over; the ridge pole drops with them; each
        /// side of the canvas swings down about its pegged foot into a heap of cloth.
        /// </summary>
        (StructureGraph, ElementShape[]) BuildTentModel(Func<Vector3, float> rest)
        {
            var graph = new StructureGraph();
            var shapes = new List<ElementShape>();
            int Add(string id, ElementRole role, BuildMaterial material, DetachMethod detach, GreekMesh mesh, Vector3 outward, string joint, ElementShape shape, params int[] supports) =>
                AddElement(graph, shapes, id, role, material, detach, mesh, outward, joint, shape, supports);
            var pegs = Builder();
            TentPegs(pegs);
            var ground = Add("pegs", ElementRole.Foundation, BuildMaterial.Timber, DetachMethod.Fixed, pegs.Build(GreekMaterial.Wood, GreekMaterial.Rope), Vector3.zero,
                "pegs driven into the shingle", new ElementShape());
            var poles = new int[2];
            var k = 0;
            foreach (var end in new[] { -1f, 1f })
            {
                var b = Builder();
                TentPole(b, end);
                poles[k] = Add($"pole.{k}", ElementRole.LoadBearing, BuildMaterial.Timber, DetachMethod.Topple, b.Build(GreekMaterial.Wood), new Vector3(0f, 0f, end),
                    "stood in the ground, held by its guy", new ElementShape { Radius = 0.05f, Base = new Vector3(0f, 0f, end * (TentLength * 0.5f - 0.1f)) }, ground);
                graph[poles[k]].Column = 500 + k;
                graph[poles[k]].Primary = true;
                k++;
            }
            var ridgeMesh = Builder();
            TentRidge(ridgeMesh);
            var ridge = Add("ridge", ElementRole.Beam, BuildMaterial.Timber, DetachMethod.Drop, ridgeMesh.Build(GreekMaterial.Wood), Vector3.up, "on the poles' heads",
                new ElementShape { Radius = 0.04f }, poles);
            foreach (var side in new[] { -1f, 1f })
            {
                var b = Builder();
                TentSide(b, side);
                if (side < 0f) TentBack(b);
                var hash = DestructionHash.Mix((uint)seed, 1300 + (side > 0f ? 0 : 1));
                var shape = new ElementShape
                {
                    EaveEnd = new Vector3(side * TentWidth * 0.5f, 0.12f, 0f), RidgeEnd = new Vector3(0f, TentHeight, 0f),
                    EaveHinge = new Vector3(side * TentWidth * 0.5f, 0.12f, 0f), InnerHinge = new Vector3(0f, TentHeight, 0f),
                };
                var inside = Builder();
                var inX = side * TentWidth * 0.2f;
                Heap(inside, GreekMaterial.Linen, new Vector3(inX, rest(new Vector3(inX, 0f, 0f)), 0f), 0.55f, TentLength * 0.42f, 0.22f, hash);
                shape.Heap = inside.Build(GreekMaterial.Linen);
                var outside = Builder();
                var outX = side * (TentWidth * 0.5f + 0.5f);
                Heap(outside, GreekMaterial.Linen, new Vector3(outX, rest(new Vector3(outX, 0f, 0f)), 0f), 0.6f, TentLength * 0.42f, 0.18f, hash + 1u);
                shape.HeapOut = outside.Build(GreekMaterial.Linen);
                var made = Add($"canvas.{(side > 0f ? "a" : "b")}", ElementRole.Roof, BuildMaterial.Timber, DetachMethod.CaveIn, b.Build(GreekMaterial.Linen), new Vector3(side, 0f, 0f),
                    "linen over the ridge pole, pegged at its foot", shape, ridge);
                graph[made].Primary = true;
            }
            return (graph, shapes.ToArray());
        }

        /// <summary>
        /// The camp's gear, one mesh in the settlement's frame: a boat drawn up and laid keel up on two trestles, a hearth of
        /// stones with its ash, crates, amphorae and sacks by the gate. It is no piece: the camp's place on the land is the
        /// palisade's and the tents'.
        /// </summary>
        void AddCampGear(Structure item)
        {
            var b = Builder();
            // The boat, 4.4 m long, keel up on its trestles at the back of the camp, along the shore.
            var boat = Builder();
            BoatHull(boat, 4.4f, 1.6f, 0.7f);
            var hull = boat.Build(GreekMaterial.Pitch, GreekMaterial.Wood);
            var at = (u: -10.1f, z: 0f);
            var ground = Ground(at.u, at.z);
            b.Append(hull, Local(at.u, at.z, ground + 0.85f), Quaternion.Euler(0f, 0f, 180f));
            Discard(hull.Mesh);
            foreach (var end in new[] { -1.3f, 1.3f })
                foreach (var leg in new[] { -0.55f, 0.55f })
                    b.Box(GreekMaterial.Wood, Local(at.u + leg * 0.5f, at.z + end, ground + 0.42f), new Vector3(0.05f, 0.45f, 0.05f), Quaternion.Euler(0f, 0f, toward * -leg * 30f));
            foreach (var end in new[] { -1.3f, 1.3f })
                b.Box(GreekMaterial.Wood, Local(at.u, at.z + end, ground + 0.82f), new Vector3(0.85f, 0.05f, 0.06f), Quaternion.identity);

            // The hearth in the middle of the camp: a ring of stones round its ash.
            var hearth = (u: -4.6f, z: 0.4f);
            var level = Ground(hearth.u, hearth.z);
            for (var i = 0; i < 9; i++)
            {
                var angle = i * Mathf.PI * 2f / 9f;
                var p = Local(hearth.u + Mathf.Cos(angle) * 0.55f, hearth.z + Mathf.Sin(angle) * 0.55f, level + 0.08f);
                b.Box(GreekMaterial.Stone, p, new Vector3(0.14f, 0.1f, 0.12f), Quaternion.Euler(0f, angle * Mathf.Rad2Deg, 0f));
            }
            b.Lathe(GreekMaterial.Pitch, new[] { (0f, 0.45f), (0.06f, 0.3f), (0.08f, 0f) }, 10, Local(hearth.u, hearth.z, level));

            // Crates and amphorae by the gate, sacks across from them.
            var crates = (u: -2.4f, z: 4.3f);
            for (var i = 0; i < 4; i++)
            {
                var p = Local(crates.u - (i % 2) * 0.7f, crates.z + (i / 2) * 0.7f, Ground(crates.u, crates.z) + 0.3f);
                b.Box(GreekMaterial.Wood, p, new Vector3(0.3f, 0.3f, 0.3f), Quaternion.Euler(0f, 7f * i, 0f));
            }
            b.Box(GreekMaterial.Wood, Local(crates.u - 0.35f, crates.z + 0.35f, Ground(crates.u, crates.z) + 0.85f), new Vector3(0.28f, 0.25f, 0.28f), Quaternion.Euler(0f, 20f, 0f));
            var jar = Builder();
            jar.Lathe(GreekMaterial.Terracotta, Amphora, 10);
            var amphora = jar.Build(GreekMaterial.Terracotta);
            for (var i = 0; i < 4; i++)
            {
                var u = -1.6f;
                var z = 2.4f + i * 0.4f;
                b.Append(amphora, Local(u, z, Ground(u, z)), Quaternion.Euler(0f, 0f, toward * 10f) * Quaternion.Euler(0f, 41f * i, 0f));
            }
            Discard(amphora.Mesh);
            var sacks = (u: -2.2f, z: -4.2f);
            for (var i = 0; i < 5; i++)
            {
                var u = sacks.u - (i % 3) * 0.55f;
                var z = sacks.z - (i / 3) * 0.6f;
                b.Lathe(GreekMaterial.Linen, new[] { (0f, 0.2f), (0.15f, 0.3f), (0.4f, 0.26f), (0.52f, 0.12f), (0.58f, 0.03f) }, 8, Local(u, z, Ground(u, z)));
            }
            Attach(item.Root.transform, "Camp gear", b.Build(GreekMaterial.Wood, GreekMaterial.Pitch, GreekMaterial.Stone, GreekMaterial.Terracotta, GreekMaterial.Linen));
        }

        /// <summary>
        /// A small boat's hull, right way up about its gunwale (y 0), along z: sections narrowing to the stem and stern, the
        /// keel deeper amidships; pitch outside and in, a timber gunwale strake.
        /// </summary>
        static void BoatHull(GreekShips.MeshBuilder b, float length, float beam, float depth)
        {
            const int sections = 10;
            var profile = new[] { (0f, -1f), (0.55f, -0.8f), (0.92f, -0.4f), (1f, 0f) };
            var ids = new int[sections + 1, 2, profile.Length];
            for (var s = 0; s <= sections; s++)
            {
                var t = s / (float)sections;
                var z = (t - 0.5f) * length;
                var width = Mathf.Pow(Mathf.Sin(Mathf.PI * t), 0.55f);
                var keel = depth * (0.5f + 0.5f * width);
                for (var side = 0; side < 2; side++)
                    for (var j = 0; j < profile.Length; j++)
                    {
                        var (across, down) = profile[j];
                        var x = (side == 0 ? -1f : 1f) * across * beam * 0.5f * width;
                        ids[s, side, j] = b.Vertex(new Vector3(x, down * keel, z), new Vector2(z, j * 0.4f));
                    }
            }
            for (var s = 0; s < sections; s++)
                for (var side = 0; side < 2; side++)
                    for (var j = 0; j + 1 < profile.Length; j++)
                    {
                        var outward = new Vector3(side == 0 ? -1f : 1f, -0.6f, 0f);
                        b.Quad(GreekMaterial.Pitch, ids[s, side, j], ids[s + 1, side, j], ids[s + 1, side, j + 1], ids[s, side, j + 1], outward);
                        b.Quad(GreekMaterial.Pitch, ids[s, side, j], ids[s, side, j + 1], ids[s + 1, side, j + 1], ids[s + 1, side, j], -outward);
                    }
            foreach (var side in new[] { -1f, 1f })
                for (var s = 1; s < sections - 1; s++)
                {
                    var t0 = s / (float)sections;
                    var t1 = (s + 1) / (float)sections;
                    var p0 = new Vector3(side * beam * 0.5f * Mathf.Pow(Mathf.Sin(Mathf.PI * t0), 0.55f), 0.02f, (t0 - 0.5f) * length);
                    var p1 = new Vector3(side * beam * 0.5f * Mathf.Pow(Mathf.Sin(Mathf.PI * t1), 0.55f), 0.02f, (t1 - 0.5f) * length);
                    b.Tube(GreekMaterial.Wood, new List<Vector3> { p0, p1 }, 0.035f, 0.035f, 4);
                }
        }
    }
}
