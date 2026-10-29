using Microsoft.CodeAnalysis;

namespace TKCode.Arc.Foundation.Generators
{
    /// <summary>Diagnostics of the Foundation generator (ids ARCF0xx; design/architecture/foundation.md section 6).</summary>
    internal static class Diagnostics
    {
        private const string Category = "TKCode.Arc.Foundation";

        public static readonly DiagnosticDescriptor UnsupportedTokenType = new DiagnosticDescriptor(
            "ARCF001", "Unsupported token property type",
            "Token '{0}.{1}' has type '{2}', which string overrides cannot parse; use Color, double, int, bool, string, Thickness, CornerRadius, BoxShadows, FontFamily, FontWeight, TimeSpan, Easing or an enum",
            Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor NotATokenClass = new DiagnosticDescriptor(
            "ARCF002", "Token class must derive from AbstractControlDesignToken",
            "'{0}' is marked [ControlDesignToken] but does not derive from TKCode.Arc.AbstractControlDesignToken",
            Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor EmptyTokenClass = new DiagnosticDescriptor(
            "ARCF003", "Token class declares no tokens",
            "'{0}' declares no public settable properties; give the control at least one token or remove the class",
            Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor DuplicateIdentity = new DiagnosticDescriptor(
            "ARCF004", "Duplicate token identity",
            "Token identity '{0}' is declared by both '{1}' and '{2}'",
            Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor MultipleGlobalIdentities = new DiagnosticDescriptor(
            "ARCF005", "More than one global token identity",
            "'{0}' and '{1}' are both marked IsGlobal; an application has a single global token identity",
            Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor InvalidTokenClassShape = new DiagnosticDescriptor(
            "ARCF006", "Token class must be a concrete top-level class",
            "'{0}' must be a non-abstract, non-generic, top-level class with a parameterless constructor",
            Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor TokenClassName = new DiagnosticDescriptor(
            "ARCF007", "Token class name should end with Token",
            "'{0}' does not end with 'Token'; its identity is the whole class name",
            Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor ThemeWithoutClass = new DiagnosticDescriptor(
            "ARCF010", "Theme file needs x:Class",
            "Theme file '{0}' has no x:Class; add x:Class=\"{1}\" so the dictionary can be created without loading XAML by URI (which is not AOT safe)",
            Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor InvalidThemeFile = new DiagnosticDescriptor(
            "ARCF011", "Theme file cannot be read",
            "Theme file '{0}' cannot be read: {1}",
            Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor UnresolvedThemeKey = new DiagnosticDescriptor(
            "ARCF012", "Theme key type not found",
            "Theme file '{0}' keys a resource by '{1}', but no accessible type '{2}' was found; the resource will not be loaded on demand",
            Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor DuplicateThemeKey = new DiagnosticDescriptor(
            "ARCF013", "Theme key defined twice",
            "Resource key '{0}' is defined by both '{1}' and '{2}'",
            Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor EmptyThemeFile = new DiagnosticDescriptor(
            "ARCF014", "Theme file defines no resources",
            "Theme file '{0}' has no keyed top-level resources, so nothing can ever load it",
            Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);
    }
}
