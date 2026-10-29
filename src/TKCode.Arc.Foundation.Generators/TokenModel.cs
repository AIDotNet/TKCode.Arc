using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace TKCode.Arc.Foundation.Generators
{
    /// <summary>How a token property is written and parsed.</summary>
    internal enum TokenValueKind
    {
        Color,
        Double,
        Int32,
        Boolean,
        String,
        Thickness,
        CornerRadius,
        BoxShadows,
        FontFamily,
        TimeSpan,
        Easing,
        Enum
    }

    internal sealed class TokenProperty : IEquatable<TokenProperty>
    {
        public TokenProperty(string name, TokenValueKind kind, string typeName, string displayType, EquatableArray<string> enumMembers)
        {
            Name = name;
            Kind = kind;
            TypeName = typeName;
            DisplayType = displayType;
            EnumMembers = enumMembers;
        }

        public string Name { get; }
        public TokenValueKind Kind { get; }

        /// <summary>Fully qualified type name (global::…).</summary>
        public string TypeName { get; }

        /// <summary>Type as shown in messages.</summary>
        public string DisplayType { get; }

        public EquatableArray<string> EnumMembers { get; }

        public bool Equals(TokenProperty? other)
            => other is not null && Name == other.Name && Kind == other.Kind && TypeName == other.TypeName
               && EnumMembers.Equals(other.EnumMembers);

        public override bool Equals(object? obj) => Equals(obj as TokenProperty);

        public override int GetHashCode() => unchecked(Name.GetHashCode() * 31 + (int)Kind);
    }

    /// <summary>A <c>[ControlDesignToken]</c> class, as the emitter needs it.</summary>
    internal sealed class TokenModel : IEquatable<TokenModel>
    {
        public TokenModel(string className, string fullClassName, string ns, string stem, string identity, bool isGlobal, bool isValid,
                          EquatableArray<TokenProperty> properties, EquatableArray<DiagnosticInfo> diagnostics, DiagnosticInfo location)
        {
            Location = location;
            ClassName = className;
            FullClassName = fullClassName;
            Namespace = ns;
            Stem = stem;
            Identity = identity;
            IsGlobal = isGlobal;
            IsValid = isValid;
            Properties = properties;
            Diagnostics = diagnostics;
        }

        public string ClassName { get; }

        /// <summary>global::Ns.ClassName</summary>
        public string FullClassName { get; }

        public string Namespace { get; }

        /// <summary>The class name without <c>Token</c>: the prefix of the generated type names.</summary>
        public string Stem { get; }

        /// <summary>The identity overrides name: <see cref="Stem"/> unless the attribute sets <c>Identity</c>.</summary>
        public string Identity { get; }
        public bool IsGlobal { get; }

        /// <summary>False when the class cannot be emitted (diagnostics explain why).</summary>
        public bool IsValid { get; }

        public EquatableArray<TokenProperty> Properties { get; }
        public EquatableArray<DiagnosticInfo> Diagnostics { get; }

        /// <summary>Where the class is declared, as a diagnostic template (for the assembly-level checks).</summary>
        public DiagnosticInfo Location { get; }

        public Diagnostic CreateDiagnostic(DiagnosticDescriptor descriptor, params string[] arguments)
            => new DiagnosticInfo(descriptor, Location.FilePath, Location.Span, Location.LineSpan, arguments).ToDiagnostic();

        /// <summary>Namespace of the generated public types.</summary>
        public string TokensNamespace => Namespace.Length == 0 ? "DesignTokens" : Namespace + ".DesignTokens";

        public bool Equals(TokenModel? other)
            => other is not null && FullClassName == other.FullClassName && Stem == other.Stem && Identity == other.Identity
               && IsGlobal == other.IsGlobal && IsValid == other.IsValid
               && Properties.Equals(other.Properties) && Diagnostics.Equals(other.Diagnostics) && Location.Equals(other.Location);

        public override bool Equals(object? obj) => Equals(obj as TokenModel);

        public override int GetHashCode() => FullClassName.GetHashCode();

        private const string AbstractTokenName = "TKCode.Arc.AbstractControlDesignToken";

        public static TokenModel Create(INamedTypeSymbol type, AttributeData attribute)
        {
            var diagnostics = new List<DiagnosticInfo>();
            var location = type.Locations.FirstOrDefault();
            var className = type.Name;
            var ns = type.ContainingNamespace is { IsGlobalNamespace: false } containing ? containing.ToDisplayString() : string.Empty;
            var fullName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var isGlobal = false;
            string? explicitIdentity = null;
            foreach (var named in attribute.NamedArguments)
            {
                if (named.Key == "IsGlobal" && named.Value.Value is bool flag)
                {
                    isGlobal = flag;
                }
                else if (named.Key == "Identity" && named.Value.Value is string text && text.Length > 0)
                {
                    explicitIdentity = text;
                }
            }

            var stem = className;
            if (className.EndsWith("Token", StringComparison.Ordinal) && className.Length > "Token".Length)
            {
                stem = className.Substring(0, className.Length - "Token".Length);
            }
            else
            {
                diagnostics.Add(DiagnosticInfo.Create(Generators.Diagnostics.TokenClassName, location, className));
            }
            var identity = explicitIdentity ?? stem;

            var isValid = true;
            if (!DerivesFrom(type, AbstractTokenName))
            {
                diagnostics.Add(DiagnosticInfo.Create(Generators.Diagnostics.NotATokenClass, location, className));
                isValid = false;
            }
            var hasDefaultCtor = type.InstanceConstructors.Any(c => c.Parameters.Length == 0 && c.DeclaredAccessibility >= Accessibility.Internal);
            if (type.IsAbstract || type.IsGenericType || type.ContainingType is not null || !hasDefaultCtor)
            {
                diagnostics.Add(DiagnosticInfo.Create(Generators.Diagnostics.InvalidTokenClassShape, location, className));
                isValid = false;
            }

            var properties = new List<TokenProperty>();
            if (isValid)
            {
                foreach (var property in CollectProperties(type))
                {
                    var token = Classify(property);
                    if (token is null)
                    {
                        diagnostics.Add(DiagnosticInfo.Create(Generators.Diagnostics.UnsupportedTokenType,
                            property.Locations.FirstOrDefault(), identity, property.Name,
                            property.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
                        continue;
                    }
                    properties.Add(token);
                }
                if (properties.Count == 0 && !diagnostics.Any(d => d.Descriptor == Generators.Diagnostics.UnsupportedTokenType))
                {
                    diagnostics.Add(DiagnosticInfo.Create(Generators.Diagnostics.EmptyTokenClass, location, className));
                }
            }

            return new TokenModel(className, fullName, ns, stem, identity, isGlobal, isValid,
                new EquatableArray<TokenProperty>(properties), new EquatableArray<DiagnosticInfo>(diagnostics),
                DiagnosticInfo.Create(Generators.Diagnostics.DuplicateIdentity, location));
        }

        private static bool DerivesFrom(INamedTypeSymbol type, string baseName)
        {
            for (var current = type.BaseType; current is not null; current = current.BaseType)
            {
                if (current.ToDisplayString() == baseName)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Public instance properties with public get and set, base classes first, up to AbstractControlDesignToken.</summary>
        private static IEnumerable<IPropertySymbol> CollectProperties(INamedTypeSymbol type)
        {
            var chain = new List<INamedTypeSymbol>();
            for (var current = type; current is not null && current.ToDisplayString() != AbstractTokenName; current = current.BaseType)
            {
                chain.Add(current);
            }
            chain.Reverse();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var declaring in chain)
            {
                foreach (var member in declaring.GetMembers())
                {
                    if (member is IPropertySymbol { IsStatic: false, IsIndexer: false, DeclaredAccessibility: Accessibility.Public } property
                        && property.GetMethod is { DeclaredAccessibility: Accessibility.Public }
                        && property.SetMethod is { DeclaredAccessibility: Accessibility.Public, IsInitOnly: false }
                        && seen.Add(property.Name))
                    {
                        yield return property;
                    }
                }
            }
        }

        private static TokenProperty? Classify(IPropertySymbol property)
        {
            var type = property.Type;
            var full = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var display = type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            TokenValueKind? kind = type.SpecialType switch
            {
                SpecialType.System_Double => TokenValueKind.Double,
                SpecialType.System_Int32 => TokenValueKind.Int32,
                SpecialType.System_Boolean => TokenValueKind.Boolean,
                SpecialType.System_String => TokenValueKind.String,
                _ => null
            };
            if (kind is null)
            {
                kind = type.WithNullableAnnotation(NullableAnnotation.NotAnnotated).ToDisplayString() switch
                {
                    "Avalonia.Media.Color" => TokenValueKind.Color,
                    "Avalonia.Thickness" => TokenValueKind.Thickness,
                    "Avalonia.CornerRadius" => TokenValueKind.CornerRadius,
                    "Avalonia.Media.BoxShadows" => TokenValueKind.BoxShadows,
                    "Avalonia.Media.FontFamily" => TokenValueKind.FontFamily,
                    "System.TimeSpan" => TokenValueKind.TimeSpan,
                    "Avalonia.Animation.Easings.Easing" => TokenValueKind.Easing,
                    _ => (TokenValueKind?)null
                };
            }
            var members = EquatableArray<string>.Empty;
            if (kind is null && type.TypeKind == TypeKind.Enum && type is INamedTypeSymbol enumType)
            {
                kind = TokenValueKind.Enum;
                members = new EquatableArray<string>(enumType.GetMembers().OfType<IFieldSymbol>()
                    .Where(f => f.IsConst && f.HasConstantValue).Select(f => f.Name));
            }
            if (kind is null)
            {
                return null;
            }
            if (kind is TokenValueKind.String or TokenValueKind.FontFamily or TokenValueKind.Easing)
            {
                full = type.WithNullableAnnotation(NullableAnnotation.NotAnnotated).ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            }
            return new TokenProperty(property.Name, kind.Value, full, display, members);
        }
    }
}
