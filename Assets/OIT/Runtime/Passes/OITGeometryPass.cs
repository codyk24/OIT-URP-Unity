using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace OIT
{
    /// <summary>
    /// URP RenderGraph pass that draws all <see cref="OITObject"/>-tagged transparent
    /// geometry into the per-pixel linked list buffers.
    ///
    /// For each visible fragment the <c>OIT/OITGeometry</c> shader atomically:
    ///   1. Increments <c>OIT_AtomicCounter</c> to allocate a node.
    ///   2. Writes packed colour + depth into <c>OIT_NodeBuffer[nodeIndex]</c>.
    ///   3. Exchanges <c>OIT_HeadBuffer[pixelIndex]</c> to link the new node at the
    ///      head of the per-pixel list.
    ///
    /// The pass runs at <see cref="RenderPassEvent.BeforeRenderingTransparents"/> so
    /// all opaque depth and CSG stencil values are fully written before this pass
    /// reads them via ZTest LEqual and Stencil Equal 0 in the shader.
    ///
    /// Buffer clearing is performed at the start of <see cref="RecordRenderGraph"/> via
    /// <see cref="OITBufferManager.ClearBuffers"/>, which uploads zeroed/sentinel data
    /// to the GPU before the render graph executes.
    /// </summary>
    public sealed class OITGeometryPass : ScriptableRenderPass
    {
        private static readonly int s_StencilMaskId = Shader.PropertyToID("_OIT_StencilMask");

        private class PassData
        {
            internal GraphicsBuffer HeadBuffer;
            internal GraphicsBuffer NodeBuffer;
            internal GraphicsBuffer AtomicCounter;
            internal Mesh[]         Meshes;
            internal Matrix4x4[]    Matrices;
            internal Material[]     Materials;
            internal int            Count;
            internal int            ScreenWidth;
            internal int            MaxNodes;
            // Null when rendering to the screen backbuffer.
            internal RenderTexture  CameraTargetRT;
            // Null when no OITBufferManager is in the scene (no CSG stencil capture active).
            internal RenderTexture  StencilMaskRT;
        }

        public OITGeometryPass()
        {
            // Scheduled after CSGStencilPass (AfterRenderingOpaques) and CapFacePass
            // (AfterRenderingOpaques+1) so stencil is fully written before this pass.
            renderPassEvent  = RenderPassEvent.BeforeRenderingTransparents;
            profilingSampler = new ProfilingSampler("OIT.GeometryPass");
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (OITResources.HeadBuffer == null)
                return;

            var system = OITSystem.Instance;
            if (system == null)
                return;

            // Clear all three OIT buffers to their frame-start state via CPU SetData
            // before the render graph executes this frame's commands.
            OITResources.BufferManager?.ClearBuffers();

            if (system.Objects.Count == 0)
                return;

            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraData   = frameData.Get<UniversalCameraData>();

            using var builder = renderGraph.AddUnsafePass<PassData>(
                "OIT.GeometryPass", out var passData, profilingSampler);

            // Declare resource usage so RenderGraph can track the dependency on
            // depth/stencil written by CSGStencilPass in the same frame.
            builder.UseTexture(resourceData.activeColorTexture, AccessFlags.Read);
            builder.UseTexture(resourceData.activeDepthTexture, AccessFlags.Read);

            // Declare a read dependency on the stencil mask so RenderGraph knows
            // StencilCapturePass (writer) must precede this pass.
            if (OITResources.StencilMaskHandle != null)
            {
                var stencilHandle = renderGraph.ImportTexture(OITResources.StencilMaskHandle);
                builder.UseTexture(stencilHandle, AccessFlags.Read);
            }

            builder.AllowPassCulling(false);

            passData.HeadBuffer    = OITResources.HeadBuffer;
            passData.NodeBuffer    = OITResources.NodeBuffer;
            passData.AtomicCounter = OITResources.AtomicCounter;
            // Render width may differ from Screen.width when camera.targetTexture is set.
            passData.ScreenWidth   = cameraData.camera.pixelWidth;
            // NodeBuffer.count == screenW × screenH × MaxLayers = total node capacity.
            passData.MaxNodes      = OITResources.NodeBuffer.count;
            passData.CameraTargetRT  = cameraData.camera.targetTexture;
            passData.StencilMaskRT   = OITResources.StencilMaskRT;

            int count = system.Objects.Count;
            passData.Count     = count;
            passData.Meshes    = new Mesh[count];
            passData.Matrices  = new Matrix4x4[count];
            passData.Materials = new Material[count];

            for (int i = 0; i < count; i++)
            {
                var obj = system.Objects[i];
                passData.Meshes[i]    = obj.SharedMesh;
                passData.Matrices[i]  = obj.LocalToWorld;
                passData.Materials[i] = obj.SharedMaterial;
            }

            builder.SetRenderFunc(static (PassData data, UnsafeGraphContext ctx) =>
            {
                var cmd = ctx.cmd;

                // Bind one colour attachment (ColorMask 0 in shader so nothing is written)
                // and the depth/stencil buffer so that:
                //   a) ZTest LEqual discards occluded fragments.
                //   b) Stencil Equal 0 skips CSG-masked pixels.
                //   c) One colour slot is occupied, placing UAV slots at index 1+.
                if (data.CameraTargetRT != null)
                {
                    cmd.SetRenderTarget(
                        data.CameraTargetRT.colorBuffer,
                        RenderBufferLoadAction.Load, RenderBufferStoreAction.Store,
                        data.CameraTargetRT.depthBuffer,
                        RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
                }
                else
                {
                    cmd.SetRenderTarget(
                        new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget),
                        RenderBufferLoadAction.Load, RenderBufferStoreAction.Store,
                        new RenderTargetIdentifier(BuiltinRenderTextureType.Depth),
                        RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
                }

                // UAV slots 1/2/3 — slot 0 is the colour attachment above.
                cmd.SetRandomWriteTarget(1, data.HeadBuffer);
                cmd.SetRandomWriteTarget(2, data.NodeBuffer);
                cmd.SetRandomWriteTarget(3, data.AtomicCounter);

                cmd.SetGlobalInt("_OIT_ScreenWidth", data.ScreenWidth);
                cmd.SetGlobalInt("_OIT_MaxNodes",    data.MaxNodes);

                // Bind the stencil-as-colour mask written by StencilCapturePass.
                // The shader uses LOAD_TEXTURE2D to discard pixels where the mask is white
                // (CSG-masked), replacing the hardware stencil test that is unavailable here.
                if (data.StencilMaskRT != null)
                    cmd.SetGlobalTexture(s_StencilMaskId, data.StencilMaskRT);

                for (int i = 0; i < data.Count; i++)
                {
                    if (data.Meshes[i] == null || data.Materials[i] == null)
                        continue;
                    cmd.DrawMesh(data.Meshes[i], data.Matrices[i], data.Materials[i], 0, 0);
                }

                cmd.ClearRandomWriteTargets();
            });
        }
    }
}
