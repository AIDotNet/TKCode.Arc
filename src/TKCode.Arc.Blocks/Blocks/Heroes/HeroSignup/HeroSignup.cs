using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The first section of a landing page built around one action: leaving an email address. An eyebrow, a large headline
/// and a sentence of copy sit over a quiet dot field; under them an email field and a button validate the address, send
/// it through <see cref="SubmitAsync"/> and confirm in place, where the form was. A row of faces and a line of proof
/// close the section.
/// <code>
/// &lt;arc:HeroSignup Title="Plan the launch, not the spreadsheet" ButtonText="Join the waitlist" /&gt;
/// </code>
/// </summary>
/// <remarks>
/// Validation follows the address field: an empty field and a malformed address each have their own message
/// (<see cref="EmptyMessage"/>, <see cref="FormatMessage"/>). Before the first submit, typing clears an error; after it,
/// every keystroke checks again. While <see cref="Phase"/> is <see cref="HeroSignupPhase.Sending"/> the field is
/// read-only and the button shows a spinner; an exception from <see cref="SubmitAsync"/> keeps the address and shows
/// <see cref="FailedMessage"/>. Without a handler the send is simulated and takes 900 ms.
/// </remarks>
[TemplatePart(EmailPart, typeof(Input))]
[TemplatePart(SubmitPart, typeof(Button))]
[TemplatePart(ResetPart, typeof(Button))]
[TemplatePart(FormPart, typeof(PresenceHost))]
[TemplatePart(DonePart, typeof(PresenceHost))]
[TemplatePart(AnnouncerPart, typeof(SiteAnnouncer))]
[PseudoClasses(SendingPseudoClass, DonePseudoClass, StartPseudoClass, CompactPseudoClass, NarrowPseudoClass, NoProofPseudoClass)]
public partial class HeroSignup : TemplatedControl
{
    public const string EmailPart = "PART_Email";
    public const string SubmitPart = "PART_Submit";
    public const string ResetPart = "PART_Reset";
    public const string FormPart = "PART_Form";
    public const string DonePart = "PART_Done";
    public const string AnnouncerPart = "PART_Announcer";
    public const string SendingPseudoClass = ":sending";
    public const string DonePseudoClass = ":done";
    public const string StartPseudoClass = ":start";
    public const string CompactPseudoClass = ":compact";
    public const string NarrowPseudoClass = ":narrow";
    public const string NoProofPseudoClass = ":no-proof";

    /// <summary>The short line in a pill above the headline; null leaves it out.</summary>
    public static readonly StyledProperty<string?> EyebrowProperty =
        AvaloniaProperty.Register<HeroSignup, string?>(nameof(Eyebrow), "Private beta · Spring cohort");

    /// <summary>The headline.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<HeroSignup, string?>(nameof(Title), "Release notes your whole team reads");

    /// <summary>The copy under the headline; null leaves it out.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<HeroSignup, string?>(nameof(Description),
            "Draft them from merged work, tune them for each audience and send them where people already are. Leave your email and we will save you a seat.");

    /// <summary>The field's placeholder.</summary>
    public static readonly StyledProperty<string> PlaceholderProperty =
        AvaloniaProperty.Register<HeroSignup, string>(nameof(Placeholder), "you@company.com");

    /// <summary>The field's screen reader name.</summary>
    public static readonly StyledProperty<string> EmailLabelProperty =
        AvaloniaProperty.Register<HeroSignup, string>(nameof(EmailLabel), "Work email");

    /// <summary>The button's text.</summary>
    public static readonly StyledProperty<string> ButtonTextProperty =
        AvaloniaProperty.Register<HeroSignup, string>(nameof(ButtonText), "Join the waitlist");

    /// <summary>The button's text while the address is being sent.</summary>
    public static readonly StyledProperty<string> SendingTextProperty =
        AvaloniaProperty.Register<HeroSignup, string>(nameof(SendingText), "Joining");

    /// <summary>The helper line under the field; null leaves it out.</summary>
    public static readonly StyledProperty<string?> NoteProperty =
        AvaloniaProperty.Register<HeroSignup, string?>(nameof(Note), "Free while in beta. No card, no spam.");

    /// <summary>The error for an empty field.</summary>
    public static readonly StyledProperty<string> EmptyMessageProperty =
        AvaloniaProperty.Register<HeroSignup, string>(nameof(EmptyMessage), "Enter your email address.");

    /// <summary>The error for an address that is not one.</summary>
    public static readonly StyledProperty<string> FormatMessageProperty =
        AvaloniaProperty.Register<HeroSignup, string>(nameof(FormatMessage), "That address doesn't look right. Check for typos.");

    /// <summary>The error when <see cref="SubmitAsync"/> throws.</summary>
    public static readonly StyledProperty<string> FailedMessageProperty =
        AvaloniaProperty.Register<HeroSignup, string>(nameof(FailedMessage), "We couldn't add you just now. Try again in a moment.");

    /// <summary>The confirmation's heading.</summary>
    public static readonly StyledProperty<string> DoneTitleProperty =
        AvaloniaProperty.Register<HeroSignup, string>(nameof(DoneTitle), "You're on the list");

    /// <summary>The confirmation's line; {0} is the address.</summary>
    public static readonly StyledProperty<string> DoneFormatProperty =
        AvaloniaProperty.Register<HeroSignup, string>(nameof(DoneFormat), "We sent a confirmation to {0}.");

    /// <summary>The text of the button that brings the form back.</summary>
    public static readonly StyledProperty<string> ResetTextProperty =
        AvaloniaProperty.Register<HeroSignup, string>(nameof(ResetText), "Use a different email");

    /// <summary>The line of proof beside the faces; null hides the line and the faces.</summary>
    public static readonly StyledProperty<string?> ProofTextProperty =
        AvaloniaProperty.Register<HeroSignup, string?>(nameof(ProofText), "Joined by 2,400 product teams");

    /// <summary>
    /// The people drawn as faces beside the proof line (at most five), each as a generated portrait from the name.
    /// Empty uses five invented names; set <see cref="ProofText"/> to null to hide the row.
    /// </summary>
    public static readonly StyledProperty<IList<string>?> ProofNamesProperty =
        AvaloniaProperty.Register<HeroSignup, IList<string>?>(nameof(ProofNames));

    /// <summary>How the content lines up.</summary>
    public static readonly StyledProperty<HeroSignupAlignment> AlignmentProperty =
        AvaloniaProperty.Register<HeroSignup, HeroSignupAlignment>(nameof(Alignment));

    /// <summary>Whether the dot field behind the content shows.</summary>
    public static readonly StyledProperty<bool> IsBackdropVisibleProperty =
        AvaloniaProperty.Register<HeroSignup, bool>(nameof(IsBackdropVisible), true);

    /// <summary>
    /// Sends the address: awaited with the trimmed address and a token that <see cref="Reset"/> (or the block leaving
    /// the window) cancels. Return to confirm; throw to show <see cref="FailedMessage"/> and keep the address. Null
    /// simulates a send of 900 ms.
    /// </summary>
    public static readonly StyledProperty<Func<string, CancellationToken, Task>?> SubmitAsyncProperty =
        AvaloniaProperty.Register<HeroSignup, Func<string, CancellationToken, Task>?>(nameof(SubmitAsync));

    /// <summary>Run with the address once it was accepted (after <see cref="SubmitAsync"/> returned).</summary>
    public static readonly StyledProperty<ICommand?> SubmitCommandProperty =
        AvaloniaProperty.Register<HeroSignup, ICommand?>(nameof(SubmitCommand));

    /// <summary>Below this width of the block (760) the headline steps down a size.</summary>
    public static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<HeroSignup, double>(nameof(CompactWidth), 760);

    /// <summary>Below this width of the block (480) the headline steps down again and the field and button stack.</summary>
    public static readonly StyledProperty<double> NarrowWidthProperty =
        AvaloniaProperty.Register<HeroSignup, double>(nameof(NarrowWidth), 480);

    /// <summary>Where the signup is (read-only).</summary>
    public static readonly DirectProperty<HeroSignup, HeroSignupPhase> PhaseProperty =
        AvaloniaProperty.RegisterDirect<HeroSignup, HeroSignupPhase>(nameof(Phase), o => o.Phase);

    /// <summary>The address that was accepted; null until then and after <see cref="Reset"/> (read-only).</summary>
    public static readonly DirectProperty<HeroSignup, string?> SubmittedEmailProperty =
        AvaloniaProperty.RegisterDirect<HeroSignup, string?>(nameof(SubmittedEmail), o => o.SubmittedEmail);

    /// <summary>The confirmation's line with the address filled in (read-only; the template shows it).</summary>
    public static readonly DirectProperty<HeroSignup, string?> DoneTextProperty =
        AvaloniaProperty.RegisterDirect<HeroSignup, string?>(nameof(DoneText), o => o.DoneText);

    /// <summary>Raised when an address was accepted.</summary>
    public static readonly RoutedEvent<HeroSignupSubmittedEventArgs> SubmittedEvent =
        RoutedEvent.Register<HeroSignup, HeroSignupSubmittedEventArgs>(nameof(Submitted), RoutingStrategies.Bubble);

    internal static readonly string[] SampleProofNames = ["Ada Brennan", "Kofi Mensah", "Lucía Ferro", "Ravi Kapoor", "Elin Strand"];

    private HeroSignupPhase _phase;
    private string? _submittedEmail;
    private string? _doneText;

    static HeroSignup()
    {
        ClipToBoundsProperty.OverrideDefaultValue<HeroSignup>(false);
    }

    /// <inheritdoc cref="EyebrowProperty"/>
    public string? Eyebrow { get => GetValue(EyebrowProperty); set => SetValue(EyebrowProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="PlaceholderProperty"/>
    public string Placeholder { get => GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }

    /// <inheritdoc cref="EmailLabelProperty"/>
    public string EmailLabel { get => GetValue(EmailLabelProperty); set => SetValue(EmailLabelProperty, value); }

    /// <inheritdoc cref="ButtonTextProperty"/>
    public string ButtonText { get => GetValue(ButtonTextProperty); set => SetValue(ButtonTextProperty, value); }

    /// <inheritdoc cref="SendingTextProperty"/>
    public string SendingText { get => GetValue(SendingTextProperty); set => SetValue(SendingTextProperty, value); }

    /// <inheritdoc cref="NoteProperty"/>
    public string? Note { get => GetValue(NoteProperty); set => SetValue(NoteProperty, value); }

    /// <inheritdoc cref="EmptyMessageProperty"/>
    public string EmptyMessage { get => GetValue(EmptyMessageProperty); set => SetValue(EmptyMessageProperty, value); }

    /// <inheritdoc cref="FormatMessageProperty"/>
    public string FormatMessage { get => GetValue(FormatMessageProperty); set => SetValue(FormatMessageProperty, value); }

    /// <inheritdoc cref="FailedMessageProperty"/>
    public string FailedMessage { get => GetValue(FailedMessageProperty); set => SetValue(FailedMessageProperty, value); }

    /// <inheritdoc cref="DoneTitleProperty"/>
    public string DoneTitle { get => GetValue(DoneTitleProperty); set => SetValue(DoneTitleProperty, value); }

    /// <inheritdoc cref="DoneFormatProperty"/>
    public string DoneFormat { get => GetValue(DoneFormatProperty); set => SetValue(DoneFormatProperty, value); }

    /// <inheritdoc cref="ResetTextProperty"/>
    public string ResetText { get => GetValue(ResetTextProperty); set => SetValue(ResetTextProperty, value); }

    /// <inheritdoc cref="ProofTextProperty"/>
    public string? ProofText { get => GetValue(ProofTextProperty); set => SetValue(ProofTextProperty, value); }

    /// <inheritdoc cref="ProofNamesProperty"/>
    public IList<string>? ProofNames { get => GetValue(ProofNamesProperty); set => SetValue(ProofNamesProperty, value); }

    /// <inheritdoc cref="AlignmentProperty"/>
    public HeroSignupAlignment Alignment { get => GetValue(AlignmentProperty); set => SetValue(AlignmentProperty, value); }

    /// <inheritdoc cref="IsBackdropVisibleProperty"/>
    public bool IsBackdropVisible { get => GetValue(IsBackdropVisibleProperty); set => SetValue(IsBackdropVisibleProperty, value); }

    /// <inheritdoc cref="SubmitAsyncProperty"/>
    public Func<string, CancellationToken, Task>? SubmitAsync { get => GetValue(SubmitAsyncProperty); set => SetValue(SubmitAsyncProperty, value); }

    /// <inheritdoc cref="SubmitCommandProperty"/>
    public ICommand? SubmitCommand { get => GetValue(SubmitCommandProperty); set => SetValue(SubmitCommandProperty, value); }

    /// <inheritdoc cref="CompactWidthProperty"/>
    public double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }

    /// <inheritdoc cref="NarrowWidthProperty"/>
    public double NarrowWidth { get => GetValue(NarrowWidthProperty); set => SetValue(NarrowWidthProperty, value); }

    /// <inheritdoc cref="PhaseProperty"/>
    public HeroSignupPhase Phase { get => _phase; private set => SetAndRaise(PhaseProperty, ref _phase, value); }

    /// <inheritdoc cref="SubmittedEmailProperty"/>
    public string? SubmittedEmail { get => _submittedEmail; private set => SetAndRaise(SubmittedEmailProperty, ref _submittedEmail, value); }

    /// <inheritdoc cref="DoneTextProperty"/>
    public string? DoneText { get => _doneText; private set => SetAndRaise(DoneTextProperty, ref _doneText, value); }

    /// <inheritdoc cref="SubmittedEvent"/>
    public event EventHandler<HeroSignupSubmittedEventArgs>? Submitted
    {
        add => AddHandler(SubmittedEvent, value);
        remove => RemoveHandler(SubmittedEvent, value);
    }

    /// <summary>The names the faces are drawn from: <see cref="ProofNames"/>, or the invented ones when it is empty (at most five).</summary>
    internal IReadOnlyList<string> EffectiveProofNames
        => (ProofNames is { Count: > 0 } names ? names : SampleProofNames).Take(5).ToList();
}
