using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

public partial class LoginSplit
{
    private enum ResetState
    {
        Idle,
        Sending,
        Sent
    }

    private static readonly LoginSplitAccount s_sampleAccount = new("Rowan Hale", "rowan@fieldnote.app");

    private LoginSplitStepView? _view;
    private bool _emailAttempted;
    private bool _emailTouched;
    private bool _syncingEmail;
    private CancellationTokenSource? _flow;
    private CancellationTokenSource? _countdown;
    private ResetState _reset;
    private int _resendLeft;
    private Button? _busyButton;
    private Func<LoginSplitAccount, string, CancellationToken, Task<bool>>? _verifyPasswordAsync;

    /// <summary>The pause the flow waits on; tests replace it with a manual clock.</summary>
    internal Func<TimeSpan, CancellationToken, Task> Delay { get; set; } = static (time, token) => Task.Delay(time, token);

    /// <summary>The step view on show (tests read it).</summary>
    internal LoginSplitStepView? CurrentView => _view;

    /// <summary>Seconds left before another reset link can be asked for (tests read it).</summary>
    internal int ResendLeft => _resendLeft;

    /// <summary>
    /// Checks a password: true signs in, false shows the wrong-password error; throw to show a general error. Null
    /// (default) simulates an 800 ms check against <see cref="DemoPassword"/> and shows the demo password under the field.
    /// </summary>
    public Func<LoginSplitAccount, string, CancellationToken, Task<bool>>? VerifyPasswordAsync
    {
        get => _verifyPasswordAsync;
        set
        {
            _verifyPasswordAsync = value;
            RefreshHint();
        }
    }

    /// <summary>Goes back to an empty email step, cancelling whatever runs.</summary>
    public void Reset()
    {
        CancelFlow();
        CancelCountdown();
        _reset = ResetState.Idle;
        _resendLeft = 0;
        _busyButton = null;
        IsBusy = false;
        _emailAttempted = false;
        _emailTouched = false;
        SetCurrentValue(EmailProperty, string.Empty);
        Account = null;
        SetStep(LoginSplitStep.Email);
        SetStatus(null, announce: false);
        _viewport?.Settle();
        ShowView(CreateEmailView(), 0);
    }

    /// <summary>Signs out from the signed-in step: back to the email step with the account's email filled in.</summary>
    public void SignOut()
    {
        if (Step != LoginSplitStep.Done || Account is not { } account)
        {
            return;
        }
        CancelFlow();
        _emailAttempted = false;
        _emailTouched = false;
        SetCurrentValue(EmailProperty, account.Email);
        Account = null;
        var view = CreateEmailView();
        ShowView(view, -1);
        SetStep(LoginSplitStep.Email);
        FocusLater(view.EmailInput);
        SetStatus(Labels.SignedOut);
        RaiseEvent(new LoginSplitSignedOutEventArgs(SignedOutEvent, account));
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_view is LoginSplitPasswordView view && _reset == ResetState.Sent && _resendLeft > 0 && _countdown is null)
        {
            RunCountdown(view, _resendLeft);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // The countdown ticks only while the block is on screen; it resumes where it stopped.
        CancelCountdown();
    }

    #region Views

    private void RebuildStep()
    {
        if (_viewport is null || (IsBusy && _view is not null))
        {
            return;
        }
        LoginSplitStepView view = Step switch
        {
            LoginSplitStep.Password when Account is { } account => CreatePasswordView(account),
            LoginSplitStep.Signing or LoginSplitStep.Done when Account is { } account => CreateSessionView(account, Step == LoginSplitStep.Done),
            _ => CreateEmailView()
        };
        if (view is LoginSplitEmailView && Step != LoginSplitStep.Email)
        {
            Account = null;
            SetStep(LoginSplitStep.Email);
        }
        ShowView(view, 0);
    }

    private void ShowView(LoginSplitStepView view, int direction)
    {
        if (_view is LoginSplitPasswordView)
        {
            CancelCountdown();
            _reset = ResetState.Idle;
            _resendLeft = 0;
        }
        _view = view;
        _viewport?.Show(view, direction);
    }

    private void SetStep(LoginSplitStep step)
    {
        var old = Step;
        if (old == step)
        {
            return;
        }
        Step = step;
        RaiseEvent(new LoginSplitStepChangedEventArgs(StepChangedEvent, old, step));
    }

    private LoginSplitEmailView CreateEmailView()
    {
        var view = new LoginSplitEmailView(Labels, EffectiveProviders);
        var input = view.EmailInput;
        _syncingEmail = true;
        input.Text = Email;
        _syncingEmail = false;
        // Text property changes arrive at once (TextChanged is posted after the render).
        input.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                OnEmailTextChanged(view);
            }
        };
        input.LostFocus += (_, _) => OnEmailBlur(view);
        input.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                SubmitEmail();
            }
        };
        view.ContinueButton.Click += (_, _) => SubmitEmail();
        foreach (var button in view.ProviderButtons)
        {
            var target = button;
            target.Click += (_, _) => StartProvider((LoginSplitProvider)target.Tag!, target);
        }
        return view;
    }

    private LoginSplitPasswordView CreatePasswordView(LoginSplitAccount account)
    {
        var view = new LoginSplitPasswordView(Labels, account, Hint());
        view.Chip.Click += (_, _) => ChangeEmail();
        view.PasswordField.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                SubmitPassword();
            }
        };
        view.PasswordField.PropertyChanged += (_, e) =>
        {
            // A new character replaces the selected wrong password and clears the error at once.
            if (e.Property == TextBox.TextProperty && view.PasswordField.ErrorMessage is not null)
            {
                view.PasswordField.ErrorMessage = null;
            }
        };
        view.ForgotButton.Click += (_, _) => SendReset();
        view.ResendButton.Click += (_, _) => Resend();
        view.SubmitButton.Click += (_, _) => SubmitPassword();
        return view;
    }

    private LoginSplitSessionView CreateSessionView(LoginSplitAccount account, bool done)
    {
        var view = new LoginSplitSessionView(Labels, account, BrandName);
        view.SignOutButton.Click += (_, _) => SignOut();
        if (done)
        {
            ShowDone(view, account, animate: false);
        }
        return view;
    }

    private string? Hint() =>
        VerifyPasswordAsync is null && Labels.DemoPasswordHint is { } hint ? hint(DemoPassword) : null;

    private void RefreshHint()
    {
        if (_view is LoginSplitPasswordView view)
        {
            view.PasswordField.Description = Hint();
        }
    }

    #endregion

    #region Email step

    private string? EmailError(string? text) =>
        string.IsNullOrWhiteSpace(text) ? Labels.EmailRequired : SiteEmail.IsValid(text) ? null : Labels.EmailInvalid;

    private void OnEmailPropertyChanged(string value)
    {
        if (_syncingEmail || _view is not LoginSplitEmailView view || view.EmailInput.Text == value)
        {
            return;
        }
        _syncingEmail = true;
        view.EmailInput.Text = value;
        _syncingEmail = false;
    }

    private void OnEmailTextChanged(LoginSplitEmailView view)
    {
        if (_view != view)
        {
            return;
        }
        var text = view.EmailInput.Text ?? string.Empty;
        if (!_syncingEmail)
        {
            _syncingEmail = true;
            SetCurrentValue(EmailProperty, text);
            _syncingEmail = false;
        }
        if (_emailAttempted || _emailTouched)
        {
            view.EmailInput.ErrorMessage = EmailError(text);
        }
    }

    private void OnEmailBlur(LoginSplitEmailView view)
    {
        if (_view != view || _emailAttempted)
        {
            return;
        }
        var text = view.EmailInput.Text;
        if (!string.IsNullOrWhiteSpace(text) && !SiteEmail.IsValid(text))
        {
            _emailTouched = true;
            view.EmailInput.ErrorMessage = Labels.EmailInvalid;
        }
    }

    private async void SubmitEmail()
    {
        if (_view is not LoginSplitEmailView view || IsBusy)
        {
            return;
        }
        _emailAttempted = true;
        var input = view.EmailInput;
        var error = EmailError(input.Text);
        input.ErrorMessage = error;
        if (error is not null)
        {
            input.Focus();
            return;
        }
        var email = LoginSplitIdentity.Normalize(input.Text);
        BeginBusy(view.ContinueButton);
        input.IsReadOnly = true;
        SetStatus(Labels.FindingAccount);
        var token = NewFlow();
        LoginSplitAccount account;
        try
        {
            if (LookupAccountAsync is { } lookup)
            {
                account = await lookup(email, token) ?? LoginSplitAccount.FromEmail(email);
            }
            else
            {
                await Delay(Timings.Lookup, token);
                account = LoginSplitIdentity.Resolve(email, EffectiveAccounts);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception)
        {
            if (!token.IsCancellationRequested && _view == view)
            {
                EndBusy(restore: true);
                input.IsReadOnly = false;
                input.ErrorMessage = Labels.SomethingWentWrong;
                SetStatus(Labels.SomethingWentWrong);
            }
            return;
        }
        if (token.IsCancellationRequested || _view != view)
        {
            return;
        }
        EndBusy(restore: false);
        _syncingEmail = true;
        SetCurrentValue(EmailProperty, email);
        _syncingEmail = false;
        Account = account;
        var next = CreatePasswordView(account);
        ShowView(next, 1);
        SetStep(LoginSplitStep.Password);
        FocusLater(next.PasswordField);
        SetStatus(Labels.PasswordFor(account.Email));
    }

    private async void StartProvider(LoginSplitProvider provider, Button button)
    {
        if (_view is not LoginSplitEmailView view || IsBusy)
        {
            return;
        }
        BeginBusy(button);
        view.EmailInput.IsReadOnly = true;
        SetStatus(Labels.OpeningProvider(provider.Name));
        var text = view.EmailInput.Text;
        var email = SiteEmail.IsValid(text) ? LoginSplitIdentity.Normalize(text) : null;
        var token = NewFlow();
        LoginSplitAccount? account = null;
        try
        {
            if (SignInWithProviderAsync is { } signIn)
            {
                account = await signIn(provider, email, token);
            }
            else
            {
                await Delay(Timings.Provider, token);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception)
        {
            if (!token.IsCancellationRequested && _view == view)
            {
                EndBusy(restore: true);
                view.EmailInput.IsReadOnly = false;
                SetStatus(Labels.SomethingWentWrong);
            }
            return;
        }
        if (token.IsCancellationRequested || _view != view)
        {
            return;
        }
        account ??= email is not null ? LoginSplitIdentity.Resolve(email, EffectiveAccounts) : FallbackAccount();
        StartSession(account, LoginSplitMethod.Provider, provider, token);
    }

    private LoginSplitAccount FallbackAccount() => DefaultAccount ?? EffectiveAccounts.FirstOrDefault() ?? s_sampleAccount;

    #endregion

    #region Password step

    private void ChangeEmail()
    {
        if (_view is not LoginSplitPasswordView || IsBusy)
        {
            return;
        }
        _emailAttempted = false;
        _emailTouched = false;
        Account = null;
        var view = CreateEmailView();
        ShowView(view, -1);
        SetStep(LoginSplitStep.Email);
        FocusLater(view.EmailInput);
        SetStatus(Labels.ChangingEmail);
    }

    private async void SubmitPassword()
    {
        if (_view is not LoginSplitPasswordView view || IsBusy || Account is not { } account)
        {
            return;
        }
        var field = view.PasswordField;
        var password = field.Text ?? string.Empty;
        if (password.Length == 0)
        {
            field.ErrorMessage = Labels.PasswordRequired;
            field.Focus();
            return;
        }
        BeginBusy(view.SubmitButton);
        SetStatus(Labels.CheckingPassword);
        var token = NewFlow();
        bool accepted;
        try
        {
            if (VerifyPasswordAsync is { } verify)
            {
                accepted = await verify(account, password, token);
            }
            else
            {
                await Delay(Timings.PasswordCheck, token);
                accepted = string.Equals(password, DemoPassword, StringComparison.Ordinal);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception)
        {
            if (!token.IsCancellationRequested && _view == view)
            {
                EndBusy(restore: true);
                field.ErrorMessage = Labels.SomethingWentWrong;
                SetStatus(Labels.SomethingWentWrong);
            }
            return;
        }
        if (token.IsCancellationRequested || _view != view)
        {
            return;
        }
        if (!accepted)
        {
            EndBusy(restore: true);
            field.ErrorMessage = Labels.PasswordWrong;
            SetStatus(Labels.PasswordIncorrect);
            // Select the wrong password so the next key replaces it (after the field has settled).
            Dispatcher.UIThread.Post(() =>
            {
                if (_view == view)
                {
                    field.Focus();
                    field.SelectAll();
                }
            }, DispatcherPriority.Background);
            return;
        }

        // Accepted: the button turns into a check and narrows to a circle, then the account opens.
        var submit = view.SubmitButton;
        // The check draws itself in; the old label must not blur back in while the spinner leaves (Arc's label layer is
        // still transparent while its label changes), so this one change of the button's label is not cross-faded.
        if (submit.GetVisualDescendants().OfType<ArcContentSwap>().FirstOrDefault() is { } label)
        {
            label.Mode = ContentSwapMode.None;
        }
        submit.Content = new LoginSplitCheck();
        submit.IsLoading = false;
        submit.Padding = new Thickness(0);
        AutomationProperties.SetName(submit, Labels.PasswordAccepted);
        view.SubmitSlot.Collapse(LoginSplitMetrics.SubmitDiameter);
        SetStatus(Labels.PasswordOk);
        try
        {
            await Delay(Timings.AcceptedHold, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        if (token.IsCancellationRequested || _view != view)
        {
            return;
        }
        StartSession(account, LoginSplitMethod.Password, null, token);
    }

    private async void SendReset()
    {
        if (_view is not LoginSplitPasswordView view || _reset != ResetState.Idle || IsBusy || Account is not { } account)
        {
            return;
        }
        _reset = ResetState.Sending;
        view.ForgotLabel.Content = LoginSplitUi.Plain(Labels.SendingLink);
        view.ForgotButton.IsInert = true;
        AutomationProperties.SetName(view.ForgotButton, Labels.SendingLink);
        SetStatus(Labels.SendingResetLink);
        var token = NewFlow();
        try
        {
            if (SendResetLinkAsync is { } send)
            {
                await send(account, token);
            }
            else
            {
                await Delay(Timings.ResetSend, token);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception)
        {
            if (!token.IsCancellationRequested && _view == view)
            {
                _reset = ResetState.Idle;
                view.ForgotLabel.Content = LoginSplitUi.Plain(Labels.ForgotPassword);
                view.ForgotButton.IsInert = false;
                AutomationProperties.SetName(view.ForgotButton, Labels.ForgotPassword);
                SetStatus(Labels.SomethingWentWrong);
            }
            return;
        }
        if (token.IsCancellationRequested || _view != view)
        {
            return;
        }
        _reset = ResetState.Sent;
        view.ForgotLabel.Content = LinkSentLabel();
        AutomationProperties.SetName(view.ForgotButton, Labels.LinkSent);
        view.SetNotice(Labels.ResetSentNotice, account.Email);
        view.Notice.IsOpen = true;
        RunCountdown(view, ResendSeconds);
        SetStatus(Labels.ResetLinkSentTo(account.Email));
        RaiseEvent(new LoginSplitResetLinkSentEventArgs(ResetLinkSentEvent, account, false));
    }

    private Control LinkSentLabel()
    {
        var check = new ArcGlyph { Data = ArcIcons.Check, Size = 14, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(check, ArcGlyph.ForegroundProperty, ArcDesignTokenKind.Success);
        var text = LoginSplitUi.Plain(Labels.LinkSent);
        return new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { check, text } };
    }

    private void Resend()
    {
        if (_view is not LoginSplitPasswordView view || _reset != ResetState.Sent || _resendLeft > 0 || Account is not { } account)
        {
            return;
        }
        RunCountdown(view, ResendSeconds);
        SetStatus(Labels.ResetLinkResent);
        RaiseEvent(new LoginSplitResetLinkSentEventArgs(ResetLinkSentEvent, account, true));
        if (SendResetLinkAsync is { } send)
        {
            ResendInBackground(send, account, view);
        }
    }

    private async void ResendInBackground(Func<LoginSplitAccount, CancellationToken, Task> send, LoginSplitAccount account, LoginSplitPasswordView view)
    {
        try
        {
            await send(account, CancellationToken.None);
        }
        catch (Exception)
        {
            if (_view == view)
            {
                SetStatus(Labels.SomethingWentWrong);
            }
        }
    }

    private async void RunCountdown(LoginSplitPasswordView view, int seconds)
    {
        CancelCountdown();
        var source = new CancellationTokenSource();
        _countdown = source;
        var token = source.Token;
        _resendLeft = seconds;
        UpdateResend(view);
        while (_resendLeft > 0)
        {
            try
            {
                await Delay(TimeSpan.FromSeconds(1), token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            if (token.IsCancellationRequested || _view != view)
            {
                return;
            }
            _resendLeft--;
            UpdateResend(view);
        }
        if (ReferenceEquals(_countdown, source))
        {
            _countdown = null;
        }
    }

    private void UpdateResend(LoginSplitPasswordView view)
    {
        var button = view.ResendButton;
        if (_resendLeft > 0)
        {
            if (view.Countdown is { } countdown)
            {
                countdown.Seconds = _resendLeft;
            }
            else
            {
                countdown = new LoginSplitCountdown { Seconds = _resendLeft, VerticalAlignment = VerticalAlignment.Center };
                countdown.SetValue(Avalonia.Controls.Documents.TextElement.FontFeaturesProperty, new FontFeatureCollection { FontFeature.Parse("tnum") });
                ArcTokens.Bind(countdown, Avalonia.Controls.Documents.TextElement.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
                ArcTokens.Bind(countdown, Avalonia.Controls.Documents.TextElement.FontSizeProperty, ArcDesignTokenKind.TypeXS);
                var words = LoginSplitUi.Plain(Labels.ResendIn, small: true);
                view.Countdown = countdown;
                // .3em between the words and the time.
                view.ResendLabel.Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3.6, Children = { words, countdown } };
            }
            button.IsInert = true;
            AutomationProperties.SetName(button, Labels.ResendAvailableIn(_resendLeft));
        }
        else
        {
            view.Countdown = null;
            view.ResendLabel.Content = LoginSplitUi.Plain(Labels.ResendLink, small: true);
            button.IsInert = false;
            AutomationProperties.SetName(button, Labels.ResendLink);
        }
    }

    #endregion

    #region Signing and done

    private async void StartSession(LoginSplitAccount account, LoginSplitMethod method, LoginSplitProvider? provider, CancellationToken token)
    {
        Account = account;
        var view = CreateSessionView(account, done: false);
        ShowView(view, 1);
        SetStep(LoginSplitStep.Signing);
        IsBusy = true;
        _busyButton = null;
        FocusLater(view.Title);
        SetStatus(Labels.SigningInAs(account.Email));

        var timings = Timings;
        var full = ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full;
        var total = full ? timings.Signing : timings.SigningReduced;
        // Under reduced motion the progress line keeps its proportions inside the shorter wait.
        var scale = timings.Signing > TimeSpan.Zero ? total / timings.Signing : 1;
        view.Progress.Play(timings.ProgressDelay * scale, timings.ProgressDuration * scale);
        try
        {
            await Delay(total, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        if (token.IsCancellationRequested || _view != view)
        {
            return;
        }
        _method = method;
        _provider = provider;
        ShowDone(view, account, animate: true);
        SetStep(LoginSplitStep.Done);
        IsBusy = false;
        var methodName = MethodName(method, provider);
        SetStatus(Labels.SignedInWith(methodName));
        var args = new LoginSplitSignedInEventArgs(SignedInEvent, account, method, provider);
        RaiseEvent(args);
        if (SignedInCommand is { } command && command.CanExecute(args))
        {
            command.Execute(args);
        }
    }

    private LoginSplitMethod _method;
    private LoginSplitProvider? _provider;

    private string MethodName(LoginSplitMethod method, LoginSplitProvider? provider) =>
        method == LoginSplitMethod.Password || provider is null ? Labels.PasswordMethod : provider.Name;

    private void ShowDone(LoginSplitSessionView view, LoginSplitAccount account, bool animate)
    {
        var labels = Labels;
        var title = labels.DoneTitle(account.GreetingName);
        view.Title.Content = LoginSplitUi.Heading(title);
        AutomationProperties.SetName(view.Title, title);
        view.Description.Content = LoginSplitUi.Text(labels.DoneDescription(MethodName(_method, _provider)), ArcDesignTokenKind.TextSecondary);
        view.ProgressRow.IsOpen = false;
        view.SignOutRow.IsOpen = true;
        _ = animate;
    }

    #endregion

    #region Helpers

    private void BeginBusy(Button button)
    {
        IsBusy = true;
        _busyButton = button;
        button.IsLoading = true;
    }

    private void EndBusy(bool restore)
    {
        if (restore && _busyButton is { } button)
        {
            button.IsLoading = false;
        }
        _busyButton = null;
        IsBusy = false;
    }

    private CancellationToken NewFlow()
    {
        _flow?.Cancel();
        _flow = new CancellationTokenSource();
        return _flow.Token;
    }

    private void CancelFlow()
    {
        _flow?.Cancel();
        _flow = null;
    }

    private void CancelCountdown()
    {
        _countdown?.Cancel();
        _countdown = null;
    }

    private void SetStatus(string? text, bool announce = true)
    {
        StatusText = text;
        if (_status is not null)
        {
            _status.Content = text is null ? null : StatusLine(text);
        }
        if (announce && text is not null)
        {
            _announcer?.Announce(text);
        }
    }

    private static void FocusLater(Control target)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (TopLevel.GetTopLevel(target) is not null)
            {
                target.Focus();
            }
        }, DispatcherPriority.Background);
    }

    #endregion
}
