using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests;

internal static class TestHelpers
{
    /// <summary>Shows content in a window. Motion is off by default so assertions are deterministic.</summary>
    public static Window Show(Control content, Motion.ArcMotionLevel level = Motion.ArcMotionLevel.Off)
    {
        // Arc's base Window theme gives the window a layer manager with the overlay and popup overlay layers, which headless
        // popups fall back to.
        var window = new Window { Content = content, Width = 400, Height = 300 };
        Motion.ArcMotion.SetLevel(window, level);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    /// <summary>
    /// Runs the dispatcher (timers and posted work) until <paramref name="condition"/> holds or <paramref name="timeoutMs"/>
    /// passes. Use it instead of a fixed real-time window: it returns as soon as the condition holds and only a genuinely
    /// stuck state runs into the timeout.
    /// </summary>
    public static bool PumpUntil(Func<bool> condition, int timeoutMs = 10000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (true)
        {
            Dispatcher.UIThread.RunJobs();
            if (condition())
            {
                return true;
            }
            if (DateTime.UtcNow >= deadline)
            {
                return false;
            }
            var frame = new DispatcherFrame();
            DispatcherTimer.RunOnce(() => frame.Continue = false, TimeSpan.FromMilliseconds(10));
            Dispatcher.UIThread.PushFrame(frame);
        }
    }

    /// <summary>
    /// Waits (up to <paramref name="timeoutMs"/>) for an exit-presence control to drop its outgoing copies, then asserts none
    /// are left. Controls remove the old copy from a continuation that hops through the thread pool and posts to the UI
    /// thread, so on a loaded runner it can land after the manual clock has already finished the motion.
    /// </summary>
    public static void ShouldDrain(Func<int> exitingCount, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        Dispatcher.UIThread.RunJobs();
        while (exitingCount() != 0 && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(5);
            Dispatcher.UIThread.RunJobs();
        }
        exitingCount().ShouldBe(0);
    }

    /// <summary>First visual descendant of type <typeparamref name="T"/> (template parts included).</summary>
    public static T Descendant<T>(Visual root) where T : Visual => root.GetVisualDescendants().OfType<T>().First();

    /// <summary>
    /// Creates a control with <paramref name="create"/>, shows it in a window, closes the window and returns a weak
    /// reference to it. Call it from a test that keeps the control's data alive, then <see cref="Collect"/>: a control that
    /// still listens to that data stays alive. The control is created in here (not by the caller) so no hidden temporary in
    /// the test keeps it; <paramref name="whileShown"/> runs while it is in the window.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static WeakReference ShowAndClose<T>(Func<T> create, Action<T>? whileShown = null, Motion.ArcMotionLevel level = Motion.ArcMotionLevel.Off)
        where T : Control
    {
        var control = create();
        var window = Show(control, level);
        whileShown?.Invoke(control);
        Dispatcher.UIThread.RunJobs();
        window.Content = null;
        window.Close();
        Dispatcher.UIThread.RunJobs();
        return new WeakReference(control);
    }

    /// <summary>
    /// Collects garbage until every reference is dead (or a few rounds have passed). Each round first commits a frame: a
    /// window that stays open keeps the visuals removed from it in its compositor until the next frame.
    /// </summary>
    public static void Collect(params WeakReference[] references)
    {
        ClearGridScratch();
        for (var i = 0; i < 6 && references.Any(r => r.IsAlive); i++)
        {
            Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static readonly LocalDataStoreSlot? s_gridScratchSlot =
        typeof(Grid).GetField("s_tempDefinitionsDataSlot", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)?
            .GetValue(null) as LocalDataStoreSlot;

    /// <summary>
    /// Empties the scratch array Avalonia's <see cref="Grid"/> shares per thread. A grid writes its row and column
    /// definitions into it while it measures and clears them when it arranges; a grid measured again with a new constraint
    /// but not arranged again (its bounds did not change) leaves them there, and every such grid holds the array. So the
    /// array can keep the definitions (and through them the whole tree) of a control a test has dropped, as long as some
    /// other grid is alive. With per-test isolation that other grid is often in an earlier test's tree, kept by a static
    /// geometry or pen that still points at that test's compositor and dispatcher; which grids those are depends on the
    /// order the tests run in. That reference belongs to Avalonia, not to the control under test.
    /// </summary>
    private static void ClearGridScratch()
    {
        if (s_gridScratchSlot is not null && Thread.GetData(s_gridScratchSlot) is WeakReference { Target: Array scratch })
        {
            Array.Clear(scratch);
        }
    }

    /// <summary>First visual descendant of type <typeparamref name="T"/> with the given name.</summary>
    public static T Descendant<T>(Visual root, string name) where T : Control =>
        root.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    public static Color ColorOf(object? value) => value switch
    {
        ISolidColorBrush solid => solid.Color,
        Color color => color,
        _ => Colors.Transparent
    };

    /// <summary>Changes for <see cref="ApplyAsync"/>: the dark appearance.</summary>
    public static readonly Action<ArcThemeBuilder> Dark = static t => t.Appearance(ArcAppearance.Dark);

    /// <summary>
    /// Applies a theme state built from the default one (light, default accent, full motion, no overrides) by
    /// <paramref name="configure"/>; null applies the default state. Like an AtomUI theme request, nothing of the previous
    /// state is kept. Runs the dispatcher's jobs afterwards.
    /// </summary>
    public static async Task ApplyAsync(Action<ArcThemeBuilder>? configure)
    {
        var state = configure is null ? ArcThemeState.Default : ArcThemeState.Default.With(configure);
        ArcTheme.Manager.Apply(state);
        await Task.CompletedTask;
        Dispatcher.UIThread.RunJobs();
    }
}
