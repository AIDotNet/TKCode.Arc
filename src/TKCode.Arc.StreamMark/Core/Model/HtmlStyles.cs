namespace StreamMark.Core.Model;

/// <summary>CSS 颜色（sRGB，带不透明度）。</summary>
public readonly record struct CssColor(byte A, byte R, byte G, byte B)
{
    public override string ToString() => $"#{A:x2}{R:x2}{G:x2}{B:x2}";
}

/// <summary>文字水平对齐。<see cref="Inherit"/> 表示沿用外层。</summary>
public enum TextAlign : byte
{
    Inherit,
    Left,
    Center,
    Right,
    Justify,
}

/// <summary>四边距离（像素）。</summary>
public readonly record struct Edges(double Top, double Right, double Bottom, double Left)
{
    public static readonly Edges Zero = default;

    public bool IsZero => Top == 0 && Right == 0 && Bottom == 0 && Left == 0;
}

/// <summary>HTML 盒子的非继承样式：外边距、内边距、背景、边框、宽度。</summary>
public sealed record HtmlBoxStyle
{
    public static readonly HtmlBoxStyle Default = new();

    public Edges Margin { get; init; }

    public Edges Padding { get; init; }

    public CssColor? Background { get; init; }

    public CssColor? BorderColor { get; init; }

    public double BorderWidth { get; init; }

    public double BorderRadius { get; init; }

    /// <summary>宽度（像素），与 <see cref="WidthPercent"/> 互斥。</summary>
    public double? Width { get; init; }

    public double? WidthPercent { get; init; }

    public double? MaxWidth { get; init; }

    public bool IsDefault => Equals(Default);
}

/// <summary>
/// 可继承的文字样式（CSS 中的 color、font-size、font-weight、font-style、text-align 等）。
/// 用于 HTML 盒子（作用于其中所有块）和行内区间。null 表示沿用外层。
/// </summary>
public sealed record HtmlTextStyle
{
    public static readonly HtmlTextStyle Default = new();

    public TextAlign Align { get; init; }

    public CssColor? Foreground { get; init; }

    /// <summary>行内背景色（只用于行内区间）。</summary>
    public CssColor? Background { get; init; }

    /// <summary>绝对字号（像素）。</summary>
    public double? FontSize { get; init; }

    /// <summary>相对外层字号的缩放（em、%、smaller / larger 等）。</summary>
    public double? FontScale { get; init; }

    public bool? Bold { get; init; }

    public bool? Italic { get; init; }

    public bool IsDefault => Equals(Default);

    /// <summary>在 <paramref name="outer"/> 之上叠加本样式（本样式中非空的项优先）。</summary>
    public HtmlTextStyle Over(HtmlTextStyle outer) => new()
    {
        Align = Align != TextAlign.Inherit ? Align : outer.Align,
        Foreground = Foreground ?? outer.Foreground,
        Background = Background ?? outer.Background,
        FontSize = FontSize ?? (FontScale is { } scale && outer.FontSize is { } size ? size * scale : outer.FontSize),
        FontScale = FontSize is not null ? null : FontScale is { } s ? s * (outer.FontScale ?? 1) : outer.FontScale,
        Bold = Bold ?? outer.Bold,
        Italic = Italic ?? outer.Italic,
    };
}
