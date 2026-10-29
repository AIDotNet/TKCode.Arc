using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using TKCode.Arc.Foundation.Generators;

namespace TKCode.Arc.Foundation.Tests;

/// <summary>
/// Drives the Foundation generator in-process and compares its output with the files under Snapshots/. Set
/// ARC_UPDATE_SNAPSHOTS=1 to rewrite them after an intended change, then review the diff.
/// </summary>
public class GeneratorTests
{
    private const string Usings = """
        using System;
        using Avalonia;
        using Avalonia.Animation.Easings;
        using Avalonia.Media;
        using TKCode.Arc;

        """;

    private const string DemoTokens = Usings + """
        [assembly: Avalonia.Metadata.XmlnsDefinition("https://example.com/demo", "Demo.Controls")]
        [assembly: Avalonia.Metadata.XmlnsDefinition("https://example.com/demo", "Demo.Controls.DesignTokens")]

        namespace Demo.Controls
        {
            public enum Tone { Soft, Loud }

            public class DemoButton : Avalonia.Controls.Button { }

            [ControlDesignToken(IsGlobal = true)]
            internal sealed class DemoDesignToken : AbstractControlDesignToken
            {
                public Color Accent { get; set; }
                public string Preset { get; set; } = "neutral";
                public override void CalculateTokenValues(bool isDarkMode)
                {
                    base.CalculateTokenValues(isDarkMode);
                    Accent = EffectiveGlobalToken.ColorPrimary;
                }
            }

            [ControlDesignToken]
            internal sealed class DemoButtonToken : AbstractControlDesignToken
            {
                public Color Fill { get; set; }
                public double Opacity { get; set; }
                public int Steps { get; set; }
                public bool IsDense { get; set; }
                public string Label { get; set; } = "";
                public Thickness Inset { get; set; }
                public CornerRadius Radius { get; set; }
                public BoxShadows Shadow { get; set; }
                public FontFamily Font { get; set; } = FontFamily.Default;
                public FontWeight Weight { get; set; }
                public TimeSpan Duration { get; set; }
                public Easing Ease { get; set; } = new LinearEasing();
                public Tone Tone { get; set; }
                public double ReadOnlyValue { get; } = 1;
                internal double Hidden { get; set; }
            }
        }
        """;

    private const string DemoTheme = """
        <ResourceDictionary xmlns="https://github.com/avaloniaui"
                            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                            xmlns:demo="https://example.com/demo"
                            xmlns:local="clr-namespace:Demo.Controls"
                            x:Class="Demo.Controls.Themes.DemoButtonTheme"
                            x:ClassModifier="internal">
            <ResourceDictionary.ThemeDictionaries />
            <ControlTheme x:Key="{x:Type demo:DemoButton}" TargetType="demo:DemoButton" />
            <ControlTheme x:Key="DemoGhostButtonTheme" TargetType="local:DemoButton" />
        </ResourceDictionary>
        """;

    [Fact]
    public void Token_classes_generate_kinds_keys_extension_writer_and_parser()
    {
        var result = Run(DemoTokens, ("/proj/Controls/DemoButton/Themes/DemoButtonTheme.axaml", DemoTheme));

        result.Diagnostics.ShouldBeEmpty();
        result.CompileErrors.ShouldBeEmpty();
        MatchSnapshot("DemoButtonToken.g.cs", result.Sources["DemoButtonToken.g.cs"]);
        MatchSnapshot("DemoDesignToken.g.cs", result.Sources["DemoDesignToken.g.cs"]);
        MatchSnapshot("ArcGeneratedAssets.g.cs", result.Sources["ArcGeneratedAssets.g.cs"]);
        MatchSnapshot("Demo.Controls.Themes.DemoButtonTheme.g.cs", result.Sources["Demo.Controls.Themes.DemoButtonTheme.g.cs"]);
    }

    [Fact]
    public void Problems_are_reported_as_diagnostics()
    {
        const string source = Usings + """
            namespace Demo.Bad
            {
                public class Thing : Avalonia.Controls.Control { }

                [ControlDesignToken]
                internal sealed class OddToken : AbstractControlDesignToken
                {
                    public Point Offset { get; set; }
                    public Color? MaybeColor { get; set; }
                    public double Fine { get; set; }
                }

                [ControlDesignToken]
                internal sealed class EmptyToken : AbstractControlDesignToken { }

                [ControlDesignToken]
                internal sealed class NotAToken { public double Size { get; set; } }

                [ControlDesignToken]
                internal sealed class Widget : AbstractControlDesignToken { public double Size { get; set; } }

                [ControlDesignToken]
                internal abstract class AbstractToken : AbstractControlDesignToken { public double Size { get; set; } }

                [ControlDesignToken(IsGlobal = true)]
                internal sealed class FirstToken : AbstractControlDesignToken { public double Size { get; set; } }

                [ControlDesignToken(IsGlobal = true)]
                internal sealed class SecondToken : AbstractControlDesignToken { public double Size { get; set; } }
            }

            namespace Demo.Other
            {
                [ControlDesignToken]
                internal sealed class FirstToken : AbstractControlDesignToken { public double Size { get; set; } }
            }
            """;
        const string noClass = """
            <ResourceDictionary xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <ControlTheme x:Key="X" />
            </ResourceDictionary>
            """;
        const string unresolved = """
            <ResourceDictionary xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                                xmlns:b="clr-namespace:Demo.Bad" x:Class="Demo.Bad.UnresolvedTheme">
                <ControlTheme x:Key="{x:Type b:Missing}" />
                <ControlTheme x:Key="{x:Type b:Thing}" />
                <ControlTheme x:Key="Shared" />
            </ResourceDictionary>
            """;
        const string duplicate = """
            <ResourceDictionary xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                                xmlns:b="using:Demo.Bad" x:Class="Demo.Bad.DuplicateTheme">
                <ControlTheme x:Key="{x:Type b:Thing}" />
                <ControlTheme x:Key="Shared" />
            </ResourceDictionary>
            """;
        const string empty = """
            <ResourceDictionary xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" x:Class="Demo.Bad.EmptyTheme" />
            """;

        var result = Run(source,
            ("/proj/A/Themes/NoClassTheme.axaml", noClass),
            ("/proj/B/Themes/BrokenTheme.axaml", "<ResourceDictionary><unclosed></ResourceDictionary>"),
            ("/proj/C/Themes/UnresolvedTheme.axaml", unresolved),
            ("/proj/D/Themes/DuplicateTheme.axaml", duplicate),
            ("/proj/E/Themes/EmptyTheme.axaml", empty),
            ("/proj/E/Themes/NotATheme.xml", "ignored"),
            ("/proj/E/Views/OtherTheme.axaml", "<ignored/>"));

        var lines = result.Diagnostics
            .Select(d => $"{d.Id} {d.Severity} {Path.GetFileName(d.Location.GetLineSpan().Path)}:{d.Location.GetLineSpan().StartLinePosition.Line + 1} {d.GetMessage()}")
            .Order(StringComparer.Ordinal);
        MatchSnapshot("Diagnostics", string.Join("\n", lines) + "\n");
        // Valid classes are still generated next to the invalid ones, and the output compiles.
        result.Sources.Keys.ShouldContain("OddToken.g.cs");
        result.CompileErrors.ShouldBeEmpty();
    }

    [Fact]
    public void Nothing_is_generated_without_a_Foundation_reference()
    {
        var result = Run("namespace X { [System.Obsolete] class ControlDesignTokenAttribute : System.Attribute { } }",
            withFoundation: false);
        result.Sources.ShouldBeEmpty();
        result.Diagnostics.ShouldBeEmpty();
    }

    // --- Harness -----------------------------------------------------------------------------------------------------

    private sealed record GeneratorRun(
        IReadOnlyDictionary<string, string> Sources,
        ImmutableArray<Diagnostic> Diagnostics,
        IReadOnlyList<Diagnostic> CompileErrors);

    private static GeneratorRun Run(string source, params (string Path, string Text)[] additional)
        => Run(source, true, additional);

    private static GeneratorRun Run(string source, bool withFoundation, params (string Path, string Text)[] additional)
    {
        var references = References(withFoundation);
        var compilation = CSharpCompilation.Create("Demo.Controls",
            new[] { CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest)) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { new ArcFoundationGenerator().AsSourceGenerator() },
            additional.Select(a => (AdditionalText)new InMemoryText(a.Path, a.Text)),
            (CSharpParseOptions)compilation.SyntaxTrees[0].Options,
            new ProjectOptions("/proj/", "Demo.Controls"));
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        var result = driver.GetRunResult().Results.Single();
        var sources = result.GeneratedSources.ToDictionary(s => s.HintName, s => s.SourceText.ToString());
        var errors = output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        return new GeneratorRun(sources, result.Diagnostics, errors);
    }

    private static List<MetadataReference> References(bool withFoundation)
    {
        var paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Where(p => Path.GetFileName(p).StartsWith("System.", StringComparison.Ordinal)
                        || Path.GetFileName(p) is "netstandard.dll" or "mscorlib.dll")
            .ToList();
        paths.AddRange(Directory.GetFiles(AppContext.BaseDirectory, "Avalonia*.dll"));
        if (withFoundation)
        {
            paths.Add(typeof(ArcTokenDescriptor).Assembly.Location);
        }
        return paths.Distinct().Select(p => (MetadataReference)MetadataReference.CreateFromFile(p)).ToList();
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

    private sealed class ProjectOptions(string projectDirectory, string rootNamespace) : AnalyzerConfigOptionsProvider
    {
        private readonly Options _global = new(new Dictionary<string, string>
        {
            ["build_property.projectdir"] = projectDirectory,
            ["build_property.rootnamespace"] = rootNamespace
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
