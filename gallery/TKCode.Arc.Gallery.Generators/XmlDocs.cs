using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;

namespace TKCode.Arc.Gallery.Generators;

/// <summary>
/// The <c>&lt;summary&gt;</c> texts of an XML documentation file, as one-line Markdown. Command-line builds do not
/// load the documentation of referenced assemblies (<see cref="ISymbol.GetDocumentationCommentXml"/> is empty there),
/// so the TKCode.Arc documentation files arrive as additional files.
/// </summary>
internal sealed class XmlDocs
{
    private static readonly Regex s_whitespace = new(@"\s+");

    private readonly Dictionary<string, string> _summaries;

    private XmlDocs(Dictionary<string, string> summaries)
    {
        _summaries = summaries;
    }

    public static XmlDocs Empty { get; } = new(new Dictionary<string, string>());

    public static XmlDocs Parse(string? text)
    {
        var summaries = new Dictionary<string, string>(System.StringComparer.Ordinal);
        if (string.IsNullOrEmpty(text))
        {
            return new XmlDocs(summaries);
        }
        try
        {
            var document = XDocument.Parse(text);
            foreach (var member in document.Descendants("member"))
            {
                if (member.Attribute("name")?.Value is { Length: > 0 } id && member.Element("summary") is { } summary)
                {
                    summaries[id] = ToMarkdown(summary);
                }
            }
        }
        catch (XmlException)
        {
        }
        return new XmlDocs(summaries);
    }

    public static XmlDocs Merge(IEnumerable<XmlDocs> files)
    {
        var summaries = new Dictionary<string, string>(System.StringComparer.Ordinal);
        foreach (var file in files)
        {
            foreach (var pair in file._summaries)
            {
                summaries[pair.Key] = pair.Value;
            }
        }
        return new XmlDocs(summaries);
    }

    public bool IsEmpty => _summaries.Count == 0;

    /// <summary>The summary of the first symbol that has a non-empty one.</summary>
    public string? Summary(params ISymbol?[] symbols)
    {
        foreach (var symbol in symbols)
        {
            if (symbol?.GetDocumentationCommentId() is { } id && _summaries.TryGetValue(id, out var summary) && summary.Length > 0)
            {
                return summary;
            }
        }
        return null;
    }

    /// <summary>Collapses whitespace so XML indentation and translations compare equal.</summary>
    public static string Collapse(string text) => s_whitespace.Replace(text, " ").Trim();

    private static string ToMarkdown(XElement summary)
    {
        var builder = new StringBuilder();
        Append(builder, summary.Nodes());
        return Collapse(builder.ToString());
    }

    private static void Append(StringBuilder builder, IEnumerable<XNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node is XText text)
            {
                builder.Append(text.Value);
            }
            else if (node is XElement element)
            {
                AppendElement(builder, element);
            }
        }
    }

    private static void AppendElement(StringBuilder builder, XElement element)
    {
        switch (element.Name.LocalName)
        {
            case "see":
            case "seealso":
                if (element.Attribute("langword")?.Value is { } word)
                {
                    builder.Append('`').Append(word).Append('`');
                }
                else if (element.Attribute("href")?.Value is { } href)
                {
                    var label = element.Value.Trim();
                    builder.Append('[').Append(label.Length > 0 ? label : href).Append("](").Append(href).Append(')');
                }
                else if (element.Value.Trim() is { Length: > 0 } inner)
                {
                    builder.Append(inner);
                }
                else if (element.Attribute("cref")?.Value is { } cref)
                {
                    builder.Append('`').Append(CrefName(cref)).Append('`');
                }
                break;
            case "c":
                builder.Append('`').Append(element.Value).Append('`');
                break;
            case "paramref":
            case "typeparamref":
                builder.Append('`').Append(element.Attribute("name")?.Value).Append('`');
                break;
            case "code":
                // Samples belong in the page, not in a table cell.
                break;
            case "para":
            case "br":
                builder.Append(' ');
                Append(builder, element.Nodes());
                builder.Append(' ');
                break;
            default:
                Append(builder, element.Nodes());
                break;
        }
    }

    /// <summary><c>P:TKCode.Arc.Field.Label</c> → <c>Label</c>; <c>T:System.Collections.Generic.List`1</c> → <c>List</c>.</summary>
    private static string CrefName(string cref)
    {
        var name = cref.Length > 2 && cref[1] == ':' ? cref.Substring(2) : cref;
        var paren = name.IndexOf('(');
        if (paren >= 0)
        {
            name = name.Substring(0, paren);
        }
        name = name.Split('.').Last();
        var tick = name.IndexOf('`');
        return tick >= 0 ? name.Substring(0, tick) : name;
    }
}
