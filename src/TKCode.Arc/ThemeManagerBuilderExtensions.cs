using AtomUI;
using AtomUI.Desktop.Controls;
using AtomUI.Generated.TKCodeArc;

namespace TKCode.Arc;

public static class ArcBuilderExtensions
{
    /// <summary>
    /// Enables Arc controls. Depends on <see cref="ArcCoreBuilderExtensions.UseArcCore"/> and AtomUI desktop controls.
    /// </summary>
    public static IAtomUIBuilder UseArc(this IAtomUIBuilder builder)
        => GeneratedControlPackageRegistration.Definition.Register(builder, static package =>
        {
            package.DependsOn(static dependency => dependency.UseArcCore());
            package.DependsOn(static dependency => dependency.UseDesktopControls());
        });
}
