// Ported from Arc's free plan-comparison block (MIT, Copyright (c) 2026 Elia Kuratli).
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// A plan header or a feature value of <see cref="PlanComparison"/> (Arc's <c>.planCell</c> / <c>.featureValue</c>):
/// a border whose background turns to the muted surface while its plan is selected (240ms standard ease in the theme).
/// </summary>
[PseudoClasses(SelectedPseudoClass)]
internal sealed class PlanComparisonCell : Border
{
    public const string SelectedPseudoClass = ":selected";

    private bool _isSelected;

    /// <summary>Whether the cell's plan is selected.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            PseudoClasses.Set(SelectedPseudoClass, value);
        }
    }

    protected override Type StyleKeyOverride => typeof(PlanComparisonCell);
}

/// <summary>
/// Text inside <see cref="PlanComparison"/>'s matrix. Its role is a style class (<c>name</c>, <c>value</c>, <c>muted</c>,
/// <c>included</c>, <c>header</c>, <c>plan-name</c>, <c>plan-description</c>, <c>price</c>, <c>period</c>); the theme
/// gives each its face and color and tweens the color of a value whose plan becomes selected.
/// </summary>
[PseudoClasses(SelectedPseudoClass, NarrowPseudoClass)]
internal sealed class PlanComparisonText : TextBlock
{
    public const string SelectedPseudoClass = ":selected";
    public const string NarrowPseudoClass = ":narrow";

    public PlanComparisonText(string role)
    {
        Classes.Add(role);
    }

    /// <summary>Whether the text's plan is selected.</summary>
    public bool IsSelected
    {
        set => PseudoClasses.Set(SelectedPseudoClass, value);
    }

    /// <summary>Whether the block uses its narrow layout.</summary>
    public bool IsNarrow
    {
        set => PseudoClasses.Set(NarrowPseudoClass, value);
    }

    protected override Type StyleKeyOverride => typeof(PlanComparisonText);
}

/// <summary>
/// One feature row of <see cref="PlanComparison"/>. Rows that "Show differences only" hides collapse instead of
/// disappearing (Arc's <c>AnimatePresence</c> rows): the height follows a smooth spring (visualDuration .4, bounce 0)
/// while the row fades, out in 160ms, in over 240ms on the enter curve after 40ms. Code drives it because the target
/// is the row's measured height and an interrupted spring keeps its velocity. The row draws its own bottom divider at
/// its current bottom edge, like a CSS border inside a box whose height animates.
/// </summary>
internal sealed class PlanComparisonRow : Decorator
{
    public static readonly StyledProperty<IBrush?> DividerBrushProperty =
        AvaloniaProperty.Register<PlanComparisonRow, IBrush?>(nameof(DividerBrush));

    /// <summary>Arc's <c>spring.smooth</c>.</summary>
    internal static readonly SpringSpec HeightSpring = SpringSpec.FromVisual(0.4, 0);

    /// <summary>motion's default curve for a tween without an ease (the exit fade).</summary>
    internal static readonly Easing ExitEase = new SplineEasing(0, 0, 0.58, 1);

    /// <summary>The enter fade waits this long (Arc: <c>delay: .04</c>).</summary>
    internal static readonly TimeSpan EnterDelay = TimeSpan.FromMilliseconds(40);

    private MotionValue<double>? _height;
    private MotionValue<double>? _fade;
    private double _reveal = 1;
    private bool _isShown = true;
    private bool _hasDivider = true;

    static PlanComparisonRow()
    {
        ClipToBoundsProperty.OverrideDefaultValue<PlanComparisonRow>(true);
        AffectsRender<PlanComparisonRow>(DividerBrushProperty);
    }

    public PlanComparisonRow(PlanComparisonFeature feature)
    {
        Feature = feature;
    }

    /// <summary>The feature the row shows.</summary>
    public PlanComparisonFeature Feature { get; }

    public IBrush? DividerBrush
    {
        get => GetValue(DividerBrushProperty);
        set => SetValue(DividerBrushProperty, value);
    }

    /// <summary>Whether the row is meant to show (it may still be collapsing).</summary>
    public bool IsShown => _isShown;

    /// <summary>Whether the row takes space: shown, or still collapsing.</summary>
    public bool IsPresent => _isShown || _reveal > 0;

    /// <summary>The share of the row's height that shows (0–1).</summary>
    public double Reveal => _reveal;

    /// <summary>Whether the bottom divider is drawn (Arc: every row but the last).</summary>
    public bool HasDivider
    {
        get => _hasDivider;
        set
        {
            if (_hasDivider != value)
            {
                _hasDivider = value;
                InvalidateMeasure();
                InvalidateVisual();
            }
        }
    }

    /// <summary>Raised when a collapse has finished and the row no longer takes space.</summary>
    public event EventHandler? Collapsed;

    /// <summary>Shows or hides the row; <paramref name="animate"/> false (or reduced motion) jumps.</summary>
    public void SetShown(bool shown, bool animate)
    {
        if (shown == _isShown)
        {
            return;
        }
        _isShown = shown;
        var full = animate && IsLoaded && ArcMotion.IsFull(this);
        if (!full)
        {
            _height?.Jump(shown ? 1 : 0);
            _fade?.Jump(shown ? 1 : 0);
            Apply(shown ? 1 : 0);
            Opacity = shown ? 1 : 0;
            if (!shown)
            {
                Collapsed?.Invoke(this, EventArgs.Empty);
            }
            return;
        }
        _height ??= CreateHeight();
        _fade ??= CreateFade();
        if (shown)
        {
            // A collapsed row is invisible, and motion values of an invisible visual jump: show it first.
            IsVisible = true;
            _ = _height.AnimateTo(1, MotionSpec.Spring(HeightSpring));
            _ = _fade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter, EnterDelay));
        }
        else
        {
            _ = _fade.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Fast, ExitEase));
            _ = CollapseAsync();
        }
    }

    private async Task CollapseAsync()
    {
        var done = await _height!.AnimateTo(0, MotionSpec.Spring(HeightSpring));
        if (done && !_isShown)
        {
            Apply(0);
            Collapsed?.Invoke(this, EventArgs.Empty);
        }
    }

    private MotionValue<double> CreateHeight()
    {
        var value = new MotionValue<double>(this, _reveal, MotionVectors.Double);
        value.Changed += Apply;
        return value;
    }

    private MotionValue<double> CreateFade()
    {
        var value = new MotionValue<double>(this, Opacity, MotionVectors.Double);
        value.Changed += v => Opacity = Math.Clamp(v, 0, 1);
        return value;
    }

    private void Apply(double reveal)
    {
        // Springs at bounce 0 never overshoot, but a value can land a hair past its target.
        reveal = Math.Clamp(reveal, 0, 1);
        if (reveal == _reveal && IsVisible == (reveal > 0 || _isShown))
        {
            return;
        }
        _reveal = reveal;
        IsVisible = reveal > 0 || _isShown;
        InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var child = Child;
        if (child is null)
        {
            return default;
        }
        child.Measure(availableSize.WithHeight(double.PositiveInfinity));
        var divider = _hasDivider ? 1 : 0;
        var full = child.DesiredSize.Height + divider;
        // Like a CSS border-box whose height animates from 0: the divider stays, the content is clipped above it.
        var height = _reveal >= 1 ? full : Math.Max(divider, full * _reveal);
        return new Size(child.DesiredSize.Width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Child?.Arrange(new Rect(0, 0, finalSize.Width, Child.DesiredSize.Height));
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (_hasDivider && DividerBrush is { } brush && Bounds.Height >= 1)
        {
            context.FillRectangle(brush, new Rect(0, Bounds.Height - 1, Bounds.Width, 1));
        }
    }

    protected override Type StyleKeyOverride => typeof(PlanComparisonRow);
}

/// <summary>
/// Two children on one line pushed apart (CSS <c>justify-content: space-between; align-items: center; flex-wrap:
/// wrap</c>): when they do not fit side by side the second one wraps under the first, at the start.
/// </summary>
internal sealed class PlanComparisonWrapRow : Panel
{
    public static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<PlanComparisonWrapRow, double>(nameof(ColumnGap));

    public static readonly StyledProperty<double> RowGapProperty =
        AvaloniaProperty.Register<PlanComparisonWrapRow, double>(nameof(RowGap));

    private bool _wrapped;

    static PlanComparisonWrapRow()
    {
        AffectsMeasure<PlanComparisonWrapRow>(ColumnGapProperty, RowGapProperty);
    }

    public double ColumnGap
    {
        get => GetValue(ColumnGapProperty);
        set => SetValue(ColumnGapProperty, value);
    }

    public double RowGap
    {
        get => GetValue(RowGapProperty);
        set => SetValue(RowGapProperty, value);
    }

    /// <summary>Whether the second child sits on its own line (tests).</summary>
    internal bool IsWrapped => _wrapped;

    protected override Size MeasureOverride(Size availableSize)
    {
        var first = Children.Count > 0 ? Children[0] : null;
        var second = Children.Count > 1 ? Children[1] : null;
        first?.Measure(availableSize);
        second?.Measure(availableSize);
        var a = first?.IsVisible == true ? first.DesiredSize : default;
        var b = second?.IsVisible == true ? second.DesiredSize : default;
        var gap = a.Width > 0 && b.Width > 0 ? ColumnGap : 0;
        _wrapped = a.Width + gap + b.Width > availableSize.Width + 0.5;
        if (_wrapped)
        {
            return new Size(Math.Max(a.Width, b.Width), a.Height + RowGap + b.Height);
        }
        var width = double.IsInfinity(availableSize.Width) ? a.Width + gap + b.Width : availableSize.Width;
        return new Size(width, Math.Max(a.Height, b.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var first = Children.Count > 0 ? Children[0] : null;
        var second = Children.Count > 1 ? Children[1] : null;
        var a = first?.DesiredSize ?? default;
        var b = second?.DesiredSize ?? default;
        if (_wrapped)
        {
            first?.Arrange(new Rect(0, 0, Math.Min(a.Width, finalSize.Width), a.Height));
            second?.Arrange(new Rect(0, a.Height + RowGap, Math.Min(b.Width, finalSize.Width), b.Height));
        }
        else
        {
            first?.Arrange(new Rect(0, (finalSize.Height - a.Height) / 2, a.Width, a.Height));
            second?.Arrange(new Rect(finalSize.Width - b.Width, (finalSize.Height - b.Height) / 2, b.Width, b.Height));
        }
        return finalSize;
    }
}

/// <summary>
/// Gives a wrapping <see cref="TextBlock"/> the narrowest width that keeps its number of lines, so a heading breaks
/// into even lines (CSS <c>text-wrap: balance</c>).
/// </summary>
internal sealed class PlanComparisonBalance : Decorator
{
    private double _width = double.NaN;

    protected override Size MeasureOverride(Size availableSize)
    {
        _width = double.NaN;
        if (Child is not TextBlock block)
        {
            return base.MeasureOverride(availableSize);
        }
        block.Measure(availableSize);
        if (double.IsInfinity(availableSize.Width))
        {
            return block.DesiredSize;
        }
        var lines = block.TextLayout.TextLines.Count;
        if (lines < 2)
        {
            return block.DesiredSize;
        }
        var full = availableSize.Width;
        var low = full / lines;
        var high = full;
        for (var i = 0; i < 8 && high - low > 1; i++)
        {
            var mid = (low + high) / 2;
            block.Measure(availableSize.WithWidth(mid));
            if (block.TextLayout.TextLines.Count > lines)
            {
                low = mid;
            }
            else
            {
                high = mid;
            }
        }
        // A pixel of slack so layout rounding never re-wraps the balanced lines.
        var width = Math.Min(full, Math.Ceiling(high) + 1);
        block.Measure(availableSize.WithWidth(width));
        while (block.TextLayout.TextLines.Count > lines && width < full)
        {
            width = Math.Min(full, width + 2);
            block.Measure(availableSize.WithWidth(width));
        }
        _width = width;
        return block.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var width = double.IsNaN(_width) ? finalSize.Width : Math.Min(_width, finalSize.Width);
        Child?.Arrange(new Rect(0, 0, width, finalSize.Height));
        return finalSize;
    }
}

/// <summary>
/// A number that counts to its new value (Arc's <c>AnimatedNumber</c>): a 240ms tween on the enter curve, rounded on
/// every frame. Reduced and Off jump. Unlike Arc, a count that is interrupted continues from the number on screen
/// instead of jumping back to the previous target.
/// </summary>
internal sealed class PlanComparisonCounter
{
    private readonly Visual _owner;
    private readonly Action<decimal> _apply;
    private MotionValue<double>? _progress;
    private decimal _from;
    private decimal _to;
    private decimal _shown;
    private int _decimals;
    private bool _initialized;

    public PlanComparisonCounter(Visual owner, Action<decimal> apply)
    {
        _owner = owner;
        _apply = apply;
    }

    /// <summary>The value the count ends on.</summary>
    public decimal Target => _to;

    /// <summary>The value on screen.</summary>
    public decimal Shown => _shown;

    /// <summary>Whether a count is running.</summary>
    public bool IsCounting => _progress?.IsAnimating == true;

    public void Set(decimal value, bool animate)
    {
        if (_initialized && value == _to)
        {
            return;
        }
        var full = animate && _initialized && _owner is Control { IsLoaded: true } && ArcMotion.IsFull(_owner);
        _initialized = true;
        if (!full)
        {
            _progress?.Stop();
            _from = _to = value;
            Show(value);
            return;
        }
        _from = _shown;
        _to = value;
        _decimals = PlanComparisonMath.CountDecimals(_from, _to);
        _progress ??= CreateProgress();
        _progress.Jump(0);
        _ = _progress.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter));
    }

    private MotionValue<double> CreateProgress()
    {
        var value = new MotionValue<double>(_owner, 0, MotionVectors.Double);
        value.Changed += p => Show(p >= 1 ? _to : PlanComparisonMath.CountValue(_from, _to, p, _decimals));
        return value;
    }

    private void Show(decimal value)
    {
        if (value == _shown && _initialized && _progress is not null)
        {
            return;
        }
        _shown = value;
        _apply(value);
    }
}
