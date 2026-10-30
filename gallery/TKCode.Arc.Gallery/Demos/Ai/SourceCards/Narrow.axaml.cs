using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>Finished results in a narrow container.</summary>
public partial class SourceCardsNarrowDemo : UserControl
{
    public SourceCardsNarrowDemo()
    {
        InitializeComponent();
        Cards.Sources = SourceCardsDemoData.Sources();
    }
}
