using System.Collections.ObjectModel;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary><see cref="AgentApprovals"/>: selection, the high-risk check, decisions, the backend handler and the keyboard.</summary>
public class AgentApprovalsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 10, 0, 0, TimeSpan.Zero);

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static (AgentApprovals Queue, IReadOnlyList<AgentApprovalRequest> Requests, Window Window) Show(double width = 900)
    {
        var requests = AgentApprovalsSamples.Requests(Now);
        var queue = new AgentApprovals { Width = width, Requests = requests, Clock = () => Now };
        var window = TestHelpers.Show(queue);
        window.Width = 1000;
        Settle(window);
        return (queue, requests, window);
    }

    [AvaloniaFact]
    public void First_Request_Is_Selected_And_Read_Out()
    {
        var (queue, requests, _) = Show();
        queue.SelectedRequest.ShouldBe(requests[0]);
        queue.PendingCount.ShouldBe(4);
        AutomationProperties.GetName(queue.RowOf(requests[0])!).ShouldBe($"{requests[0].Title}, Ledger, High risk");
        queue.RowOf(requests[0])!.IsSelected.ShouldBeTrue();
    }

    [AvaloniaFact]
    public async Task High_Risk_Approval_Needs_The_Extra_Check()
    {
        var (queue, requests, window) = Show();
        queue.Classes.ShouldContain(":high-risk");
        queue.ApproveButton!.IsEnabled.ShouldBeFalse();
        await queue.ApproveAsync();
        queue.PendingCount.ShouldBe(4);
        queue.ConfirmBox!.IsChecked = true;
        Settle(window);
        queue.ApproveButton.IsEnabled.ShouldBeTrue();
        AgentApprovalDecidedEventArgs? decided = null;
        queue.Decided += (_, e) => decided = e;
        await queue.ApproveAsync();
        decided!.Request.ShouldBe(requests[0]);
        decided.Decision.ShouldBe(AgentApprovalDecision.Approved);
        queue.PendingCount.ShouldBe(3);
        queue.SelectedRequest.ShouldBe(requests[1]);
        queue.Announcer!.Announcement.ShouldBe($"Approved: {requests[0].Title}");
    }

    [AvaloniaFact]
    public async Task Deny_Carries_The_Note_And_The_Last_Decision_Empties_The_Queue()
    {
        var (queue, requests, window) = Show();
        var reasons = new List<string?>();
        queue.Decided += (_, e) => reasons.Add(e.Reason);
        queue.ReasonBox!.Text = "  Wait for the new price list  ";
        await queue.DenyAsync();
        reasons.ShouldBe(["Wait for the new price list"]);
        queue.ReasonBox.Text.ShouldBe(string.Empty);
        for (var i = 0; i < 3; i++)
        {
            await queue.DenyAsync();
        }
        Settle(window);
        queue.PendingCount.ShouldBe(0);
        queue.SelectedRequest.ShouldBeNull();
        queue.Classes.ShouldContain(":empty");
        requests.Count.ShouldBe(4); // the app's list is never changed
        queue.Reset();
        queue.PendingCount.ShouldBe(4);
    }

    [AvaloniaFact]
    public async Task A_Failing_Handler_Keeps_The_Request_And_Shows_The_Message()
    {
        var (queue, requests, window) = Show();
        queue.SelectedRequest = requests[3];
        queue.DecisionHandler = (_, _, _, _) => throw new InvalidOperationException("server unreachable");
        var decided = 0;
        queue.Decided += (_, _) => decided++;
        await queue.ApproveAsync();
        Settle(window);
        decided.ShouldBe(0);
        queue.PendingCount.ShouldBe(4);
        queue.StatusLine!.Text.ShouldBe("Could not send: server unreachable");
        queue.DecisionHandler = (_, _, _, _) => Task.CompletedTask;
        await queue.ApproveAsync();
        decided.ShouldBe(1);
    }

    [AvaloniaFact]
    public void Arrow_Keys_Move_The_Selection()
    {
        var (queue, requests, window) = Show();
        queue.RowOf(requests[0])!.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        Settle(window);
        queue.SelectedRequest.ShouldBe(requests[1]);
        queue.RowOf(requests[1])!.IsFocused.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
        Settle(window);
        queue.SelectedRequest.ShouldBe(requests[3]);
    }

    [AvaloniaFact]
    public void New_Requests_In_The_Collection_Join_The_Queue()
    {
        var requests = new ObservableCollection<AgentApprovalRequest>();
        var queue = new AgentApprovals { Width = 900, Requests = requests };
        var window = TestHelpers.Show(queue);
        Settle(window);
        queue.Classes.ShouldContain(":empty");
        requests.Add(new AgentApprovalRequest { Title = "Send it", Kind = AgentApprovalKind.Email });
        Settle(window);
        queue.PendingCount.ShouldBe(1);
        queue.SelectedRequest.ShouldBe(requests[0]);
    }

    [AvaloniaFact]
    public void Narrow_Queue_Is_Compact()
    {
        var (queue, _, _) = Show(500);
        queue.IsCompact.ShouldBeTrue();
    }
}
