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
            world.Islands.Add(new Island { X = Rules.LandX(x), Y = y, Radius = Rules.SettlementLandRadius, Kind = IslandKind.Settlement });
        }

        static void AddSide(World world, int owner, double x, int direction)
        {
            var settlement = new Settlement
            {
                Id = world.NextId++, Owner = owner, X = x, Y = 0,
                Wood = Rules.StartWood, Silver = Rules.StartSilver,
            };
            world.Settlements.Add(settlement);
            AddCoast(world, x - direction * 75, 0, 50, direction < 0);
            world.Islands.Add(new Island { X = Rules.SettlementLandX(settlement), Y = settlement.Y, Radius = Rules.SettlementLandRadius, Kind = IslandKind.Settlement });

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
            var islet = LandShape.Ellipse(Rules.SiteIsletHalfWidth, Rules.SiteIsletHalfDepth);
            world.Islands.Add(new Island { X = x, Y = y + Rules.SiteLandOffset, Radius = LandShape.MaxRadius(islet), Outline = islet, Kind = IslandKind.ResourceSite });
            return node.Id;
        }
    }
}
