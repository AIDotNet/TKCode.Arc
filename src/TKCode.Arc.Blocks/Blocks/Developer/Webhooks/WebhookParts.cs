using Avalonia.Controls.Documents;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>Builders for the code-made parts of <see cref="Webhooks"/> (text, dots, glyphs), bound to ArcDesign tokens.</summary>
internal static class WebhookUi
{
    /// <summary>A text block with token-bound size, color and (optionally) the mono font.</summary>
    public static TextBlock Text(string? text, ArcDesignTokenKind size, ArcDesignTokenKind color,
        FontWeight weight = FontWeight.Normal, bool mono = false, double lineHeight = double.NaN)
    {
        var block = new TextBlock { Text = text, FontWeight = weight, LineHeight = lineHeight };
        ArcTokens.Bind(block, TextBlock.FontSizeProperty, size);
        ArcTokens.Bind(block, TextBlock.ForegroundProperty, color);
        ArcTokens.Bind(block, TextBlock.FontFamilyProperty, mono ? ArcDesignTokenKind.MonoFontFamily : ArcDesignTokenKind.BodyFontFamily);
        return block;
    }

    /// <summary>A 3px round separator dot (BorderStrong).</summary>
    public static Control Dot()
    {
        var dot = new Border { Width = 3, Height = 3, VerticalAlignment = VerticalAlignment.Center }.BindCornerRadius(ArcDesignTokenKind.RadiusPill);
        ArcTokens.Bind(dot, Border.BackgroundProperty, ArcDesignTokenKind.BorderStrong);
        return dot;
    }

    /// <summary>A Lucide glyph in a token color.</summary>
    public static ArcGlyph Glyph(Geometry data, double size, ArcDesignTokenKind color, double stroke = 1.75)
    {
        var glyph = new ArcGlyph { Data = data, Size = size, StrokeThickness = stroke, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(glyph, ArcGlyph.ForegroundProperty, color);
        return glyph;
    }

    /// <summary>A horizontal row with a gap.</summary>
    public static StackPanel Row(double spacing, params Control[] children)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = spacing };
        foreach (var child in children)
        {
            row.Children.Add(child);
        }
        return row;
    }

    /// <summary>A button label with a leading glyph or spinner ("↻ Retry", "◌ Resending").</summary>
    public static Control ButtonLabel(Control? leading, string text)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        if (leading is not null)
        {
            row.Children.Add(leading);
        }
        row.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
        return row;
    }

    /// <summary>A 14px spinner for a busy button.</summary>
    public static Control Spinner() => new ArcSpinner { Width = 14, Height = 14, VerticalAlignment = VerticalAlignment.Center };
}

/// <summary>
/// A list row's frame: a transparent cell with an optional top separator and the hover plate (inset 8 / 2, radius 10,
/// SurfaceMuted 80% into Surface, 160ms), hidden while the row is selected (the gliding selection shows instead). The
/// list panel owns the frame's opacity and transforms, so everything visible is a child.
/// </summary>
internal class WebhookRowFrame : ContentControl
{
    /// <summary>Where the hover plate sits.</summary>
    public static readonly StyledProperty<Thickness> PlateInsetProperty =
        AvaloniaProperty.Register<WebhookRowFrame, Thickness>(nameof(PlateInset), new Thickness(8, 2));

    /// <summary>The hover plate's fill (mixed at run time).</summary>
    public static readonly StyledProperty<IBrush?> HoverBrushProperty =
        AvaloniaProperty.Register<WebhookRowFrame, IBrush?>(nameof(HoverBrush));

    /// <summary>Whether the row is the selected one (hides the hover plate).</summary>
    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<WebhookRowFrame, bool>(nameof(IsSelected));

    /// <summary>Whether a 1px BorderSubtle line runs along the top (endpoint rows after the first).</summary>
    public static readonly StyledProperty<bool> ShowsSeparatorProperty =
        AvaloniaProperty.Register<WebhookRowFrame, bool>(nameof(ShowsSeparator));

    public Thickness PlateInset { get => GetValue(PlateInsetProperty); set => SetValue(PlateInsetProperty, value); }

    public IBrush? HoverBrush { get => GetValue(HoverBrushProperty); set => SetValue(HoverBrushProperty, value); }

    public bool IsSelected { get => GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }

    public bool ShowsSeparator { get => GetValue(ShowsSeparatorProperty); set => SetValue(ShowsSeparatorProperty, value); }

    protected override Type StyleKeyOverride => typeof(WebhookRowFrame);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsSelectedProperty)
        {
            PseudoClasses.Set(":selected", IsSelected);
        }
    }
}

/// <summary>
/// The button of a row (the whole endpoint row; a delivery row without its action column): transparent, a 2px focus
/// ring of the plate's shape on keyboard focus (radius 14 = plate 10 + offset 2 + width 2).
/// </summary>
internal sealed class WebhookRowButton : AvaloniaButton
{
    /// <summary>Where the focus ring sits around the button.</summary>
    public static readonly StyledProperty<Thickness> RingMarginProperty =
        AvaloniaProperty.Register<WebhookRowButton, Thickness>(nameof(RingMargin), new Thickness(4, -2));

    public Thickness RingMargin { get => GetValue(RingMarginProperty); set => SetValue(RingMarginProperty, value); }

    /// <summary>The row's item (an endpoint or a delivery).</summary>
    public object? Item { get; set; }

    protected override Type StyleKeyOverride => typeof(WebhookRowButton);
}

/// <summary>A delivery's event name: Foreground, dimmed to TextSecondary once resolved (240ms in the theme).</summary>
internal sealed class WebhookEventText : TemplatedControl
{
    /// <summary>The event name.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<WebhookEventText, string?>(nameof(Text));

    /// <summary>Whether the delivery was resolved.</summary>
    public static readonly StyledProperty<bool> IsDimmedProperty =
        AvaloniaProperty.Register<WebhookEventText, bool>(nameof(IsDimmed));

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public bool IsDimmed { get => GetValue(IsDimmedProperty); set => SetValue(IsDimmedProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsDimmedProperty)
        {
            PseudoClasses.Set(":dimmed", IsDimmed);
        }
    }
}

/// <summary>
/// Text whose changed characters swap one slot at a time (Arc Webhooks' rate and endpoint count, design reference): the
/// old character leaves upward by 0.6em and fades, the new one comes up from 0.6em below, on the Quick spring, each slot
/// 30ms after the one to its right. Characters that do not change stay still; added slots appear at once. Digits take
/// the width of "0" (tabular figures). Self-drawn with cached text layouts: no per-frame allocation. Code-driven because
/// every slot has its own delayed spring and the text is replaced, not styled. Reduced and Off replace at once (Arc).
/// </summary>
internal sealed class WebhookSlotText : Control
{
    /// <summary>The text.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<WebhookSlotText, string?>(nameof(Text));

    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<WebhookSlotText>();
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<WebhookSlotText>();
    public static readonly StyledProperty<FontWeight> FontWeightProperty = TextElement.FontWeightProperty.AddOwner<WebhookSlotText>();
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<WebhookSlotText>();

    /// <summary>Line height in px (NaN: 1.3 × the font size).</summary>
    public static readonly StyledProperty<double> LineHeightProperty =
        AvaloniaProperty.Register<WebhookSlotText, double>(nameof(LineHeight), double.NaN);

    private sealed class Slot
    {
        public char Old;
        public char New;
        public TextLayout? OldLayout;
        public TextLayout? NewLayout;
        public double OldX;
        public double OldWidth;
        public double NewX;
        public double NewWidth;
        public bool HasOld;
        public bool HasNew;
        public MotionValue<double>? Progress;
    }

    private readonly List<Slot> _slots = new();
    private string _shown = string.Empty;
    private double _width;
    private double _height;
    private double _digitWidth;
    private bool _layoutsDirty = true;

    static WebhookSlotText()
    {
        AffectsMeasure<WebhookSlotText>(TextProperty, FontFamilyProperty, FontSizeProperty, FontWeightProperty, LineHeightProperty);
        AffectsRender<WebhookSlotText>(ForegroundProperty);
    }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public double LineHeight { get => GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }

    /// <summary>Whether a slot is still moving.</summary>
    public bool IsMoving => _slots.Any(s => s.Progress?.IsAnimating == true);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            Swap(change.GetOldValue<string?>() ?? string.Empty, change.GetNewValue<string?>() ?? string.Empty);
        }
        else if (change.Property == FontFamilyProperty || change.Property == FontSizeProperty ||
                 change.Property == FontWeightProperty || change.Property == ForegroundProperty)
        {
            _layoutsDirty = true;
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    private void Swap(string oldText, string newText)
    {
        var animate = oldText.Length > 0 && VisualRoot is not null && ArcMotion.IsFull(this);
        foreach (var slot in _slots)
        {
            slot.Progress?.Stop();
        }
        _slots.Clear();
        var count = Math.Max(oldText.Length, newText.Length);
        for (var fromRight = 0; fromRight < count; fromRight++)
        {
            var oi = oldText.Length - 1 - fromRight;
            var ni = newText.Length - 1 - fromRight;
            var slot = new Slot
            {
                HasOld = oi >= 0,
                HasNew = ni >= 0,
                Old = oi >= 0 ? oldText[oi] : ' ',
                New = ni >= 0 ? newText[ni] : ' ',
            };
            var changed = !slot.HasOld || !slot.HasNew || slot.Old != slot.New;
            if (animate && changed && slot.HasOld)
            {
                slot.Progress = new MotionValue<double>(this, 0, MotionVectors.Double);
                slot.Progress.Changed += _ => InvalidateVisual();
                _ = slot.Progress.AnimateTo(1, MotionSpec.Spring(DeveloperSprings.Quick,
                    TimeSpan.FromMilliseconds(DeveloperSprings.DigitStagger.TotalMilliseconds * fromRight)));
            }
            _slots.Insert(0, slot);
        }
        _shown = newText;
        _layoutsDirty = true;
        InvalidateMeasure();
        InvalidateVisual();
    }

    private Typeface Face => new(FontFamily, FontStyle.Normal, FontWeight);

    private TextLayout Layout(char c) =>
        new(c.ToString(), Face, FontSize, Foreground ?? Brushes.Black, TextAlignment.Left, TextWrapping.NoWrap);

    private double WidthOf(char c, TextLayout layout) => char.IsDigit(c) ? Math.Max(_digitWidth, layout.WidthIncludingTrailingWhitespace) : layout.WidthIncludingTrailingWhitespace;

    private void BuildLayouts()
    {
        if (!_layoutsDirty)
        {
            return;
        }
        _layoutsDirty = false;
        using (var zero = Layout('0'))
        {
            _digitWidth = zero.WidthIncludingTrailingWhitespace;
        }
        double oldTotal = 0;
        double newTotal = 0;
        foreach (var slot in _slots)
        {
            slot.OldLayout?.Dispose();
            slot.NewLayout?.Dispose();
            slot.OldLayout = slot.HasOld && slot.Progress is not null ? Layout(slot.Old) : null;
            slot.NewLayout = slot.HasNew ? Layout(slot.New) : null;
            slot.OldWidth = slot.OldLayout is null ? 0 : WidthOf(slot.Old, slot.OldLayout);
            slot.NewWidth = slot.NewLayout is null ? 0 : WidthOf(slot.New, slot.NewLayout);
            oldTotal += slot.OldWidth;
            newTotal += slot.NewWidth;
        }
        // Old characters keep their place against the right edge of the new text.
        double ox = newTotal - oldTotal;
        double nx = 0;
        foreach (var slot in _slots)
        {
            slot.OldX = ox;
            slot.NewX = nx;
            ox += slot.OldWidth;
            nx += slot.NewWidth;
        }
        _width = newTotal;
        _height = double.IsNaN(LineHeight) ? Math.Round(FontSize * 1.3, 1) : LineHeight;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        BuildLayouts();
        return new Size(_width, _height);
    }

    public override void Render(DrawingContext context)
    {
        BuildLayouts();
        var travel = FontSize * 0.6;
        using var clip = context.PushClip(new Rect(-2, -travel, Bounds.Width + 4, Bounds.Height + travel * 2));
        foreach (var slot in _slots)
        {
            var p = slot.Progress?.Value ?? 1;
            if (slot.NewLayout is { } next)
            {
                var top = (_height - next.Height) / 2 + travel * (1 - p);
                var x = slot.NewX + (slot.NewWidth - next.WidthIncludingTrailingWhitespace) / 2;
                using (context.PushOpacity(Math.Clamp(p / 0.55, 0, 1)))
                {
                    next.Draw(context, new Point(x, top));
                }
            }
            if (p < 1 && slot.OldLayout is { } previous)
            {
                var top = (_height - previous.Height) / 2 - travel * p;
                var x = slot.OldX + (slot.OldWidth - previous.WidthIncludingTrailingWhitespace) / 2;
                using (context.PushOpacity(Math.Clamp(1 - p / 0.55, 0, 1)))
                {
                    previous.Draw(context, new Point(x, top));
                }
            }
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        foreach (var slot in _slots)
        {
            slot.Progress?.Jump(1);
        }
    }
}
