using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;

namespace OpenBodyCams.Utilities.IL;

public class ILInjector(IEnumerable<CodeInstruction> instructions)
{
    private const string INVALID = "Injector is invalid";

    private List<CodeInstruction> instructions = instructions.ToList();

    private int index = 0;

    public ILInjector Reset()
    {
        index = 0;
        return this;
    }

    public ILInjector Forward(int offset)
    {
        index = Math.Clamp(index + offset, -1, instructions.Count);
        return this;
    }

    public ILInjector Back(int offset) => Forward(-offset);

    private void Search(bool forward, bool cursorAtEnd, ILMatcher[] predicates)
    {
        if (!IsValid)
            return;

        var direction = forward ? 1 : -1;

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
                if (cursorAtEnd)
                    index += predicateIndex;
                return;
            }

            index += direction;
        }
    }

    public ILInjector FindStart(params ILMatcher[] predicates)
    {
        Search(forward: true, cursorAtEnd: false, predicates);
        return this;
    }

    public ILInjector FindEnd(params ILMatcher[] predicates)
    {
        Search(forward: true, cursorAtEnd: true, predicates);
        return this;
    }

    public ILInjector GoToPush(int popIndex)
    {
        if (!IsValid)
            return this;

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

    public bool IsValid => instructions != null && index >= 0 && index < instructions.Count;

    public CodeInstruction Instruction => IsValid ? instructions[index] : null;

    public ILInjector Insert(params CodeInstruction[] instructions)
    {
        if (!IsValid)
            throw new InvalidOperationException(INVALID);

        this.instructions.InsertRange(index, instructions);
        index += instructions.Length;
        return this;
    }

    public ILInjector InsertInPlace(params CodeInstruction[] instructions)
    {
        if (!IsValid)
            throw new InvalidOperationException(INVALID);

        this.instructions.InsertRange(index, instructions);
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
