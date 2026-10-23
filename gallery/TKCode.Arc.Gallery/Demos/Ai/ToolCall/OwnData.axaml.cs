using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.Demos;

public partial class ToolCallOwnDataDemo : UserControl
{
    private readonly List<string> _entries = new();
    private int _count;

    public ToolCallOwnDataDemo()
    {
        InitializeComponent();
        Reset();
    }

    private void Reset()
    {
        Group.Items.Clear();
        _count = 0;
        Add(ToolCallStatus.Success);
        Add(ToolCallStatus.Error);
        Add(ToolCallStatus.Approval);
        Write("Started over.");
    }

    private void Add(ToolCallStatus status)
    {
        _count++;
        var item = new ToolCallItem($"call-{_count}", status == ToolCallStatus.Approval ? "Deploy" : "Build",
            status == ToolCallStatus.Approval ? "Rolled out build 42" : "Ran the unit tests", status,
            status == ToolCallStatus.Approval ? ArcIcons.Server : ArcIcons.Terminal)
        {
            RunningTitle = status == ToolCallStatus.Approval ? "Rolling out build 42" : "Running the unit tests",
            ApprovalTitle = "Roll out build 42 to production",
            ErrorTitle = "Unit tests failed",
            Message = status == ToolCallStatus.Approval ? "Replaces the running version for every customer" : "3 of 212 tests failed in checkout",
            Summary = "212 tests",
            Duration = TimeSpan.FromSeconds(8.4)
        };
        item.Input.Add(new ToolCallArgument("target", status == ToolCallStatus.Approval ? "production" : "all"));
        Group.Items.Add(item);
    }

    private void Write(string line)
    {
        _entries.Add(line);
        if (_entries.Count > 3)
        {
            _entries.RemoveAt(0);
        }
        Log.Text = string.Join("  ·  ", _entries);
    }

    private void Run(ToolCallItem item, ToolCallStatus to, int milliseconds)
    {
        item.Status = ToolCallStatus.Running;
        DispatcherTimer.RunOnce(() => item.Status = to, TimeSpan.FromMilliseconds(milliseconds));
    }

    private void OnApprove(object? sender, ToolCallActionEventArgs e)
    {
        e.Handled = true;
        Write($"Allow: {e.Item.Id}");
        Run(e.Item, ToolCallStatus.Success, 1500);
    }

    private void OnDeny(object? sender, ToolCallActionEventArgs e)
    {
        e.Handled = true;
        Write($"Deny: {e.Item.Id}");
        e.Item.Status = ToolCallStatus.Denied;
    }

    private void OnRetry(object? sender, ToolCallActionEventArgs e)
    {
        e.Handled = true;
        Write($"Retry: {e.Item.Id}");
        Run(e.Item, ToolCallStatus.Success, 1200);
    }

    private void OnExpanded(object? sender, ToolCallExpandedChangedEventArgs e) =>
        Write(e.IsAutomatic ? $"Group {(e.IsExpanded ? "opened" : "folded")} by itself" : $"Group {(e.IsExpanded ? "opened" : "closed")}");

    private void OnAdd(object? sender, RoutedEventArgs e)
    {
        Add(ToolCallStatus.Success);
        Write("Added a finished call");
    }

    private void OnReset(object? sender, RoutedEventArgs e) => Reset();
}
