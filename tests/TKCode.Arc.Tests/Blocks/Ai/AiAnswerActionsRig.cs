using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// A window around one of the answer's action parts, with a button before it (to Tab in from), a button after it (to press
/// outside), pointer, key and frame helpers. With a <see cref="ManualFrameClock"/> the clock is installed before the window
/// opens, so no motion asks the real clock for a frame.
/// </summary>
internal sealed class AiAnswerActionsRig : IDisposable
{
    private readonly IDisposable? _clockInstall;

    public AiAnswerActionsRig(Control content, ArcMotionLevel level = ArcMotionLevel.Off, ManualFrameClock? clock = null,
        double width = 680, double windowHeight = 700, double top = 320)
    {
        Clock = clock;
        _clockInstall = clock?.Install();
        Content = content;
        Before = new Button { Content = "Before", Height = 20 };
        After = new Button { Content = "After", Height = 20 };
        var column = new StackPanel
        {
            Margin = new Thickness(32, top, 32, 0),
            Width = width,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            Children = { Before, content, After }
        };
        Window = TestHelpers.Show(column, level);
        Window.Width = width + 64;
        Window.Height = windowHeight;
        Settle();
        // Let ArcMotion.Tracks hand out the motion pseudo-classes (after Loaded), then start from rest.
        Frames(48);
    }

    public Control Content { get; }

    public Window Window { get; }

    public Button Before { get; }

    public Button After { get; }

    public ManualFrameClock? Clock { get; }

    /// <summary>Runs jobs and a layout pass (and a render tick, so hit testing sees the latest layout).</summary>
    public void Settle()
    {
        Dispatcher.UIThread.RunJobs();
        Window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Advances the manual clock (if any) by <paramref name="milliseconds"/> in 60 Hz frames, settling after each.</summary>
    public void Frames(int milliseconds)
    {
        if (Clock is null)
        {
            Settle();
            return;
        }
        for (var elapsed = 0.0; elapsed < milliseconds - 0.01; elapsed += 1000.0 / 60)
        {
            Clock.Advance(TimeSpan.FromMilliseconds(1000.0 / 60));
            Settle();
        }
    }

    public Point CenterOf(Visual visual) =>
        visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), Window) ?? default;

    /// <summary>The visual's bounds in window coordinates.</summary>
    public Rect BoundsOf(Visual visual) =>
        visual.TranslatePoint(default, Window) is { } origin ? new Rect(origin, visual.Bounds.Size) : default;

    public void Hover(Visual visual)
    {
        Window.MouseMove(CenterOf(visual), RawInputModifiers.None);
        Settle();
    }

    public void MoveTo(Point point)
    {
        Window.MouseMove(point, RawInputModifiers.None);
        Settle();
    }

    public void Click(Visual visual) => Click(CenterOf(visual));

    public void Click(Point point)
    {
        Window.MouseMove(point, RawInputModifiers.None);
        Window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
        Settle();
        Window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
        Settle();
    }

    public void Key(PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        Window.KeyPressQwerty(key, modifiers);
        Window.KeyReleaseQwerty(key, modifiers);
        Settle();
    }

    /// <summary>Gives continuations that left the UI thread time to come back, until <paramref name="done"/> (at most 1s).</summary>
    public bool Drain(Func<bool> done)
    {
        for (var i = 0; i < 200 && !done(); i++)
        {
            Thread.Sleep(5);
            Settle();
        }
        return done();
    }

    /// <summary>The focused element of the window.</summary>
    public IInputElement? Focused => Window.FocusManager?.GetFocusedElement();

    public void Dispose()
    {
        Window.Close();
        _clockInstall?.Dispose();
    }
}

/// <summary>A clock tests move by hand.</summary>
internal sealed class AiAnswerFakeTime : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => Now;
}
