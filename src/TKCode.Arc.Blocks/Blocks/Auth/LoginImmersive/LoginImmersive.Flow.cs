using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

public partial class LoginImmersive
{
    /// <summary>What the page is waiting for.</summary>
    internal enum BusyKind
    {
        None,
        Sending,
        LinkSentHold,
        OpeningMail,
        Provider
    }

    private LoginImmersiveBackdrop? _backdrop;
    private LoginImmersiveSwitch? _switch;
    private Border? _card;
    private LoginImmersiveGlass? _glass;
    private LoginImmersiveStepHost? _steps;
    private LoginImmersiveFormView? _form;
    private LoginImmersiveSentView? _sent;
    private LoginImmersiveDoneView? _done;
    private SiteSwap? _note;
    private LoginImmersiveTextButton? _terms;
    private LoginImmersiveTextButton? _privacy;
    private SiteAnnouncer? _announcer;

    private Input? _wiredEmail;
    private Button? _wiredSend;
    private LoginImmersiveTextButton? _wiredEdit;
    private Button? _wiredOpenMail;
    private LoginImmersiveTextButton? _wiredResend;
    private Button? _wiredSignOut;

    private readonly List<(LoginImmersiveProvider Provider, Button Button, TextBlock Name)> _providerButtons = new();
    private readonly List<IDisposable> _providerBindings = new();
    private IReadOnlyList<LoginImmersiveProvider>? _defaultProviders;
    private INotifyCollectionChanged? _observedSource;

    private BusyKind _busy;
    private LoginImmersiveProvider? _busyProvider;
    private bool _attempted;
    private bool _touched;
    private string? _sendError;
    private bool _formShowsLinkSent;
    private int _run;
    private CancellationTokenSource? _cts;
    private int _countdownRun;
    private CancellationTokenSource? _countdownCts;
    private int _resendLeft;
    private bool? _resendWaiting;
    private LoginImmersiveCountdown? _countdown;
    private string? _noteText;
    private bool _syncingEmail;
    private bool _cardCompact;

    /// <summary>Waits between the demo's beats (tests replace it with a manual clock).</summary>
    internal Func<TimeSpan, CancellationToken, Task> Delay { get; set; } = (delay, token) => Task.Delay(delay, token);

    internal BusyKind Busy => _busy;

    internal int ResendLeft => _resendLeft;

    internal string? NoteText => _noteText;

    internal string? EmailError => CurrentEmailError();

    internal LoginImmersiveFormView? FormView => _form;

    internal LoginImmersiveSentView? SentView => _sent;

    internal LoginImmersiveDoneView? DoneView => _done;

    internal LoginImmersiveStepHost? StepHost => _steps;

    internal LoginImmersiveSwitch? ModeSwitch => _switch;

    internal LoginImmersiveBackdrop? Backdrop => _backdrop;

    internal LoginImmersiveGlass? Glass => _glass;

    internal LoginImmersiveTextButton? TermsButton => _terms;

    internal LoginImmersiveTextButton? PrivacyButton => _privacy;

    internal SiteAnnouncer? Announcer => _announcer;

    internal IReadOnlyList<Button> ProviderButtons => _providerButtons.Select(p => p.Button).ToArray();

    internal LoginImmersiveCountdown? Countdown => _countdown;

    /// <summary>The providers shown: <see cref="ProvidersSource"/>, else <see cref="Providers"/>, else the three defaults.</summary>
    internal IReadOnlyList<LoginImmersiveProvider> EffectiveProviders
    {
        get
        {
            if (ProvidersSource is { } source)
            {
                return source.ToArray();
            }
            if (_providers.Count > 0)
            {
                return _providers;
            }
            return _defaultProviders ??= LoginImmersiveProvider.CreateDefaults();
        }
    }

    /// <summary>Whether Open mail app is the demo (no handler and no mail app address).</summary>
    private bool IsMailDemo => OpenMailAppAsync is null && string.IsNullOrWhiteSpace(MailAppUri);

    #region Public methods

    /// <summary>
    /// Returns to an empty form: the field is cleared, errors and the footer note go away, anything in flight is
    /// abandoned and the countdown stops. The mode stays. No event is raised.
    /// </summary>
    public void Reset()
    {
        Settle();
        StopCountdown();
        _attempted = false;
        _touched = false;
        _sendError = null;
        SetCurrentValue(EmailProperty, string.Empty);
        Account = null;
        Provider = null;
        Method = LoginImmersiveMethod.MagicLink;
        SetNote(null);
        GoTo(LoginImmersiveStep.Form);
        SyncAll();
    }

    /// <summary>Signs out from the Done step: back to the form with the address kept, focus in the field, <see cref="SignedOut"/> raised.</summary>
    public void SignOut()
    {
        if (Step != LoginImmersiveStep.Done)
        {
            return;
        }
        Settle();
        StopCountdown();
        _attempted = false;
        _touched = false;
        _sendError = null;
        Account = null;
        Provider = null;
        Method = LoginImmersiveMethod.MagicLink;
        SetNote(null);
        GoTo(LoginImmersiveStep.Form);
        FocusLater(_form?.Email);
        Announce(Labels.SignedOutStatus);
        RaiseEvent(new RoutedEventArgs(SignedOutEvent));
    }

    /// <summary>
    /// Finishes a magic-link sign-in that the app confirmed elsewhere (the person followed the link): shows the Done
    /// step for <paramref name="email"/> (default: the address the link went to) and raises <see cref="SignedIn"/>.
    /// </summary>
    public void CompleteSignIn(string? email = null)
    {
        var address = !string.IsNullOrWhiteSpace(email) ? Normalize(email) : Account ?? (SiteEmail.IsValid(Email) ? Normalize(Email) : null);
        if (address is null || Step == LoginImmersiveStep.Done)
        {
            return;
        }
        Complete(address, LoginImmersiveMethod.MagicLink, null);
    }

    #endregion

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        UnwireAll();
        if (_steps is not null)
        {
            _steps.ViewHidden -= OnViewHidden;
        }
        foreach (var view in new LoginImmersiveView?[] { _form, _sent, _done })
        {
            if (view is not null)
            {
                view.PartsReady -= OnViewPartsReady;
            }
        }
        if (_switch is not null)
        {
            _switch.Click -= OnSwitchClick;
        }
        if (_terms is not null)
        {
            _terms.Click -= OnTermsClick;
        }
        if (_privacy is not null)
        {
            _privacy.Click -= OnPrivacyClick;
        }
        if (_card is not null)
        {
            _card.SizeChanged -= OnCardSizeChanged;
        }

        _backdrop = e.NameScope.Find<LoginImmersiveBackdrop>(BackdropPart);
        _switch = e.NameScope.Find<LoginImmersiveSwitch>(SwitchPart);
        _card = e.NameScope.Find<Border>(CardPart);
        _glass = e.NameScope.Find<LoginImmersiveGlass>(GlassPart);
        _steps = e.NameScope.Find<LoginImmersiveStepHost>(StepsPart);
        _form = e.NameScope.Find<LoginImmersiveFormView>(FormPart);
        _sent = e.NameScope.Find<LoginImmersiveSentView>(SentPart);
        _done = e.NameScope.Find<LoginImmersiveDoneView>(DonePart);
        _note = e.NameScope.Find<SiteSwap>(NotePart);
        _terms = e.NameScope.Find<LoginImmersiveTextButton>(TermsPart);
        _privacy = e.NameScope.Find<LoginImmersiveTextButton>(PrivacyPart);
        _announcer = e.NameScope.Find<SiteAnnouncer>(AnnouncerPart);

        if (_glass is not null)
        {
            _glass.Backdrop = _backdrop;
        }
        if (_steps is not null)
        {
            _steps.ViewHidden += OnViewHidden;
        }
        if (_switch is not null)
        {
            _switch.Click += OnSwitchClick;
        }
        if (_terms is not null)
        {
            _terms.Click += OnTermsClick;
        }
        if (_privacy is not null)
        {
            _privacy.Click += OnPrivacyClick;
        }
        if (_card is not null)
        {
            _card.SizeChanged += OnCardSizeChanged;
        }
        if (_note is not null)
        {
            ConfigureSwap(_note);
            _note.Content = _noteText;
        }
        foreach (var view in new LoginImmersiveView?[] { _form, _sent, _done })
        {
            if (view is not null)
            {
                view.PartsReady += OnViewPartsReady;
                // Apply now (not at the first measure) so the parts exist for the first sync.
                view.ApplyTemplate();
            }
        }
        // A view whose template was already applied raised nothing above: wire once more, never twice.
        UnwireAll();
        WireAll();
        if (_steps is not null && CurrentView() is { } current)
        {
            _steps.Show(current);
        }
        SyncAll();
    }

    /// <summary>
    /// The reference's line swap (footer note, resend label): the new line rises 6px out of a 4px blur in 240 ms on the
    /// enter curve while the old one lifts 4px, blurs 2px and fades in 160 ms (ease-out); reduced motion fades the new
    /// line in and drops the old one at once.
    /// </summary>
    private static void ConfigureSwap(SiteSwap swap)
    {
        swap.EnterOffset = 6;
        swap.EnterBlur = 4;
        swap.EnterDuration = Motion.ArcMotionPresets.Standard;
        swap.ExitOffset = -4;
        swap.ExitBlur = 2;
        swap.ExitDuration = Motion.ArcMotionPresets.Fast;
        swap.ExitEasing = new Avalonia.Animation.Easings.SplineEasing(0, 0, 0.58, 1);
        swap.ReducedExitDuration = TimeSpan.Zero;
    }

    private LoginImmersiveView? CurrentView() => Step switch
    {
        LoginImmersiveStep.Sent => _sent,
        LoginImmersiveStep.Done => _done,
        _ => _form
    };

    private void OnViewPartsReady(object? sender, EventArgs e)
    {
        UnwireAll();
        WireAll();
        SyncAll();
    }

    private void WireAll()
    {
        if (_form is not null)
        {
            _wiredEmail = _form.Email;
            _wiredSend = _form.Send;
            if (_wiredEmail is not null)
            {
                _wiredEmail.TextChanged += OnEmailTextChanged;
                _wiredEmail.LostFocus += OnEmailLostFocus;
                _wiredEmail.AddHandler(KeyDownEvent, OnEmailKeyDown, RoutingStrategies.Tunnel);
                TextInputOptions.SetContentType(_wiredEmail, TextInputContentType.Email);
                TextInputOptions.SetReturnKeyType(_wiredEmail, TextInputReturnKeyType.Send);
                TextInputOptions.SetAutoCapitalization(_wiredEmail, false);
            }
            if (_wiredSend is not null)
            {
                _wiredSend.Click += OnSendClick;
            }
            RebuildProviders();
        }
        if (_sent is not null)
        {
            _wiredEdit = _sent.Edit;
            _wiredOpenMail = _sent.OpenMail;
            _wiredResend = _sent.Resend;
            if (_wiredEdit is not null)
            {
                _wiredEdit.Click += OnEditClick;
            }
            if (_wiredOpenMail is not null)
            {
                _wiredOpenMail.Click += OnOpenMailClick;
            }
            if (_wiredResend is not null)
            {
                _wiredResend.Click += OnResendClick;
            }
            if (_sent.ResendLabel is { } resendLabel)
            {
                ConfigureSwap(resendLabel);
            }
            _resendWaiting = null;
        }
        if (_done is not null)
        {
            _wiredSignOut = _done.SignOut;
            if (_wiredSignOut is not null)
            {
                _wiredSignOut.Click += OnSignOutClick;
            }
        }
    }

    private void UnwireAll()
    {
        if (_wiredEmail is not null)
        {
            _wiredEmail.TextChanged -= OnEmailTextChanged;
            _wiredEmail.LostFocus -= OnEmailLostFocus;
            _wiredEmail.RemoveHandler(KeyDownEvent, OnEmailKeyDown);
        }
        if (_wiredSend is not null)
        {
            _wiredSend.Click -= OnSendClick;
        }
        if (_wiredEdit is not null)
        {
            _wiredEdit.Click -= OnEditClick;
        }
        if (_wiredOpenMail is not null)
        {
            _wiredOpenMail.Click -= OnOpenMailClick;
        }
        if (_wiredResend is not null)
        {
            _wiredResend.Click -= OnResendClick;
        }
        if (_wiredSignOut is not null)
        {
            _wiredSignOut.Click -= OnSignOutClick;
        }
        _wiredEmail = null;
        _wiredSend = null;
        _wiredEdit = null;
        _wiredOpenMail = null;
        _wiredResend = null;
        _wiredSignOut = null;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        SyncProviderFonts();
        if (Step == LoginImmersiveStep.Sent && _resendLeft > 0)
        {
            RunCountdown();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // Abandon what is in flight (its result is ignored) and pause the countdown until the page is back.
        Settle();
        _countdownRun++;
        _countdownCts?.Cancel();
        _countdownCts = null;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == IsFullScreenProperty)
        {
            PseudoClasses.Set(FullScreenPseudoClass, IsFullScreen);
        }
        else if (property == ModeProperty)
        {
            PseudoClasses.Set(SignUpPseudoClass, Mode == LoginImmersiveMode.SignUp);
            SyncHeader();
            SyncForm();
            SyncDone();
            UpdateName();
        }
        else if (property == EmailProperty)
        {
            if (!_syncingEmail)
            {
                _sendError = null;
                SyncForm();
            }
        }
        else if (property == LabelsProperty || property == BrandNameProperty)
        {
            SyncAll();
            if (property == LabelsProperty)
            {
                // The resend label is built from the labels: rebuild it in place.
                SyncResend(fresh: true);
            }
        }
        else if (property == ProvidersSourceProperty)
        {
            if (_observedSource is not null)
            {
                _observedSource.CollectionChanged -= OnProvidersChanged;
            }
            _observedSource = ProvidersSource as INotifyCollectionChanged;
            if (_observedSource is not null)
            {
                _observedSource.CollectionChanged += OnProvidersChanged;
            }
            RebuildProviders();
        }
        else if (property == IsProvidersVisibleProperty)
        {
            SyncForm();
        }
        else if (property == MailAppUriProperty)
        {
            SyncFooterNoteForStep();
        }
    }

    private void OnProvidersChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildProviders();

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        PseudoClasses.Set(CompactPseudoClass, e.NewSize.Width <= CompactWidth);
    }

    private void OnCardSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        // The card's inside (its width less the 1px border on each side) decides the compact steps.
        var compact = e.NewSize.Width - 2 < CardCompactWidth;
        if (compact == _cardCompact)
        {
            return;
        }
        _cardCompact = compact;
        PseudoClasses.Set(CardCompactPseudoClass, compact);
        SyncCompact();
    }

    private void SyncCompact()
    {
        foreach (var view in new LoginImmersiveView?[] { _form, _sent, _done })
        {
            if (view is not null)
            {
                view.IsCompact = _cardCompact;
            }
        }
        foreach (var heading in new[] { _form?.SignInHeading, _form?.SignUpHeading, _sent?.Heading, _done?.Heading })
        {
            if (heading is not null)
            {
                heading.IsCompact = _cardCompact;
            }
        }
        SyncProviderFonts();
    }

    #region Sync

    private void SyncAll()
    {
        PseudoClasses.Set(FullScreenPseudoClass, IsFullScreen);
        PseudoClasses.Set(SignUpPseudoClass, Mode == LoginImmersiveMode.SignUp);
        UpdateName();
        SyncHeader();
        SyncForm();
        SyncSent();
        SyncDone();
        SyncFooter();
        SyncCompact();
    }

    private void UpdateName()
    {
        var labels = Labels;
        AutomationProperties.SetName(this, Mode == LoginImmersiveMode.SignUp ? labels.SignUpTitle(BrandName) : labels.SignInTitle(BrandName));
    }

    private void SyncHeader()
    {
        if (_switch is null)
        {
            return;
        }
        var labels = Labels;
        _switch.SignUpPrompt = labels.SignUpPrompt;
        _switch.SignUpAction = labels.SignUpAction;
        _switch.SignInPrompt = labels.SignInPrompt;
        _switch.SignInAction = labels.SignInAction;
        _switch.IsSignUp = Mode == LoginImmersiveMode.SignUp;
    }

    private string? CurrentEmailError()
    {
        if (_sendError is not null)
        {
            return _sendError;
        }
        if (!_touched && !_attempted)
        {
            return null;
        }
        var text = Email;
        if (string.IsNullOrWhiteSpace(text))
        {
            return Labels.EmailRequired;
        }
        return SiteEmail.IsValid(text) ? null : Labels.EmailIncomplete;
    }

    private void SyncForm()
    {
        if (_form is null)
        {
            return;
        }
        var labels = Labels;
        _form.DividerText = labels.Divider;
        _form.AreProvidersVisible = IsProvidersVisible;
        if (_form.SignInHeading is { } signIn)
        {
            signIn.Title = labels.SignInTitle(BrandName);
            signIn.Description = labels.SignInDescription;
        }
        if (_form.SignUpHeading is { } signUp)
        {
            signUp.Title = labels.SignUpTitle(BrandName);
            signUp.Description = labels.SignUpDescription;
        }
        if (_form.HeadingHost is { } host)
        {
            var heading = Mode == LoginImmersiveMode.SignUp ? _form.SignUpHeading : _form.SignInHeading;
            if (heading is not null)
            {
                host.Show(heading);
            }
        }
        if (_form.Email is { } input)
        {
            input.Label = labels.EmailLabel;
            input.Placeholder = labels.EmailPlaceholder;
            input.IsReadOnly = _busy is BusyKind.Sending or BusyKind.LinkSentHold;
            input.ErrorMessage = CurrentEmailError();
            if ((input.Text ?? string.Empty) != Email)
            {
                _syncingEmail = true;
                input.Text = Email;
                _syncingEmail = false;
            }
        }
        if (_form.Send is { } send)
        {
            send.IsLoading = _busy == BusyKind.Sending;
            var showSent = _busy == BusyKind.LinkSentHold || _formShowsLinkSent;
            if (showSent)
            {
                if (send.Content is not StackPanel)
                {
                    send.Content = LinkSentContent(labels.LinkSent);
                }
            }
            else if (send.Content as string != labels.SendLink)
            {
                send.Content = labels.SendLink;
            }
            AutomationProperties.SetName(send, showSent ? labels.LinkSent : labels.SendLink);
        }
        foreach (var (provider, button, name) in _providerButtons)
        {
            button.IsLoading = _busy == BusyKind.Provider && ReferenceEquals(_busyProvider, provider);
            name.Text = provider.Name;
            AutomationProperties.SetName(button, labels.ProviderButtonName(provider.Name));
        }
        if (_form.Providers is { } row)
        {
            AutomationProperties.SetName(row, labels.ProvidersGroupName);
        }
    }

    /// <summary>The main button's content once the link went out: a check and "Link sent".</summary>
    private static Control LinkSentContent(string text)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        panel.Children.Add(new ArcGlyph { Data = ArcIcons.Check, Size = 16, StrokeThickness = 2, VerticalAlignment = VerticalAlignment.Center });
        panel.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
        return panel;
    }

    private void RebuildProviders()
    {
        foreach (var binding in _providerBindings)
        {
            binding.Dispose();
        }
        _providerBindings.Clear();
        _providerButtons.Clear();
        var row = _form?.Providers;
        if (row is null)
        {
            return;
        }
        row.Children.Clear();
        foreach (var provider in EffectiveProviders)
        {
            var name = new TextBlock { Text = provider.Name, VerticalAlignment = VerticalAlignment.Center, TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis };
            var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
            content.Children.Add(new AuthProviderMark
            {
                // Read at render time, as the provider may change after the row is built.
                Look = () => new AuthProviderLook(AuthProviderKind.None, provider.Mark, provider.MarkBrush, provider.Layers),
                Width = 16,
                Height = 16,
                VerticalAlignment = VerticalAlignment.Center
            });
            content.Children.Add(name);
            var button = new Button
            {
                Variant = ButtonVariant.Secondary,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(8, 0),
                Content = content
            };
            var captured = provider;
            button.Click += (_, _) => _ = SignInWithAsync(captured);
            _providerButtons.Add((provider, button, name));
            row.Children.Add(button);
        }
        SyncProviderFonts();
        SyncForm();
    }

    private void SyncProviderFonts()
    {
        foreach (var binding in _providerBindings)
        {
            binding.Dispose();
        }
        _providerBindings.Clear();
        foreach (var (_, _, name) in _providerButtons)
        {
            // Provider names drop to 12px in a compact card (the buttons keep their height and padding).
            _providerBindings.Add(ArcTokens.Bind(name, TextBlock.FontSizeProperty, _cardCompact ? ArcDesignTokenKind.TypeXS : ArcDesignTokenKind.TypeSM));
        }
    }

    private void SyncSent()
    {
        if (_sent is null)
        {
            return;
        }
        var labels = Labels;
        if (_sent.Heading is { } heading)
        {
            heading.Title = labels.SentTitle;
            heading.Description = labels.SentDescription;
        }
        _sent.Address = Account;
        _sent.EditText = labels.EditEmail;
        _sent.ResendPrompt = labels.ResendPrompt;
        if (_sent.Edit is { } edit)
        {
            AutomationProperties.SetName(edit, labels.EditEmailName(Account ?? string.Empty));
        }
        if (_sent.OpenMail is { } open)
        {
            open.IsLoading = _busy == BusyKind.OpeningMail;
            if (open.Content as string != labels.OpenMailApp)
            {
                open.Content = labels.OpenMailApp;
            }
        }
        SyncResend(fresh: false);
    }

    /// <summary>The resend button: the countdown while it waits, then "Send again"; a fresh label skips the swap.</summary>
    private void SyncResend(bool fresh)
    {
        if (_sent?.Resend is not { } resend)
        {
            return;
        }
        var labels = Labels;
        var waiting = _resendLeft > 0;
        if (fresh || _resendWaiting != waiting || _sent.ResendLabel?.Content is null)
        {
            _resendWaiting = waiting;
            if (_sent.ResendLabel is { } swap)
            {
                if (waiting)
                {
                    var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
                    panel.Children.Add(new TextBlock { Text = labels.ResendCountdown, VerticalAlignment = VerticalAlignment.Center });
                    _countdown = new LoginImmersiveCountdown { Seconds = _resendLeft, VerticalAlignment = VerticalAlignment.Center };
                    panel.Children.Add(_countdown);
                    swap.Content = panel;
                }
                else
                {
                    _countdown = null;
                    swap.Content = new TextBlock { Text = labels.ResendReady, VerticalAlignment = VerticalAlignment.Center };
                }
            }
        }
        if (_countdown is not null && waiting)
        {
            _countdown.Seconds = _resendLeft;
        }
        resend.IsInert = waiting;
        AutomationProperties.SetName(resend, waiting ? labels.ResendWaitingName(_resendLeft) : labels.ResendReady);
    }

    private void SyncDone()
    {
        if (_done is null)
        {
            return;
        }
        var labels = Labels;
        if (_done.Heading is { } heading)
        {
            heading.Title = Mode == LoginImmersiveMode.SignUp ? labels.DoneTitleSignUp : labels.DoneTitle;
            heading.Description = Method == LoginImmersiveMethod.Provider && Provider is { } provider
                ? labels.DoneDescriptionProvider(provider.Name)
                : labels.DoneDescriptionLink;
        }
        _done.AccountLabel = labels.DetailsAccount;
        _done.Account = Account;
        _done.MethodLabel = labels.DetailsMethod;
        _done.Method = Method == LoginImmersiveMethod.Provider && Provider is { } p ? p.Name : labels.MethodMagicLink;
        if (_done.SignOut is { } signOut && signOut.Content as string != labels.SignOut)
        {
            signOut.Content = labels.SignOut;
        }
    }

    private void SyncFooter()
    {
        var labels = Labels;
        if (_terms is not null)
        {
            _terms.Content = labels.Terms;
        }
        if (_privacy is not null)
        {
            _privacy.Content = labels.Privacy;
        }
        if (_terms?.Parent is Control legal)
        {
            AutomationProperties.SetName(legal, labels.LegalGroupName);
        }
        if (_note is not null && !Equals(_note.Content, _noteText))
        {
            _note.Content = _noteText;
        }
    }

    private void SetNote(string? text)
    {
        _noteText = string.IsNullOrEmpty(text) ? null : text;
        SyncFooter();
    }

    private void SyncFooterNoteForStep()
    {
        if (Step == LoginImmersiveStep.Sent)
        {
            SetNote(IsMailDemo ? Labels.DemoNote : null);
        }
    }

    #endregion

    #region Flow

    private void Announce(string text) => _announcer?.Announce(text);

    private void SetBusy(BusyKind busy, LoginImmersiveProvider? provider = null)
    {
        _busy = busy;
        _busyProvider = provider;
        IsBusy = busy != BusyKind.None;
        SyncForm();
        SyncSent();
    }

    /// <summary>Starts a new run: whatever the previous one was waiting on is cancelled and its result ignored.</summary>
    private (int Run, CancellationToken Token) BeginRun()
    {
        _run++;
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        return (_run, _cts.Token);
    }

    /// <summary>Any new direction cancels what the previous one was still waiting on.</summary>
    private void Settle()
    {
        _run++;
        _cts?.Cancel();
        _cts = null;
        if (_busy != BusyKind.None)
        {
            SetBusy(BusyKind.None);
        }
    }

    private void GoTo(LoginImmersiveStep step)
    {
        Step = step;
        var view = CurrentView();
        if (step == LoginImmersiveStep.Sent)
        {
            SyncSent();
            // The Sent step mounts fresh: a new countdown label, no roll.
            SyncResend(fresh: true);
        }
        else if (step == LoginImmersiveStep.Done)
        {
            SyncDone();
        }
        if (_steps is not null && view is not null)
        {
            _steps.Show(view);
        }
        if (step == LoginImmersiveStep.Sent)
        {
            _sent?.Envelope?.Replay();
        }
        else if (step == LoginImmersiveStep.Done)
        {
            _done?.Mark?.Replay();
        }
    }

    private static void FocusLater(Control? target)
    {
        if (target is null)
        {
            return;
        }
        Dispatcher.UIThread.Post(() => target.Focus(NavigationMethod.Unspecified), DispatcherPriority.Loaded);
    }

    private void OnViewHidden(object? sender, Control view)
    {
        if (ReferenceEquals(view, _form) && _formShowsLinkSent)
        {
            // The form is out of sight: its button can go back to "Email me a link" without anyone seeing the swap.
            _formShowsLinkSent = false;
            SyncForm();
        }
    }

    private void OnEmailTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_syncingEmail || sender is not Input input)
        {
            return;
        }
        _syncingEmail = true;
        SetCurrentValue(EmailProperty, input.Text ?? string.Empty);
        _syncingEmail = false;
        _sendError = null;
        SyncForm();
    }

    private void OnEmailLostFocus(object? sender, RoutedEventArgs e)
    {
        // A field left with something in it starts checking (an empty one does not complain).
        if (!_touched && !string.IsNullOrWhiteSpace(Email))
        {
            _touched = true;
            SyncForm();
        }
    }

    private void OnEmailKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None)
        {
            e.Handled = true;
            _ = SubmitAsync();
        }
    }

    private void OnSendClick(object? sender, RoutedEventArgs e) => _ = SubmitAsync();

    /// <summary>Email me a link: checks the address, sends the link, shows Link sent, then the Sent step.</summary>
    internal async Task SubmitAsync()
    {
        if (_busy != BusyKind.None || Step != LoginImmersiveStep.Form)
        {
            return;
        }
        _attempted = true;
        _sendError = null;
        if (!SiteEmail.IsValid(Email))
        {
            SyncForm();
            _form?.Email?.Focus(NavigationMethod.Unspecified);
            return;
        }
        var email = Normalize(Email);
        var (run, token) = BeginRun();
        SetBusy(BusyKind.Sending);
        Announce(Labels.SendingStatus);
        try
        {
            if (SendLinkAsync is { } send)
            {
                await send(email, Mode, token);
            }
            else
            {
                await Delay(Timings.SendLink, token);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception)
        {
            if (run != _run)
            {
                return;
            }
            _sendError = Labels.SendFailed;
            SetBusy(BusyKind.None);
            Announce(Labels.SendFailed);
            return;
        }
        if (run != _run)
        {
            return;
        }
        Account = email;
        _formShowsLinkSent = true;
        SetBusy(BusyKind.LinkSentHold);
        Announce(Labels.SentStatus(email));
        RaiseEvent(new LoginImmersiveLinkSentEventArgs(LinkSentEvent, email, false, Mode));
        try
        {
            await Delay(Timings.LinkSentHold, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        if (run != _run)
        {
            return;
        }
        SetBusy(BusyKind.None);
        _attempted = false;
        _touched = false;
        GoTo(LoginImmersiveStep.Sent);
        StartCountdown();
        SetNote(IsMailDemo ? Labels.DemoNote : null);
        FocusLater(_sent?.Heading);
    }

    private void OnEditClick(object? sender, RoutedEventArgs e)
    {
        if (Step != LoginImmersiveStep.Sent)
        {
            return;
        }
        Settle();
        StopCountdown();
        _attempted = false;
        _sendError = null;
        Account = null;
        SetNote(null);
        GoTo(LoginImmersiveStep.Form);
        SyncForm();
        FocusLater(_form?.Email);
        Announce(Labels.EditStatus);
    }

    private void OnOpenMailClick(object? sender, RoutedEventArgs e) => _ = OpenMailAsync();

    /// <summary>Open mail app: the handler, the mail app's address, or the demo (follows the link after a second).</summary>
    internal async Task OpenMailAsync()
    {
        if (_busy != BusyKind.None || Step != LoginImmersiveStep.Sent || Account is not { } account)
        {
            return;
        }
        if (OpenMailAppAsync is null && !string.IsNullOrWhiteSpace(MailAppUri))
        {
            Announce(Labels.OpeningMailStatus);
            var args = new LoginImmersiveNavigateEventArgs(NavigateEvent, LoginImmersiveLink.MailApp, MailAppUri);
            RaiseEvent(args);
            if (!args.Handled)
            {
                ExecuteNavigateCommand(args);
                SiteLinks.TryLaunch(this, MailAppUri);
            }
            return;
        }
        var (run, token) = BeginRun();
        SetBusy(BusyKind.OpeningMail);
        Announce(Labels.OpeningMailStatus);
        var followed = true;
        try
        {
            if (OpenMailAppAsync is { } open)
            {
                followed = await open(account, token);
            }
            else
            {
                await Delay(Timings.OpenMailApp, token);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception)
        {
            followed = false;
        }
        if (run != _run)
        {
            return;
        }
        if (!followed)
        {
            SetBusy(BusyKind.None);
            return;
        }
        Complete(account, LoginImmersiveMethod.MagicLink, null);
    }

    private void OnResendClick(object? sender, RoutedEventArgs e) => _ = ResendAsync();

    /// <summary>Another link: at once (the countdown restarts and the envelope plays again); the handler runs behind it.</summary>
    internal async Task ResendAsync()
    {
        if (_busy != BusyKind.None || Step != LoginImmersiveStep.Sent || _resendLeft > 0 || Account is not { } account)
        {
            return;
        }
        Announce(Labels.ResentStatus);
        StartCountdown();
        _sent?.Envelope?.Replay();
        RaiseEvent(new LoginImmersiveLinkSentEventArgs(LinkSentEvent, account, true, Mode));
        if (SendLinkAsync is not { } send)
        {
            return;
        }
        var run = _run;
        var countdown = _countdownRun;
        try
        {
            await send(account, Mode, _countdownCts?.Token ?? CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            if (run == _run && countdown == _countdownRun && Step == LoginImmersiveStep.Sent)
            {
                // The new link did not go out: say so and let the person try again at once.
                Announce(Labels.SendFailed);
                StopCountdown();
                _resendLeft = 0;
                SyncResend(fresh: false);
            }
        }
    }

    private async Task SignInWithAsync(LoginImmersiveProvider provider)
    {
        if (_busy != BusyKind.None || Step != LoginImmersiveStep.Form)
        {
            return;
        }
        var fieldEmail = SiteEmail.IsValid(Email) ? Normalize(Email) : null;
        var (run, token) = BeginRun();
        SetBusy(BusyKind.Provider, provider);
        Announce(Labels.OpeningProviderStatus(provider.Name));
        string? email;
        try
        {
            if (SignInWithProviderAsync is { } signIn)
            {
                email = await signIn(provider, fieldEmail, token);
            }
            else
            {
                await Delay(Timings.Provider, token);
                email = fieldEmail ?? DefaultEmail;
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception)
        {
            email = null;
        }
        if (run != _run)
        {
            return;
        }
        if (string.IsNullOrWhiteSpace(email))
        {
            SetBusy(BusyKind.None);
            return;
        }
        Complete(Normalize(email), LoginImmersiveMethod.Provider, provider);
    }

    /// <summary>Test hook: presses the provider at <paramref name="index"/>.</summary>
    internal Task SignInWithAsync(int index) => SignInWithAsync(EffectiveProviders[index]);

    private void Complete(string email, LoginImmersiveMethod method, LoginImmersiveProvider? provider)
    {
        Settle();
        StopCountdown();
        Account = email;
        Method = method;
        Provider = provider;
        SetNote(null);
        GoTo(LoginImmersiveStep.Done);
        FocusLater(_done?.Heading);
        Announce(method == LoginImmersiveMethod.Provider && provider is not null
            ? Labels.SignedInProviderStatus(provider.Name)
            : Labels.SignedInLinkStatus);
        var args = new LoginImmersiveSignedInEventArgs(SignedInEvent, email, method, provider, Mode);
        RaiseEvent(args);
        if (SignedInCommand is { } command && command.CanExecute(args))
        {
            command.Execute(args);
        }
    }

    private void OnSignOutClick(object? sender, RoutedEventArgs e) => SignOut();

    private void OnSwitchClick(object? sender, RoutedEventArgs e) => SwitchMode();

    /// <summary>The header switch: flips the mode, back on the form if needed, and says which one is on.</summary>
    internal void SwitchMode()
    {
        Settle();
        StopCountdown();
        _attempted = false;
        _sendError = null;
        var next = Mode == LoginImmersiveMode.SignIn ? LoginImmersiveMode.SignUp : LoginImmersiveMode.SignIn;
        SetCurrentValue(ModeProperty, next);
        if (Step != LoginImmersiveStep.Form)
        {
            Account = null;
            Provider = null;
            Method = LoginImmersiveMethod.MagicLink;
            SetNote(null);
            GoTo(LoginImmersiveStep.Form);
        }
        SyncForm();
        Announce(next == LoginImmersiveMode.SignUp ? Labels.SignUpStatus : Labels.SignInStatus);
    }

    private void OnTermsClick(object? sender, RoutedEventArgs e) => OpenLegal(LoginImmersiveLink.Terms);

    private void OnPrivacyClick(object? sender, RoutedEventArgs e) => OpenLegal(LoginImmersiveLink.Privacy);

    /// <summary>Terms and Privacy: the app's handler, the address, or a note saying where the link goes.</summary>
    internal void OpenLegal(LoginImmersiveLink link)
    {
        var href = link == LoginImmersiveLink.Terms ? TermsHref : PrivacyHref;
        var args = new LoginImmersiveNavigateEventArgs(NavigateEvent, link, href);
        RaiseEvent(args);
        if (args.Handled)
        {
            return;
        }
        ExecuteNavigateCommand(args);
        if (!string.IsNullOrWhiteSpace(href))
        {
            SiteLinks.TryLaunch(this, href);
            return;
        }
        var labels = Labels;
        var note = labels.DocumentNote(link == LoginImmersiveLink.Terms ? labels.TermsDocument : labels.PrivacyDocument);
        SetNote(note);
        Announce(note);
    }

    private void ExecuteNavigateCommand(LoginImmersiveNavigateEventArgs args)
    {
        if (NavigateCommand is { } command && command.CanExecute(args))
        {
            command.Execute(args);
        }
    }

    private void StartCountdown()
    {
        _resendLeft = ResendSeconds;
        SyncResend(fresh: false);
        RunCountdown();
    }

    private async void RunCountdown()
    {
        _countdownCts?.Cancel();
        _countdownCts = new CancellationTokenSource();
        var token = _countdownCts.Token;
        var run = ++_countdownRun;
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
            if (run != _countdownRun)
            {
                return;
            }
            _resendLeft = Math.Max(0, _resendLeft - 1);
            SyncResend(fresh: false);
        }
    }

    private void StopCountdown()
    {
        _countdownRun++;
        _countdownCts?.Cancel();
        _countdownCts = null;
    }

    #endregion
}
