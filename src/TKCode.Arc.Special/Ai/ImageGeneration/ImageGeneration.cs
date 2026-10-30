using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// An image generation card: a prompt, model and aspect chips with Generate, a 2 × 2 grid where each image resolves as a
/// pixel mosaic (coarse colour blocks to full detail under a soft light band) with its own progress ring, and a footer with
/// the status and Upscale, Vary and Download. Pick an image to continue with it: Upscale grows it to fill the grid and
/// sweeps a band of crisp pixels across it, Vary makes four new images from it and Download hands it to the app.
/// <para>
/// Give it a <see cref="Generator"/> that makes one image (it is called once per tile, in parallel, for Generate, Vary and
/// Retry, and reports progress through its <see cref="ImageGenerationContext"/>) and optionally an <see cref="Upscaler"/>.
/// With no generator it plays its <see cref="Simulation"/>, pictures painted by your <see cref="ImageGenerationSimulation.Painter"/>,
/// nothing downloaded, and generates once when it first scrolls into view; with neither, Generate stays unavailable.
/// </para>
/// </summary>
[TemplatePart(PromptPart, typeof(Textarea))]
[TemplatePart(ModelsPart, typeof(StackPanel))]
[TemplatePart(AspectsPart, typeof(StackPanel))]
[TemplatePart(GeneratePart, typeof(AvaloniaButton))]
[TemplatePart(GridPart, typeof(ImageGenerationGrid))]
[TemplatePart(StatusPart, typeof(ArcContentSwap))]
[TemplatePart(UpscalePart, typeof(AvaloniaButton))]
[TemplatePart(VaryPart, typeof(AvaloniaButton))]
[TemplatePart(DownloadPart, typeof(AvaloniaButton))]
[PseudoClasses(GeneratingPseudoClass, ExpandedPseudoClass, NarrowPseudoClass, StackedPseudoClass)]
public partial class ImageGeneration : TemplatedControl
{
    internal const string PromptPart = "PART_Prompt";
    internal const string ModelsPart = "PART_Models";
    internal const string AspectsPart = "PART_Aspects";
    internal const string GeneratePart = "PART_Generate";
    internal const string GridPart = "PART_Grid";
    internal const string StatusPart = "PART_Status";
    internal const string UpscalePart = "PART_Upscale";
    internal const string VaryPart = "PART_Vary";
    internal const string DownloadPart = "PART_Download";
    internal const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set while a batch runs.</summary>
    public const string GeneratingPseudoClass = ":generating";

    /// <summary>Set while an image is expanded over the grid.</summary>
    public const string ExpandedPseudoClass = ":expanded";

    /// <summary>Set below <see cref="NarrowWidth"/>: the aspect chips move to a second row.</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Set below <see cref="StackWidth"/>: the footer stacks, status on top and three equal buttons below.</summary>
    public const string StackedPseudoClass = ":stacked";

    /// <summary>The card width below which the chips and Generate wrap into two rows.</summary>
    public const double NarrowWidth = 420;

    /// <summary>The card width below which the footer stacks.</summary>
    public const double StackWidth = 520;

    /// <summary>How many images a batch makes.</summary>
    public const int BatchSize = 4;

    /// <summary>How long Download reads "Saved".</summary>
    public static readonly TimeSpan SavedDuration = TimeSpan.FromMilliseconds(1600);

    /// <summary>The prompt (two-way); the field and the suggestions write it. Null starts with the simulation's prompt, if any.</summary>
    public static readonly StyledProperty<string?> PromptProperty =
        AvaloniaProperty.Register<ImageGeneration, string?>(nameof(Prompt), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// The generator the card plays while <see cref="Generator"/> is null: its prompt, models, suggestions and painter,
    /// run exactly like your own generator (four tiles resolving from coarse blocks, Upscale, Vary). Null leaves Generate
    /// unavailable instead.
    /// </summary>
    public static readonly StyledProperty<ImageGenerationSimulation?> SimulationProperty =
        AvaloniaProperty.Register<ImageGeneration, ImageGenerationSimulation?>(nameof(Simulation));

    /// <summary>The prompt field's placeholder; null uses <see cref="ImageGenerationLabels.Placeholder"/>.</summary>
    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<ImageGeneration, string?>(nameof(Placeholder));

    /// <summary>The models offered as chips. Null or empty offers the simulation's models, or one unnamed model.</summary>
    public static readonly StyledProperty<IReadOnlyList<ImageGenerationModel>?> ModelsProperty =
        AvaloniaProperty.Register<ImageGeneration, IReadOnlyList<ImageGenerationModel>?>(nameof(Models));

    /// <summary>The chosen model's id (two-way); null or unknown chooses the first model.</summary>
    public static readonly StyledProperty<string?> SelectedModelIdProperty =
        AvaloniaProperty.Register<ImageGeneration, string?>(nameof(SelectedModelId), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The shapes offered as chips. Null or empty offers 1:1, 4:5 and 16:9.</summary>
    public static readonly StyledProperty<IReadOnlyList<ImageGenerationAspect>?> AspectRatiosProperty =
        AvaloniaProperty.Register<ImageGeneration, IReadOnlyList<ImageGenerationAspect>?>(nameof(AspectRatios));

    /// <summary>
    /// The chosen shape's id (two-way); null or unknown chooses the first. It applies to the next generation; the grid keeps
    /// the shape of its batch.
    /// </summary>
    public static readonly StyledProperty<string?> SelectedAspectIdProperty =
        AvaloniaProperty.Register<ImageGeneration, string?>(nameof(SelectedAspectId), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Makes one image. Called once per tile, in parallel, for Generate, Vary (the request's VariationOf is set) and Retry.
    /// Report progress from 0 to 1 through the context, optionally with a preview the mosaic resolves from; throw to mark the
    /// tile failed. The context's token is cancelled on Stop. Null uses the <see cref="Simulation"/>'s, if any.
    /// </summary>
    public static readonly StyledProperty<Func<ImageGenerationRequest, ImageGenerationContext, Task<ImageGenerationImage>>?> GeneratorProperty =
        AvaloniaProperty.Register<ImageGeneration, Func<ImageGenerationRequest, ImageGenerationContext, Task<ImageGenerationImage>>?>(nameof(Generator));

    /// <summary>
    /// Upscales the picked image while it is expanded; return the new image with its width and height. The token is
    /// cancelled when the expanded image closes. Null uses the <see cref="Simulation"/>'s, or keeps the image as it is.
    /// </summary>
    public static readonly StyledProperty<Func<ImageGenerationImage, ImageGenerationContext, Task<ImageGenerationImage>>?> UpscalerProperty =
        AvaloniaProperty.Register<ImageGeneration, Func<ImageGenerationImage, ImageGenerationContext, Task<ImageGenerationImage>>?>(nameof(Upscaler));

    /// <summary>Prompts offered in the empty grid before the first generation; picking one fills the prompt and generates. Null offers the simulation's, or none.</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> SuggestionsProperty =
        AvaloniaProperty.Register<ImageGeneration, IReadOnlyList<string>?>(nameof(Suggestions));

    /// <summary>
    /// Generates once when the card first scrolls into view, then picks image 1 if nobody has touched the card. Null (the
    /// default) does so with the simulation only, not with your <see cref="Generator"/>.
    /// </summary>
    public static readonly StyledProperty<bool?> AutoGenerateProperty =
        AvaloniaProperty.Register<ImageGeneration, bool?>(nameof(AutoGenerate));

    /// <summary>Every word the card shows or announces.</summary>
    public static readonly StyledProperty<ImageGenerationLabels> LabelsProperty =
        AvaloniaProperty.Register<ImageGeneration, ImageGenerationLabels>(nameof(Labels), ImageGenerationLabels.Default);

    /// <summary>The picked image (the upscaled one after an upscale), or null.</summary>
    public static readonly DirectProperty<ImageGeneration, ImageGenerationImage?> SelectedImageProperty =
        AvaloniaProperty.RegisterDirect<ImageGeneration, ImageGenerationImage?>(nameof(SelectedImage), o => o.SelectedImage);

    /// <summary>True while a batch runs (Generate reads Stop).</summary>
    public static readonly DirectProperty<ImageGeneration, bool> IsGeneratingProperty =
        AvaloniaProperty.RegisterDirect<ImageGeneration, bool>(nameof(IsGenerating), o => o.IsGenerating);

    /// <summary>True while the picked image is expanded over the grid.</summary>
    public static readonly DirectProperty<ImageGeneration, bool> IsExpandedProperty =
        AvaloniaProperty.RegisterDirect<ImageGeneration, bool>(nameof(IsExpanded), o => o.IsExpanded);

    /// <summary>Raised when the person picks or clears an image, and with the upscaled image once an upscale finishes. Bubbles.</summary>
    public static readonly RoutedEvent<ImageGenerationSelectionChangedEventArgs> SelectionChangedEvent =
        RoutedEvent.Register<ImageGeneration, ImageGenerationSelectionChangedEventArgs>(nameof(SelectionChanged), RoutingStrategies.Bubble);

    /// <summary>
    /// Raised when the person presses Download, with the image and a file name built from the prompt, its number and its
    /// size. Save it from here (for example through the window's storage provider). Bubbles.
    /// </summary>
    public static readonly RoutedEvent<ImageGenerationDownloadEventArgs> DownloadRequestedEvent =
        RoutedEvent.Register<ImageGeneration, ImageGenerationDownloadEventArgs>(nameof(DownloadRequested), RoutingStrategies.Bubble);

    private ImageGenerationImage? _selectedImage;
    private bool _isGenerating;
    private bool _isExpanded;

    /// <inheritdoc cref="PromptProperty"/>
    public string? Prompt { get => GetValue(PromptProperty); set => SetValue(PromptProperty, value); }

    /// <inheritdoc cref="SimulationProperty"/>
    public ImageGenerationSimulation? Simulation { get => GetValue(SimulationProperty); set => SetValue(SimulationProperty, value); }

    /// <inheritdoc cref="PlaceholderProperty"/>
    public string? Placeholder { get => GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }

    /// <inheritdoc cref="ModelsProperty"/>
    public IReadOnlyList<ImageGenerationModel>? Models { get => GetValue(ModelsProperty); set => SetValue(ModelsProperty, value); }

    /// <inheritdoc cref="SelectedModelIdProperty"/>
    public string? SelectedModelId { get => GetValue(SelectedModelIdProperty); set => SetValue(SelectedModelIdProperty, value); }

    /// <inheritdoc cref="AspectRatiosProperty"/>
    public IReadOnlyList<ImageGenerationAspect>? AspectRatios { get => GetValue(AspectRatiosProperty); set => SetValue(AspectRatiosProperty, value); }

    /// <inheritdoc cref="SelectedAspectIdProperty"/>
    public string? SelectedAspectId { get => GetValue(SelectedAspectIdProperty); set => SetValue(SelectedAspectIdProperty, value); }

    /// <inheritdoc cref="GeneratorProperty"/>
    public Func<ImageGenerationRequest, ImageGenerationContext, Task<ImageGenerationImage>>? Generator
    {
        get => GetValue(GeneratorProperty);
        set => SetValue(GeneratorProperty, value);
    }

    /// <inheritdoc cref="UpscalerProperty"/>
    public Func<ImageGenerationImage, ImageGenerationContext, Task<ImageGenerationImage>>? Upscaler
    {
        get => GetValue(UpscalerProperty);
        set => SetValue(UpscalerProperty, value);
    }

    /// <inheritdoc cref="SuggestionsProperty"/>
    public IReadOnlyList<string>? Suggestions { get => GetValue(SuggestionsProperty); set => SetValue(SuggestionsProperty, value); }

    /// <inheritdoc cref="AutoGenerateProperty"/>
    public bool? AutoGenerate { get => GetValue(AutoGenerateProperty); set => SetValue(AutoGenerateProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public ImageGenerationLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="SelectedImageProperty"/>
    public ImageGenerationImage? SelectedImage { get => _selectedImage; private set => SetAndRaise(SelectedImageProperty, ref _selectedImage, value); }

    /// <inheritdoc cref="IsGeneratingProperty"/>
    public bool IsGenerating { get => _isGenerating; private set => SetAndRaise(IsGeneratingProperty, ref _isGenerating, value); }

    /// <inheritdoc cref="IsExpandedProperty"/>
    public bool IsExpanded { get => _isExpanded; private set => SetAndRaise(IsExpandedProperty, ref _isExpanded, value); }

    /// <inheritdoc cref="SelectionChangedEvent"/>
    public event EventHandler<ImageGenerationSelectionChangedEventArgs>? SelectionChanged
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    /// <inheritdoc cref="DownloadRequestedEvent"/>
    public event EventHandler<ImageGenerationDownloadEventArgs>? DownloadRequested
    {
        add => AddHandler(DownloadRequestedEvent, value);
        remove => RemoveHandler(DownloadRequestedEvent, value);
    }
}
