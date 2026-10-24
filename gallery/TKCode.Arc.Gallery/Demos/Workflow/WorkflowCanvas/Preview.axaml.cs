using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The demo is the automation: sample orders arrive on their own, and each one runs the workflow step by step. The demo
/// only sets each step's state when it would really change; the canvas draws the data along the edges in between.
/// </summary>
public partial class WorkflowCanvasPreviewDemo : UserControl
{
    private sealed record Order(string Customer, string Amount, string Plan, bool Routed, string Task,
        int Lookup, int Branch, int Notify, int Create, int Save, int Total);

    private static readonly Order[] s_orders =
    [
        new("Brightline Labs", "$4,800", "Enterprise, 212 seats", true, "TASK-482", 118, 2, 164, 231, 58, 412),
        new("Fernwood", "$240", "Team, 9 seats", false, "", 96, 1, 0, 0, 61, 171),
        new("Kestrel Health", "$12,600", "Enterprise, 540 seats", true, "TASK-483", 131, 2, 149, 244, 66, 436)
    ];

    private readonly WorkflowGraph _graph = new();
    private readonly WorkflowNode _trigger;
    private readonly WorkflowNode _lookup;
    private readonly WorkflowNode _branch;
    private readonly WorkflowNode _notify;
    private readonly WorkflowNode _task;
    private readonly WorkflowNode _save;
    private IDisposable? _step;
    private int _index = -1;
    private int _runs = 2417;

    public WorkflowCanvasPreviewDemo()
    {
        InitializeComponent();
        _trigger = Add(new WorkflowNode("trigger", "Order placed") { Icon = ArcIcons.Zap });
        _lookup = Add(new WorkflowNode("lookup", "Find customer account") { Description = "Plan, seats and owner", Icon = ArcIcons.Search });
        _branch = Add(new WorkflowNode("branch", "Amount over $1,000") { Description = "Condition", Icon = ArcIcons.GitBranch });
        _notify = Add(new WorkflowNode("notify", "Post to #revenue") { Description = "Team chat", Icon = ArcIcons.MessageSquare, Appearance = WorkflowNodeAppearance.Compact });
        _task = Add(new WorkflowNode("task", "Create onboarding task") { Description = "Onboarding team", Icon = ArcIcons.ListChecks, Appearance = WorkflowNodeAppearance.Compact });
        _save = Add(new WorkflowNode("save", "Save to warehouse") { Description = "Postgres", Icon = ArcIcons.Database, Appearance = WorkflowNodeAppearance.Compact });
        _graph.Connect(_trigger, _lookup);
        _graph.Connect(_lookup, _branch);
        _graph.Connect(_branch, _notify);
        _graph.Connect(_branch, _task);
        _graph.Connect(_branch, _save);
        Canvas.Graph = _graph;
        SendButton.Click += (_, _) => NextOrder(at: 0);
        Prepare(s_orders[0]);
    }

    private WorkflowNode Add(WorkflowNode node)
    {
        _graph.Nodes.Add(node);
        return node;
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        NextOrder(at: 900);
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        // The run pauses off screen.
        _step?.Dispose();
        _step = null;
    }

    /// <summary>Starts the next sample order after <paramref name="at"/> ms of waiting.</summary>
    private void NextOrder(int at)
    {
        _step?.Dispose();
        _index = (_index + 1) % s_orders.Length;
        var order = s_orders[_index];
        Prepare(order);
        RunCount.Text = $"Run {++_runs:N0}";
        Summary.Text = "Waiting for the next event";
        if (!ArcMotion.IsFull(this))
        {
            // Reduced motion: show each order as its finished run without playing it.
            Finish(order);
            return;
        }
        // Each step runs for its own time; the canvas spends about 400ms drawing the data to the next one.
        Then(at, () =>
        {
            Summary.Text = "Running";
            _trigger.State = WorkflowNodeState.Running;
            Then(420, () =>
            {
                _trigger.State = WorkflowNodeState.Done;
                _lookup.State = WorkflowNodeState.Running;
                Then(400 + 640, () =>
                {
                    _lookup.Result = order.Plan;
                    _lookup.Duration = TimeSpan.FromMilliseconds(order.Lookup);
                    _lookup.State = WorkflowNodeState.Done;
                    _branch.State = WorkflowNodeState.Running;
                    Then(400 + 360, () =>
                    {
                        Branch(order);
                        Then(400 + 760, () =>
                        {
                            Finish(order);
                            Then(2800, () => NextOrder(at: 900));
                        });
                    });
                });
            });
        });
    }

    private void Then(int milliseconds, Action action)
        => _step = DispatcherTimer.RunOnce(action, TimeSpan.FromMilliseconds(milliseconds));

    private void Prepare(Order order)
    {
        _graph.ResetRun();
        _trigger.Description = $"{order.Customer}, {order.Amount}";
    }

    /// <summary>The branch decides: a small order skips the chat post and the task.</summary>
    private void Branch(Order order)
    {
        _branch.Result = order.Routed ? "Yes, alert the team" : "No, record only";
        _branch.Duration = TimeSpan.FromMilliseconds(order.Branch);
        _branch.State = WorkflowNodeState.Done;
        _notify.State = order.Routed ? WorkflowNodeState.Running : WorkflowNodeState.Skipped;
        _task.State = order.Routed ? WorkflowNodeState.Running : WorkflowNodeState.Skipped;
        _save.State = WorkflowNodeState.Running;
    }

    private void Finish(Order order)
    {
        _trigger.State = WorkflowNodeState.Done;
        _lookup.Result = order.Plan;
        _lookup.Duration = TimeSpan.FromMilliseconds(order.Lookup);
        _lookup.State = WorkflowNodeState.Done;
        if (_branch.State != WorkflowNodeState.Done)
        {
            Branch(order);
        }
        if (order.Routed)
        {
            _notify.Result = $"Sent in {order.Notify} ms";
            _notify.Duration = TimeSpan.FromMilliseconds(order.Notify);
            _notify.State = WorkflowNodeState.Done;
            _task.Result = $"{order.Task} in {order.Create} ms";
            _task.Duration = TimeSpan.FromMilliseconds(order.Create);
            _task.State = WorkflowNodeState.Done;
        }
        _save.Result = $"Saved in {order.Save} ms";
        _save.Duration = TimeSpan.FromMilliseconds(order.Save);
        _save.State = WorkflowNodeState.Done;
        Summary.Text = $"Completed in {order.Total} ms";
    }
}
