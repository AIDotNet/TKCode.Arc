using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

public partial class CreditTopUp
{
    /// <summary>What the status line says, by priority: purchase state, then a settings notice, then the resting line.</summary>
    internal enum StatusKind
    {
        Resting,
        Charging,
        Done,
        Error,
        Notice
    }

    private Border? _card;
    private Grid? _head;
    private Grid? _chartBox;
    private TextBlock? _balanceTitle;
    private ArcContentSwap? _healthSwap;
    private CreditTopUpOdometer? _balance;
    private ArcContentSwap? _daysSwap;
    private CreditTopUpUsageChart? _chart;
    private ArcContentSwap? _chartLabel;
    private TextBlock? _addTitle;
    private TextBlock? _rate;
    private CreditTopUpChoiceGroup? _packs;
    private Slider? _slider;
    private TextBlock? _autoTitle;
    private TextBlock? _autoSubtitle;
    private Switch? _autoSwitch;
    private TextBlock? _thresholdTitle;
    private CreditTopUpChoiceGroup? _thresholds;
    private ArcContentSwap? _cardInfo;
    private Button? _change;
    private CreditTopUpOdometer? _total;
    private ArcContentSwap? _bonusLine;
    private CreditTopUpOdometer? _price;
    private Button? _buy;
    private ArcContentSwap? _status;

    // The bonus line keeps one rolling row while a bonus applies, so its figure rolls instead of crossfading.
    private CreditTopUpOdometer? _bonusFigure;
    private string? _bonusKey;
    private string? _healthKey;
    private string? _daysKey;
    private string? _chartKey;
    private string? _cardKey;
    private string? _statusKey;
    private string? _buyKey;

    private bool _syncing;
    private bool _isNarrow;
    private bool _isCompact;
    private CancellationTokenSource? _purchase;
    private CancellationTokenSource? _dwell;
    private CancellationTokenSource? _notice;
    private string? _noticeText;
    private CreditTopUpOrder? _lastOrder;

    /// <summary>The status line's current kind and text (tests).</summary>
    internal (StatusKind Kind, string Text) StatusLine { get; private set; }

    /// <summary>The pack options (tests).</summary>
    internal CreditTopUpChoiceGroup? PackGroup => _packs;

    /// <summary>The threshold options (tests).</summary>
    internal CreditTopUpChoiceGroup? ThresholdGroup => _thresholds;

    /// <summary>The usage bars (tests).</summary>
    internal CreditTopUpUsageChart? Chart => _chart;

    /// <summary>The balance figure (tests).</summary>
    internal CreditTopUpOdometer? BalanceFigure => _balance;

    /// <summary>The checkout total figure (tests).</summary>
    internal CreditTopUpOdometer? TotalFigure => _total;

    /// <summary>The checkout price figure (tests).</summary>
    internal CreditTopUpOdometer? PriceFigure => _price;

    /// <summary>The selected card, or null without cards.</summary>
    internal CreditTopUpPaymentMethod? SelectedPaymentMethod
    {
        get
        {
            var cards = PaymentMethods;
            if (cards is null || cards.Count == 0)
            {
                return null;
            }
            foreach (var card in cards)
            {
                if (card.Id == SelectedPaymentMethodId)
                {
                    return card;
                }
            }
            return cards[0];
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_packs is not null)
        {
            _packs.Picked -= OnPackPicked;
        }
        if (_thresholds is not null)
        {
            _thresholds.Picked -= OnThresholdPicked;
        }
        if (_slider is not null)
        {
            _slider.ValueChanged -= OnSliderChanged;
        }
        if (_autoSwitch is not null)
        {
            _autoSwitch.IsCheckedChanged -= OnAutoSwitched;
        }
        if (_change is not null)
        {
            _change.Click -= OnChangeClick;
        }
        if (_buy is not null)
        {
            _buy.Click -= OnBuyClick;
        }
        if (_chart is not null)
        {
            _chart.PropertyChanged -= OnChartPropertyChanged;
        }

        var scope = e.NameScope;
        _card = scope.Find<Border>("PART_Card");
        _head = scope.Find<Grid>(HeadPart);
        _chartBox = scope.Find<Grid>(ChartBoxPart);
        _balanceTitle = scope.Find<TextBlock>(BalanceTitlePart);
        _healthSwap = scope.Find<ArcContentSwap>(HealthPart);
        _balance = scope.Find<CreditTopUpOdometer>(BalancePart);
        _daysSwap = scope.Find<ArcContentSwap>(DaysLeftPart);
        _chart = scope.Find<CreditTopUpUsageChart>(ChartPart);
        _chartLabel = scope.Find<ArcContentSwap>(ChartLabelPart);
        _addTitle = scope.Find<TextBlock>(AddTitlePart);
        _rate = scope.Find<TextBlock>(RatePart);
        _packs = scope.Find<CreditTopUpChoiceGroup>(PacksPart);
        _slider = scope.Find<Slider>(SliderPart);
        _autoTitle = scope.Find<TextBlock>(AutoTitlePart);
        _autoSubtitle = scope.Find<TextBlock>(AutoSubtitlePart);
        _autoSwitch = scope.Find<Switch>(AutoSwitchPart);
        _thresholdTitle = scope.Find<TextBlock>(ThresholdTitlePart);
        _thresholds = scope.Find<CreditTopUpChoiceGroup>(ThresholdsPart);
        _cardInfo = scope.Find<ArcContentSwap>(CardInfoPart);
        _change = scope.Find<Button>(ChangePart);
        _total = scope.Find<CreditTopUpOdometer>(TotalPart);
        _bonusLine = scope.Find<ArcContentSwap>(BonusLinePart);
        _price = scope.Find<CreditTopUpOdometer>(PricePart);
        _buy = scope.Find<Button>(BuyPart);
        _status = scope.Find<ArcContentSwap>(StatusPart);

        if (_packs is not null)
        {
            _packs.Picked += OnPackPicked;
        }
        if (_thresholds is not null)
        {
            _thresholds.Picked += OnThresholdPicked;
        }
        if (_slider is not null)
        {
            _slider.ValueChanged += OnSliderChanged;
        }
        if (_autoSwitch is not null)
        {
            _autoSwitch.IsCheckedChanged += OnAutoSwitched;
        }
        if (_change is not null)
        {
            _change.Click += OnChangeClick;
        }
        if (_buy is not null)
        {
            _buy.Click += OnBuyClick;
        }
        if (_chart is not null)
        {
            _chart.PropertyChanged += OnChartPropertyChanged;
        }

        _healthKey = _daysKey = _chartKey = _cardKey = _statusKey = _buyKey = _bonusKey = null;
        _bonusFigure = null;
        RefreshAll();
        ApplyBreakpoints(Bounds.Width);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var p = change.Property;
        if (p == BalanceProperty)
        {
            RecomputeHealth();
            if (_balance is not null)
            {
                _balance.Value = Math.Max(0, Balance);
            }
            UpdateHealthAndDays();
        }
        else if (p == UsageProperty)
        {
            RecomputeHealth();
            UpdateChart();
            UpdateHealthAndDays();
        }
        else if (p == AutoTopUpThresholdProperty)
        {
            RecomputeHealth();
            UpdateHealthAndDays();
            UpdateThresholds();
        }
        else if (p == SelectedCreditsProperty)
        {
            var snapped = CreditTopUpMath.Snap(SelectedCredits, Range);
            if (snapped != SelectedCredits)
            {
                SetCurrentValue(SelectedCreditsProperty, snapped);
                return;
            }
            ClearError();
            UpdateQuote();
        }
        else if (p == PricingProperty || p == PacksProperty)
        {
            UpdateQuote();
            UpdatePacks();
            UpdateRate();
        }
        else if (p == RangeProperty)
        {
            UpdateSlider();
            SetCurrentValue(SelectedCreditsProperty, CreditTopUpMath.Snap(SelectedCredits, Range));
        }
        else if (p == PaymentMethodsProperty || p == SelectedPaymentMethodIdProperty)
        {
            if (p == SelectedPaymentMethodIdProperty)
            {
                ClearError();
            }
            UpdateCard();
            UpdateStatus();
        }
        else if (p == ThresholdsProperty)
        {
            UpdateThresholds();
        }
        else if (p == IsAutoTopUpEnabledProperty)
        {
            PseudoClasses.Set(AutoOffPseudoClass, !IsAutoTopUpEnabled);
            UpdateThresholds();
            if (_autoSwitch is not null && _autoSwitch.IsChecked != IsAutoTopUpEnabled)
            {
                _syncing = true;
                _autoSwitch.IsChecked = IsAutoTopUpEnabled;
                _syncing = false;
            }
        }
        else if (p == LabelsProperty || p == CultureNameProperty)
        {
            _healthKey = _daysKey = _chartKey = _cardKey = _statusKey = _buyKey = _bonusKey = null;
            RefreshAll();
        }
        else if (p == BoundsProperty)
        {
            ApplyBreakpoints(change.GetNewValue<Rect>().Width);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // A charge in flight is cancelled with the card (Arc aborts the purchase on unmount); timers stop.
        _purchase?.Cancel();
        _dwell?.Cancel();
        _notice?.Cancel();
        _noticeText = null;
        if (Phase is CreditTopUpPhase.Processing or CreditTopUpPhase.Done)
        {
            Phase = CreditTopUpPhase.Idle;
            UpdatePhaseVisuals();
        }
    }

    private void RecomputeHealth()
    {
        Health = CreditTopUpMath.Health(Balance, AutoTopUpThreshold);
        AverageDailyUsage = CreditTopUpMath.AverageDaily(Usage);
        DaysLeft = CreditTopUpMath.DaysLeft(Balance, AverageDailyUsage);
    }

    private void RefreshAll()
    {
        PseudoClasses.Set(AutoOffPseudoClass, !IsAutoTopUpEnabled);
        var labels = Labels;
        AutomationProperties.SetName(this, labels.CardName);
        if (_balanceTitle is not null)
        {
            _balanceTitle.Text = labels.BalanceTitle;
        }
        if (_balance is not null)
        {
            _balance.Culture = Culture;
            _balance.Unit = labels.Unit;
            _balance.Value = Math.Max(0, Balance);
        }
        if (_addTitle is not null)
        {
            _addTitle.Text = labels.AddTitle;
        }
        if (_autoTitle is not null)
        {
            _autoTitle.Text = labels.AutoTitle;
        }
        if (_autoSubtitle is not null)
        {
            _autoSubtitle.Text = labels.AutoSubtitle;
        }
        if (_autoSwitch is not null)
        {
            AutomationProperties.SetName(_autoSwitch, labels.AutoTitle);
            _syncing = true;
            _autoSwitch.IsChecked = IsAutoTopUpEnabled;
            _syncing = false;
        }
        if (_thresholdTitle is not null)
        {
            _thresholdTitle.Text = labels.ThresholdTitle;
        }
        if (_change is not null)
        {
            _change.Content = labels.Change;
        }
        UpdateHealthAndDays();
        UpdateChart();
        UpdateRate();
        UpdateSlider();
        UpdateQuote();
        UpdatePacks();
        UpdateThresholds();
        UpdateCard();
        UpdatePhaseVisuals();
    }

    private void ApplyBreakpoints(double width)
    {
        if (width <= 0)
        {
            return;
        }
        var card = Math.Min(width, _card is { } c && !double.IsNaN(c.MaxWidth) && c.MaxWidth > 0 ? c.MaxWidth : width);
        var narrow = card <= NarrowWidth;
        var compact = card <= CompactWidth;
        PseudoClasses.Set(CompactPseudoClass, compact);
        _isCompact = compact;
        if (narrow == _isNarrow && _head is not null && _chartBox is not null && Grid.GetRow(_chartBox) == (narrow ? 1 : 0))
        {
            return;
        }
        _isNarrow = narrow;
        PseudoClasses.Set(NarrowPseudoClass, narrow);
        LayoutHead();
    }

    /// <summary>Beside the balance (wide) or under it, stretched, with the label on the right (narrow).</summary>
    private void LayoutHead()
    {
        if (_chartBox is null || _chart is null || _chartLabel is null)
        {
            return;
        }
        if (_isNarrow)
        {
            Grid.SetRow(_chartBox, 1);
            Grid.SetColumn(_chartBox, 0);
            Grid.SetColumnSpan(_chartBox, 2);
            Grid.SetRow(_chartLabel, 0);
            Grid.SetColumn(_chartLabel, 1);
            Grid.SetColumnSpan(_chart, 1);
            _chart.IsStretched = true;
            _chartLabel.VerticalAlignment = VerticalAlignment.Center;
        }
        else
        {
            Grid.SetRow(_chartBox, 0);
            Grid.SetColumn(_chartBox, 1);
            Grid.SetColumnSpan(_chartBox, 1);
            Grid.SetRow(_chartLabel, 1);
            Grid.SetColumn(_chartLabel, 0);
            Grid.SetColumnSpan(_chart, 2);
            _chart.IsStretched = false;
            _chartLabel.VerticalAlignment = VerticalAlignment.Top;
        }
        Grid.SetColumnSpan(_chartLabel, _isNarrow ? 1 : 2);
    }

    #region Content

    private static (string Prefix, string Suffix) Split(string format)
    {
        var at = format.IndexOf("{0}", StringComparison.Ordinal);
        return at < 0 ? (format, string.Empty) : (format[..at], format[(at + 3)..]);
    }

    private string Count(long value) => CreditTopUpMoney.Count(value, Culture);

    private string Money(decimal amount) => CreditTopUpMoney.Format(amount, Pricing.Currency, Culture);

    private string MoneyShort(decimal amount) => CreditTopUpMoney.FormatShort(amount, Pricing.Currency, Culture);

    private static string Format(string format, params object[] args)
    {
        try
        {
            return string.Format(CultureInfo.InvariantCulture, format, args);
        }
        catch (FormatException)
        {
            return format;
        }
    }

    private TextBlock Small(string text, ArcDesignTokenKind color)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = 13,
            LineHeight = 18.2,
            LetterSpacing = -0.16,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        ArcTokens.Bind(block, TextBlock.ForegroundProperty, color);
        return block;
    }

    private static ArcGlyph Glyph(Geometry data, double size = 14)
        => new() { Data = data, Size = size, VerticalAlignment = VerticalAlignment.Center };

    private void UpdateHealthAndDays()
    {
        var labels = Labels;
        if (_healthSwap is not null)
        {
            var low = Health == CreditTopUpHealth.RunningLow;
            var key = low ? "low:" + labels.HealthLow : "ok:" + labels.HealthOk;
            if (key != _healthKey)
            {
                _healthKey = key;
                var icon = Glyph(low ? ArcIcons.TriangleAlert : ArcIcons.CircleCheck);
                if (low)
                {
                    icon.Bind(ArcGlyph.ForegroundProperty, this.GetResourceObservable(CreditTopUpTokens.Key(CreditTopUpTokenKind.LowIconColor), ToBrush));
                }
                else
                {
                    ArcTokens.Bind(icon, ArcGlyph.ForegroundProperty, ArcDesignTokenKind.Success);
                }
                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
                row.Children.Add(icon);
                row.Children.Add(Small(low ? labels.HealthLow : labels.HealthOk, ArcDesignTokenKind.TextSecondary));
                _healthSwap.Content = row;
            }
        }
        if (_daysSwap is not null)
        {
            var average = AverageDailyUsage;
            _daysSwap.IsVisible = average is not null;
            if (average is { } rate)
            {
                var days = DaysLeft ?? 0;
                var text = Format(days <= 0 ? labels.DaysLeftNoneFormat : labels.DaysLeftFormat,
                    Count(days), Count((long)Math.Round(rate, MidpointRounding.AwayFromZero)));
                if (text != _daysKey)
                {
                    _daysKey = text;
                    _daysSwap.Content = Small(text, ArcDesignTokenKind.TextMuted);
                }
            }
        }
    }

    private static object? ToBrush(object? value) => value is Color color ? new SolidColorBrush(color) : value;

    private void UpdateChart()
    {
        var usage = Usage;
        var has = usage is { Count: > 0 };
        if (_chartBox is not null)
        {
            _chartBox.IsVisible = has;
        }
        if (_chart is null)
        {
            return;
        }
        _chart.Usage = usage;
        var labels = Labels;
        if (has)
        {
            var average = AverageDailyUsage ?? 0;
            AutomationProperties.SetName(_chart, Format(labels.ChartNameFormat, Count(usage!.Count),
                Count((long)Math.Round(average, MidpointRounding.AwayFromZero))));
        }
        UpdateChartLabel();
    }

    private void OnChartPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == CreditTopUpUsageChart.HoveredIndexProperty)
        {
            UpdateChartLabel();
        }
    }

    private void UpdateChartLabel()
    {
        if (_chartLabel is null)
        {
            return;
        }
        var usage = Usage;
        var labels = Labels;
        var index = _chart?.HoveredIndex ?? -1;
        string text;
        if (usage is { Count: > 0 } && index >= 0 && index < usage.Count)
        {
            var day = usage[index];
            var date = index == usage.Count - 1 ? labels.Today : day.Date.ToString(labels.ChartDateFormat, Culture);
            text = Format(labels.ChartReadoutFormat, date, Count(day.Credits));
        }
        else
        {
            text = Format(labels.ChartIdleFormat, Count(usage?.Count ?? 0));
        }
        if (text == _chartKey)
        {
            return;
        }
        _chartKey = text;
        var block = Small(text, ArcDesignTokenKind.TextMuted);
        block.TextAlignment = TextAlignment.Right;
        _chartLabel.Content = block;
    }

    private void UpdateRate()
    {
        if (_rate is not null)
        {
            _rate.Text = Format(Labels.RateFormat, CreditTopUpMoney.Rate(CreditTopUpMath.RatePerUnit(Pricing), Pricing.Currency, Culture),
                Count(CreditTopUpMath.RateUnit));
        }
    }

    private void UpdateSlider()
    {
        if (_slider is null)
        {
            return;
        }
        var range = Range;
        var labels = Labels;
        _syncing = true;
        _slider.Minimum = range.Minimum;
        _slider.Maximum = Math.Max(range.Minimum + 1, range.Maximum);
        _slider.SmallChange = Math.Max(1, range.Step);
        _slider.LargeChange = CreditTopUpMath.PageStep(range);
        _slider.Label = labels.CustomTitle;
        var culture = Culture;
        var format = labels.CustomValueFormat;
        _slider.ValueFormatter = v => Format(format, ((long)Math.Round(v)).ToString("N0", culture));
        _slider.Value = SelectedCredits;
        _syncing = false;
    }

    private void UpdatePacks()
    {
        if (_packs is null)
        {
            return;
        }
        var packs = Packs ?? [];
        var labels = Labels;
        var pricing = Pricing;
        var contents = new List<object>(packs.Count);
        var names = new List<string>(packs.Count);
        foreach (var pack in packs)
        {
            var quote = CreditTopUpMath.Quote(pack, pricing);
            var percent = CreditTopUpMath.BonusPercent(pack, pricing.BonusTiers);
            var price = MoneyShort(quote.Price);
            var percentText = percent.ToString("0.##", Culture);
            contents.Add(PackContent(Count(pack), price, percent > 0 ? Format(labels.BonusBadgeFormat, percentText) : null));
            names.Add(percent > 0
                ? Format(labels.PackBonusNameFormat, Count(pack), price, percentText)
                : Format(labels.PackNameFormat, Count(pack), price));
        }
        _packs.SetOptions(contents, names, "pack");
        _packs.SelectedIndex = CreditTopUpMath.PackIndex(packs, SelectedCredits);
    }

    private Control PackContent(string amount, string price, string? badge)
    {
        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto"), RowSpacing = 4 };
        var amountText = new TextBlock
        {
            Text = amount,
            FontSize = 14,
            LineHeight = 19.6,
            LetterSpacing = -0.16,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        ArcTokens.Bind(amountText, TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        ArcTokens.Bind(amountText, TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
        grid.Children.Add(amountText);
        // Price on the left, badge on the right; in a very narrow pack the badge follows the price and may reach past
        // the pack's padding rather than be cut (as the reference does at 320 wide).
        var row = new Grid { Height = 20, ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        Grid.SetRow(row, 1);
        var priceText = Small(price, ArcDesignTokenKind.TextMuted);
        priceText.TextTrimming = TextTrimming.None;
        row.Children.Add(priceText);
        if (badge is not null)
        {
            var badgeText = new TextBlock
            {
                Text = badge,
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center
            };
            badgeText.Bind(TextBlock.ForegroundProperty,
                this.GetResourceObservable(CreditTopUpTokens.Key(CreditTopUpTokenKind.BadgeForeground), ToBrush));
            var pill = new Border
            {
                Height = 20,
                Padding = new Thickness(6, 0),
                Margin = new Thickness(4, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = badgeText
            }.BindCornerRadius(ArcDesignTokenKind.RadiusPill);
            pill.Bind(Border.BackgroundProperty, this.GetResourceObservable(CreditTopUpTokens.Key(CreditTopUpTokenKind.BadgeBackground), ToBrush));
            Grid.SetColumn(pill, 2);
            row.Children.Add(pill);
        }
        grid.Children.Add(row);
        return grid;
    }

    private void UpdateThresholds()
    {
        if (_thresholds is null)
        {
            return;
        }
        var thresholds = Thresholds ?? [];
        var contents = new List<object>(thresholds.Count);
        var names = new List<string>(thresholds.Count);
        foreach (var t in thresholds)
        {
            contents.Add(Count(t));
            names.Add(Format(Labels.CustomValueFormat, Count(t)));
        }
        _thresholds.SetOptions(contents, names, "threshold");
        var index = -1;
        for (var i = 0; i < thresholds.Count; i++)
        {
            if (thresholds[i] == AutoTopUpThreshold)
            {
                index = i;
            }
        }
        _thresholds.SelectedIndex = index;
        _thresholds.IsEnabled = IsAutoTopUpEnabled;
        AutomationProperties.SetName(_thresholds, Labels.ThresholdTitle);
    }

    private void UpdateCard()
    {
        var labels = Labels;
        var cards = PaymentMethods ?? [];
        var card = SelectedPaymentMethod;
        if (_change is not null)
        {
            // One card has nothing to change to; none hides it too.
            _change.IsVisible = cards.Count > 1;
            AutomationProperties.SetName(_change, Format(labels.ChangeNameFormat, cards.Count));
        }
        if (_cardInfo is null)
        {
            return;
        }
        var key = card is null ? "none:" + labels.NoCard : $"{card.Id}|{card.DisplayBrand}|{card.Last4}|{card.ExpiryMonth}|{card.ExpiryYear}|{labels.CardTitleFormat}|{labels.CardExpiresFormat}";
        if (key == _cardKey)
        {
            return;
        }
        _cardKey = key;
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        var mark = new CreditTopUpCardMark
        {
            Brand = card?.Brand ?? CreditTopUpCardBrand.Generic,
            Width = 34,
            Height = 22,
            VerticalAlignment = VerticalAlignment.Center
        };
        BindMark(mark);
        row.Children.Add(mark);
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var title = new TextBlock
        {
            Text = card is null ? labels.NoCard : Format(labels.CardTitleFormat, card.DisplayBrand, card.Last4),
            FontSize = 14,
            LineHeight = 19.6,
            LetterSpacing = -0.16,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        ArcTokens.Bind(title, TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
        text.Children.Add(title);
        if (card is not null)
        {
            var expires = $"{card.ExpiryMonth:00}/{card.ExpiryYear:0000}";
            text.Children.Add(Small(Format(labels.CardExpiresFormat, expires), ArcDesignTokenKind.TextMuted));
        }
        row.Children.Add(text);
        _cardInfo.Content = row;
    }

    private void BindMark(CreditTopUpCardMark mark)
    {
        mark.Bind(CreditTopUpCardMark.PlateBrushProperty, this.GetResourceObservable(CreditTopUpTokens.Key(CreditTopUpTokenKind.MarkBackground), ToBrush));
        mark.Bind(CreditTopUpCardMark.OutlineBrushProperty, this.GetResourceObservable(CreditTopUpTokens.Key(CreditTopUpTokenKind.MarkBorder), ToBrush));
        mark.Bind(CreditTopUpCardMark.NimbusBrushProperty, this.GetResourceObservable(CreditTopUpTokens.Key(CreditTopUpTokenKind.NimbusInk), ToBrush));
        mark.Bind(CreditTopUpCardMark.OrbitDiscBrushProperty, this.GetResourceObservable(CreditTopUpTokens.Key(CreditTopUpTokenKind.OrbitDisc), ToBrush));
        mark.Bind(CreditTopUpCardMark.OrbitRingBrushProperty, this.GetResourceObservable(CreditTopUpTokens.Key(CreditTopUpTokenKind.OrbitRing), ToBrush));
        ArcTokens.Bind(mark, CreditTopUpCardMark.GlyphBrushProperty, ArcDesignTokenKind.TextSecondary);
    }

    private void UpdateQuote()
    {
        var quote = CreditTopUpMath.Quote(SelectedCredits, Pricing);
        CurrentQuote = quote;
        var labels = Labels;
        var culture = Culture;
        if (_slider is not null && !_syncing && Math.Abs(_slider.Value - SelectedCredits) > 0.5)
        {
            _syncing = true;
            _slider.Value = SelectedCredits;
            _syncing = false;
        }
        if (_packs is not null)
        {
            _packs.SelectedIndex = CreditTopUpMath.PackIndex(Packs, SelectedCredits);
        }
        if (_total is not null)
        {
            var (prefix, suffix) = Split(labels.TotalFormat);
            _total.Culture = culture;
            _total.Prefix = prefix;
            _total.Suffix = suffix;
            _total.Value = quote.Total;
        }
        if (_price is not null)
        {
            var shape = CreditTopUpMoney.ShapeOf(Pricing.Currency, culture);
            _price.Culture = culture;
            _price.Prefix = shape.Prefix;
            _price.Suffix = shape.Suffix;
            _price.Decimals = shape.Decimals;
            var scale = 1m;
            for (var i = 0; i < shape.Decimals; i++)
            {
                scale *= 10;
            }
            _price.Value = (long)decimal.Round(quote.Price * scale, MidpointRounding.AwayFromZero);
            AutomationProperties.SetName(_price, Money(quote.Price));
        }
        UpdateBonusLine(quote);
    }

    private void UpdateBonusLine(CreditTopUpQuote quote)
    {
        if (_bonusLine is null)
        {
            return;
        }
        var labels = Labels;
        if (quote.Bonus > 0)
        {
            var key = "bonus:" + labels.BonusIncludedFormat;
            if (key != _bonusKey || _bonusFigure is null)
            {
                _bonusKey = key;
                var (prefix, suffix) = Split(labels.BonusIncludedFormat);
                _bonusFigure = new CreditTopUpOdometer
                {
                    FontSize = 13,
                    LineHeight = 18.2,
                    Prefix = prefix,
                    Suffix = suffix,
                    Culture = Culture,
                    Value = quote.Bonus,
                    RollDuration = CreditTopUpTimings.Checkout,
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                ArcTokens.Bind(_bonusFigure, CreditTopUpOdometer.ForegroundProperty, ArcDesignTokenKind.TextMuted);
                _bonusFigure.Bind(CreditTopUpOdometer.FontFamilyProperty, this.GetObservable(FontFamilyProperty));
                _bonusLine.Content = _bonusFigure;
            }
            else
            {
                _bonusFigure.Value = quote.Bonus;
            }
        }
        else
        {
            var first = CreditTopUpMath.FirstBonusAt(Pricing.BonusTiers);
            var text = first is { } at ? Format(labels.BonusStartsFormat, Count(at)) : string.Empty;
            var key = "starts:" + text;
            if (key != _bonusKey)
            {
                _bonusKey = key;
                _bonusFigure = null;
                _bonusLine.Content = Small(text, ArcDesignTokenKind.TextMuted);
            }
        }
    }

    #endregion

    #region Input

    private void OnSliderChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_syncing)
        {
            return;
        }
        var value = CreditTopUpMath.Snap((long)Math.Round(e.NewValue), Range);
        if (value != SelectedCredits)
        {
            _syncing = true;
            SetCurrentValue(SelectedCreditsProperty, value);
            _syncing = false;
        }
    }

    private void OnPackPicked(int index)
    {
        var packs = Packs;
        if (packs is null || index < 0 || index >= packs.Count)
        {
            return;
        }
        SetCurrentValue(SelectedCreditsProperty, packs[index]);
    }

    private void OnThresholdPicked(int index)
    {
        var thresholds = Thresholds;
        if (!IsAutoTopUpEnabled || thresholds is null || index < 0 || index >= thresholds.Count)
        {
            return;
        }
        if (thresholds[index] == AutoTopUpThreshold)
        {
            return;
        }
        SetCurrentValue(AutoTopUpThresholdProperty, thresholds[index]);
        ReportAuto();
    }

    private void OnAutoSwitched(object? sender, RoutedEventArgs e)
    {
        if (_syncing || _autoSwitch is null)
        {
            return;
        }
        var on = _autoSwitch.IsChecked == true;
        if (on == IsAutoTopUpEnabled)
        {
            return;
        }
        SetCurrentValue(IsAutoTopUpEnabledProperty, on);
        ReportAuto();
    }

    /// <summary>Raises the settings event and shows the notice for 2.6 seconds.</summary>
    private void ReportAuto()
    {
        var settings = new CreditTopUpAutoSettings(IsAutoTopUpEnabled, AutoTopUpThreshold, CurrentQuote.Total);
        RaiseEvent(new CreditTopUpAutoTopUpChangedEventArgs(AutoTopUpChangedEvent, settings));
        ShowNotice(IsAutoTopUpEnabled ? Format(Labels.AutoOnFormat, Count(AutoTopUpThreshold)) : Labels.AutoOff);
    }

    private void OnChangeClick(object? sender, RoutedEventArgs e) => ChangePaymentMethod();

    /// <summary>Selects the next saved card (wrapping) and raises <see cref="PaymentMethodChanged"/>.</summary>
    public void ChangePaymentMethod()
    {
        var cards = PaymentMethods;
        if (cards is null || cards.Count < 2)
        {
            return;
        }
        var current = SelectedPaymentMethod;
        var index = 0;
        for (var i = 0; i < cards.Count; i++)
        {
            if (ReferenceEquals(cards[i], current))
            {
                index = i;
            }
        }
        var next = cards[CreditTopUpMath.Wrap(index, 1, cards.Count)];
        SetCurrentValue(SelectedPaymentMethodIdProperty, next.Id);
        RaiseEvent(new CreditTopUpPaymentMethodChangedEventArgs(PaymentMethodChangedEvent, next));
    }

    private void OnBuyClick(object? sender, RoutedEventArgs e) => _ = PurchaseAsync();

    #endregion

    #region Purchase

    /// <summary>
    /// Buys <see cref="SelectedCredits"/> with the selected card, as the button does. The order is fixed when the
    /// purchase starts; changes made while it runs apply to the next one. Ignored while a purchase runs.
    /// </summary>
    public async Task PurchaseAsync()
    {
        if (Phase == CreditTopUpPhase.Processing)
        {
            return;
        }
        _dwell?.Cancel();
        var quote = CurrentQuote;
        var card = SelectedPaymentMethod;
        var order = new CreditTopUpOrder(quote.Credits, quote.Bonus, quote.Total, quote.Price, quote.Currency, card?.Id);
        _lastOrder = order;
        ErrorMessage = null;
        Phase = CreditTopUpPhase.Processing;
        UpdatePhaseVisuals();

        _purchase?.Cancel();
        var cts = new CancellationTokenSource();
        _purchase = cts;
        try
        {
            if (_purchaseHandler is { } handler)
            {
                await handler(order, cts.Token);
            }
            else
            {
                await Delay(CreditTopUpSamples.SimulatedChargeTime, cts.Token);
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            if (!ReferenceEquals(_purchase, cts))
            {
                return;
            }
            var message = string.IsNullOrWhiteSpace(ex.Message) ? Labels.ErrorFallback : ex.Message;
            ErrorMessage = message;
            Phase = CreditTopUpPhase.Error;
            UpdatePhaseVisuals();
            RaiseEvent(new CreditTopUpPurchaseFailedEventArgs(PurchaseFailedEvent, order, message));
            return;
        }
        if (!ReferenceEquals(_purchase, cts) || cts.IsCancellationRequested)
        {
            return;
        }
        var balance = Math.Max(0, Balance) + order.Total;
        SetCurrentValue(BalanceProperty, balance);
        Phase = CreditTopUpPhase.Done;
        UpdatePhaseVisuals();
        RaiseEvent(new CreditTopUpPurchasedEventArgs(PurchasedEvent, order, balance));

        var dwell = new CancellationTokenSource();
        _dwell = dwell;
        try
        {
            await Delay(DoneDwell, dwell.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        if (ReferenceEquals(_dwell, dwell) && Phase == CreditTopUpPhase.Done)
        {
            Phase = CreditTopUpPhase.Idle;
            UpdatePhaseVisuals();
        }
    }

    /// <summary>After a failure, a change of amount or card returns the button to its resting label.</summary>
    private void ClearError()
    {
        if (Phase != CreditTopUpPhase.Error)
        {
            return;
        }
        ErrorMessage = null;
        Phase = CreditTopUpPhase.Idle;
        UpdatePhaseVisuals();
    }

    private async void ShowNotice(string text)
    {
        _notice?.Cancel();
        var cts = new CancellationTokenSource();
        _notice = cts;
        _noticeText = text;
        UpdateStatus();
        try
        {
            await Delay(NoticeDwell, cts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        if (ReferenceEquals(_notice, cts))
        {
            _noticeText = null;
            UpdateStatus();
        }
    }

    private void UpdatePhaseVisuals()
    {
        PseudoClasses.Set(ProcessingPseudoClass, Phase == CreditTopUpPhase.Processing);
        PseudoClasses.Set(DonePseudoClass, Phase == CreditTopUpPhase.Done);
        PseudoClasses.Set(ErrorPseudoClass, Phase == CreditTopUpPhase.Error);
        UpdateBuy();
        UpdateStatus();
    }

    private void UpdateBuy()
    {
        if (_buy is null)
        {
            return;
        }
        var labels = Labels;
        _buy.IsLoading = Phase == CreditTopUpPhase.Processing;
        var key = Phase switch
        {
            CreditTopUpPhase.Done => "done:" + labels.Done,
            CreditTopUpPhase.Error => "retry:" + labels.Retry,
            // Processing keeps the resting label (hidden behind the spinner) so the label slot keeps its width.
            _ => "buy:" + labels.Buy
        };
        AutomationProperties.SetName(_buy, Phase switch
        {
            CreditTopUpPhase.Processing => labels.Processing,
            CreditTopUpPhase.Done => labels.Done,
            CreditTopUpPhase.Error => labels.Retry,
            _ => labels.Buy
        });
        if (key == _buyKey)
        {
            return;
        }
        _buyKey = key;
        if (Phase == CreditTopUpPhase.Done)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var check = Glyph(ArcIcons.Check, 16);
            check.Bind(ArcGlyph.ForegroundProperty, _buy.GetObservable(ForegroundProperty));
            row.Children.Add(check);
            row.Children.Add(new TextBlock { Text = labels.Done, VerticalAlignment = VerticalAlignment.Center });
            _buy.Content = row;
        }
        else
        {
            _buy.Content = Phase == CreditTopUpPhase.Error ? labels.Retry : labels.Buy;
        }
    }

    /// <summary>The status line: purchase state first, then a settings notice, then the resting sentence.</summary>
    internal void UpdateStatus()
    {
        var labels = Labels;
        StatusKind kind;
        string text;
        switch (Phase)
        {
            case CreditTopUpPhase.Processing:
            {
                var card = SelectedPaymentMethodFor(_lastOrder);
                kind = StatusKind.Charging;
                text = card is null ? labels.Processing : Format(labels.ChargingFormat, card.DisplayBrand, card.Last4);
                break;
            }
            case CreditTopUpPhase.Done:
                kind = StatusKind.Done;
                text = Format(IsSimulated ? labels.DoneSimulatedFormat : labels.DoneLiveFormat, Count(_lastOrder?.Total ?? 0));
                break;
            case CreditTopUpPhase.Error:
                kind = StatusKind.Error;
                text = ErrorMessage ?? labels.ErrorFallback;
                break;
            default:
                if (_noticeText is { } notice)
                {
                    kind = StatusKind.Notice;
                    text = notice;
                }
                else
                {
                    kind = StatusKind.Resting;
                    text = IsSimulated ? labels.FooterSimulated : labels.FooterLive;
                }
                break;
        }
        StatusLine = (kind, text);
        if (_status is null)
        {
            return;
        }
        var key = $"{kind}|{text}";
        if (key == _statusKey)
        {
            return;
        }
        _statusKey = key;
        var icon = Glyph(kind switch
        {
            StatusKind.Charging => ArcIcons.CreditCard,
            StatusKind.Done => ArcIcons.CircleCheck,
            StatusKind.Error => ArcIcons.CircleAlert,
            StatusKind.Notice => ArcIcons.RefreshCw,
            _ => ArcIcons.Lock
        });
        ArcTokens.Bind(icon, ArcGlyph.ForegroundProperty, kind switch
        {
            StatusKind.Done => ArcDesignTokenKind.Success,
            StatusKind.Error => ArcDesignTokenKind.Danger,
            _ => ArcDesignTokenKind.TextMuted
        });
        var row = new ArcShrinkRow { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center };
        row.Children.Add(icon);
        var line = Small(text, kind == StatusKind.Error ? ArcDesignTokenKind.Danger : ArcDesignTokenKind.TextMuted);
        ArcShrinkRow.SetIsShrinkable(line, true);
        row.Children.Add(line);
        _status.Content = row;
        AutomationProperties.SetName(_status, text);
    }

    private CreditTopUpPaymentMethod? SelectedPaymentMethodFor(CreditTopUpOrder? order)
    {
        var cards = PaymentMethods;
        if (order?.PaymentMethodId is { } id && cards is not null)
        {
            foreach (var card in cards)
            {
                if (card.Id == id)
                {
                    return card;
                }
            }
        }
        return SelectedPaymentMethod;
    }

    #endregion
}

/// <summary>The timed curves of <see cref="CreditTopUp"/>'s figures.</summary>
internal static class CreditTopUpTimings
{
    /// <summary>The balance odometer: 900ms on Arc's standard curve.</summary>
    public static readonly TimeSpan Balance = TimeSpan.FromMilliseconds(900);

    /// <summary>The checkout figures: 240ms on Arc's standard curve.</summary>
    public static readonly TimeSpan Checkout = TimeSpan.FromMilliseconds(240);
}
