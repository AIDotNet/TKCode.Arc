using System.Text;

namespace TKCode.Arc;

/// <summary>One translation request of an <see cref="AiTranslate"/> card: what arrived, what is still to stream and its cancellation.</summary>
internal sealed class AiTranslateRun(AiTranslateLanguage from, AiTranslateLanguage to)
{
    public AiTranslateLanguage From { get; } = from;

    public AiTranslateLanguage To { get; } = to;

    public CancellationTokenSource Cancellation { get; } = new();

    /// <summary>Received and not yet on screen.</summary>
    public StringBuilder Pending { get; } = new();

    /// <summary>Everything received.</summary>
    public StringBuilder Received { get; } = new();

    /// <summary>The source has finished (the stream ended).</summary>
    public bool SourceDone { get; set; }

    /// <summary>Seconds since the last chunk landed.</summary>
    public double Since { get; set; }

    /// <summary>Chunks shown so far (picks the next chunk's size).</summary>
    public int Chunks { get; set; }

    /// <summary>The simulation's remaining wait before its text arrives.</summary>
    public double Delay { get; set; }

    /// <summary>The simulation's text, or null when the language has no ready translation (it then fails).</summary>
    public string? Simulated { get; set; }
}
