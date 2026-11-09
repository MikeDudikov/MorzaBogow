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
                        + Wrecks.Sum(wreck => type == ResourceType.Wood ? wreck.Wood : wreck.Silver);
            return type == ResourceType.Wood
                ? total + SpentWood + LostWood - ReturnedWood
                : total + SpentSilver + LostSilver - ReturnedSilver;
        }

        // ---------- Production: complete templates, reserved in full, consumed with progress ----------

        void QueueShip(int faction, ShipClass shipClass)
        {
            var settlement = SettlementOf(faction);
            if (settlement == null || settlement.Destroyed) return;
            var queued = settlement.WarQueue.Count + settlement.PortQueue.Count;
            if (Ships.Count(ship => ship.Owner == faction) + queued >= Rules.FleetLimit) return;

            var definition = ShipCatalog.Get(shipClass);
            var wood = definition.CostWood * Rules.Centi;
            var silver = definition.CostSilver * Rules.Centi;
            if (settlement.Available(ResourceType.Wood) < wood || settlement.Available(ResourceType.Silver) < silver) return;

            settlement.ReservedWood += wood;
            settlement.ReservedSilver += silver;
            var queue = definition.IsEconomic ? settlement.PortQueue : settlement.WarQueue;
            queue.Add(new BuildOrder { Class = shipClass, PaidWood = wood, PaidSilver = silver, TotalTicks = (int)(definition.BuildSeconds * Rules.TickRate) });
        }

        void CancelQueue(int faction, int index, bool portQueue)
        {
            var settlement = SettlementOf(faction);
            if (settlement == null) return;
            var queue = portQueue ? settlement.PortQueue : settlement.WarQueue;
            if (index < 0 || index >= queue.Count) return;

            var order = queue[index];
            queue.RemoveAt(index);
            var unusedWood = order.PaidWood - order.UsedWood;
            var unusedSilver = order.PaidSilver - order.UsedSilver;
            settlement.ReservedWood -= unusedWood;
            settlement.ReservedSilver -= unusedSilver;
            if (!order.Started) return;

            // E04: 80% of the unused part returns; the rest is an organisational loss.
            var lostWood = unusedWood - (long)(unusedWood * Rules.RefundAfterStart);
            var lostSilver = unusedSilver - (long)(unusedSilver * Rules.RefundAfterStart);
            settlement.Wood -= lostWood;
            settlement.Silver -= lostSilver;
            LostWood += lostWood;
            LostSilver += lostSilver;
        }

        void StepProduction()
        {
            foreach (var settlement in Settlements)
            {
                if (settlement.Destroyed) continue;
                StepLine(settlement, settlement.WarQueue);
                StepLine(settlement, settlement.PortQueue);
            }
        }

        void StepLine(Settlement settlement, System.Collections.Generic.List<BuildOrder> queue)
        {
            if (queue.Count == 0) return;
            var order = queue[0];
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

            if (order.DoneTicks < order.TotalTicks) return;
            queue.RemoveAt(0);
            var direction = settlement.Owner == 0 ? 1 : -1;
            var ship = SpawnShip(settlement.Owner, order.Class, settlement.X + direction * 45, settlement.Y + (Tick % 5 - 2) * 25);
            Emit("ship_built", ship.Id, settlement.Id, ship.X, ship.Y, 0);
        }

        // ---------- Gathering: two berths, FIFO, shared cap, finite stock ----------

        void StepHarvest(Ship ship)
        {
            var node = FindNode(ship.NodeId);
            var settlement = SettlementOf(ship.Owner);
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
                    else if (Distance(ship.X, ship.Y, node.X, node.Y) <= Rules.NodeWorkRange)
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
                else if (node.Workers.Contains(ship.Id)) ship.Harvest = HarvestPhase.Working;
            }

            if (ship.Harvest == HarvestPhase.Working)
            {
                StopShip(ship);
                if (node == null || node.Stock <= 0 || ship.Cargo >= definition.Capacity * Rules.Centi)
                {
                    LeaveNode(ship);
                    ship.Harvest = ship.Cargo > 0 ? HarvestPhase.ToPort : HarvestPhase.ToNode;
                }
                return;
            }

            if (ship.Harvest == HarvestPhase.ToPort)
            {
                if (settlement == null || settlement.Destroyed) { ship.Order = OrderKind.Idle; StopShip(ship); return; }
                if (Distance(ship.X, ship.Y, settlement.X, settlement.Y) <= Rules.PortRange)
                {
                    StopShip(ship);
                    settlement.PortWaiting.Add(ship.Id);
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

        void StepWorkers()
        {
            foreach (var node in Nodes)
            {
                if (node.Workers.Count == 0) continue;
                var workers = node.Workers.Select(FindShip).Where(ship => ship != null && ship.Harvest == HarvestPhase.Working).OrderBy(ship => ship.Id).ToList();
                if (workers.Count == 0) continue;

                double Rate(Ship ship) => node.Type == ResourceType.Wood ? ship.Definition.WoodWork : ship.Definition.SilverWork;
                var total = workers.Sum(Rate);
                var cap = node.Type == ResourceType.Wood ? Rules.WoodNodeCap : Rules.SilverNodeCap;
                var share = total > cap ? cap / total : 1.0;

                foreach (var ship in workers)
                {
                    ship.WorkRemainder += Rate(ship) * share * Rules.Dt * Rules.Centi;
                    var whole = (long)ship.WorkRemainder;
                    ship.WorkRemainder -= whole;
                    whole = Math.Min(whole, Math.Min(node.Stock, ship.Definition.Capacity * Rules.Centi - ship.Cargo));
                    if (whole <= 0) continue;
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
            if (ship.Cargo <= 0) return;
            var kept = (long)(ship.Cargo * Rules.WreckCargoFraction);
            var lost = ship.Cargo - kept;
            if (ship.CargoType == ResourceType.Wood) LostWood += lost; else LostSilver += lost;
            ship.Cargo = 0;
            if (kept <= 0) return;
            Wrecks.Add(new Wreck
            {
                Id = NextId++, X = ship.X, Y = ship.Y,
                Wood = ship.CargoType == ResourceType.Wood ? kept : 0,
                Silver = ship.CargoType == ResourceType.Silver ? kept : 0,
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

        void StepRepair(Ship ship)
        {
            var settlement = SettlementOf(ship.Owner);
            var definition = ship.Definition;
            if (settlement == null || settlement.Destroyed || ship.Hull >= definition.Hull)
            {
                ReleaseDock(ship);
                ship.Order = OrderKind.Idle;
                return;
            }

            if (Distance(ship.X, ship.Y, settlement.X, settlement.Y) > Rules.PortRange)
            {
                Steer(ship, settlement.X, settlement.Y, definition.Speed, Rules.PortRange * 0.8);
                return;
            }

            StopShip(ship);
            if (settlement.RepairShipId != ship.Id)
            {
                if (settlement.RepairShipId >= 0) return;
                settlement.RepairShipId = ship.Id;
            }

            // E06: 25% of the construction cost for a full repair, charged by restored hull.
            var hull = Math.Min(Rules.RepairHullPerSecond * Rules.Dt, definition.Hull - ship.Hull);
            var share = hull / definition.Hull * Rules.RepairFullFraction;
            var wood = ship.RepairWoodRemainder + ship.PaidConstructionWood * share;
            var silver = ship.RepairSilverRemainder + ship.PaidConstructionSilver * share;
            var wholeWood = (long)wood;
            var wholeSilver = (long)silver;
            if (settlement.Available(ResourceType.Wood) < wholeWood || settlement.Available(ResourceType.Silver) < wholeSilver) return;

            settlement.Wood -= wholeWood;
            settlement.Silver -= wholeSilver;
            SpentWood += wholeWood;
            SpentSilver += wholeSilver;
            ship.RepairWoodRemainder = wood - wholeWood;
            ship.RepairSilverRemainder = silver - wholeSilver;
            ship.Hull += hull;
        }

        void StepScrap(Ship ship)
        {
            var settlement = SettlementOf(ship.Owner);
            if (settlement == null || settlement.Destroyed) { ship.Order = OrderKind.Idle; return; }
            if (Distance(ship.X, ship.Y, settlement.X, settlement.Y) > Rules.PortRange)
            {
                Steer(ship, settlement.X, settlement.Y, ship.Definition.Speed, Rules.PortRange * 0.8);
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
