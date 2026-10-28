namespace TKCode.Arc;

/// <summary>
/// One miniaudio sound. The audio thread owns playback; this side polls the cursor every 250 ms while playing (like an
/// HTML audio element's <c>timeupdate</c>) and notices the end. Native calls are serialized on <c>_gate</c>.
/// </summary>
internal sealed class MiniaudioAudioPlayer : AudioPlayerBase
{
    private static readonly TimeSpan s_tick = TimeSpan.FromMilliseconds(250);
    private static readonly Lazy<HttpClient> s_http = new(() => new HttpClient());
    private readonly nint _engine;
    private readonly object _gate = new();
    private readonly Timer _timer;
    private nint _sound;
    private uint _sampleRate;
    private int _loadVersion;

    public MiniaudioAudioPlayer(nint engine)
    {
        _engine = engine;
        _timer = new Timer(_ => OnTick(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public override TimeSpan Position
    {
        get
        {
            lock (_gate)
            {
                if (_sound == 0 || _sampleRate == 0 || MiniaudioInterop.SoundCursor(_sound, out var frame) != 0)
                {
                    return TimeSpan.Zero;
                }
                var position = TimeSpan.FromSeconds((double)frame / _sampleRate);
                return Duration > TimeSpan.Zero && position > Duration ? Duration : position;
            }
        }
    }

    public override async Task LoadAsync(AudioSource source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        var version = Interlocked.Increment(ref _loadVersion);
        ReleaseSound();
        SetState(AudioPlaybackState.Loading);
        nint sound;
        try
        {
            sound = await Task.Run(() => CreateSoundAsync(source, cancellationToken), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (version == _loadVersion)
            {
                SetState(AudioPlaybackState.Idle);
            }
            throw;
        }
        catch (Exception exception)
        {
            if (version == _loadVersion)
            {
                Fail(exception.Message, exception);
            }
            return;
        }
        lock (_gate)
        {
            // A newer load or Dispose won the race; drop this sound.
            if (version != _loadVersion || IsDisposed)
            {
                MiniaudioInterop.SoundDestroy(sound);
                return;
            }
            _sound = sound;
            MiniaudioInterop.SoundSampleRate(sound, out _sampleRate);
            MiniaudioInterop.SoundLength(sound, out var frames);
            MiniaudioInterop.SoundSetVolume(sound, (float)EffectiveGain);
            MiniaudioInterop.SoundSetLooping(sound, IsLooping ? 1 : 0);
            SetDuration(_sampleRate == 0 ? TimeSpan.Zero : TimeSpan.FromSeconds((double)frames / _sampleRate));
        }
        SetState(AudioPlaybackState.Paused);
        RaisePositionChanged();
    }

    private async Task<nint> CreateSoundAsync(AudioSource source, CancellationToken cancellationToken)
    {
        switch (source)
        {
            case BytesAudioSource bytes:
                return FromMemory(bytes.Data);
            case UriAudioSource { Uri.IsFile: true } file:
                {
                    var result = MiniaudioInterop.SoundCreateFromFile(_engine, file.Uri.LocalPath, out var sound);
                    Check(result, file.Uri.LocalPath);
                    return sound;
                }
            case UriAudioSource uri:
                {
                    var data = await s_http.Value.GetByteArrayAsync(uri.Uri, cancellationToken).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    return FromMemory(data);
                }
            default:
                throw new NotSupportedException($"Unsupported audio source {source.GetType().Name}.");
        }
    }

    private unsafe nint FromMemory(ReadOnlyMemory<byte> data)
    {
        if (data.IsEmpty)
        {
            throw new InvalidDataException("The audio data is empty.");
        }
        fixed (byte* pointer = data.Span)
        {
            var result = MiniaudioInterop.SoundCreateFromMemory(_engine, pointer, (nuint)data.Length, out var sound);
            Check(result, "the audio data");
            return sound;
        }
    }

    private static void Check(int result, string what)
    {
        if (result != 0)
        {
            throw new InvalidDataException($"miniaudio could not open {what} (result {result}).");
        }
    }

    public override void Play()
    {
        lock (_gate)
        {
            if (_sound == 0 || State is AudioPlaybackState.Playing or AudioPlaybackState.Loading)
            {
                return;
            }
            if (State == AudioPlaybackState.Ended || MiniaudioInterop.SoundAtEnd(_sound) != 0)
            {
                MiniaudioInterop.SoundSeek(_sound, 0);
            }
            if (MiniaudioInterop.SoundStart(_sound) != 0)
            {
                return;
            }
            _timer.Change(s_tick, s_tick);
        }
        SetState(AudioPlaybackState.Playing);
    }

    public override void Pause()
    {
        lock (_gate)
        {
            if (_sound == 0 || State != AudioPlaybackState.Playing)
            {
                return;
            }
            MiniaudioInterop.SoundStop(_sound);
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
        }
        SetState(AudioPlaybackState.Paused);
        RaisePositionChanged();
    }

    public override void Seek(TimeSpan position)
    {
        lock (_gate)
        {
            if (_sound == 0 || _sampleRate == 0)
            {
                return;
            }
            var clamped = position < TimeSpan.Zero ? TimeSpan.Zero : Duration > TimeSpan.Zero && position > Duration ? Duration : position;
            MiniaudioInterop.SoundSeek(_sound, (ulong)(clamped.TotalSeconds * _sampleRate));
            if (State == AudioPlaybackState.Ended && clamped < Duration)
            {
                SetState(AudioPlaybackState.Paused);
            }
        }
        RaisePositionChanged();
    }

    protected override void OnOutputGainChanged()
    {
        lock (_gate)
        {
            if (_sound != 0)
            {
                MiniaudioInterop.SoundSetVolume(_sound, (float)EffectiveGain);
            }
        }
    }

    protected override void OnLoopingChanged()
    {
        lock (_gate)
        {
            if (_sound != 0)
            {
                MiniaudioInterop.SoundSetLooping(_sound, IsLooping ? 1 : 0);
            }
        }
    }

    private void OnTick()
    {
        var ended = false;
        lock (_gate)
        {
            if (_sound == 0 || IsDisposed || State != AudioPlaybackState.Playing)
            {
                return;
            }
            if (!IsLooping && MiniaudioInterop.SoundIsPlaying(_sound) == 0 && MiniaudioInterop.SoundAtEnd(_sound) != 0)
            {
                _timer.Change(Timeout.Infinite, Timeout.Infinite);
                ended = true;
            }
        }
        if (ended)
        {
            SetState(AudioPlaybackState.Ended);
        }
        RaisePositionChanged();
    }

    private void ReleaseSound()
    {
        lock (_gate)
        {
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
            if (_sound != 0)
            {
                MiniaudioInterop.SoundDestroy(_sound);
                _sound = 0;
                _sampleRate = 0;
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        Interlocked.Increment(ref _loadVersion);
        ReleaseSound();
        _timer.Dispose();
    }
}
