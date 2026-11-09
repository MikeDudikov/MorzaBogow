using System.Linq;
using MorzaBogow.Settings;
using NUnit.Framework;

namespace MorzaBogow.Tests
{
    public sealed class GraphicsProfilesTests
    {
        [Test]
        public void All_ContainsFiveProfilesFromLowToUltra()
        {
            Assert.That(GraphicsProfiles.All.Select(GraphicsProfiles.DisplayName),
                Is.EqualTo(new[] { "Low", "Medium", "High", "Extra High", "Ultra" }));
        }

        [Test]
        public void Default_IsUltra()
        {
            Assert.That(GraphicsProfiles.Default, Is.EqualTo(GraphicsProfile.Ultra));
        }

        [Test]
        public void RayTracing_IsRequiredOnlyByUltra()
        {
            Assert.That(GraphicsProfiles.All.Where(GraphicsProfiles.RequiresRayTracing), Is.EqualTo(new[] { GraphicsProfile.Ultra }));
        }

        [TestCase("Extra High", GraphicsProfile.ExtraHigh)]
        [TestCase("extrahigh", GraphicsProfile.ExtraHigh)]
        [TestCase("Low", GraphicsProfile.Low)]
        [TestCase("nieznany", GraphicsProfile.Ultra)]
        [TestCase("", GraphicsProfile.Ultra)]
        [TestCase(null, GraphicsProfile.Ultra)]
        public void ParseOrDefault_ReadsNamesAndKeepsDefaultForUnknown(string value, GraphicsProfile expected)
        {
            Assert.That(GraphicsProfiles.ParseOrDefault(value), Is.EqualTo(expected));
        }
    }
}
