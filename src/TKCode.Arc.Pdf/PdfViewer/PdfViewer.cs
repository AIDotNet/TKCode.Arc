using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A PDF viewer: <see cref="PageViewer"/> plus a document (<see cref="Source"/> or <see cref="Document"/>), a sidebar with
/// thumbnails and the table of contents, search, text selection, links, and the states of opening one (loading, failed,
/// password). Rendering comes from
/// <see cref="ArcPdf.Backend"/>: call <c>ArcPdfNative.Use()</c> on desktop or <c>await ArcPdfBrowser.UseAsync()</c> in a browser.
/// Without a backend a real document fails with a hint; pages are never faked. <see cref="SimulatedPdfDocument"/> gives
/// generated pages for tests and design time.
/// </summary>
[PseudoClasses(SidebarOpenPseudoClass, SearchOpenPseudoClass, NarrowPseudoClass, FailedPseudoClass, PasswordPseudoClass)]
public partial class PdfViewer : PageViewer
{
    /// <summary>Set while <see cref="IsSidebarOpen"/> is true.</summary>
    public const string SidebarOpenPseudoClass = ":sidebar-open";

    /// <summary>Set while the viewer is narrower than 640; the sidebar then floats over the pages.</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Set while <see cref="Status"/> is <see cref="PdfViewerStatus.Failed"/>.</summary>
    public const string FailedPseudoClass = ":failed";

    /// <summary>Set while <see cref="Status"/> is <see cref="PdfViewerStatus.PasswordRequired"/>.</summary>
    public const string PasswordPseudoClass = ":password";

    /// <summary>Below this width the sidebar floats over the pages.</summary>
    internal const double NarrowWidth = 640;

    #region Properties

    /// <summary>The PDF to open; changing it stops the open in progress and closes the document the viewer opened. In XAML, a path or an absolute URI. Wins over <see cref="Document"/>.</summary>
    public static readonly StyledProperty<PdfSource?> SourceProperty =
        AvaloniaProperty.Register<PdfViewer, PdfSource?>(nameof(Source));

    /// <summary>An already open document to show. It stays yours: the viewer never disposes it.</summary>
    public static readonly StyledProperty<PdfDocument?> DocumentProperty =
        AvaloniaProperty.Register<PdfViewer, PdfDocument?>(nameof(Document));

    /// <summary>The document on show. One opened from <see cref="Source"/> belongs to the viewer, which closes it when the source changes or the viewer leaves the tree.</summary>
    public static readonly DirectProperty<PdfViewer, PdfDocument?> OpenedDocumentProperty =
        AvaloniaProperty.RegisterDirect<PdfViewer, PdfDocument?>(nameof(OpenedDocument), o => o.OpenedDocument);

    /// <summary>Where the viewer is with its document.</summary>
    public static readonly DirectProperty<PdfViewer, PdfViewerStatus> StatusProperty =
        AvaloniaProperty.RegisterDirect<PdfViewer, PdfViewerStatus>(nameof(Status), o => o.Status);

    /// <summary>Why the document could not be opened, while <see cref="Status"/> is Failed.</summary>
    public static readonly DirectProperty<PdfViewer, PdfException?> ErrorProperty =
        AvaloniaProperty.RegisterDirect<PdfViewer, PdfException?>(nameof(Error), o => o.Error);

    /// <summary>The document's title, or else <see cref="PdfSource.DisplayName"/>.</summary>
    public static readonly DirectProperty<PdfViewer, string?> TitleProperty =
        AvaloniaProperty.RegisterDirect<PdfViewer, string?>(nameof(Title), o => o.Title);

    /// <summary>Shows the sidebar (two-way). Below a width of 640 it floats over the pages.</summary>
    public static readonly StyledProperty<bool> IsSidebarOpenProperty =
        AvaloniaProperty.Register<PdfViewer, bool>(nameof(IsSidebarOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>What the sidebar shows (two-way). A document without a table of contents shows thumbnails and no tabs, whatever this says.</summary>
    public static readonly StyledProperty<PdfSidebarMode> SidebarModeProperty =
        AvaloniaProperty.Register<PdfViewer, PdfSidebarMode>(nameof(SidebarMode), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The document needs a password. No answer shows the built-in form.</summary>
    public static readonly RoutedEvent<PdfPasswordEventArgs> PasswordRequestedEvent =
        RoutedEvent.Register<PdfViewer, PdfPasswordEventArgs>(nameof(PasswordRequested), RoutingStrategies.Bubble);

    /// <summary>A document went on show.</summary>
    public static readonly RoutedEvent<PdfDocumentOpenedEventArgs> DocumentOpenedEvent =
        RoutedEvent.Register<PdfViewer, PdfDocumentOpenedEventArgs>(nameof(DocumentOpened), RoutingStrategies.Bubble);

    /// <summary>A document could not be opened.</summary>
    public static readonly RoutedEvent<PdfDocumentFailedEventArgs> DocumentFailedEvent =
        RoutedEvent.Register<PdfViewer, PdfDocumentFailedEventArgs>(nameof(DocumentFailed), RoutingStrategies.Bubble);

    /// <inheritdoc cref="SourceProperty"/>
    public PdfSource? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

    /// <inheritdoc cref="DocumentProperty"/>
    public PdfDocument? Document { get => GetValue(DocumentProperty); set => SetValue(DocumentProperty, value); }

    /// <inheritdoc cref="OpenedDocumentProperty"/>
    public PdfDocument? OpenedDocument
    {
        get => _openedDocument;
        private set => SetAndRaise(OpenedDocumentProperty, ref _openedDocument, value);
    }

    /// <inheritdoc cref="StatusProperty"/>
    public PdfViewerStatus Status
    {
        get => _status;
        private set => SetAndRaise(StatusProperty, ref _status, value);
    }

    /// <inheritdoc cref="ErrorProperty"/>
    public PdfException? Error
    {
        get => _error;
        private set => SetAndRaise(ErrorProperty, ref _error, value);
    }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title
    {
        get => _title;
        private set => SetAndRaise(TitleProperty, ref _title, value);
    }

    /// <inheritdoc cref="IsSidebarOpenProperty"/>
    public bool IsSidebarOpen { get => GetValue(IsSidebarOpenProperty); set => SetValue(IsSidebarOpenProperty, value); }

    /// <inheritdoc cref="SidebarModeProperty"/>
    public PdfSidebarMode SidebarMode { get => GetValue(SidebarModeProperty); set => SetValue(SidebarModeProperty, value); }

    /// <inheritdoc cref="PasswordRequestedEvent"/>
    public event EventHandler<PdfPasswordEventArgs>? PasswordRequested
    {
        add => AddHandler(PasswordRequestedEvent, value);
        remove => RemoveHandler(PasswordRequestedEvent, value);
    }

    /// <inheritdoc cref="DocumentOpenedEvent"/>
    public event EventHandler<PdfDocumentOpenedEventArgs>? DocumentOpened
    {
        add => AddHandler(DocumentOpenedEvent, value);
        remove => RemoveHandler(DocumentOpenedEvent, value);
    }

    /// <inheritdoc cref="DocumentFailedEvent"/>
    public event EventHandler<PdfDocumentFailedEventArgs>? DocumentFailed
    {
        add => AddHandler(DocumentFailedEvent, value);
        remove => RemoveHandler(DocumentFailedEvent, value);
    }

    #endregion

    private readonly PdfToolbarToggle _sidebarToggle;
    private readonly PresenceHost _dockedHost;
    private PresenceHost? _floatingHost;
    private PdfSidebar? _sidebar;
    private Panel? _overlay;
    private PdfStateLayer? _stateLayer;
    private PdfDocument? _openedDocument;
    private PdfViewerStatus _status;
    private PdfException? _error;
    private string? _title;
    private IReadOnlyList<PdfOutlineItem> _outline = [];
    private bool _sidebarFloats;
    private string? _currentPageText;
    private IBrush? _searchHighlightBrush;
    private IBrush? _searchHighlightCurrentBrush;
    private IBrush? _linkHoverBrush;
    private PdfDocument? _labelSource;

    /// <summary>Creates an empty viewer.</summary>
    public PdfViewer()
    {
        _sidebarToggle = new PdfToolbarToggle { Content = new ArcGlyph { Data = ArcIcons.PanelLeft }, IsEnabled = false, IsVisible = false };
        _sidebarToggle.Click += OnSidebarToggleClick;
        SetCurrentValue(ToolbarStartProperty, _sidebarToggle);
        // Empty until the sidebar is first opened, so a viewer that never opens it does not pay for it. The width follows a
        // spring and the fade plays over it; SpringSizeDecorator cannot collapse a width, PresenceHost can.
        _dockedHost = new PresenceHost
        {
            IsPresent = false,
            Mode = PresenceMode.Fade,
            Collapse = PresenceCollapse.Width,
            Spring = Motion.ArcSpring.Smooth,
            ClipToBounds = true
        };
        LeadingPane = _dockedHost;

        _searchToggle = new PdfToolbarToggle { Content = new ArcGlyph { Data = ArcIcons.Search }, IsEnabled = false, IsVisible = false, Classes = { "search" } };
        _searchToggle.Click += OnSearchToggleClick;
        SetCurrentValue(ToolbarEndProperty, _searchToggle);

        // Draw order, and pointer events go the other way: links see a press first, selection decides what it becomes.
        _searchLayer = new PdfSearchLayer(this);
        _selectionLayer = new PdfSelectionLayer(this);
        _linkLayer = new PdfLinkLayer(this);
        Layers.Add(_searchLayer);
        Layers.Add(_selectionLayer);
        Layers.Add(_linkLayer);

        // Colors of what the layers draw follow the theme through resources; layers are no controls and cannot bind.
        this.GetResourceObservable(PdfViewerTokens.Key(PdfViewerTokenKind.SearchHighlight)).Subscribe(new LayerBrush(this, static (v, b) => v._searchHighlightBrush = b));
        this.GetResourceObservable(PdfViewerTokens.Key(PdfViewerTokenKind.SearchHighlightCurrent)).Subscribe(new LayerBrush(this, static (v, b) => v._searchHighlightCurrentBrush = b));
        this.GetResourceObservable(PdfViewerTokens.Key(PdfViewerTokenKind.LinkHoverFill)).Subscribe(new LayerBrush(this, static (v, b) => v._linkHoverBrush = b));
        this.GetResourceObservable(ArcDesignTokenKind.Accent).Subscribe(new LayerBrush(this, static (v, b) =>
            v._selectionBrush = b is ISolidColorBrush accent ? ToBrush(SelectionColorFor(accent.Color)) : null));

        AddHandler(ContextRequestedEvent, OnContextRequested);
        AddHandler(PointerWheelChangedEvent, OnWheelTunnel, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    /// <summary>Keeps one brush of the layers up to date.</summary>
    private sealed class LayerBrush(PdfViewer viewer, Action<PdfViewer, IBrush?> set) : IObserver<object?>
    {
        public void OnNext(object? value)
        {
            set(viewer, value as IBrush);
            viewer._searchLayer.Invalidate();
        }

        public void OnError(Exception error)
        {
        }

        public void OnCompleted()
        {
        }
    }

    internal IBrush? SearchHighlightBrush => _searchHighlightBrush;

    internal IBrush? SearchHighlightCurrentBrush => _searchHighlightCurrentBrush;

    internal IBrush? LinkHoverBrush => _linkHoverBrush;

    /// <summary>The text of the current page, for screen readers (<see cref="PdfViewerAutomationPeer"/>). Loaded only once a peer exists.</summary>
    internal string? CurrentPageText => _currentPageText;

    /// <summary>The sidebar, once it has been opened (tests).</summary>
    internal PdfSidebar? Sidebar => _sidebar;

    /// <summary>The state cover, once it has been needed (tests).</summary>
    internal PdfStateLayer? StateLayer => _stateLayer;

    /// <summary>Takes the viewer to a place in the document: a whole page, or a point of one brought into view near the top of the viewport.</summary>
    internal void JumpTo(PdfDestination destination)
    {
        if (destination.PointInPage is not { } point || EffectivePageSource is not { } source
            || destination.PageIndex < 0 || destination.PageIndex >= source.PageCount)
        {
            GoToPage(destination.PageIndex);
            return;
        }
        // From the point to the bottom of the page: taller than the viewport lands the point at the top; shorter, the page's end is on screen with the point.
        var size = source.GetPageSize(destination.PageIndex);
        ScrollIntoView(destination.PageIndex, new Rect(point.X, point.Y, 1, Math.Max(1, size.Height - point.Y)));
    }

    #region Viewer extension points

    /// <inheritdoc />
    protected override bool OnViewerKeyDown(KeyEventArgs e)
    {
        // A key may scroll the page out from under the pointer.
        _linkLayer.ClearHover();
        if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None)
        {
            if (IsSearchOpen)
            {
                SetCurrentValue(IsSearchOpenProperty, false);
                return true;
            }
            if (_sidebarFloats && IsSidebarOpen)
            {
                SetCurrentValue(IsSidebarOpenProperty, false);
                return true;
            }
            if (_selectionLayer.HasSelection)
            {
                ClearSelection();
                return true;
            }
        }
        return HandleSearchKey(e) || HandleSelectionKey(e) || base.OnViewerKeyDown(e);
    }

    /// <inheritdoc />
    protected override void OnPageSourceChanged(IPageSource? oldSource, IPageSource? newSource)
    {
        base.OnPageSourceChanged(oldSource, newSource);
        UpdateLabelSubscription();
        OnPagesReplaced();
    }

    /// <inheritdoc />
    protected override void OnPagesChanged()
    {
        base.OnPagesChanged();
        OnPagesReplaced();
    }

    /// <inheritdoc />
    protected override void OnPageBitmapReady(int pageIndex, PixelSize targetSize)
    {
        base.OnPageBitmapReady(pageIndex, targetSize);
        _sidebar?.OnThumbnailReady(pageIndex, targetSize);
    }

    private void OnPagesReplaced()
    {
        var hasPages = HasPages;
        _sidebarToggle.IsEnabled = hasPages;
        _sidebarToggle.IsVisible = hasPages;
        _sidebar?.SetPages(EffectivePageSource);
        UpdateSidebar();
        ResetForPages();
    }

    /// <summary>Page labels may arrive after the pages do, on any thread (<see cref="PdfDocument.PageLabelsChanged"/>).</summary>
    private void UpdateLabelSubscription()
    {
        var wanted = VisualRoot is not null ? EffectivePageSource as PdfDocument : null;
        if (ReferenceEquals(wanted, _labelSource))
        {
            return;
        }
        if (_labelSource is { } old)
        {
            old.PageLabelsChanged -= OnPageLabelsChanged;
        }
        _labelSource = wanted;
        if (wanted is not null)
        {
            wanted.PageLabelsChanged += OnPageLabelsChanged;
            _sidebar?.OnPageLabelsChanged();
        }
    }

    private void OnPageLabelsChanged(object? sender, EventArgs e) => Dispatcher.UIThread.Post(() =>
    {
        if (ReferenceEquals(sender, _labelSource))
        {
            _sidebar?.OnPageLabelsChanged();
        }
    }, DispatcherPriority.Background);

    #endregion

    #region What the thumbnails need of the viewer

    internal IPageSource? PagesForThumbnails => EffectivePageSource;

    internal Bitmap? RequestThumbnail(int pageIndex, PixelSize size) => GetPageBitmap(pageIndex, size);

    internal Bitmap? PeekThumbnail(int pageIndex, PixelSize size) => TryGetPageBitmap(pageIndex, size);

    internal void DropThumbnailRequest(int pageIndex, PixelSize size) => CancelPageBitmapRequest(pageIndex, size);

    internal string ThumbnailLabel(int pageIndex) =>
        (EffectivePageSource as PdfDocument)?.GetPageLabel(pageIndex) ?? (pageIndex + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);

    #endregion

    #region Property changes, tree

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        // A set source wins: a change of document has nothing to do until the source goes.
        if (change.Property == SourceProperty || (change.Property == DocumentProperty && Source is null))
        {
            Reopen();
        }
        else if (change.Property == IsSidebarOpenProperty)
        {
            PseudoClasses.Set(SidebarOpenPseudoClass, change.GetNewValue<bool>());
            UpdateSidebar();
        }
        else if (change.Property == SidebarModeProperty)
        {
            _sidebar?.UpdateMode();
        }
        else if (change.Property == IsSearchOpenProperty)
        {
            OnSearchOpenChanged(change.GetNewValue<bool>());
        }
        else if (change.Property == SearchTextProperty)
        {
            OnSearchTextChanged();
        }
        else if (change.Property == IsMatchCaseProperty || change.Property == IsWholeWordProperty)
        {
            if (SearchText.Length > 0 && !_searchSuspended)
            {
                StartSearch();
            }
            _searchBar?.Sync();
        }
        else if (change.Property == CurrentSearchMatchProperty)
        {
            OnCurrentSearchMatchChanged();
        }
        else if (change.Property == IsTextSelectionEnabledProperty)
        {
            if (!change.GetNewValue<bool>())
            {
                ClearSelection();
            }
        }
        else if (change.Property == CurrentPageIndexProperty)
        {
            _sidebar?.OnCurrentPageChanged();
            if (ControlAutomationPeer.FromElement(this) is PdfViewerAutomationPeer)
            {
                LoadCurrentPageText();
            }
        }
        else if (change.Property == StatusProperty)
        {
            var status = change.GetNewValue<PdfViewerStatus>();
            PseudoClasses.Set(FailedPseudoClass, status == PdfViewerStatus.Failed);
            PseudoClasses.Set(PasswordPseudoClass, status == PdfViewerStatus.PasswordRequired);
        }
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        var narrow = e.NewSize.Width < NarrowWidth;
        PseudoClasses.Set(NarrowPseudoClass, narrow);
        if (narrow != _sidebarFloats)
        {
            _sidebarFloats = narrow;
            if (_sidebar is not null)
            {
                PlaceSidebar();
            }
            PlaceSearchBar();
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateLabelSubscription();
        // A source opens while the viewer is in a tree and closes when it leaves. Back before the leaving took effect, it just goes on.
        var moved = _leaving && _session == _leavingSession;
        _leaving = false;
        if (Source is not null && !moved)
        {
            Reopen();
        }
        ResumeSearch();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        UpdateLabelSubscription();
        ClearLinkTip();
        PauseSearch();
        if (Source is not null)
        {
            // Not yet: a parent can move the viewer in one step (the browser host does at startup, a full-screen preview does), and that
            // should neither fetch the file again nor ask for its password again.
            _leaving = true;
            _leavingSession = _session;
            Dispatcher.UIThread.Post(FinishLeaving, DispatcherPriority.Background);
        }
    }

    /// <summary>The viewer really left: closes what it opened from <see cref="Source"/>.</summary>
    private void FinishLeaving()
    {
        if (!_leaving || _session != _leavingSession)
        {
            return;
        }
        _leaving = false;
        StopOpening();
        CloseDocument();
        SetEmpty();
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new PdfViewerAutomationPeer(this);

    #endregion

    #region Sidebar

    /// <summary>Whether there is anything to navigate. Without pages the sidebar, the search bar and their buttons stay away, and come back with the next document.</summary>
    private bool HasPages => EffectivePageSource is { PageCount: > 0 };

    private void OnSidebarToggleClick(object? sender, RoutedEventArgs e) => SetCurrentValue(IsSidebarOpenProperty, !IsSidebarOpen);

    private void OnSearchToggleClick(object? sender, RoutedEventArgs e) => SetCurrentValue(IsSearchOpenProperty, !IsSearchOpen);

    private void UpdateSidebar()
    {
        var open = IsSidebarOpen && HasPages;
        _sidebarToggle.IsActive = IsSidebarOpen;
        if (open && _sidebar is null)
        {
            _sidebar = new PdfSidebar(this);
            _sidebar.SetPages(EffectivePageSource);
            _sidebar.SetOutline(_outline);
            PlaceSidebar();
        }
        if (_sidebar is null)
        {
            return;
        }
        if (_sidebarFloats)
        {
            _floatingHost!.IsPresent = open;
        }
        else
        {
            _dockedHost.IsPresent = open;
        }
        if (open)
        {
            _sidebar.OnReopened();
        }
        else
        {
            CancelPageBitmapRequests();
        }
    }

    /// <summary>Puts the sidebar beside the pages or over them. Each place has its own host and its own motion (spec section 8).</summary>
    private void PlaceSidebar()
    {
        var sidebar = _sidebar!;
        var open = IsSidebarOpen && HasPages;
        _dockedHost.Child = null;
        if (_floatingHost is not null)
        {
            _floatingHost.Child = null;
        }
        PresenceHost target;
        if (_sidebarFloats)
        {
            target = EnsureFloatingHost();
            _dockedHost.IsPresent = false;
        }
        else
        {
            target = _dockedHost;
            if (_floatingHost is not null)
            {
                _floatingHost.IsPresent = false;
            }
        }
        target.Child = sidebar;
        sidebar.SetFloating(_sidebarFloats);
        sidebar.IsVisible = target.IsPresent;
        target.IsPresent = open;
    }

    private PresenceHost EnsureFloatingHost()
    {
        if (_floatingHost is null)
        {
            _floatingHost = new PresenceHost
            {
                IsPresent = false,
                Mode = PresenceMode.Slide,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left
            };
            _floatingHost.Bind(PresenceHost.OffsetProperty, _sidebar!.GetObservable(WidthProperty));
            // Under the state cover: while there are no pages there is nothing to navigate.
            EnsureOverlay().Children.Insert(0, _floatingHost);
        }
        return _floatingHost;
    }

    private Panel EnsureOverlay()
    {
        if (_overlay is null)
        {
            _overlay = new Panel();
            ContentOverlay = _overlay;
        }
        return _overlay;
    }

    #endregion

    #region Accessibility

    /// <summary>A peer was created: from now on it hears about the current page's text.</summary>
    internal void OnAutomationPeerCreated() => LoadCurrentPageText();

    private async void LoadCurrentPageText()
    {
        var document = OpenedDocument;
        var page = CurrentPageIndex;
        string? text = null;
        if (document is not null && page < document.PageCount)
        {
            try
            {
                text = (await document.GetPageTextAsync(page, CancellationToken.None)).Text;
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                // Closed or unreadable: nothing to read out, and nothing a screen reader's question should bring the app down for.
            }
        }
        if (ReferenceEquals(document, OpenedDocument) && page == CurrentPageIndex)
        {
            _currentPageText = text;
        }
    }

    #endregion
}

/// <summary>
/// Exposes a <see cref="PdfViewer"/> as a document: named after its position like any <see cref="PageViewer"/>, with the text of
/// the current page as help text, so a screen reader can read it (there is no text pattern yet).
/// </summary>
public class PdfViewerAutomationPeer : PageViewerAutomationPeer
{
    /// <summary>Creates the peer of a viewer.</summary>
    public PdfViewerAutomationPeer(PdfViewer owner)
        : base(owner)
    {
        owner.OnAutomationPeerCreated();
    }

    /// <summary>The viewer.</summary>
    public new PdfViewer Owner => (PdfViewer)base.Owner;

    /// <inheritdoc />
    protected override string? GetHelpTextCore() => base.GetHelpTextCore() is { Length: > 0 } help ? help : Owner.CurrentPageText;
}
