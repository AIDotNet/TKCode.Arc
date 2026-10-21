using AtomUI;
using AtomUI.Generated.TKCodeArcAdvancedCharts;

namespace TKCode.Arc;

public static class ArcAdvancedChartsBuilderExtensions
{
    /// <summary>
    /// Enables the Arc advanced charts (<see cref="DataGrid"/>) and their built-in text in en-US, zh-CN and zh-TW.
    /// Depends on <see cref="ArcBuilderExtensions.UseArc"/>.
    /// </summary>
    public static IAtomUIBuilder UseArcAdvancedCharts(this IAtomUIBuilder builder)
        => GeneratedControlPackageRegistration.Definition.Register(builder, static package =>
        {
            package.DependsOn(static dependency => dependency.UseArc());
        });
}
