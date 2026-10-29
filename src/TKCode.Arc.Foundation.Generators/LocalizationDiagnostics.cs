using Microsoft.CodeAnalysis;

namespace TKCode.Arc.Foundation.Generators
{
    /// <summary>Diagnostics of the localization part of the Foundation generator (ids ARCF020–ARCF029).</summary>
    internal static class LocalizationDiagnostics
    {
        private const string Category = "TKCode.Arc.Foundation.Localization";

        public static readonly DiagnosticDescriptor InvalidLanguageFile = new DiagnosticDescriptor(
            "ARCF020", "Language file cannot be read",
            "Language file '{0}' cannot be read: {1}",
            Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor MissingText = new DiagnosticDescriptor(
            "ARCF021", "Text missing in a language",
            "'{0}' has no {1} text for '{2}.{3}'",
            Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor UnknownUnit = new DiagnosticDescriptor(
            "ARCF022", "Text for a member the catalog does not declare",
            "'{0}' has text for '{1}', but '{2}' declares no such member; the text is ignored",
            Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor Duplicate = new DiagnosticDescriptor(
            "ARCF023", "Text defined twice",
            "{0}",
            Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor MissingSource = new DiagnosticDescriptor(
            "ARCF024", "Catalog has no source-language file",
            "Language catalog '{0}' has no source-language file (a Localization/<language>.xlf without trgLang whose <file id> is '{0}')",
            Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor PlaceholderMismatch = new DiagnosticDescriptor(
            "ARCF025", "Placeholders differ from the source text",
            "'{0}': the {1} text of '{2}.{3}' uses placeholders {4} but the source text uses {5}",
            Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor InvalidCatalog = new DiagnosticDescriptor(
            "ARCF026", "Language catalog must be a top-level enum with distinct values",
            "Language catalog '{0}' {1}",
            Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor UnknownCatalog = new DiagnosticDescriptor(
            "ARCF027", "Language file names no catalog",
            "'{0}' has text for '{1}', which is not a [LanguageCatalog] enum of this assembly; the text is ignored",
            Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);
    }
}
