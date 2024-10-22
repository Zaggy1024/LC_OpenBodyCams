using System;
using System.Reflection;
using System.Reflection.Emit;

using HarmonyLib;

namespace OpenBodyCams.Utilities.IL;

internal static class InstructionUtilities
{

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
}
