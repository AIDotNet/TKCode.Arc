using Avalonia.Controls;
using TKCode.Arc.Gallery.Pages;

namespace TKCode.Arc.Gallery.Demos;

public partial class MorphSelectGroupedWithSearchDemo : UserControl
{
    public MorphSelectGroupedWithSearchDemo()
    {
        InitializeComponent();
        Zone.ItemsSource = SelectsDemoData.Cities.Select(c => new SelectOption(c.City, c.City)
        {
            Meta = SelectsDemoData.Utc(c.Offset),
            Group = c.Region,
            Keywords = c.Zone,
            Icon = ArcIcons.Globe
        }).ToArray();
        Zone.SelectedValue = "London";
    }
}
