using StreamMark.Avalonia.Plugins;
using StreamMark.Core.Highlighting;

namespace TKCode.Arc;

/// <summary>
/// StreamMark plugin that highlights fenced code in Arc's syntax palette, the same way <see cref="CodeBlock"/> does:
/// with the TextMate grammars of TKCode.Arc.Highlighting when <c>UseArcHighlighting()</c> is enabled, as plain text
/// otherwise. <see cref="Markdown.DefaultServices"/> includes it; add it to your own <c>MarkdownServices</c> when you
/// replace the defaults:
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
    /// <summary>
    /// Creates the plugin; <paramref name="prewarm"/> lists languages whose grammars load in the background now (when
    /// <c>UseArcHighlighting()</c> has already run).
    /// </summary>
    public MarkdownHighlightPlugin(params string[] prewarm)
    {
        if (prewarm.Length > 0 && CodeBlockHighlighter.Provider is { } provider)
        {
            _ = provider.PrewarmAsync(prewarm);
        }
    }

    /// <inheritdoc />
    public string Name => "arc-highlight";

    /// <summary>The highlighter this plugin registers.</summary>
    public ISyntaxHighlighter Highlighter => MarkdownCodeHighlighter.Shared;

    /// <inheritdoc />
    public void Configure(MarkdownPluginBuilder builder) => builder.UseHighlighter(MarkdownCodeHighlighter.Shared);
}
