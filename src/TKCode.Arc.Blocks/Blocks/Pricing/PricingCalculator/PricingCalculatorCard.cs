using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One plan card of <see cref="PricingCalculator"/> (Arc pricing-calculator, design reference, notes 3.1): name and the
/// recommended badge, a line of description, the price area (the rolling seat price with its two-line unit and the
/// card total, or, above the plan's cap, a two-line note), the plan button, and the feature list under a hairline.
/// Above its cap the description and features dim to 50%. In the stacked layout an unselected card folds its
/// description and features away (height on the Smooth spring, opacity 240 ms). Built in code for each plan.
/// </summary>
[TemplatePart(BadgePart, typeof(Border))]
[TemplatePart(ActionPart, typeof(Button))]
[TemplatePart(FeaturesPart, typeof(PricingCalculatorFeatureList))]
[PseudoClasses(SelectedPseudoClass, RecommendedPseudoClass, UnavailablePseudoClass, CollapsedPseudoClass, CompactPseudoClass)]
internal sealed class PricingCalculatorCard : TemplatedControl
{
    public const string BadgePart = "PART_Badge";
    public const string ActionPart = "PART_Action";
    public const string FeaturesPart = "PART_Features";
    public const string SelectedPseudoClass = ":selected";
    public const string RecommendedPseudoClass = ":recommended";
    public const string UnavailablePseudoClass = ":unavailable";
    public const string CollapsedPseudoClass = ":collapsed";
    public const string CompactPseudoClass = ":compact";

    /// <summary>The badge's glide between cards: Arc's measured ω 10.4, critically damped.</summary>
    internal static readonly SpringSpec BadgeGlide = SpringSpec.FromVisual(0.5, 0);

    public static readonly StyledProperty<string?> PlanNameProperty =
        AvaloniaProperty.Register<PricingCalculatorCard, string?>(nameof(PlanName));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<PricingCalculatorCard, string?>(nameof(Description));

    public static readonly StyledProperty<string?> BadgeTextProperty =
        AvaloniaProperty.Register<PricingCalculatorCard, string?>(nameof(BadgeText));

    public static readonly StyledProperty<decimal> PriceValueProperty =
        AvaloniaProperty.Register<PricingCalculatorCard, decimal>(nameof(PriceValue));

    public static readonly StyledProperty<decimal> TotalValueProperty =
        AvaloniaProperty.Register<PricingCalculatorCard, decimal>(nameof(TotalValue));

    public static readonly StyledProperty<string?> MoneyPrefixProperty =
        AvaloniaProperty.Register<PricingCalculatorCard, string?>(nameof(MoneyPrefix));

    public static readonly StyledProperty<string?> MoneySuffixProperty =
        AvaloniaProperty.Register<PricingCalculatorCard, string?>(nameof(MoneySuffix));

    public static readonly StyledProperty<int> PriceDecimalsProperty =
        AvaloniaProperty.Register<PricingCalculatorCard, int>(nameof(PriceDecimals));

    public static readonly StyledProperty<int> TotalDecimalsProperty =
        AvaloniaProperty.Register<PricingCalculatorCard, int>(nameof(TotalDecimals));

    public static readonly StyledProperty<CultureInfo?> CultureProperty =
        AvaloniaProperty.Register<PricingCalculatorCard, CultureInfo?>(nameof(Culture));

    public static readonly StyledProperty<string?> TotalCaptionProperty =
        AvaloniaProperty.Register<PricingCalculatorCard, string?>(nameof(TotalCaption));

    public static readonly StyledProperty<string?> PerSeatProperty =
        AvaloniaProperty.Register<PricingCalculatorCard, string?>(nameof(PerSeat));

    public static readonly StyledProperty<string?> PerMonthProperty =
        AvaloniaProperty.Register<PricingCalculatorCard, string?>(nameof(PerMonth));

    public static readonly StyledProperty<string?> LimitTitleProperty =
        AvaloniaProperty.Register<PricingCalculatorCard, string?>(nameof(LimitTitle));

    public static readonly StyledProperty<string?> LimitDetailProperty =
        AvaloniaProperty.Register<PricingCalculatorCard, string?>(nameof(LimitDetail));

    public static readonly StyledProperty<int> AreaIndexProperty =
        AvaloniaProperty.Register<PricingCalculatorCard, int>(nameof(AreaIndex));

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<PricingCalculatorCard, bool>(nameof(IsExpanded), true);

    public static readonly StyledProperty<double> DimOpacityProperty =
        AvaloniaProperty.Register<PricingCalculatorCard, double>(nameof(DimOpacity), 1);

    private Border? _badge;
    private Button? _action;
    private PricingCalculatorFeatureList? _features;
    private readonly TranslateTransform _badgeTranslate = new();
    private MotionValue<Vector>? _badgeOffset;
    private IReadOnlyList<PricingCalculatorMath.FeatureLine>? _lines;
    private (string Text, bool Selected)? _actionState;

    public PricingCalculatorCard(PricingCalculatorPlan plan)
    {
        Plan = plan;
    }

    /// <summary>The plan shown.</summary>
    public PricingCalculatorPlan Plan { get; }

    public string? PlanName { get => GetValue(PlanNameProperty); set => SetValue(PlanNameProperty, value); }
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public string? BadgeText { get => GetValue(BadgeTextProperty); set => SetValue(BadgeTextProperty, value); }
    public decimal PriceValue { get => GetValue(PriceValueProperty); set => SetValue(PriceValueProperty, value); }
    public decimal TotalValue { get => GetValue(TotalValueProperty); set => SetValue(TotalValueProperty, value); }
    public string? MoneyPrefix { get => GetValue(MoneyPrefixProperty); set => SetValue(MoneyPrefixProperty, value); }
    public string? MoneySuffix { get => GetValue(MoneySuffixProperty); set => SetValue(MoneySuffixProperty, value); }
    public int PriceDecimals { get => GetValue(PriceDecimalsProperty); set => SetValue(PriceDecimalsProperty, value); }
    public int TotalDecimals { get => GetValue(TotalDecimalsProperty); set => SetValue(TotalDecimalsProperty, value); }
    public CultureInfo? Culture { get => GetValue(CultureProperty); set => SetValue(CultureProperty, value); }
    public string? TotalCaption { get => GetValue(TotalCaptionProperty); set => SetValue(TotalCaptionProperty, value); }
    public string? PerSeat { get => GetValue(PerSeatProperty); set => SetValue(PerSeatProperty, value); }
    public string? PerMonth { get => GetValue(PerMonthProperty); set => SetValue(PerMonthProperty, value); }
    public string? LimitTitle { get => GetValue(LimitTitleProperty); set => SetValue(LimitTitleProperty, value); }
    public string? LimitDetail { get => GetValue(LimitDetailProperty); set => SetValue(LimitDetailProperty, value); }

    /// <summary>0: the price block shows; 1: the cap note shows.</summary>
    public int AreaIndex { get => GetValue(AreaIndexProperty); set => SetValue(AreaIndexProperty, value); }

    /// <summary>False folds the description and the features away (stacked layout, not selected).</summary>
    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

    /// <summary>Opacity of the description and features (0.5 above the cap; set by the theme).</summary>
    public double DimOpacity { get => GetValue(DimOpacityProperty); set => SetValue(DimOpacityProperty, value); }

    /// <summary>The plan button (null before the template is applied).</summary>
    public Button? ActionButton => _action;

    /// <summary>The badge (null before the template is applied).</summary>
    public Border? Badge => _badge;

    /// <summary>The feature list (null before the template is applied).</summary>
    public PricingCalculatorFeatureList? Features => _features;

    /// <summary>The badge's current FLIP offset from its place (tests read it).</summary>
    public Vector BadgeOffset => _badgeOffset?.Value ?? default;

    /// <summary>Raised when the plan button is clicked.</summary>
    public event EventHandler<RoutedEventArgs>? ActionInvoked;

    public bool IsSelected
    {
        get => PseudoClasses.Contains(SelectedPseudoClass);
        set => PseudoClasses.Set(SelectedPseudoClass, value);
    }

    public bool IsRecommended
    {
        get => PseudoClasses.Contains(RecommendedPseudoClass);
        set
        {
            PseudoClasses.Set(RecommendedPseudoClass, value);
            if (_badge is not null)
            {
                _badge.IsVisible = value;
            }
            if (!value)
            {
                _badgeOffset?.Jump(default);
                _badgeTranslate.X = 0;
                _badgeTranslate.Y = 0;
            }
        }
    }

    public bool IsUnavailable
    {
        get => PseudoClasses.Contains(UnavailablePseudoClass);
        set => PseudoClasses.Set(UnavailablePseudoClass, value);
    }

    public bool IsCompact
    {
        set => PseudoClasses.Set(CompactPseudoClass, value);
    }

    public bool IsCollapsed
    {
        set
        {
            PseudoClasses.Set(CollapsedPseudoClass, value);
            IsExpanded = !value;
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_action is not null)
        {
            _action.Click -= OnActionClick;
        }
        _badge = e.NameScope.Find<Border>(BadgePart);
        _action = e.NameScope.Find<Button>(ActionPart);
        _features = e.NameScope.Find<PricingCalculatorFeatureList>(FeaturesPart);
        if (_badge is not null)
        {
            _badge.RenderTransform = _badgeTranslate;
            _badge.IsVisible = IsRecommended;
        }
        if (_action is not null)
        {
            _action.Click += OnActionClick;
            if (_actionState is { } state)
            {
                _actionState = null;
                SetAction(state.Text, state.Selected);
            }
        }
        if (_features is not null && _lines is not null)
        {
            _features.SetLines(_lines);
        }
        if (PlanName is { } name)
        {
            AutomationProperties.SetName(this, name);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PlanNameProperty)
        {
            AutomationProperties.SetName(this, PlanName);
        }
    }

    private void OnActionClick(object? sender, RoutedEventArgs e) => ActionInvoked?.Invoke(this, e);

    /// <summary>Shows the feature lines.</summary>
    public void SetLines(IReadOnlyList<PricingCalculatorMath.FeatureLine> lines, string? listName)
    {
        _lines = lines;
        if (_features is not null)
        {
            _features.SetLines(lines);
            AutomationProperties.SetName(_features, listName);
        }
    }

    /// <summary>Sets the plan button: a check and "Current pick" when selected, the offer otherwise.</summary>
    public void SetAction(string text, bool selected)
    {
        if (_actionState is { } current && current.Text == text && current.Selected == selected)
        {
            return;
        }
        _actionState = (text, selected);
        if (_action is null)
        {
            return;
        }
        _action.Variant = selected ? ButtonVariant.Primary : ButtonVariant.Secondary;
        AutomationProperties.SetName(_action, text);
        if (selected)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
            row.Children.Add(new ArcGlyph { Data = ArcIcons.Check, Size = 16, VerticalAlignment = VerticalAlignment.Center });
            row.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
            _action.Content = row;
        }
        else
        {
            _action.Content = text;
        }
    }

    /// <summary>The badge's resting position in <paramref name="ancestor"/> (layout only, without the FLIP offset).</summary>
    public Point? BadgeHome(Visual ancestor)
    {
        if (_badge is null || !_badge.IsVisible)
        {
            return null;
        }
        var x = 0.0;
        var y = 0.0;
        Visual? v = _badge;
        while (v is not null && !ReferenceEquals(v, ancestor))
        {
            x += v.Bounds.X;
            y += v.Bounds.Y;
            v = Avalonia.VisualTree.VisualExtensions.GetVisualParent(v);
        }
        return v is null ? null : new Point(x, y);
    }

    /// <summary>The badge's velocity (px/s) while it glides.</summary>
    public Vector BadgeVelocity => _badgeOffset?.Velocity ?? default;

    /// <summary>Starts the badge's glide from <paramref name="offset"/> away back to its place, with an initial velocity.</summary>
    public void FlyBadgeFrom(Vector offset, Vector velocity)
    {
        if (_badgeOffset is null)
        {
            _badgeOffset = new MotionValue<Vector>(this, default, MotionVectors.Vector);
            _badgeOffset.Changed += v =>
            {
                _badgeTranslate.X = v.X;
                _badgeTranslate.Y = v.Y;
                ZIndex = v == default ? 0 : 1;
            };
        }
        _badgeOffset.Jump(offset);
        _badgeTranslate.X = offset.X;
        _badgeTranslate.Y = offset.Y;
        ZIndex = 1;
        _ = _badgeOffset.AnimateTo(default, MotionSpec.Spring(BadgeGlide), velocity);
    }
}
