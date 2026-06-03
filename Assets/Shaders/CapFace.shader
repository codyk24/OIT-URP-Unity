Shader "OIT/CapFace"
{
    // Fullscreen cap face shader for CSG cut boundaries.
    // Renders a solid cap colour at every pixel where the stencil buffer is non-zero
    // (i.e. pixels marked by CSGStencilPass as lying inside a cutter volume).
    //
    // ZWrite Off: the depth buffer must remain untouched so that transparent objects
    // rendered afterwards can depth-test against the original scene geometry.
    // ZTest Always: the cap face is a post-process overlay — it writes colour
    // unconditionally at stencil-marked pixels regardless of depth.
    //
    // Fullscreen triangle: drawn with DrawProcedural(Triangles, 3) — no vertex buffer.

    Properties
    {
        _CapColor ("Cap Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        ZTest  Always
        ZWrite Off
        Cull   Off

        Stencil
        {
            Ref  0
            Comp NotEqual
            Pass Keep
        }

        Pass
        {
            Name "CapFace"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag
            #pragma target   3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _CapColor;

            struct Varyings { float4 positionCS : SV_POSITION; };

            // Full-screen triangle from vertex ID — no vertex buffer needed.
            Varyings Vert(uint vertexID : SV_VertexID)
            {
                Varyings o;
                float2 uv    = float2((vertexID << 1) & 2, vertexID & 2);
                o.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                return o;
            }

            // Stencil test has already filtered — reaching this fragment means stencil != 0.
            half4 Frag(Varyings i) : SV_Target { return half4(_CapColor.rgb, 1.0); }
            ENDHLSL
        }
    }

    FallBack Off
}
