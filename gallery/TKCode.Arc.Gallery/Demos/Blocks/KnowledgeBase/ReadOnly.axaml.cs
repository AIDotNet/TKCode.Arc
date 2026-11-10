using System.Collections.ObjectModel;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class KnowledgeBaseReadOnlyDemo : UserControl
{
    public KnowledgeBaseReadOnlyDemo()
    {
        InitializeComponent();
        // Every sample document indexed, so nothing runs in the background.
        Knowledge.DocumentsSource = new ObservableCollection<KnowledgeDocument>(KnowledgeBaseSamples.IndexedDocuments());
    }
}
