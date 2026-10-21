using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ReorderableListPrioritiesDemo : UserControl
{
    public ReorderableListPrioritiesDemo()
    {
        InitializeComponent();
    }

    private void OnReordered(object? sender, ReorderableListReorderedEventArgs e)
    {
        var order = Priorities.Items.OfType<ReorderableListEntry>().Select(p => p.Id);
        Status.Text = $"Moved “{(e.Item as ReorderableListEntry)?.Title}” from {e.OldIndex + 1} to {e.NewIndex + 1}. Order: {string.Join(", ", order)}";
    }
}
