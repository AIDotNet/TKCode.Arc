using System.Globalization;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class LayoutMorphTeamDemo : UserControl
{
    public LayoutMorphTeamDemo()
    {
        InitializeComponent();
        Morph.Layouts = [LayoutMorphMode.Carousel, LayoutMorphMode.Helix, LayoutMorphMode.Grid];
        var people = LayoutMorphDemoData.CreateDesigners();
        Morph.Items.AddRange(people);
        var orders = people.Sum(p => int.Parse(p.Stats[1].Value, NumberStyles.AllowThousands, CultureInfo.InvariantCulture));
        Morph.DefaultStats.Add(new LayoutMorphFact("Designers", people.Count.ToString(CultureInfo.InvariantCulture)));
        Morph.DefaultStats.Add(new LayoutMorphFact("Orders", orders.ToString("#,0", CultureInfo.InvariantCulture)));
        Morph.DefaultStats.Add(new LayoutMorphFact("Cities", people.Count.ToString(CultureInfo.InvariantCulture)));
    }
}
