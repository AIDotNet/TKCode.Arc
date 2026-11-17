namespace TKCode.Arc;

public static class ArcHighlightingBuilderExtensions
{
    /// <summary>The package id of TKCode.Arc.Highlighting.</summary>
    internal const string PackageId = "TKCode.Arc.Highlighting";

    /// <summary>
    /// Enables syntax highlighting: <see cref="CodeBlock"/>, the rich text editor's code blocks and the fenced code of
    /// TKCode.Arc.Markdown paint TextMate grammars in Arc's role palette. Without it code shows as plain text. Depends on
    /// <see cref="ArcBuilderExtensions.UseArcControls"/>. A trimmed app can keep only the grammars it needs with the
    /// <c>ArcHighlightLanguages</c> MSBuild property (for example <c>csharp;json;xml</c>).
    /// </summary>
    public static ArcBuilder UseArcHighlighting(this ArcBuilder builder)
        => builder.UsePackage(PackageId, static package =>
        {
            package.DependsOn(static b => b.UseArcControls());
            // A process-wide service with no Avalonia state, so it is installed while the package is declared: code
            // blocks built before the application starts already highlight.
            CodeBlockHighlighter.Provider = TextMateCodeHighlighter.Shared;
        });
}
