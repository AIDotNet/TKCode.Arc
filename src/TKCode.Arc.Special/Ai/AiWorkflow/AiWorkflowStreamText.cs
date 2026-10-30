using Avalonia;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// A reason or a draft of <see cref="AiWorkflow"/> that streams in: the shared <see cref="AiStreamText"/> with the whole
/// text laid out once (so the final layout is reserved from the first frame and nothing reflows), then revealed
/// <see cref="WordsPerChunk"/> words every <see cref="ChunkInterval"/>, each chunk only fading in (over
/// <see cref="ChunkFade"/>, linear). Reduced motion: chunks appear at the same pace without the fade. Off, or
/// <see cref="Reveal"/>: all at once.
/// <para>
/// The pace runs on a <see cref="LiveLoop"/> that keeps going under reduced motion (code-driven because each chunk starts
/// at its own time, which a ControlTheme transition cannot express).
/// </para>
/// </summary>
internal sealed class AiWorkflowStreamText : AiStreamText
{
    public const int WordsPerChunk = 5;

    public static readonly TimeSpan ChunkInterval = TimeSpan.FromMilliseconds(34);

    public static readonly TimeSpan ChunkFade = TimeSpan.FromMilliseconds(140);

    private readonly LiveLoop _loop;
    private readonly List<int> _chunkEnds = [];
    private double _elapsed;
    private int _next;

    static AiWorkflowStreamText()
    {
        IsHitTestVisibleProperty.OverrideDefaultValue<AiWorkflowStreamText>(false);
    }

    /// <summary>A stream of <paramref name="text"/>, shown whole until <see cref="Play"/>.</summary>
    public AiWorkflowStreamText(string? text)
    {
        FadeDuration = ChunkFade;
        FadeEasing = null;
        SizesToText = true;
        CollapsesWhenEmpty = true;
        FontWeight = FontWeight.Normal;
        _loop = new LiveLoop(this, Step) { MinimumLevel = ArcMotionLevel.Reduced };
        SetText(text);
        SplitChunks(Text);
    }

    /// <summary>How many chunks there are.</summary>
    internal int ChunkCount => _chunkEnds.Count;

    /// <summary>How many chunks have started to show.</summary>
    internal int RevealedChunks => _next;

    /// <summary>True until every chunk has shown and fully faded in.</summary>
    internal bool IsStreaming => _loop.IsActive || FadingCount > 0;

    /// <summary>Starts streaming the text from its first chunk (at once with motion off).</summary>
    public void Play()
    {
        _elapsed = 0;
        _next = 0;
        if (_chunkEnds.Count == 0 || ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off)
        {
            Reveal();
            return;
        }
        Reserve(Text);
        _loop.Start();
    }

    /// <summary>Shows the whole text at once.</summary>
    public void Reveal()
    {
        _loop.Stop();
        _next = _chunkEnds.Count;
        RevealAll();
        Settle();
    }

    private bool Step(double dt)
    {
        // Chunk i starts i intervals after the first one, which shows on the first frame.
        var interval = ChunkInterval.TotalSeconds;
        while (_next < _chunkEnds.Count && _next * interval <= _elapsed + 1e-9)
        {
            RevealTo(_chunkEnds[_next], fade: true);
            _next++;
        }
        _elapsed += dt;
        return _next < _chunkEnds.Count;
    }

    private void SplitChunks(string text)
    {
        _chunkEnds.Clear();
        var words = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var end = i == text.Length - 1 || (char.IsWhiteSpace(text[i + 1]) && !char.IsWhiteSpace(text[i]));
            if (end && !char.IsWhiteSpace(text[i]))
            {
                words++;
                if (words % WordsPerChunk == 0 || i == text.Length - 1)
                {
                    _chunkEnds.Add(i + 1);
                }
            }
        }
        if (_chunkEnds.Count == 0 || _chunkEnds[^1] < text.Length)
        {
            _chunkEnds.Add(text.Length);
        }
    }
}
