using AtomUI;
using AtomUI.Generated.TKCodeArcBlocks;

namespace TKCode.Arc;

public static class ArcBlocksBuilderExtensions
{
    /// <summary>
    /// Enables the Arc blocks (page-level compositions such as the command palette). Depends on
    /// <see cref="ArcBuilderExtensions.UseArc"/>.
    /// </summary>
    public static IAtomUIBuilder UseArcBlocks(this IAtomUIBuilder builder)
        => GeneratedControlPackageRegistration.Definition.Register(builder, static package =>
        {
            package.DependsOn(static dependency => dependency.UseArc());
        });
}
