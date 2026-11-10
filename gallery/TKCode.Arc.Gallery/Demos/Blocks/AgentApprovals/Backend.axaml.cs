using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class AgentApprovalsBackendDemo : UserControl
{
    public AgentApprovalsBackendDemo()
    {
        InitializeComponent();
        Queue.DecisionHandler = async (request, decision, note, cancellation) =>
        {
            // A stand-in for your API call.
            await Task.Delay(800, cancellation);
        };
        Log.Text = "Approve or deny a request.";
        Queue.Decided += (_, e) => Log.Text = $"Decided: {e.Request.Id} {e.Decision}{(e.Reason is null ? null : $" (\"{e.Reason}\")")}";
        Reset.Click += (_, _) => Queue.Reset();
    }
}
