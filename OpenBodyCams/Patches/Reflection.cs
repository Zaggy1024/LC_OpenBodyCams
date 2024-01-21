using System;
using System.Reflection;

using UnityEngine;

namespace OpenBodyCams.Patches
{
    public static class Reflection
    {
        public static readonly MethodInfo m_Debug_Log = typeof(Debug).GetMethod(nameof(Debug.Log), new Type[] { typeof(object) });

        public static readonly MethodInfo m_Behaviour_set_enabled = typeof(Behaviour).GetMethod("set_enabled", new Type[] { typeof(bool) });

        public static readonly MethodInfo m_StartOfRound_get_Instance = typeof(StartOfRound).GetMethod("get_Instance", new Type[0]);
        public static readonly FieldInfo f_StartOfRound_thisClientPlayerId = typeof(StartOfRound).GetField(nameof(StartOfRound.thisClientPlayerId));
    }
}
