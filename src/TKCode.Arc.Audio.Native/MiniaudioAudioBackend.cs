namespace TKCode.Arc;

/// <summary>Plays through miniaudio's engine on the default output device. Created by <see cref="ArcAudioNative.Use"/>.</summary>
public sealed class MiniaudioAudioBackend : IAudioBackend
{
    internal MiniaudioAudioBackend(nint engine)
    {
        Engine = engine;
    }

    internal nint Engine { get; }

    /// <inheritdoc />
    public string Name => "miniaudio";

    /// <inheritdoc />
    public bool IsAudible => true;

    /// <inheritdoc />
    public IAudioPlayer CreatePlayer() => new MiniaudioAudioPlayer(Engine);
}
