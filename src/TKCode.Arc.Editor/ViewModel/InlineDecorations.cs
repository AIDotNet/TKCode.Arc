// Ported from vscode (src/vs/editor/common/viewModel/inlineDecorations.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - InlineDecoration is a record (value equality, which upstream's tests compare by);
//  - IViewDecorationsCollection is the class ViewDecorationsCollection;
//  - the contexts are interfaces with methods instead of objects of functions;
//  - InlineModelDecorationsComputer works on the TextModel (upstream's ITextModel).

using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.ViewModel;

/// <summary>What an inline decoration styles.</summary>
internal enum InlineDecorationType
{
    Regular = 0,
    Before = 1,
    After = 2,
    RegularAffectingLetterSpacing = 3,

    /// <summary>
    /// A decoration that covers no character, yet is rendered as an element of its own. Used for injected text that
    /// only reserves horizontal space (e.g. <c>{ content: '', widthInEm: 1 }</c>): there is no character to decorate,
    /// so the width comes from the decoration's class instead. Unlike <see cref="Before"/>/<see cref="After"/>, this is
    /// not a pseudo element attached to a surrounding decoration, but the injected content itself.
    /// </summary>
    WidthOnly = 4,
}

/// <summary>A style over a range of a view line.</summary>
internal sealed record InlineDecoration(TextRange Range, string InlineClassName, InlineDecorationType Type);

/// <summary>A collection of decorations in a range of lines.</summary>
internal sealed class ViewDecorationsCollection
{
    public ViewDecorationsCollection(
        IReadOnlyList<ViewModelDecoration> decorations,
        IReadOnlyList<IReadOnlyList<InlineDecoration>> inlineDecorations,
        IReadOnlyList<bool> hasVariableFonts)
    {
        Decorations = decorations;
        InlineDecorations = inlineDecorations;
        HasVariableFonts = hasVariableFonts;
    }

    /// <summary>Decorations in the range of lines (ungrouped).</summary>
    public IReadOnlyList<ViewModelDecoration> Decorations { get; }

    /// <summary>Inline decorations (grouped by each line in the range of lines).</summary>
    public IReadOnlyList<IReadOnlyList<InlineDecoration>> InlineDecorations { get; }

    /// <summary>Whether the decorations affect the fonts.</summary>
    public IReadOnlyList<bool> HasVariableFonts { get; }
}

/// <summary>Computes the inline decorations of a model line.</summary>
internal interface IInlineDecorationsComputer
{
    /// <summary>Get the inline decorations for a specific model line number, split by view line number.</summary>
    IReadOnlyList<IReadOnlyList<InlineDecoration>> GetInlineDecorations(int modelLineNumber);
}

/// <summary>Where an <see cref="InlineModelDecorationsComputer"/> gets its decorations.</summary>
internal interface IInlineModelDecorationsComputerContext
{
    /// <summary>Get model decorations for a view range.</summary>
    IReadOnlyList<IModelDecoration> GetModelDecorations(TextRange viewRange, bool onlyMinimapDecorations, bool onlyMarginDecorations);
}

/// <summary>Computes the inline decorations that come from model decorations.</summary>
internal sealed class InlineModelDecorationsComputer : IInlineDecorationsComputer
{
    private readonly IInlineModelDecorationsComputerContext _context;
    private readonly TextModel _model;
    private readonly ICoordinatesConverter _coordinatesConverter;
    private Dictionary<string, ViewModelDecoration> _decorationsCache;

    public InlineModelDecorationsComputer(IInlineModelDecorationsComputerContext context, TextModel model, ICoordinatesConverter coordinatesConverter)
    {
        _context = context;
        _model = model;
        _coordinatesConverter = coordinatesConverter;
        _decorationsCache = new Dictionary<string, ViewModelDecoration>(StringComparer.Ordinal);
    }

    public IReadOnlyList<IReadOnlyList<InlineDecoration>> GetInlineDecorations(int modelLineNumber)
    {
        var modelRange = new TextRange(modelLineNumber, 1, modelLineNumber, _model.GetLineMaxColumn(modelLineNumber));
        var viewRange = _coordinatesConverter.ConvertModelRangeToViewRange(modelRange);
        var decorationsViewportData = GetDecorations(viewRange, false, false);
        return decorationsViewportData.InlineDecorations;
    }

    public ViewDecorationsCollection GetDecorations(TextRange viewRange, bool onlyMinimapDecorations, bool onlyMarginDecorations)
    {
        var modelDecorations = _context.GetModelDecorations(viewRange, onlyMinimapDecorations, onlyMarginDecorations);
        var startLineNumber = viewRange.StartLineNumber;
        var endLineNumber = viewRange.EndLineNumber;

        var decorationsInViewport = new List<ViewModelDecoration>();
        var inlineDecorations = new List<InlineDecoration>[Math.Max(0, endLineNumber - startLineNumber + 1)];
        var hasVariableFonts = new bool[inlineDecorations.Length];
        for (var j = startLineNumber; j <= endLineNumber; j++)
        {
            inlineDecorations[j - startLineNumber] = new List<InlineDecoration>();
            hasVariableFonts[j - startLineNumber] = false;
        }

        for (int i = 0, len = modelDecorations.Count; i < len; i++)
        {
            var modelDecoration = modelDecorations[i];
            var decorationOptions = modelDecoration.Options;

            if (!ViewModelDecoration.IsModelDecorationVisible(_model, modelDecoration))
            {
                continue;
            }

            var viewModelDecoration = GetOrCreateViewModelDecoration(modelDecoration);
            var decorationViewRange = viewModelDecoration.Range;

            decorationsInViewport.Add(viewModelDecoration);

            if (!string.IsNullOrEmpty(decorationOptions.InlineClassName))
            {
                var inlineDecoration = new InlineDecoration(
                    decorationViewRange,
                    decorationOptions.InlineClassName,
                    decorationOptions.InlineClassNameAffectsLetterSpacing ? InlineDecorationType.RegularAffectingLetterSpacing : InlineDecorationType.Regular);
                var intersectedStartLineNumber = Math.Max(startLineNumber, decorationViewRange.StartLineNumber);
                var intersectedEndLineNumber = Math.Min(endLineNumber, decorationViewRange.EndLineNumber);
                for (var j = intersectedStartLineNumber; j <= intersectedEndLineNumber; j++)
                {
                    inlineDecorations[j - startLineNumber].Add(inlineDecoration);
                    if (decorationOptions.AffectsFont)
                    {
                        hasVariableFonts[j - startLineNumber] = true;
                    }
                }
            }

            if (!string.IsNullOrEmpty(decorationOptions.BeforeContentClassName))
            {
                if (startLineNumber <= decorationViewRange.StartLineNumber && decorationViewRange.StartLineNumber <= endLineNumber)
                {
                    var inlineDecoration = new InlineDecoration(
                        new TextRange(decorationViewRange.StartLineNumber, decorationViewRange.StartColumn, decorationViewRange.StartLineNumber, decorationViewRange.StartColumn),
                        decorationOptions.BeforeContentClassName,
                        InlineDecorationType.Before);
                    inlineDecorations[decorationViewRange.StartLineNumber - startLineNumber].Add(inlineDecoration);
                    if (decorationOptions.AffectsFont)
                    {
                        hasVariableFonts[decorationViewRange.StartLineNumber - startLineNumber] = true;
                    }
                }
            }

            if (!string.IsNullOrEmpty(decorationOptions.AfterContentClassName))
            {
                if (startLineNumber <= decorationViewRange.EndLineNumber && decorationViewRange.EndLineNumber <= endLineNumber)
                {
                    var inlineDecoration = new InlineDecoration(
                        new TextRange(decorationViewRange.EndLineNumber, decorationViewRange.EndColumn, decorationViewRange.EndLineNumber, decorationViewRange.EndColumn),
                        decorationOptions.AfterContentClassName,
                        InlineDecorationType.After);
                    inlineDecorations[decorationViewRange.EndLineNumber - startLineNumber].Add(inlineDecoration);
                    if (decorationOptions.AffectsFont)
                    {
                        hasVariableFonts[decorationViewRange.EndLineNumber - startLineNumber] = true;
                    }
                }
            }
        }

        return new ViewDecorationsCollection(decorationsInViewport, inlineDecorations, hasVariableFonts);
    }

    public void Reset()
    {
        _decorationsCache = new Dictionary<string, ViewModelDecoration>(StringComparer.Ordinal);
    }

    public void OnModelDecorationsChanged() => Reset();

    public void OnLineMappingChanged() => Reset();

    private ViewModelDecoration GetOrCreateViewModelDecoration(IModelDecoration modelDecoration)
    {
        var id = modelDecoration.Id;
        if (!_decorationsCache.TryGetValue(id, out var r))
        {
            var modelRange = modelDecoration.Range;
            var options = modelDecoration.Options;
            TextRange viewRange;
            if (options.IsWholeLine)
            {
                var start = _coordinatesConverter.ConvertModelPositionToViewPosition(new TextPosition(modelRange.StartLineNumber, 1), PositionAffinity.Left, false, true);
                var end = _coordinatesConverter.ConvertModelPositionToViewPosition(new TextPosition(modelRange.EndLineNumber, _model.GetLineMaxColumn(modelRange.EndLineNumber)), PositionAffinity.Right);
                viewRange = new TextRange(start.LineNumber, start.Column, end.LineNumber, end.Column);
            }
            else
            {
                // For backwards compatibility reasons, we want injected text before any decoration.
                // Thus, move decorations to the right.
                viewRange = _coordinatesConverter.ConvertModelRangeToViewRange(modelRange, PositionAffinity.Right);
            }

            r = new ViewModelDecoration(viewRange, options);
            _decorationsCache[id] = r;
        }

        return r;
    }
}

/// <summary>What an <see cref="InjectedTextInlineDecorationsComputer"/> reads about a model line.</summary>
internal interface IInjectedTextInlineDecorationsComputerContext
{
    /// <summary>Get the injections options for a model line number.</summary>
    IReadOnlyList<ModelDecorationInjectedTextOptions>? GetInjectionOptions(int modelLineNumber);

    /// <summary>Get the injection offsets for a model line number.</summary>
    IReadOnlyList<int>? GetInjectionOffsets(int modelLineNumber);

    /// <summary>Get the break offsets for a model line number.</summary>
    IReadOnlyList<int> GetBreakOffsets(int modelLineNumber);

    /// <summary>Get the wrapped text indent length for a model line number.</summary>
    int GetWrappedTextIndentLength(int modelLineNumber);

    /// <summary>Get the view line number for the first output line of a model line.</summary>
    int GetBaseViewLineNumber(int modelLineNumber);
}

/// <summary>Computes the inline decorations of the text injected into a model line.</summary>
internal sealed class InjectedTextInlineDecorationsComputer : IInlineDecorationsComputer
{
    private readonly IInjectedTextInlineDecorationsComputerContext _context;

    public InjectedTextInlineDecorationsComputer(IInjectedTextInlineDecorationsComputerContext context)
    {
        _context = context;
    }

    public IReadOnlyList<IReadOnlyList<InlineDecoration>> GetInlineDecorations(int modelLineNumber)
    {
        var injectionOffsets = _context.GetInjectionOffsets(modelLineNumber);
        if (injectionOffsets is null)
        {
            return [];
        }

        var lineInlineDecorations = new List<IReadOnlyList<InlineDecoration>>();
        var totalInjectedTextLengthBefore = 0;
        var currentInjectedOffset = 0;

        var injectionOptions = _context.GetInjectionOptions(modelLineNumber);
        var breakOffsets = _context.GetBreakOffsets(modelLineNumber);

        for (var outputLineIndex = 0; outputLineIndex < breakOffsets.Count; outputLineIndex++)
        {
            var inlineDecorations = new List<InlineDecoration>();
            lineInlineDecorations.Add(inlineDecorations);

            var lineStartOffsetInInputWithInjections = outputLineIndex > 0 ? breakOffsets[outputLineIndex - 1] : 0;
            var lineEndOffsetInInputWithInjections = breakOffsets[outputLineIndex];

            while (currentInjectedOffset < injectionOffsets.Count)
            {
                var options = injectionOptions![currentInjectedOffset];
                var length = options.Content.Length;
                var injectedTextStartOffsetInInputWithInjections = injectionOffsets[currentInjectedOffset] + totalInjectedTextLengthBefore;
                var injectedTextEndOffsetInInputWithInjections = injectedTextStartOffsetInInputWithInjections + length;
                var isWidthOnly = length == 0 && options.WidthInEm is not null;
                var isLastOutputLine = outputLineIndex == breakOffsets.Count - 1;
                var isAtInternalWrapBoundary = injectedTextStartOffsetInInputWithInjections == lineEndOffsetInInputWithInjections && !isLastOutputLine;

                if (injectedTextStartOffsetInInputWithInjections > lineEndOffsetInInputWithInjections || (isWidthOnly && isAtInternalWrapBoundary))
                {
                    // Injected text only starts in later wrapped lines.
                    break;
                }

                var isInLine = isWidthOnly
                    ? lineStartOffsetInInputWithInjections <= injectedTextStartOffsetInInputWithInjections
                        && (injectedTextStartOffsetInInputWithInjections < lineEndOffsetInInputWithInjections || isLastOutputLine)
                    : lineStartOffsetInInputWithInjections < injectedTextEndOffsetInInputWithInjections;
                if (isInLine)
                {
                    // Injected text ends after or in this line (but also starts in or before this line).
                    if (!string.IsNullOrEmpty(options.InlineClassName))
                    {
                        var wrappedTextIndentLength = _context.GetWrappedTextIndentLength(modelLineNumber);
                        var offset = outputLineIndex > 0 ? wrappedTextIndentLength : 0;
                        var start = offset + Math.Max(injectedTextStartOffsetInInputWithInjections - lineStartOffsetInInputWithInjections, 0);
                        var end = offset + Math.Min(injectedTextEndOffsetInInputWithInjections - lineStartOffsetInInputWithInjections, lineEndOffsetInInputWithInjections - lineStartOffsetInInputWithInjections);
                        if (start != end || isWidthOnly)
                        {
                            var viewLineNumber = _context.GetBaseViewLineNumber(modelLineNumber) + outputLineIndex;
                            var range = new TextRange(viewLineNumber, start + 1, viewLineNumber, end + 1);
                            var type = isWidthOnly
                                ? InlineDecorationType.WidthOnly
                                : options.InlineClassNameAffectsLetterSpacing
                                    ? InlineDecorationType.RegularAffectingLetterSpacing
                                    : InlineDecorationType.Regular;
                            inlineDecorations.Add(new InlineDecoration(range, options.InlineClassName, type));
                        }
                    }
                }

                if (injectedTextEndOffsetInInputWithInjections <= lineEndOffsetInInputWithInjections)
                {
                    totalInjectedTextLengthBefore += length;
                    currentInjectedOffset++;
                }
                else
                {
                    // injected text breaks into next line, process it again
                    break;
                }
            }
        }

        return lineInlineDecorations;
    }
}
