using System.Linq;
using MorzaBogow.Simulation;

namespace MorzaBogow.Presentation
{
    /// <summary>
    /// E03 automatic convoys from the interface (World.Convoy): merchants put on convoy duty or taken off it, the side's
    /// priority port and each port's minimum against automatic loads. Every change is an ordinary command.
    /// </summary>
    public sealed partial class BattleController
    {
        /// <summary>What a port may keep against automatic loads, in units: none, a cutter's reserve (the default), more.</summary>
        public static readonly (long wood, long silver)[] PortMinimums = { (0, 0), (60, 20), (200, 80), (500, 200) };

        /// <summary>The selected own merchants go on convoy duty; when all of them are on it already, they come off it.</summary>
        public void ToggleConvoySelected()
        {
            var merchants = selected.Select(World.FindShip).Where(ship => ship != null && ship.Owner == PlayerFaction && ship.Class == ShipClass.Merchant).ToList();
            if (merchants.Count == 0) return;
            var off = merchants.All(ship => ship.Order == OrderKind.Convoy);
            Send(off ? CommandKind.Stop : CommandKind.Convoy, merchants.Select(ship => ship.Id).ToArray());
        }

        /// <summary>Every idle own merchant goes on convoy duty; returns how many.</summary>
        public int ConvoyIdleMerchants()
        {
            var ids = World.Ships.Where(ship => ship.Owner == PlayerFaction && ship.Class == ShipClass.Merchant && ship.Order == OrderKind.Idle).Select(ship => ship.Id).ToArray();
            Send(CommandKind.Convoy, ids);
            return ids.Length;
        }

        public void SetPriorityPort(Settlement port)
        {
            if (port != null) World.Issue(new Command { Faction = PlayerFaction, Kind = CommandKind.SetPriorityPort, TargetId = port.Id });
        }

        /// <summary>Amounts in units; the command carries them in 0.01 units.</summary>
        public void SetPortMinimum(Settlement port, long wood, long silver)
        {
            if (port != null) World.Issue(new Command { Faction = PlayerFaction, Kind = CommandKind.SetPortMinimum, TargetId = port.Id, Wood = wood * Rules.Centi, Silver = silver * Rules.Centi });
        }

        /// <summary>Plain language for a merchant on convoy duty.</summary>
        string DescribeConvoy(Ship ship)
        {
            switch (ship.Transport)
            {
                case TransportPhase.Standby: return "Rozkaz: konwój, czeka na ładunek.";
                case TransportPhase.ToSource: return "Rozkaz: konwój, płynie po ładunek.";
                case TransportPhase.WaitSourceBerth:
                case TransportPhase.WaitDestinationBerth: return "Rozkaz: konwój, czeka na miejsce w porcie.";
                case TransportPhase.Loading: return "Rozkaz: konwój, załadunek.";
                case TransportPhase.Unloading: return "Rozkaz: konwój, rozładunek.";
                default: return World.ConvoyStillLoading(ship) ? "Rozkaz: konwój, czeka na resztę konwoju." : "Rozkaz: konwój, płynie do portu priorytetowego.";
            }
        }
    }
}
