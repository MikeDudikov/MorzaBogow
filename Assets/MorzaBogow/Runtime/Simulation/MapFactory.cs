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

            world.Islands.Add(new Island { X = 0, Y = 0, Radius = 60 });
            world.Islands.Add(new Island { X = -150, Y = 130, Radius = 35 });
            world.Islands.Add(new Island { X = 150, Y = -130, Radius = 35 });
            return world;
        }

        static void AddSide(World world, int owner, double x, int direction)
        {
            var settlement = new Settlement
            {
                Id = world.NextId++, Owner = owner, X = x, Y = 0,
                Wood = Rules.StartWood, Silver = Rules.StartSilver,
            };
            world.Settlements.Add(settlement);
            world.Islands.Add(new Island { X = x - direction * 75, Y = 0, Radius = 50 });

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
            return node.Id;
        }
    }
}
