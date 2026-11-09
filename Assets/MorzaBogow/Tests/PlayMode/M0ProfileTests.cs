using System.Collections;
using MorzaBogow.Settings;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.TestTools;

namespace MorzaBogow.Tests
{
    public sealed class M0ProfileTests
    {
        static RenderPipelineSettings SettingsOf(GraphicsProfile profile)
        {
            var asset = QualitySettings.GetRenderPipelineAssetAt((int)profile) as HDRenderPipelineAsset;
            Assert.That(asset, Is.Not.Null, profile.ToString());
            return asset.currentPlatformRenderPipelineSettings;
        }

        [Test]
        public void QualityLevelsMatchTheFiveProfiles()
        {
            Assert.That(QualitySettings.names, Is.EqualTo(new[] { "Low", "Medium", "High", "Extra High", "Ultra" }));
        }

        [Test]
        public void OnlyUltraEnablesRayTracing()
        {
            foreach (var profile in GraphicsProfiles.All)
            {
                Assert.That(SettingsOf(profile).supportRayTracing, Is.EqualTo(profile == GraphicsProfile.Ultra), profile.ToString());
            }
        }

        [Test]
        public void ReflectionsAndIndirectLightFollowTheProfileTable()
        {
            Assert.That(SettingsOf(GraphicsProfile.Medium).supportSSR, Is.False);
            Assert.That(SettingsOf(GraphicsProfile.High).supportSSR, Is.True);
            Assert.That(SettingsOf(GraphicsProfile.High).supportSSGI, Is.False);
            Assert.That(SettingsOf(GraphicsProfile.ExtraHigh).supportSSGI, Is.True);
            Assert.That(SettingsOf(GraphicsProfile.Low).supportVolumetrics, Is.False);
            Assert.That(SettingsOf(GraphicsProfile.Medium).supportVolumetrics, Is.True);
        }

        [Test]
        public void EveryProfileKeepsWater()
        {
            foreach (var profile in GraphicsProfiles.All)
            {
                Assert.That(SettingsOf(profile).supportWater, Is.True, profile.ToString());
            }
        }

        [UnityTest]
        public IEnumerator SwitchingThroughAllProfilesKeepsHdrpActive()
        {
            var start = QualitySettings.GetQualityLevel();
            foreach (var profile in GraphicsProfiles.All)
            {
                QualitySettings.SetQualityLevel((int)profile, true);
                yield return null;
                yield return null;
                Assert.That(GraphicsSettings.currentRenderPipeline, Is.InstanceOf<HDRenderPipelineAsset>(), profile.ToString());
                Assert.That(QualitySettings.GetQualityLevel(), Is.EqualTo((int)profile));
            }
            QualitySettings.SetQualityLevel(start, true);
            yield return null;
        }
    }
}
