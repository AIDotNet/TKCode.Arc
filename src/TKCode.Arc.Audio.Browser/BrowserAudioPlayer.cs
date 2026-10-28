using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;

namespace TKCode.Arc;

/// <summary>
/// One HTML audio element. The element's <c>timeupdate</c>, <c>durationchange</c>, <c>ended</c> and <c>error</c> events drive
/// the player's events; WebAssembly runs on the browser's main thread, so they arrive on the UI thread. A <c>play()</c> the browser
/// refuses because no user gesture preceded it leaves the player paused instead of failed.
/// </summary>
internal sealed class BrowserAudioPlayer : AudioPlayerBase
{
    private readonly JSObject _audio;
    private int _loadVersion;

    public BrowserAudioPlayer()
    {
        _audio = BrowserAudioInterop.Create(OnEvent);
        BrowserAudioInterop.SetVolume(_audio, Volume);
    }

    public override TimeSpan Position =>
        IsDisposed || State is AudioPlaybackState.Idle or AudioPlaybackState.Loading
            ? TimeSpan.Zero
            : Clamp(TimeSpan.FromSeconds(BrowserAudioInterop.CurrentTime(_audio)));

    public override async Task LoadAsync(AudioSource source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        var version = ++_loadVersion;
        SetState(AudioPlaybackState.Loading);
        double seconds;
        try
        {
            var load = source switch
            {
                BytesAudioSource bytes => BrowserAudioInterop.LoadUrl(_audio, BlobUrl(bytes), owned: true),
                UriAudioSource uri => BrowserAudioInterop.LoadUrl(_audio, uri.Uri.ToString(), owned: false),
                _ => throw new NotSupportedException($"Unsupported audio source {source.GetType().Name}."),
            };
            seconds = await load.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            if (version == _loadVersion)
            {
                SetState(AudioPlaybackState.Idle);
            }
            throw;
        }
        catch (Exception exception) when (exception is JSException or NotSupportedException)
        {
            if (version == _loadVersion && !IsDisposed)
            {
                Fail(exception.Message, exception);
            }
            return;
        }
        if (version != _loadVersion || IsDisposed)
        {
            return;
        }
        SetDuration(TimeSpan.FromSeconds(seconds));
        SetState(AudioPlaybackState.Paused);
        RaisePositionChanged();
    }

    public override async void Play()
    {
        if (IsDisposed || State is AudioPlaybackState.Idle or AudioPlaybackState.Loading or AudioPlaybackState.Failed or AudioPlaybackState.Playing)
        {
            return;
        }
        var version = _loadVersion;
        if (State == AudioPlaybackState.Ended)
        {
            BrowserAudioInterop.Seek(_audio, 0);
        }
        // Optimistic, like the element's own "play" event: the UI flips at once and falls back if the browser refuses.
        SetState(AudioPlaybackState.Playing);
        bool started;
        try
        {
            started = await BrowserAudioInterop.Play(_audio);
        }
        catch (JSException exception)
        {
            if (version == _loadVersion && !IsDisposed)
            {
                Fail(exception.Message, exception);
            }
            return;
        }
        if (!started && version == _loadVersion && !IsDisposed && State == AudioPlaybackState.Playing)
        {
            SetState(AudioPlaybackState.Paused);
        }
    }

    public override void Pause()
    {
        if (IsDisposed || State != AudioPlaybackState.Playing)
        {
            return;
        }
        BrowserAudioInterop.Pause(_audio);
        SetState(AudioPlaybackState.Paused);
        RaisePositionChanged();
    }

    public override void Seek(TimeSpan position)
    {
        if (IsDisposed || State is AudioPlaybackState.Idle or AudioPlaybackState.Loading or AudioPlaybackState.Failed)
        {
            return;
        }
        var clamped = Clamp(position);
        BrowserAudioInterop.Seek(_audio, clamped.TotalSeconds);
        if (State == AudioPlaybackState.Ended && clamped < Duration)
        {
            SetState(AudioPlaybackState.Paused);
        }
        RaisePositionChanged();
    }

    protected override void OnOutputGainChanged()
    {
        if (IsDisposed)
        {
            return;
        }
        BrowserAudioInterop.SetVolume(_audio, Volume);
        BrowserAudioInterop.SetMuted(_audio, IsMuted);
    }

    protected override void OnLoopingChanged()
    {
        if (!IsDisposed)
        {
            BrowserAudioInterop.SetLoop(_audio, IsLooping);
        }
    }

    private void OnEvent(string kind, double value)
    {
        if (IsDisposed)
        {
            return;
        }
        switch (kind)
        {
            case "time":
                RaisePositionChanged();
                break;
            case "duration":
                SetDuration(TimeSpan.FromSeconds(value));
                break;
            case "pause":
                // Paused by the browser (another tab took the audio focus, the device went away).
                if (State == AudioPlaybackState.Playing)
                {
                    SetState(AudioPlaybackState.Paused);
                }
                break;
            case "ended":
                if (!IsLooping)
                {
                    SetState(AudioPlaybackState.Ended);
                    RaisePositionChanged();
                }
                break;
            case "error":
                if (State is not (AudioPlaybackState.Idle or AudioPlaybackState.Loading))
                {
                    Fail($"Media error {value}.");
                }
                break;
        }
    }

    private static string BlobUrl(BytesAudioSource source)
    {
        // A MemoryView needs a writable Span; use the backing array when there is one (JavaScript copies it), else copy.
        Span<byte> data = MemoryMarshal.TryGetArray(source.Data, out var segment) ? segment.AsSpan() : source.Data.ToArray();
        return BrowserAudioInterop.BlobUrl(data, source.MediaType);
    }

    private TimeSpan Clamp(TimeSpan position) =>
        position < TimeSpan.Zero ? TimeSpan.Zero : Duration > TimeSpan.Zero && position > Duration ? Duration : position;

    protected override void Dispose(bool disposing)
    {
        _loadVersion++;
        BrowserAudioInterop.Dispose(_audio);
        _audio.Dispose();
    }
}
