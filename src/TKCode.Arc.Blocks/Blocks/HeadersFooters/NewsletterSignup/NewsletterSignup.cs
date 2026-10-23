// Ported from Arc's free newsletter-signup block (MIT, Copyright (c) 2026 Elia Kuratli): newsletter-signup.tsx.
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
/// A newsletter signup framed by the newsletter itself: a stack of recent issues. Subscribing drops the next issue,
/// addressed to the new reader, onto the front of the stack, drops the oldest one away and ticks the reader count up by
/// one. It comes as an <see cref="NewsletterVariant.Inline"/> page section (copy and form beside the stack) or a
/// self-contained <see cref="NewsletterVariant.Card"/> (the stack in a tray on top).
/// </summary>
/// <remarks>
/// <para>
/// The form validates the address (an empty field and a malformed address each get their own message; validation starts
/// when the field loses focus with text in it, and from the first submit on it follows every keystroke), sends it through
/// <see cref="SubscribeAsync"/> and shows the result: the pill shakes for an error, the button reports the work
/// (<see cref="NewsletterPhase.Sending"/>) and then a check mark (<see cref="NewsletterPhase.Done"/>). A failed send keeps
/// the address, shows "Try again" and shakes. Without a handler the send is simulated and takes 1100 ms.
/// </para>
/// <para>
/// Zero configuration gives Arc's preview: its copy, the publication "Margins" (no thumbnails: set the stories'
/// <see cref="NewsletterStory.Image"/> and <see cref="ReaderFaces"/> from your own pictures) and 12,480 readers. The
/// block follows its own width: one column up to 860, tighter spacing up to 520, no reading times up to 400, and a form
/// narrower than 300 stacks its field and button.
/// </para>
/// Ported from Arc's free newsletter-signup block (MIT, Copyright (c) 2026 Elia Kuratli).
/// </remarks>
[TemplatePart(FormPart, typeof(NewsletterForm))]
[TemplatePart(StackPart, typeof(NewsletterIssueStack))]
[TemplatePart(ReadersPart, typeof(NewsletterReaders))]
[TemplatePart(AnnouncerPart, typeof(SiteAnnouncer))]
[TemplatePart(AlertPart, typeof(SiteAnnouncer))]
[PseudoClasses(CardPseudoClass, CompactPseudoClass, NarrowPseudoClass, TinyPseudoClass, MicroPseudoClass, NoAsidePseudoClass,
    NoPublicationPseudoClass, NoReadersPseudoClass, SendingPseudoClass, DonePseudoClass)]
public partial class NewsletterSignup : TemplatedControl
{
    public const string FormPart = "PART_Form";
    public const string StackPart = "PART_Stack";
    public const string ReadersPart = "PART_Readers";
    public const string AnnouncerPart = "PART_Announcer";
    public const string AlertPart = "PART_Alert";

    /// <summary>Set while <see cref="Variant"/> is Card.</summary>
    public const string CardPseudoClass = ":card";

    /// <summary>Set while the block is at most 860 wide: one column.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>Set while the block is at most 520 wide: tighter spacing and smaller type.</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Set while the block is at most 400 wide: no reading times, smaller faces.</summary>
    public const string TinyPseudoClass = ":tiny";

    /// <summary>Set while the block is at most 360 wide: the readers line may wrap.</summary>
    public const string MicroPseudoClass = ":micro";

    /// <summary>Set while neither the stack nor the readers show.</summary>
    public const string NoAsidePseudoClass = ":no-aside";

    /// <summary>Set while the stack does not show.</summary>
    public const string NoPublicationPseudoClass = ":no-publication";

    /// <summary>Set while the readers do not show.</summary>
    public const string NoReadersPseudoClass = ":no-readers";

    /// <summary>Set while <see cref="Phase"/> is Sending.</summary>
    public const string SendingPseudoClass = ":sending";

    /// <summary>Set while <see cref="Phase"/> is Done.</summary>
    public const string DonePseudoClass = ":done";

    #region Copy and layout

    /// <summary><see cref="NewsletterVariant.Inline"/> (the default) or <see cref="NewsletterVariant.Card"/>.</summary>
    public static readonly StyledProperty<NewsletterVariant> VariantProperty =
        AvaloniaProperty.Register<NewsletterSignup, NewsletterVariant>(nameof(Variant));

    /// <summary>The heading. Null uses Arc's copy for the variant.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<NewsletterSignup, string?>(nameof(Title));

    /// <summary>The line under the heading. Null uses Arc's copy for the variant.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<NewsletterSignup, string?>(nameof(Description));

    /// <summary>The placeholder of the email field ("you@company.com").</summary>
    public static readonly StyledProperty<string> PlaceholderProperty =
        AvaloniaProperty.Register<NewsletterSignup, string>(nameof(Placeholder), "you@company.com");

    /// <summary>The label of the button in its idle state ("Subscribe").</summary>
    public static readonly StyledProperty<string> ButtonLabelProperty =
        AvaloniaProperty.Register<NewsletterSignup, string>(nameof(ButtonLabel), "Subscribe");

    /// <summary>The short line under the form about frequency and privacy.</summary>
    public static readonly StyledProperty<string> PrivacyNoteProperty =
        AvaloniaProperty.Register<NewsletterSignup, string>(nameof(PrivacyNote), "No tracking pixels. Unsubscribe with one click.");

    /// <summary>The label of the link after the privacy note ("Privacy policy").</summary>
    public static readonly StyledProperty<string> PrivacyLinkLabelProperty =
        AvaloniaProperty.Register<NewsletterSignup, string>(nameof(PrivacyLinkLabel), "Privacy policy");

    /// <summary>
    /// Where the privacy link goes ("#privacy"). Pressing it raises <see cref="Navigate"/>; when nobody handles the event and
    /// the value is an absolute http, https or mailto address, it is opened with the platform launcher.
    /// </summary>
    public static readonly StyledProperty<string?> PrivacyLinkHrefProperty =
        AvaloniaProperty.Register<NewsletterSignup, string?>(nameof(PrivacyLinkHref), "#privacy");

    /// <summary>Whether the privacy link shows after the note (true).</summary>
    public static readonly StyledProperty<bool> IsPrivacyLinkVisibleProperty =
        AvaloniaProperty.Register<NewsletterSignup, bool>(nameof(IsPrivacyLinkVisible), true);

    /// <summary>Runs when the privacy link is pressed, after <see cref="Navigate"/>; its parameter is the destination.</summary>
    public static readonly StyledProperty<ICommand?> NavigateCommandProperty =
        AvaloniaProperty.Register<NewsletterSignup, ICommand?>(nameof(NavigateCommand));

    #endregion

    #region Readers and publication

    /// <summary>The number of readers (12480). Subscribing shows one more.</summary>
    public static readonly StyledProperty<int> ReaderCountProperty =
        AvaloniaProperty.Register<NewsletterSignup, int>(nameof(ReaderCount), 12480);

    /// <summary>Whether the readers row (faces and count) shows (true).</summary>
    public static readonly StyledProperty<bool> IsReadersVisibleProperty =
        AvaloniaProperty.Register<NewsletterSignup, bool>(nameof(IsReadersVisible), true);

    /// <summary>The publication behind the stack. Null shows Arc's sample publication "Margins" (without thumbnails).</summary>
    public static readonly StyledProperty<NewsletterPublication?> PublicationProperty =
        AvaloniaProperty.Register<NewsletterSignup, NewsletterPublication?>(nameof(Publication));

    /// <summary>Whether the stack of issues shows (true). Set false for the form alone.</summary>
    public static readonly StyledProperty<bool> IsPublicationVisibleProperty =
        AvaloniaProperty.Register<NewsletterSignup, bool>(nameof(IsPublicationVisible), true);

    private readonly AvaloniaList<IImage> _readerFaces = new();

    /// <summary>The faces next to the reader count (the first three are shown).</summary>
    public static readonly DirectProperty<NewsletterSignup, AvaloniaList<IImage>> ReaderFacesProperty =
        AvaloniaProperty.RegisterDirect<NewsletterSignup, AvaloniaList<IImage>>(nameof(ReaderFaces), o => o.ReaderFaces);

    #endregion

    #region State and events

    private NewsletterPhase _phase;
    private string? _subscribedEmail;

    /// <summary>Where the subscription stands: Idle, Sending or Done (read only).</summary>
    public static readonly DirectProperty<NewsletterSignup, NewsletterPhase> PhaseProperty =
        AvaloniaProperty.RegisterDirect<NewsletterSignup, NewsletterPhase>(nameof(Phase), o => o.Phase);

    /// <summary>The address that was subscribed, or null before that (read only).</summary>
    public static readonly DirectProperty<NewsletterSignup, string?> SubscribedEmailProperty =
        AvaloniaProperty.RegisterDirect<NewsletterSignup, string?>(nameof(SubscribedEmail), o => o.SubscribedEmail);

    /// <summary>Runs when a subscription succeeds; its parameter is the address.</summary>
    public static readonly StyledProperty<ICommand?> SubscribedCommandProperty =
        AvaloniaProperty.Register<NewsletterSignup, ICommand?>(nameof(SubscribedCommand));

    /// <summary>A subscription succeeded: the next issue now sits on the front of the stack.</summary>
    public static readonly RoutedEvent<NewsletterSubscribedEventArgs> SubscribedEvent =
        RoutedEvent.Register<NewsletterSignup, NewsletterSubscribedEventArgs>(nameof(Subscribed), RoutingStrategies.Bubble);

    /// <summary>The privacy link was pressed. Mark the event handled to stop the default (opening an absolute address).</summary>
    public static readonly RoutedEvent<NewsletterNavigateEventArgs> NavigateEvent =
        RoutedEvent.Register<NewsletterSignup, NewsletterNavigateEventArgs>(nameof(Navigate), RoutingStrategies.Bubble);

    #endregion

    #region Labels

    /// <summary>The hidden label of the email field ("Email address").</summary>
    public static readonly StyledProperty<string> EmailLabelProperty =
        AvaloniaProperty.Register<NewsletterSignup, string>(nameof(EmailLabel), "Email address");

    /// <summary>The button label after a failed send ("Try again").</summary>
    public static readonly StyledProperty<string> RetryLabelProperty =
        AvaloniaProperty.Register<NewsletterSignup, string>(nameof(RetryLabel), "Try again");

    /// <summary>The button label while sending ("Subscribing").</summary>
    public static readonly StyledProperty<string> SendingLabelProperty =
        AvaloniaProperty.Register<NewsletterSignup, string>(nameof(SendingLabel), "Subscribing");

    /// <summary>The button label once subscribed ("Subscribed").</summary>
    public static readonly StyledProperty<string> DoneLabelProperty =
        AvaloniaProperty.Register<NewsletterSignup, string>(nameof(DoneLabel), "Subscribed");

    /// <summary>The message for an empty field ("Enter your email address").</summary>
    public static readonly StyledProperty<string> EmptyMessageProperty =
        AvaloniaProperty.Register<NewsletterSignup, string>(nameof(EmptyMessage), "Enter your email address");

    /// <summary>The message for a malformed address ("Enter an email like name@company.com").</summary>
    public static readonly StyledProperty<string> FormatMessageProperty =
        AvaloniaProperty.Register<NewsletterSignup, string>(nameof(FormatMessage), "Enter an email like name@company.com");

    /// <summary>The message after a failed send.</summary>
    public static readonly StyledProperty<string> FailedMessageProperty =
        AvaloniaProperty.Register<NewsletterSignup, string>(nameof(FailedMessage), "That didn't go through. Your email is kept, so try again.");

    /// <summary>The confirmation after a successful send ("Check your inbox to confirm.").</summary>
    public static readonly StyledProperty<string> DoneMessageProperty =
        AvaloniaProperty.Register<NewsletterSignup, string>(nameof(DoneMessage), "Check your inbox to confirm.");

    /// <summary>The label of the button that starts over ("Use a different email").</summary>
    public static readonly StyledProperty<string> DifferentEmailLabelProperty =
        AvaloniaProperty.Register<NewsletterSignup, string>(nameof(DifferentEmailLabel), "Use a different email");

    /// <summary>The recipient line of an addressed issue; {0} is the address ("To {0}").</summary>
    public static readonly StyledProperty<string> RecipientFormatProperty =
        AvaloniaProperty.Register<NewsletterSignup, string>(nameof(RecipientFormat), "To {0}");

    /// <summary>The issue number at the top of an issue; {0} is the number ("Issue {0}").</summary>
    public static readonly StyledProperty<string> IssueFormatProperty =
        AvaloniaProperty.Register<NewsletterSignup, string>(nameof(IssueFormat), "Issue {0}");

    /// <summary>The reading time of a story; {0} is the minutes ("{0} min").</summary>
    public static readonly StyledProperty<string> MinutesFormatProperty =
        AvaloniaProperty.Register<NewsletterSignup, string>(nameof(MinutesFormat), "{0} min");

    /// <summary>The screen reader name of the front issue; {0} is the publication, {1} the number ("{0}, issue {1}").</summary>
    public static readonly StyledProperty<string> IssueNameFormatProperty =
        AvaloniaProperty.Register<NewsletterSignup, string>(nameof(IssueNameFormat), "{0}, issue {1}");

    /// <summary>The word after the reader count ("readers").</summary>
    public static readonly StyledProperty<string> ReadersWordProperty =
        AvaloniaProperty.Register<NewsletterSignup, string>(nameof(ReadersWord), "readers");

    /// <summary>The phrase that opens after the count once subscribed (", including you").</summary>
    public static readonly StyledProperty<string> IncludingYouProperty =
        AvaloniaProperty.Register<NewsletterSignup, string>(nameof(IncludingYou), ", including you");

    /// <summary>What the screen reader adds to the confirmation; {0} is the address (" The link went to {0}.").</summary>
    public static readonly StyledProperty<string> SentToFormatProperty =
        AvaloniaProperty.Register<NewsletterSignup, string>(nameof(SentToFormat), " The link went to {0}.");

    #endregion

    #region Template properties

    internal static readonly DirectProperty<NewsletterSignup, string?> DisplayTitleProperty =
        AvaloniaProperty.RegisterDirect<NewsletterSignup, string?>(nameof(DisplayTitle), o => o.DisplayTitle);

    internal static readonly DirectProperty<NewsletterSignup, string?> DisplayDescriptionProperty =
        AvaloniaProperty.RegisterDirect<NewsletterSignup, string?>(nameof(DisplayDescription), o => o.DisplayDescription);

    internal static readonly DirectProperty<NewsletterSignup, double> TitleFontSizeProperty =
        AvaloniaProperty.RegisterDirect<NewsletterSignup, double>(nameof(TitleFontSize), o => o.TitleFontSize);

    internal static readonly DirectProperty<NewsletterSignup, double> TitleLineHeightProperty =
        AvaloniaProperty.RegisterDirect<NewsletterSignup, double>(nameof(TitleLineHeight), o => o.TitleLineHeight);

    internal static readonly DirectProperty<NewsletterSignup, double> TitleLetterSpacingProperty =
        AvaloniaProperty.RegisterDirect<NewsletterSignup, double>(nameof(TitleLetterSpacing), o => o.TitleLetterSpacing);

    #endregion

    /// <inheritdoc cref="VariantProperty"/>
    public NewsletterVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="PlaceholderProperty"/>
    public string Placeholder { get => GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }

    /// <inheritdoc cref="ButtonLabelProperty"/>
    public string ButtonLabel { get => GetValue(ButtonLabelProperty); set => SetValue(ButtonLabelProperty, value); }

    /// <inheritdoc cref="PrivacyNoteProperty"/>
    public string PrivacyNote { get => GetValue(PrivacyNoteProperty); set => SetValue(PrivacyNoteProperty, value); }

    /// <inheritdoc cref="PrivacyLinkLabelProperty"/>
    public string PrivacyLinkLabel { get => GetValue(PrivacyLinkLabelProperty); set => SetValue(PrivacyLinkLabelProperty, value); }

    /// <inheritdoc cref="PrivacyLinkHrefProperty"/>
    public string? PrivacyLinkHref { get => GetValue(PrivacyLinkHrefProperty); set => SetValue(PrivacyLinkHrefProperty, value); }

    /// <inheritdoc cref="IsPrivacyLinkVisibleProperty"/>
    public bool IsPrivacyLinkVisible { get => GetValue(IsPrivacyLinkVisibleProperty); set => SetValue(IsPrivacyLinkVisibleProperty, value); }

    /// <inheritdoc cref="NavigateCommandProperty"/>
    public ICommand? NavigateCommand { get => GetValue(NavigateCommandProperty); set => SetValue(NavigateCommandProperty, value); }

    /// <inheritdoc cref="ReaderCountProperty"/>
    public int ReaderCount { get => GetValue(ReaderCountProperty); set => SetValue(ReaderCountProperty, value); }

    /// <inheritdoc cref="IsReadersVisibleProperty"/>
    public bool IsReadersVisible { get => GetValue(IsReadersVisibleProperty); set => SetValue(IsReadersVisibleProperty, value); }

    /// <inheritdoc cref="ReaderFacesProperty"/>
    public AvaloniaList<IImage> ReaderFaces => _readerFaces;

    /// <inheritdoc cref="PublicationProperty"/>
    public NewsletterPublication? Publication { get => GetValue(PublicationProperty); set => SetValue(PublicationProperty, value); }

    /// <inheritdoc cref="IsPublicationVisibleProperty"/>
    public bool IsPublicationVisible { get => GetValue(IsPublicationVisibleProperty); set => SetValue(IsPublicationVisibleProperty, value); }

    /// <summary>
    /// Called with a valid, trimmed address and a token that is cancelled when the block is reset or removed. Throw to show
    /// an error and keep the address; return to show the success state. Null simulates a send that succeeds after 1100 ms.
    /// </summary>
    public Func<string, CancellationToken, Task>? SubscribeAsync { get; set; }

    /// <inheritdoc cref="PhaseProperty"/>
    public NewsletterPhase Phase => _phase;

    /// <inheritdoc cref="SubscribedEmailProperty"/>
    public string? SubscribedEmail => _subscribedEmail;

    /// <inheritdoc cref="SubscribedCommandProperty"/>
    public ICommand? SubscribedCommand { get => GetValue(SubscribedCommandProperty); set => SetValue(SubscribedCommandProperty, value); }

    /// <inheritdoc cref="SubscribedEvent"/>
    public event EventHandler<NewsletterSubscribedEventArgs>? Subscribed
    {
        add => AddHandler(SubscribedEvent, value);
        remove => RemoveHandler(SubscribedEvent, value);
    }

    /// <inheritdoc cref="NavigateEvent"/>
    public event EventHandler<NewsletterNavigateEventArgs>? Navigate
    {
        add => AddHandler(NavigateEvent, value);
        remove => RemoveHandler(NavigateEvent, value);
    }

    /// <inheritdoc cref="EmailLabelProperty"/>
    public string EmailLabel { get => GetValue(EmailLabelProperty); set => SetValue(EmailLabelProperty, value); }

    /// <inheritdoc cref="RetryLabelProperty"/>
    public string RetryLabel { get => GetValue(RetryLabelProperty); set => SetValue(RetryLabelProperty, value); }

    /// <inheritdoc cref="SendingLabelProperty"/>
    public string SendingLabel { get => GetValue(SendingLabelProperty); set => SetValue(SendingLabelProperty, value); }

    /// <inheritdoc cref="DoneLabelProperty"/>
    public string DoneLabel { get => GetValue(DoneLabelProperty); set => SetValue(DoneLabelProperty, value); }

    /// <inheritdoc cref="EmptyMessageProperty"/>
    public string EmptyMessage { get => GetValue(EmptyMessageProperty); set => SetValue(EmptyMessageProperty, value); }

    /// <inheritdoc cref="FormatMessageProperty"/>
    public string FormatMessage { get => GetValue(FormatMessageProperty); set => SetValue(FormatMessageProperty, value); }

    /// <inheritdoc cref="FailedMessageProperty"/>
    public string FailedMessage { get => GetValue(FailedMessageProperty); set => SetValue(FailedMessageProperty, value); }

    /// <inheritdoc cref="DoneMessageProperty"/>
    public string DoneMessage { get => GetValue(DoneMessageProperty); set => SetValue(DoneMessageProperty, value); }

    /// <inheritdoc cref="DifferentEmailLabelProperty"/>
    public string DifferentEmailLabel { get => GetValue(DifferentEmailLabelProperty); set => SetValue(DifferentEmailLabelProperty, value); }

    /// <inheritdoc cref="RecipientFormatProperty"/>
    public string RecipientFormat { get => GetValue(RecipientFormatProperty); set => SetValue(RecipientFormatProperty, value); }

    /// <inheritdoc cref="IssueFormatProperty"/>
    public string IssueFormat { get => GetValue(IssueFormatProperty); set => SetValue(IssueFormatProperty, value); }

    /// <inheritdoc cref="MinutesFormatProperty"/>
    public string MinutesFormat { get => GetValue(MinutesFormatProperty); set => SetValue(MinutesFormatProperty, value); }

    /// <inheritdoc cref="IssueNameFormatProperty"/>
    public string IssueNameFormat { get => GetValue(IssueNameFormatProperty); set => SetValue(IssueNameFormatProperty, value); }

    /// <inheritdoc cref="ReadersWordProperty"/>
    public string ReadersWord { get => GetValue(ReadersWordProperty); set => SetValue(ReadersWordProperty, value); }

    /// <inheritdoc cref="IncludingYouProperty"/>
    public string IncludingYou { get => GetValue(IncludingYouProperty); set => SetValue(IncludingYouProperty, value); }

    /// <inheritdoc cref="SentToFormatProperty"/>
    public string SentToFormat { get => GetValue(SentToFormatProperty); set => SetValue(SentToFormatProperty, value); }

    internal string? DisplayTitle => Title ?? (Variant == NewsletterVariant.Card ? "Get the next issue on Friday" : "Five minutes on interface craft, every Friday");

    internal string? DisplayDescription => Description ?? (Variant == NewsletterVariant.Card
        ? "Three links and one short essay on interface craft. Five minutes, once a week."
        : "Three links worth your time and one short essay on the details that make software feel right.");

    internal double TitleFontSize => _titleFontSize;

    internal double TitleLineHeight => _titleFontSize * 1.1;

    internal double TitleLetterSpacing => _titleFontSize * -0.03;

    private double _titleFontSize = 52;

    static NewsletterSignup()
    {
        AutomationProperties.LandmarkTypeProperty.OverrideDefaultValue<NewsletterSignup>(AutomationLandmarkType.Region);
        FocusableProperty.OverrideDefaultValue<NewsletterSignup>(false);
        AffectsMeasure<NewsletterSignup>(VariantProperty);
    }
}
