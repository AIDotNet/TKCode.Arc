using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The left pane of the document extraction (spec 3.3): a 48 high toolbar on Surface (the file button and its document
/// menu, the page count, the zoom group and Fit) over the canvas, a scrollable region a little darker than the surface
/// (5% Foreground over Surface in the light theme, Background in the dark one) in which the <see cref="DocumentPage"/> is
/// drawn at <see cref="Zoom"/>. The page is scaled, never laid out again: a sizer reports its scaled size to the canvas'
/// scroll extent while the page keeps its base layout under a render transform.
/// </summary>
/// <remarks>
/// <para>
/// The viewer holds no words of its own: every text and accessible name comes from <see cref="Labels"/>. The owner drives
/// the spans (<see cref="SetSpan"/>, <see cref="TryGetSpanBounds"/>, <see cref="ScrollToSpan"/>), the scan
/// (<see cref="StartScan"/>) and the layout (<see cref="IsNarrow"/>, <see cref="IsStacked"/>); it listens to the span events,
/// which are the page's own routed events bubbling through the viewer (raised once, not re-raised).
/// </para>
/// <para>
/// Keyboard: Tab reaches the file button, zoom out, the percentage, zoom in, Fit, then the canvas (a region named
/// <see cref="DocumentExtractionLabels.CanvasName"/>), where the arrows scroll and Ctrl / Cmd with = or +, − and 0 zoom in,
/// out and back to fit.
/// </para>
/// </remarks>
[TemplatePart(FileButtonPart, typeof(DocumentViewerButton))]
[TemplatePart(FileMarkPart, typeof(DocumentFileMark))]
[TemplatePart(FileNamePart, typeof(TextBlock))]
[TemplatePart(PageCountPart, typeof(TextBlock))]
[TemplatePart(ZoomGroupPart, typeof(Control))]
[TemplatePart(ZoomOutPart, typeof(DocumentViewerButton))]
[TemplatePart(ZoomResetPart, typeof(DocumentViewerButton))]
[TemplatePart(PercentPart, typeof(TextBlock))]
[TemplatePart(ZoomInPart, typeof(DocumentViewerButton))]
[TemplatePart(FitPart, typeof(DocumentViewerButton))]
[TemplatePart(CanvasPart, typeof(ScrollArea))]
[TemplatePart(SizerPart, typeof(DocumentViewerSizer))]
[TemplatePart(StagePart, typeof(Panel))]
[TemplatePart(PagePart, typeof(DocumentPage))]
[PseudoClasses(EmailPseudoClass, NarrowPseudoClass, StackedPseudoClass, MenuPseudoClass)]
internal sealed partial class DocumentViewer : TemplatedControl
{
    public const string FileButtonPart = "PART_FileButton";
    public const string FileMarkPart = "PART_FileMark";
    public const string FileNamePart = "PART_FileName";
    public const string PageCountPart = "PART_PageCount";
    public const string ZoomGroupPart = "PART_ZoomGroup";
    public const string ZoomOutPart = "PART_ZoomOut";
    public const string ZoomResetPart = "PART_ZoomReset";
    public const string PercentPart = "PART_Percent";
    public const string ZoomInPart = "PART_ZoomIn";
    public const string FitPart = "PART_Fit";
    public const string CanvasPart = "PART_Canvas";
    public const string SizerPart = "PART_Sizer";
    public const string StagePart = "PART_Stage";
    public const string PagePart = "PART_Page";

    /// <summary>The document is an email: no page count.</summary>
    public const string EmailPseudoClass = ":email";

    /// <summary>Narrow container: no page count.</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Stacked panes: the canvas padding is 14.</summary>
    public const string StackedPseudoClass = ":stacked";

    /// <summary>Two or more documents: the file button has a chevron and opens the menu.</summary>
    public const string MenuPseudoClass = ":menu";

    /// <summary>The space around the page in the canvas (the theme puts it on the sizer).</summary>
    public const double CanvasPadding = 22;

    /// <summary>The canvas padding when the panes are stacked.</summary>
    public const double StackedCanvasPadding = 14;

    /// <summary>Fit leaves this much of the canvas width free (Arc: a 641 wide canvas fits at 110%).</summary>
    public const double FitSlack = 3;

    /// <summary>How far from the nearest edge <see cref="ScrollToSpan"/> leaves a span.</summary>
    public const double ScrollMargin = 56;

    /// <summary>The share of Foreground mixed into Surface for the light canvas and laid over the menu's highlighted row.</summary>
    public const double ForegroundTint = 0.05;

    /// <summary>The corner radius of a document menu row and its highlight (spec 3.7).</summary>
    public const double MenuRowRadius = 11;

    private static readonly double[] s_defaultSteps = [0.4, 0.5, 0.67, 0.75, 0.9, 1, 1.1, 1.25, 1.5, 1.75, 2];

    #region Properties

    /// <summary>The document on the page.</summary>
    public static readonly StyledProperty<ExtractionDocument?> DocumentProperty =
        AvaloniaProperty.Register<DocumentViewer, ExtractionDocument?>(nameof(Document));

    /// <summary>The documents of the file menu; the file button opens it only with two or more.</summary>
    public static readonly StyledProperty<IReadOnlyList<ExtractionDocument>?> DocumentsProperty =
        AvaloniaProperty.Register<DocumentViewer, IReadOnlyList<ExtractionDocument>?>(nameof(Documents));

    /// <summary>The document chosen on the menu (two-way); the menu's check is on it, or on <see cref="Document"/> when null.</summary>
    public static readonly StyledProperty<ExtractionDocument?> SelectedDocumentProperty =
        AvaloniaProperty.Register<DocumentViewer, ExtractionDocument?>(nameof(SelectedDocument), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Every word the viewer shows or announces.</summary>
    public static readonly StyledProperty<DocumentExtractionLabels> LabelsProperty =
        AvaloniaProperty.Register<DocumentViewer, DocumentExtractionLabels>(nameof(Labels), new DocumentExtractionLabels());

    /// <summary>The zoom of the page (two-way), kept within the steps' range; while <see cref="IsFitWidth"/> it follows the canvas width.</summary>
    public static readonly StyledProperty<double> ZoomProperty =
        AvaloniaProperty.Register<DocumentViewer, double>(nameof(Zoom), 1, defaultBindingMode: BindingMode.TwoWay, coerce: CoerceZoom);

    /// <summary>Whether the page is fitted to the canvas width (two-way); zooming by hand turns it off.</summary>
    public static readonly StyledProperty<bool> IsFitWidthProperty =
        AvaloniaProperty.Register<DocumentViewer, bool>(nameof(IsFitWidth), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The zoom steps of the buttons and the keys.</summary>
    public static readonly StyledProperty<IReadOnlyList<double>> ZoomStepsProperty =
        AvaloniaProperty.Register<DocumentViewer, IReadOnlyList<double>>(nameof(ZoomSteps), s_defaultSteps);

    /// <summary>A narrow container: the page count is hidden.</summary>
    public static readonly StyledProperty<bool> IsNarrowProperty =
        AvaloniaProperty.Register<DocumentViewer, bool>(nameof(IsNarrow));

    /// <summary>The panes are stacked: the canvas padding is 14 instead of 22.</summary>
    public static readonly StyledProperty<bool> IsStackedProperty =
        AvaloniaProperty.Register<DocumentViewer, bool>(nameof(IsStacked));

    /// <summary>The painted span under the pointer, if any (read-only).</summary>
    public static readonly DirectProperty<DocumentViewer, string?> HoveredSpanIdProperty =
        AvaloniaProperty.RegisterDirect<DocumentViewer, string?>(nameof(HoveredSpanId), o => o.HoveredSpanId);

    /// <summary>The canvas color of the theme (set by the viewer).</summary>
    internal static readonly StyledProperty<IBrush?> CanvasBrushProperty =
        AvaloniaProperty.Register<DocumentViewer, IBrush?>(nameof(CanvasBrush));

    /// <summary>The page's <see cref="DocumentPage.SpanPointerEnteredEvent"/>, bubbling through the viewer.</summary>
    public static readonly RoutedEvent<DocumentSpanEventArgs> SpanPointerEnteredEvent = DocumentPage.SpanPointerEnteredEvent;

    /// <summary>The page's <see cref="DocumentPage.SpanPointerExitedEvent"/>, bubbling through the viewer.</summary>
    public static readonly RoutedEvent<DocumentSpanEventArgs> SpanPointerExitedEvent = DocumentPage.SpanPointerExitedEvent;

    /// <summary>The page's <see cref="DocumentPage.SpanPressedEvent"/>, bubbling through the viewer.</summary>
    public static readonly RoutedEvent<DocumentSpanEventArgs> SpanPressedEvent = DocumentPage.SpanPressedEvent;

    /// <inheritdoc cref="DocumentProperty"/>
    public ExtractionDocument? Document { get => GetValue(DocumentProperty); set => SetValue(DocumentProperty, value); }

    /// <inheritdoc cref="DocumentsProperty"/>
    public IReadOnlyList<ExtractionDocument>? Documents { get => GetValue(DocumentsProperty); set => SetValue(DocumentsProperty, value); }

    /// <inheritdoc cref="SelectedDocumentProperty"/>
    public ExtractionDocument? SelectedDocument { get => GetValue(SelectedDocumentProperty); set => SetValue(SelectedDocumentProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public DocumentExtractionLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="ZoomProperty"/>
    public double Zoom { get => GetValue(ZoomProperty); set => SetValue(ZoomProperty, value); }

    /// <inheritdoc cref="IsFitWidthProperty"/>
    public bool IsFitWidth { get => GetValue(IsFitWidthProperty); set => SetValue(IsFitWidthProperty, value); }

    /// <inheritdoc cref="ZoomStepsProperty"/>
    public IReadOnlyList<double> ZoomSteps { get => GetValue(ZoomStepsProperty); set => SetValue(ZoomStepsProperty, value); }

    /// <inheritdoc cref="IsNarrowProperty"/>
    public bool IsNarrow { get => GetValue(IsNarrowProperty); set => SetValue(IsNarrowProperty, value); }

    /// <inheritdoc cref="IsStackedProperty"/>
    public bool IsStacked { get => GetValue(IsStackedProperty); set => SetValue(IsStackedProperty, value); }

    /// <inheritdoc cref="HoveredSpanIdProperty"/>
    public string? HoveredSpanId { get => _hoveredSpanId; private set => SetAndRaise(HoveredSpanIdProperty, ref _hoveredSpanId, value); }

    /// <inheritdoc cref="CanvasBrushProperty"/>
    internal IBrush? CanvasBrush { get => GetValue(CanvasBrushProperty); set => SetValue(CanvasBrushProperty, value); }

    /// <inheritdoc cref="SpanPointerEnteredEvent"/>
    public event EventHandler<DocumentSpanEventArgs>? SpanPointerEntered { add => AddHandler(SpanPointerEnteredEvent, value); remove => RemoveHandler(SpanPointerEnteredEvent, value); }

    /// <inheritdoc cref="SpanPointerExitedEvent"/>
    public event EventHandler<DocumentSpanEventArgs>? SpanPointerExited { add => AddHandler(SpanPointerExitedEvent, value); remove => RemoveHandler(SpanPointerExitedEvent, value); }

    /// <inheritdoc cref="SpanPressedEvent"/>
    public event EventHandler<DocumentSpanEventArgs>? SpanPressed { add => AddHandler(SpanPressedEvent, value); remove => RemoveHandler(SpanPressedEvent, value); }

    /// <summary>Raised when the canvas scrolled, the zoom drawn changed or the canvas changed size.</summary>
    public event EventHandler? ViewChanged;

    #endregion

    private DocumentViewerButton? _fileButton;
    private DocumentFileMark? _fileMark;
    private TextBlock? _fileName;
    private TextBlock? _pageCount;
    private Control? _zoomGroup;
    private DocumentViewerButton? _zoomOut;
    private DocumentViewerButton? _zoomReset;
    private DocumentViewerButton? _zoomIn;
    private TextBlock? _percent;
    private DocumentViewerButton? _fit;
    private ScrollArea? _canvas;
    private DocumentViewerSizer? _sizer;
    private Panel? _stage;
    private DocumentPage? _page;
    private MenuSurface? _menu;
    private GlidingIndicator? _menuHighlight;
    private IReadOnlyList<ExtractionDocument>? _menuDocuments;
    private bool _menuKeyOpen;
    private string? _hoveredSpanId;
    private Color _canvasColor;
    private IBrush? _highlightBrush;
    private bool _writingOffset;

    /// <summary>Creates a viewer.</summary>
    public DocumentViewer()
    {
        ActualThemeVariantChanged += (_, _) => RefreshTheme();
        ResourcesChanged += (_, _) => RefreshTheme();
        AddHandler(KeyDownEvent, OnTunnelKeyDown, RoutingStrategies.Tunnel);
    }

    protected override Type StyleKeyOverride => typeof(DocumentViewer);

    /// <summary>The canvas' viewport (the scroll area); the owner measures the visibility of span links against it.</summary>
    public Visual CanvasViewport => (Visual?)_canvas ?? this;

    /// <summary>The page (tests and the owner's checks).</summary>
    internal DocumentPage? Page => _page;

    /// <summary>The canvas (tests).</summary>
    internal ScrollArea? Canvas => _canvas;

    /// <summary>The sizer the page is scaled in (tests).</summary>
    internal DocumentViewerSizer? Sizer => _sizer;

    /// <summary>The document menu (null until two documents are given).</summary>
    internal MenuSurface? Menu => _menu;

    /// <summary>The file button (tests).</summary>
    internal DocumentViewerButton? FileButton => _fileButton;

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        DetachParts();
        var scope = e.NameScope;
        _fileButton = scope.Find<DocumentViewerButton>(FileButtonPart);
        _fileMark = scope.Find<DocumentFileMark>(FileMarkPart);
        _fileName = scope.Find<TextBlock>(FileNamePart);
        _pageCount = scope.Find<TextBlock>(PageCountPart);
        _zoomGroup = scope.Find<Control>(ZoomGroupPart);
        _zoomOut = scope.Find<DocumentViewerButton>(ZoomOutPart);
        _zoomReset = scope.Find<DocumentViewerButton>(ZoomResetPart);
        _zoomIn = scope.Find<DocumentViewerButton>(ZoomInPart);
        _percent = scope.Find<TextBlock>(PercentPart);
        _fit = scope.Find<DocumentViewerButton>(FitPart);
        _canvas = scope.Find<ScrollArea>(CanvasPart);
        _sizer = scope.Find<DocumentViewerSizer>(SizerPart);
        _stage = scope.Find<Panel>(StagePart);
        _page = scope.Find<DocumentPage>(PagePart);
        AttachParts();
        if (_sizer is not null)
        {
            // The first zoom is drawn as it is: there is nothing to spring from.
            _sizer.Scale = _scale?.Target ?? Zoom;
        }
        UpdateDocumentFace();
        UpdateMenu();
        UpdateZoomFace();
    }

    private void AttachParts()
    {
        if (_fileButton is not null)
        {
            _fileButton.AddHandler(KeyDownEvent, OnFileButtonKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
            _fileButton.AddHandler(PointerPressedEvent, OnFileButtonPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        }
        if (_zoomOut is not null)
        {
            _zoomOut.Click += OnZoomOutClick;
        }
        if (_zoomIn is not null)
        {
            _zoomIn.Click += OnZoomInClick;
        }
        if (_zoomReset is not null)
        {
            _zoomReset.Click += OnZoomResetClick;
        }
        if (_fit is not null)
        {
            _fit.Click += OnFitClick;
        }
        if (_canvas is not null)
        {
            _canvas.PropertyChanged += OnCanvasPropertyChanged;
            _canvas.AddHandler(PointerWheelChangedEvent, OnCanvasWheel, RoutingStrategies.Tunnel, handledEventsToo: true);
        }
        if (_page is not null)
        {
            _page.PropertyChanged += OnPagePropertyChanged;
        }
    }

    private void DetachParts()
    {
        StopScan();
        StopScrollMotion();
        if (_fileButton is not null)
        {
            _fileButton.RemoveHandler(KeyDownEvent, OnFileButtonKeyDown);
            _fileButton.RemoveHandler(PointerPressedEvent, OnFileButtonPointerPressed);
            DropdownMenu.SetMenu(_fileButton, null);
        }
        if (_zoomOut is not null)
        {
            _zoomOut.Click -= OnZoomOutClick;
        }
        if (_zoomIn is not null)
        {
            _zoomIn.Click -= OnZoomInClick;
        }
        if (_zoomReset is not null)
        {
            _zoomReset.Click -= OnZoomResetClick;
        }
        if (_fit is not null)
        {
            _fit.Click -= OnFitClick;
        }
        if (_canvas is not null)
        {
            _canvas.PropertyChanged -= OnCanvasPropertyChanged;
            _canvas.RemoveHandler(PointerWheelChangedEvent, OnCanvasWheel);
            _canvas.LayoutUpdated -= OnLayoutUpdatedOnce;
            _offsetAfterLayout = false;
        }
        if (_page is not null)
        {
            _page.PropertyChanged -= OnPagePropertyChanged;
        }
        HoveredSpanId = null;
    }

    #endregion

    #region Property changes

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DocumentProperty)
        {
            StopScan();
            StopScrollMotion();
            _anchor = null;
            // A new document starts at its top.
            SetCanvasOffset(default);
            UpdateDocumentFace();
            UpdateMenuChecks();
        }
        else if (change.Property == DocumentsProperty)
        {
            UpdateMenu();
            UpdateDocumentFace();
        }
        else if (change.Property == SelectedDocumentProperty)
        {
            UpdateMenuChecks();
        }
        else if (change.Property == LabelsProperty)
        {
            UpdateDocumentFace();
            UpdateZoomFace();
            if (_menu is not null)
            {
                AutomationProperties.SetName(_menu, MenuName());
            }
        }
        else if (change.Property == ZoomProperty)
        {
            OnZoomChanged(change.GetNewValue<double>());
        }
        else if (change.Property == IsFitWidthProperty)
        {
            OnFitWidthChanged(change.GetNewValue<bool>());
        }
        else if (change.Property == ZoomStepsProperty)
        {
            CoerceValue(ZoomProperty);
            UpdateZoomFace();
            RefitAtOnce();
        }
        else if (change.Property == IsNarrowProperty)
        {
            PseudoClasses.Set(NarrowPseudoClass, change.GetNewValue<bool>());
        }
        else if (change.Property == IsStackedProperty)
        {
            PseudoClasses.Set(StackedPseudoClass, change.GetNewValue<bool>());
            // The padding changed, so the width that fits changed with it.
            RefitAtOnce();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        RefreshTheme();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        StopScan();
        StopScrollMotion();
        _menu?.Close(returnFocus: false);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Toolbar

    /// <summary>The file name, the mark, the page count and the accessible names that follow the document and the labels.</summary>
    private void UpdateDocumentFace()
    {
        var labels = Labels;
        var document = Document;
        var name = document?.Name ?? string.Empty;
        var email = document is not null && document.EffectiveKind == DocumentKind.Email;
        PseudoClasses.Set(EmailPseudoClass, email);
        AutomationProperties.SetName(this, labels.Document);
        if (_fileMark is not null)
        {
            _fileMark.Kind = email ? DocumentKind.Email : DocumentKind.Pdf;
        }
        if (_fileName is not null)
        {
            _fileName.Text = name;
        }
        if (_fileButton is not null)
        {
            AutomationProperties.SetName(_fileButton, HasMenu ? labels.SwitchDocument(name) : name);
        }
        if (_pageCount is not null)
        {
            _pageCount.Text = labels.PageOf(1, 1);
            AutomationProperties.SetName(_pageCount, labels.PageOfName(1, 1));
        }
        if (_page is not null)
        {
            _page.ArticleName = name;
        }
        UpdateCanvasName();
    }

    /// <summary>The percentage, the ends of the steps, Fit and the names that follow the zoom.</summary>
    private void UpdateZoomFace()
    {
        var labels = Labels;
        var zoom = Zoom;
        var percent = PercentOf(zoom);
        if (_percent is not null)
        {
            _percent.Text = string.Create(CultureInfo.CurrentCulture, $"{percent}%");
        }
        if (_zoomReset is not null)
        {
            AutomationProperties.SetName(_zoomReset, labels.ZoomReset(percent));
        }
        if (_zoomOut is not null)
        {
            _zoomOut.IsEnabled = StepFrom(zoom, -1) is not null;
            AutomationProperties.SetName(_zoomOut, labels.ZoomOut);
            Tooltip.SetTip(_zoomOut, labels.ZoomOut);
        }
        if (_zoomIn is not null)
        {
            _zoomIn.IsEnabled = StepFrom(zoom, 1) is not null;
            AutomationProperties.SetName(_zoomIn, labels.ZoomIn);
            Tooltip.SetTip(_zoomIn, labels.ZoomIn);
        }
        if (_zoomGroup is not null)
        {
            AutomationProperties.SetName(_zoomGroup, labels.Zoom);
        }
        if (_fit is not null)
        {
            _fit.Content = labels.Fit;
            _fit.IsOn = IsFitWidth;
            AutomationProperties.SetName(_fit, labels.FitTip);
            Tooltip.SetTip(_fit, labels.FitTip);
        }
        UpdateCanvasName();
    }

    private void UpdateCanvasName()
    {
        if (_canvas is not null)
        {
            _canvas.Label = Labels.CanvasName(Document?.Name ?? string.Empty, PercentOf(Zoom));
        }
    }

    /// <summary>A zoom as the whole percentage the toolbar shows.</summary>
    internal static int PercentOf(double zoom) => (int)Math.Round(zoom * 100, MidpointRounding.AwayFromZero);

    private void OnZoomOutClick(object? sender, RoutedEventArgs e) => StepZoom(-1);

    private void OnZoomInClick(object? sender, RoutedEventArgs e) => StepZoom(1);

    private void OnZoomResetClick(object? sender, RoutedEventArgs e) => ResetZoom();

    private void OnFitClick(object? sender, RoutedEventArgs e) => SetCurrentValue(IsFitWidthProperty, !IsFitWidth);

    #endregion

    #region Document menu

    /// <summary>Whether the file button opens the document menu (two or more documents).</summary>
    internal bool HasMenu => Documents is { Count: >= 2 };

    private string MenuName() => Labels.SwitchDocument(Document?.Name ?? string.Empty);

    private void UpdateMenu()
    {
        var documents = Documents;
        var hasMenu = HasMenu;
        PseudoClasses.Set(MenuPseudoClass, hasMenu);
        if (_fileButton is not null)
        {
            _fileButton.HasMenu = hasMenu;
            // One document: the button is only a label (nothing to switch to).
            _fileButton.Focusable = hasMenu;
            _fileButton.IsHitTestVisible = hasMenu;
            DropdownMenu.SetMenu(_fileButton, hasMenu ? EnsureMenu() : null);
        }
        if (!hasMenu)
        {
            if (_menu is not null)
            {
                _menu.Close(returnFocus: false);
                _menu.Items.Clear();
            }
            _menuDocuments = null;
            return;
        }
        if (!ReferenceEquals(documents, _menuDocuments) || _menu!.Items.Count != documents!.Count)
        {
            _menuDocuments = documents;
            var menu = EnsureMenu();
            menu.Items.Clear();
            foreach (var document in documents!)
            {
                var entry = new MenuEntry
                {
                    ToggleType = MenuEntryToggleType.Radio,
                    GroupName = "documents",
                    Icon = new DocumentFileMark { Kind = document.EffectiveKind == DocumentKind.Email ? DocumentKind.Email : DocumentKind.Pdf },
                    Header = new DocumentMenuRowText(document),
                    Padding = new Thickness(10, 6),
                    CornerRadius = new CornerRadius(MenuRowRadius),
                    Tag = document
                };
                AutomationProperties.SetName(entry, document.Name);
                TextSearch.SetText(entry, document.Name);
                menu.Items.Add(entry);
            }
        }
        AutomationProperties.SetName(_menu!, MenuName());
        UpdateMenuChecks();
    }

    private MenuSurface EnsureMenu()
    {
        if (_menu is not null)
        {
            return _menu;
        }
        // Spec 3.7: under the button by 6, left aligned, 5 inside, radius 16 (rows 48 high, radius 11).
        _menu = new MenuSurface
        {
            Variant = MenuSurfaceVariant.Dropdown,
            Side = MenuSide.Bottom,
            Alignment = MenuAlignment.Start,
            AnchorGap = 6,
            Padding = new Thickness(5),
        };
        _menu.BindCornerRadius(ArcDesignTokenKind.RadiusStep16);
        _menu.ItemClicked += OnMenuItemClicked;
        _menu.Opened += OnMenuOpened;
        _menu.Closed += OnMenuClosed;
        _menu.TemplateApplied += OnMenuTemplateApplied;
        return _menu;
    }

    private void UpdateMenuChecks()
    {
        if (_menu is null)
        {
            return;
        }
        var current = SelectedDocument ?? Document;
        foreach (var item in _menu.Items)
        {
            if (item is MenuEntry { Tag: ExtractionDocument document } entry)
            {
                entry.IsChecked = ReferenceEquals(document, current);
            }
        }
    }

    private void OnMenuItemClicked(object? sender, MenuEntryClickedEventArgs e)
    {
        if (e.Entry.Tag is ExtractionDocument document)
        {
            SetCurrentValue(SelectedDocumentProperty, document);
            UpdateMenuChecks();
        }
    }

    private void OnFileButtonKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space or Key.Down or Key.Up)
        {
            _menuKeyOpen = true;
        }
    }

    private void OnFileButtonPointerPressed(object? sender, PointerPressedEventArgs e) => _menuKeyOpen = false;

    private void OnMenuOpened(object? sender, RoutedEventArgs e)
    {
        if (_fileButton is not null)
        {
            _fileButton.IsOn = true;
        }
        if (_menuKeyOpen)
        {
            // Opened from the keyboard: the highlight starts on the chosen document (spec 6.2), after the surface put it on
            // the first row (both run at Loaded priority, the surface's first).
            Dispatcher.UIThread.Post(HighlightChecked, DispatcherPriority.Loaded);
        }
    }

    private void OnMenuClosed(object? sender, RoutedEventArgs e)
    {
        _menuKeyOpen = false;
        if (_fileButton is not null)
        {
            _fileButton.IsOn = false;
        }
    }

    private void HighlightChecked()
    {
        if (_menu is not { IsOpen: true })
        {
            return;
        }
        foreach (var item in _menu.Items)
        {
            if (item is MenuEntry { IsChecked: true } entry)
            {
                entry.Focus(NavigationMethod.Directional);
                return;
            }
        }
    }

    private void OnMenuTemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        _menuHighlight = e.NameScope.Find<GlidingIndicator>(MenuSurface.HighlightPart);
        ApplyMenuHighlight();
    }

    /// <summary>
    /// The highlight of the document menu: Foreground at 5% with the rows' radius, visible for the keyboard as for the
    /// pointer (spec 11: Arc's keyboard highlight has no visible fill; ours does).
    /// </summary>
    private void ApplyMenuHighlight()
    {
        if (_menuHighlight is null)
        {
            return;
        }
        if (_highlightBrush is not null)
        {
            _menuHighlight.Fill = _highlightBrush;
        }
        _menuHighlight.CornerRadius = new CornerRadius(MenuRowRadius);
    }

    #endregion

    #region Theme

    /// <summary>
    /// The canvas color and the menu highlight follow the theme: the canvas is Foreground at 5% mixed into Surface where the
    /// background is light and the Background itself where it is dark (spec 3.3: both a little deeper than the surface).
    /// </summary>
    private void RefreshTheme()
    {
        if (TopLevel.GetTopLevel(this) is null)
        {
            return;
        }
        var background = ArcTokens.GetColor(this, ArcDesignTokenKind.Background);
        var foreground = ArcTokens.GetColor(this, ArcDesignTokenKind.Foreground);
        var dark = Oklch.FromColor(background).L < 0.5;
        var canvas = dark ? background : Oklch.MixOklab(ArcTokens.GetColor(this, ArcDesignTokenKind.Surface), foreground, ForegroundTint);
        if (CanvasBrush is null || canvas != _canvasColor)
        {
            _canvasColor = canvas;
            CanvasBrush = new ImmutableSolidColorBrush(canvas);
        }
        var highlight = Oklch.WithAlpha(foreground, ForegroundTint);
        if (_highlightBrush is not ISolidColorBrush current || current.Color != highlight)
        {
            _highlightBrush = new ImmutableSolidColorBrush(highlight);
            ApplyMenuHighlight();
        }
    }

    #endregion

    #region Canvas and page

    private void OnCanvasPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ScrollViewer.OffsetProperty)
        {
            if (!_writingOffset && !_applyingScale)
            {
                // Someone else scrolled (wheel, keys, the scrollbar): an eased scroll to a span gives way.
                StopScrollMotion();
            }
            ViewChanged?.Invoke(this, EventArgs.Empty);
        }
        else if (e.Property == ScrollViewer.ExtentProperty || e.Property == ScrollViewer.ViewportProperty)
        {
            ViewChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnCanvasWheel(object? sender, PointerWheelEventArgs e) => StopScrollMotion();

    private void OnPagePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == DocumentPage.HoveredSpanIdProperty)
        {
            HoveredSpanId = e.GetNewValue<string?>();
        }
    }

    private void OnTunnelKeyDown(object? sender, KeyEventArgs e)
    {
        if (_canvas is null || e.Source is not Visual source || !(ReferenceEquals(source, _canvas) || _canvas.IsVisualAncestorOf(source)))
        {
            return;
        }
        if ((e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0 && ZoomKeyOf(e) is { } key)
        {
            e.Handled = true;
            if (key == 0)
            {
                SetCurrentValue(IsFitWidthProperty, true);
            }
            else
            {
                StepZoom(key);
            }
            return;
        }
        if (e.Key is Key.Up or Key.Down or Key.Left or Key.Right or Key.PageUp or Key.PageDown or Key.Home or Key.End or Key.Space)
        {
            StopScrollMotion();
        }
    }

    /// <summary>+1 for = or +, −1 for −, 0 for 0 (fit), else null.</summary>
    private static int? ZoomKeyOf(KeyEventArgs e) => e.Key switch
    {
        Key.OemPlus or Key.Add => 1,
        Key.OemMinus or Key.Subtract => -1,
        Key.D0 or Key.NumPad0 => 0,
        _ => e.KeySymbol switch
        {
            "=" or "+" => 1,
            "-" => -1,
            "0" => 0,
            _ => null
        }
    };

    private void SetCanvasOffset(Vector offset)
    {
        if (_canvas is null)
        {
            return;
        }
        _writingOffset = true;
        try
        {
            _canvas.Offset = offset;
        }
        finally
        {
            _writingOffset = false;
        }
    }

    /// <summary>The farthest the canvas scrolls.</summary>
    private Vector MaxOffset()
    {
        if (_canvas is null)
        {
            return default;
        }
        var extent = _canvas.Extent;
        var viewport = _canvas.Viewport;
        return new Vector(Math.Max(0, extent.Width - viewport.Width), Math.Max(0, extent.Height - viewport.Height));
    }

    #endregion
}
