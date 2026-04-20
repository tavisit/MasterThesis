Shader "PCG/Metro Lane Shader"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.06, 0.06, 0.065, 1)
        _LineColor ("Lane Color", Color) = (0.72, 0.86, 1.0, 1)
        _CenterLineColor ("Center Line Color", Color) = (0.95, 0.95, 0.98, 1)
        _NoiseScale ("Surface Noise Scale", Float) = 0.28
        _NoiseAmount ("Surface Noise Amount", Range(0, 0.3)) = 0.08
        _RoadStartU ("Road UV Start", Range(0, 1)) = 0.18
        _RoadEndU ("Road UV End", Range(0, 1)) = 0.82
        _OuterLineInset ("Outer Line Inset", Range(0.01, 0.4)) = 0.12
        _OuterLineWidth ("Outer Line Width", Range(0.002, 0.08)) = 0.014
        _CenterLineWidth ("Center Line Width", Range(0.002, 0.08)) = 0.010
        _CenterDashed ("Center Dashed (0/1)", Range(0, 1)) = 1
        _DashLength ("Dash Length", Float) = 3.0
        _GapLength ("Gap Length", Float) = 2.2
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
            };

            float4 _BaseColor;
            float4 _LineColor;
            float4 _CenterLineColor;
            float _NoiseScale;
            float _NoiseAmount;
            float _RoadStartU;
            float _RoadEndU;
            float _OuterLineInset;
            float _OuterLineWidth;
            float _CenterLineWidth;
            float _CenterDashed;
            float _DashLength;
            float _GapLength;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1.0, 0.0));
                float c = hash21(i + float2(0.0, 1.0));
                float d = hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float lineMask(float x, float center, float width)
            {
                float d = abs(x - center);
                return 1.0 - smoothstep(width * 0.5, width, d);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 nuv = i.worldPos.xz * max(0.001, _NoiseScale);
                float n = valueNoise(nuv) - 0.5;
                float3 col = _BaseColor.rgb * (1.0 + n * _NoiseAmount * 2.0);

                float roadSpan = max(0.001, _RoadEndU - _RoadStartU);
                float u = saturate((i.uv.x - _RoadStartU) / roadSpan);
                float inRoad = step(_RoadStartU, i.uv.x) * step(i.uv.x, _RoadEndU);
                if (inRoad < 0.5)
                {
                    return float4(saturate(col * 0.85), 1.0);
                }

                float leftLine = lineMask(u, _OuterLineInset, _OuterLineWidth);
                float rightLine = lineMask(u, 1.0 - _OuterLineInset, _OuterLineWidth);
                float outerMask = saturate(max(leftLine, rightLine));

                float cycle = max(0.001, _DashLength + _GapLength);
                float dashMask = step(frac(i.uv.y / cycle), _DashLength / cycle);
                float centerMask = lineMask(u, 0.5, _CenterLineWidth);
                centerMask *= lerp(1.0, dashMask, saturate(_CenterDashed));

                col = lerp(col, _LineColor.rgb, outerMask);
                col = lerp(col, _CenterLineColor.rgb, centerMask);
                return float4(saturate(col), 1.0);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
