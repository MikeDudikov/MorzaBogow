using System;
using System.Collections.Generic;

namespace MorzaBogow.Simulation
{
    /// <summary>
    /// Geometry of the wave model: a regular grid of square cells, the depth each cell's waves feel, and how much of a
    /// wave's energy flux entering a cell along each direction gets through the land inside it (sub-grid obstacles, as
    /// the unresolved obstacles of WAVEWATCH III, Tolman 2003). The match grid is 52 × 52 cells of 50 m from −1300 m,
    /// aligned with the fog grid. Built from the map, never saved.
    /// </summary>
    public sealed class WaveGrid
    {
        /// <summary>Land widened to the 2 m depth line (Seabed: 2 m lies about 5 m off the waterline): waves break there.</summary>
        public const double ShoreOffset = 5.0;

        const int MaskSide = 20;

        public readonly int NX, NY, Cells;
        public readonly double CellSize, OriginX, OriginY;

        /// <summary>Depth for each cell's dispersion, bottom friction and depth breaking, metres.</summary>
        public readonly double[] Depth;

        /// <summary>[direction × Cells + cell]: share of the flux entering the cell along that direction that passes its land.</summary>
        public readonly float[] Transparency;

        /// <summary>Size of the largest obstacle in the cell (island diameter), metres; long waves bend around small ones.</summary>
        public readonly float[] ObstacleSize;

        public readonly float[] LandFraction;
        public readonly bool[] Land;

        /// <summary>Opposite edges joined (tests: a channel uniform across the wind, an endless sea).</summary>
        public bool PeriodicX, PeriodicY;

        public WaveGrid(int nx, int ny, double cellSize, double originX, double originY, double depth)
        {
            NX = nx; NY = ny; Cells = nx * ny;
            CellSize = cellSize; OriginX = originX; OriginY = originY;
            Depth = new double[Cells];
            Transparency = new float[Cells * WaveSpectrum.Directions];
            ObstacleSize = new float[Cells];
            LandFraction = new float[Cells];
            Land = new bool[Cells];
            for (var c = 0; c < Cells; c++) Depth[c] = depth;
            for (var n = 0; n < Transparency.Length; n++) Transparency[n] = 1;
        }

        public double CenterX(int cell) => OriginX + (cell % NX + 0.5) * CellSize;
        public double CenterY(int cell) => OriginY + (cell / NX + 0.5) * CellSize;

        /// <summary>The cell containing a point, or −1 outside the grid.</summary>
        public int CellAt(double x, double y)
        {
            var i = (int)Math.Floor((x - OriginX) / CellSize);
            var j = (int)Math.Floor((y - OriginY) / CellSize);
            return i < 0 || j < 0 || i >= NX || j >= NY ? -1 : j * NX + i;
        }

        /// <summary>Makes a cell partly opaque to every direction (tests and hand-made scenes).</summary>
        public void SetObstacle(int cell, double transparency, double size)
        {
            for (var j = 0; j < WaveSpectrum.Directions; j++) Transparency[j * Cells + cell] = (float)transparency;
            ObstacleSize[cell] = (float)size;
            LandFraction[cell] = (float)(1 - transparency);
            Land[cell] = transparency <= 0;
        }

        /// <summary>The match grid: 52 × 52 cells of 50 m from −1300 m, 13 m deep (the Seabed beyond the shelf).</summary>
        public static WaveGrid ForMap(IReadOnlyList<Island> islands) =>
            FromIslands(islands, 52, 52, 50, -1300, -1300, Seabed.Depth[Seabed.Depth.Length - 1]);

        /// <summary>
        /// A grid over a map. Every water cell feels the open depth: the shelf (44 m) is narrower than a cell, so the
        /// shore acts through the transparency of the land and the depth-limited surf the presentation draws.
        /// </summary>
        public static WaveGrid FromIslands(IReadOnlyList<Island> islands, int nx, int ny, double cellSize, double originX, double originY, double depth)
        {
            var grid = new WaveGrid(nx, ny, cellSize, originX, originY, depth);
            if (islands == null || islands.Count == 0) return grid;
            var masks = new Dictionary<int, bool[]>();
            var step = cellSize / MaskSide;
            foreach (var island in islands)
            {
                var reach = Reach(island) + ShoreOffset;
                var i0 = Math.Max(0, (int)Math.Floor((island.X - reach - originX) / cellSize));
                var i1 = Math.Min(nx - 1, (int)Math.Floor((island.X + reach - originX) / cellSize));
                var j0 = Math.Max(0, (int)Math.Floor((island.Y - reach - originY) / cellSize));
                var j1 = Math.Min(ny - 1, (int)Math.Floor((island.Y + reach - originY) / cellSize));
                for (var j = j0; j <= j1; j++)
                    for (var i = i0; i <= i1; i++)
                    {
                        var cell = j * nx + i;
                        if (!masks.TryGetValue(cell, out var mask)) { mask = new bool[MaskSide * MaskSide]; masks[cell] = mask; }
                        var touched = false;
                        for (var my = 0; my < MaskSide; my++)
                            for (var mx = 0; mx < MaskSide; mx++)
                            {
                                var px = originX + i * cellSize + (mx + 0.5) * step;
                                var py = originY + j * cellSize + (my + 0.5) * step;
                                if (!Blocks(island, px, py)) continue;
                                mask[my * MaskSide + mx] = true;
                                touched = true;
                            }
                        if (touched) grid.ObstacleSize[cell] = (float)Math.Max(grid.ObstacleSize[cell], 2 * Reach(island));
                    }
            }

            foreach (var entry in masks)
            {
                var cell = entry.Key;
                var mask = entry.Value;
                var blocked = 0;
                foreach (var value in mask) if (value) blocked++;
                if (blocked == 0) continue;
                grid.LandFraction[cell] = blocked / (float)mask.Length;
                grid.Land[cell] = grid.LandFraction[cell] >= 0.9f;
                for (var d = 0; d < WaveSpectrum.Directions; d++)
                    grid.Transparency[d * grid.Cells + cell] = (float)RayTransparency(mask, WaveSpectrum.DirectionAngle[d], cellSize);
            }
            return grid;
        }

        static double Reach(Island island) => island.Outline == null ? island.Radius : LandShape.MaxRadius(island.Outline);

        /// <summary>Waterline radius toward a direction, linear between the outline's samples.</summary>
        public static double RadiusToward(Island island, double angle)
        {
            if (island.Outline == null) return island.Radius;
            var t = angle / (2 * Math.PI) * LandShape.Samples;
            t -= Math.Floor(t / LandShape.Samples) * LandShape.Samples;
            var i0 = (int)t % LandShape.Samples;
            var i1 = (i0 + 1) % LandShape.Samples;
            var w = t - Math.Floor(t);
            return island.Outline[i0] * (1 - w) + island.Outline[i1] * w;
        }

        static bool Blocks(Island island, double x, double y)
        {
            var dx = x - island.X;
            var dy = y - island.Y;
            return Math.Sqrt(dx * dx + dy * dy) < RadiusToward(island, Math.Atan2(dy, dx)) + ShoreOffset;
        }

        /// <summary>Share of parallel rays (three angles across the bin, eight offsets across the cell) that cross no land.</summary>
        static double RayTransparency(bool[] mask, double direction, double cellSize)
        {
            var half = cellSize / 2;
            var open = 0;
            var rays = 0;
            for (var a = -1; a <= 1; a++)
            {
                var angle = direction + a * WaveSpectrum.DirectionWidth / 4;
                var ex = Math.Cos(angle);
                var ey = Math.Sin(angle);
                var width = cellSize * (Math.Abs(ex) + Math.Abs(ey));
                for (var o = 0; o < 8; o++)
                {
                    var offset = ((o + 0.5) / 8 - 0.5) * width;
                    var ox = -ey * offset;
                    var oy = ex * offset;
                    var hit = false;
                    var any = false;
                    for (var s = -cellSize; s <= cellSize && !hit; s += cellSize / (2 * MaskSide))
                    {
                        var px = ox + ex * s;
                        var py = oy + ey * s;
                        if (px < -half || px >= half || py < -half || py >= half) continue;
                        any = true;
                        var mx = Math.Min(MaskSide - 1, (int)((px + half) / cellSize * MaskSide));
                        var my = Math.Min(MaskSide - 1, (int)((py + half) / cellSize * MaskSide));
                        if (mask[my * MaskSide + mx]) hit = true;
                    }
                    if (!any) continue;
                    rays++;
                    if (!hit) open++;
                }
            }
            return rays == 0 ? 1 : open / (double)rays;
        }
    }
}
