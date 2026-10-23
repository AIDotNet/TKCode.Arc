using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ModelArenaPreviewDemo : UserControl
{
    public ModelArenaPreviewDemo()
    {
        InitializeComponent();
        Arena.Contestants.AddRange(ModelArenaDemoData.Models());
        Arena.Prompts.AddRange(ModelArenaDemoData.Prompts());
        ModelArenaDemoData.SeedStandings(Arena);

        // Answers come from your runner; this one plays scripted text.
        Arena.Runner = new ModelArenaDemoData.Runner();
    }
}
