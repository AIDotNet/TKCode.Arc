namespace TKCode.Arc;

/// <summary>The <c>UseArc(...)</c> entry of TKCode.Arc.Video.Native (#149, #151).</summary>
public static class ArcVideoNativeBuilderExtensions
{
    /// <summary>The package id in <c>ArcRuntime.Packages</c>.</summary>
    internal const string PackageId = "TKCode.Arc.Video.Native";

    /// <summary>
    /// Enables <see cref="ArcVideoBuilderExtensions.UseArcVideo"/> and turns on the platform video backend (Media Foundation,
    /// AVFoundation, GStreamer or Android's player) when the app starts, as <see cref="ArcVideoNative.Use"/> does: when it is
    /// not available, video stays on the simulated backend and the reason goes to <c>Trace</c>.
    /// </summary>
    public static ArcBuilder UseArcVideoNative(this ArcBuilder builder)
        => builder.UsePackage(PackageId, static package => package
            .DependsOn(static b => b.UseArcVideo())
            .OnCommit(static _ => ArcVideoNative.Use()));
}
