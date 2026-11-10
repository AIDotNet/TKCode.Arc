using Avalonia.Controls;
using Avalonia.Interactivity;
using K = TKCode.Arc.OrchestratorMessageKind;

namespace TKCode.Arc.Gallery.Demos;

public partial class AgentOrchestratorProgrammaticDemo : UserControl
{
    private static readonly (string From, string To, OrchestratorMessageKind Kind, string Text)[] s_messages =
    [
        ("lead", "venue", K.Delegate, "Find three venues near the lake for 14 people"),
        ("lead", "budget", K.Delegate, "Keep the whole trip under 9,000 dollars"),
        ("venue", "lead", K.Progress, "Two venues have rooms on the dates"),
        ("budget", "venue", K.Question, "What does each venue charge a night?"),
        ("venue", "budget", K.Answer, "190 and 240 dollars a room"),
        ("venue", "lead", K.Result, "Pinewater Lodge: 14 rooms, a meeting hall, 190 a night"),
        ("budget", "lead", K.Result, "Total 8,400 dollars with meals and the bus"),
    ];

    private int _next;

    public AgentOrchestratorProgrammaticDemo()
    {
        InitializeComponent();
    }

    private void OnNext(object? sender, RoutedEventArgs e)
    {
        if (_next < s_messages.Length)
        {
            var (from, to, kind, text) = s_messages[_next++];
            Orchestrator.Post(new OrchestratorMessage { From = from, To = to, Kind = kind, Text = text });
            if (_next == 2)
            {
                Orchestrator.SetStatus("lead", OrchestratorAgentStatus.Waiting, "Waiting on two agents");
            }
        }
        else if (Orchestrator.RunState == OrchestratorRunState.Running)
        {
            Orchestrator.SetStatus("lead", OrchestratorAgentStatus.Done, "Plan sent to the team");
            Orchestrator.Complete();
        }
        NextButton.IsEnabled = Orchestrator.RunState != OrchestratorRunState.Completed;
    }

    private void OnReset(object? sender, RoutedEventArgs e)
    {
        Orchestrator.Reset();
        _next = 0;
        NextButton.IsEnabled = true;
    }
}
