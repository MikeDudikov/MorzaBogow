using System;
using System.Collections.Generic;
using MorzaBogow.Simulation;
using UnityEngine;

namespace MorzaBogow.Presentation
{
    /// <summary>
    /// Heave, roll and pitch of the ships the player can see, from the simulation's sea and wind: Seakeeping (buoyancy
    /// over ten stations on each side, added mass, damping) with the heel and trim of SailForces, the same functions the
    /// simulation uses when the wind moves ships. Stepped on simulation ticks, so it stands still in a pause and keeps pace
    /// with the game speed; shown between its last two steps. Presentation only: the simulation never reads it, and a
    /// loaded, newly seen or returning hull is laid on the water it floats in. It replaces the springs that followed HDRP's
    /// heights, so the waves move each hull once.
    /// <para>
    /// Far views step each hull every <see cref="FarStride"/> ticks instead of every tick, the hulls spread over the ticks
    /// by id (an explicit approximation, docs/MORZE_I_WIATR.md: 7–15% RMS of the motion, under a pixel at that zoom).
    /// </para>
    /// </summary>
    public sealed class SeakeepingDriver
    {
        /// <summary>Ticks between steps of a hull when the view is far: it divides MarineState.ShipUpdateTicks.</summary>
        public const int FarStride = 3;

        /// <summary>Pixels per metre below which the view is far (the default view shows about 7).</summary>
        public const float FarPixelsPerMetre = 10f;

        sealed class State
        {
            public ShipMotion Previous, Current;
            public HullForm Hull;
            public double Load = -1;
            public WindForces Forces;
            public double BowWater, PreviousBowWater;
            public int Seen;
            public int LastTick, PreviousTick;
        }

        readonly Dictionary<int, State> states = new Dictionary<int, State>();
        readonly List<int> stale = new List<int>();
        readonly Seakeeping model = new Seakeeping();
        int steps;

        /// <summary>Time of the last step, milliseconds, how many hulls the driver follows and how many it moved.</summary>
        public double Milliseconds { get; private set; }
        public int Ships { get; private set; }
        public int Stepped { get; private set; }

        /// <summary>The stride for a view of this many pixels per metre: every tick close up, FarStride far away.</summary>
        public static int StrideFor(float pixelsPerMetre) => pixelsPerMetre >= FarPixelsPerMetre ? 1 : FarStride;

        /// <summary>
        /// One simulation tick for the hulls in view: each steps when its turn in the stride comes, over the ticks since its
        /// last step. A hull new to the driver, or one that missed more than FarStride ticks, is laid on the water first.
        /// </summary>
        public void Step(World world, List<Ship> ships, int stride = 1)
        {
            var marine = world.Marine;
            if (marine == null) return;
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            steps++;
            stride = Math.Max(1, stride);
            var time = marine.SurfaceTime();
            var blend = marine.Surface.BlendWeight();
            var tick = world.Tick;
            var moved = 0;
            foreach (var ship in ships)
            {
                if (!states.TryGetValue(ship.Id, out var state))
                {
                    state = new State();
                    states[ship.Id] = state;
                }
                var fresh = state.Seen == 0 || tick - state.LastTick > FarStride;
                state.Seen = steps;
                if (!fresh && (tick + ship.Id) % stride != 0 && tick - state.LastTick < stride) continue;
                var load = MarineState.Load(ship);
                if (state.Hull == null || Math.Abs(load - state.Load) > 0.02)
                {
                    state.Hull = HullForm.For(ship.Class, load, marine.Climate);
                    state.Load = load;
                }
                // Wind forces five times a second, ships staggered, as the simulation does (the stride divides the six ticks).
                if (fresh || (ship.Id + tick) % MarineState.ShipUpdateTicks == 0)
                {
                    world.VelocityOf(ship, out var vx, out var vy);
                    state.Forces = SailForces.Compute(state.Hull, ship.Heading, vx, vy, marine.Wind.Sample(ship.X, ship.Y), marine.Climate.AirDensity);
                }
                var elapsed = fresh ? 1 : tick - state.LastTick;
                if (fresh) model.Settle(state.Hull, ref state.Current, marine.Surface, ship.X, ship.Y, ship.Heading, time, blend);
                state.Previous = state.Current;
                state.PreviousBowWater = state.BowWater;
                model.Step(state.Hull, ref state.Current, marine.Surface, ship.X, ship.Y, ship.Heading, time, blend, elapsed * Rules.Dt,
                    state.Forces.HeelMoment, state.Forces.TrimMoment, ship.Speed > MarineState.UnderWay);
                state.BowWater = model.BowHeight;
                if (fresh)
                {
                    state.Previous = state.Current;
                    state.PreviousBowWater = state.BowWater;
                }
                state.PreviousTick = tick - elapsed;
                state.LastTick = tick;
                moved++;
            }
            Ships = ships.Count;
            Stepped = moved;

            // A hull out of sight for ten seconds is forgotten; seen again, it is laid on the water anew.
            stale.Clear();
            foreach (var entry in states) if (steps - entry.Value.Seen > 10 * Rules.TickRate) stale.Add(entry.Key);
            foreach (var id in stale) states.Remove(id);
            Milliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        }

        /// <summary>
        /// The pose at a fraction (0–1) of the tick after <paramref name="tick"/>, between the hull's last two steps, in
        /// HullPose's terms: pitch about X with the bow down positive, roll about Z with the starboard side up positive
        /// (Seakeeping: bow up, port up). A hull stepped every tick is shown a tick late, one stepped every few ticks that
        /// many ticks late, so it moves smoothly between its steps. False for a ship not stepped yet.
        /// </summary>
        public bool TryPose(int shipId, int tick, float fraction, out HullPose pose, out float bowWater, out float heaveRate)
        {
            if (!states.TryGetValue(shipId, out var state) || state.Seen == 0)
            {
                pose = default;
                bowWater = 0f;
                heaveRate = 0f;
                return false;
            }
            var span = Math.Max(1, state.LastTick - state.PreviousTick);
            var t = Mathf.Clamp01((tick - state.LastTick + Mathf.Clamp01(fraction)) / span);
            var a = state.Previous;
            var b = state.Current;
            var heave = (float)(a.Heave + (b.Heave - a.Heave) * t);
            var pitch = (float)(a.Pitch + (b.Pitch - a.Pitch) * t);
            var roll = (float)(a.Roll + (b.Roll - a.Roll) * t);
            pose = new HullPose(heave, -pitch * Mathf.Rad2Deg, -roll * Mathf.Rad2Deg);
            bowWater = (float)(state.PreviousBowWater + (state.BowWater - state.PreviousBowWater) * t);
            heaveRate = (float)b.HeaveRate;
            return true;
        }

        /// <summary>Wind forces on a hull last worked out (for the sails' look), false for a ship not stepped yet.</summary>
        public bool TryForces(int shipId, out WindForces forces)
        {
            forces = default;
            if (!states.TryGetValue(shipId, out var state) || state.Seen == 0) return false;
            forces = state.Forces;
            return true;
        }
    }
}
