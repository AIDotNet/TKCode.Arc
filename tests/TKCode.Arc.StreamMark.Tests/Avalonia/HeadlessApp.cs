using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using StreamMark.Avalonia.Themes;

[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]

namespace StreamMark.Avalonia.Tests;

/// <summary>无头测试宿主：加载 StreamMark 默认主题，用 Skia 真实渲染以便截图。</summary>
public sealed class TestApp : Application
{
    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Dark;
        // 会话视图沿用宿主主题中的 ScrollViewer 模板
        Styles.Add(new global::Avalonia.Themes.Fluent.FluentTheme());
        Styles.Add(new StreamMarkTheme());
    }
}

public static class HeadlessApp
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApp>()
                  .UseSkia()
                  .UseHarfBuzz()
                  .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });

    private static readonly Lazy<HeadlessUnitTestSession> Shared = new(() => HeadlessUnitTestSession.StartNew(typeof(HeadlessApp)));

    public static HeadlessUnitTestSession Session => Shared.Value;

    /// <summary>截图目录：环境变量 STREAMMARK_SNAPSHOT_DIR，默认测试输出目录下的 snapshots/。</summary>
    public static string SnapshotPath(string name)
    {
        var dir = Environment.GetEnvironmentVariable("STREAMMARK_SNAPSHOT_DIR") ?? Path.Combine(AppContext.BaseDirectory, "snapshots");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, name + ".png");
    }

    public static void Save(TopLevel topLevel, string name)
    {
        using var frame = Capture(topLevel);
        using var file = File.Create(SnapshotPath(name));
        frame.Save(file, new PngBitmapEncoderOptions());
    }

    /// <summary>
    /// 在界面线程上运行异步测试体并等待其完成。
    /// （直接把 async lambda 交给 Dispatch 会匹配 Func&lt;TResult&gt; 重载，外层任务在测试体完成前就返回。）
    /// </summary>
    public static async Task Run(Func<Task> body)
    {
        await Session.Dispatch(async () =>
        {
            await body();
            return true;
        }, CancellationToken.None);
    }

    /// <summary>处理界面线程上的待办任务与渲染帧，让异步更新落定。</summary>
    public static async Task Settle(int rounds = 5)
    {
        for (var i = 0; i < rounds; i++)
        {
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            await Task.Delay(20);
        }
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    public static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "streaming", name)).Replace("\r\n", "\n");

    public static Bitmap Capture(TopLevel topLevel) =>
        topLevel.CaptureRenderedFrame() ?? throw new InvalidOperationException("No frame rendered.");
}
