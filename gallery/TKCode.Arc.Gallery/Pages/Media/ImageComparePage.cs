using AtomUI.Controls;
using Avalonia.Controls;
using Avalonia.Layout;
using TKCode.Arc.Gallery.Shell;

namespace TKCode.Arc.Gallery.Pages;

public static class ImageComparePage
{
    public static Control Create()
    {
        var basic = new ImageCompare
        {
            Before = new MediaArt { Scene = 0, Graded = false },
            After = new MediaArt { Scene = 0 }
        };

        var readout = GalleryUi.Mono("Position 50%");
        basic.PositionChanged += (_, e) => readout.Text = $"Position {e.Position:0.#}%";

        var vertical = new ImageCompare
        {
            Orientation = Orientation.Vertical,
            Before = new MediaArt { Scene = 2, Graded = false },
            After = new MediaArt { Scene = 2 },
            BeforeLabel = "Original",
            AfterLabel = "Graded"
        };
        var swing = new Button { Content = "Swing to horizontal", Variant = ButtonVariant.Secondary, Size = ControlSize.Small };
        swing.Click += (_, _) =>
        {
            vertical.Orientation = vertical.Orientation == Orientation.Vertical ? Orientation.Horizontal : Orientation.Vertical;
            swing.Content = vertical.Orientation == Orientation.Vertical ? "Swing to horizontal" : "Swing to vertical";
        };

        var wide = new ImageCompare
        {
            AspectRatio = 16.0 / 9,
            ShowLabels = false,
            Position = 30,
            Label = "Cleanup preview",
            Before = new MediaArt { Scene = 3, Graded = false },
            After = new MediaArt { Scene = 3 }
        };

        // Remote photos load over the drawn scene; offline, the scene stays and the frame still looks complete.
        var photos = new ImageCompare
        {
            Before = new MediaArt { Scene = 1, Graded = false },
            After = new MediaArt { Scene = 1 },
            BeforeSource = ImageSource.Parse("https://picsum.photos/id/1018/1200/800?grayscale"),
            AfterSource = ImageSource.Parse("https://picsum.photos/id/1018/1200/800"),
            BeforeLabel = "Black and white",
            AfterLabel = "Colour"
        };

        return GalleryUi.Column(20,
            GalleryUi.Card("Before and after",
                "Drag anywhere on the image: the divider follows 1:1, rubber-bands past the edges and stays where you let go. A press on the image springs it there; captions fade as the divider reaches them. Keys on the handle: arrows step 1% (Shift 10%), Home / End reveal one side; double-click centers it.",
                GalleryUi.Column(12, basic, readout)),
            GalleryUi.Card("Vertical",
                "Top and bottom instead of left and right. Changing the orientation swings the divider a quarter turn while the reveal follows it.",
                GalleryUi.Column(12, vertical, GalleryUi.Row(swing))),
            GalleryUi.Card("Wide frame without captions",
                "Any aspect ratio (16 / 9 here); captions off and a starting position of 30%.",
                wide),
            GalleryUi.Card("Photos",
                "BeforeSource and AfterSource load with AsyncImage and fade in from a soft blur; if they fail, the content underneath shows instead.",
                photos));
    }
}
