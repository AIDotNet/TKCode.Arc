using System.Collections.ObjectModel;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>
/// TicketRouter: the simulated inbox it opens on (the gallery's), the empty state, the flow (arrive, classify, labels, pulse, landing) driven by hand, round
/// robin assignees, the review lane and its confirm, an app classifier that resolves, throws or is cancelled by a pause,
/// All caught up and appended tickets, the breakpoints, the keyboard model, labels and the history cap.
/// </summary>
public class TicketRouterTests
{
    private static (TicketRouter Router, Window Window) Open(Action<TicketRouter>? setup = null, double width = 840,
        ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var router = new TicketRouter
        {
            Simulation = TicketRouterTestData.Simulation(), ManualClock = true, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
        };
        setup?.Invoke(router);
        var window = TestHelpers.Show(router, level);
        window.Width = width;
        window.Height = 900;
        Pump();
        return (router, window);
    }

    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private static void Pump()
    {
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Runs one whole ticket (arrive, classify, labels, pulse, landing) through the simulated flow.</summary>
    private static void RunTicket(TicketRouter router)
    {
        router.Step(TicketRouterMotion.Arrive);
        router.Step(TicketRouterMotion.Classify);
        router.Step(TicketRouterMotion.Labeled);
        router.Step(TicketRouterMotion.Pulse);
        router.Step(TicketRouterMotion.Landing);
        Pump();
    }

    private static TicketRouterLaneView Lane(TicketRouter router, string id) => router.LaneViews.First(v => v.Lane.Id == id);

    private static List<TicketMessage> Own(params TicketMessage[] messages) => [.. messages];

    private static TicketMessage Message(string id, string subject) =>
        new(id, TicketChannel.Email, "Pat Doe", "Example Co", subject, "Hello");

    [AvaloniaFact]
    public void Opens_On_The_Simulated_Inbox()
    {
        var (router, window) = Open();
        try
        {
            router.LaneViews.Select(v => v.Label).ShouldBe(["Billing", "Engineering", "Success", "Needs review"]);
            router.RoutedTickets.Count.ShouldBe(6);
            Lane(router, "billing").Count.ShouldBe(11);
            Lane(router, "engineering").Count.ShouldBe(16);
            Lane(router, "success").Count.ShouldBe(7);
            Lane(router, "review").Count.ShouldBe(1);
            Lane(router, "billing").Rows.Count().ShouldBe(2);
            AutomationProperties.GetName(Lane(router, "billing")).ShouldBe("Billing, 11 tickets");
            router.Phase.ShouldBe(TicketRouterPhase.Arriving);
            router.TicketCard!.Subject.ShouldBe("Charged twice for the March invoice");
            router.TicketCard.Channel.ShouldBe("Email");
            router.StatusText!.Text.ShouldBe("Routing");
            router.ModelText!.Text.ShouldBe(TicketRouterTestData.Model);
            router.Model.ShouldBeNull();
            AutomationProperties.GetName(router).ShouldBe("Ticket routing");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Ticket_Is_Classified_Labeled_Pulsed_And_Lands_With_The_Next_Assignee()
    {
        var (router, window) = Open();
        try
        {
            var routed = new List<RoutedTicket>();
            router.TicketRouted += (_, e) => routed.Add(e.Ticket);

            router.Step(TicketRouterMotion.Arrive);
            router.Phase.ShouldBe(TicketRouterPhase.Classifying);
            router.ClassifyingText!.IsVisible.ShouldBeTrue();
            router.ClassifyingText.IsShimmering.ShouldBeTrue();
            router.ClassifyingText.Text.ShouldBe("Classifying with " + TicketRouterTestData.Model);

            router.Step(TicketRouterMotion.Classify);
            router.Phase.ShouldBe(TicketRouterPhase.Labeled);
            router.IntentChip!.Text.ShouldBe("Billing");
            router.SentimentChip!.Text.ShouldBe("Negative");
            router.SentimentChip.Tone.ShouldBe(TicketRouterTone.Danger);
            router.ConfidenceMeter!.Text.ShouldBe("94%");
            router.ConfidenceMeter.IsLow.ShouldBeFalse();
            AutomationProperties.GetName(router.ConfidenceMeter).ShouldBe("94% confident");
            router.RouteChip!.Text.ShouldBe("Billing");
            router.IntentChip.Opacity.ShouldBe(1);

            router.Step(TicketRouterMotion.Labeled);
            router.Phase.ShouldBe(TicketRouterPhase.Routing);
            router.Connector!.PulseTarget.ShouldBe(0);

            router.Step(TicketRouterMotion.Pulse);
            Pump();
            router.Phase.ShouldBe(TicketRouterPhase.Landing);
            routed.Count.ShouldBe(1);
            routed[0].Message.Id.ShouldBe("t-101");
            routed[0].LaneId.ShouldBe("billing");
            routed[0].Assignee.ShouldBe("Andre Williams");
            routed[0].Classification!.Intent.ShouldBe(TicketIntent.Billing);
            router.RoutedTickets[0].ShouldBe(routed[0]);
            Lane(router, "billing").Count.ShouldBe(12);
            Lane(router, "billing").Rows.First().Ticket.ShouldBe(routed[0]);
            Lane(router, "billing").Rows.First().Meta.ShouldBe("Billing · Hannah Brooks");
            router.Announcement.ShouldBe(
                "Charged twice for the March invoice routed to Billing, assigned to Andre Williams. Simulated, nothing was sent.");

            router.Step(TicketRouterMotion.Landing);
            router.Phase.ShouldBe(TicketRouterPhase.Arriving);
            router.TicketCard!.Subject.ShouldBe("Export to CSV times out on large boards");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Lanes_Show_Only_Their_Visible_Rows_And_Assign_Round_Robin()
    {
        var (router, window) = Open(r => r.MaxVisible = 2);
        try
        {
            var assignees = new List<string?>();
            router.TicketRouted += (_, e) => assignees.Add(e.Ticket.Assignee);
            for (var i = 0; i < 10; i++)
            {
                RunTicket(router);
            }
            assignees.ShouldBe(["Andre Williams", "Mei Tanaka", "Sofia Alvarez", null, "Priya Nair", "Jonas Berg", "Liam Chen",
                null, "Andre Williams", "Ada Okafor"]);
            foreach (var view in router.LaneViews)
            {
                view.Rows.Count().ShouldBeLessThanOrEqualTo(2);
                view.Slots.Count().ShouldBeLessThanOrEqualTo(2);
            }
            router.RoutedTickets.Count.ShouldBe(16);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Low_Confidence_Waits_In_Review_And_A_Click_Routes_It_To_The_Suggestion()
    {
        var (router, window) = Open();
        try
        {
            for (var i = 0; i < 3; i++)
            {
                RunTicket(router);
            }
            router.Step(TicketRouterMotion.Arrive);
            router.Step(TicketRouterMotion.Classify);
            router.ConfidenceMeter!.Text.ShouldBe("54%");
            router.ConfidenceMeter.IsLow.ShouldBeTrue();
            router.RouteChip!.Text.ShouldBe("Needs review · Suggested Billing");
            router.RouteChip.Tone.ShouldBe(TicketRouterTone.Warning);
            router.TargetLane!.Id.ShouldBe("review");
            router.Step(TicketRouterMotion.Labeled);
            router.Connector!.PulseTarget.ShouldBe(3);
            router.Step(TicketRouterMotion.Pulse);
            Pump();

            var waiting = router.RoutedTickets[0];
            waiting.LaneId.ShouldBe("review");
            waiting.Assignee.ShouldBeNull();
            waiting.SuggestedLaneId.ShouldBe("billing");
            router.Announcement!.ShouldStartWith("Not sure this is the right place needs review, 54% confident, suggested Billing");
            var review = Lane(router, "review");
            review.Count.ShouldBe(2);
            var row = review.Rows.First();
            row.IsReview.ShouldBeTrue();
            row.Focusable.ShouldBeTrue();
            row.Meta.ShouldBe("54% → Billing");
            AutomationProperties.GetName(row).ShouldBe("Route Not sure this is the right place to Billing, 54% confident");

            TicketReviewConfirmedEventArgs? confirmed = null;
            router.ReviewConfirmed += (_, e) => confirmed = e;
            row.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();

            confirmed.ShouldNotBeNull();
            confirmed.LaneId.ShouldBe("billing");
            confirmed.Ticket.Message.ShouldBe(waiting.Message);
            confirmed.Ticket.Assignee.ShouldBe("Priya Nair");
            router.RoutedTickets.ShouldNotContain(waiting);
            review.Count.ShouldBe(1);
            Lane(router, "billing").Rows.First().Ticket.ShouldBe(confirmed.Ticket);
            router.Announcement!.ShouldStartWith("Confirmed, Not sure this is the right place routed to Billing, assigned to Priya Nair");
            router.ConfirmReview(waiting).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task An_App_Classifier_Decides_And_Its_Answer_Is_Not_Simulated()
    {
        var pending = new TaskCompletionSource<TicketClassification>();
        TicketMessage? asked = null;
        var (router, window) = Open(r =>
        {
            r.Tickets = Own(Message("a", "Card declined"), Message("b", "Second"));
            r.Lanes = TicketRouterTestData.Lanes;
            r.Classifier = (message, _) =>
            {
                asked = message;
                return pending.Task;
            };
            r.Route = (_, c) => c.Intent == TicketIntent.Billing ? "engineering" : "nowhere";
        });
        try
        {
            router.RoutedTickets.Count.ShouldBe(0);
            router.Step(TicketRouterMotion.Arrive);
            router.Phase.ShouldBe(TicketRouterPhase.Classifying);
            router.IsClassifying.ShouldBeTrue();
            asked!.Id.ShouldBe("a");
            router.Step(5);
            router.Phase.ShouldBe(TicketRouterPhase.Classifying);

            pending.SetResult(new TicketClassification(TicketIntent.Billing, TicketSentiment.Neutral, 0.8));
            await Task.Yield();
            Pump();
            router.Phase.ShouldBe(TicketRouterPhase.Labeled);
            router.TargetLane!.Id.ShouldBe("engineering");
            router.Step(TicketRouterMotion.Labeled);
            router.Step(TicketRouterMotion.Pulse);
            Pump();
            router.RoutedTickets[0].LaneId.ShouldBe("engineering");
            router.Announcement.ShouldBe("Card declined routed to Engineering, assigned to Mei Tanaka");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Unknown_Lane_Id_Falls_Back_To_The_First_Team_Lane()
    {
        var (router, window) = Open(r =>
        {
            r.Tickets = Own(Message("a", "Anything") );
            r.Tickets[0].Sample = new TicketClassification(TicketIntent.Question, TicketSentiment.Positive, 0.99);
            r.Route = (_, _) => "nowhere";
        });
        try
        {
            RunTicket(router);
            router.RoutedTickets[0].LaneId.ShouldBe("billing");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task A_Failing_Classifier_Offers_Retry_And_Send_To_Review()
    {
        var calls = 0;
        var (router, window) = Open(r =>
        {
            r.Tickets = Own(Message("a", "Broken thing"));
            r.Classifier = (_, _) =>
            {
                calls++;
                return calls == 1
                    ? Task.FromException<TicketClassification>(new InvalidOperationException("model down"))
                    : Task.FromException<TicketClassification>(new InvalidOperationException("still down"));
            };
        });
        try
        {
            TicketClassificationFailedEventArgs? failed = null;
            router.ClassificationFailed += (_, e) => failed = e;
            router.Step(TicketRouterMotion.Arrive);
            await Task.Yield();
            Pump();
            router.Phase.ShouldBe(TicketRouterPhase.Failed);
            failed.ShouldNotBeNull();
            failed.Error.Message.ShouldBe("model down");
            failed.Message.Id.ShouldBe("a");
            router.ErrorPanel!.IsVisible.ShouldBeTrue();
            router.RetryButton!.Content.ShouldBe("Retry");
            router.SendToReviewButton!.Content.ShouldBe("Send to review");
            router.Announcement.ShouldBe("Couldn't classify Broken thing");

            router.Retry();
            await Task.Yield();
            Pump();
            calls.ShouldBe(2);
            router.Phase.ShouldBe(TicketRouterPhase.Failed);

            router.SendToReview();
            router.Phase.ShouldBe(TicketRouterPhase.Routing);
            router.ErrorPanel.IsVisible.ShouldBeFalse();
            router.Step(TicketRouterMotion.Pulse);
            Pump();
            var ticket = router.RoutedTickets[0];
            ticket.LaneId.ShouldBe("review");
            ticket.Classification.ShouldBeNull();
            Lane(router, "review").Rows.First().Meta.ShouldBe("Unclassified → Billing");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pausing_Cancels_The_Classifier_And_Resuming_Asks_Again()
    {
        var tokens = new List<CancellationToken>();
        var (router, window) = Open(r =>
        {
            r.Tickets = Own(Message("a", "Slow"));
            r.Classifier = (_, token) =>
            {
                tokens.Add(token);
                return new TaskCompletionSource<TicketClassification>().Task;
            };
        });
        try
        {
            var pauses = new List<bool>();
            router.PausedChanged += (_, e) => pauses.Add(e.IsPaused);
            router.Step(TicketRouterMotion.Arrive);
            tokens.Count.ShouldBe(1);

            router.PauseButton!.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            router.IsPaused.ShouldBeTrue();
            pauses.ShouldBe([true]);
            tokens[0].IsCancellationRequested.ShouldBeTrue();
            router.StatusText!.Text.ShouldBe("Paused");
            AutomationProperties.GetName(router.PauseButton).ShouldBe("Resume");
            router.Announcement!.ShouldStartWith("Routing paused");

            router.IsPaused = false;
            pauses.ShouldBe([true, false]);
            tokens.Count.ShouldBe(2);
            tokens[1].IsCancellationRequested.ShouldBeFalse();
            router.Phase.ShouldBe(TicketRouterPhase.Classifying);

            router.SimulateOffScreen(true);
            tokens[1].IsCancellationRequested.ShouldBeTrue();
            router.SimulateOffScreen(false);
            tokens.Count.ShouldBe(3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Paused_Simulation_Does_Not_Move()
    {
        var (router, window) = Open(r => r.IsPaused = true);
        try
        {
            router.Step(10);
            router.Phase.ShouldBe(TicketRouterPhase.Arriving);
            router.StatusText!.Text.ShouldBe("Paused");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Own_Tickets_End_In_All_Caught_Up_And_Appending_Resumes()
    {
        var tickets = new ObservableCollection<TicketMessage> { Message("a", "One") };
        tickets[0].Sample = new TicketClassification(TicketIntent.Bug, TicketSentiment.Neutral, 0.9);
        var (router, window) = Open(r => r.Tickets = tickets);
        try
        {
            router.RoutedTickets.Count.ShouldBe(0);
            RunTicket(router);
            router.Phase.ShouldBe(TicketRouterPhase.CaughtUp);
            router.Notice!.IsVisible.ShouldBeTrue();
            router.Notice.Text.ShouldBe("All caught up");
            router.TicketCard!.IsVisible.ShouldBeFalse();
            router.StatusText!.Text.ShouldBe("All caught up");
            router.Classes.ShouldContain(":idle");

            var next = Message("b", "Two");
            next.Sample = new TicketClassification(TicketIntent.Feature, TicketSentiment.Positive, 0.9);
            tickets.Add(next);
            router.Phase.ShouldBe(TicketRouterPhase.Arriving);
            router.TicketCard.IsVisible.ShouldBeTrue();
            router.TicketCard.Subject.ShouldBe("Two");
            RunTicket(router);
            router.RoutedTickets[0].LaneId.ShouldBe("success");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void No_Tickets_Shows_The_Empty_State_And_Loop_Starts_Over()
    {
        var (router, window) = Open(r => r.Tickets = new List<TicketMessage>());
        try
        {
            router.Phase.ShouldBe(TicketRouterPhase.Empty);
            router.Notice!.Text.ShouldBe("No tickets yet");
            router.StatusText!.Text.ShouldBe("No tickets yet");

            var single = Message("a", "Only one");
            single.Sample = new TicketClassification(TicketIntent.Bug, TicketSentiment.Neutral, 0.9);
            router.Loop = true;
            router.Tickets = Own(single);
            RunTicket(router);
            router.Phase.ShouldBe(TicketRouterPhase.Arriving);
            router.CurrentMessage.ShouldBe(single);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Cards_Fold_The_Lanes_And_Stack_The_Labels()
    {
        var (router, window) = Open(width: 520);
        try
        {
            router.IsNarrow.ShouldBeTrue();
            router.IsCompact.ShouldBeFalse();
            router.LanePanel!.Columns.ShouldBe(2);
            router.Connector!.IsVisible.ShouldBeFalse();
            Grid.GetRow(router.RouteChip!.Parent as Control ?? router.RouteChip).ShouldBe(1);
            foreach (var view in router.LaneViews)
            {
                view.VisibleRows.ShouldBe(2);
            }
            var lanes = router.LaneViews;
            lanes[2].Bounds.Y.ShouldBeGreaterThan(lanes[0].Bounds.Bottom - 1);
            lanes[1].Bounds.X.ShouldBeGreaterThan(lanes[0].Bounds.Right - 1);

            window.Width = 380;
            Pump();
            router.IsCompact.ShouldBeTrue();
            router.ConfidenceMeter!.ShowsBar.ShouldBeFalse();
            router.ModelText!.IsVisible.ShouldBeFalse();
            router.Step(TicketRouterMotion.Arrive);
            router.ClassifyingText!.Text.ShouldBe("Classifying");

            window.Width = 840;
            Pump();
            router.IsNarrow.ShouldBeFalse();
            router.LanePanel.Columns.ShouldBe(4);
            router.Connector.IsVisible.ShouldBeTrue();
            router.LaneViews[0].VisibleRows.ShouldBe(3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Card_Height_Does_Not_Change_While_Tickets_Flow()
    {
        var (router, window) = Open();
        try
        {
            var height = router.Bounds.Height;
            height.ShouldBeGreaterThan(0);
            for (var i = 0; i < 6; i++)
            {
                RunTicket(router);
                router.Bounds.Height.ShouldBe(height, 0.5);
            }
            router.Bounds.Width.ShouldBe(840, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Team_Rows_Are_Not_Tab_Stops_And_Review_Rows_Are()
    {
        var (router, window) = Open();
        try
        {
            Lane(router, "billing").Rows.ShouldAllBe(r => !r.Focusable && !r.IsReview);
            Lane(router, "review").Rows.ShouldAllBe(r => r.Focusable && r.IsReview);
            router.PauseButton!.Content.ShouldNotBeNull();
            AutomationProperties.GetName(router.PauseButton).ShouldBe("Pause");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void With_No_Tickets_And_No_Simulation_It_Rests_On_The_Empty_State_In_The_Default_Lanes()
    {
        var (router, window) = Open(r => r.Simulation = null);
        try
        {
            router.Phase.ShouldBe(TicketRouterPhase.Empty);
            router.Notice!.IsVisible.ShouldBeTrue();
            router.LaneViews.Select(v => v.Lane.Id).ShouldBe(
                [TicketRouter.BillingLaneId, TicketRouter.EngineeringLaneId, TicketRouter.SuccessLaneId, TicketRouter.ReviewLaneId]);
            router.LaneViews.Select(v => v.Label).ShouldBe(["Billing", "Engineering", "Success", "Needs review"]);
            router.LaneViews.ShouldAllBe(v => v.Count == 0);
            router.RoutedTickets.Count.ShouldBe(0);
            router.ModelText!.IsVisible.ShouldBeFalse();
            router.Step(10);
            router.Phase.ShouldBe(TicketRouterPhase.Empty);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Apps_Tickets_Win_Over_The_Simulation()
    {
        var (router, window) = Open(r => r.Tickets = Own(Message("a", "Card declined")));
        try
        {
            router.ShowsSimulation.ShouldBeFalse();
            router.RoutedTickets.Count.ShouldBe(0);
            router.TicketCard!.Subject.ShouldBe("Card declined");
            router.LaneViews.Select(v => v.Lane.Assignees.Count).ShouldAllBe(n => n == 0);
            router.ModelText!.IsVisible.ShouldBeFalse();
            RunTicket(router);
            router.Phase.ShouldBe(TicketRouterPhase.CaughtUp);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Setting_The_Simulation_To_Null_Stops_The_Flow_On_The_Empty_State()
    {
        var (router, window) = Open();
        try
        {
            router.Step(TicketRouterMotion.Arrive);
            router.Phase.ShouldBe(TicketRouterPhase.Classifying);
            router.Simulation = null;
            Pump();
            router.Phase.ShouldBe(TicketRouterPhase.Empty);
            router.CurrentMessage.ShouldBeNull();
            router.RoutedTickets.Count.ShouldBe(0);
            router.LaneViews.Select(v => v.Lane.Assignees.Count).ShouldAllBe(n => n == 0);
            router.Step(10);
            router.Phase.ShouldBe(TicketRouterPhase.Empty);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Labels_Reword_Everything()
    {
        var (router, window) = Open(r =>
        {
            r.Title = "Tri des tickets";
            r.Labels = new TicketRouterLabels
            {
                Routing = "En cours",
                ClassifyingWith = model => "Classement avec " + model,
                Intent = _ => "Facturation",
                LaneName = (lane, count) => lane + " : " + count,
                AnnounceRouted = (subject, lane, _) => subject + " → " + lane,
                Simulated = "Simulation.",
            };
        });
        try
        {
            router.StatusText!.Text.ShouldBe("En cours");
            AutomationProperties.GetName(router).ShouldBe("Tri des tickets");
            AutomationProperties.GetName(Lane(router, "billing")).ShouldBe("Billing : 11");
            router.Step(TicketRouterMotion.Arrive);
            router.ClassifyingText!.Text.ShouldBe("Classement avec " + TicketRouterTestData.Model);
            router.Step(TicketRouterMotion.Classify);
            router.IntentChip!.Text.ShouldBe("Facturation");
            router.Step(TicketRouterMotion.Labeled);
            router.Step(TicketRouterMotion.Pulse);
            router.Announcement.ShouldBe("Charged twice for the March invoice → Billing. Simulation.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void History_Is_Capped_At_Eighty()
    {
        var (router, window) = Open(r => r.Speed = 4);
        try
        {
            for (var i = 0; i < 90; i++)
            {
                RunTicket(router);
            }
            router.RoutedTickets.Count.ShouldBe(TicketRouter.HistoryCap);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Flies_The_Row_From_The_Stage_And_Lights_It()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (router, window) = Open(level: ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(500), Frame);
            router.Step(TicketRouterMotion.Arrive);
            router.Step(TicketRouterMotion.Classify);
            router.Step(TicketRouterMotion.Labeled);
            router.Step(TicketRouterMotion.Pulse);
            router.Phase.ShouldBe(TicketRouterPhase.Landing);
            var row = Lane(router, "billing").Rows.First();
            row.HighlightOpacity.ShouldBeGreaterThan(0.5);
            router.IsFlying.ShouldBeTrue();
            row.RenderTransform.ShouldNotBeNull();
            clock.Advance(TimeSpan.FromMilliseconds(80), Frame);
            router.IsFlying.ShouldBeTrue();
            clock.Advance(TimeSpan.FromMilliseconds(1500), Frame);
            Pump();
            router.IsFlying.ShouldBeFalse();
            row.HighlightOpacity.ShouldBeLessThan(0.01);
            router.Step(TicketRouterMotion.Landing);
            router.Phase.ShouldBe(TicketRouterPhase.Arriving);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Keeps_The_Landing_Highlight_Without_Travel()
    {
        var (router, window) = Open(level: ArcMotionLevel.Reduced);
        try
        {
            router.Step(TicketRouterMotion.Arrive);
            router.Step(TicketRouterMotion.Classify);
            router.Step(TicketRouterMotion.Labeled);
            router.Step(TicketRouterMotion.Pulse);
            router.IsFlying.ShouldBeFalse();
            router.Connector!.PulseProgress.ShouldBe(0);
            Lane(router, "billing").Rows.First().HighlightOpacity.ShouldBeGreaterThan(0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Default_Route_Sends_Billing_And_Refunds_To_Billing_Bugs_To_Engineering_And_The_Rest_To_Success()
    {
        var message = Message("a", "x");
        TicketRouter.DefaultRoute(message, new TicketClassification(TicketIntent.Billing, TicketSentiment.Neutral, 1)).ShouldBe("billing");
        TicketRouter.DefaultRoute(message, new TicketClassification(TicketIntent.Refund, TicketSentiment.Neutral, 1)).ShouldBe("billing");
        TicketRouter.DefaultRoute(message, new TicketClassification(TicketIntent.Bug, TicketSentiment.Neutral, 1)).ShouldBe("engineering");
        TicketRouter.DefaultRoute(message, new TicketClassification(TicketIntent.Feature, TicketSentiment.Neutral, 1)).ShouldBe("success");
        TicketRouter.DefaultRoute(message, new TicketClassification(TicketIntent.Question, TicketSentiment.Neutral, 1)).ShouldBe("success");
    }

    [AvaloniaFact]
    public void Default_Labels_Format_Percentages_And_Counts()
    {
        var labels = TicketRouterLabels.Default;
        labels.Percent(0.875).ShouldBe("88%");
        labels.Percent(2).ShouldBe("100%");
        labels.LaneName("Billing", 1).ShouldBe("Billing, 1 ticket");
        labels.Channel(TicketChannel.HelpDesk).ShouldBe("Help desk");
        labels.ClassifyingWith("").ShouldBe("Classifying");
    }
}

/// <summary>
/// The invented fixture these tests play: a copy of the gallery's TicketRouter demo data with invented model names, so
/// the tests do not depend on the gallery's content.
/// </summary>
internal static class TicketRouterTestData
{
    /// <summary>The classifier named in the header: a real model.</summary>
    public static string Model => "Model A";

    /// <summary>The model named by the demo with its own classifier.</summary>
    public static string YourModel => "Model B";

    /// <summary>The lanes: Billing, Engineering, Success and Needs review, with invented assignees and earlier counts.</summary>
    public static IList<TicketLane> Lanes { get; } =
    [
        new TicketLane(TicketRouter.BillingLaneId, "Billing", "Andre Williams", "Priya Nair") { Count = 9 },
        new TicketLane(TicketRouter.EngineeringLaneId, "Engineering", "Mei Tanaka", "Jonas Berg", "Ada Okafor") { Count = 14 },
        new TicketLane(TicketRouter.SuccessLaneId, "Success", "Sofia Alvarez", "Liam Chen") { Count = 6 },
        new TicketLane(TicketRouter.ReviewLaneId, "Needs review") { IsReview = true },
    ];

    /// <summary>The stream, in arrival order, each with the simulated classifier's answer.</summary>
    public static IList<TicketMessage> Tickets { get; } =
    [
        Message("t-101", TicketChannel.Email, "Hannah Brooks", "Lumen Bakery", "Charged twice for the March invoice",
            "Hi, our card shows two charges of $49 on the 3rd and we only have one workspace.", "now",
            TicketIntent.Billing, TicketSentiment.Negative, 0.94),
        Message("t-102", TicketChannel.Chat, "Kofi Mensah", "Harbor Labs", "Export to CSV times out on large boards",
            "Every export over about 5,000 rows spins and then fails with a timeout.", "1m",
            TicketIntent.Bug, TicketSentiment.Negative, 0.91),
        Message("t-103", TicketChannel.Messages, "Elena Petrova", "Northgate Studio", "Could we get dark mode in the editor?",
            "The team works late and the bright canvas is rough. Any plans?", "2m",
            TicketIntent.Feature, TicketSentiment.Positive, 0.88),
        Message("t-104", TicketChannel.HelpDesk, "Marcus Reed", "Pinewood Dental", "Not sure this is the right place",
            "We switched plans last week and some things look different now, can someone check?", "3m",
            TicketIntent.Billing, TicketSentiment.Neutral, 0.54),
        Message("t-105", TicketChannel.Email, "Yuki Sato", "Orbit Freight", "Refund for the unused annual seats",
            "We removed eight seats in January. Can the unused months be refunded?", "4m",
            TicketIntent.Refund, TicketSentiment.Neutral, 0.9),
        Message("t-106", TicketChannel.Chat, "Grace Liu", "Fernway Health", "SSO login loops back to the sign-in page",
            "After the identity provider redirect we land on sign-in again, every browser.", "5m",
            TicketIntent.Bug, TicketSentiment.Negative, 0.96),
        Message("t-107", TicketChannel.Messages, "Omar Haddad", "Quill & Co", "How do I add a guest to one project?",
            "A freelancer needs access to a single project only, not the whole workspace.", "6m",
            TicketIntent.Question, TicketSentiment.Neutral, 0.83),
        Message("t-108", TicketChannel.HelpDesk, "Nora Lindqvist", "Brightside Schools", "Love the new reports, one idea",
            "The weekly report is great. Being able to schedule it per team would be even better.", "7m",
            TicketIntent.Feature, TicketSentiment.Positive, 0.62),
        Message("t-109", TicketChannel.Email, "Diego Ramos", "Cobalt Coffee", "Invoice needs our VAT number",
            "Our accountant needs the VAT number on the invoices from now on.", "8m",
            TicketIntent.Billing, TicketSentiment.Neutral, 0.89),
        Message("t-110", TicketChannel.Chat, "Amara Nwosu", "Tidewater Co-op", "Webhooks stopped firing overnight",
            "No events since about 2am. Nothing changed on our side as far as we know.", "9m",
            TicketIntent.Bug, TicketSentiment.Negative, 0.93),
    ];

    /// <summary>The tickets already in the lanes when the inbox opens, newest first.</summary>
    public static IList<RoutedTicket> Routed { get; } = CreateRouted();

    private static RoutedTicket[] CreateRouted()
    {
        var earlier = new[]
        {
            Message("t-091", TicketChannel.Email, "Ravi Kapoor", "Summit Outdoors", "Upgrade to the team plan",
                "We'd like to move from monthly to the yearly team plan.", "12m", TicketIntent.Billing, TicketSentiment.Positive, 0.92),
            Message("t-092", TicketChannel.Chat, "Lea Fischer", "Atelier Nord", "Images fail to upload from iPad",
                "Uploads stop at 90% from the tablet app.", "14m", TicketIntent.Bug, TicketSentiment.Negative, 0.9),
            Message("t-093", TicketChannel.Messages, "Sam Carter", "Maple Analytics", "Keyboard shortcuts for comments",
                "Would love a shortcut to jump to the next comment.", "15m", TicketIntent.Feature, TicketSentiment.Positive, 0.86),
            Message("t-094", TicketChannel.HelpDesk, "Ines Duarte", "Velo Repair", "Question about our account",
                "Something changed with our account, not sure what.", "17m", TicketIntent.Question, TicketSentiment.Neutral, 0.48),
            Message("t-095", TicketChannel.Email, "Tom Becker", "Lakeside Clinic", "Receipt for last payment",
                "Could you send the receipt for the last payment again?", "19m", TicketIntent.Billing, TicketSentiment.Neutral, 0.91),
            Message("t-096", TicketChannel.Chat, "Zara Ahmed", "Kite Studio", "Search misses new documents",
                "Documents created today do not show up in search.", "21m", TicketIntent.Bug, TicketSentiment.Negative, 0.88),
        };
        return
        [
            new RoutedTicket(earlier[0], earlier[0].Sample, TicketRouter.BillingLaneId, "Priya Nair"),
            new RoutedTicket(earlier[1], earlier[1].Sample, TicketRouter.EngineeringLaneId, "Jonas Berg"),
            new RoutedTicket(earlier[2], earlier[2].Sample, TicketRouter.SuccessLaneId, "Liam Chen"),
            new RoutedTicket(earlier[3], earlier[3].Sample, TicketRouter.ReviewLaneId, null, TicketRouter.SuccessLaneId),
            new RoutedTicket(earlier[4], earlier[4].Sample, TicketRouter.BillingLaneId, "Andre Williams"),
            new RoutedTicket(earlier[5], earlier[5].Sample, TicketRouter.EngineeringLaneId, "Mei Tanaka"),
        ];
    }

    private static TicketMessage Message(string id, TicketChannel channel, string customer, string company, string subject,
        string preview, string receivedAt, TicketIntent intent, TicketSentiment sentiment, double confidence) =>
        new(id, channel, customer, company, subject, preview)
        {
            ReceivedAt = receivedAt,
            Sample = new TicketClassification(intent, sentiment, confidence),
        };

    /// <summary>A new simulation of the inbox (tests and code use it; the XAML demos spell it out).</summary>
    public static TicketRouterSimulation Simulation() => new()
    {
        Model = Model,
        Lanes = Lanes,
        Tickets = Tickets,
        Routed = Routed,
    };
}
