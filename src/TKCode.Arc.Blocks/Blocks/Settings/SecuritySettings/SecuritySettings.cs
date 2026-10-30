using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The account security page: a checkup of four safeguards (password, two-step verification, passkeys, recovery
/// codes), the password with a change form that opens in place, the two-step methods, and every signed-in session with
/// a way to sign it out. Each action happens on the page; the host hears about it through routed events.
/// </summary>
[TemplatePart(SummaryGroupPart, typeof(SettingsGroup))]
[TemplatePart(PasswordRowPart, typeof(SettingsRow))]
[TemplatePart(ChangePasswordPart, typeof(Button))]
[TemplatePart(PasswordFormHostPart, typeof(SpringSizeDecorator))]
[TemplatePart(PasswordFormPart, typeof(Control))]
[TemplatePart(CurrentPasswordPart, typeof(PasswordField))]
[TemplatePart(NewPasswordPart, typeof(PasswordStrength))]
[TemplatePart(CancelPasswordPart, typeof(Button))]
[TemplatePart(UpdatePasswordPart, typeof(Button))]
[TemplatePart(TwoStepGroupPart, typeof(SettingsGroup))]
[TemplatePart(AuthenticatorSwitchPart, typeof(ToggleButton))]
[TemplatePart(TextMessageSwitchPart, typeof(ToggleButton))]
[TemplatePart(AddPasskeyPart, typeof(Button))]
[TemplatePart(RecoveryCodesPart, typeof(Button))]
[TemplatePart(SessionsGroupPart, typeof(SettingsGroup))]
[PseudoClasses(CompactPseudoClass, RowsStackedPseudoClass, PasswordOpenPseudoClass, ToneSuccessPseudoClass, ToneAccentPseudoClass,
    ToneWarningPseudoClass, PasswordOnPseudoClass, TwoStepOnPseudoClass, PasskeyOnPseudoClass, RecoveryOnPseudoClass)]
public class SecuritySettings : TemplatedControl
{
    public const string SummaryGroupPart = "PART_SummaryGroup";
    public const string PasswordRowPart = "PART_PasswordRow";
    public const string ChangePasswordPart = "PART_ChangePassword";
    public const string PasswordFormHostPart = "PART_PasswordFormHost";
    public const string PasswordFormPart = "PART_PasswordForm";
    public const string CurrentPasswordPart = "PART_CurrentPassword";
    public const string NewPasswordPart = "PART_NewPassword";
    public const string CancelPasswordPart = "PART_CancelPassword";
    public const string UpdatePasswordPart = "PART_UpdatePassword";
    public const string TwoStepGroupPart = "PART_TwoStepGroup";
    public const string AuthenticatorSwitchPart = "PART_AuthenticatorSwitch";
    public const string TextMessageSwitchPart = "PART_TextMessageSwitch";
    public const string AddPasskeyPart = "PART_AddPasskey";
    public const string RecoveryCodesPart = "PART_RecoveryCodes";
    public const string SessionsGroupPart = "PART_SessionsGroup";

    public const string CompactPseudoClass = ":compact";
    public const string RowsStackedPseudoClass = ":rows-stacked";
    public const string PasswordOpenPseudoClass = ":password-open";
    public const string ToneSuccessPseudoClass = ":tone-success";
    public const string ToneAccentPseudoClass = ":tone-accent";
    public const string ToneWarningPseudoClass = ":tone-warning";
    public const string PasswordOnPseudoClass = ":password-on";
    public const string TwoStepOnPseudoClass = ":two-step-on";
    public const string PasskeyOnPseudoClass = ":passkey-on";
    public const string RecoveryOnPseudoClass = ":recovery-on";

    /// <summary>The new password must reach this <see cref="PasswordStrength.Level"/> ("Fair") before it can be saved.</summary>
    public const int MinimumPasswordLevel = 2;

    /// <summary>Delay between rows when every other session signs out at once.</summary>
    private static readonly TimeSpan SignOutStagger = TimeSpan.FromMilliseconds(35);

    #region Properties

    /// <summary>
    /// When the password was last changed; null reads "Never changed". Set to the clock's time after a successful
    /// change (two-way).
    /// </summary>
    public static readonly StyledProperty<DateTimeOffset?> PasswordChangedAtProperty =
        AvaloniaProperty.Register<SecuritySettings, DateTimeOffset?>(nameof(PasswordChangedAt), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Whether the account has a password (default true). Without one the row offers "Set password" and the form asks
    /// only for the new password; a successful change sets it to true (two-way).
    /// </summary>
    public static readonly StyledProperty<bool> HasPasswordProperty =
        AvaloniaProperty.Register<SecuritySettings, bool>(nameof(HasPassword), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether sign-ins ask for a code from an authenticator app (two-way, follows its switch).</summary>
    public static readonly StyledProperty<bool> IsAuthenticatorEnabledProperty =
        AvaloniaProperty.Register<SecuritySettings, bool>(nameof(IsAuthenticatorEnabled), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether sign-ins send a code by text message (two-way, follows its switch).</summary>
    public static readonly StyledProperty<bool> IsTextMessageEnabledProperty =
        AvaloniaProperty.Register<SecuritySettings, bool>(nameof(IsTextMessageEnabled), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// The phone number for text messages; only its last four digits are shown ("•••• 4821"). Without one the text
    /// message switch is disabled.
    /// </summary>
    public static readonly StyledProperty<string?> PhoneNumberProperty =
        AvaloniaProperty.Register<SecuritySettings, string?>(nameof(PhoneNumber));

    /// <summary>How many passkeys the account has.</summary>
    public static readonly StyledProperty<int> PasskeyCountProperty =
        AvaloniaProperty.Register<SecuritySettings, int>(nameof(PasskeyCount));

    /// <summary>When the recovery codes were created; null means there are none.</summary>
    public static readonly StyledProperty<DateTimeOffset?> RecoveryCodesCreatedAtProperty =
        AvaloniaProperty.Register<SecuritySettings, DateTimeOffset?>(nameof(RecoveryCodesCreatedAt));

    /// <summary>
    /// The signed-in sessions, listed current first, then by most recent activity. A signed-out session leaves its row
    /// and is removed from this list when the list can be changed. Defaults to an empty observable list.
    /// </summary>
    public static readonly DirectProperty<SecuritySettings, IList<SecuritySession>> SessionsProperty =
        AvaloniaProperty.RegisterDirect<SecuritySettings, IList<SecuritySession>>(nameof(Sessions), o => o.Sessions, (o, v) => o.Sessions = v);

    /// <summary>Whether the security checkup card is shown at the top (default true).</summary>
    public static readonly StyledProperty<bool> IsSummaryVisibleProperty =
        AvaloniaProperty.Register<SecuritySettings, bool>(nameof(IsSummaryVisible), true);

    /// <summary>Which cards are shown (default all): show only the sessions, or only the password, on a page of your own.</summary>
    public static readonly StyledProperty<SecuritySettingsSections> SectionsProperty =
        AvaloniaProperty.Register<SecuritySettings, SecuritySettingsSections>(nameof(Sections), SecuritySettingsSections.All);

    /// <summary>
    /// How many of the four safeguards are on (0–4): a password, two-step verification by either method, at least one
    /// passkey, and recovery codes.
    /// </summary>
    public static readonly DirectProperty<SecuritySettings, int> ProtectionScoreProperty =
        AvaloniaProperty.RegisterDirect<SecuritySettings, int>(nameof(ProtectionScore), o => o.ProtectionScore);

    /// <summary>Whether the password form is open.</summary>
    public static readonly DirectProperty<SecuritySettings, bool> IsPasswordFormOpenProperty =
        AvaloniaProperty.RegisterDirect<SecuritySettings, bool>(nameof(IsPasswordFormOpen), o => o.IsPasswordFormOpen);

    /// <summary>Every built-in text and the culture for dates; replace the whole object to localize.</summary>
    public static readonly StyledProperty<SecuritySettingsLabels> LabelsProperty =
        AvaloniaProperty.Register<SecuritySettings, SecuritySettingsLabels>(nameof(Labels), new SecuritySettingsLabels(),
            coerce: static (_, value) => value ?? new SecuritySettingsLabels());

    /// <summary>
    /// Below this width the meter moves under the summary text and "Sign out other sessions" moves under the list
    /// (default 560).
    /// </summary>
    public static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<SecuritySettings, double>(nameof(CompactWidth), 560);

    /// <summary>Stack width of the session rows (from the token): their badge or "Sign out" stays beside the text longer.</summary>
    internal static readonly StyledProperty<double> SessionRowStackWidthProperty =
        AvaloniaProperty.Register<SecuritySettings, double>(nameof(SessionRowStackWidth), 280);

    /// <summary>Raised when the password form is submitted. Set <see cref="SecurityPasswordChangeEventArgs.ErrorMessage"/> to reject it.</summary>
    public static readonly RoutedEvent<SecurityPasswordChangeEventArgs> PasswordChangeRequestedEvent =
        RoutedEvent.Register<SecuritySettings, SecurityPasswordChangeEventArgs>(nameof(PasswordChangeRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when "Add passkey" is pressed; the host runs the passkey ceremony and updates <see cref="PasskeyCount"/>.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> AddPasskeyRequestedEvent =
        RoutedEvent.Register<SecuritySettings, RoutedEventArgs>(nameof(AddPasskeyRequested), RoutingStrategies.Bubble);

    /// <summary>
    /// Raised when "View codes" or "Create codes" is pressed; the host shows or creates the codes and updates
    /// <see cref="RecoveryCodesCreatedAt"/>.
    /// </summary>
    public static readonly RoutedEvent<RoutedEventArgs> RecoveryCodesRequestedEvent =
        RoutedEvent.Register<SecuritySettings, RoutedEventArgs>(nameof(RecoveryCodesRequested), RoutingStrategies.Bubble);

    /// <summary>Raised after a session's "Sign out" row has left and the session was taken off <see cref="Sessions"/>.</summary>
    public static readonly RoutedEvent<SecuritySessionEventArgs> SessionSignedOutEvent =
        RoutedEvent.Register<SecuritySettings, SecuritySessionEventArgs>(nameof(SessionSignedOut), RoutingStrategies.Bubble);

    /// <summary>Raised after "Sign out other sessions" was confirmed and every other session's row has left.</summary>
    public static readonly RoutedEvent<SecuritySessionsEventArgs> OtherSessionsSignedOutEvent =
        RoutedEvent.Register<SecuritySettings, SecuritySessionsEventArgs>(nameof(OtherSessionsSignedOut), RoutingStrategies.Bubble);

    /// <inheritdoc cref="PasswordChangedAtProperty"/>
    public DateTimeOffset? PasswordChangedAt
    {
        get => GetValue(PasswordChangedAtProperty);
        set => SetValue(PasswordChangedAtProperty, value);
    }

    /// <inheritdoc cref="HasPasswordProperty"/>
    public bool HasPassword
    {
        get => GetValue(HasPasswordProperty);
        set => SetValue(HasPasswordProperty, value);
    }

    /// <inheritdoc cref="IsAuthenticatorEnabledProperty"/>
    public bool IsAuthenticatorEnabled
    {
        get => GetValue(IsAuthenticatorEnabledProperty);
        set => SetValue(IsAuthenticatorEnabledProperty, value);
    }

    /// <inheritdoc cref="IsTextMessageEnabledProperty"/>
    public bool IsTextMessageEnabled
    {
        get => GetValue(IsTextMessageEnabledProperty);
        set => SetValue(IsTextMessageEnabledProperty, value);
    }

    /// <inheritdoc cref="PhoneNumberProperty"/>
    public string? PhoneNumber
    {
        get => GetValue(PhoneNumberProperty);
        set => SetValue(PhoneNumberProperty, value);
    }

    /// <inheritdoc cref="PasskeyCountProperty"/>
    public int PasskeyCount
    {
        get => GetValue(PasskeyCountProperty);
        set => SetValue(PasskeyCountProperty, value);
    }

    /// <inheritdoc cref="RecoveryCodesCreatedAtProperty"/>
    public DateTimeOffset? RecoveryCodesCreatedAt
    {
        get => GetValue(RecoveryCodesCreatedAtProperty);
        set => SetValue(RecoveryCodesCreatedAtProperty, value);
    }

    private IList<SecuritySession> _sessions = new AvaloniaList<SecuritySession>();
    private bool _ownsSessions = true;
    private INotifyCollectionChanged? _watchedSessions;
    // A session list the host hands in can outlive the page (a view model's): it is followed only while the page is in a
    // visual tree, and read again when the page comes back. The page's own list is always followed.
    private bool _watching;

    /// <inheritdoc cref="SessionsProperty"/>
    public IList<SecuritySession> Sessions
    {
        get => _sessions;
        set
        {
            var owns = value is null;
            value ??= new AvaloniaList<SecuritySession>();
            if (SetAndRaise(SessionsProperty, ref _sessions, value))
            {
                _ownsSessions = owns;
                WatchSessions();
                _gone.Clear();
                SyncSessions();
            }
        }
    }

    /// <inheritdoc cref="IsSummaryVisibleProperty"/>
    public bool IsSummaryVisible
    {
        get => GetValue(IsSummaryVisibleProperty);
        set => SetValue(IsSummaryVisibleProperty, value);
    }

    /// <inheritdoc cref="SectionsProperty"/>
    public SecuritySettingsSections Sections
    {
        get => GetValue(SectionsProperty);
        set => SetValue(SectionsProperty, value);
    }

    private int _protectionScore;

    /// <inheritdoc cref="ProtectionScoreProperty"/>
    public int ProtectionScore
    {
        get => _protectionScore;
        private set => SetAndRaise(ProtectionScoreProperty, ref _protectionScore, value);
    }

    private bool _isPasswordFormOpen;

    /// <inheritdoc cref="IsPasswordFormOpenProperty"/>
    public bool IsPasswordFormOpen
    {
        get => _isPasswordFormOpen;
        private set
        {
            if (SetAndRaise(IsPasswordFormOpenProperty, ref _isPasswordFormOpen, value))
            {
                PseudoClasses.Set(PasswordOpenPseudoClass, value);
            }
        }
    }

    /// <inheritdoc cref="LabelsProperty"/>
    public SecuritySettingsLabels Labels
    {
        get => GetValue(LabelsProperty);
        set => SetValue(LabelsProperty, value);
    }

    /// <inheritdoc cref="CompactWidthProperty"/>
    public double CompactWidth
    {
        get => GetValue(CompactWidthProperty);
        set => SetValue(CompactWidthProperty, value);
    }

    internal double SessionRowStackWidth
    {
        get => GetValue(SessionRowStackWidthProperty);
        set => SetValue(SessionRowStackWidthProperty, value);
    }

    private TimeProvider _clock = TimeProvider.System;

    /// <summary>
    /// Where "now" comes from for relative times ("2 hours ago") and the time stamped on a password change. Defaults to
    /// <see cref="TimeProvider.System"/>; tests pass a fixed clock.
    /// </summary>
    public TimeProvider Clock
    {
        get => _clock;
        set
        {
            _clock = value ?? TimeProvider.System;
            RefreshTimes();
        }
    }

    /// <inheritdoc cref="PasswordChangeRequestedEvent"/>
    public event EventHandler<SecurityPasswordChangeEventArgs>? PasswordChangeRequested
    {
        add => AddHandler(PasswordChangeRequestedEvent, value);
        remove => RemoveHandler(PasswordChangeRequestedEvent, value);
    }

    /// <inheritdoc cref="AddPasskeyRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? AddPasskeyRequested
    {
        add => AddHandler(AddPasskeyRequestedEvent, value);
        remove => RemoveHandler(AddPasskeyRequestedEvent, value);
    }

    /// <inheritdoc cref="RecoveryCodesRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? RecoveryCodesRequested
    {
        add => AddHandler(RecoveryCodesRequestedEvent, value);
        remove => RemoveHandler(RecoveryCodesRequestedEvent, value);
    }

    /// <inheritdoc cref="SessionSignedOutEvent"/>
    public event EventHandler<SecuritySessionEventArgs>? SessionSignedOut
    {
        add => AddHandler(SessionSignedOutEvent, value);
        remove => RemoveHandler(SessionSignedOutEvent, value);
    }

    /// <inheritdoc cref="OtherSessionsSignedOutEvent"/>
    public event EventHandler<SecuritySessionsEventArgs>? OtherSessionsSignedOut
    {
        add => AddHandler(OtherSessionsSignedOutEvent, value);
        remove => RemoveHandler(OtherSessionsSignedOutEvent, value);
    }

    #endregion

    // Template parts.
    private SettingsGroup? _summaryGroup;
    private ArcContentSwap? _summaryTitle;
    private ArcContentSwap? _summaryCount;
    private ArcGlyph? _shieldGlyph;
    private Control? _meter;
    private SettingsGroup? _passwordGroup;
    private SettingsRow? _passwordRow;
    private Button? _changePassword;
    private SpringSizeDecorator? _formHost;
    private Control? _form;
    private PasswordField? _currentPassword;
    private PasswordStrength? _newPassword;
    private Button? _cancelPassword;
    private Button? _updatePassword;
    private SettingsGroup? _twoStepGroup;
    private SettingsRow? _authenticatorRow;
    private ToggleButton? _authenticatorSwitch;
    private SettingsRow? _textMessageRow;
    private ToggleButton? _textMessageSwitch;
    private SettingsRow? _passkeysRow;
    private Button? _addPasskey;
    private SettingsRow? _recoveryRow;
    private Button? _recoveryCodes;
    private SettingsGroup? _sessionsGroup;

    // Parts that belong to the control, not the template: they keep their state across re-templating.
    private readonly ConfirmMorph _signOutOthers;
    private readonly SettingsRow _emptyNote;
    private readonly Dictionary<SecuritySession, SecuritySettingsSessionView> _views = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<SecuritySession> _gone = new(ReferenceEqualityComparer.Instance);
    private readonly List<SecuritySettingsSessionView> _ordered = new();
    private readonly DispatcherTimer _refresh;
    private bool _isCompact;
    private bool _syncingSwitches;

    public SecuritySettings()
    {
        BlockFocusModality.Watch();
        _signOutOthers = new ConfirmMorph
        {
            Tone = ConfirmMorphTone.Danger,
            ConfirmMode = ConfirmMorphMode.Press,
            Icon = ArcIcons.LogOut,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };
        _signOutOthers.ConfirmAction = () =>
        {
            SignOutOtherSessions();
            return Task.CompletedTask;
        };
        _signOutOthers.StateChanged += (_, _) => UpdateSignOutOthers();
        // A row with only a description: the sessions card keeps its rhythm when the list is empty.
        _emptyNote = new SettingsRow();
        _refresh = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _refresh.Tick += (_, _) => RefreshTimes();
        WatchSessions();
        UpdateScore();
        UpdateTexts();
    }

    protected override Type StyleKeyOverride => typeof(SecuritySettings);

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        Unhook();
        base.OnApplyTemplate(e);
        var scope = e.NameScope;
        _summaryGroup = scope.Find<SettingsGroup>(SummaryGroupPart);
        _summaryTitle = scope.Find<ArcContentSwap>("PART_SummaryTitle");
        _summaryCount = scope.Find<ArcContentSwap>("PART_SummaryCount");
        _shieldGlyph = scope.Find<ArcGlyph>("PART_ShieldGlyph");
        _meter = scope.Find<Control>("PART_Meter");
        _passwordGroup = scope.Find<SettingsGroup>("PART_PasswordGroup");
        _passwordRow = scope.Find<SettingsRow>(PasswordRowPart);
        _changePassword = scope.Find<Button>(ChangePasswordPart);
        _formHost = scope.Find<SpringSizeDecorator>(PasswordFormHostPart);
        _form = scope.Find<Control>(PasswordFormPart);
        _currentPassword = scope.Find<PasswordField>(CurrentPasswordPart);
        _newPassword = scope.Find<PasswordStrength>(NewPasswordPart);
        _cancelPassword = scope.Find<Button>(CancelPasswordPart);
        _updatePassword = scope.Find<Button>(UpdatePasswordPart);
        _twoStepGroup = scope.Find<SettingsGroup>(TwoStepGroupPart);
        _authenticatorRow = scope.Find<SettingsRow>("PART_AuthenticatorRow");
        _authenticatorSwitch = scope.Find<ToggleButton>(AuthenticatorSwitchPart);
        _textMessageRow = scope.Find<SettingsRow>("PART_TextMessageRow");
        _textMessageSwitch = scope.Find<ToggleButton>(TextMessageSwitchPart);
        _passkeysRow = scope.Find<SettingsRow>("PART_PasskeysRow");
        _addPasskey = scope.Find<Button>(AddPasskeyPart);
        _recoveryRow = scope.Find<SettingsRow>("PART_RecoveryRow");
        _recoveryCodes = scope.Find<Button>(RecoveryCodesPart);
        _sessionsGroup = scope.Find<SettingsGroup>(SessionsGroupPart);
        Hook();

        if (_form is not null)
        {
            _form.IsVisible = IsPasswordFormOpen;
        }
        if (_formHost is not null)
        {
            _formHost.IsExpanded = IsPasswordFormOpen;
        }
        if (_changePassword is not null)
        {
            _changePassword.IsEnabled = !IsPasswordFormOpen;
        }
        SyncSwitches();
        UpdateSections();
        UpdateTexts();
        UpdateScore(force: true);
        PlaceSignOutOthers();
        SyncSessions();
    }

    private void Hook()
    {
        if (_changePassword is not null)
        {
            _changePassword.Click += OnChangePasswordClick;
        }
        if (_cancelPassword is not null)
        {
            _cancelPassword.Click += OnCancelPasswordClick;
        }
        if (_updatePassword is not null)
        {
            _updatePassword.Click += OnUpdatePasswordClick;
        }
        if (_currentPassword is not null)
        {
            _currentPassword.PropertyChanged += OnPasswordFieldChanged;
        }
        if (_newPassword is not null)
        {
            _newPassword.PropertyChanged += OnPasswordFieldChanged;
        }
        if (_form is not null)
        {
            _form.KeyDown += OnFormKeyDown;
        }
        if (_formHost is not null)
        {
            _formHost.PropertyChanged += OnFormHostChanged;
        }
        if (_passwordRow is not null)
        {
            _passwordRow.PropertyChanged += OnPasswordRowChanged;
        }
        if (_authenticatorSwitch is not null)
        {
            _authenticatorSwitch.IsCheckedChanged += OnSwitchChanged;
        }
        if (_textMessageSwitch is not null)
        {
            _textMessageSwitch.IsCheckedChanged += OnSwitchChanged;
        }
        if (_addPasskey is not null)
        {
            _addPasskey.Click += OnAddPasskeyClick;
        }
        if (_recoveryCodes is not null)
        {
            _recoveryCodes.Click += OnRecoveryCodesClick;
        }
    }

    private void Unhook()
    {
        if (_changePassword is not null)
        {
            _changePassword.Click -= OnChangePasswordClick;
        }
        if (_cancelPassword is not null)
        {
            _cancelPassword.Click -= OnCancelPasswordClick;
        }
        if (_updatePassword is not null)
        {
            _updatePassword.Click -= OnUpdatePasswordClick;
        }
        if (_currentPassword is not null)
        {
            _currentPassword.PropertyChanged -= OnPasswordFieldChanged;
        }
        if (_newPassword is not null)
        {
            _newPassword.PropertyChanged -= OnPasswordFieldChanged;
        }
        if (_form is not null)
        {
            _form.KeyDown -= OnFormKeyDown;
        }
        if (_formHost is not null)
        {
            _formHost.PropertyChanged -= OnFormHostChanged;
        }
        if (_passwordRow is not null)
        {
            _passwordRow.PropertyChanged -= OnPasswordRowChanged;
        }
        if (_authenticatorSwitch is not null)
        {
            _authenticatorSwitch.IsCheckedChanged -= OnSwitchChanged;
        }
        if (_textMessageSwitch is not null)
        {
            _textMessageSwitch.IsCheckedChanged -= OnSwitchChanged;
        }
        if (_addPasskey is not null)
        {
            _addPasskey.Click -= OnAddPasskeyClick;
        }
        if (_recoveryCodes is not null)
        {
            _recoveryCodes.Click -= OnRecoveryCodesClick;
        }
        if (_sessionsGroup is not null)
        {
            _sessionsGroup.Items.Clear();
            _sessionsGroup.HeaderActions = null;
            _sessionsGroup.Footer = null;
        }
    }

    #endregion

    #region Lifecycle and layout

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _watching = true;
        WatchSessions();
        if (!_ownsSessions)
        {
            // The host's list may have changed while the page was away.
            SyncSessions();
        }
        RefreshTimes();
        _refresh.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _watching = false;
        WatchSessions();
        _refresh.Stop();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // The width comes from the parent, not from the content, so switching layouts here cannot oscillate.
        if (!double.IsInfinity(availableSize.Width))
        {
            var compact = availableSize.Width < CompactWidth;
            if (compact != _isCompact)
            {
                _isCompact = compact;
                PseudoClasses.Set(CompactPseudoClass, compact);
                PlaceSignOutOthers();
                UpdateSignOutOthers();
            }
        }
        return base.MeasureOverride(availableSize);
    }

    /// <summary>"Sign out other sessions" sits beside the sessions header, or under the list when the page is narrow.</summary>
    private void PlaceSignOutOthers()
    {
        if (_sessionsGroup is null)
        {
            return;
        }
        if (_isCompact)
        {
            if (!ReferenceEquals(_sessionsGroup.Footer, _signOutOthers))
            {
                _sessionsGroup.HeaderActions = null;
                _sessionsGroup.Footer = _signOutOthers;
            }
        }
        else if (!ReferenceEquals(_sessionsGroup.HeaderActions, _signOutOthers))
        {
            _sessionsGroup.Footer = null;
            _sessionsGroup.HeaderActions = _signOutOthers;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == PasswordChangedAtProperty)
        {
            UpdatePasswordTexts();
            UpdateScore();
        }
        else if (property == HasPasswordProperty)
        {
            UpdatePasswordTexts();
            UpdateCanSubmit();
            UpdateScore();
        }
        else if (property == IsAuthenticatorEnabledProperty || property == IsTextMessageEnabledProperty)
        {
            SyncSwitches();
            UpdateScore();
        }
        else if (property == PhoneNumberProperty)
        {
            UpdateTwoStepTexts();
        }
        else if (property == PasskeyCountProperty || property == RecoveryCodesCreatedAtProperty)
        {
            UpdateTwoStepTexts();
            UpdateScore();
        }
        else if (property == LabelsProperty)
        {
            UpdateTexts();
            UpdateScore(force: true);
        }
        else if (property == CompactWidthProperty)
        {
            InvalidateMeasure();
        }
        else if (property == SessionRowStackWidthProperty)
        {
            foreach (var view in _views.Values)
            {
                view.Row.StackWidth = SessionRowStackWidth;
            }
        }
        else if (property == SectionsProperty || property == IsSummaryVisibleProperty)
        {
            UpdateSections();
        }
    }

    private void UpdateSections()
    {
        var sections = Sections;
        if (_summaryGroup is not null)
        {
            _summaryGroup.IsVisible = IsSummaryVisible && sections.HasFlag(SecuritySettingsSections.Summary);
        }
        if (_passwordGroup is not null)
        {
            _passwordGroup.IsVisible = sections.HasFlag(SecuritySettingsSections.Password);
        }
        if (_twoStepGroup is not null)
        {
            _twoStepGroup.IsVisible = sections.HasFlag(SecuritySettingsSections.TwoStep);
        }
        if (_sessionsGroup is not null)
        {
            _sessionsGroup.IsVisible = sections.HasFlag(SecuritySettingsSections.Sessions);
        }
    }

    #endregion

    #region Texts

    private void UpdateTexts()
    {
        var labels = Labels;
        _signOutOthers.Label = labels.SignOutOthers;
        _signOutOthers.ConfirmLabel = labels.SignOutConfirm;
        _signOutOthers.CancelLabel = labels.Cancel;
        _signOutOthers.PendingLabel = labels.SigningOut;
        _signOutOthers.DoneLabel = labels.SignedOut;
        _emptyNote.Description = labels.NoSessions;
        if (_passwordRow is not null)
        {
            _passwordRow.Header = labels.PasswordHeader;
        }
        if (_currentPassword is not null)
        {
            _currentPassword.Label = labels.CurrentPassword;
        }
        if (_newPassword is not null)
        {
            _newPassword.Label = labels.NewPassword;
        }
        if (_cancelPassword is not null)
        {
            _cancelPassword.Content = labels.Cancel;
        }
        if (_twoStepGroup is not null)
        {
            _twoStepGroup.Header = labels.TwoStepHeader;
            _twoStepGroup.Description = labels.TwoStepDescription;
        }
        if (_authenticatorRow is not null)
        {
            _authenticatorRow.Header = labels.AuthenticatorHeader;
            _authenticatorRow.Description = labels.AuthenticatorDescription;
        }
        if (_textMessageRow is not null)
        {
            _textMessageRow.Header = labels.TextMessageHeader;
        }
        if (_passkeysRow is not null)
        {
            _passkeysRow.Header = labels.PasskeysHeader;
        }
        if (_addPasskey is not null)
        {
            _addPasskey.Content = labels.AddPasskey;
        }
        if (_recoveryRow is not null)
        {
            _recoveryRow.Header = labels.RecoveryCodesHeader;
        }
        if (_sessionsGroup is not null)
        {
            _sessionsGroup.Header = labels.SessionsHeader;
        }
        UpdatePasswordTexts();
        UpdateTwoStepTexts();
        UpdateSessionTexts();
    }

    private void UpdatePasswordTexts()
    {
        var labels = Labels;
        var hasPassword = HasPassword;
        if (_passwordRow is not null)
        {
            _passwordRow.Description = PasswordDescription(labels);
        }
        if (_changePassword is not null)
        {
            _changePassword.Content = hasPassword ? labels.ChangePassword : labels.SetPassword;
        }
        if (_updatePassword is not null)
        {
            _updatePassword.Content = hasPassword ? labels.UpdatePassword : labels.SetPassword;
        }
        if (_currentPassword is not null)
        {
            _currentPassword.IsVisible = hasPassword;
        }
    }

    private string PasswordDescription(SecuritySettingsLabels labels)
    {
        if (!HasPassword)
        {
            return labels.PasswordNotSet;
        }
        if (PasswordChangedAt is not { } changed)
        {
            return labels.PasswordNeverChanged;
        }
        var now = _clock.GetUtcNow();
        var elapsed = now - changed;
        return elapsed < TimeSpan.FromMinutes(1)
            ? labels.PasswordUpdatedNow
            : labels.PasswordChanged(SettingsFormat.Relative(changed, now, labels.Culture));
    }

    private void UpdateTwoStepTexts()
    {
        var labels = Labels;
        var phone = MaskPhone(PhoneNumber);
        if (_textMessageRow is not null)
        {
            _textMessageRow.Description = phone is null ? labels.TextMessageNoPhone : labels.TextMessageDescription(phone);
        }
        if (_textMessageSwitch is not null)
        {
            _textMessageSwitch.IsEnabled = phone is not null;
        }
        if (_passkeysRow is not null)
        {
            _passkeysRow.Description = PasskeyCount > 0 ? labels.PasskeyCount(PasskeyCount) : labels.NoPasskeys;
        }
        var codes = RecoveryCodesCreatedAt;
        if (_recoveryRow is not null)
        {
            _recoveryRow.Description = codes is { } created
                ? labels.RecoveryCodesCreated(SettingsFormat.Date(created.LocalDateTime.Date, labels.Culture))
                : labels.RecoveryCodesMissing;
        }
        if (_recoveryCodes is not null)
        {
            _recoveryCodes.Content = codes is null ? labels.CreateCodes : labels.ViewCodes;
        }
    }

    /// <summary>"•••• 4821" from the last four digits, or null when the number has no digits.</summary>
    internal static string? MaskPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }
        Span<char> last = stackalloc char[4];
        var found = 0;
        for (var i = phone.Length - 1; i >= 0 && found < 4; i--)
        {
            if (char.IsAsciiDigit(phone[i]))
            {
                last[3 - found] = phone[i];
                found++;
            }
        }
        return found == 0 ? null : $"•••• {new string(last[(4 - found)..])}";
    }

    /// <summary>Re-reads "now": the password row and the sessions' activity times.</summary>
    private void RefreshTimes()
    {
        UpdatePasswordTexts();
        UpdateSessionTexts();
    }

    #endregion

    #region Summary

    private void UpdateScore(bool force = false)
    {
        var password = HasPassword;
        var twoStep = IsAuthenticatorEnabled || IsTextMessageEnabled;
        var passkey = PasskeyCount > 0;
        var recovery = RecoveryCodesCreatedAt is not null;
        var score = (password ? 1 : 0) + (twoStep ? 1 : 0) + (passkey ? 1 : 0) + (recovery ? 1 : 0);
        PseudoClasses.Set(PasswordOnPseudoClass, password);
        PseudoClasses.Set(TwoStepOnPseudoClass, twoStep);
        PseudoClasses.Set(PasskeyOnPseudoClass, passkey);
        PseudoClasses.Set(RecoveryOnPseudoClass, recovery);
        PseudoClasses.Set(ToneSuccessPseudoClass, score == 4);
        PseudoClasses.Set(ToneAccentPseudoClass, score is 2 or 3);
        PseudoClasses.Set(ToneWarningPseudoClass, score < 2);
        var previous = ProtectionScore;
        ProtectionScore = score;
        if (previous == score && !force)
        {
            return;
        }
        var labels = Labels;
        var count = labels.SummaryCount(score);
        if (_summaryTitle is not null)
        {
            _summaryTitle.Content = score == 4 ? labels.SummaryProtected : labels.SummaryImprove;
        }
        if (_summaryCount is not null)
        {
            _summaryCount.Direction = score >= previous ? 1 : -1;
            _summaryCount.Content = count;
        }
        if (_meter is not null)
        {
            AutomationProperties.SetName(_meter, count);
        }
        if (_shieldGlyph is not null)
        {
            _shieldGlyph.Data = score >= 2 ? SecuritySettingsGlyphs.ShieldCheck : SecuritySettingsGlyphs.ShieldAlert;
        }
    }

    #endregion

    #region Password

    /// <summary>Opens the password form under the password row and focuses its first field.</summary>
    public void OpenPasswordForm()
    {
        if (IsPasswordFormOpen)
        {
            FocusFirstField();
            return;
        }
        ClearForm();
        IsPasswordFormOpen = true;
        if (_form is not null)
        {
            _form.IsVisible = true;
        }
        if (_formHost is not null)
        {
            _formHost.IsExpanded = true;
        }
        if (_changePassword is not null)
        {
            _changePassword.IsEnabled = false;
        }
        UpdateCanSubmit();
        FocusFirstField();
    }

    /// <summary>Closes the password form without saving; focus returns to "Change password" if it was in the form.</summary>
    public void ClosePasswordForm()
    {
        if (!IsPasswordFormOpen)
        {
            return;
        }
        var focusWasInside = IsFocusWithin(_form);
        IsPasswordFormOpen = false;
        if (_formHost is not null)
        {
            _formHost.IsExpanded = false;
        }
        if (_changePassword is not null)
        {
            _changePassword.IsEnabled = true;
            if (focusWasInside)
            {
                // Cancel clicked with the mouse hands focus back without the keyboard ring.
                _changePassword.Focus(BlockFocusModality.For(this));
            }
        }
        // Without a spring the decorator is already closed; otherwise the form hides once the height reaches zero.
        if (_formHost is null || !_formHost.IsLoaded || !ArcMotion.IsFull(_formHost))
        {
            HideForm();
        }
    }

    /// <summary>Submits the form, as "Update password" or Enter would. Does nothing while the form cannot be submitted.</summary>
    public void SubmitPasswordChange()
    {
        if (!IsPasswordFormOpen || !CanSubmit())
        {
            return;
        }
        var hasPassword = HasPassword;
        var args = new SecurityPasswordChangeEventArgs(PasswordChangeRequestedEvent,
            hasPassword ? _currentPassword?.Text ?? string.Empty : string.Empty,
            _newPassword?.Text ?? string.Empty);
        RaiseEvent(args);
        if (!string.IsNullOrEmpty(args.ErrorMessage))
        {
            FieldTextBox? field = hasPassword ? _currentPassword : _newPassword;
            if (field is not null)
            {
                field.ErrorMessage = args.ErrorMessage;
                field.Focus(NavigationMethod.Tab);
                field.SelectAll();
            }
            UpdateCanSubmit();
            return;
        }
        SetCurrentValue(HasPasswordProperty, true);
        SetCurrentValue(PasswordChangedAtProperty, _clock.GetUtcNow());
        ClosePasswordForm();
    }

    private bool CanSubmit()
    {
        if (_newPassword is null)
        {
            return false;
        }
        var currentOk = !HasPassword || !string.IsNullOrEmpty(_currentPassword?.Text);
        return currentOk && _newPassword.Level >= MinimumPasswordLevel;
    }

    private void UpdateCanSubmit()
    {
        if (_updatePassword is not null)
        {
            _updatePassword.IsEnabled = IsPasswordFormOpen && CanSubmit();
        }
    }

    private void FocusFirstField()
    {
        FieldTextBox? field = HasPassword ? _currentPassword : _newPassword;
        field?.Focus(NavigationMethod.Tab);
    }

    private void ClearForm()
    {
        if (_currentPassword is not null)
        {
            _currentPassword.Text = string.Empty;
            _currentPassword.ErrorMessage = null;
            _currentPassword.IsRevealed = false;
        }
        if (_newPassword is not null)
        {
            _newPassword.Text = string.Empty;
            _newPassword.ErrorMessage = null;
            _newPassword.IsRevealed = false;
        }
    }

    private void HideForm()
    {
        if (IsPasswordFormOpen)
        {
            return;
        }
        if (_form is not null)
        {
            _form.IsVisible = false;
        }
        // Passwords do not linger in a closed form.
        ClearForm();
    }

    private bool IsFocusWithin(Visual? root)
    {
        if (root is null)
        {
            return false;
        }
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
        return focused is not null && (ReferenceEquals(focused, root) || root.IsVisualAncestorOf(focused));
    }

    private void OnChangePasswordClick(object? sender, RoutedEventArgs e) => OpenPasswordForm();

    private void OnCancelPasswordClick(object? sender, RoutedEventArgs e) => ClosePasswordForm();

    private void OnUpdatePasswordClick(object? sender, RoutedEventArgs e) => SubmitPasswordChange();

    private void OnPasswordFieldChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TextBox.TextProperty)
        {
            // A new attempt clears the last rejection.
            if (sender is FieldTextBox field && !string.IsNullOrEmpty(field.ErrorMessage) && IsPasswordFormOpen)
            {
                field.ErrorMessage = null;
            }
            UpdateCanSubmit();
        }
        else if (e.Property == PasswordStrength.LevelProperty)
        {
            UpdateCanSubmit();
        }
    }

    private void OnFormKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None)
        {
            e.Handled = true;
            SubmitPasswordChange();
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            ClosePasswordForm();
        }
    }

    private void OnFormHostChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == BoundsProperty && !IsPasswordFormOpen && _formHost is { Bounds.Height: < 0.5 })
        {
            HideForm();
        }
    }

    private void OnPasswordRowChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == SettingsRow.IsStackedProperty)
        {
            PseudoClasses.Set(RowsStackedPseudoClass, e.GetNewValue<bool>());
        }
    }

    #endregion

    #region Two-step

    private void SyncSwitches()
    {
        _syncingSwitches = true;
        try
        {
            if (_authenticatorSwitch is not null)
            {
                _authenticatorSwitch.IsChecked = IsAuthenticatorEnabled;
            }
            if (_textMessageSwitch is not null)
            {
                _textMessageSwitch.IsChecked = IsTextMessageEnabled;
            }
        }
        finally
        {
            _syncingSwitches = false;
        }
    }

    private void OnSwitchChanged(object? sender, RoutedEventArgs e)
    {
        if (_syncingSwitches)
        {
            return;
        }
        if (ReferenceEquals(sender, _authenticatorSwitch))
        {
            SetCurrentValue(IsAuthenticatorEnabledProperty, _authenticatorSwitch!.IsChecked == true);
        }
        else if (ReferenceEquals(sender, _textMessageSwitch))
        {
            SetCurrentValue(IsTextMessageEnabledProperty, _textMessageSwitch!.IsChecked == true);
        }
    }

    private void OnAddPasskeyClick(object? sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(AddPasskeyRequestedEvent));

    private void OnRecoveryCodesClick(object? sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(RecoveryCodesRequestedEvent));

    #endregion

    #region Sessions

    private void WatchSessions()
    {
        var wanted = _ownsSessions || _watching ? _sessions as INotifyCollectionChanged : null;
        if (ReferenceEquals(wanted, _watchedSessions))
        {
            return;
        }
        if (_watchedSessions is not null)
        {
            _watchedSessions.CollectionChanged -= OnSessionsChanged;
        }
        _watchedSessions = wanted;
        if (_watchedSessions is not null)
        {
            _watchedSessions.CollectionChanged += OnSessionsChanged;
        }
    }

    private void OnSessionsChanged(object? sender, NotifyCollectionChangedEventArgs e) => SyncSessions();

    /// <summary>
    /// Brings the rows in line with <see cref="Sessions"/>: current first, then most recent; rows are reused per session
    /// and the group's items only change where the order did.
    /// </summary>
    private void SyncSessions()
    {
        var listed = new HashSet<SecuritySession>(ReferenceEqualityComparer.Instance);
        foreach (var session in _sessions)
        {
            listed.Add(session);
        }
        _gone.RemoveWhere(s => !listed.Contains(s));

        _ordered.Clear();
        var index = 0;
        var order = new Dictionary<SecuritySettingsSessionView, int>();
        foreach (var session in _sessions)
        {
            if (_gone.Contains(session))
            {
                continue;
            }
            if (!_views.TryGetValue(session, out var view))
            {
                view = new SecuritySettingsSessionView(session, v => SignOut(v.Session), Finish);
                view.Row.StackWidth = SessionRowStackWidth;
                _views[session] = view;
            }
            order[view] = index++;
            _ordered.Add(view);
        }
        _ordered.Sort((a, b) =>
        {
            if (a.Session.IsCurrent != b.Session.IsCurrent)
            {
                return a.Session.IsCurrent ? -1 : 1;
            }
            var byTime = b.Session.LastActive.CompareTo(a.Session.LastActive);
            return byTime != 0 ? byTime : order[a].CompareTo(order[b]);
        });
        foreach (var (session, view) in _views.ToArray())
        {
            if (!order.ContainsKey(view))
            {
                view.Detach();
                _views.Remove(session);
            }
        }

        if (_sessionsGroup is not null)
        {
            var wanted = new List<Control>(_ordered.Count + 1);
            foreach (var view in _ordered)
            {
                wanted.Add(view.Host);
            }
            if (wanted.Count == 0)
            {
                wanted.Add(_emptyNote);
            }
            ApplyItems(_sessionsGroup.Items, wanted);
        }
        UpdateSessionTexts();
    }

    private static void ApplyItems(ItemCollection items, List<Control> wanted)
    {
        var keep = new HashSet<Control>(wanted);
        for (var i = items.Count - 1; i >= 0; i--)
        {
            if (items[i] is not Control control || !keep.Contains(control))
            {
                items.RemoveAt(i);
            }
        }
        for (var i = 0; i < wanted.Count; i++)
        {
            if (i < items.Count && ReferenceEquals(items[i], wanted[i]))
            {
                continue;
            }
            items.Remove(wanted[i]);
            items.Insert(i, wanted[i]);
        }
    }

    private void UpdateSessionTexts()
    {
        var labels = Labels;
        var now = _clock.GetUtcNow();
        var others = 0;
        foreach (var view in _ordered)
        {
            view.Update(labels, now);
            if (!view.Session.IsCurrent && !view.IsLeaving)
            {
                others++;
            }
        }
        if (_sessionsGroup is not null)
        {
            _sessionsGroup.Header = labels.SessionsHeader;
            _sessionsGroup.Description = _ordered.Count == 0 ? null : labels.SessionsDescription(others);
        }
        UpdateSignOutOthers(others);
    }

    private void UpdateSignOutOthers() => UpdateSignOutOthers(CountOthers());

    private void UpdateSignOutOthers(int others)
    {
        var labels = Labels;
        _signOutOthers.Prompt = _isCompact ? labels.SignOutOthersCompactPrompt : labels.SignOutOthersPrompt(Math.Max(1, others));
        // A finished run keeps its "Signed out" face enabled until it returns to rest.
        _signOutOthers.IsEnabled = others > 0 || _signOutOthers.State != ConfirmMorphState.Idle;
    }

    private int CountOthers()
    {
        var others = 0;
        foreach (var view in _ordered)
        {
            if (!view.Session.IsCurrent && !view.IsLeaving)
            {
                others++;
            }
        }
        return others;
    }

    /// <summary>
    /// Signs out one session (not the current one): its row fades and collapses, the session is removed from
    /// <see cref="Sessions"/>, and <see cref="SessionSignedOut"/> is raised.
    /// </summary>
    public void SignOut(SecuritySession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.IsCurrent)
        {
            return;
        }
        if (!_views.TryGetValue(session, out var view) || _sessionsGroup is null)
        {
            // Not shown (no template yet): nothing to animate.
            if (RemoveSession(session))
            {
                RaiseEvent(new SecuritySessionEventArgs(SessionSignedOutEvent, session));
            }
            return;
        }
        if (view.IsLeaving)
        {
            return;
        }
        MoveFocusFrom(view);
        Leave(view);
    }

    /// <summary>
    /// Signs out every session except the current one, as confirming "Sign out other sessions" does: the rows leave one
    /// after another, then <see cref="OtherSessionsSignedOut"/> is raised.
    /// </summary>
    public void SignOutOtherSessions()
    {
        if (_sessionsGroup is null)
        {
            var removed = new List<SecuritySession>();
            foreach (var session in _sessions.ToArray())
            {
                if (!session.IsCurrent && RemoveSession(session))
                {
                    removed.Add(session);
                }
            }
            if (removed.Count > 0)
            {
                RaiseEvent(new SecuritySessionsEventArgs(OtherSessionsSignedOutEvent, removed));
            }
            return;
        }
        var leaving = new List<SecuritySettingsSessionView>();
        foreach (var view in _ordered)
        {
            if (!view.Session.IsCurrent && !view.IsLeaving)
            {
                leaving.Add(view);
            }
        }
        if (leaving.Count == 0)
        {
            return;
        }
        var batch = new SecuritySettingsSignOutBatch(leaving.ConvertAll(v => v.Session));
        foreach (var view in leaving)
        {
            view.Batch = batch;
        }
        // The timers only start each row's PresenceHost exit 35ms after the one above; the motion itself is the host's.
        var staggered = IsLoaded && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off;
        for (var i = 0; i < leaving.Count; i++)
        {
            var view = leaving[i];
            if (!staggered || i == 0)
            {
                Leave(view);
            }
            else
            {
                DispatcherTimer.RunOnce(() => Leave(view), SignOutStagger * i);
            }
        }
        UpdateSessionTexts();
    }

    private void Leave(SecuritySettingsSessionView view)
    {
        if (view.IsLeaving)
        {
            return;
        }
        view.Leave();
        UpdateSessionTexts();
        // Exited is raised by the host; a host that is not loaded or not animating skips straight to the end.
        if (!view.IsFinished && (!view.Host.IsLoaded || ArcMotion.GetEffectiveLevel(view.Host) == ArcMotionLevel.Off))
        {
            Finish(view);
        }
    }

    private void Finish(SecuritySettingsSessionView view)
    {
        if (view.IsFinished || !view.IsLeaving)
        {
            return;
        }
        view.IsFinished = true;
        var session = view.Session;
        RemoveSession(session);
        SyncSessions();
        if (view.Batch is { } batch)
        {
            batch.Remaining--;
            if (batch.Remaining == 0)
            {
                RaiseEvent(new SecuritySessionsEventArgs(OtherSessionsSignedOutEvent, batch.Sessions));
            }
        }
        else
        {
            RaiseEvent(new SecuritySessionEventArgs(SessionSignedOutEvent, session));
        }
    }

    /// <summary>Takes the session off the list, or hides it when the list cannot change.</summary>
    private bool RemoveSession(SecuritySession session)
    {
        if (!_sessions.Contains(session))
        {
            return false;
        }
        if (_sessions.IsReadOnly)
        {
            _gone.Add(session);
        }
        else
        {
            _sessions.Remove(session);
        }
        // A list that does not raise changes, or is not followed now, does not tell the rows.
        if (!ReferenceEquals(_watchedSessions, _sessions))
        {
            SyncSessions();
        }
        return true;
    }

    /// <summary>Keeps keyboard focus in the list when the focused sign-out button is about to leave: the next row's, else the previous row's.</summary>
    private void MoveFocusFrom(SecuritySettingsSessionView view)
    {
        if (view.SignOutButton is not { IsFocused: true })
        {
            return;
        }
        var at = _ordered.IndexOf(view);
        Button? next = null;
        for (var i = at + 1; i < _ordered.Count && next is null; i++)
        {
            next = _ordered[i].IsLeaving ? null : _ordered[i].SignOutButton;
        }
        for (var i = at - 1; i >= 0 && next is null; i--)
        {
            next = _ordered[i].IsLeaving ? null : _ordered[i].SignOutButton;
        }
        // Sign out clicked with the mouse moves focus on without the keyboard ring.
        next?.Focus(BlockFocusModality.For(this));
    }

    #endregion
}
