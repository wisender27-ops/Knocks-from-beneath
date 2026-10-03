Shader "Knocks/PixelItemOutline"
{
    Properties
    {
        _OutlineColor ("Color", Color) = (1,0.85,0.45,1)
        _PixelSize ("Pixel size", Float) = 4
        _Width ("Width in pixels", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+25" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            ZWrite Off
            ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _PixelSize;
                float _Width;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                float4 clip = TransformWorldToHClip(world);
                float3 normal = TransformObjectToWorldNormal(input.normalOS);
                float4 projectedNormal = mul(UNITY_MATRIX_VP, float4(normal, 0));
                float2 direction = projectedNormal.xy * clip.w - clip.xy * projectedNormal.w;
                direction /= max(length(direction), 0.00001);
                float2 grid = _ScaledScreenParams.xy / max(_PixelSize, 1);
                float2 ndc = clip.xy / clip.w + direction * (2 * _Width / grid);
                clip.xy = (floor((ndc * 0.5 + 0.5) * grid) + 0.5) / grid * 2 - 1;
                clip.xy *= clip.w;
                output.positionCS = clip;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target { return _OutlineColor; }
            ENDHLSL
        }
    }
}
