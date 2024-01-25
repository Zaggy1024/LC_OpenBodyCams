using System;
using System.Reflection;

using UnityEngine;

namespace OpenBodyCams.Patches
{
    public static class Reflection
    {
        public static readonly MethodInfo m_Debug_Log = typeof(Debug).GetMethod(nameof(Debug.Log), new Type[] { typeof(object) });

        public static readonly MethodInfo m_GameObject_set_layer = typeof(GameObject).GetMethod("set_layer", new Type[] { typeof(int) });

        public static readonly MethodInfo m_StartOfRound_get_Instance = typeof(StartOfRound).GetMethod("get_Instance", new Type[0]);
        public static readonly FieldInfo f_StartOfRound_thisClientPlayerId = typeof(StartOfRound).GetField(nameof(StartOfRound.thisClientPlayerId));
    }
}
