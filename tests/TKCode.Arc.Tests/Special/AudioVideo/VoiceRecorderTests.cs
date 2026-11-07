using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using Xunit;

namespace TKCode.Arc.Tests.Special.AudioVideo;

/// <summary>
/// The voice recorder: the thread, recording (timer, samples, auto stop), review (playback, scrub), send and discard
/// (events, thread, announcements), one playback at a time, the keyboard and focus model, and the morphs.
/// </summary>
public class VoiceRecorderTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private static (VoiceRecorder Recorder, Window Window) Open(ArcMotionLevel level = ArcMotionLevel.Off, Action<VoiceRecorder>? setup = null)
    {
        var recorder = new VoiceRecorder { Width = 416, Height = 408, Messages = VoiceRecorderSamples.Thread() };
        setup?.Invoke(recorder);
        var window = TestHelpers.Show(recorder, level);
        window.Width = 480;
        window.Height = 480;
        Pump();
        return (recorder, window);
    }

    private static void Pump(int rounds = 3)
    {
        for (var i = 0; i < rounds; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static void Run(ManualFrameClock clock, double seconds)
    {
        var frames = (int)Math.Round(seconds * 60);
        for (var i = 0; i < frames; i++)
        {
            clock.Advance(Frame, Frame);
            Dispatcher.UIThread.RunJobs();
        }
        Pump(1);
    }

    private static string? Name(Control control) => AutomationProperties.GetName(control);

    private static void Press(Window window, PhysicalKey key)
    {
        window.KeyPressQwerty(key, RawInputModifiers.None);
        window.KeyReleaseQwerty(key, RawInputModifiers.None);
        Pump(1);
    }

    [AvaloniaFact]
    public void ShowsTheThreadAndTheIdleComposer()
    {
        var (recorder, _) = Open();
        Assert.Equal(3, recorder.Bubbles.Count);
        Assert.Equal(VoiceRecorderPhase.Idle, recorder.Phase);
        Assert.True(recorder.Composer.Mic.IsVisible);
        Assert.False(recorder.Composer.Capsule.IsVisible);
        Assert.False(recorder.Composer.Stop.IsVisible);
        Assert.Equal("Record voice note", Name(recorder.Composer.Mic));
        Assert.Equal("Play voice message from Tomas Novak, 0:14", Name(recorder.Bubbles[0].Play));
        Assert.Equal("Play voice message, 0:06", Name(recorder.Bubbles[1].Play));
        Assert.Equal("0:09", recorder.Bubbles[2].Time.Text);
        Assert.Equal("Voice messages", Name(recorder));
        // The composer: a 48px row; the mic sits at the right edge.
        Assert.Equal(48, recorder.Composer.Mic.Bounds.Height, 3);
        Assert.Equal(recorder.Composer.Bounds.Width, recorder.Composer.Mic.Bounds.Right, 3);
    }

    [AvaloniaFact]
    public void BubbleWidthFollowsTheBarCount()
    {
        var (recorder, _) = Open();
        // Bars take 5px each less one gap; the rest is the play button, gaps, time and padding.
        var b1 = recorder.Bubbles[0];
        var b2 = recorder.Bubbles[1];
        Assert.Equal(32 * 5 - 2, b1.Bars.Bounds.Width, 3);
        Assert.Equal(22 * 5 - 2, b2.Bars.Bounds.Width, 3);
        Assert.Equal(50, b1.Bounds.Width - b2.Bounds.Width, 3);
        Assert.Equal(44, b1.Bounds.Height, 3);
    }

    [AvaloniaFact]
    public void RecordingRunsTheTimerAndCollectsSamples()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (recorder, _) = Open(ArcMotionLevel.Full);
        recorder.StartRecording();
        Pump();
        Assert.Equal(VoiceRecorderPhase.Recording, recorder.Phase);
        Assert.Equal(4, recorder.TakeNumber);
        Assert.Equal("Recording", recorder.Status?.Announcement);
        Run(clock, 2.5);
        Assert.InRange(recorder.Elapsed.TotalSeconds, 2.4, 2.6);
        Assert.StartsWith("0:02.", recorder.RecordTimer.Text);
        Assert.Equal(VoiceRecorderPeaks.SampleCount(recorder.Elapsed.TotalSeconds), recorder.SampleCount);
        Assert.Equal("Stop recording at 0:02", Name(recorder.Composer.Stop));
        Assert.True(recorder.Composer.Discard.IsVisible);
        Assert.True(recorder.Composer.Capsule.IsVisible);
        Assert.False(recorder.Composer.Mic.IsVisible);
    }

    [AvaloniaFact]
    public void ReachingTheLimitOpensTheReview()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (recorder, _) = Open(ArcMotionLevel.Full, r => r.MaxDuration = TimeSpan.FromSeconds(3));
        recorder.StartRecording();
        Run(clock, 3.5);
        Assert.Equal(VoiceRecorderPhase.Review, recorder.Phase);
        Assert.Equal(3, recorder.Elapsed.TotalSeconds, 6);
        Assert.Equal("0:03", recorder.ReviewTimer.Text);
        Assert.Equal("Recorded 0:03. Play it back or send it.", recorder.Status?.Announcement);
        Assert.Equal(VoiceRecorderPeaks.ReviewBarCount, recorder.ReviewBars.Peaks.Count);
        Assert.Equal("Play preview", Name(recorder.ReviewPlay));
    }

    [AvaloniaFact]
    public void ReviewPlaysScrubsAndEnds()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (recorder, _) = Open(ArcMotionLevel.Full);
        recorder.StartRecording();
        Run(clock, 2.0);
        recorder.StopRecording();
        var total = recorder.Elapsed.TotalSeconds;
        recorder.ReviewPlay.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.True(recorder.IsReviewPlaying);
        Assert.Equal("Pause preview", Name(recorder.ReviewPlay));
        Run(clock, 1.1);
        Assert.InRange(recorder.ReviewBars.Progress, 0.45, 0.65);
        Assert.Equal("0:01", recorder.ReviewTimer.Text);
        Run(clock, 1.4);
        Assert.False(recorder.IsReviewPlaying);
        Assert.Equal(1, recorder.ReviewBars.Progress, 6);
        Assert.Equal(VoiceRecorderClock.Format(total), recorder.ReviewTimer.Text);
        // A press on the waveform seeks without playing.
        var bars = recorder.ReviewBars;
        var extent = bars.Layout(bars.Bounds.Width).Extent;
        var window = (Window)TopLevel.GetTopLevel(bars)!;
        var point = bars.TranslatePoint(new Point(extent * 0.25, bars.Bounds.Height / 2), window)!.Value;
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Pump(1);
        Assert.Equal(0.25, recorder.ReviewBars.Progress, 2);
        Assert.False(recorder.IsReviewPlaying);
    }

    [AvaloniaFact]
    public void SendAppendsTheTakeAndRaisesTheEvent()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        VoiceRecorderMessage? sent = null;
        var (recorder, _) = Open(ArcMotionLevel.Full, r => r.MessageSent += (_, e) => sent = e.Message);
        recorder.StartRecording();
        Run(clock, 3.5);
        recorder.StopRecording();
        recorder.Send();
        Pump();
        Assert.NotNull(sent);
        Assert.Equal(VoiceRecorderPhase.Idle, recorder.Phase);
        Assert.Equal(4, recorder.Messages!.Count);
        Assert.Same(sent, recorder.Messages[3]);
        Assert.False(sent!.IsIncoming);
        Assert.Equal("Just now", sent.Meta);
        Assert.Equal(VoiceRecorderPeaks.BarCount(sent.Duration.TotalSeconds), sent.Peaks.Count);
        Assert.Equal(4, recorder.Bubbles.Count);
        Assert.Equal("Voice note sent", recorder.Status?.Announcement);
        // The new bubble starts on the bar's box and springs into place.
        var bubble = recorder.Bubbles[3];
        Assert.NotNull(bubble.Frame);
        Run(clock, 1.2);
        Assert.Null(bubble.Frame);
        Assert.True(recorder.Composer.Mic.IsVisible);
        Assert.Equal(1, recorder.MicPresence.Value, 3);
        Assert.False(recorder.Composer.Capsule.IsVisible);
        // The list glides to its end.
        var list = recorder.List!;
        Assert.Equal(Math.Max(0, list.Extent.Height - list.Viewport.Height), list.Offset.Y, 1);
    }

    [AvaloniaFact]
    public void TheSameTakeNumberGivesTheSameVoice()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var takes = new List<VoiceRecorderMessage>();
        var (recorder, _) = Open(ArcMotionLevel.Full, r =>
        {
            r.Cancelled += (_, e) => takes.Add(e.Message);
            r.MessageSent += (_, e) => takes.Add(e.Message);
        });
        recorder.StartRecording();
        Run(clock, 3.0);
        recorder.Discard();
        recorder.StartRecording();
        Run(clock, 3.0);
        recorder.Send();
        Run(clock, 1.0);
        recorder.StartRecording();
        Assert.Equal(5, recorder.TakeNumber);
        Run(clock, 3.0);
        recorder.Discard();
        Assert.Equal(3, takes.Count);
        Assert.Equal(takes[0].Peaks, takes[1].Peaks);
        Assert.NotEqual(takes[1].Peaks, takes[2].Peaks);
    }

    [AvaloniaFact]
    public void DiscardRaisesCancelledAndFoldsBackIntoTheMic()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var cancelled = 0;
        var (recorder, _) = Open(ArcMotionLevel.Full, r => r.Cancelled += (_, _) => cancelled++);
        recorder.StartRecording();
        Run(clock, 1.0);
        recorder.Discard();
        Pump(1);
        Assert.Equal(1, cancelled);
        Assert.Equal(VoiceRecorderPhase.Idle, recorder.Phase);
        Assert.Equal(3, recorder.Messages!.Count);
        Assert.Equal("Voice note discarded", recorder.Status?.Announcement);
        Assert.True(recorder.Composer.Mic.IsVisible);
        Assert.NotNull(recorder.Composer.MicFrame);
        Assert.True(recorder.Composer.MicFrame!.Value.Width > 100);
        Run(clock, 1.2);
        Assert.Null(recorder.Composer.MicFrame);
        Assert.Equal(48, recorder.Composer.Mic.Bounds.Width, 2);
    }

    [AvaloniaFact]
    public void MicToBarFollowsTheLayoutSpring()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (recorder, _) = Open(ArcMotionLevel.Full);
        var composer = recorder.Composer;
        var width = composer.Bounds.Width;
        var mic = composer.MicRect(width);
        var bar = composer.CapsuleRect(width);
        Assert.Equal(width - 104, bar.Width, 3);
        recorder.StartRecording();
        Run(clock, 0.1);
        var frame = composer.CapsuleFrame!.Value;
        var progress = (frame.Width - mic.Width) / (bar.Width - mic.Width);
        Assert.InRange(progress, 0.25, 0.45);
        // Both edges move on the same progress.
        Assert.Equal(mic.Right + (bar.Right - mic.Right) * progress, frame.Right, 1);
        Run(clock, 1.0);
        Assert.Null(composer.CapsuleFrame);
        Assert.Equal(bar.Width, composer.Capsule.Bounds.Width, 2);
    }

    [AvaloniaFact]
    public void DiscardMidMorphKeepsTheVelocity()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (recorder, _) = Open(ArcMotionLevel.Full);
        var composer = recorder.Composer;
        recorder.StartRecording();
        Run(clock, 0.1);
        var before = composer.CapsuleFrame!.Value.Width;
        recorder.Discard();
        clock.Advance(Frame, Frame);
        Dispatcher.UIThread.RunJobs();
        // The first frame after the reversal still widens a little before folding back.
        Assert.True(composer.MicFrame!.Value.Width > before);
        Run(clock, 1.5);
        Assert.Null(composer.MicFrame);
    }

    [AvaloniaFact]
    public void OnlyOneMessagePlaysAtATime()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (recorder, _) = Open(ArcMotionLevel.Full);
        recorder.PlayMessage("fence-1");
        Run(clock, 1.2);
        Assert.True(recorder.Bubbles[0].IsPlaying);
        Assert.Equal("0:01", recorder.Bubbles[0].Time.Text);
        Assert.Equal("Pause voice message from Tomas Novak, 0:14", Name(recorder.Bubbles[0].Play));
        recorder.PlayMessage("fence-3");
        Assert.False(recorder.Bubbles[0].IsPlaying);
        Assert.True(recorder.Bubbles[2].IsPlaying);
        Assert.Equal("0:14", recorder.Bubbles[0].Time.Text);
        Run(clock, 9.5);
        Assert.False(recorder.Bubbles[2].IsPlaying);
        Assert.Equal(1, recorder.Bubbles[2].Bars.Progress, 6);
        // Played to the end: play again starts over.
        recorder.PlayMessage("fence-3");
        Run(clock, 0.1);
        Assert.InRange(recorder.Bubbles[2].Bars.Progress, 0, 0.05);
        // Recording pauses playback.
        recorder.StartRecording();
        Assert.False(recorder.Bubbles[2].IsPlaying);
    }

    [AvaloniaFact]
    public void KeyboardFollowsTheButtons()
    {
        var (recorder, window) = Open();
        var composer = recorder.Composer;
        composer.Mic.Focus(NavigationMethod.Tab);
        Press(window, PhysicalKey.Enter);
        Assert.Equal(VoiceRecorderPhase.Recording, recorder.Phase);
        Assert.True(composer.Stop.IsFocused);
        Press(window, PhysicalKey.Enter);
        Assert.Equal(VoiceRecorderPhase.Review, recorder.Phase);
        Assert.True(composer.Send.IsFocused);
        Press(window, PhysicalKey.Space);
        Assert.Equal(VoiceRecorderPhase.Idle, recorder.Phase);
        Assert.True(composer.Mic.IsFocused);
        Assert.Equal(4, recorder.Messages!.Count);
    }

    [AvaloniaFact]
    public void EscapeDiscardsWhereverFocusIsInside()
    {
        var (recorder, window) = Open();
        recorder.StartRecording();
        Pump();
        recorder.Bubbles[0].Play.Focus(NavigationMethod.Tab);
        Press(window, PhysicalKey.Escape);
        Assert.Equal(VoiceRecorderPhase.Idle, recorder.Phase);
        Assert.True(recorder.Composer.Mic.IsFocused);
        Assert.Equal("Voice note discarded", recorder.Status?.Announcement);
    }

    [AvaloniaFact]
    public void PointerClickOnTheMicKeepsFocusInside()
    {
        var (recorder, window) = Open();
        var mic = recorder.Composer.Mic;
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        var point = mic.TranslatePoint(new Point(24, 24), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Pump();
        Assert.Equal(VoiceRecorderPhase.Recording, recorder.Phase);
        Assert.True(recorder.Composer.Stop.IsFocused);
        Assert.False(TestHelpers.IsFocusRingVisible(recorder.Composer.Stop));
        // Escape now works (Arc lost focus to the page here).
        Press(window, PhysicalKey.Escape);
        Assert.Equal(VoiceRecorderPhase.Idle, recorder.Phase);
    }

    [AvaloniaFact]
    public void LevelSourceSuppliesTheLevels()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        VoiceRecorderMessage? sent = null;
        var (recorder, _) = Open(ArcMotionLevel.Full, r =>
        {
            r.LevelSource = (_, sample) => sample % 2 == 0 ? 0.9 : 0;
            r.MessageSent += (_, e) => sent = e.Message;
        });
        recorder.StartRecording();
        Run(clock, 4);
        recorder.Send();
        Assert.NotNull(sent);
        // Every bar spans at least two samples, so every bar is loud; quiet samples are kept at the silence floor.
        Assert.All(sent!.Peaks, value => Assert.Equal(0.9, value, 6));
    }

    [AvaloniaFact]
    public void ResetRestoresTheThread()
    {
        var (recorder, _) = Open();
        recorder.StartRecording();
        recorder.Send();
        Assert.Equal(4, recorder.Bubbles.Count);
        recorder.ResetMessages(VoiceRecorderSamples.Thread());
        Pump();
        Assert.Equal(3, recorder.Bubbles.Count);
        Assert.Equal(3, recorder.Messages!.Count);
    }

    [AvaloniaFact]
    public void ReducedMotionSkipsTheMorphs()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (recorder, _) = Open(ArcMotionLevel.Reduced);
        recorder.StartRecording();
        Run(clock, 0.05);
        Assert.Null(recorder.Composer.CapsuleFrame);
        Assert.False(recorder.LiveWave.Smooth);
        // Pop-ins keep only their opacity.
        Assert.Equal(1, recorder.Composer.Stop.RenderTransform is Avalonia.Media.ScaleTransform s ? s.ScaleX : 1, 6);
        Run(clock, 1.0);
        recorder.Send();
        Pump();
        Assert.Null(recorder.Bubbles[^1].Frame);
    }

    [AvaloniaFact]
    public void LabelsCanBeReplaced()
    {
        var (recorder, _) = Open(setup: r =>
        {
            r.Label = "Voice notes with Ines";
            r.Labels = new VoiceRecorderLabels { Record = "Aufnehmen", Hint = "Tippe aufs Mikro" };
        });
        Assert.Equal("Aufnehmen", Name(recorder.Composer.Mic));
        Assert.Equal("Tippe aufs Mikro", recorder.Composer.Hint.Text);
        Assert.Equal("Voice notes with Ines", Name(recorder));
    }
}
