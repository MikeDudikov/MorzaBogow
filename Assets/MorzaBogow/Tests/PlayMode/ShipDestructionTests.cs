using MorzaBogow.Presentation;
using MorzaBogow.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace MorzaBogow.Tests
{
    /// <summary>
    /// The ship's part of the destruction slice (docs/art/DESTRUCTION.md): far gone, the mast snaps above the deck and
    /// goes over the side the ship lists to, yard and sail with it, two lines holding it alongside; it moves with the hull
    /// and is never moved on its own (D); it is put back by a dock's repair (G) and by a pooled view's reset (I); a ship
    /// first seen far gone shows it lying already. The game's hull decides; the view never changes the ship.
    /// </summary>
    public sealed class ShipDestructionTests
    {
        static (Ship ship, ShipVisual view) Ram()
        {
            var world = new World(1);
            var ship = world.SpawnShip(0, ShipClass.Ram, 0, 0);
            var view = ShipVisual.Create(ship, null);
            view.SetCondition(1f, ship.ModuleHp);
            return (ship, view);
        }

        static void Fall(ShipVisual view)
        {
            for (var i = 0; i < 40 && view.MastFalling; i++) view.Animate(0f, i / 20f, 0.1f);
        }

        [Test]
        public void Ship_MastGoesOverTheSideItListsTo()
        {
            var (ship, view) = Ram();
            try
            {
                var hull = ship.Hull;
                // A blow low on the port side: the ship lists to port.
                view.Hit(view.Model.TransformPoint(new Vector3(-3f, 0.3f, 1f)), 1.5f, true);
                view.SetCondition(0.7f, ship.ModuleHp);
                Assert.That(view.MastBroken, Is.False, "the mast stands until the partial collapse");
                Assert.That(view.DamageStage, Is.EqualTo(DestructionStage.LocalFailure));
                view.SetCondition(0.35f, ship.ModuleHp);
                Assert.That(view.MastBroken && view.MastFalling, Is.True, "in sight, it is seen to fall");
                Assert.That(view.Rig.gameObject.activeSelf, Is.False, "the standing rig is gone");
                Assert.That(view.MastStump, Is.Not.Null, "a ragged stump is left in its step");
                Assert.That(view.Sail.parent, Is.SameAs(view.FallenMast), "the sail comes down with its yard");
                Assert.That(view.TakeMastBreak(out var snapped), Is.True, "splinters where it snapped, once");
                Assert.That(view.TakeMastBreak(out _), Is.False);
                Assert.That(view.Model.InverseTransformPoint(snapped).y, Is.GreaterThan(view.Parts.MastFoot.y + 0.5f), "above the deck, not at its foot");

                Fall(view);
                Assert.That(view.MastFalling, Is.False);
                Assert.That(view.TakeMastSplash(out _), Is.True, "water thrown up where its head went in, once");
                var head = view.MastheadNow;
                var foot = view.Parts.MastFoot;
                Assert.That(view.MastFallWay.x, Is.LessThan(-0.8f), "over the port side, the side it lists to");
                Assert.That(head.x - foot.x, Is.LessThan(-view.Parts.Form.MastHeight * 0.6f), "its head far out to port");
                Assert.That(head.y, Is.LessThan(0.2f), "its head in the water");
                Assert.That(view.DamageStage, Is.EqualTo(DestructionStage.PartialCollapse));
                Assert.That(ship.Hull, Is.EqualTo(hull), "the view never changes the ship");
            }
            finally
            {
                Object.DestroyImmediate(view.gameObject);
            }
        }

        [Test]
        public void Ship_TheFallenMastMovesWithTheHullNotOnItsOwn()
        {
            var (ship, view) = Ram();
            try
            {
                view.SetCondition(0.3f, ship.ModuleHp);
                Fall(view);
                var local = view.FallenMast.localPosition;
                var turn = view.FallenMast.localRotation;
                // The hull moves on and turns: the wreckage alongside goes with it, held where it lies.
                view.transform.SetPositionAndRotation(new Vector3(40f, 0f, -25f), Quaternion.Euler(0f, 70f, 0f));
                view.Animate(0.6f, 3f, 0.1f);
                Assert.That(view.FallenMast.localPosition, Is.EqualTo(local));
                Assert.That(Quaternion.Angle(view.FallenMast.localRotation, turn), Is.LessThan(1e-3f));
                var head = view.Model.TransformPoint(view.MastheadNow);
                Assert.That(Vector3.Distance(head, view.FallenMast.TransformPoint(Quaternion.Inverse(turn) * (view.MastheadNow - local))), Is.LessThan(1e-3f),
                    "its world place is the hull's, nothing added");
            }
            finally
            {
                Object.DestroyImmediate(view.gameObject);
            }
        }

        [Test]
        public void Ship_ADockRepairStepsANewMastAndAReusedViewHasNoWreckage()
        {
            var (ship, view) = Ram();
            try
            {
                view.SetCondition(0.3f, ship.ModuleHp);
                Fall(view);
                view.SetCondition(0.45f, ship.ModuleHp);
                Assert.That(view.MastBroken, Is.True, "half mended it is still down");
                view.SetCondition(0.6f, ship.ModuleHp);
                Assert.That(view.MastBroken, Is.False, "mended in dock, a new mast stands");
                Assert.That(view.Rig.gameObject.activeSelf, Is.True);
                Assert.That(view.MastStump, Is.Null);
                Assert.That(view.Sail.parent, Is.SameAs(view.Model), "the sail back on the standing yard");
                Assert.That(view.Sail.localPosition, Is.EqualTo(view.Parts.SailTop));

                view.SetCondition(0.2f, ship.ModuleHp);
                Assert.That(view.MastBroken, Is.True);
                view.ResetForReuse(ship, ShipLookComposer.Plain(ship.Id, ship.Class, ship.Owner));
                Assert.That(view.MastBroken, Is.False, "a view taken from the pool shows nothing of the last ship's wreckage");
                Assert.That(view.Rig.gameObject.activeSelf, Is.True);
                Assert.That(view.Model.Find("Fallen mast"), Is.Null);
                Assert.That(view.Model.Find("Loose rope"), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(view.gameObject);
            }
        }

        [Test]
        public void Ship_FirstSeenFarGoneTheMastIsAlreadyDown()
        {
            var world = new World(1);
            var ship = world.SpawnShip(1, ShipClass.Bomber, 0, 0);
            var view = ShipVisual.Create(ship, null);
            try
            {
                view.SetCondition(0.2f, ship.ModuleHp);
                Assert.That(view.MastBroken, Is.True);
                Assert.That(view.MastFalling, Is.False, "out of sight when it fell: found lying, not falling");
                Assert.That(view.TakeMastBreak(out _), Is.False, "and no splinters fly now");
                Assert.That(view.MastheadNow.y, Is.LessThan(0.2f));
            }
            finally
            {
                Object.DestroyImmediate(view.gameObject);
            }
        }
    }
}
