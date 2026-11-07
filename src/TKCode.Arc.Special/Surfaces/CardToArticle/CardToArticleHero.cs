using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>
/// The picture of a story, drawn by itself: the image cropped to cover, a scrim over the bottom 60%, the tag pill at the
/// top-left and the balanced title with the meta line at the bottom-left. Every size is a fraction of the width
/// (<see cref="CardToArticleLayout"/>), so the card's picture and the article's hero are the same drawing at two
/// scales: the morph scales one onto the other without reflowing the title.
/// </summary>
internal sealed class CardToArticleHero : Control
{
    public static readonly StyledProperty<CardToArticleItem?> ItemProperty =
        AvaloniaProperty.Register<CardToArticleHero, CardToArticleItem?>(nameof(Item));

    public static readonly StyledProperty<double> AspectRatioProperty =
        AvaloniaProperty.Register<CardToArticleHero, double>(nameof(AspectRatio), 4.0 / 3.0);

    public static readonly StyledProperty<FontFamily> TitleFontFamilyProperty =
        AvaloniaProperty.Register<CardToArticleHero, FontFamily>(nameof(TitleFontFamily), FontFamily.Default);

    public static readonly StyledProperty<FontFamily> LabelFontFamilyProperty =
        AvaloniaProperty.Register<CardToArticleHero, FontFamily>(nameof(LabelFontFamily), FontFamily.Default);

    public static readonly StyledProperty<IBrush> TagFillProperty =
        AvaloniaProperty.Register<CardToArticleHero, IBrush>(nameof(TagFill), new ImmutableSolidColorBrush(Color.FromArgb(209, 255, 255, 255)));

    public static readonly StyledProperty<IBrush> TagInkProperty =
        AvaloniaProperty.Register<CardToArticleHero, IBrush>(nameof(TagInk), new ImmutableSolidColorBrush(Color.FromRgb(22, 22, 22)));

    public static readonly StyledProperty<IBrush> TitleInkProperty =
        AvaloniaProperty.Register<CardToArticleHero, IBrush>(nameof(TitleInk), new ImmutableSolidColorBrush(Color.FromRgb(252, 252, 252)));

    public static readonly StyledProperty<IBrush> MetaInkProperty =
        AvaloniaProperty.Register<CardToArticleHero, IBrush>(nameof(MetaInk), new ImmutableSolidColorBrush(Color.FromArgb(204, 255, 255, 255)));

    public static readonly StyledProperty<IBrush> ScrimShadeProperty =
        AvaloniaProperty.Register<CardToArticleHero, IBrush>(nameof(ScrimShade), new ImmutableSolidColorBrush(Color.FromArgb(158, 0, 0, 0)));

    public CardToArticleItem? Item { get => GetValue(ItemProperty); set => SetValue(ItemProperty, value); }
    public double AspectRatio { get => GetValue(AspectRatioProperty); set => SetValue(AspectRatioProperty, value); }
    public FontFamily TitleFontFamily { get => GetValue(TitleFontFamilyProperty); set => SetValue(TitleFontFamilyProperty, value); }
    public FontFamily LabelFontFamily { get => GetValue(LabelFontFamilyProperty); set => SetValue(LabelFontFamilyProperty, value); }
    public IBrush TagFill { get => GetValue(TagFillProperty); set => SetValue(TagFillProperty, value); }
    public IBrush TagInk { get => GetValue(TagInkProperty); set => SetValue(TagInkProperty, value); }
    public IBrush TitleInk { get => GetValue(TitleInkProperty); set => SetValue(TitleInkProperty, value); }
    public IBrush MetaInk { get => GetValue(MetaInkProperty); set => SetValue(MetaInkProperty, value); }
    public IBrush ScrimShade { get => GetValue(ScrimShadeProperty); set => SetValue(ScrimShadeProperty, value); }

    private TextLayout? _tag;
    private TextLayout? _title;
    private TextLayout? _meta;
    private double _layoutWidth = -1;
    private IImmutableBrush? _scrim;

    static CardToArticleHero()
    {
        AffectsMeasure<CardToArticleHero>(ItemProperty, AspectRatioProperty, TitleFontFamilyProperty, LabelFontFamilyProperty);
        AffectsRender<CardToArticleHero>(ItemProperty, TagFillProperty, TagInkProperty, TitleInkProperty, MetaInkProperty, ScrimShadeProperty);
    }

    /// <summary>The balanced width the title wraps at (for tests).</summary>
    internal double TitleWrapWidth { get; private set; }

    /// <summary>The title's line count at the current width (for tests).</summary>
    internal int TitleLines => _title?.TextLines.Count ?? 0;

    internal double TitleFontSize => Bounds.Width * CardToArticleLayout.TitleFont;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemProperty || change.Property == TitleFontFamilyProperty || change.Property == LabelFontFamilyProperty ||
            change.Property == TagInkProperty || change.Property == TitleInkProperty || change.Property == MetaInkProperty)
        {
            _layoutWidth = -1;
        }
        else if (change.Property == ScrimShadeProperty)
        {
            _scrim = null;
        }

    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 0;
        return new Size(width, width / CardToArticleLayout.SafeRatio(AspectRatio));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        EnsureText(finalSize.Width);
        return finalSize;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        DisposeText();
        _layoutWidth = -1;
    }

    private void DisposeText()
    {
        _tag?.Dispose();
        _title?.Dispose();
        _meta?.Dispose();
        _tag = _title = _meta = null;
    }

    private void EnsureText(double width)
    {
        if (width.Equals(_layoutWidth))
        {
            return;
        }
        DisposeText();
        _layoutWidth = width;
        TitleWrapWidth = 0;
        if (width <= 0 || Item is not { } item)
        {
            return;
        }
        var label = new Typeface(LabelFontFamily, FontStyle.Normal, FontWeight.Medium);
        var labelRegular = new Typeface(LabelFontFamily, FontStyle.Normal, FontWeight.Normal);
        var display = new Typeface(TitleFontFamily, FontStyle.Normal, FontWeight.Medium);
        if (!string.IsNullOrEmpty(item.Tag))
        {
            var size = width * CardToArticleLayout.TagFont;
            _tag = new TextLayout(item.Tag, label, size, TagInk,
                textWrapping: TextWrapping.NoWrap, lineHeight: size * CardToArticleLayout.TagLineHeight);
        }
        if (!string.IsNullOrEmpty(item.Title))
        {
            var size = width * CardToArticleLayout.TitleFont;
            var max = Math.Max(1, width * (1 - 2 * CardToArticleLayout.CaptionInsetX));
            var brush = TitleInk;
            var title = item.Title;
            TextLayout Make(double w) => new(title, display, size, brush, TextAlignment.Left, TextWrapping.Wrap, null, null,
                FlowDirection.LeftToRight, w, double.PositiveInfinity, size * CardToArticleLayout.TitleLineHeight,
                size * CardToArticleLayout.TitleTracking);
            int Lines(double w)
            {
                using var probe = Make(w);
                return probe.TextLines.Count;
            }
            TitleWrapWidth = CardToArticleLayout.Balance(max, Lines, Math.Max(0.25, width * 0.001));
            _title = Make(TitleWrapWidth);
        }
        if (!string.IsNullOrEmpty(item.Meta))
        {
            var size = CardToArticleLayout.MetaFontSize(width);
            _meta = new TextLayout(item.Meta, labelRegular, size, MetaInk,
                textWrapping: TextWrapping.NoWrap, lineHeight: size * CardToArticleLayout.MetaLineHeight);
        }
    }

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 0 || height <= 0)
        {
            return;
        }
        var bounds = new Rect(0, 0, width, height);
        // The whole picture takes the pointer (the article's hero is the mouse handle of the pull).
        context.FillRectangle(Brushes.Transparent, bounds);
        if (Item?.Image is { } image)
        {
            DrawCover(context, image, bounds);
        }
        var shade = ScrimShade is ISolidColorBrush solid ? Oklch.WithAlpha(solid.Color, solid.Color.A / 255.0 * solid.Opacity) : Color.FromArgb(158, 0, 0, 0);
        _scrim ??= new ImmutableLinearGradientBrush(
            [new ImmutableGradientStop(0, Color.FromArgb(0, shade.R, shade.G, shade.B)), new ImmutableGradientStop(1, shade)],
            startPoint: new RelativePoint(0, 0, RelativeUnit.Relative), endPoint: new RelativePoint(0, 1, RelativeUnit.Relative));
        var scrimTop = height * CardToArticleLayout.ScrimTop;
        context.FillRectangle(_scrim, new Rect(0, scrimTop, width, height - scrimTop));

        if (_tag is { } tag)
        {
            var size = width * CardToArticleLayout.TagFont;
            var padX = size * CardToArticleLayout.TagPaddingX;
            var padY = size * CardToArticleLayout.TagPaddingY;
            var inset = width * CardToArticleLayout.TagInset;
            var pill = new Rect(inset, inset, tag.WidthIncludingTrailingWhitespace + 2 * padX, tag.Height + 2 * padY);
            var radius = pill.Height / 2;
            context.DrawRectangle(TagFill, null, new RoundedRect(pill, radius));
            tag.Draw(context, new Point(pill.X + padX, pill.Y + padY));
        }

        var left = width * CardToArticleLayout.CaptionInsetX;
        var bottom = height - width * CardToArticleLayout.CaptionInsetBottom;
        if (_meta is { } meta)
        {
            bottom -= meta.Height;
            meta.Draw(context, new Point(left, bottom));
            bottom -= width * CardToArticleLayout.CaptionGap;
        }
        if (_title is { } title)
        {
            bottom -= title.Height;
            title.Draw(context, new Point(left, bottom));
        }
    }

    /// <summary>Draws <paramref name="image"/> cropped to fill <paramref name="dest"/> (CSS object-fit: cover).</summary>
    internal static void DrawCover(DrawingContext context, IImage image, Rect dest)
    {
        // A bitmap's source rectangle is in pixels; other images (drawings) use their own size.
        var source = image is Bitmap bitmap ? new Size(bitmap.PixelSize.Width, bitmap.PixelSize.Height) : image.Size;
        if (source.Width <= 0 || source.Height <= 0)
        {
            return;
        }
        context.DrawImage(image, CoverSource(source, dest.Size), dest);
    }

    internal static Rect CoverSource(Size source, Size dest)
    {
        if (dest.Width <= 0 || dest.Height <= 0)
        {
            return new Rect(source);
        }
        var destRatio = dest.Width / dest.Height;
        var sourceRatio = source.Width / source.Height;
        if (sourceRatio > destRatio)
        {
            var w = source.Height * destRatio;
            return new Rect((source.Width - w) / 2, 0, w, source.Height);
        }
        var h = source.Width / destRatio;
        return new Rect(0, (source.Height - h) / 2, source.Width, h);
    }
}
