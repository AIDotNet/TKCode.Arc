using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>The tool call controls: expand state, keyboard, actions and the group's header and auto collapse.</summary>
public class ToolCallTests
{
    private static ToolCallGroup Turn(ToolCallStatus last = ToolCallStatus.Approval)
    {
        var group = new ToolCallGroup();
        group.Items.AddRange([ToolCallSamples.TicketSearch(), ToolCallSamples.ChecklistRead(), ToolCallSamples.JobLogQuery(),
            ToolCallSamples.StatusNote(last)]);
        return group;
    }

    [AvaloniaFact]
    public void Lone_Call_Shows_The_Sample_And_Toggles()
    {
        var call = new ToolCall();
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
        var item = ToolCallSamples.JobLogQuery(ToolCallStatus.Running);
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
        var item = ToolCallSamples.StatusNote();
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
    public void Allow_Is_Simulated_Only_For_Sample_Data()
    {
        var custom = new ToolCallItem("x", "Tool", "Did it", ToolCallStatus.Approval);
        var call = new ToolCall { Item = custom };
        var window = TestHelpers.Show(call);
        try
        {
            call.Act(ToolCall.ApproveRequestedEvent);
            custom.Status.ShouldBe(ToolCallStatus.Approval);
            var sample = ToolCallSamples.StatusNote();
            call.Item = sample;
            call.Act(ToolCall.ApproveRequestedEvent);
            sample.Status.ShouldBe(ToolCallStatus.Running);
            call.Announcer!.History.Last().ShouldContain("only simulated");
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
    public void Empty_Group_Shows_Sample_And_Starts_Folded()
    {
        var group = new ToolCallGroup();
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
    public void Group_Auto_Collapses_After_Delay_Unless_Touched()
    {
        foreach (var touch in new[] { false, true })
        {
            var group = new ToolCallGroup { AutoCollapseDelay = TimeSpan.FromMilliseconds(40) };
            group.Items.Add(ToolCallSamples.TicketSearch(ToolCallStatus.Running));
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
        group.Items.Add(ToolCallSamples.TicketSearch());
        var window = TestHelpers.Show(group);
        try
        {
            group.IsExpanded.ShouldBeFalse();
            var failing = ToolCallSamples.JobLogQuery(ToolCallStatus.Running);
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
        var call = new ToolCall { Item = ToolCallSamples.TicketSearch(), Width = 380 };
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
        var call = new ToolCall { Item = ToolCallSamples.ChecklistRead(), IsExpanded = true };
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
