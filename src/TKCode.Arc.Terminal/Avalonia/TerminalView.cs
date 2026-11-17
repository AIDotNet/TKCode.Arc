using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Media;
using TKCode.Terminal.Core;

namespace TKCode.Terminal.Avalonia;

/// <summary>
/// 终端视图（05-rendering.md）：在 <see cref="Render"/> 中按快照绘制背景、装饰线、字形与光标。
/// 字形 x 坐标强制对齐网格；逐码点、按样式回退字体；字素簇经 HarfBuzz 整形；制表符、块元素、盲文与 Powerline
/// 由视图自己绘制（<see cref="CustomGlyphs"/>）。宿主可以加入自己的绘制层（<see cref="Layers"/>）与自绘字形
/// （<see cref="GlyphProviders"/>）。
/// </summary>
public sealed partial class TerminalView : Control
{
    /// <summary>Defines the <see cref="Snapshot"/> property.</summary>
    public static readonly StyledProperty<FrameSnapshot?> SnapshotProperty =
        AvaloniaProperty.Register<TerminalView, FrameSnapshot?>(nameof(Snapshot));

    /// <summary>Defines the <see cref="FontFamily"/> property.</summary>
    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        AvaloniaProperty.Register<TerminalView, FontFamily>(nameof(FontFamily), new FontFamily("Menlo, Cascadia Mono, Consolas, DejaVu Sans Mono, monospace"));

    /// <summary>Defines the <see cref="FontSize"/> property.</summary>
    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<TerminalView, double>(nameof(FontSize), 13);

    /// <summary>四周内边距（DIP）。</summary>
    public static readonly StyledProperty<double> PaddingProperty =
        AvaloniaProperty.Register<TerminalView, double>(nameof(Padding), 6);

    private GlyphTypeface? _face;
    private CellMetrics? _metrics;
    private TopLevel? _topLevel;
    private int _renderVersion;

    static TerminalView()
    {
        AffectsRender<TerminalView>(SnapshotProperty, FontFamilyProperty, FontSizeProperty, PaddingProperty, FontWeightProperty,
            BoldFontWeightProperty, LineHeightProperty, BoldIsBrightProperty, TextBlinkProperty, CustomGlyphsProperty, EmojiFontFamilyProperty,
            HoveredLinkProperty, LinkForegroundProperty, CursorBlinkProperty, UnfocusedCursorStyleProperty, CursorBarWidthProperty,
            CursorTextColorProperty, MinimumContrastRatioProperty);
        AffectsMeasure<TerminalView>(FontFamilyProperty, FontSizeProperty, PaddingProperty, FontWeightProperty, LineHeightProperty);
    }

    /// <summary>The frame to draw; the host publishes a new one for every change of the screen.</summary>
    public FrameSnapshot? Snapshot
    {
        get => GetValue(SnapshotProperty);
        set => SetValue(SnapshotProperty, value);
    }

    /// <summary>The monospaced font, as a comma-separated list of families; the first one the platform has is used.</summary>
    public FontFamily FontFamily
    {
        get => GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    /// <summary>The font size in DIP.</summary>
    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    /// <summary>The padding around the grid in DIP (rounded to device pixels).</summary>
    public double Padding
    {
        get => GetValue(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    /// <summary>
    /// Draw-time objects the host adds: highlights below the text, marks above it, overlays above the cursor. Changing the
    /// list redraws the view; a layer whose own content changes calls <see cref="Visual.InvalidateVisual"/>.
    /// </summary>
    public AvaloniaList<ITerminalRenderLayer> Layers { get; } = new();

    /// <summary>
    /// Code points the host draws itself (icon sets, extra symbol ranges), asked before the built-in box drawing, block,
    /// braille and Powerline glyphs. Unlike those, host providers draw whatever <see cref="CustomGlyphs"/> says.
    /// </summary>
    public AvaloniaList<ITerminalGlyphProvider> GlyphProviders { get; } = new();

    /// <summary>当前字体与缩放下的 cell 度量（设备像素）。</summary>
    public CellMetrics Metrics => _metrics ??= ComputeMetrics();

    /// <summary>给定可用尺寸（DIP）能容纳的网格尺寸，宿主据此请求 resize。</summary>
    public TerminalSize GridSizeFor(Size available)
    {
        var m = Metrics;
        var pad = Math.Round(Padding * m.Scaling);
        var cols = (int)Math.Max(1, (available.Width * m.Scaling - 2 * pad) / m.CellWidth);
        var rows = (int)Math.Max(1, (available.Height * m.Scaling - 2 * pad) / m.CellHeight);
        return new TerminalSize(cols, rows, m.CellWidth, m.CellHeight);
    }

    /// <summary>Bounds of <paramref name="columns"/> cells starting at (<paramref name="column"/>, <paramref name="row"/>) of the viewport, in DIP relative to the view.</summary>
    public Rect CellBounds(int column, int row, int columns = 1)
    {
        var m = Metrics;
        var scale = m.Scaling;
        var pad = Math.Round(Padding * scale);
        return new Rect((pad + column * m.CellWidth) / scale, (pad + row * m.CellHeight) / scale, columns * m.CellWidth / scale, m.CellHeight / scale);
    }

    /// <summary>The viewport cell under <paramref name="point"/> (DIP, relative to the view), or <c>null</c> outside the grid of the current frame.</summary>
    public ViewportPoint? CellAt(Point point)
    {
        if (Snapshot is not { } frame)
        {
            return null;
        }
        var m = Metrics;
        var pad = Math.Round(Padding * m.Scaling);
        var col = (int)Math.Floor((point.X * m.Scaling - pad) / m.CellWidth);
        var row = (int)Math.Floor((point.Y * m.Scaling - pad) / m.CellHeight);
        return col < 0 || row < 0 || col >= frame.Columns || row >= frame.Rows.Length ? null : new ViewportPoint(col, row);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var p = change.Property;
        if (p == FontFamilyProperty || p == FontSizeProperty || p == FontWeightProperty || p == BoldFontWeightProperty || p == EmojiFontFamilyProperty)
        {
            _face = null;
            _metrics = null;
            ClearFaces();
            _renderVersion++;
        }
        else if (p == LineHeightProperty)
        {
            _metrics = null;
            _renderVersion++;
        }
        else if (p == BoldIsBrightProperty || p == CustomGlyphsProperty || p == MinimumContrastRatioProperty)
        {
            _contrast.Clear();
            _renderVersion++;
        }
        if (p == SnapshotProperty)
        {
            NotifyCursorMoved();
            OnOutput();
        }
        if (p == TextBlinkProperty || p == CursorBlinkProperty || p == CursorBlinkIntervalProperty || p == IsFocusedProperty ||
            p == IsVisibleProperty)
        {
            ResetCursorBlink();
            UpdateBlinkTimer();
            InvalidateVisual();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);
        if (_topLevel is not null)
        {
            _topLevel.ScalingChanged += OnScalingChanged;
        }
        InvalidateMetrics();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_topLevel is not null)
        {
            _topLevel.ScalingChanged -= OnScalingChanged;
            _topLevel = null;
        }
        StopBlinkTimer();
        ClearRowCache();
    }

    private void OnLayersChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => InvalidateVisual();

    private void OnGlyphProvidersChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        _sprites.Clear();
        _renderVersion++;
        InvalidateVisual();
    }

    // Metrics are in device pixels: recompute them when the scaling is known (phones report it after the first layout)
    // or changes (another monitor).
    private void OnScalingChanged(object? sender, EventArgs e) => InvalidateMetrics();

    private void InvalidateMetrics()
    {
        _metrics = null;
        InvalidateMeasure();
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Snapshot is not { } frame)
        {
            return default;
        }
        var m = Metrics;
        var pad = Math.Round(Padding * m.Scaling);
        return new Size((frame.Columns * m.CellWidth + 2 * pad) / m.Scaling, (frame.Rows.Length * m.CellHeight + 2 * pad) / m.Scaling);
    }

    public override void Render(DrawingContext context)
    {
        var frame = Snapshot;
        var m = Metrics;
        var scale = m.Scaling;
        var pad = Math.Round(Padding * scale);
        var palette = frame?.Palette;
        context.FillRectangle(Brush(palette?.Background ?? new TerminalRgb(0, 0, 0)), new Rect(Bounds.Size));
        if (frame is null || palette is null)
        {
            StopBlinkTimer();
            return;
        }

        // 行缓存：未变化的行（同一 RowSnapshot 对象）不重新生成字形 run。先画所有行的背景，再画文字，
        // 这样 BelowText 层（选区、搜索高亮）位于背景之上、文字之下。
        EnsureRowCacheFor(palette, m, pad);
        var cellHeight = m.CellHeight / scale;
        var rows = frame.Rows;
        var rowRuns = _frameRuns;
        rowRuns.Clear();
        var anyBlink = false;
        for (var y = 0; y < rows.Length; y++)
        {
            var runs = RowRunsOf(rows[y], palette, m, pad);
            rowRuns.Add(runs);
            anyBlink |= runs.HasBlink;
            DrawBackgrounds(context, runs, (pad + y * m.CellHeight) / scale, cellHeight);
        }
        TerminalRenderContext? layerContext = null;
        RenderLayers(context, TerminalLayerPlacement.BelowText, frame, m, pad, ref layerContext);
        UpdateTextBlinkPhase();
        var hideBlinking = !_textBlinkOn;
        for (var y = 0; y < rows.Length; y++)
        {
            DrawRowText(context, rowRuns[y], (pad + y * m.CellHeight) / scale, scale, hideBlinking);
        }
        EndRowCacheFrame();
        rowRuns.Clear();
        _hasBlinkingText = anyBlink;

        DrawHoveredLink(context, frame, palette, m, pad);
        RenderLayers(context, TerminalLayerPlacement.AboveText, frame, m, pad, ref layerContext);
        DrawCursor(context, frame, palette, m, pad);
        DrawPreedit(context, palette.Foreground, palette.Background);
        RenderLayers(context, TerminalLayerPlacement.Overlay, frame, m, pad, ref layerContext);
        UpdateBlinkTimer();
    }

    private void RenderLayers(DrawingContext context, TerminalLayerPlacement placement, FrameSnapshot frame, CellMetrics m, double pad,
        ref TerminalRenderContext? layerContext)
    {
        foreach (var layer in Layers)
        {
            if (layer.Placement != placement)
            {
                continue;
            }
            layerContext ??= new TerminalRenderContext(this, frame, m, pad);
            layer.Render(context, layerContext);
        }
    }

    private CellMetrics ComputeMetrics()
    {
        var scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
        return CellMetrics.Compute(PrimaryFace(), FontSize, scaling, LineHeight);
    }
}
