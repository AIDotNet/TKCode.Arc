using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using TKCode.Arc.Icons.Lucide.Generators;

namespace TKCode.Arc.Foundation.Tests;

/// <summary>
/// The TKCode.Arc.Icons.Lucide generator, driven in-process against a stand-in of the package's public types; output is
/// compared with Snapshots/ (set ARC_UPDATE_SNAPSHOTS=1 to rewrite them after an intended change, then review the diff).
/// </summary>
public class LucideGeneratorTests
{
    private const string Package = """
        namespace TKCode.Arc.Icons.Lucide
        {
            public enum LucideIconKind { ArrowRight, Bell, Check, CircleDashed, Menu, Moon, Sparkles, Sun, Trash2, X, ZoomIn }

            public static class LucideIconData
            {
                public static void Register(LucideIconKind kind, string data) { }
            }
        }
        """;

    private const string Code = """
        using TKCode.Arc.Icons.Lucide;
        using K = TKCode.Arc.Icons.Lucide;

        namespace Demo
        {
            public static class Uses
            {
                public static object A = LucideIconKind.Bell;
                public static object B = global::TKCode.Arc.Icons.Lucide.LucideIconKind.Trash2;
                public static object C = K.LucideIconKind.ArrowRight;
                // Not Lucide: another enum's member named like an icon.
                public static object D = Other.Check;
            }

            public enum Other { Check }
        }
        """;

    private const string Xaml = """
        <UserControl xmlns="https://github.com/avaloniaui"
                     xmlns:lucide="https://github.com/AIDotNet/TKCode.Arc/lucide"
                     xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
            <UserControl.Styles>
                <Style Selector="lucide|LucideIcon.night">
                    <Setter Property="Kind" Value="Moon" />
                </Style>
            </UserControl.Styles>
            <StackPanel>
                <lucide:LucideIcon Size="20" Kind="Sparkles" />
                <lucide:LucideIcon>
                    <lucide:LucideIcon.Kind>Sun</lucide:LucideIcon.Kind>
                </lucide:LucideIcon>
                <lucide:LucideIcon Kind="{Binding Icon}" />
                <arc:MenuItem Icon="{lucide:LucideGeometry Kind=X}" />
                <arc:MenuItem Icon="{lucide:LucideGeometry ZoomIn}" />
                <!-- <lucide:LucideIcon Kind="Check" /> -->
                <arc:IconMorph Kind="Menu" />
            </StackPanel>
        </UserControl>
        """;

    [Fact]
    public void Compiles_in_the_icons_used_in_code_xaml_and_items()
    {
        var result = Run([Package, Code], new() { ["_LucideIconsDeclared"] = "circle-dashed" }, ("/proj/Views/Main.axaml", Xaml));

        result.Diagnostics.ShouldBeEmpty();
        result.CompileErrors.ShouldBeEmpty();
        MatchSnapshot("LucideIcons.Registration.g.cs", result.Sources["LucideIcons.Registration.g.cs"]);
    }

    private const string BoundXaml = """
        <UserControl xmlns="https://github.com/avaloniaui"
                     xmlns:lucide="https://github.com/AIDotNet/TKCode.Arc/lucide">
            <UserControl.Styles>
                <Style Selector="lucide|LucideIcon.state">
                    <Setter Property="Kind" Value="{Binding StateIcon}" />
                </Style>
            </UserControl.Styles>
            <StackPanel>
                <lucide:LucideIcon Size="16" Kind="{Binding Icon}" />
                <lucide:LucideIcon>
                    <lucide:LucideIcon.Kind>
                        <Binding Path="Other" />
                    </lucide:LucideIcon.Kind>
                </lucide:LucideIcon>
                <lucide:LucideIcon Kind="{x:Static lucide:LucideIconKind.Bell}" />
                <!-- <lucide:LucideIcon Kind="{Binding Commented}" /> -->
            </StackPanel>
        </UserControl>
        """;

    [Fact]
    public void Bound_kinds_are_reported_where_they_are_bound()
    {
        var result = Run([Package], new(), ("/proj/Views/Bound.axaml", BoundXaml));

        result.Diagnostics.Select(d => $"{d.Id} {d.Severity} {Path.GetFileName(d.Location.GetLineSpan().Path)}({d.Location.GetLineSpan().StartLinePosition.Line + 1})")
            .OrderBy(s => s, StringComparer.Ordinal)
            .ShouldBe(["ARCL002 Warning Bound.axaml(11)", "ARCL002 Warning Bound.axaml(5)", "ARCL002 Warning Bound.axaml(9)"]);
        result.Diagnostics[0].GetMessage().ShouldContain("<LucideIcon Include=");
        // x:Static still compiles its icon in.
        result.Sources["LucideIcons.Registration.g.cs"].ShouldContain("LucideIconKind.Bell,");
    }

    [Theory]
    [InlineData("_LucideIconsDeclared", "Sparkles")]
    [InlineData("LucideIconsIncludeAll", "true")]
    public void Bound_kinds_are_not_reported_once_icons_are_declared(string property, string value)
    {
        var result = Run([Package], new() { [property] = value }, ("/proj/Views/Bound.axaml", BoundXaml));

        result.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Emits_nothing_when_no_icon_is_used()
    {
        var result = Run([Package], new(), ("/proj/Views/Main.axaml", "<UserControl xmlns=\"https://github.com/avaloniaui\" />"));

        result.Diagnostics.ShouldBeEmpty();
        result.Sources.ShouldBeEmpty();
    }

    [Fact]
    public void Emits_nothing_without_the_package()
    {
        var result = Run(["namespace Demo { public static class A { } }"], new() { ["_LucideIconsDeclared"] = "Bell" });

        result.Sources.ShouldBeEmpty();
    }

    [Fact]
    public void Unknown_declared_icons_are_reported()
    {
        var result = Run([Package], new() { ["_LucideIconsDeclared"] = "Bell,not-an-icon" });

        result.Diagnostics.Select(d => d.Id + ": " + d.GetMessage()).ShouldBe(["ARCL001: LucideIcon item 'not-an-icon' is not a Lucide icon; it is ignored"]);
        result.Sources["LucideIcons.Registration.g.cs"].ShouldContain("LucideIconKind.Bell,");
    }

    [Fact]
    public void Include_all_compiles_in_every_icon()
    {
        var result = Run([Package], new() { ["LucideIconsIncludeAll"] = "true" });

        var registrations = result.Sources["LucideIcons.Registration.g.cs"].Split('\n').Count(l => l.Contains("LucideIconData.Register(", StringComparison.Ordinal));
        registrations.ShouldBe(2155);
    }

    private sealed record GeneratorRun(
        IReadOnlyDictionary<string, string> Sources,
        ImmutableArray<Diagnostic> Diagnostics,
        IReadOnlyList<Diagnostic> CompileErrors);

    private static GeneratorRun Run(string[] sources, Dictionary<string, string> properties, params (string Path, string Text)[] additional)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Where(p => Path.GetFileName(p).StartsWith("System.", StringComparison.Ordinal)
                        || Path.GetFileName(p) is "netstandard.dll" or "mscorlib.dll")
            .Distinct()
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .ToList();
        var compilation = CSharpCompilation.Create("Demo",
            sources.Select(s => CSharpSyntaxTree.ParseText(s, new CSharpParseOptions(LanguageVersion.Latest))),
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { new LucideIconGenerator().AsSourceGenerator() },
            additional.Select(a => (AdditionalText)new InMemoryText(a.Path, a.Text)),
            (CSharpParseOptions)compilation.SyntaxTrees[0].Options,
            new ProjectOptions(properties));
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        var result = driver.GetRunResult().Results.Single();
        var generated = result.GeneratedSources.ToDictionary(s => s.HintName, s => s.SourceText.ToString());
        var errors = output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        return new GeneratorRun(generated, result.Diagnostics, errors);
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

    private sealed class ProjectOptions(Dictionary<string, string> properties) : AnalyzerConfigOptionsProvider
    {
        private readonly Options _global = new(properties.ToDictionary(p => "build_property." + p.Key, p => p.Value));

        public override AnalyzerConfigOptions GlobalOptions => _global;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => Options.Empty;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => Options.Empty;

        private sealed class Options(Dictionary<string, string> values) : AnalyzerConfigOptions
        {
            public static readonly Options Empty = new(new Dictionary<string, string>());

            public override bool TryGetValue(string key, out string value) => values.TryGetValue(key, out value!);
        }
    }
}
