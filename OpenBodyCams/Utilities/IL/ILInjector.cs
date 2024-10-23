using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;

using HarmonyLib;

namespace OpenBodyCams.Utilities.IL;

public class ILInjector(IEnumerable<CodeInstruction> instructions, ILGenerator generator = null)
{
    private const string INVALID = "Injector is invalid";
    private const string MATCH_END_INVALID = "The end of the last search was invalid";

    private List<CodeInstruction> instructions = instructions.ToList();
    private ILGenerator generator = generator;

    private int index = 0;

    private int matchEnd = -1;

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
        matchEnd = index;
        index = Math.Clamp(index + offset, -1, instructions.Count);
        return this;
    }

    public ILInjector Back(int offset) => Forward(-offset);

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

        index = -1;
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

        matchEnd = index + 1;

        for (index = 0; index < instructions.Count; index++)
        {
            if (instructions[index].labels.Contains(label))
                return this;
        }

        index = -1;
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

    public CodeInstruction Instruction => IsIndexInRange(index) ? instructions[index] : null;

    private void GetLastMatchRange(out int start, out int size)
    {
        start = index;
        var end = matchEnd;
        if (start > end)
            (start, end) = (end, start);

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

    public Label AddLabel()
    {
        if (generator == null)
            throw new InvalidOperationException("No ILGenerator was provided");

        var label = generator.DefineLabel();
        Instruction.labels.Add(label);
        return label;
    }

    public ILInjector InsertInPlace(params CodeInstruction[] instructions)
    {
        if (!IsValid)
            throw new InvalidOperationException(INVALID);

        this.instructions.InsertRange(index, instructions);
        return this;
    }

    public ILInjector Insert(params CodeInstruction[] instructions)
    {
        InsertInPlace(instructions);
        index += instructions.Length;
        return this;
    }

    public ILInjector RemoveLastMatch()
    {
        GetLastMatchRange(out var start, out var size);
        instructions.RemoveRange(start, size);
        index = start;
        matchEnd = start;
        return this;
    }

    public ILInjector ReplaceLastMatch(params CodeInstruction[] instructions)
    {
        RemoveLastMatch();
        Insert(instructions);
        return this;
    }

    public ICollection<CodeInstruction> Instructions => instructions.AsReadOnly();

    public List<CodeInstruction> ReleaseInstructions()
    {
        if (!IsValid)
            throw new InvalidOperationException(INVALID);

        var instructions = this.instructions;
        this.instructions = null;
        return instructions;
    }

    public ILInjector PrintContext(int context, string header = "")
    {
        if (!IsValid)
            throw new InvalidOperationException(INVALID + $" ({header})");

        var builder = new StringBuilder(header);
        builder.Append(':');
        if (header.Length > 0)
            builder.AppendLine();

        var end = Math.Min(index + 1 + context, instructions.Count);
        for (var i = Math.Max(index - context, 0); i < end; i++)
        {
            if (i == index)
                builder.Append("-> ");
            else
                builder.Append("   ");
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
