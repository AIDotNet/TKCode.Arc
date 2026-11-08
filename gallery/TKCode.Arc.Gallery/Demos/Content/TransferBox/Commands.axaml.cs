using System.Collections.ObjectModel;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class TransferBoxCommandsDemo : UserControl
{
    public TransferBoxCommandsDemo()
    {
        InitializeComponent();
        var visible = new ObservableCollection<string> { "Name", "Status", "Owner", "Updated" };
        Box.SourceItems = new ObservableCollection<string> { "Created", "Priority", "Due date", "Labels", "Estimate" };
        Box.TargetItems = visible;
        Box.ItemsTransferred += (_, e) =>
        {
            var verb = e.Direction == TransferDirection.ToTarget ? "Shown" : "Hidden";
            Log.Text = $"{verb}: {string.Join(", ", e.Items)}. Visible columns: {string.Join(", ", visible)}.";
        };
    }
}
