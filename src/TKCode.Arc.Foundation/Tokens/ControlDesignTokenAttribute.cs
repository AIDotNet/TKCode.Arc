namespace TKCode.Arc;

/// <summary>
/// Marks a token class (<c>XxxToken : AbstractControlDesignToken</c>) for the Foundation generator. The generator emits
/// <c>XxxTokenKind</c>, <c>XxxTokens</c> (keys and identity), the <c>XxxTokenResource</c> markup extension, a writer that
/// publishes the properties into a resource dictionary and a parser for string overrides, all without reflection.
/// </summary>
/// <remarks>
/// The identity of the tokens is the class name without the <c>Token</c> suffix (<c>ButtonToken</c> is <c>Button</c>).
/// Every public settable property is a token; its type must be one the generator can parse (see
/// design/architecture/foundation.md).
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ControlDesignTokenAttribute : Attribute
{
    /// <summary>
    /// Whether this is the global token identity (Arc's <c>ArcDesign</c>): it receives the overrides that name no identity
    /// (<see cref="ArcThemeBuilder.Token(string, string)"/>). An application has at most one global identity.
    /// </summary>
    public bool IsGlobal { get; set; }

    /// <summary>
    /// The identity overrides name, when it must differ from the class name without <c>Token</c> (two packages that an
    /// application enables together declare a token class of the same name, such as Mobile's <c>ButtonToken</c> next to
    /// the desktop one). The generated types keep the class-name stem (<c>ButtonTokenKind</c>, <c>{arcm:ButtonTokenResource}</c>).
    /// </summary>
    public string? Identity { get; set; }
}
