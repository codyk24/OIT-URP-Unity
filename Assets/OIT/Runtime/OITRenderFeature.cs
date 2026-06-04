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

        public override void Create()
        {
            _opaquePass          = new OpaquePass();
            _csgStencilPass      = new CSGStencilPass();
            _stencilCapturePass  = new StencilCapturePass();
            _oitStencilMaskPass  = new OITStencilMaskPass();
            _capFacePass         = new CapFacePass();
            _oitGeometryPass     = new OITGeometryPass();

            _csgStencilPass.TryInitialize();
            _stencilCapturePass.TryInitialize(); // no-op when OIT/StencilProbe shader is missing
            _oitStencilMaskPass.TryInitialize();  // no-op when OIT/StencilProbe shader is missing
            _capFacePass.TryInitialize();
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
            }
        }

        protected override void Dispose(bool disposing)
        {
            _csgStencilPass?.Dispose();
            _stencilCapturePass?.Dispose();
            _oitStencilMaskPass?.Dispose();
            _capFacePass?.Dispose();
        }
    }
}
