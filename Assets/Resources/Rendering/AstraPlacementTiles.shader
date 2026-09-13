Shader "Astra/Available Connection Tiles"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+5" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            // Placement is a tactical overlay on revealed, legal cells only.
            // Keep tile bevels and decorative surfaces from hiding its corners.
            ZTest Always
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 2.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; };
            Varyings Vert(Attributes input) { Varyings o; o.positionCS=TransformObjectToHClip(input.positionOS.xyz); o.color=input.color; return o; }
            half4 Frag(Varyings input) : SV_Target { return input.color; }
            ENDHLSL
        }
    }
}
