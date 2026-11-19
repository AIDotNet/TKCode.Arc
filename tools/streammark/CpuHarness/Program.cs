using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using StreamMark.Avalonia;
using StreamMark.Avalonia.Extensions;
using StreamMark.Avalonia.Themes;
using StreamMark.Avalonia.Transcript;
using StreamMark.Core.Pacing;
using StreamMark.Highlighting.TextMate;

namespace StreamMark.CpuHarness;

/// <summary>
/// 真实窗口的流式 CPU 测量：打开会话视图（含若干条历史消息），按目标速率持续流式输出样例文本，
/// 预热后统计进程 CPU、界面线程 CPU、分配速率与 GC 次数，输出一行结果后退出。不产生任何系统输入。
/// 用 NativeAOT 发布后运行（tools/cpu-bench.sh）；JIT 构建的结果包含后台编译，不可比。
/// </summary>
/// <remarks>
/// 环境变量：SM_LABEL 结果标签；SM_OUT 追加结果的文件；SM_SECONDS 统计时长（默认 15）；SM_WARMUP 预热秒数（默认 3）；
/// SM_SAMPLE 样例文件（默认内嵌的 chat.md）；SM_RATE 字符/秒（默认 320）；SM_FADE_MS 渐显时长（0 关闭）；
/// SM_SMOOTH 0 关闭平滑；SM_HISTORY 历史消息条数（默认 10）；SM_CLEAR 1 每轮清空会话（不滚动）；
/// SM_IDLE 1 只测空闲；SM_GRADIENT 1 渐变窗口背景；SM_RENDER gl 在 macOS 上优先 OpenGL（默认 Metal）；
/// SM_REGION 1 启用区域脏矩形（CompositionOptions.UseRegionDirtyRectClipping）；SM_STAY 1 结束后不退出。
/// </remarks>
public static class Program
{
    [STAThread]
    public static int Main(string[] args) =>
        Configure().StartWithClassicDesktopLifetime(args);

    private static AppBuilder Configure()
    {
        var builder = AppBuilder.Configure<HarnessApp>().UsePlatformDetect();
        if (Env("SM_REGION", "0") == "1")
        {
            builder = builder.With(new global::Avalonia.Rendering.Composition.CompositionOptions { UseRegionDirtyRectClipping = true });
        }
        return Env("SM_RENDER", "metal") == "gl"
            ? builder.With(new AvaloniaNativePlatformOptions { RenderingMode = [AvaloniaNativeRenderingMode.OpenGl, AvaloniaNativeRenderingMode.Metal, AvaloniaNativeRenderingMode.Software] })
            : builder.With(new AvaloniaNativePlatformOptions { RenderingMode = [AvaloniaNativeRenderingMode.Metal, AvaloniaNativeRenderingMode.OpenGl, AvaloniaNativeRenderingMode.Software] });
    }

    public static string Env(string name, string fallback) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } v ? v : fallback;
    public static int EnvInt(string name, int fallback) => int.Parse(Env(name, fallback.ToString()));

    public static string Sample()
    {
        if (Environment.GetEnvironmentVariable("SM_SAMPLE") is { Length: > 0 } path)
        {
            return File.ReadAllText(path).Replace("\r\n", "\n");
        }
        using var stream = typeof(Program).Assembly.GetManifestResourceStream("chat.md")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Replace("\r\n", "\n");
    }
}

public sealed class HarnessApp : Application
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
            desktop.MainWindow = new HarnessWindow();
        }
        base.OnFrameworkInitializationCompleted();
    }
}

public sealed class HarnessWindow : Window
{
    private readonly TranscriptView _transcript;
    private readonly MarkdownServices _services;

    public HarnessWindow()
    {
        Title = "StreamMark CPU Harness";
        Width = 1100;
        Height = 800;
        Background = Program.Env("SM_GRADIENT", "0") == "1"
            ? new RadialGradientBrush
            {
                Center = new RelativePoint(0.5, 0, RelativeUnit.Relative),
                GradientOrigin = new RelativePoint(0.5, 0, RelativeUnit.Relative),
                RadiusX = new RelativeScalar(0.9, RelativeUnit.Relative),
                RadiusY = new RelativeScalar(0.9, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.FromRgb(0x3A, 0x2A, 0x66), 0), new GradientStop(Color.FromRgb(0x12, 0x12, 0x16), 1) },
            }
            : new SolidColorBrush(Color.FromRgb(0x12, 0x12, 0x16));
        _services = new MarkdownServices
        {
            Plugins = [new TextMatePlugin(prewarm: ["csharp", "python", "json"])],
        };
        MarkdownServices.SetServices(this, _services);
        _transcript = new TranscriptView { MaxContentWidth = 760, Padding = new Thickness(32, 8, 32, 16), EntrySpacing = 12 };
        var history = Program.EnvInt("SM_HISTORY", 10);
        var sample = Program.Sample();
        for (var i = 0; i < history; i++)
        {
            _transcript.Entries.Add(new ContentEntry(Bubble($"历史问题 {i}")));
            _transcript.Entries.Add(MarkdownEntry.FromTextDeferred(sample, _services.Profile));
        }
        Content = _transcript;
        Opened += (_, _) => _ = RunAsync();
    }

    private static Control Bubble(string text) => new Border
    {
        Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x33)),
        CornerRadius = new CornerRadius(14),
        Padding = new Thickness(14, 9),
        HorizontalAlignment = HorizontalAlignment.Right,
        Child = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.White },
    };

    private async Task RunAsync()
    {
        var label = Program.Env("SM_LABEL", "run");
        var seconds = Program.EnvInt("SM_SECONDS", 15);
        var warmup = Program.EnvInt("SM_WARMUP", 3);
        var idle = Program.Env("SM_IDLE", "0") == "1";
        _transcript.ScrollToEnd();
        await Task.Delay(1500);

        var cts = new CancellationTokenSource();
        var streaming = idle ? Task.CompletedTask : StreamLoopAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(warmup));

        var process = Process.GetCurrentProcess();
        process.Refresh();
        var cpu0 = process.TotalProcessorTime;
        var ui0 = ThreadCpu.Now();
        var threads0 = ThreadTimes(process);
        var gc0 = GC.CollectionCount(0);
        var alloc0 = GC.GetTotalAllocatedBytes();
        var pause0 = GC.GetTotalPauseDuration();
        var wall = Stopwatch.StartNew();

        await Task.Delay(TimeSpan.FromSeconds(seconds));

        wall.Stop();
        process.Refresh();
        var cpu = (process.TotalProcessorTime - cpu0).TotalSeconds;
        var ui = (ThreadCpu.Now() - ui0).TotalSeconds;
        var threads1 = ThreadTimes(process);
        var w = wall.Elapsed.TotalSeconds;
        var top = threads1.Select(p => (Id: p.Key, Delta: p.Value - threads0.GetValueOrDefault(p.Key))).OrderByDescending(p => p.Delta).Take(6)
            .Select(p => $"{p.Id}:{p.Delta.TotalSeconds / w * 100:F1}%");
        var line = $"{label,-28} cpu={cpu / w * 100,6:F1}%  ui={ui / w * 100,6:F1}%  other={(cpu - ui) / w * 100,6:F1}%  " +
                   $"alloc={(GC.GetTotalAllocatedBytes() - alloc0) / w / 1024 / 1024:F1}MB/s gc0={GC.CollectionCount(0) - gc0} gcpause={(GC.GetTotalPauseDuration() - pause0).TotalMilliseconds:F0}ms  threads=[{string.Join(" ", top)}]";
        Console.WriteLine(line);
        if (Environment.GetEnvironmentVariable("SM_OUT") is { Length: > 0 } output)
        {
            File.AppendAllText(output, line + Environment.NewLine);
        }
        cts.Cancel();
        if (Program.Env("SM_STAY", "0") != "1")
        {
            (Application.Current!.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)!.Shutdown();
        }
    }

    private static Dictionary<int, TimeSpan> ThreadTimes(Process process)
    {
        var result = new Dictionary<int, TimeSpan>();
        try
        {
            foreach (ProcessThread t in process.Threads)
            {
                try
                {
                    result[t.Id] = t.TotalProcessorTime;
                }
                catch
                {
                }
            }
        }
        catch
        {
        }
        return result;
    }

    private async Task StreamLoopAsync(CancellationToken ct)
    {
        var rate = Program.EnvInt("SM_RATE", 320);
        var fadeMs = Program.EnvInt("SM_FADE_MS", 220);
        var smooth = Program.Env("SM_SMOOTH", "1") == "1";
        var text = Program.Sample();
        var random = new Random(7);
        var round = 0;
        var clear = Program.Env("SM_CLEAR", "0") == "1";
        while (!ct.IsCancellationRequested)
        {
            if (clear)
            {
                _transcript.Entries.Clear();
            }
            _transcript.Entries.Add(new ContentEntry(Bubble($"第 {++round} 轮")));
            var stream = new MarkdownStream(smooth: smooth, fadeDuration: TimeSpan.FromMilliseconds(fadeMs), profile: _services.Profile);
            _transcript.Entries.Add(new MarkdownEntry(stream));
            var clock = Stopwatch.StartNew();
            var sent = 0;
            while (sent < text.Length && !ct.IsCancellationRequested)
            {
                var size = Math.Min(random.Next(2, 24), text.Length - sent);
                stream.Push(text.Substring(sent, size));
                sent += size;
                // 按目标速率发送：到达时间 = 已发送字符 / 速率
                var due = TimeSpan.FromSeconds(sent / (double)rate) - clock.Elapsed;
                if (due > TimeSpan.Zero)
                {
                    await Task.Delay(due, CancellationToken.None);
                }
            }
            stream.Complete();
            await Task.Delay(800, CancellationToken.None);
        }
    }
}

/// <summary>当前线程（界面线程）的 CPU 时间：macOS / Linux 用 clock_gettime，Windows 不支持时返回 0。</summary>
internal static class ThreadCpu
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Timespec
    {
        public long Sec;
        public long Nsec;
    }

    [DllImport("libc")]
    private static extern int clock_gettime(int clockId, out Timespec ts);

    public static TimeSpan Now()
    {
        if (OperatingSystem.IsWindows())
        {
            return TimeSpan.Zero;
        }
        // CLOCK_THREAD_CPUTIME_ID：macOS 16，Linux 3
        clock_gettime(OperatingSystem.IsMacOS() ? 16 : 3, out var ts);
        return TimeSpan.FromTicks(ts.Sec * TimeSpan.TicksPerSecond + ts.Nsec / 100);
    }
}
