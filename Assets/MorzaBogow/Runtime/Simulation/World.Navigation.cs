using System;
using System.Collections.Generic;

namespace MorzaBogow.Simulation
{
    /// <summary>
    /// Routes around land: a visibility graph of points along each waterline, kept off the shore by the ship's
    /// clearance (half its length plus the shallows its draft cannot cross). One graph per clearance, because a
    /// gap open to an empty cutter can be closed to a loaded merchant. Graphs derive from the land only, so they
    /// are not saved; the ship's next waypoint is, so a loaded game continues exactly as the saved one would.
    /// </summary>
    public sealed partial class World
    {
        /// <summary>Extra water kept while passing land. A start or target closer to the shore shrinks it.</summary>
        const double ShoreMargin = 4;
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

        /// <summary>Depth of the hull below the water: the empty draft, deeper as the hold fills.</summary>
        public static double DraftOf(Ship ship)
        {
            var definition = ship.Definition;
            var capacity = definition.Capacity * Rules.Centi;
            var load = capacity > 0 ? Math.Min(1.0, (double)(ship.Cargo + ship.HoldWood + ship.HoldSilver) / capacity) : 0;
            return definition.Draft * (1 + Rules.DraftPerLoad * load);
        }

        /// <summary>Distance the ship's centre keeps from the waterline: half its length plus the shallows too shallow for it.</summary>
        public static double LandClearance(Ship ship) => ship.Definition.Radius + Seabed.ShoalWidth(DraftOf(ship));

        /// <summary>Where to head now on the way to a point. The straight line is used whenever it is clear.</summary>
        (double x, double y) AvoidIslands(Ship ship, double x, double y)
        {
            var clearance = Math.Round((LandClearance(ship) + ShoreMargin) * 2) / 2;
            if (IsClear(ship.X, ship.Y, x, y, clearance))
            {
                ship.HasWaypoint = false;
                return (x, y);
            }

            var stale = !ship.HasWaypoint || Tick >= ship.ReplanTick
                || Distance(ship.RouteX, ship.RouteY, x, y) > TargetMoved
                || Distance(ship.X, ship.Y, ship.WaypointX, ship.WaypointY) < WaypointReached
                || !IsClear(ship.X, ship.Y, ship.WaypointX, ship.WaypointY, clearance);
            if (stale)
            {
                ship.HasWaypoint = TryPlan(ship, x, y, clearance, out ship.WaypointX, out ship.WaypointY);
                ship.RouteX = x;
                ship.RouteY = y;
                ship.ReplanTick = Tick + ReplanTicks;
            }
            return ship.HasWaypoint ? (ship.WaypointX, ship.WaypointY) : (x, y);
        }

        /// <summary>
        /// True when the segment keeps the clearance from every waterline. Land already closer at either end only
        /// blocks a way that would come closer still, so a ship can leave a shore or come alongside a berth.
        /// </summary>
        bool IsClear(double ax, double ay, double bx, double by, double clearance)
        {
            foreach (var island in Islands)
            {
                if (LandShape.PointSegmentDistance(island.X, island.Y, ax, ay, bx, by) > island.Radius + clearance) continue;
                // Most segments pass well clear; only a near one needs the distances of its ends.
                if (!LandShape.SegmentNear(island, ax, ay, bx, by, clearance)) continue;
                var allowed = Math.Min(clearance, Math.Min(LandShape.SignedDistance(island, ax, ay), LandShape.SignedDistance(island, bx, by)) - 0.1);
                if (allowed <= 0) continue;
                if (allowed >= clearance || LandShape.SegmentNear(island, ax, ay, bx, by, allowed)) return false;
            }
            return true;
        }

        double ClearanceAt(double x, double y)
        {
            var best = double.MaxValue;
            foreach (var island in Islands)
            {
                if (Distance(x, y, island.X, island.Y) - island.Radius > best) continue;
                best = Math.Min(best, LandShape.SignedDistance(island, x, y));
            }
            return best;
        }

        NavGraph GraphFor(double clearance)
        {
            if (navGraphs.TryGetValue(clearance, out var graph) && graph.LandCount == Islands.Count) return graph;

            var xs = new List<double>();
            var ys = new List<double>();
            var keep = clearance * 1.04 + 1.5;
            foreach (var island in Islands)
            {
                var count = Math.Max(8, Math.Min(32, (int)(2 * Math.PI * island.Radius / 12)));
                for (var k = 0; k < count; k++)
                {
                    var angle = 2 * Math.PI * k / count;
                    var reach = island.Outline == null ? island.Radius : island.Outline[k * LandShape.Samples / count];
                    var px = island.X + Math.Cos(angle) * (reach + keep);
                    var py = island.Y + Math.Sin(angle) * (reach + keep);

                    // Out along the nearest shore's normal until the point keeps the clearance from this land.
                    for (var pass = 0; pass < 3; pass++)
                    {
                        var distance = LandShape.SignedDistance(island, px, py, out var nx, out var ny);
                        if (distance >= keep) break;
                        px += nx * (keep - distance);
                        py += ny * (keep - distance);
                    }
                    if (ClearanceAt(px, py) < clearance) continue;
                    xs.Add(px);
                    ys.Add(py);
                }
            }

            var total = xs.Count;
            var next = new List<int>[total];
            var cost = new List<double>[total];
            for (var i = 0; i < total; i++) { next[i] = new List<int>(); cost[i] = new List<double>(); }
            for (var i = 0; i < total; i++)
            {
                for (var j = i + 1; j < total; j++)
                {
                    if (!IsClear(xs[i], ys[i], xs[j], ys[j], clearance)) continue;
                    var length = Distance(xs[i], ys[i], xs[j], ys[j]);
                    next[i].Add(j);
                    cost[i].Add(length);
                    next[j].Add(i);
                    cost[j].Add(length);
                }
            }

            graph = new NavGraph { LandCount = Islands.Count, X = xs.ToArray(), Y = ys.ToArray(), Next = new int[total][], Cost = new double[total][] };
            for (var i = 0; i < total; i++)
            {
                graph.Next[i] = next[i].ToArray();
                graph.Cost[i] = cost[i].ToArray();
            }
            navGraphs[clearance] = graph;
            return graph;
        }

        /// <summary>
        /// Shortest route by A* (straight-line distance to the target as the estimate). The legs from the ship are
        /// only checked for land when a point is taken from the queue, so a plan looks at few points. Ties are
        /// settled by point index, so the result is deterministic.
        /// </summary>
        bool TryPlan(Ship ship, double x, double y, double clearance, out double waypointX, out double waypointY)
        {
            waypointX = x;
            waypointY = y;
            var graph = GraphFor(clearance);
            var count = graph.X.Length;
            if (count == 0) return false;

            var reached = new double[count];
            var first = new int[count];
            var done = new bool[count];
            var queue = new RouteQueue(count * 2);
            for (var i = 0; i < count; i++)
            {
                reached[i] = double.MaxValue;
                first[i] = -1;
                var direct = Distance(ship.X, ship.Y, graph.X[i], graph.Y[i]);
                queue.Push(direct + Distance(graph.X[i], graph.Y[i], x, y), direct, i, true);
            }

            var best = double.MaxValue;
            var bestFirst = -1;
            while (queue.TryPop(out var estimate, out var cost, out var current, out var fromShip))
            {
                if (estimate >= best) break;
                if (done[current]) continue;
                if (fromShip)
                {
                    // A leg from the ship is only real when the water between is clear.
                    if (!IsClear(ship.X, ship.Y, graph.X[current], graph.Y[current], clearance)) continue;
                    first[current] = current;
                }
                else if (cost != reached[current]) continue;
                done[current] = true;

                var toTarget = Distance(graph.X[current], graph.Y[current], x, y);
                if (cost + toTarget < best && IsClear(graph.X[current], graph.Y[current], x, y, clearance))
                {
                    best = cost + toTarget;
                    bestFirst = first[current];
                }

                var edges = graph.Next[current];
                var costs = graph.Cost[current];
                for (var e = 0; e < edges.Length; e++)
                {
                    var next = edges[e];
                    var through = cost + costs[e];
                    if (done[next] || through >= reached[next]) continue;
                    reached[next] = through;
                    first[next] = first[current];
                    queue.Push(through + Distance(graph.X[next], graph.Y[next], x, y), through, next, false);
                }
            }

            if (bestFirst < 0) return false;
            waypointX = graph.X[bestFirst];
            waypointY = graph.Y[bestFirst];
            return true;
        }

        /// <summary>Binary min-heap ordered by estimate, then point index, then legs from the graph before legs from the ship.</summary>
        struct RouteQueue
        {
            double[] estimates, costs;
            int[] nodes;
            bool[] fromShip;
            int size;

            public RouteQueue(int capacity)
            {
                estimates = new double[capacity];
                costs = new double[capacity];
                nodes = new int[capacity];
                fromShip = new bool[capacity];
                size = 0;
            }

            bool Less(int a, int b)
            {
                if (estimates[a] != estimates[b]) return estimates[a] < estimates[b];
                if (nodes[a] != nodes[b]) return nodes[a] < nodes[b];
                return !fromShip[a] && fromShip[b];
            }

            void Swap(int a, int b)
            {
                (estimates[a], estimates[b]) = (estimates[b], estimates[a]);
                (costs[a], costs[b]) = (costs[b], costs[a]);
                (nodes[a], nodes[b]) = (nodes[b], nodes[a]);
                (fromShip[a], fromShip[b]) = (fromShip[b], fromShip[a]);
            }

            public void Push(double estimate, double cost, int node, bool ship)
            {
                if (size == nodes.Length)
                {
                    Array.Resize(ref estimates, size * 2);
                    Array.Resize(ref costs, size * 2);
                    Array.Resize(ref nodes, size * 2);
                    Array.Resize(ref fromShip, size * 2);
                }
                var at = size++;
                estimates[at] = estimate; costs[at] = cost; nodes[at] = node; fromShip[at] = ship;
                while (at > 0)
                {
                    var parent = (at - 1) / 2;
                    if (!Less(at, parent)) break;
                    Swap(at, parent);
                    at = parent;
                }
            }

            public bool TryPop(out double estimate, out double cost, out int node, out bool ship)
            {
                estimate = cost = 0; node = -1; ship = false;
                if (size == 0) return false;
                estimate = estimates[0]; cost = costs[0]; node = nodes[0]; ship = fromShip[0];
                size--;
                if (size > 0)
                {
                    estimates[0] = estimates[size]; costs[0] = costs[size]; nodes[0] = nodes[size]; fromShip[0] = fromShip[size];
                    var at = 0;
                    while (true)
                    {
                        var left = at * 2 + 1;
                        if (left >= size) break;
                        var child = left + 1 < size && Less(left + 1, left) ? left + 1 : left;
                        if (!Less(child, at)) break;
                        Swap(child, at);
                        at = child;
                    }
                }
                return true;
            }
        }

        // ---------- Contact with land and with other hulls ----------

        /// <summary>Keeps the ship off land and off water too shallow for its draft, sliding along the shore.</summary>
        void ResolveLand(Ship ship)
        {
            var need = LandClearance(ship);
            foreach (var island in Islands)
            {
                if (Distance(ship.X, ship.Y, island.X, island.Y) > island.Radius + need + 1) continue;
                var distance = LandShape.SignedDistance(island, ship.X, ship.Y, out var nx, out var ny);
                if (distance >= need) continue;
                ship.X += nx * (need - distance);
                ship.Y += ny * (need - distance);

                // Only heading into the land costs speed; a ship sliding along the shore keeps moving.
                var inward = -(Math.Cos(ship.Heading) * nx + Math.Sin(ship.Heading) * ny);
                if (inward > 0.5) ship.Speed *= 0.5;
            }
        }

        static void HullSegment(Ship ship, out double ax, out double ay, out double bx, out double by)
        {
            var definition = ship.Definition;
            var half = Math.Max(0, definition.Radius - definition.Beam / 2);
            var cos = Math.Cos(ship.Heading);
            var sin = Math.Sin(ship.Heading);
            ax = ship.X + cos * half; ay = ship.Y + sin * half;
            bx = ship.X - cos * half; by = ship.Y - sin * half;
        }

        /// <summary>
        /// Hulls are capsules along their heading, as wide as their beam. Overlaps are pushed apart, the lighter hull
        /// moving more, and the speed toward each other is lost. Pairs are handled in id order, so it is deterministic.
        /// </summary>
        void ResolveShipContacts()
        {
            for (var pass = 0; pass < 2; pass++)
            {
                for (var i = 0; i < Ships.Count; i++)
                {
                    var a = Ships[i];
                    for (var j = i + 1; j < Ships.Count; j++)
                    {
                        var b = Ships[j];
                        if (Distance(a.X, a.Y, b.X, b.Y) > a.Definition.Radius + b.Definition.Radius + HullSpacing) continue;
                        HullSegment(a, out var a1x, out var a1y, out var a2x, out var a2y);
                        HullSegment(b, out var b1x, out var b1y, out var b2x, out var b2y);
                        var gap = LandShape.SegmentSegmentDistance(a1x, a1y, a2x, a2y, b1x, b1y, b2x, b2y, out var cax, out var cay, out var cbx, out var cby);
                        // Hulls keep a fender's width apart; only ships grappled in a boarding lie side by side.
                        var spacing = Grappled(a, b) ? 0 : HullSpacing;
                        var overlap = (a.Definition.Beam + b.Definition.Beam) / 2 + spacing - gap;
                        if (overlap <= 0) continue;

                        double nx, ny;
                        if (gap > 1e-6) { nx = (cax - cbx) / gap; ny = (cay - cby) / gap; }
                        else
                        {
                            var d = Distance(a.X, a.Y, b.X, b.Y);
                            if (d > 1e-6) { nx = (a.X - b.X) / d; ny = (a.Y - b.Y) / d; }
                            else { nx = -Math.Sin(a.Heading); ny = Math.Cos(a.Heading); }
                        }

                        var total = a.Definition.Mass + b.Definition.Mass;
                        var shareA = b.Definition.Mass / total;
                        var shareB = a.Definition.Mass / total;
                        a.X += nx * overlap * shareA; a.Y += ny * overlap * shareA;
                        b.X -= nx * overlap * shareB; b.Y -= ny * overlap * shareB;

                        // Speed driving into the other hull is lost; sliding past it is kept.
                        var intoA = -(Math.Cos(a.Heading) * nx + Math.Sin(a.Heading) * ny);
                        var intoB = Math.Cos(b.Heading) * nx + Math.Sin(b.Heading) * ny;
                        if (intoA > 0.3 && a.Speed > 0) a.Speed *= 1 - 0.5 * intoA;
                        if (intoB > 0.3 && b.Speed > 0) b.Speed *= 1 - 0.5 * intoB;
                    }
                }
                foreach (var ship in Ships) ResolveLand(ship);
            }
        }

        /// <summary>Water kept between two hulls, so moored or passing ships never appear to sit on each other.</summary>
        public const double HullSpacing = 2.0;

        bool Grappled(Ship a, Ship b)
        {
            foreach (var link in Boardings)
            {
                if ((link.AttackerId == a.Id && link.TargetId == b.Id) || (link.AttackerId == b.Id && link.TargetId == a.Id)) return true;
            }
            return false;
        }

        /// <summary>Distance between two hulls' sides; negative while they overlap. Used by tests and tools.</summary>
        public static double HullGap(Ship a, Ship b)
        {
            HullSegment(a, out var a1x, out var a1y, out var a2x, out var a2y);
            HullSegment(b, out var b1x, out var b1y, out var b2x, out var b2y);
            return LandShape.SegmentSegmentDistance(a1x, a1y, a2x, a2y, b1x, b1y, b2x, b2y, out _, out _, out _, out _) - (a.Definition.Beam + b.Definition.Beam) / 2;
        }
    }
}
