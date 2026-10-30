using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The draft while it streams: the shared <see cref="AiStreamText"/>, where each chunk only fades in over 140ms on the
/// standard curve (Arc's per-chunk span fade) and fades its own glyphs, so it reads the same on any fill. It keeps one
/// line's height from the first frame, so the draft box does not collapse. Reduced motion and Off add chunks at once
/// (the card only streams into it at full motion). Its own type so the card's theme styles it (the draft's type, regular
/// weight).
/// </summary>
internal sealed class ReplyDraftStreamText : AiStreamText
{
    /// <summary>One chunk's fade in.</summary>
    internal static readonly TimeSpan ChunkFade = TimeSpan.FromMilliseconds(140);

    public ReplyDraftStreamText()
    {
        FadeDuration = ChunkFade;
        HoldsOneLine = true;
        FontWeight = FontWeight.Normal;
    }
}
