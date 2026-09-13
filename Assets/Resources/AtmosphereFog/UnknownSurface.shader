Shader "Astra/Unknown Terrain"
{
    Properties { _FogClock ("Fog time", Float) = 0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "OpaqueUnknown"
            Tags { "LightMode"="UniversalForward" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _FogClock;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.positionWS.xz;
                half drift = 0.5h + 0.5h * sin(p.x * 0.32 + p.y * 0.26 + _FogClock * 0.15);
                half detail = 0.5h + 0.5h * sin(p.x * 0.61 - p.y * 0.41 - _FogClock * 0.12);
                half top = saturate(input.normalWS.y * 0.4h + 0.6h);
                half3 color = lerp(half3(0.012, 0.025, 0.046), half3(0.028, 0.058, 0.078), drift * 0.65h + detail * 0.18h);
                return half4(color * top, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
