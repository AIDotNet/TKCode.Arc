using System.Text.Json.Nodes;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>
/// The invented calls the tool call tests use (the package ships none): a finance team asks what holds up its March books
/// close. Each method returns new calls; <see cref="Script"/> is the measured turn a simulation plays.
/// </summary>
internal static class ToolCallTestCalls
{
    /// <summary>The tool names used by the fixture.</summary>
    public const string TrackerTool = "Tracker";

    /// <inheritdoc cref="TrackerTool"/>
    public const string PlannerTool = "Planner";

    /// <inheritdoc cref="TrackerTool"/>
    public const string WarehouseTool = "Warehouse";

    /// <inheritdoc cref="TrackerTool"/>
    public const string ChatTool = "Chat";

    /// <inheritdoc cref="TrackerTool"/>
    public const string WebTool = "Web";

    /// <summary>A finished web search with three results (what a lone <see cref="ToolCall"/> shows by default).</summary>
    public static ToolCallItem WebSearch()
    {
        var item = new ToolCallItem("web-search", WebTool, "Searched the web for spring easing curves", ToolCallStatus.Success, ArcIcons.Globe)
        {
            RunningTitle = "Searching the web for spring easing curves",
            Output = new ToolCallListOutput(
            [
                new ToolCallListEntry("1", "Critically damped springs for interface motion", "guide"),
                new ToolCallListEntry("2", "Choosing a visual duration instead of stiffness", "article"),
                new ToolCallListEntry("3", "Why springs keep their velocity when retargeted", "notes")
            ]),
            Duration = TimeSpan.FromMilliseconds(640)
        };
        item.Input.Add(new ToolCallArgument("query", "spring easing curves interface motion"));
        item.Input.Add(new ToolCallArgument("limit", "3"));
        return item;
    }

    /// <summary>Call 1: a ticket search with four results (1180ms).</summary>
    public static ToolCallItem TicketSearch(ToolCallStatus status = ToolCallStatus.Success)
    {
        var item = new ToolCallItem("tickets", TrackerTool, "Searched tickets in finance/close", status, ArcIcons.Inbox)
        {
            RunningTitle = "Searching tickets in finance/close",
            Output = new ToolCallListOutput(
            [
                new ToolCallListEntry("#482", "Invoice export stops at 10,000 rows", "export"),
                new ToolCallListEntry("#491", "CSV dates use the wrong time zone", "format"),
                new ToolCallListEntry("#503", "Retry sends the same file twice", "delivery"),
                new ToolCallListEntry("#507", "Export button stays grey in one browser", "web")
            ]),
            Summary = "4 results",
            Duration = TimeSpan.FromMilliseconds(1180)
        };
        item.Input.Add(new ToolCallArgument("project", "finance/close"));
        item.Input.Add(new ToolCallArgument("query", "status:open tag:close-blocker month:march"));
        item.Input.Add(new ToolCallArgument("sort", "updated"));
        return item;
    }

    /// <summary>Call 2: a checklist read whose JSON runs 35 lines (760ms).</summary>
    public static ToolCallItem ChecklistRead(ToolCallStatus status = ToolCallStatus.Success)
    {
        var item = new ToolCallItem("checklist", PlannerTool, "Read the March close checklist", status, ArcIcons.ListChecks)
        {
            RunningTitle = "Reading the March close checklist",
            Output = new ToolCallJsonOutput(ChecklistJson()),
            Summary = "2 blocked",
            Duration = TimeSpan.FromMilliseconds(760)
        };
        item.Input.Add(new ToolCallArgument("board", "March close"));
        item.Input.Add(new ToolCallArgument("team", "FIN"));
        item.Input.Add(new ToolCallArgument("include", "tasks, owners"));
        return item;
    }

    /// <summary>Call 3: a query of the export job log. As <see cref="ToolCallStatus.Error"/> it timed out; once retried it
    /// returns two rows (420ms).</summary>
    public static ToolCallItem JobLogQuery(ToolCallStatus status = ToolCallStatus.Success)
    {
        var item = new ToolCallItem("job-log", WarehouseTool, "Read the export job log", status, ArcIcons.Database)
        {
            RunningTitle = "Reading the export job log",
            ErrorTitle = "Could not read the export job log",
            Message = "Connection to reporting_replica timed out after 5s",
            Output = new ToolCallTableOutput(
                ["job", "step", "finished_at"],
                [
                    ["0112", "build_invoice_rows", null],
                    ["0113", "write_csv_batches", null]
                ]),
            Summary = "2 rows",
            Duration = TimeSpan.FromMilliseconds(420)
        };
        item.Input.Add(new ToolCallArgument("database", "reporting_replica"));
        item.Input.Add(new ToolCallArgument("sql", "select job, step, finished_at from export_runs where finished_at is null order by job"));
        return item;
    }

    /// <summary>Call 4: posting a note to a channel, which needs approval first; once allowed it sends one message
    /// (610ms).</summary>
    public static ToolCallItem StatusNote(ToolCallStatus status = ToolCallStatus.Approval)
    {
        var item = new ToolCallItem("status-note", ChatTool, "Posted to #finance-ops", status, ArcIcons.MessageCircle)
        {
            RunningTitle = "Posting to #finance-ops",
            ApprovalTitle = "Post a status note to #finance-ops",
            Message = "Sends as Ledger bot to 36 people in #finance-ops",
            Output = new ToolCallTextOutput("Sent to #finance-ops at 09:15 as Ledger bot."),
            Summary = "1 message",
            Duration = TimeSpan.FromMilliseconds(610)
        };
        item.Input.Add(new ToolCallArgument("channel", "#finance-ops"));
        item.Input.Add(new ToolCallArgument("text",
            "The March close waits on #482 (export stops at 10,000 rows) and job 0112 on the reporting replica. The 31st still holds if both land by Thursday."));
        return item;
    }

    /// <summary>The four calls of the sample turn, all finished (1180 + 760 + 420 + 610ms = "3.0s"): what a group shows
    /// when it is given no calls.</summary>
    public static IReadOnlyList<ToolCallItem> SettledCalls() =>
    [
        TicketSearch(),
        ChecklistRead(),
        JobLogQuery(),
        StatusNote(ToolCallStatus.Success)
    ];

    /// <summary>
    /// The simulated turn <see cref="ToolCallSimulation"/> plays by default: the first call is appended 630ms after the
    /// start and each later one about 225ms after the one before settles (265ms after the retried query); the ticket
    /// search and the checklist read succeed, the job log query times out after 1.4s and waits for Retry (the retry runs
    /// 700ms), and the status note waits for Allow or Deny.
    /// </summary>
    public static IReadOnlyList<ToolCallSimulationStep> Script() =>
    [
        new(TicketSearch()) { Delay = TimeSpan.FromMilliseconds(630) },
        new(ChecklistRead()),
        new(JobLogQuery(), ToolCallSimulationOutcome.FailsUntilRetried) { RunFor = TimeSpan.FromMilliseconds(1400) },
        new(StatusNote(), ToolCallSimulationOutcome.NeedsApproval) { Delay = TimeSpan.FromMilliseconds(265) }
    ];

    /// <summary>The checklist the sample planner returns (35 lines once indented).</summary>
    private static JsonNode ChecklistJson() => new JsonObject
    {
        ["board"] = "March close",
        ["owner"] = "Iris Novak",
        ["due"] = "2026-03-31",
        ["progress"] = 0.72,
        ["health"] = "at risk",
        ["milestones"] = new JsonArray(
            new JsonObject { ["name"] = "Ledger lock", ["date"] = "2026-03-27", ["done"] = true },
            new JsonObject { ["name"] = "Board pack", ["date"] = "2026-03-31", ["done"] = false }),
        ["blocked"] = new JsonArray(
            new JsonObject
            {
                ["id"] = "FIN-77",
                ["title"] = "Export the March invoices",
                ["assignee"] = "Tomas Reyes",
                ["blockedBy"] = "finance/close#482"
            },
            new JsonObject
            {
                ["id"] = "FIN-81",
                ["title"] = "Reconcile the replica totals",
                ["assignee"] = "Ana Ibarra",
                ["blockedBy"] = null
            }),
        ["openTasks"] = 9,
        ["doneTasks"] = 31
    };
}

/// <summary>The tool call controls: expand state, keyboard, actions and the group's header and auto collapse.</summary>
public class ToolCallTests
{
    private static ToolCallGroup Turn(ToolCallStatus last = ToolCallStatus.Approval)
    {
        var group = new ToolCallGroup();
        group.Items.AddRange([ToolCallTestCalls.TicketSearch(), ToolCallTestCalls.ChecklistRead(), ToolCallTestCalls.JobLogQuery(),
            ToolCallTestCalls.StatusNote(last)]);
        return group;
    }

    [AvaloniaFact]
    public void Lone_Call_Without_An_Item_Is_Empty_And_Does_Not_Open()
    {
        var call = new ToolCall();
        var window = TestHelpers.Show(call);
        try
        {
            call.IsEmpty.ShouldBeTrue();
            call.Classes.Contains(":empty").ShouldBeTrue();
            call.DisplayedItem.ToolName.ShouldBe(string.Empty);
            call.TitleText!.Text.ShouldBe("No tool call");
            call.TitleText.Foreground.ShouldBe(call.TitleText.MutedForeground, "the empty title is muted");
            call.StatusGlyph!.IsVisible.ShouldBeFalse();
            call.MetaText.ShouldBeNull();
            AutomationProperties.GetName(call.Toggle!).ShouldBe("No tool call");
            call.ToggleByUser();
            call.IsExpanded.ShouldBeFalse();
            call.Item = ToolCallTestCalls.WebSearch();
            Dispatcher.UIThread.RunJobs();
            call.IsEmpty.ShouldBeFalse();
            call.Classes.Contains(":empty").ShouldBeFalse();
            call.StatusGlyph.IsVisible.ShouldBeTrue();
            call.TitleText.Text.ShouldBe("Searched the web for spring easing curves");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Lone_Call_Toggles()
    {
        var call = new ToolCall { Item = ToolCallTestCalls.WebSearch() };
        var window = TestHelpers.Show(call);
        try
        {
            call.DisplayedItem.ToolName.ShouldBe("Web");
            call.IsExpanded.ShouldBeFalse();
            call.Body!.IsMounted.ShouldBeFalse();
            call.Toggle!.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            call.IsExpanded.ShouldBeTrue();
            call.Body.IsMounted.ShouldBeTrue();
            call.Toggle.IsExpanded.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            call.IsExpanded.ShouldBeFalse();
            call.Body.IsMounted.ShouldBeFalse();
            AutomationProperties.GetName(call.Toggle).ShouldBe("Web: Searched the web for spring easing curves Done, 3 results, 640ms");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Status_Change_Updates_Row_In_Place()
    {
        var item = ToolCallTestCalls.JobLogQuery(ToolCallStatus.Running);
        var call = new ToolCall { Item = item, IsExpanded = true };
        var window = TestHelpers.Show(call);
        window.Width = 800;
        Dispatcher.UIThread.RunJobs();
        try
        {
            call.TitleText!.Text.ShouldBe("Reading the export job log");
            item.Status = ToolCallStatus.Error;
            Dispatcher.UIThread.RunJobs();
            call.TitleText.Text.ShouldBe("Could not read the export job log");
            call.Head!.IsShown(ToolCallHeadSlot.Note).ShouldBeTrue();
            call.Head.IsShown(ToolCallHeadSlot.Actions).ShouldBeTrue();
            call.RetryButton!.IsVisible.ShouldBeTrue();
            call.IsExpanded.ShouldBeTrue();
            call.Announcer!.Announcement.ShouldBe("Warehouse: Could not read the export job log. Connection to reporting_replica timed out after 5s");
            AutomationProperties.GetName(call.RetryButton).ShouldBe("Retry: Could not read the export job log");
            item.Status = ToolCallStatus.Success;
            Dispatcher.UIThread.RunJobs();
            call.Head.IsShown(ToolCallHeadSlot.Actions).ShouldBeFalse();
            call.MetaText.ShouldBe("2 rows · 420ms");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Unhandled_Deny_Marks_Denied_And_Handled_Actions_Change_Nothing()
    {
        var item = ToolCallTestCalls.StatusNote();
        var call = new ToolCall { Item = item };
        var window = TestHelpers.Show(call);
        try
        {
            var seen = new List<string>();
            call.ApproveRequested += (_, e) => { seen.Add("approve:" + e.Item.Id); e.Handled = true; };
            call.Act(ToolCall.ApproveRequestedEvent);
            seen.ShouldBe(["approve:status-note"]);
            item.Status.ShouldBe(ToolCallStatus.Approval);
            call.Act(ToolCall.DenyRequestedEvent);
            item.Status.ShouldBe(ToolCallStatus.Denied);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Unhandled_Allow_Reaches_Only_The_Simulation_That_Drives_The_Call()
    {
        var custom = new ToolCallItem("x", "Tool", "Did it", ToolCallStatus.Approval);
        var call = new ToolCall { Item = custom };
        var window = TestHelpers.Show(call);
        var time = new AgentStatusTestClock();
        using var sim = new ToolCallSimulation(
            [new ToolCallSimulationStep(ToolCallTestCalls.StatusNote(), ToolCallSimulationOutcome.NeedsApproval) { Delay = TimeSpan.Zero }], time);
        try
        {
            call.Act(ToolCall.ApproveRequestedEvent);
            custom.Status.ShouldBe(ToolCallStatus.Approval);
            sim.Start();
            time.Advance(1);
            var simulated = sim.Items.ShouldHaveSingleItem();
            simulated.Status.ShouldBe(ToolCallStatus.Approval);
            call.Item = simulated;
            call.Act(ToolCall.ApproveRequestedEvent);
            simulated.Status.ShouldBe(ToolCallStatus.Running);
            call.Announcer!.History.Last().ShouldContain("only simulated");
            time.Advance(610);
            simulated.Status.ShouldBe(ToolCallStatus.Success);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Group_Header_Follows_The_Calls_And_Actions_Bubble()
    {
        var group = Turn();
        var window = TestHelpers.Show(group);
        try
        {
            group.IsExpanded.ShouldBeTrue();
            group.HeaderText.ShouldBe("Waiting for approval");
            group.Stack!.Count.ShouldBe(4);
            group.RowControls.Count().ShouldBe(4);
            ApprovalHandler(group, out var seen);
            group.RowOf(group.Shown[3])!.Act(ToolCall.ApproveRequestedEvent);
            seen.ShouldBe(["approve:status-note"]);
            group.Shown[2].Status = ToolCallStatus.Error;
            Dispatcher.UIThread.RunJobs();
            group.FailedCount.ShouldBe(1);
            group.HeaderText.ShouldBe("Waiting for approval 1 failed");
            AutomationProperties.GetName(group.Header!).ShouldBe(group.HeaderText);
        }
        finally
        {
            window.Close();
        }
    }

    private static void ApprovalHandler(ToolCallGroup group, out List<string> seen)
    {
        var list = new List<string>();
        group.ApproveRequested += (_, e) => { list.Add("approve:" + e.Item.Id); e.Handled = true; };
        seen = list;
    }

    [AvaloniaFact]
    public void Group_Without_Calls_Or_Simulation_Is_Empty()
    {
        var group = new ToolCallGroup();
        var window = TestHelpers.Show(group);
        try
        {
            group.Shown.Count.ShouldBe(0);
            group.Classes.Contains(":empty").ShouldBeTrue();
            group.RowControls.ShouldBeEmpty();
            group.IsExpanded.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Settled_Calls_Start_Folded()
    {
        var group = new ToolCallGroup();
        group.Items.AddRange(ToolCallTestCalls.SettledCalls());
        var window = TestHelpers.Show(group);
        try
        {
            group.Shown.Count.ShouldBe(4);
            group.IsExpanded.ShouldBeFalse();
            group.HeaderText.ShouldBe("Used 4 tools 3.0s");
            group.ToggleByUser();
            group.IsExpanded.ShouldBeTrue();
            group.List!.IsMounted.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Group_Plays_Its_Simulation_On_Screen()
    {
        var time = new AgentStatusTestClock();
        var sim = new ToolCallSimulation(ToolCallTestCalls.Script(), time);
        var group = new ToolCallGroup { Simulation = sim };
        sim.State.ShouldBe(ToolCallSimulationState.Idle, "it starts once the group is on screen");
        var window = TestHelpers.Show(group);
        try
        {
            sim.State.ShouldBe(ToolCallSimulationState.Working);
            group.Shown.Count.ShouldBe(0);
            time.Advance(631);
            group.Shown.ShouldBe(sim.Items);
            group.Shown.Count.ShouldBe(1);
            group.HeaderText.ShouldBe("Using 1 tool");
            group.IsExpanded.ShouldBeTrue();
            time.Advance(1180 + 225);
            group.Shown.Count.ShouldBe(2);
            group.HeaderText.ShouldBe("Using 2 tools", "the simulation still works");
            time.Advance(760 + 225 + 1400);
            sim.State.ShouldBe(ToolCallSimulationState.WaitingForRetry);
            group.FailedCount.ShouldBe(1);
            group.RowOf(group.Shown[2])!.Act(ToolCall.RetryRequestedEvent);
            group.Shown[2].Status.ShouldBe(ToolCallStatus.Running);
        }
        finally
        {
            window.Close();
        }
        sim.State.ShouldBe(ToolCallSimulationState.Idle, "off screen the simulation stops");
    }

    [AvaloniaFact]
    public void App_Calls_Win_Over_The_Simulation()
    {
        var time = new AgentStatusTestClock();
        var sim = new ToolCallSimulation(ToolCallTestCalls.Script(), time);
        var group = new ToolCallGroup { Simulation = sim };
        group.Items.Add(ToolCallTestCalls.TicketSearch());
        var window = TestHelpers.Show(group);
        try
        {
            sim.State.ShouldBe(ToolCallSimulationState.Idle, "a simulation the group does not show is not started");
            time.Advance(5_000);
            group.Shown.Count.ShouldBe(1);
            group.Shown[0].Id.ShouldBe("tickets");
            group.HeaderText.ShouldBe("Used 1 tool 1.2s");
            group.Items.Clear();
            sim.State.ShouldBe(ToolCallSimulationState.Working, "without its own calls the group plays the simulation");
            time.Advance(631);
            group.Shown.ShouldBe(sim.Items);
            group.ItemsSource = new[] { ToolCallTestCalls.ChecklistRead() };
            sim.State.ShouldBe(ToolCallSimulationState.Idle, "the app's source wins and the simulation stops");
            group.Shown.Single().Id.ShouldBe("checklist");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Clearing_The_Simulation_Stops_Playback()
    {
        var time = new AgentStatusTestClock();
        var sim = new ToolCallSimulation(ToolCallTestCalls.Script(), time);
        var group = new ToolCallGroup { Simulation = sim };
        var window = TestHelpers.Show(group);
        try
        {
            time.Advance(631);
            group.Shown.Count.ShouldBe(1);
            group.Simulation = null;
            sim.State.ShouldBe(ToolCallSimulationState.Idle);
            group.Shown.Count.ShouldBe(0);
            group.Classes.Contains(":empty").ShouldBeTrue();
            time.Advance(10_000);
            sim.Items.Count.ShouldBe(1, "the stopped simulation appends nothing more");
            group.Shown.Count.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Group_Auto_Collapses_After_Delay_Unless_Touched()
    {
        foreach (var touch in new[] { false, true })
        {
            var group = new ToolCallGroup { AutoCollapseDelay = TimeSpan.FromMilliseconds(40) };
            group.Items.Add(ToolCallTestCalls.TicketSearch(ToolCallStatus.Running));
            var window = TestHelpers.Show(group);
            try
            {
                group.IsExpanded.ShouldBeTrue();
                var events = new List<bool>();
                group.ExpandedChanged += (_, e) => events.Add(e.IsAutomatic);
                if (touch)
                {
                    group.RowControls.First().ToggleByUser();
                }
                group.Items[0].Status = ToolCallStatus.Success;
                Dispatcher.UIThread.RunJobs();
                group.IsCollapsePending.ShouldBe(!touch);
                var until = DateTime.UtcNow.AddSeconds(3);
                while (!touch && group.IsExpanded && DateTime.UtcNow < until)
                {
                    Thread.Sleep(10);
                    Dispatcher.UIThread.RunJobs();
                }
                group.IsExpanded.ShouldBe(touch);
                if (!touch)
                {
                    events.ShouldBe([true]);
                }
            }
            finally
            {
                window.Close();
            }
        }
    }

    [AvaloniaFact]
    public void Failure_Opens_A_Folded_Group()
    {
        var group = new ToolCallGroup { IsExpanded = false };
        group.Items.Add(ToolCallTestCalls.TicketSearch());
        var window = TestHelpers.Show(group);
        try
        {
            group.IsExpanded.ShouldBeFalse();
            var failing = ToolCallTestCalls.JobLogQuery(ToolCallStatus.Running);
            group.Items.Add(failing);
            failing.Status = ToolCallStatus.Error;
            Dispatcher.UIThread.RunJobs();
            group.IsExpanded.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Width_Switches_To_The_Compact_Layout()
    {
        var call = new ToolCall { Item = ToolCallTestCalls.TicketSearch(), Width = 380 };
        var window = TestHelpers.Show(call);
        try
        {
            window.Width = 600;
            Dispatcher.UIThread.RunJobs();
            call.IsCompact.ShouldBeTrue();
            call.MetaText.ShouldBe("1.2s");
            call.Width = 360;
            Dispatcher.UIThread.RunJobs();
            call.IsTight.ShouldBeTrue();
            call.MetaText.ShouldBeNull();
            call.Width = 600;
            Dispatcher.UIThread.RunJobs();
            call.IsCompact.ShouldBeFalse();
            call.MetaText.ShouldBe("4 results · 1.2s");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Open_Panel_Has_Input_And_Output_Sections_With_Copy_Buttons()
    {
        var call = new ToolCall { Item = ToolCallTestCalls.ChecklistRead(), IsExpanded = true };
        var window = TestHelpers.Show(call);
        try
        {
            var buttons = call.GetVisualDescendants().OfType<ToolCallCopyButton>().ToList();
            buttons.Select(b => AutomationProperties.GetName(b)).ShouldBe(["Copy input", "Copy output"]);
            buttons[0].Text!()!.ShouldContain("\"board\": \"March close\"");
            call.GetVisualDescendants().OfType<ToolCallFoldButton>().Single().Content.ShouldBeOfType<TextBlock>()
                .Text.ShouldBe("Show 25 more lines");
        }
        finally
        {
            window.Close();
        }
    }
}
