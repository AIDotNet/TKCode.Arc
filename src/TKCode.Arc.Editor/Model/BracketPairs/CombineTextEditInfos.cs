// Ported from vscode (src/vs/editor/common/model/bracketPairsTextModelPart/bracketPairsTree/combineTextEditInfos.ts,
// commit b57d5c9e), MIT license.
//
// Differences: upstream's last mapping with undefined lengths ("copy everything from old to new") is a mapping flagged
// IsRest; ArrayQueue is a Queue.

using static TKCode.Arc.Editor.Model.BracketPairs.LengthHelpers;

namespace TKCode.Arc.Editor.Model.BracketPairs;

/// <summary>Combines two sets of edits, the second made after the first, into one set against the original text.</summary>
internal static class TextEditInfoCombiner
{
    public static IReadOnlyList<TextEditInfo> CombineTextEditInfos(IReadOnlyList<TextEditInfo> textEditInfoFirst, IReadOnlyList<TextEditInfo> textEditInfoSecond)
    {
        if (textEditInfoFirst.Count == 0)
        {
            return textEditInfoSecond;
        }

        if (textEditInfoSecond.Count == 0)
        {
            return textEditInfoFirst;
        }

        // s0: State before any edits
        var s0ToS1Map = new Queue<LengthMapping>(ToLengthMapping(textEditInfoFirst));
        // s1: State after first edit, but before second edit
        var s1ToS2Map = ToLengthMapping(textEditInfoSecond);
        s1ToS2Map.Add(LengthMapping.Rest); // Copy everything from old to new
        // s2: State after both edits

        s0ToS1Map.TryDequeue(out var curItem);

        // s1Length null means "infinity"
        List<LengthMapping> NextS0ToS1MapWithS1LengthOf(Length? s1LengthOrInfinity)
        {
            if (s1LengthOrInfinity is not { } s1Length)
            {
                var arr = new List<LengthMapping>();
                if (curItem is not null)
                {
                    arr.Add(curItem);
                }

                arr.AddRange(s0ToS1Map);
                s0ToS1Map.Clear();
                curItem = null;
                return arr;
            }

            var result = new List<LengthMapping>();
            while (curItem is not null && !LengthIsZero(s1Length))
            {
                var (item, remainingItem) = curItem.SplitAt(s1Length);
                result.Add(item);
                s1Length = LengthDiffNonNegative(item.LengthAfter, s1Length);
                if (remainingItem is not null)
                {
                    curItem = remainingItem;
                }
                else
                {
                    s0ToS1Map.TryDequeue(out curItem);
                }
            }

            if (!LengthIsZero(s1Length))
            {
                result.Add(new LengthMapping(false, s1Length, s1Length));
            }

            return result;
        }

        var edits = new List<TextEditInfo>();

        void PushEdit(Length startOffset, Length endOffset, Length newLength)
        {
            if (edits.Count > 0 && LengthEquals(edits[^1].EndOffset, startOffset))
            {
                var lastResult = edits[^1];
                edits[^1] = new TextEditInfo(lastResult.StartOffset, endOffset, LengthAdd(lastResult.NewLength, newLength));
            }
            else
            {
                edits.Add(new TextEditInfo(startOffset, endOffset, newLength));
            }
        }

        var s0Offset = Length.Zero;
        foreach (var s1ToS2 in s1ToS2Map)
        {
            var s0ToS1 = NextS0ToS1MapWithS1LengthOf(s1ToS2.IsRest ? null : s1ToS2.LengthBefore);
            if (s1ToS2.Modified)
            {
                var s0Length = SumLengths(s0ToS1, static s => s.LengthBefore);
                var s0EndOffset = LengthAdd(s0Offset, s0Length);
                PushEdit(s0Offset, s0EndOffset, s1ToS2.LengthAfter);
                s0Offset = s0EndOffset;
            }
            else
            {
                foreach (var s1 in s0ToS1)
                {
                    var s0StartOffset = s0Offset;
                    s0Offset = LengthAdd(s0Offset, s1.LengthBefore);
                    if (s1.Modified)
                    {
                        PushEdit(s0StartOffset, s0Offset, s1.LengthAfter);
                    }
                }
            }
        }

        return edits;
    }

    private static List<LengthMapping> ToLengthMapping(IReadOnlyList<TextEditInfo> textEditInfos)
    {
        var result = new List<LengthMapping>();
        var lastOffset = Length.Zero;
        foreach (var textEditInfo in textEditInfos)
        {
            var spaceLength = LengthDiffNonNegative(lastOffset, textEditInfo.StartOffset);
            if (!LengthIsZero(spaceLength))
            {
                result.Add(new LengthMapping(false, spaceLength, spaceLength));
            }

            var lengthBefore = LengthDiffNonNegative(textEditInfo.StartOffset, textEditInfo.EndOffset);
            result.Add(new LengthMapping(true, lengthBefore, textEditInfo.NewLength));
            lastOffset = textEditInfo.EndOffset;
        }

        return result;
    }

    private sealed class LengthMapping(bool modified, Length lengthBefore, Length lengthAfter, bool isRest = false)
    {
        public static readonly LengthMapping Rest = new(false, Length.Zero, Length.Zero, isRest: true);

        /// <summary>If false, length before and length after equal.</summary>
        public bool Modified { get; } = modified;

        public Length LengthBefore { get; } = lengthBefore;

        public Length LengthAfter { get; } = lengthAfter;

        public bool IsRest { get; } = isRest;

        public (LengthMapping Item, LengthMapping? Remaining) SplitAt(Length lengthAfter)
        {
            var remainingLengthAfter = LengthDiffNonNegative(lengthAfter, LengthAfter);
            if (LengthEquals(remainingLengthAfter, Length.Zero))
            {
                return (this, null);
            }

            if (Modified)
            {
                return (new LengthMapping(Modified, LengthBefore, lengthAfter), new LengthMapping(Modified, Length.Zero, remainingLengthAfter));
            }

            return (new LengthMapping(Modified, lengthAfter, lengthAfter), new LengthMapping(Modified, remainingLengthAfter, remainingLengthAfter));
        }

        /// <inheritdoc />
        public override string ToString() => $"{(Modified ? "M" : "U")}:{LengthToObj(LengthBefore)} -> {LengthToObj(LengthAfter)}";
    }
}
