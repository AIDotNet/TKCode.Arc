// Ported from Arc's free login-centered block (MIT, Copyright (c) 2026 Elia Kuratli): login-centered.tsx.
using System.Text.RegularExpressions;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// A passkey-first sign in and sign up screen (Arc's Login › Centered block): a card on a quiet ring backdrop that
/// morphs from the passkey view through an email address and a six digit code to the signed-in account, with single
/// sign-on buttons, a sign in / sign up switch in the top bar and a status line in the footer. Steps rise into place out
/// of a small blur while the card's height follows on a spring; the resend countdown rolls; the backdrop drifts with a
/// fine pointer.
/// </summary>
/// <remarks>
/// The flow is simulated unless you connect it: <see cref="PasskeyAsync"/> (WebAuthn), <see cref="SendCodeAsync"/>
/// (your code sender), <see cref="VerifyCodeAsync"/> (verification) and <see cref="SignInWithProviderAsync"/> (OAuth). Without
/// them the block behaves like Arc's preview: a passkey resolves to <see cref="DemoEmail"/> after 1.7s, a code is "sent"
/// in .8s and <see cref="DemoCode"/> is the right one. <see cref="SignedIn"/> reports the address, the method and the
/// mode. Set <see cref="IsFullScreen"/> when the block is the whole window.
/// </remarks>
[TemplatePart(BackdropPart, typeof(LoginCenteredBackdrop))]
[TemplatePart(ViewportPart, typeof(LoginCenteredViewport))]
[TemplatePart(StatusPart, typeof(LoginCenteredPresence))]
[TemplatePart(AnnouncerPart, typeof(AuthAnnouncer))]
[TemplatePart(ModeSwitchPart, typeof(LoginCenteredPillButton))]
[TemplatePart(TermsPart, typeof(LoginCenteredPillButton))]
[TemplatePart(PrivacyPart, typeof(LoginCenteredPillButton))]
[PseudoClasses(SignUpPseudoClass, NarrowPseudoClass, TinyPseudoClass, FullScreenPseudoClass)]
public class LoginCentered : TemplatedControl
{
    public const string BackdropPart = "PART_Backdrop";
    public const string ViewportPart = "PART_Viewport";
    public const string StatusPart = "PART_Status";
    public const string AnnouncerPart = "PART_Announcer";
    public const string ModeSwitchPart = "PART_ModeSwitch";
    public const string TermsPart = "PART_Terms";
    public const string PrivacyPart = "PART_Privacy";
    public const string LegalPart = "PART_Legal";
    public const string BrandNamePart = "PART_BrandName";
    public const string NoAccountPromptPart = "PART_NoAccountPrompt";
    public const string HaveAccountPromptPart = "PART_HaveAccountPrompt";
    public const string SignUpActionPart = "PART_SignUpAction";
    public const string SignInActionPart = "PART_SignInAction";

    /// <summary>Set while <see cref="Mode"/> is <see cref="LoginCenteredMode.SignUp"/>.</summary>
    public const string SignUpPseudoClass = ":signup";

    /// <summary>Set while the block is at most 520 wide: tighter bars, stage and card (Arc's container query).</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Set while the block is at most 400 wide: a 28px title and tighter provider buttons.</summary>
    public const string TinyPseudoClass = ":tiny";

    /// <summary>Set while <see cref="IsFullScreen"/> is true.</summary>
    public const string FullScreenPseudoClass = ":fullscreen";

    internal const double NarrowWidth = 520;
    internal const double TinyWidth = 400;
    internal const int CodeLength = 6;

    private static readonly Regex s_email = new(@"^[^\s@]+@[^\s@]+\.[^\s@]{2,}$", RegexOptions.CultureInvariant);

    #region Properties

    /// <summary>Fill the host with no frame (Arc's <c>fullScreen</c>): no border, square corners, no minimum height. Off by default: a framed 640px block.</summary>
    public static readonly StyledProperty<bool> IsFullScreenProperty =
        AvaloniaProperty.Register<LoginCentered, bool>(nameof(IsFullScreen));

    /// <summary>Sign in (default) or create an account; two-way. Setting it moves the card as the top bar's switch does.</summary>
    public static readonly StyledProperty<LoginCenteredMode> ModeProperty =
        AvaloniaProperty.Register<LoginCentered, LoginCenteredMode>(nameof(Mode), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The text of the email field (Arc's <c>email</c> state); two-way. Kept when the card moves between views.</summary>
    public static readonly StyledProperty<string> EmailProperty =
        AvaloniaProperty.Register<LoginCentered, string>(nameof(Email), string.Empty, defaultBindingMode: BindingMode.TwoWay,
            coerce: (_, v) => v ?? string.Empty);

    /// <summary>The code the simulated email contains (Arc's <c>demoCode</c>); shown as helper copy while no <see cref="VerifyCodeAsync"/> is set.</summary>
    public static readonly StyledProperty<string> DemoCodeProperty =
        AvaloniaProperty.Register<LoginCentered, string>(nameof(DemoCode), "482913");

    /// <summary>The account a simulated passkey or single sign-on resolves to (Arc's <c>demoEmail</c>).</summary>
    public static readonly StyledProperty<string> DemoEmailProperty =
        AvaloniaProperty.Register<LoginCentered, string>(nameof(DemoEmail), "maya@northwind.studio");

    /// <summary>The footer's Terms link (Arc's <c>termsHref</c>). An in-page address (<c>#terms</c>) only says in the status line where it would go.</summary>
    public static readonly StyledProperty<string?> TermsHrefProperty =
        AvaloniaProperty.Register<LoginCentered, string?>(nameof(TermsHref), "#terms");

    /// <summary>The footer's Privacy link (Arc's <c>privacyHref</c>).</summary>
    public static readonly StyledProperty<string?> PrivacyHrefProperty =
        AvaloniaProperty.Register<LoginCentered, string?>(nameof(PrivacyHref), "#privacy");

    /// <summary>Seconds before another code can be requested (Arc's <c>RESEND_SECONDS</c>, 30).</summary>
    public static readonly StyledProperty<int> ResendSecondsProperty =
        AvaloniaProperty.Register<LoginCentered, int>(nameof(ResendSeconds), 30, coerce: (_, v) => Math.Max(0, v));

    /// <summary>All built-in text; replace the object to localize or reword it.</summary>
    public static readonly StyledProperty<LoginCenteredLabels> LabelsProperty =
        AvaloniaProperty.Register<LoginCentered, LoginCenteredLabels>(nameof(Labels), new LoginCenteredLabels(),
            coerce: (_, v) => v ?? new LoginCenteredLabels());

    /// <summary>Executed with the <see cref="LoginCenteredSignedInEventArgs"/> after <see cref="SignedIn"/>.</summary>
    public static readonly StyledProperty<ICommand?> SignedInCommandProperty =
        AvaloniaProperty.Register<LoginCentered, ICommand?>(nameof(SignedInCommand));

    /// <summary>The view on show: Passkey, Email, Code or Done.</summary>
    public static readonly DirectProperty<LoginCentered, LoginCenteredStep> StepProperty =
        AvaloniaProperty.RegisterDirect<LoginCentered, LoginCenteredStep>(nameof(Step), o => o.Step);

    /// <summary>The passkey request: Idle, Waiting (the device is asked) or Verified.</summary>
    public static readonly DirectProperty<LoginCentered, LoginCenteredPasskeyPhase> PasskeyPhaseProperty =
        AvaloniaProperty.RegisterDirect<LoginCentered, LoginCenteredPasskeyPhase>(nameof(PasskeyPhase), o => o.PasskeyPhase);

    /// <summary>True while a request runs (sending or checking a code, a provider); actions are ignored meanwhile.</summary>
    public static readonly DirectProperty<LoginCentered, bool> IsBusyProperty =
        AvaloniaProperty.RegisterDirect<LoginCentered, bool>(nameof(IsBusy), o => o.IsBusy);

    /// <summary>The account the card shows: the address a code was sent to, or the signed-in address.</summary>
    public static readonly DirectProperty<LoginCentered, string> AccountProperty =
        AvaloniaProperty.RegisterDirect<LoginCentered, string>(nameof(Account), o => o.Account);

    /// <summary>How the person signed in (meaningful on the Done view).</summary>
    public static readonly DirectProperty<LoginCentered, LoginCenteredMethod> MethodProperty =
        AvaloniaProperty.RegisterDirect<LoginCentered, LoginCenteredMethod>(nameof(Method), o => o.Method);

    /// <summary>The line in the footer, also announced to screen readers ("Code sent to …").</summary>
    public static readonly DirectProperty<LoginCentered, string> StatusProperty =
        AvaloniaProperty.RegisterDirect<LoginCentered, string>(nameof(Status), o => o.Status);

    /// <summary>Raised once a sign in or sign up succeeds (Arc's <c>onSignIn(email, method, mode)</c>).</summary>
    public static readonly RoutedEvent<LoginCenteredSignedInEventArgs> SignedInEvent =
        RoutedEvent.Register<LoginCentered, LoginCenteredSignedInEventArgs>(nameof(SignedIn), RoutingStrategies.Bubble);

    /// <summary>Raised when Sign out returns the card to the passkey view.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> SignedOutEvent =
        RoutedEvent.Register<LoginCentered, RoutedEventArgs>(nameof(SignedOut), RoutingStrategies.Bubble);

    /// <summary>Raised when a footer link is activated; handle it to open the destination yourself.</summary>
    public static readonly RoutedEvent<LoginCenteredNavigateEventArgs> NavigateEvent =
        RoutedEvent.Register<LoginCentered, LoginCenteredNavigateEventArgs>(nameof(Navigate), RoutingStrategies.Bubble);

    /// <summary>Fill the host with no frame.</summary>
    public bool IsFullScreen { get => GetValue(IsFullScreenProperty); set => SetValue(IsFullScreenProperty, value); }

    /// <summary>Sign in or create an account.</summary>
    public LoginCenteredMode Mode { get => GetValue(ModeProperty); set => SetValue(ModeProperty, value); }

    /// <summary>The text of the email field.</summary>
    public string Email { get => GetValue(EmailProperty); set => SetValue(EmailProperty, value); }

    /// <summary>The simulated code.</summary>
    public string DemoCode { get => GetValue(DemoCodeProperty); set => SetValue(DemoCodeProperty, value); }

    /// <summary>The simulated account.</summary>
    public string DemoEmail { get => GetValue(DemoEmailProperty); set => SetValue(DemoEmailProperty, value); }

    /// <summary>The Terms link.</summary>
    public string? TermsHref { get => GetValue(TermsHrefProperty); set => SetValue(TermsHrefProperty, value); }

    /// <summary>The Privacy link.</summary>
    public string? PrivacyHref { get => GetValue(PrivacyHrefProperty); set => SetValue(PrivacyHrefProperty, value); }

    /// <summary>Seconds before a code can be resent.</summary>
    public int ResendSeconds { get => GetValue(ResendSecondsProperty); set => SetValue(ResendSecondsProperty, value); }

    /// <summary>All built-in text.</summary>
    public LoginCenteredLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <summary>Executed after a successful sign in.</summary>
    public ICommand? SignedInCommand { get => GetValue(SignedInCommandProperty); set => SetValue(SignedInCommandProperty, value); }

    /// <summary>The view on show.</summary>
    public LoginCenteredStep Step { get => _step; private set => SetAndRaise(StepProperty, ref _step, value); }

    /// <summary>The passkey request.</summary>
    public LoginCenteredPasskeyPhase PasskeyPhase { get => _passkey; private set => SetAndRaise(PasskeyPhaseProperty, ref _passkey, value); }

    /// <summary>True while a request runs.</summary>
    public bool IsBusy { get => _isBusy; private set => SetAndRaise(IsBusyProperty, ref _isBusy, value); }

    /// <summary>The account the card shows.</summary>
    public string Account { get => _account; private set => SetAndRaise(AccountProperty, ref _account, value); }

    /// <summary>How the person signed in.</summary>
    public LoginCenteredMethod Method { get => _method; private set => SetAndRaise(MethodProperty, ref _method, value); }

    /// <summary>The footer's status line.</summary>
    public string Status { get => _status; private set => SetAndRaise(StatusProperty, ref _status, value); }

    /// <summary>Raised once a sign in or sign up succeeds.</summary>
    public event EventHandler<LoginCenteredSignedInEventArgs>? SignedIn
    {
        add => AddHandler(SignedInEvent, value);
        remove => RemoveHandler(SignedInEvent, value);
    }

    /// <summary>Raised when Sign out returns the card to the passkey view.</summary>
    public event EventHandler<RoutedEventArgs>? SignedOut
    {
        add => AddHandler(SignedOutEvent, value);
        remove => RemoveHandler(SignedOutEvent, value);
    }

    /// <summary>Raised when a footer link is activated.</summary>
    public event EventHandler<LoginCenteredNavigateEventArgs>? Navigate
    {
        add => AddHandler(NavigateEvent, value);
        remove => RemoveHandler(NavigateEvent, value);
    }

    /// <summary>
    /// Asks the device for a passkey (connect it to WebAuthn) and returns the account's address. Throwing
    /// <see cref="OperationCanceledException"/> means the person dismissed the prompt; any other exception means it
    /// failed. The token is cancelled when the person presses Cancel. Null simulates Arc's preview (1.7s, <see cref="DemoEmail"/>).
    /// </summary>
    public Func<CancellationToken, Task<string>>? PasskeyAsync { get; set; }

    /// <summary>
    /// Signs in with a provider (connect it to OAuth) and returns the account's address; an exception means it did not
    /// finish. Null simulates Arc's preview (.9s, <see cref="DemoEmail"/>).
    /// </summary>
    public Func<LoginCenteredProvider, CancellationToken, Task<string>>? SignInWithProviderAsync { get; set; }

    /// <summary>
    /// Sends a code to the address (trimmed, lower case) for the given mode; also used by "Resend code". An exception
    /// shows <see cref="LoginCenteredLabels.SendFailed"/>. Null simulates Arc's preview (.8s).
    /// </summary>
    public Func<string, LoginCenteredMode, CancellationToken, Task>? SendCodeAsync { get; set; }

    /// <summary>
    /// Checks a code for an address; true accepts it. An exception shows <see cref="LoginCenteredLabels.VerifyFailed"/>.
    /// Null simulates Arc's preview (.7s, accepts <see cref="DemoCode"/>) and shows the demo code as helper copy.
    /// </summary>
    public Func<string, string, CancellationToken, Task<bool>>? VerifyCodeAsync { get; set; }

    #endregion

    private enum Busy
    {
        None,
        Email,
        Code,
        Provider
    }

    private LoginCenteredStep _step;
    private LoginCenteredPasskeyPhase _passkey;
    private bool _isBusy;
    private string _account = "maya@northwind.studio";
    private LoginCenteredMethod _method;
    private string _status = string.Empty;
    private Busy _busy;
    private LoginCenteredProvider _busyProvider;
    private int _direction = 1;
    private bool _verified;
    private bool _touched;
    private bool _attempted;
    private string _code = string.Empty;
    private string _codeError = string.Empty;
    private string _sendError = string.Empty;
    private int _resendIn = 30;
    private LoginCenteredStep? _focusNext;
    private CancellationTokenSource _timers = new();
    private int _countdownRun;
    private bool _countdownRunning;
    private bool _settingMode;
    private bool _syncingEmail;
    private bool _keyboardModality;
    private bool _isNarrow;
    private bool _isTiny;

    private LoginCenteredBackdrop? _backdrop;
    private LoginCenteredViewport? _viewport;
    private LoginCenteredPresence? _statusLine;
    private AuthAnnouncer? _announcer;
    private LoginCenteredPillButton? _modeSwitch;
    private LoginCenteredPillButton? _terms;
    private LoginCenteredPillButton? _privacy;
    private Control? _legal;
    private TextBlock? _brandName;
    private TextBlock? _noAccountPrompt;
    private TextBlock? _haveAccountPrompt;
    private TextBlock? _signUpAction;
    private TextBlock? _signInAction;
    private LoginCenteredStepView? _view;
    private string? _shownStatus;

    static LoginCentered()
    {
        FocusableProperty.OverrideDefaultValue<LoginCentered>(false);
    }

    public LoginCentered()
    {
        AddHandler(PointerMovedEvent, OnAnyPointerMoved, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(KeyDownEvent, (_, _) => _keyboardModality = true, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerPressedEvent, (_, _) => _keyboardModality = false, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    /// <summary>
    /// Replaces how the block waits (simulated requests, the pause after a success, the resend countdown); tests use a
    /// manual clock. Defaults to <see cref="Task.Delay(int, CancellationToken)"/>.
    /// </summary>
    internal Func<int, CancellationToken, Task> Delay { get; set; } = static (ms, token) => Task.Delay(ms, token);

    internal LoginCenteredStepView? View => _view;

    internal LoginCenteredViewport? Viewport => _viewport;

    internal LoginCenteredBackdrop? Backdrop => _backdrop;

    internal AuthAnnouncer? Announcer => _announcer;

    internal LoginCenteredPresence? StatusLine => _statusLine;

    internal LoginCenteredPillButton? ModeSwitch => _modeSwitch;

    internal LoginCenteredPillButton? TermsButton => _terms;

    internal LoginCenteredPillButton? PrivacyButton => _privacy;

    /// <summary>The email field's error on show (tests read it).</summary>
    internal string EmailError => CurrentEmailError();

    internal string CodeError => _codeError;

    internal string Code => _code;

    internal int ResendIn => _resendIn;

    internal bool IsVerified => _verified;

    // ---- public actions (Arc's handlers) --------------------------------------------------------------------------

    /// <summary>Asks for the passkey, as the passkey button does. Ignored unless the passkey view is idle and nothing is busy.</summary>
    public void StartPasskey()
    {
        if (_step != LoginCenteredStep.Passkey || _passkey != LoginCenteredPasskeyPhase.Idle || _busy != Busy.None)
        {
            return;
        }
        _ = RunPasskeyAsync();
    }

    /// <summary>Cancels a passkey request in progress, as Cancel does.</summary>
    public void CancelPasskey()
    {
        if (_passkey != LoginCenteredPasskeyPhase.Waiting)
        {
            return;
        }
        Settle();
        PasskeyPhase = LoginCenteredPasskeyPhase.Idle;
        SetStatus(Labels.StatusPasskeyCanceled);
        Sync();
        FocusSoon((_view as LoginCenteredPasskeyView)?.PasskeyButton);
    }

    /// <summary>Signs in with a provider, as its button does. Ignored while something is busy or a passkey is asked.</summary>
    public void SignInWith(LoginCenteredProvider provider)
    {
        if (_step != LoginCenteredStep.Passkey || _busy != Busy.None || _passkey != LoginCenteredPasskeyPhase.Idle)
        {
            return;
        }
        _ = RunProviderAsync(provider);
    }

    /// <summary>Flips between signing in and creating an account, as the top bar's switch does.</summary>
    public void SwitchMode()
    {
        Settle();
        _attempted = false;
        _code = string.Empty;
        if (Mode == LoginCenteredMode.SignUp)
        {
            SetMode(LoginCenteredMode.SignIn);
            Go(LoginCenteredStep.Passkey, -1);
            SetStatus(Labels.StatusSignIn);
        }
        else
        {
            SetMode(LoginCenteredMode.SignUp);
            Go(LoginCenteredStep.Email, 1);
            SetStatus(Labels.StatusSignUp);
        }
        Sync();
    }

    /// <summary>Signs out: clears the address and returns to the passkey view in sign-in mode, as Sign out does.</summary>
    public void SignOut()
    {
        Settle();
        _syncingEmail = true;
        SetCurrentValue(EmailProperty, string.Empty);
        _syncingEmail = false;
        _touched = false;
        _attempted = false;
        _sendError = string.Empty;
        _code = string.Empty;
        SetMode(LoginCenteredMode.SignIn);
        Go(LoginCenteredStep.Passkey, -1);
        SetStatus(Labels.StatusSignedOut);
        Sync();
        RaiseEvent(new RoutedEventArgs(SignedOutEvent));
    }

    // ---- Arc's flow ---------------------------------------------------------------------------------------------

    private CancellationToken TimersToken => _timers.Token;

    /// <summary>Any new direction cancels whatever the previous one was still waiting on (Arc's <c>settle</c>).</summary>
    private void Settle()
    {
        _timers.Cancel();
        _timers.Dispose();
        _timers = new CancellationTokenSource();
        SetBusy(Busy.None);
        _verified = false;
    }

    private void SetBusy(Busy busy, LoginCenteredProvider provider = default)
    {
        _busy = busy;
        _busyProvider = provider;
        IsBusy = busy != Busy.None;
    }

    private void Go(LoginCenteredStep next, int towards)
    {
        _focusNext = next == LoginCenteredStep.Code ? null : next;
        _direction = towards;
        if (next == LoginCenteredStep.Passkey)
        {
            PasskeyPhase = LoginCenteredPasskeyPhase.Idle;
        }
        Step = next;
    }

    private void SetMode(LoginCenteredMode mode)
    {
        _settingMode = true;
        SetCurrentValue(ModeProperty, mode);
        _settingMode = false;
        PseudoClasses.Set(SignUpPseudoClass, mode == LoginCenteredMode.SignUp);
    }

    private void Complete(string address, LoginCenteredMethod how)
    {
        Settle();
        Account = address;
        Method = how;
        Go(LoginCenteredStep.Done, 1);
        var mode = Mode;
        SetStatus(Labels.StatusComplete(mode, how));
        Sync();
        var args = new LoginCenteredSignedInEventArgs(SignedInEvent, address, how, mode);
        RaiseEvent(args);
        if (SignedInCommand is { } command && command.CanExecute(args))
        {
            command.Execute(args);
        }
    }

    private async Task RunPasskeyAsync()
    {
        PasskeyPhase = LoginCenteredPasskeyPhase.Waiting;
        SetStatus(Labels.StatusWaiting);
        Sync();
        var token = TimersToken;
        string address;
        try
        {
            if (PasskeyAsync is { } handler)
            {
                address = await handler(token);
            }
            else
            {
                await Delay(1700, token);
                address = DemoEmail;
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return;
        }
        catch (OperationCanceledException)
        {
            PasskeyPhase = LoginCenteredPasskeyPhase.Idle;
            SetStatus(Labels.StatusPasskeyCanceled);
            Sync();
            return;
        }
        catch (Exception) when (!token.IsCancellationRequested)
        {
            PasskeyPhase = LoginCenteredPasskeyPhase.Idle;
            SetStatus(Labels.StatusPasskeyFailed);
            Sync();
            return;
        }
        if (token.IsCancellationRequested)
        {
            return;
        }
        PasskeyPhase = LoginCenteredPasskeyPhase.Verified;
        SetStatus(Labels.PasskeyVerified);
        Sync();
        if (!await Wait(640, token))
        {
            return;
        }
        Complete(address, LoginCenteredMethod.Passkey);
    }

    private void SwitchToEmail()
    {
        if (_passkey == LoginCenteredPasskeyPhase.Verified || _busy != Busy.None)
        {
            return;
        }
        Settle();
        PasskeyPhase = LoginCenteredPasskeyPhase.Idle;
        _attempted = false;
        Go(LoginCenteredStep.Email, 1);
        SetStatus(Labels.StatusUseEmail);
        Sync();
    }

    private async Task RunProviderAsync(LoginCenteredProvider provider)
    {
        SetBusy(Busy.Provider, provider);
        SetStatus(Labels.StatusOpening(provider));
        Sync();
        var token = TimersToken;
        string address;
        try
        {
            if (SignInWithProviderAsync is { } handler)
            {
                address = await handler(provider, token);
            }
            else
            {
                await Delay(900, token);
                address = DemoEmail;
            }
        }
        catch (Exception) when (token.IsCancellationRequested)
        {
            return;
        }
        catch (Exception)
        {
            SetBusy(Busy.None);
            SetStatus(Labels.StatusProviderFailed(provider));
            Sync();
            return;
        }
        if (token.IsCancellationRequested)
        {
            return;
        }
        Complete(address, provider switch
        {
            LoginCenteredProvider.Google => LoginCenteredMethod.Google,
            LoginCenteredProvider.Apple => LoginCenteredMethod.Apple,
            _ => LoginCenteredMethod.GitHub
        });
    }

    /// <summary>Arc's <c>isEmail</c>: something, an at sign, something, a dot and two more characters, no spaces.</summary>
    internal static bool IsEmail(string? value) => value is not null && s_email.IsMatch(value.Trim());

    private string CurrentEmailError()
    {
        var email = Email;
        if ((_touched || _attempted) && !IsEmail(email))
        {
            return string.IsNullOrWhiteSpace(email) ? Labels.EmptyEmail : Labels.InvalidEmail;
        }
        return _sendError;
    }

    private void SubmitEmail()
    {
        if (_step != LoginCenteredStep.Email || _busy != Busy.None)
        {
            return;
        }
        _attempted = true;
        if (!IsEmail(Email))
        {
            Sync();
            FocusSoon((_view as LoginCenteredEmailView)?.Input, keyboard: false);
            return;
        }
        _ = RunSendAsync();
    }

    private async Task RunSendAsync()
    {
        _sendError = string.Empty;
        SetBusy(Busy.Email);
        SetStatus(Labels.StatusSending);
        Sync();
        var token = TimersToken;
        var address = Email.Trim().ToLowerInvariant();
        var mode = Mode;
        try
        {
            if (SendCodeAsync is { } handler)
            {
                await handler(address, mode, token);
            }
            else
            {
                await Delay(800, token);
            }
        }
        catch (Exception) when (token.IsCancellationRequested)
        {
            return;
        }
        catch (Exception)
        {
            SetBusy(Busy.None);
            _sendError = Labels.SendFailed;
            SetStatus(Labels.SendFailed);
            Sync();
            FocusSoon((_view as LoginCenteredEmailView)?.Input, keyboard: false);
            return;
        }
        if (token.IsCancellationRequested)
        {
            return;
        }
        SetBusy(Busy.None);
        Account = address;
        _code = string.Empty;
        _codeError = string.Empty;
        _resendIn = ResendSeconds;
        Go(LoginCenteredStep.Code, 1);
        SetStatus(Labels.StatusCodeSent(address));
        RestartCountdown();
        Sync();
    }

    private void Verify(string value)
    {
        if (_step != LoginCenteredStep.Code || _busy != Busy.None || _verified)
        {
            return;
        }
        if (value.Length < CodeLength)
        {
            _codeError = Labels.IncompleteCode;
            Sync();
            FocusCode();
            return;
        }
        _ = RunVerifyAsync(value);
    }

    private async Task RunVerifyAsync(string value)
    {
        SetBusy(Busy.Code);
        SetStatus(Labels.StatusChecking);
        Sync();
        var token = TimersToken;
        bool accepted;
        string mismatch = Labels.CodeMismatch;
        string mismatchStatus = Labels.StatusMismatch;
        try
        {
            if (VerifyCodeAsync is { } handler)
            {
                accepted = await handler(Account, value, token);
            }
            else
            {
                await Delay(700, token);
                accepted = value == DemoCode;
            }
        }
        catch (Exception) when (token.IsCancellationRequested)
        {
            return;
        }
        catch (Exception)
        {
            accepted = false;
            mismatch = Labels.VerifyFailed;
            mismatchStatus = Labels.VerifyFailed;
        }
        if (token.IsCancellationRequested)
        {
            return;
        }
        SetBusy(Busy.None);
        if (!accepted)
        {
            _code = string.Empty;
            _codeError = mismatch;
            SetStatus(mismatchStatus);
            Sync();
            FocusCode();
            return;
        }
        _verified = true;
        SetStatus(Labels.StatusVerified);
        Sync();
        if (!await Wait(600, token))
        {
            return;
        }
        Complete(Account, LoginCenteredMethod.EmailCode);
    }

    private void ChangeCode(string value)
    {
        if (_busy != Busy.None || _verified)
        {
            return;
        }
        _code = value;
        if (_codeError.Length > 0 && value.Length > 0)
        {
            _codeError = string.Empty;
        }
        Sync();
    }

    private void Resend()
    {
        if (_busy != Busy.None || _verified || _resendIn > 0)
        {
            return;
        }
        _resendIn = ResendSeconds;
        _code = string.Empty;
        _codeError = string.Empty;
        SetStatus(Labels.StatusNewCode);
        RestartCountdown();
        Sync();
        FocusCode();
        if (SendCodeAsync is { } handler)
        {
            _ = ResendAsync(handler);
        }
    }

    private async Task ResendAsync(Func<string, LoginCenteredMode, CancellationToken, Task> handler)
    {
        var token = TimersToken;
        try
        {
            await handler(Account, Mode, token);
        }
        catch (Exception) when (!token.IsCancellationRequested && _step == LoginCenteredStep.Code)
        {
            _codeError = Labels.SendFailed;
            SetStatus(Labels.SendFailed);
            Sync();
        }
        catch (Exception)
        {
            // Superseded: the card moved on.
        }
    }

    private void ChangeEmail()
    {
        Settle();
        _attempted = false;
        Go(LoginCenteredStep.Email, -1);
        SetStatus(Labels.StatusEditEmail);
        Sync();
    }

    private void SwitchToPasskey()
    {
        Settle();
        SetMode(LoginCenteredMode.SignIn);
        Go(LoginCenteredStep.Passkey, -1);
        SetStatus(Labels.StatusUsePasskey);
        Sync();
    }

    private async void OpenLegal(LoginCenteredLink link)
    {
        var href = link == LoginCenteredLink.Terms ? TermsHref : PrivacyHref;
        var args = new LoginCenteredNavigateEventArgs(NavigateEvent, link, href);
        RaiseEvent(args);
        if (args.Handled)
        {
            return;
        }
        if (string.IsNullOrWhiteSpace(href) || href.TrimStart().StartsWith('#'))
        {
            SetStatus(Labels.LegalStatus(link));
            Sync();
            return;
        }
        if (Uri.TryCreate(href.Trim(), UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" or "mailto" &&
            TopLevel.GetTopLevel(this)?.Launcher is { } launcher)
        {
            try
            {
                await launcher.LaunchUriAsync(uri);
            }
            catch (Exception)
            {
                // Nothing to open it with: the link stays a no-op, as a blocked popup would.
            }
        }
    }

    private async Task<bool> Wait(int milliseconds, CancellationToken token)
    {
        try
        {
            await Delay(milliseconds, token);
            return !token.IsCancellationRequested;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>Counts the resend timer down once a second while the code view is on show (Arc's effect on <c>[step, resendIn]</c>).</summary>
    private void RestartCountdown()
    {
        _countdownRun++;
        _countdownRunning = false;
        EnsureCountdown();
    }

    private async void EnsureCountdown()
    {
        if (_countdownRunning || _step != LoginCenteredStep.Code || _resendIn <= 0 || !IsLoaded)
        {
            return;
        }
        _countdownRunning = true;
        var run = _countdownRun;
        while (true)
        {
            try
            {
                await Delay(1000, CancellationToken.None);
            }
            catch (Exception)
            {
                break;
            }
            if (run != _countdownRun || _step != LoginCenteredStep.Code || _resendIn <= 0)
            {
                break;
            }
            _resendIn = Math.Max(0, _resendIn - 1);
            Sync();
        }
        if (run == _countdownRun)
        {
            _countdownRunning = false;
        }
    }

    private void FocusCode() => (_view as LoginCenteredCodeView)?.FocusCode();

    private void SetStatus(string text) => Status = text;

    // ---- rendering ----------------------------------------------------------------------------------------------

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_modeSwitch is not null)
        {
            _modeSwitch.Click -= OnModeSwitchClick;
        }
        if (_terms is not null)
        {
            _terms.Click -= OnTermsClick;
        }
        if (_privacy is not null)
        {
            _privacy.Click -= OnPrivacyClick;
        }
        _viewport?.Clear();
        _statusLine?.Clear();
        _backdrop = e.NameScope.Find<LoginCenteredBackdrop>(BackdropPart);
        _viewport = e.NameScope.Find<LoginCenteredViewport>(ViewportPart);
        _statusLine = e.NameScope.Find<LoginCenteredPresence>(StatusPart);
        _announcer = e.NameScope.Find<AuthAnnouncer>(AnnouncerPart);
        _modeSwitch = e.NameScope.Find<LoginCenteredPillButton>(ModeSwitchPart);
        _terms = e.NameScope.Find<LoginCenteredPillButton>(TermsPart);
        _privacy = e.NameScope.Find<LoginCenteredPillButton>(PrivacyPart);
        _legal = e.NameScope.Find<Control>(LegalPart);
        _brandName = e.NameScope.Find<TextBlock>(BrandNamePart);
        _noAccountPrompt = e.NameScope.Find<TextBlock>(NoAccountPromptPart);
        _haveAccountPrompt = e.NameScope.Find<TextBlock>(HaveAccountPromptPart);
        _signUpAction = e.NameScope.Find<TextBlock>(SignUpActionPart);
        _signInAction = e.NameScope.Find<TextBlock>(SignInActionPart);
        if (_modeSwitch is not null)
        {
            _modeSwitch.Click += OnModeSwitchClick;
        }
        if (_terms is not null)
        {
            _terms.Click += OnTermsClick;
        }
        if (_privacy is not null)
        {
            _privacy.Click += OnPrivacyClick;
        }
        if (_legal is not null)
        {
            AutomationProperties.SetLandmarkType(_legal, AutomationLandmarkType.Navigation);
        }
        _view = null;
        _shownStatus = null;
        Sync();
    }

    private void OnModeSwitchClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        SwitchMode();
    }

    private void OnTermsClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        OpenLegal(LoginCenteredLink.Terms);
    }

    private void OnPrivacyClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        OpenLegal(LoginCenteredLink.Privacy);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        EnsureCountdown();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        // Leaving the tree abandons whatever was pending (Arc clears its timers on unmount).
        if (_busy != Busy.None || _passkey == LoginCenteredPasskeyPhase.Waiting || _verified)
        {
            Settle();
            if (_passkey == LoginCenteredPasskeyPhase.Waiting)
            {
                PasskeyPhase = LoginCenteredPasskeyPhase.Idle;
            }
            Sync();
        }
        _countdownRun++;
        _countdownRunning = false;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == ModeProperty)
        {
            PseudoClasses.Set(SignUpPseudoClass, Mode == LoginCenteredMode.SignUp);
            if (!_settingMode)
            {
                OnModeSetByHost();
            }
        }
        else if (property == IsFullScreenProperty)
        {
            PseudoClasses.Set(FullScreenPseudoClass, IsFullScreen);
        }
        else if (property == EmailProperty)
        {
            if (!_syncingEmail)
            {
                _sendError = string.Empty;
                Sync();
            }
        }
        else if (property == LabelsProperty || property == DemoCodeProperty)
        {
            _shownStatus = null;
            Sync();
        }
        else if (property == StatusProperty)
        {
            ShowStatus();
        }
    }

    /// <summary>The host set <see cref="Mode"/>: move the card the way the switch would (at once before the block is shown).</summary>
    private void OnModeSetByHost()
    {
        var signingUp = Mode == LoginCenteredMode.SignUp;
        Settle();
        _attempted = false;
        _code = string.Empty;
        if (!IsLoaded)
        {
            Step = signingUp ? LoginCenteredStep.Email : LoginCenteredStep.Passkey;
            PasskeyPhase = LoginCenteredPasskeyPhase.Idle;
            Sync();
            return;
        }
        Go(signingUp ? LoginCenteredStep.Email : LoginCenteredStep.Passkey, signingUp ? 1 : -1);
        SetStatus(signingUp ? Labels.StatusSignUp : Labels.StatusSignIn);
        Sync();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        var width = e.NewSize.Width;
        var narrow = width <= NarrowWidth;
        var tiny = width <= TinyWidth;
        if (narrow == _isNarrow && tiny == _isTiny)
        {
            return;
        }
        _isNarrow = narrow;
        _isTiny = tiny;
        PseudoClasses.Set(NarrowPseudoClass, narrow);
        PseudoClasses.Set(TinyPseudoClass, tiny);
        _view?.SetWidthClass(narrow, tiny);
    }

    /// <summary>Arc's render: the view for the current step (a new one when the key changes), its state, the frame's text.</summary>
    private void Sync()
    {
        SyncFrame();
        if (_viewport is null)
        {
            return;
        }
        var signingUp = Mode == LoginCenteredMode.SignUp;
        var key = _step switch
        {
            LoginCenteredStep.Passkey => "passkey",
            LoginCenteredStep.Email => signingUp ? "email-sign-up" : "email-sign-in",
            LoginCenteredStep.Code => "code",
            _ => "done"
        };
        if (_view is null || _view.Key != key)
        {
            var animate = _view is not null && IsLoaded;
            var view = CreateView(_step, signingUp);
            view.SetWidthClass(_isNarrow, _isTiny);
            UpdateView(view);
            _view = view;
            _viewport.ShowStep(view, _direction, animate);
            var focus = _focusNext;
            _focusNext = null;
            if (focus is not null && animate)
            {
                FocusSoon(view.FocusTarget, keyboard: view is not LoginCenteredEmailView && view is not LoginCenteredDoneView ? null : false);
            }
        }
        else
        {
            UpdateView(_view);
        }
        if (_step != LoginCenteredStep.Code)
        {
            _countdownRun++;
            _countdownRunning = false;
        }
        else
        {
            EnsureCountdown();
        }
    }

    private void SyncFrame()
    {
        var labels = Labels;
        var signingUp = Mode == LoginCenteredMode.SignUp;
        AutomationProperties.SetName(this, signingUp ? labels.SignUpScreenName : labels.SignInScreenName);
        if (_brandName is not null)
        {
            _brandName.Text = labels.BrandName;
        }
        if (_noAccountPrompt is not null)
        {
            _noAccountPrompt.Text = labels.NoAccountPrompt;
            _noAccountPrompt.SetValue(AutomationProperties.AccessibilityViewProperty, signingUp ? AccessibilityView.Raw : AccessibilityView.Default);
        }
        if (_haveAccountPrompt is not null)
        {
            _haveAccountPrompt.Text = labels.HaveAccountPrompt;
            _haveAccountPrompt.SetValue(AutomationProperties.AccessibilityViewProperty, signingUp ? AccessibilityView.Default : AccessibilityView.Raw);
        }
        if (_signUpAction is not null)
        {
            _signUpAction.Text = labels.SignUpAction;
        }
        if (_signInAction is not null)
        {
            _signInAction.Text = labels.SignInAction;
        }
        if (_modeSwitch is not null)
        {
            AutomationProperties.SetName(_modeSwitch, signingUp ? labels.SignInAction : labels.SignUpAction);
        }
        if (_terms is not null)
        {
            _terms.Content = labels.Terms;
            AutomationProperties.SetName(_terms, labels.Terms);
        }
        if (_privacy is not null)
        {
            _privacy.Content = labels.Privacy;
            AutomationProperties.SetName(_privacy, labels.Privacy);
        }
        if (_legal is not null)
        {
            AutomationProperties.SetName(_legal, labels.LegalName);
        }
        if (_shownStatus is null)
        {
            ShowStatus();
        }
    }

    private void ShowStatus()
    {
        var text = _status;
        if (_statusLine is null || text == _shownStatus)
        {
            return;
        }
        var animate = _shownStatus is not null && IsLoaded;
        _shownStatus = text;
        _announcer?.Announce(text);
        var line = new TextBlock
        {
            Text = text,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
            VerticalAlignment = VerticalAlignment.Center
        };
        _statusLine.Show(text.Length == 0 ? null : line, animate, 6, -4);
    }

    private LoginCenteredStepView CreateView(LoginCenteredStep step, bool signingUp)
    {
        switch (step)
        {
            case LoginCenteredStep.Passkey:
            {
                var view = new LoginCenteredPasskeyView();
                view.PasskeyButton.Click += (_, e) =>
                {
                    e.Handled = true;
                    StartPasskey();
                };
                view.SecondaryButton.Click += (_, e) =>
                {
                    e.Handled = true;
                    if (_passkey == LoginCenteredPasskeyPhase.Waiting)
                    {
                        CancelPasskey();
                    }
                    else
                    {
                        SwitchToEmail();
                    }
                };
                foreach (var button in view.ProviderButtons)
                {
                    var provider = (LoginCenteredProvider)button.Tag!;
                    button.Click += (_, e) =>
                    {
                        e.Handled = true;
                        SignInWith(provider);
                    };
                }
                return view;
            }
            case LoginCenteredStep.Email:
            {
                var view = new LoginCenteredEmailView(signingUp);
                view.Input.TextChanged += (_, _) =>
                {
                    if (_syncingEmail || !ReferenceEquals(_view, view))
                    {
                        return;
                    }
                    _syncingEmail = true;
                    SetCurrentValue(EmailProperty, view.Input.Text ?? string.Empty);
                    _syncingEmail = false;
                    _sendError = string.Empty;
                    Sync();
                };
                view.Input.LostFocus += (_, _) =>
                {
                    if (ReferenceEquals(_view, view) && !string.IsNullOrWhiteSpace(Email) && !_touched)
                    {
                        _touched = true;
                        Sync();
                    }
                };
                view.Input.AddHandler(KeyDownEvent, (_, e) =>
                {
                    if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None)
                    {
                        e.Handled = true;
                        SubmitEmail();
                    }
                }, RoutingStrategies.Tunnel);
                view.SubmitButton.Click += (_, e) =>
                {
                    e.Handled = true;
                    SubmitEmail();
                };
                if (view.PasskeyButton is { } passkey)
                {
                    passkey.Click += (_, e) =>
                    {
                        e.Handled = true;
                        SwitchToPasskey();
                    };
                }
                return view;
            }
            case LoginCenteredStep.Code:
            {
                var view = new LoginCenteredCodeView();
                view.ChangeButton.Click += (_, e) =>
                {
                    e.Handled = true;
                    ChangeEmail();
                };
                view.Otp.ValueChanged += (_, e) =>
                {
                    if (e.IsUserEdit && ReferenceEquals(_view, view))
                    {
                        ChangeCode(e.NewValue);
                    }
                };
                view.Otp.Completed += (_, e) =>
                {
                    if (ReferenceEquals(_view, view))
                    {
                        Verify(e.Code);
                    }
                };
                view.Otp.AddHandler(KeyDownEvent, (_, e) =>
                {
                    if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None)
                    {
                        e.Handled = true;
                        Verify(_code);
                    }
                }, RoutingStrategies.Bubble, handledEventsToo: true);
                view.VerifyButton.Click += (_, e) =>
                {
                    e.Handled = true;
                    Verify(_code);
                };
                view.ResendButton.Click += (_, e) =>
                {
                    e.Handled = true;
                    Resend();
                };
                return view;
            }
            default:
            {
                var view = new LoginCenteredDoneView();
                view.SignOutButton.Click += (_, e) =>
                {
                    e.Handled = true;
                    SignOut();
                };
                return view;
            }
        }
    }

    private void UpdateView(LoginCenteredStepView view)
    {
        var labels = Labels;
        var signingUp = Mode == LoginCenteredMode.SignUp;
        switch (view)
        {
            case LoginCenteredPasskeyView passkey:
                passkey.Update(labels, _passkey, _busy == Busy.Provider ? _busyProvider : null);
                break;
            case LoginCenteredEmailView email:
                _syncingEmail = true;
                email.Update(labels, Email, CurrentEmailError(), _busy == Busy.Email);
                _syncingEmail = false;
                break;
            case LoginCenteredCodeView code:
                code.Update(labels, Account, signingUp, VerifyCodeAsync is null ? DemoCode : null, _code, _codeError,
                    _busy == Busy.Code, _verified, _resendIn);
                break;
            case LoginCenteredDoneView done:
                done.Update(labels, Account, signingUp, _method);
                break;
        }
    }

    /// <summary>
    /// Focuses <paramref name="target"/> once it is laid out (Arc's <c>focusNext</c> effect). Buttons show their ring when
    /// the last input was the keyboard, as <c>:focus-visible</c> does after a programmatic focus; text fields never select.
    /// </summary>
    private void FocusSoon(Control? target, bool? keyboard = null)
    {
        if (target is null)
        {
            return;
        }
        var method = (keyboard ?? _keyboardModality) ? NavigationMethod.Tab : NavigationMethod.Unspecified;
        Dispatcher.UIThread.Post(() =>
        {
            if (TopLevel.GetTopLevel(target) is not null && target.IsEffectivelyVisible)
            {
                target.Focus(method);
            }
        }, DispatcherPriority.Loaded);
    }

    private void OnAnyPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_backdrop is null || e.Pointer.Type != PointerType.Mouse || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }
        var point = e.GetPosition(this);
        _backdrop.DriftTo((point.X / Bounds.Width - 0.5) * -2, (point.Y / Bounds.Height - 0.5) * -2);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _backdrop?.Release();
    }
}
