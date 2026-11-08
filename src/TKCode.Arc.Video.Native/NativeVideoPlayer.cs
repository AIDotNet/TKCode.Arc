using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Platform;

namespace TKCode.Arc;

/// <summary>
/// One system player behind the tkcode_video C ABI (the same managed code for Media Foundation, AVFoundation and GStreamer).
/// </summary>
/// <remarks>
/// <para>
/// Events: the shim calls <see cref="OnNativeEvent"/> on any thread, sometimes inside a tkv_* call. The callback only queues
/// the event; a thread-pool drain handles the queue in order (it may call tkv_player_buffered and tkv_player_last_error,
/// which the callback itself must not), and <see cref="VideoPlayerBase"/> posts the public events to the creating context.
/// An <c>Open</c> marker queued before every tkv_player_open separates events of the previous media from the new one.
/// </para>
/// <para>
/// Native calls are serialized on <c>_gate</c>. The native player is created on the first <see cref="LoadAsync"/>, so the
/// <see cref="IVideoPlayer.FrameTarget"/>'s adapter (Windows decodes on the compositor's GPU) is known by then. When the
/// target's adapter changes later, the current media keeps playing on the old adapter; the next <see cref="LoadAsync"/>
/// recreates the native player on the new one.
/// </para>
/// <para>
/// Dispose destroys the native player under the gate; tkv_player_destroy guarantees no callbacks after it returns, so the
/// GC handle passed as the callback context is freed only then.
/// </para>
/// <para>
/// A CPU lease points into native memory and is read outside the gate. A load, a cancelled load or Dispose on another thread
/// would free that memory under the reader, so they first wait (up to <see cref="LeaseWaitMilliseconds"/>) for the lease to
/// come back, and no new frame is lent meanwhile. The lease's own thread does not wait (it cannot be reading then).
/// </para>
/// </remarks>
internal sealed class NativeVideoPlayer : VideoPlayerBase, IVideoFrameOwner, IVideoTimelineSync
{
    private const int MarkerOpen = 0;
    private const int MaxBufferedRanges = 32;
    private const int LeaseWaitMilliseconds = 1000;
    private static readonly WaitCallback s_drain = static state => ((NativeVideoPlayer)state!).Drain();

    private readonly NativeVideoBackend _backend;
    private readonly VideoPlayerOptions _options;
    private readonly object _gate = new();
    private readonly ConcurrentQueue<NativeEvent> _events = new();
    private readonly Dictionary<long, NativeGpuImageHandle> _gpuImages = [];
    private GCHandle _self;
    private nint _native;
    private byte[]? _nativeLuid;
    private int _drainScheduled;
    private int _loadVersion;
    private int _openEpoch;
    private int _drainEpoch;
    private PendingLoad? _pendingLoad;
    private bool _leased;
    private long _leaseToken;
    private int _leaseThread;
    private int _lendingBlocked;
    private bool _rateWarningShown;
    private bool _failureRaised;
    private long _leaseImageId;
    private bool _timelineSyncMissing;
    private bool _redeliverMissing;
    private readonly Dictionary<nint, NativeSemaphoreHandle> _semaphores = [];

    public NativeVideoPlayer(NativeVideoBackend backend, VideoPlayerOptions options)
        : base(backend)
    {
        _backend = backend;
        _options = options;
    }

    public override TimeSpan Position
    {
        get
        {
            double seconds;
            lock (_gate)
            {
                if (_native == 0)
                {
                    return TimeSpan.Zero;
                }
                seconds = VideoNativeInterop.PlayerPosition(_native);
            }
            if (!double.IsFinite(seconds) || seconds <= 0)
            {
                return TimeSpan.Zero;
            }
            var position = TimeSpan.FromSeconds(seconds);
            return Duration > TimeSpan.Zero && position > Duration ? Duration : position;
        }
    }

    public override async Task LoadAsync(VideoSource source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        var version = Interlocked.Increment(ref _loadVersion);
        cancellationToken.ThrowIfCancellationRequested();
        SetState(VideoPlaybackState.Loading);

        Uri uri;
        try
        {
            uri = await VideoSourceFiles.MaterializeAsync(source, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (version == Volatile.Read(ref _loadVersion) && !IsDisposed)
            {
                SetState(VideoPlaybackState.Idle);
            }
            throw;
        }
        catch (Exception exception)
        {
            if (version == Volatile.Read(ref _loadVersion) && !IsDisposed)
            {
                var kind = exception is NotSupportedException ? VideoErrorKind.Unsupported : VideoErrorKind.Network;
                Fail(kind, exception.Message, exception);
            }
            return;
        }

        var pending = new PendingLoad();
        lock (_gate)
        {
            WaitForLeaseLocked();
            // A newer load or Dispose won the race while the source was materialized.
            if (version != _loadVersion || IsDisposed)
            {
                return;
            }
            var error = EnsureNative();
            if (error is not null)
            {
                Fail(VideoErrorKind.Aborted, error);
                return;
            }
            _pendingLoad?.Completion.TrySetResult();
            pending.Epoch = ++_openEpoch;
            _pendingLoad = pending;
            ReleaseLeaseLocked();
            _gpuImages.Clear();
            SetDuration(TimeSpan.Zero);
            SetNaturalSize(default);
            SetBuffered([]);
            SetSeekable(false);
            _events.Enqueue(new NativeEvent(MarkerOpen, pending.Epoch, 0));
            var result = VideoNativeInterop.PlayerOpen(_native, uri.IsFile ? uri.AbsoluteUri : uri.OriginalString);
            if (result != VideoNativeInterop.Ok)
            {
                _pendingLoad = null;
                var message = VideoNativeInterop.LastError(_native);
                Fail(result == VideoNativeInterop.ErrorUnsupported ? VideoErrorKind.Unsupported : VideoErrorKind.Aborted,
                    string.IsNullOrEmpty(message) ? $"tkv_player_open failed ({result})" : message);
                return;
            }
        }
        ScheduleDrain();

        if (!cancellationToken.CanBeCanceled)
        {
            await pending.Completion.Task.ConfigureAwait(false);
            return;
        }
        using (cancellationToken.UnsafeRegister(static (state, token) => ((PendingLoad)state!).Completion.TrySetCanceled(token), pending))
        {
            try
            {
                await pending.Completion.Task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                lock (_gate)
                {
                    WaitForLeaseLocked();
                    // Stop the native load: there is no "close" in the ABI, so drop the player; the next load recreates it.
                    if (ReferenceEquals(_pendingLoad, pending) && !IsDisposed)
                    {
                        _pendingLoad = null;
                        DestroyNativeLocked();
                        SetState(VideoPlaybackState.Idle);
                    }
                }
                throw;
            }
        }
    }

    public override void Play()
    {
        lock (_gate)
        {
            if (_native == 0 || IsDisposed)
            {
                return;
            }
            if (State == VideoPlaybackState.Ended)
            {
                VideoNativeInterop.PlayerSeek(_native, 0, 1);
            }
            VideoNativeInterop.PlayerPlay(_native);
        }
    }

    public override void Pause()
    {
        lock (_gate)
        {
            if (_native == 0 || IsDisposed)
            {
                return;
            }
            VideoNativeInterop.PlayerPause(_native);
        }
    }

    public override void Seek(TimeSpan position, VideoSeekMode mode = VideoSeekMode.Accurate)
    {
        var seconds = Math.Max(0, position.TotalSeconds);
        if (Duration > TimeSpan.Zero)
        {
            seconds = Math.Min(seconds, Duration.TotalSeconds);
        }
        lock (_gate)
        {
            if (_native == 0 || IsDisposed)
            {
                return;
            }
            VideoNativeInterop.PlayerSeek(_native, seconds, mode == VideoSeekMode.Accurate ? 1 : 0);
        }
    }

    public override unsafe bool TryAcquireFrame(in VideoFrameRequest request, out VideoFrameLease lease)
    {
        lease = default;
        var accept = (int)(request.Accepted & _backend.FrameKinds);
        var gpuType = VideoNativeInterop.GpuNone;
        if ((accept & VideoNativeInterop.FrameGpu) != 0)
        {
            var target = FrameTarget;
            var descriptor = _backend.GpuHandleDescriptor;
            if (!_options.PreferGpuFrames || descriptor is null || (target is not null && !target.GpuHandleTypes.Contains(descriptor)))
            {
                accept &= ~VideoNativeInterop.FrameGpu;
            }
            else
            {
                gpuType = _backend.NativeGpuHandleType;
            }
        }
        if (accept == 0)
        {
            return false;
        }
        // HostTime is on Avalonia's compositor clock, whose origin the shims cannot know; 0 asks for the frame due now.
        var nativeRequest = new TkvFrameRequest
        {
            HostTimeNs = 0,
            TargetWidth = Math.Max(0, request.TargetSize.Width),
            TargetHeight = Math.Max(0, request.TargetSize.Height),
            Accept = accept,
            GpuHandleType = gpuType,
        };
        TkvFrame frame;
        lock (_gate)
        {
            if (_native == 0 || IsDisposed || _leased || _lendingBlocked > 0)
            {
                return false;
            }
            var result = VideoNativeInterop.PlayerAcquireFrame(_native, &nativeRequest, &frame);
            if (result != VideoNativeInterop.Ok)
            {
                return false;
            }
            _leased = true;
            _leaseToken = frame.Token;
            _leaseThread = Environment.CurrentManagedThreadId;
            _leaseImageId = frame.Kind == VideoNativeInterop.FrameGpu ? frame.GpuImageId : 0;
        }
        var size = new PixelSize(frame.Width, frame.Height);
        var time = TimeSpan.FromSeconds(double.IsFinite(frame.Time) && frame.Time > 0 ? frame.Time : 0);
        var rotation = NormalizeRotation(frame.Rotation);
        if (frame.Kind == VideoNativeInterop.FrameGpu)
        {
            var image = GpuImage(frame.GpuImageId, frame.GpuHandle, frame.GpuHandleType);
            if (image is null)
            {
                ReleaseFrame(frame.Token);
                return false;
            }
            lease = new VideoFrameLease(this, frame.Token, size, time, image, frame.GpuImageId, PlatformGraphicsExternalImageFormat.B8G8R8A8UNorm,
                frame.KeyedMutex != 0 ? frame.AcquireKey : null, frame.ReleaseKey, rotation);
            return true;
        }
        if (frame.Pixels == 0 || frame.RowBytes <= 0 || frame.Height <= 0)
        {
            ReleaseFrame(frame.Token);
            return false;
        }
        var pixels = new ReadOnlySpan<byte>((void*)frame.Pixels, checked(frame.RowBytes * frame.Height));
        lease = new VideoFrameLease(this, frame.Token, size, time, pixels, frame.RowBytes, rotation, VideoCpuPixelFormat.Bgra8888);
        return true;
    }

    public void ReleaseFrame(long token)
    {
        lock (_gate)
        {
            if (!_leased || token != _leaseToken)
            {
                return;
            }
            _leased = false;
            _leaseImageId = 0;
            if (_native != 0)
            {
                VideoNativeInterop.PlayerReleaseFrame(_native, token);
            }
            Monitor.PulseAll(_gate);
        }
    }

    protected override void OnOutputGainChanged()
    {
        lock (_gate)
        {
            if (_native != 0)
            {
                VideoNativeInterop.PlayerSetVolume(_native, EffectiveGain);
            }
        }
    }

    protected override void OnLoopingChanged()
    {
        lock (_gate)
        {
            if (_native != 0)
            {
                VideoNativeInterop.PlayerSetLoop(_native, IsLooping ? 1 : 0);
            }
        }
    }

    protected override void OnPlaybackRateChanged(double oldRate)
    {
        int result;
        lock (_gate)
        {
            if (_native == 0)
            {
                return;
            }
            result = ApplyRateLocked();
        }
        if (result == VideoNativeInterop.ErrorUnsupported)
        {
            // Clamp toward normal speed: 2x and 0.5x are the widest every system player supports.
            var fallback = PlaybackRate > 1 ? Math.Min(PlaybackRate, 2) : Math.Max(PlaybackRate, 0.5);
            if (fallback == PlaybackRate)
            {
                fallback = 1;
            }
            if (!_rateWarningShown)
            {
                _rateWarningShown = true;
                Trace.TraceWarning($"TKCode.Arc.Video.Native: {_backend.Name} refused rate {PlaybackRate}; using {fallback}.");
            }
            PlaybackRate = fallback;
        }
    }

    protected override void OnFrameTargetChanged()
    {
        // The surface that shows the frames changed (re-attached, full screen, other render path): it has no copy of the
        // current frame, so the next TryAcquireFrame hands it out again even when paused. A changed adapter is applied by
        // the next LoadAsync (see the class remarks).
        lock (_gate)
        {
            if (_native == 0 || _redeliverMissing)
            {
                return;
            }
            try
            {
                VideoNativeInterop.PlayerRedeliverFrame(_native);
            }
            catch (EntryPointNotFoundException)
            {
                _redeliverMissing = true;
            }
        }
    }

    public bool TryGetTimelineSync(long gpuImageId, out IPlatformHandle semaphore, out ulong waitValue, out ulong signalValue)
    {
        semaphore = null!;
        waitValue = 0;
        signalValue = 0;
        lock (_gate)
        {
            if (_native == 0 || !_leased || _leaseImageId == 0 || _leaseImageId != gpuImageId || _timelineSyncMissing)
            {
                return false;
            }
            int result;
            nint handle;
            int type;
            try
            {
                result = VideoNativeInterop.PlayerTimelineSync(_native, _leaseToken, out handle, out type, out waitValue, out signalValue);
            }
            catch (EntryPointNotFoundException)
            {
                _timelineSyncMissing = true;
                return false;
            }
            if (result != VideoNativeInterop.Ok || handle == 0 || type != VideoNativeInterop.SemaphoreMetalSharedEvent)
            {
                return false;
            }
            if (!_semaphores.TryGetValue(handle, out var cached))
            {
                cached = new NativeSemaphoreHandle(handle, KnownPlatformGraphicsExternalSemaphoreHandleTypes.MetalSharedEvent);
                _semaphores[handle] = cached;
            }
            semaphore = cached;
            return true;
        }
    }

    protected override void Dispose(bool disposing)
    {
        PendingLoad? pending;
        lock (_gate)
        {
            Interlocked.Increment(ref _loadVersion);
            WaitForLeaseLocked();
            pending = _pendingLoad;
            _pendingLoad = null;
            DestroyNativeLocked();
            if (_self.IsAllocated)
            {
                _self.Free();
            }
        }
        pending?.Completion.TrySetCanceled();
    }

    /// <summary>Creates the native player when there is none, or when the frame target's adapter changed. Returns an error message on failure.</summary>
    private unsafe string? EnsureNative()
    {
        var luid = FrameTarget?.AdapterLuid;
        if (luid is { Length: not 8 })
        {
            luid = null;
        }
        if (_native != 0)
        {
            if (SameLuid(luid, _nativeLuid))
            {
                return null;
            }
            DestroyNativeLocked();
        }
        if (!_self.IsAllocated)
        {
            _self = GCHandle.Alloc(this);
        }
        int result;
        nint native;
        fixed (byte* luidPointer = luid)
        {
            result = VideoNativeInterop.PlayerCreate(&OnNativeEvent, GCHandle.ToIntPtr(_self), luid is null ? null : luidPointer, out native);
        }
        if (result != VideoNativeInterop.Ok || native == 0)
        {
            return $"tkv_player_create failed ({result})";
        }
        _native = native;
        _nativeLuid = luid is null ? null : (byte[])luid.Clone();
        VideoNativeInterop.PlayerSetVolume(native, EffectiveGain);
        VideoNativeInterop.PlayerSetLoop(native, IsLooping ? 1 : 0);
        if (PlaybackRate != 1)
        {
            ApplyRateLocked();
        }
        return null;
    }

    private int ApplyRateLocked() => VideoNativeInterop.PlayerSetRate(_native, PlaybackRate);

    private void DestroyNativeLocked()
    {
        if (_native == 0)
        {
            return;
        }
        ReleaseLeaseLocked();
        VideoNativeInterop.PlayerDestroy(_native);
        _native = 0;
        _nativeLuid = null;
        _gpuImages.Clear();
        _semaphores.Clear();
    }

    /// <summary>
    /// Waits, holding the gate between checks, until a frame lent to another thread is returned (see the class remarks). Gives
    /// up after <see cref="LeaseWaitMilliseconds"/>: a lease that is never disposed is a caller bug and must not hang a load.
    /// </summary>
    private void WaitForLeaseLocked()
    {
        if (!_leased || _leaseThread == Environment.CurrentManagedThreadId)
        {
            return;
        }
        _lendingBlocked++;
        try
        {
            var deadline = Environment.TickCount64 + LeaseWaitMilliseconds;
            while (_leased)
            {
                var left = deadline - Environment.TickCount64;
                if (left <= 0)
                {
                    Trace.TraceWarning("TKCode.Arc.Video.Native: a leased frame was not returned in time; releasing it anyway.");
                    return;
                }
                Monitor.Wait(_gate, (int)left);
            }
        }
        finally
        {
            _lendingBlocked--;
        }
    }

    private void ReleaseLeaseLocked()
    {
        if (_leased && _native != 0)
        {
            VideoNativeInterop.PlayerReleaseFrame(_native, _leaseToken);
        }
        _leased = false;
        _leaseImageId = 0;
    }

    private NativeGpuImageHandle? GpuImage(long imageId, nint handle, int handleType)
    {
        var descriptor = NativeVideoBackend.HandleDescriptor(handleType);
        if (descriptor is null || handle == 0)
        {
            return null;
        }
        lock (_gate)
        {
            if (_gpuImages.TryGetValue(imageId, out var image) && image.Handle == handle && image.HandleDescriptor == descriptor)
            {
                return image;
            }
            image = new NativeGpuImageHandle(handle, descriptor);
            _gpuImages[imageId] = image;
            return image;
        }
    }

    private static int NormalizeRotation(int degrees)
    {
        var normalized = ((degrees % 360) + 360) % 360;
        return normalized switch
        {
            90 or 180 or 270 => normalized,
            _ => 0,
        };
    }

    private static bool SameLuid(byte[]? a, byte[]? b)
    {
        if (a is null || b is null)
        {
            return a is null && b is null;
        }
        return a.AsSpan().SequenceEqual(b);
    }

    // ---- Events.

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnNativeEvent(nint context, int kind, double a, double b)
    {
        try
        {
            if (context == 0 || GCHandle.FromIntPtr(context).Target is not NativeVideoPlayer player)
            {
                return;
            }
            player._events.Enqueue(new NativeEvent(kind, a, b));
            player.ScheduleDrain();
        }
        catch (Exception exception)
        {
            // An exception must not unwind into native code.
            Trace.TraceError($"TKCode.Arc.Video.Native: event callback failed: {exception}");
        }
    }

    private void ScheduleDrain()
    {
        if (Interlocked.CompareExchange(ref _drainScheduled, 1, 0) == 0)
        {
            ThreadPool.UnsafeQueueUserWorkItem(s_drain, this);
        }
    }

    private void Drain()
    {
        while (true)
        {
            while (_events.TryDequeue(out var e))
            {
                if (IsDisposed)
                {
                    continue;
                }
                try
                {
                    Handle(e);
                }
                catch (Exception exception)
                {
                    Trace.TraceError($"TKCode.Arc.Video.Native: handling event {e.Kind} failed: {exception}");
                }
            }
            Volatile.Write(ref _drainScheduled, 0);
            if (_events.IsEmpty || Interlocked.CompareExchange(ref _drainScheduled, 1, 0) != 0)
            {
                return;
            }
        }
    }

    private void Handle(NativeEvent e)
    {
        // Events queued before the latest Open marker belong to media that has been replaced.
        if (e.Kind != MarkerOpen && _drainEpoch != Volatile.Read(ref _openEpoch))
        {
            return;
        }
        switch (e.Kind)
        {
            case MarkerOpen:
                _drainEpoch = (int)e.A;
                _failureRaised = false;
                break;
            case VideoNativeInterop.EventState:
                HandleState((int)e.A);
                break;
            case VideoNativeInterop.EventPosition:
                RaisePositionChanged();
                break;
            case VideoNativeInterop.EventDuration:
                SetDuration(double.IsFinite(e.A) && e.A > 0 ? TimeSpan.FromSeconds(e.A) : TimeSpan.Zero);
                break;
            case VideoNativeInterop.EventSize:
                SetNaturalSize(new PixelSize(Math.Max(0, (int)Math.Round(e.A)), Math.Max(0, (int)Math.Round(e.B))));
                break;
            case VideoNativeInterop.EventBuffered:
                SetBuffered(ReadBuffered());
                break;
            case VideoNativeInterop.EventFrame:
                RaiseFrameAvailable();
                break;
            case VideoNativeInterop.EventError:
                // Shims send ERROR then STATE(FAILED); tolerate either order and duplicates: Failed is raised once per media.
                if (!_failureRaised)
                {
                    _failureRaised = true;
                    Fail(ErrorKind((int)e.A), ReadLastError("playback failed"));
                }
                CompleteLoad();
                break;
            case VideoNativeInterop.EventEnded:
                SetState(VideoPlaybackState.Ended);
                RaisePositionChanged();
                break;
            case VideoNativeInterop.EventSeekable:
                SetSeekable(e.A != 0);
                break;
        }
    }

    private void HandleState(int state)
    {
        switch (state)
        {
            case 0:
                SetState(VideoPlaybackState.Idle);
                break;
            case 1:
                SetState(VideoPlaybackState.Loading);
                break;
            case 2:
                SetState(VideoPlaybackState.Paused);
                CompleteLoad();
                break;
            case 3:
                SetState(VideoPlaybackState.Playing);
                CompleteLoad();
                break;
            case 4:
                SetState(VideoPlaybackState.Buffering);
                break;
            case 5:
                SetState(VideoPlaybackState.Ended);
                break;
            case 6:
                // The Failed event (with its kind) comes from TKV_EVENT_ERROR, which may arrive before or after this.
                SetState(VideoPlaybackState.Failed);
                CompleteLoad();
                break;
        }
    }

    /// <summary>Completes the pending load once an event of its own media has settled the state.</summary>
    private void CompleteLoad()
    {
        PendingLoad? pending;
        lock (_gate)
        {
            pending = _pendingLoad;
            if (pending is null || pending.Epoch != _drainEpoch)
            {
                return;
            }
            _pendingLoad = null;
        }
        pending.Completion.TrySetResult();
    }

    private unsafe IReadOnlyList<VideoTimeRange> ReadBuffered()
    {
        var ranges = stackalloc double[MaxBufferedRanges * 2];
        int count;
        lock (_gate)
        {
            if (_native == 0)
            {
                return [];
            }
            count = VideoNativeInterop.PlayerBuffered(_native, ranges, MaxBufferedRanges);
        }
        if (count <= 0)
        {
            return [];
        }
        var list = new VideoTimeRange[Math.Min(count, MaxBufferedRanges)];
        for (var i = 0; i < list.Length; i++)
        {
            var start = Math.Max(0, ranges[2 * i]);
            var end = Math.Max(start, ranges[2 * i + 1]);
            list[i] = new VideoTimeRange(TimeSpan.FromSeconds(start), TimeSpan.FromSeconds(end));
        }
        return list;
    }

    private string ReadLastError(string fallback)
    {
        lock (_gate)
        {
            var message = VideoNativeInterop.LastError(_native);
            return string.IsNullOrEmpty(message) ? fallback : message;
        }
    }

    private static VideoErrorKind ErrorKind(int kind) => kind switch
    {
        0 => VideoErrorKind.Aborted,
        1 => VideoErrorKind.Network,
        3 => VideoErrorKind.Unsupported,
        _ => VideoErrorKind.Decode,
    };

    private readonly record struct NativeEvent(int Kind, double A, double B);

    private sealed class PendingLoad
    {
        public int Epoch;
        public readonly TaskCompletionSource Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
