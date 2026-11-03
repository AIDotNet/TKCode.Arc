using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TKCode.Arc.Foundation.Generators;

namespace TKCode.Arc.Foundation.Tests;

/// <summary>
/// Drives <see cref="PropertyGridModelGenerator"/> against stand-ins for the TKCode.Arc runtime types (this project does
/// not reference TKCode.Arc), checks that the generated registration compiles, and compares it with Snapshots/. Set
/// ARC_UPDATE_SNAPSHOTS=1 to rewrite the snapshot after an intended change.
/// </summary>
public class PropertyGridGeneratorTests
{
    private const string Runtime = """
        #nullable enable
        using System;
        using System.Collections.Generic;
        namespace TKCode.Arc
        {
            [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)] public sealed class PropertyGridModelAttribute : Attribute { }
            [AttributeUsage(AttributeTargets.Property)] public sealed class PropertyGridOptionsAttribute : Attribute
            {
                public int Order { get; set; }
                public string? EditorKey { get; set; }
                public string? Classes { get; set; }
                public double Minimum { get; set; } = double.NaN;
                public double Maximum { get; set; } = double.NaN;
                public double Step { get; set; } = double.NaN;
                public int FractionDigits { get; set; } = -1;
                public PropertyGridExpand Expand { get; set; }
            }
            public enum PropertyGridExpand { Auto, Always, Never }
            public sealed record PropertyGridChoice(object? Value, string Text);
            public sealed class PropertyGridProperty
            {
                public PropertyGridProperty(string name, Type type, Func<object, object?> getter, Func<object, object?, object>? setter = null) { }
                public string DisplayName { get; init; } = "";
                public string? Category { get; init; }
                public string? Description { get; init; }
                public bool IsReadOnly { get; init; }
                public int Order { get; init; }
                public string? EditorKey { get; init; }
                public string? Classes { get; init; }
                public double? Minimum { get; init; }
                public double? Maximum { get; init; }
                public double? Step { get; init; }
                public int? FractionDigits { get; init; }
                public IReadOnlyList<PropertyGridChoice>? Choices { get; init; }
                public PropertyGridExpand Expand { get; init; }
                public Type? ElementType { get; init; }
                public Func<object?>? CreateElement { get; init; }
            }
            public sealed class PropertyGridTypeInfo
            {
                public PropertyGridTypeInfo(Type type, IReadOnlyList<PropertyGridProperty> properties) { }
            }
            public static class PropertyGridTypes
            {
                public static void Register(Type type, Func<PropertyGridTypeInfo> factory) { }
            }
        }
        """;

    private const string Models = """
        #nullable enable
        using System.Collections.Generic;
        using System.ComponentModel;
        using TKCode.Arc;

        namespace Demo.Model
        {
            public enum LineType
            {
                [Description("Continuous")] Solid,
                Dashed,
                [Browsable(false)] Hidden
            }

            [PropertyGridModel]
            public struct Point3
            {
                public double X { get; set; }
                public double Y { get; set; }
                public double Length => X + Y;
            }

            public class Entity
            {
                [Category("General"), DisplayName("Layer name"), Description("The layer the entity is drawn on.")]
                public string Layer { get; set; } = "0";

                [Browsable(false)]
                public int Secret { get; set; }
            }

            [PropertyGridModel]
            public sealed class Arc : Entity
            {
                [Category("Geometry"), PropertyGridOptions(Order = -1, Minimum = 0, FractionDigits = 2, Classes = "accent")]
                public double Radius { get; set; }

                [Category("Geometry")]
                public Point3 Center { get; set; }

                [Category("General")]
                public LineType? LineType { get; set; }

                public List<Point3> Points { get; } = new();

                public string[] Tags { get; set; } = [];

                [ReadOnly(true)]
                public double Area { get; set; }

                public string Id { get; init; } = "";

                internal int Hidden { get; set; }

                public int this[int index] => index;
            }

            internal sealed class Outer
            {
                [PropertyGridModel]
                private sealed class Inaccessible { }
            }

            [PropertyGridModel]
            public sealed class Box<T> { }
        }
        """;

    [Fact]
    public void Models_register_typed_descriptors_that_compile()
    {
        var (sources, diagnostics, errors) = Run(Models);

        errors.ShouldBeEmpty(string.Join("\n", errors));
        sources.Keys.Order(StringComparer.Ordinal).ShouldBe(["PropertyGrid.Demo.Model.Arc.g.cs", "PropertyGrid.Demo.Model.Point3.g.cs"]);
        MatchSnapshot("PropertyGrid.Arc", sources["PropertyGrid.Demo.Model.Arc.g.cs"]);
        MatchSnapshot("PropertyGrid.Point3", sources["PropertyGrid.Demo.Model.Point3.g.cs"]);

        diagnostics.Select(d => d.Id).Order(StringComparer.Ordinal).ShouldBe(["ARCF030", "ARCF031"]);
    }

    [Fact]
    public void Struct_setters_return_the_changed_copy_and_classes_the_same_instance()
    {
        var (sources, _, _) = Run(Models);

        sources["PropertyGrid.Demo.Model.Point3.g.cs"].ShouldContain(
            "static (o, v) => { var t = (global::Demo.Model.Point3)o; t.X = (double)v!; return t; }");
        sources["PropertyGrid.Demo.Model.Arc.g.cs"].ShouldContain(
            "static (o, v) => { ((global::Demo.Model.Arc)o).Radius = (double)v!; return o; }");
        // Get-only, init-only and [ReadOnly] properties have no setter or are marked read-only.
        sources["PropertyGrid.Demo.Model.Point3.g.cs"].ShouldContain("((global::Demo.Model.Point3)o).Length, null)");
        sources["PropertyGrid.Demo.Model.Arc.g.cs"].ShouldContain("((global::Demo.Model.Arc)o).Id, null)");
        sources["PropertyGrid.Demo.Model.Arc.g.cs"].ShouldContain("IsReadOnly = true,");
        sources["PropertyGrid.Demo.Model.Arc.g.cs"].ShouldNotContain("Secret");
        sources["PropertyGrid.Demo.Model.Arc.g.cs"].ShouldNotContain("Hidden");
    }

    private static (Dictionary<string, string> Sources, IReadOnlyList<Diagnostic> Diagnostics, List<Diagnostic> Errors) Run(string source)
    {
        var parse = new CSharpParseOptions(LanguageVersion.Latest);
        var compilation = CSharpCompilation.Create("Demo.Model",
            [CSharpSyntaxTree.ParseText(Runtime, parse), CSharpSyntaxTree.ParseText(source, parse)],
            References(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new PropertyGridModelGenerator().AsSourceGenerator()], parseOptions: parse);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        var result = driver.GetRunResult().Results.Single();
        var sources = result.GeneratedSources.ToDictionary(s => s.HintName, s => s.SourceText.ToString());
        var errors = output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        return (sources, result.Diagnostics, errors);
    }

    private static List<MetadataReference> References()
    {
        var paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Where(p => Path.GetFileName(p).StartsWith("System.", StringComparison.Ordinal)
                        || Path.GetFileName(p) is "netstandard.dll" or "mscorlib.dll");
        return paths.Distinct().Select(p => (MetadataReference)MetadataReference.CreateFromFile(p)).ToList();
    }

    private static void MatchSnapshot(string name, string actual, [CallerFilePath] string callerPath = "")
    {
        var path = Path.Combine(Path.GetDirectoryName(callerPath)!, "Snapshots", name + ".g.cs.txt");
        actual = actual.Replace("\r\n", "\n");
        if (Environment.GetEnvironmentVariable("ARC_UPDATE_SNAPSHOTS") == "1" || !File.Exists(path))
        {
            File.WriteAllText(path, actual, new UTF8Encoding(false));
            if (Environment.GetEnvironmentVariable("ARC_UPDATE_SNAPSHOTS") != "1")
            {
                throw new InvalidOperationException($"Snapshot {name} did not exist and was written; review it and run again.");
            }
            return;
        }
        File.ReadAllText(path).Replace("\r\n", "\n").ShouldBe(actual, $"Generator output differs from Snapshots/{name}.g.cs.txt (ARC_UPDATE_SNAPSHOTS=1 rewrites it).");
    }
}
