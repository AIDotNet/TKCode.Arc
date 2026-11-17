using System.Runtime.CompilerServices;
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
/// MeetingSummary: the simulation (an invented fixture) and its summarizing pass, the card with no data, streaming (chunk sizes, reserved meta), controlled states (error,
/// empty), the summarizer delegate, checking items off, sends (handled, simulated, failed, cancelled), quotes (hover
/// intent, instant switch, pin, Escape), the breakpoints, labels and the pure helpers.
/// </summary>
public class MeetingSummaryTests
{
    /// <summary>The invented call.</summary>
    private static MeetingSummarySimulation Demo() => new()
    {
        Meeting = MeetingSummaryFixture.Meeting,
        Model = MeetingSummaryFixture.Model,
        ReferenceFormats = MeetingSummaryFixture.ReferenceFormats,
        FirstReferenceNumber = MeetingSummaryFixture.FirstReferenceNumber,
    };

    private static string ModelName => MeetingSummaryFixture.Model.Name;

    /// <summary>A card playing the fixture call (pass <paramref name="simulate"/> false for a card with no data).</summary>
    private static (MeetingSummary Card, Window Window, MeetingSummaryTestTimers Timers) Open(ArcMotionLevel level = ArcMotionLevel.Off,
        Action<MeetingSummary>? setup = null, double width = 640, bool simulate = true)
    {
        var timers = new MeetingSummaryTestTimers();
        var card = new MeetingSummary { Timers = timers, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        if (simulate)
        {
            card.Simulation = Demo();
        }
        setup?.Invoke(card);
        var window = TestHelpers.Show(card, level);
        window.Width = width;
        window.Height = 1200;
        Pump();
        return (card, window, timers);
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

    private static MeetingSummaryData Notes(int points = 1, int decisions = 1, int items = 2) => new()
    {
        Title = "Roadmap review",
        Platform = "Video call",
        StartsAt = new DateTime(2026, 11, 3, 9, 30, 0),
        DurationMinutes = 75,
        TranscriptWords = 1200,
        Attendees = [new("ana", "Ana Silva", "Product"), new("ben", "Ben Ito")],
        Summary = [.. Enumerable.Range(1, points).Select(i => new MeetingSummaryPoint("s" + i, "Point number " + i + " about the roadmap.",
            new MeetingSummaryQuote("ana", TimeSpan.FromSeconds(60 * i + 5), "Quote " + i)))],
        Decisions = [.. Enumerable.Range(1, decisions).Select(i => new MeetingSummaryPoint("d" + i, "Decision " + i))],
        ActionItems = [.. Enumerable.Range(1, items).Select(i => new MeetingActionItem("a" + i, "Task " + i + " to do", i == 1 ? "ben" : null,
            new DateOnly(2026, 11, 10)) { Quote = new MeetingSummaryQuote("ben", TimeSpan.FromSeconds(30 * i), "I will do task " + i) })],
    };

    [AvaloniaFact]
    public void Plays_The_Simulation_Pass_When_Seen()
    {
        var (card, window, timers) = Open();
        try
        {
            card.UsesSimulation.ShouldBeTrue();
            card.EffectiveStatus.ShouldBe(MeetingSummaryStatus.Summarizing);
            card.ShownPanel.ShouldBe(card.LoadingPanel);
            card.TitleText!.Text.ShouldBe("Pricing page relaunch sync");
            card.MetaText!.Text.ShouldBe("Video call · Wed, Oct 28 · 2:00 PM · 34 min");
            card.CreditText!.Text.ShouldBe("Summarized by " + ModelName + " · 4,812 words");
            card.LastAnnouncement.ShouldBe("Summarizing the meeting.");
            timers.Requested.ShouldContain(MeetingSummary.PassDuration.TotalMilliseconds);

            timers.Advance(1000);
            Pump();
            card.EffectiveStatus.ShouldBe(MeetingSummaryStatus.Ready);
            card.ShownPanel.ShouldBe(card.NotesPanel);
            card.Rows.Count.ShouldBe(9);
            card.IsStreaming.ShouldBeFalse(); // Off: no stream
            card.Rows[0].ShownText.ShouldBe(card.EffectiveMeeting.Summary[0].Text);
            card.LastAnnouncement.ShouldBe("Summary ready: 3 points, 2 decisions, 4 action items.");
            card.CopyButtonPart!.Value!.ShouldContain("- [ ] Redesign the seats row");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void AutoPlay_False_Starts_On_The_Finished_Notes()
    {
        var (card, window, timers) = Open(setup: c => c.AutoPlay = false);
        try
        {
            card.EffectiveStatus.ShouldBe(MeetingSummaryStatus.Ready);
            card.ShownPanel.ShouldBe(card.NotesPanel);
            card.Rows.All(r => r.ShownText == r.Point.Text).ShouldBeTrue();
            timers.Pending.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Streams_The_Notes_In_Chunks_At_Full_Motion()
    {
        var (card, window, timers) = Open(ArcMotionLevel.Full);
        try
        {
            timers.Advance(1000);
            Pump();
            card.IsStreaming.ShouldBeTrue();
            card.Rows.All(r => r.IsStreaming).ShouldBeTrue();
            card.Rows[0].ShownText.ShouldBeEmpty();
            var chunk = card.Stream.ChunkWords;
            chunk.ShouldBeInRange(MeetingSummaryStream.MinWords, MeetingSummaryStream.MaxWords);

            timers.Advance(34);
            Pump();
            MeetingSummaryStream.CountWords(card.Rows[0].ShownText).ShouldBe(Math.Min(chunk, MeetingSummaryStream.CountWords(card.Rows[0].Point.Text)));
            card.Rows[^1].IsStreaming.ShouldBeTrue();
            var action = card.ActionRows[0];
            action.IsStreaming.ShouldBeTrue();
            action.Classes.Contains(":streaming").ShouldBeTrue();

            var ticks = 0;
            while (card.IsStreaming && ticks++ < 200)
            {
                timers.Advance(34);
            }
            Pump();
            card.IsStreaming.ShouldBeFalse();
            ticks.ShouldBeLessThan(60); // about 1.5 seconds
            card.Rows.All(r => !r.IsStreaming && r.ShownText == r.Point.Text).ShouldBeTrue();
            card.LastAnnouncement.ShouldBe("Summary ready: 3 points, 2 decisions, 4 action items.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Controlled_Status_Shows_Error_And_Empty()
    {
        var (card, window, timers) = Open(setup: c =>
        {
            c.Meeting = Notes();
            c.Status = MeetingSummaryStatus.Summarizing;
        });
        try
        {
            card.ShownPanel.ShouldBe(card.LoadingPanel);
            timers.Pending.ShouldBe(0); // controlled: no pass of its own
            card.RegenerateButton!.IsEnabled.ShouldBeFalse();

            card.ErrorText = "The transcript service timed out.";
            card.Status = MeetingSummaryStatus.Error;
            Pump();
            card.ShownPanel.ShouldBe(card.ErrorPanel);
            card.ErrorMessageText!.Text.ShouldBe("The transcript service timed out.");
            card.LastAnnouncement.ShouldBe("Couldn't summarize this meeting.");
            card.Classes.Contains(":error").ShouldBeTrue();

            card.Status = MeetingSummaryStatus.Ready;
            Pump();
            card.ShownPanel.ShouldBe(card.NotesPanel);
            card.Rows.Count.ShouldBe(4);

            card.Meeting = new MeetingSummaryData { Title = "Empty call" };
            Pump();
            card.ShownPanel.ShouldBe(card.EmptyPanel);
            card.Classes.Contains(":empty").ShouldBeTrue();
            card.CopyButtonPart!.IsEnabled.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Header_Reads_Platform_Date_And_Duration()
    {
        var (card, window, _) = Open(setup: c =>
        {
            c.Meeting = Notes();
            c.Status = MeetingSummaryStatus.Ready;
            c.Model = null;
        });
        try
        {
            card.MetaText!.Text.ShouldBe("Video call · Tue, Nov 3 · 9:30 AM · 1 h 15 min");
            card.CreditText!.Text.ShouldBe("1,200 words");
            card.AttendeeStack!.ItemsSource!.Cast<AvatarGroupMember>().Select(m => m.DisplayName).ShouldBe(["Ana Silva", "Ben Ito"]);
            AutomationProperties.GetName(card).ShouldBe("Roadmap review");
            var row = card.ActionRows[0];
            row.OwnerText!.Text.ShouldBe("Ben Ito");
            row.DueText!.Text.ShouldBe("Due Nov 10");
            card.ActionRows[1].OwnerText!.IsVisible.ShouldBeFalse();
            card.Rows[0].Stamp!.Content.ShouldBe("1:05");
            AutomationProperties.GetName(card.Rows[0].Stamp!).ShouldBe("Show quote at 1:05");
            card.Rows[1].Stamp!.IsVisible.ShouldBeFalse(); // the decision has no quote
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Checking_An_Item_With_Space_Raises_The_Event_And_Strikes_It()
    {
        var (card, window, _) = Open(setup: c =>
        {
            c.Meeting = Notes();
            c.Status = MeetingSummaryStatus.Ready;
        });
        try
        {
            var changes = new List<(string Id, bool Done)>();
            card.ActionItemChanged += (_, e) => changes.Add((e.Item.Id, e.IsDone));
            var row = card.ActionRows[0];
            row.Check!.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Space);
            changes.ShouldBe([("a1", true)]);
            row.IsDone.ShouldBeTrue();
            row.Check.IsChecked.ShouldBeTrue();
            row.TextView!.Strike.ShouldBe(1);
            row.TextView.Muted.ShouldBe(1);
            row.Check.Mark!.DrawProgress.ShouldBe(1);
            card.LastAnnouncement.ShouldBe("Done: Task 1 to do");
            card.CopyButtonPart!.Value!.ShouldContain("- [x] Task 1 to do (Ben Ito), Due Nov 10");
            AutomationProperties.GetName(row.Check).ShouldBe("Done: Task 1 to do");

            Press(window, PhysicalKey.Enter);
            changes[^1].ShouldBe(("a1", false));
            row.TextView.Strike.ShouldBe(0);
            card.LastAnnouncement.ShouldBe("Not done: Task 1 to do");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Items_Done_In_The_Data_Start_Checked()
    {
        var notes = Notes();
        notes.ActionItems[1].IsDone = true;
        var (card, window, _) = Open(setup: c =>
        {
            c.Meeting = notes;
            c.Status = MeetingSummaryStatus.Ready;
        });
        try
        {
            card.ActionRows[1].IsDone.ShouldBeTrue();
            card.ActionRows[1].TextView!.Strike.ShouldBe(1);
            card.ActionRows[0].IsDone.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Handled_Send_Shows_Spinner_Then_Sent_Chip()
    {
        var (card, window, _) = Open(setup: c =>
        {
            c.Meeting = Notes();
            c.Status = MeetingSummaryStatus.Ready;
        });
        try
        {
            var pending = new TaskCompletionSource<MeetingSummarySendResult?>();
            MeetingSummarySendEventArgs? request = null;
            MeetingSummarySentEventArgs? sent = null;
            card.SendRequested += (_, e) =>
            {
                request = e;
                e.Completion = pending.Task;
            };
            card.ActionItemSent += (_, e) => sent = e;
            var send = card.ActionRows[0].Sends[0];
            send.Target.ShouldBe(MeetingSummarySendTarget.Issues);
            AutomationProperties.GetName(send).ShouldBe("Send “Task 1 to do” to Issues");
            send.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Enter);
            request.ShouldNotBeNull();
            request!.Item.Id.ShouldBe("a1");
            request.Target.ShouldBe(MeetingSummarySendTarget.Issues);
            send.State.ShouldBe(MeetingSummarySendState.Sending);
            send.Classes.Contains(":sending").ShouldBeTrue();

            pending.SetResult(new MeetingSummarySendResult("PRJ-7", new Uri("https://example.com/PRJ-7")));
            Pump();
            send.State.ShouldBe(MeetingSummarySendState.Sent);
            send.ShownLabel.ShouldBe("PRJ-7");
            sent.ShouldNotBeNull();
            sent!.IsSimulated.ShouldBeFalse();
            sent.Result!.Reference.ShouldBe("PRJ-7");
            card.LastAnnouncement.ShouldBe("Sent to Issues as PRJ-7.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Failed_Send_Shows_Retry_And_Retries()
    {
        var (card, window, _) = Open(setup: c =>
        {
            c.Meeting = Notes();
            c.Status = MeetingSummaryStatus.Ready;
        });
        try
        {
            var attempts = 0;
            card.SendRequested += (_, e) =>
            {
                attempts++;
                e.Completion = attempts == 1
                    ? Task.FromException<MeetingSummarySendResult?>(new InvalidOperationException("401"))
                    : Task.FromResult<MeetingSummarySendResult?>(new MeetingSummarySendResult("Page 3"));
            };
            var send = card.ActionRows[1].Sends[1];
            send.Target.ShouldBe(MeetingSummarySendTarget.Docs);
            send.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Enter);
            send.State.ShouldBe(MeetingSummarySendState.Failed);
            send.ShownLabel.ShouldBe("Retry");
            card.LastAnnouncement.ShouldBe("Couldn't send to Docs.");

            Press(window, PhysicalKey.Enter);
            attempts.ShouldBe(2);
            send.State.ShouldBe(MeetingSummarySendState.Sent);
            send.ShownLabel.ShouldBe("Page 3");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Unhandled_Send_Is_Simulated_And_Says_So()
    {
        var (card, window, timers) = Open(setup: c =>
        {
            c.Meeting = Notes();
            c.Status = MeetingSummaryStatus.Ready;
        });
        try
        {
            MeetingSummarySentEventArgs? sent = null;
            card.ActionItemSent += (_, e) => sent = e;
            var send = card.ActionRows[0].Sends[0];
            send.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Enter);
            send.State.ShouldBe(MeetingSummarySendState.Sending);
            timers.Advance(MeetingSummary.SimulatedSend.TotalMilliseconds);
            Pump();
            send.State.ShouldBe(MeetingSummarySendState.Sent);
            // Your own notes: a simulated send has no reference to hand out.
            send.ShownLabel.ShouldBe("Sent");
            sent!.IsSimulated.ShouldBeTrue();
            sent.Result.ShouldBeNull();
            card.LastAnnouncement.ShouldBe("Sent to Issues. Simulated, nothing was sent.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Removing_The_Card_Cancels_A_Send_In_Flight()
    {
        var (card, window, _) = Open(setup: c =>
        {
            c.Meeting = Notes();
            c.Status = MeetingSummaryStatus.Ready;
        });
        CancellationToken token = default;
        var pending = new TaskCompletionSource<MeetingSummarySendResult?>();
        card.SendRequested += (_, e) =>
        {
            token = e.CancellationToken;
            e.CancellationToken.Register(() => pending.TrySetCanceled());
            e.Completion = pending.Task;
        };
        var send = card.ActionRows[0].Sends[0];
        send.Focus(NavigationMethod.Tab);
        Press(window, PhysicalKey.Enter);
        token.IsCancellationRequested.ShouldBeFalse();
        window.Content = null;
        Pump();
        token.IsCancellationRequested.ShouldBeTrue();
        send.State.ShouldBe(MeetingSummarySendState.Idle);
        window.Close();
    }

    [AvaloniaFact]
    public void Quotes_Open_After_Hover_Intent_And_Switch_At_Once()
    {
        var (card, window, timers) = Open(setup: c =>
        {
            c.Meeting = Notes(points: 2);
            c.Status = MeetingSummaryStatus.Ready;
        });
        try
        {
            var first = card.Rows[0];
            var second = card.Rows[1];
            card.HoverRow(first);
            card.QuotedRow.ShouldBeNull();
            timers.Requested.ShouldContain(MeetingSummary.HoverIntent.TotalMilliseconds);
            timers.Advance(MeetingSummary.HoverIntent.TotalMilliseconds);
            Pump();
            card.QuotedRow.ShouldBe(first);
            card.QuotePopup!.IsOpen.ShouldBeTrue();
            first.IsQuoted.ShouldBeTrue();
            first.Stamp!.IsExpanded.ShouldBeTrue();
            card.QuoteCard!.Speaker.ShouldBe("Ana Silva");
            card.QuoteCard.Role.ShouldBe("Product");
            card.QuoteCard.Time.ShouldBe("1:05");
            card.QuoteCard.Quote.ShouldBe("“Quote 1”");

            card.LeaveRow(first);
            card.HoverRow(second);
            card.QuotedRow.ShouldBe(second); // at once, no second intent
            first.IsQuoted.ShouldBeFalse();
            card.QuoteCard.Time.ShouldBe("2:05");

            card.LeaveRow(second);
            timers.Advance(MeetingSummary.CloseGrace.TotalMilliseconds);
            Pump();
            card.QuotedRow.ShouldBeNull();
            second.IsQuoted.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Timestamp_Pins_Its_Quote_And_Escape_Closes_It()
    {
        var (card, window, timers) = Open(setup: c =>
        {
            c.Meeting = Notes();
            c.Status = MeetingSummaryStatus.Ready;
        });
        try
        {
            var row = card.ActionRows[0];
            row.Stamp!.Focus(NavigationMethod.Tab);
            Pump();
            card.QuotedRow.ShouldBe(row); // keyboard focus shows it
            card.IsQuotePinned.ShouldBeFalse();
            Press(window, PhysicalKey.Enter);
            card.IsQuotePinned.ShouldBeTrue();
            card.LeaveRow(row);
            timers.Advance(500);
            card.QuotedRow.ShouldBe(row); // pinned quotes stay

            Press(window, PhysicalKey.Escape);
            card.QuotedRow.ShouldBeNull();
            card.QuotePopup!.IsOpen.ShouldBeFalse();
            card.IsQuotePinned.ShouldBeFalse();

            row.Stamp.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Enter);
            card.IsQuotePinned.ShouldBeTrue();
            Press(window, PhysicalKey.Enter);
            card.QuotedRow.ShouldBeNull(); // Enter again closes
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Cards_Move_Attendees_And_Shorten_Owners()
    {
        var (card, window, _) = Open(setup: c =>
        {
            c.Meeting = Notes();
            c.Status = MeetingSummaryStatus.Ready;
        }, width: 500);
        try
        {
            card.Classes.Contains(":compact").ShouldBeTrue();
            card.Classes.Contains(":narrow").ShouldBeFalse();
            card.ActionRows[0].OwnerText!.Text.ShouldBe("Ben");
            AutomationProperties.GetName(card.ActionRows[0].OwnerText!).ShouldBe("Ben Ito");
            Grid.GetRow(card.AttendeeStack!).ShouldBe(0);

            window.Width = 420;
            Pump();
            card.Classes.Contains(":narrow").ShouldBeTrue();
            Grid.GetRow(card.AttendeeStack!).ShouldBe(1);

            window.Width = 640;
            Pump();
            card.Classes.Contains(":compact").ShouldBeFalse();
            card.ActionRows[0].OwnerText!.Text.ShouldBe("Ben Ito");
            card.Bounds.Width.ShouldBe(640, 0.5); // fills its container up to 640
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Summarizer_Streams_Snapshots_And_Reports_Failure()
    {
        var gate = new TaskCompletionSource();
        async IAsyncEnumerable<MeetingSummaryData> Run([EnumeratorCancellation] CancellationToken token)
        {
            await gate.Task;
            yield return Notes(points: 1, decisions: 0, items: 0);
            yield return Notes(points: 2, decisions: 1, items: 1);
        }

        var (card, window, timers) = Open(setup: c => c.Summarizer = Run);
        try
        {
            card.EffectiveStatus.ShouldBe(MeetingSummaryStatus.Summarizing);
            timers.Pending.ShouldBe(0); // the summarizer, not a timed pass
            gate.SetResult();
            TestHelpers.PumpUntil(() => card.Rows.Count == 4).ShouldBeTrue();
            card.EffectiveStatus.ShouldBe(MeetingSummaryStatus.Ready);
            card.EffectiveMeeting.Title.ShouldBe("Roadmap review");
            card.LastAnnouncement.ShouldBe("Summary ready: 2 points, 1 decision, 1 action item.");

            static async IAsyncEnumerable<MeetingSummaryData> Fail([EnumeratorCancellation] CancellationToken token)
            {
                await Task.Yield();
                throw new InvalidOperationException("Transcript not found.");
#pragma warning disable CS0162
                yield break;
#pragma warning restore CS0162
            }
            var regenerated = 0;
            card.RegenerateRequested += (_, _) => regenerated++;
            card.Summarizer = Fail;
            card.Regenerate();
            regenerated.ShouldBe(1);
            TestHelpers.PumpUntil(() => card.EffectiveStatus == MeetingSummaryStatus.Error).ShouldBeTrue();
            card.ErrorMessageText!.Text.ShouldBe("Transcript not found.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Regenerate_Replays_The_Own_Pass()
    {
        var (card, window, timers) = Open();
        try
        {
            timers.Advance(1000);
            Pump();
            card.ActionRows[0].Check!.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Space);
            card.ActionRows[0].IsDone.ShouldBeTrue();
            card.RegenerateButton!.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Enter);
            card.EffectiveStatus.ShouldBe(MeetingSummaryStatus.Summarizing);
            timers.Advance(1000);
            Pump();
            card.EffectiveStatus.ShouldBe(MeetingSummaryStatus.Ready);
            card.ActionRows[0].IsDone.ShouldBeFalse(); // fresh notes
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Simulation_Ticks_One_Item_And_Sends_Another_Once()
    {
        var (card, window, timers) = Open();
        try
        {
            timers.Advance(1000);
            Pump();
            timers.Advance(MeetingSummary.SampleCheckDelay.TotalMilliseconds);
            Pump();
            card.ActionRows[1].IsDone.ShouldBeTrue();
            timers.Advance(MeetingSummary.SampleSendDelay.TotalMilliseconds);
            Pump();
            card.ActionRows[0].Sends[0].State.ShouldBe(MeetingSummarySendState.Sending);
            timers.Advance(MeetingSummary.SimulatedSend.TotalMilliseconds);
            Pump();
            card.ActionRows[0].Sends[0].State.ShouldBe(MeetingSummarySendState.Sent);
            card.ActionRows[0].Sends[0].ShownLabel.ShouldBe("ISS-142");
            card.LastAnnouncement.ShouldBe("Sent to Issues as ISS-142. Simulated, nothing was sent.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Simulation_Moves_Wait_For_Nobody_To_Use_The_Card()
    {
        var (card, window, timers) = Open();
        try
        {
            timers.Advance(1000);
            Pump();
            card.ActionRows[3].Check!.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Space);
            timers.Advance(5000);
            Pump();
            card.ActionRows[1].IsDone.ShouldBeFalse();
            card.ActionRows[0].Sends[0].State.ShouldBe(MeetingSummarySendState.Idle);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Labels_And_Targets_Are_Replaceable()
    {
        var (card, window, _) = Open(setup: c =>
        {
            c.Meeting = Notes();
            c.Model = MeetingSummaryFixture.Model;
            c.Status = MeetingSummaryStatus.Ready;
            c.SendTargets = [];
            c.Labels = new MeetingSummaryLabels
            {
                Summary = "Résumé",
                Words = n => n + " mots",
                SummarizedBy = m => "Par " + m,
                Due = d => "Pour le " + d.Day,
            };
        });
        try
        {
            card.CreditText!.Text.ShouldBe("Par " + ModelName + " · 1200 mots");
            card.ActionRows[0].DueText!.Text.ShouldBe("Pour le 10");
            card.ActionRows[0].Sends.Count.ShouldBe(0);
            card.GetVisualDescendantsNamed("PART_SummaryHeading").OfType<TextBlock>().Single().Text.ShouldBe("Résumé");
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void Stream_Chunks_Follow_The_Word_Count_And_Retarget()
    {
        var stream = new MeetingSummaryStream();
        stream.Start(["one two three four five", "six seven"]);
        stream.ChunkWords.ShouldBe(MeetingSummaryStream.MinWords);
        var first = stream.Tick();
        first.ShouldBe([(0, "one two three ")]);
        var second = stream.Tick();
        second.ShouldBe([(0, "four five"), (1, "six ")]);
        stream.IsLineDone(0).ShouldBeTrue();
        stream.IsLineDone(1).ShouldBeFalse();

        stream.Retarget(["one two three four five", "six eight nine", "ten"]);
        stream.ShownText(1).ShouldBe("six ");
        stream.Retarget(["one two", "six eight nine", "ten"]);
        stream.ShownText(0).ShouldBe("one two");
        stream.Tick().ShouldBe([(1, "eight nine"), (2, "ten")]);
        stream.IsDone.ShouldBeTrue();

        var big = new MeetingSummaryStream();
        big.Start([string.Join(' ', Enumerable.Repeat("word", 1000))]);
        big.ChunkWords.ShouldBe(MeetingSummaryStream.MaxWords);
        var mid = new MeetingSummaryStream();
        mid.Start([string.Join(' ', Enumerable.Repeat("word", 220))]);
        mid.ChunkWords.ShouldBe(5);
    }

    [Fact]
    public void Formats_Timestamps_Names_And_Notes()
    {
        MeetingSummaryFormat.Timestamp(TimeSpan.FromSeconds(134)).ShouldBe("2:14");
        MeetingSummaryFormat.Timestamp(TimeSpan.FromSeconds(1025)).ShouldBe("17:05");
        MeetingSummaryFormat.Timestamp(TimeSpan.FromSeconds(3727)).ShouldBe("1:02:07");
        MeetingSummaryFormat.FirstName("Maya Lindqvist").ShouldBe("Maya");
        MeetingSummaryFormat.FirstName("Cher").ShouldBe("Cher");
        var labels = MeetingSummaryLabels.Default;
        labels.Duration(34).ShouldBe("34 min");
        labels.Duration(65).ShouldBe("1 h 05 min");
        labels.AnnounceReady(1, 0, 2).ShouldBe("Summary ready: 1 point, 0 decisions, 2 action items.");
        var text = MeetingSummaryFormat.PlainText(Notes(points: 1, decisions: 0, items: 1), labels, _ => false);
        text.ShouldBe("Roadmap review\n\nSummary\n- Point number 1 about the roadmap.\n\nAction items\n- [ ] Task 1 to do (Ben Ito), Due Nov 10");
    }

    [AvaloniaFact]
    public void No_Data_Starts_On_The_No_Transcript_State()
    {
        var (card, window, timers) = Open(simulate: false);
        try
        {
            card.HasNotesSource.ShouldBeFalse();
            card.EffectiveStatus.ShouldBe(MeetingSummaryStatus.Ready);
            card.ShownPanel.ShouldBe(card.EmptyPanel);
            card.TitleText!.Text.ShouldBe(string.Empty);
            card.MetaText!.Text.ShouldBe(string.Empty);
            card.CreditText!.Text.ShouldBe(string.Empty);
            card.Rows.Count.ShouldBe(0);
            timers.Pending.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Simulation_Set_Later_Plays_Its_Pass()
    {
        var (card, window, timers) = Open(simulate: false);
        try
        {
            card.ShownPanel.ShouldBe(card.EmptyPanel);
            card.Simulation = Demo();
            Pump();
            card.EffectiveStatus.ShouldBe(MeetingSummaryStatus.Summarizing);
            card.TitleText!.Text.ShouldBe(MeetingSummaryFixture.Meeting.Title);
            timers.Advance(MeetingSummary.PassDuration.TotalMilliseconds);
            Pump();
            card.EffectiveStatus.ShouldBe(MeetingSummaryStatus.Ready);
            card.Rows.Count.ShouldBe(9);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Your_Meeting_Wins_Over_The_Simulation()
    {
        var (card, window, timers) = Open(setup: c => c.Meeting = Notes());
        try
        {
            card.UsesSimulation.ShouldBeFalse();
            card.TitleText!.Text.ShouldBe("Roadmap review");
            card.CreditText!.Text.ShouldBe("1,200 words");
            timers.Advance(1000);
            Pump();
            timers.Advance(5000);
            Pump();
            // No simulated moves on your notes.
            card.ActionRows[1].IsDone.ShouldBeFalse();
            card.ActionRows[0].Sends[0].State.ShouldBe(MeetingSummarySendState.Idle);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Setting_Simulation_To_Null_Stops_Playback_And_Shows_The_No_Transcript_State()
    {
        var (card, window, timers) = Open();
        try
        {
            card.EffectiveStatus.ShouldBe(MeetingSummaryStatus.Summarizing);
            card.Simulation = null;
            Pump();
            card.EffectiveStatus.ShouldBe(MeetingSummaryStatus.Ready);
            card.ShownPanel.ShouldBe(card.EmptyPanel);
            card.Rows.Count.ShouldBe(0);
            timers.Pending.ShouldBe(0);
            timers.Advance(5000);
            Pump();
            card.ShownPanel.ShouldBe(card.EmptyPanel);
        }
        finally
        {
            window.Close();
        }
    }
}

internal static class MeetingSummaryTestExtensions
{
    public static IEnumerable<Visual> GetVisualDescendantsNamed(this Visual root, string name) =>
        Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(root).Where(v => v is Control c && c.Name == name);
}

/// <summary>Timers for MeetingSummary tests: nothing fires until <see cref="Advance"/> moves the test clock past it.</summary>
internal sealed class MeetingSummaryTestTimers : IMeetingSummaryTimers
{
    private readonly List<Shot> _shots = new();

    public double Now { get; private set; }

    public List<double> Requested { get; } = new();

    public int Pending => _shots.Count(s => !s.Done);

    public IDisposable Start(TimeSpan delay, Action callback)
    {
        Requested.Add(delay.TotalMilliseconds);
        var shot = new Shot(Now + delay.TotalMilliseconds, callback);
        _shots.Add(shot);
        return shot;
    }

    public void Advance(double milliseconds)
    {
        var end = Now + milliseconds;
        while (true)
        {
            var next = _shots.Where(s => !s.Done && s.Due <= end + 1e-6).OrderBy(s => s.Due).FirstOrDefault();
            if (next is null)
            {
                break;
            }
            Now = Math.Max(Now, next.Due);
            next.Fire();
            Dispatcher.UIThread.RunJobs();
        }
        Now = end;
    }

    private sealed class Shot(double due, Action callback) : IDisposable
    {
        public double Due { get; } = due;

        public bool Done { get; private set; }

        public void Fire()
        {
            if (Done)
            {
                return;
            }
            Done = true;
            callback();
        }

        public void Dispose() => Done = true;
    }
}

/// <summary>Invented test data for the MeetingSummary tests (people, companies and numbers are made up).</summary>
file static class MeetingSummaryFixture
{
    private static TimeSpan At(int minutes, int seconds) => new(0, minutes, seconds);

    /// <summary>The notes of the call (the card never changes them).</summary>
    public static MeetingSummaryData Meeting { get; } = new()
    {
        Title = "Pricing page relaunch sync",
        Platform = "Video call",
        StartsAt = new DateTime(2026, 10, 28, 14, 0, 0),
        DurationMinutes = 34,
        TranscriptWords = 4812,
        Attendees =
        [
            new("maya", "Maya Lindqvist", "Design"),
            new("daniel", "Daniel Okafor", "Engineering"),
            new("priya", "Priya Raman", "Marketing"),
            new("tom", "Tom Becker", "Product"),
            new("lena", "Lena Moreau", "Support"),
        ],
        Summary =
        [
            new("s1", "The team reviewed the new pricing page draft and agreed the three-tier layout reads clearly on desktop.",
                new("maya", At(2, 14), "I think the three columns finally work. People see the middle plan first, which is what we want.")),
            new("s2", "Support sees most confusion around seat limits, so the comparison table needs a clearer seats row.",
                new("lena", At(9, 41), "Half of the tickets this month were about seats. Nobody can tell what happens when you add the sixth person.")),
            new("s3", "Engineering can ship the page behind a flag next week once the checkout copy is final.",
                new("daniel", At(17, 5), "The page itself is done. If copy lands by Monday I can put it behind the flag on Tuesday.")),
        ],
        Decisions =
        [
            new("d1", "Launch with annual billing selected by default, with the monthly price shown beside it.",
                new("tom", At(21, 30), "Let's default to annual and keep monthly right next to it, so nobody feels tricked.")),
            new("d2", "Drop the enterprise calculator from this release and link to sales instead.",
                new("priya", At(26, 52), "The calculator is a whole project. For now a Talk to sales link is enough.")),
        ],
        ActionItems =
        [
            new("a1", "Redesign the seats row in the comparison table", "maya", new DateOnly(2026, 10, 31),
                new("maya", At(11, 8), "I'll take the seats row. I'll have something to look at by Friday.")),
            new("a2", "Finalize the checkout copy and send it for review", "priya", new DateOnly(2026, 11, 2),
                new("priya", At(18, 20), "I'll write the checkout copy over the weekend and share it Monday morning.")),
            new("a3", "Put the new page behind the pricing_v2 flag", "daniel", new DateOnly(2026, 11, 4),
                new("daniel", At(19, 2), "Flag is pricing_v2. I'll wire it up as soon as the copy is in.")),
            new("a4", "Draft a help article on how seat limits work", "lena", new DateOnly(2026, 11, 6),
                new("lena", At(29, 47), "I can write the help article so we have something to link from the table.")),
        ],
    };

    /// <summary>The model credited in the footer (a neutral mark; no brand mark is drawn).</summary>
    public static AgentModel Model { get; } = new("Qwen3.8-Max", ArcIcons.Sparkles);

    /// <summary>The references simulated sends hand out: "ISS-142" in Issues, "Notes 143" in Docs.</summary>
    public static IReadOnlyDictionary<string, string> ReferenceFormats { get; } = new Dictionary<string, string>
    {
        [MeetingSummarySendTarget.Issues.Id] = "ISS-{0}",
        [MeetingSummarySendTarget.Docs.Id] = "Notes {0}",
    };

    public const int FirstReferenceNumber = 142;
}
