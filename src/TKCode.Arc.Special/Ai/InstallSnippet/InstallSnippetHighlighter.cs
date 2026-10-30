using System.Collections.Immutable;
using StreamMark.Core.Highlighting;

namespace TKCode.Arc;

/// <summary>
/// Syntax colors for <see cref="InstallSnippet"/> snippets from the library's highlighter (the public
/// <see cref="MarkdownHighlightPlugin"/> highlighter: <see cref="CodeBlock"/>'s TextMate grammars painted in Arc's role
/// palette), memoized per snippet. A snippet is highlighted once per language and appearance on a worker; switching back
/// to a framework, or a new code view for the same snippet during a crossfade, gets its colors at once. Concurrent
/// requests for one snippet share the work. Snippets are a few lines each, so the cache keeps the latest 64 and then
/// starts over.
/// </summary>
internal static class InstallSnippetHighlighter
{
    private const int Capacity = 64;

    private static readonly ISyntaxHighlighter s_highlighter = new MarkdownHighlightPlugin().Highlighter;
    private static readonly Dictionary<string, IReadOnlyList<ImmutableArray<HighlightToken>>?> s_done = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, Task<IReadOnlyList<ImmutableArray<HighlightToken>>?>> s_running = new(StringComparer.Ordinal);
    private static readonly object s_gate = new();

    /// <summary>Whether the language has a grammar.</summary>
    public static bool Supports(string language) => language.Length > 0 && s_highlighter.SupportsLanguage(language);

    /// <summary>The tokens of <paramref name="code"/> when they were made already (null tokens: no grammar, plain text).</summary>
    public static bool TryGet(string code, string language, bool dark, out IReadOnlyList<ImmutableArray<HighlightToken>>? tokens)
    {
        if (code.Length == 0 || !Supports(language))
        {
            tokens = null;
            return true;
        }
        lock (s_gate)
        {
            return s_done.TryGetValue(KeyOf(code, language, dark), out tokens);
        }
    }

    /// <summary>Highlights <paramref name="code"/> (on a worker) and remembers the result. Call on the UI thread.</summary>
    public static Task<IReadOnlyList<ImmutableArray<HighlightToken>>?> HighlightAsync(string code, string language, bool dark)
    {
        if (TryGet(code, language, dark, out var known))
        {
            return Task.FromResult(known);
        }
        var key = KeyOf(code, language, dark);
        lock (s_gate)
        {
            if (s_running.TryGetValue(key, out var running))
            {
                return running;
            }
        }
        var task = RunAsync(key, code, language, dark);
        lock (s_gate)
        {
            if (!task.IsCompleted)
            {
                s_running[key] = task;
            }
        }
        return task;
    }

    /// <summary>Forgets every remembered snippet (tests).</summary>
    internal static void Clear()
    {
        lock (s_gate)
        {
            s_done.Clear();
        }
    }

    private static async Task<IReadOnlyList<ImmutableArray<HighlightToken>>?> RunAsync(string key, string code, string language, bool dark)
    {
        IReadOnlyList<ImmutableArray<HighlightToken>>? tokens;
        try
        {
            using var session = s_highlighter.CreateSession(language, dark ? HighlightThemeKind.Dark : HighlightThemeKind.Light);
            tokens = await Task.Run(() => session.Update(code));
        }
        catch (Exception)
        {
            // A grammar that cannot load leaves the snippet plain.
            tokens = null;
        }
        lock (s_gate)
        {
            s_running.Remove(key);
            if (s_done.Count >= Capacity)
            {
                s_done.Clear();
            }
            s_done[key] = tokens;
        }
        return tokens;
    }

    private static string KeyOf(string code, string language, bool dark) => (dark ? "d/" : "l/") + language + "\n" + code;
}
