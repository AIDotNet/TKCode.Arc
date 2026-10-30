using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>The default demo: the simulated inbox live, a classifier that fails once, a queue that runs out and the empty state.</summary>
public partial class TicketRouterPreviewDemo : UserControl
{
    private readonly bool _ready;

    public TicketRouterPreviewDemo()
    {
        InitializeComponent();
        _ready = true;
    }

    private void OnScenarioChanged(object? sender, SelectionChangedEventArgs e)
    {
        // The control applies SelectedValue while the XAML loads, before the router exists.
        if (_ready)
        {
            Show(Scenario.SelectedValue as string ?? "live");
        }
    }

    private void Show(string scenario)
    {
        Router.IsPaused = false;
        switch (scenario)
        {
            case "error":
                // A demo classifier: it reads the simulation's answers after a short wait and fails on the second ticket.
                var calls = 0;
                Router.Loop = true;
                Router.Classifier = async (message, token) =>
                {
                    await Task.Delay(700, token);
                    calls++;
                    if (calls == 2)
                    {
                        throw new InvalidOperationException("The classifier timed out.");
                    }
                    return message.Sample ?? new TicketClassification(TicketIntent.Question, TicketSentiment.Neutral, 0.5);
                };
                Router.InitialRouted = null;
                Router.Tickets = null;
                break;
            case "caughtup":
                Router.Classifier = null;
                Router.Loop = false;
                Router.InitialRouted = [];
                Router.Tickets = [.. TicketRouterDemoData.Tickets.Take(3)];
                break;
            case "empty":
                // No tickets of your own: the router rests on "No tickets yet" in the default lanes.
                Router.Classifier = null;
                Router.Loop = null;
                Router.InitialRouted = [];
                Router.Tickets = [];
                break;
            default:
                Router.Classifier = null;
                Router.Loop = null;
                Router.InitialRouted = null;
                Router.Tickets = null;
                break;
        }
        Router.Restart();
    }
}
