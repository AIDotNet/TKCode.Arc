using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>
/// The code area of a <see cref="CodeMagicMove"/>: draws the changed line bands, the line numbers, every word where the
/// current move puts it, and the typing caret. It is the walkthrough's keyboard stop (the arrow keys step) and the
/// surface a sideways drag scrubs. Its height follows the line count of the move.
/// </summary>
/// <remarks>
/// Drawn rather than templated: up to a hundred words move every frame, each with its own position, opacity and typed
/// length, so they are cached text layouts drawn in one pass (code drawn, #154). The code size fits the widest step to
/// the available width between <see cref="CodeFontSize"/> and <see cref="MinCodeFontSize"/>, so lines never wrap.
/// </remarks>
internal sealed class CodeMagicMoveSurface : Control
{
    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        TextElement.FontFamilyProperty.AddOwner<CodeMagicMoveSurface>();

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<CodeMagicMoveSurface>();

    public static readonly StyledProperty<IBrush?> LineNumberBrushProperty =
        AvaloniaProperty.Register<CodeMagicMoveSurface, IBrush?>(nameof(LineNumberBrush));

    public static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<CodeMagicMoveSurface, IBrush?>(nameof(AccentBrush));

    public static readonly StyledProperty<IBrush?> FocusRingBrushProperty =
        AvaloniaProperty.Register<CodeMagicMoveSurface, IBrush?>(nameof(FocusRingBrush));

    public static readonly StyledProperty<double> BandFillOpacityProperty =
        AvaloniaProperty.Register<CodeMagicMoveSurface, double>(nameof(BandFillOpacity), 0.11);

    public static readonly StyledProperty<Thickness> PaddingProperty =
        AvaloniaProperty.Register<CodeMagicMoveSurface, Thickness>(nameof(Padding), new Thickness(20, 18));

    public static readonly StyledProperty<Thickness> CompactPaddingProperty =
        AvaloniaProperty.Register<CodeMagicMoveSurface, Thickness>(nameof(CompactPadding), new Thickness(16, 18));

    public static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<CodeMagicMoveSurface, double>(nameof(CompactWidth), 520);

    public static readonly StyledProperty<double> CodeFontSizeProperty =
        AvaloniaProperty.Register<CodeMagicMoveSurface, double>(nameof(CodeFontSize), 13);

    public static readonly StyledProperty<double> MinCodeFontSizeProperty =
        AvaloniaProperty.Register<CodeMagicMoveSurface, double>(nameof(MinCodeFontSize), 10.5);

    public static readonly StyledProperty<double> LineSpacingProperty =
        AvaloniaProperty.Register<CodeMagicMoveSurface, double>(nameof(LineSpacing), 1.7);

    public static readonly StyledProperty<double> GutterGapProperty =
        AvaloniaProperty.Register<CodeMagicMoveSurface, double>(nameof(GutterGap), 2.4);

    public static readonly StyledProperty<double> BandRuleWidthProperty =
        AvaloniaProperty.Register<CodeMagicMoveSurface, double>(nameof(BandRuleWidth), 2);

    public static readonly StyledProperty<double> CaretWidthProperty =
        AvaloniaProperty.Register<CodeMagicMoveSurface, double>(nameof(CaretWidth), 2);

    public static readonly StyledProperty<double> CaretHeightProperty =
        AvaloniaProperty.Register<CodeMagicMoveSurface, double>(nameof(CaretHeight), 1.3);

    public static readonly StyledProperty<double> RubberBandLimitProperty =
        AvaloniaProperty.Register<CodeMagicMoveSurface, double>(nameof(RubberBandLimit), 40);

    private readonly Dictionary<CodeMagicMoveSource, TextLayout?[]> _words = [];
    private readonly Dictionary<uint, IImmutableSolidColorBrush> _brushes = [];
    private TextLayout?[] _numbers = [];
    private double _layoutSize;
    private Typeface _typeface;
    private double _charRatio = 0.6;
    private bool _charRatioKnown;
    private double _fontSize = 13;
    private double _lineHeight = 22.1;
    private double _charWidth = 7.8;
    private Thickness _padding;
    private ImmutablePen? _ringPen;
    private IBrush? _ringBrush;
    private ImmutableSolidColorBrush? _bandBrush;
    private Color _bandColor;
    private double _bandOpacity = -1;

    static CodeMagicMoveSurface()
    {
        FocusableProperty.OverrideDefaultValue<CodeMagicMoveSurface>(true);
        ClipToBoundsProperty.OverrideDefaultValue<CodeMagicMoveSurface>(false);
        AffectsMeasure<CodeMagicMoveSurface>(FontFamilyProperty, PaddingProperty, CompactPaddingProperty, CompactWidthProperty,
            CodeFontSizeProperty, MinCodeFontSizeProperty, LineSpacingProperty, GutterGapProperty);
        AffectsRender<CodeMagicMoveSurface>(ForegroundProperty, LineNumberBrushProperty, AccentBrushProperty, FocusRingBrushProperty,
            BandFillOpacityProperty, BandRuleWidthProperty, CaretWidthProperty, CaretHeightProperty);
    }

    public CodeMagicMove? Owner { get; set; }

    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public IBrush? LineNumberBrush { get => GetValue(LineNumberBrushProperty); set => SetValue(LineNumberBrushProperty, value); }

    public IBrush? AccentBrush { get => GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }

    public IBrush? FocusRingBrush { get => GetValue(FocusRingBrushProperty); set => SetValue(FocusRingBrushProperty, value); }

    public double BandFillOpacity { get => GetValue(BandFillOpacityProperty); set => SetValue(BandFillOpacityProperty, value); }

    public Thickness Padding { get => GetValue(PaddingProperty); set => SetValue(PaddingProperty, value); }

    public Thickness CompactPadding { get => GetValue(CompactPaddingProperty); set => SetValue(CompactPaddingProperty, value); }

    public double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }

    public double CodeFontSize { get => GetValue(CodeFontSizeProperty); set => SetValue(CodeFontSizeProperty, value); }

    public double MinCodeFontSize { get => GetValue(MinCodeFontSizeProperty); set => SetValue(MinCodeFontSizeProperty, value); }

    public double LineSpacing { get => GetValue(LineSpacingProperty); set => SetValue(LineSpacingProperty, value); }

    public double GutterGap { get => GetValue(GutterGapProperty); set => SetValue(GutterGapProperty, value); }

    public double BandRuleWidth { get => GetValue(BandRuleWidthProperty); set => SetValue(BandRuleWidthProperty, value); }

    public double CaretWidth { get => GetValue(CaretWidthProperty); set => SetValue(CaretWidthProperty, value); }

    public double CaretHeight { get => GetValue(CaretHeightProperty); set => SetValue(CaretHeightProperty, value); }

    /// <summary>How far the code can be pulled past the first or last step (px).</summary>
    public double RubberBandLimit { get => GetValue(RubberBandLimitProperty); set => SetValue(RubberBandLimitProperty, value); }

    /// <summary>The code size in use (after fitting).</summary>
    public double FittedFontSize => _fontSize;

    /// <summary>The line height in use.</summary>
    public double LineHeight => _lineHeight;

    /// <summary>The width of one character in use.</summary>
    public double CharWidth => _charWidth;

    /// <summary>The padding in use (wide or narrow).</summary>
    public Thickness ActivePadding => _padding;

    /// <summary>Where the code starts horizontally (after the line numbers).</summary>
    public double TextLeft => _padding.Left + (Digits + GutterGap) * _charWidth;

    /// <summary>Horizontal pull of the code past the first or last step (px).</summary>
    public double RubberOffset { get; set; }

    private int Digits => Math.Max(1, (Owner?.MaxLineCount ?? 1).ToString(System.Globalization.CultureInfo.InvariantCulture).Length);

    /// <summary>Drops the cached layouts of <paramref name="source"/> (its colors changed).</summary>
    public void InvalidateWords(CodeMagicMoveSource source)
    {
        if (_words.Remove(source, out var layouts))
        {
            Dispose(layouts);
        }
        InvalidateVisual();
    }

    /// <summary>Drops every cached layout.</summary>
    public void InvalidateAllWords()
    {
        foreach (var layouts in _words.Values)
        {
            Dispose(layouts);
        }
        _words.Clear();
        Dispose(_numbers);
        _numbers = [];
        InvalidateVisual();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FontFamilyProperty)
        {
            _charRatioKnown = false;
            InvalidateAllWords();
        }
        else if (change.Property == ForegroundProperty || change.Property == LineNumberBrushProperty)
        {
            InvalidateAllWords();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        InvalidateAllWords();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var owner = Owner;
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 0;
        _padding = width > 0 && width < CompactWidth ? CompactPadding : Padding;
        EnsureCharRatio();
        var max = CodeFontSize;
        var size = max;
        if (owner is not null && width > 0)
        {
            var columns = owner.MaxColumns + Digits + GutterGap + 1;
            var fit = (width - _padding.Left - _padding.Right) / (columns * _charRatio);
            size = Math.Clamp(fit, Math.Min(MinCodeFontSize, max), max);
        }
        if (Math.Abs(size - _fontSize) > 0.001)
        {
            _fontSize = size;
            InvalidateAllWords();
        }
        _lineHeight = _fontSize * LineSpacing;
        _charWidth = _fontSize * _charRatio;
        var lines = owner?.DisplayedLines ?? 1;
        var height = lines * _lineHeight + _padding.Top + _padding.Bottom;
        return new Size(width, height);
    }

    private void EnsureCharRatio()
    {
        if (_charRatioKnown)
        {
            return;
        }
        _typeface = new Typeface(FontFamily);
        using var probe = new TextLayout("0000000000", _typeface, 100, null);
        var width = probe.WidthIncludingTrailingWhitespace;
        _charRatio = width > 0 ? width / 1000 : 0.6;
        _charRatioKnown = true;
    }

    public override void Render(DrawingContext context)
    {
        var owner = Owner;
        var bounds = new Rect(Bounds.Size);
        // Hit testing covers the whole area (it is dragged anywhere).
        context.FillRectangle(Brushes.Transparent, bounds);
        if (owner is null)
        {
            return;
        }
        using (context.PushClip(bounds))
        {
            DrawBands(context, owner, bounds.Width);
            DrawNumbers(context, owner, bounds.Height);
            using (context.PushTransform(Matrix.CreateTranslation(RubberOffset, 0)))
            {
                DrawWords(context, owner);
                DrawCaret(context, owner);
            }
        }
        DrawFocusRing(context, bounds);
    }

    private void DrawBands(DrawingContext context, CodeMagicMove owner, double width)
    {
        if (AccentBrush is not ISolidColorBrush accent)
        {
            return;
        }
        if (_bandBrush is null || _bandColor != accent.Color || Math.Abs(_bandOpacity - BandFillOpacity) > 1e-6)
        {
            _bandColor = accent.Color;
            _bandOpacity = BandFillOpacity;
            var c = accent.Color;
            _bandBrush = new ImmutableSolidColorBrush(Color.FromArgb((byte)Math.Round(c.A * _bandOpacity), c.R, c.G, c.B));
        }
        var plan = owner.Plan;
        if (plan is not null)
        {
            DrawBandSet(context, plan.OldBands, plan.OldBandAlpha, accent, width);
            DrawBandSet(context, plan.ToBands, plan.BandAlpha, accent, width);
        }
        else
        {
            var bands = owner.RestBands;
            foreach (var band in bands)
            {
                DrawBand(context, band, 1, accent, width);
            }
        }
    }

    private void DrawBandSet(DrawingContext context, CodeMagicMoveBand[] bands, double[] alpha, IBrush accent, double width)
    {
        for (var b = 0; b < bands.Length; b++)
        {
            if (alpha[b] > 0.001)
            {
                DrawBand(context, bands[b], alpha[b], accent, width);
            }
        }
    }

    private void DrawBand(DrawingContext context, CodeMagicMoveBand band, double alpha, IBrush accent, double width)
    {
        var rect = new Rect(0, _padding.Top + band.Line * _lineHeight, width, band.Count * _lineHeight);
        using (context.PushOpacity(alpha))
        {
            context.FillRectangle(_bandBrush!, rect);
            context.FillRectangle(accent, rect.WithWidth(BandRuleWidth));
        }
    }

    private void DrawNumbers(DrawingContext context, CodeMagicMove owner, double height)
    {
        var count = owner.MaxLineCount;
        if (_numbers.Length != count || Math.Abs(_layoutSize - _fontSize) > 0.001)
        {
            Dispose(_numbers);
            _numbers = new TextLayout?[count];
            _layoutSize = _fontSize;
        }
        var visible = height - _padding.Top - _padding.Bottom;
        var right = _padding.Left + Digits * _charWidth;
        for (var i = 0; i < count; i++)
        {
            var top = i * _lineHeight;
            var shown = Math.Clamp((visible - top) / _lineHeight, 0, 1);
            if (shown <= 0.001)
            {
                break;
            }
            var layout = _numbers[i] ??= new TextLayout((i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), _typeface, _fontSize,
                LineNumberBrush ?? Foreground);
            var y = _padding.Top + top + (_lineHeight - layout.Height) / 2;
            using (context.PushOpacity(shown))
            {
                layout.Draw(context, new Point(right - layout.WidthIncludingTrailingWhitespace, y));
            }
        }
    }

    private void DrawWords(DrawingContext context, CodeMagicMove owner)
    {
        var left = TextLeft;
        var top = _padding.Top;
        var plan = owner.Plan;
        if (plan is null)
        {
            var source = owner.RestSource;
            var layouts = LayoutsOf(source);
            for (var i = 0; i < source.Words.Length; i++)
            {
                var word = source.Words[i];
                DrawWord(context, LayoutOf(layouts, source, i), left + word.Column * _charWidth, top + word.Line * _lineHeight, 1, word.Text.Length, word.Text.Length);
            }
            return;
        }
        var toLayouts = LayoutsOf(plan.To);
        var fromLayouts = LayoutsOf(plan.From.Source);
        for (var k = 0; k < plan.Roles.Length; k++)
        {
            var alpha = plan.Alpha[k];
            if (alpha <= 0.001)
            {
                continue;
            }
            var removed = plan.Roles[k] == CodeMagicMoveRole.Removed;
            var layout = removed ? LayoutOf(fromLayouts, plan.From.Source, plan.Words[k]) : LayoutOf(toLayouts, plan.To, plan.Words[k]);
            DrawWord(context, layout, left + plan.X[k] * _charWidth, top + plan.Y[k] * _lineHeight, alpha, plan.Chars[k], plan.Texts[k].Length);
        }
    }

    private void DrawWord(DrawingContext context, TextLayout layout, double x, double y, double alpha, int chars, int length)
    {
        var origin = new Point(x, y + (_lineHeight - layout.Height) / 2);
        if (chars <= 0)
        {
            return;
        }
        using (context.PushOpacity(alpha))
        {
            if (chars >= length)
            {
                layout.Draw(context, origin);
                return;
            }
            // Typed so far: whole characters, with room above and below for ascenders and the caret.
            var overflow = _fontSize * 0.4;
            var clip = new Rect(x - _fontSize * 0.3, y - overflow, _fontSize * 0.3 + chars * _charWidth, _lineHeight + overflow * 2);
            using (context.PushClip(clip))
            {
                layout.Draw(context, origin);
            }
        }
    }

    private void DrawCaret(DrawingContext context, CodeMagicMove owner)
    {
        var plan = owner.Plan;
        if (plan is null || AccentBrush is not { } accent)
        {
            return;
        }
        var (column, line, alpha) = plan.Caret;
        if (alpha <= 0.001)
        {
            return;
        }
        var height = _fontSize * CaretHeight;
        var rect = new Rect(TextLeft + column * _charWidth - CaretWidth / 2, _padding.Top + line * _lineHeight + (_lineHeight - height) / 2,
            CaretWidth, height);
        using (context.PushOpacity(alpha))
        {
            context.DrawRectangle(accent, null, rect, CaretWidth / 2, CaretWidth / 2);
        }
    }

    private void DrawFocusRing(DrawingContext context, Rect bounds)
    {
        if (!IsKeyboardFocusVisible || FocusRingBrush is not { } brush)
        {
            return;
        }
        if (_ringPen is null || !ReferenceEquals(_ringBrush, brush))
        {
            _ringBrush = brush;
            _ringPen = new ImmutablePen(brush.ToImmutable(), 2);
        }
        // Arc's shared ring, 2px outside the code area; the card clips its sides, so it reads as two rules.
        context.DrawRectangle(null, _ringPen, bounds.Inflate(3));
    }

    private bool IsKeyboardFocusVisible => IsFocused && Classes.Contains(":focus-visible");

    private TextLayout?[] LayoutsOf(CodeMagicMoveSource source)
    {
        if (!_words.TryGetValue(source, out var layouts))
        {
            layouts = new TextLayout?[source.Words.Length];
            _words[source] = layouts;
        }
        return layouts;
    }

    private TextLayout LayoutOf(TextLayout?[] layouts, CodeMagicMoveSource source, int index)
    {
        if (layouts[index] is { } layout)
        {
            return layout;
        }
        var colors = source.Colors;
        IBrush? brush = Foreground;
        if (colors is not null && index < colors.Length && colors[index] != 0)
        {
            var argb = colors[index];
            if (!_brushes.TryGetValue(argb, out var cached))
            {
                cached = new ImmutableSolidColorBrush(Color.FromUInt32(argb));
                _brushes[argb] = cached;
            }
            brush = cached;
        }
        layout = new TextLayout(source.Words[index].Text, _typeface, _fontSize, brush);
        layouts[index] = layout;
        return layout;
    }

    private static void Dispose(TextLayout?[] layouts)
    {
        for (var i = 0; i < layouts.Length; i++)
        {
            layouts[i]?.Dispose();
            layouts[i] = null;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        Owner?.HandleSurfaceKey(e);
        base.OnKeyDown(e);
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

    protected override AutomationPeer OnCreateAutomationPeer() => new SurfaceAutomationPeer(this);

    private sealed class SurfaceAutomationPeer(CodeMagicMoveSurface owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string? GetNameCore() => owner.Owner?.ViewportName ?? base.GetNameCore();

        protected override string? GetHelpTextCore() => owner.Owner?.KeyboardHint ?? base.GetHelpTextCore();

        protected override bool IsContentElementCore() => true;
    }
}
