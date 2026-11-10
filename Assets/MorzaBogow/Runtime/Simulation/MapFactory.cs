using System;
using System.Linq;

namespace MorzaBogow.Simulation
{
    /// <summary>
    /// The 1v1 skirmish map in three scales (MatchProfile, docs/BALANS_I_SKALA.md): the same point-symmetric pattern of
    /// a home base, four expansion pairs and two contested central pairs, spread wider and stocked richer for a longer
    /// game. Ports and work points stand on small islets joined to large hinterland islands, so a town or a forest has
    /// land in proportion to the ships beside it.
    /// </summary>
    public static class MapFactory
    {
        public const double BaseX = 375;

        public static World CreateSkirmish(ulong seed) => CreateSkirmish(seed, MatchLength.Standard);

        public static World CreateSkirmish(ulong seed, MatchLength length)
        {
            var world = new World(seed) { Length = length };
            var profile = world.Profile;
            var s = profile.Spread;
            AddSide(world, profile, 0, -BaseX * s, 1);
            AddSide(world, profile, 1, BaseX * s, -1);

            // Four symmetric expansion pairs, about 325 m from a base at the smallest scale.
            AddPair(world, -250 * s, 300 * s, profile.PairWood, profile.PairSilver, 1);
            AddPair(world, -250 * s, -300 * s, profile.PairWood, profile.PairSilver, 1);
            AddPair(world, 250 * s, 300 * s, profile.PairWood, profile.PairSilver, 2);
            AddPair(world, 250 * s, -300 * s, profile.PairWood, profile.PairSilver, 2);

            // Two contested central pairs.
            AddPair(world, 0, 210 * s, profile.CentreWood, profile.CentreSilver, 0);
            AddPair(world, 0, -210 * s, profile.CentreWood, profile.CentreSilver, 0);

            // Free shores beside each pair, mirrored so neither side starts closer to any of them.
            foreach (var y in new[] { 300 * s, -300 * s })
            {
                AddSite(world, -180 * s, y);
                AddSite(world, 180 * s, y);
            }
            foreach (var y in new[] { 210 * s, -210 * s })
            {
                AddSite(world, -70 * s, y);
                AddSite(world, 70 * s, y);
            }

            AddCoast(world, 0, 0, 60 * s, false);
            AddCoast(world, -150 * s, 130 * s, 35 * s, false);
            AddCoast(world, 150 * s, -130 * s, 35 * s, true);

            // Last, so each hinterland keeps clear of every port, work point and channel already laid out.
            AddHinterlands(world);
            world.RefreshExplored();
            return world;
        }

        /// <summary>
        /// Waterline of the rocky coast model in units of the land's radius, read from its mesh (BattleSceneBuilder.
        /// CoastWaterline; a test keeps the two in step). Coasts on the second side are turned half a turn, so the
        /// map stays point-symmetric for both players.
        /// </summary>
        public static readonly double[] CoastOutline =
        {
            0.2659, 0.2365, 0.2224, 0.2216, 0.2266, 0.2348, 0.2424, 0.2543, 0.2787, 0.3071, 0.3367, 0.3696, 0.3930, 0.4065, 0.4269, 0.4589,
            0.5151, 0.6241, 0.8072, 0.9977, 1.0906, 1.1082, 1.0762, 1.0013, 0.9337, 0.8752, 0.8151, 0.7609, 0.7324, 0.7304, 0.7241, 0.7066,
            0.7269, 0.7946, 0.8591, 0.9183, 0.9713, 1.0380, 1.1429, 1.2473, 1.3155, 1.3120, 1.2503, 1.1909, 1.1314, 1.0665, 1.0275, 1.0110,
            1.0044, 1.0101, 1.0267, 1.0551, 1.0984, 1.1547, 1.2014, 1.2199, 1.2244, 1.2081, 1.1516, 1.0511, 0.8786, 0.6697, 0.4858, 0.3421,
        };

        static void AddCoast(World world, double x, double y, double scale, bool halfTurn)
        {
            var outline = LandShape.Place(CoastOutline, scale, halfTurn);
            world.Islands.Add(new Island { X = x, Y = y, Radius = LandShape.MaxRadius(outline), Outline = outline, Yaw = halfTurn ? 180 : 0, Kind = IslandKind.Coast });
        }

        static void AddSite(World world, double x, double y)
        {
            world.Sites.Add(new BuildSite { Id = world.NextId++, X = x, Y = y });
            world.Islands.Add(SettlementLand(x, y));
        }

        /// <summary>
        /// A port's islet: an irregular rocky shore around the land's centre. Toward the port, where the jetty and
        /// both slipways run out, the shore stays at the usual distance; on the far side capes and bays come and go.
        /// </summary>
        public static Island SettlementLand(double portX, double portY)
        {
            var landX = Rules.LandX(portX);
            var toward = portX >= landX ? 0.0 : Math.PI;
            var circle = new double[LandShape.Samples];
            for (var i = 0; i < circle.Length; i++) circle[i] = Rules.SettlementLandRadius;
            var outline = LandShape.Irregular(circle, Seed(landX, portY), angle =>
            {
                var d = LandShape.AngleBetween(angle, toward);
                return d < Math.PI * 0.6
                    ? LandShape.Blend((0.96, 1.02), (0.93, 1.06), d, Math.PI * 0.2, Math.PI * 0.3)
                    : LandShape.Blend((0.93, 1.06), (0.84, 1.32), d, Math.PI * 0.6, Math.PI * 0.72);
            });
            return new Island { X = landX, Y = portY, Radius = LandShape.MaxRadius(outline), Outline = outline, Kind = IslandKind.Settlement };
        }

        /// <summary>
        /// A work point's islet, beyond the point from the berth. The jetty side stays as it was; the far half,
        /// where the trees or the mine stand, only grows, and the flanks may draw in toward small bays.
        /// </summary>
        public static Island SiteLand(double nodeX, double nodeY)
        {
            var islet = LandShape.Ellipse(Rules.SiteIsletHalfWidth, Rules.SiteIsletHalfDepth);
            var jetty = -Math.PI / 2;
            var outline = LandShape.Irregular(islet, Seed(nodeX, nodeY), angle =>
            {
                var d = LandShape.AngleBetween(angle, jetty);
                if (Math.Sin(angle) >= 0) return (0.98, 1.38);
                return LandShape.Blend((0.96, 1.04), (0.84, 1.28), d, Math.PI * 0.18, Math.PI * 0.3);
            });
            return new Island { X = nodeX, Y = nodeY + Rules.SiteLandOffset, Radius = LandShape.MaxRadius(outline), Outline = outline, Kind = IslandKind.ResourceSite };
        }

        static uint Seed(double x, double y) => (uint)((int)Math.Round(x * 7.0) * 73856093 ^ (int)Math.Round(y * 7.0) * 19349663);

        // Hinterland sizes: the largest that keeps every berth and channel open, never below the smallest.
        public const double HinterlandLargest = 110, HinterlandSmallest = 35, HinterlandBerthClearance = 34, HinterlandChannel = 30;

        /// <summary>
        /// Behind each port's islet (away from the berth) and each work point's islet (beyond the trees or the mine)
        /// rises a larger island joined to it. Its size is the largest that leaves every port, work point and free
        /// shore its approach, and every other land either a channel or a firm join (no narrow strait a hull could
        /// stick in); where there is no room, the islet stands alone.
        /// </summary>
        static void AddHinterlands(World world)
        {
            var anchors = world.Islands.Where(item => item.Kind == IslandKind.Settlement || item.Kind == IslandKind.ResourceSite).ToList();
            foreach (var anchor in anchors)
            {
                // Settlement land lies away from its port along x; a work point's islet lies beyond it along +y.
                var port = world.Settlements.Concat<object>(world.Sites).FirstOrDefault(item => Math.Abs(Rules.LandX(PortX(item)) - anchor.X) < 0.5 && Math.Abs(PortY(item) - anchor.Y) < 0.5);
                double awayX, awayY;
                if (anchor.Kind == IslandKind.Settlement) { awayX = port != null && Rules.LandX(PortX(port)) < PortX(port) ? -1 : 1; awayY = 0; }
                else { awayX = 0; awayY = 1; }

                Island best = null;
                foreach (var turn in new[] { 0.0, 0.5, -0.5, 1.0, -1.0 })
                {
                    var dx = awayX * Math.Cos(turn) - awayY * Math.Sin(turn);
                    var dy = awayX * Math.Sin(turn) + awayY * Math.Cos(turn);
                    for (var radius = HinterlandLargest; radius >= HinterlandSmallest; radius -= 5)
                    {
                        if (best != null && radius <= best.Radius / 1.25) break;
                        var candidate = Hinterland(anchor, dx, dy, radius);
                        if (!Fits(world, anchor, candidate)) continue;
                        if (best == null || candidate.Radius > best.Radius) best = candidate;
                        break;
                    }
                }
                if (best != null) world.Islands.Add(best);
            }
        }

        static double PortX(object item) => item is Settlement settlement ? settlement.X : ((BuildSite)item).X;
        static double PortY(object item) => item is Settlement settlement ? settlement.Y : ((BuildSite)item).Y;

        static Island Hinterland(Island anchor, double dx, double dy, double radius)
        {
            // Its near shore runs across the islet's far half, 14 m behind the islet's centre at the most: the two read
            // as one shore while the berth on the islet's other side keeps its approach. Away from the islet, capes and bays.
            var reach = 14 + radius * 0.9;
            var x = anchor.X + dx * reach;
            var y = anchor.Y + dy * reach;
            var toward = Math.Atan2(-dy, -dx);
            var circle = new double[LandShape.Samples];
            for (var i = 0; i < circle.Length; i++) circle[i] = radius;
            var outline = LandShape.Irregular(circle, Seed(x * 1.7, y * 1.3), angle =>
                LandShape.Blend((0.85, 0.95), (0.82, 1.22), LandShape.AngleBetween(angle, toward), Math.PI * 0.25, Math.PI * 0.45));
            return new Island { X = x, Y = y, Radius = LandShape.MaxRadius(outline), Outline = outline, Kind = IslandKind.Hinterland };
        }

        static bool Fits(World world, Island anchor, Island land)
        {
            bool Clear(double x, double y) => LandShape.SignedDistance(land, x, y) >= HinterlandBerthClearance;
            if (world.Settlements.Any(item => !Clear(item.X, item.Y))) return false;
            if (world.Sites.Any(item => !Clear(item.X, item.Y))) return false;
            if (world.Nodes.Any(item => !Clear(item.X, item.Y))) return false;
            // Between the real shores either a full channel or land joined to land, measured from points along the new
            // waterline: a gap narrower than the channel would be a strait a hull could stick in.
            foreach (var other in world.Islands)
            {
                if (other == anchor) continue;
                if (World.Distance(other.X, other.Y, land.X, land.Y) > other.Radius + land.Radius + HinterlandChannel) continue;
                for (var i = 0; i < LandShape.Samples; i++)
                {
                    var angle = LandShape.Angle(i);
                    var x = land.X + Math.Cos(angle) * land.Outline[i];
                    var y = land.Y + Math.Sin(angle) * land.Outline[i];
                    var gap = LandShape.SignedDistance(other, x, y);
                    if (gap > 0 && gap < HinterlandChannel) return false;
                }
            }
            return true;
        }

        static void AddSide(World world, MatchProfile profile, int owner, double x, int direction)
        {
            var s = profile.Spread;
            var settlement = new Settlement
            {
                Id = world.NextId++, Owner = owner, X = x, Y = 0,
                Wood = profile.StartWood * Rules.Centi, Silver = profile.StartSilver * Rules.Centi,
                Walls = profile.HomeWalls,
            };
            settlement.Hull = settlement.MaxHull;
            world.Settlements.Add(settlement);
            AddCoast(world, x - direction * 75 * s, 0, 50 * s, direction < 0);
            world.Islands.Add(SettlementLand(settlement.X, settlement.Y));

            var mask = 1 << owner;
            var wood = AddNode(world, ResourceType.Wood, x + direction * 80 * s, 45 * s, profile.HomeWood, mask);
            var silver = AddNode(world, ResourceType.Silver, x + direction * 80 * s, -45 * s, profile.HomeSilver, mask);

            // E04: three cutters on wood and one on silver from the first moment.
            for (var i = 0; i < 4; i++)
            {
                var ship = world.SpawnShip(owner, ShipClass.Cutter, x + direction * 40, (i - 1.5) * 20);
                ship.Order = OrderKind.Harvest;
                ship.Harvest = HarvestPhase.ToNode;
                ship.NodeId = i < 3 ? wood : silver;
            }
        }

        static void AddPair(World world, double x, double y, long wood, long silver, int knownMask)
        {
            var s = world.Profile.Spread;
            AddNode(world, ResourceType.Wood, x, y + 30 * s, wood, knownMask);
            AddNode(world, ResourceType.Silver, x, y - 30 * s, silver, knownMask);
        }

        static int AddNode(World world, ResourceType type, double x, double y, long units, int knownMask)
        {
            var node = new ResourceNode { Id = world.NextId++, Type = type, X = x, Y = y, Stock = units * Rules.Centi, Capacity = units * Rules.Centi, KnownMask = knownMask };
            world.Nodes.Add(node);
            world.Islands.Add(SiteLand(x, y));
            return node.Id;
        }
    }
}
