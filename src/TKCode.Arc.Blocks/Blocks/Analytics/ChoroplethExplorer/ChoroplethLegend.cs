using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// The legend of a <see cref="ChoroplethExplorer"/>: one focusable step per bin in a 420-wide ramp (the column's width in the
/// compact layout), the bin edges as ticks between the steps, and a no-data step to the right when some subregions have no
/// value. Pointing at a step lights its regions; pressing it pins them (notes 3.5 and 4.5).
/// </summary>
/// <remarks>
/// Why code-driven: each step's colour, opacity and height are tweens advanced by one frame loop (480ms colour, the 580ms
/// CSS spring curve for the 1.6× height), drawn by the step itself; the ramp is rebuilt when the number of bins changes.
/// </remarks>
internal sealed class ChoroplethLegend : Control
{
    internal const double StepHeight = 28;
    internal const double TickHeight = 16;
    internal const double NoDataGap = 16;

    private readonly AnalyticsPalette _palette = new();
    private readonly AnalyticsTextCache _text = new(64);
    private readonly List<ChoroplethLegendStep> _steps = new();
    private readonly ChoroplethLegendStep _noData;
    private readonly LiveLoop _loop;
    private string[] _ticks = [];
    private int? _hovered;

    public ChoroplethLegend()
    {
        _noData = new ChoroplethLegendStep(this, ChoroplethExplorer.NoDataBinIndex) { IsVisible = false };
        Attach(_noData);
        _loop = new LiveLoop(this, Advance) { MinimumLevel = ArcMotionLevel.Off };
        ActualThemeVariantChanged += (_, _) => RefreshTheme();
    }

    /// <summary>The pointer moved onto a step (null: off every step).</summary>
    public event Action<int?>? StepHovered;

    /// <summary>A step was pressed (click, Enter or Space).</summary>
    public event Action<int>? StepToggled;

    /// <summary>The ramp is as wide as the legend (compact layout) instead of <see cref="RampWidth"/>.</summary>
    public bool FillsWidth { get; set; }

    /// <summary>The ramp's width when it does not fill the legend (420).</summary>
    public static readonly StyledProperty<double> RampWidthProperty =
        AvaloniaProperty.Register<ChoroplethLegend, double>(nameof(RampWidth), 420);

    static ChoroplethLegend()
    {
        AffectsMeasure<ChoroplethLegend>(RampWidthProperty);
    }

    /// <summary>The ramp's width when it does not fill the legend (420).</summary>
    public double RampWidth { get => GetValue(RampWidthProperty); set => SetValue(RampWidthProperty, value); }

    internal AnalyticsPalette Palette => _palette;

    internal AnalyticsTextCache Text => _text;

    /// <summary>The bin steps (tests).</summary>
    internal IReadOnlyList<ChoroplethLegendStep> Steps => _steps;

    /// <summary>The no-data step (tests).</summary>
    internal ChoroplethLegendStep NoDataStep => _noData;

    /// <summary>The tick texts between the steps (tests).</summary>
    internal IReadOnlyList<string> Ticks => _ticks;

    internal bool Animates => IsLoaded && ArcMotion.IsFull(this);

    /// <summary>Shows a ramp of <paramref name="colors"/> (each with its composite opacity), the ticks between them and the step names.</summary>
    public void SetBins(IReadOnlyList<Color> colors, IReadOnlyList<double> opacities, IReadOnlyList<string> ticks,
        IReadOnlyList<string> names, bool animate)
    {
        var rebuild = colors.Count != _steps.Count;
        if (rebuild)
        {
            foreach (var step in _steps)
            {
                Detach(step);
            }
            _steps.Clear();
            for (var i = 0; i < colors.Count; i++)
            {
                var step = new ChoroplethLegendStep(this, i);
                _steps.Add(step);
                Attach(step);
            }
        }
        for (var i = 0; i < _steps.Count; i++)
        {
            var step = _steps[i];
            step.Position = _steps.Count == 1 ? 3 : i == 0 ? 0 : i == _steps.Count - 1 ? 2 : 1;
            step.SetColor(colors[i], opacities[i], animate && !rebuild && Animates);
            AutomationProperties.SetName(step, i < names.Count ? names[i] : string.Empty);
        }
        _ticks = ticks.ToArray();
        InvalidateMeasure();
        InvalidateVisual();
        _loop.Start();
    }

    /// <summary>Shows or hides the no-data step.</summary>
    public void SetNoData(bool visible, Color color, string text, string name)
    {
        _noData.IsVisible = visible;
        _noData.Label = text;
        _noData.SetColor(color, 1, false);
        AutomationProperties.SetName(_noData, name);
        InvalidateMeasure();
    }

    /// <summary>Marks the lit step (hovered or pinned: it grows to 1.6× its height) and the pinned one (pressed).</summary>
    public void SetState(int? active, int? pinned)
    {
        var animate = Animates;
        foreach (var step in _steps)
        {
            step.SetActive(step.Bin == active, animate);
            step.IsChecked = step.Bin == pinned;
        }
        _noData.SetActive(active == ChoroplethExplorer.NoDataBinIndex, animate);
        _noData.IsChecked = pinned == ChoroplethExplorer.NoDataBinIndex;
        _loop.Start();
    }

    /// <summary>Focuses the first step (tests and keyboard hand-off).</summary>
    internal ChoroplethLegendStep? FirstStep => _steps.Count > 0 ? _steps[0] : null;

    internal void OnStepPointer(ChoroplethLegendStep step, bool entered)
    {
        int? bin = entered ? step.Bin : null;
        if (bin != _hovered)
        {
            _hovered = bin;
            StepHovered?.Invoke(bin);
        }
    }

    internal void OnStepClicked(ChoroplethLegendStep step) => StepToggled?.Invoke(step.Bin);

    internal void Kick() => _loop.Start();

    private void Attach(Control child)
    {
        VisualChildren.Add(child);
        LogicalChildren.Add(child);
    }

    private void Detach(Control child)
    {
        VisualChildren.Remove(child);
        LogicalChildren.Remove(child);
    }

    private bool Advance(double dt)
    {
        var moving = false;
        foreach (var step in _steps)
        {
            moving |= step.Advance(dt);
        }
        moving |= _noData.Advance(dt);
        return moving;
    }

    private double EffectiveRamp(double available) =>
        FillsWidth && double.IsFinite(available) ? available : Math.Min(RampWidth, double.IsFinite(available) ? available : RampWidth);

    protected override Size MeasureOverride(Size availableSize)
    {
        var ramp = EffectiveRamp(availableSize.Width);
        var stepWidth = _steps.Count > 0 ? ramp / _steps.Count : 0;
        foreach (var step in _steps)
        {
            step.Measure(new Size(stepWidth, StepHeight));
        }
        _noData.Measure(new Size(double.PositiveInfinity, StepHeight));
        var width = ramp + (_noData.IsVisible ? NoDataGap + _noData.DesiredSize.Width : 0);
        return new Size(double.IsFinite(availableSize.Width) ? Math.Min(width, availableSize.Width) : width, StepHeight + TickHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var ramp = EffectiveRamp(finalSize.Width);
        var stepWidth = _steps.Count > 0 ? ramp / _steps.Count : 0;
        for (var i = 0; i < _steps.Count; i++)
        {
            _steps[i].Arrange(new Rect(i * stepWidth, 0, stepWidth, StepHeight));
        }
        _noData.Arrange(new Rect(ramp + NoDataGap, 0, _noData.DesiredSize.Width, StepHeight));
        return finalSize;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ResourcesChanged += OnResourcesChanged;
        RefreshTheme();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ResourcesChanged -= OnResourcesChanged;
    }

    private void OnResourcesChanged(object? sender, ResourcesChangedEventArgs e) => RefreshTheme();

    private void RefreshTheme()
    {
        if (!this.IsAttachedToVisualTree())
        {
            return;
        }
        _palette.Refresh(this);
        _text.Clear();
        InvalidateVisual();
        foreach (var step in _steps)
        {
            step.InvalidateVisual();
        }
        _noData.InvalidateMeasure();
        _noData.InvalidateVisual();
    }

    internal void EnsurePalette()
    {
        if (!_palette.IsReady && this.IsAttachedToVisualTree())
        {
            _palette.Refresh(this);
        }
    }

    public override void Render(DrawingContext context)
    {
        EnsurePalette();
        if (!_palette.IsReady || _steps.Count == 0)
        {
            return;
        }
        var ramp = EffectiveRamp(Bounds.Width);
        var brush = _palette.Brush(_palette.TextMuted);
        var face = new Typeface(_palette.BodyFont);
        for (var i = 0; i < _ticks.Length && i < _steps.Count - 1; i++)
        {
            var text = _text.Get(_ticks[i], face, 12, true);
            var x = ramp * (i + 1) / _steps.Count - text.Width / 2;
            text.Draw(context, brush, new Point(x, StepHeight + (TickHeight - text.Height) / 2));
        }
    }
}

/// <summary>
/// One legend step: a 10px swatch (the bin's colour at its opacity over the surface) with rounded outer ends; a no-data step
/// is an 18 × 10 pill and its label. It is a toggle button for the keyboard and screen readers: pressed while pinned.
/// </summary>
internal sealed class ChoroplethLegendStep : ToggleButton
{
    private static readonly TimeSpan ColorDuration = ArcMotionPresets.Considered;

    private readonly ChoroplethLegend _owner;
    private Color _from;
    private Color _to;
    private AnalyticsTween _colorProgress = new(1);
    private AnalyticsTween _opacity = new(1);
    private AnalyticsTween _scale = new(1);
    private bool _hasColor;
    private string _label = string.Empty;

    public ChoroplethLegendStep(ChoroplethLegend owner, int bin)
    {
        _owner = owner;
        Bin = bin;
        Focusable = true;
        FocusAdorner = null;
        Cursor = new Cursor(StandardCursorType.Hand);
        ClipToBounds = false;
    }

    protected override Type StyleKeyOverride => typeof(ChoroplethLegendStep);

    /// <summary>The bin (or <see cref="ChoroplethExplorer.NoDataBinIndex"/>).</summary>
    public int Bin { get; }

    /// <summary>0 first, 1 middle, 2 last, 3 alone: picks the swatch's corner radii.</summary>
    public int Position { get; set; }

    /// <summary>The no-data step's text.</summary>
    public string Label
    {
        get => _label;
        set
        {
            if (_label != value)
            {
                _label = value;
                InvalidateMeasure();
                InvalidateVisual();
            }
        }
    }

    /// <summary>The swatch's current height factor (tests).</summary>
    internal double ScaleY => _scale.Value;

    /// <summary>The swatch's colour and opacity (tests).</summary>
    internal (Color Color, double Opacity) Swatch => (_to, _opacity.Target);

    private bool IsNoData => Bin == ChoroplethExplorer.NoDataBinIndex;

    public void SetColor(Color color, double opacity, bool animate)
    {
        if (!_hasColor || !animate)
        {
            _hasColor = true;
            _from = _to = color;
            _colorProgress.Jump(1);
            _opacity.Jump(opacity);
            InvalidateVisual();
            return;
        }
        if (color != _to)
        {
            _from = Current;
            _to = color;
            _colorProgress.Jump(0);
            _colorProgress.Start(1, ColorDuration, AnalyticsEasings.Standard);
        }
        _opacity.Start(opacity, ColorDuration, AnalyticsEasings.Standard);
        InvalidateVisual();
    }

    public void SetActive(bool active, bool animate)
    {
        var target = active ? 1.6 : 1;
        if (!animate)
        {
            _scale.Jump(target);
            InvalidateVisual();
        }
        else if (Math.Abs(_scale.Target - target) > 1e-6)
        {
            _scale.Start(target, ArcMotionPresets.CssSpring, AnalyticsEasings.CssSpring);
        }
    }

    public bool Advance(double dt)
    {
        var moving = _colorProgress.Advance(dt) | _opacity.Advance(dt) | _scale.Advance(dt);
        if (moving)
        {
            InvalidateVisual();
        }
        return moving;
    }

    private Color Current
    {
        get
        {
            var t = Math.Clamp(_colorProgress.Value, 0, 1);
            return t >= 1 ? _to : Color.FromArgb(
                (byte)Math.Round(_from.A + (_to.A - _from.A) * t),
                (byte)Math.Round(_from.R + (_to.R - _from.R) * t),
                (byte)Math.Round(_from.G + (_to.G - _from.G) * t),
                (byte)Math.Round(_from.B + (_to.B - _from.B) * t));
        }
    }

    // The block owns the pinned state: pressing reports a click and the block sets IsChecked.
    protected override void Toggle()
    {
    }

    protected override void OnClick()
    {
        base.OnClick();
        _owner.OnStepClicked(this);
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type != PointerType.Touch)
        {
            _owner.OnStepPointer(this, true);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _owner.OnStepPointer(this, false);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (!IsNoData)
        {
            return new Size(double.IsFinite(availableSize.Width) ? availableSize.Width : 84, ChoroplethLegend.StepHeight);
        }
        _owner.EnsurePalette();
        var width = 18 + 8 + (_owner.Palette.IsReady ? _owner.Text.Get(_label, new Typeface(_owner.Palette.BodyFont), 12).Width : _label.Length * 6);
        return new Size(Math.Ceiling(width), ChoroplethLegend.StepHeight);
    }

    protected override Size ArrangeOverride(Size finalSize) => finalSize;

    public override void Render(DrawingContext context)
    {
        _owner.EnsurePalette();
        var palette = _owner.Palette;
        if (!palette.IsReady)
        {
            return;
        }
        var size = Bounds.Size;
        context.FillRectangle(Brushes.Transparent, new Rect(size));
        var top = (size.Height - 10) / 2;
        Rect swatch;
        CornerRadius radius;
        if (IsNoData)
        {
            swatch = new Rect(0, top, 18, 10);
            radius = new CornerRadius(5);
        }
        else
        {
            swatch = new Rect(1, top, Math.Max(0, size.Width - 2), 10);
            radius = Position switch
            {
                0 => new CornerRadius(5, 2, 2, 5),
                2 => new CornerRadius(2, 5, 5, 2),
                3 => new CornerRadius(5),
                _ => new CornerRadius(2),
            };
        }
        var scale = _scale.Value;
        var center = swatch.Center;
        using (context.PushTransform(Matrix.CreateTranslation(-center.X, -center.Y) * Matrix.CreateScale(1, scale) * Matrix.CreateTranslation(center.X, center.Y)))
        {
            var shape = new RoundedRect(swatch, radius);
            context.DrawRectangle(palette.Brush(palette.Surface), null, shape);
            context.DrawRectangle(palette.Brush(Current, Math.Clamp(_opacity.Value, 0, 1)), null, shape);
        }
        if (IsNoData)
        {
            var text = _owner.Text.Get(_label, new Typeface(palette.BodyFont), 12);
            text.Draw(context, palette.Brush(palette.TextSecondary), new Point(26, (size.Height - text.Height) / 2));
        }
        if (AnalyticsFocus.IsVisible(this))
        {
            AnalyticsFocus.Draw(context, palette, new Rect(size), 6);
        }
    }
}
