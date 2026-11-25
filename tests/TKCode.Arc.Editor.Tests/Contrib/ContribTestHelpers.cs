// Helpers of the contrib tests: a scheduler whose timers the test advances (upstream's runWithFakedTimers) and a
// minimal editor for the code action model (upstream's createTestCodeEditor with a model and a position).

using TKCode.Arc.Editor.Contrib.CodeAction;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Tests;

/// <summary>An <see cref="IdleScheduler"/> whose timers run only when the test advances the clock.</summary>
internal sealed class CodeActionFakeTimers
{
    private readonly List<(double Due, long Order, Action Callback, Box Cancelled)> _timers = new();
    private long _order;

    public CodeActionFakeTimers()
    {
        Scheduler = new IdleScheduler(
            action => Add(0, action),
            () => Now,
            runAfter: (delay, action) => Add(delay.TotalMilliseconds, action));
    }

    public IdleScheduler Scheduler { get; }

    public double Now { get; private set; }

    public int Pending => _timers.Count(t => !t.Cancelled.Value);

    /// <summary>Advances the clock, running the timers that come due in order (also those they start).</summary>
    public void Advance(double milliseconds)
    {
        var end = Now + milliseconds;
        while (true)
        {
            var next = _timers.Where(t => !t.Cancelled.Value && t.Due <= end).OrderBy(t => t.Due).ThenBy(t => t.Order).FirstOrDefault();
            if (next.Callback is null)
            {
                break;
            }

            _timers.Remove(next);
            Now = Math.Max(Now, next.Due);
            next.Callback();
        }

        Now = end;
        _timers.RemoveAll(t => t.Cancelled.Value);
    }

    /// <summary>Runs every pending timer.</summary>
    public void RunAll() => Advance(1_000_000);

    private IDisposable Add(double delay, Action callback)
    {
        var cancelled = new Box();
        _timers.Add((Now + delay, _order++, callback, cancelled));
        return new Cancel(cancelled);
    }

    private sealed class Box
    {
        public bool Value { get; set; }
    }

    private sealed class Cancel(Box box) : IDisposable
    {
        public void Dispose() => box.Value = true;
    }
}

/// <summary>A model, a selection and the events the code action model listens to.</summary>
internal sealed class FakeCodeActionEditor(TextModel? model, IdleScheduler scheduler) : ICodeActionEditor
{
    private TextSelection _selection = new(1, 1, 1, 1);

    public TextModel? Model { get; private set; } = model;

    public bool IsReadOnly { get; set; }

    public ShowLightbulbIconMode Lightbulb { get; set; } = ShowLightbulbIconMode.OnCode;

    public bool IncludeNearbyQuickFixes { get; set; }

    public IdleScheduler Scheduler { get; } = scheduler;

    public event Action? DidChangeCursorPosition;

    public event Action? DidChangeModel;

    public event Action? DidChangeModelLanguage;

    public event Action? DidChangeConfiguration;

    public TextSelection? GetSelection() => Model is null ? null : _selection;

    public void SetPosition(int lineNumber, int column) => SetSelection(new TextSelection(lineNumber, column, lineNumber, column));

    public void SetSelection(TextSelection selection)
    {
        _selection = selection;
        DidChangeCursorPosition?.Invoke();
    }

    public void SetModel(TextModel? model)
    {
        Model = model;
        DidChangeModel?.Invoke();
    }

    public void RaiseLanguageChanged() => DidChangeModelLanguage?.Invoke();

    public void RaiseConfigurationChanged() => DidChangeConfiguration?.Invoke();
}
