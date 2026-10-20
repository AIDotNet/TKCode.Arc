using AtomUI;
using AtomUI.Desktop.Controls;
using AtomUI.Generated.TKCodeArcCore;

namespace TKCode.Arc;

public static class ArcCoreBuilderExtensions
{
    /// <summary>Enables the Arc core package (design tokens, motion, primitives).</summary>
    public static IAtomUIBuilder UseArcCore(this IAtomUIBuilder builder)
        => GeneratedControlPackageRegistration.Definition.Register(builder, static package => package
            .DependsOn(static dependency => dependency.UseDesktopControls()));
}
