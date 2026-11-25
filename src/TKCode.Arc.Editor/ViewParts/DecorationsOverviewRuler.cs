// Ported from vscode (src/vs/editor/browser/viewParts/overviewRuler/decorationsOverviewRuler.ts, commit b57d5c9e),
// MIT license.
//
// Differences:
//  - no canvas: Render returns an OverviewRulerFrame (the background and the filled rectangles, in canvas pixels) that
//    the host draws; the border's two strokes are one-pixel rectangles;
//  - Settings is built from values the host resolved (the overview ruler position of the layout, the theme colors)
//    instead of reading the configuration and the theme; the cursor colors are given opaque and made 70% transparent
//    here, as upstream does with transparent(0.7); the theme type is not part of it (the host passes new colors);
//  - a decoration group's color is a hex string or a theme color id (OverviewRulerDecorationsGroup.Color);
//    ColorResolver turns it into a color (upstream hands the string to the canvas as a CSS color);
//  - the tokens color map listener is the host's (it calls SetSettings with the new background).

using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc.Editor.ViewParts;

/// <summary>A filled rectangle of the overview ruler, in canvas (device) pixels.</summary>
internal readonly record struct OverviewRulerRect(RgbaColor Color, int X, int Y, int Width, int Height);

/// <summary>What the host draws for an overview ruler frame.</summary>
internal sealed class OverviewRulerFrame
{
    public OverviewRulerFrame(OverviewRulerSettings settings, IReadOnlyList<OverviewRulerRect> rects)
    {
        Settings = settings;
        Rects = rects;
    }

    public OverviewRulerSettings Settings { get; }

    /// <summary>The decorations (in z-index order), then the cursors, then the border.</summary>
    public IReadOnlyList<OverviewRulerRect> Rects { get; }

    /// <summary>The background to fill first, or null (clear).</summary>
    public RgbaColor? Background => Settings.BackgroundColor;

    /// <summary>Whether nothing is drawn (no lanes).</summary>
    public bool IsHidden => Settings.OverviewRulerLanes == 0;
}

/// <summary>Upstream's <c>Settings</c> of the decorations overview ruler.</summary>
internal sealed class OverviewRulerSettings
{
    /// <param name="position">The overview ruler's place (EditorLayoutInfo.OverviewRuler).</param>
    /// <param name="lineHeight">The editor's line height.</param>
    /// <param name="pixelRatio">Device pixels per CSS pixel.</param>
    /// <param name="overviewRulerLanes">Upstream's <c>overviewRulerLanes</c> (default 3; 0 hides the ruler).</param>
    /// <param name="renderBorder">Upstream's <c>overviewRulerBorder</c> (default true).</param>
    /// <param name="borderColor"><c>editorOverviewRuler.border</c>, or null.</param>
    /// <param name="hideCursor">Upstream's <c>hideCursorInOverviewRuler</c> (default false).</param>
    /// <param name="cursorColor"><c>editorCursor.foreground</c> (opaque; drawn at 70%), or null.</param>
    /// <param name="cursorColorPrimary"><c>editorMultiCursor.primary.foreground</c>; null uses <paramref name="cursorColor"/>.</param>
    /// <param name="cursorColorSecondary"><c>editorMultiCursor.secondary.foreground</c>; null uses <paramref name="cursorColor"/>.</param>
    /// <param name="backgroundColor">
    /// <c>editorOverviewRuler.background</c>, or (as upstream) the default background when the minimap is on the right;
    /// null leaves the ruler transparent.
    /// </param>
    public OverviewRulerSettings(
        OverviewRulerPosition position,
        double lineHeight,
        double pixelRatio = 1,
        int overviewRulerLanes = 3,
        bool renderBorder = true,
        RgbaColor? borderColor = null,
        bool hideCursor = false,
        RgbaColor? cursorColor = null,
        RgbaColor? cursorColorPrimary = null,
        RgbaColor? cursorColorSecondary = null,
        RgbaColor? backgroundColor = null)
    {
        LineHeight = lineHeight;
        PixelRatio = pixelRatio;
        OverviewRulerLanes = overviewRulerLanes;

        RenderBorder = renderBorder;
        BorderColor = borderColor;

        HideCursor = hideCursor;
        CursorColorSingle = Transparent(cursorColor, 0.7);
        CursorColorPrimary = Transparent(cursorColorPrimary ?? cursorColor, 0.7);
        CursorColorSecondary = Transparent(cursorColorSecondary ?? cursorColor, 0.7);

        BackgroundColor = backgroundColor;

        Top = position.Top;
        Right = position.Right;
        DomWidth = position.Width;
        DomHeight = position.Height;
        if (OverviewRulerLanes == 0)
        {
            // overview ruler is off
            CanvasWidth = 0;
            CanvasHeight = 0;
        }
        else
        {
            CanvasWidth = (int)(DomWidth * PixelRatio);
            CanvasHeight = (int)(DomHeight * PixelRatio);
        }

        (X, W) = InitLanes(1, CanvasWidth, OverviewRulerLanes);
    }

    public double LineHeight { get; }

    public double PixelRatio { get; }

    public int OverviewRulerLanes { get; }

    public bool RenderBorder { get; }

    public RgbaColor? BorderColor { get; }

    public bool HideCursor { get; }

    public RgbaColor? CursorColorSingle { get; }

    public RgbaColor? CursorColorPrimary { get; }

    public RgbaColor? CursorColorSecondary { get; }

    public RgbaColor? BackgroundColor { get; }

    public double Top { get; }

    public double Right { get; }

    public double DomWidth { get; }

    public double DomHeight { get; }

    public int CanvasWidth { get; }

    public int CanvasHeight { get; }

    /// <summary>The x of each lane combination (index = OverviewRulerLane flags).</summary>
    public int[] X { get; }

    /// <summary>The width of each lane combination (index = OverviewRulerLane flags).</summary>
    public int[] W { get; }

    private static RgbaColor? Transparent(RgbaColor? color, double factor) =>
        color is { } c ? c with { A = (byte)Math.Round(c.A * factor, MidpointRounding.AwayFromZero) } : null;

    private static (int[] X, int[] W) InitLanes(int canvasLeftOffset, int canvasWidth, int laneCount)
    {
        var remainingWidth = canvasWidth - canvasLeftOffset;

        if (laneCount >= 3)
        {
            var leftWidth = (int)Math.Floor(remainingWidth / 3.0);
            var rightWidth = (int)Math.Floor(remainingWidth / 3.0);
            var centerWidth = remainingWidth - leftWidth - rightWidth;
            var leftOffset = canvasLeftOffset;
            var centerOffset = leftOffset + leftWidth;
            var rightOffset = leftOffset + leftWidth + centerWidth;

            return (
                [
                    0,
                    leftOffset, // Left
                    centerOffset, // Center
                    leftOffset, // Left | Center
                    rightOffset, // Right
                    leftOffset, // Left | Right
                    centerOffset, // Center | Right
                    leftOffset, // Left | Center | Right
                ],
                [
                    0,
                    leftWidth, // Left
                    centerWidth, // Center
                    leftWidth + centerWidth, // Left | Center
                    rightWidth, // Right
                    leftWidth + centerWidth + rightWidth, // Left | Right
                    centerWidth + rightWidth, // Center | Right
                    leftWidth + centerWidth + rightWidth, // Left | Center | Right
                ]);
        }

        if (laneCount == 2)
        {
            var leftWidth = (int)Math.Floor(remainingWidth / 2.0);
            var rightWidth = remainingWidth - leftWidth;
            var leftOffset = canvasLeftOffset;
            var rightOffset = leftOffset + leftWidth;

            return (
                [
                    0,
                    leftOffset, // Left
                    leftOffset, // Center
                    leftOffset, // Left | Center
                    rightOffset, // Right
                    leftOffset, // Left | Right
                    leftOffset, // Center | Right
                    leftOffset, // Left | Center | Right
                ],
                [
                    0,
                    leftWidth, // Left
                    leftWidth, // Center
                    leftWidth, // Left | Center
                    rightWidth, // Right
                    leftWidth + rightWidth, // Left | Right
                    leftWidth + rightWidth, // Center | Right
                    leftWidth + rightWidth, // Left | Center | Right
                ]);
        }

        var offset = canvasLeftOffset;
        var width = remainingWidth;
        return (
            [0, offset, offset, offset, offset, offset, offset, offset],
            [0, width, width, width, width, width, width, width]);
    }

    public bool Equals(OverviewRulerSettings other) =>
        LineHeight == other.LineHeight
        && PixelRatio == other.PixelRatio
        && OverviewRulerLanes == other.OverviewRulerLanes
        && RenderBorder == other.RenderBorder
        && BorderColor == other.BorderColor
        && HideCursor == other.HideCursor
        && CursorColorSingle == other.CursorColorSingle
        && CursorColorPrimary == other.CursorColorPrimary
        && CursorColorSecondary == other.CursorColorSecondary
        && BackgroundColor == other.BackgroundColor
        && Top == other.Top
        && Right == other.Right
        && DomWidth == other.DomWidth
        && DomHeight == other.DomHeight
        && CanvasWidth == other.CanvasWidth
        && CanvasHeight == other.CanvasHeight;
}

/// <summary>
/// The decorations overview ruler: the decorations with overview ruler options (find matches, selection highlights,
/// markers) and the cursors as marks over the whole height of the document, beside the vertical scroll bar. One per
/// editor view; it listens to the view model's events and draws a frame only when what it shows changed.
/// </summary>
internal sealed class DecorationsOverviewRuler : ViewEventHandler
{
    private const int MinDecorationHeight = 6;

    private readonly IViewModel _viewModel;
    private ShouldRenderValue _actualShouldRender = ShouldRenderValue.NotNeeded;
    private List<CursorMark> _cursorPositions;
    private IReadOnlyList<OverviewRulerDecorationsGroup> _renderedDecorations = [];
    private List<CursorMark> _renderedCursorPositions = [];
    private OverviewRulerFrame? _lastFrame;

    public DecorationsOverviewRuler(IViewModel viewModel, OverviewRulerSettings settings)
    {
        _viewModel = viewModel;
        Settings = settings;
        _cursorPositions = [new CursorMark(new TextPosition(1, 1), Settings.CursorColorSingle)];
        _actualShouldRender = ShouldRenderValue.Needed;
    }

    private enum ShouldRenderValue
    {
        NotNeeded = 0,
        Maybe = 1,
        Needed = 2,
    }

    private readonly record struct CursorMark(TextPosition Position, RgbaColor? Color);

    public OverviewRulerSettings Settings { get; private set; }

    /// <summary>The color of a decoration group (a hex string or a theme color id); null draws nothing. Default: hex only.</summary>
    public Func<string, RgbaColor?> ColorResolver { get; set; } = RgbaColor.TryParseHex;

    /// <summary>The last frame drawn, or null before the first.</summary>
    public OverviewRulerFrame? LastFrame => _lastFrame;

    /// <summary>Takes new settings (upstream's _updateSettings); false when they draw the same.</summary>
    public bool SetSettings(OverviewRulerSettings settings)
    {
        if (Settings.Equals(settings))
        {
            // nothing to do
            return false;
        }

        Settings = settings;
        // the cursor colors follow the settings
        UpdateCursorColors();
        MarkRenderingIsNeeded();
        return true;
    }

    /// <summary>Draws again on the next frame even if nothing changed (a decoration color changed in the theme).</summary>
    public void Invalidate() => MarkRenderingIsNeeded();

    // ---- begin view event handlers

    private bool MarkRenderingIsNeeded()
    {
        _actualShouldRender = ShouldRenderValue.Needed;
        SetShouldRender();
        return true;
    }

    private bool MarkRenderingIsMaybeNeeded()
    {
        if (_actualShouldRender != ShouldRenderValue.Needed)
        {
            _actualShouldRender = ShouldRenderValue.Maybe;
        }

        return true;
    }

    public override bool OnCursorStateChanged(ViewCursorStateChangedEvent e)
    {
        _cursorPositions = new List<CursorMark>(e.Selections.Count);
        for (int i = 0, len = e.Selections.Count; i < len; i++)
        {
            var color = Settings.CursorColorSingle;
            if (len > 1)
            {
                color = i == 0 ? Settings.CursorColorPrimary : Settings.CursorColorSecondary;
            }

            _cursorPositions.Add(new CursorMark(e.Selections[i].Position, color));
        }

        // a stable sort, as upstream's Array.prototype.sort
        _cursorPositions = [.. _cursorPositions.OrderBy(c => c.Position)];
        return MarkRenderingIsMaybeNeeded();
    }

    public override bool OnDecorationsChanged(ViewDecorationsChangedEvent e)
    {
        if (e.AffectsOverviewRuler)
        {
            return MarkRenderingIsMaybeNeeded();
        }

        return false;
    }

    public override bool OnFlushed(ViewFlushedEvent e) => MarkRenderingIsNeeded();

    public override bool OnScrollChanged(ViewScrollChangedEvent e) => e.ScrollHeightChanged && MarkRenderingIsNeeded();

    public override bool OnZonesChanged(ViewZonesChangedEvent e) => MarkRenderingIsNeeded();

    // onConfigurationChanged and onThemeChanged re-read the settings upstream; here the host calls SetSettings.

    // ---- end view event handlers

    private void UpdateCursorColors()
    {
        var len = _cursorPositions.Count;
        for (var i = 0; i < len; i++)
        {
            var color = Settings.CursorColorSingle;
            if (len > 1)
            {
                color = i == 0 ? Settings.CursorColorPrimary : Settings.CursorColorSecondary;
            }

            _cursorPositions[i] = _cursorPositions[i] with { Color = color };
        }
    }

    /// <summary>
    /// Draws a frame when something changed (upstream's render), and returns the frame to show: the new one, or the last
    /// one when the decorations and the cursor lines are as they were.
    /// </summary>
    public OverviewRulerFrame Render()
    {
        var frame = RenderCore();
        _actualShouldRender = ShouldRenderValue.NotNeeded;
        OnDidRender();
        return frame;
    }

    private OverviewRulerFrame RenderCore()
    {
        if (Settings.OverviewRulerLanes == 0)
        {
            // overview ruler is off
            _lastFrame = new OverviewRulerFrame(Settings, []);
            return _lastFrame;
        }

        var decorations = _viewModel.GetAllOverviewRulerDecorations(this).ToList();
        decorations.Sort(OverviewRulerDecorationsGroup.CompareByRenderingProps);

        if (_lastFrame is null)
        {
            _actualShouldRender = ShouldRenderValue.Needed;
        }

        if (_actualShouldRender == ShouldRenderValue.Maybe && !OverviewRulerDecorationsGroup.EqualsArr(_renderedDecorations, decorations))
        {
            _actualShouldRender = ShouldRenderValue.Needed;
        }

        if (_actualShouldRender == ShouldRenderValue.Maybe && !CursorLinesEqual(_renderedCursorPositions, _cursorPositions))
        {
            _actualShouldRender = ShouldRenderValue.Needed;
        }

        if (_actualShouldRender != ShouldRenderValue.Needed && _lastFrame is not null)
        {
            // both decorations and cursor positions are unchanged, nothing to do
            return _lastFrame;
        }

        _renderedDecorations = decorations;
        _renderedCursorPositions = _cursorPositions;

        var rects = new List<OverviewRulerRect>();
        var canvasWidth = Settings.CanvasWidth;
        var canvasHeight = Settings.CanvasHeight;
        var lineHeight = Settings.LineHeight;
        var viewLayout = _viewModel.ViewLayout;
        var outerHeight = viewLayout.GetScrollHeight();
        var heightRatio = outerHeight > 0 ? canvasHeight / outerHeight : 0;

        var minDecorationHeight = (int)(MinDecorationHeight * Settings.PixelRatio);
        var halfMinDecorationHeight = minDecorationHeight / 2;

        var x = Settings.X;
        var w = Settings.W;

        foreach (var decorationGroup in decorations)
        {
            var color = ColorResolver(decorationGroup.Color);
            var decorationGroupData = decorationGroup.Data;

            var prevLane = 0;
            var prevY1 = 0;
            var prevY2 = 0;
            for (int i = 0, len = decorationGroupData.Count / 3; i < len; i++)
            {
                var lane = decorationGroupData[3 * i];
                var startLineNumber = decorationGroupData[(3 * i) + 1];
                var endLineNumber = decorationGroupData[(3 * i) + 2];

                var y1 = (int)(viewLayout.GetVerticalOffsetForLineNumber(startLineNumber) * heightRatio);
                var y2 = (int)((viewLayout.GetVerticalOffsetForLineNumber(endLineNumber) + lineHeight) * heightRatio);
                var height = y2 - y1;
                if (height < minDecorationHeight)
                {
                    var yCenter = (y1 + y2) / 2;
                    if (yCenter < halfMinDecorationHeight)
                    {
                        yCenter = halfMinDecorationHeight;
                    }
                    else if (yCenter + halfMinDecorationHeight > canvasHeight)
                    {
                        yCenter = canvasHeight - halfMinDecorationHeight;
                    }

                    y1 = yCenter - halfMinDecorationHeight;
                    y2 = yCenter + halfMinDecorationHeight;
                }

                if (y1 > prevY2 + 1 || lane != prevLane)
                {
                    // flush prev
                    if (i != 0)
                    {
                        FillRect(rects, color, x[prevLane], prevY1, w[prevLane], prevY2 - prevY1);
                    }

                    prevLane = lane;
                    prevY1 = y1;
                    prevY2 = y2;
                }
                else
                {
                    // merge into prev
                    if (y2 > prevY2)
                    {
                        prevY2 = y2;
                    }
                }
            }

            FillRect(rects, color, x[prevLane], prevY1, w[prevLane], prevY2 - prevY1);
        }

        // Draw cursors
        if (!Settings.HideCursor)
        {
            var cursorHeight = (int)(2 * Settings.PixelRatio);
            var halfCursorHeight = cursorHeight / 2;
            var cursorX = Settings.X[(int)Model.OverviewRulerLane.Full];
            var cursorW = Settings.W[(int)Model.OverviewRulerLane.Full];

            var prevY1 = -100;
            var prevY2 = -100;
            RgbaColor? prevColor = null;
            for (int i = 0, len = _cursorPositions.Count; i < len; i++)
            {
                var color = _cursorPositions[i].Color;
                if (color is null)
                {
                    continue;
                }

                var cursor = _cursorPositions[i].Position;

                var yCenter = (int)(viewLayout.GetVerticalOffsetForLineNumber(cursor.LineNumber) * heightRatio);
                if (yCenter < halfCursorHeight)
                {
                    yCenter = halfCursorHeight;
                }
                else if (yCenter + halfCursorHeight > canvasHeight)
                {
                    yCenter = canvasHeight - halfCursorHeight;
                }

                var y1 = yCenter - halfCursorHeight;
                var y2 = y1 + cursorHeight;

                if (y1 > prevY2 + 1 || color != prevColor)
                {
                    // flush prev
                    if (i != 0 && prevColor is not null)
                    {
                        FillRect(rects, prevColor, cursorX, prevY1, cursorW, prevY2 - prevY1);
                    }

                    prevY1 = y1;
                    prevY2 = y2;
                }
                else
                {
                    // merge into prev
                    if (y2 > prevY2)
                    {
                        prevY2 = y2;
                    }
                }

                prevColor = color;
            }

            if (prevColor is not null)
            {
                FillRect(rects, prevColor, cursorX, prevY1, cursorW, prevY2 - prevY1);
            }
        }

        if (Settings.RenderBorder && Settings.BorderColor is { } borderColor && Settings.OverviewRulerLanes > 0)
        {
            // upstream strokes (0,0)-(0,h) and (1,0)-(w,0) one pixel wide
            FillRect(rects, borderColor, 0, 0, 1, canvasHeight);
            FillRect(rects, borderColor, 1, 0, canvasWidth - 1, 1);
        }

        _lastFrame = new OverviewRulerFrame(Settings, rects);
        return _lastFrame;
    }

    private static void FillRect(List<OverviewRulerRect> rects, RgbaColor? color, int x, int y, int width, int height)
    {
        if (color is not { A: > 0 } c || width <= 0 || height <= 0)
        {
            return;
        }

        rects.Add(new OverviewRulerRect(c, x, y, width, height));
    }

    private static bool CursorLinesEqual(List<CursorMark> a, List<CursorMark> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        for (var i = 0; i < a.Count; i++)
        {
            if (a[i].Position.LineNumber != b[i].Position.LineNumber || a[i].Color != b[i].Color)
            {
                return false;
            }
        }

        return true;
    }
}
