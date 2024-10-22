using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using HarmonyLib;

namespace OpenBodyCams.Utilities.IL;

public interface ILMatcher
{
    public bool Matches(CodeInstruction instruction);

    private static OpCode[] branchInstructions = [OpCodes.Br, OpCodes.Br_S, OpCodes.Brfalse, OpCodes.Brfalse_S];

    public static OpcodeMatcher Opcode(OpCode opcode) => new(opcode);
    public static OpcodesMatcher Opcodes(params OpCode[] opcodes) => new(opcodes);
    public static OpcodeOperandMatcher OpcodeOperand(OpCode opcode, object operand) => new(opcode, operand);
    public static InstructionMatcher Instruction(CodeInstruction instruction) => new(instruction);

    public static LdlocMatcher Ldloc() => new();
    public static StlocMatcher Stloc() => new();
    public static BranchMatcher Branch() => new();

    public static OpcodeOperandMatcher Ldfld(FieldInfo field) => new(OpCodes.Ldfld, field);
    public static OpcodeOperandMatcher Ldsfld(FieldInfo field) => new(OpCodes.Ldsfld, field);

    public static OpcodeOperandMatcher Callvirt(MethodBase method)
    {
        if (method == null)
            Plugin.Instance.Logger.LogWarning($"Method passed to ILMatcher.Callvirt() was null\n{new StackTrace()}");
        return OpcodeOperand(OpCodes.Callvirt, method);
    }
    public static OpcodeOperandMatcher Call(MethodBase method)
    {
        if (method == null)
            Plugin.Instance.Logger.LogWarning($"Method passed to ILMatcher.Call() was null\n{new StackTrace()}");
        return OpcodeOperand(OpCodes.Call, method);
    }

    public static PredicateMatcher Predicate(Func<CodeInstruction, bool> predicate) => new(predicate);
    public static PredicateMatcher Predicate(Func<FieldInfo, bool> predicate)
    {
        return new(insn =>
        {
            if (insn.operand is not FieldInfo field)
                return false;
            return predicate(field);
        });
    }
}

public class OpcodeMatcher(OpCode opcode) : ILMatcher
{
    private readonly OpCode opcode = opcode;

    public bool Matches(CodeInstruction instruction) => instruction.opcode == opcode;
}

public class OpcodesMatcher(OpCode[] opcodes) : ILMatcher
{
    private readonly OpCode[] opcodes = opcodes;

    public bool Matches(CodeInstruction instruction) => opcodes.Contains(instruction.opcode);
}

public class OpcodeOperandMatcher(OpCode opcode, object operand) : ILMatcher
{
    private readonly OpCode opcode = opcode;
    private readonly object operand = operand;

    public bool Matches(CodeInstruction instruction) => instruction.opcode == opcode && instruction.operand == operand;
}

public class InstructionMatcher(CodeInstruction instruction) : ILMatcher
{
    private readonly OpCode opcode = instruction.opcode;
    private readonly object operand = instruction.operand;
    private readonly Label[] labels = [.. instruction.labels];

    public bool Matches(CodeInstruction instruction)
    {
        if (instruction.opcode != opcode)
            return false;
        if (instruction.operand != operand)
            return false;
        if (instruction.labels.Count != labels.Length)
            return false;
        for (var i = 0; i < labels.Length; i++)
        {
            if (labels[i] != instruction.labels[i])
                return false;
        }
        return true;
    }
}

public class LdlocMatcher : ILMatcher
{
    public bool Matches(CodeInstruction instruction) => instruction.IsLdloc();
}

public class StlocMatcher : ILMatcher
{
    public bool Matches(CodeInstruction instruction) => instruction.IsStloc();
}

public class BranchMatcher : ILMatcher
{
    public bool Matches(CodeInstruction instruction) => instruction.Branches(out _);
}

public class PredicateMatcher(Func<CodeInstruction, bool> predicate) : ILMatcher
{
    private Func<CodeInstruction, bool> predicate = predicate;

    public bool Matches(CodeInstruction instruction) => predicate(instruction);
}
