using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>Binds code-built parts of <see cref="ModelCard"/> to its control tokens and to ArcDesign tokens.</summary>
internal static class ModelCardTokenBinding
{
    /// <summary>Binds <paramref name="property"/> to a <see cref="ModelCardToken"/> value and returns the target.</summary>
    public static T Tok<T>(this T target, AvaloniaProperty property, ModelCardTokenKind kind) where T : StyledElement
    {
        target.Bind(property, target.GetResourceObservable(ModelCardTokens.Key(kind)));
        return target;
    }

    /// <summary>Binds <paramref name="property"/> to an ArcDesign token and returns the target.</summary>
    public static T Arc<T>(this T target, AvaloniaProperty property, ArcDesignTokenKind kind) where T : StyledElement
    {
        ArcTokens.Bind(target, property, kind);
        return target;
    }
}

/// <summary>
/// A text value of <see cref="ModelCard"/> that crossfades in place when it changes (Core <see cref="ArcContentSwap"/>,
/// Crossfade: out 100ms, in 160ms, no movement) and reserves <see cref="Lines"/> lines of height, so a longer or shorter
/// value never moves what is below it. Reduced motion and Off: replaced at once.
/// </summary>
internal sealed class ModelCardText : Control
{
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<ModelCardText>();

    public static readonly StyledProperty<FontWeight> FontWeightProperty = TextElement.FontWeightProperty.AddOwner<ModelCardText>();

    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<ModelCardText>();

    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<ModelCardText>();

    /// <summary>The height of one line; the reserved height is <see cref="Lines"/> times this.</summary>
    public static readonly StyledProperty<double> LineHeightProperty =
        AvaloniaProperty.Register<ModelCardText, double>(nameof(LineHeight), 18);

    /// <summary>How many lines are reserved; longer text is clamped with an ellipsis.</summary>
    public static readonly StyledProperty<int> LinesProperty =
        AvaloniaProperty.Register<ModelCardText, int>(nameof(Lines), 1);

    /// <summary>Which edge the text keeps to.</summary>
    public static readonly StyledProperty<TextAlignment> TextAlignmentProperty =
        AvaloniaProperty.Register<ModelCardText, TextAlignment>(nameof(TextAlignment));

    private readonly ArcContentSwap _swap;
    private TextBlock? _current;

    static ModelCardText()
    {
        AffectsMeasure<ModelCardText>(LineHeightProperty, LinesProperty);
    }

    public ModelCardText()
    {
        _swap = new ArcContentSwap
        {
            Mode = ContentSwapMode.None,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            VerticalContentAlignment = VerticalAlignment.Top
        };
        VisualChildren.Add(_swap);
        LogicalChildren.Add(_swap);
    }

    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }

    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public double LineHeight { get => GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }

    public int Lines { get => GetValue(LinesProperty); set => SetValue(LinesProperty, value); }

    public TextAlignment TextAlignment { get => GetValue(TextAlignmentProperty); set => SetValue(TextAlignmentProperty, value); }

    /// <summary>The text on screen.</summary>
    public string Text { get; private set; } = string.Empty;

    /// <summary>Shows <paramref name="text"/>; <paramref name="animate"/> crossfades it in (Full motion only).</summary>
    public void Show(string text, bool animate)
    {
        if (_current is not null && text == Text)
        {
            return;
        }
        Text = text;
        AutomationProperties.SetName(this, text);
        _swap.Mode = animate && IsLoaded && ArcMotion.IsFull(this) ? ContentSwapMode.Crossfade : ContentSwapMode.None;
        _current = new TextBlock { Text = text };
        Style(_current);
        _swap.Content = _current;
    }

    private void Style(TextBlock block)
    {
        var lines = Math.Max(1, Lines);
        block.LineHeight = LineHeight;
        block.MaxLines = lines;
        block.TextWrapping = lines > 1 ? TextWrapping.Wrap : TextWrapping.NoWrap;
        block.TextTrimming = TextTrimming.CharacterEllipsis;
        block.TextAlignment = TextAlignment;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LineHeightProperty || change.Property == LinesProperty || change.Property == TextAlignmentProperty)
        {
            _swap.HorizontalContentAlignment = TextAlignment == TextAlignment.Right ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            if (_current is not null)
            {
                Style(_current);
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _swap.Measure(availableSize);
        var height = Math.Max(1, Lines) * LineHeight;
        var width = double.IsFinite(availableSize.Width) ? Math.Min(_swap.DesiredSize.Width, availableSize.Width) : _swap.DesiredSize.Width;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _swap.Arrange(new Rect(finalSize));
        return finalSize;
    }
}

/// <summary>
/// A number of <see cref="ModelCard"/>: Core <see cref="RollingDigits"/> in tabular digits that roll to each new value, or a
/// placeholder ("Not listed", "None") when there is none. The digits keep to <see cref="Alignment"/>, so a value that gains a
/// column grows away from that edge. Reduced motion and Off: values change at once.
/// </summary>
internal sealed class ModelCardNumber : Control
{
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<ModelCardNumber>();

    public static readonly StyledProperty<FontWeight> FontWeightProperty = TextElement.FontWeightProperty.AddOwner<ModelCardNumber>();

    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<ModelCardNumber>();

    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<ModelCardNumber>();

    /// <summary>The placeholder's color; the number's own color when null.</summary>
    public static readonly StyledProperty<IBrush?> PlaceholderBrushProperty =
        AvaloniaProperty.Register<ModelCardNumber, IBrush?>(nameof(PlaceholderBrush));

    /// <summary>The edge the value keeps to.</summary>
    public static readonly StyledProperty<HorizontalAlignment> AlignmentProperty =
        AvaloniaProperty.Register<ModelCardNumber, HorizontalAlignment>(nameof(Alignment), HorizontalAlignment.Left);

    private readonly RollingDigits _digits;
    private readonly TextBlock _placeholder;
    private ModelCardNumberValue? _value;

    public ModelCardNumber()
    {
        _digits = new RollingDigits { IsVisible = false, VerticalAlignment = VerticalAlignment.Center, IsShortestTurn = false };
        _placeholder = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        VisualChildren.Add(_digits);
        VisualChildren.Add(_placeholder);
        LogicalChildren.Add(_digits);
        LogicalChildren.Add(_placeholder);
    }

    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }

    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public IBrush? PlaceholderBrush { get => GetValue(PlaceholderBrushProperty); set => SetValue(PlaceholderBrushProperty, value); }

    public HorizontalAlignment Alignment { get => GetValue(AlignmentProperty); set => SetValue(AlignmentProperty, value); }

    /// <summary>The value it shows, or null while it shows the placeholder.</summary>
    public ModelCardNumberValue? Value => _value;

    /// <summary>The text it reads as (the value's text, or the placeholder).</summary>
    public string Text => _value?.Text ?? _placeholder.Text ?? string.Empty;

    /// <summary>The digits (tests: whether they rolled).</summary>
    internal RollingDigits Digits => _digits;

    /// <summary>Shows <paramref name="value"/> or <paramref name="placeholder"/>; <paramref name="animate"/> rolls the digits.</summary>
    public void Show(ModelCardNumberValue? value, string placeholder, bool animate)
    {
        _placeholder.Text = placeholder;
        if (value is not { } next)
        {
            _value = null;
            _digits.IsVisible = false;
            _placeholder.IsVisible = true;
            AutomationProperties.SetName(this, placeholder);
            InvalidateMeasure();
            return;
        }
        var wasShown = _value is not null;
        _value = next;
        // A value that appears from the placeholder is written at once; only a value that was already there rolls.
        _digits.IsInstant = !(animate && wasShown && IsLoaded && ArcMotion.IsFull(this));
        if (_digits.Decimals != next.Decimals)
        {
            _digits.Decimals = next.Decimals;
        }
        if (_digits.Prefix != next.Prefix)
        {
            _digits.Prefix = next.Prefix;
        }
        if (_digits.Suffix != next.Suffix)
        {
            _digits.Suffix = next.Suffix;
        }
        if (_digits.Value != next.Value)
        {
            _digits.Value = next.Value;
        }
        _digits.IsVisible = true;
        _placeholder.IsVisible = false;
        AutomationProperties.SetName(this, next.Text);
        InvalidateMeasure();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PlaceholderBrushProperty || change.Property == ForegroundProperty)
        {
            _placeholder.Foreground = PlaceholderBrush ?? Foreground;
        }
        else if (change.Property == AlignmentProperty)
        {
            InvalidateArrange();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _digits.Measure(Size.Infinity);
        _placeholder.Measure(availableSize);
        var shown = _value is null ? _placeholder.DesiredSize : _digits.DesiredSize;
        var height = Math.Max(_digits.DesiredSize.Height, _placeholder.DesiredSize.Height);
        var width = double.IsFinite(availableSize.Width) ? Math.Min(shown.Width, availableSize.Width) : shown.Width;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Place(_digits, finalSize);
        Place(_placeholder, finalSize);
        return finalSize;
    }

    private void Place(Control child, Size finalSize)
    {
        var width = child == _placeholder ? Math.Min(child.DesiredSize.Width, finalSize.Width) : child.DesiredSize.Width;
        var x = Alignment switch
        {
            HorizontalAlignment.Right => finalSize.Width - width,
            HorizontalAlignment.Center => (finalSize.Width - width) / 2,
            _ => 0
        };
        child.Arrange(new Rect(x, 0, width, finalSize.Height));
    }
}

/// <summary>
/// A bar of <see cref="ModelCard"/> (benchmark bars, the context gauge, the compare table's mini bars): a rounded track and
/// a fill from the left edge. The fill slides to each new fraction on a critically damped spring (visual duration 0.24s, no
/// bounce) and, while <see cref="IsArmed"/>, rests at zero until <see cref="Reveal"/> grows it once. Drawn in
/// <see cref="Render"/>: changing the fill never lays anything out.
/// <para>
/// Code-driven (<see cref="MotionValue{T}"/>) because each bar keeps its velocity when the model changes mid-slide and the
/// first reveal is staggered per row; a ControlTheme transition cannot express either.
/// </para>
/// </summary>
internal sealed class ModelCardBar : Control
{
    /// <summary>The slide of the fill: critically damped, 0.24s.</summary>
    internal static readonly SpringSpec Slide = SpringSpec.FromVisual(0.24, 0);

    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<ModelCardBar, IBrush?>(nameof(TrackBrush));

    public static readonly StyledProperty<IBrush?> FillBrushProperty =
        AvaloniaProperty.Register<ModelCardBar, IBrush?>(nameof(FillBrush));

    public static readonly StyledProperty<double> BarHeightProperty =
        AvaloniaProperty.Register<ModelCardBar, double>(nameof(BarHeight), 6);

    private MotionValue<double>? _fill;
    private double _target;
    private double _shown;

    static ModelCardBar()
    {
        AffectsRender<ModelCardBar>(TrackBrushProperty, FillBrushProperty);
        AffectsMeasure<ModelCardBar>(BarHeightProperty);
    }

    public ModelCardBar()
    {
        IsHitTestVisible = false;
    }

    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }

    public IBrush? FillBrush { get => GetValue(FillBrushProperty); set => SetValue(FillBrushProperty, value); }

    public double BarHeight { get => GetValue(BarHeightProperty); set => SetValue(BarHeightProperty, value); }

    /// <summary>True until the first reveal: the fill rests at zero.</summary>
    public bool IsArmed { get; private set; } = true;

    /// <summary>The fraction the fill is heading to.</summary>
    public double Target => _target;

    /// <summary>The fraction drawn now.</summary>
    public double Shown => _shown;

    /// <summary>Sets the fraction; <paramref name="animate"/> slides to it, otherwise it jumps (an armed bar only remembers it).</summary>
    public void SetFraction(double fraction, bool animate)
    {
        _target = double.IsFinite(fraction) ? Math.Clamp(fraction, 0, 1) : 0;
        if (IsArmed)
        {
            return;
        }
        MoveTo(_target, animate, TimeSpan.Zero);
    }

    /// <summary>Grows the fill from zero once, after <paramref name="delay"/>; without Full motion it appears at once.</summary>
    public void Reveal(TimeSpan delay, bool animate)
    {
        if (!IsArmed)
        {
            return;
        }
        IsArmed = false;
        MoveTo(_target, animate, delay);
    }

    private void MoveTo(double target, bool animate, TimeSpan delay)
    {
        var fill = Fill;
        if (animate && IsLoaded && ArcMotion.IsFull(this))
        {
            _ = fill.AnimateTo(target, MotionSpec.Spring(Slide, delay));
        }
        else
        {
            fill.Jump(target);
            Draw(target);
        }
    }

    private MotionValue<double> Fill
    {
        get
        {
            if (_fill is null)
            {
                _fill = new MotionValue<double>(this, _shown, MotionVectors.Double);
                _fill.Changed += Draw;
            }
            return _fill;
        }
    }

    private void Draw(double value)
    {
        _shown = value;
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsFinite(availableSize.Width) ? availableSize.Width : 0, BarHeight);

    public override void Render(DrawingContext context)
    {
        var height = Math.Min(BarHeight, Bounds.Height);
        var y = (Bounds.Height - height) / 2;
        var radius = height / 2;
        if (TrackBrush is { } track)
        {
            context.DrawRectangle(track, null, new RoundedRect(new Rect(0, y, Bounds.Width, height), radius));
        }
        var width = Bounds.Width * Math.Clamp(_shown, 0, 1);
        if (FillBrush is { } fill && width > 0.25)
        {
            context.DrawRectangle(fill, null, new RoundedRect(new Rect(0, y, Math.Max(width, Math.Min(height, width)), height), radius));
        }
    }
}

/// <summary>
/// A modality chip of <see cref="ModelCard"/>: a pill with a glyph and a word. A supported modality has a solid outline and
/// secondary text; an unsupported one is dashed and muted, so the difference never relies on color alone.
/// </summary>
internal sealed class ModelCardChip : Panel
{
    private readonly Avalonia.Controls.Shapes.Rectangle _edge;
    private readonly ArcGlyph _glyph;
    private readonly TextBlock _label;
    private readonly List<IDisposable> _colors = [];
    private bool? _supported;

    public ModelCardChip(ModelCardModalities modality, Geometry glyph)
    {
        Modality = modality;
        _edge = new Avalonia.Controls.Shapes.Rectangle { StrokeThickness = 1 };
        _glyph = new ArcGlyph { Data = glyph, VerticalAlignment = VerticalAlignment.Center };
        _glyph.Tok(ArcGlyph.SizeProperty, ModelCardTokenKind.ChipGlyphSize);
        _label = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        _label.Tok(TextBlock.FontSizeProperty, ModelCardTokenKind.ChipFontSize);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(_glyph);
        row.Children.Add(_label);
        var content = new Border { Child = row }.Tok(Border.PaddingProperty, ModelCardTokenKind.ChipPadding);
        Children.Add(_edge);
        Children.Add(content);
        this.Tok(HeightProperty, ModelCardTokenKind.ChipHeight);
        SetSupported(false, string.Empty, string.Empty);
    }

    /// <summary>The modality the chip stands for.</summary>
    public ModelCardModalities Modality { get; }

    /// <summary>Whether the model supports it.</summary>
    public bool IsSupported => _supported == true;

    /// <summary>Whether the outline is dashed.</summary>
    public bool IsDashed => _edge.StrokeDashArray is { Count: > 0 };

    /// <summary>Sets the word, the state and the name screen readers hear ("Image, supported").</summary>
    public void SetSupported(bool supported, string label, string state)
    {
        _label.Text = label;
        AutomationProperties.SetName(this, label.Length > 0 ? label + ", " + state : state);
        if (_supported == supported)
        {
            return;
        }
        _supported = supported;
        foreach (var binding in _colors)
        {
            binding.Dispose();
        }
        _colors.Clear();
        var ink = supported ? ArcDesignTokenKind.TextSecondary : ArcDesignTokenKind.TextMuted;
        _colors.Add(ArcTokens.Bind(_label, TextBlock.ForegroundProperty, ink));
        _colors.Add(ArcTokens.Bind(_glyph, ArcGlyph.ForegroundProperty, supported ? ArcDesignTokenKind.Foreground : ArcDesignTokenKind.TextMuted));
        _colors.Add(ArcTokens.Bind(_edge, Avalonia.Controls.Shapes.Shape.StrokeProperty, supported ? ArcDesignTokenKind.Border : ArcDesignTokenKind.BorderStrong));
        if (supported)
        {
            _colors.Add(ArcTokens.Bind(_edge, Avalonia.Controls.Shapes.Shape.FillProperty, ArcDesignTokenKind.SurfaceMuted));
            _edge.StrokeDashArray = null;
        }
        else
        {
            _edge.Fill = null;
            _edge.StrokeDashArray = [3, 3];
        }
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        _edge.RadiusX = _edge.RadiusY = finalSize.Height / 2;
        return size;
    }
}

/// <summary>
/// The mark square in the header of <see cref="ModelCard"/>: the model's glyph in its color on a 14% tint of it, or the
/// first letter of its name when it has no glyph. Without a color it uses the accent. Decorative (the name is beside it).
/// </summary>
internal sealed class ModelCardMark : Border
{
    public ModelCardMark(ModelCardModel model)
    {
        this.Tok(WidthProperty, ModelCardTokenKind.MarkSize).Tok(HeightProperty, ModelCardTokenKind.MarkSize);
        this.BindCornerRadius(ArcDesignTokenKind.RadiusStep10);
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
        IsHitTestVisible = false;
        Control inner;
        if (model.Mark is { } glyph)
        {
            var mark = new ArcGlyph { Data = glyph, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            mark.Tok(ArcGlyph.SizeProperty, ModelCardTokenKind.MarkGlyphSize);
            if (model.MarkColor is { } color)
            {
                mark.Foreground = new ImmutableSolidColorBrush(color);
            }
            else
            {
                mark.Arc(ArcGlyph.ForegroundProperty, ArcDesignTokenKind.Accent);
            }
            inner = mark;
        }
        else
        {
            var name = model.Name.Trim();
            var letter = new TextBlock
            {
                Text = name.Length > 0 ? char.ToUpperInvariant(name[0]).ToString() : "?",
                FontWeight = FontWeight.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            letter.Tok(TextBlock.FontSizeProperty, ModelCardTokenKind.MonogramFontSize);
            if (model.MarkColor is { } color)
            {
                letter.Foreground = new ImmutableSolidColorBrush(color);
            }
            else
            {
                letter.Arc(TextBlock.ForegroundProperty, ArcDesignTokenKind.Accent);
            }
            inner = letter;
        }
        if (model.MarkColor is { } tint)
        {
            Background = new ImmutableSolidColorBrush(tint, 0.14);
        }
        else
        {
            this.Arc(BackgroundProperty, ArcDesignTokenKind.AccentSubtle);
        }
        Child = inner;
    }
}
