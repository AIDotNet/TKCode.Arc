using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// A window around a document extraction field part (a row, a table, an indicator or a whole sample form) with the frame
/// clock in the test's hands at Full and Reduced motion, and pointer, key and typing helpers.
/// </summary>
internal sealed class ExtractionFieldRig : IDisposable
{
    private readonly IDisposable? _install;

    public ExtractionFieldRig(Control content, ArcMotionLevel level = ArcMotionLevel.Off, double width = 410, double height = 900)
    {
        if (level != ArcMotionLevel.Off)
        {
            Clock = new ManualFrameClock();
            _install = Clock.Install();
        }
        Content = content;
        content.HorizontalAlignment = HorizontalAlignment.Left;
        content.VerticalAlignment = VerticalAlignment.Top;
        if (double.IsNaN(content.Width))
        {
            content.Width = width;
        }
        Window = TestHelpers.Show(new Border { Padding = new Thickness(20), Child = content }, level);
        Window.Width = width + 80;
        Window.Height = height;
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
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Advances the manual clock (if any) by <paramref name="milliseconds"/> in 60 Hz frames, settling after each.</summary>
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

    /// <summary>Gives continuations that left the UI thread (a crossfade's old layer) time to come back, until <paramref name="done"/>.</summary>
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

    public void MoveTo(Point point)
    {
        Window.MouseMove(point, RawInputModifiers.None);
        Settle();
    }

    public void Hover(Visual visual) => MoveTo(CenterOf(visual));

    /// <summary>Moves the pointer to the window's corner (outside every part).</summary>
    public void Leave() => MoveTo(new Point(2, Window.Height - 2));

    public void Click(Visual visual)
    {
        var point = CenterOf(visual);
        Window.MouseMove(point, RawInputModifiers.None);
        Window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
        Window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
        Settle();
    }

    public void Key(PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        Window.KeyPressQwerty(key, modifiers);
        Window.KeyReleaseQwerty(key, modifiers);
        Settle();
    }

    public void Type(string text)
    {
        Window.KeyTextInput(text);
        Settle();
    }

    public IInputElement? Focused => TopLevel.GetTopLevel(Window)?.FocusManager?.GetFocusedElement();

    public void Dispose()
    {
        Window.Close();
        _install?.Dispose();
    }
}
