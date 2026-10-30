using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>A still life drawn from a scene of its own: lines, regions back to front and a light source.</summary>
public partial class SketchToImageOwnSceneDemo : UserControl
{
    private const string Bowl = "M180 250 C180 320 420 320 420 250 C380 238 220 238 180 250 Z";
    private const string Pear = "M262 172 C250 172 248 194 244 206 C226 222 226 260 262 262 C298 260 296 222 280 206 C276 194 274 172 262 172 Z";
    private const string Apple = "M308 232 A32 32 0 1 0 372 232 A32 32 0 1 0 308 232 Z";

    public SketchToImageOwnSceneDemo()
    {
        InitializeComponent();
        Card.Scene = new SketchToImageScene
        {
            Alt = "A pear and an apple in a blue bowl on a wooden table",
            Lines =
            [
                "M0 262 H600",
                "M180 250 C180 320 420 320 420 250",
                "M180 250 C220 238 380 238 420 250",
                Pear,
                Apple,
                "M262 172 L266 158",
                "M340 200 L343 188",
                "M150 334 C220 348 380 348 450 334",
            ],
            Regions =
            [
                new SketchToImageRegion
                {
                    Brush = new Rect(0, 0, 600, 270),
                    Shapes = [new SketchToImageShape("M0 0H600V262H0Z", Color.Parse("#E8DCCB"), Vertical("#F1E8DA", "#D8C7B0"))],
                },
                new SketchToImageRegion
                {
                    Brush = new Rect(0, 256, 600, 144),
                    Shapes = [new SketchToImageShape("M0 262H600V400H0Z", Color.Parse("#8A5A3C"), Vertical("#A06C49", "#6A432D"))],
                },
                new SketchToImageRegion
                {
                    Brush = new Rect(222, 164, 156, 104),
                    Shapes =
                    [
                        new SketchToImageShape(Pear, Color.Parse("#B8C25A"), Vertical("#D2D873", "#8E9A3A")),
                        new SketchToImageShape(Apple, Color.Parse("#C2483A"), Vertical("#D96150", "#99302A")),
                    ],
                },
                new SketchToImageRegion
                {
                    Brush = new Rect(176, 236, 248, 82),
                    Shapes = [new SketchToImageShape(Bowl, Color.Parse("#3E6D8E"), Vertical("#5287AB", "#2C4F69"))],
                },
            ],
            Light = new Point(110, 60),
        };
    }

    private static IBrush Vertical(string top, string bottom) =>
        new ImmutableLinearGradientBrush(
            [new ImmutableGradientStop(0, Color.Parse(top)), new ImmutableGradientStop(1, Color.Parse(bottom))],
            startPoint: new RelativePoint(0.5, 0, RelativeUnit.Relative),
            endPoint: new RelativePoint(0.5, 1, RelativeUnit.Relative));
}
