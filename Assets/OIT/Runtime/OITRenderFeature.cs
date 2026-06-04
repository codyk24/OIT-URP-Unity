using UnityEngine.Rendering.Universal;

namespace OIT
{
    // Add to Assets/Settings/URP-Balanced-Renderer.asset via the Inspector
    // Renderer Features list after Unity reimports this file.
    public sealed class OITRenderFeature : ScriptableRendererFeature
    {
        private OpaquePass          _opaquePass;
        private CSGStencilPass      _csgStencilPass;
        private StencilCapturePass  _stencilCapturePass;
        private OITStencilMaskPass  _oitStencilMaskPass;
        private CapFacePass         _capFacePass;
        private OITGeometryPass     _oitGeometryPass;
        private OITResolvePass      _oitResolvePass;
        private FinalCompositePass  _finalCompositePass;

        public override void Create()
        {
            _opaquePass          = new OpaquePass();
            _csgStencilPass      = new CSGStencilPass();
            _stencilCapturePass  = new StencilCapturePass();
            _oitStencilMaskPass  = new OITStencilMaskPass();
            _capFacePass         = new CapFacePass();
            _oitGeometryPass     = new OITGeometryPass();
            _oitResolvePass      = new OITResolvePass();
            _finalCompositePass  = new FinalCompositePass();

            _csgStencilPass.TryInitialize();
            _stencilCapturePass.TryInitialize(); // no-op when OIT/StencilProbe shader is missing
            _oitStencilMaskPass.TryInitialize();  // no-op when OIT/StencilProbe shader is missing
            _capFacePass.TryInitialize();
            _oitResolvePass.TryInitialize();
            _finalCompositePass.TryInitialize();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(_opaquePass);
            renderer.EnqueuePass(_csgStencilPass);
            renderer.EnqueuePass(_stencilCapturePass); // no-op unless StencilCapturePass.ActiveCapture is set

            // Only enqueue OIT geometry passes when OITBufferManager has allocated the buffers.
            // Unconditionally enqueuing passes — even ones that return early in RecordRenderGraph —
            // can alter URP's NativeRenderPassCompiler scheduling and affect unrelated tests.
            if (OITResources.BufferManager != null)
            {
                renderer.EnqueuePass(_oitStencilMaskPass);
            }

            renderer.EnqueuePass(_capFacePass);

            if (OITResources.HeadBuffer != null)
            {
                renderer.EnqueuePass(_oitGeometryPass);
                renderer.EnqueuePass(_oitResolvePass);
                // FinalCompositePass is always paired with ResolvePass: RecordRenderGraph
                // for ResolvePass runs first (same enqueue order) so ResolveTexture will
                // be non-null by the time FinalCompositePass records. If ResolveTexture
                // is somehow null at record time, FinalCompositePass early-returns safely.
                renderer.EnqueuePass(_finalCompositePass);
            }
        }

        protected override void Dispose(bool disposing)
        {
            _csgStencilPass?.Dispose();
            _stencilCapturePass?.Dispose();
            _oitStencilMaskPass?.Dispose();
            _capFacePass?.Dispose();
            _finalCompositePass?.Dispose();
        }
    }
}
