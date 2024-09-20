﻿using System;
using System.Reflection;

namespace OpenBodyCams.Patches;

public static class Reflection
{
    public static readonly MethodInfo m_StartOfRound_get_Instance = typeof(StartOfRound).GetMethod("get_Instance", []);
    public static readonly FieldInfo f_StartOfRound_thisClientPlayerId = typeof(StartOfRound).GetField(nameof(StartOfRound.thisClientPlayerId));

    public static readonly MethodInfo m_GameNetworkManager_get_Instance = typeof(GameNetworkManager).GetMethod("get_Instance", []);
    public static readonly FieldInfo f_GameNetworkManager_localPlayerController = typeof(GameNetworkManager).GetField(nameof(GameNetworkManager.localPlayerController));

    public static readonly MethodInfo m_Object_op_Equality = typeof(UnityEngine.Object).GetMethod("op_Equality", [typeof(UnityEngine.Object), typeof(UnityEngine.Object)]);
    public static readonly MethodInfo m_Object_op_Inequality = typeof(UnityEngine.Object).GetMethod("op_Inequality", [typeof(UnityEngine.Object), typeof(UnityEngine.Object)]);

    public static MethodInfo GetMethod(this Type type, string name, BindingFlags bindingFlags, Type[] parameters)
    {
        return type.GetMethod(name, bindingFlags, null, parameters, null);
    }
}
