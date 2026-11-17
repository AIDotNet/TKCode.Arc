using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>A clock the tests move by hand (run times).</summary>
internal sealed class StudioClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = PromptStudioSamples.Now;

    public void Advance(TimeSpan time) => Now += time;

    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>
/// A run handler the test drives: each run becomes a <see cref="StudioScriptCall"/> that is fed text, ended or failed on demand
/// (continuations run on the dispatcher, so the test pumps it).
/// </summary>
internal sealed class StudioScript
{
    public List<StudioScriptCall> Calls { get; } = new();

    public StudioScriptCall Last => Calls[^1];

    public PromptRunHandler Handler => (request, token) =>
    {
        var call = new StudioScriptCall(request, token);
        Calls.Add(call);
        return call.Read(token);
    };
}

/// <summary>One scripted run: <see cref="Push"/> text, <see cref="Complete"/> or <see cref="Fail"/>.</summary>
internal sealed class StudioScriptCall(PromptRunRequest request, CancellationToken token)
{
    private static readonly object s_end = new();
    private readonly Queue<object> _items = new();
    private TaskCompletionSource _signal = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public PromptRunRequest Request { get; } = request;

    public CancellationToken Token { get; } = token;

    /// <summary>Whether the handler saw its token cancelled while it waited.</summary>
    public bool WasCancelled { get; private set; }

    public void Push(string text) => Add(text);

    public void Complete() => Add(s_end);

    public void Fail(Exception exception) => Add(exception);

    private void Add(object item)
    {
        _items.Enqueue(item);
        var signal = _signal;
        _signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        signal.TrySetResult();
    }

    public async IAsyncEnumerable<string> Read([EnumeratorCancellation] CancellationToken cancellationToken)
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
            yield return (string)item;
        }
    }
}

/// <summary>A window with the sample studio, a hand-moved clock and input helpers.</summary>
internal sealed class PromptStudioRig : IDisposable
{
    private readonly IDisposable? _frames;

    public PromptStudioRig(Action<PromptStudio>? configure = null, ArcMotionLevel level = ArcMotionLevel.Off, double width = 720,
                           double height = 1100, ManualFrameClock? frames = null, bool simulatorInstant = true)
    {
        _frames = frames?.Install();
        Frames = frames;
        Studio = PromptStudioSamples.Studio(studio =>
        {
            studio.Time = Time;
            if (simulatorInstant)
            {
                studio.Delay = static (_, _) => Task.CompletedTask;
            }
            configure?.Invoke(studio);
        });
        Root = new Border { Padding = new Thickness(20), Child = Studio };
        Window = TestHelpers.Show(Root, level);
        Window.Width = width;
        Window.Height = height;
        Pump();
    }

    public PromptStudio Studio { get; }

    public Border Root { get; }

    public Window Window { get; }

    public StudioClock Time { get; } = new();

    public ManualFrameClock? Frames { get; }

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

    /// <summary>Advances the frame clock by <paramref name="count"/> 60 Hz frames, pumping between them.</summary>
    public void Frame(int count = 1)
    {
        var step = TimeSpan.FromMilliseconds(1000.0 / 60);
        for (var i = 0; i < count; i++)
        {
            Frames?.Advance(step, step);
            Dispatcher.UIThread.RunJobs();
            Window.UpdateLayout();
        }
    }

    /// <summary>Lets the output panel show everything it has (frames until it stops revealing, at most 3 s).</summary>
    public void Reveal()
    {
        for (var i = 0; i < 180 && Studio.Output is { IsRevealing: true }; i++)
        {
            Frame();
        }
        Pump();
    }

    public Point CenterOf(Visual visual) =>
        visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), Window)!.Value;

    public Rect BoundsOf(Visual visual) => new(visual.TranslatePoint(default, Window)!.Value, visual.Bounds.Size);

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
    public bool FocusIsIn(Visual visual) => Focused is Visual focused && (ReferenceEquals(focused, visual) || visual.IsVisualAncestorOf(focused));

    public void Dispose()
    {
        Window.Close();
        _frames?.Dispose();
    }
}
