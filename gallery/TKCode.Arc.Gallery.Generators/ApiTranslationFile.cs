using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace TKCode.Arc.Gallery.Generators;

/// <summary>One translated API description: <c>&lt;trans-unit id="FieldTextBox.Label"&gt;</c>.</summary>
internal sealed class ApiTranslationUnit
{
    public ApiTranslationUnit(string key, string source, string target, Location location)
    {
        Key = key;
        Source = source;
        Target = target;
        Location = location;
    }

    /// <summary><c>Type</c> for a type summary, <c>DeclaringType.Member</c> for a member.</summary>
    public string Key { get; }

    /// <summary>The English summary the translation was made from.</summary>
    public string Source { get; }

    public string Target { get; }

    public Location Location { get; }
}

/// <summary>
/// <c>Docs/Api/&lt;language&gt;.xlf</c> (XLIFF 1.2): translations of the API descriptions, keyed by type and member,
/// so a member that several components inherit is translated once. Each unit keeps the English it was made from in
/// <c>&lt;source&gt;</c>, which is how a changed summary is detected.
/// </summary>
internal sealed class ApiTranslationFile
{
    private ApiTranslationFile(string path, string language)
    {
        Path = path;
        Language = language;
    }

    public string Path { get; }

    public string Language { get; }

    public Dictionary<string, ApiTranslationUnit> Units { get; } = new(System.StringComparer.Ordinal);

    public List<Diagnostic> Diagnostics { get; } = new();

    public static ApiTranslationFile Parse(string path, string? text)
    {
        var file = new ApiTranslationFile(path, System.IO.Path.GetFileNameWithoutExtension(path));
        try
        {
            var document = XDocument.Parse(text ?? string.Empty, LoadOptions.SetLineInfo);
            foreach (var unit in document.Descendants())
            {
                if (unit.Name.LocalName != "trans-unit")
                {
                    continue;
                }
                var key = unit.Attribute("id")?.Value ?? string.Empty;
                var source = XmlDocs.Collapse(Child(unit, "source"));
                var target = XmlDocs.Collapse(Child(unit, "target"));
                var location = file.LocationOf(unit);
                if (key.Length == 0)
                {
                    file.Diagnostics.Add(Diagnostic.Create(GalleryDocsDiagnostics.MalformedFile, location, "A trans-unit has no id"));
                }
                else if (target.Length > 0)
                {
                    file.Units[key] = new ApiTranslationUnit(key, source, target, location);
                }
            }
        }
        catch (XmlException exception)
        {
            file.Diagnostics.Add(Diagnostic.Create(GalleryDocsDiagnostics.MalformedFile,
                file.LocationAt(System.Math.Max(0, exception.LineNumber - 1)), $"Invalid XLIFF: {exception.Message}"));
        }
        return file;
    }

    private static string Child(XElement unit, string name)
    {
        foreach (var child in unit.Elements())
        {
            if (child.Name.LocalName == name)
            {
                return child.Value;
            }
        }
        return string.Empty;
    }

    private Location LocationOf(XElement element)
        => LocationAt(element is IXmlLineInfo info && info.HasLineInfo() ? info.LineNumber - 1 : 0);

    private Location LocationAt(int line)
        => Location.Create(Path, default, new LinePositionSpan(new LinePosition(line, 0), new LinePosition(line, 0)));
}
