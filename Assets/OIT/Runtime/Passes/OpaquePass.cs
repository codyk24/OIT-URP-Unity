using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace OIT
{
    public sealed class OpaquePass : ScriptableRenderPass
    {
        private static readonly List<ShaderTagId> s_ShaderTagIds = new List<ShaderTagId>
        {
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("LightweightForward"),
            new ShaderTagId("SRPDefaultUnlit"),
        };

        private class PassData
        {
            internal RendererListHandle RendererList;
        }

        public OpaquePass()
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
            profilingSampler = new ProfilingSampler("OIT.OpaquePass");
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var renderingData = frameData.Get<UniversalRenderingData>();
            var cameraData    = frameData.Get<UniversalCameraData>();
            var lightData     = frameData.Get<UniversalLightData>();
            var resourceData  = frameData.Get<UniversalResourceData>();

            var drawSettings  = RenderingUtils.CreateDrawingSettings(
                s_ShaderTagIds, renderingData, cameraData, lightData,
                SortingCriteria.CommonOpaque);
            var filterSettings = new FilteringSettings(RenderQueueRange.opaque);

            using var builder = renderGraph.AddRasterRenderPass<PassData>(
                "OIT.OpaquePass", out var passData, profilingSampler);

            passData.RendererList = renderGraph.CreateRendererList(
                new RendererListParams(renderingData.cullResults, drawSettings, filterSettings));

            builder.UseRendererList(passData.RendererList);
            builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);
            builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.ReadWrite);

            builder.SetRenderFunc(static (PassData data, RasterGraphContext ctx) =>
            {
                ctx.cmd.DrawRendererList(data.RendererList);
            });
        }
    }
}
