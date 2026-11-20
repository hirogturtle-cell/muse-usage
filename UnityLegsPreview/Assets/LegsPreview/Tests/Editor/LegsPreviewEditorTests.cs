using System.Linq;
using LegsPreview.EditorTools;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LegsPreview.Tests
{
    /// <summary>Static checks on the generated scene and meshes (no simulation).</summary>
    public class LegsPreviewEditorTests
    {
        [OneTimeSetUp]
        public void BuildScene()
        {
            LegsPreviewSceneBuilder.Build(openAfter: false);
            EditorSceneManager.OpenScene(LegsPreviewSceneBuilder.ScenePath);
        }

        [Test]
        public void LegsAreSkinnedToThighAndShin()
        {
            var legs = Object.FindObjectsOfType<SkinnedMeshRenderer>().Where(r => r.name.StartsWith("Leg_")).ToArray();
            Assert.AreEqual(2, legs.Length);
            foreach (var leg in legs)
            {
                Assert.AreEqual(2, leg.bones.Length);
                Assert.AreEqual(2, leg.sharedMesh.bindposes.Length);
                Assert.AreEqual(leg.sharedMesh.vertexCount, leg.sharedMesh.boneWeights.Length);
                Assert.Greater(leg.sharedMesh.vertexCount, 10000, "leg mesh should be dense enough for smooth silhouettes");
                Assert.IsTrue(leg.sharedMesh.normals.All(n => Mathf.Abs(n.sqrMagnitude - 1f) < 0.01f), "bad normals on " + leg.name);
            }
        }

        [Test]
        public void LegNormalsPointOutwards()
        {
            foreach (bool left in new[] { true, false })
            {
                var m = LegGeometry.BuildLeg(left, 60, 32);
                var v = m.vertices; var n = m.normals;
                int outward = 0, legVerts = 61 * 33;
                for (int i = 0; i < legVerts; i++)
                {
                    Vector3 axis = LegGeometry.LegAxis(v[i].y, left);
                    if (Vector3.Dot(n[i], new Vector3(v[i].x - axis.x, 0, v[i].z - axis.z)) > 0) outward++;
                }
                Assert.Greater(outward, legVerts * 0.97f, (left ? "left" : "right") + " leg has inverted faces");
                Object.DestroyImmediate(m);
            }
        }

        [Test]
        public void ClothHasCapsulesOnBothLegsAndHipPair()
        {
            var cloth = Object.FindObjectOfType<Cloth>();
            Assert.IsNotNull(cloth);
            Assert.AreEqual(6, cloth.capsuleColliders.Length);
            Assert.AreEqual(3, cloth.capsuleColliders.Count(c => c.name.EndsWith("_L")));
            Assert.AreEqual(3, cloth.capsuleColliders.Count(c => c.name.EndsWith("_R")));
            Assert.AreEqual(1, cloth.sphereColliders.Length);
            Assert.IsNotNull(cloth.sphereColliders[0].first);
            Assert.IsNotNull(cloth.sphereColliders[0].second);
        }

        [Test]
        public void CapsulesHugTheLegSurface()
        {
            var cloth = Object.FindObjectOfType<Cloth>();
            foreach (var c in cloth.capsuleColliders)
            {
                Assert.That(c.radius, Is.InRange(0.03f, 0.11f), c.name);
                Assert.Greater(c.height, 2f * c.radius, c.name);
            }
        }

        [Test]
        public void PresetsAreInRealisticRanges()
        {
            foreach (SkirtCloth.Fabric f in System.Enum.GetValues(typeof(SkirtCloth.Fabric)))
            {
                if (f == SkirtCloth.Fabric.Custom) continue;
                var p = SkirtCloth.Preset(f);
                Assert.That(p.stretchingStiffness, Is.InRange(0.85f, 1f), f + " stretch");
                Assert.That(p.bendingStiffness, Is.InRange(0.01f, 0.7f), f + " bend");
                Assert.That(p.damping, Is.InRange(0.02f, 0.5f), f + " damping");
                Assert.That(p.solverFrequency, Is.InRange(120f, 400f), f + " solver");
            }
            Assert.Less(SkirtCloth.Preset(SkirtCloth.Fabric.SilkChiffon).bendingStiffness,
                        SkirtCloth.Preset(SkirtCloth.Fabric.Denim).bendingStiffness);
        }

        [Test]
        public void MaterialsUseIntendedShaders()
        {
            var legs = Object.FindObjectsOfType<SkinnedMeshRenderer>().Where(r => r.name.StartsWith("Leg_"));
            foreach (var l in legs) Assert.IsNotNull(l.sharedMaterial);
            var skirt = Object.FindObjectOfType<Cloth>().GetComponent<SkinnedMeshRenderer>();
            Assert.IsNotNull(skirt.sharedMaterial);
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null)
            {
                foreach (var l in legs) Assert.AreEqual("LegsPreview/SheerStocking", l.sharedMaterial.shader.name);
                Assert.AreEqual("LegsPreview/WovenFabric", skirt.sharedMaterial.shader.name);
            }
        }
    }
}
