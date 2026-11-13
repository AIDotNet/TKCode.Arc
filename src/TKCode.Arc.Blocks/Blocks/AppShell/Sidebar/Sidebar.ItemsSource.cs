using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
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
    /// as it is; any other object is mapped through the <c>*MemberBinding</c> bindings (or the <c>*Member</c> paths),
    /// and its object is kept in
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

    /// <summary>The property path of an item's label (for items that are not <see cref="SidebarItem"/>s). Resolved with
    /// reflection, so trimmed and NativeAOT apps use <see cref="LabelMemberBinding"/> instead, which wins when both are
    /// set.</summary>
    public static readonly StyledProperty<string?> LabelMemberProperty =
        AvaloniaProperty.Register<Sidebar, string?>(nameof(LabelMember));

    /// <summary>The property path of an item's key, which <see cref="SelectedKey"/> matches against. Resolved with
    /// reflection, so trimmed and NativeAOT apps use <see cref="KeyMemberBinding"/> instead, which wins when both are
    /// set.</summary>
    public static readonly StyledProperty<string?> KeyMemberProperty =
        AvaloniaProperty.Register<Sidebar, string?>(nameof(KeyMember));

    /// <summary>The property path of an item's icon (a geometry or a control). Resolved with reflection, so trimmed and
    /// NativeAOT apps use <see cref="IconMemberBinding"/> instead, which wins when both are set.</summary>
    public static readonly StyledProperty<string?> IconMemberProperty =
        AvaloniaProperty.Register<Sidebar, string?>(nameof(IconMember));

    /// <summary>The property path of an item's badge text. Resolved with reflection, so trimmed and NativeAOT apps use <see
    /// cref="BadgeMemberBinding"/> instead, which wins when both are set.</summary>
    public static readonly StyledProperty<string?> BadgeMemberProperty =
        AvaloniaProperty.Register<Sidebar, string?>(nameof(BadgeMember));

    /// <summary>The property path of an item's child items (a collection of the same kind of data). Resolved with
    /// reflection, so trimmed and NativeAOT apps use <see cref="ChildrenMemberBinding"/> instead, which wins when both are
    /// set.</summary>
    public static readonly StyledProperty<string?> ChildrenMemberProperty =
        AvaloniaProperty.Register<Sidebar, string?>(nameof(ChildrenMember));

    /// <summary>The property path of an item's enabled state (a bool). Resolved with reflection, so trimmed and NativeAOT
    /// apps use <see cref="IsEnabledMemberBinding"/> instead, which wins when both are set.</summary>
    public static readonly StyledProperty<string?> IsEnabledMemberProperty =
        AvaloniaProperty.Register<Sidebar, string?>(nameof(IsEnabledMember));

    /// <summary>The property path of an item's visibility (a bool; false hides the page, see <see cref="SidebarItem.IsVisible"/>).
    /// Resolved with reflection, so trimmed and NativeAOT apps use <see cref="IsVisibleMemberBinding"/> instead, which wins
    /// when both are set.</summary>
    public static readonly StyledProperty<string?> IsVisibleMemberProperty =
        AvaloniaProperty.Register<Sidebar, string?>(nameof(IsVisibleMember));

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

    /// <inheritdoc cref="IsVisibleMemberProperty"/>
    public string? IsVisibleMember
    {
        get => GetValue(IsVisibleMemberProperty);
        set => SetValue(IsVisibleMemberProperty, value);
    }

    private BindingBase? _labelBinding;
    private BindingBase? _keyBinding;
    private BindingBase? _iconBinding;
    private BindingBase? _badgeBinding;
    private BindingBase? _childrenBinding;
    private BindingBase? _isEnabledBinding;
    private BindingBase? _isVisibleBinding;

    /// <summary>
    /// Binding that reads an item's label, e.g. <c>{Binding Title, DataType=local:MenuNode}</c> (for items that are not
    /// <see cref="SidebarItem"/>s). A compiled binding stays trimming and NativeAOT safe; it wins over
    /// <see cref="LabelMember"/>.
    /// </summary>
    [AssignBinding]
    public BindingBase? LabelMemberBinding
    {
        get => _labelBinding;
        set => SetMemberBinding(ref _labelBinding, value);
    }

    /// <summary>Binding that reads an item's key, which <see cref="SelectedKey"/> matches against; it wins over <see cref="KeyMember"/>.</summary>
    [AssignBinding]
    public BindingBase? KeyMemberBinding
    {
        get => _keyBinding;
        set => SetMemberBinding(ref _keyBinding, value);
    }

    /// <summary>Binding that reads an item's icon (a geometry or a control); it wins over <see cref="IconMember"/>.</summary>
    [AssignBinding]
    public BindingBase? IconMemberBinding
    {
        get => _iconBinding;
        set => SetMemberBinding(ref _iconBinding, value);
    }

    /// <summary>Binding that reads an item's badge text; it wins over <see cref="BadgeMember"/>.</summary>
    [AssignBinding]
    public BindingBase? BadgeMemberBinding
    {
        get => _badgeBinding;
        set => SetMemberBinding(ref _badgeBinding, value);
    }

    /// <summary>
    /// Binding that reads an item's child items (a collection of the same kind of data); it wins over
    /// <see cref="ChildrenMember"/>.
    /// </summary>
    [AssignBinding]
    public BindingBase? ChildrenMemberBinding
    {
        get => _childrenBinding;
        set => SetMemberBinding(ref _childrenBinding, value);
    }

    /// <summary>Binding that reads an item's enabled state (a bool); it wins over <see cref="IsEnabledMember"/>.</summary>
    [AssignBinding]
    public BindingBase? IsEnabledMemberBinding
    {
        get => _isEnabledBinding;
        set => SetMemberBinding(ref _isEnabledBinding, value);
    }

    /// <summary>Binding that reads an item's visibility (a bool; false hides the page, see <see cref="SidebarItem.IsVisible"/>); it wins over <see cref="IsVisibleMember"/>.</summary>
    [AssignBinding]
    public BindingBase? IsVisibleMemberBinding
    {
        get => _isVisibleBinding;
        set => SetMemberBinding(ref _isVisibleBinding, value);
    }

    private void SetMemberBinding(ref BindingBase? field, BindingBase? value)
    {
        if (ReferenceEquals(field, value))
        {
            return;
        }
        field = value;
        SyncBound(reset: true);
    }

    /// <summary>
    /// Reads the members of one data object: the object is its <c>DataContext</c> and each member binding targets the
    /// property of the same type as the <see cref="SidebarItem"/> property it feeds.
    /// </summary>
    private sealed class MemberReader : StyledElement
    {
        public static readonly StyledProperty<string> LabelProperty =
            AvaloniaProperty.Register<MemberReader, string>("Label", string.Empty);

        public static readonly StyledProperty<string?> KeyProperty =
            AvaloniaProperty.Register<MemberReader, string?>("Key");

        public static readonly StyledProperty<object?> IconProperty =
            AvaloniaProperty.Register<MemberReader, object?>("Icon");

        public static readonly StyledProperty<string?> BadgeProperty =
            AvaloniaProperty.Register<MemberReader, string?>("Badge");

        public static readonly StyledProperty<bool> IsEnabledProperty =
            AvaloniaProperty.Register<MemberReader, bool>("IsEnabled", true);

        public static readonly StyledProperty<bool> IsVisibleProperty =
            AvaloniaProperty.Register<MemberReader, bool>("IsVisible", true);

        public static readonly StyledProperty<IEnumerable?> ChildrenProperty =
            AvaloniaProperty.Register<MemberReader, IEnumerable?>("Children");
    }

    private sealed class BoundEntry
    {
        public BoundEntry(SidebarItem item) => Item = item;

        public SidebarItem Item { get; }
        public bool Generated { get; set; }
        public List<IDisposable> Subscriptions { get; } = new();
        public MemberReader? Reader { get; set; }
        public bool HasChildren { get; set; }
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
                 p == IsEnabledMemberProperty || p == IsVisibleMemberProperty)
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
                var reader = new MemberReader { DataContext = data };
                entry.Reader = reader;
                BindMember(entry, MemberBinding(_labelBinding, LabelMember), MemberReader.LabelProperty, SidebarItem.LabelProperty);
                BindMember(entry, MemberBinding(_keyBinding, KeyMember), MemberReader.KeyProperty, SidebarItem.KeyProperty);
                BindMember(entry, MemberBinding(_iconBinding, IconMember), MemberReader.IconProperty, SidebarItem.IconProperty);
                BindMember(entry, MemberBinding(_badgeBinding, BadgeMember), MemberReader.BadgeProperty, SidebarItem.BadgeProperty);
                BindMember(entry, MemberBinding(_isEnabledBinding, IsEnabledMember), MemberReader.IsEnabledProperty,
                    SidebarItem.IsEnabledProperty);
                BindMember(entry, MemberBinding(_isVisibleBinding, IsVisibleMember), MemberReader.IsVisibleProperty,
                    SidebarItem.IsVisibleProperty);
                if (MemberBinding(_childrenBinding, ChildrenMember) is { } childrenBinding)
                {
                    entry.HasChildren = true;
                    entry.Subscriptions.Add(reader.Bind(MemberReader.ChildrenProperty, childrenBinding));
                    reader.PropertyChanged += (_, e) =>
                    {
                        if (e.Property == MemberReader.ChildrenProperty)
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
        if (entry.Generated && ChildrenOf(entry) is { } children)
        {
            SyncList(entry.Item.Items, children, live);
        }
        return entry.Item;
    }

    /// <summary>The binding for one member: the <c>*MemberBinding</c> when set, else a binding over the <c>*Member</c> path.</summary>
    private static BindingBase? MemberBinding(BindingBase? binding, string? path) =>
        binding ?? (string.IsNullOrEmpty(path) ? null : PathBinding(path));

    /// <summary>
    /// The only reflection in <see cref="Sidebar"/>: the string <c>*Member</c> paths kept from 1.0. Apps that set none
    /// never reach it, and their documentation sends trimmed and NativeAOT apps to the <c>*MemberBinding</c> properties.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "Only reached when the app sets a string *Member path, documented as reflection based; " +
                        "trimmed and NativeAOT apps use the *MemberBinding properties instead.")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
        Justification = "Only reached when the app sets a string *Member path; see the IL2026 justification.")]
    private static BindingBase PathBinding(string path) => new Binding(path) { Mode = BindingMode.OneWay };

    private static void BindMember<T>(BoundEntry entry, BindingBase? binding, StyledProperty<T> source, StyledProperty<T> target)
    {
        if (binding is null || entry.Reader is not { } reader)
        {
            return;
        }
        entry.Subscriptions.Add(reader.Bind(source, binding));
        entry.Subscriptions.Add(entry.Item.Bind(target, reader.GetObservable(source)));
    }

    private static IEnumerable? ChildrenOf(BoundEntry entry) =>
        entry.HasChildren ? entry.Reader?.GetValue(MemberReader.ChildrenProperty) : null;

    private void WatchChildren(BoundEntry entry)
    {
        if (entry.Watched is not null && entry.Handler is not null)
        {
            entry.Watched.CollectionChanged -= entry.Handler;
        }
        entry.Watched = ChildrenOf(entry) as INotifyCollectionChanged;
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
