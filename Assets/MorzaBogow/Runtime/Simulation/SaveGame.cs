using System;
using System.Collections.Generic;
using System.IO;

namespace MorzaBogow.Simulation
{
    /// <summary>Versioned binary save with atomic file replacement and a backup of the previous save (C12).</summary>
    public static class SaveGame
    {
        const uint Magic = 0x4D42474D;
        public const int Version = 3;

        static void WriteIds(BinaryWriter writer, List<int> ids)
        {
            writer.Write(ids.Count);
            foreach (var id in ids) writer.Write(id);
        }

        static void ReadIds(BinaryReader reader, List<int> ids)
        {
            var count = reader.ReadInt32();
            for (var i = 0; i < count; i++) ids.Add(reader.ReadInt32());
        }

        static void WriteQueue(BinaryWriter writer, List<BuildOrder> queue)
        {
            writer.Write(queue.Count);
            foreach (var order in queue)
            {
                writer.Write((int)order.Class); writer.Write(order.PaidWood); writer.Write(order.PaidSilver);
                writer.Write(order.UsedWood); writer.Write(order.UsedSilver);
                writer.Write(order.TotalTicks); writer.Write(order.DoneTicks); writer.Write(order.Started);
            }
        }

        static void ReadQueue(BinaryReader reader, List<BuildOrder> queue)
        {
            var count = reader.ReadInt32();
            for (var i = 0; i < count; i++)
            {
                queue.Add(new BuildOrder
                {
                    Class = (ShipClass)reader.ReadInt32(), PaidWood = reader.ReadInt64(), PaidSilver = reader.ReadInt64(),
                    UsedWood = reader.ReadInt64(), UsedSilver = reader.ReadInt64(),
                    TotalTicks = reader.ReadInt32(), DoneTicks = reader.ReadInt32(), Started = reader.ReadBoolean(),
                });
            }
        }

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
                writer.Write(world.SpentWood); writer.Write(world.SpentSilver);
                writer.Write(world.LostWood); writer.Write(world.LostSilver);
                writer.Write(world.ReturnedWood); writer.Write(world.ReturnedSilver);
                writer.Write(world.Explored);

                writer.Write(world.Islands.Count);
                foreach (var island in world.Islands) { writer.Write(island.X); writer.Write(island.Y); writer.Write(island.Radius); }

                writer.Write(world.Nodes.Count);
                foreach (var node in world.Nodes)
                {
                    writer.Write(node.Id); writer.Write((int)node.Type); writer.Write(node.X); writer.Write(node.Y);
                    writer.Write(node.Stock); writer.Write(node.KnownMask);
                    WriteIds(writer, node.Workers);
                    WriteIds(writer, node.Waiting);
                }

                writer.Write(world.Settlements.Count);
                foreach (var settlement in world.Settlements)
                {
                    writer.Write(settlement.Id); writer.Write(settlement.Owner); writer.Write(settlement.X); writer.Write(settlement.Y);
                    writer.Write(settlement.Hull); writer.Write(settlement.Wood); writer.Write(settlement.Silver);
                    writer.Write(settlement.ReservedWood); writer.Write(settlement.ReservedSilver); writer.Write(settlement.RepairShipId);
                    WriteQueue(writer, settlement.WarQueue);
                    WriteQueue(writer, settlement.PortQueue);
                    WriteIds(writer, settlement.DockedShips);
                    WriteIds(writer, settlement.PortWaiting);
                }

                writer.Write(world.Ships.Count);
                foreach (var ship in world.Ships)
                {
                    writer.Write(ship.Id); writer.Write(ship.Owner); writer.Write((int)ship.Class);
                    writer.Write(ship.X); writer.Write(ship.Y); writer.Write(ship.Heading); writer.Write(ship.Speed); writer.Write(ship.Hull);
                    writer.Write(ship.Sailors); writer.Write(ship.Soldiers); writer.Write(ship.CrewDamage); writer.Write(ship.Morale);
                    writer.Write(ship.Reload); writer.Write(ship.LastDamagedTick);
                    writer.Write(ship.PaidConstructionWood); writer.Write(ship.PaidConstructionSilver);
                    writer.Write((int)ship.Order); writer.Write(ship.TargetX); writer.Write(ship.TargetY); writer.Write(ship.TargetId);
                    writer.Write(ship.NodeId); writer.Write(ship.WreckId); writer.Write((int)ship.Harvest); writer.Write((int)ship.CargoType); writer.Write(ship.Cargo);
                    writer.Write(ship.WorkRemainder); writer.Write(ship.UnloadRemainder);
                    writer.Write(ship.RepairWoodRemainder); writer.Write(ship.RepairSilverRemainder); writer.Write(ship.ScrapTicks);
                    writer.Write((int)ship.RamState); writer.Write(ship.RamTimer); writer.Write(ship.LastRamTick);
                }

                writer.Write(world.Wrecks.Count);
                foreach (var wreck in world.Wrecks)
                {
                    writer.Write(wreck.Id); writer.Write(wreck.X); writer.Write(wreck.Y);
                    writer.Write(wreck.Wood); writer.Write(wreck.Silver); writer.Write(wreck.ExpiresTick);
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
                world.SpentWood = reader.ReadInt64(); world.SpentSilver = reader.ReadInt64();
                world.LostWood = reader.ReadInt64(); world.LostSilver = reader.ReadInt64();
                world.ReturnedWood = reader.ReadInt64(); world.ReturnedSilver = reader.ReadInt64();
                world.Explored = reader.ReadBytes(World.FogCells * World.FogCells);
                if (world.Explored.Length != World.FogCells * World.FogCells) throw new InvalidDataException("Damaged fog data.");

                var count = reader.ReadInt32();
                for (var i = 0; i < count; i++) world.Islands.Add(new Island { X = reader.ReadDouble(), Y = reader.ReadDouble(), Radius = reader.ReadDouble() });

                count = reader.ReadInt32();
                for (var i = 0; i < count; i++)
                {
                    var node = new ResourceNode
                    {
                        Id = reader.ReadInt32(), Type = (ResourceType)reader.ReadInt32(), X = reader.ReadDouble(), Y = reader.ReadDouble(),
                        Stock = reader.ReadInt64(), KnownMask = reader.ReadInt32(),
                    };
                    ReadIds(reader, node.Workers);
                    ReadIds(reader, node.Waiting);
                    world.Nodes.Add(node);
                }

                count = reader.ReadInt32();
                for (var i = 0; i < count; i++)
                {
                    var settlement = new Settlement
                    {
                        Id = reader.ReadInt32(), Owner = reader.ReadInt32(), X = reader.ReadDouble(), Y = reader.ReadDouble(),
                        Hull = reader.ReadDouble(), Wood = reader.ReadInt64(), Silver = reader.ReadInt64(),
                        ReservedWood = reader.ReadInt64(), ReservedSilver = reader.ReadInt64(), RepairShipId = reader.ReadInt32(),
                    };
                    ReadQueue(reader, settlement.WarQueue);
                    ReadQueue(reader, settlement.PortQueue);
                    ReadIds(reader, settlement.DockedShips);
                    ReadIds(reader, settlement.PortWaiting);
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
                        PaidConstructionWood = reader.ReadInt64(), PaidConstructionSilver = reader.ReadInt64(),
                        Order = (OrderKind)reader.ReadInt32(), TargetX = reader.ReadDouble(), TargetY = reader.ReadDouble(), TargetId = reader.ReadInt32(),
                        NodeId = reader.ReadInt32(), WreckId = reader.ReadInt32(), Harvest = (HarvestPhase)reader.ReadInt32(), CargoType = (ResourceType)reader.ReadInt32(), Cargo = reader.ReadInt64(),
                        WorkRemainder = reader.ReadDouble(), UnloadRemainder = reader.ReadDouble(),
                        RepairWoodRemainder = reader.ReadDouble(), RepairSilverRemainder = reader.ReadDouble(), ScrapTicks = reader.ReadInt32(),
                        RamState = (RamPhase)reader.ReadInt32(), RamTimer = reader.ReadInt32(), LastRamTick = reader.ReadInt32(),
                    });
                }

                count = reader.ReadInt32();
                for (var i = 0; i < count; i++)
                {
                    world.Wrecks.Add(new Wreck
                    {
                        Id = reader.ReadInt32(), X = reader.ReadDouble(), Y = reader.ReadDouble(),
                        Wood = reader.ReadInt64(), Silver = reader.ReadInt64(), ExpiresTick = reader.ReadInt32(),
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
