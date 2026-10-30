using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A brand voice card (Arc Tone tuner, design reference): a customer reply with Friendly, Confident and Empathetic voice
/// chips and two sliders, formality and detail, that rewrite it. Each change waits <see cref="Debounce"/> and then asks
/// <see cref="Rewrite"/> for a new version; the sentences that changed crossfade in place while the rest stay still, words
/// that are new against the original can be highlighted (the Changes switch), and Copy takes the version on screen. The
/// status says where the text comes from: Original draft, Rewriting with the model (shimmering), Rewritten, or Couldn't
/// rewrite with Retry.
/// <para>
/// Give it the draft in <see cref="Text"/> and your model call in <see cref="Rewrite"/>; without <see cref="Rewrite"/> the
/// rewrites are simulated by a few plain rules. Without a draft it plays a <see cref="Simulation"/> (a draft and its
/// versions you pass, toured by itself until someone touches it); with neither, it shows an empty card whose dials are off.
/// </para>
/// </summary>
[TemplatePart(AvatarPart, typeof(Avatar))]
[TemplatePart(NamePart, typeof(TextBlock))]
[TemplatePart(SubjectPart, typeof(TextBlock))]
[TemplatePart(CountPart, typeof(Control))]
[TemplatePart(CountTextPart, typeof(TextBlock))]
[TemplatePart(ChipsPart, typeof(Panel))]
[TemplatePart(SlidersPart, typeof(Panel))]
[TemplatePart(FormalityPart, typeof(Slider))]
[TemplatePart(DetailPart, typeof(Slider))]
[TemplatePart(MessagePart, typeof(Control))]
[TemplatePart(StatusPanelPart, typeof(Control))]
[TemplatePart(StatusGlyphPart, typeof(ArcGlyph))]
[TemplatePart(StatusPart, typeof(Control))]
[TemplatePart(SettingsPart, typeof(TextBlock))]
[TemplatePart(RetryPart, typeof(Button))]
[TemplatePart(ChangesPart, typeof(Switch))]
[TemplatePart(CopyPart, typeof(CopyButton))]
[PseudoClasses(OriginalPseudoClass, RewritingPseudoClass, RewrittenPseudoClass, FailedPseudoClass, StackedPseudoClass, CompactPseudoClass)]
public partial class ToneTuner : TemplatedControl
{
    internal const string AvatarPart = "PART_Avatar";
    internal const string NamePart = "PART_Name";
    internal const string SubjectPart = "PART_Subject";
    internal const string CountPart = "PART_Count";
    internal const string CountTextPart = "PART_CountText";
    internal const string ChipsPart = "PART_Chips";
    internal const string SlidersPart = "PART_Sliders";
    internal const string FormalityPart = "PART_Formality";
    internal const string DetailPart = "PART_Detail";
    internal const string MessagePart = "PART_Message";
    internal const string StatusPanelPart = "PART_StatusPanel";
    internal const string StatusGlyphPart = "PART_StatusGlyph";
    internal const string StatusPart = "PART_Status";
    internal const string SettingsPart = "PART_Settings";
    internal const string RetryPart = "PART_Retry";
    internal const string ChangesPart = "PART_Changes";
    internal const string CopyPart = "PART_Copy";

    internal const string OriginalPseudoClass = ":original";
    internal const string RewritingPseudoClass = ":rewriting";
    internal const string RewrittenPseudoClass = ":rewritten";
    internal const string FailedPseudoClass = ":failed";
    internal const string StackedPseudoClass = ":stacked";
    internal const string CompactPseudoClass = ":compact";

    /// <summary>
    /// The original draft, the source of every rewrite. Blank lines split paragraphs; single line breaks keep lines (a sign
    /// off). Null plays <see cref="Simulation"/>, or shows the empty card without one.
    /// </summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ToneTuner, string?>(nameof(Text));

    /// <summary>
    /// Calls your model and returns the rewritten plain text. The request carries the draft, the tone and a ready made
    /// instruction; the token is cancelled when a newer change supersedes the request. Null simulates the rewrites of your
    /// draft with a few plain rules (nothing is sent).
    /// </summary>
    public static readonly StyledProperty<Func<ToneRewriteRequest, CancellationToken, Task<string>>?> RewriteProperty =
        AvaloniaProperty.Register<ToneTuner, Func<ToneRewriteRequest, CancellationToken, Task<string>>?>(nameof(Rewrite));

    /// <summary>The tone (two-way): formality and detail (0–2) and the voice. The original draft shows at <see cref="ToneSettings.Default"/>.</summary>
    public static readonly StyledProperty<ToneSettings> ValueProperty =
        AvaloniaProperty.Register<ToneTuner, ToneSettings>(nameof(Value), ToneSettings.Default, defaultBindingMode: BindingMode.TwoWay,
            coerce: (_, value) => value ?? ToneSettings.Default);

    /// <summary>Highlights the words that differ from the original draft (two-way; the Changes switch).</summary>
    public static readonly StyledProperty<bool> ShowChangesProperty =
        AvaloniaProperty.Register<ToneTuner, bool>(nameof(ShowChanges), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Who the message is for, shown in the header with an avatar. Null while a <see cref="Simulation"/> plays shows its recipient.</summary>
    public static readonly StyledProperty<string?> RecipientNameProperty =
        AvaloniaProperty.Register<ToneTuner, string?>(nameof(RecipientName));

    /// <summary>The subject under the recipient's name.</summary>
    public static readonly StyledProperty<string?> RecipientSubjectProperty =
        AvaloniaProperty.Register<ToneTuner, string?>(nameof(RecipientSubject));

    /// <summary>The recipient's picture; without it the avatar shows their initials.</summary>
    public static readonly StyledProperty<ImageSource?> RecipientAvatarProperty =
        AvaloniaProperty.Register<ToneTuner, ImageSource?>(nameof(RecipientAvatar));

    /// <summary>The model named in the status while a rewrite runs; null names the <see cref="Simulation"/>'s model while one plays, or none.</summary>
    public static readonly StyledProperty<string?> ModelProperty =
        AvaloniaProperty.Register<ToneTuner, string?>(nameof(Model));

    /// <summary>The voice chips; null shows Friendly, Confident and Empathetic (<see cref="TonePreset.Defaults"/>).</summary>
    public static readonly StyledProperty<IReadOnlyList<TonePreset>?> PresetsProperty =
        AvaloniaProperty.Register<ToneTuner, IReadOnlyList<TonePreset>?>(nameof(Presets));

    /// <summary>Quiet time after the last change before a rewrite starts (default 220ms).</summary>
    public static readonly StyledProperty<TimeSpan> DebounceProperty =
        AvaloniaProperty.Register<ToneTuner, TimeSpan>(nameof(Debounce), TimeSpan.FromMilliseconds(220));

    /// <summary>
    /// Tours the voices while the card is on screen, until the first pointer press or focus inside it. Null (default)
    /// tours only while a <see cref="Simulation"/> plays. Reduced motion never tours.
    /// </summary>
    public static readonly StyledProperty<bool?> AutoPlayProperty =
        AvaloniaProperty.Register<ToneTuner, bool?>(nameof(AutoPlay));

    /// <summary>
    /// A tuning session to play while the card has no <see cref="Text"/>: its draft, recipient, model and versions, with
    /// simulated rewrites (nothing is sent). Your own <see cref="Text"/> wins over it; null stops it and shows the empty card.
    /// </summary>
    public static readonly StyledProperty<ToneTunerSimulation?> SimulationProperty =
        AvaloniaProperty.Register<ToneTuner, ToneTunerSimulation?>(nameof(Simulation));

    /// <summary>Every word the card shows or says.</summary>
    public static readonly StyledProperty<ToneTunerLabels> LabelsProperty =
        AvaloniaProperty.Register<ToneTuner, ToneTunerLabels>(nameof(Labels), ToneTunerLabels.Default);

    /// <summary>Where the text on screen comes from: the original, a rewrite on its way, a landed rewrite, or a failure.</summary>
    public static readonly DirectProperty<ToneTuner, ToneTunerStatus> StatusProperty =
        AvaloniaProperty.RegisterDirect<ToneTuner, ToneTunerStatus>(nameof(Status), o => o.Status);

    /// <summary>The version on screen as plain text (what Copy takes).</summary>
    public static readonly DirectProperty<ToneTuner, string> DisplayedTextProperty =
        AvaloniaProperty.RegisterDirect<ToneTuner, string>(nameof(DisplayedText), o => o.DisplayedText);

    /// <summary>How many words of the version on screen are new against the original draft.</summary>
    public static readonly DirectProperty<ToneTuner, int> ChangedWordsProperty =
        AvaloniaProperty.RegisterDirect<ToneTuner, int>(nameof(ChangedWords), o => o.ChangedWords);

    /// <summary>Raised on every slider step or chip press (and when code sets <see cref="Value"/>), before the debounce. Bubbles.</summary>
    public static readonly RoutedEvent<ToneTunerValueChangedEventArgs> ValueChangedEvent =
        RoutedEvent.Register<ToneTuner, ToneTunerValueChangedEventArgs>(nameof(ValueChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when a rewrite lands, with the new plain text and its tone. Bubbles.</summary>
    public static readonly RoutedEvent<ToneTunerRewrittenEventArgs> RewrittenEvent =
        RoutedEvent.Register<ToneTuner, ToneTunerRewrittenEventArgs>(nameof(Rewritten), RoutingStrategies.Bubble);

    /// <summary>Raised after Copy put the version on screen on the clipboard. Bubbles.</summary>
    public static readonly RoutedEvent<ToneTunerCopiedEventArgs> CopiedEvent =
        RoutedEvent.Register<ToneTuner, ToneTunerCopiedEventArgs>(nameof(Copied), RoutingStrategies.Bubble);

    /// <summary>Below this card width the sliders stack, the chips share one row and the status hides its settings (theme).</summary>
    internal static readonly StyledProperty<double> StackWidthProperty =
        AvaloniaProperty.Register<ToneTuner, double>(nameof(StackWidth), 520);

    /// <summary>Below this card width the chip icons, the changed count and Copy's label drop (theme).</summary>
    internal static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<ToneTuner, double>(nameof(CompactWidth), 400);

    private ToneTunerStatus _status = ToneTunerStatus.Original;
    private string _displayedText = string.Empty;
    private int _changedWords;

    /// <inheritdoc cref="TextProperty"/>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc cref="RewriteProperty"/>
    public Func<ToneRewriteRequest, CancellationToken, Task<string>>? Rewrite { get => GetValue(RewriteProperty); set => SetValue(RewriteProperty, value); }

    /// <inheritdoc cref="ValueProperty"/>
    public ToneSettings Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <inheritdoc cref="ShowChangesProperty"/>
    public bool ShowChanges { get => GetValue(ShowChangesProperty); set => SetValue(ShowChangesProperty, value); }

    /// <inheritdoc cref="RecipientNameProperty"/>
    public string? RecipientName { get => GetValue(RecipientNameProperty); set => SetValue(RecipientNameProperty, value); }

    /// <inheritdoc cref="RecipientSubjectProperty"/>
    public string? RecipientSubject { get => GetValue(RecipientSubjectProperty); set => SetValue(RecipientSubjectProperty, value); }

    /// <inheritdoc cref="RecipientAvatarProperty"/>
    public ImageSource? RecipientAvatar { get => GetValue(RecipientAvatarProperty); set => SetValue(RecipientAvatarProperty, value); }

    /// <inheritdoc cref="ModelProperty"/>
    public string? Model { get => GetValue(ModelProperty); set => SetValue(ModelProperty, value); }

    /// <inheritdoc cref="PresetsProperty"/>
    public IReadOnlyList<TonePreset>? Presets { get => GetValue(PresetsProperty); set => SetValue(PresetsProperty, value); }

    /// <inheritdoc cref="DebounceProperty"/>
    public TimeSpan Debounce { get => GetValue(DebounceProperty); set => SetValue(DebounceProperty, value); }

    /// <inheritdoc cref="AutoPlayProperty"/>
    public bool? AutoPlay { get => GetValue(AutoPlayProperty); set => SetValue(AutoPlayProperty, value); }

    /// <inheritdoc cref="SimulationProperty"/>
    public ToneTunerSimulation? Simulation { get => GetValue(SimulationProperty); set => SetValue(SimulationProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public ToneTunerLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="StatusProperty"/>
    public ToneTunerStatus Status { get => _status; private set => SetAndRaise(StatusProperty, ref _status, value); }

    /// <inheritdoc cref="DisplayedTextProperty"/>
    public string DisplayedText { get => _displayedText; private set => SetAndRaise(DisplayedTextProperty, ref _displayedText, value); }

    /// <inheritdoc cref="ChangedWordsProperty"/>
    public int ChangedWords { get => _changedWords; private set => SetAndRaise(ChangedWordsProperty, ref _changedWords, value); }

    internal double StackWidth { get => GetValue(StackWidthProperty); set => SetValue(StackWidthProperty, value); }

    internal double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }

    /// <inheritdoc cref="ValueChangedEvent"/>
    public event EventHandler<ToneTunerValueChangedEventArgs>? ValueChanged
    {
        add => AddHandler(ValueChangedEvent, value);
        remove => RemoveHandler(ValueChangedEvent, value);
    }

    /// <inheritdoc cref="RewrittenEvent"/>
    public event EventHandler<ToneTunerRewrittenEventArgs>? Rewritten
    {
        add => AddHandler(RewrittenEvent, value);
        remove => RemoveHandler(RewrittenEvent, value);
    }

    /// <inheritdoc cref="CopiedEvent"/>
    public event EventHandler<ToneTunerCopiedEventArgs>? Copied
    {
        add => AddHandler(CopiedEvent, value);
        remove => RemoveHandler(CopiedEvent, value);
    }

    /// <summary>The simulation being played: <see cref="Simulation"/> while the card has no <see cref="Text"/>.</summary>
    internal ToneTunerSimulation? PlayingSimulation => Text is null ? Simulation : null;

    /// <summary>True when there is a draft to tune: your <see cref="Text"/> or a simulation's.</summary>
    internal bool HasDraft => Text is not null || Simulation is not null;

    /// <summary>True when the rewrites are simulated: a <see cref="Simulation"/> plays, or your draft has no <see cref="Rewrite"/>.</summary>
    internal bool IsSimulated => PlayingSimulation is not null || (Text is not null && Rewrite is null);

    /// <summary>True when a tone change can be rewritten (there is a draft).</summary>
    internal bool CanRewrite => HasDraft;

    /// <summary>The draft every rewrite starts from (empty with no data).</summary>
    internal string Original => Text is { } text ? ToneTunerText.Normalize(text) : PlayingSimulation?.Original ?? string.Empty;

    /// <summary>The model named in the status.</summary>
    internal string EffectiveModel => Model ?? PlayingSimulation?.Model ?? string.Empty;

    /// <summary>The chips on screen.</summary>
    internal IReadOnlyList<TonePreset> EffectivePresets => Presets ?? TonePreset.Defaults;

    /// <summary>The recipient shown in the header.</summary>
    internal string? EffectiveRecipientName => RecipientName ?? PlayingSimulation?.RecipientName;

    internal string? EffectiveRecipientSubject => RecipientSubject ?? (RecipientName is null ? PlayingSimulation?.RecipientSubject : null);

    internal ImageSource? EffectiveRecipientAvatar => RecipientAvatar ?? (RecipientName is null ? PlayingSimulation?.RecipientAvatar : null);

    /// <summary>Whether the voices tour themselves (before reduced motion and the first interaction).</summary>
    internal bool EffectiveAutoPlay => CanRewrite && (AutoPlay ?? PlayingSimulation is not null);
}
