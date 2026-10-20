using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>
/// P3 text roll for short copy that changes as a whole (strength words, inline-edit values, error lines): the new
/// text rises <see cref="Rise"/> em out of a soft blur on standard + enter while the old one leaves the other way
/// with a subtle blur on fast + standard. <see cref="Direction"/> -1 runs it downward (strength falling, a
/// rollback). Reduced motion crossfades; Off swaps at once. The outgoing text is out of layout (popLayout).
/// Drawn in <see cref="Render"/> from cached text layouts.
/// </summary>
internal sealed class FieldSwapText : Control
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<FieldSwapText, string?>(nameof(Text));

    public static readonly StyledProperty<int> DirectionProperty =
        AvaloniaProperty.Register<FieldSwapText, int>(nameof(Direction), 1);

    public static readonly StyledProperty<double> RiseProperty =
        AvaloniaProperty.Register<FieldSwapText, double>(nameof(Rise), 0.3);

    public static readonly StyledProperty<TextAlignment> TextAlignmentProperty =
        TextBlock.TextAlignmentProperty.AddOwner<FieldSwapText>();

    public static readonly StyledProperty<TextWrapping> TextWrappingProperty =
        TextBlock.TextWrappingProperty.AddOwner<FieldSwapText>();

    public static readonly StyledProperty<double> LineHeightProperty =
        AvaloniaProperty.Register<FieldSwapText, double>(nameof(LineHeight), double.NaN);

    public static readonly StyledProperty<Thickness> PaddingProperty = Decorator.PaddingProperty.AddOwner<FieldSwapText>();
    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<FieldSwapText>();
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<FieldSwapText>();
    public static readonly StyledProperty<FontWeight> FontWeightProperty = TextElement.FontWeightProperty.AddOwner<FieldSwapText>();
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<FieldSwapText>();

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public int Direction { get => GetValue(DirectionProperty); set => SetValue(DirectionProperty, value); }
    public double Rise { get => GetValue(RiseProperty); set => SetValue(RiseProperty, value); }
    public TextAlignment TextAlignment { get => GetValue(TextAlignmentProperty); set => SetValue(TextAlignmentProperty, value); }
    public TextWrapping TextWrapping { get => GetValue(TextWrappingProperty); set => SetValue(TextWrappingProperty, value); }
    public double LineHeight { get => GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }
    public Thickness Padding { get => GetValue(PaddingProperty); set => SetValue(PaddingProperty, value); }
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    private sealed class Layer
    {
        public string Text = string.Empty;
        public TextLayout? Layout;
        public double LayoutWidth = double.NaN;
        public MotionValue<double>? Progress;
        public bool FadeOnly;
        public int Direction = 1;
    }

    private Layer _current = new();
    private readonly List<Layer> _leaving = new();
    private bool _silent;

    static FieldSwapText()
    {
        AffectsMeasure<FieldSwapText>(FontFamilyProperty, FontSizeProperty, FontWeightProperty, LineHeightProperty,
            TextWrappingProperty, PaddingProperty);
        AffectsRender<FieldSwapText>(ForegroundProperty, TextAlignmentProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<FieldSwapText>(false);
    }

    private double EffectiveLineHeight => double.IsNaN(LineHeight) || LineHeight <= 0 ? FontSize * 1.4 : LineHeight;

    /// <summary>Replaces the text without motion (a draft mirroring keystrokes).</summary>
    public void SetTextSilently(string? text)
    {
        _silent = true;
        try
        {
            SetCurrentValue(TextProperty, text);
        }
        finally
        {
            _silent = false;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            Swap(Text ?? string.Empty);
            AutomationProperties.SetName(this, Text ?? string.Empty);
        }
        else if (change.Property == FontFamilyProperty || change.Property == FontSizeProperty ||
                 change.Property == FontWeightProperty || change.Property == ForegroundProperty ||
                 change.Property == LineHeightProperty || change.Property == TextWrappingProperty ||
                 change.Property == TextAlignmentProperty)
        {
            _current.Layout = null;
            foreach (var layer in _leaving)
            {
                layer.Layout = null;
            }
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    private void Swap(string next)
    {
        if (next == _current.Text)
        {
            return;
        }
        var level = IsLoaded && !_silent ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;
        if (level == ArcMotionLevel.Off)
        {
            _current.Text = next;
            _current.Layout = null;
            _current.Progress?.Jump(1);
            InvalidateMeasure();
            InvalidateVisual();
            return;
        }
        var reduced = level == ArcMotionLevel.Reduced;
        var direction = Direction >= 0 ? 1 : -1;
        var outgoing = _current;
        if (outgoing.Text.Length > 0)
        {
            outgoing.Direction = direction;
            outgoing.FadeOnly = reduced;
            outgoing.Progress?.Dispose();
            outgoing.Progress = CreateProgress(0);
            _leaving.Add(outgoing);
            _ = RemoveWhenDone(outgoing, outgoing.Progress.AnimateTo(1, reduced
                ? MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard)
                : MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard)));
        }
        _current = new Layer { Text = next, Direction = direction, FadeOnly = reduced, Progress = CreateProgress(0) };
        _ = _current.Progress.AnimateTo(1, reduced
            ? MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard)
            : MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter));
        InvalidateMeasure();
        InvalidateVisual();
    }

    private MotionValue<double> CreateProgress(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    private async Task RemoveWhenDone(Layer layer, Task<bool> exit)
    {
        await exit;
        if (_leaving.Remove(layer))
        {
            layer.Progress?.Dispose();
            InvalidateVisual();
        }
    }

    private TextLayout LayoutOf(Layer layer, double maxWidth)
    {
        if (layer.Layout is null || Math.Abs(layer.LayoutWidth - maxWidth) > 0.01)
        {
            layer.Layout?.Dispose();
            layer.LayoutWidth = maxWidth;
            layer.Layout = new TextLayout(layer.Text.Length == 0 ? "​" : layer.Text,
                new Typeface(FontFamily, FontStyle.Normal, FontWeight), FontSize, Foreground ?? Brushes.Black,
                TextAlignment, TextWrapping, maxWidth: maxWidth, lineHeight: EffectiveLineHeight);
        }
        return layer.Layout;
    }

    /// <summary>Character index under <paramref name="point"/> (in this control's coordinates).</summary>
    public int HitTest(Point point)
    {
        var padding = Padding;
        var layout = LayoutOf(_current, ContentWidth(Bounds.Width));
        var hit = layout.HitTestPoint(new Point(point.X - padding.Left, point.Y - padding.Top));
        return Math.Clamp(hit.TextPosition, 0, _current.Text.Length);
    }

    private double ContentWidth(double width)
    {
        var padding = Padding;
        return double.IsInfinity(width) ? double.PositiveInfinity : Math.Max(0, width - padding.Left - padding.Right);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var padding = Padding;
        var wrap = TextWrapping != TextWrapping.NoWrap;
        var layout = LayoutOf(_current, wrap ? ContentWidth(availableSize.Width) : double.PositiveInfinity);
        var height = Math.Max(EffectiveLineHeight, layout.Height);
        return new Size(layout.WidthIncludingTrailingWhitespace + padding.Left + padding.Right, height + padding.Top + padding.Bottom);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (TextWrapping != TextWrapping.NoWrap)
        {
            LayoutOf(_current, ContentWidth(finalSize.Width));
        }
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        var em = FontSize;
        var wrap = TextWrapping != TextWrapping.NoWrap;
        var maxWidth = wrap ? ContentWidth(Bounds.Width) : double.PositiveInfinity;
        foreach (var layer in _leaving)
        {
            DrawLayer(context, layer, LayoutOf(layer, maxWidth), em, leaving: true);
        }
        DrawLayer(context, _current, LayoutOf(_current, maxWidth), em, leaving: false);
    }

    private void DrawLayer(DrawingContext context, Layer layer, TextLayout layout, double em, bool leaving)
    {
        if (layer.Text.Length == 0)
        {
            return;
        }
        var p = Math.Clamp(layer.Progress?.Value ?? 1, 0, 1);
        double opacity, dy = 0, blur = 0;
        if (leaving)
        {
            opacity = 1 - p;
            if (!layer.FadeOnly)
            {
                dy = -Rise * em * p * layer.Direction;
                blur = 2 * p;
            }
        }
        else
        {
            opacity = p;
            if (!layer.FadeOnly)
            {
                dy = Rise * em * (1 - p) * layer.Direction;
                blur = 4 * (1 - p);
            }
        }
        if (opacity <= 0.001)
        {
            return;
        }
        var padding = Padding;
        var x = padding.Left;
        if (TextWrapping == TextWrapping.NoWrap)
        {
            var width = layout.WidthIncludingTrailingWhitespace;
            var available = Bounds.Width - padding.Left - padding.Right;
            x += TextAlignment switch
            {
                TextAlignment.Right or TextAlignment.End => available - width,
                TextAlignment.Center => (available - width) / 2,
                _ => 0
            };
        }
        var lineOffset = (Math.Max(EffectiveLineHeight, layout.Height) - layout.Height) / 2;
        var origin = new Point(x, padding.Top + lineOffset + dy);
        var effect = ArcBlur.Get(blur);
        using (context.PushOpacity(opacity))
        {
            if (effect is null)
            {
                layout.Draw(context, origin);
            }
            else
            {
                using (context.PushEffect(effect, new Rect(origin, new Size(layout.WidthIncludingTrailingWhitespace, layout.Height))))
                {
                    layout.Draw(context, origin);
                }
            }
        }
    }
}
