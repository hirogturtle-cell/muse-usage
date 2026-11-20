using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LegsPreview.Tests
{
    /// <summary>
    /// Builds the scene if needed (editor prebuild step), enters Play mode, lets the cloth settle,
    /// then checks the simulation is sane under idle, walk and twirl motion.
    /// Run: Window ▸ General ▸ Test Runner ▸ PlayMode, or
    ///   Unity -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults playmode-results.xml
    /// </summary>
    [PrebuildSetup(typeof(EnsurePreviewScene))]
    public class LegsPreviewPlayModeTests
    {
        const string SceneName = "LegsPreview";

        SkirtCloth _skirt;
        ClothHealthMonitor _monitor;
        LegPoseAnimator _animator;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;
            _skirt = UnityEngine.Object.FindObjectOfType<SkirtCloth>();
            _monitor = UnityEngine.Object.FindObjectOfType<ClothHealthMonitor>();
            _animator = UnityEngine.Object.FindObjectOfType<LegPoseAnimator>();
            Assert.IsNotNull(_skirt, "SkirtCloth missing from scene");
            Assert.IsNotNull(_monitor, "ClothHealthMonitor missing from scene");
            Assert.IsNotNull(_animator, "LegPoseAnimator missing from scene");
        }

        static IEnumerator Wait(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) yield return null;
        }

        [UnityTest]
        public IEnumerator SceneHasCameraLightsAndMaterials()
        {
            var cam = Camera.main;
            Assert.IsNotNull(cam, "No MainCamera");
            var lights = UnityEngine.Object.FindObjectsOfType<Light>();
            Assert.GreaterOrEqual(lights.Length, 4, "Expected a key/fill/rim studio rig");
            Assert.IsTrue(Array.Exists(lights, l => l.shadows != LightShadows.None), "No shadow-casting light");
            foreach (var r in UnityEngine.Object.FindObjectsOfType<Renderer>())
                Assert.IsNotNull(r.sharedMaterial, r.name + " has no material");
            yield return null;
        }

        [UnityTest]
        public IEnumerator ClothIsConfiguredWithLegCapsules()
        {
            Cloth cloth = _skirt.Cloth;
            Assert.IsTrue(cloth.enabled);
            Assert.AreEqual(6, cloth.capsuleColliders.Length, "Expected 3 capsules per leg");
            foreach (var c in cloth.capsuleColliders) Assert.IsNotNull(c, "Null capsule collider on Cloth");
            Assert.That(cloth.stretchingStiffness, Is.InRange(0.85f, 1f));
            Assert.That(cloth.bendingStiffness, Is.InRange(0.01f, 0.7f));
            Assert.That(cloth.damping, Is.InRange(0.02f, 0.5f));
            yield return null; // Start() has run → coefficients applied
            Assert.AreEqual(cloth.vertices.Length, cloth.coefficients.Length, "Coefficient count must match cloth particles");
            int pinned = 0;
            foreach (var k in cloth.coefficients) if (k.maxDistance == 0f) pinned++;
            Assert.Greater(pinned, 50, "Waistband rows should be pinned");
        }

        [UnityTest]
        public IEnumerator ClothSettlesWithoutExplodingOrPenetratingLegs()
        {
            _animator.SetMode(LegPoseAnimator.Mode.Idle);
            yield return Wait(3.5f);
            _monitor.Sample();
            Debug.Log("[LegsPreview][Test] idle: " + _monitor.Summary);
            Assert.IsTrue(_monitor.Finite, "NaN/Inf in cloth particles");
            Assert.Greater(_monitor.MeanFreeDisplacement, 0.003f, "Skirt did not drape (gravity/solver not running?)");
            Assert.LessOrEqual(_monitor.MaxPinnedDrift, 0.01f, "Waist came loose");
            Assert.LessOrEqual(_monitor.MaxPenetration, 0.012f, "Skirt sinks into the legs");
            Assert.IsTrue(_monitor.Passed, _monitor.Summary);
        }

        [UnityTest]
        public IEnumerator ClothStaysStableWhileWalkingAndTwirling()
        {
            _animator.SetMode(LegPoseAnimator.Mode.WalkInPlace);
            yield return Wait(3f);
            _monitor.Sample();
            Debug.Log("[LegsPreview][Test] walk: " + _monitor.Summary);
            Assert.IsTrue(_monitor.Finite);
            Assert.LessOrEqual(_monitor.MaxPenetration, 0.02f, "Legs punch through the skirt while walking");

            _animator.SetMode(LegPoseAnimator.Mode.Twirl);
            yield return Wait(4f);
            _monitor.Sample();
            Debug.Log("[LegsPreview][Test] twirl: " + _monitor.Summary);
            Assert.IsTrue(_monitor.Finite);
            Assert.LessOrEqual(_monitor.MaxPinnedDrift, 0.01f, "Waist came loose while twirling");

            foreach (SkirtCloth.Fabric f in new[] { SkirtCloth.Fabric.SilkChiffon, SkirtCloth.Fabric.Denim })
            {
                _skirt.SetFabric(f);
                yield return Wait(1.5f);
                _monitor.Sample();
                Assert.IsTrue(_monitor.Finite, "Unstable with preset " + f);
            }
        }
    }

    /// <summary>Runs in the editor before Play-mode tests: generates the scene via the editor builder.</summary>
    public class EnsurePreviewScene : IPrebuildSetup
    {
        public void Setup()
        {
#if UNITY_EDITOR
            var t = Type.GetType("LegsPreview.EditorTools.LegsPreviewSceneBuilder, LegsPreview.Editor");
            Assert.IsNotNull(t, "LegsPreview.Editor assembly not found");
            t.GetMethod("EnsureScene").Invoke(null, null);
#endif
        }
    }
}
