using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>A summarizer whose first request fails, so the card shows its alert and Try again.</summary>
public partial class TldrToggleFailureDemo : UserControl
{
    private readonly TldrToggleSimulation _simulation = TldrToggleDemoData.Simulation();
    private int _attempts;

    public TldrToggleFailureDemo()
    {
        InitializeComponent();
        Card.Article = TldrToggleDemoData.Article;
        Card.Model = TldrToggleDemoData.Model;
        // The demo's written summaries stand in for a model; every other request fails after the thinking pause.
        Card.Summarizer = request => _simulation.Summarize(request, fail: _attempts++ % 2 == 0);
    }
}
