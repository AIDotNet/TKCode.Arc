using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using TKCode.Terminal.Core;

namespace TKCode.Terminal.Avalonia;

/// <summary>
/// 终端视图（05-rendering.md）。M0：R3 宿主——在 <see cref="Render"/> 中按快照绘制背景、字形与光标。
/// 字形 x 坐标强制对齐网格；逐码点回退字体（M0 同步查询，M3 移到后台线程，§6.3）。
/// 尚未实现：行缓存、sprite（box drawing 等程序化字形）、下划线样式、选区、输入、IME。
/// </summary>
public sealed partial class TerminalView : Control
{
    public static readonly StyledProperty<FrameSnapshot?> SnapshotProperty =
        AvaloniaProperty.Register<TerminalView, FrameSnapshot?>(nameof(Snapshot));

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        AvaloniaProperty.Register<TerminalView, FontFamily>(nameof(FontFamily), new FontFamily("Menlo, Cascadia Mono, Consolas, DejaVu Sans Mono, monospace"));

    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<TerminalView, double>(nameof(FontSize), 13);

    /// <summary>四周内边距（DIP）。</summary>
    public static readonly StyledProperty<double> PaddingProperty =
        AvaloniaProperty.Register<TerminalView, double>(nameof(Padding), 6);

    private readonly Dictionary<int, GlyphTypeface?> _fallback = [];
    private GlyphTypeface? _face;
    private CellMetrics? _metrics;

    static TerminalView()
    {
        AffectsRender<TerminalView>(SnapshotProperty, FontFamilyProperty, FontSizeProperty, PaddingProperty);
        AffectsMeasure<TerminalView>(FontFamilyProperty, FontSizeProperty, PaddingProperty);
    }

    public FrameSnapshot? Snapshot
    {
        get => GetValue(SnapshotProperty);
        set => SetValue(SnapshotProperty, value);
    }

    public FontFamily FontFamily
    {
        get => GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public double Padding
    {
        get => GetValue(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

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

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FontFamilyProperty || change.Property == FontSizeProperty)
        {
            _face = null;
            _metrics = null;
            _fallback.Clear();
        }
        if (change.Property == SnapshotProperty)
        {
            NotifyCursorMoved();
        }
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
            return;
        }

        // 设备像素 → DIP
        Rect Cell(int col, int row, int width = 1) =>
            new((pad + col * m.CellWidth) / scale, (pad + row * m.CellHeight) / scale, width * m.CellWidth / scale, m.CellHeight / scale);

        for (var y = 0; y < frame.Rows.Length; y++)
        {
            var row = frame.Rows[y];
            var cells = row.Cells;

            // 背景：同色的连续格合并成一个矩形（inverse 时前景作背景）
            for (var x = 0; x < cells.Length;)
            {
                var bg = BackgroundOf(row, cells[x], palette);
                var end = x + 1;
                while (end < cells.Length && BackgroundOf(row, cells[end], palette) == bg)
                {
                    end++;
                }
                if (bg is { } color)
                {
                    context.FillRectangle(Brush(color), Cell(x, y, end - x));
                }
                x = end;
            }

            // 字形：每格一个 GlyphRun，原点对齐网格（宽字符占两格）
            for (var x = 0; x < cells.Length; x++)
            {
                var cell = cells[x];
                if (TCell.Codepoint(cell) is 0 or ' ' || TCell.Width(cell) == CellWidth.SpacerTail)
                {
                    continue;
                }
                var style = row.Styles[TCell.StyleIndex(cell)];
                if (style.Flags.HasFlag(StyleFlags.Invisible))
                {
                    continue;
                }
                DrawText(context, row.TextAt(x)!, TCell.Width(cell), ForegroundOf(style, palette), Cell(x, y), m);
            }
        }

        var cursor = frame.Cursor;
        if (cursor.Visible && cursor.Row < frame.Rows.Length && cursor.Column < frame.Columns)
        {
            var rect = Cell(cursor.Column, cursor.Row);
            var cursorColor = palette.Cursor ?? palette.Foreground;
            var cursorBrush = Brush(cursorColor);
            switch (cursor.Style)
            {
                case CursorStyle.Bar:
                    context.FillRectangle(cursorBrush, rect.WithWidth(Math.Max(1, m.UnderlineThickness) / scale));
                    break;
                case CursorStyle.Underline:
                    context.FillRectangle(cursorBrush, new Rect(rect.X, rect.Bottom - m.UnderlineThickness / scale, rect.Width, m.UnderlineThickness / scale));
                    break;
                case CursorStyle.BlockHollow:
                    context.DrawRectangle(new Pen(cursorBrush, 1 / scale), rect.Deflate(0.5 / scale));
                    break;
                default:
                    context.FillRectangle(cursorBrush, rect);
                    var row = frame.Rows[cursor.Row];
                    if (row.TextAt(cursor.Column) is { } under)
                    {
                        DrawText(context, under, TCell.Width(row.Cells[cursor.Column]), palette.Background, rect, m);
                    }
                    break;
            }
        }
        DrawPreedit(context, palette.Foreground, palette.Background);
    }

    private static TerminalRgb ForegroundOf(in ResolvedStyle style, TerminalPalette palette)
    {
        var fg = style.Flags.HasFlag(StyleFlags.Inverse) ? palette.Resolve(style.Background, foreground: false) : palette.Resolve(style.Foreground, foreground: true);
        return style.Flags.HasFlag(StyleFlags.Faint) ? new TerminalRgb((byte)(fg.R / 2), (byte)(fg.G / 2), (byte)(fg.B / 2)) : fg;
    }

    private static TerminalRgb? BackgroundOf(RowSnapshot row, ulong cell, TerminalPalette palette)
    {
        var style = row.Styles[TCell.StyleIndex(cell)];
        if (style.Flags.HasFlag(StyleFlags.Inverse))
        {
            return palette.Resolve(style.Foreground, foreground: true);
        }
        return style.Background.IsDefault ? null : palette.Resolve(style.Background, foreground: false);
    }

    private void DrawText(DrawingContext context, string text, CellWidth width, TerminalRgb color, Rect cellRect, CellMetrics m)
    {
        var codepoint = char.ConvertToUtf32(text, 0);
        var face = FaceFor(codepoint);
        if (face is null || !face.CharacterToGlyphMap.TryGetGlyph(codepoint, out var glyph))
        {
            return;
        }
        var advance = (width == CellWidth.Wide ? 2 : 1) * m.CellWidth / m.Scaling;
        var origin = new Point(cellRect.X, cellRect.Y + m.BaselineY / m.Scaling);
        var run = new GlyphRun(face, m.EmSizePx / m.Scaling, text.AsMemory(), [new GlyphInfo(glyph, 0, advance)], origin);
        context.DrawGlyphRun(Brush(color), run);
    }

    private GlyphTypeface? FaceFor(int codepoint)
    {
        var primary = _face ??= new Typeface(FontFamily).GlyphTypeface;
        if (primary.CharacterToGlyphMap.TryGetGlyph(codepoint, out _))
        {
            return primary;
        }
        if (!_fallback.TryGetValue(codepoint, out var face))
        {
            face = FontManager.Current.TryMatchCharacter(codepoint, FontStyle.Normal, FontWeight.Normal, FontStretch.Normal, FontFamily, null,
                out var typeface) ? typeface.GlyphTypeface : null;
            _fallback[codepoint] = face;
        }
        return face;
    }

    private CellMetrics ComputeMetrics()
    {
        var scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
        _face ??= new Typeface(FontFamily).GlyphTypeface;
        return CellMetrics.Compute(_face, FontSize, scaling);
    }

    private static IImmutableSolidColorBrush Brush(TerminalRgb c) => new global::Avalonia.Media.Immutable.ImmutableSolidColorBrush(Color.FromRgb(c.R, c.G, c.B));
}
