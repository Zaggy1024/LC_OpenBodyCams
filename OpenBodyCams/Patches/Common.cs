using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenBodyCams.Patches
{
    public class SequenceMatch
    {
        public int Start;
        public int End;

        public SequenceMatch(int start, int end)
        {
            Start = start;
            End = end;
        }

        public int Size { get => End - Start; }
    }

    public static class Common
    {
        public static SequenceMatch FindIndexOfSequence<T>(this List<T> list, int startIndex, int count, IEnumerable<Predicate<T>> predicates)
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

        public static SequenceMatch FindIndexOfSequence<T>(this List<T> list, int startIndex, IEnumerable<Predicate<T>> predicates)
        {
            return FindIndexOfSequence(list, startIndex, -1, predicates);
        }

        public static SequenceMatch FindIndexOfSequence<T>(this List<T> list, IEnumerable<Predicate<T>> predicates)
        {
            return FindIndexOfSequence(list, 0, -1, predicates);
        }
    }
}
