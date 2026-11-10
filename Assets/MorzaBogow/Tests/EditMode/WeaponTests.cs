using System;
using System.Linq;
using MorzaBogow.Simulation;
using NUnit.Framework;

namespace MorzaBogow.Tests
{
    /// <summary>Weapons in posts, shots in flight, modules, armour and the ram of every warship (docs/UZBROJENIE.md).</summary>
    public sealed class WeaponTests
    {
        static World Empty(ulong seed = 7)
        {
            var world = new World(seed);
            world.Settlements.Add(new Settlement { Id = world.NextId++, Owner = 0, X = -500, Y = 0, Wood = 2000 * Rules.Centi, Silver = 1500 * Rules.Centi });
            world.Settlements.Add(new Settlement { Id = world.NextId++, Owner = 1, X = 500, Y = 0, Wood = 2000 * Rules.Centi, Silver = 1500 * Rules.Centi });
            return world;
        }

        static void Run(World world, double seconds)
        {
            for (var i = 0; i < (int)(seconds * Rules.TickRate); i++) world.Step();
        }

        [Test]
        public void Weapons_APostShortOfItsCrewNeitherShootsNorLoads()
        {
            var world = Empty();
            var ship = world.SpawnShip(0, ShipClass.CrewDestroyer, 0, 0);
            ship.Archers = 3;
            ship.Progress[0] = 0.5;
            Assert.That(World.Manned(ship).Any(item => item), Is.False, "Three archers cannot man a bank of four.");
            Run(world, 3);
            Assert.That(ship.Progress[0], Is.EqualTo(0.5), "The cycle waits, it is not reset.");
            ship.Archers = 4;
            Assert.That(World.Manned(ship).Count(item => item), Is.EqualTo(1), "Four archers man one bank, the first.");
            Run(world, 0.5);
            Assert.That(ship.Progress[0], Is.GreaterThan(0.5));
        }

        [Test]
        public void Weapons_RangeDeadZoneAndArcDecideWhatAPostCanReach()
        {
            var world = Empty();
            var ship = world.SpawnShip(0, ShipClass.Ram, 0, 0);
            ship.Heading = 0;
            var catapult = WeaponCatalog.Get(WeaponType.HeavyCatapult);
            Assert.That(World.InReach(ship, catapult, 50, 0), Is.False, "Inside the dead zone.");
            Assert.That(World.InReach(ship, catapult, 150, 0), Is.True, "Ahead, in range.");
            Assert.That(World.InReach(ship, catapult, -150, 0), Is.False, "Astern, outside its 120° arc.");
            Assert.That(World.InReach(ship, catapult, 300, 0), Is.False, "Beyond its range.");
            var bank = WeaponCatalog.Get(WeaponType.ArcherBank);
            Assert.That(World.InReach(ship, bank, -60, 0), Is.True, "A bank of archers shoots all round.");
            Assert.That(WeaponCatalog.Fits(WeaponType.HeavyCatapult, SlotSize.Medium), Is.False, "A heavy machine never fits a medium post.");
        }

        [Test]
        public void Weapons_AShotFliesForItsTimeAndHurtsOnlyWhenItLands()
        {
            var world = Empty();
            var shooter = world.SpawnShip(0, ShipClass.CrewDestroyer, 0, 0);
            var target = world.SpawnShip(1, ShipClass.Boarder, 80, 0);
            target.Javelins = 0;
            target.Archers = 0;
            var hull = target.Hull;
            Projectile shot = null;
            for (var i = 0; i < 60 && shot == null; i++)
            {
                world.Step();
                shot = world.Projectiles.FirstOrDefault(item => item.ShooterId == shooter.Id);
            }
            Assert.That(shot, Is.Not.Null);
            var flight = WeaponCatalog.Get(shot.Weapon).FlightSeconds(World.Distance(shot.FromX, shot.FromY, shot.ToX, shot.ToY));
            Assert.That(shot.ArriveTick - shot.LaunchTick, Is.EqualTo((int)Math.Ceiling(flight * Rules.TickRate - 1e-9)));
            Assert.That(target.Hull, Is.EqualTo(hull), "Nothing until the shot lands.");
            while (world.Tick < shot.ArriveTick) world.Step();
            Assert.That(world.Projectiles.Any(item => item.Id == shot.Id), Is.False, "Landed once.");
        }

        [Test]
        public void Weapons_ScatterComesFromTheSeedAndRepeatsExactly()
        {
            double[] Landings(ulong seed)
            {
                var world = Empty(seed);
                world.SpawnShip(0, ShipClass.CrewDestroyer, 0, 0);
                world.SpawnShip(1, ShipClass.Boarder, 90, 0).Hull = 1e6;
                var points = new System.Collections.Generic.List<double>();
                for (var i = 0; i < 300; i++)
                {
                    world.Step();
                    points.AddRange(world.Projectiles.Where(item => item.LaunchTick == world.Tick).SelectMany(item => new[] { item.ToX, item.ToY }));
                }
                return points.ToArray();
            }
            var first = Landings(5);
            Assert.That(first.Length, Is.GreaterThan(4));
            Assert.That(Landings(5), Is.EqualTo(first), "Same seed, same landing points.");
            Assert.That(Landings(6), Is.Not.EqualTo(first));
        }

        [Test]
        public void Weapons_LandStopsBoltsButAStoneFliesOverIt()
        {
            int Hits(WeaponType weapon)
            {
                var world = Empty();
                world.Islands.Add(new Island { X = 70, Y = 0, Radius = 12, Kind = IslandKind.Coast });
                var shooter = world.SpawnShip(0, ShipClass.Ram, 0, 0);
                shooter.Heading = 0;
                shooter.Mounts[0] = WeaponType.None;
                shooter.Mounts[1] = weapon;
                var target = world.SpawnShip(1, ShipClass.Boarder, 150, 0);
                target.Heading = Math.PI / 2;
                target.Hull = 1e6;
                target.Javelins = 0;
                var hits = 0;
                for (var i = 0; i < 40 * Rules.TickRate; i++)
                {
                    world.Step();
                    hits += world.Events.Count(item => item.Type == "projectile_hit" && item.SubjectId == target.Id);
                    world.Events.Clear();
                }
                return hits;
            }
            Assert.That(Hits(WeaponType.DeckBallista), Is.Zero, "A bolt does not pass through the island.");
            Assert.That(Hits(WeaponType.StoneCatapult), Is.GreaterThan(0), "A lobbed stone does.");
        }

        [Test]
        public void Weapons_ASplashHitsEveryHullUnderItOnceAndArmourTakesItsShare()
        {
            var world = Empty();
            var main = world.SpawnShip(1, ShipClass.Boarder, 200, 0);
            // Its side lies 6 m from the impact, inside the catapult's 8 m splash.
            var beside = world.SpawnShip(1, ShipClass.Ram, 200, 6 + ShipCatalog.Get(ShipClass.Ram).Beam / 2);
            main.Heading = beside.Heading = 0;
            var friend = world.SpawnShip(0, ShipClass.Ram, 200, -12);
            friend.Heading = 0;
            var before = (main.Hull, beside.Hull, friend.Hull);
            world.Projectiles.Add(new Projectile { Id = world.NextProjectileId++, OwnerId = 0, ShooterId = -1, Weapon = WeaponType.HeavyCatapult, FromX = 0, FromY = 0, ToX = 200, ToY = 0, LaunchTick = 0, ArriveTick = world.Tick + 1 });
            world.Step();
            var packet = WeaponCatalog.Get(WeaponType.HeavyCatapult).Hull;
            Assert.That(before.Item1 - main.Hull, Is.EqualTo(packet * (1 - main.Definition.ProjectileArmor)).Within(1e-6), "The main target: one packet, less its armour.");
            Assert.That(before.Item2 - beside.Hull, Is.EqualTo(packet * (1 - beside.Definition.ProjectileArmor)).Within(1e-6), "A hull within the splash: one packet.");
            Assert.That(friend.Hull, Is.EqualTo(before.Item3), "No friendly fire.");
            Assert.That(world.Events.Count(item => item.Type == "projectile_hit"), Is.EqualTo(2));
            foreach (var shipClass in new[] { ShipClass.Cutter, ShipClass.Ram, ShipClass.CrewDestroyer, ShipClass.Boarder, ShipClass.Merchant })
                Assert.That(ShipCatalog.Get(shipClass).ProjectileArmor, Is.LessThanOrEqualTo(Rules.MaxHullReduction));
        }

        [Test]
        public void Weapons_ABrokenModuleIsSilentAndOnlyAPaidRepairMendsIt()
        {
            var world = Empty();
            var port = world.SettlementOf(0);
            port.HasDock = true;
            var ship = world.SpawnShip(0, ShipClass.CrewDestroyer, port.X + 30, 0);
            ship.ModuleHp[0] = 0;
            ship.ModuleHp[1] = 40;
            ship.Hull = ship.Definition.Hull - 120;
            var enemy = world.SpawnShip(1, ShipClass.Boarder, port.X + 30, 70);
            enemy.Javelins = 0;
            enemy.Archers = 0;
            enemy.Hull = 1e6;
            var (wood, silver) = World.ModuleRepairCost(ship);
            Assert.That(wood, Is.Zero, "Modules are mended for silver only.");
            Assert.That(silver, Is.GreaterThan(0));
            var silentFired = false;
            var sawHullRepairedFirst = false;
            for (var i = 0; i < 60 * Rules.TickRate && ship.ModuleHp.Any(item => item < Rules.ModuleHitPoints); i++)
            {
                world.Step();
                silentFired |= ship.ModuleHp[0] <= 0 && world.Events.Any(item => item.Type == "weapon_fired" && item.SubjectId == ship.Id && (int)item.Amount % 100 == 0);
                if (ship.Hull >= ship.Definition.Hull && ship.ModuleHp[0] <= 0) sawHullRepairedFirst = true;
                world.Events.Clear();
            }
            Assert.That(silentFired, Is.False, "A post at zero does not shoot.");
            Assert.That(sawHullRepairedFirst, Is.True, "Mending the hull does not mend the module.");
            Assert.That(ship.ModuleHp, Is.All.EqualTo(Rules.ModuleHitPoints), "The port mends the modules after the hull.");
        }

        [Test]
        public void Weapons_FittedItemsAndTheRamShareTheEquipmentPriceExactly()
        {
            foreach (ShipClass shipClass in System.Enum.GetValues(typeof(ShipClass)))
            {
                var definition = ShipCatalog.Get(shipClass);
                var values = definition.ItemValues();
                if (values.All(item => item.wood == 0 && item.silver == 0)) continue;
                Assert.That(values.Sum(item => item.wood), Is.EqualTo(TemplatePricing.EquipmentWood(definition)), $"{shipClass} wood.");
                Assert.That(values.Sum(item => item.silver), Is.EqualTo(TemplatePricing.EquipmentSilver(definition)), $"{shipClass} silver.");
            }
        }

        [Test]
        public void Ram_EveryWarshipCarriesARamAndTheChargeIsCalledBeforeItStrikes()
        {
            var world = Empty();
            var destroyer = world.SpawnShip(0, ShipClass.CrewDestroyer, -80, 0);
            destroyer.Heading = 0;
            destroyer.Archers = 0;
            var cutter = world.SpawnShip(0, ShipClass.Cutter, -80, 40);
            var target = world.SpawnShip(1, ShipClass.Boarder, 0, 0);
            target.Heading = Math.PI / 2;
            target.Javelins = 0;
            target.Archers = 0;
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Ram, ShipIds = new[] { destroyer.Id, cutter.Id }, TargetId = target.Id });
            world.Step();
            Assert.That(destroyer.Order, Is.EqualTo(OrderKind.Ram), "A crew destroyer has a ram too.");
            Assert.That(cutter.Order, Is.Not.EqualTo(OrderKind.Ram), "A cutter does not.");

            int called = -1, struck = -1;
            for (var i = 0; i < 40 * Rules.TickRate && struck < 0; i++)
            {
                world.Step();
                if (called < 0 && world.Events.Any(item => item.Type == "ram_telegraph" && item.SubjectId == destroyer.Id)) called = world.Tick;
                if (world.Events.Any(item => item.Type == "ram_hit" && item.SubjectId == destroyer.Id)) struck = world.Tick;
                world.Events.Clear();
            }
            Assert.That(called, Is.GreaterThan(0));
            Assert.That(struck, Is.GreaterThan(0));
            Assert.That(struck - called, Is.GreaterThanOrEqualTo((int)Math.Ceiling(Rules.RamTelegraphSeconds * Rules.TickRate)), "At least 1.2 s of warning.");

            // The next charge needs the cooldown and a hull's length of sea room.
            var next = -1;
            for (var i = 0; i < 30 * Rules.TickRate && next < 0; i++)
            {
                world.Step();
                if (world.Events.Any(item => item.Type == "ram_hit" && item.SubjectId == destroyer.Id)) next = world.Tick;
                world.Events.Clear();
            }
            if (next > 0) Assert.That(next - struck, Is.GreaterThanOrEqualTo((int)(Rules.RamCooldownSeconds * Rules.TickRate)));

            Assert.That(ShipCatalog.Get(ShipClass.Ram).RamMultiplier, Is.GreaterThan(ShipCatalog.Get(ShipClass.Boarder).RamMultiplier));
            Assert.That(ShipCatalog.Get(ShipClass.Boarder).RamMultiplier, Is.GreaterThan(ShipCatalog.Get(ShipClass.CrewDestroyer).RamMultiplier));
        }

        [Test]
        public void Ram_AnUncalledContactDoesNoDamage()
        {
            var world = Empty();
            var ram = world.SpawnShip(0, ShipClass.Ram, 0, 0);
            var target = world.SpawnShip(1, ShipClass.CrewDestroyer, 22, 0);
            ram.Heading = 0;
            ram.Speed = 8;
            target.Heading = Math.PI / 2;
            target.Archers = 0;
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Ram, ShipIds = new[] { ram.Id }, TargetId = target.Id });
            var hull = target.Hull;
            world.Step();
            Assert.That(world.Events.Any(item => item.Type == "ram_failed"), Is.True, "Contact before the charge was called.");
            Assert.That(target.Hull, Is.EqualTo(hull));
        }

        [Test]
        public void Javelins_ThrowOnlyAtCloseRangeAndFightInTheFront()
        {
            int Throws(double distance)
            {
                var world = Empty();
                var boarder = world.SpawnShip(0, ShipClass.Boarder, 0, 0);
                var target = world.SpawnShip(1, ShipClass.Merchant, distance, 0);
                target.Hull = 1e6;
                var fired = 0;
                for (var i = 0; i < 10 * Rules.TickRate; i++)
                {
                    world.Step();
                    fired += world.Events.Count(item => item.Type == "weapon_fired" && item.SubjectId == boarder.Id);
                    world.Events.Clear();
                }
                return fired;
            }
            Assert.That(Throws(70), Is.Zero, "Beyond 45 m.");
            Assert.That(Throws(35), Is.GreaterThan(0));

            var ship = Empty().SpawnShip(0, ShipClass.Boarder, 0, 0);
            ship.Swordsmen = 2;
            var deck = World.DeckStrength(ship, 10, true);
            Assert.That((deck.swordsmen, deck.javelins), Is.EqualTo((2, Math.Min(ship.Javelins, 8))), "Swordsmen first, then javelin men.");
        }

        [Test]
        public void Weapons_SaveWithShotsInTheAirContinuesIdentically()
        {
            var world = Empty();
            world.SpawnShip(0, ShipClass.CrewDestroyer, 0, 0);
            world.SpawnShip(1, ShipClass.CrewDestroyer, 80, 0);
            while (world.Projectiles.Count == 0) world.Step();
            var loaded = SaveGame.Deserialize(SaveGame.Serialize(world), out _);
            Assert.That(loaded.Projectiles.Count, Is.EqualTo(world.Projectiles.Count));
            for (var i = 0; i < 20 * Rules.TickRate; i++) { world.Step(); loaded.Step(); }
            foreach (var ship in world.Ships)
            {
                var copy = loaded.FindShip(ship.Id);
                Assert.That((copy.Hull, copy.Crew, copy.Wounded), Is.EqualTo((ship.Hull, ship.Crew, ship.Wounded)));
                Assert.That(copy.Progress, Is.EqualTo(ship.Progress));
                Assert.That(copy.ModuleHp, Is.EqualTo(ship.ModuleHp));
            }
        }
    }
}
