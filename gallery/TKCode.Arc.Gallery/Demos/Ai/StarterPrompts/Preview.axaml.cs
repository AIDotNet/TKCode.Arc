using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>The default demo: the demo starters, the placeholder cards while they load, and the empty control.</summary>
public partial class StarterPromptsPreviewDemo : UserControl
{
    private readonly bool _ready;

    public StarterPromptsPreviewDemo()
    {
        InitializeComponent();
        // The starters are a list the demos share, so they are added here rather than written out in XAML.
        StarterPromptsDemoData.AddStarters(Prompts);
        _ready = true;
    }

    private void OnStateChanged(object? sender, SelectionChangedEventArgs e)
    {
        // The segmented control applies SelectedValue while the XAML loads, before Prompts is assigned.
        if (!_ready)
        {
            return;
        }
        var state = State.SelectedValue as string;
        Prompts.IsLoading = state == "loading";
        if (state == "empty")
        {
            Prompts.Prompts.Clear();
            Prompts.Categories.Clear();
            Prompts.Greeting = null;
            Prompts.Description = null;
            Prompts.Placeholder = null;
            Prompts.ModelName = null;
        }
        else if (Prompts.Prompts.Count == 0)
        {
            StarterPromptsDemoData.AddStarters(Prompts);
            Prompts.Greeting = StarterPromptsDemoData.Greeting;
            Prompts.Description = StarterPromptsDemoData.Description;
            Prompts.Placeholder = StarterPromptsDemoData.Placeholder;
            Prompts.ModelName = StarterPromptsDemoData.ModelName;
        }
    }
}
