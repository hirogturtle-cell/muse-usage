using System;
using System.Collections.Generic;
using UnityEngine;

namespace LegsPreview
{
    /// <summary>
    /// Procedural anatomy and garment meshes for the preview scene.
    /// Units are metres, +Y is up, the figure faces -Z (towards the default camera).
    /// Proportions follow an adult female of ~1.68 m: crotch ≈ 0.80 m, knee centre 0.47 m,
    /// ankle 0.075 m, calf ≈ 34 cm, knee ≈ 33 cm and upper thigh ≈ 54 cm circumference.
    /// </summary>
    public static class LegGeometry
    {
        public const float LegBottom = 0.035f;
        public const float LegTop = 0.95f;
        public const float AnkleHeight = 0.075f;
        public const float KneeHeight = 0.47f;
        public const float HipJointHeight = 0.88f;

        public const float WaistHeight = 1.02f;
        public const float SkirtHemHeight = 0.64f;

        const float TwoPi = Mathf.PI * 2f;

        // ---------------------------------------------------------------- leg profile
        // Cross-sections are ellipses (a = lateral half-width, b = front/back half-depth)
        // around a centre line, plus Gaussian "muscle/bone" bumps. All values are for the
        // RIGHT leg (x > 0); the left leg is mirrored.

        static readonly float[] ProfileY = { 0.035f, 0.075f, 0.12f, 0.20f, 0.28f, 0.34f, 0.40f, 0.445f, 0.47f, 0.52f, 0.60f, 0.68f, 0.76f, 0.84f, 0.95f };
        static readonly float[] ProfileA = { 0.030f, 0.032f, 0.029f, 0.036f, 0.046f, 0.052f, 0.050f, 0.046f, 0.051f, 0.055f, 0.063f, 0.071f, 0.079f, 0.086f, 0.089f };
        static readonly float[] ProfileB = { 0.040f, 0.035f, 0.033f, 0.042f, 0.051f, 0.056f, 0.053f, 0.047f, 0.049f, 0.053f, 0.061f, 0.069f, 0.077f, 0.084f, 0.087f };

        static readonly float[] CenterY = { 0.035f, 0.075f, 0.34f, 0.47f, 0.70f, 0.84f, 0.95f };
        static readonly float[] CenterXs = { 0.068f, 0.068f, 0.070f, 0.072f, 0.084f, 0.095f, 0.092f };
        static readonly float[] CenterZs = { 0.004f, 0.004f, 0.012f, 0.000f, -0.004f, 0.002f, 0.006f };

        // Angles (radians) in the right-leg frame: 0 = lateral (+X), +π/2 = back (+Z),
        // -π/2 = front (-Z), π = medial (-X).
        struct Bump
        {
            public float theta, y, sTheta, sY, amp;
            public Bump(float theta, float y, float sTheta, float sY, float amp)
            { this.theta = theta; this.y = y; this.sTheta = sTheta; this.sY = sY; this.amp = amp; }
        }

        static readonly Bump[] Bumps =
        {
            new Bump( 2.00f, 0.360f, 0.60f, 0.060f,  0.010f), // gastrocnemius, medial head (lower, fuller)
            new Bump( 1.20f, 0.385f, 0.55f, 0.055f,  0.007f), // gastrocnemius, lateral head
            new Bump(-1.57f, 0.250f, 0.50f, 0.100f, -0.004f), // flat anterior tibia
            new Bump(-1.90f, 0.260f, 0.18f, 0.120f,  0.002f), // tibial crest
            new Bump( 0.97f, 0.150f, 0.30f, 0.030f, -0.005f), // hollow beside Achilles (lateral)
            new Bump( 2.17f, 0.150f, 0.30f, 0.030f, -0.005f), // hollow beside Achilles (medial)
            new Bump( 0.00f, 0.070f, 0.40f, 0.015f,  0.006f), // lateral malleolus (lower)
            new Bump( 3.14f, 0.085f, 0.40f, 0.015f,  0.006f), // medial malleolus (higher)
            new Bump(-1.57f, 0.475f, 0.45f, 0.030f,  0.010f), // patella
            new Bump( 1.57f, 0.465f, 0.60f, 0.030f, -0.006f), // popliteal fossa
            new Bump( 2.55f, 0.500f, 0.50f, 0.030f,  0.006f), // medial knee fat pad
            new Bump(-2.20f, 0.550f, 0.45f, 0.040f,  0.007f), // vastus medialis "teardrop"
            new Bump( 3.14f, 0.750f, 0.80f, 0.100f,  0.006f), // inner thigh fullness
            new Bump( 0.00f, 0.800f, 0.80f, 0.080f,  0.004f), // outer thigh
            new Bump( 1.57f, 0.820f, 0.90f, 0.060f,  0.006f), // gluteal fold / hamstrings
        };

        static AnimationCurve s_A, s_B, s_Cx, s_Cz;

        static AnimationCurve Smooth(float[] xs, float[] ys)
        {
            var c = new AnimationCurve();
            for (int i = 0; i < xs.Length; i++) c.AddKey(new Keyframe(xs[i], ys[i]));
            for (int i = 0; i < xs.Length; i++) c.SmoothTangents(i, 0f);
            return c;
        }

        static void EnsureCurves()
        {
            if (s_A != null) return;
            s_A = Smooth(ProfileY, ProfileA);
            s_B = Smooth(ProfileY, ProfileB);
            s_Cx = Smooth(CenterY, CenterXs);
            s_Cz = Smooth(CenterY, CenterZs);
        }

        static float WrapAngle(float a)
        {
            a = Mathf.Repeat(a + Mathf.PI, TwoPi) - Mathf.PI;
            return a;
        }

        static float EllipseRadius(float a, float b, float theta)
        {
            float c = Mathf.Cos(theta), s = Mathf.Sin(theta);
            return a * b / Mathf.Sqrt(b * b * c * c + a * a * s * s);
        }

        /// <summary>Centre line of the leg at height y (right leg; mirror X for the left).</summary>
        public static Vector3 LegAxis(float y, bool left)
        {
            EnsureCurves();
            float x = s_Cx.Evaluate(y);
            return new Vector3(left ? -x : x, y, s_Cz.Evaluate(y));
        }

        /// <summary>Largest radius of the leg surface at height y.</summary>
        public static float MaxRadiusAt(float y)
        {
            EnsureCurves();
            float r = 0f;
            for (int i = 0; i < 64; i++) r = Mathf.Max(r, LegRadius(i / 64f * TwoPi, y));
            return r;
        }

        static float LegRadius(float theta, float y)
        {
            float r = EllipseRadius(s_A.Evaluate(y), s_B.Evaluate(y), theta);
            foreach (var b in Bumps)
            {
                float dy = (y - b.y) / b.sY;
                float dt = WrapAngle(theta - b.theta) / b.sTheta;
                r += b.amp * Mathf.Exp(-(dy * dy + dt * dt));
            }
            return r;
        }

        public static Vector3 HipJoint(bool left) => new Vector3(left ? -0.088f : 0.088f, HipJointHeight, 0.0f);
        public static Vector3 Knee(bool left) => LegAxis(KneeHeight, left);
        public static Vector3 Ankle(bool left) => LegAxis(AnkleHeight, left);

        // ---------------------------------------------------------------- mesh helpers

        class MeshData
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int> t = new List<int>();

            public void Quad(int a, int b, int c, int d)
            {
                // a = bottom-left, b = bottom-right, c = top-left, d = top-right (seen from the front).
                t.Add(a); t.Add(c); t.Add(d);
                t.Add(a); t.Add(d); t.Add(b);
            }

            /// <summary>Flips all triangles if most faces point against <paramref name="desired"/>.</summary>
            public void OrientFaces(Func<Vector3, Vector3> desired)
            {
                double score = 0;
                for (int i = 0; i < t.Count; i += 3)
                {
                    Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                    Vector3 n = Vector3.Cross(b - a, c - a);
                    score += Vector3.Dot(n, desired((a + b + c) / 3f));
                }
                if (score >= 0) return;
                for (int i = 0; i < t.Count; i += 3)
                {
                    int tmp = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = tmp;
                }
            }

            public Vector3 Centroid()
            {
                Vector3 s = Vector3.zero;
                foreach (var p in v) s += p;
                return v.Count > 0 ? s / v.Count : s;
            }

            public Mesh ToMesh(string name)
            {
                var m = new Mesh { name = name };
                if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                m.SetVertices(v);
                m.SetUVs(0, uv);
                m.SetTriangles(t, 0);
                m.RecalculateNormals();
                return m;
            }
        }

        /// <summary>Averages normals of the duplicated seam column in a ring grid.</summary>
        static void WeldSeamNormals(Vector3[] normals, int ringStart, int rings, int vertsPerRing)
        {
            for (int j = 0; j < rings; j++)
            {
                int i0 = ringStart + j * vertsPerRing, i1 = i0 + vertsPerRing - 1;
                Vector3 n = (normals[i0] + normals[i1]).normalized;
                normals[i0] = normals[i1] = n;
            }
        }

        static float SmoothStep01(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        // ---------------------------------------------------------------- leg + foot

        /// <summary>
        /// Builds one leg (ankle to upper thigh) plus the stockinged foot as a single mesh,
        /// skinned to two bones: 0 = thigh, 1 = shin (foot is rigid on the shin).
        /// UV.y is normalised height along the leg (used by the stocking shader for the thigh band).
        /// Bind poses are NOT set here; the caller sets them once the bones exist.
        /// </summary>
        public static Mesh BuildLeg(bool left, int rings = 220, int segments = 88)
        {
            EnsureCurves();
            var leg = new MeshData();
            int vpr = segments + 1;
            for (int j = 0; j <= rings; j++)
            {
                float y = Mathf.Lerp(LegBottom, LegTop, j / (float)rings);
                float cx = s_Cx.Evaluate(y), cz = s_Cz.Evaluate(y);
                for (int i = 0; i <= segments; i++)
                {
                    float theta = i / (float)segments * TwoPi;
                    float r = LegRadius(theta, y);
                    float x = cx + r * Mathf.Cos(theta);
                    float z = cz + r * Mathf.Sin(theta);
                    leg.v.Add(new Vector3(left ? -x : x, y, z));
                    leg.uv.Add(new Vector2(i / (float)segments, (y - LegBottom) / (LegTop - LegBottom)));
                }
            }
            for (int j = 0; j < rings; j++)
                for (int i = 0; i < segments; i++)
                {
                    int a = j * vpr + i;
                    leg.Quad(a, a + 1, a + vpr, a + vpr + 1);
                }
            leg.OrientFaces(c => new Vector3(c.x - (left ? -1 : 1) * s_Cx.Evaluate(c.y), 0, c.z - s_Cz.Evaluate(c.y)));
            Mesh legMesh = leg.ToMesh("tmp");
            var legNormals = legMesh.normals;
            WeldSeamNormals(legNormals, 0, rings + 1, vpr);
            UnityEngine.Object.DestroyImmediate(legMesh);

            var foot = BuildFootData(left);
            Mesh footMesh = foot.ToMesh("tmp");
            var footNormals = footMesh.normals;
            UnityEngine.Object.DestroyImmediate(footMesh);

            // merge
            int legCount = leg.v.Count;
            var verts = new List<Vector3>(leg.v); verts.AddRange(foot.v);
            var uvs = new List<Vector2>(leg.uv); uvs.AddRange(foot.uv);
            var normals = new List<Vector3>(legNormals); normals.AddRange(footNormals);
            var tris = new List<int>(leg.t);
            foreach (int idx in foot.t) tris.Add(idx + legCount);

            var weights = new BoneWeight[verts.Count];
            for (int k = 0; k < verts.Count; k++)
            {
                // Knee blend region ±4.5 cm gives a soft, believable bend instead of a hinge crease.
                float thigh = k < legCount ? SmoothStep01(KneeHeight - 0.045f, KneeHeight + 0.045f, verts[k].y) : 0f;
                float shin = 1f - thigh;
                var w = new BoneWeight();
                if (thigh >= shin) { w.boneIndex0 = 0; w.weight0 = thigh; w.boneIndex1 = 1; w.weight1 = shin; }
                else { w.boneIndex0 = 1; w.weight0 = shin; w.boneIndex1 = 0; w.weight1 = thigh; }
                weights[k] = w;
            }

            var mesh = new Mesh { name = left ? "Leg_L" : "Leg_R" };
            if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.boneWeights = weights;
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        static MeshData BuildFootData(bool left)
        {
            const int sections = 56, segs = 44;
            const float length = 0.235f, toeOut = 7f * Mathf.Deg2Rad;
            float[] ks = { 0.00f, 0.05f, 0.15f, 0.30f, 0.50f, 0.68f, 0.82f, 0.92f, 1.00f };
            float[] kw = { 0.018f, 0.029f, 0.032f, 0.036f, 0.040f, 0.046f, 0.045f, 0.037f, 0.010f };
            float[] kyc = { 0.040f, 0.045f, 0.060f, 0.055f, 0.040f, 0.026f, 0.019f, 0.014f, 0.010f };
            float[] khh = { 0.030f, 0.042f, 0.058f, 0.053f, 0.038f, 0.025f, 0.018f, 0.013f, 0.007f };
            var cw = Smooth(ks, kw); var cyc = Smooth(ks, kyc); var chh = Smooth(ks, khh);

            // right-foot frame, mirrored at the end
            Vector3 fwd = new Vector3(Mathf.Sin(toeOut), 0, -Mathf.Cos(toeOut));
            Vector3 lat = new Vector3(Mathf.Cos(toeOut), 0, Mathf.Sin(toeOut));
            Vector3 ankle = LegAxis(AnkleHeight, false);
            Vector3 heel = new Vector3(ankle.x, 0f, ankle.z + 0.055f);

            var d = new MeshData();
            int vpr = segs + 1;
            Func<float, Vector3> Pos = u => heel + fwd * (u * length) + lat * (-0.008f * SmoothStep01(0.6f, 1f, u));

            for (int j = 0; j <= sections; j++)
            {
                float s = j / (float)sections;
                float w = cw.Evaluate(s), yc = cyc.Evaluate(s), hh = chh.Evaluate(s);
                Vector3 p = Pos(s);
                for (int i = 0; i <= segs; i++)
                {
                    float phi = i / (float)segs * TwoPi;
                    float y = Mathf.Max(yc + hh * Mathf.Sin(phi), 0.003f); // flattened sole
                    Vector3 q = p + lat * (w * Mathf.Cos(phi)) + Vector3.up * y;
                    d.v.Add(q);
                    d.uv.Add(new Vector2(i / (float)segs, 0.004f));
                }
            }
            for (int j = 0; j < sections; j++)
                for (int i = 0; i < segs; i++)
                {
                    int a = j * vpr + i;
                    d.Quad(a, a + 1, a + vpr, a + vpr + 1);
                }
            // caps
            foreach (int j in new[] { 0, sections })
            {
                float s = j / (float)sections;
                int c = d.v.Count;
                d.v.Add(Pos(s) + Vector3.up * cyc.Evaluate(s));
                d.uv.Add(new Vector2(0.5f, 0.004f));
                for (int i = 0; i < segs; i++)
                {
                    d.t.Add(c); d.t.Add(j * vpr + i); d.t.Add(j * vpr + i + 1);
                }
            }
            if (left)
                for (int k = 0; k < d.v.Count; k++) { var p = d.v[k]; p.x = -p.x; d.v[k] = p; }

            Vector3 centroid = d.Centroid();
            d.OrientFaces(c => c - centroid);
            return d;
        }

        // ---------------------------------------------------------------- pelvis (under-shorts)

        public static Mesh BuildPelvis(int rings = 40, int segments = 72)
        {
            float[] ky = { 0.80f, 0.85f, 0.90f, 0.95f, 1.00f, 1.05f, 1.10f };
            float[] ka = { 0.120f, 0.165f, 0.178f, 0.172f, 0.145f, 0.140f, 0.145f };
            float[] kb = { 0.090f, 0.125f, 0.130f, 0.125f, 0.108f, 0.105f, 0.110f };
            float[] kz = { 0.005f, 0.012f, 0.012f, 0.008f, 0.004f, 0.004f, 0.004f };
            var ca = Smooth(ky, ka); var cb = Smooth(ky, kb); var cz = Smooth(ky, kz);
            var d = new MeshData();
            int vpr = segments + 1;
            for (int j = 0; j <= rings; j++)
            {
                float y = Mathf.Lerp(0.80f, 1.10f, j / (float)rings);
                for (int i = 0; i <= segments; i++)
                {
                    float th = i / (float)segments * TwoPi;
                    float r = EllipseRadius(ca.Evaluate(y), cb.Evaluate(y), th);
                    d.v.Add(new Vector3(r * Mathf.Cos(th), y, cz.Evaluate(y) + r * Mathf.Sin(th)));
                    d.uv.Add(new Vector2(i / (float)segments, j / (float)rings));
                }
            }
            for (int j = 0; j < rings; j++)
                for (int i = 0; i < segments; i++)
                {
                    int a = j * vpr + i;
                    d.Quad(a, a + 1, a + vpr, a + vpr + 1);
                }
            foreach (int j in new[] { 0, rings })
            {
                float y = Mathf.Lerp(0.80f, 1.10f, j / (float)rings);
                int c = d.v.Count;
                d.v.Add(new Vector3(0, y, cz.Evaluate(y)));
                d.uv.Add(new Vector2(0.5f, j / (float)rings));
                for (int i = 0; i < segments; i++) { d.t.Add(c); d.t.Add(j * vpr + i); d.t.Add(j * vpr + i + 1); }
            }
            Vector3 centroid = d.Centroid();
            d.OrientFaces(c => c - centroid);
            var m = d.ToMesh("Pelvis");
            m.RecalculateTangents();
            return m;
        }

        // ---------------------------------------------------------------- skirt (cloth)

        /// <summary>
        /// A-line pleated skirt, waist (y = 1.02) to hem (y = 0.64): 38 cm long.
        /// Built slightly flared; the Cloth solver drapes it onto the hip/leg colliders.
        /// Pleats are stitched flat for the first ~6 cm below the waistband, like a real skirt.
        /// </summary>
        public static Mesh BuildSkirt(int segments = 96, int rings = 30, int pleats = 16, float pleatDepth = 0.010f)
        {
            float[] kt = { 0.00f, 0.18f, 0.37f, 1.00f };
            float[] ka = { 0.150f, 0.190f, 0.214f, 0.270f };
            float[] kb = { 0.118f, 0.150f, 0.170f, 0.230f };
            var ca = Smooth(kt, ka); var cb = Smooth(kt, kb);
            var d = new MeshData();
            int vpr = segments + 1;
            for (int j = 0; j <= rings; j++)
            {
                float t = j / (float)rings;
                float y = Mathf.Lerp(WaistHeight, SkirtHemHeight, t);
                float pleatAmount = SmoothStep01(0.08f, 0.35f, t);
                for (int i = 0; i <= segments; i++)
                {
                    float th = i / (float)segments * TwoPi;
                    float tri = Mathf.Abs(Mathf.Repeat(th / TwoPi * pleats, 1f) * 2f - 1f) * 2f - 1f;
                    float r = EllipseRadius(ca.Evaluate(t), cb.Evaluate(t), th) + pleatDepth * tri * pleatAmount;
                    d.v.Add(new Vector3(r * Mathf.Cos(th), y, 0.010f + r * Mathf.Sin(th)));
                    d.uv.Add(new Vector2(i / (float)segments, 1f - t));
                }
            }
            // rings run top -> bottom, so "bottom-left" of a quad is on ring j+1
            for (int j = 0; j < rings; j++)
                for (int i = 0; i < segments; i++)
                {
                    int top = j * vpr + i, bottom = (j + 1) * vpr + i;
                    d.Quad(bottom, bottom + 1, top, top + 1);
                }
            d.OrientFaces(c => new Vector3(c.x, 0, c.z - 0.010f));
            var m = d.ToMesh("Skirt");
            var n = m.normals;
            WeldSeamNormals(n, 0, rings + 1, vpr);
            m.normals = n;
            m.RecalculateTangents();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Static waistband that hides the pinned top edge of the cloth.</summary>
        public static Mesh BuildWaistband(int segments = 96)
        {
            var d = new MeshData();
            int vpr = segments + 1;
            float[] ys = { 0.995f, 1.020f, 1.050f };
            for (int j = 0; j < ys.Length; j++)
                for (int i = 0; i <= segments; i++)
                {
                    float th = i / (float)segments * TwoPi;
                    float r = EllipseRadius(0.157f, 0.125f, th);
                    d.v.Add(new Vector3(r * Mathf.Cos(th), ys[j], 0.010f + r * Mathf.Sin(th)));
                    d.uv.Add(new Vector2(i / (float)segments * 8f, j / 2f * 0.15f));
                }
            for (int j = 0; j < ys.Length - 1; j++)
                for (int i = 0; i < segments; i++)
                {
                    int a = j * vpr + i;
                    d.Quad(a, a + 1, a + vpr, a + vpr + 1);
                }
            d.OrientFaces(c => new Vector3(c.x, 0, c.z - 0.010f));
            var m = d.ToMesh("Waistband");
            var n = m.normals;
            WeldSeamNormals(n, 0, ys.Length, vpr);
            m.normals = n;
            m.RecalculateTangents();
            return m;
        }

        // ---------------------------------------------------------------- studio cyclorama

        /// <summary>Seamless "infinity cove": floor that curves up into a back wall.</summary>
        public static Mesh BuildCyclorama(float width = 9f, float floorFront = -4f, float coveStartZ = 1.6f,
                                          float coveRadius = 1.2f, float wallHeight = 4.5f)
        {
            var profile = new List<Vector2>(); // (z, y)
            for (int k = 0; k <= 8; k++) profile.Add(new Vector2(Mathf.Lerp(floorFront, coveStartZ, k / 8f), 0));
            for (int k = 1; k <= 24; k++)
            {
                float a = k / 24f * Mathf.PI * 0.5f;
                profile.Add(new Vector2(coveStartZ + coveRadius * Mathf.Sin(a), coveRadius - coveRadius * Mathf.Cos(a)));
            }
            for (int k = 1; k <= 6; k++) profile.Add(new Vector2(coveStartZ + coveRadius, Mathf.Lerp(coveRadius, wallHeight, k / 6f)));

            const int cols = 12;
            var d = new MeshData();
            float len = 0f;
            for (int p = 0; p < profile.Count; p++)
            {
                if (p > 0) len += Vector2.Distance(profile[p], profile[p - 1]);
                for (int c = 0; c <= cols; c++)
                {
                    float x = Mathf.Lerp(-width / 2, width / 2, c / (float)cols);
                    d.v.Add(new Vector3(x, profile[p].y, profile[p].x));
                    d.uv.Add(new Vector2(x, len));
                }
            }
            int vpr = cols + 1;
            for (int p = 0; p < profile.Count - 1; p++)
                for (int c = 0; c < cols; c++)
                {
                    int a = p * vpr + c;
                    d.Quad(a, a + 1, a + vpr, a + vpr + 1);
                }
            var subject = new Vector3(0, 1.5f, -1f);
            d.OrientFaces(c => subject - c);
            var m = d.ToMesh("Cyclorama");
            m.RecalculateTangents();
            return m;
        }
    }
}
