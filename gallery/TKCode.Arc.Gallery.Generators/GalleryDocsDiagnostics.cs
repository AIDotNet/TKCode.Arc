using Microsoft.CodeAnalysis;

namespace TKCode.Arc.Gallery.Generators;

internal static class GalleryDocsDiagnostics
{
    private const string Category = "GalleryDocs";

    public static readonly DiagnosticDescriptor UnknownApiType = new(
        "GDOC001", "Unknown API type",
        "'<!-- api: {0} -->' names no public type in the TKCode.Arc assemblies",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MissingSourcePage = new(
        "GDOC002", "Translation without an en-US page",
        "{0} has a {1} page but no en-US.md; en-US is the source every translation is made from",
        Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor OutdatedPage = new(
        "GDOC003", "Translation is outdated",
        "The {1} page of {0} was translated from another en-US.md ({2}); update it and set 'source-hash: {3}'",
        Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MissingSummary = new(
        "GDOC004", "API member without a summary",
        "{0} has no XML <summary>; its API reference row has no description",
        // A member of an API table without a summary has an empty description cell: write the XML <summary>.
        Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor OutdatedApiTranslation = new(
        "GDOC005", "API translation is outdated",
        "The {1} translation of {0} was made from different English text; the summary is now: {2}",
        Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MissingApiTranslation = new(
        "GDOC006", "API translation is missing",
        "The {1} page shows {0} but Docs/Api/{1}.xlf has no translation for it: {2}",
        Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnknownApiTranslation = new(
        "GDOC007", "API translation for an unknown member",
        "'{0}' is not a public type or member of the TKCode.Arc assemblies",
        Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MissingXmlDocumentation = new(
        "GDOC008", "Arc XML documentation not found",
        "No TKCode.Arc XML documentation file reached the generator; API references have no descriptions",
        Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MalformedFile = new(
        "GDOC009", "Malformed docs file",
        "{0}",
        Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnknownDemo = new(
        "GDOC010", "Unknown demo",
        "'<Demo name=\"{0}\" />' names no demo; add Demos/<Category>/{1}/{2}.axaml",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidDemo = new(
        "GDOC011", "Invalid demo",
        "{0} cannot be a demo: {1}",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnusedDemo = new(
        "GDOC012", "Demo not shown",
        "The demo {0} is not shown by the en-US page of {1}; add '<Demo name=\"{2}\" />' to it or remove the file",
        Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);
}
