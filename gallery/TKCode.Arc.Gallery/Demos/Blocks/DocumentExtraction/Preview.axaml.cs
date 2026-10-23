using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class DocumentExtractionPreviewDemo : UserControl
{
    public DocumentExtractionPreviewDemo()
    {
        InitializeComponent();
        Extraction.Samples.AddRange(DocumentExtractionDemoData.Samples());
        Extraction.SelectedSample = Extraction.Samples[0];
        Extraction.Model = DocumentExtractionDemoData.Model();

        // The model's answers come from your extractor; this one plays scripted events.
        Extraction.Extractor = DocumentExtractionDemoData.Extractor();

        // Accepting hands you the typed output; save it here (throw to refuse).
        Extraction.Accepting += (_, e) => e.Defer(Task.Delay(550));
    }
}
