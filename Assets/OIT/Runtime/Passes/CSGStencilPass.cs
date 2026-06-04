using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace OIT
{
    /// <summary>
    /// URP RenderGraph pass that writes stencil for CSG inside-outside detection.
    ///
    /// For each registered cutter mesh, two draw calls are issued with ZTest Greater
    /// (passes when the drawn surface is behind existing scene depth):
    ///
    ///   Pass 0 — back faces (Cull Front):  ZTest Greater passes → IncrSat
    ///            marks pixels where the cutter's far side is behind scene geometry,
    ///            meaning the scene geometry is inside the cutter volume.
    ///
    ///   Pass 1 — front faces (Cull Back):  ZTest Greater passes → DecrSat
    ///            removes false positives (e.g. camera inside cutter, target in front).
    ///
    /// Net stencil > 0 only where scene geometry lies inside the cutter volume.
    /// ColorMask 0 on both passes — no colour is written to the render target.
    ///
    /// Scheduled at AfterRenderingOpaques so scene depth is established before testing.
    /// </summary>
    public sealed class CSGStencilPass : ScriptableRenderPass
    {
        private const int k_BackFacePass  = 0;
        private const int k_FrontFacePass = 1;

        private Material _stencilMaterial;

        private class PassData
        {
            internal Mesh[]      Meshes;
            internal Matrix4x4[] Matrices;
            internal int         Count;
            internal Material    Material;
            internal string      PassName;
        }

        public CSGStencilPass()
        {
            renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
            profilingSampler = new ProfilingSampler("OIT.CSGStencilPass");
        }

        /// <summary>
        /// Loads the CSGStencil material. Called once from
        /// <see cref="OITRenderFeature.Create"/>. Returns false if the shader is missing.
        /// </summary>
        public bool TryInitialize()
        {
            var shader = Shader.Find("OIT/CSGStencil");
            if (shader == null)
            {
                Debug.LogError(
                    "[CSGStencilPass] Shader 'OIT/CSGStencil' not found. " +
                    "Ensure Assets/Shaders/CSGStencil.shader exists and has been imported.");
                return false;
            }
            _stencilMaterial = CoreUtils.CreateEngineMaterial(shader);
            return true;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_stencilMaterial == null)
                return;

            var system = CSGSystem.Instance;
            if (system == null || system.Cutters.Count == 0)
                return;

            var resourceData = frameData.Get<UniversalResourceData>();

            using var builder = renderGraph.AddRasterRenderPass<PassData>(
                "OIT.CSGStencilPass", out var passData, profilingSampler);

            int count = system.Cutters.Count;
            passData.Count    = count;
            passData.Material = _stencilMaterial;
            passData.PassName = "OIT.CSGStencilPass";
            passData.Meshes   = new Mesh[count];
            passData.Matrices = new Matrix4x4[count];

            for (int i = 0; i < count; i++)
            {
                passData.Meshes[i]   = system.Cutters[i].CutterMesh;
                passData.Matrices[i] = system.Cutters[i].LocalToWorld;
            }

            // Declare Read (not Write) on colour: we write only depth/stencil.
            // Read avoids the load/store pair that tile-based GPUs (Metal) insert for
            // Write attachments, preventing a spurious colour clear.
            builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Read);
            builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.ReadWrite);

            // No RendererList → RenderGraph cannot infer output; pass culling must be disabled.
            builder.AllowPassCulling(false);

            builder.SetRenderFunc(static (PassData data, RasterGraphContext ctx) =>
            {
                OITPassOrder.Record(data.PassName);
                for (int i = 0; i < data.Count; i++)
                {
                    if (data.Meshes[i] == null)
                        continue;

                    ctx.cmd.DrawMesh(data.Meshes[i], data.Matrices[i], data.Material, 0, k_BackFacePass);
                    ctx.cmd.DrawMesh(data.Meshes[i], data.Matrices[i], data.Material, 0, k_FrontFacePass);
                }
            });
        }

        /// <summary>Called by <see cref="OITRenderFeature.Dispose"/>.</summary>
        public void Dispose()
        {
            CoreUtils.Destroy(_stencilMaterial);
            _stencilMaterial = null;
        }
    }
}
