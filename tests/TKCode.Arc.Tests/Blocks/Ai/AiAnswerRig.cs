using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// A responder whose answers the test writes: every request makes a <see cref="ScriptedAnswerCall"/>, and the test sends
/// its events, ends it or throws. Events sent on the UI thread reach the block at once (the awaiting code continues inline
/// on the same synchronization context).
/// </summary>
internal sealed class ScriptedAnswerResponder : IAiAnswerResponder
{
    public List<ScriptedAnswerCall> Calls { get; } = new();

    public ScriptedAnswerCall Last => Calls[^1];

    public IAsyncEnumerable<AiAnswerEvent> RespondAsync(AiAnswerRequest request, CancellationToken cancellationToken)
    {
        var call = new ScriptedAnswerCall(request, cancellationToken);
        Calls.Add(call);
        return call;
    }
}

/// <summary>One answer of a <see cref="ScriptedAnswerResponder"/>: an async stream the test feeds.</summary>
internal sealed class ScriptedAnswerCall : IAsyncEnumerable<AiAnswerEvent>, IAsyncEnumerator<AiAnswerEvent>
{
    private readonly Queue<AiAnswerEvent> _queue = new();
    private TaskCompletionSource<bool>? _waiting;
    private CancellationToken _token;
    private bool _ended;
    private Exception? _error;

    public ScriptedAnswerCall(AiAnswerRequest request, CancellationToken token)
    {
        Request = request;
        _token = token;
    }

    public AiAnswerRequest Request { get; }

    /// <summary>Whether the block cancelled the answer.</summary>
    public bool IsCancelled => _token.IsCancellationRequested;

    public AiAnswerEvent Current { get; private set; } = null!;

    public IAsyncEnumerator<AiAnswerEvent> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.CanBeCanceled)
        {
            _token = cancellationToken;
        }
        _token.Register(() => _waiting?.TrySetCanceled(_token));
        return this;
    }

    public ValueTask<bool> MoveNextAsync()
    {
        _token.ThrowIfCancellationRequested();
        if (_queue.Count > 0)
        {
            Current = _queue.Dequeue();
            return new ValueTask<bool>(true);
        }
        if (_error is { } error)
        {
            _error = null;
            return ValueTask.FromException<bool>(error);
        }
        if (_ended)
        {
            return new ValueTask<bool>(false);
        }
        _waiting = new TaskCompletionSource<bool>();
        return new ValueTask<bool>(_waiting.Task);
    }

    /// <summary>Sends events (each one reaches the block before the next is sent).</summary>
    public ScriptedAnswerCall Push(params AiAnswerEvent[] events)
    {
        foreach (var item in events)
        {
            if (_waiting is { } waiting)
            {
                _waiting = null;
                Current = item;
                waiting.TrySetResult(true);
            }
            else
            {
                _queue.Enqueue(item);
            }
            Dispatcher.UIThread.RunJobs();
        }
        return this;
    }

    public ScriptedAnswerCall Reasoning(string text) => Push(new AiAnswerReasoningEvent(text));

    public ScriptedAnswerCall Text(string text) => Push(new AiAnswerTextEvent(text));

    public ScriptedAnswerCall Thought(TimeSpan? duration = null) => Push(new AiAnswerThoughtEvent(duration));

    public ScriptedAnswerCall Sources(params AiSource[] sources) => Push(new AiAnswerSourcesEvent(sources));

    public ScriptedAnswerCall Done(int? tokens = null, TimeSpan? duration = null, params string[] followUps) =>
        Push(new AiAnswerDoneEvent(tokens, duration, followUps.Length == 0 ? null : followUps));

    /// <summary>Ends the stream.</summary>
    public void End()
    {
        _ended = true;
        if (_waiting is { } waiting)
        {
            _waiting = null;
            waiting.TrySetResult(false);
        }
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Throws <paramref name="error"/> from the stream.</summary>
    public void Throw(Exception error)
    {
        if (_waiting is { } waiting)
        {
            _waiting = null;
            waiting.TrySetException(error);
        }
        else
        {
            _error = error;
        }
        Dispatcher.UIThread.RunJobs();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// An answer page in a window (745 × 1000 by default, Arc's preview width) with the sample models and suggestions, a
/// scripted responder, a manual frame clock and time, a fake clipboard and launcher, and every event it raises recorded.
/// The frame clock is installed before the window opens, so no motion asks the real clock for a frame.
/// </summary>
internal sealed class AiAnswerRig : IDisposable
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);
    private readonly IDisposable _clock;

    public AiAnswerRig(Action<AiAnswer>? configure = null, ArcMotionLevel level = ArcMotionLevel.Off, double width = 745, double height = 1000,
        bool responder = true, bool staticPage = false)
    {
        Frames = new ManualFrameClock();
        _clock = Frames.Install();
        Responder = responder ? new ScriptedAnswerResponder() : null;
        Answer = staticPage
            ? AiAnswerPageSamples.StaticPage(a => a.Responder = Responder)
            : AiAnswerPageSamples.LivePage(Responder, a => a.Responder = Responder);
        Answer.Time = Time;
        Answer.CopyTextAsync = text =>
        {
            Copied.Add(text);
            return Task.CompletedTask;
        };
        Answer.OpenUri = uri => Opened.Add(uri);
        Answer.QuestionAsked += (_, e) =>
        {
            Asked.Add((e.Question, e.Origin));
            if (CancelQuestions)
            {
                e.Cancel = true;
            }
        };
        Answer.StatusChanged += (_, e) => Statuses.Add(e.NewStatus);
        Answer.FeedbackChanged += (_, e) => Feedback.Add(e);
        Answer.SourceInvoked += (_, e) =>
        {
            Sources.Add(e);
            e.Handled = HandleSources;
        };
        Answer.Copied += (_, e) => CopiedEvents.Add(e);
        configure?.Invoke(Answer);
        Window = TestHelpers.Show(Answer, level);
        Window.Width = width;
        Window.Height = height;
        Settle();
        // Let ArcMotion.Tracks hand out the motion pseudo-classes (after Loaded) and every first layout settle.
        Run(0.1);
    }

    public AiAnswer Answer { get; }
    public Window Window { get; }
    public ScriptedAnswerResponder? Responder { get; }
    public ManualFrameClock Frames { get; }
    public ManualTime Time { get; } = new();
    public List<string> Copied { get; } = new();
    public List<Uri> Opened { get; } = new();
    public List<(string Question, AiAnswerQuestionOrigin Origin)> Asked { get; } = new();
    public List<AiAnswerStatus> Statuses { get; } = new();
    public List<AiAnswerFeedbackEventArgs> Feedback { get; } = new();
    public List<AiAnswerSourceEventArgs> Sources { get; } = new();
    public List<AiAnswerCopiedEventArgs> CopiedEvents { get; } = new();

    /// <summary>Cancel every question asked (QuestionAsked).</summary>
    public bool CancelQuestions { get; set; }

    /// <summary>Handle every invoked source (nothing opens).</summary>
    public bool HandleSources { get; set; }

    public ScriptedAnswerCall Call => Responder!.Last;
    public AiComposer Composer => Answer.Composer!;
    public TokenTextEditor Editor => Composer.Editor!;
    public AiAnswerDocument Document => Answer.Document!;
    public AiAnswerActions Actions => Answer.Actions!;
    public AiSourcesRow SourcesRow => Answer.SourcesRow!;
    public AiReasoningDisclosure Reasoning => Answer.ReasoningRow!;
    public string? Announcement => Answer.LastAnnouncement;
    public IInputElement? Focused => Window.FocusManager?.GetFocusedElement();

    /// <summary>Asks the first sample question and writes its whole answer (reasoning, sources, thought, text, done).</summary>
    public ScriptedAnswerCall AnswerFirst(string question = AiAnswerPageSamples.MoveBuilds, bool finish = true)
    {
        _ = Answer.AskAsync(question);
        Settle();
        var call = Call;
        call.Reasoning(AiAnswerPageSamples.ReasoningFor(question))
            .Push(new AiAnswerSourcesEvent(AiAnswerPageSamples.SourcesFor(question)))
            .Thought(TimeSpan.FromSeconds(8))
            .Text(AiAnswerPageSamples.AnswerFor(question, AiAnswerMode.Answer))
            .Done(812, TimeSpan.FromSeconds(18.6), AiAnswerPageSamples.FollowUpsFor(question).ToArray());
        if (finish)
        {
            call.End();
            RunUntil(() => Answer.Status == AiAnswerStatus.Ready, 30);
        }
        return call;
    }

    /// <summary>Runs the jobs and a layout pass.</summary>
    public void Settle()
    {
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Window.UpdateLayout();
        }
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Runs 60 Hz frames for <paramref name="seconds"/> with the jobs and a layout between them.</summary>
    public void Run(double seconds)
    {
        for (var elapsed = 0.0; elapsed < seconds - 1e-9; elapsed += Frame.TotalSeconds)
        {
            Frames.Advance(Frame, Frame);
            Dispatcher.UIThread.RunJobs();
            Window.UpdateLayout();
        }
        Settle();
    }

    /// <summary>Runs frames until <paramref name="done"/> or <paramref name="seconds"/> pass; returns whether it got there.</summary>
    public bool RunUntil(Func<bool> done, double seconds = 5)
    {
        for (var elapsed = 0.0; elapsed < seconds && !done(); elapsed += Frame.TotalSeconds)
        {
            Frames.Advance(Frame, Frame);
            Dispatcher.UIThread.RunJobs();
            Window.UpdateLayout();
        }
        Settle();
        return done();
    }

    /// <summary>Types <paramref name="text"/> into the box and presses Enter.</summary>
    public void Send(string text)
    {
        Composer.FocusInput();
        Settle();
        Window.KeyTextInput(text);
        Settle();
        Key(PhysicalKey.Enter);
    }

    public void Key(PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        Window.KeyPressQwerty(key, modifiers);
        Settle();
        Window.KeyReleaseQwerty(key, modifiers);
        Settle();
    }

    public Point CenterOf(Visual visual) =>
        visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), Window) ?? default;

    /// <summary>The top left of <paramref name="visual"/> in window coordinates.</summary>
    public Point TopLeftOf(Visual visual) => visual.TranslatePoint(default, Window) ?? default;

    /// <summary>Scrolls the page so <paramref name="visual"/> is on screen (the answer is longer than the window).</summary>
    public void Reveal(Visual visual)
    {
        if (visual is Control control)
        {
            control.BringIntoView();
            Settle();
        }
    }

    public void Hover(Visual visual)
    {
        Reveal(visual);
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        Window.MouseMove(CenterOf(visual), RawInputModifiers.None);
        Settle();
    }

    public void Leave()
    {
        Window.MouseMove(new Point(2, Window.Height - 2), RawInputModifiers.None);
        Settle();
    }

    public void Click(Visual visual)
    {
        Reveal(visual);
        var point = CenterOf(visual);
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        Window.MouseMove(point, RawInputModifiers.None);
        Window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
        Settle();
        Window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
        Settle();
    }

    /// <summary>The first descendant of the window (popups included) of type <typeparamref name="T"/>.</summary>
    public T? Find<T>(Func<T, bool>? where = null) where T : Visual =>
        Window.GetVisualDescendants().OfType<T>().FirstOrDefault(v => where is null || where(v));

    public IEnumerable<T> FindAll<T>() where T : Visual => Window.GetVisualDescendants().OfType<T>();

    /// <summary>Whether <paramref name="visual"/> or something inside it has the focus.</summary>
    public bool HasFocus(Visual visual) => Focused is Visual focused && (ReferenceEquals(focused, visual) || visual.IsVisualAncestorOf(focused));

    public void Dispose()
    {
        Window.Close();
        _clock.Dispose();
    }
}
