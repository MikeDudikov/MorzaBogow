using System;
using System.Linq;
using MorzaBogow.Presentation;
using MorzaBogow.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace MorzaBogow.Tests
{
    /// <summary>The Aegean ships of the 12th century BC built for every class: sizes from the simulation, types kept apart, sound meshes.</summary>
    public sealed class GreekShipsTests
    {
        static ShipClass[] Classes => Enum.GetValues(typeof(ShipClass)).Cast<ShipClass>().ToArray();

        static int Triangles(GreekMesh mesh) => Enumerable.Range(0, mesh.Mesh.subMeshCount).Sum(i => (int)mesh.Mesh.GetIndexCount(i) / 3);

        [Test]
        public void Ships_EveryClassHasItsCatalogueSizeKeelAndPosts()
        {
            foreach (var shipClass in Classes)
            {
                var definition = ShipCatalog.Get(shipClass);
                var parts = GreekShips.Parts(shipClass);
                var length = (float)definition.Radius * 2f;
                var bounds = parts.Hull.Mesh.bounds;
                if (parts.Ram != null) bounds.Encapsulate(parts.Ram.Mesh.bounds);
                Assert.That(bounds.min.z, Is.EqualTo(-length * 0.5f).Within(length * 0.01f), $"{shipClass}: stern");
                Assert.That(bounds.max.z, Is.EqualTo(length * 0.5f).Within(length * 0.02f), $"{shipClass}: bow");
                Assert.That(parts.Hull.Mesh.bounds.min.y, Is.EqualTo(-(float)definition.Draft).Within((float)definition.Draft * 0.02f), $"{shipClass}: keel at the draft");
                Assert.That(parts.Hull.Mesh.bounds.size.x, Is.LessThanOrEqualTo((float)definition.Beam * 1.02f), $"{shipClass}: no wider than its beam");
                Assert.That(parts.Ram != null, Is.EqualTo(definition.CanRam), $"{shipClass}: a spur only on a ship that rams");
                Assert.That(parts.Mounts.Length, Is.EqualTo(definition.Slots.Length), $"{shipClass}: a stand for every weapon post");
                var reach = (float)Rules.OarReach(definition);
                for (var i = 0; i < parts.OarPins.Length; i++)
                {
                    var tip = parts.OarPins[i].x + parts.OarLength[parts.OarBank[i]] * Mathf.Cos(parts.OarTilt[i] * Mathf.Deg2Rad);
                    Assert.That(tip, Is.InRange((float)definition.Beam * 0.5f + reach * 0.75f, (float)definition.Beam * 0.5f + reach * 1.01f), $"{shipClass}: oars reach as far as the simulation keeps room for");
                }
            }
        }

        [Test]
        public void Ships_EachTypeKeepsItsOwnMarks()
        {
            foreach (var shipClass in Classes)
            {
                var form = GreekShips.Form(shipClass);
                Assert.That(form.Banks, Is.EqualTo(1), $"{shipClass}: one bank of oars, as in the 12th century BC");
                Assert.That(form.Castles, Is.EqualTo(shipClass == ShipClass.Bomber), shipClass.ToString());
                Assert.That(form.Lattice > 0f, Is.EqualTo(shipClass == ShipClass.Boarder), shipClass.ToString());
                Assert.That(form.Cargo && form.Fence > 0f, Is.EqualTo(shipClass == ShipClass.Merchant), shipClass.ToString());
                Assert.That(form.CrowsNest, Is.EqualTo(shipClass != ShipClass.Cutter && shipClass != ShipClass.Merchant), shipClass.ToString());
                Assert.That(form.TypeName, Is.Not.Empty);
            }
            Assert.That(Classes.Select(c => GreekShips.Form(c).TypeName).Distinct().Count(), Is.EqualTo(Classes.Length), "Every class is its own type.");
        }

        [Test]
        public void Ships_PiecesOfABrokenHullMakeUpTheWhole()
        {
            foreach (var shipClass in Classes)
            {
                var parts = GreekShips.Parts(shipClass);
                var whole = Triangles(parts.Hull) + Triangles(parts.Deck);
                var pieces = parts.Sections.Sum(Triangles);
                Assert.That(pieces - whole, Is.InRange(0, 60), $"{shipClass}: every face in one piece, plus the ragged ends");
                Assert.That(parts.Sections.Length, Is.EqualTo(3));
            }
        }

        [Test]
        public void Ships_HullFacesPointOutward()
        {
            foreach (var shipClass in Classes)
            {
                var parts = GreekShips.Parts(shipClass);
                var form = parts.Form;
                var mesh = parts.Hull.Mesh;
                var vertices = mesh.vertices;
                var triangles = mesh.GetTriangles(0);
                int outward = 0, counted = 0;
                for (var i = 0; i < triangles.Length; i += 3)
                {
                    var a = vertices[triangles[i]];
                    var b = vertices[triangles[i + 1]];
                    var c = vertices[triangles[i + 2]];
                    var centre = (a + b + c) / 3f;
                    var s = Mathf.InverseLerp(GreekShips.Z(form, 0f), GreekShips.Z(form, 1f), centre.z);
                    if (s < 0.1f || s > 0.9f || Mathf.Abs(centre.x) < GreekShips.HalfWidth(form, s) * 0.3f || centre.y > GreekShips.Sheer(form, s)) continue;
                    counted++;
                    var axis = new Vector3(0f, (GreekShips.Sheer(form, s) - GreekShips.Depth(form, s)) * 0.5f, centre.z);
                    if (Vector3.Dot(Vector3.Cross(b - a, c - a), centre - axis) > 0f) outward++;
                }
                Assert.That(counted, Is.GreaterThan(50), shipClass.ToString());
                Assert.That(outward / (float)counted, Is.GreaterThan(0.97f), $"{shipClass}: the planking faces the sea");
            }
        }

        [Test]
        public void Ships_LowerDetailHasFewerTriangles()
        {
            foreach (var shipClass in Classes)
            {
                var counts = Enumerable.Range(0, GreekShips.Details).Select(d => Triangles(GreekShips.Parts(shipClass, d).Hull) + Triangles(GreekShips.Parts(shipClass, d).Deck)).ToArray();
                Assert.That(counts[0], Is.GreaterThan(counts[1]), shipClass.ToString());
                Assert.That(counts[1], Is.GreaterThan(counts[2]), shipClass.ToString());
            }
        }
    }
}
