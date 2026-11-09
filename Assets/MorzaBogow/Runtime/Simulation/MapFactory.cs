namespace MorzaBogow.Simulation
{
    /// <summary>The M1 skirmish: start per E15 and the standard 1v1 resource layout from E03.</summary>
    public static class MapFactory
    {
        public const long StartBronze = 1400 * Rules.Centi;
        public const long StartRations = 600 * Rules.Centi;

        public static World CreateSkirmish(ulong seed)
        {
            var world = new World(seed);
            AddSide(world, 0, -560, 1);
            AddSide(world, 1, 560, -1);

            AddNode(world, ResourceType.Bronze, 0, 240, 5500, 3);
            AddNode(world, ResourceType.Bronze, 0, -240, 5500, 3);
            AddNode(world, ResourceType.Bronze, -60, 430, 5500, 0);
            AddNode(world, ResourceType.Bronze, 60, -430, 5500, 0);
            AddNode(world, ResourceType.Rations, -90, 90, 2000, 0);
            AddNode(world, ResourceType.Rations, 90, -90, 2000, 0);

            world.Islands.Add(new Island { X = 0, Y = 0, Radius = 70 });
            world.Islands.Add(new Island { X = -300, Y = 300, Radius = 55 });
            world.Islands.Add(new Island { X = 300, Y = -300, Radius = 55 });
            return world;
        }

        static void AddSide(World world, int owner, double x, int direction)
        {
            var settlement = new Settlement { Id = world.NextId++, Owner = owner, X = x, Y = 0, Bronze = StartBronze, Rations = StartRations };
            world.Settlements.Add(settlement);
            world.Islands.Add(new Island { X = x - direction * 75, Y = 0, Radius = 50 });

            var mask = 1 << owner;
            AddNode(world, ResourceType.Bronze, x + direction * 120, 130, 3000, mask);
            AddNode(world, ResourceType.Bronze, x + direction * 120, -130, 3000, mask);
            AddNode(world, ResourceType.Rations, x + direction * 170, 70, 2000, mask);
            AddNode(world, ResourceType.Rations, x + direction * 170, -70, 2000, mask);

            world.SpawnShip(owner, ShipClass.Cutter, x + direction * 45, 25);
            world.SpawnShip(owner, ShipClass.Cutter, x + direction * 45, -25);
        }

        static void AddNode(World world, ResourceType type, double x, double y, long units, int knownMask)
        {
            world.Nodes.Add(new ResourceNode
            {
                Id = world.NextId++, Type = type, X = x, Y = y,
                Stock = units * Rules.Centi, Capacity = units * Rules.Centi, KnownMask = knownMask,
            });
        }
    }
}
