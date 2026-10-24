// Ported from Arc's free sign-in block (MIT, Copyright (c) 2026 Elia Kuratli): the flow of sign-in.tsx (submitEmail,
// applySuggestion, verify, changeCode, resend, changeEmail, signInWith, complete, signOut) on top of the steps.
using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;

namespace TKCode.Arc;

public partial class SignIn
{
    /// <summary>The number of digits in a code (Arc's six).</summary>
    internal const int CodeLength = 6;

    private enum BusyKind
    {
        None,
        Email,
        Code,
        Passkey,
        Provider
    }

    private enum FocusTarget
    {
        None,
        Email,
        Done
    }

    // Arc's domainFixes.
    private static readonly Dictionary<string, string> s_domainFixes = new(StringComparer.Ordinal)
    {
        ["gmial.com"] = "gmail.com",
        ["gamil.com"] = "gmail.com",
        ["gmai.com"] = "gmail.com",
        ["gnail.com"] = "gmail.com",
        ["gmail.co"] = "gmail.com",
        ["hotmial.com"] = "hotmail.com",
        ["outlok.com"] = "outlook.com",
        ["outlook.co"] = "outlook.com",
        ["iclod.com"] = "icloud.com",
        ["icoud.com"] = "icloud.com",
        ["yaho.com"] = "yahoo.com"
    };

    private Border? _card;
    private SignInViewport? _viewport;
    private SignInStepHost? _host;
    private SignInSwap? _statusSwap;
    private TextBlock? _signUpPrompt;
    private SignInTextButton? _signUp;
    private Control? _switch;
    private AuthAnnouncer? _announcer;
    private SignInStepBase? _current;

    private BusyKind _busy;
    private string? _busyProvider;
    private bool _touched;
    private bool _attempted;
    private string _code = string.Empty;
    private string _codeError = string.Empty;
    private string? _sendError;
    private int _resendIn;
    private FocusTarget _focusNext;
    private int _run;
    private CancellationTokenSource? _cts;
    private DispatcherTimer? _timer;
    private bool _syncingEmail;
    private bool _narrow;
    private bool _compact;
    private IReadOnlyList<SignInAccount>? _sampleAccounts;
    private IReadOnlyList<SignInProvider>? _defaultProviders;

    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]{2,}$")]
    private static partial Regex EmailPattern();

    /// <summary>Arc's <c>isEmail</c>: something, an @, a domain with a dot and a two letter ending, on the trimmed text.</summary>
    public static bool IsEmailAddress(string? value) => EmailPattern().IsMatch((value ?? string.Empty).Trim());

    /// <summary>Arc's <c>suggestionFor</c>: the address with a common domain typo fixed ("gmial.com" to "gmail.com"), or "".</summary>
    public static string SuggestEmail(string? value)
    {
        var parts = (value ?? string.Empty).Trim().ToLowerInvariant().Split('@');
        return parts.Length == 2 && parts[0].Length > 0 && parts[1].Length > 0 && s_domainFixes.TryGetValue(parts[1], out var fix)
            ? parts[0] + "@" + fix
            : string.Empty;
    }

    /// <summary>Waits for the simulated sends and checks (tests replace it).</summary>
    internal Func<TimeSpan, CancellationToken, Task> Delay { get; set; } = static (time, token) => Task.Delay(time, token);

    /// <summary>The step control showing (tests).</summary>
    internal SignInStepBase? CurrentStep => _current;

    internal SignInStepHost? StepHost => _host;

    internal SignInViewport? Viewport => _viewport;

    internal SignInSwap? StatusSwap => _statusSwap;

    internal AuthAnnouncer? Announcer => _announcer;

    internal SignInTextButton? SignUpButton => _signUp;

    internal Border? Card => _card;

    /// <summary>The code in the slots (tests).</summary>
    internal string CodeText => _code;

    /// <summary>The error under the code, or "" (tests).</summary>
    internal string CodeError => _codeError;

    /// <summary>The error under the email field, or "" (tests).</summary>
    internal string EmailError => CurrentEmailError();

    /// <summary>The typo suggestion showing, or "" (tests).</summary>
    internal string Suggestion => CurrentSuggestion();

    /// <summary>Seconds before the code can be sent again (tests).</summary>
    internal int ResendIn => _resendIn;

    internal bool IsNarrow => _narrow;

    internal bool IsCompact => _compact;

    // ---- public methods ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Signs out (what the signed in step's button does): back to the email step with the account's address in the field,
    /// selected. Does nothing unless signed in.
    /// </summary>
    public void SignOut()
    {
        if (Step != SignInStep.Done || Account is not { } account)
        {
            return;
        }
        SetCurrentValue(EmailProperty, account.Email);
        _touched = false;
        _attempted = false;
        _sendError = null;
        _code = string.Empty;
        GoTo(SignInStep.Email, -1, FocusTarget.Email);
        SetStatus(SignedOutStatus);
        RaiseEvent(new SignInSignedOutEventArgs(SignedOutEvent, account));
    }

    /// <summary>
    /// Back to an empty email step: a send or check in flight is abandoned (its token cancelled), the field, code and errors
    /// are cleared and the status line empties without an announcement.
    /// </summary>
    public void Reset()
    {
        CancelPending();
        _busy = BusyKind.None;
        _busyProvider = null;
        IsBusy = false;
        PseudoClasses.Set(BusyPseudoClass, false);
        _touched = false;
        _attempted = false;
        _sendError = null;
        _code = string.Empty;
        _codeError = string.Empty;
        StopTimer();
        SetCurrentValue(EmailProperty, string.Empty);
        Account = null;
        Method = null;
        if (Step != SignInStep.Email)
        {
            GoTo(SignInStep.Email, -1, FocusTarget.None);
        }
        Status = string.Empty;
        _announcer?.Announce(null);
        _statusSwap?.Show(string.Empty, () => StatusText(string.Empty));
        SyncAll();
    }

    // ---- template -----------------------------------------------------------------------------------------------------

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_signUp is not null)
        {
            _signUp.Click -= OnSignUpClick;
        }
        _card = e.NameScope.Find<Border>(CardPart);
        _viewport = e.NameScope.Find<SignInViewport>(ViewportPart);
        _host = e.NameScope.Find<SignInStepHost>(StepsPart);
        _statusSwap = e.NameScope.Find<SignInSwap>(StatusPart);
        _signUpPrompt = e.NameScope.Find<TextBlock>(SignUpPromptPart);
        _signUp = e.NameScope.Find<SignInTextButton>(SignUpPart);
        _switch = e.NameScope.Find<Control>(SwitchPart);
        _announcer = e.NameScope.Find<AuthAnnouncer>(AnnouncerPart);
        if (_signUp is not null)
        {
            _signUp.Click += OnSignUpClick;
        }
        _current = null;
        if (_host is not null)
        {
            _host.Children.Clear();
            ShowStep(Step, 1, animate: false);
        }
        _statusSwap?.Show(Status, () => StatusText(Status));
        SyncFooter();
        UpdateStepPseudoClasses();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property.OwnerType != typeof(SignIn) || change.Property == StepProperty || change.Property == AccountProperty ||
            change.Property == MethodProperty || change.Property == IsBusyProperty || change.Property == StatusProperty)
        {
            return;
        }
        if (change.Property == EmailProperty)
        {
            if (!_syncingEmail)
            {
                _sendError = null;
            }
            SyncEmailStep(_current as SignInEmailStep);
            return;
        }
        SyncAll();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _providers.CollectionChanged += OnProvidersChanged;
        if (Step == SignInStep.Code && _resendIn > 0)
        {
            StartTimer();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _providers.CollectionChanged -= OnProvidersChanged;
        // Arc clears its timers on unmount: a send or check in flight is abandoned.
        CancelPending();
        StopTimer();
        if (_busy != BusyKind.None)
        {
            SetBusy(BusyKind.None, null);
        }
    }

    private void OnProvidersChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) =>
        SyncEmailStep(_current as SignInEmailStep);

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = availableSize.Width;
        var cardMax = _card is { MaxWidth: > 0 and < double.PositiveInfinity } card ? card.MaxWidth : 432;
        var narrow = Math.Min(width, cardMax) <= NarrowWidth;
        var compact = width <= CompactWidth;
        if (narrow != _narrow || compact != _compact)
        {
            _narrow = narrow;
            _compact = compact;
            PseudoClasses.Set(NarrowPseudoClass, narrow);
            PseudoClasses.Set(CompactPseudoClass, compact);
            if (_host is not null)
            {
                foreach (var child in _host.Children)
                {
                    if (child is SignInStepBase step)
                    {
                        step.IsNarrow = narrow;
                    }
                }
            }
            SyncProviders(_current as SignInEmailStep);
        }
        return base.MeasureOverride(availableSize);
    }

    // ---- steps --------------------------------------------------------------------------------------------------------

    private SignInStepBase CreateStep(SignInStep step)
    {
        SignInStepBase control = step switch
        {
            SignInStep.Code => new SignInCodeStep(),
            SignInStep.Done => new SignInDoneStep(),
            _ => new SignInEmailStep()
        };
        control.IsNarrow = _narrow;
        control.PartsReady += OnStepPartsReady;
        return control;
    }

    private void ShowStep(SignInStep step, int direction, bool animate)
    {
        if (_host is null)
        {
            return;
        }
        var control = CreateStep(step);
        _current = control;
        _host.Show(control);
        if (animate)
        {
            EnterStep(control, direction, still: step == SignInStep.Done);
        }
    }

    private void GoTo(SignInStep next, int direction, FocusTarget focus)
    {
        _focusNext = focus;
        if (next != SignInStep.Code)
        {
            StopTimer();
        }
        var previous = Step;
        Step = next;
        UpdateStepPseudoClasses();
        if (_host is null || (previous == next && _current is not null))
        {
            SyncAll();
            return;
        }
        TransitionTo(next, direction);
    }

    private void UpdateStepPseudoClasses()
    {
        PseudoClasses.Set(EmailPseudoClass, Step == SignInStep.Email);
        PseudoClasses.Set(CodePseudoClass, Step == SignInStep.Code);
        PseudoClasses.Set(DonePseudoClass, Step == SignInStep.Done);
    }

    private void OnStepPartsReady(object? sender, EventArgs e)
    {
        switch (sender)
        {
            case SignInEmailStep email:
                WireEmailStep(email);
                SyncEmailStep(email);
                break;
            case SignInCodeStep code:
                WireCodeStep(code);
                SyncCodeStep(code);
                break;
            case SignInDoneStep done:
                done.SignOut.Click += (_, _) => SignOut();
                SyncDoneStep(done);
                OnDonePartsReady(done);
                break;
        }
        if (sender is SignInStepBase step && ReferenceEquals(step, _current) && _focusNext != FocusTarget.None)
        {
            // Arc focuses in an effect after the step renders.
            Dispatcher.UIThread.Post(() => ApplyFocus(step));
        }
    }

    private void ApplyFocus(SignInStepBase step)
    {
        if (!ReferenceEquals(step, _current))
        {
            return;
        }
        var target = _focusNext;
        _focusNext = FocusTarget.None;
        if (target == FocusTarget.Email && step is SignInEmailStep email)
        {
            email.Email.Focus();
            email.Email.SelectAll();
        }
        else if (target == FocusTarget.Done && step is SignInDoneStep done)
        {
            done.Title.Focus();
        }
    }

    private void FocusEmail()
    {
        if (_current is SignInEmailStep { HasParts: true } email)
        {
            email.Email.Focus();
        }
    }

    private void FocusCode()
    {
        if (_current is SignInCodeStep { HasParts: true } code)
        {
            code.Code.FocusSlot(0);
        }
    }

    // ---- wiring -------------------------------------------------------------------------------------------------------

    private void WireEmailStep(SignInEmailStep step)
    {
        var input = step.Email;
        input.TextChanged += (_, _) => OnEmailTextChanged(input.Text);
        input.LostFocus += (_, _) =>
        {
            if (Email.Trim().Length > 0)
            {
                _touched = true;
                SyncEmailState(step);
            }
        };
        input.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                SubmitEmail();
            }
            else if (e.Key == Key.Tab && !e.KeyModifiers.HasFlag(KeyModifiers.Shift) && Email.Trim().Length > 0 && !_touched)
            {
                // The browser blurs before it picks the next stop, so a suggestion that the blur reveals is the next stop.
                _touched = true;
                SyncEmailState(step);
            }
        }, RoutingStrategies.Tunnel);
        TextInputOptions.SetContentType(input, TextInputContentType.Email);
        TextInputOptions.SetAutoCapitalization(input, false);
        TextInputOptions.SetReturnKeyType(input, TextInputReturnKeyType.Go);
        step.SuggestionButton.Click += (_, _) => ApplySuggestion();
        step.Continue.Click += (_, _) => SubmitEmail();
        step.Passkey.Click += (_, _) => SignInWith(SignInMethod.Passkey);
        step.ProviderButtons.Clear();
        step.Providers.Children.Clear();
    }

    private void WireCodeStep(SignInCodeStep step)
    {
        step.Change.Click += (_, _) => ChangeEmail();
        step.Code.ValueChanged += (_, e) =>
        {
            if (e.IsUserEdit)
            {
                ChangeCode(e.NewValue);
            }
        };
        step.Code.Completed += (_, e) => Verify(e.Code);
        step.Code.AddHandler(KeyDownEvent, (_, e) =>
        {
            // Enter submits the form (Arc: onSubmit verifies whatever is typed).
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                Verify(_code);
            }
        }, RoutingStrategies.Tunnel);
        step.Verify.Click += (_, _) => Verify(_code);
        step.Resend.Click += (_, _) => Resend();
        step.Time = null;
    }

    // ---- sync ---------------------------------------------------------------------------------------------------------

    private void SyncAll()
    {
        switch (_current)
        {
            case SignInEmailStep email:
                SyncEmailStep(email);
                break;
            case SignInCodeStep code:
                SyncCodeStep(code);
                break;
            case SignInDoneStep done:
                SyncDoneStep(done);
                break;
        }
        SyncFooter();
    }

    private void SyncFooter()
    {
        if (_signUpPrompt is not null)
        {
            _signUpPrompt.Text = SignUpPrompt;
        }
        if (_signUp is not null)
        {
            _signUp.Text = SignUpLabel;
            AutomationProperties.SetName(_signUp, SignUpLabel);
        }
        if (_switch is not null)
        {
            _switch.IsVisible = IsSignUpVisible;
        }
    }

    private void SyncEmailStep(SignInEmailStep? step)
    {
        if (step is not { HasParts: true })
        {
            return;
        }
        step.Title.Text = Title;
        step.Description.Text = Description;
        step.Email.Label = EmailLabel;
        step.Email.Placeholder = EmailPlaceholder;
        if ((step.Email.Text ?? string.Empty) != Email)
        {
            _syncingEmail = true;
            step.Email.Text = Email;
            _syncingEmail = false;
        }
        SyncEmailState(step);
        step.Continue.Content = ContinueLabel;
        step.DividerText.Text = DividerLabel;
        step.PasskeyText.Text = PasskeyLabel;
        AutomationProperties.SetName(step.Passkey, PasskeyLabel);
        step.Passkey.IsVisible = IsPasskeyVisible;
        SyncProviders(step);
        SyncBusy(step);
    }

    /// <summary>The email error and the typo suggestion (they follow every keystroke once touched or submitted).</summary>
    private void SyncEmailState(SignInEmailStep? step)
    {
        if (step is not { HasParts: true })
        {
            return;
        }
        var error = CurrentEmailError();
        step.Email.ErrorMessage = error.Length > 0 ? error : null;
        var suggestion = CurrentSuggestion();
        var show = suggestion.Length > 0;
        step.Suggestion.IsExpanded = show;
        step.SuggestionBody.Opacity = show ? 1 : 0;
        // The leaving row keeps its last address while it closes (Arc's exit animation).
        if (show)
        {
            var format = SuggestionFormat ?? "{0}";
            var at = format.IndexOf("{0}", StringComparison.Ordinal);
            step.SuggestionBefore.Text = at < 0 ? format : format[..at];
            step.SuggestionAfter.Text = at < 0 ? string.Empty : format[(at + 3)..];
            step.SuggestionButton.Text = suggestion;
            AutomationProperties.SetName(step.SuggestionButton, Fmt(UseSuggestionLabelFormat, suggestion));
        }
        step.SuggestionButton.IsEnabled = show;
    }

    private void SyncProviders(SignInEmailStep? step)
    {
        if (step is not { HasParts: true })
        {
            return;
        }
        var providers = EffectiveProviders();
        var visible = IsProvidersVisible && providers.Count > 0;
        step.Providers.IsVisible = visible;
        step.Providers.Gap = 8;
        AutomationProperties.SetName(step.Providers, SingleSignOnLabel);
        var same = step.ProviderButtons.Count == providers.Count;
        for (var i = 0; same && i < providers.Count; i++)
        {
            same = ReferenceEquals(step.ProviderButtons[i].Provider, providers[i]);
        }
        if (!same)
        {
            step.Providers.Children.Clear();
            step.ProviderButtons.Clear();
            foreach (var provider in providers)
            {
                var parts = CreateProviderButton(provider);
                step.ProviderButtons.Add(parts);
                step.Providers.Children.Add(parts.Button);
            }
        }
        foreach (var parts in step.ProviderButtons)
        {
            var name = parts.Provider.Name;
            parts.Label.Text = name;
            parts.Glyph.Kind = parts.Provider.Mark switch
            {
                SignInProviderMark.Google => AuthProviderKind.Google,
                SignInProviderMark.Apple => AuthProviderKind.Apple,
                SignInProviderMark.GitHub => AuthProviderKind.GitHub,
                _ => AuthProviderKind.None
            };
            parts.Glyph.Icon = parts.Provider.Icon;
            parts.Glyph.IsVisible = parts.Glyph.HasMark;
            // Narrow cards tighten the provider buttons: 6 between mark and name, 12px text (Arc's container query).
            parts.Content.Spacing = _narrow ? 6 : 8;
            if (_narrow)
            {
                parts.Button.FontSize = 12;
            }
            else
            {
                parts.Button.ClearValue(FontSizeProperty);
            }
            AutomationProperties.SetName(parts.Button, Fmt(ProviderLabelFormat, name));
            parts.Button.IsLoading = _busy == BusyKind.Provider && _busyProvider == name;
        }
        step.Divider.IsVisible = IsPasskeyVisible || visible;
        step.Alternatives.IsVisible = IsPasskeyVisible || visible;
    }

    private SignInEmailStep.ProviderParts CreateProviderButton(SignInProvider provider)
    {
        var glyph = new AuthProviderMark { Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetAccessibilityView(glyph, AccessibilityView.Raw);
        var label = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis };
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { glyph, label } };
        var button = new Button
        {
            Variant = ButtonVariant.Secondary,
            Content = content,
            Padding = new Thickness(8, 0),
            MinWidth = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        button.Click += (_, _) => SignInWith(SignInMethod.FromProvider(provider.Name));
        return new SignInEmailStep.ProviderParts(provider, button, content, glyph, label);
    }

    private void SyncCodeStep(SignInCodeStep? step)
    {
        if (step is not { HasParts: true })
        {
            return;
        }
        step.Title.Text = CodeTitle;
        step.Description.Text = CodeDescription;
        var account = Account;
        step.Avatar.DisplayName = account?.Name;
        step.Avatar.Photo = account?.Photo;
        step.IdentityEmail.Text = account?.Email;
        step.Change.Content = ChangeEmailLabel;
        AutomationProperties.SetName(step.Change, ChangeEmailAccessibleName);
        step.Code.Label = CodeLabel;
        step.Code.Description = EffectiveHint();
        if (step.Code.Value != _code)
        {
            step.Code.Value = _code;
        }
        step.Code.ErrorMessage = _codeError.Length > 0 ? _codeError : null;
        step.Code.AutoFocus = true;
        step.Verify.Content = VerifyLabel;
        SyncResend(step);
        SyncBusy(step);
    }

    private void SyncResend(SignInCodeStep step)
    {
        var waiting = _resendIn > 0;
        step.Resend.IsWaiting = waiting;
        AutomationProperties.SetName(step.Resend, waiting ? Fmt(ResendAvailableInFormat, _resendIn) : ResendLabel);
        step.ResendSwap.Show(waiting ? "wait" : "ready", () => waiting ? WaitingLabel(step) : ReadyLabel(step));
        if (step.ResendSwap.Current is TextBlock ready)
        {
            ready.Text = ResendLabel;
        }
        else if (step.ResendSwap.Current is StackPanel panel && panel.Children.Count > 0 && panel.Children[0] is TextBlock words)
        {
            words.Text = ResendCountdownLabel;
        }
        if (waiting)
        {
            step.Time?.SetSeconds(_resendIn);
        }
    }

    private TextBlock ReadyLabel(SignInCodeStep step)
    {
        step.Time = null;
        return new TextBlock { Text = ResendLabel };
    }

    /// <summary>"Resend code in" and the rolling seconds, .3em apart (Arc's <c>.resendLabel</c>).</summary>
    private Control WaitingLabel(SignInCodeStep step)
    {
        var time = new SignInRollingTime();
        time.SetSeconds(_resendIn);
        step.Time = time;
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4.2,
            Children = { new TextBlock { Text = ResendCountdownLabel }, time }
        };
    }

    private void SyncDoneStep(SignInDoneStep? step)
    {
        if (step is not { HasParts: true })
        {
            return;
        }
        var account = Account;
        step.Avatar.DisplayName = account?.Name;
        step.Avatar.Photo = account?.Photo;
        AutomationProperties.SetName(step.Avatar, account?.Name);
        step.Title.Text = Fmt(WelcomeFormat, account?.FirstName ?? string.Empty);
        step.Email.Text = account?.Email;
        step.MethodLabel.Text = SignedInWithLabel;
        step.MethodValue.Text = Method is { } method ? MethodName(method) : string.Empty;
        step.DeviceLabel.Text = DeviceLabel;
        step.DeviceValue.Text = DeviceValue;
        step.SignOut.Content = SignOutLabel;
    }

    private void SyncBusy(SignInStepBase? step)
    {
        switch (step)
        {
            case SignInEmailStep { HasParts: true } email:
                email.Email.IsReadOnly = _busy == BusyKind.Email;
                email.Continue.IsLoading = _busy == BusyKind.Email;
                email.Passkey.IsLoading = _busy == BusyKind.Passkey;
                foreach (var parts in email.ProviderButtons)
                {
                    parts.Button.IsLoading = _busy == BusyKind.Provider && _busyProvider == parts.Provider.Name;
                }
                break;
            case SignInCodeStep { HasParts: true } code:
                code.Code.IsReadOnly = _busy == BusyKind.Code;
                code.Verify.IsLoading = _busy == BusyKind.Code;
                break;
        }
    }

    // ---- Arc's handlers -----------------------------------------------------------------------------------------------

    private void OnEmailTextChanged(string? text)
    {
        if (_syncingEmail)
        {
            return;
        }
        _syncingEmail = true;
        SetCurrentValue(EmailProperty, text ?? string.Empty);
        _syncingEmail = false;
        _sendError = null;
        SyncEmailState(_current as SignInEmailStep);
    }

    private string CurrentEmailError()
    {
        if ((_touched || _attempted) && !IsEmailAddress(Email))
        {
            return Email.Trim().Length > 0 ? InvalidEmailMessage : EmptyEmailMessage;
        }
        return _sendError ?? string.Empty;
    }

    private string CurrentSuggestion() => _touched || _attempted ? SuggestEmail(Email) : string.Empty;

    /// <summary>Continue (Arc's <c>submitEmail</c>).</summary>
    internal async void SubmitEmail()
    {
        if (_busy != BusyKind.None)
        {
            return;
        }
        _attempted = true;
        SyncEmailState(_current as SignInEmailStep);
        if (!IsEmailAddress(Email))
        {
            FocusEmail();
            return;
        }
        SetBusy(BusyKind.Email, null);
        SetStatus(SendingStatus);
        var run = _run;
        var token = NewToken();
        var address = Email.Trim();
        SignInAccount? account;
        try
        {
            if (SendCodeAsync is { } send)
            {
                account = await send(address, token);
            }
            else
            {
                await Delay(TimeSpan.FromMilliseconds(800), token);
                account = null;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !token.IsCancellationRequested)
        {
            if (run != _run)
            {
                return;
            }
            SetBusy(BusyKind.None, null);
            _sendError = SendFailedMessage;
            SyncEmailState(_current as SignInEmailStep);
            SetStatus(SendFailedMessage);
            FocusEmail();
            return;
        }
        catch (OperationCanceledException)
        {
            return;
        }
        if (run != _run)
        {
            return;
        }
        SetBusy(BusyKind.None, null);
        Account = account ?? SignInAccount.ForEmail(address, EffectiveAccounts(), FallbackName);
        _code = string.Empty;
        _codeError = string.Empty;
        _resendIn = ResendSeconds;
        GoTo(SignInStep.Code, 1, FocusTarget.None);
        SetStatus(CodeSentStatus);
        StartTimer();
    }

    /// <summary>The typo suggestion's button (Arc's <c>applySuggestion</c>).</summary>
    internal void ApplySuggestion()
    {
        var suggestion = CurrentSuggestion();
        if (suggestion.Length == 0)
        {
            return;
        }
        SetCurrentValue(EmailProperty, suggestion);
        SetStatus(Fmt(EmailChangedStatusFormat, suggestion));
        FocusEmail();
    }

    /// <summary>Verify, Enter or a complete code (Arc's <c>verify</c>).</summary>
    internal async void Verify(string value)
    {
        if (_busy != BusyKind.None || Step != SignInStep.Code)
        {
            return;
        }
        value ??= string.Empty;
        if (value.Length < CodeLength)
        {
            _codeError = IncompleteCodeMessage;
            SyncCodeStep(_current as SignInCodeStep);
            FocusCode();
            return;
        }
        var account = Account ?? SignInAccount.ForEmail(Email, EffectiveAccounts(), FallbackName);
        SetBusy(BusyKind.Code, null);
        SetStatus(CheckingStatus);
        var run = _run;
        var token = NewToken();
        bool ok;
        try
        {
            if (VerifyCodeAsync is { } verify)
            {
                ok = await verify(account, value, token);
            }
            else
            {
                await Delay(TimeSpan.FromMilliseconds(700), token);
                ok = value == DemoCode;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !token.IsCancellationRequested)
        {
            if (run != _run)
            {
                return;
            }
            Reject(VerifyFailedMessage, VerifyFailedMessage);
            return;
        }
        catch (OperationCanceledException)
        {
            return;
        }
        if (run != _run)
        {
            return;
        }
        if (ok)
        {
            Complete(account, SignInMethod.EmailCode);
            return;
        }
        Reject(CodeMismatchMessage, MismatchStatus);
    }

    private void Reject(string error, string status)
    {
        SetBusy(BusyKind.None, null);
        _code = string.Empty;
        _codeError = error;
        SyncCodeStep(_current as SignInCodeStep);
        SetStatus(status);
        FocusCode();
    }

    /// <summary>An edit in the code (Arc's <c>changeCode</c>; a complete code is checked through <see cref="OtpInput.Completed"/>).</summary>
    private void ChangeCode(string value)
    {
        if (_busy != BusyKind.None)
        {
            return;
        }
        _code = value ?? string.Empty;
        if (_codeError.Length > 0 && _code.Length > 0)
        {
            _codeError = string.Empty;
            if (_current is SignInCodeStep { HasParts: true } step)
            {
                step.Code.ErrorMessage = null;
            }
        }
    }

    /// <summary>The resend button (Arc's <c>resend</c>): nothing while the timer runs.</summary>
    internal async void Resend()
    {
        if (_busy != BusyKind.None || _resendIn > 0 || Step != SignInStep.Code)
        {
            return;
        }
        _resendIn = ResendSeconds;
        _code = string.Empty;
        _codeError = string.Empty;
        SyncCodeStep(_current as SignInCodeStep);
        SetStatus(NewCodeStatus);
        FocusCode();
        StartTimer();
        if (SendCodeAsync is not { } send || Account is not { } account)
        {
            return;
        }
        var run = _run;
        var token = NewToken();
        try
        {
            await send(account.Email, token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !token.IsCancellationRequested)
        {
            if (run == _run && Step == SignInStep.Code)
            {
                _codeError = SendFailedMessage;
                SyncCodeStep(_current as SignInCodeStep);
                SetStatus(SendFailedMessage);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <summary>The account row's Change (Arc's <c>changeEmail</c>).</summary>
    internal void ChangeEmail()
    {
        if (_busy != BusyKind.None || Step != SignInStep.Code)
        {
            return;
        }
        _attempted = false;
        GoTo(SignInStep.Email, -1, FocusTarget.Email);
        SetStatus(EditEmailStatus);
    }

    /// <summary>The passkey and provider buttons (Arc's <c>signInWith</c>).</summary>
    internal async void SignInWith(SignInMethod method)
    {
        if (_busy != BusyKind.None || Step != SignInStep.Email)
        {
            return;
        }
        var passkey = method.Kind == SignInMethodKind.Passkey;
        SetBusy(passkey ? BusyKind.Passkey : BusyKind.Provider, method.Provider);
        SetStatus(passkey ? PasskeyWaitingStatus : Fmt(OpeningProviderStatusFormat, method.Provider ?? string.Empty));
        var run = _run;
        var token = NewToken();
        SignInAccount? account;
        try
        {
            if (SignInWithAsync is { } signIn)
            {
                account = await signIn(method, token);
            }
            else
            {
                await Delay(TimeSpan.FromMilliseconds(passkey ? 1100 : 900), token);
                account = null;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !token.IsCancellationRequested)
        {
            if (run != _run)
            {
                return;
            }
            SetBusy(BusyKind.None, null);
            SetStatus(Fmt(SignInFailedStatusFormat, MethodName(method)));
            return;
        }
        catch (OperationCanceledException)
        {
            return;
        }
        if (run != _run)
        {
            return;
        }
        var known = EffectiveAccounts();
        Complete(account ?? (known.Count > 0 ? known[0] : new SignInAccount(FallbackName, string.Empty)), method);
    }

    private void Complete(SignInAccount account, SignInMethod method)
    {
        SetBusy(BusyKind.None, null);
        Account = account;
        Method = method;
        GoTo(SignInStep.Done, 1, FocusTarget.Done);
        SetStatus(Fmt(SignedInStatusFormat, method.Kind == SignInMethodKind.EmailCode ? EmailCodeStatusMethod : MethodName(method)));
        RaiseEvent(new SignInSignedInEventArgs(SignedInEvent, account, method));
        if (SignedInCommand is { } command && command.CanExecute(account))
        {
            command.Execute(account);
        }
    }

    private void OnSignUpClick(object? sender, RoutedEventArgs e)
    {
        var args = new RoutedEventArgs(SignUpRequestedEvent);
        RaiseEvent(args);
        if (SignUpCommand is { } command && command.CanExecute(null))
        {
            command.Execute(null);
        }
        if (!args.Handled)
        {
            SetStatus(SignUpStatus);
        }
    }

    // ---- helpers ------------------------------------------------------------------------------------------------------

    private void SetBusy(BusyKind kind, string? provider)
    {
        _busy = kind;
        _busyProvider = provider;
        IsBusy = kind != BusyKind.None;
        PseudoClasses.Set(BusyPseudoClass, IsBusy);
        SyncBusy(_current);
    }

    private void SetStatus(string text)
    {
        text ??= string.Empty;
        Status = text;
        _announcer?.Announce(text);
        _statusSwap?.Show(text, () => StatusText(text));
    }

    /// <summary>One status line: single line, ellipsis (Arc's <c>.status &gt; span</c>).</summary>
    private static TextBlock StatusText(string text)
    {
        // The visible line is for sighted people (Arc's aria-hidden span); the hidden live region speaks.
        var block = new TextBlock
        {
            Text = text,
            TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
            TextWrapping = Avalonia.Media.TextWrapping.NoWrap,
            LineHeight = 16.8
        };
        AutomationProperties.SetAccessibilityView(block, AccessibilityView.Raw);
        return block;
    }

    private CancellationToken NewToken()
    {
        _cts ??= new CancellationTokenSource();
        return _cts.Token;
    }

    private void CancelPending()
    {
        _run++;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private void StartTimer()
    {
        if (Step != SignInStep.Code || _resendIn <= 0 || !IsAttachedToTree())
        {
            return;
        }
        if (_timer is null)
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (_, _) => TickResend();
        }
        if (!_timer.IsEnabled)
        {
            _timer.Start();
        }
    }

    private bool IsAttachedToTree() => TopLevel.GetTopLevel(this) is not null;

    private void StopTimer() => _timer?.Stop();

    /// <summary>One second of the resend timer (Arc's effect that counts down while the code step shows).</summary>
    internal void TickResend()
    {
        if (Step != SignInStep.Code || _resendIn <= 0)
        {
            StopTimer();
            return;
        }
        _resendIn = Math.Max(0, _resendIn - 1);
        if (_current is SignInCodeStep { HasParts: true } step)
        {
            SyncResend(step);
        }
        if (_resendIn == 0)
        {
            StopTimer();
        }
    }

    private IReadOnlyList<SignInAccount> EffectiveAccounts()
    {
        if (AccountsSource is { } source)
        {
            return source as IReadOnlyList<SignInAccount> ?? source.ToList();
        }
        return _accounts.Count > 0 ? _accounts : _sampleAccounts ??= SignInAccount.CreateSamples();
    }

    private IReadOnlyList<SignInProvider> EffectiveProviders()
    {
        if (ProvidersSource is { } source)
        {
            return source as IReadOnlyList<SignInProvider> ?? source.ToList();
        }
        return _providers.Count > 0 ? _providers : _defaultProviders ??= SignInProvider.CreateDefaults();
    }

    private string? EffectiveHint() => CodeHint ?? (VerifyCodeAsync is null ? Fmt(DemoCodeHintFormat, DemoCode) : null);

    /// <summary>The method's name on the signed in step ("Email code", "Passkey", "GitHub").</summary>
    private string MethodName(SignInMethod method) => method.Kind switch
    {
        SignInMethodKind.EmailCode => EmailCodeMethodLabel,
        SignInMethodKind.Passkey => PasskeyMethodLabel,
        _ => method.Provider ?? string.Empty
    };

    private static string Fmt(string? format, object argument)
    {
        if (string.IsNullOrEmpty(format))
        {
            return string.Empty;
        }
        try
        {
            return string.Format(CultureInfo.CurrentCulture, format, argument);
        }
        catch (FormatException)
        {
            return format;
        }
    }
}
