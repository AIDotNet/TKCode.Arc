namespace StreamMark.Avalonia;

/// <summary>
/// StreamMark 公开的资源键。宿主（例如 TKCode.Arc.Markdown）通过覆盖这些键来改变外观；
/// 所有键统一使用 <c>Md.</c> 前缀，避免与宿主资源冲突。
/// </summary>
public static class MarkdownResourceKeys
{
    public const string Foreground = "Md.Foreground";
    public const string SecondaryForeground = "Md.SecondaryForeground";
    public const string LinkForeground = "Md.LinkForeground";
    public const string SelectionBackground = "Md.SelectionBackground";
    public const string FontFamily = "Md.FontFamily";
    public const string MonospaceFontFamily = "Md.MonospaceFontFamily";
    public const string FontSize = "Md.FontSize";
    public const string LineHeight = "Md.LineHeight";
    /// <summary>代码块、差异块及图表 / 公式源码的字号，未设置时为 <c>Math.Round(FontSize * 0.9)</c>。</summary>
    public const string CodeFontSize = "Md.CodeFontSize";
    /// <summary>代码块、差异块的行高（同时用于会话视图的高度估算），未设置时为 <c>Math.Round(CodeFontSize * 1.55)</c>。</summary>
    public const string CodeLineHeight = "Md.CodeLineHeight";
    public const string BlockSpacing = "Md.BlockSpacing";
    public const string CodeBackground = "Md.CodeBackground";
    public const string CodeBorderBrush = "Md.CodeBorderBrush";
    public const string InlineCodeBackground = "Md.InlineCodeBackground";
    /// <summary>行内代码文字色，未设置时使用 <see cref="Foreground"/>。</summary>
    public const string InlineCodeForeground = "Md.InlineCodeForeground";
    /// <summary>列表项目符号与序号的颜色，未设置时使用 <see cref="SecondaryForeground"/>。</summary>
    public const string ListMarkerForeground = "Md.ListMarkerForeground";
    /// <summary><c>&lt;mark&gt;</c> 高亮背景。</summary>
    public const string MarkBackground = "Md.MarkBackground";
    /// <summary>查找结果的高亮背景。</summary>
    public const string SearchHighlightBackground = "Md.SearchHighlightBackground";
    /// <summary>当前查找结果的高亮背景。</summary>
    public const string SearchCurrentBackground = "Md.SearchCurrentBackground";
    public const string QuoteBorderBrush = "Md.QuoteBorderBrush";
    public const string TableBorderBrush = "Md.TableBorderBrush";
    public const string TableHeaderBackground = "Md.TableHeaderBackground";
    public const string CornerRadius = "Md.CornerRadius";
    public const string HoverBackground = "Md.HoverBackground";
}
