using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>A window around one reply part, with pointer, key and frame helpers for the reply part tests.</summary>
internal sealed class AiReplyRig : IDisposable
{
    public AiReplyRig(Control content, ArcMotionLevel level = ArcMotionLevel.Off, ManualFrameClock? clock = null, double width = 700)
    {
        Content = content;
        Clock = clock;
        Window = TestHelpers.Show(new StackPanel { Margin = new Thickness(20), Width = width, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, Children = { content } }, level);
        Window.Width = width + 80;
        Window.Height = 600;
        Settle();
        // Let ArcMotion.Tracks hand out the motion pseudo-classes (after Loaded), then start from rest.
        Frames(48);
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

    public Point CenterOf(Visual visual) =>
        visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), Window) ?? default;

    public void Hover(Visual visual)
    {
        Window.MouseMove(CenterOf(visual), RawInputModifiers.None);
        Settle();
    }

    public void Click(Visual visual)
    {
        var point = CenterOf(visual);
        Window.MouseMove(point, RawInputModifiers.None);
        Window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
        Window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
        Settle();
    }

    public void Key(PhysicalKey key)
    {
        Window.KeyPressQwerty(key, RawInputModifiers.None);
        Window.KeyReleaseQwerty(key, RawInputModifiers.None);
        Settle();
    }

    public void Dispose() => Window.Close();
}
