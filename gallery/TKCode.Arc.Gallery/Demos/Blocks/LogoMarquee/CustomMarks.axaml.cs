using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Demos;

public partial class LogoMarqueeCustomMarksDemo : UserControl
{
    public LogoMarqueeCustomMarksDemo()
    {
        InitializeComponent();
        // Invented marks: replace them with marks you have permission to show.
        Marquee.Brands =
        [
            new LogoMarqueeBrand { Name = "Pinecrest", Icon = Geometry.Parse("M12 2 22 22H2z"), Color = Color.Parse("#3C9D6B") },
            new LogoMarqueeBrand { Name = "Saltmarsh", Icon = Geometry.Parse("M2 8h20v3H2zM2 13h20v3H2z") },
            new LogoMarqueeBrand { Name = "Copperfield", Icon = Geometry.Parse("M12 2a10 10 0 1 0 0 20 10 10 0 0 0 0-20z"), Color = Color.Parse("#C4703A") }
        ];
    }
}
