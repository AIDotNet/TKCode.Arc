using Avalonia;
using Avalonia.Media;
using TKCode.Terminal.Core;

namespace TKCode.Terminal.Avalonia;

/// <summary>
/// Draws code points itself instead of taking them from a font (box drawing, block elements, Powerline, icon sets...).
/// <see cref="TerminalView.GlyphProviders"/> are asked before the built-in <see cref="TerminalBuiltInGlyphs"/>; the first one
/// whose <see cref="Handles"/> answers <c>true</c> draws the cell. Results are cached per code point and cell size, so
/// <see cref="Create"/> must return the same shape for the same arguments.
/// </summary>
public interface ITerminalGlyphProvider
{
    /// <summary>Whether this provider draws <paramref name="codepoint"/>. Called for every non-blank cell while a row is built, so keep it cheap.</summary>
    bool Handles(int codepoint);

    /// <summary>The shape of <paramref name="codepoint"/> in a cell of <paramref name="request"/>'s size, or <c>null</c> to leave the cell blank.</summary>
    TerminalGlyph? Create(int codepoint, TerminalGlyphRequest request);
}

/// <summary>
/// The cell a <see cref="ITerminalGlyphProvider"/> draws into, in device pixels: <see cref="Width"/> covers all the cells of
/// the character (two for a wide one).
/// </summary>
/// <param name="Width">Width of the character's cells, in device pixels.</param>
/// <param name="Height">Height of a cell, in device pixels.</param>
/// <param name="Thickness">Thickness of a light line (<see cref="CellMetrics.BoxThickness"/>), in device pixels.</param>
/// <param name="Scaling">Device pixels per DIP.</param>
public readonly record struct TerminalGlyphRequest(int Width, int Height, int Thickness, double Scaling);

/// <summary>
/// A shape drawn in a cell with the cell's foreground. <see cref="Geometry"/> is in device pixels with the origin at the
/// top-left corner of the cell; keep edges on whole pixels so neighboring cells meet without a seam.
/// </summary>
/// <param name="Geometry">The shape, in device pixels relative to the cell's top-left corner.</param>
/// <param name="Opacity">Opacity of the foreground the shape is drawn with (shades use 0.25, 0.5 and 0.75).</param>
/// <param name="StrokeThickness">0 fills <paramref name="Geometry"/>; a positive value strokes it with a pen this thick (device pixels) instead.</param>
public sealed record TerminalGlyph(Geometry Geometry, double Opacity = 1, double StrokeThickness = 0);

/// <summary>Where an <see cref="ITerminalRenderLayer"/> is drawn.</summary>
public enum TerminalLayerPlacement
{
    /// <summary>Above the cell backgrounds and below the text (selection or search highlights).</summary>
    BelowText,

    /// <summary>Above the text and below the cursor.</summary>
    AboveText,

    /// <summary>Above everything, including the cursor and the input method's composition.</summary>
    Overlay,
}

/// <summary>
/// Something a host draws into a <see cref="TerminalView"/> together with the terminal's own content: selection, search
/// matches, command marks, hover effects. Add it to <see cref="TerminalView.Layers"/>; call
/// <see cref="Visual.InvalidateVisual"/> on the view when the layer's content changes.
/// </summary>
public interface ITerminalRenderLayer
{
    /// <summary>Where the layer is drawn.</summary>
    TerminalLayerPlacement Placement { get; }

    /// <summary>Draws the layer. <paramref name="context"/> places cells and resolves their colors.</summary>
    void Render(DrawingContext drawingContext, TerminalRenderContext context);
}

/// <summary>What an <see cref="ITerminalRenderLayer"/> needs to draw over a frame: the frame, the cell geometry, and the colors the view uses.</summary>
public sealed class TerminalRenderContext
{
    private readonly TerminalView _view;

    internal TerminalRenderContext(TerminalView view, FrameSnapshot frame, CellMetrics metrics, double paddingPx)
    {
        _view = view;
        Frame = frame;
        Metrics = metrics;
        PaddingPx = paddingPx;
    }

    /// <summary>The frame being drawn.</summary>
    public FrameSnapshot Frame { get; }

    /// <summary>The cell metrics (device pixels).</summary>
    public CellMetrics Metrics { get; }

    /// <summary>The view's padding, rounded to device pixels.</summary>
    public double PaddingPx { get; }

    /// <summary>Whether the cursor blinks into its visible phase in this frame (it is always visible when it does not blink).</summary>
    public bool CursorVisiblePhase => _view.CursorPhaseOn;

    /// <summary>Bounds of <paramref name="columns"/> cells starting at (<paramref name="column"/>, <paramref name="row"/>), in DIP relative to the view.</summary>
    public Rect CellBounds(int column, int row, int columns = 1) => _view.CellBounds(column, row, columns);

    /// <summary>Bounds of a span of cells on one row, in DIP (<see cref="CellSpan.EndColumn"/> inclusive).</summary>
    public Rect SpanBounds(CellSpan span) => CellBounds(span.StartColumn, span.Row, span.EndColumn - span.StartColumn + 1);

    /// <summary>
    /// The colors the view draws the cell with: foreground after bold-is-bright, inverse, faint and the minimum contrast
    /// ratio, and the background after inverse (the default background for cells without one).
    /// </summary>
    public (Color Foreground, Color Background) ColorsAt(int column, int row)
    {
        if (row < 0 || row >= Frame.Rows.Length || column < 0 || column >= Frame.Rows[row].Cells.Length)
        {
            var palette = Frame.Palette;
            return (ToColor(palette.Foreground), ToColor(palette.Background));
        }
        var (fg, bg) = _view.ResolveDrawnColors(Frame.Rows[row], column, Frame.Palette);
        return (ToColor(fg), ToColor(bg));
    }

    private static Color ToColor(TerminalRgb c) => Color.FromRgb(c.R, c.G, c.B);
}

/// <summary>When the cursor blinks (<see cref="TerminalView.CursorBlink"/>).</summary>
public enum TerminalCursorBlink
{
    /// <summary>When the program asks for a blinking cursor (DECSCUSR odd values, mode 12).</summary>
    Program,

    /// <summary>Never.</summary>
    Never,

    /// <summary>Always, whatever the program asks.</summary>
    Always,
}

/// <summary>How the cursor looks while the view does not have focus (<see cref="TerminalView.UnfocusedCursorStyle"/>).</summary>
public enum TerminalUnfocusedCursorStyle
{
    /// <summary>A hollow block, so the focused pane stands out among several terminals.</summary>
    Hollow,

    /// <summary>No cursor.</summary>
    None,

    /// <summary>The same shape as with focus (without blinking).</summary>
    Same,
}
