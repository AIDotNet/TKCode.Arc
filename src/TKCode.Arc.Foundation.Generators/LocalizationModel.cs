using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace TKCode.Arc.Foundation.Generators
{
    /// <summary>A <c>[LanguageCatalog]</c> enum.</summary>
    internal sealed class CatalogModel : IEquatable<CatalogModel>
    {
        private CatalogModel(string fullName, string? ns, string name, bool isPublic, bool isNested,
                             EquatableArray<string> members, EquatableArray<string> values, string? filePath, TextSpan span,
                             LinePositionSpan lineSpan)
        {
            FullName = fullName;
            Namespace = ns;
            Name = name;
            IsPublic = isPublic;
            IsNested = isNested;
            Members = members;
            Values = values;
            FilePath = filePath;
            Span = span;
            LineSpan = lineSpan;
        }

        /// <summary>Metadata-style full name (<c>Ns.Name</c>), the xlf <c>file id</c>.</summary>
        public string FullName { get; }

        public string? Namespace { get; }
        public string Name { get; }
        public bool IsPublic { get; }
        public bool IsNested { get; }

        /// <summary>Member names in declaration order.</summary>
        public EquatableArray<string> Members { get; }

        /// <summary>The members' constant values, aligned with <see cref="Members"/>.</summary>
        public EquatableArray<string> Values { get; }

        public string? FilePath { get; }
        public TextSpan Span { get; }
        public LinePositionSpan LineSpan { get; }

        /// <summary><c>DataGridLangResourceKind</c> → <c>DataGridLangResource</c>.</summary>
        public string BaseName => Name.EndsWith("Kind", StringComparison.Ordinal) && Name.Length > 4
            ? Name.Substring(0, Name.Length - 4)
            : Name;

        public string ExtensionName => BaseName + "Extension";

        public string ResourcesName => BaseName + "s";

        public static CatalogModel Create(INamedTypeSymbol symbol)
        {
            var ns = symbol.ContainingNamespace is { IsGlobalNamespace: false } containing ? containing.ToDisplayString() : null;
            var fields = symbol.GetMembers().OfType<IFieldSymbol>().Where(f => f.HasConstantValue).ToList();
            var isPublic = symbol.DeclaredAccessibility == Accessibility.Public;
            var location = symbol.Locations.FirstOrDefault();
            var lineSpan = location?.GetLineSpan();
            return new CatalogModel((ns is null ? "" : ns + ".") + symbol.Name, ns, symbol.Name, isPublic,
                symbol.ContainingType is not null,
                new EquatableArray<string>(fields.Select(f => f.Name)),
                new EquatableArray<string>(fields.Select(f => Convert.ToString(f.ConstantValue, System.Globalization.CultureInfo.InvariantCulture) ?? "")),
                location?.SourceTree is null ? null : lineSpan?.Path, location?.SourceSpan ?? default, lineSpan?.Span ?? default);
        }

        public Diagnostic CreateDiagnostic(DiagnosticDescriptor descriptor, params object[] arguments)
        {
            var location = FilePath is null ? Location.None : Location.Create(FilePath, Span, LineSpan);
            return Diagnostic.Create(descriptor, location, arguments);
        }

        public bool Equals(CatalogModel? other)
            => other is not null && FullName == other.FullName && IsPublic == other.IsPublic && IsNested == other.IsNested
               && Members.Equals(other.Members) && Values.Equals(other.Values) && FilePath == other.FilePath
               && Span.Equals(other.Span);

        public override bool Equals(object? obj) => Equals(obj as CatalogModel);

        public override int GetHashCode() => FullName.GetHashCode();
    }

    /// <summary>One <c>&lt;unit&gt;</c> of an xlf file.</summary>
    internal sealed class XlfUnit
    {
        public XlfUnit(string id, string? source, string? target, int line)
        {
            Id = id;
            Source = source;
            Target = target;
            Line = line;
        }

        public string Id { get; }
        public string? Source { get; }
        public string? Target { get; }

        /// <summary>One-based line of the unit.</summary>
        public int Line { get; }
    }

    /// <summary>One <c>&lt;file&gt;</c> of an xlf file (the text of one catalog).</summary>
    internal sealed class XlfSection
    {
        public XlfSection(string id, int line, List<XlfUnit> units)
        {
            Id = id;
            Line = line;
            Units = units;
        }

        public string Id { get; }
        public int Line { get; }
        public List<XlfUnit> Units { get; }
    }

    /// <summary>
    /// A parsed <c>Localization/&lt;language&gt;.xlf</c> file (XLIFF 2). The file name is its language. A file without
    /// <c>trgLang</c> (or with <c>trgLang</c> = <c>srcLang</c>) is a source file: its text is the <c>&lt;source&gt;</c>;
    /// any other file's text is the <c>&lt;target&gt;</c>.
    /// </summary>
    internal sealed class XlfFile : IEquatable<XlfFile>
    {
        private const string Xliff2Namespace = "urn:oasis:names:tc:xliff:document:2.0";

        private XlfFile(string path, string relativePath, string text)
        {
            Path = path;
            RelativePath = relativePath;
            Text = text;
        }

        public string Path { get; }
        public string RelativePath { get; }

        /// <summary>The raw text; equality compares it, so an edit re-runs the generator and nothing else does.</summary>
        public string Text { get; }

        /// <summary>The canonical language from the file name.</summary>
        public string? Language { get; private set; }

        public string? SourceLanguage { get; private set; }

        public bool IsSource { get; private set; }

        /// <summary>Why the file cannot be used, or null.</summary>
        public string? Error { get; private set; }

        public int ErrorLine { get; private set; } = 1;

        public List<XlfSection> Sections { get; } = new List<XlfSection>();

        /// <summary>Whether <paramref name="path"/> is an xlf file directly inside a <c>Localization</c> folder.</summary>
        public static bool IsLanguageFile(string path)
        {
            if (!path.EndsWith(".xlf", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            var directory = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(path) ?? "");
            return string.Equals(directory, "Localization", StringComparison.Ordinal);
        }

        public static XlfFile Parse(string path, string relativePath, SourceText? text)
        {
            var file = new XlfFile(path, relativePath, text?.ToString() ?? "");
            file.Read();
            return file;
        }

        private void Read()
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(Path);
            Language = CanonicalTag(name);
            if (Language is null)
            {
                Error = $"the file name '{name}' is not a language tag (name it after its language, for example zh-CN.xlf)";
                return;
            }
            XDocument document;
            try
            {
                document = XDocument.Parse(Text, LoadOptions.SetLineInfo);
            }
            catch (XmlException e)
            {
                Error = e.Message;
                ErrorLine = Math.Max(1, e.LineNumber);
                return;
            }
            var root = document.Root!;
            XNamespace ns = Xliff2Namespace;
            var version = (string?)root.Attribute("version");
            if (root.Name != ns + "xliff" || version is null || !version.StartsWith("2.", StringComparison.Ordinal))
            {
                Error = $"the root element must be <xliff version=\"2.x\" xmlns=\"{Xliff2Namespace}\">";
                return;
            }
            var source = CanonicalTag((string?)root.Attribute("srcLang"));
            if (source is null)
            {
                Error = "<xliff> needs a srcLang that is a language tag";
                return;
            }
            SourceLanguage = source;
            var targetText = (string?)root.Attribute("trgLang");
            var target = targetText is null ? null : CanonicalTag(targetText);
            if (targetText is not null && target is null)
            {
                Error = $"trgLang '{targetText}' is not a language tag";
                return;
            }
            IsSource = target is null || target == source;
            var declared = target ?? source;
            if (declared != Language)
            {
                Error = $"the file is named '{name}' but declares the language '{declared}' ({(target is null ? "srcLang" : "trgLang")})";
                return;
            }
            foreach (var fileElement in root.Elements(ns + "file"))
            {
                var id = (string?)fileElement.Attribute("id");
                if (string.IsNullOrEmpty(id))
                {
                    Error = "a <file> has no id (the catalog enum's full name)";
                    ErrorLine = LineOf(fileElement);
                    return;
                }
                var units = new List<XlfUnit>();
                foreach (var unit in fileElement.Descendants(ns + "unit"))
                {
                    var unitId = (string?)unit.Attribute("id");
                    if (string.IsNullOrEmpty(unitId))
                    {
                        Error = "a <unit> has no id (the catalog member name)";
                        ErrorLine = LineOf(unit);
                        return;
                    }
                    var segments = unit.Elements(ns + "segment").ToList();
                    units.Add(new XlfUnit(unitId!, Join(segments, ns + "source"), Join(segments, ns + "target"), LineOf(unit)));
                }
                Sections.Add(new XlfSection(id!, LineOf(fileElement), units));
            }
        }

        private static string? Join(List<XElement> segments, XName part)
        {
            StringBuilder? text = null;
            foreach (var segment in segments)
            {
                var element = segment.Element(part);
                if (element is null)
                {
                    continue;
                }
                text ??= new StringBuilder();
                text.Append(element.Value);
            }
            return text?.ToString();
        }

        private static int LineOf(XObject node) => node is IXmlLineInfo info && info.HasLineInfo() ? info.LineNumber : 1;

        /// <summary>The canonical form of a BCP 47 tag (the runtime's <c>LanguageTag</c> rules), or null.</summary>
        public static string? CanonicalTag(string? tag)
        {
            if (tag is null || tag.Trim().Length == 0)
            {
                return null;
            }
            var parts = tag.Trim().Split('-', '_');
            var builder = new StringBuilder();
            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                if (part.Length == 0 || part.Length > 8 || !part.All(c => c < 128 && char.IsLetterOrDigit(c)))
                {
                    return null;
                }
                if (i == 0)
                {
                    if (part.Length < 2 || !part.All(char.IsLetter))
                    {
                        return null;
                    }
                    builder.Append(part.ToLowerInvariant());
                    continue;
                }
                builder.Append('-');
                if (i == 1 && part.Length == 4 && part.All(char.IsLetter))
                {
                    builder.Append(char.ToUpperInvariant(part[0])).Append(part.Substring(1).ToLowerInvariant());
                }
                else if ((part.Length == 2 && part.All(char.IsLetter)) || (part.Length == 3 && part.All(char.IsDigit)))
                {
                    builder.Append(part.ToUpperInvariant());
                }
                else
                {
                    builder.Append(part.ToLowerInvariant());
                }
            }
            return builder.ToString();
        }

        public Location LocationAt(int line)
        {
            var position = new LinePosition(Math.Max(0, line - 1), 0);
            return Location.Create(Path, default, new LinePositionSpan(position, position));
        }

        public bool Equals(XlfFile? other) => other is not null && Path == other.Path && RelativePath == other.RelativePath && Text == other.Text;

        public override bool Equals(object? obj) => Equals(obj as XlfFile);

        public override int GetHashCode() => Path.GetHashCode();
    }
}
