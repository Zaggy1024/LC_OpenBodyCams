using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;

using HarmonyLib;

namespace OpenBodyCams.Utilities.IL;

internal class ILInjector(IEnumerable<CodeInstruction> instructions, ILGenerator generator = null)
{
    private const string INVALID = "Injector is invalid";

    private List<CodeInstruction> instructions = instructions.ToList();
    private ILGenerator generator = generator;

    private int index = 0;

    private int matchEnd = -1;

    public int Index => index;

    public ILInjector GoToStart()
    {
        matchEnd = index;
        index = 0;
        return this;
    }

    public ILInjector GoToEnd()
    {
        matchEnd = index;
        index = instructions.Count;
        return this;
    }

    public ILInjector Forward(int offset)
    {
        if (!IsValid)
            return this;
        matchEnd = index;
        index = Math.Clamp(index + offset, -1, instructions.Count);
        return this;
    }

    public ILInjector Back(int offset) => Forward(-offset);

    private void MarkInvalid()
    {
        index = -1;
        matchEnd = -1;
    }

    private void Search(bool forward, ILMatcher[] predicates)
    {
        if (!IsValid)
            return;

        var direction = 1;

        if (!forward)
        {
            direction = -1;
            index--;
        }

        while (forward ? index < instructions.Count : index >= 0)
        {
            if (forward && index + predicates.Length > instructions.Count)
            {
                index = instructions.Count;
                break;
            }

            var predicateIndex = 0;
            while (predicateIndex < predicates.Length)
            {
                if (!predicates[predicateIndex].Matches(instructions[index + predicateIndex]))
                    break;
                predicateIndex++;
            }

            if (predicateIndex == predicates.Length)
            {
                matchEnd = index + predicateIndex;
                return;
            }

            index += direction;
        }

        MarkInvalid();
    }

    public ILInjector Find(params ILMatcher[] predicates)
    {
        Search(forward: true, predicates);
        return this;
    }

    public ILInjector ReverseFind(params ILMatcher[] predicates)
    {
        Search(forward: false, predicates);
        return this;
    }

    public ILInjector GoToPush(int popIndex)
    {
        if (!IsValid)
            return this;

        matchEnd = index;

        index--;
        int stackPosition = 0;
        while (index >= 0)
        {
            var instruction = instructions[index];

            stackPosition += instruction.PushCount();
            stackPosition -= instruction.PopCount();

            if (stackPosition > popIndex)
                return this;

            index--;
        }

        return this;
    }

    public ILInjector SkipBranch()
    {
        if (Instruction == null)
            return this;

        if (Instruction.operand is not Label label)
            throw new InvalidOperationException($"Current instruction is not a branch: {Instruction}");

        return FindLabel(label);
    }

    public ILInjector FindLabel(Label label)
    {
        if (label == default)
            return this;

        matchEnd = index;

        for (index = 0; index < instructions.Count; index++)
        {
            if (instructions[index].labels.Contains(label))
                return this;
        }

        MarkInvalid();
        return this;
    }

    public ILInjector GoToMatchEnd()
    {
        index = matchEnd;
        return this;
    }

    public ILInjector GoToLastMatchedInstruction()
    {
        if (!IsIndexValid(matchEnd))
            return this;
        index = matchEnd - 1;
        return this;
    }

    private bool IsIndexValid(int index)
    {
        return index != -1;
    }

    private bool IsIndexInRange(int index)
    {
        return index >= 0 && index < instructions.Count;
    }

    public bool IsValid => instructions != null && IsIndexValid(index);

    public CodeInstruction Instruction
    {
        get
        {
            if (!IsIndexInRange(index))
                return null;
            return instructions[index];
        }
        set
        {
            if (!IsIndexInRange(index))
                throw new InvalidOperationException($"Current index {index} is out of range of instruction count {instructions.Count}");
            instructions[index] = value;
        }
    }

    public CodeInstruction LastMatchedInstruction
    {
        get
        {
            var lastMatchIndex = matchEnd - 1;
            if (!IsIndexInRange(lastMatchIndex))
                return null;
            return instructions[lastMatchIndex];
        }
        set
        {
            var lastMatchIndex = matchEnd - 1;
            if (!IsIndexInRange(lastMatchIndex))
                throw new InvalidOperationException($"Last matched index {index} is out of range of instruction count {instructions.Count}");
            instructions[lastMatchIndex] = value;
        }
    }

    public CodeInstruction GetRelativeInstruction(int offset)
    {
        if (!IsValid)
            throw new InvalidOperationException(INVALID);

        var offsetIndex = index + offset;
        if (!IsIndexInRange(offsetIndex))
            throw new IndexOutOfRangeException($"Offset {offset} would read out of bounds at index {offsetIndex}");
        return instructions[offsetIndex];
    }

    public ILInjector SetRelativeInstruction(int offset, CodeInstruction instruction)
    {
        if (!IsValid)
            throw new InvalidOperationException(INVALID);

        var offsetIndex = index + offset;
        if (!IsIndexInRange(offsetIndex))
            throw new IndexOutOfRangeException($"Offset {offset} would write out of bounds at index {offsetIndex}");
        instructions[offsetIndex] = instruction;
        return this;
    }

    public IEnumerable<CodeInstruction> GetRelativeInstructions(int offset, int size)
    {
        for (var i = 0; i < size; i++)
            yield return instructions[index + offset + i];
    }

    public IEnumerable<CodeInstruction> GetRelativeInstructions(int size)
    {
        return GetRelativeInstructions(0, size);
    }

    private void GetLastMatchRangeAbsolute(out int start, out int end)
    {
        start = index;
        end = matchEnd;
        if (start > end)
            (start, end) = (end, start);
    }

    private void GetLastMatchRange(out int start, out int size)
    {
        GetLastMatchRangeAbsolute(out start, out var end);

        if (start < 0 || start >= instructions.Count)
            throw new InvalidOperationException($"Last match range starts at invalid index {start}");

        if (end < 0 || end > instructions.Count)
            throw new InvalidOperationException($"Last match range ends at invalid index {end}");

        size = end - start;
    }

    public List<CodeInstruction> GetLastMatch()
    {
        GetLastMatchRange(out var start, out var size);
        return instructions.GetRange(start, size);
    }

    public ILInjector DefineLabel(out Label label)
    {
        if (generator == null)
            throw new InvalidOperationException("No ILGenerator was provided");

        label = generator.DefineLabel();
        return this;
    }

    public ILInjector AddLabel(out Label label)
    {
        DefineLabel(out label);
        return AddLabel(label);
    }

    public ILInjector AddLabel(Label label)
    {
        Instruction = new(Instruction);
        Instruction.labels.Add(label);
        return this;
    }

    public ILInjector InsertInPlace(params CodeInstruction[] instructions)
    {
        if (!IsValid)
            throw new InvalidOperationException(INVALID);

        this.instructions.InsertRange(index, instructions);
        if (matchEnd >= index)
            matchEnd += instructions.Length;
        return this;
    }

    public ILInjector Insert(params CodeInstruction[] instructions)
    {
        InsertInPlace(instructions);
        index += instructions.Length;
        return this;
    }

    public ILInjector InsertInPlaceAfterBranch(params CodeInstruction[] instructions)
    {
        if (!IsValid)
            throw new InvalidOperationException(INVALID);

        var labels = Instruction.labels;
        Instruction = new(Instruction);
        Instruction.labels.Clear();

        this.instructions.InsertRange(index, instructions);
        Instruction.labels.AddRange(labels);

        if (matchEnd >= index)
            matchEnd += instructions.Length;
        return this;
    }

    public ILInjector InsertAfterBranch(params CodeInstruction[] instructions)
    {
        InsertInPlaceAfterBranch(instructions);
        index += instructions.Length;
        return this;
    }

    public ILInjector RemoveAllPreviousInstructions()
    {
        if (!IsValid)
            throw new InvalidOperationException(INVALID);

        instructions.RemoveRange(0, index);
        matchEnd -= index;
        if (matchEnd < 0)
            matchEnd = 0;
        index = 0;
        return this;
    }

    public ILInjector Remove(int count = 1)
    {
        if (!IsValid)
            throw new InvalidOperationException(INVALID);

        instructions.RemoveRange(index, count);
        if (matchEnd > index)
            matchEnd = Math.Max(index, matchEnd - count);
        return this;
    }

    public ILInjector RemoveLastMatch()
    {
        GetLastMatchRange(out var start, out var size);
        var labels = instructions[start].labels;
        instructions.RemoveRange(start, size);
        index = start;
        matchEnd = start;
        instructions[start].labels.AddRange(labels);
        return this;
    }

    public ILInjector ReplaceLastMatch(params CodeInstruction[] replacementInstructions)
    {
        if (replacementInstructions.Length == 0)
            throw new ArgumentException("Cannot replace a match with an empty array.");

        GetLastMatchRange(out var start, out var size);
        var labels = instructions[start].labels;
        instructions.RemoveRange(start, size);
        instructions.InsertRange(start, replacementInstructions);
        index = start;
        matchEnd = start + replacementInstructions.Length;
        instructions[start].labels.AddRange(labels);
        return this;
    }

    public ICollection<CodeInstruction> Instructions => instructions.AsReadOnly();

    public List<CodeInstruction> ReleaseInstructions()
    {
        var instructions = this.instructions;
        this.instructions = null;
        return instructions;
    }

    public ILInjector PrintContext(int context, string header = "")
    {
        if (!IsValid)
            throw new InvalidOperationException(INVALID + $" ({header})");

        var builder = new StringBuilder(header);
        if (header.Length > 0)
            builder.Append(':');
        builder.AppendLine();

        GetLastMatchRangeAbsolute(out var matchStart, out var matchEnd);

        var end = Math.Min(matchEnd + 1 + context, instructions.Count);

        for (var i = Math.Max(matchStart - context, 0); i < end; i++)
        {
            if (matchEnd == -1 && i == index)
            {
                builder.Append("\u2576> ");
            }
            else
            {
                if (i >= matchStart && i < matchEnd)
                    builder.Append("\u2502");
                else
                    builder.Append(" ");

                if (i == index)
                    builder.Append("\u2576> ");
                else
                    builder.Append("   ");
            }

            builder.AppendLine($"{i}: {instructions[i]}");
        }

        Plugin.Instance.Logger.LogInfo(builder);
        return this;
    }

    public ILInjector PrintContext(string header = "")
    {
        return PrintContext(4, header);
    }
}
