using System.Diagnostics;
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
/// The dictation parts: the headless state machine against a hand-driven recognizer, the mic and pill (faces, focus, keys,
/// the timer and Try again), the composer (live words in the field, insert, send, the toolbar label and its breakpoints,
/// autoplay), the waveform's ring buffer and the simulated recognizer.
/// </summary>
public class DictationButtonTests
{
    // ----- Helpers -------------------------------------------------------------------------------------------------

    /// <summary>The tests' own invented scripts (the sample the library used to ship; the Gallery now passes its own).</summary>
    private static readonly IReadOnlyList<IReadOnlyList<DictationPhrase>> s_scripts =
    [
        [
            new("Draft a short reply to the design team", "Draft a short reply to the desk team"),
            new("thanking them for the new onboarding screens,"),
            new("and ask if the empty states can ship on Friday.", "and ask if the empty states can ship on fry day"),
        ],
        [
            new("Summarize the last three support tickets about billing", "Summarize the last free support tickets about billing"),
            new("and list anything that still needs a refund."),
        ],
    ];

    private static SimulatedDictationRecognizer Simulation() => new() { Scripts = s_scripts };

    /// <summary>A recognizer the test drives: it hands out its handlers and completes start and stop when told.</summary>
    private sealed class ManualRecognizer : IDictationRecognizer
    {
        public DictationHandlers? Handlers { get; private set; }

        public CancellationToken Token { get; private set; }

        public int Starts { get; private set; }

        public TaskCompletionSource<IDictationSession>? PendingStart { get; private set; }

        public bool HoldStart { get; set; }

        public Exception? StartFailure { get; set; }

        public ManualSession Session { get; } = new();

        public Task<IDictationSession> StartAsync(DictationHandlers handlers, CancellationToken cancellationToken)
        {
            Starts++;
            Handlers = handlers;
            Token = cancellationToken;
            if (StartFailure is { } failure)
            {
                return Task.FromException<IDictationSession>(failure);
            }
            if (HoldStart)
            {
                PendingStart = new TaskCompletionSource<IDictationSession>();
                cancellationToken.Register(() => PendingStart.TrySetCanceled());
                return PendingStart.Task;
            }
            return Task.FromResult<IDictationSession>(Session);
        }
    }

    private sealed class ManualSession : IDictationSession
    {
        public string Final { get; set; } = "hello there";

        public bool HoldStop { get; set; }

        public TaskCompletionSource<string>? PendingStop { get; private set; }

        public int Stops { get; private set; }

        public Task<string> StopAsync()
        {
            Stops++;
            if (HoldStop)
            {
                PendingStop = new TaskCompletionSource<string>();
                return PendingStop.Task;
            }
            return Task.FromResult(Final);
        }
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

    private static void Press(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        Pump();
    }

    private static (DictationButton Composer, Window Window, ManualRecognizer Recognizer) OpenComposer(double width = 600,
        ArcMotionLevel level = ArcMotionLevel.Off, Action<DictationButton>? setup = null)
    {
        var recognizer = new ManualRecognizer();
        // The model label the library used to show by default, now the tests' own invented model.
        var composer = new DictationButton
        {
            Recognizer = recognizer,
            Model = new DictationModel("Atlas 3 Pro"),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
        };
        setup?.Invoke(composer);
        var window = TestHelpers.Show(composer, level);
        window.Width = width;
        window.Height = 500;
        Pump();
        return (composer, window, recognizer);
    }

    private static (DictationControl Control, Window Window, ManualRecognizer Recognizer) OpenControl(ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var recognizer = new ManualRecognizer();
        var control = new DictationControl { Recognizer = recognizer };
        var window = TestHelpers.Show(control, level);
        Pump();
        return (control, window, recognizer);
    }

    // ----- The state machine ---------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Controller_Runs_Idle_Starting_Listening_Processing_Idle()
    {
        var recognizer = new ManualRecognizer { HoldStart = true };
        recognizer.Session.HoldStop = true;
        var controller = new DictationController(recognizer);
        var statuses = new List<DictationStatus>();
        string? committed = null;
        controller.StatusChanged += (_, e) => statuses.Add(e.Status);
        controller.Committed += (_, e) => committed = e.Text;

        _ = controller.StartAsync();
        controller.Status.ShouldBe(DictationStatus.Starting);
        controller.IsActive.ShouldBeTrue();
        recognizer.PendingStart!.SetResult(recognizer.Session);
        Pump();
        controller.Status.ShouldBe(DictationStatus.Listening);

        recognizer.Handlers!.OnTranscript("Draft a reply", "to the team");
        controller.Transcript.ShouldBe(new DictationTranscript("Draft a reply", "to the team"));
        controller.Transcript.Text.ShouldBe("Draft a reply to the team");
        recognizer.Handlers.OnLevel(0.7);
        controller.Level.ShouldBe(0.7);
        recognizer.Handlers.OnLevel(4);
        controller.Level.ShouldBe(1);

        _ = controller.ConfirmAsync();
        controller.Status.ShouldBe(DictationStatus.Processing);
        controller.Level.ShouldBe(0);
        recognizer.Session.PendingStop!.SetResult("Draft a reply to the team.");
        Pump();
        controller.Status.ShouldBe(DictationStatus.Idle);
        committed.ShouldBe("Draft a reply to the team.");
        controller.Transcript.IsEmpty.ShouldBeTrue();
        statuses.ShouldBe([DictationStatus.Starting, DictationStatus.Listening, DictationStatus.Processing, DictationStatus.Idle]);
    }

    [AvaloniaFact]
    public void Controller_Commits_The_Transcript_When_The_Recognizer_Returns_Nothing()
    {
        var recognizer = new ManualRecognizer();
        recognizer.Session.Final = "";
        var controller = new DictationController(recognizer);
        string? committed = null;
        controller.Committed += (_, e) => committed = e.Text;
        _ = controller.StartAsync();
        Pump();
        recognizer.Handlers!.OnTranscript("one two", "three");
        _ = controller.ConfirmAsync();
        Pump();
        committed.ShouldBe("one two three");
    }

    [AvaloniaFact]
    public void Controller_Cancel_Aborts_The_Recognizer_And_Ignores_Late_Callbacks()
    {
        var recognizer = new ManualRecognizer();
        var controller = new DictationController(recognizer);
        var cancelled = 0;
        var committed = 0;
        controller.Cancelled += (_, _) => cancelled++;
        controller.Committed += (_, _) => committed++;
        _ = controller.StartAsync();
        Pump();
        var handlers = recognizer.Handlers!;
        handlers.OnTranscript("half a", "sentence");
        controller.Cancel();
        recognizer.Token.IsCancellationRequested.ShouldBeTrue();
        controller.Status.ShouldBe(DictationStatus.Idle);
        controller.Transcript.IsEmpty.ShouldBeTrue();
        cancelled.ShouldBe(1);
        handlers.OnTranscript("late", "words");
        handlers.OnError(DictationErrorCode.Network);
        controller.Transcript.IsEmpty.ShouldBeTrue();
        controller.Status.ShouldBe(DictationStatus.Idle);
        committed.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Controller_Maps_A_Blocked_Microphone_To_Denied_And_Other_Errors_To_Error()
    {
        var recognizer = new ManualRecognizer { StartFailure = new DictationException(DictationErrorCode.NotAllowed) };
        var controller = new DictationController(recognizer);
        DictationStatusChangedEventArgs? last = null;
        controller.StatusChanged += (_, e) => last = e;
        _ = controller.StartAsync();
        Pump();
        controller.Status.ShouldBe(DictationStatus.Denied);
        controller.Error.ShouldBe(DictationErrorCode.NotAllowed);
        last!.Error.ShouldBe(DictationErrorCode.NotAllowed);
        controller.Dismiss();
        controller.Status.ShouldBe(DictationStatus.Idle);
        controller.Error.ShouldBeNull();

        recognizer.StartFailure = new InvalidOperationException("boom");
        _ = controller.StartAsync();
        Pump();
        controller.Status.ShouldBe(DictationStatus.Error);
        controller.Error.ShouldBe(DictationErrorCode.Failed);

        recognizer.StartFailure = null;
        _ = controller.StartAsync();
        Pump();
        controller.Status.ShouldBe(DictationStatus.Listening);
        recognizer.Handlers!.OnError(DictationErrorCode.NoSpeech);
        controller.Status.ShouldBe(DictationStatus.Error);
        controller.Error.ShouldBe(DictationErrorCode.NoSpeech);
        recognizer.Token.IsCancellationRequested.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Controller_Counts_Elapsed_Time_And_Stops_Counting_On_Insert()
    {
        var recognizer = new ManualRecognizer();
        recognizer.Session.HoldStop = true;
        long now = 1_000;
        var controller = new DictationController(recognizer) { Now = () => now };
        _ = controller.StartAsync();
        Pump();
        now += Stopwatch.Frequency * 7;
        controller.Elapsed.TotalSeconds.ShouldBe(7, 0.01);
        _ = controller.ConfirmAsync();
        now += Stopwatch.Frequency * 3;
        controller.Elapsed.TotalSeconds.ShouldBe(7, 0.01);
    }

    [AvaloniaFact]
    public void Controller_Inserts_On_Its_Own_After_MaxDuration()
    {
        var recognizer = new ManualRecognizer();
        var controller = new DictationController(recognizer) { MaxDuration = TimeSpan.FromMilliseconds(60) };
        string? committed = null;
        controller.Committed += (_, e) => committed = e.Text;
        _ = controller.StartAsync();
        Pump();
        controller.Status.ShouldBe(DictationStatus.Listening);
        TestHelpers.PumpUntil(() => committed is not null, 3000).ShouldBeTrue();
        committed.ShouldBe("hello there");
        controller.Status.ShouldBe(DictationStatus.Idle);
    }

    // ----- The mic and its pill -------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Control_Shows_The_Mic_At_Rest_With_Its_Name()
    {
        var (control, window, _) = OpenControl();
        try
        {
            control.Status.ShouldBe(DictationStatus.Idle);
            control.Pill!.ActiveFace.ShouldBe(control.MicButton);
            control.Pill.CurrentWidth.ShouldBe(36);
            control.Bounds.Width.ShouldBe(36);
            AutomationProperties.GetName(control.MicButton!).ShouldBe("Dictate");
            Tooltip.GetTip(control.MicButton!).ShouldBe("Dictate");
            control.InsertButton!.IsEffectivelyVisible.ShouldBeFalse();
            control.Pill.FillOpacity.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Control_Springs_Into_The_216px_Pill_And_Back()
    {
        var (control, window, recognizer) = OpenControl();
        try
        {
            control.Start();
            Pump();
            control.Status.ShouldBe(DictationStatus.Listening);
            control.Pill!.ActiveFace!.Name.ShouldBe(DictationControl.RecordingPart);
            control.Pill.TargetWidth.ShouldBe(216);
            control.Bounds.Width.ShouldBe(216);
            control.Pill.FillOpacity.ShouldBe(1);
            control.Waveform!.IsLive.ShouldBeTrue();
            control.InsertButton!.IsEffectivelyVisible.ShouldBeTrue();
            control.MicButton!.IsVisible.ShouldBeFalse();
            // The hidden face takes no pointer and no Tab.
            control.MicButton.IsHitTestVisible.ShouldBeFalse();
            KeyboardNavigation.GetIsTabStop(control.MicButton).ShouldBeFalse();
            AutomationProperties.GetName(control.CancelButton!).ShouldBe("Cancel");
            AutomationProperties.GetName(control.InsertButton).ShouldBe("Insert");

            recognizer.Session.HoldStop = true;
            control.Confirm();
            Pump();
            control.Status.ShouldBe(DictationStatus.Processing);
            control.InsertButton.IsBusy.ShouldBeTrue();
            AutomationProperties.GetName(control.InsertButton).ShouldBe("Transcribing");
            control.Waveform.IsLive.ShouldBeFalse();
            recognizer.Session.PendingStop!.SetResult("done");
            Pump();
            control.Status.ShouldBe(DictationStatus.Idle);
            control.Pill.ActiveFace.ShouldBe(control.MicButton);
            control.Bounds.Width.ShouldBe(36);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Control_Raises_Its_Events_With_Their_Arguments()
    {
        var (control, window, recognizer) = OpenControl();
        try
        {
            var statuses = new List<DictationStatus>();
            var transcripts = new List<DictationTranscript>();
            string? committed = null;
            var words = 0;
            var cancelled = 0;
            var ended = 0;
            control.StatusChanged += (_, e) => statuses.Add(e.Status);
            control.TranscriptChanged += (_, e) => transcripts.Add(e.Transcript);
            control.Committed += (_, e) => { committed = e.Text; words = e.WordCount; };
            control.Cancelled += (_, _) => cancelled++;
            control.SpeechEnded += (_, _) => ended++;

            control.Start();
            Pump();
            recognizer.Handlers!.OnTranscript("", "plan a");
            recognizer.Handlers.OnTranscript("plan a trip", "");
            recognizer.Handlers.OnSpeechEnd();
            control.Transcript.Final.ShouldBe("plan a trip");
            control.Confirm();
            Pump();
            committed.ShouldBe("hello there");
            words.ShouldBe(2);
            ended.ShouldBe(1);
            transcripts.ShouldContain(new DictationTranscript("", "plan a"));
            transcripts[^1].IsEmpty.ShouldBeTrue();
            control.Announcer!.History.ShouldContain("Inserted 2 words");

            control.Start();
            Pump();
            control.Cancel();
            cancelled.ShouldBe(1);
            control.Announcer.Announcement.ShouldBe("Dictation cancelled");
            statuses.ShouldBe([
                DictationStatus.Starting, DictationStatus.Listening, DictationStatus.Processing, DictationStatus.Idle,
                DictationStatus.Starting, DictationStatus.Listening, DictationStatus.Idle,
            ]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Control_Announces_That_The_Simulated_Recognizer_Records_Nothing()
    {
        var control = new DictationControl { Recognizer = new SimulatedDictationRecognizer { StartDelay = TimeSpan.Zero } };
        var window = TestHelpers.Show(control);
        try
        {
            control.Start();
            TestHelpers.PumpUntil(() => control.Status == DictationStatus.Listening).ShouldBeTrue();
            control.Announcer!.Announcement.ShouldBe(DictationButtonLabels.Default.SimulatedAnnouncement);
            control.Cancel();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Starts_From_The_Mic_Moves_Focus_To_Insert_And_Escape_Returns_It()
    {
        var (control, window, _) = OpenControl();
        try
        {
            control.MicButton!.Focus(NavigationMethod.Tab);
            Pump();
            Press(window, PhysicalKey.Enter);
            control.Status.ShouldBe(DictationStatus.Listening);
            control.InsertButton!.IsFocused.ShouldBeTrue();

            Press(window, PhysicalKey.Escape);
            control.Status.ShouldBe(DictationStatus.Idle);
            control.MicButton.IsFocused.ShouldBeTrue();

            Press(window, PhysicalKey.Enter);
            control.Status.ShouldBe(DictationStatus.Listening);
            // Enter on Insert stops and inserts; the control alone gives the focus back to the mic.
            Press(window, PhysicalKey.Enter);
            control.Status.ShouldBe(DictationStatus.Idle);
            control.MicButton.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Blocked_Microphone_Shows_Try_Again_With_The_Mic_Off_Icon_And_Escape_Dismisses_It()
    {
        var (control, window, recognizer) = OpenControl();
        try
        {
            recognizer.StartFailure = new DictationException(DictationErrorCode.NotAllowed);
            control.MicButton!.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Enter);
            control.Status.ShouldBe(DictationStatus.Denied);
            control.Pill!.ActiveFace.ShouldBe(control.RetryButton);
            control.RetryButton!.Text.ShouldBe("Try again");
            control.RetryButton.Icon.ShouldBe(DictationControl.MicOff);
            control.RetryButton.IsFocused.ShouldBeTrue();
            control.Announcer!.Announcement!.ShouldContain("Microphone blocked");
            control.Pill.TargetWidth.ShouldBeGreaterThan(36);
            control.Pill.TargetWidth.ShouldBeLessThan(216);

            Press(window, PhysicalKey.Escape);
            control.Status.ShouldBe(DictationStatus.Idle);
            control.MicButton.IsFocused.ShouldBeTrue();

            recognizer.StartFailure = new DictationException(DictationErrorCode.Network);
            control.Start();
            Pump();
            control.Status.ShouldBe(DictationStatus.Error);
            control.RetryButton.Icon.ShouldBe(ArcIcons.CircleAlert);
            recognizer.StartFailure = null;
            control.RetryButton.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Enter);
            control.Status.ShouldBe(DictationStatus.Listening);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Timer_Reads_Minutes_And_Seconds()
    {
        var (control, window, _) = OpenControl();
        try
        {
            long now = 5_000;
            control.Controller.Now = () => now;
            control.Start();
            Pump();
            control.TimerText!.Text.ShouldBe("0:00");
            now += Stopwatch.Frequency * 65;
            TestHelpers.PumpUntil(() => control.TimerText.Text == "1:05", 2000).ShouldBeTrue();
            control.Cancel();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Springs_The_Width_And_Crossfades_The_Faces()
    {
        var (control, window, _) = OpenControl(ArcMotionLevel.Full);
        try
        {
            Pump();
            control.Start();
            Pump();
            control.Pill!.TargetWidth.ShouldBe(216);
            // The width is on its way, not there yet, and both faces are on screen while they crossfade.
            control.Pill.CurrentWidth.ShouldBeLessThan(216);
            TestHelpers.PumpUntil(() => !control.Pill.IsMoving && Math.Abs(control.Pill.CurrentWidth - 216) < 0.5, 3000).ShouldBeTrue();
            TestHelpers.PumpUntil(() => !control.MicButton!.IsVisible, 3000).ShouldBeTrue();
            control.Cancel();
        }
        finally
        {
            window.Close();
        }
    }

    // ----- The composer -------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Composer_Opens_On_The_Model_Label_With_The_Placeholder()
    {
        var (composer, window, _) = OpenComposer(setup: c => c.Placeholder = "Message Atlas");
        try
        {
            composer.LabelKind.ShouldBe("model");
            composer.Model!.Name.ShouldBe("Atlas 3 Pro");
            composer.Placeholder.ShouldBe("Message Atlas");
            composer.Field!.IsPlaceholderShown.ShouldBeTrue();
            AutomationProperties.GetName(composer.Field).ShouldBe("Message");
            composer.SendButton!.IsEnabled.ShouldBeFalse();
            composer.IsAutoPlaying.ShouldBeFalse();
            composer.Bounds.Width.ShouldBe(600);

            composer.Model = null;
            Pump();
            composer.LabelKind.ShouldBe("none");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Live_Words_Follow_The_Text_And_The_Guess_Stays_Muted()
    {
        var (composer, window, recognizer) = OpenComposer(setup: c => c.Text = "Note:");
        try
        {
            composer.StartDictation();
            Pump();
            composer.Field!.IsReadOnly.ShouldBeTrue();
            recognizer.Handlers!.OnTranscript("ship it", "on fry");
            Pump();
            var words = composer.Field.LiveWords;
            words.Select(w => w.Text).ShouldBe(["ship", "it", "on", "fry"]);
            words.Select(w => w.IsFinal).ShouldBe([true, true, false, false]);
            composer.Field.Presenter!.LaidOutText.ShouldBe("Note: ship it on fry");
            composer.Text.ShouldBe("Note:");
            composer.Field.IsPlaceholderShown.ShouldBeFalse();
            // Settled and guessed words are painted in different inks.
            words[0].Brush.Color.ShouldNotBe(words[2].Brush.Color);

            recognizer.Handlers.OnTranscript("ship it on Friday", "");
            Pump();
            composer.Field.LiveWords.Select(w => w.Text).ShouldBe(["ship", "it", "on", "Friday"]);
            composer.Field.LiveWords.All(w => w.IsFinal).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Corrected_Guess_Refades_Only_The_Words_That_Changed()
    {
        var (composer, window, recognizer) = OpenComposer(level: ArcMotionLevel.Full);
        try
        {
            composer.StartDictation();
            Pump();
            recognizer.Handlers!.OnTranscript("", "ship on fry");
            TestHelpers.PumpUntil(() => composer.Field!.LiveWords.All(w => w.Opacity >= 1), 3000).ShouldBeTrue();
            recognizer.Handlers.OnTranscript("", "ship on Friday");
            var words = composer.Field!.LiveWords;
            words[0].Opacity.ShouldBe(1);
            words[1].Opacity.ShouldBe(1);
            words[2].Opacity.ShouldBeLessThan(1);
            composer.CancelDictation();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Insert_Appends_To_The_Field_And_Returns_The_Focus_To_Its_End()
    {
        var (composer, window, recognizer) = OpenComposer(setup: c => c.Text = "Hi team,");
        try
        {
            string? committed = null;
            composer.Committed += (_, e) => committed = e.Text;
            recognizer.Session.Final = "the build is green.";
            var dictation = composer.Dictation!;
            dictation.MicButton!.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Enter);
            dictation.InsertButton!.IsFocused.ShouldBeTrue();
            recognizer.Handlers!.OnTranscript("the build", "is green");
            Pump();
            Press(window, PhysicalKey.Enter);
            composer.Status.ShouldBe(DictationStatus.Idle);
            composer.Text.ShouldBe("Hi team, the build is green.");
            committed.ShouldBe("the build is green.");
            composer.Field!.HasLive.ShouldBeFalse();
            composer.Field.IsReadOnly.ShouldBeFalse();
            composer.Field.IsFocused.ShouldBeTrue();
            composer.Field.CaretIndex.ShouldBe(composer.Text!.Length);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_Inserts_From_Anywhere_While_Listening_And_Escape_Cancels()
    {
        var (composer, window, recognizer) = OpenComposer();
        try
        {
            composer.Field!.Focus();
            composer.StartDictation();
            Pump();
            composer.Status.ShouldBe(DictationStatus.Listening);
            Press(window, PhysicalKey.Enter);
            composer.Status.ShouldBe(DictationStatus.Idle);
            composer.Text.ShouldBe("hello there");

            composer.StartDictation();
            Pump();
            recognizer.Handlers!.OnTranscript("drop", "this");
            Pump();
            Press(window, PhysicalKey.Escape);
            composer.Status.ShouldBe(DictationStatus.Idle);
            composer.Field.HasLive.ShouldBeFalse();
            composer.Text.ShouldBe("hello there");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_In_The_Field_Sends_And_The_Field_Clears_When_The_Send_Completes()
    {
        var (composer, window, _) = OpenComposer(setup: c => c.Text = "Summarize the tickets");
        try
        {
            var pending = new TaskCompletionSource();
            string? sent = null;
            composer.SendRequested += (_, e) =>
            {
                sent = e.Text;
                e.Completion = pending.Task;
            };
            composer.SendButton!.IsEnabled.ShouldBeTrue();
            composer.Field!.Focus();
            Pump();
            Press(window, PhysicalKey.Enter);
            sent.ShouldBe("Summarize the tickets");
            composer.IsSending.ShouldBeTrue();
            composer.SendButton.IsBusy.ShouldBeTrue();
            composer.Text.ShouldBe("Summarize the tickets");
            pending.SetResult();
            TestHelpers.PumpUntil(() => !composer.IsSending, 2000).ShouldBeTrue();
            composer.Text.ShouldBe(string.Empty);
            composer.SendButton.IsBusy.ShouldBeFalse();
            composer.Announcer!.Announcement.ShouldBe("Message sent");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Shift_Enter_Adds_A_Line_And_A_Failed_Send_Keeps_The_Text()
    {
        var (composer, window, _) = OpenComposer(setup: c => c.Text = "one");
        try
        {
            var sends = 0;
            composer.SendRequested += (_, e) =>
            {
                sends++;
                e.Completion = Task.FromException(new InvalidOperationException("offline"));
            };
            composer.Field!.Focus();
            composer.Field.CaretToEnd();
            Pump();
            Press(window, PhysicalKey.Enter, RawInputModifiers.Shift);
            sends.ShouldBe(0);
            composer.Text.ShouldBe("one\n");
            composer.Send();
            TestHelpers.PumpUntil(() => !composer.IsSending, 2000).ShouldBeTrue();
            sends.ShouldBe(1);
            composer.Text.ShouldBe("one\n");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Without_A_Handler_The_Send_Is_Simulated()
    {
        var (composer, window, _) = OpenComposer(setup: c => c.Text = "hi");
        try
        {
            composer.Send();
            Pump();
            composer.IsSending.ShouldBeTrue();
            TestHelpers.PumpUntil(() => !composer.IsSending, 3000).ShouldBeTrue();
            composer.Text.ShouldBe(string.Empty);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Toolbar_Label_Shows_Hints_While_Listening_And_The_Error_After_A_Failure()
    {
        var (composer, window, recognizer) = OpenComposer(width: 600);
        try
        {
            composer.StartDictation();
            Pump();
            composer.LabelKind.ShouldBe("hints");
            composer.CancelDictation();
            Pump();
            composer.LabelKind.ShouldBe("model");

            recognizer.StartFailure = new DictationException(DictationErrorCode.NotAllowed);
            composer.StartDictation();
            Pump();
            composer.Status.ShouldBe(DictationStatus.Denied);
            composer.LabelKind.ShouldBe("error");
            TestHelpers.Descendant<ArcGlyph>(composer.ToolbarLabel!).Data.ShouldBe(DictationControl.MicOff);
            composer.ToolbarLabel!.GetVisualDescendantsText().ShouldContain("Microphone blocked in system settings");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Breakpoints_Follow_The_Card_Width()
    {
        var (composer, window, recognizer) = OpenComposer(width: 440);
        try
        {
            composer.Classes.Contains(DictationButton.NarrowPseudoClass).ShouldBeTrue();
            composer.Classes.Contains(DictationButton.CompactPseudoClass).ShouldBeFalse();
            composer.StartDictation();
            Pump();
            // Under 460px the hints hide and the model stays.
            composer.LabelKind.ShouldBe("model");
            composer.CancelDictation();

            window.Width = 380;
            Pump();
            recognizer.StartFailure = new DictationException(DictationErrorCode.NotAllowed);
            composer.StartDictation();
            Pump();
            composer.ToolbarLabel!.GetVisualDescendantsText().ShouldContain("Mic blocked");
            composer.Dictation!.Dismiss();
            recognizer.StartFailure = null;

            window.Width = 340;
            Pump();
            composer.Classes.Contains(DictationButton.TinyPseudoClass).ShouldBeTrue();
            composer.StartDictation();
            Pump();
            composer.LabelKind.ShouldBe("none");
            // The pill keeps its width and the card does not grow.
            composer.Dictation.Bounds.Width.ShouldBe(216);
            composer.Bounds.Width.ShouldBe(340);
            composer.CancelDictation();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Send_While_Listening_Inserts_First_Then_Sends()
    {
        var (composer, window, _) = OpenComposer();
        try
        {
            string? sent = null;
            composer.SendRequested += (_, e) =>
            {
                sent = e.Text;
                e.Completion = Task.CompletedTask;
            };
            composer.StartDictation();
            Pump();
            composer.SendButton!.IsEnabled.ShouldBeTrue();
            composer.Send();
            TestHelpers.PumpUntil(() => sent is not null, 2000).ShouldBeTrue();
            sent.ShouldBe("hello there");
            TestHelpers.PumpUntil(() => composer.Text == string.Empty, 2000).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Composer_Re_Raises_Status_And_Transcript_Events()
    {
        var (composer, window, recognizer) = OpenComposer();
        try
        {
            var statuses = new List<DictationStatus>();
            DictationTranscript? last = null;
            composer.StatusChanged += (_, e) => statuses.Add(e.Status);
            composer.TranscriptChanged += (_, e) => last = e.Transcript;
            composer.StartDictation();
            Pump();
            recognizer.Handlers!.OnTranscript("a", "b");
            last.ShouldBe(new DictationTranscript("a", "b"));
            composer.CancelDictation();
            statuses.ShouldBe([DictationStatus.Starting, DictationStatus.Listening, DictationStatus.Idle]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Autoplay_Runs_With_The_Simulated_Recognizer_Until_The_Person_Touches_The_Card()
    {
        var composer = new DictationButton { Simulation = Simulation() };
        var window = TestHelpers.Show(composer);
        try
        {
            window.Width = 600;
            Pump();
            composer.AutoPlays.ShouldBeTrue();
            composer.IsAutoPlaying.ShouldBeTrue();
            composer.InterruptAutoPlay();
            composer.IsAutoPlaying.ShouldBeFalse();
            composer.AutoPlays.ShouldBeFalse();

            var other = new DictationButton { AutoPlay = false, Simulation = Simulation() };
            window.Content = other;
            Pump();
            other.IsAutoPlaying.ShouldBeFalse();
            // The app's own recognizer wins over a simulation: no autoplay, and the mic dictates with the recognizer.
            var recognizer = new ManualRecognizer();
            var withRecognizer = new DictationButton { Recognizer = recognizer, Simulation = Simulation() };
            window.Content = withRecognizer;
            Pump();
            withRecognizer.IsAutoPlaying.ShouldBeFalse();
            withRecognizer.Dictation!.Controller.Recognizer.ShouldBeSameAs(recognizer);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Without_A_Recognizer_Or_A_Simulation_The_Composer_Is_A_Plain_Field_And_The_Mic_Is_Unavailable()
    {
        var composer = new DictationButton { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        var window = TestHelpers.Show(composer);
        try
        {
            window.Width = 600;
            Pump();
            composer.Model.ShouldBeNull();
            composer.Placeholder.ShouldBeNull();
            composer.LabelKind.ShouldBe("none");
            composer.AutoPlays.ShouldBeFalse();
            composer.IsAutoPlaying.ShouldBeFalse();
            composer.Dictation!.Controller.Recognizer.ShouldBeNull();
            composer.Dictation.Controller.IsSimulated.ShouldBeFalse();
            composer.Dictation.Start();
            TestHelpers.PumpUntil(() => composer.Status == DictationStatus.Error, 2000).ShouldBeTrue();
            composer.Dictation.Error.ShouldBe(DictationErrorCode.Unsupported);
            composer.Text.ShouldBeNullOrEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Clearing_The_Simulation_Stops_The_Autoplay_And_Its_Dictation()
    {
        var simulation = new SimulatedDictationRecognizer { Scripts = s_scripts, StartDelay = TimeSpan.Zero };
        var composer = new DictationButton { Simulation = simulation, AutoPlay = false };
        var window = TestHelpers.Show(composer);
        try
        {
            window.Width = 600;
            Pump();
            composer.Dictation!.Controller.Recognizer.ShouldBeSameAs(simulation);
            composer.Dictation.Controller.IsSimulated.ShouldBeTrue();
            composer.AutoPlay = null;
            Pump();
            composer.IsAutoPlaying.ShouldBeTrue();
            composer.Dictation.Start();
            TestHelpers.PumpUntil(() => composer.Status == DictationStatus.Listening, 2000).ShouldBeTrue();

            composer.Simulation = null;
            Pump();
            composer.IsAutoPlaying.ShouldBeFalse();
            composer.AutoPlays.ShouldBeFalse();
            composer.Status.ShouldBe(DictationStatus.Idle);
            composer.Dictation.Controller.Recognizer.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Control_Dictates_With_Its_Simulation_And_Without_One_Is_Unavailable()
    {
        var control = new DictationControl
        {
            Simulation = new SimulatedDictationRecognizer
            {
                Scripts = [[new DictationPhrase("ship on Friday")]],
                StartDelay = TimeSpan.Zero,
                WordInterval = TimeSpan.FromMilliseconds(10),
            },
        };
        var window = TestHelpers.Show(control);
        try
        {
            var ended = false;
            control.SpeechEnded += (_, _) => ended = true;
            control.Start();
            TestHelpers.PumpUntil(() => ended, 5000).ShouldBeTrue();
            control.Transcript.Final.ShouldBe("ship on Friday");
            control.Announcer!.Announcement.ShouldBe(DictationButtonLabels.Default.SimulatedAnnouncement);
            control.Cancel();
            Pump();

            control.Simulation = null;
            control.Start();
            TestHelpers.PumpUntil(() => control.Status == DictationStatus.Error, 2000).ShouldBeTrue();
            control.Error.ShouldBe(DictationErrorCode.Unsupported);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Disabled_Composer_Disables_The_Mic_And_Send()
    {
        var (composer, window, _) = OpenComposer(setup: c => { c.Text = "hi"; c.IsEnabled = false; });
        try
        {
            composer.SendButton!.IsEffectivelyEnabled.ShouldBeFalse();
            composer.Dictation!.MicButton!.IsEffectivelyEnabled.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    // ----- Waveform, labels, simulation -----------------------------------------------------------------------------

    [AvaloniaFact]
    public void The_Waveform_Fits_Whole_Bars_Glides_To_Levels_And_Settles_To_Dots()
    {
        var level = 0.0;
        var waveform = new DictationWaveform { Width = 102, Height = 18, LevelSource = () => level };
        var window = TestHelpers.Show(waveform, ArcMotionLevel.Full);
        try
        {
            Pump();
            // (102 + 2) / 5 = 20 bars of 3px with 2px gaps, none clipped.
            waveform.BarCount.ShouldBe(20);
            waveform.IsLive = true;
            level = 0.8;
            for (var i = 0; i < 30; i++)
            {
                waveform.Step(1 / 60.0);
            }
            waveform.Shown(0).ShouldBeGreaterThan(0.3);
            // Older bars still hold the silence that came before.
            waveform.Shown(19).ShouldBeLessThan(waveform.Shown(0));

            waveform.IsLive = false;
            var running = true;
            for (var i = 0; i < 600 && running; i++)
            {
                running = waveform.Step(1 / 60.0);
            }
            running.ShouldBeFalse();
            waveform.IsSettled.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Pulses_The_Bars_In_Place()
    {
        var waveform = new DictationWaveform { Width = 52, Height = 18, LevelSource = () => 0.9 };
        var window = TestHelpers.Show(waveform, ArcMotionLevel.Reduced);
        try
        {
            Pump();
            waveform.IsLive = true;
            for (var i = 0; i < 20; i++)
            {
                waveform.Step(1 / 60.0);
            }
            // Every bar moves at once (nothing travels in from the right).
            for (var r = 0; r < waveform.BarCount; r++)
            {
                waveform.Shown(r).ShouldBeGreaterThan(0.1);
            }
            DictationWaveform.Profile(0, 10).ShouldBeLessThan(DictationWaveform.Profile(5, 10));
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void Labels_Format_Times_Counts_And_Errors()
    {
        var labels = DictationButtonLabels.Default;
        labels.Time(TimeSpan.FromSeconds(7.9)).ShouldBe("0:07");
        labels.Time(TimeSpan.FromSeconds(65)).ShouldBe("1:05");
        labels.InsertedAnnouncement(1).ShouldBe("Inserted 1 word");
        labels.InsertedAnnouncement(12).ShouldBe("Inserted 12 words");
        labels.Error(DictationErrorCode.NotAllowed).ShouldBe("Microphone blocked in system settings");
        labels.ErrorShort(DictationErrorCode.NotAllowed).ShouldBe("Mic blocked");
        foreach (var code in Enum.GetValues<DictationErrorCode>())
        {
            labels.Error(code).ShouldNotBeNullOrWhiteSpace();
            labels.ErrorShort(code).Length.ShouldBeLessThan(labels.Error(code).Length + 1);
            labels.ErrorAnnouncement(code).ShouldNotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void Text_Helpers_Join_Count_And_Split_Words()
    {
        DictationText.Join("a ", " b").ShouldBe("a b");
        DictationText.Join("", "b").ShouldBe("b");
        DictationText.Join("a", null).ShouldBe("a");
        DictationText.CountWords("  two  words ").ShouldBe(2);
        DictationText.CountWords("").ShouldBe(0);
        var words = new List<string>();
        DictationText.SplitWords(" ship  it\non ", words);
        words.ShouldBe(["ship", "it", "on"]);
        new DictationTranscript("a", "").IsEmpty.ShouldBeFalse();
        DictationTranscript.Empty.IsEmpty.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void The_Simulated_Recognizer_Streams_Guesses_Corrects_Them_And_Settles()
    {
        var recognizer = new SimulatedDictationRecognizer
        {
            StartDelay = TimeSpan.Zero,
            WordInterval = TimeSpan.FromMilliseconds(10),
            FinalizeDelay = TimeSpan.FromMilliseconds(10),
        };
        recognizer.Scripts = [[new DictationPhrase("ship on Friday", "ship on fry")]];
        var controller = new DictationController(recognizer);
        var interims = new List<string>();
        var ended = false;
        string? committed = null;
        double peak = 0;
        controller.TranscriptChanged += (_, e) => interims.Add(e.Interim);
        controller.SpeechEnded += (_, _) => ended = true;
        controller.Committed += (_, e) => committed = e.Text;
        _ = controller.StartAsync();
        TestHelpers.PumpUntil(() =>
        {
            peak = Math.Max(peak, controller.Level);
            return ended;
        }, 5000).ShouldBeTrue();
        interims.ShouldContain("ship on fry");
        interims.ShouldContain("ship on Friday");
        controller.Transcript.Final.ShouldBe("ship on Friday");
        peak.ShouldBeGreaterThan(0);
        _ = controller.ConfirmAsync();
        TestHelpers.PumpUntil(() => committed is not null, 3000).ShouldBeTrue();
        committed.ShouldBe("ship on Friday");
        recognizer.IsSimulated.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void The_Simulated_Recognizer_Can_Deny_Like_A_Blocked_Microphone()
    {
        var controller = new DictationController(new SimulatedDictationRecognizer { Deny = true, StartDelay = TimeSpan.Zero });
        _ = controller.StartAsync();
        TestHelpers.PumpUntil(() => controller.Status == DictationStatus.Denied, 2000).ShouldBeTrue();
        controller.Error.ShouldBe(DictationErrorCode.NotAllowed);
    }
}

internal static class DictationTestExtensions
{
    /// <summary>All text shown under a visual, joined by spaces.</summary>
    public static string GetVisualDescendantsText(this Visual root) =>
        string.Join(" ", Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(root).OfType<TextBlock>().Select(t => t.Text));
}
