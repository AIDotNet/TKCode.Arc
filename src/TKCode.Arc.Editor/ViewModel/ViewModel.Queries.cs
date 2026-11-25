// Ported from vscode (src/vs/editor/common/viewModel/viewModelImpl.ts: ViewModel, commit b57d5c9e), MIT license.
// This part: hidden areas, visible ranges, view state, the view lines and their rendering data, decorations, the
// overview ruler and the text to copy.
// Differences from upstream:
//  - getCompletelyVisibleViewRange is virtual: upstream's tests replace it on the instance, tests here override it;
//  - saveState returns the record ViewModelViewState (upstream's IViewState) and reduceRestoreState a tuple;
//  - getAllOverviewRulerDecorations takes the theme as an opaque object (as ViewThemeChangedEvent carries it) and
//    names a decoration's color by its value or, for a theme color, its id: there is no theme type to resolve colors
//    against, and so no color cache to invalidate (_invalidateDecorationsColorCache does nothing);
//  - getPlainTextToCopy returns one text (SourceText) or one text per range (SourceTexts), see IViewModel;
//  - getRichTextToCopy reads the token colors from the configuration's TokenColorMap (upstream's global
//    TokenizationRegistry); without one, or for color ids past its end, the colors read 'undefined' as they do in
//    upstream's template strings; the font size and line height print as JavaScript prints numbers (invariant culture).

using System.Globalization;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Tokens;

namespace TKCode.Arc.Editor.ViewModel;

/// <summary>The scroll state a view state keeps (upstream's IViewState).</summary>
/// <param name="ScrollLeft">The horizontal scroll position.</param>
/// <param name="FirstPosition">The model position of the first visible line; null in a state of an older format.</param>
/// <param name="FirstPositionDeltaTop">The offset of <paramref name="FirstPosition"/>'s line from the top of the viewport.</param>
/// <param name="ScrollTop">The vertical scroll position (older format).</param>
/// <param name="ScrollTopWithoutViewZones">The vertical scroll position without view zones (older format).</param>
internal sealed record ViewModelViewState(
    double ScrollLeft,
    TextPosition? FirstPosition,
    double FirstPositionDeltaTop,
    double? ScrollTop = null,
    double? ScrollTopWithoutViewZones = null);

internal partial class ViewModel
{
    public string? GetFontSizeAtPosition(TextPosition position)
    {
        var allowVariableFonts = _configuration.Options.EffectiveAllowVariableFonts;
        if (!allowVariableFonts)
        {
            return null;
        }
        var fontDecorations = Model.GetFontDecorationsInRange(new TextRange(position, position), _editorId);
        var fontSize = FormatNumber(_configuration.Options.FontInfo.FontSize) + "px";
        foreach (var fontDecoration in fontDecorations)
        {
            if (!string.IsNullOrEmpty(fontDecoration.Options.FontSize))
            {
                fontSize = fontDecoration.Options.FontSize;
                break;
            }
        }
        return fontSize;
    }

    /// <summary>Hides the model ranges of <paramref name="source"/> (folding, the API, ...).</summary>
    /// <param name="ranges">The model ranges to hide.</param>
    /// <param name="source">Who hides them; each source has its own ranges and the view hides their merge.</param>
    /// <param name="forceUpdate">
    /// If true, the hidden areas will be updated even if the new ranges are the same as the previous ranges. This is
    /// because the model might have changed, which resets the hidden areas, but not the last cached value.
    /// </param>
    public void SetHiddenAreas(IReadOnlyList<TextRange> ranges, object? source = null, bool forceUpdate = false)
    {
        _hiddenAreasModel.SetHiddenAreas(source, ranges);
        var mergedRanges = _hiddenAreasModel.GetMergedRanges();
        if (ReferenceEquals(mergedRanges, _previousHiddenAreas) && !forceUpdate)
        {
            return;
        }

        _previousHiddenAreas = mergedRanges;

        var stableViewport = CaptureStableViewport();

        var lineMappingChanged = false;
        try
        {
            var eventsCollector = _eventDispatcher.BeginEmitViewEvents();
            lineMappingChanged = _lines.SetHiddenAreas(mergedRanges);
            if (lineMappingChanged)
            {
                eventsCollector.EmitViewEvent(new ViewFlushedEvent());
                eventsCollector.EmitViewEvent(new ViewLineMappingChangedEvent());
                eventsCollector.EmitViewEvent(new ViewDecorationsChangedEvent(null));
                _cursor.OnLineMappingChanged(eventsCollector);
                _decorations.OnLineMappingChanged();
                ViewLayout.OnFlushed(GetLineCount(), GetCustomLineHeights());
                ViewLayout.OnHeightMaybeChanged();
            }

            var firstModelLineInViewPort = stableViewport.ViewportStartModelPosition?.LineNumber;
            var firstModelLineIsHidden = firstModelLineInViewPort is { } firstLine && firstLine != 0
                && mergedRanges.Any(range => range.StartLineNumber <= firstLine && firstLine <= range.EndLineNumber);
            if (!firstModelLineIsHidden)
            {
                stableViewport.RecoverViewportStart(CoordinatesConverter, ViewLayout);
            }
        }
        finally
        {
            _eventDispatcher.EndEmitViewEvents();
        }
        UpdateConfigurationViewLineCountNow();

        if (lineMappingChanged)
        {
            _eventDispatcher.EmitOutgoingEvent(new HiddenAreasChangedEvent());
        }
    }

    public List<TextRange> GetVisibleRangesPlusViewportAboveBelow()
    {
        var layoutHeight = _configuration.Options.Layout.LayoutInfo.Height;
        var lineHeight = _configuration.Options.Layout.LineHeight;
        var linesAround = (int)Math.Max(20, Math.Round(layoutHeight / lineHeight, MidpointRounding.AwayFromZero));
        var partialData = ViewLayout.GetLinesViewportData();
        var startViewLineNumber = Math.Max(1, partialData.CompletelyVisibleStartLineNumber - linesAround);
        var endViewLineNumber = Math.Min(GetLineCount(), partialData.CompletelyVisibleEndLineNumber + linesAround);

        return ToModelVisibleRanges(new TextRange(
            startViewLineNumber, GetLineMinColumn(startViewLineNumber),
            endViewLineNumber, GetLineMaxColumn(endViewLineNumber)));
    }

    public List<TextRange> GetVisibleRanges()
    {
        var visibleViewRange = GetCompletelyVisibleViewRange();
        return ToModelVisibleRanges(visibleViewRange);
    }

    public IReadOnlyList<TextRange> GetHiddenAreas()
    {
        return _lines.GetHiddenAreas();
    }

    private List<TextRange> ToModelVisibleRanges(TextRange visibleViewRange)
    {
        var visibleRange = CoordinatesConverter.ConvertViewRangeToModelRange(visibleViewRange);
        var hiddenAreas = _lines.GetHiddenAreas();

        if (hiddenAreas.Count == 0)
        {
            return [visibleRange];
        }

        var result = new List<TextRange>();
        var startLineNumber = visibleRange.StartLineNumber;
        var startColumn = visibleRange.StartColumn;
        var endLineNumber = visibleRange.EndLineNumber;
        var endColumn = visibleRange.EndColumn;
        for (int i = 0, len = hiddenAreas.Count; i < len; i++)
        {
            var hiddenStartLineNumber = hiddenAreas[i].StartLineNumber;
            var hiddenEndLineNumber = hiddenAreas[i].EndLineNumber;

            if (hiddenEndLineNumber < startLineNumber)
            {
                continue;
            }
            if (hiddenStartLineNumber > endLineNumber)
            {
                continue;
            }

            if (startLineNumber < hiddenStartLineNumber)
            {
                result.Add(new TextRange(
                    startLineNumber, startColumn,
                    hiddenStartLineNumber - 1, Model.GetLineMaxColumn(hiddenStartLineNumber - 1)));
            }
            startLineNumber = hiddenEndLineNumber + 1;
            startColumn = 1;
        }

        if (startLineNumber < endLineNumber || (startLineNumber == endLineNumber && startColumn < endColumn))
        {
            result.Add(new TextRange(
                startLineNumber, startColumn,
                endLineNumber, endColumn));
        }

        return result;
    }

    public virtual TextRange GetCompletelyVisibleViewRange()
    {
        var partialData = ViewLayout.GetLinesViewportData();
        var startViewLineNumber = partialData.CompletelyVisibleStartLineNumber;
        var endViewLineNumber = partialData.CompletelyVisibleEndLineNumber;

        return new TextRange(
            startViewLineNumber, GetLineMinColumn(startViewLineNumber),
            endViewLineNumber, GetLineMaxColumn(endViewLineNumber));
    }

    public TextRange GetCompletelyVisibleViewRangeAtScrollTop(double scrollTop)
    {
        var partialData = ViewLayout.GetLinesViewportDataAtScrollTop(scrollTop);
        var startViewLineNumber = partialData.CompletelyVisibleStartLineNumber;
        var endViewLineNumber = partialData.CompletelyVisibleEndLineNumber;

        return new TextRange(
            startViewLineNumber, GetLineMinColumn(startViewLineNumber),
            endViewLineNumber, GetLineMaxColumn(endViewLineNumber));
    }

    /// <summary>Applies <c>cursorSurroundingLines</c> and <c>stickyScroll</c> padding to the given view range.</summary>
    public TextRange GetViewRangeWithCursorPadding(TextRange viewRange)
    {
        var options = _configuration.Options;
        var cursorSurroundingLines = options.CursorSurroundingLines;
        var stickyScroll = options.StickyScroll;

        var startLineNumber = viewRange.StartLineNumber;
        var endLineNumber = viewRange.EndLineNumber;
        var padding = Math.Min(
            Math.Max(cursorSurroundingLines, stickyScroll.Enabled ? stickyScroll.MaxLineCount : 0),
            (endLineNumber - startLineNumber + 1) / 2);

        startLineNumber += padding;
        endLineNumber -= Math.Max(0, padding - 1);

        if (padding == 0 || startLineNumber > endLineNumber)
        {
            return viewRange;
        }

        return new TextRange(
            startLineNumber, GetLineMinColumn(startLineNumber),
            endLineNumber, GetLineMaxColumn(endLineNumber));
    }

    public ViewModelViewState SaveState()
    {
        var compatViewState = ViewLayout.SaveState();

        var scrollTop = compatViewState.ScrollTop;
        var firstViewLineNumber = ViewLayout.GetLineNumberAtVerticalOffset(scrollTop);
        var firstPosition = CoordinatesConverter.ConvertViewPositionToModelPosition(new TextPosition(firstViewLineNumber, GetLineMinColumn(firstViewLineNumber)));
        var firstPositionDeltaTop = ViewLayout.GetVerticalOffsetForLineNumber(firstViewLineNumber) - scrollTop;

        return new ViewModelViewState(compatViewState.ScrollLeft, firstPosition, firstPositionDeltaTop);
    }

    public (double ScrollLeft, double ScrollTop) ReduceRestoreState(ViewModelViewState state)
    {
        if (state.FirstPosition is not { } firstPosition)
        {
            // This is a view state serialized by an older version
            return ReduceRestoreStateCompatibility(state);
        }

        var modelPosition = Model.ValidatePosition(firstPosition);
        var viewPosition = CoordinatesConverter.ConvertModelPositionToViewPosition(modelPosition);
        var scrollTop = ViewLayout.GetVerticalOffsetForLineNumber(viewPosition.LineNumber) - state.FirstPositionDeltaTop;
        return (state.ScrollLeft, scrollTop);
    }

    private static (double ScrollLeft, double ScrollTop) ReduceRestoreStateCompatibility(ViewModelViewState state)
    {
        return (state.ScrollLeft, state.ScrollTopWithoutViewZones!.Value);
    }

    private int GetTabSize()
    {
        return Model.GetOptions().TabSize;
    }

    public int GetLineCount()
    {
        return _lines.GetViewLineCount();
    }

    /// <summary>Gives a hint that a lot of requests are about to come in for these line numbers.</summary>
    public void SetViewport(int startLineNumber, int endLineNumber, int centeredLineNumber)
    {
        _viewportStart.Update(this, startLineNumber);
    }

    public ActiveIndentGuideInfo GetActiveIndentGuide(int lineNumber, int minLineNumber, int maxLineNumber)
    {
        return _lines.GetActiveIndentGuide(lineNumber, minLineNumber, maxLineNumber);
    }

    public IReadOnlyList<int> GetLinesIndentGuides(int startLineNumber, int endLineNumber)
    {
        return _lines.GetViewLinesIndentGuides(startLineNumber, endLineNumber);
    }

    public IReadOnlyList<IReadOnlyList<IndentGuide>> GetBracketGuidesInRangeByLine(
        int startLineNumber,
        int endLineNumber,
        TextPosition? activePosition,
        BracketGuideOptions options)
    {
        return _lines.GetViewLinesBracketGuides(startLineNumber, endLineNumber, activePosition, options);
    }

    public string GetLineContent(int lineNumber)
    {
        return _lines.GetViewLineContent(lineNumber);
    }

    public int GetLineLength(int lineNumber)
    {
        return _lines.GetViewLineLength(lineNumber);
    }

    public int GetLineMinColumn(int lineNumber)
    {
        return _lines.GetViewLineMinColumn(lineNumber);
    }

    public int GetLineMaxColumn(int lineNumber)
    {
        return _lines.GetViewLineMaxColumn(lineNumber);
    }

    public int GetLineFirstNonWhitespaceColumn(int lineNumber)
    {
        var result = Strings.FirstNonWhitespaceIndex(GetLineContent(lineNumber));
        if (result == -1)
        {
            return 0;
        }
        return result + 1;
    }

    public int GetLineLastNonWhitespaceColumn(int lineNumber)
    {
        var result = Strings.LastNonWhitespaceIndex(GetLineContent(lineNumber));
        if (result == -1)
        {
            return 0;
        }
        return result + 2;
    }

    public IReadOnlyList<ViewModelDecoration> GetMinimapDecorationsInRange(TextRange range)
    {
        return _decorations.GetMinimapDecorationsInRange(range);
    }

    public IReadOnlyList<ViewModelDecoration> GetDecorationsInViewport(TextRange visibleRange)
    {
        return _decorations.GetDecorationsViewportData(visibleRange).Decorations;
    }

    public InjectedText? GetInjectedTextAt(TextPosition viewPosition)
    {
        return _lines.GetInjectedTextAt(viewPosition);
    }

    private static TextDirection GetTextDirection(int lineNumber, IReadOnlyList<ViewModelDecoration> decorations)
    {
        var rtlCount = 0;

        foreach (var decoration in decorations)
        {
            var range = decoration.Range;
            if (range.StartLineNumber > lineNumber || range.EndLineNumber < lineNumber)
            {
                continue;
            }
            var textDirection = decoration.Options.TextDirection;
            if (textDirection == TextDirection.RTL)
            {
                rtlCount++;
            }
            else if (textDirection == TextDirection.LTR)
            {
                rtlCount--;
            }
        }

        return rtlCount > 0 ? TextDirection.RTL : TextDirection.LTR;
    }

    public TextDirection GetTextDirection(int lineNumber)
    {
        var decorationsCollection = _decorations.GetDecorationsOnLine(lineNumber);
        return GetTextDirection(lineNumber, decorationsCollection.Decorations);
    }

    public ViewLineRenderingData GetViewportViewLineRenderingData(TextRange visibleRange, int lineNumber)
    {
        var viewportDecorationsCollection = _decorations.GetDecorationsViewportData(visibleRange);
        var relativeLineNumber = lineNumber - visibleRange.StartLineNumber;
        var inlineDecorations = viewportDecorationsCollection.InlineDecorations[relativeLineNumber];
        var hasVariableFonts = viewportDecorationsCollection.HasVariableFonts[relativeLineNumber];
        return GetViewLineRenderingData(lineNumber, inlineDecorations, hasVariableFonts, viewportDecorationsCollection.Decorations);
    }

    public ViewLineRenderingData GetViewLineRenderingData(int lineNumber)
    {
        var decorationsCollection = _decorations.GetDecorationsOnLine(lineNumber);
        return GetViewLineRenderingData(lineNumber, decorationsCollection.InlineDecorations[0], decorationsCollection.HasVariableFonts[0], decorationsCollection.Decorations);
    }

    private ViewLineRenderingData GetViewLineRenderingData(
        int lineNumber,
        IReadOnlyList<InlineDecoration> inlineDecorations,
        bool hasVariableFonts,
        IReadOnlyList<ViewModelDecoration> decorations)
    {
        var mightContainRTL = Model.MightContainRTL();
        var mightContainNonBasicASCII = Model.MightContainNonBasicASCII();
        var tabSize = GetTabSize();
        var lineData = _lines.GetViewLineData(lineNumber);

        if (lineData.InlineDecorations is { } lineInlineDecorations)
        {
            inlineDecorations = [.. inlineDecorations, .. lineInlineDecorations];
        }

        return new ViewLineRenderingData(
            lineData.MinColumn,
            lineData.MaxColumn,
            lineData.Content,
            lineData.ContinuesWithWrappedLine,
            mightContainRTL,
            mightContainNonBasicASCII,
            lineData.Tokens,
            inlineDecorations,
            tabSize,
            lineData.StartVisibleColumn,
            GetTextDirection(lineNumber, decorations),
            hasVariableFonts);
    }

    public ViewLineData GetViewLineData(int lineNumber)
    {
        return _lines.GetViewLineData(lineNumber);
    }

    public bool GetViewLineContinuesWithWrappedLine(int lineNumber)
    {
        return _lines.GetViewLineContinuesWithWrappedLine(lineNumber);
    }

    public MinimapLinesRenderingData GetMinimapLinesRenderingData(int startLineNumber, int endLineNumber, IReadOnlyList<bool> needed)
    {
        var result = _lines.GetViewLinesData(startLineNumber, endLineNumber, needed);
        return new MinimapLinesRenderingData(
            GetTabSize(),
            result);
    }

    public IReadOnlyList<OverviewRulerDecorationsGroup> GetAllOverviewRulerDecorations(object theme)
    {
        var decorations = Model.GetOverviewRulerDecorations(_editorId, _configuration.FilterValidationDecorations(), _configuration.FilterFontDecorations());
        var result = new OverviewRulerDecorations();
        foreach (var decoration in decorations)
        {
            var decorationOptions = decoration.Options;
            var opts = decorationOptions.OverviewRuler;
            if (opts is null)
            {
                continue;
            }
            var lane = (int)opts.Position;
            if (lane == 0)
            {
                continue;
            }
            var color = opts.Color.Value ?? opts.Color.ThemeColorId ?? "";
            var viewStartLineNumber = CoordinatesConverter.GetViewLineNumberOfModelPosition(decoration.Range.StartLineNumber, decoration.Range.StartColumn);
            var viewEndLineNumber = CoordinatesConverter.GetViewLineNumberOfModelPosition(decoration.Range.EndLineNumber, decoration.Range.EndColumn);

            result.Accept(color, decorationOptions.ZIndex, viewStartLineNumber, viewEndLineNumber, lane);
        }
        return result.AsArray;
    }

    private static void InvalidateDecorationsColorCache()
    {
        // Upstream drops the colors the overview ruler and minimap options resolved against the old theme; colors are
        // not resolved (or cached) here.
    }

    public string GetValueInRange(TextRange range, EndOfLinePreference eol)
    {
        var modelRange = CoordinatesConverter.ConvertViewRangeToModelRange(range);
        return Model.GetValueInRange(modelRange, eol);
    }

    public int GetValueLengthInRange(TextRange range, EndOfLinePreference eol)
    {
        var modelRange = CoordinatesConverter.ConvertViewRangeToModelRange(range);
        return Model.GetValueLengthInRange(modelRange, eol);
    }

    public TextPosition ModifyPosition(TextPosition position, int offset)
    {
        var modelPosition = CoordinatesConverter.ConvertViewPositionToModelPosition(position);
        var resultModelPosition = Model.ModifyPosition(modelPosition, offset);
        return CoordinatesConverter.ConvertModelPositionToViewPosition(resultModelPosition);
    }

    public TextPosition DeduceModelPositionRelativeToViewPosition(TextPosition viewAnchorPosition, int deltaOffset, int lineFeedCnt)
    {
        var modelAnchor = CoordinatesConverter.ConvertViewPositionToModelPosition(viewAnchorPosition);
        if (Model.GetEOL().Length == 2)
        {
            // This model uses CRLF, so the delta must take that into account
            if (deltaOffset < 0)
            {
                deltaOffset -= lineFeedCnt;
            }
            else
            {
                deltaOffset += lineFeedCnt;
            }
        }

        var modelAnchorOffset = Model.GetOffsetAt(modelAnchor);
        var resultOffset = modelAnchorOffset + deltaOffset;
        return Model.GetPositionAt(resultOffset);
    }

    public (IReadOnlyList<TextRange> SourceRanges, string? SourceText, IReadOnlyList<string>? SourceTexts) GetPlainTextToCopy(
        IReadOnlyList<TextRange> modelRanges,
        bool emptySelectionClipboard,
        bool forceCRLF)
    {
        var newLineCharacter = forceCRLF ? "\r\n" : Model.GetEOL();

        var sortedRanges = modelRanges
            .Select((range, index) => (range, index))
            .OrderBy(x => x.range, Comparer<TextRange>.Create(TextRange.CompareRangesUsingStarts))
            .ThenBy(x => x.index)
            .Select(x => x.range)
            .ToList();

        var hasEmptyRange = false;
        var hasNonEmptyRange = false;
        foreach (var range in sortedRanges)
        {
            if (range.IsEmpty)
            {
                hasEmptyRange = true;
            }
            else
            {
                hasNonEmptyRange = true;
            }
        }

        if (!hasNonEmptyRange && !emptySelectionClipboard)
        {
            // all ranges are empty
            return ([], "", null);
        }

        var ranges = new List<TextRange>();
        var result = new List<string>();
        void PushRange(TextRange modelRange, string append = "")
        {
            ranges.Add(modelRange);
            result.Add(Model.GetValueInRange(modelRange, forceCRLF ? EndOfLinePreference.CRLF : EndOfLinePreference.TextDefined) + append);
        }

        if (hasEmptyRange && emptySelectionClipboard)
        {
            // some (maybe all) empty selections
            var prevModelLineNumber = 0;
            foreach (var modelRange in sortedRanges)
            {
                var modelLineNumber = modelRange.StartLineNumber;
                if (modelRange.IsEmpty)
                {
                    if (modelLineNumber != prevModelLineNumber)
                    {
                        PushRange(new TextRange(modelLineNumber, Model.GetLineMinColumn(modelLineNumber), modelLineNumber, Model.GetLineMaxColumn(modelLineNumber)), newLineCharacter);
                    }
                }
                else
                {
                    PushRange(modelRange);
                }
                prevModelLineNumber = modelLineNumber;
            }
        }
        else
        {
            foreach (var modelRange in sortedRanges)
            {
                if (!modelRange.IsEmpty)
                {
                    PushRange(modelRange);
                }
            }
        }

        return result.Count == 1 ? (ranges, result[0], null) : (ranges, null, result);
    }

    public (string Html, string Mode)? GetRichTextToCopy(IReadOnlyList<TextRange> modelRanges, bool emptySelectionClipboard)
    {
        var languageId = Model.GetLanguageId();
        if (languageId == LanguageIds.PlaintextLanguageId)
        {
            return null;
        }

        if (modelRanges.Count != 1)
        {
            // no multiple selection support at this time
            return null;
        }

        var range = modelRanges[0];
        if (range.IsEmpty)
        {
            if (!emptySelectionClipboard)
            {
                // nothing to copy
                return null;
            }
            var lineNumber = range.StartLineNumber;
            range = new TextRange(lineNumber, Model.GetLineMinColumn(lineNumber), lineNumber, Model.GetLineMaxColumn(lineNumber));
        }

        var fontInfo = _configuration.Options.FontInfo;
        var colorMap = GetColorMap();
        var hasBadChars = fontInfo.FontFamily.AsSpan().IndexOfAny(":;\\/<>") >= 0;
        var useDefaultFontFamily = hasBadChars || fontInfo.FontFamily == BareFontInfo.DefaultFontFamily;
        string fontFamily;
        if (useDefaultFontFamily)
        {
            fontFamily = BareFontInfo.DefaultFontFamily;
        }
        else
        {
            fontFamily = fontInfo.FontFamily;
            fontFamily = fontFamily.Replace('"', '\'');
            var hasQuotesOrIsList = fontFamily.AsSpan().IndexOfAny(",'") >= 0;
            if (!hasQuotesOrIsList)
            {
                var needsQuotes = fontFamily.AsSpan().IndexOfAny("+ ") >= 0;
                if (needsQuotes)
                {
                    fontFamily = $"'{fontFamily}'";
                }
            }
            fontFamily = $"{fontFamily}, {BareFontInfo.DefaultFontFamily}";
        }

        var html =
            "<div style=\""
            + $"color: {colorMap[(int)ColorId.DefaultForeground]};"
            + $"background-color: {colorMap[(int)ColorId.DefaultBackground]};"
            + $"font-family: {fontFamily};"
            + $"font-weight: {fontInfo.FontWeight};"
            + $"font-size: {FormatNumber(fontInfo.FontSize)}px;"
            + $"line-height: {FormatNumber(fontInfo.LineHeight)}px;"
            + "white-space: pre;"
            + "\">"
            + GetHTMLToCopy(range, colorMap)
            + "</div>";
        return (html, languageId);
    }

    private string GetHTMLToCopy(TextRange modelRange, IReadOnlyList<string> colorMap)
    {
        var startLineNumber = modelRange.StartLineNumber;
        var startColumn = modelRange.StartColumn;
        var endLineNumber = modelRange.EndLineNumber;
        var endColumn = modelRange.EndColumn;

        var tabSize = GetTabSize();

        var result = "";

        for (var lineNumber = startLineNumber; lineNumber <= endLineNumber; lineNumber++)
        {
            var lineTokens = Model.Tokenization.GetLineTokens(lineNumber);
            var lineContent = lineTokens.GetLineContent();
            var startOffset = lineNumber == startLineNumber ? startColumn - 1 : 0;
            var endOffset = lineNumber == endLineNumber ? endColumn - 1 : lineContent.Length;

            if (lineContent.Length == 0)
            {
                result += "<br>";
            }
            else
            {
                result += TextToHtmlTokenizer.TokenizeLineToHTML(lineContent, lineTokens.Inflate(), colorMap, startOffset, endOffset, tabSize, OperatingSystem.IsWindows());
            }
        }

        return result;
    }

    private List<string> GetColorMap()
    {
        var colorMap = _configuration.Options.TokenColorMap;
        var result = new List<string> { "#000000" };
        if (colorMap is not null)
        {
            for (int i = 1, len = colorMap.Count; i < len; i++)
            {
                result.Add(colorMap[i]);
            }
        }
        // Reading past the end of upstream's array gives undefined, which its template strings print as 'undefined'.
        while (result.Count < 512)
        {
            result.Add("undefined");
        }
        return result;
    }

    // A number as JavaScript's String(number) prints it, for whole numbers and simple fractions.
    private static string FormatNumber(double value) => value.ToString(CultureInfo.InvariantCulture);
}
