using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using TKCode.Arc;
using TKCode.Arc.FoundationAotSample.Scale;
using TKCode.Arc.FoundationAotSample.Scale.DesignTokens;
using TKCode.Arc.Generated.FoundationAotSample;

namespace TKCode.Arc.FoundationAotSample;

/// <summary>
/// Starts Avalonia headless with Foundation and an Arc-sized token and theme set, checks that tokens resolve, switch and
/// parse, and prints timings. Usage: FoundationAotSample [lazy|eager]. Exit code 0 when every check passed.
/// </summary>
public static class Program
{
    private static int s_failures;

    public static int Main(string[] args)
    {
        var mode = args.Length > 0 && args[0] == "eager" ? ArcThemeLoading.Eager : ArcThemeLoading.Lazy;
        var total = Stopwatch.StartNew();
        var install = new Stopwatch();
        AppBuilder.Configure<App>()
                  .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true })
                  .AfterSetup(_ => install.Start())
                  .UseArc(arc => arc.UsePackage(ArcGeneratedAssets.PackageId, static p => ArcGeneratedAssets.AddTo(p))
                                    .WithThemeLoading(mode))
                  .AfterSetup(_ => install.Stop())
                  .SetupWithoutStarting();
        var setup = total.Elapsed;

        var runtime = ArcRuntime.Current!;
        var theme = runtime.Theme;
        Console.WriteLine($"mode                      {mode}");
        Console.WriteLine($"runtime                   {(System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeCompiled ? "JIT" : "Native AOT")}");
        Console.WriteLine($"identities / tokens       {theme.Descriptors.Count} / {theme.TokenCount}");
        Console.WriteLine($"theme files               {runtime.Themes.AssetCount}");
        Console.WriteLine($"avalonia setup total      {Ms(setup)}");
        Console.WriteLine($"  UseArc install          {Ms(install.Elapsed)}");
        Console.WriteLine($"    first compute         {Ms(theme.LastComputeDuration)}");
        Console.WriteLine($"    first publish         {Ms(theme.LastPublishDuration)}");
        Console.WriteLine($"    theme files loaded    {runtime.Themes.LoadedCount} in {Ms(runtime.Themes.TotalLoadDuration)}");

        // Tokens resolve through the boxed kind (what ArcTokens.Bind does).
        Check("global token resolves",
            Application.Current!.TryGetResource(ScaleDesignTokenKind.Color0, null, out var accent)
            && accent is ISolidColorBrush { Color: var c } && c == ArcSeedToken.DefaultAccent);

        // First use of 50 control types, then the same 50 types again.
        var types = Enumerable.Range(0, 50).Select(CreateControl).ToArray();
        var loadsBefore = runtime.Themes.TotalLoadDuration;
        var watch = Stopwatch.StartNew();
        var window = new Window { Width = 1200, Height = 900, Content = new WrapPanel() };
        window.Show();
        foreach (var control in types)
        {
            ((WrapPanel)window.Content!).Children.Add(control);
        }
        Dispatcher.UIThread.RunJobs();
        var firstUse = watch.Elapsed;
        Console.WriteLine($"first use of 50 controls  {Ms(firstUse)} (theme file loads {Ms(runtime.Themes.TotalLoadDuration - loadsBefore)}, {runtime.Themes.LoadedCount} files loaded)");

        watch.Restart();
        var again = new WrapPanel();
        foreach (var index in Enumerable.Range(0, 50))
        {
            again.Children.Add(CreateControl(index));
        }
        window.Content = again;
        Dispatcher.UIThread.RunJobs();
        Console.WriteLine($"same 50 controls again    {Ms(watch.Elapsed)}");

        var probe = (TemplatedControl)again.Children[3];
        Check("control theme applies token", probe.Background is ISolidColorBrush { Color: var probeColor } && probeColor == ExpectedColor(3, false));

        // All 348 controls on screen, then switch light/dark.
        var all = new WrapPanel();
        foreach (var index in Enumerable.Range(0, runtime.Themes.AssetCount))
        {
            all.Children.Add(CreateControl(index));
        }
        watch.Restart();
        window.Content = all;
        Dispatcher.UIThread.RunJobs();
        Console.WriteLine($"show all 348 controls     {Ms(watch.Elapsed)} ({runtime.Themes.LoadedCount} files loaded, {Ms(runtime.Themes.TotalLoadDuration)} in loads)");

        var switches = new List<(TimeSpan Compute, TimeSpan Publish, TimeSpan Total)>();
        for (var i = 0; i < 10; i++)
        {
            watch.Restart();
            theme.Apply(t => t.Appearance(i % 2 == 0 ? ArcAppearance.Dark : ArcAppearance.Light));
            Dispatcher.UIThread.RunJobs();
            switches.Add((theme.LastComputeDuration, theme.LastPublishDuration, watch.Elapsed));
        }
        Console.WriteLine($"light/dark switch x10     compute {Avg(switches, s => s.Compute)}, publish+refresh {Avg(switches, s => s.Publish)}, total {Avg(switches, s => s.Total)} (avg; {theme.LastPublishedCount} keys rewritten)");
        var probeAll = (TemplatedControl)all.Children[3];
        Check("light/dark switch updates control", probeAll.Background is ISolidColorBrush { Color: var light } && light == ExpectedColor(3, false));

        watch.Restart();
        theme.Apply(t => t.Accent(Color.Parse("#7747FF")));
        Dispatcher.UIThread.RunJobs();
        Console.WriteLine($"accent change             compute {Ms(theme.LastComputeDuration)}, publish+refresh {Ms(theme.LastPublishDuration)}, total {Ms(watch.Elapsed)} ({theme.LastPublishedCount} keys rewritten)");
        Check("accent recomputes global token",
            Application.Current.TryGetResource(ScaleDesignTokenKind.Color0, null, out var violet)
            && violet is ISolidColorBrush { Color: var v } && v == Color.Parse("#7747FF"));

        // String overrides through the generated parsers.
        theme.Apply(t => t.Token("Radius0", "12").Token("Scale003", "Inset0", "4,2").Token("Duration0", "90ms"));
        Check("overrides parse",
            Application.Current.TryGetResource(ScaleDesignTokenKind.Radius0, null, out var radius) && radius is CornerRadius { TopLeft: 12 }
            && Application.Current.TryGetResource(Scale003TokenKind.Inset0, null, out var inset) && inset is Thickness { Left: 4, Top: 2 });
        try
        {
            theme.Apply(t => t.Token("Radius0", "round"));
            Check("bad override rejected", false);
        }
        catch (ArcThemeException error)
        {
            Check("bad override rejected", error.Errors.Count == 1 && error.Errors[0].Contains("'round' is not a valid corner radius"));
        }

        // Scoped dark subtree.
        var scoped = CreateControl(3);
        var scope = new ThemeVariantScope { Child = scoped };
        ArcThemeScope.SetAppearance(scope, ArcAppearance.Dark);
        window.Content = scope;
        Dispatcher.UIThread.RunJobs();
        Check("scoped dark subtree", scoped.Background is ISolidColorBrush { Color: var dark } && dark == ExpectedColor(3, true));

        if (mode == ArcThemeLoading.Lazy)
        {
            var remaining = runtime.Themes.TotalLoadDuration;
            runtime.Themes.LoadAll();
            Console.WriteLine($"eager-load remaining      {Ms(runtime.Themes.TotalLoadDuration - remaining)} ({runtime.Themes.LoadedCount} files now)");
        }
        Console.WriteLine($"total                     {Ms(total.Elapsed)}");
        Console.WriteLine($"working set               {Environment.WorkingSet / (1024 * 1024)} MB");
        Console.WriteLine(s_failures == 0 ? "ALL CHECKS PASSED" : $"{s_failures} CHECK(S) FAILED");
        return s_failures == 0 ? 0 : 1;
    }

    // Matches generate_scale.py: control i uses global Color{i % 120} as background.
    private static Color ExpectedColor(int control, bool dark)
    {
        var i = control % 120;
        if (i == 0)
        {
            return ArcSeedToken.DefaultAccent;
        }
        var b = (i * 37) % 200 + (dark ? 40 : 0);
        return Color.FromRgb((byte)b, (byte)((b * 3) % 256), (byte)((b * 7) % 256));
    }

    private static TemplatedControl CreateControl(int index) => ScaleControls.Create(index);

    private static void Check(string name, bool ok)
    {
        Console.WriteLine($"check {(ok ? "ok  " : "FAIL")}                {name}");
        if (!ok)
        {
            s_failures++;
        }
    }

    private static string Ms(TimeSpan span) => $"{span.TotalMilliseconds:0.00} ms";

    private static string Avg<T>(List<T> items, Func<T, TimeSpan> select)
        => Ms(TimeSpan.FromTicks((long)items.Average(i => select(i).Ticks)));
}

/// <summary>Headless host without Fluent: the window gets a minimal template.</summary>
public sealed class App : Application
{
    /// <inheritdoc />
    public override void Initialize()
    {
        var windowTheme = new ControlTheme(typeof(Window));
        windowTheme.Setters.Add(new Setter(TemplatedControl.TemplateProperty,
            new FuncControlTemplate<Window>((window, _) => new ContentPresenter
            {
                Name = "PART_ContentPresenter",
                [!ContentPresenter.ContentProperty] = window[!ContentControl.ContentProperty]
            })));
        Resources[typeof(Window)] = windowTheme;
    }
}
