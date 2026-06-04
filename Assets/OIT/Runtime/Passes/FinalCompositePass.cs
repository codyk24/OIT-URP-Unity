using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace OIT
{
    /// <summary>
    /// URP RenderGraph pass that blends the OIT resolve texture produced by
    /// <see cref="OITResolvePass"/> over the opaque + cap-face backbuffer.
    ///
    /// Runs at <see cref="RenderPassEvent.AfterRenderingTransparents"/> so it is
    /// guaranteed to be the last OIT pass each frame.
    ///
    /// Uses a full-screen triangle with <c>OIT/FinalComposite</c> shader:
    ///   result = resolve.rgb + backbuffer.rgb * (1 - resolve.a)   (src-over)
    /// </summary>
    public sealed class FinalCompositePass : ScriptableRenderPass
    {
        private static readonly int k_ResolveTex = Shader.PropertyToID("_OIT_ResolveTexture");

        private Material      _material;
        private RTHandle      _resolveHandle;
        private RenderTexture _resolveHandleSource;

        private class PassData
        {
            internal Material      Material;
            internal TextureHandle ResolveTextureHandle;
            internal string        PassName;
        }

        public FinalCompositePass()
        {
            renderPassEvent  = RenderPassEvent.AfterRenderingTransparents;
            profilingSampler = new ProfilingSampler("OIT.FinalCompositePass");
        }

        /// <summary>
        /// Loads the OIT/FinalComposite shader from the asset database.
        /// Returns false when the shader is missing (tests skip in that case).
        /// </summary>
        public bool TryInitialize()
        {
            var shader = Shader.Find("OIT/FinalComposite");
            if (shader == null)
                return false;

            _material = CoreUtils.CreateEngineMaterial(shader);
            return _material != null;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_material == null)
                return;

            if (OITResources.ResolveTexture == null)
                return;

            var resourceData = frameData.Get<UniversalResourceData>();

            // Import OIT_ResolveTexture as a RenderGraph TextureHandle. RasterCommandBuffer
            // requires TextureHandle for SetGlobalTexture — raw RenderTexture is not accepted.
            // Re-allocate the RTHandle only when the underlying RenderTexture changes.
            var resolveRT = OITResources.ResolveTexture;
            if (_resolveHandle == null || _resolveHandleSource != resolveRT)
            {
                _resolveHandle?.Release();
                _resolveHandle       = RTHandles.Alloc(resolveRT);
                _resolveHandleSource = resolveRT;
            }
            var resolveTextureHandle = renderGraph.ImportTexture(_resolveHandle);

            // Use a RasterPass so URP binds the active colour target automatically,
            // matching the same approach as CapFacePass. An UnsafePass leaves the
            // render target state undefined after the preceding compute dispatch.
            using var builder = renderGraph.AddRasterRenderPass<PassData>(
                "OIT.FinalCompositePass", out var passData, profilingSampler);

            builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);
            builder.UseTexture(resolveTextureHandle, AccessFlags.Read);
            builder.AllowPassCulling(false);
            builder.AllowGlobalStateModification(true);

            passData.Material            = _material;
            passData.ResolveTextureHandle = resolveTextureHandle;
            passData.PassName            = "OIT.FinalCompositePass";

            builder.SetRenderFunc(static (PassData data, RasterGraphContext ctx) =>
            {
                OITPassOrder.Record(data.PassName);
                ctx.cmd.SetGlobalTexture(k_ResolveTex, data.ResolveTextureHandle);
                ctx.cmd.DrawProcedural(Matrix4x4.identity, data.Material, 0,
                    MeshTopology.Triangles, 3);
            });
        }

        public void Dispose()
        {
            _resolveHandle?.Release();
            _resolveHandle = null;
            CoreUtils.Destroy(_material);
        }

    }
}
