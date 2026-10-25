using AtomUI;
using AtomUI.Generated.TKCodeArcMobile;
using TKCode.Arc;

namespace TKCode.Arc.Mobile;

public static class ArcMobileBuilderExtensions
{
    /// <summary>
    /// Enables the Arc mobile controls (<see cref="Button"/>, <see cref="Switch"/>, <see cref="Page"/>,
    /// <see cref="Navbar"/>, <see cref="Tabbar"/>). Depends on <see cref="ArcCoreBuilderExtensions.UseArcCore"/> and does
    /// not enable the desktop Arc controls.
    /// </summary>
    public static IAtomUIBuilder UseArcMobile(this IAtomUIBuilder builder)
        => GeneratedControlPackageRegistration.Definition.Register(builder, static package =>
        {
            package.DependsOn(static dependency => dependency.UseArcCore());
        });
}
