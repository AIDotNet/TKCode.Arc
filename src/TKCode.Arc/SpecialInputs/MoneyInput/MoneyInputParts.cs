using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using TKCode.Arc.Motion;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A money input button that answers hover only to a fine pointer (Arc <c>@media (hover: hover) and (pointer: fine)</c>):
/// <c>:hover</c> is set for mouse and pen, never for touch.
/// </summary>
[PseudoClasses(HoverPseudoClass)]
internal abstract class MoneyInputPressable : AvaloniaButton
{
    public const string HoverPseudoClass = ":hover";

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        PseudoClasses.Set(HoverPseudoClass, e.Pointer.Type != PointerType.Touch);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (e.Pointer.Type != PointerType.Touch && !PseudoClasses.Contains(HoverPseudoClass))
        {
            PseudoClasses.Set(HoverPseudoClass, true);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }
}

/// <summary>
/// A quick add chip (Arc <c>.chip</c>): a 30px pill that adds a whole-unit amount ("+$10", accessible name "Add $10").
/// Hover fills <c>SurfaceMuted</c> with <c>Foreground</c> text, a press scales to 0.96 (full motion only), and at the
/// maximum it keeps working but looks disabled (<c>aria-disabled</c>, opacity 0.45) so the press can answer with the kick.
/// </summary>
[PseudoClasses(AtLimitPseudoClass)]
internal sealed class MoneyInputChip : MoneyInputPressable
{
    public const string AtLimitPseudoClass = ":at-limit";

    private static readonly TransformOperations s_identity = TransformOperations.Parse("scale(1)");
    private static readonly TransformOperations s_pressed = TransformOperations.Parse("scale(0.96)");

    public static readonly StyledProperty<bool> IsAtLimitProperty =
        AvaloniaProperty.Register<MoneyInputChip, bool>(nameof(IsAtLimit));

    public MoneyInputChip()
    {
        RenderTransform = s_identity;
    }

    /// <summary>The amount (major units) this chip adds.</summary>
    public decimal Amount { get; init; }

    /// <summary>Arc <c>aria-disabled</c> at the maximum.</summary>
    public bool IsAtLimit { get => GetValue(IsAtLimitProperty); set => SetValue(IsAtLimitProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsAtLimitProperty)
        {
            PseudoClasses.Set(AtLimitPseudoClass, IsAtLimit);
        }
        else if (change.Property == IsPressedProperty)
        {
            // Applied as a value so the theme's transition (full motion only) animates it; reduced motion never scales.
            RenderTransform = IsPressed && ArcMotion.IsFull(this) ? s_pressed : s_identity;
        }
    }
}

/// <summary>
/// The currency switch (Arc <c>.currencyButton</c>): the code and a 16px chevron on a 36px pill. Hover, press and the
/// open menu fill it with <c>SurfaceMuted</c>; the chevron turns 180° on Arc's GLIDE spring while the menu is open.
/// ArrowDown and ArrowUp open the menu. Exposes the expand / collapse pattern (<c>aria-expanded</c>).
/// </summary>
[TemplatePart(ChevronPart, typeof(Control))]
[PseudoClasses(OpenPseudoClass)]
internal sealed class MoneyInputCurrencyButton : MoneyInputPressable
{
    public const string ChevronPart = "PART_Chevron";
    public const string OpenPseudoClass = ":open";

    /// <summary>Arc <c>GLIDE = physical(.28, .08)</c>.</summary>
    internal static readonly SpringSpec Glide = SpringSpec.FromVisual(0.28, 0.08);

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<MoneyInputCurrencyButton, bool>(nameof(IsOpen));

    private readonly RotateTransform _rotation = new();
    private readonly MotionValue<double> _angle;
    private Control? _chevron;

    public MoneyInputCurrencyButton()
    {
        _angle = new MotionValue<double>(this, 0, MotionVectors.Double);
        _angle.Changed += angle => _rotation.Angle = angle;
    }

    /// <summary>True while the currency menu is open.</summary>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <summary>Opens or closes the menu (set by the owner).</summary>
    internal Action<bool>? ToggleRequested { get; set; }

    /// <summary>Chevron angle (degrees).</summary>
    internal double ChevronAngle => _angle.Value;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_chevron is not null)
        {
            _chevron.RenderTransform = null;
        }
        _chevron = e.NameScope.Find<Control>(ChevronPart);
        if (_chevron is not null)
        {
            _chevron.RenderTransformOrigin = RelativePoint.Center;
            _chevron.RenderTransform = _rotation;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty)
        {
            var open = change.GetNewValue<bool>();
            PseudoClasses.Set(OpenPseudoClass, open);
            _ = _angle.AnimateTo(open ? 180 : 0, MotionSpec.Spring(Glide));
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new CurrencyButtonPeer(this);

    private sealed class CurrencyButtonPeer : ButtonAutomationPeer, IExpandCollapseProvider
    {
        public CurrencyButtonPeer(MoneyInputCurrencyButton owner) : base(owner)
        {
            owner.PropertyChanged += (_, e) =>
            {
                if (e.Property == IsOpenProperty)
                {
                    RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                        e.GetOldValue<bool>() ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed,
                        e.GetNewValue<bool>() ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed);
                }
            };
        }

        private MoneyInputCurrencyButton Button => (MoneyInputCurrencyButton)Owner;

        public ExpandCollapseState ExpandCollapseState => Button.IsOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => true;

        public void Expand() => Button.ToggleRequested?.Invoke(true);

        public void Collapse() => Button.ToggleRequested?.Invoke(false);
    }
}

/// <summary>
/// The row under the shell (Arc <c>.toolbar</c>): quick add chips on the left and the range text on the right, which
/// wraps to its own line when both do not fit (<c>flex-wrap: wrap; justify-content: space-between</c>).
/// </summary>
internal sealed class MoneyInputToolbar : Panel
{
    public static readonly StyledProperty<double> RowGapProperty =
        AvaloniaProperty.Register<MoneyInputToolbar, double>(nameof(RowGap), 8);

    public static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<MoneyInputToolbar, double>(nameof(ColumnGap), 12);

    static MoneyInputToolbar()
    {
        AffectsMeasure<MoneyInputToolbar>(RowGapProperty, ColumnGapProperty);
    }

    public double RowGap { get => GetValue(RowGapProperty); set => SetValue(RowGapProperty, value); }

    public double ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }

    /// <summary>True when the second item sits on its own line.</summary>
    internal bool IsWrapped { get; private set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        Control? first = null;
        Control? second = null;
        foreach (var child in Children)
        {
            child.Measure(availableSize);
            if (!child.IsVisible)
            {
                continue;
            }
            if (first is null)
            {
                first = child;
            }
            else
            {
                second ??= child;
            }
        }
        if (first is null)
        {
            IsWrapped = false;
            return default;
        }
        if (second is null)
        {
            IsWrapped = false;
            return first.DesiredSize;
        }
        var a = first.DesiredSize;
        var b = second.DesiredSize;
        IsWrapped = a.Width + ColumnGap + b.Width > availableSize.Width;
        return IsWrapped
            ? new Size(Math.Max(a.Width, b.Width), a.Height + RowGap + b.Height)
            : new Size(a.Width + ColumnGap + b.Width, Math.Max(a.Height, b.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Control? first = null;
        Control? second = null;
        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }
            if (first is null)
            {
                first = child;
            }
            else
            {
                second ??= child;
            }
        }
        if (first is null)
        {
            return finalSize;
        }
        var a = first.DesiredSize;
        if (second is null)
        {
            first.Arrange(new Rect(0, 0, Math.Min(a.Width, finalSize.Width), a.Height));
            return finalSize;
        }
        var b = second.DesiredSize;
        if (IsWrapped)
        {
            first.Arrange(new Rect(0, 0, Math.Min(a.Width, finalSize.Width), a.Height));
            second.Arrange(new Rect(0, a.Height + RowGap, Math.Min(b.Width, finalSize.Width), b.Height));
        }
        else
        {
            var height = Math.Max(a.Height, b.Height);
            first.Arrange(new Rect(0, (height - a.Height) / 2, a.Width, a.Height));
            second.Arrange(new Rect(finalSize.Width - b.Width, (height - b.Height) / 2, b.Width, b.Height));
        }
        return finalSize;
    }
}
