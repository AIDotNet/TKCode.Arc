using System.ComponentModel;

namespace TKCode.Arc;

/// <summary>
/// Generated description of one token class: its identity, token names, resource keys, and reflection-free code that
/// creates the token, writes its values and applies string overrides. One instance per <c>[ControlDesignToken]</c> class,
/// emitted by the Foundation generator; applications do not derive from it.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public abstract class ArcTokenDescriptor
{
    private readonly string[] _tokenNames;
    private readonly object[] _keys;

    /// <summary>Creates a descriptor. Called by generated code only.</summary>
    /// <param name="identity">The identity (token class name without the <c>Token</c> suffix).</param>
    /// <param name="isGlobal">Whether this is the global identity.</param>
    /// <param name="tokenNames">The token (property) names, in <c>XxxTokenKind</c> order.</param>
    /// <param name="keys">The resource keys (boxed <c>XxxTokenKind</c> values), in the same order.</param>
    protected ArcTokenDescriptor(string identity, bool isGlobal, string[] tokenNames, object[] keys)
    {
        if (tokenNames.Length != keys.Length)
        {
            throw new ArgumentException("Every token needs exactly one key.", nameof(keys));
        }
        Identity = identity;
        IsGlobal = isGlobal;
        _tokenNames = tokenNames;
        _keys = keys;
    }

    /// <summary>The identity: the token class name without the <c>Token</c> suffix.</summary>
    public string Identity { get; }

    /// <summary>Whether this is the global identity (receives overrides that name no identity).</summary>
    public bool IsGlobal { get; }

    /// <summary>The token names, in <c>XxxTokenKind</c> order.</summary>
    public IReadOnlyList<string> TokenNames => _tokenNames;

    /// <summary>The resource keys, in <c>XxxTokenKind</c> order. A key is the boxed <c>XxxTokenKind</c> value.</summary>
    public IReadOnlyList<object> Keys => _keys;

    /// <summary>Creates a fresh token instance.</summary>
    public abstract AbstractControlDesignToken CreateToken();

    /// <summary>
    /// Writes the token's values into <paramref name="values"/> (same order as <see cref="Keys"/>). Colors are written as
    /// immutable solid color brushes.
    /// </summary>
    /// <param name="token">A token created by <see cref="CreateToken"/>.</param>
    /// <param name="values">The destination, at least <see cref="Keys"/> long.</param>
    public abstract void Write(AbstractControlDesignToken token, object?[] values);

    /// <summary>
    /// Parses <paramref name="value"/> by the type of token <paramref name="name"/> and assigns it.
    /// </summary>
    /// <param name="token">A token created by <see cref="CreateToken"/>.</param>
    /// <param name="name">The token name.</param>
    /// <param name="value">The value text.</param>
    /// <param name="error">Why the override was rejected, when the method returns false.</param>
    /// <returns>True when the value was parsed and assigned.</returns>
    public abstract bool TrySetToken(AbstractControlDesignToken token, string name, string value, out string? error);

    /// <summary>Message for an unknown token name. Used by generated code.</summary>
    /// <param name="name">The token name that was not found.</param>
    protected string UnknownToken(string name)
        => $"'{Identity}' has no token named '{name}'.";

    /// <summary>Message for a value that does not parse. Used by generated code.</summary>
    /// <param name="name">The token name.</param>
    /// <param name="typeName">The token's type as shown to the user.</param>
    /// <param name="value">The value text.</param>
    protected string InvalidValue(string name, string typeName, string value)
        => $"'{value}' is not a valid {typeName} for token '{Identity}.{name}'.";
}
