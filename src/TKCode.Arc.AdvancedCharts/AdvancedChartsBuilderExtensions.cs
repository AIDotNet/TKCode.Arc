using TKCode.Arc.Generated.TKCodeArcAdvancedCharts;

namespace TKCode.Arc;

public static class ArcAdvancedChartsBuilderExtensions
{
    /// <summary>
    /// Enables the Arc advanced charts (<see cref="DataGrid"/>) and their built-in text in en-US, zh-CN and zh-TW.
    /// Depends on <see cref="ArcBuilderExtensions.UseArcControls"/>.
    /// </summary>
    public static ArcBuilder UseArcAdvancedCharts(this ArcBuilder builder)
        => builder.UsePackage(ArcGeneratedAssets.PackageId, static package => ArcGeneratedAssets.AddTo(package
            .DependsOn(static b => b.UseArcControls())));
}
