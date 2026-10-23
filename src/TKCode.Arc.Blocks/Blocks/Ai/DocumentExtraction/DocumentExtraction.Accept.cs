using Avalonia.Input;
using Avalonia.Interactivity;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

public partial class DocumentExtraction
{
    /// <summary>The accept cascade's step from one field to the next (spec 5.2: 35ms).</summary>
    internal static readonly TimeSpan CascadeStep = TimeSpan.FromMilliseconds(35);

    private Cascade? _cascade;
    private TaskCompletionSource? _cascadeDone;
    private ExtractionState _stateBeforeAccept = ExtractionState.Done;

    /// <summary>The accept cascade in progress: when it started and how many fields it has reached.</summary>
    private sealed class Cascade(DateTimeOffset start, int count)
    {
        public DateTimeOffset Start { get; } = start;

        public int Count { get; } = count;

        public int Reached { get; set; }
    }

    /// <summary>
    /// Accept all (spec 1.7, 2.2, 5.2): when a value does not fit its type, nothing is accepted: its field takes the focus and
    /// the reason is announced. Otherwise <see cref="Accepting"/> is raised with the typed output (the button spins while
    /// the handlers' deferrals run); <c>Cancel</c> or a failed deferral leaves every field as it was. Then the fields turn
    /// accepted one after another, 35ms apart (all at once with reduced motion), and <see cref="Accepted"/> is raised.
    /// </summary>
    public async Task AcceptAllAsync()
    {
        if (_store is not { } store || !CanAccept)
        {
            return;
        }
        if (store.InvalidValues.Count > 0)
        {
            var invalid = store.InvalidValues[0];
            SetCurrentValue(SelectedTabProperty, ExtractionTab.Fields);
            SelectPath(invalid.Path);
            ScrollFormTo(invalid.Path, animate: true);
            FocusField(invalid.Path, NavigationMethod.Tab);
            AnnounceReview(Labels.AnnounceInvalid(LabelOf(invalid.Path)));
            return;
        }
        _stateBeforeAccept = State;
        State = ExtractionState.Accepting;
        RefreshAll();
        var args = new ExtractionAcceptingEventArgs(AcceptingEvent, this, Output, store.Values);
        var refused = false;
        try
        {
            RaiseEvent(args);
            if (args.Deferrals.Count > 0)
            {
                await Task.WhenAll(args.Deferrals);
            }
            refused = args.Cancel;
        }
        catch (Exception)
        {
            refused = true;
        }
        if (!ReferenceEquals(_store, store) || State != ExtractionState.Accepting)
        {
            // The document changed or a run started meanwhile.
            return;
        }
        if (refused)
        {
            State = _stateBeforeAccept;
            AnnounceReview(Labels.AnnounceAcceptFailed);
            RefreshAll();
            return;
        }
        await RunCascade(store);
    }

    /// <summary>The fields turn accepted in schema order, one every 35ms; the values are accepted when the last one has.</summary>
    private Task RunCascade(ExtractionStore store)
    {
        _cascadeDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var done = _cascadeDone.Task;
        var count = store.FieldValues.Count;
        if (ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Full || !IsLoaded || count == 0)
        {
            // Reduced and Off: every field at once (the confidence columns still crossfade).
            _cascade = new Cascade(Time.GetUtcNow(), count);
            ReachCascade(count);
            FinishCascade();
            return done;
        }
        _cascade = new Cascade(Time.GetUtcNow(), count);
        ReachCascade(1);
        _ticker.Start();
        return done;
    }

    private void AdvanceCascade(DateTimeOffset now)
    {
        if (_cascade is not { } cascade)
        {
            return;
        }
        var steps = (int)((now - cascade.Start).Ticks / CascadeStep.Ticks) + 1;
        ReachCascade(Math.Min(steps, cascade.Count));
        if (cascade.Reached >= cascade.Count && now - cascade.Start >= CascadeStep * cascade.Count)
        {
            FinishCascade();
        }
    }

    /// <summary>Marks the first <paramref name="reached"/> fields accepted on screen (their column and their bar).</summary>
    private void ReachCascade(int reached)
    {
        if (_cascade is not { } cascade || reached <= cascade.Reached)
        {
            return;
        }
        for (var i = cascade.Reached; i < reached && i < _fieldViews.Count; i++)
        {
            switch (_fieldViews[i])
            {
                case ExtractionFieldRow row:
                    row.IsAccepted = true;
                    break;
                case ExtractionTableGrid table:
                    table.IsAccepted = true;
                    break;
            }
        }
        cascade.Reached = reached;
        RefreshBars();
    }

    /// <summary>The cascade is through: the values are accepted for good and the block says so.</summary>
    private void FinishCascade()
    {
        if (_cascade is null || _store is not { } store)
        {
            _cascade = null;
            return;
        }
        _cascade = null;
        store.AcceptAll();
        store.CommitAccept();
        // The values carry the accepted state now; the views go back to following them (an edit returns only its field).
        foreach (var view in _fieldViews)
        {
            switch (view)
            {
                case ExtractionFieldRow row:
                    row.IsAccepted = false;
                    break;
                case ExtractionTableGrid table:
                    table.IsAccepted = false;
                    break;
            }
        }
        State = ExtractionState.Accepted;
        store.Flush();
        AnnounceReview(Labels.AnnounceAccepted);
        RefreshAll();
        RaiseEvent(new RoutedEventArgs(AcceptedEvent, this));
        _cascadeDone?.TrySetResult();
        _cascadeDone = null;
    }

    /// <summary>Ends a cascade that is running at once (the block left the screen or a new run starts).</summary>
    private void FinishCascadeAtOnce()
    {
        if (_cascade is { } cascade)
        {
            ReachCascade(cascade.Count);
            FinishCascade();
        }
    }

    private void OnAcceptAllClick(object? sender, RoutedEventArgs e)
    {
        if (State != ExtractionState.Accepting)
        {
            _ = AcceptAllAsync();
        }
    }

    /// <summary>
    /// The Review pill (spec 3.4): the first value that needs a check takes the focus (Fields tab), scrolled into view and
    /// selected, so its cited text is lit too. Nothing when no value needs a check.
    /// </summary>
    public void FocusNextCheck()
    {
        if (_store is not { } store)
        {
            return;
        }
        ExtractionValue? first = null;
        foreach (var value in store.Values)
        {
            if (value.NeedsCheck)
            {
                first = value;
                break;
            }
        }
        if (first is null)
        {
            return;
        }
        SetCurrentValue(SelectedTabProperty, ExtractionTab.Fields);
        SelectPath(first.Path);
        ScrollToSource(first.Path);
        ScrollFormTo(first.Path, animate: true);
        FocusField(first.Path, FocusMethod);
    }

    private void OnReviewRequested(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        FocusNextCheck();
    }

    /// <summary>"Looks right": the value is checked, its reason closes and the change is announced.</summary>
    private void OnLooksRightRequested(object? sender, ExtractionPathEventArgs e)
    {
        e.Handled = true;
        if (e.Path is not { } path || _store?.MarkChecked(path) != true)
        {
            return;
        }
        _store.Flush();
        RaiseEvent(new ExtractionPathEventArgs(ValueCheckedEvent, this, path));
        AnnounceReview(Labels.AnnounceChecked(LabelOf(path)));
    }

    /// <summary>The person typed: the value is parsed again (Edited), its JSON line flashes and <see cref="ValueChanged"/> is raised.</summary>
    private void OnFieldValueEdited(object? sender, ExtractionValueChangedEventArgs e)
    {
        e.Handled = true;
        if (_store?.EditByUser(e.Path, e.NewText) is null)
        {
            return;
        }
        _store.Flush();
        _json?.Flash(e.Path);
        RaiseEvent(new ExtractionValueChangedEventArgs(ValueChangedEvent, this, e.Path, e.OldText, e.NewText, ExtractionOrigin.User));
    }

    /// <summary>A value's name for announcements: the field's label, or "column, row n" for a cell.</summary>
    private string LabelOf(string path)
    {
        if (_store is null || !_store.Info.TryResolve(path, out var field, out var column, out var row))
        {
            return path;
        }
        return column is not null && row is { } index ? Labels.CellName(column.Label, index + 1) : field.Label;
    }
}
