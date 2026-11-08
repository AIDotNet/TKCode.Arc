using Avalonia.Headless.XUnit;
using Shouldly;

namespace TKCode.Arc.Tests.Video;

/// <summary>
/// Text tracks stored in the media (<see cref="IVideoEmbeddedTextTracks"/>): <see cref="VideoPlayer"/> lists them in
/// <see cref="VideoPlayer.TextTracks"/> after the app's own tracks, tells the backend which one to deliver, builds cues from
/// what it sends and drops the tracks with the media. The backend is stood in for by <see cref="RecordingVideoPlayer"/>.
/// </summary>
public class VideoEmbeddedTextTests
{
    private static readonly VideoEmbeddedTextTrackInfo s_english = new(0, VideoTextTrackKind.Captions, "en", "English");
    private static readonly VideoEmbeddedTextTrackInfo s_chinese = new(1, VideoTextTrackKind.Subtitles, "zh", null);

    [AvaloniaFact]
    public void Embedded_Tracks_Join_The_Apps_Own_And_Leave_With_The_Media()
    {
        using var scope = new VideoTestScope(recording: true);
        var own = new VideoTextTrack { Kind = VideoTextTrackKind.Captions, Language = "fr", Text = "WEBVTT\n\n00:01.000 --> 00:02.000\nBonjour\n" };
        var player = new VideoPlayer { Source = VideoTestScope.Clip(60) };
        player.TextTracks.Add(own);
        scope.Show(player);
        player.CaptionsButton!.IsVisible.ShouldBeTrue();

        scope.Recording!.Last.SimulateEmbeddedTracks(s_english, s_chinese);
        player.TextTracks.Count.ShouldBe(3);
        player.TextTracks[0].ShouldBeSameAs(own);
        own.IsEmbedded.ShouldBeFalse();
        var english = player.TextTracks[1];
        english.IsEmbedded.ShouldBeTrue();
        english.Kind.ShouldBe(VideoTextTrackKind.Captions);
        english.Language.ShouldBe("en");
        english.Label.ShouldBe("English");
        player.TextTracks[2].Kind.ShouldBe(VideoTextTrackKind.Subtitles);
        player.TextTracks[2].Label.ShouldBeNull();

        // New media: the old media's tracks go at once; the app's own stay.
        player.SelectedTextTrack = english;
        player.Source = VideoTestScope.Clip(30);
        VideoTestScope.Pump();
        player.TextTracks.ShouldBe([own]);
        player.SelectedTextTrack.ShouldBeNull();
    }

    [AvaloniaFact]
    public void Selecting_A_Track_Tells_The_Backend_And_Its_Cues_Show()
    {
        using var scope = new VideoTestScope(recording: true);
        var player = new VideoPlayer { Source = VideoTestScope.Clip(60) };
        scope.Show(player);
        var backend = scope.Recording!.Last;
        backend.SimulateEmbeddedTracks(s_english, s_chinese);
        var chinese = player.TextTracks[1];

        player.SelectedTextTrack = chinese;
        backend.EmbeddedSelections.ShouldBe([1]);
        backend.SimulateEmbeddedCue(1, 1.0, 2.0, "你好 <b>世界</b>");
        chinese.Cues.Count.ShouldBe(1);
        chinese.Cues[0].Text.ShouldBe("你好 世界");
        chinese.Cues[0].Runs.ShouldContain(r => r.Text == "世界" && r.IsBold);

        player.Play();
        scope.Advance(player, TimeSpan.FromSeconds(1.5));
        player.CaptionLayer!.ActiveCues.ShouldHaveSingleItem().Text.ShouldBe("你好 世界");
        scope.Advance(player, TimeSpan.FromSeconds(1));
        player.CaptionLayer.ActiveCues.ShouldBeEmpty();

        // Off, and an app track: the backend delivers nothing.
        player.SelectedTextTrack = null;
        backend.EmbeddedSelections.ShouldBe([1, -1]);
    }

    [AvaloniaFact]
    public void Open_Ended_Cues_Last_Until_The_Next_One_And_Repeats_Are_Not_Doubled()
    {
        using var scope = new VideoTestScope(recording: true);
        var player = new VideoPlayer { Source = VideoTestScope.Clip(60) };
        scope.Show(player);
        var backend = scope.Recording!.Last;
        backend.SimulateEmbeddedTracks(s_english);
        var english = player.TextTracks[0];
        player.SelectedTextTrack = english;

        backend.SimulateEmbeddedCue(0, 1.0, null, "First");
        english.Cues.ShouldHaveSingleItem().End.ShouldBe(TimeSpan.MaxValue);
        backend.SimulateEmbeddedCue(0, 2.5, null, "Second &amp; last");
        english.Cues.Count.ShouldBe(2);
        english.Cues[0].End.ShouldBe(TimeSpan.FromSeconds(2.5));
        english.Cues[1].Text.ShouldBe("Second & last");
        backend.SimulateEmbeddedCue(0, 3.0, null, "");
        english.Cues[1].End.ShouldBe(TimeSpan.FromSeconds(3));

        // After a seek back the backend sends the first cue again.
        backend.SimulateEmbeddedCue(0, 1.0, null, "First");
        backend.SimulateEmbeddedCue(0, 2.5, null, "Second &amp; last");
        english.Cues.Select(c => c.Text).ShouldBe(["First", "Second & last"]);
        english.Cues[0].End.ShouldBe(TimeSpan.FromSeconds(2.5));

        // A playhead-timed backend sends the cue again a little off (looping): still one cue.
        backend.SimulateEmbeddedCue(0, 1.08, null, "First");
        english.Cues.Count(c => c.Text == "First").ShouldBe(1);
    }

    [AvaloniaFact]
    public void A_Cue_For_Another_Track_Is_Ignored()
    {
        using var scope = new VideoTestScope(recording: true);
        var player = new VideoPlayer { Source = VideoTestScope.Clip(60) };
        scope.Show(player);
        var backend = scope.Recording!.Last;
        backend.SimulateEmbeddedTracks(s_english, s_chinese);
        backend.SimulateEmbeddedCue(5, 1.0, 2.0, "Nowhere");
        player.TextTracks.ShouldAllBe(t => t.Cues.Count == 0);
    }

    [AvaloniaFact]
    public void Releasing_The_Player_Removes_Its_Tracks()
    {
        using var scope = new VideoTestScope(recording: true);
        var player = new VideoPlayer { Source = VideoTestScope.Clip(60) };
        scope.Show(player);
        var backend = scope.Recording!.Last;
        backend.SimulateEmbeddedTracks(s_english);
        player.TextTracks.Count.ShouldBe(1);
        player.Source = null;
        VideoTestScope.Pump();
        player.TextTracks.ShouldBeEmpty();
        player.CaptionsButton!.IsVisible.ShouldBeFalse();

        // A late event from the released player changes nothing.
        backend.SimulateEmbeddedTracks(s_english, s_chinese);
        player.TextTracks.ShouldBeEmpty();
    }
}
