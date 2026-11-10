using System.Collections;
using System.Linq;
using MorzaBogow.Presentation;
using MorzaBogow.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MorzaBogow.Tests
{
    /// <summary>Ships as the battle shows them: Aegean parts, colours, marks of the fight, sinking and breaking up, missiles, wreckage, the fight on deck.</summary>
    public sealed class ShipLookTests
    {
        static PlaceholderMaterials Materials()
        {
            var lit = Shader.Find("HDRP/Lit");
            return new PlaceholderMaterials
            {
                Wood = new Material(lit) { name = "wood" },
                Bronze = new Material(lit) { name = "bronze" },
                PlayerSail = new Material(lit) { name = "player sail" },
                EnemySail = new Material(lit) { name = "enemy sail" },
            };
        }

        [Test]
        public void Look_EveryClassIsBuiltFromItsAegeanParts()
        {
            var world = new World(1);
            foreach (ShipClass shipClass in System.Enum.GetValues(typeof(ShipClass)))
            {
                var ship = world.SpawnShip(0, shipClass, 0, 0);
                var view = ShipVisual.Create(ship, null);
                try
                {
                    var model = view.Model;
                    foreach (var part in new[] { "Hull", "Deck", "Rig", "Steering oars", "Sail" })
                        Assert.That(model.Find(part), Is.Not.Null, $"{shipClass}: {part}");
                    Assert.That(model.Find("Ram") != null, Is.EqualTo(ship.Definition.CanRam), $"{shipClass}: spur");
                    Assert.That(view.OarCount, Is.EqualTo(view.Parts.OarPins.Length * 2), $"{shipClass}: oars on both sides");
                    Assert.That(model.GetComponentsInChildren<Transform>().Count(t => t.name.StartsWith("Mount ")), Is.EqualTo(ship.Definition.Slots.Length));
                    Assert.That(view.GetComponentsInChildren<Collider>(true), Is.Empty, "Picked by position, moved by the simulation.");
                    Assert.That(view.gameObject.name, Does.Contain(view.Parts.Form.TypeName));
                }
                finally
                {
                    Object.DestroyImmediate(view.gameObject);
                }
            }
        }

        [Test]
        public void Look_TheSailTakesTheSideColourAndChangesItWhenTaken()
        {
            var materials = Materials();
            var ship = new World(1).SpawnShip(0, ShipClass.Ram, 0, 0);
            var view = ShipVisual.Create(ship, materials);
            try
            {
                Assert.That(view.SailMaterial, Is.SameAs(materials.PlayerSail));
                view.ApplyOwner(1, materials);
                Assert.That(view.SailChanging, Is.True, "The sail comes down before the new colours go up.");
                Assert.That(view.SailMaterial, Is.SameAs(materials.PlayerSail));
                for (var i = 0; i < 6; i++) view.Animate(0.3f, i * 0.5f, 0.5f);
                Assert.That(view.SailMaterial, Is.SameAs(materials.EnemySail));
                Assert.That(view.SailChanging, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(view.gameObject);
            }
        }

        [Test]
        public void Damage_BlowsLeaveBreachesOnTheStruckSideAndRepairsTakeThemAway()
        {
            var ship = new World(1).SpawnShip(0, ShipClass.Ram, 0, 0);
            var view = ShipVisual.Create(ship, null);
            try
            {
                view.Hit(view.transform.position + view.transform.right * 6f, 1f, false);
                Assert.That(view.BreachCount, Is.EqualTo(1));
                var breach = view.Model.Find("Breach");
                Assert.That(breach.localPosition.x, Is.GreaterThan(0f), "On the side that was struck.");
                for (var i = 0; i < 12; i++) view.Hit(view.transform.position - view.transform.right * 6f, 1.5f, i % 2 == 0);
                Assert.That(view.BreachCount, Is.EqualTo(ShipVisual.MaxBreaches));
                view.StickArrows(view.transform.position + view.transform.right * 30f, 40);
                Assert.That(view.StuckArrows, Is.InRange(1, ShipVisual.MaxStuckArrows));

                view.SetCondition(0.4f, new double[] { 0, 10, 0 });
                Assert.That(view.Sail.GetComponentsInChildren<Transform>().Count(t => t.name == "Tear"), Is.EqualTo(2), "A sail torn below half the hull.");
                Assert.That(view.IsPostBroken(0), Is.True);
                Assert.That(view.IsPostBroken(1), Is.False);
                Assert.That(view.RamBent, Is.True);

                view.SetCondition(1f, new double[] { 10, 10, 10 });
                Assert.That(view.BreachCount, Is.Zero, "A repaired hull shows no breaches.");
                Assert.That(view.StuckArrows, Is.Zero);
                Assert.That(view.IsPostBroken(0), Is.False);
                Assert.That(view.RamBent, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(view.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator Fate_ABrokenShipPartsSinksAndLeavesWreckageThatGoesInTime()
        {
            var effects = new BattleEffects(null);
            var ship = new World(1).SpawnShip(1, ShipClass.Merchant, 0, 0);
            var view = ShipVisual.Create(ship, null);
            try
            {
                effects.Sink(view, true);
                Assert.That(effects.BrokenShips, Is.EqualTo(1));
                effects.Update(1f);
                Assert.That(view.Model.Find("Hull").gameObject.activeSelf, Is.False, "The whole hull is gone...");
                Assert.That(Enumerable.Range(0, 3).All(i => view.Model.Find($"Broken piece {i}") != null), Is.True, "...in three pieces.");
                Assert.That(effects.SplinterParticles, Is.GreaterThan(0), "Splinters burst where it broke.");
                effects.Update(2f);
                Assert.That(effects.DebrisCount, Is.InRange(1, BattleEffects.MaxDebris), "Planks, oars and the merchantman's jars float off.");
                effects.Update(9f);
                Assert.That(effects.SinkingCount, Is.Zero);
                yield return null;
                Assert.That(view == null, Is.True, "The sunk ship's objects are gone.");
                effects.Update(40f);
                yield return null;
                Assert.That(effects.DebrisCount, Is.Zero, "Wreckage goes under in the end.");
            }
            finally
            {
                effects.Clear();
                if (view != null) Object.DestroyImmediate(view.gameObject);
            }
        }

        [Test]
        public void Missiles_FlyOnlyWhereThePlayerCanSee()
        {
            var world = new World(1);
            world.SpawnShip(0, ShipClass.Ram, 0, 0);
            var effects = new BattleEffects(null);
            try
            {
                world.Projectiles.Add(new Projectile { Id = 9001, Weapon = WeaponType.DeckBallista, FromX = 0, FromY = 0, ToX = 120, ToY = 0, LaunchTick = world.Tick, ArriveTick = world.Tick + 30 });
                world.Projectiles.Add(new Projectile { Id = 9002, Weapon = WeaponType.HeavyCatapult, FromX = 5000, FromY = 5000, ToX = 5200, ToY = 5000, LaunchTick = world.Tick, ArriveTick = world.Tick + 60 });
                effects.SyncMissiles(world, 0, 0.5f);
                Assert.That(effects.MissilesShown, Is.EqualTo(1), "The shot far out in the fog is not drawn.");
                world.Projectiles.Clear();
                effects.SyncMissiles(world, 0, 0.5f);
                Assert.That(effects.MissilesShown, Is.Zero, "A landed shot leaves the sky.");
                var lobbed = BattleEffects.Along(Vector3.zero, new Vector3(200f, 0f, 0f), 0.5f, true, 3f);
                var straight = BattleEffects.Along(Vector3.zero, new Vector3(200f, 0f, 0f), 0.5f, false, 3f);
                Assert.That(lobbed.y, Is.GreaterThan(straight.y + 5f), "A stone flies a high arc, a bolt almost straight.");
            }
            finally
            {
                effects.Clear();
            }
        }

        [Test]
        public void Wreck_FloatsAsWreckageWithItsCargo()
        {
            var effects = new BattleEffects(null);
            try
            {
                var silver = effects.CreateWreck(new Vector3(10f, 0f, 10f), 7, true);
                Assert.That(silver.transform.Find("Hull piece"), Is.Not.Null);
                Assert.That(silver.GetComponentsInChildren<Transform>().Count(t => t.name == "Jar"), Is.GreaterThan(0));
                Assert.That(silver.GetComponentsInChildren<Transform>().Count(t => t.name == "Silver"), Is.GreaterThan(0));
                var timber = effects.CreateWreck(new Vector3(-10f, 0f, 10f), 8, false);
                Assert.That(timber.GetComponentsInChildren<Transform>().Count(t => t.name == "Timber"), Is.GreaterThan(0));
                Object.DestroyImmediate(silver);
                Object.DestroyImmediate(timber);
            }
            finally
            {
                effects.Clear();
            }
        }

        [UnityTest]
        public IEnumerator Boarding_LossesFellMenOnDeckOrOverTheSide()
        {
            var world = new World(1);
            var attackerShip = world.SpawnShip(0, ShipClass.Boarder, 0, 0);
            var targetShip = world.SpawnShip(1, ShipClass.Ram, 12, 0);
            var attacker = ShipVisual.Create(attackerShip, null);
            var target = ShipVisual.Create(targetShip, null);
            target.transform.position = new Vector3(12f, 0f, 0f);
            var parent = new GameObject("Crews under test").transform;
            var fight = new DeckFight(parent, null, 0, 1);
            var splashes = 0;
            fight.Splash = _ => splashes++;
            try
            {
                fight.Update(attacker, target, 16, 16, 0.1f, true);
                Assert.That(fight.Shown, Is.EqualTo(16));
                Assert.That(fight.Fallen, Is.Zero, "Nobody falls before the first loss.");
                fight.Update(attacker, target, 10, 16, 0.1f, true);
                Assert.That(fight.Fallen, Is.EqualTo(3), "Six men lost: three figures fall.");
                for (var front = 9; front >= 0; front--) fight.Update(attacker, target, front, front, 0.1f, true);
                Assert.That(fight.Fallen, Is.EqualTo(DeckFight.MaxFallen), "Only the latest fallen stay on deck.");
                Assert.That(fight.Overboard, Is.GreaterThan(0), "Some go over the side.");
                for (var i = 0; i < 30; i++) fight.Update(attacker, target, 0, 0, 0.1f, true);
                Assert.That(splashes, Is.GreaterThan(0), "A man in the sea makes a splash.");
                yield return null;
            }
            finally
            {
                fight.Destroy();
                Object.DestroyImmediate(parent.gameObject);
                Object.DestroyImmediate(attacker.gameObject);
                Object.DestroyImmediate(target.gameObject);
            }
        }
    }
}
