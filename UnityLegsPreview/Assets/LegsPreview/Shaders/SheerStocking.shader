// Sheer nylon stocking over skin (Built-in Render Pipeline surface shader).
//
// Real hosiery is a sparse knit: looking straight at the leg you see skin through the gaps, while at
// grazing angles the yarns overlap and the colour becomes almost fully opaque. That angular
// "denier falloff" is what makes stockings read as stockings, so coverage is driven by N·V on top
// of a procedural knit micro-normal. Skin under the nylon gets a cheap wrap-lighting subsurface
// term; the nylon gets a soft fibre sheen. UV.y = height along the leg selects the lace thigh band.
Shader "LegsPreview/SheerStocking"
{
    Properties
    {
        [Header(Skin)]
        _SkinColor ("Skin Color", Color) = (0.87, 0.67, 0.56, 1)
        _SkinSmoothness ("Skin Smoothness", Range(0,1)) = 0.40
        _SubsurfaceColor ("Subsurface Color", Color) = (0.90, 0.30, 0.20, 1)
        _Wrap ("Subsurface Wrap", Range(0,1)) = 0.45

        [Header(Nylon)]
        _MainTex ("Nylon Detail (RGB)", 2D) = "white" {}
        _NylonColor ("Nylon Color", Color) = (0.97, 0.97, 0.96, 1)
        _Denier ("Facing Opacity (denier)", Range(0,1)) = 0.55
        _GrazingOpacity ("Grazing Opacity", Range(0,1)) = 0.97
        _FresnelPower ("Opacity Falloff", Range(0.5,8)) = 2.2
        _NylonSmoothness ("Nylon Smoothness", Range(0,1)) = 0.55
        _SheenColor ("Fibre Sheen Color", Color) = (1, 1, 1, 1)
        _SheenPower ("Fibre Sheen Falloff", Range(1,16)) = 5
        _SheenStrength ("Fibre Sheen Strength", Range(0,2)) = 0.5
        _KnitTiling ("Knit Loops (U around, V along)", Vector) = (320, 900, 0, 0)
        _KnitStrength ("Knit Bump", Range(0,1)) = 0.25

        [Header(Thigh band)]
        _BandStart ("Band Start (UV.y)", Range(0,1)) = 0.80
        _BandHeight ("Band Height (UV.y)", Range(0.01,0.2)) = 0.075
        _LaceRepeats ("Lace Repeats", Float) = 18
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Stocking fullforwardshadows
        #pragma target 3.0
        #include "UnityPBSLighting.cginc"

        sampler2D _MainTex;
        fixed4 _SkinColor, _SubsurfaceColor, _NylonColor, _SheenColor;
        half _SkinSmoothness, _Wrap, _Denier, _GrazingOpacity, _FresnelPower, _NylonSmoothness;
        half _SheenPower, _SheenStrength, _KnitStrength;
        float4 _KnitTiling;
        float _BandStart, _BandHeight, _LaceRepeats;

        struct Input
        {
            float2 uv_MainTex;
            float3 viewDir;
        };

        struct SurfaceOutputStocking
        {
            fixed3 Albedo;
            float3 Normal;
            half3 Emission;
            half Metallic;
            half Smoothness;
            half Occlusion;
            fixed Alpha;
            half Coverage;   // 0 = bare skin, 1 = opaque nylon
        };

        inline SurfaceOutputStandard ToStandard(SurfaceOutputStocking s)
        {
            SurfaceOutputStandard o;
            o.Albedo = s.Albedo;
            o.Normal = s.Normal;
            o.Emission = s.Emission;
            o.Metallic = s.Metallic;
            o.Smoothness = s.Smoothness;
            o.Occlusion = s.Occlusion;
            o.Alpha = s.Alpha;
            return o;
        }

        inline half4 LightingStocking(SurfaceOutputStocking s, float3 viewDir, UnityGI gi)
        {
            half4 c = LightingStandard(ToStandard(s), viewDir, gi);

            half ndl = dot(s.Normal, gi.light.dir);
            // Light bleeding past the terminator through the skin (softer where nylon covers it).
            half wrapExtra = saturate((ndl + _Wrap) / (1.0 + _Wrap)) - saturate(ndl);
            c.rgb += wrapExtra * gi.light.color * _SubsurfaceColor.rgb * _SkinColor.rgb * (1.0 - 0.7 * s.Coverage);

            // Fibre sheen: retro-reflective glow of nylon filaments near the silhouette.
            half ndv = saturate(dot(s.Normal, viewDir));
            half sheen = pow(1.0 - ndv, _SheenPower) * saturate(ndl * 0.5 + 0.5);
            c.rgb += sheen * _SheenStrength * _SheenColor.rgb * gi.light.color * s.Coverage;
            return c;
        }

        inline void LightingStocking_GI(SurfaceOutputStocking s, UnityGIInput data, inout UnityGI gi)
        {
            LightingStandard_GI(ToStandard(s), data, gi);
        }

        void surf(Input IN, inout SurfaceOutputStocking o)
        {
            float2 uv = IN.uv_MainTex;

            // --- procedural knit micro-normal, faded out when loops get smaller than a pixel
            float2 k = uv * _KnitTiling.xy;
            float2 fw = fwidth(k);
            float aa = saturate(1.0 - max(fw.x, fw.y) * 1.5);
            float2 kp = k * 6.2831853;
            float nx = cos(kp.x) * (0.6 + 0.4 * sin(kp.y));
            float ny = cos(kp.y + 0.8 * sin(kp.x));
            float strength = _KnitStrength * aa;
            float3 knitN = normalize(float3(nx * strength, ny * strength, 1.0));

            // --- regions: sheer leg / lace welt band / bare skin above the scalloped edge
            float bandT = (uv.y - _BandStart) / _BandHeight;
            float scallop = 1.0 - 0.16 * abs(sin(uv.x * 3.14159265 * _LaceRepeats));
            float edgeAA = max(fwidth(bandT), 1e-4);
            float inStocking = 1.0 - smoothstep(scallop - edgeAA, scallop + edgeAA, bandT);
            float inBand = smoothstep(-edgeAA, edgeAA, bandT) * inStocking;

            float laceT = saturate((bandT - 0.35) / 0.65);
            float lace = 0.5 + 0.5 * sin(uv.x * 6.2831853 * _LaceRepeats * 2.0) * sin(laceT * 9.42477 + sin(uv.x * 6.2831853 * _LaceRepeats));
            float laceAA = max(fwidth(lace), 1e-3);
            float laceMask = smoothstep(0.42 - laceAA, 0.42 + laceAA, lace);
            float bandCov = bandT < 0.35 ? 0.96 : lerp(0.30, 0.95, laceMask);

            // --- angular coverage (the "denier" look)
            float ndv = saturate(dot(normalize(IN.viewDir), knitN));
            float fres = pow(1.0 - ndv, _FresnelPower);
            float sheerCov = lerp(_Denier, _GrazingOpacity, fres);
            float cov = lerp(sheerCov, max(sheerCov, bandCov), inBand) * inStocking;

            fixed3 nylon = _NylonColor.rgb * tex2D(_MainTex, uv).rgb;
            o.Albedo = lerp(_SkinColor.rgb, nylon, cov);
            o.Normal = normalize(lerp(float3(0, 0, 1), knitN, cov));
            o.Smoothness = lerp(_SkinSmoothness, _NylonSmoothness, cov);
            o.Metallic = 0;
            o.Occlusion = 1;
            o.Emission = 0;
            o.Alpha = 1;
            o.Coverage = cov;
        }
        ENDCG
    }
    FallBack "Standard"
}
