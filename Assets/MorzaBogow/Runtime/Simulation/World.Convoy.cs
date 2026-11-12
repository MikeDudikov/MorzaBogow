using System;
using System.Linq;

namespace MorzaBogow.Simulation
{
    /// <summary>
    /// E03 automatic convoys. A merchant on convoy duty takes the surplus above a port's minimum (Settlement.KeepWood,
    /// KeepSilver) from the port with the most of it toward what its side's priority port lacks, and brings it there.
    /// The priority port is stocked up to the price of the costliest ship it may build now: the shipyard's default
    /// stock is the price of one template, not endless hoarding. Once it holds that, counting what is already on its
    /// way, the merchant waits. Up to three merchants load at one source together and sail together. The automation
    /// reads only its own side's stores; it knows nothing of the enemy.
    /// </summary>
    public sealed partial class World
    {
        /// <summary>Each side's priority port by id; -1, or a port lost, means its home port.</summary>
        public readonly int[] PriorityPorts = { -1, -1 };

        /// <summary>A merchant carrying goods between ports: a transfer, an expedition or convoy duty.</summary>
        public static bool Carries(Ship ship) => ship.Order == OrderKind.Transfer || ship.Order == OrderKind.Expedition || ship.Order == OrderKind.Convoy;

        /// <summary>The port a side's convoys supply: the one it named, else its home port; null when neither stands.</summary>
        public Settlement PriorityPort(int faction)
        {
            var named = FindSettlement(PriorityPorts[faction]);
            if (named != null && named.Owner == faction && !named.Destroyed) return named;
            var home = SettlementOf(faction);
            return home != null && !home.Destroyed ? home : null;
        }

        /// <summary>
        /// What a convoy stocks a port up to: the price of the costliest ship it may build now (wood and silver
        /// together decide which), in 0.01 units.
        /// </summary>
        public (long wood, long silver) ConvoyGoal(Settlement port)
        {
            long wood = 0, silver = 0;
            var best = -1;
            foreach (ShipClass shipClass in Enum.GetValues(typeof(ShipClass)))
            {
                if (ShipProblem(port, shipClass) != null) continue;
                var definition = ShipCatalog.Get(shipClass);
                if (definition.CostWood + definition.CostSilver <= best) continue;
                best = definition.CostWood + definition.CostSilver;
                wood = definition.CostWood * Rules.Centi;
                silver = definition.CostSilver * Rules.Centi;
            }
            return (wood, silver);
        }

        /// <summary>What a port still lacks of its convoy goal: free stock and goods already on their way to it count.</summary>
        public (long wood, long silver) ConvoyNeed(Settlement port)
        {
            var (wood, silver) = ConvoyGoal(port);
            wood -= port.Available(ResourceType.Wood);
            silver -= port.Available(ResourceType.Silver);
            foreach (var ship in Ships)
            {
                if (ship.Owner != port.Owner || ship.DestinationId != port.Id || (ship.Order != OrderKind.Convoy && ship.Order != OrderKind.Transfer)) continue;
                wood -= ship.HoldWood + ship.WantWood;
                silver -= ship.HoldSilver + ship.WantSilver;
            }
            return (Math.Max(0, wood), Math.Max(0, silver));
        }

        /// <summary>What a port can give toward a need without going below its minimum.</summary>
        public static long ConvoySurplus(Settlement port, long needWood, long needSilver) =>
            Math.Min(needWood, Math.Max(0, port.Available(ResourceType.Wood) - port.KeepWood))
            + Math.Min(needSilver, Math.Max(0, port.Available(ResourceType.Silver) - port.KeepSilver));

        CommandReason BeginConvoy(Ship ship)
        {
            if (ship.Class != ShipClass.Merchant) return CommandReason.NotCapable;
            ClearOrder(ship);
            ship.Order = OrderKind.Convoy;
            ship.Transport = TransportPhase.Standby;
            return CommandReason.None;
        }

        CommandReason SetPriorityPort(int faction, int portId)
        {
            if (faction < 0 || faction >= PriorityPorts.Length) return CommandReason.InvalidOrder;
            var port = FindSettlement(portId);
            if (portId >= 0 && (port == null || port.Owner != faction || port.Destroyed)) return CommandReason.InvalidTarget;
            PriorityPorts[faction] = portId;
            return CommandReason.None;
        }

        CommandReason SetPortMinimum(int faction, int portId, long wood, long silver)
        {
            var port = FindSettlement(portId);
            if (port == null || port.Owner != faction || port.Destroyed) return CommandReason.InvalidTarget;
            port.KeepWood = Math.Max(0, Math.Min(wood, port.Capacity(ResourceType.Wood)));
            port.KeepSilver = Math.Max(0, Math.Min(silver, port.Capacity(ResourceType.Silver)));
            return CommandReason.None;
        }

        void StepConvoy(Ship ship)
        {
            if (ship.Transport != TransportPhase.Standby)
            {
                StepTransport(ship);
                // A delivery that ended (unloaded, a store full, a port lost) leaves the merchant on convoy duty.
                if (ship.Order == OrderKind.Idle)
                {
                    ship.Order = OrderKind.Convoy;
                    ship.Transport = TransportPhase.Standby;
                    ship.ConvoyGroup = -1;
                }
                return;
            }

            StopShip(ship);
            var target = PriorityPort(ship.Owner);
            if (target == null) return;
            if (ship.HoldWood + ship.HoldSilver > 0)
            {
                // Goods already aboard go to the priority port first, once its store has room for them.
                var room = (ship.HoldWood > 0 && target.Wood < target.Capacity(ResourceType.Wood))
                           || (ship.HoldSilver > 0 && target.Silver < target.Capacity(ResourceType.Silver));
                if (!room) return;
                ship.DestinationId = target.Id;
                ship.Transport = TransportPhase.ToDestination;
                return;
            }
            // A merchant at rest looks for a load once a second.
            if ((Tick + ship.Id) % Rules.TickRate != 0) return;
            ChooseConvoyLoad(ship, target);
        }

        /// <summary>
        /// The next load for a merchant on convoy duty: it joins a convoy still loading for the same port while that one
        /// has room and its source still has surplus; otherwise it starts one at the port whose surplus covers most of
        /// the need, nearer first on a tie. Silver, the scarcer store, goes aboard first.
        /// </summary>
        void ChooseConvoyLoad(Ship ship, Settlement target)
        {
            var (needWood, needSilver) = ConvoyNeed(target);
            if (needWood + needSilver <= 0) return;

            Settlement source = null;
            var group = -1;
            var forming = Ships
                .Where(other => other != ship && other.Owner == ship.Owner && other.Order == OrderKind.Convoy && other.ConvoyGroup >= 0 && other.DestinationId == target.Id)
                .GroupBy(other => other.ConvoyGroup)
                .Where(members => members.Count() < Rules.ConvoySize && members.Any(member => member.Transport <= TransportPhase.Loading))
                .OrderBy(members => members.Key);
            foreach (var members in forming)
            {
                var candidate = FindSettlement(members.First(member => member.Transport <= TransportPhase.Loading).SourceId);
                if (candidate == null || candidate.Destroyed || candidate.Owner != ship.Owner || ConvoySurplus(candidate, needWood, needSilver) <= 0) continue;
                source = candidate;
                group = members.Key;
                break;
            }
            if (source == null)
            {
                source = PortsOf(ship.Owner)
                    .Where(port => port.Id != target.Id && ConvoySurplus(port, needWood, needSilver) > 0)
                    .OrderByDescending(port => ConvoySurplus(port, needWood, needSilver))
                    .ThenBy(port => Distance2(ship.X, ship.Y, port.X, port.Y)).ThenBy(port => port.Id)
                    .FirstOrDefault();
                if (source == null) return;
                group = ship.Id;
            }

            var capacity = ship.Definition.Capacity * Rules.Centi;
            var spareWood = Math.Max(0, source.Available(ResourceType.Wood) - source.KeepWood);
            var spareSilver = Math.Max(0, source.Available(ResourceType.Silver) - source.KeepSilver);
            var silver = Math.Min(Math.Min(needSilver, spareSilver), capacity);
            var wood = Math.Min(Math.Min(needWood, spareWood), capacity - silver);
            if (wood + silver <= 0) return;
            ship.ConvoyGroup = group;
            ship.DestinationId = target.Id;
            Reserve(ship, source, wood, silver);
        }

        /// <summary>
        /// A loaded member stays by its source, but clears the approaches of the members still loading. Stopping in
        /// their way can otherwise deadlock an empty berth. The small muster ring is derived from existing state;
        /// it adds no saved field and does not start the voyage before the group finishes loading.
        /// </summary>
        void MakeRoomForConvoy(Ship ship)
        {
            var source = FindSettlement(ship.SourceId);
            if (source == null || source.Destroyed) { StopShip(ship); return; }
            if (ConvoyApproachClearance(ship, source, ship.X, ship.Y) >= HullSpacing) { StopShip(ship); return; }

            var radius = PortReach(ship) + ship.Definition.Radius;
            var best = double.NegativeInfinity;
            var nearest = double.MaxValue;
            var targetX = ship.X;
            var targetY = ship.Y;
            for (var point = 0; point < 8; point++)
            {
                var angle = point * Math.PI / 4;
                var x = source.X + radius * Math.Cos(angle);
                var y = source.Y + radius * Math.Sin(angle);
                if (ClearanceAt(x, y) < LandClearance(ship) + 1) continue;
                var clearance = ConvoyApproachClearance(ship, source, x, y);
                var distance = Distance2(ship.X, ship.Y, x, y);
                if (clearance < best - 1e-9 || (Math.Abs(clearance - best) <= 1e-9 && distance >= nearest)) continue;
                best = clearance;
                nearest = distance;
                targetX = x;
                targetY = y;
            }
            if (double.IsNegativeInfinity(best)) { StopShip(ship); return; }
            if (Distance(ship.X, ship.Y, targetX, targetY) <= ship.Definition.Beam / 2) StopShip(ship);
            else Steer(ship, targetX, targetY, ship.Definition.Speed, ship.Definition.Beam / 2);
        }

        double ConvoyApproachClearance(Ship ship, Settlement source, double x, double y)
        {
            var clearance = double.MaxValue;
            foreach (var other in Ships)
            {
                if (other == ship || other.Owner != ship.Owner || other.Order != OrderKind.Convoy
                    || other.ConvoyGroup != ship.ConvoyGroup || other.SourceId != source.Id
                    || other.Transport > TransportPhase.Loading) continue;
                clearance = Math.Min(clearance, LandShape.PointSegmentDistance(x, y, other.X, other.Y, source.X, source.Y)
                    - ship.Definition.Radius - other.Definition.Radius);
            }
            return clearance;
        }

        /// <summary>A convoy sails together: a loaded member waits by the source while another of its group still loads.</summary>
        public bool ConvoyStillLoading(Ship ship) =>
            ship.ConvoyGroup >= 0 && Ships.Any(other => other != ship && other.Owner == ship.Owner && other.Order == OrderKind.Convoy
                && other.ConvoyGroup == ship.ConvoyGroup && other.Transport <= TransportPhase.Loading);
    }
}
