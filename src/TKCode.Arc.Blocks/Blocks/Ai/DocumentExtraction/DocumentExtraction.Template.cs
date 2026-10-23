using System.Collections.Generic;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

[TemplatePart(CardPart, typeof(Border))]
[TemplatePart(BodyPart, typeof(ExtractionBody))]
[TemplatePart(ViewerPart, typeof(DocumentViewer))]
[TemplatePart(FormPanePart, typeof(Border))]
[TemplatePart(TabsPart, typeof(ExtractionTabStrip))]
[TemplatePart(ActionPart, typeof(Button))]
[TemplatePart(FieldsScrollPart, typeof(ScrollViewer))]
[TemplatePart(FieldsContentPart, typeof(Panel))]
[TemplatePart(StatusPart, typeof(AiRunStatusLine))]
[TemplatePart(FormPart, typeof(StackPanel))]
[TemplatePart(JsonScrollPart, typeof(ScrollViewer))]
[TemplatePart(JsonPart, typeof(ExtractionJsonView))]
[TemplatePart(JsonNotePart, typeof(TextBlock))]
[TemplatePart(JsonNoteIconPart, typeof(ArcGlyph))]
[TemplatePart(JsonCopyPart, typeof(CopyButton))]
[TemplatePart(SchemaScrollPart, typeof(ScrollViewer))]
[TemplatePart(SchemaPart, typeof(ExtractionJsonView))]
[TemplatePart(SchemaNotePart, typeof(TextBlock))]
[TemplatePart(SchemaCopyPart, typeof(CopyButton))]
[TemplatePart(PanelRingPart, typeof(Control))]
[TemplatePart(LinksPart, typeof(SourceLinkOverlay))]
[TemplatePart(FooterPart, typeof(ExtractionFooter))]
[TemplatePart(BarsPart, typeof(ConfidenceBars))]
[TemplatePart(SummaryNotePart, typeof(TextBlock))]
[TemplatePart(AverageGroupPart, typeof(Control))]
[TemplatePart(AveragePart, typeof(TextBlock))]
[TemplatePart(AverageWordPart, typeof(TextBlock))]
[TemplatePart(FlagPart, typeof(Control))]
[TemplatePart(FlagIconPart, typeof(ArcGlyph))]
[TemplatePart(FlagTextPart, typeof(TextBlock))]
[TemplatePart(MetaPart, typeof(AiRunMeta))]
[TemplatePart(AcceptAllPart, typeof(Button))]
[TemplatePart(RunAnnouncerPart, typeof(AiAnnouncer))]
[TemplatePart(ReviewAnnouncerPart, typeof(AiAnnouncer))]
[PseudoClasses(StackedPseudoClass, FooterWrapPseudoClass, NarrowPseudoClass, RunningPseudoClass, AcceptedPseudoClass)]
public partial class DocumentExtraction
{
    public const string CardPart = "PART_Card";
    public const string BodyPart = "PART_Body";
    public const string ViewerPart = "PART_Viewer";
    public const string FormPanePart = "PART_FormPane";
    public const string TabsPart = "PART_Tabs";
    public const string ActionPart = "PART_Action";
    public const string FieldsScrollPart = "PART_FieldsScroll";
    public const string FieldsContentPart = "PART_FieldsContent";
    public const string StatusPart = "PART_Status";
    public const string FormPart = "PART_Form";
    public const string JsonScrollPart = "PART_JsonScroll";
    public const string JsonPart = "PART_Json";
    public const string JsonNotePart = "PART_JsonNote";
    public const string JsonNoteIconPart = "PART_JsonNoteIcon";
    public const string JsonCopyPart = "PART_JsonCopy";
    public const string SchemaScrollPart = "PART_SchemaScroll";
    public const string SchemaPart = "PART_Schema";
    public const string SchemaNotePart = "PART_SchemaNote";
    public const string SchemaCopyPart = "PART_SchemaCopy";
    public const string PanelRingPart = "PART_PanelRing";
    public const string LinksPart = "PART_Links";
    public const string FooterPart = "PART_Footer";
    public const string BarsPart = "PART_Bars";
    public const string SummaryNotePart = "PART_SummaryNote";
    public const string AverageGroupPart = "PART_AverageGroup";
    public const string AveragePart = "PART_Average";
    public const string AverageWordPart = "PART_AverageWord";
    public const string FlagPart = "PART_Flag";
    public const string FlagIconPart = "PART_FlagIcon";
    public const string FlagTextPart = "PART_FlagText";
    public const string MetaPart = "PART_Meta";
    public const string AcceptAllPart = "PART_AcceptAll";
    public const string RunAnnouncerPart = "PART_RunAnnouncer";
    public const string ReviewAnnouncerPart = "PART_ReviewAnnouncer";

    /// <summary>Set while the block is narrower than 760: the viewer above the form, no links.</summary>
    public const string StackedPseudoClass = ":stacked";

    /// <summary>Set while the block is narrower than 700: Accept all on the footer's second row.</summary>
    public const string FooterWrapPseudoClass = ":footer-wrap";

    /// <summary>Set while the block is narrower than 520: the narrow field grid, footer and button labels.</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Set while a run is going (reading or extracting).</summary>
    public const string RunningPseudoClass = ":running";

    /// <summary>Set while every field is accepted.</summary>
    public const string AcceptedPseudoClass = ":accepted";

    /// <summary>The tab panels' crossfade (spec 5.2: the old one out in 120ms, the new one in over 240ms).</summary>
    private static readonly TimeSpan PanelOut = TimeSpan.FromMilliseconds(120);

    private static readonly TimeSpan PanelIn = TimeSpan.FromMilliseconds(240);

    #region Token-driven internals

    internal static readonly StyledProperty<double> StackBreakpointProperty =
        AvaloniaProperty.Register<DocumentExtraction, double>(nameof(StackBreakpoint), 760);

    internal static readonly StyledProperty<double> FooterWrapBreakpointProperty =
        AvaloniaProperty.Register<DocumentExtraction, double>(nameof(FooterWrapBreakpoint), 700);

    internal static readonly StyledProperty<double> NarrowBreakpointProperty =
        AvaloniaProperty.Register<DocumentExtraction, double>(nameof(NarrowBreakpoint), 520);

    /// <summary>Below this width the panes stack (the token's 760).</summary>
    internal double StackBreakpoint { get => GetValue(StackBreakpointProperty); set => SetValue(StackBreakpointProperty, value); }

    /// <summary>Below this width the footer wraps (the token's 700).</summary>
    internal double FooterWrapBreakpoint { get => GetValue(FooterWrapBreakpointProperty); set => SetValue(FooterWrapBreakpointProperty, value); }

    /// <summary>Below this width the layout is narrow (the token's 520).</summary>
    internal double NarrowBreakpoint { get => GetValue(NarrowBreakpointProperty); set => SetValue(NarrowBreakpointProperty, value); }

    #endregion

    private Border? _card;
    private ExtractionBody? _body;
    private DocumentViewer? _viewer;
    private Border? _formPane;
    private ExtractionTabStrip? _tabs;
    private Button? _action;
    private ScrollViewer? _fieldsScroll;
    private Panel? _fieldsContent;
    private AiRunStatusLine? _status;
    private StackPanel? _form;
    private ScrollViewer? _jsonScroll;
    private ExtractionJsonView? _json;
    private TextBlock? _jsonNote;
    private ArcGlyph? _jsonNoteIcon;
    private CopyButton? _jsonCopy;
    private ScrollViewer? _schemaScroll;
    private ExtractionJsonView? _schema;
    private TextBlock? _schemaNote;
    private CopyButton? _schemaCopy;
    private Control? _panelRing;
    private SourceLinkOverlay? _links;
    private ExtractionFooter? _footer;
    private ConfidenceBars? _bars;
    private TextBlock? _summaryNote;
    private Control? _averageGroup;
    private TextBlock? _average;
    private TextBlock? _averageWord;
    private Control? _flag;
    private ArcGlyph? _flagIcon;
    private TextBlock? _flagText;
    private AiRunMeta? _meta;
    private Button? _acceptAll;
    private AiAnnouncer? _runAnnouncer;
    private AiAnnouncer? _reviewAnnouncer;
    private readonly TranslateTransform _statusShift = new();
    private MotionValue<double>? _fadeFields;
    private MotionValue<double>? _fadeJson;
    private MotionValue<double>? _fadeSchema;
    private bool _isStacked;
    private bool _isFooterWrap;
    private bool _isNarrow;
    private bool _keyboardMode;
    private bool _actionShort;
    private double _longActionWidth;

    static DocumentExtraction()
    {
        FocusableProperty.OverrideDefaultValue<DocumentExtraction>(false);
    }

    /// <summary>Creates an empty block: give it <see cref="Samples"/> (or a <see cref="Document"/> and a <see cref="Schema"/>) and an <see cref="Extractor"/>.</summary>
    public DocumentExtraction()
    {
        _ticker = new LiveLoop(this, Tick)
        {
            // The clock, the fresh tints and the accept cascade are state, not decoration: they run at every motion level.
            MinimumLevel = ArcMotionLevel.Off,
            TracksViewport = false
        };
        // Where the last input came from decides whether a focus move shows the keyboard ring.
        AddHandler(KeyDownEvent, (_, _) => _keyboardMode = true, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerPressedEvent, OnAnyPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerWheelChangedEvent, OnAnyWheel, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(ExtractionFieldRow.PathEnteredEvent, OnFieldPathEntered);
        AddHandler(ExtractionFieldRow.PathExitedEvent, OnFieldPathExited);
        AddHandler(ExtractionFieldRow.PathFocusedEvent, OnFieldPathFocused);
        AddHandler(ExtractionFieldRow.ValueEditedEvent, OnFieldValueEdited);
        AddHandler(ExtractionFieldRow.LooksRightRequestedEvent, OnLooksRightRequested);
        AddHandler(ExtractionJsonView.PathEnteredEvent, OnJsonPathEntered);
        AddHandler(ExtractionJsonView.PathExitedEvent, OnJsonPathExited);
        AddHandler(ExtractionJsonView.PathPressedEvent, OnJsonPathPressed);
        AddHandler(DocumentPage.SpanPointerEnteredEvent, OnSpanEntered);
        AddHandler(DocumentPage.SpanPointerExitedEvent, OnSpanExited);
        AddHandler(DocumentPage.SpanPressedEvent, OnSpanPressed);
        AddHandler(AiRunStatusLine.ActionRequestedEvent, OnReviewRequested);
        Samples.CollectionChanged += OnSamplesChanged;
    }

    #region Test surface

    /// <summary>The run-status live region's last announcement (tests).</summary>
    internal string? LastRunAnnouncement => _runAnnouncer?.Announcement;

    /// <summary>The checks-and-accepts live region's last announcement (tests).</summary>
    internal string? LastReviewAnnouncement => _reviewAnnouncer?.Announcement;

    internal DocumentViewer? Viewer => _viewer;
    internal ExtractionTabStrip? TabStrip => _tabs;
    internal Button? ActionButton => _action;
    internal Button? AcceptAllButton => _acceptAll;
    internal AiRunStatusLine? StatusLine => _status;
    internal ScrollViewer? FieldsScroll => _fieldsScroll;
    internal ScrollViewer? JsonScroll => _jsonScroll;
    internal ScrollViewer? SchemaScroll => _schemaScroll;
    internal ExtractionJsonView? JsonView => _json;
    internal ExtractionJsonView? SchemaView => _schema;
    internal TextBlock? JsonNote => _jsonNote;
    internal TextBlock? SchemaNote => _schemaNote;
    internal CopyButton? JsonCopy => _jsonCopy;
    internal SourceLinkOverlay? Links => _links;
    internal ConfidenceBars? Bars => _bars;
    internal AiRunMeta? Meta => _meta;
    internal ExtractionFooter? Footer => _footer;
    internal ExtractionBody? Body => _body;
    internal Border? FormPane => _formPane;
    internal StackPanel? FormPanel => _form;
    internal TextBlock? SummaryNote => _summaryNote;
    internal TextBlock? AverageText => _average;
    internal TextBlock? FlagText => _flagText;
    internal Control? FlagGroup => _flag;
    internal Control? AverageGroup => _averageGroup;
    internal Control? PanelRing => _panelRing;

    /// <summary>The row of a field (null for a table or an unknown key).</summary>
    internal ExtractionFieldRow? RowOf(string key) => _rows.GetValueOrDefault(key);

    /// <summary>The grid of a table field.</summary>
    internal ExtractionTableGrid? TableOf(string key) => _tables.GetValueOrDefault(key);

    /// <summary>Whether the stacked layout is in use.</summary>
    internal bool IsStackedLayout => _isStacked;

    /// <summary>Whether the footer wraps.</summary>
    internal bool IsFooterWrapLayout => _isFooterWrap;

    /// <summary>Whether the narrow layout is in use.</summary>
    internal bool IsNarrowLayout => _isNarrow;

    #endregion

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        UnhookParts();
        base.OnApplyTemplate(e);
        var scope = e.NameScope;
        _card = scope.Find<Border>(CardPart);
        _body = scope.Find<ExtractionBody>(BodyPart);
        _viewer = scope.Find<DocumentViewer>(ViewerPart);
        _formPane = scope.Find<Border>(FormPanePart);
        _tabs = scope.Find<ExtractionTabStrip>(TabsPart);
        _action = scope.Find<Button>(ActionPart);
        _fieldsScroll = scope.Find<ScrollViewer>(FieldsScrollPart);
        _fieldsContent = scope.Find<Panel>(FieldsContentPart);
        _status = scope.Find<AiRunStatusLine>(StatusPart);
        _form = scope.Find<StackPanel>(FormPart);
        _jsonScroll = scope.Find<ScrollViewer>(JsonScrollPart);
        _json = scope.Find<ExtractionJsonView>(JsonPart);
        _jsonNote = scope.Find<TextBlock>(JsonNotePart);
        _jsonNoteIcon = scope.Find<ArcGlyph>(JsonNoteIconPart);
        _jsonCopy = scope.Find<CopyButton>(JsonCopyPart);
        _schemaScroll = scope.Find<ScrollViewer>(SchemaScrollPart);
        _schema = scope.Find<ExtractionJsonView>(SchemaPart);
        _schemaNote = scope.Find<TextBlock>(SchemaNotePart);
        _schemaCopy = scope.Find<CopyButton>(SchemaCopyPart);
        _panelRing = scope.Find<Control>(PanelRingPart);
        _links = scope.Find<SourceLinkOverlay>(LinksPart);
        _footer = scope.Find<ExtractionFooter>(FooterPart);
        _bars = scope.Find<ConfidenceBars>(BarsPart);
        _summaryNote = scope.Find<TextBlock>(SummaryNotePart);
        _averageGroup = scope.Find<Control>(AverageGroupPart);
        _average = scope.Find<TextBlock>(AveragePart);
        _averageWord = scope.Find<TextBlock>(AverageWordPart);
        _flag = scope.Find<Control>(FlagPart);
        _flagIcon = scope.Find<ArcGlyph>(FlagIconPart);
        _flagText = scope.Find<TextBlock>(FlagTextPart);
        _meta = scope.Find<AiRunMeta>(MetaPart);
        _acceptAll = scope.Find<Button>(AcceptAllPart);
        _runAnnouncer = scope.Find<AiAnnouncer>(RunAnnouncerPart);
        _reviewAnnouncer = scope.Find<AiAnnouncer>(ReviewAnnouncerPart);
        HookParts();

        if (_status is not null)
        {
            _status.RenderTransform = _statusShift;
        }
        _fadeFields = PanelFade(_fieldsScroll);
        _fadeJson = PanelFade(_jsonScroll);
        _fadeSchema = PanelFade(_schemaScroll);
        if (_viewer is not null)
        {
            _viewer.Document = EffectiveDocument;
        }
        if (_meta is not null)
        {
            _meta.ModelMark = ModelMark;
        }
        BuildForm();
        SyncSources();
        ApplyLabels();
        ApplyWidthClasses(force: true);
        ShowTab(SelectedTab, animate: false);
        RepaintAllSpans();
        RefreshAll();
    }

    private MotionValue<double>? PanelFade(ScrollViewer? panel)
    {
        if (panel is null)
        {
            return null;
        }
        var fade = new MotionValue<double>(panel, 1, MotionVectors.Double);
        fade.Changed += opacity => panel.Opacity = opacity;
        return fade;
    }

    private void HookParts()
    {
        if (_action is not null)
        {
            _action.Click += OnActionClick;
            // A new label re-lays out the head only: the fit is checked when the button or the tabs change size.
            _action.SizeChanged += OnHeadSizeChanged;
        }
        if (_tabs is not null)
        {
            _tabs.SizeChanged += OnHeadSizeChanged;
        }
        if (_acceptAll is not null)
        {
            _acceptAll.Click += OnAcceptAllClick;
        }
        if (_viewer is not null)
        {
            _viewer.PropertyChanged += OnViewerPropertyChanged;
        }
        foreach (var panel in Panels())
        {
            panel.ScrollChanged += OnPanelScrollChanged;
            panel.GotFocus += OnPanelGotFocus;
            panel.LostFocus += OnPanelLostFocus;
        }
    }

    private void UnhookParts()
    {
        if (_action is not null)
        {
            _action.Click -= OnActionClick;
            _action.SizeChanged -= OnHeadSizeChanged;
        }
        if (_tabs is not null)
        {
            _tabs.SizeChanged -= OnHeadSizeChanged;
        }
        if (_acceptAll is not null)
        {
            _acceptAll.Click -= OnAcceptAllClick;
        }
        if (_viewer is not null)
        {
            _viewer.PropertyChanged -= OnViewerPropertyChanged;
        }
        foreach (var panel in Panels())
        {
            panel.ScrollChanged -= OnPanelScrollChanged;
            panel.GotFocus -= OnPanelGotFocus;
            panel.LostFocus -= OnPanelLostFocus;
        }
        _fadeFields?.Dispose();
        _fadeJson?.Dispose();
        _fadeSchema?.Dispose();
        _fadeFields = _fadeJson = _fadeSchema = null;
        _formScroll?.Dispose();
        _formScroll = null;
        // The rolling readouts belong to the old template's parts.
        _costShown?.Dispose();
        _costShown = null;
        _averageShown?.Dispose();
        _averageShown = null;
        ForgetReadouts();
    }

    private IEnumerable<ScrollViewer> Panels()
    {
        if (_fieldsScroll is not null)
        {
            yield return _fieldsScroll;
        }
        if (_jsonScroll is not null)
        {
            yield return _jsonScroll;
        }
        if (_schemaScroll is not null)
        {
            yield return _schemaScroll;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        RefreshAll();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // A run does not outlive the block: it stops, keeping what it found.
        if (_run is not null)
        {
            EndRun(ExtractionState.Stopped, null);
        }
        FinishCascadeAtOnce();
        _hoverPath = null;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DocumentProperty || change.Property == SchemaProperty || change.Property == SelectedSampleProperty)
        {
            OnSourceChanged();
        }
        else if (change.Property == ExtractorProperty)
        {
            RefreshButtons();
        }
        else if (change.Property == ModelProperty)
        {
            RefreshSchemaText();
            RefreshMeta();
        }
        else if (change.Property == ModelMarkProperty)
        {
            if (_meta is not null)
            {
                _meta.ModelMark = change.NewValue;
            }
        }
        else if (change.Property == ReviewThresholdProperty)
        {
            _store?.RefreshThreshold();
        }
        else if (change.Property == SelectedPathProperty)
        {
            OnSelectedPathChanged(change.GetOldValue<string?>(), change.GetNewValue<string?>());
        }
        else if (change.Property == SelectedTabProperty)
        {
            ShowTab(change.GetNewValue<ExtractionTab>(), animate: true);
        }
        else if (change.Property == LabelsProperty)
        {
            ApplyLabels();
        }
        else if (change.Property == StateProperty)
        {
            var state = change.GetNewValue<ExtractionState>();
            PseudoClasses.Set(RunningPseudoClass, state is ExtractionState.Reading or ExtractionState.Extracting);
            PseudoClasses.Set(AcceptedPseudoClass, state == ExtractionState.Accepted);
        }
    }

    /// <summary>Says <paramref name="text"/> in the run-status live region.</summary>
    private void AnnounceRun(string? text) => _runAnnouncer?.Announce(text);

    /// <summary>Says <paramref name="text"/> in the checks-and-accepts live region.</summary>
    private void AnnounceReview(string? text) => _reviewAnnouncer?.Announce(text);

    /// <summary>How a focus move should look: with the ring after a key, without after a pointer.</summary>
    private NavigationMethod FocusMethod => _keyboardMode ? NavigationMethod.Tab : NavigationMethod.Pointer;

    #endregion

    #region Sources

    /// <summary>The sample in use: the selected one, else the first.</summary>
    private ExtractionSample? EffectiveSample => SelectedSample ?? (Samples.Count > 0 ? Samples[0] : null);

    /// <summary>The document read: <see cref="Document"/>, else the sample's.</summary>
    private ExtractionDocument? EffectiveDocument => Document ?? EffectiveSample?.Document;

    /// <summary>The schema used: <see cref="Schema"/>, else the sample's.</summary>
    private ExtractionSchema? EffectiveSchema => Schema ?? EffectiveSample?.Schema;

    private void OnSamplesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (SelectedSample is null && Samples.Count > 0)
        {
            // The first sample becomes the selected one (two-way), which syncs everything.
            SetCurrentValue(SelectedSampleProperty, Samples[0]);
            return;
        }
        if (SelectedSample is { } selected && !Samples.Contains(selected))
        {
            SetCurrentValue(SelectedSampleProperty, Samples.Count > 0 ? Samples[0] : null);
            return;
        }
        SyncSources();
    }

    /// <summary>The file menu of the viewer: the samples' documents while no <see cref="Document"/> is set.</summary>
    private void SyncSources()
    {
        if (_viewer is null)
        {
            return;
        }
        var samples = Document is null && Samples.Count > 0;
        _viewer.Documents = samples ? Samples.Select(s => s.Document).ToList() : null;
        _viewer.SelectedDocument = samples ? EffectiveSample?.Document : null;
        _viewer.Document = EffectiveDocument;
    }

    private void OnViewerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == DocumentViewer.SelectedDocumentProperty && Document is null && e.GetNewValue<ExtractionDocument?>() is { } document)
        {
            // The file menu chose a document: its sample becomes the selected one.
            var sample = Samples.FirstOrDefault(s => ReferenceEquals(s.Document, document));
            if (sample is not null && !ReferenceEquals(sample, SelectedSample))
            {
                SetCurrentValue(SelectedSampleProperty, sample);
            }
        }
    }

    #endregion

    #region Width classes

    protected override Size ArrangeOverride(Size finalSize)
    {
        // Decided from the arranged width (not in measure, where a trial width could flip the layout back and forth).
        ApplyWidthClasses(force: false, finalSize.Width);
        var size = base.ArrangeOverride(finalSize);
        FitAction();
        return size;
    }

    /// <summary>
    /// "Extract again" says only "Extract" (no icon) when the form head has no room for it beside the tabs: in the narrow
    /// layout (spec 7) and in a narrow pane side by side. Back to the long label once it fits again (1px of slack).
    /// </summary>
    private void OnHeadSizeChanged(object? sender, SizeChangedEventArgs e) => FitAction();

    private void FitAction()
    {
        if (_action?.Parent is not Control head || _tabs is null || head.Bounds.Width <= 0 || _actionKind != ActionKind.Again)
        {
            return;
        }
        var room = head.Bounds.Width;
        var tabs = _tabs.NaturalWidth;
        if (!_actionShort && !_isNarrow)
        {
            _longActionWidth = _action.DesiredSize.Width;
        }
        var shorten = tabs + _longActionWidth > room + 0.5;
        if (shorten != _actionShort && (shorten || tabs + _longActionWidth <= room - 1))
        {
            _actionShort = shorten;
            RefreshButtons();
        }
    }

    /// <summary>
    /// <c>:stacked</c> below 760, <c>:footer-wrap</c> below 700 and <c>:narrow</c> below 520 (spec 7), with 1px of
    /// hysteresis: a layout leaves a mode only one pixel past its breakpoint.
    /// </summary>
    private void ApplyWidthClasses(bool force, double? width = null)
    {
        var stacked = _isStacked;
        var wrap = _isFooterWrap;
        var narrow = _isNarrow;
        if (width is { } w && w > 0 && !double.IsInfinity(w))
        {
            stacked = _isStacked ? w < StackBreakpoint + 1 : w < StackBreakpoint;
            wrap = _isFooterWrap ? w < FooterWrapBreakpoint + 1 : w < FooterWrapBreakpoint;
            narrow = _isNarrow ? w < NarrowBreakpoint + 1 : w < NarrowBreakpoint;
        }
        if (!force && stacked == _isStacked && wrap == _isFooterWrap && narrow == _isNarrow)
        {
            return;
        }
        var narrowChanged = narrow != _isNarrow;
        var stackedChanged = stacked != _isStacked;
        _isStacked = stacked;
        _isFooterWrap = wrap;
        _isNarrow = narrow;
        PseudoClasses.Set(StackedPseudoClass, stacked);
        PseudoClasses.Set(FooterWrapPseudoClass, wrap);
        PseudoClasses.Set(NarrowPseudoClass, narrow);
        if (_body is not null)
        {
            _body.IsStacked = stacked;
        }
        if (_viewer is not null)
        {
            _viewer.IsStacked = stacked;
            _viewer.IsNarrow = narrow;
        }
        if (_footer is not null)
        {
            _footer.Mode = narrow ? ExtractionFooterMode.Narrow : wrap ? ExtractionFooterMode.Wrap : ExtractionFooterMode.Row;
        }
        if (_meta is not null)
        {
            _meta.ShowsModel = !narrow;
        }
        if (force || narrowChanged)
        {
            foreach (var row in _rows.Values)
            {
                row.IsNarrow = narrow;
            }
            foreach (var table in _tables.Values)
            {
                table.IsNarrow = narrow;
            }
            RefreshButtons();
        }
        if (force || stackedChanged)
        {
            // Stacked, the panes are far apart: no link is drawn (the tints and the scrolling stay).
            UpdateLink();
        }
    }

    #endregion

    #region Words

    /// <summary>Every word on screen and every accessible name, from <see cref="Labels"/>.</summary>
    private void ApplyLabels()
    {
        var labels = Labels;
        if (_store is not null)
        {
            _store.Labels = labels;
        }
        if (_tabs is not null)
        {
            _tabs.FieldsText = labels.FieldsTab;
            _tabs.JsonText = labels.JsonTab;
            _tabs.SchemaText = labels.SchemaTab;
            _tabs.ListName = labels.Output;
        }
        if (_status is not null)
        {
            _status.ActionText = labels.Review;
        }
        if (_json is not null)
        {
            _json.AutomationName = labels.JsonName;
        }
        if (_schema is not null)
        {
            _schema.AutomationName = labels.SchemaName;
        }
        foreach (var copy in new[] { _jsonCopy, _schemaCopy })
        {
            if (copy is not null)
            {
                copy.Label = labels.Copy;
                copy.CopiedLabel = labels.Copied;
            }
        }
        if (_averageWord is not null)
        {
            _averageWord.Text = labels.AverageWord;
        }
        if (_formPane is not null)
        {
            AutomationProperties.SetName(_formPane, labels.Output);
        }
        ApplyFormLabels();
        RefreshSchemaText();
        ForgetReadouts();
        RefreshAll();
    }

    #endregion

    #region Tabs

    /// <summary>
    /// Shows the panel of <paramref name="tab"/>: the old one fades out in 120ms out of layout while the new one fades in
    /// over 240ms (Reduced: 120ms each; Off and the first show: at once). The other panels stay built and keep their scroll.
    /// </summary>
    private void ShowTab(ExtractionTab tab, bool animate)
    {
        var full = ArcMotion.GetEffectiveLevel(this);
        animate &= full != ArcMotionLevel.Off && IsLoaded;
        Show(_fieldsScroll, _fadeFields, tab == ExtractionTab.Fields);
        Show(_jsonScroll, _fadeJson, tab == ExtractionTab.Json);
        Show(_schemaScroll, _fadeSchema, tab == ExtractionTab.Schema);
        if (_panelRing is not null)
        {
            _panelRing.IsVisible = false;
        }
        _hoverPath = null;
        ApplyLinked(LinkedPath, force: true);

        void Show(ScrollViewer? panel, MotionValue<double>? fade, bool shown)
        {
            if (panel is null || fade is null)
            {
                return;
            }
            if (!animate)
            {
                fade.Jump(shown ? 1 : 0);
                panel.IsVisible = shown;
                panel.IsHitTestVisible = shown;
                return;
            }
            var reduced = full == ArcMotionLevel.Reduced;
            if (shown)
            {
                panel.IsVisible = true;
                panel.IsHitTestVisible = true;
                fade.Jump(0);
                _ = fade.AnimateTo(1, MotionSpec.Tween(reduced ? ArcMotionPresets.Instant : PanelIn, reduced ? s_linear : ArcEasings.Enter));
            }
            else if (panel.IsVisible)
            {
                // Out of the hit test at once; hidden (out of layout and the Tab order) when the fade ends.
                panel.IsHitTestVisible = false;
                _ = HideAfter(panel, fade, reduced);
            }
        }
    }

    private static async Task HideAfter(ScrollViewer panel, MotionValue<double> fade, bool reduced)
    {
        var done = await fade.AnimateTo(0, MotionSpec.Tween(reduced ? ArcMotionPresets.Instant : PanelOut, reduced ? s_linear : ArcEasings.Standard));
        if (done && !panel.IsHitTestVisible)
        {
            panel.IsVisible = false;
        }
    }

    private static readonly Avalonia.Animation.Easings.Easing s_linear = new Avalonia.Animation.Easings.LinearEasing();

    /// <summary>The panels are Tab stops; a keyboard focus on one draws the ring 3px inside the pane.</summary>
    private void OnPanelGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (_panelRing is not null && ReferenceEquals(e.Source, sender))
        {
            _panelRing.IsVisible = e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional;
        }
    }

    private void OnPanelLostFocus(object? sender, RoutedEventArgs e)
    {
        if (_panelRing is not null && ReferenceEquals(e.Source, sender))
        {
            _panelRing.IsVisible = false;
        }
    }

    #endregion

    #region Keys

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.Key != Key.Escape || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        if (IsRunning)
        {
            // Escape stops a run (spec 6.2).
            e.Handled = true;
            Stop();
        }
        else if (SelectedPath is not null || _hoverPath is not null)
        {
            // Otherwise it clears the link; the focus stays where it is.
            e.Handled = true;
            _hoverPath = null;
            SetCurrentValue(SelectedPathProperty, null);
            ApplyLinked(LinkedPath, force: true);
        }
    }

    #endregion
}
