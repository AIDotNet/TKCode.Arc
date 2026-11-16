using Avalonia;
using Avalonia.Controls;
#if core || arc
using TKCode.Arc;
#endif

internal static class Program
{
    [STAThread]
    public static void Main()
    {
        var builder = AppBuilder.Configure<Application>().UsePlatformDetect();
#if core
        builder.UseArc(arc => arc.UseArcCore());
#elif arc
        builder.UseArc(arc => arc.UseArcControls());
#endif
        builder.SetupWithoutStarting();
#if arc
        var button = new TKCode.Arc.Button { Content = "Hello" };
        Console.WriteLine($"themes registered={ArcRuntime.Current!.Themes.AssetCount}; loaded={ArcRuntime.Current.Themes.LoadedCount}");
        var found = Application.Current!.TryGetResource(typeof(TKCode.Arc.Button), null, out var theme);
        Console.WriteLine($"button theme found={found}; loaded after button={ArcRuntime.Current.Themes.LoadedCount}");
        GC.KeepAlive(button);
#else
        GC.KeepAlive(new Avalonia.Controls.Button { Content = "Hello" });
#endif
        Console.WriteLine("OK");
    }
}
