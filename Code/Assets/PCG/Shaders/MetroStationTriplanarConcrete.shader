Shader "PCG/Metro Station Triplanar Concrete"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.62, 0.62, 0.62, 1)
        _SecondaryColor ("Secondary Color", Color) = (0.52, 0.52, 0.52, 1)
        _NoiseScale ("Noise Scale", Float) = 0.18
        _NoiseAmount ("Noise Amount", Range(0, 0.4)) = 0.16
        _DetailScale ("Detail Scale", Float) = 1.2
        _DetailAmount ("Detail Amount", Range(0, 0.25)) = 0.08
        _VerticalDarkening ("Vertical Face Darkening", Range(0, 0.35)) = 0.10
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        Pass
        {
            Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
            };

            float4 _BaseColor;
            float4 _SecondaryColor;
            float _NoiseScale;
            float _NoiseAmount;
            float _DetailScale;
            float _DetailAmount;
            float _VerticalDarkening;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 34.45);
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

            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.worldNormal);
                float3 w = pow(abs(n), 4.0);
                w /= max(1e-5, w.x + w.y + w.z);

                float3 p = i.worldPos;
                float scale = max(0.001, _NoiseScale);
                float detailScale = max(0.001, _DetailScale);

                float nx = valueNoise(p.yz * scale);
                float ny = valueNoise(p.xz * scale);
                float nz = valueNoise(p.xy * scale);
                float macroNoise = nx * w.x + ny * w.y + nz * w.z;

                float dx = valueNoise(p.yz * detailScale);
                float dy = valueNoise(p.xz * detailScale);
                float dz = valueNoise(p.xy * detailScale);
                float detailNoise = dx * w.x + dy * w.y + dz * w.z;

                float blend = saturate(macroNoise);
                float3 col = lerp(_BaseColor.rgb, _SecondaryColor.rgb, blend);
                col *= (1.0 + (macroNoise - 0.5) * 2.0 * _NoiseAmount);
                col *= (1.0 + (detailNoise - 0.5) * 2.0 * _DetailAmount);

                // Slightly darken vertical faces to improve shape readability.
                float verticalFactor = 1.0 - abs(n.y);
                col *= (1.0 - verticalFactor * _VerticalDarkening);

                return float4(saturate(col), 1.0);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
