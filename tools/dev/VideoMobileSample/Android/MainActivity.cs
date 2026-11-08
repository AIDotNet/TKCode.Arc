using Android.App;
using Android.Content.PM;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;

namespace TKCode.Arc.VideoMobileSample;

[Application]
public sealed class AndroidApp : AvaloniaAndroidApplication<App>
{
    public AndroidApp(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) =>
        base.CustomizeAppBuilder(builder).UseArc(arc => arc.UseArcControls().UseArcVideo());
}

[Activity(Label = "Arc Video Sample", MainLauncher = true, Theme = "@style/Theme.AppCompat.Light.NoActionBar",
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public sealed class MainActivity : AvaloniaMainActivity
{
}
