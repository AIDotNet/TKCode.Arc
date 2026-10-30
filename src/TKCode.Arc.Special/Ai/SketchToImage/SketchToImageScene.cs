using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// What a <see cref="SketchToImage"/> draws and the picture it resolves into, on a 600 × 400 canvas: the pencil
/// <see cref="Lines"/> drawn in order, the <see cref="Regions"/> a brush paints back to front, the point the warm light
/// comes from, and the finished picture. Trace the lines and regions from your picture so the sketch matches it.
/// </summary>
public sealed class SketchToImageScene
{
    /// <summary>The width of the canvas the paths are drawn on.</summary>
    public const double CanvasWidth = 600;

    /// <summary>The height of the canvas the paths are drawn on.</summary>
    public const double CanvasHeight = 400;

    /// <summary>Describes the picture; it names the canvas for assistive technology.</summary>
    public string Alt { get; init; } = string.Empty;

    /// <summary>The pencil strokes as SVG path data, drawn in this order. Each should be one open or closed figure.</summary>
    public IReadOnlyList<string> Lines { get; init; } = [];

    /// <summary>The regions the brush blocks in, back to front.</summary>
    public IReadOnlyList<SketchToImageRegion> Regions { get; init; } = [];

    /// <summary>Where the warm light of the refining pass comes from, on the canvas; null puts it at the upper right.</summary>
    public Point? Light { get; init; }

    /// <summary>
    /// The finished picture, stretched to fill the canvas. Null paints the regions' <see cref="SketchToImageShape.Finish"/>
    /// brushes instead.
    /// </summary>
    public IImage? Image { get; init; }
}

/// <summary>One region of a <see cref="SketchToImageScene"/>: shapes the brush sweeps in together through one rectangle.</summary>
public sealed class SketchToImageRegion
{
    /// <summary>The shapes, drawn in order.</summary>
    public IReadOnlyList<SketchToImageShape> Shapes { get; init; } = [];

    /// <summary>The rectangle the round brush sweeps back and forth through, on the canvas. Cover every visible part of the shapes.</summary>
    public Rect Brush { get; init; }

    /// <summary>The color of the brush tip; null uses the first shape's flat color.</summary>
    public Color? Tip { get; init; }
}

/// <summary>One shape of a <see cref="SketchToImageRegion"/>.</summary>
public sealed class SketchToImageShape
{
    /// <summary>Creates an empty shape.</summary>
    public SketchToImageShape()
    {
    }

    /// <summary>Creates a shape from its path, its flat color and its finished brush.</summary>
    public SketchToImageShape(string data, Color? fill, IBrush? finish = null)
    {
        Data = data;
        Fill = fill;
        Finish = finish;
    }

    /// <summary>The outline as SVG path data on the 600 × 400 canvas.</summary>
    public string Data { get; init; } = string.Empty;

    /// <summary>The flat color the brush blocks in; null leaves the shape out of the blocking (a detail of the finished picture only).</summary>
    public Color? Fill { get; init; }

    /// <summary>How the shape looks in the finished picture when the scene has no <see cref="SketchToImageScene.Image"/>; null uses <see cref="Fill"/>.</summary>
    public IBrush? Finish { get; init; }
}
