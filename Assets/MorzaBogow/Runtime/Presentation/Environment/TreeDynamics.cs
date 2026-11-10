using System;
using UnityEngine;

namespace MorzaBogow.Presentation.Landscape
{
    /// <summary>
    /// How one tree moves in the wind: a chain of rigid limbs on rotational springs (the stem on the ground, each main
    /// limb on the stem, each branch on its limb), two bending angles per oscillator, driven by drag on the crown and
    /// by the motion of the limb it grows from. Shoots flutter on top of it, analytically.
    /// <list type="bullet">
    /// <item>Drag F = ½·ρ_air·Cd·A·|U|·U on the crown seen from the side, with reconfiguration above U_r: the
    /// effective area falls as (U/U_r)^V, so drag grows as U^(2+V) (Gosselin 2019).</item>
    /// <item>The stem's first mode has the species' frequency at a reference height, scaled to the tree's size by
    /// the species' model (a pendulum for broadleaved trees, Jackson et al. 2021); a limb's spring is that of a
    /// cantilever, k = 3·E·I/L, with I of its mean diameter and E of the wood (Euler–Bernoulli).</item>
    /// <item>Damping 2·ζ·√(k·J) plus the drag's own damping, linearised: ∂F/∂U = (1 + V/2)·ρ·Cd·A_eff·U.</item>
    /// <item>A limb on a moving limb feels the frame's angular acceleration (−J·α) and its joint's linear
    /// acceleration (−m·r_c × a): small-angle Newton–Euler, the products of rates left out.</item>
    /// </list>
    /// Semi-implicit Euler at 60 steps per second of game time: stable while ω·Δt &lt; 2, here at most 0.94.
    /// Gravity's effect on the bending stiffness and the sway of the root plate are left out.
    /// Presentation only: nothing here reaches the simulation.
    /// </summary>
    public sealed class TreeDynamics
    {
        public const float StepLength = 1f / 60f;

        /// <summary>Highest oscillator frequency kept, Hz: a numerical limit (ω·Δt ≤ 0.94 at 60 steps a second), noted in the documentation.</summary>
        public const float HighestFrequency = 9f;

        public struct Mode
        {
            public int Bone, Parent;
            public Vector3 Joint, AxisX, AxisZ, FromParent;

            /// <summary>Moment of inertia for the bend toward local X (about local Z) and toward local Z (about local X).</summary>
            public Vector2 Inertia;

            public float Stiffness, Damping, Frequency;

            /// <summary>A branch's oscillator: left still when the tree is drawn too small for branches (LOD2).</summary>
            public bool Branch;

            /// <summary>Σ A·(c − joint) over the crown it carries (m³) and Σ A·d⊥² (m⁴) for the drag's damping.</summary>
            public Vector3 AreaMoment;
            public float AreaLever;

            /// <summary>Σ m·(c − joint) over everything it carries, kg·m.</summary>
            public Vector3 MassMoment;
            public float Mass;
        }

        public readonly TreeModel Model;
        public readonly Mode[] Modes;
        public readonly int[] ModeOfBone;
        public readonly Vector2[] Angle, Rate;

        /// <summary>The wind the tree feels now, in tree space, at the crown (m/s).</summary>
        public Vector3 Wind { get; private set; }

        /// <summary>Phase of the shoots' flutter, radians; kept in double so hours of play lose no precision.</summary>
        public double FlutterPhase { get; private set; }

        readonly Vector3[] frameAcceleration, jointAcceleration;

        public TreeDynamics(TreeModel model)
        {
            Model = model;
            var bones = model.Bones;
            ModeOfBone = new int[bones.Count];
            var count = 0;
            for (var i = 0; i < bones.Count; i++) ModeOfBone[i] = bones[i].Oscillates ? count++ : -1;
            Modes = new Mode[count];
            Angle = new Vector2[count];
            Rate = new Vector2[count];
            frameAcceleration = new Vector3[count];
            jointAcceleration = new Vector3[count];

            var sprayArea = model.Sprays.Count > 0 ? model.CrownArea / model.Sprays.Count : 0f;
            var species = model.Species;
            for (var i = 0; i < bones.Count; i++)
            {
                var m = ModeOfBone[i];
                if (m < 0) continue;
                var bone = bones[i];
                var mode = new Mode
                {
                    Bone = i,
                    Parent = -1,
                    Joint = bone.Origin,
                    Branch = bone.Role == BoneRole.Branch,
                    AxisX = bone.Rest * Vector3.right,
                    AxisZ = bone.Rest * Vector3.forward,
                };
                for (var p = bone.Parent; p >= 0; p = bones[p].Parent)
                {
                    if (ModeOfBone[p] < 0) continue;
                    mode.Parent = ModeOfBone[p];
                    mode.FromParent = bone.Origin - bones[p].Origin;
                    break;
                }

                // Everything this oscillator carries: its own wood, the bones that follow it and all it bears.
                float jx = 0f, jz = 0f;
                var axis = bone.Axis;
                for (var j = 0; j < bones.Count; j++)
                {
                    if (!Carries(bones, i, j)) continue;
                    var carried = bones[j];
                    if (carried.WoodMass > 0f)
                    {
                        var r = carried.WoodCentre - bone.Origin;
                        Accumulate(ref mode, ref jx, ref jz, carried.WoodMass, r, axis);
                        // A rod's own inertia about its centre, m·L²/12, for bending either way.
                        var own = carried.WoodMass * carried.Length * carried.Length / 12f;
                        jx += own;
                        jz += own;
                        var woodArea = (carried.BaseRadius + carried.TipRadius) * carried.Length;
                        AccumulateArea(ref mode, woodArea, r, axis);
                    }
                    if (carried.Sprays > 0)
                    {
                        var centre = carried.SprayCentres / carried.Sprays;
                        var r = centre - bone.Origin;
                        Accumulate(ref mode, ref jx, ref jz, carried.Sprays * species.SprayMass, r, axis);
                        AccumulateArea(ref mode, carried.Sprays * sprayArea, r, axis);
                    }
                }
                mode.Inertia = new Vector2(Mathf.Max(jz, 1e-4f), Mathf.Max(jx, 1e-4f));
                var inertia = 0.5f * (mode.Inertia.x + mode.Inertia.y);

                float frequency;
                if (bone.Role == BoneRole.Trunk) frequency = species.FrequencyFor(2f * bone.BaseRadius, model.Height);
                else
                {
                    // Cantilever on its joint: k = 3·E·I/L for a load at the tip, I of the limb's mean diameter.
                    float length = 0f, diameter = 0f;
                    for (var j = 0; j < bones.Count; j++)
                    {
                        if (bones[j].Driver != i) continue;
                        length += bones[j].Length;
                        diameter += (bones[j].BaseRadius + bones[j].TipRadius) * bones[j].Length;
                    }
                    diameter /= Mathf.Max(length, 1e-4f);
                    var second = Mathf.PI * Mathf.Pow(diameter, 4f) / 64f;
                    var spring = 3f * species.WoodModulus * second / Mathf.Max(length, 0.05f);
                    frequency = Mathf.Sqrt(spring / inertia) / (2f * Mathf.PI);
                }
                frequency = Mathf.Clamp(frequency, 0.3f, HighestFrequency);
                var omega = 2f * Mathf.PI * frequency;
                mode.Frequency = frequency;
                mode.Stiffness = inertia * omega * omega;
                var zeta = bone.Role == BoneRole.Trunk ? species.TrunkDamping : species.BranchDamping;
                mode.Damping = 2f * zeta * Mathf.Sqrt(mode.Stiffness * inertia);
                Modes[m] = mode;
            }
        }

        /// <summary>Whether bone j moves with oscillator i: i itself, its followers and everything beyond them.</summary>
        static bool Carries(System.Collections.Generic.List<TreeBone> bones, int i, int j)
        {
            for (var b = j; b >= 0; b = bones[b].Parent)
            {
                if (b == i) return true;
            }
            return false;
        }

        static void Accumulate(ref Mode mode, ref float jx, ref float jz, float mass, Vector3 r, Vector3 axis)
        {
            var square = r.sqrMagnitude;
            jx += mass * (square - Sq(Vector3.Dot(r, mode.AxisX)));
            jz += mass * (square - Sq(Vector3.Dot(r, mode.AxisZ)));
            mode.MassMoment += mass * r;
            mode.Mass += mass;
        }

        static void AccumulateArea(ref Mode mode, float area, Vector3 r, Vector3 axis)
        {
            mode.AreaMoment += area * r;
            var along = Vector3.Dot(r, axis);
            mode.AreaLever += area * Mathf.Max(0f, r.sqrMagnitude - along * along) + area * along * along;
        }

        static float Sq(float x) => x * x;

        /// <summary>Forces of the wind on the crown, per square metre of crown seen from the side: ½·ρ·Cd·reconf·|U|·U.</summary>
        Vector3 Pressure(Vector3 wind, float airDensity, out float aeroDamping)
        {
            var species = Model.Species;
            var speed = wind.magnitude;
            var reconfiguration = species.Reconfiguration(speed);
            var exponent = speed > species.ReconfigurationSpeed ? species.VogelExponent : 0f;
            aeroDamping = (1f + 0.5f * exponent) * airDensity * species.CrownDrag * reconfiguration * speed;
            return wind * (0.5f * airDensity * species.CrownDrag * reconfiguration * speed);
        }

        /// <summary>Puts every oscillator at rest in its static bending under a steady wind (no start-up swing).</summary>
        public void Settle(Vector3 wind, float airDensity)
        {
            Wind = wind;
            var force = Pressure(wind, airDensity, out _);
            for (var m = 0; m < Modes.Length; m++)
            {
                var torque = Vector3.Cross(Modes[m].AreaMoment, force);
                Angle[m] = new Vector2(-Vector3.Dot(torque, Modes[m].AxisZ), Vector3.Dot(torque, Modes[m].AxisX)) / Modes[m].Stiffness;
                Rate[m] = Vector2.zero;
            }
        }

        /// <summary>Advances the tree by dt seconds of game time in a steady wind (tree space, at the crown).</summary>
        public void Advance(float dt, Vector3 wind, float airDensity, bool limbsOnly = false)
        {
            if (dt <= 0f) return;
            var steps = Mathf.Max(1, Mathf.CeilToInt(dt / StepLength - 1e-3f));
            var h = dt / steps;
            for (var i = 0; i < steps; i++) Step(h, wind, airDensity, limbsOnly);
            var speed = wind.magnitude;
            // Shoots flap faster in a stronger wind, within the species' range.
            var rate = Model.Species.FlutterFrequency * Mathf.Clamp(0.7f + 0.3f * speed / 8f, 0.7f, 1.6f);
            FlutterPhase += 2.0 * Math.PI * rate * dt;
        }

        void Step(float dt, Vector3 wind, float airDensity, bool limbsOnly)
        {
            Wind = wind;
            var force = Pressure(wind, airDensity, out var aero);
            for (var m = 0; m < Modes.Length; m++)
            {
                var mode = Modes[m];
                if (limbsOnly && mode.Branch) continue;
                Vector3 frame = Vector3.zero, joint = Vector3.zero;
                if (mode.Parent >= 0)
                {
                    frame = frameAcceleration[mode.Parent];
                    joint = jointAcceleration[mode.Parent] + Vector3.Cross(frame, mode.FromParent);
                }
                var torque = Vector3.Cross(mode.AreaMoment, force) - Vector3.Cross(mode.MassMoment, joint);
                var damping = mode.Damping + aero * mode.AreaLever;
                var angle = Angle[m];
                var rate = Rate[m];
                var accelerationX = (-Vector3.Dot(torque, mode.AxisZ) - damping * rate.x - mode.Stiffness * angle.x) / mode.Inertia.x + Vector3.Dot(frame, mode.AxisZ);
                var accelerationZ = (Vector3.Dot(torque, mode.AxisX) - damping * rate.y - mode.Stiffness * angle.y) / mode.Inertia.y - Vector3.Dot(frame, mode.AxisX);
                rate.x += accelerationX * dt;
                rate.y += accelerationZ * dt;
                angle += rate * dt;
                Angle[m] = angle;
                Rate[m] = rate;
                frameAcceleration[m] = frame + mode.AxisX * accelerationZ - mode.AxisZ * accelerationX;
                jointAcceleration[m] = joint;
            }
        }

        /// <summary>
        /// The bending of a bone on top of its rest pose, as a rotation in the bone's own frame: an oscillator's two
        /// angles; a share of its driver's for a bone that follows one; a shoot's flutter.
        /// </summary>
        public Quaternion Bend(int index)
        {
            var bone = Model.Bones[index];
            if (bone.Role == BoneRole.Twig) return Flutter(bone);
            if (bone.Driver < 0) return Quaternion.identity;
            var m = ModeOfBone[bone.Driver];
            if (m < 0) return Quaternion.identity;
            var q = Angle[m];
            if (bone.Oscillates) return Small(new Vector3(q.y, 0f, -q.x));
            var mode = Modes[m];
            var tree = mode.AxisX * q.y - mode.AxisZ * q.x;
            return Small(Quaternion.Inverse(bone.Rest) * tree * bone.Share);
        }

        /// <summary>
        /// A shoot bent downwind by the drag and flapping across and along it at the flutter frequency, its phase set by
        /// the shoot's own id, and twisted about itself so the wind shows the leaves' silvery undersides.
        /// </summary>
        Quaternion Flutter(TreeBone twig)
        {
            var wind = Wind;
            var speed = wind.magnitude;
            if (speed < 0.05f) return Quaternion.identity;
            var hash = StableHash.Mix(Model.Seed, (uint)twig.Index, 0xF1u);
            var offset = StableHash.Unit(hash) * 2f * Mathf.PI;
            var multiplier = 0.85 + 0.3 * StableHash.Unit(StableHash.Mix(hash, 7u));
            var phase = (float)((FlutterPhase * multiplier) % (2.0 * Math.PI * 1000.0)) + offset;
            // Leaves start to flutter only above an onset speed (Tadrist et al. 2018: 1.35 m/s for Prunus).
            var excess = Mathf.Max(0f, speed - Model.Species.FlutterOnset);
            var amplitude = Mathf.Min(0.35f, 0.045f * Mathf.Pow(excess / 5f, 1.5f));
            var lean = Mathf.Min(0.3f, 0.025f * speed);
            var local = Quaternion.Inverse(twig.Rest) * (wind / speed);
            var downwind = new Vector2(local.x, local.z);
            downwind = downwind.sqrMagnitude > 1e-4f ? downwind.normalized : new Vector2(1f, 0f);
            var across = new Vector2(-downwind.y, downwind.x);
            var tilt = downwind * (lean + amplitude * Mathf.Sin(phase)) + across * (0.6f * amplitude * Mathf.Cos(1.37f * phase + 0.7f));
            var side = (hash & 1u) == 0u ? 1f : -1f;
            var twist = side * (Mathf.Min(0.9f, 0.07f * speed) + 0.5f * amplitude * Mathf.Sin(1.9f * phase + offset));
            return Small(new Vector3(tilt.y, 0f, -tilt.x)) * Quaternion.AngleAxis(twist * Mathf.Rad2Deg, Vector3.up);
        }

        static Quaternion Small(Vector3 rotation)
        {
            var angle = rotation.magnitude;
            return angle < 1e-7f ? Quaternion.identity : Quaternion.AngleAxis(angle * Mathf.Rad2Deg, rotation / angle);
        }

        /// <summary>How far the crown's centre has moved sideways from rest, metres: the stem's angle times its height.</summary>
        public float CrownSway
        {
            get
            {
                if (Modes.Length == 0) return 0f;
                var lever = Model.CrownCentre.y;
                return Angle[0].magnitude * lever;
            }
        }
    }
}
