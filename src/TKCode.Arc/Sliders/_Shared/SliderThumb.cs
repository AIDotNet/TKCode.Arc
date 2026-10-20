using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>What a <see cref="SliderThumb"/> asks its slider (values for assistive technology).</summary>
internal interface ISliderThumbHost
{
    double GetThumbMinimum(int index);
    double GetThumbMaximum(int index);
    double GetThumbValue(int index);
    double SmallStep { get; }
    double LargeStep { get; }
    bool IsThumbReadOnly { get; }
    void SetThumbValue(int index, double value);
}

/// <summary>
/// One focusable thumb (role slider). It draws itself (fill, the three-layer thumb shadow and the focus ring) and is
/// placed with a render transform only, so dragging never relayouts. Pointer input is handled by the rail.
/// </summary>
internal sealed class SliderThumb : Control
{
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<SliderThumb, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<BoxShadows> ShadowProperty =
        AvaloniaProperty.Register<SliderThumb, BoxShadows>(nameof(Shadow));

    public static readonly StyledProperty<IBrush?> RingBrushProperty =
        AvaloniaProperty.Register<SliderThumb, IBrush?>(nameof(RingBrush));

    public static readonly StyledProperty<double> DiameterProperty =
        AvaloniaProperty.Register<SliderThumb, double>(nameof(Diameter), 22);

    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }
    public BoxShadows Shadow { get => GetValue(ShadowProperty); set => SetValue(ShadowProperty, value); }
    public IBrush? RingBrush { get => GetValue(RingBrushProperty); set => SetValue(RingBrushProperty, value); }
    public double Diameter { get => GetValue(DiameterProperty); set => SetValue(DiameterProperty, value); }

    private readonly MatrixTransform _transform = new();
    private Pen? _ringPen;
    private bool _showRing;

    static SliderThumb()
    {
        FocusableProperty.OverrideDefaultValue<SliderThumb>(true);
        IsHitTestVisibleProperty.OverrideDefaultValue<SliderThumb>(false);
        AffectsMeasure<SliderThumb>(DiameterProperty);
        AffectsRender<SliderThumb>(FillProperty, ShadowProperty, RingBrushProperty);
    }

    public SliderThumb(ISliderThumbHost host, int index)
    {
        Host = host;
        Index = index;
        RenderTransform = _transform;
        RenderTransformOrigin = RelativePoint.Center;
    }

    public ISliderThumbHost Host { get; }

    public int Index { get; }

    /// <summary>Shows the ring after a key press even when focus came from the pointer (Arc drops "quiet" on keys).</summary>
    public bool ShowRing
    {
        get => _showRing;
        set
        {
            if (_showRing != value)
            {
                _showRing = value;
                InvalidateVisual();
            }
        }
    }

    /// <summary>Moves the thumb by <paramref name="x"/> from its layout slot and scales it about its center.</summary>
    public void Place(double x, double scale)
    {
        var matrix = Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(x, 0);
        if (_transform.Matrix != matrix)
        {
            _transform.Matrix = matrix;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RingBrushProperty)
        {
            _ringPen = null;
        }
        else if (change.Property == IsFocusedProperty)
        {
            if (!IsFocused)
            {
                _showRing = false;
            }
            InvalidateVisual();
        }
    }

    protected override Size MeasureOverride(Size availableSize) => new(Diameter, Diameter);

    public override void Render(DrawingContext context)
    {
        var d = Diameter;
        var rect = new Rect(0, 0, d, d);
        context.DrawRectangle(Fill, null, new RoundedRect(rect, d / 2), Shadow);
        if (IsFocused && (_showRing || Classes.Contains(":focus-visible")) && RingBrush is { } ring)
        {
            // 2px ring 2px outside the thumb.
            _ringPen ??= new Pen(ring, 2);
            context.DrawEllipse(null, _ringPen, rect.Center, d / 2 + 3, d / 2 + 3);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new SliderThumbAutomationPeer(this);
}

/// <summary>Each thumb is a Slider control with the range value pattern (Arc: role="slider" with valuemin/max/now).</summary>
internal sealed class SliderThumbAutomationPeer : ControlAutomationPeer, IRangeValueProvider
{
    public SliderThumbAutomationPeer(SliderThumb owner) : base(owner)
    {
    }

    private SliderThumb Thumb => (SliderThumb)Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Slider;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;

    public bool IsReadOnly => Thumb.Host.IsThumbReadOnly;

    public double Minimum => Thumb.Host.GetThumbMinimum(Thumb.Index);

    public double Maximum => Thumb.Host.GetThumbMaximum(Thumb.Index);

    public double Value => Thumb.Host.GetThumbValue(Thumb.Index);

    public double LargeChange => Thumb.Host.LargeStep;

    public double SmallChange => Thumb.Host.SmallStep;

    public void SetValue(double value) => Thumb.Host.SetThumbValue(Thumb.Index, value);
}
