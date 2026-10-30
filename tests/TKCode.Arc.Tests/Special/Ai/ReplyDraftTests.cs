using System.Threading.Channels;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>
/// ReplyDraft: the simulation it plays (an invented fixture), the empty card, auto start and the on-demand empty state, quick replies, tone and length rewrites
/// (with the reader's edits as Previous), the option groups' keyboard model, Insert (simulated, handled, with a handler,
/// Cmd/Ctrl+Enter), failures, the height hold, streaming with full and reduced motion, the tour, the breakpoints, labels
/// and the pure stream and simulation helpers.
/// </summary>
public class ReplyDraftTests
{
    /// <summary>A generator the test feeds chunk by chunk; each request gets its own channel.</summary>
    private sealed class Scripted
    {
        public List<ReplyDraftRequest> Requests { get; } = [];

        public Channel<string> Current { get; private set; } = Channel.CreateUnbounded<string>();

        public async IAsyncEnumerable<string> Generate(ReplyDraftRequest request)
        {
            var channel = Channel.CreateUnbounded<string>();
            Current = channel;
            Requests.Add(request);
            await foreach (var chunk in channel.Reader.ReadAllAsync(request.CancellationToken))
            {
                yield return chunk;
            }
        }

        public void Send(params string[] chunks)
        {
            foreach (var chunk in chunks)
            {
                Current.Writer.TryWrite(chunk);
            }
        }

        public void Finish(params string[] chunks)
        {
            Send(chunks);
            Current.Writer.TryComplete();
        }
    }

    /// <summary>
    /// A card playing the fixture email (pass <paramref name="simulate"/> false for a card with no data). A setup that sets a
    /// <see cref="ReplyDraft.Generator"/> gets the fixture email as its own message, so the generator writes the replies.
    /// </summary>
    private static (ReplyDraft Card, Window Window) Open(Action<ReplyDraft>? setup = null, ArcMotionLevel level = ArcMotionLevel.Off,
        double width = 640, bool simulate = true)
    {
        var card = new ReplyDraft { AutoPlay = false, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        if (simulate)
        {
            card.Simulation = ReplyDraftFixture.Simulation();
        }
        setup?.Invoke(card);
        if (card.Generator is not null && card.Message is null)
        {
            card.Message = ReplyDraftFixture.Message;
            card.Suggestions ??= ReplyDraftFixture.Suggestions;
            card.Model ??= ReplyDraftFixture.Model;
        }
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

    private static void Press(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        Pump();
    }

    private static void WaitFor(ReplyDraft card, ReplyDraftStatus status) =>
        TestHelpers.PumpUntil(() => card.Status == status).ShouldBeTrue($"status stayed {card.Status}");

    [AvaloniaFact]
    public void Opens_On_The_Simulation_And_Writes_The_First_Draft()
    {
        var (card, window) = Open();
        try
        {
            AutomationProperties.GetName(card).ShouldBe("Email from Maya Chen");
            card.Chips.Count.ShouldBe(3);
            card.Chips[0].IsChecked.ShouldBe(true);
            card.Chips[1].IsChecked.ShouldBe(false);
            card.BodyPanel!.Children.Count.ShouldBe(4);
            card.Status.ShouldBe(ReplyDraftStatus.Drafting);
            card.RequestCount.ShouldBe(1);
            WaitFor(card, ReplyDraftStatus.Ready);
            card.Draft.ShouldStartWith("Hi Maya,");
            card.Draft.ShouldContain("Tuesday at 10am works great");
            card.Editor.ShouldNotBeNull();
            card.DraftSwap!.Content.ShouldBeSameAs(card.Editor);
            card.StatusView!.Text.ShouldBe("Ready to edit");
            card.InsertButton!.IsEnabled.ShouldBeTrue();
            card.ToneGroup!.SelectedIndex.ShouldBe(0);
            card.LengthGroup!.SelectedIndex.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Request_Carries_Message_Tone_Length_And_Suggestion()
    {
        var gen = new Scripted();
        var (card, window) = Open(c =>
        {
            c.Generator = gen.Generate;
            c.Tone = ReplyDraftTone.Concise;
            c.Length = ReplyDraftLength.Long;
            c.SelectedSuggestionId = "thursday";
        });
        try
        {
            gen.Requests.Count.ShouldBe(1);
            var request = gen.Requests[0];
            request.Mode.ShouldBe(ReplyDraftMode.Draft);
            request.Tone.ShouldBe(ReplyDraftTone.Concise);
            request.Length.ShouldBe(ReplyDraftLength.Long);
            request.Suggestion!.Id.ShouldBe("thursday");
            request.Message.FromName.ShouldBe("Maya Chen");
            request.Previous.ShouldBeNull();
            card.EffectiveSimulated.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void AutoStart_Off_Shows_Draft_Reply_Until_Asked()
    {
        var (card, window) = Open(c => c.AutoStart = false);
        try
        {
            card.Status.ShouldBe(ReplyDraftStatus.Idle);
            card.RequestCount.ShouldBe(0);
            var empty = card.DraftSwap!.Content.ShouldBeOfType<ReplyDraftEmptyView>();
            empty.ButtonText.ShouldBe("Draft reply");
            card.RegenerateButton!.IsEnabled.ShouldBeFalse();
            card.InsertButton!.IsEnabled.ShouldBeFalse();
            card.StartDraft();
            card.Status.ShouldBe(ReplyDraftStatus.Drafting);
            card.RequestCount.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tone_Change_Rewrites_The_Finished_Draft_From_The_Edited_Text()
    {
        var gen = new Scripted();
        var (card, window) = Open(c => c.Generator = gen.Generate);
        try
        {
            gen.Finish("Hi Maya,\n\nTuesday works.");
            WaitFor(card, ReplyDraftStatus.Ready);
            card.Editor!.Text = "Hi Maya,\n\nTuesday works for me.";
            Pump();
            card.Draft.ShouldBe("Hi Maya,\n\nTuesday works for me.");
            card.Tone = ReplyDraftTone.Formal;
            card.Status.ShouldBe(ReplyDraftStatus.Rewriting);
            card.Editor.IsDimmed.ShouldBeTrue();
            card.Editor.IsReadOnly.ShouldBeTrue();
            card.StatusView!.Text.ShouldBe("Rewriting");
            var request = gen.Requests[^1];
            request.Mode.ShouldBe(ReplyDraftMode.Rewrite);
            request.Tone.ShouldBe(ReplyDraftTone.Formal);
            request.Previous.ShouldBe("Hi Maya,\n\nTuesday works for me.");
            ReplyDraftEventArgs? done = null;
            card.DraftCompleted += (_, e) => done = e;
            gen.Finish("Dear Maya,\n\nTuesday suits me.");
            WaitFor(card, ReplyDraftStatus.Ready);
            card.Draft.ShouldBe("Dear Maya,\n\nTuesday suits me.");
            card.Editor!.IsDimmed.ShouldBeFalse();
            done.ShouldNotBeNull();
            done!.Mode.ShouldBe(ReplyDraftMode.Rewrite);
            done.Tone.ShouldBe(ReplyDraftTone.Formal);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Style_Change_While_Streaming_Restarts_The_Draft()
    {
        var gen = new Scripted();
        var (card, window) = Open(c => c.Generator = gen.Generate);
        try
        {
            gen.Send("Hi Maya, ");
            Pump();
            card.Length = ReplyDraftLength.Short;
            gen.Requests.Count.ShouldBe(2);
            gen.Requests[0].CancellationToken.IsCancellationRequested.ShouldBeTrue();
            gen.Requests[1].Mode.ShouldBe(ReplyDraftMode.Draft);
            gen.Requests[1].Length.ShouldBe(ReplyDraftLength.Short);
            card.Status.ShouldBe(ReplyDraftStatus.Drafting);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Quick_Reply_Chip_Streams_A_Fresh_Draft_And_Raises_SuggestionSelected()
    {
        var gen = new Scripted();
        var (card, window) = Open(c => c.Generator = gen.Generate);
        try
        {
            gen.Finish("Hi Maya, sure.");
            WaitFor(card, ReplyDraftStatus.Ready);
            ReplyDraftSuggestion? chosen = null;
            card.SuggestionSelected += (_, e) => chosen = e.Suggestion;
            card.Chips[1].Focus();
            Press(window, PhysicalKey.Enter);
            chosen!.Id.ShouldBe("wednesday");
            card.SelectedSuggestionId.ShouldBe("wednesday");
            card.Chips[1].IsChecked.ShouldBe(true);
            card.Chips[0].IsChecked.ShouldBe(false);
            card.Status.ShouldBe(ReplyDraftStatus.Drafting);
            card.HeightBox!.IsHeld.ShouldBeTrue();
            gen.Requests[^1].Suggestion!.Id.ShouldBe("wednesday");
            gen.Requests[^1].Mode.ShouldBe(ReplyDraftMode.Draft);
            gen.Finish("Hi Maya, Wednesday?");
            WaitFor(card, ReplyDraftStatus.Ready);
            card.HeightBox.IsHeld.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Empty_Suggestions_Hide_The_Row()
    {
        var gen = new Scripted();
        var (card, window) = Open(c =>
        {
            c.Suggestions = [];
            c.Generator = gen.Generate;
        });
        try
        {
            card.Chips.Count.ShouldBe(0);
            gen.Requests[0].Suggestion.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tone_Group_Arrows_Home_And_End_Move_The_Selection_And_Raise_ToneChanged()
    {
        var gen = new Scripted();
        var (card, window) = Open(c => c.Generator = gen.Generate);
        try
        {
            var group = card.ToneGroup!;
            group.Options.Count.ShouldBe(3);
            group.Options[0].IsTabStop.ShouldBeTrue();
            group.Options[1].IsTabStop.ShouldBeFalse();
            var picks = new List<ReplyDraftTone>();
            card.ToneChanged += (_, e) => picks.Add(e.Tone);
            group.Options[0].Focus();
            Press(window, PhysicalKey.ArrowRight);
            card.Tone.ShouldBe(ReplyDraftTone.Concise);
            group.Options[1].IsChecked.ShouldBe(true);
            group.Options[1].IsTabStop.ShouldBeTrue();
            group.Options[1].IsFocused.ShouldBeTrue();
            Press(window, PhysicalKey.End);
            card.Tone.ShouldBe(ReplyDraftTone.Formal);
            Press(window, PhysicalKey.ArrowRight);
            card.Tone.ShouldBe(ReplyDraftTone.Friendly);
            Press(window, PhysicalKey.Home);
            card.Tone.ShouldBe(ReplyDraftTone.Friendly);
            picks.ShouldBe([ReplyDraftTone.Concise, ReplyDraftTone.Formal, ReplyDraftTone.Friendly]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Length_Set_From_Code_Is_Silent_But_Moves_The_Group()
    {
        var (card, window) = Open();
        try
        {
            var raised = false;
            card.LengthChanged += (_, _) => raised = true;
            card.Length = ReplyDraftLength.Long;
            card.LengthGroup!.SelectedIndex.ShouldBe(2);
            raised.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Ctrl_Enter_Inserts_And_A_Simulated_Insert_Says_So()
    {
        var gen = new Scripted();
        var (card, window) = Open(c => c.Generator = gen.Generate);
        try
        {
            gen.Finish("Hi Maya, sure.");
            WaitFor(card, ReplyDraftStatus.Ready);
            ReplyDraftEventArgs? inserted = null;
            card.InsertRequested += (_, e) => inserted = e;
            card.Editor!.Focus();
            Press(window, PhysicalKey.Enter, RawInputModifiers.Control);
            inserted.ShouldNotBeNull();
            inserted!.Draft.ShouldBe("Hi Maya, sure.");
            card.Status.ShouldBe(ReplyDraftStatus.Inserted);
            card.StatusView!.Text.ShouldBe("Inserted");
            card.LastAnnouncement!.ShouldContain("Simulated, nothing was sent");
            card.InsertButton!.IsEnabled.ShouldBeFalse();
            // An edit after inserting makes the draft ready to insert again.
            card.Editor.Text = "Hi Maya, sure thing.";
            Pump();
            card.Status.ShouldBe(ReplyDraftStatus.Ready);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Handled_InsertRequested_Is_Not_Simulated()
    {
        var gen = new Scripted();
        var (card, window) = Open(c => c.Generator = gen.Generate);
        try
        {
            gen.Finish("Hi Maya.");
            WaitFor(card, ReplyDraftStatus.Ready);
            card.InsertRequested += (_, e) => e.Handled = true;
            _ = card.Insert();
            Pump();
            card.Status.ShouldBe(ReplyDraftStatus.Inserted);
            card.LastAnnouncement.ShouldBe("Draft inserted");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void InsertHandler_Shows_A_Spinner_Until_It_Completes()
    {
        var gen = new Scripted();
        var pending = new TaskCompletionSource();
        string? received = null;
        var (card, window) = Open(c =>
        {
            c.Generator = gen.Generate;
            c.InsertHandler = args =>
            {
                received = args.Draft;
                return pending.Task;
            };
        });
        try
        {
            gen.Finish("Hi Maya, see you Tuesday.");
            WaitFor(card, ReplyDraftStatus.Ready);
            _ = card.Insert();
            Pump();
            received.ShouldBe("Hi Maya, see you Tuesday.");
            card.Status.ShouldBe(ReplyDraftStatus.Inserting);
            card.InsertButton!.IsLoading.ShouldBeTrue();
            card.StatusView!.IsWorking.ShouldBeTrue();
            pending.SetResult();
            WaitFor(card, ReplyDraftStatus.Inserted);
            card.InsertButton.IsLoading.ShouldBeFalse();
            card.LastAnnouncement.ShouldBe("Draft inserted");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Failing_InsertHandler_Keeps_The_Draft()
    {
        var gen = new Scripted();
        var (card, window) = Open(c =>
        {
            c.Generator = gen.Generate;
            c.InsertHandler = _ => Task.FromException(new InvalidOperationException("Composer is closed."));
        });
        try
        {
            gen.Finish("Hi Maya.");
            WaitFor(card, ReplyDraftStatus.Ready);
            _ = card.Insert();
            Pump();
            card.Status.ShouldBe(ReplyDraftStatus.Ready);
            card.StatusView!.Text.ShouldBe("Couldn't insert");
            card.LastAnnouncement.ShouldBe("Composer is closed.");
            card.Draft.ShouldBe("Hi Maya.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Failing_Generator_Shows_The_Error_With_Try_Again()
    {
        var calls = 0;
        var (card, window) = Open(c => c.Generator = request => Fail(++calls, request));
        try
        {
            WaitFor(card, ReplyDraftStatus.Failed);
            var alert = card.DraftSwap!.Content.ShouldBeOfType<ReplyDraftAlert>();
            alert.Message.ShouldBe("Network down.");
            alert.RetryText.ShouldBe("Try again");
            card.StatusView!.Text.ShouldBe("Couldn't draft");
            card.Draft.ShouldBeEmpty();
            Pump();
            alert.RetryButton!.Focus();
            Press(window, PhysicalKey.Enter);
            calls.ShouldBe(2);
            WaitFor(card, ReplyDraftStatus.Ready);
            card.Draft.ShouldBe("Recovered.");
        }
        finally
        {
            window.Close();
        }

        static async IAsyncEnumerable<string> Fail(int call, ReplyDraftRequest request)
        {
            await Task.Yield();
            if (call == 1)
            {
                throw new InvalidOperationException("Network down.");
            }
            yield return "Recovered.";
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Streams_Chunks_Into_The_Stream_View_Then_Swaps_To_The_Editor()
    {
        var gen = new Scripted();
        var (card, window) = Open(c => c.Generator = gen.Generate, ArcMotionLevel.Full);
        try
        {
            card.DraftSwap!.Content.ShouldBeOfType<ReplyDraftPending>();
            gen.Send("Hi Maya, ", "Tuesday works ");
            TestHelpers.PumpUntil(() => card.StreamView?.Text == "Hi Maya, Tuesday works ").ShouldBeTrue();
            card.DraftSwap.Content.ShouldBeSameAs(card.StreamView);
            card.Classes.Contains(ReplyDraft.BusyPseudoClass).ShouldBeTrue();
            gen.Finish("for me.");
            WaitFor(card, ReplyDraftStatus.Ready);
            card.StreamView.ShouldBeNull();
            card.DraftSwap.Content.ShouldBeSameAs(card.Editor);
            card.Draft.ShouldBe("Hi Maya, Tuesday works for me.");
            card.Classes.Contains(ReplyDraft.BusyPseudoClass).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Shows_The_Draft_Whole()
    {
        var gen = new Scripted();
        var (card, window) = Open(c => c.Generator = gen.Generate, ArcMotionLevel.Reduced);
        try
        {
            gen.Send("Hi Maya, ", "Tuesday ");
            Pump();
            card.StreamView.ShouldBeNull();
            card.DraftSwap!.Content.ShouldBeOfType<ReplyDraftPending>();
            gen.Finish("works.");
            WaitFor(card, ReplyDraftStatus.Ready);
            card.Draft.ShouldBe("Hi Maya, Tuesday works.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Regenerate_Holds_The_Height_Until_The_New_Draft_Finishes()
    {
        var gen = new Scripted();
        var (card, window) = Open(c => c.Generator = gen.Generate);
        try
        {
            gen.Finish("Line one.\n\nLine two.\n\nLine three.");
            WaitFor(card, ReplyDraftStatus.Ready);
            Pump();
            var before = card.HeightBox!.DisplayedHeight;
            card.Regenerate();
            Pump();
            card.HeightBox.IsHeld.ShouldBeTrue();
            card.HeightBox.DisplayedHeight.ShouldBe(before, 0.5);
            gen.Finish("Short.");
            WaitFor(card, ReplyDraftStatus.Ready);
            Pump();
            card.HeightBox.IsHeld.ShouldBeFalse();
            card.HeightBox.DisplayedHeight.ShouldBeLessThan(before);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tour_Steps_Through_Tone_And_Stops_For_Good_On_A_Key_Press()
    {
        var gen = new Scripted();
        var (card, window) = Open(c =>
        {
            c.Generator = gen.Generate;
            c.AutoPlay = true;
        });
        try
        {
            card.EffectiveAutoPlay.ShouldBeTrue();
            gen.Finish("Hi Maya.");
            WaitFor(card, ReplyDraftStatus.Ready);
            card.IsTourScheduled.ShouldBeTrue();
            card.TourNext();
            card.Tone.ShouldBe(ReplyDraftTone.Concise);
            card.Status.ShouldBe(ReplyDraftStatus.Rewriting);
            gen.Finish("Maya, fine.");
            WaitFor(card, ReplyDraftStatus.Ready);
            card.IsTourScheduled.ShouldBeTrue();
            card.Editor!.Focus();
            Press(window, PhysicalKey.ArrowLeft);
            card.IsTouched.ShouldBeTrue();
            card.IsTourScheduled.ShouldBeFalse();
            card.TourNext();
            card.Tone.ShouldBe(ReplyDraftTone.Concise);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Widths_Compact_The_Card_And_Stack_The_Groups()
    {
        var (card, window) = Open(width: 440);
        try
        {
            card.Classes.Contains(ReplyDraft.CompactPseudoClass).ShouldBeTrue();
            card.Classes.Contains(ReplyDraft.NarrowPseudoClass).ShouldBeTrue();
            card.ControlsPanel!.IsStacked.ShouldBeTrue();
            card.ControlsPanel.IsWrapped.ShouldBeTrue();
            card.Bounds.Width.ShouldBeLessThanOrEqualTo(440);
            window.Width = 800;
            Pump();
            card.Bounds.Width.ShouldBe(600, 0.5);
            card.Classes.Contains(ReplyDraft.CompactPseudoClass).ShouldBeFalse();
            card.ControlsPanel.IsStacked.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Labels_Reword_The_Card()
    {
        var gen = new Scripted();
        var (card, window) = Open(c =>
        {
            c.Generator = gen.Generate;
            c.Labels = new ReplyDraftLabels
            {
                ReplyWith = model => "Antworten mit " + model,
                Drafting = "Schreibt",
                Ready = "Fertig",
                Friendly = "Freundlich",
                InsertShortcut = "Strg Enter",
            };
        });
        try
        {
            TestHelpers.Descendant<TextBlock>(card, ReplyDraft.HeadingPart).Text.ShouldBe("Antworten mit " + ReplyDraftFixture.Model.Name);
            TestHelpers.Descendant<TextBlock>(card, ReplyDraft.HintPart).Text.ShouldBe("Strg Enter");
            card.StatusView!.Text.ShouldBe("Schreibt");
            card.ToneGroup!.Options[0].Content.ShouldBe("Freundlich");
            gen.Finish("Hallo.");
            WaitFor(card, ReplyDraftStatus.Ready);
            card.StatusView!.Text.ShouldBe("Fertig");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Targets_Use_Pointer_Sizes_Until_A_Touch_Press()
    {
        var (card, window) = Open();
        try
        {
            card.IsCoarse.ShouldBeFalse();
            card.Chips[0].Height.ShouldBe(32);
            card.ToneGroup!.Options[0].Height.ShouldBe(28);
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void Split_Makes_Chunks_Of_Three_To_Eight_Words_That_Add_Up()
    {
        var text = ReplyDraftFixture.Reply("tuesday", ReplyDraftTone.Formal, ReplyDraftLength.Long);
        var chunks = ReplyDraftStreams.Split(text);
        string.Concat(chunks).ShouldBe(text);
        for (var i = 0; i < chunks.Count; i++)
        {
            var words = ReplyDraftStreams.WordCount(chunks[i]);
            words.ShouldBeLessThanOrEqualTo(8);
            if (i < chunks.Count - 1)
            {
                words.ShouldBeGreaterThanOrEqualTo(3);
            }
        }
        ReplyDraftStreams.Split(null).ShouldBeEmpty();
    }

    [Fact]
    public void Simulated_Replies_Follow_Tone_Length_And_Suggestion()
    {
        var message = ReplyDraftFixture.Message;
        var simulation = ReplyDraftFixture.Simulation();
        string Reply(string id, ReplyDraftTone tone, ReplyDraftLength length) => simulation.Reply(new ReplyDraftRequest(message, tone, length,
            new ReplyDraftSuggestion(id, id), ReplyDraftMode.Draft, null, CancellationToken.None));
        Reply("tuesday", ReplyDraftTone.Friendly, ReplyDraftLength.Medium).ShouldStartWith("Hi Maya,");
        Reply("tuesday", ReplyDraftTone.Formal, ReplyDraftLength.Medium).ShouldStartWith("Dear Maya,");
        Reply("tuesday", ReplyDraftTone.Formal, ReplyDraftLength.Medium).ShouldEndWith("Kind regards,\nSam");
        var shortText = Reply("wednesday", ReplyDraftTone.Friendly, ReplyDraftLength.Short);
        var medium = Reply("wednesday", ReplyDraftTone.Friendly, ReplyDraftLength.Medium);
        var longText = Reply("wednesday", ReplyDraftTone.Friendly, ReplyDraftLength.Long);
        shortText.Length.ShouldBeLessThan(medium.Length);
        medium.Length.ShouldBeLessThan(longText.Length);
        shortText.ShouldContain("Wednesday afternoon");
        // An unknown quick reply gets the first reply for its tone and length.
        Reply("unknown", ReplyDraftTone.Friendly, ReplyDraftLength.Short)
            .ShouldBe(Reply("tuesday", ReplyDraftTone.Friendly, ReplyDraftLength.Short));
        new ReplyDraftSimulation().Reply(new ReplyDraftRequest(message, ReplyDraftTone.Friendly, ReplyDraftLength.Short, null,
            ReplyDraftMode.Draft, null, CancellationToken.None)).ShouldBe(string.Empty);
        ReplyDraftStreams.FirstName("Maya Chen").ShouldBe("Maya");
        message.Paragraphs.Count.ShouldBe(4);
    }

    [Fact]
    public async Task FromText_Streams_A_Whole_Text_In_Chunks()
    {
        var generator = ReplyDraftStreams.FromText(_ => Task.FromResult("one two three four five six seven eight nine ten eleven"));
        var request = new ReplyDraftRequest(ReplyDraftFixture.Message, ReplyDraftTone.Friendly, ReplyDraftLength.Medium, null,
            ReplyDraftMode.Draft, null, CancellationToken.None);
        var chunks = new List<string>();
        await foreach (var chunk in generator(request))
        {
            chunks.Add(chunk);
        }
        chunks.Count.ShouldBeGreaterThan(1);
        string.Concat(chunks).ShouldBe("one two three four five six seven eight nine ten eleven");
    }

    [Fact]
    public void Labels_Name_Tones_And_Lengths()
    {
        var labels = ReplyDraftLabels.Default;
        labels.ToneName(ReplyDraftTone.Concise).ShouldBe("Concise");
        labels.LengthName(ReplyDraftLength.Long).ShouldBe("Long");
        labels.Article("Maya Chen").ShouldBe("Email from Maya Chen");
        new ReplyDraftLabels { InsertShortcut = "X" }.EffectiveInsertShortcut.ShouldBe("X");
    }

    [AvaloniaFact]
    public void No_Message_And_No_Simulation_Shows_The_Empty_Card()
    {
        var (card, window) = Open(simulate: false);
        try
        {
            card.CanDraft.ShouldBeFalse();
            card.EffectiveAutoPlay.ShouldBeFalse();
            card.Status.ShouldBe(ReplyDraftStatus.Idle);
            card.RequestCount.ShouldBe(0);
            card.Chips.Count.ShouldBe(0);
            card.BodyPanel!.Children.Count.ShouldBe(0);
            AutomationProperties.GetName(card).ShouldBe("Email");
            TestHelpers.Descendant<TextBlock>(card, ReplyDraft.HeadingPart).Text.ShouldBe("Reply");
            var empty = card.DraftSwap!.Content.ShouldBeOfType<ReplyDraftEmptyView>();
            empty.Text.ShouldBe("No reply drafted yet.");
            empty.DraftButton!.IsEnabled.ShouldBeFalse();
            card.StartDraft();
            card.RequestCount.ShouldBe(0);
            card.InsertButton!.IsEnabled.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Simulation_Set_Later_Plays_Its_Email_And_Replies()
    {
        var (card, window) = Open(simulate: false);
        try
        {
            card.Simulation = ReplyDraftFixture.Simulation();
            Pump();
            card.Chips.Count.ShouldBe(3);
            card.EffectiveSimulated.ShouldBeTrue();
            TestHelpers.Descendant<TextBlock>(card, ReplyDraft.HeadingPart).Text.ShouldBe("Reply with " + ReplyDraftFixture.Model.Name);
            card.Status.ShouldBe(ReplyDraftStatus.Drafting);
            WaitFor(card, ReplyDraftStatus.Ready);
            card.Draft.ShouldBe(ReplyDraftFixture.Reply("tuesday", ReplyDraftTone.Friendly, ReplyDraftLength.Medium));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Your_Message_Wins_Over_The_Simulation_And_Needs_A_Generator()
    {
        var (card, window) = Open(c => c.Message = new ReplyDraftMessage { FromName = "Sam Lee", Subject = "Hello", Body = "Hi there." });
        try
        {
            AutomationProperties.GetName(card).ShouldBe("Email from Sam Lee");
            card.Chips.Count.ShouldBe(0);
            card.CanDraft.ShouldBeFalse();
            card.RequestCount.ShouldBe(0);
            card.DraftSwap!.Content.ShouldBeOfType<ReplyDraftEmptyView>().DraftButton!.IsEnabled.ShouldBeFalse();

            var gen = new Scripted();
            card.Generator = gen.Generate;
            Pump();
            card.CanDraft.ShouldBeTrue();
            card.RequestCount.ShouldBe(1);
            gen.Requests[0].Message.FromName.ShouldBe("Sam Lee");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Setting_Simulation_To_Null_Stops_Playback_And_Shows_The_Empty_Card()
    {
        var (card, window) = Open(c => c.AutoPlay = null);
        try
        {
            card.EffectiveAutoPlay.ShouldBeTrue();
            card.Status.ShouldBe(ReplyDraftStatus.Drafting);
            card.Simulation = null;
            Pump();
            card.Status.ShouldBe(ReplyDraftStatus.Idle);
            card.EffectiveAutoPlay.ShouldBeFalse();
            card.IsTourScheduled.ShouldBeFalse();
            card.Chips.Count.ShouldBe(0);
            card.Draft.ShouldBe(string.Empty);
            card.DraftSwap!.Content.ShouldBeOfType<ReplyDraftEmptyView>();
            // Nothing late lands after the stop.
            TestHelpers.PumpUntil(() => card.Status == ReplyDraftStatus.Ready, 1500).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }
}

/// <summary>Invented test data for the ReplyDraft tests (people, companies and numbers are made up).</summary>
file static class ReplyDraftFixture
{
    /// <summary>Who signs the replies.</summary>
    public const string Signature = "Sam";

    /// <summary>The email: a request to move a launch review.</summary>
    public static ReplyDraftMessage Message { get; } = new()
    {
        FromName = "Maya Chen",
        FromEmail = "maya.chen@harborline.example",
        Subject = "Can we move Thursday's launch review?",
        Body = "Hi Sam,\n\n"
            + "The pricing page copy came back from legal with a few changes, and I don't think we can hold the launch review on "
            + "Thursday at 2pm without them.\n\n"
            + "Could we push it to next Tuesday at 10am instead? That gives us time to fold in their notes and rerun the "
            + "screenshots. If Tuesday doesn't work, Wednesday afternoon is open on my side too.\n\n"
            + "Thanks,\nMaya",
        ReceivedAt = new DateTimeOffset(DateTime.Today.AddHours(9).AddMinutes(41)),
    };

    /// <summary>The quick replies.</summary>
    public static IReadOnlyList<ReplyDraftSuggestion> Suggestions { get; } =
    [
        new("tuesday", "Tuesday works"),
        new("wednesday", "Try Wednesday"),
        new("thursday", "Keep Thursday"),
    ];

    /// <summary>The model credited on the reply (a neutral mark; no brand mark is drawn).</summary>
    public static AgentModel Model { get; } = new("Mistral Medium 3.5");

    // Four sentences per quick reply and tone: Short takes the first, Medium the first two, Long all four in two paragraphs.
    private static readonly Dictionary<(string Id, ReplyDraftTone Tone), string[]> s_sentences = new()
    {
        [("tuesday", ReplyDraftTone.Friendly)] =
        [
            "Tuesday at 10am works great for me, so let's move the launch review there.",
            "Thanks for chasing legal on the pricing copy. Having their notes in first will make the review much smoother.",
            "I'll update the invite and ask Priya to rerun the screenshots on Monday afternoon.",
            "If anything else comes back from legal before then, just drop it in the doc and I'll take a look.",
        ],
        [("tuesday", ReplyDraftTone.Concise)] =
        [
            "Tuesday at 10am works. I'll move the review.",
            "Please share legal's notes once they're in.",
            "I'll update the invite today and get the screenshots rerun on Monday.",
            "Anything else from legal can go straight into the doc.",
        ],
        [("tuesday", ReplyDraftTone.Formal)] =
        [
            "Thank you for letting me know. Tuesday at 10am suits me, and I will reschedule the launch review accordingly.",
            "I appreciate you following up with legal on the pricing copy; reviewing their changes beforehand will make the session more productive.",
            "I will send an updated invitation today and arrange for the screenshots to be regenerated on Monday.",
            "Should legal raise any further points before then, please add them to the review document and I will read them ahead of the meeting.",
        ],
        [("wednesday", ReplyDraftTone.Friendly)] =
        [
            "Tuesday morning is tricky for me, but Wednesday afternoon would be perfect.",
            "How about 2pm? That still gives us a full day to fold in legal's notes.",
            "I'll hold the slot on my calendar and move the invite once you confirm.",
            "And thanks for flagging this early, it's much easier to shift now than on Thursday morning.",
        ],
        [("wednesday", ReplyDraftTone.Concise)] =
        [
            "Tuesday doesn't work for me. Wednesday afternoon does.",
            "Let's do 2pm.",
            "I'll move the invite once you confirm.",
            "That leaves a day for legal's notes and the screenshots.",
        ],
        [("wednesday", ReplyDraftTone.Formal)] =
        [
            "Thank you for the suggestion. Unfortunately, Tuesday morning is not possible for me; Wednesday afternoon would be preferable.",
            "Would 2pm on Wednesday be convenient? It would still allow time to incorporate legal's comments.",
            "I will reserve the slot and update the invitation as soon as you confirm.",
            "Thank you as well for raising this early, which makes the change considerably easier to arrange.",
        ],
        [("thursday", ReplyDraftTone.Friendly)] =
        [
            "I'd love to keep Thursday at 2pm if we can, since the launch date depends on it.",
            "Could we review everything except the pricing page, and pick up legal's changes in a quick follow-up?",
            "I'm happy to set up a 15-minute slot on Friday for just the pricing copy.",
            "Let me know if that feels too tight and we can figure something else out.",
        ],
        [("thursday", ReplyDraftTone.Concise)] =
        [
            "Let's keep Thursday at 2pm; the launch date depends on it.",
            "We can review everything but pricing and cover legal's changes separately.",
            "I'll add a 15-minute pricing check on Friday.",
            "Tell me if that's too tight.",
        ],
        [("thursday", ReplyDraftTone.Formal)] =
        [
            "If possible, I would prefer to keep the review on Thursday at 2pm, as the launch date depends on it.",
            "Perhaps we could review all materials except the pricing page and address legal's changes in a brief follow-up.",
            "I would be glad to schedule a 15-minute session on Friday dedicated to the pricing copy.",
            "Please let me know if this timeline seems too tight, and we can consider alternatives.",
        ],
    };

    /// <summary>A reply for every quick reply, tone and length (3 × 3 × 3).</summary>
    public static IReadOnlyList<ReplyDraftVersion> Replies { get; } =
    [
        .. from suggestion in new[] { "tuesday", "wednesday", "thursday" }
           from tone in new[] { ReplyDraftTone.Friendly, ReplyDraftTone.Concise, ReplyDraftTone.Formal }
           from length in new[] { ReplyDraftLength.Short, ReplyDraftLength.Medium, ReplyDraftLength.Long }
           select new ReplyDraftVersion(suggestion, tone, length, Reply(suggestion, tone, length)),
    ];

    /// <summary>A new simulation of the email (each card gets its own).</summary>
    public static ReplyDraftSimulation Simulation() => new()
    {
        Message = Message,
        Suggestions = Suggestions,
        Model = Model,
        Replies = Replies,
    };

    /// <summary>The reply for a quick reply, tone and length, greeting Maya and signed by Sam.</summary>
    public static string Reply(string suggestionId, ReplyDraftTone tone, ReplyDraftLength length)
    {
        var sentences = s_sentences[(suggestionId, tone)];
        var greeting = tone == ReplyDraftTone.Formal ? "Dear Maya," : "Hi Maya,";
        var closing = tone switch
        {
            ReplyDraftTone.Formal => "Kind regards,\n" + Signature,
            ReplyDraftTone.Concise => Signature,
            _ => "Thanks!\n" + Signature,
        };
        var body = length switch
        {
            ReplyDraftLength.Short => sentences[0],
            ReplyDraftLength.Long => sentences[0] + " " + sentences[1] + "\n\n" + sentences[2] + " " + sentences[3],
            _ => sentences[0] + " " + sentences[1],
        };
        return greeting + "\n\n" + body + "\n\n" + closing;
    }
}
