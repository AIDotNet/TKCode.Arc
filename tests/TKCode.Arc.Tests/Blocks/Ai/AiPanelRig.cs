using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>A window of a given size around one side panel part, with frame, pointer and key helpers.</summary>
internal sealed class AiPanelRig : IDisposable
{
    private readonly IDisposable? _clockScope;

    public AiPanelRig(Control content, double width = 1080, double height = 900, ArcMotionLevel level = ArcMotionLevel.Off,
                      ManualFrameClock? clock = null)
    {
        Content = content;
        Clock = clock;
        // The clock goes in before anything can ask for a frame (a spring that asks the real clock never gets one).
        _clockScope = clock?.Install();
        Window = TestHelpers.Show(content, level);
        Window.Width = width;
        Window.Height = height;
        Settle();
        Frames(800);
    }

    public Control Content { get; }

    public Window Window { get; }

    public ManualFrameClock? Clock { get; }

    /// <summary>Runs jobs and a layout pass.</summary>
    public void Settle()
    {
        Dispatcher.UIThread.RunJobs();
        Window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        Window.UpdateLayout();
    }

    /// <summary>Advances the manual clock by <paramref name="milliseconds"/> in 60 Hz frames, settling after each.</summary>
    public void Frames(double milliseconds)
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

    /// <summary>One 60 Hz frame.</summary>
    public void Frame() => FrameCount(1);

    /// <summary>Exactly <paramref name="count"/> 60 Hz frames (a motion value has run count / 60 s after them).</summary>
    public void FrameCount(int count)
    {
        for (var i = 0; i < count; i++)
        {
            Clock?.Advance(TimeSpan.FromMilliseconds(1000.0 / 60));
            Settle();
        }
    }

    public Point PointIn(Visual visual, Point local) => visual.TranslatePoint(local, Window) ?? default;

    public Point CenterOf(Visual visual) => PointIn(visual, new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2));

    public void Down(Point point, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        Window.MouseMove(point, modifiers);
        Window.MouseDown(point, MouseButton.Left, modifiers);
        Settle();
    }

    public void Drag(Point point)
    {
        Window.MouseMove(point, RawInputModifiers.LeftMouseButton);
        Settle();
    }

    public void Up(Point point)
    {
        Window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
        Settle();
    }

    public void Click(Point point)
    {
        Down(point);
        Up(point);
    }

    public void Click(Visual visual) => Click(CenterOf(visual));

    public void Key(PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        Window.KeyPressQwerty(key, modifiers);
        Window.KeyReleaseQwerty(key, modifiers);
        Settle();
    }

    public void Dispose()
    {
        Window.Close();
        _clockScope?.Dispose();
    }
}
