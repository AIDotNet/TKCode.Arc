using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>
/// What a share sheet's tiles and rows have in common: they draw themselves, take a press anywhere in their box, show a
/// hover tint and an inset keyboard ring, and report an invoke (pointer, Enter, Space or assistive technology) to the
/// panel. Their text layouts are kept until the text, the font or the width changes.
/// </summary>
internal abstract class ShareSheetPressable : Control
{
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<ShareSheetPressable, IBrush?>(nameof(Foreground));

    public static readonly StyledProperty<IBrush?> HoverBrushProperty =
        AvaloniaProperty.Register<ShareSheetPressable, IBrush?>(nameof(HoverBrush));

    public static readonly StyledProperty<IBrush?> FocusRingBrushProperty =
        AvaloniaProperty.Register<ShareSheetPressable, IBrush?>(nameof(FocusRingBrush));

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<ShareSheetPressable, CornerRadius>(nameof(CornerRadius));

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        TextBlock.FontFamilyProperty.AddOwner<ShareSheetPressable>();

    public static readonly StyledProperty<double> FontSizeProperty =
        TextBlock.FontSizeProperty.AddOwner<ShareSheetPressable>();

    private Pen? _ringPen;
    private IBrush? _ringPenBrush;
    private Pen? _glyphPen;
    private IBrush? _glyphPenBrush;
    private bool _pressed;

    static ShareSheetPressable()
    {
        AffectsMeasure<ShareSheetPressable>(FontFamilyProperty, FontSizeProperty);
        AffectsRender<ShareSheetPressable>(HoverBrushProperty, FocusRingBrushProperty, CornerRadiusProperty);
        FocusableProperty.OverrideDefaultValue<ShareSheetPressable>(true);
        CursorProperty.OverrideDefaultValue<ShareSheetPressable>(new Cursor(StandardCursorType.Hand));
    }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public IBrush? HoverBrush { get => GetValue(HoverBrushProperty); set => SetValue(HoverBrushProperty, value); }

    public IBrush? FocusRingBrush { get => GetValue(FocusRingBrushProperty); set => SetValue(FocusRingBrushProperty, value); }

    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>Called when the element is invoked.</summary>
    public Action<ShareSheetPressable>? Invoked { get; set; }

    public void Invoke() => Invoked?.Invoke(this);

    protected abstract void DropText();

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ForegroundProperty || change.Property == FontFamilyProperty || change.Property == FontSizeProperty)
        {
            DropText();
            InvalidateMeasure();
            InvalidateVisual();
        }
        else if (change.Property == IsPointerOverProperty)
        {
            InvalidateVisual();
        }
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
            Invoke();
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
        this.BringIntoView();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        DropText();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        InvalidateMeasure();
    }

    protected Typeface Typeface(FontWeight weight = FontWeight.Normal) => new(FontFamily ?? FontFamily.Default, FontStyle.Normal, weight);

    protected void DrawHover(DrawingContext context, Rect bounds)
    {
        context.FillRectangle(Brushes.Transparent, bounds);
        if (IsPointerOver && HoverBrush is { } hover)
        {
            context.DrawRectangle(hover, null, new RoundedRect(bounds, CornerRadius.TopLeft));
        }
    }

    protected void DrawGlyph(DrawingContext context, Geometry glyph, IBrush ink, Rect box)
    {
        if (_glyphPen is null || !ReferenceEquals(ink, _glyphPenBrush))
        {
            _glyphPen = new Pen(ink, 1.75, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            _glyphPenBrush = ink;
        }
        var scale = box.Width / 24;
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(box.X, box.Y)))
        {
            context.DrawGeometry(null, _glyphPen, glyph);
        }
    }

    protected void DrawFocusRing(DrawingContext context, Rect bounds)
    {
        if (!Classes.Contains(":focus-visible") || FocusRingBrush is not { } ring)
        {
            return;
        }
        if (_ringPen is null || !ReferenceEquals(ring, _ringPenBrush))
        {
            _ringPen = new Pen(ring, 2);
            _ringPenBrush = ring;
        }
        // Inside the element: the strip scrolls and clips at its edges.
        context.DrawRectangle(null, _ringPen, new RoundedRect(bounds.Deflate(1), Math.Max(0, CornerRadius.TopLeft - 1)));
    }

    protected abstract class PressablePeer : ControlAutomationPeer, IInvokeProvider
    {
        protected PressablePeer(ShareSheetPressable owner) : base(owner)
        {
        }

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        public void Invoke() => ((ShareSheetPressable)Owner).Invoke();
    }
}

/// <summary>A person or a destination in a share sheet: a soft circle with initials, or a glyph tile, and the name under it.</summary>
internal sealed class ShareSheetTile : ShareSheetPressable
{
    public static readonly StyledProperty<IBrush?> AvatarBrushProperty =
        AvaloniaProperty.Register<ShareSheetTile, IBrush?>(nameof(AvatarBrush));

    public static readonly StyledProperty<double> AvatarTintOpacityProperty =
        AvaloniaProperty.Register<ShareSheetTile, double>(nameof(AvatarTintOpacity), 0.16);

    public static readonly StyledProperty<IBrush?> TileBrushProperty =
        AvaloniaProperty.Register<ShareSheetTile, IBrush?>(nameof(TileBrush));

    public static readonly StyledProperty<CornerRadius> AvatarCornerRadiusProperty =
        AvaloniaProperty.Register<ShareSheetTile, CornerRadius>(nameof(AvatarCornerRadius));

    public static readonly StyledProperty<CornerRadius> TileCornerRadiusProperty =
        AvaloniaProperty.Register<ShareSheetTile, CornerRadius>(nameof(TileCornerRadius));

    public static readonly StyledProperty<double> TileWidthProperty =
        AvaloniaProperty.Register<ShareSheetTile, double>(nameof(TileWidth), 68);

    public static readonly StyledProperty<double> TileHeightProperty =
        AvaloniaProperty.Register<ShareSheetTile, double>(nameof(TileHeight), 82);

    public static readonly StyledProperty<double> AvatarSizeProperty =
        AvaloniaProperty.Register<ShareSheetTile, double>(nameof(AvatarSize), 48);

    public static readonly StyledProperty<double> GlyphSizeProperty =
        AvaloniaProperty.Register<ShareSheetTile, double>(nameof(GlyphSize), 20);

    public static readonly StyledProperty<double> NameGapProperty =
        AvaloniaProperty.Register<ShareSheetTile, double>(nameof(NameGap), 6);

    private TextLayout? _name;
    private TextLayout? _initials;

    static ShareSheetTile()
    {
        AffectsMeasure<ShareSheetTile>(TileWidthProperty, TileHeightProperty, AvatarSizeProperty, NameGapProperty);
        AffectsRender<ShareSheetTile>(AvatarBrushProperty, AvatarTintOpacityProperty, TileBrushProperty, AvatarCornerRadiusProperty,
            TileCornerRadiusProperty, GlyphSizeProperty);
    }

    public ShareSheetTile(ShareSheetTarget target, bool isPerson)
    {
        Target = target;
        IsPerson = isPerson;
        AutomationProperties.SetName(this, target.Name);
        if (target.Detail is { Length: > 0 } detail)
        {
            AutomationProperties.SetHelpText(this, detail);
        }
    }

    public ShareSheetTarget Target { get; }

    public bool IsPerson { get; }

    public IBrush? AvatarBrush { get => GetValue(AvatarBrushProperty); set => SetValue(AvatarBrushProperty, value); }

    public double AvatarTintOpacity { get => GetValue(AvatarTintOpacityProperty); set => SetValue(AvatarTintOpacityProperty, value); }

    public IBrush? TileBrush { get => GetValue(TileBrushProperty); set => SetValue(TileBrushProperty, value); }

    public CornerRadius AvatarCornerRadius { get => GetValue(AvatarCornerRadiusProperty); set => SetValue(AvatarCornerRadiusProperty, value); }

    public CornerRadius TileCornerRadius { get => GetValue(TileCornerRadiusProperty); set => SetValue(TileCornerRadiusProperty, value); }

    public double TileWidth { get => GetValue(TileWidthProperty); set => SetValue(TileWidthProperty, value); }

    public double TileHeight { get => GetValue(TileHeightProperty); set => SetValue(TileHeightProperty, value); }

    public double AvatarSize { get => GetValue(AvatarSizeProperty); set => SetValue(AvatarSizeProperty, value); }

    public double GlyphSize { get => GetValue(GlyphSizeProperty); set => SetValue(GlyphSizeProperty, value); }

    public double NameGap { get => GetValue(NameGapProperty); set => SetValue(NameGapProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == AvatarBrushProperty)
        {
            DropText();
            InvalidateMeasure();
        }
    }

    protected override void DropText()
    {
        _name?.Dispose();
        _initials?.Dispose();
        _name = null;
        _initials = null;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_name is null)
        {
            _name = new TextLayout(Target.Name ?? string.Empty, Typeface(), FontSize, Foreground, TextAlignment.Center,
                TextWrapping.NoWrap, TextTrimming.CharacterEllipsis, null, FlowDirection.LeftToRight, Math.Max(1, TileWidth - 4));
            if (IsPerson && Target.Glyph is null)
            {
                _initials = new TextLayout(Target.EffectiveInitials, Typeface(FontWeight.SemiBold), FontSize + 1, AvatarBrush);
            }
        }
        return new Size(TileWidth, TileHeight);
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        DrawHover(context, bounds);
        var size = AvatarSize;
        var avatar = new Rect((bounds.Width - size) / 2, 6, size, size);
        if (IsPerson && Target.Glyph is null)
        {
            var radius = Math.Min(AvatarCornerRadius.TopLeft, size / 2);
            if (AvatarBrush is { } tint)
            {
                using (context.PushOpacity(AvatarTintOpacity))
                {
                    context.DrawRectangle(tint, null, new RoundedRect(avatar, radius));
                }
            }
            if (_initials is not null)
            {
                _initials.Draw(context, new Point(avatar.Center.X - _initials.WidthIncludingTrailingWhitespace / 2,
                    avatar.Center.Y - _initials.Height / 2));
            }
        }
        else
        {
            if (TileBrush is { } fill)
            {
                context.DrawRectangle(fill, null, new RoundedRect(avatar, Math.Min(TileCornerRadius.TopLeft, size / 2)));
            }
            if (Target.Glyph is { } glyph && Foreground is { } ink)
            {
                var g = GlyphSize;
                DrawGlyph(context, glyph, ink, new Rect(avatar.Center.X - g / 2, avatar.Center.Y - g / 2, g, g));
            }
        }
        _name?.Draw(context, new Point(2, avatar.Bottom + NameGap));
        DrawFocusRing(context, bounds);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TilePeer(this);

    private sealed class TilePeer : PressablePeer
    {
        public TilePeer(ShareSheetTile owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;
    }
}

/// <summary>An action row in a share sheet: a glyph and its label.</summary>
internal sealed class ShareSheetActionRow : ShareSheetPressable
{
    public static readonly StyledProperty<double> RowHeightProperty =
        AvaloniaProperty.Register<ShareSheetActionRow, double>(nameof(RowHeight), 40);

    public static readonly StyledProperty<double> InsetProperty =
        AvaloniaProperty.Register<ShareSheetActionRow, double>(nameof(Inset), 10);

    public static readonly StyledProperty<double> GlyphSizeProperty =
        AvaloniaProperty.Register<ShareSheetActionRow, double>(nameof(GlyphSize), 16);

    private TextLayout? _label;
    private double _labelWidth = double.NaN;

    static ShareSheetActionRow()
    {
        AffectsMeasure<ShareSheetActionRow>(RowHeightProperty, InsetProperty, GlyphSizeProperty);
    }

    public ShareSheetActionRow(ShareSheetAction action)
    {
        Action = action;
        AutomationProperties.SetName(this, action.Label);
    }

    public ShareSheetAction Action { get; }

    public double RowHeight { get => GetValue(RowHeightProperty); set => SetValue(RowHeightProperty, value); }

    public double Inset { get => GetValue(InsetProperty); set => SetValue(InsetProperty, value); }

    public double GlyphSize { get => GetValue(GlyphSizeProperty); set => SetValue(GlyphSizeProperty, value); }

    private double TextLeft => Action.Glyph is null ? Inset : Inset + GlyphSize + 12;

    protected override void DropText()
    {
        _label?.Dispose();
        _label = null;
        _labelWidth = double.NaN;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 240 : availableSize.Width;
        var textWidth = Math.Max(1, width - TextLeft - Inset);
        if (_label is null || Math.Abs(_labelWidth - textWidth) > 0.5)
        {
            _label?.Dispose();
            _labelWidth = textWidth;
            _label = new TextLayout(Action.Label ?? string.Empty, Typeface(), FontSize, Foreground, TextAlignment.Left,
                TextWrapping.NoWrap, TextTrimming.CharacterEllipsis, null, FlowDirection.LeftToRight, textWidth);
        }
        return new Size(width, RowHeight);
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        DrawHover(context, bounds);
        if (Action.Glyph is { } glyph && Foreground is { } ink)
        {
            var g = GlyphSize;
            DrawGlyph(context, glyph, ink, new Rect(Inset, (bounds.Height - g) / 2, g, g));
        }
        _label?.Draw(context, new Point(TextLeft, (bounds.Height - _label.Height) / 2));
        DrawFocusRing(context, bounds);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new RowPeer(this);

    private sealed class RowPeer : PressablePeer
    {
        public RowPeer(ShareSheetActionRow owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.MenuItem;
    }
}
