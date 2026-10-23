using System.Globalization;
using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// A source's small site mark in a tool chip (13px) and in the source rows of a tool's detail (14px). A
/// <see cref="Geometry"/> mark is a 24 × 24 shape filled with <see cref="AiSource.MarkBrush"/> or the secondary text color;
/// an <see cref="IImage"/> keeps its own colors; without a mark it shows the domain's first letter on a muted disc.
/// Decorative: the source's title and domain are read elsewhere.
/// </summary>
/// <remarks>The citation parts of the AI group will bring a shared source mark; this one can give way to it then.</remarks>
internal sealed class AiToolSourceMark : Control
{
    private const double Box = 24;

    /// <summary>The source whose mark is drawn.</summary>
    public static readonly StyledProperty<AiSource?> SourceProperty =
        AvaloniaProperty.Register<AiToolSourceMark, AiSource?>(nameof(Source));

    /// <summary>Width and height in DIPs.</summary>
    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<AiToolSourceMark, double>(nameof(Size), 13);

    // Theme colors, bound to their tokens.
    private static readonly StyledProperty<IBrush?> InkProperty =
        AvaloniaProperty.Register<AiToolSourceMark, IBrush?>("Ink");

    private static readonly StyledProperty<IBrush?> DiscProperty =
        AvaloniaProperty.Register<AiToolSourceMark, IBrush?>("Disc");

    private AiSource? _watched;
    private FormattedText? _letter;
    private string? _letterText;
    private double _letterSize;
    private IBrush? _letterBrush;

    static AiToolSourceMark()
    {
        AffectsMeasure<AiToolSourceMark>(SizeProperty);
        AffectsRender<AiToolSourceMark>(SourceProperty, InkProperty, DiscProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<AiToolSourceMark>(false);
        FocusableProperty.OverrideDefaultValue<AiToolSourceMark>(false);
        VerticalAlignmentProperty.OverrideDefaultValue<AiToolSourceMark>(Avalonia.Layout.VerticalAlignment.Center);
    }

    public AiToolSourceMark()
    {
        ArcTokens.Bind(this, InkProperty, ArcDesignTokenKind.TextSecondary);
        ArcTokens.Bind(this, DiscProperty, ArcDesignTokenKind.SurfaceMuted);
    }

    /// <inheritdoc cref="SourceProperty"/>
    public AiSource? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

    /// <inheritdoc cref="SizeProperty"/>
    public double Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>The letter shown when the source has no mark of its own ("D" for docs.example.org), or null.</summary>
    internal static string? FallbackLetter(AiSource? source)
    {
        var text = !string.IsNullOrWhiteSpace(source?.Domain) ? source!.Domain : source?.Title;
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        var trimmed = text.Trim();
        if (trimmed.StartsWith("www.", StringComparison.OrdinalIgnoreCase) && trimmed.Length > 4)
        {
            trimmed = trimmed[4..];
        }
        return StringInfo.GetNextTextElement(trimmed).ToUpper(CultureInfo.InvariantCulture);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourceProperty)
        {
            Watch(this.IsAttachedToVisualTree() ? change.GetNewValue<AiSource?>() : null);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Watch(Source);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Watch(null);
    }

    private void Watch(AiSource? source)
    {
        if (ReferenceEquals(_watched, source))
        {
            return;
        }
        if (_watched is not null)
        {
            _watched.PropertyChanged -= OnSourceChanged;
        }
        _watched = source;
        if (_watched is not null)
        {
            _watched.PropertyChanged += OnSourceChanged;
        }
    }

    private void OnSourceChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == AiSource.MarkProperty || e.Property == AiSource.MarkBrushProperty || e.Property == AiSource.DomainProperty
            || e.Property == AiSource.TitleProperty)
        {
            InvalidateVisual();
        }
    }

    protected override Size MeasureOverride(Size availableSize) => new(Size, Size);

    public override void Render(DrawingContext context)
    {
        var size = Size;
        var source = Source;
        switch (source?.Mark)
        {
            case Geometry geometry when (source.MarkBrush ?? GetValue(InkProperty)) is { } brush:
            {
                var scale = size / Box;
                using (context.PushTransform(Matrix.CreateScale(scale, scale)))
                {
                    context.DrawGeometry(brush, null, geometry);
                }
                return;
            }
            case IImage image when image.Size is { Width: > 0, Height: > 0 } natural:
            {
                // Contain: keep the aspect ratio, centered in the square.
                var fit = Math.Min(size / natural.Width, size / natural.Height);
                var width = natural.Width * fit;
                var height = natural.Height * fit;
                context.DrawImage(image, new Rect(natural), new Rect((size - width) / 2, (size - height) / 2, width, height));
                return;
            }
        }
        if (FallbackLetter(source) is not { } letter)
        {
            return;
        }
        if (GetValue(DiscProperty) is { } disc)
        {
            context.DrawEllipse(disc, null, new Rect(0, 0, size, size));
        }
        if (GetValue(InkProperty) is not { } ink)
        {
            return;
        }
        if (_letter is null || _letterText != letter || Math.Abs(_letterSize - size) > 0.01 || !ReferenceEquals(_letterBrush, ink))
        {
            // Rebuilt only when the letter, size or color changes; a static mark draws once.
            _letter = new FormattedText(letter, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold), size * 0.62, ink);
            _letterText = letter;
            _letterSize = size;
            _letterBrush = ink;
        }
        context.DrawText(_letter, new Point((size - _letter.Width) / 2, (size - _letter.Height) / 2));
    }
}
