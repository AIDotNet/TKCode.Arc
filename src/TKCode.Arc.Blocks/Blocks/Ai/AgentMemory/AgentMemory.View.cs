using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

public partial class AgentMemory
{
    /// <summary>Above this many memories rows come and go without moving their height (a fade only).</summary>
    internal const int RowMotionLimit = 200;

    /// <summary>What a memory's row shows.</summary>
    private enum EntryMode
    {
        /// <summary>The sentence, its source and the buttons.</summary>
        Reading,

        /// <summary>The editor.</summary>
        Editing,

        /// <summary>The undo bar, while the undo window runs.</summary>
        Forgotten,

        /// <summary>Leaving the list: the undo window ended (or the memory left the list) and the row folds away.</summary>
        Removing
    }

    /// <summary>The card's own state for one memory of the list.</summary>
    private sealed class Entry(MemoryItem memory)
    {
        public MemoryItem Memory { get; } = memory;
        public EntryMode Mode { get; set; }
        public AgentMemoryRowView? View { get; set; }
        public RowEditor? Editor { get; set; }
        public AgentMemoryUndoBar? Bar { get; set; }
        public AgentMemoryRow? Row { get; set; }
        public bool IsSaving { get; set; }
        public bool Arriving { get; set; }
        public bool InList { get; set; }
    }

    /// <summary>A group as the card shows it (from <see cref="Groups"/> or the defaults).</summary>
    private sealed record GroupInfo(string Key, string Label, string? EmptyText);

    private readonly Dictionary<MemoryItem, Entry> _entries = new(ReferenceEqualityComparer.Instance);
    private readonly List<Entry> _ordered = new();
    private readonly List<GroupInfo> _groups = new();
    private readonly Dictionary<string, AgentMemoryGroupView> _groupViews = new(StringComparer.Ordinal);
    private readonly HashSet<MemoryItem> _arrivals = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<MemoryItem> _watchedMemories = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<MemoryGroup> _watchedGroups = new(ReferenceEqualityComparer.Instance);
    private INotifyCollectionChanged? _observedMemories;
    private INotifyCollectionChanged? _observedCaptured;
    private bool _listSynced;
    private int _matchCount;

    /// <summary>The list the card shows and changes.</summary>
    internal IList<MemoryItem> MemoryList => MemoriesSource ?? Memories;

    /// <summary>The query while the search is open, without its outer white space; empty otherwise.</summary>
    internal string ActiveQuery => IsSearchOpen ? (SearchText ?? string.Empty).Trim() : string.Empty;

    /// <summary>The memories found by the current search (shown rows, not counting forgotten ones).</summary>
    internal int MatchCount => _matchCount;

    #region Properties

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == MemoriesSourceProperty)
        {
            ObserveSources();
            SyncEntries();
            RefreshList(animate: true);
            UpdatePill(animate: true);
        }
        else if (property == CapturedSourceProperty)
        {
            ObserveSources();
            UpdatePill(animate: true);
        }
        else if (property == IsSavedMemoriesEnabledProperty)
        {
            UpdateSettings();
            UpdatePseudoClasses();
            UpdatePill(animate: true);
        }
        else if (property == IsChatHistoryEnabledProperty)
        {
            UpdateSettings();
        }
        else if (property == CapacityProperty || property == WarningRatioProperty)
        {
            UpdateMeter();
        }
        else if (property == MaxLengthProperty)
        {
            foreach (var entry in _entries.Values)
            {
                if (entry.Editor is { } editor)
                {
                    editor.MaxLength = change.GetNewValue<int>();
                }
            }
            UpdateAddStatus();
        }
        else if (property == AssistantNameProperty || property == TitleProperty || property == SubtitleProperty ||
                 property == LabelsProperty)
        {
            ApplyLabels();
            RebuildGroups();
            RefreshList(animate: false);
            UpdatePill(animate: false);
        }
        else if (property == SelectedGroupKeyProperty)
        {
            if (_filters is not null && _filters.SelectedKey != change.GetNewValue<string?>())
            {
                _filters.SelectedKey = change.GetNewValue<string?>();
            }
            RefreshList(animate: true);
        }
        else if (property == SearchTextProperty)
        {
            OnSearchTextChanged();
        }
        else if (property == IsSearchOpenProperty)
        {
            OnSearchOpenChanged();
        }
        else if (property == IsAddEnabledProperty)
        {
            UpdatePseudoClasses();
        }
        else if (property == CompactWidthProperty)
        {
            UpdateCompact(Bounds.Width);
        }
        else if (property == ListFadeTopProperty || property == ListFadeBottomProperty)
        {
            _fadeState = default;
            UpdateListFades();
        }
    }

    #endregion

    #region Data sources

    private void OnOwnMemoriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (MemoriesSource is null)
        {
            OnMemoriesChanged();
        }
    }

    private void OnSourceMemoriesChanged(object? sender, NotifyCollectionChangedEventArgs e) => OnMemoriesChanged();

    private void OnMemoriesChanged()
    {
        SyncEntries();
        RefreshList(animate: true);
        UpdatePill(animate: true);
    }

    private void OnOwnCapturedChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (CapturedSource is null)
        {
            UpdatePill(animate: true);
        }
    }

    private void OnSourceCapturedChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdatePill(animate: true);

    private void OnGroupsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        WatchGroups();
        RebuildGroups();
        RefreshList(animate: true);
        UpdatePill(animate: false);
    }

    /// <summary>
    /// Follows the outside lists (<see cref="MemoriesSource"/>, <see cref="CapturedSource"/>) and the groups' own
    /// properties only while the card is in a window, so a long-lived list never keeps a closed card alive.
    /// </summary>
    private void ObserveSources()
    {
        var memories = _attached ? MemoriesSource as INotifyCollectionChanged : null;
        if (!ReferenceEquals(memories, _observedMemories))
        {
            if (_observedMemories is not null)
            {
                _observedMemories.CollectionChanged -= OnSourceMemoriesChanged;
            }
            _observedMemories = memories;
            if (memories is not null)
            {
                memories.CollectionChanged += OnSourceMemoriesChanged;
            }
        }
        var captured = _attached ? CapturedSource as INotifyCollectionChanged : null;
        if (!ReferenceEquals(captured, _observedCaptured))
        {
            if (_observedCaptured is not null)
            {
                _observedCaptured.CollectionChanged -= OnSourceCapturedChanged;
            }
            _observedCaptured = captured;
            if (captured is not null)
            {
                captured.CollectionChanged += OnSourceCapturedChanged;
            }
        }
        WatchGroups();
    }

    private void WatchGroups()
    {
        foreach (var group in _watchedGroups)
        {
            group.PropertyChanged -= OnGroupPropertyChanged;
        }
        _watchedGroups.Clear();
        if (!_attached)
        {
            return;
        }
        foreach (var group in Groups)
        {
            if (group is not null && _watchedGroups.Add(group))
            {
                group.PropertyChanged += OnGroupPropertyChanged;
            }
        }
    }

    private void OnGroupPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        RebuildGroups();
        RefreshList(animate: true);
    }

    private void Watch(MemoryItem memory)
    {
        if (_attached && _watchedMemories.Add(memory))
        {
            memory.PropertyChanged += OnMemoryPropertyChanged;
        }
    }

    private void Unwatch(MemoryItem memory)
    {
        if (_watchedMemories.Remove(memory))
        {
            memory.PropertyChanged -= OnMemoryPropertyChanged;
        }
    }

    private void UnwatchAll()
    {
        foreach (var memory in _watchedMemories)
        {
            memory.PropertyChanged -= OnMemoryPropertyChanged;
        }
        _watchedMemories.Clear();
        WatchGroups();
    }

    /// <summary>A memory changed from outside (or by a saved edit): its row, the meter and the filtering follow.</summary>
    private void OnMemoryPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is not MemoryItem memory || !_entries.TryGetValue(memory, out var entry))
        {
            return;
        }
        if (entry.View is { } view)
        {
            UpdateView(entry, view);
        }
        if (e.Property == MemoryItem.TextProperty || e.Property == MemoryItem.GroupKeyProperty)
        {
            RefreshList(animate: true);
        }
    }

    /// <summary>
    /// Brings the entries in line with the list: new memories get an entry, memories that left lose theirs (their
    /// undo window and editor end with them, and their rows leave).
    /// </summary>
    private void SyncEntries()
    {
        var list = MemoryList;
        _ordered.Clear();
        foreach (var entry in _entries.Values)
        {
            entry.InList = false;
        }
        for (var i = 0; i < list.Count; i++)
        {
            var memory = list[i];
            if (memory is null)
            {
                continue;
            }
            if (!_entries.TryGetValue(memory, out var entry))
            {
                entry = new Entry(memory);
                _entries[memory] = entry;
                if (_arrivals.Remove(memory))
                {
                    entry.Arriving = true;
                }
            }
            if (entry.InList)
            {
                // The same memory twice in the list shows once.
                continue;
            }
            entry.InList = true;
            Watch(memory);
            _ordered.Add(entry);
        }
        List<Entry>? gone = null;
        foreach (var entry in _entries.Values)
        {
            if (!entry.InList)
            {
                (gone ??= new List<Entry>()).Add(entry);
            }
        }
        if (gone is null)
        {
            return;
        }
        foreach (var entry in gone)
        {
            _entries.Remove(entry.Memory);
            Unwatch(entry.Memory);
            entry.Bar?.Undo.Cancel();
            entry.Mode = EntryMode.Removing;
        }
    }

    #endregion

    #region Groups

    /// <summary>The groups in filter order: <see cref="Groups"/>, or the four default groups while it is empty.</summary>
    private void RebuildGroups()
    {
        var labels = Labels;
        var name = AssistantName;
        _groups.Clear();
        foreach (var group in Groups)
        {
            if (group is null || string.IsNullOrEmpty(group.Key) || _groups.Exists(g => g.Key == group.Key))
            {
                continue;
            }
            _groups.Add(new GroupInfo(group.Key, group.Label, group.EmptyText));
        }
        if (_groups.Count == 0)
        {
            _groups.Add(new GroupInfo(MemoryClassifier.AboutKey, labels.GroupAbout, labels.EmptyAbout(name)));
            _groups.Add(new GroupInfo(MemoryClassifier.PreferencesKey, labels.GroupPreferences, labels.EmptyPreferences(name)));
            _groups.Add(new GroupInfo(MemoryClassifier.ProjectsKey, labels.GroupProjects, labels.EmptyProjects(name)));
            _groups.Add(new GroupInfo(MemoryClassifier.PeopleKey, labels.GroupPeople, labels.EmptyPeople(name)));
        }
        if (_filters is not null)
        {
            var options = new List<PillFilterOption>(_groups.Count + 1) { new(null, labels.FilterAll) };
            foreach (var group in _groups)
            {
                options.Add(new PillFilterOption(group.Key, group.Label));
            }
            _filters.Options = options;
            _filters.SelectedKey = SelectedGroupKey;
        }
        if (_pill is not null)
        {
            // The pill is as wide as its widest text: every "Saved to" it may show.
            _pill.SizingTexts = _groups.Select(g => labels.SavedTo(g.Label)).ToList();
        }
        // Views of groups that no longer exist go; the others keep their place in group order.
        if (_groupsPanel is not null)
        {
            foreach (var key in _groupViews.Keys.ToList())
            {
                if (!_groups.Exists(g => g.Key == key))
                {
                    var view = _groupViews[key];
                    _groupViews.Remove(key);
                    if (view.Host is { } host)
                    {
                        _groupsPanel.Children.Remove(host);
                    }
                    foreach (var row in view.Rows.Children.OfType<AgentMemoryRow>())
                    {
                        row.Release();
                        if (_entries.TryGetValue(row.Memory, out var entry) && ReferenceEquals(entry.Row, row))
                        {
                            entry.Row = null;
                        }
                    }
                }
            }
        }
    }

    /// <summary>The group a memory shows in: its own key, or the first group when the card has no such group.</summary>
    private string GroupKeyOf(MemoryItem memory)
    {
        var key = memory.GroupKey;
        foreach (var group in _groups)
        {
            if (string.Equals(group.Key, key, StringComparison.Ordinal))
            {
                return group.Key;
            }
        }
        return _groups.Count > 0 ? _groups[0].Key : key;
    }

    private GroupInfo? GroupOf(string key) => _groups.Find(g => string.Equals(g.Key, key, StringComparison.Ordinal));

    /// <summary>The selected group, or null for All (also while the key names no group).</summary>
    private GroupInfo? SelectedGroup => SelectedGroupKey is { } key ? GroupOf(key) : null;

    private string GroupLabelOf(MemoryItem memory) => GroupOf(GroupKeyOf(memory))?.Label ?? memory.GroupKey;

    #endregion

    #region The list

    /// <summary>
    /// Shows the current filter or search. All shows every group that has memories, with headings; one group shows its
    /// rows alone (or its empty sentence); a search shows the matching memories of every group, grouped with headings
    /// and counts, or the empty-search state. Groups and rows that stay keep their controls; leaving ones fade out and
    /// fold, arriving ones open and fade in.
    /// </summary>
    private void RefreshList(bool animate)
    {
        if (_groupsPanel is null)
        {
            UpdateMeter();
            return;
        }
        animate &= _listSynced;
        _listSynced = true;
        var query = ActiveQuery;
        var searching = query.Length > 0;
        var selected = searching ? null : SelectedGroup;
        var headings = searching || selected is null;
        var rowMotion = _ordered.Count <= RowMotionLimit;
        var matches = 0;
        var firstShown = true;
        var visible = new HashSet<Entry>(ReferenceEqualityComparer.Instance);
        var members = new List<Entry>();
        foreach (var group in _groups)
        {
            members.Clear();
            visible.Clear();
            var count = 0;
            foreach (var entry in _ordered)
            {
                if (!string.Equals(GroupKeyOf(entry.Memory), group.Key, StringComparison.Ordinal))
                {
                    continue;
                }
                members.Add(entry);
                if (entry.Mode == EntryMode.Removing ||
                    (searching && HighlightedText.CountMatches(entry.Memory.Text ?? string.Empty, query, StringComparison.OrdinalIgnoreCase) == 0))
                {
                    continue;
                }
                visible.Add(entry);
                if (entry.Mode != EntryMode.Forgotten)
                {
                    count++;
                }
            }
            if (searching)
            {
                matches += count;
            }
            var shown = selected is null ? visible.Count > 0 : ReferenceEquals(group, selected);
            var view = EnsureGroupView(group);
            var host = view.Host!;
            var entering = shown && !host.IsShown;
            host.AnimatesHeight = rowMotion;
            view.Label = group.Label;
            view.CountText = Count(count);
            view.EmptyText = group.EmptyText;
            if (shown)
            {
                view.SetFirst(firstShown);
                firstShown = false;
            }
            // Inside a group that is coming in, everything takes its state at once: the group's own entrance shows it.
            var inner = animate && !entering && host.IsShown;
            view.SetHeading(headings, inner);
            view.SetEmpty(shown && !searching && visible.Count == 0, inner);
            SyncRows(view, members, visible, inner, rowMotion);
            if (shown && !host.IsShown)
            {
                if (animate)
                {
                    host.Show(animate: true);
                }
                else
                {
                    host.Set(true);
                }
            }
            else if (!shown && host.IsShown)
            {
                if (animate)
                {
                    host.Hide(animate: true);
                }
                else
                {
                    host.Set(false);
                }
            }
        }
        _matchCount = matches;
        if (_emptySearch is not null)
        {
            var empty = searching && firstShown;
            if (empty && _noMatches is not null)
            {
                _noMatches.Text = Labels.NoMatches(query);
            }
            if (!animate)
            {
                _emptySearch.Set(empty);
            }
            else if (empty)
            {
                _emptySearch.Show(animate: true);
            }
            else
            {
                _emptySearch.Hide(animate: true);
            }
        }
        UpdateMeter();
    }

    private AgentMemoryGroupView EnsureGroupView(GroupInfo group)
    {
        if (_groupViews.TryGetValue(group.Key, out var view))
        {
            return view;
        }
        view = new AgentMemoryGroupView(group.Key) { Label = group.Label };
        var host = new AgentMemoryPresence { Child = view };
        host.Set(false);
        view.Host = host;
        _groupViews[group.Key] = view;
        // Group views keep the order of the groups.
        var panel = _groupsPanel!;
        var index = 0;
        foreach (var other in _groups)
        {
            if (other.Key == group.Key)
            {
                break;
            }
            if (_groupViews.TryGetValue(other.Key, out var before) && before.Host is { } beforeHost && panel.Children.Contains(beforeHost))
            {
                index = panel.Children.IndexOf(beforeHost) + 1;
            }
        }
        panel.Children.Insert(index, host);
        return view;
    }

    /// <summary>
    /// The rows of one group: every member of the group has a row (hidden while filtered out), in list order; rows of
    /// memories that left the group or the list fold away where they are and are removed after.
    /// </summary>
    private void SyncRows(AgentMemoryGroupView view, List<Entry> members, HashSet<Entry> visible, bool animate, bool rowMotion)
    {
        var panel = view.Rows;
        var order = new List<Control>(members.Count);
        foreach (var entry in members)
        {
            var row = entry.Row;
            if (row is not null && !ReferenceEquals(row.Parent, panel))
            {
                // The memory moved to another group: its old row goes at once so the content can move with it.
                FinishRow(row);
                row = null;
            }
            if (row is null)
            {
                row = CreateRow(entry);
                panel.Children.Insert(Math.Min(order.Count, panel.Children.Count), row);
            }
            row.AnimatesHeight = rowMotion;
            row.SetContent(ContentOf(entry), animate: false);
            var shown = visible.Contains(entry);
            if (shown && !row.IsShown)
            {
                if (animate || entry.Arriving)
                {
                    row.Show(animate: true, entry.Arriving ? AgentMemoryEnter.Arrival : AgentMemoryEnter.Filter);
                }
                else
                {
                    row.Set(true);
                }
            }
            else if (!shown && row.IsShown)
            {
                if (animate)
                {
                    row.Hide(animate: true);
                }
                else
                {
                    row.Set(false);
                }
            }
            if (shown && entry.Arriving)
            {
                entry.Arriving = false;
                row.PlayArrival(ArrivalDuration);
                ScrollToArrival(row);
            }
            order.Add(row);
        }

        // Rows whose memory is gone from the group fold away in place.
        var children = panel.Children;
        for (var i = 0; i < children.Count; i++)
        {
            if (children[i] is not AgentMemoryRow row || order.Contains(row))
            {
                continue;
            }
            if (row.IsShown && animate)
            {
                row.Hide(animate: true);
            }
            if (row.IsLeaving)
            {
                order.Insert(Math.Min(i, order.Count), row);
            }
        }
        for (var i = 0; i < order.Count; i++)
        {
            var at = children.IndexOf(order[i]);
            if (at == i)
            {
                continue;
            }
            if (at >= 0)
            {
                children.Move(at, i);
            }
            else
            {
                children.Insert(i, order[i]);
            }
        }
        while (children.Count > order.Count)
        {
            var last = children[^1];
            children.RemoveAt(children.Count - 1);
            if (last is AgentMemoryRow gone)
            {
                ReleaseRow(gone);
            }
        }
    }

    private AgentMemoryRow CreateRow(Entry entry)
    {
        var row = new AgentMemoryRow(entry.Memory);
        row.Set(false);
        row.Hidden += OnRowHidden;
        entry.Row = row;
        return row;
    }

    /// <summary>A row finished folding: if its memory is gone from the group it leaves the panel.</summary>
    private void OnRowHidden(object? sender, EventArgs e)
    {
        if (sender is not AgentMemoryRow row || row.Parent is not Panel panel)
        {
            return;
        }
        var entry = _entries.GetValueOrDefault(row.Memory);
        var group = _groupViews.Values.FirstOrDefault(v => ReferenceEquals(v.Rows, panel));
        if (entry is not null && ReferenceEquals(entry.Row, row) && group is not null &&
            string.Equals(GroupKeyOf(entry.Memory), group.Key, StringComparison.Ordinal))
        {
            // Still a member of the group (filtered out, or folding while its forgetting is saved): the row stays,
            // hidden, for when it comes back.
            return;
        }
        panel.Children.Remove(row);
        ReleaseRow(row);
    }

    private void FinishRow(AgentMemoryRow row)
    {
        row.Set(false);
        (row.Parent as Panel)?.Children.Remove(row);
        ReleaseRow(row);
    }

    private void ReleaseRow(AgentMemoryRow row)
    {
        row.Hidden -= OnRowHidden;
        row.Release();
        if (_entries.TryGetValue(row.Memory, out var entry) && ReferenceEquals(entry.Row, row))
        {
            entry.Row = null;
        }
    }

    /// <summary>The control a memory's row shows in its current state.</summary>
    private Control ContentOf(Entry entry) => entry.Mode switch
    {
        EntryMode.Editing when entry.Editor is { } editor => editor,
        EntryMode.Forgotten or EntryMode.Removing when entry.Bar is { } bar => bar,
        _ => EnsureView(entry)
    };

    /// <summary>Puts the entry's current state in its row (crossfading when the row is on screen).</summary>
    private void ShowContent(Entry entry, bool animate)
    {
        if (entry.Row is { } row)
        {
            row.SetContent(ContentOf(entry), animate && row.IsShown);
        }
    }

    private AgentMemoryRowView EnsureView(Entry entry)
    {
        if (entry.View is { } existing)
        {
            return existing;
        }
        var view = new AgentMemoryRowView(entry.Memory);
        view.EditRequested += (_, _) => BeginEdit(entry.Memory);
        view.ForgetRequested += (_, _) => Forget(entry.Memory);
        view.SourceRequested += (_, _) => RequestSource(entry.Memory);
        UpdateView(entry, view);
        entry.View = view;
        return view;
    }

    /// <summary>The words of a reading row: the sentence, the search marks, the person, the source line and the names.</summary>
    private void UpdateView(Entry entry, AgentMemoryRowView view)
    {
        var labels = Labels;
        var memory = entry.Memory;
        var text = memory.Text ?? string.Empty;
        view.Text = text;
        view.Query = ActiveQuery;
        view.PersonName = memory.PersonName;
        view.PersonImage = memory.PersonImage;
        view.SourceText = memory.SourceKind == MemorySourceKind.Chat ? labels.SourceChat : labels.SourceManual;
        view.DateText = memory.SourceDate == DateTimeOffset.MinValue ? null : labels.Date(memory.SourceDate);
        view.IsSourceLink = memory.SourceUri is not null;
        view.EditTip = labels.Edit;
        view.ForgetTip = labels.Forget;
        view.EditName = labels.EditName(text);
        view.ForgetName = labels.ForgetName(text);
        view.IsRevealed = _touch;
    }

    /// <summary>The person pressed the source of a memory with a link.</summary>
    private void RequestSource(MemoryItem memory)
    {
        if (memory.SourceUri is null)
        {
            return;
        }
        RaiseEvent(new MemorySourceRequestedEventArgs(SourceRequestedEvent, this, memory));
        if (SourceCommand is { } command && command.CanExecute(memory))
        {
            command.Execute(memory);
        }
    }

    /// <summary>
    /// Brings a landing row into the list's view with the least scrolling, once the layout has placed it; at full motion
    /// the scroll rides the smooth spring like the slot that opens (Arc scrolls to the landing spot with the same spring).
    /// Code-driven because the target is the row's measured position and the offset of a scroll viewer has no
    /// transition; scrolling by the person stops it.
    /// </summary>
    private void ScrollToArrival(AgentMemoryRow row)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_list is not { } list || list.Content is not Visual content || !row.IsAttachedToVisualTree() ||
                row.TranslatePoint(default, content) is not { } origin)
            {
                return;
            }
            var height = row.Child?.DesiredSize.Height ?? row.Bounds.Height;
            var viewport = list.Viewport.Height;
            var offset = list.Offset.Y;
            if (viewport <= 0)
            {
                return;
            }
            var target = offset;
            if (origin.Y < offset)
            {
                target = origin.Y;
            }
            else if (origin.Y + height > offset + viewport)
            {
                target = origin.Y + height - viewport;
            }
            // The slot is still opening: the extent will grow by what the row has yet to take.
            var max = Math.Max(0, list.Extent.Height + Math.Max(0, height - row.Bounds.Height) - viewport);
            target = Math.Clamp(target, 0, max);
            if (Math.Abs(target - offset) < 0.5)
            {
                return;
            }
            if (_listScroll is null)
            {
                _listScroll = new MotionValue<double>(this, offset, MotionVectors.Double);
                _listScroll.Changed += OnListScrollChanged;
            }
            _listScroll.Jump(offset);
            _listScrollLast = offset;
            _ = _listScroll.AnimateTo(target, MotionSpec.Spring(ArcSpring.Smooth));
        }, DispatcherPriority.Loaded);
    }

    private MotionValue<double>? _listScroll;
    private double _listScrollLast;

    private void OnListScrollChanged(double value)
    {
        if (_list is not { } list)
        {
            return;
        }
        if (Math.Abs(list.Offset.Y - _listScrollLast) > 0.5)
        {
            // The person scrolled meanwhile: stop following the landing row.
            _listScroll?.Stop();
            return;
        }
        _listScrollLast = value;
        list.Offset = new Vector(list.Offset.X, value);
    }

    /// <summary>The row on screen for <paramref name="memory"/> (tests and focus moves).</summary>
    internal AgentMemoryRow? RowOf(MemoryItem memory) => _entries.TryGetValue(memory, out var entry) ? entry.Row : null;

    /// <summary>The reading view of <paramref name="memory"/>, if it has one.</summary>
    internal AgentMemoryRowView? ViewOf(MemoryItem memory) => _entries.TryGetValue(memory, out var entry) ? entry.View : null;

    /// <summary>The editor of <paramref name="memory"/> while it is edited.</summary>
    internal RowEditor? EditorOf(MemoryItem memory) => _entries.TryGetValue(memory, out var entry) ? entry.Editor : null;

    /// <summary>The undo bar of <paramref name="memory"/> while it is forgotten.</summary>
    internal AgentMemoryUndoBar? UndoBarOf(MemoryItem memory) => _entries.TryGetValue(memory, out var entry) ? entry.Bar : null;

    /// <summary>The view of the group <paramref name="key"/>, if it was ever shown.</summary>
    internal AgentMemoryGroupView? GroupViewOf(string key) => _groupViews.GetValueOrDefault(key);

    /// <summary>The group views that are shown (not leaving), in order.</summary>
    internal IEnumerable<AgentMemoryGroupView> ShownGroups =>
        _groupsPanel?.Children.OfType<AgentMemoryPresence>().Where(h => h.IsShown).Select(h => h.Child).OfType<AgentMemoryGroupView>() ?? [];

    /// <summary>The memories whose rows are shown in <paramref name="key"/>'s group, in order.</summary>
    internal IEnumerable<MemoryItem> ShownMemories(string key) =>
        GroupViewOf(key) is { Host.IsShown: true } view
            ? view.Rows.Children.OfType<AgentMemoryRow>().Where(r => r.IsShown).Select(r => r.Memory)
            : [];

    #endregion

    #region Filters and search

    private void OnFiltersPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        // The person's choice and the bar's fallback to All both come back as the selected key.
        if (e.Property == PillFilterBar.SelectedKeyProperty && _filters is not null && SelectedGroupKey != _filters.SelectedKey)
        {
            SetCurrentValue(SelectedGroupKeyProperty, _filters.SelectedKey);
        }
    }

    private void OnSearchPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_search is null)
        {
            return;
        }
        if (e.Property == AgentMemorySearchBox.TextProperty)
        {
            var text = _search.Text ?? string.Empty;
            if (text != SearchText)
            {
                SetCurrentValue(SearchTextProperty, text);
            }
        }
        else if (e.Property == IsKeyboardFocusWithinProperty && !_search.IsKeyboardFocusWithin && IsSearchOpen &&
                 string.IsNullOrWhiteSpace(SearchText))
        {
            // Leaving an empty search closes it; leaving one with a query keeps it open.
            SetCurrentValue(IsSearchOpenProperty, false);
        }
    }

    private void OnSearchOpenRequested(object? sender, EventArgs e) => OpenSearch();

    private void OnSearchCloseRequested(object? sender, RoutedEventArgs e) => CloseSearch(focusToggle: true);

    private void OnClearSearchClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        SetCurrentValue(SearchTextProperty, string.Empty);
        _search?.FocusInput(FocusMethod);
    }

    /// <summary>
    /// Opens the search box over the filter pills and puts the focus in it. The search covers every group, whatever the
    /// filter; closing it brings the filter's view back.
    /// </summary>
    public void OpenSearch()
    {
        SetCurrentValue(IsSearchOpenProperty, true);
        var method = FocusMethod;
        if (_search is { } search && !search.FocusInput(method))
        {
            Dispatcher.UIThread.Post(() => search.FocusInput(method), DispatcherPriority.Loaded);
        }
    }

    /// <summary>Clears and closes the search; the focus goes back to the magnifier when asked.</summary>
    private void CloseSearch(bool focusToggle)
    {
        var method = FocusMethod;
        SetCurrentValue(SearchTextProperty, string.Empty);
        SetCurrentValue(IsSearchOpenProperty, false);
        if (focusToggle)
        {
            _search?.FocusToggle(method);
        }
    }

    private void OnSearchOpenChanged()
    {
        if (!IsSearchOpen && !string.IsNullOrEmpty(SearchText))
        {
            // A closed search filters nothing: its text goes with it.
            SetCurrentValue(SearchTextProperty, string.Empty);
        }
        UpdateSearchParts();
        RefreshQueries();
        RefreshList(animate: true);
    }

    private void OnSearchTextChanged()
    {
        if (!string.IsNullOrEmpty(SearchText) && !IsSearchOpen)
        {
            // Text from code opens the search (which shows the results), so the filtering is never invisible.
            SetCurrentValue(IsSearchOpenProperty, true);
        }
        else
        {
            UpdateSearchParts();
            RefreshQueries();
            RefreshList(animate: true);
        }
        if (ActiveQuery.Length > 0)
        {
            Announce(Labels.MatchCount(_matchCount));
        }
    }

    private void UpdateSearchParts()
    {
        if (_search is not null)
        {
            _search.IsOpen = IsSearchOpen;
            if ((_search.Text ?? string.Empty) != (SearchText ?? string.Empty))
            {
                _search.Text = SearchText;
            }
        }
        if (_filters is not null)
        {
            _filters.IsConcealed = IsSearchOpen;
        }
    }

    /// <summary>Every reading row marks the current query.</summary>
    private void RefreshQueries()
    {
        var query = ActiveQuery;
        foreach (var entry in _entries.Values)
        {
            if (entry.View is { } view)
            {
                view.Query = query;
            }
        }
    }

    /// <summary>Clears a search that would hide <paramref name="memory"/> and switches the filter to its group if it is filtered out.</summary>
    private void Reveal(MemoryItem memory)
    {
        var query = ActiveQuery;
        if (IsSearchOpen && query.Length > 0 &&
            HighlightedText.CountMatches(memory.Text ?? string.Empty, query, StringComparison.OrdinalIgnoreCase) == 0)
        {
            var hadFocus = IsFocusWithin(_search);
            CloseSearch(focusToggle: hadFocus);
        }
        if (ActiveQuery.Length == 0 && SelectedGroup is { } selected && selected.Key != GroupKeyOf(memory))
        {
            SetCurrentValue(SelectedGroupKeyProperty, GroupKeyOf(memory));
        }
    }

    #endregion

    #region Settings

    private void OnSavedToggled(object? sender, bool on) => OnSettingToggled(MemorySetting.SavedMemories, on);

    private void OnHistoryToggled(object? sender, bool on) => OnSettingToggled(MemorySetting.ChatHistory, on);

    /// <summary>The person flipped a switch: the property follows, the event says so and the live region reads it.</summary>
    private void OnSettingToggled(MemorySetting setting, bool on)
    {
        SetCurrentValue(setting == MemorySetting.SavedMemories ? IsSavedMemoriesEnabledProperty : IsChatHistoryEnabledProperty, on);
        RaiseEvent(new MemorySettingToggledEventArgs(SettingToggledEvent, this, setting, on));
        Announce(Labels.AnnounceSetting(setting, on));
    }

    #endregion
}
