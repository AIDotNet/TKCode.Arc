using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace TKCode.Arc.Foundation.Generators
{
    /// <summary>
    /// Registers every enum marked <c>[EnumMetadata]</c> (or named by <c>[assembly: EnumMetadata(typeof(...))]</c>) with
    /// <c>TKCode.Arc.EnumTypes</c> from a module initializer: its members in declaration order with their values and
    /// bits, the <c>[Description]</c> or <c>[Display(Name)]</c> text, the <c>[Flags]</c> marker, a language catalog key per
    /// member when the attribute names a catalog, and typed converters between the enum and its bits. Controls such as
    /// <c>EnumSelector</c> read it instead of reflecting over the enum's fields (AOT and trimming safe).
    /// </summary>
    [Generator(LanguageNames.CSharp)]
    public sealed class EnumMetadataGenerator : IIncrementalGenerator
    {
        internal const string AttributeName = "TKCode.Arc.EnumMetadataAttribute";
        private const string CatalogAttributeName = "TKCode.Arc.Localization.LanguageCatalogAttribute";
        private const string ModuleInitializerName = "System.Runtime.CompilerServices.ModuleInitializerAttribute";

        /// <inheritdoc />
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var declared = context.SyntaxProvider.ForAttributeWithMetadataName(
                AttributeName,
                static (node, _) => node is EnumDeclarationSyntax,
                static (ctx, cancellation) =>
                {
                    var attribute = ctx.Attributes[0];
                    var location = ctx.TargetSymbol.Locations.FirstOrDefault();
                    return Build((INamedTypeSymbol)ctx.TargetSymbol, attribute, location, ctx.SemanticModel.Compilation, cancellation);
                });

            // Enums of other assemblies (or of this one) named by an assembly attribute. Assembly attributes are few, so
            // reading them from the compilation is cheap; the models compare by their code, so unchanged output is cached.
            var named = context.CompilationProvider.Select(static (compilation, cancellation) =>
            {
                var models = new List<EnumMetadataModel>();
                foreach (var attribute in compilation.Assembly.GetAttributes())
                {
                    if (attribute.AttributeClass?.ToDisplayString() != AttributeName)
                    {
                        continue;
                    }
                    var location = attribute.ApplicationSyntaxReference?.GetSyntax(cancellation).GetLocation();
                    if (attribute.ConstructorArguments.Length != 1 || attribute.ConstructorArguments[0].Value is not INamedTypeSymbol type)
                    {
                        models.Add(EnumMetadataModel.Failed("EnumMetadata.Invalid.g.cs",
                            DiagnosticInfo.Create(Diagnostics.EnumMetadataTarget, location, "(none)")));
                        continue;
                    }
                    models.Add(Build(type, attribute, location, compilation, cancellation));
                }
                return new EquatableArray<EnumMetadataModel>(models);
            });

            var all = declared.Collect().Combine(named);
            context.RegisterSourceOutput(all, static (spc, input) =>
            {
                var emitted = new HashSet<string>(StringComparer.Ordinal);
                foreach (var model in input.Left.Concat(input.Right))
                {
                    foreach (var diagnostic in model.Diagnostics)
                    {
                        spc.ReportDiagnostic(diagnostic.ToDiagnostic());
                    }
                    // An enum both marked and named by the assembly is registered once.
                    if (model.Code is not null && emitted.Add(model.HintName))
                    {
                        spc.AddSource(model.HintName, SourceText.From(model.Code, Encoding.UTF8));
                    }
                }
            });
        }

        internal static EnumMetadataModel Build(INamedTypeSymbol type, AttributeData attribute, Location? location, Compilation compilation,
            System.Threading.CancellationToken cancellation)
        {
            var display = type.ToDisplayString();
            var hint = "EnumMetadata." + SafeName(display) + ".g.cs";
            if (type.TypeKind != TypeKind.Enum || type.EnumUnderlyingType is null)
            {
                return EnumMetadataModel.Failed(hint, DiagnosticInfo.Create(Diagnostics.EnumMetadataTarget, location, display));
            }
            if (!IsAccessible(type, compilation.Assembly))
            {
                return EnumMetadataModel.Failed(hint, DiagnosticInfo.Create(Diagnostics.EnumMetadataAccess, location, display));
            }
            if (compilation.GetTypeByMetadataName(ModuleInitializerName) is null)
            {
                return EnumMetadataModel.Failed(hint, DiagnosticInfo.Create(Diagnostics.EnumMetadataNoModuleInitializer, location, display));
            }

            var diagnostics = new List<DiagnosticInfo>();
            INamedTypeSymbol? catalog = null;
            string? prefix = null;
            foreach (var argument in attribute.NamedArguments)
            {
                if (argument.Key == "LanguageCatalog" && argument.Value.Value is INamedTypeSymbol catalogType)
                {
                    if (catalogType.TypeKind != TypeKind.Enum
                        || !catalogType.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == CatalogAttributeName))
                    {
                        diagnostics.Add(DiagnosticInfo.Create(Diagnostics.EnumMetadataCatalog, location, display, catalogType.ToDisplayString()));
                    }
                    else
                    {
                        catalog = catalogType;
                    }
                }
                else if (argument.Key == "KeyPrefix" && argument.Value.Value is string text)
                {
                    prefix = text;
                }
            }
            prefix ??= type.Name;
            var catalogMembers = catalog is null
                ? new HashSet<string>(StringComparer.Ordinal)
                : new HashSet<string>(catalog.GetMembers().OfType<IFieldSymbol>().Where(f => f.HasConstantValue).Select(f => f.Name), StringComparer.Ordinal);

            var name = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var underlying = type.EnumUnderlyingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var isFlags = type.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "System.FlagsAttribute");

            var builder = new StringBuilder();
            builder.AppendLine("// <auto-generated/>");
            builder.AppendLine("#nullable enable");
            builder.AppendLine("#pragma warning disable CS0612, CS0618, CA2255");
            builder.AppendLine();
            builder.Append("internal static class __ArcEnumMetadata_").Append(SafeName(display).Replace('.', '_')).AppendLine();
            builder.AppendLine("{");
            builder.AppendLine("    [global::System.Runtime.CompilerServices.ModuleInitializer]");
            builder.AppendLine("    internal static void Register()");
            builder.AppendLine("    {");
            builder.Append("        global::TKCode.Arc.EnumTypes.Register(typeof(").Append(name).AppendLine("), static () =>");
            builder.Append("            new global::TKCode.Arc.EnumTypeInfo(typeof(").Append(name).Append("), ")
                .Append(isFlags ? "true" : "false").AppendLine(",");
            builder.Append("                static value => unchecked((ulong)(").Append(underlying).Append(")(").Append(name).AppendLine(")value),");
            builder.Append("                static bits => (").Append(name).Append(")unchecked((").Append(underlying).AppendLine(")bits),");
            builder.AppendLine("                new global::TKCode.Arc.EnumMemberInfo[]");
            builder.AppendLine("                {");
            var seen = new HashSet<ulong>();
            foreach (var member in type.GetMembers().OfType<IFieldSymbol>())
            {
                cancellation.ThrowIfCancellationRequested();
                if (!member.HasConstantValue || IsHidden(member))
                {
                    continue;
                }
                var bits = ToBits(member.ConstantValue);
                // Aliases (two names, one value) would select together; the first declared one stands for the value.
                if (!seen.Add(bits))
                {
                    continue;
                }
                builder.Append("                    new global::TKCode.Arc.EnumMemberInfo(").Append(Literal(member.Name)).Append(", ")
                    .Append(name).Append('.').Append(Identifier(member.Name)).Append(", 0x")
                    .Append(bits.ToString("X", CultureInfo.InvariantCulture)).Append("UL)");
                var text = TextOf(member);
                string? key = null;
                if (catalog is not null)
                {
                    var keyName = prefix + member.Name;
                    if (catalogMembers.Contains(keyName))
                    {
                        key = catalog.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + "." + Identifier(keyName);
                    }
                    else
                    {
                        diagnostics.Add(DiagnosticInfo.Create(Diagnostics.EnumMetadataMissingText,
                            member.Locations.FirstOrDefault() ?? location, display + "." + member.Name, catalog.ToDisplayString(), keyName));
                    }
                }
                if (text is null && key is null)
                {
                    builder.AppendLine(",");
                    continue;
                }
                builder.AppendLine();
                builder.AppendLine("                    {");
                if (text is not null)
                {
                    builder.Append("                        DisplayName = ").Append(Literal(text)).AppendLine(",");
                }
                if (key is not null)
                {
                    builder.Append("                        TextKey = ").Append(key).AppendLine(",");
                }
                builder.AppendLine("                    },");
            }
            builder.AppendLine("                }));");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            return new EnumMetadataModel(hint, builder.ToString(), new EquatableArray<DiagnosticInfo>(diagnostics));
        }

        /// <summary>The value as 64 bits; signed values are sign-extended, as the generated converter does at run time.</summary>
        private static ulong ToBits(object? value) => value switch
        {
            sbyte v => unchecked((ulong)v),
            byte v => v,
            short v => unchecked((ulong)v),
            ushort v => v,
            int v => unchecked((ulong)v),
            uint v => v,
            long v => unchecked((ulong)v),
            ulong v => v,
            _ => 0
        };

        private static bool IsHidden(IFieldSymbol field)
            => field.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "System.ComponentModel.BrowsableAttribute"
                                              && a.ConstructorArguments.Length == 1 && a.ConstructorArguments[0].Value is false);

        /// <summary><c>[Display(Name)]</c>, else <c>[Description]</c>; null when the member has neither.</summary>
        private static string? TextOf(IFieldSymbol field)
        {
            string? description = null;
            foreach (var attribute in field.GetAttributes())
            {
                var attributeName = attribute.AttributeClass?.ToDisplayString();
                if (attributeName == "System.ComponentModel.DataAnnotations.DisplayAttribute")
                {
                    foreach (var named in attribute.NamedArguments)
                    {
                        if (named.Key == "Name" && named.Value.Value is string display)
                        {
                            return display;
                        }
                    }
                }
                else if (attributeName == "System.ComponentModel.DescriptionAttribute" && attribute.ConstructorArguments.Length == 1
                         && attribute.ConstructorArguments[0].Value is string text)
                {
                    description = text;
                }
            }
            return description;
        }

        /// <summary>Reachable from generated code in <paramref name="assembly"/>: public, or internal to it, all the way out.</summary>
        private static bool IsAccessible(INamedTypeSymbol type, IAssemblySymbol assembly)
        {
            var sameAssembly = SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, assembly);
            for (var current = type; current is not null; current = current.ContainingType)
            {
                var accessibility = current.DeclaredAccessibility;
                if (accessibility == Accessibility.Public)
                {
                    continue;
                }
                if (sameAssembly && accessibility is Accessibility.Internal or Accessibility.ProtectedOrInternal)
                {
                    continue;
                }
                return false;
            }
            return true;
        }

        /// <summary>A member named like a keyword (<c>@default</c>) keeps its verbatim prefix in generated code.</summary>
        private static string Identifier(string name) => SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ? "@" + name : name;

        private static string Literal(string value) => SymbolDisplay.FormatLiteral(value, quote: true);

        private static string SafeName(string name)
        {
            var builder = new StringBuilder(name.Length);
            foreach (var c in name)
            {
                builder.Append(char.IsLetterOrDigit(c) || c == '.' || c == '_' ? c : '_');
            }
            return builder.ToString();
        }
    }

    /// <summary>The generated registration of one enum (the code is the cache key).</summary>
    internal sealed class EnumMetadataModel : IEquatable<EnumMetadataModel>
    {
        public EnumMetadataModel(string hintName, string? code, EquatableArray<DiagnosticInfo> diagnostics)
        {
            HintName = hintName;
            Code = code;
            Diagnostics = diagnostics;
        }

        public string HintName { get; }
        public string? Code { get; }
        public EquatableArray<DiagnosticInfo> Diagnostics { get; }

        public static EnumMetadataModel Failed(string hintName, DiagnosticInfo diagnostic)
            => new EnumMetadataModel(hintName, null, new EquatableArray<DiagnosticInfo>(ImmutableArray.Create(diagnostic)));

        public bool Equals(EnumMetadataModel? other)
            => other is not null && HintName == other.HintName && Code == other.Code && Diagnostics.Equals(other.Diagnostics);

        public override bool Equals(object? obj) => Equals(obj as EnumMetadataModel);

        public override int GetHashCode() => HintName.GetHashCode();
    }
}
