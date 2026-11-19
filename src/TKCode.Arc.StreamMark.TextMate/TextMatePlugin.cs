using StreamMark.Avalonia.Plugins;

namespace StreamMark.Highlighting.TextMate;

/// <summary>代码高亮插件（TextMate 语法，按行增量着色）。</summary>
public sealed class TextMatePlugin(TextMateSyntaxHighlighter? highlighter = null, params string[] prewarm) : IMarkdownPlugin
{
    public string Name => "textmate";

    public TextMateSyntaxHighlighter Highlighter { get; } = Prewarmed(highlighter ?? new TextMateSyntaxHighlighter(), prewarm);

    public void Configure(MarkdownPluginBuilder builder) => builder.UseHighlighter(Highlighter);

    private static TextMateSyntaxHighlighter Prewarmed(TextMateSyntaxHighlighter highlighter, string[] languages)
    {
        if (languages.Length > 0)
        {
            highlighter.Prewarm(languages);
        }
        return highlighter;
    }
}
