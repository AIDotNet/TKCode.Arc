using TKCode.Arc.Localization;

namespace TKCode.Arc;

/// <summary>The dock controls' built-in text in the current Arc language (<c>Localization/*.xlf</c>).</summary>
internal static class DockText
{
    public static string Get(DockLangResourceKind kind) => DockLangResources.Get(kind);

    public static string Format(DockLangResourceKind kind, params object?[] arguments) => DockLangResources.Format(kind, arguments);
}
