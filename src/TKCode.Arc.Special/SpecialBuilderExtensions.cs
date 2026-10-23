using AtomUI;
using AtomUI.Generated.TKCodeArcSpecial;

namespace TKCode.Arc;

public static class ArcSpecialBuilderExtensions
{
    /// <summary>
    /// Enables the Arc special components (the animated AI explainers such as <c>AgentLoop</c>). Depends on
    /// <see cref="ArcBuilderExtensions.UseArc"/>.
    /// </summary>
    public static IAtomUIBuilder UseArcSpecial(this IAtomUIBuilder builder)
        => GeneratedControlPackageRegistration.Definition.Register(builder, static package =>
        {
            package.DependsOn(static dependency => dependency.UseArc());
        });
}
