using System;
using System.Collections.Generic;
using System.IO;

namespace MorzaBogow.Simulation
{
    /// <summary>Versioned binary save with atomic file replacement and a backup of the previous save (C12).</summary>
    public static class SaveGame
    {
        const uint Magic = 0x4D42474D;
        public const int Version = 17;

        /// <summary>Weapon posts: type, cycle progress, module hit points and item worth; the ram's worth last.</summary>
        static void WriteArms(BinaryWriter writer, Ship ship)
        {
            writer.Write(ship.Mounts.Length);
            for (var i = 0; i < ship.Mounts.Length; i++)
            {
                writer.Write((int)ship.Mounts[i]); writer.Write(ship.Progress[i]); writer.Write(ship.ModuleHp[i]);
            }
            writer.Write(ship.ItemWood.Length);
            for (var i = 0; i < ship.ItemWood.Length; i++) { writer.Write(ship.ItemWood[i]); writer.Write(ship.ItemSilver[i]); }
        }

        static void ReadArms(BinaryReader reader, Ship ship)
        {
            var count = reader.ReadInt32();
            if (count < 0 || count > 16) throw new InvalidDataException("Damaged weapon posts.");
            ship.Mounts = new WeaponType[count];
            ship.Progress = new double[count];
            ship.ModuleHp = new double[count];
            for (var i = 0; i < count; i++)
            {
                ship.Mounts[i] = (WeaponType)reader.ReadInt32(); ship.Progress[i] = reader.ReadDouble(); ship.ModuleHp[i] = reader.ReadDouble();
            }
            var items = reader.ReadInt32();
            if (items < 0 || items > 17) throw new InvalidDataException("Damaged item values.");
            ship.ItemWood = new long[items];
            ship.ItemSilver = new long[items];
            for (var i = 0; i < items; i++) { ship.ItemWood[i] = reader.ReadInt64(); ship.ItemSilver[i] = reader.ReadInt64(); }
        }

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
                writer.Write((int)order.Weapon); writer.Write(order.BlueprintId); writer.Write(order.Unarmed);
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
                    Weapon = (WeaponType)reader.ReadInt32(), BlueprintId = reader.ReadInt32(), Unarmed = reader.ReadBoolean(),
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
                writer.Write(world.GrownWood); writer.Write(world.GrownSilver);
                writer.Write(world.Explored);
                writer.Write((int)world.Length);

                writer.Write(world.Islands.Count);
                foreach (var island in world.Islands)
                {
                    writer.Write(island.X); writer.Write(island.Y); writer.Write(island.Radius); writer.Write((int)island.Kind);
                    writer.Write(island.Yaw);
                    writer.Write(island.Outline?.Length ?? 0);
                    if (island.Outline != null) foreach (var radius in island.Outline) writer.Write(radius);
                }

                writer.Write(world.Nodes.Count);
                foreach (var node in world.Nodes)
                {
                    writer.Write(node.Id); writer.Write((int)node.Type); writer.Write(node.X); writer.Write(node.Y);
                    writer.Write(node.Stock); writer.Write(node.KnownMask);
                    writer.Write(node.Capacity); writer.Write(node.LastWorkedTick); writer.Write(node.RegrowRemainder);
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
                    writer.Write(settlement.IsOutpost); writer.Write(settlement.HasShipyard); writer.Write(settlement.Walls);
                    var yard = new List<BuildOrder>();
                    if (settlement.ShipyardBuild != null) yard.Add(settlement.ShipyardBuild);
                    WriteQueue(writer, yard);
                    writer.Write(settlement.HasWorkshop); writer.Write(settlement.IsTown); writer.Write(settlement.HasSageSchool); writer.Write(settlement.HasDock);
                    writer.Write((int)settlement.ProjectKind);
                    var project = new List<BuildOrder>();
                    if (settlement.Project != null) project.Add(settlement.Project);
                    WriteQueue(writer, project);
                    WriteQueue(writer, settlement.WorkshopQueue);
                    writer.Write(settlement.Arsenal.Length);
                    foreach (var count in settlement.Arsenal) writer.Write(count);
                }

                writer.Write(world.Ships.Count);
                foreach (var ship in world.Ships)
                {
                    writer.Write(ship.Id); writer.Write(ship.Owner); writer.Write((int)ship.Class);
                    writer.Write(ship.X); writer.Write(ship.Y); writer.Write(ship.Heading); writer.Write(ship.Speed); writer.Write(ship.Hull);
                    writer.Write(ship.WindSpeedFactor); writer.Write(ship.DriftX); writer.Write(ship.DriftY);
                    writer.Write(ship.Sailors); writer.Write(ship.Swordsmen); writer.Write(ship.Archers); writer.Write(ship.Wounded); writer.Write(ship.CrewDamage); writer.Write(ship.Morale);
                    writer.Write(ship.Reload); writer.Write(ship.LastDamagedTick);
                    writer.Write(ship.PaidConstructionWood); writer.Write(ship.PaidConstructionSilver);
                    writer.Write((int)ship.Order); writer.Write(ship.TargetX); writer.Write(ship.TargetY); writer.Write(ship.TargetId);
                    writer.Write(ship.NodeId); writer.Write(ship.WreckId); writer.Write((int)ship.Harvest); writer.Write((int)ship.CargoType); writer.Write(ship.Cargo);
                    writer.Write(ship.WorkRemainder); writer.Write(ship.UnloadRemainder);
                    writer.Write(ship.RepairSilverRemainder); writer.Write(ship.ScrapTicks);
                    writer.Write((int)ship.RamState); writer.Write(ship.RamTimer); writer.Write(ship.LastRamTick);
                    writer.Write(ship.PortId); writer.Write(ship.HoldWood); writer.Write(ship.HoldSilver);
                    writer.Write(ship.WantWood); writer.Write(ship.WantSilver); writer.Write((int)ship.Transport);
                    writer.Write(ship.SourceId); writer.Write(ship.DestinationId); writer.Write(ship.SiteId); writer.Write(ship.EscortId);
                    writer.Write(ship.BuildTicks); writer.Write(ship.BuildUsedWood); writer.Write(ship.BuildUsedSilver);
                    writer.Write(ship.HasWaypoint); writer.Write(ship.WaypointX); writer.Write(ship.WaypointY);
                    writer.Write(ship.RouteX); writer.Write(ship.RouteY); writer.Write(ship.ReplanTick);
                    writer.Write(ship.CrewTicks);
                    writer.Write(ship.Plan.Sailors); writer.Write(ship.Plan.Swordsmen); writer.Write(ship.Plan.Archers); writer.Write((int)ship.Plan.Preset);
                    writer.Write(ship.WoundRemainder); writer.Write(ship.MusterTicks);
                    writer.Write(ship.Javelins); writer.Write(ship.Plan.Javelins); writer.Write(ship.TelegraphTicks);
                    WriteArms(writer, ship);
                    writer.Write(ship.Loadout.Length);
                    foreach (var weapon in ship.Loadout) writer.Write((int)weapon);
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
                    writer.Write(link.CutProgress); writer.Write(link.AttackerLossRemainder); writer.Write(link.TargetLossRemainder);
                    writer.Write(link.AttackerWoundedAtStart);
                }

                writer.Write(world.Sites.Count);
                foreach (var site in world.Sites)
                {
                    writer.Write(site.Id); writer.Write(site.X); writer.Write(site.Y);
                    writer.Write(site.SettlementId); writer.Write(site.BuilderShipId);
                }

                // Shots in the air land after loading exactly as they would have (docs/UZBROJENIE.md).
                writer.Write(world.HeavyArmsResearched);
                writer.Write(world.NextBlueprintId);
                writer.Write(world.Blueprints.Count);
                foreach (var blueprint in world.Blueprints)
                {
                    writer.Write(blueprint.Id); writer.Write(blueprint.Owner); writer.Write(blueprint.Name ?? string.Empty);
                    writer.Write((int)blueprint.Class); writer.Write((int)blueprint.Preset);
                    writer.Write(blueprint.Loadout.Length);
                    foreach (var weapon in blueprint.Loadout) writer.Write((int)weapon);
                }
                writer.Write(world.NextProjectileId);
                writer.Write(world.Projectiles.Count);
                foreach (var shot in world.Projectiles)
                {
                    writer.Write(shot.Id); writer.Write(shot.OwnerId); writer.Write(shot.ShooterId); writer.Write(shot.TargetShipId); writer.Write(shot.TargetSettlementId);
                    writer.Write((int)shot.Weapon); writer.Write(shot.FromX); writer.Write(shot.FromY); writer.Write(shot.ToX); writer.Write(shot.ToY);
                    writer.Write(shot.LaunchTick); writer.Write(shot.ArriveTick);
                }

                // Wind and sea (docs/MORZE_I_WIATR.md): what evolves; grids and the surface are rebuilt from the map.
                writer.Write(world.Marine != null);
                world.Marine?.Write(writer);

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
                world.GrownWood = reader.ReadInt64(); world.GrownSilver = reader.ReadInt64();
                world.Explored = reader.ReadBytes(World.FogCells * World.FogCells);
                if (world.Explored.Length != World.FogCells * World.FogCells) throw new InvalidDataException("Damaged fog data.");
                world.Length = (MatchLength)reader.ReadInt32();
                if (world.Length < MatchLength.Short || world.Length > MatchLength.Long) throw new InvalidDataException("Damaged match length.");

                var count = reader.ReadInt32();
                for (var i = 0; i < count; i++)
                {
                    var island = new Island { X = reader.ReadDouble(), Y = reader.ReadDouble(), Radius = reader.ReadDouble(), Kind = (IslandKind)reader.ReadInt32(), Yaw = reader.ReadDouble() };
                    var samples = reader.ReadInt32();
                    if (samples != 0 && samples != LandShape.Samples) throw new InvalidDataException("Damaged land outline.");
                    if (samples > 0)
                    {
                        island.Outline = new double[samples];
                        for (var k = 0; k < samples; k++) island.Outline[k] = reader.ReadDouble();
                    }
                    world.Islands.Add(island);
                }

                count = reader.ReadInt32();
                for (var i = 0; i < count; i++)
                {
                    var node = new ResourceNode
                    {
                        Id = reader.ReadInt32(), Type = (ResourceType)reader.ReadInt32(), X = reader.ReadDouble(), Y = reader.ReadDouble(),
                        Stock = reader.ReadInt64(), KnownMask = reader.ReadInt32(),
                        Capacity = reader.ReadInt64(), LastWorkedTick = reader.ReadInt32(), RegrowRemainder = reader.ReadDouble(),
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
                    settlement.IsOutpost = reader.ReadBoolean();
                    settlement.HasShipyard = reader.ReadBoolean();
                    settlement.Walls = reader.ReadDouble();
                    if (!(settlement.Walls > 0)) throw new InvalidDataException("Damaged walls.");
                    var yard = new List<BuildOrder>();
                    ReadQueue(reader, yard);
                    if (yard.Count > 0) settlement.ShipyardBuild = yard[0];
                    settlement.HasWorkshop = reader.ReadBoolean(); settlement.IsTown = reader.ReadBoolean(); settlement.HasSageSchool = reader.ReadBoolean(); settlement.HasDock = reader.ReadBoolean();
                    settlement.ProjectKind = (ProjectKind)reader.ReadInt32();
                    var project = new List<BuildOrder>();
                    ReadQueue(reader, project);
                    if (project.Count > 0) settlement.Project = project[0];
                    ReadQueue(reader, settlement.WorkshopQueue);
                    var arsenal = reader.ReadInt32();
                    if (arsenal != settlement.Arsenal.Length) throw new InvalidDataException("Damaged arsenal.");
                    for (var a = 0; a < arsenal; a++) settlement.Arsenal[a] = reader.ReadInt32();
                    world.Settlements.Add(settlement);
                }

                count = reader.ReadInt32();
                for (var i = 0; i < count; i++)
                {
                    var ship = new Ship
                    {
                        Id = reader.ReadInt32(), Owner = reader.ReadInt32(), Class = (ShipClass)reader.ReadInt32(),
                        X = reader.ReadDouble(), Y = reader.ReadDouble(), Heading = reader.ReadDouble(), Speed = reader.ReadDouble(), Hull = reader.ReadDouble(),
                        WindSpeedFactor = reader.ReadDouble(), DriftX = reader.ReadDouble(), DriftY = reader.ReadDouble(),
                        Sailors = reader.ReadInt32(), Swordsmen = reader.ReadInt32(), Archers = reader.ReadInt32(), Wounded = reader.ReadInt32(), CrewDamage = reader.ReadDouble(), Morale = reader.ReadDouble(),
                        Reload = reader.ReadDouble(), LastDamagedTick = reader.ReadInt32(),
                        PaidConstructionWood = reader.ReadInt64(), PaidConstructionSilver = reader.ReadInt64(),
                        Order = (OrderKind)reader.ReadInt32(), TargetX = reader.ReadDouble(), TargetY = reader.ReadDouble(), TargetId = reader.ReadInt32(),
                        NodeId = reader.ReadInt32(), WreckId = reader.ReadInt32(), Harvest = (HarvestPhase)reader.ReadInt32(), CargoType = (ResourceType)reader.ReadInt32(), Cargo = reader.ReadInt64(),
                        WorkRemainder = reader.ReadDouble(), UnloadRemainder = reader.ReadDouble(),
                        RepairSilverRemainder = reader.ReadDouble(), ScrapTicks = reader.ReadInt32(),
                        RamState = (RamPhase)reader.ReadInt32(), RamTimer = reader.ReadInt32(), LastRamTick = reader.ReadInt32(),
                        PortId = reader.ReadInt32(), HoldWood = reader.ReadInt64(), HoldSilver = reader.ReadInt64(),
                        WantWood = reader.ReadInt64(), WantSilver = reader.ReadInt64(), Transport = (TransportPhase)reader.ReadInt32(),
                        SourceId = reader.ReadInt32(), DestinationId = reader.ReadInt32(), SiteId = reader.ReadInt32(), EscortId = reader.ReadInt32(),
                        BuildTicks = reader.ReadInt32(), BuildUsedWood = reader.ReadInt64(), BuildUsedSilver = reader.ReadInt64(),
                        HasWaypoint = reader.ReadBoolean(), WaypointX = reader.ReadDouble(), WaypointY = reader.ReadDouble(),
                        RouteX = reader.ReadDouble(), RouteY = reader.ReadDouble(), ReplanTick = reader.ReadInt32(),
                        CrewTicks = reader.ReadInt32(),
                        Plan = new CrewPlan { Sailors = reader.ReadInt32(), Swordsmen = reader.ReadInt32(), Archers = reader.ReadInt32(), Preset = (CrewPreset)reader.ReadInt32() },
                        WoundRemainder = reader.ReadDouble(), MusterTicks = reader.ReadInt32(),
                    };
                    ship.Javelins = reader.ReadInt32();
                    var plan = ship.Plan;
                    plan.Javelins = reader.ReadInt32();
                    ship.Plan = plan;
                    ship.TelegraphTicks = reader.ReadInt32();
                    ReadArms(reader, ship);
                    var planned = reader.ReadInt32();
                    if (planned != ship.Mounts.Length) throw new InvalidDataException("Damaged weapon plan.");
                    ship.Loadout = new WeaponType[planned];
                    for (var p = 0; p < planned; p++) ship.Loadout[p] = (WeaponType)reader.ReadInt32();
                    if (ship.Mounts.Length != ship.Definition.Slots.Length) throw new InvalidDataException("Weapon posts do not match the class.");
                    world.Ships.Add(ship);
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
                        CutProgress = reader.ReadDouble(), AttackerLossRemainder = reader.ReadDouble(), TargetLossRemainder = reader.ReadDouble(),
                        AttackerWoundedAtStart = reader.ReadInt32(),
                    });
                }

                count = reader.ReadInt32();
                for (var i = 0; i < count; i++)
                {
                    world.Sites.Add(new BuildSite
                    {
                        Id = reader.ReadInt32(), X = reader.ReadDouble(), Y = reader.ReadDouble(),
                        SettlementId = reader.ReadInt32(), BuilderShipId = reader.ReadInt32(),
                    });
                }

                world.HeavyArmsResearched = reader.ReadInt32();
                world.NextBlueprintId = reader.ReadInt32();
                count = reader.ReadInt32();
                for (var i = 0; i < count; i++)
                {
                    var blueprint = new Blueprint
                    {
                        Id = reader.ReadInt32(), Owner = reader.ReadInt32(), Name = reader.ReadString(),
                        Class = (ShipClass)reader.ReadInt32(), Preset = (CrewPreset)reader.ReadInt32(),
                    };
                    var slots = reader.ReadInt32();
                    if (slots < 0 || slots > 16) throw new InvalidDataException("Damaged blueprint.");
                    blueprint.Loadout = new WeaponType[slots];
                    for (var s = 0; s < slots; s++) blueprint.Loadout[s] = (WeaponType)reader.ReadInt32();
                    world.Blueprints.Add(blueprint);
                }
                world.NextProjectileId = reader.ReadInt32();
                count = reader.ReadInt32();
                for (var i = 0; i < count; i++)
                {
                    world.Projectiles.Add(new Projectile
                    {
                        Id = reader.ReadInt32(), OwnerId = reader.ReadInt32(), ShooterId = reader.ReadInt32(), TargetShipId = reader.ReadInt32(), TargetSettlementId = reader.ReadInt32(),
                        Weapon = (WeaponType)reader.ReadInt32(), FromX = reader.ReadDouble(), FromY = reader.ReadDouble(), ToX = reader.ReadDouble(), ToY = reader.ReadDouble(),
                        LaunchTick = reader.ReadInt32(), ArriveTick = reader.ReadInt32(),
                    });
                }
                if (reader.ReadBoolean()) world.Marine = MarineState.Read(reader, world);
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
