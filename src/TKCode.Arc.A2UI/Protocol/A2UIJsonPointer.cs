using System.Text;

namespace TKCode.Arc.A2UI;

/// <summary>
/// JSON Pointers (RFC 6901) as the A2UI data model reads them: <c>~0</c> and <c>~1</c> escapes, empty segments ignored
/// (so <c>/a//b/</c> is <c>/a/b</c>), and the segments <c>__proto__</c>, <c>constructor</c> and <c>prototype</c> refused.
/// </summary>
public static class A2UIJsonPointer
{
    /// <summary>The largest list index a write may create.</summary>
    public const int MaxListIndex = 10_000;

    private static readonly string[] s_empty = [];

    /// <summary>Splits a pointer into unescaped segments; <c>""</c> and <c>"/"</c> give none (the root).</summary>
    public static string[] Parse(string? pointer)
    {
        if (string.IsNullOrEmpty(pointer))
        {
            return s_empty;
        }

        var parts = pointer.Split('/');
        var list = new List<string>(parts.Length);
        foreach (var raw in parts)
        {
            if (raw.Length == 0)
            {
                continue;
            }

            for (var i = raw.IndexOf('~'); i >= 0; i = raw.IndexOf('~', i + 1))
            {
                if (i + 1 >= raw.Length || raw[i + 1] is not ('0' or '1'))
                {
                    throw A2UIException.Validation($"Invalid path syntax in '{pointer}': '~' must be followed by 0 or 1.");
                }
            }

            var segment = Unescape(raw);
            if (segment is "__proto__" or "constructor" or "prototype")
            {
                throw A2UIException.DataError($"Forbidden path segment '{segment}' in '{pointer}'.");
            }

            list.Add(segment);
        }

        return list.Count == 0 ? s_empty : list.ToArray();
    }

    /// <summary>Joins segments into a normalized pointer (<c>/</c> for the root).</summary>
    public static string Format(IReadOnlyList<string> segments)
    {
        if (segments.Count == 0)
        {
            return "/";
        }

        var sb = new StringBuilder();
        foreach (var segment in segments)
        {
            sb.Append('/').Append(Escape(segment));
        }

        return sb.ToString();
    }

    /// <summary>The normalized form of a pointer (<c>/a/b</c>, or <c>/</c> for the root).</summary>
    public static string Normalize(string? pointer) => Format(Parse(pointer));

    /// <summary>Escapes one segment (<c>~</c> to <c>~0</c>, <c>/</c> to <c>~1</c>).</summary>
    public static string Escape(string segment)
        => segment.IndexOfAny(['~', '/']) < 0 ? segment : segment.Replace("~", "~0").Replace("/", "~1");

    /// <summary>Unescapes one segment (<c>~1</c> to <c>/</c> first, then <c>~0</c> to <c>~</c>).</summary>
    public static string Unescape(string segment)
        => segment.IndexOf('~') < 0 ? segment : segment.Replace("~1", "/").Replace("~0", "~");

    /// <summary>
    /// Resolves <paramref name="path"/> against a context path: absolute paths (leading <c>/</c>) stand alone; relative
    /// ones are appended to the context; <c>""</c> and <c>"."</c> are the context itself.
    /// </summary>
    public static string Resolve(string? contextPath, string? path)
    {
        if (path is not null && path.StartsWith('/'))
        {
            return Normalize(path);
        }

        var context = Normalize(contextPath);
        if (string.IsNullOrEmpty(path) || path == ".")
        {
            return context;
        }

        return Normalize(context == "/" ? "/" + path : context + "/" + path);
    }

    /// <summary>True when <paramref name="segment"/> is a list index: digits only, no leading zero.</summary>
    public static bool TryParseIndex(string segment, out int index)
    {
        index = -1;
        if (segment.Length == 0 || segment.Length > 9)
        {
            return false;
        }

        if (segment.Length > 1 && segment[0] == '0')
        {
            return false;
        }

        foreach (var c in segment)
        {
            if (c < '0' || c > '9')
            {
                return false;
            }
        }

        index = int.Parse(segment, System.Globalization.CultureInfo.InvariantCulture);
        return true;
    }

    /// <summary>True when <paramref name="ancestor"/> equals <paramref name="path"/> or contains it (both normalized).</summary>
    public static bool IsAncestorOrSelf(string ancestor, string path)
    {
        if (ancestor == "/")
        {
            return true;
        }

        return path.Length >= ancestor.Length && path.StartsWith(ancestor, StringComparison.Ordinal)
               && (path.Length == ancestor.Length || path[ancestor.Length] == '/');
    }
}
