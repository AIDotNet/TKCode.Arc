using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>Shows the playback backend, what it can play, and the render path the player's surface uses.</summary>
public partial class VideoPlayerPlaybackDemo : UserControl
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(500) };

    public VideoPlayerPlaybackDemo()
    {
        InitializeComponent();
        NativeSwitch.IsVisible = OperatingSystem.IsBrowser();
        NativeSwitch.IsCheckedChanged += (_, _) =>
        {
            Player.BrowserPresentation = NativeSwitch.IsChecked == true ? VideoBrowserPresentation.Native : VideoBrowserPresentation.Canvas;
            Update();
        };
        _timer.Tick += (_, _) => Update();
        AttachedToVisualTree += (_, _) =>
        {
            Update();
            _timer.Start();
        };
        DetachedFromVisualTree += (_, _) => _timer.Stop();
    }

    private void Update()
    {
        var backend = ArcVideo.Backend;
        var surface = Player.GetVisualDescendants().OfType<VideoSurface>().FirstOrDefault();
        Info.Text =
            $"Backend      {backend.Name}{(backend.IsReal ? "" : " (simulated: no platform backend)")}\n" +
            $"Frames       {backend.FrameKinds}{(backend.GpuHandleTypes.Count > 0 ? " · " + string.Join(", ", backend.GpuHandleTypes) : "")}\n" +
            $"Render path  {(Player.BrowserPresentation == VideoBrowserPresentation.Native && surface?.IsVisible == false ? "Native (browser video element)" : surface?.RenderPath.ToString() ?? "-")}\n" +
            $"State        {Player.State}\n" +
            $"video/mp4    {backend.CanPlay("video/mp4; codecs=\"avc1.4d401f, mp4a.40.2\"")}\n" +
            $"video/webm   {backend.CanPlay("video/webm; codecs=\"vp9, opus\"")}\n" +
            $"HEVC         {backend.CanPlay("video/mp4; codecs=\"hvc1.1.6.L93.B0\"")}";
    }
}
