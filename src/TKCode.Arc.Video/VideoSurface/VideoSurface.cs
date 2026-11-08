using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;

namespace TKCode.Arc;

/// <summary>Which path a <see cref="VideoSurface"/> draws frames through.</summary>
public enum VideoRenderPath
{
    /// <summary>Nothing drawn yet (no player, or not attached).</summary>
    None,

    /// <summary>GPU images imported into the compositor (<c>ICompositionGpuInterop</c>).</summary>
    GpuInterop,

    /// <summary>CPU pixels copied into a <c>WriteableBitmap</c>.</summary>
    Bitmap,
}

/// <summary>
/// Shows the frames of an <see cref="IVideoPlayer"/> inside the Avalonia composition tree, so other controls can be drawn over
/// it, and it can be clipped, transformed and faded. Has no controls of its own; <see cref="VideoPlayer"/> adds them.
/// </summary>
/// <remarks>
/// While the player plays (or buffers) and the surface is attached and visible, the surface asks for a frame on every
/// compositor frame (<see cref="TopLevel.RequestAnimationFrame"/>); paused, hidden or detached it does no work. A paused
/// player's new frame (after loading or seeking) arrives through <see cref="IVideoPlayer.FrameAvailable"/> and is fetched once.
/// GPU frames are imported into the compositor when it supports their handle type (<see cref="VideoRenderPath.GpuInterop"/>);
/// CPU frames are copied into one bitmap (<see cref="VideoRenderPath.Bitmap"/>). Frames are requested at the size the picture
/// is drawn at, so a large video in a small surface is not copied at full size.
/// </remarks>
public class VideoSurface : Control
{
    /// <summary>Defines the <see cref="Player"/> property.</summary>
    public static readonly StyledProperty<IVideoPlayer?> PlayerProperty =
        AvaloniaProperty.Register<VideoSurface, IVideoPlayer?>(nameof(Player));

    /// <summary>Defines the <see cref="Stretch"/> property.</summary>
    public static readonly StyledProperty<Stretch> StretchProperty =
        AvaloniaProperty.Register<VideoSurface, Stretch>(nameof(Stretch), Stretch.Uniform);

    /// <summary>Defines the <see cref="LetterboxBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> LetterboxBrushProperty =
        AvaloniaProperty.Register<VideoSurface, IBrush?>(nameof(LetterboxBrush), Brushes.Black);

    /// <summary>Defines the <see cref="CornerRadius"/> property.</summary>
    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<VideoSurface, CornerRadius>(nameof(CornerRadius));

    /// <summary>Defines the <see cref="PreferBitmapPath"/> property.</summary>
    public static readonly StyledProperty<bool> PreferBitmapPathProperty =
        AvaloniaProperty.Register<VideoSurface, bool>(nameof(PreferBitmapPath));

    /// <summary>Defines the <see cref="RenderPath"/> property.</summary>
    public static readonly DirectProperty<VideoSurface, VideoRenderPath> RenderPathProperty =
        AvaloniaProperty.RegisterDirect<VideoSurface, VideoRenderPath>(nameof(RenderPath), static s => s.RenderPath);

    // GPU updates in flight at once. An update's task completes only after the compositor's next frame, so with one in flight
    // every other compositor frame would be skipped (measured on Metal: 60 fps content showed 68 of 120 frames); with two,
    // one is being copied while the next is queued (120 of 120). Past this the frame is dropped, so the queue stays short.
    private const int MaxPendingUpdates = 2;

    // More imported GPU images than any backend's texture pool; past it the oldest are released.
    private const int MaxImportedImages = 8;

    private readonly Action<TimeSpan> _onFrame;
    private VideoRenderPath _renderPath;

    // Frame loop.
    private TopLevel? _topLevel;
    private IVideoPlayer? _subscribed;
    private VideoFrameTarget? _appliedTarget;
    private bool _frameRequested;
    private bool _wantOneFrame;
    private bool _waitingForLayout;
    private int _attachGeneration;
    private PixelSize _lastTargetSize;

    // The shown frame (either path).
    private PixelSize _frameSize;
    private int _rotation;

    // Bitmap path.
    private WriteableBitmap? _bitmap;
    private VideoCpuPixelFormat _bitmapFormat;

    // GPU path.
    private ICompositionGpuInterop? _interop;
    private Compositor? _compositor;
    private bool _interopResolved;
    private bool _gpuDisabled;
    private VideoSurfaceGpuHost? _gpuHost;
    private CompositionSurfaceVisual? _surfaceVisual;
    private CompositionDrawingSurface? _drawingSurface;
    private readonly List<ImportedImage> _imported = [];
    private readonly List<(IntPtr Handle, ICompositionImportedGpuSemaphore Semaphore)> _semaphores = [];
    private int _gpuUpdatesPending;

    private int _gpuGeneration;

    static VideoSurface()
    {
        AffectsRender<VideoSurface>(StretchProperty, LetterboxBrushProperty, CornerRadiusProperty);
    }

    /// <summary>Creates a surface.</summary>
    public VideoSurface()
    {
        _onFrame = OnFrame;
    }

    /// <summary>The player whose frames are shown; created and disposed by the caller.</summary>
    public IVideoPlayer? Player
    {
        get => GetValue(PlayerProperty);
        set => SetValue(PlayerProperty, value);
    }

    /// <summary>How the picture fills the surface: <see cref="Stretch.Uniform"/> (bars), <see cref="Stretch.UniformToFill"/> (cropped), <see cref="Stretch.Fill"/> or <see cref="Stretch.None"/>.</summary>
    public Stretch Stretch
    {
        get => GetValue(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    /// <summary>The brush behind the picture, seen as bars when the picture does not fill the surface.</summary>
    public IBrush? LetterboxBrush
    {
        get => GetValue(LetterboxBrushProperty);
        set => SetValue(LetterboxBrushProperty, value);
    }

    /// <summary>Rounds the corners of the picture and the bars.</summary>
    public CornerRadius CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    /// <summary>Draws CPU frames through a bitmap even when GPU import is available (for debugging and driver problems).</summary>
    public bool PreferBitmapPath
    {
        get => GetValue(PreferBitmapPathProperty);
        set => SetValue(PreferBitmapPathProperty, value);
    }

    /// <summary>The path frames are drawn through now.</summary>
    public VideoRenderPath RenderPath
    {
        get => _renderPath;
        protected set => SetAndRaise(RenderPathProperty, ref _renderPath, value);
    }

    /// <summary>Called with every CPU frame copied into the bitmap; tests read the shown picture through it.</summary>
    internal FrameCopiedHandler? FrameCopied { get; set; }

    /// <summary>The bitmap of the CPU path; null on the GPU path or before the first frame.</summary>
    internal WriteableBitmap? Bitmap => _bitmap;

    /// <summary>The device-pixel size asked of the player last.</summary>
    internal PixelSize LastTargetSize => _lastTargetSize;

    /// <summary>How many compositor frame callbacks have run; tests wait on it.</summary>
    internal int FrameCallbackCount { get; private set; }

    /// <summary>Whether a compositor frame callback is outstanding.</summary>
    internal bool IsFrameRequested => _frameRequested;

    /// <summary>Where the picture is drawn, in the surface's coordinates (after <see cref="Stretch"/>, in display orientation).</summary>
    internal Rect PictureRect => ComputePictureRect(Bounds.Size, ContentSize(), Stretch);

    /// <summary>The rectangle a picture of <paramref name="content"/> size occupies in <paramref name="bounds"/> under <paramref name="stretch"/>, centred.</summary>
    internal static Rect ComputePictureRect(Size bounds, Size content, Stretch stretch)
    {
        if (content.Width <= 0 || content.Height <= 0 || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return new Rect(bounds);
        }
        Size size;
        switch (stretch)
        {
            case Stretch.None:
                size = content;
                break;
            case Stretch.Fill:
                size = bounds;
                break;
            case Stretch.UniformToFill:
            {
                var scale = Math.Max(bounds.Width / content.Width, bounds.Height / content.Height);
                size = new Size(content.Width * scale, content.Height * scale);
                break;
            }
            default:
            {
                var scale = Math.Min(bounds.Width / content.Width, bounds.Height / content.Height);
                size = new Size(content.Width * scale, content.Height * scale);
                break;
            }
        }
        return new Rect((bounds.Width - size.Width) / 2, (bounds.Height - size.Height) / 2, size.Width, size.Height);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);
        _attachGeneration++;
        if (Player is { } player)
        {
            Subscribe(player);
        }
        // The first frame is asked for once the compositor's GPU support is known (a round trip to the render thread), so it
        // is not fetched twice: the frame target set then makes the player hand out its current frame again.
        ResolveInterop(_attachGeneration);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attachGeneration++;
        _topLevel = null;
        WaitForLayout(false);
        Unsubscribe();
        TearDownGpu();
        _interop = null;
        _compositor = null;
        _interopResolved = false;
        // The bitmap stays: a surface moved to another parent (full screen) shows its paused frame at once.
        if (RenderPath == VideoRenderPath.GpuInterop)
        {
            RenderPath = VideoRenderPath.None;
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PlayerProperty)
        {
            Unsubscribe();
            ReleaseFrame();
            if (_topLevel is not null && change.GetNewValue<IVideoPlayer?>() is { } player)
            {
                Subscribe(player);
                RequestOneFrame();
            }
        }
        else if (change.Property == BoundsProperty || change.Property == StretchProperty)
        {
            LayoutGpuVisual();
            if (TargetSizeChanged())
            {
                RequestOneFrame();
            }
        }
        else if (change.Property == CornerRadiusProperty)
        {
            UpdateHostClip();
        }
        else if (change.Property == IsVisibleProperty)
        {
            UpdateLoop();
        }
        else if (change.Property == PreferBitmapPathProperty)
        {
            if (change.GetNewValue<bool>())
            {
                TearDownGpu();
                if (RenderPath == VideoRenderPath.GpuInterop)
                {
                    RenderPath = VideoRenderPath.None;
                }
            }
            ApplyFrameTarget();
            RequestOneFrame();
        }
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        _gpuHost?.Measure(availableSize);
        return default;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        _gpuHost?.Arrange(new Rect(finalSize));
        return finalSize;
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }
        using var clip = CornerRadius == default ? context.PushClip(bounds) : context.PushClip(new RoundedRect(bounds, CornerRadius));
        if (LetterboxBrush is { } brush)
        {
            context.DrawRectangle(brush, null, bounds);
        }
        if (RenderPath != VideoRenderPath.Bitmap || _bitmap is not { } bitmap)
        {
            return;
        }
        var picture = PictureRect;
        var source = new Rect(0, 0, _frameSize.Width, _frameSize.Height);
        if (_rotation == 0)
        {
            context.DrawImage(bitmap, source, picture);
            return;
        }
        // Draw the unrotated frame centred on the picture, then turn it about that centre.
        var center = picture.Center;
        var drawn = _rotation is 90 or 270
            ? new Rect(center.X - picture.Height / 2, center.Y - picture.Width / 2, picture.Height, picture.Width)
            : picture;
        var turn = Matrix.CreateTranslation(-center.X, -center.Y) * Matrix.CreateRotation(_rotation * Math.PI / 180) *
                   Matrix.CreateTranslation(center.X, center.Y);
        using (context.PushTransform(turn))
        {
            context.DrawImage(bitmap, source, drawn);
        }
    }

    // ---------------------------------------------------------------- player

    private void Subscribe(IVideoPlayer player)
    {
        if (ReferenceEquals(_subscribed, player))
        {
            return;
        }
        Unsubscribe();
        _subscribed = player;
        player.StateChanged += OnPlayerStateChanged;
        player.FrameAvailable += OnPlayerFrameAvailable;
        player.NaturalSizeChanged += OnPlayerNaturalSizeChanged;
        ApplyFrameTarget();
    }

    private void Unsubscribe()
    {
        if (_subscribed is not { } player)
        {
            return;
        }
        player.StateChanged -= OnPlayerStateChanged;
        player.FrameAvailable -= OnPlayerFrameAvailable;
        player.NaturalSizeChanged -= OnPlayerNaturalSizeChanged;
        if (_appliedTarget is not null && ReferenceEquals(player.FrameTarget, _appliedTarget))
        {
            player.FrameTarget = null;
        }
        _appliedTarget = null;
        _subscribed = null;
        _wantOneFrame = false;
        UpdateLoop();
    }

    private void OnPlayerStateChanged(object? sender, EventArgs e) => UpdateLoop();

    private void OnPlayerFrameAvailable(object? sender, EventArgs e) => RequestOneFrame();

    private void OnPlayerNaturalSizeChanged(object? sender, EventArgs e)
    {
        LayoutGpuVisual();
        InvalidateVisual();
        RequestOneFrame();
    }

    /// <summary>Drops the shown frame (the player changed): the bitmap, the GPU images and the path.</summary>
    private void ReleaseFrame()
    {
        TearDownGpu();
        _bitmap?.Dispose();
        _bitmap = null;
        _frameSize = default;
        _rotation = 0;
        _lastTargetSize = default;
        RenderPath = VideoRenderPath.None;
        InvalidateVisual();
    }

    // ---------------------------------------------------------------- frame loop

    private void RequestOneFrame()
    {
        if (_subscribed is null)
        {
            return;
        }
        _wantOneFrame = true;
        UpdateLoop();
    }

    private bool WantsFrames => _subscribed is { } player && (_wantOneFrame || player.State is VideoPlaybackState.Playing or VideoPlaybackState.Buffering);

    private void UpdateLoop()
    {
        var wanted = WantsFrames;
        var canShow = _topLevel is not null && IsVisible && IsEffectivelyVisible;
        // Hidden by an ancestor: nothing here says when that ends, but showing it again always runs a layout pass.
        WaitForLayout(wanted && _topLevel is not null && IsVisible && !IsEffectivelyVisible);
        if (wanted && canShow && !_frameRequested)
        {
            _frameRequested = true;
            _topLevel!.RequestAnimationFrame(_onFrame);
        }
    }

    private void WaitForLayout(bool wait)
    {
        if (wait == _waitingForLayout)
        {
            return;
        }
        _waitingForLayout = wait;
        if (wait)
        {
            LayoutUpdated += OnLayoutUpdated;
        }
        else
        {
            LayoutUpdated -= OnLayoutUpdated;
        }
    }

    private void OnLayoutUpdated(object? sender, EventArgs e) => UpdateLoop();

    private void OnFrame(TimeSpan hostTime)
    {
        _frameRequested = false;
        FrameCallbackCount++;
        if (_subscribed is not { } player || _topLevel is null || !WantsFrames)
        {
            return;
        }
        if (!IsVisible || !IsEffectivelyVisible)
        {
            UpdateLoop();
            return;
        }
        // Showing a frame may ask for another one (its size or rotation changed the target), so clear the wish first.
        var wantedOne = _wantOneFrame;
        _wantOneFrame = false;
        if (!TryShowFrame(player, hostTime))
        {
            _wantOneFrame |= wantedOne;
        }
        UpdateLoop();
    }

    /// <summary>Asks the player for a frame and shows it. False when the request should be repeated (nothing could be asked yet).</summary>
    private bool TryShowFrame(IVideoPlayer player, TimeSpan hostTime)
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            // Not laid out: the size change asks again.
            return true;
        }
        if (_interop is { IsLost: true })
        {
            FallBackToBitmap("the compositor's GPU device was lost", null);
        }
        var gpu = UsesGpu(player);
        if (gpu && _gpuUpdatesPending >= MaxPendingUpdates)
        {
            // The compositor has not taken the previous images yet: skip this frame (the backend keeps time, so a later frame
            // simply shows a later picture). A completing update asks again.
            return false;
        }
        var target = TargetPixelSize();
        _lastTargetSize = target;
        var request = new VideoFrameRequest(hostTime, target, gpu ? VideoFrameKinds.CpuBgra | VideoFrameKinds.Gpu : VideoFrameKinds.CpuBgra);
        if (!player.TryAcquireFrame(in request, out var lease))
        {
            return true;
        }
        try
        {
            if (lease.Kind == VideoFrameKinds.Gpu)
            {
                if (gpu)
                {
                    ShowGpuFrame(lease);
                }
            }
            else if (lease.Pixels.Length > 0)
            {
                ShowCpuFrame(lease);
            }
        }
        catch (Exception ex) when (lease.Kind == VideoFrameKinds.Gpu)
        {
            FallBackToBitmap("importing or presenting a GPU frame failed", ex);
        }
        finally
        {
            lease.Dispose();
        }
        return true;
    }

    private Size ContentSize()
    {
        if (_subscribed?.NaturalSize is { Width: > 0, Height: > 0 } natural)
        {
            return new Size(natural.Width, natural.Height);
        }
        if (_frameSize is { Width: > 0, Height: > 0 })
        {
            return _rotation is 90 or 270 ? new Size(_frameSize.Height, _frameSize.Width) : new Size(_frameSize.Width, _frameSize.Height);
        }
        return default;
    }

    /// <summary>The picture rectangle in device pixels, in the frame's own orientation (before rotation).</summary>
    private PixelSize TargetPixelSize()
    {
        var content = ContentSize();
        if (content.Width <= 0 || content.Height <= 0)
        {
            return default;
        }
        var picture = ComputePictureRect(Bounds.Size, content, Stretch);
        var scaling = _topLevel?.RenderScaling ?? 1;
        var width = Math.Max(1, (int)Math.Ceiling(picture.Width * scaling - 0.01));
        var height = Math.Max(1, (int)Math.Ceiling(picture.Height * scaling - 0.01));
        return _rotation is 90 or 270 ? new PixelSize(height, width) : new PixelSize(width, height);
    }

    private bool TargetSizeChanged() => _subscribed is not null && TargetPixelSize() != _lastTargetSize;

    // ---------------------------------------------------------------- bitmap path

    private void ShowCpuFrame(in VideoFrameLease lease)
    {
        var size = lease.Size;
        if (size.Width <= 0 || size.Height <= 0 || lease.RowBytes < size.Width * 4 || lease.Pixels.Length < (size.Height - 1) * lease.RowBytes + size.Width * 4)
        {
            return;
        }
        if (_bitmap is null || _bitmap.PixelSize != size || _bitmapFormat != lease.CpuPixelFormat)
        {
            _bitmap?.Dispose();
            var format = lease.CpuPixelFormat == VideoCpuPixelFormat.Rgba8888 ? PixelFormat.Rgba8888 : PixelFormat.Bgra8888;
            _bitmap = new WriteableBitmap(size, new Vector(96, 96), format, AlphaFormat.Opaque);
            _bitmapFormat = lease.CpuPixelFormat;
        }
        var rowLength = size.Width * 4;
        using (var buffer = _bitmap.Lock())
        {
            var pixels = lease.Pixels;
            if (buffer.RowBytes == lease.RowBytes)
            {
                var length = (size.Height - 1) * lease.RowBytes + rowLength;
                pixels[..length].CopyTo(NativeBytes(buffer.Address, length));
            }
            else
            {
                for (var y = 0; y < size.Height; y++)
                {
                    pixels.Slice(y * lease.RowBytes, rowLength).CopyTo(NativeBytes(buffer.Address + y * buffer.RowBytes, rowLength));
                }
            }
        }
        FrameCopied?.Invoke(lease.Pixels, size, lease.RowBytes);
        var rotation = NormalizeRotation(lease.RotationDegrees);
        var layoutChanged = rotation != _rotation || size != _frameSize;
        _frameSize = size;
        _rotation = rotation;
        if (RenderPath == VideoRenderPath.GpuInterop)
        {
            TearDownGpu();
        }
        RenderPath = VideoRenderPath.Bitmap;
        if (layoutChanged && TargetSizeChanged())
        {
            _wantOneFrame = true;
        }
        InvalidateVisual();
    }

    // A span over the bitmap's locked memory, without an unsafe block (the package does not allow them).
    private static Span<byte> NativeBytes(IntPtr address, int length) =>
        MemoryMarshal.CreateSpan(ref Unsafe.AddByteOffset(ref Unsafe.NullRef<byte>(), address), length);

    private static int NormalizeRotation(int degrees)
    {
        var turned = ((degrees % 360) + 360) % 360;
        return turned switch
        {
            >= 45 and < 135 => 90,
            >= 135 and < 225 => 180,
            >= 225 and < 315 => 270,
            _ => 0,
        };
    }

    // ---------------------------------------------------------------- GPU path

    private async void ResolveInterop(int generation)
    {
        ICompositionGpuInterop? interop = null;
        Compositor? compositor = null;
        try
        {
            compositor = ElementComposition.GetElementVisual(this)?.Compositor
                         ?? (_topLevel is { } top ? ElementComposition.GetElementVisual(top)?.Compositor : null);
            if (compositor is not null)
            {
                interop = await compositor.TryGetCompositionGpuInterop();
            }
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("VideoSurface: asking the compositor for GPU interop failed; frames are drawn through a bitmap. " + ex.Message);
            interop = null;
        }
        if (generation != _attachGeneration)
        {
            return;
        }
        _compositor = compositor;
        _interop = interop;
        _interopResolved = true;
        ApplyFrameTarget();
        RequestOneFrame();
    }

    private IReadOnlyList<string> GpuHandleTypes(IVideoPlayer player)
    {
        if (_interop is not { IsLost: false } interop || _gpuDisabled || PreferBitmapPath || _compositor is null
            || (player.Backend.FrameKinds & VideoFrameKinds.Gpu) == 0)
        {
            return [];
        }
        var backendTypes = player.Backend.GpuHandleTypes;
        var types = new List<string>();
        foreach (var type in interop.SupportedImageHandleTypes)
        {
            if (backendTypes.Contains(type) && CanSynchronize(interop, type, player))
            {
                types.Add(type);
            }
        }
        return types;
    }

    // A handle type is offered only when some way of synchronising its images works here: the compositor's automatic sync
    // or a keyed mutex (Direct3D 11 through ANGLE), or a timeline semaphore the player supplies (Metal imports IOSurfaces
    // only that way).
    private static bool CanSynchronize(ICompositionGpuInterop interop, string handleType, IVideoPlayer player)
    {
        CompositionGpuImportedImageSynchronizationCapabilities capabilities;
        try
        {
            capabilities = interop.GetSynchronizationCapabilities(handleType);
        }
        catch (Exception)
        {
            return false;
        }
        const CompositionGpuImportedImageSynchronizationCapabilities direct =
            CompositionGpuImportedImageSynchronizationCapabilities.Automatic | CompositionGpuImportedImageSynchronizationCapabilities.KeyedMutex;
        if ((capabilities & direct) != 0)
        {
            return true;
        }
        return (capabilities & CompositionGpuImportedImageSynchronizationCapabilities.TimelineSemaphores) != 0 && player is IVideoTimelineSync;
    }

    private bool UsesGpu(IVideoPlayer player) => _appliedTarget is { AcceptsGpu: true } && GpuHandleTypes(player).Count > 0;

    /// <summary>Tells the player where its frames go: the handle types this compositor imports and its adapter.</summary>
    private void ApplyFrameTarget()
    {
        if (_subscribed is not { } player || !_interopResolved)
        {
            return;
        }
        var target = new VideoFrameTarget
        {
            GpuHandleTypes = GpuHandleTypes(player),
            AdapterLuid = _interop?.DeviceLuid,
            AdapterUuid = _interop?.DeviceUuid,
        };
        if (_appliedTarget is { } applied && applied.GpuHandleTypes.SequenceEqual(target.GpuHandleTypes)
            && ReferenceEquals(applied.AdapterLuid, target.AdapterLuid) && ReferenceEquals(applied.AdapterUuid, target.AdapterUuid)
            && ReferenceEquals(player.FrameTarget, applied))
        {
            return;
        }
        _appliedTarget = target;
        player.FrameTarget = target;
    }

    private void ShowGpuFrame(in VideoFrameLease lease)
    {
        if (_interop is not { } interop || _compositor is null || lease.GpuImage is not { } handle)
        {
            return;
        }
        var image = Import(interop, handle, lease);
        if (image.IsLost)
        {
            throw new InvalidOperationException("The imported GPU image is lost.");
        }
        EnsureGpuVisual();
        var rotation = NormalizeRotation(lease.RotationDegrees);
        var layoutChanged = rotation != _rotation || lease.Size != _frameSize;
        _frameSize = lease.Size;
        _rotation = rotation;
        LayoutGpuVisual();
        var update = Present(interop, image, handle.HandleDescriptor, lease);
        _gpuUpdatesPending++;
        ObserveUpdate(update, _gpuGeneration);
        if (RenderPath != VideoRenderPath.GpuInterop)
        {
            RenderPath = VideoRenderPath.GpuInterop;
            _bitmap?.Dispose();
            _bitmap = null;
            InvalidateVisual();
        }
        if (layoutChanged && TargetSizeChanged())
        {
            _wantOneFrame = true;
        }
    }

    private Task Present(ICompositionGpuInterop interop, ICompositionImportedGpuImage image, string? handleType, in VideoFrameLease lease)
    {
        var surface = _drawingSurface!;
        if (lease.KeyedMutexAcquireKey is { } acquire)
        {
            return surface.UpdateWithKeyedMutexAsync(image, acquire, lease.KeyedMutexReleaseKey);
        }
        if (_subscribed is IVideoTimelineSync sync && sync.TryGetTimelineSync(lease.GpuImageId, out var semaphoreHandle, out var waitValue, out var signalValue))
        {
            var semaphore = ImportSemaphore(interop, semaphoreHandle);
            return surface.UpdateWithTimelineSemaphoresAsync(image, semaphore, waitValue, semaphore, signalValue);
        }
        var capabilities = handleType is null ? default : interop.GetSynchronizationCapabilities(handleType);
        if ((capabilities & CompositionGpuImportedImageSynchronizationCapabilities.Automatic) == 0)
        {
            throw new NotSupportedException($"The compositor cannot synchronise {handleType} images without a keyed mutex or a timeline semaphore ({capabilities}).");
        }
        return surface.UpdateAsync(image);
    }

    private ICompositionImportedGpuSemaphore ImportSemaphore(ICompositionGpuInterop interop, IPlatformHandle handle)
    {
        foreach (var (key, imported) in _semaphores)
        {
            if (key == handle.Handle)
            {
                return imported;
            }
        }
        var semaphore = interop.ImportSemaphore(handle);
        _semaphores.Add((handle.Handle, semaphore));
        return semaphore;
    }

    private ICompositionImportedGpuImage Import(ICompositionGpuInterop interop, IPlatformHandle handle, in VideoFrameLease lease)
    {
        for (var i = 0; i < _imported.Count; i++)
        {
            var entry = _imported[i];
            if (entry.Id != lease.GpuImageId)
            {
                continue;
            }
            if (entry.Size == lease.Size && entry.Format == lease.GpuFormat)
            {
                // Most recently used last, so the first entry is the one to release when the cache is full.
                if (i != _imported.Count - 1)
                {
                    _imported.RemoveAt(i);
                    _imported.Add(entry);
                }
                return entry.Image;
            }
            // The backend reused the id for a new texture.
            _imported.RemoveAt(i);
            DisposeImported(entry.Image);
            break;
        }
        var image = interop.ImportImage(handle, new PlatformGraphicsExternalImageProperties
        {
            Width = lease.Size.Width,
            Height = lease.Size.Height,
            Format = lease.GpuFormat,
            // IOSurfaces (CVPixelBuffers) and the D3D11 textures Media Foundation writes store the top row first. Other
            // handle types keep Avalonia's default (bottom-left origin, as OpenGL textures).
            TopLeftOrigin = handle.HandleDescriptor == KnownPlatformGraphicsExternalImageHandleTypes.IOSurfaceRef
                || handle.HandleDescriptor == KnownPlatformGraphicsExternalImageHandleTypes.D3D11TextureNtHandle,
        });
        if (_imported.Count >= MaxImportedImages)
        {
            // The least recently used is not on screen: the images of pending updates are the newest entries.
            DisposeImported(_imported[0].Image);
            _imported.RemoveAt(0);
        }
        _imported.Add(new ImportedImage(lease.GpuImageId, lease.Size, lease.GpuFormat, image));
        return image;
    }

    private async void ObserveUpdate(Task update, int generation)
    {
        try
        {
            await update;
        }
        catch (Exception ex)
        {
            if (generation == _gpuGeneration)
            {
                FallBackToBitmap("presenting a GPU frame failed", ex);
            }
            return;
        }
        if (generation != _gpuGeneration)
        {
            return;
        }
        _gpuUpdatesPending = Math.Max(0, _gpuUpdatesPending - 1);
        UpdateLoop();
    }

    private void EnsureGpuVisual()
    {
        if (_surfaceVisual is not null || _compositor is not { } compositor)
        {
            return;
        }
        if (_gpuHost is null)
        {
            _gpuHost = new VideoSurfaceGpuHost();
            VisualChildren.Add(_gpuHost);
            _gpuHost.Measure(Bounds.Size);
            _gpuHost.Arrange(new Rect(Bounds.Size));
        }
        _drawingSurface = compositor.CreateDrawingSurface();
        _surfaceVisual = compositor.CreateSurfaceVisual();
        _surfaceVisual.Surface = _drawingSurface;
        ElementComposition.SetElementChildVisual(_gpuHost, _surfaceVisual);
        UpdateHostClip();
    }

    private void LayoutGpuVisual()
    {
        if (_surfaceVisual is not { } visual)
        {
            return;
        }
        var picture = PictureRect;
        var size = _rotation is 90 or 270 ? new Size(picture.Height, picture.Width) : picture.Size;
        var center = picture.Center;
        visual.Size = new Vector(size.Width, size.Height);
        visual.Offset = new Vector3D(center.X - size.Width / 2, center.Y - size.Height / 2, 0);
        visual.CenterPoint = new Vector3D(size.Width / 2, size.Height / 2, 0);
        visual.RotationAngle = (float)(_rotation * Math.PI / 180);
        UpdateHostClip();
    }

    /// <summary>
    /// Clips the GPU picture to the surface's rounded bounds. A composition visual has no public geometry clip, but the
    /// element it is attached to clips its child visual with its own <see cref="Visual.Clip"/>; the host element exists so
    /// the surface's own <see cref="Visual.Clip"/> stays the app's.
    /// </summary>
    private void UpdateHostClip()
    {
        if (_gpuHost is not { } host)
        {
            return;
        }
        host.Clip = CreateRoundedGeometry(new Rect(Bounds.Size), CornerRadius);
    }

    internal static Geometry CreateRoundedGeometry(Rect rect, CornerRadius radius)
    {
        if (radius.TopLeft == radius.TopRight && radius.TopLeft == radius.BottomRight && radius.TopLeft == radius.BottomLeft)
        {
            return new RectangleGeometry(rect, radius.TopLeft, radius.TopLeft);
        }
        var max = Math.Min(rect.Width, rect.Height) / 2;
        var tl = Math.Min(radius.TopLeft, max);
        var tr = Math.Min(radius.TopRight, max);
        var br = Math.Min(radius.BottomRight, max);
        var bl = Math.Min(radius.BottomLeft, max);
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.BeginFigure(new Point(rect.Left + tl, rect.Top), true);
            g.LineTo(new Point(rect.Right - tr, rect.Top));
            if (tr > 0)
            {
                g.ArcTo(new Point(rect.Right, rect.Top + tr), new Size(tr, tr), 0, false, SweepDirection.Clockwise);
            }
            g.LineTo(new Point(rect.Right, rect.Bottom - br));
            if (br > 0)
            {
                g.ArcTo(new Point(rect.Right - br, rect.Bottom), new Size(br, br), 0, false, SweepDirection.Clockwise);
            }
            g.LineTo(new Point(rect.Left + bl, rect.Bottom));
            if (bl > 0)
            {
                g.ArcTo(new Point(rect.Left, rect.Bottom - bl), new Size(bl, bl), 0, false, SweepDirection.Clockwise);
            }
            g.LineTo(new Point(rect.Left, rect.Top + tl));
            if (tl > 0)
            {
                g.ArcTo(new Point(rect.Left + tl, rect.Top), new Size(tl, tl), 0, false, SweepDirection.Clockwise);
            }
            g.EndFigure(true);
        }
        return geometry;
    }

    private void FallBackToBitmap(string reason, Exception? exception)
    {
        if (_gpuDisabled)
        {
            return;
        }
        _gpuDisabled = true;
        Trace.TraceWarning($"VideoSurface: {reason}; drawing frames through a bitmap from now on. {exception?.GetType().Name}: {exception?.Message}");
        TearDownGpu();
        if (RenderPath == VideoRenderPath.GpuInterop)
        {
            RenderPath = VideoRenderPath.None;
        }
        ApplyFrameTarget();
        RequestOneFrame();
    }

    private void TearDownGpu()
    {
        _gpuGeneration++;
        _gpuUpdatesPending = 0;
        if (_gpuHost is { } host)
        {
            ElementComposition.SetElementChildVisual(host, null);
            VisualChildren.Remove(host);
            _gpuHost = null;
        }
        _surfaceVisual = null;
        _drawingSurface?.Dispose();
        _drawingSurface = null;
        foreach (var entry in _imported)
        {
            DisposeImported(entry.Image);
        }
        _imported.Clear();
        foreach (var (_, semaphore) in _semaphores)
        {
            DisposeImported(semaphore);
        }
        _semaphores.Clear();
    }

    private static async void DisposeImported(ICompositionGpuImportedObject imported)
    {
        try
        {
            await imported.DisposeAsync();
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("VideoSurface: releasing an imported GPU object failed. " + ex.Message);
        }
    }

    private readonly record struct ImportedImage(long Id, PixelSize Size, PlatformGraphicsExternalImageFormat Format, ICompositionImportedGpuImage Image);
}

/// <summary>Receives a CPU frame copied into a <see cref="VideoSurface"/>'s bitmap (tests).</summary>
internal delegate void FrameCopiedHandler(ReadOnlySpan<byte> pixels, PixelSize size, int rowBytes);

/// <summary>Carries a <see cref="VideoSurface"/>'s GPU picture visual and clips it to the surface's rounded bounds.</summary>
internal sealed class VideoSurfaceGpuHost : Control
{
    public VideoSurfaceGpuHost()
    {
        IsHitTestVisible = false;
    }
}
