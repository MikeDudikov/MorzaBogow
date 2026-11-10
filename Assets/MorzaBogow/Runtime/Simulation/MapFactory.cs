using System;
using System.Linq;

namespace MorzaBogow.Simulation
{
    /// <summary>
    /// The 1v1 skirmish map in three scales (MatchProfile, docs/BALANS_I_SKALA.md): the same point-symmetric pattern of
    /// a home base, four expansion pairs and two contested central pairs, spread wider and stocked richer for a longer
    /// game. The sea dominates: land is the islets of ports, free shores and work points (the forests to fell, the
    /// silver to mine) and a few small limestone rocks, as among the Aegean islands; no land stands only for scenery
    /// (the user's decision of 2026-11-10).
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

            // Last, so each rock keeps clear of every port, work point and channel already laid out.
            AddRocks(world);
            world.RefreshExplored();
            return world;
        }

        // Small limestone rocks: the largest point of the waterline from the rock's centre, the same at every scale.
        public const double CentreRockRadius = 22, RockRadius = 16;

        // Every berth, work point and free shore keeps this much water toward a rock; between the rock's shore and any
        // other either a full channel or land joined to land. A rock that does not fit moves out from the middle.
        public const double BerthClearance = 34, Channel = 30, RockStep = 5;
        public const int RockSteps = 12;

        /// <summary>
        /// Five small rocks in the open sea: one in the middle, a pair across the lanes between the central pairs and the
        /// expansions, and a pair behind the home ports. Each pair is point-symmetric, the second turned half a turn.
        /// They are cover from shots and the lie of the land, not scenery to fill the map.
        /// </summary>
        static void AddRocks(World world)
        {
            var s = world.Profile.Spread;
            var home = world.SettlementOf(0);
            AddRock(world, 0, 0, CentreRockRadius, false);
            AddRock(world, -150 * s, 130 * s, RockRadius, true);
            AddRock(world, home.X - 75 * s, home.Y, RockRadius, true);
        }

        /// <summary>
        /// Places a rock at its spot, or as near to it as it fits further out from the middle of the map; with its twin
        /// on the second side when the pattern has one. Where no spot fits, the map goes without the rock.
        /// </summary>
        static void AddRock(World world, double x, double y, double radius, bool twin)
        {
            var length = Math.Sqrt(x * x + y * y);
            var dx = length > 1e-9 ? x / length : 0;
            var dy = length > 1e-9 ? y / length : 0;
            for (var step = 0; step <= RockSteps; step++)
            {
                var rock = Rock(x + dx * step * RockStep, y + dy * step * RockStep, radius, false);
                var mirror = twin ? Rock(-rock.X, -rock.Y, radius, true) : null;
                if (!KeepsApproaches(world, rock) || (mirror != null && !KeepsApproaches(world, mirror))) continue;
                world.Islands.Add(rock);
                if (mirror != null) world.Islands.Add(mirror);
                return;
            }
        }

        /// <summary>A rock on the rocky coast model's own waterline, scaled so its farthest shore lies at the radius.</summary>
        static Island Rock(double x, double y, double radius, bool halfTurn)
        {
            var outline = LandShape.Place(CoastOutline, radius / LandShape.MaxRadius(CoastOutline), halfTurn);
            return new Island { X = x, Y = y, Radius = LandShape.MaxRadius(outline), Outline = outline, Yaw = halfTurn ? 180 : 0, Kind = IslandKind.Coast };
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

        /// <summary>
        /// Whether new land leaves every port, work point and free shore its approach, both slipways of every port
        /// their water, and every other land either a full channel or a firm join (no narrow strait a hull could stick in).
        /// </summary>
        public static bool KeepsApproaches(World world, Island land)
        {
            bool Clear(double x, double y) => LandShape.SignedDistance(land, x, y) >= BerthClearance;
            if (world.Settlements.Any(item => !Clear(item.X, item.Y))) return false;
            if (world.Sites.Any(item => !Clear(item.X, item.Y))) return false;
            if (world.Nodes.Any(item => !Clear(item.X, item.Y))) return false;
            foreach (var settlement in world.Settlements)
            {
                var landX = Rules.SettlementLandX(settlement);
                var toward = Math.Sign(settlement.X - landX);
                foreach (var side in new[] { -1, 1 })
                    if (LandShape.SignedDistance(land, landX + toward * Rules.SlipAlong, settlement.Y + side * 30) <= 0) return false;
            }
            // Between the real shores either a full channel or land joined to land, measured from points along the new
            // waterline: a gap narrower than the channel would be a strait a hull could stick in.
            foreach (var other in world.Islands)
            {
                if (other == land) continue;
                if (World.Distance(other.X, other.Y, land.X, land.Y) > other.Radius + land.Radius + Channel) continue;
                for (var i = 0; i < LandShape.Samples; i++)
                {
                    var angle = LandShape.Angle(i);
                    var x = land.X + Math.Cos(angle) * land.Outline[i];
                    var y = land.Y + Math.Sin(angle) * land.Outline[i];
                    var gap = LandShape.SignedDistance(other, x, y);
                    if (gap > 0 && gap < Channel) return false;
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
