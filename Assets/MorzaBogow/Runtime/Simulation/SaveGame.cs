using System;
using System.IO;

namespace MorzaBogow.Simulation
{
    /// <summary>Versioned binary save with atomic file replacement and a backup of the previous save (C12).</summary>
    public static class SaveGame
    {
        const uint Magic = 0x4D42474D;
        public const int Version = 1;

        public static byte[] Serialize(World world, int opponentBuildIndex = 0)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Magic);
                writer.Write(Version);
                writer.Write(world.Tick);
                writer.Write(world.NextId);
                writer.Write(world.NextEventId);
                writer.Write(world.Random.State);
                writer.Write((int)world.Outcome);
                writer.Write(world.Winner);
                writer.Write(opponentBuildIndex);

                writer.Write(world.Islands.Count);
                foreach (var island in world.Islands) { writer.Write(island.X); writer.Write(island.Y); writer.Write(island.Radius); }

                writer.Write(world.Nodes.Count);
                foreach (var node in world.Nodes)
                {
                    writer.Write(node.Id); writer.Write((int)node.Type); writer.Write(node.X); writer.Write(node.Y);
                    writer.Write(node.Stock); writer.Write(node.Capacity); writer.Write(node.Closed); writer.Write(node.KnownMask); writer.Write(node.RegenRemainder);
                }

                writer.Write(world.Settlements.Count);
                foreach (var settlement in world.Settlements)
                {
                    writer.Write(settlement.Id); writer.Write(settlement.Owner); writer.Write(settlement.X); writer.Write(settlement.Y);
                    writer.Write(settlement.Hull); writer.Write(settlement.Bronze); writer.Write(settlement.Rations);
                    writer.Write(settlement.Queue.Count);
                    foreach (var order in settlement.Queue)
                    {
                        writer.Write((int)order.Class); writer.Write(order.PaidBronze); writer.Write(order.PaidRations);
                        writer.Write(order.TotalTicks); writer.Write(order.DoneTicks); writer.Write(order.Started);
                    }
                    writer.Write(settlement.DockedShips.Count);
                    foreach (var id in settlement.DockedShips) writer.Write(id);
                }

                writer.Write(world.Ships.Count);
                foreach (var ship in world.Ships)
                {
                    writer.Write(ship.Id); writer.Write(ship.Owner); writer.Write((int)ship.Class);
                    writer.Write(ship.X); writer.Write(ship.Y); writer.Write(ship.Heading); writer.Write(ship.Speed); writer.Write(ship.Hull);
                    writer.Write(ship.Sailors); writer.Write(ship.Soldiers); writer.Write(ship.CrewDamage); writer.Write(ship.Morale);
                    writer.Write(ship.Reload); writer.Write(ship.LastDamagedTick);
                    writer.Write((int)ship.Order); writer.Write(ship.TargetX); writer.Write(ship.TargetY); writer.Write(ship.TargetId);
                    writer.Write(ship.NodeId); writer.Write((int)ship.Harvest); writer.Write((int)ship.CargoType); writer.Write(ship.Cargo);
                    writer.Write(ship.WorkRemainder); writer.Write(ship.UnloadRemainder);
                    writer.Write((int)ship.RamState); writer.Write(ship.RamTimer); writer.Write(ship.LastRamTick);
                }

                writer.Write(world.Boardings.Count);
                foreach (var link in world.Boardings)
                {
                    writer.Write(link.AttackerId); writer.Write(link.TargetId); writer.Write(link.HookTicks);
                    writer.Write(link.CycleTicks); writer.Write(link.ControlTicks); writer.Write(link.Hooked);
                }

                writer.Flush();
                return stream.ToArray();
            }
        }

        public static World Deserialize(byte[] data, out int opponentBuildIndex)
        {
            using (var reader = new BinaryReader(new MemoryStream(data)))
            {
                if (reader.ReadUInt32() != Magic) throw new InvalidDataException("Not a Morza Bogów save.");
                var version = reader.ReadInt32();
                if (version != Version) throw new InvalidDataException($"Unsupported save version {version}.");

                var world = new World(1)
                {
                    Tick = reader.ReadInt32(), NextId = reader.ReadInt32(), NextEventId = reader.ReadInt32(),
                };
                world.Random.State = reader.ReadUInt64();
                world.Outcome = (OutcomeKind)reader.ReadInt32();
                world.Winner = reader.ReadInt32();
                opponentBuildIndex = reader.ReadInt32();

                var count = reader.ReadInt32();
                for (var i = 0; i < count; i++) world.Islands.Add(new Island { X = reader.ReadDouble(), Y = reader.ReadDouble(), Radius = reader.ReadDouble() });

                count = reader.ReadInt32();
                for (var i = 0; i < count; i++)
                {
                    world.Nodes.Add(new ResourceNode
                    {
                        Id = reader.ReadInt32(), Type = (ResourceType)reader.ReadInt32(), X = reader.ReadDouble(), Y = reader.ReadDouble(),
                        Stock = reader.ReadInt64(), Capacity = reader.ReadInt64(), Closed = reader.ReadBoolean(), KnownMask = reader.ReadInt32(), RegenRemainder = reader.ReadDouble(),
                    });
                }

                count = reader.ReadInt32();
                for (var i = 0; i < count; i++)
                {
                    var settlement = new Settlement
                    {
                        Id = reader.ReadInt32(), Owner = reader.ReadInt32(), X = reader.ReadDouble(), Y = reader.ReadDouble(),
                        Hull = reader.ReadDouble(), Bronze = reader.ReadInt64(), Rations = reader.ReadInt64(),
                    };
                    var queue = reader.ReadInt32();
                    for (var q = 0; q < queue; q++)
                    {
                        settlement.Queue.Add(new BuildOrder
                        {
                            Class = (ShipClass)reader.ReadInt32(), PaidBronze = reader.ReadInt64(), PaidRations = reader.ReadInt64(),
                            TotalTicks = reader.ReadInt32(), DoneTicks = reader.ReadInt32(), Started = reader.ReadBoolean(),
                        });
                    }
                    var docked = reader.ReadInt32();
                    for (var d = 0; d < docked; d++) settlement.DockedShips.Add(reader.ReadInt32());
                    world.Settlements.Add(settlement);
                }

                count = reader.ReadInt32();
                for (var i = 0; i < count; i++)
                {
                    world.Ships.Add(new Ship
                    {
                        Id = reader.ReadInt32(), Owner = reader.ReadInt32(), Class = (ShipClass)reader.ReadInt32(),
                        X = reader.ReadDouble(), Y = reader.ReadDouble(), Heading = reader.ReadDouble(), Speed = reader.ReadDouble(), Hull = reader.ReadDouble(),
                        Sailors = reader.ReadInt32(), Soldiers = reader.ReadInt32(), CrewDamage = reader.ReadDouble(), Morale = reader.ReadDouble(),
                        Reload = reader.ReadDouble(), LastDamagedTick = reader.ReadInt32(),
                        Order = (OrderKind)reader.ReadInt32(), TargetX = reader.ReadDouble(), TargetY = reader.ReadDouble(), TargetId = reader.ReadInt32(),
                        NodeId = reader.ReadInt32(), Harvest = (HarvestPhase)reader.ReadInt32(), CargoType = (ResourceType)reader.ReadInt32(), Cargo = reader.ReadInt64(),
                        WorkRemainder = reader.ReadDouble(), UnloadRemainder = reader.ReadDouble(),
                        RamState = (RamPhase)reader.ReadInt32(), RamTimer = reader.ReadInt32(), LastRamTick = reader.ReadInt32(),
                    });
                }

                count = reader.ReadInt32();
                for (var i = 0; i < count; i++)
                {
                    world.Boardings.Add(new BoardingLink
                    {
                        AttackerId = reader.ReadInt32(), TargetId = reader.ReadInt32(), HookTicks = reader.ReadInt32(),
                        CycleTicks = reader.ReadInt32(), ControlTicks = reader.ReadInt32(), Hooked = reader.ReadBoolean(),
                    });
                }
                return world;
            }
        }

        /// <summary>Writes to a temporary file first, keeps the previous save as .bak, then swaps.</summary>
        public static void WriteFile(string path, byte[] data)
        {
            var temporary = path + ".tmp";
            File.WriteAllBytes(temporary, data);
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
            else File.Move(temporary, path);
        }

        public static ulong Hash(byte[] data)
        {
            var hash = 14695981039346656037UL;
            foreach (var value in data)
            {
                hash ^= value;
                hash *= 1099511628211UL;
            }
            return hash;
        }
    }
}
