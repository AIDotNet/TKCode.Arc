using Avalonia;
using Avalonia.Interactivity;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

public partial class AgentMemory
{
    private readonly HashSet<CapturedMemory> _filed = new(ReferenceEqualityComparer.Instance);
    private CapturedMemory? _holding;
    private bool _pillBusy;
    private int _addGeneration;
    private bool _settingAddText;

    /// <summary>The list of memories just noted.</summary>
    internal IList<CapturedMemory> CapturedList => CapturedSource ?? Captured;

    /// <summary>The first captured memory not yet filed (and not already in the list), or null.</summary>
    internal CapturedMemory? PendingCaptured
    {
        get
        {
            foreach (var captured in CapturedList)
            {
                if (captured?.Memory is { } memory && !_filed.Contains(captured) && !_entries.ContainsKey(memory))
                {
                    return captured;
                }
            }
            return null;
        }
    }

    #region Captured pill

    /// <summary>
    /// The pill's row: the pending captured memory ("Memory updated" and its note), "Saved to …" while a filed one is
    /// held, or the paused notice while saved memories are off; with none of these the row folds away (smooth spring).
    /// </summary>
    private void UpdatePill(bool animate)
    {
        if (_pill is null || _pillHost is null)
        {
            return;
        }
        var labels = Labels;
        var paused = !IsSavedMemoriesEnabled;
        if (paused && IsFocusWithin(_pill))
        {
            // The notice takes no focus: the keyboard goes on from the filters.
            FocusFilters(FocusMethod);
        }
        _pill.IsPaused = paused;
        _pill.PausedText = labels.PausedNotice(AssistantName);
        var shown = _holding ?? PendingCaptured;
        if (shown?.Memory is { } memory)
        {
            var group = GroupLabelOf(memory);
            _pill.Title = labels.Captured;
            _pill.Note = shown.Note;
            _pill.SavedText = _holding is not null ? labels.SavedTo(group) : null;
            _pill.AccessibleName = labels.CapturedName(shown.Note, group);
        }
        var visible = paused || shown is not null;
        animate &= _listSynced;
        if (visible)
        {
            if (!_pillHost.IsShown || _pillHost.IsLeaving)
            {
                if (animate)
                {
                    _pillHost.Show(animate: true, AgentMemoryEnter.Fold);
                }
                else
                {
                    _pillHost.Set(true);
                }
            }
        }
        else if (_pillHost.IsShown)
        {
            if (animate)
            {
                _pillHost.Hide(animate: true, fade: false, spring: ArcSpring.Smooth);
            }
            else
            {
                _pillHost.Set(false);
            }
        }
    }

    private void OnPillClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        FileCaptured();
    }

    /// <summary>
    /// Files the captured memory the pill shows, as pressing the pill does: a search that would hide it is cleared or
    /// the filter switches to its group, <see cref="CommitAsync"/> runs (as a capture), the memory lands at the top of
    /// its group with a short tint, the pill says "Saved to …" for a moment and then folds away (or shows the next
    /// captured memory). Returns false while saved memories are off, while another one is being filed or when nothing
    /// is pending.
    /// </summary>
    /// <returns>Whether filing started.</returns>
    public bool FileCaptured()
    {
        if (!IsSavedMemoriesEnabled || _pillBusy || _holding is not null || PendingCaptured is not { Memory: { } memory } captured)
        {
            return false;
        }
        _ = FileAsync(captured, memory);
        return true;
    }

    private async Task FileAsync(CapturedMemory captured, MemoryItem memory)
    {
        _pillBusy = true;
        var token = _lifetime.Token;
        Reveal(memory);
        try
        {
            if (CommitAsync is { } commit)
            {
                await commit(new MemoryChange(MemoryChangeKind.Capture, memory), token);
            }
        }
        catch (OperationCanceledException)
        {
            _pillBusy = false;
            return;
        }
        catch (Exception exception)
        {
            _pillBusy = false;
            Announce(MessageOf(exception, Labels.SaveFailed));
            return;
        }
        var labels = Labels;
        var group = GroupLabelOf(memory);
        _holding = captured;
        InsertMemory(memory);
        UpdatePill(animate: true);
        Announce(labels.AnnounceSaved(group, memory.Text ?? string.Empty));
        RaiseEvent(new MemoryChangedEventArgs(MemoryChangedEvent, this, new MemoryChange(MemoryChangeKind.Capture, memory), MemoryList.ToList()));
        try
        {
            await DelayAsync(CapturedHold, token);
        }
        catch (OperationCanceledException)
        {
            // Detached during the hold: the memory is filed all the same.
        }
        _filed.Add(captured);
        _holding = null;
        _pillBusy = false;
        var keyboardFocus = _keyboardMode && IsFocusWithin(_pill);
        UpdatePill(animate: true);
        if (keyboardFocus && _pillHost is { IsShown: false })
        {
            // The pill folds away under the keyboard focus: it moves on to the new memory's Edit button (with its tooltip).
            FocusSoon(() => ViewOf(memory)?.EditButton, Avalonia.Input.NavigationMethod.Tab);
        }
    }

    /// <summary>Puts a new memory at the top of the list (newest first) and lets it land with the arrival tint.</summary>
    private void InsertMemory(MemoryItem memory)
    {
        var list = MemoryList;
        if (list.Contains(memory) || list.IsReadOnly)
        {
            return;
        }
        _arrivals.Add(memory);
        list.Insert(0, memory);
        AfterListChanged();
    }

    #endregion

    #region Add field

    private void OnAddFieldPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == AgentMemoryAddField.TextProperty && !_settingAddText)
        {
            UpdateAddStatus();
        }
    }

    private void OnAddSubmitted(object? sender, EventArgs e) => _ = AddAsync();

    /// <summary>The status that follows the typed text: nothing, the Add button, or the live count past the limit.</summary>
    private void UpdateAddStatus()
    {
        if (_addField is not { } field || field.IsBusy)
        {
            return;
        }
        var length = (field.Text ?? string.Empty).Trim().Length;
        if (length == 0)
        {
            if (field.Status != AgentMemoryAddStatus.Added)
            {
                field.SetStatus(AgentMemoryAddStatus.None);
            }
            return;
        }
        var limit = MaxLength;
        if (limit > 0 && length > limit)
        {
            field.SetStatus(AgentMemoryAddStatus.Alert, Labels.CharacterCount(length, limit), isCount: true);
            return;
        }
        field.SetStatus(AgentMemoryAddStatus.Ready);
    }

    /// <summary>
    /// Adds the typed memory: too long or past the capacity is refused in place (the text stays); otherwise the field
    /// shows "Sorting into a group" while the memory is sorted (<see cref="ClassifyAsync"/> or the built-in guess) and
    /// saved, the filter switches to its group if needed, the memory lands at the top of its group, the field empties
    /// and says "Added to …" for a moment. The focus stays in the field.
    /// </summary>
    private async Task AddAsync()
    {
        if (_addField is not { } field || field.IsBusy)
        {
            return;
        }
        var text = (field.Text ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return;
        }
        var labels = Labels;
        _addGeneration++;
        var limit = MaxLength;
        if (limit > 0 && text.Length > limit)
        {
            field.SetStatus(AgentMemoryAddStatus.Alert, labels.TooLongError(limit));
            return;
        }
        if (Capacity > 0 && UsedCharacters + text.Length > Capacity)
        {
            field.SetStatus(AgentMemoryAddStatus.Alert, labels.Full);
            Announce(labels.AnnounceFull);
            return;
        }
        var token = _lifetime.Token;
        field.IsBusy = true;
        field.SetStatus(AgentMemoryAddStatus.Sorting, labels.Sorting);
        string key;
        try
        {
            key = await ClassifyMemoryAsync(text, token);
        }
        catch (OperationCanceledException)
        {
            field.IsBusy = false;
            UpdateAddStatus();
            return;
        }
        catch (Exception)
        {
            field.IsBusy = false;
            field.SetStatus(AgentMemoryAddStatus.Alert, labels.SortFailed);
            return;
        }
        var group = GroupOf(key) ?? _groups[0];
        var memory = new MemoryItem
        {
            Id = NewId(),
            Text = text,
            GroupKey = group.Key,
            SourceKind = MemorySourceKind.Manual,
            SourceDate = Time.GetLocalNow()
        };
        Reveal(memory);
        try
        {
            if (CommitAsync is { } commit)
            {
                await commit(new MemoryChange(MemoryChangeKind.Add, memory), token);
            }
        }
        catch (OperationCanceledException)
        {
            field.IsBusy = false;
            UpdateAddStatus();
            return;
        }
        catch (Exception exception)
        {
            field.IsBusy = false;
            field.SetStatus(AgentMemoryAddStatus.Alert, MessageOf(exception, labels.SaveFailed));
            return;
        }
        InsertMemory(memory);
        _settingAddText = true;
        try
        {
            field.Text = string.Empty;
        }
        finally
        {
            _settingAddText = false;
        }
        field.IsBusy = false;
        field.SetStatus(AgentMemoryAddStatus.Added, labels.AddedTo(group.Label));
        Announce(labels.AnnounceAdded(group.Label, text));
        RaiseEvent(new MemoryChangedEventArgs(MemoryChangedEvent, this, new MemoryChange(MemoryChangeKind.Add, memory), MemoryList.ToList()));
        var generation = ++_addGeneration;
        try
        {
            await DelayAsync(AddedHold, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        if (generation == _addGeneration && field.Status == AgentMemoryAddStatus.Added)
        {
            // "Added to …" fades; whatever was typed since decides what follows.
            field.SetStatus(AgentMemoryAddStatus.None);
            UpdateAddStatus();
        }
    }

    /// <summary>The group key of a typed memory: the hook's, or the built-in keyword guess after a short simulated wait.</summary>
    private async ValueTask<string> ClassifyMemoryAsync(string text, CancellationToken token)
    {
        if (ClassifyAsync is { } classify)
        {
            return await classify(text, token);
        }
        await DelayAsync(SimulatedSort, token);
        var keys = new List<string>(_groups.Count);
        foreach (var group in _groups)
        {
            keys.Add(group.Key);
        }
        return MemoryClassifier.Classify(text, keys);
    }

    #endregion
}
