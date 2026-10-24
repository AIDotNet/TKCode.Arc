using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>A nightly data pipeline: three sources merge, get checked, and fan out to a warehouse, a dashboard and alerts.</summary>
public partial class WorkflowCanvasPanZoomDemo : UserControl
{
    public WorkflowCanvasPanZoomDemo()
    {
        InitializeComponent();
        var graph = new WorkflowGraph();
        WorkflowNode Add(string id, string title, string description, object icon, bool compact = false)
        {
            var node = new WorkflowNode(id, title)
            {
                Description = description,
                Icon = icon,
                Appearance = compact ? WorkflowNodeAppearance.Compact : WorkflowNodeAppearance.Standard
            };
            graph.Nodes.Add(node);
            return node;
        }
        var schedule = Add("schedule", "Every night at 02:00", "Schedule", ArcIcons.Clock);
        var orders = Add("orders", "Export orders", "Postgres", ArcIcons.Database, compact: true);
        var events = Add("events", "Export events", "Event stream", ArcIcons.Activity, compact: true);
        var accounts = Add("accounts", "Export accounts", "Billing API", ArcIcons.CreditCard, compact: true);
        var merge = Add("merge", "Join on account", "3 sources", ArcIcons.Layers);
        var check = Add("check", "Rows look right?", "Condition", ArcIcons.ShieldCheck);
        var load = Add("load", "Load warehouse", "Daily tables", ArcIcons.Server, compact: true);
        var refresh = Add("refresh", "Refresh dashboard", "Revenue", ArcIcons.ChartLine, compact: true);
        var alert = Add("alert", "Alert data team", "On-call", ArcIcons.Bell, compact: true);
        graph.Connect(schedule, orders);
        graph.Connect(schedule, events);
        graph.Connect(schedule, accounts);
        graph.Connect(orders, merge);
        graph.Connect(events, merge);
        graph.Connect(accounts, merge);
        graph.Connect(merge, check);
        graph.Connect(check, load);
        graph.Connect(check, refresh);
        graph.Connect(check, alert);
        Canvas.Graph = graph;
    }
}
