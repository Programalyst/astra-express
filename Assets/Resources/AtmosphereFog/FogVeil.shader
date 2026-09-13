Shader "Astra/Frontier Fog Veil"
{
    Properties
    {
        _VisibilityMask ("Unknown cells only", 2D) = "white" {}
        _FogClock ("Fog time", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-20" }
        Pass
        {
            Name "SoftFog"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 2.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_VisibilityMask);
            SAMPLER(sampler_VisibilityMask);
            CBUFFER_START(UnityPerMaterial)
                float _FogClock;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float2 worldXZ : TEXCOORD1; };
            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1,0)), f.x),
                            lerp(Hash(cell + float2(0,1)), Hash(cell + float2(1,1)), f.x), f.y);
            }
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(world);
                output.worldXZ = world.xz;
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half mask = SAMPLE_TEXTURE2D(_VisibilityMask, sampler_VisibilityMask, input.uv).r;
                clip(mask - 0.004h);
                float2 p = input.worldXZ * 0.21;
                float2 drift = float2(_FogClock * 0.025, -_FogClock * 0.018);
                half cloud = Noise(p + drift) * 0.7h + Noise(p * 2.1 - drift * 1.2) * 0.3h;
                // A one-cell bilinear fringe softens tile edges; data is visibility only.
                half coverage = smoothstep(0.02h, 0.82h, mask);
                half wisps = smoothstep(0.24h, 0.79h, cloud);
                half alpha = coverage * (0.33h + wisps * 0.26h);
                half3 color = lerp(half3(0.048, 0.092, 0.128), half3(0.16, 0.26, 0.29), wisps);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
