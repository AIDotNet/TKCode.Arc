using Avalonia;

namespace TKCode.Arc.Tests.Special;

/// <summary>Visual-tree lookup the Special tests share (SpeculativeDecodingTests moved to the gallery repository, which keeps its own copy).</summary>
internal static class SpeculativeDecodingTestExtensions
{
    public static IEnumerable<T> GetVisualDescendantsOfType<T>(this Visual root) where T : Visual
        => Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(root).OfType<T>();
}
