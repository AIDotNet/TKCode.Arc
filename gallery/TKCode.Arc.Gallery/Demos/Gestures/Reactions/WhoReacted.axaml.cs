using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ReactionsWhoReactedDemo : UserControl
{
    public ReactionsWhoReactedDemo()
    {
        InitializeComponent();
        Bar.Value =
        [
            new Reaction("love", 4, true) { ReactedBy = ["Ada Okafor", "Jonas Berg", "Lin Park"] },
            new Reaction("celebrate", 2) { ReactedBy = ["Maya Chen", "Sam Ortiz"] },
            new Reaction("ship", 12) { ReactedBy = ["Priya Nair", "Tom Hale", "Eli Moss"] }
        ];
    }
}
