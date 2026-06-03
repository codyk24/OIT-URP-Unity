using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace OIT
{
    /// <summary>
    /// URP RenderGraph pass that paints the CSG cap colour at every pixel where
    /// <see cref="CSGStencilPass"/> left a non-zero stencil value.
    ///
    /// A fullscreen triangle is drawn with the <c>OIT/CapFace</c> shader, which tests
    /// <c>Stencil { Ref 0; Comp NotEqual }</c> so only interior-of-cutter pixels are
    /// affected. ZWrite Off preserves the original scene depth so that transparent
    /// passes scheduled after this one can depth-test correctly (PRD §9, cap z-fighting risk).
    ///
    /// Scheduled one slot after CSGStencilPass (<see cref="RenderPassEvent.AfterRenderingOpaques"/> + 1)
    /// to guarantee the stencil values are fully written before this pass reads them.
    /// </summary>
    public sealed class CapFacePass : ScriptableRenderPass
    {
        private static readonly int s_CapColorId = Shader.PropertyToID("_CapColor");

        private Material _capMaterial;

        private class PassData
        {
            internal Material Material;
            internal Color    CapColor;
        }

        public CapFacePass()
        {
            // +1 ensures this executes after CSGStencilPass (also AfterRenderingOpaques).
            renderPassEvent  = (RenderPassEvent)((int)RenderPassEvent.AfterRenderingOpaques + 1);
            profilingSampler = new ProfilingSampler("OIT.CapFacePass");
        }

        /// <summary>
        /// Loads the CapFace material. Called once from
        /// <see cref="OITRenderFeature.Create"/>. Returns false if the shader is missing.
        /// </summary>
        public bool TryInitialize()
        {
            var shader = Shader.Find("OIT/CapFace");
            if (shader == null)
            {
                Debug.LogError(
                    "[CapFacePass] Shader 'OIT/CapFace' not found. " +
                    "Ensure Assets/Shaders/CapFace.shader exists and has been imported.");
                return false;
            }
            _capMaterial = CoreUtils.CreateEngineMaterial(shader);
            return true;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_capMaterial == null)
                return;

            var system = CSGSystem.Instance;
            if (system == null || system.Cutters.Count == 0)
                return;

            var resourceData = frameData.Get<UniversalResourceData>();

            using var builder = renderGraph.AddRasterRenderPass<PassData>(
                "OIT.CapFacePass", out var passData, profilingSampler);

            passData.Material = _capMaterial;
            // Use the first registered cutter's cap colour. Multiple cutters share the same
            // stencil plane in this implementation; blending per-cutter colours is out of scope.
            passData.CapColor = system.Cutters[0].capColor;

            // Write cap colour to the active scene colour attachment.
            builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);

            // Read stencil from the depth attachment written by CSGStencilPass.
            // AccessFlags.Read: stencil test only — this pass does not write depth or stencil.
            builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Read);

            // No RendererList — RenderGraph cannot infer output; disable automatic pass culling.
            builder.AllowPassCulling(false);

            builder.SetRenderFunc(static (PassData data, RasterGraphContext ctx) =>
            {
                data.Material.SetColor(s_CapColorId, data.CapColor);
                ctx.cmd.DrawProcedural(
                    Matrix4x4.identity, data.Material, 0, MeshTopology.Triangles, 3);
            });
        }

        /// <summary>Called by <see cref="OITRenderFeature.Dispose"/>.</summary>
        public void Dispose()
        {
            CoreUtils.Destroy(_capMaterial);
            _capMaterial = null;
        }
    }
}
