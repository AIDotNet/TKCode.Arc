using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// CodeBlock header file label: icon, file name, then "· language". The file name shrinks with an ellipsis when
/// space runs out while the language keeps its width (Arc's flex row with a <c>min-width: 0</c> name). A new name
/// swaps in with a soft blur rise and the name's slot follows the new width on the morph spring, so the language
/// glides instead of jumping (Arc's <c>FileName</c>). The width spring is code-driven: it starts from the slot's
/// current width and targets a width measured during layout, then returns to "auto" (no fixed property to transition).
/// </summary>
internal sealed class CodeBlockHeaderLabel : Control
{
    public static readonly StyledProperty<string> FileNameProperty =
        AvaloniaProperty.Register<CodeBlockHeaderLabel, string>(nameof(FileName), string.Empty);

    public static readonly StyledProperty<string> LanguageProperty =
        AvaloniaProperty.Register<CodeBlockHeaderLabel, string>(nameof(Language), string.Empty);

    private const double Gap = 8;

    private readonly ArcGlyph _icon;
    private readonly ArcContentSwap _name;
    private readonly TextBlock _dot;
    private readonly TextBlock _language;
    private MotionValue<double>? _slot;
    private double _naturalName;
    private double _lastNatural = double.NaN;

    static CodeBlockHeaderLabel()
    {
        AffectsMeasure<CodeBlockHeaderLabel>(FileNameProperty, LanguageProperty);
    }

    public CodeBlockHeaderLabel()
    {
        _icon = new ArcGlyph { Data = ArcIcons.FileCode2, Size = 16, StrokeThickness = 1.75, VerticalAlignment = VerticalAlignment.Center };
        _name = new ArcContentSwap
        {
            Mode = ContentSwapMode.BlurSwap,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            ContentTemplate = new FuncDataTemplate<string>((text, _) => new TextBlock
            {
                Text = text,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap
            })
        };
        _dot = new TextBlock { Text = "·" };
        _language = new TextBlock();
        foreach (var child in new Control[] { _icon, _name, _dot, _language })
        {
            LogicalChildren.Add(child);
            VisualChildren.Add(child);
        }
        ArcTokens.Bind(_icon, ArcGlyph.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        ArcTokens.Bind(_name, TextElement.ForegroundProperty, ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(_name, TextElement.FontSizeProperty, ArcDesignTokenKind.TypeSM);
        ArcTokens.Bind(_name, TextElement.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        ArcTokens.Bind(_name, TextElement.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
        ArcTokens.Bind(_dot, TextBlock.ForegroundProperty, ArcDesignTokenKind.BorderStrong);
        ArcTokens.Bind(_dot, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeSM);
        ArcTokens.Bind(_language, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        ArcTokens.Bind(_language, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeXS);
        ArcTokens.Bind(_language, TextBlock.FontFamilyProperty, ArcDesignTokenKind.MonoFontFamily);
    }

    public string FileName
    {
        get => GetValue(FileNameProperty);
        set => SetValue(FileNameProperty, value);
    }

    public string Language
    {
        get => GetValue(LanguageProperty);
        set => SetValue(LanguageProperty, value);
    }

    /// <summary>The swap presenter of the file name (tests).</summary>
    internal ArcContentSwap NamePresenter => _name;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FileNameProperty)
        {
            _name.Content = FileName;
        }
        else if (change.Property == LanguageProperty)
        {
            _language.Text = Language;
            _dot.IsVisible = !string.IsNullOrEmpty(Language);
            _language.IsVisible = _dot.IsVisible;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var infinite = new Size(double.PositiveInfinity, availableSize.Height);
        _icon.Measure(infinite);
        _dot.Measure(infinite);
        _language.Measure(infinite);
        _name.Measure(infinite);
        _naturalName = _name.DesiredSize.Width;
        var trailing = _language.IsVisible ? Gap + _dot.DesiredSize.Width + Gap + _language.DesiredSize.Width : 0;
        var fixedWidth = _icon.DesiredSize.Width + Gap + trailing;
        var room = Math.Max(0, availableSize.Width - fixedWidth);
        var slot = SlotWidth();
        var nameWidth = Math.Min(slot, room);
        _name.Measure(new Size(nameWidth, availableSize.Height));
        var height = Math.Max(_icon.DesiredSize.Height, Math.Max(_name.DesiredSize.Height, _language.DesiredSize.Height));
        return new Size(fixedWidth + nameWidth, height);
    }

    private double SlotWidth()
    {
        var natural = _naturalName;
        if (double.IsNaN(_lastNatural))
        {
            _lastNatural = natural;
            return natural;
        }
        if (Math.Abs(natural - _lastNatural) >= 1)
        {
            var from = _slot?.IsAnimating == true ? _slot.Value : _lastNatural;
            _lastNatural = natural;
            if (IsLoaded && ArcMotion.IsFull(this) && from > 0 && natural > 0)
            {
                _slot ??= CreateSlot(from);
                _slot.Jump(from);
                _ = _slot.AnimateTo(natural, MotionSpec.Spring(ArcSpring.Morph));
            }
            else
            {
                _slot?.Jump(natural);
            }
        }
        return _slot?.IsAnimating == true ? _slot.Value : natural;
    }

    private MotionValue<double> CreateSlot(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidateMeasure();
        return value;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var x = 0.0;
        var height = finalSize.Height;
        _icon.Arrange(new Rect(x, (height - _icon.DesiredSize.Height) / 2, _icon.DesiredSize.Width, _icon.DesiredSize.Height));
        x += _icon.DesiredSize.Width + Gap;
        var nameWidth = _name.DesiredSize.Width;
        var nameHeight = _name.DesiredSize.Height;
        var nameTop = (height - nameHeight) / 2;
        _name.Arrange(new Rect(x, nameTop, nameWidth, nameHeight));
        x += nameWidth;
        if (_language.IsVisible)
        {
            // Baseline alignment: the smaller mono label sits on the file name's baseline.
            var baseline = nameTop + nameHeight * 0.78;
            x += Gap;
            _dot.Arrange(new Rect(x, baseline - _dot.DesiredSize.Height * 0.78, _dot.DesiredSize.Width, _dot.DesiredSize.Height));
            x += _dot.DesiredSize.Width + Gap;
            var languageHeight = _language.DesiredSize.Height;
            _language.Arrange(new Rect(x, baseline - languageHeight * 0.78, _language.DesiredSize.Width, languageHeight));
        }
        return finalSize;
    }
}
