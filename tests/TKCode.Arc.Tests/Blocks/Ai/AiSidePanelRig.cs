using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// A side panel in a window (1100 × 700 by default) with the sample page and suggestions, a scripted responder the test
/// answers by hand, a manual frame clock, and every event it raises recorded.
/// </summary>
internal sealed class AiSidePanelRig : IDisposable
{
    private static readonly TimeSpan FrameTime = TimeSpan.FromMilliseconds(1000.0 / 60);
    private readonly IDisposable _clock;

    public AiSidePanelRig(Action<AiSidePanel>? configure = null, ArcMotionLevel level = ArcMotionLevel.Off, double width = 1100,
                          double height = 700, bool responder = true)
    {
        Frames = new ManualFrameClock();
        _clock = Frames.Install();
        Responder = responder ? new ScriptedPanelResponder() : null;
        Panel = new AiSidePanel { Page = AiSidePanelSamples.Page(), Responder = Responder, AssistantName = "Helper", ModelName = "Model X1" };
        Panel.Suggestions.AddRange(AiSidePanelSamples.Suggestions);
        Panel.ChangeApplied += (_, e) => Applied.Add(e);
        Panel.ChangeUndone += (_, e) => Undone.Add(e);
        Panel.ChangeDiscarded += (_, e) => Discarded.Add(e);
        Panel.SelectionAsked += (_, e) => Selections.Add(e.Selection);
        configure?.Invoke(Panel);
        Window = TestHelpers.Show(Panel, level);
        Window.Width = width;
        Window.Height = height;
        Settle();
        Run(0.1);
    }

    public AiSidePanel Panel { get; }
    public Window Window { get; }
    public ScriptedPanelResponder? Responder { get; }
    public ManualFrameClock Frames { get; }
    public List<AiPanelChangeEventArgs> Applied { get; } = new();
    public List<AiPanelChangeEventArgs> Undone { get; } = new();
    public List<AiPanelChangeEventArgs> Discarded { get; } = new();
    public List<AiPanelSelection> Selections { get; } = new();

    public PanelCall Call => Responder!.Last;
    public AiPanelBox Box => Panel.Box!;
    public AiPanelInput Input => Box.Input!;
    public AiPanelPageView PageView => Panel.PageView!;
    public AiPanelDock Dock => Panel.Dock!;
    public string? Announcement => Panel.LastAnnouncement;
    public IReadOnlyList<AiPanelTurnView> Views => Panel.TurnViews;
    public AiPanelTurnView LastView => Views[^1];
    public AiPanelTurn LastTurn => Panel.Turns[^1];
    public IInputElement? Focused => Window.FocusManager?.GetFocusedElement();

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

    /// <summary>One 60 Hz frame.</summary>
    public void Frame()
    {
        Frames.Advance(FrameTime, FrameTime);
        Dispatcher.UIThread.RunJobs();
        Window.UpdateLayout();
    }

    /// <summary>Runs 60 Hz frames for <paramref name="seconds"/>, with the jobs and a layout between them.</summary>
    public void Run(double seconds)
    {
        for (var elapsed = 0.0; elapsed < seconds - 1e-9; elapsed += FrameTime.TotalSeconds)
        {
            Frame();
        }
        Settle();
    }

    /// <summary>Runs frames until <paramref name="done"/> or <paramref name="seconds"/> pass; returns whether it got there.</summary>
    public bool RunUntil(Func<bool> done, double seconds = 5)
    {
        for (var elapsed = 0.0; elapsed < seconds && !done(); elapsed += FrameTime.TotalSeconds)
        {
            Frame();
        }
        Settle();
        return done();
    }

    /// <summary>Types <paramref name="text"/> into the box and presses Enter.</summary>
    public void Send(string text)
    {
        Box.FocusInput();
        Settle();
        Window.KeyTextInput(text);
        Settle();
        Key(PhysicalKey.Enter);
    }

    /// <summary>Asks <paramref name="prompt"/>, answers it with <paramref name="text"/> (whole) and runs until the turn ends.</summary>
    public AiPanelTurn Answer(string prompt, string text, AiPanelChangeProposal? change = null)
    {
        _ = Panel.AskAsync(prompt);
        Settle();
        Call.ReplyText(text, change);
        RunUntil(() => !Panel.IsBusy);
        return LastTurn;
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

    public void Click(Visual visual)
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        var point = CenterOf(visual);
        Window.MouseMove(point, RawInputModifiers.None);
        Window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
        Settle();
        Window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
        Settle();
    }

    /// <summary>Selects the whole text of block <paramref name="blockId"/> on the page and presses "Ask about selection".</summary>
    public AiPanelSelection AskAboutBlock(string blockId)
    {
        var view = PageView.ViewOf(blockId)!;
        var text = view.Texts[0].Text;
        var pieces = PageView.Pieces;
        var index = Enumerable.Range(0, pieces.Count).First(i => ReferenceEquals(PageView.TextOf(i), text));
        PageView.Select(new AiPanelTextPoint(index, 0), new AiPanelTextPoint(index, text.Text!.Length));
        Settle();
        var selection = PageView.Selection!;
        Run(0.3);
        Click(PageView.AskButton);
        Run(0.3);
        return selection;
    }

    public T? Find<T>() where T : Visual => Window.GetVisualDescendants().OfType<T>().FirstOrDefault();

    public void Dispose()
    {
        Window.Close();
        _clock.Dispose();
    }
}

/// <summary>A responder whose answers the test writes: every request makes a <see cref="PanelCall"/>.</summary>
internal sealed class ScriptedPanelResponder : IAiPanelResponder
{
    public List<PanelCall> Calls { get; } = new();

    public PanelCall Last => Calls[^1];

    public ValueTask<AiPanelReply> RespondAsync(AiPanelRequest request, CancellationToken cancellationToken)
    {
        var call = new PanelCall(request, cancellationToken);
        Calls.Add(call);
        return new ValueTask<AiPanelReply>(call.Reply.Task);
    }
}

/// <summary>One request of a <see cref="ScriptedPanelResponder"/>: the test replies, streams text, ends or fails it.</summary>
internal sealed class PanelCall : IAsyncEnumerable<string>, IAsyncEnumerator<string>
{
    private readonly Queue<string> _queue = new();
    private readonly CancellationToken _token;
    private TaskCompletionSource<bool>? _waiting;
    private bool _ended;
    private Exception? _error;

    public PanelCall(AiPanelRequest request, CancellationToken token)
    {
        Request = request;
        _token = token;
        // A stopped request ends what is waiting (the reply, the next text).
        token.Register(() =>
        {
            Reply.TrySetCanceled(token);
            _waiting?.TrySetCanceled(token);
        });
    }

    public AiPanelRequest Request { get; }

    public TaskCompletionSource<AiPanelReply> Reply { get; } = new();

    public bool IsCancelled => _token.IsCancellationRequested;

    public string Current { get; private set; } = string.Empty;

    /// <summary>Replies with a stream the test feeds (<see cref="Push"/>, <see cref="End"/>).</summary>
    public PanelCall Stream(AiPanelChangeProposal? change = null)
    {
        Reply.TrySetResult(new AiPanelReply(this, change));
        return this;
    }

    /// <summary>Replies with a whole text.</summary>
    public void ReplyText(string text, AiPanelChangeProposal? change = null) => Reply.TrySetResult(AiPanelReply.FromText(text, change));

    /// <summary>Fails the request before any text.</summary>
    public void Fail(string message) => Reply.TrySetException(new InvalidOperationException(message));

    public PanelCall Push(string text)
    {
        _queue.Enqueue(text);
        Wake();
        return this;
    }

    public void End()
    {
        _ended = true;
        Wake();
    }

    /// <summary>Fails the stream after the text pushed so far.</summary>
    public void FailStream(string message)
    {
        _error = new InvalidOperationException(message);
        Wake();
    }

    private void Wake()
    {
        var waiting = _waiting;
        _waiting = null;
        waiting?.TrySetResult(true);
    }

    public IAsyncEnumerator<string> GetAsyncEnumerator(CancellationToken cancellationToken = default) => this;

    public async ValueTask<bool> MoveNextAsync()
    {
        while (true)
        {
            _token.ThrowIfCancellationRequested();
            if (_queue.Count > 0)
            {
                Current = _queue.Dequeue();
                return true;
            }
            if (_error is { } error)
            {
                _error = null;
                throw error;
            }
            if (_ended)
            {
                return false;
            }
            _waiting = new TaskCompletionSource<bool>();
            await _waiting.Task;
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
