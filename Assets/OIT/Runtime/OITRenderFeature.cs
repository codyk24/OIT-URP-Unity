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
            renderer.EnqueuePass(_oitStencilMaskPass); // no-op unless OITResources.BufferManager is present
            renderer.EnqueuePass(_capFacePass);
            renderer.EnqueuePass(_oitGeometryPass);
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
