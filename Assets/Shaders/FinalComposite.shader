Shader "OIT/FinalComposite"
{
    // Full-screen over-composite: blends OIT_ResolveTexture (ARGBFloat, premultiplied)
    // over whatever is currently in the backbuffer.
    //
    // src_over blending: result = src.rgb + dst.rgb * (1 - src.a)
    // SrcAlpha / OneMinusSrcAlpha with premultiplied source equals the same formula
    // because premult stores (r*a, g*a, b*a, a); One/OneMinusSrcAlpha handles it correctly.
    //
    // Fullscreen triangle: drawn with DrawProcedural(Triangles, 3).

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        ZTest  Always
        ZWrite Off
        Cull   Off

        Blend One OneMinusSrcAlpha

        Pass
        {
            Name "FinalComposite"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag
            #pragma target   3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_OIT_ResolveTexture);
            SAMPLER(sampler_OIT_ResolveTexture);

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings Vert(uint vertexID : SV_VertexID)
            {
                Varyings o;
                float2 uv    = float2((vertexID << 1) & 2, vertexID & 2);
                o.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                // UV origin matches DirectX/Metal convention; flip Y on OpenGL.
                #if UNITY_UV_STARTS_AT_TOP
                o.uv = float2(uv.x, 1.0 - uv.y);
                #else
                o.uv = uv;
                #endif
                return o;
            }

            float4 Frag(Varyings i) : SV_Target
            {
                return SAMPLE_TEXTURE2D(_OIT_ResolveTexture, sampler_OIT_ResolveTexture, i.uv);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
