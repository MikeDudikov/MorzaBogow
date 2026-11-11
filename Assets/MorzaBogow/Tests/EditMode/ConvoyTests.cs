using System.Collections.Generic;
using System.Linq;
using MorzaBogow.Simulation;
using NUnit.Framework;

namespace MorzaBogow.Tests
{
    /// <summary>
    /// E03 automatic convoys: a merchant on convoy duty brings the surplus above each port's minimum from the port with
    /// the most of it to its side's priority port, up to the price of the costliest ship that port may build; up to
    /// three merchants load at one source and sail together; the side names its priority port and each port's minimum.
    /// </summary>
    public sealed class ConvoyTests
    {
        static World Empty(long homeWood = 0, long homeSilver = 0)
        {
            var world = new World(7);
            world.Settlements.Add(new Settlement { Id = world.NextId++, Owner = 0, X = -500, Y = 0, Wood = homeWood * Rules.Centi, Silver = homeSilver * Rules.Centi });
            world.Settlements.Add(new Settlement { Id = world.NextId++, Owner = 1, X = 500, Y = 0, Wood = 2000 * Rules.Centi, Silver = 1500 * Rules.Centi });
            for (var i = 0; i < world.Explored.Length; i++) world.Explored[i] = 3;
            return world;
        }

        static Settlement AddOutpost(World world, double x, long wood, long silver)
        {
            var outpost = new Settlement
            {
                Id = world.NextId++, Owner = 0, X = x, Y = 0, IsOutpost = true, HasShipyard = false, Hull = Rules.OutpostHull,
                Wood = wood * Rules.Centi, Silver = silver * Rules.Centi,
            };
            world.Settlements.Add(outpost);
            return outpost;
        }

        static void Convoy(World world, params Ship[] ships) =>
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Convoy, ShipIds = ships.Select(ship => ship.Id).ToArray() });

        /// <summary>Steps until every merchant is back on standby with an empty hold after at least one delivery, or the time runs out.</summary>
        static void UntilDelivered(World world, double seconds, params Ship[] ships)
        {
            var sailed = new HashSet<int>();
            for (var i = 0; i < (int)(seconds * Rules.TickRate); i++)
            {
                world.Step();
                foreach (var ship in ships) if (ship.Transport != TransportPhase.Standby) sailed.Add(ship.Id);
                if (ships.All(ship => sailed.Contains(ship.Id) && ship.Transport == TransportPhase.Standby && ship.HoldWood + ship.HoldSilver == 0)) return;
            }
        }

        static void Run(World world, double seconds)
        {
            for (var i = 0; i < (int)(seconds * Rules.TickRate); i++) world.Step();
        }

        [Test]
        public void Convoy_StocksThePriorityPortWithThePriceOfItsCostliestShip()
        {
            var world = Empty(50, 20);
            var home = world.SettlementOf(0);
            var outpost = AddOutpost(world, -200, 2000, 1000);
            // A shipyard without a workshop: the boarder, 200 D / 180 S, is the costliest ship home may build.
            Assert.That(world.ConvoyGoal(home), Is.EqualTo((200 * Rules.Centi, 180 * Rules.Centi)));
            var merchant = world.SpawnShip(0, ShipClass.Merchant, -480, 30);
            var woodTotal = world.LedgerTotal(ResourceType.Wood);
            var silverTotal = world.LedgerTotal(ResourceType.Silver);
            Convoy(world, merchant);
            UntilDelivered(world, 240, merchant);

            Assert.That(home.Wood, Is.EqualTo(200 * Rules.Centi), "Up to the price, no more.");
            Assert.That(home.Silver, Is.EqualTo(180 * Rules.Centi));
            Assert.That(outpost.Wood, Is.EqualTo(1850 * Rules.Centi));
            Assert.That(outpost.Silver, Is.EqualTo(840 * Rules.Centi));
            Assert.That(merchant.Order, Is.EqualTo(OrderKind.Convoy), "Still on convoy duty.");
            Assert.That(merchant.Transport, Is.EqualTo(TransportPhase.Standby));
            Assert.That(world.LedgerTotal(ResourceType.Wood), Is.EqualTo(woodTotal));
            Assert.That(world.LedgerTotal(ResourceType.Silver), Is.EqualTo(silverTotal));

            Run(world, 10);
            Assert.That(merchant.Transport, Is.EqualTo(TransportPhase.Standby), "Nothing lacking, it waits.");
            Assert.That(outpost.Wood, Is.EqualTo(1850 * Rules.Centi));

            // Ordering a ship takes the price from the free stock: the convoy brings the next one's.
            home.ReservedWood += 200 * Rules.Centi;
            home.ReservedSilver += 180 * Rules.Centi;
            Run(world, 2);
            Assert.That(merchant.Transport, Is.Not.EqualTo(TransportPhase.Standby));
            Assert.That(merchant.WantWood + merchant.WantSilver, Is.EqualTo(380 * Rules.Centi));
        }

        [Test]
        public void Convoy_NeverTakesASourceBelowItsMinimum()
        {
            var world = Empty();
            var home = world.SettlementOf(0);
            var outpost = AddOutpost(world, -200, 300, 100);
            world.Issue(new Command { Faction = 0, Kind = CommandKind.SetPortMinimum, TargetId = outpost.Id, Wood = 250 * Rules.Centi, Silver = 80 * Rules.Centi });
            var merchant = world.SpawnShip(0, ShipClass.Merchant, -480, 30);
            Convoy(world, merchant);
            UntilDelivered(world, 240, merchant);

            Assert.That(outpost.KeepWood, Is.EqualTo(250 * Rules.Centi));
            Assert.That(outpost.Wood, Is.EqualTo(250 * Rules.Centi), "The minimum stays.");
            Assert.That(outpost.Silver, Is.EqualTo(80 * Rules.Centi));
            Assert.That(home.Wood, Is.EqualTo(50 * Rules.Centi));
            Assert.That(home.Silver, Is.EqualTo(20 * Rules.Centi));
            Run(world, 20);
            Assert.That(outpost.Wood, Is.EqualTo(250 * Rules.Centi), "Nothing above the minimum is left, so it waits.");
            Assert.That(merchant.Transport, Is.EqualTo(TransportPhase.Standby));
        }

        [Test]
        public void Convoy_LoadsAtThePortWithTheMostSurplus()
        {
            var world = Empty();
            var thin = AddOutpost(world, -300, 400, 100);
            var rich = AddOutpost(world, -100, 2000, 1000);
            var merchant = world.SpawnShip(0, ShipClass.Merchant, -310, 30);
            Convoy(world, merchant);
            Run(world, 2);
            Assert.That(merchant.SourceId, Is.EqualTo(rich.Id), "The rich port covers the whole need, the near one less.");
            Assert.That(thin.ReservedWood, Is.Zero);
            Assert.That(rich.ReservedWood + rich.ReservedSilver, Is.EqualTo(380 * Rules.Centi));
        }

        [Test]
        public void Convoy_ThePriorityPortCanBeNamedAndFallsBackHome()
        {
            var world = Empty();
            var home = world.SettlementOf(0);
            var outpost = AddOutpost(world, -200, 0, 0);
            Assert.That(world.PriorityPort(0), Is.SameAs(home), "By default the home port.");
            world.Issue(new Command { Faction = 0, Kind = CommandKind.SetPriorityPort, TargetId = outpost.Id });
            world.Step();
            Assert.That(world.PriorityPort(0), Is.SameAs(outpost));
            world.Issue(new Command { Faction = 0, Kind = CommandKind.SetPriorityPort, TargetId = world.SettlementOf(1).Id });
            world.Step();
            Assert.That(world.PriorityPort(0), Is.SameAs(outpost), "Not the enemy's port.");
            // A bridgehead without a shipyard builds merchants at most: its goal is a merchant's price.
            Assert.That(world.ConvoyGoal(outpost), Is.EqualTo((140 * Rules.Centi, 60 * Rules.Centi)));
            outpost.Hull = 0;
            Assert.That(world.PriorityPort(0), Is.SameAs(home), "A lost port gives way to home.");
        }

        [Test]
        public void Convoy_MerchantsLoadTogetherAndSailTogether()
        {
            var world = Empty();
            var home = world.SettlementOf(0);
            home.HasWorkshop = true;
            world.HeavyArmsResearched |= 1;
            // The bomber, 400 D / 260 S, is more than one hold of 480: two merchants are needed and a third waits.
            Assert.That(world.ConvoyGoal(home), Is.EqualTo((400 * Rules.Centi, 260 * Rules.Centi)));
            var source = AddOutpost(world, -200, 3000, 1500);
            var merchants = new[] { world.SpawnShip(0, ShipClass.Merchant, -470, 40), world.SpawnShip(0, ShipClass.Merchant, -470, 80), world.SpawnShip(0, ShipClass.Merchant, -470, 120) };
            Convoy(world, merchants);

            var lastLoading = -1;
            var departed = new Dictionary<int, int>();
            for (var i = 0; i < 240 * Rules.TickRate; i++)
            {
                world.Step();
                var group = merchants.Where(ship => ship.ConvoyGroup >= 0).ToList();
                if (group.Any(ship => ship.Transport <= TransportPhase.Loading)) lastLoading = world.Tick;
                foreach (var ship in group)
                {
                    if (!departed.ContainsKey(ship.Id) && ship.Transport == TransportPhase.ToDestination && World.Distance(ship.X, ship.Y, source.X, source.Y) > 60) departed[ship.Id] = world.Tick;
                }
                if (home.Available(ResourceType.Wood) >= 400 * Rules.Centi && home.Available(ResourceType.Silver) >= 260 * Rules.Centi) break;
            }

            Assert.That(departed.Count, Is.EqualTo(2), "Two holds for one bomber.");
            var sailed = merchants.Where(ship => departed.ContainsKey(ship.Id)).ToList();
            Assert.That(departed.Values.All(tick => tick > lastLoading), Is.True, "Nobody left while the other was loading.");
            Assert.That(merchants.Single(ship => !departed.ContainsKey(ship.Id)).Transport, Is.EqualTo(TransportPhase.Standby), "The third waits: nothing left to carry.");
            Assert.That(home.Wood, Is.EqualTo(400 * Rules.Centi));
            Assert.That(home.Silver, Is.EqualTo(260 * Rules.Centi));
            Assert.That(sailed.Count, Is.EqualTo(2));
        }

        [Test]
        public void Convoy_AGroupTakesThreeMerchantsAtMost()
        {
            Ship Fourth(int members)
            {
                var world = Empty();
                var home = world.SettlementOf(0);
                var source = AddOutpost(world, -200, 3000, 1500);
                // A group forming at the source: its members booked one unit each, so the home port still lacks almost everything.
                for (var i = 0; i < members; i++)
                {
                    var member = world.SpawnShip(0, ShipClass.Merchant, -220, 40 + 30 * i);
                    member.Order = OrderKind.Convoy;
                    member.Transport = TransportPhase.ToSource;
                    member.ConvoyGroup = 900;
                    member.SourceId = source.Id;
                    member.DestinationId = home.Id;
                    member.WantWood = Rules.Centi;
                    source.ReservedWood += Rules.Centi;
                }
                var newcomer = world.SpawnShip(0, ShipClass.Merchant, -480, 200);
                Convoy(world, newcomer);
                Run(world, 2);
                Assert.That(newcomer.SourceId, Is.EqualTo(source.Id));
                return newcomer;
            }

            Assert.That(Fourth(2).ConvoyGroup, Is.EqualTo(900), "A third merchant joins.");
            Assert.That(Fourth(3).ConvoyGroup, Is.Not.EqualTo(900), "A fourth starts another convoy.");
        }

        [Test]
        public void Transfer_WithoutAmountsKeepsTheSourcesMinimum()
        {
            var world = Empty();
            var home = world.SettlementOf(0);
            home.Wood = 2000 * Rules.Centi;
            home.KeepWood = 1900 * Rules.Centi;
            var outpost = AddOutpost(world, -200, 0, 0);
            var merchant = world.SpawnShip(0, ShipClass.Merchant, -480, 30);
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Transfer, ShipIds = new[] { merchant.Id }, TargetId = outpost.Id, SourceId = home.Id });
            world.Step();
            Assert.That(merchant.WantWood, Is.EqualTo(100 * Rules.Centi), "An automatic load takes only what is above the minimum.");
            // An explicit amount is the player's own order and may go below it.
            var other = world.SpawnShip(0, ShipClass.Merchant, -480, 60);
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Transfer, ShipIds = new[] { other.Id }, TargetId = outpost.Id, SourceId = home.Id, Wood = 300 * Rules.Centi });
            world.Step();
            Assert.That(other.WantWood, Is.EqualTo(300 * Rules.Centi));
        }

        [Test]
        public void Save_MidConvoyContinuesIdentically()
        {
            var world = Empty(30, 10);
            var outpost = AddOutpost(world, -200, 3000, 1500);
            world.SettlementOf(0).HasWorkshop = true;
            world.HeavyArmsResearched |= 1;
            world.Issue(new Command { Faction = 0, Kind = CommandKind.SetPortMinimum, TargetId = outpost.Id, Wood = 500 * Rules.Centi, Silver = 200 * Rules.Centi });
            var merchants = new[] { world.SpawnShip(0, ShipClass.Merchant, -470, 40), world.SpawnShip(0, ShipClass.Merchant, -470, 80) };
            Convoy(world, merchants);
            Run(world, 30);
            Assert.That(merchants.Any(ship => ship.HoldWood + ship.HoldSilver + ship.WantWood + ship.WantSilver > 0), Is.True, "Saved with goods booked or aboard.");

            var data = SaveGame.Serialize(world);
            var loaded = SaveGame.Deserialize(data, out _);
            Assert.That(SaveGame.Serialize(loaded), Is.EqualTo(data));
            Assert.That(loaded.FindSettlement(outpost.Id).KeepWood, Is.EqualTo(500 * Rules.Centi));
            Assert.That(loaded.FindShip(merchants[0].Id).ConvoyGroup, Is.EqualTo(merchants[0].ConvoyGroup));
            Run(world, 60);
            Run(loaded, 60);
            Assert.That(SaveGame.Hash(SaveGame.Serialize(loaded)), Is.EqualTo(SaveGame.Hash(SaveGame.Serialize(world))));
            Assert.That(loaded.LedgerTotal(ResourceType.Silver), Is.EqualTo(world.LedgerTotal(ResourceType.Silver)));
        }
    }
}
