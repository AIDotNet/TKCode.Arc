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
                             string? sourceLanguage, EquatableArray<string> members, EquatableArray<string> values,
                             string? filePath, TextSpan span, LinePositionSpan lineSpan)
        {
            FullName = fullName;
            Namespace = ns;
            Name = name;
            IsPublic = isPublic;
            IsNested = isNested;
            SourceLanguage = sourceLanguage;
            Members = members;
            Values = values;
            FilePath = filePath;
            Span = span;
            LineSpan = lineSpan;
        }

        /// <summary>Metadata-style full name (<c>Ns.Name</c>), the catalog id.</summary>
        public string FullName { get; }

        public string? Namespace { get; }
        public string Name { get; }
        public bool IsPublic { get; }
        public bool IsNested { get; }

        /// <summary>Optional raw <c>[LanguageCatalog(SourceLanguage = ...)]</c> value.</summary>
        public string? SourceLanguage { get; }

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

        public static CatalogModel Create(INamedTypeSymbol symbol, AttributeData? attribute = null)
        {
            var ns = symbol.ContainingNamespace is { IsGlobalNamespace: false } containing ? containing.ToDisplayString() : null;
            var fields = symbol.GetMembers().OfType<IFieldSymbol>().Where(f => f.HasConstantValue).ToList();
            var isPublic = symbol.DeclaredAccessibility == Accessibility.Public;
            var location = symbol.Locations.FirstOrDefault();
            var lineSpan = location?.GetLineSpan();
            string? sourceLanguage = null;
            if (attribute is not null)
            {
                foreach (var arg in attribute.NamedArguments)
                {
                    if (arg.Key == "SourceLanguage" && arg.Value.Value is string value)
                    {
                        sourceLanguage = value;
                    }
                }
            }
            return new CatalogModel((ns is null ? "" : ns + ".") + symbol.Name, ns, symbol.Name, isPublic,
                symbol.ContainingType is not null, sourceLanguage,
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
               && SourceLanguage == other.SourceLanguage && Members.Equals(other.Members) && Values.Equals(other.Values)
               && FilePath == other.FilePath && Span.Equals(other.Span);

        public override bool Equals(object? obj) => Equals(obj as CatalogModel);

        public override int GetHashCode() => FullName.GetHashCode();
    }

    internal enum LanguageFileFormat
    {
        Xlf,
        Json
    }

    /// <summary>One translated entry (<c>&lt;unit&gt;</c> in XLIFF or a key-value pair in JSON).</summary>
    internal sealed class LanguageUnit
    {
        public LanguageUnit(string id, string? text, int line)
        {
            Id = id;
            Text = text;
            Line = line;
        }

        public string Id { get; }
        public string? Text { get; }

        /// <summary>One-based line of the entry.</summary>
        public int Line { get; }
    }

    /// <summary>One catalog's text inside a language file (<c>Id</c> is null for a flat JSON file without <c>$catalog</c>).</summary>
    internal sealed class LanguageSection
    {
        public LanguageSection(string? id, int line, List<LanguageUnit> units)
        {
            Id = id;
            Line = line;
            Units = units;
        }

        public string? Id { get; }
        public int Line { get; }
        public List<LanguageUnit> Units { get; }
    }

    /// <summary>
    /// A parsed <c>Localization/&lt;language&gt;.xlf</c> (XLIFF 2) or <c>Localization/&lt;language&gt;.json</c> file.
    /// The file name is its language.
    /// </summary>
    internal sealed class LanguageFile : IEquatable<LanguageFile>
    {
        private const string Xliff2Namespace = "urn:oasis:names:tc:xliff:document:2.0";

        private LanguageFile(string path, string relativePath, string text, LanguageFileFormat format)
        {
            Path = path;
            RelativePath = relativePath;
            Text = text;
            Format = format;
        }

        public string Path { get; }
        public string RelativePath { get; }

        /// <summary>The raw text; equality compares it, so an edit re-runs the generator and nothing else does.</summary>
        public string Text { get; }

        public LanguageFileFormat Format { get; }

        /// <summary>The canonical language from the file name.</summary>
        public string? Language { get; private set; }

        /// <summary>The declared source language (<c>srcLang</c> in XLIFF or <c>$sourceLanguage</c> in JSON), or null if unspecified.</summary>
        public string? SourceLanguage { get; private set; }

        /// <summary>
        /// Explicit source-file marker from the file format: for XLIFF, true when <c>trgLang</c> is absent or equals
        /// <c>srcLang</c>, false otherwise; for JSON, null (resolved by matching <see cref="Language"/> against the catalog's source language).
        /// </summary>
        public bool? IsSourceExplicit { get; private set; }

        /// <summary>Why the file cannot be used, or null.</summary>
        public string? Error { get; private set; }

        public int ErrorLine { get; private set; } = 1;

        public List<LanguageSection> Sections { get; } = new List<LanguageSection>();

        /// <summary>Whether <paramref name="path"/> is an xlf or json file directly inside a <c>Localization</c> folder.</summary>
        public static bool IsLanguageFile(string path)
        {
            if (!path.EndsWith(".xlf", StringComparison.OrdinalIgnoreCase)
                && !path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            var directory = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(path) ?? "");
            return string.Equals(directory, "Localization", StringComparison.Ordinal);
        }

        public static LanguageFile Parse(string path, string relativePath, SourceText? text)
        {
            var format = path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                ? LanguageFileFormat.Json
                : LanguageFileFormat.Xlf;
            var file = new LanguageFile(path, relativePath, text?.ToString() ?? "", format);
            if (format == LanguageFileFormat.Json)
            {
                file.ReadJson();
            }
            else
            {
                file.ReadXlf();
            }
            return file;
        }

        private void ReadXlf()
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
            var isSource = target is null || target == source;
            IsSourceExplicit = isSource;
            var declared = target ?? source;
            if (declared != Language)
            {
                Error = $"the file is named '{name}' but declares the language '{declared}' ({(target is null ? "srcLang" : "trgLang")})";
                return;
            }
            var textPart = isSource ? ns + "source" : ns + "target";
            foreach (var fileElement in root.Elements(ns + "file"))
            {
                var id = (string?)fileElement.Attribute("id");
                if (string.IsNullOrEmpty(id))
                {
                    Error = "a <file> has no id (the catalog enum's full name)";
                    ErrorLine = LineOf(fileElement);
                    return;
                }
                var units = new List<LanguageUnit>();
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
                    units.Add(new LanguageUnit(unitId!, Join(segments, textPart), LineOf(unit)));
                }
                Sections.Add(new LanguageSection(id!, LineOf(fileElement), units));
            }
        }

        private void ReadJson()
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(Path);
            Language = CanonicalTag(name);
            if (Language is null)
            {
                Error = $"the file name '{name}' is not a language tag (name it after its language, for example zh-CN.json)";
                return;
            }
            if (!LocalizationJsonParser.TryParse(Text, out var root, out var parseError, out var parseErrorLine))
            {
                Error = parseError;
                ErrorLine = parseErrorLine;
                return;
            }
            if (root!.Kind != JsonValueKind.Object)
            {
                Error = "the root JSON value must be an object";
                ErrorLine = root.Line;
                return;
            }

            string? explicitCatalog = null;
            var explicitCatalogLine = root.Line;
            var hasSchema = false;
            var hasDeclaredLanguage = false;
            var entries = new List<JsonProperty>();

            foreach (var prop in root.Properties!)
            {
                if (prop.Name.StartsWith("$", StringComparison.Ordinal))
                {
                    switch (prop.Name)
                    {
                        case "$schema":
                            if (hasSchema)
                            {
                                Error = "duplicate '$schema' metadata property";
                                ErrorLine = prop.Line;
                                return;
                            }
                            if (prop.Value.Kind != JsonValueKind.String)
                            {
                                Error = "'$schema' must be a string";
                                ErrorLine = prop.Line;
                                return;
                            }
                            hasSchema = true;
                            break;
                        case "$sourceLanguage":
                        case "$srcLang":
                            if (SourceLanguage is not null)
                            {
                                Error = $"duplicate source language metadata property '{prop.Name}'";
                                ErrorLine = prop.Line;
                                return;
                            }
                            if (prop.Value.Kind != JsonValueKind.String)
                            {
                                Error = $"'{prop.Name}' must be a language tag string";
                                ErrorLine = prop.Line;
                                return;
                            }
                            var src = CanonicalTag(prop.Value.StringValue);
                            if (src is null)
                            {
                                Error = $"{prop.Name} '{prop.Value.StringValue}' is not a language tag";
                                ErrorLine = prop.Line;
                                return;
                            }
                            SourceLanguage = src;
                            IsSourceExplicit = Language == src;
                            break;
                        case "$language":
                        case "$lang":
                            if (hasDeclaredLanguage)
                            {
                                Error = $"duplicate language metadata property '{prop.Name}'";
                                ErrorLine = prop.Line;
                                return;
                            }
                            if (prop.Value.Kind != JsonValueKind.String)
                            {
                                Error = $"'{prop.Name}' must be a language tag string";
                                ErrorLine = prop.Line;
                                return;
                            }
                            var declared = CanonicalTag(prop.Value.StringValue);
                            if (declared is null)
                            {
                                Error = $"{prop.Name} '{prop.Value.StringValue}' is not a language tag";
                                ErrorLine = prop.Line;
                                return;
                            }
                            if (declared != Language)
                            {
                                Error = $"the file is named '{name}' but declares the language '{declared}' ({prop.Name})";
                                ErrorLine = prop.Line;
                                return;
                            }
                            hasDeclaredLanguage = true;
                            break;
                        case "$catalog":
                            if (explicitCatalog is not null)
                            {
                                Error = "duplicate '$catalog' metadata property";
                                ErrorLine = prop.Line;
                                return;
                            }
                            if (prop.Value.Kind != JsonValueKind.String || string.IsNullOrEmpty(prop.Value.StringValue))
                            {
                                Error = "'$catalog' must be a non-empty catalog enum full name";
                                ErrorLine = prop.Line;
                                return;
                            }
                            explicitCatalog = prop.Value.StringValue;
                            explicitCatalogLine = prop.Line;
                            break;
                        default:
                            Error = $"unknown metadata property '{prop.Name}' (supported: $schema, $catalog, $language, $sourceLanguage)";
                            ErrorLine = prop.Line;
                            return;
                    }
                    continue;
                }
                entries.Add(prop);
            }

            if (explicitCatalog is not null)
            {
                var units = new List<LanguageUnit>(entries.Count);
                foreach (var entry in entries)
                {
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Error = "an entry key cannot be empty";
                        ErrorLine = entry.Line;
                        return;
                    }
                    if (entry.Value.Kind != JsonValueKind.String && entry.Value.Kind != JsonValueKind.Null)
                    {
                        Error = $"entry '{entry.Name}' must be a string or null";
                        ErrorLine = entry.Line;
                        return;
                    }
                    units.Add(new LanguageUnit(entry.Name, entry.Value.StringValue, entry.Line));
                }
                Sections.Add(new LanguageSection(explicitCatalog, explicitCatalogLine, units));
                return;
            }

            if (entries.Count == 0)
            {
                Sections.Add(new LanguageSection(null, root.Line, new List<LanguageUnit>()));
                return;
            }

            var hasObjects = false;
            var hasScalars = false;
            foreach (var entry in entries)
            {
                if (entry.Value.Kind == JsonValueKind.Object)
                {
                    hasObjects = true;
                }
                else if (entry.Value.Kind == JsonValueKind.String || entry.Value.Kind == JsonValueKind.Null)
                {
                    hasScalars = true;
                }
                else
                {
                    Error = $"property '{entry.Name}' must be a string (flat catalog) or an object of strings (multi-catalog)";
                    ErrorLine = entry.Line;
                    return;
                }
                if (hasObjects && hasScalars)
                {
                    Error = "cannot mix catalog objects and flat string entries at the root of a JSON language file";
                    ErrorLine = entry.Line;
                    return;
                }
            }

            if (hasObjects)
            {
                foreach (var sectionProp in entries)
                {
                    if (string.IsNullOrEmpty(sectionProp.Name))
                    {
                        Error = "a catalog key cannot be empty";
                        ErrorLine = sectionProp.Line;
                        return;
                    }
                    var childProps = sectionProp.Value.Properties!;
                    var units = new List<LanguageUnit>(childProps.Count);
                    foreach (var unitProp in childProps)
                    {
                        if (string.IsNullOrEmpty(unitProp.Name))
                        {
                            Error = $"an entry key in '{sectionProp.Name}' cannot be empty";
                            ErrorLine = unitProp.Line;
                            return;
                        }
                        if (unitProp.Value.Kind != JsonValueKind.String && unitProp.Value.Kind != JsonValueKind.Null)
                        {
                            Error = $"entry '{unitProp.Name}' in '{sectionProp.Name}' must be a string or null";
                            ErrorLine = unitProp.Line;
                            return;
                        }
                        units.Add(new LanguageUnit(unitProp.Name, unitProp.Value.StringValue, unitProp.Line));
                    }
                    Sections.Add(new LanguageSection(sectionProp.Name, sectionProp.Line, units));
                }
            }
            else
            {
                var units = new List<LanguageUnit>(entries.Count);
                foreach (var entry in entries)
                {
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Error = "an entry key cannot be empty";
                        ErrorLine = entry.Line;
                        return;
                    }
                    units.Add(new LanguageUnit(entry.Name, entry.Value.StringValue, entry.Line));
                }
                Sections.Add(new LanguageSection(null, root.Line, units));
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

        public bool Equals(LanguageFile? other) => other is not null && Path == other.Path && RelativePath == other.RelativePath && Text == other.Text;

        public override bool Equals(object? obj) => Equals(obj as LanguageFile);

        public override int GetHashCode() => Path.GetHashCode();
    }
}
