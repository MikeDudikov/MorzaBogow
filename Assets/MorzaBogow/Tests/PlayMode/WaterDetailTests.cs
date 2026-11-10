using System.Linq;
using MorzaBogow.Presentation;
using MorzaBogow.Settings;
using MorzaBogow.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace MorzaBogow.Tests
{
    /// <summary>The simulation's sea drawn on every profile; the water's decorative detail (wakes, foam, surf) from High on.</summary>
    public sealed class WaterDetailTests
    {
        [Test]
        public void Sea_EveryProfileDrawsTheSimulatedWavesAndTheDetailFromHigh()
        {
            Assert.That(QualitySettings.count, Is.EqualTo(5), "Low, Medium, High, Extra High, Ultra.");
            for (var level = 0; level < QualitySettings.count; level++)
            {
                var asset = QualitySettings.GetRenderPipelineAssetAt(level) as HDRenderPipelineAsset;
                Assert.That(asset, Is.Not.Null, QualitySettings.names[level]);
                var settings = asset.currentPlatformRenderPipelineSettings;
                Assert.That(settings.supportWaterDecals, Is.True, $"{QualitySettings.names[level]}: the simulation's waves are a water decal, so hulls ride the waves shown.");
                Assert.That(settings.supportWaterExclusion, Is.True, $"{QualitySettings.names[level]}: water is kept out of open hulls.");
            }
            Assert.That(WaterDetail.ShownAt((int)GraphicsProfile.Low), Is.False, "Low: simple detail, no wakes, foam or surf.");
            Assert.That(WaterDetail.ShownAt((int)GraphicsProfile.Medium), Is.False, "Medium: limited detail.");
            Assert.That(WaterDetail.ShownAt((int)GraphicsProfile.High), Is.True, "High: the full wake.");
            Assert.That(WaterDetail.ShownAt((int)GraphicsProfile.Ultra), Is.True);
        }

        [Test]
        public void Ship_WakeAndFoamFollowTheProfileButNeverTheirShape()
        {
            var world = new World(1);
            var ship = world.SpawnShip(0, ShipClass.Merchant, 0, 0);
            var view = ShipVisual.Create(ship, null);
            try
            {
                var decals = view.GetComponentsInChildren<WaterDecal>(true);
                Assert.That(decals.Length, Is.EqualTo(3), "Bow wave, hull foam, bow foam.");
                Assert.That(decals.All(item => item.enabled == WaterDetail.Shown), "New decals follow the current profile.");

                WaterDetail.Apply(false);
                Assert.That(decals.Any(item => item.enabled), Is.False, "Below High the wake and foam are off.");
                view.Animate(1f, 1f, 0.1f);
                Assert.That(view.BowWaveHeight, Is.GreaterThan(0f), "The ship still shapes its wake; only the drawing is off.");

                WaterDetail.Apply(true);
                Assert.That(decals.All(item => item.enabled), "From High on they are drawn.");
            }
            finally
            {
                WaterDetail.Apply(WaterDetail.Shown);
                Object.DestroyImmediate(view.gameObject);
            }
        }
    }
}
