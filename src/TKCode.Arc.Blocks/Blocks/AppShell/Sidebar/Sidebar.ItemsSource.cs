using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;

namespace TKCode.Arc;

/// <summary>Data binding for <see cref="Sidebar"/>: <c>ItemsSource</c>, member names and a trailing template.</summary>
public partial class Sidebar
{
    /// <summary>
    /// The pages as data. Each item becomes a row in a header-less section placed before the sections declared in
    /// <see cref="Sections"/>. An item that is a <see cref="SidebarItem"/> (or a subclass with your own fields) is used
    /// as it is; any other object is mapped through the <c>*Member</c> names, and its object is kept in
    /// <see cref="SidebarItem.Tag"/>. Collection changes are followed when the source is an
    /// <see cref="INotifyCollectionChanged"/>.
    /// </summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<Sidebar, IEnumerable?>(nameof(ItemsSource));

    /// <summary>
    /// Builds the content at the end of each bound row. The control gets the item's data (the object in
    /// <see cref="ItemsSource"/>) as its <c>DataContext</c>, so a template can bind to your own fields, e.g. show an
    /// add button only while <c>CanAdd</c> is true. A <see cref="SidebarItem.TrailingIcon"/> that is already set wins.
    /// </summary>
    public static readonly StyledProperty<IDataTemplate?> TrailingTemplateProperty =
        AvaloniaProperty.Register<Sidebar, IDataTemplate?>(nameof(TrailingTemplate));

    /// <summary>The property path of an item's label (for items that are not <see cref="SidebarItem"/>s).</summary>
    public static readonly StyledProperty<string?> LabelMemberProperty =
        AvaloniaProperty.Register<Sidebar, string?>(nameof(LabelMember));

    /// <summary>The property path of an item's key, which <see cref="SelectedKey"/> matches against.</summary>
    public static readonly StyledProperty<string?> KeyMemberProperty =
        AvaloniaProperty.Register<Sidebar, string?>(nameof(KeyMember));

    /// <summary>The property path of an item's icon (a geometry or a control).</summary>
    public static readonly StyledProperty<string?> IconMemberProperty =
        AvaloniaProperty.Register<Sidebar, string?>(nameof(IconMember));

    /// <summary>The property path of an item's badge text.</summary>
    public static readonly StyledProperty<string?> BadgeMemberProperty =
        AvaloniaProperty.Register<Sidebar, string?>(nameof(BadgeMember));

    /// <summary>The property path of an item's child items (a collection of the same kind of data).</summary>
    public static readonly StyledProperty<string?> ChildrenMemberProperty =
        AvaloniaProperty.Register<Sidebar, string?>(nameof(ChildrenMember));

    /// <summary>The property path of an item's enabled state (a bool).</summary>
    public static readonly StyledProperty<string?> IsEnabledMemberProperty =
        AvaloniaProperty.Register<Sidebar, string?>(nameof(IsEnabledMember));

    /// <inheritdoc cref="ItemsSourceProperty"/>
    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <inheritdoc cref="TrailingTemplateProperty"/>
    public IDataTemplate? TrailingTemplate
    {
        get => GetValue(TrailingTemplateProperty);
        set => SetValue(TrailingTemplateProperty, value);
    }

    /// <inheritdoc cref="LabelMemberProperty"/>
    public string? LabelMember
    {
        get => GetValue(LabelMemberProperty);
        set => SetValue(LabelMemberProperty, value);
    }

    /// <inheritdoc cref="KeyMemberProperty"/>
    public string? KeyMember
    {
        get => GetValue(KeyMemberProperty);
        set => SetValue(KeyMemberProperty, value);
    }

    /// <inheritdoc cref="IconMemberProperty"/>
    public string? IconMember
    {
        get => GetValue(IconMemberProperty);
        set => SetValue(IconMemberProperty, value);
    }

    /// <inheritdoc cref="BadgeMemberProperty"/>
    public string? BadgeMember
    {
        get => GetValue(BadgeMemberProperty);
        set => SetValue(BadgeMemberProperty, value);
    }

    /// <inheritdoc cref="ChildrenMemberProperty"/>
    public string? ChildrenMember
    {
        get => GetValue(ChildrenMemberProperty);
        set => SetValue(ChildrenMemberProperty, value);
    }

    /// <inheritdoc cref="IsEnabledMemberProperty"/>
    public string? IsEnabledMember
    {
        get => GetValue(IsEnabledMemberProperty);
        set => SetValue(IsEnabledMemberProperty, value);
    }

    private sealed class ChildrenHolder : AvaloniaObject
    {
        public static readonly StyledProperty<IEnumerable?> ValueProperty =
            AvaloniaProperty.Register<ChildrenHolder, IEnumerable?>("Value");
    }

    private sealed class BoundEntry
    {
        public BoundEntry(SidebarItem item) => Item = item;

        public SidebarItem Item { get; }
        public bool Generated { get; set; }
        public List<IDisposable> Subscriptions { get; } = new();
        public ChildrenHolder? Holder { get; set; }
        public INotifyCollectionChanged? Watched { get; set; }
        public NotifyCollectionChangedEventHandler? Handler { get; set; }
    }

    private readonly Dictionary<object, BoundEntry> _bound = new(ReferenceEqualityComparer.Instance);
    private SidebarSection? _boundSection;
    private INotifyCollectionChanged? _watchedSource;
    private bool _boundSyncing;

    private void OnBoundSourceChanged(AvaloniaPropertyChangedEventArgs change)
    {
        var p = change.Property;
        if (p == ItemsSourceProperty)
        {
            if (_watchedSource is not null)
            {
                _watchedSource.CollectionChanged -= OnBoundSourceCollectionChanged;
                _watchedSource = null;
            }
            if (change.GetNewValue<IEnumerable?>() is INotifyCollectionChanged notify)
            {
                _watchedSource = notify;
                notify.CollectionChanged += OnBoundSourceCollectionChanged;
            }
            SyncBound(reset: true);
        }
        else if (p == TrailingTemplateProperty || p == LabelMemberProperty || p == KeyMemberProperty ||
                 p == IconMemberProperty || p == BadgeMemberProperty || p == ChildrenMemberProperty ||
                 p == IsEnabledMemberProperty)
        {
            SyncBound(reset: true);
        }
    }

    private void OnBoundSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => SyncBound(reset: false);

    /// <summary>Brings the generated section in step with <see cref="ItemsSource"/> (a reset drops every mapping first).</summary>
    private void SyncBound(bool reset)
    {
        if (_boundSyncing)
        {
            return;
        }
        _boundSyncing = true;
        try
        {
            if (reset)
            {
                foreach (var entry in _bound.Values)
                {
                    ReleaseEntry(entry);
                }
                _bound.Clear();
            }
            var source = ItemsSource;
            if (source is null)
            {
                if (_boundSection is not null)
                {
                    Sections.Remove(_boundSection);
                    _boundSection = null;
                }
                return;
            }
            if (_boundSection is null)
            {
                _boundSection = new SidebarSection();
                Sections.Insert(0, _boundSection);
            }
            var live = new HashSet<object>(ReferenceEqualityComparer.Instance);
            SyncList(_boundSection.Items, source, live);
            foreach (var key in _bound.Keys.Where(k => !live.Contains(k)).ToList())
            {
                ReleaseEntry(_bound[key]);
                _bound.Remove(key);
            }
        }
        finally
        {
            _boundSyncing = false;
        }
    }

    private void SyncList(Avalonia.Collections.AvaloniaList<SidebarItem> target, IEnumerable data, HashSet<object> live)
    {
        var desired = new List<SidebarItem>();
        foreach (var obj in data)
        {
            if (obj is null || !live.Add(obj))
            {
                continue;
            }
            desired.Add(Resolve(obj, live));
        }
        if (target.Count == desired.Count)
        {
            var same = true;
            for (var i = 0; i < desired.Count; i++)
            {
                if (!ReferenceEquals(target[i], desired[i]))
                {
                    same = false;
                    break;
                }
            }
            if (same)
            {
                return;
            }
        }
        target.Clear();
        target.AddRange(desired);
    }

    private SidebarItem Resolve(object data, HashSet<object> live)
    {
        if (!_bound.TryGetValue(data, out var entry))
        {
            var item = data as SidebarItem ?? new SidebarItem { Tag = data };
            entry = new BoundEntry(item) { Generated = !ReferenceEquals(item, data) };
            _bound[data] = entry;
            if (entry.Generated)
            {
                BindMember(entry, data, LabelMember, SidebarItem.LabelProperty);
                BindMember(entry, data, KeyMember, SidebarItem.KeyProperty);
                BindMember(entry, data, IconMember, SidebarItem.IconProperty);
                BindMember(entry, data, BadgeMember, SidebarItem.BadgeProperty);
                BindMember(entry, data, IsEnabledMember, SidebarItem.IsEnabledProperty);
                if (!string.IsNullOrEmpty(ChildrenMember))
                {
                    var holder = new ChildrenHolder();
                    entry.Holder = holder;
                    entry.Subscriptions.Add(holder.Bind(ChildrenHolder.ValueProperty,
                        new Binding(ChildrenMember) { Source = data }));
                    holder.PropertyChanged += (_, e) =>
                    {
                        if (e.Property == ChildrenHolder.ValueProperty)
                        {
                            WatchChildren(entry);
                            SyncBound(reset: false);
                        }
                    };
                    WatchChildren(entry);
                }
            }
            if (TrailingTemplate is { } template && item.TrailingIcon is null && template.Match(data) &&
                template.Build(data) is { } trailing)
            {
                trailing.DataContext = data;
                item.TrailingIcon = trailing;
            }
        }
        if (entry.Generated && entry.Holder?.GetValue(ChildrenHolder.ValueProperty) is { } children)
        {
            SyncList(entry.Item.Items, children, live);
        }
        return entry.Item;
    }

    private static void BindMember(BoundEntry entry, object data, string? member, AvaloniaProperty property)
    {
        if (!string.IsNullOrEmpty(member))
        {
            entry.Subscriptions.Add(entry.Item.Bind(property, new Binding(member) { Source = data, Mode = BindingMode.OneWay }));
        }
    }

    private void WatchChildren(BoundEntry entry)
    {
        if (entry.Watched is not null && entry.Handler is not null)
        {
            entry.Watched.CollectionChanged -= entry.Handler;
        }
        entry.Watched = entry.Holder?.GetValue(ChildrenHolder.ValueProperty) as INotifyCollectionChanged;
        if (entry.Watched is not null)
        {
            entry.Handler = (_, _) => SyncBound(reset: false);
            entry.Watched.CollectionChanged += entry.Handler;
        }
    }

    private static void ReleaseEntry(BoundEntry entry)
    {
        if (entry.Watched is not null && entry.Handler is not null)
        {
            entry.Watched.CollectionChanged -= entry.Handler;
        }
        foreach (var subscription in entry.Subscriptions)
        {
            subscription.Dispose();
        }
        entry.Subscriptions.Clear();
    }
}
