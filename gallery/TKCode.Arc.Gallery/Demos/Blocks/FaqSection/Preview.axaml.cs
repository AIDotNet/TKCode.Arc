using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class FaqSectionPreviewDemo : UserControl
{
    public FaqSectionPreviewDemo()
    {
        InitializeComponent();
        Layouts.SelectionChanged += (_, _) => Section.Variant = Layouts.SelectedValue switch
        {
            "columns" => FaqSectionVariant.Columns,
            "search" => FaqSectionVariant.Search,
            _ => FaqSectionVariant.Accordion
        };
    }
}
