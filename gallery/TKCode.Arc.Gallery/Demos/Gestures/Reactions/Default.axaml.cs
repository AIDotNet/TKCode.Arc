using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ReactionsDefaultDemo : UserControl
{
    public ReactionsDefaultDemo()
    {
        InitializeComponent();
        Bar.Value = [new Reaction("like", 3)];
        Bar.ValueChanged += (_, _) => Announcement.Text = Bar.LastAnnouncement;
    }
}
