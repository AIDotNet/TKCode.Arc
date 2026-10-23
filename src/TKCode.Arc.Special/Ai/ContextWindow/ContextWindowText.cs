using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>What a <see cref="ContextWindowNumber"/> shows.</summary>
internal enum ContextWindowNumberMode
{
    /// <summary>A token count ("4.1K").</summary>
    Tokens,

    /// <summary>A legend percentage of <see cref="ContextWindowNumber.Total"/> ("0.4%", "29%").</summary>
    Percent
}

/// <summary>Shared text helpers of the context window's drawn numbers.</summary>
internal static class ContextWindowTextLayouts
{
    /// <summary>Tabular figures, so a rolling number keeps its width.</summary>
    public static readonly FontFeatureCollection Tabular = new() { FontFeature.Parse("tnum") };

    public static TextLayout Create(string text, Typeface typeface, double size, IBrush? brush, double lineHeight, double letterSpacing,
        bool tabular) =>
        new(text, typeface, size, brush ?? Brushes.Black, TextAlignment.Left, TextWrapping.NoWrap, TextTrimming.None, null,
            FlowDirection.LeftToRight, double.PositiveInfinity, double.PositiveInfinity, lineHeight, letterSpacing, 1,
            tabular ? Tabular : null);
}

/// <summary>
/// A legend value of a <see cref="ContextWindow"/>: a token count or a percentage, drawn right-aligned in tabular figures.
/// The owner sets <see cref="Value"/> on every frame of a roll; the text (and its layout) is rebuilt only when the shown
/// text changes, and the control measures again only when its width does. A <see cref="Placeholder"/> replaces the number
/// ("No summary yet", or nothing for an empty row's percentage).
/// </summary>
internal sealed class ContextWindowNumber : Control
{
    public static readonly StyledProperty<ContextWindowNumberMode> ModeProperty =
        AvaloniaProperty.Register<ContextWindowNumber, ContextWindowNumberMode>(nameof(Mode));

    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<ContextWindowNumber, string?>(nameof(Placeholder));

    public static readonly StyledProperty<double> LineHeightProperty =
        AvaloniaProperty.Register<ContextWindowNumber, double>(nameof(LineHeight), 19.6);

    public static readonly StyledProperty<double> LetterSpacingProperty =
        AvaloniaProperty.Register<ContextWindowNumber, double>(nameof(LetterSpacing));

    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<ContextWindowNumber>();
    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<ContextWindowNumber>();
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<ContextWindowNumber>();
    public static readonly StyledProperty<FontWeight> FontWeightProperty = TextElement.FontWeightProperty.AddOwner<ContextWindowNumber>();

    private double _value;
    private double _total;
    private long _key = long.MinValue;
    private string _text = string.Empty;
    private TextLayout? _layout;

    static ContextWindowNumber()
    {
        AffectsRender<ContextWindowNumber>(ForegroundProperty);
    }

    public ContextWindowNumber()
    {
        IsHitTestVisible = false;
    }

    public ContextWindowNumberMode Mode { get => GetValue(ModeProperty); set => SetValue(ModeProperty, value); }

    public string? Placeholder { get => GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }

    public double LineHeight { get => GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }

    public double LetterSpacing { get => GetValue(LetterSpacingProperty); set => SetValue(LetterSpacingProperty, value); }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }

    /// <summary>The text on screen.</summary>
    public string Text => _text;

    /// <summary>The value shown (tokens).</summary>
    public double Value => _value;

    /// <summary>The whole a percentage is taken of (the window).</summary>
    public double Total => _total;

    /// <summary>Shows <paramref name="value"/> (of <paramref name="total"/> for a percentage).</summary>
    public void Show(double value, double total)
    {
        _value = value;
        _total = total;
        Refresh();
    }

    private long KeyOf() => Placeholder is not null
        ? long.MaxValue
        : Mode == ContextWindowNumberMode.Percent
            ? ContextWindowFormat.LegendPercentKey(_value, _total)
            : ContextWindowFormat.TokensKey(_value);

    private void Refresh()
    {
        var key = KeyOf();
        if (key == _key && _layout is not null)
        {
            return;
        }
        _key = key;
        _text = Placeholder ?? (Mode == ContextWindowNumberMode.Percent
            ? ContextWindowFormat.LegendPercent(_value, _total)
            : ContextWindowFormat.Tokens(_value));
        var oldWidth = _layout?.WidthIncludingTrailingWhitespace ?? -1;
        RebuildLayout();
        if (Math.Abs((_layout?.WidthIncludingTrailingWhitespace ?? 0) - oldWidth) > 0.01)
        {
            InvalidateMeasure();
        }
        InvalidateVisual();
    }

    private void RebuildLayout()
    {
        _layout?.Dispose();
        _layout = ContextWindowTextLayouts.Create(_text, new Typeface(FontFamily, FontStyle.Normal, FontWeight), FontSize, Foreground,
            LineHeight, LetterSpacing, tabular: true);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var p = change.Property;
        if (p == PlaceholderProperty || p == ModeProperty)
        {
            _key = long.MinValue;
            Refresh();
        }
        else if (p == ForegroundProperty || p == FontFamilyProperty || p == FontSizeProperty || p == FontWeightProperty ||
                 p == LineHeightProperty || p == LetterSpacingProperty)
        {
            if (_layout is not null)
            {
                RebuildLayout();
                InvalidateMeasure();
                InvalidateVisual();
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_layout is null)
        {
            Refresh();
        }
        return new Size(Math.Ceiling(_layout?.WidthIncludingTrailingWhitespace ?? 0), LineHeight);
    }

    public override void Render(DrawingContext context)
    {
        if (_layout is null || _text.Length == 0)
        {
            return;
        }
        var x = Bounds.Width - _layout.WidthIncludingTrailingWhitespace;
        var y = (Bounds.Height - _layout.Height) / 2;
        _layout.Draw(context, new Point(x, y));
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new NoneAutomationPeer(this);
}

/// <summary>
/// The readout at the top of a <see cref="ContextWindow"/>: the tokens used as a large display number (36px, tabular,
/// rolling), "of 1M tokens" on its baseline (moving under it when there is no room), and under them the percentage used,
/// "used", a dot and the free tokens. Drawn as one control so the number can roll on every frame without layout passes:
/// texts are rebuilt only when what they show changes.
/// </summary>
internal sealed class ContextWindowReadout : Control
{
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<ContextWindowReadout>();
    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<ContextWindowReadout>();

    public static readonly StyledProperty<FontFamily?> DisplayFontFamilyProperty =
        AvaloniaProperty.Register<ContextWindowReadout, FontFamily?>(nameof(DisplayFontFamily));

    public static readonly StyledProperty<IBrush?> SecondaryInkProperty =
        AvaloniaProperty.Register<ContextWindowReadout, IBrush?>(nameof(SecondaryInk));

    public static readonly StyledProperty<IBrush?> MutedInkProperty =
        AvaloniaProperty.Register<ContextWindowReadout, IBrush?>(nameof(MutedInk));

    public static readonly StyledProperty<double> NumberSizeProperty =
        AvaloniaProperty.Register<ContextWindowReadout, double>(nameof(NumberSize), 36);

    public static readonly StyledProperty<double> NumberLineHeightProperty =
        AvaloniaProperty.Register<ContextWindowReadout, double>(nameof(NumberLineHeight), 39.6);

    public static readonly StyledProperty<double> NumberLetterSpacingProperty =
        AvaloniaProperty.Register<ContextWindowReadout, double>(nameof(NumberLetterSpacing), -1.08);

    public static readonly StyledProperty<double> TextSizeProperty =
        AvaloniaProperty.Register<ContextWindowReadout, double>(nameof(TextSize), 14);

    public static readonly StyledProperty<double> TextLineHeightProperty =
        AvaloniaProperty.Register<ContextWindowReadout, double>(nameof(TextLineHeight), 19.6);

    public static readonly StyledProperty<double> TextLetterSpacingProperty =
        AvaloniaProperty.Register<ContextWindowReadout, double>(nameof(TextLetterSpacing), -0.14);

    public static readonly StyledProperty<double> LineGapProperty =
        AvaloniaProperty.Register<ContextWindowReadout, double>(nameof(LineGap), 4);

    private const double NumberGap = 8;
    private const double PartGap = 6;
    private const double DotSize = 3;

    private double _used;
    private double _window;
    private long _usedKey = long.MinValue;
    private long _percentKey = long.MinValue;
    private string _ofText = string.Empty;
    private string _usedWord = string.Empty;
    private string _freeText = string.Empty;
    private TextLayout? _number;
    private TextLayout? _of;
    private TextLayout? _percent;
    private TextLayout? _usedLabel;
    private TextLayout? _free;
    private bool _texts = true;
    private bool _wrapOf;
    private IImmutableBrush? _dotBrush;

    public ContextWindowReadout()
    {
        IsHitTestVisible = false;
    }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    public FontFamily? DisplayFontFamily { get => GetValue(DisplayFontFamilyProperty); set => SetValue(DisplayFontFamilyProperty, value); }

    public IBrush? SecondaryInk { get => GetValue(SecondaryInkProperty); set => SetValue(SecondaryInkProperty, value); }

    public IBrush? MutedInk { get => GetValue(MutedInkProperty); set => SetValue(MutedInkProperty, value); }

    public double NumberSize { get => GetValue(NumberSizeProperty); set => SetValue(NumberSizeProperty, value); }

    public double NumberLineHeight { get => GetValue(NumberLineHeightProperty); set => SetValue(NumberLineHeightProperty, value); }

    public double NumberLetterSpacing { get => GetValue(NumberLetterSpacingProperty); set => SetValue(NumberLetterSpacingProperty, value); }

    public double TextSize { get => GetValue(TextSizeProperty); set => SetValue(TextSizeProperty, value); }

    public double TextLineHeight { get => GetValue(TextLineHeightProperty); set => SetValue(TextLineHeightProperty, value); }

    public double TextLetterSpacing { get => GetValue(TextLetterSpacingProperty); set => SetValue(TextLetterSpacingProperty, value); }

    public double LineGap { get => GetValue(LineGapProperty); set => SetValue(LineGapProperty, value); }

    /// <summary>The large number as shown ("427K").</summary>
    public string NumberText { get; private set; } = string.Empty;

    /// <summary>The percentage as shown ("43%").</summary>
    public string PercentText { get; private set; } = string.Empty;

    public string OfText => _ofText;

    public string FreeText => _freeText;

    /// <summary>Sets the words around the numbers (they change at once, never roll).</summary>
    public void SetTexts(string ofText, string usedWord, string freeText)
    {
        if (ofText == _ofText && usedWord == _usedWord && freeText == _freeText)
        {
            return;
        }
        _ofText = ofText;
        _usedWord = usedWord;
        _freeText = freeText;
        _texts = true;
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>Shows <paramref name="used"/> tokens of a <paramref name="window"/> (called on every frame of a roll).</summary>
    public void Show(double used, double window)
    {
        _used = used;
        _window = window;
        var usedKey = ContextWindowFormat.TokensKey(used);
        var percentKey = ContextWindowFormat.HeaderPercent(used, window);
        if (usedKey == _usedKey && percentKey == _percentKey && _number is not null)
        {
            return;
        }
        var oldWidth = Width0();
        if (usedKey != _usedKey || _number is null)
        {
            _usedKey = usedKey;
            NumberText = ContextWindowFormat.Tokens(used);
            _number?.Dispose();
            _number = null;
        }
        if (percentKey != _percentKey || _percent is null)
        {
            _percentKey = percentKey;
            PercentText = ContextWindowFormat.HeaderPercentText(used, window);
            _percent?.Dispose();
            _percent = null;
        }
        EnsureLayouts();
        if (Math.Abs(Width0() - oldWidth) > 0.01)
        {
            InvalidateMeasure();
        }
        InvalidateVisual();
    }

    private double Width0() => (_number?.WidthIncludingTrailingWhitespace ?? 0) + (_percent?.WidthIncludingTrailingWhitespace ?? 0) * 1000;

    private Typeface BodyFace => new(FontFamily, FontStyle.Normal, FontWeight.Normal);

    private void EnsureLayouts()
    {
        if (_texts)
        {
            _texts = false;
            _of?.Dispose();
            _usedLabel?.Dispose();
            _free?.Dispose();
            _of = ContextWindowTextLayouts.Create(_ofText, BodyFace, TextSize, SecondaryInk, TextLineHeight, TextLetterSpacing, false);
            _usedLabel = ContextWindowTextLayouts.Create(_usedWord, BodyFace, TextSize, SecondaryInk, TextLineHeight, TextLetterSpacing, false);
            _free = ContextWindowTextLayouts.Create(_freeText, BodyFace, TextSize, SecondaryInk, TextLineHeight, TextLetterSpacing, true);
        }
        _number ??= ContextWindowTextLayouts.Create(NumberText, new Typeface(DisplayFontFamily ?? FontFamily, FontStyle.Normal, FontWeight.Normal),
            NumberSize, Foreground, NumberLineHeight, NumberLetterSpacing, true);
        _percent ??= ContextWindowTextLayouts.Create(PercentText, BodyFace, TextSize, SecondaryInk, TextLineHeight, TextLetterSpacing, true);
    }

    private void DropLayouts()
    {
        _number?.Dispose();
        _percent?.Dispose();
        _number = null;
        _percent = null;
        _texts = true;
        _dotBrush = null;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property.OwnerType == typeof(ContextWindowReadout) || change.Property == ForegroundProperty ||
            change.Property == FontFamilyProperty)
        {
            DropLayouts();
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    private double SecondLineWidth() =>
        _percent!.WidthIncludingTrailingWhitespace + PartGap + _usedLabel!.WidthIncludingTrailingWhitespace + PartGap + DotSize + PartGap +
        _free!.WidthIncludingTrailingWhitespace;

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_number is null && NumberText.Length == 0)
        {
            Show(_used, _window);
        }
        EnsureLayouts();
        var firstWidth = _number!.WidthIncludingTrailingWhitespace + NumberGap + _of!.WidthIncludingTrailingWhitespace;
        _wrapOf = double.IsFinite(availableSize.Width) && firstWidth > availableSize.Width + 0.5;
        var width = _wrapOf ? Math.Max(_number.WidthIncludingTrailingWhitespace, _of.WidthIncludingTrailingWhitespace) : firstWidth;
        width = Math.Max(width, SecondLineWidth());
        var height = NumberLineHeight + (_wrapOf ? LineGap + TextLineHeight : 0) + LineGap + TextLineHeight;
        return new Size(Math.Ceiling(width), height);
    }

    public override void Render(DrawingContext context)
    {
        EnsureLayouts();
        var number = _number!;
        number.Draw(context, new Point(0, 0));
        double y;
        if (_wrapOf)
        {
            _of!.Draw(context, new Point(0, NumberLineHeight + LineGap));
            y = NumberLineHeight + LineGap + TextLineHeight + LineGap;
        }
        else
        {
            // "of 1M tokens" sits on the large number's baseline.
            var ofY = number.Baseline - _of!.Baseline;
            _of.Draw(context, new Point(number.WidthIncludingTrailingWhitespace + NumberGap, ofY));
            y = NumberLineHeight + LineGap;
        }
        var x = 0.0;
        _percent!.Draw(context, new Point(x, y));
        x += _percent.WidthIncludingTrailingWhitespace + PartGap;
        _usedLabel!.Draw(context, new Point(x, y));
        x += _usedLabel.WidthIncludingTrailingWhitespace + PartGap;
        _dotBrush ??= (MutedInk ?? Brushes.Gray).ToImmutable();
        context.DrawEllipse(_dotBrush, null, new Rect(x, y + (TextLineHeight - DotSize) / 2, DotSize, DotSize));
        x += DotSize + PartGap;
        _free!.Draw(context, new Point(x, y));
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ReadoutPeer(this);

    private sealed class ReadoutPeer(ContextWindowReadout owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override bool IsContentElementCore() => true;

        protected override string? GetNameCore() => Avalonia.Automation.AutomationProperties.GetName(Owner);
    }
}
