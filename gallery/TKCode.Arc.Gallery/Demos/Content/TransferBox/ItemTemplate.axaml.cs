using System.Collections.ObjectModel;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class TransferBoxItemTemplateDemo : UserControl
{
    public TransferBoxItemTemplateDemo()
    {
        InitializeComponent();
        var available = TransferBoxDemoData.Permissions();
        var granted = new ObservableCollection<TransferBoxPermission> { available[0], available[1] };
        available.RemoveAt(1);
        available.RemoveAt(0);
        // The template draws the rows; the selector still names each one for screen readers.
        Box.ItemTextSelector = item => ((TransferBoxPermission)item!).Name;
        Box.SourceItems = available;
        Box.TargetItems = granted;
    }
}
