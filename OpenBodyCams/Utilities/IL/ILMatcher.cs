using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

using HarmonyLib;

namespace OpenBodyCams.Utilities.IL;

internal interface ILMatcher
{
    public bool Matches(CodeInstruction instruction);

    public ILMatcher CaptureAs(out CodeInstruction variable)
    {
        variable = new CodeInstruction(OpCodes.Nop, null);
        return new InstructionCapturingMatcher(this, variable);
    }

    public unsafe ILMatcher CaptureOperandAs<T>(out T operand) where T : unmanaged
    {
        operand = default;
        fixed (T* operandPtr = &operand)
        {
            return new OperandCapturingMatcher<T>(this, operandPtr);
        }
    }

    public static ILMatcher Not(ILMatcher matcher) => new NotMatcher(matcher);

    public static ILMatcher Opcode(OpCode opcode) => new OpcodeMatcher(opcode);
    public static ILMatcher Opcodes(params OpCode[] opcodes) => new OpcodesMatcher(opcodes);
    public static ILMatcher OpcodeOperand(OpCode opcode, object operand) => new OpcodeOperandMatcher(opcode, operand);
    public static ILMatcher Instruction(CodeInstruction instruction) => new InstructionMatcher(instruction);

    public static ILMatcher Ldarg(int? arg = null) => new LdargMatcher(arg);
    public static ILMatcher Ldloc(int? loc = null) => new LdlocMatcher(loc);
    public static ILMatcher Stloc(int? loc = null) => new StlocMatcher(loc);
    public static ILMatcher Ldc(int? value = null) => new LdcI32Matcher(value);
    public static ILMatcher LdcF32(float? value = null) => new LdcF32Matcher(value);

    public static ILMatcher Branch() => new BranchMatcher();

    public static ILMatcher Ldfld(FieldInfo field, [CallerMemberName] string callerName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
    {
        if (field == null)
            Plugin.Instance.Logger.LogWarning($"Field passed to ILMatcher.Ldfld() was null at {sourceFilePath}#{sourceLineNumber} ({callerName})");
        return new OpcodeOperandMatcher(OpCodes.Ldfld, field);
    }
    public static ILMatcher Ldsfld(FieldInfo field, [CallerMemberName] string callerName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
    {
        if (field == null)
            Plugin.Instance.Logger.LogWarning($"Field passed to ILMatcher.Ldsfld() was null at {sourceFilePath}#{sourceLineNumber} ({callerName})");
        return new OpcodeOperandMatcher(OpCodes.Ldsfld, field);
    }
    public static ILMatcher Stfld(FieldInfo field, [CallerMemberName] string callerName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
    {
        if (field == null)
            Plugin.Instance.Logger.LogWarning($"Field passed to ILMatcher.Stfld() was null at {sourceFilePath}#{sourceLineNumber} ({callerName})");
        return new OpcodeOperandMatcher(OpCodes.Stfld, field);
    }
    public static ILMatcher Stsfld(FieldInfo field, [CallerMemberName] string callerName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
    {
        if (field == null)
            Plugin.Instance.Logger.LogWarning($"Field passed to ILMatcher.Stsfld() was null at {sourceFilePath}#{sourceLineNumber} ({callerName})");
        return new OpcodeOperandMatcher(OpCodes.Stsfld, field);
    }

    public static ILMatcher Callvirt(MethodBase method, [CallerMemberName] string callerName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
    {
        if (method == null)
            Plugin.Instance.Logger.LogWarning($"Method passed to ILMatcher.Callvirt() was null at {sourceFilePath}#{sourceLineNumber} ({callerName})");
        return OpcodeOperand(OpCodes.Callvirt, method);
    }
    public static ILMatcher Call(MethodBase method, [CallerMemberName] string callerName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
    {
        if (method == null)
            Plugin.Instance.Logger.LogWarning($"Method passed to ILMatcher.Call() was null at {sourceFilePath}#{sourceLineNumber} ({callerName})");
        return OpcodeOperand(OpCodes.Call, method);
    }

    public static ILMatcher Predicate(Func<CodeInstruction, bool> predicate) => new PredicateMatcher(predicate);
    public static ILMatcher Predicate(Func<FieldInfo, bool> predicate)
    {
        return new PredicateMatcher(insn =>
        {
            if (insn.operand is not FieldInfo field)
                return false;
            return predicate(field);
        });
    }
}

internal class NotMatcher(ILMatcher matcher) : ILMatcher
{
    private readonly ILMatcher matcher = matcher;

    public bool Matches(CodeInstruction instruction) => !matcher.Matches(instruction);
}

internal class OpcodeMatcher(OpCode opcode) : ILMatcher
{
    private readonly OpCode opcode = opcode;

    public bool Matches(CodeInstruction instruction) => instruction.opcode == opcode;
}

internal class OpcodesMatcher(OpCode[] opcodes) : ILMatcher
{
    private readonly OpCode[] opcodes = opcodes;

    public bool Matches(CodeInstruction instruction) => opcodes.Contains(instruction.opcode);
}

internal class OpcodeOperandMatcher(OpCode opcode, object operand) : ILMatcher
{
    private readonly OpCode opcode = opcode;
    private readonly object operand = operand;

    public bool Matches(CodeInstruction instruction) => instruction.opcode == opcode && instruction.operand == operand;
}

internal class InstructionMatcher(CodeInstruction instruction) : ILMatcher
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

internal class LdargMatcher(int? arg) : ILMatcher
{
    private readonly int? arg = arg;

    public bool Matches(CodeInstruction instruction) => arg.HasValue ? instruction.GetLdargIndex() == arg : instruction.GetLdargIndex().HasValue;
}

internal class LdlocMatcher(int? loc) : ILMatcher
{
    private readonly int? loc = loc;

    public bool Matches(CodeInstruction instruction) => loc.HasValue ? instruction.GetLdlocIndex() == loc : instruction.GetLdlocIndex().HasValue;
}

internal class StlocMatcher(int? loc) : ILMatcher
{
    private readonly int? loc = loc;

    public bool Matches(CodeInstruction instruction) => loc.HasValue ? instruction.GetStlocIndex() == loc : instruction.GetStlocIndex().HasValue;
}

internal class LdcI32Matcher(int? value) : ILMatcher
{
    private readonly int? value = value;

    public bool Matches(CodeInstruction instruction) => value.HasValue ? instruction.GetLdcI32() == value : instruction.GetLdcI32().HasValue;
}

internal class LdcF32Matcher(float? value) : ILMatcher
{
    private readonly float? value = value;

    public bool Matches(CodeInstruction instruction) => instruction.opcode == OpCodes.Ldc_R4 && (!value.HasValue || (float)instruction.operand == value.Value);
}

internal class BranchMatcher : ILMatcher
{
    public bool Matches(CodeInstruction instruction) => instruction.Branches(out _);
}

internal class PredicateMatcher(Func<CodeInstruction, bool> predicate) : ILMatcher
{
    private readonly Func<CodeInstruction, bool> predicate = predicate;

    public bool Matches(CodeInstruction instruction) => predicate(instruction);
}

internal class InstructionCapturingMatcher(ILMatcher matcher, CodeInstruction variable) : ILMatcher
{
    private readonly ILMatcher matcher = matcher;
    private readonly CodeInstruction variable = variable;

    public bool Matches(CodeInstruction instruction)
    {
        var isMatch = matcher.Matches(instruction);
        if (isMatch)
        {
            variable.opcode = instruction.opcode;
            variable.operand = instruction.operand;
            variable.blocks = [.. instruction.blocks];
            variable.labels = [.. instruction.labels];
        }
        return isMatch;
    }
}

internal unsafe class OperandCapturingMatcher<T>(ILMatcher matcher, T* operand) : ILMatcher where T : unmanaged
{
    private readonly ILMatcher matcher = matcher;
    private readonly T* operand = operand;

    public bool Matches(CodeInstruction instruction)
    {
        var isMatch = matcher.Matches(instruction);
        if (isMatch)
            *operand = (T)instruction.operand;
        return isMatch;
    }
}
