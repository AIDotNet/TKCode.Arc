using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Metadata;
using Avalonia.VisualTree;
using TKCode.Arc.Localization;

namespace TKCode.Arc;

/// <summary>
/// An IDE-style dock layout: panes (<see cref="DockPane"/>) sit in tab groups, groups are nested in horizontal and vertical
/// splits with draggable dividers, and documents live in a central <see cref="DockDocumentArea"/>. Each group is a rounded
/// card with 6px gaps between cards. Panes move with <see cref="Move"/> or the tab menu (Move to, Dock to), close from their
/// tab, menu or keyboard, and the whole layout saves to JSON (<see cref="SaveLayout"/>) and loads back
/// (<see cref="LoadLayout"/>). Write the tree as the content in XAML, or build a <see cref="DockWorkspace"/> in code.
/// </summary>
[TemplatePart(HostPart, typeof(Panel))]
[TemplatePart(AnnouncerPart, typeof(TextBlock))]
[TemplatePart(FloatsPart, typeof(Canvas))]
[TemplatePart(IndicatorsPart, typeof(DockIndicatorLayer))]
[PseudoClasses(LockedPseudoClass, DraggingPseudoClass, MaximizedPseudoClass)]
public partial class DockLayout : TemplatedControl
{
    public const string HostPart = "PART_Host";
    public const string AnnouncerPart = "PART_Announcer";
    public const string LockedPseudoClass = ":locked";

    /// <summary>The layout model. Replace it to show another layout; never null.</summary>
    public static readonly DirectProperty<DockLayout, DockWorkspace> WorkspaceProperty =
        AvaloniaProperty.RegisterDirect<DockLayout, DockWorkspace>(nameof(Workspace), o => o.Workspace, (o, v) => o.Workspace = v);

    /// <summary>
    /// The active pane: the one whose content last had focus or whose tab was last chosen. Two-way; setting it selects the
    /// pane's tab (use <see cref="Activate"/> to also move focus into it).
    /// </summary>
    public static readonly StyledProperty<DockPane?> ActivePaneProperty =
        AvaloniaProperty.Register<DockLayout, DockPane?>(nameof(ActivePane), defaultBindingMode: BindingMode.TwoWay,
            coerce: CoerceActivePane);

    /// <summary>Locks the layout: panes cannot be moved, closed, floated or auto-hidden; the dividers still resize.</summary>
    public static readonly StyledProperty<bool> IsLockedProperty =
        AvaloniaProperty.Register<DockLayout, bool>(nameof(IsLocked));

    /// <summary>
    /// Supplies panes while a saved layout loads, for ids that no pane of this layout has had (declared in XAML or opened
    /// since). Return null to skip the id.
    /// </summary>
    public static readonly StyledProperty<Func<string, DockPane?>?> PaneResolverProperty =
        AvaloniaProperty.Register<DockLayout, Func<string, DockPane?>?>(nameof(PaneResolver));

    /// <summary>What the document area shows while no document is open; by default an empty state that reads "No open documents".</summary>
    public static readonly StyledProperty<object?> EmptyDocumentContentProperty =
        AvaloniaProperty.Register<DockLayout, object?>(nameof(EmptyDocumentContent));

    /// <summary>Raised before a pane closes; set <see cref="DockPaneClosingEventArgs.Cancel"/> to keep it open.</summary>
    public static readonly RoutedEvent<DockPaneClosingEventArgs> PaneClosingEvent =
        RoutedEvent.Register<DockLayout, DockPaneClosingEventArgs>(nameof(PaneClosing), RoutingStrategies.Bubble);

    /// <summary>Raised after a pane closed.</summary>
    public static readonly RoutedEvent<DockPaneEventArgs> PaneClosedEvent =
        RoutedEvent.Register<DockLayout, DockPaneEventArgs>(nameof(PaneClosed), RoutingStrategies.Bubble);

    /// <summary>Raised after any change of the layout (move, close, open, resize, activate, load); handy for saving it automatically.</summary>
    public static readonly RoutedEvent<DockLayoutChangedEventArgs> LayoutChangedEvent =
        RoutedEvent.Register<DockLayout, DockLayoutChangedEventArgs>(nameof(LayoutChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when <see cref="ActivePane"/> changes.</summary>
    public static readonly RoutedEvent<DockPaneEventArgs> ActivePaneChangedEvent =
        RoutedEvent.Register<DockLayout, DockPaneEventArgs>(nameof(ActivePaneChanged), RoutingStrategies.Bubble);

    private DockWorkspace _workspace;
    private Panel? _host;
    private TextBlock? _announcer;
    private bool _suppressAutoActive;
    private readonly DockCommand _closeActiveCommand;

    static DockLayout()
    {
        AffectsMeasure<DockLayout>(EmptyDocumentContentProperty);
        RegisterSourceHandlers();
        FloatingModeProperty.Changed.AddClassHandler<DockLayout>((layout, _) => layout.SyncFloats());
    }

    public DockLayout()
    {
        _workspace = new DockWorkspace();
        _workspace.Changed += OnWorkspaceChanged;
        _closeActiveCommand = new DockCommand(
            () =>
            {
                if (ActivePane is { } pane)
                {
                    Close(pane);
                }
            },
            () => !IsLocked && ActivePane is { CanClose: true });
        AddHandler(GotFocusEvent, OnAnyGotFocus, RoutingStrategies.Bubble, handledEventsToo: true);
        InitializeExtras();
    }

    /// <inheritdoc cref="WorkspaceProperty"/>
    public DockWorkspace Workspace
    {
        get => _workspace;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (ReferenceEquals(value, _workspace))
            {
                return;
            }
            CancelDrag();
            var old = _workspace;
            old.Changed -= OnWorkspaceChanged;
            foreach (var node in old.Root?.SelfAndDescendants() ?? [])
            {
                if (node.View is IDockView { } view && ReferenceEquals(view.Layout, this))
                {
                    view.Release();
                    node.View = null;
                }
            }
            SetAndRaise(WorkspaceProperty, ref _workspace, value);
            value.Changed += OnWorkspaceChanged;
            OnWorkspaceReplaced(old);
            UpdateRootView();
            SyncFloats();
            CoerceValue(ActivePaneProperty);
        }
    }

    /// <summary>The layout tree, forwarded to <see cref="DockWorkspace.Root"/>: write it as the content in XAML.</summary>
    [Content]
    public DockNode? Root
    {
        get => _workspace.Root;
        set => _workspace.Root = value;
    }

    /// <inheritdoc cref="ActivePaneProperty"/>
    public DockPane? ActivePane { get => GetValue(ActivePaneProperty); set => SetValue(ActivePaneProperty, value); }

    /// <inheritdoc cref="IsLockedProperty"/>
    public bool IsLocked { get => GetValue(IsLockedProperty); set => SetValue(IsLockedProperty, value); }

    /// <inheritdoc cref="PaneResolverProperty"/>
    public Func<string, DockPane?>? PaneResolver { get => GetValue(PaneResolverProperty); set => SetValue(PaneResolverProperty, value); }

    /// <inheritdoc cref="EmptyDocumentContentProperty"/>
    public object? EmptyDocumentContent { get => GetValue(EmptyDocumentContentProperty); set => SetValue(EmptyDocumentContentProperty, value); }

    /// <summary>Closes the active pane (when it can close and the layout is not locked); bind it to a menu or key.</summary>
    public ICommand CloseActiveCommand => _closeActiveCommand;

    /// <inheritdoc cref="PaneClosingEvent"/>
    public event EventHandler<DockPaneClosingEventArgs>? PaneClosing
    {
        add => AddHandler(PaneClosingEvent, value);
        remove => RemoveHandler(PaneClosingEvent, value);
    }

    /// <inheritdoc cref="PaneClosedEvent"/>
    public event EventHandler<DockPaneEventArgs>? PaneClosed
    {
        add => AddHandler(PaneClosedEvent, value);
        remove => RemoveHandler(PaneClosedEvent, value);
    }

    /// <inheritdoc cref="LayoutChangedEvent"/>
    public event EventHandler<DockLayoutChangedEventArgs>? LayoutChanged
    {
        add => AddHandler(LayoutChangedEvent, value);
        remove => RemoveHandler(LayoutChangedEvent, value);
    }

    /// <inheritdoc cref="ActivePaneChangedEvent"/>
    public event EventHandler<DockPaneEventArgs>? ActivePaneChanged
    {
        add => AddHandler(ActivePaneChangedEvent, value);
        remove => RemoveHandler(ActivePaneChangedEvent, value);
    }

    /// <summary>The last announcement for screen readers (tests).</summary>
    internal string? LastAnnouncement => _announcer?.Text;

    /// <summary>The panel that holds the root view (tests).</summary>
    internal Panel? Host => _host;

    #region Views

    /// <summary>The view of a node, made once and reused when the node moves.</summary>
    internal Control ViewFor(DockNode node)
    {
        if (node.View is IDockView { } existing && ReferenceEquals(existing.Layout, this))
        {
            return node.View;
        }
        Control view = node switch
        {
            DockSplit split => new DockSplitView(this, split),
            DockGroup group => new DockGroupView(this, group),
            DockDocumentArea area => new DockDocumentAreaView(this, area),
            _ => throw new ArgumentOutOfRangeException(nameof(node))
        };
        node.View = view;
        _viewNodes.Add(node);
        return view;
    }

    /// <summary>
    /// Takes a view or pane out of wherever it is shown, so it can be shown elsewhere in the same frame (a control has one
    /// parent; moving it never rebuilds it).
    /// </summary>
    internal static void Adopt(Control control)
    {
        switch (control.Parent)
        {
            case null:
                break;
            case ContentControl owner when ReferenceEquals(owner.Content, control):
                owner.Content = null;
                break;
            case Decorator decorator when ReferenceEquals(decorator.Child, control):
                decorator.Child = null;
                break;
            case Panel panel:
                panel.Children.Remove(control);
                break;
        }
        if (control.GetVisualParent() is ContentPresenter presenter && ReferenceEquals(presenter.Content, control))
        {
            presenter.Content = null;
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _host?.Children.Clear();
        _host = e.NameScope.Find<Panel>(HostPart);
        _announcer = e.NameScope.Find<TextBlock>(AnnouncerPart);
        ApplyExtraParts(e.NameScope);
        ApplyDragTemplate(e);
        UpdateRootView();
        ApplyFloatingTemplate(e);
    }

    private void UpdateRootView()
    {
        if (_host is null || !_listening)
        {
            return;
        }
        var root = _workspace.Root;
        var view = MaximizedGroup is { } maximized ? ViewFor(maximized) : root is null ? null : ViewFor(root);
        if (_host.Children.Count == 1 && ReferenceEquals(_host.Children[0], view))
        {
            return;
        }
        _host.Children.Clear();
        if (view is not null)
        {
            Adopt(view);
            _host.Children.Add(view);
        }
        UpdateActiveViews();
    }

    private void UpdateActiveViews()
    {
        foreach (var group in _workspace.Groups())
        {
            if (group.View is DockGroupView view && ReferenceEquals(view.Layout, this))
            {
                view.UpdateActive();
            }
        }
    }

    private void RefreshTabs()
    {
        foreach (var group in _workspace.Groups())
        {
            if (group.View is DockGroupView view && ReferenceEquals(view.Layout, this))
            {
                foreach (var tab in view.Tabs)
                {
                    tab.Refresh();
                }
            }
        }
    }

    private void OnWorkspaceChanged(object? sender, EventArgs e)
    {
        UpdateRootView();
        SyncFloats();
        if (!_suppressAutoActive && ActivePane is { } active && !ReferenceEquals(active.Workspace, _workspace))
        {
            SetCurrentValue(ActivePaneProperty, FallbackActivePane());
        }
        UpdateActiveViews();
        _closeActiveCommand.RaiseCanExecuteChanged();
        OnWorkspaceChangedExtras();
    }

    private DockPane? FallbackActivePane()
        => _workspace.RecentDocumentGroup?.ActivePane
           ?? _workspace.Root?.SelfAndDescendants().OfType<DockGroup>().Select(g => g.ActivePane).FirstOrDefault(p => p is not null);

    #endregion

    #region Activation and focus

    private static DockPane? CoerceActivePane(AvaloniaObject sender, DockPane? value)
    {
        var layout = (DockLayout)sender;
        return value is not null && ReferenceEquals(value.Workspace, layout._workspace) ? value : null;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ActivePaneProperty)
        {
            var old = change.GetOldValue<DockPane?>();
            var pane = change.GetNewValue<DockPane?>();
            old?.SetIsActive(false);
            if (pane is not null)
            {
                pane.SetIsActive(true);
                if (pane.Group is { } group)
                {
                    group.ActivePane = pane;
                    if (group.IsDocumentGroup)
                    {
                        _workspace.RecentDocumentGroup = group;
                    }
                }
            }
            UpdateActiveViews();
            _closeActiveCommand.RaiseCanExecuteChanged();
            OnActivePaneChangedExtras(old, pane);
            RaiseEvent(new DockPaneEventArgs(ActivePaneChangedEvent, pane));
            RaiseLayoutChanged(DockLayoutChangeKind.Activate, pane);
        }
        else if (change.Property == IsLockedProperty)
        {
            PseudoClasses.Set(LockedPseudoClass, change.GetNewValue<bool>());
            CancelDrag();
            RefreshTabs();
            _closeActiveCommand.RaiseCanExecuteChanged();
            OnLockedChangedExtras();
        }
        else if (change.Property == EmptyDocumentContentProperty)
        {
            if (_workspace.DocumentArea?.View is DockDocumentAreaView view && ReferenceEquals(view.Layout, this))
            {
                view.Sync();
            }
        }
    }

    /// <summary>Selects a pane's tab and makes it the active pane, without moving focus.</summary>
    internal void Select(DockPane pane)
    {
        if (!ReferenceEquals(pane.Workspace, _workspace))
        {
            return;
        }
        if (pane.Group is { } group)
        {
            group.ActivePane = pane;
        }
        ActivePane = pane;
    }

    /// <summary>
    /// Activates a pane: selects its tab, makes it the <see cref="ActivePane"/> and moves keyboard focus into its content
    /// (to its tab when the content has nothing focusable).
    /// </summary>
    public void Activate(DockPane pane)
    {
        ArgumentNullException.ThrowIfNull(pane);
        Select(pane);
        if (!ReferenceEquals(pane.Workspace, _workspace))
        {
            return;
        }
        BringFloatToFront(pane);
        var target = pane.GetVisualDescendants().OfType<InputElement>()
            .FirstOrDefault(e => e.Focusable && e.IsEffectivelyEnabled && e.IsEffectivelyVisible);
        if (target is not null)
        {
            target.Focus(NavigationMethod.Tab);
        }
        else if (!FocusTab(pane, NavigationMethod.Tab))
        {
            FocusAutoHideCard(pane);
        }
    }

    /// <summary>Focuses a pane's tab; false when the tab is not shown.</summary>
    internal bool FocusTab(DockPane pane, NavigationMethod method = NavigationMethod.Tab)
        => pane.Group?.View is DockGroupView view && view.TabOf(pane) is { } tab && tab.Focus(method);

    private void OnAnyGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (e.Source is not Visual visual)
        {
            return;
        }
        var pane = visual as DockPane ?? visual.FindAncestorOfType<DockPane>();
        if (pane is not null && ReferenceEquals(pane.Workspace, _workspace) && !ReferenceEquals(pane, ActivePane))
        {
            Select(pane);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
        {
            return;
        }
        var command = (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0;
        var closeKey = (e.Key == Key.F4 && e.KeyModifiers == KeyModifiers.Control)
                       || (e.Key == Key.W && e.KeyModifiers == KeyModifiers.Meta);
        if (closeKey && command && ActivePane is { } pane && IsInsidePane(e.Source, pane))
        {
            e.Handled = true;
            Close(pane);
        }
    }

    private static bool IsInsidePane(object? source, DockPane pane)
    {
        if (source is not Visual visual)
        {
            return false;
        }
        if (visual is DockTab tab)
        {
            return ReferenceEquals(tab.Pane, pane);
        }
        return ReferenceEquals(visual, pane) || pane.IsVisualAncestorOf(visual);
    }

    #endregion

    #region Announcements and events

    /// <summary>Tells screen readers about a layout change (polite live region).</summary>
    internal void Announce(string text)
    {
        if (_announcer is null)
        {
            return;
        }
        _announcer.Text = text;
        AutomationProperties.SetName(_announcer, text);
    }

    private void RaiseLayoutChanged(DockLayoutChangeKind kind, DockPane? pane)
        => RaiseEvent(new DockLayoutChangedEventArgs(LayoutChangedEvent, kind, pane));

    /// <summary>A divider was moved (from a split view).</summary>
    internal void OnResized() => RaiseLayoutChanged(DockLayoutChangeKind.Resize, null);

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new DockLayoutAutomationPeer(this);

    private sealed class DockLayoutAutomationPeer : ControlAutomationPeer
    {
        public DockLayoutAutomationPeer(DockLayout owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string? GetNameCore()
            => base.GetNameCore() is { Length: > 0 } name ? name : DockText.Get(DockLangResourceKind.LayoutName);
    }
}

/// <summary>A view made by a <see cref="DockLayout"/> for a model node.</summary>
internal interface IDockView
{
    DockLayout Layout { get; }

    /// <summary>Stops following the model (the layout let go of the node's workspace).</summary>
    void Release();
}

/// <summary>A command that runs an action and asks a predicate whether it can.</summary>
internal sealed class DockCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool> _canExecute;

    public DockCommand(Action execute, Func<bool> canExecute)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute();

    public void Execute(object? parameter)
    {
        if (_canExecute())
        {
            _execute();
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
