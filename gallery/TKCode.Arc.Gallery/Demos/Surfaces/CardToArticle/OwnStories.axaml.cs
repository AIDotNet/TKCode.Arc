using Avalonia.Collections;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class CardToArticleOwnStoriesDemo : UserControl
{
    public CardToArticleOwnStoriesDemo()
    {
        InitializeComponent();
        // The pictures are borrowed from the samples; any IImage (a Bitmap, a DrawingImage) works.
        var art = CardToArticleSamples.AllStories();
        Notes.Labels = new CardToArticleLabels
        {
            Close = "Back to the floor plan",
            CardName = item => $"Exhibit: {item.Title}"
        };
        Notes.Items = new AvaloniaList<CardToArticleItem>
        {
            new()
            {
                Id = "room-2",
                Tag = "Room 2",
                Title = "Paper lanterns of the river towns",
                Meta = "Ground floor",
                Image = art[5].Image,
                ImageAlt = "Night sky with meteor streaks above a city skyline",
                Author = "Curator's note",
                Body = """
                    Each lantern in this room was folded from a single sheet and lit for one night only, at the end of the harvest.

                    > Look for the small cut in every rim: it marks the family that made it.

                    The oldest lantern, by the window, has been repaired with rice paste more than forty times.
                    """
            },
            new()
            {
                Id = "room-5",
                Tag = "Room 5",
                Title = "A loom you are allowed to touch",
                Meta = "First floor",
                Image = art[4].Image,
                ImageAlt = "Stacked planks of pale and dark wood under a warm lamp",
                Author = "Curator's note",
                Body = """
                    This loom is a working copy. Sit down, press the left pedal and pass the shuttle through; a guide shows the next row.

                    Visitors have woven more than nine metres of cloth since the room opened in spring.
                    """
            }
        };
    }
}
