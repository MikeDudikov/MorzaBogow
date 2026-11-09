using MorzaBogow.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace MorzaBogow.Tests
{
    public sealed class RingMarkerTests
    {
        [Test]
        public void Ring_MeshCarriesCoordinatesAcrossTheBandForSoftEdges()
        {
            var ring = RingMarker.Create("Test ring", null, null, 5f);
            try
            {
                var coordinates = ring.GetComponent<MeshFilter>().sharedMesh.uv;
                Assert.That(coordinates.Length, Is.EqualTo(ring.GetComponent<MeshFilter>().sharedMesh.vertexCount));
                Assert.That(coordinates[0].y, Is.EqualTo(0f), "Inner edge.");
                Assert.That(coordinates[1].y, Is.EqualTo(1f), "Outer edge.");
            }
            finally
            {
                Object.DestroyImmediate(ring.gameObject);
            }
        }
    }
}
