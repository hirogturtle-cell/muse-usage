using UnityEngine;

namespace LegsPreview
{
    /// <summary>
    /// Watches the skirt simulation in Play mode and logs a one-line PASS/FAIL report after it has
    /// settled: particles finite, waist stays pinned, cloth actually draped, no deep leg penetration.
    /// The same numbers drive the HUD and the automated Play-mode test.
    /// </summary>
    [RequireComponent(typeof(SkirtCloth))]
    public class ClothHealthMonitor : MonoBehaviour
    {
        public float reportAfterSeconds = 3f;
        public float penetrationTolerance = 0.012f;
        public float pinnedDriftTolerance = 0.01f;

        public int ParticleCount { get; private set; }
        public int PinnedCount { get; private set; }
        public bool Finite { get; private set; } = true;
        public float MaxPenetration { get; private set; }
        public float MaxPinnedDrift { get; private set; }
        public float MeanFreeDisplacement { get; private set; }
        public bool Reported { get; private set; }
        public bool Passed { get; private set; }
        public string Summary { get; private set; } = "waiting for simulation…";

        SkirtCloth _skirt;
        Vector3[] _rest;
        float _elapsed, _nextSample;

        void Start()
        {
            _skirt = GetComponent<SkirtCloth>();
            _rest = _skirt.Cloth.vertices;
        }

        void LateUpdate()
        {
            _elapsed += Time.deltaTime;
            if (_elapsed < _nextSample) return;
            _nextSample = _elapsed + 0.25f;
            Sample();
            if (!Reported && _elapsed >= reportAfterSeconds)
            {
                Reported = true;
                if (Passed) Debug.Log("[LegsPreview] Play-mode cloth check PASSED — " + Summary);
                else Debug.LogWarning("[LegsPreview] Play-mode cloth check FAILED — " + Summary);
            }
        }

        public void Sample()
        {
            Cloth cloth = _skirt.Cloth;
            Vector3[] v = cloth.vertices;
            ParticleCount = v.Length;
            Finite = ClothDiagnostics.AllFinite(v);
            MaxPenetration = ClothDiagnostics.MaxCapsulePenetration(cloth, out _);

            int pinned = 0, free = 0;
            float drift = 0f, disp = 0f;
            if (_rest != null && _rest.Length == v.Length)
            {
                for (int i = 0; i < v.Length; i++)
                {
                    float d = Vector3.Distance(v[i], _rest[i]);
                    if (_skirt.IsPinned(_rest[i].y)) { pinned++; drift = Mathf.Max(drift, d); }
                    else { free++; disp += d; }
                }
            }
            PinnedCount = pinned;
            MaxPinnedDrift = drift;
            MeanFreeDisplacement = free > 0 ? disp / free : 0f;

            bool draped = MeanFreeDisplacement > 0.003f;
            Passed = Finite && ParticleCount > 0 && pinned > 0 && draped &&
                     MaxPenetration <= penetrationTolerance && MaxPinnedDrift <= pinnedDriftTolerance;
            Summary = string.Format(
                "{0} particles ({1} pinned), finite={2}, waist drift={3:0.0} mm, drape={4:0.0} mm, max leg penetration={5:0.0} mm, fabric={6}",
                ParticleCount, pinned, Finite, MaxPinnedDrift * 1000f, MeanFreeDisplacement * 1000f, MaxPenetration * 1000f, _skirt.fabric);
        }
    }
}
