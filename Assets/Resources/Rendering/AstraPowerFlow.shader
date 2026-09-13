Shader "AstraExpress/Conduit Power Flow"
{
    Properties
    {
        _BaseColor ("Energized core", Color) = (0.035, 0.62, 0.55, 1)
        _PulseColor ("Moving power", Color) = (0.91, 1, 0.84, 1)
        _DormantColor ("No power", Color) = (0.025, 0.075, 0.09, 1)
        _FlowTime ("Simulation flow clock", Float) = 0
        _PowerAvailable ("Grid energized", Range(0, 1)) = 1
        [PerRendererData] _FlowLength ("Segment world length", Float) = 1
        [PerRendererData] _FlowOffset ("Distance from cable start", Float) = 0
        [PerRendererData] _FlowDirection ("Source to destination direction", Float) = 1
        [PerRendererData] _FlowAmount ("Branch running", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "ConduitFlow"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            ZWrite On
            Cull Back
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _PulseColor;
                half4 _DormantColor;
                float _FlowTime;
                float _PowerAvailable;
                float _FlowLength;
                float _FlowOffset;
                float _FlowDirection;
                float _FlowAmount;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 cablePosition : TEXCOORD0;
                half faceShade : TEXCOORD1;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                // Cube UV orientation changes between faces. Object coordinates keep
                // the arrows moving along every core's local Z axis, including ramps.
                output.cablePosition = float2(input.positionOS.x * 2,
                    (input.positionOS.z + 0.5) * _FlowLength + _FlowOffset);
                output.faceShade = 0.72h + 0.28h * saturate(input.normalOS.y);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                // Broad, high-contrast chevrons remain visible without HDR or bloom.
                float travel = input.cablePosition.y * _FlowDirection;
                float phase = frac((travel + abs(input.cablePosition.x) * 0.22 - _FlowTime * 1.5) / 1.6);
                half pulse = smoothstep(0.14, 0.24, phase) * (1 - smoothstep(0.38, 0.48, phase));
                pulse *= saturate(abs(_FlowDirection)) * _FlowAmount;
                half3 energized = lerp(_BaseColor.rgb, _PulseColor.rgb, pulse);
                half3 color = lerp(_DormantColor.rgb, energized, _PowerAvailable);
                return half4(color * input.faceShade, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
