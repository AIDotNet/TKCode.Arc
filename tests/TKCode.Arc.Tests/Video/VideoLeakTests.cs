using System.Runtime.CompilerServices;
using Avalonia.Headless.XUnit;
using Shouldly;

namespace TKCode.Arc.Tests.Video;

/// <summary>
/// Leaks (design/components/video/video.md section 16, completion-plan.md A7): a <see cref="VideoPlayer"/> lets go of its
/// backend player when the source is cleared or it leaves the visual tree (a new source reuses the player), and nothing
/// keeps a closed player alive.
/// </summary>
public class VideoLeakTests
{
    [AvaloniaFact]
    public void A_cleared_source_releases_its_player()
    {
        using var scope = new VideoTestScope();
        var control = new VideoPlayer { Source = VideoTestScope.Clip(60) };
        scope.Show(control);
        var weak = PlayThenClear(scope, control);
        TestHelpers.Collect(weak);
        weak.IsAlive.ShouldBeFalse();
        GC.KeepAlive(control);
    }

    [AvaloniaFact]
    public void A_closed_player_is_collectable_with_its_backend_player()
    {
        using var scope = new VideoTestScope();
        WeakReference? backend = null;
        var weak = TestHelpers.ShowAndClose(() => new VideoPlayer { Source = VideoTestScope.Clip(60) }, control =>
        {
            control.Play();
            scope.Advance(control, TimeSpan.FromSeconds(1));
            backend = new WeakReference(control.Player.ShouldNotBeNull());
        });
        TestHelpers.Collect(weak, backend!);
        weak.IsAlive.ShouldBeFalse();
        backend!.IsAlive.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void A_closed_full_screen_player_is_collectable()
    {
        using var scope = new VideoTestScope();
        var weak = TestHelpers.ShowAndClose(() => new VideoPlayer { Source = VideoTestScope.Clip(60) }, control =>
        {
            control.Play();
            control.EnterFullScreen();
            VideoTestScope.Pump();
            control.ExitFullScreen();
            VideoTestScope.Pump();
        });
        TestHelpers.Collect(weak);
        weak.IsAlive.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void A_removed_native_view_is_collectable()
    {
        using var scope = new VideoTestScope(recording: true);
        scope.Recording!.OffersNativeView = true;
        var control = new VideoPlayer { Source = VideoTestScope.Clip(60), BrowserPresentation = VideoBrowserPresentation.Native };
        scope.Show(control);
        var weak = DropNativeView(control);
        // The recording backend lists every view it made; only the player's own references matter here.
        scope.Recording.Last.NativeViews.Clear();
        TestHelpers.Collect(weak);
        weak.IsAlive.ShouldBeFalse();
        GC.KeepAlive(control);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference PlayThenClear(VideoTestScope scope, VideoPlayer control)
    {
        control.Play();
        scope.Advance(control, TimeSpan.FromSeconds(1));
        var first = new WeakReference(control.Player.ShouldNotBeNull());
        control.Source = null;
        VideoTestScope.Pump();
        control.Player.ShouldBeNull();
        // The next source gets a new player; the released one must not be kept by anything the control still holds.
        control.Source = VideoTestScope.Clip(30);
        VideoTestScope.Pump();
        control.Player.ShouldNotBeNull();
        return first;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference DropNativeView(VideoPlayer control)
    {
        var view = new WeakReference(control.NativeView.ShouldNotBeNull());
        control.BrowserPresentation = VideoBrowserPresentation.Canvas;
        VideoTestScope.Pump();
        control.NativeView.ShouldBeNull();
        return view;
    }
}
