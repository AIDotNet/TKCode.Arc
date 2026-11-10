using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class HeroSectionPreviewDemo : UserControl
{
    public HeroSectionPreviewDemo()
    {
        InitializeComponent();
        Designs.SelectionChanged += (_, _) => Hero.Variant = Designs.SelectedValue switch
        {
            "split" => HeroSectionVariant.Split,
            "minimal" => HeroSectionVariant.Minimal,
            _ => HeroSectionVariant.Centered
        };
    }
}
