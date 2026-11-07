using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One voice message in a <see cref="VoiceRecorder"/> thread: its length, its waveform and, for messages from someone else,
/// who sent it. A message with a <see cref="From"/> name or an <see cref="Avatar"/> is incoming (left, muted bubble); one
/// without is yours (right, dark bubble).
/// </summary>
public sealed record VoiceRecorderMessage
{
    /// <summary>A stable identifier (used by <see cref="VoiceRecorder.PlayMessage"/> and <see cref="VoiceRecorder.PauseMessage"/>).</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>The length of the recording; the bubble shows it as m:ss, rounded down.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>The bar heights of the waveform, each 0 to 1 (0.12 reads as silence). See <see cref="VoiceRecorderPeaks"/>.</summary>
    public IReadOnlyList<double> Peaks { get; init; } = [];

    /// <summary>The sender's name for an incoming message (also its initials avatar and accessible name); null for yours.</summary>
    public string? From { get; init; }

    /// <summary>The sender's picture, drawn in a 28px circle; null shows the initials of <see cref="From"/>.</summary>
    public IImage? Avatar { get; init; }

    /// <summary>The small line under the bubble, such as the time it was sent; null or empty shows none.</summary>
    public string? Meta { get; init; }

    /// <summary>True for a message from someone else (it has <see cref="From"/> or <see cref="Avatar"/>).</summary>
    public bool IsIncoming => From is not null || Avatar is not null;
}

/// <summary>Where the composer of a <see cref="VoiceRecorder"/> is.</summary>
public enum VoiceRecorderPhase
{
    /// <summary>The hint and the mic button.</summary>
    Idle,

    /// <summary>Recording: discard, the live bar with its timer and waveform, and stop.</summary>
    Recording,

    /// <summary>Reviewing the take: discard, playback with a scrubbable waveform, and send.</summary>
    Review,
}

/// <summary>How a <see cref="VoiceRecorder"/> is framed.</summary>
public enum VoiceRecorderVariant
{
    /// <summary>A bordered, rounded card on the surface color.</summary>
    Card,

    /// <summary>No border, corner or background, to sit inside a page or a phone-sized column.</summary>
    Plain,
}

/// <summary>
/// Supplies the input level while recording: called once for every 90ms sample with the take number (the number of
/// messages in the thread plus one when the take started) and the sample's index from 0. Return 0 to 1; values are kept
/// at 0.12 or more, which reads as silence.
/// </summary>
/// <param name="take">The take number; the same take number gives the built-in simulation the same voice.</param>
/// <param name="sample">The index of the sample (each covers 90ms).</param>
public delegate double VoiceRecorderLevelSource(int take, int sample);

/// <summary>Carries the take that was sent or discarded.</summary>
public sealed class VoiceRecorderMessageEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public VoiceRecorderMessageEventArgs(RoutedEvent routedEvent, VoiceRecorderMessage message)
        : base(routedEvent)
    {
        Message = message;
    }

    /// <summary>The take as a message: its length and waveform (bars by length), with no sender. A sent take also carries the meta line.</summary>
    public VoiceRecorderMessage Message { get; }
}
