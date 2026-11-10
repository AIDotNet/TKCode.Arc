using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ContactSectionPreviewDemo : UserControl
{
    public ContactSectionPreviewDemo()
    {
        InitializeComponent();
        Layouts.SelectionChanged += (_, _) => Section.Variant = Layouts.SelectedValue switch
        {
            "channels" => ContactSectionVariant.Channels,
            "offices" => ContactSectionVariant.Offices,
            _ => ContactSectionVariant.Form
        };
    }
}
