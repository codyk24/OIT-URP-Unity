using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace OIT
{
    /// <summary>
    /// Captures the live CSG stencil buffer into a per-camera-dimension R8 texture
    /// (<see cref="OITResources.StencilMaskRT"/>) each frame for use by
    /// <see cref="OITGeometryPass"/>.
    ///
    /// WHY A SEPARATE PASS:
    ///   <see cref="OITGeometryPass"/> runs as an URP <c>AddUnsafePass</c> (required for
    ///   <c>SetRandomWriteTarget</c> / UAV writes). URP's <c>activeDepthTexture</c> is an
    ///   internal transient texture that cannot be bound as a render attachment inside an
    ///   UnsafePass, so hardware stencil is unavailable there. This pass is a RasterRenderPass
    ///   that CAN bind <c>activeDepthTexture</c>, runs immediately after
    ///   <see cref="CSGStencilPass"/>, and paints stencil != 0 as white so that
    ///   OITGeometryPass can sample it with <c>LOAD_TEXTURE2D</c> and discard CSG-masked pixels.
    ///
    /// RENDER ATTACHMENT SIZING:
    ///   The mask texture is created at <c>cameraData.camera.pixelWidth × pixelHeight</c>
    ///   each frame, matching <c>activeDepthTexture</c> exactly. This avoids the
    ///   NativeRenderPassCompiler dimension-mismatch error that occurs when a screen-sized
    ///   texture is combined with a smaller camera-targetTexture's depth attachment.
    ///
    /// LIFECYCLE:
    ///   Active only when <see cref="OITResources.BufferManager"/> is non-null.
    ///   Production code adds OITBufferManager to the scene; PlayMode tests that do not
    ///   use the OIT geometry pipeline omit it, making this pass a no-op.
    /// </summary>
    public sealed class OITStencilMaskPass : ScriptableRenderPass
    {
        private Material      _probeMaterial;
        private RenderTexture _maskRT;
        private RTHandle      _maskHandle;
        private int           _lastW = -1;
        private int           _lastH = -1;

        private class PassData { internal Material Material; }

        public OITStencilMaskPass()
        {
            // Runs after CSGStencilPass (also AfterRenderingOpaques) and after
            // StencilCapturePass. Enqueue order in OITRenderFeature guarantees this.
            renderPassEvent  = RenderPassEvent.AfterRenderingOpaques;
            profilingSampler = new ProfilingSampler("OIT.StencilMask");
        }

        /// <summary>
        /// Loads the StencilProbe material. Called once from
        /// <see cref="OITRenderFeature.Create"/>. Returns false if the shader is missing.
        /// </summary>
        public bool TryInitialize()
        {
            var shader = Shader.Find("OIT/StencilProbe");
            if (shader == null)
            {
                Debug.LogWarning(
                    "[OITStencilMaskPass] Shader 'OIT/StencilProbe' not found. " +
                    "CSG masking in OITGeometryPass will be unavailable.");
                return false;
            }
            _probeMaterial = CoreUtils.CreateEngineMaterial(shader);
            return true;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            // No-op when OITBufferManager is not in the scene.
            if (_probeMaterial == null || OITResources.BufferManager == null)
                return;

            var cameraData   = frameData.Get<UniversalCameraData>();
            var resourceData = frameData.Get<UniversalResourceData>();

            int w = cameraData.camera.pixelWidth;
            int h = cameraData.camera.pixelHeight;

            // Create or resize the mask to match the camera's render dimensions so that
            // its size equals activeDepthTexture's size (NativeRenderPassCompiler requirement).
            if (_maskRT == null || _lastW != w || _lastH != h)
            {
                _maskHandle?.Release();
                if (_maskRT != null)
                {
                    _maskRT.Release();
                    CoreUtils.Destroy(_maskRT);
                }

                _maskRT = new RenderTexture(w, h, 0, RenderTextureFormat.R8)
                    { name = "OIT_StencilMask", filterMode = FilterMode.Point };
                _maskRT.Create();
                _maskHandle = RTHandles.Alloc(_maskRT);
                _lastW = w;
                _lastH = h;

                // Publish so OITGeometryPass can import and sample it.
                OITResources.StencilMaskHandle?.Release();
                OITResources.StencilMaskRT     = _maskRT;
                OITResources.StencilMaskHandle = RTHandles.Alloc(_maskRT);
            }

            var maskHandle = renderGraph.ImportTexture(_maskHandle);

            using var builder = renderGraph.AddRasterRenderPass<PassData>(
                "OIT.StencilMask", out var passData, profilingSampler);

            passData.Material = _probeMaterial;

            // Write stencil-as-colour (white = masked, black = unmasked) to the mask.
            builder.SetRenderAttachment(maskHandle, 0, AccessFlags.Write);

            // Bind the same live depth/stencil that CSGStencilPass wrote into.
            builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Read);

            builder.AllowPassCulling(false);

            builder.SetRenderFunc(static (PassData data, RasterGraphContext ctx) =>
            {
                // Clear mask to black each frame before redrawing CSG stencil marks.
                ctx.cmd.ClearRenderTarget(RTClearFlags.Color, Color.black, 1.0f, 0);
                ctx.cmd.DrawProcedural(
                    Matrix4x4.identity, data.Material, 0, MeshTopology.Triangles, 3);
            });
        }

        /// <summary>Called by <see cref="OITRenderFeature.Dispose"/>.</summary>
        public void Dispose()
        {
            CoreUtils.Destroy(_probeMaterial);
            _probeMaterial = null;

            _maskHandle?.Release();
            _maskHandle = null;

            if (_maskRT != null)
            {
                _maskRT.Release();
                CoreUtils.Destroy(_maskRT);
                _maskRT = null;
            }

            _lastW = -1;
            _lastH = -1;

            OITResources.StencilMaskHandle?.Release();
            OITResources.StencilMaskHandle = null;
            OITResources.StencilMaskRT     = null;
        }
    }
}
