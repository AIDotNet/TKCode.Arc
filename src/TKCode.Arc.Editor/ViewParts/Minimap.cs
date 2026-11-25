// Ported from vscode (src/vs/editor/browser/viewParts/minimap/minimap.ts: Minimap (the IMinimapModel) and the drawing
// parts of InnerMinimap, commit b57d5c9e), MIT license.
//
// Differences:
//  - no DOM: Render returns a MinimapFrame (the layout, the slider, the line image with the rows that changed, and the
//    decoration rectangles) that the host draws; pointer handling (click to reveal, slider dragging) is in the host and
//    calls RevealLineNumber, GetLineNumberAtY, SetScrollTop and SetEditorScrollTop here;
//  - options come from the host (SetOptions, upstream's _onOptionsMaybeChanged) instead of the configuration;
//  - the rendering context (viewport lines, scroll position) is read from the view model's layout when rendering;
//  - decoration colors are resolved by DecorationColorResolver (upstream: ModelDecorationMinimapOptions.getColor with
//    the theme); the selection color is MinimapOptions.SelectionColor;
//  - decorations are a list of rectangles (upstream records canvas drawings and replays the rows that differ:
//    minimapDirtyRows.ts, not ported); section headers are not drawn;
//  - touch gestures, autohide and the shadow's DOM classes are the host's.

using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.ViewLayout;
using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc.Editor.ViewParts;

/// <summary>A filled rectangle of the minimap's decorations, in canvas (device) pixels.</summary>
internal readonly record struct MinimapRect(RgbaColor Color, double X, double Y, double Width, double Height);

/// <summary>What the host draws for a minimap frame.</summary>
internal sealed class MinimapFrame
{
    public MinimapFrame(MinimapLayout layout, MinimapImageData? imageData, int dirtyY1, int dirtyY2, IReadOnlyList<MinimapRect> decorations, bool decorationsChanged, bool shadowVisible)
    {
        Layout = layout;
        ImageData = imageData;
        DirtyY1 = dirtyY1;
        DirtyY2 = dirtyY2;
        Decorations = decorations;
        DecorationsChanged = decorationsChanged;
        ShadowVisible = shadowVisible;
    }

    public MinimapLayout Layout { get; }

    /// <summary>The image of the lines (canvas inner size), or null for an empty canvas.</summary>
    public MinimapImageData? ImageData { get; }

    /// <summary>The first row of <see cref="ImageData"/> that differs from the last frame's.</summary>
    public int DirtyY1 { get; }

    /// <summary>The row after the last one that differs (<see cref="DirtyY1"/> = <see cref="DirtyY2"/>: nothing to copy).</summary>
    public int DirtyY2 { get; }

    /// <summary>Selections and decorations, bottom to top.</summary>
    public IReadOnlyList<MinimapRect> Decorations { get; }

    public bool DecorationsChanged { get; }

    /// <summary>Whether the text is scrolled horizontally so that it continues under the minimap.</summary>
    public bool ShadowVisible { get; }
}

/// <summary>
/// The minimap: a zoomed out picture of the view lines beside the scroll bar, with the slider that shows the
/// viewport. One per editor view; it listens to the view model's events and keeps the image of its last frame, so
/// a frame repaints only the lines that changed.
/// </summary>
internal sealed class Minimap : ViewEventHandler
{
    /// <summary>The orthogonal distance to the slider at which dragging "resets" (snapping).</summary>
    public const int PointerDragResetDistance = 140;

    private const int GutterDecorationWidth = 2;

    private readonly IViewModel _viewModel;
    private IReadOnlyList<TextSelection> _selections = [];
    private List<TextSelection>? _minimapSelections;
    private MinimapSamplingState? _samplingState;
    private bool _shouldCheckSampling;

    // InnerMinimap
    private MinimapRenderData? _lastRenderData;
    private MinimapBuffers? _buffers;
    private bool _renderDecorations = true;
    private IReadOnlyList<MinimapRect> _decorations = [];

    public Minimap(IViewModel viewModel, MinimapOptions options)
    {
        _viewModel = viewModel;
        Options = options;
        _samplingState = MinimapSamplingState.Compute(Options, _viewModel.GetLineCount(), null).State;
        _selections = [.. _viewModel.GetCursorStates().Select(s => s.ViewState.Selection)];
    }

    public MinimapOptions Options { get; private set; }

    /// <summary>The color of a decoration's minimap options in the current theme (upstream's getColor(theme)); null draws nothing.</summary>
    public Func<ModelDecorationMinimapOptions, RgbaColor?> DecorationColorResolver { get; set; } = DefaultDecorationColor;

    /// <summary>The layout of the last frame (for pointer handling), or null before the first.</summary>
    public MinimapLayout? LastLayout => _lastRenderData?.RenderedLayout;

    internal MinimapSamplingState? SamplingState => _samplingState;

    private static RgbaColor? DefaultDecorationColor(ModelDecorationMinimapOptions options) =>
        RgbaColor.TryParseHex(options.Color.Value);

    /// <summary>Takes new options (upstream's _onOptionsMaybeChanged); false when they draw the same.</summary>
    public bool SetOptions(MinimapOptions options)
    {
        if (Options.Equals(options))
        {
            return false;
        }

        Options = options;
        RecreateLineSampling();
        // InnerMinimap.onDidChangeOptions
        _lastRenderData = null;
        _buffers = null;
        _renderDecorations = true;
        SetShouldRender();
        return true;
    }

    /// <summary>Repaints the decorations on the next frame (a theme or decoration color change).</summary>
    public void InvalidateDecorations()
    {
        _renderDecorations = true;
        SetShouldRender();
    }

    // ---- begin view event handlers

    public override bool OnCursorStateChanged(ViewCursorStateChangedEvent e)
    {
        _selections = e.Selections;
        _minimapSelections = null;
        _renderDecorations = true;
        return true;
    }

    public override bool OnDecorationsChanged(ViewDecorationsChangedEvent e)
    {
        if (e.AffectsMinimap)
        {
            _renderDecorations = true;
            return true;
        }

        return false;
    }

    public override bool OnFlushed(ViewFlushedEvent e)
    {
        if (_samplingState is not null)
        {
            _shouldCheckSampling = true;
        }

        _lastRenderData = null;
        return true;
    }

    public override bool OnLinesChanged(ViewLinesChangedEvent e)
    {
        if (_samplingState is not null)
        {
            var minimapLineRange = _samplingState.ModelLineRangeToMinimapLineRange(e.FromLineNumber, e.FromLineNumber + e.Count - 1);
            if (minimapLineRange is { } range)
            {
                return InnerOnLinesChanged(range.From, range.To - range.From + 1);
            }

            return false;
        }

        return InnerOnLinesChanged(e.FromLineNumber, e.Count);
    }

    public override bool OnLinesDeleted(ViewLinesDeletedEvent e)
    {
        if (_samplingState is not null)
        {
            var (changeStartIndex, changeEndIndex) = _samplingState.OnLinesDeleted(e);
            if (changeStartIndex <= changeEndIndex)
            {
                InnerOnLinesChanged(changeStartIndex + 1, changeEndIndex - changeStartIndex + 1);
            }

            _shouldCheckSampling = true;
            return true;
        }

        _lastRenderData?.OnLinesDeleted(e.FromLineNumber, e.ToLineNumber);
        return true;
    }

    public override bool OnLinesInserted(ViewLinesInsertedEvent e)
    {
        if (_samplingState is not null)
        {
            _samplingState.OnLinesInserted(e);
            _shouldCheckSampling = true;
            return true;
        }

        _lastRenderData?.OnLinesInserted(e.FromLineNumber, e.ToLineNumber);
        return true;
    }

    public override bool OnScrollChanged(ViewScrollChangedEvent e)
    {
        _renderDecorations = true;
        return true;
    }

    public override bool OnThemeChanged(ViewThemeChangedEvent e)
    {
        _renderDecorations = true;
        return true;
    }

    public override bool OnTokensChanged(ViewTokensChangedEvent e)
    {
        if (_samplingState is not null)
        {
            var ranges = new List<ViewTokensChangedRange>();
            foreach (var range in e.Ranges)
            {
                if (_samplingState.ModelLineRangeToMinimapLineRange(range.FromLineNumber, range.ToLineNumber) is { } minimapLineRange)
                {
                    ranges.Add(new ViewTokensChangedRange(minimapLineRange.From, minimapLineRange.To));
                }
            }

            return ranges.Count > 0 && (_lastRenderData?.OnTokensChanged(ranges) ?? false);
        }

        return _lastRenderData?.OnTokensChanged(e.Ranges) ?? false;
    }

    public override bool OnTokensColorsChanged(ViewTokensColorsChangedEvent e)
    {
        _lastRenderData = null;
        _buffers = null;
        return true;
    }

    public override bool OnZonesChanged(ViewZonesChangedEvent e)
    {
        _lastRenderData = null;
        return true;
    }

    private bool InnerOnLinesChanged(int changeFromLineNumber, int changeCount) =>
        _lastRenderData?.OnLinesChanged(changeFromLineNumber, changeCount) ?? false;

    // ---- end view event handlers

    /// <summary>Lays out and draws a frame at the view layout's current scroll position.</summary>
    public MinimapFrame? Render()
    {
        // prepareRender
        if (_shouldCheckSampling)
        {
            _shouldCheckSampling = false;
            RecreateLineSampling();
        }

        OnDidRender();
        if (Options.RenderMinimap == RenderMinimap.None)
        {
            return null;
        }

        var viewLayout = _viewModel.ViewLayout;
        var linesViewport = viewLayout.GetLinesViewportData();
        var viewport = viewLayout.GetCurrentViewport();
        var viewportStartLineNumber = linesViewport.StartLineNumber;
        var viewportEndLineNumber = linesViewport.EndLineNumber;

        var scrollTop = viewLayout.GetCurrentScrollTop();
        var scrollHeight = viewLayout.GetScrollHeight();
        if (Options.MinimapHeightIsEditorHeight)
        {
            (scrollTop, scrollHeight) = MinimapLayout.GetZoneFreeScrollPosition(MinimapLineOffsets.From(viewLayout), GetRealLineCount(), Options.LineHeight, Options.PaddingTop, viewportStartLineNumber, viewport.Height, scrollTop, scrollHeight);
        }

        if (_samplingState is not null)
        {
            viewportStartLineNumber = _samplingState.ModelLineToMinimapLine(viewportStartLineNumber);
            viewportEndLineNumber = _samplingState.ModelLineToMinimapLine(viewportEndLineNumber);
        }

        // InnerMinimap.render
        var shadowVisible = viewLayout.GetCurrentScrollLeft() + viewport.Width < viewLayout.GetScrollWidth();
        var layout = MinimapLayout.Create(
            Options,
            viewportStartLineNumber,
            viewportEndLineNumber,
            viewLayout.GetVerticalOffsetForLineNumber(viewportStartLineNumber),
            viewLayout.GetLineHeightForLineNumber(viewportStartLineNumber),
            viewport.Height,
            viewLayout.GetWhitespaceViewportData().Length > 0,
            GetLineCount(),
            GetRealLineCount(),
            scrollTop,
            scrollHeight,
            _lastRenderData?.RenderedLayout);

        var decorationsChanged = RenderDecorations(layout);
        var (renderData, dirtyY1, dirtyY2) = RenderLines(layout);
        _lastRenderData = renderData;
        return new MinimapFrame(layout, renderData?.Get().ImageData, dirtyY1, dirtyY2, _decorations, decorationsChanged, shadowVisible);
    }

    //#region IMinimapModel

    private void RecreateLineSampling()
    {
        _minimapSelections = null;

        var wasSampling = _samplingState is not null;
        var (samplingState, events) = MinimapSamplingState.Compute(Options, _viewModel.GetLineCount(), _samplingState);
        _samplingState = samplingState;

        if (wasSampling && _samplingState is not null)
        {
            // was sampling, is sampling
            foreach (var e in events)
            {
                switch (e.Type)
                {
                    case MinimapSamplingEventType.Deleted:
                        _lastRenderData?.OnLinesDeleted(e.FromLineNumber, e.ToLineNumber);
                        break;
                    case MinimapSamplingEventType.Inserted:
                        _lastRenderData?.OnLinesInserted(e.FromLineNumber, e.ToLineNumber);
                        break;
                    case MinimapSamplingEventType.Flush:
                        _lastRenderData = null;
                        break;
                }
            }
        }
    }

    public int GetLineCount() => _samplingState?.MinimapLines.Length ?? _viewModel.GetLineCount();

    public int GetRealLineCount() => _viewModel.GetLineCount();

    public string GetLineContent(int lineNumber) =>
        _viewModel.GetLineContent(_samplingState is not null ? _samplingState.MinimapLines[lineNumber - 1] : lineNumber);

    public int GetLineMaxColumn(int lineNumber) =>
        _viewModel.GetLineMaxColumn(_samplingState is not null ? _samplingState.MinimapLines[lineNumber - 1] : lineNumber);

    private IReadOnlyList<ViewLineData?> GetMinimapLinesRenderingData(int startLineNumber, int endLineNumber, bool[] needed)
    {
        if (_samplingState is not null)
        {
            var result = new ViewLineData?[endLineNumber - startLineNumber + 1];
            for (var lineIndex = 0; lineIndex < result.Length; lineIndex++)
            {
                result[lineIndex] = needed[lineIndex] ? _viewModel.GetViewLineData(_samplingState.MinimapLines[startLineNumber + lineIndex - 1]) : null;
            }

            return result;
        }

        return _viewModel.GetMinimapLinesRenderingData(startLineNumber, endLineNumber, needed).Data;
    }

    public IReadOnlyList<TextSelection> GetSelections()
    {
        if (_minimapSelections is null)
        {
            if (_samplingState is not null)
            {
                _minimapSelections = [];
                foreach (var selection in _selections)
                {
                    var range = selection.Range;
                    var (minimapLineStart, minimapLineEnd) = _samplingState.DecorationLineRangeToMinimapLineRange(range.StartLineNumber, range.EndLineNumber);
                    _minimapSelections.Add(new TextSelection(minimapLineStart, range.StartColumn, minimapLineEnd, range.EndColumn));
                }
            }
            else
            {
                _minimapSelections = [.. _selections];
            }
        }

        return _minimapSelections;
    }

    public List<ViewModelDecoration> GetMinimapDecorationsInViewport(int startLineNumber, int endLineNumber)
    {
        TextRange visibleRange;
        if (_samplingState is not null)
        {
            var modelStartLineNumber = _samplingState.MinimapLines[startLineNumber - 1];
            var modelEndLineNumber = _samplingState.MinimapLines[endLineNumber - 1];
            visibleRange = new TextRange(modelStartLineNumber, 1, modelEndLineNumber, _viewModel.GetLineMaxColumn(modelEndLineNumber));
        }
        else
        {
            visibleRange = new TextRange(startLineNumber, 1, endLineNumber, _viewModel.GetLineMaxColumn(endLineNumber));
        }

        var decorations = _viewModel.GetMinimapDecorationsInRange(visibleRange);
        var result = new List<ViewModelDecoration>(decorations.Count);
        foreach (var decoration in decorations)
        {
            if (decoration.Options.Minimap is not { } minimap || minimap.SectionHeaderStyle is not null)
            {
                // section headers are drawn by _renderSectionHeaders upstream (not ported)
                if (decoration.Options.Minimap is null && _samplingState is null)
                {
                    result.Add(decoration);
                }

                continue;
            }

            if (_samplingState is not null)
            {
                var range = decoration.Range;
                var minimapStartLineNumber = _samplingState.ModelLineToMinimapLine(range.StartLineNumber);
                var minimapEndLineNumber = _samplingState.ModelLineToMinimapLine(range.EndLineNumber);
                result.Add(new ViewModelDecoration(new TextRange(minimapStartLineNumber, range.StartColumn, minimapEndLineNumber, range.EndColumn), decoration.Options));
            }
            else
            {
                result.Add(decoration);
            }
        }

        return result;
    }

    public void RevealLineNumber(int lineNumber)
    {
        if (_samplingState is not null)
        {
            lineNumber = _samplingState.MinimapLines[lineNumber - 1];
        }

        _viewModel.RevealRange("mouse", false, new TextRange(lineNumber, 1, lineNumber, 1), VerticalRevealType.Center, ScrollType.Smooth);
    }

    /// <summary>In <c>fit</c> / <c>fill</c> mode, <paramref name="scrollTop"/> is measured without view zones.</summary>
    public void SetScrollTop(double scrollTop)
    {
        if (Options.MinimapHeightIsEditorHeight)
        {
            var viewLayout = _viewModel.ViewLayout;
            scrollTop = MinimapLayout.GetScrollTopFromZoneFreeScrollTop(MinimapLineOffsets.From(viewLayout), GetRealLineCount(), Options.LineHeight, Options.PaddingTop, viewLayout.GetCurrentViewport().Height, scrollTop, viewLayout.GetScrollHeight());
        }

        SetEditorScrollTop(scrollTop);
    }

    /// <summary>The editor scrollTop, measured with view zones in every mode.</summary>
    public double GetEditorScrollTop() => _viewModel.ViewLayout.GetCurrentScrollTop();

    /// <summary>Sets the editor scrollTop, measured with view zones in every mode.</summary>
    public void SetEditorScrollTop(double scrollTop)
    {
        _viewModel.ViewLayout.SetScrollPosition(new NewScrollPosition(ScrollTop: scrollTop), ScrollType.Immediate);
    }

    //#endregion

    /// <summary>
    /// The line a click at <paramref name="offsetY"/> (in CSS px from the minimap's top) reveals in <c>proportional</c>
    /// size (upstream's pointer down handler), or null without a frame.
    /// </summary>
    public int? GetLineNumberAtY(double offsetY)
    {
        if (_lastRenderData is null || Options.RenderMinimap == RenderMinimap.None)
        {
            return null;
        }

        var minimapLineHeight = Options.MinimapLineHeight;
        var internalOffsetY = Options.CanvasInnerHeight / Options.CanvasOuterHeight * offsetY;
        var lineIndex = (int)Math.Floor(internalOffsetY / minimapLineHeight);

        var lineNumber = lineIndex + _lastRenderData.RenderedLayout.StartLineNumber - _lastRenderData.RenderedLayout.TopPaddingLineCount;
        return Math.Max(1, Math.Min(lineNumber, GetLineCount()));
    }

    // ---- InnerMinimap: decorations

    private bool RenderDecorations(MinimapLayout layout)
    {
        if (!_renderDecorations)
        {
            return false;
        }

        _renderDecorations = false;
        var selections = GetSelections().Select(s => s.Range).ToList();
        selections.Sort(TextRange.CompareRangesUsingStarts);

        var decorations = GetMinimapDecorationsInViewport(layout.StartLineNumber, layout.EndLineNumber);
        // a stable sort, as upstream's Array.prototype.sort
        decorations = [.. decorations.OrderBy(d => d.Options.ZIndex)];

        var canvasInnerWidth = Options.CanvasInnerWidth;
        var canvasInnerHeight = Options.CanvasInnerHeight;
        var minimapLineHeight = Options.MinimapLineHeight;
        var minimapCharWidth = Options.MinimapCharWidth;
        var tabSize = _viewModel.Model.GetOptions().TabSize;
        if (canvasInnerWidth == 0 || canvasInnerHeight == 0)
        {
            _decorations = [];
            return true;
        }

        var frame = new List<MinimapRect>();

        // We first need to render line highlights and then render decorations on top of those.
        // But we need to pick a single color for each line, and use that as a line highlight.
        // This needs to be the color of the decoration with the highest `zIndex`, but priority
        // is given to the selection.

        var highlightedLines = new ContiguousLineMap<bool>(layout.StartLineNumber, layout.EndLineNumber, false);
        RenderSelectionLineHighlights(frame, selections, highlightedLines, layout, minimapLineHeight, canvasInnerWidth);
        RenderDecorationsLineHighlights(frame, decorations, highlightedLines, layout, minimapLineHeight, canvasInnerWidth);

        var lineOffsetMap = new ContiguousLineMap<double[]?>(layout.StartLineNumber, layout.EndLineNumber, null);
        RenderSelectionsHighlights(frame, selections, lineOffsetMap, layout, minimapLineHeight, tabSize, minimapCharWidth, canvasInnerWidth);
        RenderDecorationsHighlights(frame, decorations, lineOffsetMap, layout, minimapLineHeight, tabSize, minimapCharWidth, canvasInnerWidth);

        _decorations = frame;
        return true;
    }

    private static RgbaColor Transparent(RgbaColor color, double factor) =>
        color with { A = (byte)Math.Round(color.A * factor, MidpointRounding.AwayFromZero) };

    private static void FillRect(List<MinimapRect> frame, RgbaColor color, double x, double y, double width, double height)
    {
        if (width == 0 || height == 0)
        {
            return;
        }

        frame.Add(new MinimapRect(color, x, y, width, height));
    }

    private void RenderSelectionLineHighlights(List<MinimapRect> frame, List<TextRange> selections, ContiguousLineMap<bool> highlightedLines, MinimapLayout layout, double minimapLineHeight, int canvasInnerWidth)
    {
        if (Options.SelectionColor is not { A: > 0 } selectionColor)
        {
            return;
        }

        var fillStyle = Transparent(selectionColor, 0.5);

        double y1 = 0;
        double y2 = 0;

        foreach (var selection in selections)
        {
            if (layout.IntersectWithViewport(selection) is not { } intersection)
            {
                // entirely outside minimap's viewport
                continue;
            }

            var (startLineNumber, endLineNumber) = intersection;
            for (var line = startLineNumber; line <= endLineNumber; line++)
            {
                highlightedLines.Set(line, true);
            }

            var yy1 = layout.GetYForLineNumber(startLineNumber, minimapLineHeight);
            var yy2 = layout.GetYForLineNumber(endLineNumber, minimapLineHeight);

            if (y2 >= yy1)
            {
                // merge into previous
                y2 = yy2;
            }
            else
            {
                if (y2 > y1)
                {
                    // flush
                    FillRect(frame, fillStyle, EditorLayoutInfoComputer.MinimapGutterWidth, y1, canvasInnerWidth, y2 - y1);
                }

                y1 = yy1;
                y2 = yy2;
            }
        }

        if (y2 > y1)
        {
            // flush
            FillRect(frame, fillStyle, EditorLayoutInfoComputer.MinimapGutterWidth, y1, canvasInnerWidth, y2 - y1);
        }
    }

    private void RenderDecorationsLineHighlights(List<MinimapRect> frame, List<ViewModelDecoration> decorations, ContiguousLineMap<bool> highlightedLines, MinimapLayout layout, double minimapLineHeight, int canvasInnerWidth)
    {
        // Loop backwards to hit first decorations with higher `zIndex`
        for (var i = decorations.Count - 1; i >= 0; i--)
        {
            var decoration = decorations[i];
            if (decoration.Options.Minimap is not { Position: MinimapPosition.Inline } minimapOptions)
            {
                continue;
            }

            if (layout.IntersectWithViewport(decoration.Range) is not { } intersection)
            {
                // entirely outside minimap's viewport
                continue;
            }

            if (DecorationColorResolver(minimapOptions) is not { A: > 0 } decorationColor)
            {
                continue;
            }

            var highlightColor = Transparent(decorationColor, 0.5);
            var (startLineNumber, endLineNumber) = intersection;
            for (var line = startLineNumber; line <= endLineNumber; line++)
            {
                if (highlightedLines.Has(line))
                {
                    continue;
                }

                highlightedLines.Set(line, true);
                var y = layout.GetYForLineNumber(line, minimapLineHeight);
                FillRect(frame, highlightColor, EditorLayoutInfoComputer.MinimapGutterWidth, y, canvasInnerWidth, minimapLineHeight);
            }
        }
    }

    private void RenderSelectionsHighlights(List<MinimapRect> frame, List<TextRange> selections, ContiguousLineMap<double[]?> lineOffsetMap, MinimapLayout layout, double lineHeight, int tabSize, int characterWidth, int canvasInnerWidth)
    {
        if (Options.SelectionColor is not { A: > 0 } selectionColor)
        {
            return;
        }

        foreach (var selection in selections)
        {
            if (layout.IntersectWithViewport(selection) is not { } intersection)
            {
                // entirely outside minimap's viewport
                continue;
            }

            var (startLineNumber, endLineNumber) = intersection;
            for (var line = startLineNumber; line <= endLineNumber; line++)
            {
                RenderDecorationOnLine(frame, lineOffsetMap, selection, selectionColor, layout, line, lineHeight, lineHeight, tabSize, characterWidth, canvasInnerWidth);
            }
        }
    }

    private void RenderDecorationsHighlights(List<MinimapRect> frame, List<ViewModelDecoration> decorations, ContiguousLineMap<double[]?> lineOffsetMap, MinimapLayout layout, double minimapLineHeight, int tabSize, int characterWidth, int canvasInnerWidth)
    {
        // Loop forwards to hit first decorations with lower `zIndex`
        foreach (var decoration in decorations)
        {
            if (decoration.Options.Minimap is not { } minimapOptions)
            {
                continue;
            }

            if (layout.IntersectWithViewport(decoration.Range) is not { } intersection)
            {
                // entirely outside minimap's viewport
                continue;
            }

            if (DecorationColorResolver(minimapOptions) is not { A: > 0 } decorationColor)
            {
                continue;
            }

            var (startLineNumber, endLineNumber) = intersection;
            for (var line = startLineNumber; line <= endLineNumber; line++)
            {
                switch (minimapOptions.Position)
                {
                    case MinimapPosition.Inline:
                        RenderDecorationOnLine(frame, lineOffsetMap, decoration.Range, decorationColor, layout, line, minimapLineHeight, minimapLineHeight, tabSize, characterWidth, canvasInnerWidth);
                        continue;

                    case MinimapPosition.Gutter:
                    {
                        var y = layout.GetYForLineNumber(line, minimapLineHeight);
                        const int x = 2;
                        FillRect(frame, decorationColor, x, y, GutterDecorationWidth, minimapLineHeight);
                        continue;
                    }
                }
            }
        }
    }

    private void RenderDecorationOnLine(List<MinimapRect> frame, ContiguousLineMap<double[]?> lineOffsetMap, TextRange decorationRange, RgbaColor decorationColor, MinimapLayout layout, int lineNumber, double height, double minimapLineHeight, int tabSize, int charWidth, int canvasInnerWidth)
    {
        var y = layout.GetYForLineNumber(lineNumber, minimapLineHeight);

        // Skip rendering the line if it's vertically outside our viewport
        if (y + height < 0 || y > Options.CanvasInnerHeight)
        {
            return;
        }

        var startColumn = decorationRange.StartLineNumber == lineNumber ? decorationRange.StartColumn : 1;
        var endColumn = decorationRange.EndLineNumber == lineNumber ? decorationRange.EndColumn : GetLineMaxColumn(lineNumber);

        var x1 = GetXOffsetForPosition(lineOffsetMap, lineNumber, startColumn, tabSize, charWidth, canvasInnerWidth);
        var x2 = GetXOffsetForPosition(lineOffsetMap, lineNumber, endColumn, tabSize, charWidth, canvasInnerWidth);

        FillRect(frame, decorationColor, x1, y, x2 - x1, height);
    }

    private double GetXOffsetForPosition(ContiguousLineMap<double[]?> lineOffsetMap, int lineNumber, int column, int tabSize, int charWidth, int canvasInnerWidth)
    {
        if (column == 1)
        {
            return EditorLayoutInfoComputer.MinimapGutterWidth;
        }

        var minimumXOffset = (column - 1) * charWidth;
        if (minimumXOffset >= canvasInnerWidth)
        {
            // there is no need to look at actual characters,
            // as this column is certainly after the minimap width
            return canvasInnerWidth;
        }

        // Cache line offset data so that it is only read once per line
        var lineIndexToXOffset = lineOffsetMap.Get(lineNumber);
        if (lineIndexToXOffset is null)
        {
            var lineData = GetLineContent(lineNumber);
            var offsets = new List<double>(lineData.Length + 1) { EditorLayoutInfoComputer.MinimapGutterWidth };
            double prevx = EditorLayoutInfoComputer.MinimapGutterWidth;
            for (var i = 1; i < lineData.Length + 1; i++)
            {
                var charCode = lineData[i - 1];
                double dx = charCode == '\t'
                    ? tabSize * charWidth
                    : Strings.IsFullWidthCharacter(charCode)
                        ? 2 * charWidth
                        : charWidth;

                var x = prevx + dx;
                if (x >= canvasInnerWidth)
                {
                    // no need to keep on going, as we've hit the canvas width
                    offsets.Add(canvasInnerWidth);
                    break;
                }

                offsets.Add(x);
                prevx = x;
            }

            lineIndexToXOffset = [.. offsets];
            lineOffsetMap.Set(lineNumber, lineIndexToXOffset);
        }

        if (column - 1 < lineIndexToXOffset.Length)
        {
            return lineIndexToXOffset[column - 1];
        }

        // goes over the canvas width
        return canvasInnerWidth;
    }

    // ---- InnerMinimap: lines

    private MinimapImageData? GetBuffer()
    {
        if (_buffers is null && Options.CanvasInnerWidth > 0 && Options.CanvasInnerHeight > 0)
        {
            _buffers = new MinimapBuffers(Options.CanvasInnerWidth, Options.CanvasInnerHeight, Options.BackgroundColor);
        }

        return _buffers?.GetBuffer();
    }

    private (MinimapRenderData? Data, int DirtyY1, int DirtyY2) RenderLines(MinimapLayout layout)
    {
        var startLineNumber = layout.StartLineNumber;
        var endLineNumber = layout.EndLineNumber;
        var minimapLineHeight = Options.MinimapLineHeight;

        // Check if nothing changed w.r.t. lines from last frame
        if (_lastRenderData is not null && _lastRenderData.LinesEquals(layout))
        {
            var lastData = _lastRenderData.Get();
            // Nice!! Nothing changed from last frame
            return (new MinimapRenderData(layout, lastData.ImageData, lastData.Lines), 0, 0);
        }

        // Oh well!! We need to repaint some lines...
        var imageData = GetBuffer();
        if (imageData is null)
        {
            // 0 width or 0 height canvas, nothing to do
            return (null, 0, 0);
        }

        // Render untouched lines by using last rendered data.
        var (renderedDirtyY1, renderedDirtyY2, needed) = RenderUntouchedLines(imageData, layout.TopPaddingLineCount, startLineNumber, endLineNumber, minimapLineHeight, _lastRenderData);

        // Fetch rendering info from view model for rest of lines that need rendering.
        var lineInfo = GetMinimapLinesRenderingData(startLineNumber, endLineNumber, needed);
        var tabSize = _viewModel.Model.GetOptions().TabSize;
        var defaultBackground = Options.DefaultBackgroundColor;
        var background = Options.BackgroundColor;
        var foregroundAlpha = Options.ForegroundAlpha;
        var tokensColorTracker = Options.TokensColorTracker;
        var useLighterFont = tokensColorTracker.BackgroundIsLight;
        var renderMinimap = Options.RenderMinimap;
        var charRenderer = Options.CharRenderer();
        var fontScale = Options.FontScale;
        var minimapCharWidth = Options.MinimapCharWidth;

        var baseCharHeight = renderMinimap == RenderMinimap.Text ? MinimapCharSheet.BaseCharHeight : MinimapCharSheet.BaseCharHeight + 1;
        var renderMinimapLineHeight = baseCharHeight * fontScale;
        var innerLinePadding = minimapLineHeight > renderMinimapLineHeight ? (int)Math.Floor((minimapLineHeight - renderMinimapLineHeight) / 2) : 0;

        // Render the rest of lines
        var backgroundA = background.A / 255.0;
        var renderBackground = new RgbaColor(
            Round((background.R - defaultBackground.R) * backgroundA + defaultBackground.R),
            Round((background.G - defaultBackground.G) * backgroundA + defaultBackground.G),
            Round((background.B - defaultBackground.B) * backgroundA + defaultBackground.B),
            255);
        var dy = (int)(layout.TopPaddingLineCount * minimapLineHeight);
        var renderedLines = new List<MinimapLine>(endLineNumber - startLineNumber + 1);
        for (int lineIndex = 0, lineCount = endLineNumber - startLineNumber + 1; lineIndex < lineCount; lineIndex++)
        {
            if (needed[lineIndex])
            {
                RenderLine(imageData, renderBackground, background.A, useLighterFont, renderMinimap, minimapCharWidth, tokensColorTracker, foregroundAlpha, charRenderer, dy, innerLinePadding, tabSize, lineInfo[lineIndex]!, fontScale, minimapLineHeight);
            }

            renderedLines.Add(new MinimapLine(dy));
            dy += (int)minimapLineHeight;
        }

        var dirtyY1 = renderedDirtyY1 == -1 ? 0 : renderedDirtyY1;
        var dirtyY2 = renderedDirtyY2 == -1 ? imageData.Height : renderedDirtyY2;

        // Save rendered data for reuse on next frame if possible
        return (new MinimapRenderData(layout, imageData, renderedLines), dirtyY1, dirtyY2);

        static byte Round(double v) => (byte)Math.Clamp(Math.Round(v, MidpointRounding.AwayFromZero), 0, 255);
    }

    private static (int DirtyY1, int DirtyY2, bool[] Needed) RenderUntouchedLines(MinimapImageData target, int topPaddingLineCount, int startLineNumber, int endLineNumber, double minimapLineHeight, MinimapRenderData? lastRenderData)
    {
        var needed = new bool[Math.Max(0, endLineNumber - startLineNumber + 1)];
        if (lastRenderData is null)
        {
            Array.Fill(needed, true);
            return (-1, -1, needed);
        }

        var (lastImageData, lastStartLineNumber, lastLines) = lastRenderData.Get();
        var lastTargetData = lastImageData.Data;
        var lastLinesLength = lastLines.Count;
        var width = target.Width;
        var targetData = target.Data;
        var lineHeight = (int)minimapLineHeight;

        var maxDestPixel = (endLineNumber - startLineNumber + 1) * lineHeight * width * 4;
        var dirtyPixel1 = -1; // the pixel offset up to which all the data is equal to the prev frame
        var dirtyPixel2 = -1; // the pixel offset after which all the data is equal to the prev frame

        var copySourceStart = -1;
        var copySourceEnd = -1;
        var copyDestStart = -1;
        var copyDestEnd = -1;

        var destDy = topPaddingLineCount * lineHeight;
        for (var lineNumber = startLineNumber; lineNumber <= endLineNumber; lineNumber++)
        {
            var lineIndex = lineNumber - startLineNumber;
            var lastLineIndex = lineNumber - lastStartLineNumber;
            var sourceDy = lastLineIndex >= 0 && lastLineIndex < lastLinesLength ? lastLines[lastLineIndex].Dy : -1;

            if (sourceDy == -1)
            {
                needed[lineIndex] = true;
                destDy += lineHeight;
                continue;
            }

            var sourceStart = sourceDy * width * 4;
            var sourceEnd = (sourceDy + lineHeight) * width * 4;
            var destStart = destDy * width * 4;
            var destEnd = (destDy + lineHeight) * width * 4;

            if (copySourceEnd == sourceStart && copyDestEnd == destStart)
            {
                // contiguous zone => extend copy request
                copySourceEnd = sourceEnd;
                copyDestEnd = destEnd;
            }
            else
            {
                if (copySourceStart != -1)
                {
                    // flush existing copy request
                    Flush();
                }

                copySourceStart = sourceStart;
                copySourceEnd = sourceEnd;
                copyDestStart = destStart;
                copyDestEnd = destEnd;
            }

            needed[lineIndex] = false;
            destDy += lineHeight;
        }

        if (copySourceStart != -1)
        {
            // flush existing copy request
            Flush();
        }

        var dirtyY1 = dirtyPixel1 == -1 ? -1 : dirtyPixel1 / (width * 4);
        var dirtyY2 = dirtyPixel2 == -1 ? -1 : dirtyPixel2 / (width * 4);

        return (dirtyY1, dirtyY2, needed);

        void Flush()
        {
            var length = Math.Min(copySourceEnd, lastTargetData.Length) - copySourceStart;
            length = Math.Min(length, targetData.Length - copyDestStart);
            if (length > 0)
            {
                Array.Copy(lastTargetData, copySourceStart, targetData, copyDestStart, length);
            }

            if (dirtyPixel1 == -1 && copySourceStart == 0 && copySourceStart == copyDestStart)
            {
                dirtyPixel1 = copySourceEnd;
            }

            if (dirtyPixel2 == -1 && copySourceEnd == maxDestPixel && copySourceStart == copyDestStart)
            {
                dirtyPixel2 = copySourceStart;
            }
        }
    }

    private static void RenderLine(
        MinimapImageData target,
        RgbaColor backgroundColor,
        int backgroundAlpha,
        bool useLighterFont,
        RenderMinimap renderMinimap,
        int charWidth,
        MinimapTokensColorTracker colorTracker,
        int foregroundAlpha,
        MinimapCharRenderer minimapCharRenderer,
        int dy,
        int innerLinePadding,
        int tabSize,
        ViewLineData lineData,
        int fontScale,
        double minimapLineHeight)
    {
        var content = lineData.Content;
        var tokens = lineData.Tokens;
        var maxDx = target.Width - charWidth;
        var force1pxHeight = minimapLineHeight == 1;

        var dx = EditorLayoutInfoComputer.MinimapGutterWidth;
        var charIndex = 0;
        var tabsCharDelta = 0;

        for (int tokenIndex = 0, tokensLen = tokens.GetCount(); tokenIndex < tokensLen; tokenIndex++)
        {
            var tokenEndIndex = Math.Min(tokens.GetEndOffset(tokenIndex), content.Length);
            var tokenColorId = tokens.GetForeground(tokenIndex);
            var tokenColor = colorTracker.GetColor(tokenColorId);

            for (; charIndex < tokenEndIndex; charIndex++)
            {
                if (dx > maxDx)
                {
                    // hit edge of minimap
                    return;
                }

                int charCode = content[charIndex];

                if (charCode == '\t')
                {
                    var insertSpacesCount = tabSize - ((charIndex + tabsCharDelta) % tabSize);
                    tabsCharDelta += insertSpacesCount - 1;
                    // No need to render anything since tab is invisible
                    dx += insertSpacesCount * charWidth;
                }
                else if (charCode == ' ')
                {
                    // No need to render anything since space is invisible
                    dx += charWidth;
                }
                else
                {
                    // Render twice for a full width character
                    var count = Strings.IsFullWidthCharacter(charCode) ? 2 : 1;

                    for (var i = 0; i < count; i++)
                    {
                        if (renderMinimap == RenderMinimap.Blocks)
                        {
                            minimapCharRenderer.BlockRenderChar(target, dx, dy + innerLinePadding, tokenColor, foregroundAlpha, backgroundColor, backgroundAlpha, force1pxHeight);
                        }
                        else
                        {
                            // RenderMinimap.Text
                            minimapCharRenderer.RenderChar(target, dx, dy + innerLinePadding, charCode, tokenColor, foregroundAlpha, backgroundColor, backgroundAlpha, fontScale, useLighterFont, force1pxHeight);
                        }

                        dx += charWidth;

                        if (dx > maxDx)
                        {
                            // hit edge of minimap
                            return;
                        }
                    }
                }
            }
        }
    }
}
