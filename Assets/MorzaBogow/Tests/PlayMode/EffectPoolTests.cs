using System.Collections;
using System.Linq;
using MorzaBogow.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MorzaBogow.Tests
{
    /// <summary>The effect pool lends, takes back and reuses its objects within a limit; the battle's effects use it unchanged.</summary>
    public sealed class EffectPoolTests
    {
        /// <summary>A splash with a spray under it: the pool drives the spray through the splash, its top system.</summary>
        static GameObject Splash()
        {
            var item = new GameObject("Test splash");
            var system = item.AddComponent<ParticleSystem>();
            var main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            var spray = new GameObject("Test spray");
            spray.transform.SetParent(item.transform, false);
            var sprayMain = spray.AddComponent<ParticleSystem>().main;
            sprayMain.playOnAwake = false;
            sprayMain.loop = true;
            return item;
        }

        [UnityTest]
        public IEnumerator Pool_LendsTakesBackAndReusesWithinItsLimit()
        {
            var pool = new EffectPool(Splash, 3);
            var stranger = new GameObject("Not from the pool");
            try
            {
                pool.Prewarm(2);
                Assert.That(pool.CreatedCount, Is.EqualTo(2));
                Assert.That(pool.FreeCount, Is.EqualTo(2));
                Assert.That(pool.ActiveCount, Is.Zero);

                var first = pool.Rent();
                var second = pool.Rent();
                var third = pool.Rent();
                Assert.That(pool.CreatedCount, Is.EqualTo(3), "Two waited in the pool; only the third is new.");
                Assert.That(pool.ActiveCount, Is.EqualTo(3));
                Assert.That(pool.Rent(), Is.Null, "Beyond the limit an effect is simply not shown.");
                foreach (var item in new[] { first, second, third })
                {
                    Assert.That(item.activeSelf, Is.True);
                    Assert.That(item.GetComponent<ParticleSystem>().isPlaying, Is.True, "A lent splash plays from the start.");
                    Assert.That(item.transform.GetChild(0).GetComponent<ParticleSystem>().isPlaying, Is.True, "Its spray plays with it.");
                }

                var splash = first.GetComponent<ParticleSystem>();
                var spray = first.transform.GetChild(0).GetComponent<ParticleSystem>();
                splash.Emit(5);
                spray.Emit(5);
                Assert.That(splash.particleCount, Is.GreaterThan(0));
                Assert.That(pool.Return(first), Is.True);
                Assert.That(first.activeSelf, Is.False);
                Assert.That(splash.isPlaying, Is.False);
                Assert.That(splash.particleCount, Is.Zero, "A returned splash keeps no particles.");
                Assert.That(spray.particleCount, Is.Zero, "Nor does its spray.");
                Assert.That(pool.Return(first), Is.False, "Only what is out can come back.");
                Assert.That(pool.Return(stranger), Is.False);
                Assert.That(stranger.activeSelf, Is.True, "Nothing foreign is switched off.");

                var again = pool.Rent();
                Assert.That(again, Is.SameAs(first), "The returned splash is used again.");
                Assert.That(pool.CreatedCount, Is.EqualTo(3));
                Assert.That(again.GetComponent<ParticleSystem>().isPlaying, Is.True);

                Object.Destroy(second);
                yield return null;
                Assert.That(pool.Return(second), Is.True, "A splash destroyed while out is forgotten.");
                Assert.That(pool.ActiveCount, Is.EqualTo(2));

                // One destroyed while it waits in the pool is skipped, and a new one is made in its place.
                Assert.That(pool.Return(third), Is.True);
                Object.Destroy(third);
                yield return null;
                var fresh = pool.Rent();
                Assert.That(fresh, Is.Not.Null);
                Assert.That(fresh != third, Is.True);
                Assert.That(pool.CreatedCount, Is.EqualTo(4));
                Assert.That(fresh.activeSelf, Is.True);
            }
            finally
            {
                pool.Clear();
                Object.Destroy(stranger);
            }
            yield return null;
            var left = Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None).Count(item => item.name == "Test splash");
            Assert.That(left, Is.Zero, "Clear destroys what the pool made.");
        }

        [UnityTest]
        public IEnumerator Effects_KeepTheLimitOf160AndReuseTheirObjects()
        {
            var effects = new BattleEffects(null);
            var hull = new GameObject("Sinking hull under test");
            try
            {
                for (var i = 0; i < 200; i++) effects.Flash(new Vector3(i, 0f, 0f), 10f);
                Assert.That(effects.ActiveCount, Is.EqualTo(160), "At most 160 effects at once, as before the pool.");
                Assert.That(effects.CreatedObjects, Is.EqualTo(160));

                effects.Update(1f);
                Assert.That(effects.ActiveCount, Is.Zero, "A flash lasts 0.6 s.");

                for (var i = 0; i < 100; i++) effects.Flash(Vector3.zero, 5f);
                for (var i = 0; i < 20; i++) effects.Ping(Vector3.zero);
                Assert.That(effects.ActiveCount, Is.EqualTo(120));
                Assert.That(effects.CreatedObjects, Is.EqualTo(180), "Flashes reuse the first volley's objects; only the pings are new.");

                effects.Sink(hull.transform);
                Assert.That(effects.SinkingCount, Is.EqualTo(1));
            }
            finally
            {
                effects.Clear();
            }
            Assert.That(effects.ActiveCount, Is.Zero);
            Assert.That(effects.SinkingCount, Is.Zero);
            yield return null;
            Assert.That(hull == null, Is.True, "A hull still sinking goes with the battle.");
        }
    }
}
