Shader "OIT/StencilProbe"
{
    // Test-only utility shader. Renders a fullscreen triangle and outputs white (1,1,1,1)
    // at pixels where the stencil buffer is non-zero, black (0,0,0,0) elsewhere.
    //
    // Usage in PlayMode tests:
    //   var cmd = new CommandBuffer();
    //   cmd.SetRenderTarget(probeRT.colorBuffer, sceneRT.depthBuffer);
    //   cmd.ClearRenderTarget(false, true, Color.black);
    //   cmd.DrawProcedural(Matrix4x4.identity, probeMaterial, 0, MeshTopology.Triangles, 3);
    //   Graphics.ExecuteCommandBuffer(cmd);
    // Then ReadPixels from probeRT — white pixels had stencil != 0.

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

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
            Name "StencilProbe"

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag
            #pragma target   3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Varyings { float4 positionCS : SV_POSITION; };

            // Full-screen triangle from vertex ID — no vertex buffer needed.
            Varyings Vert(uint vertexID : SV_VertexID)
            {
                Varyings o;
                float2 uv = float2((vertexID << 1) & 2, vertexID & 2);
                o.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                return o;
            }

            // Stencil test already filtered — if we reach this fragment, stencil != 0.
            half4 Frag(Varyings i) : SV_Target { return half4(1, 1, 1, 1); }
            ENDHLSL
        }
    }

    FallBack Off
}
