using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace TKCode.Arc.Gallery.Generators;

internal sealed class ApiMember
{
    public ApiMember(bool isEvent, string name, string type, string key, string? summary, string? fieldExpression,
                     bool isReadOnly, bool isAttached)
    {
        IsEvent = isEvent;
        Name = name;
        Type = type;
        Key = key;
        Summary = summary;
        FieldExpression = fieldExpression;
        IsReadOnly = isReadOnly;
        IsAttached = isAttached;
    }

    public bool IsEvent { get; }

    public string Name { get; }

    /// <summary>The property's value type, or the event's argument type.</summary>
    public string Type { get; }

    /// <summary><c>DeclaringType.Member</c>: the translation key.</summary>
    public string Key { get; }

    public string? Summary { get; }

    /// <summary>The <c>AvaloniaProperty</c> field, whose metadata gives the default value at runtime.</summary>
    public string? FieldExpression { get; }

    public bool IsReadOnly { get; }

    public bool IsAttached { get; }
}

/// <summary>
/// The API reference of one control: the Avalonia properties and events declared by the control and its Arc base
/// classes (TKCode.Arc and TKCode.Arc.Core), its pseudo-classes, and the first non-Arc base class it inherits the rest
/// from.
/// </summary>
internal sealed class ApiType
{
    private static readonly SymbolDisplayFormat s_typeFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
                              SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier |
                              SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

    private ApiType(INamedTypeSymbol symbol, string? summary, string? baseName)
    {
        Name = ApiName(symbol);
        TypeExpression = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        Package = symbol.ContainingAssembly.Name;
        Summary = summary;
        BaseName = baseName;
    }

    public string Name { get; }

    public string TypeExpression { get; }

    /// <summary>The assembly that defines the type, which is also its NuGet package.</summary>
    public string Package { get; }

    public string? Summary { get; }

    public string? BaseName { get; }

    public List<string> PseudoClasses { get; } = new();

    public List<ApiMember> Members { get; } = new();

    public static bool IsArc(ITypeSymbol? type)
        => type?.ContainingAssembly?.Name is { } name && (name == "TKCode.Arc" || name.StartsWith("TKCode.Arc.", System.StringComparison.Ordinal));

    /// <summary>
    /// The name a type goes by in the docs (the <c>api:</c> line, the table's heading, the translation keys): its own name,
    /// except for the types of <c>TKCode.Arc.Mobile</c>, which carry a <c>Mobile</c> prefix (<c>MobileButton</c>) because they
    /// share short names with the desktop controls (<c>Button</c>, <c>Switch</c>) and the docs key types by name.
    /// </summary>
    public static string ApiName(INamedTypeSymbol type)
        => type.ContainingNamespace?.ToDisplayString() == MobileNamespace ? "Mobile" + type.Name : type.Name;

    private const string MobileNamespace = "TKCode.Arc.Mobile";

    /// <summary>
    /// A public type of the TKCode.Arc assemblies referenced by <paramref name="compilation"/>: <c>TKCode.Arc.&lt;name&gt;</c>,
    /// or, for a name that starts with <c>Mobile</c> and names no such type, <c>TKCode.Arc.Mobile.&lt;rest&gt;</c> (see <see cref="ApiName"/>).
    /// </summary>
    public static INamedTypeSymbol? Find(Compilation compilation, string name)
    {
        foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            if (IsArcAssembly(assembly) && assembly.GetTypeByMetadataName("TKCode.Arc." + name) is { DeclaredAccessibility: Accessibility.Public } type)
            {
                return type;
            }
        }
        if (name.Length > "Mobile".Length && name.StartsWith("Mobile", System.StringComparison.Ordinal))
        {
            foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
            {
                if (IsArcAssembly(assembly) &&
                    assembly.GetTypeByMetadataName(MobileNamespace + "." + name.Substring("Mobile".Length)) is { DeclaredAccessibility: Accessibility.Public } mobile)
                {
                    return mobile;
                }
            }
        }
        return null;
    }

    /// <summary>Whether <paramref name="key"/> (<c>Type</c> or <c>Type.Member</c>) names something the docs can translate.</summary>
    public static bool IsKnownKey(Compilation compilation, string key)
    {
        var dot = key.IndexOf('.');
        var type = Find(compilation, dot < 0 ? key : key.Substring(0, dot));
        if (type is null || dot < 0)
        {
            return type is not null;
        }
        var member = key.Substring(dot + 1);
        return type.GetMembers(member + "Property").Concat(type.GetMembers(member + "Event")).Concat(type.GetMembers(member))
                   .Any(m => m.DeclaredAccessibility == Accessibility.Public);
    }

    private static bool IsArcAssembly(IAssemblySymbol assembly)
        => assembly.Name == "TKCode.Arc" || assembly.Name.StartsWith("TKCode.Arc.", System.StringComparison.Ordinal);

    public static ApiType Build(INamedTypeSymbol type, XmlDocs docs, List<Diagnostic> diagnostics)
    {
        var chain = new List<INamedTypeSymbol>();
        var current = type;
        while (current is not null && IsArc(current))
        {
            chain.Add(current);
            current = current.BaseType;
        }
        var baseName = current is null || current.SpecialType == SpecialType.System_Object ? null : current.ToDisplayString(s_typeFormat);
        var result = new ApiType(type, docs.Summary(type), baseName);

        var names = new HashSet<string>(System.StringComparer.Ordinal);
        var events = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (var owner in chain)
        {
            foreach (var field in owner.GetMembers().OfType<IFieldSymbol>())
            {
                if (!field.IsStatic || field.DeclaredAccessibility != Accessibility.Public || IsHidden(field))
                {
                    continue;
                }
                if (PropertyValueType(field.Type, out var isAttached, out var isDirect) is { } valueType)
                {
                    var name = Trim(field.Name, "Property");
                    if (!names.Add(name))
                    {
                        continue;
                    }
                    var clr = FindClrProperty(chain, name);
                    var isReadOnly = isAttached
                        ? !owner.GetMembers("Set" + name).Any(m => m.DeclaredAccessibility == Accessibility.Public)
                        : clr is not null && clr.SetMethod is not { DeclaredAccessibility: Accessibility.Public };
                    var key = ApiName(owner) + "." + name;
                    var summary = docs.Summary(field, clr);
                    Require(summary, key, docs, diagnostics);
                    result.Members.Add(new ApiMember(false, name, valueType.ToDisplayString(s_typeFormat), key, summary,
                        isDirect ? null : field.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + "." + field.Name,
                        isReadOnly, isAttached));
                }
                else if (RoutedEventArgsType(field.Type) is { } argsType)
                {
                    var name = Trim(field.Name, "Event");
                    if (!events.Add(name))
                    {
                        continue;
                    }
                    var clr = chain.SelectMany(t => t.GetMembers(name)).OfType<IEventSymbol>().FirstOrDefault();
                    var key = ApiName(owner) + "." + name;
                    var summary = docs.Summary(field, clr);
                    Require(summary, key, docs, diagnostics);
                    result.Members.Add(new ApiMember(true, name, argsType, key, summary, null, false, false));
                }
            }
            foreach (var clrEvent in owner.GetMembers().OfType<IEventSymbol>())
            {
                if (clrEvent.IsStatic || clrEvent.DeclaredAccessibility != Accessibility.Public || IsHidden(clrEvent) || !events.Add(clrEvent.Name))
                {
                    continue;
                }
                var key = ApiName(owner) + "." + clrEvent.Name;
                var summary = docs.Summary(clrEvent);
                Require(summary, key, docs, diagnostics);
                result.Members.Add(new ApiMember(true, clrEvent.Name, EventArgsType(clrEvent.Type), key, summary, null, false, false));
            }
            foreach (var attribute in owner.GetAttributes())
            {
                if (attribute.AttributeClass?.ToDisplayString() == "Avalonia.Controls.Metadata.PseudoClassesAttribute" &&
                    attribute.ConstructorArguments.Length == 1)
                {
                    foreach (var value in attribute.ConstructorArguments[0].Values)
                    {
                        if (value.Value is string pseudo && !result.PseudoClasses.Contains(pseudo))
                        {
                            result.PseudoClasses.Add(pseudo);
                        }
                    }
                }
            }
        }
        return result;
    }

    private static void Require(string? summary, string key, XmlDocs docs, List<Diagnostic> diagnostics)
    {
        // Without the documentation file every member would warn; GDOC008 reports that once instead.
        if (summary is null && !docs.IsEmpty)
        {
            diagnostics.Add(Diagnostic.Create(GalleryDocsDiagnostics.MissingSummary, Location.None, key));
        }
    }

    private static IPropertySymbol? FindClrProperty(List<INamedTypeSymbol> chain, string name)
        => chain.SelectMany(t => t.GetMembers(name)).OfType<IPropertySymbol>().FirstOrDefault(p => !p.IsStatic);

    /// <summary>The value type of a <c>StyledProperty&lt;T&gt;</c>, <c>AttachedProperty&lt;T&gt;</c> or <c>DirectProperty&lt;TOwner, T&gt;</c> field.</summary>
    private static ITypeSymbol? PropertyValueType(ITypeSymbol fieldType, out bool isAttached, out bool isDirect)
    {
        isAttached = false;
        isDirect = false;
        for (var type = fieldType as INamedTypeSymbol; type is not null; type = type.BaseType)
        {
            switch (type.OriginalDefinition.ToDisplayString())
            {
                case "Avalonia.AttachedProperty<TValue>":
                    isAttached = true;
                    return type.TypeArguments[0];
                case "Avalonia.StyledProperty<TValue>":
                    return type.TypeArguments[0];
                case "Avalonia.DirectProperty<TOwner, TValue>":
                    isDirect = true;
                    return type.TypeArguments[1];
                case "Avalonia.DirectPropertyBase<TValue>":
                    isDirect = true;
                    return type.TypeArguments[0];
            }
        }
        return null;
    }

    private static string? RoutedEventArgsType(ITypeSymbol fieldType)
    {
        for (var type = fieldType as INamedTypeSymbol; type is not null; type = type.BaseType)
        {
            var name = type.OriginalDefinition.ToDisplayString();
            if (name == "Avalonia.Interactivity.RoutedEvent<TEventArgs>")
            {
                return type.TypeArguments[0].ToDisplayString(s_typeFormat);
            }
            if (name == "Avalonia.Interactivity.RoutedEvent")
            {
                return "RoutedEventArgs";
            }
        }
        return null;
    }

    private static string EventArgsType(ITypeSymbol delegateType)
        => delegateType is INamedTypeSymbol { IsGenericType: true } named && named.OriginalDefinition.ToDisplayString() == "System.EventHandler<TEventArgs>"
            ? named.TypeArguments[0].ToDisplayString(s_typeFormat)
            : delegateType.ToDisplayString() == "System.EventHandler" ? "EventArgs" : delegateType.ToDisplayString(s_typeFormat);

    private static bool IsHidden(ISymbol symbol)
        => symbol.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() is "System.ObsoleteAttribute" ||
                                           a.AttributeClass?.ToDisplayString() == "System.ComponentModel.EditorBrowsableAttribute" &&
                                           a.ConstructorArguments.Length == 1 && a.ConstructorArguments[0].Value is 1);

    private static string Trim(string name, string suffix)
        => name.EndsWith(suffix, System.StringComparison.Ordinal) && name.Length > suffix.Length ? name.Substring(0, name.Length - suffix.Length) : name;
}
