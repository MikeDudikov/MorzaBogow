using System.Collections.Generic;
using MorzaBogow.Simulation;

namespace MorzaBogow.Presentation
{
    /// <summary>One man aboard as drawn: his lasting number on his ship, his trade, the role he fills and his station.</summary>
    public struct CrewPlace
    {
        public int Man;
        public CrewTrade Trade;
        public CrewRole Role;
        /// <summary>Index into the profile's stations; -1 when the ship has no room left to draw him.</summary>
        public int Station;
        /// <summary>The weapon post he serves, -1 for everyone else.</summary>
        public int Slot;
    }

    /// <summary>
    /// The men of one ship by trade, each with a number that lasts while he is aboard: a new man gets the next number of
    /// his trade, and a man who falls takes his number with him, so the others keep their looks and places. A ship seen
    /// for the first time, or after a load, numbers its men from zero in each trade.
    /// </summary>
    public sealed class CrewRoster
    {
        public const int TradeStride = 1000;
        readonly List<int>[] men = { new List<int>(), new List<int>(), new List<int>(), new List<int>(), new List<int>() };
        readonly int[] next = new int[5];

        public IReadOnlyList<int> Of(CrewTrade trade) => men[(int)trade];

        public int Count(CrewTrade trade) => men[(int)trade].Count;

        public static CrewTrade TradeOf(int man) => (CrewTrade)(man / TradeStride);

        public void Reset(Ship ship)
        {
            for (var t = 0; t < 5; t++)
            {
                men[t].Clear();
                next[t] = 0;
            }
            Match(ship, null);
        }

        /// <summary>
        /// Brings the roster to the simulation's numbers. Men who leave are chosen by the callback (the one nearest a blow,
        /// or at the front), else the last numbered; the numbers of those removed go into <paramref name="gone"/>.
        /// </summary>
        public bool Match(Ship ship, System.Func<CrewTrade, IReadOnlyList<int>, int> chooseLeaving, List<int> gone = null)
        {
            var changed = false;
            for (var t = 0; t < 5; t++)
            {
                var trade = (CrewTrade)t;
                var want = System.Math.Max(0, CrewRoles.Count(ship, trade));
                var list = men[t];
                while (list.Count < want)
                {
                    list.Add(t * TradeStride + next[t]++ % TradeStride);
                    changed = true;
                }
                while (list.Count > want)
                {
                    var man = chooseLeaving?.Invoke(trade, list) ?? -1;
                    var index = man >= 0 ? list.IndexOf(man) : -1;
                    if (index < 0) index = list.Count - 1;
                    gone?.Add(list[index]);
                    list.RemoveAt(index);
                    changed = true;
                }
            }
            return changed;
        }
    }

    /// <summary>
    /// Who does what aboard, from the simulation's numbers and nothing else (docs/art/ZALOGI.md):
    /// the weapon posts take exactly the men World.Manned gives them, trade by trade as the simulation draws them (the banks
    /// four of their own trade, the machines swordsmen first, then javelin men, then archers); sailors take the helm, then
    /// the oars in pairs from the stern, then the bow, the sail and the lines; a fighter leads, another watches at the bow;
    /// at sea the other fighters sit at the free oars and in the waist, and when the ship is ready to fight they leave the
    /// oars for their places on the platforms and the middle line. Never more men of a trade than the ship has; a man for
    /// whom no station is left is not drawn.
    /// </summary>
    public static class CrewManning
    {
        public static List<CrewPlace> Assign(CrewLayoutProfile profile, CrewRoster roster, WeaponType[] mounts, bool[] manned, bool ready)
        {
            var places = new List<CrewPlace>();
            var taken = new bool[profile.Stations.Count];
            var queues = new Queue<int>[5];
            for (var t = 0; t < 5; t++) queues[t] = new Queue<int>(roster.Of((CrewTrade)t));

            int Take(params CrewTrade[] order)
            {
                foreach (var trade in order)
                    if (queues[(int)trade].Count > 0) return queues[(int)trade].Dequeue();
                return -1;
            }

            int Station(StationKind kind, CrewRole role, int slot = -1)
            {
                var best = -1;
                for (var i = 0; i < profile.Stations.Count; i++)
                {
                    var station = profile.Stations[i];
                    if (taken[i] || station.Kind != kind || !station.Holds(role) || station.Slot != slot) continue;
                    if (best < 0 || station.Order < profile.Stations[best].Order) best = i;
                }
                if (best >= 0) taken[best] = true;
                return best;
            }

            void Place(int man, CrewRole role, int station, int slot = -1)
            {
                if (man < 0) return;
                places.Add(new CrewPlace { Man = man, Trade = CrewRoster.TradeOf(man), Role = role, Station = station, Slot = slot });
            }

            // Weapon posts, as the simulation mans them: banks first, each from four men of its own trade, then machines.
            for (var slot = 0; slot < mounts.Length && slot < manned.Length; slot++)
            {
                if (!manned[slot] || mounts[slot] == WeaponType.None) continue;
                var weapon = WeaponCatalog.Get(mounts[slot]);
                if (weapon.IsMachine) continue;
                var trade = weapon.CrewKind == WeaponCrew.Archers ? CrewTrade.Archer : CrewTrade.Javelin;
                var role = trade == CrewTrade.Archer ? CrewRole.BankArcher : CrewRole.BankJavelin;
                for (var i = 0; i < weapon.Crew; i++) Place(Take(trade), role, Station(StationKind.Post, role, slot), slot);
            }
            for (var slot = 0; slot < mounts.Length && slot < manned.Length; slot++)
            {
                if (!manned[slot] || mounts[slot] == WeaponType.None) continue;
                var weapon = WeaponCatalog.Get(mounts[slot]);
                if (!weapon.IsMachine) continue;
                for (var i = 0; i < weapon.Crew; i++) Place(Take(CrewTrade.Swordsman, CrewTrade.Javelin, CrewTrade.Archer), CrewRole.MachineCrew, Station(StationKind.Post, CrewRole.MachineCrew, slot), slot);
            }

            // Sailors: the helm, the oars in pairs, then the bow, the merchant's skipper and lookout, the sail and the lines.
            var helm = 0;
            foreach (var _ in profile.Of(StationKind.Helm)) helm++;
            for (var h = 0; h < helm && queues[(int)CrewTrade.Sailor].Count > 0; h++) Place(Take(CrewTrade.Sailor), CrewRole.Helmsman, Station(StationKind.Helm, CrewRole.Helmsman));
            var benches = Benches(profile);
            var pairs = System.Math.Min(benches.Count, queues[(int)CrewTrade.Sailor].Count / 2);
            for (var p = 0; p < pairs; p++)
                foreach (var bench in benches[p])
                {
                    taken[bench] = true;
                    Place(Take(CrewTrade.Sailor), CrewRole.Rower, bench);
                }
            var spareRoles = profile.Class == ShipClass.Cutter ? new[] { CrewRole.BowHand }
                : profile.Class == ShipClass.Merchant ? new[] { CrewRole.Skipper, CrewRole.Lookout, CrewRole.SailHand, CrewRole.SailHand, CrewRole.LineHand, CrewRole.LineHand, CrewRole.LineHand }
                : new[] { CrewRole.SailHand, CrewRole.SailHand, CrewRole.LineHand };
            foreach (var role in spareRoles)
            {
                if (queues[(int)CrewTrade.Sailor].Count == 0) break;
                var station = role == CrewRole.Skipper ? Station(StationKind.Command, role)
                    : role == CrewRole.BowHand ? Station(StationKind.Bow, role)
                    : role == CrewRole.Lookout ? Station(StationKind.Lookout, role)
                    : role == CrewRole.SailHand ? Station(StationKind.Sail, role) : Station(StationKind.Line, role);
                if (station < 0 && role == CrewRole.LineHand) station = Station(StationKind.Sail, role);
                if (station >= 0) Place(Take(CrewTrade.Sailor), role, station);
            }
            // Sailors left over pull an oar where a bench is free, or stand by.
            var fighters = new[] { CrewTrade.Swordsman, CrewTrade.Javelin, CrewTrade.Archer };
            for (var p = pairs; p < benches.Count; p++)
            {
                if (queues[(int)CrewTrade.Sailor].Count < 2) break;
                foreach (var bench in benches[p])
                {
                    taken[bench] = true;
                    Place(Take(CrewTrade.Sailor), CrewRole.Rower, bench);
                }
                pairs++;
            }

            // Fighters: the leader aft, a man at the bow; at sea the rest sit at free oars and in the waist.
            if (profile.Class != ShipClass.Cutter && profile.Class != ShipClass.Merchant)
            {
                var leader = Take(CrewTrade.Swordsman, CrewTrade.Javelin, CrewTrade.Archer);
                if (leader >= 0)
                {
                    var station = Station(StationKind.Command, CrewRole.Commander);
                    Place(leader, CrewRole.Commander, station >= 0 ? station : Station(ready ? StationKind.Stand : StationKind.Seat, CrewRole.Marine));
                }
                var watch = Take(CrewTrade.Javelin, CrewTrade.Archer, CrewTrade.Swordsman);
                if (watch >= 0)
                {
                    var station = Station(StationKind.Lookout, CrewRole.Lookout);
                    if (station >= 0) Place(watch, CrewRole.Lookout, station);
                    else Place(watch, CrewRole.Marine, Station(ready ? StationKind.Stand : StationKind.Seat, CrewRole.Marine));
                }
            }
            if (!ready)
            {
                for (var p = pairs; p < benches.Count; p++)
                {
                    var left = queues[(int)CrewTrade.Swordsman].Count + queues[(int)CrewTrade.Javelin].Count + queues[(int)CrewTrade.Archer].Count;
                    if (left < 2) break;
                    foreach (var bench in benches[p])
                    {
                        taken[bench] = true;
                        Place(Take(CrewTrade.Archer, CrewTrade.Javelin, CrewTrade.Swordsman), CrewRole.WarriorRower, bench);
                    }
                }
            }
            foreach (var trade in fighters)
                while (queues[(int)trade].Count > 0)
                {
                    var man = queues[(int)trade].Dequeue();
                    var station = Station(ready ? StationKind.Stand : StationKind.Seat, CrewRole.Marine);
                    if (station < 0) station = Station(ready ? StationKind.Seat : StationKind.Stand, CrewRole.Marine);
                    if (station < 0)
                    {
                        // Ready to fight but no place left to stand: he stays at an oar rather than vanish from the deck.
                        var bench = FreeBench(profile, taken);
                        if (bench >= 0)
                        {
                            taken[bench] = true;
                            Place(man, CrewRole.WarriorRower, bench);
                            continue;
                        }
                    }
                    Place(man, CrewRole.Marine, station);
                }
            // Sailors still without a place (a captured hull's prize crew can bring more than it has benches for).
            while (queues[(int)CrewTrade.Sailor].Count > 0)
                Place(queues[(int)CrewTrade.Sailor].Dequeue(), CrewRole.LineHand, Station(StationKind.Seat, CrewRole.Marine));

            // The wounded, as many as there are places for them.
            while (queues[(int)CrewTrade.Wounded].Count > 0)
                Place(queues[(int)CrewTrade.Wounded].Dequeue(), CrewRole.Wounded, Station(StationKind.Wounded, CrewRole.Wounded));
            return places;
        }

        static int FreeBench(CrewLayoutProfile profile, bool[] taken)
        {
            foreach (var pair in Benches(profile))
                foreach (var bench in pair)
                    if (!taken[bench]) return bench;
            return -1;
        }

        /// <summary>The benches in pairs, port and starboard at the same oar, in the order rowers take them.</summary>
        public static List<int[]> Benches(CrewLayoutProfile profile)
        {
            var port = new List<int>();
            var starboard = new List<int>();
            for (var i = 0; i < profile.Stations.Count; i++)
            {
                var station = profile.Stations[i];
                if (station.Kind != StationKind.Bench) continue;
                (station.Side < 0 ? port : starboard).Add(i);
            }
            port.Sort((a, b) => profile.Stations[a].Order.CompareTo(profile.Stations[b].Order));
            starboard.Sort((a, b) => profile.Stations[a].Order.CompareTo(profile.Stations[b].Order));
            var pairs = new List<int[]>();
            for (var i = 0; i < port.Count && i < starboard.Count; i++) pairs.Add(new[] { port[i], starboard[i] });
            return pairs;
        }
    }
}
