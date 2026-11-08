using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

using Avalonia.Media.Immutable;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

/// <summary>
/// A hidden live region (Arc's visually hidden <c>aria-live</c> text): screen readers say its name whenever it changes, and
/// the same text said twice is read again. Takes no space and no input.
/// </summary>
internal sealed class LiveCheckoutAnnouncer : Control
{
    private string? _last;
    private bool _toggle;

    public LiveCheckoutAnnouncer()
    {
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
        IsHitTestVisible = false;
        Focusable = false;
    }

    /// <summary>Every announcement, oldest first (at most 64; tests read it).</summary>
    public List<string> History { get; } = new();

    /// <summary>Announces <paramref name="text"/>; empty text is ignored.</summary>
    public void Announce(string? text, bool assertive = false)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        AutomationProperties.SetLiveSetting(this, assertive ? AutomationLiveSetting.Assertive : AutomationLiveSetting.Polite);
        _toggle = text == _last && !_toggle;
        _last = text;
        History.Add(text);
        if (History.Count > 64)
        {
            History.RemoveAt(0);
        }
        AutomationProperties.SetName(this, _toggle ? text + "​" : text);
    }

    protected override Size MeasureOverride(Size availableSize) => default;
}

/// <summary>A heading the block can focus from code (Arc's <c>h2</c> with <c>tabindex=-1</c>): never a tab stop, no ring.</summary>
internal sealed class LiveCheckoutHeading : TextBlock
{
    static LiveCheckoutHeading()
    {
        FocusableProperty.OverrideDefaultValue<LiveCheckoutHeading>(true);
        KeyboardNavigation.IsTabStopProperty.OverrideDefaultValue<LiveCheckoutHeading>(false);
        FocusAdornerProperty.OverrideDefaultValue<LiveCheckoutHeading>(null);
    }

    protected override Type StyleKeyOverride => typeof(TextBlock);
}

/// <summary>The receipt's dashed separator (1px, Arc's <c>border-top: 1px dashed</c>).</summary>
internal sealed class LiveCheckoutDashedRule : Control
{
    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<LiveCheckoutDashedRule, IBrush?>(nameof(Stroke));

    private Pen? _pen;

    static LiveCheckoutDashedRule()
    {
        AffectsRender<LiveCheckoutDashedRule>(StrokeProperty);
        HeightProperty.OverrideDefaultValue<LiveCheckoutDashedRule>(1);
    }

    public IBrush? Stroke { get => GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StrokeProperty)
        {
            _pen = null;
        }
    }

    public override void Render(DrawingContext context)
    {
        if (Stroke is null)
        {
            return;
        }
        _pen ??= new Pen(Stroke, 1, new DashStyle(new double[] { 3, 3 }, 0));
        context.DrawLine(_pen, new Point(0, 0.5), new Point(Bounds.Width, 0.5));
    }
}

/// <summary>
/// The block's buttons, styled by class in the theme: <c>wallet</c>, <c>pay</c>, <c>text</c> (Use test card, Add coupon),
/// <c>step</c> (seat −/+), <c>apply</c>, <c>chip-remove</c>, <c>pill</c> (Receipt PDF), <c>ghost</c> (Start over) and
/// <c>bar</c> (the narrow summary toggle). Hover only answers a mouse or pen.
/// </summary>
[PseudoClasses(HoverPseudoClass, BusyPseudoClass)]
internal sealed class LiveCheckoutButton : AvaloniaButton
{
    public const string HoverPseudoClass = ":hover";
    public const string BusyPseudoClass = ":busy";

    public static readonly StyledProperty<bool> IsBusyProperty =
        AvaloniaProperty.Register<LiveCheckoutButton, bool>(nameof(IsBusy));

    /// <summary>True shows the busy state and ignores presses (the button keeps its focus).</summary>
    public bool IsBusy { get => GetValue(IsBusyProperty); set => SetValue(IsBusyProperty, value); }

    protected override Type StyleKeyOverride => typeof(LiveCheckoutButton);

    protected override void OnClick()
    {
        if (IsBusy)
        {
            return;
        }
        base.OnClick();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsBusyProperty)
        {
            PseudoClasses.Set(BusyPseudoClass, IsBusy);
        }
        else if (change.Property == IsEffectivelyEnabledProperty && !IsEffectivelyEnabled)
        {
            PseudoClasses.Set(HoverPseudoClass, false);
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        PseudoClasses.Set(HoverPseudoClass, e.Pointer.Type != PointerType.Touch && IsEffectivelyEnabled);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }
}

/// <summary>
/// One plan card of the plan radio group: name, the rolling seat price with its unit, and the blurb; stacked layouts put
/// the price on the right. The selection ring is not part of the card: it glides between cards in
/// <see cref="LiveCheckoutTileGrid"/>. Disabled cards (while paying) do not fade.
/// </summary>
[PseudoClasses(HoverPseudoClass, StackedPseudoClass)]
internal sealed class LiveCheckoutPlanCard : RadioButton
{
    public const string HoverPseudoClass = ":hover";
    public const string StackedPseudoClass = ":stacked";

    public static readonly StyledProperty<string?> PlanNameProperty =
        AvaloniaProperty.Register<LiveCheckoutPlanCard, string?>(nameof(PlanName));

    public static readonly StyledProperty<string?> BlurbProperty =
        AvaloniaProperty.Register<LiveCheckoutPlanCard, string?>(nameof(Blurb));

    public static readonly StyledProperty<decimal> PriceProperty =
        AvaloniaProperty.Register<LiveCheckoutPlanCard, decimal>(nameof(Price));

    public static readonly StyledProperty<string> CurrencyProperty =
        AvaloniaProperty.Register<LiveCheckoutPlanCard, string>(nameof(Currency), "USD");

    public static readonly StyledProperty<CultureInfo?> CultureProperty =
        AvaloniaProperty.Register<LiveCheckoutPlanCard, CultureInfo?>(nameof(Culture));

    public static readonly StyledProperty<string?> UnitProperty =
        AvaloniaProperty.Register<LiveCheckoutPlanCard, string?>(nameof(Unit));

    public static readonly StyledProperty<bool> IsStackedProperty =
        AvaloniaProperty.Register<LiveCheckoutPlanCard, bool>(nameof(IsStacked));

    public LiveCheckoutPlanCard(LiveCheckoutPlan plan)
    {
        Plan = plan;
        PlanName = plan.Name;
        Blurb = plan.Blurb;
        AutomationProperties.SetName(this, plan.Name);
    }

    public LiveCheckoutPlan Plan { get; }

    public string? PlanName { get => GetValue(PlanNameProperty); set => SetValue(PlanNameProperty, value); }
    public string? Blurb { get => GetValue(BlurbProperty); set => SetValue(BlurbProperty, value); }
    public decimal Price { get => GetValue(PriceProperty); set => SetValue(PriceProperty, value); }
    public string Currency { get => GetValue(CurrencyProperty); set => SetValue(CurrencyProperty, value); }
    public CultureInfo? Culture { get => GetValue(CultureProperty); set => SetValue(CultureProperty, value); }
    public string? Unit { get => GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
    public bool IsStacked { get => GetValue(IsStackedProperty); set => SetValue(IsStackedProperty, value); }

    protected override Type StyleKeyOverride => typeof(LiveCheckoutPlanCard);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsStackedProperty)
        {
            PseudoClasses.Set(StackedPseudoClass, IsStacked);
        }
        else if (change.Property == IsCheckedProperty)
        {
            // Roving tab stop: only the selected card is reached with Tab.
            KeyboardNavigation.SetIsTabStop(this, IsChecked == true);
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        PseudoClasses.Set(HoverPseudoClass, e.Pointer.Type != PointerType.Touch);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }
}

/// <summary>
/// Lays out its children in <see cref="Columns"/> equal columns with <see cref="Gap"/> between them (rows as tall as their
/// tallest child): the wallet buttons, the plan cards and the city / postal code pair. For the plans it also moves the
/// selection ring (1px accent edge over a 6% accent fill) to the selected card: a glide on the smooth spring when the
/// selection changes, a jump when only the layout changes.
/// </summary>
internal sealed class LiveCheckoutTileGrid : Panel
{
    private GlidingIndicator? _ring;
    private Control? _selected;
    private bool _selectionMoved;

    public static readonly StyledProperty<int> ColumnsProperty =
        AvaloniaProperty.Register<LiveCheckoutTileGrid, int>(nameof(Columns), 3);

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<LiveCheckoutTileGrid, double>(nameof(Gap), 8);

    static LiveCheckoutTileGrid()
    {
        AffectsMeasure<LiveCheckoutTileGrid>(ColumnsProperty, GapProperty);
    }

    /// <summary>Columns (one when the block is stacked).</summary>
    public int Columns { get => GetValue(ColumnsProperty); set => SetValue(ColumnsProperty, value); }

    /// <summary>Gap between tiles, both ways.</summary>
    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    /// <summary>The selection ring, created on first use (the block sets its colors and radius).</summary>
    public GlidingIndicator Ring
    {
        get
        {
            if (_ring is null)
            {
                _ring = new GlidingIndicator { IsHitTestVisible = false, StrokeThickness = 1 };
                Children.Add(_ring);
            }
            return _ring;
        }
    }

    /// <summary>The tiles (every child but the ring).</summary>
    public IEnumerable<Control> Tiles => Children.Where(c => !ReferenceEquals(c, _ring) && c.IsVisible);

    /// <summary>Marks <paramref name="tile"/> selected; the ring glides there on the next arrange when <paramref name="animate"/>.</summary>
    public void Select(Control? tile, bool animate)
    {
        if (ReferenceEquals(tile, _selected))
        {
            return;
        }
        _selected = tile;
        _selectionMoved = animate;
        InvalidateArrange();
    }

    /// <summary>Replaces the tiles (the ring stays).</summary>
    public void SetTiles(IEnumerable<Control> tiles)
    {
        for (var i = Children.Count - 1; i >= 0; i--)
        {
            if (!ReferenceEquals(Children[i], _ring))
            {
                Children.RemoveAt(i);
            }
        }
        var index = 0;
        foreach (var tile in tiles)
        {
            Children.Insert(index++, tile);
        }
        _selected = null;
    }

    private int EffectiveColumns(int count) => Math.Max(1, Math.Min(Columns, Math.Max(1, count)));

    protected override Size MeasureOverride(Size availableSize)
    {
        var tiles = Tiles.ToList();
        var columns = EffectiveColumns(tiles.Count);
        var width = double.IsInfinity(availableSize.Width) ? 600 : availableSize.Width;
        var cell = Math.Max(0, (width - Gap * (columns - 1)) / columns);
        var height = 0.0;
        var rowHeight = 0.0;
        var rows = 0;
        for (var i = 0; i < tiles.Count; i++)
        {
            tiles[i].Measure(new Size(cell, double.PositiveInfinity));
            rowHeight = Math.Max(rowHeight, tiles[i].DesiredSize.Height);
            if ((i + 1) % columns == 0 || i == tiles.Count - 1)
            {
                height += rowHeight + (rows > 0 ? Gap : 0);
                rows++;
                rowHeight = 0;
            }
        }
        _ring?.Measure(availableSize);
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var tiles = Tiles.ToList();
        var columns = EffectiveColumns(tiles.Count);
        var cell = Math.Max(0, (finalSize.Width - Gap * (columns - 1)) / columns);
        var y = 0.0;
        for (var start = 0; start < tiles.Count; start += columns)
        {
            var rowHeight = 0.0;
            for (var i = start; i < Math.Min(tiles.Count, start + columns); i++)
            {
                rowHeight = Math.Max(rowHeight, tiles[i].DesiredSize.Height);
            }
            for (var i = start; i < Math.Min(tiles.Count, start + columns); i++)
            {
                tiles[i].Arrange(new Rect((i - start) * (cell + Gap), y, cell, rowHeight));
            }
            y += rowHeight + Gap;
        }
        if (_ring is not null)
        {
            _ring.Arrange(new Rect(finalSize));
            if (_selected is not null)
            {
                var animate = _selectionMoved && _ring.HasTarget && LiveCheckoutMotion.CanAnimate(this);
                _ring.MoveTo(_selected.Bounds, LiveCheckoutMotion.Smooth, animate);
            }
            else
            {
                _ring.Hide(false);
            }
            _selectionMoved = false;
        }
        return finalSize;
    }
}

/// <summary>One segment of the billing switch (both segments are tab stops, as measured).</summary>
[PseudoClasses(HoverPseudoClass)]
internal sealed class LiveCheckoutBillingOption : RadioButton
{
    public const string HoverPseudoClass = ":hover";

    public static readonly StyledProperty<string?> BadgeProperty =
        AvaloniaProperty.Register<LiveCheckoutBillingOption, string?>(nameof(Badge));

    public string? Badge { get => GetValue(BadgeProperty); set => SetValue(BadgeProperty, value); }

    protected override Type StyleKeyOverride => typeof(LiveCheckoutBillingOption);

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        PseudoClasses.Set(HoverPseudoClass, e.Pointer.Type != PointerType.Touch);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }
}

/// <summary>
/// The billing period switch: a pill track with Monthly and Yearly (the yearly discount in the success color) and a
/// surface pill that glides to the chosen segment on the smooth spring.
/// </summary>
[TemplatePart(PillPart, typeof(GlidingIndicator))]
[TemplatePart(MonthlyPart, typeof(LiveCheckoutBillingOption))]
[TemplatePart(YearlyPart, typeof(LiveCheckoutBillingOption))]
internal sealed class LiveCheckoutBillingSwitch : TemplatedControl
{
    public const string PillPart = "PART_Pill";
    public const string MonthlyPart = "PART_Monthly";
    public const string YearlyPart = "PART_Yearly";

    public static readonly StyledProperty<LiveCheckoutBilling> BillingProperty =
        AvaloniaProperty.Register<LiveCheckoutBillingSwitch, LiveCheckoutBilling>(nameof(Billing), LiveCheckoutBilling.Yearly);

    public static readonly StyledProperty<string?> MonthlyLabelProperty =
        AvaloniaProperty.Register<LiveCheckoutBillingSwitch, string?>(nameof(MonthlyLabel));

    public static readonly StyledProperty<string?> YearlyLabelProperty =
        AvaloniaProperty.Register<LiveCheckoutBillingSwitch, string?>(nameof(YearlyLabel));

    public static readonly StyledProperty<string?> BadgeProperty =
        AvaloniaProperty.Register<LiveCheckoutBillingSwitch, string?>(nameof(Badge));

    private GlidingIndicator? _pill;
    private LiveCheckoutBillingOption? _monthly;
    private LiveCheckoutBillingOption? _yearly;
    private bool _moved;

    public LiveCheckoutBilling Billing { get => GetValue(BillingProperty); set => SetValue(BillingProperty, value); }
    public string? MonthlyLabel { get => GetValue(MonthlyLabelProperty); set => SetValue(MonthlyLabelProperty, value); }
    public string? YearlyLabel { get => GetValue(YearlyLabelProperty); set => SetValue(YearlyLabelProperty, value); }
    public string? Badge { get => GetValue(BadgeProperty); set => SetValue(BadgeProperty, value); }

    /// <summary>The user picked a period.</summary>
    public event EventHandler<LiveCheckoutBilling>? Picked;

    internal LiveCheckoutBillingOption? MonthlyOption => _monthly;
    internal LiveCheckoutBillingOption? YearlyOption => _yearly;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_monthly is not null)
        {
            _monthly.IsCheckedChanged -= OnOptionChecked;
            _monthly.KeyDown -= OnOptionKeyDown;
        }
        if (_yearly is not null)
        {
            _yearly.IsCheckedChanged -= OnOptionChecked;
            _yearly.KeyDown -= OnOptionKeyDown;
        }
        _pill = e.NameScope.Find<GlidingIndicator>(PillPart);
        _monthly = e.NameScope.Find<LiveCheckoutBillingOption>(MonthlyPart);
        _yearly = e.NameScope.Find<LiveCheckoutBillingOption>(YearlyPart);
        Sync();
        if (_monthly is not null)
        {
            _monthly.IsCheckedChanged += OnOptionChecked;
            _monthly.KeyDown += OnOptionKeyDown;
        }
        if (_yearly is not null)
        {
            _yearly.IsCheckedChanged += OnOptionChecked;
            _yearly.KeyDown += OnOptionKeyDown;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BillingProperty)
        {
            _moved = true;
            Sync();
            InvalidateArrange();
        }
    }

    private void Sync()
    {
        if (_monthly is not null)
        {
            _monthly.IsChecked = Billing == LiveCheckoutBilling.Monthly;
        }
        if (_yearly is not null)
        {
            _yearly.IsChecked = Billing == LiveCheckoutBilling.Yearly;
        }
    }

    private void OnOptionChecked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is LiveCheckoutBillingOption { IsChecked: true } option)
        {
            var billing = ReferenceEquals(option, _monthly) ? LiveCheckoutBilling.Monthly : LiveCheckoutBilling.Yearly;
            if (billing != Billing)
            {
                Billing = billing;
                Picked?.Invoke(this, billing);
            }
        }
    }

    // Our addition (the reference's arrow keys were not observed): Left/Right move between the two periods.
    private void OnOptionKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Left or Key.Right or Key.Up or Key.Down))
        {
            return;
        }
        var next = Billing == LiveCheckoutBilling.Monthly ? _yearly : _monthly;
        if (next is not null)
        {
            next.IsChecked = true;
            next.Focus(NavigationMethod.Directional);
        }
        e.Handled = true;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        var target = Billing == LiveCheckoutBilling.Monthly ? _monthly : _yearly;
        if (_pill is not null && target is not null && _pill.GetVisualParent() is Visual host &&
            target.TranslatePoint(default, host) is { } origin)
        {
            var animate = _moved && _pill.HasTarget && LiveCheckoutMotion.CanAnimate(this);
            _pill.MoveTo(new Rect(origin, target.Bounds.Size), LiveCheckoutMotion.Smooth, animate);
            _moved = false;
        }
        return size;
    }
}

/// <summary>The seat count box: digits only, clamped as you type; ↑/↓ step by one (Shift by ten), an addition of ours.</summary>
internal sealed class LiveCheckoutSeatBox : TextBox
{
    protected override Type StyleKeyOverride => typeof(LiveCheckoutSeatBox);
}

/// <summary>
/// The seat stepper: − / the count / +, from <see cref="Minimum"/> to <see cref="Maximum"/>; the button at a bound is disabled.
/// Typing keeps only digits and clamps at once: an emptied box reads as the minimum, 999 becomes the maximum.
/// </summary>
[TemplatePart(MinusPart, typeof(LiveCheckoutButton))]
[TemplatePart(BoxPart, typeof(LiveCheckoutSeatBox))]
[TemplatePart(PlusPart, typeof(LiveCheckoutButton))]
internal sealed class LiveCheckoutSeatStepper : TemplatedControl
{
    public const string MinusPart = "PART_Minus";
    public const string BoxPart = "PART_Box";
    public const string PlusPart = "PART_Plus";

    public static readonly StyledProperty<int> ValueProperty =
        AvaloniaProperty.Register<LiveCheckoutSeatStepper, int>(nameof(Value), 8);

    public static readonly StyledProperty<int> MinimumProperty =
        AvaloniaProperty.Register<LiveCheckoutSeatStepper, int>(nameof(Minimum), 1);

    public static readonly StyledProperty<int> MaximumProperty =
        AvaloniaProperty.Register<LiveCheckoutSeatStepper, int>(nameof(Maximum), 500);

    public static readonly StyledProperty<string?> RemoveLabelProperty =
        AvaloniaProperty.Register<LiveCheckoutSeatStepper, string?>(nameof(RemoveLabel));

    public static readonly StyledProperty<string?> AddLabelProperty =
        AvaloniaProperty.Register<LiveCheckoutSeatStepper, string?>(nameof(AddLabel));

    public static readonly StyledProperty<string?> BoxLabelProperty =
        AvaloniaProperty.Register<LiveCheckoutSeatStepper, string?>(nameof(BoxLabel));

    private LiveCheckoutButton? _minus;
    private LiveCheckoutButton? _plus;
    private LiveCheckoutSeatBox? _box;
    private bool _syncing;

    public int Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public int Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public int Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public string? RemoveLabel { get => GetValue(RemoveLabelProperty); set => SetValue(RemoveLabelProperty, value); }
    public string? AddLabel { get => GetValue(AddLabelProperty); set => SetValue(AddLabelProperty, value); }
    public string? BoxLabel { get => GetValue(BoxLabelProperty); set => SetValue(BoxLabelProperty, value); }

    internal LiveCheckoutSeatBox? Box => _box;
    internal LiveCheckoutButton? MinusButton => _minus;
    internal LiveCheckoutButton? PlusButton => _plus;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_minus is not null)
        {
            _minus.Click -= OnMinus;
        }
        if (_plus is not null)
        {
            _plus.Click -= OnPlus;
        }
        if (_box is not null)
        {
            _box.TextChanged -= OnTextChanged;
            _box.RemoveHandler(KeyDownEvent, OnBoxKeyDown);
        }
        _minus = e.NameScope.Find<LiveCheckoutButton>(MinusPart);
        _plus = e.NameScope.Find<LiveCheckoutButton>(PlusPart);
        _box = e.NameScope.Find<LiveCheckoutSeatBox>(BoxPart);
        if (_minus is not null)
        {
            _minus.Click += OnMinus;
        }
        if (_plus is not null)
        {
            _plus.Click += OnPlus;
        }
        if (_box is not null)
        {
            _box.TextChanged += OnTextChanged;
            // Tunnel: the text box would take Up / Down for its caret first.
            _box.AddHandler(KeyDownEvent, OnBoxKeyDown, RoutingStrategies.Tunnel);
        }
        Sync();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty || change.Property == MinimumProperty || change.Property == MaximumProperty ||
            change.Property == RemoveLabelProperty || change.Property == AddLabelProperty || change.Property == BoxLabelProperty)
        {
            Sync();
        }
    }

    private void Sync()
    {
        _syncing = true;
        try
        {
            var text = Value.ToString(CultureInfo.InvariantCulture);
            if (_box is not null && _box.Text != text)
            {
                _box.Text = text;
                _box.CaretIndex = text.Length;
            }
            if (_minus is not null)
            {
                _minus.IsEnabled = Value > Minimum;
                AutomationProperties.SetName(_minus, RemoveLabel);
            }
            if (_plus is not null)
            {
                _plus.IsEnabled = Value < Maximum;
                AutomationProperties.SetName(_plus, AddLabel);
            }
            if (_box is not null)
            {
                AutomationProperties.SetName(_box, BoxLabel);
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private void OnMinus(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Value = Math.Max(Minimum, Value - 1);

    private void OnPlus(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Value = Math.Min(Maximum, Value + 1);

    private void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_syncing || _box is null)
        {
            return;
        }
        var value = LiveCheckoutValidation.ParseSeats(_box.Text, Minimum, Maximum);
        var text = value.ToString(CultureInfo.InvariantCulture);
        if (value == Value)
        {
            // Same value, but the box may hold letters or a clamped number: put the clean text back.
            Sync();
            return;
        }
        Value = value;
        if (_box.Text != text)
        {
            Sync();
        }
    }

    private void OnBoxKeyDown(object? sender, KeyEventArgs e)
    {
        var step = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 10 : 1;
        if (e.Key == Key.Up)
        {
            Value = Math.Clamp(Value + step, Minimum, Maximum);
            e.Handled = true;
        }
        else if (e.Key == Key.Down)
        {
            Value = Math.Clamp(Value - step, Minimum, Maximum);
            e.Handled = true;
        }
    }
}

/// <summary>
/// One add-on row (a real check box): an 18px box whose tick draws on the ω 20 spring, the name and description, and the
/// flat monthly price. The row tints on hover; disabled rows (while paying) do not fade.
/// </summary>
[TemplatePart(TickPart, typeof(StrokeDrawGlyph))]
[PseudoClasses(HoverPseudoClass)]
internal sealed class LiveCheckoutAddonRow : CheckBox
{
    public const string TickPart = "PART_Tick";
    public const string HoverPseudoClass = ":hover";

    public static readonly StyledProperty<string?> AddonNameProperty =
        AvaloniaProperty.Register<LiveCheckoutAddonRow, string?>(nameof(AddonName));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<LiveCheckoutAddonRow, string?>(nameof(Description));

    public static readonly StyledProperty<string?> PriceTextProperty =
        AvaloniaProperty.Register<LiveCheckoutAddonRow, string?>(nameof(PriceText));

    public static readonly StyledProperty<string?> UnitProperty =
        AvaloniaProperty.Register<LiveCheckoutAddonRow, string?>(nameof(Unit));

    private StrokeDrawGlyph? _tick;
    private MotionValue<double>? _draw;

    public LiveCheckoutAddonRow(LiveCheckoutAddon addon)
    {
        Addon = addon;
        AddonName = addon.Name;
        Description = addon.Description;
        AutomationProperties.SetName(this, addon.Name);
    }

    public LiveCheckoutAddon Addon { get; }

    public string? AddonName { get => GetValue(AddonNameProperty); set => SetValue(AddonNameProperty, value); }
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public string? PriceText { get => GetValue(PriceTextProperty); set => SetValue(PriceTextProperty, value); }
    public string? Unit { get => GetValue(UnitProperty); set => SetValue(UnitProperty, value); }

    /// <summary>How far the tick is drawn (tests).</summary>
    internal double TickProgress => _draw?.Value ?? (IsChecked == true ? 1 : 0);

    protected override Type StyleKeyOverride => typeof(LiveCheckoutAddonRow);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _tick = e.NameScope.Find<StrokeDrawGlyph>(TickPart);
        ApplyTick(IsChecked == true ? 1 : 0);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsCheckedProperty)
        {
            // Code-driven: the tick's stroke length and opacity follow one spring that keeps its speed when toggled again.
            _draw ??= CreateDraw();
            var target = IsChecked == true ? 1 : 0;
            if (LiveCheckoutMotion.CanAnimate(this))
            {
                _ = _draw.AnimateTo(target, MotionSpec.Spring(LiveCheckoutMotion.Roll));
            }
            else
            {
                _draw.Jump(target);
            }
        }
    }

    private MotionValue<double> CreateDraw()
    {
        var value = new MotionValue<double>(this, IsChecked == true ? 1 : 0, MotionVectors.Double);
        value.Changed += ApplyTick;
        return value;
    }

    private void ApplyTick(double progress)
    {
        if (_tick is null)
        {
            return;
        }
        var p = Math.Clamp(progress, 0, 1);
        _tick.Progress = p;
        _tick.Opacity = p;
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        PseudoClasses.Set(HoverPseudoClass, e.Pointer.Type != PointerType.Touch);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }
}

/// <summary>One summary row: title and detail on the left, the rolling amount on the right, a rule under it.</summary>
internal sealed class LiveCheckoutSummaryRow : TemplatedControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<LiveCheckoutSummaryRow, string?>(nameof(Title));

    public static readonly StyledProperty<string?> DetailProperty =
        AvaloniaProperty.Register<LiveCheckoutSummaryRow, string?>(nameof(Detail));

    public static readonly StyledProperty<decimal> AmountProperty =
        AvaloniaProperty.Register<LiveCheckoutSummaryRow, decimal>(nameof(Amount));

    public static readonly StyledProperty<string> CurrencyProperty =
        AvaloniaProperty.Register<LiveCheckoutSummaryRow, string>(nameof(Currency), "USD");

    public static readonly StyledProperty<CultureInfo?> CultureProperty =
        AvaloniaProperty.Register<LiveCheckoutSummaryRow, CultureInfo?>(nameof(Culture));

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string? Detail { get => GetValue(DetailProperty); set => SetValue(DetailProperty, value); }
    public decimal Amount { get => GetValue(AmountProperty); set => SetValue(AmountProperty, value); }
    public string Currency { get => GetValue(CurrencyProperty); set => SetValue(CurrencyProperty, value); }
    public CultureInfo? Culture { get => GetValue(CultureProperty); set => SetValue(CultureProperty, value); }
}
