using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class LayoutMorphCatalogueDemo : UserControl
{
    public LayoutMorphCatalogueDemo()
    {
        InitializeComponent();
        var pieces = new ObservableCollection<LayoutMorphItem>(LayoutMorphDemoData.CreatePieces());
        Morph.Layouts = [LayoutMorphMode.Grid, LayoutMorphMode.List, LayoutMorphMode.Carousel];
        Morph.ItemsSource = pieces;
        Morph.DefaultStats.Add(new LayoutMorphFact("Pieces", pieces.Count.ToString(CultureInfo.InvariantCulture)));
        Morph.DefaultStats.Add(new LayoutMorphFact("From", "$26"));
        Morph.DefaultStats.Add(new LayoutMorphFact("Ships in", "1–14 days"));
        // IsActive flips before ItemActionInvoked: count what is in the bag now.
        Morph.ItemActionInvoked += (_, _) =>
        {
            var saved = pieces.Count(p => p.IsActive);
            Bag.Text = saved == 0 ? "Your bag is empty" : saved == 1 ? "1 piece in your bag" : saved + " pieces in your bag";
        };
    }
}
