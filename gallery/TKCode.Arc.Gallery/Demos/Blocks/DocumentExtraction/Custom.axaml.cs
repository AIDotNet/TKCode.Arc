using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class DocumentExtractionCustomDemo : UserControl
{
    public DocumentExtractionCustomDemo()
    {
        InitializeComponent();
        var schema = new ExtractionSchema { Name = "parcel", Title = "Parcel" };
        schema.Fields.Add(new ExtractionField { Key = "carrier", Label = "Carrier", Type = ExtractionFieldType.Text });
        schema.Fields.Add(new ExtractionField { Key = "trackingNumber", Label = "Tracking no.", Type = ExtractionFieldType.Text });
        schema.Fields.Add(new ExtractionField { Key = "estimatedDelivery", Label = "Estimated delivery", Type = ExtractionFieldType.Date });

        Extraction.Document = DocumentExtractionDemoData.Email();
        Extraction.Schema = schema;
        Extraction.Model = DocumentExtractionDemoData.Model();
        Extraction.Extractor = new DocumentExtractionDemoData.SampleExtractor();
    }
}
