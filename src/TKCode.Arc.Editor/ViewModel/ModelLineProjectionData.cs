// Ported from vscode (src/vs/editor/common/modelLineProjectionData.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - upstream's InjectedTextOptions is ModelDecorationInjectedTextOptions;
//  - breakOffsetsVisibleColumn holds doubles: with a font whose full-width characters are not two columns wide the
//    visible columns are fractional (upstream's numbers);
//  - ILineBreaksComputerFactory takes the word break and the wrapping indent as the enums WordBreak and WrappingIndent;
//    ILineBreaksComputer.Finalize returns an array (upstream's (ModelLineProjectionData | null)[]).

using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.ViewModel;

/// <summary>
/// How a model line is projected into view lines: the text injected into it, where it breaks and how wrapped lines are
/// indented.
/// </summary>
/// <remarks>
/// <para>*input*: <c>xxxxxxxxxxxxxxxxxxxxxxxxxxx</c></para>
/// <para>-&gt; Applying injections <c>[i...i]</c>, *inputWithInjections*: <c>xxxxxx[iiiiiiiiii]xxxxxxxxxxxxxxxxx[ii]xxxx</c></para>
/// <para>-&gt; breaking at offsets <c>|</c> in <c>xxxxxx[iiiiiii|iii]xxxxxxxxxxx|xxxxxx[ii]xxxx|</c>:</para>
/// <code>
/// xxxxxx[iiiiiii
/// iii]xxxxxxxxxxx
/// xxxxxx[ii]xxxx
/// </code>
/// <para>-&gt; applying wrappedTextIndentLength, *output*:</para>
/// <code>
/// xxxxxx[iiiiiii
///    iii]xxxxxxxxxxx
///    xxxxxx[ii]xxxx
/// </code>
/// </remarks>
internal sealed class ModelLineProjectionData
{
    public ModelLineProjectionData(
        int[]? injectionOffsets,
        ModelDecorationInjectedTextOptions[]? injectionOptions,
        int[] breakOffsets,
        double[] breakOffsetsVisibleColumn,
        int wrappedTextIndentLength)
    {
        InjectionOffsets = injectionOffsets;
        InjectionOptions = injectionOptions;
        BreakOffsets = breakOffsets;
        BreakOffsetsVisibleColumn = breakOffsetsVisibleColumn;
        WrappedTextIndentLength = wrappedTextIndentLength;
    }

    public int[]? InjectionOffsets { get; set; }

    /// <summary><c>InjectionOptions.Length</c> must equal <c>InjectionOffsets.Length</c>.</summary>
    public ModelDecorationInjectedTextOptions[]? InjectionOptions { get; set; }

    /// <summary>
    /// Refers to offsets after applying injections to the source. The last break offset indicates the length of the
    /// source after applying injections.
    /// </summary>
    public int[] BreakOffsets { get; set; }

    /// <summary>Refers to offsets after applying injections.</summary>
    public double[] BreakOffsetsVisibleColumn { get; set; }

    public int WrappedTextIndentLength { get; set; }

    public int GetOutputLineCount() => BreakOffsets.Length;

    public int GetMinOutputOffset(int outputLineIndex)
    {
        if (outputLineIndex > 0)
        {
            return WrappedTextIndentLength;
        }

        return 0;
    }

    public int GetLineLength(int outputLineIndex)
    {
        // These offsets refer to model text with injected text.
        var startOffset = outputLineIndex > 0 ? BreakOffsets[outputLineIndex - 1] : 0;
        var endOffset = BreakOffsets[outputLineIndex];

        var lineLength = endOffset - startOffset;
        if (outputLineIndex > 0)
        {
            lineLength += WrappedTextIndentLength;
        }

        return lineLength;
    }

    public int GetMaxOutputOffset(int outputLineIndex) => GetLineLength(outputLineIndex);

    public int TranslateToInputOffset(int outputLineIndex, int outputOffset)
    {
        if (outputLineIndex > 0)
        {
            outputOffset = Math.Max(0, outputOffset - WrappedTextIndentLength);
        }

        var offsetInInputWithInjection = outputLineIndex == 0 ? outputOffset : BreakOffsets[outputLineIndex - 1] + outputOffset;
        var offsetInInput = offsetInInputWithInjection;

        if (InjectionOffsets is not null)
        {
            for (var i = 0; i < InjectionOffsets.Length; i++)
            {
                if (offsetInInput > InjectionOffsets[i])
                {
                    if (offsetInInput < InjectionOffsets[i] + InjectionOptions![i].Content.Length)
                    {
                        // `inputOffset` is within injected text
                        offsetInInput = InjectionOffsets[i];
                    }
                    else
                    {
                        offsetInInput -= InjectionOptions![i].Content.Length;
                    }
                }
                else
                {
                    break;
                }
            }
        }

        return offsetInInput;
    }

    public OutputPosition TranslateToOutputPosition(int inputOffset, PositionAffinity affinity = PositionAffinity.None)
    {
        var inputOffsetInInputWithInjection = inputOffset;
        if (InjectionOffsets is not null)
        {
            for (var i = 0; i < InjectionOffsets.Length; i++)
            {
                if (inputOffset < InjectionOffsets[i])
                {
                    break;
                }

                if (affinity != PositionAffinity.Right && inputOffset == InjectionOffsets[i])
                {
                    break;
                }

                inputOffsetInInputWithInjection += InjectionOptions![i].Content.Length;
            }
        }

        return OffsetInInputWithInjectionsToOutputPosition(inputOffsetInInputWithInjection, affinity);
    }

    private OutputPosition OffsetInInputWithInjectionsToOutputPosition(int offsetInInputWithInjections, PositionAffinity affinity = PositionAffinity.None)
    {
        var low = 0;
        var high = BreakOffsets.Length - 1;
        var mid = 0;
        var midStart = 0;

        while (low <= high)
        {
            mid = low + ((high - low) / 2);

            var midStop = BreakOffsets[mid];
            midStart = mid > 0 ? BreakOffsets[mid - 1] : 0;

            if (affinity == PositionAffinity.Left)
            {
                if (offsetInInputWithInjections <= midStart)
                {
                    high = mid - 1;
                }
                else if (offsetInInputWithInjections > midStop)
                {
                    low = mid + 1;
                }
                else
                {
                    break;
                }
            }
            else
            {
                if (offsetInInputWithInjections < midStart)
                {
                    high = mid - 1;
                }
                else if (offsetInInputWithInjections >= midStop)
                {
                    low = mid + 1;
                }
                else
                {
                    break;
                }
            }
        }

        var outputOffset = offsetInInputWithInjections - midStart;
        if (mid > 0)
        {
            outputOffset += WrappedTextIndentLength;
        }

        return new OutputPosition(mid, outputOffset);
    }

    public OutputPosition NormalizeOutputPosition(int outputLineIndex, int outputOffset, PositionAffinity affinity)
    {
        if (InjectionOffsets is not null)
        {
            var offsetInInputWithInjections = OutputPositionToOffsetInInputWithInjections(outputLineIndex, outputOffset);
            var normalizedOffsetInUnwrappedLine = NormalizeOffsetInInputWithInjectionsAroundInjections(offsetInInputWithInjections, affinity);
            if (normalizedOffsetInUnwrappedLine != offsetInInputWithInjections)
            {
                // injected text caused a change
                return OffsetInInputWithInjectionsToOutputPosition(normalizedOffsetInUnwrappedLine, affinity);
            }
        }

        if (affinity == PositionAffinity.Left)
        {
            if (outputLineIndex > 0 && outputOffset == GetMinOutputOffset(outputLineIndex))
            {
                return new OutputPosition(outputLineIndex - 1, GetMaxOutputOffset(outputLineIndex - 1));
            }
        }
        else if (affinity == PositionAffinity.Right)
        {
            var maxOutputLineIndex = GetOutputLineCount() - 1;
            if (outputLineIndex < maxOutputLineIndex && outputOffset == GetMaxOutputOffset(outputLineIndex))
            {
                return new OutputPosition(outputLineIndex + 1, GetMinOutputOffset(outputLineIndex + 1));
            }
        }

        return new OutputPosition(outputLineIndex, outputOffset);
    }

    private int OutputPositionToOffsetInInputWithInjections(int outputLineIndex, int outputOffset)
    {
        if (outputLineIndex > 0)
        {
            outputOffset = Math.Max(0, outputOffset - WrappedTextIndentLength);
        }

        var result = (outputLineIndex > 0 ? BreakOffsets[outputLineIndex - 1] : 0) + outputOffset;
        return result;
    }

    private int NormalizeOffsetInInputWithInjectionsAroundInjections(int offsetInInputWithInjections, PositionAffinity affinity)
    {
        var injectedText = GetInjectedTextAtOffset(offsetInInputWithInjections);
        if (injectedText is not { } found)
        {
            return offsetInInputWithInjections;
        }

        var injectionOffsets = InjectionOffsets!;
        var injectionOptions = InjectionOptions!;
        if (affinity == PositionAffinity.None)
        {
            if (offsetInInputWithInjections == found.OffsetInInputWithInjections + found.Length
                && HasRightCursorStop(injectionOptions[found.InjectedTextIndex].CursorStops))
            {
                return found.OffsetInInputWithInjections + found.Length;
            }
            else
            {
                var result = found.OffsetInInputWithInjections;
                if (HasLeftCursorStop(injectionOptions[found.InjectedTextIndex].CursorStops))
                {
                    return result;
                }

                var index = found.InjectedTextIndex - 1;
                while (index >= 0 && injectionOffsets[index] == injectionOffsets[found.InjectedTextIndex])
                {
                    if (HasRightCursorStop(injectionOptions[index].CursorStops))
                    {
                        break;
                    }

                    result -= injectionOptions[index].Content.Length;
                    if (HasLeftCursorStop(injectionOptions[index].CursorStops))
                    {
                        break;
                    }

                    index--;
                }

                return result;
            }
        }
        else if (affinity is PositionAffinity.Right or PositionAffinity.RightOfInjectedText)
        {
            var result = found.OffsetInInputWithInjections + found.Length;
            var index = found.InjectedTextIndex;
            // traverse all injected text that touch each other
            while (index + 1 < injectionOffsets.Length && injectionOffsets[index + 1] == injectionOffsets[index])
            {
                result += injectionOptions[index + 1].Content.Length;
                index++;
            }

            return result;
        }
        else if (affinity is PositionAffinity.Left or PositionAffinity.LeftOfInjectedText)
        {
            // affinity is left
            var result = found.OffsetInInputWithInjections;
            var index = found.InjectedTextIndex;
            // traverse all injected text that touch each other
            while (index - 1 >= 0 && injectionOffsets[index - 1] == injectionOffsets[index])
            {
                result -= injectionOptions[index - 1].Content.Length;
                index--;
            }

            return result;
        }

        throw new ArgumentOutOfRangeException(nameof(affinity), affinity, "Unknown position affinity.");
    }

    public InjectedText? GetInjectedText(int outputLineIndex, int outputOffset)
    {
        var offset = OutputPositionToOffsetInInputWithInjections(outputLineIndex, outputOffset);
        var injectedText = GetInjectedTextAtOffset(offset);
        if (injectedText is not { } found)
        {
            return null;
        }

        return new InjectedText(InjectionOptions![found.InjectedTextIndex]);
    }

    private InjectedTextAtOffset? GetInjectedTextAtOffset(int offsetInInputWithInjections)
    {
        var injectionOffsets = InjectionOffsets;
        var injectionOptions = InjectionOptions;

        if (injectionOffsets is not null)
        {
            var totalInjectedTextLengthBefore = 0;
            for (var i = 0; i < injectionOffsets.Length; i++)
            {
                var length = injectionOptions![i].Content.Length;
                var injectedTextStartOffsetInInputWithInjections = injectionOffsets[i] + totalInjectedTextLengthBefore;
                var injectedTextEndOffsetInInputWithInjections = injectionOffsets[i] + totalInjectedTextLengthBefore + length;

                if (injectedTextStartOffsetInInputWithInjections > offsetInInputWithInjections)
                {
                    // Injected text starts later.
                    break; // All later injected texts have an even larger offset.
                }

                if (offsetInInputWithInjections <= injectedTextEndOffsetInInputWithInjections)
                {
                    // Injected text ends after or with the given position (but also starts with or before it).
                    return new InjectedTextAtOffset(i, injectedTextStartOffsetInInputWithInjections, length);
                }

                totalInjectedTextLengthBefore += length;
            }
        }

        return null;
    }

    private static bool HasRightCursorStop(InjectedTextCursorStops? cursorStop)
    {
        if (cursorStop is null)
        {
            return true;
        }

        return cursorStop is InjectedTextCursorStops.Right or InjectedTextCursorStops.Both;
    }

    private static bool HasLeftCursorStop(InjectedTextCursorStops? cursorStop)
    {
        if (cursorStop is null)
        {
            return true;
        }

        return cursorStop is InjectedTextCursorStops.Left or InjectedTextCursorStops.Both;
    }

    private readonly record struct InjectedTextAtOffset(int InjectedTextIndex, int OffsetInInputWithInjections, int Length);
}

/// <summary>The injected text at a view position.</summary>
internal sealed record InjectedText(ModelDecorationInjectedTextOptions Options);

/// <summary>A position in the output of a projection: an output (view) line of the model line, and an offset in it.</summary>
internal sealed class OutputPosition
{
    public OutputPosition(int outputLineIndex, int outputOffset)
    {
        OutputLineIndex = outputLineIndex;
        OutputOffset = outputOffset;
    }

    public int OutputLineIndex { get; set; }

    public int OutputOffset { get; set; }

    public override string ToString() => $"{OutputLineIndex}:{OutputOffset}";

    public TextPosition ToPosition(int baseLineNumber) => new(baseLineNumber + OutputLineIndex, OutputOffset + 1);
}

/// <summary>What a line breaks computer reads from the model.</summary>
internal interface ILineBreaksComputerContext
{
    string GetLineContent(int lineNumber);

    IReadOnlyList<LineInjectedText>? GetLineInjectedText(int lineNumber);
}

/// <summary>Creates line breaks computers for a set of wrapping settings.</summary>
internal interface ILineBreaksComputerFactory
{
    ILineBreaksComputer CreateLineBreaksComputer(
        ILineBreaksComputerContext context,
        FontInfo fontInfo,
        int tabSize,
        int wrappingColumn,
        WrappingIndent wrappingIndent,
        WordBreak wordBreak,
        bool wrapOnEscapedLineFeeds,
        bool useTwoCellFullwidthCharacters);
}

/// <summary>Collects the lines to break and computes their projections at once.</summary>
internal interface ILineBreaksComputer
{
    /// <summary>Pass in <paramref name="previousLineBreakData"/> if the only difference is in breaking columns!!!</summary>
    void AddRequest(int lineNumber, ModelLineProjectionData? previousLineBreakData);

    ModelLineProjectionData?[] Finalize();
}
