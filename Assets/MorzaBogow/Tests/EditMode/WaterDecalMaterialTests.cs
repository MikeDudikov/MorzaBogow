using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MorzaBogow.Tests
{
    public sealed class WaterDecalMaterialTests
    {
        const string Folder = "Assets/Art/Placeholders";

        [TestCase("Placeholder_WaterBowWave", 2, 1, 0)]
        [TestCase("Placeholder_WaterFoamDisk", 0, 0, 1)]
        public void WaterDecal_UsesTheHdrpShapeAndAffectsTheRightBuffers(string name, int type, int deformation, int foam)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>($"{Folder}/{name}.mat");
            Assert.That(material, Is.Not.Null);
            // The sub-target tag reads as a fallback in batch mode, so the shader itself is checked; the build shows the effect.
            Assert.That(material.shader.name, Is.EqualTo("HDRP/Water/Water Decal Sample"));
            Assert.That(material.shader.isSupported, Is.True);
            Assert.That(UnityEditor.ShaderUtil.GetShaderMessages(material.shader), Is.Empty, "No compile errors.");
            Assert.That(material.GetFloat("_TYPE"), Is.EqualTo(type));
            Assert.That(material.GetFloat("_AffectDeformation"), Is.EqualTo(deformation));
            Assert.That(material.GetFloat("_AffectFoam"), Is.EqualTo(foam));
        }
    }
}
