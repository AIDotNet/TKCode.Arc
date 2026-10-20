using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The currency menu surface (Arc <c>.menu</c>): 256 wide, 6px padding, 1px <c>Border</c>, <c>RadiusPanel</c> on
/// <c>SurfaceRaised</c> with the floating shadow (from <see cref="ArcPopup"/>), and a list that scrolls past 288px.
/// </summary>
internal sealed class MoneyInputCurrencyMenu : ContentControl
{
}

/// <summary>
/// The row highlight (Arc <c>.highlight</c>): <c>Foreground</c> at 6.5% with a 14px radius that glides between rows on
/// Arc's GLIDE spring; it lands in place when the menu opens and under reduced motion. Its position is a
/// <see cref="MotionValue{T}"/> because the target is a measured row rectangle and the glide must keep its velocity.
/// </summary>
internal sealed class MoneyInputCurrencyHighlight : Control
{
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<MoneyInputCurrencyHighlight, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<MoneyInputCurrencyHighlight, CornerRadius>(nameof(CornerRadius), new CornerRadius(14));

    private readonly MotionValue<double> _y;
    private readonly MotionValue<double> _height;
    private double _opacity;

    static MoneyInputCurrencyHighlight()
    {
        AffectsRender<MoneyInputCurrencyHighlight>(FillProperty, CornerRadiusProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<MoneyInputCurrencyHighlight>(false);
    }

    public MoneyInputCurrencyHighlight()
    {
        Action<double> visual = _ => InvalidateVisual();
        _y = new MotionValue<double>(this, 0, MotionVectors.Double);
        _height = new MotionValue<double>(this, 0, MotionVectors.Double);
        _y.Changed += visual;
        _height.Changed += visual;
    }

    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }

    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    /// <summary>Current top of the highlight.</summary>
    internal double Top => _y.Value;

    /// <summary>Top the highlight is heading to.</summary>
    internal double TargetTop => _y.Target;

    internal bool IsShown => _opacity > 0.5;

    /// <summary>Arc: lands in place while hidden (or under reduced motion), otherwise glides on GLIDE.</summary>
    public void MoveTo(double top, double height)
    {
        if (_opacity < 0.05 || !ArcMotion.IsFull(this))
        {
            _y.Jump(top);
            _height.Jump(height);
        }
        else
        {
            _ = _y.AnimateTo(top, MotionSpec.Spring(MoneyInputCurrencyButton.Glide));
            _ = _height.AnimateTo(height, MotionSpec.Spring(MoneyInputCurrencyButton.Glide));
        }
        _opacity = 1;
        InvalidateVisual();
    }

    public void Hide()
    {
        _opacity = 0;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (_opacity <= 0 || Fill is not { } fill || _height.Value <= 0)
        {
            return;
        }
        context.DrawRectangle(fill, null, new RoundedRect(new Rect(0, _y.Value, Bounds.Width, _height.Value), CornerRadius));
    }
}

/// <summary>
/// One currency row (Arc <c>.option</c>): symbol (22 wide, muted), code (medium, tabular), name (12px, muted, ellipsis)
/// and a check on the selected row that fades in and grows from 0.6 on the CSS spring. Row text is
/// <c>TextSecondary</c>; the active or selected row is <c>Foreground</c>.
/// </summary>
[PseudoClasses(ActivePseudoClass, SelectedPseudoClass)]
internal sealed class MoneyInputCurrencyOption : TemplatedControl
{
    public const string ActivePseudoClass = ":active";
    public const string SelectedPseudoClass = ":selected";

    public static readonly StyledProperty<string?> SymbolProperty =
        AvaloniaProperty.Register<MoneyInputCurrencyOption, string?>(nameof(Symbol));

    public static readonly StyledProperty<string?> CodeProperty =
        AvaloniaProperty.Register<MoneyInputCurrencyOption, string?>(nameof(Code));

    public static readonly StyledProperty<string?> CurrencyNameProperty =
        AvaloniaProperty.Register<MoneyInputCurrencyOption, string?>(nameof(CurrencyName));

    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<MoneyInputCurrencyOption, bool>(nameof(IsActive));

    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<MoneyInputCurrencyOption, bool>(nameof(IsSelected));

    public string? Symbol { get => GetValue(SymbolProperty); set => SetValue(SymbolProperty, value); }

    public string? Code { get => GetValue(CodeProperty); set => SetValue(CodeProperty, value); }

    public string? CurrencyName { get => GetValue(CurrencyNameProperty); set => SetValue(CurrencyNameProperty, value); }

    public bool IsActive { get => GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }

    public bool IsSelected { get => GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }

    /// <summary>Chooses this row (automation <c>Select</c>).</summary>
    internal Action<MoneyInputCurrencyOption>? Chosen { get; set; }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsActiveProperty)
        {
            PseudoClasses.Set(ActivePseudoClass, IsActive);
        }
        else if (change.Property == IsSelectedProperty)
        {
            PseudoClasses.Set(SelectedPseudoClass, IsSelected);
        }
        else if (change.Property == CodeProperty || change.Property == CurrencyNameProperty)
        {
            AutomationProperties.SetName(this, $"{Code} {CurrencyName}".Trim());
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new OptionPeer(this);

    private sealed class OptionPeer : ControlAutomationPeer, ISelectionItemProvider
    {
        public OptionPeer(MoneyInputCurrencyOption owner) : base(owner)
        {
        }

        private MoneyInputCurrencyOption Option => (MoneyInputCurrencyOption)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        public bool IsSelected => Option.IsSelected;

        public ISelectionProvider? SelectionContainer => null;

        public void AddToSelection() => Select();

        public void RemoveFromSelection()
        {
        }

        public void Select() => Option.Chosen?.Invoke(Option);
    }
}

/// <summary>
/// The currency listbox (Arc <c>.list</c>, <c>role="listbox"</c> with <c>aria-activedescendant</c>): keyboard focus stays
/// on the list while the active row moves; rows sit 2px apart above the gliding highlight.
/// </summary>
internal sealed class MoneyInputCurrencyList : Panel
{
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<MoneyInputCurrencyList, double>(nameof(Spacing), 2);

    static MoneyInputCurrencyList()
    {
        AffectsMeasure<MoneyInputCurrencyList>(SpacingProperty);
        FocusableProperty.OverrideDefaultValue<MoneyInputCurrencyList>(true);
        FocusAdornerProperty.OverrideDefaultValue<MoneyInputCurrencyList>(null);
    }

    public MoneyInputCurrencyList()
    {
        Highlight = new MoneyInputCurrencyHighlight();
        Children.Add(Highlight);
        AutomationProperties.SetName(this, "Currency");
    }

    public double Spacing { get => GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }

    public MoneyInputCurrencyHighlight Highlight { get; }

    /// <summary>Runs after every arrange (the owner places the highlight on laid-out rows).</summary>
    internal Action? Arranged { get; set; }

    public IEnumerable<MoneyInputCurrencyOption> Options => Children.OfType<MoneyInputCurrencyOption>();

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = 0.0;
        var height = 0.0;
        var rows = 0;
        var row = availableSize.WithHeight(double.PositiveInfinity);
        foreach (var child in Children)
        {
            if (child is not MoneyInputCurrencyOption option)
            {
                continue;
            }
            option.Measure(row);
            width = Math.Max(width, option.DesiredSize.Width);
            height += option.DesiredSize.Height;
            rows++;
        }
        height += Math.Max(0, rows - 1) * Spacing;
        Highlight.Measure(new Size(width, height));
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var y = 0.0;
        foreach (var child in Children)
        {
            if (child is not MoneyInputCurrencyOption option)
            {
                continue;
            }
            option.Arrange(new Rect(0, y, finalSize.Width, option.DesiredSize.Height));
            y += option.DesiredSize.Height + Spacing;
        }
        Highlight.Arrange(new Rect(finalSize));
        Arranged?.Invoke();
        return finalSize;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ListPeer(this);

    private sealed class ListPeer : ControlAutomationPeer
    {
        public ListPeer(MoneyInputCurrencyList owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}

/// <summary>
/// The currency menu popup: anchored to the shell's bottom-right edge, 8px below it, its right edge on the shell's inner
/// edge. Arc's enter (opacity fast + enter ease, scale 0.96 and 4px of travel on the snappy spring) and exit (scale 0.97,
/// 2px, instant + standard ease) grow from the currency switch; reduced motion only fades. The subtle 2px blur of Arc's
/// enter and exit is not drawn (AtomUI's popup motion actor has no blur channel).
/// </summary>
internal sealed class MoneyInputCurrencyPopup : ArcPopup
{
    private static readonly Avalonia.Animation.Easings.Easing s_linear = new Avalonia.Animation.Easings.LinearEasing();

    private readonly MenuSurfaceEnterMotion _enter = new();
    private readonly MenuSurfaceExitMotion _exit = new();

    public MoneyInputCurrencyPopup()
    {
        WindowManagerAddShadowHint = false;
        TakesFocusFromNativeControl = true;
        OverlayDismissEventPassThrough = true;
        Placement = PlacementMode.BottomEdgeAlignedRight;
        HorizontalOffset = -1;
        MarginToAnchor = 8;
        ApplyMotions();
    }

    /// <summary>Horizontal distance from the menu's right edge to the switch's centre (Arc: 44px).</summary>
    public double OriginInset { get; set; } = 44;

    /// <summary>Distance from the menu's top edge up to the switch's centre (Arc: drop + 18px = 46px).</summary>
    public double OriginRise { get; set; } = 46;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        // ArcPopup rebuilds its own motions on these changes (and right before every open); put the menu motions back.
        if (change.Property == PlacementProperty || change.Property == EnterOffsetProperty ||
            change.Property == EnterScaleProperty || change.Property == IsInstantProperty ||
            change.Property == ArcMotion.LevelProperty || change.Property == PlacementTargetProperty ||
            (change.Property == IsOpenProperty && change.GetNewValue<bool>()))
        {
            ApplyMotions();
        }
    }

    private void ApplyMotions()
    {
        var level = PlacementTarget is { } target ? ArcMotion.GetEffectiveLevel(target) : ArcMotion.GetEffectiveLevel(this);
        var reduced = level == ArcMotionLevel.Reduced;
        IsMotionEnabled = level != ArcMotionLevel.Off;
        if (reduced)
        {
            _enter.Set(1, ArcMotionPresets.Instant, s_linear, ArcMotionPresets.Instant, s_linear);
            _exit.Set(1, 0, ArcMotionPresets.Instant, s_linear);
        }
        else
        {
            var snappy = ArcSpringEasing.For(ArcSpring.Snappy);
            _enter.Set(0.96, snappy.SettleDuration, snappy, ArcMotionPresets.Fast, ArcEasings.Enter);
            _exit.Set(0.97, 0.5, ArcMotionPresets.Instant, ArcEasings.Standard);
        }
        _enter.FadeOnly = reduced;
        _exit.FadeOnly = reduced;
        _exit.FadesOut = true;
        OpenMotion = _enter;
        CloseMotion = _exit;
    }

    protected override void OnPlacementResolved(CustomPopupPlacement placement, Thickness shadow, double marginToAnchor,
                                                bool isOverlayHost, bool flipX, bool flipY)
    {
        var width = Math.Max(1, placement.PopupSize.Width - placement.Deflate.Left - placement.Deflate.Right);
        var height = Math.Max(1, placement.PopupSize.Height - placement.Deflate.Top - placement.Deflate.Bottom);
        // Arc: transform-origin: calc(100% - 44px) calc(var(--drop) * -1 - 18px), i.e. the switch's centre.
        var originX = flipX ? OriginInset / width : 1 - OriginInset / width;
        var originY = flipY ? 1 + OriginRise / height : -OriginRise / height;
        var origin = new RelativePoint(originX, originY, RelativeUnit.Relative);
        var travel = new Vector(0, flipY ? 4 : -4);
        _enter.Origin = origin;
        _enter.Offset = travel;
        _exit.Origin = origin;
        _exit.Offset = travel * _exit.OffsetFactor;
    }
}

/// <summary>
/// A fixed-width slot that centres its child without clipping it (Arc <c>.optionSymbol { width: 22px; text-align: center }</c>):
/// a wide symbol such as "CHF" overflows both sides evenly, as CSS overflow does.
/// </summary>
internal sealed class MoneyInputCenteredSlot : Decorator
{
    public static readonly StyledProperty<double> SlotWidthProperty =
        AvaloniaProperty.Register<MoneyInputCenteredSlot, double>(nameof(SlotWidth), 22);

    static MoneyInputCenteredSlot()
    {
        AffectsMeasure<MoneyInputCenteredSlot>(SlotWidthProperty);
        ClipToBoundsProperty.OverrideDefaultValue<MoneyInputCenteredSlot>(false);
    }

    public double SlotWidth { get => GetValue(SlotWidthProperty); set => SetValue(SlotWidthProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child is not { } child)
        {
            return new Size(SlotWidth, 0);
        }
        child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
        return new Size(SlotWidth, child.DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Child is { } child)
        {
            var size = child.DesiredSize;
            child.Arrange(new Rect((finalSize.Width - size.Width) / 2, (finalSize.Height - size.Height) / 2, size.Width, size.Height));
        }
        return finalSize;
    }
}
