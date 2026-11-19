using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;
using StreamMark.Core.Highlighting;

namespace StreamMark.Avalonia.Rendering;

/// <summary>
/// 一次性解析出的不可变样式快照。块视图只读取这个对象，不在每个文本片段上查找资源。
/// 主题或资源变化时由宿主控件重新解析并下发。
/// </summary>
public sealed class MarkdownStyle
{
    public required IBrush Foreground { get; init; }
    public required IBrush SecondaryForeground { get; init; }
    public required IBrush LinkForeground { get; init; }
    public required IBrush SelectionBackground { get; init; }
    public required IBrush CodeBackground { get; init; }
    public required IBrush CodeBorderBrush { get; init; }
    public required IBrush InlineCodeBackground { get; init; }
    public required IBrush InlineCodeForeground { get; init; }
    public required IBrush ListMarkerForeground { get; init; }
    public required IBrush MarkBackground { get; init; }

    public required IBrush SearchHighlightBackground { get; init; }

    public required IBrush SearchCurrentBackground { get; init; }
    public required IBrush QuoteBorderBrush { get; init; }
    public required IBrush TableBorderBrush { get; init; }
    public required IBrush TableHeaderBackground { get; init; }
    public required IBrush HoverBackground { get; init; }
    public required FontFamily FontFamily { get; init; }
    public required FontFamily MonospaceFontFamily { get; init; }
    public required double FontSize { get; init; }
    public required double LineHeight { get; init; }
    public required double BlockSpacing { get; init; }
    public required CornerRadius CornerRadius { get; init; }
    public required bool IsDark { get; init; }

    public double CodeFontSize => Math.Round(FontSize * 0.9);

    public double CodeLineHeight => Math.Round(CodeFontSize * 1.55);

    public HighlightThemeKind HighlightTheme => IsDark ? HighlightThemeKind.Dark : HighlightThemeKind.Light;

    /// <summary>标题字号（1–6 级）。</summary>
    public double HeadingFontSize(int level) => level switch
    {
        1 => Math.Round(FontSize * 1.75),
        2 => Math.Round(FontSize * 1.5),
        3 => Math.Round(FontSize * 1.25),
        4 => Math.Round(FontSize * 1.1),
        _ => FontSize,
    };

    /// <summary>提示块（NOTE / TIP / IMPORTANT / WARNING / CAUTION）的强调色。</summary>
    public IBrush AlertBrush(string kind) => kind switch
    {
        "TIP" => IsDark ? Brushes.MediumSeaGreen : Brushes.SeaGreen,
        "IMPORTANT" => IsDark ? new SolidColorBrush(Color.FromRgb(0xA3, 0x71, 0xF7)) : new SolidColorBrush(Color.FromRgb(0x82, 0x50, 0xDF)),
        "WARNING" => IsDark ? new SolidColorBrush(Color.FromRgb(0xD2, 0x99, 0x22)) : new SolidColorBrush(Color.FromRgb(0x9A, 0x67, 0x00)),
        "CAUTION" => IsDark ? new SolidColorBrush(Color.FromRgb(0xF8, 0x51, 0x49)) : new SolidColorBrush(Color.FromRgb(0xCF, 0x22, 0x2E)),
        _ => LinkForeground,
    };

    /// <summary>从控件可见的资源（<c>Md.*</c> 键）解析样式。</summary>
    public static MarkdownStyle Resolve(Control control)
    {
        var variant = control.ActualThemeVariant;
        var isDark = variant == ThemeVariant.Dark;
        T Get<T>(string key, T fallback)
        {
            if (!control.TryFindResource(key, variant, out var value))
            {
                return fallback;
            }
            // 别名：从控件本身查找目标键（宿主 token 可能只在窗口 / 主题作用域内可见）
            for (var depth = 0; value is MarkdownResourceAlias alias && depth < 4; depth++)
            {
                if (!control.TryFindResource(alias.Key, variant, out value))
                {
                    return fallback;
                }
            }
            if (value is T typed)
            {
                return typed;
            }
            if (value is Color color && typeof(T) == typeof(IBrush))
            {
                return (T)(object)new ImmutableSolidColorBrush(color);
            }
            return fallback;
        }

        var foreground = Get<IBrush>(MarkdownResourceKeys.Foreground, isDark ? Brushes.WhiteSmoke : Brushes.Black);
        var secondary = Get<IBrush>(MarkdownResourceKeys.SecondaryForeground, Brushes.Gray);
        // 未显式配置字体时沿用继承的 TextElement 字体（宿主设在窗口上的字体）
        var fontSize = Get<double>(MarkdownResourceKeys.FontSize, control.GetValue(TextElement.FontSizeProperty));
        var fontFamily = Get<FontFamily>(MarkdownResourceKeys.FontFamily, control.GetValue(TextElement.FontFamilyProperty));
        return new MarkdownStyle
        {
            Foreground = foreground,
            SecondaryForeground = secondary,
            InlineCodeForeground = Get<IBrush>(MarkdownResourceKeys.InlineCodeForeground, foreground),
            ListMarkerForeground = Get<IBrush>(MarkdownResourceKeys.ListMarkerForeground, secondary),
            MarkBackground = Get<IBrush>(MarkdownResourceKeys.MarkBackground, new SolidColorBrush(isDark ? Color.FromArgb(0x55, 0xE6, 0xB4, 0x22) : Color.FromArgb(0x66, 0xFF, 0xE0, 0x66))),
            SearchHighlightBackground = Get<IBrush>(MarkdownResourceKeys.SearchHighlightBackground, new SolidColorBrush(isDark ? Color.FromArgb(0x4D, 0xE6, 0xB4, 0x22) : Color.FromArgb(0x66, 0xFF, 0xD2, 0x4D))),
            SearchCurrentBackground = Get<IBrush>(MarkdownResourceKeys.SearchCurrentBackground, new SolidColorBrush(isDark ? Color.FromArgb(0xB3, 0xF5, 0x8A, 0x1F) : Color.FromArgb(0xCC, 0xFF, 0x96, 0x32))),
            LinkForeground = Get<IBrush>(MarkdownResourceKeys.LinkForeground, Brushes.DodgerBlue),
            SelectionBackground = Get<IBrush>(MarkdownResourceKeys.SelectionBackground, new SolidColorBrush(Color.FromArgb(0x59, 0x40, 0x96, 0xFF))),
            CodeBackground = Get<IBrush>(MarkdownResourceKeys.CodeBackground, isDark ? new SolidColorBrush(Color.FromRgb(0x16, 0x1B, 0x22)) : new SolidColorBrush(Color.FromRgb(0xF6, 0xF8, 0xFA))),
            CodeBorderBrush = Get<IBrush>(MarkdownResourceKeys.CodeBorderBrush, Brushes.Transparent),
            InlineCodeBackground = Get<IBrush>(MarkdownResourceKeys.InlineCodeBackground, new SolidColorBrush(Color.FromArgb(0x1A, 0x80, 0x80, 0x80))),
            QuoteBorderBrush = Get<IBrush>(MarkdownResourceKeys.QuoteBorderBrush, Brushes.Gray),
            TableBorderBrush = Get<IBrush>(MarkdownResourceKeys.TableBorderBrush, Brushes.Gray),
            TableHeaderBackground = Get<IBrush>(MarkdownResourceKeys.TableHeaderBackground, Brushes.Transparent),
            HoverBackground = Get<IBrush>(MarkdownResourceKeys.HoverBackground, new SolidColorBrush(Color.FromArgb(0x26, 0x80, 0x80, 0x80))),
            FontFamily = fontFamily,
            MonospaceFontFamily = Get<FontFamily>(MarkdownResourceKeys.MonospaceFontFamily, new FontFamily("Menlo, Consolas, monospace")),
            FontSize = fontSize,
            LineHeight = Get<double>(MarkdownResourceKeys.LineHeight, Math.Round(fontSize * 1.6)),
            BlockSpacing = Get<double>(MarkdownResourceKeys.BlockSpacing, 12),
            CornerRadius = Get<CornerRadius>(MarkdownResourceKeys.CornerRadius, new CornerRadius(6)),
            IsDark = isDark,
        };
    }
}
