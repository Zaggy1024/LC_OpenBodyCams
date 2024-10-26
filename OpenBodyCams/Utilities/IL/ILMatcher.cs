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

    public static NotMatcher Not(ILMatcher matcher) => new(matcher);

    public static OpcodeMatcher Opcode(OpCode opcode) => new(opcode);
    public static OpcodesMatcher Opcodes(params OpCode[] opcodes) => new(opcodes);
    public static OpcodeOperandMatcher OpcodeOperand(OpCode opcode, object operand) => new(opcode, operand);
    public static InstructionMatcher Instruction(CodeInstruction instruction) => new(instruction);

    public static LdargMatcher Ldarg(int? arg = null) => new(arg);
    public static LdlocMatcher Ldloc(int? loc = null) => new(loc);
    public static StlocMatcher Stloc(int? loc = null) => new(loc);
    public static LdcI32Matcher Ldc(int? value = null) => new(value);

    public static BranchMatcher Branch() => new();

    public static OpcodeOperandMatcher Ldfld(FieldInfo field)
    {
        if (field == null)
            Plugin.Instance.Logger.LogWarning($"Field passed to ILMatcher.Ldfld() was null\n{new StackTrace()}");
        return new(OpCodes.Ldfld, field);
    }
    public static OpcodeOperandMatcher Ldsfld(FieldInfo field)
    {
        if (field == null)
            Plugin.Instance.Logger.LogWarning($"Field passed to ILMatcher.Ldsfld() was null\n{new StackTrace()}");
        return new(OpCodes.Ldsfld, field);
    }
    public static OpcodeOperandMatcher Stfld(FieldInfo field)
    {
        if (field == null)
            Plugin.Instance.Logger.LogWarning($"Field passed to ILMatcher.Stfld() was null\n{new StackTrace()}");
        return new(OpCodes.Stfld, field);
    }
    public static OpcodeOperandMatcher Stsfld(FieldInfo field)
    {
        if (field == null)
            Plugin.Instance.Logger.LogWarning($"Field passed to ILMatcher.Stsfld() was null\n{new StackTrace()}");
        return new(OpCodes.Stsfld, field);
    }

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

public class NotMatcher(ILMatcher matcher) : ILMatcher
{
    private readonly ILMatcher matcher = matcher;

    public bool Matches(CodeInstruction instruction) => !matcher.Matches(instruction);
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

public class LdargMatcher(int? arg) : ILMatcher
{
    private readonly int? arg = arg;

    public bool Matches(CodeInstruction instruction) => arg.HasValue ? instruction.GetLdargIndex() == arg : instruction.GetLdargIndex().HasValue;
}

public class LdlocMatcher(int? loc) : ILMatcher
{
    private readonly int? loc = loc;

    public bool Matches(CodeInstruction instruction) => loc.HasValue ? instruction.GetLdlocIndex() == loc : instruction.GetLdlocIndex().HasValue;
}

public class StlocMatcher(int? loc) : ILMatcher
{
    private readonly int? loc = loc;

    public bool Matches(CodeInstruction instruction) => loc.HasValue ? instruction.GetStlocIndex() == loc : instruction.GetStlocIndex().HasValue;
}

public class LdcI32Matcher(int? value) : ILMatcher
{
    private readonly int? value = value;

    public bool Matches(CodeInstruction instruction) => (!value.HasValue && instruction.GetLdcI32().HasValue) || instruction.GetLdcI32() == value;
}

public class BranchMatcher : ILMatcher
{
    public bool Matches(CodeInstruction instruction) => instruction.Branches(out _);
}

public class PredicateMatcher(Func<CodeInstruction, bool> predicate) : ILMatcher
{
    private readonly Func<CodeInstruction, bool> predicate = predicate;

    public bool Matches(CodeInstruction instruction) => predicate(instruction);
}
