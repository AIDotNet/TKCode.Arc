using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// One bullet chart of <see cref="BudgetVariance"/>, drawn in one pass on the block's shared scale (all positions are
/// fractions of the width): three qualitative bands, the actual bar from the left edge to <see cref="Fraction"/>, the part
/// past <see cref="Budget"/> drawn again in <see cref="OverBrush"/> (the same bar clipped to the space past budget, so the
/// color appears exactly as the bar crosses the tick), and the budget tick reaching past the band. Movement is declared in
/// the theme (a smooth spring on <see cref="Fraction"/> under <c>:motion-full</c>), so the bar only draws its current values.
/// </summary>
internal sealed class BudgetBulletBar : Control
{
    public static readonly StyledProperty<double> FractionProperty =
        AvaloniaProperty.Register<BudgetBulletBar, double>(nameof(Fraction));

    public static readonly StyledProperty<double> BudgetProperty =
        AvaloniaProperty.Register<BudgetBulletBar, double>(nameof(Budget), 1);

    public static readonly StyledProperty<double> LowProperty =
        AvaloniaProperty.Register<BudgetBulletBar, double>(nameof(Low), 0.9);

    public static readonly StyledProperty<double> HighProperty =
        AvaloniaProperty.Register<BudgetBulletBar, double>(nameof(High), 1.1);

    public static readonly StyledProperty<double> BandHeightProperty =
        AvaloniaProperty.Register<BudgetBulletBar, double>(nameof(BandHeight), 14);

    public static readonly StyledProperty<double> BarHeightProperty =
        AvaloniaProperty.Register<BudgetBulletBar, double>(nameof(BarHeight), 6);

    public static readonly StyledProperty<double> BandRadiusProperty =
        AvaloniaProperty.Register<BudgetBulletBar, double>(nameof(BandRadius), 3);

    public static readonly StyledProperty<double> TickOverhangProperty =
        AvaloniaProperty.Register<BudgetBulletBar, double>(nameof(TickOverhang), 3);

    public static readonly StyledProperty<double> TickWidthProperty =
        AvaloniaProperty.Register<BudgetBulletBar, double>(nameof(TickWidth), 2);

    public static readonly StyledProperty<IBrush?> BandLowBrushProperty =
        AvaloniaProperty.Register<BudgetBulletBar, IBrush?>(nameof(BandLowBrush));

    public static readonly StyledProperty<IBrush?> BandMidBrushProperty =
        AvaloniaProperty.Register<BudgetBulletBar, IBrush?>(nameof(BandMidBrush));

    public static readonly StyledProperty<IBrush?> BandHighBrushProperty =
        AvaloniaProperty.Register<BudgetBulletBar, IBrush?>(nameof(BandHighBrush));

    public static readonly StyledProperty<IBrush?> BarBrushProperty =
        AvaloniaProperty.Register<BudgetBulletBar, IBrush?>(nameof(BarBrush));

    public static readonly StyledProperty<IBrush?> OverBrushProperty =
        AvaloniaProperty.Register<BudgetBulletBar, IBrush?>(nameof(OverBrush));

    public static readonly StyledProperty<IBrush?> TickBrushProperty =
        AvaloniaProperty.Register<BudgetBulletBar, IBrush?>(nameof(TickBrush));

    static BudgetBulletBar()
    {
        AffectsRender<BudgetBulletBar>(FractionProperty, BudgetProperty, LowProperty, HighProperty, BandHeightProperty, BarHeightProperty,
            BandRadiusProperty, TickOverhangProperty, TickWidthProperty, BandLowBrushProperty, BandMidBrushProperty, BandHighBrushProperty,
            BarBrushProperty, OverBrushProperty, TickBrushProperty);
        AffectsMeasure<BudgetBulletBar>(BandHeightProperty, TickOverhangProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<BudgetBulletBar>(false);
        FocusableProperty.OverrideDefaultValue<BudgetBulletBar>(false);
    }

    public double Fraction { get => GetValue(FractionProperty); set => SetValue(FractionProperty, value); }
    public double Budget { get => GetValue(BudgetProperty); set => SetValue(BudgetProperty, value); }
    public double Low { get => GetValue(LowProperty); set => SetValue(LowProperty, value); }
    public double High { get => GetValue(HighProperty); set => SetValue(HighProperty, value); }
    public double BandHeight { get => GetValue(BandHeightProperty); set => SetValue(BandHeightProperty, value); }
    public double BarHeight { get => GetValue(BarHeightProperty); set => SetValue(BarHeightProperty, value); }
    public double BandRadius { get => GetValue(BandRadiusProperty); set => SetValue(BandRadiusProperty, value); }
    public double TickOverhang { get => GetValue(TickOverhangProperty); set => SetValue(TickOverhangProperty, value); }
    public double TickWidth { get => GetValue(TickWidthProperty); set => SetValue(TickWidthProperty, value); }
    public IBrush? BandLowBrush { get => GetValue(BandLowBrushProperty); set => SetValue(BandLowBrushProperty, value); }
    public IBrush? BandMidBrush { get => GetValue(BandMidBrushProperty); set => SetValue(BandMidBrushProperty, value); }
    public IBrush? BandHighBrush { get => GetValue(BandHighBrushProperty); set => SetValue(BandHighBrushProperty, value); }
    public IBrush? BarBrush { get => GetValue(BarBrushProperty); set => SetValue(BarBrushProperty, value); }
    public IBrush? OverBrush { get => GetValue(OverBrushProperty); set => SetValue(OverBrushProperty, value); }
    public IBrush? TickBrush { get => GetValue(TickBrushProperty); set => SetValue(TickBrushProperty, value); }

    protected override Size MeasureOverride(Size availableSize) => new(0, BandHeight + 2 * TickOverhang);

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        if (width <= 0)
        {
            return;
        }
        var top = TickOverhang;
        var band = new Rect(0, top, width, BandHeight);
        double X(double fraction) => Math.Clamp(fraction, 0, 1) * width;

        using (context.PushClip(new RoundedRect(band, BandRadius)))
        {
            context.FillRectangle(BandLowBrush ?? Brushes.Transparent, new Rect(0, top, X(Low), BandHeight));
            context.FillRectangle(BandMidBrush ?? Brushes.Transparent, new Rect(X(Low), top, X(High) - X(Low), BandHeight));
            context.FillRectangle(BandHighBrush ?? Brushes.Transparent, new Rect(X(High), top, width - X(High), BandHeight));

            var barTop = top + (BandHeight - BarHeight) / 2;
            var end = X(Fraction);
            if (end > 0)
            {
                context.FillRectangle(BarBrush ?? Brushes.Black, new Rect(0, barTop, end, BarHeight));
            }
            var budget = X(Budget);
            if (end > budget && OverBrush is { } over)
            {
                context.FillRectangle(over, new Rect(budget, barTop, end - budget, BarHeight));
            }
        }

        var tick = X(Budget);
        context.FillRectangle(TickBrush ?? Brushes.Black,
            new Rect(Math.Clamp(tick - TickWidth / 2, 0, width - TickWidth), 0, TickWidth, BandHeight + 2 * TickOverhang));
    }
}

/// <summary>
/// The scale above the bullet charts: "0%", "50%", "100%" at their places on the shared scale (the first starts at the
/// left edge, the others are centered on their position and kept inside the width).
/// </summary>
internal sealed class BudgetVarianceScale : Control
{
    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<BudgetVarianceScale, double>(nameof(Maximum), 1.4);

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<BudgetVarianceScale>();

    public static readonly StyledProperty<double> FontSizeProperty =
        TextElement.FontSizeProperty.AddOwner<BudgetVarianceScale>();

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        TextElement.FontFamilyProperty.AddOwner<BudgetVarianceScale>();

    private static readonly (double Share, string Text)[] s_ticks = [(0, "0%"), (0.5, "50%"), (1, "100%")];
    private readonly FinanceText _text = new();

    static BudgetVarianceScale()
    {
        AffectsRender<BudgetVarianceScale>(MaximumProperty, ForegroundProperty, FontSizeProperty, FontFamilyProperty);
        AffectsMeasure<BudgetVarianceScale>(FontSizeProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<BudgetVarianceScale>(false);
    }

    public double Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    protected override Size MeasureOverride(Size availableSize) => new(0, Math.Ceiling(FontSize * 4 / 3));

    public override void Render(DrawingContext context)
    {
        _text.FontFamily = FontFamily;
        var brush = Foreground ?? Brushes.Gray;
        var width = Bounds.Width;
        foreach (var (share, text) in s_ticks)
        {
            var layout = _text.Get(text, FontSize, FontWeight.Normal, brush);
            var x = width * share / Math.Max(Maximum, 1e-6);
            var w = layout.WidthIncludingTrailingWhitespace;
            var left = share == 0 ? 0 : Math.Clamp(x - w / 2, 0, Math.Max(0, width - w));
            layout.Draw(context, new Point(left, (Bounds.Height - layout.Height) / 2));
        }
        _text.Sweep();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FontFamilyProperty || change.Property == ForegroundProperty)
        {
            _text.Clear();
        }
    }
}

/// <summary>The part a child plays in a <see cref="BudgetVarianceRowPanel"/>.</summary>
internal enum BudgetVarianceRole
{
    Lead,
    Chart,
    Actual,
    Budget,
    Variance,
    Chevron,
}

/// <summary>
/// Lays out one row of <see cref="BudgetVariance"/>. As a table (<see cref="IsTable"/>): the name, the chart, actual,
/// budget, variance and the chevron in aligned columns (fixed widths for the numbers, the rest split between name and
/// chart). Stacked: the name and the variance (and the chevron) on top, the chart across the full width below; the actual
/// and budget cells are hidden (the name shows "actual of budget" instead). Every row of the block uses the same column
/// widths, so the budget ticks line up down the table.
/// </summary>
internal sealed class BudgetVarianceRowPanel : Panel
{
    public static readonly AttachedProperty<BudgetVarianceRole> RoleProperty =
        AvaloniaProperty.RegisterAttached<BudgetVarianceRowPanel, Control, BudgetVarianceRole>("Role");

    public static readonly StyledProperty<bool> IsTableProperty =
        AvaloniaProperty.Register<BudgetVarianceRowPanel, bool>(nameof(IsTable), true);

    public static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<BudgetVarianceRowPanel, double>(nameof(ColumnGap), 20);

    public static readonly StyledProperty<double> AmountWidthProperty =
        AvaloniaProperty.Register<BudgetVarianceRowPanel, double>(nameof(AmountWidth), 84);

    public static readonly StyledProperty<double> VarianceWidthProperty =
        AvaloniaProperty.Register<BudgetVarianceRowPanel, double>(nameof(VarianceWidth), 112);

    public static readonly StyledProperty<double> ChevronWidthProperty =
        AvaloniaProperty.Register<BudgetVarianceRowPanel, double>(nameof(ChevronWidth), 16);

    public static readonly StyledProperty<double> NameShareProperty =
        AvaloniaProperty.Register<BudgetVarianceRowPanel, double>(nameof(NameShare), 0.314);

    /// <summary>Vertical gap between the top line and the chart when stacked.</summary>
    public static readonly StyledProperty<double> StackGapProperty =
        AvaloniaProperty.Register<BudgetVarianceRowPanel, double>(nameof(StackGap), 8);

    /// <summary>Left inset of the name cell (budget lines indent past the avatar column).</summary>
    public static readonly StyledProperty<double> LeadInsetProperty =
        AvaloniaProperty.Register<BudgetVarianceRowPanel, double>(nameof(LeadInset));

    static BudgetVarianceRowPanel()
    {
        AffectsMeasure<BudgetVarianceRowPanel>(IsTableProperty, ColumnGapProperty, AmountWidthProperty, VarianceWidthProperty,
            ChevronWidthProperty, NameShareProperty, StackGapProperty, LeadInsetProperty);
        AffectsParentMeasure<BudgetVarianceRowPanel>(RoleProperty);
    }

    public static BudgetVarianceRole GetRole(Control control) => control.GetValue(RoleProperty);
    public static void SetRole(Control control, BudgetVarianceRole value) => control.SetValue(RoleProperty, value);

    public bool IsTable { get => GetValue(IsTableProperty); set => SetValue(IsTableProperty, value); }
    public double ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }
    public double AmountWidth { get => GetValue(AmountWidthProperty); set => SetValue(AmountWidthProperty, value); }
    public double VarianceWidth { get => GetValue(VarianceWidthProperty); set => SetValue(VarianceWidthProperty, value); }
    public double ChevronWidth { get => GetValue(ChevronWidthProperty); set => SetValue(ChevronWidthProperty, value); }
    public double NameShare { get => GetValue(NameShareProperty); set => SetValue(NameShareProperty, value); }
    public double StackGap { get => GetValue(StackGapProperty); set => SetValue(StackGapProperty, value); }
    public double LeadInset { get => GetValue(LeadInsetProperty); set => SetValue(LeadInsetProperty, value); }

    private readonly record struct Columns(double Lead, double Chart, double Actual, double Budget, double Variance, double Chevron);

    private Columns ColumnsFor(double width)
    {
        var gap = ColumnGap;
        var fixedWidth = 2 * AmountWidth + VarianceWidth + ChevronWidth + 5 * gap;
        var flexible = Math.Max(0, width - fixedWidth);
        var lead = flexible * NameShare;
        var chartLeft = lead + gap;
        var actualLeft = chartLeft + (flexible - lead) + gap;
        var budgetLeft = actualLeft + AmountWidth + gap;
        var varianceLeft = budgetLeft + AmountWidth + gap;
        var chevronLeft = varianceLeft + VarianceWidth + gap;
        return new Columns(lead, chartLeft, actualLeft, budgetLeft, varianceLeft, chevronLeft);
    }

    private double WidthOf(BudgetVarianceRole role, double width)
    {
        var c = ColumnsFor(width);
        return role switch
        {
            BudgetVarianceRole.Lead => c.Lead,
            BudgetVarianceRole.Chart => c.Actual - ColumnGap - c.Chart,
            BudgetVarianceRole.Actual or BudgetVarianceRole.Budget => AmountWidth,
            BudgetVarianceRole.Variance => VarianceWidth,
            _ => ChevronWidth,
        };
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 800 : availableSize.Width;
        double top = 0, chart = 0;
        foreach (var child in Children)
        {
            var role = GetRole(child);
            if (!child.IsVisible)
            {
                continue;
            }
            if (IsTable)
            {
                var w = role == BudgetVarianceRole.Lead ? Math.Max(0, WidthOf(role, width) - LeadInset) : WidthOf(role, width);
                child.Measure(new Size(w, double.PositiveInfinity));
                top = Math.Max(top, child.DesiredSize.Height);
            }
            else if (role == BudgetVarianceRole.Chart)
            {
                child.Measure(new Size(width, double.PositiveInfinity));
                chart = child.DesiredSize.Height;
            }
            else
            {
                child.Measure(new Size(StackedWidth(role, width), double.PositiveInfinity));
                top = Math.Max(top, child.DesiredSize.Height);
            }
        }
        var height = IsTable ? top : top + (chart > 0 ? StackGap + chart : 0);
        return new Size(width, height);
    }

    private double StackedWidth(BudgetVarianceRole role, double width) => role switch
    {
        BudgetVarianceRole.Variance => VarianceWidth,
        BudgetVarianceRole.Chevron => ChevronWidth,
        _ => Math.Max(0, width - LeadInset - VarianceWidth - ChevronWidth - 2 * ColumnGap),
    };

    protected override Size ArrangeOverride(Size finalSize)
    {
        var width = finalSize.Width;
        if (IsTable)
        {
            var c = ColumnsFor(width);
            foreach (var child in Children)
            {
                var role = GetRole(child);
                var left = role switch
                {
                    BudgetVarianceRole.Lead => LeadInset,
                    BudgetVarianceRole.Chart => c.Chart,
                    BudgetVarianceRole.Actual => c.Actual,
                    BudgetVarianceRole.Budget => c.Budget,
                    BudgetVarianceRole.Variance => c.Variance,
                    _ => c.Chevron,
                };
                var w = role == BudgetVarianceRole.Lead ? Math.Max(0, c.Lead - LeadInset) : WidthOf(role, width);
                var h = child.DesiredSize.Height;
                child.Arrange(new Rect(left, (finalSize.Height - h) / 2, w, h));
            }
            return finalSize;
        }

        double top = 0;
        foreach (var child in Children)
        {
            if (child.IsVisible && GetRole(child) is not (BudgetVarianceRole.Chart or BudgetVarianceRole.Actual or BudgetVarianceRole.Budget))
            {
                top = Math.Max(top, child.DesiredSize.Height);
            }
        }
        foreach (var child in Children)
        {
            var role = GetRole(child);
            var h = child.DesiredSize.Height;
            switch (role)
            {
                case BudgetVarianceRole.Chart:
                    child.Arrange(new Rect(0, top + StackGap, width, h));
                    break;
                case BudgetVarianceRole.Actual or BudgetVarianceRole.Budget:
                    child.Arrange(default);
                    break;
                case BudgetVarianceRole.Lead:
                    child.Arrange(new Rect(LeadInset, 0, StackedWidth(role, width), h));
                    break;
                case BudgetVarianceRole.Variance:
                    child.Arrange(new Rect(width - ChevronWidth - ColumnGap - VarianceWidth, 0, VarianceWidth, h));
                    break;
                default:
                    child.Arrange(new Rect(width - ChevronWidth, (top - h) / 2, ChevronWidth, h));
                    break;
            }
        }
        return finalSize;
    }
}

/// <summary>
/// The disclosure button of a department row (filled on hover and keyboard focus; themed in BudgetVarianceTheme). It
/// reports expanded or collapsed to screen readers.
/// </summary>
internal sealed class BudgetVarianceRowButton : AvaloniaButton
{
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<BudgetVarianceRowButton, bool>(nameof(IsExpanded));

    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

    protected override Type StyleKeyOverride => typeof(BudgetVarianceRowButton);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsExpandedProperty)
        {
            PseudoClasses.Set(":expanded", IsExpanded);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new Peer(this);

    private sealed class Peer : ButtonAutomationPeer, IExpandCollapseProvider
    {
        public Peer(BudgetVarianceRowButton owner) : base(owner)
        {
        }

        private BudgetVarianceRowButton Button => (BudgetVarianceRowButton)Owner;

        public ExpandCollapseState ExpandCollapseState => Button.IsExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => false;

        public void Expand()
        {
            if (!Button.IsExpanded)
            {
                Invoke();
            }
        }

        public void Collapse()
        {
            if (Button.IsExpanded)
            {
                Invoke();
            }
        }
    }
}

/// <summary>The panel behind a department and its lines: clear while closed, a muted fill while open.</summary>
internal sealed class BudgetVarianceItemHost : Border
{
    protected override Type StyleKeyOverride => typeof(BudgetVarianceItemHost);
}

/// <summary>A department's lines inside their height spring: fade in with the height, out faster.</summary>
internal sealed class BudgetVarianceLinesHost : Border
{
    protected override Type StyleKeyOverride => typeof(BudgetVarianceLinesHost);
}

/// <summary>The chevron of a department row; turns half a turn while the department is open.</summary>
internal sealed class BudgetVarianceChevron : ArcGlyph
{
    protected override Type StyleKeyOverride => typeof(BudgetVarianceChevron);
}

/// <summary>The total row: a hairline under it separates it from the departments.</summary>
internal sealed class BudgetVarianceTotalHost : Border
{
    protected override Type StyleKeyOverride => typeof(BudgetVarianceTotalHost);
}

/// <summary>A budget line inside an open department.</summary>
internal sealed class BudgetVarianceLineHost : Border
{
    protected override Type StyleKeyOverride => typeof(BudgetVarianceLineHost);
}

/// <summary>A legend mark: the actual bar, the budget tick, the overspend or the tolerance band (by class).</summary>
internal sealed class BudgetVarianceSwatch : Border
{
    protected override Type StyleKeyOverride => typeof(BudgetVarianceSwatch);
}
