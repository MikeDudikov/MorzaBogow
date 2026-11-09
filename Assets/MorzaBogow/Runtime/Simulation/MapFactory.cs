using System;

namespace MorzaBogow.Simulation
{
    /// <summary>
    /// The first playable battle with the E08 reference budget: 34000 wood and 12400 silver in points.
    /// Distances are straight lines here; real route checks remain open.
    /// </summary>
    public static class MapFactory
    {
        public const double BaseX = 375;

        public static World CreateSkirmish(ulong seed)
        {
            var world = new World(seed);
            AddSide(world, 0, -BaseX, 1);
            AddSide(world, 1, BaseX, -1);

            // Four symmetric expansion pairs, about 325 m from a base.
            AddPair(world, -250, 300, 4000, 1600, 1);
            AddPair(world, -250, -300, 4000, 1600, 1);
            AddPair(world, 250, 300, 4000, 1600, 2);
            AddPair(world, 250, -300, 4000, 1600, 2);

            // Two contested central pairs.
            AddPair(world, 0, 210, 6000, 2000, 0);
            AddPair(world, 0, -210, 6000, 2000, 0);

            // Free shores beside each pair, mirrored so neither side starts closer to any of them.
            foreach (var y in new double[] { 300, -300 })
            {
                AddSite(world, -180, y);
                AddSite(world, 180, y);
            }
            foreach (var y in new double[] { 210, -210 })
            {
                AddSite(world, -70, y);
                AddSite(world, 70, y);
            }

            AddCoast(world, 0, 0, 60, false);
            AddCoast(world, -150, 130, 35, false);
            AddCoast(world, 150, -130, 35, true);
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

        static void AddSide(World world, int owner, double x, int direction)
        {
            var settlement = new Settlement
            {
                Id = world.NextId++, Owner = owner, X = x, Y = 0,
                Wood = Rules.StartWood, Silver = Rules.StartSilver,
            };
            world.Settlements.Add(settlement);
            AddCoast(world, x - direction * 75, 0, 50, direction < 0);
            world.Islands.Add(SettlementLand(settlement.X, settlement.Y));

            var mask = 1 << owner;
            var wood = AddNode(world, ResourceType.Wood, x + direction * 80, 45, 3000, mask);
            var silver = AddNode(world, ResourceType.Silver, x + direction * 80, -45, 1000, mask);

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
            AddNode(world, ResourceType.Wood, x, y + 30, wood, knownMask);
            AddNode(world, ResourceType.Silver, x, y - 30, silver, knownMask);
        }

        static int AddNode(World world, ResourceType type, double x, double y, long units, int knownMask)
        {
            var node = new ResourceNode { Id = world.NextId++, Type = type, X = x, Y = y, Stock = units * Rules.Centi, KnownMask = knownMask };
            world.Nodes.Add(node);
            world.Islands.Add(SiteLand(x, y));
            return node.Id;
        }
    }
}
