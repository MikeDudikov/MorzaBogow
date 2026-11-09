using System;

namespace MorzaBogow.Simulation
{
    public enum FogState { Unexplored, Explored, Visible }

    /// <summary>
    /// Logic fog of war on a fixed grid (section 4.4): unexplored, explored but not observed, and currently
    /// observed. Exploration is simulation state and is saved. Current sight is always derived from units.
    /// </summary>
    public sealed partial class World
    {
        // Large enough for the longest scenario's map (MatchProfile.Spread 1.6): 2.6 km square.
        public const int FogCells = 104;
        public const double FogCellSize = 25;
        public const double FogOrigin = -1300;

        /// <summary>One bit per faction for each cell.</summary>
        public byte[] Explored = new byte[FogCells * FogCells];

        static int CellOf(double position) => (int)Math.Floor((position - FogOrigin) / FogCellSize);

        public static double CellCenter(int cell) => FogOrigin + (cell + 0.5) * FogCellSize;

        public void RefreshExplored()
        {
            foreach (var ship in Ships) Stamp(ship.X, ship.Y, ship.Definition.Vision, ship.Owner);
            foreach (var settlement in Settlements)
            {
                if (!settlement.Destroyed) Stamp(settlement.X, settlement.Y, Rules.SettlementVision, settlement.Owner);
            }
        }

        void Stamp(double x, double y, double range, int faction)
        {
            var bit = (byte)(1 << faction);
            var reach = (int)Math.Ceiling(range / FogCellSize);
            var centerX = CellOf(x);
            var centerY = CellOf(y);
            for (var j = Math.Max(0, centerY - reach); j <= Math.Min(FogCells - 1, centerY + reach); j++)
            {
                for (var i = Math.Max(0, centerX - reach); i <= Math.Min(FogCells - 1, centerX + reach); i++)
                {
                    if (Distance2(CellCenter(i), CellCenter(j), x, y) <= range * range) Explored[j * FogCells + i] |= bit;
                }
            }
        }

        public FogState GetFog(int faction, int i, int j)
        {
            if (CanSee(faction, CellCenter(i), CellCenter(j))) return FogState.Visible;
            return (Explored[j * FogCells + i] & (1 << faction)) != 0 ? FogState.Explored : FogState.Unexplored;
        }

        /// <summary>Fills a grid of current sight for one faction, cheaper than asking cell by cell.</summary>
        public void FillVisible(int faction, bool[] visible)
        {
            Array.Clear(visible, 0, visible.Length);
            foreach (var ship in Ships)
            {
                if (ship.Owner == faction) Mark(ship.X, ship.Y, ship.Definition.Vision, visible);
            }
            foreach (var settlement in Settlements)
            {
                if (settlement.Owner == faction && !settlement.Destroyed) Mark(settlement.X, settlement.Y, Rules.SettlementVision, visible);
            }
        }

        static void Mark(double x, double y, double range, bool[] visible)
        {
            var reach = (int)Math.Ceiling(range / FogCellSize);
            var centerX = CellOf(x);
            var centerY = CellOf(y);
            for (var j = Math.Max(0, centerY - reach); j <= Math.Min(FogCells - 1, centerY + reach); j++)
            {
                for (var i = Math.Max(0, centerX - reach); i <= Math.Min(FogCells - 1, centerX + reach); i++)
                {
                    if (Distance2(CellCenter(i), CellCenter(j), x, y) <= range * range) visible[j * FogCells + i] = true;
                }
            }
        }
    }
}
