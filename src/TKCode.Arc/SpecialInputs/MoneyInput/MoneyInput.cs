using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// Arc money input: a currency amount field. Digits group as they are typed without moving the caret, the missing cents
/// wait as faint ghosts so the amount never changes width when it settles, long amounts scale down to fit instead of
/// scrolling, and quick add chips and arrow keys roll the digits. A currency menu switches the currency and keeps the major
/// amount. <see cref="Value"/> is the amount in major units (12.5 for $12.50); <see cref="MinorUnits"/> gives the integer
/// minor units Arc reports.
/// <code>&lt;arc:MoneyInput Label="Amount" Value="{Binding Amount}" Currency="{Binding Currency}" Maximum="1000" /&gt;</code>
/// </summary>
/// <remarks>
/// Keyboard: ArrowUp / ArrowDown step by <see cref="Step"/> (Shift ×10), PageUp / PageDown by ten steps, Enter settles the
/// fraction (12.5 → 12.50); Backspace and Delete on a group separator delete the neighbouring digit; pasted amounts such
/// as "$1,234.56" or "1.234,56 €" are parsed. Typing past <see cref="Maximum"/> is refused with a kick and a flash of
/// the range text; <see cref="Minimum"/> is checked when the field loses focus. The currency switch opens its menu with
/// ArrowDown / ArrowUp; in the menu ArrowUp / ArrowDown / Home / End and typeahead move, Enter / Space choose, Escape
/// closes and returns to the switch.
/// </remarks>
[TemplatePart(FieldPart, typeof(Field))]
[TemplatePart(StagePart, typeof(Control))]
[TemplatePart(AmountPart, typeof(Control))]
[TemplatePart(CurrencyButtonPart, typeof(Avalonia.Controls.Button))]
[TemplatePart(StaticCodePart, typeof(TextBlock))]
[TemplatePart(ChipsPart, typeof(Panel))]
[TemplatePart(RangePart, typeof(TextBlock))]
public class MoneyInput : FieldTextBox
{
    internal const string FieldPart = "PART_Field";
    internal const string StagePart = "PART_Stage";
    internal const string AmountPart = "PART_Amount";
    internal const string PrefixSymbolPart = "PART_PrefixSymbol";
    internal const string SuffixSymbolPart = "PART_SuffixSymbol";
    internal const string CurrencyButtonPart = "PART_CurrencyButton";
    internal const string StaticCodePart = "PART_StaticCode";
    internal const string ToolbarPart = "PART_Toolbar";
    internal const string ChipsPart = "PART_Chips";
    internal const string RangePart = "PART_Range";
    internal const string RangeFlashPart = "PART_RangeFlash";
    internal const string LiveRegionPart = "PART_LiveRegion";
    internal const string PresenterPart = "PART_TextPresenter";
    internal const string QuietClass = "money-quiet";

    // Shared defaults are read-only so no instance can change another's list.
    private static readonly IList<string> s_defaultCurrencies =
        Array.AsReadOnly(new[] { "USD", "EUR", "GBP", "JPY", "CAD", "AUD", "CHF", "INR" });
    private static readonly IList<decimal> s_defaultQuickAdd = Array.AsReadOnly(new[] { 10m, 50m, 100m });
    private static readonly CultureInfo s_defaultCulture = ResolveDefaultCulture();
    private static readonly FontFeatureCollection s_tabular = new() { FontFeature.Parse("+tnum") };
    private static readonly TimeSpan s_flashDuration = TimeSpan.FromMilliseconds(1400);

    /// <summary>The amount in major units (12.5 for $12.50); null is empty. Two-way by default.</summary>
    public static readonly StyledProperty<decimal?> ValueProperty =
        AvaloniaProperty.Register<MoneyInput, decimal?>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>ISO 4217 code of the amount ("USD" by default). Two-way by default.</summary>
    public static readonly StyledProperty<string> CurrencyProperty =
        AvaloniaProperty.Register<MoneyInput, string>(nameof(Currency), "USD", defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Codes offered by the currency menu; a single code shows it as static text instead of the menu.</summary>
    public static readonly StyledProperty<IList<string>> CurrenciesProperty =
        AvaloniaProperty.Register<MoneyInput, IList<string>>(nameof(Currencies), s_defaultCurrencies);

    /// <summary>Lowest amount in major units, checked when the field loses focus.</summary>
    public static readonly StyledProperty<decimal?> MinimumProperty =
        AvaloniaProperty.Register<MoneyInput, decimal?>(nameof(Minimum));

    /// <summary>Highest amount in major units; typing past it is refused with a kick.</summary>
    public static readonly StyledProperty<decimal?> MaximumProperty =
        AvaloniaProperty.Register<MoneyInput, decimal?>(nameof(Maximum));

    /// <summary>Quick add chips in major units (10, 50, 100 by default); an empty list hides them.</summary>
    public static readonly StyledProperty<IList<decimal>> QuickAddAmountsProperty =
        AvaloniaProperty.Register<MoneyInput, IList<decimal>>(nameof(QuickAddAmounts), s_defaultQuickAdd);

    /// <summary>What the arrow keys add, in major units; null is one major unit. Shift moves ten times as far.</summary>
    public static readonly StyledProperty<decimal?> StepProperty =
        AvaloniaProperty.Register<MoneyInput, decimal?>(nameof(Step));

    /// <summary>Formatting culture (Arc <c>locale</c>): en-US by default, never the current culture.</summary>
    public static readonly StyledProperty<CultureInfo?> CultureProperty =
        AvaloniaProperty.Register<MoneyInput, CultureInfo?>(nameof(Culture), s_defaultCulture);

    /// <summary>Range text when both limits are set; {0} is the minimum, {1} the maximum.</summary>
    public static readonly StyledProperty<string> RangeFormatProperty =
        AvaloniaProperty.Register<MoneyInput, string>(nameof(RangeFormat), "{0} to {1}");

    /// <summary>Range text when only the maximum is set.</summary>
    public static readonly StyledProperty<string> MaximumOnlyFormatProperty =
        AvaloniaProperty.Register<MoneyInput, string>(nameof(MaximumOnlyFormat), "Up to {0}");

    /// <summary>Range text when only the minimum is set.</summary>
    public static readonly StyledProperty<string> MinimumOnlyFormatProperty =
        AvaloniaProperty.Register<MoneyInput, string>(nameof(MinimumOnlyFormat), "At least {0}");

    /// <summary>Built-in error below the minimum (after the field loses focus).</summary>
    public static readonly StyledProperty<string> MinimumErrorFormatProperty =
        AvaloniaProperty.Register<MoneyInput, string>(nameof(MinimumErrorFormat), "The minimum is {0}");

    /// <summary>Built-in error above the maximum (after the field loses focus).</summary>
    public static readonly StyledProperty<string> MaximumErrorFormatProperty =
        AvaloniaProperty.Register<MoneyInput, string>(nameof(MaximumErrorFormat), "The maximum is {0}");

    /// <summary>The amount in minor units (cents for USD, yen for JPY), or null when empty.</summary>
    public static readonly DirectProperty<MoneyInput, long?> MinorUnitsProperty =
        AvaloniaProperty.RegisterDirect<MoneyInput, long?>(nameof(MinorUnits), o => o.MinorUnits);

    /// <summary>The allowed range as shown under the shell ("$5.00 to $1,000.00"); empty without limits.</summary>
    public static readonly DirectProperty<MoneyInput, string> RangeTextProperty =
        AvaloniaProperty.RegisterDirect<MoneyInput, string>(nameof(RangeText), o => o.RangeText);

    /// <summary>True while the currency menu is open.</summary>
    public static readonly DirectProperty<MoneyInput, bool> IsCurrencyMenuOpenProperty =
        AvaloniaProperty.RegisterDirect<MoneyInput, bool>(nameof(IsCurrencyMenuOpen), o => o.IsCurrencyMenuOpen);

    /// <summary>
    /// Raised for every real change of <see cref="Value"/>: typing, paste, keys, chips, a currency choice, automation and
    /// programmatic sets (<see cref="MoneyInputValueChangedEventArgs.ChangeSource"/> tells them apart). Never raised during
    /// template application.
    /// </summary>
    public static readonly RoutedEvent<MoneyInputValueChangedEventArgs> ValueChangedEvent =
        RoutedEvent.Register<MoneyInput, MoneyInputValueChangedEventArgs>(nameof(ValueChanged), RoutingStrategies.Bubble);

    /// <summary>
    /// Raised when the currency changes: a choice in the menu (Arc <c>onCurrencyChange</c>) or a programmatic set
    /// (<see cref="MoneyInputCurrencyChangedEventArgs.ChangeSource"/>).
    /// </summary>
    public static readonly RoutedEvent<MoneyInputCurrencyChangedEventArgs> CurrencyChangedEvent =
        RoutedEvent.Register<MoneyInput, MoneyInputCurrencyChangedEventArgs>(nameof(CurrencyChanged), RoutingStrategies.Bubble);

    private readonly List<MoneyInputPart> _parts = new();
    private readonly DispatcherTimer _flashTimer;
    private string _draft = string.Empty;
    private CultureInfo _culture = s_defaultCulture;
    private MoneyInputCurrencyInfo _info;
    private char _group = ',';
    private char _decimal = '.';
    private MoneyInputSource _source = MoneyInputSource.External;
    private int _direction = 1;
    private bool _touched;
    private bool _syncingText;
    private bool _syncingValue;
    private bool _syncingCurrency;
    private bool _inBaseKey;
    private int? _pendingCaret;
    private long? _minorUnits;
    private string _rangeText = string.Empty;
    private string? _shownError;
    private bool _flashing;
    private MotionValue<double>? _flash;
    private INotifyCollectionChanged? _currenciesSource;
    private INotifyCollectionChanged? _quickAddSource;

    private Field? _field;
    private MoneyInputStage? _stage;
    private MoneyInputAmount? _amount;
    private MoneyInputSymbol? _prefixSymbol;
    private MoneyInputSymbol? _suffixSymbol;
    private MoneyInputCurrencyButton? _button;
    private TextBlock? _staticCode;
    private MoneyInputToolbar? _toolbar;
    private Panel? _chips;
    private TextBlock? _range;
    private TextBlock? _rangeFlash;
    private SpecialInputsLiveRegion? _live;
    private TextPresenter? _presenter;

    private MoneyInputCurrencyPopup? _popup;
    private MoneyInputCurrencyMenu? _menu;
    private MoneyInputCurrencyList? _list;
    private bool _menuOpen;
    private bool _optionsStale = true;
    private bool _highlightPending;
    private string _activeCode = "USD";
    private string _typeahead = string.Empty;
    private long _typeaheadAt;

    static MoneyInput()
    {
        IsUndoEnabledProperty.OverrideDefaultValue<MoneyInput>(false);
    }

    public MoneyInput()
    {
        _info = MoneyInputCurrencyInfo.Get(Currency, _culture);
        UpdateSymbols();
        _flashTimer = new DispatcherTimer { Interval = s_flashDuration };
        _flashTimer.Tick += (_, _) => EndFlash();
        AddHandler(PastingFromClipboardEvent, OnPasting);
        AddHandler(CuttingToClipboardEvent, OnCutting);
        AddHandler(RequestBringIntoViewEvent, OnRequestBringIntoView);
    }

    /// <summary>The amount in major units (12.5 for $12.50); null is empty.</summary>
    public decimal? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>ISO 4217 code of the amount.</summary>
    public string Currency { get => GetValue(CurrencyProperty); set => SetValue(CurrencyProperty, value); }

    /// <summary>Codes offered by the currency menu.</summary>
    public IList<string> Currencies { get => GetValue(CurrenciesProperty); set => SetValue(CurrenciesProperty, value); }

    /// <summary>Lowest amount in major units (checked on blur).</summary>
    public decimal? Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }

    /// <summary>Highest amount in major units.</summary>
    public decimal? Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }

    /// <summary>Quick add chips in major units.</summary>
    public IList<decimal> QuickAddAmounts { get => GetValue(QuickAddAmountsProperty); set => SetValue(QuickAddAmountsProperty, value); }

    /// <summary>Arrow key step in major units (null: one major unit).</summary>
    public decimal? Step { get => GetValue(StepProperty); set => SetValue(StepProperty, value); }

    /// <summary>Formatting culture (en-US by default).</summary>
    public CultureInfo? Culture { get => GetValue(CultureProperty); set => SetValue(CultureProperty, value); }

    /// <summary>Range text when both limits are set.</summary>
    public string RangeFormat { get => GetValue(RangeFormatProperty); set => SetValue(RangeFormatProperty, value); }

    /// <summary>Range text when only the maximum is set.</summary>
    public string MaximumOnlyFormat { get => GetValue(MaximumOnlyFormatProperty); set => SetValue(MaximumOnlyFormatProperty, value); }

    /// <summary>Range text when only the minimum is set.</summary>
    public string MinimumOnlyFormat { get => GetValue(MinimumOnlyFormatProperty); set => SetValue(MinimumOnlyFormatProperty, value); }

    /// <summary>Built-in error below the minimum.</summary>
    public string MinimumErrorFormat { get => GetValue(MinimumErrorFormatProperty); set => SetValue(MinimumErrorFormatProperty, value); }

    /// <summary>Built-in error above the maximum.</summary>
    public string MaximumErrorFormat { get => GetValue(MaximumErrorFormatProperty); set => SetValue(MaximumErrorFormatProperty, value); }

    /// <summary>The amount in minor units, or null when empty.</summary>
    public long? MinorUnits
    {
        get => _minorUnits;
        private set => SetAndRaise(MinorUnitsProperty, ref _minorUnits, value);
    }

    /// <summary>The allowed range as shown under the shell.</summary>
    public string RangeText
    {
        get => _rangeText;
        private set => SetAndRaise(RangeTextProperty, ref _rangeText, value);
    }

    private bool _isCurrencyMenuOpen;

    /// <summary>True while the currency menu is open.</summary>
    public bool IsCurrencyMenuOpen
    {
        get => _isCurrencyMenuOpen;
        private set => SetAndRaise(IsCurrencyMenuOpenProperty, ref _isCurrencyMenuOpen, value);
    }

    /// <summary>The amount as a full currency string ("$1,234.50"), empty when there is no amount.</summary>
    public string FormattedValue => _info.FormatMinor(CurrentMinor);

    /// <summary>
    /// Raised for every real change of <see cref="Value"/>, programmatic sets included (ChangeSource = Programmatic).
    /// Programmatic changes update the display without announcements, kicks or flashes.
    /// </summary>
    public event EventHandler<MoneyInputValueChangedEventArgs>? ValueChanged
    {
        add => AddHandler(ValueChangedEvent, value);
        remove => RemoveHandler(ValueChangedEvent, value);
    }

    /// <summary>
    /// Raised when the currency changes, from the menu (ChangeSource = Menu, followed by ValueChanged with
    /// ChangeSource = Currency) or from code (ChangeSource = Programmatic; the display follows Value in the new minor units).
    /// </summary>
    public event EventHandler<MoneyInputCurrencyChangedEventArgs>? CurrencyChanged
    {
        add => AddHandler(CurrencyChangedEvent, value);
        remove => RemoveHandler(CurrencyChangedEvent, value);
    }

    /// <summary>Opens the currency menu (needs at least two <see cref="Currencies"/>).</summary>
    public void OpenCurrencyMenu() => OpenMenu(false);

    /// <summary>Closes the currency menu.</summary>
    public void CloseCurrencyMenu() => CloseMenu(false, false);

    // ------------------------------------------------------------------ state helpers

    /// <summary>The canonical draft ("1234.5").</summary>
    internal string Draft => _draft;

    internal decimal? CurrentMinor => MoneyInputDraft.ToMinor(_draft, _info.Digits);

    internal MoneyInputCurrencyInfo CurrencyInfo => _info;

    /// <summary>The error currently shown (explicit, validation or the built-in range error).</summary>
    internal string? ShownError => _shownError;

    /// <summary>True while the range text flashes after a refused change.</summary>
    internal bool IsLimitFlashing => _flashing;

    /// <summary>Current flash progress (0 muted, 1 warning).</summary>
    internal double LimitFlash => _flash?.Value ?? (_flashing ? 1 : 0);

    internal SpecialInputsLiveRegion? LiveRegion => _live;

    internal MoneyInputStage? Stage => _stage;

    internal MoneyInputAmount? Amount => _amount;

    internal MoneyInputCurrencyList? CurrencyList => _list;

    internal MoneyInputCurrencyPopup? CurrencyPopup => _popup;

    internal MoneyInputCurrencyButton? CurrencyButton => _button;

    internal string ActiveCurrency => _activeCode;

    internal string ValueText => CurrentMinor is { } minor ? _info.FormatMinor(minor) : "Empty";

    private decimal? MinimumMinor => Minimum is { } min ? ToMinorUnits(min) : null;

    private decimal? MaximumMinor => Maximum is { } max ? ToMinorUnits(max) : null;

    private decimal StepMinor => Step is { } step && step > 0 ? step * _info.MinorFactor : _info.MinorFactor;

    private decimal ToMinorUnits(decimal major)
    {
        try
        {
            return major * _info.MinorFactor;
        }
        catch (OverflowException)
        {
            return major > 0 ? decimal.MaxValue : decimal.MinValue;
        }
    }

    private static CultureInfo ResolveDefaultCulture()
    {
        try
        {
            return CultureInfo.GetCultureInfo("en-US");
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }

    private void UpdateSymbols()
    {
        var numbers = _culture.NumberFormat;
        _group = numbers.NumberGroupSeparator is { Length: > 0 } g ? g[0] : ',';
        _decimal = numbers.NumberDecimalSeparator is { Length: > 0 } d ? d[0] : '.';
    }

    // ------------------------------------------------------------------ template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_button is not null)
        {
            _button.Click -= OnCurrencyButtonClick;
            _button.KeyDown -= OnCurrencyButtonKeyDown;
            _button.ToggleRequested = null;
        }
        CloseMenu(false, false);
        base.OnApplyTemplate(e);
        var scope = e.NameScope;
        _field = scope.Find<Field>(FieldPart);
        _stage = scope.Find<MoneyInputStage>(StagePart);
        _amount = scope.Find<MoneyInputAmount>(AmountPart);
        _prefixSymbol = scope.Find<MoneyInputSymbol>(PrefixSymbolPart);
        _suffixSymbol = scope.Find<MoneyInputSymbol>(SuffixSymbolPart);
        _button = scope.Find<MoneyInputCurrencyButton>(CurrencyButtonPart);
        _staticCode = scope.Find<TextBlock>(StaticCodePart);
        _toolbar = scope.Find<MoneyInputToolbar>(ToolbarPart);
        _chips = scope.Find<Panel>(ChipsPart);
        _range = scope.Find<TextBlock>(RangePart);
        _rangeFlash = scope.Find<TextBlock>(RangeFlashPart);
        _live = scope.Find<SpecialInputsLiveRegion>(LiveRegionPart);
        _presenter = scope.Find<TextPresenter>(PresenterPart);
        if (_stage is not null)
        {
            TextElement.SetFontFeatures(_stage, MoneyInputText.Features);
        }
        if (_range is not null)
        {
            TextElement.SetFontFeatures(_range, s_tabular);
        }
        if (_rangeFlash is not null)
        {
            TextElement.SetFontFeatures(_rangeFlash, s_tabular);
            _rangeFlash.Opacity = _flashing ? 1 : 0;
        }
        if (_button is not null)
        {
            _button.Click += OnCurrencyButtonClick;
            _button.KeyDown += OnCurrencyButtonKeyDown;
            _button.ToggleRequested = open =>
            {
                if (open)
                {
                    OpenMenu(true);
                }
                else
                {
                    CloseMenu(false, false);
                }
            };
        }
        _menu = null;
        _list = null;
        _popup = null;
        _optionsStale = true;
        RebuildChips();
        Frame?.Classes.Set(QuietClass, !IsFocused);
        RefreshVisuals(MoneyInputAmountMotion.Cut);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Subscribe(ref _currenciesSource, Currencies as INotifyCollectionChanged, OnCurrenciesCollectionChanged);
        Subscribe(ref _quickAddSource, QuickAddAmounts as INotifyCollectionChanged, OnQuickAddCollectionChanged);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Subscribe(ref _currenciesSource, null, OnCurrenciesCollectionChanged);
        Subscribe(ref _quickAddSource, null, OnQuickAddCollectionChanged);
        _flashTimer.Stop();
        if (_flashing)
        {
            _flashing = false;
            _flash?.Jump(0);
            ApplyFlash(0);
        }
        CloseMenu(false, false);
    }

    private static void Subscribe(ref INotifyCollectionChanged? field, INotifyCollectionChanged? next, NotifyCollectionChangedEventHandler handler)
    {
        if (field is not null)
        {
            field.CollectionChanged -= handler;
        }
        field = next;
        if (field is not null)
        {
            field.CollectionChanged += handler;
        }
    }

    private void OnCurrenciesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => OnCurrenciesChanged();

    private void OnQuickAddCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildChips();
        UpdateToolbar();
    }

    // ------------------------------------------------------------------ properties

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == ValueProperty)
        {
            if (!_syncingValue)
            {
                ApplyExternalValue(change.GetOldValue<decimal?>(), change.GetNewValue<decimal?>());
            }
        }
        else if (property == CurrencyProperty)
        {
            if (!_syncingCurrency)
            {
                ApplyExternalCurrency(change.GetNewValue<string>());
            }
        }
        else if (property == CultureProperty)
        {
            _culture = change.GetNewValue<CultureInfo?>() ?? s_defaultCulture;
            UpdateSymbols();
            _info = MoneyInputCurrencyInfo.Get(_info.Code, _culture);
            _optionsStale = true;
            RefreshChipLabels();
            RefreshVisuals(MoneyInputAmountMotion.Cut);
        }
        else if (property == CurrenciesProperty)
        {
            if (IsHosted())
            {
                Subscribe(ref _currenciesSource, change.GetNewValue<IList<string>>() as INotifyCollectionChanged, OnCurrenciesCollectionChanged);
            }
            OnCurrenciesChanged();
        }
        else if (property == QuickAddAmountsProperty)
        {
            if (IsHosted())
            {
                Subscribe(ref _quickAddSource, change.GetNewValue<IList<decimal>>() as INotifyCollectionChanged, OnQuickAddCollectionChanged);
            }
            RebuildChips();
            UpdateToolbar();
        }
        else if (property == MinimumProperty || property == MaximumProperty || property == RangeFormatProperty ||
                 property == MaximumOnlyFormatProperty || property == MinimumOnlyFormatProperty ||
                 property == MinimumErrorFormatProperty || property == MaximumErrorFormatProperty)
        {
            UpdateLimits();
        }
        else if (property == EffectiveErrorMessageProperty || property == DescriptionProperty)
        {
            UpdateMessages();
        }
        else if (property == IsFocusedProperty)
        {
            OnFocusChanged(change.GetNewValue<bool>());
        }
        else if (property == TextProperty)
        {
            if (!_syncingText && !_inBaseKey)
            {
                // Text written by something other than the editing model (code, IME, a context command): read it like an edit.
                var text = change.GetNewValue<string?>() ?? string.Empty;
                if (!string.Equals(text, MoneyInputDraft.Display(_draft, _group, _decimal), StringComparison.Ordinal))
                {
                    ApplyRaw(text, Math.Min(CaretIndex, text.Length), MoneyInputChangeSource.Type);
                }
            }
        }
    }

    private bool IsHosted() => TopLevel.GetTopLevel(this) is not null;

    /// <summary>
    /// Value set by code or a binding: the display follows (rolling, as Arc's external value), ValueChanged reports it as
    /// <see cref="MoneyInputChangeSource.Programmatic"/>, and nothing is announced, kicked or flashed.
    /// </summary>
    private void ApplyExternalValue(decimal? oldValue, decimal? value)
    {
        decimal? next;
        try
        {
            next = value is { } v ? Math.Round(v * _info.MinorFactor, 0, MidpointRounding.AwayFromZero) : null;
        }
        catch (OverflowException)
        {
            next = null;
        }
        var current = CurrentMinor;
        if (next != current)
        {
            _draft = MoneyInputDraft.FromMinor(next, _info.Digits);
            _source = MoneyInputSource.External;
            _direction = (next ?? 0) >= (current ?? 0) ? 1 : -1;
            RefreshVisuals(MoneyInputAmountMotion.Roll);
        }
        RaiseValueChanged(oldValue, value, CurrentMinor, _info, MoneyInputChangeSource.Programmatic);
    }

    private void ApplyExternalCurrency(string? code)
    {
        var oldCode = _info.Code;
        _info = MoneyInputCurrencyInfo.Get(code, _culture);
        // A programmatic currency is a controlled change: the display follows Value in the new minor units, Value itself
        // is left alone, and CurrencyChanged reports it as Programmatic with no announcement.
        if (Value is { } value)
        {
            decimal? minor;
            try
            {
                minor = Math.Round(value * _info.MinorFactor, 0, MidpointRounding.AwayFromZero);
            }
            catch (OverflowException)
            {
                minor = null;
            }
            _draft = MoneyInputDraft.FromMinor(minor, _info.Digits);
        }
        else
        {
            _draft = MoneyInputDraft.Convert(_draft, _info.Digits);
        }
        _source = MoneyInputSource.External;
        _activeCode = _info.Code;
        RefreshChipLabels();
        RefreshVisuals(MoneyInputAmountMotion.Roll);
        UpdateOptionStates();
        if (!string.Equals(oldCode, _info.Code, StringComparison.Ordinal))
        {
            RaiseEvent(new MoneyInputCurrencyChangedEventArgs(CurrencyChangedEvent, this, oldCode, _info.Code,
                MoneyInputCurrencyChangeSource.Programmatic));
        }
    }

    private void OnCurrenciesChanged()
    {
        _optionsStale = true;
        if (_menuOpen)
        {
            CloseMenu(false, false);
        }
        UpdateSwitch();
    }

    private void OnFocusChanged(bool focused)
    {
        Frame?.Classes.Set(QuietClass, !focused);
        if (!focused)
        {
            // Arc onBlur: the field is touched and the draft settles (12.5 → 12.50).
            _touched = true;
            if (_draft.Length > 0)
            {
                var settled = MoneyInputDraft.Settle(_draft, _info.Digits);
                if (!string.Equals(settled, _draft, StringComparison.Ordinal))
                {
                    _draft = settled;
                    RefreshVisuals(MotionForSource());
                    return;
                }
            }
        }
        UpdateMessages();
    }

    private MoneyInputAmountMotion MotionForSource()
        => _source == MoneyInputSource.Type ? MoneyInputAmountMotion.Cut : MoneyInputAmountMotion.Roll;

    // ------------------------------------------------------------------ rendering state

    private void RefreshVisuals(MoneyInputAmountMotion motion)
    {
        SyncText();
        MoneyInputDraft.Parts(_draft, _info.Digits, _group, _decimal, _parts);
        _amount?.Update(_parts, _direction, motion);
        if (_prefixSymbol is not null)
        {
            _prefixSymbol.IsVisible = !_info.IsSuffix;
            _prefixSymbol.Symbol = _info.Symbol;
        }
        if (_suffixSymbol is not null)
        {
            _suffixSymbol.IsVisible = _info.IsSuffix;
            _suffixSymbol.Symbol = _info.Symbol;
        }
        var minor = CurrentMinor;
        MinorUnits = minor is { } m && m <= long.MaxValue && m >= long.MinValue ? (long)m : null;
        UpdateSwitch();
        UpdateLimits();
    }

    private void SyncText()
    {
        var display = MoneyInputDraft.Display(_draft, _group, _decimal);
        _syncingText = true;
        try
        {
            if (!string.Equals(Text, display, StringComparison.Ordinal))
            {
                SetCurrentValue(TextProperty, display);
            }
            if (_pendingCaret is { } count)
            {
                _pendingCaret = null;
                var position = count == int.MaxValue ? display.Length : MoneyInputDraft.PositionAfter(display, count, _decimal);
                position = Math.Clamp(position, 0, display.Length);
                SetCurrentValue(SelectionStartProperty, position);
                SetCurrentValue(SelectionEndProperty, position);
                SetCurrentValue(CaretIndexProperty, position);
            }
        }
        finally
        {
            _syncingText = false;
        }
    }

    private void UpdateSwitch()
    {
        var canSwitch = (Currencies?.Count ?? 0) > 1;
        if (_button is not null)
        {
            _button.IsVisible = canSwitch;
            _button.Content = _info.Code;
            AutomationProperties.SetName(_button, $"Currency, {_info.Name}");
        }
        if (_staticCode is not null)
        {
            _staticCode.IsVisible = !canSwitch;
            _staticCode.Text = _info.Code;
        }
    }

    private void UpdateLimits()
    {
        var min = MinimumMinor;
        var max = MaximumMinor;
        RangeText = min is not null && max is not null
            ? string.Format(CultureInfo.InvariantCulture, RangeFormat, _info.FormatMinor(min), _info.FormatMinor(max))
            : max is not null
                ? string.Format(CultureInfo.InvariantCulture, MaximumOnlyFormat, _info.FormatMinor(max))
                : min is not null
                    ? string.Format(CultureInfo.InvariantCulture, MinimumOnlyFormat, _info.FormatMinor(min))
                    : string.Empty;
        if (_range is not null)
        {
            _range.Text = RangeText;
        }
        if (_rangeFlash is not null)
        {
            _rangeFlash.Text = RangeText;
        }
        var atMax = max is not null && (CurrentMinor ?? 0) >= max;
        if (_chips is not null)
        {
            foreach (var child in _chips.Children)
            {
                if (child is MoneyInputChip chip)
                {
                    chip.IsAtLimit = atMax;
                }
            }
        }
        UpdateToolbar();
        UpdateMessages();
    }

    private void UpdateToolbar()
    {
        var hasChips = _chips is { Children.Count: > 0 };
        if (_chips is not null)
        {
            _chips.IsVisible = hasChips;
        }
        if (_range?.Parent is Control rangeHost)
        {
            rangeHost.IsVisible = RangeText.Length > 0;
        }
        if (_toolbar is not null)
        {
            _toolbar.IsVisible = hasChips || RangeText.Length > 0;
        }
    }

    private void UpdateMessages()
    {
        var minor = CurrentMinor;
        var min = MinimumMinor;
        var max = MaximumMinor;
        var belowMin = minor is not null && min is not null && minor < min;
        var outOfRange = belowMin || (minor is not null && max is not null && minor > max);
        string? builtIn = null;
        if (_touched && !IsFocused && outOfRange)
        {
            builtIn = belowMin
                ? string.Format(CultureInfo.InvariantCulture, MinimumErrorFormat, _info.FormatMinor(min))
                : string.Format(CultureInfo.InvariantCulture, MaximumErrorFormat, _info.FormatMinor(max));
        }
        // Arc shows one message row: the error replaces the description.
        _shownError = !string.IsNullOrEmpty(EffectiveErrorMessage) ? EffectiveErrorMessage : builtIn;
        if (_field is not null)
        {
            _field.ErrorMessage = _shownError;
            _field.Description = _shownError is null ? Description : null;
        }
        Frame?.SetCurrentValue(FieldFrame.IsInvalidProperty, IsInvalid || _shownError is not null);
        UpdateAutomation();
    }

    /// <summary>Names the editor after its label; the range, description and error describe it (Arc aria-describedby).</summary>
    protected override void UpdateAutomation()
    {
        base.UpdateAutomation();
        var parts = new List<string>(3);
        if (!string.IsNullOrEmpty(_rangeText))
        {
            parts.Add(_rangeText);
        }
        var message = _shownError ?? Description;
        if (!string.IsNullOrEmpty(message))
        {
            parts.Add(message);
        }
        AutomationProperties.SetHelpText(this, string.Join(" ", parts));
    }

    // ------------------------------------------------------------------ editing model (Arc applyRaw / setAmount / nudge)

    private void ApplyRaw(string raw, int caret, MoneyInputChangeSource origin)
    {
        var result = MoneyInputDraft.Sanitize(raw, caret, _group, _decimal, _info.Digits);
        if (result is not { } accepted)
        {
            // A refused keystroke keeps the caret where it was.
            _pendingCaret = MoneyInputDraft.RefusedCaret(raw, caret, _decimal);
            SyncText();
            return;
        }
        var nextMinor = MoneyInputDraft.ToMinor(accepted.Draft, _info.Digits);
        if (MaximumMinor is { } max && nextMinor is { } next && next > max)
        {
            _pendingCaret = Math.Max(0, accepted.Before - 1);
            Refuse(1, max);
            SyncText();
            return;
        }
        _pendingCaret = accepted.Before;
        SetAmount(accepted.Draft, MoneyInputSource.Type, origin);
    }

    private void SetAmount(string nextDraft, MoneyInputSource source, MoneyInputChangeSource origin)
    {
        var previous = CurrentMinor;
        var next = MoneyInputDraft.ToMinor(nextDraft, _info.Digits);
        _direction = (next ?? 0) >= (previous ?? 0) ? 1 : -1;
        _source = source;
        _draft = nextDraft;
        RefreshVisuals(source == MoneyInputSource.Type ? MoneyInputAmountMotion.Cut : MoneyInputAmountMotion.Roll);
        if (next != previous)
        {
            Emit(next, _info, origin);
        }
        if (source != MoneyInputSource.Type)
        {
            Announce(_info.FormatMinor(next));
        }
    }

    private void Emit(decimal? nextMinor, MoneyInputCurrencyInfo info, MoneyInputChangeSource origin)
    {
        var old = Value;
        var major = nextMinor is { } minor ? minor / info.MinorFactor : (decimal?)null;
        _syncingValue = true;
        try
        {
            SetCurrentValue(ValueProperty, major);
        }
        finally
        {
            _syncingValue = false;
        }
        RaiseValueChanged(old, major, nextMinor, info, origin);
    }

    private void RaiseValueChanged(decimal? oldValue, decimal? newValue, decimal? minor, MoneyInputCurrencyInfo info,
                                   MoneyInputChangeSource origin)
    {
        long? minorUnits = minor is { } n && n <= long.MaxValue && n >= long.MinValue ? (long)n : null;
        RaiseEvent(new MoneyInputValueChangedEventArgs(ValueChangedEvent, this, oldValue, newValue, info.Code, minorUnits,
            info.FormatMinor(minor), origin));
    }

    private void Nudge(decimal amount, MoneyInputSource source, MoneyInputChangeSource origin)
    {
        if (IsReadOnly || !IsEffectivelyEnabled)
        {
            return;
        }
        var current = CurrentMinor;
        var from = current ?? 0;
        var floor = MinimumMinor ?? 0;
        var ceiling = MaximumMinor ?? 9007199254740991m;
        var next = from + amount;
        if (next > ceiling)
        {
            if (from >= ceiling)
            {
                Refuse(1, ceiling);
                return;
            }
            next = ceiling;
            Refuse(1, ceiling);
        }
        if (next < floor)
        {
            if (from <= floor && current is not null)
            {
                Refuse(-1, floor);
                return;
            }
            next = floor;
        }
        _pendingCaret = int.MaxValue;
        SetAmount(MoneyInputDraft.FromMinor(next, _info.Digits), source, origin);
    }

    /// <summary>A limit refuses the change: the amount kicks toward the press, the range text flashes, the limit is announced.</summary>
    private void Refuse(int toward, decimal limit)
    {
        _stage?.Kick(toward);
        BeginFlash();
        Announce($"{(toward > 0 ? "Maximum" : "Minimum")} is {_info.FormatMinor(limit)}");
    }

    private void BeginFlash()
    {
        _flashing = true;
        _flashTimer.Stop();
        _flashTimer.Start();
        AnimateFlash(1, ArcMotionPresets.Instant);
    }

    private void EndFlash()
    {
        _flashTimer.Stop();
        _flashing = false;
        AnimateFlash(0, ArcMotionPresets.Standard);
    }

    private void AnimateFlash(double target, TimeSpan duration)
    {
        // CSS flips the range color with the state: instant into the warning, standard back out; reduced motion is a cut.
        _flash ??= CreateFlashMotion();
        if (ArcMotion.IsFull(this))
        {
            _ = _flash.AnimateTo(target, MotionSpec.Tween(duration, ArcEasings.Standard));
        }
        else
        {
            _flash.Jump(target);
        }
    }

    private MotionValue<double> CreateFlashMotion()
    {
        var motion = new MotionValue<double>(this, 0, MotionVectors.Double);
        motion.Changed += ApplyFlash;
        return motion;
    }

    private void ApplyFlash(double value)
    {
        if (_range is not null)
        {
            _range.Opacity = 1 - value;
        }
        if (_rangeFlash is not null)
        {
            _rangeFlash.Opacity = value;
        }
    }

    private void Announce(string message) => _live?.Announce(message);

    // ------------------------------------------------------------------ input

    protected override void OnTextInput(TextInputEventArgs e)
    {
        // Text typed into the currency switch, a chip or the menu bubbles through here: only the editor edits.
        if (!ReferenceEquals(e.Source, this))
        {
            return;
        }
        if (IsReadOnly || string.IsNullOrEmpty(e.Text))
        {
            e.Handled = true;
            return;
        }
        var input = e.Text;
        var lineBreak = input.IndexOfAny(['\r', '\n']);
        if (lineBreak >= 0)
        {
            input = input[..lineBreak];
        }
        var text = Text ?? string.Empty;
        var start = Math.Clamp(Math.Min(SelectionStart, SelectionEnd), 0, text.Length);
        var end = Math.Clamp(Math.Max(SelectionStart, SelectionEnd), 0, text.Length);
        ApplyRaw(text[..start] + input + text[end..], start + input.Length, MoneyInputChangeSource.Type);
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!ReferenceEquals(e.Source, this))
        {
            return;
        }
        var text = Text ?? string.Empty;
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        if (!IsReadOnly)
        {
            switch (e.Key)
            {
                case Key.Up:
                    e.Handled = true;
                    Nudge(StepMinor * (shift ? 10 : 1), MoneyInputSource.Key, MoneyInputChangeSource.Key);
                    return;
                case Key.Down:
                    e.Handled = true;
                    Nudge(-StepMinor * (shift ? 10 : 1), MoneyInputSource.Key, MoneyInputChangeSource.Key);
                    return;
                case Key.PageUp:
                    e.Handled = true;
                    Nudge(StepMinor * 10, MoneyInputSource.Key, MoneyInputChangeSource.Key);
                    return;
                case Key.PageDown:
                    e.Handled = true;
                    Nudge(-StepMinor * 10, MoneyInputSource.Key, MoneyInputChangeSource.Key);
                    return;
                case Key.Enter:
                    // Arc: Enter settles (12.5 → 12.50) and puts the caret at the end; the key still reaches forms.
                    _draft = MoneyInputDraft.Settle(_draft, _info.Digits);
                    _pendingCaret = int.MaxValue;
                    RefreshVisuals(MotionForSource());
                    return;
            }
            var start = SelectionStart;
            var plain = SelectionStart == SelectionEnd &&
                        (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta | KeyModifiers.Alt)) == 0;
            if (plain && e.Key == Key.Back && start > 1 && start <= text.Length && text[start - 1] == _group)
            {
                // Deleting a group separator deletes the digit before it instead of doing nothing.
                e.Handled = true;
                ApplyRaw(text[..(start - 2)] + text[(start - 1)..], start - 2, MoneyInputChangeSource.Type);
                return;
            }
            if (plain && e.Key == Key.Delete && start + 1 < text.Length && text[start] == _group)
            {
                e.Handled = true;
                ApplyRaw(text[..(start + 1)] + text[(start + 2)..], start, MoneyInputChangeSource.Type);
                return;
            }
        }
        _inBaseKey = true;
        try
        {
            base.OnKeyDown(e);
        }
        finally
        {
            _inBaseKey = false;
        }
        var after = Text ?? string.Empty;
        if (!string.Equals(text, after, StringComparison.Ordinal))
        {
            // The editor deleted something (Backspace, Delete, word deletion): read the result back into the draft.
            ApplyRaw(after, Math.Clamp(CaretIndex, 0, after.Length), MoneyInputChangeSource.Type);
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        // Focus moving to the switch or a chip bubbles here; only the editor's own focus shows the caret.
        if (ReferenceEquals(e.Source, this))
        {
            base.OnGotFocus(e);
        }
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        if (ReferenceEquals(e.Source, this))
        {
            base.OnLostFocus(e);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (e.Source is Visual source && _presenter is not null && (ReferenceEquals(source, _presenter) || _presenter.IsVisualAncestorOf(source)))
        {
            base.OnPointerPressed(e);
            return;
        }
        if (e.Source is not Visual visual || !this.IsVisualAncestorOf(visual) || !IsEffectivelyEnabled)
        {
            return;
        }
        // The amount area and the label focus the editor with the caret at the end (Arc amountWrap click, label for).
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            Focus(NavigationMethod.Pointer);
            var end = (Text ?? string.Empty).Length;
            SetCurrentValue(SelectionStartProperty, end);
            SetCurrentValue(SelectionEndProperty, end);
            SetCurrentValue(CaretIndexProperty, end);
            e.Handled = true;
        }
    }

    private void OnRequestBringIntoView(object? sender, RequestBringIntoViewEventArgs e)
    {
        // The amount scales to fit instead of scrolling: the caret never scrolls an outer viewer.
        if (_presenter is not null && ReferenceEquals(e.Source, _presenter))
        {
            e.Handled = true;
        }
    }

    private void OnPasting(object? sender, RoutedEventArgs e)
    {
        if (!ReferenceEquals(e.Source, this))
        {
            return;
        }
        e.Handled = true;
        if (!IsReadOnly)
        {
            PasteFromClipboard();
        }
    }

    private async void PasteFromClipboard()
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return;
        }
        string? text;
        try
        {
            text = await Avalonia.Input.Platform.ClipboardExtensions.TryGetTextAsync(clipboard);
        }
        catch (InvalidOperationException)
        {
            return;
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            return;
        }
        if (!string.IsNullOrEmpty(text))
        {
            PasteText(text);
        }
    }

    /// <summary>Arc onPaste: parses "$1,234.56", "1.234,56 €" or "1234.5"; replaces a whole-field selection, else inserts.</summary>
    internal void PasteText(string text, bool replaceWhole = false, MoneyInputChangeSource origin = MoneyInputChangeSource.Paste)
    {
        if (IsReadOnly)
        {
            return;
        }
        var pasted = MoneyInputDraft.ParsePaste(text, _info.Digits);
        if (pasted is null)
        {
            return;
        }
        var inserted = pasted.Replace('.', _decimal);
        var value = Text ?? string.Empty;
        var start = Math.Clamp(Math.Min(SelectionStart, SelectionEnd), 0, value.Length);
        var end = Math.Clamp(Math.Max(SelectionStart, SelectionEnd), 0, value.Length);
        var replaceAll = replaceWhole || (start == 0 && end == value.Length) || value.Length == 0;
        var raw = replaceAll ? inserted : value[..start] + inserted + value[end..];
        ApplyRaw(raw, replaceAll ? raw.Length : start + inserted.Length, origin);
    }

    private void OnCutting(object? sender, RoutedEventArgs e)
    {
        if (!ReferenceEquals(e.Source, this))
        {
            return;
        }
        e.Handled = true;
        var value = Text ?? string.Empty;
        var start = Math.Clamp(Math.Min(SelectionStart, SelectionEnd), 0, value.Length);
        var end = Math.Clamp(Math.Max(SelectionStart, SelectionEnd), 0, value.Length);
        if (end <= start)
        {
            return;
        }
        _ = ArcClipboard.CopyTextAsync(this, value[start..end]);
        if (!IsReadOnly)
        {
            ApplyRaw(value[..start] + value[end..], start, MoneyInputChangeSource.Type);
        }
    }

    // ------------------------------------------------------------------ chips

    private void RebuildChips()
    {
        if (_chips is null)
        {
            return;
        }
        foreach (var child in _chips.Children)
        {
            if (child is MoneyInputChip old)
            {
                old.Click -= OnChipClick;
            }
        }
        _chips.Children.Clear();
        if (QuickAddAmounts is { } amounts)
        {
            foreach (var amount in amounts)
            {
                if (amount <= 0)
                {
                    continue;
                }
                var chip = new MoneyInputChip { Amount = amount };
                TextElement.SetFontFeatures(chip, s_tabular);
                chip.Click += OnChipClick;
                _chips.Children.Add(chip);
            }
        }
        RefreshChipLabels();
        UpdateLimits();
    }

    private void RefreshChipLabels()
    {
        if (_chips is null)
        {
            return;
        }
        foreach (var child in _chips.Children)
        {
            if (child is MoneyInputChip chip)
            {
                var whole = _info.FormatWhole(chip.Amount);
                chip.Content = "+" + whole;
                AutomationProperties.SetName(chip, "Add " + whole);
            }
        }
    }

    private void OnChipClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MoneyInputChip chip)
        {
            Nudge(ToMinorUnits(chip.Amount), MoneyInputSource.Chip, MoneyInputChangeSource.Chip);
        }
    }

    // ------------------------------------------------------------------ currency menu

    private void OnCurrencyButtonClick(object? sender, RoutedEventArgs e)
    {
        if (_menuOpen)
        {
            CloseMenu(true, false);
        }
        else
        {
            OpenMenu(false);
        }
    }

    private void OnCurrencyButtonKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Down or Key.Up)
        {
            e.Handled = true;
            OpenMenu(true);
        }
    }

    private IReadOnlyList<string> CurrencyCodes()
    {
        var list = new List<string>();
        if (Currencies is { } currencies)
        {
            foreach (var code in currencies)
            {
                if (!string.IsNullOrWhiteSpace(code))
                {
                    list.Add(MoneyInputCurrencyTable.Lookup(code).Code);
                }
            }
        }
        return list;
    }

    private void EnsureMenu()
    {
        if (_popup is not null && _list is not null)
        {
            return;
        }
        _list = new MoneyInputCurrencyList();
        _list.KeyDown += OnMenuKeyDown;
        _list.Arranged = OnListArranged;
        _menu = new MoneyInputCurrencyMenu { Content = _list };
        _popup = new MoneyInputCurrencyPopup { Child = _menu };
        _popup.Closed += OnPopupClosed;
        _optionsStale = true;
    }

    private void BuildOptions()
    {
        if (_list is null)
        {
            return;
        }
        for (var i = _list.Children.Count - 1; i >= 0; i--)
        {
            if (_list.Children[i] is MoneyInputCurrencyOption old)
            {
                old.PointerMoved -= OnOptionPointerMoved;
                old.Tapped -= OnOptionTapped;
                old.Chosen = null;
                _list.Children.RemoveAt(i);
            }
        }
        foreach (var code in CurrencyCodes())
        {
            var info = MoneyInputCurrencyInfo.Get(code, _culture);
            var option = new MoneyInputCurrencyOption
            {
                Symbol = info.Symbol,
                Code = info.Code,
                CurrencyName = info.Name,
                Chosen = o => Choose(o.Code, false)
            };
            TextElement.SetFontFeatures(option, s_tabular);
            option.PointerMoved += OnOptionPointerMoved;
            option.Tapped += OnOptionTapped;
            _list.Children.Add(option);
        }
        _optionsStale = false;
        UpdateOptionStates();
    }

    private void UpdateOptionStates()
    {
        if (_list is null)
        {
            return;
        }
        foreach (var option in _list.Options)
        {
            option.IsSelected = string.Equals(option.Code, _info.Code, StringComparison.Ordinal);
            option.IsActive = _menuOpen && string.Equals(option.Code, _activeCode, StringComparison.Ordinal);
        }
    }

    private void OpenMenu(bool keyboard)
    {
        if (_menuOpen || !IsEffectivelyEnabled || _button is null || !_button.IsVisible || Frame is null || CurrencyCodes().Count < 2)
        {
            return;
        }
        EnsureMenu();
        if (_optionsStale)
        {
            BuildOptions();
        }
        var popup = _popup!;
        _activeCode = _info.Code;
        _menuOpen = true;
        IsCurrencyMenuOpen = true;
        _button.IsOpen = true;
        UpdateOptionStates();
        _list!.Highlight.Hide();
        _highlightPending = true;
        if (popup.Parent is null)
        {
            ((ISetLogicalParent)popup).SetParent(this);
        }
        popup.PlacementTarget = Frame;
        popup.OverlayInputPassThroughElement = _button;
        popup.IsOpen = true;
        if (!popup.IsOpen)
        {
            _menuOpen = false;
            IsCurrencyMenuOpen = false;
            _button.IsOpen = false;
            return;
        }
        Dispatcher.UIThread.Post(() =>
        {
            if (_menuOpen && _list is not null)
            {
                _list.Focus(keyboard ? NavigationMethod.Tab : NavigationMethod.Pointer);
                ActiveOption()?.BringIntoView();
            }
        }, DispatcherPriority.Loaded);
    }

    private void CloseMenu(bool focusButton, bool keyboard)
    {
        if (!_menuOpen)
        {
            return;
        }
        _menuOpen = false;
        IsCurrencyMenuOpen = false;
        _highlightPending = false;
        if (_button is not null)
        {
            _button.IsOpen = false;
        }
        _list?.Highlight.Hide();
        UpdateOptionStates();
        if (_popup is { } popup)
        {
            popup.IsOpen = false;
        }
        if (focusButton)
        {
            _button?.Focus(keyboard ? NavigationMethod.Tab : NavigationMethod.Pointer);
        }
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        if (_menuOpen)
        {
            // Light dismiss (a press outside, the window losing focus): sync the state.
            _menuOpen = false;
            IsCurrencyMenuOpen = false;
            _highlightPending = false;
            if (_button is not null)
            {
                _button.IsOpen = false;
            }
            _list?.Highlight.Hide();
            UpdateOptionStates();
        }
        if (_popup is { IsOpen: false, Parent: not null } popup)
        {
            ((ISetLogicalParent)popup).SetParent(null);
        }
    }

    private MoneyInputCurrencyOption? ActiveOption()
    {
        if (_list is null)
        {
            return null;
        }
        foreach (var option in _list.Options)
        {
            if (string.Equals(option.Code, _activeCode, StringComparison.Ordinal))
            {
                return option;
            }
        }
        return null;
    }

    private void OnListArranged()
    {
        if (_highlightPending && _menuOpen)
        {
            PlaceHighlight();
        }
    }

    private void PlaceHighlight()
    {
        if (_list is null || ActiveOption() is not { } option || option.Bounds.Height <= 0)
        {
            _highlightPending = _menuOpen;
            return;
        }
        _highlightPending = false;
        _list.Highlight.MoveTo(option.Bounds.Y, option.Bounds.Height);
    }

    private void SetActive(string code)
    {
        if (string.Equals(code, _activeCode, StringComparison.Ordinal))
        {
            return;
        }
        _activeCode = code;
        UpdateOptionStates();
        PlaceHighlight();
        ActiveOption()?.BringIntoView();
    }

    private void OnOptionPointerMoved(object? sender, PointerEventArgs e)
    {
        if (sender is MoneyInputCurrencyOption { Code: { } code } && e.Pointer.Type == PointerType.Mouse && _menuOpen)
        {
            SetActive(code);
        }
    }

    private void OnOptionTapped(object? sender, TappedEventArgs e)
    {
        if (sender is MoneyInputCurrencyOption { Code: { } code })
        {
            e.Handled = true;
            Choose(code, false);
        }
    }

    private void OnMenuKeyDown(object? sender, KeyEventArgs e)
    {
        var codes = CurrencyCodes();
        if (codes.Count == 0)
        {
            return;
        }
        var at = Math.Max(-1, IndexOf(codes, _activeCode));
        switch (e.Key)
        {
            case Key.Down:
                e.Handled = true;
                SetActive(codes[Math.Clamp(at + 1, 0, codes.Count - 1)]);
                return;
            case Key.Up:
                e.Handled = true;
                SetActive(codes[Math.Clamp(at - 1, 0, codes.Count - 1)]);
                return;
            case Key.Home:
                e.Handled = true;
                SetActive(codes[0]);
                return;
            case Key.End:
                e.Handled = true;
                SetActive(codes[^1]);
                return;
            case Key.Enter:
            case Key.Space:
                e.Handled = true;
                if (at >= 0)
                {
                    Choose(codes[at], true);
                }
                return;
            case Key.Escape:
                e.Handled = true;
                CloseMenu(true, true);
                return;
            case Key.Tab:
                e.Handled = true;
                // The list sits right after the switch in Arc's DOM: Shift+Tab lands on the switch, Tab continues after it.
                CloseMenu(true, true);
                if (!e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                {
                    TopLevel.GetTopLevel(this)?.FocusManager?.TryMoveFocus(NavigationDirection.Next);
                }
                return;
        }
        if (e.KeySymbol is { Length: 1 } symbol && (char.IsAsciiLetterOrDigit(symbol[0]) || symbol[0] == '_') &&
            (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta | KeyModifiers.Alt)) == 0)
        {
            var now = Environment.TickCount64;
            var key = symbol.ToLowerInvariant();
            _typeahead = now - _typeaheadAt > 600 ? key : _typeahead + key;
            _typeaheadAt = now;
            foreach (var code in codes)
            {
                var info = MoneyInputCurrencyInfo.Get(code, _culture);
                if (code.StartsWith(_typeahead, StringComparison.OrdinalIgnoreCase) ||
                    info.Name.StartsWith(_typeahead, StringComparison.OrdinalIgnoreCase))
                {
                    e.Handled = true;
                    SetActive(code);
                    break;
                }
            }
        }
    }

    private static int IndexOf(IReadOnlyList<string> codes, string code)
    {
        for (var i = 0; i < codes.Count; i++)
        {
            if (string.Equals(codes[i], code, StringComparison.Ordinal))
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>Arc chooseCurrency: keeps the major amount (convertDraft) and settles it unless the editor is focused.</summary>
    private void Choose(string? code, bool keyboard)
    {
        CloseMenu(true, keyboard);
        if (code is null || string.Equals(code, _info.Code, StringComparison.Ordinal) || IsReadOnly)
        {
            return;
        }
        var next = MoneyInputCurrencyInfo.Get(code, _culture);
        var oldCode = _info.Code;
        var converted = MoneyInputDraft.Convert(_draft, next.Digits);
        var nextDraft = IsFocused ? converted : MoneyInputDraft.Settle(converted, next.Digits);
        var nextMinor = MoneyInputDraft.ToMinor(nextDraft, next.Digits);
        _info = next;
        _activeCode = next.Code;
        _syncingCurrency = true;
        try
        {
            SetCurrentValue(CurrencyProperty, next.Code);
        }
        finally
        {
            _syncingCurrency = false;
        }
        _source = MoneyInputSource.External;
        _draft = nextDraft;
        RefreshChipLabels();
        RefreshVisuals(MoneyInputAmountMotion.Roll);
        UpdateOptionStates();
        RaiseEvent(new MoneyInputCurrencyChangedEventArgs(CurrencyChangedEvent, this, oldCode, next.Code,
            MoneyInputCurrencyChangeSource.Menu));
        Emit(nextMinor, next, MoneyInputChangeSource.Currency);
        Announce(nextMinor is null ? next.Name : $"{next.Name}, {next.FormatMinor(nextMinor)}");
    }

    // ------------------------------------------------------------------ automation

    protected override AutomationPeer OnCreateAutomationPeer() => new MoneyInputAutomationPeer(this);

    internal void SetValueFromAutomation(decimal value)
    {
        if (IsReadOnly || !IsEffectivelyEnabled)
        {
            return;
        }
        var minor = Math.Round(ToMinorUnits(Math.Abs(value)), 0, MidpointRounding.AwayFromZero);
        if (MaximumMinor is { } max && minor > max)
        {
            minor = max;
        }
        if (MinimumMinor is { } min && minor < min)
        {
            minor = min;
        }
        SetAmount(MoneyInputDraft.FromMinor(minor, _info.Digits), MoneyInputSource.Key, MoneyInputChangeSource.Automation);
    }

    /// <summary>Spin button with the range pattern in major units; the value text is the full currency string or "Empty".</summary>
    private sealed class MoneyInputAutomationPeer : TextBoxAutomationPeer, IValueProvider, IRangeValueProvider
    {
        public MoneyInputAutomationPeer(MoneyInput owner) : base(owner)
        {
        }

        private MoneyInput Money => (MoneyInput)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Spinner;

        string? IValueProvider.Value => Money.ValueText;

        void IValueProvider.SetValue(string? value)
        {
            if (value is not null)
            {
                Money.PasteText(value, replaceWhole: true, MoneyInputChangeSource.Automation);
            }
        }

        double IRangeValueProvider.Minimum => (double)(Money.Minimum ?? 0m);

        double IRangeValueProvider.Maximum => (double)(Money.Maximum ?? 999_999_999_999m);

        double IRangeValueProvider.Value => (double)(Money.Value ?? 0m);

        double IRangeValueProvider.LargeChange => (double)(Money.StepMinor * 10 / Money._info.MinorFactor);

        double IRangeValueProvider.SmallChange => (double)(Money.StepMinor / Money._info.MinorFactor);

        void IRangeValueProvider.SetValue(double value) => Money.SetValueFromAutomation((decimal)value);
    }
}

/// <summary>Where a change came from (Arc <c>Source</c>): typing cuts straight to the result, the rest roll.</summary>
internal enum MoneyInputSource
{
    Type,
    Chip,
    Key,
    External
}
