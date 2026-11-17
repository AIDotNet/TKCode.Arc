using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Terminal.Avalonia;

namespace TKCode.Arc.TerminalMobileSample;

/// <summary>
/// Runs <see cref="TerminalCheck"/> against the bundled libghostty-vt, shows the captured frame in a
/// <see cref="TerminalView"/> and, once the view has been laid out, writes one line to the console (the iOS simulator's
/// console, Android's logcat): <c>TERMINAL-SAMPLE: PASS ghostty=... cell=WxH@S ...</c> or <c>TERMINAL-SAMPLE: FAIL ...</c>.
/// </summary>
public sealed class App : Avalonia.Application
{
    public override void OnFrameworkInitializationCompleted()
    {
        var view = new TerminalView { Height = 8 * 24, FontSize = 14 };
        var status = new TextBlock { Margin = new Thickness(16), TextWrapping = TextWrapping.Wrap, Foreground = Brushes.White };
        var root = new StackPanel
        {
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Background = Brushes.Black,
            Children = { view, status },
        };
        switch (ApplicationLifetime)
        {
            case ISingleViewApplicationLifetime single:
                single.MainView = root;
                break;
            case IClassicDesktopStyleApplicationLifetime desktop:
                desktop.MainWindow = new Window { Width = 480, Height = 400, Content = root };
                break;
        }

        TerminalCheck.Result result;
        try
        {
            result = TerminalCheck.Run();
        }
        catch (Exception e)
        {
            // DllNotFoundException / EntryPointNotFoundException when the native library is missing from the app.
            result = new TerminalCheck.Result(false, $"{e.GetType().Name}: {e.Message}", null);
        }
        view.Snapshot = result.Frame;
        status.Text = result.Detail;
        Report(view, status, result);
        base.OnFrameworkInitializationCompleted();
    }

    private static void Report(TerminalView view, TextBlock status, TerminalCheck.Result result)
    {
        var ticks = 0;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        timer.Tick += (_, _) =>
        {
            ticks++;
            var laidOut = view.Bounds.Width > 0 && view.IsArrangeValid;
            if (!laidOut && ticks < 40)
            {
                return;
            }
            timer.Stop();
            var metrics = laidOut ? view.Metrics : null;
            var scaling = TopLevel.GetTopLevel(view)?.RenderScaling ?? 0;
            // A monospace face (an advance of about 0.6 em; a proportional face's widest ASCII glyph is about 0.9 em),
            // measured in device pixels at the screen's scaling.
            var monospace = metrics is not null && metrics.CellWidth / metrics.Scaling <= view.FontSize * 0.7;
            var scaled = metrics is not null && Math.Abs(metrics.Scaling - scaling) < 0.01;
            var passed = result.Passed && metrics is { CellWidth: > 0, CellHeight: > 0 } && monospace && scaled;
            var line = $"{result.Detail} cell={metrics?.CellWidth ?? 0}x{metrics?.CellHeight ?? 0}@{metrics?.Scaling ?? 0:0.##}x " +
                       $"screen={scaling:0.##}x monospace={monospace}";
            status.Text = line;
            Console.WriteLine($"TERMINAL-SAMPLE: {(passed ? "PASS" : "FAIL")} {line}");
        };
        timer.Start();
    }
}
