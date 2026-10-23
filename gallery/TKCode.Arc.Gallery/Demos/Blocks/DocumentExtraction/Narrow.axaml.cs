using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class DocumentExtractionNarrowDemo : UserControl
{
    public DocumentExtractionNarrowDemo()
    {
        InitializeComponent();
        Extraction.Samples.AddRange(DocumentExtractionDemoData.Samples());
        Extraction.SelectedSample = Extraction.Samples[0];
        Extraction.Model = DocumentExtractionDemoData.Model();
        Extraction.Extractor = DocumentExtractionDemoData.Extractor();
    }
}
