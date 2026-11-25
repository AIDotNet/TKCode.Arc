// Tests of this port's scheduling of tokenization (no upstream counterpart: upstream runs on the browser's idle
// callbacks): the default background tokenizer on an injected IdleScheduler, the visible lines of an attached view, and
// the model's token events reaching the view model.

using Shouldly;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.ViewModel;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class BackgroundTokenizationTests
{
    /// <summary>An idle queue the test drains, and a clock that advances a little on every reading.</summary>
    private sealed class ManualScheduler
    {
        private readonly Queue<Action> _queue = new();
        private double _now;

        public IdleScheduler Scheduler { get; }

        public ManualScheduler(double tickPerReading = 0.25)
        {
            Scheduler = new IdleScheduler(Enqueue, () => _now += tickPerReading, idleTimeBudget: 10);
        }

        public int Pending => _queue.Count;

        public int Drain(int max = 10_000)
        {
            var runs = 0;
            while (_queue.Count > 0 && runs < max)
            {
                _queue.Dequeue()();
                runs++;
            }

            return runs;
        }

        private IDisposable Enqueue(Action action)
        {
            var cancelled = false;
            _queue.Enqueue(() =>
            {
                if (!cancelled)
                {
                    action();
                }
            });
            return new Cancel(() => cancelled = true);
        }

        private sealed class Cancel(Action cancel) : IDisposable
        {
            public void Dispose() => cancel();
        }
    }

    private sealed class CountingSupport : ITokenizationSupport
    {
        public int Calls { get; private set; }

        public IState GetInitialState() => NullState.Instance;

        public TokenizationResult Tokenize(string line, bool hasEOL, IState state) => throw new NotSupportedException();

        public EncodedTokenizationResult TokenizeEncoded(string line, bool hasEOL, IState state)
        {
            Calls++;
            return new EncodedTokenizationResult([0, 7u << Tokens.MetadataConsts.ForegroundOffset], state);
        }
    }

    private static TextModel CreateModel(int lineCount, IdleScheduler scheduler, out CountingSupport support)
    {
        var text = string.Join('\n', Enumerable.Range(1, lineCount).Select(i => "line " + i));
        var registry = new TokenizationRegistry<ITokenizationSupport>();
        support = new CountingSupport();
        registry.Register("counting", support);
        return new TextModel(text, "counting", TextModel.DefaultCreationOptions, TestTextModel.CreateUndoRedoService(), null, null, registry, scheduler);
    }

    [Fact]
    public void A_detached_model_tokenizes_only_on_demand()
    {
        var scheduler = new ManualScheduler();
        using var model = CreateModel(100, scheduler.Scheduler, out var support);

        scheduler.Drain();
        support.Calls.ShouldBe(0);

        model.Tokenization.ForceTokenization(10);
        support.Calls.ShouldBe(10);
        model.Tokenization.HasAccurateTokensForLine(10).ShouldBeTrue();
        model.Tokenization.HasAccurateTokensForLine(11).ShouldBeFalse();
    }

    [Fact]
    public void An_attached_model_tokenizes_everything_in_idle_slices_and_reports_it()
    {
        var scheduler = new ManualScheduler();
        using var model = CreateModel(5_000, scheduler.Scheduler, out var support);
        var changedLines = 0;
        model.DidChangeTokens += e => changedLines += e.Ranges.Sum(r => r.ToLineNumber - r.FromLineNumber + 1);
        var finished = 0;
        model.Tokenization.DidChangeBackgroundTokenizationState += () => finished++;

        var view = model.OnBeforeAttached();
        scheduler.Pending.ShouldBe(1);
        model.Tokenization.BackgroundTokenizationState.ShouldBe(BackgroundTokenizationState.InProgress);

        // the slices yield: one run does not do it all
        scheduler.Drain(max: 1);
        support.Calls.ShouldBeGreaterThan(0);
        support.Calls.ShouldBeLessThan(5_000);

        var runs = scheduler.Drain();
        runs.ShouldBeGreaterThan(1);
        support.Calls.ShouldBe(5_000);
        changedLines.ShouldBe(5_000);
        finished.ShouldBe(1);
        model.Tokenization.BackgroundTokenizationState.ShouldBe(BackgroundTokenizationState.Completed);
        model.Tokenization.HasAccurateTokensForLine(5_000).ShouldBeTrue();
        model.Tokenization.GetLineTokens(4_321).GetForeground(0).ShouldBe((Tokens.ColorId)7);

        model.OnBeforeDetached(view);
    }

    [Fact]
    public void An_edit_retokenizes_from_the_changed_line_until_the_state_matches()
    {
        var scheduler = new ManualScheduler();
        using var model = CreateModel(1_000, scheduler.Scheduler, out var support);
        var view = model.OnBeforeAttached();
        scheduler.Drain();
        support.Calls.ShouldBe(1_000);

        model.ApplyEdits([EditOperation.Insert(new TextPosition(500, 1), "x")]);
        scheduler.Drain();
        // NullState equals itself, so the line after the edit already ends in the stored state
        support.Calls.ShouldBe(1_001);

        model.OnBeforeDetached(view);
    }

    [Fact]
    public void The_visible_lines_of_a_view_are_tokenized_without_waiting_for_the_background()
    {
        using var model = CreateModel(10_000, IdleScheduler.None, out var support);
        var view = model.OnBeforeAttached();

        view.SetVisibleLines([new TextRange(9_000, 1, 9_020, 1)], stabilized: true);

        // heuristically, from a nearby state: not the 9,000 lines above
        support.Calls.ShouldBeLessThan(200);
        model.Tokenization.GetLineTokens(9_010).GetForeground(0).ShouldBe((Tokens.ColorId)7);

        model.OnBeforeDetached(view);
    }

    [Fact]
    public void Token_changes_reach_the_view_model_as_view_events()
    {
        var scheduler = new ManualScheduler();
        using var model = CreateModel(50, scheduler.Scheduler, out _);
        using var editor = TestCodeEditor.Create(model);
        var events = new List<ViewTokensChangedEvent>();
        var handler = new RecordingHandler(events);
        editor.ViewModel.AddViewEventHandler(handler);

        scheduler.Drain();

        events.ShouldNotBeEmpty();
        events.SelectMany(e => e.Ranges).Max(r => r.ToLineNumber).ShouldBe(50);
        editor.ViewModel.RemoveViewEventHandler(handler);
    }

    private sealed class RecordingHandler(List<ViewTokensChangedEvent> events) : ViewEventHandler
    {
        public override void HandleEvents(IReadOnlyList<ViewEvent> viewEvents)
        {
            events.AddRange(viewEvents.OfType<ViewTokensChangedEvent>());
        }
    }
}
