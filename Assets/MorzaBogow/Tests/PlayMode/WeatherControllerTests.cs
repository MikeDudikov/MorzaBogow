using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MorzaBogow.Presentation;
using MorzaBogow.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.TestTools;

namespace MorzaBogow.Tests
{
    /// <summary>
    /// Weather is presentation only: it moves smoothly, changes the sea no more than four times a second, never touches
    /// the scene's own profile and leaves the world as it was. No HDRP water in the test scene.
    /// </summary>
    public sealed class WeatherControllerTests
    {
        /// <summary>A stand-in sea with a swell, so hulls really pitch, roll and heave; it counts the batches it is asked for.</summary>
        sealed class SwellSea : IWaterHeights
        {
            public int Batches;

            public bool TryHeights(Vector3[] points, int count, float[] heights)
            {
                Batches++;
                for (var i = 0; i < count; i++) heights[i] = 1.5f * Mathf.Sin(points[i].x * 0.11f) + Mathf.Cos(points[i].z * 0.07f);
                return true;
            }
        }

        [Test]
        public void Weather_AStormComesSmoothlyAndTheCalmReturnsExactly()
        {
            var sunObject = new GameObject("Test sun");
            sunObject.AddHDLight(LightType.Directional);
            var sun = sunObject.GetComponent<Light>();
            sun.intensity = 100000f;
            sun.useColorTemperature = true;
            sun.colorTemperature = 5600f;

            // A profile in memory stands in for the scene's profile asset.
            var sceneProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            var sceneFog = sceneProfile.Add<Fog>();
            sceneFog.enabled.Override(true);
            sceneFog.meanFreePath.Override(6000f);
            sceneFog.maximumHeight.Override(50f);
            var sceneExposure = sceneProfile.Add<Exposure>();
            sceneExposure.mode.Override(ExposureMode.Fixed);
            sceneExposure.fixedExposure.Override(12.25f);
            var volumeObject = new GameObject("Test volume");
            var volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = sceneProfile;

            var host = new GameObject("Weather under test");
            var weather = host.AddComponent<WeatherController>();
            try
            {
                weather.Configure(null, sun, volume);
                Assert.That(weather.SceneVolume, Is.SameAs(volume));
                Assert.That(weather.Overlay.weight, Is.Zero, "Calm: the scene's own picture.");
                Assert.That(weather.Overlay.priority, Is.GreaterThan(volume.priority));
                Assert.That(weather.CalmSea, Is.EqualTo(WeatherController.DefaultCalmSea));
                Assert.That(weather.Overlay.sharedProfile.TryGet<Fog>(out var stormFog), Is.True);
                Assert.That(weather.Overlay.sharedProfile.TryGet<Exposure>(out var stormExposure), Is.True);

                weather.SetWeather(1f, 4f);
                weather.Advance(2f);
                Assert.That(weather.Intensity, Is.EqualTo(0.5f).Within(1e-4f), "Halfway after half the time.");
                Assert.That(weather.Overlay.weight, Is.EqualTo(1f));
                Assert.That(sun.intensity, Is.EqualTo(Mathf.Lerp(100000f, 100000f * WeatherController.StormSunFactor, 0.5f)).Within(1f));
                Assert.That(1f / stormFog.meanFreePath.value, Is.EqualTo(Mathf.Lerp(1f / 6000f, 1f / WeatherController.StormFogDistance, 0.5f)).Within(1e-6f),
                    "The haze's density moves evenly.");
                Assert.That(stormExposure.fixedExposure.value, Is.EqualTo(12.25f + WeatherController.StormExposureShift * 0.5f).Within(1e-4f));

                // Asked again for the storm it is heading to, the weather goes on instead of starting over.
                weather.SetWeather(1f, 4f);
                Assert.That(weather.Intensity, Is.EqualTo(0.5f).Within(1e-4f));
                weather.Advance(2f);
                Assert.That(weather.Intensity, Is.EqualTo(1f), "The storm arrives when it was first due.");
                Assert.That(sun.intensity, Is.EqualTo(100000f * WeatherController.StormSunFactor).Within(1f));
                Assert.That(sun.colorTemperature, Is.EqualTo(WeatherController.StormSunTemperature).Within(0.5f));
                Assert.That(stormFog.meanFreePath.value, Is.EqualTo(WeatherController.StormFogDistance).Within(0.01f));
                Assert.That(stormFog.maximumHeight.value, Is.EqualTo(WeatherController.StormFogHeight).Within(0.01f));

                weather.SetWeather(0f, 2f);
                weather.Advance(2.5f);
                Assert.That(weather.Intensity, Is.Zero);
                Assert.That(weather.Overlay.weight, Is.Zero, "Calm again: the overlay has no effect.");
                Assert.That(sun.intensity, Is.EqualTo(100000f), "The sun exactly as the scene had it.");
                Assert.That(sun.colorTemperature, Is.EqualTo(5600f));

                // The scene's profile was never touched.
                Assert.That(volume.sharedProfile, Is.SameAs(sceneProfile));
                Assert.That(sceneFog.meanFreePath.value, Is.EqualTo(6000f));
                Assert.That(sceneFog.maximumHeight.value, Is.EqualTo(50f));
                Assert.That(sceneExposure.fixedExposure.value, Is.EqualTo(12.25f));
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(volumeObject);
                Object.DestroyImmediate(sunObject);
                foreach (var component in sceneProfile.components) Object.DestroyImmediate(component);
                Object.DestroyImmediate(sceneProfile);
            }
        }

        [Test]
        public void Weather_TheSeaChangesAtMostFourTimesASecondAndEndsExactly()
        {
            var host = new GameObject("Weather under test");
            var weather = host.AddComponent<WeatherController>();
            try
            {
                weather.Configure(null, null, null);
                weather.SetWeather(1f, 3f);
                var changes = new List<float>();
                var seen = weather.SeaChanges;
                for (var frame = 1; frame <= 100; frame++)
                {
                    weather.Advance(0.04f);
                    if (weather.SeaChanges == seen) continue;
                    seen = weather.SeaChanges;
                    changes.Add(frame * 0.04f);
                }
                Assert.That(changes.Count, Is.GreaterThan(4), "The sea follows the storm in steps.");
                for (var i = 1; i < changes.Count; i++)
                    Assert.That(changes[i] - changes[i - 1], Is.GreaterThanOrEqualTo(WeatherController.SeaInterval - 1e-4f), "HDRP rebuilds the sea's spectrum on every change.");
                Assert.That(weather.Sea, Is.EqualTo(WeatherController.StormSea), "The last step reaches the storm exactly.");
                Assert.That(weather.CalmSea, Is.EqualTo(WeatherController.DefaultCalmSea));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [UnityTest]
        public IEnumerator Weather_TheBattleStaysCalmAndAStormChangesNothingInTheWorld()
        {
            var host = new GameObject("Battle under test");
            var battle = host.AddComponent<BattleController>();
            battle.Paused = true;
            var sea = new SwellSea();
            try
            {
                yield return null;
                Assert.That(battle.Weather, Is.Not.Null);
                Assert.That(battle.Weather.Intensity, Is.Zero, "A match is calm.");
                Assert.That(battle.Weather.Target, Is.Zero);

                // Storm and swell together: the hulls rock on the sea they read, and the world must not notice.
                battle.UseWaterForTest(sea);
                battle.Weather.SetWeather(1f, 0f);
                var reference = MapFactory.CreateSkirmish(20261109);
                var opponent = new Opponent(1);
                for (var i = 0; i < 300; i++) { opponent.Think(reference); reference.Step(); }
                for (var chunk = 0; chunk < 10; chunk++) battle.RunTicks(30);
                yield return null;
                reference.Events.Clear();
                Assert.That(battle.Weather.Intensity, Is.EqualTo(1f));
                Assert.That(sea.Batches, Is.GreaterThanOrEqualTo(10), "The sea was read in every presented frame.");
                Assert.That(battle.Views.Values.Any(view => view.isActiveAndEnabled && Mathf.Abs(view.CurrentPose.Heave) > 0.01f), Is.True, "The hulls ride the swell.");
                Assert.That(SaveGame.Hash(SaveGame.Serialize(battle.World)), Is.EqualTo(SaveGame.Hash(SaveGame.Serialize(reference))), "Storm and waves are only a picture.");
            }
            finally
            {
                Object.DestroyImmediate(host);
                foreach (var item in Object.FindObjectsByType<ShipVisual>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Object.DestroyImmediate(item.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator Weather_EachBattleTakesTheScenesVolumeNotAnotherOverlay()
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var volumeObject = new GameObject("Scene volume under test");
            var volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
            var first = new GameObject("First battle").AddComponent<BattleController>();
            first.Paused = true;
            BattleController second = null;
            try
            {
                yield return null;
                second = new GameObject("Second battle").AddComponent<BattleController>();
                second.Paused = true;
                yield return null;
                Assert.That(first.Weather.SceneVolume, Is.SameAs(volume));
                Assert.That(second.Weather.SceneVolume, Is.SameAs(volume), "Not the first battle's weather overlay, which ranks higher.");
                Assert.That(second.Weather.Overlay.priority, Is.EqualTo(volume.priority + 1f));
            }
            finally
            {
                Object.DestroyImmediate(first.gameObject);
                if (second != null) Object.DestroyImmediate(second.gameObject);
                Object.DestroyImmediate(volumeObject);
                Object.DestroyImmediate(profile);
                foreach (var item in Object.FindObjectsByType<ShipVisual>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Object.DestroyImmediate(item.gameObject);
            }
        }
    }
}
