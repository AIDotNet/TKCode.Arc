using System.Globalization;
using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// A source's site mark in the AI answer: 12px in the overlapping stack of the sources row, 14px on a source card and 16px
/// where a mark stands alone. A <see cref="Geometry"/> mark is a 24 × 24 shape scaled to <see cref="Size"/> and filled with
/// <see cref="AiSource.MarkBrush"/> (the site's own color) or the secondary text color; an <see cref="IImage"/> keeps its
/// colors and is fitted whole into the square. A source without a mark shows the first letter of its domain (of its title
/// when there is no domain, "www." skipped) on a round chip: Border fill, the letter in the secondary text color, semibold
/// at 60% of the size and centered on its ink, so a capital sits in the middle rather than on the text line. Decorative: the
/// card or row around it says which source it is, so automation does not read it.
/// </summary>
internal sealed class AiSourceMark : Control
{
    /// <summary>The geometry marks are drawn in a 24 × 24 box (Lucide's grid), like the model marks.</summary>
    internal const double Box = 24;

    /// <summary>The fallback letter is this share of the mark's size.</summary>
    internal const double LetterScale = 0.6;

    /// <summary>The source whose mark is drawn.</summary>
    public static readonly StyledProperty<AiSource?> SourceProperty =
        AvaloniaProperty.Register<AiSourceMark, AiSource?>(nameof(Source));

    /// <summary>Width and height in DIPs (12, 14 or 16 in the answer).</summary>
    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<AiSourceMark, double>(nameof(Size), 14);

    // Theme colors, bound to their tokens.
    private static readonly StyledProperty<IBrush?> InkProperty =
        AvaloniaProperty.Register<AiSourceMark, IBrush?>("Ink");

    private static readonly StyledProperty<IBrush?> ChipProperty =
        AvaloniaProperty.Register<AiSourceMark, IBrush?>("Chip");

    private AiSource? _watched;
    private FormattedText? _letter;
    private Point _letterOrigin;
    private string? _letterText;
    private double _letterSize;
    private IBrush? _letterBrush;
    private FontFamily? _letterFamily;

    static AiSourceMark()
    {
        AffectsMeasure<AiSourceMark>(SizeProperty);
        AffectsRender<AiSourceMark>(SourceProperty, InkProperty, ChipProperty, TextElement.FontFamilyProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<AiSourceMark>(false);
        FocusableProperty.OverrideDefaultValue<AiSourceMark>(false);
        HorizontalAlignmentProperty.OverrideDefaultValue<AiSourceMark>(Avalonia.Layout.HorizontalAlignment.Left);
        VerticalAlignmentProperty.OverrideDefaultValue<AiSourceMark>(Avalonia.Layout.VerticalAlignment.Center);
    }

    public AiSourceMark()
    {
        ArcTokens.Bind(this, InkProperty, ArcDesignTokenKind.TextSecondary);
        ArcTokens.Bind(this, ChipProperty, ArcDesignTokenKind.Border);
    }

    /// <inheritdoc cref="SourceProperty"/>
    public AiSource? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

    /// <inheritdoc cref="SizeProperty"/>
    public double Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>What is drawn now: the source's own mark, the fallback letter, or nothing.</summary>
    internal AiSourceMarkKind Kind => Source?.Mark switch
    {
        Geometry => AiSourceMarkKind.Geometry,
        IImage image when image.Size is { Width: > 0, Height: > 0 } => AiSourceMarkKind.Image,
        _ => FallbackLetter(Source) is null ? AiSourceMarkKind.None : AiSourceMarkKind.Letter
    };

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

    /// <summary>A source's mark or domain can change while it is shown (sources streaming in); only the shown one is watched.</summary>
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

    protected override AutomationPeer OnCreateAutomationPeer() => new NoneAutomationPeer(this);

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
                // Contain: the whole image, its aspect kept, centered in the square. The destination overload draws the
                // whole image whatever its DPI (a source rectangle would be read in pixels).
                var fit = Math.Min(size / natural.Width, size / natural.Height);
                var width = natural.Width * fit;
                var height = natural.Height * fit;
                context.DrawImage(image, new Rect((size - width) / 2, (size - height) / 2, width, height));
                return;
            }
        }
        if (FallbackLetter(source) is not { } letter)
        {
            return;
        }
        if (GetValue(ChipProperty) is { } chip)
        {
            context.DrawEllipse(chip, null, new Rect(0, 0, size, size));
        }
        if (GetValue(InkProperty) is not { } ink)
        {
            return;
        }
        var family = GetValue(TextElement.FontFamilyProperty);
        if (_letter is null || _letterText != letter || Math.Abs(_letterSize - size) > 0.01 || !ReferenceEquals(_letterBrush, ink)
            || !Equals(_letterFamily, family))
        {
            // Rebuilt only when the letter, size, color or font changes; a static mark lays its letter out once.
            _letter = new FormattedText(letter, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(family, FontStyle.Normal, FontWeight.SemiBold), size * LetterScale, ink);
            _letterText = letter;
            _letterSize = size;
            _letterBrush = ink;
            _letterFamily = family;
            // Centered on the ink, not on the line box: a capital would otherwise sit above the middle.
            var ink2 = _letter.BuildGeometry(default)?.Bounds ?? new Rect(0, 0, _letter.Width, _letter.Height);
            _letterOrigin = new Point((size - ink2.Width) / 2 - ink2.X, (size - ink2.Height) / 2 - ink2.Y);
        }
        context.DrawText(_letter, _letterOrigin);
    }
}

/// <summary>What an <see cref="AiSourceMark"/> draws.</summary>
internal enum AiSourceMarkKind
{
    /// <summary>Nothing: no mark, no domain, no title.</summary>
    None,

    /// <summary>The source's geometry, in its color.</summary>
    Geometry,

    /// <summary>The source's image.</summary>
    Image,

    /// <summary>The first letter of the domain on a round chip.</summary>
    Letter
}
