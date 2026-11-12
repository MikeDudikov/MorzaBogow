using System;
using System.Linq;

namespace MorzaBogow.Simulation
{
    /// <summary>
    /// Second-island play: E03 real transport between local stores, E05 bridgehead expeditions and local
    /// shipyards, E03 escorts. Goods exist in one place at a time: store, hold, construction or wreck.
    /// </summary>
    public sealed partial class World
    {
        public readonly System.Collections.Generic.List<BuildSite> Sites = new System.Collections.Generic.List<BuildSite>();

        public Settlement FindSettlement(int id) => Settlements.FirstOrDefault(item => item.Id == id);
        public BuildSite FindSite(int id) => Sites.FirstOrDefault(item => item.Id == id);

        public System.Collections.Generic.IEnumerable<Settlement> PortsOf(int faction) =>
            Settlements.Where(item => item.Owner == faction && !item.Destroyed);

        public Settlement ClosestPort(Ship ship) => ClosestOf(ship, PortsOf(ship.Owner));

        public Settlement ClosestYard(Ship ship) => ClosestOf(ship, PortsOf(ship.Owner).Where(item => item.HasShipyard));

        static Settlement ClosestOf(Ship ship, System.Collections.Generic.IEnumerable<Settlement> ports) =>
            ports.OrderBy(item => Distance2(ship.X, ship.Y, item.X, item.Y)).ThenBy(item => item.Id).FirstOrDefault();

        /// <summary>A destroyed bridgehead leaves its shore free again.</summary>
        public bool IsSiteFree(BuildSite site)
        {
            var occupant = FindSettlement(site.SettlementId);
            return occupant == null || occupant.Destroyed;
        }

        public bool IsExplored(int faction, double x, double y)
        {
            var i = CellOf(x);
            var j = CellOf(y);
            if (i < 0 || j < 0 || i >= FogCells || j >= FogCells) return false;
            return (Explored[j * FogCells + i] & (1 << faction)) != 0;
        }

        /// <summary>
        /// How close to a port point a ship must come. A bigger hull cannot reach the point itself past the land
        /// beside it, so its reach grows with the hull. A cutter keeps the plain range the income model assumes.
        /// </summary>
        static double PortReach(Ship ship) =>
            Rules.PortRange + Math.Max(0, LandClearance(ship) - EmptyCutterClearance) + ship.Definition.Beam * BerthSpread;

        /// <summary>Hulls cannot share one point any more: berths and the queue spread out by about this many beams.</summary>
        const double BerthSpread = 1.5;

        static readonly double EmptyCutterClearance = ShipCatalog.Get(ShipClass.Cutter).Radius + Seabed.ShoalWidth(ShipCatalog.Get(ShipClass.Cutter).Draft);

        /// <summary>A work point is reached within this distance; the second berth and the queue lie beside the first.</summary>
        static double NodeReach(Ship ship) => Rules.NodeWorkRange + ship.Definition.Beam * BerthSpread;

        // ---------- Orders ----------

        void Reject(Ship ship) => Emit("order_rejected", ship.Id, -1, ship.X, ship.Y, 0);

        CommandReason BeginTransfer(Ship ship, Command command)
        {
            if (ship.Class != ShipClass.Merchant) return CommandReason.NotCapable;
            var destination = FindSettlement(command.TargetId);
            if (destination == null || destination.Owner != ship.Owner || destination.Destroyed) { Reject(ship); return CommandReason.InvalidTarget; }

            // Goods already on board go straight to the destination.
            if (ship.HoldWood + ship.HoldSilver > 0)
            {
                ClearOrder(ship);
                ship.Order = OrderKind.Transfer;
                ship.DestinationId = destination.Id;
                ship.Transport = TransportPhase.ToDestination;
                return CommandReason.None;
            }

            var source = FindSettlement(command.SourceId) ?? PortsOf(ship.Owner).Where(item => item.Id != destination.Id)
                .OrderBy(item => Distance2(ship.X, ship.Y, item.X, item.Y)).ThenBy(item => item.Id).FirstOrDefault();
            if (source == null || source.Owner != ship.Owner || source.Destroyed || source.Id == destination.Id) { Reject(ship); return CommandReason.InvalidTarget; }

            var capacity = ship.Definition.Capacity * Rules.Centi;
            long wood, silver;
            if (command.Wood > 0 || command.Silver > 0)
            {
                wood = Math.Min(command.Wood, source.Available(ResourceType.Wood));
                silver = Math.Min(Math.Min(command.Silver, source.Available(ResourceType.Silver)), capacity - Math.Min(wood, capacity));
                wood = Math.Min(wood, capacity - silver);
            }
            else
            {
                // E03: an automatic load keeps the source's minimum, by default the reserve that protects rebuilding a cutter.
                var spareWood = Math.Max(0, source.Available(ResourceType.Wood) - source.KeepWood);
                var spareSilver = Math.Max(0, source.Available(ResourceType.Silver) - source.KeepSilver);
                wood = Math.Min(capacity * 2 / 3, spareWood);
                silver = Math.Min(capacity - wood, spareSilver);
                wood = Math.Min(capacity - silver, spareWood);
            }
            if (wood + silver <= 0) { Reject(ship); return CommandReason.NoFunds; }

            ClearOrder(ship);
            ship.Order = OrderKind.Transfer;
            ship.DestinationId = destination.Id;
            Reserve(ship, source, wood, silver);
            return CommandReason.None;
        }

        CommandReason BeginExpedition(Ship ship, Command command)
        {
            if (ship.Class != ShipClass.Merchant) return CommandReason.NotCapable;
            var site = FindSite(command.TargetId);
            if (site == null || !IsExplored(ship.Owner, site.X, site.Y)) { Reject(ship); return CommandReason.NoObservedTarget; }
            // Known limit (J08, P03/E05): a site taken or claimed by an unseen enemy is refused too, as before.
            if (!IsSiteFree(site) || site.BuilderShipId >= 0) { Reject(ship); return CommandReason.NoSpace; }

            var wood = Math.Max(0, Rules.OutpostWood - ship.HoldWood);
            var silver = Math.Max(0, Rules.OutpostSilver - ship.HoldSilver);
            var needsLoad = wood + silver > 0;
            var source = FindSettlement(command.SourceId) ?? PortsOf(ship.Owner)
                .Where(item => item.Available(ResourceType.Wood) >= wood && item.Available(ResourceType.Silver) >= silver)
                .OrderBy(item => Distance2(ship.X, ship.Y, item.X, item.Y)).ThenBy(item => item.Id).FirstOrDefault();
            if (needsLoad)
            {
                if (source == null || source.Owner != ship.Owner || source.Destroyed) { Reject(ship); return source == null ? CommandReason.NoFunds : CommandReason.InvalidTarget; }
                if (source.Available(ResourceType.Wood) < wood || source.Available(ResourceType.Silver) < silver) { Reject(ship); return CommandReason.NoFunds; }
                if (ship.HoldWood + ship.HoldSilver + wood + silver > ship.Definition.Capacity * Rules.Centi) { Reject(ship); return CommandReason.NoSpace; }
            }

            ClearOrder(ship);
            ship.Order = OrderKind.Expedition;
            ship.SiteId = site.Id;
            site.BuilderShipId = ship.Id;
            if (needsLoad) Reserve(ship, source, wood, silver);
            else ship.Transport = TransportPhase.ToDestination;
            return CommandReason.None;
        }

        void Reserve(Ship ship, Settlement source, long wood, long silver)
        {
            source.ReservedWood += wood;
            source.ReservedSilver += silver;
            ship.WantWood = wood;
            ship.WantSilver = silver;
            ship.SourceId = source.Id;
            ship.Transport = TransportPhase.ToSource;
        }

        CommandReason BeginEscort(Ship ship, Command command)
        {
            if (ship.Definition.IsEconomic) return CommandReason.NotCapable;
            var escorted = FindShip(command.TargetId);
            if (escorted == null || escorted == ship || escorted.Owner != ship.Owner) return CommandReason.InvalidTarget;
            ClearOrder(ship);
            ship.Order = OrderKind.Escort;
            ship.EscortId = escorted.Id;
            return CommandReason.None;
        }

        CommandReason BeginShipyard(int faction, int settlementId)
        {
            var settlement = FindSettlement(settlementId);
            if (settlement == null || settlement.Owner != faction || settlement.Destroyed) return CommandReason.InvalidTarget;
            if (settlement.HasShipyard || settlement.ShipyardBuild != null) return CommandReason.NothingToDo;
            if (settlement.Available(ResourceType.Wood) < Rules.ShipyardWood || settlement.Available(ResourceType.Silver) < Rules.ShipyardSilver) return CommandReason.NoFunds;
            settlement.ReservedWood += Rules.ShipyardWood;
            settlement.ReservedSilver += Rules.ShipyardSilver;
            settlement.ShipyardBuild = new BuildOrder { PaidWood = Rules.ShipyardWood, PaidSilver = Rules.ShipyardSilver, TotalTicks = (int)(Rules.ShipyardBuildSeconds * Rules.TickRate) };
            return CommandReason.None;
        }

        /// <summary>Gives back what was reserved and not yet loaded. Goods already on board stay there.</summary>
        void ReleaseTransport(Ship ship)
        {
            var source = FindSettlement(ship.SourceId);
            if (source != null)
            {
                source.ReservedWood -= ship.WantWood;
                source.ReservedSilver -= ship.WantSilver;
            }
            foreach (var site in Sites) if (site.BuilderShipId == ship.Id) site.BuilderShipId = -1;
            if (Carries(ship)) ship.UnloadRemainder = 0;
            ship.WantWood = ship.WantSilver = 0;
            ship.SourceId = ship.DestinationId = ship.SiteId = ship.EscortId = ship.ConvoyGroup = -1;
            ship.BuildTicks = 0;
            ship.BuildUsedWood = ship.BuildUsedSilver = 0;
        }

        // ---------- Transport ----------

        void StepTransport(Ship ship)
        {
            var definition = ship.Definition;
            switch (ship.Transport)
            {
                case TransportPhase.ToSource:
                case TransportPhase.WaitSourceBerth:
                case TransportPhase.Loading:
                    var source = FindSettlement(ship.SourceId);
                    if (source == null || source.Destroyed) { ClearOrder(ship); return; }
                    StepAtSource(ship, source);
                    return;

                case TransportPhase.ToDestination:
                    if (ship.Order == OrderKind.Expedition) { StepToSite(ship); return; }
                    if (ship.Order == OrderKind.Convoy && ConvoyStillLoading(ship)) { StopShip(ship); return; }
                    var target = FindSettlement(ship.DestinationId);
                    if (target == null || target.Destroyed) { ClearOrder(ship); return; }
                    if (Distance(ship.X, ship.Y, target.X, target.Y) <= PortReach(ship))
                    {
                        StopShip(ship);
                        target.PortWaiting.Add(ship.Id);
                        ship.Transport = TransportPhase.WaitDestinationBerth;
                    }
                    else Steer(ship, target.X, target.Y, definition.Speed, PortReach(ship) * 0.8);
                    return;

                case TransportPhase.WaitDestinationBerth:
                case TransportPhase.Unloading:
                    var port = FindSettlement(ship.DestinationId);
                    if (port == null || port.Destroyed) { ClearOrder(ship); return; }
                    StopShip(ship);
                    if (ship.Transport == TransportPhase.WaitDestinationBerth)
                    {
                        if (port.DockedShips.Contains(ship.Id)) ship.Transport = TransportPhase.Unloading;
                        return;
                    }
                    StepUnloadHold(ship, port);
                    return;

                case TransportPhase.Building:
                    StepBuilding(ship);
                    return;
            }
        }

        void StepAtSource(Ship ship, Settlement source)
        {
            if (ship.Transport == TransportPhase.ToSource)
            {
                if (Distance(ship.X, ship.Y, source.X, source.Y) > PortReach(ship))
                {
                    Steer(ship, source.X, source.Y, ship.Definition.Speed, PortReach(ship) * 0.8);
                    return;
                }
                StopShip(ship);
                source.PortWaiting.Add(ship.Id);
                ship.Transport = TransportPhase.WaitSourceBerth;
                return;
            }

            StopShip(ship);
            if (ship.Transport == TransportPhase.WaitSourceBerth)
            {
                if (source.DockedShips.Contains(ship.Id)) ship.Transport = TransportPhase.Loading;
                return;
            }

            ship.UnloadRemainder += Rules.HandlingPerSecond * Rules.Dt * Rules.Centi;
            var budget = (long)ship.UnloadRemainder;
            ship.UnloadRemainder -= budget;
            var wood = Math.Min(budget, ship.WantWood);
            var silver = Math.Min(budget - wood, ship.WantSilver);
            source.Wood -= wood;
            source.ReservedWood -= wood;
            ship.WantWood -= wood;
            ship.HoldWood += wood;
            source.Silver -= silver;
            source.ReservedSilver -= silver;
            ship.WantSilver -= silver;
            ship.HoldSilver += silver;
            if (ship.WantWood + ship.WantSilver > 0) return;

            ReleaseDock(ship);
            ship.UnloadRemainder = 0;
            ship.Transport = TransportPhase.ToDestination;
        }

        void StepUnloadHold(Ship ship, Settlement port)
        {
            var freeWood = port.Capacity(ResourceType.Wood) - port.Wood;
            var freeSilver = port.Capacity(ResourceType.Silver) - port.Silver;
            if ((ship.HoldWood == 0 || freeWood <= 0) && (ship.HoldSilver == 0 || freeSilver <= 0))
            {
                // E03: a full store keeps the rest on the ship and frees the berth, with one alarm.
                if (ship.HoldWood + ship.HoldSilver > 0) Emit("store_full", ship.Id, port.Id, ship.X, ship.Y, ship.HoldWood + ship.HoldSilver);
                ClearOrder(ship);
                return;
            }

            ship.UnloadRemainder += Rules.HandlingPerSecond * Rules.Dt * Rules.Centi;
            var budget = (long)ship.UnloadRemainder;
            ship.UnloadRemainder -= budget;
            var wood = Math.Min(Math.Min(budget, ship.HoldWood), freeWood);
            var silver = Math.Min(Math.Min(budget - wood, ship.HoldSilver), freeSilver);
            ship.HoldWood -= wood;
            port.Wood += wood;
            ship.HoldSilver -= silver;
            port.Silver += silver;
        }

        // ---------- Bridgehead ----------

        void StepToSite(Ship ship)
        {
            var site = FindSite(ship.SiteId);
            if (site == null || !IsSiteFree(site)) { Reject(ship); ClearOrder(ship); return; }
            if (Distance(ship.X, ship.Y, site.X, site.Y) > PortReach(ship))
            {
                Steer(ship, site.X, site.Y, ship.Definition.Speed, PortReach(ship) * 0.8);
                return;
            }
            StopShip(ship);
            ship.Transport = TransportPhase.Building;
            ship.BuildTicks = 0;
            ship.BuildUsedWood = ship.BuildUsedSilver = 0;
        }

        void StepBuilding(Ship ship)
        {
            StopShip(ship);
            var site = FindSite(ship.SiteId);
            if (site == null || !IsSiteFree(site)) { Reject(ship); ClearOrder(ship); return; }

            // Computed from total progress, so rounding never drifts. Materials leave the hold as they are used.
            var total = (int)(Rules.OutpostBuildSeconds * Rules.TickRate);
            ship.BuildTicks++;
            var wood = Math.Min(Rules.OutpostWood * ship.BuildTicks / total - ship.BuildUsedWood, ship.HoldWood);
            var silver = Math.Min(Rules.OutpostSilver * ship.BuildTicks / total - ship.BuildUsedSilver, ship.HoldSilver);
            ship.BuildUsedWood += wood;
            ship.BuildUsedSilver += silver;
            ship.HoldWood -= wood;
            ship.HoldSilver -= silver;
            SpentWood += wood;
            SpentSilver += silver;
            if (ship.BuildTicks < total) return;

            var outpost = new Settlement
            {
                Id = NextId++, Owner = ship.Owner, X = site.X, Y = site.Y,
                IsOutpost = true, HasShipyard = false, Hull = Rules.OutpostHull,
            };
            Settlements.Add(outpost);
            site.SettlementId = outpost.Id;
            Emit("outpost_built", ship.Id, outpost.Id, site.X, site.Y, 0);
            ClearOrder(ship);
        }

        // ---------- Escort ----------

        void StepEscort(Ship ship)
        {
            var escorted = FindShip(ship.EscortId);
            if (escorted == null || escorted.Owner != ship.Owner) { ClearOrder(ship); return; }

            // A threat counts only inside the zone around the transport, so the escort never chases beyond it.
            var threat = Ships
                .Where(item => item.Owner != ship.Owner && CanSee(ship.Owner, item) && Distance(item.X, item.Y, escorted.X, escorted.Y) <= Rules.EscortZone)
                .OrderBy(item => Distance2(item.X, item.Y, escorted.X, escorted.Y)).ThenBy(item => item.Id).FirstOrDefault();
            if (threat != null && HasWeapons(ship))
            {
                Pursue(ship, threat.X, threat.Y, Rules.ArcherRange * 0.8);
                return;
            }

            // A transport at rest: close up to the station distance and wait there.
            if (escorted.Speed < 0.5)
            {
                if (Distance(ship.X, ship.Y, escorted.X, escorted.Y) > Rules.EscortStation) Steer(ship, escorted.X, escorted.Y, ship.Definition.Speed, Rules.EscortStation);
                else StopShip(ship);
                return;
            }

            // Under way: keep a station abeam of the transport on the side the escort already keeps, never across
            // its bow. The station moves, so there is no slowing on arrival: behind it the escort makes full speed,
            // ahead of it the escort eases off, and on it the escort matches the transport.
            var cos = Math.Cos(escorted.Heading);
            var sin = Math.Sin(escorted.Heading);
            var side = -(ship.X - escorted.X) * sin + (ship.Y - escorted.Y) * cos >= 0 ? 1 : -1;
            var abeam = Rules.EscortStation * 0.8;
            var stationX = escorted.X - sin * abeam * side + cos * abeam * 0.5;
            var stationY = escorted.Y + cos * abeam * side + sin * abeam * 0.5;
            var ahead = (ship.X - stationX) * cos + (ship.Y - stationY) * sin;
            var off = Distance(ship.X, ship.Y, stationX, stationY);
            var wanted = ahead > 3 ? escorted.Speed * 0.8 : off > 6 ? ship.Definition.Speed : escorted.Speed;
            Steer(ship, stationX, stationY, Math.Min(ship.Definition.Speed, wanted), -25);
        }
    }
}
