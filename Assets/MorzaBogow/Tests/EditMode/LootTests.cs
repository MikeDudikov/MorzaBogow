using System.Linq;
using MorzaBogow.Simulation;
using NUnit.Framework;

namespace MorzaBogow.Tests
{
    /// <summary>
    /// E06: what a fallen settlement and a sunk hull leave to be salvaged. A settlement's reservations are settled first,
    /// then 30% of its unspent stores and the arsenal's surviving items lie at its port point; every item aboard a
    /// sinking hull survives with a 30% chance, drawn once; a cutter lifts the items and its port's arsenal gets them.
    /// </summary>
    public sealed class LootTests
    {
        static World Empty()
        {
            var world = new World(7);
            world.Settlements.Add(new Settlement { Id = world.NextId++, Owner = 0, X = -500, Y = 0, Wood = 2000 * Rules.Centi, Silver = 1500 * Rules.Centi });
            world.Settlements.Add(new Settlement { Id = world.NextId++, Owner = 1, X = 500, Y = 0, Wood = 2000 * Rules.Centi, Silver = 1500 * Rules.Centi });
            for (var i = 0; i < world.Explored.Length; i++) world.Explored[i] = 3;
            return world;
        }

        static Settlement AddOutpost(World world, int owner, double x, long wood, long silver)
        {
            var outpost = new Settlement
            {
                Id = world.NextId++, Owner = owner, X = x, Y = 0, IsOutpost = true, HasShipyard = false, Hull = Rules.OutpostHull,
                Wood = wood * Rules.Centi, Silver = silver * Rules.Centi,
            };
            world.Settlements.Add(outpost);
            return outpost;
        }

        static void Run(World world, double seconds)
        {
            for (var i = 0; i < (int)(seconds * Rules.TickRate); i++) world.Step();
        }

        /// <summary>A last bolt from the other side lands on the settlement's land next tick, past any hull on the water.</summary>
        static void Strike(World world, Settlement settlement)
        {
            settlement.Hull = 1;
            var landward = Rules.SettlementLandX(settlement) < settlement.X ? -15 : 15;
            world.Projectiles.Add(new Projectile
            {
                Id = world.NextProjectileId++, OwnerId = 1 - settlement.Owner, ShooterId = -1, Weapon = WeaponType.DeckBallista,
                FromX = settlement.X + landward * 10, FromY = settlement.Y + 60, ToX = settlement.X + landward, ToY = settlement.Y,
                LaunchTick = world.Tick, ArriveTick = world.Tick + 1,
            });
        }

        [Test]
        public void Fall_SettlesTheReservationsThenLeavesAThirdOfTheStoresAtThePort()
        {
            var world = Empty();
            var port = AddOutpost(world, 1, 300, 1000, 400);
            port.HasShipyard = true;
            world.Issue(new Command { Faction = 1, Kind = CommandKind.QueueShip, Class = ShipClass.Ram, TargetId = port.Id });
            Run(world, 2);
            Assert.That(port.WarQueue.Count, Is.EqualTo(1));
            var reservedWood = port.ReservedWood;
            var reservedSilver = port.ReservedSilver;
            Assert.That(reservedWood, Is.GreaterThan(0), "The ram's unused price is still reserved.");

            var woodTotal = world.LedgerTotal(ResourceType.Wood);
            var silverTotal = world.LedgerTotal(ResourceType.Silver);
            var wood = port.Wood;
            var silver = port.Silver;
            var spentWood = world.SpentWood;
            var spentSilver = world.SpentSilver;
            var lostWood = world.LostWood;
            var lostSilver = world.LostSilver;
            Strike(world, port);
            world.Step();

            Assert.That(port.Destroyed, Is.True);
            Assert.That(world.Events.Any(item => item.Type == "settlement_fallen" && item.SubjectId == port.Id), Is.True);
            Assert.That(port.WarQueue, Is.Empty, "The ram on the slipway is gone with the port.");
            Assert.That(port.ReservedWood, Is.Zero);
            Assert.That(port.ReservedSilver, Is.Zero);
            Assert.That(port.Wood, Is.Zero);
            Assert.That(port.Silver, Is.Zero);

            // The store as it stood when it fell: the reserved part counts, it was never spent.
            var woodAtFall = wood - (world.SpentWood - spentWood);
            var silverAtFall = silver - (world.SpentSilver - spentSilver);
            var wreck = world.Wrecks.Single();
            Assert.That(wreck.X, Is.EqualTo(port.X));
            Assert.That(wreck.Y, Is.EqualTo(port.Y));
            Assert.That(wreck.Wood, Is.EqualTo((long)(woodAtFall * Rules.SettlementLootFraction)));
            Assert.That(wreck.Silver, Is.EqualTo((long)(silverAtFall * Rules.SettlementLootFraction)));
            Assert.That(wreck.Wood, Is.GreaterThan((long)((woodAtFall - reservedWood) * Rules.SettlementLootFraction)), "Reservations settled before the loot.");
            Assert.That(world.LostWood - lostWood, Is.EqualTo(woodAtFall - wreck.Wood), "The rest is lost.");
            Assert.That(world.LostSilver - lostSilver, Is.EqualTo(silverAtFall - wreck.Silver));
            Assert.That(world.LedgerTotal(ResourceType.Wood), Is.EqualTo(woodTotal));
            Assert.That(world.LedgerTotal(ResourceType.Silver), Is.EqualTo(silverTotal));
            Assert.That(reservedSilver, Is.GreaterThan(0));
        }

        [Test]
        public void Fall_TheArsenalsItemsSurviveTheirDrawIntoTheWreck()
        {
            int Survivors()
            {
                var world = Empty();
                var port = AddOutpost(world, 1, 300, 0, 0);
                port.Arsenal[(int)WeaponType.Scorpion] = 100;
                port.Arsenal[(int)WeaponType.HeavyCatapult] = 2;
                Strike(world, port);
                world.Step();
                Assert.That(port.Arsenal.Sum(), Is.Zero, "Nothing stays in a fallen arsenal.");
                var wreck = world.Wrecks.Single();
                Assert.That(wreck.Wood + wreck.Silver, Is.Zero, "An empty store leaves no goods.");
                Assert.That(wreck.Items[(int)WeaponType.HeavyCatapult], Is.InRange(0, 2));
                return wreck.Items[(int)WeaponType.Scorpion];
            }

            var survivors = Survivors();
            Assert.That(survivors, Is.InRange(15, 45), "About 30 of 100 survive.");
            Assert.That(Survivors(), Is.EqualTo(survivors), "Drawn from a hash, the same every time.");
        }

        [Test]
        public void Fall_GoodsAtSeaStayOnTheShipsAndTheirBookingsEnd()
        {
            var world = Empty();
            var port = AddOutpost(world, 1, 300, 1000, 0);
            var home = world.SettlementOf(1);
            var merchant = world.SpawnShip(1, ShipClass.Merchant, 300, 30);
            world.Issue(new Command { Faction = 1, Kind = CommandKind.Transfer, ShipIds = new[] { merchant.Id }, TargetId = home.Id, SourceId = port.Id, Wood = 400 * Rules.Centi });
            for (var i = 0; i < 60 * Rules.TickRate && merchant.HoldWood == 0; i++) world.Step();
            world.Step();
            Assert.That(merchant.HoldWood, Is.GreaterThan(0), "Loading has begun.");
            Assert.That(merchant.WantWood, Is.GreaterThan(0), "And is not done.");

            Strike(world, port);
            world.Step();
            Assert.That(port.Destroyed, Is.True);
            // What came aboard before the bolt landed, this tick included, is the ship's.
            var aboard = merchant.HoldWood;
            Assert.That(merchant.WantWood, Is.Zero, "The booking ended with the store.");
            Assert.That(merchant.Hull, Is.EqualTo(merchant.Definition.Hull), "The bolt fell on the land.");
            Run(world, 1);
            Assert.That(merchant.HoldWood, Is.EqualTo(aboard), "Goods at sea stay on the ship.");
            Assert.That(merchant.Order, Is.Not.EqualTo(OrderKind.Transfer));
            Assert.That(port.ReservedWood, Is.Zero, "Nothing is released twice.");
        }

        [Test]
        public void Wreck_MachinesAboardASunkHullSurviveAThirdOfTheTime()
        {
            (int items, int wrecks, int[] perWreck) Sink()
            {
                var world = Empty();
                var ships = Enumerable.Range(0, 60).Select(i => world.SpawnShip(0, ShipClass.ShipKiller, -200 + i * 5, 300)).ToList();
                var destroyer = world.SpawnShip(0, ShipClass.CrewDestroyer, 0, 500);
                foreach (var ship in ships) ship.Hull = 0;
                destroyer.Hull = 0;
                world.Step();
                Assert.That(world.Wrecks.All(wreck => wreck.Wood + wreck.Silver == 0 && wreck.ItemCount > 0), Is.True, "Empty hulls pay out only items.");
                Assert.That(world.Wrecks.All(wreck => wreck.Items[(int)WeaponType.ArcherBank] == 0), Is.True, "A bank is the crew's post, not an item.");
                return (world.Wrecks.Sum(wreck => wreck.Items[(int)WeaponType.DeckBallista]), world.Wrecks.Count, world.Wrecks.Select(wreck => wreck.ItemCount).ToArray());
            }

            var first = Sink();
            Assert.That(first.items, Is.InRange(22, 50), "About 36 of 120 ballistas survive.");
            Assert.That(first.wrecks, Is.LessThan(60), "A hull none of whose items survived leaves no wreck.");
            var second = Sink();
            Assert.That(second.perWreck, Is.EqualTo(first.perWreck), "Drawn once from a hash: the same sinking, the same survivors.");
        }

        [Test]
        public void Salvage_ACutterLiftsTheItemsAndBringsThemToItsPortsArsenal()
        {
            var world = Empty();
            var home = world.SettlementOf(0);
            var wreck = new Wreck { Id = world.NextId++, X = -450, Y = 0, Wood = 20 * Rules.Centi, ExpiresTick = 100000 };
            wreck.Items[(int)WeaponType.DeckBallista] = 2;
            wreck.Items[(int)WeaponType.Onager] = 1;
            world.Wrecks.Add(wreck);
            var cutter = world.SpawnShip(0, ShipClass.Cutter, -470, 0);
            var wood = home.Wood;
            var woodTotal = world.LedgerTotal(ResourceType.Wood);
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Salvage, ShipIds = new[] { cutter.Id }, TargetId = wreck.Id });
            var carried = 0;
            for (var i = 0; i < 40 * Rules.TickRate; i++)
            {
                world.Step();
                carried = System.Math.Max(carried, cutter.CarriedItemCount);
            }
            Assert.That(carried, Is.EqualTo(3), "Lifted aboard together.");
            Assert.That(world.Wrecks, Is.Empty);
            Assert.That(home.Arsenal[(int)WeaponType.DeckBallista], Is.EqualTo(2));
            Assert.That(home.Arsenal[(int)WeaponType.Onager], Is.EqualTo(1));
            Assert.That(cutter.CarriedItemCount, Is.Zero);
            Assert.That(home.Wood - wood, Is.EqualTo(20 * Rules.Centi));
            Assert.That(world.LedgerTotal(ResourceType.Wood), Is.EqualTo(woodTotal));
            Assert.That(cutter.Order, Is.EqualTo(OrderKind.Idle));
            Assert.That(world.Events.Any(item => item.Type == "items_unloaded" && item.SubjectId == cutter.Id), Is.True);
        }

        [Test]
        public void Salvage_AWreckOfItemsAloneIsWorthTheTrip()
        {
            var world = Empty();
            var home = world.SettlementOf(0);
            var wreck = new Wreck { Id = world.NextId++, X = -440, Y = 20, ExpiresTick = 100000 };
            wreck.Items[(int)WeaponType.Scorpion] = 1;
            world.Wrecks.Add(wreck);
            Assert.That(wreck.Empty, Is.False, "Items alone keep a wreck afloat until it expires.");
            Run(world, 1);
            Assert.That(world.Wrecks, Has.Count.EqualTo(1));

            var cutter = world.SpawnShip(0, ShipClass.Cutter, -470, 0);
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Salvage, ShipIds = new[] { cutter.Id }, TargetId = wreck.Id });
            Run(world, 40);
            Assert.That(home.Arsenal[(int)WeaponType.Scorpion], Is.EqualTo(1));
            Assert.That(world.Wrecks, Is.Empty);
            Assert.That(cutter.Order, Is.EqualTo(OrderKind.Idle));
        }

        [Test]
        public void Scrap_AHullCarryingMachinesLeavesThemInTheArsenal()
        {
            var world = Empty();
            var home = world.SettlementOf(0);
            var cutter = world.SpawnShip(0, ShipClass.Cutter, home.X, 0);
            cutter.CarriedItems[(int)WeaponType.Scorpion] = 2;
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Scrap, ShipIds = new[] { cutter.Id } });
            Run(world, 21);
            Assert.That(world.FindShip(cutter.Id), Is.Null, "Scrapped.");
            Assert.That(home.Arsenal[(int)WeaponType.Scorpion], Is.EqualTo(2), "The machines it carried stay items.");
        }

        static (World world, Ship cutter, Wreck wreck) AiBeside(double wreckX, double wreckY)
        {
            var world = Empty();
            var cutter = world.SpawnShip(1, ShipClass.Cutter, 470, 0);
            var wreck = new Wreck { Id = world.NextId++, X = wreckX, Y = wreckY, Wood = 30 * Rules.Centi, ExpiresTick = 100000 };
            world.Wrecks.Add(wreck);
            return (world, cutter, wreck);
        }

        static void Think(World world, Opponent opponent, double seconds)
        {
            for (var i = 0; i < (int)(seconds * Rules.TickRate); i++)
            {
                opponent.Think(world);
                world.Step();
            }
        }

        [Test]
        public void Opponent_SendsAFreeCutterForAWreckNearItsPort()
        {
            var (world, cutter, wreck) = AiBeside(430, 40);
            var lost = world.LostWood;
            var opponent = new Opponent(1);
            Think(world, opponent, 1);
            Assert.That(cutter.Order, Is.EqualTo(OrderKind.Salvage));
            Assert.That(cutter.WreckId, Is.EqualTo(wreck.Id));
            Think(world, opponent, 60);
            Assert.That(world.Wrecks, Is.Empty, "Collected, as the player's cutters collect.");
            Assert.That(world.LostWood, Is.EqualTo(lost), "Nothing of it left to sink.");
        }

        [Test]
        public void Opponent_LeavesAWreckWithAnEnemyBesideItOrFarFromItsPorts()
        {
            var (guarded, guardedCutter, _) = AiBeside(430, 40);
            guarded.SpawnShip(0, ShipClass.Ram, 420, 120);
            Think(guarded, new Opponent(1), 1);
            Assert.That(guardedCutter.Order, Is.Not.EqualTo(OrderKind.Salvage), "An enemy is seen beside it.");

            var (far, farCutter, _) = AiBeside(0, 0);
            far.SpawnShip(1, ShipClass.Cutter, 0, 60);
            Think(far, new Opponent(1), 1);
            Assert.That(farCutter.Order, Is.Not.EqualTo(OrderKind.Salvage), "500 m from the nearest port of its own.");
            Assert.That(far.Ships.All(ship => ship.Order != OrderKind.Salvage), Is.True);
        }

        [Test]
        public void Save_KeepsTheWrecksItemsAndWhatAHullCarries()
        {
            var world = Empty();
            var wreck = new Wreck { Id = world.NextId++, X = 10, Y = 20, Silver = 5 * Rules.Centi, ExpiresTick = 9000 };
            wreck.Items[(int)WeaponType.LeadBallista] = 2;
            world.Wrecks.Add(wreck);
            var cutter = world.SpawnShip(0, ShipClass.Cutter, -470, 0);
            cutter.CarriedItems[(int)WeaponType.StoneCatapult] = 3;

            var data = SaveGame.Serialize(world);
            var loaded = SaveGame.Deserialize(data, out _);
            Assert.That(SaveGame.Serialize(loaded), Is.EqualTo(data));
            Assert.That(loaded.Wrecks.Single().Items[(int)WeaponType.LeadBallista], Is.EqualTo(2));
            Assert.That(loaded.FindShip(cutter.Id).CarriedItems[(int)WeaponType.StoneCatapult], Is.EqualTo(3));
        }
    }
}
