// Ported from vscode (src/vs/editor/contrib/stickyScroll/browser/stickyScrollProvider.ts: StickyLineCandidate,
// StickyLineCandidateProvider; stickyScrollController.ts: findScrollWidgetState; stickyScrollWidget.ts:
// StickyScrollWidgetState; commit b57d5c9e), MIT license.
// Differences:
//  - the provider takes an IStickyScrollEditor (the text model, line heights, hidden areas and the layout) instead of
//    the editor; it does not listen to the editor itself: the CodeEditor's controller calls Update (debounced, as
//    upstream's RunOnceScheduler(50) and the model provider's Delayer(300)) when the model, the providers or the hidden
//    areas change, and DidChangeStickyScroll fires after each update;
//  - findScrollWidgetState is a static function over the editor's layout (StickyScrollState.Find), so that it can be
//    tested without a view;
//  - the provider does not read the configuration: the controller creates it only while sticky scroll is enabled (the
//    view model's StickyScrollOptions) and passes the default model.

using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Contrib.StickyScroll;

/// <summary>A scope line that may stick, with where it sits in the widget (upstream's StickyLineCandidate).</summary>
internal readonly record struct StickyLineCandidate(int StartLineNumber, int EndLineNumber, double Top, double Height);

/// <summary>What sticky scroll needs of an editor.</summary>
internal interface IStickyScrollEditor
{
    /// <summary>The model; null when the editor has none.</summary>
    TextModel? GetModel();

    /// <summary>The height of a model line (upstream's getLineHeightForPosition).</summary>
    double GetLineHeightForLine(int lineNumber);

    /// <summary>The model ranges folded away (upstream's viewModel.getHiddenAreas()).</summary>
    IReadOnlyList<TextRange> GetHiddenAreas();

    /// <summary>The scroll position.</summary>
    double GetScrollTop();

    /// <summary>The model ranges that are visible (upstream's getVisibleRanges).</summary>
    IReadOnlyList<TextRange> GetVisibleRanges();

    /// <summary>The top of a model line in the scrolled content (upstream's getTopForLineNumber).</summary>
    double GetTopForLineNumber(int lineNumber);

    /// <summary>The bottom of a model line in the scrolled content (upstream's getBottomForLineNumber).</summary>
    double GetBottomForLineNumber(int lineNumber);
}

/// <summary>The scope lines that stick at the top (upstream's StickyScrollWidgetState).</summary>
internal sealed class StickyScrollWidgetState(IReadOnlyList<int> startLineNumbers, IReadOnlyList<int> endLineNumbers, double lastLineRelativePosition, int? showEndForLine = null)
{
    public IReadOnlyList<int> StartLineNumbers { get; } = startLineNumbers;
    public IReadOnlyList<int> EndLineNumbers { get; } = endLineNumbers;
    public double LastLineRelativePosition { get; } = lastLineRelativePosition;
    public int? ShowEndForLine { get; } = showEndForLine;

    public static readonly StickyScrollWidgetState Empty = new([], [], 0);

    /// <summary>Whether the two states show the same lines at the same place (upstream's equals).</summary>
    public bool StateEquals(StickyScrollWidgetState? state) =>
        state is not null
        && LastLineRelativePosition == state.LastLineRelativePosition
        && ShowEndForLine == state.ShowEndForLine
        && StartLineNumbers.SequenceEqual(state.StartLineNumbers)
        && EndLineNumbers.SequenceEqual(state.EndLineNumbers);
}

/// <summary>Keeps the sticky model of an editor and finds the scopes around a range (upstream's StickyLineCandidateProvider).</summary>
internal sealed class StickyLineCandidateProvider : IDisposable
{
    private readonly IStickyScrollEditor _editor;
    private readonly LanguageFeatures _languageFeatures;
    private readonly LanguageConfigurationService _languageConfigurationService;
    private readonly StickyScrollDefaultModel _defaultModel;
    private StickyModel? _model;
    private CancellationTokenSource? _cts;
    private StickyModelProvider? _stickyModelProvider;
    private TextModel? _providerModel;

    public StickyLineCandidateProvider(
        IStickyScrollEditor editor,
        LanguageFeatures languageFeatures,
        LanguageConfigurationService languageConfigurationService,
        StickyScrollDefaultModel defaultModel = StickyScrollDefaultModel.OutlineModel)
    {
        _editor = editor;
        _languageFeatures = languageFeatures;
        _languageConfigurationService = languageConfigurationService;
        _defaultModel = defaultModel;
    }

    /// <summary>Raised after an update (upstream's onDidChangeStickyScroll).</summary>
    public event Action? DidChangeStickyScroll;

    /// <summary>The model version the sticky model was computed for (upstream's getVersionId).</summary>
    public int? GetVersionId() => _model?.Version;

    /// <summary>The current sticky model (for tests).</summary>
    public StickyModel? Model => _model;

    /// <summary>Computes the sticky model again (upstream's update).</summary>
    public async Task Update()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        await UpdateStickyModel(_cts.Token).ConfigureAwait(true);
        DidChangeStickyScroll?.Invoke();
    }

    private async Task UpdateStickyModel(CancellationToken token)
    {
        var textModel = _editor.GetModel();
        if (textModel is null || textModel.IsTooLargeForTokenization())
        {
            _model = null;
            return;
        }

        if (!ReferenceEquals(_providerModel, textModel) || _stickyModelProvider is null)
        {
            // upstream's updateStickyModelProvider (on a new model)
            _providerModel = textModel;
            _stickyModelProvider = new StickyModelProvider(textModel, _languageFeatures, _languageConfigurationService, _defaultModel);
        }

        var model = await _stickyModelProvider.Update(token).ConfigureAwait(true);
        if (!token.IsCancellationRequested)
        {
            _model = model;
        }
    }

    /// <summary>The scope lines that intersect <paramref name="range"/>, outermost first, without folded ones (upstream's getCandidateStickyLinesIntersecting).</summary>
    public List<StickyLineCandidate> GetCandidateStickyLinesIntersecting(StickyRange range)
    {
        if (_model?.Element is not { } element)
        {
            return [];
        }

        var stickyLineCandidates = new List<StickyLineCandidate>();
        GetCandidateStickyLinesIntersectingFromStickyModel(range, element, stickyLineCandidates, 0, 0, -1);
        return FilterHiddenRanges(stickyLineCandidates);
    }

    private void GetCandidateStickyLinesIntersectingFromStickyModel(StickyRange range, StickyElement outlineModel, List<StickyLineCandidate> result, int depth, double top, int lastStartLineNumber)
    {
        var textModel = _editor.GetModel();
        if (textModel is null || outlineModel.Children.Count == 0)
        {
            return;
        }

        var lastLine = lastStartLineNumber;
        var childrenStartLines = new List<int>();
        foreach (var child in outlineModel.Children)
        {
            if (child.Range is { } childRange)
            {
                childrenStartLines.Add(childRange.StartLineNumber);
            }
        }

        var lowerBound = UpdateIndex(Arrays.BinarySearch(childrenStartLines, range.StartLineNumber, static (a, b) => a - b));
        var upperBound = UpdateIndex(Arrays.BinarySearch(childrenStartLines, range.EndLineNumber, static (a, b) => a - b));

        for (var i = lowerBound; i <= upperBound; i++)
        {
            if (i < 0 || i >= outlineModel.Children.Count || outlineModel.Children[i].Range is not { } childRange)
            {
                continue;
            }

            var child = outlineModel.Children[i];
            var (startLineNumber, endLineNumber) = (childRange.StartLineNumber, childRange.EndLineNumber);
            if (endLineNumber > startLineNumber + 1
                && range.StartLineNumber <= endLineNumber + 1
                && startLineNumber - 1 <= range.EndLineNumber
                && startLineNumber != lastLine
                && textModel.IsValidRange(new TextRange(startLineNumber, 1, endLineNumber, 1)))
            {
                lastLine = startLineNumber;
                var lineHeight = _editor.GetLineHeightForLine(startLineNumber);
                result.Add(new StickyLineCandidate(startLineNumber, endLineNumber - 1, top, lineHeight));
                GetCandidateStickyLinesIntersectingFromStickyModel(range, child, result, depth + 1, top + lineHeight, startLineNumber);
            }
        }
    }

    // Filter out sticky line candidates that are within hidden ranges.
    private List<StickyLineCandidate> FilterHiddenRanges(List<StickyLineCandidate> stickyLineCandidates)
    {
        var hiddenRanges = _editor.GetHiddenAreas();
        if (hiddenRanges.Count == 0)
        {
            return stickyLineCandidates;
        }

        return stickyLineCandidates
            .Where(candidate => !hiddenRanges.Any(hiddenRange =>
                candidate.StartLineNumber >= hiddenRange.StartLineNumber
                && candidate.EndLineNumber <= hiddenRange.EndLineNumber + 1))
            .ToList();
    }

    // Update the binary search index.
    private static int UpdateIndex(int index)
    {
        if (index == -1)
        {
            return 0;
        }

        if (index < 0)
        {
            return -index - 2;
        }

        return index;
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _stickyModelProvider = null;
        _model = null;
    }
}

/// <summary>Which scope lines stick for the current scroll position (upstream's StickyScrollController.findScrollWidgetState).</summary>
internal static class StickyScrollState
{
    /// <summary>
    /// The scopes whose first line has scrolled above the widget's slot for it and whose last line has not, at most
    /// <paramref name="maxNumberStickyLines"/>; the last one moves up as its scope ends (LastLineRelativePosition).
    /// </summary>
    public static StickyScrollWidgetState Find(IStickyScrollEditor editor, StickyLineCandidateProvider provider, int maxNumberStickyLines, int? showEndForLine = null)
    {
        var scrollTop = editor.GetScrollTop();
        double lastLineRelativePosition = 0;
        var startLineNumbers = new List<int>();
        var endLineNumbers = new List<int>();
        var arrayVisibleRanges = editor.GetVisibleRanges();
        if (arrayVisibleRanges.Count != 0)
        {
            var fullVisibleRange = new StickyRange(arrayVisibleRanges[0].StartLineNumber, arrayVisibleRanges[^1].EndLineNumber);
            var candidateRanges = provider.GetCandidateStickyLinesIntersecting(fullVisibleRange);
            foreach (var range in candidateRanges)
            {
                var start = range.StartLineNumber;
                var end = range.EndLineNumber;
                var topOfElement = range.Top;
                var bottomOfElement = topOfElement + range.Height;
                var topOfBeginningLine = editor.GetTopForLineNumber(start) - scrollTop;
                var bottomOfEndLine = editor.GetBottomForLineNumber(end) - scrollTop;
                if (topOfElement > topOfBeginningLine && topOfElement <= bottomOfEndLine)
                {
                    startLineNumbers.Add(start);
                    endLineNumbers.Add(end + 1);
                    if (bottomOfElement > bottomOfEndLine)
                    {
                        lastLineRelativePosition = bottomOfEndLine - bottomOfElement;
                    }
                }

                if (startLineNumbers.Count == maxNumberStickyLines)
                {
                    break;
                }
            }
        }

        return new StickyScrollWidgetState(startLineNumbers, endLineNumbers, lastLineRelativePosition, showEndForLine);
    }
}

/// <summary>
/// Sticky scroll's view of an editor through its view model (upstream's ICodeEditor methods getTopForLineNumber,
/// getBottomForLineNumber, getVisibleRanges, getScrollTop and the view model's hidden areas).
/// </summary>
internal sealed class StickyScrollViewModelEditor(Func<TextModel?> model, Func<ViewModel.ViewModel?> viewModel) : IStickyScrollEditor
{
    public TextModel? GetModel() => model();

    public double GetLineHeightForLine(int lineNumber)
    {
        if (viewModel() is not { } vm)
        {
            return 0;
        }

        var viewLine = vm.CoordinatesConverter.ConvertModelPositionToViewPosition(new TextPosition(lineNumber, 1)).LineNumber;
        return vm.ViewLayout.GetLineHeightForLineNumber(viewLine);
    }

    public IReadOnlyList<TextRange> GetHiddenAreas() => viewModel()?.GetHiddenAreas() ?? [];

    public double GetScrollTop() => viewModel()?.ViewLayout.GetCurrentScrollTop() ?? 0;

    public IReadOnlyList<TextRange> GetVisibleRanges() => viewModel()?.GetVisibleRanges() ?? [];

    public double GetTopForLineNumber(int lineNumber)
    {
        if (viewModel() is not { } vm)
        {
            return 0;
        }

        // upstream's _getVerticalOffsetForPosition(lineNumber, 1)
        var viewLine = vm.CoordinatesConverter.ConvertModelPositionToViewPosition(new TextPosition(lineNumber, 1)).LineNumber;
        return vm.ViewLayout.GetVerticalOffsetForLineNumber(viewLine);
    }

    public double GetBottomForLineNumber(int lineNumber)
    {
        if (viewModel() is not { } vm || model() is not { } textModel)
        {
            return 0;
        }

        // upstream's _getVerticalOffsetAfterPosition(lineNumber, maxColumn)
        var maxColumn = textModel.GetLineMaxColumn(Math.Clamp(lineNumber, 1, textModel.GetLineCount()));
        var viewLine = vm.CoordinatesConverter.ConvertModelPositionToViewPosition(new TextPosition(lineNumber, maxColumn)).LineNumber;
        return vm.ViewLayout.GetVerticalOffsetAfterLineNumber(viewLine);
    }
}
