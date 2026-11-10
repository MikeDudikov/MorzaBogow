using System;
using MorzaBogow.Presentation;
using MorzaBogow.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace MorzaBogow.Tests
{
    /// <summary>
    /// The ships give their crews room to work: every rower reaches the handle of his oar from his seat without the oar
    /// passing through the hull, his body inside the hull and above its side; every helmsman holds the tillers of the
    /// steering oars at the height of his hands while their blades reach the water. Measured on the generator's geometry,
    /// never by scaling a man to fit.
    /// </summary>
    public sealed class CrewGeometryTests
    {
        static ShipClass[] Classes => (ShipClass[])Enum.GetValues(typeof(ShipClass));

        static float StationOf(GreekShipForm f, float z) => Mathf.InverseLerp(GreekShips.Z(f, 0f), GreekShips.Z(f, 1f), z);

        [Test]
        public void Oars_EveryRowerReachesHisHandleFromHisSeatInsideTheHull()
        {
            foreach (var shipClass in Classes)
            {
                var parts = GreekShips.Parts(shipClass);
                var f = parts.Form;
                var tilt = parts.OarTilt[0] * Mathf.Deg2Rad;
                var inboard = parts.OarInboard[0];
                var outboard = parts.OarLength[0];
                Assert.That(parts.RowerSeats.Length, Is.EqualTo(parts.OarPins.Length), $"{shipClass}: a place for the rower of every oar");
                for (var i = 0; i < parts.OarPins.Length; i++)
                {
                    var pin = parts.OarPins[i];
                    var seat = parts.RowerSeats[i];
                    // The handle at mid-stroke: the loom rises inboard as steeply as the oar falls outboard.
                    var handle = pin + new Vector3(-inboard * Mathf.Cos(tilt), inboard * Mathf.Sin(tilt), 0f);
                    var s = StationOf(f, pin.z);
                    var label = $"{shipClass} oar {i}";

                    var rise = handle.y - seat.y;
                    if (parts.RowersStand) Assert.That(rise, Is.InRange(0.85f, 1.15f), $"{label}: a standing rower holds the loom at his waist");
                    else Assert.That(rise, Is.InRange(0.2f, 0.45f), $"{label}: a seated rower holds the loom at his lower chest");
                    var reach = new Vector2(handle.x - seat.x, handle.z - seat.z).magnitude;
                    Assert.That(reach, Is.InRange(0.3f, 0.75f), $"{label}: the handle in front of him, within his arms");
                    Assert.That(handle.z, Is.LessThan(seat.z), $"{label}: he faces aft, the handle on his stern side");

                    var seatS = StationOf(f, seat.z);
                    Assert.That(Mathf.Abs(seat.x) + 0.25f, Is.LessThan(GreekShips.HalfWidth(f, seatS) * 0.97f), $"{label}: his body inside the hull");
                    Assert.That(seat.y, Is.GreaterThanOrEqualTo(GreekShips.DeckAt(f, seatS) - 0.01f), $"{label}: on the deck, a seat or a thwart, never below it");
                    Assert.That(Mathf.Abs(handle.x), Is.LessThan(GreekShips.HalfWidth(f, s) - 0.1f), $"{label}: the handle inboard of the side");

                    // Where the oar crosses the side it passes over the gunwale (a boat's in a notch of the top strake).
                    var atSide = (pin.x - GreekShips.HalfWidth(f, s)) / Mathf.Cos(tilt);
                    var crossing = pin.y + atSide * Mathf.Sin(tilt);
                    Assert.That(crossing, Is.GreaterThan(GreekShips.Sheer(f, s) - (shipClass == ShipClass.Cutter ? 0.2f : 0.001f)), $"{label}: the oar never passes through the planking");
                    Assert.That(pin.y - outboard * Mathf.Sin(tilt), Is.LessThan(0.1f), $"{label}: the blade still reaches the water");

                    if (i > 0)
                    {
                        var spacing = pin.z - parts.OarPins[i - 1].z;
                        Assert.That(spacing, Is.GreaterThan(0.43f + 0.45f), $"{label}: room between rowers, shoulders and a swing of the oar");
                    }
                }
            }
        }

        [Test]
        public void Steering_HelmsmanHoldsTheTillersAndTheBladesReachTheWater()
        {
            foreach (var shipClass in Classes)
            {
                var parts = GreekShips.Parts(shipClass);
                var f = parts.Form;
                Assert.That(parts.SteeringOars.Length, Is.EqualTo(2), $"{shipClass}: a steering oar on each quarter");
                Assert.That(parts.HelmStands.Length, Is.InRange(1, 2), $"{shipClass}: one helmsman between both tillers or one at each");
                for (var i = 0; i < 2; i++)
                {
                    var side = i == 0 ? -1f : 1f;
                    var label = $"{shipClass} steering oar {i}";
                    var pivot = parts.SteeringPivots[i];
                    var grip = parts.TillerGrips[i];
                    var stand = parts.HelmStands[parts.HelmStands.Length == 2 ? i : 0];
                    Assert.That(Mathf.Sign(pivot.x), Is.EqualTo(side), $"{label}: on its own quarter");
                    Assert.That(Mathf.Abs(pivot.x), Is.GreaterThan(GreekShips.HalfWidth(f, 0.15f)), $"{label}: outboard of the hull");
                    var hands = grip.y - stand.y;
                    if (parts.HelmSeated) Assert.That(hands, Is.InRange(0.25f, 0.45f), $"{label}: at a seated man's hands");
                    else Assert.That(hands, Is.InRange(0.85f, 1.05f), $"{label}: at a standing man's hands");
                    var beside = new Vector2(grip.x - stand.x, grip.z - stand.z).magnitude;
                    Assert.That(beside, Is.LessThan(0.5f), $"{label}: within the helmsman's reach");
                    var bounds = parts.SteeringOars[i].Mesh.bounds;
                    Assert.That(pivot.y + bounds.min.y, Is.LessThan(-0.5f), $"{label}: the blade in the water");
                    Assert.That(parts.SteeringAxes[i].y, Is.LessThan(-0.5f), $"{label}: the shaft slants down into the sea");
                }
                foreach (var stand in parts.HelmStands)
                {
                    var s = StationOf(f, stand.z);
                    Assert.That(Mathf.Abs(stand.x) + 0.25f, Is.LessThan(GreekShips.HalfWidth(f, s)), $"{shipClass}: the helmsman inside the hull");
                    Assert.That(stand.y, Is.GreaterThanOrEqualTo(GreekShips.DeckAt(f, s) - 0.01f), $"{shipClass}: the helmsman on a deck, platform or seat");
                }
            }
        }

        [Test]
        public void Stroke_TheBladeDrivesAftInTheWaterAndReturnsAboveIt()
        {
            var previous = 0f;
            RowingStroke.Sample(0f, out previous, out _);
            for (var step = 1; step <= 200; step++)
            {
                var phase = step / 200f;
                RowingStroke.Sample(phase, out var sweep, out var lift);
                var moving = sweep - previous;
                if (phase > 0.05f && phase < RowingStroke.Drive - 0.05f)
                {
                    Assert.That(lift, Is.LessThan(0f), $"phase {phase:0.00}: in the water through the drive");
                    Assert.That(moving, Is.GreaterThan(0f), $"phase {phase:0.00}: sweeping aft (toward +1)");
                }
                else if (phase > RowingStroke.Drive + 0.05f && phase < 0.95f)
                {
                    Assert.That(lift, Is.GreaterThan(0f), $"phase {phase:0.00}: above the water through the recovery");
                    Assert.That(moving, Is.LessThan(0f), $"phase {phase:0.00}: returning forward");
                }
                previous = sweep;
            }
            Assert.That(RowingStroke.RateFor(0f, false, false), Is.Zero, "No stroke at rest.");
            Assert.That(RowingStroke.RateFor(1f, false, true), Is.GreaterThan(RowingStroke.RateFor(1f, false, false)), "A sprint in a ram's charge.");
            Assert.That(RowingStroke.RateFor(-0.3f, false, false), Is.EqualTo(RowingStroke.Astern), "Backing water.");
        }
    }
}
