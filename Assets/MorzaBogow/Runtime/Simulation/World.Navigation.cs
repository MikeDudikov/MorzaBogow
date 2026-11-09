using System;
using System.Collections.Generic;

namespace MorzaBogow.Simulation
{
    /// <summary>
    /// Routes around land: a visibility graph of points circling each land, one graph per hull size because a
    /// gap open to a cutter can be closed to a merchant. Graphs derive from the land only, so they are not saved;
    /// the ship's next waypoint is, so a loaded game continues exactly as the saved one would.
    /// </summary>
    public sealed partial class World
    {
        /// <summary>Clear water kept while passing land. A start or target closer to the shore shrinks it.</summary>
        const double ShoreMargin = 4;
        const int PointsPerLand = 12;
        const int ReplanTicks = Rules.TickRate;
        const double WaypointReached = 6;
        const double TargetMoved = 10;

        sealed class NavGraph
        {
            public int LandCount;
            public double[] X, Y;
            public int[][] Next;
            public double[][] Cost;
        }

        readonly Dictionary<double, NavGraph> navGraphs = new Dictionary<double, NavGraph>();

        /// <summary>Where to head now on the way to a point. The straight line is used whenever it is clear.</summary>
        (double x, double y) AvoidIslands(Ship ship, double x, double y)
        {
            var hull = ship.Definition.Radius;
            if (IsClear(ship.X, ship.Y, x, y, hull))
            {
                ship.HasWaypoint = false;
                return (x, y);
            }

            var stale = !ship.HasWaypoint || Tick >= ship.ReplanTick
                || Distance(ship.RouteX, ship.RouteY, x, y) > TargetMoved
                || Distance(ship.X, ship.Y, ship.WaypointX, ship.WaypointY) < WaypointReached
                || !IsClear(ship.X, ship.Y, ship.WaypointX, ship.WaypointY, hull);
            if (stale)
            {
                ship.HasWaypoint = TryPlan(ship, x, y, out ship.WaypointX, out ship.WaypointY);
                ship.RouteX = x;
                ship.RouteY = y;
                ship.ReplanTick = Tick + ReplanTicks;
            }
            return ship.HasWaypoint ? (ship.WaypointX, ship.WaypointY) : (x, y);
        }

        /// <summary>
        /// True when a hull can sail the segment. Land already within the margin at either end only blocks the
        /// part that would come closer, so a ship can leave a shore or come alongside a berth.
        /// </summary>
        bool IsClear(double ax, double ay, double bx, double by, double hull)
        {
            var dx = bx - ax;
            var dy = by - ay;
            var length2 = dx * dx + dy * dy;
            foreach (var island in Islands)
            {
                var radius = island.Radius + hull + ShoreMargin;
                radius = Math.Min(radius, Distance(ax, ay, island.X, island.Y) - 0.1);
                radius = Math.Min(radius, Distance(bx, by, island.X, island.Y) - 0.1);
                if (radius <= 0) continue;

                var along = length2 > 1e-9 ? Math.Max(0, Math.Min(1, ((island.X - ax) * dx + (island.Y - ay) * dy) / length2)) : 0;
                if (Distance(ax + along * dx, ay + along * dy, island.X, island.Y) < radius) return false;
            }
            return true;
        }

        NavGraph GraphFor(double hull)
        {
            if (navGraphs.TryGetValue(hull, out var graph) && graph.LandCount == Islands.Count) return graph;

            var xs = new List<double>();
            var ys = new List<double>();
            foreach (var island in Islands)
            {
                var ring = (island.Radius + hull + ShoreMargin) * 1.04 + 0.5;
                for (var k = 0; k < PointsPerLand; k++)
                {
                    var angle = 2 * Math.PI * k / PointsPerLand;
                    var px = island.X + Math.Cos(angle) * ring;
                    var py = island.Y + Math.Sin(angle) * ring;
                    var free = true;
                    foreach (var other in Islands)
                    {
                        if (Distance(px, py, other.X, other.Y) < other.Radius + hull + ShoreMargin) { free = false; break; }
                    }
                    if (!free) continue;
                    xs.Add(px);
                    ys.Add(py);
                }
            }

            var count = xs.Count;
            var next = new List<int>[count];
            var cost = new List<double>[count];
            for (var i = 0; i < count; i++) { next[i] = new List<int>(); cost[i] = new List<double>(); }
            for (var i = 0; i < count; i++)
            {
                for (var j = i + 1; j < count; j++)
                {
                    if (!IsClear(xs[i], ys[i], xs[j], ys[j], hull)) continue;
                    var length = Distance(xs[i], ys[i], xs[j], ys[j]);
                    next[i].Add(j);
                    cost[i].Add(length);
                    next[j].Add(i);
                    cost[j].Add(length);
                }
            }

            graph = new NavGraph { LandCount = Islands.Count, X = xs.ToArray(), Y = ys.ToArray(), Next = new int[count][], Cost = new double[count][] };
            for (var i = 0; i < count; i++)
            {
                graph.Next[i] = next[i].ToArray();
                graph.Cost[i] = cost[i].ToArray();
            }
            navGraphs[hull] = graph;
            return graph;
        }

        /// <summary>Shortest route by Dijkstra with ties settled by point index, so the result is deterministic.</summary>
        bool TryPlan(Ship ship, double x, double y, out double waypointX, out double waypointY)
        {
            waypointX = x;
            waypointY = y;
            var hull = ship.Definition.Radius;
            var graph = GraphFor(hull);
            var count = graph.X.Length;
            if (count == 0) return false;

            var distance = new double[count];
            var first = new int[count];
            var done = new bool[count];
            for (var i = 0; i < count; i++)
            {
                first[i] = -1;
                distance[i] = double.MaxValue;
                if (!IsClear(ship.X, ship.Y, graph.X[i], graph.Y[i], hull)) continue;
                distance[i] = Distance(ship.X, ship.Y, graph.X[i], graph.Y[i]);
                first[i] = i;
            }

            var best = double.MaxValue;
            var bestFirst = -1;
            while (true)
            {
                var current = -1;
                var shortest = double.MaxValue;
                for (var i = 0; i < count; i++)
                {
                    if (!done[i] && distance[i] < shortest) { shortest = distance[i]; current = i; }
                }
                if (current < 0 || shortest >= best) break;
                done[current] = true;

                if (IsClear(graph.X[current], graph.Y[current], x, y, hull))
                {
                    var total = shortest + Distance(graph.X[current], graph.Y[current], x, y);
                    if (total < best) { best = total; bestFirst = first[current]; }
                }

                var edges = graph.Next[current];
                var costs = graph.Cost[current];
                for (var e = 0; e < edges.Length; e++)
                {
                    var through = shortest + costs[e];
                    if (through >= distance[edges[e]]) continue;
                    distance[edges[e]] = through;
                    first[edges[e]] = first[current];
                }
            }

            if (bestFirst < 0) return false;
            waypointX = graph.X[bestFirst];
            waypointY = graph.Y[bestFirst];
            return true;
        }
    }
}
