using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>The demo starters in a narrow column.</summary>
public partial class StarterPromptsNarrowDemo : UserControl
{
    public StarterPromptsNarrowDemo()
    {
        InitializeComponent();
        StarterPromptsDemoData.AddStarters(Prompts);
    }
}
