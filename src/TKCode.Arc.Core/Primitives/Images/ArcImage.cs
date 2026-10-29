using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace TKCode.Arc.Primitives;

/// <summary>
/// A picture that loads in the background: http(s), <c>avares://</c>, files, <c>data:</c> addresses, picked storage files
/// or a bitmap you hold (<see cref="ImageSource"/>). Pictures are decoded off the UI thread, scaled down when they are much
/// larger than the slot they fill, and shared through <see cref="ArcImageCache"/>, so a picture that was shown before
/// appears at once without a loading state. Changing <see cref="Source"/> or leaving the visual tree cancels the load
/// (the download stops once no image waits for it); it resumes when the image is attached again.
/// <para>
/// <see cref="IsLoading"/>, <see cref="IsImageLoaded"/> and <see cref="IsFailed"/> report the state, so a template can fade
/// the picture in or fall back to initials. Loading goes through <see cref="Loader"/> (inherited, so it can be set on a
/// window or page), or <see cref="DefaultLoader"/> when none is set. Lays out and stretches like Avalonia's <c>Image</c>.
/// </para>
/// </summary>
public class ArcImage : Control
{
    /// <summary>The picture to show; null shows nothing.</summary>
    public static readonly StyledProperty<ImageSource?> SourceProperty =
        AvaloniaProperty.Register<ArcImage, ImageSource?>(nameof(Source));

    /// <summary>How the picture fills the image's bounds (default Uniform, as Avalonia's <c>Image</c>).</summary>
    public static readonly StyledProperty<Stretch> StretchProperty =
        Image.StretchProperty.AddOwner<ArcImage>();

    /// <summary>Whether the picture may be scaled up, down or both (default Both).</summary>
    public static readonly StyledProperty<StretchDirection> StretchDirectionProperty =
        Image.StretchDirectionProperty.AddOwner<ArcImage>();

    /// <summary>
    /// The loader that reads pictures for this image and the images inside it (inherited); null uses
    /// <see cref="DefaultLoader"/>.
    /// </summary>
    public static readonly AttachedProperty<IArcImageLoader?> LoaderProperty =
        AvaloniaProperty.RegisterAttached<ArcImage, Visual, IArcImageLoader?>("Loader", inherits: true);

    /// <summary>True while the picture of <see cref="Source"/> is being read or decoded.</summary>
    public static readonly DirectProperty<ArcImage, bool> IsLoadingProperty =
        AvaloniaProperty.RegisterDirect<ArcImage, bool>(nameof(IsLoading), o => o.IsLoading);

    /// <summary>True while the picture of <see cref="Source"/> is shown.</summary>
    public static readonly DirectProperty<ArcImage, bool> IsImageLoadedProperty =
        AvaloniaProperty.RegisterDirect<ArcImage, bool>(nameof(IsImageLoaded), o => o.IsImageLoaded);

    /// <summary>True when the picture of <see cref="Source"/> could not be read or decoded (see <see cref="LoadError"/>).</summary>
    public static readonly DirectProperty<ArcImage, bool> IsFailedProperty =
        AvaloniaProperty.RegisterDirect<ArcImage, bool>(nameof(IsFailed), o => o.IsFailed);

    /// <summary>The decoded picture being shown, or null.</summary>
    public static readonly DirectProperty<ArcImage, IImage?> CurrentImageProperty =
        AvaloniaProperty.RegisterDirect<ArcImage, IImage?>(nameof(CurrentImage), o => o.CurrentImage);

    private static IArcImageLoader s_defaultLoader = ArcImageLoader.Shared;

    private ArcImageFrame? _frame;
    private CancellationTokenSource? _load;
    private bool _upgradeTried;
    private bool _scheduled;
    private int _version;
    private bool _isLoading;
    private bool _isImageLoaded;
    private bool _isFailed;

    static ArcImage()
    {
        AffectsRender<ArcImage>(StretchProperty, StretchDirectionProperty);
        AffectsMeasure<ArcImage>(StretchProperty, StretchDirectionProperty);
    }

    /// <summary>The loader images use when no <see cref="Loader"/> is set (default <see cref="ArcImageLoader.Shared"/>).</summary>
    public static IArcImageLoader DefaultLoader
    {
        get => s_defaultLoader;
        set => s_defaultLoader = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <inheritdoc cref="SourceProperty"/>
    public ImageSource? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

    /// <inheritdoc cref="StretchProperty"/>
    public Stretch Stretch { get => GetValue(StretchProperty); set => SetValue(StretchProperty, value); }

    /// <inheritdoc cref="StretchDirectionProperty"/>
    public StretchDirection StretchDirection { get => GetValue(StretchDirectionProperty); set => SetValue(StretchDirectionProperty, value); }

    /// <inheritdoc cref="LoaderProperty"/>
    public IArcImageLoader? Loader { get => GetValue(LoaderProperty); set => SetValue(LoaderProperty, value); }

    /// <inheritdoc cref="IsLoadingProperty"/>
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetAndRaise(IsLoadingProperty, ref _isLoading, value);
    }

    /// <inheritdoc cref="IsImageLoadedProperty"/>
    public bool IsImageLoaded
    {
        get => _isImageLoaded;
        private set => SetAndRaise(IsImageLoadedProperty, ref _isImageLoaded, value);
    }

    /// <inheritdoc cref="IsFailedProperty"/>
    public bool IsFailed
    {
        get => _isFailed;
        private set => SetAndRaise(IsFailedProperty, ref _isFailed, value);
    }

    /// <inheritdoc cref="CurrentImageProperty"/>
    public IImage? CurrentImage => _frame?.Bitmap;

    /// <summary>Why the last load failed, while <see cref="IsFailed"/> is true.</summary>
    public Exception? LoadError { get; private set; }

    /// <summary>Gets the loader set on <paramref name="visual"/> (see <see cref="LoaderProperty"/>).</summary>
    public static IArcImageLoader? GetLoader(Visual visual) => visual.GetValue(LoaderProperty);

    /// <summary>Sets the loader for <paramref name="visual"/> and the images inside it (see <see cref="LoaderProperty"/>).</summary>
    public static void SetLoader(Visual visual, IArcImageLoader? loader) => visual.SetValue(LoaderProperty, loader);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourceProperty)
        {
            ApplySource(change.GetOldValue<ImageSource?>(), change.GetNewValue<ImageSource?>());
        }
    }

    private void ApplySource(ImageSource? old, ImageSource? source)
    {
        // The same picture again (a list rebinding a fresh source object) keeps what is shown or loading; a failed one retries.
        if (old is not null && source is not null && !IsFailed && old.IsSamePicture(source))
        {
            return;
        }
        CancelLoad();
        _version++;
        _upgradeTried = false;
        LoadError = null;
        IsFailed = false;
        if (source is null)
        {
            SetFrame(null);
            IsImageLoaded = false;
            IsLoading = false;
            return;
        }
        if (source is BitmapImageSource { Bitmap: var bitmap })
        {
            SetFrame(new ArcImageFrame(bitmap, bitmap.Size, bitmap.PixelSize, isReduced: false));
            IsImageLoaded = true;
            IsLoading = false;
            return;
        }
        if (ArcImageCache.TryGet(source, out var cached))
        {
            // Shown at once; a copy that was decoded for a smaller slot is replaced quietly after layout.
            SetFrame(cached);
            IsImageLoaded = true;
            IsLoading = false;
            ScheduleLoad();
            return;
        }
        SetFrame(null);
        IsImageLoaded = false;
        IsLoading = true;
        ScheduleLoad();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ScheduleLoad();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // IsLoading stays true, so the load starts again when the image is attached again.
        CancelLoad();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (_frame is { IsReduced: true })
        {
            _upgradeTried = false;
            ScheduleLoad();
        }
    }

    /// <summary>Starts the load after layout, when the slot the picture fills is known (used to decode at that size).</summary>
    private void ScheduleLoad()
    {
        if (_scheduled || VisualRoot is null || Source is null)
        {
            return;
        }
        _scheduled = true;
        Dispatcher.UIThread.Post(RunScheduledLoad, DispatcherPriority.Loaded);
    }

    private void RunScheduledLoad()
    {
        _scheduled = false;
        if (VisualRoot is null || Source is not { } source || _load is not null)
        {
            return;
        }
        var slot = SlotPixels();
        if (IsLoading)
        {
            _ = LoadAsync(source, slot, upgrade: false);
        }
        else if (IsImageLoaded && !_upgradeTried && _frame is { IsReduced: true } frame && slot is { } size && !frame.Covers(size, Stretch))
        {
            _upgradeTried = true;
            _ = LoadAsync(source, slot, upgrade: true);
        }
    }

    private async Task LoadAsync(ImageSource source, Size? slot, bool upgrade)
    {
        var cancellation = new CancellationTokenSource();
        _load = cancellation;
        var version = _version;
        try
        {
            var frame = await ArcImageCache.LoadAsync(source, Loader ?? DefaultLoader, slot, Stretch, cancellation.Token);
            if (cancellation.IsCancellationRequested || version != _version)
            {
                return;
            }
            _load = null;
            SetFrame(frame);
            IsImageLoaded = true;
            IsLoading = false;
            if (!upgrade)
            {
                // A shared download may have been decoded for a smaller slot.
                ScheduleLoad();
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception error)
        {
            if (version != _version || cancellation.IsCancellationRequested)
            {
                return;
            }
            _load = null;
            if (upgrade)
            {
                // The smaller copy stays.
                return;
            }
            LoadError = error;
            IsFailed = true;
            IsLoading = false;
        }
        finally
        {
            // Not disposed: Cancel can run this continuation inline, and a source without a timer holds nothing to free.
            if (ReferenceEquals(_load, cancellation))
            {
                _load = null;
            }
        }
    }

    private void CancelLoad()
    {
        if (_load is { } load)
        {
            _load = null;
            load.Cancel();
        }
    }

    /// <summary>The slot the picture fills, in device pixels, or null when it is not known yet.</summary>
    private Size? SlotPixels()
    {
        var width = Bounds.Width > 0 ? Bounds.Width : double.IsFinite(Width) ? Width : double.IsFinite(MaxWidth) ? MaxWidth : double.NaN;
        var height = Bounds.Height > 0 ? Bounds.Height : double.IsFinite(Height) ? Height : double.IsFinite(MaxHeight) ? MaxHeight : double.NaN;
        if (double.IsNaN(width) && double.IsNaN(height))
        {
            return null;
        }
        var scale = LayoutHelper.GetLayoutScale(this);
        return new Size(width * scale, height * scale);
    }

    private void SetFrame(ArcImageFrame? frame)
    {
        if (ReferenceEquals(_frame, frame))
        {
            return;
        }
        var old = _frame?.Bitmap;
        _frame = frame;
        RaisePropertyChanged(CurrentImageProperty, old, frame?.Bitmap);
        InvalidateMeasure();
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize) =>
        _frame is { } frame ? Stretch.CalculateSize(availableSize, frame.NaturalSize, StretchDirection) : default;

    protected override Size ArrangeOverride(Size finalSize) =>
        // Without a picture the image keeps its slot, so the slot is known when the picture is decoded.
        _frame is { } frame ? Stretch.CalculateSize(finalSize, frame.NaturalSize) : finalSize;

    public override void Render(DrawingContext context)
    {
        if (_frame is not { } frame || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }
        var natural = frame.NaturalSize;
        if (natural.Width <= 0 || natural.Height <= 0)
        {
            return;
        }
        var viewPort = new Rect(Bounds.Size);
        var scale = Stretch.CalculateScaling(Bounds.Size, natural, StretchDirection);
        var destRect = viewPort.CenterRect(new Rect(natural * scale)).Intersect(viewPort);
        var sourceRect = new Rect(natural).CenterRect(new Rect(destRect.Size / scale));
        // The source rectangle is in the bitmap's pixels (which a reduced copy has fewer of than the picture's own size).
        var pixels = frame.Bitmap.PixelSize;
        var kx = pixels.Width / natural.Width;
        var ky = pixels.Height / natural.Height;
        context.DrawImage(frame.Bitmap, new Rect(sourceRect.X * kx, sourceRect.Y * ky, sourceRect.Width * kx, sourceRect.Height * ky), destRect);
    }
}
