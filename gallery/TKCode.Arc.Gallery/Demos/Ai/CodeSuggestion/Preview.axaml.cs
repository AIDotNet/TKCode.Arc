using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>The default demo: a simulated suggestion, one to review, a failed one and the empty card.</summary>
public partial class CodeSuggestionPreviewDemo : UserControl
{
    private readonly CodeSuggestionSimulation _simulation;
    private readonly bool _ready;

    public CodeSuggestionPreviewDemo()
    {
        InitializeComponent();
        _simulation = Card.Simulation!;
        Card.RegenerateRequested += OnRegenerate;
        _ready = true;
    }

    private void OnScenarioChanged(object? sender, SelectionChangedEventArgs e)
    {
        // The control applies SelectedValue while the XAML loads, before the constructor has finished.
        if (_ready)
        {
            Show(Scenario.SelectedValue as string ?? "live");
        }
    }

    private void Show(string scenario)
    {
        switch (scenario)
        {
            case "review":
                CodeSuggestionDemoData.Show(Card);
                Card.AllowsRegenerate = true;
                Card.Status = CodeSuggestionStatus.Ready;
                break;
            case "failed":
                CodeSuggestionDemoData.Show(Card);
                Card.AllowsRegenerate = true;
                Card.Status = CodeSuggestionStatus.Error;
                break;
            case "empty":
                // No code, no hunks and no simulation: an empty file.
                Clear();
                Card.ClearValue(CodeSuggestion.FileNameProperty);
                Card.ClearValue(CodeSuggestion.FilePathProperty);
                Card.ClearValue(CodeSuggestion.InstructionProperty);
                Card.ClearValue(CodeSuggestion.ModelProperty);
                Card.Simulation = null;
                break;
            default:
                // No code and no hunks: the card plays its simulation from the top.
                CodeSuggestionDemoData.ShowSimulated(Card);
                Clear();
                Card.Simulation = _simulation;
                break;
        }
    }

    private void Clear()
    {
        Card.Hunks.Clear();
        Card.ClearValue(CodeSuggestion.CodeProperty);
        Card.ClearValue(CodeSuggestion.StatusProperty);
        Card.ClearValue(CodeSuggestion.AllowsRegenerateProperty);
    }

    private void OnRegenerate(object? sender, RoutedEventArgs e)
    {
        // A real app asks its model again; the demo shows the same suggestion afresh (thinking clears the decisions).
        Card.Status = CodeSuggestionStatus.Thinking;
        Card.Status = CodeSuggestionStatus.Ready;
        Scenario.SelectedValue = "review";
    }
}
