using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// Children in equal columns that wrap to rows: the sliders (three, one when narrow) and the breakdown (four, two when
/// compact). Every column has the same width, so a changing figure never moves its neighbours.
/// </summary>
internal sealed class LlmCostCalculatorColumns : Panel
{
    public static readonly StyledProperty<int> ColumnsProperty =
        AvaloniaProperty.Register<LlmCostCalculatorColumns, int>(nameof(Columns), 1);

    public static readonly StyledProperty<double> ColumnSpacingProperty =
        AvaloniaProperty.Register<LlmCostCalculatorColumns, double>(nameof(ColumnSpacing));

    public static readonly StyledProperty<double> RowSpacingProperty =
        AvaloniaProperty.Register<LlmCostCalculatorColumns, double>(nameof(RowSpacing));

    static LlmCostCalculatorColumns()
    {
        AffectsMeasure<LlmCostCalculatorColumns>(ColumnsProperty, ColumnSpacingProperty, RowSpacingProperty);
    }

    public int Columns { get => GetValue(ColumnsProperty); set => SetValue(ColumnsProperty, value); }

    public double ColumnSpacing { get => GetValue(ColumnSpacingProperty); set => SetValue(ColumnSpacingProperty, value); }

    public double RowSpacing { get => GetValue(RowSpacingProperty); set => SetValue(RowSpacingProperty, value); }

    private int ColumnCount => Math.Max(1, Columns);

    private double CellWidth(double width) =>
        double.IsFinite(width) ? Math.Max(0, (width - ColumnSpacing * (ColumnCount - 1)) / ColumnCount) : double.PositiveInfinity;

    protected override Size MeasureOverride(Size availableSize)
    {
        var cell = CellWidth(availableSize.Width);
        var visible = Children.Where(c => c.IsVisible).ToList();
        double height = 0;
        double widest = 0;
        for (var start = 0; start < visible.Count; start += ColumnCount)
        {
            double row = 0;
            for (var i = start; i < Math.Min(visible.Count, start + ColumnCount); i++)
            {
                visible[i].Measure(new Size(cell, double.PositiveInfinity));
                row = Math.Max(row, visible[i].DesiredSize.Height);
                widest = Math.Max(widest, visible[i].DesiredSize.Width);
            }
            height += row + (start > 0 ? RowSpacing : 0);
        }
        var width = double.IsFinite(availableSize.Width)
            ? availableSize.Width
            : widest * ColumnCount + ColumnSpacing * (ColumnCount - 1);
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var cell = CellWidth(finalSize.Width);
        var visible = Children.Where(c => c.IsVisible).ToList();
        double y = 0;
        for (var start = 0; start < visible.Count; start += ColumnCount)
        {
            double row = 0;
            for (var i = start; i < Math.Min(visible.Count, start + ColumnCount); i++)
            {
                row = Math.Max(row, visible[i].DesiredSize.Height);
            }
            for (var i = start; i < Math.Min(visible.Count, start + ColumnCount); i++)
            {
                var x = (i - start) * (cell + ColumnSpacing);
                visible[i].Arrange(new Rect(x, y, cell, row));
            }
            y += row + RowSpacing;
        }
        return finalSize;
    }
}

/// <summary>
/// The ranked model list. Every row is laid out at the top and placed at its rank by a render transform on a critically
/// damped 0.24s spring, so a re-rank is one glide with no reflow. Rows keep their order in the visual tree (moving a
/// focused child would drop its focus); the accessibility tree lists them by rank instead.
/// </summary>
internal sealed class LlmCostCalculatorList : Panel
{
    public static readonly StyledProperty<double> RowHeightProperty =
        AvaloniaProperty.Register<LlmCostCalculatorList, double>(nameof(RowHeight), 44);

    static LlmCostCalculatorList()
    {
        AffectsMeasure<LlmCostCalculatorList>(RowHeightProperty);
    }

    public LlmCostCalculatorList()
    {
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Once);
    }

    public double RowHeight { get => GetValue(RowHeightProperty); set => SetValue(RowHeightProperty, value); }

    /// <summary>The rows by rank, cheapest first.</summary>
    public IEnumerable<LlmCostCalculatorRow> Ranked =>
        Children.OfType<LlmCostCalculatorRow>().OrderBy(r => r.Rank);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RowHeightProperty)
        {
            // A new row height (the compact layout) is a layout change, not a re-rank: rows jump to their new places.
            foreach (var row in Children.OfType<LlmCostCalculatorRow>())
            {
                row.PlaceAt(row.Rank * RowHeight, animate: false);
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var rows = Children.OfType<LlmCostCalculatorRow>().ToList();
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 0;
        foreach (var row in rows)
        {
            row.Measure(new Size(availableSize.Width, RowHeight));
            width = Math.Max(width, double.IsFinite(availableSize.Width) ? 0 : row.DesiredSize.Width);
        }
        return new Size(width, rows.Count * RowHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var row in Children.OfType<LlmCostCalculatorRow>())
        {
            row.Arrange(new Rect(0, 0, finalSize.Width, RowHeight));
        }
        return finalSize;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ListPeer(this);

    private sealed class ListPeer(LlmCostCalculatorList owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;

        protected override string? GetNameCore() => AutomationProperties.GetName(Owner);

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore()
        {
            var list = (LlmCostCalculatorList)Owner;
            return [.. list.Ranked.Where(r => r.IsVisible).Select(r => ControlAutomationPeer.CreatePeerForElement(r))];
        }
    }
}

/// <summary>
/// One model in the ranked list: the accent bar of the selection, the mark (or a monogram), the name and provider, a bar
/// relative to the most expensive model and the rolling monthly cost. It is one radio button to assistive technology,
/// named with the model, its provider, its rank and its cost.
/// </summary>
[TemplatePart(MarkPart, typeof(ArcGlyph))]
[TemplatePart(MonogramPart, typeof(Border))]
[TemplatePart(MonogramTextPart, typeof(TextBlock))]
[TemplatePart(NamePart, typeof(TextBlock))]
[TemplatePart(ProviderPart, typeof(TextBlock))]
[TemplatePart(BarPart, typeof(LlmCostCalculatorBar))]
[TemplatePart(CostPart, typeof(RollingDigits))]
[PseudoClasses(SelectedPseudoClass, CompactPseudoClass)]
internal sealed class LlmCostCalculatorRow : TemplatedControl
{
    internal const string MarkPart = "PART_Mark";
    internal const string MonogramPart = "PART_Monogram";
    internal const string MonogramTextPart = "PART_MonogramText";
    internal const string NamePart = "PART_Name";
    internal const string ProviderPart = "PART_Provider";
    internal const string BarPart = "PART_Bar";
    internal const string CostPart = "PART_Cost";
    internal const string SelectedPseudoClass = ":selected";
    internal const string CompactPseudoClass = ":compact";

    /// <summary>Rows move to a new rank on this spring: critically damped, about 240ms.</summary>
    internal static readonly SpringSpec RankSpring = SpringSpec.FromVisual(0.24, 0);

    /// <summary>Every price rolls to its new value over 240ms on CSS ease-out.</summary>
    internal static readonly MotionSpec PriceRoll = MotionSpec.Tween(TimeSpan.FromMilliseconds(240), AiHeightBox.EaseOut);

    private readonly TranslateTransform _shift = new();
    private MotionValue<double>? _y;
    private bool _placed;
    private ArcGlyph? _mark;
    private Border? _monogram;
    private TextBlock? _monogramText;
    private TextBlock? _name;
    private TextBlock? _provider;
    private LlmCostCalculatorBar? _bar;
    private RollingDigits? _cost;
    private LlmCostFigure _figure;
    private double _fraction;
    private bool _selected;
    private bool _compact;

    public LlmCostCalculatorRow(LlmModelPrice model)
    {
        Model = model;
        RenderTransform = _shift;
        Focusable = true;
        IsTabStop = false;
    }

    public LlmModelPrice Model { get; }

    /// <summary>The rank, from 0 (the cheapest).</summary>
    public int Rank { get; private set; } = -1;

    /// <summary>Where the row is drawn now (its spring's value).</summary>
    public double ShownY => _shift.Y;

    /// <summary>Where the row is going.</summary>
    public double TargetY => _y?.Target ?? _shift.Y;

    public LlmCostFigure Figure => _figure;

    public double Fraction => _fraction;

    public bool IsSelected => _selected;

    public RollingDigits? CostDigits => _cost;

    public LlmCostCalculatorBar? Bar => _bar;

    /// <summary>Called when the person picks this row by pointer.</summary>
    public Action<LlmCostCalculatorRow>? Picked { get; set; }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _mark = e.NameScope.Find<ArcGlyph>(MarkPart);
        _monogram = e.NameScope.Find<Border>(MonogramPart);
        _monogramText = e.NameScope.Find<TextBlock>(MonogramTextPart);
        _name = e.NameScope.Find<TextBlock>(NamePart);
        _provider = e.NameScope.Find<TextBlock>(ProviderPart);
        _bar = e.NameScope.Find<LlmCostCalculatorBar>(BarPart);
        _cost = e.NameScope.Find<RollingDigits>(CostPart);
        ShowModel();
        _bar?.SetFraction(_fraction, animate: false);
        if (_cost is not null)
        {
            _cost.WheelMotion = PriceRoll;
            _cost.IsInstant = true;
            ApplyFigure(_cost, _figure);
            _cost.IsInstant = false;
        }
    }

    /// <summary>Writes the model's name, provider and mark (the model's fields are read again on every refresh).</summary>
    public void ShowModel()
    {
        if (_name is not null && _name.Text != Model.Name)
        {
            _name.Text = Model.Name;
        }
        if (_provider is not null && _provider.Text != Model.Provider)
        {
            _provider.Text = Model.Provider;
            _provider.IsVisible = Model.Provider.Length > 0;
        }
        var hasMark = Model.Mark is not null;
        if (_mark is not null)
        {
            _mark.Data = Model.Mark;
            _mark.IsVisible = hasMark;
            if (Model.MarkBrush is { } brush)
            {
                _mark.Foreground = brush;
            }
            else
            {
                _mark.ClearValue(ArcGlyph.ForegroundProperty);
            }
        }
        if (_monogram is not null)
        {
            _monogram.IsVisible = !hasMark;
        }
        if (_monogramText is not null)
        {
            var source = Model.Provider.Length > 0 ? Model.Provider : Model.Name;
            _monogramText.Text = source.Length > 0 ? char.ToUpperInvariant(source[0]).ToString() : string.Empty;
        }
    }

    /// <summary>Sets the rank and the row's place; a change of rank glides there, the first placement jumps.</summary>
    public void SetRank(int rank, double rowHeight, bool animate)
    {
        Rank = rank;
        PlaceAt(rank * rowHeight, animate);
    }

    public void PlaceAt(double y, bool animate)
    {
        _y ??= CreateY();
        if (!_placed || !animate || !IsLoaded)
        {
            _placed = true;
            _y.Jump(y);
            _shift.Y = y;
            return;
        }
        if (_y.Target != y)
        {
            _ = _y.AnimateTo(y, MotionSpec.Spring(RankSpring));
        }
    }

    private MotionValue<double> CreateY()
    {
        var value = new MotionValue<double>(this, 0, MotionVectors.Double);
        value.Changed += y => _shift.Y = y;
        return value;
    }

    /// <summary>The bar's share of the most expensive model and the monthly cost.</summary>
    public void SetCost(LlmCostFigure figure, double fraction, bool animate)
    {
        _fraction = fraction;
        _bar?.SetFraction(fraction, animate);
        if (_figure == figure)
        {
            return;
        }
        _figure = figure;
        if (_cost is not null)
        {
            _cost.IsInstant = !animate;
            ApplyFigure(_cost, figure);
            _cost.IsInstant = false;
        }
    }

    public void SetSelected(bool selected)
    {
        _selected = selected;
        IsTabStop = selected;
        PseudoClasses.Set(SelectedPseudoClass, selected);
    }

    public void SetCompact(bool compact)
    {
        _compact = compact;
        PseudoClasses.Set(CompactPseudoClass, compact);
    }

    internal bool IsCompact => _compact;

    internal static void ApplyFigure(RollingDigits digits, LlmCostFigure figure)
    {
        digits.Prefix = "$";
        digits.Suffix = figure.Suffix.Length > 0 ? figure.Suffix : null;
        digits.Decimals = figure.Decimals;
        digits.Value = figure.Value;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        e.Handled = true;
        Picked?.Invoke(this);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new RowPeer(this);

    private sealed class RowPeer(LlmCostCalculatorRow owner) : ControlAutomationPeer(owner), ISelectionItemProvider
    {
        private LlmCostCalculatorRow Row => (LlmCostCalculatorRow)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.RadioButton;

        protected override string? GetNameCore() => AutomationProperties.GetName(Owner);

        // The row's name carries everything; its texts and rolling figures stay out of the tree.
        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;

        public bool IsSelected => Row.IsSelected;

        public ISelectionProvider? SelectionContainer => null;

        public void Select() => Row.Picked?.Invoke(Row);

        public void AddToSelection() => Row.Picked?.Invoke(Row);

        public void RemoveFromSelection()
        {
        }
    }
}

/// <summary>
/// A row's cost bar: a quiet track and a fill at the model's share of the most expensive model. The fill follows the rank
/// spring (critically damped, 0.24s) and is drawn here, so a drag that moves eight bars lays nothing out.
/// </summary>
internal sealed class LlmCostCalculatorBar : Control
{
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<LlmCostCalculatorBar, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<IBrush?> TrackProperty =
        AvaloniaProperty.Register<LlmCostCalculatorBar, IBrush?>(nameof(Track));

    public static readonly StyledProperty<double> ThicknessProperty =
        AvaloniaProperty.Register<LlmCostCalculatorBar, double>(nameof(Thickness), 6);

    private MotionValue<double>? _fraction;
    private double _shown;
    private bool _placed;

    static LlmCostCalculatorBar()
    {
        AffectsRender<LlmCostCalculatorBar>(FillProperty, TrackProperty, ThicknessProperty);
        AffectsMeasure<LlmCostCalculatorBar>(ThicknessProperty);
    }

    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }

    public IBrush? Track { get => GetValue(TrackProperty); set => SetValue(TrackProperty, value); }

    public double Thickness { get => GetValue(ThicknessProperty); set => SetValue(ThicknessProperty, value); }

    /// <summary>The fill drawn now, from 0 to 1.</summary>
    public double ShownFraction => _shown;

    /// <summary>Where the fill is going.</summary>
    public double TargetFraction => _fraction?.Target ?? _shown;

    public void SetFraction(double fraction, bool animate)
    {
        fraction = double.IsFinite(fraction) ? Math.Clamp(fraction, 0, 1) : 0;
        if (_fraction is null)
        {
            _fraction = new MotionValue<double>(this, fraction, MotionVectors.Double);
            _fraction.Changed += value =>
            {
                _shown = value;
                InvalidateVisual();
            };
        }
        if (!_placed || !animate || !IsLoaded)
        {
            _placed = true;
            _fraction.Jump(fraction);
            _shown = fraction;
            InvalidateVisual();
            return;
        }
        if (_fraction.Target != fraction)
        {
            _ = _fraction.AnimateTo(fraction, MotionSpec.Spring(LlmCostCalculatorRow.RankSpring));
        }
    }

    protected override Size MeasureOverride(Size availableSize) => new(0, Thickness);

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        var height = Math.Min(Thickness, Bounds.Height);
        if (width <= 0 || height <= 0)
        {
            return;
        }
        var top = (Bounds.Height - height) / 2;
        var radius = height / 2;
        if (Track is { } track)
        {
            context.DrawRectangle(track, null, new Rect(0, top, width, height), radius, radius);
        }
        if (Fill is { } fill && _shown > 0)
        {
            // A share too small to see still shows as a dot.
            var fillWidth = Math.Max(height, width * _shown);
            context.DrawRectangle(fill, null, new Rect(0, top, Math.Min(width, fillWidth), height), radius, radius);
        }
    }
}

/// <summary>
/// The loading placeholder: rows with the list's exact geometry (a mark, the name and provider, the bar and the cost),
/// drawn here and pulsing while shown. The pulse runs only with full motion; reduced motion holds it still.
/// </summary>
internal sealed class LlmCostCalculatorSkeleton : Control
{
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<LlmCostCalculatorSkeleton, IBrush?>(nameof(Fill));

    /// <summary>How many rows to draw before any model has arrived.</summary>
    public static readonly StyledProperty<int> PlaceholderRowsProperty =
        AvaloniaProperty.Register<LlmCostCalculatorSkeleton, int>(nameof(PlaceholderRows), 8);

    /// <summary>How many models the list holds; when above zero the placeholder draws that many rows.</summary>
    public static readonly StyledProperty<int> ModelRowsProperty =
        AvaloniaProperty.Register<LlmCostCalculatorSkeleton, int>(nameof(ModelRows));

    public static readonly StyledProperty<double> RowHeightProperty =
        AvaloniaProperty.Register<LlmCostCalculatorSkeleton, double>(nameof(RowHeight), 44);

    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<LlmCostCalculatorSkeleton, bool>(nameof(IsCompact));

    public static readonly StyledProperty<Thickness> RowPaddingProperty =
        AvaloniaProperty.Register<LlmCostCalculatorSkeleton, Thickness>(nameof(RowPadding));

    public static readonly StyledProperty<double> CostWidthProperty =
        AvaloniaProperty.Register<LlmCostCalculatorSkeleton, double>(nameof(CostWidth), 84);

    public static readonly StyledProperty<double> PulseLowProperty =
        AvaloniaProperty.Register<LlmCostCalculatorSkeleton, double>(nameof(PulseLow), 0.45);

    public static readonly StyledProperty<double> PulsePeriodProperty =
        AvaloniaProperty.Register<LlmCostCalculatorSkeleton, double>(nameof(PulsePeriod), 1.4);

    public static readonly StyledProperty<bool> IsPulsingProperty =
        AvaloniaProperty.Register<LlmCostCalculatorSkeleton, bool>(nameof(IsPulsing));

    private readonly LiveLoop _pulse;
    private double _phase;
    private double _alpha = 1;

    static LlmCostCalculatorSkeleton()
    {
        AffectsRender<LlmCostCalculatorSkeleton>(FillProperty, PlaceholderRowsProperty, ModelRowsProperty, RowHeightProperty, IsCompactProperty, RowPaddingProperty,
            CostWidthProperty);
        AffectsMeasure<LlmCostCalculatorSkeleton>(PlaceholderRowsProperty, ModelRowsProperty, RowHeightProperty);
    }

    public LlmCostCalculatorSkeleton()
    {
        IsHitTestVisible = false;
        _pulse = new LiveLoop(this, dt =>
        {
            _phase = (_phase + dt / Math.Max(0.2, PulsePeriod)) % 1;
            // A soft breath between full and the low opacity.
            var wave = (1 + Math.Cos(_phase * Math.PI * 2)) / 2;
            _alpha = PulseLow + (1 - PulseLow) * wave;
            InvalidateVisual();
            return true;
        });
        _pulse.RunningChanged += running =>
        {
            if (!running)
            {
                _alpha = 1;
                InvalidateVisual();
            }
        };
    }

    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }

    public int PlaceholderRows { get => GetValue(PlaceholderRowsProperty); set => SetValue(PlaceholderRowsProperty, value); }

    public int ModelRows { get => GetValue(ModelRowsProperty); set => SetValue(ModelRowsProperty, value); }

    /// <summary>The rows drawn: one per model, or the placeholder count before any model has arrived.</summary>
    public int Rows => ModelRows > 0 ? ModelRows : Math.Max(1, PlaceholderRows);

    public double RowHeight { get => GetValue(RowHeightProperty); set => SetValue(RowHeightProperty, value); }

    public bool IsCompact { get => GetValue(IsCompactProperty); set => SetValue(IsCompactProperty, value); }

    public Thickness RowPadding { get => GetValue(RowPaddingProperty); set => SetValue(RowPaddingProperty, value); }

    public double CostWidth { get => GetValue(CostWidthProperty); set => SetValue(CostWidthProperty, value); }

    public double PulseLow { get => GetValue(PulseLowProperty); set => SetValue(PulseLowProperty, value); }

    public double PulsePeriod { get => GetValue(PulsePeriodProperty); set => SetValue(PulsePeriodProperty, value); }

    /// <summary>Whether the pulse is wanted (while prices load); it still runs only when shown and with full motion.</summary>
    public bool IsPulsing { get => GetValue(IsPulsingProperty); set => SetValue(IsPulsingProperty, value); }

    internal LiveLoop Pulse => _pulse;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsPulsingProperty)
        {
            if (IsPulsing)
            {
                _pulse.Start();
            }
            else
            {
                _pulse.Stop();
                _alpha = 1;
                InvalidateVisual();
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize) => new(0, Rows * RowHeight);

    public override void Render(DrawingContext context)
    {
        if (Fill is not { } fill)
        {
            return;
        }
        using var _ = context.PushOpacity(_alpha);
        var width = Bounds.Width;
        var pad = RowPadding;
        var compact = IsCompact;
        for (var i = 0; i < Rows; i++)
        {
            var top = i * RowHeight;
            var left = pad.Left;
            var right = width - pad.Right;
            var mid = top + (compact ? 20 : RowHeight / 2);
            context.DrawRectangle(fill, null, new Rect(left, mid - 8, 16, 16), 4, 4);
            var textLeft = left + 26;
            var nameWidth = Math.Max(0, Math.Min(140, (right - textLeft) * 0.32) - (i % 3) * 14);
            context.DrawRectangle(fill, null, new Rect(textLeft, mid - 5, nameWidth, 10), 5, 5);
            var costLeft = right - CostWidth * 0.7;
            context.DrawRectangle(fill, null, new Rect(costLeft, mid - 5, right - costLeft, 10), 5, 5);
            var barLeft = compact ? textLeft : textLeft + Math.Max(0, (right - CostWidth - textLeft) * 0.4);
            var barRight = compact ? right : right - CostWidth - 10;
            var barY = compact ? top + RowHeight - 18 : mid - 3;
            if (barRight > barLeft)
            {
                context.DrawRectangle(fill, null, new Rect(barLeft, barY, barRight - barLeft, 6), 3, 3);
            }
        }
    }
}
