Shader "OIT/CSGStencil"
{
    // Stencil-only shader for CSG inside-outside detection.
    // Two passes per cutter mesh use z-fail logic (ZTest Greater) so the GPU
    // marks pixels where scene geometry lies inside the cutter volume:
    //
    //   Pass 0 (BackFace):  Cull Front, ZTest Greater → IncrSat when back face is BEHIND scene
    //   Pass 1 (FrontFace): Cull Back,  ZTest Greater → DecrSat removes false positives
    //
    // ColorMask 0 on both passes — zero colour is written to the render target.

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Geometry+1"
        }

        // ---- Pass 0: Back-face z-fail → increment stencil ----
        // Draws the far-side (back) faces of the cutter mesh.
        // ZTest Greater passes when the cutter face is BEHIND existing scene depth,
        // meaning the scene geometry at this pixel is inside the cutter volume.
        Pass
        {
            Name "CSGStencil_BackFace"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull     Front
            ZTest    Greater
            ZWrite   Off
            ColorMask 0

            Stencil
            {
                Ref   0
                Comp  Always
                Pass  IncrSat
                Fail  Keep
                ZFail Keep
            }

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag
            #pragma target   3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionCS : SV_POSITION; };

            Varyings Vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                return o;
            }

            // ColorMask 0 discards all colour output — fragment return value is irrelevant.
            half4 Frag(Varyings i) : SV_Target { return half4(0, 0, 0, 0); }
            ENDHLSL
        }

        // ---- Pass 1: Front-face z-fail → decrement stencil ----
        // Draws the near-side (front) faces of the cutter mesh.
        // Cancels the back-face increment for pixels where the scene geometry is
        // NOT inside the cutter (e.g. camera is inside cutter, or target is in front).
        Pass
        {
            Name "CSGStencil_FrontFace"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull     Back
            ZTest    Greater
            ZWrite   Off
            ColorMask 0

            Stencil
            {
                Ref   0
                Comp  Always
                Pass  DecrSat
                Fail  Keep
                ZFail Keep
            }

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag
            #pragma target   3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionCS : SV_POSITION; };

            Varyings Vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target { return half4(0, 0, 0, 0); }
            ENDHLSL
        }
    }

    FallBack Off
}
