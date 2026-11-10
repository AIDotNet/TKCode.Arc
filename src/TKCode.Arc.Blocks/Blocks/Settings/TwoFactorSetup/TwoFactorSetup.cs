using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>One numbered instruction beside the QR code.</summary>
internal sealed record TwoFactorSetupInstruction(string Number, string Text);

/// <summary>
/// Turns on two-factor sign-in in one card: choose an authenticator app or text messages, scan a real QR code of an
/// <c>otpauth://</c> address (or enter a phone number), prove it works with a six-digit code, and save the backup codes.
/// A four-step progress shows where the person is; steps slide in the direction of travel while the card's height follows.
/// Without a <see cref="CodeVerifier"/> the card checks codes itself against <see cref="Secret"/> (RFC 6238, 30 second
/// steps, one step of clock drift either way), so any authenticator app works with the built-in example. Enter runs the
/// step's main action, Alt+Left goes back, and focus moves to the new step after every change.
/// </summary>
[TemplatePart(StagePart, typeof(ArcContentSwap))]
[TemplatePart(StepperPart, typeof(Stepper))]
[TemplatePart(PrimaryButtonPart, typeof(AvaloniaButton))]
[TemplatePart(BackButtonPart, typeof(AvaloniaButton))]
[TemplatePart(QrPart, typeof(Control))]
[TemplatePart(CodePart, typeof(OtpInput))]
[PseudoClasses(NarrowPseudoClass)]
public class TwoFactorSetup : TemplatedControl
{
    public const string StagePart = "PART_Stage";
    public const string StepperPart = "PART_Stepper";
    public const string PrimaryButtonPart = "PART_PrimaryButton";
    public const string BackButtonPart = "PART_BackButton";
    public const string QrPart = "PART_Qr";
    public const string CodePart = "PART_Code";

    /// <summary>Set below <c>NarrowWidth</c>: the QR code stacks over its instructions and the buttons fill the row.</summary>
    public const string NarrowPseudoClass = ":narrow";

    internal const string MethodsPart = "PART_Methods";
    internal const string PhonePart = "PART_Phone";
    internal const string KeyPart = "PART_Key";
    internal const string CodesPart = "PART_Codes";
    internal const string SavedCheckPart = "PART_SavedCheck";
    internal const string CopyCodesPart = "PART_CopyCodes";
    internal const string DownloadButtonPart = "PART_DownloadButton";
    internal const string ResendButtonPart = "PART_ResendButton";
    internal const string AnnouncerPart = "PART_Announcer";
    internal const string DoneTitlePart = "PART_DoneTitle";

    /// <summary>The invented secret of the built-in example (Base32, 80 bits).</summary>
    public const string SampleSecret = "QF7M4KZTW2NBX6RD";

    private static readonly string[] s_sampleCodes =
        ["7KQ2-M9XD", "H4TP-2WNA", "R8CV-6LZE", "3YBF-QK7M", "N5WS-J2RU", "D9GX-4HPT", "L6ME-8VCQ", "Z2RA-5NKW", "P7HU-3DXF", "C4JT-9YGB"];

    #region Properties

    /// <summary>The service name shown in the authenticator app and written into the QR code.</summary>
    public static readonly StyledProperty<string> IssuerProperty =
        AvaloniaProperty.Register<TwoFactorSetup, string>(nameof(Issuer), "Lumenfield");

    /// <summary>The account the codes are for (usually the email address), shown in the authenticator app.</summary>
    public static readonly StyledProperty<string> AccountNameProperty =
        AvaloniaProperty.Register<TwoFactorSetup, string>(nameof(AccountName), "mara.okafor@lumenfield.example");

    /// <summary>The shared secret in Base32 (create one per person on your server, or with <see cref="GenerateSecret"/>). The default is an invented example.</summary>
    public static readonly StyledProperty<string> SecretProperty =
        AvaloniaProperty.Register<TwoFactorSetup, string>(nameof(Secret), SampleSecret);

    /// <summary>The chosen method (two-way).</summary>
    public static readonly StyledProperty<TwoFactorSetupMethod> MethodProperty =
        AvaloniaProperty.Register<TwoFactorSetup, TwoFactorSetupMethod>(nameof(Method), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>Whether text messages are offered beside the authenticator app.</summary>
    public static readonly StyledProperty<bool> IsTextMessageAvailableProperty =
        AvaloniaProperty.Register<TwoFactorSetup, bool>(nameof(IsTextMessageAvailable), true);

    /// <summary>The phone number for text messages (two-way).</summary>
    public static readonly StyledProperty<string?> PhoneNumberProperty =
        AvaloniaProperty.Register<TwoFactorSetup, string?>(nameof(PhoneNumber), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>
    /// Checks a code; null checks it against <see cref="Secret"/> as a time-based code. Use it to verify on your server
    /// (for a text message, the code you sent).
    /// </summary>
    public static readonly StyledProperty<Func<TwoFactorSetupVerifyRequest, Task<bool>>?> CodeVerifierProperty =
        AvaloniaProperty.Register<TwoFactorSetup, Func<TwoFactorSetupVerifyRequest, Task<bool>>?>(nameof(CodeVerifier));

    /// <summary>The backup codes to show; empty (the default) shows ten invented examples. Create real ones on your server, or with <see cref="GenerateBackupCodes"/>.</summary>
    public static readonly DirectProperty<TwoFactorSetup, IList<string>> BackupCodesProperty =
        AvaloniaProperty.RegisterDirect<TwoFactorSetup, IList<string>>(nameof(BackupCodes), o => o.BackupCodes, (o, v) => o.BackupCodes = v);

    /// <summary>The step on screen. Changes through the card's buttons, <see cref="GoBack"/> and <see cref="Reset"/>.</summary>
    public static readonly DirectProperty<TwoFactorSetup, TwoFactorSetupStep> StepProperty =
        AvaloniaProperty.RegisterDirect<TwoFactorSetup, TwoFactorSetupStep>(nameof(Step), o => o.Step);

    /// <summary>The built-in text; replace the whole object to localize the card.</summary>
    public static readonly StyledProperty<TwoFactorSetupLabels> LabelsProperty =
        AvaloniaProperty.Register<TwoFactorSetup, TwoFactorSetupLabels>(nameof(Labels), new TwoFactorSetupLabels());

    /// <summary>Below this width the layout stacks (set by the theme from the token).</summary>
    internal static readonly StyledProperty<double> NarrowWidthProperty =
        AvaloniaProperty.Register<TwoFactorSetup, double>(nameof(NarrowWidth), 460);

    /// <inheritdoc cref="IssuerProperty"/>
    public string Issuer { get => GetValue(IssuerProperty); set => SetValue(IssuerProperty, value); }

    /// <inheritdoc cref="AccountNameProperty"/>
    public string AccountName { get => GetValue(AccountNameProperty); set => SetValue(AccountNameProperty, value); }

    /// <inheritdoc cref="SecretProperty"/>
    public string Secret { get => GetValue(SecretProperty); set => SetValue(SecretProperty, value); }

    /// <inheritdoc cref="MethodProperty"/>
    public TwoFactorSetupMethod Method { get => GetValue(MethodProperty); set => SetValue(MethodProperty, value); }

    /// <inheritdoc cref="IsTextMessageAvailableProperty"/>
    public bool IsTextMessageAvailable { get => GetValue(IsTextMessageAvailableProperty); set => SetValue(IsTextMessageAvailableProperty, value); }

    /// <inheritdoc cref="PhoneNumberProperty"/>
    public string? PhoneNumber { get => GetValue(PhoneNumberProperty); set => SetValue(PhoneNumberProperty, value); }

    /// <inheritdoc cref="CodeVerifierProperty"/>
    public Func<TwoFactorSetupVerifyRequest, Task<bool>>? CodeVerifier { get => GetValue(CodeVerifierProperty); set => SetValue(CodeVerifierProperty, value); }

    private IList<string> _backupCodes = new AvaloniaList<string>();

    /// <inheritdoc cref="BackupCodesProperty"/>
    public IList<string> BackupCodes
    {
        get => _backupCodes;
        set
        {
            if (SetAndRaise(BackupCodesProperty, ref _backupCodes, value ?? new AvaloniaList<string>()))
            {
                UpdateCodes();
            }
        }
    }

    private TwoFactorSetupStep _step;

    /// <inheritdoc cref="StepProperty"/>
    public TwoFactorSetupStep Step
    {
        get => _step;
        private set => SetAndRaise(StepProperty, ref _step, value);
    }

    /// <inheritdoc cref="LabelsProperty"/>
    public TwoFactorSetupLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    internal double NarrowWidth { get => GetValue(NarrowWidthProperty); set => SetValue(NarrowWidthProperty, value); }

    /// <summary>The <c>otpauth://</c> address in the QR code: issuer, account and secret, six digits every 30 seconds.</summary>
    public string ProvisioningUri
    {
        get
        {
            var issuer = Uri.EscapeDataString(Issuer ?? string.Empty);
            var account = Uri.EscapeDataString(AccountName ?? string.Empty);
            var secret = TwoFactorSetupTotp.Normalize(Secret) ?? string.Empty;
            return $"otpauth://totp/{issuer}:{account}?secret={secret}&issuer={issuer}&algorithm=SHA1&digits=6&period=30";
        }
    }

    /// <summary>The backup codes on screen: <see cref="BackupCodes"/>, or the examples while it is empty.</summary>
    public IReadOnlyList<string> ShownBackupCodes => _backupCodes.Count > 0 ? _backupCodes.ToArray() : s_sampleCodes;

    #endregion

    #region Events

    /// <summary>Raised after the step changes, with the old and the new step.</summary>
    public static readonly RoutedEvent<TwoFactorSetupStepChangedEventArgs> StepChangedEvent =
        RoutedEvent.Register<TwoFactorSetup, TwoFactorSetupStepChangedEventArgs>(nameof(StepChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when a code should go out by text message (the first time and on "Send a new code").</summary>
    public static readonly RoutedEvent<TwoFactorSetupTextMessageEventArgs> TextMessageRequestedEvent =
        RoutedEvent.Register<TwoFactorSetup, TwoFactorSetupTextMessageEventArgs>(nameof(TextMessageRequested), RoutingStrategies.Bubble);

    /// <summary>Raised by Download before the save dialog opens; mark it handled to save the codes yourself.</summary>
    public static readonly RoutedEvent<TwoFactorSetupSaveEventArgs> BackupCodesSavingEvent =
        RoutedEvent.Register<TwoFactorSetup, TwoFactorSetupSaveEventArgs>(nameof(BackupCodesSaving), RoutingStrategies.Bubble);

    /// <summary>Raised when the person turns two-factor sign-in on, with the method, the secret, the phone number and the backup codes.</summary>
    public static readonly RoutedEvent<TwoFactorSetupCompletedEventArgs> CompletedEvent =
        RoutedEvent.Register<TwoFactorSetup, TwoFactorSetupCompletedEventArgs>(nameof(Completed), RoutingStrategies.Bubble);

    /// <inheritdoc cref="StepChangedEvent"/>
    public event EventHandler<TwoFactorSetupStepChangedEventArgs>? StepChanged
    {
        add => AddHandler(StepChangedEvent, value);
        remove => RemoveHandler(StepChangedEvent, value);
    }

    /// <inheritdoc cref="TextMessageRequestedEvent"/>
    public event EventHandler<TwoFactorSetupTextMessageEventArgs>? TextMessageRequested
    {
        add => AddHandler(TextMessageRequestedEvent, value);
        remove => RemoveHandler(TextMessageRequestedEvent, value);
    }

    /// <inheritdoc cref="BackupCodesSavingEvent"/>
    public event EventHandler<TwoFactorSetupSaveEventArgs>? BackupCodesSaving
    {
        add => AddHandler(BackupCodesSavingEvent, value);
        remove => RemoveHandler(BackupCodesSavingEvent, value);
    }

    /// <inheritdoc cref="CompletedEvent"/>
    public event EventHandler<TwoFactorSetupCompletedEventArgs>? Completed
    {
        add => AddHandler(CompletedEvent, value);
        remove => RemoveHandler(CompletedEvent, value);
    }

    #endregion

    private ArcContentSwap? _stage;
    private Stepper? _stepper;
    private AvaloniaButton? _primary;
    private AvaloniaButton? _back;
    private AuthAnnouncer? _announcer;
    private Control? _methodView;
    private Control? _scanView;
    private Control? _phoneView;
    private Control? _verifyView;
    private Control? _backupView;
    private Control? _doneView;
    private TextBlock? _heading;
    private TextBlock? _headingDescription;
    private TextBlock? _methodTitle;
    private RadioCards? _methods;
    private TextBlock? _scanTitle;
    private Grid? _scanLayout;
    private Control? _scanText;
    private TwoFactorSetupQrCode? _qr;
    private ItemsControl? _instructions;
    private TextBlock? _manualKeyLabel;
    private SelectableTextBlock? _key;
    private CopyButton? _copyKey;
    private TextBlock? _phoneTitle;
    private Input? _phone;
    private TextBlock? _verifyTitle;
    private TextBlock? _verifyDescription;
    private OtpInput? _code;
    private AvaloniaButton? _resend;
    private TextBlock? _backupTitle;
    private TextBlock? _backupDescription;
    private ItemsControl? _codes;
    private CopyButton? _copyCodes;
    private AvaloniaButton? _download;
    private Checkbox? _saved;
    private TextBlock? _doneTitle;
    private TextBlock? _doneDescription;
    private StrokeDrawGlyph? _doneGlyph;
    private bool _verifying;
    private bool _phoneTouched;
    private int _verifyGeneration;

    /// <summary>The clock the built-in verifier reads (tests replace it).</summary>
    internal Func<DateTimeOffset> Clock { get; set; } = static () => DateTimeOffset.UtcNow;

    static TwoFactorSetup()
    {
        FocusableProperty.OverrideDefaultValue<TwoFactorSetup>(false);
    }

    public TwoFactorSetup()
    {
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    #region Public methods

    /// <summary>The current six-digit code of a Base32 secret at a moment (what an authenticator app shows then).</summary>
    public static string ComputeCode(string secret, DateTimeOffset time)
        => TwoFactorSetupTotp.Decode(secret) is { Length: > 0 } key ? TwoFactorSetupTotp.Code(key, time) : string.Empty;

    /// <summary>A new random secret in Base32 (160 bits, the length authenticator apps expect).</summary>
    public static string GenerateSecret()
    {
        Span<byte> bytes = stackalloc byte[20];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return TwoFactorSetupTotp.Encode(bytes);
    }

    /// <summary>New random backup codes in the form <c>XXXX-XXXX</c>, without look-alike characters.</summary>
    public static IReadOnlyList<string> GenerateBackupCodes(int count = 10)
    {
        const string alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";
        var codes = new string[Math.Max(0, count)];
        Span<char> chars = stackalloc char[9];
        for (var i = 0; i < codes.Length; i++)
        {
            for (var j = 0; j < 9; j++)
            {
                chars[j] = j == 4 ? '-' : alphabet[System.Security.Cryptography.RandomNumberGenerator.GetInt32(alphabet.Length)];
            }
            codes[i] = new string(chars);
        }
        return codes;
    }

    /// <summary>Goes back one step (from Connect, Verify and the backup codes). Returns false when there is nowhere to go.</summary>
    public bool GoBack()
    {
        var target = Step switch
        {
            TwoFactorSetupStep.Connect => TwoFactorSetupStep.Method,
            TwoFactorSetupStep.Verify => TwoFactorSetupStep.Connect,
            TwoFactorSetupStep.BackupCodes => TwoFactorSetupStep.Verify,
            _ => (TwoFactorSetupStep?)null
        };
        if (target is not { } step)
        {
            return false;
        }
        GoTo(step, -1);
        return true;
    }

    /// <summary>Returns to the first step and clears the code, the phone check and the saved checkbox.</summary>
    public void Reset()
    {
        _code?.SetCurrentValue(OtpInput.ValueProperty, string.Empty);
        _saved?.SetCurrentValue(ToggleButton.IsCheckedProperty, false);
        _phoneTouched = false;
        GoTo(TwoFactorSetupStep.Method, -1);
    }

    /// <summary>Runs the main action of the current step, as the primary button does.</summary>
    public void Continue() => _ = ContinueAsync();

    #endregion

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        Detach();
        base.OnApplyTemplate(e);
        _stage = e.NameScope.Find<ArcContentSwap>(StagePart);
        _stepper = e.NameScope.Find<Stepper>(StepperPart);
        _primary = e.NameScope.Find<AvaloniaButton>(PrimaryButtonPart);
        _back = e.NameScope.Find<AvaloniaButton>(BackButtonPart);
        _announcer = e.NameScope.Find<AuthAnnouncer>(AnnouncerPart);
        _methodView = e.NameScope.Find<Control>("PART_MethodStep");
        _scanView = e.NameScope.Find<Control>("PART_ScanStep");
        _phoneView = e.NameScope.Find<Control>("PART_PhoneStep");
        _verifyView = e.NameScope.Find<Control>("PART_VerifyStep");
        _backupView = e.NameScope.Find<Control>("PART_BackupStep");
        _doneView = e.NameScope.Find<Control>("PART_DoneStep");
        _heading = e.NameScope.Find<TextBlock>("PART_Heading");
        _headingDescription = e.NameScope.Find<TextBlock>("PART_HeadingDescription");
        _methodTitle = e.NameScope.Find<TextBlock>("PART_MethodTitle");
        _methods = e.NameScope.Find<RadioCards>(MethodsPart);
        _scanTitle = e.NameScope.Find<TextBlock>("PART_ScanTitle");
        _scanLayout = e.NameScope.Find<Grid>("PART_ScanLayout");
        _scanText = e.NameScope.Find<Control>("PART_ScanText");
        _qr = e.NameScope.Find<TwoFactorSetupQrCode>(QrPart);
        _instructions = e.NameScope.Find<ItemsControl>("PART_Instructions");
        _manualKeyLabel = e.NameScope.Find<TextBlock>("PART_ManualKeyLabel");
        _key = e.NameScope.Find<SelectableTextBlock>(KeyPart);
        _copyKey = e.NameScope.Find<CopyButton>("PART_CopyKey");
        _phoneTitle = e.NameScope.Find<TextBlock>("PART_PhoneTitle");
        _phone = e.NameScope.Find<Input>(PhonePart);
        _verifyTitle = e.NameScope.Find<TextBlock>("PART_VerifyTitle");
        _verifyDescription = e.NameScope.Find<TextBlock>("PART_VerifyDescription");
        _code = e.NameScope.Find<OtpInput>(CodePart);
        _resend = e.NameScope.Find<AvaloniaButton>(ResendButtonPart);
        _backupTitle = e.NameScope.Find<TextBlock>("PART_BackupTitle");
        _backupDescription = e.NameScope.Find<TextBlock>("PART_BackupDescription");
        _codes = e.NameScope.Find<ItemsControl>(CodesPart);
        _copyCodes = e.NameScope.Find<CopyButton>(CopyCodesPart);
        _download = e.NameScope.Find<AvaloniaButton>(DownloadButtonPart);
        _saved = e.NameScope.Find<Checkbox>(SavedCheckPart);
        _doneTitle = e.NameScope.Find<TextBlock>(DoneTitlePart);
        _doneDescription = e.NameScope.Find<TextBlock>("PART_DoneDescription");
        _doneGlyph = e.NameScope.Find<StrokeDrawGlyph>("PART_DoneGlyph");

        if (_primary is not null)
        {
            _primary.Click += OnPrimaryClick;
        }
        if (_back is not null)
        {
            _back.Click += OnBackClick;
        }
        if (_methods is not null)
        {
            _methods.SelectionChanged += OnMethodSelectionChanged;
        }
        if (_phone is not null)
        {
            _phone.PropertyChanged += OnPhonePropertyChanged;
        }
        if (_code is not null)
        {
            _code.Completed += OnCodeCompleted;
            _code.ValueChanged += OnCodeValueChanged;
        }
        if (_resend is not null)
        {
            _resend.Click += OnResendClick;
        }
        if (_download is not null)
        {
            _download.Click += OnDownloadClick;
        }
        if (_saved is not null)
        {
            _saved.IsCheckedChanged += OnSavedChanged;
        }
        if (_doneGlyph is not null)
        {
            _doneGlyph.AttachedToVisualTree += OnDoneGlyphAttached;
        }

        BuildMethods();
        BuildStepper();
        UpdateTexts();
        UpdateQr();
        UpdateCodes();
        ShowStep(Step, 1, animate: false);
        UpdateChrome();
    }

    private void Detach()
    {
        if (_primary is not null)
        {
            _primary.Click -= OnPrimaryClick;
        }
        if (_back is not null)
        {
            _back.Click -= OnBackClick;
        }
        if (_methods is not null)
        {
            _methods.SelectionChanged -= OnMethodSelectionChanged;
        }
        if (_phone is not null)
        {
            _phone.PropertyChanged -= OnPhonePropertyChanged;
        }
        if (_code is not null)
        {
            _code.Completed -= OnCodeCompleted;
            _code.ValueChanged -= OnCodeValueChanged;
        }
        if (_resend is not null)
        {
            _resend.Click -= OnResendClick;
        }
        if (_download is not null)
        {
            _download.Click -= OnDownloadClick;
        }
        if (_saved is not null)
        {
            _saved.IsCheckedChanged -= OnSavedChanged;
        }
        if (_doneGlyph is not null)
        {
            _doneGlyph.AttachedToVisualTree -= OnDoneGlyphAttached;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelsProperty)
        {
            BuildMethods();
            BuildStepper();
            UpdateTexts();
            UpdateCodes();
            UpdateChrome();
        }
        else if (change.Property == IssuerProperty || change.Property == AccountNameProperty || change.Property == SecretProperty)
        {
            UpdateQr();
            UpdateCodes();
        }
        else if (change.Property == MethodProperty)
        {
            if (_methods is not null)
            {
                _methods.SelectedIndex = Method == TwoFactorSetupMethod.TextMessage && IsTextMessageAvailable ? 1 : 0;
            }
            UpdateTexts();
            UpdateChrome();
        }
        else if (change.Property == IsTextMessageAvailableProperty)
        {
            if (!IsTextMessageAvailable && Method == TwoFactorSetupMethod.TextMessage)
            {
                SetCurrentValue(MethodProperty, TwoFactorSetupMethod.AuthenticatorApp);
            }
            BuildMethods();
        }
        else if (change.Property == PhoneNumberProperty)
        {
            if (_phone is not null && _phone.Text != PhoneNumber)
            {
                _phone.Text = PhoneNumber;
            }
            UpdatePhoneError();
            UpdateChrome();
        }
        else if (change.Property == BoundsProperty || change.Property == NarrowWidthProperty)
        {
            UpdateNarrow();
        }
    }

    #endregion

    #region Content

    private void BuildMethods()
    {
        if (_methods is null)
        {
            return;
        }
        var labels = Labels;
        var options = new List<RadioCardOption>
        {
            new(TwoFactorSetupMethod.AuthenticatorApp, labels.AppOption, labels.AppOptionDescription, labels.Recommended)
            {
                Icon = ArcIcons.Smartphone
            }
        };
        if (IsTextMessageAvailable)
        {
            options.Add(new RadioCardOption(TwoFactorSetupMethod.TextMessage, labels.TextOption, labels.TextOptionDescription)
            {
                Icon = ArcIcons.MessageSquare
            });
        }
        _methods.SelectionChanged -= OnMethodSelectionChanged;
        _methods.ItemsSource = options;
        _methods.SelectedIndex = Method == TwoFactorSetupMethod.TextMessage && IsTextMessageAvailable ? 1 : 0;
        _methods.SelectionChanged += OnMethodSelectionChanged;
        AutomationProperties.SetName(_methods, labels.MethodTitle);
    }

    private void UpdateTexts()
    {
        var labels = Labels;
        SetText(_heading, labels.Title);
        SetText(_headingDescription, labels.Description);
        SetText(_methodTitle, labels.MethodTitle);
        SetText(_scanTitle, labels.ScanTitle);
        SetText(_manualKeyLabel, labels.ManualKey);
        SetText(_phoneTitle, labels.PhoneTitle);
        SetText(_verifyTitle, labels.VerifyTitle);
        SetText(_verifyDescription, Method == TwoFactorSetupMethod.AuthenticatorApp
            ? labels.VerifyAppDescription
            : labels.VerifyTextDescription(MaskPhone(PhoneNumber)));
        SetText(_backupTitle, labels.BackupTitle);
        SetText(_backupDescription, labels.BackupDescription);
        SetText(_doneTitle, labels.DoneTitle);
        SetText(_doneDescription, labels.DoneDescription(Method));

        if (_instructions is not null)
        {
            var steps = labels.ScanSteps;
            var items = new TwoFactorSetupInstruction[steps.Length];
            for (var i = 0; i < steps.Length; i++)
            {
                items[i] = new TwoFactorSetupInstruction((i + 1).ToString(labels.Culture), steps[i]);
            }
            _instructions.ItemsSource = items;
        }
        if (_copyKey is not null)
        {
            _copyKey.Label = labels.CopyKey;
            AutomationProperties.SetName(_copyKey, labels.CopyKey);
        }
        if (_phone is not null)
        {
            _phone.Label = labels.PhoneLabel;
            _phone.Placeholder = labels.PhonePlaceholder;
            _phone.Description = labels.PhoneDescription;
        }
        if (_code is not null)
        {
            _code.Label = labels.CodeLabel;
        }
        if (_resend is not null)
        {
            _resend.Content = labels.Resend;
        }
        if (_copyCodes is not null)
        {
            _copyCodes.Label = labels.CopyCodes;
        }
        if (_download is not null)
        {
            _download.Content = labels.Download;
        }
        if (_saved is not null)
        {
            _saved.Content = labels.SavedConfirm;
        }
        if (_codes is not null)
        {
            AutomationProperties.SetName(_codes, labels.BackupListName);
        }
        if (_back is not null)
        {
            _back.Content = labels.Back;
        }
    }

    private void BuildStepper()
    {
        if (_stepper is null)
        {
            return;
        }
        _stepper.Steps.Clear();
        foreach (var name in Labels.StepNames)
        {
            _stepper.Steps.Add(new StepperStep(name));
        }
        _stepper.CurrentIndex = (int)Step;
    }

    private static void SetText(TextBlock? block, string text)
    {
        if (block is not null)
        {
            block.Text = text;
        }
    }

    private void UpdateQr()
    {
        var secret = TwoFactorSetupTotp.Normalize(Secret) ?? string.Empty;
        if (_qr is not null)
        {
            _qr.Text = ProvisioningUri;
            AutomationProperties.SetName(_qr, Labels.QrName(Issuer, AccountName));
        }
        if (_key is not null)
        {
            _key.Text = GroupKey(secret);
        }
        if (_copyKey is not null)
        {
            _copyKey.Value = secret;
        }
    }

    /// <summary>The key in groups of four, the way people copy it by hand.</summary>
    internal static string GroupKey(string secret)
    {
        var text = new StringBuilder(secret.Length + secret.Length / 4);
        for (var i = 0; i < secret.Length; i++)
        {
            if (i > 0 && i % 4 == 0)
            {
                text.Append(' ');
            }
            text.Append(secret[i]);
        }
        return text.ToString();
    }

    private void UpdateCodes()
    {
        var codes = ShownBackupCodes;
        if (_codes is not null)
        {
            _codes.ItemsSource = codes;
        }
        if (_copyCodes is not null)
        {
            _copyCodes.Value = BackupText(codes);
        }
    }

    private string BackupText(IReadOnlyList<string> codes)
    {
        var text = new StringBuilder();
        text.AppendLine(Labels.FileHeading(Issuer, AccountName));
        text.AppendLine();
        foreach (var code in codes)
        {
            text.AppendLine(code);
        }
        return text.ToString();
    }

    /// <summary>"•••• 0199": the last four digits of a number.</summary>
    internal static string MaskPhone(string? phone)
    {
        var digits = new StringBuilder();
        foreach (var c in phone ?? string.Empty)
        {
            if (char.IsAsciiDigit(c))
            {
                digits.Append(c);
            }
        }
        return digits.Length <= 4 ? digits.ToString() : "•••• " + digits.ToString(digits.Length - 4, 4);
    }

    private static int DigitCount(string? phone)
    {
        var count = 0;
        foreach (var c in phone ?? string.Empty)
        {
            if (char.IsAsciiDigit(c))
            {
                count++;
            }
        }
        return count;
    }

    private bool IsPhoneValid => DigitCount(PhoneNumber) >= 7;

    private void UpdatePhoneError()
    {
        if (_phone is null)
        {
            return;
        }
        var show = _phoneTouched && !IsPhoneValid;
        _phone.IsInvalid = show;
        _phone.ErrorMessage = show ? Labels.PhoneInvalid : null;
    }

    private void UpdateNarrow()
    {
        var narrow = Bounds.Width > 0 && Bounds.Width < NarrowWidth;
        PseudoClasses.Set(NarrowPseudoClass, narrow);
        if (_scanLayout is null || _scanText is null)
        {
            return;
        }
        var qrTile = _qr?.Parent as Control;
        if (narrow)
        {
            Grid.SetColumn(_scanText, 0);
            Grid.SetRow(_scanText, 1);
            Grid.SetColumnSpan(_scanText, 2);
            _scanText.Margin = new Thickness(0, 20, 0, 0);
            if (qrTile is not null)
            {
                qrTile.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
                Grid.SetColumnSpan(qrTile, 2);
            }
        }
        else
        {
            Grid.SetColumn(_scanText, 1);
            Grid.SetRow(_scanText, 0);
            Grid.SetColumnSpan(_scanText, 1);
            _scanText.Margin = new Thickness(24, 0, 0, 0);
            if (qrTile is not null)
            {
                qrTile.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
                Grid.SetColumnSpan(qrTile, 1);
            }
        }
    }

    #endregion

    #region Steps

    private Control? ViewFor(TwoFactorSetupStep step) => step switch
    {
        TwoFactorSetupStep.Method => _methodView,
        TwoFactorSetupStep.Connect => Method == TwoFactorSetupMethod.AuthenticatorApp ? _scanView : _phoneView,
        TwoFactorSetupStep.Verify => _verifyView,
        TwoFactorSetupStep.BackupCodes => _backupView,
        _ => _doneView
    };

    private TextBlock? TitleFor(TwoFactorSetupStep step) => step switch
    {
        TwoFactorSetupStep.Method => _methodTitle,
        TwoFactorSetupStep.Connect => Method == TwoFactorSetupMethod.AuthenticatorApp ? _scanTitle : _phoneTitle,
        TwoFactorSetupStep.Verify => _verifyTitle,
        TwoFactorSetupStep.BackupCodes => _backupTitle,
        _ => _doneTitle
    };

    private void GoTo(TwoFactorSetupStep step, int direction)
    {
        var old = Step;
        _verifyGeneration++;
        _verifying = false;
        Step = step;
        UpdateTexts();
        ShowStep(step, direction, animate: true);
        UpdateChrome();
        if (step == TwoFactorSetupStep.Verify && _code is not null)
        {
            _code.IsInvalid = false;
            _code.IsSuccess = false;
            _code.ErrorMessage = null;
        }
        if (step == TwoFactorSetupStep.Connect && Method == TwoFactorSetupMethod.AuthenticatorApp)
        {
            _qr?.PlayReveal();
        }
        if (old != step)
        {
            var title = TitleFor(step)?.Text ?? string.Empty;
            var number = Math.Min((int)step + 1, Labels.StepNames.Length);
            _announcer?.Announce(step == TwoFactorSetupStep.Done ? title : Labels.StepAnnouncement(number, Labels.StepNames.Length, title));
            FocusStep();
            RaiseEvent(new TwoFactorSetupStepChangedEventArgs(StepChangedEvent, old, step));
        }
    }

    private void ShowStep(TwoFactorSetupStep step, int direction, bool animate)
    {
        if (_stage is null || ViewFor(step) is not { } view)
        {
            return;
        }
        if (_stage.Content is TwoFactorSetupStepHost { Child: { } shown } && ReferenceEquals(shown, view))
        {
            return;
        }
        if (view.Parent is TwoFactorSetupStepHost previous)
        {
            previous.Child = null;
        }
        else if (view.Parent is Panel store)
        {
            store.Children.Remove(view);
        }
        var host = new TwoFactorSetupStepHost { Child = view };
        _stage.Direction = direction;
        if (animate)
        {
            _stage.Content = host;
        }
        else
        {
            var mode = _stage.Mode;
            _stage.Mode = ContentSwapMode.None;
            _stage.Content = host;
            _stage.Mode = mode;
        }
    }

    /// <summary>Focus goes to what the person does next: the code field, the phone field, or the step's title.</summary>
    private void FocusStep()
    {
        var step = Step;
        void Focus()
        {
            if (Step != step)
            {
                return;
            }
            if (step == TwoFactorSetupStep.Verify && _code is not null)
            {
                _code.FocusSlot(0);
            }
            else if (step == TwoFactorSetupStep.Connect && Method == TwoFactorSetupMethod.TextMessage && _phone is not null)
            {
                _phone.Focus();
            }
            else
            {
                TitleFor(step)?.Focus();
            }
        }
        // The new view joins the tree with the stage's next layout.
        Dispatcher.UIThread.Post(Focus, DispatcherPriority.Loaded);
    }

    private void UpdateChrome()
    {
        var labels = Labels;
        if (_stepper is not null)
        {
            _stepper.CurrentIndex = (int)Step;
        }
        if (_back is not null)
        {
            var canGoBack = Step is TwoFactorSetupStep.Connect or TwoFactorSetupStep.Verify or TwoFactorSetupStep.BackupCodes;
            _back.IsVisible = canGoBack;
        }
        if (_primary is not null)
        {
            _primary.Content = Step switch
            {
                TwoFactorSetupStep.Connect when Method == TwoFactorSetupMethod.TextMessage => labels.SendCode,
                TwoFactorSetupStep.Verify => labels.Verify,
                TwoFactorSetupStep.BackupCodes => labels.Finish,
                TwoFactorSetupStep.Done => labels.Restart,
                _ => labels.Continue
            };
            if (_primary is Button arcButton)
            {
                arcButton.Variant = Step == TwoFactorSetupStep.Done ? ButtonVariant.Secondary : ButtonVariant.Primary;
                arcButton.IsLoading = _verifying;
            }
            _primary.IsEnabled = Step switch
            {
                TwoFactorSetupStep.Verify => !_verifying && (_code?.Value.Length ?? 0) == TwoFactorSetupTotp.Digits,
                TwoFactorSetupStep.BackupCodes => _saved?.IsChecked == true,
                _ => true
            };
        }
        if (_resend is not null)
        {
            _resend.IsVisible = Step == TwoFactorSetupStep.Verify && Method == TwoFactorSetupMethod.TextMessage;
        }
    }

    private async Task ContinueAsync()
    {
        switch (Step)
        {
            case TwoFactorSetupStep.Method:
                GoTo(TwoFactorSetupStep.Connect, 1);
                break;
            case TwoFactorSetupStep.Connect when Method == TwoFactorSetupMethod.TextMessage:
                _phoneTouched = true;
                UpdatePhoneError();
                if (!IsPhoneValid)
                {
                    _phone?.Focus();
                    return;
                }
                RaiseEvent(new TwoFactorSetupTextMessageEventArgs(TextMessageRequestedEvent, PhoneNumber!.Trim(), isResend: false));
                GoTo(TwoFactorSetupStep.Verify, 1);
                break;
            case TwoFactorSetupStep.Connect:
                GoTo(TwoFactorSetupStep.Verify, 1);
                break;
            case TwoFactorSetupStep.Verify:
                await VerifyAsync();
                break;
            case TwoFactorSetupStep.BackupCodes when _saved?.IsChecked == true:
                GoTo(TwoFactorSetupStep.Done, 1);
                RaiseEvent(new TwoFactorSetupCompletedEventArgs(CompletedEvent, Method, TwoFactorSetupTotp.Normalize(Secret) ?? string.Empty,
                    Method == TwoFactorSetupMethod.TextMessage ? PhoneNumber : null, ShownBackupCodes));
                break;
            case TwoFactorSetupStep.Done:
                Reset();
                break;
        }
    }

    private async Task VerifyAsync()
    {
        if (_code is null || _verifying || _code.Value.Length != TwoFactorSetupTotp.Digits)
        {
            return;
        }
        var generation = ++_verifyGeneration;
        var secret = TwoFactorSetupTotp.Normalize(Secret) ?? string.Empty;
        var request = new TwoFactorSetupVerifyRequest(Method, _code.Value, secret,
            Method == TwoFactorSetupMethod.TextMessage ? PhoneNumber : null);
        bool valid;
        if (CodeVerifier is { } verifier)
        {
            _verifying = true;
            UpdateChrome();
            try
            {
                valid = await verifier(request);
            }
            catch (Exception)
            {
                valid = false;
            }
            if (generation != _verifyGeneration)
            {
                // The person moved on (Back or Reset) while the check ran.
                return;
            }
            _verifying = false;
        }
        else
        {
            valid = TwoFactorSetupTotp.Verify(secret, request.Code, Clock());
        }

        if (valid)
        {
            _code.IsInvalid = false;
            _code.ErrorMessage = null;
            _code.IsSuccess = true;
            GoTo(TwoFactorSetupStep.BackupCodes, 1);
        }
        else
        {
            _code.ErrorMessage = Labels.CodeInvalid;
            _code.IsInvalid = true;
            _code.Shake();
            _announcer?.Announce(Labels.CodeInvalid);
            UpdateChrome();
            _code.FocusSlot(0);
        }
    }

    #endregion

    #region Handlers

    private void OnPrimaryClick(object? sender, RoutedEventArgs e) => Continue();

    private void OnBackClick(object? sender, RoutedEventArgs e) => GoBack();

    private void OnMethodSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_methods?.SelectedItem is RadioCardOption { Value: TwoFactorSetupMethod method })
        {
            SetCurrentValue(MethodProperty, method);
        }
    }

    private void OnPhonePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TextBox.TextProperty && _phone is not null && _phone.Text != PhoneNumber)
        {
            SetCurrentValue(PhoneNumberProperty, _phone.Text);
        }
    }

    private void OnCodeValueChanged(object? sender, OtpInputValueChangedEventArgs e)
    {
        if (_code is not null && _code.IsInvalid && e.IsUserEdit)
        {
            _code.IsInvalid = false;
            _code.ErrorMessage = null;
        }
        UpdateChrome();
    }

    private void OnCodeCompleted(object? sender, OtpInputCompletedEventArgs e)
    {
        if (Step == TwoFactorSetupStep.Verify)
        {
            Continue();
        }
    }

    private void OnResendClick(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(PhoneNumber))
        {
            return;
        }
        RaiseEvent(new TwoFactorSetupTextMessageEventArgs(TextMessageRequestedEvent, PhoneNumber.Trim(), isResend: true));
        _code?.SetCurrentValue(OtpInput.ValueProperty, string.Empty);
        _announcer?.Announce(Labels.Resent);
        _code?.FocusSlot(0);
    }

    private void OnSavedChanged(object? sender, RoutedEventArgs e) => UpdateChrome();

    private async void OnDownloadClick(object? sender, RoutedEventArgs e)
    {
        var args = new TwoFactorSetupSaveEventArgs(BackupCodesSavingEvent, Labels.FileName, BackupText(ShownBackupCodes));
        RaiseEvent(args);
        if (args.Handled || TopLevel.GetTopLevel(this)?.StorageProvider is not { CanSave: true } storage)
        {
            return;
        }
        try
        {
            var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                SuggestedFileName = args.FileName,
                DefaultExtension = "txt",
                FileTypeChoices = [FilePickerFileTypes.TextPlain]
            });
            if (file is null)
            {
                return;
            }
            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            await writer.WriteAsync(args.Text);
        }
        catch (Exception)
        {
            // A refused or failed save leaves the codes on screen to copy instead.
        }
    }

    private void OnDoneGlyphAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (_doneGlyph is null)
        {
            return;
        }
        // The check draws itself each time the last step arrives (the view is reused), which a theme transition on
        // Progress cannot express; below full motion it is simply there.
        if (ArcMotion.IsFull(this))
        {
            _doneGlyph.Progress = 0;
            _ = _doneGlyph.Play(ArcMotionPresets.Considered, ArcEasings.Standard, ArcMotionPresets.Fast);
        }
        else
        {
            _doneGlyph.Progress = 1;
        }
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Left && e.KeyModifiers == KeyModifiers.Alt)
        {
            if (GoBack())
            {
                e.Handled = true;
            }
            return;
        }
        if (e.Key != Key.Enter || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        // Buttons, checkboxes and the code field answer Enter themselves.
        if (e.Source is AvaloniaButton or ToggleButton || (e.Source is Visual visual && _code is not null && _code.IsVisualAncestorOf(visual)))
        {
            return;
        }
        if (_primary is { IsEnabled: true, IsVisible: true } && Step != TwoFactorSetupStep.Done)
        {
            e.Handled = true;
            Continue();
        }
    }

    #endregion
}

/// <summary>Carries one step view of a <see cref="TwoFactorSetup"/> onto the stage; it always takes the full width it is given.</summary>
internal sealed class TwoFactorSetupStepHost : Decorator
{
    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        return double.IsInfinity(availableSize.Width) ? size : size.WithWidth(availableSize.Width);
    }
}
