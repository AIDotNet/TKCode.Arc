namespace TKCode.Arc;

/// <summary>
/// The summary and the key points as they stream: the shared <see cref="AiStreamText"/>, where each new chunk only fades
/// in over 140ms on the standard curve (Arc's per-chunk span fade) and fades its own glyphs, so it reads the same on any
/// surface. Text set whole (a cached result) and reduced motion or Off show at once. Its own type so the card's theme
/// styles it (the body's type, inherited weight).
/// </summary>
internal sealed class TldrToggleStreamText : AiStreamText
{
    /// <summary>One chunk's fade in.</summary>
    internal static readonly TimeSpan ChunkFade = TimeSpan.FromMilliseconds(140);

    public TldrToggleStreamText()
    {
        FadeDuration = ChunkFade;
    }
}
