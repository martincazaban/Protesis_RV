// Resaltado de agarre: brillo de borde (fresnel) + leve relleno, aditivo.
// Compatible con URP y VR (single-pass instanced).
Shader "Entrenamiento/ResaltadoBorde"
{
    Properties
    {
        _RimColor ("Color", Color) = (1, 0.2, 0.2, 1)
        _RimPower ("Potencia del borde", Range(0.5, 8)) = 2.5
        _RimIntensity ("Intensidad del borde", Range(0, 3)) = 1.2
        _FillAlpha ("Relleno", Range(0, 1)) = 0.15
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "Resaltado"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _RimColor;
                half _RimPower;
                half _RimIntensity;
                half _FillAlpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert (Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.viewDirWS = GetWorldSpaceViewDir(p.positionWS);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 n = normalize(i.normalWS);
                float3 v = normalize(i.viewDirWS);
                half rim = pow(1.0h - saturate(dot(n, v)), _RimPower);
                half a = saturate(rim * _RimIntensity + _FillAlpha) * _RimColor.a;
                return half4(_RimColor.rgb, a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
