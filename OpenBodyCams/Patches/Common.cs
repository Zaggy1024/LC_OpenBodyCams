using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;

using HarmonyLib;

namespace OpenBodyCams.Patches
{
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
    }
}
