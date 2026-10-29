using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace TKCode.Arc.Foundation.Generators
{
    /// <summary>
    /// Generates, for every <c>[ControlDesignToken]</c> class, its token kinds, keys, markup extension, writer and
    /// override parser; for every <c>Themes/*Theme.axaml</c> file, the dictionary class that instantiates it; and per
    /// assembly the <c>ArcGeneratedAssets</c> table a package registers (design/architecture/foundation.md).
    /// </summary>
    [Generator(LanguageNames.CSharp)]
    public sealed class ArcFoundationGenerator : IIncrementalGenerator
    {
        private const string AttributeName = "TKCode.Arc.ControlDesignTokenAttribute";
        private const string FoundationMarker = "TKCode.Arc.ArcTokenDescriptor";
        private const string CatalogAttributeName = "TKCode.Arc.Localization.LanguageCatalogAttribute";

        /// <inheritdoc />
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var tokens = context.SyntaxProvider.ForAttributeWithMetadataName(
                    AttributeName,
                    static (node, _) => node is ClassDeclarationSyntax,
                    static (ctx, _) => TokenModel.Create((INamedTypeSymbol)ctx.TargetSymbol, ctx.Attributes[0]))
                .Collect();

            var projectDirectory = context.AnalyzerConfigOptionsProvider.Select(static (options, _) =>
            {
                options.GlobalOptions.TryGetValue("build_property.projectdir", out var directory);
                options.GlobalOptions.TryGetValue("build_property.rootnamespace", out var rootNamespace);
                return (Directory: directory, RootNamespace: rootNamespace);
            });

            var themes = context.AdditionalTextsProvider
                .Where(static file => ThemeFile.IsThemeFile(file.Path))
                .Combine(projectDirectory)
                .Select(static (pair, cancellation) => ThemeFile.Parse(pair.Left.Path,
                    ThemeFile.MakeRelative(pair.Left.Path, pair.Right.Directory), pair.Left.GetText(cancellation)))
                .Collect();

            // Language catalogs: [LanguageCatalog] enums and Localization/*.xlf (LocalizationEmitter).
            var catalogs = context.SyntaxProvider.ForAttributeWithMetadataName(
                    CatalogAttributeName,
                    static (node, _) => node is EnumDeclarationSyntax,
                    static (ctx, _) => CatalogModel.Create((INamedTypeSymbol)ctx.TargetSymbol))
                .Collect();
            var languageFiles = context.AdditionalTextsProvider
                .Where(static file => XlfFile.IsLanguageFile(file.Path))
                .Combine(projectDirectory)
                .Select(static (pair, cancellation) => XlfFile.Parse(pair.Left.Path,
                    ThemeFile.MakeRelative(pair.Left.Path, pair.Right.Directory), pair.Left.GetText(cancellation)))
                .Collect();
            var languages = catalogs.Combine(languageFiles);

            var input = tokens.Combine(themes).Combine(projectDirectory).Combine(languages).Combine(context.CompilationProvider);
            context.RegisterSourceOutput(input, static (spc, source) =>
            {
                var ((((tokenModels, themeFiles), project), (catalogModels, xlfFiles)), compilation) = source;
                Execute(spc, tokenModels, themeFiles, catalogModels, xlfFiles, project.RootNamespace, compilation);
            });
        }

        private static void Execute(SourceProductionContext context, ImmutableArray<TokenModel> tokenModels,
                                    ImmutableArray<ThemeFile> themeFiles, ImmutableArray<CatalogModel> catalogModels,
                                    ImmutableArray<XlfFile> xlfFiles, string? rootNamespace, Compilation compilation)
        {
            if (compilation.GetTypeByMetadataName(FoundationMarker) is null)
            {
                // The project does not reference TKCode.Arc.Foundation: nothing to generate against.
                return;
            }

            // Tokens.
            var validTokens = new List<TokenModel>();
            var identities = new Dictionary<string, TokenModel>(StringComparer.Ordinal);
            TokenModel? global = null;
            foreach (var model in tokenModels.OrderBy(m => m.FullClassName, StringComparer.Ordinal))
            {
                foreach (var diagnostic in model.Diagnostics)
                {
                    context.ReportDiagnostic(diagnostic.ToDiagnostic());
                }
                if (!model.IsValid)
                {
                    continue;
                }
                if (identities.TryGetValue(model.Identity, out var other))
                {
                    context.ReportDiagnostic(model.CreateDiagnostic(Diagnostics.DuplicateIdentity,
                        model.Identity, other.FullClassName, model.FullClassName));
                    continue;
                }
                identities.Add(model.Identity, model);
                if (model.IsGlobal)
                {
                    if (global is not null)
                    {
                        context.ReportDiagnostic(model.CreateDiagnostic(Diagnostics.MultipleGlobalIdentities,
                            global.FullClassName, model.FullClassName));
                    }
                    else
                    {
                        global = model;
                    }
                }
                validTokens.Add(model);
                context.AddSource(model.Stem + "Token.g.cs", SourceText.From(TokenEmitter.Emit(model), Encoding.UTF8));
            }

            // Themes.
            var xmlns = XmlnsMap.Build(compilation);
            var assets = new List<(ThemeFile File, string ClassName, List<string> Keys)>();
            var themeKeys = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var file in themeFiles.OrderBy(f => f.RelativePath, StringComparer.Ordinal))
            {
                if (file.Error is not null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Diagnostics.InvalidThemeFile,
                        Location.Create(file.Path, default, default), file.RelativePath, file.Error));
                    continue;
                }
                if (file.ClassName is null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Diagnostics.ThemeWithoutClass,
                        Location.Create(file.Path, default, default), file.RelativePath,
                        ThemeFile.SuggestClassName(file.RelativePath, rootNamespace)));
                    continue;
                }
                var keys = new List<string>();
                foreach (var key in file.Keys)
                {
                    string expression;
                    string identity;
                    if (key.IsType)
                    {
                        var type = xmlns.Resolve(compilation, key.XmlNamespace!, key.TypeName!);
                        if (type is null)
                        {
                            context.ReportDiagnostic(Diagnostic.Create(Diagnostics.UnresolvedThemeKey,
                                Location.Create(file.Path, default, file.LineSpan(key)), file.RelativePath, key.Text, key.TypeName!));
                            continue;
                        }
                        var typeName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                        expression = "typeof(" + typeName + ")";
                        identity = typeName;
                    }
                    else
                    {
                        expression = "\"" + key.Text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
                        identity = "\"" + key.Text + "\"";
                    }
                    if (themeKeys.TryGetValue(identity, out var owner))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(Diagnostics.DuplicateThemeKey,
                            Location.Create(file.Path, default, file.LineSpan(key)), key.Text, owner, file.RelativePath));
                        continue;
                    }
                    themeKeys.Add(identity, file.RelativePath);
                    keys.Add(expression);
                }
                if (file.Keys.Count == 0)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Diagnostics.EmptyThemeFile,
                        Location.Create(file.Path, default, default), file.RelativePath));
                }
                var className = file.ClassName;
                // A dictionary class the project declares itself (with its own constructor) is used as is.
                if (compilation.GetTypeByMetadataName(className) is null)
                {
                    context.AddSource(SafeFileName(className) + ".g.cs",
                        SourceText.From(ThemeEmitter.EmitDictionaryClass(file, className), Encoding.UTF8));
                }
                assets.Add((file, className, keys));
            }

            // Language catalogs.
            var catalogs = LocalizationEmitter.Execute(context, catalogModels, xlfFiles);

            if (validTokens.Count == 0 && assets.Count == 0 && catalogs.Count == 0)
            {
                return;
            }
            context.AddSource("ArcGeneratedAssets.g.cs",
                SourceText.From(ThemeEmitter.EmitAssets(compilation.AssemblyName ?? "Assembly", validTokens, assets, catalogs), Encoding.UTF8));
        }

        private static string SafeFileName(string name)
        {
            var builder = new StringBuilder(name.Length);
            foreach (var c in name)
            {
                builder.Append(char.IsLetterOrDigit(c) || c == '.' || c == '_' ? c : '_');
            }
            return builder.ToString();
        }
    }

    /// <summary>XML namespace → CLR namespaces, from the [XmlnsDefinition] attributes of the compilation and its references.</summary>
    internal sealed class XmlnsMap
    {
        private readonly Dictionary<string, List<string>> _map = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        public static XmlnsMap Build(Compilation compilation)
        {
            var map = new XmlnsMap();
            map.Collect(compilation.Assembly);
            foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
            {
                map.Collect(assembly);
            }
            return map;
        }

        private void Collect(IAssemblySymbol assembly)
        {
            foreach (var attribute in assembly.GetAttributes())
            {
                if (attribute.AttributeClass?.ToDisplayString() != "Avalonia.Metadata.XmlnsDefinitionAttribute"
                    || attribute.ConstructorArguments.Length < 2
                    || attribute.ConstructorArguments[0].Value is not string xml
                    || attribute.ConstructorArguments[1].Value is not string clr)
                {
                    continue;
                }
                if (!_map.TryGetValue(xml, out var list))
                {
                    list = new List<string>();
                    _map.Add(xml, list);
                }
                if (!list.Contains(clr))
                {
                    list.Add(clr);
                }
            }
        }

        public INamedTypeSymbol? Resolve(Compilation compilation, string xmlNamespace, string typeName)
        {
            IEnumerable<string> namespaces;
            if (xmlNamespace.StartsWith("clr-namespace:", StringComparison.Ordinal) || xmlNamespace.StartsWith("using:", StringComparison.Ordinal))
            {
                var value = xmlNamespace.Substring(xmlNamespace.IndexOf(':') + 1);
                var semicolon = value.IndexOf(';');
                namespaces = new[] { semicolon >= 0 ? value.Substring(0, semicolon) : value };
            }
            else if (_map.TryGetValue(xmlNamespace, out var list))
            {
                namespaces = list;
            }
            else
            {
                return null;
            }
            foreach (var ns in namespaces)
            {
                var type = compilation.GetTypeByMetadataName(ns + "." + typeName);
                if (type is not null && compilation.IsSymbolAccessibleWithin(type, compilation.Assembly))
                {
                    return type;
                }
            }
            return null;
        }
    }
}
