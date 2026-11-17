using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>
/// SpeechPlayer: the sample, Read aloud into the player and back, the clock and the spoken word, pause / resume / seek /
/// end, speed and voice (resuming from the same word, aborting the old request), failures and Retry, the app's playback,
/// the progress bar's keys, the paragraphs' reading look, feedback, labels, the narrow layout and the timing helpers.
/// </summary>
public class SpeechPlayerTests
{
    private const string Reply = "One two three four.\n\nFive six seven eight nine ten.";

    /// <summary>The tests' own invented sample (the one the library used to ship; the Gallery now passes its own).</summary>
    private const string SampleReply =
        "Lisbon in early May is a lovely choice. Mornings start near 16°C and afternoons reach the low twenties, so pack light layers and shoes with good grip for the steep, cobbled hills.\n\n" +
        "Spend your first morning in Alfama before the crowds arrive, then ride tram 28 across the old town. On day two, take an early train to Sintra and come back in time for sunset at a miradouro.";

    private static readonly IReadOnlyList<SpeechVoice> s_voices =
    [
        new SpeechVoice("ava", "Ava", "Warm and conversational"),
        new SpeechVoice("leo", "Leo", "Calm, lower register"),
        new SpeechVoice("maya", "Maya", "Bright and upbeat"),
        new SpeechVoice("noah", "Noah", "Clear and steady"),
    ];

    private static SpeechSimulation Sample() => new() { Text = SampleReply, AuthorName = "Atlas 3 Pro", Voices = s_voices };

    private static (SpeechPlayer Player, Window Window) Open(Action<SpeechPlayer>? setup = null, double width = 600,
        ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var player = new SpeechPlayer { SimulationDelay = TimeSpan.Zero, AutoPlay = false, ManualClock = true, Voices = s_voices };
        setup?.Invoke(player);
        var window = TestHelpers.Show(player, level);
        window.Width = width;
        window.Height = 600;
        Pump();
        return (player, window);
    }

    private static void Pump()
    {
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static void Press(Window window, PhysicalKey key)
    {
        window.KeyPressQwerty(key, RawInputModifiers.None);
        window.KeyReleaseQwerty(key, RawInputModifiers.None);
        Pump();
    }

    /// <summary>Exact timings: word i from i to i + 0.5 seconds.</summary>
    private static IReadOnlyList<SpeechWordTiming> Evenly(int words) =>
        Enumerable.Range(0, words).Select(i => new SpeechWordTiming(TimeSpan.FromSeconds(i), TimeSpan.FromSeconds(i + 0.5))).ToList();

    [AvaloniaFact]
    public void Shows_The_Simulations_Reply_With_Its_Author_And_The_Action_Row()
    {
        var (player, window) = Open(p =>
        {
            p.Voices = null;
            p.Simulation = Sample();
        });
        try
        {
            player.PlaysSimulation.ShouldBeTrue();
            player.EffectiveAuthor.ShouldBe("Atlas 3 Pro");
            player.Document.Paragraphs.Count.ShouldBe(2);
            player.ParagraphViews.Count.ShouldBe(2);
            player.EffectiveVoices.Select(v => v.Name).ShouldBe(["Ava", "Leo", "Maya", "Noah"]);
            player.EffectiveVoice.Name.ShouldBe("Ava");
            player.EffectiveSimulated.ShouldBeTrue();
            player.State.ShouldBe(SpeechPlayerState.Idle);
            player.IsOpen.ShouldBeFalse();
            player.ActionRow.ReadAloud!.Content.ShouldBe("Read aloud");
            player.ActionRow.Defaults!.IsVisible.ShouldBeTrue();
            player.ActionRow.Copy!.Value.ShouldBe(player.Document.Spoken);
            AutomationProperties.GetHelpText(player.ActionRow.ReadAloud).ShouldBe(player.Document.Spoken);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Text_Replaces_The_Simulation_And_Drops_The_Author()
    {
        var (player, window) = Open(p =>
        {
            p.Simulation = Sample();
            p.Text = Reply;
        });
        try
        {
            player.PlaysSimulation.ShouldBeFalse();
            player.EffectiveAuthor.ShouldBeNull();
            player.EffectiveAutoPlay.ShouldBeFalse();
            player.Document.Words.Count.ShouldBe(10);
            player.Document.Paragraphs[1].WordOffset.ShouldBe(4);
            player.AuthorName = "Atlas 3";
            player.EffectiveAuthor.ShouldBe("Atlas 3");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Read_Aloud_Opens_The_Player_And_Starts_Reading()
    {
        var played = new List<SpeechPlaybackEventArgs>();
        var (player, window) = Open(p =>
        {
            p.Text = Reply;
            p.Played += (_, e) => played.Add(e);
        });
        try
        {
            player.ActionRow.ReadAloud!.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Enter);
            player.IsOpen.ShouldBeTrue();
            player.State.ShouldBe(SpeechPlayerState.Playing);
            player.CurrentWordIndex.ShouldBe(0);
            played.Count.ShouldBe(1);
            played[0].Voice.Name.ShouldBe("Ava");
            played[0].Speed.ShouldBe(SpeechSpeed.Normal);
            played[0].Time.ShouldBe(TimeSpan.Zero);
            player.Duration.ShouldBeGreaterThan(TimeSpan.FromSeconds(2));
            player.Announcement.ShouldBe("Reading with Ava at 1× (simulated, no audio plays)");
            player.Controls.Play!.IsFocused.ShouldBeTrue();
            AutomationProperties.GetName(player.Controls.Play).ShouldBe("Pause");
            AutomationProperties.GetName(player.Controls).ShouldBe("Read aloud player");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Clock_Moves_The_Spoken_Word_And_The_Times()
    {
        var words = new List<SpeechWordEventArgs>();
        var (player, window) = Open(p =>
        {
            p.Text = Reply;
            p.Synthesize = _ => Task.FromResult(new SpeechAudio { Timings = Evenly(10) });
            p.WordChanged += (_, e) => words.Add(e);
        });
        try
        {
            player.Open();
            Pump();
            player.Duration.TotalSeconds.ShouldBe(9.75, 1e-6);
            words.Select(w => w.Word).ShouldBe(["One"]);
            for (var i = 0; i < 25; i++)
            {
                player.Step(0.1);
            }
            player.Position.TotalSeconds.ShouldBe(2.5, 1e-6);
            player.CurrentWordIndex.ShouldBe(2);
            words.Select(w => w.Word).ShouldBe(["One", "two", "three"]);
            player.Controls.Elapsed!.Text.ShouldBe("0:02");
            player.Controls.Total!.Text.ShouldBe("0:09");
            player.Controls.Progress!.Fraction.ShouldBe(2.5 / 9.75, 1e-6);
            AutomationProperties.GetHelpText(player.Controls.Progress).ShouldBe("0:02 of 0:09");

            // A stalled frame loses time instead of jumping (at most 0.1s of 1× time per frame).
            player.Step(5);
            player.Position.TotalSeconds.ShouldBe(2.6, 1e-6);

            // 2× doubles the clock.
            player.Speed = SpeechSpeed.Twice;
            player.Step(0.1);
            player.Position.TotalSeconds.ShouldBe(2.8, 1e-6);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pause_Resume_And_The_End()
    {
        var events = new List<string>();
        var (player, window) = Open(p =>
        {
            p.Text = Reply;
            p.Synthesize = _ => Task.FromResult(new SpeechAudio { Timings = Evenly(10), Duration = TimeSpan.FromSeconds(10) });
            p.Played += (_, _) => events.Add("play");
            p.Paused += (_, e) => events.Add("pause@" + e.WordIndex);
            p.Ended += (_, _) => events.Add("end");
        });
        try
        {
            player.Open();
            Pump();
            for (var i = 0; i < 32; i++)
            {
                player.Step(0.1);
            }
            player.TogglePlayback();
            player.State.ShouldBe(SpeechPlayerState.Paused);
            player.Announcement.ShouldBe("Paused at 0:03");
            AutomationProperties.GetName(player.Controls.Play!).ShouldBe("Play");
            player.TogglePlayback();
            player.State.ShouldBe(SpeechPlayerState.Playing);
            for (var i = 0; i < 80; i++)
            {
                player.Step(0.1);
            }
            player.State.ShouldBe(SpeechPlayerState.Finished);
            player.Position.ShouldBe(TimeSpan.FromSeconds(10));
            player.Announcement.ShouldBe("Finished reading");
            AutomationProperties.GetName(player.Controls.Play!).ShouldBe("Play again");
            player.ParagraphViews[1].LocalWord.ShouldBe(SpeechPlayerParagraph.AllSpoken);
            player.ReadingAmount.ShouldBe(0);
            events.ShouldBe(["play", "pause@3", "play", "end"]);

            // Play again starts over.
            player.Play();
            player.State.ShouldBe(SpeechPlayerState.Playing);
            player.Position.ShouldBe(TimeSpan.Zero);
            player.CurrentWordIndex.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Progress_Bar_Keys_Seek_And_Toggle()
    {
        var seeks = new List<double>();
        var (player, window) = Open(p =>
        {
            p.Text = Reply;
            p.Synthesize = _ => Task.FromResult(new SpeechAudio { Timings = Evenly(10), Duration = TimeSpan.FromSeconds(30) });
            p.Seeked += (_, e) => seeks.Add(e.Time.TotalSeconds);
        });
        try
        {
            player.Open();
            Pump();
            player.Pause();
            var bar = player.Controls.Progress!;
            bar.Focus(NavigationMethod.Tab);
            Pump();
            Press(window, PhysicalKey.ArrowRight);
            player.Position.TotalSeconds.ShouldBe(2, 1e-6);
            player.CurrentWordIndex.ShouldBe(2);
            Press(window, PhysicalKey.PageUp);
            player.Position.TotalSeconds.ShouldBe(12, 1e-6);
            Press(window, PhysicalKey.ArrowLeft);
            player.Position.TotalSeconds.ShouldBe(10, 1e-6);
            Press(window, PhysicalKey.PageDown);
            player.Position.TotalSeconds.ShouldBe(0, 1e-6);
            Press(window, PhysicalKey.End);
            player.State.ShouldBe(SpeechPlayerState.Finished);
            Press(window, PhysicalKey.Home);
            player.State.ShouldBe(SpeechPlayerState.Paused);
            seeks.ShouldBe([2, 12, 10, 0, 30, 0]);
            Press(window, PhysicalKey.K);
            player.State.ShouldBe(SpeechPlayerState.Playing);
            Press(window, PhysicalKey.Space);
            player.State.ShouldBe(SpeechPlayerState.Paused);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Drag_Reports_One_Seek_When_It_Ends()
    {
        var seeks = 0;
        var (player, window) = Open(p =>
        {
            p.Text = Reply;
            p.Synthesize = _ => Task.FromResult(new SpeechAudio { Timings = Evenly(10), Duration = TimeSpan.FromSeconds(10) });
            p.Seeked += (_, _) => seeks++;
        });
        try
        {
            player.Open();
            Pump();
            var bar = player.Controls.Progress!;
            bar.SeekTo!(0.2, true);
            bar.SeekTo!(0.4, true);
            seeks.ShouldBe(0);
            player.Position.TotalSeconds.ShouldBe(4, 1e-6);
            bar.SeekTo!(0.5, false);
            seeks.ShouldBe(1);
            player.Position.TotalSeconds.ShouldBe(5, 1e-6);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Click_On_A_Word_Jumps_There()
    {
        var (player, window) = Open(p =>
        {
            p.Text = Reply;
            p.Synthesize = _ => Task.FromResult(new SpeechAudio { Timings = Evenly(10) });
        });
        try
        {
            player.ParagraphViews[0].WordsClickable.ShouldBeFalse();
            player.Open();
            Pump();
            var second = player.ParagraphViews[1];
            second.WordsClickable.ShouldBeTrue();
            second.WordPressed!(6);
            player.CurrentWordIndex.ShouldBe(6);
            player.Position.TotalSeconds.ShouldBe(6, 1e-6);
            player.ParagraphViews[0].LocalWord.ShouldBe(SpeechPlayerParagraph.AllSpoken);
            second.LocalWord.ShouldBe(2);
            second.HighlightRect.ShouldNotBeNull();
            second.WordAtPoint(new Point(1, 1)).ShouldBe(4);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Switching_Voice_Aborts_The_Request_And_Resumes_From_The_Same_Word()
    {
        var requests = new List<(SpeechRequest Request, TaskCompletionSource<SpeechAudio> Answer)>();
        var voices = new List<string>();
        var (player, window) = Open(p =>
        {
            p.Text = Reply;
            p.Synthesize = request =>
            {
                var answer = new TaskCompletionSource<SpeechAudio>();
                requests.Add((request, answer));
                return answer.Task;
            };
            p.VoiceChanged += (_, e) => voices.Add(e.Voice.Name);
        });
        try
        {
            player.Open();
            Pump();
            player.State.ShouldBe(SpeechPlayerState.Preparing);
            player.Controls.Preparing!.Text.ShouldBe("Preparing Ava's voice");
            player.Controls.Preparing.IsShimmering.ShouldBeTrue();
            AutomationProperties.GetName(player.Controls.Play!).ShouldBe("Preparing audio");
            requests[0].Answer.SetResult(new SpeechAudio { Timings = Evenly(10) });
            Pump();
            player.State.ShouldBe(SpeechPlayerState.Playing);
            for (var i = 0; i < 52; i++)
            {
                player.Step(0.1);
            }
            player.CurrentWordIndex.ShouldBe(5);

            player.SelectedVoiceId = "leo";
            voices.ShouldBe(["Leo"]);
            player.State.ShouldBe(SpeechPlayerState.Preparing);
            player.Announcement.ShouldBe("Switching to Leo");
            player.Controls.Preparing.Text.ShouldBe("Preparing Leo's voice");
            player.CurrentWordIndex.ShouldBe(5);

            // A second switch aborts the request for Leo.
            player.SelectedVoiceId = "maya";
            requests[1].Request.CancellationToken.IsCancellationRequested.ShouldBeTrue();
            requests[1].Answer.SetResult(new SpeechAudio { Timings = Evenly(10) });
            Pump();
            player.State.ShouldBe(SpeechPlayerState.Preparing);

            requests[2].Request.Voice.Name.ShouldBe("Maya");
            requests[2].Answer.SetResult(new SpeechAudio
            {
                Timings = Enumerable.Range(0, 10).Select(i => new SpeechWordTiming(TimeSpan.FromSeconds(2 * i), TimeSpan.FromSeconds(2 * i + 1))).ToList()
            });
            Pump();
            player.State.ShouldBe(SpeechPlayerState.Playing);
            player.CurrentWordIndex.ShouldBe(5);
            player.Position.TotalSeconds.ShouldBe(10, 1e-6);
            player.CachedVoices.ShouldBe(["ava", "maya"], ignoreOrder: true);

            // Back to a voice already fetched: no request, it plays at once.
            player.SelectedVoiceId = "ava";
            requests.Count.ShouldBe(3);
            player.State.ShouldBe(SpeechPlayerState.Playing);
            player.Position.TotalSeconds.ShouldBe(5, 1e-6);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Failure_Shows_The_Reason_And_Retry_Asks_Again()
    {
        var calls = 0;
        var (player, window) = Open(p =>
        {
            p.Text = Reply;
            p.Synthesize = _ =>
            {
                calls++;
                return calls == 1
                    ? Task.FromException<SpeechAudio>(new InvalidOperationException("The voice service is busy."))
                    : Task.FromResult(new SpeechAudio { Timings = Evenly(10) });
            };
        });
        try
        {
            player.Open();
            Pump();
            player.State.ShouldBe(SpeechPlayerState.Failed);
            player.Controls.Error!.Text.ShouldBe("Couldn't generate audio. The voice service is busy.");
            player.Announcement.ShouldBe("Couldn't generate audio. The voice service is busy.");
            AutomationProperties.GetName(player.Controls.Play!).ShouldBe("Retry");
            player.Controls.Play!.Shows.ShouldBe(SpeechPlayerState.Failed);
            player.Controls.Play.Focus();
            Press(window, PhysicalKey.Enter);
            calls.ShouldBe(2);
            player.State.ShouldBe(SpeechPlayerState.Playing);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Stop_Reading_Returns_The_Action_Row_And_Keeps_The_Speech()
    {
        var calls = 0;
        var closed = 0;
        var (player, window) = Open(p =>
        {
            p.Text = Reply;
            p.Synthesize = _ =>
            {
                calls++;
                return Task.FromResult(new SpeechAudio { Timings = Evenly(10) });
            };
            p.Closed += (_, _) => closed++;
        });
        try
        {
            player.Open();
            Pump();
            player.Step(0.1);
            player.Controls.Stop!.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Enter);
            player.IsOpen.ShouldBeFalse();
            player.State.ShouldBe(SpeechPlayerState.Idle);
            player.Position.ShouldBe(TimeSpan.Zero);
            player.CurrentWordIndex.ShouldBe(-1);
            player.ReadingAmount.ShouldBe(0);
            closed.ShouldBe(1);
            player.Announcement.ShouldBe("Stopped reading");
            player.ActionRow.ReadAloud!.IsFocused.ShouldBeTrue();

            player.Open();
            calls.ShouldBe(1);
            player.State.ShouldBe(SpeechPlayerState.Playing);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Speed_Button_Cycles_And_Its_Labels_Never_Resize()
    {
        var speeds = new List<SpeechSpeed>();
        var (player, window) = Open(p =>
        {
            p.Text = Reply;
            p.SpeedChanged += (_, e) => speeds.Add(e.Speed);
        });
        try
        {
            player.Open();
            Pump();
            var button = player.Controls.Speed!;
            var width = button.Bounds.Width;
            var labels = player.Controls.SpeedLabels!.Children.OfType<SpeechPlayerChoice>().ToList();
            labels.Select(l => l.Text).ShouldBe(["1×", "1.5×", "2×"]);
            labels.Single(l => l.IsShown).Text.ShouldBe("1×");
            button.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Enter);
            Press(window, PhysicalKey.Enter);
            Press(window, PhysicalKey.Enter);
            speeds.ShouldBe([SpeechSpeed.OneAndHalf, SpeechSpeed.Twice, SpeechSpeed.Normal]);
            player.Speed = SpeechSpeed.OneAndHalf;
            Pump();
            labels.Single(l => l.IsShown).Text.ShouldBe("1.5×");
            button.Bounds.Width.ShouldBe(width, 0.01);
            AutomationProperties.GetName(button).ShouldBe("Playback speed, 1.5×");
            player.Announcement.ShouldBe("Speed 1.5×");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Voice_Menu_Lists_The_Voices_As_Radio_Rows_With_The_Simulated_Note()
    {
        var (player, window) = Open(p => p.Text = Reply);
        try
        {
            player.Open();
            Pump();
            var entries = player.VoiceMenu.Items.OfType<MenuEntry>().ToList();
            entries.Count.ShouldBe(4);
            entries.All(e => e.ToggleType == MenuEntryToggleType.Radio).ShouldBeTrue();
            entries.Single(e => e.IsChecked).Tag.ShouldBeOfType<SpeechVoice>().Name.ShouldBe("Ava");
            AutomationProperties.GetName(entries[1]).ShouldBe("Leo, Calm, lower register");
            player.VoiceMenu.Items.OfType<MenuEntryLabel>().Single().Text.ShouldBe("Simulated preview, no audio plays");

            player.SelectedVoiceId = "noah";
            Pump();
            entries.Single(e => e.IsChecked).Tag.ShouldBeOfType<SpeechVoice>().Name.ShouldBe("Noah");
            player.Controls.VoiceLabels!.Children.OfType<SpeechPlayerChoice>().Single(c => c.IsShown).Text.ShouldBe("Noah");
            AutomationProperties.GetName(player.Controls.Voice!).ShouldBe("Voice, Noah");

            // A real service drops the note.
            player.Synthesize = _ => Task.FromResult(new SpeechAudio());
            player.IsSimulated = false;
            player.VoiceMenu.Items.OfType<MenuEntryLabel>().ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Apps_Playback_Is_The_Clock()
    {
        var audio = new FakeAudio { Duration = TimeSpan.FromSeconds(20) };
        var (player, window) = Open(p =>
        {
            p.Text = Reply;
            p.Synthesize = _ => Task.FromResult(new SpeechAudio { Audio = audio });
        });
        try
        {
            player.Open();
            Pump();
            audio.Playing.ShouldBeTrue();
            audio.Rate.ShouldBe(1);
            player.Duration.ShouldBe(TimeSpan.FromSeconds(20));
            // No timings came back: estimated and fitted to the audio's length.
            player.Timings!.Count.ShouldBe(10);
            player.Timings[^1].End.TotalSeconds.ShouldBe(20, 1e-6);

            audio.Position = TimeSpan.FromSeconds(11);
            player.Step(0.016);
            player.Position.TotalSeconds.ShouldBe(11, 1e-6);
            player.CurrentWordIndex.ShouldBeGreaterThan(3);

            player.Speed = SpeechSpeed.Twice;
            audio.Rate.ShouldBe(2);
            player.Seek(TimeSpan.FromSeconds(3));
            audio.Position.ShouldBe(TimeSpan.FromSeconds(3));
            player.Pause();
            audio.Playing.ShouldBeFalse();

            player.Play();
            audio.Position = TimeSpan.FromSeconds(20);
            player.Step(0.016);
            player.State.ShouldBe(SpeechPlayerState.Finished);
            audio.Playing.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reading_Dims_Upcoming_Words_And_Highlights_The_Spoken_One()
    {
        var (player, window) = Open(p =>
        {
            p.Text = Reply;
            p.Synthesize = _ => Task.FromResult(new SpeechAudio { Timings = Evenly(10) });
        });
        try
        {
            var first = player.ParagraphViews[0];
            first.Reading.ShouldBe(0);
            player.Open();
            Pump();
            first.Reading.ShouldBe(1);
            first.LocalWord.ShouldBe(0);
            player.ParagraphViews[1].LocalWord.ShouldBe(-1);
            first.HighlightRect.ShouldNotBeNull();
            // Motion off: no glide.
            player.Step(0.1);
            for (var i = 0; i < 10; i++)
            {
                player.Step(0.1);
            }
            first.LocalWord.ShouldBe(1);
            first.IsGliding.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Feedback_Is_Local_And_Announced_As_Simulated()
    {
        var (player, window) = Open(p => p.Text = Reply);
        try
        {
            player.ActionRow.Good!.Focus();
            Press(window, PhysicalKey.Enter);
            player.Feedback.ShouldBe(1);
            player.ActionRow.Good.IsActive.ShouldBeTrue();
            player.Announcement.ShouldBe("Marked as a good response (simulated)");
            player.ActionRow.Bad!.Focus();
            Press(window, PhysicalKey.Enter);
            player.Feedback.ShouldBe(-1);
            player.ActionRow.Good.IsActive.ShouldBeFalse();
            Press(window, PhysicalKey.Enter);
            player.Feedback.ShouldBeNull();
            player.Announcement.ShouldBe("Feedback removed");

            player.Actions = new TextBlock { Text = "Mine" };
            player.ActionRow.Defaults!.IsVisible.ShouldBeFalse();
            player.ActionRow.Actions!.IsVisible.ShouldBeTrue();
            player.Actions = null;
            player.ShowsDefaultActions = false;
            player.ActionRow.Defaults.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Labels_Reword_Everything()
    {
        var (player, window) = Open(p =>
        {
            p.Text = Reply;
            p.Labels = new SpeechPlayerLabels
            {
                ReadAloud = "Vorlesen",
                Pause = "Pausieren",
                Reading = (voice, speed, _) => $"Liest mit {voice} ({speed})"
            };
        });
        try
        {
            player.ActionRow.ReadAloud!.Content.ShouldBe("Vorlesen");
            player.Open();
            Pump();
            AutomationProperties.GetName(player.Controls.Play!).ShouldBe("Pausieren");
            player.Announcement.ShouldBe("Liest mit Ava (1×)");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Below_440_The_Total_Time_Steps_Aside_And_The_Card_Fills_Its_Container()
    {
        var (player, window) = Open(p => p.Text = Reply, width: 400);
        try
        {
            player.Open();
            Pump();
            player.IsNarrow.ShouldBeTrue();
            player.Controls.Total!.IsVisible.ShouldBeFalse();
            player.Bounds.Width.ShouldBe(400, 0.5);
            window.Width = 800;
            Pump();
            player.IsNarrow.ShouldBeFalse();
            player.Controls.Total.IsVisible.ShouldBeTrue();
            player.Bounds.Width.ShouldBe(640, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Simulation_Plays_Once_In_View()
    {
        var player = new SpeechPlayer { SimulationDelay = TimeSpan.Zero, Simulation = Sample() };
        var window = TestHelpers.Show(player);
        try
        {
            window.Height = 800;
            TestHelpers.PumpUntil(() => player.IsOpen).ShouldBeTrue();
            player.State.ShouldBe(SpeechPlayerState.Playing);
            player.EffectiveVoice.Name.ShouldBe("Ava");
            // Motion off: no loop through the voices.
            for (var i = 0; i < 400 && player.State == SpeechPlayerState.Playing; i++)
            {
                player.Step(0.1);
            }
            player.State.ShouldBe(SpeechPlayerState.Finished);
            player.IsSimulationLoopPending.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Without_Text_Or_A_Simulation_It_Waits_Empty_With_Read_Aloud_Disabled()
    {
        var player = new SpeechPlayer { SimulationDelay = TimeSpan.Zero, ManualClock = true };
        var window = TestHelpers.Show(player);
        try
        {
            window.Height = 800;
            Pump();
            player.PlaysSimulation.ShouldBeFalse();
            player.EffectiveAutoPlay.ShouldBeFalse();
            player.Document.Words.Count.ShouldBe(0);
            player.ParagraphViews.Count.ShouldBe(0);
            player.EffectiveAuthor.ShouldBeNull();
            player.EffectiveVoices.ShouldBeEmpty();
            player.EffectiveVoice.Id.ShouldBe(string.Empty);
            player.EffectiveVoice.Name.ShouldBe("Default voice");
            player.ActionRow.ReadAloud!.IsEnabled.ShouldBeFalse();
            player.IsOpen.ShouldBeFalse();
            player.State.ShouldBe(SpeechPlayerState.Idle);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Apps_Text_Wins_Over_The_Simulation_And_Clearing_The_Simulation_Stops_It()
    {
        var (player, window) = Open(p =>
        {
            p.Voices = null;
            p.Simulation = Sample();
        });
        try
        {
            player.Open();
            player.IsOpen.ShouldBeTrue();
            player.State.ShouldBe(SpeechPlayerState.Playing);
            player.EffectiveVoice.Name.ShouldBe("Ava");

            // Clearing the simulation stops the reading and empties the player.
            player.Simulation = null;
            player.IsOpen.ShouldBeFalse();
            player.State.ShouldBe(SpeechPlayerState.Idle);
            player.Document.Words.Count.ShouldBe(0);
            player.ActionRow.ReadAloud!.IsEnabled.ShouldBeFalse();

            // The app's own text wins over a simulation set later.
            player.Text = Reply;
            player.Simulation = Sample();
            player.PlaysSimulation.ShouldBeFalse();
            player.Document.Words.Count.ShouldBe(10);
            player.EffectiveAuthor.ShouldBeNull();
            player.EffectiveVoice.Name.ShouldBe("Default voice");
            player.ActionRow.ReadAloud.IsEnabled.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Text_Splits_Into_Paragraphs_And_Words()
    {
        var text = SpeechPlayerText.Parse("  Hello   world.\r\n\r\n\r\nSecond\nline here.  ");
        text.Paragraphs.Count.ShouldBe(2);
        text.Paragraphs[0].Text.ShouldBe("Hello   world.");
        text.Paragraphs[0].Words.ShouldBe([new SpeechPlayerWord(0, 5), new SpeechPlayerWord(8, 6)]);
        text.Paragraphs[1].WordOffset.ShouldBe(2);
        text.Words.ShouldBe(["Hello", "world.", "Second", "line", "here."]);
        SpeechPlayerText.Parse("   ").Paragraphs.ShouldBeEmpty();
        SpeechPlayerText.Clock(0).ShouldBe("0:00");
        SpeechPlayerText.Clock(65.9).ShouldBe("1:05");
        SpeechPlayerText.Clock(3729).ShouldBe("1:02:09");
        var timings = Evenly(3);
        SpeechPlayerText.WordAt(timings, -0.1).ShouldBe(-1);
        SpeechPlayerText.WordAt(timings, 0).ShouldBe(0);
        SpeechPlayerText.WordAt(timings, 1.7).ShouldBe(1);
        SpeechPlayerText.WordAt(timings, 50).ShouldBe(2);
    }

    [AvaloniaFact]
    public async Task Simulation_Estimates_Fits_And_Fails()
    {
        var timings = SpeechSimulation.EstimateTimings("Short words, then a considerably longer one.\n\nNext.");
        timings.Count.ShouldBe(8);
        for (var i = 1; i < timings.Count; i++)
        {
            timings[i].Start.ShouldBeGreaterThanOrEqualTo(timings[i - 1].End);
        }
        // The comma, the sentence end and the paragraph add pauses; longer words take longer.
        (timings[2].Start - timings[1].End).ShouldBeGreaterThan(TimeSpan.Zero);
        (timings[7].Start - timings[6].End).ShouldBeGreaterThan(timings[2].Start - timings[1].End);
        (timings[5].End - timings[5].Start).ShouldBeGreaterThan(timings[3].End - timings[3].Start);
        var slower = SpeechSimulation.EstimateTimings("Short words, then a considerably longer one.\n\nNext.", 85);
        slower[^1].End.TotalSeconds.ShouldBe(timings[^1].End.TotalSeconds * 2, 1e-6);

        var fitted = SpeechSimulation.FitTimings(timings, TimeSpan.FromSeconds(4));
        fitted[^1].End.TotalSeconds.ShouldBe(4, 1e-6);
        fitted[0].Start.ShouldBe(TimeSpan.Zero);
        SpeechSimulation.FitTimings([], TimeSpan.FromSeconds(4)).ShouldBeEmpty();

        var voice = new SpeechVoice("ava", "Ava");
        var audio = await SpeechSimulation.SynthesizeAsync(new SpeechRequest("One two", voice, CancellationToken.None),
            new SpeechSimulationOptions { Delay = TimeSpan.Zero });
        audio.Audio.ShouldBeNull();
        audio.Timings!.Count.ShouldBe(2);
        await Should.ThrowAsync<InvalidOperationException>(() => SpeechSimulation.SynthesizeAsync(
            new SpeechRequest("One", voice, CancellationToken.None), new SpeechSimulationOptions { Delay = TimeSpan.Zero, Fail = true }));
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => SpeechSimulation.SynthesizeAsync(
            new SpeechRequest("One", voice, cancelled.Token)));
    }

    private sealed class FakeAudio : ISpeechAudio
    {
        public TimeSpan Position { get; set; }

        public TimeSpan Duration { get; set; }

        public double Rate { get; set; } = 1;

        public bool Playing { get; private set; }

        public void Play() => Playing = true;

        public void Pause() => Playing = false;

        public void Seek(TimeSpan position) => Position = position;
    }
}
