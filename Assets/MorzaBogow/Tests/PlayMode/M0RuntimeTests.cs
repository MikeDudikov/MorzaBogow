using System.Collections;
using System.Linq;
using MorzaBogow.Presentation;
using MorzaBogow.Settings;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.TestTools;

namespace MorzaBogow.Tests
{
    public sealed class M0RuntimeTests
    {
        [Test]
        public void ProjectUsesUltraHdrpAsset()
        {
            var asset = GraphicsSettings.currentRenderPipeline as HDRenderPipelineAsset;
            Assert.That(asset, Is.Not.Null);
            var settings = asset.currentPlatformRenderPipelineSettings;
            Assert.That(settings.supportRayTracing, Is.True);
            Assert.That(settings.supportWater, Is.True);
            Assert.That(settings.supportVolumetrics, Is.True);
        }

        [UnityTest]
        public IEnumerator ControllerExposesOnlyModesInRange()
        {
            var host = new GameObject("Display settings under test");
            var controller = host.AddComponent<DisplaySettingsController>();
            yield return null;
            Assert.That(controller.Available.All(ResolutionCatalog.IsInRange), Is.True);
            Object.Destroy(host);
        }

        [UnityTest]
        public IEnumerator RequestForUnreportedModeIsIgnored()
        {
            var host = new GameObject("Display settings under test");
            var controller = host.AddComponent<DisplaySettingsController>();
            yield return null;
            controller.Request(new DisplayModeSize(1, 1), false);
            Assert.That(controller.Session.State, Is.EqualTo(ResolutionChangeState.Idle));
            Object.Destroy(host);
        }
    }
}
