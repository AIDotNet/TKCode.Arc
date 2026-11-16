using Avalonia.iOS;
using Foundation;
using UIKit;

namespace TKCode.Arc.TerminalMobileSample;

[Register("AppDelegate")]
public partial class AppDelegate : AvaloniaAppDelegate<App>
{
}

public static class Program
{
    private static void Main(string[] args) => UIApplication.Main(args, null, typeof(AppDelegate));
}
