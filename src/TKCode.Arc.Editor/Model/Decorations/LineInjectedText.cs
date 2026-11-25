// Ported from vscode (src/vs/editor/common/textModelEvents.ts, commit b57d5c9e), MIT license.

using System.Text;

namespace TKCode.Arc.Editor.Model;

/// <summary>A part of a line with injected text that has a fixed width.</summary>
internal readonly record struct FixedWidthInjectedTextRange(int StartOffset, int EndOffset, double WidthInEm);

/// <summary>Text that a decoration injects into a line.</summary>
internal sealed class LineInjectedText
{
    public LineInjectedText(int ownerId, int lineNumber, int column, ModelDecorationInjectedTextOptions options, int order)
    {
        OwnerId = ownerId;
        LineNumber = lineNumber;
        Column = column;
        Options = options;
        Order = order;
    }

    public int OwnerId { get; }

    public int LineNumber { get; }

    public int Column { get; }

    public ModelDecorationInjectedTextOptions Options { get; }

    /// <summary>0 for text before the decoration's range, 1 for text after it.</summary>
    public int Order { get; }

    public LineInjectedText WithText(string text) => new(OwnerId, LineNumber, Column, Options with { Content = text }, Order);

    /// <summary>Returns the line as it reads with the injected texts in it.</summary>
    public static string ApplyInjectedText(string lineText, IReadOnlyList<LineInjectedText>? injectedTexts)
    {
        if (injectedTexts is null || injectedTexts.Count == 0)
        {
            return lineText;
        }

        var result = new StringBuilder(lineText.Length);
        var lastOriginalOffset = 0;
        for (var i = 0; i < injectedTexts.Count; i++)
        {
            var injectedText = injectedTexts[i];
            result.Append(lineText, lastOriginalOffset, injectedText.Column - 1 - lastOriginalOffset);
            lastOriginalOffset = injectedText.Column - 1;
            result.Append(injectedText.Options.Content);
        }

        result.Append(lineText, lastOriginalOffset, lineText.Length - lastOriginalOffset);
        return result.ToString();
    }

    /// <summary>Collects the injected texts of the decorations, ordered by line, column and before/after.</summary>
    public static List<LineInjectedText> FromDecorations(IReadOnlyList<IModelDecoration> decorations)
    {
        var result = new List<LineInjectedText>();
        for (var i = 0; i < decorations.Count; i++)
        {
            var decoration = decorations[i];
            if (decoration.Options.Before is { OccupiesHorizontalSpace: true } before)
            {
                result.Add(new LineInjectedText(
                    decoration.OwnerId, decoration.Range.StartLineNumber, decoration.Range.StartColumn, before, 0));
            }

            if (decoration.Options.After is { OccupiesHorizontalSpace: true } after)
            {
                result.Add(new LineInjectedText(
                    decoration.OwnerId, decoration.Range.EndLineNumber, decoration.Range.EndColumn, after, 1));
            }
        }

        // Upstream relies on a stable sort; the index keeps equal items in their order.
        var order = new Dictionary<LineInjectedText, int>(result.Count, ReferenceEqualityComparer.Instance);
        for (var i = 0; i < result.Count; i++)
        {
            order[result[i]] = i;
        }

        result.Sort((a, b) =>
        {
            if (a.LineNumber != b.LineNumber)
            {
                return a.LineNumber - b.LineNumber;
            }

            if (a.Column != b.Column)
            {
                return a.Column - b.Column;
            }

            return a.Order != b.Order ? a.Order - b.Order : order[a] - order[b];
        });
        return result;
    }

    public static List<FixedWidthInjectedTextRange> GetFixedWidthInjectedTextRanges(IReadOnlyList<LineInjectedText>? injectedTexts)
    {
        var result = new List<FixedWidthInjectedTextRange>();
        if (injectedTexts is null)
        {
            return result;
        }

        var injectedTextLength = 0;
        for (var i = 0; i < injectedTexts.Count; i++)
        {
            var injectedText = injectedTexts[i];
            var length = injectedText.Options.Content.Length;
            var startOffset = injectedText.Column - 1 + injectedTextLength;
            if (injectedText.Options.WidthInEm is { } widthInEm)
            {
                result.Add(new FixedWidthInjectedTextRange(startOffset, startOffset + length, widthInEm));
            }

            injectedTextLength += length;
        }

        return result;
    }
}

/// <summary>A decoration changed the height of a line.</summary>
internal readonly record struct ModelLineHeightChanged(int OwnerId, string DecorationId, int LineNumber, double? LineHeightMultiplier);

/// <summary>A decoration changed the font of a line.</summary>
internal readonly record struct ModelFontChanged(int OwnerId, int LineNumber);

/// <summary>The injected text of some lines changed.</summary>
internal sealed class ModelInjectedTextChangedEvent : IModelContentOrInjectedTextChangedEvent
{
    public ModelInjectedTextChangedEvent(IReadOnlyList<ModelRawLineChanged> changes)
    {
        Changes = changes;
    }

    public IReadOnlyList<ModelRawLineChanged> Changes { get; }
}

/// <summary>Decorations changed the height of some lines.</summary>
internal sealed class ModelLineHeightChangedEvent
{
    public ModelLineHeightChangedEvent(IReadOnlyList<ModelLineHeightChanged> changes)
    {
        Changes = changes;
    }

    public IReadOnlyList<ModelLineHeightChanged> Changes { get; }

    public bool Affects(TextRange range)
    {
        for (var i = 0; i < Changes.Count; i++)
        {
            if (Changes[i].LineNumber >= range.StartLineNumber && Changes[i].LineNumber <= range.EndLineNumber)
            {
                return true;
            }
        }

        return false;
    }

    public bool Affects(TextPosition position)
    {
        for (var i = 0; i < Changes.Count; i++)
        {
            if (Changes[i].LineNumber == position.LineNumber)
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>Decorations changed the font of some lines.</summary>
internal sealed class ModelFontChangedEvent
{
    public ModelFontChangedEvent(IReadOnlyList<ModelFontChanged> changes)
    {
        Changes = changes;
    }

    public IReadOnlyList<ModelFontChanged> Changes { get; }
}
