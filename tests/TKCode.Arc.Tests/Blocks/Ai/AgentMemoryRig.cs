using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>Waits that finish only when a test says so (the card's simulated save and sort, its holds).</summary>
internal sealed class ManualDelays
{
    private readonly List<(TimeSpan Delay, TaskCompletionSource Done)> _pending = new();

    /// <summary>The waits that have not finished, in the order they started.</summary>
    public IReadOnlyList<TimeSpan> Pending => _pending.Select(p => p.Delay).ToList();

    public Task Delay(TimeSpan delay, CancellationToken token)
    {
        var done = new TaskCompletionSource();
        var entry = (delay, done);
        _pending.Add(entry);
        token.Register(() =>
        {
            _pending.Remove(entry);
            done.TrySetCanceled(token);
        });
        return done.Task;
    }

    /// <summary>Finishes the first pending wait of <paramref name="delay"/>; false when there is none.</summary>
    public bool Finish(TimeSpan delay)
    {
        var index = _pending.FindIndex(p => p.Delay == delay);
        if (index < 0)
        {
            return false;
        }
        var (_, done) = _pending[index];
        _pending.RemoveAt(index);
        done.TrySetResult();
        Dispatcher.UIThread.RunJobs();
        return true;
    }

    /// <summary>Finishes every pending wait (and the ones they start) until none is left.</summary>
    public void FinishAll()
    {
        for (var round = 0; round < 20 && _pending.Count > 0; round++)
        {
            var batch = _pending.ToList();
            _pending.Clear();
            foreach (var (_, done) in batch)
            {
                done.TrySetResult();
            }
            Dispatcher.UIThread.RunJobs();
        }
    }
}

/// <summary>A time source that moves only when told (the undo countdowns).</summary>
internal sealed class ManualMemoryTime : TimeProvider
{
    public DateTimeOffset Now { get; set; } = AgentMemorySamples.Today;

    public override DateTimeOffset GetUtcNow() => Now;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}

/// <summary>
/// A sample card in a 700 x 1000 window with every clock in the test's hands: delays (<see cref="Delays"/>), the undo
/// countdowns (<see cref="Advance"/>) and, at full motion, the frames (<see cref="Frames"/>). Records the events.
/// </summary>
internal sealed class AgentMemoryRig : IDisposable
{
    public AgentMemoryRig(ArcMotionLevel level = ArcMotionLevel.Off, Action<AgentMemory>? configure = null, bool captured = true,
                          double width = 700, bool useFrameClock = false)
    {
        if (useFrameClock || level != ArcMotionLevel.Off)
        {
            Frames = new ManualFrameClock();
            _clock = Frames.Install();
        }
        Clock = new UndoClock(Time);
        Memory = AgentMemorySamples.Create(captured);
        Memory.DelayAsync = Delays.Delay;
        Memory.UndoClock = Clock;
        Memory.Time = Time;
        Memory.MemoryChanged += (_, e) => Changes.Add(e);
        Memory.SettingToggled += (_, e) => Toggles.Add(e);
        configure?.Invoke(Memory);
        Window = TestHelpers.Show(Memory, level);
        Window.Width = width;
        Window.Height = 1000;
        Pump();
    }

    private readonly IDisposable? _clock;

    public AgentMemory Memory { get; }
    public Window Window { get; }
    public ManualDelays Delays { get; } = new();
    public ManualMemoryTime Time { get; } = new();
    public UndoClock Clock { get; }
    public ManualFrameClock? Frames { get; }
    public List<MemoryChangedEventArgs> Changes { get; } = new();
    public List<MemorySettingToggledEventArgs> Toggles { get; } = new();

    public MemoryItem Item(string id) => Memory.MemoryList.First(m => m.Id == id);

    /// <summary>Runs the jobs and a layout.</summary>
    public void Pump()
    {
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Moves the undo time on and ticks the countdowns, as their timer would.</summary>
    public void Advance(double seconds)
    {
        Time.Now += TimeSpan.FromSeconds(seconds);
        Clock.Tick();
        Pump();
    }

    /// <summary>
    /// Runs frames (full motion) for <paramref name="seconds"/> in 50ms steps, with the posted jobs in between: a
    /// crossfade removes its old layer from a thread-pool continuation, and the row height follows only after that.
    /// </summary>
    public void Run(double seconds)
    {
        var step = TimeSpan.FromMilliseconds(50);
        for (var elapsed = 0.0; elapsed < seconds - 1e-9; elapsed += step.TotalSeconds)
        {
            Frames?.Advance(step);
            for (var i = 0; i < 3; i++)
            {
                Dispatcher.UIThread.RunJobs();
                Thread.Sleep(1);
            }
            Pump();
        }
    }

    public void Key(PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        Window.KeyPressQwerty(key, modifiers);
        Window.KeyReleaseQwerty(key, modifiers);
        Pump();
    }

    public void Type(string text)
    {
        Window.KeyTextInput(text);
        Pump();
    }

    public Visual? Focused => Window.FocusManager?.GetFocusedElement() as Visual;

    public bool IsFocusWithin(Visual visual) => Focused is { } focused && (ReferenceEquals(focused, visual) || visual.IsVisualAncestorOf(focused));

    public void Dispose()
    {
        Window.Close();
        _clock?.Dispose();
    }
}
