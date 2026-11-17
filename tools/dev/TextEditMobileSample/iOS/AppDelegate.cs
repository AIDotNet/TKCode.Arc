using Avalonia;
using Foundation;
using TKCode.Arc.Mobile;
using UIKit;

namespace TKCode.Arc.TextEditMobileSample;

// ArcAppDelegate answers cut:/copy:/paste:/selectAll: for Avalonia text boxes (TKCode.Arc.Mobile.iOS).
[Register("AppDelegate")]
public partial class AppDelegate : ArcAppDelegate<App>
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) =>
        base.CustomizeAppBuilder(builder).UseArc(arc => arc.UseArcMobile());
}

public static class Program
{
    private static void Main(string[] args) => UIApplication.Main(args, null, typeof(AppDelegate));
}
