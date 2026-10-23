using System.Collections.Specialized;
using System.Globalization;
using Avalonia.Automation;
using Avalonia.Interactivity;

namespace TKCode.Arc;

public partial class AgentMemory
{
    #region Editing

    /// <summary>
    /// Opens the editor in place of <paramref name="memory"/>'s row and puts the caret at the end of its sentence, as the
    /// row's Edit button does. Several rows may be edited at once. Returns false when the memory is not in the list or is
    /// being forgotten.
    /// </summary>
    /// <param name="memory">A memory of the list.</param>
    /// <returns>Whether the editor is open.</returns>
    public bool BeginEdit(MemoryItem memory)
    {
        if (!_entries.TryGetValue(memory, out var entry))
        {
            return false;
        }
        if (entry.Mode == EntryMode.Editing)
        {
            entry.Editor?.FocusEditor();
            return true;
        }
        if (entry.Mode != EntryMode.Reading)
        {
            return false;
        }
        var editor = new RowEditor { Text = memory.Text ?? string.Empty, MaxLength = MaxLength };
        ApplyEditorLabels(entry, editor);
        editor.Submitted += (_, _) => _ = SaveEditAsync(entry, editor);
        editor.Cancelled += (_, _) => CloseEditor(entry, editor);
        entry.Editor = editor;
        entry.Mode = EntryMode.Editing;
        ShowContent(entry, animate: true);
        editor.FocusEditor(caretAtEnd: true);
        return true;
    }

    private void ApplyEditorLabels(Entry entry, RowEditor editor)
    {
        var labels = Labels;
        editor.HintText = labels.EditHint;
        editor.CancelText = labels.Cancel;
        editor.SaveText = labels.Save;
        editor.CountText = labels.CharacterCount;
        editor.EmptyErrorText = labels.EmptyError;
        editor.TooLongErrorText = labels.TooLongError;
        editor.AccessibleName = labels.EditName(entry.Memory.Text ?? string.Empty);
    }

    /// <summary>Cancel or Escape: the row comes back unchanged; the focus returns to its Edit button.</summary>
    private void CloseEditor(Entry entry, RowEditor editor)
    {
        if (!ReferenceEquals(entry.Editor, editor))
        {
            return;
        }
        var hadFocus = IsFocusWithin(editor);
        var method = FocusMethod;
        entry.Editor = null;
        entry.Mode = EntryMode.Reading;
        ShowContent(entry, animate: true);
        if (hadFocus)
        {
            FocusSoon(() => entry.View?.EditButton, method);
        }
    }

    /// <summary>
    /// Saves a submitted draft: unchanged text just closes the editor; otherwise the editor shows its saving state while
    /// <see cref="CommitAsync"/> runs (or the simulated save), then the row crossfades to the new sentence, the focus
    /// returns to its Edit button and the live region says so. A failure keeps the editor open with the error; a
    /// cancellation keeps it open silently. The memory changes only after the save succeeded.
    /// </summary>
    private async Task SaveEditAsync(Entry entry, RowEditor editor)
    {
        if (!ReferenceEquals(entry.Editor, editor) || entry.IsSaving)
        {
            return;
        }
        var memory = entry.Memory;
        var draft = editor.Text;
        var previous = memory.Text ?? string.Empty;
        if (draft == previous)
        {
            CloseEditor(entry, editor);
            return;
        }
        entry.IsSaving = true;
        editor.IsSaving = true;
        try
        {
            if (CommitAsync is { } commit)
            {
                await commit(new MemoryChange(MemoryChangeKind.Edit, Proposal(memory, draft), previous), _lifetime.Token);
            }
            else
            {
                await DelayAsync(SimulatedSave, _lifetime.Token);
            }
        }
        catch (OperationCanceledException)
        {
            entry.IsSaving = false;
            editor.IsSaving = false;
            return;
        }
        catch (Exception exception)
        {
            entry.IsSaving = false;
            editor.IsSaving = false;
            editor.ErrorText = MessageOf(exception, Labels.SaveFailed);
            return;
        }
        entry.IsSaving = false;
        editor.IsSaving = false;
        if (!ReferenceEquals(entry.Editor, editor) || !_entries.ContainsKey(memory))
        {
            return;
        }
        var hadFocus = IsFocusWithin(editor);
        var method = FocusMethod;
        memory.Text = draft;
        entry.Editor = null;
        entry.Mode = EntryMode.Reading;
        if (entry.View is { } view)
        {
            UpdateView(entry, view);
        }
        ShowContent(entry, animate: true);
        RefreshList(animate: true);
        if (hadFocus)
        {
            FocusSoon(() => entry.View?.EditButton, method);
        }
        Announce(Labels.AnnounceUpdated);
        RaiseEvent(new MemoryChangedEventArgs(MemoryChangedEvent, this, new MemoryChange(MemoryChangeKind.Edit, memory, previous),
            MemoryList.ToList()));
    }

    /// <summary>The memory as an edit would leave it: a copy with the new sentence (the memory itself changes after the save).</summary>
    private static MemoryItem Proposal(MemoryItem memory, string text) => new()
    {
        Id = memory.Id,
        Text = text,
        GroupKey = memory.GroupKey,
        SourceKind = memory.SourceKind,
        SourceDate = memory.SourceDate,
        SourceUri = memory.SourceUri,
        PersonName = memory.PersonName,
        PersonImage = memory.PersonImage
    };

    private static string MessageOf(Exception exception, string fallback) =>
        string.IsNullOrWhiteSpace(exception.Message) ? fallback : exception.Message;

    #endregion

    #region Forgetting

    /// <summary>
    /// Forgets <paramref name="memory"/> as its Forget button does: the row turns into the undo bar for
    /// <see cref="UndoDuration"/>, the group count and the meter drop at once, and when the window ends the bar folds away,
    /// <see cref="CommitAsync"/> runs, <see cref="MemoryChanged"/> reports it and the memory leaves the list. Several
    /// memories may wait in their undo windows together. Returns false when the memory is not in the list, is being
    /// saved or is already forgotten.
    /// </summary>
    /// <param name="memory">A memory of the list.</param>
    /// <returns>Whether the memory is now forgotten (and can be brought back with <see cref="UndoForget"/>).</returns>
    public bool Forget(MemoryItem memory)
    {
        if (!_entries.TryGetValue(memory, out var entry) || entry.IsSaving || entry.Mode is EntryMode.Forgotten or EntryMode.Removing)
        {
            return false;
        }
        var hadFocus = IsFocusWithin(entry.Row);
        var method = FocusMethod;
        var labels = Labels;
        var text = memory.Text ?? string.Empty;
        var undo = new UndoCountdownButton
        {
            Text = labels.Undo,
            SecondsText = labels.Seconds,
            SecondsName = labels.SecondsLeft,
            Duration = UndoDuration,
            Clock = UndoClock
        };
        var bar = new AgentMemoryUndoBar(memory, undo) { UndoText = labels.Undo };
        (bar.Prefix, bar.Quote) = AgentMemoryUndoBar.Split(labels.Forgot(text), text);
        AutomationProperties.SetName(bar, labels.ForgottenName);
        undo.Click += (_, e) =>
        {
            e.Handled = true;
            UndoForget(memory);
        };
        undo.Elapsed += (_, _) => OnUndoElapsed(entry, bar);
        entry.Editor = null;
        entry.Bar = bar;
        entry.Mode = EntryMode.Forgotten;
        undo.Start();
        ShowContent(entry, animate: true);
        RefreshList(animate: true);
        Announce(labels.AnnounceForgot(text, Math.Max(1, (int)Math.Ceiling(UndoDuration.TotalSeconds))));
        if (hadFocus)
        {
            FocusSoon(() => bar.Undo, method);
        }
        return true;
    }

    /// <summary>
    /// Brings back a memory whose undo window is still running, as its Undo button does: the row returns, the counts and
    /// the meter with it, and the live region says so. Returns false when the memory is not waiting in an undo window.
    /// </summary>
    /// <param name="memory">A forgotten memory.</param>
    /// <returns>Whether the memory was brought back.</returns>
    public bool UndoForget(MemoryItem memory)
    {
        if (!_entries.TryGetValue(memory, out var entry) || entry.Mode != EntryMode.Forgotten || entry.Bar is not { } bar)
        {
            return false;
        }
        var hadFocus = IsFocusWithin(bar);
        var method = FocusMethod;
        bar.Undo.Cancel();
        entry.Bar = null;
        entry.Mode = EntryMode.Reading;
        ShowContent(entry, animate: true);
        RefreshList(animate: true);
        Announce(Labels.AnnounceRestored);
        if (hadFocus)
        {
            FocusSoon(() => entry.View?.ForgetButton, method);
        }
        return true;
    }

    /// <summary>
    /// The undo window ended: the focus leaves the bar for the filters, the bar folds away (fade 100ms and the settle
    /// spring), and only then is the forgetting committed.
    /// </summary>
    private void OnUndoElapsed(Entry entry, AgentMemoryUndoBar bar)
    {
        if (!ReferenceEquals(entry.Bar, bar) || entry.Mode != EntryMode.Forgotten)
        {
            return;
        }
        if (IsFocusWithin(bar))
        {
            FocusFilters(FocusMethod);
        }
        entry.Mode = EntryMode.Removing;
        var row = entry.Row;
        RefreshList(animate: true);
        if (row is { IsLeaving: true })
        {
            void OnFolded(object? sender, EventArgs e)
            {
                row.Hidden -= OnFolded;
                _ = CommitForgetAsync(entry);
            }

            row.Hidden += OnFolded;
            return;
        }
        _ = CommitForgetAsync(entry);
    }

    /// <summary>
    /// Commits a forgetting: the hook first, then <see cref="MemoryChanged"/> (its list already without the memory),
    /// then the memory leaves the list. A failure brings the row back and says so; a cancellation brings it back silently.
    /// </summary>
    private async Task CommitForgetAsync(Entry entry)
    {
        var memory = entry.Memory;
        if (entry.Mode != EntryMode.Removing)
        {
            return;
        }
        try
        {
            if (CommitAsync is { } commit)
            {
                await commit(new MemoryChange(MemoryChangeKind.Forget, memory), _lifetime.Token);
            }
        }
        catch (OperationCanceledException)
        {
            Restore(entry, null);
            return;
        }
        catch (Exception exception)
        {
            Restore(entry, MessageOf(exception, Labels.ForgetFailed));
            return;
        }
        var list = MemoryList;
        var after = new List<MemoryItem>(list.Count);
        foreach (var item in list)
        {
            if (!ReferenceEquals(item, memory))
            {
                after.Add(item);
            }
        }
        RaiseEvent(new MemoryChangedEventArgs(MemoryChangedEvent, this, new MemoryChange(MemoryChangeKind.Forget, memory), after));
        list = MemoryList;
        if (!list.IsReadOnly && list.Contains(memory))
        {
            list.Remove(memory);
            AfterListChanged();
        }
        Announce(Labels.AnnounceForgotten);
    }

    /// <summary>A forgetting that did not go through: the memory's row comes back.</summary>
    private void Restore(Entry entry, string? announcement)
    {
        if (!_entries.ContainsKey(entry.Memory) || entry.Mode != EntryMode.Removing)
        {
            return;
        }
        entry.Bar = null;
        entry.Mode = EntryMode.Reading;
        ShowContent(entry, animate: false);
        RefreshList(animate: true);
        Announce(announcement);
    }

    /// <summary>A list that does not report its changes is read again after the card changed it.</summary>
    private void AfterListChanged()
    {
        if (MemoryList is not INotifyCollectionChanged || (MemoriesSource is not null && !ReferenceEquals(_observedMemories, MemoriesSource)))
        {
            OnMemoriesChanged();
        }
    }

    #endregion

    /// <summary>A new memory's id.</summary>
    private static string NewId() => Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
}
