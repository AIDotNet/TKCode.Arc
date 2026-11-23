using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;

namespace TKCode.Arc;

/// <summary>Data binding for <see cref="Sidebar"/>: <c>ItemsSource</c>, <c>SectionsSource</c>, member mappings and a trailing template.</summary>
public partial class Sidebar
{
    /// <summary>
    /// The pages as data. Each item becomes a row in a header-less section placed before any sections from
    /// <see cref="SectionsSource"/> and before the sections declared in <see cref="Sections"/>. An item that is a
    /// <see cref="SidebarItem"/> (or a subclass with your own fields) is used as it is; any other object is mapped
    /// through the <c>*MemberBinding</c> bindings (or the <c>*Member</c> paths), and its object is kept in
    /// <see cref="SidebarItem.Tag"/>. Collection changes are followed when the source is an
    /// <see cref="INotifyCollectionChanged"/>.
    /// </summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<Sidebar, IEnumerable?>(nameof(ItemsSource));

    /// <summary>
    /// The sections as data. Each element becomes a <see cref="SidebarSection"/> placed after the section generated for
    /// <see cref="ItemsSource"/> (if any) and before the sections declared in <see cref="Sections"/>. An element that is
    /// already a <see cref="SidebarSection"/> (or a subclass) is used as it is; any other object is mapped through the
    /// <c>Section*MemberBinding</c> bindings (or the <c>Section*Member</c> paths), and its items are mapped through the
    /// item <c>*MemberBinding</c> / <c>*Member</c> properties and <see cref="TrailingTemplate"/>. Collection changes on
    /// both the sections source and each section's items collection are followed when they implement
    /// <see cref="INotifyCollectionChanged"/>.
    /// </summary>
    public static readonly StyledProperty<IEnumerable?> SectionsSourceProperty =
        AvaloniaProperty.Register<Sidebar, IEnumerable?>(nameof(SectionsSource));

    /// <summary>
    /// Builds the content at the end of each bound row. The control gets the item's data (the object in
    /// <see cref="ItemsSource"/> or in a bound section's items) as its <c>DataContext</c>, so a template can bind to
    /// your own fields, e.g. show an add button only while <c>CanAdd</c> is true. A <see cref="SidebarItem.TrailingIcon"/>
    /// that is already set wins.
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

    /// <summary>The property path of a parent item's expanded state (a bool; default false, see <see cref="SidebarItem.IsExpanded"/>).
    /// Resolved with reflection, so trimmed and NativeAOT apps use <see cref="IsExpandedMemberBinding"/> instead, which wins
    /// when both are set.</summary>
    public static readonly StyledProperty<string?> IsExpandedMemberProperty =
        AvaloniaProperty.Register<Sidebar, string?>(nameof(IsExpandedMember));

    /// <summary>The property path of a section's heading text (for items in <see cref="SectionsSource"/> that are not
    /// <see cref="SidebarSection"/>s). Resolved with reflection, so trimmed and NativeAOT apps use
    /// <see cref="SectionHeaderMemberBinding"/> instead, which wins when both are set.</summary>
    public static readonly StyledProperty<string?> SectionHeaderMemberProperty =
        AvaloniaProperty.Register<Sidebar, string?>(nameof(SectionHeaderMember));

    /// <summary>The property path of a section's items collection (for items in <see cref="SectionsSource"/> that are not
    /// <see cref="SidebarSection"/>s). Resolved with reflection, so trimmed and NativeAOT apps use
    /// <see cref="SectionItemsMemberBinding"/> instead, which wins when both are set.</summary>
    public static readonly StyledProperty<string?> SectionItemsMemberProperty =
        AvaloniaProperty.Register<Sidebar, string?>(nameof(SectionItemsMember));

    /// <summary>The property path of whether a section can be collapsed (a bool; see <see cref="SidebarSection.IsCollapsible"/>).
    /// Resolved with reflection, so trimmed and NativeAOT apps use <see cref="SectionIsCollapsibleMemberBinding"/> instead,
    /// which wins when both are set.</summary>
    public static readonly StyledProperty<string?> SectionIsCollapsibleMemberProperty =
        AvaloniaProperty.Register<Sidebar, string?>(nameof(SectionIsCollapsibleMember));

    /// <summary>The property path of whether a collapsible section is expanded (a bool; default true, see
    /// <see cref="SidebarSection.IsExpanded"/>). Resolved with reflection, so trimmed and NativeAOT apps use
    /// <see cref="SectionIsExpandedMemberBinding"/> instead, which wins when both are set.</summary>
    public static readonly StyledProperty<string?> SectionIsExpandedMemberProperty =
        AvaloniaProperty.Register<Sidebar, string?>(nameof(SectionIsExpandedMember));

    /// <summary>The property path of a section's visibility (a bool; default true, see <see cref="SidebarSection.IsVisible"/>).
    /// Resolved with reflection, so trimmed and NativeAOT apps use <see cref="SectionIsVisibleMemberBinding"/> instead,
    /// which wins when both are set.</summary>
    public static readonly StyledProperty<string?> SectionIsVisibleMemberProperty =
        AvaloniaProperty.Register<Sidebar, string?>(nameof(SectionIsVisibleMember));

    /// <inheritdoc cref="ItemsSourceProperty"/>
    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <inheritdoc cref="SectionsSourceProperty"/>
    public IEnumerable? SectionsSource
    {
        get => GetValue(SectionsSourceProperty);
        set => SetValue(SectionsSourceProperty, value);
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

    /// <inheritdoc cref="IsExpandedMemberProperty"/>
    public string? IsExpandedMember
    {
        get => GetValue(IsExpandedMemberProperty);
        set => SetValue(IsExpandedMemberProperty, value);
    }

    /// <inheritdoc cref="SectionHeaderMemberProperty"/>
    public string? SectionHeaderMember
    {
        get => GetValue(SectionHeaderMemberProperty);
        set => SetValue(SectionHeaderMemberProperty, value);
    }

    /// <inheritdoc cref="SectionItemsMemberProperty"/>
    public string? SectionItemsMember
    {
        get => GetValue(SectionItemsMemberProperty);
        set => SetValue(SectionItemsMemberProperty, value);
    }

    /// <inheritdoc cref="SectionIsCollapsibleMemberProperty"/>
    public string? SectionIsCollapsibleMember
    {
        get => GetValue(SectionIsCollapsibleMemberProperty);
        set => SetValue(SectionIsCollapsibleMemberProperty, value);
    }

    /// <inheritdoc cref="SectionIsExpandedMemberProperty"/>
    public string? SectionIsExpandedMember
    {
        get => GetValue(SectionIsExpandedMemberProperty);
        set => SetValue(SectionIsExpandedMemberProperty, value);
    }

    /// <inheritdoc cref="SectionIsVisibleMemberProperty"/>
    public string? SectionIsVisibleMember
    {
        get => GetValue(SectionIsVisibleMemberProperty);
        set => SetValue(SectionIsVisibleMemberProperty, value);
    }

    private BindingBase? _labelBinding;
    private BindingBase? _keyBinding;
    private BindingBase? _iconBinding;
    private BindingBase? _badgeBinding;
    private BindingBase? _childrenBinding;
    private BindingBase? _isEnabledBinding;
    private BindingBase? _isVisibleBinding;
    private BindingBase? _isExpandedBinding;
    private BindingBase? _sectionHeaderBinding;
    private BindingBase? _sectionItemsBinding;
    private BindingBase? _sectionIsCollapsibleBinding;
    private BindingBase? _sectionIsExpandedBinding;
    private BindingBase? _sectionIsVisibleBinding;

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

    /// <summary>
    /// Binding that reads (and, with <see cref="BindingMode.TwoWay"/>, writes back) a parent item's expanded state
    /// (a bool; default false, see <see cref="SidebarItem.IsExpanded"/>); it wins over <see cref="IsExpandedMember"/>.
    /// </summary>
    [AssignBinding]
    public BindingBase? IsExpandedMemberBinding
    {
        get => _isExpandedBinding;
        set => SetMemberBinding(ref _isExpandedBinding, value);
    }

    /// <summary>
    /// Binding that reads a section's heading text (for items in <see cref="SectionsSource"/> that are not
    /// <see cref="SidebarSection"/>s). A compiled binding stays trimming and NativeAOT safe; it wins over
    /// <see cref="SectionHeaderMember"/>.
    /// </summary>
    [AssignBinding]
    public BindingBase? SectionHeaderMemberBinding
    {
        get => _sectionHeaderBinding;
        set => SetMemberBinding(ref _sectionHeaderBinding, value, isSection: true);
    }

    /// <summary>
    /// Binding that reads a section's items collection (for items in <see cref="SectionsSource"/> that are not
    /// <see cref="SidebarSection"/>s); it wins over <see cref="SectionItemsMember"/>.
    /// </summary>
    [AssignBinding]
    public BindingBase? SectionItemsMemberBinding
    {
        get => _sectionItemsBinding;
        set => SetMemberBinding(ref _sectionItemsBinding, value, isSection: true);
    }

    /// <summary>
    /// Binding that reads whether a section can be collapsed (a bool; see <see cref="SidebarSection.IsCollapsible"/>);
    /// it wins over <see cref="SectionIsCollapsibleMember"/>.
    /// </summary>
    [AssignBinding]
    public BindingBase? SectionIsCollapsibleMemberBinding
    {
        get => _sectionIsCollapsibleBinding;
        set => SetMemberBinding(ref _sectionIsCollapsibleBinding, value, isSection: true);
    }

    /// <summary>
    /// Binding that reads (and, with <see cref="BindingMode.TwoWay"/>, writes back) whether a collapsible section is
    /// expanded (a bool; default true, see <see cref="SidebarSection.IsExpanded"/>); it wins over
    /// <see cref="SectionIsExpandedMember"/>.
    /// </summary>
    [AssignBinding]
    public BindingBase? SectionIsExpandedMemberBinding
    {
        get => _sectionIsExpandedBinding;
        set => SetMemberBinding(ref _sectionIsExpandedBinding, value, isSection: true);
    }

    /// <summary>
    /// Binding that reads a section's visibility (a bool; default true, see <see cref="SidebarSection.IsVisible"/>);
    /// it wins over <see cref="SectionIsVisibleMember"/>.
    /// </summary>
    [AssignBinding]
    public BindingBase? SectionIsVisibleMemberBinding
    {
        get => _sectionIsVisibleBinding;
        set => SetMemberBinding(ref _sectionIsVisibleBinding, value, isSection: true);
    }

    private void SetMemberBinding(ref BindingBase? field, BindingBase? value, bool isSection = false)
    {
        if (ReferenceEquals(field, value))
        {
            return;
        }
        field = value;
        if (_boundSuspended)
        {
            if (isSection)
            {
                _boundResetSectionsOnReattach = true;
            }
            else
            {
                _boundResetItemsOnReattach = true;
            }
            return;
        }
        SyncBound(resetItems: !isSection, resetSections: isSection);
    }

    /// <summary>
    /// Reads the members of one item data object: the object is its <c>DataContext</c> and each member binding targets
    /// the property of the same type as the <see cref="SidebarItem"/> property it feeds.
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

        public static readonly StyledProperty<bool> IsExpandedProperty =
            AvaloniaProperty.Register<MemberReader, bool>("IsExpanded");

        public static readonly StyledProperty<IEnumerable?> ChildrenProperty =
            AvaloniaProperty.Register<MemberReader, IEnumerable?>("Children");
    }

    /// <summary>
    /// Reads the members of one section data object: the object is its <c>DataContext</c> and each member binding
    /// targets the property of the same type as the <see cref="SidebarSection"/> property it feeds.
    /// </summary>
    private sealed class SectionMemberReader : StyledElement
    {
        public static readonly StyledProperty<string?> HeaderProperty =
            AvaloniaProperty.Register<SectionMemberReader, string?>("Header");

        public static readonly StyledProperty<IEnumerable?> ItemsProperty =
            AvaloniaProperty.Register<SectionMemberReader, IEnumerable?>("Items");

        public static readonly StyledProperty<bool> IsCollapsibleProperty =
            AvaloniaProperty.Register<SectionMemberReader, bool>("IsCollapsible");

        public static readonly StyledProperty<bool> IsExpandedProperty =
            AvaloniaProperty.Register<SectionMemberReader, bool>("IsExpanded", true);

        public static readonly StyledProperty<bool> IsVisibleProperty =
            AvaloniaProperty.Register<SectionMemberReader, bool>("IsVisible", true);
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

    private sealed class BoundSectionEntry
    {
        public BoundSectionEntry(SidebarSection section) => Section = section;

        public SidebarSection Section { get; }
        public bool Generated { get; set; }
        public List<IDisposable> Subscriptions { get; } = new();
        public SectionMemberReader? Reader { get; set; }
        public bool HasItems { get; set; }
        public INotifyCollectionChanged? Watched { get; set; }
        public NotifyCollectionChangedEventHandler? Handler { get; set; }
    }

    private sealed class CallbackDisposable : IDisposable
    {
        private Action? _dispose;

        public CallbackDisposable(Action dispose) => _dispose = dispose;

        public void Dispose()
        {
            var action = _dispose;
            _dispose = null;
            action?.Invoke();
        }
    }

    private readonly Dictionary<object, BoundEntry> _bound = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<object, BoundSectionEntry> _boundSections = new(ReferenceEqualityComparer.Instance);
    private readonly List<SidebarSection> _boundSectionsList = new();
    private SidebarSection? _boundSection;
    private INotifyCollectionChanged? _watchedSource;
    private INotifyCollectionChanged? _watchedSectionsSource;
    private bool _boundSyncing;
    private bool _boundSuspended;
    private bool _boundResetItemsOnReattach;
    private bool _boundResetSectionsOnReattach;

    private void OnBoundSourceChanged(AvaloniaPropertyChangedEventArgs change)
    {
        var p = change.Property;
        if (p == ItemsSourceProperty)
        {
            var source = change.GetNewValue<IEnumerable?>();
            if (_boundSuspended)
            {
                if (source is null && SectionsSource is null)
                {
                    ClearAllBound();
                }
                return;
            }
            WatchRootSource(ref _watchedSource, source);
            SyncBound(resetItems: false, resetSections: false);
        }
        else if (p == SectionsSourceProperty)
        {
            var source = change.GetNewValue<IEnumerable?>();
            if (_boundSuspended)
            {
                if (source is null && ItemsSource is null)
                {
                    ClearAllBound();
                }
                return;
            }
            WatchRootSource(ref _watchedSectionsSource, source);
            SyncBound(resetItems: false, resetSections: false);
        }
        else if (p == TrailingTemplateProperty || p == LabelMemberProperty || p == KeyMemberProperty ||
                 p == IconMemberProperty || p == BadgeMemberProperty || p == ChildrenMemberProperty ||
                 p == IsEnabledMemberProperty || p == IsVisibleMemberProperty || p == IsExpandedMemberProperty)
        {
            if (_boundSuspended)
            {
                _boundResetItemsOnReattach = true;
                return;
            }
            SyncBound(resetItems: true, resetSections: false);
        }
        else if (p == SectionHeaderMemberProperty || p == SectionItemsMemberProperty ||
                 p == SectionIsCollapsibleMemberProperty || p == SectionIsExpandedMemberProperty ||
                 p == SectionIsVisibleMemberProperty)
        {
            if (_boundSuspended)
            {
                _boundResetSectionsOnReattach = true;
                return;
            }
            SyncBound(resetItems: false, resetSections: true);
        }
    }

    private void WatchRootSource(ref INotifyCollectionChanged? watched, IEnumerable? source)
    {
        if (watched is not null)
        {
            watched.CollectionChanged -= OnBoundSourceCollectionChanged;
            watched = null;
        }
        if (source is INotifyCollectionChanged notify)
        {
            watched = notify;
            notify.CollectionChanged += OnBoundSourceCollectionChanged;
        }
    }

    private void OnBoundSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => SyncBound(resetItems: false, resetSections: false);

    /// <summary>
    /// Releases all source listeners and property bindings while the sidebar is off screen so external collections and
    /// view models do not keep it alive. Existing generated sections and items stay in place so selection survives a
    /// detach and re-attach.
    /// </summary>
    private void DetachBoundSources()
    {
        if (_boundSuspended)
        {
            return;
        }
        _boundSuspended = true;
        WatchRootSource(ref _watchedSource, null);
        WatchRootSource(ref _watchedSectionsSource, null);
        foreach (var entry in _boundSections.Values)
        {
            ReleaseSectionEntry(entry, clearGeneratedItems: false);
        }
        foreach (var entry in _bound.Values)
        {
            ReleaseEntry(entry);
        }
    }

    /// <summary>Resumes source listeners and bindings when the sidebar returns to the visual tree.</summary>
    private void ReattachBoundSources()
    {
        if (!_boundSuspended)
        {
            return;
        }
        _boundSuspended = false;
        var resetItems = _boundResetItemsOnReattach;
        var resetSections = _boundResetSectionsOnReattach;
        _boundResetItemsOnReattach = false;
        _boundResetSectionsOnReattach = false;
        WatchRootSource(ref _watchedSource, ItemsSource);
        WatchRootSource(ref _watchedSectionsSource, SectionsSource);
        SyncBound(resetItems, resetSections);
    }

    private void ClearAllBound()
    {
        if (_boundSection is not null)
        {
            Sections.Remove(_boundSection);
            _boundSection = null;
        }
        foreach (var section in _boundSectionsList)
        {
            Sections.Remove(section);
        }
        _boundSectionsList.Clear();
        foreach (var entry in _boundSections.Values)
        {
            ReleaseSectionEntry(entry, clearGeneratedItems: true);
        }
        _boundSections.Clear();
        foreach (var entry in _bound.Values)
        {
            ReleaseEntry(entry);
        }
        _bound.Clear();
        _boundResetItemsOnReattach = false;
        _boundResetSectionsOnReattach = false;
    }

    /// <summary>
    /// Brings the generated sections in step with <see cref="ItemsSource"/> and <see cref="SectionsSource"/> using a
    /// single live item set so syncing one never prunes items belonging to the other.
    /// </summary>
    private void SyncBound(bool resetItems, bool resetSections)
    {
        if (_boundSyncing)
        {
            return;
        }
        _boundSyncing = true;
        try
        {
            if (resetItems)
            {
                foreach (var entry in _bound.Values)
                {
                    ReleaseEntry(entry);
                }
                _bound.Clear();
            }
            if (resetSections)
            {
                foreach (var entry in _boundSections.Values)
                {
                    ReleaseSectionEntry(entry, clearGeneratedItems: true);
                }
                _boundSections.Clear();
            }

            var liveItems = new HashSet<object>(ReferenceEqualityComparer.Instance);
            var liveSections = new HashSet<object>(ReferenceEqualityComparer.Instance);

            var itemsSource = ItemsSource;
            if (itemsSource is null)
            {
                if (_boundSection is not null)
                {
                    Sections.Remove(_boundSection);
                    _boundSection = null;
                }
            }
            else
            {
                if (_boundSection is null)
                {
                    _boundSection = new SidebarSection();
                    Sections.Insert(0, _boundSection);
                }
                else if (Sections.Count == 0 || !ReferenceEquals(Sections[0], _boundSection))
                {
                    Sections.Remove(_boundSection);
                    Sections.Insert(0, _boundSection);
                }
                SyncList(_boundSection.Items, itemsSource, liveItems);
            }

            var desiredSections = new List<SidebarSection>();
            if (SectionsSource is { } sectionsSource)
            {
                foreach (var obj in sectionsSource)
                {
                    if (obj is null || !liveSections.Add(obj))
                    {
                        continue;
                    }
                    desiredSections.Add(ResolveSection(obj, liveItems));
                }
            }

            SyncBoundSectionsInOrder(desiredSections);

            foreach (var key in _boundSections.Keys.Where(k => !liveSections.Contains(k)).ToList())
            {
                ReleaseSectionEntry(_boundSections[key], clearGeneratedItems: true);
                _boundSections.Remove(key);
            }

            foreach (var key in _bound.Keys.Where(k => !liveItems.Contains(k)).ToList())
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

    private void SyncBoundSectionsInOrder(List<SidebarSection> desiredSections)
    {
        var offset = _boundSection is not null ? 1 : 0;
        var same = _boundSectionsList.Count == desiredSections.Count &&
                   offset + desiredSections.Count <= Sections.Count;
        if (same)
        {
            for (var i = 0; i < desiredSections.Count; i++)
            {
                if (!ReferenceEquals(_boundSectionsList[i], desiredSections[i]) ||
                    !ReferenceEquals(Sections[offset + i], desiredSections[i]))
                {
                    same = false;
                    break;
                }
            }
        }
        if (same)
        {
            return;
        }
        foreach (var old in _boundSectionsList)
        {
            Sections.Remove(old);
        }
        _boundSectionsList.Clear();
        var insertAt = Math.Min(offset, Sections.Count);
        for (var i = 0; i < desiredSections.Count; i++)
        {
            var section = desiredSections[i];
            Sections.Insert(insertAt + i, section);
            _boundSectionsList.Add(section);
        }
    }

    private SidebarSection ResolveSection(object data, HashSet<object> liveItems)
    {
        if (!_boundSections.TryGetValue(data, out var entry))
        {
            var section = data as SidebarSection ?? new SidebarSection();
            entry = new BoundSectionEntry(section) { Generated = !ReferenceEquals(section, data) };
            _boundSections[data] = entry;
        }
        if (entry.Generated && entry.Reader is null)
        {
            AttachSectionBindings(data, entry);
        }
        if (entry.Generated && entry.HasItems)
        {
            if (SectionItemsOf(entry) is { } items)
            {
                SyncList(entry.Section.Items, items, liveItems);
            }
            else if (entry.Section.Items.Count > 0)
            {
                entry.Section.Items.Clear();
            }
        }
        return entry.Section;
    }

    private void AttachSectionBindings(object data, BoundSectionEntry entry)
    {
        var reader = new SectionMemberReader { DataContext = data };
        entry.Reader = reader;
        BindMember(entry.Subscriptions, reader, entry.Section, MemberBinding(_sectionHeaderBinding, SectionHeaderMember),
            SectionMemberReader.HeaderProperty, SidebarSection.HeaderProperty);
        BindMember(entry.Subscriptions, reader, entry.Section, MemberBinding(_sectionIsCollapsibleBinding, SectionIsCollapsibleMember),
            SectionMemberReader.IsCollapsibleProperty, SidebarSection.IsCollapsibleProperty);
        BindMember(entry.Subscriptions, reader, entry.Section, MemberBinding(_sectionIsVisibleBinding, SectionIsVisibleMember),
            SectionMemberReader.IsVisibleProperty, SidebarSection.IsVisibleProperty);
        BindExpandableMember(entry.Subscriptions, reader, entry.Section, MemberBinding(_sectionIsExpandedBinding, SectionIsExpandedMember),
            SectionMemberReader.IsExpandedProperty, SidebarSection.IsExpandedProperty);
        if (MemberBinding(_sectionItemsBinding, SectionItemsMember) is { } itemsBinding)
        {
            entry.HasItems = true;
            entry.Subscriptions.Add(reader.Bind(SectionMemberReader.ItemsProperty, itemsBinding));
            void OnItemsPropertyChanged(object? _, AvaloniaPropertyChangedEventArgs e)
            {
                if (e.Property == SectionMemberReader.ItemsProperty)
                {
                    WatchSectionItems(entry);
                    SyncBound(resetItems: false, resetSections: false);
                }
            }
            reader.PropertyChanged += OnItemsPropertyChanged;
            entry.Subscriptions.Add(new CallbackDisposable(() => reader.PropertyChanged -= OnItemsPropertyChanged));
            WatchSectionItems(entry);
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
        var created = false;
        if (!_bound.TryGetValue(data, out var entry))
        {
            var item = data as SidebarItem ?? new SidebarItem { Tag = data };
            entry = new BoundEntry(item) { Generated = !ReferenceEquals(item, data) };
            _bound[data] = entry;
            created = true;
        }
        if (entry.Generated && entry.Reader is null)
        {
            AttachItemBindings(data, entry);
        }
        if (created && TrailingTemplate is { } template && entry.Item.TrailingIcon is null && template.Match(data) &&
            template.Build(data) is { } trailing)
        {
            trailing.DataContext = data;
            entry.Item.TrailingIcon = trailing;
        }
        if (entry.Generated && entry.HasChildren)
        {
            if (ChildrenOf(entry) is { } children)
            {
                SyncList(entry.Item.Items, children, live);
            }
            else if (entry.Item.Items.Count > 0)
            {
                entry.Item.Items.Clear();
            }
        }
        return entry.Item;
    }

    private void AttachItemBindings(object data, BoundEntry entry)
    {
        var reader = new MemberReader { DataContext = data };
        entry.Reader = reader;
        BindMember(entry.Subscriptions, reader, entry.Item, MemberBinding(_labelBinding, LabelMember),
            MemberReader.LabelProperty, SidebarItem.LabelProperty);
        BindMember(entry.Subscriptions, reader, entry.Item, MemberBinding(_keyBinding, KeyMember),
            MemberReader.KeyProperty, SidebarItem.KeyProperty);
        BindMember(entry.Subscriptions, reader, entry.Item, MemberBinding(_iconBinding, IconMember),
            MemberReader.IconProperty, SidebarItem.IconProperty);
        BindMember(entry.Subscriptions, reader, entry.Item, MemberBinding(_badgeBinding, BadgeMember),
            MemberReader.BadgeProperty, SidebarItem.BadgeProperty);
        BindMember(entry.Subscriptions, reader, entry.Item, MemberBinding(_isEnabledBinding, IsEnabledMember),
            MemberReader.IsEnabledProperty, SidebarItem.IsEnabledProperty);
        BindMember(entry.Subscriptions, reader, entry.Item, MemberBinding(_isVisibleBinding, IsVisibleMember),
            MemberReader.IsVisibleProperty, SidebarItem.IsVisibleProperty);
        BindExpandableMember(entry.Subscriptions, reader, entry.Item, MemberBinding(_isExpandedBinding, IsExpandedMember),
            MemberReader.IsExpandedProperty, SidebarItem.IsExpandedProperty);
        if (MemberBinding(_childrenBinding, ChildrenMember) is { } childrenBinding)
        {
            entry.HasChildren = true;
            entry.Subscriptions.Add(reader.Bind(MemberReader.ChildrenProperty, childrenBinding));
            void OnChildrenPropertyChanged(object? _, AvaloniaPropertyChangedEventArgs e)
            {
                if (e.Property == MemberReader.ChildrenProperty)
                {
                    WatchChildren(entry);
                    SyncBound(resetItems: false, resetSections: false);
                }
            }
            reader.PropertyChanged += OnChildrenPropertyChanged;
            entry.Subscriptions.Add(new CallbackDisposable(() => reader.PropertyChanged -= OnChildrenPropertyChanged));
            WatchChildren(entry);
        }
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

    private static void BindMember<T>(
        List<IDisposable> subscriptions,
        StyledElement reader,
        AvaloniaObject target,
        BindingBase? binding,
        StyledProperty<T> sourceProperty,
        StyledProperty<T> targetProperty)
    {
        if (binding is null)
        {
            return;
        }
        subscriptions.Add(reader.Bind(sourceProperty, binding));
        target.SetCurrentValue(targetProperty, reader.GetValue(sourceProperty));
        void OnReaderPropertyChanged(object? _, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == sourceProperty)
            {
                target.SetCurrentValue(targetProperty, e.GetNewValue<T>());
            }
        }
        reader.PropertyChanged += OnReaderPropertyChanged;
        subscriptions.Add(new CallbackDisposable(() => reader.PropertyChanged -= OnReaderPropertyChanged));
    }

    private static void BindExpandableMember(
        List<IDisposable> subscriptions,
        StyledElement reader,
        AvaloniaObject target,
        BindingBase? binding,
        StyledProperty<bool> sourceProperty,
        StyledProperty<bool> targetProperty)
    {
        if (binding is null)
        {
            return;
        }
        var syncing = false;
        subscriptions.Add(reader.Bind(sourceProperty, binding));
        target.SetCurrentValue(targetProperty, reader.GetValue(sourceProperty));

        void OnReaderPropertyChanged(object? _, AvaloniaPropertyChangedEventArgs e)
        {
            if (syncing || e.Property != sourceProperty)
            {
                return;
            }
            syncing = true;
            try
            {
                target.SetCurrentValue(targetProperty, e.GetNewValue<bool>());
            }
            finally
            {
                syncing = false;
            }
        }

        void OnTargetPropertyChanged(object? _, AvaloniaPropertyChangedEventArgs e)
        {
            if (syncing || e.Property != targetProperty)
            {
                return;
            }
            syncing = true;
            try
            {
                reader.SetCurrentValue(sourceProperty, e.GetNewValue<bool>());
            }
            finally
            {
                syncing = false;
            }
        }

        reader.PropertyChanged += OnReaderPropertyChanged;
        target.PropertyChanged += OnTargetPropertyChanged;
        subscriptions.Add(new CallbackDisposable(() =>
        {
            reader.PropertyChanged -= OnReaderPropertyChanged;
            target.PropertyChanged -= OnTargetPropertyChanged;
        }));
    }

    private static IEnumerable? ChildrenOf(BoundEntry entry) =>
        entry.HasChildren ? entry.Reader?.GetValue(MemberReader.ChildrenProperty) : null;

    private static IEnumerable? SectionItemsOf(BoundSectionEntry entry) =>
        entry.HasItems ? entry.Reader?.GetValue(SectionMemberReader.ItemsProperty) : null;

    private void WatchChildren(BoundEntry entry)
    {
        if (entry.Watched is not null && entry.Handler is not null)
        {
            entry.Watched.CollectionChanged -= entry.Handler;
        }
        entry.Watched = ChildrenOf(entry) as INotifyCollectionChanged;
        if (entry.Watched is not null)
        {
            entry.Handler = (_, _) => SyncBound(resetItems: false, resetSections: false);
            entry.Watched.CollectionChanged += entry.Handler;
        }
        else
        {
            entry.Handler = null;
        }
    }

    private void WatchSectionItems(BoundSectionEntry entry)
    {
        if (entry.Watched is not null && entry.Handler is not null)
        {
            entry.Watched.CollectionChanged -= entry.Handler;
        }
        entry.Watched = SectionItemsOf(entry) as INotifyCollectionChanged;
        if (entry.Watched is not null)
        {
            entry.Handler = (_, _) => SyncBound(resetItems: false, resetSections: false);
            entry.Watched.CollectionChanged += entry.Handler;
        }
        else
        {
            entry.Handler = null;
        }
    }

    private static void ReleaseEntry(BoundEntry entry)
    {
        if (entry.Watched is not null && entry.Handler is not null)
        {
            entry.Watched.CollectionChanged -= entry.Handler;
        }
        entry.Watched = null;
        entry.Handler = null;
        // Dispose in reverse (LIFO) so PropertyChanged listeners detach before reader bindings clear.
        for (var i = entry.Subscriptions.Count - 1; i >= 0; i--)
        {
            entry.Subscriptions[i].Dispose();
        }
        entry.Subscriptions.Clear();
        if (entry.Reader is not null)
        {
            entry.Reader.DataContext = null;
            entry.Reader = null;
        }
        entry.HasChildren = false;
    }

    private static void ReleaseSectionEntry(BoundSectionEntry entry, bool clearGeneratedItems)
    {
        if (entry.Watched is not null && entry.Handler is not null)
        {
            entry.Watched.CollectionChanged -= entry.Handler;
        }
        entry.Watched = null;
        entry.Handler = null;
        // Dispose in reverse (LIFO) so PropertyChanged listeners detach before reader bindings clear.
        for (var i = entry.Subscriptions.Count - 1; i >= 0; i--)
        {
            entry.Subscriptions[i].Dispose();
        }
        entry.Subscriptions.Clear();
        if (entry.Reader is not null)
        {
            entry.Reader.DataContext = null;
            entry.Reader = null;
        }
        entry.HasItems = false;
        if (clearGeneratedItems && entry.Generated && entry.Section.Items.Count > 0)
        {
            entry.Section.Items.Clear();
        }
    }
}
