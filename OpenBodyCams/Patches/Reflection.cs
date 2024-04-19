using System;
using System.Reflection;

using UnityEngine;

namespace OpenBodyCams.Patches
{
    public static class Reflection
    {
        public static readonly MethodInfo m_StartOfRound_get_Instance = typeof(StartOfRound).GetMethod("get_Instance", []);
        public static readonly FieldInfo f_StartOfRound_thisClientPlayerId = typeof(StartOfRound).GetField(nameof(StartOfRound.thisClientPlayerId));

        public static MethodInfo GetMethod(this Type type, string name, BindingFlags bindingFlags, Type[] parameters)
        {
            return type.GetMethod(name, bindingFlags, null, parameters, null);
        }
    }
}
