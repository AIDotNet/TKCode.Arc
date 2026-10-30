using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>A vendor form with its own fields and samples.</summary>
public partial class SmartFormFillCustomFieldsDemo : UserControl
{
    public SmartFormFillCustomFieldsDemo()
    {
        InitializeComponent();
        Card.Samples =
        [
            new SmartFormFillSample("Invoice footer",
                "Questions about this invoice? Write to billing@harborline.example.com\n" +
                "Harborline Supply Co.\n" +
                "88 Quarry Lane, Portland\n" +
                "+1 503 555 0199 · harborline.example.com"),
        ];
    }
}
