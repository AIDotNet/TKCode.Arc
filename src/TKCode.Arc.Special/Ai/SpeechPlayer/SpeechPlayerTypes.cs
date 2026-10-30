using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Where a <see cref="SpeechPlayer"/> is: the action row, or the player in one of its states.</summary>
public enum SpeechPlayerState
{
    /// <summary>The action row with Read aloud shows; nothing is being read.</summary>
    Idle,

    /// <summary>The player is open and waiting for speech: a shimmering "Preparing Ava's voice".</summary>
    Preparing,

    /// <summary>The reply is being read; the highlight follows the spoken word.</summary>
    Playing,

    /// <summary>Reading is paused at a word.</summary>
    Paused,

    /// <summary>The last word was read; the play button reads Play again.</summary>
    Finished,

    /// <summary>Speech could not be generated; the play button reads Retry and the reason shows.</summary>
    Failed
}

/// <summary>How a <see cref="SpeechPlayer"/> is drawn.</summary>
public enum SpeechPlayerVariant
{
    /// <summary>A bordered surface with padding.</summary>
    Card,

    /// <summary>No border and no padding, to sit inside a chat column.</summary>
    Plain
}

/// <summary>The playback speeds the speed button cycles through.</summary>
public enum SpeechSpeed
{
    /// <summary>1×.</summary>
    Normal,

    /// <summary>1.5×.</summary>
    OneAndHalf,

    /// <summary>2×.</summary>
    Twice
}

/// <summary>One voice in the <see cref="SpeechPlayer"/> voice picker.</summary>
public sealed class SpeechVoice
{
    /// <summary>Creates a voice.</summary>
    public SpeechVoice()
    {
    }

    /// <summary>Creates a voice with its id, name and an optional one-line description.</summary>
    public SpeechVoice(string id, string name, string? description = null)
    {
        Id = id;
        Name = name;
        Description = description;
    }

    /// <summary>The id your TTS provider knows the voice by.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The name shown in the picker and the announcements ("Ava").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>One short line under the name ("Warm and conversational"), or null.</summary>
    public string? Description { get; set; }

    /// <inheritdoc />
    public override string ToString() => Name;
}

/// <summary>When one word of the text is spoken, on the 1× timeline.</summary>
/// <param name="Start">When the word starts.</param>
/// <param name="End">When the word ends.</param>
public readonly record struct SpeechWordTiming(TimeSpan Start, TimeSpan End);

/// <summary>What a <see cref="SpeechPlayer"/> asks your text to speech service for.</summary>
public sealed class SpeechRequest
{
    /// <summary>Creates a request.</summary>
    public SpeechRequest(string text, SpeechVoice voice, CancellationToken cancellationToken)
    {
        Text = text;
        Voice = voice;
        CancellationToken = cancellationToken;
    }

    /// <summary>The text to speak, exactly as shown.</summary>
    public string Text { get; }

    /// <summary>The voice picked.</summary>
    public SpeechVoice Voice { get; }

    /// <summary>Cancelled when the request is no longer wanted (a new voice, Stop reading, new text); abort the call.</summary>
    public CancellationToken CancellationToken { get; }
}

/// <summary>
/// The playback of fetched speech, supplied by the app (an audio element, a platform player, or an adapter over
/// <c>IAudioPlayer</c>). The <see cref="SpeechPlayer"/> drives it and reads <see cref="Position"/> on every frame while it
/// plays, so its position is the player's clock. All calls come from the UI thread.
/// </summary>
public interface ISpeechAudio
{
    /// <summary>The playback position on the 1× timeline.</summary>
    TimeSpan Position { get; }

    /// <summary>The length of the audio, or <see cref="TimeSpan.Zero"/> while it is not known.</summary>
    TimeSpan Duration { get; }

    /// <summary>The playback rate (1, 1.5 or 2).</summary>
    double Rate { get; set; }

    /// <summary>Starts or resumes playback.</summary>
    void Play();

    /// <summary>Pauses and keeps the position.</summary>
    void Pause();

    /// <summary>Moves the position (clamped to the audio).</summary>
    void Seek(TimeSpan position);
}

/// <summary>
/// What your text to speech service returned: the playback, the word timings and the length. Each part is optional:
/// without <see cref="Audio"/> the player keeps time itself (simulated, no sound); without <see cref="Timings"/> they are
/// estimated from the text and fitted to the length.
/// </summary>
public sealed class SpeechAudio
{
    /// <summary>The playback to drive, or null for a timed simulation that plays no sound.</summary>
    public ISpeechAudio? Audio { get; init; }

    /// <summary>One start and end per whitespace separated word of the text, from the provider's alignment data; or null.</summary>
    public IReadOnlyList<SpeechWordTiming>? Timings { get; init; }

    /// <summary>The length of the speech on the 1× timeline, when known (the estimated timings are fitted to it).</summary>
    public TimeSpan? Duration { get; init; }
}

/// <summary>Arguments of the <see cref="SpeechPlayer"/> playback events: where playback is, with which voice and speed.</summary>
public class SpeechPlaybackEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public SpeechPlaybackEventArgs(RoutedEvent routedEvent, TimeSpan time, TimeSpan duration, int wordIndex, SpeechVoice voice, SpeechSpeed speed)
        : base(routedEvent)
    {
        Time = time;
        Duration = duration;
        WordIndex = wordIndex;
        Voice = voice;
        Speed = speed;
    }

    /// <summary>The position on the 1× timeline.</summary>
    public TimeSpan Time { get; }

    /// <summary>The length on the 1× timeline.</summary>
    public TimeSpan Duration { get; }

    /// <summary>The index of the word being spoken (−1 before the first).</summary>
    public int WordIndex { get; }

    /// <summary>The voice reading.</summary>
    public SpeechVoice Voice { get; }

    /// <summary>The playback speed.</summary>
    public SpeechSpeed Speed { get; }
}

/// <summary>Arguments of <see cref="SpeechPlayer.WordChanged"/>: the playback event plus the word now spoken.</summary>
public sealed class SpeechWordEventArgs : SpeechPlaybackEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public SpeechWordEventArgs(RoutedEvent routedEvent, TimeSpan time, TimeSpan duration, int wordIndex, SpeechVoice voice, SpeechSpeed speed, string word)
        : base(routedEvent, time, duration, wordIndex, voice, speed)
    {
        Word = word;
    }

    /// <summary>The word now spoken, as written in the text.</summary>
    public string Word { get; }
}

/// <summary>Arguments of <see cref="SpeechPlayer.VoiceChanged"/>.</summary>
public sealed class SpeechVoiceChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public SpeechVoiceChangedEventArgs(RoutedEvent routedEvent, SpeechVoice voice)
        : base(routedEvent)
    {
        Voice = voice;
    }

    /// <summary>The voice picked.</summary>
    public SpeechVoice Voice { get; }
}

/// <summary>Arguments of <see cref="SpeechPlayer.SpeedChanged"/>.</summary>
public sealed class SpeechSpeedChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public SpeechSpeedChangedEventArgs(RoutedEvent routedEvent, SpeechSpeed speed)
        : base(routedEvent)
    {
        Speed = speed;
    }

    /// <summary>The new speed.</summary>
    public SpeechSpeed Speed { get; }
}
