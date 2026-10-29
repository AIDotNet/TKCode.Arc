using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A text-only button of <see cref="LoginSplit"/>: "Sign up" in the header, Terms and Privacy in the footer, "Forgot
/// password?" and the resend button. <see cref="AuthInertButton.IsInert"/> is Arc's <c>aria-disabled</c>: the button keeps
/// focus and its place in the tab order but ignores activation and shows no hover. Styled by its class in the block's theme.
/// </summary>
internal class LoginSplitTextButton : AuthInertButton
{
    protected override Type StyleKeyOverride => typeof(LoginSplitTextButton);
}

/// <summary>
/// The account chip of the password step: a 28px avatar, the email and a chevron in a pill. Pressing it goes back to the
/// email step. Hover fills it with the muted surface; a press scales it to .97 on Arc's CSS spring.
/// </summary>
internal sealed class LoginSplitChip : AvaloniaButton
{
    public static readonly StyledProperty<string?> DisplayNameProperty =
        AvaloniaProperty.Register<LoginSplitChip, string?>(nameof(DisplayName));

    public static readonly StyledProperty<string?> EmailProperty =
        AvaloniaProperty.Register<LoginSplitChip, string?>(nameof(Email));

    public static readonly StyledProperty<ImageSource?> PictureProperty =
        AvaloniaProperty.Register<LoginSplitChip, ImageSource?>(nameof(Picture));

    /// <summary>The account's name (the avatar's initials).</summary>
    public string? DisplayName { get => GetValue(DisplayNameProperty); set => SetValue(DisplayNameProperty, value); }

    /// <summary>The account's email.</summary>
    public string? Email { get => GetValue(EmailProperty); set => SetValue(EmailProperty, value); }

    /// <summary>The account's photo, if any.</summary>
    public ImageSource? Picture { get => GetValue(PictureProperty); set => SetValue(PictureProperty, value); }

    protected override Type StyleKeyOverride => typeof(LoginSplitChip);
}

/// <summary>
/// A row that opens and closes in place: the height follows the content on Arc's smooth spring while the content fades
/// (in ~200 ms on the enter curve, out in 160 ms). Used for the reset notice, the progress line and the Sign out row. The
/// first state shows without motion; a closed row hides its content (no focus, no hit testing).
/// </summary>
/// <remarks>Code-driven: the height is measured from the content and must keep its velocity when the row flips mid-way.</remarks>
internal sealed class LoginSplitCollapse : Decorator
{
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<LoginSplitCollapse, bool>(nameof(IsOpen), true);

    private static readonly Avalonia.Animation.Easings.Easing s_easeOut = new Avalonia.Animation.Easings.SplineEasing(0, 0, 0.58, 1);
    private MotionValue<double>? _reveal;
    private MotionValue<double>? _fade;
    private double _fraction = 1;
    private int _run;

    /// <summary>Whether the row is open.</summary>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <summary>The share of the content's height shown (tests read it).</summary>
    internal double Fraction => _fraction;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty)
        {
            Run(change.GetNewValue<bool>());
        }
        else if (change.Property == ChildProperty && change.NewValue is Control child)
        {
            child.IsVisible = IsOpen;
            _fraction = IsOpen ? 1 : 0;
        }
    }

    private async void Run(bool open)
    {
        var run = ++_run;
        var child = Child;
        if (child is null)
        {
            _fraction = open ? 1 : 0;
            InvalidateMeasure();
            return;
        }
        if (!IsLoaded || ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off)
        {
            _reveal?.Stop();
            _fade?.Stop();
            _fraction = open ? 1 : 0;
            child.Opacity = 1;
            child.IsVisible = open;
            ClipToBounds = false;
            InvalidateMeasure();
            return;
        }
        child.IsVisible = true;
        child.IsHitTestVisible = open;
        if (_reveal is null)
        {
            _reveal = new MotionValue<double>(this, _fraction, MotionVectors.Double);
            _reveal.Changed += value =>
            {
                _fraction = Math.Max(0, value);
                ClipToBounds = _fraction < 0.999;
                InvalidateMeasure();
            };
            _fade = new MotionValue<double>(this, child.Opacity, MotionVectors.Double);
            _fade.Changed += value =>
            {
                if (Child is { } c)
                {
                    c.Opacity = value;
                }
            };
        }
        _fade!.Jump(child.Opacity);
        _ = _fade.AnimateTo(open ? 1 : 0, open
            ? MotionSpec.Tween(TimeSpan.FromMilliseconds(200), ArcEasings.Enter)
            : MotionSpec.Tween(ArcMotionPresets.Fast, s_easeOut));
        await _reveal.AnimateTo(open ? 1 : 0, MotionSpec.Spring(LoginSplitMotion.Smooth));
        if (run != _run)
        {
            return;
        }
        _fraction = open ? 1 : 0;
        ClipToBounds = !open;
        if (Child is { } done)
        {
            done.IsVisible = open;
            done.IsHitTestVisible = true;
            done.Opacity = open ? 1 : 0;
        }
        InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var child = Child;
        if (child is null)
        {
            return default;
        }
        child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
        var fraction = child.IsVisible ? Math.Clamp(_fraction, 0, 1) : 0;
        return new Size(child.DesiredSize.Width, child.DesiredSize.Height * fraction);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Child?.Arrange(new Rect(0, 0, finalSize.Width, Child.DesiredSize.Height));
        return finalSize;
    }
}

/// <summary>
/// Holds the Sign in button: full width until the password is accepted, then the button narrows to a 44px circle on
/// Arc's morph spring (no bounce) while its corners round off in 160 ms, centered in the row.
/// </summary>
/// <remarks>Code-driven: the start width is the measured full width and the spring must not bounce (the library's Morph preset does).</remarks>
internal sealed class LoginSplitSubmitSlot : Decorator
{
    private MotionValue<double>? _width;
    private MotionValue<double>? _radius;
    private bool _collapsed;

    /// <summary>True once the button narrows (tests read it).</summary>
    internal bool IsCollapsed => _collapsed;

    /// <summary>The width the button is given now (tests read it).</summary>
    internal double ShownWidth => _collapsed && _width is not null ? _width.Value : Bounds.Width;

    /// <summary>Narrows the button to <paramref name="diameter"/> (the circle) on the morph spring.</summary>
    public void Collapse(double diameter)
    {
        if (_collapsed || Child is not AvaloniaButton button)
        {
            return;
        }
        _collapsed = true;
        var from = button.Bounds.Width > 0 ? button.Bounds.Width : diameter;
        _width ??= new MotionValue<double>(this, from, MotionVectors.Double);
        _width.Changed += _ => InvalidateMeasure();
        _width.Jump(from);
        _ = _width.AnimateTo(diameter, MotionSpec.Spring(LoginSplitMotion.Morph));
        var startRadius = button.CornerRadius.TopLeft;
        _radius ??= new MotionValue<double>(this, startRadius, MotionVectors.Double);
        _radius.Changed += r => button.CornerRadius = new CornerRadius(r);
        _radius.Jump(startRadius);
        _ = _radius.AnimateTo(diameter / 2, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
        InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var child = Child;
        if (child is null)
        {
            return default;
        }
        if (!_collapsed || _width is null)
        {
            child.Measure(availableSize);
            var width = double.IsInfinity(availableSize.Width) ? child.DesiredSize.Width : availableSize.Width;
            return new Size(width, child.DesiredSize.Height);
        }
        var shown = Math.Max(0, _width.Value);
        child.Measure(new Size(shown, availableSize.Height));
        return new Size(double.IsInfinity(availableSize.Width) ? shown : availableSize.Width, child.DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var child = Child;
        if (child is null)
        {
            return finalSize;
        }
        if (!_collapsed || _width is null)
        {
            child.Arrange(new Rect(finalSize));
            return finalSize;
        }
        var shown = Math.Max(0, _width.Value);
        child.Arrange(new Rect((finalSize.Width - shown) / 2, 0, shown, child.DesiredSize.Height));
        return finalSize;
    }
}

/// <summary>
/// The check that replaces "Sign in" once the password is accepted: it grows from .6 on a quick spring while its stroke
/// draws in 340 ms on the enter curve after 150 ms. Drawn in the button's text color; whole at once without full motion.
/// </summary>
internal sealed class LoginSplitCheck : Control
{
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<LoginSplitCheck>();

    // Our own check in a 24 box, drawn from the short stroke to the long one.
    private static readonly Geometry s_path = Geometry.Parse("M4.5 12.5 9.5 17.5 19.5 6.5");
    private MotionValue<double>? _draw;
    private MotionValue<double>? _scale;
    private double _progress = 1;
    private double _scaleValue = 1;
    private Pen? _pen;
    private IBrush? _penBrush;

    static LoginSplitCheck()
    {
        AffectsRender<LoginSplitCheck>(ForegroundProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<LoginSplitCheck>(false);
        WidthProperty.OverrideDefaultValue<LoginSplitCheck>(20);
        HeightProperty.OverrideDefaultValue<LoginSplitCheck>(20);
    }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <summary>How much of the stroke is drawn (tests read it).</summary>
    internal double Progress => _progress;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!ArcMotion.IsFull(this))
        {
            _progress = 1;
            _scaleValue = 1;
            return;
        }
        _draw ??= Value(v => _progress = v);
        _scale ??= Value(v => _scaleValue = v);
        _draw.Jump(0);
        _scale.Jump(0.6);
        _progress = 0;
        _scaleValue = 0.6;
        _ = _draw.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(340), ArcEasings.Enter, TimeSpan.FromMilliseconds(150)));
        _ = _scale.AnimateTo(1, MotionSpec.Spring(LoginSplitMotion.CheckPop));
    }

    private MotionValue<double> Value(Action<double> write)
    {
        var value = new MotionValue<double>(this, 0, MotionVectors.Double);
        value.Changed += v =>
        {
            write(v);
            InvalidateVisual();
        };
        return value;
    }

    public override void Render(DrawingContext context)
    {
        var brush = Foreground;
        var progress = Math.Clamp(_progress, 0, 1);
        if (brush is null || progress <= 0)
        {
            return;
        }
        if (_pen is null || !ReferenceEquals(_penBrush, brush))
        {
            _pen = new Pen(brush, 2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            _penBrush = brush;
        }
        var size = Math.Min(Bounds.Width, Bounds.Height);
        var scale = size / 24 * _scaleValue;
        var offset = (size - 24 * scale) / 2;
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offset, offset)))
        {
            if (progress >= 1)
            {
                context.DrawGeometry(null, _pen, s_path);
                return;
            }
            var length = s_path.ContourLength;
            if (length > 0 && s_path.TryGetSegment(0, length * progress, true, out var segment) && segment is not null)
            {
                context.DrawGeometry(null, _pen, segment);
            }
        }
    }
}

/// <summary>
/// The 2px progress line of the signing step: a rounded track in the border color and an accent fill that grows from the
/// left on the in-out curve (Arc: 1.4 s, starting 260 ms after the step appears).
/// </summary>
internal sealed class LoginSplitProgressLine : Control
{
    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<LoginSplitProgressLine, IBrush?>(nameof(TrackBrush));

    public static readonly StyledProperty<IBrush?> FillBrushProperty =
        AvaloniaProperty.Register<LoginSplitProgressLine, IBrush?>(nameof(FillBrush));

    private MotionValue<double>? _value;
    private double _progress;

    static LoginSplitProgressLine()
    {
        AffectsRender<LoginSplitProgressLine>(TrackBrushProperty, FillBrushProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<LoginSplitProgressLine>(false);
    }

    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }

    public IBrush? FillBrush { get => GetValue(FillBrushProperty); set => SetValue(FillBrushProperty, value); }

    /// <summary>The filled share (tests read it).</summary>
    internal double Progress => _progress;

    /// <summary>Fills the line from empty after <paramref name="delay"/> over <paramref name="duration"/>.</summary>
    public void Play(TimeSpan delay, TimeSpan duration)
    {
        if (_value is null)
        {
            _value = new MotionValue<double>(this, 0, MotionVectors.Double);
            _value.Changed += v =>
            {
                _progress = v;
                InvalidateVisual();
            };
        }
        _value.Jump(0);
        _ = _value.AnimateTo(1, MotionSpec.Tween(duration, ArcEasings.InOut, delay));
    }

    protected override Size MeasureOverride(Size availableSize) => new(0, Height is > 0 and var h ? h : 2);

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        var radius = bounds.Height / 2;
        if (TrackBrush is { } track)
        {
            context.DrawRectangle(track, null, bounds, radius, radius);
        }
        var width = bounds.Width * Math.Clamp(_progress, 0, 1);
        if (FillBrush is { } fill && width > 0)
        {
            var r = Math.Min(radius, width / 2);
            context.DrawRectangle(fill, null, new Rect(0, 0, width, bounds.Height), r, r);
        }
    }
}

/// <summary>
/// The footer row: the status line on the left and the legal links on the right; when both do not fit on one line (a
/// narrow form column) the links wrap under the status, left-aligned (Arc's flex-wrap with space-between).
/// </summary>
internal sealed class LoginSplitFooterPanel : Panel
{
    public static readonly StyledProperty<double> RowGapProperty =
        AvaloniaProperty.Register<LoginSplitFooterPanel, double>(nameof(RowGap), 8);

    public static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<LoginSplitFooterPanel, double>(nameof(ColumnGap), 20);

    private bool _wrapped;

    static LoginSplitFooterPanel()
    {
        AffectsMeasure<LoginSplitFooterPanel>(RowGapProperty, ColumnGapProperty);
    }

    public double RowGap { get => GetValue(RowGapProperty); set => SetValue(RowGapProperty, value); }

    public double ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }

    /// <summary>True while the links sit under the status (tests read it).</summary>
    internal bool IsWrapped => _wrapped;

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count < 2)
        {
            return default;
        }
        var status = Children[0];
        var links = Children[1];
        status.Measure(new Size(availableSize.Width, double.PositiveInfinity));
        links.Measure(new Size(availableSize.Width, double.PositiveInfinity));
        var s = status.DesiredSize;
        var l = links.DesiredSize;
        _wrapped = s.Width > 0.5 && s.Width + ColumnGap + l.Width > availableSize.Width + 0.01;
        return _wrapped
            ? new Size(Math.Max(s.Width, l.Width), s.Height + RowGap + l.Height)
            : new Size(s.Width + (s.Width > 0.5 ? ColumnGap : 0) + l.Width, Math.Max(s.Height, l.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count < 2)
        {
            return finalSize;
        }
        var status = Children[0];
        var links = Children[1];
        var s = status.DesiredSize;
        var l = links.DesiredSize;
        if (_wrapped)
        {
            status.Arrange(new Rect(0, 0, finalSize.Width, s.Height));
            links.Arrange(new Rect(0, s.Height + RowGap, l.Width, l.Height));
            return finalSize;
        }
        var height = finalSize.Height;
        status.Arrange(new Rect(0, (height - s.Height) / 2, Math.Max(0, finalSize.Width - l.Width - ColumnGap), s.Height));
        links.Arrange(new Rect(finalSize.Width - l.Width, (height - l.Height) / 2, l.Width, l.Height));
        return finalSize;
    }
}

/// <summary>Motion constants of <see cref="LoginSplit"/>, as measured on the reference preview (springs without bounce).</summary>
internal static class LoginSplitMotion
{
    /// <summary>Arc's smooth spring (visual duration .4, no bounce): steps, viewport height, rows.</summary>
    public static readonly SpringSpec Smooth = SpringSpec.FromVisual(0.4, 0);

    /// <summary>Arc's morph spring (.42, no bounce): the Sign in button narrowing to a circle.</summary>
    public static readonly SpringSpec Morph = SpringSpec.FromVisual(0.42, 0);

    /// <summary>Arc's snappy spring (.26, no bounce): countdown digits.</summary>
    public static readonly SpringSpec Snappy = SpringSpec.FromVisual(0.26, 0);

    /// <summary>The accepted check growing from .6 (measured ω ≈ 15.6, critically damped).</summary>
    public static readonly SpringSpec CheckPop = SpringSpec.FromVisual(0.33, 0);

    /// <summary>A step enters from 14px below (going forward) or above (going back) and the old one leaves 8px the other way.</summary>
    public const double StepEnterOffset = 14;

    public const double StepExitOffset = 8;

    /// <summary>The entering step's fade and blur wait 60 ms, then run 240 ms on the enter curve from a 4px blur.</summary>
    public static readonly TimeSpan StepEnterDelay = TimeSpan.FromMilliseconds(60);

    public const double StepEnterBlur = 4;

    /// <summary>The leaving step fades in 180 ms while blurring to 2px.</summary>
    public const double StepExitBlur = 2;

    /// <summary>Countdown digits travel .7em.</summary>
    public const double RollTravel = 0.7;
}
