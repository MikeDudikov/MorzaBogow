using System;
using System.Collections.Generic;
using System.Linq;

namespace MorzaBogow.Simulation
{
    /// <summary>
    /// Fixed-step simulation. Step order follows C05: commands, movement, collisions and rams, hits,
    /// crew losses, boarding, owner change, deaths, victory. Rendering never writes to this state.
    /// </summary>
    public sealed partial class World
    {
        public int Tick;
        public int NextId = 1;
        public int NextEventId = 1;
        public DeterministicRandom Random;
        public OutcomeKind Outcome = OutcomeKind.InProgress;
        public int Winner = -1;

        public readonly List<Island> Islands = new List<Island>();
        public readonly List<ResourceNode> Nodes = new List<ResourceNode>();
        public readonly List<Settlement> Settlements = new List<Settlement>();
        public readonly List<Ship> Ships = new List<Ship>();
        public readonly List<BoardingLink> Boardings = new List<BoardingLink>();
        public readonly List<Wreck> Wrecks = new List<Wreck>();

        /// <summary>Not saved: pending commands are applied on the next step and events are transient.</summary>
        public readonly List<Command> Pending = new List<Command>();
        public readonly List<SimEvent> Events = new List<SimEvent>();

        public World(ulong seed)
        {
            Random = new DeterministicRandom(seed);
        }

        public Ship FindShip(int id)
        {
            for (var i = 0; i < Ships.Count; i++) if (Ships[i].Id == id) return Ships[i];
            return null;
        }

        public ResourceNode FindNode(int id) => Nodes.FirstOrDefault(node => node.Id == id);
        public Settlement SettlementOf(int faction) => Settlements.FirstOrDefault(item => item.Owner == faction);
        public void Issue(Command command) => Pending.Add(command);

        public Ship SpawnShip(int owner, ShipClass shipClass, double x, double y, bool fullCrew = true)
        {
            var definition = ShipCatalog.Get(shipClass);
            var ship = new Ship
            {
                Id = NextId++, Owner = owner, Class = shipClass, X = x, Y = y, Hull = definition.Hull,
                Sailors = definition.StandardSailors, Heading = owner == 0 ? 0 : Math.PI,
                PaidConstructionWood = TemplatePricing.ConstructionWood(definition),
                PaidConstructionSilver = TemplatePricing.ConstructionSilver(definition),
            };
            if (fullCrew) ship.Soldiers = definition.StandardSoldiers;
            Ships.Add(ship);
            return ship;
        }

        public void Step()
        {
            if (Outcome != OutcomeKind.InProgress) { Pending.Clear(); return; }
            Tick++;
            ApplyCommands();
            AssignBerths();
            StepProduction();
            StepOrdersAndMovement();
            StepRams();
            StepWeapons();
            StepBoarding();
            StepRecovery();
            StepWrecks();
            RemoveDead();
            UpdateKnowledge();
            CheckVictory();
        }

        void Emit(string type, int subject, int other, double x, double y, double amount)
        {
            Events.Add(new SimEvent { EventId = NextEventId++, Tick = Tick, Type = type, SubjectId = subject, OtherId = other, X = x, Y = y, Amount = amount });
        }

        // ---------- Visibility: logic fog of war, also used by the AI ----------

        public bool CanSee(int faction, double x, double y)
        {
            foreach (var ship in Ships)
            {
                if (ship.Owner != faction) continue;
                var range = ship.Definition.Vision;
                if (Distance2(ship.X, ship.Y, x, y) <= range * range) return true;
            }
            foreach (var settlement in Settlements)
            {
                if (settlement.Owner != faction || settlement.Destroyed) continue;
                if (Distance2(settlement.X, settlement.Y, x, y) <= Rules.SettlementVision * Rules.SettlementVision) return true;
            }
            return false;
        }

        public bool CanSee(int faction, Ship ship) => ship.Owner == faction || CanSee(faction, ship.X, ship.Y);

        void UpdateKnowledge()
        {
            if (Tick % 15 != 0) return;
            RefreshExplored();
            foreach (var node in Nodes)
            {
                for (var faction = 0; faction < 2; faction++)
                {
                    if ((node.KnownMask & (1 << faction)) == 0 && CanSee(faction, node.X, node.Y)) node.KnownMask |= 1 << faction;
                }
            }
        }

        // ---------- Commands: single validation point (C12) ----------

        void ApplyCommands()
        {
            foreach (var command in Pending) Apply(command);
            Pending.Clear();
        }

        void Apply(Command command)
        {
            switch (command.Kind)
            {
                case CommandKind.QueueShip: QueueShip(command.Faction, command.Class); return;
                case CommandKind.CancelQueue: CancelQueue(command.Faction, command.QueueIndex, command.PortQueue); return;
            }

            foreach (var id in command.ShipIds.OrderBy(value => value))
            {
                var ship = FindShip(id);
                if (ship == null || ship.Owner != command.Faction) continue;
                ApplyToShip(ship, command);
            }
        }

        void ApplyToShip(Ship ship, Command command)
        {
            switch (command.Kind)
            {
                case CommandKind.Stop:
                    ClearOrder(ship);
                    break;
                case CommandKind.Move:
                    ClearOrder(ship);
                    ship.Order = OrderKind.Move;
                    ship.TargetX = command.X;
                    ship.TargetY = command.Y;
                    break;
                case CommandKind.Harvest:
                    var node = FindNode(command.TargetId);
                    if (node == null || !ship.Definition.CanHarvest) return;
                    ClearOrder(ship);
                    ship.Order = OrderKind.Harvest;
                    ship.NodeId = node.Id;
                    ship.Harvest = HarvestPhase.ToNode;
                    break;
                case CommandKind.AttackShip:
                case CommandKind.Ram:
                case CommandKind.Board:
                    var target = FindShip(command.TargetId);
                    if (target == null || target.Owner == ship.Owner || !CanSee(ship.Owner, target)) return;
                    if (command.Kind == CommandKind.AttackShip && ship.Definition.LightSlots == 0) return;
                    if (command.Kind == CommandKind.Ram && ship.Class != ShipClass.Ram) return;
                    if (command.Kind == CommandKind.Board && (ship.Class == ShipClass.Cutter || ship.Soldiers < Rules.BoardMinSoldiers)) return;
                    ClearOrder(ship);
                    ship.TargetId = target.Id;
                    ship.Order = command.Kind == CommandKind.AttackShip ? OrderKind.AttackShip
                        : command.Kind == CommandKind.Ram ? OrderKind.Ram : OrderKind.Board;
                    ship.RamState = RamPhase.Approach;
                    break;
                case CommandKind.Salvage:
                    if (!ship.Definition.CanHarvest || Wrecks.All(wreck => wreck.Id != command.TargetId)) return;
                    ClearOrder(ship);
                    ship.Order = OrderKind.Salvage;
                    ship.WreckId = command.TargetId;
                    break;
                case CommandKind.Repair:
                    if (ship.Hull >= ship.Definition.Hull) return;
                    ClearOrder(ship);
                    ship.Order = OrderKind.Repair;
                    break;
                case CommandKind.Scrap:
                    if (ship.Cargo > 0) return;
                    ClearOrder(ship);
                    ship.Order = OrderKind.Scrap;
                    ship.ScrapTicks = 0;
                    break;
                case CommandKind.AttackSettlement:
                    var settlement = Settlements.FirstOrDefault(item => item.Id == command.TargetId);
                    if (settlement == null || settlement.Owner == ship.Owner || settlement.Destroyed || ship.Definition.LightSlots == 0) return;
                    if (!CanSee(ship.Owner, settlement.X, settlement.Y)) return;
                    ClearOrder(ship);
                    ship.Order = OrderKind.AttackSettlement;
                    ship.TargetId = settlement.Id;
                    break;
            }
        }

        void ClearOrder(Ship ship)
        {
            Boardings.RemoveAll(link => link.AttackerId == ship.Id);
            ReleaseDock(ship);
            LeaveNode(ship);
            ship.Order = OrderKind.Idle;
            ship.TargetId = -1;
            ship.NodeId = -1;
            ship.WreckId = -1;
        }

        // ---------- Movement (C02): limited turning, no lateral movement ----------

        void StepOrdersAndMovement()
        {
            foreach (var ship in Ships.OrderBy(item => item.Id))
            {
                switch (ship.Order)
                {
                    case OrderKind.Idle: StopShip(ship); break;
                    case OrderKind.Move:
                        if (Distance(ship.X, ship.Y, ship.TargetX, ship.TargetY) <= ship.Definition.Radius + 4) { ship.Order = OrderKind.Idle; StopShip(ship); }
                        else Steer(ship, ship.TargetX, ship.TargetY, ship.Definition.Speed, ship.Definition.Radius + 4);
                        break;
                    case OrderKind.Harvest: StepHarvest(ship); break;
                    case OrderKind.Salvage: StepSalvage(ship); break;
                    case OrderKind.Repair: StepRepair(ship); break;
                    case OrderKind.Scrap: StepScrap(ship); break;
                    case OrderKind.AttackShip:
                        var target = FindShip(ship.TargetId);
                        if (target == null || !CanSee(ship.Owner, target)) { ship.Order = OrderKind.Idle; break; }
                        Pursue(ship, target.X, target.Y, Rules.ArcherRange * 0.8);
                        break;
                    case OrderKind.AttackSettlement:
                        var settlement = Settlements.FirstOrDefault(item => item.Id == ship.TargetId);
                        if (settlement == null || settlement.Destroyed) { ship.Order = OrderKind.Idle; break; }
                        Pursue(ship, settlement.X, settlement.Y, Rules.ArcherRange * 0.8);
                        break;
                    case OrderKind.Ram: StepRamMovement(ship); break;
                    case OrderKind.Board: StepBoardMovement(ship); break;
                }

                Integrate(ship);
            }
            StepWorkers();
        }

        void Pursue(Ship ship, double x, double y, double hold)
        {
            if (Distance(ship.X, ship.Y, x, y) <= hold) StopShip(ship);
            else Steer(ship, x, y, ship.Definition.Speed, hold);
        }

        void StopShip(Ship ship) => ship.Speed = Math.Max(0, ship.Speed - ship.Definition.Speed / 4 * Rules.Dt);

        void Steer(Ship ship, double x, double y, double wantedSpeed, double tolerance)
        {
            var aim = AvoidIslands(ship, x, y);
            var desired = Math.Atan2(aim.y - ship.Y, aim.x - ship.X);
            var difference = NormalizeAngle(desired - ship.Heading);
            var turn = ship.Definition.TurnRadiansPerSecond * Rules.Dt;
            ship.Heading = NormalizeAngle(ship.Heading + Math.Max(-turn, Math.Min(turn, difference)));

            var turningFactor = Math.Max(0.25, 1.0 - Math.Abs(difference) / Math.PI);
            var distance = Distance(ship.X, ship.Y, x, y);
            var arrivalFactor = Math.Min(1.0, Math.Max(0.15, (distance - tolerance) / 25.0));
            var target = wantedSpeed * Math.Min(turningFactor, arrivalFactor) * CrewSpeedFactor(ship);
            var acceleration = ship.Definition.Speed / 4 * Rules.Dt;
            ship.Speed = ship.Speed < target ? Math.Min(target, ship.Speed + acceleration) : Math.Max(target, ship.Speed - acceleration);
        }

        static double CrewSpeedFactor(Ship ship)
        {
            var minimum = ship.Definition.BaseSailors;
            var crew = ship.Sailors + ship.Soldiers;
            return crew >= minimum ? 1.0 : (double)crew / minimum;
        }

        (double x, double y) AvoidIslands(Ship ship, double x, double y)
        {
            Island blocker = null;
            var nearest = double.MaxValue;
            foreach (var island in Islands)
            {
                var clearance = island.Radius + ship.Definition.Radius + 12;
                var dx = x - ship.X;
                var dy = y - ship.Y;
                var length2 = dx * dx + dy * dy;
                if (length2 < 1e-6) continue;
                var along = ((island.X - ship.X) * dx + (island.Y - ship.Y) * dy) / length2;
                if (along < 0 || along > 1) continue;
                var closestX = ship.X + along * dx;
                var closestY = ship.Y + along * dy;
                if (Distance(closestX, closestY, island.X, island.Y) >= clearance) continue;
                var distance = along * Math.Sqrt(length2);
                if (distance < nearest) { nearest = distance; blocker = island; }
            }
            if (blocker == null) return (x, y);

            var radius = blocker.Radius + ship.Definition.Radius + 30;
            var toShip = Math.Atan2(ship.Y - blocker.Y, ship.X - blocker.X);
            var toTarget = Math.Atan2(y - blocker.Y, x - blocker.X);
            var side = NormalizeAngle(toTarget - toShip) >= 0 ? 1 : -1;
            var aimAngle = toShip + side * 0.7;
            return (blocker.X + Math.Cos(aimAngle) * radius, blocker.Y + Math.Sin(aimAngle) * radius);
        }

        void Integrate(Ship ship)
        {
            ship.X += Math.Cos(ship.Heading) * ship.Speed * Rules.Dt;
            ship.Y += Math.Sin(ship.Heading) * ship.Speed * Rules.Dt;

            foreach (var island in Islands)
            {
                var minimum = island.Radius + ship.Definition.Radius;
                var distance = Distance(ship.X, ship.Y, island.X, island.Y);
                if (distance >= minimum || distance < 1e-6) continue;
                ship.X = island.X + (ship.X - island.X) / distance * minimum;
                ship.Y = island.Y + (ship.Y - island.Y) / distance * minimum;
                ship.Speed *= 0.5;
            }
        }

        // ---------- Ram (C05): one resolution per charge ----------

        void StepRamMovement(Ship ship)
        {
            var target = FindShip(ship.TargetId);
            if (target == null) { ship.Order = OrderKind.Idle; return; }

            if (ship.RamState == RamPhase.Recoil)
            {
                ship.Speed = -ship.Definition.Speed * 0.2;
                if (--ship.RamTimer <= 0) { ship.RamState = RamPhase.Cooldown; ship.RamTimer = (int)((Rules.RamCooldownSeconds - Rules.RamBackoffSeconds) * Rules.TickRate); ship.Speed = 0; }
                return;
            }

            if (ship.RamState == RamPhase.Cooldown)
            {
                var away = Math.Atan2(ship.Y - target.Y, ship.X - target.X);
                Steer(ship, ship.X + Math.Cos(away) * 80, ship.Y + Math.Sin(away) * 80, ship.Definition.Speed * 0.6, 0);
                if (--ship.RamTimer <= 0) ship.RamState = RamPhase.Approach;
                return;
            }

            if (!CanSee(ship.Owner, target)) { ship.Order = OrderKind.Idle; return; }
            var lead = Distance(ship.X, ship.Y, target.X, target.Y) / Math.Max(1, ship.Definition.Speed);
            lead = Math.Min(lead, 8);
            var aimX = target.X + Math.Cos(target.Heading) * target.Speed * lead;
            var aimY = target.Y + Math.Sin(target.Heading) * target.Speed * lead;
            Steer(ship, aimX, aimY, ship.Definition.Speed, -100);
        }

        void StepRams()
        {
            foreach (var ship in Ships.Where(item => item.Order == OrderKind.Ram && item.RamState == RamPhase.Approach).OrderBy(item => item.Id).ToList())
            {
                var target = FindShip(ship.TargetId);
                if (target == null || ship.Hull <= 0 || target.Hull <= 0) continue;
                var distance = Distance(ship.X, ship.Y, target.X, target.Y);
                if (distance > ship.Definition.Radius + target.Definition.Radius + 2) continue;

                var toTarget = Math.Atan2(target.Y - ship.Y, target.X - ship.X);
                var bowError = Math.Abs(NormalizeAngle(toTarget - ship.Heading));
                var closing = ship.Speed * Math.Cos(bowError)
                              - target.Speed * Math.Cos(NormalizeAngle(target.Heading - toTarget));

                ship.RamState = RamPhase.Recoil;
                ship.RamTimer = (int)(Rules.RamBackoffSeconds * Rules.TickRate);
                if (bowError > Math.PI / 4 || closing < Rules.RamMinClosing)
                {
                    Emit("ram_failed", ship.Id, target.Id, ship.X, ship.Y, closing);
                    continue;
                }

                var damage = RamDamage(ship, target, closing, bowError);
                ApplyHullDamage(target, damage);
                ApplyHullDamage(ship, damage * Rules.RamRecoilFraction);
                ship.LastRamTick = Tick;
                Emit("ram_hit", ship.Id, target.Id, target.X, target.Y, damage);
            }
        }

        public static double RamDamage(Ship attacker, Ship target, double closing, double bowError)
        {
            var impactDirection = Math.Atan2(target.Y - attacker.Y, target.X - attacker.X);
            var sideness = Math.Abs(Math.Sin(NormalizeAngle(target.Heading - impactDirection)));
            var angle = bowError > 0.35 ? 0.25 : sideness >= 0.7 ? 1.0 : 0.5;
            var massRatio = Math.Max(0.5, Math.Min(1.5, attacker.Definition.Mass / target.Definition.Mass));
            var speed = closing / Rules.RamReferenceSpeed;
            return Math.Min(Rules.RamMaxDamage, Rules.RamBase * speed * speed * angle * massRatio);
        }

        void ApplyHullDamage(Ship ship, double amount)
        {
            ship.Hull -= amount;
            ship.LastDamagedTick = Tick;
        }

        // ---------- Ranged fire: seeded hit roll, reload in simulation time ----------

        void StepWeapons()
        {
            foreach (var ship in Ships.OrderBy(item => item.Id))
            {
                var slots = ship.Definition.LightSlots;
                if (slots == 0) continue;
                if (ship.Reload > 0) { ship.Reload -= Rules.Dt; continue; }

                Ship shipTarget = null;
                Settlement settlementTarget = null;
                if (ship.Order == OrderKind.AttackSettlement) settlementTarget = Settlements.FirstOrDefault(item => item.Id == ship.TargetId);
                else if (ship.Order == OrderKind.AttackShip) shipTarget = FindShip(ship.TargetId);
                if (shipTarget == null && settlementTarget == null && ship.Order != OrderKind.Ram) shipTarget = NearestVisibleEnemyInRange(ship);
                if (shipTarget == null && settlementTarget == null) continue;

                if (settlementTarget != null)
                {
                    var range = Distance(ship.X, ship.Y, settlementTarget.X, settlementTarget.Y);
                    if (range > Rules.ArcherRange || settlementTarget.Destroyed) continue;
                    ship.Reload = Rules.ArcherReloadSeconds;
                    for (var i = 0; i < slots; i++)
                    {
                        if (Random.NextDouble() < Rules.ArcherAccuracy) settlementTarget.Hull -= Rules.ArcherHullDamage;
                    }
                    Emit("volley", ship.Id, settlementTarget.Id, settlementTarget.X, settlementTarget.Y, slots);
                    continue;
                }

                var distance = Distance(ship.X, ship.Y, shipTarget.X, shipTarget.Y);
                if (distance > Rules.ArcherRange || distance < Rules.ArcherMinRange) continue;
                if (HasIslandBetween(ship.X, ship.Y, shipTarget.X, shipTarget.Y)) continue;

                ship.Reload = Rules.ArcherReloadSeconds;
                var movementPenalty = 1.0 - 0.25 * Math.Min(1.0, Math.Abs(ship.Speed) / ship.Definition.Speed);
                for (var i = 0; i < slots; i++)
                {
                    if (Random.NextDouble() >= Rules.ArcherAccuracy * movementPenalty) continue;
                    ApplyHullDamage(shipTarget, Rules.ArcherHullDamage);
                    ApplyCrewDamage(shipTarget, Rules.ArcherCrewDamage);
                }
                Emit("volley", ship.Id, shipTarget.Id, shipTarget.X, shipTarget.Y, slots);
            }
        }

        Ship NearestVisibleEnemyInRange(Ship ship)
        {
            Ship best = null;
            var bestDistance = double.MaxValue;
            foreach (var other in Ships)
            {
                if (other.Owner == ship.Owner || !CanSee(ship.Owner, other)) continue;
                var distance = Distance(ship.X, ship.Y, other.X, other.Y);
                if (distance > Rules.ArcherRange || distance < Rules.ArcherMinRange || distance >= bestDistance) continue;
                bestDistance = distance;
                best = other;
            }
            return best;
        }

        public bool HasIslandBetween(double ax, double ay, double bx, double by)
        {
            var dx = bx - ax;
            var dy = by - ay;
            var length2 = dx * dx + dy * dy;
            if (length2 < 1e-9) return false;
            foreach (var island in Islands)
            {
                var along = Math.Max(0, Math.Min(1, ((island.X - ax) * dx + (island.Y - ay) * dy) / length2));
                if (Distance(ax + along * dx, ay + along * dy, island.X, island.Y) < island.Radius) return true;
            }
            return false;
        }

        void ApplyCrewDamage(Ship ship, double amount)
        {
            ship.CrewDamage += amount;
            while (ship.CrewDamage >= Rules.SoldierHitPoints && ship.Sailors + ship.Soldiers > 0)
            {
                ship.CrewDamage -= Rules.SoldierHitPoints;
                KillCrew(ship, 1);
            }
        }

        static void KillCrew(Ship ship, int count)
        {
            var soldiers = Math.Min(ship.Soldiers, count);
            ship.Soldiers -= soldiers;
            ship.Sailors = Math.Max(0, ship.Sailors - (count - soldiers));
            ship.Morale = Math.Max(0, ship.Morale - count * 0.5);
        }

        // ---------- Boarding (C05): no instant ownership change ----------

        void StepBoardMovement(Ship ship)
        {
            var target = FindShip(ship.TargetId);
            if (target == null) { ship.Order = OrderKind.Idle; return; }
            var link = Boardings.FirstOrDefault(item => item.AttackerId == ship.Id);
            if (link != null) { ship.Speed = target.Speed; ship.Heading = target.Heading; return; }

            if (!CanSee(ship.Owner, target)) { ship.Order = OrderKind.Idle; return; }
            var gap = Distance(ship.X, ship.Y, target.X, target.Y) - ship.Definition.Radius - target.Definition.Radius;
            if (gap > Rules.BoardGap)
            {
                Steer(ship, target.X, target.Y, ship.Definition.Speed, ship.Definition.Radius + target.Definition.Radius);
                return;
            }
            var relative = Math.Abs(ship.Speed - target.Speed);
            ship.Speed = target.Speed > ship.Speed ? Math.Min(target.Speed, ship.Speed + ship.Definition.Speed / 4 * Rules.Dt)
                : Math.Max(target.Speed, ship.Speed - ship.Definition.Speed / 4 * Rules.Dt);
            if (relative > Rules.BoardRelativeSpeed) return;
            if (Boardings.Any(item => item.TargetId == target.Id)) return;
            Boardings.Add(new BoardingLink { AttackerId = ship.Id, TargetId = target.Id, HookTicks = (int)(Rules.BoardHookSeconds * Rules.TickRate) });
            Emit("hooks_thrown", ship.Id, target.Id, target.X, target.Y, 0);
        }

        void StepBoarding()
        {
            foreach (var link in Boardings.OrderBy(item => item.AttackerId).ToList())
            {
                var attacker = FindShip(link.AttackerId);
                var target = FindShip(link.TargetId);
                if (attacker == null || target == null || attacker.Hull <= 0 || target.Hull <= 0 || attacker.Owner == target.Owner)
                {
                    Boardings.Remove(link);
                    continue;
                }

                if (!link.Hooked)
                {
                    var gap = Distance(attacker.X, attacker.Y, target.X, target.Y) - attacker.Definition.Radius - target.Definition.Radius;
                    if (gap > Rules.BoardGap * 2 || target.Speed - attacker.Speed > Rules.BoardRelativeSpeed)
                    {
                        Boardings.Remove(link);
                        attacker.Order = OrderKind.Idle;
                        Emit("hooks_cut", attacker.Id, target.Id, target.X, target.Y, 0);
                        continue;
                    }
                    if (--link.HookTicks <= 0) link.Hooked = true;
                    continue;
                }

                target.Speed = attacker.Speed = Math.Max(0, Math.Min(attacker.Speed, target.Speed) - 1.0 * Rules.Dt);
                if (++link.CycleTicks >= Rules.TickRate)
                {
                    link.CycleTicks = 0;
                    var attack = attacker.Soldiers * (0.5 + attacker.Morale / 100.0);
                    var defend = target.Soldiers * (0.5 + target.Morale / 100.0);
                    var attackerLosses = (int)Math.Ceiling(defend * Rules.BoardLossFraction);
                    var targetLosses = (int)Math.Ceiling(attack * Rules.BoardLossFraction);
                    attacker.Soldiers = Math.Max(0, attacker.Soldiers - attackerLosses);
                    target.Soldiers = Math.Max(0, target.Soldiers - targetLosses);
                    attacker.Morale = Math.Max(0, attacker.Morale - attackerLosses * 0.5);
                    target.Morale = Math.Max(0, target.Morale - targetLosses * 0.5);
                    attacker.LastDamagedTick = target.LastDamagedTick = Tick;
                }

                if (attacker.Soldiers <= 0)
                {
                    Boardings.Remove(link);
                    attacker.Order = OrderKind.Idle;
                    Emit("boarding_repelled", attacker.Id, target.Id, target.X, target.Y, 0);
                    continue;
                }

                if (target.Soldiers > 0) { link.ControlTicks = 0; continue; }
                if (++link.ControlTicks < Rules.BoardControlSeconds * Rules.TickRate) continue;

                Capture(attacker, target);
                Boardings.Remove(link);
            }
        }

        void Capture(Ship attacker, Ship target)
        {
            var transfer = Math.Min(attacker.Soldiers, target.Definition.BaseSailors);
            attacker.Soldiers -= transfer;
            target.Sailors = transfer;
            target.Soldiers = 0;
            target.Owner = attacker.Owner;
            target.Morale = 50;
            ClearOrder(target);
            foreach (var other in Ships.Where(item => item.TargetId == target.Id && item.Owner == attacker.Owner)) other.Order = OrderKind.Idle;
            attacker.Order = OrderKind.Idle;
            attacker.TargetId = -1;
            Emit("ship_captured", target.Id, attacker.Id, target.X, target.Y, 0);
        }

        // ---------- Recovery, deaths, victory ----------

        void StepRecovery()
        {
            foreach (var ship in Ships)
            {
                if (Tick - ship.LastDamagedTick > 10 * Rules.TickRate) ship.Morale = Math.Min(100, ship.Morale + 0.2 * Rules.Dt);
            }
        }

        void RemoveDead()
        {
            foreach (var ship in Ships.Where(item => item.Hull <= 0).ToList())
            {
                if (!ship.Scrapped)
                {
                    Emit("ship_sunk", ship.Id, -1, ship.X, ship.Y, ship.Cargo);
                    CreateWreck(ship);
                }
                ReleaseDock(ship);
                LeaveNode(ship);
                Boardings.RemoveAll(link => link.AttackerId == ship.Id || link.TargetId == ship.Id);
                foreach (var other in Ships.Where(item => item.Order != OrderKind.Idle && item.Order != OrderKind.Move && item.Order != OrderKind.AttackSettlement && item.Order != OrderKind.Harvest && item.TargetId == ship.Id))
                {
                    other.Order = OrderKind.Idle;
                    other.TargetId = -1;
                }
                Ships.Remove(ship);
            }
        }

        bool FactionDefeated(int faction)
        {
            var settlement = SettlementOf(faction);
            if (settlement == null || settlement.Destroyed) return true;
            if (Ships.Any(ship => ship.Owner == faction) || settlement.WarQueue.Count > 0 || settlement.PortQueue.Count > 0) return false;
            var cutter = ShipCatalog.Get(ShipClass.Cutter);
            return settlement.Available(ResourceType.Wood) < cutter.CostWood * Rules.Centi
                   || settlement.Available(ResourceType.Silver) < cutter.CostSilver * Rules.Centi;
        }

        void CheckVictory()
        {
            var first = FactionDefeated(0);
            var second = FactionDefeated(1);
            if (!first && !second) return;
            Outcome = first && second ? OutcomeKind.Draw : OutcomeKind.Won;
            Winner = first && second ? -1 : first ? 1 : 0;
            Emit("match_ended", Winner, -1, 0, 0, 0);
        }

        // ---------- Math ----------

        public static double Distance(double ax, double ay, double bx, double by) => Math.Sqrt(Distance2(ax, ay, bx, by));

        internal static double Distance2(double ax, double ay, double bx, double by)
        {
            var dx = ax - bx;
            var dy = ay - by;
            return dx * dx + dy * dy;
        }

        public static double NormalizeAngle(double angle)
        {
            while (angle > Math.PI) angle -= 2 * Math.PI;
            while (angle < -Math.PI) angle += 2 * Math.PI;
            return angle;
        }
    }
}
