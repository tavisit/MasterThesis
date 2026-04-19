Shader "PCG/Road Lane Shader"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.12, 0.12, 0.12, 1)
        _LaneColor ("Lane Marking Color", Color) = (0.96, 0.96, 0.92, 1)
        _CenterLineColor ("Center Line Color", Color) = (0.95, 0.78, 0.15, 1)
        _AsphaltNoise ("Asphalt Noise", 2D) = "gray" {}
        _AsphaltTiling ("Asphalt Tiling", Float) = 0.35
        _LaneCount ("Lane Count", Float) = 2
        _LaneLineWidth ("Lane Line Width", Range(0.002, 0.06)) = 0.012
        _DashLength ("Dash Length", Float) = 3.2
        _GapLength ("Gap Length", Float) = 2.4
        _CenterLineWidth ("Center Line Width", Range(0.002, 0.06)) = 0.014
        _CenterLineGap ("Double Center Gap", Range(0.001, 0.08)) = 0.014
        _RoadStartU ("Road UV Start", Range(0, 1)) = 0.15
        _RoadEndU ("Road UV End", Range(0, 1)) = 0.85
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
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float4 pos : SV_POSITION;
            };

            sampler2D _AsphaltNoise;
            float4 _BaseColor;
            float4 _LaneColor;
            float4 _CenterLineColor;
            float _AsphaltTiling;
            float _LaneCount;
            float _LaneLineWidth;
            float _DashLength;
            float _GapLength;
            float _CenterLineWidth;
            float _CenterLineGap;
            float _RoadStartU;
            float _RoadEndU;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            float lineMask(float x, float center, float width)
            {
                float d = abs(x - center);
                return saturate(1.0 - smoothstep(width * 0.5, width, d));
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 nuv = i.worldPos.xz * _AsphaltTiling;
                float noise = tex2D(_AsphaltNoise, nuv).r;
                float3 col = _BaseColor.rgb * (0.85 + noise * 0.22);

                float roadSpan = max(0.001, _RoadEndU - _RoadStartU);
                float roadU = saturate((i.uv.x - _RoadStartU) / roadSpan);
                float inRoad = step(_RoadStartU, i.uv.x) * step(i.uv.x, _RoadEndU);
                if (inRoad < 0.5)
                {
                    return float4(col * 0.85, 1.0);
                }

                float laneCount = max(1.0, round(_LaneCount));
                float cycle = max(0.001, _DashLength + _GapLength);
                float dashT = frac(i.uv.y / cycle);
                float dashMask = step(dashT, _DashLength / cycle);

                float laneMask = 0.0;
                [unroll]
                for (int k = 1; k <= 7; k++)
                {
                    if (k >= laneCount)
                    {
                        break;
                    }

                    float split = (float)k / laneCount;
                    laneMask = max(laneMask, lineMask(roadU, split, _LaneLineWidth) * dashMask);
                }

                float centerMask = 0.0;
                float centerUsesLaneColor = 0.0;
                if (laneCount >= 2.0)
                {
                    if (laneCount <= 2.1)
                    {
                        centerMask = lineMask(roadU, 0.5, _CenterLineWidth) * dashMask;
                        centerUsesLaneColor = 1.0;
                    }
                    else
                    {
                        centerMask = max(
                            lineMask(roadU, 0.5 - _CenterLineGap, _CenterLineWidth),
                            lineMask(roadU, 0.5 + _CenterLineGap, _CenterLineWidth));
                    }
                }

                col = lerp(col, _LaneColor.rgb, laneMask);
                float3 centerColor = lerp(_CenterLineColor.rgb, _LaneColor.rgb, centerUsesLaneColor);
                col = lerp(col, centerColor, centerMask);
                return float4(col, 1.0);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
