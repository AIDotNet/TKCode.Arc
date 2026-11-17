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
/// PiiRedaction: the pure helpers (detect, redact, restore, Luhn, streaming chunks) and the card: the simulation it opens
/// on (an invented fixture), the empty field,
/// scan, Show original, send with the simulated and an app's model, stop, failures and retry, reset, the keyboard model,
/// the two-way message, the breakpoints and the labels.
/// </summary>
public class PiiRedactionTests
{
    private const string Sample = PiiRedactionFixture.Message;

    /// <summary>The invented conversation.</summary>
    private static PiiRedactionSimulation Demo() => new()
    {
        Message = PiiRedactionFixture.Message,
        Names = PiiRedactionFixture.Names,
        ModelName = PiiRedactionFixture.ModelName,
        Reply = PiiRedactionFixture.Reply,
    };

    /// <summary>A card playing the fixture conversation (pass <paramref name="simulate"/> false for a card with no data).</summary>
    private static (PiiRedaction Card, Window Window) Open(Action<PiiRedaction>? setup = null, double width = 600,
        ArcMotionLevel level = ArcMotionLevel.Off, bool simulate = true)
    {
        var card = new PiiRedaction
        {
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
            // Every wait of the flow finishes at once; a test that needs one in flight passes a gate through Sender.
            Delay = static (_, token) =>
            {
                token.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            },
        };
        if (simulate)
        {
            card.Simulation = Demo();
        }
        setup?.Invoke(card);
        var window = TestHelpers.Show(card, level);
        window.Width = width;
        window.Height = 1000;
        Pump();
        return (card, window);
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

    private static void ScanAndPump(PiiRedaction card)
    {
        _ = card.Scan();
        Pump();
    }

    // ---- Pure helpers ----

    [Fact]
    public void Detect_Finds_The_Sample_Entities_In_Order()
    {
        var matches = PiiRedactor.Detect(Sample, ["Juniper Vale"]);
        matches.Select(m => m.Type).ShouldBe([PiiType.Name, PiiType.Card, PiiType.Email, PiiType.Phone]);
        matches.Select(m => Sample[m.Start..m.End])
               .ShouldBe(["Juniper Vale", "4242 4242 4242 4242", "juniper.vale@example.com", "(555) 555-0142"]);
    }

    [Fact]
    public void Detect_Uses_Introductions_And_The_Names_List()
    {
        var text = "Hello, this is Orrin Pell. Please loop in Tamsin Roe as well.";
        var withoutList = PiiRedactor.Detect(text);
        withoutList.Count.ShouldBe(1);
        text[withoutList[0].Start..withoutList[0].End].ShouldBe("Orrin Pell");
        var withList = PiiRedactor.Detect(text, ["tamsin roe"]);
        withList.Select(m => text[m.Start..m.End]).ShouldBe(["Orrin Pell", "Tamsin Roe"]);
    }

    [Fact]
    public void Detect_Checks_Luhn_And_Digit_Counts()
    {
        PiiRedactor.PassesLuhn("4242 4242 4242 4242").ShouldBeTrue();
        PiiRedactor.PassesLuhn("4242 4242 4242 4241").ShouldBeFalse();
        // A 16 digit number that fails Luhn is not a card and too long for a phone.
        PiiRedactor.Detect("ref 4242 4242 4242 4241").ShouldBeEmpty();
        PiiRedactor.Detect("call 555-0142 now").Single().Type.ShouldBe(PiiType.Phone);
        // Six digits are too few for a phone; a date is not a phone.
        PiiRedactor.Detect("order 123456 placed 2026-10-29").ShouldBeEmpty();
        PiiRedactor.Detect("mail sam@example.com").Single().Type.ShouldBe(PiiType.Email);
    }

    [Fact]
    public void Redact_Numbers_Each_Type_And_Reuses_The_Token_Of_A_Repeated_Value()
    {
        var text = "a@example.com, b@example.com and A@EXAMPLE.COM";
        var result = PiiRedactor.Redact(text, PiiRedactor.Detect(text));
        result.Prompt.ShouldBe("[EMAIL_1], [EMAIL_2] and [EMAIL_1]");
        result.Entities.Select(e => e.Token).ShouldBe(["EMAIL_1", "EMAIL_2", "EMAIL_1"]);
        var last = result.Entities[2];
        result.Prompt[last.PromptStart..last.PromptEnd].ShouldBe("[EMAIL_1]");
        last.Value.ShouldBe("A@EXAMPLE.COM");
    }

    [Fact]
    public void Redact_Skips_Overlaps_And_Spans_Outside_The_Text()
    {
        var text = "Call Dana Wren";
        var result = PiiRedactor.Redact(text,
        [
            new PiiMatch(PiiType.Name, 5, 14),
            new PiiMatch(PiiType.Name, 10, 14),
            new PiiMatch(PiiType.Phone, 12, 40),
        ]);
        result.Prompt.ShouldBe("Call [NAME_1]");
        result.Entities.Count.ShouldBe(1);
    }

    [Fact]
    public void Restore_Puts_Values_Back_And_Leaves_Unknown_Tokens()
    {
        var result = PiiRedactor.Redact(Sample, PiiRedactor.Detect(Sample, ["Juniper Vale"]));
        PiiRedactor.Restore("Thanks [NAME_1], we wrote to [EMAIL_1] about [CARD_9].", result.Entities)
                   .ShouldBe("Thanks Juniper Vale, we wrote to juniper.vale@example.com about [CARD_9].");
    }

    [Fact]
    public void Stream_Chunks_Are_Three_To_Six_Words_And_Cover_The_Text()
    {
        const string text = "one two three four five six seven eight nine ten eleven twelve thirteen fourteen fifteen sixteen seventeen";
        var ends = PiiRedactionStreamText.Chunks(text);
        ends[^1].ShouldBe(text.Length);
        var start = 0;
        for (var i = 0; i < ends.Count; i++)
        {
            var words = text[start..ends[i]].Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            if (i < ends.Count - 1)
            {
                words.ShouldBeInRange(3, 6);
            }
            start = ends[i];
        }
    }

    // ---- The card ----

    [AvaloniaFact]
    public void Opens_Idle_On_The_Simulation()
    {
        var (card, window) = Open();
        try
        {
            card.Phase.ShouldBe(PiiRedactionPhase.Idle);
            card.EditorControl!.Text.ShouldBe(Sample);
            card.EditorControl.Opacity.ShouldBe(1);
            card.RedactedControl!.Opacity.ShouldBe(0);
            card.PrimaryText.ShouldBe("Scan");
            card.StatusText.ShouldBe("Ready to scan");
            card.Cell(PiiType.Email)!.Value.ShouldBe("Not checked");
            card.ShowOriginalSwitch!.IsEnabled.ShouldBeFalse();
            card.ResetButton!.IsTabStop.ShouldBeFalse();
            card.FlowModel!.Label.ShouldBe(PiiRedactionFixture.ModelName);
            TestHelpers.Descendant<TextBlock>(card, "PART_ReplyModel").Text.ShouldBe(PiiRedactionFixture.ModelName);
            card.IsSimulated.ShouldBeTrue();
            card.Classes.Contains(":simulated").ShouldBeTrue();
            card.SafePrompt.ShouldBeNull();
            card.IsAutoPlaying.ShouldBeFalse();
            AutomationProperties.GetName(card).ShouldBe("PII redaction");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Scan_Marks_Each_Entity_And_Shows_The_Safe_Prompt()
    {
        PiiRedactedEventArgs? redacted = null;
        var phases = new List<PiiRedactionPhase>();
        var (card, window) = Open(c =>
        {
            c.Redacted += (_, e) => redacted = e;
            c.PhaseChanged += (_, e) => phases.Add(e.NewPhase);
        });
        try
        {
            ScanAndPump(card);
            card.Phase.ShouldBe(PiiRedactionPhase.Redacted);
            phases.ShouldBe([PiiRedactionPhase.Scanning, PiiRedactionPhase.Redacted]);
            const string prompt = "Hi, I'm [NAME_1]. I was charged twice for my March order on my card [CARD_1]. " +
                                  "Please send the refund confirmation to [EMAIL_1] or call me at [PHONE_1].";
            card.SafePrompt.ShouldBe(prompt);
            redacted.ShouldNotBeNull();
            redacted!.Prompt.ShouldBe(prompt);
            redacted.Entities.Select(e => e.Token).ShouldBe(["NAME_1", "CARD_1", "EMAIL_1", "PHONE_1"]);
            card.StatusText.ShouldBe("4 items redacted");
            card.PrimaryText.ShouldBe("Send");
            card.Cell(PiiType.Name)!.Value.ShouldBe("NAME_1");
            card.Cell(PiiType.Card)!.IsFound.ShouldBeTrue();
            AutomationProperties.GetName(card.Cell(PiiType.Phone)!).ShouldBe("Phone, PHONE_1");
            card.OriginalMarks.Count.ShouldBe(4);
            card.OriginalMarks.ShouldAllBe(m => m.Opacity == 1);
            card.RedactedControl!.Opacity.ShouldBe(1);
            card.OriginalControl!.Opacity.ShouldBe(0);
            card.EditorControl!.Opacity.ShouldBe(0);
            card.EditorControl.IsTabStop.ShouldBeFalse();
            card.ShowOriginalSwitch!.IsEnabled.ShouldBeTrue();
            (AutomationProperties.GetHelpText(card.OriginalControl) ?? string.Empty).ShouldContain("juniper.vale@example.com: Email, redacted as EMAIL_1");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Nothing_Found_Says_So_In_Every_Cell()
    {
        var (card, window) = Open(c => c.Message = "Can you check the status of my order?");
        try
        {
            ScanAndPump(card);
            card.Phase.ShouldBe(PiiRedactionPhase.Redacted);
            card.StatusText.ShouldBe("No personal data found");
            foreach (var type in Enum.GetValues<PiiType>())
            {
                card.Cell(type)!.Value.ShouldBe("None found");
                card.Cell(type)!.IsFound.ShouldBeFalse();
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Show_Original_Flips_The_Layers_And_The_Label()
    {
        var (card, window) = Open();
        try
        {
            ScanAndPump(card);
            card.ShowOriginal = true;
            Pump();
            card.OriginalControl!.Opacity.ShouldBe(1);
            card.RedactedControl!.Opacity.ShouldBe(0);
            card.ShowOriginalSwitch!.IsChecked.ShouldBe(true);
            AutomationProperties.GetAccessibilityView(card.RedactedControl).ShouldBe(AccessibilityView.Raw);
            card.ShowOriginal = false;
            Pump();
            card.RedactedControl.Opacity.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Send_Simulates_The_Reply_And_Restores_The_Values()
    {
        PiiReplyRestoredEventArgs? restored = null;
        var phases = new List<PiiRedactionPhase>();
        var (card, window) = Open(c =>
        {
            c.ReplyRestored += (_, e) => restored = e;
            c.PhaseChanged += (_, e) => phases.Add(e.NewPhase);
        });
        try
        {
            ScanAndPump(card);
            _ = card.Send();
            Pump();
            card.Phase.ShouldBe(PiiRedactionPhase.Done);
            phases.Skip(2).ShouldBe([PiiRedactionPhase.Sending, PiiRedactionPhase.Thinking, PiiRedactionPhase.Streaming,
                PiiRedactionPhase.Restoring, PiiRedactionPhase.Done]);
            restored.ShouldNotBeNull();
            restored!.IsSimulated.ShouldBeTrue();
            restored.Raw.ShouldContain("[EMAIL_1]");
            restored.Restored.ShouldContain("juniper.vale@example.com");
            restored.Restored.ShouldNotContain("[");
            card.Reply.ShouldBe(restored.Restored);
            card.RestoredControl!.Text.ShouldBe(restored.Restored);
            card.RestoredControl.Opacity.ShouldBe(1);
            card.RestoredMarks.Count.ShouldBe(4);
            card.StreamControl!.RevealedText.ShouldBe(restored.Raw);
            card.StatusText.ShouldBe("Reply restored");
            card.PrimaryText.ShouldBe("Run again");
            card.Announcer!.Announcement.ShouldBe("Reply restored. Simulated, nothing was sent");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Apps_Model_Gets_The_Safe_Prompt_Only()
    {
        string? sent = null;
        var (card, window) = Open(c =>
        {
            c.ModelName = PiiRedactionFixture.LongModelName;
            c.Sender = (prompt, _) =>
            {
                sent = prompt;
                return Task.FromResult("Hello [NAME_1], noted [PHONE_1] and [CARD_7].");
            };
        });
        try
        {
            card.Classes.Contains(":simulated").ShouldBeFalse();
            ScanAndPump(card);
            _ = card.Send();
            Pump();
            sent.ShouldNotBeNull();
            sent!.ShouldNotContain("Juniper");
            sent.ShouldNotContain("4242");
            card.Reply.ShouldBe("Hello Juniper Vale, noted (555) 555-0142 and [CARD_7].");
            card.Announcer!.Announcement.ShouldBe("Reply restored");
            card.FlowModel!.Label.ShouldBe(PiiRedactionFixture.LongModelName);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Stop_While_The_Model_Works_Cancels_And_Keeps_The_Safe_Prompt()
    {
        var gate = new TaskCompletionSource<string>();
        CancellationToken seen = default;
        var (card, window) = Open(c => c.Sender = (_, token) =>
        {
            seen = token;
            token.Register(() => gate.TrySetCanceled(token));
            return gate.Task;
        });
        try
        {
            ScanAndPump(card);
            _ = card.Send();
            Pump();
            card.Phase.ShouldBe(PiiRedactionPhase.Thinking);
            card.StatusText.ShouldBe("Waiting for " + PiiRedactionFixture.ModelName);
            card.PrimaryText.ShouldBe("Stop");
            card.PrimaryButton!.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            seen.IsCancellationRequested.ShouldBeTrue();
            card.Phase.ShouldBe(PiiRedactionPhase.Redacted);
            card.StatusText.ShouldBe("Stopped");
            card.SafePrompt.ShouldNotBeNull();
            card.Reply.ShouldBeNull();
            card.PrimaryText.ShouldBe("Send");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Failed_Request_Shows_Its_Message_And_Retry_Sends_Again()
    {
        var calls = 0;
        var (card, window) = Open(c => c.Sender = (_, _) =>
        {
            calls++;
            return calls == 1 ? Task.FromException<string>(new InvalidOperationException("The model is busy.")) : Task.FromResult("OK [NAME_1]");
        });
        try
        {
            ScanAndPump(card);
            _ = card.Send();
            Pump();
            card.Phase.ShouldBe(PiiRedactionPhase.Error);
            card.StatusText.ShouldBe("The model is busy.");
            card.PrimaryText.ShouldBe("Retry");
            card.Classes.Contains(":error").ShouldBeTrue();
            card.RedactedControl!.Opacity.ShouldBe(1);
            card.PrimaryButton!.Focus();
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Pump();
            card.Phase.ShouldBe(PiiRedactionPhase.Done);
            card.Reply.ShouldBe("OK Juniper Vale");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Failed_Detector_Keeps_The_Field_And_Retry_Scans_Again()
    {
        var calls = 0;
        var (card, window) = Open(c =>
        {
            c.Message = "Reach Ada at 555-0100.";
            c.Detector = (text, _) =>
            {
                calls++;
                if (calls == 1)
                {
                    throw new InvalidOperationException("DLP unavailable");
                }
                IReadOnlyList<PiiMatch> spans = [new PiiMatch(PiiType.Name, 6, 9), new PiiMatch(PiiType.Phone, 13, 21)];
                return Task.FromResult(spans);
            };
        });
        try
        {
            ScanAndPump(card);
            card.Phase.ShouldBe(PiiRedactionPhase.Error);
            card.StatusText.ShouldBe("DLP unavailable");
            card.EditorControl!.Opacity.ShouldBe(1);
            card.ShowOriginalSwitch!.IsEnabled.ShouldBeFalse();
            card.PrimaryButton!.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            card.Phase.ShouldBe(PiiRedactionPhase.Redacted);
            card.SafePrompt.ShouldBe("Reach [NAME_1] at [PHONE_1].");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reset_Returns_To_The_Field_With_Focus()
    {
        var (card, window) = Open();
        try
        {
            ScanAndPump(card);
            card.ShowOriginal = true;
            Pump();
            card.Classes.Contains(":resettable").ShouldBeTrue();
            card.ResetButton!.IsTabStop.ShouldBeTrue();
            card.ResetButton.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            card.Phase.ShouldBe(PiiRedactionPhase.Idle);
            card.ShowOriginal.ShouldBeFalse();
            card.SafePrompt.ShouldBeNull();
            card.EditorControl!.IsFocused.ShouldBeTrue();
            card.Cell(PiiType.Name)!.Value.ShouldBe("Not checked");
            card.Classes.Contains(":resettable").ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Ctrl_Enter_In_The_Field_Scans_And_Moves_Focus_To_The_Action()
    {
        var (card, window) = Open();
        try
        {
            card.EditorControl!.Focus();
            Pump();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.Control);
            Pump();
            card.Phase.ShouldBe(PiiRedactionPhase.Redacted);
            card.PrimaryButton!.IsFocused.ShouldBeTrue();
            card.HasTakenOver.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Editing_The_Field_Writes_The_Message_Back()
    {
        var (card, window) = Open(c => c.Message = string.Empty);
        try
        {
            card.PrimaryButton!.IsEnabled.ShouldBeFalse();
            card.EditorControl!.Text = "Write to kim@example.com";
            Pump();
            card.Message.ShouldBe("Write to kim@example.com");
            card.HasTakenOver.ShouldBeTrue();
            card.PrimaryButton.IsEnabled.ShouldBeTrue();
            ScanAndPump(card);
            card.SafePrompt.ShouldBe("Write to [EMAIL_1]");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_New_Message_From_The_App_Starts_Over()
    {
        var (card, window) = Open();
        try
        {
            ScanAndPump(card);
            card.Message = "Nothing personal here.";
            Pump();
            card.Phase.ShouldBe(PiiRedactionPhase.Idle);
            card.EditorControl!.Text.ShouldBe("Nothing personal here.");
            card.SafePrompt.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Read_Only_Field_When_Not_Editable()
    {
        var (card, window) = Open(c => c.IsEditable = false);
        try
        {
            card.EditorControl!.IsReadOnly.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(600, 4, false)]
    [InlineData(480, 2, false)]
    [InlineData(360, 2, true)]
    public void Breakpoints_Follow_The_Cards_Width(double width, int columns, bool compact)
    {
        var (card, window) = Open(width: width);
        try
        {
            card.CellsPanel!.Columns.ShouldBe(columns);
            card.Classes.Contains(":compact").ShouldBe(compact);
            card.Bounds.Width.ShouldBeLessThanOrEqualTo(640);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Card_Never_Grows_Past_640()
    {
        var (card, window) = Open(width: 900);
        try
        {
            card.Bounds.Width.ShouldBe(640);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Labels_Replace_Every_Word()
    {
        var (card, window) = Open(c => c.Labels = new PiiRedactionLabels
        {
            Scan = "Prüfen",
            Ready = "Bereit",
            NotChecked = "Offen",
            Redacted = n => n + " entfernt",
        });
        try
        {
            card.PrimaryText.ShouldBe("Prüfen");
            card.StatusText.ShouldBe("Bereit");
            card.Cell(PiiType.Card)!.Value.ShouldBe("Offen");
            ScanAndPump(card);
            card.StatusText.ShouldBe("4 entfernt");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Several_Values_Of_One_Type_Show_The_First_Token_And_A_Count()
    {
        var (card, window) = Open(c => c.Message = "Copy a@example.com, b@example.com and c@example.com");
        try
        {
            ScanAndPump(card);
            card.Cell(PiiType.Email)!.Value.ShouldBe("EMAIL_1 +2");
            card.StatusText.ShouldBe("3 items redacted");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Scan_Staggers_The_Marks()
    {
        var waits = new List<TimeSpan>();
        var (card, window) = Open(level: ArcMotionLevel.Full, setup: c =>
        {
            c.AutoPlay = false;
            c.Delay = (time, token) =>
            {
                waits.Add(time);
                return Task.CompletedTask;
            };
        });
        try
        {
            ScanAndPump(card);
            card.Phase.ShouldBe(PiiRedactionPhase.Redacted);
            // Three 120ms gaps between four marks, then the last 160ms tint.
            waits.ShouldBe([TimeSpan.FromMilliseconds(120), TimeSpan.FromMilliseconds(120), TimeSpan.FromMilliseconds(120),
                TimeSpan.FromMilliseconds(160)]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Has_No_Stagger_And_No_Autoplay()
    {
        var waits = new List<TimeSpan>();
        var (card, window) = Open(level: ArcMotionLevel.Reduced, setup: c => c.Delay = (time, _) =>
        {
            waits.Add(time);
            return Task.CompletedTask;
        });
        try
        {
            card.IsAutoPlaying.ShouldBeFalse();
            ScanAndPump(card);
            _ = card.Send();
            Pump();
            card.Phase.ShouldBe(PiiRedactionPhase.Done);
            // Only the simulated model's thinking time remains: no stagger, no hops, no streaming.
            waits.ShouldBe([PiiRedaction.SimulatedThinking]);
            card.StreamControl!.ChunkCount.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Autoplay_Runs_At_Full_Motion_And_Stops_For_Good_On_Interaction()
    {
        var gate = new TaskCompletionSource();
        var (card, window) = Open(level: ArcMotionLevel.Full, setup: c => c.Delay = (time, token) =>
        {
            // The autoplay's first pause holds until the test lets it go.
            if (time == PiiRedaction.AutoPlayLead)
            {
                token.Register(() => gate.TrySetCanceled());
                return gate.Task;
            }
            return Task.CompletedTask;
        });
        try
        {
            TestHelpers.PumpUntil(() => card.IsAutoPlaying, 2000).ShouldBeTrue();
            card.ShowOriginal.ShouldBeFalse();
            card.EditorControl!.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.Control);
            Pump();
            card.HasTakenOver.ShouldBeTrue();
            card.IsAutoPlaying.ShouldBeFalse();
            card.Phase.ShouldNotBe(PiiRedactionPhase.Idle);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void No_Message_And_No_Simulation_Shows_The_Empty_Field()
    {
        var (card, window) = Open(level: ArcMotionLevel.Full, simulate: false);
        try
        {
            card.EditorControl!.Text.ShouldBe(string.Empty);
            card.PrimaryButton!.IsEnabled.ShouldBeFalse();
            card.FlowModel!.Label.ShouldBe("Model");
            card.IsAutoPlaying.ShouldBeFalse();
            card.EffectiveNames.ShouldBeEmpty();
            TestHelpers.Descendant<TextBlock>(card, "PART_ReplyModel").Text.ShouldBe(string.Empty);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Simulation_Plays_Its_Written_Reply()
    {
        var (card, window) = Open();
        try
        {
            ScanAndPump(card);
            _ = card.Send();
            TestHelpers.PumpUntil(() => card.Phase == PiiRedactionPhase.Done).ShouldBeTrue();
            card.Reply.ShouldBe(PiiRedactor.Restore(PiiRedactionFixture.Reply, card.Entities));
            card.Reply!.ShouldStartWith("Sorry about the double charge, Juniper Vale.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Your_Message_Wins_Over_The_Simulation_And_Gets_The_Stand_In_Reply()
    {
        var (card, window) = Open(c => c.Message = "This is Orrin Pell, reach me at orrin@example.com.");
        try
        {
            card.EditorControl!.Text.ShouldBe("This is Orrin Pell, reach me at orrin@example.com.");
            ScanAndPump(card);
            _ = card.Send();
            TestHelpers.PumpUntil(() => card.Phase == PiiRedactionPhase.Done).ShouldBeTrue();
            card.Reply.ShouldBe("Thanks for reaching out, Orrin Pell. I have looked into your request and can help with it. " +
                                "I will send an update to orrin@example.com as soon as I have one.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Setting_Simulation_To_Null_Stops_Playback_And_Shows_The_Empty_Field()
    {
        var (card, window) = Open();
        try
        {
            ScanAndPump(card);
            card.Phase.ShouldNotBe(PiiRedactionPhase.Idle);
            card.Simulation = null;
            Pump();
            card.Phase.ShouldBe(PiiRedactionPhase.Idle);
            card.EditorControl!.Text.ShouldBe(string.Empty);
            card.SafePrompt.ShouldBeNull();
            card.IsAutoPlaying.ShouldBeFalse();
            card.FlowModel!.Label.ShouldBe("Model");
        }
        finally
        {
            window.Close();
        }
    }
}

/// <summary>Invented test data for the PiiRedaction tests (people, companies and numbers are made up).</summary>
file static class PiiRedactionFixture
{
    /// <summary>The name the detector always flags.</summary>
    public const string Name = "Juniper Vale";

    /// <summary>The names the detector always flags.</summary>
    public static IReadOnlyList<string> Names { get; } = [Name];

    /// <summary>A double charge support request with a name, a test card, an email and a phone number.</summary>
    public const string Message =
        "Hi, I'm Juniper Vale. I was charged twice for my March order on my card 4242 4242 4242 4242. " +
        "Please send the refund confirmation to juniper.vale@example.com or call me at (555) 555-0142.";

    /// <summary>The model's reply, placeholders included (the guard restores them).</summary>
    public const string Reply =
        "Sorry about the double charge, [NAME_1]. I can see two payments for the March order on [CARD_1], and I have " +
        "refunded the second one. A confirmation is on its way to [EMAIL_1], and if anything looks off we will call you " +
        "at [PHONE_1]. The refund reaches your card in 3 to 5 business days.";

    /// <summary>The model the safe prompt goes to.</summary>
    public static string ModelName => "Grok 4.7";

    /// <summary>A long model name, to show it truncating in a narrow flow.</summary>
    public static string LongModelName => "Grok 4.20 (Non-Reasoning)";
}
