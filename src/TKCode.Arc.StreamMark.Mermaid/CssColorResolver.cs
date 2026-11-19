using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace StreamMark.Mermaid;

/// <summary>
/// 把 Mermaider 输出 SVG 中的 CSS 自定义属性（<c>var(--x, fallback)</c>）和 <c>color-mix(in srgb, A p%, B q%)</c>
/// 解析成字面量。Svg.Skia 不支持 <c>color-mix()</c>（会画成黑色填充、丢失连线），因此必须在交给它之前展开。
/// </summary>
/// <remarks>
/// 变量来源：根 <c>&lt;svg style="--bg:…;--fg:…"&gt;</c> 的内联声明，以及 <c>&lt;style&gt;</c> 中 <c>svg { --_text: … }</c> 规则里的声明。
/// 解析后整份文档里再也不包含 <c>var(</c> / <c>color-mix(</c>：颜色输出为 <c>#rrggbb</c>（不透明）或 <c>rgba(r,g,b,a)</c>，
/// <c>rem</c>/<c>em</c> 字号换算为 px（1rem = 16px）。
/// </remarks>
internal static partial class CssColorResolver
{
    private const int MaxDepth = 32;

    [GeneratedRegex(@"--([A-Za-z0-9_-]+)\s*:\s*([^;{}]+?)\s*(?:;|(?=\})|$)", RegexOptions.CultureInvariant)]
    private static partial Regex DeclarationRegex();

    [GeneratedRegex(@"<svg\b[^>]*?\sstyle=""([^""]*)""", RegexOptions.CultureInvariant)]
    private static partial Regex RootStyleRegex();

    [GeneratedRegex(@"<style[^>]*>(.*?)</style>", RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex StyleBlockRegex();

    [GeneratedRegex(@"(?<![-\w])svg\s*\{([^}]*)\}", RegexOptions.CultureInvariant)]
    private static partial Regex SvgRuleRegex();

    [GeneratedRegex(@"^\s*(-?\d*\.?\d+)(rem|em)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex RelativeLengthRegex();

    /// <summary>收集变量并展开整份 SVG。<paramref name="overrides"/> 优先于文档内的声明。</summary>
    public static string Resolve(string svg, IReadOnlyDictionary<string, string>? overrides = null)
    {
        var vars = new Dictionary<string, string>(StringComparer.Ordinal);

        var root = RootStyleRegex().Match(svg);
        if (root.Success)
        {
            CollectDeclarations(root.Groups[1].Value, vars);
        }

        foreach (Match style in StyleBlockRegex().Matches(svg))
        {
            foreach (Match rule in SvgRuleRegex().Matches(style.Groups[1].Value))
            {
                CollectDeclarations(rule.Groups[1].Value, vars);
            }
        }

        if (overrides is not null)
        {
            foreach (var (name, value) in overrides)
            {
                vars[name] = value;
            }
        }

        return Substitute(svg, vars, 0);
    }

    private static void CollectDeclarations(string text, Dictionary<string, string> vars)
    {
        foreach (Match m in DeclarationRegex().Matches(text))
        {
            // 先收集的根内联声明优先（CSS 层叠中内联样式胜过样式表规则）。
            vars.TryAdd(m.Groups[1].Value, m.Groups[2].Value.Trim());
        }
    }

    /// <summary>把文本中所有 <c>var(...)</c> / <c>color-mix(...)</c> 替换为字面量。</summary>
    internal static string Substitute(string text, IReadOnlyDictionary<string, string> vars, int depth)
    {
        if (depth > MaxDepth || (text.IndexOf("var(", StringComparison.Ordinal) < 0 && text.IndexOf("color-mix(", StringComparison.Ordinal) < 0))
        {
            return text;
        }

        var sb = new StringBuilder(text.Length);
        var i = 0;
        while (i < text.Length)
        {
            var fn = FindFunction(text, i, out var start);
            if (fn is null)
            {
                sb.Append(text, i, text.Length - i);
                break;
            }

            sb.Append(text, i, start - i);
            var open = start + fn.Length; // 指向 '(' 之后
            var close = FindClosingParen(text, open);
            if (close < 0)
            {
                // 括号不配对：原样保留剩余部分。
                sb.Append(text, start, text.Length - start);
                break;
            }

            var inner = text.AsSpan(open, close - open).ToString();
            sb.Append(fn == "var(" ? EvaluateVar(inner, vars, depth + 1) : EvaluateColorMix(inner, vars, depth + 1));
            i = close + 1;
        }

        return sb.ToString();
    }

    private static string? FindFunction(string text, int from, out int start)
    {
        var v = IndexOfFunction(text, "var(", from);
        var c = IndexOfFunction(text, "color-mix(", from);
        if (v < 0 && c < 0)
        {
            start = -1;
            return null;
        }

        if (c < 0 || (v >= 0 && v < c))
        {
            start = v;
            return "var(";
        }

        start = c;
        return "color-mix(";
    }

    private static int IndexOfFunction(string text, string name, int from)
    {
        while (true)
        {
            var idx = text.IndexOf(name, from, StringComparison.Ordinal);
            if (idx <= 0)
            {
                return idx;
            }

            var prev = text[idx - 1];
            if (!char.IsLetterOrDigit(prev) && prev != '-' && prev != '_')
            {
                return idx;
            }

            from = idx + name.Length;
        }
    }

    private static int FindClosingParen(string text, int from)
    {
        var level = 0;
        for (var i = from; i < text.Length; i++)
        {
            switch (text[i])
            {
                case '(':
                    level++;
                    break;
                case ')':
                    if (level == 0)
                    {
                        return i;
                    }

                    level--;
                    break;
                case '"':
                case '<':
                case '>':
                    // 不跨越 XML 属性 / 标签边界。
                    return -1;
            }
        }

        return -1;
    }

    private static List<string> SplitTopLevel(string text)
    {
        var parts = new List<string>(3);
        var level = 0;
        var last = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '(')
            {
                level++;
            }
            else if (ch == ')')
            {
                level--;
            }
            else if (ch == ',' && level == 0)
            {
                parts.Add(text[last..i]);
                last = i + 1;
            }
        }

        parts.Add(text[last..]);
        return parts;
    }

    private static string EvaluateVar(string inner, IReadOnlyDictionary<string, string> vars, int depth)
    {
        var comma = IndexOfTopLevelComma(inner);
        var name = (comma < 0 ? inner : inner[..comma]).Trim();
        if (name.StartsWith("--", StringComparison.Ordinal))
        {
            name = name[2..];
        }

        string? value = null;
        if (vars.TryGetValue(name, out var raw) && depth <= MaxDepth)
        {
            value = Substitute(raw, vars, depth).Trim();
        }
        else if (comma >= 0)
        {
            value = Substitute(inner[(comma + 1)..], vars, depth).Trim();
        }

        if (string.IsNullOrEmpty(value))
        {
            return "none";
        }

        var rel = RelativeLengthRegex().Match(value);
        if (rel.Success && double.TryParse(rel.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var em))
        {
            return FormatNumber(em * 16);
        }

        return value;
    }

    private static int IndexOfTopLevelComma(string text)
    {
        var level = 0;
        for (var i = 0; i < text.Length; i++)
        {
            switch (text[i])
            {
                case '(':
                    level++;
                    break;
                case ')':
                    level--;
                    break;
                case ',' when level == 0:
                    return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// CSS Color 5 <c>color-mix()</c>：百分比归一化、预乘 alpha 在 sRGB 中插值；两者之和小于 100% 时按比例降低 alpha。
    /// 非 srgb 插值空间（oklab 等）按 srgb 近似。
    /// </summary>
    private static string EvaluateColorMix(string inner, IReadOnlyDictionary<string, string> vars, int depth)
    {
        var args = SplitTopLevel(inner);
        if (args.Count != 3)
        {
            return "none";
        }

        if (!TryParseMixArgument(Substitute(args[1], vars, depth), out var c1, out var p1) ||
            !TryParseMixArgument(Substitute(args[2], vars, depth), out var c2, out var p2))
        {
            return "none";
        }

        double w1, w2;
        if (p1 is null && p2 is null)
        {
            w1 = w2 = 0.5;
        }
        else if (p1 is null)
        {
            w2 = p2!.Value;
            w1 = 1 - w2;
        }
        else if (p2 is null)
        {
            w1 = p1.Value;
            w2 = 1 - w1;
        }
        else
        {
            w1 = p1.Value;
            w2 = p2.Value;
        }

        var sum = w1 + w2;
        if (sum <= 0)
        {
            return "none";
        }

        var alphaScale = sum < 1 ? sum : 1;
        w1 /= sum;
        w2 /= sum;

        var a = c1.A * w1 + c2.A * w2;
        double r = 0, g = 0, b = 0;
        if (a > 0)
        {
            r = (c1.R * c1.A * w1 + c2.R * c2.A * w2) / a;
            g = (c1.G * c1.A * w1 + c2.G * c2.A * w2) / a;
            b = (c1.B * c1.A * w1 + c2.B * c2.A * w2) / a;
        }

        return Format(new RgbaColor(r, g, b, a * alphaScale));
    }

    private static bool TryParseMixArgument(string arg, out RgbaColor color, out double? percent)
    {
        percent = null;
        arg = arg.Trim();

        // 颜色与百分比顺序任意："#fff 30%" 或 "30% #fff"。
        var tokens = SplitBySpaceTopLevel(arg);
        string? colorText = null;
        foreach (var token in tokens)
        {
            if (token.EndsWith('%') && double.TryParse(token.AsSpan(0, token.Length - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out var p))
            {
                percent = Math.Clamp(p / 100, 0, 1);
            }
            else
            {
                colorText = colorText is null ? token : colorText + " " + token;
            }
        }

        color = default;
        return colorText is not null && TryParseColor(colorText, out color);
    }

    private static List<string> SplitBySpaceTopLevel(string text)
    {
        var parts = new List<string>(2);
        var level = 0;
        var start = -1;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '(')
            {
                level++;
            }
            else if (ch == ')')
            {
                level--;
            }

            if (char.IsWhiteSpace(ch) && level == 0)
            {
                if (start >= 0)
                {
                    parts.Add(text[start..i]);
                    start = -1;
                }
            }
            else if (start < 0)
            {
                start = i;
            }
        }

        if (start >= 0)
        {
            parts.Add(text[start..]);
        }

        return parts;
    }

    /// <summary>解析 CSS 颜色：#rgb / #rgba / #rrggbb / #rrggbbaa、rgb()/rgba()、常用颜色名。分量 0..1。</summary>
    internal static bool TryParseColor(string text, out RgbaColor color)
    {
        color = default;
        text = text.Trim();
        if (text.Length == 0)
        {
            return false;
        }

        if (text[0] == '#')
        {
            var hex = text.AsSpan(1);
            if (!uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v))
            {
                return false;
            }

            switch (hex.Length)
            {
                case 3:
                    color = new RgbaColor(Nibble(v >> 8), Nibble(v >> 4), Nibble(v), 1);
                    return true;
                case 4:
                    color = new RgbaColor(Nibble(v >> 12), Nibble(v >> 8), Nibble(v >> 4), Nibble(v));
                    return true;
                case 6:
                    color = new RgbaColor(((v >> 16) & 0xFF) / 255.0, ((v >> 8) & 0xFF) / 255.0, (v & 0xFF) / 255.0, 1);
                    return true;
                case 8:
                    color = new RgbaColor(((v >> 24) & 0xFF) / 255.0, ((v >> 16) & 0xFF) / 255.0, ((v >> 8) & 0xFF) / 255.0, (v & 0xFF) / 255.0);
                    return true;
                default:
                    return false;
            }
        }

        if (text.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
        {
            var open = text.IndexOf('(');
            var close = text.LastIndexOf(')');
            if (open < 0 || close < open)
            {
                return false;
            }

            var parts = text[(open + 1)..close].Split([',', ' ', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length is < 3 or > 4)
            {
                return false;
            }

            var c = new double[4];
            c[3] = 1;
            for (var i = 0; i < parts.Length; i++)
            {
                var p = parts[i];
                var isPercent = p.EndsWith('%');
                if (!double.TryParse(isPercent ? p[..^1] : p, NumberStyles.Float, CultureInfo.InvariantCulture, out var n))
                {
                    return false;
                }

                c[i] = isPercent ? n / 100 : i < 3 ? n / 255 : n;
            }

            color = new RgbaColor(Math.Clamp(c[0], 0, 1), Math.Clamp(c[1], 0, 1), Math.Clamp(c[2], 0, 1), Math.Clamp(c[3], 0, 1));
            return true;
        }

        switch (text.ToLowerInvariant())
        {
            case "transparent":
                color = new RgbaColor(0, 0, 0, 0);
                return true;
            case "black":
                color = new RgbaColor(0, 0, 0, 1);
                return true;
            case "white":
                color = new RgbaColor(1, 1, 1, 1);
                return true;
        }

        // 其它命名颜色交给 Avalonia（它的 8 位十六进制是 #AARRGGBB，但上面已处理所有 # 开头的情况）。
        if (global::Avalonia.Media.Color.TryParse(text, out var named))
        {
            color = new RgbaColor(named.R / 255.0, named.G / 255.0, named.B / 255.0, named.A / 255.0);
            return true;
        }

        return false;

        static double Nibble(uint v) => (v & 0xF) * 17 / 255.0;
    }

    internal static string Format(RgbaColor c)
    {
        var r = ToByte(c.R);
        var g = ToByte(c.G);
        var b = ToByte(c.B);
        if (c.A >= 0.999)
        {
            return string.Create(CultureInfo.InvariantCulture, $"#{r:x2}{g:x2}{b:x2}");
        }

        return string.Create(CultureInfo.InvariantCulture, $"rgba({r},{g},{b},{Math.Round(Math.Clamp(c.A, 0, 1), 3)})");
    }

    internal static string Format(global::Avalonia.Media.Color c) =>
        Format(new RgbaColor(c.R / 255.0, c.G / 255.0, c.B / 255.0, c.A / 255.0));

    private static byte ToByte(double v) => (byte)Math.Clamp(Math.Round(v * 255 + 1e-9, MidpointRounding.AwayFromZero), 0, 255);

    private static string FormatNumber(double v) => Math.Round(v, 3).ToString(CultureInfo.InvariantCulture);
}

/// <summary>sRGB 颜色，分量 0..1（非预乘）。</summary>
internal readonly record struct RgbaColor(double R, double G, double B, double A);
