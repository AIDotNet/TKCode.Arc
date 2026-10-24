using System.Collections;
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
/// A full-page magic-link sign-in over a picture: the product's mark and a sign-in / sign-up switch along the top, a
/// frosted glass card in the middle, a note and the legal links along the bottom. The card walks through three steps:
/// the email form (with single sign-on buttons under "or"), the Sent step (an envelope folds shut over the address, Open
/// mail app, and a resend countdown) and the Done step (a drawn check, the account and the method, Sign out).
/// </summary>
/// <remarks>
/// With no handlers the page runs the reference's demo on fixed delays (see <see cref="Timings"/>): sending takes
/// 900 ms, Open mail app follows the link for you after 1 s and a provider answers after 900 ms. Set
/// <see cref="SendLinkAsync"/>, <see cref="OpenMailAppAsync"/> and <see cref="SignInWithProviderAsync"/> to do the real
/// work, and listen to <see cref="SignedIn"/>, <see cref="LinkSent"/>, <see cref="SignedOut"/> and
/// <see cref="Navigate"/>. Without a <see cref="Photo"/> the page draws a dusk landscape. The card's glass is a blurred
/// copy of the picture made once (Avalonia cannot blur live content behind a control); with reduced transparency the
/// card is solid.
/// </remarks>
[TemplatePart(BackdropPart, typeof(LoginImmersiveBackdrop))]
[TemplatePart(SwitchPart, typeof(LoginImmersiveSwitch))]
[TemplatePart(CardPart, typeof(Border))]
[TemplatePart(GlassPart, typeof(LoginImmersiveGlass))]
[TemplatePart(StepsPart, typeof(LoginImmersiveStepHost))]
[TemplatePart(FormPart, typeof(LoginImmersiveFormView))]
[TemplatePart(SentPart, typeof(LoginImmersiveSentView))]
[TemplatePart(DonePart, typeof(LoginImmersiveDoneView))]
[TemplatePart(NotePart, typeof(SiteSwap))]
[TemplatePart(FooterPart, typeof(LoginImmersiveFooterPanel))]
[TemplatePart(TermsPart, typeof(LoginImmersiveTextButton))]
[TemplatePart(PrivacyPart, typeof(LoginImmersiveTextButton))]
[TemplatePart(AnnouncerPart, typeof(SiteAnnouncer))]
[PseudoClasses(FullScreenPseudoClass, CompactPseudoClass, CardCompactPseudoClass, SignUpPseudoClass, BusyPseudoClass)]
public partial class LoginImmersive : TemplatedControl
{
    public const string BackdropPart = "PART_Backdrop";
    public const string SwitchPart = "PART_Switch";
    public const string CardPart = "PART_Card";
    public const string GlassPart = "PART_Glass";
    public const string StepsPart = "PART_Steps";
    public const string FormPart = "PART_Form";
    public const string SentPart = "PART_Sent";
    public const string DonePart = "PART_Done";
    public const string NotePart = "PART_Note";
    public const string FooterPart = "PART_Footer";
    public const string TermsPart = "PART_Terms";
    public const string PrivacyPart = "PART_Privacy";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set while <see cref="IsFullScreen"/> is true: square corners, no minimum height.</summary>
    public const string FullScreenPseudoClass = ":fullscreen";

    /// <summary>Set while the block is at most <see cref="CompactWidth"/> wide: tighter header, footer and stage padding.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>Set while the card's inside is narrower than <see cref="CardCompactWidth"/>: tighter steps, a smaller heading.</summary>
    public const string CardCompactPseudoClass = ":card-compact";

    /// <summary>Set while <see cref="Mode"/> is <see cref="LoginImmersiveMode.SignUp"/>.</summary>
    public const string SignUpPseudoClass = ":signup";

    /// <summary>Set while the page waits for a send, the mail app or a provider.</summary>
    public const string BusyPseudoClass = ":busy";

    /// <summary>The block's width at and below which the layout is compact (520).</summary>
    public const double CompactWidth = 520;

    /// <summary>The card's inner width below which its steps are compact (380).</summary>
    public const double CardCompactWidth = 380;

    /// <summary>The address the demo signs in with when a provider is used and the email field holds no valid address.</summary>
    public const string SampleEmail = "rowan@fieldnote.app";

    #region Properties

    /// <summary>Fills its container edge to edge with square corners (the page is the whole window). False (default): a block with 26px corners and at least 640px high.</summary>
    public static readonly StyledProperty<bool> IsFullScreenProperty =
        AvaloniaProperty.Register<LoginImmersive, bool>(nameof(IsFullScreen));

    /// <summary>Sign in (default) or create an account (two way); the header switch flips it.</summary>
    public static readonly StyledProperty<LoginImmersiveMode> ModeProperty =
        AvaloniaProperty.Register<LoginImmersive, LoginImmersiveMode>(nameof(Mode), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The text of the email field (two way).</summary>
    public static readonly StyledProperty<string> EmailProperty =
        AvaloniaProperty.Register<LoginImmersive, string>(nameof(Email), string.Empty, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The product's name in the header and the headings ("Fieldnote").</summary>
    public static readonly StyledProperty<string> BrandNameProperty =
        AvaloniaProperty.Register<LoginImmersive, string>(nameof(BrandName), "Fieldnote");

    /// <summary>The product's mark: an outline on a 20-unit box, drawn 2.25 wide with round ends. Null draws the default notebook mark.</summary>
    public static readonly StyledProperty<Geometry?> BrandMarkProperty =
        AvaloniaProperty.Register<LoginImmersive, Geometry?>(nameof(BrandMark));

    /// <summary>The picture behind everything, cropped to fill like CSS <c>object-fit: cover</c>. Null draws a dusk landscape.</summary>
    public static readonly StyledProperty<IImage?> PhotoProperty =
        AvaloniaProperty.Register<LoginImmersive, IImage?>(nameof(Photo));

    /// <summary>Which point of the picture stays in view when it is cropped, as fractions (default 0.5, 0.58: centred, a little below the middle).</summary>
    public static readonly StyledProperty<Point> PhotoAlignmentProperty =
        AvaloniaProperty.Register<LoginImmersive, Point>(nameof(PhotoAlignment), new Point(0.5, 0.58));

    /// <summary>Seconds before another link can be sent from the Sent step (30).</summary>
    public static readonly StyledProperty<int> ResendSecondsProperty =
        AvaloniaProperty.Register<LoginImmersive, int>(nameof(ResendSeconds), 30, coerce: (_, v) => Math.Max(0, v));

    /// <summary>The address a provider sign-in uses in the demo when the email field holds no valid address.</summary>
    public static readonly StyledProperty<string> DefaultEmailProperty =
        AvaloniaProperty.Register<LoginImmersive, string>(nameof(DefaultEmail), SampleEmail);

    /// <summary>The terms' address. Without one, Terms shows a note in the footer instead.</summary>
    public static readonly StyledProperty<string?> TermsHrefProperty =
        AvaloniaProperty.Register<LoginImmersive, string?>(nameof(TermsHref));

    /// <summary>The privacy policy's address. Without one, Privacy shows a note in the footer instead.</summary>
    public static readonly StyledProperty<string?> PrivacyHrefProperty =
        AvaloniaProperty.Register<LoginImmersive, string?>(nameof(PrivacyHref));

    /// <summary>
    /// The mail app's address (for example <c>mailto:</c>). With one (and no <see cref="OpenMailAppAsync"/>), Open mail
    /// app opens it and the page waits on the Sent step for <see cref="CompleteSignIn"/>; without one it runs the demo.
    /// </summary>
    public static readonly StyledProperty<string?> MailAppUriProperty =
        AvaloniaProperty.Register<LoginImmersive, string?>(nameof(MailAppUri));

    /// <summary>Shows "or" and the single sign-on buttons (default true).</summary>
    public static readonly StyledProperty<bool> IsProvidersVisibleProperty =
        AvaloniaProperty.Register<LoginImmersive, bool>(nameof(IsProvidersVisible), true);

    /// <summary>Single sign-on providers from a view model; when set it wins over <see cref="Providers"/>.</summary>
    public static readonly StyledProperty<IEnumerable<LoginImmersiveProvider>?> ProvidersSourceProperty =
        AvaloniaProperty.Register<LoginImmersive, IEnumerable<LoginImmersiveProvider>?>(nameof(ProvidersSource));

    /// <summary>Every text the page shows or announces.</summary>
    public static readonly StyledProperty<LoginImmersiveLabels> LabelsProperty =
        AvaloniaProperty.Register<LoginImmersive, LoginImmersiveLabels>(nameof(Labels), new LoginImmersiveLabels());

    /// <summary>The demo's delays.</summary>
    public static readonly StyledProperty<LoginImmersiveTimings> TimingsProperty =
        AvaloniaProperty.Register<LoginImmersive, LoginImmersiveTimings>(nameof(Timings), new LoginImmersiveTimings());

    /// <summary>Runs after <see cref="SignedIn"/>; its parameter is the event arguments.</summary>
    public static readonly StyledProperty<ICommand?> SignedInCommandProperty =
        AvaloniaProperty.Register<LoginImmersive, ICommand?>(nameof(SignedInCommand));

    /// <summary>Runs after an unhandled <see cref="Navigate"/>; its parameter is the event arguments.</summary>
    public static readonly StyledProperty<ICommand?> NavigateCommandProperty =
        AvaloniaProperty.Register<LoginImmersive, ICommand?>(nameof(NavigateCommand));

    /// <summary>The card's step.</summary>
    public static readonly DirectProperty<LoginImmersive, LoginImmersiveStep> StepProperty =
        AvaloniaProperty.RegisterDirect<LoginImmersive, LoginImmersiveStep>(nameof(Step), o => o.Step);

    /// <summary>The address the link went to, or the signed-in address (trimmed, lower case); null on the form.</summary>
    public static readonly DirectProperty<LoginImmersive, string?> AccountProperty =
        AvaloniaProperty.RegisterDirect<LoginImmersive, string?>(nameof(Account), o => o.Account);

    /// <summary>How the person signed in (meaningful on the Done step).</summary>
    public static readonly DirectProperty<LoginImmersive, LoginImmersiveMethod> MethodProperty =
        AvaloniaProperty.RegisterDirect<LoginImmersive, LoginImmersiveMethod>(nameof(Method), o => o.Method);

    /// <summary>The provider the person signed in with, or null.</summary>
    public static readonly DirectProperty<LoginImmersive, LoginImmersiveProvider?> ProviderProperty =
        AvaloniaProperty.RegisterDirect<LoginImmersive, LoginImmersiveProvider?>(nameof(Provider), o => o.Provider);

    /// <summary>True while the page waits for a send, the mail app or a provider.</summary>
    public static readonly DirectProperty<LoginImmersive, bool> IsBusyProperty =
        AvaloniaProperty.RegisterDirect<LoginImmersive, bool>(nameof(IsBusy), o => o.IsBusy);

    /// <summary>The single sign-on providers, one equal button each. Empty (the default) shows Google, Apple and GitHub.</summary>
    public static readonly DirectProperty<LoginImmersive, AvaloniaList<LoginImmersiveProvider>> ProvidersProperty =
        AvaloniaProperty.RegisterDirect<LoginImmersive, AvaloniaList<LoginImmersiveProvider>>(nameof(Providers), o => o.Providers);

    /// <summary>The person signed in (the emailed link, a provider, or <see cref="CompleteSignIn"/>).</summary>
    public static readonly RoutedEvent<LoginImmersiveSignedInEventArgs> SignedInEvent =
        RoutedEvent.Register<LoginImmersive, LoginImmersiveSignedInEventArgs>(nameof(SignedIn), RoutingStrategies.Bubble);

    /// <summary>The person signed out from the Done step (or <see cref="SignOut"/> was called).</summary>
    public static readonly RoutedEvent<RoutedEventArgs> SignedOutEvent =
        RoutedEvent.Register<LoginImmersive, RoutedEventArgs>(nameof(SignedOut), RoutingStrategies.Bubble);

    /// <summary>A link went out (the first one, or another from the Sent step).</summary>
    public static readonly RoutedEvent<LoginImmersiveLinkSentEventArgs> LinkSentEvent =
        RoutedEvent.Register<LoginImmersive, LoginImmersiveLinkSentEventArgs>(nameof(LinkSent), RoutingStrategies.Bubble);

    /// <summary>Terms, Privacy or the mail app was chosen. Handle it to open the destination yourself.</summary>
    public static readonly RoutedEvent<LoginImmersiveNavigateEventArgs> NavigateEvent =
        RoutedEvent.Register<LoginImmersive, LoginImmersiveNavigateEventArgs>(nameof(Navigate), RoutingStrategies.Bubble);

    private readonly AvaloniaList<LoginImmersiveProvider> _providers = new();
    private LoginImmersiveStep _step;
    private string? _account;
    private LoginImmersiveMethod _method;
    private LoginImmersiveProvider? _provider;
    private bool _isBusy;

    /// <inheritdoc cref="IsFullScreenProperty"/>
    public bool IsFullScreen { get => GetValue(IsFullScreenProperty); set => SetValue(IsFullScreenProperty, value); }

    /// <inheritdoc cref="ModeProperty"/>
    public LoginImmersiveMode Mode { get => GetValue(ModeProperty); set => SetValue(ModeProperty, value); }

    /// <inheritdoc cref="EmailProperty"/>
    public string Email { get => GetValue(EmailProperty); set => SetValue(EmailProperty, value); }

    /// <inheritdoc cref="BrandNameProperty"/>
    public string BrandName { get => GetValue(BrandNameProperty); set => SetValue(BrandNameProperty, value); }

    /// <inheritdoc cref="BrandMarkProperty"/>
    public Geometry? BrandMark { get => GetValue(BrandMarkProperty); set => SetValue(BrandMarkProperty, value); }

    /// <inheritdoc cref="PhotoProperty"/>
    public IImage? Photo { get => GetValue(PhotoProperty); set => SetValue(PhotoProperty, value); }

    /// <inheritdoc cref="PhotoAlignmentProperty"/>
    public Point PhotoAlignment { get => GetValue(PhotoAlignmentProperty); set => SetValue(PhotoAlignmentProperty, value); }

    /// <inheritdoc cref="ResendSecondsProperty"/>
    public int ResendSeconds { get => GetValue(ResendSecondsProperty); set => SetValue(ResendSecondsProperty, value); }

    /// <inheritdoc cref="DefaultEmailProperty"/>
    public string DefaultEmail { get => GetValue(DefaultEmailProperty); set => SetValue(DefaultEmailProperty, value); }

    /// <inheritdoc cref="TermsHrefProperty"/>
    public string? TermsHref { get => GetValue(TermsHrefProperty); set => SetValue(TermsHrefProperty, value); }

    /// <inheritdoc cref="PrivacyHrefProperty"/>
    public string? PrivacyHref { get => GetValue(PrivacyHrefProperty); set => SetValue(PrivacyHrefProperty, value); }

    /// <inheritdoc cref="MailAppUriProperty"/>
    public string? MailAppUri { get => GetValue(MailAppUriProperty); set => SetValue(MailAppUriProperty, value); }

    /// <inheritdoc cref="IsProvidersVisibleProperty"/>
    public bool IsProvidersVisible { get => GetValue(IsProvidersVisibleProperty); set => SetValue(IsProvidersVisibleProperty, value); }

    /// <inheritdoc cref="ProvidersSourceProperty"/>
    public IEnumerable<LoginImmersiveProvider>? ProvidersSource { get => GetValue(ProvidersSourceProperty); set => SetValue(ProvidersSourceProperty, value); }

    /// <inheritdoc cref="ProvidersProperty"/>
    public AvaloniaList<LoginImmersiveProvider> Providers => _providers;

    /// <inheritdoc cref="LabelsProperty"/>
    public LoginImmersiveLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="TimingsProperty"/>
    public LoginImmersiveTimings Timings { get => GetValue(TimingsProperty); set => SetValue(TimingsProperty, value); }

    /// <inheritdoc cref="SignedInCommandProperty"/>
    public ICommand? SignedInCommand { get => GetValue(SignedInCommandProperty); set => SetValue(SignedInCommandProperty, value); }

    /// <inheritdoc cref="NavigateCommandProperty"/>
    public ICommand? NavigateCommand { get => GetValue(NavigateCommandProperty); set => SetValue(NavigateCommandProperty, value); }

    /// <inheritdoc cref="StepProperty"/>
    public LoginImmersiveStep Step
    {
        get => _step;
        private set => SetAndRaise(StepProperty, ref _step, value);
    }

    /// <inheritdoc cref="AccountProperty"/>
    public string? Account
    {
        get => _account;
        private set => SetAndRaise(AccountProperty, ref _account, value);
    }

    /// <inheritdoc cref="MethodProperty"/>
    public LoginImmersiveMethod Method
    {
        get => _method;
        private set => SetAndRaise(MethodProperty, ref _method, value);
    }

    /// <inheritdoc cref="ProviderProperty"/>
    public LoginImmersiveProvider? Provider
    {
        get => _provider;
        private set => SetAndRaise(ProviderProperty, ref _provider, value);
    }

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

    /// <summary>
    /// Sends the link (email, mode, cancellation). Throwing shows an error under the field. Null: the demo waits
    /// <see cref="LoginImmersiveTimings.SendLink"/>.
    /// </summary>
    public Func<string, LoginImmersiveMode, CancellationToken, Task>? SendLinkAsync { get; set; }

    /// <summary>
    /// Opens the person's mail app for the address and returns true once the link was followed (the page shows Done) or
    /// false to stay on the Sent step. Null: see <see cref="MailAppUri"/>.
    /// </summary>
    public Func<string, CancellationToken, Task<bool>>? OpenMailAppAsync { get; set; }

    /// <summary>
    /// Signs in with a provider (the provider, the valid address in the field or null, cancellation) and returns the
    /// signed-in address, or null when the person cancelled. Null: the demo waits <see cref="LoginImmersiveTimings.Provider"/>.
    /// </summary>
    public Func<LoginImmersiveProvider, string?, CancellationToken, Task<string?>>? SignInWithProviderAsync { get; set; }

    /// <inheritdoc cref="SignedInEvent"/>
    public event EventHandler<LoginImmersiveSignedInEventArgs>? SignedIn
    {
        add => AddHandler(SignedInEvent, value);
        remove => RemoveHandler(SignedInEvent, value);
    }

    /// <inheritdoc cref="SignedOutEvent"/>
    public event EventHandler<RoutedEventArgs>? SignedOut
    {
        add => AddHandler(SignedOutEvent, value);
        remove => RemoveHandler(SignedOutEvent, value);
    }

    /// <inheritdoc cref="LinkSentEvent"/>
    public event EventHandler<LoginImmersiveLinkSentEventArgs>? LinkSent
    {
        add => AddHandler(LinkSentEvent, value);
        remove => RemoveHandler(LinkSentEvent, value);
    }

    /// <inheritdoc cref="NavigateEvent"/>
    public event EventHandler<LoginImmersiveNavigateEventArgs>? Navigate
    {
        add => AddHandler(NavigateEvent, value);
        remove => RemoveHandler(NavigateEvent, value);
    }

    #endregion

    static LoginImmersive()
    {
        AutomationProperties.LandmarkTypeProperty.OverrideDefaultValue<LoginImmersive>(AutomationLandmarkType.Region);
    }

    public LoginImmersive()
    {
        _providers.CollectionChanged += OnProvidersChanged;
    }
}
