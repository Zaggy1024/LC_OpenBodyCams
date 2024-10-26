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

namespace OpenBodyCams.Patches;

[HarmonyPatch(typeof(HDRenderPipeline))]
internal static class PatchHDRenderPipeline
{
    internal static Action<ScriptableRenderContext, Camera> BeforeCameraCulling;
    internal static Action<ScriptableRenderContext, Camera> BeforeCameraRendering;

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

        injector.Back(1)
            .Insert([
                new(OpCodes.Dup),
                new(OpCodes.Ldarg_1),
                new(OpCodes.Call, typeof(PatchHDRenderPipeline).GetMethod(nameof(BeforeCameraRenderingHook), BindingFlags.NonPublic | BindingFlags.Static, [typeof(HDRenderPipeline.RenderRequest), typeof(ScriptableRenderContext)])),
            ]);

        return injector.ReleaseInstructions();
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
