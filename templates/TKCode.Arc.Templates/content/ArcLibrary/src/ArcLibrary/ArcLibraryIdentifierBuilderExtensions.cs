using TKCode.Arc;
using TKCode.Arc.Generated.ArcLibraryIdentifier;

namespace ArcLibrary;

/// <summary>Registers the library with Arc.</summary>
public static class ArcLibraryIdentifierBuilderExtensions
{
    /// <summary>
    /// Enables the library's controls: their themes load on first use and their design tokens are published with Arc's.
    /// Call it inside <c>UseArc(...)</c>: <c>builder.UseArc(arc =&gt; arc.UseArcLibraryIdentifier())</c>. It registers
    /// Arc's own controls first (<see cref="ArcBuilderExtensions.UseArcControls"/>), since the themes use them.
    /// </summary>
    public static ArcBuilder UseArcLibraryIdentifier(this ArcBuilder builder)
        => builder.UsePackage(ArcGeneratedAssets.PackageId, static package => ArcGeneratedAssets.AddTo(package
            .DependsOn(static b => b.UseArcControls())));
}
