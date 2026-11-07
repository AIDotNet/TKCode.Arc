using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.AudioVideo;

/// <summary>The voice orb's companions: captions, the call bar, the clock format and the scripted session.</summary>
public class VoiceOrbPartsTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    // ---- Formats ----

    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(7, "0:07")]
    [InlineData(65, "1:05")]
    [InlineData(754, "12:34")]
    [InlineData(3725, "1:02:05")]
    public void Clock_reads_m_ss(int seconds, string text)
    {
        VoiceControlsLabels.FormatClock(TimeSpan.FromSeconds(seconds)).ShouldBe(text);
    }

    [Fact]
    public void Clock_name_handles_singular_and_zero_minutes()
    {
        var labels = new VoiceControlsLabels();
        labels.ClockName(0, 1).ShouldBe("1 second");
        labels.ClockName(0, 12).ShouldBe("12 seconds");
        labels.ClockName(1, 5).ShouldBe("1 minute 5 seconds");
        labels.ClockName(3, 0).ShouldBe("3 minutes 0 seconds");
    }

    // ---- Caption layout ----

    [Fact]
    public void Balanced_wrapping_keeps_the_line_count_and_evens_the_lines()
    {
        double[] widths = [40, 40, 40, 40, 40, 40, 40];
        var lines = new int[widths.Length];
        // Greedy at 300 puts six words on the first line and one on the second.
        VoiceTranscriptLayout.Greedy(widths, 5, 300, lines).ShouldBe(2);
        lines.Count(l => l == 0).ShouldBe(6);
        VoiceTranscriptLayout.Balance(widths, 5, 300, lines).ShouldBe(2);
        lines.Count(l => l == 0).ShouldBe(4);
        lines.Count(l => l == 1).ShouldBe(3);
    }

    [Fact]
    public void Words_split_on_any_white_space()
    {
        VoiceTranscriptLayout.Words("  Booked.  A\ttable\nfor four ").ShouldBe(["Booked.", "A", "table", "for", "four"]);
        VoiceTranscriptLayout.Words(null).ShouldBeEmpty();
    }

    // ---- Transcript ----

    [AvaloniaFact]
    public void Transcript_is_two_lines_tall_and_announces_only_complete_sentences()
    {
        var transcript = new VoiceTranscript { Width = 400 };
        var window = TestHelpers.Show(transcript);
        try
        {
            transcript.Bounds.Height.ShouldBe(44.8, 0.5);
            transcript.Text = "Could you book";
            Dispatcher.UIThread.RunJobs();
            transcript.Announcer.Announcement.ShouldBeNull();
            transcript.Text = "Could you book a table";
            transcript.IsComplete = true;
            transcript.Announcer.Announcement.ShouldBe("Could you book a table");
            transcript.Current!.WordCount.ShouldBe(5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Appended_words_enter_one_by_one_and_a_new_turn_crosses_over()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var transcript = new VoiceTranscript { Width = 400 };
        var window = TestHelpers.Show(transcript, ArcMotionLevel.Full);
        try
        {
            transcript.Speaker = VoiceTranscriptSpeaker.User;
            transcript.Text = "What is";
            window.UpdateLayout();
            clock.Advance(TimeSpan.FromSeconds(0.5), Frame);
            transcript.Text = "What is the";
            window.UpdateLayout();
            clock.Advance(TimeSpan.FromMilliseconds(50), Frame);
            var block = transcript.Current!;
            block.WordOpacity(0).ShouldBe(1);
            block.WordOpacity(2).ShouldBeInRange(0.3, 0.95);
            clock.Advance(TimeSpan.FromMilliseconds(400), Frame);
            block.WordOpacity(2).ShouldBe(1);

            transcript.Turn = 1;
            transcript.Speaker = VoiceTranscriptSpeaker.Assistant;
            transcript.Text = "";
            window.UpdateLayout();
            transcript.ExitingCount.ShouldBe(1);
            transcript.Current!.Turn.ShouldBe(1);
            clock.Advance(TimeSpan.FromMilliseconds(250), Frame);
            transcript.ExitingCount.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_text_that_does_not_extend_the_caption_is_rewritten()
    {
        var transcript = new VoiceTranscript { Width = 400 };
        var window = TestHelpers.Show(transcript);
        try
        {
            transcript.Text = "one two three";
            window.UpdateLayout();
            transcript.Text = "one four";
            window.UpdateLayout();
            transcript.Current!.Joined.ShouldBe("one four");
            transcript.ExitingCount.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    // ---- Controls ----

    [AvaloniaFact]
    public void Start_shows_the_live_controls_and_moves_the_focus_to_mute()
    {
        var controls = new VoiceControls();
        var window = TestHelpers.Show(controls);
        try
        {
            var started = 0;
            controls.StartRequested += (_, _) => started++;
            var start = controls.StartButton!;
            AutomationProperties.GetName(start).ShouldBe("Start voice chat");
            start.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            controls.IsActive.ShouldBeTrue();
            started.ShouldBe(1);
            controls.MuteButton!.IsFocused.ShouldBeTrue();
            AutomationProperties.GetName(controls.MuteButton).ShouldBe("Mute microphone");
            AutomationProperties.GetName(controls.EndButton!).ShouldBe("End voice chat");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Mute_toggles_with_space_and_end_returns_the_focus_to_start()
    {
        var controls = new VoiceControls { IsActive = true };
        var window = TestHelpers.Show(controls);
        try
        {
            var mutes = new List<bool>();
            var ended = 0;
            controls.MuteChanged += (_, e) => mutes.Add(e.IsMuted);
            controls.EndRequested += (_, _) => ended++;
            var mute = controls.MuteButton!;
            mute.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
            controls.IsMuted.ShouldBeTrue();
            mute.IsChecked.ShouldBe(true);
            AutomationProperties.GetName(mute).ShouldBe("Unmute microphone");
            mutes.ShouldBe([true]);

            controls.EndButton!.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            controls.IsActive.ShouldBeFalse();
            ended.ShouldBe(1);
            controls.StartButton!.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Timer_shows_m_ss_and_speaks_minutes_and_seconds()
    {
        var controls = new VoiceControls { IsActive = true, Elapsed = TimeSpan.FromSeconds(65) };
        var window = TestHelpers.Show(controls);
        try
        {
            controls.Clock!.Text.ShouldBe("1:05");
            AutomationProperties.GetName(controls.Clock).ShouldBe("1 minute 5 seconds");
            controls.Labels = new VoiceControlsLabels { ClockName = (m, s) => $"{m}m {s}s" };
            AutomationProperties.GetName(controls.Clock).ShouldBe("1m 5s");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pill_width_springs_to_the_new_group()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var controls = new VoiceControls();
        var window = TestHelpers.Show(controls, ArcMotionLevel.Full);
        try
        {
            window.UpdateLayout();
            var pill = TestHelpers.Descendant<VoiceControlsPill>(controls);
            var idle = pill.TargetWidth;
            idle.ShouldBeGreaterThan(0);
            controls.IsActive = true;
            window.UpdateLayout();
            var live = pill.TargetWidth;
            live.ShouldNotBe(idle);
            clock.Advance(TimeSpan.FromMilliseconds(80), Frame);
            window.UpdateLayout();
            // Mid-spring the pill sits between the two widths.
            pill.CurrentWidth.ShouldBeInRange(Math.Min(idle, live) + 0.5, Math.Max(idle, live) - 0.5);
            clock.Advance(TimeSpan.FromMilliseconds(800), Frame);
            window.UpdateLayout();
            pill.CurrentWidth.ShouldBe(live, 0.5);
            // The start group is hidden once it has left.
            Dispatcher.UIThread.RunJobs();
            controls.StartButton!.IsEffectivelyVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    // ---- Session ----

    private static void Step(VoiceOrbSession session, double seconds)
    {
        for (var t = 0.0; t < seconds - 1e-9; t += 0.01)
        {
            session.Advance(TimeSpan.FromMilliseconds(10));
        }
    }

    [Fact]
    public void Session_follows_the_measured_word_timing()
    {
        var session = new VoiceOrbSession(VoiceOrbSamples.WeekendTrip);
        session.Text.ShouldBe("Tap the orb and plan a weekend trip.");
        session.Speaker.ShouldBe(VoiceTranscriptSpeaker.System);
        session.Start();
        session.State.ShouldBe(VoiceOrbState.Listening);
        session.Speaker.ShouldBe(VoiceTranscriptSpeaker.User);
        session.Text.ShouldBe("");
        Step(session, 0.89);
        session.Text.ShouldBe("");
        Step(session, 0.02);
        session.Text.ShouldBe("Could");
        // "Could" has 5 characters: the next word comes 150 + 22 × 5 = 260 ms later.
        Step(session, 0.24);
        session.Text.ShouldBe("Could");
        Step(session, 0.03);
        session.Text.ShouldBe("Could you");
        VoiceOrbSession.WordGap("Done.").TotalMilliseconds.ShouldBe(260);
    }

    [Fact]
    public void Session_thinks_after_the_sentence_then_speaks_and_loops()
    {
        var session = new VoiceOrbSession(VoiceOrbSamples.WeekendTrip);
        var words = new List<string>();
        session.AnswerWord += (_, w) => words.Add(w);
        session.Start();
        var user = VoiceOrbSamples.WeekendTrip.Turns[0].User.Split(' ');
        var hearing = 0.9 + user.Take(user.Length - 1).Sum(w => VoiceOrbSession.WordGap(w).TotalSeconds);
        Step(session, hearing + 0.69);
        session.State.ShouldBe(VoiceOrbState.Listening);
        session.IsComplete.ShouldBeFalse();
        Step(session, 0.02);
        session.State.ShouldBe(VoiceOrbState.Thinking);
        session.IsComplete.ShouldBeTrue();
        Step(session, 1.48);
        session.State.ShouldBe(VoiceOrbState.Thinking);
        Step(session, 0.03);
        session.State.ShouldBe(VoiceOrbState.Speaking);
        session.Speaker.ShouldBe(VoiceTranscriptSpeaker.Assistant);
        var answer = VoiceOrbSamples.WeekendTrip.Turns[0].Assistant.Split(' ');
        var speaking = 0.27 + answer.Take(answer.Length - 1).Sum(w => VoiceOrbSession.WordGap(w).TotalSeconds);
        Step(session, speaking + 0.01);
        words.Count.ShouldBe(answer.Length);
        session.IsComplete.ShouldBeTrue();
        Step(session, 1.25);
        session.State.ShouldBe(VoiceOrbState.Speaking);
        Step(session, 0.04);
        session.State.ShouldBe(VoiceOrbState.Listening);
        session.ExchangeIndex.ShouldBe(1);
        session.Elapsed.TotalSeconds.ShouldBe(hearing + 0.7 + 1.5 + speaking + 1.3, 0.05);
    }

    [Fact]
    public void Orb_taps_send_cancel_and_interrupt()
    {
        var session = new VoiceOrbSession(VoiceOrbSamples.WeekendTrip);
        session.Tap();
        session.IsActive.ShouldBeTrue();
        Step(session, 1.2);
        session.Tap();
        session.State.ShouldBe(VoiceOrbState.Thinking);
        session.Text.ShouldBe(VoiceOrbSamples.WeekendTrip.Turns[0].User);
        session.Tap();
        session.State.ShouldBe(VoiceOrbState.Listening);
        Step(session, 0.71);
        session.State.ShouldBe(VoiceOrbState.Thinking);
        Step(session, 1.5 + 0.3);
        session.State.ShouldBe(VoiceOrbState.Speaking);
        var spoken = session.Text;
        spoken.ShouldNotBeEmpty();
        session.Tap();
        session.State.ShouldBe(VoiceOrbState.Listening);
        session.ExchangeIndex.ShouldBe(1);
    }

    [Fact]
    public void Muting_pauses_the_conversation_but_not_the_call_timer()
    {
        var session = new VoiceOrbSession(VoiceOrbSamples.WeekendTrip);
        session.Start();
        Step(session, 1);
        var text = session.Text;
        session.SetMuted(true);
        Step(session, 3);
        session.Text.ShouldBe(text);
        session.State.ShouldBe(VoiceOrbState.Listening);
        session.Elapsed.TotalSeconds.ShouldBe(4, 0.05);
        session.SetMuted(false);
        Step(session, 0.5);
        session.Text.ShouldNotBe(text);
    }

    [Fact]
    public void Ending_the_call_shows_its_length()
    {
        var session = new VoiceOrbSession(VoiceOrbSamples.WeekendTrip);
        session.Start();
        Step(session, 7.4);
        var turn = session.Turn;
        session.End();
        session.IsActive.ShouldBeFalse();
        session.State.ShouldBe(VoiceOrbState.Idle);
        session.Turn.ShouldBe(turn + 1);
        session.Speaker.ShouldBe(VoiceTranscriptSpeaker.System);
        session.Text.ShouldBe("Call ended after 0:07.");
        session.IsComplete.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Attached_session_drives_all_three_controls()
    {
        var orb = new VoiceOrb();
        var transcript = new VoiceTranscript { Width = 400 };
        var controls = new VoiceControls();
        var panel = new StackPanel { Children = { orb, transcript, controls } };
        var window = TestHelpers.Show(panel);
        var session = new VoiceOrbSession(VoiceOrbSamples.WeekendTrip);
        using var link = session.Attach(orb, transcript, controls, runClock: false);
        try
        {
            transcript.Text.ShouldBe(VoiceOrbSamples.WeekendTrip.Intro);
            controls.Start();
            session.IsActive.ShouldBeTrue();
            orb.State.ShouldBe(VoiceOrbState.Listening);
            controls.IsMuted = true;
            session.IsMuted.ShouldBeTrue();
            orb.IsMuted.ShouldBeTrue();
            controls.IsMuted = false;
            Step(session, 2);
            transcript.Speaker.ShouldBe(VoiceTranscriptSpeaker.User);
            transcript.Text.ShouldNotBeNullOrEmpty();
            controls.Elapsed.ShouldBe(TimeSpan.FromSeconds(2));
            controls.End();
            orb.State.ShouldBe(VoiceOrbState.Idle);
            transcript.Speaker.ShouldBe(VoiceTranscriptSpeaker.System);
        }
        finally
        {
            window.Close();
        }
    }
}
