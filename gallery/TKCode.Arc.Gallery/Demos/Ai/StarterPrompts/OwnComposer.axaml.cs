using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>Cards paired with the app's own composer.</summary>
public partial class StarterPromptsOwnComposerDemo : UserControl
{
    public StarterPromptsOwnComposerDemo()
    {
        InitializeComponent();
        StarterPromptsDemoData.AddStarters(Prompts);
        Prompts.PromptSelected += (_, e) =>
        {
            Composer.Text = e.Prompt.Prompt;
            Composer.Focus();
            Composer.CaretIndex = Composer.Text?.Length ?? 0;
            Picked.Text = $"Picked “{e.Prompt.Title}” (id: {e.Prompt.Id}).";
        };
    }
}
