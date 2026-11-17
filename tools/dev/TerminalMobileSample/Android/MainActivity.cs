using Android.App;
using Android.Content.PM;
using Android.Runtime;
using Avalonia.Android;

namespace TKCode.Arc.TerminalMobileSample;

[Application]
public sealed class AndroidApp : AvaloniaAndroidApplication<App>
{
    public AndroidApp(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
    }
}

[Activity(Label = "Arc Terminal Sample", MainLauncher = true, Theme = "@style/Theme.AppCompat.Light.NoActionBar",
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public sealed class MainActivity : AvaloniaMainActivity
{
}
