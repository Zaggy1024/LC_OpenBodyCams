using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

using OpenBodyCams.Utilities.IL;
using UnityEngine.Experimental.Rendering;

using static UnityEngine.Rendering.HighDefinition.RenderPipelineSettings;

namespace OpenBodyCams.Patches;

[HarmonyPatch(typeof(HDRenderPipeline))]
internal static class PatchHDRenderPipeline
{
    internal static Action<ScriptableRenderContext, Camera> BeforeCameraCulling;
    internal static Action<ScriptableRenderContext, Camera> BeforeCameraRendering;

    private static ColorBufferFormat? originalColorBufferFormat = null;
    internal static bool ForceEnableAlpha
    {
        get
        {
            return originalColorBufferFormat.HasValue;
        }
        set
        {
            ref var settings = ref ((HDRenderPipelineAsset)GraphicsSettings.currentRenderPipeline).m_RenderPipelineSettings;

            var prevFormat = settings.colorBufferFormat;

            if (!value)
            {
                if (originalColorBufferFormat.HasValue)
                {
                    settings.colorBufferFormat = originalColorBufferFormat.Value;
                    originalColorBufferFormat = null;
                }
            }
            else
            {
                if (!originalColorBufferFormat.HasValue)
                    originalColorBufferFormat = settings.colorBufferFormat;
                settings.colorBufferFormat = ColorBufferFormat.R16G16B16A16;
            }

            if (settings.colorBufferFormat != prevFormat)
            {
                var pipeline = (HDRenderPipeline)RenderPipelineManager.currentPipeline;
                var alpha = settings.SupportsAlpha();
                pipeline.m_EnableAlpha = alpha && settings.postProcessSettings.supportsAlpha;
                pipeline.m_KeepAlpha = alpha;
            }
        }
    }

    [HarmonyTranspiler]
    [HarmonyPatch(nameof(HDRenderPipeline.Render), [typeof(ScriptableRenderContext), typeof(List<Camera>)])]
    private static IEnumerable<CodeInstruction> RenderTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var injector = new ILInjector(instructions);

        // + BeforeCameraCullingHook(camera, context);
        //   if (PrepareAndCullCamera(camera, xrPass, flag, value, renderContext, out var renderRequest))
        injector
            .Find([
                ILMatcher.Call(typeof(HDRenderPipeline).GetMethod(nameof(HDRenderPipeline.PrepareAndCullCamera), BindingFlags.NonPublic | BindingFlags.Instance, [typeof(Camera), typeof(XRPass), typeof(bool), typeof(List<HDRenderPipeline.RenderRequest>), typeof(ScriptableRenderContext), typeof(HDRenderPipeline.RenderRequest).MakeByRefType(), typeof(CubemapFace)])),
            ])
            .GoToPush(6)
            .Forward(1);

        if (!injector.IsValid)
        {
            Plugin.Instance.Logger.LogError($"Failed to find the HDRP culling loop");
            return instructions;
        }
        injector
            .InsertInPlace([
                new(OpCodes.Dup),
                new(OpCodes.Ldarg_1),
                new(OpCodes.Call, typeof(PatchHDRenderPipeline).GetMethod(nameof(BeforeCameraCullingHook), BindingFlags.NonPublic | BindingFlags.Static, [typeof(Camera), typeof(ScriptableRenderContext)])),
            ]);


        //   RenderRequest request;
        // - request = flattenedRequests[requestIndex];
        // + BeforeCameraCullingHook(request = flattenedRequests[requestIndex]);
        //   ..
        //   ExecuteRenderRequest(request, context, commandBuffer, AOVRequestData.defaultAOVRequestDataNonAlloc);
        injector
            .Find([
                ILMatcher.Ldloc(),
                ILMatcher.Ldloc(),
                ILMatcher.Callvirt(typeof(List<HDRenderPipeline.RenderRequest>).GetMethod("get_Item", [typeof(int)])),
                ILMatcher.Stloc(),
            ])
            .GoToMatchEnd();

        if (!injector.IsValid)
        {
            Plugin.Instance.Logger.LogError($"Failed to find the HDRP render loop");
            return instructions;
        }

        return injector.Back(1)
            .InsertInPlace([
                new(OpCodes.Dup),
                new(OpCodes.Ldarg_1),
                new(OpCodes.Call, typeof(PatchHDRenderPipeline).GetMethod(nameof(BeforeCameraRenderingHook), BindingFlags.NonPublic | BindingFlags.Static, [typeof(HDRenderPipeline.RenderRequest), typeof(ScriptableRenderContext)])),
            ])
            .ReleaseInstructions();
    }

    private static void BeforeCameraCullingHook(Camera camera, ScriptableRenderContext context)
    {
        BeforeCameraCulling?.Invoke(context, camera);
    }

    private static void BeforeCameraRenderingHook(HDRenderPipeline.RenderRequest request, ScriptableRenderContext context)
    {
        BeforeCameraRendering?.Invoke(context, request.hdCamera.camera);
    }
}
