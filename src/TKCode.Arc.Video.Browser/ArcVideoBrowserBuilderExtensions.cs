namespace TKCode.Arc;

/// <summary>The <c>UseArc(...)</c> entry of TKCode.Arc.Video.Browser (#149, #151).</summary>
public static class ArcVideoBrowserBuilderExtensions
{
    /// <summary>The package id in <c>ArcRuntime.Packages</c>.</summary>
    internal const string PackageId = "TKCode.Arc.Video.Browser";

    /// <summary>
    /// Enables <see cref="ArcVideoBuilderExtensions.UseArcVideo"/> and starts the browser video backend when the app starts
    /// (<see cref="ArcVideoBrowser.UseAsync"/>, not awaited). A <see cref="VideoPlayer"/> created before it has loaded moves to
    /// it when it arrives (<see cref="ArcVideo.BackendChanged"/>).
    /// </summary>
    public static ArcBuilder UseArcVideoBrowser(this ArcBuilder builder)
        => builder.UsePackage(PackageId, static package => package
            .DependsOn(static b => b.UseArcVideo())
            .OnCommit(static context => { _ = ArcVideoBrowser.UseAsync(); }));
}
