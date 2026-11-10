using System;
using System.Collections.Generic;
using MorzaBogow.Simulation;
using UnityEngine;

namespace MorzaBogow.Presentation
{
    /// <summary>
    /// Heave, roll and pitch of the ships the player can see, from the simulation's sea and wind: Seakeeping (buoyancy
    /// over ten stations on each side, added mass, damping) with the heel and trim of SailForces, the same functions the
    /// simulation uses when the wind moves ships. Stepped once per simulation tick, so it stands still in a pause and
    /// keeps pace with the game speed; shown between the last two ticks. Presentation only: the simulation never reads
    /// it, a loaded or newly seen hull starts level and settles within seconds. It replaces the springs that followed
    /// HDRP's heights, so the waves move each hull once.
    /// </summary>
    public sealed class SeakeepingDriver
    {
        sealed class State
        {
            public ShipMotion Previous, Current;
            public HullForm Hull;
            public double Load = -1;
            public WindForces Forces;
            public double BowWater, PreviousBowWater;
            public int Seen;
        }

        readonly Dictionary<int, State> states = new Dictionary<int, State>();
        readonly List<int> stale = new List<int>();
        readonly Seakeeping model = new Seakeeping();
        int steps;

        /// <summary>Time of the last step, milliseconds, and how many hulls it moved.</summary>
        public double Milliseconds { get; private set; }
        public int Ships { get; private set; }

        public void Step(World world, List<Ship> ships)
        {
            var marine = world.Marine;
            if (marine == null) return;
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            steps++;
            var time = marine.SurfaceTime();
            var blend = marine.Surface.BlendWeight();
            foreach (var ship in ships)
            {
                if (!states.TryGetValue(ship.Id, out var state))
                {
                    state = new State();
                    states[ship.Id] = state;
                }
                var load = MarineState.Load(ship);
                if (state.Hull == null || Math.Abs(load - state.Load) > 0.02)
                {
                    state.Hull = HullForm.For(ship.Class, load, marine.Climate);
                    state.Load = load;
                }
                // Wind forces five times a second, ships staggered, as the simulation does.
                var fresh = state.Seen == 0;
                if (fresh || (ship.Id + world.Tick) % MarineState.ShipUpdateTicks == 0)
                {
                    world.VelocityOf(ship, out var vx, out var vy);
                    state.Forces = SailForces.Compute(state.Hull, ship.Heading, vx, vy, marine.Wind.Sample(ship.X, ship.Y), marine.Climate.AirDensity);
                }
                if (fresh) model.Settle(state.Hull, ref state.Current, marine.Surface, ship.X, ship.Y, ship.Heading, time, blend);
                state.Previous = state.Current;
                state.PreviousBowWater = state.BowWater;
                model.Step(state.Hull, ref state.Current, marine.Surface, ship.X, ship.Y, ship.Heading, time, blend, Rules.Dt,
                    state.Forces.HeelMoment, state.Forces.TrimMoment, ship.Speed > MarineState.UnderWay);
                state.BowWater = model.BowHeight;
                if (fresh)
                {
                    state.Previous = state.Current;
                    state.PreviousBowWater = state.BowWater;
                }
                state.Seen = steps;
            }
            Ships = ships.Count;

            // A hull out of sight for ten seconds starts from rest when it is seen again.
            stale.Clear();
            foreach (var entry in states) if (steps - entry.Value.Seen > 10 * Rules.TickRate) stale.Add(entry.Key);
            foreach (var id in stale) states.Remove(id);
            Milliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        }

        /// <summary>
        /// The pose between the last two ticks (fraction 0–1 of the present one) in HullPose's terms: pitch about X with the
        /// bow down positive, roll about Z with the starboard side up positive (Seakeeping: bow up, port up). False for a
        /// ship not stepped yet.
        /// </summary>
        public bool TryPose(int shipId, float fraction, out HullPose pose, out float bowWater, out float heaveRate)
        {
            if (!states.TryGetValue(shipId, out var state) || state.Seen == 0)
            {
                pose = default;
                bowWater = 0f;
                heaveRate = 0f;
                return false;
            }
            var t = Mathf.Clamp01(fraction);
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
