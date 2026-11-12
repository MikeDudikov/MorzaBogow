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

        /// <summary>
        /// Orders given since the last step, in the order given; the next step applies them first. They are saved with
        /// their numbers, so a save in a pause keeps them and the loaded game applies them at the same step as the
        /// continued one (docs/GRYWALNOSC_I_ODBIOR.md, J02/J10). Events are transient and not saved.
        /// </summary>
        public readonly List<Command> Pending = new List<Command>();
        public readonly List<SimEvent> Events = new List<SimEvent>();

        /// <summary>The number the next order gets (CommandId).</summary>
        public int NextCommandId = 1;

        /// <summary>
        /// What became of the orders the last step applied (J02): one result per ship, or one per port order, in the order
        /// applied. For the interface: cleared at the start of each step and not saved, since a save lies between steps
        /// after the interface has read them; the AI does not read them, so a loaded game decides as the continued one.
        /// </summary>
        public readonly List<CommandResult> CommandResults = new List<CommandResult>();

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
        public Settlement SettlementOf(int faction) => Settlements.FirstOrDefault(item => CanControl(faction, item.Owner) && !item.IsOutpost);
        /// <summary>
        /// Takes an order for the next step and numbers it. Taking it costs nothing: validation and payment happen when
        /// the step applies it (J02), so orders given in a pause wait without effect.
        /// </summary>
        public int Issue(Command command)
        {
            command.Id = NextCommandId++;
            Pending.Add(command);
            return command.Id;
        }

        public Ship SpawnShip(int owner, ShipClass shipClass, double x, double y, bool fullCrew = true)
        {
            var definition = ShipCatalog.Get(shipClass);
            var ship = new Ship
            {
                Id = NextId++, Owner = owner, Class = shipClass, X = x, Y = y, Hull = definition.Hull,
                Heading = owner == Owners.Human ? 0 : Math.PI,
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
            CommandResults.Clear();
            if (Outcome != OutcomeKind.InProgress) { Pending.Clear(); return; }
            Tick++;
            ApplyCommands();
            AssignBerths();
            StepProduction();
            StepRegrowth();
            StepMarine();
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
                for (var faction = 0; faction < Owners.SlotCount; faction++)
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
                case CommandKind.QueueShip: Record(command, -1, QueueShip(command.Faction, command.Class, command.TargetId, command.BlueprintId, command.Unarmed)); return;
                case CommandKind.BuildProject: Record(command, -1, BeginProject(command.Faction, command.TargetId, command.Project)); return;
                case CommandKind.CancelProject: Record(command, -1, CancelProject(command.Faction, command.TargetId)); return;
                case CommandKind.QueueWeapon: Record(command, -1, QueueWeapon(command.Faction, command.TargetId, command.Weapon)); return;
                case CommandKind.CancelWeapon: Record(command, -1, CancelWeapon(command.Faction, command.TargetId, command.QueueIndex)); return;
                case CommandKind.SaveBlueprint: Record(command, -1, SaveBlueprint(command)); return;
                case CommandKind.CancelQueue: Record(command, -1, CancelQueue(command.Faction, command.QueueIndex, command.PortQueue, command.TargetId)); return;
                case CommandKind.BuildShipyard: Record(command, -1, BeginShipyard(command.Faction, command.TargetId)); return;
                case CommandKind.SetPriorityPort: Record(command, -1, SetPriorityPort(command.Faction, command.TargetId)); return;
                case CommandKind.SetPortMinimum: Record(command, -1, SetPortMinimum(command.Faction, command.TargetId, command.Wood, command.Silver)); return;
            }

            foreach (var id in command.ShipIds.OrderBy(value => value))
            {
                var ship = FindShip(id);
                // A ship not the side's own, gone or never there, gets one answer: nothing about the enemy leaks (J08).
                Record(command, id, ship == null || !CanControl(command.Faction, ship.Owner) ? CommandReason.NoUnit : ApplyToShip(ship, command));
            }
        }

        void Record(Command command, int shipId, CommandReason reason) => CommandResults.Add(new CommandResult
        {
            CommandId = command.Id, Faction = command.Faction, ShipId = shipId, Kind = command.Kind,
            Outcome = reason == CommandReason.None ? CommandOutcome.Accepted : CommandOutcome.Rejected, Reason = reason,
        });

        /// <summary>
        /// Applies an order to one of the side's ships and says what became of it (J02): None when taken, otherwise the
        /// reason, and a refusal changes nothing. Capability is checked before the target, and a target the side does not
        /// see is refused as one that is gone, so a refusal never tells of a hidden enemy (J08).
        /// </summary>
        CommandReason ApplyToShip(Ship ship, Command command)
        {
            switch (command.Kind)
            {
                case CommandKind.Stop:
                    ClearOrder(ship);
                    return CommandReason.None;
                case CommandKind.Move:
                    ClearOrder(ship);
                    ship.Order = OrderKind.Move;
                    ship.TargetX = command.X;
                    ship.TargetY = command.Y;
                    return CommandReason.None;
                case CommandKind.Harvest:
                    if (!ship.Definition.CanHarvest) return CommandReason.NotCapable;
                    var node = FindNode(command.TargetId);
                    if (node == null) return CommandReason.NoObservedTarget;
                    ClearOrder(ship);
                    ship.Order = OrderKind.Harvest;
                    ship.NodeId = node.Id;
                    ship.Harvest = HarvestPhase.ToNode;
                    return CommandReason.None;
                case CommandKind.AttackShip:
                case CommandKind.Ram:
                case CommandKind.Board:
                    // An order to shoot is taken only by a ship with a post that can fire now (J02): accepted, it would
                    // lie still and do nothing.
                    if (command.Kind == CommandKind.AttackShip && ShootingProblem(ship) != CommandReason.None) return ShootingProblem(ship);
                    // Every warship carries a ram (docs/UZBROJENIE.md); working ships do not.
                    if (command.Kind == CommandKind.Ram && !ship.Definition.CanRam) return CommandReason.NotCapable;
                    // Only a main side takes prizes (CanCapture): the independent actors board nobody.
                    if (command.Kind == CommandKind.Board && (ship.Class == ShipClass.Cutter || !IsMainParticipant(ship.Owner))) return CommandReason.NotCapable;
                    if (command.Kind == CommandKind.Board && ship.Soldiers < Rules.BoardMinSoldiers) return CommandReason.NoCrew;
                    var target = FindShip(command.TargetId);
                    if (target == null || !CanSee(ship.Owner, target)) return CommandReason.NoObservedTarget;
                    // A ship is attacked only if its owner is an enemy: one's own, and one of a side at peace, is no target.
                    if (command.Kind == CommandKind.Board ? !CanCapture(ship.Owner, target) : !IsHostile(ship.Owner, target.Owner)) return CommandReason.InvalidTarget;
                    ClearOrder(ship);
                    ship.TargetId = target.Id;
                    ship.Order = command.Kind == CommandKind.AttackShip ? OrderKind.AttackShip
                        : command.Kind == CommandKind.Ram ? OrderKind.Ram : OrderKind.Board;
                    ship.RamState = RamPhase.Approach;
                    return CommandReason.None;
                case CommandKind.Salvage:
                    if (!ship.Definition.CanHarvest) return CommandReason.NotCapable;
                    if (Wrecks.All(wreck => wreck.Id != command.TargetId)) return CommandReason.NoObservedTarget;
                    ClearOrder(ship);
                    ship.Order = OrderKind.Salvage;
                    ship.WreckId = command.TargetId;
                    return CommandReason.None;
                case CommandKind.Repair:
                    // A sound hull still goes to port when its crew is short of the plan or has wounded, or its weapons
                    // differ from the plan; a hull or a module needs a dock on its side, the only place they are mended.
                    var mending = NeedsMending(ship);
                    if (!(mending && HasDock(ship.Owner)) && !NeedsMuster(ship) && !NeedsRefit(ship, ClosestYard(ship)))
                        return mending ? CommandReason.NoDock : CommandReason.NothingToDo;
                    ClearOrder(ship);
                    ship.Order = OrderKind.Repair;
                    return CommandReason.None;
                case CommandKind.SetLoadout:
                    // Only the plan: weapons change at a port (Refit); missing ones are ordered from its workshop.
                    return SetLoadout(ship, command.Loadout) ? CommandReason.None : CommandReason.InvalidOrder;
                case CommandKind.SetCrewPlan:
                    // Only a plan: the people change at a port (Muster), not at sea.
                    ship.Plan = command.Preset == CrewPreset.Custom ? ship.Definition.Clamp(command.Plan) : ship.Definition.PlanFor(command.Preset);
                    return CommandReason.None;
                case CommandKind.Scrap:
                    // Goods aboard would be lost with the hull (A01): it must unload first, and the refusal says so.
                    if (ship.Cargo > 0 || ship.HoldWood + ship.HoldSilver > 0) return CommandReason.CarriesCargo;
                    ClearOrder(ship);
                    ship.Order = OrderKind.Scrap;
                    ship.ScrapTicks = 0;
                    return CommandReason.None;
                case CommandKind.Transfer: return BeginTransfer(ship, command);
                case CommandKind.Convoy: return BeginConvoy(ship);
                case CommandKind.FoundOutpost: return BeginExpedition(ship, command);
                case CommandKind.Escort: return BeginEscort(ship, command);
                case CommandKind.AttackSettlement:
                    if (ShootingProblem(ship) != CommandReason.None) return ShootingProblem(ship);
                    var settlement = Settlements.FirstOrDefault(item => item.Id == command.TargetId);
                    // A port the side does not see is refused alike whether it stands, fell or never was.
                    if (settlement == null || !CanSee(ship.Owner, settlement.X, settlement.Y)) return CommandReason.NoObservedTarget;
                    if (!IsHostile(ship.Owner, settlement.Owner) || settlement.Destroyed) return CommandReason.InvalidTarget;
                    ClearOrder(ship);
                    ship.Order = OrderKind.AttackSettlement;
                    ship.TargetId = settlement.Id;
                    return CommandReason.None;
                default:
                    return CommandReason.InvalidOrder;
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
                // A hull lashed to a boarder rows nowhere while the decks fight, whichever ship comes first in the list.
                if (Boardings.Any(link => link.Hooked && link.TargetId == ship.Id))
                {
                    Integrate(ship);
                    continue;
                }
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
                    case OrderKind.Convoy: StepConvoy(ship); break;
                    case OrderKind.Escort: StepEscort(ship); break;
                    case OrderKind.AttackShip:
                        var target = FindShip(ship.TargetId);
                        // The order ends with the target, and with the last post that could fire at it (J02).
                        if (target == null || !CanSee(ship.Owner, target) || EngageRange(ship) == null) { ship.Order = OrderKind.Idle; StopShip(ship); break; }
                        HoldAtRange(ship, target.X, target.Y, true);
                        break;
                    case OrderKind.AttackSettlement:
                        var settlement = Settlements.FirstOrDefault(item => item.Id == ship.TargetId);
                        if (settlement == null || settlement.Destroyed || EngageRange(ship) == null) { ship.Order = OrderKind.Idle; StopShip(ship); break; }
                        HoldAtRange(ship, settlement.X, settlement.Y, false);
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

        /// <summary>
        /// Brings a ship ordered to shoot to where its working, manned posts can (A13, Q04). Farther than the distance it
        /// holds (HoldRange) it closes in. Within it, it lies still only while a post reaches the target; otherwise it does
        /// what the obstacle asks (FireBlockAt): turns its bow to a target outside its posts' arcs; closes in past land
        /// that hides the target from straight shots, no nearer than its posts' least range; and makes off from a target
        /// inside every post's least range until one reaches. A slower hull may not get away from a faster enemy: that
        /// is the counter to artillery. With no post that can fire it does not close in.
        /// </summary>
        void HoldAtRange(Ship ship, double x, double y, bool lineOfFire)
        {
            var hold = HoldRange(ship);
            if (hold == null) { StopShip(ship); return; }
            var distance = Distance(ship.X, ship.Y, x, y);
            if (distance > hold.Value) { Steer(ship, x, y, ship.Definition.Speed, hold.Value); return; }
            switch (FireBlockAt(ship, x, y, lineOfFire))
            {
                case FireBlock.Arc:
                    var difference = NormalizeAngle(Math.Atan2(y - ship.Y, x - ship.X) - ship.Heading);
                    var turn = ship.Definition.TurnRadiansPerSecond * Rules.Dt;
                    ship.Heading = NormalizeAngle(ship.Heading + Math.Max(-turn, Math.Min(turn, difference)));
                    StopShip(ship);
                    break;
                case FireBlock.Land:
                    var least = LeastRange(ship);
                    if (distance > least + 2) Steer(ship, x, y, ship.Definition.Speed, least);
                    else StopShip(ship);
                    break;
                case FireBlock.Range:
                    // Straight away from the target, to the distance it holds; dead ahead when it lies on top of it.
                    var awayX = distance > 1e-6 ? (ship.X - x) / distance : Math.Cos(ship.Heading);
                    var awayY = distance > 1e-6 ? (ship.Y - y) / distance : Math.Sin(ship.Heading);
                    Steer(ship, x + awayX * hold.Value, y + awayY * hold.Value, ship.Definition.Speed, 0);
                    break;
                default:
                    StopShip(ship);
                    break;
            }
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
            if (WindMoves) target *= ship.WindSpeedFactor;
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
                var otherAlong = other.Speed * Math.Cos(other.Heading - desired);
                if (WindMoves) { VelocityOf(other, out var ovx, out var ovy); otherAlong = ovx * dx + ovy * dy; }
                if (otherAlong >= Math.Max(0, ship.Speed) * 0.9 && other.Speed > 0.5) continue;

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
            Drift(ship);
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
            if (WindMoves)
            {
                VelocityOf(target, out var leadX, out var leadY);
                aimX = target.X + leadX * lead;
                aimY = target.Y + leadY * lead;
            }
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
                if (WindMoves)
                {
                    VelocityOf(ship, out var ownX, out var ownY);
                    VelocityOf(target, out var otherX, out var otherY);
                    closing = (ownX - otherX) * Math.Cos(toTarget) + (ownY - otherY) * Math.Sin(toTarget);
                }
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

        // ---------- Boarding (C05, C11): from the side, no instant ownership change ----------

        /// <summary>
        /// |v_a − v_b|: how fast two hulls move against each other, course included (Q04), with leeway and drift when the
        /// wind moves ships. Two hulls at 5.2 m/s on opposite courses pass at 10.4 m/s, not at 0.
        /// </summary>
        public double RelativeSpeed(Ship a, Ship b)
        {
            VelocityOf(a, out var ax, out var ay);
            VelocityOf(b, out var bx, out var by);
            return Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));
        }

        /// <summary>
        /// The hulls lie side by side, as a boarding needs (C11: from the side, never bow or stern on): their courses run
        /// parallel or opposed within Rules.BoardAlignDegrees, and the line between the nearest points of their keels stands
        /// across the target's keel within the same angle, so the attacker lies off the target's side, not off an end.
        /// </summary>
        public static bool BoardAlongside(Ship attacker, Ship target)
        {
            var limit = Math.Sin(Rules.BoardAlignDegrees * Math.PI / 180.0);
            if (Math.Abs(Math.Sin(attacker.Heading - target.Heading)) > limit) return false;
            HullSegment(attacker, out var a1x, out var a1y, out var a2x, out var a2y);
            HullSegment(target, out var b1x, out var b1y, out var b2x, out var b2y);
            var distance = LandShape.SegmentSegmentDistance(a1x, a1y, a2x, a2y, b1x, b1y, b2x, b2y, out var ax, out var ay, out var bx, out var by);
            if (distance < 1e-6) return false;
            var along = ((ax - bx) * Math.Cos(target.Heading) + (ay - by) * Math.Sin(target.Heading)) / distance;
            return Math.Abs(along) <= limit;
        }

        /// <summary>
        /// The course a boarder lies on beside its target: the target's own while it is under way; beside a hull lying
        /// still, the parallel or the opposed course, whichever is nearer the boarder's heading.
        /// </summary>
        static double BoardingCourse(Ship ship, Ship target)
        {
            if (target.Speed >= BoardUnderWay) return target.Heading;
            return Math.Cos(NormalizeAngle(ship.Heading - target.Heading)) >= 0 ? target.Heading : NormalizeAngle(target.Heading + Math.PI);
        }

        /// <summary>A target slower than this lies still for a boarder, which may then come up on either course.</summary>
        const double BoardUnderWay = 0.5;

        /// <summary>
        /// Beside the target the boarder turns to the boarding course, as fast as its hull turns, and takes the target's
        /// pace along it; it does not row astern to keep up with a target sailing the other way.
        /// </summary>
        void MatchBoardingCourse(Ship ship, Ship target)
        {
            var course = BoardingCourse(ship, target);
            var difference = NormalizeAngle(course - ship.Heading);
            var turn = ship.Definition.TurnRadiansPerSecond * Rules.Dt;
            ship.Heading = NormalizeAngle(ship.Heading + Math.Max(-turn, Math.Min(turn, difference)));
            var pace = Math.Max(0, target.Speed * Math.Cos(NormalizeAngle(course - target.Heading)));
            var acceleration = ship.Definition.Speed / 4 * Rules.Dt;
            ship.Speed = ship.Speed < pace ? Math.Min(pace, ship.Speed + acceleration) : Math.Max(pace, ship.Speed - acceleration);
        }

        /// <summary>How far off the lane alongside a boarder may lie and still run in along it, in metres.</summary>
        const double BoardLaneWidth = 2.5;

        /// <summary>
        /// Brings a boarder alongside (C11). The station lies off the target's midships, on the side the boarder is on, at
        /// the spacing passing hulls keep; the lane leads to it along the boarding course from a hull's length or so behind.
        /// Off the lane the boarder makes for the lane's start, led by the target's motion as a ram leads its charge; on the
        /// lane it first lines up with the course (turning where it lies) and then runs in, easing to the target's pace as
        /// it closes, so it comes up parallel and never bow first. A hull cannot slip sideways, hence the lane.
        /// Where another hull lies in the station on its side, the boarder goes round to the target's other side instead
        /// of shoving that hull away; with both sides taken it waits close by until one is free (J01). The side is read
        /// from where the hulls lie each tick, so nothing of the approach is kept or saved.
        /// </summary>
        void SteerAlongside(Ship ship, Ship target)
        {
            var tx = Math.Cos(target.Heading);
            var ty = Math.Sin(target.Heading);
            var side = -(ship.X - target.X) * ty + (ship.Y - target.Y) * tx >= 0 ? 1 : -1;
            var offset = (ship.Definition.Beam + target.Definition.Beam) / 2 + HullSpacing;
            var run = ship.Definition.Radius + target.Definition.Radius;
            if (StationTaken(ship, target, target.X - ty * side * offset, target.Y + tx * side * offset))
            {
                if (StationTaken(ship, target, target.X + ty * side * offset, target.Y - tx * side * offset))
                {
                    var wait = run + 2 * offset;
                    if (Distance(ship.X, ship.Y, target.X, target.Y) > wait) Steer(ship, target.X, target.Y, ship.Definition.Speed, wait);
                    else StopShip(ship);
                    return;
                }
                side = -side;
            }
            var stationX = target.X - ty * side * offset;
            var stationY = target.Y + tx * side * offset;
            // Under way, the lane runs with the target. Beside a hull lying still, the lane whose start is nearer, so the
            // choice holds as the boarder makes for it (a choice by heading would flip from tick to tick when it comes
            // abeam bow on); on a tie, the course nearer its heading.
            var course = BoardingCourse(ship, target);
            if (target.Speed < BoardUnderWay)
            {
                var forward = Distance2(ship.X, ship.Y, stationX - tx * run, stationY - ty * run);
                var backward = Distance2(ship.X, ship.Y, stationX + tx * run, stationY + ty * run);
                if (forward < backward - 1e-6) course = target.Heading;
                else if (backward < forward - 1e-6) course = NormalizeAngle(target.Heading + Math.PI);
            }
            var ux = Math.Cos(course);
            var uy = Math.Sin(course);
            var along = (ship.X - stationX) * ux + (ship.Y - stationY) * uy;
            var across = -(ship.X - stationX) * uy + (ship.Y - stationY) * ux;

            if (Math.Abs(across) <= BoardLaneWidth && along >= -run - BoardLaneWidth && along <= BoardLaneWidth)
            {
                var desired = NormalizeAngle(course - Math.Atan2(across, 10));
                var difference = NormalizeAngle(desired - ship.Heading);
                var turn = ship.Definition.TurnRadiansPerSecond * Rules.Dt;
                ship.Heading = NormalizeAngle(ship.Heading + Math.Max(-turn, Math.Min(turn, difference)));
                var pace = Math.Max(0, target.Speed * Math.Cos(NormalizeAngle(course - target.Heading)));
                var lined = Math.Abs(NormalizeAngle(course - ship.Heading)) <= 0.35;
                var wanted = lined ? Math.Min(ship.Definition.Speed * CrewSpeedFactor(ship), pace + Math.Max(0, -along) * 0.3) : 0;
                var acceleration = ship.Definition.Speed / 4 * Rules.Dt;
                ship.Speed = ship.Speed < wanted ? Math.Min(wanted, ship.Speed + acceleration) : Math.Max(wanted, ship.Speed - acceleration);
                return;
            }

            var startX = stationX - ux * run;
            var startY = stationY - uy * run;
            var lead = Math.Min(Distance(ship.X, ship.Y, startX, startY) / Math.Max(1, ship.Definition.Speed), 8);
            VelocityOf(target, out var vx, out var vy);
            // The lane's start moves with a target under way: easing off on arrival would only keep pace behind it, so the
            // boarder makes full speed until it is on the lane (as a ram charges); for a hull lying still it eases off and
            // can turn onto the lane where it stops.
            Steer(ship, startX + vx * lead, startY + vy * lead, ship.Definition.Speed, target.Speed >= BoardUnderWay ? -100 : 0);
        }

        /// <summary>
        /// Another hull, neither the boarder nor its target, lies where the boarder would lie at a station beside the
        /// target: nearer its place there than hulls keep from one another. Only hulls the boarder's side sees count (Q06,
        /// J08): its course must not tell of a ship it does not see. Once seen, the hull counts from that tick on.
        /// </summary>
        bool StationTaken(Ship ship, Ship target, double x, double y)
        {
            var half = Math.Max(0, ship.Definition.Radius - ship.Definition.Beam / 2);
            var cos = Math.Cos(target.Heading) * half;
            var sin = Math.Sin(target.Heading) * half;
            foreach (var other in Ships)
            {
                if (other == ship || other == target) continue;
                if (Distance(x, y, other.X, other.Y) > ship.Definition.Radius + other.Definition.Radius + HullSpacing) continue;
                if (!CanSee(ship.Owner, other)) continue;
                HullSegment(other, out var b1x, out var b1y, out var b2x, out var b2y);
                var gap = LandShape.SegmentSegmentDistance(x + cos, y + sin, x - cos, y - sin, b1x, b1y, b2x, b2y, out _, out _, out _, out _)
                          - (ship.Definition.Beam + other.Definition.Beam) / 2;
                if (gap < HullSpacing) return true;
            }
            return false;
        }

        void StepBoardMovement(Ship ship)
        {
            var target = FindShip(ship.TargetId);
            if (target == null) { ship.Order = OrderKind.Idle; return; }
            var link = Boardings.FirstOrDefault(item => item.AttackerId == ship.Id);
            if (link != null)
            {
                if (link.Hooked)
                {
                    // Lashed, the two hulls move as one.
                    ship.Heading = BoardingCourse(ship, target);
                    ship.Speed = Math.Max(0, target.Speed * Math.Cos(NormalizeAngle(ship.Heading - target.Heading)));
                }
                // While the hooks bite the boarder keeps beside the target as a ship can; one pulling away parts the lines.
                else MatchBoardingCourse(ship, target);
                return;
            }

            if (!CanSee(ship.Owner, target)) { ship.Order = OrderKind.Idle; return; }
            // The hooks reach across the water between the hulls as they are (the capsule outline contacts use), from the
            // side only, and hold only between hulls keeping pace with each other, course included (Q04).
            if (HullGap(ship, target) > Rules.BoardGap || !BoardAlongside(ship, target))
            {
                SteerAlongside(ship, target);
                return;
            }
            MatchBoardingCourse(ship, target);
            if (RelativeSpeed(ship, target) > Rules.BoardRelativeSpeed) return;
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
                // The hooks hold only on a ship the attacker may take: not one that became its own side's or a friend's.
                if (attacker == null || target == null || attacker.Hull <= 0 || target.Hull <= 0 || !CanCapture(attacker.Owner, target))
                {
                    Boardings.Remove(link);
                    continue;
                }

                if (!link.Hooked)
                {
                    // The lines part when the hulls drift apart or one pulls away, course included (Q04).
                    var gap = HullGap(attacker, target);
                    // The defender's swordsmen hack at the lines; the attacker's own swordsmen keep half as many off them.
                    var cutters = target.Swordsmen - attacker.Swordsmen * 0.5;
                    if (cutters > 0) link.CutProgress += cutters * Rules.HookCutPerSwordsman * Rules.Dt;
                    if (gap > Rules.BoardGap * 2 || RelativeSpeed(attacker, target) > Rules.BoardRelativeSpeed || link.CutProgress >= 1)
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
            // The one place a ship changes hands: only to a side that may take it (CanCapture).
            if (!CanCapture(attacker.Owner, target)) return;
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
            // Whoever was after the prize and is no enemy of its new owner lets it be.
            foreach (var other in Ships.Where(item => item.TargetId == target.Id && !IsHostile(item.Owner, target.Owner))) other.Order = OrderKind.Idle;
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

        /// <summary>
        /// The match is decided between the two main sides only (Owners.IsMainParticipant): what the independent actors
        /// hold or lose neither ends it nor wins it. Winner stays -1 while nobody has won, and in a draw.
        /// </summary>
        void CheckVictory()
        {
            var first = FactionDefeated(Owners.Human);
            var second = FactionDefeated(Owners.MainAi);
            if (!first && !second) return;
            Outcome = first && second ? OutcomeKind.Draw : OutcomeKind.Won;
            Winner = first && second ? -1 : Owners.MainOpponentOf(first ? Owners.Human : Owners.MainAi);
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
