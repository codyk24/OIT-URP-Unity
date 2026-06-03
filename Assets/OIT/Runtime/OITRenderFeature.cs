using UnityEngine.Rendering.Universal;

namespace OIT
{
    // Add to Assets/Settings/URP-Balanced-Renderer.asset via the Inspector
    // Renderer Features list after Unity reimports this file.
    //
    // Future passes (OITGeometry, OITResolve, FinalComposite)
    // added in subsequent feature branches.
    public sealed class OITRenderFeature : ScriptableRendererFeature
    {
        private OpaquePass          _opaquePass;
        private CSGStencilPass      _csgStencilPass;
        private StencilCapturePass  _stencilCapturePass;
        private CapFacePass         _capFacePass;

        public override void Create()
        {
            _opaquePass         = new OpaquePass();
            _csgStencilPass     = new CSGStencilPass();
            _stencilCapturePass = new StencilCapturePass();
            _capFacePass        = new CapFacePass();

            _csgStencilPass.TryInitialize();
            _stencilCapturePass.TryInitialize(); // no-op when OIT/StencilProbe shader is missing
            _capFacePass.TryInitialize();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(_opaquePass);
            renderer.EnqueuePass(_csgStencilPass);
            renderer.EnqueuePass(_stencilCapturePass); // no-op when StencilCapturePass.ActiveCapture is null
            renderer.EnqueuePass(_capFacePass);
        }

        protected override void Dispose(bool disposing)
        {
            _csgStencilPass?.Dispose();
            _stencilCapturePass?.Dispose();
            _capFacePass?.Dispose();
        }
    }
}
