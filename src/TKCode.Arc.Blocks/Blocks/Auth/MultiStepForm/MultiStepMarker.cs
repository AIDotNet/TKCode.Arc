using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The 20 px round marker of a progress item: the step's number, or a check once the step is done. The swap pops: the
/// incoming glyph grows from 0.6 and fades in on a critically damped spring (fitted on Arc's preview: ω 20.1, ζ 1 =
/// <c>FromVisual(0.26, 0)</c>), the number also clearing a 2px blur; the outgoing glyph shrinks to 0.6 and fades out in
/// 120 ms (standard curve), without blur. The check then draws itself 38 ms later over 320 ms (enter curve). Colors are ink
/// properties set by the item's theme. Reduced motion and Off swap at once with the check fully drawn.
/// </summary>
/// <remarks>
/// Code-driven because the pop runs on a spring with a separate stroke draw and must reverse from where it is when a step is
/// undone mid-swap, which ControlTheme transitions cannot express.
/// </remarks>
internal sealed class MultiStepMarker : Control
{
    /// <summary>Defines the <see cref="Fill"/> property.</summary>
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<MultiStepMarker, IBrush?>(nameof(Fill));

    /// <summary>Defines the <see cref="Ring"/> property.</summary>
    public static readonly StyledProperty<IBrush?> RingProperty =
        AvaloniaProperty.Register<MultiStepMarker, IBrush?>(nameof(Ring));

    /// <summary>Defines the <see cref="DigitBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> DigitBrushProperty =
        AvaloniaProperty.Register<MultiStepMarker, IBrush?>(nameof(DigitBrush));

    /// <summary>Defines the <see cref="CheckBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> CheckBrushProperty =
        AvaloniaProperty.Register<MultiStepMarker, IBrush?>(nameof(CheckBrush));

    /// <summary>Defines the <see cref="DigitWeight"/> property.</summary>
    public static readonly StyledProperty<FontWeight> DigitWeightProperty =
        AvaloniaProperty.Register<MultiStepMarker, FontWeight>(nameof(DigitWeight), FontWeight.Normal);

    /// <summary>Defines the <see cref="Number"/> property.</summary>
    public static readonly StyledProperty<int> NumberProperty =
        AvaloniaProperty.Register<MultiStepMarker, int>(nameof(Number), 1);

    /// <summary>Defines the <see cref="IsChecked"/> property.</summary>
    public static readonly StyledProperty<bool> IsCheckedProperty =
        AvaloniaProperty.Register<MultiStepMarker, bool>(nameof(IsChecked));

    /// <summary>The pop spring (fitted: ω 20.1 rad/s, critically damped).</summary>
    internal static readonly SpringSpec PopSpring = SpringSpec.FromVisual(0.26, 0);

    /// <summary>The glyphs start from (and leave to) this scale.</summary>
    internal const double PopScale = 0.6;

    /// <summary>The number enters from this blur.</summary>
    internal const double DigitBlur = 2;

    /// <summary>The check's stroke starts this long after the pop (measured 38 ms) and takes 320 ms.</summary>
    internal static readonly TimeSpan DrawDelay = TimeSpan.FromMilliseconds(38);
    internal static readonly TimeSpan DrawDuration = TimeSpan.FromMilliseconds(320);

    private static readonly FontFeatureCollection s_tabular = new() { FontFeature.Parse("tnum") };

    private readonly TextBlock _digit;
    private readonly MultiStepCheck _check;
    private readonly ScaleTransform _digitScale = new();
    private readonly ScaleTransform _checkScale = new();
    private readonly MotionValue<double> _digitPresence;
    private readonly MotionValue<double> _checkPresence;
    private readonly MotionValue<double> _draw;
    private bool _digitEntering;
    private Pen? _ringPen;
    private IBrush? _ringBrush;

    static MultiStepMarker()
    {
        AffectsRender<MultiStepMarker>(FillProperty, RingProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<MultiStepMarker>(false);
    }

    public MultiStepMarker()
    {
        _digit = new TextBlock
        {
            Text = "1",
            FontSize = 12,
            LineHeight = 16.8,
            FontFeatures = s_tabular,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            RenderTransform = _digitScale
        };
        _check = new MultiStepCheck
        {
            Width = 11,
            Height = 11,
            StrokeThickness = 2.6,
            Opacity = 0,
            Progress = 0,
            RenderTransform = _checkScale
        };
        LogicalChildren.Add(_digit);
        VisualChildren.Add(_digit);
        LogicalChildren.Add(_check);
        VisualChildren.Add(_check);
        _digitPresence = new MotionValue<double>(this, 1, MotionVectors.Double);
        _digitPresence.Changed += p =>
        {
            _digit.Opacity = Math.Clamp(p, 0, 1);
            _digitScale.ScaleX = _digitScale.ScaleY = PopScale + (1 - PopScale) * p;
            _digit.Effect = _digitEntering ? ArcBlur.Get(DigitBlur * (1 - p)) : null;
        };
        _checkPresence = new MotionValue<double>(this, 0, MotionVectors.Double);
        _checkPresence.Changed += p =>
        {
            _check.Opacity = Math.Clamp(p, 0, 1);
            _checkScale.ScaleX = _checkScale.ScaleY = PopScale + (1 - PopScale) * p;
        };
        _draw = new MotionValue<double>(this, 0, MotionVectors.Double);
        _draw.Changed += p => _check.Progress = p;
        SetGlyphs(false);
    }

    /// <summary>The circle's fill.</summary>
    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }

    /// <summary>The circle's 1px ring.</summary>
    public IBrush? Ring { get => GetValue(RingProperty); set => SetValue(RingProperty, value); }

    /// <summary>The number's color.</summary>
    public IBrush? DigitBrush { get => GetValue(DigitBrushProperty); set => SetValue(DigitBrushProperty, value); }

    /// <summary>The check's color.</summary>
    public IBrush? CheckBrush { get => GetValue(CheckBrushProperty); set => SetValue(CheckBrushProperty, value); }

    /// <summary>The number's weight (medium on the current step).</summary>
    public FontWeight DigitWeight { get => GetValue(DigitWeightProperty); set => SetValue(DigitWeightProperty, value); }

    /// <summary>The one-based step number shown until the step is done.</summary>
    public int Number { get => GetValue(NumberProperty); set => SetValue(NumberProperty, value); }

    /// <summary>True shows the check instead of the number.</summary>
    public bool IsChecked { get => GetValue(IsCheckedProperty); set => SetValue(IsCheckedProperty, value); }

    /// <summary>The number's text block (tests read it).</summary>
    internal TextBlock Digit => _digit;

    /// <summary>The check (tests read it).</summary>
    internal MultiStepCheck Check => _check;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == NumberProperty)
        {
            _digit.Text = Number.ToString(System.Globalization.CultureInfo.CurrentCulture);
        }
        else if (change.Property == DigitBrushProperty)
        {
            _digit.Foreground = DigitBrush;
        }
        else if (change.Property == DigitWeightProperty)
        {
            _digit.FontWeight = DigitWeight;
        }
        else if (change.Property == CheckBrushProperty)
        {
            _check.Foreground = CheckBrush;
        }
        else if (change.Property == IsCheckedProperty)
        {
            var animate = IsLoaded && IsEffectivelyVisible && ArcMotion.IsFull(this);
            if (animate)
            {
                Pop(IsChecked);
            }
            else
            {
                SetGlyphs(IsChecked);
            }
        }
    }

    private void SetGlyphs(bool isChecked)
    {
        _digitEntering = false;
        _digitPresence.Jump(isChecked ? 0 : 1);
        _checkPresence.Jump(isChecked ? 1 : 0);
        _draw.Jump(isChecked ? 1 : 0);
    }

    private void Pop(bool isChecked)
    {
        var exit = MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard);
        var enter = MotionSpec.Spring(PopSpring);
        if (isChecked)
        {
            _digitEntering = false;
            _ = _digitPresence.AnimateTo(0, exit);
            // A check that is (nearly) gone draws again from its start; one caught mid-exit keeps its stroke.
            if (_checkPresence.Value < 0.05)
            {
                _draw.Jump(0);
            }
            _ = _checkPresence.AnimateTo(1, enter);
            _ = _draw.AnimateTo(1, MotionSpec.Tween(DrawDuration, ArcEasings.Enter, DrawDelay));
        }
        else
        {
            _ = _checkPresence.AnimateTo(0, exit);
            _digitEntering = true;
            _ = _digitPresence.AnimateTo(1, enter);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _digit.Measure(availableSize);
        _check.Measure(availableSize);
        return new Size(Math.Max(_digit.DesiredSize.Width, 11), Math.Max(_digit.DesiredSize.Height, 11));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var digit = _digit.DesiredSize;
        _digit.Arrange(new Rect((finalSize.Width - digit.Width) / 2, (finalSize.Height - digit.Height) / 2, digit.Width, digit.Height));
        _check.Arrange(new Rect((finalSize.Width - 11) / 2, (finalSize.Height - 11) / 2, 11, 11));
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 0)
        {
            return;
        }
        var ring = Ring;
        if (ring is not null && (_ringPen is null || !ReferenceEquals(_ringBrush, ring)))
        {
            _ringPen = new Pen(ring, 1);
            _ringBrush = ring;
        }
        var radius = (size - 1) / 2;
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        context.DrawEllipse(Fill, ring is null ? null : _ringPen, center, radius, radius);
    }
}
