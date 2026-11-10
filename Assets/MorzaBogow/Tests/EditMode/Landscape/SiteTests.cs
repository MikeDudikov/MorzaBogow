using System.Linq;
using MorzaBogow.Presentation;
using MorzaBogow.Presentation.Landscape;
using MorzaBogow.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace MorzaBogow.Tests
{
    /// <summary>The silver outcrop of the Laurion type and the gear of a beach shipyard (12th century BC).</summary>
    public sealed class SiteTests
    {
        static float Flat(float x, float z) => 1f;

        [Test]
        public void Ore_MarbleUnderSchistWithTheOreInTheContactBetween()
        {
            // A hillside rising 1.2 m for each metre inland, as at the foot of the islet's cliffs.
            float Slope(float x, float z) => 1f + 1.2f * Mathf.Max(0f, z - 6f);
            var made = OreOutcrop.Make(5, 30f, 6.4f, Slope);
            Assert.That(made.Marble.vertexCount > 0 && made.Schist.vertexCount > 0 && made.Contact.vertexCount > 0, Is.True);
            Assert.That(made.Marble.bounds.min.y, Is.LessThan(made.Contact.bounds.min.y), "Marble below the contact...");
            Assert.That(made.Schist.bounds.max.y, Is.GreaterThan(made.Contact.bounds.max.y), "...schist above it.");
            Assert.That(made.Marble.vertices.All(v => v.y >= Slope(v.x, v.z) + 0.1f), Is.True, "The outcrop lies on the hillside, not inside it.");
            Assert.That(made.Grains, Is.GreaterThan(20), "Galena shows as grains, not as a glittering face.");
            var contact = made.Contact.bounds;
            contact.Expand(1.2f);
            Assert.That(made.Galena.vertices.All(contact.Contains), Is.True, "The galena lies in the contact.");
            Assert.That(made.Pits, Is.InRange(2, 10), "A Bronze Age working: a few shallow pits, no galleries.");
            Assert.That(made.Workings.bounds.size.y, Is.LessThan(2.5f));
        }

        [Test]
        public void Ore_OnlyTheGalenaIsMetal()
        {
            var rock = new Material(Shader.Find("HDRP/Lit"));
            try
            {
                Assert.That(LandscapeArt.Galena(rock).GetFloat("_Metallic"), Is.EqualTo(1f), "Lead glance is metallic.");
                foreach (var material in new[] { LandscapeArt.Schist(rock), LandscapeArt.Limonite(rock), LandscapeArt.Cerussite(rock), rock })
                    Assert.That(material.GetFloat("_Metallic"), Is.EqualTo(0f), $"{material.name} is stone, not metal.");
                Assert.That(LandscapeArt.Cerussite(rock).GetColor("_BaseColor").grayscale, Is.GreaterThan(0.8f), "Cerussite is a white crust.");
            }
            finally
            {
                Object.DestroyImmediate(rock);
            }
        }

        [Test]
        public void Yard_GearStandsOnDryLandClearOfTheRampsAndIsBronzeInTheTwelfthCentury()
        {
            float Ground(float x, float z) => 2.4f - 0.05f * (x * x + z * z) / 20f;
            bool Clear(float x, float z) => Mathf.Abs(x - 6f) > 3.5f && Mathf.Abs(z) > 2.5f;
            var plan = ShipyardProps.Plan(9, 1f, Ground, Clear);
            Assert.That(plan.Select(item => item.Kind).Distinct().Count(), Is.EqualTo(6), "Logs, planks, a shelter, a tool rack, a pitch pot and flat stones.");
            Assert.That(plan.All(item => Clear(item.Position.x, item.Position.z) && Ground(item.Position.x, item.Position.z) >= 0.7f), Is.True, "On dry land, off the ramps and the jetty.");
            for (var i = 0; i < plan.Count; i++)
            for (var j = i + 1; j < plan.Count; j++)
                Assert.That((plan[i].Position - plan[j].Position).magnitude, Is.GreaterThan(plan[i].Radius + plan[j].Radius - 1e-3f), "No two pieces stand in each other.");
            Assert.That(ShipyardProps.ShelterEaves, Is.GreaterThanOrEqualTo(1.9f), "A man stands under the eaves.");
            Assert.That(EnvironmentProfile.Default.IronTools, Is.False, "The 12th century's tools are bronze.");
            var stone = new Material(Shader.Find("HDRP/Lit"));
            try
            {
                var bronze = ShipyardProps.ToolMetal(stone, false).GetColor("_BaseColor");
                var iron = ShipyardProps.ToolMetal(stone, true).GetColor("_BaseColor");
                Assert.That(bronze.r - bronze.b, Is.GreaterThan(0.2f), "Bronze is warm and coppery.");
                Assert.That(Mathf.Abs(iron.r - iron.b), Is.LessThan(0.05f), "Iron is grey.");
            }
            finally
            {
                Object.DestroyImmediate(stone);
            }
        }
    }
}
