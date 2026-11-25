// Ported from vscode (commit b57d5c9e, MIT license):
//  - src/vs/editor/browser/viewParts/minimap/minimap.ts: MinimapOptions, MinimapLine, RenderData, MinimapBuffers,
//    MinimapSamplingState and ContiguousLineMap;
//  - src/vs/editor/browser/view/viewLayer.ts: RenderedLinesCollection (the part RenderData uses);
//  - src/vs/editor/common/viewModel/minimapTokensColorTracker.ts.
//
// Differences:
//  - MinimapOptions is built from the computed layout (EditorLayoutInfo), the minimap options and the colors the host
//    resolved (MinimapColors), instead of reading the configuration and the theme; the section header options are not
//    here (section headers are not drawn) and the character renderer comes from MinimapCharRendererFactory by scale;
//  - MinimapTokensColorTracker is an instance per editor built from the color map (upstream: a process-wide singleton
//    listening to the TokenizationRegistry);
//  - sampling events are a small record (upstream: a union of object literals).

using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Tokens;
using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc.Editor.ViewParts;

/// <summary>The token colors the minimap draws with (upstream's MinimapTokensColorTracker).</summary>
internal sealed class MinimapTokensColorTracker
{
    private readonly RgbaColor[] _colors;

    /// <param name="colorMap">The color map (index = color id; 1 the default foreground, 2 the default background); null or empty is no theme.</param>
    public MinimapTokensColorTracker(IReadOnlyList<RgbaColor>? colorMap)
    {
        if (colorMap is null || colorMap.Count <= (int)ColorId.DefaultBackground)
        {
            _colors = new RgbaColor[(int)ColorId.DefaultBackground + 1];
            BackgroundIsLight = true;
            return;
        }

        _colors = new RgbaColor[colorMap.Count];
        _colors[0] = RgbaColor.Transparent;
        for (var colorId = 1; colorId < colorMap.Count; colorId++)
        {
            _colors[colorId] = colorMap[colorId];
        }

        BackgroundIsLight = GetRelativeLuminance(colorMap[(int)ColorId.DefaultBackground]) >= 0.5;
    }

    public bool BackgroundIsLight { get; }

    public RgbaColor GetColor(ColorId colorId)
    {
        var id = (int)colorId;
        if (id < 1 || id >= _colors.Length)
        {
            // background color (basically invisible)
            id = (int)ColorId.DefaultBackground;
        }

        return _colors[id];
    }

    /// <summary>Whether two trackers draw the same.</summary>
    public bool ColorsEqual(MinimapTokensColorTracker other) =>
        BackgroundIsLight == other.BackgroundIsLight && _colors.AsSpan().SequenceEqual(other._colors);

    // upstream's Color.getRelativeLuminance (https://www.w3.org/TR/WCAG20/#relativeluminancedef)
    private static double GetRelativeLuminance(RgbaColor color)
    {
        var r = Component(color.R);
        var g = Component(color.G);
        var b = Component(color.B);
        var luminance = (0.2126 * r) + (0.7152 * g) + (0.0722 * b);
        return Math.Round(luminance, 4, MidpointRounding.AwayFromZero);

        static double Component(byte value)
        {
            var c = value / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
    }
}

/// <summary>The colors of the minimap besides the token colors (upstream reads them from the theme).</summary>
internal sealed record MinimapColors
{
    /// <summary><c>minimap.background</c>; null uses the default token background.</summary>
    public RgbaColor? Background { get; init; }

    /// <summary><c>minimap.foregroundOpacity</c> as 0..255.</summary>
    public int ForegroundAlpha { get; init; } = 255;

    /// <summary><c>minimap.selectionHighlight</c>; null or transparent draws no selections.</summary>
    public RgbaColor? Selection { get; init; }
}

/// <summary>What the minimap draws with, from the layout and the theme (upstream's MinimapOptions).</summary>
internal sealed class MinimapOptions : IMinimapLayoutOptions
{
    public MinimapOptions(
        EditorLayoutInfo layoutInfo,
        EditorMinimapOptions minimapOpts,
        double pixelRatio,
        double lineHeight,
        double typicalHalfwidthCharacterWidth,
        double paddingTop,
        double paddingBottom,
        bool scrollBeyondLastLine,
        MinimapTokensColorTracker tokensColorTracker,
        MinimapColors colors)
    {
        var minimapLayout = layoutInfo.Minimap;
        RenderMinimap = minimapLayout.RenderMinimap;
        Size = minimapOpts.Size;
        MinimapHeightIsEditorHeight = minimapLayout.MinimapHeightIsEditorHeight;
        ScrollBeyondLastLine = scrollBeyondLastLine;
        PaddingTop = paddingTop;
        PaddingBottom = paddingBottom;
        ShowSlider = minimapOpts.ShowSlider;
        Autohide = minimapOpts.Autohide;
        PixelRatio = pixelRatio;
        TypicalHalfwidthCharacterWidth = typicalHalfwidthCharacterWidth;
        LineHeight = lineHeight;
        MinimapLeft = minimapLayout.MinimapLeft;
        MinimapWidth = minimapLayout.MinimapWidth;
        MinimapHeight = layoutInfo.Height;

        CanvasInnerWidth = minimapLayout.MinimapCanvasInnerWidth;
        CanvasInnerHeight = minimapLayout.MinimapCanvasInnerHeight;
        CanvasOuterWidth = minimapLayout.MinimapCanvasOuterWidth;
        CanvasOuterHeight = minimapLayout.MinimapCanvasOuterHeight;

        IsSampling = minimapLayout.MinimapIsSampling;
        EditorHeight = layoutInfo.Height;
        FontScale = minimapLayout.MinimapScale;
        MinimapLineHeight = minimapLayout.MinimapLineHeight;
        MinimapCharWidth = MinimapCharSheet.BaseCharWidth * FontScale;

        TokensColorTracker = tokensColorTracker;
        DefaultBackgroundColor = tokensColorTracker.GetColor(ColorId.DefaultBackground);
        BackgroundColor = colors.Background ?? DefaultBackgroundColor;
        ForegroundAlpha = Math.Clamp(colors.ForegroundAlpha, 0, 255);
        SelectionColor = colors.Selection;
    }

    public RenderMinimap RenderMinimap { get; }

    public EditorMinimapSize Size { get; }

    public bool MinimapHeightIsEditorHeight { get; }

    public bool ScrollBeyondLastLine { get; }

    public double PaddingTop { get; }

    public double PaddingBottom { get; }

    public EditorMinimapShowSlider ShowSlider { get; }

    public EditorMinimapAutohide Autohide { get; }

    public double PixelRatio { get; }

    public double TypicalHalfwidthCharacterWidth { get; }

    public double LineHeight { get; }

    /// <summary>Container left position (in CSS px).</summary>
    public double MinimapLeft { get; }

    /// <summary>Container width (in CSS px).</summary>
    public double MinimapWidth { get; }

    /// <summary>Container height (in CSS px).</summary>
    public double MinimapHeight { get; }

    /// <summary>Canvas backing store width (in device px).</summary>
    public int CanvasInnerWidth { get; }

    /// <summary>Canvas backing store height (in device px).</summary>
    public int CanvasInnerHeight { get; }

    /// <summary>Canvas width (in CSS px).</summary>
    public double CanvasOuterWidth { get; }

    /// <summary>Canvas height (in CSS px).</summary>
    public double CanvasOuterHeight { get; }

    public bool IsSampling { get; }

    public double EditorHeight { get; }

    public int FontScale { get; }

    public double MinimapLineHeight { get; }

    public int MinimapCharWidth { get; }

    public MinimapTokensColorTracker TokensColorTracker { get; }

    public RgbaColor DefaultBackgroundColor { get; }

    public RgbaColor BackgroundColor { get; }

    /// <summary>Foreground alpha: integer in [0-255].</summary>
    public int ForegroundAlpha { get; }

    public RgbaColor? SelectionColor { get; }

    public MinimapCharRenderer CharRenderer() => MinimapCharRendererFactory.Create(FontScale);

    public bool Equals(MinimapOptions other) =>
        RenderMinimap == other.RenderMinimap
        && Size == other.Size
        && MinimapHeightIsEditorHeight == other.MinimapHeightIsEditorHeight
        && ScrollBeyondLastLine == other.ScrollBeyondLastLine
        && PaddingTop == other.PaddingTop
        && PaddingBottom == other.PaddingBottom
        && ShowSlider == other.ShowSlider
        && Autohide == other.Autohide
        && PixelRatio == other.PixelRatio
        && TypicalHalfwidthCharacterWidth == other.TypicalHalfwidthCharacterWidth
        && LineHeight == other.LineHeight
        && MinimapLeft == other.MinimapLeft
        && MinimapWidth == other.MinimapWidth
        && MinimapHeight == other.MinimapHeight
        && CanvasInnerWidth == other.CanvasInnerWidth
        && CanvasInnerHeight == other.CanvasInnerHeight
        && CanvasOuterWidth == other.CanvasOuterWidth
        && CanvasOuterHeight == other.CanvasOuterHeight
        && IsSampling == other.IsSampling
        && EditorHeight == other.EditorHeight
        && FontScale == other.FontScale
        && MinimapLineHeight == other.MinimapLineHeight
        && MinimapCharWidth == other.MinimapCharWidth
        && DefaultBackgroundColor == other.DefaultBackgroundColor
        && BackgroundColor == other.BackgroundColor
        && ForegroundAlpha == other.ForegroundAlpha
        && SelectionColor == other.SelectionColor
        && TokensColorTracker.ColorsEqual(other.TokensColorTracker);
}

/// <summary>Where a rendered minimap line is in the last image (-1: it has to be drawn again).</summary>
internal sealed class MinimapLine
{
    public MinimapLine(int dy)
    {
        Dy = dy;
    }

    public static MinimapLine Invalid => new(-1);

    public int Dy { get; set; }

    public void OnContentChanged()
    {
        Dy = -1;
    }

    public void OnTokensChanged()
    {
        Dy = -1;
    }
}

/// <summary>The rendered lines of a frame, kept in step with line insertions and deletions (upstream's RenderedLinesCollection).</summary>
internal sealed class RenderedLinesCollection
{
    private List<MinimapLine> _lines = [];
    private int _rendLineNumberStart = 1;

    public void Set(int rendLineNumberStart, List<MinimapLine> lines)
    {
        _lines = lines;
        _rendLineNumberStart = rendLineNumberStart;
    }

    public (int RendLineNumberStart, List<MinimapLine> Lines) Get() => (_rendLineNumberStart, _lines);

    /// <summary>Inclusive line number that is inside this collection.</summary>
    public int GetStartLineNumber() => _rendLineNumberStart;

    /// <summary>Inclusive line number that is inside this collection.</summary>
    public int GetEndLineNumber() => _rendLineNumberStart + _lines.Count - 1;

    public int GetCount() => _lines.Count;

    public void OnLinesDeleted(int deleteFromLineNumber, int deleteToLineNumber)
    {
        if (GetCount() == 0)
        {
            // no lines
            return;
        }

        var startLineNumber = GetStartLineNumber();
        var endLineNumber = GetEndLineNumber();

        if (deleteToLineNumber < startLineNumber)
        {
            // deleting above the viewport
            var deleteCnt = deleteToLineNumber - deleteFromLineNumber + 1;
            _rendLineNumberStart -= deleteCnt;
            return;
        }

        if (deleteFromLineNumber > endLineNumber)
        {
            // deleted below the viewport
            return;
        }

        // Record what needs to be deleted
        var deleteStartIndex = 0;
        var deleteCount = 0;
        for (var lineNumber = startLineNumber; lineNumber <= endLineNumber; lineNumber++)
        {
            var lineIndex = lineNumber - _rendLineNumberStart;
            if (deleteFromLineNumber <= lineNumber && lineNumber <= deleteToLineNumber)
            {
                // this is a line to be deleted
                if (deleteCount == 0)
                {
                    // this is the first line to be deleted
                    deleteStartIndex = lineIndex;
                    deleteCount = 1;
                }
                else
                {
                    deleteCount++;
                }
            }
        }

        // Adjust _rendLineNumberStart for lines deleted above
        if (deleteFromLineNumber < startLineNumber)
        {
            // Something was deleted above
            int deleteAboveCount;
            if (deleteToLineNumber < startLineNumber)
            {
                // the entire deleted lines are above
                deleteAboveCount = deleteToLineNumber - deleteFromLineNumber + 1;
            }
            else
            {
                deleteAboveCount = startLineNumber - deleteFromLineNumber;
            }

            _rendLineNumberStart -= deleteAboveCount;
        }

        _lines.RemoveRange(deleteStartIndex, deleteCount);
    }

    public bool OnLinesChanged(int changeFromLineNumber, int changeCount)
    {
        var changeToLineNumber = changeFromLineNumber + changeCount - 1;
        if (GetCount() == 0)
        {
            // no lines
            return false;
        }

        var startLineNumber = GetStartLineNumber();
        var endLineNumber = GetEndLineNumber();

        var someoneNotified = false;
        for (var changedLineNumber = changeFromLineNumber; changedLineNumber <= changeToLineNumber; changedLineNumber++)
        {
            if (changedLineNumber >= startLineNumber && changedLineNumber <= endLineNumber)
            {
                // Notify the line
                _lines[changedLineNumber - _rendLineNumberStart].OnContentChanged();
                someoneNotified = true;
            }
        }

        return someoneNotified;
    }

    public void OnLinesInserted(int insertFromLineNumber, int insertToLineNumber)
    {
        if (GetCount() == 0)
        {
            // no lines
            return;
        }

        var insertCnt = insertToLineNumber - insertFromLineNumber + 1;
        var startLineNumber = GetStartLineNumber();
        var endLineNumber = GetEndLineNumber();

        if (insertFromLineNumber <= startLineNumber)
        {
            // inserting above the viewport
            _rendLineNumberStart += insertCnt;
            return;
        }

        if (insertFromLineNumber > endLineNumber)
        {
            // inserting below the viewport
            return;
        }

        if (insertCnt + insertFromLineNumber > endLineNumber)
        {
            // insert inside the viewport in such a way that all remaining lines are pushed outside
            _lines.RemoveRange(insertFromLineNumber - _rendLineNumberStart, endLineNumber - insertFromLineNumber + 1);
            return;
        }

        // insert inside the viewport, push out some lines, but not all remaining lines
        var newLines = new List<MinimapLine>(insertCnt);
        for (var i = 0; i < insertCnt; i++)
        {
            newLines.Add(MinimapLine.Invalid);
        }

        var insertIndex = insertFromLineNumber - _rendLineNumberStart;
        var beforeLines = _lines.GetRange(0, insertIndex);
        var afterLines = _lines.GetRange(insertIndex, _lines.Count - insertCnt - insertIndex);
        _lines = [.. beforeLines, .. newLines, .. afterLines];
    }

    public bool OnTokensChanged(IReadOnlyList<ViewTokensChangedRange> ranges)
    {
        if (GetCount() == 0)
        {
            // no lines
            return false;
        }

        var startLineNumber = GetStartLineNumber();
        var endLineNumber = GetEndLineNumber();

        var notifiedSomeone = false;
        foreach (var rng in ranges)
        {
            if (rng.ToLineNumber < startLineNumber || rng.FromLineNumber > endLineNumber)
            {
                // range outside viewport
                continue;
            }

            var from = Math.Max(startLineNumber, rng.FromLineNumber);
            var to = Math.Min(endLineNumber, rng.ToLineNumber);

            for (var lineNumber = from; lineNumber <= to; lineNumber++)
            {
                var lineIndex = lineNumber - _rendLineNumberStart;
                _lines[lineIndex].OnTokensChanged();
                notifiedSomeone = true;
            }
        }

        return notifiedSomeone;
    }
}

/// <summary>The image of the last frame and where each of its lines is (upstream's RenderData).</summary>
internal sealed class MinimapRenderData
{
    private readonly MinimapImageData _imageData;
    private readonly RenderedLinesCollection _renderedLines = new();

    public MinimapRenderData(MinimapLayout renderedLayout, MinimapImageData imageData, List<MinimapLine> lines)
    {
        RenderedLayout = renderedLayout;
        _imageData = imageData;
        _renderedLines.Set(renderedLayout.StartLineNumber, lines);
    }

    /// <summary>Last rendered layout.</summary>
    public MinimapLayout RenderedLayout { get; }

    /// <summary>Check if the current render data matches accurately the new desired layout and no painting is needed.</summary>
    public bool LinesEquals(MinimapLayout layout)
    {
        if (!ScrollEquals(layout))
        {
            return false;
        }

        foreach (var line in _renderedLines.Get().Lines)
        {
            if (line.Dy == -1)
            {
                // This line is invalid
                return false;
            }
        }

        return true;
    }

    /// <summary>Check if the current render data matches the new layout's scroll position.</summary>
    public bool ScrollEquals(MinimapLayout layout) =>
        RenderedLayout.StartLineNumber == layout.StartLineNumber && RenderedLayout.EndLineNumber == layout.EndLineNumber;

    public (MinimapImageData ImageData, int RendLineNumberStart, List<MinimapLine> Lines) Get()
    {
        var (start, lines) = _renderedLines.Get();
        return (_imageData, start, lines);
    }

    public bool OnLinesChanged(int changeFromLineNumber, int changeCount) => _renderedLines.OnLinesChanged(changeFromLineNumber, changeCount);

    public void OnLinesDeleted(int deleteFromLineNumber, int deleteToLineNumber) => _renderedLines.OnLinesDeleted(deleteFromLineNumber, deleteToLineNumber);

    public void OnLinesInserted(int insertFromLineNumber, int insertToLineNumber) => _renderedLines.OnLinesInserted(insertFromLineNumber, insertToLineNumber);

    public bool OnTokensChanged(IReadOnlyList<ViewTokensChangedRange> ranges) => _renderedLines.OnTokensChanged(ranges);
}

/// <summary>
/// Some sort of double buffering. Keeps two buffers around that will be rotated for painting. Always gives a buffer
/// that is filled with the background color.
/// </summary>
internal sealed class MinimapBuffers
{
    private readonly byte[] _backgroundFillData;
    private readonly MinimapImageData[] _buffers;
    private int _lastUsedBuffer;

    public MinimapBuffers(int width, int height, RgbaColor background)
    {
        _backgroundFillData = CreateBackgroundFillData(width, height, background);
        _buffers = [new MinimapImageData(width, height), new MinimapImageData(width, height)];
    }

    public MinimapImageData GetBuffer()
    {
        // rotate buffers
        _lastUsedBuffer = 1 - _lastUsedBuffer;
        var result = _buffers[_lastUsedBuffer];

        // fill with background color
        _backgroundFillData.CopyTo(result.Data, 0);
        return result;
    }

    private static byte[] CreateBackgroundFillData(int width, int height, RgbaColor background)
    {
        var result = new byte[width * height * 4];
        var offset = 0;
        for (var i = 0; i < height; i++)
        {
            for (var j = 0; j < width; j++)
            {
                result[offset] = background.R;
                result[offset + 1] = background.G;
                result[offset + 2] = background.B;
                result[offset + 3] = background.A;
                offset += 4;
            }
        }

        return result;
    }
}

/// <summary>A change of the sampled lines (upstream's SamplingStateEvent).</summary>
internal sealed record MinimapSamplingEvent(MinimapSamplingEventType Type, int FromLineNumber, int ToLineNumber)
{
    internal int OldIndex { get; init; }

    internal int I { get; init; }
}

internal enum MinimapSamplingEventType
{
    Deleted,
    Inserted,
    Flush,
}

/// <summary>Which view lines a sampling minimap shows (a fit / fill minimap with more lines than pixel rows).</summary>
internal sealed class MinimapSamplingState
{
    public MinimapSamplingState(double samplingRatio, int[] minimapLines)
    {
        SamplingRatio = samplingRatio;
        MinimapLines = minimapLines;
    }

    public double SamplingRatio { get; }

    /// <summary>A map of 0-based minimap line indexes to 1-based view line numbers.</summary>
    public int[] MinimapLines { get; }

    public static (MinimapSamplingState? State, List<MinimapSamplingEvent> Events) Compute(MinimapOptions options, int viewLineCount, MinimapSamplingState? oldSamplingState)
    {
        if (options.RenderMinimap == RenderMinimap.None || !options.IsSampling)
        {
            return (null, []);
        }

        // ratio is intentionally not part of the layout to avoid the layout changing all the time
        // so we need to recompute it again...
        var minimapLineCount = (int)EditorLayoutInfoComputer.ComputeContainedMinimapLineCount(
            viewLineCount,
            options.ScrollBeyondLastLine,
            options.PaddingTop,
            options.PaddingBottom,
            options.EditorHeight,
            options.LineHeight,
            options.PixelRatio).MinimapLineCount;
        var ratio = (double)viewLineCount / minimapLineCount;
        var halfRatio = ratio / 2;

        if (oldSamplingState is null || oldSamplingState.MinimapLines.Length == 0)
        {
            var initial = new int[Math.Max(1, minimapLineCount)];
            initial[0] = 1;
            if (minimapLineCount > 1)
            {
                for (int i = 0, lastIndex = minimapLineCount - 1; i < lastIndex; i++)
                {
                    initial[i] = (int)Math.Round((i * ratio) + halfRatio, MidpointRounding.AwayFromZero);
                }

                initial[minimapLineCount - 1] = viewLineCount;
            }

            return (new MinimapSamplingState(ratio, initial), []);
        }

        var oldMinimapLines = oldSamplingState.MinimapLines;
        var oldLength = oldMinimapLines.Length;
        var result = new int[minimapLineCount];
        var oldIndex = 0;
        var oldDeltaLineCount = 0;
        var minViewLineNumber = 1;
        const int maxEventCount = 10; // generate at most 10 events, if there are more than 10 changes, just flush all previous data
        var events = new List<MinimapSamplingEvent>();
        MinimapSamplingEvent? lastEvent = null;
        for (var i = 0; i < minimapLineCount; i++)
        {
            var fromViewLineNumber = Math.Max(minViewLineNumber, (int)Math.Round(i * ratio, MidpointRounding.AwayFromZero));
            var toViewLineNumber = Math.Max(fromViewLineNumber, (int)Math.Round((i + 1) * ratio, MidpointRounding.AwayFromZero));

            while (oldIndex < oldLength && oldMinimapLines[oldIndex] < fromViewLineNumber)
            {
                if (events.Count < maxEventCount)
                {
                    var oldMinimapLineNumber = oldIndex + 1 + oldDeltaLineCount;
                    if (lastEvent is { Type: MinimapSamplingEventType.Deleted } deleted && deleted.OldIndex == oldIndex - 1)
                    {
                        lastEvent = deleted with { ToLineNumber = deleted.ToLineNumber + 1 };
                        events[^1] = lastEvent;
                    }
                    else
                    {
                        lastEvent = new MinimapSamplingEvent(MinimapSamplingEventType.Deleted, oldMinimapLineNumber, oldMinimapLineNumber) { OldIndex = oldIndex };
                        events.Add(lastEvent);
                    }

                    oldDeltaLineCount--;
                }

                oldIndex++;
            }

            int selectedViewLineNumber;
            if (oldIndex < oldLength && oldMinimapLines[oldIndex] <= toViewLineNumber)
            {
                // reuse the old sampled line
                selectedViewLineNumber = oldMinimapLines[oldIndex];
                oldIndex++;
            }
            else
            {
                if (i == 0)
                {
                    selectedViewLineNumber = 1;
                }
                else if (i + 1 == minimapLineCount)
                {
                    selectedViewLineNumber = viewLineCount;
                }
                else
                {
                    selectedViewLineNumber = (int)Math.Round((i * ratio) + halfRatio, MidpointRounding.AwayFromZero);
                }

                if (events.Count < maxEventCount)
                {
                    var oldMinimapLineNumber = oldIndex + 1 + oldDeltaLineCount;
                    if (lastEvent is { Type: MinimapSamplingEventType.Inserted } inserted && inserted.I == i - 1)
                    {
                        lastEvent = inserted with { ToLineNumber = inserted.ToLineNumber + 1 };
                        events[^1] = lastEvent;
                    }
                    else
                    {
                        lastEvent = new MinimapSamplingEvent(MinimapSamplingEventType.Inserted, oldMinimapLineNumber, oldMinimapLineNumber) { I = i };
                        events.Add(lastEvent);
                    }

                    oldDeltaLineCount++;
                }
            }

            result[i] = selectedViewLineNumber;
            minViewLineNumber = selectedViewLineNumber;
        }

        if (events.Count < maxEventCount)
        {
            while (oldIndex < oldLength)
            {
                var oldMinimapLineNumber = oldIndex + 1 + oldDeltaLineCount;
                if (lastEvent is { Type: MinimapSamplingEventType.Deleted } deleted && deleted.OldIndex == oldIndex - 1)
                {
                    lastEvent = deleted with { ToLineNumber = deleted.ToLineNumber + 1 };
                    events[^1] = lastEvent;
                }
                else
                {
                    lastEvent = new MinimapSamplingEvent(MinimapSamplingEventType.Deleted, oldMinimapLineNumber, oldMinimapLineNumber) { OldIndex = oldIndex };
                    events.Add(lastEvent);
                }

                oldDeltaLineCount--;
                oldIndex++;
            }
        }
        else
        {
            // too many events, just give up
            events = [new MinimapSamplingEvent(MinimapSamplingEventType.Flush, 0, 0)];
        }

        return (new MinimapSamplingState(ratio, result), events);
    }

    public int ModelLineToMinimapLine(int lineNumber) =>
        Math.Min(MinimapLines.Length, Math.Max(1, (int)Math.Round(lineNumber / SamplingRatio, MidpointRounding.AwayFromZero)));

    /// <summary>Null if the model line ranges are not intersecting with a sampled model line.</summary>
    public (int From, int To)? ModelLineRangeToMinimapLineRange(int fromLineNumber, int toLineNumber)
    {
        var fromLineIndex = ModelLineToMinimapLine(fromLineNumber) - 1;
        while (fromLineIndex > 0 && MinimapLines[fromLineIndex - 1] >= fromLineNumber)
        {
            fromLineIndex--;
        }

        var toLineIndex = ModelLineToMinimapLine(toLineNumber) - 1;
        while (toLineIndex + 1 < MinimapLines.Length && MinimapLines[toLineIndex + 1] <= toLineNumber)
        {
            toLineIndex++;
        }

        if (fromLineIndex == toLineIndex)
        {
            var sampledLineNumber = MinimapLines[fromLineIndex];
            if (sampledLineNumber < fromLineNumber || sampledLineNumber > toLineNumber)
            {
                // This line is not part of the sampled lines ==> nothing to do
                return null;
            }
        }

        return (fromLineIndex + 1, toLineIndex + 1);
    }

    /// <summary>Always returns a range, even if it is not intersecting with a sampled model line.</summary>
    public (int Start, int End) DecorationLineRangeToMinimapLineRange(int startLineNumber, int endLineNumber)
    {
        var minimapLineStart = ModelLineToMinimapLine(startLineNumber);
        var minimapLineEnd = ModelLineToMinimapLine(endLineNumber);
        if (startLineNumber != endLineNumber && minimapLineEnd == minimapLineStart)
        {
            if (minimapLineEnd == MinimapLines.Length)
            {
                if (minimapLineStart > 1)
                {
                    minimapLineStart--;
                }
            }
            else
            {
                minimapLineEnd++;
            }
        }

        return (minimapLineStart, minimapLineEnd);
    }

    public (int ChangeStartIndex, int ChangeEndIndex) OnLinesDeleted(ViewLinesDeletedEvent e)
    {
        // have the mapping be sticky
        var deletedLineCount = e.ToLineNumber - e.FromLineNumber + 1;
        var changeStartIndex = MinimapLines.Length;
        var changeEndIndex = 0;
        for (var i = MinimapLines.Length - 1; i >= 0; i--)
        {
            if (MinimapLines[i] < e.FromLineNumber)
            {
                break;
            }

            if (MinimapLines[i] <= e.ToLineNumber)
            {
                // this line got deleted => move to previous available
                MinimapLines[i] = Math.Max(1, e.FromLineNumber - 1);
                changeStartIndex = Math.Min(changeStartIndex, i);
                changeEndIndex = Math.Max(changeEndIndex, i);
            }
            else
            {
                MinimapLines[i] -= deletedLineCount;
            }
        }

        return (changeStartIndex, changeEndIndex);
    }

    public void OnLinesInserted(ViewLinesInsertedEvent e)
    {
        // have the mapping be sticky
        var insertedLineCount = e.ToLineNumber - e.FromLineNumber + 1;
        for (var i = MinimapLines.Length - 1; i >= 0; i--)
        {
            if (MinimapLines[i] < e.FromLineNumber)
            {
                break;
            }

            MinimapLines[i] += insertedLineCount;
        }
    }
}

/// <summary>A value per line of a contiguous range of lines (upstream's ContiguousLineMap).</summary>
internal sealed class ContiguousLineMap<T>
{
    private readonly int _startLineNumber;
    private readonly int _endLineNumber;
    private readonly T _defaultValue;
    private readonly T[] _values;

    public ContiguousLineMap(int startLineNumber, int endLineNumber, T defaultValue)
    {
        _startLineNumber = startLineNumber;
        _endLineNumber = endLineNumber;
        _defaultValue = defaultValue;
        _values = new T[Math.Max(0, endLineNumber - startLineNumber + 1)];
        Array.Fill(_values, defaultValue);
    }

    public bool Has(int lineNumber) => !EqualityComparer<T>.Default.Equals(Get(lineNumber), _defaultValue);

    public void Set(int lineNumber, T value)
    {
        if (lineNumber < _startLineNumber || lineNumber > _endLineNumber)
        {
            return;
        }

        _values[lineNumber - _startLineNumber] = value;
    }

    public T Get(int lineNumber)
    {
        if (lineNumber < _startLineNumber || lineNumber > _endLineNumber)
        {
            return _defaultValue;
        }

        return _values[lineNumber - _startLineNumber];
    }
}
