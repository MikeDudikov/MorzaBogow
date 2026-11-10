using System;

namespace MorzaBogow.Simulation
{
    /// <summary>
    /// The wind and the sea of a match (docs/MORZE_I_WIATR.md). They step with the world, on its clock, and are saved
    /// with it. Without EnableMarine the world runs exactly as before; with it but WindAffectsMovement off (the
    /// default), ships still move exactly as before: the sea is there for the presentation to read.
    /// </summary>
    public sealed partial class World
    {
        public MarineState Marine;

        /// <summary>Turns the wind and the sea on at the start of a match, after the map is laid out.</summary>
        public MarineState EnableMarine(MarineClimate climate, ulong seed, bool windAffectsMovement = false, double settleSeconds = 300)
        {
            Marine = new MarineState(this, climate, seed, windAffectsMovement);
            Marine.Initialise(settleSeconds);
            return Marine;
        }

        void StepMarine() => Marine?.Tick();

        /// <summary>Whether the wind and the waves move ships in this match.</summary>
        bool WindMoves => Marine != null && Marine.WindAffectsMovement;

        /// <summary>
        /// A ship's velocity over the ground: along its heading, plus leeway and the surface drift when the wind moves
        /// ships and the ship is under way.
        /// </summary>
        public void VelocityOf(Ship ship, out double vx, out double vy)
        {
            vx = Math.Cos(ship.Heading) * ship.Speed;
            vy = Math.Sin(ship.Heading) * ship.Speed;
            if (!WindMoves || ship.Speed < MarineState.UnderWay) return;
            vx += ship.DriftX;
            vy += ship.DriftY;
        }

        /// <summary>Leeway and drift of a ship under way when the wind moves ships; nothing otherwise.</summary>
        void Drift(Ship ship)
        {
            if (!WindMoves || ship.Speed < MarineState.UnderWay) return;
            ship.X += ship.DriftX * Rules.Dt;
            ship.Y += ship.DriftY * Rules.Dt;
        }
    }
}
