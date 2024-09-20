using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;

using HarmonyLib;

namespace OpenBodyCams.Patches;

public class SequenceMatch(int start, int end)
{
    public int Start = start;
    public int End = end;

    public int Size { get => End - Start; }
}

public static class Common
{
    public static IEnumerable<T> IndexRangeView<T>(this List<T> list, int start, int end)
    {
        for (int i = start; i < end; i++)
            yield return list[i];
    }

    public static IEnumerable<T> IndexRangeView<T>(this List<T> list, SequenceMatch range)
    {
        return list.IndexRangeView(range.Start, range.End);
    }

    public static void RemoveAbsoluteRange<T>(this List<T> list, int start, int end)
    {
        list.RemoveRange(start, end - start);
    }

    public static void RemoveRange<T>(this List<T> list, SequenceMatch range)
    {
        list.RemoveAbsoluteRange(range.Start, range.End);
    }

    public static SequenceMatch FindIndexOfSequence<T>(this List<T> list, int startIndex, IEnumerable<Predicate<T>> predicates)
    {
        var index = startIndex;
        while (index < list.Count())
        {
            var predicateEnumerator = predicates.GetEnumerator();
            if (!predicateEnumerator.MoveNext())
                return null;
            index = list.FindIndex(index, predicateEnumerator.Current);

            if (index < 0)
                break;

            bool matches = true;
            var sequenceIndex = 1;
            while (predicateEnumerator.MoveNext())
            {
                if (sequenceIndex >= list.Count() - index
                    || !predicateEnumerator.Current(list[index + sequenceIndex]))
                {
                    matches = false;
                    break;
                }
                sequenceIndex++;
            }

            if (matches)
                return new SequenceMatch(index, index + predicates.Count());
            index++;
        }

        return null;
    }

    public static SequenceMatch FindIndexOfSequence<T>(this List<T> list, IEnumerable<Predicate<T>> predicates)
    {
        return FindIndexOfSequence(list, 0, predicates);
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
            StackBehaviour.Varpop => throw new NotImplementedException("Variable pop on non-call instruction"),
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
            StackBehaviour.Varpush => throw new NotImplementedException("Variable push on non-call instruction"),
            _ => throw new NotSupportedException($"StackBehaviourPush of {instruction.opcode.StackBehaviourPush} was not a push for instruction '{instruction}'"),
        };
    }

    public static SequenceMatch InstructionRangeForStackItems(this List<CodeInstruction> instructions, int instructionIndex, int startIndex, int endIndex)
    {
        int start = -1;
        int end = -1;

        instructionIndex--;
        int stackPosition = 0;
        while (instructionIndex >= 0)
        {
            var instruction = instructions[instructionIndex];
            var pushes = instruction.PushCount();

            if (end == -1 && stackPosition == startIndex && pushes > 0)
                end = instructionIndex + 1;

            stackPosition += instruction.PushCount();
            stackPosition -= instruction.PopCount();

            if (stackPosition > endIndex)
            {
                start = instructionIndex;
                break;
            }

            instructionIndex--;
        }

        if (start == -1 || end == -1)
            return null;

        return new SequenceMatch(start, end);
    }

    public static int GetLocalIndex(this CodeInstruction instruction)
    {
        if (instruction.opcode == OpCodes.Ldloc_0 || instruction.opcode == OpCodes.Stloc_0)
            return 0;
        if (instruction.opcode == OpCodes.Ldloc_1 || instruction.opcode == OpCodes.Stloc_1)
            return 1;
        if (instruction.opcode == OpCodes.Ldloc_2 || instruction.opcode == OpCodes.Stloc_2)
            return 2;
        if (instruction.opcode == OpCodes.Ldloc_3 || instruction.opcode == OpCodes.Stloc_3)
            return 3;

        if (instruction.opcode != OpCodes.Ldloc && instruction.opcode != OpCodes.Ldloc_S
            && instruction.opcode != OpCodes.Ldloca && instruction.opcode != OpCodes.Ldloca_S
            && instruction.opcode != OpCodes.Stloc && instruction.opcode != OpCodes.Stloc_S)
            return -1;

        return ((LocalBuilder)instruction.operand).LocalIndex;
    }

    public static CodeInstruction CreateStloc(LocalBuilder local)
    {
        return local.LocalIndex switch
        {
            0 => new CodeInstruction(OpCodes.Stloc_0),
            1 => new CodeInstruction(OpCodes.Stloc_1),
            2 => new CodeInstruction(OpCodes.Stloc_2),
            3 => new CodeInstruction(OpCodes.Stloc_3),
            < 256 => new CodeInstruction(OpCodes.Stloc_S, local),
            _ => new CodeInstruction(OpCodes.Stloc, local),
        };
    }

    public static CodeInstruction CreateLdloc(LocalBuilder local)
    {
        return local.LocalIndex switch
        {
            0 => new CodeInstruction(OpCodes.Ldloc_0),
            1 => new CodeInstruction(OpCodes.Ldloc_1),
            2 => new CodeInstruction(OpCodes.Ldloc_2),
            3 => new CodeInstruction(OpCodes.Ldloc_3),
            < 256 => new CodeInstruction(OpCodes.Ldloc_S, local),
            _ => new CodeInstruction(OpCodes.Ldloc, local),
        };
    }

    public static CodeInstruction CreateLdloca(LocalBuilder local)
    {
        return local.LocalIndex switch
        {
            < 256 => new CodeInstruction(OpCodes.Ldloca_S, local),
            _ => new CodeInstruction(OpCodes.Ldloca, local),
        };
    }

    public static Type GetPushType(MethodBase method, List<CodeInstruction> instructions, int index)
    {
        var pushInstruction = instructions[index];
        var opcode = pushInstruction.opcode;

        if (opcode == OpCodes.Call || opcode == OpCodes.Callvirt)
            return ((MethodInfo)pushInstruction.operand).ReturnType;
        if (opcode == OpCodes.Calli)
        {
            Plugin.Instance.Logger.LogWarning($"Calli instruction is unsupported by GetPushType. Operand: {pushInstruction.operand}");
            return null;
        }

        if (opcode == OpCodes.Add
            || opcode == OpCodes.Mul
            || opcode == OpCodes.Mul_Ovf
            || opcode == OpCodes.Neg
            || opcode == OpCodes.Not
            || opcode == OpCodes.Or
            || opcode == OpCodes.Sub
            || opcode == OpCodes.Sub_Ovf
            || opcode == OpCodes.Rem
            || opcode == OpCodes.Shl
            || opcode == OpCodes.Shr
            || opcode == OpCodes.Xor
            || opcode == OpCodes.Add_Ovf
            || opcode == OpCodes.Mul_Ovf_Un
            || opcode == OpCodes.Sub_Ovf_Un
            || opcode == OpCodes.Rem_Un
            || opcode == OpCodes.Shr_Un
            || opcode == OpCodes.Add_Ovf_Un
            || opcode == OpCodes.And
            || opcode == OpCodes.Div
            || opcode == OpCodes.Div_Un)
        {
            return GetPushType(method, instructions, instructions.InstructionRangeForStackItems(index, 0, 0).End - 1)
                ?? GetPushType(method, instructions, instructions.InstructionRangeForStackItems(index, 1, 1).End - 1);
        }

        if (opcode == OpCodes.Ldloca
            || opcode == OpCodes.Ldloca_S
            || opcode == OpCodes.Ldloc_0
            || opcode == OpCodes.Ldloc_1
            || opcode == OpCodes.Ldloc_2
            || opcode == OpCodes.Ldloc_3
            || opcode == OpCodes.Ldloc
            || opcode == OpCodes.Ldloc_S)
        {
            var variableIndex = pushInstruction.GetLocalIndex();
            var stloc = instructions.FindLastIndex(index - 1, insn => insn.IsStloc() && insn.GetLocalIndex() == variableIndex);
            var push = instructions.InstructionRangeForStackItems(stloc, 0, 0);
            return GetPushType(method, instructions, push.End - 1);
        }

        if (opcode == OpCodes.Ldobj)
            return (Type)pushInstruction.operand;

        if (opcode == OpCodes.Ldsfld
            || opcode == OpCodes.Ldsflda
            || opcode == OpCodes.Ldfld
            || opcode == OpCodes.Ldflda)
            return ((FieldInfo)pushInstruction.operand).FieldType;

        if (opcode == OpCodes.Ldstr)
            return typeof(string);
        if (opcode == OpCodes.Ldtoken)
        {
            if (pushInstruction.operand is MethodInfo)
                return typeof(RuntimeMethodHandle);
            if (pushInstruction.operand is FieldInfo)
                return typeof(RuntimeFieldHandle);
            if (pushInstruction.operand is Type)
                return typeof(RuntimeTypeHandle);
            Plugin.Instance.Logger.LogError($"Unknown operand type for ldtoken found: {pushInstruction.operand}");
            return null;
        }

        if (opcode == OpCodes.Ldvirtftn
            || opcode == OpCodes.Localloc)
            return typeof(nint);

        if (opcode == OpCodes.Mkrefany)
            return (Type)pushInstruction.operand;

        if (opcode == OpCodes.Newarr)
            return ((Type)pushInstruction.operand).MakeArrayType();
        if (opcode == OpCodes.Newobj)
            return (Type)pushInstruction.operand;

        if (opcode == OpCodes.Ldelem_I)
            return typeof(nint);
        if (opcode == OpCodes.Ldelem_I1
            || opcode == OpCodes.Ldelem_I2
            || opcode == OpCodes.Ldelem_I4)
            return typeof(int);
        if (opcode == OpCodes.Ldelem_I8)
            return typeof(long);
        if (opcode == OpCodes.Ldelem_R4)
            return typeof(float);
        if (opcode == OpCodes.Ldelem_R8)
            return typeof(double);
        if (opcode == OpCodes.Ldelem_U1
            || opcode == OpCodes.Ldelem_U2
            || opcode == OpCodes.Ldelem_U4)
            return typeof(uint);
        if (opcode == OpCodes.Ldelem_Ref)
            return typeof(object);
        if (opcode == OpCodes.Ldelem)
            return (Type)pushInstruction.operand;
        if (opcode == OpCodes.Ldelema)
            return ((Type)pushInstruction.operand).MakeByRefType();

        if (opcode == OpCodes.Ldftn)
            return typeof(MethodInfo);


        if (opcode == OpCodes.Ldind_I)
            return typeof(nint);
        if (opcode == OpCodes.Ldind_I1
            || opcode == OpCodes.Ldind_I2
            || opcode == OpCodes.Ldind_I4)
            return typeof(int);
        if (opcode == OpCodes.Ldind_I8)
            return typeof(long);
        if (opcode == OpCodes.Ldind_R4)
            return typeof(float);
        if (opcode == OpCodes.Ldind_R8)
            return typeof(double);
        if (opcode == OpCodes.Ldind_U1
            || opcode == OpCodes.Ldind_U2
            || opcode == OpCodes.Ldind_U4)
            return typeof(uint);
        if (opcode == OpCodes.Ldind_Ref)
            return typeof(object);

        if (opcode == OpCodes.Ldlen)
            return typeof(nuint);
        if (opcode == OpCodes.Box
            || opcode == OpCodes.Unbox
            || opcode == OpCodes.Unbox_Any
            || opcode == OpCodes.Castclass)
            return (Type)pushInstruction.operand;

        if (opcode == OpCodes.Refanytype)
            return typeof(Type);
        if (opcode == OpCodes.Refanyval)
            return typeof(nint);

        if (opcode == OpCodes.Ckfinite)
            return GetPushType(method, instructions, instructions.InstructionRangeForStackItems(index, 0, 0).End - 1);

        if (opcode == OpCodes.Conv_I
            || opcode == OpCodes.Conv_Ovf_I
            || opcode == OpCodes.Conv_Ovf_I_Un)
            return typeof(nint);
        if (opcode == OpCodes.Conv_I1
            || opcode == OpCodes.Conv_Ovf_I1
            || opcode == OpCodes.Conv_Ovf_I1_Un
            || opcode == OpCodes.Conv_I2
            || opcode == OpCodes.Conv_Ovf_I2
            || opcode == OpCodes.Conv_Ovf_I2_Un
            || opcode == OpCodes.Conv_I4
            || opcode == OpCodes.Conv_Ovf_I4
            || opcode == OpCodes.Conv_Ovf_I4_Un)
            return typeof(int);
        if (opcode == OpCodes.Conv_I8
            || opcode == OpCodes.Conv_Ovf_I8
            || opcode == OpCodes.Conv_Ovf_I8_Un)
            return typeof(long);

        if (opcode == OpCodes.Conv_U
            || opcode == OpCodes.Conv_Ovf_U
            || opcode == OpCodes.Conv_Ovf_U_Un)
            return typeof(nuint);
        if (opcode == OpCodes.Conv_U1
            || opcode == OpCodes.Conv_Ovf_U1
            || opcode == OpCodes.Conv_Ovf_U1_Un
            || opcode == OpCodes.Conv_U2
            || opcode == OpCodes.Conv_Ovf_U2
            || opcode == OpCodes.Conv_Ovf_U2_Un
            || opcode == OpCodes.Conv_U4
            || opcode == OpCodes.Conv_Ovf_U4
            || opcode == OpCodes.Conv_Ovf_U4_Un)
            return typeof(uint);
        if (opcode == OpCodes.Conv_U8
            || opcode == OpCodes.Conv_Ovf_U8
            || opcode == OpCodes.Conv_Ovf_U8_Un)
            return typeof(ulong);

        if (opcode == OpCodes.Conv_R4
            || opcode == OpCodes.Conv_R_Un)
            return typeof(float);
        if (opcode == OpCodes.Conv_R8)
            return typeof(double);

        if (opcode == OpCodes.Ceq
            || opcode == OpCodes.Cgt
            || opcode == OpCodes.Cgt_Un
            || opcode == OpCodes.Clt
            || opcode == OpCodes.Clt_Un)
            return typeof(bool);

        if (opcode == OpCodes.Arglist)
            return typeof(nint);

        Type GetArgType(object arg)
        {
            return method.GetParameters()[(int)arg].ParameterType;
        }
        if (opcode == OpCodes.Ldarg
            || opcode == OpCodes.Ldarg_S)
            return GetArgType(pushInstruction.operand);
        if (opcode == OpCodes.Ldarga
            || opcode == OpCodes.Ldarga_S)
            return GetArgType(pushInstruction.operand).MakeByRefType();
        if (opcode == OpCodes.Ldarg_0)
            return GetArgType(0);
        if (opcode == OpCodes.Ldarg_1)
            return GetArgType(1);
        if (opcode == OpCodes.Ldarg_2)
            return GetArgType(2);
        if (opcode == OpCodes.Ldarg_3)
            return GetArgType(3);

        if (opcode == OpCodes.Ldc_I4
            || opcode == OpCodes.Ldc_I4_S
            || opcode == OpCodes.Ldc_I4_0
            || opcode == OpCodes.Ldc_I4_1
            || opcode == OpCodes.Ldc_I4_2
            || opcode == OpCodes.Ldc_I4_3
            || opcode == OpCodes.Ldc_I4_4
            || opcode == OpCodes.Ldc_I4_5
            || opcode == OpCodes.Ldc_I4_6
            || opcode == OpCodes.Ldc_I4_7
            || opcode == OpCodes.Ldc_I4_8
            || opcode == OpCodes.Ldc_I4_M1)
            return typeof(int);
        if (opcode == OpCodes.Ldc_I8)
            return typeof(long);
        if (opcode == OpCodes.Ldc_R4)
            return typeof(float);
        if (opcode == OpCodes.Ldc_R8)
            return typeof(double);

        if (opcode == OpCodes.Isinst)
            return (Type)pushInstruction.operand;

        if (opcode == OpCodes.Dup)
            return GetPushType(method, instructions, instructions.InstructionRangeForStackItems(index, 0, 0).End - 1);

        return null;
    }

    public static Type GetLocalType(MethodBase method, List<CodeInstruction> instructions, int index)
    {
        var instruction = instructions[index];

        if (instruction.opcode == OpCodes.Ldloc_0
            || instruction.opcode == OpCodes.Ldloc_1
            || instruction.opcode == OpCodes.Ldloc_2
            || instruction.opcode == OpCodes.Ldloc_3)
            return GetPushType(method, instructions, index);

        if (instruction.opcode == OpCodes.Stloc_0
            || instruction.opcode == OpCodes.Stloc_1
            || instruction.opcode == OpCodes.Stloc_2
            || instruction.opcode == OpCodes.Stloc_3)
            return GetPushType(method, instructions, instructions.InstructionRangeForStackItems(index, 0, 0).End - 1);

        if (instruction.opcode != OpCodes.Ldloc && instruction.opcode != OpCodes.Ldloc_S
            && instruction.opcode != OpCodes.Ldloca && instruction.opcode != OpCodes.Ldloca_S
            && instruction.opcode != OpCodes.Stloc && instruction.opcode != OpCodes.Stloc_S)
            return null;

        return ((LocalBuilder)instruction.operand).LocalType;
    }

    public static List<CodeInstruction> TransferLabelsAndVariables(MethodBase method, ref List<CodeInstruction> originalInstructions, ILGenerator generator)
    {
        var instructions = new List<CodeInstruction>(originalInstructions);

        var localDict = new Dictionary<int, LocalBuilder>();
        LocalBuilder GetLocal(int index, Type type)
        {
            if (!localDict.ContainsKey(index))
                localDict[index] = generator.DeclareLocal(type);
            return localDict[index];
        }

        var labelDict = new Dictionary<Label, Label>();
        Label GetLabel(Label label)
        {
            if (!labelDict.ContainsKey(label))
                labelDict[label] = generator.DefineLabel();
            return labelDict[label];
        }

        var instructionCount = instructions.Count;
        for (int i = 0; i < instructionCount; i++)
        {
            var instruction = instructions[i];

            var localIndex = instruction.GetLocalIndex();
            if (localIndex != -1)
            {
                var localType = GetLocalType(method, instructions, i);
                var local = GetLocal(localIndex, localType);
                if (instruction.IsStloc())
                    instructions[i] = CreateStloc(local);
                else if (instruction.opcode == OpCodes.Ldloca || instruction.opcode == OpCodes.Ldloca_S)
                    instructions[i] = CreateLdloca(local);
                else if (instruction.IsLdloc())
                    instructions[i] = CreateLdloc(local);
                instructions[i].labels = instruction.labels;
                instructions[i].blocks = instruction.blocks;
                instruction = instructions[i];
            }

            for (var labelIndex = 0; labelIndex < instruction.labels.Count(); labelIndex++)
                instructions[i].labels[labelIndex] = GetLabel(instruction.labels[labelIndex]);
            if (instruction.operand is Label label)
                instructions[i].operand = GetLabel(label);
        }

        return instructions;
    }
}
