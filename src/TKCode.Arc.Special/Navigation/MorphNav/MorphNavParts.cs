using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>The closed face of a <see cref="MorphNav"/>: the mark, the title, the current page and the menu glyph.</summary>
[TemplatePart("PART_Root", typeof(Panel))]
internal sealed class MorphNavPill : AvaloniaButton
{
    public static readonly StyledProperty<Geometry?> MarkProperty =
        AvaloniaProperty.Register<MorphNavPill, Geometry?>(nameof(Mark));

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<MorphNavPill, string?>(nameof(Title));

    public static readonly StyledProperty<string?> CurrentProperty =
        AvaloniaProperty.Register<MorphNavPill, string?>(nameof(Current));

    private LazyFocusRing? _focusRing;

    public Geometry? Mark { get => GetValue(MarkProperty); set => SetValue(MarkProperty, value); }

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public string? Current { get => GetValue(CurrentProperty); set => SetValue(CurrentProperty, value); }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _focusRing ??= new LazyFocusRing(this, MorphNavTokens.Key(MorphNavTokenKind.FocusRingMargin),
            ArcDesignTokens.Key(ArcDesignTokenKind.RadiusStep20));
        _focusRing.Attach(e.NameScope.Find<Panel>("PART_Root"));
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing?.Update();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing?.Update();
    }
}

/// <summary>
/// Lays a <see cref="MorphNav"/>'s sections out in equal columns, as many side by side as the width allows (at least
/// <see cref="MinColumnWidth"/> each), wrapping into further rows.
/// </summary>
internal sealed class MorphNavColumns : Panel
{
    public static readonly StyledProperty<double> MinColumnWidthProperty =
        AvaloniaProperty.Register<MorphNavColumns, double>(nameof(MinColumnWidth), 176);

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<MorphNavColumns, double>(nameof(Gap), 12);

    static MorphNavColumns()
    {
        AffectsMeasure<MorphNavColumns>(MinColumnWidthProperty, GapProperty);
    }

    public double MinColumnWidth { get => GetValue(MinColumnWidthProperty); set => SetValue(MinColumnWidthProperty, value); }

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    /// <summary>How many sections sit side by side in the last layout.</summary>
    public int Columns { get; private set; } = 1;

    internal static int ColumnsFor(double width, int sections, double minColumn, double gap)
    {
        if (sections <= 1 || double.IsInfinity(width))
        {
            return Math.Max(1, double.IsInfinity(width) ? sections : 1);
        }
        var fit = (int)Math.Floor((width + gap) / (Math.Max(1, minColumn) + gap));
        return Math.Clamp(fit, 1, sections);
    }

    private double ColumnWidth(double width) => (width - (Columns - 1) * Gap) / Columns;

    protected override Size MeasureOverride(Size availableSize)
    {
        var count = Children.Count;
        var width = double.IsInfinity(availableSize.Width) ? MinColumnWidth * Math.Max(1, count) + Gap * Math.Max(0, count - 1) : availableSize.Width;
        Columns = ColumnsFor(width, count, MinColumnWidth, Gap);
        var column = Math.Max(0, ColumnWidth(width));
        var height = 0.0;
        for (var start = 0; start < count; start += Columns)
        {
            var row = 0.0;
            for (var i = start; i < Math.Min(count, start + Columns); i++)
            {
                Children[i].Measure(new Size(column, double.PositiveInfinity));
                row = Math.Max(row, Children[i].DesiredSize.Height);
            }
            height += row + (start > 0 ? Gap : 0);
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var count = Children.Count;
        var column = Math.Max(0, ColumnWidth(finalSize.Width));
        var y = 0.0;
        for (var start = 0; start < count; start += Columns)
        {
            var row = 0.0;
            for (var i = start; i < Math.Min(count, start + Columns); i++)
            {
                row = Math.Max(row, Children[i].DesiredSize.Height);
            }
            for (var i = start; i < Math.Min(count, start + Columns); i++)
            {
                var x = (i - start) * (column + Gap);
                Children[i].Arrange(new Rect(x, y, column, row));
            }
            y += row + Gap;
        }
        return finalSize;
    }
}

/// <summary>
/// One link of a <see cref="MorphNav"/>'s panel, drawn by itself: a glyph tile, the text and the line under it, a hover
/// tint and the keyboard ring. Its text layouts are kept until the text, the font or the width changes, so hovering and
/// focusing only repaint.
/// </summary>
[PseudoClasses(CurrentPseudoClass)]
internal sealed class MorphNavLinkRow : Control
{
    internal const string CurrentPseudoClass = ":current";

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<MorphNavLinkRow, IBrush?>(nameof(Foreground));

    public static readonly StyledProperty<IBrush?> SecondaryForegroundProperty =
        AvaloniaProperty.Register<MorphNavLinkRow, IBrush?>(nameof(SecondaryForeground));

    public static readonly StyledProperty<IBrush?> HoverBrushProperty =
        AvaloniaProperty.Register<MorphNavLinkRow, IBrush?>(nameof(HoverBrush));

    public static readonly StyledProperty<IBrush?> TileBrushProperty =
        AvaloniaProperty.Register<MorphNavLinkRow, IBrush?>(nameof(TileBrush));

    public static readonly StyledProperty<IBrush?> FocusRingBrushProperty =
        AvaloniaProperty.Register<MorphNavLinkRow, IBrush?>(nameof(FocusRingBrush));

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<MorphNavLinkRow, CornerRadius>(nameof(CornerRadius));

    public static readonly StyledProperty<CornerRadius> TileCornerRadiusProperty =
        AvaloniaProperty.Register<MorphNavLinkRow, CornerRadius>(nameof(TileCornerRadius));

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        TextBlock.FontFamilyProperty.AddOwner<MorphNavLinkRow>();

    public static readonly StyledProperty<double> FontSizeProperty =
        TextBlock.FontSizeProperty.AddOwner<MorphNavLinkRow>();

    public static readonly StyledProperty<double> SecondaryFontSizeProperty =
        AvaloniaProperty.Register<MorphNavLinkRow, double>(nameof(SecondaryFontSize), 12);

    public static readonly StyledProperty<FontWeight> FontWeightProperty =
        TextBlock.FontWeightProperty.AddOwner<MorphNavLinkRow>();

    public static readonly StyledProperty<double> RowHeightProperty =
        AvaloniaProperty.Register<MorphNavLinkRow, double>(nameof(RowHeight), 36);

    public static readonly StyledProperty<double> TallRowHeightProperty =
        AvaloniaProperty.Register<MorphNavLinkRow, double>(nameof(TallRowHeight), 52);

    public static readonly StyledProperty<double> InsetProperty =
        AvaloniaProperty.Register<MorphNavLinkRow, double>(nameof(Inset), 8);

    public static readonly StyledProperty<double> TileSizeProperty =
        AvaloniaProperty.Register<MorphNavLinkRow, double>(nameof(TileSize), 32);

    public static readonly StyledProperty<double> GlyphSizeProperty =
        AvaloniaProperty.Register<MorphNavLinkRow, double>(nameof(GlyphSize), 16);

    private TextLayout? _title;
    private TextLayout? _description;
    private double _layoutWidth = double.NaN;
    private Pen? _glyphPen;
    private IBrush? _glyphPenBrush;
    private Pen? _ringPen;
    private IBrush? _ringPenBrush;
    private bool _pressed;

    static MorphNavLinkRow()
    {
        AffectsMeasure<MorphNavLinkRow>(FontFamilyProperty, FontSizeProperty, SecondaryFontSizeProperty, FontWeightProperty,
            RowHeightProperty, TallRowHeightProperty, InsetProperty, TileSizeProperty);
        AffectsRender<MorphNavLinkRow>(HoverBrushProperty, TileBrushProperty, FocusRingBrushProperty, CornerRadiusProperty,
            TileCornerRadiusProperty, GlyphSizeProperty);
        FocusableProperty.OverrideDefaultValue<MorphNavLinkRow>(true);
        CursorProperty.OverrideDefaultValue<MorphNavLinkRow>(new Cursor(StandardCursorType.Hand));
    }

    public MorphNavLinkRow(MorphNavLink link)
    {
        Link = link;
        AutomationProperties.SetName(this, link.Text);
        if (link.Description is { Length: > 0 } help)
        {
            AutomationProperties.SetHelpText(this, help);
        }
    }

    public MorphNavLink Link { get; }

    /// <summary>The nav that owns the row; it is told when the row is invoked.</summary>
    public MorphNav? Owner { get; set; }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public IBrush? SecondaryForeground { get => GetValue(SecondaryForegroundProperty); set => SetValue(SecondaryForegroundProperty, value); }

    public IBrush? HoverBrush { get => GetValue(HoverBrushProperty); set => SetValue(HoverBrushProperty, value); }

    public IBrush? TileBrush { get => GetValue(TileBrushProperty); set => SetValue(TileBrushProperty, value); }

    public IBrush? FocusRingBrush { get => GetValue(FocusRingBrushProperty); set => SetValue(FocusRingBrushProperty, value); }

    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    public CornerRadius TileCornerRadius { get => GetValue(TileCornerRadiusProperty); set => SetValue(TileCornerRadiusProperty, value); }

    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    public double SecondaryFontSize { get => GetValue(SecondaryFontSizeProperty); set => SetValue(SecondaryFontSizeProperty, value); }

    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }

    public double RowHeight { get => GetValue(RowHeightProperty); set => SetValue(RowHeightProperty, value); }

    public double TallRowHeight { get => GetValue(TallRowHeightProperty); set => SetValue(TallRowHeightProperty, value); }

    public double Inset { get => GetValue(InsetProperty); set => SetValue(InsetProperty, value); }

    public double TileSize { get => GetValue(TileSizeProperty); set => SetValue(TileSizeProperty, value); }

    public double GlyphSize { get => GetValue(GlyphSizeProperty); set => SetValue(GlyphSizeProperty, value); }

    /// <summary>Marks the row as the page the nav is on.</summary>
    public void SetCurrent(bool current) => PseudoClasses.Set(CurrentPseudoClass, current);

    private double TextLeft => Link.Glyph is null ? Inset + 2 : Inset + TileSize + 12;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ForegroundProperty || change.Property == SecondaryForegroundProperty
            || change.Property == FontFamilyProperty || change.Property == FontSizeProperty
            || change.Property == SecondaryFontSizeProperty || change.Property == FontWeightProperty)
        {
            DropLayouts();
            InvalidateMeasure();
            InvalidateVisual();
        }
        else if (change.Property == IsPointerOverProperty)
        {
            InvalidateVisual();
        }
    }

    private void DropLayouts()
    {
        _title?.Dispose();
        _description?.Dispose();
        _title = null;
        _description = null;
        _layoutWidth = double.NaN;
    }

    private void EnsureLayouts(double width)
    {
        var textWidth = Math.Max(1, width - TextLeft - Inset);
        if (_title is not null && Math.Abs(_layoutWidth - textWidth) < 0.5)
        {
            return;
        }
        DropLayouts();
        _layoutWidth = textWidth;
        var family = FontFamily ?? FontFamily.Default;
        _title = new TextLayout(Link.Text ?? string.Empty, new Typeface(family, FontStyle.Normal, FontWeight), FontSize, Foreground,
            TextAlignment.Left, TextWrapping.NoWrap, TextTrimming.CharacterEllipsis, null, FlowDirection.LeftToRight, textWidth);
        if (Link.Description is { Length: > 0 } description)
        {
            _description = new TextLayout(description, new Typeface(family), SecondaryFontSize, SecondaryForeground,
                TextAlignment.Left, TextWrapping.NoWrap, TextTrimming.CharacterEllipsis, null, FlowDirection.LeftToRight, textWidth);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var height = Link.Description is { Length: > 0 } ? TallRowHeight : RowHeight;
        var width = double.IsInfinity(availableSize.Width) ? 240 : availableSize.Width;
        EnsureLayouts(width);
        return new Size(width, height);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.Handled && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _pressed = true;
            e.Pointer.Capture(this);
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pressed)
        {
            return;
        }
        _pressed = false;
        e.Pointer.Capture(null);
        e.Handled = true;
        if (new Rect(Bounds.Size).Contains(e.GetPosition(this)))
        {
            Owner?.Invoke(this);
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pressed = false;
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        var radius = CornerRadius.TopLeft;
        context.FillRectangle(Brushes.Transparent, bounds);
        if ((IsPointerOver || Classes.Contains(CurrentPseudoClass)) && HoverBrush is { } hover)
        {
            context.DrawRectangle(hover, null, new RoundedRect(bounds, radius));
        }
        if (Link.Glyph is { } glyph)
        {
            var tile = new Rect(Inset, (bounds.Height - TileSize) / 2, TileSize, TileSize);
            if (TileBrush is { } tileBrush)
            {
                context.DrawRectangle(tileBrush, null, new RoundedRect(tile, TileCornerRadius.TopLeft));
            }
            if (Foreground is { } ink)
            {
                if (_glyphPen is null || !ReferenceEquals(ink, _glyphPenBrush))
                {
                    _glyphPen = new Pen(ink, 1.75, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
                    _glyphPenBrush = ink;
                }
                var scale = GlyphSize / 24;
                var origin = tile.Center - new Point(GlyphSize / 2, GlyphSize / 2);
                using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(origin.X, origin.Y)))
                {
                    context.DrawGeometry(null, _glyphPen, glyph);
                }
            }
        }
        if (_title is not null)
        {
            var left = TextLeft;
            var block = _title.Height + (_description?.Height ?? 0);
            var top = (bounds.Height - block) / 2;
            _title.Draw(context, new Point(left, top));
            _description?.Draw(context, new Point(left, top + _title.Height));
        }
        if (Classes.Contains(":focus-visible") && FocusRingBrush is { } ring)
        {
            if (_ringPen is null || !ReferenceEquals(ring, _ringPenBrush))
            {
                _ringPen = new Pen(ring, 2);
                _ringPenBrush = ring;
            }
            // Inside the row: the panel clips its content while it moves.
            context.DrawRectangle(null, _ringPen, new RoundedRect(bounds.Deflate(1), Math.Max(0, radius - 1)));
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        DropLayouts();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        InvalidateMeasure();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new MorphNavLinkRowAutomationPeer(this);

    private sealed class MorphNavLinkRowAutomationPeer : ControlAutomationPeer, IInvokeProvider
    {
        public MorphNavLinkRowAutomationPeer(MorphNavLinkRow owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Hyperlink;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        public void Invoke()
        {
            var row = (MorphNavLinkRow)Owner;
            row.Owner?.Invoke(row);
        }
    }
}
