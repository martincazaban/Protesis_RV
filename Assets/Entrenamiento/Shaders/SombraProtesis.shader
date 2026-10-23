Shader "Entrenamiento/SombraProtesis"
{
    Properties { _Color ("Color", Color) = (0.3, 0.8, 1, 0.4) }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Sombra"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
            CBUFFER_END
            struct A { float4 pos : POSITION; float3 n : NORMAL; };
            struct V { float4 pos : SV_POSITION; float3 n : TEXCOORD0; float3 v : TEXCOORD1; };
            V vert(A i)
            {
                V o;
                o.pos = TransformObjectToHClip(i.pos.xyz);
                o.n = TransformObjectToWorldNormal(i.n);
                float3 wp = TransformObjectToWorld(i.pos.xyz);
                o.v = GetWorldSpaceViewDir(wp);
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                float f = pow(1.0 - saturate(dot(normalize(i.n), normalize(i.v))), 2.0);
                float a = _Color.a * (0.55 + 0.45 * f);
                return half4(_Color.rgb + f * 0.3, a);
            }
            ENDHLSL
        }
    }
}
