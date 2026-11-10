using System.Linq;
using MorzaBogow.Simulation;
using NUnit.Framework;

namespace MorzaBogow.Tests
{
    /// <summary>The stage 3c classes, their conditions, bare-hull orders and the opponent's way to them (docs/UZBROJENIE.md).</summary>
    public sealed class ShipClassTests
    {
        static World Empty(ulong seed = 7)
        {
            var world = new World(seed);
            world.Settlements.Add(new Settlement { Id = world.NextId++, Owner = 0, X = -500, Y = 0, Wood = 4000 * Rules.Centi, Silver = 3000 * Rules.Centi });
            world.Settlements.Add(new Settlement { Id = world.NextId++, Owner = 1, X = 500, Y = 0, Wood = 2000 * Rules.Centi, Silver = 1500 * Rules.Centi });
            return world;
        }

        static void Run(World world, double seconds)
        {
            for (var i = 0; i < (int)(seconds * Rules.TickRate); i++) world.Step();
        }

        static void Order(World world, ShipClass shipClass, bool unarmed = false) =>
            world.Issue(new Command { Faction = 0, Kind = CommandKind.QueueShip, Class = shipClass, TargetId = world.SettlementOf(0).Id, Unarmed = unarmed });

        [Test]
        public void Classes_TheNewWarshipsHaveCompleteTemplatesThatFitTheirPosts()
        {
            foreach (var shipClass in new[] { ShipClass.ShipKiller, ShipClass.Bomber })
            {
                var definition = ShipCatalog.Get(shipClass);
                Assert.That(definition.Class, Is.EqualTo(shipClass));
                Assert.That(definition.IsEconomic, Is.False);
                Assert.That(definition.CanRam, Is.True, "Every warship has a ram.");
                Assert.That(definition.DefaultLoadout.Length, Is.EqualTo(definition.Slots.Length));
                for (var i = 0; i < definition.Slots.Length; i++)
                    Assert.That(WeaponCatalog.Fits(definition.DefaultLoadout[i], definition.Slots[i]), Is.True, $"{shipClass} post {i}.");
                Assert.That(TemplatePricing.ConstructionSilver(definition), Is.GreaterThan(0), $"{shipClass}: the template pays its crew and still builds the hull.");
                Assert.That(TemplatePricing.EquipmentWood(definition), Is.GreaterThan(0));
                Assert.That(TemplatePricing.EquipmentSilver(definition), Is.GreaterThan(0));
                var values = definition.ItemValues();
                Assert.That(values.Sum(item => item.wood), Is.EqualTo(TemplatePricing.EquipmentWood(definition)), $"{shipClass}: the items add up to the equipment share.");
                Assert.That(values.Sum(item => item.silver), Is.EqualTo(TemplatePricing.EquipmentSilver(definition)));
                Assert.That(definition.ProjectileArmor, Is.LessThanOrEqualTo(Rules.MaxHullReduction));
            }
            var bomber = ShipCatalog.Get(ShipClass.Bomber);
            Assert.That(bomber.Hull, Is.GreaterThan(ShipCatalog.Get(ShipClass.Boarder).Hull), "The heaviest hull.");
            Assert.That(bomber.Speed, Is.LessThan(ShipCatalog.Get(ShipClass.Boarder).Speed), "And the slowest.");
            Assert.That(bomber.Slots, Is.All.EqualTo(SlotSize.Heavy));
            Assert.That(ShipCatalog.Get(ShipClass.ShipKiller).Slots, Is.All.EqualTo(SlotSize.Medium));
        }

        [Test]
        public void Classes_TheBomberNeedsAWorkshopAndTheHeavyArmsResearch()
        {
            var world = Empty();
            var port = world.SettlementOf(0);
            Assert.That(world.ShipProblem(port, ShipClass.Bomber), Is.EqualTo("Bombowiec wymaga Warsztatu w tym porcie."));
            Order(world, ShipClass.Bomber);
            world.Step();
            Assert.That(port.WarQueue, Is.Empty, "No workshop, no bomber.");

            port.HasWorkshop = true;
            Assert.That(world.ShipProblem(port, ShipClass.Bomber), Does.StartWith("Bombowiec wymaga badania"));
            Order(world, ShipClass.Bomber);
            world.Step();
            Assert.That(port.WarQueue, Is.Empty, "No research, no bomber.");

            world.HeavyArmsResearched |= 1 << 0;
            Assert.That(world.ShipProblem(port, ShipClass.Bomber), Is.Null);
            Order(world, ShipClass.Bomber);
            world.Step();
            Assert.That(port.WarQueue.Select(order => order.Class), Is.EqualTo(new[] { ShipClass.Bomber }));
        }

        [Test]
        public void Classes_TheShipKillerNeedsOnlyAShipyard()
        {
            var world = Empty();
            var port = world.SettlementOf(0);
            Assert.That(world.ShipProblem(port, ShipClass.ShipKiller), Is.Null);
            Order(world, ShipClass.ShipKiller);
            Run(world, ShipCatalog.Get(ShipClass.ShipKiller).BuildSeconds + 1);
            var ship = world.Ships.Single(item => item.Class == ShipClass.ShipKiller);
            Assert.That(ship.Mounts, Is.EqualTo(new[] { WeaponType.DeckBallista, WeaponType.DeckBallista }), "Launched with its two bolt-throwers.");
            Assert.That(ship.Loadout, Is.EqualTo(ship.Mounts));

            port.HasShipyard = false;
            Assert.That(world.ShipProblem(port, ShipClass.ShipKiller), Is.EqualTo("Ten port nie ma stoczni."));
        }

        [Test]
        public void Production_ABareHullCostsTheTemplateLessItsWeaponsAndLaunchesEmpty()
        {
            var world = Empty();
            var port = world.SettlementOf(0);
            var definition = ShipCatalog.Get(ShipClass.ShipKiller);
            var values = definition.ItemValues();
            var ram = definition.Slots.Length;
            var (wood, silver) = World.OrderPrice(definition, true);
            Assert.That(wood, Is.EqualTo(definition.CostWood * Rules.Centi - values[0].wood - values[1].wood), "Cheaper by exactly the weapons' worth.");
            Assert.That(silver, Is.EqualTo(definition.CostSilver * Rules.Centi - values[0].silver - values[1].silver));
            Assert.That(World.OrderPrice(definition, false), Is.EqualTo((definition.CostWood * Rules.Centi, definition.CostSilver * Rules.Centi)));

            var startWood = port.Wood;
            var startSilver = port.Silver;
            var ledger = (world.LedgerTotal(ResourceType.Wood), world.LedgerTotal(ResourceType.Silver));
            Order(world, ShipClass.ShipKiller, unarmed: true);
            world.Step();
            Assert.That(port.WarQueue.Single().Unarmed, Is.True);
            Assert.That(port.ReservedWood + (startWood - port.Wood), Is.EqualTo(wood), "Reserved at the bare price.");

            // A save in the middle of the work keeps the order bare.
            Run(world, 5);
            var loaded = SaveGame.Deserialize(SaveGame.Serialize(world), out _);
            Assert.That(loaded.SettlementOf(0).WarQueue.Single().Unarmed, Is.True, "Saved with the order.");
            Run(world, definition.BuildSeconds);
            Run(loaded, definition.BuildSeconds);
            foreach (var copy in new[] { world, loaded })
            {
                var ship = copy.Ships.Single(item => item.Class == ShipClass.ShipKiller);
                Assert.That(ship.Mounts, Is.All.EqualTo(WeaponType.None), "Empty posts.");
                Assert.That(ship.Loadout, Is.All.EqualTo(WeaponType.None), "An empty weapon plan.");
                Assert.That(ship.ItemWood.Take(ram).Concat(ship.ItemSilver.Take(ram)), Is.All.EqualTo(0L), "No worth in the empty posts.");
                Assert.That((ship.ItemWood[ram], ship.ItemSilver[ram]), Is.EqualTo(values[ram]), "The ram stays with its worth.");
            }
            Assert.That((startWood - port.Wood, startSilver - port.Silver), Is.EqualTo((wood, silver)), "Paid the bare price, once.");
            Assert.That((world.LedgerTotal(ResourceType.Wood), world.LedgerTotal(ResourceType.Silver)), Is.EqualTo(ledger), "The ledger balances.");
        }

        [Test]
        public void Opponent_BuildsTheSchoolAndResearchAfterTheDockAndThenABomber()
        {
            var world = Empty();
            var home = world.SettlementOf(0);
            home.Wood = 6000 * Rules.Centi;
            home.Silver = 3000 * Rules.Centi;
            world.Tick = 8 * 60 * Rules.TickRate;
            var opponent = new Opponent(0);
            var bomber = false;
            for (var i = 0; i < 1200 * Rules.TickRate && !bomber; i++)
            {
                opponent.Think(world);
                world.Step();
                // Generous stores, so the chain is measured and not the income.
                home.Wood = System.Math.Max(home.Wood, 3000 * Rules.Centi);
                home.Silver = System.Math.Max(home.Silver, 2000 * Rules.Centi);
                bomber = home.WarQueue.Any(order => order.Class == ShipClass.Bomber) || world.Ships.Any(ship => ship.Class == ShipClass.Bomber);
            }
            Assert.That(home.HasWorkshop && home.IsTown && home.HasDock, Is.True, "Workshop, town and dock first.");
            Assert.That(home.HasSageSchool, Is.True, "Then the Sages' School.");
            Assert.That(world.HasHeavyArms(0), Is.True, "And the heavy-arms research.");
            Assert.That(bomber, Is.True, "After which the line builds bombers.");
        }
    }
}
