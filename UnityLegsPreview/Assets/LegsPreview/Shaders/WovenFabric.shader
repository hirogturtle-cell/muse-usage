// Double-sided woven fabric for the skirt (Built-in Render Pipeline surface shader).
// - Cull Off + VFACE so the inside of the skirt is lit correctly (lining colour on the back face).
// - Procedural 2/2 twill micro-normal (diagonal ribs), faded with distance to avoid moiré.
// - Soft "fuzz" sheen at grazing angles, typical of wool and brushed cotton.
Shader "LegsPreview/WovenFabric"
{
    Properties
    {
        _Color ("Face Color", Color) = (0.06, 0.08, 0.17, 1)
        _BackColor ("Lining Color", Color) = (0.04, 0.05, 0.10, 1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Smoothness ("Smoothness", Range(0,1)) = 0.22
        _WeaveTiling ("Weave Threads (U, V)", Vector) = (700, 160, 0, 0)
        _WeaveStrength ("Weave Bump", Range(0,1)) = 0.35
        _SheenColor ("Sheen Color", Color) = (0.55, 0.60, 0.80, 1)
        _SheenPower ("Sheen Falloff", Range(1,16)) = 4
        _SheenStrength ("Sheen Strength", Range(0,2)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 300
        Cull Off

        CGPROGRAM
        #pragma surface surf Fabric fullforwardshadows addshadow
        #pragma target 3.0
        #include "UnityPBSLighting.cginc"

        sampler2D _MainTex;
        fixed4 _Color, _BackColor, _SheenColor;
        half _Smoothness, _WeaveStrength, _SheenPower, _SheenStrength;
        float4 _WeaveTiling;

        struct Input
        {
            float2 uv_MainTex;
            fixed facing : VFACE;
        };

        inline half4 LightingFabric(SurfaceOutputStandard s, float3 viewDir, UnityGI gi)
        {
            half4 c = LightingStandard(s, viewDir, gi);
            half ndl = dot(s.Normal, gi.light.dir);
            half ndv = saturate(dot(s.Normal, viewDir));
            half sheen = pow(1.0 - ndv, _SheenPower) * saturate(ndl * 0.5 + 0.5);
            c.rgb += sheen * _SheenStrength * _SheenColor.rgb * gi.light.color;
            return c;
        }

        inline void LightingFabric_GI(SurfaceOutputStandard s, UnityGIInput data, inout UnityGI gi)
        {
            LightingStandard_GI(s, data, gi);
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float2 w = IN.uv_MainTex * _WeaveTiling.xy;
            float2 fw = fwidth(w);
            float aa = saturate(1.0 - max(fw.x, fw.y) * 1.2);
            float diag = (w.x + w.y * 2.0) * 3.14159265;          // 2/2 twill diagonal
            float nx = cos(diag) * 0.7 + cos(w.x * 6.2831853) * 0.3;
            float ny = cos(diag) * 0.5 + cos(w.y * 6.2831853) * 0.3;
            float s = _WeaveStrength * aa;
            float3 n = normalize(float3(nx * s, ny * s, 1.0));

            bool front = IN.facing > 0;
            n.z = front ? n.z : -n.z;

            fixed3 tex = tex2D(_MainTex, IN.uv_MainTex).rgb;
            o.Albedo = (front ? _Color.rgb : _BackColor.rgb) * tex;
            o.Normal = n;
            o.Smoothness = _Smoothness;
            o.Metallic = 0;
            o.Occlusion = 1;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Standard"
}
