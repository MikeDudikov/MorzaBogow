using System;
using System.Collections.Generic;
using System.Linq;

namespace MorzaBogow.Simulation
{
    /// <summary>How a group sent to one point arranges itself there.</summary>
    public enum FormationKind
    {
        /// <summary>Abreast, across the course, at most eight to a line; a larger group forms more lines behind.</summary>
        Line,

        /// <summary>A leader at the point and two arms trailing behind it, as an arrowhead.</summary>
        Wedge,
    }

    /// <summary>
    /// Where each ship of a group goes when the group is sent to one point, so the ships do not crowd onto it.
    /// <list type="bullet">
    /// <item>The course runs from the group's centre to the point; a group already about there keeps its leader's heading.</item>
    /// <item>Ships stand a hull and both banks of oars apart across the course, plus the fender gap (World.HullSpacing),
    /// and a hull's length apart along it, so no two places overlap.</item>
    /// <item>The point moves off the land as the simulation moves any target there, with the room the group's deepest
    /// hull needs; a place that would be on land or in shallows, or too close to another, moves to the nearest free
    /// place in open water.</item>
    /// <item>Ships are first matched to places by how far to the side they stand, then pairs swap places while that
    /// shortens their total way, so no two courses cross. Every step goes in id order, so the same group and point
    /// give the same places whatever order the ships were selected in.</item>
    /// </list>
    /// Pure arithmetic: each place becomes an ordinary move command, so saves and replays keep only the commands.
    /// </summary>
    public static class Formation
    {
        /// <summary>The most ships abreast in one line.</summary>
        public const int PerRank = 8;

        /// <summary>The target of each ship, in the order the ships are given.</summary>
        public static (double X, double Y)[] Targets(World world, IReadOnlyList<Ship> ships, double targetX, double targetY, FormationKind kind)
        {
            var count = ships.Count;
            var targets = new (double X, double Y)[count];
            if (count == 0) return targets;
            if (count == 1)
            {
                targets[0] = (targetX, targetY);
                return targets;
            }

            var byId = Enumerable.Range(0, count).OrderBy(i => ships[i].Id).ToArray();
            double centreX = 0, centreY = 0;
            foreach (var i in byId)
            {
                centreX += ships[i].X;
                centreY += ships[i].Y;
            }
            centreX /= count;
            centreY /= count;

            var across = Spacing(ships);
            var depth = Depth(ships);
            var room = Room(ships);
            (targetX, targetY) = OffShore(world, targetX, targetY, room);

            var forwardX = targetX - centreX;
            var forwardY = targetY - centreY;
            var distance = Math.Sqrt(forwardX * forwardX + forwardY * forwardY);
            if (distance < across)
            {
                var leader = ships[byId[0]];
                forwardX = Math.Cos(leader.Heading);
                forwardY = Math.Sin(leader.Heading);
            }
            else
            {
                forwardX /= distance;
                forwardY /= distance;
            }
            var leftX = -forwardY;
            var leftY = forwardX;

            // Places in the formation's own frame (Side to the left of the course, Back behind the point), in order of
            // preference; each keeps its ideal spot if it is free, else takes the nearest free one.
            (double X, double Y) Spot((double Side, double Back) place) =>
                (targetX + leftX * place.Side - forwardX * place.Back, targetY + leftY * place.Side - forwardY * place.Back);
            var places = new List<(double Side, double Back)>(count);
            foreach (var ideal in Ideal(count, kind, across, depth))
                places.Add(FreePlace(world, ideal, places, across, depth, room, Spot));

            // Matched by side first: the ship furthest to the right takes the place furthest to the right.
            var shipsBySide = byId.OrderBy(i => (ships[i].X - centreX) * leftX + (ships[i].Y - centreY) * leftY).ThenBy(i => ships[i].Id).ToArray();
            var placesBySide = Enumerable.Range(0, count).OrderBy(p => places[p].Side).ThenBy(p => places[p].Back).ToArray();
            var placeOf = new int[count];
            for (var k = 0; k < count; k++) placeOf[shipsBySide[k]] = placesBySide[k];

            // Then pairs swap places while that shortens their total way; when none can, no two courses cross.
            var spots = places.Select(Spot).ToArray();
            double Way(int ship, int place) => World.Distance(ships[ship].X, ships[ship].Y, spots[place].X, spots[place].Y);
            for (var pass = 0; pass < 64; pass++)
            {
                var swapped = false;
                for (var a = 0; a < count; a++)
                for (var b = a + 1; b < count; b++)
                {
                    var first = byId[a];
                    var second = byId[b];
                    if (Way(first, placeOf[second]) + Way(second, placeOf[first]) >= Way(first, placeOf[first]) + Way(second, placeOf[second]) - 1e-9) continue;
                    (placeOf[first], placeOf[second]) = (placeOf[second], placeOf[first]);
                    swapped = true;
                }
                if (!swapped) break;
            }

            for (var i = 0; i < count; i++) targets[i] = spots[placeOf[i]];
            return targets;
        }

        /// <summary>
        /// The room one ship takes across the course: the widest hull of the group with both banks of oars out, and the
        /// fender gap kept between hulls.
        /// </summary>
        public static double Spacing(IReadOnlyList<Ship> ships)
        {
            double width = 0;
            foreach (var ship in ships) width = Math.Max(width, ship.Definition.Beam + 2 * Rules.OarReach(ship.Definition));
            return width + World.HullSpacing;
        }

        /// <summary>The room one ship takes along the course: the longest hull of the group and the fender gap.</summary>
        public static double Depth(IReadOnlyList<Ship> ships)
        {
            double length = 0;
            foreach (var ship in ships) length = Math.Max(length, ship.Definition.Radius * 2);
            return length + World.HullSpacing;
        }

        /// <summary>
        /// The water every place keeps from the land: the clearance the group's deepest hull needs, as the simulation
        /// keeps a target there, and a metre more.
        /// </summary>
        public static double Room(IReadOnlyList<Ship> ships)
        {
            double room = 0;
            foreach (var ship in ships) room = Math.Max(room, World.LandClearance(ship));
            return room + 1;
        }

        /// <summary>
        /// The ideal places in order of preference. A line fills ranks of at most eight abreast, centred, the middle
        /// first, a hull's length behind each other; a wedge puts its first place on the point and the rest in ranks
        /// behind it, alternately left and right, half the longest hull deeper per rank.
        /// </summary>
        static IEnumerable<(double Side, double Back)> Ideal(int count, FormationKind kind, double across, double depth)
        {
            if (kind == FormationKind.Wedge)
            {
                var behind = (depth - World.HullSpacing) * 0.5 + World.HullSpacing;
                for (var i = 0; i < count; i++)
                {
                    var rank = (i + 1) / 2;
                    var side = i % 2 == 1 ? 1 : -1;
                    yield return (side * rank * across, rank * behind);
                }
                yield break;
            }
            for (var start = 0; start < count; start += PerRank)
            {
                var abreast = Math.Min(PerRank, count - start);
                var back = start / PerRank * depth;
                var rank = Enumerable.Range(0, abreast).Select(k => ((k - (abreast - 1) / 2.0) * across, back));
                foreach (var place in rank.OrderBy(place => Math.Abs(place.Item1)).ThenBy(place => place.Item1)) yield return place;
            }
        }

        /// <summary>
        /// The ideal place if it is in open water with room and clear of every place already taken; otherwise the nearest
        /// place that is, searched on a grid of half a ship's room in rings around it. Two places clash when they are
        /// closer than a ship's room both across and along the course.
        /// </summary>
        static (double Side, double Back) FreePlace(World world, (double Side, double Back) ideal, List<(double Side, double Back)> taken,
            double across, double depth, double room, Func<(double Side, double Back), (double X, double Y)> spot)
        {
            bool Free((double Side, double Back) place)
            {
                foreach (var other in taken)
                {
                    if (Math.Abs(other.Side - place.Side) < across - 1e-9 && Math.Abs(other.Back - place.Back) < depth - 1e-9) return false;
                }
                var at = spot(place);
                return Clear(world, at.X, at.Y, room);
            }

            if (Free(ideal)) return ideal;
            for (var ring = 1; ring <= 16; ring++)
            {
                var candidates = new List<(double Side, double Back)>();
                for (var i = -ring; i <= ring; i++)
                for (var j = -ring; j <= ring; j++)
                {
                    if (Math.Max(Math.Abs(i), Math.Abs(j)) != ring) continue;
                    candidates.Add((ideal.Side + i * across * 0.5, ideal.Back + j * depth * 0.5));
                }
                foreach (var candidate in candidates
                    .OrderBy(place => Math.Pow(place.Side - ideal.Side, 2) + Math.Pow(place.Back - ideal.Back, 2))
                    .ThenBy(place => place.Back).ThenBy(place => place.Side))
                {
                    if (Free(candidate)) return candidate;
                }
            }
            return ideal;
        }

        /// <summary>True when the point keeps at least <paramref name="room"/> from every waterline.</summary>
        public static bool Clear(World world, double x, double y, double room)
        {
            if (world == null) return true;
            foreach (var island in world.Islands)
            {
                if (World.Distance(x, y, island.X, island.Y) > island.Radius + room) continue;
                if (LandShape.SignedDistance(island, x, y) < room) return false;
            }
            return true;
        }

        /// <summary>
        /// A point closer to land than <paramref name="room"/> moves out along the shore's normal, as the simulation moves
        /// a target (World.MoveTargetToWater), a few times over for points between two shores.
        /// </summary>
        public static (double X, double Y) OffShore(World world, double x, double y, double room)
        {
            if (world == null) return (x, y);
            for (var pass = 0; pass < 4; pass++)
            {
                var moved = false;
                foreach (var island in world.Islands)
                {
                    if (World.Distance(x, y, island.X, island.Y) > island.Radius + room) continue;
                    var distance = LandShape.SignedDistance(island, x, y, out var normalX, out var normalY);
                    if (distance >= room) continue;
                    x += normalX * (room - distance);
                    y += normalY * (room - distance);
                    moved = true;
                }
                if (!moved) break;
            }
            return (x, y);
        }
    }
}
