namespace TKCode.Arc;

/// <summary>The <c>UseArc(...)</c> entry of TKCode.Arc.Graphics3D.Browser.</summary>
public static class ArcGraphics3DBrowserBuilderExtensions
{
    /// <summary>The package id in <c>ArcRuntime.Packages</c>.</summary>
    internal const string PackageId = "TKCode.Arc.Graphics3D.Browser";

    /// <summary>
    /// Enables <see cref="ArcGraphics3DBuilderExtensions.UseArcGraphics3D"/> and starts the browser backend when the app
    /// starts (<see cref="ArcGraphics3DBrowser.UseAsync"/>, not awaited). A <see cref="Viewport3D"/> that is on screen
    /// before it has loaded starts drawing when it arrives.
    /// </summary>
    public static ArcBuilder UseArcGraphics3DBrowser(this ArcBuilder builder)
        => builder.UsePackage(PackageId, static package => package
            .DependsOn(static b => b.UseArcGraphics3D())
            .OnCommit(static context => { _ = ArcGraphics3DBrowser.UseAsync(); }));
}
