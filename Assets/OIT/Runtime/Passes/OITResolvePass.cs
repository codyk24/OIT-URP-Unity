using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace OIT
{
    /// <summary>
    /// URP RenderGraph pass that resolves the per-pixel linked list built by
    /// <see cref="OITGeometryPass"/> into a screen-sized RGBA resolve texture.
    ///
    /// Dispatches <c>OITResolve.compute</c> at 1 thread per pixel (8×8 groups).
    /// Each thread:
    ///   1. Walks the linked list at its pixel index.
    ///   2. Insertion-sorts fragments front-to-back by NDC depth.
    ///   3. Under-composites them into a premultiplied RGBA result.
    ///   4. Writes the result to <see cref="OITResources.ResolveTexture"/>.
    ///
    /// The resolve texture is created (or resized) here to match the camera pixel
    /// dimensions, then stored in <see cref="OITResources"/> for
    /// <see cref="FinalCompositePass"/> to consume.
    /// </summary>
    public sealed class OITResolvePass : ScriptableRenderPass
    {
        // Compute shader parameter names — SetComputeXxxParam uses strings, not property IDs.
        private const string k_HeadBuffer    = "OIT_HeadBuffer";
        private const string k_NodeBuffer    = "OIT_NodeBuffer";
        private const string k_ResolveTex    = "OIT_ResolveTexture";
        private const string k_ScreenWidth   = "_OIT_ScreenWidth";
        private const string k_ScreenHeight  = "_OIT_ScreenHeight";

        private ComputeShader _computeShader;
        private int           _kernelIndex;

        private class PassData
        {
            internal ComputeShader  ComputeShader;
            internal int            Kernel;
            internal GraphicsBuffer HeadBuffer;
            internal GraphicsBuffer NodeBuffer;
            internal RenderTexture  ResolveTexture;
            internal int            ScreenWidth;
            internal int            ScreenHeight;
            internal string         PassName;
        }

        public OITResolvePass()
        {
            // Runs after OITGeometryPass (BeforeRenderingTransparents) so node
            // buffers are populated before this dispatch.
            renderPassEvent  = RenderPassEvent.BeforeRenderingTransparents + 1;
            profilingSampler = new ProfilingSampler("OIT.ResolvePass");
        }

        /// <summary>
        /// Loads the OITResolve compute shader from Resources. Returns false when
        /// the asset is missing (tests skip in that case).
        /// </summary>
        public bool TryInitialize()
        {
            _computeShader = Resources.Load<ComputeShader>("OITResolve");
            if (_computeShader == null)
                return false;

            _kernelIndex = _computeShader.FindKernel("CSMain");
            return true;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_computeShader == null)
                return;

            if (OITResources.HeadBuffer == null || OITResources.NodeBuffer == null)
                return;

            var cameraData = frameData.Get<UniversalCameraData>();
            int w = cameraData.camera.pixelWidth;
            int h = cameraData.camera.pixelHeight;

            EnsureResolveTexture(w, h);

            using var builder = renderGraph.AddUnsafePass<PassData>(
                "OIT.ResolvePass", out var passData, profilingSampler);

            builder.AllowPassCulling(false);

            passData.ComputeShader  = _computeShader;
            passData.Kernel         = _kernelIndex;
            passData.HeadBuffer     = OITResources.HeadBuffer;
            passData.NodeBuffer     = OITResources.NodeBuffer;
            passData.ResolveTexture = OITResources.ResolveTexture;
            passData.ScreenWidth    = w;
            passData.ScreenHeight   = h;
            passData.PassName       = "OIT.ResolvePass";

            builder.SetRenderFunc(static (PassData data, UnsafeGraphContext ctx) =>
            {
                OITPassOrder.Record(data.PassName);
                var cmd = ctx.cmd;

                cmd.SetComputeIntParam(data.ComputeShader, k_ScreenWidth,  data.ScreenWidth);
                cmd.SetComputeIntParam(data.ComputeShader, k_ScreenHeight, data.ScreenHeight);
                cmd.SetComputeBufferParam(data.ComputeShader, data.Kernel, k_HeadBuffer, data.HeadBuffer);
                cmd.SetComputeBufferParam(data.ComputeShader, data.Kernel, k_NodeBuffer, data.NodeBuffer);
                cmd.SetComputeTextureParam(data.ComputeShader, data.Kernel, k_ResolveTex, data.ResolveTexture);

                int groupsX = Mathf.CeilToInt(data.ScreenWidth  / 8f);
                int groupsY = Mathf.CeilToInt(data.ScreenHeight / 8f);
                cmd.DispatchCompute(data.ComputeShader, data.Kernel, groupsX, groupsY, 1);
            });
        }

        private static void EnsureResolveTexture(int w, int h)
        {
            if (OITResources.ResolveTexture != null &&
                OITResources.ResolveTexture.width  == w &&
                OITResources.ResolveTexture.height == h)
                return;

            OITResources.ResolveTexture?.Release();

            var rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGBFloat)
            {
                name            = "OIT_ResolveTexture",
                enableRandomWrite = true,
                filterMode      = FilterMode.Point,
            };
            rt.Create();
            OITResources.ResolveTexture = rt;
        }

    }
}
