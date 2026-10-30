using System.Globalization;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>How much room an <see cref="EnrichColumn"/> has.</summary>
internal enum EnrichColumnDensity
{
    /// <summary>560px and wider: everything shows.</summary>
    Regular,

    /// <summary>Below 560px: the domain, the source text and the model detail fold away.</summary>
    Compact,

    /// <summary>Below 440px: 16px gutters, values wrap to two lines in 60px rows, failure reasons hide behind "Failed".</summary>
    Narrow
}

/// <summary>
/// A quiet button of the enrich column: <c>.icon</c> is a 26px square (row run and retry, the column's edit control),
/// <c>.source</c> the source link after a value. Transparent at rest, SurfaceMuted under the pointer, a 2px keyboard ring.
/// </summary>
internal sealed class EnrichColumnButton : AvaloniaButton
{
    protected override Type StyleKeyOverride => typeof(EnrichColumnButton);
}

/// <summary>
/// A row's 16px logo: the row's own glyph (24 × 24 stroked, as Lucide) or, with none, the first letter of its name in a
/// tinted rounded square. Drawn in code because every row has one. Decorative.
/// </summary>
internal sealed class EnrichColumnLogo : Control
{
    public static readonly StyledProperty<EnrichColumnRow?> RowProperty =
        AvaloniaProperty.Register<EnrichColumnLogo, EnrichColumnRow?>(nameof(Row));

    /// <summary>The square behind the letter (theme: AccentSubtle).</summary>
    public static readonly StyledProperty<IBrush?> TintProperty =
        AvaloniaProperty.Register<EnrichColumnLogo, IBrush?>(nameof(Tint));

    /// <summary>The letter and a glyph without its own color (theme: AccentStrong and Foreground).</summary>
    public static readonly StyledProperty<IBrush?> InkProperty =
        AvaloniaProperty.Register<EnrichColumnLogo, IBrush?>(nameof(Ink));

    public static readonly StyledProperty<IBrush?> GlyphBrushProperty =
        AvaloniaProperty.Register<EnrichColumnLogo, IBrush?>(nameof(GlyphBrush));

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        Border.CornerRadiusProperty.AddOwner<EnrichColumnLogo>();

    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<EnrichColumnLogo, double>(nameof(Size), 16);

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        TextBlock.FontFamilyProperty.AddOwner<EnrichColumnLogo>();

    private FormattedText? _letter;
    private IPen? _pen;

    static EnrichColumnLogo()
    {
        AffectsRender<EnrichColumnLogo>(RowProperty, TintProperty, InkProperty, GlyphBrushProperty, CornerRadiusProperty);
        AffectsMeasure<EnrichColumnLogo>(SizeProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<EnrichColumnLogo>(false);
    }

    public EnrichColumnRow? Row { get => GetValue(RowProperty); set => SetValue(RowProperty, value); }

    public IBrush? Tint { get => GetValue(TintProperty); set => SetValue(TintProperty, value); }

    public IBrush? Ink { get => GetValue(InkProperty); set => SetValue(InkProperty, value); }

    public IBrush? GlyphBrush { get => GetValue(GlyphBrushProperty); set => SetValue(GlyphBrushProperty, value); }

    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    public double Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    /// <summary>The monogram's letter: the first letter or digit of the name, upper case.</summary>
    internal static string Monogram(string? name)
    {
        if (!string.IsNullOrEmpty(name))
        {
            foreach (var c in name)
            {
                if (char.IsLetterOrDigit(c))
                {
                    return char.ToUpper(c, CultureInfo.InvariantCulture).ToString();
                }
            }
        }
        return "?";
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RowProperty || change.Property == InkProperty || change.Property == FontFamilyProperty ||
            change.Property == SizeProperty)
        {
            _letter = null;
        }
        if (change.Property == RowProperty || change.Property == GlyphBrushProperty)
        {
            _pen = null;
        }
    }

    protected override Size MeasureOverride(Size availableSize) => new(Size, Size);

    protected override AutomationPeer OnCreateAutomationPeer() => new NoneAutomationPeer(this);

    public override void Render(DrawingContext context)
    {
        var row = Row;
        if (row is null)
        {
            return;
        }
        var size = Size;
        var box = new Rect((Bounds.Width - size) / 2, (Bounds.Height - size) / 2, size, size);
        if (row.Logo is { } logo)
        {
            var brush = row.LogoBrush ?? GlyphBrush;
            if (brush is null)
            {
                return;
            }
            _pen ??= new ImmutablePen(brush.ToImmutable(), 2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            using (context.PushTransform(Matrix.CreateScale(size / 24, size / 24) * Matrix.CreateTranslation(box.X, box.Y)))
            {
                context.DrawGeometry(null, _pen, logo);
            }
            return;
        }
        if (Tint is { } tint)
        {
            var r = CornerRadius.TopLeft;
            context.DrawRectangle(tint, null, new RoundedRect(box, r));
        }
        if (Ink is not { } ink)
        {
            return;
        }
        _letter ??= new FormattedText(Monogram(row.Name), CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily, FontStyle.Normal, FontWeight.SemiBold), size * 0.62, ink);
        context.DrawText(_letter, new Point(box.X + (size - _letter.Width) / 2, box.Y + (size - _letter.Height) / 2));
    }
}

/// <summary>One row of the table as the list holds it: the record, its current cell and the column it belongs to.</summary>
internal sealed class EnrichColumnRowItem
{
    public EnrichColumnRowItem(EnrichColumn owner, EnrichColumnRow row, EnrichColumnCell cell)
    {
        Owner = owner;
        Row = row;
        Cell = cell;
    }

    public EnrichColumn Owner { get; }

    public EnrichColumnRow Row { get; set; }

    public EnrichColumnCell Cell { get; private set; }

    /// <summary>Raised when the cell changes; the bound row view animates the swap.</summary>
    public event Action<EnrichColumnRowItem>? Changed;

    public void Update(EnrichColumnCell cell)
    {
        Cell = cell;
        Changed?.Invoke(this);
    }

    /// <summary>The row record itself changed (a new name or logo under the same id).</summary>
    public void Refresh() => Changed?.Invoke(this);
}
