using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using TKCode.Arc.Localization;

using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The view of a <see cref="DockGroup"/>: a card with the tab strip (and its overflow and menu buttons) on top and the
/// active pane below. Only the active pane is in the visual tree; the others keep their instances. Built and reused by
/// <see cref="DockLayout"/>; it follows the group's panes and active pane.
/// </summary>
[TemplatePart(StripPart, typeof(DockTabStrip))]
[TemplatePart(ContentPart, typeof(Decorator))]
[TemplatePart(OverflowPart, typeof(AvaloniaButton))]
[TemplatePart(MorePart, typeof(AvaloniaButton))]
[PseudoClasses(ActivePseudoClass, DocumentPseudoClass, DropTargetPseudoClass)]
internal sealed class DockGroupView : TemplatedControl, IDockView
{
    public const string StripPart = "PART_Strip";
    public const string ContentPart = "PART_Content";
    public const string OverflowPart = "PART_Overflow";
    public const string MorePart = "PART_More";
    public const string ActivePseudoClass = ":active";
    public const string DocumentPseudoClass = ":document";
    public const string DropTargetPseudoClass = ":drop-target";

    private readonly Dictionary<DockPane, DockTab> _tabs = new();
    private DockTabStrip? _strip;
    private Decorator? _content;
    private AvaloniaButton? _overflow;
    private AvaloniaButton? _more;
    private DockDragGesture? _drag;

    public DockGroupView(DockLayout layout, DockGroup group)
    {
        Layout = layout;
        Group = group;
        group.Panes.CollectionChanged += OnPanesChanged;
        group.PropertyChanged += OnGroupPropertyChanged;
        AddHandler(DoubleTappedEvent, OnDoubleTapped);
        SyncTabs();
        UpdateDocument();
    }

    /// <inheritdoc />
    public void Release()
    {
        Group.Panes.CollectionChanged -= OnPanesChanged;
        Group.PropertyChanged -= OnGroupPropertyChanged;
        foreach (var tab in _tabs.Values)
        {
            tab.Release();
        }
        _drag?.Dispose();
        _drag = null;
        _strip?.SetTabs([]);
        if (_content is not null)
        {
            _content.Child = null;
        }
    }

    /// <summary>A double click on the strip's empty part maximizes the group or restores the layout (spec 5.3).</summary>
    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Handled || Group.Float is not null || e.Source is not Visual source)
        {
            return;
        }
        // The strip has no background of its own: judge by where the pointer is, not by what it hit.
        if (_strip?.GetVisualParent() is not Visual row || !new Rect(row.Bounds.Size).Contains(e.GetPosition(row)))
        {
            return;
        }
        for (var node = (Visual?)source; node is not null && !ReferenceEquals(node, row); node = node.GetVisualParent())
        {
            if (node is DockTab or AvaloniaButton)
            {
                return;
            }
        }
        e.Handled = true;
        Layout.ToggleMaximize(Group);
    }

    public DockLayout Layout { get; }

    public DockGroup Group { get; }

    /// <summary>The tab strip (tests).</summary>
    internal DockTabStrip? Strip => _strip;

    /// <summary>The tab of a pane, or null.</summary>
    internal DockTab? TabOf(DockPane pane) => _tabs.GetValueOrDefault(pane);

    /// <summary>The tabs in order.</summary>
    internal List<DockTab> Tabs => Group.Panes.Select(p => _tabs[p]).ToList();

    internal AvaloniaButton? OverflowButton => _overflow;

    internal AvaloniaButton? MoreButton => _more;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_strip is not null)
        {
            _strip.OverflowChanged -= OnOverflowChanged;
            _strip.SetTabs([]);
        }
        if (_overflow is not null)
        {
            _overflow.Click -= OnOverflowClick;
        }
        if (_more is not null)
        {
            _more.Click -= OnMoreClick;
        }
        if (_content is not null)
        {
            _content.Child = null;
        }
        _drag?.Dispose();
        _strip = e.NameScope.Find<DockTabStrip>(StripPart);
        // Tabs drag their pane; the strip's empty part drags the whole group (spec 9.1).
        _drag = _strip is null ? null : new DockDragGesture(_strip, Layout, p => Layout.ResolveStripPress(this, p));
        _content = e.NameScope.Find<Decorator>(ContentPart);
        _overflow = e.NameScope.Find<AvaloniaButton>(OverflowPart);
        _more = e.NameScope.Find<AvaloniaButton>(MorePart);
        if (_strip is not null)
        {
            _strip.OverflowChanged += OnOverflowChanged;
        }
        if (_overflow is not null)
        {
            _overflow.Click += OnOverflowClick;
            AutomationProperties.SetName(_overflow, DockText.Get(DockLangResourceKind.MoreTabs));
        }
        if (_more is not null)
        {
            _more.Click += OnMoreClick;
            AutomationProperties.SetName(_more, DockText.Get(DockLangResourceKind.MoreActions));
        }
        SyncTabs();
        UpdateContent();
        UpdateActive();
    }

    private void OnPanesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SyncTabs();
        UpdateContent();
    }

    private void OnGroupPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == DockGroup.ActivePaneProperty)
        {
            UpdateSelection();
            UpdateContent();
        }
        else if (e.Property == DockGroup.IsDocumentGroupProperty)
        {
            UpdateDocument();
        }
    }

    private void SyncTabs()
    {
        var panes = Group.Panes;
        foreach (var (pane, tab) in _tabs.ToList())
        {
            if (!panes.Contains(pane))
            {
                tab.Release();
                _tabs.Remove(pane);
            }
        }
        var list = new List<DockTab>(panes.Count);
        foreach (var pane in panes)
        {
            if (!_tabs.TryGetValue(pane, out var tab))
            {
                tab = new DockTab(this, pane);
                tab.SetDocument(Group.IsDocumentGroup);
                _tabs[pane] = tab;
            }
            list.Add(tab);
        }
        _strip?.SetTabs(list);
        UpdateSelection();
    }

    private void UpdateSelection()
    {
        var active = Group.ActivePane;
        DockTab? selected = null;
        foreach (var (pane, tab) in _tabs)
        {
            var isActive = ReferenceEquals(pane, active);
            tab.IsSelected = isActive;
            if (isActive)
            {
                selected = tab;
            }
        }
        if (_strip is not null)
        {
            _strip.SelectedTab = selected;
        }
    }

    private void UpdateContent()
    {
        if (_content is null)
        {
            return;
        }
        var pane = Group.ActivePane;
        if (ReferenceEquals(_content.Child, pane))
        {
            return;
        }
        if (pane is not null)
        {
            DockLayout.Adopt(pane);
            Layout.FlushForeignLayouts(this);
        }
        _content.Child = pane;
    }

    private void UpdateDocument()
    {
        var document = Group.IsDocumentGroup;
        PseudoClasses.Set(DocumentPseudoClass, document);
        foreach (var tab in _tabs.Values)
        {
            tab.SetDocument(document);
        }
    }

    /// <summary>Follows the layout's active pane: <c>:active</c> and the accent pill when this group holds it.</summary>
    internal void UpdateActive()
    {
        var active = Layout.ActivePane is { } pane && ReferenceEquals(pane.Group, Group);
        PseudoClasses.Set(ActivePseudoClass, active);
        _strip?.SetGroupActive(active);
        foreach (var tab in _tabs.Values)
        {
            tab.SetGroupActive(active);
        }
    }

    private void OnOverflowChanged(object? sender, EventArgs e)
    {
        if (_overflow is not null && _strip is not null)
        {
            _overflow.IsVisible = _strip.IsOverflowing;
        }
    }

    internal void OnTabPressed(DockTab tab)
    {
        Layout.Select(tab.Pane);
        if (!tab.IsKeyboardFocusWithin)
        {
            tab.Focus(NavigationMethod.Pointer);
        }
    }

    internal void OnTabFocused(DockTab tab) => Layout.Select(tab.Pane);

    /// <summary>Marks the group the drag would dock into (<c>:drop-target</c>).</summary>
    internal void SetDropTarget(bool target) => PseudoClasses.Set(DropTargetPseudoClass, target);

    internal void OnTabKeyDown(DockTab tab, KeyEventArgs e)
    {
        var tabs = Tabs;
        var index = tabs.IndexOf(tab);
        if (index < 0)
        {
            return;
        }
        var modifiers = e.KeyModifiers;
        var command = (modifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0;
        var shift = (modifiers & KeyModifiers.Shift) != 0;
        switch (e.Key)
        {
            case Key.Left or Key.Right when command && shift:
                e.Handled = true;
                Layout.MoveTab(tab.Pane, e.Key == Key.Left ? -1 : 1);
                return;
            case Key.Left when modifiers == KeyModifiers.None:
                e.Handled = true;
                FocusTab(tabs[(index - 1 + tabs.Count) % tabs.Count]);
                return;
            case Key.Right when modifiers == KeyModifiers.None:
                e.Handled = true;
                FocusTab(tabs[(index + 1) % tabs.Count]);
                return;
            case Key.Home when modifiers == KeyModifiers.None:
                e.Handled = true;
                FocusTab(tabs[0]);
                return;
            case Key.End when modifiers == KeyModifiers.None:
                e.Handled = true;
                FocusTab(tabs[^1]);
                return;
            case Key.Delete when modifiers == KeyModifiers.None:
                e.Handled = true;
                Layout.Close(tab.Pane);
                return;
            case Key.Apps:
            case Key.F10 when shift:
                e.Handled = true;
                Layout.ShowPaneMenu(tab.Pane, tab, null);
                return;
        }
    }

    private static void FocusTab(DockTab tab) => tab.Focus(NavigationMethod.Directional);

    private void OnOverflowClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (_overflow is not null)
        {
            Layout.ShowTabList(this, _overflow);
        }
    }

    private void OnMoreClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (_more is not null && Group.ActivePane is { } pane)
        {
            Layout.ShowPaneMenu(pane, _more, null, MenuOpenFocus.Surface);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new DockGroupViewAutomationPeer(this);

    private sealed class DockGroupViewAutomationPeer : ControlAutomationPeer
    {
        public DockGroupViewAutomationPeer(DockGroupView owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Tab;
    }
}
