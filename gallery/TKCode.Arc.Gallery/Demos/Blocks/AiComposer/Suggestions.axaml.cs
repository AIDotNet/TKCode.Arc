using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class AiComposerSuggestionsDemo : UserControl
{
    public AiComposerSuggestionsDemo()
    {
        InitializeComponent();
        AiComposerDemoData.Fill(Composer, models: false, tools: false);
        Composer.Placeholder = AiComposerDemoData.Placeholder;

        // SetPrompt fills the box, turns the commands and mentions it knows into tokens and focuses the text.
        First.Click += (_, _) => Composer.SetPrompt("/recap @Sprint 15 review and list what slipped since the previous sprint");
        Second.Click += (_, _) => Composer.SetPrompt("/rewrite our tester welcome email so it fits on one screen");
        Third.Click += (_, _) => Composer.SetPrompt("/debug @export-worker.ts large CSV exports stall at the last chunk");
    }
}
