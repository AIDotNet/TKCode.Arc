using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Video;

/// <summary>
/// <see cref="VideoPlayer.BrowserPresentation"/> = <see cref="VideoBrowserPresentation.Native"/>: the backend's platform view
/// takes the picture area, the control bar moves below it and always shows, the view hides while Arc content must show in
/// the picture area, and captions go to the view (design/components/video/video.md section 5.3). The browser's element is
/// stood in for by a plain control from <see cref="RecordingVideoPlayer"/>.
/// </summary>
public class VideoNativePresentationTests
{
    [AvaloniaFact]
    public void Native_Shows_The_Platform_View_Instead_Of_The_Surface()
    {
        using var scope = NativeScope();
        var player = new VideoPlayer { Source = VideoTestScope.Clip(60), BrowserPresentation = VideoBrowserPresentation.Native };
        scope.Show(player);

        var view = player.NativeView.ShouldNotBeNull();
        scope.Recording!.Last.NativeViews.ShouldBe([view]);
        view.GetVisualParent().ShouldBeSameAs(Surface(player).GetVisualParent());
        Surface(player).IsVisible.ShouldBeFalse();
        view.IsHitTestVisible.ShouldBeFalse();
        player.Classes.ShouldContain(VideoPlayer.NativePresentationPseudoClass);
    }

    [AvaloniaFact]
    public void Canvas_Or_A_Backend_Without_A_Platform_View_Keeps_The_Surface()
    {
        using var scope = NativeScope();
        var canvas = new VideoPlayer { Source = VideoTestScope.Clip(60) };
        scope.Show(canvas);
        canvas.NativeView.ShouldBeNull();
        Surface(canvas).IsVisible.ShouldBeTrue();
        canvas.Classes.ShouldNotContain(VideoPlayer.NativePresentationPseudoClass);

        using var plain = new VideoTestScope(recording: true);
        var native = new VideoPlayer { Source = VideoTestScope.Clip(60), BrowserPresentation = VideoBrowserPresentation.Native };
        plain.Show(native);
        native.NativeView.ShouldBeNull();
        Surface(native).IsVisible.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void The_Control_Bar_Sits_Below_The_Picture_And_Stays()
    {
        using var scope = NativeScope();
        var player = new VideoPlayer { Source = VideoTestScope.Clip(60), BrowserPresentation = VideoBrowserPresentation.Native };
        scope.Show(player, width: 640);
        player.Play();
        scope.Advance(player, TimeSpan.FromSeconds(10));

        // Playing with no activity would hide the bar over the picture; below it, it stays.
        player.AreControlsVisible.ShouldBeTrue();
        var bar = player.Frame!.ExtraHeight;
        bar.ShouldBeGreaterThan(0);
        player.NativeView!.Margin.Bottom.ShouldBe(bar);
        player.Frame.Bounds.Height.ShouldBe(640 * 9.0 / 16.0 + bar, 0.5);
        player.NativeView.Bounds.Height.ShouldBe(640 * 9.0 / 16.0, 0.5);
    }

    [AvaloniaFact]
    public void The_View_Hides_Until_The_First_Play_While_A_Menu_Is_Open_At_The_End_And_On_Failure()
    {
        using var scope = NativeScope();
        var player = new VideoPlayer { Source = VideoTestScope.Clip(60), BrowserPresentation = VideoBrowserPresentation.Native };
        scope.Show(player);
        var view = player.NativeView!;

        // Before the first play the big play button (drawn by Arc) must be seen.
        view.IsVisible.ShouldBeFalse();
        player.StateLayer!.ShowBigPlay.ShouldBeTrue();

        player.Play();
        VideoTestScope.Pump();
        view.IsVisible.ShouldBeTrue();

        // Menus open over the picture.
        player.SpeedButton!.ToggleMenu(MenuOpenFocus.Surface);
        VideoTestScope.Pump();
        view.IsVisible.ShouldBeFalse();
        player.SpeedButton.CloseMenu();
        VideoTestScope.Pump();
        view.IsVisible.ShouldBeTrue();

        // At the end the replay button shows.
        scope.Advance(player, TimeSpan.FromSeconds(61));
        player.State.ShouldBe(VideoPlaybackState.Ended);
        view.IsVisible.ShouldBeFalse();
        player.StateLayer!.ShowReplay.ShouldBeTrue();
        player.Play();
        VideoTestScope.Pump();
        view.IsVisible.ShouldBeTrue();

        VideoTestScope.Sim(player).SimulateFailure(VideoErrorKind.Network);
        VideoTestScope.Pump();
        view.IsVisible.ShouldBeFalse();
        player.StateLayer.ShowError.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Captions_Go_To_The_View()
    {
        using var scope = NativeScope();
        var english = new VideoTextTrack { Kind = VideoTextTrackKind.Captions, Language = "en-US", Text = "WEBVTT\n\n00:00.000 --> 00:05.000\nHello\n" };
        var player = new VideoPlayer { Source = VideoTestScope.Clip(60), BrowserPresentation = VideoBrowserPresentation.Native };
        player.TextTracks.Add(english);
        scope.Show(player);
        var backend = scope.Recording!.Last;

        player.SelectedTextTrack = english;
        backend.NativeCaptions.ShouldBeSameAs(english);
        player.SelectedTextTrack = null;
        backend.NativeCaptions.ShouldBeNull();

        player.SelectedTextTrack = english;
        player.BrowserPresentation = VideoBrowserPresentation.Canvas;
        VideoTestScope.Pump();
        backend.NativeCaptions.ShouldBeNull();
        player.NativeView.ShouldBeNull();
        Surface(player).IsVisible.ShouldBeTrue();
        player.Frame!.ExtraHeight.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Releasing_The_Player_Takes_The_View_Out_First()
    {
        using var scope = NativeScope();
        var player = new VideoPlayer { Source = VideoTestScope.Clip(60), BrowserPresentation = VideoBrowserPresentation.Native };
        scope.Show(player);
        var view = player.NativeView!;
        player.Source = null;
        VideoTestScope.Pump();
        player.NativeView.ShouldBeNull();
        view.GetVisualParent().ShouldBeNull();
        player.Classes.ShouldNotContain(VideoPlayer.NativePresentationPseudoClass);
    }

    [AvaloniaFact]
    public void Full_Screen_Keeps_The_View_With_The_Root()
    {
        using var scope = NativeScope();
        var player = new VideoPlayer { Source = VideoTestScope.Clip(60), BrowserPresentation = VideoBrowserPresentation.Native };
        scope.Show(player);
        var view = player.NativeView!;
        player.EnterFullScreen();
        VideoTestScope.Pump();
        player.NativeView.ShouldBeSameAs(view);
        view.FindAncestorOfType<VideoFullScreenHost>().ShouldNotBeNull();
        player.ExitFullScreen();
        VideoTestScope.Pump();
        player.NativeView.ShouldBeSameAs(view);
        view.FindAncestorOfType<VideoFullScreenHost>().ShouldBeNull();
    }

    private static VideoTestScope NativeScope()
    {
        var scope = new VideoTestScope(recording: true);
        scope.Recording!.OffersNativeView = true;
        return scope;
    }

    private static VideoSurface Surface(VideoPlayer player) => player.GetVisualDescendants().OfType<VideoSurface>().Single();
}
