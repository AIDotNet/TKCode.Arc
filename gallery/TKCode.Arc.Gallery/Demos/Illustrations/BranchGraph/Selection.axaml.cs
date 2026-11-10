using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class BranchGraphSelectionDemo : UserControl
{
    public BranchGraphSelectionDemo()
    {
        InitializeComponent();
        Graph.PropertyChanged += (_, e) =>
        {
            if (e.Property == BranchGraph.SelectedCommitProperty)
            {
                Info.Text = Graph.SelectedCommit is { } commit
                    ? $"{commit.Id} on {commit.Branch}: “{commit.Message}”. Its history stays lit."
                    : "Click a commit, or focus the graph and use the arrow keys.";
            }
        };
    }
}
