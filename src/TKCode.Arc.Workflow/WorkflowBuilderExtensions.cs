using TKCode.Arc.Generated.TKCodeArcWorkflow;

namespace TKCode.Arc;

public static class ArcWorkflowBuilderExtensions
{
    /// <summary>
    /// Enables the Arc workflow canvas (<see cref="WorkflowCanvas"/>) and its built-in text in en-US, zh-CN and zh-TW. Depends on
    /// <see cref="ArcBuilderExtensions.UseArcControls"/>.
    /// </summary>
    public static ArcBuilder UseArcWorkflow(this ArcBuilder builder)
        => builder.UsePackage(ArcGeneratedAssets.PackageId, static package => ArcGeneratedAssets.AddTo(package
            .DependsOn(static b => b.UseArcControls())));
}
