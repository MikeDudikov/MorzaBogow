using System.Collections.Generic;
using System.Linq;
using MorzaBogow.Presentation;
using MorzaBogow.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace MorzaBogow.Tests
{
    /// <summary>The hull motion driver's timing: which hulls step on which tick, and what the view is shown between steps.</summary>
    public sealed class SeakeepingDriverTests
    {
        // A moderate Etesian sea, named rather than drawn from the seed: a calm match would hardly move the hulls.
        static World Match()
        {
            var world = MapFactory.CreateSkirmish(20261109);
            world.EnableMarine(MarineClimate.Aegean(WindRegime.EtesianModerate, AegeanRegion.Central), world.Random.State, false, World.MatchSettleSeconds);
            return world;
        }

        static readonly float[] Fractions = { 0f, 0.25f, 0.5f, 0.75f };

        [Test]
        public void Seakeeping_FarViewStepsEachHullEveryThirdTickAndShowsItSmoothly()
        {
            var world = Match();
            var ships = world.Ships.ToList();
            Assert.That(ships.Count, Is.GreaterThanOrEqualTo(3), "A fleet to spread over the ticks.");
            Assert.That(SeakeepingDriver.StrideFor(SeakeepingDriver.FarPixelsPerMetre - 0.1f), Is.EqualTo(SeakeepingDriver.FarStride));
            Assert.That(SeakeepingDriver.StrideFor(SeakeepingDriver.FarPixelsPerMetre), Is.EqualTo(1), "Close views step every tick.");
            Assert.That(MarineState.ShipUpdateTicks % SeakeepingDriver.FarStride, Is.Zero, "Wind forces keep their five a second.");

            var driver = new SeakeepingDriver();
            var stepped = new List<int>();
            var shown = new Dictionary<int, HullPose>();
            float largestHeaveJump = 0f, largestTiltJump = 0f, heaveSeen = 0f;
            for (var tick = 0; tick < 120; tick++)
            {
                world.Step();
                driver.Step(world, ships, SeakeepingDriver.FarStride);
                stepped.Add(driver.Stepped);
                foreach (var fraction in Fractions)
                    foreach (var ship in ships)
                    {
                        Assert.That(driver.TryPose(ship.Id, world.Tick, fraction, out var pose, out _, out _), $"Ship {ship.Id} has a pose.");
                        if (shown.TryGetValue(ship.Id, out var before) && tick > 0)
                        {
                            largestHeaveJump = Mathf.Max(largestHeaveJump, Mathf.Abs(pose.Heave - before.Heave));
                            largestTiltJump = Mathf.Max(largestTiltJump, Mathf.Abs(pose.Roll - before.Roll), Mathf.Abs(pose.Pitch - before.Pitch));
                        }
                        heaveSeen = Mathf.Max(heaveSeen, Mathf.Abs(pose.Heave));
                        shown[ship.Id] = pose;
                    }
            }

            Assert.That(stepped[0], Is.EqualTo(ships.Count), "All hulls are laid on the water at once.");
            for (var tick = 6; tick + 2 < stepped.Count; tick++)
                Assert.That(stepped[tick] + stepped[tick + 1] + stepped[tick + 2], Is.EqualTo(ships.Count), $"Each hull steps once in every three ticks (from tick {tick}).");
            Assert.That(stepped.Skip(6).Max(), Is.LessThan(ships.Count), "Never all of them on one tick.");
            Assert.That(heaveSeen, Is.GreaterThan(0.02f), "The sea moves the hulls.");
            // A quarter of a tick is 8 ms: hull motion moves millimetres in it, so a jump would stand out.
            Assert.That(largestHeaveJump, Is.LessThan(0.05f), "No jump in heave between steps.");
            Assert.That(largestTiltJump, Is.LessThan(1f), "No jump in roll or pitch between steps.");
        }

        [Test]
        public void Seakeeping_CloseViewStepsEveryTickAndAReturningHullStartsOnTheWater()
        {
            var world = Match();
            var ships = world.Ships.ToList();
            var driver = new SeakeepingDriver();
            for (var tick = 0; tick < 30; tick++)
            {
                world.Step();
                driver.Step(world, ships);
                Assert.That(driver.Stepped, Is.EqualTo(ships.Count), "Close up, every hull every tick.");
            }

            // One hull leaves the view for a second; back in it, it is laid on the water anew: still at once, the same
            // pose at either end of the tick.
            var away = ships[0];
            var rest = ships.Skip(1).ToList();
            for (var tick = 0; tick < 30; tick++)
            {
                world.Step();
                driver.Step(world, rest);
            }
            world.Step();
            driver.Step(world, ships);
            Assert.That(driver.TryPose(away.Id, world.Tick, 0f, out var start, out _, out _));
            Assert.That(driver.TryPose(away.Id, world.Tick, 1f, out var end, out _, out _));
            Assert.That(end.Heave, Is.EqualTo(start.Heave), "Laid on the water, not moving yet.");
            Assert.That(end.Roll, Is.EqualTo(start.Roll));
            Assert.That(end.Pitch, Is.EqualTo(start.Pitch));
            var other = ships[1];
            Assert.That(driver.TryPose(other.Id, world.Tick, 0f, out var otherStart, out _, out _));
            Assert.That(driver.TryPose(other.Id, world.Tick, 1f, out var otherEnd, out _, out _));
            Assert.That(otherEnd.Heave != otherStart.Heave || otherEnd.Roll != otherStart.Roll || otherEnd.Pitch != otherStart.Pitch,
                "A hull that stayed in view keeps moving between its ticks.");
        }
    }
}
