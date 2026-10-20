using StreamMark.Avalonia.Plugins;
using StreamMark.Core.Highlighting;

namespace TKCode.Arc;

/// <summary>
/// StreamMark plugin that highlights fenced code with TextMate grammars painted in Arc's syntax palette, the same way
/// <see cref="CodeBlock"/> does. <see cref="Markdown.DefaultServices"/> includes it; add it to your own
/// <c>MarkdownServices</c> when you replace the defaults:
/// <code>
/// markdown.Services = new MarkdownServices
/// {
///     Plugins = [new MarkdownHighlightPlugin("cs", "ts"), new RaTeXPlugin()],
/// };
/// </code>
/// (Use StreamMark's <c>TextMatePlugin</c> instead for VS Code's own theme colors.)
/// </summary>
public sealed class MarkdownHighlightPlugin : IMarkdownPlugin
{
    /// <summary>Creates the plugin; <paramref name="prewarm"/> lists languages whose grammars load in the background now.</summary>
    public MarkdownHighlightPlugin(params string[] prewarm)
    {
        if (prewarm.Length > 0)
        {
            // Sessions always use the Light+ context (the role table), so only that one is warmed.
            _ = CodeBlockHighlighter.Engine.PrewarmAsync(CodeBlockHighlighter.SessionTheme, prewarm);
        }
    }

    /// <inheritdoc />
    public string Name => "arc-highlight";

    /// <summary>The highlighter this plugin registers.</summary>
    public ISyntaxHighlighter Highlighter => MarkdownCodeHighlighter.Shared;

    /// <inheritdoc />
    public void Configure(MarkdownPluginBuilder builder) => builder.UseHighlighter(MarkdownCodeHighlighter.Shared);
}
