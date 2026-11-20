using UnityEngine;

namespace LegsPreview
{
    /// <summary>Geometry checks shared by the in-game health monitor and the automated tests.</summary>
    public static class ClothDiagnostics
    {
        public static void CapsuleSegment(CapsuleCollider c, out Vector3 a, out Vector3 b, out float radius)
        {
            Transform t = c.transform;
            Vector3 axis = c.direction == 0 ? Vector3.right : c.direction == 1 ? Vector3.up : Vector3.forward;
            Vector3 s = t.lossyScale;
            float axisScale = Mathf.Abs(c.direction == 0 ? s.x : c.direction == 1 ? s.y : s.z);
            float radScale = c.direction == 0 ? Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z))
                           : c.direction == 1 ? Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.z))
                           : Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y));
            radius = c.radius * radScale;
            float half = Mathf.Max(0f, c.height * axisScale * 0.5f - radius);
            Vector3 center = t.TransformPoint(c.center);
            Vector3 dir = t.TransformDirection(axis).normalized;
            a = center - dir * half;
            b = center + dir * half;
        }

        public static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float len2 = ab.sqrMagnitude;
            float t = len2 > 1e-12f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / len2) : 0f;
            return Vector3.Distance(p, a + ab * t);
        }

        /// <summary>Deepest penetration (m) of any cloth particle into any of the cloth's capsule colliders.</summary>
        public static float MaxCapsulePenetration(Cloth cloth, out int worstParticle)
        {
            worstParticle = -1;
            float worst = 0f;
            var caps = cloth.capsuleColliders;
            if (caps == null || caps.Length == 0) return 0f;
            Vector3[] verts = cloth.vertices;
            Transform ct = cloth.transform;

            var segA = new Vector3[caps.Length];
            var segB = new Vector3[caps.Length];
            var rad = new float[caps.Length];
            for (int k = 0; k < caps.Length; k++)
                if (caps[k] != null) CapsuleSegment(caps[k], out segA[k], out segB[k], out rad[k]);

            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 p = ct.TransformPoint(verts[i]);
                for (int k = 0; k < caps.Length; k++)
                {
                    if (caps[k] == null) continue;
                    float pen = rad[k] - DistanceToSegment(p, segA[k], segB[k]);
                    if (pen > worst) { worst = pen; worstParticle = i; }
                }
            }
            return worst;
        }

        public static bool AllFinite(Vector3[] v)
        {
            foreach (var p in v)
                if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z) ||
                    float.IsInfinity(p.x) || float.IsInfinity(p.y) || float.IsInfinity(p.z))
                    return false;
            return true;
        }
    }
}
