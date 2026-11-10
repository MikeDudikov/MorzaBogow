using System;
using System.Linq;

namespace MorzaBogow.Simulation
{
    /// <summary>Wood and silver economy: E02 gathering, E03 stores and berths, E04 production, E06 repairs and wrecks.</summary>
    public sealed partial class World
    {
        /// <summary>Permanently consumed by production, repairs or losses; part of the world ledger (E10).</summary>
        public long SpentWood, SpentSilver, LostWood, LostSilver, ReturnedWood, ReturnedSilver;

        public long LedgerTotal(ResourceType type)
        {
            var total = Nodes.Where(node => node.Type == type).Sum(node => node.Stock)
                        + Settlements.Sum(settlement => settlement.Stock(type))
                        + Ships.Where(ship => ship.CargoType == type).Sum(ship => ship.Cargo)
                        + Ships.Sum(ship => type == ResourceType.Wood ? ship.HoldWood : ship.HoldSilver)
                        + Wrecks.Sum(wreck => type == ResourceType.Wood ? wreck.Wood : wreck.Silver);
            return type == ResourceType.Wood
                ? total + SpentWood + LostWood - ReturnedWood - GrownWood
                : total + SpentSilver + LostSilver - ReturnedSilver - GrownSilver;
        }

        /// <summary>What deposits have grown back since the start; the ledger counts it as new resources, not a leak.</summary>
        public long GrownWood, GrownSilver;

        /// <summary>
        /// Renewing deposits: a point no hull has worked for Rules.RegrowDelaySeconds grows back toward its starting stock
        /// at Rules.RegrowShare of what it yields at most (World.NodeCap), forests and mines alike. So a game is never ended
        /// by empty deposits; working a point hard still exhausts it for a while.
        /// </summary>
        void StepRegrowth()
        {
            foreach (var node in Nodes)
            {
                if (node.Capacity <= 0 || node.Stock >= node.Capacity) continue;
                if (Tick - node.LastWorkedTick < Rules.RegrowDelaySeconds * Rules.TickRate) continue;
                node.RegrowRemainder += NodeCap(node) * Rules.RegrowShare * Rules.Dt * Rules.Centi;
                var whole = Math.Min((long)node.RegrowRemainder, node.Capacity - node.Stock);
                if (whole <= 0) continue;
                node.RegrowRemainder -= whole;
                node.Stock += whole;
                if (node.Type == ResourceType.Wood) GrownWood += whole; else GrownSilver += whole;
            }
        }

        // ---------- Production: complete templates, reserved in full, consumed with progress ----------

        /// <summary>Orders pay from the local store of the chosen port; without a target the main settlement builds.</summary>
        void QueueShip(int faction, ShipClass shipClass, int settlementId = -1, int blueprintId = -1)
        {
            // A blueprint fixes the class; the hull is ordered at the class price, its other weapons from the workshop.
            var blueprint = blueprintId >= 0 ? Blueprints.FirstOrDefault(item => item.Id == blueprintId && item.Owner == faction) : null;
            if (blueprintId >= 0 && blueprint == null) return;
            if (blueprint != null) shipClass = blueprint.Class;
            var settlement = settlementId >= 0 ? FindSettlement(settlementId) : SettlementOf(faction);
            if (settlement == null || settlement.Owner != faction || settlement.Destroyed) return;
            var queued = PortsOf(faction).Sum(port => port.WarQueue.Count + port.PortQueue.Count);
            if (Ships.Count(ship => ship.Owner == faction) + queued >= Rules.FleetLimit) return;

            var definition = ShipCatalog.Get(shipClass);
            if (!definition.IsEconomic && !settlement.HasShipyard) return;
            var wood = definition.CostWood * Rules.Centi;
            var silver = definition.CostSilver * Rules.Centi;
            if (settlement.Available(ResourceType.Wood) < wood || settlement.Available(ResourceType.Silver) < silver) return;

            settlement.ReservedWood += wood;
            settlement.ReservedSilver += silver;
            var queue = definition.IsEconomic ? settlement.PortQueue : settlement.WarQueue;
            queue.Add(new BuildOrder { Class = shipClass, PaidWood = wood, PaidSilver = silver, TotalTicks = (int)(definition.BuildSeconds * Rules.TickRate), BlueprintId = blueprint?.Id ?? -1 });
            if (blueprint != null) OrderBlueprintWeapons(settlement, blueprint);
        }

        void CancelQueue(int faction, int index, bool portQueue, int settlementId = -1)
        {
            var settlement = settlementId >= 0 ? FindSettlement(settlementId) : SettlementOf(faction);
            if (settlement == null || settlement.Owner != faction) return;
            var queue = portQueue ? settlement.PortQueue : settlement.WarQueue;
            if (index < 0 || index >= queue.Count) return;

            var quote = QuoteCancel(settlement, portQueue, index);
            queue.RemoveAt(index);
            settlement.ReservedWood -= quote.ReleasedWood;
            settlement.ReservedSilver -= quote.ReleasedSilver;
            settlement.Wood -= quote.LostWood;
            settlement.Silver -= quote.LostSilver;
            LostWood += quote.LostWood;
            LostSilver += quote.LostSilver;
        }

        /// <summary>
        /// What cancelling an order would settle, by the same rule the cancel applies (E04): the unused part of its
        /// reservation is released; once work has started, 80% of that part comes back and the rest is lost. For
        /// the interface's confirmation; it changes nothing.
        /// </summary>
        public CancelQuote QuoteCancel(Settlement settlement, bool portQueue, int index)
        {
            var queue = settlement == null ? null : portQueue ? settlement.PortQueue : settlement.WarQueue;
            if (queue == null || index < 0 || index >= queue.Count) return default;
            var order = queue[index];
            var unusedWood = order.PaidWood - order.UsedWood;
            var unusedSilver = order.PaidSilver - order.UsedSilver;
            var quote = new CancelQuote
            {
                Valid = true,
                Started = order.Started,
                Progress = order.TotalTicks > 0 ? (double)order.DoneTicks / order.TotalTicks : 0,
                ReleasedWood = unusedWood,
                ReleasedSilver = unusedSilver,
            };
            if (order.Started)
            {
                quote.LostWood = unusedWood - (long)(unusedWood * Rules.RefundAfterStart);
                quote.LostSilver = unusedSilver - (long)(unusedSilver * Rules.RefundAfterStart);
            }
            return quote;
        }

        void StepProduction()
        {
            foreach (var settlement in Settlements)
            {
                if (settlement.Destroyed) continue;
                StepLine(settlement, settlement.WarQueue);
                StepLine(settlement, settlement.PortQueue);
                StepProjects(settlement);
                if (settlement.ShipyardBuild == null || !AdvanceOrder(settlement, settlement.ShipyardBuild)) continue;
                settlement.ShipyardBuild = null;
                settlement.HasShipyard = true;
                Emit("shipyard_built", settlement.Id, settlement.Id, settlement.X, settlement.Y, 0);
            }
        }

        void StepLine(Settlement settlement, System.Collections.Generic.List<BuildOrder> queue)
        {
            if (queue.Count == 0) return;
            var order = queue[0];
            if (!AdvanceOrder(settlement, order)) return;
            queue.RemoveAt(0);
            var war = queue == settlement.WarQueue;
            var (x, y, heading) = LaunchSpot(settlement, ShipCatalog.Get(order.Class), war);
            var ship = SpawnShip(settlement.Owner, order.Class, x, y);
            ship.Heading = heading;
            if (order.BlueprintId >= 0) ApplyBlueprint(ship, order.BlueprintId);
            Emit("ship_built", ship.Id, settlement.Id, ship.X, ship.Y, war ? 1 : 0);
        }

        /// <summary>
        /// Where a finished hull floats off its slipway: on water just off the land, bow to the sea. Ships already
        /// there are not overlapped: the hull comes to rest further out, or beside them, at the first free place.
        /// </summary>
        (double x, double y, double heading) LaunchSpot(Settlement settlement, ShipDefinition definition, bool war)
        {
            var landX = Rules.SettlementLandX(settlement);
            var toward = settlement.X >= landX ? 1 : -1;
            var side = war ? -1 : 1;
            var clearance = definition.Radius + Seabed.ShoalWidth(definition.Draft);
            var baseX = landX + toward * Rules.SlipAlong;
            var baseY = settlement.Y + side * (Rules.SettlementLandRadius + clearance + 1);
            var heading = side * Math.PI / 2;
            var room = definition.Beam + Rules.OarReach(definition) * 2 + HullSpacing;
            for (var ring = 0; ring < 6; ring++)
            {
                foreach (var lateral in new[] { 0, 1, -1, 2, -2 })
                {
                    var x = baseX + toward * lateral * room;
                    var y = baseY + side * ring * (definition.Radius + HullSpacing + 2);
                    if (LaunchFree(x, y, definition, clearance)) return (x, y, heading);
                }
            }
            return (baseX, baseY + side * 6 * (definition.Radius + HullSpacing + 2), heading);
        }

        bool LaunchFree(double x, double y, ShipDefinition definition, double clearance)
        {
            if (ClearanceAt(x, y) < clearance) return false;
            foreach (var other in Ships)
            {
                if (Distance(x, y, other.X, other.Y) < definition.Radius + other.Definition.Radius + HullSpacing + Rules.OarReach(definition)) return false;
            }
            return true;
        }

        /// <summary>One tick of paid work. Returns true when the order is complete.</summary>
        bool AdvanceOrder(Settlement settlement, BuildOrder order)
        {
            order.Started = true;
            order.DoneTicks++;

            // Computed from total progress, so rounding never drifts.
            var wood = order.PaidWood * order.DoneTicks / order.TotalTicks - order.UsedWood;
            var silver = order.PaidSilver * order.DoneTicks / order.TotalTicks - order.UsedSilver;
            order.UsedWood += wood;
            order.UsedSilver += silver;
            settlement.Wood -= wood;
            settlement.Silver -= silver;
            settlement.ReservedWood -= wood;
            settlement.ReservedSilver -= silver;
            SpentWood += wood;
            SpentSilver += silver;
            return order.DoneTicks >= order.TotalTicks;
        }

        // ---------- Gathering: two berths, FIFO, shared cap, finite stock ----------

        void StepHarvest(Ship ship)
        {
            var node = FindNode(ship.NodeId);

            // A cutter delivers to its nearest own port; once in a port's queue it stays with that port.
            var settlement = ship.Harvest == HarvestPhase.WaitingPort || ship.Harvest == HarvestPhase.Docked
                ? FindSettlement(ship.PortId) ?? SettlementOf(ship.Owner)
                : ClosestPort(ship);
            var definition = ship.Definition;

            if (ship.Harvest == HarvestPhase.ToNode)
            {
                if (node == null || node.Stock <= 0)
                {
                    node = ClosestOpenNode(ship, node?.Type);
                    if (node == null)
                    {
                        if (ship.Cargo > 0) ship.Harvest = HarvestPhase.ToPort;
                        else { ship.Order = OrderKind.Idle; StopShip(ship); return; }
                    }
                    else ship.NodeId = node.Id;
                }

                if (ship.Harvest == HarvestPhase.ToNode)
                {
                    if (ship.Cargo > 0 && ship.CargoType != node.Type) ship.Harvest = HarvestPhase.ToPort;
                    else if (Distance(ship.X, ship.Y, node.X, node.Y) <= NodeReach(ship))
                    {
                        ship.CargoType = node.Type;
                        node.Waiting.Add(ship.Id);
                        ship.Harvest = HarvestPhase.WaitingBerth;
                    }
                    else Steer(ship, node.X, node.Y, definition.Speed, Rules.NodeWorkRange * 0.8);
                }
            }

            if (ship.Harvest == HarvestPhase.WaitingBerth)
            {
                StopShip(ship);
                if (node == null || node.Stock <= 0)
                {
                    LeaveNode(ship);
                    ship.Harvest = ship.Cargo > 0 ? HarvestPhase.ToPort : HarvestPhase.ToNode;
                }
                else if (node.Workers.Contains(ship.Id))
                {
                    ship.Harvest = HarvestPhase.Landing;
                    ship.CrewTicks = (int)(Rules.LandCrewSeconds * Rules.TickRate);
                    return;
                }
            }

            if (ship.Harvest == HarvestPhase.Landing)
            {
                StopShip(ship);
                if (node == null || node.Stock <= 0) { BeginEmbarking(ship); return; }
                if (--ship.CrewTicks > 0) return;
                ship.Harvest = HarvestPhase.Working;
                return;
            }

            if (ship.Harvest == HarvestPhase.Working)
            {
                StopShip(ship);
                if (node == null || node.Stock <= 0 || ship.Cargo >= definition.Capacity * Rules.Centi) BeginEmbarking(ship);
                return;
            }

            if (ship.Harvest == HarvestPhase.Embarking)
            {
                // The berth stays taken until the last of the crew is aboard.
                StopShip(ship);
                if (--ship.CrewTicks > 0) return;
                LeaveNode(ship);
                ship.Harvest = ship.Cargo > 0 ? HarvestPhase.ToPort : HarvestPhase.ToNode;
                return;
            }

            if (ship.Harvest == HarvestPhase.ToPort)
            {
                if (settlement == null || settlement.Destroyed) { ship.Order = OrderKind.Idle; StopShip(ship); return; }
                if (Distance(ship.X, ship.Y, settlement.X, settlement.Y) <= PortReach(ship))
                {
                    StopShip(ship);
                    settlement.PortWaiting.Add(ship.Id);
                    ship.PortId = settlement.Id;
                    ship.Harvest = HarvestPhase.WaitingPort;
                }
                else Steer(ship, settlement.X, settlement.Y, definition.Speed, Rules.PortRange * 0.8);
            }

            if (ship.Harvest == HarvestPhase.WaitingPort)
            {
                StopShip(ship);
                if (settlement == null || settlement.Destroyed) { ReleaseDock(ship); ship.Order = OrderKind.Idle; return; }
                if (settlement.DockedShips.Contains(ship.Id)) ship.Harvest = HarvestPhase.Docked;
                else return;
            }

            if (ship.Harvest == HarvestPhase.Docked) StepUnload(ship, settlement);
        }

        void BeginEmbarking(Ship ship)
        {
            ship.Harvest = HarvestPhase.Embarking;
            ship.CrewTicks = (int)(Rules.EmbarkSeconds * Rules.TickRate);
        }

        void StepUnload(Ship ship, Settlement settlement)
        {
            StopShip(ship);
            if (settlement == null || settlement.Destroyed) { ReleaseDock(ship); ship.Order = OrderKind.Idle; return; }

            var free = settlement.Capacity(ship.CargoType) - settlement.Stock(ship.CargoType);
            if (free <= 0 && ship.Cargo > 0)
            {
                // E03: a full store keeps the rest on the ship and frees the berth, with one alarm.
                ReleaseDock(ship);
                ship.Order = OrderKind.Idle;
                Emit("store_full", ship.Id, settlement.Id, ship.X, ship.Y, ship.Cargo);
                return;
            }

            ship.UnloadRemainder += Rules.HandlingPerSecond * Rules.Dt * Rules.Centi;
            var moved = (long)ship.UnloadRemainder;
            ship.UnloadRemainder -= moved;
            moved = Math.Min(Math.Min(moved, ship.Cargo), free);
            if (moved > 0)
            {
                ship.Cargo -= moved;
                settlement.Add(ship.CargoType, moved);
            }
            if (ship.Cargo > 0) return;

            ReleaseDock(ship);
            ship.UnloadRemainder = 0;
            if (Wrecks.Any(wreck => wreck.Id == ship.WreckId)) { ship.Order = OrderKind.Salvage; return; }
            ship.WreckId = -1;
            if (ship.NodeId < 0) { ship.Order = OrderKind.Idle; return; }
            ship.Harvest = HarvestPhase.ToNode;
        }

        void AssignBerths()
        {
            foreach (var settlement in Settlements)
            {
                while (settlement.DockedShips.Count < Rules.PortBerths && settlement.PortWaiting.Count > 0)
                {
                    settlement.DockedShips.Add(settlement.PortWaiting[0]);
                    settlement.PortWaiting.RemoveAt(0);
                }
            }
            foreach (var node in Nodes)
            {
                while (node.Workers.Count < Rules.WorkBerths && node.Waiting.Count > 0)
                {
                    node.Workers.Add(node.Waiting[0]);
                    node.Waiting.RemoveAt(0);
                }
            }
        }

        /// <summary>
        /// How fast one hull works a point, in units per second, and how much the point yields at most to all its
        /// workers together. Both scale with the scenario's yield (MatchProfile): felling and smelting take as long
        /// as the scenario's length asks for.
        /// </summary>
        public double WorkRate(ResourceNode node, Ship ship) =>
            node.Type == ResourceType.Wood ? ship.Definition.WoodWork * Profile.WoodYield : ship.Definition.SilverWork * Profile.SilverYield;

        public double NodeCap(ResourceNode node) =>
            node.Type == ResourceType.Wood ? Rules.WoodNodeCap * Profile.WoodYield : Rules.SilverNodeCap * Profile.SilverYield;

        void StepWorkers()
        {
            foreach (var node in Nodes)
            {
                if (node.Workers.Count == 0) continue;
                var workers = node.Workers.Select(FindShip).Where(ship => ship != null && ship.Harvest == HarvestPhase.Working).OrderBy(ship => ship.Id).ToList();
                if (workers.Count == 0) continue;

                double Rate(Ship ship) => WorkRate(node, ship);
                var total = workers.Sum(Rate);
                var cap = NodeCap(node);
                var share = total > cap ? cap / total : 1.0;

                foreach (var ship in workers)
                {
                    ship.WorkRemainder += Rate(ship) * share * Rules.Dt * Rules.Centi;
                    var whole = (long)ship.WorkRemainder;
                    ship.WorkRemainder -= whole;
                    whole = Math.Min(whole, Math.Min(node.Stock, ship.Definition.Capacity * Rules.Centi - ship.Cargo));
                    if (whole <= 0) continue;
                    node.LastWorkedTick = Tick;
                    node.Stock -= whole;
                    ship.Cargo += whole;
                }
            }
        }

        void LeaveNode(Ship ship)
        {
            foreach (var node in Nodes)
            {
                node.Workers.Remove(ship.Id);
                node.Waiting.Remove(ship.Id);
            }
        }

        void ReleaseDock(Ship ship)
        {
            foreach (var settlement in Settlements)
            {
                settlement.DockedShips.Remove(ship.Id);
                settlement.PortWaiting.Remove(ship.Id);
                if (settlement.RepairShipId == ship.Id) settlement.RepairShipId = -1;
            }
        }

        ResourceNode ClosestOpenNode(Ship ship, ResourceType? type)
        {
            ResourceNode best = null;
            var bestDistance = double.MaxValue;
            foreach (var node in Nodes)
            {
                if (type.HasValue && node.Type != type.Value) continue;
                if (node.Stock <= 0 || (node.KnownMask & (1 << ship.Owner)) == 0) continue;
                var distance = Distance2(ship.X, ship.Y, node.X, node.Y);
                if (distance < bestDistance) { bestDistance = distance; best = node; }
            }
            return best;
        }

        // ---------- Wrecks ----------

        void CreateWreck(Ship ship)
        {
            var carriedWood = ship.HoldWood + (ship.CargoType == ResourceType.Wood ? ship.Cargo : 0);
            var carriedSilver = ship.HoldSilver + (ship.CargoType == ResourceType.Silver ? ship.Cargo : 0);
            if (carriedWood + carriedSilver <= 0) return;

            var keptWood = (long)(carriedWood * Rules.WreckCargoFraction);
            var keptSilver = (long)(carriedSilver * Rules.WreckCargoFraction);
            LostWood += carriedWood - keptWood;
            LostSilver += carriedSilver - keptSilver;
            ship.Cargo = ship.HoldWood = ship.HoldSilver = 0;
            if (keptWood + keptSilver <= 0) return;
            Wrecks.Add(new Wreck
            {
                Id = NextId++, X = ship.X, Y = ship.Y, Wood = keptWood, Silver = keptSilver,
                ExpiresTick = Tick + (int)(Rules.WreckLifeSeconds * Rules.TickRate),
            });
        }

        void StepWrecks()
        {
            for (var i = Wrecks.Count - 1; i >= 0; i--)
            {
                var wreck = Wrecks[i];
                if (wreck.Wood > 0 || wreck.Silver > 0)
                {
                    if (Tick < wreck.ExpiresTick) continue;
                    LostWood += wreck.Wood;
                    LostSilver += wreck.Silver;
                }
                Wrecks.RemoveAt(i);
            }
        }

        void StepSalvage(Ship ship)
        {
            var wreck = Wrecks.FirstOrDefault(item => item.Id == ship.WreckId);
            var capacity = ship.Definition.Capacity * Rules.Centi;
            if (wreck == null || ship.Cargo >= capacity)
            {
                FinishSalvage(ship);
                return;
            }

            var type = wreck.Wood > 0 ? ResourceType.Wood : ResourceType.Silver;
            if (ship.Cargo > 0 && ship.CargoType != type) { FinishSalvage(ship); return; }
            if (Distance(ship.X, ship.Y, wreck.X, wreck.Y) > Rules.WreckRange)
            {
                Steer(ship, wreck.X, wreck.Y, ship.Definition.Speed, Rules.WreckRange * 0.8);
                return;
            }

            StopShip(ship);
            ship.CargoType = type;
            ship.UnloadRemainder += Rules.HandlingPerSecond * Rules.Dt * Rules.Centi;
            var moved = (long)ship.UnloadRemainder;
            ship.UnloadRemainder -= moved;
            var available = type == ResourceType.Wood ? wreck.Wood : wreck.Silver;
            moved = Math.Min(Math.Min(moved, available), capacity - ship.Cargo);
            ship.Cargo += moved;
            if (type == ResourceType.Wood) wreck.Wood -= moved; else wreck.Silver -= moved;
            if (wreck.Wood <= 0 && wreck.Silver <= 0) FinishSalvage(ship);
        }

        void FinishSalvage(Ship ship)
        {
            ship.UnloadRemainder = 0;
            if (ship.Cargo <= 0) { ship.Order = OrderKind.Idle; ship.WreckId = -1; return; }
            ship.Order = OrderKind.Harvest;
            ship.Harvest = HarvestPhase.ToPort;
        }

        // ---------- Repair and scrap at the shipyard station ----------

        /// <summary>A side has a dock somewhere: only there are hulls and weapon modules mended.</summary>
        public bool HasDock(int faction) => PortsOf(faction).Any(item => item.HasDock && item.HasShipyard && !item.Destroyed);

        /// <summary>The hull or a fitted module needs mending.</summary>
        public static bool NeedsMending(Ship ship) => ship.Hull < ship.Definition.Hull || ModulesDamaged(ship);

        /// <summary>
        /// Where a ship goes on a repair order (docs/UZGODNIENIE_SPECYFIKACJI.md): the station it already holds; else,
        /// with a hull or a module to mend and a dock on its side, the nearest dock; else the nearest shipyard, which
        /// musters the crew and refits the weapons but mends nothing.
        /// </summary>
        public Settlement RepairYard(Ship ship)
        {
            var held = PortsOf(ship.Owner).FirstOrDefault(item => item.RepairShipId == ship.Id && !item.Destroyed);
            if (held != null) return held;
            if (NeedsMending(ship))
            {
                var dock = ClosestOf(ship, PortsOf(ship.Owner).Where(item => item.HasShipyard && item.HasDock && !item.Destroyed));
                if (dock != null) return dock;
            }
            return ClosestYard(ship);
        }

        void StepRepair(Ship ship)
        {
            var settlement = RepairYard(ship);
            var definition = ship.Definition;
            // Only a dock mends: elsewhere the hull and the modules count as done and the visit is the muster and refit.
            var mends = settlement != null && settlement.HasDock;
            var hullDone = ship.Hull >= definition.Hull || !mends;
            if (settlement == null || settlement.Destroyed || (hullDone && (!mends || !ModulesDamaged(ship)) && !NeedsRefit(ship, settlement) && !NeedsMuster(ship) && ship.MusterTicks == 0))
            {
                if (settlement != null && settlement.RepairShipId == ship.Id) Emit("ship_restored", ship.Id, settlement.Id, ship.X, ship.Y, ship.Crew);
                ReleaseDock(ship);
                ship.Order = OrderKind.Idle;
                return;
            }

            if (Distance(ship.X, ship.Y, settlement.X, settlement.Y) > PortReach(ship))
            {
                Steer(ship, settlement.X, settlement.Y, definition.Speed, PortReach(ship) * 0.8);
                return;
            }

            StopShip(ship);
            if (settlement.RepairShipId != ship.Id)
            {
                if (settlement.RepairShipId >= 0) return;
                settlement.RepairShipId = ship.Id;
            }

            if (hullDone)
            {
                // After the hull: the posts' modules (at a dock), then the crew; one job at a time, each charged as it starts.
                if (ship.MusterTicks > 0) { ship.MusterTicks--; return; }
                if (mends && RepairModule(ship, settlement)) return;
                if (Refit(ship, settlement)) return;
                Muster(ship, settlement);
                return;
            }

            // At a dock, for silver only (docs/UZGODNIENIE_SPECYFIKACJI.md, replacing E06's wood and silver): a full
            // repair costs 25% of the whole price the hull was built for, wood and silver counted together, charged by
            // restored hull.
            var hull = Math.Min(Rules.RepairHullPerSecond * Rules.Dt, definition.Hull - ship.Hull);
            var share = hull / definition.Hull * Rules.RepairFullFraction;
            var silver = ship.RepairSilverRemainder + (ship.PaidConstructionWood + ship.PaidConstructionSilver) * share;
            var wholeSilver = (long)silver;
            if (settlement.Available(ResourceType.Silver) < wholeSilver) return;

            settlement.Silver -= wholeSilver;
            SpentSilver += wholeSilver;
            ship.RepairSilverRemainder = silver - wholeSilver;
            ship.Hull += hull;
        }

        // ---------- Muster at the dock: the wounded, recruits and retraining (docs/ZALOGA_I_ABORDAZ.md) ----------

        /// <summary>The crew differs from its plan, or there are wounded aboard.</summary>
        public static bool NeedsMuster(Ship ship) =>
            ship.Wounded > 0 || ship.Sailors != ship.Plan.Sailors || ship.Swordsmen != ship.Plan.Swordsmen
            || ship.Javelins != ship.Plan.Javelins || ship.Archers != ship.Plan.Archers;

        /// <summary>A fitted post's module is below full strength.</summary>
        public static bool ModulesDamaged(Ship ship)
        {
            for (var i = 0; i < ship.Mounts.Length; i++) if (ship.Mounts[i] != WeaponType.None && ship.ModuleHp[i] < Rules.ModuleHitPoints) return true;
            return false;
        }

        /// <summary>
        /// What mending one module costs at a dock (docs/UZBROJENIE.md, docs/UZGODNIENIE_SPECYFIKACJI.md): 25% of the
        /// item's whole worth, wood and silver counted together, times the share of its hit points restored, paid in
        /// silver only and rounded down to 0.01; and how long, the whole module taking at most 10 s. The wood part is
        /// always zero; it stays in the tuple for the quotes that show both stores.
        /// </summary>
        public static (long wood, long silver, int ticks) ModuleRepair(Ship ship, int slot)
        {
            var missing = Math.Max(0, Rules.ModuleHitPoints - ship.ModuleHp[slot]) / Rules.ModuleHitPoints;
            var silver = (long)((ship.ItemWood[slot] + ship.ItemSilver[slot]) * Rules.ModuleRepairFraction * missing);
            return (0, silver, (int)Math.Ceiling(missing * Rules.ModuleRepairSeconds * Rules.TickRate - 1e-9));
        }

        /// <summary>All modules' repair together, as the interface quotes it.</summary>
        public static (long wood, long silver) ModuleRepairCost(Ship ship)
        {
            long wood = 0, silver = 0;
            for (var i = 0; i < ship.Mounts.Length; i++)
            {
                if (ship.Mounts[i] == WeaponType.None) continue;
                var (w, s, _) = ModuleRepair(ship, i);
                wood += w;
                silver += s;
            }
            return (wood, silver);
        }

        /// <summary>Mends the first damaged module if the port can pay; true while there is module work (done or waiting).</summary>
        bool RepairModule(Ship ship, Settlement port)
        {
            for (var i = 0; i < ship.Mounts.Length; i++)
            {
                if (ship.Mounts[i] == WeaponType.None || ship.ModuleHp[i] >= Rules.ModuleHitPoints) continue;
                var (wood, silver, ticks) = ModuleRepair(ship, i);
                if (port.Available(ResourceType.Wood) < wood || port.Available(ResourceType.Silver) < silver) return true;
                port.Wood -= wood;
                port.Silver -= silver;
                SpentWood += wood;
                SpentSilver += silver;
                ship.ModuleHp[i] = Rules.ModuleHitPoints;
                ship.MusterTicks = Math.Max(0, ticks - 1);
                Emit("module_repaired", ship.Id, port.Id, ship.X, ship.Y, i);
                return true;
            }
            return false;
        }

        /// <summary>Silver the port needs to bring the crew to its plan: healing, then recruits, then retraining.</summary>
        public static long MusterCost(Ship ship)
        {
            // The same steps Muster takes, on a copy of the crew, so the price shown is the price paid.
            var copy = new Ship { Class = ship.Class, Sailors = ship.Sailors, Swordsmen = ship.Swordsmen, Javelins = ship.Javelins, Archers = ship.Archers, Wounded = ship.Wounded, Plan = ship.Plan };
            long cost = 0;
            for (var guard = 0; guard < 1000 && NeedsMuster(copy); guard++)
            {
                var step = MusterStep(copy);
                if (step < 0) break;
                cost += step;
            }
            return cost;
        }

        /// <summary>
        /// One step of the muster, after the hull is whole. The port works on one man at a time and charges as it starts:
        /// a wounded man is healed and returns to the trade the plan lacks most (0.25 S, four a second); then a trade
        /// still short gets a recruit (0.5 S a sailor, 1 S a fighter, a second each); when the numbers match but the
        /// trades do not, a man of a surplus trade is retrained (0.5 S, two seconds). Without the silver it waits.
        /// The wounded the plan has no room for are sent ashore at no cost.
        /// </summary>
        void Muster(Ship ship, Settlement port)
        {
            if (ship.MusterTicks > 0) { ship.MusterTicks--; return; }
            var price = MusterPrice(ship, out var ticks);
            if (price < 0) return;
            if (price > 0 && !Pay(port, price)) return;
            MusterStep(ship);
            ship.MusterTicks = Math.Max(0, ticks - 1);
        }

        /// <summary>What the next muster step costs and how many ticks it takes; -1 when there is nothing to do.</summary>
        static long MusterPrice(Ship ship, out int ticks)
        {
            var plan = ship.Plan;
            var lacking = MostLacking(ship);
            ticks = 0;
            if (ship.Wounded > 0)
            {
                if (lacking == null) return 0;
                ticks = (int)Math.Round(Rules.TickRate / Rules.HealPerSecond);
                return Rules.HealSilver;
            }
            if (lacking != null && ship.Crew < plan.Total)
            {
                ticks = (int)(Rules.RecruitSeconds * Rules.TickRate);
                return lacking.Value == CrewTrade.Sailor ? Rules.SailorSilver : Rules.SoldierSilver;
            }
            if (Surplus(ship) == null) return -1;
            if (lacking == null) return 0;
            ticks = (int)(Rules.RetrainSeconds * Rules.TickRate);
            return Rules.RetrainSilver;
        }

        /// <summary>Carries out the next muster step on the crew and returns its price (-1: nothing to do).</summary>
        static long MusterStep(Ship ship)
        {
            var price = MusterPrice(ship, out _);
            if (price < 0) return -1;
            var plan = ship.Plan;
            var lacking = MostLacking(ship);
            if (ship.Wounded > 0)
            {
                // A wounded man the plan has no room for is sent ashore at no cost.
                ship.Wounded--;
                if (lacking != null) Add(ship, lacking.Value, 1);
                return price;
            }
            if (lacking != null && ship.Crew < plan.Total)
            {
                Add(ship, lacking.Value, 1);
                return price;
            }
            // More men than the plan holds go ashore; otherwise a man of a surplus trade retrains for the lacking one.
            Add(ship, Surplus(ship).Value, -1);
            if (lacking != null) Add(ship, lacking.Value, 1);
            return price;
        }

        static CrewTrade? Surplus(Ship ship) =>
            ship.Swordsmen > ship.Plan.Swordsmen ? CrewTrade.Swordsman : ship.Javelins > ship.Plan.Javelins ? CrewTrade.Javelin
            : ship.Archers > ship.Plan.Archers ? CrewTrade.Archer : ship.Sailors > ship.Plan.Sailors ? CrewTrade.Sailor : (CrewTrade?)null;

        /// <summary>The trade the plan lacks most; ties go sailors, swordsmen, javelin men, archers.</summary>
        static CrewTrade? MostLacking(Ship ship)
        {
            var plan = ship.Plan;
            var gaps = new[] { plan.Sailors - ship.Sailors, plan.Swordsmen - ship.Swordsmen, plan.Javelins - ship.Javelins, plan.Archers - ship.Archers };
            var trades = new[] { CrewTrade.Sailor, CrewTrade.Swordsman, CrewTrade.Javelin, CrewTrade.Archer };
            var pick = -1;
            for (var i = 0; i < gaps.Length; i++) if (gaps[i] > 0 && (pick < 0 || gaps[i] > gaps[pick])) pick = i;
            return pick < 0 ? (CrewTrade?)null : trades[pick];
        }

        static void Add(Ship ship, CrewTrade trade, int count)
        {
            if (trade == CrewTrade.Sailor) ship.Sailors += count;
            else if (trade == CrewTrade.Swordsman) ship.Swordsmen += count;
            else if (trade == CrewTrade.Javelin) ship.Javelins += count;
            else ship.Archers += count;
        }

        bool Pay(Settlement port, long silver)
        {
            if (port.Available(ResourceType.Silver) < silver) return false;
            port.Silver -= silver;
            SpentSilver += silver;
            return true;
        }

        /// <summary>
        /// Quick regrouping: an idle ship lying off its own shipyard port sends itself to the dock when its hull is hurt
        /// or its crew is short of the plan or wounded, provided the port can pay for the crew. Checked once a second.
        /// </summary>
        bool SendToDockIfNeeded(Ship ship)
        {
            if ((Tick + ship.Id) % Rules.TickRate != 0) return false;
            var port = ClosestYard(ship);
            // A hull or a module counts only beside a dock, which alone mends them; other ports muster and refit.
            var hurt = (port != null && port.HasDock && NeedsMending(ship)) || NeedsRefit(ship, port);
            if (!hurt && !NeedsMuster(ship)) return false;
            if (port == null || port.Destroyed || Distance(ship.X, ship.Y, port.X, port.Y) > Rules.MusterRange) return false;
            if (!hurt && port.Available(ResourceType.Silver) < MusterCost(ship)) return false;
            ship.Order = OrderKind.Repair;
            return true;
        }

        void StepScrap(Ship ship)
        {
            var settlement = ClosestYard(ship);
            if (settlement == null || settlement.Destroyed) { ship.Order = OrderKind.Idle; return; }
            if (Distance(ship.X, ship.Y, settlement.X, settlement.Y) > PortReach(ship))
            {
                Steer(ship, settlement.X, settlement.Y, ship.Definition.Speed, PortReach(ship) * 0.8);
                return;
            }

            StopShip(ship);
            if (Tick - ship.LastDamagedTick < Rules.ScrapCombatLockSeconds * Rules.TickRate) { ship.ScrapTicks = 0; return; }
            if (++ship.ScrapTicks < Rules.ScrapSeconds * Rules.TickRate) return;

            var condition = ship.Hull / ship.Definition.Hull;
            var wood = (long)(ship.PaidConstructionWood * Rules.ScrapFraction * condition);
            var silver = (long)(ship.PaidConstructionSilver * Rules.ScrapFraction * condition);
            wood = Math.Min(wood, settlement.Capacity(ResourceType.Wood) - settlement.Wood);
            silver = Math.Min(silver, settlement.Capacity(ResourceType.Silver) - settlement.Silver);
            settlement.Wood += wood;
            settlement.Silver += silver;
            ReturnedWood += wood;
            ReturnedSilver += silver;
            Emit("ship_scrapped", ship.Id, settlement.Id, ship.X, ship.Y, wood + silver);
            ship.Hull = 0;
            ship.Scrapped = true;
        }
    }
}
