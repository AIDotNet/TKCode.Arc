using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using TKCode.Arc.Foundation.Generators;
using TKCode.Arc.Localization;

namespace TKCode.Arc.Foundation.Tests;

/// <summary>
/// The localization part of the Foundation generator, driven in-process; output is compared with Snapshots/ (set
/// ARC_UPDATE_SNAPSHOTS=1 to rewrite them after an intended change, then review the diff).
/// </summary>
public class LocalizationGeneratorTests
{
    private const string Catalog = """
        using TKCode.Arc.Localization;

        [assembly: Avalonia.Metadata.XmlnsDefinition("https://example.com/demo", "Demo.Controls.Localization")]

        namespace Demo.Controls.Localization
        {
            [LanguageCatalog]
            public enum DemoLangResourceKind
            {
                Save,
                RowCount,
                @event
            }
        }
        """;

    private static string Xlf(string language, string units, bool source = false, string? id = null)
        => $"""
           <?xml version="1.0" encoding="utf-8"?>
           <xliff version="2.1" srcLang="en-US"{(source ? "" : $" trgLang=\"{language}\"")} xmlns="urn:oasis:names:tc:xliff:document:2.0">
             <file id="{id ?? "Demo.Controls.Localization.DemoLangResourceKind"}">
           {units}
             </file>
           </xliff>
           """;

    private static string Unit(string id, string source, string? target = null)
        => $"""
               <unit id="{id}">
                 <segment>
                   <source>{source}</source>{(target is null ? "" : $"\n          <target>{target}</target>")}
                 </segment>
               </unit>
           """;

    private static readonly string English = Xlf("en-US",
        Unit("Save", "Save &amp; close") + "\n" + Unit("RowCount", "{0} rows") + "\n" + Unit("event", "Say \"hi\"\\n"), source: true);

    private static readonly string Chinese = Xlf("zh-CN",
        Unit("Save", "Save &amp; close", "保存并关闭") + "\n" + Unit("RowCount", "{0} rows", "{0} 行") + "\n" + Unit("event", "x", "事件"));

    [Fact]
    public void Catalogs_generate_tables_lookup_and_markup_extension()
    {
        var result = Run(Catalog,
            ("/proj/Localization/en-US.xlf", English),
            ("/proj/Localization/zh-CN.xlf", Chinese),
            // Only xlf files directly in a Localization folder are language files.
            ("/proj/Docs/Api/zh-CN.xlf", "<not-xliff/>"));

        result.Diagnostics.ShouldBeEmpty();
        result.CompileErrors.ShouldBeEmpty();
        MatchSnapshot("Demo.Controls.Localization.DemoLangResourceKind.Localization.g.cs",
            result.Sources["Demo.Controls.Localization.DemoLangResourceKind.Localization.g.cs"]);
        MatchSnapshot("ArcGeneratedAssets.Languages.g.cs", result.Sources["ArcGeneratedAssets.g.cs"]);
    }

    [Fact]
    public void Language_problems_are_reported_as_diagnostics()
    {
        const string source = """
            using TKCode.Arc.Localization;

            namespace Demo.Bad
            {
                [LanguageCatalog]
                public enum GoodLangResourceKind { Save, Cancel, RowCount }

                [LanguageCatalog]
                public enum OrphanLangResourceKind { Alone }

                [LanguageCatalog]
                public enum SameLangResourceKind { One = 1, Uno = 1 }

                public class Holder
                {
                    [LanguageCatalog]
                    public enum NestedLangResourceKind { Inner }
                }
            }
            """;
        const string good = "Demo.Bad.GoodLangResourceKind";
        var english = Xlf("en-US", Unit("Save", "Save") + "\n" + Unit("Cancel", "Cancel") + "\n" + Unit("RowCount", "{0} rows"), source: true, id: good);
        var chinese = Xlf("zh-CN",
            Unit("Save", "Save", "保存") + "\n" + Unit("Save", "Save", "保存") + "\n" + Unit("Stale", "Stale", "旧")
            + "\n" + Unit("RowCount", "{0} rows", "{1} 行"), id: good);
        var traditional = Xlf("zh-TW", Unit("Save", "Save", "") + "\n" + Unit("Cancel", "Cancel", "取消") + "\n" + Unit("RowCount", "{0} rows", "{0} 列"), id: good);

        var result = Run(source,
            ("/proj/A/Localization/en-US.xlf", english),
            ("/proj/A/Localization/zh-CN.xlf", chinese),
            ("/proj/A/Localization/zh-TW.xlf", traditional),
            ("/proj/B/Localization/zh-TW.xlf", traditional),
            ("/proj/C/Localization/fr-FR.xlf", "<xliff><unclosed></xliff>"),
            ("/proj/C/Localization/notes.xlf", english),
            ("/proj/C/Localization/de-DE.xlf", Xlf("fr-FR", Unit("Save", "Save", "Enregistrer"), id: good)),
            ("/proj/C/Localization/ja-JP.xlf", Xlf("ja-JP", Unit("X", "X", "X"), id: "Demo.Bad.Unknown")),
            ("/proj/C/Localization/en-GB.xlf", "<?xml version=\"1.0\"?><xliff version=\"1.2\" srcLang=\"en-GB\" />"));

        var lines = result.Diagnostics
            .Select(d => $"{d.Id} {d.Severity} {Relative(d.Location.GetLineSpan().Path)}:{d.Location.GetLineSpan().StartLinePosition.Line + 1} {d.GetMessage()}")
            .Order(StringComparer.Ordinal);
        MatchSnapshot("LocalizationDiagnostics", string.Join("\n", lines) + "\n");
        // Every catalog that can be generated still is, and the output compiles.
        result.Sources.Keys.ShouldContain("Demo.Bad.GoodLangResourceKind.Localization.g.cs");
        result.Sources.Keys.ShouldContain("Demo.Bad.OrphanLangResourceKind.Localization.g.cs");
        result.CompileErrors.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("{0} of {1}", "{0}, {1}")]
    [InlineData("{1:N0} and {0,5}", "{0}, {1}")]
    [InlineData("{{0}} literal", "none")]
    [InlineData("{0}{0}", "{0}")]
    [InlineData("{name}", "none")]
    public void Placeholders_are_read_like_composite_format(string text, string expected)
        => LocalizationEmitter.Placeholders(text).ShouldBe(expected);

    [Fact]
    public void Generated_tables_match_the_runtime_tag_rules()
    {
        foreach (var tag in new[] { "en-us", "ZH_hant_tw", "es-419", "sr-latn-rs", "x" })
        {
            var runtime = LanguageTag.TryParse(tag, out var parsed) ? parsed.Value : null;
            XlfFile.CanonicalTag(tag).ShouldBe(runtime, tag);
        }
    }

    // --- Harness -----------------------------------------------------------------------------------------------------

    private static string Relative(string path) => path.StartsWith("/proj/", StringComparison.Ordinal) ? path["/proj/".Length..] : Path.GetFileName(path);

    private sealed record GeneratorRun(
        IReadOnlyDictionary<string, string> Sources,
        ImmutableArray<Diagnostic> Diagnostics,
        IReadOnlyList<Diagnostic> CompileErrors);

    private static GeneratorRun Run(string source, params (string Path, string Text)[] additional)
    {
        var paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Where(p => Path.GetFileName(p).StartsWith("System.", StringComparison.Ordinal)
                        || Path.GetFileName(p) is "netstandard.dll" or "mscorlib.dll")
            .ToList();
        paths.AddRange(Directory.GetFiles(AppContext.BaseDirectory, "Avalonia*.dll"));
        paths.Add(typeof(ArcTokenDescriptor).Assembly.Location);
        var references = paths.Distinct().Select(p => (MetadataReference)MetadataReference.CreateFromFile(p)).ToList();

        var compilation = CSharpCompilation.Create("Demo.Controls",
            new[] { CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest)) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { new ArcFoundationGenerator().AsSourceGenerator() },
            additional.Select(a => (AdditionalText)new InMemoryText(a.Path, a.Text)),
            (CSharpParseOptions)compilation.SyntaxTrees[0].Options,
            new ProjectOptions());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        var result = driver.GetRunResult().Results.Single();
        var sources = result.GeneratedSources.ToDictionary(s => s.HintName, s => s.SourceText.ToString());
        var errors = output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        return new GeneratorRun(sources, result.Diagnostics, errors);
    }

    private static void MatchSnapshot(string name, string actual, [CallerFilePath] string callerPath = "")
    {
        var path = Path.Combine(Path.GetDirectoryName(callerPath)!, "Snapshots", name + ".txt");
        actual = actual.Replace("\r\n", "\n");
        if (Environment.GetEnvironmentVariable("ARC_UPDATE_SNAPSHOTS") == "1" || !File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, actual, new UTF8Encoding(false));
            if (Environment.GetEnvironmentVariable("ARC_UPDATE_SNAPSHOTS") != "1")
            {
                throw new InvalidOperationException($"Snapshot {name} did not exist and was written; review it and run again.");
            }
            return;
        }
        File.ReadAllText(path).Replace("\r\n", "\n").ShouldBe(actual, $"Generator output differs from Snapshots/{name}.txt (ARC_UPDATE_SNAPSHOTS=1 rewrites it).");
    }

    private sealed class InMemoryText(string path, string text) : AdditionalText
    {
        public override string Path { get; } = path;

        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(text, Encoding.UTF8);
    }

    private sealed class ProjectOptions : AnalyzerConfigOptionsProvider
    {
        private readonly Options _global = new(new Dictionary<string, string>
        {
            ["build_property.projectdir"] = "/proj/",
            ["build_property.rootnamespace"] = "Demo.Controls"
        });

        public override AnalyzerConfigOptions GlobalOptions => _global;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => Options.Empty;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => Options.Empty;

        private sealed class Options(Dictionary<string, string> values) : AnalyzerConfigOptions
        {
            public static readonly Options Empty = new(new Dictionary<string, string>());

            public override bool TryGetValue(string key, out string value)
            {
                var found = values.TryGetValue(key, out var text);
                value = text ?? string.Empty;
                return found;
            }
        }
    }
}
