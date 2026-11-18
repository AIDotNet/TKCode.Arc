using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// One step dot of a <see cref="CodeMagicMove"/>: a real button (named "Step n: title") drawn as a small round pip that
/// stretches into a pill for the current step. The current pill fills with the accent as autoplay rests on the step, and
/// is full when autoplay is off.
/// </summary>
/// <remarks>Drawn: the pip's width follows Arc's spring curve (440ms) through a <see cref="MotionValue{T}"/>.</remarks>
[PseudoClasses(CurrentPseudoClass)]
internal sealed class CodeMagicMoveDot : AvaloniaButton
{
    public const string CurrentPseudoClass = ":current";

    private static readonly TimeSpan WidthDuration = TimeSpan.FromMilliseconds(440);

    public static readonly StyledProperty<bool> IsCurrentProperty =
        AvaloniaProperty.Register<CodeMagicMoveDot, bool>(nameof(IsCurrent));

    public static readonly StyledProperty<double> FillProperty =
        AvaloniaProperty.Register<CodeMagicMoveDot, double>(nameof(Fill), 1);

    public static readonly StyledProperty<IBrush?> PipBrushProperty =
        AvaloniaProperty.Register<CodeMagicMoveDot, IBrush?>(nameof(PipBrush));

    public static readonly StyledProperty<IBrush?> FillBrushProperty =
        AvaloniaProperty.Register<CodeMagicMoveDot, IBrush?>(nameof(FillBrush));

    public static readonly StyledProperty<IBrush?> FocusRingBrushProperty =
        AvaloniaProperty.Register<CodeMagicMoveDot, IBrush?>(nameof(FocusRingBrush));

    public static readonly StyledProperty<double> PipSizeProperty =
        AvaloniaProperty.Register<CodeMagicMoveDot, double>(nameof(PipSize), 6);

    public static readonly StyledProperty<double> ActivePipWidthProperty =
        AvaloniaProperty.Register<CodeMagicMoveDot, double>(nameof(ActivePipWidth), 24);

    private MotionValue<double>? _width;
    private double _shownWidth = -1;
    private ImmutablePen? _ringPen;
    private IBrush? _ringBrush;

    static CodeMagicMoveDot()
    {
        AffectsRender<CodeMagicMoveDot>(FillProperty, PipBrushProperty, FillBrushProperty, FocusRingBrushProperty);
        AffectsMeasure<CodeMagicMoveDot>(PipSizeProperty, ActivePipWidthProperty);
        ClipToBoundsProperty.OverrideDefaultValue<CodeMagicMoveDot>(false);
    }

    public bool IsCurrent { get => GetValue(IsCurrentProperty); set => SetValue(IsCurrentProperty, value); }

    /// <summary>How much of the current pill is filled (0–1).</summary>
    public double Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }

    public IBrush? PipBrush { get => GetValue(PipBrushProperty); set => SetValue(PipBrushProperty, value); }

    public IBrush? FillBrush { get => GetValue(FillBrushProperty); set => SetValue(FillBrushProperty, value); }

    public IBrush? FocusRingBrush { get => GetValue(FocusRingBrushProperty); set => SetValue(FocusRingBrushProperty, value); }

    public double PipSize { get => GetValue(PipSizeProperty); set => SetValue(PipSizeProperty, value); }

    public double ActivePipWidth { get => GetValue(ActivePipWidthProperty); set => SetValue(ActivePipWidthProperty, value); }

    /// <summary>The pip's width as drawn now.</summary>
    public double PipWidth => _shownWidth >= 0 ? _shownWidth : TargetWidth;

    private double TargetWidth => IsCurrent ? ActivePipWidth : PipSize;

    protected override Type StyleKeyOverride => typeof(CodeMagicMoveDot);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsCurrentProperty)
        {
            PseudoClasses.Set(CurrentPseudoClass, IsCurrent);
            AnimateWidth();
        }
        else if (change.Property == PipSizeProperty || change.Property == ActivePipWidthProperty)
        {
            _width?.Jump(TargetWidth);
            _shownWidth = TargetWidth;
            InvalidateMeasure();
        }
    }

    private void AnimateWidth()
    {
        var target = TargetWidth;
        if (_shownWidth < 0 || !ArcMotion.IsFull(this))
        {
            _width?.Jump(target);
            _shownWidth = target;
            InvalidateMeasure();
            InvalidateVisual();
            return;
        }
        if (_width is null)
        {
            _width = new MotionValue<double>(this, _shownWidth, MotionVectors.Double);
            _width.Changed += value =>
            {
                _shownWidth = value;
                InvalidateMeasure();
                InvalidateVisual();
            };
        }
        _ = _width.AnimateTo(target, MotionSpec.Tween(WidthDuration, ArcEasings.CssSpring));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        base.MeasureOverride(availableSize);
        if (_shownWidth < 0)
        {
            _shownWidth = TargetWidth;
        }
        var width = Math.Max(MinWidth, PipWidth + Padding.Left + Padding.Right);
        return new Size(width, double.IsNaN(Height) ? PipSize : Height);
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        context.FillRectangle(Brushes.Transparent, bounds);
        var width = PipWidth;
        var size = PipSize;
        var pip = new Rect((bounds.Width - width) / 2, (bounds.Height - size) / 2, width, size);
        var radius = size / 2;
        if (PipBrush is { } pipBrush)
        {
            context.DrawRectangle(pipBrush, null, pip, radius, radius);
        }
        if (IsCurrent && FillBrush is { } fill && Fill > 0)
        {
            using (context.PushClip(new RoundedRect(pip, radius)))
            {
                context.DrawRectangle(fill, null, pip.WithWidth(pip.Width * Math.Clamp(Fill, 0, 1)), radius, radius);
            }
        }
        if (IsFocused && Classes.Contains(":focus-visible") && FocusRingBrush is { } ring)
        {
            if (_ringPen is null || !ReferenceEquals(_ringBrush, ring))
            {
                _ringBrush = ring;
                _ringPen = new ImmutablePen(ring.ToImmutable(), 2);
            }
            var r = pip.Inflate(new Thickness(4, 4));
            context.DrawRectangle(null, _ringPen, r, r.Height / 2, r.Height / 2);
        }
    }
}
