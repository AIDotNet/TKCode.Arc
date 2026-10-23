using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The measured geometry of the timeline's plot (Arc Agent timeline at 744 px: rows 44 high from 6 px down, bars 16 high
/// 14 px into their row, an axis band 30 high; spans inset 0.75 px at each end so neighbors keep a 1.5 px gap; 5 px
/// corners, 1.5 px on the leading edge of an active span; elbow connectors with 7 px corners; 7 px port dots), and the
/// width rules: a 176 px label column (132 below a 520 px container) and a plot at least 300 px wide, scrolling when the
/// container is narrower than that.
/// </summary>
internal static class AgentTimelineLayout
{
    public const double RowHeight = 44;
    public const double TopPadding = 6;
    public const double BarTop = 14;
    public const double BarHeight = 16;
    public const double AxisHeight = 30;
    public const double SpanInset = 0.75;
    public const double SpanRadius = 5;
    public const double ActiveEdgeRadius = 1.5;
    public const double LeadingEdgeWidth = 2;
    public const double ConnectorRadius = 7;
    public const double PortRadius = 3.5;
    public const double PortRing = 1.5;
    public const double KnobRadius = 4.5;
    public const double KnobRing = 2;
    public const double KnobFocusScale = 1.3;
    public const double TickLabelTop = 9;
    public const double TickFontSize = 12;
    public const double IconSize = 11;
    public const double LabelColumnWidth = 176;
    public const double NarrowLabelColumnWidth = 132;
    public const double LabelLeftPadding = 24;
    public const double NarrowLabelLeftPadding = 16;
    public const double TrackLeftPadding = 12;
    public const double TrackRightPadding = 24;
    public const double NarrowTrackRightPadding = 16;
    public const double MinPlotWidth = 300;
    public const double NarrowWidth = 520;
    public const double ShineWidth = 28;
    public const double ShinePeriod = 1.5;
    public const double ShineSweep = 1.05;
    public const double CardGap = 8;
    public const double CardOffset = 12.75;
    public const double CardMargin = 8;

    /// <summary>The playhead stays this far from the right of the visible plot while a scrolled replay follows it.</summary>
    public const double FollowMargin = 80;

    /// <summary>After a person scrolls the plot, the replay waits this long before following the playhead again.</summary>
    public const double FollowCooldown = 2.5;

    public static double RowTop(int row) => TopPadding + row * RowHeight;

    public static double BarY(int row) => RowTop(row) + BarTop;

    public static double BarBottom(int row) => BarY(row) + BarHeight;

    public static double BarCenter(int row) => BarY(row) + BarHeight / 2;

    /// <summary>The height of the rows (where the axis starts).</summary>
    public static double RowsHeight(int rows) => TopPadding + Math.Max(0, rows) * RowHeight;

    public static double PlotHeight(int rows) => RowsHeight(rows) + AxisHeight;

    /// <summary>A span's drawn box from <paramref name="start"/> to <paramref name="end"/> seconds.</summary>
    public static Rect SpanRect(double start, double end, double scale, int row)
        => new(start * scale + SpanInset, BarY(row), Math.Max(0, (end - start) * scale - 2 * SpanInset), BarHeight);

    /// <summary>The label column's width.</summary>
    public static double LabelWidth(bool narrow) => narrow ? NarrowLabelColumnWidth : LabelColumnWidth;

    /// <summary>The track's right padding.</summary>
    public static double TrackRight(bool narrow) => narrow ? NarrowTrackRightPadding : TrackRightPadding;

    /// <summary>The space the plot has inside a container whose inner width is <paramref name="inner"/> (before the 300 px floor).</summary>
    public static double AvailablePlotWidth(double inner, bool narrow)
        => inner - LabelWidth(narrow) - TrackLeftPadding - TrackRight(narrow);

    /// <summary>The plot's width: what the container leaves, but never under 300 px (the plot scrolls then).</summary>
    public static double PlotWidth(double inner, bool narrow) => Math.Max(MinPlotWidth, AvailablePlotWidth(inner, narrow));

    /// <summary>
    /// Where the shine's 28 px band starts, relative to the span's left edge, <paramref name="phase"/> seconds into its
    /// 1.5 s cycle: it sweeps from just left of the span to just past its right edge in the first 1.05 s on the in-out
    /// curve, then rests out of sight for 0.45 s.
    /// </summary>
    public static double ShineOffset(double phase, double width)
    {
        var p = phase % ShinePeriod;
        if (p < 0)
        {
            p += ShinePeriod;
        }
        if (p >= ShineSweep)
        {
            return width;
        }
        var eased = ArcEasings.InOut.Ease(p / ShineSweep);
        return -ShineWidth + eased * (width + ShineWidth);
    }

    /// <summary>
    /// Writes a handoff's elbow into <paramref name="context"/>: a dispatch drops from (x, top) and turns right into the
    /// lower span's start at its bar's middle; a result leaves the lower span's end at its bar's middle and turns up to
    /// (x, top). The corner is 7 px (less when there is not room). <paramref name="end"/> is NaN while the lower span is
    /// not known yet: the elbow then ends in a 7 px stub.
    /// </summary>
    public static void Elbow(StreamGeometryContext context, bool dispatch, double x, double top, double middle, double end)
    {
        var radius = ConnectorRadius;
        if (dispatch)
        {
            var right = double.IsFinite(end) ? Math.Max(end, x) : x + radius;
            radius = Math.Max(0, Math.Min(radius, Math.Min(right - x, middle - top)));
            context.BeginFigure(new Point(x, top), false);
            context.LineTo(new Point(x, middle - radius));
            if (radius > 0)
            {
                context.ArcTo(new Point(x + radius, middle), new Size(radius, radius), 0, false, SweepDirection.CounterClockwise);
            }
            context.LineTo(new Point(right, middle));
            context.EndFigure(false);
            return;
        }
        var left = double.IsFinite(end) ? Math.Min(end, x) : x - radius;
        radius = Math.Max(0, Math.Min(radius, Math.Min(x - left, middle - top)));
        context.BeginFigure(new Point(left, middle), false);
        context.LineTo(new Point(x - radius, middle));
        if (radius > 0)
        {
            context.ArcTo(new Point(x, middle - radius), new Size(radius, radius), 0, false, SweepDirection.CounterClockwise);
        }
        context.LineTo(new Point(x, top));
        context.EndFigure(false);
    }
}

/// <summary>The colors of one agent row: its dot, the span fills by kind and state, the hatch, its connectors and port.</summary>
internal sealed class AgentTimelineRowPalette
{
    public AgentTimelineRowPalette(Color color, Color surface, Color foreground, double tint, double hoverTint)
    {
        Color = color;
        Solid = new ImmutableSolidColorBrush(color);
        Thinking = new ImmutableSolidColorBrush(Oklch.MixOklab(surface, color, tint));
        ThinkingHover = new ImmutableSolidColorBrush(Oklch.MixOklab(surface, color, hoverTint));
        ToolHover = new ImmutableSolidColorBrush(Oklch.MixOklab(color, foreground, 0.14));
        ToolEdge = new ImmutableSolidColorBrush(Oklch.MixOklab(color, foreground, 0.30));
        WaitingFill = new ImmutableSolidColorBrush(Oklch.MixOklab(surface, color, 0.05));
        Hatch = new ImmutablePen(new ImmutableSolidColorBrush(Oklch.WithAlpha(color, 0.30)), 1);
        WaitingInset = new ImmutablePen(new ImmutableSolidColorBrush(Oklch.WithAlpha(color, 0.24)), 1);
        Connector = new ImmutablePen((IImmutableBrush)Solid, 1);
    }

    public Color Color { get; }

    /// <summary>The agent color: dot, tool fill, connector, port, the leading edge of thinking and waiting.</summary>
    public IBrush Solid { get; }

    public IBrush Thinking { get; }

    public IBrush ThinkingHover { get; }

    public IBrush ToolHover { get; }

    /// <summary>A tool span's leading edge: 70% agent, 30% foreground.</summary>
    public IBrush ToolEdge { get; }

    public IBrush WaitingFill { get; }

    public IPen Hatch { get; }

    public IPen WaitingInset { get; }

    public IPen Connector { get; }
}

/// <summary>
/// Everything the timeline draws with, built once per theme or accent change from the ink colors its theme sets (no brush
/// or pen is made while a frame draws): the neutrals, the four series colors, the failed look, the shine band and the
/// per-agent rows.
/// </summary>
internal sealed class AgentTimelinePalette
{
    public AgentTimelinePalette(Color surface, Color foreground, Color neutral, Color muted, Color border, Color grid, Color danger,
        Color success, Color focus, Color[] series, double tint, double hoverTint)
    {
        Surface = surface;
        Foreground = foreground;
        Neutral = neutral;
        Muted = muted;
        Danger = danger;
        Success = success;
        Series = series;
        Tint = tint;
        HoverTint = hoverTint;
        SurfaceBrush = new ImmutableSolidColorBrush(surface);
        ForegroundBrush = new ImmutableSolidColorBrush(foreground);
        MutedBrush = new ImmutableSolidColorBrush(muted);
        NeutralBrush = new ImmutableSolidColorBrush(neutral);
        DangerBrush = new ImmutableSolidColorBrush(danger);
        SuccessBrush = new ImmutableSolidColorBrush(success);
        GridPen = new ImmutablePen(new ImmutableSolidColorBrush(grid), 1);
        AxisPen = new ImmutablePen(new ImmutableSolidColorBrush(border), 1);
        PlayheadPen = new ImmutablePen((IImmutableBrush)ForegroundBrush, 1);
        FocusPen = new ImmutablePen(new ImmutableSolidColorBrush(focus), 2);
        FailedFill = new ImmutableSolidColorBrush(Oklch.MixOklab(surface, danger, 0.14));
        FailedInset = new ImmutablePen(new ImmutableSolidColorBrush(Oklch.WithAlpha(danger, 0.55)), 1);
        // Icons are 24 × 24 glyphs drawn at 11 px with a stroke of 2 viewBox units.
        FailedIcon = new ImmutablePen((IImmutableBrush)DangerBrush, 2, null, PenLineCap.Round, PenLineJoin.Round);
        RetryIcon = new ImmutablePen((IImmutableBrush)SurfaceBrush, 2, null, PenLineCap.Round, PenLineJoin.Round);
        LegendThinking = new ImmutableSolidColorBrush(Oklch.MixOklab(surface, neutral, 0.28));
        LegendHatch = new ImmutablePen(new ImmutableSolidColorBrush(Oklch.WithAlpha(neutral, 0.40)), 1);
        LegendInset = new ImmutablePen(new ImmutableSolidColorBrush(Oklch.WithAlpha(neutral, 0.30)), 1);
        var white = Colors.White;
        Shine = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(0, white.R, white.G, white.B), 0),
                new GradientStop(Color.FromArgb((byte)Math.Round(0.62 * 255), white.R, white.G, white.B), 0.5),
                new GradientStop(Color.FromArgb(0, white.R, white.G, white.B), 1)
            }
        };
    }

    public Color Surface { get; }

    public Color Foreground { get; }

    public Color Neutral { get; }

    public Color Muted { get; }

    public Color Danger { get; }

    public Color Success { get; }

    public Color[] Series { get; }

    public double Tint { get; }

    public double HoverTint { get; }

    public IBrush SurfaceBrush { get; }

    public IBrush ForegroundBrush { get; }

    public IBrush MutedBrush { get; }

    public IBrush NeutralBrush { get; }

    public IBrush DangerBrush { get; }

    public IBrush SuccessBrush { get; }

    public IPen GridPen { get; }

    public IPen AxisPen { get; }

    public IPen PlayheadPen { get; }

    public IPen FocusPen { get; }

    public IBrush FailedFill { get; }

    public IPen FailedInset { get; }

    public IPen FailedIcon { get; }

    public IPen RetryIcon { get; }

    public IBrush LegendThinking { get; }

    public IPen LegendHatch { get; }

    public IPen LegendInset { get; }

    /// <summary>The shine band: clear, white at 62% in the middle, clear (mapped onto the 28 px band it fills).</summary>
    public IBrush Shine { get; }

    /// <summary>
    /// The default color of row <paramref name="row"/>: a neutral (secondary text) for the orchestrator, then chart series 1
    /// to 4, in turn.
    /// </summary>
    public Color DefaultColor(int row) => row <= 0 || Series.Length == 0 ? Neutral : Series[(row - 1) % Series.Length];

    /// <summary>The palette of one agent row (its own color, or the row's default).</summary>
    public AgentTimelineRowPalette Row(int row, AgentTimelineAgent agent)
    {
        var color = agent.Color is ISolidColorBrush solid ? solid.Color : DefaultColor(row);
        return new AgentTimelineRowPalette(color, Surface, Foreground, Tint, HoverTint);
    }
}

/// <summary>
/// Lucide glyphs the timeline needs that Core <see cref="ArcIcons"/> does not ship (24 × 24, stroked). Path data is
/// lucide-static 1.59.0 as converted by <c>tools/lucide/generate_lucide.py</c> (the same string as
/// <c>TKCode.Arc.Icons.Lucide</c>, which a control package must not depend on), parsed once by <see cref="ArcIcons.Parse"/>.
/// </summary>
internal static class AgentTimelineIcons
{
    // Lucide corner-up-left, ISC.
    private const string CornerUpLeftData = "M20 20v-7a4 4 0 0 0-4-4H4 M9 14 4 9l5-5";

    /// <summary>An arrow that turns up and back: work handed back to another agent.</summary>
    public static Geometry CornerUpLeft => ArcIcons.Parse(CornerUpLeftData);
}
