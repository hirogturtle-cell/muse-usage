using System;
using UnityEngine;

namespace LegsPreview
{
    /// <summary>
    /// Configures Unity's built-in <see cref="Cloth"/> for a short skirt: fabric presets with
    /// physically-motivated solver values, waistband pinning (skinning coefficients) and an
    /// optional studio-fan breeze.
    /// </summary>
    [RequireComponent(typeof(Cloth))]
    [DisallowMultipleComponent]
    public class SkirtCloth : MonoBehaviour
    {
        public enum Fabric { WoolBlendTwill, CottonPoplin, SilkChiffon, Denim, Custom }

        [Serializable]
        public struct ClothParams
        {
            [Tooltip("Resistance to stretching. Woven fabric barely stretches (0.9-1).")]
            [Range(0, 1)] public float stretchingStiffness;
            [Tooltip("Resistance to bending. Chiffon ~0.02, cotton ~0.12, wool ~0.25, denim ~0.6.")]
            [Range(0, 1)] public float bendingStiffness;
            [Tooltip("Velocity damping: air drag + internal fibre friction.")]
            [Range(0, 1)] public float damping;
            [Tooltip("Friction against colliders (fabric sliding over nylon stockings is fairly slick).")]
            [Range(0, 1)] public float friction;
            [Tooltip("How strongly character motion (world velocity) affects the cloth.")]
            [Range(0, 1)] public float worldVelocityScale;
            [Range(0, 1)] public float worldAccelerationScale;
            [Tooltip("Extra particle mass on collision; small values reduce jitter against legs.")]
            [Range(0, 1)] public float collisionMassScale;
            [Tooltip("Solver iterations per second. 240 keeps a short, light skirt stable against fast legs.")]
            public float solverFrequency;
            [Tooltip("0 = off, 1 = on. Virtual particles improve collision accuracy between vertices.")]
            [Range(0, 1)] public float useVirtualParticles;
            public float sleepThreshold;
        }

        public static ClothParams Preset(Fabric f)
        {
            switch (f)
            {
                case Fabric.CottonPoplin:
                    return new ClothParams { stretchingStiffness = 0.95f, bendingStiffness = 0.12f, damping = 0.12f, friction = 0.45f,
                        worldVelocityScale = 0.5f, worldAccelerationScale = 1f, collisionMassScale = 0.1f,
                        solverFrequency = 240f, useVirtualParticles = 1f, sleepThreshold = 0.001f };
                case Fabric.SilkChiffon:
                    return new ClothParams { stretchingStiffness = 0.90f, bendingStiffness = 0.02f, damping = 0.05f, friction = 0.25f,
                        worldVelocityScale = 0.6f, worldAccelerationScale = 1f, collisionMassScale = 0.05f,
                        solverFrequency = 300f, useVirtualParticles = 1f, sleepThreshold = 0.0005f };
                case Fabric.Denim:
                    return new ClothParams { stretchingStiffness = 0.99f, bendingStiffness = 0.60f, damping = 0.30f, friction = 0.60f,
                        worldVelocityScale = 0.4f, worldAccelerationScale = 1f, collisionMassScale = 0.15f,
                        solverFrequency = 200f, useVirtualParticles = 1f, sleepThreshold = 0.002f };
                default: // WoolBlendTwill — mid-weight pleated skirt fabric (~250 g/m²)
                    return new ClothParams { stretchingStiffness = 0.97f, bendingStiffness = 0.25f, damping = 0.18f, friction = 0.40f,
                        worldVelocityScale = 0.5f, worldAccelerationScale = 1f, collisionMassScale = 0.1f,
                        solverFrequency = 240f, useVirtualParticles = 1f, sleepThreshold = 0.001f };
            }
        }

        public Fabric fabric = Fabric.WoolBlendTwill;
        [Tooltip("Used when Fabric = Custom.")]
        public ClothParams custom = Preset(Fabric.WoolBlendTwill);

        [Header("Waistband pinning (local space of this object)")]
        [Tooltip("Particles at or above this height are fully pinned to the waistband.")]
        public float pinHeight = 0.995f;
        [Tooltip("Below the pin line, movement is limited over this distance so the sewn-down pleats stay tidy.")]
        public float pinBlend = 0.06f;
        [Tooltip("Max travel (m) at the bottom of the blend region.")]
        public float pinBlendMaxDistance = 0.04f;

        [Header("Studio fan")]
        public bool fanOn;
        public Vector3 fanAcceleration = new Vector3(0.4f, 0.1f, 1.2f);
        public Vector3 fanTurbulence = new Vector3(1.0f, 0.4f, 1.0f);

        Cloth _cloth;
        public Cloth Cloth => _cloth != null ? _cloth : (_cloth = GetComponent<Cloth>());

        public ClothParams Current => fabric == Fabric.Custom ? custom : Preset(fabric);

        void Start()
        {
            ApplyParameters();
            ApplyCoefficients();
            Cloth.ClearTransformMotion();
        }

        void OnValidate()
        {
            if (isActiveAndEnabled) ApplyParameters();
        }

        public void SetFabric(Fabric f)
        {
            fabric = f;
            ApplyParameters();
        }

        public void SetFan(bool on)
        {
            fanOn = on;
            ApplyParameters();
        }

        public void ApplyParameters()
        {
            var c = Cloth;
            if (c == null) return;
            var p = Current;
            c.stretchingStiffness = p.stretchingStiffness;
            c.bendingStiffness = p.bendingStiffness;
            c.damping = p.damping;
            c.friction = p.friction;
            c.worldVelocityScale = p.worldVelocityScale;
            c.worldAccelerationScale = p.worldAccelerationScale;
            c.collisionMassScale = p.collisionMassScale;
            c.clothSolverFrequency = p.solverFrequency;
            c.useVirtualParticles = p.useVirtualParticles;
            c.sleepThreshold = p.sleepThreshold;
            c.useGravity = true;
            c.useTethers = true; // long-range constraints: stops the hem "rubber-banding" when spinning
            c.externalAcceleration = fanOn ? fanAcceleration : Vector3.zero;
            c.randomAcceleration = fanOn ? fanTurbulence : Vector3.zero;
        }

        /// <summary>Pins the waist rows and frees the rest. Safe to call repeatedly.</summary>
        public bool ApplyCoefficients()
        {
            var c = Cloth;
            if (c == null) return false;
            Vector3[] verts = c.vertices;
            if (verts == null || verts.Length == 0) return false;

            var coeffs = new ClothSkinningCoefficient[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                coeffs[i].maxDistance = MaxDistanceFor(verts[i].y);
                coeffs[i].collisionSphereDistance = float.MaxValue; // no backstop
            }
            c.coefficients = coeffs;
            return true;
        }

        public float MaxDistanceFor(float localY)
        {
            if (localY >= pinHeight) return 0f;
            float t = (pinHeight - localY) / Mathf.Max(pinBlend, 1e-4f);
            if (t < 1f) return Mathf.Lerp(0f, pinBlendMaxDistance, t * t);
            return float.MaxValue; // unconstrained
        }

        public bool IsPinned(float localY) => localY >= pinHeight;
    }
}
