using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// The full-bleed layer of a <see cref="LoginImmersive"/> page: the base colour, the picture (a photo, or the drawn
/// dusk scene) placed like CSS <c>object-fit: cover</c> at an alignment point, and the flat scrim. It also keeps the
/// frosted copy the glass card shows: the same layers rendered at a quarter of the size, blurred (σ 28px) and
/// saturated (×1.6) once, rebuilt when the size, picture or theme changes.
/// </summary>
/// <remarks>
/// Avalonia cannot blur what is behind a control inside a window, so the card cannot use a live backdrop filter. The
/// picture is static, though: a blurred copy made once and drawn behind the card, aligned with the picture and clipped
/// to the card, looks the same and costs nothing per frame (the card moving on its height spring only changes which
/// part of the copy is drawn). While the block is being resized the old copy is stretched; the new one is built
/// 120 ms after the size settles.
/// </remarks>
internal sealed class LoginImmersiveBackdrop : Control
{
    public static readonly StyledProperty<IImage?> PhotoProperty =
        AvaloniaProperty.Register<LoginImmersiveBackdrop, IImage?>(nameof(Photo));

    public static readonly StyledProperty<Point> PhotoAlignmentProperty =
        AvaloniaProperty.Register<LoginImmersiveBackdrop, Point>(nameof(PhotoAlignment), new Point(0.5, 0.58));

    public static readonly StyledProperty<IBrush?> BaseBrushProperty =
        AvaloniaProperty.Register<LoginImmersiveBackdrop, IBrush?>(nameof(BaseBrush));

    public static readonly StyledProperty<IBrush?> ScrimBrushProperty =
        AvaloniaProperty.Register<LoginImmersiveBackdrop, IBrush?>(nameof(ScrimBrush));

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<LoginImmersiveBackdrop, CornerRadius>(nameof(CornerRadius));

    /// <summary>The frosted copy is rendered at this fraction of the block's size.</summary>
    public const double FrostScale = 0.25;

    /// <summary>The blur of the frosted copy, in px at full size (CSS <c>blur(28px)</c>).</summary>
    public const double FrostBlur = 28;

    /// <summary>The saturation boost of the frosted copy (CSS <c>saturate(1.6)</c>).</summary>
    public const double FrostSaturation = 1.6;

    private WriteableBitmap? _frost;
    private DispatcherTimer? _rebuild;
    private bool _frostFailed;

    static LoginImmersiveBackdrop()
    {
        AffectsRender<LoginImmersiveBackdrop>(PhotoProperty, PhotoAlignmentProperty, BaseBrushProperty, ScrimBrushProperty, CornerRadiusProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<LoginImmersiveBackdrop>(false);
    }

    public IImage? Photo { get => GetValue(PhotoProperty); set => SetValue(PhotoProperty, value); }

    public Point PhotoAlignment { get => GetValue(PhotoAlignmentProperty); set => SetValue(PhotoAlignmentProperty, value); }

    public IBrush? BaseBrush { get => GetValue(BaseBrushProperty); set => SetValue(BaseBrushProperty, value); }

    public IBrush? ScrimBrush { get => GetValue(ScrimBrushProperty); set => SetValue(ScrimBrushProperty, value); }

    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    /// <summary>The frosted copy (null before the first build, or when the platform cannot render one).</summary>
    public Bitmap? Frost => _frost;

    /// <summary>How many frosted copies were built (tests read it).</summary>
    internal int FrostBuilds { get; private set; }

    /// <summary>Raised when the frosted copy changed (the glass redraws).</summary>
    public event EventHandler? FrostChanged;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PhotoProperty || change.Property == PhotoAlignmentProperty ||
            change.Property == BaseBrushProperty || change.Property == ScrimBrushProperty)
        {
            ScheduleFrost(TimeSpan.Zero);
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (_frost is null && !_frostFailed)
        {
            BuildFrost();
        }
        else
        {
            ScheduleFrost(TimeSpan.FromMilliseconds(120));
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _rebuild?.Stop();
    }

    private void ScheduleFrost(TimeSpan delay)
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }
        if (_rebuild is null)
        {
            _rebuild = new DispatcherTimer { Interval = delay };
            _rebuild.Tick += (_, _) =>
            {
                _rebuild!.Stop();
                BuildFrost();
            };
        }
        _rebuild.Stop();
        _rebuild.Interval = delay <= TimeSpan.Zero ? TimeSpan.FromMilliseconds(1) : delay;
        _rebuild.Start();
    }

    /// <summary>Draws the base, the picture and the scrim over <paramref name="bounds"/>.</summary>
    private void DrawLayers(DrawingContext context, Rect bounds)
    {
        if (BaseBrush is { } baseBrush)
        {
            context.FillRectangle(baseBrush, bounds);
        }
        if (Photo is { } photo)
        {
            var size = photo.Size;
            using (context.PushClip(bounds))
            using (context.PushTransform(LoginImmersiveScene.CoverTransform(size, bounds, PhotoAlignment)))
            {
                context.DrawImage(photo, new Rect(size));
            }
        }
        else
        {
            LoginImmersiveScene.Draw(context, bounds, PhotoAlignment);
        }
        if (ScrimBrush is { } scrim)
        {
            context.FillRectangle(scrim, bounds);
        }
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }
        var radius = CornerRadius;
        if (radius == default)
        {
            DrawLayers(context, bounds);
            return;
        }
        using (context.PushClip(new RoundedRect(bounds, radius)))
        {
            DrawLayers(context, bounds);
        }
    }

    /// <summary>Renders the layers small, blurs and saturates them, and keeps the result as the frosted copy.</summary>
    internal void BuildFrost()
    {
        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }
        var pixels = new PixelSize(Math.Max(1, (int)Math.Ceiling(size.Width * FrostScale)), Math.Max(1, (int)Math.Ceiling(size.Height * FrostScale)));
        WriteableBitmap? frost = null;
        try
        {
            using var target = new RenderTargetBitmap(pixels, new Vector(96, 96));
            using (var context = target.CreateDrawingContext())
            using (context.PushTransform(Matrix.CreateScale(pixels.Width / size.Width, pixels.Height / size.Height)))
            {
                DrawLayers(context, new Rect(size));
            }
            var stride = pixels.Width * 4;
            var buffer = new byte[stride * pixels.Height];
            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                target.CopyPixels(new PixelRect(pixels), handle.AddrOfPinnedObject(), buffer.Length, stride);
            }
            finally
            {
                handle.Free();
            }
            var format = target.Format ?? PixelFormat.Bgra8888;
            var redFirst = format == PixelFormat.Rgba8888;
            LoginImmersiveBlur.Apply(buffer, pixels.Width, pixels.Height, stride, FrostBlur * FrostScale, FrostSaturation, redFirst);
            frost = new WriteableBitmap(pixels, new Vector(96, 96), redFirst ? PixelFormat.Rgba8888 : PixelFormat.Bgra8888, AlphaFormat.Premul);
            using (var locked = frost.Lock())
            {
                for (var y = 0; y < pixels.Height; y++)
                {
                    Marshal.Copy(buffer, y * stride, locked.Address + y * locked.RowBytes, stride);
                }
            }
            _frostFailed = false;
        }
        catch (Exception)
        {
            // No offscreen rendering or pixel access on this platform: the card falls back to a solid surface, as the
            // web original does where backdrop filters are unsupported.
            frost?.Dispose();
            frost = null;
            _frostFailed = true;
        }
        var old = _frost;
        _frost = frost;
        old?.Dispose();
        FrostBuilds++;
        FrostChanged?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// The glass card's bottom layer: the part of the backdrop's frosted copy that lies under the card, then the card's
/// translucent fill, both clipped to the card's rounded inside. Without a frosted copy (not built yet, unsupported, or
/// reduced transparency) it fills with the solid surface instead.
/// </summary>
internal sealed class LoginImmersiveGlass : Control
{
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<LoginImmersiveGlass, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<IBrush?> SolidFillProperty =
        AvaloniaProperty.Register<LoginImmersiveGlass, IBrush?>(nameof(SolidFill));

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<LoginImmersiveGlass, CornerRadius>(nameof(CornerRadius));

    public static readonly StyledProperty<bool> ReduceTransparencyProperty =
        AvaloniaProperty.Register<LoginImmersiveGlass, bool>(nameof(ReduceTransparency));

    private LoginImmersiveBackdrop? _backdrop;
    private Point _offset;
    private bool _attached;

    static LoginImmersiveGlass()
    {
        AffectsRender<LoginImmersiveGlass>(FillProperty, SolidFillProperty, CornerRadiusProperty, ReduceTransparencyProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<LoginImmersiveGlass>(false);
    }

    /// <summary>The card's translucent fill over the frosted copy.</summary>
    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }

    /// <summary>The opaque fill used without a frosted copy.</summary>
    public IBrush? SolidFill { get => GetValue(SolidFillProperty); set => SetValue(SolidFillProperty, value); }

    /// <summary>The card's inner corner radius.</summary>
    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    /// <summary>The ArcDesign reduce-transparency setting: true fills solid.</summary>
    public bool ReduceTransparency { get => GetValue(ReduceTransparencyProperty); set => SetValue(ReduceTransparencyProperty, value); }

    /// <summary>The backdrop whose frosted copy shows through.</summary>
    public LoginImmersiveBackdrop? Backdrop
    {
        get => _backdrop;
        set
        {
            if (ReferenceEquals(_backdrop, value))
            {
                return;
            }
            if (_backdrop is not null)
            {
                _backdrop.FrostChanged -= OnFrostChanged;
            }
            _backdrop = value;
            if (_backdrop is not null && _attached)
            {
                _backdrop.FrostChanged += OnFrostChanged;
            }
            InvalidateVisual();
        }
    }

    /// <summary>Whether the glass shows the frosted copy (false: the solid fill; tests read it).</summary>
    internal bool IsFrosted => !ReduceTransparency && _backdrop?.Frost is not null;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        if (_backdrop is not null)
        {
            _backdrop.FrostChanged -= OnFrostChanged;
            _backdrop.FrostChanged += OnFrostChanged;
        }
        LayoutUpdated += OnLayoutUpdated;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        if (_backdrop is not null)
        {
            _backdrop.FrostChanged -= OnFrostChanged;
        }
        LayoutUpdated -= OnLayoutUpdated;
    }

    private void OnFrostChanged(object? sender, EventArgs e) => InvalidateVisual();

    /// <summary>The card moves when its height springs or the block resizes: redraw when the part under it changes.</summary>
    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (_backdrop is null || !IsFrosted)
        {
            return;
        }
        var offset = this.TranslatePoint(default, _backdrop) ?? default;
        if (Math.Abs(offset.X - _offset.X) > 0.01 || Math.Abs(offset.Y - _offset.Y) > 0.01)
        {
            _offset = offset;
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }
        using (context.PushClip(new RoundedRect(bounds, CornerRadius)))
        {
            if (!ReduceTransparency && _backdrop is { Frost: { } frost } backdrop && backdrop.Bounds.Width > 0 && backdrop.Bounds.Height > 0)
            {
                _offset = this.TranslatePoint(default, backdrop) ?? default;
                // The copy covers the whole backdrop (stretched while a resize is pending), so map through its bounds.
                var sx = frost.PixelSize.Width / backdrop.Bounds.Width;
                var sy = frost.PixelSize.Height / backdrop.Bounds.Height;
                var source = new Rect(_offset.X * sx, _offset.Y * sy, bounds.Width * sx, bounds.Height * sy)
                    .Intersect(new Rect(0, 0, frost.PixelSize.Width, frost.PixelSize.Height));
                if (source.Width > 0 && source.Height > 0)
                {
                    var target = new Rect(source.X / sx - _offset.X, source.Y / sy - _offset.Y, source.Width / sx, source.Height / sy);
                    using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.HighQuality }))
                    {
                        context.DrawImage(frost, source, target);
                    }
                }
                if (Fill is { } fill)
                {
                    context.FillRectangle(fill, bounds);
                }
                return;
            }
            if (SolidFill is { } solid)
            {
                context.FillRectangle(solid, bounds);
            }
        }
    }
}
