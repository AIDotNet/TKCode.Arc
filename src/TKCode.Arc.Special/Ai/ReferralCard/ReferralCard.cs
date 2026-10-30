using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// A give-and-get referral card (Arc Referral card, design reference). The ask on top ("Give $20, get $20"), the invite link
/// in a quiet field with a Copy button and three share buttons (a post, a network and email, drawn with generic glyphs),
/// and an invite-by-email field that turns each address into a chip. Below, the funnel as three rolling numbers (invited,
/// signed up, earned), a segmented milestone bar with the reward it unlocks, and the people invited with their avatar and
/// stage as a glyph plus a word.
/// <para>
/// Everything is derived from <see cref="Referrals"/>: pass the list from your backend and replace it when sign-ups and
/// payments come in. Stage changes crossfade in place, the earned total counts up, and reaching the goal turns the bar and
/// its line into the unlocked state. Sending invites calls <see cref="InviteHandler"/>; without one the send is simulated.
/// With no referrals the card plays its <see cref="Simulation"/> (your program) once when it is 40% on screen; with neither
/// it is empty: zero counts and "No invites yet".
/// </para>
/// </summary>
[TemplatePart(RootPart, typeof(Panel))]
[PseudoClasses(NarrowPseudoClass, SendingPseudoClass, UnlockedPseudoClass)]
public partial class ReferralCard : TemplatedControl
{
    internal const string RootPart = "PART_Root";
    internal const string NarrowPseudoClass = ":narrow";
    internal const string SendingPseudoClass = ":sending";
    internal const string UnlockedPseudoClass = ":unlocked";

    /// <summary>Below this card width the share buttons take their own row and the milestone line keeps room for two lines.</summary>
    internal const double NarrowWidth = 470;

    /// <summary>Digits of the same width, so numbers and dates never shift.</summary>
    internal static readonly FontFeatureCollection TabularNumbers = new() { FontFeature.Parse("tnum") };

    /// <summary>The person's invite link. The domain shows muted and the code in full color; Copy copies the whole URL.</summary>
    public static readonly StyledProperty<string> LinkProperty =
        AvaloniaProperty.Register<ReferralCard, string>(nameof(Link), string.Empty);

    /// <summary>
    /// The credit each side gets, in whole currency units. Drives the heading, the Earned total and the rows' Earned label;
    /// 0 (the default) leaves the amount out of the words.
    /// </summary>
    public static readonly StyledProperty<int> RewardProperty =
        AvaloniaProperty.Register<ReferralCard, int>(nameof(Reward), coerce: (_, value) => Math.Max(0, value));

    /// <summary>The ISO 4217 code used to format the reward and the total ("USD" writes "$20").</summary>
    public static readonly StyledProperty<string> CurrencyProperty =
        AvaloniaProperty.Register<ReferralCard, string>(nameof(Currency), "USD");

    /// <summary>The heading. Null builds it from the reward: "Give $20, get $20" (or "Invite friends" without one).</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ReferralCard, string?>(nameof(Title));

    /// <summary>One or two lines under the heading. Null says what both sides get.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<ReferralCard, string?>(nameof(Description));

    /// <summary>
    /// Everyone invited, newest first. The counts, the milestone and the list are derived from it, and the card follows it
    /// when you replace it. Null shows the <see cref="Simulation"/>'s program, or the empty card.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<Referral>?> ReferralsProperty =
        AvaloniaProperty.Register<ReferralCard, IReadOnlyList<Referral>?>(nameof(Referrals));

    /// <summary>
    /// The bonus after a number of paid referrals: the goal (up to 12 segments are drawn), the reward in a few words and an
    /// optional line shown once unlocked. Null (the default) hides the milestone.
    /// </summary>
    public static readonly StyledProperty<ReferralMilestone?> MilestoneProperty =
        AvaloniaProperty.Register<ReferralCard, ReferralMilestone?>(nameof(Milestone));

    /// <summary>Text that goes with the link in a post, a network share and the email body.</summary>
    public static readonly StyledProperty<string?> ShareMessageProperty =
        AvaloniaProperty.Register<ReferralCard, string?>(nameof(ShareMessage));

    /// <summary>The subject of the share email, or null for none.</summary>
    public static readonly StyledProperty<string?> EmailSubjectProperty =
        AvaloniaProperty.Register<ReferralCard, string?>(nameof(EmailSubject));

    /// <summary>
    /// The share URL of the post button, with <c>{url}</c> and <c>{text}</c> placeholders (filled escaped), for example
    /// your social network's share intent. Null passes the link itself.
    /// </summary>
    public static readonly StyledProperty<string?> PostShareUrlFormatProperty =
        AvaloniaProperty.Register<ReferralCard, string?>(nameof(PostShareUrlFormat));

    /// <summary>The share URL of the network button, with <c>{url}</c> and <c>{text}</c> placeholders. Null passes the link itself.</summary>
    public static readonly StyledProperty<string?> NetworkShareUrlFormatProperty =
        AvaloniaProperty.Register<ReferralCard, string?>(nameof(NetworkShareUrlFormat));

    /// <summary>The most addresses one send can hold. Extra addresses are refused with a note.</summary>
    public static readonly StyledProperty<int> MaxInvitesProperty =
        AvaloniaProperty.Register<ReferralCard, int>(nameof(MaxInvites), 10, coerce: (_, value) => Math.Max(1, value));

    /// <summary>
    /// Plays the <see cref="Simulation"/>'s beats once on screen and moves the first address of each simulated send through
    /// the stages. Null means true while a <see cref="Simulation"/> shows (no <see cref="Referrals"/>) and false otherwise.
    /// </summary>
    public static readonly StyledProperty<bool?> SimulateProperty =
        AvaloniaProperty.Register<ReferralCard, bool?>(nameof(Simulate));

    /// <summary>
    /// Sends the invites. Send stays busy until the task ends; a failed task keeps the chips and shows the exception's message;
    /// on success the addresses join the top of the list as Invited. The token is cancelled when the card leaves the screen.
    /// Null simulates the send.
    /// </summary>
    public static readonly StyledProperty<Func<IReadOnlyList<string>, CancellationToken, Task>?> InviteHandlerProperty =
        AvaloniaProperty.Register<ReferralCard, Func<IReadOnlyList<string>, CancellationToken, Task>?>(nameof(InviteHandler));

    /// <summary>
    /// A program to play while <see cref="Referrals"/> is null: the card shows its list and plays its beats once when it is
    /// 40% on screen. Null (the default) leaves a card without referrals empty. Give each card its own.
    /// </summary>
    public static readonly StyledProperty<ReferralCardSimulation?> SimulationProperty =
        AvaloniaProperty.Register<ReferralCard, ReferralCardSimulation?>(nameof(Simulation));

    /// <summary>Every word the card shows or says.</summary>
    public static readonly StyledProperty<ReferralCardLabels> LabelsProperty =
        AvaloniaProperty.Register<ReferralCard, ReferralCardLabels>(nameof(Labels), ReferralCardLabels.Default);

    /// <summary>Raised after Copy has written the link to the clipboard. Bubbles.</summary>
    public static readonly RoutedEvent<ReferralLinkEventArgs> LinkCopiedEvent =
        RoutedEvent.Register<ReferralCard, ReferralLinkEventArgs>(nameof(LinkCopied), RoutingStrategies.Bubble);

    /// <summary>
    /// Raised when a share button is pressed, with the channel and a ready share URL. Open the URL and set <c>Handled</c>;
    /// when nobody does, the share is simulated and the card says so. Bubbles.
    /// </summary>
    public static readonly RoutedEvent<ReferralShareEventArgs> ShareRequestedEvent =
        RoutedEvent.Register<ReferralCard, ReferralShareEventArgs>(nameof(ShareRequested), RoutingStrategies.Bubble);

    /// <summary>Raised after a send succeeded (or was simulated), with the addresses. Bubbles.</summary>
    public static readonly RoutedEvent<ReferralInvitesEventArgs> InvitesSentEvent =
        RoutedEvent.Register<ReferralCard, ReferralInvitesEventArgs>(nameof(InvitesSent), RoutingStrategies.Bubble);

    /// <summary>Raised once when paid referrals reach the milestone's goal. Bubbles.</summary>
    public static readonly RoutedEvent<ReferralMilestoneEventArgs> MilestoneReachedEvent =
        RoutedEvent.Register<ReferralCard, ReferralMilestoneEventArgs>(nameof(MilestoneReached), RoutingStrategies.Bubble);

    /// <inheritdoc cref="LinkProperty"/>
    public string Link { get => GetValue(LinkProperty); set => SetValue(LinkProperty, value); }

    /// <inheritdoc cref="RewardProperty"/>
    public int Reward { get => GetValue(RewardProperty); set => SetValue(RewardProperty, value); }

    /// <inheritdoc cref="CurrencyProperty"/>
    public string Currency { get => GetValue(CurrencyProperty); set => SetValue(CurrencyProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="ReferralsProperty"/>
    public IReadOnlyList<Referral>? Referrals { get => GetValue(ReferralsProperty); set => SetValue(ReferralsProperty, value); }

    /// <inheritdoc cref="MilestoneProperty"/>
    public ReferralMilestone? Milestone { get => GetValue(MilestoneProperty); set => SetValue(MilestoneProperty, value); }

    /// <inheritdoc cref="ShareMessageProperty"/>
    public string? ShareMessage { get => GetValue(ShareMessageProperty); set => SetValue(ShareMessageProperty, value); }

    /// <inheritdoc cref="EmailSubjectProperty"/>
    public string? EmailSubject { get => GetValue(EmailSubjectProperty); set => SetValue(EmailSubjectProperty, value); }

    /// <inheritdoc cref="PostShareUrlFormatProperty"/>
    public string? PostShareUrlFormat { get => GetValue(PostShareUrlFormatProperty); set => SetValue(PostShareUrlFormatProperty, value); }

    /// <inheritdoc cref="NetworkShareUrlFormatProperty"/>
    public string? NetworkShareUrlFormat { get => GetValue(NetworkShareUrlFormatProperty); set => SetValue(NetworkShareUrlFormatProperty, value); }

    /// <inheritdoc cref="MaxInvitesProperty"/>
    public int MaxInvites { get => GetValue(MaxInvitesProperty); set => SetValue(MaxInvitesProperty, value); }

    /// <inheritdoc cref="SimulateProperty"/>
    public bool? Simulate { get => GetValue(SimulateProperty); set => SetValue(SimulateProperty, value); }

    /// <inheritdoc cref="SimulationProperty"/>
    public ReferralCardSimulation? Simulation { get => GetValue(SimulationProperty); set => SetValue(SimulationProperty, value); }

    /// <inheritdoc cref="InviteHandlerProperty"/>
    public Func<IReadOnlyList<string>, CancellationToken, Task>? InviteHandler
    {
        get => GetValue(InviteHandlerProperty);
        set => SetValue(InviteHandlerProperty, value);
    }

    /// <inheritdoc cref="LabelsProperty"/>
    public ReferralCardLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="LinkCopiedEvent"/>
    public event EventHandler<ReferralLinkEventArgs>? LinkCopied
    {
        add => AddHandler(LinkCopiedEvent, value);
        remove => RemoveHandler(LinkCopiedEvent, value);
    }

    /// <inheritdoc cref="ShareRequestedEvent"/>
    public event EventHandler<ReferralShareEventArgs>? ShareRequested
    {
        add => AddHandler(ShareRequestedEvent, value);
        remove => RemoveHandler(ShareRequestedEvent, value);
    }

    /// <inheritdoc cref="InvitesSentEvent"/>
    public event EventHandler<ReferralInvitesEventArgs>? InvitesSent
    {
        add => AddHandler(InvitesSentEvent, value);
        remove => RemoveHandler(InvitesSentEvent, value);
    }

    /// <inheritdoc cref="MilestoneReachedEvent"/>
    public event EventHandler<ReferralMilestoneEventArgs>? MilestoneReached
    {
        add => AddHandler(MilestoneReachedEvent, value);
        remove => RemoveHandler(MilestoneReachedEvent, value);
    }

    /// <summary>
    /// Builds the share URL for a channel, the same one <see cref="ShareRequested"/> carries. Email gives a <c>mailto:</c>
    /// link whose body is the message and the link. Post and Network fill <paramref name="urlFormat"/> (<c>{url}</c> and
    /// <c>{text}</c> are replaced, escaped); without a format they return the link.
    /// </summary>
    public static string BuildShareUrl(ReferralShareChannel channel, string link, string? message = null, string? urlFormat = null,
                                       string? subject = null) =>
        ReferralCardFormat.ShareUrl(channel, link, message, urlFormat, subject);

    /// <summary>The simulation on screen: <see cref="Simulation"/> while there are no <see cref="Referrals"/>.</summary>
    internal ReferralCardSimulation? ShownSimulation => Referrals is null ? Simulation : null;

    /// <summary>Whether the card plays the simulation's program and moves simulated sends through the stages.</summary>
    internal bool Simulates => Simulate ?? ShownSimulation is not null;

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Teardown();
        var root = e.NameScope.Find<Panel>(RootPart);
        if (root is not null)
        {
            Build(root);
            ApplyBreakpoint(Bounds.Width, force: true);
            Refresh(animate: false);
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ReferralsProperty)
        {
            ResetItems();
            Refresh(animate: true);
        }
        else if (change.Property == SimulationProperty)
        {
            // A new program starts over: the old one stops, the new one plays once on screen.
            _programCts?.Cancel();
            _programCts?.Dispose();
            _programCts = null;
            _programStarted = false;
            if (Referrals is null)
            {
                ResetItems();
                Refresh(animate: false);
            }
            WatchViewport();
        }
        else if (change.Property == LabelsProperty)
        {
            RefreshWords();
            Refresh(animate: false);
        }
        else if (change.Property == LinkProperty || change.Property == RewardProperty || change.Property == CurrencyProperty
                 || change.Property == TitleProperty || change.Property == DescriptionProperty
                 || change.Property == MilestoneProperty)
        {
            Refresh(animate: true);
        }
        else if (change.Property == SimulateProperty)
        {
            WatchViewport();
        }
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        ApplyBreakpoint(e.NewSize.Width, force: false);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        WatchViewport();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _viewport?.Dispose();
        _viewport = null;
        CancelWork();
    }
}
