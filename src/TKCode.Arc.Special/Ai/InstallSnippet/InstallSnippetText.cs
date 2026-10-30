using System.Text;

namespace TKCode.Arc;

/// <summary>
/// The pure text work of an <see cref="InstallSnippet"/>: install commands per package manager, filling in the key,
/// guessing a highlighting language from a file name, and splitting a snippet's lines around its key slots.
/// </summary>
internal static class InstallSnippetText
{
    /// <summary>Where the API key goes in a snippet.</summary>
    public const string KeyToken = "{{key}}";

    /// <summary>The command that installs <paramref name="packages"/> with <paramref name="manager"/>.</summary>
    public static string InstallCommand(InstallSnippetPackageManager manager, string? packages)
    {
        var names = string.Join(' ', (packages ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var verb = manager switch
        {
            InstallSnippetPackageManager.Pnpm => "pnpm add",
            InstallSnippetPackageManager.Yarn => "yarn add",
            InstallSnippetPackageManager.Bun => "bun add",
            _ => "npm install",
        };
        return names.Length == 0 ? verb : verb + " " + names;
    }

    /// <summary>Line breaks as <c>\n</c>, tabs as four spaces and no trailing line break.</summary>
    public static string Normalize(string? code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return string.Empty;
        }
        return code.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Replace("\t", "    ", StringComparison.Ordinal)
            .TrimEnd('\n');
    }

    /// <summary>The snippet with every <see cref="KeyToken"/> replaced by <paramref name="key"/>: the text a copy puts on the clipboard.</summary>
    public static string Fill(string? template, string key) =>
        Normalize(template).Replace(KeyToken, key, StringComparison.Ordinal);

    /// <summary>Whether the snippet has a key slot.</summary>
    public static bool HasKey(string? template) =>
        template is not null && template.Contains(KeyToken, StringComparison.Ordinal);

    /// <summary>How many lines the snippet has (at least one).</summary>
    public static int LineCount(string? code)
    {
        var text = Normalize(code);
        var count = 1;
        foreach (var c in text)
        {
            if (c == '\n')
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// The highlighting language of a snippet: <paramref name="language"/> when set, else a guess from the file name
    /// (<c>Terminal</c> and dotenv files read as shell).
    /// </summary>
    public static string LanguageOf(string? fileName, string? language)
    {
        if (!string.IsNullOrWhiteSpace(language))
        {
            return language.Trim();
        }
        var name = (fileName ?? string.Empty).Trim();
        if (name.Length == 0 || name.Equals("Terminal", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Shell", StringComparison.OrdinalIgnoreCase) || name.StartsWith(".env", StringComparison.OrdinalIgnoreCase))
        {
            return "bash";
        }
        var dot = name.LastIndexOf('.');
        var extension = dot >= 0 ? name[(dot + 1)..].ToLowerInvariant() : string.Empty;
        return extension switch
        {
            "js" or "mjs" or "cjs" => "js",
            "ts" or "mts" or "cts" => "ts",
            "tsx" => "tsx",
            "jsx" => "jsx",
            "py" => "python",
            "go" => "go",
            "rb" => "ruby",
            "rs" => "rust",
            "cs" => "csharp",
            "html" or "htm" => "html",
            "vue" => "vue",
            "sh" or "bash" or "zsh" => "bash",
            "yml" or "yaml" => "yaml",
            "" => "bash",
            _ => extension,
        };
    }

    /// <summary>
    /// Splits one line of a snippet around its key slots. <paramref name="display"/> is the line with every slot holding
    /// <paramref name="placeholder"/> (what is highlighted); each segment is a range of it, a slot or text.
    /// </summary>
    public static List<InstallSnippetSegment> Split(string line, string placeholder, out string display)
    {
        var segments = new List<InstallSnippetSegment>();
        var builder = new StringBuilder(line.Length);
        var index = 0;
        while (index <= line.Length)
        {
            var at = line.IndexOf(KeyToken, index, StringComparison.Ordinal);
            var end = at < 0 ? line.Length : at;
            if (end > index)
            {
                segments.Add(new InstallSnippetSegment(builder.Length, end - index, false));
                builder.Append(line, index, end - index);
            }
            if (at < 0)
            {
                break;
            }
            segments.Add(new InstallSnippetSegment(builder.Length, placeholder.Length, true));
            builder.Append(placeholder);
            index = at + KeyToken.Length;
        }
        display = builder.ToString();
        return segments;
    }
}

/// <summary>A range of one displayed snippet line: plain text, or a key slot holding the placeholder.</summary>
internal readonly record struct InstallSnippetSegment(int Start, int Length, bool IsSlot)
{
    public int End => Start + Length;
}
