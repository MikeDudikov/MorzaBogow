using MorzaBogow.Presentation;
using MorzaBogow.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace MorzaBogow.Tests
{
    /// <summary>
    /// The crews on deck as the battle draws them. First the oars and helm they work: one stroke for the whole bank with
    /// the blade driving aft in the water, an oar without a rower drawn in on its own, the steering oars turning with the
    /// hull's turn.
    /// </summary>
    public sealed class CrewPresentationTests
    {
        [Test]
        public void Oars_AllPullTogetherAndDriveAftInTheWater()
        {
            var world = new World(1);
            var ship = world.SpawnShip(0, ShipClass.Ram, 0, 0);
            var view = ShipVisual.Create(ship, null);
            try
            {
                var parts = view.Parts;
                var half = parts.OarPins.Length;
                float? lastTip = null;
                var driving = 0;
                var wasOut = false;
                for (var frame = 0; frame < 240; frame++)
                {
                    view.Animate(0.8f, frame / 60f, 1f / 60f);
                    // Every oar of a side stands at the same angle: one stroke, not a ripple along the bank.
                    for (var i = 1; i < half; i++)
                    {
                        Assert.That(Quaternion.Angle(view.OarPivot(i).localRotation, view.OarPivot(0).localRotation), Is.LessThan(0.01f), "port oars together");
                        Assert.That(Quaternion.Angle(view.OarPivot(half + i).localRotation, view.OarPivot(half).localRotation), Is.LessThan(0.01f), "starboard oars together");
                    }
                    var pivot = view.OarPivot(half);
                    var tip = view.Model.InverseTransformPoint(pivot.TransformPoint(new Vector3(parts.OarLength[0], 0f, 0f)));
                    var bladeLow = tip.y < view.Model.InverseTransformPoint(pivot.position).y - parts.OarLength[0] * Mathf.Sin(parts.OarTilt[0] * Mathf.Deg2Rad) - 0.05f;
                    // Only once the oar is fully out, so its coming out from along the side is not taken for the stroke.
                    var isOut = view.OarStowed(half) < 1e-3f;
                    if (lastTip.HasValue && wasOut && isOut && RowingStroke.Driving(view.StrokePhase) && bladeLow)
                    {
                        Assert.That(tip.z, Is.LessThanOrEqualTo(lastTip.Value + 1e-4f), $"frame {frame}: the blade in the water moves aft");
                        driving++;
                    }
                    lastTip = tip.z;
                    wasOut = isOut;
                }
                Assert.That(driving, Is.GreaterThan(20), "Strokes were pulled.");
            }
            finally
            {
                Object.DestroyImmediate(view.gameObject);
            }
        }

        [Test]
        public void Oars_AnOarWithoutARowerIsDrawnInOnItsOwn()
        {
            var world = new World(1);
            var ship = world.SpawnShip(0, ShipClass.Cutter, 0, 0);
            var view = ShipVisual.Create(ship, null);
            try
            {
                view.SetOarCrewed(0, false);
                for (var frame = 0; frame < 120; frame++) view.Animate(0.7f, frame / 60f, 1f / 60f);
                Assert.That(view.OarStowed(0), Is.GreaterThan(0.95f), "The oar without its rower lies along the side.");
                Assert.That(view.OarStowed(1), Is.LessThan(0.05f), "The crewed oar beside it keeps rowing.");
                view.SetOarCrewed(0, true);
                for (var frame = 0; frame < 120; frame++) view.Animate(0.7f, frame / 60f, 1f / 60f);
                Assert.That(view.OarStowed(0), Is.LessThan(0.05f), "Manned again, it rows.");
            }
            finally
            {
                Object.DestroyImmediate(view.gameObject);
            }
        }

        [Test]
        public void Helm_SteeringOarsTurnWithTheHullAndCarryTheTillers()
        {
            var world = new World(1);
            var ship = world.SpawnShip(0, ShipClass.Ram, 0, 0);
            var view = ShipVisual.Create(ship, null);
            try
            {
                view.Animate(0.5f, 0f, 1f / 60f);
                var rest = view.Model.InverseTransformPoint(view.TillerGrip(1));
                for (var frame = 1; frame <= 90; frame++)
                {
                    view.transform.rotation = Quaternion.Euler(0f, frame * 0.4f, 0f);
                    view.Animate(0.5f, frame / 60f, 1f / 60f);
                }
                Assert.That(Mathf.Abs(view.SteeringAngle), Is.GreaterThan(5f), "A steady turn puts the helm over.");
                var turned = view.Model.InverseTransformPoint(view.TillerGrip(1));
                Assert.That(Vector3.Distance(turned, rest), Is.GreaterThan(0.05f), "The tiller moves with the oar.");
                for (var frame = 0; frame < 120; frame++) view.Animate(0.5f, 2f + frame / 60f, 1f / 60f);
                Assert.That(Mathf.Abs(view.SteeringAngle), Is.LessThan(1f), "Straight again, the helm comes amidships.");
            }
            finally
            {
                Object.DestroyImmediate(view.gameObject);
            }
        }
    }
}
