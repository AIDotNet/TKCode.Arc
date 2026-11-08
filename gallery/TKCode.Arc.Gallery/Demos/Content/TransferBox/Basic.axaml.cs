using System.Collections.ObjectModel;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class TransferBoxBasicDemo : UserControl
{
    public TransferBoxBasicDemo()
    {
        InitializeComponent();
        // In an app these are view model collections bound with {Binding}; moving edits them in place.
        Box.SourceItems = TransferBoxDemoData.Languages();
        Box.TargetItems = new ObservableCollection<string> { "Python", "Go" };
    }
}
