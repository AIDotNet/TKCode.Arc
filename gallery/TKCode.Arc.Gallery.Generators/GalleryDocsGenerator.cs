using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace TKCode.Arc.Gallery.Generators;

/// <summary>
/// Builds <c>GalleryDocData</c>, the data behind the gallery's Code tab:
/// <list type="bullet">
/// <item>every page (<c>Docs/&lt;Category&gt;/&lt;ClassName&gt;/&lt;language&gt;.md</c>, item metadata <c>GalleryDocKind=Page</c>): its
/// front matter, demos and screenshots, not its Markdown, which the gallery reads when the page is shown
/// (<c>GalleryTexts</c>), so a host can fetch it instead of carrying every page in every language;</item>
/// <item>the API reference of every type a page names with <c>&lt;!-- api: Type --&gt;</c>, read from the referenced
/// TKCode.Arc assemblies and their XML documentation (<c>GalleryDocKind=XmlDoc</c>);</item>
/// <item>the API description translations (<c>Docs/Api/&lt;language&gt;.xlf</c>, <c>GalleryDocKind=ApiTranslation</c>).</item>
/// </list>
/// It also builds <c>GalleryDemoData</c>: every demo (<c>Demos/&lt;Category&gt;/&lt;Component&gt;/&lt;Name&gt;.axaml</c> and its
/// code-behind), with the paths of the files the demo's Code tab shows (not their text, which the gallery reads when
/// it is shown, like a page's Markdown).
/// Outdated or missing translations, unknown types or demos and undocumented members are reported as GDOC diagnostics.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class GalleryDocsGenerator : IIncrementalGenerator
{
    private const string KindMetadata = "build_metadata.AdditionalFiles.GalleryDocKind";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var files = context.AdditionalTextsProvider
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Select(static (pair, _) =>
            {
                pair.Right.GetOptions(pair.Left).TryGetValue(KindMetadata, out var kind);
                return (Kind: kind ?? string.Empty, File: pair.Left);
            });

        var pages = files.Where(static f => f.Kind == "Page")
                         .Select(static (f, ct) => DocPage.Parse(f.File.Path, f.File.GetText(ct)?.ToString()));
        var translations = files.Where(static f => f.Kind == "ApiTranslation")
                                .Select(static (f, ct) => ApiTranslationFile.Parse(f.File.Path, f.File.GetText(ct)?.ToString()));
        var xmlDocs = files.Where(static f => f.Kind == "XmlDoc")
                           .Select(static (f, ct) => XmlDocs.Parse(f.File.GetText(ct)?.ToString()));

        var input = pages.Collect()
                         .Combine(translations.Collect())
                         .Combine(xmlDocs.Collect())
                         .Combine(context.CompilationProvider);
        context.RegisterSourceOutput(input, static (spc, data) =>
            Emit(spc, data.Left.Left.Left, data.Left.Left.Right, XmlDocs.Merge(data.Left.Right), data.Right));

        var projectDirectory = context.AnalyzerConfigOptionsProvider.Select(static (options, _) =>
            options.GlobalOptions.TryGetValue("build_property.ProjectDir", out var directory) ? directory : null);
        // Avalonia passes every AvaloniaXaml item to generators as an additional file; the demos are the ones under Demos/.
        var demos = context.AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(".axaml", System.StringComparison.OrdinalIgnoreCase))
            .Combine(projectDirectory)
            .Select(static (pair, ct) => DemoFile.Parse(pair.Left.Path, pair.Left.GetText(ct)?.ToString(), pair.Right))
            .Where(static demo => demo is not null)
            .Select(static (demo, _) => demo!);
        context.RegisterSourceOutput(demos.Collect().Combine(pages.Collect()).Combine(context.CompilationProvider),
            static (spc, data) => EmitDemos(spc, data.Left.Left, data.Left.Right, data.Right));
    }

    /// <summary>
    /// The demo registry, a view class for each demo without a code-behind, and the checks between the demos and the
    /// pages that show them.
    /// </summary>
    private static void EmitDemos(SourceProductionContext context, ImmutableArray<DemoFile> demos, ImmutableArray<DocPage> pages,
                                  Compilation compilation)
    {
        var valid = new List<(DemoFile Demo, SyntaxTree? CodeBehind)>();
        var views = new StringBuilder();
        foreach (var demo in demos.OrderBy(d => d.Id, System.StringComparer.Ordinal))
        {
            var codeBehind = demo.Problem is null ? demo.FindCodeBehind(compilation) : null;
            var declared = demo.ClassName is { } name && compilation.GetTypeByMetadataName(name) is not null;
            var problem = demo.Problem;
            if (problem is null && !declared && !demo.IsUserControl)
            {
                problem = "a demo without a code-behind needs a UserControl root element";
            }
            if (problem is not null)
            {
                context.ReportDiagnostic(Diagnostic.Create(GalleryDocsDiagnostics.InvalidDemo, demo.Location, demo.RelativePath, problem));
                continue;
            }
            valid.Add((demo, codeBehind));
            if (!declared)
            {
                // No code-behind: the view class is generated. Avalonia's XAML compiler replaces AvaloniaXamlLoader.Load(this)
                // with the compiled XAML, as it does in a hand-written InitializeComponent.
                var dot = demo.ClassName!.LastIndexOf('.');
                var type = demo.ClassName.Substring(dot + 1);
                if (dot > 0)
                {
                    views.Append("namespace ").Append(demo.ClassName.Substring(0, dot)).AppendLine();
                    views.AppendLine("{");
                }
                views.Append("    partial class ").Append(type).AppendLine(" : global::Avalonia.Controls.UserControl");
                views.AppendLine("    {");
                views.Append("        public ").Append(type).AppendLine("() => global::Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);");
                views.AppendLine("    }");
                if (dot > 0)
                {
                    views.AppendLine("}");
                }
                views.AppendLine();
            }
        }

        var ids = new HashSet<string>(valid.Select(d => d.Demo.Id), System.StringComparer.Ordinal);
        var shown = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (var page in pages)
        {
            foreach (var reference in page.Demos)
            {
                if (!ids.Contains(reference.Id))
                {
                    var dot = reference.Id.LastIndexOf('.');
                    context.ReportDiagnostic(Diagnostic.Create(GalleryDocsDiagnostics.UnknownDemo, reference.Location,
                        reference.Id, reference.Id.Substring(0, dot), reference.Id.Substring(dot + 1)));
                }
                else if (page.IsSource)
                {
                    shown.Add(reference.Id);
                }
            }
        }
        foreach (var (demo, _) in valid.Where(d => !shown.Contains(d.Demo.Id)))
        {
            context.ReportDiagnostic(Diagnostic.Create(GalleryDocsDiagnostics.UnusedDemo, demo.Location, demo.Id, demo.Component, demo.Name));
        }

        var code = new StringBuilder();
        code.AppendLine("// <auto-generated />");
        code.AppendLine("// Generated by TKCode.Arc.Gallery.Generators from the gallery's Demos/**/*.axaml files.");
        code.AppendLine("#nullable enable");
        code.AppendLine();
        code.AppendLine("namespace TKCode.Arc.Gallery.Docs;");
        code.AppendLine();
        code.AppendLine("internal static class GalleryDemoData");
        code.AppendLine("{");
        // In chunks for the same reason as GalleryDocData's arrays.
        AppendChunked(code, "GalleryDemo", "Demos", valid, 64, entry =>
        {
            var (demo, codeBehind) = entry;
            code.Append("        new(").Append(Literal(demo.Component)).Append(", ").Append(Literal(demo.Name))
                .Append(", static () => new global::").Append(demo.ClassName).AppendLine("(),");
            // The files' paths only: their text is read when a Code tab or "Copy page" needs it (GalleryDemoFile), from the
            // content assembly or, in the WebAssembly host, from the server.
            code.Append("            [new(").Append(Literal(demo.RelativePath)).Append(", \"axaml\")");
            if (codeBehind is not null)
            {
                code.AppendLine(",");
                code.Append("             new(").Append(Literal(demo.RelativePath + ".cs")).Append(", \"cs\")");
            }
            code.AppendLine("]),");
        });
        code.AppendLine("}");
        context.AddSource("GalleryDemoData.g.cs", SourceText.From(code.ToString(), Encoding.UTF8));
        if (views.Length > 0)
        {
            context.AddSource("GalleryDemoViews.g.cs", SourceText.From("// <auto-generated />\n" + views, Encoding.UTF8));
        }
    }

    private static void Emit(SourceProductionContext context, ImmutableArray<DocPage> pages,
                             ImmutableArray<ApiTranslationFile> translationFiles, XmlDocs docs, Compilation compilation)
    {
        var diagnostics = new List<Diagnostic>();
        var ordered = pages.OrderBy(p => p.ClassName, System.StringComparer.Ordinal)
                           .ThenBy(p => p.IsSource ? 0 : 1)
                           .ThenBy(p => p.Language, System.StringComparer.Ordinal)
                           .ToList();
        foreach (var page in ordered)
        {
            diagnostics.AddRange(page.Diagnostics);
        }

        // Translations against their en-US source.
        var sources = ordered.Where(p => p.IsSource).GroupBy(p => p.ClassName).ToDictionary(g => g.Key, g => g.First());
        var outdated = new HashSet<DocPage>();
        foreach (var page in ordered.Where(p => !p.IsSource))
        {
            if (!sources.TryGetValue(page.ClassName, out var source))
            {
                diagnostics.Add(Diagnostic.Create(GalleryDocsDiagnostics.MissingSourcePage, Location.Create(page.Path, default, default),
                    page.ClassName, page.Language));
            }
            else if (page.SourceHash != source.Hash)
            {
                outdated.Add(page);
                diagnostics.Add(Diagnostic.Create(GalleryDocsDiagnostics.OutdatedPage, Location.Create(page.Path, default, default),
                    page.ClassName, page.Language, page.SourceHash ?? "no source-hash", source.Hash));
            }
        }

        // API references, and the languages each one is shown in.
        var types = new Dictionary<string, ApiType>(System.StringComparer.Ordinal);
        var shownIn = new Dictionary<string, HashSet<string>>(System.StringComparer.Ordinal);
        foreach (var page in ordered)
        {
            foreach (var directive in page.Directives)
            {
                if (!types.ContainsKey(directive.TypeName))
                {
                    if (ApiType.Find(compilation, directive.TypeName) is not { } symbol)
                    {
                        diagnostics.Add(Diagnostic.Create(GalleryDocsDiagnostics.UnknownApiType, directive.Location, directive.TypeName));
                        continue;
                    }
                    types[directive.TypeName] = ApiType.Build(symbol, docs, diagnostics);
                }
                if (!shownIn.TryGetValue(directive.TypeName, out var languages))
                {
                    shownIn[directive.TypeName] = languages = new HashSet<string>(System.StringComparer.Ordinal);
                }
                languages.Add(page.Language);
            }
        }
        if (types.Count > 0 && docs.IsEmpty)
        {
            diagnostics.Add(Diagnostic.Create(GalleryDocsDiagnostics.MissingXmlDocumentation, Location.None));
        }

        var translations = new List<(string Language, ApiTranslationUnit Unit)>();
        foreach (var file in translationFiles.OrderBy(f => f.Language, System.StringComparer.Ordinal))
        {
            diagnostics.AddRange(file.Diagnostics);
            translations.AddRange(file.Units.Values.OrderBy(u => u.Key, System.StringComparer.Ordinal).Select(u => (file.Language, u)));
            foreach (var unit in file.Units.Values)
            {
                if (!ApiType.IsKnownKey(compilation, unit.Key))
                {
                    diagnostics.Add(Diagnostic.Create(GalleryDocsDiagnostics.UnknownApiTranslation, unit.Location, unit.Key));
                }
            }
        }
        var byLanguage = translationFiles.GroupBy(f => f.Language).ToDictionary(g => g.Key, g => g.First());
        foreach (var type in types.Values.OrderBy(t => t.Name, System.StringComparer.Ordinal))
        {
            var entries = new List<(string Key, string? Summary)> { (type.Name, type.Summary) };
            entries.AddRange(type.Members.Select(m => (m.Key, m.Summary)));
            foreach (var language in shownIn[type.Name].Where(l => l != DocPage.SourceLanguage).OrderBy(l => l, System.StringComparer.Ordinal))
            {
                byLanguage.TryGetValue(language, out var file);
                foreach (var (key, summary) in entries)
                {
                    if (summary is null)
                    {
                        continue;
                    }
                    if (file is null || !file.Units.TryGetValue(key, out var unit))
                    {
                        diagnostics.Add(Diagnostic.Create(GalleryDocsDiagnostics.MissingApiTranslation,
                            file is null ? Location.None : Location.Create(file.Path, default, default), key, language, summary));
                    }
                    else if (unit.Source != summary)
                    {
                        diagnostics.Add(Diagnostic.Create(GalleryDocsDiagnostics.OutdatedApiTranslation, unit.Location, key, language, summary));
                    }
                }
            }
        }

        foreach (var diagnostic in diagnostics)
        {
            context.ReportDiagnostic(diagnostic);
        }
        context.AddSource("GalleryDocData.g.cs", SourceText.From(Generate(ordered, outdated, types, translations), Encoding.UTF8));
    }

    private static string Generate(List<DocPage> pages, HashSet<DocPage> outdated, Dictionary<string, ApiType> types,
                                   List<(string Language, ApiTranslationUnit Unit)> translations)
    {
        var code = new StringBuilder();
        code.AppendLine("// <auto-generated />");
        code.AppendLine("// Generated by TKCode.Arc.Gallery.Generators from Docs/**/*.md, Docs/Api/*.xlf and the TKCode.Arc XML documentation.");
        code.AppendLine("#nullable enable");
        code.AppendLine();
        code.AppendLine("namespace TKCode.Arc.Gallery.Docs;");
        code.AppendLine();
        // A class per list, because reading one static field of a class builds all of its fields: the page list is read at startup,
        // the types only with an API table, and the translations only for the language that table is shown in.
        code.AppendLine("internal static class GalleryDocData");
        code.AppendLine("{");

        // Each array is built from chunks, one method per chunk: one static constructor holding every entry exceeds the
        // WebAssembly (Mono) interpreter's limit on a method's locals ("locals size too big") once the gallery has enough pages.
        AppendChunked(code, "GalleryDocPage", "Pages", pages, 32, page =>
        {
            code.Append("        new(").Append(Literal(page.ClassName)).Append(", ").Append(Literal(page.Language)).Append(", ")
                .Append(Literal(page.Folder)).Append(", ").Append(Literal(page.Title)).Append(", ").Append(Literal(page.Summary)).Append(", ")
                .Append(outdated.Contains(page) ? "true" : "false").Append(",").AppendLine();
            code.Append("            [").Append(string.Join(", ", page.Demos.Select(d => Literal(d.Id)))).Append("],").AppendLine();
            code.Append("            [").Append(string.Join(", ", page.Screenshots.Select(s => $"({Literal(s.Name)}, {Literal(s.Caption)})"))).AppendLine("]),");
        });

        code.AppendLine("}");
        code.AppendLine();
        code.AppendLine("internal static class GalleryApiTypeData");
        code.AppendLine("{");

        // The type and its property fields are only named inside a lambda per type: reading the page list at startup must not load
        // the assembly of every documented control or run its static constructor (a host may load an assembly on demand).
        AppendChunked(code, "GalleryApiType", "Types", types.Values.OrderBy(t => t.Name, System.StringComparer.Ordinal).ToList(), 16, type =>
        {
            code.Append("        new(").Append(Literal(type.Name)).Append(", ").Append(Literal(type.Package)).Append(", ")
                .Append(Literal(type.Summary)).Append(", ").Append(Literal(type.BaseName)).AppendLine(",");
            code.Append("            [").Append(string.Join(", ", type.PseudoClasses.Select(Literal))).AppendLine("],");
            code.AppendLine("            [");
            foreach (var member in type.Members)
            {
                code.Append("                new(")
                    .Append(member.IsEvent ? "GalleryApiMemberKind.Event" : "GalleryApiMemberKind.Property").Append(", ")
                    .Append(Literal(member.Name)).Append(", ").Append(Literal(member.Type)).Append(", ")
                    .Append(Literal(member.Key)).Append(", ").Append(Literal(member.Summary)).Append(", ")
                    .Append(member.IsReadOnly ? "true" : "false").Append(", ")
                    .Append(member.IsAttached ? "true" : "false").AppendLine("),");
            }
            code.AppendLine("            ],");
            code.Append("            static () => new(typeof(").Append(type.TypeExpression).Append("), [")
                .Append(string.Join(", ", type.Members.Select(m => m.FieldExpression ?? "null"))).AppendLine("])),");
        });

        code.AppendLine("}");
        code.AppendLine();
        var languages = translations.GroupBy(t => t.Language, System.StringComparer.Ordinal).ToList();
        for (var i = 0; i < languages.Count; i++)
        {
            code.Append("internal static class GalleryApiTranslations").Append(i).AppendLine();
            code.AppendLine("{");
            AppendChunked(code, "GalleryApiTranslation", "Entries", languages[i].ToList(), 256, entry =>
            {
                code.Append("        new(").Append(Literal(entry.Unit.Key)).Append(", ").Append(Literal(entry.Unit.Target)).AppendLine("),");
            });
            code.AppendLine("}");
            code.AppendLine();
        }

        code.AppendLine("internal static class GalleryApiTranslations");
        code.AppendLine("{");
        code.AppendLine("    /// <summary>The API translations of a language tag in lower case; none for a language without any.</summary>");
        code.AppendLine("    public static GalleryApiTranslation[] For(string language) => language switch");
        code.AppendLine("    {");
        for (var i = 0; i < languages.Count; i++)
        {
            code.Append("        ").Append(Literal(languages[i].Key.ToLowerInvariant())).Append(" => GalleryApiTranslations").Append(i).AppendLine(".Entries,");
        }
        code.AppendLine("        _ => []");
        code.AppendLine("    };");
        code.AppendLine("}");
        code.AppendLine();

        code.AppendLine("internal static class GalleryDocChunks");
        code.AppendLine("{");
        code.AppendLine("    public static T[] Join<T>(params T[][] parts)");
        code.AppendLine("    {");
        code.AppendLine("        var length = 0;");
        code.AppendLine("        foreach (var part in parts)");
        code.AppendLine("        {");
        code.AppendLine("            length += part.Length;");
        code.AppendLine("        }");
        code.AppendLine("        var all = new T[length];");
        code.AppendLine("        var at = 0;");
        code.AppendLine("        foreach (var part in parts)");
        code.AppendLine("        {");
        code.AppendLine("            part.CopyTo(all, at);");
        code.AppendLine("            at += part.Length;");
        code.AppendLine("        }");
        code.AppendLine("        return all;");
        code.AppendLine("    }");
        code.AppendLine("}");
        return code.ToString();
    }

    /// <summary>Writes <c>public static readonly T[] name = Join(name0(), name1(), ...);</c> and one method per <paramref name="size"/> entries.</summary>
    private static void AppendChunked<T>(StringBuilder code, string type, string name, IReadOnlyList<T> items, int size, System.Action<T> writeEntry)
    {
        var chunks = (items.Count + size - 1) / size;
        code.Append("    public static readonly ").Append(type).Append("[] ").Append(name).Append(" = GalleryDocChunks.Join<").Append(type).Append(">(")
            .Append(string.Join(", ", Enumerable.Range(0, chunks).Select(i => $"{name}{i}()"))).AppendLine(");");
        code.AppendLine();
        for (var chunk = 0; chunk < chunks; chunk++)
        {
            code.Append("    private static ").Append(type).Append("[] ").Append(name).Append(chunk).AppendLine("() =>");
            code.AppendLine("    [");
            for (var i = chunk * size; i < System.Math.Min(items.Count, (chunk + 1) * size); i++)
            {
                writeEntry(items[i]);
            }
            code.AppendLine("    ];");
            code.AppendLine();
        }
    }

    private static string Literal(string? value) => value is null ? "null" : SymbolDisplay.FormatLiteral(value, quote: true);
}
