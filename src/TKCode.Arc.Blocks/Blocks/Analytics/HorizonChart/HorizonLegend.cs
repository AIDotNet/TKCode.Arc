using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The colour scale of a <see cref="HorizonChart"/>: four cells below the baseline (outer to inner), the zero mark and four
/// cells above. A cell's width is its grow factor (1 while its layer exists at the band count, else 0) and its opacity is
/// the stacked alpha of its layer; both change over 480ms in-out when the band count does. Under it the end labels with
/// "below" and "above" between them, and the caption.
/// </summary>
/// <remarks>
/// Code-driven motion: the cells are rectangles inside one drawing whose widths share the line (a flex-grow
/// distribution), which a ControlTheme transition cannot express; eight tween pairs run on one <see cref="LiveLoop"/>.
/// </remarks>
internal sealed class HorizonLegend : Control
{
    private const int Cells = 8;
    private static readonly TimeSpan CellDuration = TimeSpan.FromMilliseconds(480);

    public static readonly StyledProperty<double> LegendWidthProperty =
        AvaloniaProperty.Register<HorizonLegend, double>(nameof(LegendWidth), 300);

    public static readonly StyledProperty<bool> IsFilledProperty =
        AvaloniaProperty.Register<HorizonLegend, bool>(nameof(IsFilled));

    public static readonly StyledProperty<double> RampHeightProperty =
        AvaloniaProperty.Register<HorizonLegend, double>(nameof(RampHeight), 10);

    public static readonly StyledProperty<double> CellRadiusProperty =
        AvaloniaProperty.Register<HorizonLegend, double>(nameof(CellRadius), 2);

    public static readonly StyledProperty<double> ZeroMarkHeightProperty =
        AvaloniaProperty.Register<HorizonLegend, double>(nameof(ZeroMarkHeight), 14);

    public static readonly StyledProperty<double> ZeroMarkMarginProperty =
        AvaloniaProperty.Register<HorizonLegend, double>(nameof(ZeroMarkMargin), 3);

    public static readonly StyledProperty<double> RowGapProperty =
        AvaloniaProperty.Register<HorizonLegend, double>(nameof(RowGap), 8);

    public static readonly StyledProperty<double> LabelGapProperty =
        AvaloniaProperty.Register<HorizonLegend, double>(nameof(LabelGap), 4);

    public static readonly StyledProperty<double> TextLineHeightProperty =
        AvaloniaProperty.Register<HorizonLegend, double>(nameof(TextLineHeight), 16.8);

    private readonly AnalyticsPalette _palette = new();
    private readonly AnalyticsTextCache _text = new(64);
    private readonly LiveLoop _loop;
    private readonly AnalyticsTween[] _grow = new AnalyticsTween[Cells];
    private readonly AnalyticsTween[] _opacity = new AnalyticsTween[Cells];
    private readonly double[] _grows = new double[Cells];
    private readonly double[] _widths = new double[Cells];
    private readonly Avalonia.Media.TextFormatting.TextLine[] _one = new Avalonia.Media.TextFormatting.TextLine[1];
    private int _bands = 3;
    private bool _initialized;
    private string _start = string.Empty;
    private string _below = string.Empty;
    private string _zero = "0";
    private string _above = string.Empty;
    private string _end = string.Empty;
    private string _caption = string.Empty;
    private Color? _aboveColor;
    private Color? _belowColor;
    private int _paletteVersion = -1;

    static HorizonLegend()
    {
        AffectsMeasure<HorizonLegend>(LegendWidthProperty, IsFilledProperty, RampHeightProperty, RowGapProperty, LabelGapProperty,
            TextLineHeightProperty);
        AffectsRender<HorizonLegend>(CellRadiusProperty, ZeroMarkHeightProperty, ZeroMarkMarginProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<HorizonLegend>(false);
        FocusableProperty.OverrideDefaultValue<HorizonLegend>(false);
    }

    public HorizonLegend()
    {
        // Function as well as decoration: the loop also finishes the cell tweens after the level drops (they then snap).
        _loop = new LiveLoop(this, Advance) { MinimumLevel = ArcMotionLevel.Off };
        ActualThemeVariantChanged += (_, _) => Invalidate();
        ResourcesChanged += (_, _) => Invalidate();
        for (var i = 0; i < Cells; i++)
        {
            _grow[i] = new AnalyticsTween(1);
            _opacity[i] = new AnalyticsTween(0);
        }
        ApplyBands(_bands, animate: false);
    }

    public double LegendWidth { get => GetValue(LegendWidthProperty); set => SetValue(LegendWidthProperty, value); }

    /// <summary>True takes the whole available width (the compact block), else <see cref="LegendWidth"/>.</summary>
    public bool IsFilled { get => GetValue(IsFilledProperty); set => SetValue(IsFilledProperty, value); }

    public double RampHeight { get => GetValue(RampHeightProperty); set => SetValue(RampHeightProperty, value); }

    public double CellRadius { get => GetValue(CellRadiusProperty); set => SetValue(CellRadiusProperty, value); }

    public double ZeroMarkHeight { get => GetValue(ZeroMarkHeightProperty); set => SetValue(ZeroMarkHeightProperty, value); }

    public double ZeroMarkMargin { get => GetValue(ZeroMarkMarginProperty); set => SetValue(ZeroMarkMarginProperty, value); }

    public double RowGap { get => GetValue(RowGapProperty); set => SetValue(RowGapProperty, value); }

    public double LabelGap { get => GetValue(LabelGapProperty); set => SetValue(LabelGapProperty, value); }

    public double TextLineHeight { get => GetValue(TextLineHeightProperty); set => SetValue(TextLineHeightProperty, value); }

    /// <summary>The caption under the scale.</summary>
    public string Caption => _caption;

    /// <summary>The current grow factor of cell <paramref name="cell"/> (0–7: below outer→inner, then above inner→outer).</summary>
    public double CellGrow(int cell) => _grow[cell].Value;

    /// <summary>The current opacity of cell <paramref name="cell"/>.</summary>
    public double CellOpacity(int cell) => _opacity[cell].Value;

    /// <summary>The cell widths at the current grows for a legend <paramref name="width"/> wide.</summary>
    public double[] CellWidths(double width)
    {
        var widths = new double[Cells];
        var grows = new double[Cells];
        for (var i = 0; i < Cells; i++)
        {
            grows[i] = _grow[i].Value;
        }
        HorizonEngine.LegendWidths(width, grows, widths, 2 * ZeroMarkMargin + 1);
        return widths;
    }

    /// <summary>True while a cell tween runs.</summary>
    public bool IsMoving => _grow.Any(t => t.IsRunning) || _opacity.Any(t => t.IsRunning);

    /// <summary>Sets the label texts and the caption.</summary>
    public void SetTexts(string start, string below, string zero, string above, string end, string caption)
    {
        _start = start;
        _below = below;
        _zero = zero;
        _above = above;
        _end = end;
        _caption = caption;
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>The colours of the cells (null: Series3 below, Series1 above).</summary>
    public void SetColors(Color? above, Color? below)
    {
        _aboveColor = above;
        _belowColor = below;
        InvalidateVisual();
    }

    /// <summary>Moves the scale to <paramref name="bands"/> bands (480ms in-out, at once when motion is not Full).</summary>
    public void SetBands(int bands, bool animate)
    {
        if (bands == _bands && _initialized)
        {
            return;
        }
        _bands = bands;
        ApplyBands(bands, animate && ArcMotion.IsFull(this) && this.IsAttachedToVisualTree());
    }

    private void ApplyBands(int bands, bool animate)
    {
        _initialized = true;
        for (var cell = 0; cell < Cells; cell++)
        {
            var layer = Layer(cell);
            var grow = HorizonEngine.LegendGrow(layer, bands);
            var opacity = HorizonEngine.LegendOpacity(layer, bands);
            if (animate)
            {
                _grow[cell].Start(grow, CellDuration, ArcEasings.InOut);
                _opacity[cell].Start(opacity, CellDuration, ArcEasings.InOut);
            }
            else
            {
                _grow[cell].Jump(grow);
                _opacity[cell].Jump(opacity);
            }
        }
        if (IsMoving)
        {
            _loop.Start();
        }
        InvalidateVisual();
    }

    // Cells 0–3 are the layers 4…1 below the baseline, cells 4–7 the layers 1…4 above it.
    private static int Layer(int cell) => cell < 4 ? 4 - cell : cell - 3;

    private bool Advance(double dt)
    {
        var full = ArcMotion.IsFull(this);
        var moving = false;
        for (var i = 0; i < Cells; i++)
        {
            if (full)
            {
                moving |= _grow[i].Advance(dt);
                moving |= _opacity[i].Advance(dt);
            }
            else
            {
                _grow[i].Jump(_grow[i].Target);
                _opacity[i].Jump(_opacity[i].Target);
            }
        }
        InvalidateVisual();
        return moving;
    }

    private void Invalidate()
    {
        _paletteVersion = -1;
        _text.Clear();
        InvalidateVisual();
    }

    private void EnsurePalette()
    {
        if (!this.IsAttachedToVisualTree() || (_paletteVersion >= 0 && _palette.IsReady))
        {
            return;
        }
        _palette.Refresh(this);
        _paletteVersion = _palette.Version;
    }

    private int CaptionLines(double width)
    {
        EnsurePalette();
        if (!_palette.IsReady || string.IsNullOrEmpty(_caption))
        {
            return string.IsNullOrEmpty(_caption) ? 0 : 2;
        }
        return Math.Max(1, _text.GetWrapped(_caption, new Typeface(_palette.BodyFont), 12, width, 8).Lines.Count);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = IsFilled && double.IsFinite(availableSize.Width) ? availableSize.Width : Math.Min(LegendWidth, availableSize.Width);
        if (!double.IsFinite(width))
        {
            width = LegendWidth;
        }
        var lines = CaptionLines(width);
        var height = RampHeight + LabelGap + TextLineHeight + (lines > 0 ? RowGap + lines * TextLineHeight : 0);
        return new Size(width, height);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        EnsurePalette();
        var width = Bounds.Width;
        if (!_palette.IsReady || width <= 0)
        {
            return;
        }
        for (var i = 0; i < Cells; i++)
        {
            _grows[i] = _grow[i].Value;
        }
        HorizonEngine.LegendWidths(width, _grows, _widths, 2 * ZeroMarkMargin + 1);
        var below = _belowColor ?? _palette.Series3;
        var above = _aboveColor ?? _palette.Series1;
        var x = 0.0;
        for (var cell = 0; cell < Cells; cell++)
        {
            if (cell == 4)
            {
                // The zero mark sits between the sides with a margin on each.
                x += ZeroMarkMargin;
                context.FillRectangle(_palette.Brush(_palette.Foreground, 0.5),
                    new Rect(x, RampHeight / 2 - ZeroMarkHeight / 2, 1, ZeroMarkHeight));
                x += 1 + ZeroMarkMargin;
            }
            var w = _widths[cell];
            if (w > 2 && _opacity[cell].Value > 0.001)
            {
                var rect = new Rect(x + 1, 0, w - 2, RampHeight);
                context.DrawRectangle(_palette.Brush(cell < 4 ? below : above, _opacity[cell].Value), null, new RoundedRect(rect, CellRadius));
            }
            x += w;
        }

        var body = new Typeface(_palette.BodyFont);
        var labelTop = RampHeight + LabelGap;
        var muted = _palette.Brush(_palette.TextMuted);
        var start = _text.Get(_start, body, 12, tabular: true);
        var zero = _text.Get(_zero, body, 12, tabular: true);
        var end = _text.Get(_end, body, 12, tabular: true);
        var columns = HorizonEngine.LegendColumns(width, start.Width, zero.Width, end.Width);
        var belowText = _text.Get(_below, body, 12, tabular: true);
        var aboveText = _text.Get(_above, body, 12, tabular: true);
        start.Draw(context, muted, new Point(0, labelTop + (TextLineHeight - start.Height) / 2));
        belowText.Draw(context, muted, new Point(columns.Left + (columns.Below - belowText.Width) / 2, labelTop + (TextLineHeight - belowText.Height) / 2));
        zero.Draw(context, _palette.Brush(_palette.TextSecondary), new Point(columns.Left + columns.Below, labelTop + (TextLineHeight - zero.Height) / 2));
        aboveText.Draw(context, muted,
            new Point(columns.Left + columns.Below + columns.Zero + (columns.Above - aboveText.Width) / 2, labelTop + (TextLineHeight - aboveText.Height) / 2));
        end.Draw(context, muted, new Point(width - end.Width, labelTop + (TextLineHeight - end.Height) / 2));

        if (!string.IsNullOrEmpty(_caption))
        {
            var caption = _text.GetWrapped(_caption, body, 12, width, 8);
            var top = labelTop + TextLineHeight + RowGap;
            var brush = _palette.Brush(_palette.TextSecondary);
            for (var line = 0; line < caption.Lines.Count; line++)
            {
                _one[0] = caption.Lines[line];
                AnalyticsText.DrawRuns(context, _one, brush, new Point(0, top + line * TextLineHeight + (TextLineHeight - _one[0].Height) / 2));
            }
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new HorizonLegendPeer(this);
}

/// <summary>The legend's peer: an image named by the block's description of the scale.</summary>
internal sealed class HorizonLegendPeer(HorizonLegend owner) : ControlAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;
}
