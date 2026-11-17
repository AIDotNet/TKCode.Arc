namespace TKCode.Arc;

/// <summary>The <c>UseArc(...)</c> entry of TKCode.Arc.Audio.Browser (#149, #151).</summary>
public static class ArcAudioBrowserBuilderExtensions
{
    /// <summary>The package id in <c>ArcRuntime.Packages</c>.</summary>
    internal const string PackageId = "TKCode.Arc.Audio.Browser";

    /// <summary>
    /// Starts the browser audio backend when the app starts (<see cref="ArcAudioBrowser.UseAsync"/>, not awaited). Until it
    /// has loaded, audio is simulated; players that should follow it listen to <see cref="ArcAudio.BackendChanged"/>, as
    /// <c>MediaPlayer</c> does.
    /// </summary>
    public static ArcBuilder UseArcAudioBrowser(this ArcBuilder builder)
        => builder.UsePackage(PackageId, static package => package.OnCommit(static context => { _ = ArcAudioBrowser.UseAsync(); }));
}
