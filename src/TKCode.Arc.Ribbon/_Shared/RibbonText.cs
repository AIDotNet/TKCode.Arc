using TKCode.Arc.Localization;

namespace TKCode.Arc;

/// <summary>The ribbon's built-in text in the current language.</summary>
internal static class RibbonText
{
    public static string Get(RibbonLangResourceKind kind) => RibbonLangResources.Get(kind);

    public static string Format(RibbonLangResourceKind kind, params object?[] arguments) => RibbonLangResources.Format(kind, arguments);
}
