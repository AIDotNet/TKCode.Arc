namespace TKCode.Arc;

/// <summary>How glass surfaces blur what lies behind them (see <see cref="GlassBackdrop.ModeProperty"/>).</summary>
public enum GlassBackdropMode
{
    /// <summary>
    /// The glass paints <see cref="GlassBackdrop.SourceProperty"/> again under itself with a visual brush and blurs that copy.
    /// It needs nothing from the renderer, but it needs a source, and a live source is drawn twice per frame.
    /// </summary>
    Brush,

    /// <summary>
    /// The compositor blurs what is already drawn behind the glass (Avalonia's <c>Visual.BackdropEffect</c>), so no source
    /// is needed and nothing is drawn twice. The renderer must be able to read back what it drew: a renderer that cannot
    /// (headless drawing), a picture taken with <c>RenderTargetBitmap</c> or a <c>VisualBrush</c> shows the tint without
    /// the blur; so does glass inside an element with an <c>Effect</c> or an <c>OpacityMask</c> (it draws into a layer of
    /// its own, with nothing behind). A popup in its own window has nothing behind it either, so the glass there is a tinted
    /// surface.
    /// </summary>
    Native
}
