Shader "PCG/Sidewalk Concrete Slabs Shader"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.72, 0.72, 0.72, 1)
        _SecondaryColor ("Secondary Color", Color) = (0.62, 0.62, 0.62, 1)
        _JointColor ("Joint Color", Color) = (0.2, 0.2, 0.2, 1)
        _PatternScale ("Pattern Scale", Float) = 0.18
        _PatternRotationX ("Pattern Rotation OX (Degrees)", Range(-180, 180)) = 0
        _PatternRotationY ("Pattern Rotation OY (Degrees)", Range(-180, 180)) = 0
        _PatternRotationZ ("Pattern Rotation OZ (Degrees)", Range(-180, 180)) = 0
        _TileAspect ("Tile Aspect (X/Z)", Range(0.3, 3.0)) = 1.3
        _RowOffsetStrength ("Row Offset Strength", Range(0, 1)) = 0.5
        _JointWidth ("Joint Width", Range(0.005, 0.20)) = 0.09
        _ColorVariation ("Color Variation", Range(0, 0.5)) = 0.08
        _EdgeDarkening ("Edge Darkening", Range(0, 0.35)) = 0.10
        _SurfaceNoiseScale ("Surface Noise Scale", Float) = 4.0
        _SurfaceNoiseAmount ("Surface Noise Amount", Range(0, 0.25)) = 0.06
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
            float _PatternRotationX;
            float _PatternRotationY;
            float _PatternRotationZ;
            float _TileAspect;
            float _RowOffsetStrength;
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

            float3 rotateByEulerDegrees(float3 p, float3 eulerDeg)
            {
                float3 r = eulerDeg * 0.01745329251;
                float sx = sin(r.x); float cx = cos(r.x);
                float sy = sin(r.y); float cy = cos(r.y);
                float sz = sin(r.z); float cz = cos(r.z);

                float3 rx = float3(
                    p.x,
                    p.y * cx - p.z * sx,
                    p.y * sx + p.z * cx);

                float3 ry = float3(
                    rx.x * cy + rx.z * sy,
                    rx.y,
                    -rx.x * sy + rx.z * cy);

                return float3(
                    ry.x * cz - ry.y * sz,
                    ry.x * sz + ry.y * cz,
                    ry.z);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 rotatedWorld = rotateByEulerDegrees(
                    i.worldPos,
                    float3(_PatternRotationX, _PatternRotationY, _PatternRotationZ));
                float2 uv = rotatedWorld.xz * max(0.001, _PatternScale);
                float2 gridUv = float2(uv.x * _TileAspect, uv.y);

                float rowId = floor(gridUv.y);
                float rowOffset = (_RowOffsetStrength * 0.5) * (fmod(rowId, 2.0) > 0.5 ? 1.0 : 0.0);
                gridUv.x += rowOffset;

                float2 cellId = floor(gridUv);
                float2 localUv = frac(gridUv);

                float halfJoint = _JointWidth * 0.5;
                float distToEdge = min(min(localUv.x, 1.0 - localUv.x), min(localUv.y, 1.0 - localUv.y));
                float jointMask = 1.0 - step(halfJoint, distToEdge);

                float seed = hash21(cellId);
                float blendFactor = saturate(0.2 + 0.8 * seed);
                float3 slabColor = lerp(_BaseColor.rgb, _SecondaryColor.rgb, blendFactor);

                float variation = (hash21(cellId + 19.7) - 0.5) * 2.0 * _ColorVariation;
                slabColor *= (1.0 + variation);

                float noise = valueNoise(uv * _SurfaceNoiseScale) - 0.5;
                slabColor *= (1.0 + noise * _SurfaceNoiseAmount);

                float rim = saturate(1.0 - distToEdge * 7.0);
                slabColor *= (1.0 - rim * _EdgeDarkening);

                float3 col = lerp(slabColor, _JointColor.rgb, jointMask);
                return float4(saturate(col), 1.0);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
