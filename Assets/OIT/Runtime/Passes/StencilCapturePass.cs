using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace OIT
{
    /// <summary>
    /// Optional pass that captures the live CSG stencil buffer as colour during the
    /// same URP frame in which <see cref="CSGStencilPass"/> writes it.
    ///
    /// WHY THIS EXISTS:
    ///   In URP with RenderGraph, depth/stencil lives in <c>resourceData.activeDepthTexture</c>
    ///   — an internal transient texture. URP copies only the final colour output to
    ///   <c>camera.targetTexture</c>; the depth/stencil is never mirrored there.
    ///   Reading <c>camera.targetTexture.depthBuffer</c> after the frame therefore yields
    ///   no stencil data. This pass solves the problem by running <em>within</em> the frame,
    ///   binding the same live depth attachment and painting stencil != 0 as white into
    ///   <see cref="ActiveCapture"/>.
    ///
    /// USAGE (PlayMode tests only):
    /// <code>
    ///   StencilCapturePass.ActiveCapture = myColorOnlyRenderTexture; // [SetUp]
    ///   yield return null; // stencil-as-colour written to ActiveCapture during frame
    ///   // ReadPixels from ActiveCapture — white = stencil was non-zero
    ///   StencilCapturePass.ActiveCapture = null; // [TearDown]
    /// </code>
    ///
    /// When <see cref="ActiveCapture"/> is null (i.e. in production) the pass is a no-op.
    /// </summary>
    public sealed class StencilCapturePass : ScriptableRenderPass
    {
        /// <summary>
        /// Set to a plain colour <see cref="RenderTexture"/> before the frame to enable
        /// capture. Clear to null in TearDown. Thread-safe only within the Unity main thread.
        /// </summary>
        public static RenderTexture ActiveCapture;

        private Material      _probeMaterial;
        private RTHandle      _captureHandle;
        private RenderTexture _lastCapture;

        private class PassData { internal Material Material; }

        public StencilCapturePass()
        {
            renderPassEvent  = RenderPassEvent.AfterRenderingOpaques;
            profilingSampler = new ProfilingSampler("OIT.StencilCapture");
        }

        /// <summary>
        /// Loads the probe material. Called once from
        /// <see cref="OITRenderFeature.Create"/>. Returns false if the shader is missing.
        /// </summary>
        public bool TryInitialize()
        {
            var shader = Shader.Find("OIT/StencilProbe");
            if (shader == null)
            {
                Debug.LogWarning(
                    "[StencilCapturePass] Shader 'OIT/StencilProbe' not found. " +
                    "Stencil capture will be unavailable.");
                return false;
            }
            _probeMaterial = CoreUtils.CreateEngineMaterial(shader);
            return true;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            // No-op in production when no test has set a capture target.
            if (_probeMaterial == null || ActiveCapture == null)
                return;

            // Refresh RTHandle when the test swaps to a different RenderTexture.
            if (_lastCapture != ActiveCapture)
            {
                _captureHandle?.Release();
                _captureHandle = RTHandles.Alloc(ActiveCapture);
                _lastCapture   = ActiveCapture;
            }

            var resourceData  = frameData.Get<UniversalResourceData>();
            var captureHandle = renderGraph.ImportTexture(_captureHandle);

            using var builder = renderGraph.AddRasterRenderPass<PassData>(
                "OIT.StencilCapture", out var passData, profilingSampler);

            passData.Material = _probeMaterial;

            // Write stencil-as-colour to the imported capture texture.
            builder.SetRenderAttachment(captureHandle, 0, AccessFlags.Write);

            // Read from the same depth/stencil that CSGStencilPass just wrote.
            // AccessFlags.Read = stencil test (and depth test) without any writes.
            builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Read);

            // No RendererList — must disable automatic pass culling.
            builder.AllowPassCulling(false);

            builder.SetRenderFunc(static (PassData data, RasterGraphContext ctx) =>
            {
                // Clear only the colour attachment to black so pixels with stencil == 0
                // remain black after the probe draw. RTClearFlags.Color avoids touching
                // the depth attachment we declared as read-only.
                ctx.cmd.ClearRenderTarget(RTClearFlags.Color, Color.black, 1.0f, 0);
                // Draw fullscreen triangle; StencilProbe shader paints white where stencil != 0.
                ctx.cmd.DrawProcedural(
                    Matrix4x4.identity, data.Material, 0, MeshTopology.Triangles, 3);
            });
        }

        /// <summary>Called by <see cref="OITRenderFeature.Dispose"/>.</summary>
        public void Dispose()
        {
            CoreUtils.Destroy(_probeMaterial);
            _captureHandle?.Release();
            _captureHandle = null;
            _lastCapture   = null;
        }
    }
}
