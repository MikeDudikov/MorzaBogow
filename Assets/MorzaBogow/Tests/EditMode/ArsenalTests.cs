using System.Linq;
using MorzaBogow.Simulation;
using NUnit.Framework;

namespace MorzaBogow.Tests
{
    /// <summary>Port buildings, research, the workshop and arsenal, refitting by plan and fleet blueprints (docs/UZBROJENIE.md).</summary>
    public sealed class ArsenalTests
    {
        static World Empty(ulong seed = 7)
        {
            var world = new World(seed);
            world.Settlements.Add(new Settlement { Id = world.NextId++, Owner = 0, X = -500, Y = 0, Wood = 3000 * Rules.Centi, Silver = 2500 * Rules.Centi });
            world.Settlements.Add(new Settlement { Id = world.NextId++, Owner = 1, X = 500, Y = 0, Wood = 2000 * Rules.Centi, Silver = 1500 * Rules.Centi });
            return world;
        }

        static void Run(World world, double seconds)
        {
            for (var i = 0; i < (int)(seconds * Rules.TickRate); i++) world.Step();
        }

        static void Project(World world, Settlement port, ProjectKind kind) =>
            world.Issue(new Command { Faction = port.Owner, Kind = CommandKind.BuildProject, Project = kind, TargetId = port.Id });

        [Test]
        public void Projects_FollowTheChainAndArePaidOnceFromThePort()
        {
            var world = Empty();
            var port = world.SettlementOf(0);
            Assert.That(world.ProjectProblem(port, ProjectKind.Town), Is.Not.Null, "No town without a workshop.");
            Assert.That(world.ProjectProblem(port, ProjectKind.SageSchool), Is.Not.Null, "No school without a town.");

            var wood = port.Wood;
            var silver = port.Silver;
            Project(world, port, ProjectKind.Workshop);
            world.Step();
            Assert.That(port.ReservedWood + (wood - port.Wood), Is.EqualTo(Rules.WorkshopWood), "The whole price is reserved, the work uses it as it goes.");
            Assert.That(world.ProjectProblem(port, ProjectKind.Town), Is.EqualTo("Port już coś buduje."), "One project at a time.");
            Run(world, Rules.WorkshopSeconds);
            Assert.That(port.HasWorkshop, Is.True);
            Assert.That((wood - port.Wood, silver - port.Silver), Is.EqualTo((Rules.WorkshopWood, Rules.WorkshopSilver)), "Paid once.");
            Assert.That(port.ReservedWood + port.ReservedSilver, Is.Zero);

            var walls = port.MaxHull;
            Project(world, port, ProjectKind.Town);
            Run(world, Rules.TownSeconds + 1);
            Assert.That(port.IsTown, Is.True);
            Assert.That(port.MaxHull, Is.EqualTo(walls * Rules.TownWalls).Within(1e-6), "A town's walls are stronger.");

            Project(world, port, ProjectKind.SageSchool);
            Run(world, Rules.SchoolSeconds + 1);
            Assert.That(port.HasSageSchool, Is.True);
            Assert.That(world.HasHeavyArms(0), Is.False);
            Project(world, port, ProjectKind.HeavyArmsResearch);
            Run(world, Rules.HeavyArmsSeconds + 1);
            Assert.That(world.HasHeavyArms(0), Is.True, "The research serves the whole side.");
            Assert.That(world.HasHeavyArms(1), Is.False);
        }

        [Test]
        public void Projects_TheDockFollowsTheTownAndIsPaidOnce()
        {
            var world = Empty();
            var port = world.SettlementOf(0);
            Assert.That(world.ProjectProblem(port, ProjectKind.Dock), Is.EqualTo("Wymaga Miasta ze stocznią."), "No dock without a town.");
            port.HasWorkshop = true;
            port.IsTown = true;
            var wood = port.Wood;
            var silver = port.Silver;
            Project(world, port, ProjectKind.Dock);
            Run(world, Rules.DockSeconds + 1);
            Assert.That(port.HasDock, Is.True);
            Assert.That((wood - port.Wood, silver - port.Silver), Is.EqualTo((Rules.DockWood, Rules.DockSilver)), "Paid once.");
            Assert.That(port.ReservedWood + port.ReservedSilver, Is.Zero);
            Assert.That(world.HasDock(0), Is.True);
            Assert.That(world.HasDock(1), Is.False, "Each side builds its own.");
            Assert.That(world.ProjectProblem(port, ProjectKind.Dock), Is.EqualTo("Dok już stoi."));

            var loaded = SaveGame.Deserialize(SaveGame.Serialize(world), out _);
            Assert.That(loaded.SettlementOf(0).HasDock, Is.True, "The dock is saved.");
            Assert.That(loaded.HasDock(1), Is.False);
        }

        [Test]
        public void Projects_CancellingSettlesLikeAShipOrder()
        {
            var world = Empty();
            var port = world.SettlementOf(0);
            Project(world, port, ProjectKind.Workshop);
            Run(world, 10);
            var quote = World.QuoteOrder(port.Project);
            var ledger = (world.LedgerTotal(ResourceType.Wood), world.LedgerTotal(ResourceType.Silver));
            var wood = port.Wood;
            world.Issue(new Command { Faction = 0, Kind = CommandKind.CancelProject, TargetId = port.Id });
            world.Step();
            Assert.That(port.Project, Is.Null);
            Assert.That(quote.Started, Is.True);
            Assert.That(wood - port.Wood, Is.EqualTo(quote.LostWood), "After the start 20% of the unused part is lost.");
            Assert.That((world.LedgerTotal(ResourceType.Wood), world.LedgerTotal(ResourceType.Silver)), Is.EqualTo(ledger), "The ledger balances.");
        }

        [Test]
        public void Workshop_MakesWeaponsIntoTheArsenalAndHeavyOnesWaitForResearch()
        {
            var world = Empty();
            var port = world.SettlementOf(0);
            Assert.That(world.WeaponProblem(port, WeaponType.Scorpion), Is.Not.Null, "No workshop yet.");
            port.HasWorkshop = true;
            world.Issue(new Command { Faction = 0, Kind = CommandKind.QueueWeapon, Weapon = WeaponType.HeavyCatapult, TargetId = port.Id });
            world.Issue(new Command { Faction = 0, Kind = CommandKind.QueueWeapon, Weapon = WeaponType.ArcherBank, TargetId = port.Id });
            world.Issue(new Command { Faction = 0, Kind = CommandKind.QueueWeapon, Weapon = WeaponType.Scorpion, TargetId = port.Id });
            world.Step();
            Assert.That(port.WorkshopQueue.Select(order => order.Weapon), Is.EqualTo(new[] { WeaponType.Scorpion }), "Heavy needs research; a bank is men, not a machine.");
            Run(world, WeaponCatalog.Get(WeaponType.Scorpion).WorkshopSeconds);
            Assert.That(port.Arsenal[(int)WeaponType.Scorpion], Is.EqualTo(1));

            world.HeavyArmsResearched |= 1;
            Assert.That(world.WeaponProblem(port, WeaponType.HeavyCatapult), Is.Null);
        }

        [Test]
        public void Refit_FollowsThePlanFromTheArsenalAndOrdersWhatIsMissing()
        {
            var world = Empty();
            var port = world.SettlementOf(0);
            port.HasWorkshop = true;
            port.Arsenal[(int)WeaponType.StoneCatapult] = 1;
            var ram = world.SpawnShip(0, ShipClass.Ram, port.X + 30, 0);
            world.Issue(new Command { Faction = 0, Kind = CommandKind.SetLoadout, ShipIds = new[] { ram.Id }, Loadout = new[] { WeaponType.ArcherBank, WeaponType.StoneCatapult } });
            Run(world, 20);
            Assert.That(ram.Mounts[1], Is.EqualTo(WeaponType.StoneCatapult), "Fitted from the arsenal when the ship regrouped.");
            Assert.That(port.Arsenal[(int)WeaponType.StoneCatapult], Is.Zero);
            Assert.That(ram.ItemWood[1], Is.EqualTo(WeaponCatalog.Get(WeaponType.StoneCatapult).CostWood * Rules.Centi), "Valued at its workshop price.");

            world.Issue(new Command { Faction = 0, Kind = CommandKind.SetLoadout, ShipIds = new[] { ram.Id }, Loadout = new[] { WeaponType.ArcherBank, WeaponType.DeckBallista } });
            world.Step();
            Assert.That(port.WorkshopQueue.Any(order => order.Weapon == WeaponType.DeckBallista), Is.True, "The missing weapon is ordered at once.");
            Assert.That(ram.Mounts[1], Is.EqualTo(WeaponType.StoneCatapult), "Nothing comes off before the new weapon is there.");
            Run(world, WeaponCatalog.Get(WeaponType.DeckBallista).WorkshopSeconds + 20);
            Assert.That(ram.Mounts[1], Is.EqualTo(WeaponType.DeckBallista));
            Assert.That(port.Arsenal[(int)WeaponType.StoneCatapult], Is.EqualTo(1), "The old weapon went back to the arsenal.");

            world.Issue(new Command { Faction = 0, Kind = CommandKind.SetLoadout, ShipIds = new[] { ram.Id }, Loadout = new[] { WeaponType.ArcherBank, WeaponType.HeavyCatapult } });
            world.Step();
            Assert.That(ram.Loadout[1], Is.EqualTo(WeaponType.DeckBallista), "A heavy machine never fits a medium post.");
        }

        [Test]
        public void Capture_KeepsTheWeaponsWithTheirState()
        {
            var world = Empty();
            var boarder = world.SpawnShip(0, ShipClass.Boarder, 0, 200);
            var ram = world.SpawnShip(1, ShipClass.Ram, boarder.Definition.Radius + ShipCatalog.Get(ShipClass.Ram).Radius + 2, 200);
            boarder.Heading = ram.Heading = 0;
            ram.Mounts[1] = WeaponType.StoneCatapult;
            ram.ModuleHp[1] = 60;
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Board, ShipIds = new[] { boarder.Id }, TargetId = ram.Id });
            for (var i = 0; i < 60 * Rules.TickRate && ram.Owner == 1; i++) world.Step();
            Assert.That(ram.Owner, Is.EqualTo(0));
            Assert.That(ram.Mounts, Is.EqualTo(new[] { WeaponType.ArcherBank, WeaponType.StoneCatapult }), "Taken with its weapons.");
            Assert.That(ram.ModuleHp[1], Is.InRange(0.0, 60.0), "With their state: the damaged module stays damaged.");
            Assert.That(ram.Loadout, Is.EqualTo(ram.Mounts), "The new owner plans to keep them.");
        }

        [Test]
        public void Blueprint_OneOrderBuildsTheShipWithItsPlansAndItsWeapons()
        {
            var world = Empty();
            var port = world.SettlementOf(0);
            port.HasWorkshop = true;
            world.Issue(new Command { Faction = 0, Kind = CommandKind.SaveBlueprint, Name = "Szturm", Class = ShipClass.Ram, Loadout = new[] { WeaponType.ArcherBank, WeaponType.Scorpion }, Preset = CrewPreset.Boarding });
            world.Step();
            var blueprint = world.Blueprints.Single();
            world.Issue(new Command { Faction = 0, Kind = CommandKind.QueueShip, BlueprintId = blueprint.Id, TargetId = port.Id });
            world.Step();
            Assert.That(port.WarQueue.Single().BlueprintId, Is.EqualTo(blueprint.Id));
            Assert.That(port.WorkshopQueue.Select(order => order.Weapon), Is.EqualTo(new[] { WeaponType.Scorpion }), "The weapon is ordered with the hull.");

            Run(world, ShipCatalog.Get(ShipClass.Ram).BuildSeconds + 1);
            var ram = world.Ships.Single(ship => ship.Owner == 0 && ship.Class == ShipClass.Ram);
            Assert.That(ram.Plan.Preset, Is.EqualTo(CrewPreset.Boarding));
            Assert.That(ram.Loadout, Is.EqualTo(blueprint.Loadout));
            Run(world, 60);
            Assert.That(ram.Mounts, Is.EqualTo(blueprint.Loadout), "Refitted after launch.");
            Assert.That((ram.Swordsmen, ram.Javelins, ram.Archers), Is.EqualTo((ram.Plan.Swordsmen, ram.Plan.Javelins, ram.Plan.Archers)), "And its crew trained.");
        }

        [Test]
        public void Save_DuringProjectsWorkshopAndBlueprintsContinuesIdentically()
        {
            var world = Empty();
            var port = world.SettlementOf(0);
            Project(world, port, ProjectKind.Workshop);
            Run(world, Rules.WorkshopSeconds + 1);
            Project(world, port, ProjectKind.Town);
            world.Issue(new Command { Faction = 0, Kind = CommandKind.QueueWeapon, Weapon = WeaponType.Gastraphetes, TargetId = port.Id });
            world.Issue(new Command { Faction = 0, Kind = CommandKind.SaveBlueprint, Name = "Łowca", Class = ShipClass.CrewDestroyer, Loadout = ShipCatalog.Get(ShipClass.CrewDestroyer).DefaultLoadout, Preset = CrewPreset.Volley });
            Run(world, 5);
            var loaded = SaveGame.Deserialize(SaveGame.Serialize(world), out _);
            var copy = loaded.SettlementOf(0);
            Assert.That((copy.HasWorkshop, copy.ProjectKind, copy.WorkshopQueue.Count, loaded.Blueprints.Count), Is.EqualTo((true, ProjectKind.Town, 1, 1)));
            Run(world, 70);
            Run(loaded, 70);
            Assert.That((copy.IsTown, copy.Arsenal[(int)WeaponType.Gastraphetes], copy.Wood, copy.Silver), Is.EqualTo((port.IsTown, port.Arsenal[(int)WeaponType.Gastraphetes], port.Wood, port.Silver)));
            Assert.That(loaded.Blueprints[0].Name, Is.EqualTo("Łowca"));
        }
    }
}
