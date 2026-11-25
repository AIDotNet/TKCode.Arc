// Ported from vscode (src/vs/editor/common/languageSelector.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - LanguageSelector (upstream: string | LanguageFilter | array of them) is a class with implicit conversions from a
//    string and from a LanguageFilter, and LanguageSelector.Of(...) for the array form; score, targetsNotebooks and
//    selectLanguageIds are its static members Score, TargetsNotebooks and SelectLanguageIds;
//  - a pattern (upstream: string | IRelativePattern) is a GlobPattern, matched by the small Glob below instead of
//    base/common/glob.ts (*, **, ?, {a,b}, [...]; no glob options, no caching of parsed patterns across calls beyond an
//    immutable per-pattern regex);
//  - a URI's fsPath is computed by FsPath (upstream's uriToFsPath without UNC authorities): the path, with a leading
//    "/c:" turned into "c:".

using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;

namespace TKCode.Arc.Editor.Languages;

/// <summary>A glob pattern, optionally relative to a base folder (upstream's <c>string | IRelativePattern</c>).</summary>
internal sealed record GlobPattern(string Pattern, string? Base = null)
{
    public static implicit operator GlobPattern(string pattern) => new(pattern);
}

/// <summary>Selects documents by language, URI scheme, path pattern or notebook type (upstream's LanguageFilter).</summary>
internal sealed record LanguageFilter
{
    public string? Language { get; init; }

    public string? Scheme { get; init; }

    public GlobPattern? Pattern { get; init; }

    public string? NotebookType { get; init; }

    /// <summary>This provider is implemented in the UI thread.</summary>
    public bool HasAccessToAllModels { get; init; }

    public bool Exclusive { get; init; }

    /// <summary>This provider comes from a builtin extension.</summary>
    public bool IsBuiltin { get; init; }
}

/// <summary>Where the notebook a document belongs to is (upstream's NotebookInfo).</summary>
internal sealed record NotebookInfo(Uri Uri, string Type);

/// <summary>
/// Selects the documents a language feature provider applies to: a language id (<c>"csharp"</c>, <c>"*"</c> for any),
/// a <see cref="LanguageFilter"/>, or several of them (<see cref="Of"/>), the best of which counts.
/// </summary>
internal sealed class LanguageSelector
{
    private LanguageSelector(string? language, LanguageFilter? filter, IReadOnlyList<LanguageSelector>? items)
    {
        Language = language;
        Filter = filter;
        Items = items;
    }

    /// <summary>The language id of the short-hand form, or null.</summary>
    public string? Language { get; }

    /// <summary>The filter, or null.</summary>
    public LanguageFilter? Filter { get; }

    /// <summary>The selectors of the array form, or null.</summary>
    public IReadOnlyList<LanguageSelector>? Items { get; }

    public static implicit operator LanguageSelector(string language) => new(language, null, null);

    public static implicit operator LanguageSelector(LanguageFilter filter) => new(null, filter, null);

    /// <summary>The array form: the highest score of <paramref name="items"/>.</summary>
    public static LanguageSelector Of(params LanguageSelector[] items) => new(null, null, items);

    /// <summary>
    /// How well <paramref name="selector"/> matches a document: 10 for an exact match, 5 for a wildcard match, 0 for none.
    /// </summary>
    public static int Score(
        LanguageSelector? selector,
        Uri candidateUri,
        string candidateLanguage,
        bool candidateIsSynchronized,
        Uri? candidateNotebookUri,
        string? candidateNotebookType)
    {
        if (selector is null)
        {
            return 0;
        }

        if (selector.Items is { } items)
        {
            // array -> take max individual value
            var ret = 0;
            foreach (var filter in items)
            {
                var value = Score(filter, candidateUri, candidateLanguage, candidateIsSynchronized, candidateNotebookUri, candidateNotebookType);
                if (value == 10)
                {
                    return value; // already at the highest
                }

                if (value > ret)
                {
                    ret = value;
                }
            }

            return ret;
        }

        if (selector.Language is { } language)
        {
            if (!candidateIsSynchronized)
            {
                return 0;
            }

            // short-hand notion, desugars to
            // 'fooLang' -> { language: 'fooLang'}
            // '*' -> { language: '*' }
            if (language == "*")
            {
                return 5;
            }

            return language == candidateLanguage ? 10 : 0;
        }

        if (selector.Filter is { } f)
        {
            return ScoreFilter(f, candidateUri, candidateLanguage, candidateIsSynchronized, candidateNotebookUri, candidateNotebookType);
        }

        return 0;
    }

    private static int ScoreFilter(
        LanguageFilter selector,
        Uri candidateUri,
        string candidateLanguage,
        bool candidateIsSynchronized,
        Uri? candidateNotebookUri,
        string? candidateNotebookType)
    {
        // filter -> select accordingly, use defaults for scheme
        if (!candidateIsSynchronized && !selector.HasAccessToAllModels)
        {
            return 0;
        }

        // selector targets a notebook -> use the notebook uri instead of the "normal" document uri.
        if (!string.IsNullOrEmpty(selector.NotebookType) && candidateNotebookUri is not null)
        {
            candidateUri = candidateNotebookUri;
        }

        var ret = 0;

        if (!string.IsNullOrEmpty(selector.Scheme))
        {
            if (selector.Scheme == GetScheme(candidateUri))
            {
                ret = 10;
            }
            else if (selector.Scheme == "*")
            {
                ret = 5;
            }
            else
            {
                return 0;
            }
        }

        if (!string.IsNullOrEmpty(selector.Language))
        {
            if (selector.Language == candidateLanguage)
            {
                ret = 10;
            }
            else if (selector.Language == "*")
            {
                ret = Math.Max(ret, 5);
            }
            else
            {
                return 0;
            }
        }

        if (!string.IsNullOrEmpty(selector.NotebookType))
        {
            if (selector.NotebookType == candidateNotebookType)
            {
                ret = 10;
            }
            else if (selector.NotebookType == "*" && candidateNotebookType is not null)
            {
                ret = Math.Max(ret, 5);
            }
            else
            {
                return 0;
            }
        }

        if (selector.Pattern is { } pattern && !string.IsNullOrEmpty(pattern.Pattern))
        {
            var fsPath = FsPath(candidateUri);
            var matches = pattern.Base is null
                ? pattern.Pattern == fsPath || Glob.Match(pattern.Pattern, fsPath)
                : Glob.MatchRelative(Glob.NormalizeBase(pattern.Base), pattern.Pattern, fsPath);
            if (matches)
            {
                ret = 10;
            }
            else
            {
                return 0;
            }
        }

        return ret;
    }

    /// <summary>Whether the selector selects notebook cells.</summary>
    public static bool TargetsNotebooks(LanguageSelector selector)
    {
        if (selector.Language is not null)
        {
            return false;
        }

        if (selector.Items is { } items)
        {
            return items.Any(TargetsNotebooks);
        }

        return !string.IsNullOrEmpty(selector.Filter?.NotebookType);
    }

    /// <summary>Adds the language ids the selector names to <paramref name="into"/>.</summary>
    public static void SelectLanguageIds(LanguageSelector selector, ISet<string> into)
    {
        if (selector.Language is { } language)
        {
            into.Add(language);
        }
        else if (selector.Items is { } items)
        {
            foreach (var item in items)
            {
                SelectLanguageIds(item, into);
            }
        }
        else if (selector.Filter?.Language is { Length: > 0 } filterLanguage)
        {
            into.Add(filterLanguage);
        }
    }

    /// <summary>Whether every part of the selector is exclusive (upstream's isExclusive in languageFeatureRegistry.ts).</summary>
    internal static bool IsExclusive(LanguageSelector selector)
    {
        if (selector.Language is not null)
        {
            return false;
        }

        if (selector.Items is { } items)
        {
            return items.All(IsExclusive);
        }

        return selector.Filter?.Exclusive == true;
    }

    /// <summary>Whether some part of the selector comes from a builtin extension (upstream's isBuiltinSelector).</summary>
    internal static bool IsBuiltinSelector(LanguageSelector selector)
    {
        if (selector.Language is not null)
        {
            return false;
        }

        if (selector.Items is { } items)
        {
            return items.Any(IsBuiltinSelector);
        }

        return selector.Filter?.IsBuiltin == true;
    }

    /// <summary>The scheme of a URI (lower case, as upstream's URI keeps it).</summary>
    internal static string GetScheme(Uri uri) => uri.IsAbsoluteUri ? uri.Scheme : string.Empty;

    /// <summary>The file system path of a URI (upstream's uriToFsPath, '/' separated): its path, "/c:/x" becoming "c:/x".</summary>
    internal static string FsPath(Uri uri)
    {
        if (!uri.IsAbsoluteUri)
        {
            return uri.OriginalString;
        }

        var path = Uri.UnescapeDataString(uri.AbsolutePath);
        if (path.Length >= 3 && path[0] == '/' && char.IsAsciiLetter(path[1]) && path[2] == ':')
        {
            return char.ToLowerInvariant(path[1]) + path[2..];
        }

        return path;
    }
}

/// <summary>
/// A small glob matcher for document selectors (the subset of upstream's base/common/glob.ts they use): <c>*</c> and
/// <c>?</c> within a path segment, <c>**</c> across segments, <c>{a,b}</c> alternatives and <c>[...]</c> ranges.
/// Paths and patterns use '/' (a '\' in a path counts as '/').
/// </summary>
internal static class Glob
{
    // compiled patterns: immutable values, safe to share (a memo cache, not shared mutable state)
    private static readonly ConcurrentDictionary<string, Regex> s_regexes = new(StringComparer.Ordinal);

    /// <summary>Whether <paramref name="path"/> matches <paramref name="pattern"/>.</summary>
    public static bool Match(string pattern, string path) =>
        s_regexes.GetOrAdd(pattern, static p => new Regex(ToRegex(p), RegexOptions.CultureInvariant)).IsMatch(path.Replace('\\', '/'));

    /// <summary>Whether <paramref name="path"/> is inside <paramref name="basePath"/> and its rest matches <paramref name="pattern"/>.</summary>
    public static bool MatchRelative(string basePath, string pattern, string path)
    {
        path = path.Replace('\\', '/');
        if (!path.StartsWith(basePath, StringComparison.Ordinal) || path.Length <= basePath.Length || path[basePath.Length] != '/')
        {
            return false;
        }

        return Match(pattern, path[(basePath.Length + 1)..]);
    }

    /// <summary>A base folder with '/' separators and no trailing separator (upstream normalizes it with path.normalize).</summary>
    public static string NormalizeBase(string basePath)
    {
        var result = basePath.Replace('\\', '/');
        while (result.Length > 1 && result.EndsWith('/'))
        {
            result = result[..^1];
        }

        return result;
    }

    private static string ToRegex(string pattern)
    {
        var builder = new StringBuilder("^");
        Append(builder, pattern, 0, pattern.Length);
        builder.Append('$');
        return builder.ToString();
    }

    private static void Append(StringBuilder builder, string pattern, int start, int end)
    {
        var i = start;
        while (i < end)
        {
            var c = pattern[i];
            switch (c)
            {
                case '*' when i + 1 < end && pattern[i + 1] == '*':
                {
                    var atSegmentStart = i == start || pattern[i - 1] == '/';
                    var followedBySlash = i + 2 < end && pattern[i + 2] == '/';
                    if (atSegmentStart && followedBySlash)
                    {
                        // "**/": any number of whole segments, none included
                        builder.Append("(?:[^/]*/)*");
                        i += 3;
                    }
                    else
                    {
                        builder.Append(".*");
                        i += 2;
                    }

                    break;
                }

                case '*':
                    builder.Append("[^/]*");
                    i++;
                    break;
                case '?':
                    builder.Append("[^/]");
                    i++;
                    break;
                case '{':
                {
                    var close = FindClose(pattern, i, end);
                    if (close < 0)
                    {
                        builder.Append(Regex.Escape("{"));
                        i++;
                        break;
                    }

                    builder.Append("(?:");
                    var partStart = i + 1;
                    var depth = 0;
                    for (var j = i + 1; j < close; j++)
                    {
                        if (pattern[j] == '{')
                        {
                            depth++;
                        }
                        else if (pattern[j] == '}')
                        {
                            depth--;
                        }
                        else if (pattern[j] == ',' && depth == 0)
                        {
                            Append(builder, pattern, partStart, j);
                            builder.Append('|');
                            partStart = j + 1;
                        }
                    }

                    Append(builder, pattern, partStart, close);
                    builder.Append(')');
                    i = close + 1;
                    break;
                }

                case '[':
                {
                    var close = pattern.IndexOf(']', i + 1);
                    if (close < 0 || close >= end)
                    {
                        builder.Append(Regex.Escape("["));
                        i++;
                        break;
                    }

                    var content = pattern[(i + 1)..close];
                    builder.Append('[');
                    if (content.StartsWith('!') || content.StartsWith('^'))
                    {
                        builder.Append('^');
                        content = content[1..];
                    }

                    builder.Append(content.Replace("\\", "\\\\").Replace("[", "\\["));
                    builder.Append(']');
                    i = close + 1;
                    break;
                }

                default:
                    builder.Append(Regex.Escape(c.ToString()));
                    i++;
                    break;
            }
        }
    }

    private static int FindClose(string pattern, int open, int end)
    {
        var depth = 0;
        for (var j = open; j < end; j++)
        {
            if (pattern[j] == '{')
            {
                depth++;
            }
            else if (pattern[j] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return j;
                }
            }
        }

        return -1;
    }
}
