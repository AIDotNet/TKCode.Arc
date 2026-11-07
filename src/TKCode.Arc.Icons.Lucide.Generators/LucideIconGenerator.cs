using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace TKCode.Arc.Icons.Lucide.Generators
{
    /// <summary>
    /// Compiles into the consuming assembly the path data of the Lucide icons it uses, and registers them with
    /// <c>LucideIconData</c> from a module initializer, so an app ships only its own icons. An icon counts as used when:
    /// <list type="bullet">
    /// <item>C# names it as <c>LucideIconKind.Name</c> (any qualification; aliases and <c>using static</c> are not followed);</item>
    /// <item>a XAML file (an AdditionalFiles item ending in .axaml or .xaml; Avalonia adds them) sets it on a
    /// <c>LucideIcon</c> (<c>Kind="Name"</c>, property element, or a style setter of <c>Kind</c>), uses it in
    /// <c>LucideGeometry</c>, or names <c>LucideIconKind.Name</c> (x:Static);</item>
    /// <item>the project declares it: <c>&lt;LucideIcon Include="Bell;arrow-right" /&gt;</c> (PascalCase or Lucide's own name);</item>
    /// <item>the project sets <c>LucideIconsIncludeAll=true</c> (every icon, for icons chosen at run time).</item>
    /// </list>
    /// </summary>
    [Generator(LanguageNames.CSharp)]
    public sealed class LucideIconGenerator : IIncrementalGenerator
    {
        private const string KindTypeName = "TKCode.Arc.Icons.Lucide.LucideIconKind";
        private const string DataTypeName = "TKCode.Arc.Icons.Lucide.LucideIconData";

        private static readonly DiagnosticDescriptor UnknownDeclaredIcon = new(
            "ARCL001",
            "Unknown Lucide icon",
            "LucideIcon item '{0}' is not a Lucide icon; it is ignored",
            "TKCode.Arc.Icons.Lucide",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor BoundKind = new(
            "ARCL002",
            "Bound Lucide icon",
            "LucideIcon.Kind is bound here, so the generator cannot tell which icons it shows: the ones that are not used elsewhere draw nothing. "
            + "Declare them in the project file with <LucideIcon Include=\"Bell;ArrowRight\" />, or compile in every icon with <LucideIconsIncludeAll>true</LucideIconsIncludeAll>.",
            "TKCode.Arc.Icons.Lucide",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Reported for a binding (or any markup extension other than x:Static) on LucideIcon.Kind in XAML, as an attribute, a property element or a style setter. "
                         + "Not reported once the project declares any <LucideIcon> item or sets LucideIconsIncludeAll; <NoWarn>ARCL002</NoWarn> silences it too.");

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var fromCode = context.SyntaxProvider
                .CreateSyntaxProvider(
                    static (node, _) => node is MemberAccessExpressionSyntax access && RightmostName(access.Expression) == "LucideIconKind",
                    static (ctx, ct) => KindFieldName(ctx.SemanticModel.GetSymbolInfo(ctx.Node, ct).Symbol))
                .Where(static name => name is not null)
                .Collect();

            var fromXaml = context.AdditionalTextsProvider
                .Where(static file => file.Path.EndsWith(".axaml", StringComparison.OrdinalIgnoreCase)
                                      || file.Path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
                .Select(static (file, ct) => XamlScanner.Scan(file.Path, file.GetText(ct)))
                .Collect();

            var settings = context.AnalyzerConfigOptionsProvider.Select(static (options, _) =>
            {
                options.GlobalOptions.TryGetValue("build_property.LucideIconsIncludeAll", out var all);
                options.GlobalOptions.TryGetValue("build_property._LucideIconsDeclared", out var declared);
                return new Settings(string.Equals(all?.Trim(), "true", StringComparison.OrdinalIgnoreCase), declared?.Trim() ?? string.Empty);
            });

            var target = context.CompilationProvider.Select(static (compilation, _) =>
                compilation.GetTypeByMetadataName(KindTypeName) is not null && compilation.GetTypeByMetadataName(DataTypeName) is not null);

            context.RegisterSourceOutput(fromCode.Combine(fromXaml).Combine(settings).Combine(target), static (spc, input) =>
            {
                var (((code, xaml), options), referencesLucide) = input;
                if (!referencesLucide)
                {
                    return;
                }
                var paths = LucidePaths.All;
                var used = new SortedSet<string>(StringComparer.Ordinal);
                if (options.IncludeAll)
                {
                    used.UnionWith(paths.Keys);
                }
                foreach (var name in code)
                {
                    used.Add(name!);
                }
                foreach (var scan in xaml)
                {
                    foreach (var name in scan.Names.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        used.Add(name);
                    }
                }
                if (!options.IncludeAll && options.Declared.Length == 0)
                {
                    foreach (var scan in xaml)
                    {
                        foreach (var location in scan.BindingLocations())
                        {
                            spc.ReportDiagnostic(Diagnostic.Create(BoundKind, location));
                        }
                    }
                }
                foreach (var item in options.Declared.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var name = item.Trim();
                    if (name.Length == 0)
                    {
                        continue;
                    }
                    var pascal = paths.ContainsKey(name) ? name : LucidePaths.Pascal(name);
                    if (paths.ContainsKey(pascal))
                    {
                        used.Add(pascal);
                    }
                    else
                    {
                        spc.ReportDiagnostic(Diagnostic.Create(UnknownDeclaredIcon, Location.None, name));
                    }
                }
                used.RemoveWhere(name => !paths.ContainsKey(name));
                if (used.Count == 0)
                {
                    return;
                }
                spc.AddSource("LucideIcons.Registration.g.cs", SourceText.From(Emit(used, paths), Encoding.UTF8));
            });
        }

        private static string? RightmostName(ExpressionSyntax expression) => expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
            AliasQualifiedNameSyntax alias => alias.Name.Identifier.ValueText,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
            _ => null
        };

        private static string? KindFieldName(ISymbol? symbol)
            => symbol is IFieldSymbol { ContainingType: { TypeKind: TypeKind.Enum } type } field
               && type.ToDisplayString() == KindTypeName
                ? field.Name
                : null;

        internal static string Emit(IEnumerable<string> names, IReadOnlyDictionary<string, string> paths)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("// The Lucide icons this assembly uses (TKCode.Arc.Icons.Lucide generator). Lucide: ISC license, Copyright (c) Lucide Contributors.");
            sb.AppendLine("#pragma warning disable CA2255");
            sb.AppendLine();
            sb.AppendLine("namespace TKCode.Arc.Icons.Lucide.Generated");
            sb.AppendLine("{");
            sb.AppendLine("    internal static class LucideIconRegistration");
            sb.AppendLine("    {");
            sb.AppendLine("        [global::System.Runtime.CompilerServices.ModuleInitializer]");
            sb.AppendLine("        internal static void Register()");
            sb.AppendLine("        {");
            foreach (var name in names)
            {
                sb.Append("            global::TKCode.Arc.Icons.Lucide.LucideIconData.Register(global::TKCode.Arc.Icons.Lucide.LucideIconKind.")
                  .Append(name).Append(", \"").Append(paths[name]).AppendLine("\");");
            }
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private sealed class Settings : IEquatable<Settings>
        {
            public Settings(bool includeAll, string declared)
            {
                IncludeAll = includeAll;
                Declared = declared;
            }

            public bool IncludeAll { get; }

            public string Declared { get; }

            public bool Equals(Settings? other) => other is not null && other.IncludeAll == IncludeAll && other.Declared == Declared;

            public override bool Equals(object? obj) => Equals(obj as Settings);

            public override int GetHashCode() => (IncludeAll ? 1 : 0) ^ Declared.GetHashCode();
        }
    }

    /// <summary>What one XAML file says about Lucide icons; equatable, so unchanged files are not looked at again.</summary>
    internal sealed class XamlScan : IEquatable<XamlScan>
    {
        public static readonly XamlScan Empty = new(string.Empty, string.Empty, string.Empty);

        public XamlScan(string path, string names, string bindings)
        {
            Path = path;
            Names = names;
            Bindings = bindings;
        }

        public string Path { get; }

        /// <summary>The icon names used, sorted and joined with ';'.</summary>
        public string Names { get; }

        /// <summary>Where Kind is bound: "start,length,line,column,endLine,endColumn" entries joined with ';'.</summary>
        public string Bindings { get; }

        public IEnumerable<Location> BindingLocations()
        {
            foreach (var entry in Bindings.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var n = entry.Split(',').Select(int.Parse).ToArray();
                yield return Location.Create(Path, new TextSpan(n[0], n[1]),
                    new LinePositionSpan(new LinePosition(n[2], n[3]), new LinePosition(n[4], n[5])));
            }
        }

        public bool Equals(XamlScan? other) => other is not null && other.Path == Path && other.Names == Names && other.Bindings == Bindings;

        public override bool Equals(object? obj) => Equals(obj as XamlScan);

        public override int GetHashCode() => (Path.GetHashCode() * 31 + Names.GetHashCode()) * 31 + Bindings.GetHashCode();
    }

    /// <summary>Finds Lucide icon names, and bindings of LucideIcon.Kind, in XAML text.</summary>
    internal static class XamlScanner
    {
        private const string Name = @"([A-Za-z_][A-Za-z0-9_]*)";

        private static readonly Regex Comment = new(@"<!--.*?-->", RegexOptions.Singleline | RegexOptions.CultureInvariant);

        // LucideIconKind.Bell, as in {x:Static lucide:LucideIconKind.Bell}.
        private static readonly Regex KindMember = new(@"\bLucideIconKind\." + Name, RegexOptions.CultureInvariant);

        // <lucide:LucideIcon ... Kind="Bell" ...> and <lucide:LucideGeometry Kind="Bell" />.
        private static readonly Regex Element = new(@"<(?:[\w.]+:)?(LucideIcon|LucideGeometry(?:Extension)?)(?=[\s/>])([^>]*)>",
            RegexOptions.CultureInvariant);

        private static readonly Regex KindAttribute = new(@"(?<![\w.:])Kind\s*=\s*""\s*" + Name + @"\s*""", RegexOptions.CultureInvariant);

        // Kind="{Binding Icon}", or any markup extension but x:Static (whose LucideIconKind.Name KindMember finds).
        private static readonly Regex BoundKindAttribute = new(@"(?<![\w.:])Kind\s*=\s*""\s*\{(?!\s*x:Static\b)[^""]*""", RegexOptions.CultureInvariant);

        // <lucide:LucideIcon.Kind>Bell</lucide:LucideIcon.Kind>.
        private static readonly Regex PropertyElement = new(@"<(?:[\w.]+:)?LucideIcon\.Kind\s*>\s*" + Name + @"\s*<", RegexOptions.CultureInvariant);

        // <lucide:LucideIcon.Kind><Binding ... /></lucide:LucideIcon.Kind>.
        private static readonly Regex BoundPropertyElement = new(@"<(?:[\w.]+:)?LucideIcon\.Kind\s*>\s*<(?:[\w.]+:)?(?:Binding|CompiledBinding|ReflectionBinding|MultiBinding|TemplateBinding)\b",
            RegexOptions.CultureInvariant);

        // {lucide:LucideGeometry Bell} and {lucide:LucideGeometry Kind=Bell}.
        private static readonly Regex MarkupExtension = new(@"\{\s*(?:[\w.]+:)?LucideGeometry(?:Extension)?\s+(?:Kind\s*=\s*)?" + Name + @"\s*[,}]",
            RegexOptions.CultureInvariant);

        // <Setter Property="Kind" Value="Bell" /> in a file that styles LucideIcon.
        private static readonly Regex Setter = new(@"<Setter\b([^>]*)>", RegexOptions.CultureInvariant);

        private static readonly Regex SetterKindProperty = new(@"\bProperty\s*=\s*""(?:[\w.]+:)?(?:LucideIcon\.)?Kind""", RegexOptions.CultureInvariant);

        private static readonly Regex SetterValue = new(@"\bValue\s*=\s*""\s*" + Name + @"\s*""", RegexOptions.CultureInvariant);

        private static readonly Regex BoundSetterValue = new(@"\bValue\s*=\s*""\s*\{(?!\s*x:Static\b)[^""]*""", RegexOptions.CultureInvariant);

        public static XamlScan Scan(string path, SourceText? source)
        {
            var original = source?.ToString();
            if (source is null || original is null || original.IndexOf("Lucide", StringComparison.Ordinal) < 0)
            {
                return XamlScan.Empty;
            }
            // Blank comments out at the same length, so offsets still point into the file.
            var text = Comment.Replace(original, m => new string(' ', m.Length));
            var paths = LucidePaths.All;
            var found = new SortedSet<string>(StringComparer.Ordinal);
            var bindings = new List<string>();

            void Add(string name)
            {
                if (paths.ContainsKey(name))
                {
                    found.Add(name);
                }
            }

            void Bound(int start, int length)
            {
                var span = source.Lines.GetLinePositionSpan(new TextSpan(start, length));
                bindings.Add(string.Join(",", start, length, span.Start.Line, span.Start.Character, span.End.Line, span.End.Character));
            }

            foreach (Match m in KindMember.Matches(text))
            {
                Add(m.Groups[1].Value);
            }
            foreach (Match element in Element.Matches(text))
            {
                var attributes = element.Groups[2];
                foreach (Match m in KindAttribute.Matches(attributes.Value))
                {
                    Add(m.Groups[1].Value);
                }
                if (element.Groups[1].Value == "LucideIcon")
                {
                    foreach (Match m in BoundKindAttribute.Matches(attributes.Value))
                    {
                        Bound(attributes.Index + m.Index, m.Length);
                    }
                }
            }
            foreach (Match m in PropertyElement.Matches(text))
            {
                Add(m.Groups[1].Value);
            }
            foreach (Match m in BoundPropertyElement.Matches(text))
            {
                Bound(m.Index, m.Length);
            }
            foreach (Match m in MarkupExtension.Matches(text))
            {
                Add(m.Groups[1].Value);
            }
            if (text.IndexOf("LucideIcon", StringComparison.Ordinal) >= 0)
            {
                foreach (Match setter in Setter.Matches(text))
                {
                    var attributes = setter.Groups[1].Value;
                    if (!SetterKindProperty.IsMatch(attributes))
                    {
                        continue;
                    }
                    if (SetterValue.Match(attributes) is { Success: true } value)
                    {
                        Add(value.Groups[1].Value);
                    }
                    else if (BoundSetterValue.Match(attributes) is { Success: true } bound)
                    {
                        Bound(setter.Groups[1].Index + bound.Index, bound.Length);
                    }
                }
            }
            return new XamlScan(path, string.Join(";", found), string.Join(";", bindings));
        }
    }

    /// <summary>Every Lucide icon's path data (24x24 viewBox), keyed by its LucideIconKind name; read once from LucidePaths.txt.</summary>
    internal static class LucidePaths
    {
        private static IReadOnlyDictionary<string, string>? s_all;

        public static IReadOnlyDictionary<string, string> All => s_all ??= Load();

        /// <summary>The LucideIconKind name of a Lucide file name (arrow-right → ArrowRight), as generate_lucide.py does.</summary>
        public static string Pascal(string name)
        {
            var sb = new StringBuilder();
            foreach (var part in name.Split('-', '_'))
            {
                if (part.Length > 0)
                {
                    sb.Append(char.ToUpperInvariant(part[0])).Append(part, 1, part.Length - 1);
                }
            }
            if (sb.Length > 0 && char.IsDigit(sb[0]))
            {
                sb.Insert(0, "Icon");
            }
            return sb.ToString();
        }

        private static IReadOnlyDictionary<string, string> Load()
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            using var stream = typeof(LucidePaths).Assembly.GetManifestResourceStream("LucidePaths.txt")
                               ?? throw new InvalidOperationException("LucidePaths.txt is not embedded in the Lucide generator.");
            using var reader = new StreamReader(stream, Encoding.UTF8);
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }
                var tab = line.IndexOf('\t');
                if (tab > 0)
                {
                    map[line.Substring(0, tab)] = line.Substring(tab + 1);
                }
            }
            return map;
        }
    }
}
