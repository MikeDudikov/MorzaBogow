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

        /// <summary>The scenario's intended length; MapFactory laid the map out for it and the opponent paces itself by it.</summary>
        public MatchLength Length = MatchLength.Standard;
        public MatchProfile Profile => MatchProfile.Of(Length);

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

        /// <summary>The faction's main settlement. Its loss ends the match; a bridgehead's loss does not.</summary>
        public Settlement SettlementOf(int faction) => Settlements.FirstOrDefault(item => item.Owner == faction && !item.IsOutpost);
        public void Issue(Command command) => Pending.Add(command);

        public Ship SpawnShip(int owner, ShipClass shipClass, double x, double y, bool fullCrew = true)
        {
            var definition = ShipCatalog.Get(shipClass);
            var ship = new Ship
            {
                Id = NextId++, Owner = owner, Class = shipClass, X = x, Y = y, Hull = definition.Hull,
                Heading = owner == 0 ? 0 : Math.PI,
                PaidConstructionWood = TemplatePricing.ConstructionWood(definition),
                PaidConstructionSilver = TemplatePricing.ConstructionSilver(definition),
                Plan = definition.PlanFor(definition.DefaultPreset),
            };
            ship.Sailors = ship.Plan.Sailors;
            if (fullCrew) { ship.Swordsmen = ship.Plan.Swordsmen; ship.Javelins = ship.Plan.Javelins; ship.Archers = ship.Plan.Archers; }
            Fit(ship, definition.DefaultLoadout, definition.ItemValues());
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
            StepRegrowth();
            StepOrdersAndMovement();
            StepRams();
            StepWeapons();
            StepProjectiles();
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
                case CommandKind.QueueShip: QueueShip(command.Faction, command.Class, command.TargetId, command.BlueprintId); return;
                case CommandKind.BuildProject: BeginProject(command.Faction, command.TargetId, command.Project); return;
                case CommandKind.CancelProject: CancelProject(command.Faction, command.TargetId); return;
                case CommandKind.QueueWeapon: QueueWeapon(command.Faction, command.TargetId, command.Weapon); return;
                case CommandKind.CancelWeapon: CancelWeapon(command.Faction, command.TargetId, command.QueueIndex); return;
                case CommandKind.SaveBlueprint: SaveBlueprint(command); return;
                case CommandKind.CancelQueue: CancelQueue(command.Faction, command.QueueIndex, command.PortQueue, command.TargetId); return;
                case CommandKind.BuildShipyard: BeginShipyard(command.Faction, command.TargetId); return;
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
                    if (command.Kind == CommandKind.AttackShip && !HasWeapons(ship)) return;
                    // Every warship carries a ram (docs/UZBROJENIE.md); working ships do not.
                    if (command.Kind == CommandKind.Ram && !ship.Definition.CanRam) return;
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
                    // A sound hull still goes to the dock when its crew is short of the plan or has wounded.
                    if (ship.Hull >= ship.Definition.Hull && !NeedsMuster(ship) && !ModulesDamaged(ship) && !NeedsRefit(ship, ClosestYard(ship))) return;
                    ClearOrder(ship);
                    ship.Order = OrderKind.Repair;
                    break;
                case CommandKind.SetLoadout:
                    // Only the plan: weapons change at a port (Refit); missing ones are ordered from its workshop.
                    SetLoadout(ship, command.Loadout);
                    break;
                case CommandKind.SetCrewPlan:
                    // Only a plan: the people change at a port (Muster), not at sea.
                    ship.Plan = command.Preset == CrewPreset.Custom ? ship.Definition.Clamp(command.Plan) : ship.Definition.PlanFor(command.Preset);
                    break;
                case CommandKind.Scrap:
                    if (ship.Cargo > 0 || ship.HoldWood + ship.HoldSilver > 0) return;
                    ClearOrder(ship);
                    ship.Order = OrderKind.Scrap;
                    ship.ScrapTicks = 0;
                    break;
                case CommandKind.Transfer: BeginTransfer(ship, command); break;
                case CommandKind.FoundOutpost: BeginExpedition(ship, command); break;
                case CommandKind.Escort: BeginEscort(ship, command); break;
                case CommandKind.AttackSettlement:
                    var settlement = Settlements.FirstOrDefault(item => item.Id == command.TargetId);
                    if (settlement == null || settlement.Owner == ship.Owner || settlement.Destroyed || !HasWeapons(ship)) return;
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
            ReleaseTransport(ship);
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
                    case OrderKind.Idle:
                        SendToDockIfNeeded(ship);
                        StopShip(ship);
                        break;
                    case OrderKind.Move:
                        MoveTargetToWater(ship);
                        if (Distance(ship.X, ship.Y, ship.TargetX, ship.TargetY) <= ship.Definition.Radius + 4) { ship.Order = OrderKind.Idle; StopShip(ship); }
                        else Steer(ship, ship.TargetX, ship.TargetY, ship.Definition.Speed, ship.Definition.Radius + 4);
                        break;
                    case OrderKind.Harvest: StepHarvest(ship); break;
                    case OrderKind.Salvage: StepSalvage(ship); break;
                    case OrderKind.Repair: StepRepair(ship); break;
                    case OrderKind.Scrap: StepScrap(ship); break;
                    case OrderKind.Transfer:
                    case OrderKind.Expedition: StepTransport(ship); break;
                    case OrderKind.Escort: StepEscort(ship); break;
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
            ResolveShipContacts();
            StepWorkers();
            // Loading just deepened some hulls: they ease off the shallows they no longer clear.
            foreach (var ship in Ships) ResolveLand(ship);
        }

        /// <summary>A target on land moves to the nearest water, so the ship stops at the shore instead of circling.</summary>
        void MoveTargetToWater(Ship ship)
        {
            var minimum = LandClearance(ship) + 1;
            foreach (var island in Islands)
            {
                if (Distance(ship.TargetX, ship.TargetY, island.X, island.Y) > island.Radius + minimum) continue;
                var distance = LandShape.SignedDistance(island, ship.TargetX, ship.TargetY, out var nx, out var ny);
                if (distance >= minimum) continue;
                ship.TargetX += nx * (minimum - distance);
                ship.TargetY += ny * (minimum - distance);
            }
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
            desired = AvoidHulls(ship, desired, Distance(ship.X, ship.Y, aim.x, aim.y));
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

        /// <summary>
        /// Turns the course past the nearest hull lying across it within a few seconds' sailing, so ships go round
        /// one another instead of running into each other. A ram's or boarder's own target is not avoided, and a
        /// ship moving away as fast is no obstacle. Ships are taken in list order, so it is deterministic.
        /// </summary>
        double AvoidHulls(Ship ship, double desired, double reach)
        {
            var definition = ship.Definition;
            var look = Math.Min(reach, definition.Radius * 2 + Math.Max(0, ship.Speed) * 3 + 10);
            if (look <= 0) return desired;
            var dx = Math.Cos(desired);
            var dy = Math.Sin(desired);
            var closest = double.MaxValue;
            var turn = 0.0;
            foreach (var other in Ships)
            {
                if (other == ship) continue;
                if ((ship.Order == OrderKind.Ram || ship.Order == OrderKind.Board) && other.Id == ship.TargetId) continue;
                var rx = other.X - ship.X;
                var ry = other.Y - ship.Y;
                var along = rx * dx + ry * dy;
                var otherDefinition = other.Definition;
                if (along <= 0 || along - otherDefinition.Radius > look || along >= closest) continue;
                if (other.Speed * Math.Cos(other.Heading - desired) >= Math.Max(0, ship.Speed) * 0.9 && other.Speed > 0.5) continue;

                var across = -rx * dy + ry * dx;
                var otherHalf = Math.Max(0, otherDefinition.Radius - otherDefinition.Beam / 2);
                // Room for the oars: one's own always while rowing, the other's while it is under way too.
                var oars = Rules.OarReach(definition) + (Math.Abs(other.Speed) > 0.5 ? Rules.OarReach(otherDefinition) : 0);
                var need = (definition.Beam + otherDefinition.Beam) / 2 + Math.Abs(Math.Sin(other.Heading - desired)) * otherHalf + oars + 1;
                if (Math.Abs(across) >= need) continue;
                closest = along;
                // Pass with the other hull on the side it already lies, clearing its nearer end, so a ship close
                // behind a long hull turns hard enough to slide past it.
                var ownHalf = Math.Max(0, definition.Radius - definition.Beam / 2);
                var nearEnd = Math.Max(1, along - Math.Abs(Math.Cos(other.Heading - desired)) * otherHalf - ownHalf);
                turn = Math.Atan2(across >= 0 ? across - need : across + need, nearEnd);
            }
            return closest == double.MaxValue ? desired : NormalizeAngle(desired + turn);
        }

        /// <summary>
        /// Only sailors row: fewer than the hull's 25% slows it in proportion, and the Seamen crew's spare oars make it
        /// a little faster. Soldiers no longer stand in for missing rowers; the wounded row no more.
        /// </summary>
        public static double CrewSpeedFactor(Ship ship)
        {
            var definition = ship.Definition;
            var minimum = definition.BaseSailors;
            var factor = ship.Sailors >= minimum ? 1.0 : (double)ship.Sailors / minimum;
            if (!definition.IsEconomic && ship.Sailors >= (int)Math.Ceiling(definition.MaxCrew * Rules.SeamenSailorShare)) factor *= 1 + Rules.SeamenSpeedBonus;
            return factor;
        }

        void Integrate(Ship ship)
        {
            ship.X += Math.Cos(ship.Heading) * ship.Speed * Rules.Dt;
            ship.Y += Math.Sin(ship.Heading) * ship.Speed * Rules.Dt;
            ResolveLand(ship);
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
                // Another charge needs the cooldown and a hull's length of sea room (docs/UZBROJENIE.md).
                var away = Math.Atan2(ship.Y - target.Y, ship.X - target.X);
                Steer(ship, ship.X + Math.Cos(away) * 80, ship.Y + Math.Sin(away) * 80, ship.Definition.Speed * 0.6, 0);
                if (ship.RamTimer > 0) ship.RamTimer--;
                var room = Distance(ship.X, ship.Y, target.X, target.Y) - ship.Definition.Radius - target.Definition.Radius;
                if (ship.RamTimer <= 0 && room >= ship.Definition.Radius * 2) ship.RamState = RamPhase.Approach;
                return;
            }

            if (!CanSee(ship.Owner, target)) { ship.Order = OrderKind.Idle; return; }
            var gap = Distance(ship.X, ship.Y, target.X, target.Y) - ship.Definition.Radius - target.Definition.Radius;
            // The charge is called when the target is a few seconds' sailing off: the drums quicken for all to see.
            if (ship.RamState == RamPhase.Approach && gap <= ship.Definition.Speed * Rules.RamCallSeconds)
            {
                ship.RamState = RamPhase.Telegraph;
                ship.TelegraphTicks = (int)Math.Ceiling(Rules.RamTelegraphSeconds * Rules.TickRate);
                Emit("ram_telegraph", ship.Id, target.Id, ship.X, ship.Y, 0);
            }
            if (ship.RamState == RamPhase.Telegraph && ship.TelegraphTicks > 0) ship.TelegraphTicks--;
            var lead = Distance(ship.X, ship.Y, target.X, target.Y) / Math.Max(1, ship.Definition.Speed);
            lead = Math.Min(lead, 8);
            var aimX = target.X + Math.Cos(target.Heading) * target.Speed * lead;
            var aimY = target.Y + Math.Sin(target.Heading) * target.Speed * lead;
            Steer(ship, aimX, aimY, ship.Definition.Speed, -100);
        }

        /// <summary>
        /// Contact during a charge resolves once. Only a charge called at least RamTelegraphSeconds before strikes; an
        /// uncalled or early contact, a glancing bow or too slow a closing fails, and every outcome goes through recoil
        /// and cooldown, so orders cannot be spammed past the telegraph.
        /// </summary>
        void StepRams()
        {
            foreach (var ship in Ships.Where(item => item.Order == OrderKind.Ram && (item.RamState == RamPhase.Approach || item.RamState == RamPhase.Telegraph)).OrderBy(item => item.Id).ToList())
            {
                var target = FindShip(ship.TargetId);
                if (target == null || ship.Hull <= 0 || target.Hull <= 0) continue;
                var distance = Distance(ship.X, ship.Y, target.X, target.Y);
                if (distance > ship.Definition.Radius + target.Definition.Radius + 2) continue;

                var toTarget = Math.Atan2(target.Y - ship.Y, target.X - ship.X);
                var bowError = Math.Abs(NormalizeAngle(toTarget - ship.Heading));
                var closing = ship.Speed * Math.Cos(bowError)
                              - target.Speed * Math.Cos(NormalizeAngle(target.Heading - toTarget));
                var called = ship.RamState == RamPhase.Telegraph && ship.TelegraphTicks <= 0;

                ship.RamState = RamPhase.Recoil;
                ship.RamTimer = (int)(Rules.RamBackoffSeconds * Rules.TickRate);
                if (!called || bowError > Math.PI / 4 || closing < Rules.RamMinClosing)
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

        /// <summary>
        /// Base damage = min(400, 120 x (closing/8)^2 x the attacker's ram x angle x mass ratio). Angle: abeam 1, from
        /// astern 0.75, head on 0.5, a glancing bow 0.25. Projectile armour does not protect against a ram.
        /// </summary>
        public static double RamDamage(Ship attacker, Ship target, double closing, double bowError)
        {
            var impactDirection = Math.Atan2(target.Y - attacker.Y, target.X - attacker.X);
            var relative = NormalizeAngle(target.Heading - impactDirection);
            var sideness = Math.Abs(Math.Sin(relative));
            var astern = Math.Cos(relative) > 0;
            var angle = bowError > 0.35 ? 0.25 : sideness >= 0.7 ? 1.0 : astern ? Rules.RamSternFactor : 0.5;
            var massRatio = Math.Max(0.5, Math.Min(1.5, attacker.Definition.Mass / target.Definition.Mass));
            var speed = closing / Rules.RamReferenceSpeed;
            return Math.Min(Rules.RamMaxDamage, Rules.RamBase * speed * speed * attacker.Definition.RamMultiplier * angle * massRatio);
        }

        void ApplyHullDamage(Ship ship, double amount)
        {
            ship.Hull -= amount;
            ship.LastDamagedTick = Tick;
        }

        public bool HasIslandBetween(double ax, double ay, double bx, double by)
        {
            var dx = bx - ax;
            var dy = by - ay;
            var length2 = dx * dx + dy * dy;
            if (length2 < 1e-9) return false;
            foreach (var island in Islands)
            {
                if (!island.BlocksShots) continue;
                if (LandShape.PointSegmentDistance(island.X, island.Y, ax, ay, bx, by) > island.Radius) continue;
                if (LandShape.SegmentNear(island, ax, ay, bx, by, 0)) return true;
            }
            return false;
        }

        void ApplyCrewDamage(Ship ship, double amount)
        {
            ship.CrewDamage += amount;
            while (ship.CrewDamage >= Rules.SoldierHitPoints && ship.Crew > 0)
            {
                ship.CrewDamage -= Rules.SoldierHitPoints;
                // Shots find the fighting men on deck first, the most numerous trade first; rowers below last.
                var trade = ship.Soldiers == 0 ? CrewTrade.Sailor
                    : ship.Archers >= ship.Swordsmen && ship.Archers >= ship.Javelins ? CrewTrade.Archer
                    : ship.Swordsmen >= ship.Javelins ? CrewTrade.Swordsman : CrewTrade.Javelin;
                LoseMan(ship, trade);
            }
        }

        public enum CrewTrade { Sailor, Swordsman, Archer, Javelin }

        /// <summary>
        /// One man of a trade falls: of every five, two are wounded (Rules.WoundedShare) and stay aboard unable to fight
        /// or row until a port replaces them; the rest are dead. Morale falls as before.
        /// </summary>
        static void LoseMan(Ship ship, CrewTrade trade)
        {
            switch (trade)
            {
                case CrewTrade.Sailor: if (ship.Sailors == 0) return; ship.Sailors--; break;
                case CrewTrade.Swordsman: if (ship.Swordsmen == 0) return; ship.Swordsmen--; break;
                case CrewTrade.Javelin: if (ship.Javelins == 0) return; ship.Javelins--; break;
                default: if (ship.Archers == 0) return; ship.Archers--; break;
            }
            ship.WoundRemainder += Rules.WoundedShare;
            if (ship.WoundRemainder >= 1 - 1e-9)
            {
                ship.WoundRemainder -= 1;
                ship.Wounded++;
            }
            ship.Morale = Math.Max(0, ship.Morale - 0.5);
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
            Boardings.Add(new BoardingLink { AttackerId = ship.Id, TargetId = target.Id, HookTicks = (int)(Rules.BoardHookSeconds * Rules.TickRate), AttackerWoundedAtStart = ship.Wounded });
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
                    // The defender's swordsmen hack at the lines; the attacker's own swordsmen keep half as many off them.
                    var cutters = target.Swordsmen - attacker.Swordsmen * 0.5;
                    if (cutters > 0) link.CutProgress += cutters * Rules.HookCutPerSwordsman * Rules.Dt;
                    if (gap > Rules.BoardGap * 2 || target.Speed - attacker.Speed > Rules.BoardRelativeSpeed || link.CutProgress >= 1)
                    {
                        Boardings.Remove(link);
                        attacker.Order = OrderKind.Idle;
                        Emit("hooks_cut", attacker.Id, target.Id, target.X, target.Y, link.CutProgress >= 1 ? 1 : 0);
                        continue;
                    }
                    if (--link.HookTicks <= 0) link.Hooked = true;
                    continue;
                }

                target.Speed = attacker.Speed = Math.Max(0, Math.Min(attacker.Speed, target.Speed) - 1.0 * Rules.Dt);
                if (++link.CycleTicks >= Rules.TickRate)
                {
                    link.CycleTicks = 0;
                    FightRound(link, attacker, target);
                }

                if (attacker.Soldiers <= 0)
                {
                    // A boarding lost on the enemy deck costs the whole fighting crew (docs/UZGODNIENIE_SPECYFIKACJI.md):
                    // the wounded of this attack are left there too. The sailors stay at the oars to bring the hull home,
                    // where a port must train new fighters before it can board or hold its own deck again.
                    var lost = Math.Max(0, attacker.Wounded - link.AttackerWoundedAtStart);
                    attacker.Wounded -= lost;
                    Boardings.Remove(link);
                    attacker.Order = OrderKind.Idle;
                    Emit("boarding_repelled", attacker.Id, target.Id, target.X, target.Y, lost);
                    continue;
                }

                // No fighters left, or a broken crew, strikes its colours after the attacker holds the deck a while; a deck
                // without fighters puts up no fight at all (DeckStrength), so it is taken without loss.
                var yielding = target.Soldiers <= 0 || target.Morale < Rules.SurrenderMorale;
                if (!yielding) { link.ControlTicks = 0; continue; }
                if (++link.ControlTicks < Rules.BoardControlSeconds * Rules.TickRate) continue;

                if (target.Soldiers > 0) Emit("ship_surrendered", target.Id, attacker.Id, target.X, target.Y, target.Soldiers);
                Capture(attacker, target);
                Boardings.Remove(link);
            }
        }

        /// <summary>Men who can fight across the rail at once: a few plus one per metre of the narrower deck.</summary>
        public static int BoardingFront(Ship attacker, Ship target) =>
            Rules.BoardFrontBase + (int)Math.Floor(Math.Min(attacker.Definition.Beam, target.Definition.Beam));

        /// <summary>
        /// What a crew brings to one round on deck: the men in the front (swordsmen first, then javelin men, then archers);
        /// the javelin men and archers behind the front throwing and shooting onto the other deck; all scaled by morale. A
        /// defender fights on its own deck: its missiles count for more. Sailors never stand at the rail, so a ship with no
        /// fighters aboard cannot resist a boarding (docs/UZGODNIENIE_SPECYFIKACJI.md). The sailors' place in the tuple
        /// stays for the front order, always empty.
        /// </summary>
        public static (double strength, int swordsmen, int javelins, int archers, int sailors) DeckStrength(Ship ship, int front, bool attacking)
        {
            var swordsmen = Math.Min(ship.Swordsmen, front);
            var javelins = Math.Min(ship.Javelins, front - swordsmen);
            var archers = Math.Min(ship.Archers, front - swordsmen - javelins);
            var melee = attacking
                ? swordsmen * Rules.SwordAttack + javelins * Rules.JavelinAttack + archers * Rules.ArcherAttack
                : swordsmen * Rules.SwordDefence + javelins * Rules.JavelinDefence + archers * Rules.ArcherDefence;
            var support = (ship.Archers - archers + ship.Javelins - javelins) * (attacking ? Rules.ArcherSupportAttack : Rules.ArcherSupportDefence);
            return ((melee + support) * (0.5 + ship.Morale / 100.0), swordsmen, javelins, archers, 0);
        }

        /// <summary>
        /// How a boarding would likely go, above 1 in the attacker's favour: the first round's strength on deck times the
        /// fighters each side can still feed into the front.
        /// </summary>
        public static double BoardingOdds(Ship attacker, Ship target)
        {
            var front = BoardingFront(attacker, target);
            var attack = DeckStrength(attacker, front, true).strength * Math.Max(1, attacker.Soldiers);
            var defend = DeckStrength(target, front, false).strength * Math.Max(1, target.Soldiers);
            return defend <= 0 ? double.MaxValue : attack / defend;
        }

        /// <summary>
        /// One second on the decks (docs/ZALOGA_I_ABORDAZ.md): each side loses Rules.DeckLossFraction of the other's
        /// strength in men, carried in fractions; the men who fall are taken from its front, the most numerous trade
        /// first. Losses also break morale: a crew that loses two thirds of its full strength is near surrender.
        /// </summary>
        void FightRound(BoardingLink link, Ship attacker, Ship target)
        {
            var front = BoardingFront(attacker, target);
            var attack = DeckStrength(attacker, front, true);
            var defend = DeckStrength(target, front, false);
            link.AttackerStrength = attack.strength;
            link.TargetStrength = defend.strength;
            link.AttackerFront = attack.swordsmen + attack.javelins + attack.archers + attack.sailors;
            link.TargetFront = defend.swordsmen + defend.javelins + defend.archers + defend.sailors;

            link.AttackerLossRemainder += defend.strength * Rules.DeckLossFraction;
            link.TargetLossRemainder += attack.strength * Rules.DeckLossFraction;
            var attackerLosses = (int)Math.Floor(link.AttackerLossRemainder);
            var targetLosses = (int)Math.Floor(link.TargetLossRemainder);
            link.AttackerLossRemainder -= attackerLosses;
            link.TargetLossRemainder -= targetLosses;

            Fall(attacker, attackerLosses, new[] { attack.swordsmen, attack.javelins, attack.archers, attack.sailors });
            Fall(target, targetLosses, new[] { defend.swordsmen, defend.javelins, defend.archers, defend.sailors });
            attacker.LastDamagedTick = target.LastDamagedTick = Tick;
        }

        static readonly CrewTrade[] FrontOrder = { CrewTrade.Swordsman, CrewTrade.Javelin, CrewTrade.Archer, CrewTrade.Sailor };

        /// <summary>The fallen come from the front, each time from its most numerous trade (in front order on ties).</summary>
        static void Fall(Ship ship, int losses, int[] front)
        {
            var fallen = 0;
            for (; fallen < losses; fallen++)
            {
                var pick = -1;
                for (var t = 0; t < front.Length; t++) if (front[t] > 0 && (pick < 0 || front[t] > front[pick])) pick = t;
                if (pick < 0) break;
                front[pick]--;
                LoseMan(ship, FrontOrder[pick]);
            }
            ship.Morale = Math.Max(0, ship.Morale - fallen * Rules.DeckMoralePerCrew / ship.Definition.MaxCrew);
        }

        /// <summary>
        /// The prize is taken whole: hull, cargo, paid cost and fitted weapons (with their state) stay with it. Its
        /// sailors row on for the new owner, its wounded are lost, and a prize crew of the attacker's fighters
        /// (swordsmen first, then javelin men, then archers) goes across; if the prize has too few sailors to keep way,
        /// part of the prize crew takes the oars. Its port will train it to the class's usual crew.
        /// </summary>
        void Capture(Ship attacker, Ship target)
        {
            var definition = target.Definition;
            var prize = Math.Min(attacker.Soldiers, definition.BaseSailors);
            var swordsmen = Math.Min(attacker.Swordsmen, prize);
            var javelins = Math.Min(attacker.Javelins, prize - swordsmen);
            var archers = prize - swordsmen - javelins;
            attacker.Swordsmen -= swordsmen;
            attacker.Javelins -= javelins;
            attacker.Archers -= archers;

            // Rowers come from the prize crew, archers first, then javelin men, then swordsmen.
            var rowers = Math.Min(prize, Math.Max(0, definition.BaseSailors - target.Sailors));
            var take = Math.Min(archers, rowers); archers -= take; var left = rowers - take;
            take = Math.Min(javelins, left); javelins -= take; left -= take;
            swordsmen -= left;
            target.Sailors += rowers;
            target.Swordsmen = swordsmen;
            target.Javelins = javelins;
            target.Archers = archers;
            target.Wounded = 0;
            target.WoundRemainder = 0;
            target.CrewDamage = 0;
            target.Plan = definition.PlanFor(definition.DefaultPreset);
            // The prize keeps the weapons it carries; its new owner plans to keep them too.
            target.Loadout = (WeaponType[])target.Mounts.Clone();
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
                ReleaseTransport(ship);
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
            if (Ships.Any(ship => ship.Owner == faction)) return false;
            var cutter = ShipCatalog.Get(ShipClass.Cutter);
            return !PortsOf(faction).Any(port => port.WarQueue.Count > 0 || port.PortQueue.Count > 0
                || (port.Available(ResourceType.Wood) >= cutter.CostWood * Rules.Centi && port.Available(ResourceType.Silver) >= cutter.CostSilver * Rules.Centi));
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
