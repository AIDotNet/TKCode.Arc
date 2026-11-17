using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests;

/// <summary>
/// Real pointer and key input for the focus-return tests: focus that a control moves after a click must not show the
/// keyboard ring (<c>:focus-visible</c>); after a key it must.
/// </summary>
internal static class FocusRingInput
{
    public static void Pump()
    {
        for (var i = 0; i < 4; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Dispatcher.UIThread.RunJobs();
    }

    public static Point Center(Visual visual, Visual root) =>
        visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), root)!.Value;

    /// <summary>Moves the mouse to the middle of <paramref name="target"/> and clicks it.</summary>
    public static void Click(Window window, Visual target) => Click(window, target, Center(target, window));

    /// <summary>Moves the mouse to <paramref name="point"/>, waits until it is over <paramref name="target"/>, and clicks.</summary>
    public static void Click(Window window, Visual target, Point point)
    {
        // Hover first and let it render: some buttons only paint (and so only hit-test) once hovered. Headless hit
        // testing reads the last rendered frame, which can lag the layout on a busy machine (a popup that just opened
        // still hit-tests as its empty surface), so wait until the point reaches the target.
        window.MouseMove(point);
        Pump();
        WaitUntilOver(window, target, point);
        window.MouseMove(point);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Pump();
    }

    /// <summary>
    /// Renders until a hit test at <paramref name="point"/> lands on <paramref name="target"/> or inside it. Headless hit
    /// testing reads the last rendered frame, which can lag the layout on a busy machine.
    /// </summary>
    public static void WaitUntilOver(Window window, Visual target, Point point)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!IsOver(window, target, point) && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(1);
            Pump();
        }
        IsOver(window, target, point).ShouldBeTrue($"the point never reached {target.GetType().Name}");
    }

    private static bool IsOver(Window window, Visual target, Point point) =>
        window.InputHitTest(point) is Visual hit && (ReferenceEquals(hit, target) || target.IsVisualAncestorOf(hit));

    public static void Press(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        Pump();
    }

    public static bool HasRing(StyledElement element) => element.Classes.Contains(":focus-visible");

    public static void ShouldBeFocusedWithoutRing(this InputElement element)
    {
        element.IsFocused.ShouldBeTrue($"{element.GetType().Name} should have focus");
        HasRing(element).ShouldBeFalse($"{element.GetType().Name} should not show the keyboard ring after a click");
    }

    public static void ShouldBeFocusedWithRing(this InputElement element)
    {
        element.IsFocused.ShouldBeTrue($"{element.GetType().Name} should have focus");
        HasRing(element).ShouldBeTrue($"{element.GetType().Name} should show the keyboard ring after a key");
    }
}
