using System;
using System.IO;
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

        // ---------- Escort, transport and convoys ----------

        [Test]
        public void Escort_EngagesOnlyShipsOfASideAtWar()
        {
            // A merchant at rest with its escort on station, and a hull of side 1 inside the escort zone, seen, on the far
            // side of the merchant and beyond the escort's reach. At war the escort leaves its station to engage it and
            // shoots; at peace it keeps its station and looses nothing. A third side's hull is an enemy either way.
            (double moved, int shots, OrderKind order) Guard(bool atWar, int intruder)
            {
                var world = Empty();
                if (!atWar) world.Relations = SideRelations.AllHostile.With(0, 1, false);
                var merchant = world.SpawnShip(0, ShipClass.Merchant, 0, 0);
                var escort = world.SpawnShip(0, ShipClass.CrewDestroyer, -25, 0);
                var other = world.SpawnShip(intruder, ShipClass.Merchant, 95, 0);
                other.Hull = 1e6;
                // Each main side keeps a ship, far off, so the match goes on whoever the intruder belongs to.
                world.SpawnShip(1, ShipClass.Cutter, 480, 0);
                Assert.That(world.CanSee(0, other), Is.True, "The escort's side sees it.");
                Assert.That(Distance(other, merchant), Is.LessThanOrEqualTo(Rules.EscortZone), "Inside the escort zone.");
                Assert.That(World.EngageRange(escort), Is.LessThan(Distance(other, escort)), "Out of the escort's reach from its station.");
                Assert.That(Answer(world, 0, CommandKind.Escort, escort, merchant.Id), Is.EqualTo(CommandReason.None));
                var shots = 0;
                for (var i = 0; i < 25 * Rules.TickRate; i++)
                {
                    world.Step();
                    shots += world.Events.Count(item => item.Type == "weapon_fired" && item.SubjectId == escort.Id);
                    world.Events.Clear();
                }
                Assert.That(world.Outcome, Is.EqualTo(OutcomeKind.InProgress));
                return (Math.Sqrt((escort.X + 25) * (escort.X + 25) + escort.Y * escort.Y), shots, escort.Order);
            }

            var war = Guard(true, 1);
            Assert.That(war.order, Is.EqualTo(OrderKind.Escort));
            Assert.That(war.moved, Is.GreaterThan(20), "At war the escort leaves its station for the threat.");
            Assert.That(war.shots, Is.GreaterThan(0), "And shoots at it.");

            var peace = Guard(false, 1);
            Assert.That(peace.order, Is.EqualTo(OrderKind.Escort));
            Assert.That(peace.moved, Is.LessThan(1), "At peace it keeps its station by the transport.");
            Assert.That(peace.shots, Is.Zero, "And looses nothing at the side at peace.");

            var third = Guard(false, 2);
            Assert.That(third.moved, Is.GreaterThan(20), "The third side is at war with it still: the escort engages.");
            Assert.That(third.shots, Is.GreaterThan(0));
        }

        static double Distance(Ship a, Ship b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

        [Test]
        public void Escort_OnlyOnesOwnShipIsEscortedEvenAtPeace()
        {
            var world = Empty();
            world.Relations = SideRelations.NoneHostile;
            var escort = world.SpawnShip(0, ShipClass.CrewDestroyer, 0, 0);
            var theirs = world.SpawnShip(1, ShipClass.Merchant, 0, 40);
            var mine = world.SpawnShip(0, ShipClass.Merchant, 0, -40);
            Assert.That(Answer(world, 0, CommandKind.Escort, escort, theirs.Id), Is.EqualTo(CommandReason.InvalidTarget), "Not a ship its side commands.");
            Assert.That(Answer(world, 0, CommandKind.Escort, escort, mine.Id), Is.EqualTo(CommandReason.None));
            // The escorted ship passes to another slot: the escort's order ends, peace or not.
            mine.Owner = 1;
            world.Step();
            Assert.That(escort.Order, Is.EqualTo(OrderKind.Idle), "A ship no longer its side's is not escorted.");
        }

        [Test]
        public void Transport_UsesOnlyPortsItsSideCommandsEvenAtPeace()
        {
            // Side 1 is at peace with the human; its ports are full of goods and one of them lies nearer the human's
            // merchant than any port of its own. The merchant neither brings goods to side 1's ports nor takes them
            // from there, and no order touches them.
            var world = Empty();
            world.Relations = SideRelations.AllHostile.With(0, 1, false);
            var home = world.SettlementOf(0);
            var theirs = world.SettlementOf(1);
            var outpost = new Settlement { Id = world.NextId++, Owner = 0, X = -300, Y = 0, IsOutpost = true, HasShipyard = false, Hull = Rules.OutpostHull, Wood = 300 * Rules.Centi };
            world.Settlements.Add(outpost);
            var merchant = world.SpawnShip(0, ShipClass.Merchant, -400, 30);
            var theirOutpost = new Settlement { Id = world.NextId++, Owner = 1, X = -410, Y = 60, IsOutpost = true, HasShipyard = false, Hull = Rules.OutpostHull, Wood = 900 * Rules.Centi, Silver = 900 * Rules.Centi };
            world.Settlements.Add(theirOutpost);
            double Away(Settlement port) => Math.Sqrt((port.X - merchant.X) * (port.X - merchant.X) + (port.Y - merchant.Y) * (port.Y - merchant.Y));
            Assert.That(Away(theirOutpost), Is.LessThan(Math.Min(Away(outpost), Away(home))), "The port at peace is the nearest of all.");

            CommandReason Transfer(int source, int destination)
            {
                world.Issue(new Command { Faction = 0, Kind = CommandKind.Transfer, ShipIds = new[] { merchant.Id }, SourceId = source, TargetId = destination });
                world.Step();
                return world.CommandResults.Single(item => item.ShipId == merchant.Id && item.Kind == CommandKind.Transfer).Reason;
            }
            CommandReason Port(CommandKind kind, int portId)
            {
                world.Issue(new Command { Faction = 0, Kind = kind, TargetId = portId, Wood = 10 * Rules.Centi, Silver = 10 * Rules.Centi });
                world.Step();
                return world.CommandResults.Single(item => item.Kind == kind).Reason;
            }

            Assert.That(Transfer(home.Id, theirs.Id), Is.EqualTo(CommandReason.InvalidTarget), "Not to a port of another side.");
            Assert.That(Transfer(theirs.Id, home.Id), Is.EqualTo(CommandReason.InvalidTarget), "Nor from one.");
            Assert.That(Transfer(-1, theirs.Id), Is.EqualTo(CommandReason.InvalidTarget));
            Assert.That(merchant.Order, Is.EqualTo(OrderKind.Idle), "A refusal changes nothing.");
            foreach (var kind in new[] { CommandKind.SetPriorityPort, CommandKind.SetPortMinimum, CommandKind.BuildShipyard })
                Assert.That(Port(kind, theirs.Id), Is.EqualTo(CommandReason.InvalidTarget), $"{kind} at a port of a side at peace");
            Assert.That((theirs.ReservedWood, theirs.ReservedSilver, theirs.KeepWood, theirs.KeepSilver, theirs.ShipyardBuild == null), Is.EqualTo((0L, 0L, new Settlement().KeepWood, new Settlement().KeepSilver, true)), "Their port is untouched.");
            Assert.That(world.PriorityPort(0), Is.SameAs(home));

            // With no source named, the nearest port of its own side is taken: not the nearer, richer one of the side at peace.
            Assert.That(Transfer(-1, home.Id), Is.EqualTo(CommandReason.None));
            Assert.That(merchant.SourceId, Is.EqualTo(outpost.Id), "Its own outpost, though side 1's lies nearer.");
            Assert.That(outpost.ReservedWood, Is.EqualTo(merchant.WantWood).And.GreaterThan(0), "The booking is at its own port.");
            Assert.That((theirOutpost.ReservedWood, theirOutpost.ReservedSilver), Is.EqualTo((0L, 0L)), "Nothing is booked at the port at peace.");

            // The source passes to the side at peace while the merchant is on its way: the order ends, the booking is given back.
            outpost.Owner = 1;
            world.Step();
            Assert.That(merchant.Order, Is.EqualTo(OrderKind.Idle), "Permission lost with the port.");
            Assert.That((outpost.ReservedWood, outpost.ReservedSilver), Is.EqualTo((0L, 0L)));
        }

        [Test]
        public void Convoy_DrawsOnlyOnPortsOfItsOwnSideEvenAtPeace()
        {
            // The human's home lacks the price of a ship; the only surplus on the map lies in ports of side 1, at peace.
            // The convoy waits. Once an outpost of its own holds a surplus, it loads there.
            var world = Empty();
            world.Relations = SideRelations.AllHostile.With(0, 1, false);
            var home = world.SettlementOf(0);
            home.Wood = home.Silver = 0;
            var theirs = world.SettlementOf(1);
            var theirOutpost = new Settlement { Id = world.NextId++, Owner = 1, X = -350, Y = 0, IsOutpost = true, HasShipyard = false, Hull = Rules.OutpostHull, Wood = 900 * Rules.Centi, Silver = 900 * Rules.Centi };
            world.Settlements.Add(theirOutpost);
            var merchant = world.SpawnShip(0, ShipClass.Merchant, -430, 30);
            // A convoy of side 1 forming at its outpost for its own home: the human's merchant does not join it.
            var theirMerchant = world.SpawnShip(1, ShipClass.Merchant, -350, 40);
            theirs.Wood = theirs.Silver = 0;
            world.Issue(new Command { Faction = 1, Kind = CommandKind.Convoy, ShipIds = new[] { theirMerchant.Id } });
            Assert.That(Answer(world, 0, CommandKind.Convoy, merchant, -1), Is.EqualTo(CommandReason.None));
            var need = world.ConvoyNeed(home);
            Assert.That(need.wood + need.silver, Is.GreaterThan(0), "The home port lacks goods.");

            long theirBooking = 0;
            for (var i = 0; i < 20 * Rules.TickRate; i++)
            {
                world.Step();
                Assert.That(merchant.SourceId, Is.EqualTo(-1), "It takes no port of the side at peace for its source.");
                Assert.That((theirOutpost.ReservedWood, theirOutpost.ReservedSilver), Is.EqualTo((theirMerchant.WantWood, theirMerchant.WantSilver)), "Nothing is booked at the other side's port but by its own ship.");
                theirBooking = Math.Max(theirBooking, theirMerchant.WantWood + theirMerchant.WantSilver);
            }
            Assert.That(theirBooking, Is.GreaterThan(0), "Side 1's own ship did book its load there meanwhile.");
            Assert.That(theirMerchant.ConvoyGroup, Is.GreaterThanOrEqualTo(0), "Side 1's own convoy is under way.");
            Assert.That((merchant.Order, merchant.Transport, merchant.ConvoyGroup), Is.EqualTo((OrderKind.Convoy, TransportPhase.Standby, -1)), "No port of its own has a surplus: it waits, in no convoy.");
            Assert.That(merchant.HoldWood + merchant.HoldSilver + merchant.WantWood + merchant.WantSilver, Is.Zero);
            Assert.That(world.ConvoyNeed(home), Is.EqualTo(need), "Goods on their way to side 1 do not count toward the human's need.");

            var outpost = new Settlement { Id = world.NextId++, Owner = 0, X = -300, Y = -200, IsOutpost = true, HasShipyard = false, Hull = Rules.OutpostHull, Wood = 900 * Rules.Centi, Silver = 900 * Rules.Centi };
            world.Settlements.Add(outpost);
            for (var i = 0; i < 2 * Rules.TickRate && merchant.SourceId < 0; i++) world.Step();
            Assert.That(merchant.SourceId, Is.EqualTo(outpost.Id), "Its own outpost's surplus is taken.");
            Assert.That(merchant.ConvoyGroup, Is.EqualTo(merchant.Id), "In a convoy of its own, not side 1's.");
        }

        [Test]
        public void Convoy_TheIndependentSlotNamesItsPriorityPort()
        {
            // Every one of the three slots has its place in the list of priority ports (format 20). The third side
            // supplies its home port until it names another; the save carries the name, and its convoy sails there.
            var world = Empty();
            var home = new Settlement { Id = world.NextId++, Owner = 2, X = 0, Y = 400 };
            var depot = new Settlement { Id = world.NextId++, Owner = 2, X = 0, Y = 200, IsOutpost = true, HasShipyard = false, Hull = Rules.OutpostHull, Wood = 900 * Rules.Centi, Silver = 900 * Rules.Centi };
            var yard = new Settlement { Id = world.NextId++, Owner = 2, X = 200, Y = 300, IsOutpost = true, HasShipyard = true, Hull = Rules.OutpostHull };
            world.Settlements.Add(home);
            world.Settlements.Add(depot);
            world.Settlements.Add(yard);
            var merchant = world.SpawnShip(2, ShipClass.Merchant, 0, 300);
            Assert.That(world.PriorityPorts.Length, Is.EqualTo(Owners.SlotCount));
            Assert.That(world.PriorityPort(2), Is.SameAs(home), "Until it names one, its home port.");
            Assert.That(world.PriorityPort(3), Is.Null, "A slot that is not has no port.");
            Assert.That(world.PriorityPort(-1), Is.Null);

            CommandReason Name(int faction, int portId)
            {
                world.Issue(new Command { Faction = faction, Kind = CommandKind.SetPriorityPort, TargetId = portId });
                world.Step();
                return world.CommandResults.Single(item => item.Kind == CommandKind.SetPriorityPort).Reason;
            }
            Assert.That(Name(1, yard.Id), Is.EqualTo(CommandReason.InvalidTarget), "Not a port the main opponent commands.");
            Assert.That(Name(3, yard.Id), Is.EqualTo(CommandReason.InvalidOrder), "A slot that is not names nothing.");
            Assert.That(world.PriorityPort(2), Is.SameAs(home), "Refusals change nothing.");
            Assert.That(Name(2, yard.Id), Is.EqualTo(CommandReason.None), "The third side names its own port.");
            Assert.That(world.PriorityPort(2), Is.SameAs(yard));
            Assert.That((world.PriorityPort(0), world.PriorityPort(1)), Is.EqualTo((world.SettlementOf(0), world.SettlementOf(1))), "The main sides' ports are as they were.");

            var loaded = SaveGame.Deserialize(SaveGame.Serialize(world), out _);
            Assert.That(loaded.PriorityPort(2)?.Id, Is.EqualTo(yard.Id), "The save carries the third side's priority port.");

            Assert.That(Answer(world, 2, CommandKind.Convoy, merchant, -1), Is.EqualTo(CommandReason.None));
            for (var i = 0; i < 2 * Rules.TickRate && merchant.SourceId < 0; i++) world.Step();
            Assert.That((merchant.SourceId, merchant.DestinationId), Is.EqualTo((depot.Id, yard.Id)), "Its convoy brings the depot's surplus to the port it named.");
        }

        [Test]
        public void Orders_ABlueprintOfASlotThatIsNotIsRefusedAndTheSaveStillLoads()
        {
            // A blueprint is lasting state under its owner. An order to keep one for a slot that is not must leave nothing
            // behind, or every later save of the match would be refused on reading.
            var world = Empty();
            CommandReason Keep(int faction, string name)
            {
                world.Issue(new Command { Faction = faction, Kind = CommandKind.SaveBlueprint, Class = ShipClass.Ram, Name = name, Loadout = new[] { WeaponType.ArcherBank, WeaponType.None } });
                world.Step();
                return world.CommandResults.Single(item => item.Kind == CommandKind.SaveBlueprint).Reason;
            }
            foreach (var faction in new[] { -1, 3 })
            {
                Assert.That(Keep(faction, "Obcy"), Is.EqualTo(CommandReason.InvalidOrder), $"A blueprint for slot {faction}");
                Assert.That(world.Blueprints, Is.Empty, "Nothing is kept.");
                Assert.That(world.NextBlueprintId, Is.EqualTo(new World(7).NextBlueprintId), "No number is spent.");
            }
            Assert.That(SaveGame.Deserialize(SaveGame.Serialize(world), out _).Blueprints, Is.Empty, "The match still saves and loads.");

            // Each of the three slots keeps its own, apart from the others' of the same name.
            foreach (var faction in new[] { 0, 1, 2 }) Assert.That(Keep(faction, "Wzor"), Is.EqualTo(CommandReason.None), $"Slot {faction}");
            Assert.That(Keep(2, "Wzor"), Is.EqualTo(CommandReason.None), "Kept again under the same name: the same blueprint.");
            Assert.That(world.Blueprints.Select(item => item.Owner), Is.EqualTo(new[] { 0, 1, 2 }));
            var loaded = SaveGame.Deserialize(SaveGame.Serialize(world), out _);
            Assert.That(loaded.Blueprints.Select(item => (item.Owner, item.Name)), Is.EqualTo(world.Blueprints.Select(item => (item.Owner, item.Name))));
        }

        // ---------- Peace, as the review of A1 asked ----------

        [Test]
        public void Orders_AHunterOfAPrizeTakenInPeaceGoesIdle()
        {
            // The human is at peace with side 1 and at war with the third side. A human warship hunts a third-side
            // merchant; side 1 boards and takes it. The prize now belongs to a side at peace: the hunt ends, and no
            // more is loosed at it.
            var world = Empty();
            world.Relations = SideRelations.AllHostile.With(0, 1, false);
            var prey = world.SpawnShip(2, ShipClass.Merchant, 0, 12);
            var boarder = world.SpawnShip(1, ShipClass.Boarder, 0, 0);
            boarder.Heading = prey.Heading = 0;
            var hunter = world.SpawnShip(0, ShipClass.CrewDestroyer, -70, 40);
            prey.Hull = 1e6;
            world.Issue(new Command { Faction = 1, Kind = CommandKind.Board, ShipIds = new[] { boarder.Id }, TargetId = prey.Id });
            Assert.That(Answer(world, 0, CommandKind.AttackShip, hunter, prey.Id), Is.EqualTo(CommandReason.None), "At war with the third side, the hunt is taken.");
            var shotsBefore = 0;
            for (var i = 0; i < 40 * Rules.TickRate && prey.Owner != 1; i++)
            {
                world.Step();
                shotsBefore += world.Events.Count(item => item.Type == "weapon_fired" && item.SubjectId == hunter.Id);
                world.Events.Clear();
            }
            Assert.That(prey.Owner, Is.EqualTo(1), "Taken by side 1.");
            Assert.That(hunter.Order, Is.EqualTo(OrderKind.Idle), "A prize of a side at peace is hunted no more.");

            // Shots loosed before the capture may still be in the air: the six seconds they need to land are not measured
            // here (Weapons_ShotsSpareASideAtPeace covers such shots). After them nothing is loosed and nothing harmed.
            for (var i = 0; i < 6 * Rules.TickRate; i++) world.Step();
            world.Events.Clear();
            var after = (prey.Hull, prey.Crew, prey.Wounded);
            for (var i = 0; i < 15 * Rules.TickRate; i++) world.Step();
            Assert.That(world.Events.Count(item => item.Type == "weapon_fired" && item.SubjectId == hunter.Id), Is.Zero, "Nothing more is loosed at it.");
            Assert.That((prey.Hull, prey.Crew, prey.Wounded), Is.EqualTo(after));
            Assert.That(Answer(world, 0, CommandKind.AttackShip, hunter, prey.Id), Is.EqualTo(CommandReason.InvalidTarget), "Nor can the hunt be ordered again.");
        }

        [Test]
        public void Weapons_ASplashInPeaceHarmsOnlyTheEnemy()
        {
            // A bomber shoots at a third-side hull with a hull of side 1 right beside it, inside the splash. At war the
            // splash hurts both; at peace with side 1 only the enemy is hurt, shot after shot.
            (double enemyLost, double besideLost, int hits) Bombard(bool atPeace)
            {
                var world = Empty();
                if (atPeace) world.Relations = SideRelations.AllHostile.With(0, 1, false);
                var bomber = world.SpawnShip(0, ShipClass.Bomber, 0, 0);
                var enemy = world.SpawnShip(2, ShipClass.Merchant, 150, 0);
                var beside = world.SpawnShip(1, ShipClass.Merchant, 150, 5);
                enemy.Hull = beside.Hull = 1e6;
                Assert.That(Answer(world, 0, CommandKind.AttackShip, bomber, enemy.Id), Is.EqualTo(CommandReason.None));
                var hits = 0;
                for (var i = 0; i < 150 * Rules.TickRate; i++)
                {
                    world.Step();
                    hits += world.Events.Count(item => item.Type == "projectile_hit" && item.SubjectId == beside.Id);
                    world.Events.Clear();
                }
                return (1e6 - enemy.Hull, 1e6 - beside.Hull, hits);
            }

            var war = Bombard(false);
            Assert.That(war.enemyLost, Is.GreaterThan(0), "At war the target is hurt.");
            Assert.That(war.besideLost, Is.GreaterThan(0), "And the hull beside it, of a side at war too.");
            var peace = Bombard(true);
            Assert.That(peace.enemyLost, Is.GreaterThan(0), "At peace with side 1 the enemy is hurt still.");
            Assert.That((peace.besideLost, peace.hits), Is.EqualTo((0.0, 0)), "The hull of the side at peace is not, however near.");
        }

        [Test]
        public void Knowledge_TheThirdSideLearnsOfDepositsItSees()
        {
            // Which deposits a side knows is a mask of one bit a slot: each of the three learns of what it sees, and only that.
            var world = Empty();
            ResourceNode Deposit(double x, double y)
            {
                var node = new ResourceNode { Id = world.NextId++, Type = ResourceType.Wood, X = x, Y = y, Stock = 500 * Rules.Centi, Capacity = 500 * Rules.Centi };
                world.Nodes.Add(node);
                return node;
            }
            var north = Deposit(0, 900);
            var south = Deposit(0, -900);
            var unseen = Deposit(900, 900);
            world.SpawnShip(2, ShipClass.Cutter, 0, 880);
            world.SpawnShip(0, ShipClass.Cutter, 0, -880);
            for (var i = 0; i < Rules.TickRate; i++) world.Step();
            Assert.That(north.KnownMask, Is.EqualTo(1 << Owners.Independent), "The third side has seen it, nobody else.");
            Assert.That(south.KnownMask, Is.EqualTo(1 << Owners.Human));
            Assert.That(unseen.KnownMask, Is.Zero);
            var loaded = SaveGame.Deserialize(SaveGame.Serialize(world), out _);
            Assert.That(loaded.Nodes.Select(node => node.KnownMask), Is.EqualTo(world.Nodes.Select(node => node.KnownMask)), "The save keeps what each slot knows.");
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
