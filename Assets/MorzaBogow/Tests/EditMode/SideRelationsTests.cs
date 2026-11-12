using System;
using System.Linq;
using MorzaBogow.Simulation;
using NUnit.Framework;

namespace MorzaBogow.Tests
{
    /// <summary>
    /// P02: the three owner slots and the one resolver of who is whose enemy, who commands what, who may take a ship and
    /// who the match is decided between (World.Sides.cs).
    /// </summary>
    public sealed class SideRelationsTests
    {
        static readonly int[] Slots = { -1, 0, 1, 2, 3 };

        static World Empty()
        {
            var world = new World(7);
            world.Settlements.Add(new Settlement { Id = world.NextId++, Owner = 0, X = -500, Y = 0, Wood = 2000 * Rules.Centi, Silver = 1500 * Rules.Centi });
            world.Settlements.Add(new Settlement { Id = world.NextId++, Owner = 1, X = 500, Y = 0, Wood = 2000 * Rules.Centi, Silver = 1500 * Rules.Centi });
            return world;
        }

        static CommandReason Answer(World world, int faction, CommandKind kind, Ship ship, int targetId)
        {
            world.Issue(new Command { Faction = faction, Kind = kind, ShipIds = new[] { ship.Id }, TargetId = targetId });
            world.Step();
            return world.CommandResults.Single(item => item.ShipId == ship.Id && item.Kind == kind).Reason;
        }

        // ---------- The slots ----------

        [Test]
        public void Owners_ThreeSlotsOfWhichTwoAreMainSides()
        {
            Assert.That((Owners.Human, Owners.MainAi, Owners.Independent, Owners.SlotCount), Is.EqualTo((0, 1, 2, 3)));
            Assert.That(Slots.Select(Owners.IsValid), Is.EqualTo(new[] { false, true, true, true, false }));
            Assert.That(Slots.Select(Owners.IsMainParticipant), Is.EqualTo(new[] { false, true, true, false, false }));
            var world = Empty();
            Assert.That(Slots.Select(world.IsMainParticipant), Is.EqualTo(new[] { false, true, true, false, false }));
        }

        [Test]
        public void Owners_OnlyAMainSideHasAMainOpponent()
        {
            Assert.That(Owners.MainOpponentOf(Owners.Human), Is.EqualTo(Owners.MainAi));
            Assert.That(Owners.MainOpponentOf(Owners.MainAi), Is.EqualTo(Owners.Human));
            foreach (var other in new[] { -1, 2, 3 })
                Assert.Throws<ArgumentOutOfRangeException>(() => Owners.MainOpponentOf(other), $"Slot {other} has no single opponent.");
        }

        [Test]
        public void Opponent_PlaysOnlyAMainSide()
        {
            Assert.DoesNotThrow(() => new Opponent(Owners.Human));
            Assert.DoesNotThrow(() => new Opponent(Owners.MainAi));
            foreach (var other in new[] { -1, 2, 3 })
                Assert.Throws<ArgumentOutOfRangeException>(() => new Opponent(other), $"No opponent for slot {other}.");
        }

        // ---------- The relations ----------

        [Test]
        public void Relations_ByDefaultEveryPairIsAtWarAndNoSlotWithItself()
        {
            var world = Empty();
            Assert.That(world.Relations, Is.EqualTo(SideRelations.AllHostile));
            foreach (var a in Slots)
            {
                foreach (var b in Slots)
                {
                    var expected = Owners.IsValid(a) && Owners.IsValid(b) && a != b;
                    Assert.That(world.IsHostile(a, b), Is.EqualTo(expected), $"{a} against {b}");
                    Assert.That(SideRelations.AllHostile.IsHostile(a, b), Is.EqualTo(expected), $"{a} against {b}");
                    Assert.That(SideRelations.NoneHostile.IsHostile(a, b), Is.False, $"{a} against {b}, nobody at war");
                }
            }
        }

        [Test]
        public void Relations_EachPairIsSetApartSymmetricAndNeverOnTheDiagonal()
        {
            var pairs = new[] { (0, 1), (0, 2), (1, 2) };
            foreach (var (peaceA, peaceB) in pairs)
            {
                var relations = SideRelations.AllHostile.With(peaceB, peaceA, false);
                foreach (var a in Slots)
                {
                    foreach (var b in Slots)
                    {
                        var atPeace = (a == peaceA && b == peaceB) || (a == peaceB && b == peaceA);
                        var expected = Owners.IsValid(a) && Owners.IsValid(b) && a != b && !atPeace;
                        Assert.That(relations.IsHostile(a, b), Is.EqualTo(expected), $"Peace between {peaceA} and {peaceB}: {a} against {b}");
                        Assert.That(relations.IsHostile(a, b), Is.EqualTo(relations.IsHostile(b, a)), "Symmetric.");
                    }
                }
                Assert.That(relations.With(peaceA, peaceB, true), Is.EqualTo(SideRelations.AllHostile), "Set at war again.");
                Assert.That(SideRelations.NoneHostile.With(peaceA, peaceB, true).IsHostile(peaceB, peaceA), Is.True, "One pair at war alone.");
            }
            // A slot with itself, or outside the three, changes nothing, the independent actors among themselves included.
            foreach (var slot in Slots)
            {
                Assert.That(SideRelations.NoneHostile.With(slot, slot, true), Is.EqualTo(SideRelations.NoneHostile), $"{slot} with itself");
                Assert.That(SideRelations.AllHostile.With(slot, 3, false), Is.EqualTo(SideRelations.AllHostile), $"{slot} with a slot that is not");
                Assert.That(SideRelations.AllHostile.With(-1, slot, false), Is.EqualTo(SideRelations.AllHostile));
            }
            Assert.That(new SideRelations(0xFF), Is.EqualTo(SideRelations.AllHostile), "Only the three pairs' bits are kept.");
        }

        [Test]
        public void CanControl_OnlyOnesOwnAndOnlyARealSlot()
        {
            var world = Empty();
            foreach (var issuer in Slots)
                foreach (var owner in Slots)
                    Assert.That(world.CanControl(issuer, owner), Is.EqualTo(Owners.IsValid(issuer) && issuer == owner), $"{issuer} commanding what {owner} holds");
        }

        [Test]
        public void CanCapture_AnEnemysShipAndOnlyByAMainSide()
        {
            var world = Empty();
            var ships = new[] { world.SpawnShip(0, ShipClass.Ram, 0, 0), world.SpawnShip(1, ShipClass.Ram, 0, 60), world.SpawnShip(2, ShipClass.Ram, 0, 120) };
            foreach (var capturer in Slots)
            {
                foreach (var target in ships)
                {
                    var expected = (capturer == 0 || capturer == 1) && capturer != target.Owner;
                    Assert.That(world.CanCapture(capturer, target), Is.EqualTo(expected), $"{capturer} taking a ship of {target.Owner}");
                }
                Assert.That(world.CanCapture(capturer, null), Is.False);
            }
            world.Relations = SideRelations.AllHostile.With(0, 2, false);
            Assert.That(world.CanCapture(0, ships[2]), Is.False, "Not a ship of a side at peace.");
            Assert.That(world.CanCapture(1, ships[2]), Is.True, "The other main side is still at war with it.");
        }

        // ---------- The rules ask the resolver ----------

        [Test]
        public void Orders_AShipIsCommandedOnlyByItsOwnSlot()
        {
            var world = Empty();
            var third = world.SpawnShip(2, ShipClass.Ram, 0, 0);
            var mine = world.SpawnShip(0, ShipClass.Ram, 0, 80);
            Assert.That(Answer(world, 0, CommandKind.Stop, third, -1), Is.EqualTo(CommandReason.NoUnit), "Not the human's ship.");
            Assert.That(Answer(world, 1, CommandKind.Stop, third, -1), Is.EqualTo(CommandReason.NoUnit));
            Assert.That(Answer(world, 2, CommandKind.Stop, third, -1), Is.EqualTo(CommandReason.None), "The independent slot commands its own.");
            Assert.That(Answer(world, 3, CommandKind.Stop, mine, -1), Is.EqualTo(CommandReason.NoUnit), "A slot that is not commands nothing.");
            Assert.That(Answer(world, -1, CommandKind.Stop, mine, -1), Is.EqualTo(CommandReason.NoUnit));
            Assert.That(Answer(world, 0, CommandKind.Stop, mine, -1), Is.EqualTo(CommandReason.None));
        }

        [Test]
        public void Orders_AttackingASideAtPeaceIsRefused()
        {
            var world = Empty();
            world.Relations = SideRelations.AllHostile.With(0, 1, false);
            var boarder = world.SpawnShip(0, ShipClass.Boarder, 380, 0);
            var theirs = world.SpawnShip(1, ShipClass.Merchant, 380, 60);
            var third = world.SpawnShip(2, ShipClass.Merchant, 380, -60);
            Assert.That(world.CanSee(0, theirs) && world.CanSee(0, third), Is.True);
            foreach (var kind in new[] { CommandKind.AttackShip, CommandKind.Ram, CommandKind.Board })
            {
                Assert.That(Answer(world, 0, kind, boarder, theirs.Id), Is.EqualTo(CommandReason.InvalidTarget), $"{kind} against a side at peace");
                Assert.That(boarder.Order, Is.EqualTo(OrderKind.Idle), "A refusal changes nothing.");
            }
            Assert.That(Answer(world, 0, CommandKind.AttackSettlement, boarder, world.SettlementOf(1).Id), Is.EqualTo(CommandReason.InvalidTarget), "Nor its port.");
            Assert.That(Answer(world, 0, CommandKind.AttackShip, boarder, third.Id), Is.EqualTo(CommandReason.None), "The third side is still an enemy.");
            Assert.That((boarder.Order, boarder.TargetId), Is.EqualTo((OrderKind.AttackShip, third.Id)));

            world.Relations = SideRelations.AllHostile;
            Assert.That(Answer(world, 0, CommandKind.AttackShip, boarder, theirs.Id), Is.EqualTo(CommandReason.None), "At war again, the order is taken.");
        }

        [Test]
        public void Orders_TheIndependentSlotBoardsNobodyButCanBeBoarded()
        {
            var world = Empty();
            var third = world.SpawnShip(2, ShipClass.Boarder, 0, 0);
            var mine = world.SpawnShip(0, ShipClass.Boarder, 0, 60);
            Assert.That(Answer(world, 2, CommandKind.Board, third, mine.Id), Is.EqualTo(CommandReason.NotCapable), "Only a main side takes prizes.");
            Assert.That(Answer(world, 2, CommandKind.AttackShip, third, mine.Id), Is.EqualTo(CommandReason.None), "It may still shoot at its enemies.");
            Assert.That(Answer(world, 0, CommandKind.Board, mine, third.Id), Is.EqualTo(CommandReason.None), "And a main side may board it.");
        }

        [Test]
        public void Weapons_ShotsSpareASideAtPeace()
        {
            // Two crew destroyers in each other's reach, and a third side's hull beside them. At war they shoot of their
            // own accord; shots already in the air when peace comes land without harm, and no more are loosed.
            var world = Empty();
            var first = world.SpawnShip(0, ShipClass.CrewDestroyer, 0, 0);
            var second = world.SpawnShip(1, ShipClass.CrewDestroyer, 80, 0);
            first.Hull = second.Hull = 1e6;
            for (var i = 0; i < 30 * Rules.TickRate && world.Projectiles.Count < 2; i++) world.Step();
            Assert.That(world.Projectiles.Count, Is.GreaterThanOrEqualTo(2), "At war they shoot at each other.");
            world.Events.Clear();

            world.Relations = SideRelations.AllHostile.With(0, 1, false);
            var before = (first.Hull, first.Crew, first.Wounded, second.Hull, second.Crew, second.Wounded);
            var inTheAir = world.Projectiles.Count;
            for (var i = 0; i < 10 * Rules.TickRate; i++) world.Step();
            Assert.That(inTheAir, Is.GreaterThan(0));
            Assert.That(world.Projectiles, Is.Empty, "Every shot in the air has landed.");
            Assert.That(world.Events.Count(item => item.Type == "projectile_hit"), Is.Zero, "None of them hit a ship of the side now at peace.");
            Assert.That(world.Events.Count(item => item.Type == "weapon_fired"), Is.Zero, "And no more are loosed at it.");
            Assert.That((first.Hull, first.Crew, first.Wounded, second.Hull, second.Crew, second.Wounded), Is.EqualTo(before));

            // The third side is still an enemy of both: it is shot at and hit.
            world.Events.Clear();
            var third = world.SpawnShip(2, ShipClass.Merchant, 40, 50);
            third.Hull = 1e6;
            for (var i = 0; i < 20 * Rules.TickRate; i++) world.Step();
            Assert.That(world.Events.Count(item => item.Type == "projectile_hit" && item.SubjectId == third.Id), Is.GreaterThan(0), "A ship of a side at war is hit.");
            Assert.That(world.Events.Count(item => item.Type == "projectile_hit" && item.SubjectId != third.Id), Is.Zero, "Still nobody of the two at peace.");
        }

        [Test]
        public void Weapons_AShotAtAPortOfASideAtPeaceDoesNoHarm()
        {
            var world = Empty();
            var port = world.SettlementOf(1);
            var ship = world.SpawnShip(0, ShipClass.CrewDestroyer, port.X - 80, 0);
            world.Issue(new Command { Faction = 0, Kind = CommandKind.AttackSettlement, ShipIds = new[] { ship.Id }, TargetId = port.Id });
            for (var i = 0; i < 60 * Rules.TickRate && world.Projectiles.Count == 0; i++) world.Step();
            world.Relations = SideRelations.AllHostile.With(0, 1, false);
            var hull = port.Hull;
            var inTheAir = world.Projectiles.Count;
            world.Events.Clear();
            for (var i = 0; i < 10 * Rules.TickRate; i++) world.Step();
            Assert.That(inTheAir, Is.GreaterThan(0), "Shots loosed in war are in the air when peace comes.");
            Assert.That(world.Projectiles, Is.Empty, "They have landed.");
            Assert.That(world.Events.Count(item => item.Type == "projectile_hit_settlement"), Is.Zero);
            Assert.That(port.Hull, Is.EqualTo(hull), "The port of a side at peace is not harmed.");
            Assert.That(world.Events.Count(item => item.Type == "weapon_fired"), Is.Zero, "And the ship looses no more at it, though its order stands.");
        }

        [Test]
        public void Opponent_LeavesASideAtPeaceAlone()
        {
            // The opponent of side 0 sees ships of side 1 and its port. At war it sends its warship at them; at peace it
            // gives no order against them and does not sail for their home.
            string Orders(bool atWar)
            {
                var world = Empty();
                if (!atWar) world.Relations = SideRelations.AllHostile.With(0, 1, false);
                world.SettlementOf(0).Silver = 0;
                var warship = world.SpawnShip(0, ShipClass.CrewDestroyer, 300, 0);
                world.SpawnShip(1, ShipClass.Merchant, 380, 0);
                Assert.That(world.CanSee(0, world.SettlementOf(1).X, world.SettlementOf(1).Y), Is.True);
                var ai = new Opponent(0, 1);
                var given = new System.Collections.Generic.List<string>();
                for (var i = 0; i < 3 * Rules.TickRate; i++)
                {
                    ai.Think(world);
                    given.AddRange(world.Pending.Where(command => command.ShipIds.Contains(warship.Id)).Select(command => command.Kind.ToString()));
                    world.Step();
                    Assert.That(world.CommandResults.Count(item => item.Faction == 0 && item.Outcome == CommandOutcome.Rejected), Is.Zero, "No order of its own is refused.");
                }
                return string.Join(",", given.Distinct());
            }
            Assert.That(Orders(true), Is.EqualTo("AttackShip"), "At war the warship is sent at the enemy it sees.");
            Assert.That(Orders(false), Is.Empty, "At peace it gets no order against that side.");
        }

        [Test]
        public void Opponent_StillFightsTheThirdSideWhileAtPeaceWithTheOther()
        {
            var world = Empty();
            world.Relations = SideRelations.AllHostile.With(0, 1, false);
            world.SettlementOf(0).Silver = 0;
            var warship = world.SpawnShip(0, ShipClass.CrewDestroyer, 0, 0);
            world.SpawnShip(1, ShipClass.Merchant, 60, 0);
            var raider = world.SpawnShip(2, ShipClass.Merchant, 0, 90);
            new Opponent(0, 1).Think(world);
            world.Step();
            Assert.That((warship.Order, warship.TargetId), Is.EqualTo((OrderKind.AttackShip, raider.Id)), "The nearer ship is at peace; the enemy farther off is the target.");
        }

        [Test]
        public void Boarding_APrizeGoesToTheMainSideThatTookItWhateverSlotHeldIt()
        {
            var world = Empty();
            var prey = world.SpawnShip(2, ShipClass.Merchant, 0, 12);
            var boarder = world.SpawnShip(1, ShipClass.Boarder, 0, 0);
            boarder.Heading = prey.Heading = 0;
            // A hunter of the taking side, and one of the other main side: after the capture only the enemy still hunts it.
            var friend = world.SpawnShip(1, ShipClass.CrewDestroyer, -200, 0);
            var foe = world.SpawnShip(0, ShipClass.CrewDestroyer, -100, 100);
            prey.Hull = 1e6;
            world.Issue(new Command { Faction = 1, Kind = CommandKind.Board, ShipIds = new[] { boarder.Id }, TargetId = prey.Id });
            world.Issue(new Command { Faction = 1, Kind = CommandKind.AttackShip, ShipIds = new[] { friend.Id }, TargetId = prey.Id });
            world.Issue(new Command { Faction = 0, Kind = CommandKind.AttackShip, ShipIds = new[] { foe.Id }, TargetId = prey.Id });
            for (var i = 0; i < 30 * Rules.TickRate && prey.Owner != 1; i++) world.Step();
            Assert.That(prey.Owner, Is.EqualTo(1), "Taken by the main opponent.");
            Assert.That(world.Boardings, Is.Empty);
            Assert.That(friend.Order, Is.EqualTo(OrderKind.Idle), "Its own side lets the prize be.");
            Assert.That((foe.Order, foe.TargetId), Is.EqualTo((OrderKind.AttackShip, prey.Id)), "The other side is its enemy still.");
        }

        [Test]
        public void Match_IsDecidedBetweenTheTwoMainSidesOnly()
        {
            // The independent slot has ships and no home: that neither ends the match nor wins it.
            var world = Empty();
            world.SpawnShip(0, ShipClass.Cutter, -480, 0);
            world.SpawnShip(1, ShipClass.Cutter, 480, 0);
            world.SpawnShip(2, ShipClass.Ram, 0, 300);
            for (var i = 0; i < Rules.TickRate; i++) world.Step();
            Assert.That((world.Outcome, world.Winner), Is.EqualTo((OutcomeKind.InProgress, -1)), "A slot without a home is not a defeated side.");

            world.SettlementOf(1).Hull = 0;
            world.Step();
            Assert.That((world.Outcome, world.Winner), Is.EqualTo((OutcomeKind.Won, Owners.Human)), "The main opponent's home falls: the human wins, the third side's ships notwithstanding.");

            var other = Empty();
            other.SpawnShip(2, ShipClass.Ram, 0, 300);
            other.SettlementOf(0).Hull = 0;
            other.SettlementOf(1).Hull = 0;
            other.Step();
            Assert.That((other.Outcome, other.Winner), Is.EqualTo((OutcomeKind.Draw, -1)), "Both homes fall: a draw, and the third side wins nothing.");
        }
    }
}
