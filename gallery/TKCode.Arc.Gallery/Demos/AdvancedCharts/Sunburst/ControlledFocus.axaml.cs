using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class SunburstControlledFocusDemo : UserControl
{
    public SunburstControlledFocusDemo()
    {
        InitializeComponent();
        Chart.ValueFormatter = v => $"{v:#,0} loans";
        Chart.Root = new SunburstNode("city", "All branches", children:
        [
            new SunburstNode("riverside", "Riverside", children:
            [
                new SunburstNode("riverside-fiction", "Fiction", children:
                [
                    new SunburstNode("riverside-mystery", "Mystery", 1840),
                    new SunburstNode("riverside-fantasy", "Fantasy", 1260),
                    new SunburstNode("riverside-romance", "Romance", 730)
                ]),
                new SunburstNode("riverside-kids", "Children", 2150),
                new SunburstNode("riverside-reference", "Reference", 410)
            ]),
            new SunburstNode("old-town", "Old Town", children:
            [
                new SunburstNode("old-town-history", "Local history", 980),
                new SunburstNode("old-town-maps", "Maps", 260),
                new SunburstNode("old-town-kids", "Children", 640)
            ]),
            new SunburstNode("harbour", "Harbour", children:
            [
                new SunburstNode("harbour-audio", "Audiobooks", 1120),
                new SunburstNode("harbour-film", "Film", 870)
            ])
        ]);
    }

    private void OnGoClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: string id })
        {
            Chart.FocusedId = id;
        }
    }

    private void OnFocusedIdChanged(object? sender, SunburstFocusedIdChangedEventArgs e)
        => Status.Text = $"Centre: {e.Node.Label} (from {e.ChangeSource}, was {e.PreviousId})";
}
