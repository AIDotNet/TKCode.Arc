using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class MorphNavPreviewDemo : UserControl
{
    public MorphNavPreviewDemo()
    {
        InitializeComponent();
        // The feature cards' pictures are painted in code (see PhotosDemoArt).
        int[] scenes = [1, 10, 2];
        for (var i = 0; i < scenes.Length; i++)
        {
            if (Nav.Items[i].Feature is MorphNavStory story)
            {
                story.Image = PhotosDemoArt.Scene(scenes[i], 480, 300);
            }
        }
        Nav.Navigate += (_, e) =>
        {
            Status.Text = $"Opened {e.Label}";
            if (e.Section is not null)
            {
                Nav.Current = e.Section;
            }
        };
        BarSize.SelectionChanged += (_, _) => Nav.IsCompact = Equals(BarSize.SelectedValue, "compact");
    }
}
