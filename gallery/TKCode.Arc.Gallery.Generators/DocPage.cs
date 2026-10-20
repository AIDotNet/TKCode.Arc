using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace TKCode.Arc.Gallery.Generators;

/// <summary>An API directive in a page: <c>&lt;!-- api: Type --&gt;</c> on a line of its own.</summary>
internal sealed class ApiDirective
{
    public ApiDirective(string typeName, Location location)
    {
        TypeName = typeName;
        Location = location;
    }

    public string TypeName { get; }

    public Location Location { get; }
}

/// <summary>
/// One component page in one language: <c>Docs/&lt;Category&gt;/&lt;ClassName&gt;/&lt;language&gt;.md</c> with a front
/// matter (<c>title</c>, <c>summary</c>, and in translations <c>source-hash</c>) and a Markdown body.
/// </summary>
internal sealed class DocPage
{
    public const string SourceLanguage = "en-US";

    // Shared with the gallery's GalleryDocs, which replaces the same lines at runtime.
    private static readonly Regex s_directive = new(@"^[ \t]*<!--[ \t]*api:[ \t]*([A-Za-z_][A-Za-z0-9_]*)[ \t]*-->[ \t]*$", RegexOptions.Multiline);
    private static readonly Regex s_language = new(@"^[a-z]{2,3}(-[A-Z][a-z]{3})?(-[A-Z]{2})?$");

    private DocPage(string path, string className, string language)
    {
        Path = path;
        ClassName = className;
        Language = language;
    }

    public string Path { get; }

    public string ClassName { get; }

    public string Language { get; }

    public string Title { get; private set; } = string.Empty;

    public string Summary { get; private set; } = string.Empty;

    /// <summary>The <c>source-hash</c> a translation was made from (null on en-US pages or when missing).</summary>
    public string? SourceHash { get; private set; }

    /// <summary>The hash of this file, which translations of an en-US page record as their <c>source-hash</c>.</summary>
    public string Hash { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    public List<ApiDirective> Directives { get; } = new();

    public List<Diagnostic> Diagnostics { get; } = new();

    public bool IsSource => Language == SourceLanguage;

    public static DocPage Parse(string path, string? text)
    {
        var className = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(path)) ?? string.Empty;
        var language = System.IO.Path.GetFileNameWithoutExtension(path);
        var page = new DocPage(path, className, language);
        var normalized = Normalize(text ?? string.Empty);
        page.Hash = HashOf(normalized);

        if (!s_language.IsMatch(language))
        {
            page.Report(0, $"'{System.IO.Path.GetFileName(path)}' should be named after a language tag, such as en-US.md or zh-CN.md");
        }

        var bodyStart = 0;
        if (normalized.StartsWith("---\n", System.StringComparison.Ordinal))
        {
            var end = normalized.IndexOf("\n---", 3, System.StringComparison.Ordinal);
            if (end >= 0 && (end + 4 == normalized.Length || normalized[end + 4] == '\n'))
            {
                page.ReadFrontMatter(normalized.Substring(4, System.Math.Max(0, end - 4)));
                bodyStart = System.Math.Min(normalized.Length, end + 5);
            }
            else
            {
                page.Report(0, "The front matter that starts on line 1 has no closing '---' line");
            }
        }
        if (page.Title.Length == 0 || page.Summary.Length == 0)
        {
            page.Report(0, "The front matter needs a 'title' and a 'summary'");
        }

        page.Body = normalized.Substring(bodyStart).TrimStart('\n');
        foreach (Match match in s_directive.Matches(normalized, bodyStart))
        {
            var line = LineOf(normalized, match.Index);
            page.Directives.Add(new ApiDirective(match.Groups[1].Value, page.LocationAt(line)));
        }
        return page;
    }

    private void ReadFrontMatter(string block)
    {
        foreach (var raw in block.Split('\n'))
        {
            var colon = raw.IndexOf(':');
            if (colon <= 0 || raw.TrimStart().StartsWith("#", System.StringComparison.Ordinal))
            {
                continue;
            }
            var key = raw.Substring(0, colon).Trim();
            var value = raw.Substring(colon + 1).Trim();
            if (value.Length >= 2 && (value[0] == '"' || value[0] == '\'') && value[value.Length - 1] == value[0])
            {
                value = value.Substring(1, value.Length - 2);
            }
            else if (value.IndexOf(" #", System.StringComparison.Ordinal) is var comment and >= 0)
            {
                value = value.Substring(0, comment).TrimEnd();
            }
            switch (key)
            {
                case "title":
                    Title = value;
                    break;
                case "summary":
                    Summary = value;
                    break;
                case "source-hash":
                    SourceHash = value;
                    break;
            }
        }
    }

    private void Report(int line, string message)
        => Diagnostics.Add(Diagnostic.Create(GalleryDocsDiagnostics.MalformedFile, LocationAt(line), message));

    private Location LocationAt(int line)
        => Location.Create(Path, default, new LinePositionSpan(new LinePosition(line, 0), new LinePosition(line, 0)));

    private static int LineOf(string text, int index)
    {
        var line = 0;
        for (var i = 0; i < index; i++)
        {
            if (text[i] == '\n')
            {
                line++;
            }
        }
        return line;
    }

    /// <summary>LF line endings and no trailing blank lines, so the hash does not depend on the checkout.</summary>
    public static string Normalize(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n').TrimEnd();

    /// <summary>The first 12 hex digits of the FNV-1a 64-bit hash of the UTF-8 text.</summary>
    public static string HashOf(string normalized)
    {
        var hash = 14695981039346656037UL;
        foreach (var b in Encoding.UTF8.GetBytes(normalized))
        {
            hash ^= b;
            hash *= 1099511628211UL;
        }
        return hash.ToString("x16").Substring(0, 12);
    }
}
