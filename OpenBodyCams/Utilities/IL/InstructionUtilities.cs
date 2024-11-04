using System;
using System.Reflection;
using System.Reflection.Emit;

using HarmonyLib;

namespace OpenBodyCams.Utilities.IL;

internal static class InstructionUtilities
{

    public static CodeInstruction MakeLdarg(int index)
    {
        return index switch
        {
            0 => new CodeInstruction(OpCodes.Ldarg_0),
            1 => new CodeInstruction(OpCodes.Ldarg_1),
            2 => new CodeInstruction(OpCodes.Ldarg_2),
            3 => new CodeInstruction(OpCodes.Ldarg_3),
            < 256 => new CodeInstruction(OpCodes.Ldarg_S, index),
            _ => new CodeInstruction(OpCodes.Ldarg, index),
        };
    }

    public static int PopCount(this CodeInstruction instruction)
    {
        if (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt)
        {
            var method = (MethodInfo)instruction.operand;
            var parameterCount = method.GetParameters().Length;
            if (!method.IsStatic)
                parameterCount++;
            return parameterCount;
        }

        if (instruction.opcode == OpCodes.Ret)
            return 1;

        return instruction.opcode.StackBehaviourPop switch
        {
            StackBehaviour.Pop0 => 0,
            StackBehaviour.Pop1 => 1,
            StackBehaviour.Pop1_pop1 => 2,
            StackBehaviour.Popi => 1,
            StackBehaviour.Popi_pop1 => 2,
            StackBehaviour.Popi_popi => 2,
            StackBehaviour.Popi_popi8 => 2,
            StackBehaviour.Popi_popi_popi => 3,
            StackBehaviour.Popi_popr4 => 2,
            StackBehaviour.Popi_popr8 => 2,
            StackBehaviour.Popref => 1,
            StackBehaviour.Popref_pop1 => 2,
            StackBehaviour.Popref_popi => 2,
            StackBehaviour.Popref_popi_popi => 3,
            StackBehaviour.Popref_popi_popi8 => 3,
            StackBehaviour.Popref_popi_popr4 => 3,
            StackBehaviour.Popref_popi_popr8 => 3,
            StackBehaviour.Popref_popi_popref => 3,
            StackBehaviour.Varpop => throw new NotImplementedException($"Variable pop on non-call instruction '{instruction}'"),
            StackBehaviour.Popref_popi_pop1 => 3,
            _ => throw new NotSupportedException($"StackBehaviourPop of {instruction.opcode.StackBehaviourPop} was not a pop for instruction '{instruction}'"),
        };
    }

    public static int PushCount(this CodeInstruction instruction)
    {
        if (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt)
        {
            var method = (MethodInfo)instruction.operand;
            if (method.ReturnType == typeof(void))
                return 0;
            return 1;
        }

        return instruction.opcode.StackBehaviourPush switch
        {
            StackBehaviour.Push0 => 0,
            StackBehaviour.Push1 => 1,
            StackBehaviour.Push1_push1 => 2,
            StackBehaviour.Pushi => 1,
            StackBehaviour.Pushi8 => 1,
            StackBehaviour.Pushr4 => 1,
            StackBehaviour.Pushr8 => 1,
            StackBehaviour.Pushref => 1,
            StackBehaviour.Varpush => throw new NotImplementedException($"Variable push on non-call instruction '{instruction}'"),
            _ => throw new NotSupportedException($"StackBehaviourPush of {instruction.opcode.StackBehaviourPush} was not a push for instruction '{instruction}'"),
        };
    }

    public static int? GetLdargIndex(this CodeInstruction instruction)
    {
        var opcode = instruction.opcode;
        if (opcode == OpCodes.Ldarg_0)
            return 0;
        if (opcode == OpCodes.Ldarg_1)
            return 1;
        if (opcode == OpCodes.Ldarg_2)
            return 2;
        if (opcode == OpCodes.Ldarg_3)
            return 3;
        if (opcode == OpCodes.Ldarg || opcode == OpCodes.Ldarg_S)
            return instruction.operand as int?;
        return null;
    }

    public static int? GetLdlocIndex(this CodeInstruction instruction)
    {
        var opcode = instruction.opcode;
        if (opcode == OpCodes.Ldloc_0)
            return 0;
        if (opcode == OpCodes.Ldloc_1)
            return 1;
        if (opcode == OpCodes.Ldloc_2)
            return 2;
        if (opcode == OpCodes.Ldloc_3)
            return 3;
        if (opcode == OpCodes.Ldloc || opcode == OpCodes.Ldloc_S)
            return (instruction.operand as LocalBuilder)?.LocalIndex;
        return null;
    }

    public static int? GetStlocIndex(this CodeInstruction instruction)
    {
        var opcode = instruction.opcode;
        if (opcode == OpCodes.Stloc_0)
            return 0;
        if (opcode == OpCodes.Stloc_1)
            return 1;
        if (opcode == OpCodes.Stloc_2)
            return 2;
        if (opcode == OpCodes.Stloc_3)
            return 3;
        if (opcode == OpCodes.Stloc || opcode == OpCodes.Stloc_S)
            return (instruction.operand as LocalBuilder)?.LocalIndex;
        return null;
    }

    public static CodeInstruction LdlocToStloc(this CodeInstruction instruction)
    {
        var opcode = instruction.opcode;
        if (opcode == OpCodes.Ldloc_0)
            return new CodeInstruction(OpCodes.Stloc_0);
        if (opcode == OpCodes.Ldloc_1)
            return new CodeInstruction(OpCodes.Stloc_1);
        if (opcode == OpCodes.Ldloc_2)
            return new CodeInstruction(OpCodes.Stloc_2);
        if (opcode == OpCodes.Ldloc_3)
            return new CodeInstruction(OpCodes.Stloc_3);
        if (opcode == OpCodes.Ldloc || opcode == OpCodes.Ldloc_S)
            return new CodeInstruction(OpCodes.Stloc, instruction.operand);
        return null;
    }

    public static CodeInstruction StlocToLdloc(this CodeInstruction instruction)
    {
        var opcode = instruction.opcode;
        if (opcode == OpCodes.Stloc_0)
            return new CodeInstruction(OpCodes.Ldloc_0);
        if (opcode == OpCodes.Stloc_1)
            return new CodeInstruction(OpCodes.Ldloc_1);
        if (opcode == OpCodes.Stloc_2)
            return new CodeInstruction(OpCodes.Ldloc_2);
        if (opcode == OpCodes.Stloc_3)
            return new CodeInstruction(OpCodes.Ldloc_3);
        if (opcode == OpCodes.Stloc || opcode == OpCodes.Stloc_S)
            return new CodeInstruction(OpCodes.Ldloc, instruction.operand);
        return null;
    }

    public static int? GetLdcI32(this CodeInstruction instruction)
    {
        var opcode = instruction.opcode;
        if (opcode == OpCodes.Ldc_I4_M1)
            return -1;
        if (opcode == OpCodes.Ldc_I4_0)
            return 0;
        if (opcode == OpCodes.Ldc_I4_1)
            return 1;
        if (opcode == OpCodes.Ldc_I4_2)
            return 2;
        if (opcode == OpCodes.Ldc_I4_3)
            return 3;
        if (opcode == OpCodes.Ldc_I4_4)
            return 4;
        if (opcode == OpCodes.Ldc_I4_5)
            return 5;
        if (opcode == OpCodes.Ldc_I4_6)
            return 6;
        if (opcode == OpCodes.Ldc_I4_7)
            return 7;
        if (opcode == OpCodes.Ldc_I4_8)
            return 8;
        if (opcode == OpCodes.Ldc_I4 || opcode == OpCodes.Ldc_I4_S)
            return instruction.operand as int?;
        return null;
    }
}
