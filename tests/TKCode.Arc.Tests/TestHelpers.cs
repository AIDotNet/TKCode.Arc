using AtomUI;
using AtomUI.Theme;
using AtomUI.Theme.Configuration;
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
        // Headless has no popup windows: popups fall back to this layer manager's overlay layer.
        var layers = new Avalonia.Controls.Primitives.VisualLayerManager { Child = content };
        typeof(Avalonia.Controls.Primitives.VisualLayerManager)
            .GetProperty("EnablePopupOverlayLayer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?
            .SetValue(layers, true);
        var window = new Window { Content = layers, Width = 400, Height = 300 };
        Motion.ArcMotion.SetLevel(window, level);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
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
        for (var i = 0; i < 6 && references.Any(r => r.IsAlive); i++)
        {
            Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Dispatcher.UIThread.RunJobs();
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

    public static async Task ApplyAsync(ThemeConfig? config)
    {
        var manager = Application.Current!.GetThemeManager()!;
        await manager.ApplyThemeAsync(new ThemeRequest(IThemeManager.DEFAULT_THEME_ID, config, ThemeTransitionReason.UserRequest),
            TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();
    }
}
