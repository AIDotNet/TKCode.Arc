namespace TKCode.Arc;

/// <summary>Where a player is in its life: nothing loaded, loading, loaded and paused or playing, at the end, or failed.</summary>
public enum AudioPlaybackState
{
    Idle,
    Loading,
    Paused,
    Playing,
    Ended,
    Failed,
}

/// <summary>
/// One audio stream that can be loaded, played, paused and sought. Implementations raise every event on the
/// <see cref="SynchronizationContext"/> that was current when the player was created (the UI thread for controls),
/// and report the position a few times a second while playing, like an HTML audio element's <c>timeupdate</c>.
/// </summary>
public interface IAudioPlayer : IDisposable
{
    /// <summary>The current playback state.</summary>
    AudioPlaybackState State { get; }

    /// <summary>The playback position, clamped to <see cref="Duration"/>.</summary>
    TimeSpan Position { get; }

    /// <summary>The length of the loaded audio; <see cref="TimeSpan.Zero"/> until it is known.</summary>
    TimeSpan Duration { get; }

    /// <summary>Output volume from 0 to 1, independent of <see cref="IsMuted"/>.</summary>
    double Volume { get; set; }

    /// <summary>Whether the output is silenced without changing <see cref="Volume"/>.</summary>
    bool IsMuted { get; set; }

    /// <summary>Whether playback starts again from the beginning at the end instead of stopping.</summary>
    bool IsLooping { get; set; }

    /// <summary>Loads <paramref name="source"/>, replacing what was loaded; the player is paused at zero when it completes.</summary>
    Task LoadAsync(AudioSource source, CancellationToken cancellationToken = default);

    /// <summary>Starts or resumes playback; from the start when the player is at the end.</summary>
    void Play();

    /// <summary>Pauses playback and keeps the position.</summary>
    void Pause();

    /// <summary>Moves the playback position; clamped to the audio's length.</summary>
    void Seek(TimeSpan position);

    /// <summary>Raised when <see cref="State"/> changes.</summary>
    event EventHandler? StateChanged;

    /// <summary>Raised while playing (a few times a second) and after a seek.</summary>
    event EventHandler? PositionChanged;

    /// <summary>Raised when <see cref="Duration"/> becomes known or changes.</summary>
    event EventHandler? DurationChanged;

    /// <summary>Raised when loading or playback fails; <see cref="State"/> is then <see cref="AudioPlaybackState.Failed"/>.</summary>
    event EventHandler<AudioErrorEventArgs>? Failed;
}

/// <summary>Carries the reason a player failed.</summary>
public sealed class AudioErrorEventArgs(string message, Exception? exception = null) : EventArgs
{
    /// <summary>A short description of the failure.</summary>
    public string Message { get; } = message;

    /// <summary>The underlying exception, when there is one.</summary>
    public Exception? Exception { get; } = exception;
}

/// <summary>Creates players for one platform; set <see cref="ArcAudio.Backend"/> to choose it.</summary>
public interface IAudioBackend
{
    /// <summary>A short name for diagnostics, such as <c>"miniaudio"</c> or <c>"browser"</c>.</summary>
    string Name { get; }

    /// <summary>Whether the backend makes sound; false for the simulated backend.</summary>
    bool IsAudible { get; }

    /// <summary>Creates a new player. The caller disposes it.</summary>
    IAudioPlayer CreatePlayer();
}

/// <summary>The process-wide audio backend. Platform packages replace the default silent backend at startup.</summary>
public static class ArcAudio
{
    private static IAudioBackend s_backend = SimulatedAudioBackend.Instance;

    /// <summary>The backend new players come from; defaults to <see cref="SimulatedAudioBackend"/>.</summary>
    public static IAudioBackend Backend
    {
        get => s_backend;
        set => s_backend = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>Creates a player from <see cref="Backend"/>.</summary>
    public static IAudioPlayer CreatePlayer() => s_backend.CreatePlayer();
}
