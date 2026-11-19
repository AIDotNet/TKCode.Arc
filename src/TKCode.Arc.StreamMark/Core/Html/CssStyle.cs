using System.Globalization;
using StreamMark.Core.Model;

namespace StreamMark.Core.Html;

/// <summary>
/// 内联 <c>style</c> 与旧式表现属性（align、bgcolor、width、&lt;font color size&gt;）的子集解析。
/// 不支持的属性直接忽略；数值限制在合理范围内，防止撑坏版面。
/// </summary>
public static class CssStyle
{
    public const double MaxSpacing = 200;
    public const double MinFontSize = 8;
    public const double MaxFontSize = 64;

    // em / rem 换算成像素时的参考字号
    private const double ReferenceFontSize = 16;

    /// <summary>从元素的属性得到盒子样式与文字样式。</summary>
    public static (HtmlBoxStyle Box, HtmlTextStyle Text) FromAttributes(string tag, ComponentAttributes attributes)
    {
        var box = HtmlBoxStyle.Default;
        var text = HtmlTextStyle.Default;
        // 表现属性（优先级低于 style）
        if (ParseAlign(attributes["align"]) is { } align and not TextAlign.Inherit && !IsImage(tag))
        {
            text = text with { Align = align };
        }
        if (string.Equals(tag, "center", StringComparison.OrdinalIgnoreCase))
        {
            text = text with { Align = TextAlign.Center };
        }
        if (TryParseColor(attributes["bgcolor"], out var bg))
        {
            box = box with { Background = bg };
        }
        if (!IsImage(tag) && ParseWidth(attributes["width"]) is var (px, percent) && (px ?? percent) is not null)
        {
            box = box with { Width = px, WidthPercent = percent };
        }
        if (string.Equals(tag, "font", StringComparison.OrdinalIgnoreCase))
        {
            if (TryParseColor(attributes["color"], out var color))
            {
                text = text with { Foreground = color };
            }
            if (attributes["size"] is { } size && FontTagScale(size) is { } scale)
            {
                text = text with { FontScale = scale };
            }
        }
        if (attributes["style"] is { Length: > 0 } style)
        {
            Apply(style, ref box, ref text, IsImage(tag));
        }
        return (box, text);
    }

    private static bool IsImage(string tag) => string.Equals(tag, "img", StringComparison.OrdinalIgnoreCase);

    /// <summary>解析 <c>style</c> 声明并叠加到给定样式上。</summary>
    public static void Apply(string css, ref HtmlBoxStyle box, ref HtmlTextStyle text, bool isImage = false)
    {
        foreach (var declaration in css.Split(';'))
        {
            var colon = declaration.IndexOf(':');
            if (colon <= 0)
            {
                continue;
            }
            var name = declaration[..colon].Trim().ToLowerInvariant();
            var value = declaration[(colon + 1)..].Replace("!important", "", StringComparison.OrdinalIgnoreCase).Trim();
            if (value.Length == 0)
            {
                continue;
            }
            switch (name)
            {
                case "text-align":
                    if (ParseAlign(value) is { } align and not TextAlign.Inherit)
                    {
                        text = text with { Align = align };
                    }
                    break;
                case "color":
                    if (TryParseColor(value, out var color))
                    {
                        text = text with { Foreground = color };
                    }
                    break;
                case "background":
                case "background-color":
                    foreach (var part in SplitValues(value))
                    {
                        if (TryParseColor(part, out var background))
                        {
                            box = box with { Background = background };
                            text = text with { Background = background };
                            break;
                        }
                    }
                    break;
                case "font-size":
                    ApplyFontSize(value, ref text);
                    break;
                case "font-weight":
                    text = text with { Bold = value is "bold" or "bolder" || int.TryParse(value, out var weight) && weight >= 600 };
                    break;
                case "font-style":
                    text = text with { Italic = value is "italic" or "oblique" };
                    break;
                case "margin":
                    if (ParseEdges(value) is { } margin)
                    {
                        box = box with { Margin = margin };
                    }
                    break;
                case "margin-top":
                    box = box with { Margin = box.Margin with { Top = Spacing(value) } };
                    break;
                case "margin-right":
                    box = box with { Margin = box.Margin with { Right = Spacing(value) } };
                    break;
                case "margin-bottom":
                    box = box with { Margin = box.Margin with { Bottom = Spacing(value) } };
                    break;
                case "margin-left":
                    box = box with { Margin = box.Margin with { Left = Spacing(value) } };
                    break;
                case "padding":
                    if (ParseEdges(value) is { } padding)
                    {
                        box = box with { Padding = padding };
                    }
                    break;
                case "padding-top":
                    box = box with { Padding = box.Padding with { Top = Spacing(value) } };
                    break;
                case "padding-right":
                    box = box with { Padding = box.Padding with { Right = Spacing(value) } };
                    break;
                case "padding-bottom":
                    box = box with { Padding = box.Padding with { Bottom = Spacing(value) } };
                    break;
                case "padding-left":
                    box = box with { Padding = box.Padding with { Left = Spacing(value) } };
                    break;
                case "border":
                    foreach (var part in SplitValues(value))
                    {
                        if (TryParseColor(part, out var borderColor))
                        {
                            box = box with { BorderColor = borderColor };
                        }
                        else if (TryParseLength(part, out var borderWidth, out _))
                        {
                            box = box with { BorderWidth = Math.Clamp(borderWidth, 0, 8) };
                        }
                        else if (part == "none")
                        {
                            box = box with { BorderWidth = 0 };
                        }
                    }
                    if (box.BorderColor is not null && box.BorderWidth == 0 && !value.Contains("none", StringComparison.Ordinal))
                    {
                        box = box with { BorderWidth = 1 };
                    }
                    break;
                case "border-color":
                    if (TryParseColor(value, out var bc))
                    {
                        box = box with { BorderColor = bc, BorderWidth = box.BorderWidth > 0 ? box.BorderWidth : 1 };
                    }
                    break;
                case "border-width":
                    if (TryParseLength(value, out var bw, out _))
                    {
                        box = box with { BorderWidth = Math.Clamp(bw, 0, 8) };
                    }
                    break;
                case "border-radius":
                    if (TryParseLength(SplitValues(value).FirstOrDefault() ?? "", out var radius, out _))
                    {
                        box = box with { BorderRadius = Math.Clamp(radius, 0, 64) };
                    }
                    break;
                case "width" when !isImage:
                    {
                        var (px, percent) = ParseWidth(value);
                        if ((px ?? percent) is not null)
                        {
                            box = box with { Width = px, WidthPercent = percent };
                        }
                        break;
                    }
                case "max-width" when !isImage:
                    if (TryParseLength(value, out var max, out var isPercent) && !isPercent)
                    {
                        box = box with { MaxWidth = Math.Max(0, max) };
                    }
                    break;
            }
        }
    }

    /// <summary>图片的 width / height：属性与 style 中的像素值（style 优先）。</summary>
    public static (double? Width, double? Height) ImageSize(ComponentAttributes attributes)
    {
        double? width = TryParseLength(attributes["width"] ?? "", out var w, out var wp) && !wp ? w : null;
        double? height = TryParseLength(attributes["height"] ?? "", out var h, out var hp) && !hp ? h : null;
        if (attributes["style"] is { Length: > 0 } style)
        {
            foreach (var declaration in style.Split(';'))
            {
                var colon = declaration.IndexOf(':');
                if (colon <= 0)
                {
                    continue;
                }
                var name = declaration[..colon].Trim().ToLowerInvariant();
                var value = declaration[(colon + 1)..].Trim();
                if (name == "width" && TryParseLength(value, out var sw, out var swp) && !swp)
                {
                    width = sw;
                }
                else if (name == "height" && TryParseLength(value, out var sh, out var shp) && !shp)
                {
                    height = sh;
                }
            }
        }
        return (width is > 0 ? Math.Min(width.Value, 4096) : null, height is > 0 ? Math.Min(height.Value, 4096) : null);
    }

    public static TextAlign? ParseAlign(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "left" or "start" => TextAlign.Left,
        "center" or "middle" or "-webkit-center" => TextAlign.Center,
        "right" or "end" => TextAlign.Right,
        "justify" => TextAlign.Justify,
        null => null,
        _ => TextAlign.Inherit,
    };

    private static void ApplyFontSize(string value, ref HtmlTextStyle text)
    {
        var keywordScale = value switch
        {
            "xx-small" => 0.6,
            "x-small" => 0.75,
            "small" => 0.89,
            "smaller" => 0.83,
            "medium" => 1.0,
            "large" => 1.2,
            "larger" => 1.2,
            "x-large" => 1.5,
            "xx-large" => 2.0,
            "xxx-large" => 3.0,
            _ => (double?)null,
        };
        if (keywordScale is { } k)
        {
            text = text with { FontScale = k, FontSize = null };
            return;
        }
        var number = value.TrimEnd();
        if (number.EndsWith('%') && TryParseNumber(number[..^1], out var percent))
        {
            text = text with { FontScale = Math.Clamp(percent / 100, 0.5, 4), FontSize = null };
        }
        else if ((number.EndsWith("rem", StringComparison.Ordinal) || number.EndsWith("em", StringComparison.Ordinal))
                 && TryParseNumber(number.EndsWith("rem", StringComparison.Ordinal) ? number[..^3] : number[..^2], out var em))
        {
            text = text with { FontScale = Math.Clamp(em, 0.5, 4), FontSize = null };
        }
        else if (TryParseLength(number, out var px, out var isPercent) && !isPercent)
        {
            text = text with { FontSize = Math.Clamp(px, MinFontSize, MaxFontSize), FontScale = null };
        }
    }

    private static double? FontTagScale(string size)
    {
        // <font size="1..7"> 或 "+1" / "-1"（相对 3）
        size = size.Trim();
        if (!int.TryParse(size, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var n))
        {
            return null;
        }
        var level = size[0] is '+' or '-' ? 3 + n : n;
        return Math.Clamp(level, 1, 7) switch
        {
            1 => 0.63,
            2 => 0.82,
            3 => 1.0,
            4 => 1.13,
            5 => 1.5,
            6 => 2.0,
            _ => 3.0,
        };
    }

    private static (double? Px, double? Percent) ParseWidth(string? value)
    {
        if (value is null || !TryParseLength(value, out var number, out var isPercent) || number <= 0)
        {
            return (null, null);
        }
        return isPercent ? (null, Math.Min(number, 100)) : (Math.Min(number, 4096), null);
    }

    private static double Spacing(string value) =>
        TryParseLength(value, out var px, out var isPercent) && !isPercent ? Math.Clamp(px, -MaxSpacing / 4, MaxSpacing) : 0;

    // CSS 简写：1 到 4 个值（上 右 下 左）；auto 视为 0
    private static Edges? ParseEdges(string value)
    {
        var parts = SplitValues(value).Select(Spacing).ToArray();
        return parts.Length switch
        {
            1 => new Edges(parts[0], parts[0], parts[0], parts[0]),
            2 => new Edges(parts[0], parts[1], parts[0], parts[1]),
            3 => new Edges(parts[0], parts[1], parts[2], parts[1]),
            >= 4 => new Edges(parts[0], parts[1], parts[2], parts[3]),
            _ => null,
        };
    }

    private static string[] SplitValues(string value) => value.Split([' ', '\t', '\n'], StringSplitOptions.RemoveEmptyEntries);

    /// <summary>解析长度：px（或无单位）、pt、em、rem、%。em / rem 按 16px 换算。</summary>
    public static bool TryParseLength(string value, out double px, out bool isPercent)
    {
        px = 0;
        isPercent = false;
        value = value.Trim().ToLowerInvariant();
        if (value.Length == 0)
        {
            return false;
        }
        if (value is "0" or "auto")
        {
            return true;
        }
        double scale = 1;
        if (value.EndsWith('%'))
        {
            isPercent = true;
            value = value[..^1];
        }
        else if (value.EndsWith("px", StringComparison.Ordinal))
        {
            value = value[..^2];
        }
        else if (value.EndsWith("pt", StringComparison.Ordinal))
        {
            value = value[..^2];
            scale = 96.0 / 72;
        }
        else if (value.EndsWith("rem", StringComparison.Ordinal))
        {
            value = value[..^3];
            scale = ReferenceFontSize;
        }
        else if (value.EndsWith("em", StringComparison.Ordinal))
        {
            value = value[..^2];
            scale = ReferenceFontSize;
        }
        if (!TryParseNumber(value, out var number))
        {
            return false;
        }
        px = number * scale;
        return double.IsFinite(px);
    }

    private static bool TryParseNumber(string value, out double number) =>
        double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out number);

    /// <summary>解析颜色：#rgb、#rgba、#rrggbb、#rrggbbaa、rgb()/rgba()、命名颜色、transparent。</summary>
    public static bool TryParseColor(string? value, out CssColor color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }
        var v = value.Trim().ToLowerInvariant();
        if (v[0] == '#')
        {
            return TryParseHex(v.AsSpan(1), out color);
        }
        if (v.StartsWith("rgb", StringComparison.Ordinal))
        {
            var open = v.IndexOf('(');
            var close = v.IndexOf(')');
            if (open < 0 || close < open)
            {
                return false;
            }
            var parts = v[(open + 1)..close].Split([',', ' ', '/'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
            {
                return false;
            }
            var channels = new byte[3];
            for (var i = 0; i < 3; i++)
            {
                var part = parts[i];
                var isPercent = part.EndsWith('%');
                if (!TryParseNumber(isPercent ? part[..^1] : part, out var n))
                {
                    return false;
                }
                channels[i] = (byte)Math.Clamp(Math.Round(isPercent ? n * 2.55 : n), 0, 255);
            }
            byte alpha = 255;
            if (parts.Length >= 4)
            {
                var part = parts[3];
                var isPercent = part.EndsWith('%');
                if (TryParseNumber(isPercent ? part[..^1] : part, out var a))
                {
                    alpha = (byte)Math.Clamp(Math.Round((isPercent ? a / 100 : a) * 255), 0, 255);
                }
            }
            color = new CssColor(alpha, channels[0], channels[1], channels[2]);
            return true;
        }
        if (v == "transparent")
        {
            color = new CssColor(0, 0, 0, 0);
            return true;
        }
        if (NamedColors.TryGetValue(v, out var rgb))
        {
            color = new CssColor(255, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
            return true;
        }
        // 没有 # 的十六进制（bgcolor="ffcc00" 这种旧写法）
        return v.Length is 3 or 6 && TryParseHex(v, out color);
    }

    private static bool TryParseHex(ReadOnlySpan<char> hex, out CssColor color)
    {
        color = default;
        foreach (var c in hex)
        {
            if (!char.IsAsciiHexDigit(c))
            {
                return false;
            }
        }
        static byte Nibble(char c) => (byte)(Convert.ToInt32(c.ToString(), 16) * 17);
        static byte Pair(ReadOnlySpan<char> s) => (byte)Convert.ToInt32(s.ToString(), 16);
        switch (hex.Length)
        {
            case 3:
                color = new CssColor(255, Nibble(hex[0]), Nibble(hex[1]), Nibble(hex[2]));
                return true;
            case 4:
                color = new CssColor(Nibble(hex[3]), Nibble(hex[0]), Nibble(hex[1]), Nibble(hex[2]));
                return true;
            case 6:
                color = new CssColor(255, Pair(hex[..2]), Pair(hex[2..4]), Pair(hex[4..6]));
                return true;
            case 8:
                color = new CssColor(Pair(hex[6..8]), Pair(hex[..2]), Pair(hex[2..4]), Pair(hex[4..6]));
                return true;
            default:
                return false;
        }
    }

    private static readonly Dictionary<string, int> NamedColors = new(StringComparer.Ordinal)
    {
        ["black"] = 0x000000, ["silver"] = 0xC0C0C0, ["gray"] = 0x808080, ["grey"] = 0x808080, ["white"] = 0xFFFFFF,
        ["maroon"] = 0x800000, ["red"] = 0xFF0000, ["purple"] = 0x800080, ["fuchsia"] = 0xFF00FF, ["magenta"] = 0xFF00FF,
        ["green"] = 0x008000, ["lime"] = 0x00FF00, ["olive"] = 0x808000, ["yellow"] = 0xFFFF00, ["navy"] = 0x000080,
        ["blue"] = 0x0000FF, ["teal"] = 0x008080, ["aqua"] = 0x00FFFF, ["cyan"] = 0x00FFFF, ["orange"] = 0xFFA500,
        ["darkgray"] = 0xA9A9A9, ["darkgrey"] = 0xA9A9A9, ["lightgray"] = 0xD3D3D3, ["lightgrey"] = 0xD3D3D3, ["dimgray"] = 0x696969,
        ["gainsboro"] = 0xDCDCDC, ["whitesmoke"] = 0xF5F5F5, ["darkred"] = 0x8B0000, ["crimson"] = 0xDC143C, ["tomato"] = 0xFF6347,
        ["coral"] = 0xFF7F50, ["orangered"] = 0xFF4500, ["gold"] = 0xFFD700, ["darkorange"] = 0xFF8C00, ["khaki"] = 0xF0E68C,
        ["darkgreen"] = 0x006400, ["forestgreen"] = 0x228B22, ["seagreen"] = 0x2E8B57, ["limegreen"] = 0x32CD32, ["lightgreen"] = 0x90EE90,
        ["darkblue"] = 0x00008B, ["mediumblue"] = 0x0000CD, ["royalblue"] = 0x4169E1, ["steelblue"] = 0x4682B4, ["dodgerblue"] = 0x1E90FF,
        ["deepskyblue"] = 0x00BFFF, ["skyblue"] = 0x87CEEB, ["lightblue"] = 0xADD8E6, ["indigo"] = 0x4B0082, ["violet"] = 0xEE82EE,
        ["pink"] = 0xFFC0CB, ["hotpink"] = 0xFF69B4, ["brown"] = 0xA52A2A, ["chocolate"] = 0xD2691E, ["tan"] = 0xD2B48C,
        ["beige"] = 0xF5F5DC, ["ivory"] = 0xFFFFF0, ["lavender"] = 0xE6E6FA, ["salmon"] = 0xFA8072, ["turquoise"] = 0x40E0D0,
        ["slategray"] = 0x708090, ["slategrey"] = 0x708090, ["darkslategray"] = 0x2F4F4F, ["midnightblue"] = 0x191970, ["rebeccapurple"] = 0x663399,
    };
}
