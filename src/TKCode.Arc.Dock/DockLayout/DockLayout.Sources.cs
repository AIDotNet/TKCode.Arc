using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Utilities;

namespace TKCode.Arc;

public partial class DockLayout
{
    /// <summary>
    /// Data items that each become a document pane in the document area. Adding an item opens its pane, removing
    /// it closes the pane; when the user closes a tab and the collection is a writable list, the item is removed from it.
    /// </summary>
    public static readonly StyledProperty<IEnumerable?> DocumentsSourceProperty =
        AvaloniaProperty.Register<DockLayout, IEnumerable?>(nameof(DocumentsSource));

    /// <summary>
    /// Builds a document item's pane. A template that produces a <see cref="DockPane"/> is used as it is; anything else is
    /// wrapped in a pane. The pane's <c>DataContext</c> is the item; without an <c>Id</c> or <c>Title</c> it takes them from
    /// <see cref="IDockItem"/> or the item's <c>ToString()</c>.
    /// </summary>
    public static readonly StyledProperty<IDataTemplate?> DocumentTemplateProperty =
        AvaloniaProperty.Register<DockLayout, IDataTemplate?>(nameof(DocumentTemplate));

    /// <summary>
    /// Data items that each become a tool pane, placed by its <see cref="DockPane.PreferredSide"/> (left when the template
    /// sets none). Same rules as <see cref="DocumentsSource"/>.
    /// </summary>
    public static readonly StyledProperty<IEnumerable?> ToolsSourceProperty =
        AvaloniaProperty.Register<DockLayout, IEnumerable?>(nameof(ToolsSource));

    /// <summary>Builds a tool item's pane; same rules as <see cref="DocumentTemplate"/>.</summary>
    public static readonly StyledProperty<IDataTemplate?> ToolTemplateProperty =
        AvaloniaProperty.Register<DockLayout, IDataTemplate?>(nameof(ToolTemplate));

    private DockItemsSource? _documentsSource;
    private DockItemsSource? _toolsSource;
    private int _initDepth;

    /// <inheritdoc cref="DocumentsSourceProperty"/>
    public IEnumerable? DocumentsSource { get => GetValue(DocumentsSourceProperty); set => SetValue(DocumentsSourceProperty, value); }

    /// <inheritdoc cref="DocumentTemplateProperty"/>
    public IDataTemplate? DocumentTemplate { get => GetValue(DocumentTemplateProperty); set => SetValue(DocumentTemplateProperty, value); }

    /// <inheritdoc cref="ToolsSourceProperty"/>
    public IEnumerable? ToolsSource { get => GetValue(ToolsSourceProperty); set => SetValue(ToolsSourceProperty, value); }

    /// <inheritdoc cref="ToolTemplateProperty"/>
    public IDataTemplate? ToolTemplate { get => GetValue(ToolTemplateProperty); set => SetValue(ToolTemplateProperty, value); }

    private DockItemsSource Documents => _documentsSource ??= new DockItemsSource(this, tools: false);

    private DockItemsSource Tools => _toolsSource ??= new DockItemsSource(this, tools: true);

    private static void RegisterSourceHandlers()
    {
        DocumentsSourceProperty.Changed.AddClassHandler<DockLayout>((layout, _) => layout.SyncSource(layout.Documents));
        DocumentTemplateProperty.Changed.AddClassHandler<DockLayout>((layout, _) => layout.SyncSource(layout.Documents));
        ToolsSourceProperty.Changed.AddClassHandler<DockLayout>((layout, _) => layout.SyncSource(layout.Tools));
        ToolTemplateProperty.Changed.AddClassHandler<DockLayout>((layout, _) => layout.SyncSource(layout.Tools));
    }

    /// <summary>The pane made for a source item, or null (tests and hosts).</summary>
    internal DockPane? PaneForItem(object item)
        => _documentsSource?.PaneOf(item) ?? _toolsSource?.PaneOf(item);

    public override void BeginInit()
    {
        base.BeginInit();
        _initDepth++;
    }

    public override void EndInit()
    {
        base.EndInit();
        if (--_initDepth == 0)
        {
            // While XAML loads, the tree is set after the sources; panes are made once both are in.
            if (_documentsSource?.IsPending == true)
            {
                SyncSource(_documentsSource);
            }
            if (_toolsSource?.IsPending == true)
            {
                SyncSource(_toolsSource);
            }
        }
    }

    private void SyncSource(DockItemsSource source)
    {
        if (_initDepth > 0)
        {
            source.IsPending = true;
            return;
        }
        source.IsPending = false;
        source.Reset(ReferenceEquals(source, _documentsSource) ? DocumentsSource : ToolsSource,
            ReferenceEquals(source, _documentsSource) ? DocumentTemplate : ToolTemplate);
    }

    /// <summary>A user close of a pane made from a writable list removes its item; the list's change then closes the pane.</summary>
    private bool CloseThroughSource(DockPane pane)
        => pane.SourceItem is not null
           && ((_documentsSource?.TryRemoveItem(pane) ?? false) || (_toolsSource?.TryRemoveItem(pane) ?? false));

    /// <summary>After the workspace was replaced, the source panes move from the old one into the new one.</summary>
    private void MoveSourcePanes(DockWorkspace old)
    {
        _documentsSource?.MoveTo(old);
        _toolsSource?.MoveTo(old);
    }

    /// <summary>Opens a pane made for a new source item (documents join the recent document group, tools go by their side).</summary>
    internal void OpenSourcePane(DockPane pane, bool select)
    {
        if (_workspace.FindPane(pane.Id) is not null)
        {
            throw new InvalidOperationException($"A pane with the Id '{pane.Id}' is already in the layout; give every source item a unique id.");
        }
        _workspace.PlaceByPreference(pane, EdgeShareFor(pane));
        if (select)
        {
            Select(pane);
        }
        RaiseLayoutChanged(DockLayoutChangeKind.Open, pane);
    }

    /// <summary>Closes a pane whose source item went away (no <see cref="PaneClosing"/>) and forgets it.</summary>
    internal void CloseSourcePane(DockPane pane)
    {
        if (ReferenceEquals(pane.Workspace, _workspace))
        {
            FinishClose(pane);
        }
        _workspace.Forget(pane);
        _recent.Remove(pane);
    }
}

/// <summary>
/// One of the layout's item sources: the pane made for each item (in source order), kept in step with the collection through
/// a weak subscription (the collection usually outlives the layout).
/// </summary>
internal sealed class DockItemsSource : IWeakEventSubscriber<NotifyCollectionChangedEventArgs>, IWeakEventSubscriber<PropertyChangedEventArgs>
{
    private readonly DockLayout _layout;
    private readonly bool _tools;
    private readonly List<Entry> _entries = [];
    private IEnumerable? _source;
    private IDataTemplate? _template;

    public DockItemsSource(DockLayout layout, bool tools)
    {
        _layout = layout;
        _tools = tools;
    }

    /// <summary>Set while XAML is loading: the panes are made at the end.</summary>
    public bool IsPending { get; set; }

    public DockPane? PaneOf(object item) => _entries.FirstOrDefault(e => Equals(e.Item, item))?.Pane;

    /// <summary>Replaces the source or template: the old panes close, a pane is made for each item.</summary>
    public void Reset(IEnumerable? source, IDataTemplate? template)
    {
        if (_source is INotifyCollectionChanged oldNotify)
        {
            WeakEvents.CollectionChanged.Unsubscribe(oldNotify, this);
        }
        foreach (var entry in _entries.ToList())
        {
            Remove(entry);
        }
        _source = source;
        _template = template;
        if (source is null)
        {
            return;
        }
        if (source is INotifyCollectionChanged notify)
        {
            WeakEvents.CollectionChanged.Subscribe(notify, this);
        }
        // One tidy-up and one view update for the whole collection.
        var workspace = _layout.Workspace;
        workspace.BeginUpdate();
        try
        {
            foreach (var item in source)
            {
                if (item is not null)
                {
                    Add(item, select: false);
                }
            }
        }
        finally
        {
            workspace.EndUpdate();
        }
    }

    /// <summary>Removes the item of a pane the user closed from a writable list; false when the list cannot change.</summary>
    public bool TryRemoveItem(DockPane pane)
    {
        var entry = _entries.FirstOrDefault(e => ReferenceEquals(e.Pane, pane));
        if (entry is null || _source is not IList { IsReadOnly: false, IsFixedSize: false } list)
        {
            return false;
        }
        list.Remove(entry.Item);
        // A list without change notifications: close it here.
        if (_entries.Contains(entry))
        {
            Remove(entry);
        }
        return true;
    }

    /// <summary>Moves the open source panes from <paramref name="old"/> into the layout's new workspace.</summary>
    public void MoveTo(DockWorkspace old)
    {
        foreach (var entry in _entries)
        {
            if (ReferenceEquals(entry.Pane.Workspace, old))
            {
                old.Remove(entry.Pane);
                old.Forget(entry.Pane);
                _layout.OpenSourcePane(entry.Pane, select: false);
            }
        }
    }

    private void Add(object item, bool select)
    {
        var entry = new Entry(item, Create(item, out var titleFromItem)) { TitleFromItem = titleFromItem };
        _entries.Add(entry);
        if (item is INotifyPropertyChanged notify && titleFromItem && item is IDockItem)
        {
            WeakEvents.ThreadSafePropertyChanged.Subscribe(notify, this);
        }
        _layout.OpenSourcePane(entry.Pane, select);
    }

    private void Remove(Entry entry)
    {
        _entries.Remove(entry);
        if (entry.Item is INotifyPropertyChanged notify && entry.TitleFromItem && entry.Item is IDockItem
            && !_entries.Any(e => ReferenceEquals(e.Item, entry.Item)))
        {
            WeakEvents.ThreadSafePropertyChanged.Unsubscribe(notify, this);
        }
        _layout.CloseSourcePane(entry.Pane);
        entry.Pane.SourceItem = null;
        entry.Pane.ClearValue(StyledElement.DataContextProperty);
    }

    private DockPane Create(object item, out bool titleFromItem)
    {
        var built = _template is not null && _template.Match(item) ? _template.Build(item) : null;
        var pane = built as DockPane ?? new DockPane { Content = built ?? item };
        pane.DataContext = item;
        pane.SourceItem = item;
        if (string.IsNullOrEmpty(pane.Id))
        {
            pane.SetCurrentValue(DockPane.IdProperty, DockPane.IdOf(item));
        }
        titleFromItem = string.IsNullOrEmpty(pane.Title);
        if (titleFromItem)
        {
            pane.SetCurrentValue(DockPane.TitleProperty, DockPane.TitleOf(item));
        }
        if (_tools && pane.PreferredSide is null)
        {
            pane.SetCurrentValue(DockPane.PreferredSideProperty, DockSide.Left);
        }
        return pane;
    }

    void IWeakEventSubscriber<NotifyCollectionChangedEventArgs>.OnEvent(object? sender, WeakEvent ev, NotifyCollectionChangedEventArgs e)
    {
        if (!ReferenceEquals(sender, _source))
        {
            return;
        }
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add when e.NewItems is not null:
                foreach (var item in e.NewItems)
                {
                    if (item is not null)
                    {
                        Add(item, select: true);
                    }
                }
                break;
            case NotifyCollectionChangedAction.Remove when e.OldItems is not null:
                RemoveItems(e.OldItems);
                break;
            case NotifyCollectionChangedAction.Replace when e.OldItems is not null && e.NewItems is not null:
                RemoveItems(e.OldItems);
                foreach (var item in e.NewItems)
                {
                    if (item is not null)
                    {
                        Add(item, select: true);
                    }
                }
                break;
            case NotifyCollectionChangedAction.Move:
                break;
            default:
                Resync();
                break;
        }
    }

    void IWeakEventSubscriber<PropertyChangedEventArgs>.OnEvent(object? sender, WeakEvent ev, PropertyChangedEventArgs e)
    {
        if (sender is not IDockItem item || (!string.IsNullOrEmpty(e.PropertyName) && e.PropertyName != nameof(IDockItem.DockTitle)))
        {
            return;
        }
        foreach (var entry in _entries)
        {
            if (ReferenceEquals(entry.Item, sender) && entry.TitleFromItem)
            {
                entry.Pane.SetCurrentValue(DockPane.TitleProperty, item.DockTitle ?? string.Empty);
            }
        }
    }

    private void RemoveItems(IList items)
    {
        foreach (var item in items)
        {
            var entry = _entries.FirstOrDefault(e => Equals(e.Item, item));
            if (entry is not null)
            {
                Remove(entry);
            }
        }
    }

    /// <summary>After a reset: close the panes whose items left, open panes for new items, keep the rest.</summary>
    private void Resync()
    {
        var items = _source?.Cast<object?>().Where(i => i is not null).Cast<object>().ToList() ?? [];
        var remaining = new List<object>(items);
        foreach (var entry in _entries.ToList())
        {
            var at = remaining.FindIndex(i => Equals(i, entry.Item));
            if (at < 0)
            {
                Remove(entry);
            }
            else
            {
                remaining.RemoveAt(at);
            }
        }
        foreach (var item in remaining)
        {
            Add(item, select: false);
        }
    }

    private sealed class Entry
    {
        public Entry(object item, DockPane pane)
        {
            Item = item;
            Pane = pane;
        }

        public object Item { get; }

        public DockPane Pane { get; }

        public bool TitleFromItem { get; init; }
    }
}
