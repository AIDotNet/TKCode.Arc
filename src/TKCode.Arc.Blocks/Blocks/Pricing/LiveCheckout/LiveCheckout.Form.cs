using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

using Avalonia.Media.Immutable;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

/// <summary>The form side: parts, building the plan / add-on / wallet controls, field shaping and validation.</summary>
public partial class LiveCheckout
{
    private sealed class FieldState
    {
        public required LiveCheckoutField Field { get; init; }
        public Input? Input { get; set; }
        public ArcGlyph? Check { get; set; }
        public LiveCheckoutPresence? CheckPresence { get; set; }
        public bool IsDirty { get; set; }
        public bool IsShown { get; set; }
        public LiveCheckoutFieldError Error { get; set; }
    }

    private readonly Dictionary<LiveCheckoutField, FieldState> _fields = new();
    private readonly List<LiveCheckoutPlanCard> _planCards = new();
    private readonly List<LiveCheckoutAddonRow> _addonRows = new();
    private readonly List<LiveCheckoutButton> _walletButtons = new();
    private readonly Dictionary<string, TextBlock> _texts = new(StringComparer.Ordinal);

    private LiveCheckoutShell? _shell;
    private Control? _form;
    private Control? _expressSection;
    private LiveCheckoutTileGrid? _walletGrid;
    private LiveCheckoutBillingSwitch? _billingSwitch;
    private LiveCheckoutTileGrid? _planGrid;
    private LiveCheckoutSeatStepper? _seatStepper;
    private StackPanel? _addonList;
    private Control? _addonCard;
    private Select? _countrySelect;
    private LiveCheckoutTileGrid? _cityRow;
    private LiveCheckoutSwap? _brandSwap;
    private LiveCheckoutButton? _testCardButton;
    private LiveCheckoutCollapse? _alertRow;
    private TextBlock? _alertText;
    private LiveCheckoutButton? _payButton;
    private LiveCheckoutSwap? _payLabel;
    private Control? _payIdle;
    private Control? _payBusy;
    private LiveCheckoutAmount? _payAmount;
    private Control? _secureNote;
    private LiveCheckoutAnnouncer? _announcer;
    private LiveCheckoutPresence? _formExit;

    private string _cardNumber = string.Empty;
    private string _expiry = string.Empty;
    private string _cvc = string.Empty;
    private bool _syncingFields;
    private bool _syncingAddons;
    private LiveCheckoutButton? _busyWallet;
    private bool _keyboardInput;

    /// <summary>Focus moved by the block shows a ring only when the last input was the keyboard.</summary>
    private NavigationMethod FocusMethod => _keyboardInput ? NavigationMethod.Tab : NavigationMethod.Pointer;

    private readonly Dictionary<LiveCheckoutCardBrand, TextBlock> _brandTexts = new();

    public LiveCheckout()
    {
        foreach (var list in new INotifyCollectionChanged?[]
                 {
                     _plans as INotifyCollectionChanged, _addons as INotifyCollectionChanged,
                     _countries as INotifyCollectionChanged, _wallets as INotifyCollectionChanged,
                     _selectedAddonIds as INotifyCollectionChanged
                 })
        {
            if (list is not null)
            {
                list.CollectionChanged += OnListChanged;
            }
        }
        foreach (var field in Enum.GetValues<LiveCheckoutField>())
        {
            _fields[field] = new FieldState { Field = field };
        }
        AddHandler(KeyDownEvent, (_, _) => _keyboardInput = true, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerPressedEvent, (_, _) => _keyboardInput = false, RoutingStrategies.Tunnel, handledEventsToo: true);
        ActualThemeVariantChanged += (_, _) =>
        {
            UpdateRingFill();
            ApplyWalletColors();
        };
    }

    private T? Part<T>(TemplateAppliedEventArgs e, string name) where T : class => e.NameScope.Find<T>(name);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        DetachParts();

        _shell = Part<LiveCheckoutShell>(e, ShellPart);
        _form = Part<Control>(e, "PART_Form");
        _expressSection = Part<Control>(e, "PART_Express");
        _walletGrid = Part<LiveCheckoutTileGrid>(e, "PART_Wallets");
        _billingSwitch = Part<LiveCheckoutBillingSwitch>(e, "PART_Billing");
        _planGrid = Part<LiveCheckoutTileGrid>(e, "PART_Plans");
        _seatStepper = Part<LiveCheckoutSeatStepper>(e, "PART_Seats");
        _addonList = Part<StackPanel>(e, "PART_AddonList");
        _addonCard = Part<Control>(e, "PART_AddonCard");
        _countrySelect = Part<Select>(e, "PART_Country");
        _cityRow = Part<LiveCheckoutTileGrid>(e, "PART_CityRow");
        _brandSwap = Part<LiveCheckoutSwap>(e, "PART_Brand");
        _testCardButton = Part<LiveCheckoutButton>(e, "PART_TestCard");
        _alertRow = Part<LiveCheckoutCollapse>(e, "PART_AlertRow");
        _alertText = Part<TextBlock>(e, "PART_Alert");
        _payButton = Part<LiveCheckoutButton>(e, "PART_Pay");
        _payLabel = Part<LiveCheckoutSwap>(e, "PART_PayLabel");
        _secureNote = Part<Control>(e, "PART_SecureNote");
        _announcer = Part<LiveCheckoutAnnouncer>(e, "PART_Announcer");

        foreach (var name in TextPartNames)
        {
            if (Part<TextBlock>(e, name) is { } text)
            {
                _texts[name] = text;
            }
        }

        BindField(e, LiveCheckoutField.Email, "PART_Email");
        BindField(e, LiveCheckoutField.Name, "PART_Name");
        BindField(e, LiveCheckoutField.Address, "PART_Address");
        BindField(e, LiveCheckoutField.City, "PART_City");
        BindField(e, LiveCheckoutField.PostalCode, "PART_Postal");
        BindField(e, LiveCheckoutField.CardNumber, "PART_CardNumber");
        BindField(e, LiveCheckoutField.Expiry, "PART_Expiry");
        BindField(e, LiveCheckoutField.Cvc, "PART_Cvc");

        if (_form is not null)
        {
            _formExit = new LiveCheckoutPresence(_form);
            _formExit.Hidden += (_, _) =>
            {
                if (IsPaid)
                {
                    _shell?.EndFormExit();
                }
            };
            _form.AddHandler(KeyDownEvent, OnFormKeyDown, RoutingStrategies.Bubble);
        }
        if (_billingSwitch is not null)
        {
            _billingSwitch.Picked += OnBillingPicked;
        }
        if (_seatStepper is not null)
        {
            _seatStepper.PropertyChanged += OnSeatStepperChanged;
        }
        if (_countrySelect is not null)
        {
            _countrySelect.SelectionChanged += OnCountryPicked;
            _countrySelect.TextSelector = item => (item as LiveCheckoutCountry)?.Name;
        }
        if (_testCardButton is not null)
        {
            _testCardButton.Click += OnTestCard;
        }
        if (_payButton is not null)
        {
            _payButton.Click += OnPay;
        }
        BuildPayLabel();
        BuildBrandTexts();
        OnApplySummaryTemplate(e);

        RebuildWallets();
        RebuildPlans();
        RebuildAddons();
        RebuildCountries();
        SyncLabels();
        SyncFieldTexts();
        SyncSeats();
        SyncBilling();
        _alertRow?.SetOpen(false, false);
        Recalculate(raise: false);
        SyncLayoutState();
    }

    private static readonly string[] TextPartNames =
    [
        "PART_PlanHeading", "PART_AddonsHeading", "PART_BillingHeading", "PART_CardHeading", "PART_OrDivider", "PART_SeatsLabel",
        "PART_EmailLabel", "PART_NameLabel", "PART_CountryLabel", "PART_AddressLabel", "PART_CityLabel", "PART_PostalLabel",
        "PART_CardNumberLabel", "PART_ExpiryLabel", "PART_CvcLabel", "PART_SecureText", "PART_SummaryTitle", "PART_ToggleLabel",
        "PART_SubtotalLabel", "PART_DiscountLabel", "PART_DiscountCode", "PART_TaxLabel", "PART_TaxNotCharged", "PART_GrandLabel",
        "PART_ReceiptTitle", "PART_ReceiptText", "PART_MetaOrderLabel", "PART_MetaMethodLabel", "PART_MetaDateLabel",
        "PART_MetaEmailLabel", "PART_MetaOrder", "PART_MetaMethod", "PART_MetaDate", "PART_MetaEmail", "PART_StartOverText",
        "PART_TestCardText", "PART_AddCouponText"
    ];

    private void SetText(string part, string? text)
    {
        if (_texts.TryGetValue(part, out var block))
        {
            block.Text = text;
        }
    }

    private TextBlock? Text(string part) => _texts.TryGetValue(part, out var block) ? block : null;

    private void DetachParts()
    {
        if (_form is not null)
        {
            _form.RemoveHandler(KeyDownEvent, OnFormKeyDown);
        }
        if (_billingSwitch is not null)
        {
            _billingSwitch.Picked -= OnBillingPicked;
        }
        if (_seatStepper is not null)
        {
            _seatStepper.PropertyChanged -= OnSeatStepperChanged;
        }
        if (_countrySelect is not null)
        {
            _countrySelect.SelectionChanged -= OnCountryPicked;
        }
        if (_testCardButton is not null)
        {
            _testCardButton.Click -= OnTestCard;
        }
        if (_payButton is not null)
        {
            _payButton.Click -= OnPay;
        }
        foreach (var state in _fields.Values)
        {
            if (state.Input is { } input)
            {
                input.TextChanged -= OnFieldTextChanged;
                input.LostFocus -= OnFieldLostFocus;
            }
            state.Input = null;
            state.Check = null;
            state.CheckPresence = null;
        }
        DetachSummaryParts();
        _texts.Clear();
    }

    private void BindField(TemplateAppliedEventArgs e, LiveCheckoutField field, string name)
    {
        var state = _fields[field];
        var input = Part<Input>(e, name);
        state.Input = input;
        if (input is null)
        {
            return;
        }
        input.Tag = field;
        // The valid check sits inside the field's right edge and pops in (opacity, scale .6 → 1) on the ω 20 spring.
        var check = new ArcGlyph { Data = ArcIcons.Check, Size = 18, StrokeThickness = 2, Margin = new Thickness(0, 0, 2, 0) };
        check.Classes.Add("valid");
        ArcTokens.Bind(check, ArcGlyph.ForegroundProperty, ArcDesignTokenKind.Success);
        AutomationProperties.SetAccessibilityView(check, AccessibilityView.Raw);
        input.Suffix = check;
        state.Check = check;
        state.CheckPresence = new LiveCheckoutPresence(check, scale: 0.6, visible: false);
        input.TextChanged += OnFieldTextChanged;
        input.LostFocus += OnFieldLostFocus;
    }

    // --- wallets, plans, add-ons, countries -------------------------------------------------------------------------

    private void RebuildWallets()
    {
        if (_walletGrid is null)
        {
            return;
        }
        foreach (var button in _walletButtons)
        {
            button.Click -= OnWallet;
        }
        _walletButtons.Clear();
        foreach (var wallet in _wallets)
        {
            var button = new LiveCheckoutButton { Tag = wallet, Content = BuildWalletMark(wallet) };
            button.Classes.Add("wallet");
            AutomationProperties.SetName(button, wallet.Name);
            button.Click += OnWallet;
            _walletButtons.Add(button);
        }
        _walletGrid.SetTiles(_walletButtons);
        if (_expressSection is not null)
        {
            _expressSection.IsVisible = _walletButtons.Count > 0;
        }
        ApplyWalletColors();
    }

    private static Control BuildWalletMark(LiveCheckoutWallet wallet)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 5,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (wallet.Glyph is { } glyph)
        {
            var path = new Avalonia.Controls.Shapes.Path
            {
                Data = glyph,
                Width = 18,
                Height = 18,
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center,
                StrokeThickness = wallet.IsGlyphFilled ? 0 : 1.8,
                StrokeLineCap = PenLineCap.Round,
                StrokeJoin = PenLineJoin.Round
            };
            path.Classes.Add("wallet-glyph");
            row.Children.Add(path);
        }
        if (!string.IsNullOrEmpty(wallet.MarkText))
        {
            var text = new TextBlock
            {
                Text = wallet.MarkText,
                FontSize = 17,
                FontWeight = FontWeight.Medium,
                FontStyle = wallet.IsMarkItalic ? FontStyle.Italic : FontStyle.Normal,
                VerticalAlignment = VerticalAlignment.Center
            };
            row.Children.Add(text);
        }
        var spinner = new ArcSpinner { Width = 16, Height = 16, IsVisible = false, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        spinner.Classes.Add("wallet-spinner");
        return new Panel { Children = { row, spinner } };
    }

    private void ApplyWalletColors()
    {
        var dark = ActualThemeVariant == ThemeVariant.Dark;
        foreach (var button in _walletButtons)
        {
            if (button.Tag is not LiveCheckoutWallet wallet)
            {
                continue;
            }
            var background = dark ? wallet.DarkBackground ?? wallet.Background : wallet.Background;
            var foreground = dark ? wallet.DarkForeground ?? wallet.Foreground : wallet.Foreground;
            var fg = new ImmutableSolidColorBrush(foreground);
            button.Background = new ImmutableSolidColorBrush(background);
            button.Foreground = fg;
            button.BorderBrush = wallet.BorderColor is { } edge ? new ImmutableSolidColorBrush(edge) : new ImmutableSolidColorBrush(background);
            if (button.Content is Panel { Children: [StackPanel row, ArcSpinner spinner] })
            {
                spinner.Foreground = fg;
                foreach (var child in row.Children)
                {
                    if (child is Avalonia.Controls.Shapes.Path path)
                    {
                        path.Stroke = wallet.IsGlyphFilled ? null : fg;
                        path.Fill = wallet.IsGlyphFilled ? fg : null;
                    }
                    else if (child is TextBlock text)
                    {
                        text.Foreground = fg;
                    }
                }
            }
        }
    }

    private void RebuildPlans()
    {
        if (_planGrid is null)
        {
            return;
        }
        foreach (var card in _planCards)
        {
            card.IsCheckedChanged -= OnPlanChecked;
            card.KeyDown -= OnPlanKeyDown;
        }
        _planCards.Clear();
        foreach (var plan in _plans)
        {
            var card = new LiveCheckoutPlanCard(plan) { GroupName = null };
            card.IsCheckedChanged += OnPlanChecked;
            card.KeyDown += OnPlanKeyDown;
            _planCards.Add(card);
        }
        _planGrid.SetTiles(_planCards);
        var ring = _planGrid.Ring;
        ArcTokens.Bind(ring, GlidingIndicator.StrokeProperty, ArcDesignTokenKind.Accent);
        ring.BindCornerRadius(ArcDesignTokenKind.RadiusStep22);
        UpdateRingFill();
        SyncPlanCards(animate: false);
    }

    /// <summary>The ring's fill: the accent at 6%, rebuilt when the theme changes.</summary>
    private void UpdateRingFill()
    {
        if (_planGrid is null || !IsAttachedToTheme())
        {
            return;
        }
        var accent = ArcTokens.GetColor(this, ArcDesignTokenKind.Accent);
        _planGrid.Ring.Fill = new ImmutableSolidColorBrush(Color.FromArgb((byte)Math.Round(255 * 0.06), accent.R, accent.G, accent.B));
    }

    private bool IsAttachedToTheme() => TopLevel.GetTopLevel(this) is not null;

    private LiveCheckoutPlan? SelectedPlan
    {
        get
        {
            foreach (var plan in _plans)
            {
                if (plan.Id == SelectedPlanId)
                {
                    return plan;
                }
            }
            return _plans.Count > 0 ? _plans[0] : null;
        }
    }

    private void SyncPlanCards(bool animate)
    {
        var selected = SelectedPlan;
        var culture = Labels.Culture;
        LiveCheckoutPlanCard? selectedCard = null;
        foreach (var card in _planCards)
        {
            card.Currency = Currency;
            card.Culture = culture;
            card.Unit = Labels.PerSeatMonth;
            card.Price = LiveCheckoutMath.SeatPrice(card.Plan.MonthlySeatPrice, Billing, YearlyDiscount);
            var isSelected = ReferenceEquals(card.Plan, selected);
            card.IsChecked = isSelected;
            KeyboardNavigation.SetIsTabStop(card, isSelected);
            if (isSelected)
            {
                selectedCard = card;
            }
        }
        _planGrid?.Select(selectedCard, animate && LiveCheckoutMotion.CanAnimate(this));
    }

    private void OnPlanChecked(object? sender, RoutedEventArgs e)
    {
        if (sender is LiveCheckoutPlanCard { IsChecked: true } card && card.Plan.Id != SelectedPlan?.Id)
        {
            SelectedPlanId = card.Plan.Id;
        }
    }

    private void OnPlanKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not LiveCheckoutPlanCard card || _planCards.Count == 0)
        {
            return;
        }
        var step = e.Key switch
        {
            Key.Right or Key.Down => 1,
            Key.Left or Key.Up => -1,
            _ => 0
        };
        if (step == 0)
        {
            return;
        }
        // Arrows move and select, wrapping at both ends (Home / End do nothing, as measured).
        var index = (_planCards.IndexOf(card) + step + _planCards.Count) % _planCards.Count;
        var next = _planCards[index];
        SelectedPlanId = next.Plan.Id;
        next.Focus(NavigationMethod.Directional);
        e.Handled = true;
    }

    private void RebuildAddons()
    {
        if (_addonList is null)
        {
            return;
        }
        foreach (var row in _addonRows)
        {
            row.IsCheckedChanged -= OnAddonChecked;
        }
        _addonRows.Clear();
        _addonList.Children.Clear();
        for (var i = 0; i < _addons.Count; i++)
        {
            var row = new LiveCheckoutAddonRow(_addons[i]);
            if (i > 0)
            {
                row.Classes.Add("separated");
            }
            row.IsCheckedChanged += OnAddonChecked;
            _addonRows.Add(row);
            _addonList.Children.Add(row);
        }
        if (_addonCard is not null)
        {
            _addonCard.IsVisible = _addons.Count > 0;
        }
        SyncAddonRows();
        SyncAddonChecks();
    }

    private void SyncAddonRows()
    {
        foreach (var row in _addonRows)
        {
            row.PriceText = SettingsFormat.Money(row.Addon.MonthlyPrice, Currency, Labels.Culture);
            row.Unit = Labels.PerMonth;
        }
    }

    private void SyncAddonChecks()
    {
        _syncingAddons = true;
        try
        {
            foreach (var row in _addonRows)
            {
                row.IsChecked = _selectedAddonIds.Contains(row.Addon.Id);
            }
        }
        finally
        {
            _syncingAddons = false;
        }
        Recalculate(raise: true);
    }

    private void OnAddonChecked(object? sender, RoutedEventArgs e)
    {
        if (_syncingAddons || sender is not LiveCheckoutAddonRow row)
        {
            return;
        }
        var id = row.Addon.Id;
        var has = _selectedAddonIds.Contains(id);
        if (row.IsChecked == true && !has)
        {
            _selectedAddonIds.Add(id);
        }
        else if (row.IsChecked != true && has)
        {
            _selectedAddonIds.Remove(id);
        }
        if (_selectedAddonIds is not INotifyCollectionChanged)
        {
            Recalculate(raise: true);
        }
        ClearAlert();
    }

    private void RebuildCountries()
    {
        if (_countrySelect is null)
        {
            return;
        }
        _syncingFields = true;
        try
        {
            _countrySelect.ItemsSource = _countries;
            _countrySelect.SelectedItem = SelectedCountry;
        }
        finally
        {
            _syncingFields = false;
        }
        Recalculate(raise: true);
    }

    private LiveCheckoutCountry? SelectedCountry
    {
        get
        {
            foreach (var country in _countries)
            {
                if (string.Equals(country.Code, CountryCode, StringComparison.OrdinalIgnoreCase))
                {
                    return country;
                }
            }
            return _countries.Count > 0 ? _countries[0] : null;
        }
    }

    private void OnCountryPicked(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncingFields || _countrySelect?.SelectedItem is not LiveCheckoutCountry country)
        {
            return;
        }
        CountryCode = country.Code;
        ClearAlert();
    }

    private void OnBillingPicked(object? sender, LiveCheckoutBilling billing)
    {
        Billing = billing;
        ClearAlert();
    }

    private void OnSeatStepperChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == LiveCheckoutSeatStepper.ValueProperty && _seatStepper is not null && !_syncingFields)
        {
            Seats = _seatStepper.Value;
            ClearAlert();
        }
    }

    private void SyncSeats()
    {
        if (_seatStepper is null)
        {
            return;
        }
        _syncingFields = true;
        try
        {
            _seatStepper.Minimum = MinSeats;
            _seatStepper.Maximum = MaxSeats;
            _seatStepper.Value = Seats;
        }
        finally
        {
            _syncingFields = false;
        }
    }

    private void SyncBilling()
    {
        if (_billingSwitch is null)
        {
            return;
        }
        _billingSwitch.Billing = Billing;
        _billingSwitch.MonthlyLabel = Labels.Monthly;
        _billingSwitch.YearlyLabel = Labels.Yearly;
        _billingSwitch.Badge = LiveCheckoutMath.DiscountBadge(YearlyDiscount, Labels.Culture);
        AutomationProperties.SetName(_billingSwitch, Labels.BillingPeriod);
    }

    // --- text fields ------------------------------------------------------------------------------------------------

    private string FieldValue(LiveCheckoutField field) => field switch
    {
        LiveCheckoutField.Email => Email ?? string.Empty,
        LiveCheckoutField.Name => CardholderName ?? string.Empty,
        LiveCheckoutField.Address => Address ?? string.Empty,
        LiveCheckoutField.City => City ?? string.Empty,
        LiveCheckoutField.PostalCode => PostalCode ?? string.Empty,
        LiveCheckoutField.CardNumber => _cardNumber,
        LiveCheckoutField.Expiry => _expiry,
        _ => _cvc
    };

    private void SyncFieldTexts()
    {
        _syncingFields = true;
        try
        {
            foreach (var state in _fields.Values)
            {
                if (state.Input is { } input && input.Text != FieldValue(state.Field))
                {
                    input.Text = FieldValue(state.Field);
                }
            }
        }
        finally
        {
            _syncingFields = false;
        }
    }

    private void SetFieldText(LiveCheckoutField field, string text)
    {
        if (_fields[field].Input is not { } input || input.Text == text)
        {
            return;
        }
        _syncingFields = true;
        try
        {
            input.Text = text;
        }
        finally
        {
            _syncingFields = false;
        }
    }

    private void OnFieldTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_syncingFields || sender is not Input { Tag: LiveCheckoutField field } input)
        {
            return;
        }
        var text = input.Text ?? string.Empty;
        var previous = field switch
        {
            LiveCheckoutField.CardNumber => _cardNumber,
            LiveCheckoutField.Expiry => _expiry,
            LiveCheckoutField.Cvc => _cvc,
            LiveCheckoutField.Email => Email,
            LiveCheckoutField.Name => CardholderName,
            LiveCheckoutField.Address => Address,
            LiveCheckoutField.City => City,
            _ => PostalCode
        };
        // Only a real edit counts: a field re-raising its unchanged text (re-enabled after a payment) neither marks it
        // edited nor clears the payment error.
        if (text == (previous ?? string.Empty))
        {
            return;
        }
        switch (field)
        {
            case LiveCheckoutField.CardNumber:
                text = Reshape(input, text, LiveCheckoutValidation.FormatCard(text));
                _cardNumber = text;
                UpdateBrand(animate: true);
                // The security code length follows the network.
                var cvc = LiveCheckoutValidation.FormatCvc(_cvc, LiveCheckoutValidation.Brand(_cardNumber));
                if (cvc != _cvc)
                {
                    _cvc = cvc;
                    SetFieldText(LiveCheckoutField.Cvc, cvc);
                }
                break;
            case LiveCheckoutField.Expiry:
                text = Reshape(input, text, LiveCheckoutValidation.FormatExpiry(text, _expiry));
                _expiry = text;
                break;
            case LiveCheckoutField.Cvc:
                text = Reshape(input, text, LiveCheckoutValidation.FormatCvc(text, LiveCheckoutValidation.Brand(_cardNumber)));
                _cvc = text;
                break;
            case LiveCheckoutField.Email:
                SetCurrentValue(EmailProperty, text);
                break;
            case LiveCheckoutField.Name:
                SetCurrentValue(CardholderNameProperty, text);
                break;
            case LiveCheckoutField.Address:
                SetCurrentValue(AddressProperty, text);
                break;
            case LiveCheckoutField.City:
                SetCurrentValue(CityProperty, text);
                break;
            case LiveCheckoutField.PostalCode:
                SetCurrentValue(PostalCodeProperty, text);
                break;
        }
        var state = _fields[field];
        state.IsDirty = true;
        if (state.IsShown)
        {
            Validate(state);
        }
        ClearAlert();
    }

    /// <summary>Puts the shaped text back with the caret after the same digits.</summary>
    private string Reshape(Input input, string typed, string shaped)
    {
        if (shaped == typed)
        {
            return typed;
        }
        var digits = LiveCheckoutValidation.DigitsBefore(typed, input.CaretIndex);
        _syncingFields = true;
        try
        {
            input.Text = shaped;
            input.CaretIndex = Math.Min(shaped.Length,
                input.CaretIndex >= typed.Length ? shaped.Length : LiveCheckoutValidation.CaretAfterDigits(shaped, digits));
        }
        finally
        {
            _syncingFields = false;
        }
        return shaped;
    }

    private void OnFieldLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is not Input { Tag: LiveCheckoutField field })
        {
            return;
        }
        var state = _fields[field];
        // Errors appear once a field was edited and left (or after a submit), then follow every keystroke.
        if (state.IsDirty && !state.IsShown)
        {
            state.IsShown = true;
            Validate(state);
        }
    }

    private LiveCheckoutFieldError Rule(LiveCheckoutField field)
    {
        var brand = LiveCheckoutValidation.Brand(_cardNumber);
        return field switch
        {
            LiveCheckoutField.Email => LiveCheckoutValidation.Email(Email),
            LiveCheckoutField.Name => LiveCheckoutValidation.Name(CardholderName),
            LiveCheckoutField.Address => LiveCheckoutValidation.Address(Address),
            LiveCheckoutField.City => LiveCheckoutValidation.City(City),
            LiveCheckoutField.PostalCode => LiveCheckoutValidation.Postal(PostalCode, SelectedCountry),
            LiveCheckoutField.CardNumber => LiveCheckoutValidation.Card(_cardNumber),
            LiveCheckoutField.Expiry => LiveCheckoutValidation.Expiry(_expiry, Clock.GetLocalNow().DateTime),
            _ => LiveCheckoutValidation.Cvc(_cvc, brand)
        };
    }

    private void Validate(FieldState state)
    {
        state.Error = Rule(state.Field);
        if (state.Input is { } input)
        {
            input.ErrorMessage = state.IsShown && state.Error != LiveCheckoutFieldError.None
                ? Labels.Error(state.Error, SelectedCountry)
                : null;
        }
        var valid = state.IsShown && state.Error == LiveCheckoutFieldError.None;
        if (state.CheckPresence is { } check && check.IsShown != valid)
        {
            if (valid)
            {
                check.Show(LiveCheckoutMotion.CanAnimate(this), MotionSpec.Spring(LiveCheckoutMotion.Roll));
            }
            else
            {
                check.Jump(false);
            }
        }
    }

    /// <summary>Validates every field and shows the result; returns the first invalid field in form order.</summary>
    private FieldState? ValidateAll()
    {
        FieldState? first = null;
        foreach (var field in Enum.GetValues<LiveCheckoutField>())
        {
            var state = _fields[field];
            state.IsShown = true;
            Validate(state);
            if (first is null && state.Error != LiveCheckoutFieldError.None)
            {
                first = state;
            }
        }
        return first;
    }

    private void UpdateBrand(bool animate)
    {
        var brand = LiveCheckoutValidation.Brand(_cardNumber);
        _brandSwap?.Show(brand == LiveCheckoutCardBrand.None ? null : _brandTexts.GetValueOrDefault(brand), animate);
    }

    private void BuildBrandTexts()
    {
        _brandTexts.Clear();
        foreach (var brand in new[] { LiveCheckoutCardBrand.Novacard, LiveCheckoutCardBrand.Meridian, LiveCheckoutCardBrand.Atlas })
        {
            var text = new TextBlock { Text = Labels.BrandName(brand), FontSize = 12 };
            ArcTokens.Bind(text, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
            _brandTexts[brand] = text;
        }
        if (_brandSwap is not null)
        {
            _brandSwap.Distance = 0;
            _brandSwap.BlurRadius = 0;
            _brandSwap.ContentAlignment = HorizontalAlignment.Right;
        }
        UpdateBrand(animate: false);
    }

    private void OnFormKeyDown(object? sender, KeyEventArgs e)
    {
        // Enter in any field submits the form (the coupon field handles its own Enter).
        if (e.Key == Key.Enter && !e.Handled && e.Source is Visual source && source.FindAncestorOfType<Input>(true) is { Tag: LiveCheckoutField })
        {
            e.Handled = true;
            _ = SubmitAsync();
        }
    }

    private void OnTestCard(object? sender, RoutedEventArgs e) => FillTestCard();

    /// <summary>Fills the card fields with <see cref="TestCard"/> at once (no typing, the focus stays where it is).</summary>
    public void FillTestCard()
    {
        if (TestCard is not { } card || IsProcessing || IsPaid)
        {
            return;
        }
        _cardNumber = LiveCheckoutValidation.FormatCard(card.Number);
        _expiry = LiveCheckoutValidation.FormatExpiry(card.Expiry, string.Empty);
        _cvc = LiveCheckoutValidation.FormatCvc(card.Cvc, LiveCheckoutValidation.Brand(_cardNumber));
        SyncFieldTexts();
        UpdateBrand(animate: true);
        foreach (var field in new[] { LiveCheckoutField.CardNumber, LiveCheckoutField.Expiry, LiveCheckoutField.Cvc })
        {
            var state = _fields[field];
            state.IsDirty = true;
            if (state.IsShown)
            {
                Validate(state);
            }
        }
        ClearAlert();
    }

    private void ClearAlert()
    {
        if (_alertRow is { IsOpen: true })
        {
            _alertRow.SetOpen(false, true);
        }
    }

    private void ShowAlert(string message)
    {
        if (_alertText is not null)
        {
            _alertText.Text = message;
        }
        _alertRow?.SetOpen(true, true);
        _announcer?.Announce(message, assertive: true);
    }

    private void SetFormEnabled(bool enabled)
    {
        foreach (var state in _fields.Values)
        {
            if (state.Input is { } input)
            {
                input.IsEnabled = enabled;
            }
        }
        if (_countrySelect is not null)
        {
            _countrySelect.IsEnabled = enabled;
        }
        foreach (var button in _walletButtons)
        {
            button.IsEnabled = enabled || ReferenceEquals(button, _busyWallet);
            button.IsBusy = ReferenceEquals(button, _busyWallet);
        }
        foreach (var card in _planCards)
        {
            card.IsEnabled = enabled;
        }
        foreach (var row in _addonRows)
        {
            row.IsEnabled = enabled;
        }
        if (_seatStepper is not null)
        {
            _seatStepper.IsEnabled = enabled;
        }
        if (_billingSwitch is not null)
        {
            _billingSwitch.IsEnabled = enabled;
        }
        if (_testCardButton is not null)
        {
            _testCardButton.IsEnabled = enabled;
        }
        if (_payButton is not null)
        {
            _payButton.IsBusy = !enabled && _busyWallet is null;
        }
    }

    private void BuildPayLabel()
    {
        var pay = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        pay.Classes.Add("pay-word");
        _payAmount = new LiveCheckoutAmount { VerticalAlignment = VerticalAlignment.Center };
        _payAmount.Classes.Add("pay-amount");
        _payIdle = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { pay, _payAmount } };
        var spinner = new ArcSpinner { Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(spinner, ArcSpinner.ForegroundProperty, ArcDesignTokenKind.AccentForeground);
        var processing = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        processing.Classes.Add("pay-processing");
        _payBusy = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { spinner, processing } };
        _payLabel?.Show(_payIdle, animate: false);
    }

    private void SyncPayTexts()
    {
        if (_payIdle is StackPanel { Children: [TextBlock pay, ..] })
        {
            pay.Text = Labels.Pay;
        }
        if (_payBusy is StackPanel { Children: [_, TextBlock processing] })
        {
            processing.Text = Labels.Processing;
        }
        if (_payButton is not null)
        {
            AutomationProperties.SetName(_payButton, IsProcessing ? Labels.Processing : $"{Labels.Pay} {_payAmount?.Text}");
        }
    }

    private void SyncStacked(bool stacked)
    {
        if (_walletGrid is not null)
        {
            _walletGrid.Columns = stacked ? 1 : 3;
        }
        if (_planGrid is not null)
        {
            _planGrid.Columns = stacked ? 1 : 3;
        }
        foreach (var card in _planCards)
        {
            card.IsStacked = stacked;
        }
        if (_cityRow is not null)
        {
            _cityRow.Columns = stacked ? 1 : 2;
        }
    }
}
