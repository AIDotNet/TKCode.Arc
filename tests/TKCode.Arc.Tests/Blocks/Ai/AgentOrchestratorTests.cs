using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.Motion;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="AgentOrchestrator"/>: the sample defaults, the scripted run (start, pause, resume, reset, finish), what each
/// message kind does to the agents, the graph's layouts and edges, dots under each motion level, selection and the
/// keyboard, the log and the detail panel, events, announcements and the compact layout. Waits and the clock are manual.
/// </summary>
public class AgentOrchestratorTests
{
    private sealed class Gate
    {
        private readonly List<(TimeSpan Time, TaskCompletionSource Done)> _waits = new();

        public int Count => _waits.Count;

        public TimeSpan Now { get; set; }

        public Task Wait(TimeSpan time, CancellationToken token)
        {
            var done = new TaskCompletionSource();
            // A cancelled wait (pause, reset) leaves the queue.
            token.Register(() =>
            {
                done.TrySetCanceled(token);
                _waits.RemoveAll(w => ReferenceEquals(w.Done, done));
            });
            _waits.Add((time, done));
            return done.Task;
        }

        /// <summary>Releases the oldest wait and advances the clock by its time.</summary>
        public void Step()
        {
            _waits.Count.ShouldBeGreaterThan(0);
            var (time, done) = _waits[0];
            _waits.RemoveAt(0);
            Now += time;
            done.TrySetResult();
            Dispatcher.UIThread.RunJobs();
        }

        public void Drain()
        {
            var guard = 0;
            while (_waits.Count > 0 && guard++ < 500)
            {
                Step();
            }
        }
    }

    private static (AgentOrchestrator Card, Window Window, Gate Gate) Show(double width = 900, ArcMotionLevel level = ArcMotionLevel.Off,
        Action<AgentOrchestrator>? configure = null)
    {
        var gate = new Gate();
        var card = new AgentOrchestrator { Delay = gate.Wait, Clock = () => gate.Now, IsAutoStart = false };
        configure?.Invoke(card);
        var window = TestHelpers.Show(card, level);
        window.Width = width;
        window.Height = 1000;
        Dispatcher.UIThread.RunJobs();
        return (card, window, gate);
    }

    private static OrchestratorAgent[] Team() =>
    [
        new OrchestratorAgent { Id = "lead", Name = "Atlas", Role = "Planner" },
        new OrchestratorAgent { Id = "a", Name = "Scout", Role = "Venues" },
        new OrchestratorAgent { Id = "b", Name = "Ledger", Role = "Budget" },
    ];

    private static OrchestratorMessage Msg(string from, string to, OrchestratorMessageKind kind, string text = "x") =>
        new() { From = from, To = to, Kind = kind, Text = text };

    [AvaloniaFact]
    public void Defaults_Show_The_Sample_Team_With_The_Lead_First()
    {
        var (card, window, _) = Show();
        card.IsUsingSamples.ShouldBeTrue();
        card.EffectiveAgents.Count.ShouldBe(5);
        card.Lead!.Id.ShouldBe("atlas");
        card.EffectiveGoal.ShouldBe(AgentOrchestratorSamples.Goal);
        card.EffectiveScript.ShouldNotBeNull();
        card.RunState.ShouldBe(OrchestratorRunState.Idle);
        card.Graph!.Nodes.Count.ShouldBe(5);
        card.Graph.Nodes[0].Classes.ShouldContain(":lead");
        card.Graph.IsTree.ShouldBeFalse();
        card.Graph.Edges.Count.ShouldBe(4);
        card.ProgressText!.Text.ShouldBe("0 of 5 agents done · 0 messages");
        card.Classes.ShouldContain(":empty-log");
        // The lead is shown in the detail panel until another agent is selected.
        card.DetailNameText!.Text.ShouldBe("Atlas");
        card.Graph.Nodes[0].IsSelected.ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public void Auto_Start_Plays_The_Script_When_Shown()
    {
        var gate = new Gate();
        var card = new AgentOrchestrator { Delay = gate.Wait, Clock = () => gate.Now };
        var window = TestHelpers.Show(card);
        Dispatcher.UIThread.RunJobs();
        card.RunState.ShouldBe(OrchestratorRunState.Running);
        card.Lead!.Task.ShouldBe(AgentOrchestratorSamples.Goal);
        gate.Count.ShouldBe(1);
        window.Close();
    }

    [AvaloniaFact]
    public void The_Sample_Script_Runs_To_Completion()
    {
        var (card, window, gate) = Show();
        var states = new List<OrchestratorRunState>();
        card.RunStateChanged += (_, e) => states.Add(e.NewState);
        card.Start();
        gate.Drain();
        card.RunState.ShouldBe(OrchestratorRunState.Completed);
        states.ShouldBe([OrchestratorRunState.Running, OrchestratorRunState.Completed]);
        card.Messages.Count.ShouldBe(AgentOrchestratorSamples.Script().Count(s => s.Message is not null));
        card.Messages.Select(m => m.Sequence).ShouldBe(Enumerable.Range(1, card.Messages.Count));
        foreach (var agent in card.EffectiveAgents)
        {
            agent.Status.ShouldBe(OrchestratorAgentStatus.Done, agent.Name);
            agent.Result.ShouldNotBeNullOrEmpty();
        }
        card.ProgressText!.Text.ShouldBe($"5 of 5 agents done · {card.Messages.Count} messages");
        card.LastAnnouncement.ShouldBe("Run complete");
        // Scout and Ledger talked, and so did Lens and Quill: two peer lines.
        card.Graph!.Edges.Count(e => e.IsPeer).ShouldBe(2);
        card.Elapsed.ShouldBe(TimeSpan.FromMilliseconds(AgentOrchestratorSamples.Script().Sum(s => s.Delay.TotalMilliseconds)));
        window.Close();
    }

    [AvaloniaFact]
    public void Pause_Holds_The_Step_And_Resume_Waits_For_It_Again()
    {
        var (card, window, gate) = Show();
        card.Start();
        gate.Step();
        gate.Step();
        card.StepIndex.ShouldBe(2);
        card.Pause();
        card.RunState.ShouldBe(OrchestratorRunState.Paused);
        card.Elapsed.ShouldBe(TimeSpan.FromMilliseconds(1400));
        card.Toggle();
        card.RunState.ShouldBe(OrchestratorRunState.Running);
        card.StepIndex.ShouldBe(2);
        gate.Count.ShouldBe(1);
        gate.Step();
        card.StepIndex.ShouldBe(3);
        card.Messages.Count.ShouldBe(1);
        card.Find("scout")!.Status.ShouldBe(OrchestratorAgentStatus.Working);
        window.Close();
    }

    [AvaloniaFact]
    public void Speed_Scales_The_Delays()
    {
        var times = new List<TimeSpan>();
        var card = new AgentOrchestrator
        {
            IsAutoStart = false,
            Speed = 2,
            Delay = (time, token) =>
            {
                times.Add(time);
                return Task.Delay(Timeout.Infinite, token);
            }
        };
        var window = TestHelpers.Show(card);
        card.Start();
        times.ShouldBe([TimeSpan.FromMilliseconds(150)]);
        card.Reset();
        window.Close();
    }

    [AvaloniaFact]
    public void Reset_Clears_The_Run_And_Run_Again_Starts_Over()
    {
        var (card, window, gate) = Show();
        card.Start();
        gate.Drain();
        card.RunButton!.Content.ShouldNotBeNull();
        AutomationProperties.GetName(card.RunButton).ShouldBe("Run again");
        card.Toggle();
        card.RunState.ShouldBe(OrchestratorRunState.Running);
        card.Messages.Count.ShouldBe(0);
        card.StepIndex.ShouldBe(0);
        card.Reset();
        card.RunState.ShouldBe(OrchestratorRunState.Idle);
        card.Messages.ShouldBeEmpty();
        foreach (var agent in card.EffectiveAgents)
        {
            agent.Status.ShouldBe(OrchestratorAgentStatus.Idle);
            agent.Task.ShouldBeNull();
            agent.Result.ShouldBeNull();
        }
        card.Graph!.Edges.Count(e => e.IsPeer).ShouldBe(0);
        card.ResetButton!.IsEnabled.ShouldBeFalse();
        window.Close();
    }

    [AvaloniaFact]
    public void Each_Message_Kind_Updates_The_Agents()
    {
        var (card, window, _) = Show(configure: c => c.Agents.AddRange(Team()));
        card.IsUsingSamples.ShouldBeFalse();
        card.EffectiveScript.ShouldBeNull();
        var lead = card.Find("lead")!;
        var a = card.Find("a")!;
        var b = card.Find("b")!;

        // Posting while idle with no script starts the run.
        card.Post(Msg("lead", "a", OrchestratorMessageKind.Delegate, "Find venues"));
        card.RunState.ShouldBe(OrchestratorRunState.Running);
        a.Task.ShouldBe("Find venues");
        a.Status.ShouldBe(OrchestratorAgentStatus.Working);

        card.Post(Msg("a", "lead", OrchestratorMessageKind.Progress, "Two found"));
        a.Activity.ShouldBe("Two found");

        card.Post(Msg("b", "a", OrchestratorMessageKind.Question, "What do they cost?"));
        b.Status.ShouldBe(OrchestratorAgentStatus.Waiting);
        card.Post(Msg("a", "b", OrchestratorMessageKind.Answer, "190 a night"));
        b.Status.ShouldBe(OrchestratorAgentStatus.Working);

        card.Post(Msg("a", "lead", OrchestratorMessageKind.Result, "Pinewater Lodge"));
        a.Status.ShouldBe(OrchestratorAgentStatus.Done);
        a.Result.ShouldBe("Pinewater Lodge");
        a.Activity.ShouldBeNull();

        card.Post(Msg("b", "lead", OrchestratorMessageKind.Error, "No prices"));
        b.Status.ShouldBe(OrchestratorAgentStatus.Failed);
        b.Result.ShouldBe("No prices");

        card.SetStatus("lead", OrchestratorAgentStatus.Done, "Plan sent");
        lead.Activity.ShouldBe("Plan sent");
        card.Complete();
        card.RunState.ShouldBe(OrchestratorRunState.Completed);
        card.Messages.Count.ShouldBe(6);
        window.Close();
    }

    [AvaloniaFact]
    public void Post_Rejects_Unknown_Agents_And_Reposts()
    {
        var (card, window, _) = Show(configure: c => c.Agents.AddRange(Team()));
        Should.Throw<ArgumentException>(() => card.Post(Msg("lead", "nobody", OrchestratorMessageKind.Delegate)));
        Should.Throw<ArgumentException>(() => card.SetStatus("nobody", OrchestratorAgentStatus.Done));
        var message = Msg("lead", "a", OrchestratorMessageKind.Delegate);
        card.Post(message);
        Should.Throw<ArgumentException>(() => card.Post(message));
        card.Post(message.Copy());
        card.Messages.Count.ShouldBe(2);
        window.Close();
    }

    [AvaloniaFact]
    public void Events_Report_Messages_And_Status_Changes()
    {
        var (card, window, gate) = Show(configure: c => c.Agents.AddRange(Team()));
        var posted = new List<OrchestratorMessage>();
        var changes = new List<(string, OrchestratorAgentStatus, OrchestratorAgentStatus)>();
        card.MessagePosted += (_, e) => posted.Add(e.Message);
        card.AgentStatusChanged += (_, e) => changes.Add((e.Agent.Id, e.OldStatus, e.NewStatus));
        card.Start();
        gate.Now = TimeSpan.FromSeconds(3);
        card.Post(Msg("lead", "a", OrchestratorMessageKind.Delegate));
        posted.Single().Elapsed.ShouldBe(TimeSpan.FromSeconds(3));
        changes.ShouldBe([("a", OrchestratorAgentStatus.Idle, OrchestratorAgentStatus.Working)]);
        card.LastAnnouncement.ShouldBe("Scout is working");
        // A change made on the agent itself is reported too.
        card.Find("b")!.Status = OrchestratorAgentStatus.Thinking;
        changes.Last().ShouldBe(("b", OrchestratorAgentStatus.Idle, OrchestratorAgentStatus.Thinking));
        window.Close();
    }

    [AvaloniaFact]
    public void Lead_Id_Picks_The_Lead_And_Orders_The_Graph()
    {
        var (card, window, _) = Show(configure: c =>
        {
            c.Agents.AddRange(Team());
            c.LeadId = "b";
        });
        card.Lead!.Id.ShouldBe("b");
        card.Graph!.Nodes.Select(n => n.Agent!.Id).ShouldBe(["b", "lead", "a"]);
        card.Graph.Edges.All(e => e.A == 0).ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public void The_Fan_Layout_Puts_The_Lead_Above_A_Row_Of_Sub_Agents()
    {
        var (card, window, _) = Show(900);
        var graph = card.Graph!;
        graph.IsTree.ShouldBeFalse();
        var lead = graph.NodeRect(0);
        for (var i = 1; i < graph.Nodes.Count; i++)
        {
            var rect = graph.NodeRect(i);
            rect.Y.ShouldBeGreaterThan(lead.Bottom);
            rect.Width.ShouldBeGreaterThanOrEqualTo(156);
            if (i > 1)
            {
                rect.X.ShouldBeGreaterThan(graph.NodeRect(i - 1).Right);
            }
        }
        // Each lead edge leaves the lead's bottom and ends on top of its sub-agent.
        foreach (var edge in graph.Edges)
        {
            edge.Points[0].Y.ShouldBe(lead.Bottom, 0.01);
            edge.Points[^1].Y.ShouldBe(graph.NodeRect(edge.B).Y, 0.01);
            edge.At(0.5).Y.ShouldBeInRange(lead.Bottom, graph.NodeRect(edge.B).Y);
        }
        window.Close();
    }

    [AvaloniaFact]
    public void A_Narrow_Card_Stacks_The_Sub_Agents_And_Moves_The_Detail_Up()
    {
        var (card, window, _) = Show(420);
        var graph = card.Graph!;
        graph.IsTree.ShouldBeTrue();
        card.Classes.ShouldContain(":compact");
        for (var i = 2; i < graph.Nodes.Count; i++)
        {
            graph.NodeRect(i).Y.ShouldBeGreaterThan(graph.NodeRect(i - 1).Bottom);
            graph.Nodes[i].Classes.ShouldContain(":tree");
        }
        graph.Nodes[0].Classes.ShouldNotContain(":tree");
        var detail = TestHelpers.Descendant<Border>(card, "PART_DetailBox");
        Grid.GetColumn(detail).ShouldBe(0);
        var log = TestHelpers.Descendant<Border>(card, "PART_LogBox");
        Grid.GetRow(log).ShouldBe(1);
        window.Close();
    }

    [AvaloniaFact]
    public void Dots_Travel_Only_Under_Full_Motion()
    {
        var (off, offWindow, _) = Show(configure: c => c.Agents.AddRange(Team()));
        off.Post(Msg("lead", "a", OrchestratorMessageKind.Delegate));
        off.Graph!.PacketCount.ShouldBe(0);
        offWindow.Close();

        var (full, fullWindow, _) = Show(level: ArcMotionLevel.Full, configure: c => c.Agents.AddRange(Team()));
        full.Post(Msg("lead", "a", OrchestratorMessageKind.Delegate));
        full.Post(Msg("a", "b", OrchestratorMessageKind.Question));
        full.Graph!.PacketCount.ShouldBe(2);
        full.Graph.Edges.Count(e => e.IsPeer).ShouldBe(1);
        fullWindow.Close();
    }

    [AvaloniaFact]
    public void Clicking_Or_Keys_Select_A_Node_And_Fill_The_Detail()
    {
        var (card, window, _) = Show(configure: c => c.Agents.AddRange(Team()));
        card.Post(Msg("lead", "a", OrchestratorMessageKind.Delegate, "Find venues"));
        var graph = card.Graph!;
        graph.Nodes[1].Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        card.SelectedAgentId.ShouldBe("a");
        card.SelectedAgent!.Name.ShouldBe("Scout");
        graph.Nodes[1].IsSelected.ShouldBeTrue();
        graph.Nodes[0].IsSelected.ShouldBeFalse();
        card.DetailNameText!.Text.ShouldBe("Scout");
        card.DetailTaskText!.Text.ShouldBe("Find venues");
        card.DetailResultText!.Text.ShouldBe("Nothing yet");
        card.DetailTrafficText!.Text.ShouldBe("0 sent · 1 received");

        // The detail follows the agent.
        card.Post(Msg("a", "lead", OrchestratorMessageKind.Result, "Pinewater Lodge"));
        card.DetailResultText.Text.ShouldBe("Pinewater Lodge");

        // Right moves along the row, Up goes to the lead, Down back to the selected one, End to the last.
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        graph.Nodes[2].IsFocused.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
        graph.Nodes[0].IsFocused.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        graph.Nodes[1].IsFocused.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
        graph.Nodes[2].IsFocused.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
        graph.Nodes[0].IsFocused.ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public void Tree_Navigation_Moves_Up_And_Down_The_List()
    {
        var (card, window, _) = Show(420);
        var graph = card.Graph!;
        graph.Navigate(0, Key.Down).ShouldBe(1);
        graph.Navigate(2, Key.Up).ShouldBe(1);
        graph.Navigate(4, Key.Down).ShouldBe(4);
        graph.Navigate(0, Key.Up).ShouldBe(0);
        graph.Navigate(1, Key.A).ShouldBeNull();
        window.Close();
    }

    [AvaloniaFact]
    public void Nodes_Have_Names_That_Follow_The_Agent()
    {
        var (card, window, _) = Show(configure: c => c.Agents.AddRange(Team()));
        var node = card.Graph!.Nodes[1];
        AutomationProperties.GetName(node).ShouldBe("Scout, Venues, Idle");
        node.Classes.ShouldContain(":idle");
        node.Classes.ShouldContain(":empty");
        card.Post(Msg("lead", "a", OrchestratorMessageKind.Delegate, "Find venues"));
        AutomationProperties.GetName(node).ShouldBe("Scout, Venues, Working: Find venues");
        node.Classes.ShouldContain(":working");
        node.Classes.ShouldNotContain(":empty");
        node.Doing.ShouldBe("Find venues");
        AutomationProperties.GetName(card.Graph).ShouldBe("Agents");
        window.Close();
    }

    [AvaloniaFact]
    public void The_Log_Lists_Messages_With_Names_And_Times()
    {
        var (card, window, gate) = Show(configure: c => c.Agents.AddRange(Team()));
        card.Start();
        gate.Now = TimeSpan.FromSeconds(75);
        card.Post(Msg("lead", "a", OrchestratorMessageKind.Delegate, "Find venues"));
        Dispatcher.UIThread.RunJobs();
        card.Classes.ShouldNotContain(":empty-log");
        var row = TestHelpers.Descendant<AgentOrchestratorLogRow>(card);
        row.FromName.ShouldBe("Atlas");
        row.ToName.ShouldBe("Scout");
        row.KindText.ShouldBe("Task");
        row.TimeText.ShouldBe("1:15");
        row.Classes.ShouldContain(":delegate");
        AutomationProperties.GetName(row).ShouldBe("Atlas to Scout, Task: Find venues");
        AgentOrchestratorLogRow.FormatTime(TimeSpan.FromSeconds(3725)).ShouldBe("1:02:05");
        window.Close();
    }

    [AvaloniaFact]
    public void Labels_Reword_The_Card()
    {
        var (card, window, _) = Show(configure: c => c.Agents.AddRange(Team()));
        card.Labels = new AgentOrchestratorLabels
        {
            Title = "Équipe",
            Start = "Lancer",
            StatusName = s => s == OrchestratorAgentStatus.Idle ? "Libre" : s.ToString(),
        };
        TestHelpers.Descendant<TextBlock>(card, "PART_Title").Text.ShouldBe("Équipe");
        AutomationProperties.GetName(card.RunButton!).ShouldBe("Lancer");
        card.Graph!.Nodes[1].StatusText.ShouldBe("Libre");
        card.Title = "Offsite";
        TestHelpers.Descendant<TextBlock>(card, "PART_Title").Text.ShouldBe("Offsite");
        window.Close();
    }

    [AvaloniaFact]
    public void The_Run_Button_Toggles_And_Reset_Clears()
    {
        var (card, window, gate) = Show();
        card.RunButton!.RaiseEvent(new RoutedEventArgs(AvaloniaButton.ClickEvent));
        card.RunState.ShouldBe(OrchestratorRunState.Running);
        AutomationProperties.GetName(card.RunButton).ShouldBe("Pause");
        card.ResetButton!.IsEnabled.ShouldBeTrue();
        gate.Step();
        card.RunButton.RaiseEvent(new RoutedEventArgs(AvaloniaButton.ClickEvent));
        card.RunState.ShouldBe(OrchestratorRunState.Paused);
        AutomationProperties.GetName(card.RunButton).ShouldBe("Resume");
        card.LastAnnouncement.ShouldBe("Run paused");
        card.ResetButton.RaiseEvent(new RoutedEventArgs(AvaloniaButton.ClickEvent));
        card.RunState.ShouldBe(OrchestratorRunState.Idle);
        AutomationProperties.GetName(card.RunButton).ShouldBe("Run");
        window.Close();
    }

    [AvaloniaFact]
    public void Leaving_The_Screen_Pauses_And_Coming_Back_Resumes()
    {
        var (card, window, gate) = Show();
        card.Start();
        gate.Step();
        var host = (ContentControl)window;
        host.Content = null;
        Dispatcher.UIThread.RunJobs();
        card.RunState.ShouldBe(OrchestratorRunState.Paused);
        host.Content = card;
        Dispatcher.UIThread.RunJobs();
        card.RunState.ShouldBe(OrchestratorRunState.Running);
        window.Close();
    }

    [AvaloniaFact]
    public void A_New_Agent_List_Resets_The_Run()
    {
        var (card, window, gate) = Show();
        card.Start();
        gate.Step();
        gate.Step();
        gate.Step();
        card.Messages.Count.ShouldBe(1);
        card.Agents.AddRange(Team());
        card.RunState.ShouldBe(OrchestratorRunState.Idle);
        card.Messages.ShouldBeEmpty();
        card.Graph!.Nodes.Count.ShouldBe(3);
        window.Close();
    }
}
