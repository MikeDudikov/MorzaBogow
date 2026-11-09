using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MorzaBogow.Tests
{
    public sealed class RingMaterialTests
    {
        const string Folder = "Assets/Art/Placeholders";

        [Test]
        public void SelectionRing_IsSemiTransparentAndUnlit()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>($"{Folder}/Placeholder_Selection.mat");
            Assert.That(material, Is.Not.Null);
            Assert.That(material.shader.name, Is.EqualTo("HDRP/Unlit"));
            Assert.That(material.GetFloat("_SurfaceType"), Is.EqualTo(1f), "Transparent surface.");
            Assert.That(material.GetColor("_UnlitColor").a, Is.InRange(0.3f, 0.7f));
        }

        [Test]
        public void RingEdgeTexture_FadesAtBothEdgesAndIsSolidInTheMiddle()
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Folder}/Placeholder_RingEdge.asset");
            Assert.That(texture, Is.Not.Null);
            Assert.That(texture.GetPixel(0, 0).a, Is.EqualTo(0f).Within(0.01f));
            Assert.That(texture.GetPixel(0, texture.height - 1).a, Is.EqualTo(0f).Within(0.01f));
            Assert.That(texture.GetPixel(0, texture.height / 2).a, Is.EqualTo(1f).Within(0.01f));
        }
    }
}
