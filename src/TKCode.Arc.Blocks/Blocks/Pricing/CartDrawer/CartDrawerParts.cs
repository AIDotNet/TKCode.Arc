using TKCode.Arc.DesignTokens;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A small button of the cart (close, ×, stepper keys, Apply, the chip's Remove): transparent until hovered (foreground
/// at 6%), a glyph or a text, keyboard-only focus ring. Icon buttons press to 0.92 over 120ms (theme).
/// </summary>
internal sealed class CartDrawerPlainButton : AvaloniaButton
{
    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<CartDrawerPlainButton, Geometry?>(nameof(Glyph));

    public static readonly StyledProperty<double> GlyphSizeProperty =
        AvaloniaProperty.Register<CartDrawerPlainButton, double>(nameof(GlyphSize), 15);

    public Geometry? Glyph
    {
        get => GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public double GlyphSize
    {
        get => GetValue(GlyphSizeProperty);
        set => SetValue(GlyphSizeProperty, value);
    }
}

/// <summary>The pill cart button in the shop header: bag, "Cart" and the count badge (content built by the block).</summary>
internal sealed class CartDrawerCartButton : AvaloniaButton
{
}

/// <summary>
/// The checkout button: 50px, accent, label left and amount right. While <see cref="IsBusy"/> it keeps focus but ignores
/// presses (aria-busy, not disabled); disabled (empty cart) it dims.
/// </summary>
internal sealed class CartDrawerCheckoutButton : AvaloniaButton
{
    public bool IsBusy { get; set; }

    protected override void OnClick()
    {
        if (IsBusy)
        {
            return;
        }
        base.OnClick();
    }
}

/// <summary>Equal columns with gaps: the product grid (3 columns, 2 at 640px and below).</summary>
internal sealed class CartDrawerGridPanel : Panel
{
    public static readonly StyledProperty<int> ColumnsProperty =
        AvaloniaProperty.Register<CartDrawerGridPanel, int>(nameof(Columns), 3);

    public static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<CartDrawerGridPanel, double>(nameof(ColumnGap), 16);

    public static readonly StyledProperty<double> RowGapProperty =
        AvaloniaProperty.Register<CartDrawerGridPanel, double>(nameof(RowGap), 24);

    static CartDrawerGridPanel()
    {
        AffectsMeasure<CartDrawerGridPanel>(ColumnsProperty, ColumnGapProperty, RowGapProperty);
    }

    public int Columns
    {
        get => GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    public double ColumnGap
    {
        get => GetValue(ColumnGapProperty);
        set => SetValue(ColumnGapProperty, value);
    }

    public double RowGap
    {
        get => GetValue(RowGapProperty);
        set => SetValue(RowGapProperty, value);
    }

    private double ColumnWidth(double width)
    {
        var columns = Math.Max(1, Columns);
        return Math.Max(0, (width - ColumnGap * (columns - 1)) / columns);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 960 : availableSize.Width;
        var column = ColumnWidth(width);
        var columns = Math.Max(1, Columns);
        double height = 0;
        double rowHeight = 0;
        var index = 0;
        foreach (var child in Children)
        {
            child.Measure(new Size(column, double.PositiveInfinity));
            rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);
            index++;
            if (index % columns == 0)
            {
                height += rowHeight + RowGap;
                rowHeight = 0;
            }
        }
        if (index % columns != 0)
        {
            height += rowHeight;
        }
        else if (index > 0)
        {
            height -= RowGap;
        }
        return new Size(width, Math.Max(0, height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var column = ColumnWidth(finalSize.Width);
        var columns = Math.Max(1, Columns);
        double y = 0;
        for (var start = 0; start < Children.Count; start += columns)
        {
            double rowHeight = 0;
            for (var i = start; i < Math.Min(start + columns, Children.Count); i++)
            {
                rowHeight = Math.Max(rowHeight, Children[i].DesiredSize.Height);
            }
            for (var i = start; i < Math.Min(start + columns, Children.Count); i++)
            {
                var x = (i - start) * (column + ColumnGap);
                Children[i].Arrange(new Rect(x, y, column, rowHeight));
            }
            y += rowHeight + RowGap;
        }
        return finalSize;
    }
}

/// <summary>Sizes its child to its width × <see cref="Ratio"/> (the product picture's 4:5 frame).</summary>
internal sealed class CartDrawerAspectBox : Decorator
{
    public double Ratio { get; set; } = 1.25;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 260 : availableSize.Width;
        var size = new Size(width, width * Ratio);
        Child?.Measure(size);
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Child?.Arrange(new Rect(finalSize));
        return finalSize;
    }
}

/// <summary>Money text and the rolling-digit shape of an amount in the block's currency and culture.</summary>
internal static class CartDrawerFormat
{
    /// <summary>The minus sign of the discount (U+2212).</summary>
    public const string Minus = "−";

    public static string Money(decimal amount, string? currency, CultureInfo culture, int decimals)
    {
        var shape = UsageBillingMoney.ShapeOf(currency, culture);
        var number = Math.Abs(amount).ToString("N" + Math.Clamp(decimals, 0, 4).ToString(CultureInfo.InvariantCulture), culture);
        var sign = amount < 0 ? Minus : string.Empty;
        return sign + shape.Prefix + number + shape.Suffix;
    }

    /// <summary>Points <paramref name="digits"/> at <paramref name="amount"/> (sign and symbol as a fixed prefix).</summary>
    public static void Apply(RollingDigits digits, decimal amount, string? currency, CultureInfo culture, int decimals, bool negative = false)
    {
        var shape = UsageBillingMoney.ShapeOf(currency, culture);
        digits.CultureName = culture.Name;
        digits.UseGrouping = true;
        digits.Decimals = Math.Clamp(decimals, 0, 4);
        digits.Prefix = (negative ? Minus : string.Empty) + shape.Prefix;
        digits.Suffix = string.IsNullOrEmpty(shape.Suffix) ? null : shape.Suffix;
        digits.Value = (double)Math.Abs(amount);
    }
}

/// <summary>Small builders for the code-built parts of the block.</summary>
internal static class CartDrawerBuild
{
    public static TextBlock Text(string? text, ArcDesignTokenKind size, ArcDesignTokenKind color, bool medium = false, double? lineHeight = null)
    {
        var block = new TextBlock { Text = text, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(block, TextBlock.FontSizeProperty, size);
        ArcTokens.Bind(block, TextBlock.ForegroundProperty, color);
        if (medium)
        {
            ArcTokens.Bind(block, TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        }
        if (lineHeight is { } lh)
        {
            block.LineHeight = lh;
        }
        return block;
    }

    public static ArcGlyph Glyph(Geometry data, double size, ArcDesignTokenKind color, double stroke = 1.75)
    {
        var glyph = new ArcGlyph { Data = data, Size = size, StrokeThickness = stroke, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(glyph, ArcGlyph.ForegroundProperty, color);
        return glyph;
    }

    public static RollingDigits Digits(ArcDesignTokenKind size, ArcDesignTokenKind color, bool medium = false)
    {
        var digits = new RollingDigits
        {
            Falloff = RollingDigitsFalloff.Linear,
            WheelSpring = ArcSpring.Smooth,
            VerticalAlignment = VerticalAlignment.Center
        };
        ArcTokens.Bind(digits, RollingDigits.FontSizeProperty, size);
        ArcTokens.Bind(digits, RollingDigits.ForegroundProperty, color);
        ArcTokens.Bind(digits, RollingDigits.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
        if (medium)
        {
            ArcTokens.Bind(digits, RollingDigits.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        }
        return digits;
    }

    public static CartDrawerPlainButton IconButton(Geometry glyph, double size, double glyphSize, string name, ArcDesignTokenKind color)
    {
        var button = new CartDrawerPlainButton
        {
            Glyph = glyph,
            GlyphSize = glyphSize,
            Width = size,
            Height = size
        };
        button.Classes.Add("icon");
        ArcTokens.Bind(button, Avalonia.Controls.Primitives.TemplatedControl.ForegroundProperty, color);
        Avalonia.Automation.AutomationProperties.SetName(button, name);
        return button;
    }

    /// <summary>Whether the last input that moved focus was the keyboard.</summary>
    public static NavigationMethod Method(bool keyboard) => keyboard ? NavigationMethod.Tab : NavigationMethod.Pointer;
}
