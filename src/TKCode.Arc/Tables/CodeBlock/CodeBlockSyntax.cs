using System.Collections.Immutable;

namespace TKCode.Arc;

/// <summary>Syntax role of a highlighted span (Arc's CSS classes).</summary>
internal enum CodeBlockSyntaxKind : byte
{
    None,
    Comment,
    String,
    Number,
    Keyword,
    Type,
    Function,
    Property,
    Tag,
    Punctuation,

    /// <summary>An added line of a diff (Arc's success color).</summary>
    Inserted,

    /// <summary>A removed line of a diff (Arc's danger color).</summary>
    Deleted
}

/// <summary>A highlighted range of the displayed source.</summary>
internal readonly record struct CodeBlockSyntaxSpan(int Start, int Length, CodeBlockSyntaxKind Kind);

/// <summary>
/// A syntax highlighter that <see cref="CodeBlock"/>, the rich text editor's code blocks and the Markdown package's
/// fenced code read their roles from. TKCode.Arc ships none: <c>UseArcHighlighting()</c> (TKCode.Arc.Highlighting)
/// installs the TextMate one, and without it code is plain text.
/// </summary>
/// <remarks>Implementations are thread safe; a session is used by one thread at a time.</remarks>
internal interface ICodeSyntaxHighlighter
{
    /// <summary>Whether <paramref name="language"/> (a fence or <see cref="CodeBlock.Language"/> name) has a grammar.</summary>
    bool SupportsLanguage(string language);

    /// <summary>Creates an incremental session for a language <see cref="SupportsLanguage"/> accepted.</summary>
    ICodeSyntaxSession CreateSession(string language);

    /// <summary>Loads these languages' grammars in the background; unknown names are ignored. The task never fails.</summary>
    Task PrewarmAsync(IEnumerable<string> languages);
}

/// <summary>An incremental highlighting session for one source in one language.</summary>
internal interface ICodeSyntaxSession : IDisposable
{
    /// <summary>
    /// Highlights <paramref name="code"/>: one entry per '\n'-separated line, each with the line's role spans (ascending,
    /// non-overlapping, offsets relative to the line start, plain text left out). An unchanged line keeps the same
    /// array as in the previous result.
    /// </summary>
    IReadOnlyList<ImmutableArray<CodeBlockSyntaxSpan>> Update(string code);
}

/// <summary>
/// The installed <see cref="ICodeSyntaxHighlighter"/> and the helpers CodeBlock and the rich text editor share.
/// </summary>
internal static class CodeBlockHighlighter
{
    private static volatile ICodeSyntaxHighlighter? s_provider;

    /// <summary>The number of <see cref="CodeBlockSyntaxKind"/> values (palettes are indexed by role).</summary>
    public const int KindCount = (int)CodeBlockSyntaxKind.Deleted + 1;

    /// <summary>
    /// The highlighter in use, or null for plain text. Set once at startup by a package (<c>UseArcHighlighting()</c>);
    /// code blocks created before that stay plain until their source or language changes.
    /// </summary>
    public static ICodeSyntaxHighlighter? Provider
    {
        get => s_provider;
        set => s_provider = value;
    }

    /// <summary>Arc's <c>normaliseLanguage</c> for the header label: lower case, no leading dot.</summary>
    public static string NormalizeLanguage(string? language)
    {
        if (string.IsNullOrEmpty(language))
        {
            return string.Empty;
        }
        var lower = language.ToLowerInvariant();
        return lower.StartsWith('.') ? lower[1..] : lower;
    }

    /// <summary>Whether the installed highlighter has a grammar for <paramref name="language"/>.</summary>
    public static bool SupportsLanguage(string? language)
        => !string.IsNullOrWhiteSpace(language) && s_provider is { } provider && provider.SupportsLanguage(language);

    /// <summary>Creates a session, or null when nothing highlights the language (plain text).</summary>
    public static ICodeSyntaxSession? CreateSession(string? language)
        => !string.IsNullOrWhiteSpace(language) && s_provider is { } provider && provider.SupportsLanguage(language)
            ? provider.CreateSession(language)
            : null;

    /// <summary>
    /// Appends a session result as absolute spans of <paramref name="code"/> (lines are separated by '\n').
    /// </summary>
    public static void AppendSpans(string code, IReadOnlyList<ImmutableArray<CodeBlockSyntaxSpan>> lines, List<CodeBlockSyntaxSpan> output)
    {
        var lineStart = 0;
        for (var i = 0; i < lines.Count && lineStart <= code.Length; i++)
        {
            var line = lines[i];
            if (!line.IsDefaultOrEmpty)
            {
                foreach (var span in line)
                {
                    output.Add(span with { Start = lineStart + span.Start });
                }
            }
            var newline = code.IndexOf('\n', lineStart);
            if (newline < 0)
            {
                break;
            }
            lineStart = newline + 1;
        }
    }
}
