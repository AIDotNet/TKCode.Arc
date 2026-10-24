using AtomUI;
using AtomUI.Generated.TKCodeArcWorkflow;

namespace TKCode.Arc;

public static class ArcWorkflowBuilderExtensions
{
    /// <summary>
    /// Enables the Arc workflow canvas (<see cref="WorkflowCanvas"/>) and its built-in text in en-US, zh-CN and zh-TW. Depends on
    /// <see cref="ArcBuilderExtensions.UseArc"/>.
    /// </summary>
    public static IAtomUIBuilder UseArcWorkflow(this IAtomUIBuilder builder)
        => GeneratedControlPackageRegistration.Definition.Register(builder, static package =>
        {
            package.DependsOn(static dependency => dependency.UseArc());
        });
}
