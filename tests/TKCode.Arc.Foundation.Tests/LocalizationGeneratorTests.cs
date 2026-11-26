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
            LanguageFile.CanonicalTag(tag).ShouldBe(runtime, tag);
        }
    }

    [Fact]
    public void Flat_json_files_generate_identical_output_to_xliff()
    {
        // Includes UTF-8 BOM, JSON escapes, and Unicode escape \u4e2d.
        const string englishJson = """
            {
              "$schema": "./schema.json",
              "$language": "en-US",
              "Save": "Save & close",
              "RowCount": "{0} rows",
              "event": "Say \"hi\"\\n"
            }
            """;
        const string chineseJson = "\uFEFF" + """
            {
              "Save": "\u4fdd\u5b58\u5e76\u5173\u95ed",
              "RowCount": "{0} 行",
              "event": "事件"
            }
            """;

        var result = Run(Catalog,
            ("/proj/Localization/en-US.json", englishJson),
            ("/proj/Localization/zh-CN.json", chineseJson),
            // JSON outside a Localization folder is ignored.
            ("/proj/appsettings.json", "{ invalid json }"),
            ("/proj/Docs/Api/zh-CN.json", "[1, 2, 3]"));

        result.Diagnostics.ShouldBeEmpty();
        result.CompileErrors.ShouldBeEmpty();
        MatchSnapshot("Demo.Controls.Localization.DemoLangResourceKind.Localization.g.cs",
            result.Sources["Demo.Controls.Localization.DemoLangResourceKind.Localization.g.cs"]);
        MatchSnapshot("ArcGeneratedAssets.Languages.g.cs", result.Sources["ArcGeneratedAssets.g.cs"]);
    }

    [Fact]
    public void Multi_catalog_json_supports_multiple_catalogs_and_custom_source_language()
    {
        const string source = """
            using TKCode.Arc.Localization;

            namespace Demo.Multi
            {
                [LanguageCatalog(SourceLanguage = "zh-CN")]
                public enum FirstLangResourceKind { Title, Count }

                [LanguageCatalog]
                public enum SecondLangResourceKind { Ok }
            }
            """;
        const string zh = """
            {
              "Demo.Multi.FirstLangResourceKind": {
                "Title": "标题",
                "Count": "共 {0} 项"
              },
              "Demo.Multi.SecondLangResourceKind": {
                "Ok": "确定"
              }
            }
            """;
        const string en = """
            {
              "Demo.Multi.FirstLangResourceKind": {
                "Title": "Title",
                "Count": "{0} items"
              },
              "Demo.Multi.SecondLangResourceKind": {
                "Ok": "OK"
              }
            }
            """;

        var result = Run(source,
            ("/proj/Localization/zh-CN.json", zh),
            ("/proj/Localization/en-US.json", en));

        result.Diagnostics.ShouldBeEmpty();
        result.CompileErrors.ShouldBeEmpty();
        result.Sources["Demo.Multi.FirstLangResourceKind.Localization.g.cs"].ShouldContain("LanguageTag.Parse(\"zh-CN\")");
        result.Sources["Demo.Multi.SecondLangResourceKind.Localization.g.cs"].ShouldContain("LanguageTag.Parse(\"en-US\")");
    }

    [Fact]
    public void Flat_json_resolves_catalog_by_directory_or_explicit_metadata()
    {
        var trees = new[]
        {
            ("/proj/FeatureA/Localization/FeatureALangResourceKind.cs", """
                using TKCode.Arc.Localization;
                namespace Demo.Dirs { [LanguageCatalog] public enum FeatureALangResourceKind { Open } }
                """),
            ("/proj/FeatureB/FeatureBLangResourceKind.cs", """
                using TKCode.Arc.Localization;
                namespace Demo.Dirs { [LanguageCatalog] public enum FeatureBLangResourceKind { Close } }
                """),
            ("/proj/Shared/Localization/SharedKinds.cs", """
                using TKCode.Arc.Localization;
                namespace Demo.Dirs
                {
                    [LanguageCatalog] public enum SharedOneLangResourceKind { One }
                    [LanguageCatalog] public enum SharedTwoLangResourceKind { Two }
                }
                """)
        };

        var result = RunTrees(trees,
            // Same directory as FeatureALangResourceKind.cs
            ("/proj/FeatureA/Localization/en-US.json", """{ "Open": "Open" }"""),
            // Parent directory holds FeatureBLangResourceKind.cs
            ("/proj/FeatureB/Localization/en-US.json", """{ "Close": "Close" }"""),
            // Multiple catalogs in Shared/Localization -> disambiguated via $catalog
            ("/proj/Shared/Localization/en-US.json", """{ "$catalog": "Demo.Dirs.SharedOneLangResourceKind", "One": "One" }"""),
            ("/proj/Shared/Sub/Localization/en-US.json", """{ "$catalog": "Demo.Dirs.SharedTwoLangResourceKind", "Two": "Two" }"""));

        result.Diagnostics.ShouldBeEmpty();
        result.CompileErrors.ShouldBeEmpty();
        result.Sources.Keys.ShouldContain("Demo.Dirs.FeatureALangResourceKind.Localization.g.cs");
        result.Sources.Keys.ShouldContain("Demo.Dirs.FeatureBLangResourceKind.Localization.g.cs");
        result.Sources.Keys.ShouldContain("Demo.Dirs.SharedOneLangResourceKind.Localization.g.cs");
        result.Sources.Keys.ShouldContain("Demo.Dirs.SharedTwoLangResourceKind.Localization.g.cs");
    }

    [Fact]
    public void Json_and_xliff_can_coexist_for_different_languages_and_detect_duplicates()
    {
        const string zhJson = """
            {
              "Save": "保存并关闭",
              "RowCount": "{0} 行",
              "event": "事件"
            }
            """;

        var mixed = Run(Catalog,
            ("/proj/Localization/en-US.xlf", English),
            ("/proj/Localization/zh-CN.json", zhJson));
        mixed.Diagnostics.ShouldBeEmpty();
        mixed.CompileErrors.ShouldBeEmpty();
        MatchSnapshot("Demo.Controls.Localization.DemoLangResourceKind.Localization.g.cs",
            mixed.Sources["Demo.Controls.Localization.DemoLangResourceKind.Localization.g.cs"]);

        var dup = Run(Catalog,
            ("/proj/Localization/en-US.xlf", English),
            ("/proj/Localization/en-US.json", """{ "Save": "Save", "RowCount": "{0}", "event": "e" }"""));
        dup.Diagnostics.Select(d => d.Id).ShouldContain("ARCF023");
    }

    [Fact]
    public void Json_sourceLanguage_metadata_sets_the_source_language()
    {
        const string source = """
            using TKCode.Arc.Localization;
            namespace Demo.SrcMeta
            {
                [LanguageCatalog]
                public enum MetaLangResourceKind { Greeting }
            }
            """;
        var result = Run(source,
            ("/proj/Localization/zh-CN.json", """{ "$sourceLanguage": "zh-CN", "Greeting": "你好 {0}" }"""),
            ("/proj/Localization/en-US.json", """{ "$sourceLanguage": "zh-CN", "Greeting": "Hello {0}" }"""));

        result.Diagnostics.ShouldBeEmpty();
        result.CompileErrors.ShouldBeEmpty();
        result.Sources["Demo.SrcMeta.MetaLangResourceKind.Localization.g.cs"].ShouldContain("LanguageTag.Parse(\"zh-CN\")");
    }

    [Fact]
    public void Json_problems_are_reported_as_diagnostics_with_line_numbers()
    {
        const string source = """
            using TKCode.Arc.Localization;

            namespace Demo.JsonBad
            {
                [LanguageCatalog]
                public enum GoodLangResourceKind { Save, Cancel, RowCount }

                [LanguageCatalog]
                public enum OtherLangResourceKind { Alone }

                [LanguageCatalog(SourceLanguage = "bad!")]
                public enum BadSourceLangResourceKind { Item }
            }
            """;
        const string good = "Demo.JsonBad.GoodLangResourceKind";

        var result = Run(source,
            ("/proj/A/Localization/en-US.json", $$"""
                {
                  "{{good}}": {
                    "Save": "Save",
                    "Cancel": "Cancel",
                    "RowCount": "{0} rows"
                  }
                }
                """),
            // Duplicate unit, unknown unit, placeholder mismatch, missing Cancel
            ("/proj/A/Localization/zh-CN.json", $$"""
                {
                  "{{good}}": {
                    "Save": "保存",
                    "Save": "保存2",
                    "Stale": "旧",
                    "RowCount": "{1} 行"
                  }
                }
                """),
            // Null value counts as missing text (ARCF021), empty string also missing
            ("/proj/A/Localization/zh-TW.json", $$"""
                {
                  "{{good}}": {
                    "Save": null,
                    "Cancel": "",
                    "RowCount": "{0} 列"
                  }
                }
                """),
            // Invalid syntax on line 3 (trailing comma)
            ("/proj/Bad/Localization/fr-FR.json", "{\n  \"Save\": \"Enregistrer\",\n}"),
            // Non-object root
            ("/proj/Bad/Localization/de-DE.json", "[\"not\", \"object\"]"),
            // Invalid filename
            ("/proj/Bad/Localization/123.json", """{ "Save": "x" }"""),
            // Mismatched $language
            ("/proj/Bad/Localization/es-ES.json", """{ "$language": "fr-FR", "Save": "x" }"""),
            // Unknown metadata key
            ("/proj/Bad/Localization/it-IT.json", """{ "$unknown": "x", "Save": "x" }"""),
            // Mixed catalog object and flat entry
            ("/proj/Bad/Localization/ko-KR.json", $$"""
                {
                  "{{good}}": { "Save": "저장" },
                  "Cancel": "취소"
                }
                """),
            // Non-string entry value
            ("/proj/Bad/Localization/nl-NL.json", $$"""
                {
                  "{{good}}": {
                    "Save": 123
                  }
                }
                """),
            // Ambiguous flat JSON (two catalogs exist and neither is in /proj/Bad/Localization)
            ("/proj/Bad/Localization/pt-BR.json", """{ "Save": "Salvar" }"""),
            // Unknown catalog
            ("/proj/Bad/Localization/ja-JP.json", """{ "Demo.JsonBad.Unknown": { "X": "X" } }"""),
            // Mismatched $sourceLanguage
            ("/proj/Bad/Localization/sv-SE.json", $$"""
                {
                  "$sourceLanguage": "de-DE",
                  "{{good}}": {
                    "Save": "Spara",
                    "Cancel": "Avbryt",
                    "RowCount": "{0} rader"
                  }
                }
                """));

        var byFile = result.Diagnostics
            .Select(d => (d.Id, d.Severity, File: Relative(d.Location.GetLineSpan().Path), Line: d.Location.GetLineSpan().StartLinePosition.Line + 1, Message: d.GetMessage()))
            .ToList();

        // ARCF026 invalid SourceLanguage on attribute
        byFile.ShouldContain(d => d.Id == "ARCF026" && d.Message.Contains("BadSourceLangResourceKind"));
        // ARCF024 missing source for OtherLangResourceKind
        byFile.ShouldContain(d => d.Id == "ARCF024" && d.Message.Contains("OtherLangResourceKind"));
        // ARCF023 duplicate unit on line 4 of zh-CN.json
        byFile.ShouldContain(d => d.Id == "ARCF023" && d.File == "A/Localization/zh-CN.json" && d.Line == 4);
        // ARCF022 unknown unit on line 5 of zh-CN.json
        byFile.ShouldContain(d => d.Id == "ARCF022" && d.File == "A/Localization/zh-CN.json" && d.Line == 5);
        // ARCF025 placeholder mismatch on line 6 of zh-CN.json
        byFile.ShouldContain(d => d.Id == "ARCF025" && d.File == "A/Localization/zh-CN.json" && d.Line == 6);
        // ARCF021 missing Cancel in zh-CN.json, and null/empty Save/Cancel in zh-TW.json
        byFile.ShouldContain(d => d.Id == "ARCF021" && d.File == "A/Localization/zh-CN.json" && d.Message.Contains("GoodLangResourceKind.Cancel"));
        byFile.ShouldContain(d => d.Id == "ARCF021" && d.File == "A/Localization/zh-TW.json" && d.Line == 3 && d.Message.Contains("GoodLangResourceKind.Save"));
        byFile.ShouldContain(d => d.Id == "ARCF021" && d.File == "A/Localization/zh-TW.json" && d.Line == 4 && d.Message.Contains("GoodLangResourceKind.Cancel"));
        // ARCF020 syntax / structure / metadata / ambiguity / $sourceLanguage mismatch
        byFile.ShouldContain(d => d.Id == "ARCF020" && d.File == "Bad/Localization/fr-FR.json" && d.Line == 3);
        byFile.ShouldContain(d => d.Id == "ARCF020" && d.File == "Bad/Localization/de-DE.json" && d.Message.Contains("root JSON value must be an object"));
        byFile.ShouldContain(d => d.Id == "ARCF020" && d.File == "Bad/Localization/123.json" && d.Message.Contains("not a language tag"));
        byFile.ShouldContain(d => d.Id == "ARCF020" && d.File == "Bad/Localization/es-ES.json" && d.Message.Contains("declares the language 'fr-FR'"));
        byFile.ShouldContain(d => d.Id == "ARCF020" && d.File == "Bad/Localization/it-IT.json" && d.Message.Contains("unknown metadata property '$unknown'"));
        byFile.ShouldContain(d => d.Id == "ARCF020" && d.File == "Bad/Localization/ko-KR.json" && d.Line == 3 && d.Message.Contains("cannot mix"));
        byFile.ShouldContain(d => d.Id == "ARCF020" && d.File == "Bad/Localization/nl-NL.json" && d.Line == 3 && d.Message.Contains("must be a string or null"));
        byFile.ShouldContain(d => d.Id == "ARCF020" && d.File == "Bad/Localization/pt-BR.json" && d.Message.Contains("cannot infer"));
        byFile.ShouldContain(d => d.Id == "ARCF020" && d.File == "Bad/Localization/sv-SE.json" && d.Message.Contains("$sourceLanguage 'de-DE' differs"));
        // ARCF027 unknown catalog
        byFile.ShouldContain(d => d.Id == "ARCF027" && d.File == "Bad/Localization/ja-JP.json" && d.Message.Contains("Demo.JsonBad.Unknown"));
        result.CompileErrors.ShouldBeEmpty();
    }

    // --- Harness -----------------------------------------------------------------------------------------------------

    private static string Relative(string path) => path.StartsWith("/proj/", StringComparison.Ordinal) ? path["/proj/".Length..] : Path.GetFileName(path);

    private sealed record GeneratorRun(
        IReadOnlyDictionary<string, string> Sources,
        ImmutableArray<Diagnostic> Diagnostics,
        IReadOnlyList<Diagnostic> CompileErrors);

    private static GeneratorRun Run(string source, params (string Path, string Text)[] additional)
        => RunTrees([("", source)], additional);

    private static GeneratorRun RunTrees((string Path, string Source)[] sources, params (string Path, string Text)[] additional)
    {
        var paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Where(p => Path.GetFileName(p).StartsWith("System.", StringComparison.Ordinal)
                        || Path.GetFileName(p) is "netstandard.dll" or "mscorlib.dll")
            .ToList();
        paths.AddRange(Directory.GetFiles(AppContext.BaseDirectory, "Avalonia*.dll"));
        paths.Add(typeof(ArcTokenDescriptor).Assembly.Location);
        var references = paths.Distinct().Select(p => (MetadataReference)MetadataReference.CreateFromFile(p)).ToList();

        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
        var syntaxTrees = sources.Select(s => CSharpSyntaxTree.ParseText(s.Source, parseOptions, path: s.Path)).ToArray();
        var compilation = CSharpCompilation.Create("Demo.Controls",
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { new ArcFoundationGenerator().AsSourceGenerator() },
            additional.Select(a => (AdditionalText)new InMemoryText(a.Path, a.Text)),
            parseOptions,
            new ProjectOptions());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        var result = driver.GetRunResult().Results.Single();
        var generatedSources = result.GeneratedSources.ToDictionary(s => s.HintName, s => s.SourceText.ToString());
        var errors = output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        return new GeneratorRun(generatedSources, result.Diagnostics, errors);
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
