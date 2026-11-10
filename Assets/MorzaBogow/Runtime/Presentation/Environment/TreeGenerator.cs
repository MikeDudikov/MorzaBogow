using System.Collections.Generic;
using UnityEngine;

namespace MorzaBogow.Presentation.Landscape
{
    public enum BoneRole { Root, Trunk, Scaffold, Branch, Twig }

    /// <summary>
    /// One bone of a tree in the tree's own space: the base of the stem at the origin, +Y up, metres. A bone bends
    /// about its joint. <see cref="Driver"/> names the oscillator that moves it: itself for a stem, a main limb or a
    /// branch; the inner part of the same limb for its outer part, which then adds <see cref="Share"/> of that bending
    /// on top (the shape of the mode: a limb curves further toward its tip). Shoots (twigs) flutter on their own.
    /// </summary>
    public sealed class TreeBone
    {
        public int Index;
        public int Parent = -1;
        public BoneRole Role;
        public Vector3 Origin;
        public Quaternion Rest = Quaternion.identity;
        public float Length;
        public float BaseRadius, TipRadius;
        public int Driver = -1;
        public float Share = 1f;

        /// <summary>Green wood of the bone (kg) and its centre; leaf sprays it carries and the sum of their centres.</summary>
        public float WoodMass;
        public Vector3 WoodCentre;
        public int Sprays;
        public Vector3 SprayCentres;

        public Vector3 Axis => Rest * Vector3.up;
        public Vector3 Tip => Origin + Axis * Length;
        public bool Oscillates => Driver == Index;
    }

    /// <summary>A spray of leaves drawn as a pair of cards, top and underside, bound to one bone.</summary>
    public readonly struct LeafSpray
    {
        public readonly int Bone;
        public readonly Vector3 Centre, Normal, Along;
        public readonly float Size;

        public LeafSpray(int bone, Vector3 centre, Vector3 normal, Vector3 along, float size)
        {
            Bone = bone;
            Centre = centre;
            Normal = normal;
            Along = along;
            Size = size;
        }
    }

    /// <summary>One level of detail of a tree: bark (submesh 0) and leaves (submesh 1), skinned to the tree's bones.</summary>
    public sealed class TreeMesh
    {
        public readonly List<Vector3> Positions = new List<Vector3>();
        public readonly List<Vector3> Normals = new List<Vector3>();
        public readonly List<Vector4> Tangents = new List<Vector4>();
        public readonly List<Vector2> Uv = new List<Vector2>();
        public readonly List<BoneWeight> Weights = new List<BoneWeight>();
        public readonly List<int> Bark = new List<int>();
        public readonly List<int> Leaves = new List<int>();

        public int Vertices => Positions.Count;
        public int Triangles => (Bark.Count + Leaves.Count) / 3;
    }

    public sealed class TreeModel
    {
        public TreeSpecies Species;
        public uint Seed;
        public float Height, StemDiameter, ForkHeight, CrownRadius;

        /// <summary>The crown's envelope: an ellipsoid round the leaf-bearing shoots (centre, semi-axes).</summary>
        public Vector3 CrownCentre, CrownAxes;

        /// <summary>First sway frequency of the whole tree, Hz (the species' measured one scaled to this size).</summary>
        public float SwayFrequency;

        public readonly List<TreeBone> Bones = new List<TreeBone>();
        public readonly List<LeafSpray> Sprays = new List<LeafSpray>();
        public readonly TreeMesh[] Lods = { new TreeMesh(), new TreeMesh(), new TreeMesh() };

        /// <summary>The crown seen from the side, m²: what the wind's drag acts on (Cd is given on this area).</summary>
        public float CrownArea => Mathf.PI * CrownAxes.x * CrownAxes.y;

        /// <summary>Highest point of the drawn tree (bark and leaves of the most detailed level).</summary>
        public float Top
        {
            get
            {
                var top = 0f;
                foreach (var point in Lods[0].Positions) top = Mathf.Max(top, point.y);
                return top;
            }
        }
    }

    /// <summary>
    /// Builds a tree from its species and a stable id. Same id, same tree; nothing here touches the game's random
    /// numbers. The skeleton follows the pipe model (the cross-sections of the limbs leaving a fork add up to the one
    /// entering it); the stem is fluted and twisted like an old olive's, the limbs curve upward into a vase-shaped,
    /// open crown and the leaves sit in clumps round the shoots at its surface. Three levels of detail are drawn
    /// over the same bones, so changing level never changes how far the tree is bent.
    /// </summary>
    public static class TreeGenerator
    {
        public const int LodCount = 3;

        /// <summary>Metres of bark per repeat of the bark texture.</summary>
        public const float BarkTile = 1f;

        /// <summary>Leaves and shoot of one drawn spray at the full count, kg (a share of a mature olive's foliage, estimate).</summary>
        public const float SprayMass = 0.1f;

        /// <summary>Card twins sit this far apart so the top and the underside never fight for the same depth.</summary>
        public const float CardGap = 0.006f;

        /// <summary>Most sprays a tree draws at full detail: keeps the largest crowns within the triangle budget.</summary>
        public const int MostSprays = 600;

        /// <summary>
        /// Atlas cells of the leaf texture (lower-left corners of quarters): a spray from above, the same spray from
        /// below, a cluster of sprays from above and from below. Farther levels draw fewer, larger cluster cards.
        /// </summary>
        public static readonly Vector2[] AtlasCells = { new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(0.5f, 0f) };

        static readonly float[] SprayShare = { 1f, 0.3f, 0.06f };
        static readonly float[] SprayGrowth = { 1f, 1.8f, 3.8f };

        /// <summary>Thinnest limb drawn at each level, radius in metres: thinner ones are under half a pixel there.</summary>
        static readonly float[] ThinnestDrawn = { 0.006f, 0.02f, 0.05f };

        // Most sides and the spacing of rings (metres) per level: a thin stem gets fewer sides (one per 6, 5 or 4 cm of
        // girth), a level drops branches altogether where they would be under a pixel.
        static readonly int[] TrunkSides = { 14, 8, 5 };
        static readonly int[] LimbSides = { 9, 6, 4 };
        static readonly int[] BranchSides = { 6, 4, 0 };
        static readonly float[] TrunkStep = { 0.12f, 0.35f, 100f };
        static readonly float[] LimbStep = { 0.3f, 0.7f, 100f };
        static readonly float[] BranchStep = { 0.45f, 100f, 100f };

        public static TreeModel Build(TreeSpecies species, uint seed, Vector3 leeward = default, float exposure = 0f)
        {
            var random = new StableHash.Sequence(StableHash.Mix(seed, 0x0A11CEu));
            var model = new TreeModel { Species = species, Seed = seed };
            Form(model, ref random, leeward, Mathf.Clamp01(exposure));
            Leaves(model, ref random);
            Wood(model);
            for (var lod = 0; lod < LodCount; lod++) Draw(model, lod);
            return model;
        }

        // ---------------------------------------------------------------- skeleton

        static void Form(TreeModel model, ref StableHash.Sequence random, Vector3 leeward, float exposure)
        {
            var species = model.Species;
            var size = random.Next();
            model.Height = Mathf.Lerp(species.HeightMin, species.HeightMax, size);
            model.StemDiameter = Mathf.Lerp(species.StemDiameterMin, species.StemDiameterMax, Mathf.Clamp01(0.7f * size + 0.3f * random.Next()));
            model.ForkHeight = Mathf.Lerp(species.ForkHeightMin, species.ForkHeightMax, random.Next());
            model.CrownRadius = 0.5f * model.Height * species.CrownWidthRatio * random.Range(0.85f, 1.15f);
            leeward.y = 0f;
            leeward = leeward.sqrMagnitude > 1e-6f ? leeward.normalized : Vector3.zero;

            var root = AddBone(model, -1, BoneRole.Root, Vector3.zero, Vector3.up, 0f, 0f, 0f);
            var stems = Mathf.Clamp(random.Range(species.StemsMin, species.StemsMax + 1), 1, 6);
            var multi = stems > 1;
            var spread = random.Range(0f, 360f);
            var forks = Vector3.zero;
            var tops = new List<(int bone, Vector3 outward)>();
            for (var s = 0; s < stems; s++)
            {
                var outward = Horizontal(spread + s * 360f / stems + random.Range(-20f, 20f));
                var lean = multi ? outward : Horizontal(random.Range(0f, 360f));
                var axis = Tilt(Vector3.up, lean, multi ? random.Range(10f, 24f) : random.Range(3f, 10f));
                // A tree out in the wind leans away from it.
                if (leeward != Vector3.zero) axis = Tilt(axis, leeward, exposure * random.Range(3f, 8f));
                var forkHeight = model.ForkHeight * (multi ? random.Range(0.8f, 1.2f) : 1f);
                var radius = 0.5f * model.StemDiameter * (multi ? random.Range(0.8f, 1.1f) : 1f);
                var origin = multi ? outward * random.Range(0.06f, 0.18f) : Vector3.zero;
                var middle = radius * 0.89f;
                var lower = AddBone(model, root, BoneRole.Trunk, origin, axis, 0.55f * forkHeight / Mathf.Max(0.5f, axis.y), radius, middle);
                model.Bones[lower].Driver = lower;
                // The upper stem turns another way: the twist of an old olive.
                var upperAxis = Tilt(axis, Horizontal(random.Range(0f, 360f)), random.Range(species.StemKinkMin, species.StemKinkMax));
                var upper = AddBone(model, lower, BoneRole.Trunk, model.Bones[lower].Tip, upperAxis, 0.45f * forkHeight / Mathf.Max(0.5f, upperAxis.y), middle, radius * 0.8f);
                Follow(model, upper, lower, 0.5f);
                forks += model.Bones[upper].Tip;
                tops.Add((upper, outward));
            }
            forks /= stems;
            var crownBottom = Mathf.Max(forks.y, model.Height * species.CrownBase);
            var crownHeight = model.Height - crownBottom;
            model.CrownCentre = new Vector3(forks.x, crownBottom + crownHeight * 0.5f, forks.z);
            model.CrownAxes = new Vector3(model.CrownRadius, crownHeight * 0.5f, model.CrownRadius);

            foreach (var (stem, outward) in tops)
            {
                var fork = model.Bones[stem].Tip;
                var forkRadius = model.Bones[stem].TipRadius;
                var limbs = random.Range(species.ScaffoldsMin, species.ScaffoldsMax + 1);
                var turn = random.Range(0f, 360f);
                // Pipe model: the limbs leaving the fork carry about the cross-section of the stem entering it.
                var limbRadius = forkRadius / Mathf.Sqrt(limbs) * 1.05f;
                for (var i = 0; i < limbs; i++)
                {
                    var direction = Horizontal(turn + i * 360f / limbs + random.Range(-22f, 22f));
                    if (multi) direction = (direction + outward * 0.8f).normalized;
                    var tilt = random.Range(species.ScaffoldTiltMin, species.ScaffoldTiltMax);
                    var axis = Tilt(Vector3.up, direction, tilt);
                    // Limbs toward the lee grow longer, those into the wind shorter: a wind-trained crown.
                    var reach = 1f + 0.25f * exposure * Vector3.Dot(direction, leeward);
                    // Main limbs end well inside the crown: the foliage, not the limbs, makes its outline.
                    var rise = (model.Height - fork.y) * random.Range(species.LimbRiseMin, species.LimbRiseMax) * reach;
                    var length = rise / Mathf.Max(0.4f, axis.y);
                    var radius = limbRadius * random.Range(0.9f, 1.1f);
                    var inner = AddBone(model, stem, BoneRole.Scaffold, fork, axis, 0.5f * length, radius, radius * 0.8f);
                    model.Bones[inner].Driver = inner;
                    var outerAxis = Vector3.RotateTowards(axis, Vector3.up, random.Range(species.LimbCurlMin, species.LimbCurlMax) * Mathf.Deg2Rad, 0f);
                    var outer = AddBone(model, inner, BoneRole.Scaffold, model.Bones[inner].Tip, outerAxis, 0.5f * length, radius * 0.8f, radius * 0.55f);
                    Follow(model, outer, inner, 0.6f);
                    Branches(model, ref random, inner, outer, direction);
                }
            }
        }

        static void Branches(TreeModel model, ref StableHash.Sequence random, int inner, int outer, Vector3 direction)
        {
            var count = random.Range(model.Species.BranchesMin, model.Species.BranchesMax + 1);
            for (var k = 0; k < count; k++)
            {
                var leader = k == count - 1;
                var along = leader ? 1f : random.Range(0.35f, 0.95f);
                var parent = along >= 0.5f ? outer : inner;
                var p = model.Bones[parent];
                var t = parent == outer ? (along - 0.5f) * 2f : along * 2f;
                var at = p.Origin + p.Axis * (p.Length * t);
                var radiusAt = Mathf.Lerp(p.BaseRadius, p.TipRadius, t);
                var axis = leader
                    ? Vector3.RotateTowards(p.Axis, Vector3.up, random.Range(0f, 12f) * Mathf.Deg2Rad, 0f)
                    : Tilt(Vector3.up, Quaternion.AngleAxis(random.Range(-70f, 70f), Vector3.up) * direction, random.Range(model.Species.BranchTiltMin, model.Species.BranchTiltMax));
                var length = model.Height * random.Range(model.Species.BranchLengthMin, model.Species.BranchLengthMax) * (leader ? 1.1f : 1f);
                length = InsideCrown(model, at, axis, length);
                var radius = Mathf.Min(radiusAt * random.Range(0.45f, 0.65f) * (leader ? 1.15f : 1f), radiusAt * 0.85f);
                var branch = AddBone(model, parent, BoneRole.Branch, at, axis, length, radius, Mathf.Max(0.008f, radius * 0.3f));
                model.Bones[branch].Driver = branch;

                // Shoots hang from the outer part of a branch: the young, leafy wood of an olive.
                var shoots = random.Range(model.Species.ShootsMin, model.Species.ShootsMax + 1);
                for (var w = 0; w < shoots; w++)
                {
                    var b = model.Bones[branch];
                    var from = b.Origin + b.Axis * (b.Length * Mathf.Lerp(0.4f, 1f, (w + random.Range(0.2f, 0.8f)) / shoots));
                    var side = Quaternion.AngleAxis(random.Range(-80f, 80f), Vector3.up) * Flat(b.Axis, direction);
                    var shootAxis = Tilt(Vector3.up, side, random.Range(model.Species.ShootTiltMin, model.Species.ShootTiltMax));
                    AddBone(model, branch, BoneRole.Twig, from, shootAxis, random.Range(model.Species.ShootLengthMin, model.Species.ShootLengthMax) * Mathf.Clamp(model.Height / 6f, 0.6f, 1.2f), 0f, 0f);
                }
            }
        }

        static int AddBone(TreeModel model, int parent, BoneRole role, Vector3 origin, Vector3 axis, float length, float baseRadius, float tipRadius)
        {
            var bone = new TreeBone
            {
                Index = model.Bones.Count,
                Parent = parent,
                Role = role,
                Origin = origin,
                Rest = Quaternion.FromToRotation(Vector3.up, axis.normalized),
                Length = length,
                BaseRadius = baseRadius,
                TipRadius = tipRadius,
            };
            model.Bones.Add(bone);
            return bone.Index;
        }

        static void Follow(TreeModel model, int bone, int driver, float share)
        {
            model.Bones[bone].Driver = driver;
            model.Bones[bone].Share = share;
        }

        /// <summary>A horizontal unit vector at a bearing (degrees about +Y from +Z).</summary>
        static Vector3 Horizontal(float degrees) => Quaternion.Euler(0f, degrees, 0f) * Vector3.forward;

        /// <summary>The direction turned toward a horizontal direction by some degrees (past vertical if over 90°).</summary>
        static Vector3 Tilt(Vector3 axis, Vector3 toward, float degrees)
        {
            var hinge = Vector3.Cross(axis, toward);
            if (hinge.sqrMagnitude < 1e-8f) return axis;
            return (Quaternion.AngleAxis(degrees, hinge.normalized) * axis).normalized;
        }

        static Vector3 Flat(Vector3 axis, Vector3 fallback)
        {
            axis.y = 0f;
            return axis.sqrMagnitude > 1e-4f ? axis.normalized : fallback;
        }

        /// <summary>Inside the crown's envelope (scaled by a margin); below its middle only the width counts.</summary>
        public static bool InCrown(TreeModel model, Vector3 point, float margin = 1f)
        {
            var d = point - model.CrownCentre;
            var a = model.CrownAxes * margin;
            var across = (d.x * d.x + d.z * d.z) / (a.x * a.x);
            if (d.y < 0f) return across <= 1f;
            return across + d.y * d.y / (a.y * a.y) <= 1f;
        }

        static float InsideCrown(TreeModel model, Vector3 start, Vector3 axis, float length)
        {
            var shortest = length * 0.35f;
            for (var i = 0; i < 10 && length > shortest && !InCrown(model, start + axis * length, 1.05f); i++) length *= 0.88f;
            return Mathf.Max(length, shortest);
        }

        /// <summary>The outward normal of the crown's envelope through a point.</summary>
        public static Vector3 Outward(TreeModel model, Vector3 point)
        {
            var d = point - model.CrownCentre;
            var a = model.CrownAxes;
            var gradient = new Vector3(d.x / (a.x * a.x), d.y / (a.y * a.y), d.z / (a.z * a.z));
            return gradient.sqrMagnitude > 1e-8f ? gradient.normalized : Vector3.up;
        }

        // ---------------------------------------------------------------- leaves

        /// <summary>Surface of the crown that bears leaves: the envelope less its shaded underside (Thomsen's formula).</summary>
        public static float ShellArea(TreeModel model)
        {
            const float p = 1.6075f;
            var a = model.CrownAxes.x;
            var b = model.CrownAxes.y;
            var c = model.CrownAxes.z;
            var full = 4f * Mathf.PI * Mathf.Pow((Mathf.Pow(a * b, p) + Mathf.Pow(a * c, p) + Mathf.Pow(b * c, p)) / 3f, 1f / p);
            return 0.75f * full;
        }

        /// <summary>
        /// Leaves over the crown's outer layer, where the light is: points spread over the envelope (its underside left
        /// thin, as in a pruned olive), thinned by a smooth noise into masses with sky between them, each carried by
        /// the nearest shoot or branch and drawn in toward it if the wood does not reach that far.
        /// </summary>
        static void Leaves(TreeModel model, ref StableHash.Sequence random)
        {
            var bearers = new List<int>();
            foreach (var bone in model.Bones)
            {
                if (bone.Role == BoneRole.Twig || bone.Role == BoneRole.Branch || (bone.Role == BoneRole.Scaffold && !bone.Oscillates)) bearers.Add(bone.Index);
            }
            if (bearers.Count == 0) return;

            var count = Mathf.Clamp(Mathf.RoundToInt(model.Species.SprayDensity * ShellArea(model)), bearers.Count, MostSprays);
            var clumps = StableHash.Mix(model.Seed, 0xC10Du);
            var placed = 0;
            for (var attempt = 0; attempt < count * 8 && placed < count; attempt++)
            {
                var direction = Unit(ref random);
                if (direction.y < model.Species.SprayFloor) continue;
                var point = model.CrownCentre + Vector3.Scale(direction, model.CrownAxes) * random.Range(model.Species.ShellInner, 1.02f);
                if (random.Next() > Mathf.Clamp01(0.2f + Clumps(clumps, point / 1.1f))) continue;
                var bone = NearestBearer(model, bearers, point, out var onWood);
                var reach = point - onWood;
                if (reach.magnitude > model.Species.SprayReach) point = onWood + reach.normalized * model.Species.SprayReach;
                var outward = Outward(model, point);
                var normal = (outward * 0.65f + Vector3.up * 0.55f + Unit(ref random) * 0.4f).normalized;
                var along = point - onWood;
                along -= normal * Vector3.Dot(along, normal);
                if (along.sqrMagnitude < 1e-4f) along = Vector3.Cross(normal, Vector3.right);
                if (along.sqrMagnitude < 1e-4f) along = Vector3.Cross(normal, Vector3.forward);
                along = Quaternion.AngleAxis(random.Range(-35f, 35f), normal) * along.normalized;
                var size = model.Species.SprayLength * random.Range(0.8f, 1.2f);
                model.Sprays.Add(new LeafSpray(bone, point, normal, along.normalized, size));
                var carrier = model.Bones[bone];
                carrier.Sprays++;
                carrier.SprayCentres += point;
                placed++;
            }
        }

        static int NearestBearer(TreeModel model, List<int> bearers, Vector3 point, out Vector3 onWood)
        {
            var best = bearers[0];
            var bestDistance = float.MaxValue;
            onWood = model.Bones[best].Origin;
            foreach (var index in bearers)
            {
                var bone = model.Bones[index];
                var t = Mathf.Clamp(Vector3.Dot(point - bone.Origin, bone.Axis), 0f, bone.Length);
                var closest = bone.Origin + bone.Axis * t;
                var distance = (point - closest).sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = index;
                onWood = closest;
            }
            return best;
        }

        /// <summary>Smooth value noise in [0, 1) on a unit lattice, from a stable hash (no game random numbers).</summary>
        static float Clumps(uint seed, Vector3 p)
        {
            var x0 = Mathf.FloorToInt(p.x);
            var y0 = Mathf.FloorToInt(p.y);
            var z0 = Mathf.FloorToInt(p.z);
            var f = new Vector3(p.x - x0, p.y - y0, p.z - z0);
            var s = new Vector3(f.x * f.x * (3f - 2f * f.x), f.y * f.y * (3f - 2f * f.y), f.z * f.z * (3f - 2f * f.z));
            float Corner(int dx, int dy, int dz) => StableHash.Unit(StableHash.Mix(StableHash.Mix(seed, (uint)(x0 + dx)), (uint)(y0 + dy), (uint)(z0 + dz)));
            var x00 = Mathf.Lerp(Corner(0, 0, 0), Corner(1, 0, 0), s.x);
            var x10 = Mathf.Lerp(Corner(0, 1, 0), Corner(1, 1, 0), s.x);
            var x01 = Mathf.Lerp(Corner(0, 0, 1), Corner(1, 0, 1), s.x);
            var x11 = Mathf.Lerp(Corner(0, 1, 1), Corner(1, 1, 1), s.x);
            return Mathf.Lerp(Mathf.Lerp(x00, x10, s.y), Mathf.Lerp(x01, x11, s.y), s.z);
        }

        static Vector3 Unit(ref StableHash.Sequence random)
        {
            var v = new Vector3(random.Range(-1f, 1f), random.Range(-1f, 1f), random.Range(-1f, 1f));
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.up;
        }

        static void Wood(TreeModel model)
        {
            foreach (var bone in model.Bones)
            {
                if (bone.BaseRadius <= 0f || bone.Length <= 0f)
                {
                    bone.WoodMass = 0f;
                    bone.WoodCentre = bone.Origin;
                    continue;
                }
                var r0 = bone.BaseRadius;
                var r1 = bone.TipRadius;
                var sum = r0 * r0 + r0 * r1 + r1 * r1;
                bone.WoodMass = Mathf.PI * bone.Length * sum / 3f * model.Species.WoodDensity;
                // Centroid of a frustum from its wide end.
                bone.WoodCentre = bone.Origin + bone.Axis * (bone.Length * (r0 * r0 + 2f * r0 * r1 + 3f * r1 * r1) / (4f * sum));
            }
            model.SwayFrequency = model.Species.FrequencyFor(model.StemDiameter, model.Height);
        }

        // ---------------------------------------------------------------- drawing

        struct Station
        {
            public Vector3 Position, Axis;
            public float Radius, Distance, Height;
            public int Bone, Partner;
            public float PartnerWeight;
        }

        static void Draw(TreeModel model, int lod)
        {
            var mesh = model.Lods[lod];
            foreach (var bone in model.Bones)
            {
                if (bone.Role == BoneRole.Root || bone.Role == BoneRole.Twig || !bone.Oscillates) continue;
                if (bone.Role != BoneRole.Trunk && bone.BaseRadius < ThinnestDrawn[lod]) continue;
                var girth = 2f * Mathf.PI * bone.BaseRadius;
                int sides;
                float step;
                switch (bone.Role)
                {
                    case BoneRole.Trunk:
                        sides = Sides(girth, 0.06f, 5, TrunkSides[lod]);
                        step = TrunkStep[lod];
                        break;
                    case BoneRole.Scaffold:
                        sides = Sides(girth, 0.05f, 4, LimbSides[lod]);
                        step = LimbStep[lod];
                        break;
                    default:
                        sides = BranchSides[lod] < 3 ? 0 : Sides(girth, 0.04f, 3, BranchSides[lod]);
                        step = BranchStep[lod];
                        break;
                }
                if (sides < 3) continue;
                Tube(model, mesh, Chain(model, bone.Index), sides, step, lod < 2);
            }

            var share = SprayShare[lod];
            for (var i = 0; i < model.Sprays.Count; i++)
            {
                if (Mathf.FloorToInt((i + 1) * share) == Mathf.FloorToInt(i * share)) continue;
                var spray = model.Sprays[i];
                Card(model, mesh, spray, Mathf.Min(spray.Size * SprayGrowth[lod], Mathf.Max(spray.Size, model.Species.FarCardCap * model.CrownRadius)), lod > 0);
            }
        }

        static int Sides(float girth, float perSide, int fewest, int most) => Mathf.Clamp(Mathf.RoundToInt(girth / perSide), fewest, Mathf.Max(fewest, most));

        /// <summary>A limb as drawn: its driving bone and the bones that follow it outward.</summary>
        static List<int> Chain(TreeModel model, int start)
        {
            var chain = new List<int> { start };
            var found = true;
            while (found)
            {
                found = false;
                var last = chain[chain.Count - 1];
                foreach (var bone in model.Bones)
                {
                    if (bone.Parent != last || bone.Driver != start || bone.Index == start) continue;
                    chain.Add(bone.Index);
                    found = true;
                    break;
                }
            }
            return chain;
        }

        static void Tube(TreeModel model, TreeMesh mesh, List<int> chain, int sides, float step, bool cap)
        {
            var bones = model.Bones;
            var first = bones[chain[0]];
            var trunk = first.Role == BoneRole.Trunk;
            var species = model.Species;
            var phase = StableHash.Unit(StableHash.Mix(model.Seed, (uint)first.Index, 31u)) * Mathf.PI * 2f;
            var lobes = 3 + (int)(StableHash.Unit(StableHash.Mix(model.Seed, 77u)) * 2f);
            var fluting = trunk ? species.TrunkFluting : first.Role == BoneRole.Scaffold ? species.TrunkFluting * 0.4f : 0f;
            var twist = trunk ? species.TrunkTwist : species.TrunkTwist * 0.5f;

            var stations = new List<Station>();
            var distance = 0f;
            if (trunk)
            {
                // Sunk a little into the ground, so no slope shows a gap under the stem; held by the root bone.
                stations.Add(new Station { Position = first.Origin - Vector3.up * 0.15f, Axis = first.Axis, Radius = first.BaseRadius, Distance = -0.15f, Height = -0.15f, Bone = 0, Partner = -1 });
            }
            for (var c = 0; c < chain.Count; c++)
            {
                var bone = bones[chain[c]];
                var next = c + 1 < chain.Count ? bones[chain[c + 1]] : null;
                var previous = c > 0 ? chain[c - 1] : bone.Parent;
                var rings = Mathf.Max(1, Mathf.CeilToInt(bone.Length / step));
                var blend = Mathf.Min(0.3f * bone.Length, 0.25f);
                for (var i = c == 0 ? 0 : 1; i <= rings; i++)
                {
                    var t = (float)i / rings;
                    var along = bone.Length * t;
                    var station = new Station
                    {
                        Position = bone.Origin + bone.Axis * along,
                        Axis = i == rings && next != null ? (bone.Axis + next.Axis).normalized : bone.Axis,
                        Radius = Mathf.Lerp(bone.BaseRadius, bone.TipRadius, t),
                        Distance = distance + along,
                        Bone = bone.Index,
                        Partner = -1,
                    };
                    station.Height = station.Position.y;
                    if (i == rings && next != null)
                    {
                        station.Partner = next.Index;
                        station.PartnerWeight = 0.5f;
                    }
                    else if (trunk && c == 0 && along < 0.3f)
                    {
                        // The foot of the stem stays with the ground.
                        station.Partner = 0;
                        station.PartnerWeight = 1f - Mathf.SmoothStep(0f, 1f, along / 0.3f);
                    }
                    else if (along < blend && previous >= 0)
                    {
                        station.Partner = previous;
                        station.PartnerWeight = 0.5f * (1f - along / blend);
                    }
                    stations.Add(station);
                }
                distance += bone.Length;
            }

            var meanRadius = 0.5f * (first.BaseRadius + bones[chain[chain.Count - 1]].TipRadius);
            var turns = Mathf.Max(1f, Mathf.Round(2f * Mathf.PI * meanRadius / BarkTile));
            var start = mesh.Positions.Count;
            var u = Perpendicular(stations[0].Axis);
            for (var s = 0; s < stations.Count; s++)
            {
                var station = stations[s];
                u = (u - station.Axis * Vector3.Dot(u, station.Axis)).normalized;
                var v = Vector3.Cross(u, station.Axis);
                var flare = trunk ? 0.35f * Mathf.Exp(-Mathf.Max(0f, station.Height) / 0.3f) : 0f;
                var radius = station.Radius * (1f + flare);
                var swirl = twist * station.Distance;
                for (var k = 0; k <= sides; k++)
                {
                    var angle = 2f * Mathf.PI * k / sides;
                    var a1 = lobes * angle + swirl + phase;
                    var a2 = (lobes + 1) * angle - 0.6f * swirl + 1.3f * phase;
                    var ridge = fluting * (0.6f * Mathf.Cos(a1) + 0.4f * Mathf.Cos(a2));
                    var slope = radius * fluting * (-0.6f * lobes * Mathf.Sin(a1) - 0.4f * (lobes + 1) * Mathf.Sin(a2));
                    var r = radius * (1f + ridge);
                    var er = Mathf.Cos(angle) * u + Mathf.Sin(angle) * v;
                    var ephi = -Mathf.Sin(angle) * u + Mathf.Cos(angle) * v;
                    var normal = (er * r - ephi * slope).normalized;
                    var tangent = (er * slope + ephi * r).normalized;
                    mesh.Positions.Add(station.Position + er * r);
                    mesh.Normals.Add(normal);
                    mesh.Tangents.Add(Tangent(normal, tangent, station.Axis));
                    mesh.Uv.Add(new Vector2(turns * k / sides, station.Distance / BarkTile));
                    mesh.Weights.Add(Weight(station.Bone, station.Partner, station.PartnerWeight));
                }
            }
            var row = sides + 1;
            for (var s = 0; s + 1 < stations.Count; s++)
            {
                for (var k = 0; k < sides; k++)
                {
                    var a = start + s * row + k;
                    var b = a + 1;
                    var d = a + row;
                    var c = d + 1;
                    mesh.Bark.Add(a); mesh.Bark.Add(d); mesh.Bark.Add(b);
                    mesh.Bark.Add(b); mesh.Bark.Add(d); mesh.Bark.Add(c);
                }
            }

            if (!cap) return;
            // A blunt end where the limb thins out, so its tip is never an open pipe.
            var end = stations[stations.Count - 1];
            var tip = mesh.Positions.Count;
            mesh.Positions.Add(end.Position + end.Axis * end.Radius * 0.8f);
            mesh.Normals.Add(end.Axis);
            mesh.Tangents.Add(Tangent(end.Axis, Perpendicular(end.Axis), Vector3.Cross(end.Axis, Perpendicular(end.Axis))));
            mesh.Uv.Add(new Vector2(0.5f * turns, (end.Distance + end.Radius) / BarkTile));
            mesh.Weights.Add(Weight(end.Bone, -1, 0f));
            var last = start + (stations.Count - 1) * row;
            for (var k = 0; k < sides; k++)
            {
                mesh.Bark.Add(last + k);
                mesh.Bark.Add(tip);
                mesh.Bark.Add(last + k + 1);
            }
        }

        static void Card(TreeModel model, TreeMesh mesh, LeafSpray spray, float size, bool cluster)
        {
            var n = spray.Normal;
            var a = spray.Along;
            var w = Vector3.Cross(n, a).normalized;
            var h = 0.5f * size;
            var c = spray.Centre;
            var corners = new[] { c - w * h - a * h, c + w * h - a * h, c + w * h + a * h, c - w * h + a * h };
            var uvs = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            var top = cluster ? 2 : 0;
            var outward = Outward(model, c);

            // Shading normals bent toward the crown's surface: the crown reads as a mass, not as flat cards.
            var shadeTop = (n * 0.55f + outward * 0.45f).normalized;
            var first = mesh.Positions.Count;
            for (var i = 0; i < 4; i++) LeafVertex(mesh, corners[i], shadeTop, w, a, AtlasCells[top] + uvs[i] * 0.5f, spray.Bone);
            mesh.Leaves.Add(first); mesh.Leaves.Add(first + 2); mesh.Leaves.Add(first + 1);
            mesh.Leaves.Add(first); mesh.Leaves.Add(first + 3); mesh.Leaves.Add(first + 2);

            // The silvery underside, facing the other way a hair below.
            var shadeUnder = (-n * 0.75f + outward * 0.25f).normalized;
            first = mesh.Positions.Count;
            for (var i = 0; i < 4; i++) LeafVertex(mesh, corners[i] - n * CardGap, shadeUnder, w, a, AtlasCells[top + 1] + uvs[i] * 0.5f, spray.Bone);
            mesh.Leaves.Add(first); mesh.Leaves.Add(first + 1); mesh.Leaves.Add(first + 2);
            mesh.Leaves.Add(first); mesh.Leaves.Add(first + 2); mesh.Leaves.Add(first + 3);
        }

        static void LeafVertex(TreeMesh mesh, Vector3 position, Vector3 normal, Vector3 uDirection, Vector3 vDirection, Vector2 uv, int bone)
        {
            mesh.Positions.Add(position);
            mesh.Normals.Add(normal);
            mesh.Tangents.Add(Tangent(normal, uDirection, vDirection));
            mesh.Uv.Add(uv);
            mesh.Weights.Add(Weight(bone, -1, 0f));
        }

        /// <summary>A tangent along increasing u, with the handedness that makes the bitangent run along increasing v.</summary>
        static Vector4 Tangent(Vector3 normal, Vector3 uDirection, Vector3 vDirection)
        {
            var t = uDirection - normal * Vector3.Dot(uDirection, normal);
            t = t.sqrMagnitude > 1e-10f ? t.normalized : Perpendicular(normal);
            var w = Vector3.Dot(Vector3.Cross(normal, t), vDirection) >= 0f ? 1f : -1f;
            return new Vector4(t.x, t.y, t.z, w);
        }

        static Vector3 Perpendicular(Vector3 axis)
        {
            var p = Vector3.Cross(axis, Vector3.forward);
            if (p.sqrMagnitude < 1e-4f) p = Vector3.Cross(axis, Vector3.right);
            return p.normalized;
        }

        static BoneWeight Weight(int bone, int partner, float partnerWeight)
        {
            if (partner < 0 || partner == bone || partnerWeight <= 0f) return new BoneWeight { boneIndex0 = bone, weight0 = 1f };
            partnerWeight = Mathf.Clamp01(partnerWeight);
            return partnerWeight >= 0.5f
                ? new BoneWeight { boneIndex0 = partner, weight0 = partnerWeight, boneIndex1 = bone, weight1 = 1f - partnerWeight }
                : new BoneWeight { boneIndex0 = bone, weight0 = 1f - partnerWeight, boneIndex1 = partner, weight1 = partnerWeight };
        }
    }
}
