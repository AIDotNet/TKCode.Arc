using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Foundation.Tests;

internal static class TestHelpers
{
    public static Window Show(Control content)
    {
        var window = new Window { Width = 400, Height = 300, Content = content };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    public static Color BrushColor(IBrush? brush)
        => brush is ISolidColorBrush solid ? solid.Color : throw new InvalidOperationException($"Not a solid brush: {brush}");

    public static object? AppResource(object key)
        => Application.Current!.TryGetResource(key, Application.Current.ActualThemeVariant, out var value) ? value : null;

    public static T Part<T>(Visual root, string name) where T : Control
        => root.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    public static Color Mix(Color a, Color b, double amount)
        => Samples.SampleDesignToken.Mix(a, b, amount);
}
