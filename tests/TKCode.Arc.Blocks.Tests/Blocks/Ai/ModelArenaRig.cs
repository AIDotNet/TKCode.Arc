using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>A clock the arena tests move by hand (measured times).</summary>
internal sealed class ArenaClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);

    public void Advance(TimeSpan time) => Now += time;

    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>
/// A runner the test drives: every stream the arena asks for becomes an <see cref="ArenaScriptCall"/> that is fed events,
/// ended or failed on demand (its continuations run on the dispatcher, so the test pumps it).
/// </summary>
internal sealed class ArenaScript : IArenaRunner
{
    public List<ArenaScriptCall> Calls { get; } = new();

    /// <summary>The latest stream for <paramref name="model"/>.</summary>
    public ArenaScriptCall For(AiModel model) => Calls.Last(call => ReferenceEquals(call.Model, model));

    public IAsyncEnumerable<ArenaStreamEvent> RunAsync(AiModel model, string prompt, CancellationToken cancellationToken)
    {
        var call = new ArenaScriptCall(model, prompt, cancellationToken);
        Calls.Add(call);
        return call.Read(cancellationToken);
    }
}

/// <summary>One scripted stream: <see cref="Push(string)"/> text or counts, <see cref="Complete"/> or <see cref="Fail"/>.</summary>
internal sealed class ArenaScriptCall(AiModel model, string prompt, CancellationToken token)
{
    private static readonly object s_end = new();
    private readonly Queue<object> _items = new();
    private TaskCompletionSource _signal = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public AiModel Model { get; } = model;

    public string Prompt { get; } = prompt;

    public CancellationToken Token { get; } = token;

    /// <summary>Whether the stream saw its token cancelled while it waited.</summary>
    public bool WasCancelled { get; private set; }

    public void Push(string text) => Add(new ArenaStreamEvent(text));

    public void Push(ArenaStreamEvent item) => Add(item);

    public void Complete() => Add(s_end);

    public void Fail(Exception exception) => Add(exception);

    private void Add(object item)
    {
        _items.Enqueue(item);
        var signal = _signal;
        _signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        signal.TrySetResult();
    }

    public async IAsyncEnumerable<ArenaStreamEvent> Read([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (true)
        {
            while (_items.Count == 0)
            {
                var signal = _signal;
                await using (cancellationToken.Register(() =>
                             {
                                 WasCancelled = true;
                                 signal.TrySetCanceled();
                             }))
                {
                    await signal.Task;
                }
            }
            var item = _items.Dequeue();
            if (ReferenceEquals(item, s_end))
            {
                yield break;
            }
            if (item is Exception exception)
            {
                throw exception;
            }
            yield return (ArenaStreamEvent)item;
        }
    }
}

/// <summary>Always the same pair, recorded.</summary>
internal sealed class FixedPairPicker(int a, int b) : IArenaPairPicker
{
    public List<(AiModel A, AiModel B)?> Previous { get; } = new();

    public (AiModel A, AiModel B) Pick(IReadOnlyList<AiModel> contestants, (AiModel A, AiModel B)? previous)
    {
        Previous.Add(previous);
        return (contestants[a], contestants[b]);
    }
}

/// <summary>A window with the sample arena, a scripted runner, a hand-moved frame clock and measured-time clock, and input helpers.</summary>
internal sealed class ModelArenaRig : IDisposable
{
    public static readonly TimeSpan FrameStep = TimeSpan.FromMilliseconds(1000.0 / 60);

    private readonly IDisposable _frames;

    public ModelArenaRig(Action<ModelArena>? configure = null, ArcMotionLevel level = ArcMotionLevel.Off, double width = 800,
        double height = 900, bool withRunner = true)
    {
        _frames = Frames.Install();
        Arena = ModelArenaSamples.Arena(arena =>
        {
            arena.Time = Time;
            arena.Random = new Random(7);
            if (withRunner)
            {
                arena.Runner = Script;
            }
            configure?.Invoke(arena);
        });
        Host = new Border { Width = width, Child = Arena };
        Window = TestHelpers.Show(Host, level);
        Window.Width = width + 40;
        Window.Height = height;
        Pump();
    }

    public ModelArena Arena { get; }

    public Border Host { get; }

    public Window Window { get; }

    public ArenaScript Script { get; } = new();

    public ArenaClock Time { get; } = new();

    public ManualFrameClock Frames { get; } = new();

    /// <summary>The streams of the latest round (or retry), by slot.</summary>
    public ArenaScriptCall A => Script.For(Arena.PaneA.Model!);

    public ArenaScriptCall B => Script.For(Arena.PaneB.Model!);

    /// <summary>Runs layout, render ticks and posted jobs a few times.</summary>
    public void Pump(int rounds = 3)
    {
        for (var i = 0; i < rounds; i++)
        {
            Window.UpdateLayout();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
        }
        Window.UpdateLayout();
    }

    /// <summary>Advances both clocks by <paramref name="count"/> 60 Hz frames, running posted jobs between them.</summary>
    public void Frame(int count = 1)
    {
        for (var i = 0; i < count; i++)
        {
            Time.Advance(FrameStep);
            Frames.Advance(FrameStep, FrameStep);
            Dispatcher.UIThread.RunJobs();
        }
        Window.UpdateLayout();
    }

    /// <summary>Advances both clocks by about <paramref name="time"/> in frames.</summary>
    public void Advance(TimeSpan time) => Frame((int)Math.Ceiling(time / FrameStep));

    /// <summary>Feeds text to a stream and lets the arena read it.</summary>
    public void Push(ArenaScriptCall call, string text)
    {
        call.Push(text);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Ends a stream and lets the arena read the end.</summary>
    public void Complete(ArenaScriptCall call)
    {
        call.Complete();
        Dispatcher.UIThread.RunJobs();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Starts a round and lets both streams begin.</summary>
    public Task Run(string? prompt = null)
    {
        var task = Arena.RunAsync(prompt);
        Pump();
        return task;
    }

    /// <summary>
    /// Streams both answers to the end: the whole text at once, then frames until both are complete and the vote row shows
    /// (at most 10 s).
    /// </summary>
    public void Finish(string textA = "Alpha answer with a few words.", string textB = "Beta answer.")
    {
        Push(A, textA);
        Push(B, textB);
        Complete(A);
        Complete(B);
        for (var i = 0; i < 600 && (Arena.Phase == ArenaPhase.Running || Arena.IsVoteRowPending); i++)
        {
            Frame();
        }
        Pump();
    }

    /// <summary>A full round up to the open vote.</summary>
    public void RoundToVote(string? prompt = null)
    {
        _ = Run(prompt);
        Finish();
    }

    public Point CenterOf(Visual visual) =>
        visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), Window)!.Value;

    public void Click(Visual visual) => Click(CenterOf(visual));

    public void Click(Point point)
    {
        Window.MouseMove(point, RawInputModifiers.None);
        Window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
        Pump(1);
        Window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
        Pump();
    }

    public void Key(PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        Window.KeyPressQwerty(key, modifiers);
        Window.KeyReleaseQwerty(key, modifiers);
        Pump();
    }

    public void Type(string text)
    {
        foreach (var c in text)
        {
            Window.KeyTextInput(c.ToString());
        }
        Pump();
    }

    /// <summary>The focused element, if any.</summary>
    public IInputElement? Focused => TopLevel.GetTopLevel(Window)?.FocusManager?.GetFocusedElement();

    /// <summary>Whether the focus is on <paramref name="visual"/> or inside it.</summary>
    public bool FocusIsIn(Visual? visual) =>
        visual is not null && Focused is Visual focused && (ReferenceEquals(focused, visual) || visual.IsVisualAncestorOf(focused));

    public void Dispose()
    {
        Window.Close();
        _frames.Dispose();
    }
}
