using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A screenshot-to-code card (Arc Design to code, design reference). The person drops a UI screenshot or imports a frame
/// from a design file; a scan line crosses it and the layout regions it finds appear as boxes with tags; then code streams
/// in beside it, linked region by region (pointing at a box or a tag highlights its lines and the other way round), with
/// a working live preview one tab away.
/// <para>
/// Generation comes from <see cref="Generator"/>, an async delegate that adds regions and writes code chunks through a
/// <see cref="DesignToCodeContext"/>. Without one the card plays a built-in simulation on a sample pricing card and, while
/// it is on screen, loops it (generate, hold on the code, show the preview, start again) until the first pointer or key
/// press inside the card. Dropping a file uses Avalonia drag and drop; Choose file raises
/// <see cref="ChooseFileRequested"/> for your picker (hand the file to <see cref="LoadFileAsync"/>).
/// </para>
/// </summary>
[TemplatePart(SplitPart, typeof(DesignToCodeSplit))]
[TemplatePart(DesignPanePart, typeof(Border))]
[TemplatePart(DesignLabelPart, typeof(TextBlock))]
[TemplatePart(SourceNamePart, typeof(TextBlock))]
[TemplatePart(SourceKindPart, typeof(TextBlock))]
[TemplatePart(RemovePart, typeof(AvaloniaButton))]
[TemplatePart(CanvasPart, typeof(DesignToCodeCanvas))]
[TemplatePart(DropZonePart, typeof(Control))]
[TemplatePart(DropTitlePart, typeof(TextBlock))]
[TemplatePart(DropHintPart, typeof(TextBlock))]
[TemplatePart(DropErrorPart, typeof(TextBlock))]
[TemplatePart(ChooseFilePart, typeof(AvaloniaButton))]
[TemplatePart(UseSamplePart, typeof(AvaloniaButton))]
[TemplatePart(DragCoverPart, typeof(Control))]
[TemplatePart(DragCoverTextPart, typeof(TextBlock))]
[TemplatePart(OutputPanePart, typeof(Border))]
[TemplatePart(FileNamePart, typeof(TextBlock))]
[TemplatePart(ModelMarkPart, typeof(ArcGlyph))]
[TemplatePart(ModelNamePart, typeof(TextBlock))]
[TemplatePart(ViewTogglePart, typeof(SegmentedControl))]
[TemplatePart(CopyPart, typeof(CopyButton))]
[TemplatePart(LayersPart, typeof(DesignToCodeLayers))]
[TemplatePart(CodeScrollPart, typeof(ScrollViewer))]
[TemplatePart(CodePart, typeof(DesignToCodeCodeView))]
[TemplatePart(CodePlaceholderPart, typeof(ArcContentSwap))]
[TemplatePart(PreviewHostPart, typeof(Decorator))]
[TemplatePart(StatusPart, typeof(ArcContentSwap))]
[TemplatePart(ImportPart, typeof(AvaloniaButton))]
[TemplatePart(MainPart, typeof(AvaloniaButton))]
[TemplatePart(AnnouncerPart, typeof(ExplainerAnnouncer))]
[PseudoClasses(StackedPseudoClass, CompactPseudoClass, NarrowPseudoClass, EmptyPseudoClass, RunningPseudoClass, DraggingPseudoClass,
    HasCodePseudoClass, ErrorPseudoClass)]
public partial class DesignToCode : TemplatedControl
{
    internal const string SplitPart = "PART_Split";
    internal const string DesignPanePart = "PART_DesignPane";
    internal const string DesignLabelPart = "PART_DesignLabel";
    internal const string SourceNamePart = "PART_SourceName";
    internal const string SourceKindPart = "PART_SourceKind";
    internal const string RemovePart = "PART_Remove";
    internal const string CanvasPart = "PART_Canvas";
    internal const string DropZonePart = "PART_DropZone";
    internal const string DropTitlePart = "PART_DropTitle";
    internal const string DropHintPart = "PART_DropHint";
    internal const string DropErrorPart = "PART_DropError";
    internal const string ChooseFilePart = "PART_ChooseFile";
    internal const string UseSamplePart = "PART_UseSample";
    internal const string DragCoverPart = "PART_DragCover";
    internal const string DragCoverTextPart = "PART_DragCoverText";
    internal const string OutputPanePart = "PART_OutputPane";
    internal const string FileNamePart = "PART_FileName";
    internal const string ModelMarkPart = "PART_ModelMark";
    internal const string ModelNamePart = "PART_ModelName";
    internal const string ViewTogglePart = "PART_ViewToggle";
    internal const string CopyPart = "PART_Copy";
    internal const string LayersPart = "PART_Layers";
    internal const string CodeScrollPart = "PART_CodeScroll";
    internal const string CodePart = "PART_Code";
    internal const string CodePlaceholderPart = "PART_CodePlaceholder";
    internal const string PreviewHostPart = "PART_PreviewHost";
    internal const string StatusPart = "PART_Status";
    internal const string ImportPart = "PART_Import";
    internal const string MainPart = "PART_Main";
    internal const string AnnouncerPart = "PART_Announcer";

    internal const string StackedPseudoClass = ":stacked";
    internal const string CompactPseudoClass = ":compact";
    internal const string NarrowPseudoClass = ":narrow";
    internal const string EmptyPseudoClass = ":empty";
    internal const string RunningPseudoClass = ":running";
    internal const string DraggingPseudoClass = ":dragging";
    internal const string HasCodePseudoClass = ":has-code";
    internal const string ErrorPseudoClass = ":error";

    /// <summary>
    /// The design shown (two-way). Leave it unset to show the <see cref="Simulation"/>'s design (or the drop zone without
    /// one, or when <see cref="Generator"/> is set); set it to null to open on the drop zone. A source without an image
    /// draws its <see cref="DesignToCodeSource.Sketch"/>.
    /// </summary>
    public static readonly StyledProperty<DesignToCodeSource?> SourceProperty =
        AvaloniaProperty.Register<DesignToCode, DesignToCodeSource?>(nameof(Source), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The layer open on the output side (two-way). Each new run returns to Code so the stream is visible.</summary>
    public static readonly StyledProperty<DesignToCodeView> ViewProperty =
        AvaloniaProperty.Register<DesignToCode, DesignToCodeView>(nameof(View), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// The file name shown in the code header, in the status while writing and in the copy button's name. Empty uses the
    /// simulation's while it plays.
    /// </summary>
    public static readonly StyledProperty<string> FileNameProperty =
        AvaloniaProperty.Register<DesignToCode, string>(nameof(FileName), string.Empty);

    /// <summary>The language the code is highlighted as (a TextMate grammar name or alias such as tsx, jsx, html or vue).</summary>
    public static readonly StyledProperty<string> LanguageProperty =
        AvaloniaProperty.Register<DesignToCode, string>(nameof(Language), "tsx");

    /// <summary>The model credited in the code header: its mark and name. Null credits the simulation's model while it plays, and none otherwise.</summary>
    public static readonly StyledProperty<AgentModel?> ModelProperty =
        AvaloniaProperty.Register<DesignToCode, AgentModel?>(nameof(Model));

    /// <summary>
    /// Plays the <see cref="Simulation"/> on a loop while the card is on screen (generate, hold on the code, show the
    /// preview, start again), and stops at the first pointer or key press inside the card. Null plays it when there is no
    /// <see cref="Generator"/>.
    /// Under reduced motion it plays once.
    /// </summary>
    public static readonly StyledProperty<bool?> AutoPlayProperty =
        AvaloniaProperty.Register<DesignToCode, bool?>(nameof(AutoPlay));

    /// <summary>The file types accepted, as in an input's accept attribute (MIME types, <c>image/*</c> or extensions). Other files show an error.</summary>
    public static readonly StyledProperty<string> AcceptedFileTypesProperty =
        AvaloniaProperty.Register<DesignToCode, string>(nameof(AcceptedFileTypes), "image/png,image/jpeg,image/webp");

    /// <summary>The largest file accepted, in bytes (10 MB by default).</summary>
    public static readonly StyledProperty<long> MaxFileSizeProperty =
        AvaloniaProperty.Register<DesignToCode, long>(nameof(MaxFileSize), 10 * 1024 * 1024);

    /// <summary>
    /// Turns a source into code: call <see cref="DesignToCodeContext.AddRegion"/> as parts are detected, then
    /// <see cref="DesignToCodeContext.Write"/> as code streams, with a region id to link a chunk to its box. Complete the task
    /// when done, throw to show the error, and stop when the context's token is cancelled. Null plays the
    /// <see cref="Simulation"/>, if any.
    /// </summary>
    public static readonly StyledProperty<Func<DesignToCodeSource, DesignToCodeContext, Task>?> GeneratorProperty =
        AvaloniaProperty.Register<DesignToCode, Func<DesignToCodeSource, DesignToCodeContext, Task>?>(nameof(Generator));

    /// <summary>
    /// Runs your design file picker or link flow for Import design file: return the frame (with its rendered image) or null
    /// when cancelled. Null simulates an import of the simulation's design, if any.
    /// </summary>
    public static readonly StyledProperty<Func<CancellationToken, Task<DesignToCodeSource?>>?> DesignFileImporterProperty =
        AvaloniaProperty.Register<DesignToCode, Func<CancellationToken, Task<DesignToCodeSource?>>?>(nameof(DesignFileImporter));

    /// <summary>
    /// Renders the finished code in the Preview tab (for example in a sandboxed web view; never run generated code in your
    /// app). Return null for no preview. Null uses the simulation's preview after a simulated run, and a short note otherwise.
    /// </summary>
    public static readonly StyledProperty<Func<string, DesignToCodeSource, Control?>?> PreviewFactoryProperty =
        AvaloniaProperty.Register<DesignToCode, Func<string, DesignToCodeSource, Control?>?>(nameof(PreviewFactory));

    /// <summary>
    /// The design and code the card plays while there is no <see cref="Generator"/>: its design shows while
    /// <see cref="Source"/> is unset, Use sample brings it back, and Generate (or the autoplay loop) scans it and streams its
    /// code exactly like a generator would. Null leaves the card on its drop zone.
    /// </summary>
    public static readonly StyledProperty<DesignToCodeSimulation?> SimulationProperty =
        AvaloniaProperty.Register<DesignToCode, DesignToCodeSimulation?>(nameof(Simulation));

    /// <summary>Every word the card shows or announces.</summary>
    public static readonly StyledProperty<DesignToCodeLabels> LabelsProperty =
        AvaloniaProperty.Register<DesignToCode, DesignToCodeLabels>(nameof(Labels), DesignToCodeLabels.Default);

    /// <summary>Where the card is in its run.</summary>
    public static readonly DirectProperty<DesignToCode, DesignToCodePhase> PhaseProperty =
        AvaloniaProperty.RegisterDirect<DesignToCode, DesignToCodePhase>(nameof(Phase), o => o.Phase);

    /// <summary>The code written so far (the finished code once <see cref="Phase"/> is Done).</summary>
    public static readonly DirectProperty<DesignToCode, string> CodeProperty =
        AvaloniaProperty.RegisterDirect<DesignToCode, string>(nameof(Code), o => o.Code);

    /// <summary>The problem the status names (a rejected file, a failed import or run), or null.</summary>
    public static readonly DirectProperty<DesignToCode, string?> ErrorMessageProperty =
        AvaloniaProperty.RegisterDirect<DesignToCode, string?>(nameof(ErrorMessage), o => o.ErrorMessage);

    /// <summary>Raised with the finished code when a run completes. Bubbles.</summary>
    public static readonly RoutedEvent<DesignToCodeCompletedEventArgs> CompletedEvent =
        RoutedEvent.Register<DesignToCode, DesignToCodeCompletedEventArgs>(nameof(Completed), RoutingStrategies.Bubble);

    /// <summary>
    /// Raised when the person presses Choose file. Bubbles. Open your file picker, pass the file to
    /// <see cref="LoadFileAsync"/> and set Handled; when nobody handles it the card uses the sample and says nothing was opened.
    /// </summary>
    public static readonly RoutedEvent<RoutedEventArgs> ChooseFileRequestedEvent =
        RoutedEvent.Register<DesignToCode, RoutedEventArgs>(nameof(ChooseFileRequested), RoutingStrategies.Bubble);

    /// <summary>The card width from which the panes sit side by side (the theme sets it from the token).</summary>
    internal static readonly StyledProperty<double> SplitWidthProperty =
        AvaloniaProperty.Register<DesignToCode, double>(nameof(SplitWidth), 640);

    /// <summary>The card width under which the footer is compact.</summary>
    internal static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<DesignToCode, double>(nameof(CompactWidth), 520);

    /// <summary>The card width under which meta text and the model name hide.</summary>
    internal static readonly StyledProperty<double> NarrowWidthProperty =
        AvaloniaProperty.Register<DesignToCode, double>(nameof(NarrowWidth), 440);

    private DesignToCodePhase _phase = DesignToCodePhase.Empty;
    private string _code = string.Empty;
    private string? _errorMessage;
    private bool _sourceCleared;
    private bool _stacked;
    private bool _compact;
    private bool _narrow;
    private bool _dragging;
    private bool _toggleReady;
    private string? _statusKey;
    private string? _placeholderKey;
    private object? _previewKey;

    private DesignToCodeSplit? _split;
    private Border? _designPane;
    private TextBlock? _designLabel;
    private TextBlock? _sourceName;
    private TextBlock? _sourceKind;
    private AvaloniaButton? _remove;
    private DesignToCodeCanvas? _canvas;
    private Control? _dropZone;
    private TextBlock? _dropTitle;
    private TextBlock? _dropHint;
    private TextBlock? _dropError;
    private AvaloniaButton? _chooseFile;
    private AvaloniaButton? _useSample;
    private Control? _dragCover;
    private TextBlock? _dragCoverText;
    private Border? _outputPane;
    private TextBlock? _fileName;
    private ArcGlyph? _modelMark;
    private TextBlock? _modelName;
    private SegmentedControl? _viewToggle;
    private CopyButton? _copy;
    private DesignToCodeLayers? _layers;
    private ScrollViewer? _codeScroll;
    private DesignToCodeCodeView? _codeView;
    private ArcContentSwap? _codePlaceholder;
    private Decorator? _previewHost;
    private ArcContentSwap? _status;
    private AvaloniaButton? _import;
    private AvaloniaButton? _main;
    private ExplainerAnnouncer? _announcer;

    static DesignToCode()
    {
        SpecialFocusModality.Watch();
        FocusableProperty.OverrideDefaultValue<DesignToCode>(false);
    }

    /// <summary>Creates the card.</summary>
    public DesignToCode()
    {
        _loop = new LiveLoop(this, OnFrame) { MinimumLevel = ArcMotionLevel.Off };
        AddHandler(PointerPressedEvent, OnAnyPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(KeyDownEvent, OnAnyKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        InitializeDrop();
    }

    /// <inheritdoc cref="SourceProperty"/>
    public DesignToCodeSource? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

    /// <inheritdoc cref="ViewProperty"/>
    public DesignToCodeView View { get => GetValue(ViewProperty); set => SetValue(ViewProperty, value); }

    /// <inheritdoc cref="FileNameProperty"/>
    public string FileName { get => GetValue(FileNameProperty); set => SetValue(FileNameProperty, value); }

    /// <inheritdoc cref="LanguageProperty"/>
    public string Language { get => GetValue(LanguageProperty); set => SetValue(LanguageProperty, value); }

    /// <inheritdoc cref="ModelProperty"/>
    public AgentModel? Model { get => GetValue(ModelProperty); set => SetValue(ModelProperty, value); }

    /// <inheritdoc cref="AutoPlayProperty"/>
    public bool? AutoPlay { get => GetValue(AutoPlayProperty); set => SetValue(AutoPlayProperty, value); }

    /// <inheritdoc cref="AcceptedFileTypesProperty"/>
    public string AcceptedFileTypes { get => GetValue(AcceptedFileTypesProperty); set => SetValue(AcceptedFileTypesProperty, value); }

    /// <inheritdoc cref="MaxFileSizeProperty"/>
    public long MaxFileSize { get => GetValue(MaxFileSizeProperty); set => SetValue(MaxFileSizeProperty, value); }

    /// <inheritdoc cref="GeneratorProperty"/>
    public Func<DesignToCodeSource, DesignToCodeContext, Task>? Generator { get => GetValue(GeneratorProperty); set => SetValue(GeneratorProperty, value); }

    /// <inheritdoc cref="DesignFileImporterProperty"/>
    public Func<CancellationToken, Task<DesignToCodeSource?>>? DesignFileImporter
    {
        get => GetValue(DesignFileImporterProperty);
        set => SetValue(DesignFileImporterProperty, value);
    }

    /// <inheritdoc cref="PreviewFactoryProperty"/>
    public Func<string, DesignToCodeSource, Control?>? PreviewFactory { get => GetValue(PreviewFactoryProperty); set => SetValue(PreviewFactoryProperty, value); }

    /// <inheritdoc cref="SimulationProperty"/>
    public DesignToCodeSimulation? Simulation { get => GetValue(SimulationProperty); set => SetValue(SimulationProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public DesignToCodeLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    internal double SplitWidth { get => GetValue(SplitWidthProperty); set => SetValue(SplitWidthProperty, value); }

    internal double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }

    internal double NarrowWidth { get => GetValue(NarrowWidthProperty); set => SetValue(NarrowWidthProperty, value); }

    /// <inheritdoc cref="PhaseProperty"/>
    public DesignToCodePhase Phase { get => _phase; private set => SetAndRaise(PhaseProperty, ref _phase, value); }

    /// <inheritdoc cref="CodeProperty"/>
    public string Code { get => _code; private set => SetAndRaise(CodeProperty, ref _code, value); }

    /// <inheritdoc cref="ErrorMessageProperty"/>
    public string? ErrorMessage { get => _errorMessage; private set => SetAndRaise(ErrorMessageProperty, ref _errorMessage, value); }

    /// <summary>The regions found in the current run, in the order they were found.</summary>
    public IReadOnlyList<DesignToCodeRegion> Regions => _regions;

    /// <inheritdoc cref="CompletedEvent"/>
    public event EventHandler<DesignToCodeCompletedEventArgs>? Completed
    {
        add => AddHandler(CompletedEvent, value);
        remove => RemoveHandler(CompletedEvent, value);
    }

    /// <inheritdoc cref="ChooseFileRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? ChooseFileRequested
    {
        add => AddHandler(ChooseFileRequestedEvent, value);
        remove => RemoveHandler(ChooseFileRequestedEvent, value);
    }

    /// <summary>The design on screen: <see cref="Source"/>, or the simulation's while it is left unset and there is no generator.</summary>
    internal DesignToCodeSource? EffectiveSource =>
        Source ?? (!_sourceCleared && !IsSet(SourceProperty) && Generator is null ? Simulation?.Source : null);

    /// <summary>The file name: <see cref="FileName"/>, else the simulation's while it can play.</summary>
    internal string EffectiveFileName =>
        FileName is { Length: > 0 } name ? name : CanSimulate ? Simulation!.FileName ?? string.Empty : string.Empty;

    /// <summary>The preview renderer: <see cref="PreviewFactory"/>, else the simulation's after a simulated run.</summary>
    private Func<string, DesignToCodeSource, Control?>? EffectivePreviewFactory =>
        PreviewFactory ?? (_simulated ? _runSimulation?.PreviewFactory : null);

    /// <summary>Whether a run would be simulated: no generator, and a simulation to play.</summary>
    internal bool CanSimulate => Generator is null && Simulation is not null;

    /// <summary>Whether the sample loop plays.</summary>
    internal bool EffectiveAutoPlay => AutoPlay ?? Generator is null;

    internal DesignToCodeCanvas? Canvas => _canvas;

    internal DesignToCodeCodeView? CodeView => _codeView;

    internal DesignToCodeLayers? Layers => _layers;

    internal ScrollViewer? CodeScroll => _codeScroll;

    internal AvaloniaButton? MainButton => _main;

    internal AvaloniaButton? ImportButton => _import;

    internal AvaloniaButton? RemoveButton => _remove;

    internal AvaloniaButton? ChooseFileButton => _chooseFile;

    internal AvaloniaButton? UseSampleButton => _useSample;

    internal SegmentedControl? ViewToggle => _viewToggle;

    internal CopyButton? CopyButton => _copy;

    internal Control? DropZone => _dropZone;

    internal TextBlock? DropError => _dropError;

    internal ExplainerAnnouncer? Announcer => _announcer;

    internal TextBlock? ModelNameText => _modelName;

    internal Control? PreviewContent => _previewHost?.Child;


    /// <summary>The status line's parts (icon, label, file, meta) as last shown; tests.</summary>
    internal (Geometry Icon, string Label, string? File, string? Meta) StatusParts { get; private set; }

    internal bool IsStackedLayout => _stacked;

    internal bool IsCompactLayout => _compact;

    internal bool IsNarrowLayout => _narrow;

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unhook();
        var scope = e.NameScope;
        _split = scope.Find<DesignToCodeSplit>(SplitPart);
        _designPane = scope.Find<Border>(DesignPanePart);
        _designLabel = scope.Find<TextBlock>(DesignLabelPart);
        _sourceName = scope.Find<TextBlock>(SourceNamePart);
        _sourceKind = scope.Find<TextBlock>(SourceKindPart);
        _remove = scope.Find<AvaloniaButton>(RemovePart);
        _canvas = scope.Find<DesignToCodeCanvas>(CanvasPart);
        _dropZone = scope.Find<Control>(DropZonePart);
        _dropTitle = scope.Find<TextBlock>(DropTitlePart);
        _dropHint = scope.Find<TextBlock>(DropHintPart);
        _dropError = scope.Find<TextBlock>(DropErrorPart);
        _chooseFile = scope.Find<AvaloniaButton>(ChooseFilePart);
        _useSample = scope.Find<AvaloniaButton>(UseSamplePart);
        _dragCover = scope.Find<Control>(DragCoverPart);
        _dragCoverText = scope.Find<TextBlock>(DragCoverTextPart);
        _outputPane = scope.Find<Border>(OutputPanePart);
        _fileName = scope.Find<TextBlock>(FileNamePart);
        _modelMark = scope.Find<ArcGlyph>(ModelMarkPart);
        _modelName = scope.Find<TextBlock>(ModelNamePart);
        _viewToggle = scope.Find<SegmentedControl>(ViewTogglePart);
        _copy = scope.Find<CopyButton>(CopyPart);
        _layers = scope.Find<DesignToCodeLayers>(LayersPart);
        _codeScroll = scope.Find<ScrollViewer>(CodeScrollPart);
        _codeView = scope.Find<DesignToCodeCodeView>(CodePart);
        _codePlaceholder = scope.Find<ArcContentSwap>(CodePlaceholderPart);
        _previewHost = scope.Find<Decorator>(PreviewHostPart);
        _status = scope.Find<ArcContentSwap>(StatusPart);
        _import = scope.Find<AvaloniaButton>(ImportPart);
        _main = scope.Find<AvaloniaButton>(MainPart);
        _announcer = scope.Find<ExplainerAnnouncer>(AnnouncerPart);

        if (_remove is not null)
        {
            _remove.Click += OnRemove;
        }
        if (_chooseFile is not null)
        {
            _chooseFile.Click += OnChooseFile;
        }
        if (_useSample is not null)
        {
            _useSample.Click += OnUseSample;
        }
        if (_import is not null)
        {
            _import.Click += OnImport;
        }
        if (_main is not null)
        {
            _main.Click += OnMain;
        }
        if (_canvas is not null)
        {
            _canvas.HoverChanged += OnCanvasHover;
            _canvas.TagFocusChanged += OnTagFocus;
            _canvas.TagInvoked += OnTagInvoked;
        }
        if (_codeView is not null)
        {
            _codeView.HoverRegionChanged += OnCodeHover;
            _codeView.Language = Language;
        }
        if (_codeScroll is not null)
        {
            _codeScroll.ScrollChanged += OnCodeScrollChanged;
        }
        if (_viewToggle is not null)
        {
            _toggleReady = false;
            _viewToggle.ItemsSource = new[]
            {
                new SegmentedOption { Value = DesignToCodeView.Code, Label = Labels.Code },
                new SegmentedOption { Value = DesignToCodeView.Preview, Label = Labels.Preview },
            };
            _viewToggle.SelectedValue = View;
            _viewToggle.SelectionChanged += OnViewToggleChanged;
            _toggleReady = true;
        }
        _statusKey = null;
        _placeholderKey = null;
        _previewKey = null;
        _layers?.Show(View == DesignToCodeView.Preview, animate: false);
        RebuildFromState();
        UpdateBreakpoints(Bounds.Width);
        RefreshAll();
    }

    private void Unhook()
    {
        if (_remove is not null)
        {
            _remove.Click -= OnRemove;
        }
        if (_chooseFile is not null)
        {
            _chooseFile.Click -= OnChooseFile;
        }
        if (_useSample is not null)
        {
            _useSample.Click -= OnUseSample;
        }
        if (_import is not null)
        {
            _import.Click -= OnImport;
        }
        if (_main is not null)
        {
            _main.Click -= OnMain;
        }
        if (_canvas is not null)
        {
            _canvas.HoverChanged -= OnCanvasHover;
            _canvas.TagFocusChanged -= OnTagFocus;
            _canvas.TagInvoked -= OnTagInvoked;
            _canvas.ClearRegions();
        }
        if (_codeView is not null)
        {
            _codeView.HoverRegionChanged -= OnCodeHover;
        }
        if (_codeScroll is not null)
        {
            _codeScroll.ScrollChanged -= OnCodeScrollChanged;
        }
        if (_viewToggle is not null)
        {
            _viewToggle.SelectionChanged -= OnViewToggleChanged;
        }
        if (_previewHost is not null)
        {
            _previewHost.Child = null;
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourceProperty)
        {
            OnSourceChanged(change.GetOldValue<DesignToCodeSource?>());
        }
        else if (change.Property == SimulationProperty)
        {
            // Another simulation (or none): a simulated run stops, and an unset Source shows the new design (or the drop zone).
            if (_simulated && IsRunning || Source is null)
            {
                ResetRun(announce: false);
            }
            UpdateLoop();
            RefreshAll();
        }
        else if (change.Property == GeneratorProperty)
        {
            if (Phase is DesignToCodePhase.Empty or DesignToCodePhase.Ready)
            {
                ResetRun(announce: false);
            }
            UpdateLoop();
        }
        else if (change.Property == ViewProperty)
        {
            if (_viewToggle is not null && !Equals(_viewToggle.SelectedValue, View))
            {
                _toggleReady = false;
                _viewToggle.SelectedValue = View;
                _toggleReady = true;
            }
            _layers?.Show(View == DesignToCodeView.Preview, animate: true);
            RefreshPreview();
        }
        else if (change.Property == LabelsProperty)
        {
            if (_viewToggle is not null)
            {
                _toggleReady = false;
                _viewToggle.ItemsSource = new[]
                {
                    new SegmentedOption { Value = DesignToCodeView.Code, Label = Labels.Code },
                    new SegmentedOption { Value = DesignToCodeView.Preview, Label = Labels.Preview },
                };
                _viewToggle.SelectedValue = View;
                _toggleReady = true;
            }
            _canvas?.RefreshTagNames();
            _statusKey = null;
            _placeholderKey = null;
            RefreshAll();
        }
        else if (change.Property == SplitWidthProperty || change.Property == CompactWidthProperty || change.Property == NarrowWidthProperty)
        {
            UpdateBreakpoints(Bounds.Width);
        }
        else if (change.Property == LanguageProperty)
        {
            if (_codeView is not null)
            {
                _codeView.Language = Language;
            }
        }
        else if (change.Property == FileNameProperty || change.Property == ModelProperty || change.Property == AcceptedFileTypesProperty
                 || change.Property == MaxFileSizeProperty || change.Property == AutoPlayProperty || change.Property == PreviewFactoryProperty)
        {
            if (change.Property == AutoPlayProperty)
            {
                UpdateLoop();
            }
            if (change.Property == PreviewFactoryProperty)
            {
                _previewKey = null;
            }
            RefreshAll();
        }
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateBreakpoints(e.NewSize.Width);
    }

    /// <summary>The layout follows the card's own width: stacked under 640, compact footer under 520, narrow under 440.</summary>
    private void UpdateBreakpoints(double width)
    {
        if (width <= 0)
        {
            return;
        }
        var stacked = width < SplitWidth;
        var isCompact = width < CompactWidth;
        var isNarrow = width < NarrowWidth;
        if (stacked == _stacked && isCompact == _compact && isNarrow == _narrow && _split?.IsStacked == stacked)
        {
            return;
        }
        _stacked = stacked;
        _compact = isCompact;
        _narrow = isNarrow;
        PseudoClasses.Set(StackedPseudoClass, stacked);
        PseudoClasses.Set(CompactPseudoClass, isCompact);
        PseudoClasses.Set(NarrowPseudoClass, isNarrow);
        if (_split is not null)
        {
            _split.IsStacked = stacked;
        }
        _statusKey = null;
        RefreshAll();
    }

    private void OnViewToggleChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_toggleReady || _viewToggle?.SelectedValue is not DesignToCodeView view)
        {
            return;
        }
        SetCurrentValue(ViewProperty, view);
    }

    /// <summary>Stops the sample loop at the first pointer press inside the card.</summary>
    private void OnAnyPointerPressed(object? sender, PointerPressedEventArgs e) => StopAutoPlay();

    /// <summary>Stops the sample loop at the first key press inside the card; Escape stops a run.</summary>
    private void OnAnyKeyDown(object? sender, KeyEventArgs e)
    {
        StopAutoPlay();
        if (e.Key == Key.Escape && IsRunning)
        {
            Stop();
            e.Handled = true;
        }
    }

    private void OnMain(object? sender, RoutedEventArgs e)
    {
        if (IsRunning)
        {
            Stop();
        }
        else
        {
            Generate();
        }
    }

    private void OnRemove(object? sender, RoutedEventArgs e)
    {
        StopAutoPlay();
        _sourceCleared = true;
        SetCurrentValue(SourceProperty, null);
        // Unset or already null: the effective source changed all the same.
        OnSourceChanged(null);
        // Remove clicked with the mouse hands focus on without the keyboard ring.
        _chooseFile?.Focus(SpecialFocusModality.For(this));
    }

    private void OnUseSample(object? sender, RoutedEventArgs e) => UseSample();

    private void OnChooseFile(object? sender, RoutedEventArgs e)
    {
        StopAutoPlay();
        var args = new RoutedEventArgs(ChooseFileRequestedEvent, this);
        RaiseEvent(args);
        if (!args.Handled)
        {
            // No picker in the app: the simulation's design stands in, and says that nothing was opened.
            if (Simulation?.Source is { } sample)
            {
                UseSample();
                Announce(Labels.AnnounceSource(sample.Name) + " " + Labels.SimulationNote);
            }
        }
    }

    private void OnImport(object? sender, RoutedEventArgs e) => _ = ImportDesignFileAsync();

    private void OnCanvasHover(string? region)
    {
        _canvasHover = region;
        UpdateHighlight();
    }

    private void OnCodeHover(string? region)
    {
        _codeHover = region;
        UpdateHighlight();
    }

    private void OnTagFocus(string? region)
    {
        _focusRegion = region;
        UpdateHighlight();
        if (region is not null)
        {
            ScrollRegionIntoView(region);
        }
    }

    private void OnTagInvoked(string region)
    {
        if (View != DesignToCodeView.Code)
        {
            SetCurrentValue(ViewProperty, DesignToCodeView.Code);
        }
        ScrollRegionIntoView(region);
    }

    private string? _canvasHover;
    private string? _codeHover;
    private string? _focusRegion;

    /// <summary>The region pointed at (on the design or in the code) or focused; its box and its lines light up together.</summary>
    internal string? HighlightedRegion => _canvasHover ?? _codeHover ?? _focusRegion;

    private void UpdateHighlight()
    {
        var region = HighlightedRegion;
        if (_canvas is not null)
        {
            _canvas.Highlighted = region;
        }
        if (_codeView is not null)
        {
            _codeView.HighlightedRegion = region;
        }
    }

    /// <summary>Scrolls the lines of <paramref name="region"/> into view in the code.</summary>
    internal void ScrollRegionIntoView(string region)
    {
        if (_codeView?.LinesOf(region) is not { } lines)
        {
            return;
        }
        _following = false;
        _codeView.BringIntoView(_codeView.LineRect(lines.First, lines.Last));
    }

    /// <summary>Says <paramref name="text"/> through the polite live region.</summary>
    internal void Announce(string text)
    {
        LastAnnouncement = text;
        // The same sentence twice is still read again.
        _announcer?.Announce(text);
    }

    /// <summary>The last sentence announced (tests).</summary>
    internal string? LastAnnouncement { get; private set; }

    /// <summary>Brings every part in line with the state; a part that already shows the value is left alone.</summary>
    private void RefreshAll()
    {
        if (_main is null)
        {
            return;
        }
        var labels = Labels;
        var source = EffectiveSource;
        var phase = Phase;
        var running = IsRunning;
        PseudoClasses.Set(EmptyPseudoClass, source is null);
        PseudoClasses.Set(RunningPseudoClass, running);
        PseudoClasses.Set(HasCodePseudoClass, Code.Length > 0);
        PseudoClasses.Set(ErrorPseudoClass, ErrorMessage is not null);
        AutomationProperties.SetName(this, labels.Title);
        if (_designPane is not null)
        {
            AutomationProperties.SetName(_designPane, labels.Design);
        }
        if (_outputPane is not null)
        {
            AutomationProperties.SetName(_outputPane, labels.Output);
        }
        if (_designLabel is not null)
        {
            _designLabel.Text = labels.Design;
        }

        // Design side.
        if (_canvas is not null)
        {
            _canvas.Source = source;
            _canvas.IsVisible = source is not null;
            _canvas.TagName = labels.RegionTag;
            if (source is not null)
            {
                AutomationProperties.SetName(_canvas.Shot, labels.Screenshot(source.Name));
            }
        }
        if (_sourceName is not null)
        {
            _sourceName.Text = source?.Name;
            _sourceName.IsVisible = source is not null;
        }
        if (_sourceKind is not null)
        {
            var kind = source?.Kind switch
            {
                DesignToCodeSourceKind.Sample => labels.Sample,
                DesignToCodeSourceKind.DesignFile => labels.DesignFile,
                _ => null,
            };
            _sourceKind.Text = kind;
            if (_sourceKind.Parent is Control pill)
            {
                pill.IsVisible = kind is not null;
            }
        }
        if (_remove is not null)
        {
            _remove.IsVisible = source is not null;
            AutomationProperties.SetName(_remove, labels.Remove);
            Avalonia.Controls.ToolTip.SetTip(_remove, labels.Remove);
        }
        if (_dropZone is not null)
        {
            _dropZone.IsVisible = source is null;
        }
        if (_dropTitle is not null)
        {
            _dropTitle.Text = labels.DropTitle;
        }
        if (_dropHint is not null)
        {
            _dropHint.Text = labels.DropHint(DesignToCodeFormat.TypesText(AcceptedFileTypes), DesignToCodeFormat.SizeText(MaxFileSize));
        }
        if (_dropError is not null)
        {
            _dropError.Text = ErrorMessage;
            _dropError.IsVisible = source is null && ErrorMessage is not null;
        }
        if (_chooseFile is not null)
        {
            _chooseFile.Content = labels.ChooseFile;
        }
        if (_useSample is not null)
        {
            _useSample.Content = labels.UseSample;
            _useSample.IsVisible = Simulation?.Source is not null;
        }
        if (_dragCoverText is not null)
        {
            _dragCoverText.Text = labels.DropCover;
        }

        // Output header.
        if (_fileName is not null)
        {
            _fileName.Text = EffectiveFileName;
        }
        var model = Model ?? (CanSimulate ? Simulation!.Model : null);
        if (_modelName is not null)
        {
            _modelName.Text = model?.Name;
        }
        if (_modelMark is not null)
        {
            _modelMark.Data = model?.Mark ?? ArcIcons.Sparkles;
            if (model?.MarkBrush is { } markBrush)
            {
                _modelMark.Foreground = markBrush;
            }
            else
            {
                _modelMark.ClearValue(ArcGlyph.ForegroundProperty);
            }
            AutomationProperties.SetName(_modelMark, _modelName?.Text);
        }
        if (_viewToggle is not null)
        {
            AutomationProperties.SetName(_viewToggle, labels.ViewToggle);
        }
        if (_copy is not null)
        {
            if (_copy.Value != Code)
            {
                _copy.Value = Code;
            }
            _copy.IsEnabled = Code.Length > 0;
            AutomationProperties.SetName(_copy, labels.Copy(EffectiveFileName));
        }
        if (_codeScroll is not null)
        {
            AutomationProperties.SetName(_codeScroll, labels.CodeArea(EffectiveFileName));
            AutomationProperties.SetItemStatus(_codeScroll, phase == DesignToCodePhase.Writing ? labels.Busy : null);
        }
        RefreshPlaceholder();
        RefreshPreview();
        RefreshStatus();

        // Footer actions.
        if (_import is not null)
        {
            var key = _compact ? "icon" : "full";
            if (_import.Tag as string != key)
            {
                _import.Tag = key;
                _import.Content = ActionContent(ArcIcons.LayoutTemplate, _compact ? null : labels.Import);
            }
            else if (!_compact && _import.Content is StackPanel { Children: [_, TextBlock text] } && text.Text != labels.Import)
            {
                text.Text = labels.Import;
            }
            AutomationProperties.SetName(_import, labels.Import);
            Avalonia.Controls.ToolTip.SetTip(_import, _compact ? labels.Import : null);
            _import.IsEnabled = !running;
        }
        var main = running
            ? labels.Stop
            : phase switch
            {
                DesignToCodePhase.Done or DesignToCodePhase.Stopped => labels.Regenerate,
                DesignToCodePhase.Error => labels.TryAgain,
                _ => labels.Generate,
            };
        if (_main.Content as string != main)
        {
            _main.Content = main;
        }
        _main.IsEnabled = running || source is not null;
        if (_main is Button arcMain)
        {
            arcMain.Variant = running ? ButtonVariant.Secondary : ButtonVariant.Primary;
        }
    }

    private static StackPanel ActionContent(Geometry icon, string? text)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(new ArcGlyph { Data = icon, Size = 16, VerticalAlignment = VerticalAlignment.Center });
        if (text is not null)
        {
            panel.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
        }
        return panel;
    }

    /// <summary>The code side's placeholder while there is no code: what to do next, crossfading as the phase changes.</summary>
    private void RefreshPlaceholder()
    {
        if (_codePlaceholder is null)
        {
            return;
        }
        var labels = Labels;
        var text = Code.Length > 0
            ? null
            : EffectiveSource is null
                ? labels.PlaceholderEmpty
                : Phase switch
                {
                    DesignToCodePhase.Analyzing or DesignToCodePhase.Writing => labels.PlaceholderAnalyzing,
                    DesignToCodePhase.Error => ErrorMessage,
                    _ => labels.PlaceholderReady(EffectiveFileName),
                };
        if (_placeholderKey == text && _codePlaceholder.Content is not null == text is not null)
        {
            return;
        }
        _placeholderKey = text;
        _codePlaceholder.IsHitTestVisible = false;
        _codePlaceholder.Content = text is null ? null : Placeholder(ArcIcons.Code, text);
    }

    private static StackPanel Placeholder(Geometry icon, string text)
    {
        var panel = new StackPanel
        {
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 260,
        };
        var glyph = new ArcGlyph { Data = icon, Size = 20, HorizontalAlignment = HorizontalAlignment.Center };
        ArcTokens.Bind(glyph, ArcGlyph.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        var block = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center };
        ArcTokens.Bind(block, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        ArcTokens.Bind(block, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeSM);
        panel.Children.Add(glyph);
        panel.Children.Add(block);
        return panel;
    }

    /// <summary>The preview layer: the app's renderer, the working sample card, or a short note.</summary>
    private void RefreshPreview()
    {
        if (_previewHost is null)
        {
            return;
        }
        var source = EffectiveSource;
        var labels = Labels;
        var ready = Phase == DesignToCodePhase.Done && source is not null;
        var factory = EffectivePreviewFactory;
        object key = !ready
            ? labels.PreviewPending
            : factory is not null
                ? (Code, source!)
                : labels.PreviewUnavailable;
        if (Equals(_previewKey, key))
        {
            return;
        }
        // An app renderer is only called once the Preview tab is open, once per finished run.
        if (ready && factory is not null && View != DesignToCodeView.Preview)
        {
            return;
        }
        _previewKey = key;
        Control? content = key switch
        {
            string text => Placeholder(ArcIcons.Eye, text),
            _ => factory?.Invoke(Code, source!) ?? Placeholder(ArcIcons.Eye, labels.PreviewUnavailable),
        };
        _previewFromFactory = key is not string && content is not null;
        if (!ReferenceEquals(_previewHost.Child, content))
        {
            _previewHost.Child = content;
        }
    }

    /// <summary>The footer status: an icon with a short label, the file, and meta text; it crossfades when it changes.</summary>
    private void RefreshStatus()
    {
        var labels = Labels;
        var source = EffectiveSource;
        var regions = _regions.Count;
        (Geometry Icon, string Label, string? File, string? Meta, ArcDesignTokenKind Tone) status = ErrorMessage is { } error
            ? (ArcIcons.CircleAlert, error, null, null, ArcDesignTokenKind.Danger)
            : Phase switch
            {
                DesignToCodePhase.Empty => (ArcIcons.Upload, labels.StatusEmpty, null, null, ArcDesignTokenKind.TextMuted),
                DesignToCodePhase.Analyzing => (ArcIcons.ScanText, labels.StatusAnalyzing, source?.Name, regions > 0 ? labels.Regions(regions) : null,
                    ArcDesignTokenKind.Accent),
                DesignToCodePhase.Writing => (ArcIcons.Code, labels.StatusWriting, EffectiveFileName, labels.Regions(regions), ArcDesignTokenKind.Accent),
                DesignToCodePhase.Done => (ArcIcons.CircleCheck, labels.StatusDone, EffectiveFileName, labels.DoneMeta(_doneLines, _doneElapsed),
                    ArcDesignTokenKind.Success),
                DesignToCodePhase.Stopped => (ArcIcons.Square, labels.StatusStopped, EffectiveFileName, labels.StoppedMeta, ArcDesignTokenKind.TextMuted),
                _ => (ArcIcons.Image, labels.StatusReady, source?.Name, null, ArcDesignTokenKind.TextMuted),
            };
        var file = _compact ? null : status.File;
        var meta = _narrow ? null : status.Meta;
        StatusParts = (status.Icon, status.Label, file, meta);
        var key = string.Join("\u001F", status.Label, file, meta, status.Tone.ToString(), status.Icon.GetHashCode().ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (_status is null || key == _statusKey)
        {
            return;
        }
        _statusKey = key;
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        var glyph = new ArcGlyph { Data = status.Icon, Size = 16, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(glyph, ArcGlyph.ForegroundProperty, status.Tone);
        panel.Children.Add(glyph);
        var label = new TextBlock { Text = status.Label, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        ArcTokens.Bind(label, TextBlock.ForegroundProperty, ErrorMessage is not null ? ArcDesignTokenKind.Danger : ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(label, TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        panel.Children.Add(label);
        if (file is not null)
        {
            var fileText = new TextBlock { Text = file, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            ArcTokens.Bind(fileText, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
            panel.Children.Add(fileText);
        }
        if (meta is not null)
        {
            var metaText = new TextBlock { Text = "· " + meta, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            ArcTokens.Bind(metaText, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
            panel.Children.Add(metaText);
        }
        _status.Content = panel;
    }
}
