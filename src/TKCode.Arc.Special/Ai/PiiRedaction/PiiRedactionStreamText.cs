using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The reply as it streams in: the shared <see cref="AiStreamText"/> with the whole reply laid out once (<see
/// cref="AiStreamText.Reserve"/>, so its words never move or rewrap as it appears and its height is known from the first
/// chunk), and only the revealed part drawn. Each new chunk fades in on its own over 160ms (linear); nothing slides.
/// </summary>
internal sealed class PiiRedactionStreamText : AiStreamText
{
    /// <summary>How long a chunk takes to fade in.</summary>
    internal static readonly TimeSpan ChunkFade = ArcMotionPresets.Fast;

    public PiiRedactionStreamText()
    {
        FadeDuration = ChunkFade;
        FadeEasing = null;
        SizesToText = true;
        CollapsesWhenEmpty = true;
        FontWeight = FontWeight.Normal;
    }

    /// <summary>How many chunks have arrived (a reveal of the whole reply counts as one).</summary>
    public int ChunkCount => RevealCount;

    /// <summary>
    /// The reply split into chunks of three to six words (a fixed pattern, so a replay streams the same way), as the end
    /// index of each chunk.
    /// </summary>
    internal static List<int> Chunks(string text)
    {
        var ends = new List<int>();
        var words = 0;
        var target = 3;
        var pattern = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var endsWord = !char.IsWhiteSpace(text[i]) && (i + 1 == text.Length || char.IsWhiteSpace(text[i + 1]));
            if (!endsWord)
            {
                continue;
            }
            words++;
            if (words == target || i + 1 == text.Length)
            {
                // Include the space after the chunk so the next one starts at a word.
                var end = i + 1;
                while (end < text.Length && char.IsWhiteSpace(text[end]))
                {
                    end++;
                }
                ends.Add(end);
                words = 0;
                pattern++;
                target = 3 + pattern * 5 % 4;
            }
        }
        if (ends.Count == 0 && text.Length > 0)
        {
            ends.Add(text.Length);
        }
        return ends;
    }
}
