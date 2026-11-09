namespace TKCode.Arc;

/// <summary>Resource keys for a <see cref="TKCode.Arc.Primitives.LazyFocusRing"/> whose margin or radius is not a token.</summary>
internal static class FocusRingKeys
{
    /// <summary>
    /// A key no theme defines: the ring keeps the property's default, or the local value its add callback sets (a local value
    /// wins over the ring's token bindings). For rings whose template wrote a literal margin or bound the owner's radius.
    /// </summary>
    public static readonly object None = new();
}
