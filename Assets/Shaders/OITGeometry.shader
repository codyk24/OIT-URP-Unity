Shader "OIT/OITGeometry"
{
    // Per-pixel linked list append shader for Order-Independent Transparency.
    //
    // Each fragment atomically allocates a node in OIT_NodeBuffer, stores its
    // packed colour and depth, then exchanges itself into OIT_HeadBuffer at the
    // pixel's list head — building a singly-linked list that OITResolvePass
    // will sort and composite in a subsequent compute dispatch.
    //
    // ColorMask 0: no colour is written to the colour attachment; all output
    //   goes to UAV buffers.
    // ZTest LEqual: fragments behind opaque geometry are discarded before
    //   the UAV write, matching standard transparency depth behaviour.
    // ZWrite Off: transparent objects do not occlude each other or opaques.
    //
    // CSG masking — texture-based (not hardware stencil):
    //   OITGeometryPass runs inside an URP RenderGraph UnsafePass. URP's
    //   depth/stencil buffer lives in an internal transient texture
    //   (resourceData.activeDepthTexture) that cannot be bound as a render
    //   attachment inside an UnsafePass. Hardware stencil is therefore
    //   unavailable here. Instead, StencilCapturePass (a RasterPass that CAN
    //   bind the live depth) captures stencil != 0 as white into _OIT_StencilMask
    //   each frame. The fragment shader loads that texture and discards pixels
    //   where the mask is non-zero (CSG-inside).
    //
    // UAV register layout (slot 0 = colour attachment):
    //   register(u1) OIT_HeadBuffer     — uint per pixel, linked list head
    //   register(u2) OIT_NodeBuffer     — OITNode[MAX_LAYERS * w * h]
    //   register(u3) OIT_AtomicCounter  — uint, node allocation counter
    //
    // Packing matches OITColorPacking.cs: R in bits 31-24, G 23-16, B 15-8, A 7-0.

    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 1, 1, 0.5)
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            ZWrite Off
            ZTest  LEqual
            Cull   Back
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   5.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
            CBUFFER_END

            // Set each frame by OITGeometryPass before draw calls.
            int _OIT_ScreenWidth;
            int _OIT_MaxNodes;

            // Written by StencilCapturePass: white = CSG-masked, black = unmasked.
            // Null-safe: if OITBufferManager is absent the global is never set and
            // the texture samples as 0 (unmasked) on most platforms.
            TEXTURE2D(_OIT_StencilMask);
            SAMPLER(sampler_OIT_StencilMask);

            // UAV bindings — slot 0 is the colour attachment, so UAVs start at 1.
            RWStructuredBuffer<uint> OIT_HeadBuffer : register(u1);

            struct OITNode
            {
                uint packedColor; // RGBA8: R in bits 31-24
                uint depth;       // asuint(float) — NDC depth from SV_Position.z
                uint next;        // index of previous head (sentinel 0xFFFFFFFF)
            };
            RWStructuredBuffer<OITNode> OIT_NodeBuffer : register(u2);

            RWStructuredBuffer<uint> OIT_AtomicCounter : register(u3);

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                // SV_POSITION.xy are screen-space pixel centre coordinates.
                // Cast to uint for buffer indexing; row-major with _OIT_ScreenWidth columns.
                uint px = (uint)input.positionHCS.x;
                uint py = (uint)input.positionHCS.y;
                uint pixelIndex = py * (uint)_OIT_ScreenWidth + px;

                // Texture-based CSG masking: StencilCapturePass wrote white into
                // _OIT_StencilMask at every pixel the hardware stencil marked as
                // CSG-inside. Load (integer coords, no UV/flip ambiguity) and discard.
                float csgMask = LOAD_TEXTURE2D(_OIT_StencilMask, int2((int)px, (int)py)).r;
                if (csgMask > 0.5)
                    return float4(0, 0, 0, 0);

                // Atomically claim the next free node slot.
                uint nodeIndex;
                InterlockedAdd(OIT_AtomicCounter[0], 1u, nodeIndex);

                // Guard against buffer overflow (EDGE-OVF-01/02).
                if (nodeIndex < (uint)_OIT_MaxNodes)
                {
                    // Pack RGBA8 — R in bits 31-24, matching OITColorPacking.cs.
                    uint r = (uint)(saturate(_BaseColor.r) * 255.0 + 0.5);
                    uint g = (uint)(saturate(_BaseColor.g) * 255.0 + 0.5);
                    uint b = (uint)(saturate(_BaseColor.b) * 255.0 + 0.5);
                    uint a = (uint)(saturate(_BaseColor.a) * 255.0 + 0.5);

                    OIT_NodeBuffer[nodeIndex].packedColor = (r << 24) | (g << 16) | (b << 8) | a;
                    OIT_NodeBuffer[nodeIndex].depth       = asuint(input.positionHCS.z);

                    // Atomically splice this node at the head of the pixel's list.
                    uint prevHead;
                    InterlockedExchange(OIT_HeadBuffer[pixelIndex], nodeIndex, prevHead);
                    OIT_NodeBuffer[nodeIndex].next = prevHead;
                }

                // ColorMask 0 — this return value is discarded by the hardware.
                return float4(0, 0, 0, 0);
            }
            ENDHLSL
        }
    }
}
