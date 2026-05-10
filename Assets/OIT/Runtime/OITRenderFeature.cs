using UnityEngine.Rendering.Universal;

namespace OIT
{
    // Add to Assets/Settings/URP-Balanced-Renderer.asset via the Inspector
    // Renderer Features list after Unity reimports this file.
    //
    // Future passes (CSGStencil, CapFace, OITGeometry, OITResolve, FinalComposite)
    // added in subsequent feature branches.
    public sealed class OITRenderFeature : ScriptableRendererFeature
    {
        private OpaquePass _opaquePass;

        public override void Create()
        {
            _opaquePass = new OpaquePass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(_opaquePass);
        }

        protected override void Dispose(bool disposing)
        {
            // OpaquePass holds no unmanaged resources.
            // Override retained as a cleanup hook for future buffer passes.
        }
    }
}
