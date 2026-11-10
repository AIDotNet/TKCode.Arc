using TKCode.Arc.DesignTokens;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// Passwordless sign-in by email link in one card: the person types an address and asks for a link; the card turns into
/// "Check your inbox" with the address, a resend button that waits out <see cref="ResendDelay"/> on a countdown ring, and
/// a way back to change the address; and, when your app opens a stale link, the expired state sends a fresh one in one
/// press. The states swap with a short lift while the card's height follows on a spring. Your app sends the mail from
/// <see cref="LinkRequested"/> or an async <see cref="LinkSender"/> (the button shows a spinner and a failure message).
/// </summary>
[TemplatePart(StagePart, typeof(ArcContentSwap))]
[TemplatePart(EmailPart, typeof(Input))]
[TemplatePart(SendButtonPart, typeof(AvaloniaButton))]
[TemplatePart(ResendButtonPart, typeof(AvaloniaButton))]
[TemplatePart(ChangeButtonPart, typeof(AvaloniaButton))]
[TemplatePart(SendNewButtonPart, typeof(AvaloniaButton))]
[PseudoClasses(NarrowPseudoClass)]
public class MagicLinkSignIn : TemplatedControl
{
    public const string StagePart = "PART_Stage";
    public const string EmailPart = "PART_Email";
    public const string SendButtonPart = "PART_SendButton";
    public const string ResendButtonPart = "PART_ResendButton";
    public const string ChangeButtonPart = "PART_ChangeButton";
    public const string SendNewButtonPart = "PART_SendNewButton";

    /// <summary>Set below <c>NarrowWidth</c>: the card tightens its padding.</summary>
    public const string NarrowPseudoClass = ":narrow";

    internal const string ResendTextPart = "PART_ResendText";
    internal const string ExpiredChangeButtonPart = "PART_ExpiredChangeButton";
    internal const string ErrorPart = "PART_Error";
    internal const string AnnouncerPart = "PART_Announcer";
    internal const string SentDescriptionPart = "PART_SentDescription";

    #region Properties

    /// <summary>The product name in the title ("Sign in to Lumenfield").</summary>
    public static readonly StyledProperty<string> ProductNameProperty =
        AvaloniaProperty.Register<MagicLinkSignIn, string>(nameof(ProductName), "Lumenfield");

    /// <summary>The address in the field (two-way); the link goes here.</summary>
    public static readonly StyledProperty<string?> EmailProperty =
        AvaloniaProperty.Register<MagicLinkSignIn, string?>(nameof(Email), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The state on screen (two-way). Set it to Expired when your app opens a link that is too old.</summary>
    public static readonly StyledProperty<MagicLinkSignInState> StateProperty =
        AvaloniaProperty.Register<MagicLinkSignIn, MagicLinkSignInState>(nameof(State), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>How long the resend button waits after each link (default 30 seconds).</summary>
    public static readonly StyledProperty<TimeSpan> ResendDelayProperty =
        AvaloniaProperty.Register<MagicLinkSignIn, TimeSpan>(nameof(ResendDelay), TimeSpan.FromSeconds(30));

    /// <summary>How long a link works, as told in the text (default 15 minutes).</summary>
    public static readonly StyledProperty<TimeSpan> LinkLifetimeProperty =
        AvaloniaProperty.Register<MagicLinkSignIn, TimeSpan>(nameof(LinkLifetime), TimeSpan.FromMinutes(15));

    /// <summary>Sends the link and reports success; while it runs the button shows a spinner. Null sends nothing itself (handle <see cref="LinkRequested"/>).</summary>
    public static readonly StyledProperty<Func<MagicLinkSignInRequest, Task<bool>>?> LinkSenderProperty =
        AvaloniaProperty.Register<MagicLinkSignIn, Func<MagicLinkSignInRequest, Task<bool>>?>(nameof(LinkSender));

    /// <summary>True while <see cref="LinkSender"/> runs.</summary>
    public static readonly DirectProperty<MagicLinkSignIn, bool> IsSendingProperty =
        AvaloniaProperty.RegisterDirect<MagicLinkSignIn, bool>(nameof(IsSending), o => o.IsSending);

    /// <summary>The time left before the link can be sent again (zero when it can).</summary>
    public static readonly DirectProperty<MagicLinkSignIn, TimeSpan> ResendRemainingProperty =
        AvaloniaProperty.RegisterDirect<MagicLinkSignIn, TimeSpan>(nameof(ResendRemaining), o => o.ResendRemaining);

    /// <summary>The built-in text; replace the whole object to localize the card.</summary>
    public static readonly StyledProperty<MagicLinkSignInLabels> LabelsProperty =
        AvaloniaProperty.Register<MagicLinkSignIn, MagicLinkSignInLabels>(nameof(Labels), new MagicLinkSignInLabels());

    internal static readonly StyledProperty<double> NarrowWidthProperty =
        AvaloniaProperty.Register<MagicLinkSignIn, double>(nameof(NarrowWidth), 360);

    /// <inheritdoc cref="ProductNameProperty"/>
    public string ProductName { get => GetValue(ProductNameProperty); set => SetValue(ProductNameProperty, value); }

    /// <inheritdoc cref="EmailProperty"/>
    public string? Email { get => GetValue(EmailProperty); set => SetValue(EmailProperty, value); }

    /// <inheritdoc cref="StateProperty"/>
    public MagicLinkSignInState State { get => GetValue(StateProperty); set => SetValue(StateProperty, value); }

    /// <inheritdoc cref="ResendDelayProperty"/>
    public TimeSpan ResendDelay { get => GetValue(ResendDelayProperty); set => SetValue(ResendDelayProperty, value); }

    /// <inheritdoc cref="LinkLifetimeProperty"/>
    public TimeSpan LinkLifetime { get => GetValue(LinkLifetimeProperty); set => SetValue(LinkLifetimeProperty, value); }

    /// <inheritdoc cref="LinkSenderProperty"/>
    public Func<MagicLinkSignInRequest, Task<bool>>? LinkSender { get => GetValue(LinkSenderProperty); set => SetValue(LinkSenderProperty, value); }

    private bool _isSending;

    /// <inheritdoc cref="IsSendingProperty"/>
    public bool IsSending
    {
        get => _isSending;
        private set => SetAndRaise(IsSendingProperty, ref _isSending, value);
    }

    private TimeSpan _resendRemaining;

    /// <inheritdoc cref="ResendRemainingProperty"/>
    public TimeSpan ResendRemaining
    {
        get => _resendRemaining;
        private set => SetAndRaise(ResendRemainingProperty, ref _resendRemaining, value);
    }

    /// <inheritdoc cref="LabelsProperty"/>
    public MagicLinkSignInLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    internal double NarrowWidth { get => GetValue(NarrowWidthProperty); set => SetValue(NarrowWidthProperty, value); }

    #endregion

    #region Events

    /// <summary>Raised when a link should be emailed (the first time, on Resend and after an expired link), before <see cref="LinkSender"/> runs.</summary>
    public static readonly RoutedEvent<MagicLinkSignInLinkEventArgs> LinkRequestedEvent =
        RoutedEvent.Register<MagicLinkSignIn, MagicLinkSignInLinkEventArgs>(nameof(LinkRequested), RoutingStrategies.Bubble);

    /// <summary>Raised after the state changes, with the old and the new state.</summary>
    public static readonly RoutedEvent<MagicLinkSignInStateChangedEventArgs> StateChangedEvent =
        RoutedEvent.Register<MagicLinkSignIn, MagicLinkSignInStateChangedEventArgs>(nameof(StateChanged), RoutingStrategies.Bubble);

    /// <inheritdoc cref="LinkRequestedEvent"/>
    public event EventHandler<MagicLinkSignInLinkEventArgs>? LinkRequested
    {
        add => AddHandler(LinkRequestedEvent, value);
        remove => RemoveHandler(LinkRequestedEvent, value);
    }

    /// <inheritdoc cref="StateChangedEvent"/>
    public event EventHandler<MagicLinkSignInStateChangedEventArgs>? StateChanged
    {
        add => AddHandler(StateChangedEvent, value);
        remove => RemoveHandler(StateChangedEvent, value);
    }

    #endregion

    private ArcContentSwap? _stage;
    private Control? _emailView;
    private Control? _sentView;
    private Control? _expiredView;
    private TextBlock? _emailTitle;
    private TextBlock? _emailDescription;
    private Input? _email;
    private AvaloniaButton? _send;
    private TextBlock? _note;
    private TextBlock? _sentTitle;
    private TextBlock? _sentDescription;
    private TextBlock? _sentHint;
    private AvaloniaButton? _resend;
    private ArcRing? _ring;
    private TextBlock? _resendText;
    private AvaloniaButton? _change;
    private TextBlock? _expiredTitle;
    private TextBlock? _expiredDescription;
    private AvaloniaButton? _sendNew;
    private AvaloniaButton? _expiredChange;
    private TextBlock? _error;
    private AuthAnnouncer? _announcer;
    private Control? _sentGlyph;
    private DispatcherTimer? _timer;
    private DateTimeOffset _sentAt;
    private bool _emailTouched;
    private int _sendGeneration;
    private string? _sentTo;
    private MotionValue<double>? _flight;
    private readonly TranslateTransform _flightShift = new();

    /// <summary>The clock the countdown reads (tests replace it).</summary>
    internal Func<DateTimeOffset> Clock { get; set; } = static () => DateTimeOffset.UtcNow;

    static MagicLinkSignIn()
    {
        FocusableProperty.OverrideDefaultValue<MagicLinkSignIn>(false);
    }

    public MagicLinkSignIn()
    {
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    #region Public methods

    /// <summary>Sends a link to <see cref="Email"/> as the send button does; returns false when the address is not valid or a send runs.</summary>
    public Task<bool> RequestLinkAsync() => SendAsync(isResend: State != MagicLinkSignInState.Email);

    /// <summary>Shows the expired state (for example when your app opens a link that is too old).</summary>
    public void ShowExpired() => SetCurrentValue(StateProperty, MagicLinkSignInState.Expired);

    /// <summary>Returns to the email field, keeping the address, and puts the caret in it.</summary>
    public void ChangeEmail()
    {
        SetCurrentValue(StateProperty, MagicLinkSignInState.Email);
        Dispatcher.UIThread.Post(() =>
        {
            if (State == MagicLinkSignInState.Email && _email is not null)
            {
                _email.Focus();
                _email.SelectAll();
            }
        }, DispatcherPriority.Loaded);
    }

    /// <summary>True for an address with a name, an @ and a domain with a dot, and no spaces.</summary>
    public static bool IsValidEmail(string? email)
    {
        var text = email?.Trim();
        if (string.IsNullOrEmpty(text) || text.Contains(' '))
        {
            return false;
        }
        var at = text.IndexOf('@');
        if (at <= 0 || at != text.LastIndexOf('@'))
        {
            return false;
        }
        var dot = text.LastIndexOf('.');
        return dot > at + 1 && dot < text.Length - 1;
    }

    #endregion

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        Detach();
        base.OnApplyTemplate(e);
        _stage = e.NameScope.Find<ArcContentSwap>(StagePart);
        _emailView = e.NameScope.Find<Control>("PART_EmailView");
        _sentView = e.NameScope.Find<Control>("PART_SentView");
        _expiredView = e.NameScope.Find<Control>("PART_ExpiredView");
        _emailTitle = e.NameScope.Find<TextBlock>("PART_EmailTitle");
        _emailDescription = e.NameScope.Find<TextBlock>("PART_EmailDescription");
        _email = e.NameScope.Find<Input>(EmailPart);
        _send = e.NameScope.Find<AvaloniaButton>(SendButtonPart);
        _note = e.NameScope.Find<TextBlock>("PART_Note");
        _sentTitle = e.NameScope.Find<TextBlock>("PART_SentTitle");
        _sentDescription = e.NameScope.Find<TextBlock>(SentDescriptionPart);
        _sentHint = e.NameScope.Find<TextBlock>("PART_SentHint");
        _resend = e.NameScope.Find<AvaloniaButton>(ResendButtonPart);
        _ring = e.NameScope.Find<ArcRing>("PART_ResendRing");
        _resendText = e.NameScope.Find<TextBlock>(ResendTextPart);
        _change = e.NameScope.Find<AvaloniaButton>(ChangeButtonPart);
        _expiredTitle = e.NameScope.Find<TextBlock>("PART_ExpiredTitle");
        _expiredDescription = e.NameScope.Find<TextBlock>("PART_ExpiredDescription");
        _sendNew = e.NameScope.Find<AvaloniaButton>(SendNewButtonPart);
        _expiredChange = e.NameScope.Find<AvaloniaButton>(ExpiredChangeButtonPart);
        _error = e.NameScope.Find<TextBlock>(ErrorPart);
        _announcer = e.NameScope.Find<AuthAnnouncer>(AnnouncerPart);
        _sentGlyph = e.NameScope.Find<Control>("PART_SentGlyph");

        if (_email is not null)
        {
            _email.Text = Email;
            _email.PropertyChanged += OnEmailPropertyChanged;
            _email.LostFocus += OnEmailLostFocus;
        }
        if (_send is not null)
        {
            _send.Click += OnSendClick;
        }
        if (_resend is not null)
        {
            _resend.Click += OnResendClick;
        }
        if (_change is not null)
        {
            _change.Click += OnChangeClick;
        }
        if (_sendNew is not null)
        {
            _sendNew.Click += OnSendNewClick;
        }
        if (_expiredChange is not null)
        {
            _expiredChange.Click += OnChangeClick;
        }
        if (_sentGlyph is not null)
        {
            _sentGlyph.RenderTransform = _flightShift;
        }
        UpdateTexts();
        ShowState(State, animate: false);
        UpdateResend();
    }

    private void Detach()
    {
        if (_email is not null)
        {
            _email.PropertyChanged -= OnEmailPropertyChanged;
            _email.LostFocus -= OnEmailLostFocus;
        }
        if (_send is not null)
        {
            _send.Click -= OnSendClick;
        }
        if (_resend is not null)
        {
            _resend.Click -= OnResendClick;
        }
        if (_change is not null)
        {
            _change.Click -= OnChangeClick;
        }
        if (_sendNew is not null)
        {
            _sendNew.Click -= OnSendNewClick;
        }
        if (_expiredChange is not null)
        {
            _expiredChange.Click -= OnChangeClick;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StateProperty)
        {
            var (old, state) = change.GetOldAndNewValue<MagicLinkSignInState>();
            _sendGeneration++;
            if (IsSending)
            {
                IsSending = false;
            }
            ShowError(null);
            UpdateTexts();
            ShowState(state, animate: true);
            UpdateResend();
            if (state == MagicLinkSignInState.Sent)
            {
                PlayFlight();
            }
            _announcer?.Announce(TitleFor(state)?.Text);
            FocusState(state);
            RaiseEvent(new MagicLinkSignInStateChangedEventArgs(StateChangedEvent, old, state));
        }
        else if (change.Property == EmailProperty)
        {
            if (_email is not null && _email.Text != Email)
            {
                _email.Text = Email;
            }
            UpdateEmailError();
        }
        else if (change.Property == LabelsProperty || change.Property == ProductNameProperty || change.Property == LinkLifetimeProperty)
        {
            UpdateTexts();
            UpdateResend();
        }
        else if (change.Property == BoundsProperty || change.Property == NarrowWidthProperty)
        {
            PseudoClasses.Set(NarrowPseudoClass, Bounds.Width > 0 && Bounds.Width < NarrowWidth);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer?.Stop();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateResend();
    }

    #endregion

    #region Content

    private int LifetimeMinutes => Math.Max(1, (int)Math.Round(LinkLifetime.TotalMinutes));

    private void UpdateTexts()
    {
        var labels = Labels;
        SetText(_emailTitle, labels.Title(ProductName));
        SetText(_emailDescription, labels.Description);
        SetText(_note, labels.Note);
        SetText(_sentTitle, labels.SentTitle);
        SetText(_sentHint, labels.SentHint);
        SetText(_expiredTitle, labels.ExpiredTitle);
        var address = _sentTo ?? (IsValidEmail(Email) ? Email!.Trim() : null);
        SetText(_expiredDescription, labels.ExpiredDescription(address, LifetimeMinutes));
        if (_sentDescription is not null)
        {
            // The address stands out in the sentence: the sentence is split around it.
            var email = address ?? string.Empty;
            var sentence = labels.SentDescription(email, LifetimeMinutes);
            var at = email.Length > 0 ? sentence.IndexOf(email, StringComparison.Ordinal) : -1;
            _sentDescription.Inlines ??= new InlineCollection();
            _sentDescription.Inlines.Clear();
            if (at < 0)
            {
                _sentDescription.Inlines.Add(new Run(sentence));
            }
            else
            {
                _sentDescription.Inlines.Add(new Run(sentence[..at]));
                var bold = new Run(email) { FontWeight = FontWeight.Medium };
                ArcTokens.Bind(bold, TextElement.ForegroundProperty, ArcDesignTokenKind.Foreground);
                _sentDescription.Inlines.Add(bold);
                _sentDescription.Inlines.Add(new Run(sentence[(at + email.Length)..]));
            }
        }
        if (_email is not null)
        {
            _email.Label = labels.EmailLabel;
            _email.Placeholder = labels.EmailPlaceholder;
        }
        if (_send is not null)
        {
            _send.Content = labels.Send;
        }
        if (_change is not null)
        {
            _change.Content = labels.ChangeEmail;
        }
        if (_expiredChange is not null)
        {
            _expiredChange.Content = labels.ChangeEmail;
        }
        if (_sendNew is not null)
        {
            _sendNew.Content = labels.SendNew;
        }
        UpdateEmailError();
    }

    private static void SetText(TextBlock? block, string text)
    {
        if (block is not null)
        {
            block.Text = text;
        }
    }

    private void UpdateEmailError()
    {
        if (_email is null)
        {
            return;
        }
        var show = _emailTouched && !IsValidEmail(Email);
        _email.IsInvalid = show;
        _email.ErrorMessage = show ? Labels.EmailInvalid : null;
    }

    private void ShowError(string? message)
    {
        if (State == MagicLinkSignInState.Email && _email is not null)
        {
            _email.IsInvalid = message is not null;
            _email.ErrorMessage = message;
            if (_error is not null)
            {
                _error.IsVisible = false;
            }
            return;
        }
        if (_error is not null)
        {
            _error.Text = message;
            _error.IsVisible = message is not null;
        }
        if (message is not null)
        {
            _announcer?.Announce(message);
        }
    }

    #endregion

    #region States

    private Control? ViewFor(MagicLinkSignInState state) => state switch
    {
        MagicLinkSignInState.Email => _emailView,
        MagicLinkSignInState.Sent => _sentView,
        _ => _expiredView
    };

    private TextBlock? TitleFor(MagicLinkSignInState state) => state switch
    {
        MagicLinkSignInState.Email => _emailTitle,
        MagicLinkSignInState.Sent => _sentTitle,
        _ => _expiredTitle
    };

    private void ShowState(MagicLinkSignInState state, bool animate)
    {
        if (_stage is null || ViewFor(state) is not { } view)
        {
            return;
        }
        if (_stage.Content is MagicLinkSignInHost { Child: { } shown } && ReferenceEquals(shown, view))
        {
            return;
        }
        if (view.Parent is MagicLinkSignInHost previous)
        {
            previous.Child = null;
        }
        else if (view.Parent is Panel store)
        {
            store.Children.Remove(view);
        }
        var host = new MagicLinkSignInHost { Child = view };
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

    private void FocusState(MagicLinkSignInState state)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (State != state)
            {
                return;
            }
            if (state == MagicLinkSignInState.Email)
            {
                _email?.Focus();
            }
            else
            {
                TitleFor(state)?.Focus();
            }
        }, DispatcherPriority.Loaded);
    }

    /// <summary>
    /// The paper plane arrives from the lower left as the sent state opens. Code-driven because it replays on every
    /// arrival of the reused view, which a theme transition cannot express; Reduced and Off show it in place.
    /// </summary>
    private void PlayFlight()
    {
        if (_sentGlyph is null)
        {
            return;
        }
        if (!IsLoaded || !ArcMotion.IsFull(this))
        {
            _flight?.Jump(1);
            _flightShift.X = 0;
            _flightShift.Y = 0;
            _sentGlyph.Opacity = 1;
            return;
        }
        if (_flight is null)
        {
            _flight = new MotionValue<double>(this, 1, MotionVectors.Double);
            _flight.Changed += p =>
            {
                _flightShift.X = (1 - p) * -12;
                _flightShift.Y = (1 - p) * 10;
                if (_sentGlyph is not null)
                {
                    _sentGlyph.Opacity = Math.Clamp(p * 1.6, 0, 1);
                }
            };
        }
        _flight.Jump(0);
        _ = _flight.AnimateTo(1, MotionSpec.Spring(ArcSpring.Smooth));
    }

    private async Task<bool> SendAsync(bool isResend)
    {
        if (IsSending)
        {
            return false;
        }
        if (!isResend)
        {
            _emailTouched = true;
            UpdateEmailError();
            if (!IsValidEmail(Email))
            {
                _email?.Focus();
                return false;
            }
        }
        else if (State == MagicLinkSignInState.Sent && ResendRemaining > TimeSpan.Zero)
        {
            return false;
        }
        var email = isResend && _sentTo is not null ? _sentTo : Email?.Trim();
        if (!IsValidEmail(email))
        {
            ChangeEmail();
            return false;
        }

        var generation = ++_sendGeneration;
        ShowError(null);
        RaiseEvent(new MagicLinkSignInLinkEventArgs(LinkRequestedEvent, email!, isResend));
        var ok = true;
        if (LinkSender is { } sender)
        {
            IsSending = true;
            UpdateBusy();
            try
            {
                ok = await sender(new MagicLinkSignInRequest(email!, isResend));
            }
            catch (Exception)
            {
                ok = false;
            }
            if (generation != _sendGeneration)
            {
                return false;
            }
            IsSending = false;
            UpdateBusy();
        }
        if (!ok)
        {
            ShowError(Labels.SendFailed);
            return false;
        }

        _sentTo = email;
        _sentAt = Clock();
        if (State == MagicLinkSignInState.Sent)
        {
            UpdateTexts();
            UpdateResend();
            _announcer?.Announce(Labels.Resent);
        }
        else
        {
            SetCurrentValue(StateProperty, MagicLinkSignInState.Sent);
        }
        return true;
    }

    private void UpdateBusy()
    {
        var button = State switch
        {
            MagicLinkSignInState.Email => _send,
            MagicLinkSignInState.Sent => _resend,
            _ => _sendNew
        };
        if (button is Button arc)
        {
            arc.IsLoading = IsSending;
        }
        if (_email is not null)
        {
            _email.IsReadOnly = IsSending;
        }
    }

    /// <summary>Brings the countdown up to date: the remaining time, the ring and the resend button's text and state.</summary>
    internal void UpdateResend()
    {
        var total = ResendDelay;
        var remaining = State == MagicLinkSignInState.Sent && _sentTo is not null
            ? total - (Clock() - _sentAt)
            : TimeSpan.Zero;
        if (remaining < TimeSpan.Zero)
        {
            remaining = TimeSpan.Zero;
        }
        ResendRemaining = remaining;
        var waiting = remaining > TimeSpan.Zero;
        if (_resend is not null)
        {
            _resend.IsEnabled = !waiting && !IsSending;
        }
        if (_ring is not null)
        {
            _ring.IsVisible = waiting;
            _ring.Value = total > TimeSpan.Zero ? remaining.TotalSeconds / total.TotalSeconds : 0;
        }
        if (_resendText is not null)
        {
            var seconds = (int)Math.Ceiling(remaining.TotalSeconds);
            _resendText.Text = waiting
                ? Labels.ResendIn(string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}", seconds / 60, seconds % 60))
                : Labels.Resend;
        }
        if (waiting && IsAttachedToVisualTree())
        {
            _timer ??= CreateTimer();
            _timer.Start();
        }
        else
        {
            _timer?.Stop();
        }
    }

    private bool IsAttachedToVisualTree() => TopLevel.GetTopLevel(this) is not null;

    private DispatcherTimer CreateTimer()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        timer.Tick += (_, _) => UpdateResend();
        return timer;
    }

    #endregion

    #region Handlers

    private void OnEmailPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TextBox.TextProperty && _email is not null && _email.Text != Email)
        {
            SetCurrentValue(EmailProperty, _email.Text);
        }
    }

    private void OnEmailLostFocus(object? sender, RoutedEventArgs e)
    {
        // Point out a wrong address once the person leaves a field they typed in, not while they type.
        if (!string.IsNullOrWhiteSpace(Email))
        {
            _emailTouched = true;
            UpdateEmailError();
        }
    }

    private void OnSendClick(object? sender, RoutedEventArgs e) => _ = SendAsync(isResend: false);

    private void OnResendClick(object? sender, RoutedEventArgs e) => _ = SendAsync(isResend: true);

    private void OnSendNewClick(object? sender, RoutedEventArgs e) => _ = SendAsync(isResend: true);

    private void OnChangeClick(object? sender, RoutedEventArgs e) => ChangeEmail();

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || e.KeyModifiers != KeyModifiers.None || e.Source is AvaloniaButton)
        {
            return;
        }
        if (State == MagicLinkSignInState.Email)
        {
            e.Handled = true;
            _ = SendAsync(isResend: false);
        }
        else if (State == MagicLinkSignInState.Expired)
        {
            e.Handled = true;
            _ = SendAsync(isResend: true);
        }
    }

    #endregion
}

/// <summary>Carries one state view of a <see cref="MagicLinkSignIn"/> onto the stage; it always takes the full width it is given.</summary>
internal sealed class MagicLinkSignInHost : Decorator
{
    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        return double.IsInfinity(availableSize.Width) ? size : size.WithWidth(availableSize.Width);
    }
}
