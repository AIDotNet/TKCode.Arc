using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// Hosts the tab indicator and the tab triggers in one coordinate space (the indicator scrolls with the row) and
/// reports every arrange so the indicator can follow passive layout changes.
/// </summary>
internal sealed class PageHeaderTabStrip : Panel
{
    public Action? Arranged { get; set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = 0.0;
        var height = 0.0;
        foreach (var child in Children)
        {
            child.Measure(availableSize);
            width = Math.Max(width, child.DesiredSize.Width);
            height = Math.Max(height, child.DesiredSize.Height);
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var rect = new Rect(finalSize);
        foreach (var child in Children)
        {
            child.Arrange(rect);
        }
        Arranged?.Invoke();
        return finalSize;
    }
}

/// <summary>
/// The page header's breadcrumb row (Arc .crumbs ol: one line, 6px apart). Every crumb keeps its width except the
/// last one, the current page, which takes what is left and ellipsizes, so a narrow lead cell never wraps the path.
/// </summary>
internal sealed class PageHeaderCrumbPanel : Panel
{
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<PageHeaderCrumbPanel, double>(nameof(Spacing), 6);

    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    static PageHeaderCrumbPanel()
    {
        AffectsMeasure<PageHeaderCrumbPanel>(SpacingProperty);
    }

    private int LastVisible()
    {
        for (var i = Children.Count - 1; i >= 0; i--)
        {
            if (Children[i].IsVisible)
            {
                return i;
            }
        }
        return -1;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var last = LastVisible();
        double width = 0, height = 0;
        var count = 0;
        for (var i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            if (!child.IsVisible || i == last)
            {
                continue;
            }
            child.Measure(Size.Infinity);
            width += child.DesiredSize.Width;
            height = Math.Max(height, child.DesiredSize.Height);
            count++;
        }
        if (last >= 0)
        {
            var gaps = count * Spacing;
            var room = Math.Max(0, availableSize.Width - width - gaps);
            var child = Children[last];
            child.Measure(new Size(room, availableSize.Height));
            width += gaps + child.DesiredSize.Width;
            height = Math.Max(height, child.DesiredSize.Height);
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var last = LastVisible();
        double x = 0;
        for (var i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            if (!child.IsVisible)
            {
                continue;
            }
            var desired = child.DesiredSize;
            var w = i == last ? Math.Max(0, Math.Min(desired.Width, finalSize.Width - x)) : desired.Width;
            child.Arrange(new Rect(x, (finalSize.Height - desired.Height) / 2, w, desired.Height));
            x += w + Spacing;
        }
        return finalSize;
    }
}

/// <summary>
/// The slot of the secondary actions. Folding springs its width to zero (smooth) and fades it (fast, 40ms late);
/// unfolding springs back to the natural width and fades in (standard, 80ms late). The actions keep their own width
/// and stay pinned left while the slot narrows, so they fold to the right under the overflow button; only the right
/// edge clips (Arc: clip-path inset(-8px 0 -8px -8px)). Without motion (first measurement, Reduced, Off) it jumps.
/// Code-driven because the width spring, the fade and their delays differ between folding and unfolding, and the
/// slot must follow its content's natural width at once between folds, which ControlTheme transitions cannot express.
/// </summary>
internal sealed class PageHeaderFoldSlot : Decorator
{
    private const double ClipBleed = 8;

    private MotionValue<double>? _width;
    private MotionValue<double>? _opacity;
    private readonly RectangleGeometry _clip = new();
    private bool _folded;
    private double _natural;

    /// <summary>True while the actions are folded into the overflow menu.</summary>
    public bool IsFolded => _folded;

    /// <summary>The natural width of the actions (measured even while folded).</summary>
    public double NaturalWidth => Child?.DesiredSize.Width ?? 0;

    public void SetFolded(bool folded, bool animate)
    {
        var changed = _folded != folded;
        _folded = folded;
        _width ??= CreateWidth();
        _opacity ??= CreateOpacity();
        IsHitTestVisible = !folded;
        KeyboardNavigation.SetTabNavigation(this, folded ? KeyboardNavigationMode.None : KeyboardNavigationMode.Continue);
        AutomationProperties.SetAccessibilityView(this, folded ? AccessibilityView.Raw : AccessibilityView.Default);
        var target = folded ? 0 : _natural;
        if (!animate || !changed || !IsLoaded || !ArcMotion.IsFull(this))
        {
            if (changed || !animate)
            {
                _width.Jump(target);
                _opacity.Jump(folded ? 0 : 1);
            }
            InvalidateMeasure();
            return;
        }
        _ = _width.AnimateTo(target, MotionSpec.Spring(ArcSpring.Smooth));
        _ = folded
            ? _opacity.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard, TimeSpan.FromMilliseconds(40)))
            : _opacity.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard, TimeSpan.FromMilliseconds(80)));
    }

    private MotionValue<double> CreateWidth()
    {
        var value = new MotionValue<double>(this, _folded ? 0 : _natural, MotionVectors.Double);
        value.Changed += _ => InvalidateMeasure();
        return value;
    }

    private MotionValue<double> CreateOpacity()
    {
        var value = new MotionValue<double>(this, _folded ? 0 : 1, MotionVectors.Double);
        value.Changed += v => Opacity = Math.Clamp(v, 0, 1);
        return value;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var child = Child;
        if (child is null)
        {
            return default;
        }
        child.Measure(availableSize.WithWidth(double.PositiveInfinity));
        _natural = child.DesiredSize.Width;
        _width ??= CreateWidth();
        var target = _folded ? 0 : _natural;
        if (_width.IsAnimating)
        {
            if (Math.Abs(_width.Target - target) > 0.5)
            {
                _ = _width.AnimateTo(target, MotionSpec.Spring(ArcSpring.Smooth));
            }
        }
        else if (Math.Abs(_width.Value - target) > 0.01)
        {
            // Between folds the slot follows its content at once (the buttons spring their own width).
            _width.Jump(target);
        }
        return new Size(Math.Max(0, _width.Value), child.DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var child = Child;
        if (child is null)
        {
            return finalSize;
        }
        child.Arrange(new Rect(0, 0, child.DesiredSize.Width, finalSize.Height));
        _clip.Rect = new Rect(-ClipBleed, -ClipBleed, finalSize.Width + ClipBleed, finalSize.Height + 2 * ClipBleed);
        Clip = _clip;
        InvalidateVisual();
        return finalSize;
    }
}

/// <summary>The 6px dot of a status badge; it takes the badge tone through the inherited foreground.</summary>
internal sealed class PageHeaderStatusDot : Control
{
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<PageHeaderStatusDot>();

    public static readonly StyledProperty<double> DiameterProperty =
        AvaloniaProperty.Register<PageHeaderStatusDot, double>(nameof(Diameter), 6);

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public double Diameter
    {
        get => GetValue(DiameterProperty);
        set => SetValue(DiameterProperty, value);
    }

    static PageHeaderStatusDot()
    {
        AffectsMeasure<PageHeaderStatusDot>(DiameterProperty);
        AffectsRender<PageHeaderStatusDot>(ForegroundProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<PageHeaderStatusDot>(false);
    }

    protected override Size MeasureOverride(Size availableSize) => new(Diameter, Diameter);

    public override void Render(DrawingContext context)
    {
        if (Foreground is not { } brush)
        {
            return;
        }
        var r = Diameter / 2;
        context.DrawEllipse(brush, null, new Point(Bounds.Width / 2, Bounds.Height / 2), r, r);
    }
}

/// <summary>
/// The compact bar's title button (Arc .compact): the page title at text-base with a small status badge. Choosing it
/// scrolls the page back to the top.
/// </summary>
internal sealed class PageHeaderCompactTitle : AvaloniaButton
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<PageHeaderCompactTitle, string?>(nameof(Text));

    public static readonly StyledProperty<string?> StatusProperty =
        AvaloniaProperty.Register<PageHeaderCompactTitle, string?>(nameof(Status));

    public static readonly StyledProperty<BadgeTone> StatusToneProperty =
        AvaloniaProperty.Register<PageHeaderCompactTitle, BadgeTone>(nameof(StatusTone));

    /// <summary>False hides the badge (below 560px).</summary>
    public static readonly StyledProperty<bool> IsStatusVisibleProperty =
        AvaloniaProperty.Register<PageHeaderCompactTitle, bool>(nameof(IsStatusVisible), true);

    public static readonly DirectProperty<PageHeaderCompactTitle, bool> ShowsStatusProperty =
        AvaloniaProperty.RegisterDirect<PageHeaderCompactTitle, bool>(nameof(ShowsStatus), o => o.ShowsStatus);

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string? Status
    {
        get => GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public BadgeTone StatusTone
    {
        get => GetValue(StatusToneProperty);
        set => SetValue(StatusToneProperty, value);
    }

    public bool IsStatusVisible
    {
        get => GetValue(IsStatusVisibleProperty);
        set => SetValue(IsStatusVisibleProperty, value);
    }

    private bool _showsStatus;

    public bool ShowsStatus
    {
        get => _showsStatus;
        private set => SetAndRaise(ShowsStatusProperty, ref _showsStatus, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StatusProperty || change.Property == IsStatusVisibleProperty)
        {
            ShowsStatus = IsStatusVisible && !string.IsNullOrEmpty(Status);
        }
    }
}

/// <summary>
/// Moves one part of the header between its shown pose and a hidden pose as the header condenses (Arc's swap
/// transition): position and scale on the smooth spring, opacity on a tween (arriving: standard, 50ms late; leaving:
/// fast) and a blur that clears with the opacity. Reduced keeps only the fade; Off jumps. A hidden part is inert.
/// Code-driven because arriving and leaving use different durations and delays for the same properties and the
/// springs must keep their velocity when the header flips back mid-way, which ControlTheme transitions cannot express.
/// </summary>
internal sealed class PageHeaderSwap
{
    private readonly Control _target;
    private readonly double _hiddenY;
    private readonly double _hiddenScale;
    private readonly double _hiddenBlur;
    private readonly MatrixTransform _transform = new();
    private MotionValue<double>? _pose;
    private MotionValue<double>? _fade;
    private MotionValue<double>? _blur;
    private bool? _shown;

    public PageHeaderSwap(Control target, double hiddenY, double hiddenScale = 1, double hiddenBlur = 0)
    {
        _target = target;
        _hiddenY = hiddenY;
        _hiddenScale = hiddenScale;
        _hiddenBlur = hiddenBlur;
        _target.RenderTransformOrigin = RelativePoint.TopLeft;
        _target.RenderTransform = _transform;
    }

    public bool IsShown => _shown ?? true;

    public void Set(bool shown, bool animate)
    {
        if (_shown == shown)
        {
            return;
        }
        var first = _shown is null;
        _shown = shown;
        _pose ??= Create(shown ? 0 : 1, ApplyTransform);
        _fade ??= Create(shown ? 1 : 0, v => _target.Opacity = Math.Clamp(v, 0, 1));
        _blur ??= Create(0, ApplyBlur);
        SetInert(!shown);
        var level = ArcMotion.GetEffectiveLevel(_target);
        if (first || !animate || !_target.IsLoaded || level == ArcMotionLevel.Off)
        {
            _pose.Jump(shown ? 0 : 1);
            _fade.Jump(shown ? 1 : 0);
            _blur.Jump(0);
            return;
        }
        var duration = shown ? ArcMotionPresets.Standard : ArcMotionPresets.Fast;
        var delay = shown ? TimeSpan.FromMilliseconds(50) : TimeSpan.Zero;
        _ = _fade.AnimateTo(shown ? 1 : 0, MotionSpec.Tween(duration, ArcEasings.Standard, delay));
        if (level != ArcMotionLevel.Full)
        {
            // Reduced: only the fade.
            _pose.Jump(shown ? 0 : 1);
            _blur.Jump(0);
            return;
        }
        _ = _pose.AnimateTo(shown ? 0 : 1, MotionSpec.Spring(ArcSpring.Smooth));
        if (_hiddenBlur > 0)
        {
            // Arriving copy clears from the blur; leaving copy blurs as it goes (the blur follows the fade timing).
            if (shown && !_blur.IsAnimating && _target.Opacity < 0.01)
            {
                _blur.Jump(_hiddenBlur);
            }
            _ = _blur.AnimateTo(shown ? 0 : _hiddenBlur, MotionSpec.Tween(duration, ArcEasings.Standard));
        }
    }

    private MotionValue<double> Create(double initial, Action<double> apply)
    {
        var value = new MotionValue<double>(_target, initial, MotionVectors.Double);
        value.Changed += apply;
        apply(initial);
        return value;
    }

    private void ApplyTransform(double pose)
    {
        var scale = 1 + (_hiddenScale - 1) * pose;
        _transform.Matrix = Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(0, _hiddenY * pose);
    }

    private void ApplyBlur(double radius) => _target.Effect = ArcBlur.Get(radius);

    private void SetInert(bool inert)
    {
        _target.IsHitTestVisible = !inert;
        if (_target is AvaloniaButton)
        {
            _target.IsTabStop = !inert;
        }
        KeyboardNavigation.SetTabNavigation(_target, inert ? KeyboardNavigationMode.None : KeyboardNavigationMode.Continue);
        AutomationProperties.SetAccessibilityView(_target, inert ? AccessibilityView.Raw : AccessibilityView.Default);
    }
}
