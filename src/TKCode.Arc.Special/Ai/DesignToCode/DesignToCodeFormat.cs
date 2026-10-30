using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// The pure rules of <see cref="DesignToCode"/>: what an accept list allows (MIME types such as <c>image/png</c>,
/// wildcards such as <c>image/*</c> and extensions such as <c>.png</c>, as in an input's accept attribute), how it and
/// the size limit read in the drop zone, and how many lines a piece of code has.
/// </summary>
internal static class DesignToCodeFormat
{
    private static readonly Dictionary<string, string> s_mimeByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".webp"] = "image/webp",
        [".gif"] = "image/gif",
        [".bmp"] = "image/bmp",
        [".svg"] = "image/svg+xml",
        [".avif"] = "image/avif",
    };

    private static readonly Dictionary<string, string> s_names = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/png"] = "PNG",
        ["image/jpeg"] = "JPEG",
        ["image/webp"] = "WebP",
        ["image/gif"] = "GIF",
        ["image/bmp"] = "BMP",
        ["image/svg+xml"] = "SVG",
        ["image/avif"] = "AVIF",
        ["image/*"] = "image",
    };

    /// <summary>The accept list's entries, trimmed and lower case.</summary>
    public static string[] Entries(string? accept) =>
        string.IsNullOrWhiteSpace(accept)
            ? []
            : accept.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(entry => entry.ToLowerInvariant()).ToArray();

    /// <summary>The MIME type a file name implies, or null when its extension is unknown.</summary>
    public static string? MimeOf(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return extension.Length > 0 && s_mimeByExtension.TryGetValue(extension, out var mime) ? mime : null;
    }

    /// <summary>Whether <paramref name="fileName"/> is allowed by <paramref name="accept"/> (an empty list allows anything).</summary>
    public static bool Accepts(string? accept, string fileName)
    {
        var entries = Entries(accept);
        if (entries.Length == 0)
        {
            return true;
        }
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var mime = MimeOf(fileName);
        foreach (var entry in entries)
        {
            if (entry.StartsWith('.'))
            {
                if (entry == extension)
                {
                    return true;
                }
            }
            else if (mime is not null)
            {
                if (entry == mime || (entry.EndsWith("/*", StringComparison.Ordinal) && mime.StartsWith(entry[..^1], StringComparison.Ordinal)))
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>The accept list as words: "PNG, JPEG or WebP".</summary>
    public static string TypesText(string? accept)
    {
        var names = new List<string>();
        foreach (var entry in Entries(accept))
        {
            string name;
            if (s_names.TryGetValue(entry, out var known))
            {
                name = known;
            }
            else if (entry.StartsWith('.'))
            {
                name = s_names.TryGetValue(MimeOf(entry) ?? string.Empty, out var byExtension) ? byExtension : entry[1..].ToUpperInvariant();
            }
            else
            {
                var slash = entry.IndexOf('/');
                name = (slash >= 0 ? entry[(slash + 1)..] : entry).ToUpperInvariant();
            }
            if (!names.Contains(name))
            {
                names.Add(name);
            }
        }
        return names.Count switch
        {
            0 => "image",
            1 => names[0],
            _ => string.Join(", ", names.Take(names.Count - 1)) + " or " + names[^1],
        };
    }

    /// <summary>A byte count as the drop zone reads it: "10 MB", "1.5 MB", "500 KB".</summary>
    public static string SizeText(long bytes)
    {
        const double kb = 1024;
        const double mb = kb * 1024;
        if (bytes >= mb)
        {
            return Trim(bytes / mb) + " MB";
        }
        if (bytes >= kb)
        {
            return Trim(bytes / kb) + " KB";
        }
        return bytes.ToString(CultureInfo.InvariantCulture) + " bytes";

        static string Trim(double value) => value.ToString(value >= 10 ? "0" : "0.#", CultureInfo.InvariantCulture);
    }

    /// <summary>The lines of <paramref name="code"/>, not counting the empty line after a final line break.</summary>
    public static int LineCount(string code)
    {
        if (code.Length == 0)
        {
            return 0;
        }
        var count = 1;
        foreach (var c in code)
        {
            if (c == '\n')
            {
                count++;
            }
        }
        return code[^1] == '\n' ? count - 1 : count;
    }
}
