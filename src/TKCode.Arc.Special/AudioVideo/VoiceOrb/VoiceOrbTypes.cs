using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>What a voice assistant is doing, as a <see cref="VoiceOrb"/> shows it.</summary>
public enum VoiceOrbState
{
    /// <summary>No conversation: the orb breathes slowly.</summary>
    Idle,

    /// <summary>The assistant hears the user: the orb swells with the input level and sends out ripples.</summary>
    Listening,

    /// <summary>The assistant works on an answer: the orb draws in and a bright band runs around its rim.</summary>
    Thinking,

    /// <summary>The assistant talks: the orb grows, turns lumpier and pulses with the output level.</summary>
    Speaking
}

/// <summary>Who a line of a <see cref="VoiceTranscript"/> belongs to, which sets its size and color.</summary>
public enum VoiceTranscriptSpeaker
{
    /// <summary>The assistant's words: the base size in the foreground color.</summary>
    Assistant,

    /// <summary>The user's words: the base size in the secondary text color.</summary>
    User,

    /// <summary>A hint or a notice from the app: one step smaller, in the muted text color.</summary>
    System
}

/// <summary>A source of live levels for a <see cref="VoiceOrb"/> (a microphone meter, a speech synthesizer's envelope).</summary>
public interface IVoiceOrbLevelSource
{
    /// <summary>
    /// The input level (the user's voice, 0 to 1) at <paramref name="seconds"/> of the orb's own clock. The orb reads it
    /// once per frame while it listens.
    /// </summary>
    double ReadInput(double seconds);

    /// <summary>
    /// The output level (the assistant's voice, 0 to 1) at <paramref name="seconds"/> of the orb's own clock. The orb reads
    /// it once per frame while it speaks.
    /// </summary>
    double ReadOutput(double seconds);
}

/// <summary>Arguments of <see cref="VoiceControls.MuteChanged"/>.</summary>
public sealed class VoiceControlsMuteChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments for <paramref name="routedEvent"/>.</summary>
    public VoiceControlsMuteChangedEventArgs(RoutedEvent routedEvent, bool isMuted)
        : base(routedEvent)
    {
        IsMuted = isMuted;
    }

    /// <summary>Whether the microphone is now muted.</summary>
    public bool IsMuted { get; }
}
