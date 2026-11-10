using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// The budget meter of <see cref="SpendAlert"/>, drawn in one pass: the track, the pace segment (where the period's pace
/// lands), the spent fill (accent, warning once an alert is reached, danger past the budget), the budget line and one tick
/// per switched-on alert with its percentage above it (ticks already reached take the fill's tone).
/// </summary>
internal sealed class SpendAlertMeter : Control
{
    /// <summary>Spent as a share of the budget; springs in the full motion level.</summary>
    public static readonly StyledProperty<double> FractionProperty =
        AvaloniaProperty.Register<SpendAlertMeter, double>(nameof(Fraction));

    /// <summary>Where the pace lands as a share of the budget.</summary>
    public static readonly StyledProperty<double> ProjectedProperty =
        AvaloniaProperty.Register<SpendAlertMeter, double>(nameof(Projected));

    /// <summary>The share at the right end of the track (at least 1.1).</summary>
    public static readonly StyledProperty<double> ScaleProperty =
        AvaloniaProperty.Register<SpendAlertMeter, double>(nameof(Scale), 1.2);

    /// <summary>0 normal, 1 alert reached, 2 over budget.</summary>
    public static readonly StyledProperty<int> ToneProperty =
        AvaloniaProperty.Register<SpendAlertMeter, int>(nameof(Tone));

    public static readonly StyledProperty<double> BarHeightProperty =
        AvaloniaProperty.Register<SpendAlertMeter, double>(nameof(BarHeight), 12);

    public static readonly StyledProperty<double> LabelBandProperty =
        AvaloniaProperty.Register<SpendAlertMeter, double>(nameof(LabelBand), 18);

    public static readonly StyledProperty<double> LabelFontSizeProperty =
        AvaloniaProperty.Register<SpendAlertMeter, double>(nameof(LabelFontSize), 12);

    public static readonly StyledProperty<FontFamily?> LabelFontFamilyProperty =
        AvaloniaProperty.Register<SpendAlertMeter, FontFamily?>(nameof(LabelFontFamily));

    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<SpendAlertMeter, IBrush?>(nameof(TrackBrush));

    public static readonly StyledProperty<IBrush?> FillBrushProperty =
        AvaloniaProperty.Register<SpendAlertMeter, IBrush?>(nameof(FillBrush));

    public static readonly StyledProperty<IBrush?> WarningBrushProperty =
        AvaloniaProperty.Register<SpendAlertMeter, IBrush?>(nameof(WarningBrush));

    public static readonly StyledProperty<IBrush?> DangerBrushProperty =
        AvaloniaProperty.Register<SpendAlertMeter, IBrush?>(nameof(DangerBrush));

    public static readonly StyledProperty<IBrush?> PaceBrushProperty =
        AvaloniaProperty.Register<SpendAlertMeter, IBrush?>(nameof(PaceBrush));

    public static readonly StyledProperty<IBrush?> TickBrushProperty =
        AvaloniaProperty.Register<SpendAlertMeter, IBrush?>(nameof(TickBrush));

    public static readonly StyledProperty<IBrush?> LabelBrushProperty =
        AvaloniaProperty.Register<SpendAlertMeter, IBrush?>(nameof(LabelBrush));

    private (double Share, bool Reached)[] _marks = [];
    private FormattedText?[] _labels = [];
    private ImmutablePen? _tickPen;
    private ImmutablePen? _reachedPen;
    private ImmutablePen? _budgetPen;

    static SpendAlertMeter()
    {
        AffectsRender<SpendAlertMeter>(FractionProperty, ProjectedProperty, ScaleProperty, ToneProperty, TrackBrushProperty, FillBrushProperty,
            WarningBrushProperty, DangerBrushProperty, PaceBrushProperty, TickBrushProperty, LabelBrushProperty);
        AffectsMeasure<SpendAlertMeter>(BarHeightProperty, LabelBandProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<SpendAlertMeter>(false);
    }

    public double Fraction { get => GetValue(FractionProperty); set => SetValue(FractionProperty, value); }
    public double Projected { get => GetValue(ProjectedProperty); set => SetValue(ProjectedProperty, value); }
    public double Scale { get => GetValue(ScaleProperty); set => SetValue(ScaleProperty, value); }
    public int Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }
    public double BarHeight { get => GetValue(BarHeightProperty); set => SetValue(BarHeightProperty, value); }
    public double LabelBand { get => GetValue(LabelBandProperty); set => SetValue(LabelBandProperty, value); }
    public double LabelFontSize { get => GetValue(LabelFontSizeProperty); set => SetValue(LabelFontSizeProperty, value); }
    public FontFamily? LabelFontFamily { get => GetValue(LabelFontFamilyProperty); set => SetValue(LabelFontFamilyProperty, value); }
    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }
    public IBrush? FillBrush { get => GetValue(FillBrushProperty); set => SetValue(FillBrushProperty, value); }
    public IBrush? WarningBrush { get => GetValue(WarningBrushProperty); set => SetValue(WarningBrushProperty, value); }
    public IBrush? DangerBrush { get => GetValue(DangerBrushProperty); set => SetValue(DangerBrushProperty, value); }
    public IBrush? PaceBrush { get => GetValue(PaceBrushProperty); set => SetValue(PaceBrushProperty, value); }
    public IBrush? TickBrush { get => GetValue(TickBrushProperty); set => SetValue(TickBrushProperty, value); }
    public IBrush? LabelBrush { get => GetValue(LabelBrushProperty); set => SetValue(LabelBrushProperty, value); }

    /// <summary>The ticks drawn (switched-on alerts), as shares of the budget and whether spending reached them.</summary>
    internal IReadOnlyList<(double Share, bool Reached)> Marks => _marks;

    /// <summary>Replaces the ticks.</summary>
    public void SetMarks(IEnumerable<(double Share, bool Reached)> marks)
    {
        _marks = marks.OrderBy(m => m.Share).ToArray();
        _labels = new FormattedText?[_marks.Length];
        InvalidateVisual();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelBrushProperty || change.Property == LabelFontSizeProperty || change.Property == LabelFontFamilyProperty)
        {
            _labels = new FormattedText?[_marks.Length];
            InvalidateVisual();
        }
        else if (change.Property == TickBrushProperty || change.Property == ToneProperty || change.Property == WarningBrushProperty
                 || change.Property == DangerBrushProperty || change.Property == FillBrushProperty)
        {
            _tickPen = _reachedPen = _budgetPen = null;
        }
    }

    protected override Size MeasureOverride(Size availableSize) => new(0, LabelBand + BarHeight + 4);

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        if (width <= 0)
        {
            return;
        }
        var scale = Math.Max(1.1, Scale);
        var top = LabelBand;
        var h = BarHeight;
        var r = h / 2;
        double X(double share) => Math.Clamp(share / scale, 0, 1) * width;

        context.DrawRectangle(TrackBrush, null, new Rect(0, top, width, h), r, r);
        var toneBrush = Tone switch
        {
            2 => DangerBrush,
            1 => WarningBrush,
            _ => FillBrush,
        };
        var fill = X(Fraction);
        var pace = X(Projected);
        if (pace > fill + 1)
        {
            context.DrawRectangle(PaceBrush, null, new Rect(0, top, pace, h), r, r);
        }
        if (fill > 0.5)
        {
            context.DrawRectangle(toneBrush, null, new Rect(0, top, Math.Max(fill, h), h), r, r);
        }

        _tickPen ??= new ImmutablePen((TickBrush ?? Brushes.Gray).ToImmutable(), 1.5);
        _reachedPen ??= new ImmutablePen((toneBrush ?? Brushes.Gray).ToImmutable(), 2);
        _budgetPen ??= new ImmutablePen((TickBrush ?? Brushes.Gray).ToImmutable(), 2, new ImmutableDashStyle([2, 2], 0));

        // The budget line runs through the bar and a little above and below it.
        var bx = Math.Round(X(1)) + 0.5;
        context.DrawLine(_budgetPen, new Point(bx, top - 4), new Point(bx, top + h + 4));

        for (var i = 0; i < _marks.Length; i++)
        {
            var (share, reached) = _marks[i];
            var x = Math.Round(X(share)) + 0.5;
            context.DrawLine(reached ? _reachedPen : _tickPen, new Point(x, top - 3), new Point(x, top + h + 3));
            var label = _labels[i] ??= new FormattedText(Percent(share), CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(LabelFontFamily ?? FontFamily.Default), LabelFontSize, LabelBrush);
            var lx = Math.Clamp(x - label.Width / 2, 0, Math.Max(0, width - label.Width));
            context.DrawText(label, new Point(lx, Math.Max(0, top - 4 - label.Height)));
        }
    }

    internal static string Percent(double share) => Math.Round(share * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
}

/// <summary>The alert banner: a tinted panel (warning or danger class) that rises in when an alert is crossed.</summary>
internal sealed class SpendAlertBanner : Border
{
    protected override Type StyleKeyOverride => typeof(SpendAlertBanner);
}

/// <summary>The status pill beside the title (classes on-track, at-risk, warning, over).</summary>
internal sealed class SpendAlertPill : Border
{
    protected override Type StyleKeyOverride => typeof(SpendAlertPill);
}
