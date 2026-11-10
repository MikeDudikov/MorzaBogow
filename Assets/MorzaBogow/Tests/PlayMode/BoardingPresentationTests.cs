using System.Collections;
using System.Linq;
using MorzaBogow.Presentation;
using MorzaBogow.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MorzaBogow.Tests
{
    /// <summary>
    /// The boarding as drawn (user's decisions of 2026-11-10): the attacker is hauled alongside, broadside to broadside and
    /// never bow first, and its front crosses the rails to fight on the other deck. The picture only: the game's ticks,
    /// positions and numbers are not touched (CrewDirector, CrewAboard).
    /// </summary>
    public sealed class BoardingPresentationTests
    {
        static (GameObject host, BattleController battle, CrewDirector crews) Battle()
        {
            var host = new GameObject("Battle with boarding");
            var battle = host.AddComponent<BattleController>();
            battle.Paused = true;
            return (host, battle, host.GetComponent<CrewDirector>());
        }

        static void Clean(GameObject host)
        {
            Object.DestroyImmediate(host);
            foreach (var item in Object.FindObjectsByType<ShipVisual>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Object.DestroyImmediate(item.gameObject);
        }

        static Ship Spawn(BattleController battle, int owner, ShipClass shipClass, double dx, double dy)
        {
            var home = battle.World.SettlementOf(0);
            return battle.World.SpawnShip(owner, shipClass, home.X + dx, home.Y + dy);
        }

        /// <summary>One tick and its frame for the views and the crews (CrewPresentationTests.Tick).</summary>
        static void Tick(BattleController battle, CrewDirector crews)
        {
            battle.RunTicks(1);
            foreach (var ship in battle.World.Ships)
                if (battle.Views.TryGetValue(ship.Id, out var view) && view != null)
                    view.Animate((float)(ship.Speed / ship.Definition.Speed), Time.unscaledTime, (float)Rules.Dt);
            crews.Present((float)Rules.Dt, null);
            crews.UpdateHauls((float)Rules.Dt);
        }

        /// <summary>The attacker's picture in the target's frame: (across, along, turn in degrees).</summary>
        static (float across, float along, float turn) Picture(CrewDirector crews, Ship attacker, Ship target)
        {
            var attackerAt = new Vector3((float)attacker.X, 0f, (float)attacker.Y) + crews.HaulOffset(attacker.Id);
            var targetAt = new Vector3((float)target.X, 0f, (float)target.Y) + crews.HaulOffset(target.Id);
            var attackerYaw = 90f - (float)(attacker.Heading * Mathf.Rad2Deg) + crews.HaulYaw(attacker.Id);
            var targetYaw = 90f - (float)(target.Heading * Mathf.Rad2Deg) + crews.HaulYaw(target.Id);
            var local = Quaternion.Inverse(Quaternion.Euler(0f, targetYaw, 0f)) * (attackerAt - targetAt);
            return (local.x, local.z, Mathf.DeltaAngle(targetYaw, attackerYaw));
        }

        [UnityTest]
        public IEnumerator Boarding_HullsLieAlongsideBroadsideWhateverTheApproach()
        {
            // Two approaches at right angles to each other (one in line, one abeam of the boarder's course); both end side by
            // side. The target has fighters, so the boarding lasts while the hulls come together.
            foreach (var (name, targetClass, dx, dy) in new[] { ("north", ShipClass.Ram, 300.0, 452.0), ("east", ShipClass.Ram, 330.0, 420.0) })
            {
                var (host, battle, crews) = Battle();
                yield return null;
                try
                {
                    var boarder = Spawn(battle, 0, ShipClass.Boarder, 300, 420);
                    var target = Spawn(battle, 1, targetClass, dx, dy);
                    target.Speed = 0;
                    battle.World.Issue(new Command { Faction = 0, Kind = CommandKind.Board, ShipIds = new[] { boarder.Id }, TargetId = target.Id });
                    var hooked = false;
                    for (var t = 0; t < 1200 && !hooked; t++)
                    {
                        Tick(battle, crews);
                        hooked = battle.World.Boardings.Any(link => link.Hooked && link.AttackerId == boarder.Id);
                    }
                    Assert.That(hooked, $"{name}: the boarder grappled its target.");
                    for (var t = 0; t < 240 && crews.HaulWeight(boarder.Id) < 0.999f; t++) Tick(battle, crews);
                    Assert.That(crews.HaulWeight(boarder.Id), Is.GreaterThan(0.999f), $"{name}: hauled alongside within the haul's 7 s.");

                    var apart = GreekShips.HalfWidth(GreekShips.Form(target.Class), 0.5f) + GreekShips.HalfWidth(GreekShips.Form(boarder.Class), 0.5f) + 0.2f;
                    var (across, along, turn) = Picture(crews, boarder, target);
                    Assert.That(Mathf.Abs(turn), Is.LessThan(2f), $"{name}: the hulls lie parallel.");
                    Assert.That(Mathf.Abs(across), Is.EqualTo(apart).Within(0.05f), $"{name}: rail to rail, a hand's breadth apart, never one hull in the other.");
                    Assert.That(Mathf.Abs(along), Is.LessThan(0.5f), $"{name}: amidships to amidships, not bow to stern.");

                    var side = Mathf.Sign(across);
                    for (var t = 0; t < 60; t++) Tick(battle, crews);
                    if (battle.World.Boardings.Any(link => link.AttackerId == boarder.Id))
                        Assert.That(Mathf.Sign(Picture(crews, boarder, target).across), Is.EqualTo(side), $"{name}: the side chosen holds through the boarding.");
                    var contact = crews.ContactSide(boarder.Id);
                    Assert.That(contact, Is.EqualTo(-(int)side), $"{name}: the boarder's oars come in on the side against the target.");
                }
                finally { Clean(host); }
            }
        }

        [Test]
        public void Boarding_TheHaulEasesBackInFourSecondsAtAnyFrameRate()
        {
            // The release once compounded frame by frame (gone in under a second at 60 fps); now it is linear in time.
            float At(int fps, float seconds)
            {
                var since = 0f;
                for (var i = 0; i < Mathf.RoundToInt(seconds * fps); i++) since += 1f / fps;
                return CrewDirector.ReleaseWeight(0.9f, since);
            }
            Assert.That(At(30, 2f), Is.EqualTo(0.45f).Within(1e-3f), "Halfway back after 2 s.");
            Assert.That(At(120, 2f), Is.EqualTo(At(30, 2f)).Within(1e-3f), "The same at 30 and 120 frames a second.");
            Assert.That(At(60, 4f), Is.EqualTo(0f).Within(1e-3f), "Back in its place after 4 s.");
        }

        [UnityTest]
        public IEnumerator Boarding_TheFrontCrossesTheRailsAndFightsOnTheOtherDeck()
        {
            var (host, battle, crews) = Battle();
            yield return null;
            try
            {
                var boarder = Spawn(battle, 0, ShipClass.Boarder, 300, 420);
                var target = Spawn(battle, 1, ShipClass.Ram, 300, 452);
                target.Speed = 0;
                battle.World.Issue(new Command { Faction = 0, Kind = CommandKind.Board, ShipIds = new[] { boarder.Id }, TargetId = target.Id });
                var hooked = false;
                for (var t = 0; t < 900 && !hooked; t++)
                {
                    Tick(battle, crews);
                    hooked = battle.World.Boardings.Any(link => link.Hooked && link.AttackerId == boarder.Id);
                }
                Assert.That(hooked, "The boarder grappled the ram.");
                var across = 0;
                for (var t = 0; t < 360 && across == 0; t++)
                {
                    Tick(battle, crews);
                    if (crews.Crews.TryGetValue(boarder.Id, out var crew)) across = crew.Across.Count;
                }
                Assert.That(across, Is.GreaterThan(0), "Boarders stand on the ram's deck.");
                var link = battle.World.Boardings.FirstOrDefault(l => l.AttackerId == boarder.Id);
                if (link != null && link.AttackerFront > 0) Assert.That(across, Is.LessThanOrEqualTo(link.AttackerFront), "No more across than the front the game fights with.");
                var view = battle.Views[target.Id];
                var form = view.Parts.Form;
                foreach (var at in crews.Crews[boarder.Id].Across)
                {
                    var local = view.Model.InverseTransformPoint(at);
                    var s = Mathf.InverseLerp(GreekShips.Z(form, 0f), GreekShips.Z(form, 1f), local.z);
                    Assert.That(s, Is.InRange(0.1f, 0.9f), "Within the ram's length.");
                    Assert.That(Mathf.Abs(local.x), Is.LessThan(GreekShips.HalfWidth(form, s)), "Inside its rails.");
                    Assert.That(local.y, Is.EqualTo(GreekShips.FloorAt(form, s, local.x)).Within(0.1f), "Feet on its deck.");
                }
            }
            finally { Clean(host); }
        }
    }
}
