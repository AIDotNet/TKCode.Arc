using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Metadata;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A paste-to-fill form card (Arc Smart form fill, design reference). People paste an email signature, a forwarded
/// message or messy notes into the well; AI reads it and fills the contact form field by field. Each value fades in with
/// a confidence dot, the text it came from lights up in the pasted text, fields below <see cref="ReviewThreshold"/> ask
/// for review (and hold Save until they are checked or edited), and Undo puts the values from before the fill back.
/// <para>
/// Give it your model through <see cref="Extract"/>: it receives the text and a cancellation token (Stop, Escape and a new
/// paste cancel) and returns a <see cref="SmartFormFillResult"/> with a value, a confidence, the exact source substring and
/// an optional note per field id. With no extractor the card uses a simulated reader (a sample's known result, a small
/// rule-based parser for anything else) and plays a <see cref="Simulation"/> if you give it one: its samples behind Try
/// sample and, when motion is full, a loop until someone touches it. <see cref="Submit"/> saves the form.
/// </para>
/// </summary>
[TemplatePart(CardPart, typeof(Border))]
[TemplatePart(TitlePart, typeof(TextBlock))]
[TemplatePart(DescriptionPart, typeof(TextBlock))]
[TemplatePart(BodyPart, typeof(Grid))]
[TemplatePart(WellPart, typeof(Border))]
[TemplatePart(WellHostPart, typeof(Panel))]
[TemplatePart(EditorPart, typeof(Textarea))]
[TemplatePart(SourceScrollPart, typeof(ScrollViewer))]
[TemplatePart(SourcePart, typeof(DecoratedTextBlock))]
[TemplatePart(ModelMarkPart, typeof(ArcGlyph))]
[TemplatePart(ModelNamePart, typeof(TextBlock))]
[TemplatePart(SamplePart, typeof(AvaloniaButton))]
[TemplatePart(FillPart, typeof(AvaloniaButton))]
[TemplatePart(EditPart, typeof(AvaloniaButton))]
[TemplatePart(FormPart, typeof(Panel))]
[TemplatePart(FooterPart, typeof(Grid))]
[TemplatePart(StatusPart, typeof(ArcContentSwap))]
[TemplatePart(ActionsPart, typeof(StackPanel))]
[TemplatePart(UndoPart, typeof(AvaloniaButton))]
[TemplatePart(SubmitPart, typeof(AvaloniaButton))]
[PseudoClasses(ReadingPseudoClass, SourcePseudoClass, StackedPseudoClass, NarrowPseudoClass)]
public partial class SmartFormFill : TemplatedControl
{
    internal const string CardPart = "PART_Card";
    internal const string TitlePart = "PART_Title";
    internal const string DescriptionPart = "PART_Description";
    internal const string BodyPart = "PART_Body";
    internal const string WellPart = "PART_Well";
    internal const string WellHostPart = "PART_WellHost";
    internal const string EditorPart = "PART_Editor";
    internal const string SourceScrollPart = "PART_SourceScroll";
    internal const string SourcePart = "PART_Source";
    internal const string ModelMarkPart = "PART_ModelMark";
    internal const string ModelNamePart = "PART_ModelName";
    internal const string SamplePart = "PART_Sample";
    internal const string FillPart = "PART_Fill";
    internal const string EditPart = "PART_Edit";
    internal const string FormPart = "PART_Form";
    internal const string FooterPart = "PART_Footer";
    internal const string StatusPart = "PART_Status";
    internal const string ActionsPart = "PART_Actions";
    internal const string UndoPart = "PART_Undo";
    internal const string SubmitPart = "PART_Submit";

    internal const string ReadingPseudoClass = ":reading";
    internal const string SourcePseudoClass = ":source";
    internal const string StackedPseudoClass = ":stacked";
    internal const string NarrowPseudoClass = ":narrow";

    /// <summary>From this content width the well sits left of the form; below it the well stacks on top.</summary>
    internal const double SideBySideWidth = 720;

    /// <summary>Under this content width every field takes the full row and the footer stacks its status over the actions.</summary>
    internal const double SingleColumnWidth = 440;

    /// <summary>The time between two fields filling, in form order.</summary>
    internal static readonly TimeSpan FillStagger = TimeSpan.FromMilliseconds(140);

    /// <summary>The default description under the title.</summary>
    internal const string DefaultDescription = "Paste a signature or notes. AI fills each field and shows where it found it.";

    /// <summary>The pasted text (two-way). Setting it while the paste area shows does not start a read; call <see cref="ReadAsync"/>.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<SmartFormFill, string?>(nameof(Text), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Starting values keyed by field id, applied when the card is built and whenever this changes.</summary>
    public static readonly StyledProperty<IReadOnlyDictionary<string, string>?> DefaultValuesProperty =
        AvaloniaProperty.Register<SmartFormFill, IReadOnlyDictionary<string, string>?>(nameof(DefaultValues));

    /// <summary>
    /// Reads the pasted text: receives it and a token that is cancelled by Stop, Escape, a new paste or the card leaving the
    /// tree, and returns a value per field id. Null uses the simulated reader (a sample's known result, a local parser for
    /// anything else) and plays <see cref="Simulation"/> when one is set.
    /// </summary>
    public static readonly StyledProperty<Func<string, CancellationToken, Task<SmartFormFillResult>>?> ExtractProperty =
        AvaloniaProperty.Register<SmartFormFill, Func<string, CancellationToken, Task<SmartFormFillResult>>?>(nameof(Extract));

    /// <summary>Fields whose confidence is below this ask for review and hold Save until checked or edited. Default 0.8.</summary>
    public static readonly StyledProperty<double> ReviewThresholdProperty =
        AvaloniaProperty.Register<SmartFormFill, double>(nameof(ReviewThreshold), 0.8);

    /// <summary>The texts behind Try sample, cycled in order. Null uses the <see cref="Simulation"/>'s while one plays; null or an empty list hides the button and the autoplay.</summary>
    public static readonly StyledProperty<IReadOnlyList<SmartFormFillSample>?> SamplesProperty =
        AvaloniaProperty.Register<SmartFormFill, IReadOnlyList<SmartFormFillSample>?>(nameof(Samples));

    /// <summary>
    /// Loops paste, fill, review and undo while the card is on screen, and stops for good on the first pointer or key press
    /// inside it. Null (default) plays while a <see cref="Simulation"/> plays. Never plays with reduced motion.
    /// </summary>
    public static readonly StyledProperty<bool?> AutoPlayProperty =
        AvaloniaProperty.Register<SmartFormFill, bool?>(nameof(AutoPlay));

    /// <summary>
    /// Texts to play while the card has no <see cref="Extract"/>: the samples behind Try sample with the results a read
    /// returns, and the model to name (nothing is sent). Your own <see cref="Extract"/> wins over it; null stops it and
    /// leaves the empty form.
    /// </summary>
    public static readonly StyledProperty<SmartFormFillSimulation?> SimulationProperty =
        AvaloniaProperty.Register<SmartFormFill, SmartFormFillSimulation?>(nameof(Simulation));

    /// <summary>Saves the form with its values. The button shows a loading state while the task runs; a fault shows an error status. Null simulates a save.</summary>
    public static readonly StyledProperty<Func<IReadOnlyDictionary<string, string>, Task>?> SubmitProperty =
        AvaloniaProperty.Register<SmartFormFill, Func<IReadOnlyDictionary<string, string>, Task>?>(nameof(Submit));

    /// <summary>The logo for the company field, from the company's name; null or a null result falls back to a monogram, and an empty company shows a building.</summary>
    public static readonly StyledProperty<Func<string, Control?>?> ResolveLogoProperty =
        AvaloniaProperty.Register<SmartFormFill, Func<string, Control?>?>(nameof(ResolveLogo));

    /// <summary>The model named beside the paste area; null names the <see cref="Simulation"/>'s model while one plays, or none.</summary>
    public static readonly StyledProperty<string?> ModelNameProperty =
        AvaloniaProperty.Register<SmartFormFill, string?>(nameof(ModelName));

    /// <summary>The model's mark beside its name (a 24 × 24 stroked glyph); the sparkle by default.</summary>
    public static readonly StyledProperty<Geometry?> ModelMarkProperty =
        AvaloniaProperty.Register<SmartFormFill, Geometry?>(nameof(ModelMark), ArcIcons.Sparkles);

    /// <summary>The card's title.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<SmartFormFill, string?>(nameof(Title), "New contact");

    /// <summary>One line under the title; an empty string hides it.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<SmartFormFill, string?>(nameof(Description), DefaultDescription);

    /// <summary>The primary action's label.</summary>
    public static readonly StyledProperty<string> SubmitLabelProperty =
        AvaloniaProperty.Register<SmartFormFill, string>(nameof(SubmitLabel), "Save contact");

    /// <summary>Every other word the card shows or says.</summary>
    public static readonly StyledProperty<SmartFormFillLabels> LabelsProperty =
        AvaloniaProperty.Register<SmartFormFill, SmartFormFillLabels>(nameof(Labels), SmartFormFillLabels.Default);

    /// <summary>The form's values by field id (a new snapshot on every change).</summary>
    public static readonly DirectProperty<SmartFormFill, IReadOnlyDictionary<string, string>> ValuesProperty =
        AvaloniaProperty.RegisterDirect<SmartFormFill, IReadOnlyDictionary<string, string>>(nameof(Values), o => o.Values);

    /// <summary>Where the card is: idle, reading, filling, filled, saving, saved or failed.</summary>
    public static readonly DirectProperty<SmartFormFill, SmartFormFillPhase> PhaseProperty =
        AvaloniaProperty.RegisterDirect<SmartFormFill, SmartFormFillPhase>(nameof(Phase), o => o.Phase);

    /// <summary>How many filled fields still ask for review.</summary>
    public static readonly DirectProperty<SmartFormFill, int> ReviewCountProperty =
        AvaloniaProperty.RegisterDirect<SmartFormFill, int>(nameof(ReviewCount), o => o.ReviewCount);

    /// <summary>Whether Undo can restore the values from before the last fill.</summary>
    public static readonly DirectProperty<SmartFormFill, bool> CanUndoProperty =
        AvaloniaProperty.RegisterDirect<SmartFormFill, bool>(nameof(CanUndo), o => o.CanUndo);

    // Paint of the source highlight, set by the theme from the ArcDesign tokens (decorations are not controls).
    internal static readonly StyledProperty<IBrush?> HighlightBrushProperty =
        AvaloniaProperty.Register<SmartFormFill, IBrush?>(nameof(HighlightBrush));

    internal static readonly StyledProperty<IBrush?> ReviewHighlightBrushProperty =
        AvaloniaProperty.Register<SmartFormFill, IBrush?>(nameof(ReviewHighlightBrush));

    internal static readonly StyledProperty<double> HighlightRestOpacityProperty =
        AvaloniaProperty.Register<SmartFormFill, double>(nameof(HighlightRestOpacity), 0.6);

    internal static readonly StyledProperty<CornerRadius> HighlightCornerRadiusProperty =
        AvaloniaProperty.Register<SmartFormFill, CornerRadius>(nameof(HighlightCornerRadius), new CornerRadius(4));

    internal static readonly StyledProperty<Thickness> HighlightOutsetProperty =
        AvaloniaProperty.Register<SmartFormFill, Thickness>(nameof(HighlightOutset), new Thickness(2, 1));

    internal static readonly StyledProperty<double> EditorHeightProperty =
        AvaloniaProperty.Register<SmartFormFill, double>(nameof(EditorHeight), 168);

    internal static readonly StyledProperty<double> EditorMinHeightProperty =
        AvaloniaProperty.Register<SmartFormFill, double>(nameof(EditorMinHeight), 200);

    internal static readonly StyledProperty<double> SectionGapProperty =
        AvaloniaProperty.Register<SmartFormFill, double>(nameof(SectionGap), 20);

    /// <summary>Raised on every change of the values, typed or filled. Bubbles.</summary>
    public static readonly RoutedEvent<SmartFormFillValuesEventArgs> ValuesChangedEvent =
        RoutedEvent.Register<SmartFormFill, SmartFormFillValuesEventArgs>(nameof(ValuesChanged), RoutingStrategies.Bubble);

    /// <summary>Raised once every field from a read has been filled. Bubbles.</summary>
    public static readonly RoutedEvent<SmartFormFillFilledEventArgs> FilledEvent =
        RoutedEvent.Register<SmartFormFill, SmartFormFillFilledEventArgs>(nameof(Filled), RoutingStrategies.Bubble);

    /// <summary>Raised after Undo restored the values from before the last fill; carries the restored values. Bubbles.</summary>
    public static readonly RoutedEvent<SmartFormFillValuesEventArgs> UndoneEvent =
        RoutedEvent.Register<SmartFormFill, SmartFormFillValuesEventArgs>(nameof(Undone), RoutingStrategies.Bubble);

    /// <summary>Raised after the form was saved; carries the saved values. Bubbles.</summary>
    public static readonly RoutedEvent<SmartFormFillValuesEventArgs> SubmittedEvent =
        RoutedEvent.Register<SmartFormFill, SmartFormFillValuesEventArgs>(nameof(Submitted), RoutingStrategies.Bubble);

    private readonly AvaloniaList<SmartFormFillField> _fields = [];

    /// <summary>Creates the card.</summary>
    public SmartFormFill()
    {
        _fields.CollectionChanged += (_, _) => RebuildFields();
        AddHandler(PointerPressedEvent, OnAnyPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(KeyDownEvent, OnAnyKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    /// <summary>The form's fields. Empty (default) shows the contact form: name, job title, company, email, phone and address.</summary>
    [Content]
    public AvaloniaList<SmartFormFillField> Fields => _fields;

    /// <inheritdoc cref="TextProperty"/>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc cref="DefaultValuesProperty"/>
    public IReadOnlyDictionary<string, string>? DefaultValues { get => GetValue(DefaultValuesProperty); set => SetValue(DefaultValuesProperty, value); }

    /// <inheritdoc cref="ExtractProperty"/>
    public Func<string, CancellationToken, Task<SmartFormFillResult>>? Extract { get => GetValue(ExtractProperty); set => SetValue(ExtractProperty, value); }

    /// <inheritdoc cref="ReviewThresholdProperty"/>
    public double ReviewThreshold { get => GetValue(ReviewThresholdProperty); set => SetValue(ReviewThresholdProperty, value); }

    /// <inheritdoc cref="SamplesProperty"/>
    public IReadOnlyList<SmartFormFillSample>? Samples { get => GetValue(SamplesProperty); set => SetValue(SamplesProperty, value); }

    /// <inheritdoc cref="AutoPlayProperty"/>
    public bool? AutoPlay { get => GetValue(AutoPlayProperty); set => SetValue(AutoPlayProperty, value); }

    /// <inheritdoc cref="SimulationProperty"/>
    public SmartFormFillSimulation? Simulation { get => GetValue(SimulationProperty); set => SetValue(SimulationProperty, value); }

    /// <inheritdoc cref="SubmitProperty"/>
    public Func<IReadOnlyDictionary<string, string>, Task>? Submit { get => GetValue(SubmitProperty); set => SetValue(SubmitProperty, value); }

    /// <inheritdoc cref="ResolveLogoProperty"/>
    public Func<string, Control?>? ResolveLogo { get => GetValue(ResolveLogoProperty); set => SetValue(ResolveLogoProperty, value); }

    /// <inheritdoc cref="ModelNameProperty"/>
    public string? ModelName { get => GetValue(ModelNameProperty); set => SetValue(ModelNameProperty, value); }

    /// <inheritdoc cref="ModelMarkProperty"/>
    public Geometry? ModelMark { get => GetValue(ModelMarkProperty); set => SetValue(ModelMarkProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="SubmitLabelProperty"/>
    public string SubmitLabel { get => GetValue(SubmitLabelProperty); set => SetValue(SubmitLabelProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public SmartFormFillLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="ValuesProperty"/>
    public IReadOnlyDictionary<string, string> Values
    {
        get => _valuesSnapshot;
        private set => SetAndRaise(ValuesProperty, ref _valuesSnapshot, value);
    }

    /// <inheritdoc cref="PhaseProperty"/>
    public SmartFormFillPhase Phase
    {
        get => _phase;
        private set => SetAndRaise(PhaseProperty, ref _phase, value);
    }

    /// <inheritdoc cref="ReviewCountProperty"/>
    public int ReviewCount
    {
        get => _reviewCount;
        private set => SetAndRaise(ReviewCountProperty, ref _reviewCount, value);
    }

    /// <inheritdoc cref="CanUndoProperty"/>
    public bool CanUndo
    {
        get => _canUndo;
        private set => SetAndRaise(CanUndoProperty, ref _canUndo, value);
    }

    internal IBrush? HighlightBrush { get => GetValue(HighlightBrushProperty); set => SetValue(HighlightBrushProperty, value); }

    internal IBrush? ReviewHighlightBrush { get => GetValue(ReviewHighlightBrushProperty); set => SetValue(ReviewHighlightBrushProperty, value); }

    internal double HighlightRestOpacity { get => GetValue(HighlightRestOpacityProperty); set => SetValue(HighlightRestOpacityProperty, value); }

    internal CornerRadius HighlightCornerRadius { get => GetValue(HighlightCornerRadiusProperty); set => SetValue(HighlightCornerRadiusProperty, value); }

    internal Thickness HighlightOutset { get => GetValue(HighlightOutsetProperty); set => SetValue(HighlightOutsetProperty, value); }

    internal double EditorHeight { get => GetValue(EditorHeightProperty); set => SetValue(EditorHeightProperty, value); }

    internal double EditorMinHeight { get => GetValue(EditorMinHeightProperty); set => SetValue(EditorMinHeightProperty, value); }

    internal double SectionGap { get => GetValue(SectionGapProperty); set => SetValue(SectionGapProperty, value); }

    /// <inheritdoc cref="ValuesChangedEvent"/>
    public event EventHandler<SmartFormFillValuesEventArgs>? ValuesChanged
    {
        add => AddHandler(ValuesChangedEvent, value);
        remove => RemoveHandler(ValuesChangedEvent, value);
    }

    /// <inheritdoc cref="FilledEvent"/>
    public event EventHandler<SmartFormFillFilledEventArgs>? Filled
    {
        add => AddHandler(FilledEvent, value);
        remove => RemoveHandler(FilledEvent, value);
    }

    /// <inheritdoc cref="UndoneEvent"/>
    public event EventHandler<SmartFormFillValuesEventArgs>? Undone
    {
        add => AddHandler(UndoneEvent, value);
        remove => RemoveHandler(UndoneEvent, value);
    }

    /// <inheritdoc cref="SubmittedEvent"/>
    public event EventHandler<SmartFormFillValuesEventArgs>? Submitted
    {
        add => AddHandler(SubmittedEvent, value);
        remove => RemoveHandler(SubmittedEvent, value);
    }
}
