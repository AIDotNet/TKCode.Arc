using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// The stage of the wallet: as wide as the wallet plus 12px on each side and the card height plus 220px tall, clipped
/// vertically only (shadows may spill sideways). It hosts the activity panel and one slot per card (the first card on
/// top), owns the baked faces, the shadow and light bitmaps and the theme inks, and applies the poses
/// <see cref="WalletStack"/> computes.
/// </summary>
internal sealed class WalletStackStage : Control
{
    /// <summary>How far shadows may spill sideways past the stage.</summary>
    public const double ShadowMargin = 96;

    public static readonly StyledProperty<IBrush?> ForegroundInkProperty =
        AvaloniaProperty.Register<WalletStackStage, IBrush?>(nameof(ForegroundInk));

    public static readonly StyledProperty<IBrush?> SecondaryInkProperty =
        AvaloniaProperty.Register<WalletStackStage, IBrush?>(nameof(SecondaryInk));

    public static readonly StyledProperty<IBrush?> MutedInkProperty =
        AvaloniaProperty.Register<WalletStackStage, IBrush?>(nameof(MutedInk));

    public static readonly StyledProperty<IBrush?> BorderInkProperty =
        AvaloniaProperty.Register<WalletStackStage, IBrush?>(nameof(BorderInk));

    public static readonly StyledProperty<IBrush?> SurfaceMutedInkProperty =
        AvaloniaProperty.Register<WalletStackStage, IBrush?>(nameof(SurfaceMutedInk));

    public static readonly StyledProperty<IBrush?> SuccessInkProperty =
        AvaloniaProperty.Register<WalletStackStage, IBrush?>(nameof(SuccessInk));

    public static readonly StyledProperty<IBrush?> FocusInkProperty =
        AvaloniaProperty.Register<WalletStackStage, IBrush?>(nameof(FocusInk));

    public static readonly StyledProperty<IBrush?> BackgroundInkProperty =
        AvaloniaProperty.Register<WalletStackStage, IBrush?>(nameof(BackgroundInk));


    public static readonly StyledProperty<IBrush?> ShadowInkProperty =
        AvaloniaProperty.Register<WalletStackStage, IBrush?>(nameof(ShadowInk));

    public static readonly StyledProperty<IBrush?> ContactInkProperty =
        AvaloniaProperty.Register<WalletStackStage, IBrush?>(nameof(ContactInk));

    public static readonly StyledProperty<FontFamily> DisplayFontProperty =
        AvaloniaProperty.Register<WalletStackStage, FontFamily>(nameof(DisplayFont), FontFamily.Default);

    public static readonly StyledProperty<FontFamily> BodyFontProperty =
        AvaloniaProperty.Register<WalletStackStage, FontFamily>(nameof(BodyFont), FontFamily.Default);

    public static readonly StyledProperty<double> SmallSizeProperty =
        AvaloniaProperty.Register<WalletStackStage, double>(nameof(SmallSize), 14);

    public static readonly StyledProperty<double> ExtraSmallSizeProperty =
        AvaloniaProperty.Register<WalletStackStage, double>(nameof(ExtraSmallSize), 12);

    public static readonly StyledProperty<double> BalanceSizeProperty =
        AvaloniaProperty.Register<WalletStackStage, double>(nameof(BalanceSize), 22);

    private readonly List<WalletStackCardSlot> _slots = new();
    private readonly List<WalletStackCardView> _views = new();
    private readonly Dictionary<(double, double), Bitmap> _lights = new();
    private readonly TranslateTransform _detailShift = new();
    private WalletStackGeometry _geometry = new(296);
    private double _bakedWidth = -1;
    private double _bakedScaling = -1;
    private bool _bakedDark;
    private bool _bakePending;
    private double _lightWidth = -1;

    public WalletStackStage()
    {
        ClipToBounds = false;
        Detail = new WalletStackDetail { RenderTransform = _detailShift, Opacity = 0, IsVisible = false };
        VisualChildren.Add(Detail);
        LogicalChildren.Add(Detail);
    }

    public IBrush? ForegroundInk { get => GetValue(ForegroundInkProperty); set => SetValue(ForegroundInkProperty, value); }
    public IBrush? SecondaryInk { get => GetValue(SecondaryInkProperty); set => SetValue(SecondaryInkProperty, value); }
    public IBrush? MutedInk { get => GetValue(MutedInkProperty); set => SetValue(MutedInkProperty, value); }
    public IBrush? BorderInk { get => GetValue(BorderInkProperty); set => SetValue(BorderInkProperty, value); }
    public IBrush? SurfaceMutedInk { get => GetValue(SurfaceMutedInkProperty); set => SetValue(SurfaceMutedInkProperty, value); }
    public IBrush? SuccessInk { get => GetValue(SuccessInkProperty); set => SetValue(SuccessInkProperty, value); }
    public IBrush? FocusInk { get => GetValue(FocusInkProperty); set => SetValue(FocusInkProperty, value); }
    public IBrush? BackgroundInk { get => GetValue(BackgroundInkProperty); set => SetValue(BackgroundInkProperty, value); }
    public IBrush? ShadowInk { get => GetValue(ShadowInkProperty); set => SetValue(ShadowInkProperty, value); }
    public IBrush? ContactInk { get => GetValue(ContactInkProperty); set => SetValue(ContactInkProperty, value); }

    /// <summary>The soft drop shadow's color (the token's brush color).</summary>
    public Color ShadowColor => (ShadowInk as ISolidColorBrush)?.Color ?? Color.FromArgb(66, 40, 44, 56);

    /// <summary>The contact shadow's color.</summary>
    public Color ContactColor => (ContactInk as ISolidColorBrush)?.Color ?? Color.FromArgb(41, 0, 0, 0);
    public FontFamily DisplayFont { get => GetValue(DisplayFontProperty); set => SetValue(DisplayFontProperty, value); }
    public FontFamily BodyFont { get => GetValue(BodyFontProperty); set => SetValue(BodyFontProperty, value); }
    public double SmallSize { get => GetValue(SmallSizeProperty); set => SetValue(SmallSizeProperty, value); }
    public double ExtraSmallSize { get => GetValue(ExtraSmallSizeProperty); set => SetValue(ExtraSmallSizeProperty, value); }
    public double BalanceSize { get => GetValue(BalanceSizeProperty); set => SetValue(BalanceSizeProperty, value); }

    /// <summary>The stack that drives this stage.</summary>
    public WalletStack? Owner { get; set; }

    public WalletStackDetail Detail { get; }

    public IReadOnlyList<WalletStackCardView> Views => _views;

    public IReadOnlyList<WalletStackCardSlot> Slots => _slots;

    public WalletStackGeometry Geometry => _geometry;

    /// <summary>The soft drop shadow: <c>0 18px 34px −8px</c> and <c>0 30px 60px −20px</c> in the shadow color.</summary>
    public BoxShadows SoftShadow { get; private set; }

    public BoxShadows ContactShadow { get; private set; }

    public IPen? FocusPen { get; private set; }

    /// <summary>Whether the theme is dark: the background is darker than the foreground.</summary>
    public bool IsDark => Luminance(BackgroundInk) < Luminance(ForegroundInk);

    /// <summary>Raised after the card width changed (the owner re-applies poses).</summary>
    public event Action? GeometryChanged;

    public WalletStackInks Inks => new(
        ForegroundInk ?? Brushes.Black, SecondaryInk ?? Brushes.Gray, MutedInk ?? Brushes.Gray, BorderInk ?? Brushes.LightGray,
        SurfaceMutedInk ?? Brushes.WhiteSmoke, SuccessInk ?? Brushes.Green, FocusInk ?? Brushes.Black, DisplayFont, BodyFont,
        SmallSize, ExtraSmallSize, BalanceSize);

    private static double Luminance(IBrush? brush)
    {
        if (brush is not ISolidColorBrush solid)
        {
            return 0.5;
        }
        var c = solid.Color;
        return (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255;
    }

    /// <summary>Replaces the cards: one slot and view per card, the first card on top.</summary>
    public void SetCards(IReadOnlyList<WalletStackCard> cards)
    {
        foreach (var slot in _slots)
        {
            VisualChildren.Remove(slot);
            LogicalChildren.Remove(slot);
            slot.View.Art?.Dispose();
            slot.View.Tilt.Dispose();
        }
        _slots.Clear();
        _views.Clear();
        for (var i = 0; i < cards.Count; i++)
        {
            var view = new WalletStackCardView(this, cards[i], i);
            _views.Add(view);
            _slots.Add(new WalletStackCardSlot(this, view));
        }
        // Back to front, so the first card is drawn (and hit) last.
        for (var i = _slots.Count - 1; i >= 0; i--)
        {
            VisualChildren.Add(_slots[i]);
            LogicalChildren.Add(_slots[i]);
        }
        _bakedWidth = -1;
        InvalidateMeasure();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == ShadowInkProperty || property == ContactInkProperty || property == FocusInkProperty)
        {
            UpdatePens();
            foreach (var slot in _slots)
            {
                slot.InvalidateVisual();
            }
        }
        else if (property == BackgroundInkProperty || property == ForegroundInkProperty || property == DisplayFontProperty ||
                 property == BodyFontProperty)
        {
            ScheduleBake();
        }
        if (property == ForegroundInkProperty || property == SecondaryInkProperty || property == MutedInkProperty ||
            property == BorderInkProperty || property == SuccessInkProperty)
        {
            Detail.Restyle(Inks);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdatePens();
        ScheduleBake();
    }

    private void UpdatePens()
    {
        var contact = ContactColor;
        ContactShadow = new BoxShadows(new BoxShadow { OffsetY = 1, Blur = 1, Color = contact },
            new[] { new BoxShadow { OffsetY = 3, Blur = 8, Spread = -3, Color = contact } });
        var soft = ShadowColor;
        SoftShadow = new BoxShadows(new BoxShadow { OffsetY = 18, Blur = 34, Spread = -8, Color = soft },
            new[] { new BoxShadow { OffsetY = 30, Blur = 60, Spread = -20, Color = soft } });
        FocusPen = FocusInk is { } focus ? new ImmutablePen(focus.ToImmutable(), 2) : null;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 296 + 2 * WalletStackGeometry.Inset;
        var geometry = new WalletStackGeometry(Math.Max(1, width - 2 * WalletStackGeometry.Inset));
        var card = new Size(geometry.CardWidth, geometry.CardHeight);
        foreach (var slot in _slots)
        {
            slot.Measure(card);
        }
        var detailHeight = Math.Max(0, geometry.StageHeight - WalletStackLayout.DetailTop(geometry) - 36);
        Detail.Measure(new Size(geometry.CardWidth, detailHeight));
        return new Size(width, geometry.StageHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var geometry = new WalletStackGeometry(Math.Max(1, finalSize.Width - 2 * WalletStackGeometry.Inset));
        var changed = Math.Abs(geometry.CardWidth - _geometry.CardWidth) > 0.01;
        _geometry = geometry;
        var inset = WalletStackGeometry.Inset;
        foreach (var slot in _slots)
        {
            slot.Arrange(new Rect(inset, inset, geometry.CardWidth, geometry.CardHeight));
        }
        var top = WalletStackLayout.DetailTop(geometry);
        Detail.Arrange(new Rect(inset, top, geometry.CardWidth, Math.Max(0, finalSize.Height - top - 36)));
        // Clip vertically only: shadows spill sideways, the pocket is cut at the bottom edge.
        Clip = new RectangleGeometry(new Rect(-ShadowMargin * 2, 0, finalSize.Width + ShadowMargin * 4, finalSize.Height));
        if (changed || _bakedWidth < 0)
        {
            ScheduleBake();
            GeometryChanged?.Invoke();
        }
        return finalSize;
    }

    /// <summary>Moves the activity panel with the open amount.</summary>
    public void ApplyDetail(double open, bool visible)
    {
        var (offset, opacity) = WalletStackLayout.Detail(open);
        _detailShift.Y = offset;
        Detail.Opacity = opacity;
        Detail.IsVisible = visible;
        Detail.IsHitTestVisible = opacity > 0.99;
    }

    /// <summary>Re-renders glass faces (their backdrop moved).</summary>
    public void InvalidateGlass()
    {
        foreach (var view in _views)
        {
            if (view.Card.Material == WalletStackMaterial.Glass)
            {
                view.InvalidateVisual();
            }
        }
    }

    private void ScheduleBake()
    {
        if (_bakePending || VisualRoot is null)
        {
            return;
        }
        _bakePending = true;
        Avalonia.Threading.Dispatcher.UIThread.Post(Bake, Avalonia.Threading.DispatcherPriority.Render);
    }

    /// <summary>Renders the card faces, the shadow and resets the light cache when the width, scaling or theme changed.</summary>
    public void Bake()
    {
        _bakePending = false;
        if (VisualRoot is null)
        {
            return;
        }
        var scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        var width = _geometry.CardWidth;
        var dark = IsDark;
        var labels = Owner?.Labels ?? new WalletStackLabels();
        var fonts = new WalletStackFonts(DisplayFont, BodyFont);
        var faces = Math.Abs(width - _bakedWidth) > 0.01 || scaling != _bakedScaling || dark != _bakedDark || _views.Exists(v => v.Art is null);
        if (faces)
        {
            foreach (var view in _views)
            {
                view.Art?.Dispose();
                view.Art = WalletStackCardArt.Render(view.Card, width, scaling, dark, fonts, labels);
                view.InvalidateVisual();
            }
            _bakedWidth = width;
            _bakedScaling = scaling;
            _bakedDark = dark;
        }
    }

    /// <summary>Forces the faces to render again (labels changed).</summary>
    public void Rebake()
    {
        _bakedWidth = -1;
        ScheduleBake();
    }

    /// <summary>
    /// The soft shadow body of a card of <paramref name="size"/>: inset 5.04cqw from the top and 5cqw from the sides,
    /// 12% elliptical corners. The card covers it; only its two blurred layers show.
    /// </summary>
    public static RoundedRect ShadowBody(Size size)
    {
        var q = size.Width / 100;
        var body = new Rect(5 * q, 5.04 * q, size.Width - 10 * q, size.Height - 5.04 * q);
        return new RoundedRect(body, body.Width * 0.12, body.Height * 0.12);
    }

    /// <summary>The pointer light of a material at the current card size: a 2W×2H radial bitmap, white at its centre.</summary>
    public Bitmap? GetLight(WalletStackMaterialStyle style, Size size)
    {
        if (Math.Abs(size.Width - _lightWidth) > 0.01)
        {
            foreach (var bitmap in _lights.Values)
            {
                bitmap.Dispose();
            }
            _lights.Clear();
            _lightWidth = size.Width;
        }
        if (_lights.TryGetValue(style.LightStops, out var light))
        {
            return light;
        }
        try
        {
            var pixel = new PixelSize(Math.Max(1, (int)(size.Width)), Math.Max(1, (int)(size.Height)));
            var bitmap = new RenderTargetBitmap(pixel, new Vector(96, 96));
            using (var context = bitmap.CreateDrawingContext())
            {
                // CSS: circle farthest-corner in the 2W×2H layer, stops 0 / 18% / 36%. The bitmap is half size.
                var radius = Math.Sqrt(size.Width * size.Width + size.Height * size.Height) / 2;
                var white = Colors.White;
                var brush = new RadialGradientBrush
                {
                    Center = RelativePoint.Center, GradientOrigin = RelativePoint.Center,
                    RadiusX = new RelativeScalar(radius, RelativeUnit.Absolute), RadiusY = new RelativeScalar(radius, RelativeUnit.Absolute),
                    GradientStops =
                    {
                        new GradientStop(Color.FromArgb((byte)(style.LightStops.Center * 255), white.R, white.G, white.B), 0),
                        new GradientStop(Color.FromArgb((byte)(style.LightStops.Mid * 255), white.R, white.G, white.B), 0.18),
                        new GradientStop(Color.FromArgb(0, white.R, white.G, white.B), 0.36),
                    },
                };
                context.DrawRectangle(brush, null, new Rect(0, 0, pixel.Width, pixel.Height));
            }
            _lights[style.LightStops] = bitmap;
            return bitmap;
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidOperationException or NotImplementedException)
        {
            return null;
        }
    }

    /// <summary>A flat brush for a card whose face is not rendered (no offscreen rendering).</summary>
    public IBrush FallbackBrush(WalletStackCard card) => new ImmutableSolidColorBrush(WalletStackMaterialStyle.For(card, IsDark).Fallback);

    /// <summary>
    /// Frosted glass: draws the page background and then the cards behind <paramref name="glass"/> (blurred 24px,
    /// saturated 1.4×) where they are, in the glass card's own coordinates, so the glass shows their color through.
    /// Content of the page behind the stage other than its background is not part of the backdrop.
    /// </summary>
    public void DrawBackdrop(DrawingContext context, WalletStackCardView glass)
    {
        var size = glass.Bounds.Size;
        // The backdrop starts from the page (the theme background): the blurred cards fade into it, and it hides the
        // sharp cards drawn underneath, as a real backdrop blur would.
        if (BackgroundInk is { } page)
        {
            context.DrawRectangle(page, null, new Rect(size));
        }
        for (var j = _views.Count - 1; j > glass.Index; j--)
        {
            var behind = _views[j];
            var slot = _slots[j];
            if (slot.Opacity <= 0.01 || behind.Art is null)
            {
                continue;
            }
            if (behind.Art.GetFrosted(size) is not { } frosted || behind.TransformToVisual(glass) is not { } matrix)
            {
                continue;
            }
            var pad = WalletStackCardArt.FrostPad;
            var pixels = frosted.PixelSize;
            using (context.PushTransform(matrix))
            using (context.PushOpacity(slot.Opacity))
            using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.HighQuality }))
            {
                context.DrawImage(frosted, new Rect(0, 0, pixels.Width, pixels.Height),
                    new Rect(-pad, -pad, size.Width + 2 * pad, size.Height + 2 * pad));
            }
        }
    }
}
