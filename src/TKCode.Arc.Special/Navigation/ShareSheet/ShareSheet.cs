using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gestures;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// Shares one item from a Share button that stretches, on a spring, into a sharing panel: the item's name as a heading,
/// its link with a Copy button, the access level that goes with the link, channel buttons for posting it, recent people
/// to pick (each pick sends their avatar to a chip) and Send. A successful send turns the same surface into a small
/// confirmation with Undo and Done, and Done shrinks it back to the button. When the viewport is 520 px wide or less,
/// the panel is shown as a sheet at the bottom of a dimmed page instead and can be dragged closed by its grabber.
/// </summary>
/// <remarks>
/// Only the button occupies layout space; the panel is placed on the window's overlay layer above the page, so keep
/// space under the button, or near an edge set <see cref="Align"/> so that it opens away from the edge. Send and the
/// channel buttons call <see cref="SendHandler"/> and <see cref="ChannelHandler"/> and await them: the buttons spin
/// while they run, an exception is reported (the panel stays open), and success brings the confirmation or the
/// channel's done word. Assistive technology hears about sends, copies, channel results, picks and removals.
/// </remarks>
[TemplatePart(TriggerPart, typeof(ShareSheetTrigger))]
[TemplatePart(StatusPart, typeof(TextBlock))]
[PseudoClasses(OpenPseudoClass)]
public class ShareSheet : TemplatedControl
{
    internal const string TriggerPart = "PART_Trigger";
    internal const string StatusPart = "PART_Status";
    internal const string OpenPseudoClass = ":open";

    /// <summary>The item's name, which the panel puts in its heading as Share “name”.</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<ShareSheet, string>(nameof(Title), string.Empty);

    /// <summary>The URL to share: displayed in its own row and copied by the Copy button; null leaves the row out.</summary>
    public static readonly StyledProperty<string?> LinkProperty =
        AvaloniaProperty.Register<ShareSheet, string?>(nameof(Link));

    /// <summary>The Share button's text (default "Share").</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ShareSheet, string>(nameof(Label), "Share");

    /// <summary>The 24 × 24 stroked glyph before the Share button's text; null shows the text alone.</summary>
    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<ShareSheet, Geometry?>(nameof(Glyph), ArcIcons.Share);

    /// <summary>
    /// The <see cref="ShareSheetAccess.Value"/> of the chosen access level (two-way). Null, or a value no level has, means
    /// the first level.
    /// </summary>
    public static readonly StyledProperty<string?> AccessProperty =
        AvaloniaProperty.Register<ShareSheet, string?>(nameof(Access), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Where the panel is anchored to the button as it opens: the top end corner (End, the default), the top start corner (Start) or the center (Center).</summary>
    public static readonly StyledProperty<ShareSheetAlign> AlignProperty =
        AvaloniaProperty.Register<ShareSheet, ShareSheetAlign>(nameof(Align), ShareSheetAlign.End);

    /// <summary>
    /// Whether viewports 520 px wide or narrower get a sheet docked to the bottom, with the page dimmed behind it, instead
    /// of the floating panel (default true). The sheet slides up, tracks drags on its grabber, and is dismissed by
    /// Escape, a press on the dimmed page, a long drag or Done.
    /// </summary>
    public static readonly StyledProperty<bool> SheetOnPhonesProperty =
        AvaloniaProperty.Register<ShareSheet, bool>(nameof(SheetOnPhones), true);

    /// <summary>Whether the panel (or the confirmation) is open (two-way).</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<ShareSheet, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Sends the item to the people picked (to your server, say). Send waits for it with a spinner; when it throws, an
    /// error shows over the Send button and the panel stays open; when it completes, the confirmation shows. Null sends at
    /// once.
    /// </summary>
    public static readonly StyledProperty<Func<ShareSheetSubmission, CancellationToken, Task>?> SendHandlerProperty =
        AvaloniaProperty.Register<ShareSheet, Func<ShareSheetSubmission, CancellationToken, Task>?>(nameof(SendHandler));

    /// <summary>
    /// Posts the link to a channel. The channel's button spins while it runs, then shows the channel's done word, or
    /// "Failed" when it throws. Null finishes at once.
    /// </summary>
    public static readonly StyledProperty<Func<ShareSheetChannel, CancellationToken, Task>?> ChannelHandlerProperty =
        AvaloniaProperty.Register<ShareSheet, Func<ShareSheetChannel, CancellationToken, Task>?>(nameof(ChannelHandler));

    /// <summary>The words the sheet shows and announces.</summary>
    public static readonly StyledProperty<ShareSheetLabels> LabelsProperty =
        AvaloniaProperty.Register<ShareSheet, ShareSheetLabels>(nameof(Labels), new ShareSheetLabels());

    /// <summary>Raised once Copy has put the link on the clipboard; the arguments say which access level was chosen at that moment. Bubbles.</summary>
    public static readonly RoutedEvent<ShareSheetLinkCopiedEventArgs> LinkCopiedEvent =
        RoutedEvent.Register<ShareSheet, ShareSheetLinkCopiedEventArgs>(nameof(LinkCopied), RoutingStrategies.Bubble);

    /// <summary>Raised when a channel finished posting (after <see cref="ChannelHandler"/> completed). Bubbles.</summary>
    public static readonly RoutedEvent<ShareSheetChannelEventArgs> ChannelSharedEvent =
        RoutedEvent.Register<ShareSheet, ShareSheetChannelEventArgs>(nameof(ChannelShared), RoutingStrategies.Bubble);

    /// <summary>Raised after a send succeeded (after <see cref="SendHandler"/> completed), with who and under which access. Bubbles.</summary>
    public static readonly RoutedEvent<ShareSheetSentEventArgs> SentEvent =
        RoutedEvent.Register<ShareSheet, ShareSheetSentEventArgs>(nameof(Sent), RoutingStrategies.Bubble);

    /// <summary>Raised when Undo on the confirmation takes the last send back; take it back in your app too. Bubbles.</summary>
    public static readonly RoutedEvent<ShareSheetSentEventArgs> SendUndoneEvent =
        RoutedEvent.Register<ShareSheet, ShareSheetSentEventArgs>(nameof(SendUndone), RoutingStrategies.Bubble);

    private readonly List<ShareSheetPerson> _picked = new();
    private ShareSheetTrigger? _trigger;
    private TextBlock? _status;
    private ShareSheetPanelFace? _panelFace;
    private ShareSheetSentFace? _sentFace;
    private ShareSheetTrigger? _buttonFace;
    private ShareSheetStage? _floatStage;
    private ShareSheetSurface? _surface;
    private ShareSheetOverlayRoot? _overlayRoot;
    private Panel? _layer;
    private ShareSheetStage? _sheetStage;
    private ShareSheetSheetFrame? _sheetFrame;
    private ModalPresence? _presence;
    private MotionValue<double>? _sheetY;
    private MotionValue<double>? _sheetScrim;
    private DragTracker? _grabberDrag;
    private double _dragOrigin;
    private double _flingVelocity;
    private TopLevel? _pressRoot;
    private CancellationTokenSource _lifetime = new();
    private ShareSheetSubmission? _lastSent;
    private OpenMode _mode;
    private int _generation;
    private int _copyGeneration;
    private bool _syncing;
    private bool _detaching;
    private bool _isSending;

    private enum OpenMode
    {
        None,
        Floating,
        Sheet
    }

    static ShareSheet()
    {
        FocusableProperty.OverrideDefaultValue<ShareSheet>(false);
        ClipToBoundsProperty.OverrideDefaultValue<ShareSheet>(false);
        SpecialFocusModality.Watch();
    }

    public ShareSheet()
    {
        People.CollectionChanged += OnPeopleChanged;
        AccessLevels.CollectionChanged += OnAccessLevelsChanged;
        Channels.CollectionChanged += OnChannelsChanged;
        ActualThemeVariantChanged += (_, _) =>
        {
            if (_mode == OpenMode.Sheet)
            {
                UpdateScrimBrush();
            }
        };
    }

    /// <summary>The people offered for quick picking; pressing one adds them. List a few likely recipients here, not a whole address book.</summary>
    public AvaloniaList<ShareSheetPerson> People { get; } = new();

    /// <summary>The access levels in the list under the link; the first one is chosen unless <see cref="Access"/> says otherwise.</summary>
    public AvaloniaList<ShareSheetAccess> AccessLevels { get; } = new();

    /// <summary>The channel buttons under the link card (two to four fit).</summary>
    public AvaloniaList<ShareSheetChannel> Channels { get; } = new();

    /// <summary>The people picked so far, in the order they were picked.</summary>
    public IReadOnlyList<ShareSheetPerson> SelectedPeople => _picked;

    /// <summary>True while <see cref="SendHandler"/> runs.</summary>
    public bool IsSending => _isSending;

    /// <summary>The access level in effect: the one whose value is <see cref="Access"/>, else the first.</summary>
    public ShareSheetAccess? ChosenAccess =>
        AccessLevels.FirstOrDefault(a => Access is not null && string.Equals(a.Value, Access, StringComparison.Ordinal))
        ?? AccessLevels.FirstOrDefault();

    /// <inheritdoc cref="TitleProperty"/>
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="LinkProperty"/>
    public string? Link { get => GetValue(LinkProperty); set => SetValue(LinkProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="GlyphProperty"/>
    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    /// <inheritdoc cref="AccessProperty"/>
    public string? Access { get => GetValue(AccessProperty); set => SetValue(AccessProperty, value); }

    /// <inheritdoc cref="AlignProperty"/>
    public ShareSheetAlign Align { get => GetValue(AlignProperty); set => SetValue(AlignProperty, value); }

    /// <inheritdoc cref="SheetOnPhonesProperty"/>
    public bool SheetOnPhones { get => GetValue(SheetOnPhonesProperty); set => SetValue(SheetOnPhonesProperty, value); }

    /// <inheritdoc cref="IsOpenProperty"/>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <inheritdoc cref="SendHandlerProperty"/>
    public Func<ShareSheetSubmission, CancellationToken, Task>? SendHandler
    {
        get => GetValue(SendHandlerProperty);
        set => SetValue(SendHandlerProperty, value);
    }

    /// <inheritdoc cref="ChannelHandlerProperty"/>
    public Func<ShareSheetChannel, CancellationToken, Task>? ChannelHandler
    {
        get => GetValue(ChannelHandlerProperty);
        set => SetValue(ChannelHandlerProperty, value);
    }

    /// <inheritdoc cref="LabelsProperty"/>
    public ShareSheetLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="LinkCopiedEvent"/>
    public event EventHandler<ShareSheetLinkCopiedEventArgs>? LinkCopied
    {
        add => AddHandler(LinkCopiedEvent, value);
        remove => RemoveHandler(LinkCopiedEvent, value);
    }

    /// <inheritdoc cref="ChannelSharedEvent"/>
    public event EventHandler<ShareSheetChannelEventArgs>? ChannelShared
    {
        add => AddHandler(ChannelSharedEvent, value);
        remove => RemoveHandler(ChannelSharedEvent, value);
    }

    /// <inheritdoc cref="SentEvent"/>
    public event EventHandler<ShareSheetSentEventArgs>? Sent
    {
        add => AddHandler(SentEvent, value);
        remove => RemoveHandler(SentEvent, value);
    }

    /// <inheritdoc cref="SendUndoneEvent"/>
    public event EventHandler<ShareSheetSentEventArgs>? SendUndone
    {
        add => AddHandler(SendUndoneEvent, value);
        remove => RemoveHandler(SendUndoneEvent, value);
    }

    #region Internals for tests

    internal ShareSheetTrigger? Trigger => _trigger;

    internal ShareSheetPanelFace? PanelFace => _panelFace;

    internal ShareSheetSentFace? SentFace => _sentFace;

    internal ShareSheetStage? FloatingStage => _floatStage;

    internal ShareSheetSurface? Surface => _surface;

    /// <summary>What the floating shape is mounted with on the overlay layer.</summary>
    internal Control? OverlayRoot => _overlayRoot;

    internal ShareSheetSheetFrame? SheetFrame => _sheetFrame;

    internal ModalPresence? Presence => _presence;

    internal bool IsFloating => _mode == OpenMode.Floating;

    internal bool IsSheetOpen => _mode == OpenMode.Sheet;

    internal string? LastAnnouncement => _status?.Text;

    /// <summary>The stage in use (floating or sheet).</summary>
    internal ShareSheetStage? ActiveStage => _mode == OpenMode.Sheet ? _sheetStage : _floatStage;

    #endregion

    /// <summary>Opens the panel.</summary>
    public void Open() => SetCurrentValue(IsOpenProperty, true);

    /// <summary>Folds the panel (or the confirmation) back into the button.</summary>
    public void Close() => SetCurrentValue(IsOpenProperty, false);

    /// <summary>Adds <paramref name="person"/> (one of <see cref="People"/>) to the recipients.</summary>
    public void Pick(ShareSheetPerson person)
    {
        if (_picked.Contains(person) || !People.Contains(person) || _isSending)
        {
            return;
        }
        _picked.Add(person);
        _panelFace?.SetError(null);
        _panelFace?.SyncChips(animate: IsOpen);
        Announce(Labels.AddedAnnouncement(person.DisplayName));
    }

    /// <summary>Takes <paramref name="person"/> out of the recipients.</summary>
    public void Unpick(ShareSheetPerson person)
    {
        if (!_picked.Remove(person) || _isSending)
        {
            return;
        }
        _panelFace?.SetError(null);
        _panelFace?.SyncChips(animate: IsOpen);
        Announce(Labels.RemovedAnnouncement(person.DisplayName));
    }

    internal void TogglePerson(ShareSheetPerson person)
    {
        if (_picked.Contains(person))
        {
            Unpick(person);
        }
        else
        {
            Pick(person);
        }
    }

    internal bool IsPicked(ShareSheetPerson person) => _picked.Contains(person);

    /// <summary>Whether the bottom-sheet presentation applies at the current viewport width.</summary>
    internal bool UsesSheet =>
        SheetOnPhones && ArcViewport.GetSize(this) is { } viewport && viewport.Width <= Token(ShareSheetTokenKind.SheetBreakpoint, 520);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_trigger is not null)
        {
            _trigger.Click -= OnTriggerClick;
        }
        _trigger = e.NameScope.Find<ShareSheetTrigger>(TriggerPart);
        _status = e.NameScope.Find<TextBlock>(StatusPart);
        if (_trigger is not null)
        {
            _trigger.Click += OnTriggerClick;
            AutomationProperties.SetItemStatus(_trigger, IsOpen ? "Expanded" : "Collapsed");
        }
        if (_status is not null)
        {
            AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty)
        {
            var open = change.GetNewValue<bool>();
            PseudoClasses.Set(OpenPseudoClass, open);
            if (_trigger is not null)
            {
                AutomationProperties.SetItemStatus(_trigger, open ? "Expanded" : "Collapsed");
            }
            if (!_syncing)
            {
                if (open)
                {
                    OpenCore();
                }
                else
                {
                    CloseCore(focusTrigger: true);
                }
            }
        }
        else if (change.Property == AccessProperty)
        {
            _panelFace?.ShowChosen();
        }
        else if (change.Property == LinkProperty)
        {
            _panelFace?.RefreshLink();
        }
        else if (change.Property == TitleProperty || change.Property == LabelsProperty)
        {
            _panelFace?.RefreshWords();
            _sentFace?.Refresh();
        }
        else if (change.Property == AlignProperty)
        {
            if (_floatStage is not null)
            {
                _floatStage.Align = Align;
            }
            PositionSurface();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_lifetime.IsCancellationRequested)
        {
            _lifetime = new CancellationTokenSource();
        }
        if (IsOpen && _mode == OpenMode.None)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (IsOpen && _mode == OpenMode.None && TopLevel.GetTopLevel(this) is not null)
                {
                    OpenCore();
                }
            }, DispatcherPriority.Loaded);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        // The bottom sheet's presence removes itself when this control leaves the tree (it raises Closed from inside
        // the base call); IsOpen stays as it is, so the panel comes back when the control is attached again.
        _detaching = true;
        try
        {
            base.OnDetachedFromVisualTree(e);
        }
        finally
        {
            _detaching = false;
        }
        _lifetime.Cancel();
        _generation++;
        UnmountFloating();
        WatchOutsidePresses(false);
    }

    #region Open and close

    private void OnTriggerClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        SetCurrentValue(IsOpenProperty, !IsOpen);
    }

    private void OpenCore()
    {
        if (TopLevel.GetTopLevel(this) is null)
        {
            return;
        }
        _generation++;
        if (_mode == OpenMode.Sheet)
        {
            return;
        }
        if (_mode == OpenMode.None && UsesSheet)
        {
            OpenSheet();
            return;
        }
        OpenFloating();
    }

    private void CloseCore(bool focusTrigger)
    {
        _generation++;
        _panelFace?.SetAccessListOpen(false, focusList: false);
        _panelFace?.LandFlights();
        if (_mode == OpenMode.Sheet)
        {
            _presence?.Close(DismissReason.Programmatic);
            return;
        }
        if (_mode == OpenMode.Floating)
        {
            Fold(focusTrigger);
        }
    }

    /// <summary>The close button: folds back and gives focus to the Share button.</summary>
    internal void CloseFromPanel()
    {
        if (_mode == OpenMode.Sheet)
        {
            _presence?.Dismiss(DismissReason.CloseButton);
            return;
        }
        SetClosed(focusTrigger: true);
    }

    /// <summary>Sets <see cref="IsOpen"/> to false and folds, choosing whether focus goes to the button.</summary>
    private void SetClosed(bool focusTrigger)
    {
        if (!IsOpen)
        {
            return;
        }
        _syncing = true;
        try
        {
            SetCurrentValue(IsOpenProperty, false);
        }
        finally
        {
            _syncing = false;
        }
        CloseCore(focusTrigger);
    }

    private ShareSheetPanelFace EnsurePanelFace()
    {
        if (_panelFace is null)
        {
            _panelFace = new ShareSheetPanelFace(this);
        }
        return _panelFace;
    }

    private ShareSheetSentFace EnsureSentFace() => _sentFace ??= new ShareSheetSentFace(this);

    private double Token(ShareSheetTokenKind kind, double fallback) =>
        this.TryFindResource(ShareSheetTokens.Key(kind), out var value) && value is double d ? d : fallback;

    /// <summary>Width a floating face may take: its token width, never wider than the viewport less 32 px.</summary>
    private double FitWidth(double width)
    {
        if (ArcViewport.GetSize(this) is { Width: > 0 } viewport)
        {
            return Math.Max(0, Math.Min(width, viewport.Width - 32));
        }
        return width;
    }

    private void OpenFloating()
    {
        var panel = EnsurePanelFace();
        panel.SetSheet(false);
        panel.Width = FitWidth(Token(ShareSheetTokenKind.PanelWidth, 384));
        _sheetStage?.Reset();
        if (_floatStage is null)
        {
            _floatStage = new ShareSheetStage();
            _floatStage.ShapeChanged += PositionSurface;
            _surface = new ShareSheetSurface(_floatStage);
            _surface.AddHandler(KeyDownEvent, OnSurfaceKeyDown, RoutingStrategies.Bubble);
            // The shape's logical parent is this control (its styles, tokens and theme variant come from here, also in
            // an ArcOverlayHost whose layer is outside the logical tree); the layer only hosts it visually.
            LogicalChildren.Add(_surface);
            _overlayRoot = new ShareSheetOverlayRoot(_surface);
            _buttonFace = new ShareSheetTrigger { IsFace = true, IsTabStop = false };
            _buttonFace.Bind(ShareSheetTrigger.LabelProperty, this.GetObservable(LabelProperty));
            _buttonFace.Bind(ShareSheetTrigger.GlyphProperty, this.GetObservable(GlyphProperty));
            _buttonFace.Click += OnFaceButtonClick;
        }
        _floatStage.Align = Align;
        var stage = _floatStage;
        var surface = _surface!;
        var root = _overlayRoot!;
        if (_mode != OpenMode.Floating || root.Parent is null)
        {
            if (ArcOverlayLayer.Find(this) is not { } layer)
            {
                return;
            }
            _layer = layer;
            root.SetValue(ArcMotion.LevelProperty, ArcMotion.GetLevel(this));
            root.ZIndex = int.MaxValue - 1;
            layer.Children.Add(root);
            layer.SizeChanged += OnLayerSizeChanged;
            LayoutUpdated += OnLayoutUpdated;
            stage.Reset();
            surface.JumpLook(panel: false);
            stage.Show(_buttonFace!, ShareSheetFace.Button, TriggerRadius(), ShareSheetFaceSwap.Scale, 1, animate: false);
            PositionSurface();
            if (_trigger is not null)
            {
                _trigger.Opacity = 0;
                _trigger.IsHitTestVisible = false;
            }
        }
        _mode = OpenMode.Floating;
        if (_trigger is not null)
        {
            _trigger.IsTabStop = false;
        }
        _buttonFace!.IsHitTestVisible = false;
        stage.Show(panel, ShareSheetFace.Panel, Token(ShareSheetTokenKind.PanelRadius, 28), ShareSheetFaceSwap.Scale, 1, animate: true);
        surface.AnimateLook(panel: true);
        PositionSurface();
        WatchOutsidePresses(true);
        FocusSoon(() => _panelFace?.Copy is { IsEffectivelyVisible: true } copy ? copy : FirstFocusable(_panelFace));
    }

    private double TriggerRadius() => (_trigger?.Bounds.Height is > 0 and var h ? h : Token(ShareSheetTokenKind.TriggerHeight, 36)) / 2;

    /// <summary>The floating shape folds back into the button, then leaves the overlay.</summary>
    private void Fold(bool focusTrigger)
    {
        if (_floatStage is null || _surface is null || _buttonFace is null)
        {
            return;
        }
        WatchOutsidePresses(false);
        if (_trigger is not null)
        {
            _trigger.IsTabStop = true;
            if (focusTrigger && FocusInsideSurface())
            {
                // The button takes focus back now (it is still hidden under the shape): the panel's focus is about to go.
                _trigger.Focus(SpecialFocusModality.For(this));
            }
        }
        _buttonFace.IsHitTestVisible = true;
        _floatStage.Show(_buttonFace, ShareSheetFace.Button, TriggerRadius(), ShareSheetFaceSwap.Scale, -1, animate: true);
        _surface.AnimateLook(panel: false);
        var generation = _generation;
        _ = FinishFoldAsync(generation);
    }

    private async Task FinishFoldAsync(int generation)
    {
        var stage = _floatStage;
        if (stage is null)
        {
            return;
        }
        if (await stage.WhenSettledAsync(() => generation == _generation) && generation == _generation)
        {
            UnmountFloating();
            if (_lastSent is not null)
            {
                ResetAfterSend();
            }
        }
    }

    private void UnmountFloating()
    {
        if (_overlayRoot is { Parent: Panel layer })
        {
            layer.Children.Remove(_overlayRoot);
        }
        if (_layer is not null)
        {
            _layer.SizeChanged -= OnLayerSizeChanged;
        }
        LayoutUpdated -= OnLayoutUpdated;
        _layer = null;
        _floatStage?.Reset();
        if (_trigger is not null)
        {
            _trigger.Opacity = 1;
            _trigger.IsHitTestVisible = true;
            _trigger.IsTabStop = true;
        }
        if (_mode == OpenMode.Floating)
        {
            _mode = OpenMode.None;
        }
    }

    private void OnFaceButtonClick(object? sender, RoutedEventArgs e)
    {
        // A press on the shape while it folds opens it again.
        e.Handled = true;
        Open();
    }

    private void OnLayerSizeChanged(object? sender, SizeChangedEventArgs e) => PositionSurface();

    private void OnLayoutUpdated(object? sender, EventArgs e) => PositionSurface();

    private void PositionSurface()
    {
        if (_overlayRoot is null || _floatStage is null || _layer is null || _trigger is null || _overlayRoot.Parent is null)
        {
            return;
        }
        if (_trigger.TranslatePoint(default, _layer) is not { } origin)
        {
            return;
        }
        var anchor = new Rect(origin, _trigger.Bounds.Size);
        var size = _floatStage.ShapeSize;
        double x;
        double y;
        switch (Align)
        {
            case ShareSheetAlign.Center:
                x = anchor.Center.X - size.Width / 2;
                y = anchor.Center.Y - size.Height / 2;
                break;
            case ShareSheetAlign.Start:
                x = anchor.Left;
                y = anchor.Top;
                break;
            default:
                x = anchor.Right - size.Width;
                y = anchor.Top;
                break;
        }
        // Never past the layer's edges (8 px in): a button near an edge grows away from it.
        var bounds = _layer.Bounds.Size;
        const double margin = 8;
        if (bounds.Width > 2 * margin + size.Width)
        {
            x = Math.Clamp(x, margin, bounds.Width - margin - size.Width);
        }
        if (bounds.Height > 2 * margin + size.Height)
        {
            y = Math.Clamp(y, margin, bounds.Height - margin - size.Height);
        }
        if (Canvas.GetLeft(_overlayRoot) != x)
        {
            Canvas.SetLeft(_overlayRoot, x);
        }
        if (Canvas.GetTop(_overlayRoot) != y)
        {
            Canvas.SetTop(_overlayRoot, y);
        }
    }

    private bool FocusInsideSurface()
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
        return focused is not null && _surface is not null && (ReferenceEquals(focused, _surface) || _surface.IsVisualAncestorOf(focused));
    }

    private void OnSurfaceKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.Key != Key.Escape)
        {
            return;
        }
        e.Handled = true;
        HandleEscape();
    }

    private void HandleEscape()
    {
        if (_panelFace is { IsAccessListOpen: true } panel)
        {
            panel.SetAccessListOpen(false, focusList: false);
            panel.AccessRow?.Focus(SpecialFocusModality.For(this));
            return;
        }
        if (_mode == OpenMode.Sheet)
        {
            _presence?.Dismiss(DismissReason.Escape);
            return;
        }
        SetClosed(focusTrigger: true);
    }

    private void WatchOutsidePresses(bool watch)
    {
        if (_pressRoot is not null)
        {
            _pressRoot.RemoveHandler(PointerPressedEvent, OnRootPointerPressed);
            _pressRoot = null;
        }
        if (watch && TopLevel.GetTopLevel(this) is { } root)
        {
            _pressRoot = root;
            root.AddHandler(PointerPressedEvent, OnRootPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        }
    }

    private void OnRootPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_mode != OpenMode.Floating || !IsOpen || e.Source is not Visual source)
        {
            return;
        }
        if (_surface is not null && (ReferenceEquals(source, _surface) || _surface.IsVisualAncestorOf(source)))
        {
            return;
        }
        if (ReferenceEquals(source, this) || this.IsVisualAncestorOf(source))
        {
            return;
        }
        // A press elsewhere folds the panel and leaves focus to whatever was pressed.
        SetClosed(focusTrigger: false);
    }

    private void FocusSoon(Func<Control?> target)
    {
        var generation = _generation;
        var method = SpecialFocusModality.For(this);
        Dispatcher.UIThread.Post(() =>
        {
            if (generation == _generation && IsOpen && target() is { } control)
            {
                control.Focus(method);
            }
        }, DispatcherPriority.Loaded);
    }

    private static Control? FirstFocusable(Control? root) =>
        root is null ? null : FocusManager.FindFirstFocusableElement(root) as Control;

    #endregion

    #region Bottom sheet

    private void OpenSheet()
    {
        var panel = EnsurePanelFace();
        panel.SetSheet(true);
        panel.Width = double.NaN;
        _floatStage?.Reset();
        if (_sheetStage is null)
        {
            _sheetStage = new ShareSheetStage { IsSheet = true };
            _sheetFrame = new ShareSheetSheetFrame(_sheetStage);
            _sheetFrame.PartsReady += OnSheetPartsReady;
            _sheetFrame.AddHandler(KeyDownEvent, OnSheetKeyDown, RoutingStrategies.Bubble);
        }
        if (_presence is null)
        {
            _presence = new ModalPresence(_sheetFrame!, this)
            {
                Entrance = ModalEntrance.Custom,
                TrapFocus = true,
                RestoreFocus = true,
                CustomMotion = (presence, entering) => SheetMotion(presence, entering)
            };
            _presence.Dismissing += OnSheetDismissing;
            _presence.Closed += OnSheetClosed;
        }
        _sheetStage.Reset();
        _sheetStage.Show(panel, ShareSheetFace.Panel, 0, ShareSheetFaceSwap.Scale, 1, animate: false);
        _presence.InitialFocus = null;
        UpdateScrimBrush();
        _mode = OpenMode.Sheet;
        if (!_presence.Open())
        {
            _mode = OpenMode.None;
            return;
        }
        FocusSoon(() => _panelFace?.Copy is { IsEffectivelyVisible: true } copy ? copy : FirstFocusable(_panelFace));
    }

    /// <summary>
    /// The dim color comes from this control's theme: inside an <see cref="ArcOverlayHost"/> (the phone preview) the
    /// modal host sits on a layer outside the logical tree and cannot find the token itself.
    /// </summary>
    private void UpdateScrimBrush()
    {
        if (_presence is null)
        {
            return;
        }
        try
        {
            _presence.ScrimBrush = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(ArcTokens.GetColor(this, ArcDesignTokenKind.Scrim));
        }
        catch (InvalidOperationException)
        {
            _presence.ScrimBrush = null;
        }
    }

    private void OnSheetKeyDown(object? sender, KeyEventArgs e)
    {
        if (!e.Handled && e.Key == Key.Escape && _panelFace is { IsAccessListOpen: true })
        {
            e.Handled = true;
            HandleEscape();
        }
    }

    private Task<bool> SheetMotion(ModalPresence presence, bool entering)
    {
        var frame = _sheetFrame!;
        _sheetY ??= CreateSheetY(frame);
        if (_sheetScrim is null)
        {
            _sheetScrim = new MotionValue<double>(frame, 0, MotionVectors.Double);
            _sheetScrim.Changed += v =>
            {
                if (_presence is { } presence)
                {
                    presence.ScrimOpacity = v;
                }
            };
        }
        var hidden = SheetHiddenOffset();
        if (entering)
        {
            if (presence.State == ModalState.Opening && !_sheetY.IsAnimating && _sheetY.Value <= 0.5 && frame.Offset <= 0.5)
            {
                // A fresh open: below the viewport, the page not dimmed yet (a new presence starts at 0 too).
                _sheetY.Jump(hidden);
                _sheetScrim.Jump(0);
            }
            _ = _sheetScrim.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(200), ArcEasings.Standard));
            return _sheetY.AnimateTo(0, MotionSpec.Spring(ShareSheetMotion.Grow));
        }
        var velocity = _flingVelocity;
        _flingVelocity = 0;
        _ = _sheetScrim.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Exit, ArcEasings.Standard));
        return _sheetY.AnimateTo(hidden, MotionSpec.Spring(ShareSheetMotion.SheetLeave), velocity);
    }

    private MotionValue<double> CreateSheetY(ShareSheetSheetFrame frame)
    {
        var value = new MotionValue<double>(frame, 0, MotionVectors.Double);
        value.Changed += y => frame.Offset = y;
        return value;
    }

    /// <summary>The sheet starts and ends 110% of its height below its resting place.</summary>
    private double SheetHiddenOffset()
    {
        var frame = _sheetFrame!;
        var height = frame.Bounds.Height > 0 ? frame.Bounds.Height : frame.DesiredSize.Height;
        if (height <= 0)
        {
            height = ArcViewport.GetSize(this)?.Height ?? 600;
        }
        return height * 1.1 + frame.Margin.Bottom;
    }

    private void OnSheetPartsReady(object? sender, EventArgs e)
    {
        _grabberDrag?.Dispose();
        _grabberDrag = null;
        if (_sheetFrame?.Grabber is not { } grabber)
        {
            return;
        }
        _grabberDrag = new DragTracker(grabber, new DragOptions { Axis = DragAxis.Vertical, Threshold = 3, RejectCrossAxis = false, HandledEventsToo = true })
        {
            CanBegin = _ => _presence is { IsOpen: true }
        };
        _grabberDrag.Started += (_, e) => BeginSheetDrag(e.Offset.Y);
        _grabberDrag.Moved += (_, e) => MoveSheetDrag(e.Offset.Y);
        _grabberDrag.Completed += (_, e) => EndSheetDrag(e.Velocity.Y);
        _grabberDrag.Cancelled += (_, _) => EndSheetDrag(0);
    }

    private void BeginSheetDrag(double offsetAtStart)
    {
        if (_sheetFrame is null)
        {
            return;
        }
        _sheetY ??= CreateSheetY(_sheetFrame);
        _sheetY.Stop();
        _dragOrigin = _sheetY.Value - offsetAtStart;
    }

    private void MoveSheetDrag(double offset)
    {
        if (_sheetY is null)
        {
            return;
        }
        var raw = _dragOrigin + offset;
        // Down follows the finger; up barely gives (a few pixels at most).
        _sheetY.Jump(raw >= 0 ? raw : -Math.Min(4, 0.4 * Math.Sqrt(-raw)));
    }

    private void EndSheetDrag(double velocity)
    {
        if (_sheetY is null || _presence is not { IsOpen: true } presence)
        {
            return;
        }
        var offset = _sheetY.Value;
        if (offset > Token(ShareSheetTokenKind.SheetDismissDistance, 120) || velocity > Token(ShareSheetTokenKind.SheetDismissVelocity, 600))
        {
            _flingVelocity = Math.Max(0, velocity);
            if (presence.Dismiss(DismissReason.Gesture))
            {
                return;
            }
        }
        _ = _sheetY.AnimateTo(0, MotionSpec.Spring(ShareSheetMotion.SheetSettle), velocity);
    }

    private void OnSheetDismissing(object? sender, ModalDismissingEventArgs e)
    {
        if (e.Reason == DismissReason.Escape && _panelFace is { IsAccessListOpen: true })
        {
            e.Cancel = true;
            HandleEscape();
        }
    }

    private void OnSheetClosed(object? sender, ModalClosedEventArgs e)
    {
        if (_mode != OpenMode.Sheet)
        {
            return;
        }
        _mode = OpenMode.None;
        _sheetY?.Jump(0);
        _sheetScrim?.Jump(0);
        _sheetStage?.Reset();
        if (_sheetFrame is not null)
        {
            _sheetFrame.Offset = 0;
        }
        if (_detaching || TopLevel.GetTopLevel(this) is null)
        {
            // Removed because this control left the tree: IsOpen stays, and the sheet opens again when it is back.
            return;
        }
        if (_lastSent is not null)
        {
            ResetAfterSend();
        }
        if (IsOpen)
        {
            _syncing = true;
            try
            {
                SetCurrentValue(IsOpenProperty, false);
            }
            finally
            {
                _syncing = false;
            }
        }
    }

    #endregion

    #region Copy, channels, send

    internal async Task CopyLinkAsync()
    {
        var link = Link;
        var copy = _panelFace?.Copy;
        if (string.IsNullOrEmpty(link) || copy is null)
        {
            return;
        }
        var ok = await ArcClipboard.CopyTextAsync(this, link);
        var generation = ++_copyGeneration;
        copy.SetState(ok ? ShareSheetCopyState.Copied : ShareSheetCopyState.Failed);
        Announce(ok ? Labels.LinkCopiedAnnouncement : Labels.CopyFailedAnnouncement);
        if (ok)
        {
            RaiseEvent(new ShareSheetLinkCopiedEventArgs(LinkCopiedEvent, link, ChosenAccess));
        }
        DispatcherTimer.RunOnce(() =>
        {
            if (generation == _copyGeneration)
            {
                copy.SetState(ShareSheetCopyState.Idle);
            }
        }, ShareSheetMotion.CopyResult);
    }

    internal async Task RunChannelAsync(ShareSheetChannelButton button)
    {
        if (button.State == ShareSheetChannelState.Pending)
        {
            return;
        }
        var generation = ++button.Generation;
        button.SetState(ShareSheetChannelState.Pending);
        var ok = true;
        if (ChannelHandler is { } handler)
        {
            try
            {
                await handler(button.Channel, _lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                if (generation == button.Generation)
                {
                    button.SetState(ShareSheetChannelState.Idle);
                }
                return;
            }
            catch (Exception)
            {
                ok = false;
            }
        }
        if (generation != button.Generation)
        {
            return;
        }
        button.SetState(ok ? ShareSheetChannelState.Done : ShareSheetChannelState.Failed);
        var label = button.Channel.Label ?? button.Channel.Id ?? string.Empty;
        Announce(Labels.ChannelAnnouncement(label, ok ? button.Channel.DoneLabel : Labels.ChannelFailed));
        if (ok)
        {
            RaiseEvent(new ShareSheetChannelEventArgs(ChannelSharedEvent, button.Channel));
        }
        DispatcherTimer.RunOnce(() =>
        {
            if (generation == button.Generation)
            {
                button.SetState(ShareSheetChannelState.Idle);
            }
        }, ShareSheetMotion.ChannelResult);
    }

    internal async Task SendAsync()
    {
        if (_isSending)
        {
            return;
        }
        var panel = _panelFace;
        if (_picked.Count == 0)
        {
            panel?.Shake();
            Announce(Labels.NobodyAnnouncement);
            return;
        }
        var submission = new ShareSheetSubmission(_picked.ToArray(), ChosenAccess);
        _isSending = true;
        panel?.SetError(null);
        panel?.UpdateSend(animate: true);
        try
        {
            if (SendHandler is { } handler)
            {
                await handler(submission, _lifetime.Token);
            }
        }
        catch (OperationCanceledException)
        {
            _isSending = false;
            panel?.UpdateSend(animate: true);
            return;
        }
        catch (Exception)
        {
            _isSending = false;
            panel?.UpdateSend(animate: true);
            panel?.SetError(Labels.SendFailed);
            Announce(Labels.SendFailed);
            return;
        }
        _isSending = false;
        panel?.UpdateSend(animate: false);
        _lastSent = submission;
        RaiseEvent(new ShareSheetSentEventArgs(SentEvent, submission));
        if (IsOpen && _mode != OpenMode.None)
        {
            ShowSent(submission);
        }
        else
        {
            ResetAfterSend();
        }
    }

    private void ShowSent(ShareSheetSubmission submission)
    {
        var sent = EnsureSentFace();
        sent.Show(submission);
        var stage = ActiveStage;
        if (stage is null)
        {
            return;
        }
        _panelFace?.LandFlights();
        _panelFace?.SetAccessListOpen(false, focusList: false);
        if (_mode == OpenMode.Floating)
        {
            sent.Width = FitWidth(Token(ShareSheetTokenKind.SentWidth, 304));
        }
        else
        {
            sent.Width = double.NaN;
        }
        stage.Show(sent, ShareSheetFace.Sent, Token(ShareSheetTokenKind.SentRadius, 26), ShareSheetFaceSwap.Slide, 1, animate: true);
        Announce(Labels.SentTitle(submission.People));
        FocusSoon(() => _sentFace?.Done);
    }

    /// <summary>Undo on the confirmation: back to the panel with the same people, and the app is told.</summary>
    internal void UndoSend()
    {
        if (_lastSent is not { } submission || ActiveStage is not { } stage || _panelFace is null)
        {
            return;
        }
        _lastSent = null;
        stage.Show(_panelFace, ShareSheetFace.Panel, Token(ShareSheetTokenKind.PanelRadius, 28), ShareSheetFaceSwap.Slide, -1, animate: true);
        Announce(Labels.UndoneAnnouncement);
        RaiseEvent(new ShareSheetSentEventArgs(SendUndoneEvent, submission));
        FocusSoon(() => _panelFace?.Copy is { IsEffectivelyVisible: true } copy ? copy : FirstFocusable(_panelFace));
    }

    /// <summary>Done on the confirmation: folds back (the picks are cleared once it is closed).</summary>
    internal void FinishSent()
    {
        if (_mode == OpenMode.Sheet)
        {
            _presence?.Dismiss(DismissReason.Action);
            return;
        }
        SetClosed(focusTrigger: true);
    }

    private void ResetAfterSend()
    {
        _lastSent = null;
        _picked.Clear();
        _panelFace?.SetError(null);
        _panelFace?.SyncChips(animate: false);
    }

    internal void ChooseAccess(ShareSheetAccess access) => SetCurrentValue(AccessProperty, access.Value);

    #endregion

    private void Announce(string message)
    {
        if (_status is null)
        {
            return;
        }
        // The same words twice in a row are still announced: clear first.
        _status.Text = string.Empty;
        _status.Text = message;
    }

    private void OnPeopleChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _picked.RemoveAll(p => !People.Contains(p));
        _panelFace?.RebuildPeople();
        _panelFace?.SyncChips(animate: false);
    }

    private void OnAccessLevelsChanged(object? sender, NotifyCollectionChangedEventArgs e) => _panelFace?.RebuildAccess();

    private void OnChannelsChanged(object? sender, NotifyCollectionChangedEventArgs e) => _panelFace?.RebuildChannels();
}
