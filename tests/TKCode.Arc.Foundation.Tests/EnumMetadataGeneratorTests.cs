using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TKCode.Arc.Foundation.Generators;

namespace TKCode.Arc.Foundation.Tests;

/// <summary>
/// Drives <see cref="EnumMetadataGenerator"/> against stand-ins for the TKCode.Arc runtime types (this project does not
/// reference TKCode.Arc), checks that the generated registration compiles and runs, and compares it with Snapshots/. Set
/// ARC_UPDATE_SNAPSHOTS=1 to rewrite the snapshots after an intended change.
/// </summary>
public class EnumMetadataGeneratorTests
{
    private const string Runtime = """
        #nullable enable
        using System;
        using System.Collections.Generic;
        namespace TKCode.Arc
        {
            [AttributeUsage(AttributeTargets.Enum | AttributeTargets.Assembly, AllowMultiple = true)]
            public sealed class EnumMetadataAttribute : Attribute
            {
                public EnumMetadataAttribute() { }
                public EnumMetadataAttribute(Type enumType) { EnumType = enumType; }
                public Type? EnumType { get; }
                public Type? LanguageCatalog { get; set; }
                public string? KeyPrefix { get; set; }
            }
            public sealed class EnumMemberInfo
            {
                public EnumMemberInfo(string name, object value, ulong bits) { Name = name; Value = value; Bits = bits; }
                public string Name { get; }
                public object Value { get; }
                public ulong Bits { get; }
                public string? DisplayName { get; init; }
                public object? TextKey { get; init; }
            }
            public sealed class EnumTypeInfo
            {
                public EnumTypeInfo(Type type, bool isFlags, Func<object, ulong> toBits, Func<ulong, object> fromBits, IReadOnlyList<EnumMemberInfo> members)
                {
                    Type = type; IsFlags = isFlags; ToBits = toBits; FromBits = fromBits; Members = members;
                }
                public Type Type { get; }
                public bool IsFlags { get; }
                public Func<object, ulong> ToBits { get; }
                public Func<ulong, object> FromBits { get; }
                public IReadOnlyList<EnumMemberInfo> Members { get; }
            }
            public static class EnumTypes
            {
                public static readonly Dictionary<Type, Func<EnumTypeInfo>> Registered = new();
                public static void Register(Type type, Func<EnumTypeInfo> factory) => Registered[type] = factory;
            }
        }
        namespace TKCode.Arc.Localization
        {
            [AttributeUsage(AttributeTargets.Enum)] public sealed class LanguageCatalogAttribute : Attribute { }
        }
        """;

    private const string Enums = """
        using System;
        using System.ComponentModel;
        using System.ComponentModel.DataAnnotations;
        using TKCode.Arc;
        using TKCode.Arc.Localization;

        [assembly: EnumMetadata(typeof(System.DayOfWeek))]
        [assembly: EnumMetadata(typeof(Demo.Model.Priority))]
        [assembly: EnumMetadata(typeof(string))]

        namespace Demo.Model
        {
            [EnumMetadata]
            public enum Priority
            {
                [Description("Low priority")] Low,
                Medium,
                [Display(Name = "High!"), Description("ignored")] High,
                [Browsable(false)] Hidden,
                Default = Medium,
                @class = 10
            }

            [Flags, EnumMetadata(LanguageCatalog = typeof(DemoLangResourceKind))]
            public enum FilePermissions : byte
            {
                None = 0,
                Read = 1,
                Write = 2,
                Execute = 4,
                ReadWrite = Read | Write,
                All = 0xFF
            }

            [Flags, EnumMetadata]
            public enum Wide : long
            {
                Negative = -2,
                High = 1L << 62
            }

            [LanguageCatalog]
            public enum DemoLangResourceKind
            {
                FilePermissionsNone,
                FilePermissionsRead,
                FilePermissionsWrite,
                FilePermissionsExecute,
                FilePermissionsReadWrite
            }

            [EnumMetadata(LanguageCatalog = typeof(Priority))]
            public enum NotACatalog
            {
                One
            }

            internal sealed class Outer
            {
                [EnumMetadata]
                private enum Inaccessible { A }
            }
        }
        """;

    [Fact]
    public void Enums_register_members_that_compile()
    {
        var (sources, diagnostics, errors, _) = Run(Enums);

        errors.ShouldBeEmpty(string.Join("\n", errors));
        sources.Keys.Order(StringComparer.Ordinal).ShouldBe([
            "EnumMetadata.Demo.Model.FilePermissions.g.cs",
            "EnumMetadata.Demo.Model.NotACatalog.g.cs",
            "EnumMetadata.Demo.Model.Priority.g.cs",
            "EnumMetadata.Demo.Model.Wide.g.cs",
            "EnumMetadata.System.DayOfWeek.g.cs"
        ]);
        MatchSnapshot("EnumMetadata.Priority", sources["EnumMetadata.Demo.Model.Priority.g.cs"]);
        MatchSnapshot("EnumMetadata.FilePermissions", sources["EnumMetadata.Demo.Model.FilePermissions.g.cs"]);

        // string is not an enum, Inaccessible is private, NotACatalog names a non-catalog, All has no catalog text.
        diagnostics.Select(d => d.Id).Order(StringComparer.Ordinal).ShouldBe(["ARCF033", "ARCF034", "ARCF036", "ARCF037"]);
        diagnostics.Single(d => d.Id == "ARCF037").GetMessage(System.Globalization.CultureInfo.InvariantCulture)
            .ShouldContain("'Demo.Model.FilePermissions.All' has no text in the language catalog 'Demo.Model.DemoLangResourceKind' (expected a member named 'FilePermissionsAll')");
    }

    [Fact]
    public void Generated_registrations_run_and_convert_values_and_bits()
    {
        var (_, _, errors, assembly) = Run(Enums);
        errors.ShouldBeEmpty();
        assembly.ShouldNotBeNull();

        var types = assembly.GetType("TKCode.Arc.EnumTypes")!;
        var registered = (System.Collections.IDictionary)types.GetField("Registered")!.GetValue(null)!;
        // Module initializers run when the assembly loads; touching a type runs them.
        registered.Count.ShouldBe(5);

        object Info(string name) => ((Delegate)registered[assembly.GetType(name) ?? typeof(DayOfWeek)]!).DynamicInvoke()!;
        static object? Get(object target, string property) => target.GetType().GetProperty(property)!.GetValue(target);

        var permissions = Info("Demo.Model.FilePermissions");
        Get(permissions, "IsFlags").ShouldBe(true);
        ((System.Collections.IList)Get(permissions, "Members")!).Count.ShouldBe(6);
        var toBits = (Func<object, ulong>)Get(permissions, "ToBits")!;
        var fromBits = (Func<ulong, object>)Get(permissions, "FromBits")!;
        var permissionsType = assembly.GetType("Demo.Model.FilePermissions")!;
        toBits(Enum.ToObject(permissionsType, 3)).ShouldBe(3UL);
        // Bits beyond the underlying byte are dropped.
        fromBits(0x105UL).ShouldBe(Enum.ToObject(permissionsType, 5));

        var wide = Info("Demo.Model.Wide");
        var wideType = assembly.GetType("Demo.Model.Wide")!;
        ((Func<object, ulong>)Get(wide, "ToBits")!)(Enum.ToObject(wideType, -2L)).ShouldBe(unchecked((ulong)-2L));
        ((Func<ulong, object>)Get(wide, "FromBits")!)(unchecked((ulong)-2L)).ShouldBe(Enum.ToObject(wideType, -2L));

        var priority = Info("Demo.Model.Priority");
        Get(priority, "IsFlags").ShouldBe(false);
        var names = new List<string>();
        foreach (var member in (System.Collections.IEnumerable)Get(priority, "Members")!)
        {
            names.Add((string)Get(member, "Name")! + "=" + ((string?)Get(member, "DisplayName") ?? "-"));
        }
        // Hidden is [Browsable(false)]; Default has Medium's value and is left out.
        names.ShouldBe(["Low=Low priority", "Medium=-", "High=High!", "class=-"]);

        var days = Info("System.DayOfWeek");
        ((System.Collections.IList)Get(days, "Members")!).Count.ShouldBe(7);
    }

    private static (Dictionary<string, string> Sources, IReadOnlyList<Diagnostic> Diagnostics, List<Diagnostic> Errors, Assembly? Assembly) Run(string source)
    {
        var parse = new CSharpParseOptions(LanguageVersion.Latest);
        var compilation = CSharpCompilation.Create("Demo.Enums." + Guid.NewGuid().ToString("N"),
            [CSharpSyntaxTree.ParseText(Runtime, parse), CSharpSyntaxTree.ParseText(source, parse)],
            References(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new EnumMetadataGenerator().AsSourceGenerator()], parseOptions: parse);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        var result = driver.GetRunResult().Results.Single();
        var sources = result.GeneratedSources.ToDictionary(s => s.HintName, s => s.SourceText.ToString());
        var errors = output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assembly? assembly = null;
        if (errors.Count == 0)
        {
            using var stream = new MemoryStream();
            var emit = output.Emit(stream);
            if (emit.Success)
            {
                stream.Position = 0;
                assembly = new AssemblyLoadContext(null, isCollectible: true).LoadFromStream(stream);
                RuntimeHelpers.RunModuleConstructor(assembly.ManifestModule.ModuleHandle);
            }
            else
            {
                errors.AddRange(emit.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            }
        }
        return (sources, result.Diagnostics, errors, assembly);
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
