using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Input.TextInput;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// Arc phone input: a phone field with a country picker. The number formats as it is typed in the selected country's
/// shape (separators only print while digits follow, a typed trunk prefix is kept) with a faint guide showing the rest
/// of the expected number; the value comes out in E.164. The flag button grows into a searchable country list (one
/// morphing surface), pasted or autofilled international numbers pick their own country (+1 picks Canada by area code),
/// and typing "+" in the number jumps into the picker with a calling-code search. After blur an incomplete or too long
/// number shows "Numbers in United Kingdom have 10 digits" unless <see cref="IsValidationEnabled"/> is off or an
/// <see cref="FieldTextBox.ErrorMessage"/> replaces it; a valid number shows a check.
/// <para>Keyboard: on the country button ArrowDown / ArrowUp open the list and a printable key opens it seeded with that
/// key; in the search ArrowDown / ArrowUp, PageDown / PageUp (8 rows), Home / End (with an empty search) move, Enter
/// picks, Escape closes and returns to the button, Tab closes. In the number, Backspace / Delete on a separator delete
/// the digit beside it.</para>
/// </summary>
[TemplatePart(FieldPart, typeof(Field))]
[TemplatePart(AnchorPart, typeof(PhoneInputAnchor))]
[TemplatePart(TriggerPart, typeof(PhoneInputTrigger))]
[TemplatePart(FacePart, typeof(PhoneInputFace))]
[TemplatePart(GuidePart, typeof(PhoneInputGuide))]
[TemplatePart(ValidPart, typeof(PhoneInputPresence))]
[TemplatePart(PopupPart, typeof(PhoneInputPopup))]
[TemplatePart(LivePart, typeof(SpecialInputsLiveRegion))]
[PseudoClasses(OpenPseudoClass, ValidPseudoClass)]
public partial class PhoneInput : FieldTextBox, ICustomKeyboardNavigation
{
    /// <summary>The label and message rows.</summary>
    public const string FieldPart = "PART_Field";

    /// <summary>Holds the closed country trigger's place and springs its width.</summary>
    public const string AnchorPart = "PART_Anchor";

    /// <summary>The country button (flag, calling code, chevron).</summary>
    public const string TriggerPart = "PART_Trigger";

    /// <summary>The flag and calling code inside the trigger.</summary>
    public const string FacePart = "PART_Face";

    /// <summary>The faint typing guide after the number.</summary>
    public const string GuidePart = "PART_Guide";

    /// <summary>The valid check.</summary>
    public const string ValidPart = "PART_Valid";

    /// <summary>The popup hosting the morphing country surface.</summary>
    public const string PopupPart = "PART_Popup";

    /// <summary>The hidden polite live region.</summary>
    public const string LivePart = "PART_Live";

    /// <summary>Set while the country list is open.</summary>
    public const string OpenPseudoClass = ":open";

    /// <summary>Set while the number has a valid length for its country.</summary>
    public const string ValidPseudoClass = ":valid";

    private static readonly string[] s_defaultPreferred = { "US", "CA", "GB" };

    #region 公共属性定义

    /// <summary>Defines the <see cref="Value"/> property.</summary>
    public static readonly StyledProperty<string> ValueProperty =
        AvaloniaProperty.Register<PhoneInput, string>(nameof(Value), string.Empty, defaultBindingMode: BindingMode.TwoWay,
            coerce: (_, value) => value ?? string.Empty);

    /// <summary>Defines the <see cref="Country"/> property.</summary>
    public static readonly StyledProperty<string> CountryProperty =
        AvaloniaProperty.Register<PhoneInput, string>(nameof(Country), "US", defaultBindingMode: BindingMode.TwoWay,
            coerce: (_, value) => string.IsNullOrEmpty(value) ? "US" : value.ToUpperInvariant());

    /// <summary>Defines the <see cref="Countries"/> property.</summary>
    public static readonly StyledProperty<IList<string>?> CountriesProperty =
        AvaloniaProperty.Register<PhoneInput, IList<string>?>(nameof(Countries));

    /// <summary>Defines the <see cref="PreferredCountries"/> property.</summary>
    public static readonly StyledProperty<IList<string>> PreferredCountriesProperty =
        AvaloniaProperty.Register<PhoneInput, IList<string>>(nameof(PreferredCountries), s_defaultPreferred,
            coerce: (_, value) => value ?? Array.Empty<string>());

    /// <summary>Defines the <see cref="IsValidationEnabled"/> property.</summary>
    public static readonly StyledProperty<bool> IsValidationEnabledProperty =
        AvaloniaProperty.Register<PhoneInput, bool>(nameof(IsValidationEnabled), true);

    /// <summary>Defines the <see cref="IsRequired"/> property.</summary>
    public static readonly StyledProperty<bool> IsRequiredProperty =
        AvaloniaProperty.Register<PhoneInput, bool>(nameof(IsRequired));

    /// <summary>Defines the <see cref="SearchPlaceholder"/> property.</summary>
    public static readonly StyledProperty<string> SearchPlaceholderProperty =
        AvaloniaProperty.Register<PhoneInput, string>(nameof(SearchPlaceholder), "Country or code");

    /// <summary>Defines the <see cref="SearchLabel"/> property.</summary>
    public static readonly StyledProperty<string> SearchLabelProperty =
        AvaloniaProperty.Register<PhoneInput, string>(nameof(SearchLabel), "Search countries or calling codes");

    /// <summary>Defines the <see cref="ListLabel"/> property.</summary>
    public static readonly StyledProperty<string> ListLabelProperty =
        AvaloniaProperty.Register<PhoneInput, string>(nameof(ListLabel), "Countries");

    /// <summary>Defines the <see cref="SuggestedLabel"/> property.</summary>
    public static readonly StyledProperty<string> SuggestedLabelProperty =
        AvaloniaProperty.Register<PhoneInput, string>(nameof(SuggestedLabel), "Suggested");

    /// <summary>Defines the <see cref="AllCountriesLabel"/> property.</summary>
    public static readonly StyledProperty<string> AllCountriesLabelProperty =
        AvaloniaProperty.Register<PhoneInput, string>(nameof(AllCountriesLabel), "All countries");

    /// <summary>Defines the <see cref="NoMatchesTextFormat"/> property.</summary>
    public static readonly StyledProperty<string> NoMatchesTextFormatProperty =
        AvaloniaProperty.Register<PhoneInput, string>(nameof(NoMatchesTextFormat), "No countries match “{0}”");

    /// <summary>Defines the <see cref="ValidationTextFormat"/> property.</summary>
    public static readonly StyledProperty<string> ValidationTextFormatProperty =
        AvaloniaProperty.Register<PhoneInput, string>(nameof(ValidationTextFormat), "Numbers in {0} have {1} digits");

    /// <summary>Defines the <see cref="ClearSearchLabel"/> property.</summary>
    public static readonly StyledProperty<string> ClearSearchLabelProperty =
        AvaloniaProperty.Register<PhoneInput, string>(nameof(ClearSearchLabel), "Clear search");

    /// <summary>Defines the <see cref="CloseListLabel"/> property.</summary>
    public static readonly StyledProperty<string> CloseListLabelProperty =
        AvaloniaProperty.Register<PhoneInput, string>(nameof(CloseListLabel), "Close country list");

    /// <summary>Defines the <see cref="Status"/> property.</summary>
    public static readonly DirectProperty<PhoneInput, PhoneNumberStatus> StatusProperty =
        AvaloniaProperty.RegisterDirect<PhoneInput, PhoneNumberStatus>(nameof(Status), o => o.Status);

    /// <summary>Defines the <see cref="SelectedCountry"/> property.</summary>
    public static readonly DirectProperty<PhoneInput, PhoneCountry> SelectedCountryProperty =
        AvaloniaProperty.RegisterDirect<PhoneInput, PhoneCountry>(nameof(SelectedCountry), o => o.SelectedCountry);

    /// <summary>Defines the <see cref="ValidationMessage"/> property.</summary>
    public static readonly DirectProperty<PhoneInput, string?> ValidationMessageProperty =
        AvaloniaProperty.RegisterDirect<PhoneInput, string?>(nameof(ValidationMessage), o => o.ValidationMessage);

    /// <summary>Defines the <see cref="IsPickerOpen"/> property.</summary>
    public static readonly DirectProperty<PhoneInput, bool> IsPickerOpenProperty =
        AvaloniaProperty.RegisterDirect<PhoneInput, bool>(nameof(IsPickerOpen), o => o.IsPickerOpen);

    /// <summary>Defines the <see cref="ValueChanged"/> event.</summary>
    public static readonly RoutedEvent<PhoneInputValueChangedEventArgs> ValueChangedEvent =
        RoutedEvent.Register<PhoneInput, PhoneInputValueChangedEventArgs>(nameof(ValueChanged), RoutingStrategies.Bubble);

    /// <summary>The number in E.164, such as "+14155550132" (two-way); "" when there are no digits.</summary>
    public string Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>ISO 3166-1 alpha-2 code of the selected country (two-way, default "US").</summary>
    public string Country { get => GetValue(CountryProperty); set => SetValue(CountryProperty, value); }

    /// <summary>Limits the picker to these ISO codes (null or empty: all 49 countries).</summary>
    public IList<string>? Countries { get => GetValue(CountriesProperty); set => SetValue(CountriesProperty, value); }

    /// <summary>Pinned at the top of the picker under "Suggested" (default US, CA, GB; empty: no Suggested group).</summary>
    public IList<string> PreferredCountries { get => GetValue(PreferredCountriesProperty); set => SetValue(PreferredCountriesProperty, value); }

    /// <summary>Shows a message after blur when the number is incomplete or too long (default true).</summary>
    public bool IsValidationEnabled { get => GetValue(IsValidationEnabledProperty); set => SetValue(IsValidationEnabledProperty, value); }

    /// <summary>Marks the number as required for assistive technology and forms.</summary>
    public bool IsRequired { get => GetValue(IsRequiredProperty); set => SetValue(IsRequiredProperty, value); }

    /// <summary>Placeholder of the picker's search field ("Country or code").</summary>
    public string SearchPlaceholder { get => GetValue(SearchPlaceholderProperty); set => SetValue(SearchPlaceholderProperty, value); }

    /// <summary>Accessible name of the search field ("Search countries or calling codes").</summary>
    public string SearchLabel { get => GetValue(SearchLabelProperty); set => SetValue(SearchLabelProperty, value); }

    /// <summary>Accessible name of the country list ("Countries").</summary>
    public string ListLabel { get => GetValue(ListLabelProperty); set => SetValue(ListLabelProperty, value); }

    /// <summary>Label of the preferred group ("Suggested").</summary>
    public string SuggestedLabel { get => GetValue(SuggestedLabelProperty); set => SetValue(SuggestedLabelProperty, value); }

    /// <summary>Label of the full list when a Suggested group is shown ("All countries").</summary>
    public string AllCountriesLabel { get => GetValue(AllCountriesLabelProperty); set => SetValue(AllCountriesLabelProperty, value); }

    /// <summary>Empty search message; <c>{0}</c> is the query ("No countries match “{0}”").</summary>
    public string NoMatchesTextFormat { get => GetValue(NoMatchesTextFormatProperty); set => SetValue(NoMatchesTextFormatProperty, value); }

    /// <summary>Built-in validation message; <c>{0}</c> is the country, <c>{1}</c> the lengths ("Numbers in {0} have {1} digits").</summary>
    public string ValidationTextFormat { get => GetValue(ValidationTextFormatProperty); set => SetValue(ValidationTextFormatProperty, value); }

    /// <summary>Accessible name of the search clear button ("Clear search").</summary>
    public string ClearSearchLabel { get => GetValue(ClearSearchLabelProperty); set => SetValue(ClearSearchLabelProperty, value); }

    /// <summary>Accessible name of the close target over the chevron ("Close country list").</summary>
    public string CloseListLabel { get => GetValue(CloseListLabelProperty); set => SetValue(CloseListLabelProperty, value); }

    /// <summary>Where the number stands against the country's lengths.</summary>
    public PhoneNumberStatus Status
    {
        get => _status;
        private set => SetAndRaise(StatusProperty, ref _status, value);
    }

    /// <summary>The selected country's table entry.</summary>
    public PhoneCountry SelectedCountry
    {
        get => _country;
        private set => SetAndRaise(SelectedCountryProperty, ref _country, value);
    }

    /// <summary>The built-in validation message currently shown, or null.</summary>
    public string? ValidationMessage
    {
        get => _validationMessage;
        private set => SetAndRaise(ValidationMessageProperty, ref _validationMessage, value);
    }

    /// <summary>True while the country list is open.</summary>
    public bool IsPickerOpen
    {
        get => _open;
        private set => SetAndRaise(IsPickerOpenProperty, ref _open, value);
    }

    /// <summary>Raised on every edit (typing, paste, picking a country) and when <see cref="Value"/> changes.</summary>
    public event EventHandler<PhoneInputValueChangedEventArgs>? ValueChanged
    {
        add => AddHandler(ValueChangedEvent, value);
        remove => RemoveHandler(ValueChangedEvent, value);
    }

    #endregion

    #region 内部属性定义

    internal static readonly DirectProperty<PhoneInput, string?> DisplayErrorMessageProperty =
        AvaloniaProperty.RegisterDirect<PhoneInput, string?>(nameof(DisplayErrorMessage), o => o.DisplayErrorMessage);

    internal static readonly DirectProperty<PhoneInput, string?> DisplayDescriptionProperty =
        AvaloniaProperty.RegisterDirect<PhoneInput, string?>(nameof(DisplayDescription), o => o.DisplayDescription);

    internal static readonly DirectProperty<PhoneInput, bool> DisplayInvalidProperty =
        AvaloniaProperty.RegisterDirect<PhoneInput, bool>(nameof(DisplayInvalid), o => o.DisplayInvalid);

    /// <summary>The error row: the explicit or binding error, else the built-in validation message.</summary>
    internal string? DisplayErrorMessage
    {
        get => _displayError;
        private set => SetAndRaise(DisplayErrorMessageProperty, ref _displayError, value);
    }

    /// <summary>Arc shows the description only while there is no message.</summary>
    internal string? DisplayDescription
    {
        get => _displayDescription;
        private set => SetAndRaise(DisplayDescriptionProperty, ref _displayDescription, value);
    }

    internal bool DisplayInvalid
    {
        get => _displayInvalid;
        private set => SetAndRaise(DisplayInvalidProperty, ref _displayInvalid, value);
    }

    #endregion

    private PhoneCountry _country;
    private string _digits = string.Empty;
    private string _e164 = string.Empty;
    private PhoneNumberStatus _status;
    private string? _validationMessage;
    private string? _displayError;
    private string? _displayDescription;
    private bool _displayInvalid;
    private bool _touched;
    private bool _syncing;
    private bool _writingText;
    private bool _normalizePending;
    private int _roll = 1;
    private string _announcement = string.Empty;
    private string _liveText = string.Empty;
    private IReadOnlyList<PhoneCountry> _pool = PhoneNumbers.Countries;

    private Field? _field;
    private PhoneInputAnchor? _anchor;
    private PhoneInputTrigger? _trigger;
    private PhoneInputFace? _face;
    private PhoneInputGuide? _guide;
    private PhoneInputPresence? _valid;
    private PhoneInputPopup? _popup;
    private SpecialInputsLiveRegion? _live;

    static PhoneInput()
    {
        TextInputOptions.ContentTypeProperty.OverrideDefaultValue<PhoneInput>(TextInputContentType.Digits);
        AcceptsReturnProperty.OverrideDefaultValue<PhoneInput>(false);
    }

    /// <summary>Creates a phone input on the United States with an empty number.</summary>
    public PhoneInput()
    {
        _country = PhoneNumbers.Find("US")!;
        // Tabular digits for the number, its guide and the calling codes (Arc: font-variant-numeric: tabular-nums).
        SetValue(TextElement.FontFeaturesProperty, PhoneInputFace.TabularNumbers, BindingPriority.Style);
        AddHandler(PastingFromClipboardEvent, OnPastingFromClipboard);
    }

    /// <summary>The countries the picker offers (the table filtered by <see cref="Countries"/>).</summary>
    public IReadOnlyList<PhoneCountry> AvailableCountries => _pool;

    /// <summary>The number as typed (national digits, a trunk prefix included).</summary>
    internal string Digits => _digits;

    // ---------------------------------------------------------------- template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_trigger is not null)
        {
            _trigger.Field = null;
        }
        if (_popup is not null)
        {
            _popup.Closed -= OnPopupClosed;
            _popup.Child = null;
        }
        if (_anchor is not null)
        {
            _anchor.TargetChanged -= OnAnchorTargetChanged;
        }
        _surface = null;
        _field = e.NameScope.Find<Field>(FieldPart);
        _anchor = e.NameScope.Find<PhoneInputAnchor>(AnchorPart);
        _trigger = e.NameScope.Find<PhoneInputTrigger>(TriggerPart);
        _face = e.NameScope.Find<PhoneInputFace>(FacePart);
        _guide = e.NameScope.Find<PhoneInputGuide>(GuidePart);
        _valid = e.NameScope.Find<PhoneInputPresence>(ValidPart);
        _popup = e.NameScope.Find<PhoneInputPopup>(PopupPart);
        _live = e.NameScope.Find<SpecialInputsLiveRegion>(LivePart);
        if (_trigger is not null)
        {
            _trigger.Field = this;
        }
        if (_anchor is not null)
        {
            _anchor.TargetChanged += OnAnchorTargetChanged;
        }
        if (_valid is not null)
        {
            // P26: snappy pop from 0.6 through a subtle blur; leaves to 0.8 in 0.1s. Reduced: a 0.12s fade.
            _valid.EnterSpring = ArcMotionPresets.Snappy;
            _valid.EnterScale = 0.6;
            _valid.ExitScale = 0.8;
            _valid.EnterBlur = 2;
            _valid.ExitDuration = TimeSpan.FromMilliseconds(100);
            _valid.ReducedDuration = TimeSpan.FromMilliseconds(120);
        }
        if (_popup is not null)
        {
            ConfigurePopup(_popup);
        }
        _face?.Show(_country, _roll, animate: false);
        UpdateDisplay(announce: false);
    }

    // ---------------------------------------------------------------- properties

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty)
        {
            if (!_syncing)
            {
                OnValueSet(change.GetNewValue<string>());
            }
        }
        else if (change.Property == CountryProperty)
        {
            if (!_syncing)
            {
                var entry = PhoneNumbers.Find(change.GetNewValue<string>()) ?? PhoneNumbers.Countries[0];
                if (!ReferenceEquals(entry, _country))
                {
                    Commit(entry, _digits, user: false);
                }
            }
        }
        else if (change.Property == TextProperty)
        {
            if (!_writingText)
            {
                ScheduleNormalize();
            }
        }
        else if (change.Property == CountriesProperty)
        {
            UpdatePool();
        }
        else if (change.Property == PreferredCountriesProperty || change.Property == SuggestedLabelProperty ||
                 change.Property == AllCountriesLabelProperty || change.Property == NoMatchesTextFormatProperty)
        {
            RefreshListIfOpen();
        }
        else if (change.Property == IsValidationEnabledProperty || change.Property == EffectiveErrorMessageProperty ||
                 change.Property == DescriptionProperty || change.Property == ValidationTextFormatProperty ||
                 change.Property == IsInvalidProperty)
        {
            UpdateValidation();
        }
        else if (change.Property == IsRequiredProperty)
        {
            AutomationProperties.SetIsRequiredForForm(this, change.GetNewValue<bool>());
        }
        else if (change.Property == SearchPlaceholderProperty || change.Property == SearchLabelProperty ||
                 change.Property == ListLabelProperty || change.Property == ClearSearchLabelProperty ||
                 change.Property == CloseListLabelProperty)
        {
            UpdatePickerTexts();
        }
        else if (change.Property == IsEffectivelyEnabledProperty && !IsEffectivelyEnabled)
        {
            ClosePicker(PhoneInputFocusTarget.None, instant: true);
        }
        else if (change.Property == SurfaceBrushProperty || change.Property == SurfaceRaisedBrushProperty ||
                 change.Property == EdgeBrushProperty || change.Property == FloatingShadowProperty)
        {
            ApplySurfaceStyle();
        }
    }

    private void UpdatePool()
    {
        var codes = Countries;
        _pool = codes is { Count: > 0 }
            ? PhoneNumbers.Countries.Where(c => codes.Contains(c.Iso, StringComparer.OrdinalIgnoreCase)).ToArray()
            : PhoneNumbers.Countries;
        _order = null;
        _sorted = null;
        RefreshListIfOpen();
    }

    /// <summary>A Value that differs from what the field last produced is read back in (no announcements).</summary>
    private void OnValueSet(string value)
    {
        if (value == _e164)
        {
            return;
        }
        var parsed = PhoneNumbers.Parse(value, _pool);
        if (parsed is { } result)
        {
            Commit(result.Country, result.National, user: false);
        }
        else
        {
            Commit(_country, string.Empty, user: false);
        }
    }

    // ---------------------------------------------------------------- state

    /// <summary>
    /// Makes <paramref name="country"/> + <paramref name="digits"/> the field's number. User edits always raise
    /// <see cref="ValueChanged"/> (Arc's onValueChange) and may announce; programmatic changes raise it only when the
    /// E.164 value changed and never announce.
    /// </summary>
    private void Commit(PhoneCountry country, string digits, bool user)
    {
        var oldValue = _e164;
        var countryChanged = !ReferenceEquals(country, _country);
        SelectedCountry = country;
        _digits = digits;
        _e164 = PhoneNumbers.ToE164(country, digits);
        _syncing = true;
        try
        {
            if (countryChanged || !string.Equals(Country, country.Iso, StringComparison.Ordinal))
            {
                SetCurrentValue(CountryProperty, country.Iso);
            }
            if (Value != _e164)
            {
                SetCurrentValue(ValueProperty, _e164);
            }
        }
        finally
        {
            _syncing = false;
        }
        UpdateDisplay(announce: user);
        if (user || oldValue != _e164)
        {
            RaiseEvent(new PhoneInputValueChangedEventArgs(ValueChangedEvent, oldValue, _e164, country,
                PhoneNumbers.FormatNational(country, digits), PhoneNumbers.StatusOf(country, digits)));
        }
    }

    private void UpdateDisplay(bool announce)
    {
        var formatted = PhoneNumbers.FormatNational(_country, _digits);
        WriteText(formatted);
        Status = PhoneNumbers.StatusOf(_country, _digits);
        PseudoClasses.Set(ValidPseudoClass, Status == PhoneNumberStatus.Valid);
        if (_guide is not null)
        {
            _guide.Typed = formatted;
            _guide.Guide = PhoneNumbers.Guide(_country, _digits, formatted);
        }
        if (_valid is not null)
        {
            _valid.IsPresent = Status == PhoneNumberStatus.Valid;
        }
        var surfaceVisible = _popup?.IsOpen == true;
        // Under the open surface the frame's trigger changes at once; the lid on top carries the roll.
        _face?.Show(_country, _roll, animate: !surfaceVisible);
        if (surfaceVisible)
        {
            _lidFace?.Show(_country, _roll, animate: true);
        }
        else
        {
            _lidFace?.Show(_country, _roll, animate: false);
        }
        if (_trigger is not null)
        {
            AutomationProperties.SetName(_trigger, $"Country, {_country.Name} +{_country.Dial}");
        }
        if (_list is not null)
        {
            _list.SelectedCountry = _country;
        }
        UpdateValidation();
        UpdateLive(announce);
    }

    private void WriteText(string formatted)
    {
        if (Text == formatted)
        {
            return;
        }
        _writingText = true;
        try
        {
            SetCurrentValue(TextProperty, formatted);
        }
        finally
        {
            _writingText = false;
        }
    }

    private void UpdateValidation()
    {
        var status = PhoneNumbers.StatusOf(_country, _digits);
        var builtIn = IsValidationEnabled && _touched && status is PhoneNumberStatus.Incomplete or PhoneNumberStatus.TooLong
            ? string.Format(System.Globalization.CultureInfo.InvariantCulture, ValidationTextFormat, _country.Name, PhoneNumbers.LengthCopy(_country))
            : null;
        ValidationMessage = builtIn;
        var explicitError = EffectiveErrorMessage;
        var message = !string.IsNullOrEmpty(explicitError) ? explicitError : builtIn;
        DisplayErrorMessage = message;
        DisplayDescription = string.IsNullOrEmpty(message) ? Description : null;
        DisplayInvalid = IsInvalid || !string.IsNullOrEmpty(message);
        PseudoClasses.Set(InvalidPseudoClass, DisplayInvalid);
        UpdateAutomation();
    }

    protected override void UpdateAutomation()
    {
        base.UpdateAutomation();
        AutomationProperties.SetHelpText(this, FieldText.HelpText(Description, DisplayErrorMessage));
    }

    // ---------------------------------------------------------------- announcements

    private void SetAnnouncement(string text, bool update = true)
    {
        _announcement = text;
        if (update)
        {
            UpdateLive(announce: true);
        }
    }

    /// <summary>Arc's status span: the last announcement, else "Valid United States number" while valid.</summary>
    private void UpdateLive(bool announce)
    {
        var text = !string.IsNullOrEmpty(_announcement)
            ? _announcement
            : Status == PhoneNumberStatus.Valid ? $"Valid {_country.Name} number" : string.Empty;
        if (text == _liveText)
        {
            return;
        }
        _liveText = text;
        if (announce)
        {
            _live?.Announce(text);
        }
        else
        {
            _live?.Announce(string.Empty);
        }
    }

    /// <summary>The text of the live region (what assistive technology heard last).</summary>
    internal string LiveText => _live?.Message ?? string.Empty;

    // ---------------------------------------------------------------- number entry

    /// <summary>Arc's setNumber: caps the digits, keeps the caret after the same digit count, emits when changed.</summary>
    private void SetNumber(string nextDigits, int? caretDigits, PhoneCountry? entry = null, bool user = true)
    {
        entry ??= _country;
        var capped = PhoneNumbers.CapDigits(entry, nextDigits);
        int? caret = caretDigits is { } c ? Math.Min(c, capped.Length) : null;
        if (capped == _digits && ReferenceEquals(entry, _country))
        {
            // A refused character: the text goes back to the formatted number and the caret stays put.
            WriteText(PhoneNumbers.FormatNational(_country, _digits));
            PlaceCaret(caret);
            return;
        }
        Commit(entry, capped, user);
        PlaceCaret(caret);
    }

    private void PlaceCaret(int? digitCount)
    {
        if (digitCount is not { } count || !IsFocused)
        {
            return;
        }
        var position = PhoneNumbers.CaretAfterDigits(Text ?? string.Empty, count);
        SetCurrentValue(SelectionStartProperty, position);
        SetCurrentValue(SelectionEndProperty, position);
        SetCurrentValue(CaretIndexProperty, position);
    }

    /// <summary>Arc's onNumberChange: the edited text, read back into digits (an international number picks its country).</summary>
    private void ApplyEdit(string raw, int caret, bool user = true)
    {
        var trimmedStart = raw.TrimStart();
        if (raw.Contains('+') || (trimmedStart.StartsWith("00", StringComparison.Ordinal) && PhoneNumbers.OnlyDigits(raw).Length > 6))
        {
            var plus = raw.IndexOf('+');
            if (PhoneNumbers.Parse(raw[Math.Max(0, plus)..], _pool) is { National.Length: > 0 } parsed)
            {
                if (!ReferenceEquals(parsed.Country, _country) && user)
                {
                    SetAnnouncement($"Country set to {parsed.Country.Name}", update: false);
                }
                SetNumber(parsed.National, parsed.National.Length, parsed.Country, user);
                return;
            }
        }
        SetNumber(PhoneNumbers.OnlyDigits(raw), PhoneNumbers.CountDigits(raw, caret), user: user);
    }

    private (int Start, int End) Selection()
    {
        var length = Text?.Length ?? 0;
        var a = Math.Clamp(SelectionStart, 0, length);
        var b = Math.Clamp(SelectionEnd, 0, length);
        return a <= b ? (a, b) : (b, a);
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        if (!ReferenceEquals(e.Source, this))
        {
            // Typing on the country trigger never edits the number.
            return;
        }
        if (e.Handled || IsReadOnly || string.IsNullOrEmpty(e.Text))
        {
            base.OnTextInput(e);
            return;
        }
        e.Handled = true;
        if (e.Text == "+")
        {
            // "+" jumps into the picker with a calling-code search.
            OpenPicker("+");
            return;
        }
        var text = Text ?? string.Empty;
        var (start, end) = Selection();
        ApplyEdit(text[..start] + e.Text + text[end..], start + e.Text.Length);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!ReferenceEquals(e.Source, this))
        {
            // Keys pressed on the trigger bubble through the editor; they must not move its caret or edit it.
            return;
        }
        if (!e.Handled && !IsReadOnly && e.Key is Key.Back or Key.Delete &&
            (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) == 0 && HandleDelete(e.Key == Key.Back))
        {
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    /// <summary>Backspace / Delete as Arc reads them: a separator deletes the digit beside it.</summary>
    private bool HandleDelete(bool backward)
    {
        var text = Text ?? string.Empty;
        var (start, end) = Selection();
        if (start != end)
        {
            ApplyEdit(text[..start] + text[end..], start);
            return true;
        }
        if (backward)
        {
            if (start == 0)
            {
                return false;
            }
            if (!PhoneNumbers.IsDigit(text[start - 1]))
            {
                var index = PhoneNumbers.CountDigits(text, start);
                if (index > 0)
                {
                    SetNumber(_digits[..(index - 1)] + _digits[index..], index - 1);
                }
                return true;
            }
            ApplyEdit(text.Remove(start - 1, 1), start - 1);
            return true;
        }
        if (start >= text.Length)
        {
            return false;
        }
        if (!PhoneNumbers.IsDigit(text[start]))
        {
            var index = PhoneNumbers.CountDigits(text, start);
            SetNumber(PhoneNumbers.Slice(_digits, 0, index) + PhoneNumbers.Slice(_digits, index + 1), index);
            return true;
        }
        ApplyEdit(text.Remove(start, 1), start);
        return true;
    }

    /// <summary>Any other edit (cut, undo, word delete, a Text set from outside) is read back once it has landed.</summary>
    private void ScheduleNormalize()
    {
        if (_normalizePending)
        {
            return;
        }
        _normalizePending = true;
        Dispatcher.UIThread.Post(() =>
        {
            _normalizePending = false;
            var text = Text ?? string.Empty;
            if (text == PhoneNumbers.FormatNational(_country, _digits))
            {
                return;
            }
            ApplyEdit(text, Math.Clamp(CaretIndex, 0, text.Length), user: IsKeyboardFocusWithin);
        }, DispatcherPriority.Send);
    }

    private async void OnPastingFromClipboard(object? sender, RoutedEventArgs e)
    {
        if (!ReferenceEquals(e.Source, this))
        {
            return;
        }
        e.Handled = true;
        if (IsReadOnly || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return;
        }
        string? text;
        try
        {
            text = await clipboard.TryGetTextAsync();
        }
        catch (Exception)
        {
            return;
        }
        if (!string.IsNullOrEmpty(text))
        {
            PasteText(text);
        }
    }

    /// <summary>
    /// Arc's paste: "+…" / "00…" picks the country (the longest calling code; +1 by area code); otherwise digits, with a
    /// leading calling code dropped when the number would not fit with it. A whole number replaces the field, a fragment
    /// goes in at the caret.
    /// </summary>
    internal void PasteText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        var lead = text.TrimStart();
        var parsed = lead.StartsWith('+') || lead.StartsWith("00", StringComparison.Ordinal) ? PhoneNumbers.Parse(text, _pool) : null;
        if (parsed is { } result)
        {
            if (!ReferenceEquals(result.Country, _country))
            {
                SetAnnouncement($"Country set to {result.Country.Name}", update: false);
            }
            SetNumber(result.National, result.National.Length, result.Country);
            return;
        }
        var pasted = PhoneNumbers.OnlyDigits(text);
        if (pasted.StartsWith(_country.Dial, StringComparison.Ordinal) && pasted.Length > PhoneNumbers.CapDigits(_country, pasted).Length)
        {
            pasted = pasted[_country.Dial.Length..];
        }
        if (pasted.Length >= _country.MinLength)
        {
            SetNumber(pasted, pasted.Length);
            return;
        }
        var current = Text ?? string.Empty;
        var (start, end) = Selection();
        var a = Math.Min(PhoneNumbers.CountDigits(current, start), _digits.Length);
        var b = Math.Min(PhoneNumbers.CountDigits(current, end), _digits.Length);
        SetNumber(_digits[..a] + pasted + _digits[b..], a + pasted.Length);
    }

    // ---------------------------------------------------------------- focus and pointer routing

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        // Focus on the country trigger bubbles through the editor: only the number's own focus shows the caret.
        if (ReferenceEquals(e.Source, this))
        {
            base.OnGotFocus(e);
        }
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        if (!ReferenceEquals(e.Source, this))
        {
            return;
        }
        base.OnLostFocus(e);
        // Arc validates after blur, and only once digits were entered.
        _touched = _digits.Length > 0;
        UpdateValidation();
    }

    private bool IsInAnchor(object? source) =>
        _anchor is not null && source is Visual visual && (ReferenceEquals(visual, _anchor) || _anchor.IsVisualAncestorOf(visual));

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (IsInAnchor(e.Source))
        {
            return;
        }
        base.OnPointerPressed(e);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (IsInAnchor(e.Source))
        {
            return;
        }
        base.OnPointerMoved(e);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (IsInAnchor(e.Source))
        {
            return;
        }
        base.OnPointerReleased(e);
    }

    protected override void OnTapped(TappedEventArgs e)
    {
        if (IsInAnchor(e.Source))
        {
            return;
        }
        base.OnTapped(e);
    }

    protected override void OnHolding(HoldingRoutedEventArgs e)
    {
        if (IsInAnchor(e.Source))
        {
            return;
        }
        base.OnHolding(e);
    }

    /// <summary>
    /// Tab order as Arc's markup: the country button, then the number. Entering forward lands on the button, entering
    /// backward on the number.
    /// </summary>
    (bool handled, IInputElement? next) ICustomKeyboardNavigation.GetNext(IInputElement element, NavigationDirection direction)
    {
        var trigger = _trigger is { IsEffectivelyEnabled: true, IsEffectivelyVisible: true } ? _trigger : null;
        if (_bypassNavigation || trigger is null || direction is not (NavigationDirection.Next or NavigationDirection.Previous))
        {
            return (false, null);
        }
        var forward = direction == NavigationDirection.Next;
        if (ReferenceEquals(element, this))
        {
            return forward ? (true, null) : (true, trigger);
        }
        if (ReferenceEquals(element, trigger))
        {
            return forward ? (true, this) : (true, PreviousOutside());
        }
        if (element is Visual visual && this.IsVisualAncestorOf(visual))
        {
            return (false, null);
        }
        return forward ? (true, trigger) : (true, this);
    }

    private bool _bypassNavigation;

    /// <summary>The element before the field in tab order (default navigation from the field itself).</summary>
    private IInputElement? PreviousOutside()
    {
        _bypassNavigation = true;
        try
        {
            return TopLevel.GetTopLevel(this)?.FocusManager?.FindNextElement(NavigationDirection.Previous,
                new FindNextElementOptions { FocusedElement = this });
        }
        finally
        {
            _bypassNavigation = false;
        }
    }

    /// <summary>Focuses the number with the caret at the end.</summary>
    private void FocusNumber()
    {
        Focus(NavigationMethod.Unspecified);
        var end = Text?.Length ?? 0;
        SetCurrentValue(SelectionStartProperty, end);
        SetCurrentValue(SelectionEndProperty, end);
        SetCurrentValue(CaretIndexProperty, end);
    }
}
