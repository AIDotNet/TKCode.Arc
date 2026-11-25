using TKCode.Arc.Generated.TKCodeArcEditorControls;

namespace TKCode.Arc;

/// <summary>Registers the TKCode.Arc.Editor.Controls package.</summary>
public static class ArcCodeEditorBuilderExtensions
{
    /// <summary>
    /// Enables the Arc code editor controls (<see cref="CodeEditor"/>). Depends on <see cref="ArcBuilderExtensions.UseArcControls"/>.
    /// The engine itself (<c>TKCode.Arc.Editor</c>) needs no registration.
    /// </summary>
    public static ArcBuilder UseArcCodeEditor(this ArcBuilder builder)
        => builder.UsePackage(ArcGeneratedAssets.PackageId, static package => ArcGeneratedAssets.AddTo(package
            .DependsOn(static b => b.UseArcControls())));
}
