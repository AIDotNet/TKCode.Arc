using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using StreamMark.Avalonia;
using StreamMark.Avalonia.Extensions;
using StreamMark.Avalonia.Themes;
using StreamMark.Avalonia.Transcript;
using StreamMark.Highlighting.TextMate;
using StreamMark.Mermaid;
using StreamMark.RaTeX;

namespace StreamMark.Gallery;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Contains("--smoke"))
        {
            return SmokeTest.Run(args);
        }
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<GalleryApp>().UsePlatformDetect();

    /// <summary>渲染扩展：公式（RaTeX）、代码高亮（TextMate）、图表（Mermaid）。</summary>
    public static MarkdownServices CreateServices()
    {
        return new MarkdownServices
        {
            Plugins =
            [
                new RaTeXPlugin(),
                new TextMatePlugin(prewarm: ["csharp", "python", "typescript", "json", "bash"]),
                new MermaidPlugin(),
            ],
        };
    }

    public static string Sample(string name)
    {
        using var stream = typeof(Program).Assembly.GetManifestResourceStream($"Samples.{name}")
                           ?? throw new InvalidOperationException($"Missing sample {name}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Replace("\r\n", "\n");
    }
}

public sealed class GalleryApp : Application
{
    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Dark;
        Styles.Add(new global::Avalonia.Themes.Fluent.FluentTheme());
        Styles.Add(new StreamMarkTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new GalleryWindow();
        }
        base.OnFrameworkInitializationCompleted();
    }
}

/// <summary>演示窗口：一个会话视图，顶部按钮模拟流式回答。</summary>
public sealed class GalleryWindow : Window
{
    public GalleryWindow()
    {
        Title = "StreamMark Gallery";
        Width = 1000;
        Height = 820;
        Background = new SolidColorBrush(Color.FromRgb(0x12, 0x12, 0x16));
        var services = Program.CreateServices();
        MarkdownServices.SetServices(this, services);
        Profile = services.Profile;

        Transcript = new TranscriptView { MaxContentWidth = 820, Padding = new Thickness(0, 12, 0, 24) };
        Transcript.Entries.Add(new ContentEntry(Bubble("展示一下 StreamMark 支持的内容")));
        Transcript.Entries.Add(MarkdownEntry.FromText(Program.Sample("showcase.md"), Profile));

        var stream = new Button { Content = "模拟流式回答", Margin = new Thickness(12), HorizontalAlignment = HorizontalAlignment.Left };
        stream.Click += (_, _) => _ = SimulateAsync();
        var root = new DockPanel();
        DockPanel.SetDock(stream, Dock.Top);
        root.Children.Add(stream);
        root.Children.Add(Transcript);
        Content = root;
    }

    public TranscriptView Transcript { get; }

    /// <summary>与插件匹配的 Markdown 方言。</summary>
    public StreamMark.Core.Parsing.MarkdownProfile Profile { get; }

    public static Control Bubble(string text) => new Border
    {
        Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x33)),
        CornerRadius = new CornerRadius(14),
        Padding = new Thickness(14, 9),
        HorizontalAlignment = HorizontalAlignment.Right,
        Child = new SelectableTextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.White },
    };

    /// <summary>以不均匀的分片模拟网络流，展示平滑输出与渐显。</summary>
    public async Task SimulateAsync(int delayMs = 30)
    {
        Transcript.Entries.Add(new ContentEntry(Bubble("再来一遍，这次流式输出")));
        var markdownStream = new MarkdownStream(profile: Profile);
        Transcript.Entries.Add(new MarkdownEntry(markdownStream));
        Transcript.ScrollToEnd();
        var text = Program.Sample("showcase.md");
        var random = new Random(7);
        for (var i = 0; i < text.Length;)
        {
            var size = Math.Min(random.Next(2, 24), text.Length - i);
            markdownStream.Push(text.Substring(i, size));
            i += size;
            if (delayMs > 0)
            {
                await Task.Delay(random.Next(delayMs / 2, delayMs * 2));
            }
        }
        markdownStream.Complete();
    }
}
