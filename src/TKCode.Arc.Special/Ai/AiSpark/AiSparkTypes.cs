namespace TKCode.Arc;

/// <summary>What the AI behind an <see cref="AiSpark"/> is doing; each status has its own pose and motion.</summary>
public enum AiSparkStatus
{
    /// <summary>Ready: a slow breath of the whole mark while one diagonal at a time reaches out (see <see cref="AiSpark.Twinkle"/>).</summary>
    Idle,

    /// <summary>Thinking: one crest ripples around the ring every 1.9s while the mark turns at 26 degrees per second.</summary>
    Thinking,

    /// <summary>Writing: a lub dub heartbeat every 1.04s, axes firing in sequence, turning at 46 degrees per second.</summary>
    Generating,

    /// <summary>Done: the rays gather toward the center and settle at rest while the spin glides home.</summary>
    Done,

    /// <summary>Failed or aborted: one small decaying shake, shorter rays, dimmer diagonals and a muted danger tone.</summary>
    Error
}

/// <summary>The semantic color of an <see cref="AiSpark"/>; the diagonals and the core use a lighter tone of it.</summary>
public enum AiSparkTone
{
    /// <summary>The accent color.</summary>
    Accent,

    /// <summary>The text color.</summary>
    Foreground,

    /// <summary>The muted text color.</summary>
    Muted,

    /// <summary>The inherited <c>Foreground</c> of the spark (the color of the surrounding text).</summary>
    Inherit
}

/// <summary>How an <see cref="AiSpark"/> is presented.</summary>
public enum AiSparkVariant
{
    /// <summary>The spark alone.</summary>
    Mark,

    /// <summary>The spark with a crossfading status label in a quiet bordered capsule, 28px high.</summary>
    Pill,

    /// <summary>The spark in a round, lightly tinted tile (message avatars, icon buttons); the mark is 56% of the tile.</summary>
    Avatar
}
