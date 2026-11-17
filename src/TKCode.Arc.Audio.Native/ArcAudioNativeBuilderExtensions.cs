namespace TKCode.Arc;

/// <summary>The <c>UseArc(...)</c> entry of TKCode.Arc.Audio.Native (#149, #151).</summary>
public static class ArcAudioNativeBuilderExtensions
{
    /// <summary>The package id in <c>ArcRuntime.Packages</c>.</summary>
    internal const string PackageId = "TKCode.Arc.Audio.Native";

    /// <summary>
    /// Turns on the desktop audio backend (miniaudio) when the app starts, as <see cref="ArcAudioNative.Use"/> does: when the
    /// library cannot be loaded, audio stays on the simulated backend and the reason goes to <c>Trace</c>.
    /// </summary>
    public static ArcBuilder UseArcAudioNative(this ArcBuilder builder)
        => builder.UsePackage(PackageId, static package => package.OnCommit(static _ => ArcAudioNative.Use()));
}
