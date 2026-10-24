using System.Collections.Specialized;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Split login page: an email-first sign-in in the left column (brand and "Sign up" above, the steps in the middle, a
/// status line and the legal links below) beside a full-height picture. Continue looks the account up, the password step
/// shows it as a chip, Sign in checks the password, narrows to a check and opens the account with a progress line before
/// greeting the person by name; single sign-on skips the password. "Forgot password?" sends a reset link with a resend
/// countdown. The flow is simulated with the reference preview's timings until you set the asynchronous handlers
/// (<see cref="LookupAccountAsync"/>, <see cref="VerifyPasswordAsync"/>, <see cref="SignInWithProviderAsync"/>,
/// <see cref="SendResetLinkAsync"/>); <see cref="SignedIn"/> hands the account to your app. Breakpoints follow the
/// block's own width: 5:6 columns from 1100, 1:1 from 760, the form alone below 760 (the picture hides), tighter padding
/// below 480.
/// </summary>
[TemplatePart(FramePart, typeof(Border))]
[TemplatePart(LayoutPart, typeof(Grid))]
[TemplatePart(ViewportPart, typeof(LoginSplitViewport))]
[TemplatePart(PhotoSidePart, typeof(Border))]
[TemplatePart(StatusPart, typeof(SiteSwap))]
[TemplatePart(AnnouncerPart, typeof(SiteAnnouncer))]
[TemplatePart(SignUpPart, typeof(LoginSplitTextButton))]
[TemplatePart(TermsPart, typeof(LoginSplitTextButton))]
[TemplatePart(PrivacyPart, typeof(LoginSplitTextButton))]
[TemplatePart(NoAccountPart, typeof(TextBlock))]
[TemplatePart(LegalPart, typeof(Panel))]
[PseudoClasses(FullScreenPseudoClass, WidePseudoClass, SplitPseudoClass, StackedPseudoClass, CompactPseudoClass, BusyPseudoClass)]
public partial class LoginSplit : TemplatedControl
{
    public const string FramePart = "PART_Frame";
    public const string LayoutPart = "PART_Layout";
    public const string ViewportPart = "PART_Viewport";
    public const string PhotoSidePart = "PART_PhotoSide";
    public const string StatusPart = "PART_Status";
    public const string AnnouncerPart = "PART_Announcer";
    public const string SignUpPart = "PART_SignUp";
    public const string TermsPart = "PART_Terms";
    public const string PrivacyPart = "PART_Privacy";
    public const string NoAccountPart = "PART_NoAccount";
    public const string LegalPart = "PART_Legal";

    /// <summary>Set while <see cref="IsFullScreen"/> is true.</summary>
    public const string FullScreenPseudoClass = ":fullscreen";

    /// <summary>Set when the block is at least 1100 wide (columns 5:6).</summary>
    public const string WidePseudoClass = ":wide";

    /// <summary>Set when the block is at least 760 wide (two columns).</summary>
    public const string SplitPseudoClass = ":split";

    /// <summary>Set when the block is narrower than 760 (the form alone; the picture hides).</summary>
    public const string StackedPseudoClass = ":stacked";

    /// <summary>Set when the block is narrower than 480 (tighter header and footer padding).</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>Set while an action runs (looking up, checking, single sign-on, opening the account).</summary>
    public const string BusyPseudoClass = ":busy";

    #region Properties

    /// <summary>
    /// True fills the parent with square corners and no border (a whole screen); false (default) is a framed block with a
    /// 1px border, 26px corners and at least 640 high.
    /// </summary>
    public static readonly StyledProperty<bool> IsFullScreenProperty =
        AvaloniaProperty.Register<LoginSplit, bool>(nameof(IsFullScreen));

    /// <summary>The product name beside the brand mark; also used in the headings and the block's accessible name.</summary>
    public static readonly StyledProperty<string> BrandNameProperty =
        AvaloniaProperty.Register<LoginSplit, string>(nameof(BrandName), "Fieldnote");

    /// <summary>The brand mark (24 × 24 view box, stroked in the accent color); null draws the built-in notebook.</summary>
    public static readonly StyledProperty<Geometry?> BrandMarkProperty =
        AvaloniaProperty.Register<LoginSplit, Geometry?>(nameof(BrandMark));

    /// <summary>The picture of the right column, cropped to cover it; null draws a metal facade in code.</summary>
    public static readonly StyledProperty<IImage?> PhotoProperty =
        AvaloniaProperty.Register<LoginSplit, IImage?>(nameof(Photo));

    /// <summary>The picture's focal point as fractions of its size (default 0.5, 0.58: centered, a little below the middle).</summary>
    public static readonly StyledProperty<Point> PhotoAlignmentProperty =
        AvaloniaProperty.Register<LoginSplit, Point>(nameof(PhotoAlignment), new Point(0.5, 0.58));

    /// <summary>
    /// Accounts to recognize when <see cref="LookupAccountAsync"/> is not set (an email matching one, case-insensitive,
    /// shows its name and picture); when set this wins over <see cref="Accounts"/>.
    /// </summary>
    public static readonly StyledProperty<IEnumerable<LoginSplitAccount>?> AccountsSourceProperty =
        AvaloniaProperty.Register<LoginSplit, IEnumerable<LoginSplitAccount>?>(nameof(AccountsSource));

    /// <summary>The account single sign-on uses when the email field holds no valid address (null: the first account, else a built-in sample).</summary>
    public static readonly StyledProperty<LoginSplitAccount?> DefaultAccountProperty =
        AvaloniaProperty.Register<LoginSplit, LoginSplitAccount?>(nameof(DefaultAccount));

    /// <summary>The password the simulated check accepts while <see cref="VerifyPasswordAsync"/> is not set.</summary>
    public static readonly StyledProperty<string> DemoPasswordProperty =
        AvaloniaProperty.Register<LoginSplit, string>(nameof(DemoPassword), "fieldnote-demo");

    /// <summary>Single sign-on buttons; when set this wins over <see cref="Providers"/>. An empty list hides the divider and the buttons.</summary>
    public static readonly StyledProperty<IEnumerable<LoginSplitProvider>?> ProvidersSourceProperty =
        AvaloniaProperty.Register<LoginSplit, IEnumerable<LoginSplitProvider>?>(nameof(ProvidersSource));

    /// <summary>Address of "Sign up". With an address, an unhandled <see cref="Navigate"/> opens it; without, the status line says where it would go.</summary>
    public static readonly StyledProperty<string?> SignUpHrefProperty =
        AvaloniaProperty.Register<LoginSplit, string?>(nameof(SignUpHref));

    /// <summary>Address of "Terms" (see <see cref="SignUpHref"/>).</summary>
    public static readonly StyledProperty<string?> TermsHrefProperty =
        AvaloniaProperty.Register<LoginSplit, string?>(nameof(TermsHref));

    /// <summary>Address of "Privacy" (see <see cref="SignUpHref"/>).</summary>
    public static readonly StyledProperty<string?> PrivacyHrefProperty =
        AvaloniaProperty.Register<LoginSplit, string?>(nameof(PrivacyHref));

    /// <summary>The text of the email field (two-way). Signing out puts the account's email back here.</summary>
    public static readonly StyledProperty<string> EmailProperty =
        AvaloniaProperty.Register<LoginSplit, string>(nameof(Email), string.Empty, defaultBindingMode: BindingMode.TwoWay,
            coerce: static (_, value) => value ?? string.Empty);

    /// <summary>Seconds before another reset link can be asked for (default 30).</summary>
    public static readonly StyledProperty<int> ResendSecondsProperty =
        AvaloniaProperty.Register<LoginSplit, int>(nameof(ResendSeconds), 30, coerce: static (_, value) => Math.Max(0, value));

    /// <summary>Every built-in text (English defaults).</summary>
    public static readonly StyledProperty<LoginSplitLabels> LabelsProperty =
        AvaloniaProperty.Register<LoginSplit, LoginSplitLabels>(nameof(Labels), new LoginSplitLabels(),
            coerce: static (_, value) => value ?? new LoginSplitLabels());

    /// <summary>Pauses of the simulated flow and the beats of the sign-in sequence.</summary>
    public static readonly StyledProperty<LoginSplitTimings> TimingsProperty =
        AvaloniaProperty.Register<LoginSplit, LoginSplitTimings>(nameof(Timings), new LoginSplitTimings(),
            coerce: static (_, value) => value ?? new LoginSplitTimings());

    /// <summary>Runs with the <see cref="LoginSplitSignedInEventArgs"/> after a sign-in.</summary>
    public static readonly StyledProperty<ICommand?> SignedInCommandProperty =
        AvaloniaProperty.Register<LoginSplit, ICommand?>(nameof(SignedInCommand));

    /// <summary>Runs with the <see cref="LoginSplitLink"/> when a link is used.</summary>
    public static readonly StyledProperty<ICommand?> NavigateCommandProperty =
        AvaloniaProperty.Register<LoginSplit, ICommand?>(nameof(NavigateCommand));

    /// <summary>The step on show.</summary>
    public static readonly DirectProperty<LoginSplit, LoginSplitStep> StepProperty =
        AvaloniaProperty.RegisterDirect<LoginSplit, LoginSplitStep>(nameof(Step), o => o.Step);

    /// <summary>The account being signed in or signed in; null on the email step.</summary>
    public static readonly DirectProperty<LoginSplit, LoginSplitAccount?> AccountProperty =
        AvaloniaProperty.RegisterDirect<LoginSplit, LoginSplitAccount?>(nameof(Account), o => o.Account);

    /// <summary>True while an action runs.</summary>
    public static readonly DirectProperty<LoginSplit, bool> IsBusyProperty =
        AvaloniaProperty.RegisterDirect<LoginSplit, bool>(nameof(IsBusy), o => o.IsBusy);

    /// <summary>The visible status line (also announced politely); null when empty.</summary>
    public static readonly DirectProperty<LoginSplit, string?> StatusTextProperty =
        AvaloniaProperty.RegisterDirect<LoginSplit, string?>(nameof(StatusText), o => o.StatusText);

    /// <inheritdoc cref="IsFullScreenProperty"/>
    public bool IsFullScreen { get => GetValue(IsFullScreenProperty); set => SetValue(IsFullScreenProperty, value); }

    /// <inheritdoc cref="BrandNameProperty"/>
    public string BrandName { get => GetValue(BrandNameProperty); set => SetValue(BrandNameProperty, value); }

    /// <inheritdoc cref="BrandMarkProperty"/>
    public Geometry? BrandMark { get => GetValue(BrandMarkProperty); set => SetValue(BrandMarkProperty, value); }

    /// <inheritdoc cref="PhotoProperty"/>
    public IImage? Photo { get => GetValue(PhotoProperty); set => SetValue(PhotoProperty, value); }

    /// <inheritdoc cref="PhotoAlignmentProperty"/>
    public Point PhotoAlignment { get => GetValue(PhotoAlignmentProperty); set => SetValue(PhotoAlignmentProperty, value); }

    /// <summary>Accounts to recognize when <see cref="LookupAccountAsync"/> is not set (see <see cref="AccountsSource"/>).</summary>
    public AvaloniaList<LoginSplitAccount> Accounts { get; } = new();

    /// <inheritdoc cref="AccountsSourceProperty"/>
    public IEnumerable<LoginSplitAccount>? AccountsSource { get => GetValue(AccountsSourceProperty); set => SetValue(AccountsSourceProperty, value); }

    /// <inheritdoc cref="DefaultAccountProperty"/>
    public LoginSplitAccount? DefaultAccount { get => GetValue(DefaultAccountProperty); set => SetValue(DefaultAccountProperty, value); }

    /// <inheritdoc cref="DemoPasswordProperty"/>
    public string DemoPassword { get => GetValue(DemoPasswordProperty); set => SetValue(DemoPasswordProperty, value); }

    /// <summary>Single sign-on buttons, one equal column each (Google, Apple and GitHub by default).</summary>
    public AvaloniaList<LoginSplitProvider> Providers { get; } = new() { LoginSplitProvider.Google(), LoginSplitProvider.Apple(), LoginSplitProvider.GitHub() };

    /// <inheritdoc cref="ProvidersSourceProperty"/>
    public IEnumerable<LoginSplitProvider>? ProvidersSource { get => GetValue(ProvidersSourceProperty); set => SetValue(ProvidersSourceProperty, value); }

    /// <inheritdoc cref="SignUpHrefProperty"/>
    public string? SignUpHref { get => GetValue(SignUpHrefProperty); set => SetValue(SignUpHrefProperty, value); }

    /// <inheritdoc cref="TermsHrefProperty"/>
    public string? TermsHref { get => GetValue(TermsHrefProperty); set => SetValue(TermsHrefProperty, value); }

    /// <inheritdoc cref="PrivacyHrefProperty"/>
    public string? PrivacyHref { get => GetValue(PrivacyHrefProperty); set => SetValue(PrivacyHrefProperty, value); }

    /// <inheritdoc cref="EmailProperty"/>
    public string Email { get => GetValue(EmailProperty); set => SetValue(EmailProperty, value); }

    /// <inheritdoc cref="ResendSecondsProperty"/>
    public int ResendSeconds { get => GetValue(ResendSecondsProperty); set => SetValue(ResendSecondsProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public LoginSplitLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="TimingsProperty"/>
    public LoginSplitTimings Timings { get => GetValue(TimingsProperty); set => SetValue(TimingsProperty, value); }

    /// <inheritdoc cref="SignedInCommandProperty"/>
    public ICommand? SignedInCommand { get => GetValue(SignedInCommandProperty); set => SetValue(SignedInCommandProperty, value); }

    /// <inheritdoc cref="NavigateCommandProperty"/>
    public ICommand? NavigateCommand { get => GetValue(NavigateCommandProperty); set => SetValue(NavigateCommandProperty, value); }

    /// <summary>
    /// Looks an address up (trimmed and lower-cased). Return the account, or null to use one derived from the address
    /// ("ada.lovelace@…" → "Ada Lovelace"); throw to show an error. Null (default) simulates a 650 ms lookup in
    /// <see cref="Accounts"/>.
    /// </summary>
    public Func<string, CancellationToken, Task<LoginSplitAccount?>>? LookupAccountAsync { get; set; }

    /// <summary>
    /// Runs single sign-on with a provider and the email field's address (null when it holds none). Return the account,
    /// or null to use the address's account or the default one; throw to stay on the email step with an error. Null
    /// (default) simulates a 1 s round trip.
    /// </summary>
    public Func<LoginSplitProvider, string?, CancellationToken, Task<LoginSplitAccount?>>? SignInWithProviderAsync { get; set; }

    /// <summary>Sends a password reset link; throw to show an error. Null (default) simulates 900 ms.</summary>
    public Func<LoginSplitAccount, CancellationToken, Task>? SendResetLinkAsync { get; set; }

    private LoginSplitStep _step;

    /// <inheritdoc cref="StepProperty"/>
    public LoginSplitStep Step
    {
        get => _step;
        private set => SetAndRaise(StepProperty, ref _step, value);
    }

    private LoginSplitAccount? _account;

    /// <inheritdoc cref="AccountProperty"/>
    public LoginSplitAccount? Account
    {
        get => _account;
        private set => SetAndRaise(AccountProperty, ref _account, value);
    }

    private bool _isBusy;

    /// <inheritdoc cref="IsBusyProperty"/>
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetAndRaise(IsBusyProperty, ref _isBusy, value))
            {
                PseudoClasses.Set(BusyPseudoClass, value);
            }
        }
    }

    private string? _statusText;

    /// <inheritdoc cref="StatusTextProperty"/>
    public string? StatusText
    {
        get => _statusText;
        private set => SetAndRaise(StatusTextProperty, ref _statusText, value);
    }

    #endregion

    #region Events

    /// <summary>Raised when the greeting appears after a sign-in (Arc's <c>onSignIn</c>).</summary>
    public static readonly RoutedEvent<LoginSplitSignedInEventArgs> SignedInEvent =
        RoutedEvent.Register<LoginSplit, LoginSplitSignedInEventArgs>(nameof(SignedIn), RoutingStrategies.Bubble);

    /// <summary>Raised after Sign out.</summary>
    public static readonly RoutedEvent<LoginSplitSignedOutEventArgs> SignedOutEvent =
        RoutedEvent.Register<LoginSplit, LoginSplitSignedOutEventArgs>(nameof(SignedOut), RoutingStrategies.Bubble);

    /// <summary>Raised when the step changes.</summary>
    public static readonly RoutedEvent<LoginSplitStepChangedEventArgs> StepChangedEvent =
        RoutedEvent.Register<LoginSplit, LoginSplitStepChangedEventArgs>(nameof(StepChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when "Sign up", "Terms" or "Privacy" is used; handle it to navigate yourself.</summary>
    public static readonly RoutedEvent<LoginSplitNavigateEventArgs> NavigateEvent =
        RoutedEvent.Register<LoginSplit, LoginSplitNavigateEventArgs>(nameof(Navigate), RoutingStrategies.Bubble);

    /// <summary>Raised when a password reset link went out (first send and every resend).</summary>
    public static readonly RoutedEvent<LoginSplitResetLinkSentEventArgs> ResetLinkSentEvent =
        RoutedEvent.Register<LoginSplit, LoginSplitResetLinkSentEventArgs>(nameof(ResetLinkSent), RoutingStrategies.Bubble);

    /// <inheritdoc cref="SignedInEvent"/>
    public event EventHandler<LoginSplitSignedInEventArgs>? SignedIn
    {
        add => AddHandler(SignedInEvent, value);
        remove => RemoveHandler(SignedInEvent, value);
    }

    /// <inheritdoc cref="SignedOutEvent"/>
    public event EventHandler<LoginSplitSignedOutEventArgs>? SignedOut
    {
        add => AddHandler(SignedOutEvent, value);
        remove => RemoveHandler(SignedOutEvent, value);
    }

    /// <inheritdoc cref="StepChangedEvent"/>
    public event EventHandler<LoginSplitStepChangedEventArgs>? StepChanged
    {
        add => AddHandler(StepChangedEvent, value);
        remove => RemoveHandler(StepChangedEvent, value);
    }

    /// <inheritdoc cref="NavigateEvent"/>
    public event EventHandler<LoginSplitNavigateEventArgs>? Navigate
    {
        add => AddHandler(NavigateEvent, value);
        remove => RemoveHandler(NavigateEvent, value);
    }

    /// <inheritdoc cref="ResetLinkSentEvent"/>
    public event EventHandler<LoginSplitResetLinkSentEventArgs>? ResetLinkSent
    {
        add => AddHandler(ResetLinkSentEvent, value);
        remove => RemoveHandler(ResetLinkSentEvent, value);
    }

    #endregion

    private Border? _frame;
    private Grid? _layout;
    private LoginSplitViewport? _viewport;
    private Border? _photoSide;
    private SiteSwap? _status;
    private SiteAnnouncer? _announcer;
    private LoginSplitTextButton? _signUp;
    private LoginSplitTextButton? _terms;
    private LoginSplitTextButton? _privacy;
    private TextBlock? _noAccount;
    private Panel? _legal;
    private INotifyCollectionChanged? _observedProviders;

    static LoginSplit()
    {
        AffectsMeasure<LoginSplit>(IsFullScreenProperty);
        AutomationProperties.LandmarkTypeProperty.OverrideDefaultValue<LoginSplit>(AutomationLandmarkType.Region);
    }

    public LoginSplit()
    {
        Providers.CollectionChanged += (_, _) => OnProvidersChanged();
        PseudoClasses.Set(SplitPseudoClass, true);
        PseudoClasses.Set(WidePseudoClass, true);
    }

    /// <summary>The providers in use: <see cref="ProvidersSource"/> when set, else <see cref="Providers"/>.</summary>
    internal IReadOnlyList<LoginSplitProvider> EffectiveProviders =>
        ProvidersSource is { } source ? source.ToList() : Providers.ToList();

    /// <summary>The accounts in use: <see cref="AccountsSource"/> when set, else <see cref="Accounts"/>.</summary>
    internal IReadOnlyList<LoginSplitAccount> EffectiveAccounts =>
        AccountsSource is { } source ? source.ToList() : Accounts.ToList();

    /// <summary>The step viewport (tests read it).</summary>
    internal LoginSplitViewport? Viewport => _viewport;

    /// <summary>The announcer (tests read it).</summary>
    internal SiteAnnouncer? Announcer => _announcer;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_signUp is not null)
        {
            _signUp.Click -= OnSignUpClick;
        }
        if (_terms is not null)
        {
            _terms.Click -= OnTermsClick;
        }
        if (_privacy is not null)
        {
            _privacy.Click -= OnPrivacyClick;
        }
        _frame = e.NameScope.Find<Border>(FramePart);
        _layout = e.NameScope.Find<Grid>(LayoutPart);
        _viewport = e.NameScope.Find<LoginSplitViewport>(ViewportPart);
        _photoSide = e.NameScope.Find<Border>(PhotoSidePart);
        _status = e.NameScope.Find<SiteSwap>(StatusPart);
        if (_status is not null)
        {
            LoginSplitUi.Configure(_status);
        }
        _announcer = e.NameScope.Find<SiteAnnouncer>(AnnouncerPart);
        _signUp = e.NameScope.Find<LoginSplitTextButton>(SignUpPart);
        _terms = e.NameScope.Find<LoginSplitTextButton>(TermsPart);
        _privacy = e.NameScope.Find<LoginSplitTextButton>(PrivacyPart);
        _noAccount = e.NameScope.Find<TextBlock>(NoAccountPart);
        _legal = e.NameScope.Find<Panel>(LegalPart);
        if (_signUp is not null)
        {
            _signUp.Click += OnSignUpClick;
        }
        if (_terms is not null)
        {
            _terms.Click += OnTermsClick;
        }
        if (_privacy is not null)
        {
            _privacy.Click += OnPrivacyClick;
        }
        ApplyLabels();
        if (_status is not null && _statusText is not null)
        {
            _status.Content = StatusLine(_statusText);
        }
        UpdateBreakpoints(Bounds.Width);
        RebuildStep();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsFullScreenProperty)
        {
            PseudoClasses.Set(FullScreenPseudoClass, IsFullScreen);
        }
        else if (change.Property == BoundsProperty)
        {
            UpdateBreakpoints(Bounds.Width);
        }
        else if (change.Property == LabelsProperty || change.Property == BrandNameProperty)
        {
            ApplyLabels();
            RebuildStep();
        }
        else if (change.Property == ProvidersSourceProperty)
        {
            if (_observedProviders is not null)
            {
                _observedProviders.CollectionChanged -= OnProvidersSourceChanged;
            }
            _observedProviders = change.NewValue as INotifyCollectionChanged;
            if (_observedProviders is not null)
            {
                _observedProviders.CollectionChanged += OnProvidersSourceChanged;
            }
            OnProvidersChanged();
        }
        else if (change.Property == EmailProperty)
        {
            OnEmailPropertyChanged(change.GetNewValue<string>());
        }
        else if (change.Property == DemoPasswordProperty)
        {
            RefreshHint();
        }
    }

    private void OnProvidersSourceChanged(object? sender, NotifyCollectionChangedEventArgs e) => OnProvidersChanged();

    private void OnProvidersChanged()
    {
        if (Step == LoginSplitStep.Email && !IsBusy)
        {
            RebuildStep();
        }
    }

    private void ApplyLabels()
    {
        var labels = Labels;
        AutomationProperties.SetName(this, labels.Region(BrandName));
        if (_noAccount is not null)
        {
            _noAccount.Text = labels.NoAccount;
        }
        SetText(_signUp, labels.SignUp, small: false);
        SetText(_terms, labels.Terms, small: true);
        SetText(_privacy, labels.Privacy, small: true);
        if (_legal is not null)
        {
            AutomationProperties.SetName(_legal, labels.Legal);
            AutomationProperties.SetLandmarkType(_legal, AutomationLandmarkType.Navigation);
        }
    }

    private static void SetText(LoginSplitTextButton? button, string text, bool small)
    {
        if (button is not null)
        {
            button.Content = LoginSplitUi.Plain(text, small);
            AutomationProperties.SetName(button, text);
        }
    }

    /// <summary>Applies the width breakpoints: columns 5:6 (≥ 1100), 1:1 (≥ 760), the form alone (&lt; 760); compact padding below 480.</summary>
    private void UpdateBreakpoints(double width)
    {
        if (width <= 0)
        {
            return;
        }
        var wide = width >= LoginSplitMetrics.WideBreakpoint;
        var split = width >= LoginSplitMetrics.SplitBreakpoint;
        PseudoClasses.Set(WidePseudoClass, wide);
        PseudoClasses.Set(SplitPseudoClass, split);
        PseudoClasses.Set(StackedPseudoClass, !split);
        PseudoClasses.Set(CompactPseudoClass, width < LoginSplitMetrics.CompactBreakpoint);
        if (_layout is { ColumnDefinitions.Count: >= 2 } layout)
        {
            var form = wide ? new GridLength(5, GridUnitType.Star) : new GridLength(1, GridUnitType.Star);
            var photo = !split ? new GridLength(0, GridUnitType.Pixel) : wide ? new GridLength(6, GridUnitType.Star) : new GridLength(1, GridUnitType.Star);
            if (layout.ColumnDefinitions[0].Width != form)
            {
                layout.ColumnDefinitions[0].Width = form;
            }
            if (layout.ColumnDefinitions[1].Width != photo)
            {
                layout.ColumnDefinitions[1].Width = photo;
            }
        }
        if (_photoSide is not null)
        {
            _photoSide.IsVisible = split;
        }
    }

    private Control StatusLine(string text)
    {
        var line = LoginSplitUi.Text(text, TKCode.Arc.DesignTokens.ArcDesignTokenKind.TextMuted, small: true);
        line.TextWrapping = TextWrapping.NoWrap;
        line.TextTrimming = TextTrimming.CharacterEllipsis;
        return line;
    }

    private void OnSignUpClick(object? sender, RoutedEventArgs e) => FollowLink(LoginSplitLink.SignUp, SignUpHref);

    private void OnTermsClick(object? sender, RoutedEventArgs e) => FollowLink(LoginSplitLink.Terms, TermsHref);

    private void OnPrivacyClick(object? sender, RoutedEventArgs e) => FollowLink(LoginSplitLink.Privacy, PrivacyHref);

    private void FollowLink(LoginSplitLink link, string? href)
    {
        var args = new LoginSplitNavigateEventArgs(NavigateEvent, link, href);
        RaiseEvent(args);
        if (NavigateCommand is { } command && command.CanExecute(link))
        {
            command.Execute(link);
        }
        if (args.Handled)
        {
            return;
        }
        if (string.IsNullOrWhiteSpace(href))
        {
            SetStatus(Labels.LinkInApp(link));
        }
        else
        {
            SiteLinks.TryLaunch(this, href);
        }
    }
}
