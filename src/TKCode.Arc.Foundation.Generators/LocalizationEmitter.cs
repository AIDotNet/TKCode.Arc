using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace TKCode.Arc.Foundation.Generators
{
    /// <summary>
    /// Checks the <c>[LanguageCatalog]</c> enums against their <c>Localization/*.xlf</c> and <c>Localization/*.json</c>
    /// files and generates, per catalog, <c>XxxLangResources</c> (the reflection-free text tables and C# lookup) and
    /// <c>XxxLangResourceExtension</c>.
    /// </summary>
    internal static class LocalizationEmitter
    {
        private const string Localization = "global::TKCode.Arc.Localization.";

        /// <summary>Reports diagnostics, adds the catalog sources and returns the generated resources classes (global names).</summary>
        public static List<string> Execute(SourceProductionContext context, ImmutableArray<CatalogModel> catalogModels,
                                           ImmutableArray<LanguageFile> files)
        {
            var generated = new List<string>();
            if (catalogModels.IsDefaultOrEmpty && files.IsDefaultOrEmpty)
            {
                return generated;
            }

            var catalogs = new Dictionary<string, (CatalogModel Model, string? SourceLanguage)>(StringComparer.Ordinal);
            foreach (var catalog in catalogModels.OrderBy(c => c.FullName, StringComparer.Ordinal))
            {
                if (catalog.IsNested)
                {
                    context.ReportDiagnostic(catalog.CreateDiagnostic(LocalizationDiagnostics.InvalidCatalog, catalog.FullName,
                        "is nested in a type; declare it at namespace level"));
                    continue;
                }
                string? declaredSource = null;
                if (catalog.SourceLanguage is not null)
                {
                    declaredSource = LanguageFile.CanonicalTag(catalog.SourceLanguage);
                    if (declaredSource is null)
                    {
                        context.ReportDiagnostic(catalog.CreateDiagnostic(LocalizationDiagnostics.InvalidCatalog, catalog.FullName,
                            $"declares invalid SourceLanguage '{catalog.SourceLanguage}' (must be a BCP 47 language tag, for example 'en-US')"));
                        continue;
                    }
                }
                var byValue = new Dictionary<string, string>(StringComparer.Ordinal);
                string? clash = null;
                for (var i = 0; i < catalog.Members.Count && clash is null; i++)
                {
                    if (byValue.TryGetValue(catalog.Values[i], out var other))
                    {
                        clash = $"gives '{other}' and '{catalog.Members[i]}' the same value; each member is a resource key and needs its own value";
                    }
                    else
                    {
                        byValue.Add(catalog.Values[i], catalog.Members[i]);
                    }
                }
                if (clash is not null)
                {
                    context.ReportDiagnostic(catalog.CreateDiagnostic(LocalizationDiagnostics.InvalidCatalog, catalog.FullName, clash));
                    continue;
                }
                catalogs[catalog.FullName] = (catalog, declaredSource);
            }

            // Catalog → language → the file section holding that language's text.
            var sections = new Dictionary<string, SortedDictionary<string, (LanguageFile File, LanguageSection Section)>>(StringComparer.Ordinal);
            foreach (var file in files.OrderBy(f => f.RelativePath, StringComparer.Ordinal))
            {
                if (file.Error is not null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(LocalizationDiagnostics.InvalidLanguageFile,
                        file.LocationAt(file.ErrorLine), file.RelativePath, file.Error));
                    continue;
                }
                foreach (var section in file.Sections)
                {
                    string catalogId;
                    if (section.Id is not null)
                    {
                        catalogId = section.Id;
                        if (!catalogs.ContainsKey(catalogId))
                        {
                            context.ReportDiagnostic(Diagnostic.Create(LocalizationDiagnostics.UnknownCatalog,
                                file.LocationAt(section.Line), file.RelativePath, catalogId));
                            continue;
                        }
                    }
                    else
                    {
                        var resolved = ResolveFlatCatalog(context, file, section, catalogs);
                        if (resolved is null)
                        {
                            continue;
                        }
                        catalogId = resolved;
                    }
                    if (!sections.TryGetValue(catalogId, out var languages))
                    {
                        languages = new SortedDictionary<string, (LanguageFile, LanguageSection)>(StringComparer.Ordinal);
                        sections.Add(catalogId, languages);
                    }
                    if (languages.TryGetValue(file.Language!, out var existing))
                    {
                        var message = ReferenceEquals(existing.File, file)
                            ? $"'{file.RelativePath}' defines the {file.Language} text of '{catalogId}' twice"
                            : $"'{existing.File.RelativePath}' and '{file.RelativePath}' both hold the {file.Language} text of '{catalogId}'";
                        context.ReportDiagnostic(Diagnostic.Create(LocalizationDiagnostics.Duplicate, file.LocationAt(section.Line), message));
                        continue;
                    }
                    languages.Add(file.Language!, (file, section));
                }
            }

            foreach (var (catalog, declaredSource) in catalogs.Values)
            {
                sections.TryGetValue(catalog.FullName, out var languages);
                languages ??= new SortedDictionary<string, (LanguageFile, LanguageSection)>(StringComparer.Ordinal);
                var tables = Check(context, catalog, declaredSource, languages, out var sourceLanguage);
                var className = (catalog.Namespace is null ? "global::" : "global::" + catalog.Namespace + ".") + catalog.ResourcesName;
                context.AddSource(catalog.FullName + ".Localization.g.cs",
                    SourceText.From(EmitCatalog(catalog, sourceLanguage, tables), Encoding.UTF8));
                generated.Add(className);
            }
            return generated;
        }

        private static string? ResolveFlatCatalog(
            SourceProductionContext context, LanguageFile file, LanguageSection section,
            Dictionary<string, (CatalogModel Model, string? SourceLanguage)> catalogs)
        {
            if (catalogs.Count == 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(LocalizationDiagnostics.UnknownCatalog,
                    file.LocationAt(section.Line), file.RelativePath, "(flat JSON)"));
                return null;
            }

            var fileDir = NormalizeDirectory(Path.GetDirectoryName(file.Path));
            if (fileDir.Length > 0)
            {
                var sameDir = new List<string>();
                foreach (var pair in catalogs)
                {
                    if (NormalizeDirectory(Path.GetDirectoryName(pair.Value.Model.FilePath)) == fileDir)
                    {
                        sameDir.Add(pair.Key);
                    }
                }
                if (sameDir.Count == 1)
                {
                    return sameDir[0];
                }
                if (sameDir.Count > 1)
                {
                    ReportAmbiguousFlatCatalog(context, file, section, sameDir);
                    return null;
                }

                var parentDir = NormalizeDirectory(Path.GetDirectoryName(fileDir));
                if (parentDir.Length > 0)
                {
                    var inParent = new List<string>();
                    foreach (var pair in catalogs)
                    {
                        if (NormalizeDirectory(Path.GetDirectoryName(pair.Value.Model.FilePath)) == parentDir)
                        {
                            inParent.Add(pair.Key);
                        }
                    }
                    if (inParent.Count == 1)
                    {
                        return inParent[0];
                    }
                    if (inParent.Count > 1)
                    {
                        ReportAmbiguousFlatCatalog(context, file, section, inParent);
                        return null;
                    }
                }
            }

            if (catalogs.Count == 1)
            {
                return catalogs.Keys.First();
            }

            ReportAmbiguousFlatCatalog(context, file, section, catalogs.Keys.ToList());
            return null;
        }

        private static void ReportAmbiguousFlatCatalog(
            SourceProductionContext context, LanguageFile file, LanguageSection section, List<string> candidates)
        {
            candidates.Sort(StringComparer.Ordinal);
            context.ReportDiagnostic(Diagnostic.Create(LocalizationDiagnostics.InvalidLanguageFile,
                file.LocationAt(section.Line), file.RelativePath,
                $"cannot infer which [LanguageCatalog] this flat JSON file belongs to (candidates: {string.Join(", ", candidates)}); set \"$catalog\" or nest entries under each catalog's full name"));
        }

        private static string NormalizeDirectory(string? directory)
            => string.IsNullOrEmpty(directory) ? "" : directory!.Replace('\\', '/').TrimEnd('/');

        private static List<(string Language, string?[] Texts)> Check(
            SourceProductionContext context, CatalogModel catalog, string? declaredSource,
            SortedDictionary<string, (LanguageFile File, LanguageSection Section)> languages, out string sourceLanguageResult)
        {
            var index = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < catalog.Members.Count; i++)
            {
                index[catalog.Members[i]] = i;
            }

            var explicitSource = languages.Values.Where(e => e.File.IsSourceExplicit == true).Select(e => e.File).FirstOrDefault();
            var hasDefaultEnglishSource = languages.TryGetValue("en-US", out var defaultEnglish)
                && defaultEnglish.File.IsSourceExplicit != false;
            var sourceLanguage = declaredSource
                ?? explicitSource?.Language
                ?? (hasDefaultEnglishSource ? "en-US" : null)
                ?? languages.Values.Select(e => e.File.SourceLanguage).FirstOrDefault(s => s is not null)
                ?? "en-US";
            sourceLanguageResult = sourceLanguage;

            var hasSource = languages.TryGetValue(sourceLanguage, out var sourceEntry)
                && sourceEntry.File.IsSourceExplicit != false;
            if (!hasSource)
            {
                context.ReportDiagnostic(catalog.CreateDiagnostic(LocalizationDiagnostics.MissingSource, catalog.FullName));
            }

            var tables = new List<(string Language, string?[] Texts)>();
            var lines = new Dictionary<string, int[]>(StringComparer.Ordinal);
            // The source language first, then the others in ordinal order.
            foreach (var pair in languages.OrderBy(p => p.Key == sourceLanguage ? 0 : 1).ThenBy(p => p.Key, StringComparer.Ordinal))
            {
                var file = pair.Value.File;
                var section = pair.Value.Section;
                if (file.SourceLanguage is not null && file.SourceLanguage != sourceLanguage)
                {
                    var label = file.Format == LanguageFileFormat.Json ? "$sourceLanguage" : "srcLang";
                    context.ReportDiagnostic(Diagnostic.Create(LocalizationDiagnostics.InvalidLanguageFile, file.LocationAt(1),
                        file.RelativePath, $"{label} '{file.SourceLanguage}' differs from the source language '{sourceLanguage}' of '{catalog.FullName}'"));
                }
                var texts = new string?[catalog.Members.Count];
                var unitLines = new int[catalog.Members.Count];
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var unit in section.Units)
                {
                    if (!seen.Add(unit.Id))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(LocalizationDiagnostics.Duplicate, file.LocationAt(unit.Line),
                            $"'{file.RelativePath}' has text for '{catalog.Name}.{unit.Id}' twice"));
                        continue;
                    }
                    if (!index.TryGetValue(unit.Id, out var i))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(LocalizationDiagnostics.UnknownUnit, file.LocationAt(unit.Line),
                            file.RelativePath, unit.Id, catalog.FullName));
                        continue;
                    }
                    texts[i] = string.IsNullOrEmpty(unit.Text) ? null : unit.Text;
                    unitLines[i] = unit.Line;
                }
                for (var i = 0; i < texts.Length; i++)
                {
                    if (texts[i] is null)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(LocalizationDiagnostics.MissingText,
                            file.LocationAt(unitLines[i] > 0 ? unitLines[i] : section.Line),
                            file.RelativePath, pair.Key, catalog.Name, catalog.Members[i]));
                    }
                }
                tables.Add((pair.Key, texts));
                lines[pair.Key] = unitLines;
            }

            var sourceTexts = tables.FirstOrDefault(t => t.Language == sourceLanguage).Texts;
            if (sourceTexts is not null)
            {
                foreach (var (language, texts) in tables)
                {
                    if (language == sourceLanguage)
                    {
                        continue;
                    }
                    var file = languages[language].File;
                    for (var i = 0; i < texts.Length; i++)
                    {
                        if (texts[i] is null || sourceTexts[i] is null)
                        {
                            continue;
                        }
                        var expected = Placeholders(sourceTexts[i]!);
                        var actual = Placeholders(texts[i]!);
                        if (expected != actual)
                        {
                            context.ReportDiagnostic(Diagnostic.Create(LocalizationDiagnostics.PlaceholderMismatch,
                                file.LocationAt(lines[language][i]), file.RelativePath, language, catalog.Name, catalog.Members[i],
                                actual, expected));
                        }
                    }
                }
            }
            return tables;
        }

        /// <summary>The distinct composite-format indexes of <paramref name="text"/>, for example <c>{0}, {1}</c>, or <c>none</c>.</summary>
        internal static string Placeholders(string text)
        {
            var indexes = new SortedSet<int>();
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] != '{')
                {
                    continue;
                }
                if (i + 1 < text.Length && text[i + 1] == '{')
                {
                    i++;
                    continue;
                }
                var j = i + 1;
                var value = 0;
                while (j < text.Length && char.IsDigit(text[j]))
                {
                    value = value * 10 + (text[j] - '0');
                    j++;
                }
                if (j > i + 1 && j < text.Length && (text[j] == '}' || text[j] == ',' || text[j] == ':'))
                {
                    indexes.Add(value);
                }
            }
            return indexes.Count == 0 ? "none" : string.Join(", ", indexes.Select(n => "{" + n + "}"));
        }

        private static string EmitCatalog(CatalogModel catalog, string sourceLanguage, List<(string Language, string?[] Texts)> tables)
        {
            var kind = (catalog.Namespace is null ? "global::" : "global::" + catalog.Namespace + ".") + catalog.Name;
            var visibility = catalog.IsPublic ? "public" : "internal";
            var resources = catalog.ResourcesName;
            var extension = catalog.ExtensionName;

            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated by TKCode.Arc.Foundation.Generators />");
            sb.AppendLine("#nullable enable");
            sb.AppendLine();
            var indent = "";
            if (catalog.Namespace is not null)
            {
                sb.Append("namespace ").AppendLine(catalog.Namespace);
                sb.AppendLine("{");
                indent = "    ";
            }

            // Tables and C# lookup.
            sb.Append(indent).Append("/// <summary>The text of <see cref=\"").Append(kind).AppendLine("\"/> in every language (Localization/*.xlf, *.json).</summary>");
            sb.Append(indent).Append(visibility).Append(" static class ").AppendLine(resources);
            sb.Append(indent).AppendLine("{");
            sb.Append(indent).AppendLine("    private static readonly object[] s_keys = new object[]");
            sb.Append(indent).AppendLine("    {");
            foreach (var member in catalog.Members)
            {
                sb.Append(indent).Append("        ").Append(kind).Append('.').Append(Identifier(member)).AppendLine(",");
            }
            sb.Append(indent).AppendLine("    };");
            sb.AppendLine();
            sb.Append(indent).AppendLine("    /// <summary>The catalog: resource keys, member names and the text of each language.</summary>");
            sb.Append(indent).Append("    public static ").Append(Localization).Append("ArcLanguageCatalog Catalog { get; } = new ")
              .Append(Localization).AppendLine("ArcLanguageCatalog(");
            sb.Append(indent).Append("        ").Append(Literal(catalog.FullName)).AppendLine(",");
            sb.Append(indent).Append("        ").Append(Localization).Append("LanguageTag.Parse(").Append(Literal(sourceLanguage)).AppendLine("),");
            sb.Append(indent).AppendLine("        s_keys,");
            sb.Append(indent).Append("        new string[] { ").Append(string.Join(", ", catalog.Members.Select(Literal))).AppendLine(" },");
            sb.Append(indent).Append("        new ").Append(Localization).AppendLine("ArcLanguageTable[]");
            sb.Append(indent).AppendLine("        {");
            foreach (var (language, texts) in tables)
            {
                sb.Append(indent).Append("            new ").Append(Localization).Append("ArcLanguageTable(").Append(Localization)
                  .Append("LanguageTag.Parse(").Append(Literal(language)).AppendLine("), new string?[]");
                sb.Append(indent).AppendLine("            {");
                foreach (var text in texts)
                {
                    sb.Append(indent).Append("                ").Append(text is null ? "null" : Literal(text)).AppendLine(",");
                }
                sb.Append(indent).AppendLine("            }),");
            }
            sb.Append(indent).AppendLine("        });");
            sb.AppendLine();
            sb.Append(indent).AppendLine("    /// <summary>The resource key of <paramref name=\"kind\"/>: the boxed member, cached.</summary>");
            sb.Append(indent).Append("    public static object Key(").Append(kind).AppendLine(" kind)");
            sb.Append(indent).AppendLine("    {");
            sb.Append(indent).AppendLine("        var index = Index(kind);");
            sb.Append(indent).AppendLine("        return index < 0 ? kind : s_keys[index];");
            sb.Append(indent).AppendLine("    }");
            sb.AppendLine();
            sb.Append(indent).AppendLine("    /// <summary>The text of <paramref name=\"kind\"/> in the current language.</summary>");
            sb.Append(indent).Append("    public static string Get(").Append(kind).AppendLine(" kind)");
            sb.Append(indent).AppendLine("    {");
            sb.Append(indent).AppendLine("        var index = Index(kind);");
            sb.Append(indent).Append("        return index < 0 ? kind.ToString() : ").Append(Localization).AppendLine("ArcLanguage.Get(Catalog, index);");
            sb.Append(indent).AppendLine("    }");
            sb.AppendLine();
            sb.Append(indent).AppendLine("    /// <summary>The text of <paramref name=\"kind\"/> with its <c>{0}</c> placeholders filled, in the current language's culture.</summary>");
            sb.Append(indent).Append("    public static string Format(").Append(kind).AppendLine(" kind, params object?[] arguments)");
            sb.Append(indent).AppendLine("    {");
            sb.Append(indent).AppendLine("        var index = Index(kind);");
            sb.Append(indent).Append("        return index < 0 ? kind.ToString() : ").Append(Localization).AppendLine("ArcLanguage.Format(Catalog, index, arguments);");
            sb.Append(indent).AppendLine("    }");
            sb.AppendLine();
            sb.Append(indent).AppendLine("    /// <summary>The declaration index of <paramref name=\"kind\"/>, or -1 for a value the enum does not declare.</summary>");
            sb.Append(indent).Append("    public static int Index(").Append(kind).AppendLine(" kind)");
            sb.Append(indent).AppendLine("    {");
            sb.Append(indent).AppendLine("        switch (kind)");
            sb.Append(indent).AppendLine("        {");
            for (var i = 0; i < catalog.Members.Count; i++)
            {
                sb.Append(indent).Append("            case ").Append(kind).Append('.').Append(Identifier(catalog.Members[i])).Append(": return ")
                  .Append(i).AppendLine(";");
            }
            sb.Append(indent).AppendLine("            default: return -1;");
            sb.Append(indent).AppendLine("        }");
            sb.Append(indent).AppendLine("    }");
            sb.Append(indent).AppendLine("}");
            sb.AppendLine();

            // Markup extension.
            sb.Append(indent).Append("/// <summary>Dynamic resource reference to a <see cref=\"").Append(kind).Append("\"/> text, written <c>{arc:")
              .Append(catalog.BaseName).AppendLine(" Member}</c>; it follows the language.</summary>");
            sb.Append(indent).Append(visibility).Append(" sealed class ").AppendLine(extension);
            sb.Append(indent).AppendLine("{");
            sb.Append(indent).AppendLine("    /// <summary>Creates the extension; set <see cref=\"Kind\"/>.</summary>");
            sb.Append(indent).Append("    public ").Append(extension).AppendLine("()");
            sb.Append(indent).AppendLine("    {");
            sb.Append(indent).AppendLine("    }");
            sb.AppendLine();
            sb.Append(indent).AppendLine("    /// <summary>Creates the extension for <paramref name=\"kind\"/>.</summary>");
            sb.Append(indent).Append("    public ").Append(extension).Append('(').Append(kind).AppendLine(" kind)");
            sb.Append(indent).AppendLine("    {");
            sb.Append(indent).AppendLine("        Kind = kind;");
            sb.Append(indent).AppendLine("    }");
            sb.AppendLine();
            sb.Append(indent).AppendLine("    /// <summary>The text.</summary>");
            sb.Append(indent).Append("    public ").Append(kind).AppendLine(" Kind { get; set; }");
            sb.AppendLine();
            sb.Append(indent).AppendLine("    /// <summary>Returns a dynamic resource binding to the text's key (local priority, see <c>ArcResourceReference</c>).</summary>");
            sb.Append(indent).AppendLine("    public object ProvideValue(global::System.IServiceProvider serviceProvider)");
            sb.Append(indent).Append("        => global::TKCode.Arc.ArcResourceReference.Create(").Append(resources)
              .AppendLine(".Key(Kind), serviceProvider);");
            sb.Append(indent).AppendLine("}");
            if (catalog.Namespace is not null)
            {
                sb.AppendLine("}");
            }
            return sb.ToString();
        }

        private static string Literal(string text) => SymbolDisplay.FormatLiteral(text, quote: true);

        private static string Identifier(string name)
            => SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ? "@" + name : name;
    }
}
