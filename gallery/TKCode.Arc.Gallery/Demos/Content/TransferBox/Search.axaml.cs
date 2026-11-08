using System.Collections.ObjectModel;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class TransferBoxSearchDemo : UserControl
{
    public TransferBoxSearchDemo()
    {
        InitializeComponent();
        // The text selector names a member for the rows, the search filter and screen readers, without reflection.
        Box.ItemTextSelector = item => ((TransferBoxMember)item!).Name;
        Box.SourceItems = TransferBoxDemoData.Members();
        Box.TargetItems = new ObservableCollection<TransferBoxMember>();
    }
}
