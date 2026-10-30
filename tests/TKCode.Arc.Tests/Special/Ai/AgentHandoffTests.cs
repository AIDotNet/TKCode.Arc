using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>The agent handoff: the thread plan, the simulation, the stages, the card, the composer, keyboard and motion.</summary>
public class AgentHandoffTests
{
    private static AgentHandoffMessage Customer(string text) => new(AgentHandoffSender.Customer, text);

    private static AgentHandoffMessage Assistant(string text) => new(AgentHandoffSender.Assistant, text);

    private static AgentHandoffMessage Person(string text) => new(AgentHandoffSender.Agent, text);

    private static AgentHandoff Wired(AgentHandoffStage stage)
    {
        var handoff = new AgentHandoff
        {
            Stage = stage,
            Title = "Order NW-48213",
            Subtitle = "Northwind Home support",
            ReplyEstimate = "2 min",
            Summary = new AgentHandoffSummary("Desk arrived with a cracked leg.", new AgentHandoffField("Order", "NW-48213")),
            Agent = new AgentHandoffAgent("Jordan Reyes", "Support specialist", "14:05"),
            AllowsRequestHuman = true,
            AllowsCancelTransfer = true,
            AllowsRetry = true,
            ShowsComposer = true,
        };
        handoff.Messages.Add(Customer("My desk arrived broken."));
        handoff.Messages.Add(Assistant("Sorry about that. I can ship a new leg."));
        return handoff;
    }

    private static string Kinds(IEnumerable<object> plan) => string.Join(",", plan.Select(k => k switch
    {
        AgentHandoffMessage m => m.From.ToString(),
        _ when ReferenceEquals(k, AgentHandoff.RowKeys.AssistantStep) => "Step",
        _ when ReferenceEquals(k, AgentHandoff.RowKeys.AssistantTyping) => "Thinking",
        _ when ReferenceEquals(k, AgentHandoff.RowKeys.Suggestion) => "Talk",
        _ when ReferenceEquals(k, AgentHandoff.RowKeys.Card) => "Card",
        _ when ReferenceEquals(k, AgentHandoff.RowKeys.JoinStep) => "Join",
        _ when ReferenceEquals(k, AgentHandoff.RowKeys.AgentTyping) => "Dots",
        _ when ReferenceEquals(k, AgentHandoff.RowKeys.LateAssistantTyping) => "Thinking",
        _ when ReferenceEquals(k, AgentHandoff.RowKeys.ResolvedStep) => "Resolved",
        _ => "?",
    }));

    // ---- The plan of the thread (pure) ----

    [Fact]
    public void Plan_Offers_Talk_To_A_Person_Under_The_Last_Finished_Answer()
    {
        var plan = new List<object>();
        AgentHandoff.Plan(plan, AgentHandoffStage.Assistant, [Customer("a"), Assistant("b")], [], AgentHandoffTyping.None, true);
        Kinds(plan).ShouldBe("Step,Customer,Assistant,Talk");

        AgentHandoff.Plan(plan, AgentHandoffStage.Assistant, [Customer("a"), Assistant("b")], [], AgentHandoffTyping.None, false);
        Kinds(plan).ShouldBe("Step,Customer,Assistant");

        AgentHandoff.Plan(plan, AgentHandoffStage.Assistant, [Customer("a"), new AgentHandoffMessage(AgentHandoffSender.Assistant, "b") { IsStreaming = true }],
            [], AgentHandoffTyping.None, true);
        Kinds(plan).ShouldBe("Step,Customer,Assistant");

        AgentHandoff.Plan(plan, AgentHandoffStage.Assistant, [Customer("a")], [], AgentHandoffTyping.Assistant, true);
        Kinds(plan).ShouldBe("Step,Customer,Thinking");
    }

    [Fact]
    public void Plan_Puts_The_Card_After_The_Assistant_And_The_Join_Step_Before_The_Persons_First_Message()
    {
        var plan = new List<object>();
        AgentHandoff.Plan(plan, AgentHandoffStage.Transferring, [Customer("a"), Assistant("b")], [Customer("note")],
            AgentHandoffTyping.None, true);
        Kinds(plan).ShouldBe("Step,Customer,Assistant,Card,Customer");

        AgentHandoff.Plan(plan, AgentHandoffStage.Connected, [Customer("a")], [Customer("note"), Person("hi"), Customer("thanks")],
            AgentHandoffTyping.Agent, true);
        Kinds(plan).ShouldBe("Step,Customer,Card,Customer,Join,Agent,Customer,Dots");

        AgentHandoff.Plan(plan, AgentHandoffStage.Connected, [], [], AgentHandoffTyping.None, true);
        Kinds(plan).ShouldBe("Step,Card,Join");

        AgentHandoff.Plan(plan, AgentHandoffStage.Resolved, [], [Person("done")], AgentHandoffTyping.None, true);
        Kinds(plan).ShouldBe("Step,Card,Join,Agent,Resolved");

        AgentHandoff.Plan(plan, AgentHandoffStage.Unavailable, [Assistant("b")], [Customer("hello?")], AgentHandoffTyping.Assistant, true);
        Kinds(plan).ShouldBe("Step,Assistant,Card,Customer,Thinking");
    }

    [Fact]
    public void Default_Labels_Name_The_Person()
    {
        var labels = AgentHandoffLabels.Default;
        labels.With("Jordan").ShouldBe("With Jordan");
        labels.IsTyping("Jordan").ShouldBe("Jordan is typing");
        labels.PlaceholderAgent("Jordan").ShouldBe("Message Jordan");
        labels.TypicalReply("2 min").ShouldBe("Typical reply 2 min");
        labels.Joined("Jordan Reyes").ShouldBe("Jordan Reyes joined");
        new AgentHandoffAgent("Jordan Reyes").FirstName.ShouldBe("Jordan");
    }

    // ---- The simulation (pure, advanced by hand) ----

    /// <summary>An invented support chat: a lamp that arrived broken, handed to a person who refunds it.</summary>
    private static readonly AgentHandoffScript Script = new()
    {
        CustomerOpening = "Hi, the desk lamp I ordered arrived today and the shade is cracked.",
        AssistantAnswer = "Sorry about that. I found order 1042, the brass desk lamp delivered this morning, and I can send a new shade "
            + "that usually arrives within three working days.",
        CustomerRequest = "I would rather get my money back. Can I talk to a person?",
        AssistantHandoff = "Of course. I will pass this to our support team with a summary, so you will not have to explain it again.",
        Summary = new AgentHandoffSummary("Lamp arrived with a cracked shade; wants a refund.", new AgentHandoffField("Order", "1042")),
        CustomerNote = "I have a photo of the shade if it helps.",
        Agent = new AgentHandoffAgent("Sam Lee", "Support specialist", "10:05"),
        AgentGreeting = "Hi, I am Sam. I have read the summary.",
        AgentResolution = "I have refunded the lamp in full; it shows on your card in a few days.",
        ResolutionNotice = "Refund sent.",
        CustomerThanks = "Thank you!",
        AgentClosing = "Glad to help. I will close this chat now.",
        AssistantCancelled = "No problem, I have cancelled the transfer.",
        AssistantReply = "Thanks, I can bring in a person whenever you like.",
        AgentReply = "Thanks, I have added that to your case.",
    };

    private static AgentHandoffSimulation Simulation(AgentHandoffScenario scenario, bool reduced) => new(scenario, reduced, Script);

    private static void Run(AgentHandoffSimulation simulation, double seconds, Func<bool>? until = null)
    {
        for (var t = 0.0; t < seconds; t += 1.0 / 60)
        {
            simulation.Advance(1.0 / 60);
            if (until?.Invoke() == true)
            {
                return;
            }
        }
    }

    [Fact]
    public void Simulation_Streams_Assistant_Replies_In_Chunks_Of_Three_To_Eight_Words()
    {
        var simulation = Simulation(AgentHandoffScenario.Handoff, reduced: false);
        simulation.Start();
        Run(simulation, 10, () => simulation.Messages.Any(m => m.From == AgentHandoffSender.Assistant));
        var reply = simulation.Messages.Last(m => m.From == AgentHandoffSender.Assistant);
        reply.IsStreaming.ShouldBeTrue();
        var counts = new List<int> { reply.Text!.Split(' ').Length };
        while (reply.IsStreaming)
        {
            var before = reply.Text!.Split(' ').Length;
            simulation.Advance(AgentHandoffMotion.ChunkIntervalSeconds);
            var added = reply.Text!.Split(' ').Length - before;
            if (reply.IsStreaming)
            {
                counts.Add(added);
            }
        }
        counts.ShouldAllBe(n => n >= AgentHandoffMotion.ChunkMinWords && n <= AgentHandoffMotion.ChunkMaxWords);
        reply.Text.ShouldBe(Script.AssistantAnswer);
    }

    [Fact]
    public void Simulation_Hands_Off_Then_A_Person_Joins_And_Resolves()
    {
        var simulation = Simulation(AgentHandoffScenario.Handoff, reduced: false);
        simulation.Start();
        Run(simulation, 30, () => simulation.Stage == AgentHandoffStage.Transferring);
        simulation.Stage.ShouldBe(AgentHandoffStage.Transferring);
        simulation.Summary.ShouldNotBeNull();
        simulation.Summary!.Fields.Count.ShouldBeGreaterThan(0);

        Run(simulation, 30, () => simulation.Stage == AgentHandoffStage.Connected);
        simulation.Agent!.Name.ShouldBe("Sam Lee");
        simulation.AgentMessages.ShouldContain(m => m.Text == Script.CustomerNote);

        Run(simulation, 30, () => simulation.Typing == AgentHandoffTyping.Agent);
        simulation.Typing.ShouldBe(AgentHandoffTyping.Agent);

        Run(simulation, 60, () => simulation.Stage == AgentHandoffStage.Resolved);
        simulation.Stage.ShouldBe(AgentHandoffStage.Resolved);
        simulation.Notice.ShouldBe(Script.ResolutionNotice);

        // Full motion: the story starts over.
        Run(simulation, 10, () => simulation.Stage == AgentHandoffStage.Assistant);
        simulation.Stage.ShouldBe(AgentHandoffStage.Assistant);
        simulation.AgentMessages.ShouldBeEmpty();
    }

    [Fact]
    public void Reduced_Simulation_Shows_Replies_Whole_And_Plays_Once()
    {
        var simulation = Simulation(AgentHandoffScenario.Handoff, reduced: true);
        simulation.Start();
        Run(simulation, 10, () => simulation.Messages.Any(m => m.From == AgentHandoffSender.Assistant));
        var reply = simulation.Messages.Last(m => m.From == AgentHandoffSender.Assistant);
        reply.IsStreaming.ShouldBeFalse();
        reply.Text.ShouldBe(Script.AssistantAnswer);

        Run(simulation, 120, () => simulation.Stage == AgentHandoffStage.Resolved);
        Run(simulation, 20);
        simulation.Stage.ShouldBe(AgentHandoffStage.Resolved);
        simulation.IsWaiting.ShouldBeTrue();
    }

    [Fact]
    public void Unavailable_Simulation_Waits_For_Try_Again()
    {
        var simulation = Simulation(AgentHandoffScenario.Unavailable, reduced: true);
        simulation.Start();
        Run(simulation, 60, () => simulation.Stage == AgentHandoffStage.Unavailable);
        simulation.Stage.ShouldBe(AgentHandoffStage.Unavailable);
        Run(simulation, 20);
        simulation.Stage.ShouldBe(AgentHandoffStage.Unavailable);
        simulation.IsWaiting.ShouldBeTrue();

        simulation.Retry();
        simulation.Stage.ShouldBe(AgentHandoffStage.Transferring);
        Run(simulation, 10, () => simulation.Stage == AgentHandoffStage.Connected);
        simulation.Stage.ShouldBe(AgentHandoffStage.Connected);
    }

    [Fact]
    public void Simulation_Takes_Requests_Cancels_And_Sends()
    {
        var simulation = Simulation(AgentHandoffScenario.Handoff, reduced: true);
        simulation.Start();
        Run(simulation, 10, () => simulation.Messages.Any(m => m.From == AgentHandoffSender.Assistant));

        simulation.RequestHuman("Talk to a person");
        simulation.Messages[^1].Text.ShouldBe("Talk to a person");
        Run(simulation, 10, () => simulation.Stage == AgentHandoffStage.Transferring);
        simulation.Stage.ShouldBe(AgentHandoffStage.Transferring);

        simulation.Send("  photos attached  ");
        simulation.AgentMessages[^1].Text.ShouldBe("photos attached");

        simulation.CancelTransfer();
        simulation.Stage.ShouldBe(AgentHandoffStage.Assistant);
        simulation.Summary.ShouldBeNull();
        simulation.AgentMessages.ShouldBeEmpty();
        Run(simulation, 5, () => simulation.Messages[^1].Text == Script.AssistantCancelled);
        simulation.Messages[^1].Text.ShouldBe(Script.AssistantCancelled);
        Run(simulation, 10);
        simulation.Stage.ShouldBe(AgentHandoffStage.Assistant);

        simulation.Send("   ");
        simulation.Messages[^1].Text.ShouldBe(Script.AssistantCancelled);
        simulation.Send("Where is my order?");
        simulation.Messages[^1].From.ShouldBe(AgentHandoffSender.Customer);
        Run(simulation, 5, () => simulation.Messages[^1].From == AgentHandoffSender.Assistant);
        simulation.Messages[^1].Text.ShouldBe(Script.AssistantReply);
    }

    // ---- The control ----

    [AvaloniaFact]
    public void Card_Without_A_Stage_Or_A_Simulation_Is_An_Empty_Chat()
    {
        var handoff = new AgentHandoff();
        var window = TestHelpers.Show(handoff);
        try
        {
            handoff.IsSimulated.ShouldBeFalse();
            handoff.PlayingSimulation.ShouldBeNull();
            handoff.DisplayedStage.ShouldBe(AgentHandoffStage.Assistant);
            handoff.DisplayedMessages.ShouldBeEmpty();
            handoff.DisplayedAgentMessages.ShouldBeEmpty();
            handoff.DisplayedStatus.Text.ShouldBe(AgentHandoffLabels.Default.AssistantStatus);
            handoff.LiveRegion!.Announcement.ShouldNotBe(AgentHandoffLabels.Default.SimulatedNotice);
            TestHelpers.Descendant<Control>(handoff, AgentHandoff.ComposerPart).IsVisible.ShouldBeFalse();
            // No invented model or wait by default.
            handoff.AssistantModel.ShouldBeNull();
            handoff.ReplyEstimate.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Card_Without_A_Stage_Plays_The_Simulation_And_Says_So()
    {
        var simulation = new AgentHandoffSimulation { Script = Script };
        var handoff = new AgentHandoff { Simulation = simulation };
        var window = TestHelpers.Show(handoff);
        try
        {
            handoff.IsSimulated.ShouldBeTrue();
            handoff.PlayingSimulation.ShouldBeSameAs(simulation);
            simulation.IsPlaying.ShouldBeTrue();
            handoff.LiveRegion!.Announcement.ShouldBe(AgentHandoffLabels.Default.SimulatedNotice);
            TestHelpers.Descendant<Control>(handoff, AgentHandoff.ComposerPart).IsVisible.ShouldBeTrue();
            // Motion off is not full motion: the simulation plays reduced.
            simulation.IsReduced.ShouldBeTrue();
            simulation.Advance(3);
            handoff.DisplayedMessages.ShouldContain(m => m.Text == Script.CustomerOpening);

            // The app's own stage wins over the simulation.
            handoff.Stage = AgentHandoffStage.Assistant;
            handoff.PlayingSimulation.ShouldBeNull();
            simulation.IsPlaying.ShouldBeFalse();
            handoff.DisplayedMessages.ShouldBeEmpty();
            TestHelpers.Descendant<Control>(handoff, AgentHandoff.ComposerPart).IsVisible.ShouldBeFalse();

            // Without a stage it plays again, from the top.
            handoff.ClearValue(AgentHandoff.StageProperty);
            handoff.PlayingSimulation.ShouldBeSameAs(simulation);
            simulation.IsPlaying.ShouldBeTrue();
            simulation.Messages.ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
        handoff.PlayingSimulation.ShouldBeNull();
        simulation.IsPlaying.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Removing_The_Simulation_Stops_It_And_Empties_The_Chat()
    {
        var simulation = new AgentHandoffSimulation { Script = Script };
        var handoff = new AgentHandoff { Simulation = simulation };
        var window = TestHelpers.Show(handoff);
        try
        {
            simulation.Advance(3);
            handoff.DisplayedMessages.ShouldNotBeEmpty();
            handoff.Simulation = null;
            simulation.IsPlaying.ShouldBeFalse();
            handoff.IsSimulated.ShouldBeFalse();
            handoff.DisplayedMessages.ShouldBeEmpty();
            TestHelpers.Descendant<Control>(handoff, AgentHandoff.ComposerPart).IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Changing_The_Scenario_Starts_The_Story_Over()
    {
        var simulation = new AgentHandoffSimulation { Script = Script };
        var handoff = new AgentHandoff { Simulation = simulation };
        var window = TestHelpers.Show(handoff);
        try
        {
            Run(simulation, 30, () => simulation.Stage == AgentHandoffStage.Transferring);
            simulation.Stage.ShouldBe(AgentHandoffStage.Transferring);
            simulation.Scenario = AgentHandoffScenario.Unavailable;
            simulation.Stage.ShouldBe(AgentHandoffStage.Assistant);
            simulation.Messages.ShouldBeEmpty();
            Run(simulation, 60, () => simulation.Stage == AgentHandoffStage.Unavailable);
            handoff.DisplayedStage.ShouldBe(AgentHandoffStage.Unavailable);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Header_Shows_The_Title_Subtitle_And_A_Status_That_Follows_The_Stage()
    {
        var handoff = Wired(AgentHandoffStage.Assistant);
        var window = TestHelpers.Show(handoff);
        try
        {
            TestHelpers.Descendant<TextBlock>(handoff, AgentHandoff.TitlePart).Text.ShouldBe("Order NW-48213");
            TestHelpers.Descendant<TextBlock>(handoff, AgentHandoff.SubtitlePart).IsVisible.ShouldBeTrue();
            handoff.DisplayedStatus.Text.ShouldBe("AI assistant");

            handoff.Stage = AgentHandoffStage.Transferring;
            handoff.DisplayedStatus.ShouldBe((AgentHandoffStatusKind.Transferring, "Transferring"));
            handoff.Stage = AgentHandoffStage.Connected;
            handoff.DisplayedStatus.Text.ShouldBe("With Jordan");
            handoff.Stage = AgentHandoffStage.Resolved;
            handoff.DisplayedStatus.Text.ShouldBe("Resolved");
            handoff.Stage = AgentHandoffStage.Unavailable;
            handoff.DisplayedStatus.Text.ShouldBe("No one available");

            handoff.Title = null;
            TestHelpers.Descendant<TextBlock>(handoff, AgentHandoff.TitlePart).Text.ShouldBe("Support chat");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Stage_Changes_And_Notices_Are_Announced_Once()
    {
        var handoff = Wired(AgentHandoffStage.Assistant);
        var window = TestHelpers.Show(handoff);
        try
        {
            handoff.LiveRegion!.Announcement.ShouldBeNull();
            handoff.Stage = AgentHandoffStage.Transferring;
            handoff.LiveRegion.Announcement.ShouldBe("Transferring you to a person.");
            handoff.Stage = AgentHandoffStage.Connected;
            handoff.LiveRegion.Announcement.ShouldBe("Jordan Reyes joined the chat.");
            handoff.Notice = "Refund issued.";
            handoff.LiveRegion.Announcement.ShouldBe("Refund issued.");
            handoff.Typing = AgentHandoffTyping.Agent;
            handoff.LiveRegion.Announcement.ShouldBe("Refund issued.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Talk_To_A_Person_Shows_Under_The_Last_Answer_And_Raises_HumanRequested()
    {
        var handoff = Wired(AgentHandoffStage.Assistant);
        var raised = 0;
        handoff.HumanRequested += (_, _) => raised++;
        var window = TestHelpers.Show(handoff);
        try
        {
            var suggestion = TestHelpers.Descendant<AgentHandoffSuggestion>(handoff);
            AutomationProperties.GetName(suggestion).ShouldBe("Talk to a person");
            suggestion.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            raised.ShouldBe(1);

            handoff.AllowsRequestHuman = false;
            handoff.Rows.Any(r => r.Content is AgentHandoffSuggestion).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Talk_To_A_Person_Appears_Once_The_Streamed_Answer_Finishes()
    {
        var handoff = Wired(AgentHandoffStage.Assistant);
        var reply = new AgentHandoffMessage(AgentHandoffSender.Assistant, "Working") { IsStreaming = true };
        handoff.Messages.Add(reply);
        var window = TestHelpers.Show(handoff);
        try
        {
            handoff.Rows.Any(r => r.Content is AgentHandoffSuggestion).ShouldBeFalse();
            reply.Text = "Working on it.";
            handoff.Rows.Any(r => r.Content is AgentHandoffSuggestion).ShouldBeFalse();
            reply.IsStreaming = false;
            handoff.Rows.Any(r => r.Content is AgentHandoffSuggestion).ShouldBeTrue();

            reply.From = AgentHandoffSender.Customer;
            handoff.Rows.Select(r => r.Content).OfType<AgentHandoffBubble>().Single(b => b.Message == reply)
                .Sender.ShouldBe(AgentHandoffSender.Customer);
            handoff.Rows.Any(r => r.Content is AgentHandoffSuggestion).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Transferring_Card_Shows_The_Wait_And_Cancel_And_The_Summary()
    {
        var handoff = Wired(AgentHandoffStage.Transferring);
        var cancelled = 0;
        handoff.CancelTransferRequested += (_, _) => cancelled++;
        var window = TestHelpers.Show(handoff);
        try
        {
            var card = handoff.Card!;
            card.TitleText.ShouldBe("Transferring you to a person");
            card.Detail.ShouldBe("Typical reply 2 min");
            card.ActionText.ShouldBe("Cancel");
            card.SummaryBox!.IsExpanded.ShouldBeTrue();
            TestHelpers.Descendant<TextBlock>(card, AgentHandoffCard.IssuePart).Text.ShouldBe("Desk arrived with a cracked leg.");

            card.ActionButton!.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            cancelled.ShouldBe(1);

            handoff.ReplyEstimate = "5 min";
            card.Detail.ShouldBe("Typical reply 5 min");
            handoff.AllowsCancelTransfer = false;
            card.ActionText.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Summary_Toggle_Opens_And_Closes_With_Space_And_Reports_Its_State()
    {
        var handoff = Wired(AgentHandoffStage.Transferring);
        var window = TestHelpers.Show(handoff);
        try
        {
            var toggle = handoff.Card!.Toggle!;
            toggle.IsExpanded.ShouldBeTrue();
            AutomationProperties.GetName(toggle).ShouldBe("Summary shared with the team, Hide summary");

            toggle.Focus();
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
            handoff.IsSummaryExpanded.ShouldBeFalse();
            toggle.IsExpanded.ShouldBeFalse();
            handoff.Card.SummaryBox!.IsExpanded.ShouldBeFalse();
            AutomationProperties.GetName(toggle).ShouldBe("Summary shared with the team, Show summary");

            handoff.Summary = null;
            toggle.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Unavailable_Card_Offers_Try_Again()
    {
        var handoff = Wired(AgentHandoffStage.Unavailable);
        var retried = 0;
        handoff.RetryRequested += (_, _) => retried++;
        var window = TestHelpers.Show(handoff);
        try
        {
            var card = handoff.Card!;
            card.TitleText.ShouldBe("No one is available right now");
            card.ActionText.ShouldBe("Try again");
            card.ActionButton!.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            retried.ShouldBe(1);
            handoff.AllowsRetry = false;
            card.ActionText.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Connected_Shows_The_Person_On_The_Join_Step_In_The_Card_And_In_The_Composer()
    {
        var handoff = Wired(AgentHandoffStage.Connected);
        handoff.AgentMessages.Add(Customer("Photos attached."));
        handoff.AgentMessages.Add(Person("Hi, I'm Jordan."));
        var window = TestHelpers.Show(handoff);
        try
        {
            var join = handoff.Rows.Single(r => ReferenceEquals(r.Key, AgentHandoff.RowKeys.JoinStep));
            ((AgentHandoffStep)join.Content!).Line.ShouldBe("Jordan Reyes joined · Support specialist · 14:05");
            ((AgentHandoffMarker)join.Marker!).PersonName.ShouldBe("Jordan Reyes");
            handoff.Card!.TitleText.ShouldBe("Jordan joined from Help desk");
            handoff.Card.PersonName.ShouldBe("Jordan Reyes");
            handoff.DisplayedPlaceholder.ShouldBe("Message Jordan");

            handoff.Typing = AgentHandoffTyping.Agent;
            Dispatcher.UIThread.RunJobs();
            var dots = TestHelpers.Descendant<AgentHandoffTypingView>(handoff);
            dots.IsAgent.ShouldBeTrue();
            AutomationProperties.GetName(dots).ShouldBe("Jordan is typing");

            handoff.Placeholder = "Reply";
            handoff.DisplayedPlaceholder.ShouldBe("Reply");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Timeline_Joins_The_Markers_And_The_Card_Carries_The_Pending_Stretch_While_Transferring()
    {
        var handoff = Wired(AgentHandoffStage.Transferring);
        handoff.AgentMessages.Add(Customer("note"));
        var window = TestHelpers.Show(handoff);
        try
        {
            var rows = handoff.Rows;
            rows[0].LineAbove.ShouldBeFalse();
            rows[0].LineBelow.ShouldBeTrue();
            rows[1].LineAbove.ShouldBeTrue();
            rows[1].LineBelow.ShouldBeTrue();
            var card = rows.Single(r => ReferenceEquals(r.Key, AgentHandoff.RowKeys.Card));
            card.LineAbove.ShouldBeTrue();
            card.LineBelow.ShouldBeFalse();
            card.IsPending.ShouldBeTrue();
            rows[^1].LineAbove.ShouldBeFalse();

            handoff.Stage = AgentHandoffStage.Connected;
            card.IsPending.ShouldBeFalse();
            card.LineBelow.ShouldBeTrue();
            rows = handoff.Rows;
            rows[^1].LineAbove.ShouldBeTrue();
            rows[^1].LineBelow.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Composer_Sends_The_Trimmed_Text_On_Enter_And_Clears()
    {
        var handoff = Wired(AgentHandoffStage.Connected);
        string? sent = null;
        handoff.SendRequested += (_, e) => sent = e.Text;
        var window = TestHelpers.Show(handoff);
        try
        {
            var input = TestHelpers.Descendant<TextBox>(handoff, AgentHandoff.InputPart);
            AutomationProperties.GetName(input).ShouldBe("Message");
            input.Focus();
            input.Text = "   ";
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            sent.ShouldBeNull();

            input.Text = "  Thanks Jordan  ";
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            sent.ShouldBe("Thanks Jordan");
            input.Text.ShouldBe(string.Empty);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Thread_Is_A_Focusable_Log_That_Scrolls_With_Keys_And_Stays_Pinned()
    {
        var handoff = Wired(AgentHandoffStage.Connected);
        handoff.Height = 260;
        for (var i = 0; i < 12; i++)
        {
            handoff.AgentMessages.Add(Person("Message number " + i + " with enough words to take a line or two."));
        }
        var window = TestHelpers.Show(handoff);
        try
        {
            var scroller = handoff.Scroller!;
            scroller.Focusable.ShouldBeTrue();
            AutomationProperties.GetName(scroller).ShouldBe("Conversation");
            var max = scroller.Extent.Height - scroller.Viewport.Height;
            max.ShouldBeGreaterThan(0);
            scroller.Offset.Y.ShouldBe(max, 1);

            scroller.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            scroller.Offset.Y.ShouldBe(0);
            handoff.IsPinned.ShouldBeFalse();

            // Not pinned: a new message does not pull the reader down.
            handoff.AgentMessages.Add(Person("Another one."));
            Dispatcher.UIThread.RunJobs();
            scroller.Offset.Y.ShouldBe(0);

            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            handoff.IsPinned.ShouldBeTrue();
            handoff.AgentMessages.Add(Person("And the newest."));
            Dispatcher.UIThread.RunJobs();
            scroller.Offset.Y.ShouldBe(scroller.Extent.Height - scroller.Viewport.Height, 1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Card_Fills_Its_Container_And_Never_Follows_Its_Content()
    {
        var handoff = Wired(AgentHandoffStage.Assistant);
        var host = new Border { Width = 380, Child = handoff };
        var window = TestHelpers.Show(host);
        try
        {
            handoff.Bounds.Width.ShouldBe(380);
            handoff.IsCompact.ShouldBeTrue();
            handoff.BubbleFraction.ShouldBe(0.92);
            handoff.Messages.Add(Assistant(string.Concat(Enumerable.Repeat("A long streamed sentence ", 40))));
            Dispatcher.UIThread.RunJobs();
            handoff.Bounds.Width.ShouldBe(380);

            host.Width = 900;
            Dispatcher.UIThread.RunJobs();
            handoff.Bounds.Width.ShouldBe(560);
            handoff.IsCompact.ShouldBeFalse();
            handoff.BubbleFraction.ShouldBe(0.8);
            handoff.Bounds.Height.ShouldBe(600);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Streamed_Chunks_Fade_In_At_Full_Motion_And_Show_At_Once_Otherwise()
    {
        var streaming = new AgentHandoffMessage(AgentHandoffSender.Assistant, "First words") { IsStreaming = true };
        var handoff = Wired(AgentHandoffStage.Assistant);
        handoff.Messages.Add(streaming);
        var window = TestHelpers.Show(handoff, ArcMotionLevel.Full);
        try
        {
            var bubble = handoff.Rows.Select(r => r.Content).OfType<AgentHandoffBubble>().Single(b => b.Message == streaming);
            streaming.Text = "First words and three more";
            bubble.TextView!.Shown.ShouldBe("First words and three more");
            bubble.TextView.FadingCount.ShouldBe(1);
            TestHelpers.PumpUntil(() => bubble.TextView.FadingCount == 0).ShouldBeTrue();
            // Finished chunks fold back into the text: it draws as one piece again.
            bubble.TextView.DrawsWhole.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }

        var still = new AgentHandoffMessage(AgentHandoffSender.Assistant, "First") { IsStreaming = true };
        var plain = Wired(AgentHandoffStage.Assistant);
        plain.Messages.Add(still);
        var reduced = TestHelpers.Show(plain, ArcMotionLevel.Reduced);
        try
        {
            var bubble = plain.Rows.Select(r => r.Content).OfType<AgentHandoffBubble>().Single(b => b.Message == still);
            still.Text = "First and more";
            bubble.TextView!.FadingCount.ShouldBe(0);
            bubble.TextView.Shown.ShouldBe("First and more");
        }
        finally
        {
            reduced.Close();
        }
    }

    [AvaloniaFact]
    public void Thread_Height_Springs_At_Full_Motion_And_Jumps_Otherwise()
    {
        var handoff = Wired(AgentHandoffStage.Assistant);
        var window = TestHelpers.Show(handoff, ArcMotionLevel.Full);
        try
        {
            var spring = handoff.ThreadSpring!;
            var before = spring.DisplayedHeight;
            handoff.Messages.Add(Customer("One more message to make the thread taller."));
            Dispatcher.UIThread.RunJobs();
            spring.TargetHeight.ShouldBeGreaterThan(before);
            TestHelpers.PumpUntil(() => Math.Abs(spring.DisplayedHeight - spring.TargetHeight) < 0.5).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }

        var still = Wired(AgentHandoffStage.Assistant);
        var off = TestHelpers.Show(still);
        try
        {
            still.Messages.Add(Customer("One more."));
            Dispatcher.UIThread.RunJobs();
            still.ThreadSpring!.IsMoving.ShouldBeFalse();
            still.ThreadSpring.DisplayedHeight.ShouldBe(still.ThreadSpring.TargetHeight);
        }
        finally
        {
            off.Close();
        }
    }

    [Fact]
    public void Travel_Dot_And_Typing_Dots_Sample_Their_Keyframes()
    {
        AgentHandoffTravelDot.Sample(0).Position.ShouldBe(0);
        AgentHandoffTravelDot.Sample(0).Opacity.ShouldBe(0);
        AgentHandoffTravelDot.Sample(AgentHandoffMotion.TravelSeconds / 2).Position.ShouldBe(0.5, 1e-9);
        AgentHandoffTravelDot.Sample(AgentHandoffMotion.TravelSeconds / 2).Opacity.ShouldBe(1);
        AgentHandoffTypingDots.Sample(0, 1).Opacity.ShouldBe(0.35);
        AgentHandoffTypingDots.Sample(0.36, 0).Opacity.ShouldBe(1, 1e-9);
        AgentHandoffTypingDots.Sample(0.36 + AgentHandoffMotion.DotsStagger, 1).Lift.ShouldBe(2, 1e-9);
    }

    [AvaloniaFact]
    public void Labels_Reword_Everything()
    {
        var handoff = Wired(AgentHandoffStage.Transferring);
        handoff.Labels = new AgentHandoffLabels
        {
            Transferring = "Übergabe",
            CardTransferring = "Wir verbinden Sie",
            TypicalReply = estimate => "Antwort in " + estimate,
        };
        var window = TestHelpers.Show(handoff);
        try
        {
            handoff.DisplayedStatus.Text.ShouldBe("Übergabe");
            handoff.Card!.TitleText.ShouldBe("Wir verbinden Sie");
            handoff.Card.Detail.ShouldBe("Antwort in 2 min");
        }
        finally
        {
            window.Close();
        }
    }
}
