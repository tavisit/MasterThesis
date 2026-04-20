Shader "PCG/Sidewalk Cobblestone Shader"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.66, 0.66, 0.66, 1)
        _SecondaryColor ("Secondary Color", Color) = (0.53, 0.53, 0.53, 1)
        _JointColor ("Joint Color", Color) = (0.18, 0.18, 0.18, 1)
        _PatternScale ("Pattern Scale", Float) = 0.22
        _JointWidth ("Joint Width", Range(0.005, 0.20)) = 0.10
        _ColorVariation ("Color Variation", Range(0, 0.5)) = 0.16
        _EdgeDarkening ("Edge Darkening", Range(0, 0.35)) = 0.14
        _SurfaceNoiseScale ("Surface Noise Scale", Float) = 5.0
        _SurfaceNoiseAmount ("Surface Noise Amount", Range(0, 0.25)) = 0.08
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        Pass
        {
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };

            float4 _BaseColor;
            float4 _SecondaryColor;
            float4 _JointColor;
            float _PatternScale;
            float _JointWidth;
            float _ColorVariation;
            float _EdgeDarkening;
            float _SurfaceNoiseScale;
            float _SurfaceNoiseAmount;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
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

            float2 hash22(float2 p)
            {
                float n1 = hash21(p + 17.37);
                float n2 = hash21(p + 91.53);
                return float2(n1, n2);
            }

            void voronoi(float2 uv, out float2 nearestCell, out float edgeDistance, out float centerDistance)
            {
                float2 baseCell = floor(uv);
                float nearest = 1e9;
                float secondNearest = 1e9;
                nearestCell = baseCell;

                [unroll]
                for (int y = -1; y <= 1; y++)
                {
                    [unroll]
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 cell = baseCell + float2((float)x, (float)y);
                        float2 jitter = (hash22(cell) - 0.5) * 0.72;
                        float2 featurePoint = cell + 0.5 + jitter;
                        float2 diff = uv - featurePoint;
                        float distSq = dot(diff, diff);

                        if (distSq < nearest)
                        {
                            secondNearest = nearest;
                            nearest = distSq;
                            nearestCell = cell;
                        }
                        else if (distSq < secondNearest)
                        {
                            secondNearest = distSq;
                        }
                    }
                }

                centerDistance = sqrt(nearest);
                edgeDistance = max(0.0, (sqrt(secondNearest) - sqrt(nearest)) * 0.5);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.worldPos.xz * max(0.001, _PatternScale * 1.6);
                float2 cellId;
                float edgeDistance;
                float centerDistance;
                voronoi(uv, cellId, edgeDistance, centerDistance);
                float jointMask = 1.0 - smoothstep(0.0, _JointWidth, edgeDistance);

                float seed = hash21(cellId);
                float blendFactor = saturate(0.2 + 0.8 * seed);
                float3 stoneColor = lerp(_BaseColor.rgb, _SecondaryColor.rgb, blendFactor);

                float variation = (hash21(cellId + 42.8) - 0.5) * 2.0 * _ColorVariation;
                stoneColor *= (1.0 + variation);

                float noise = valueNoise(uv * _SurfaceNoiseScale) - 0.5;
                stoneColor *= (1.0 + noise * _SurfaceNoiseAmount);

                float rim = saturate(1.0 - edgeDistance * 9.0);
                stoneColor *= (1.0 - rim * _EdgeDarkening);

                float centerLift = saturate(1.0 - centerDistance * 1.9);
                stoneColor *= (1.0 + centerLift * 0.06);

                float3 col = lerp(stoneColor, _JointColor.rgb, jointMask);
                return float4(saturate(col), 1.0);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
