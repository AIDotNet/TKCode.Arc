using Avalonia;
using Avalonia.iOS;
using Foundation;
using UIKit;

namespace TKCode.Arc.VideoMobileSample;

[Register("AppDelegate")]
public partial class AppDelegate : AvaloniaAppDelegate<App>
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) =>
        base.CustomizeAppBuilder(builder).UseArc(arc => arc.UseArcControls().UseArcVideo());
}

public static class Program
{
    private static void Main(string[] args) => UIApplication.Main(args, null, typeof(AppDelegate));
}
