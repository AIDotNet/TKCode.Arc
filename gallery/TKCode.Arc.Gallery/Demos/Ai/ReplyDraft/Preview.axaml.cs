using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>The default demo: the self-playing tour, the on-demand state, a failing generator and the card with no data.</summary>
public partial class ReplyDraftPreviewDemo : UserControl
{
    private readonly bool _ready;

    public ReplyDraftPreviewDemo()
    {
        InitializeComponent();
        _ready = true;
        Show("tour");
    }

    private void OnScenarioChanged(object? sender, SelectionChangedEventArgs e)
    {
        // The control applies SelectedValue while the XAML loads, before the constructor body runs.
        if (_ready)
        {
            Show(Scenario.SelectedValue as string ?? "tour");
        }
    }

    private void Show(string scenario)
    {
        Host.Child = scenario switch
        {
            "demand" => new ReplyDraft { AutoStart = false, AutoPlay = false, Simulation = ReplyDraftDemoData.Simulation() },
            // The demo's email as your own data, with a generator that fails.
            "error" => new ReplyDraft
            {
                Message = ReplyDraftDemoData.Message,
                Suggestions = ReplyDraftDemoData.Suggestions,
                Model = ReplyDraftDemoData.Model,
                Generator = Failing,
                IsSimulated = true,
            },
            "empty" => new ReplyDraft(),
            _ => new ReplyDraft { Simulation = ReplyDraftDemoData.Simulation() },
        };
    }

    // A generator that reads for a moment and then fails, as a dropped connection would.
    private static async IAsyncEnumerable<string> Failing(ReplyDraftRequest request)
    {
        await Task.Delay(700, request.CancellationToken);
        yield return "Hi Maya, ";
        await Task.Delay(300, request.CancellationToken);
        throw new InvalidOperationException("The model stopped responding.");
    }
}
