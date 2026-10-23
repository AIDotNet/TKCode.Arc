using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Shared text settings of <see cref="PromptCache"/>.</summary>
internal static class PromptCacheText
{
    /// <summary>Tabular figures, so numbers keep their width while they roll.</summary>
    public static readonly FontFeatureCollection Tabular = new() { FontFeature.Parse("tnum") };
}
