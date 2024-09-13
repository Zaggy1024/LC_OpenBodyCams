using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering.RendererUtils;

namespace OpenBodyCams.Overlay
{
    public class TransparentRenderTexturePass : CustomPass
    {
        public RenderTexture targetTexture;

        private static ShaderTagId[] depthPrepassTags;
        private static ShaderTagId[] forwardTags;

        protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
        {
            depthPrepassTags = [
                HDShaderPassNames.s_DepthForwardOnlyName,
                HDShaderPassNames.s_ForwardOnlyName,
                HDShaderPassNames.s_DepthOnlyName,
                HDShaderPassNames.s_TransparentDepthPrepassName,
                HDShaderPassNames.s_TransparentBackfaceName,
            ];
            forwardTags = [
                HDShaderPassNames.s_ForwardOnlyName,
                HDShaderPassNames.s_ForwardName,
                HDShaderPassNames.s_SRPDefaultUnlitName,
                HDShaderPassNames.s_DecalMeshForwardEmissiveName,
            ];
        }

        private void Render(ref CustomPassContext ctx, in RenderQueueRange range, SortingCriteria sorting, ShaderTagId[] shaderTags, PerObjectData configuration, bool fptl)
        {
            var camera = ctx.hdCamera.camera;
            RendererList rendererList = ctx.renderContext.CreateRendererList(new RendererListDesc(shaderTags, ctx.cullingResults, camera)
            {
                renderQueueRange = range,
                rendererConfiguration = configuration,
                sortingCriteria = sorting,
                excludeObjectMotionVectors = false,
                layerMask = camera.cullingMask,
            });
            CoreUtils.SetKeyword(ctx.cmd, "USE_FPTL_LIGHTLIST", fptl);
            CoreUtils.SetKeyword(ctx.cmd, "USE_CLUSTERED_LIGHTLIST", !fptl);
            CoreUtils.DrawRendererList(ctx.renderContext, ctx.cmd, rendererList);
        }

        private bool ShouldUseFPTL(in FrameSettings frameSettings)
        {
            if (frameSettings.litShaderMode == LitShaderMode.Deferred)
                return true;
            return frameSettings.IsEnabled(FrameSettingsField.FPTLForForwardOpaque);
        }

        protected override void Execute(CustomPassContext ctx)
        {
            ctx.cmd.SetRenderTarget(targetTexture.colorBuffer, targetTexture.depthBuffer);
            ctx.cmd.ClearRenderTarget(true, true, Color.clear);

            var frameSettings = ctx.hdCamera.frameSettings;
            PerObjectData rendererConfiguration = HDUtils.GetRendererConfiguration(frameSettings.IsEnabled(FrameSettingsField.ProbeVolume), frameSettings.IsEnabled(FrameSettingsField.Shadowmask));

            Render(ref ctx, RenderQueueRange.opaque, SortingCriteria.CommonOpaque, depthPrepassTags, rendererConfiguration, true);

            Render(ref ctx, RenderQueueRange.opaque, SortingCriteria.CommonOpaque, forwardTags, rendererConfiguration, ShouldUseFPTL(frameSettings));

            Render(ref ctx, RenderQueueRange.transparent, SortingCriteria.CommonTransparent, forwardTags, rendererConfiguration, false);
        }
    }
}
