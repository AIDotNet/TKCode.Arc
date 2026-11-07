using TKCode.Arc.Generated.TKCodeArcVideo;

namespace TKCode.Arc;

/// <summary>Registers the TKCode.Arc.Video package.</summary>
public static class ArcVideoBuilderExtensions
{
    /// <summary>
    /// Enables the Arc video controls (<see cref="VideoPlayer"/>, <see cref="VideoSurface"/>) and their built-in text in en-US,
    /// zh-CN and zh-TW. Depends on <see cref="ArcBuilderExtensions.UseArcControls"/>. Playback backends are chosen separately
    /// (<see cref="ArcVideo.Backend"/>; <c>ArcVideoNative.Use()</c> on desktop, <c>ArcVideoBrowser.UseAsync()</c> in a browser).
    /// </summary>
    public static ArcBuilder UseArcVideo(this ArcBuilder builder)
        => builder.UsePackage(ArcGeneratedAssets.PackageId, static package => ArcGeneratedAssets.AddTo(package
            .DependsOn(static b => b.UseArcControls())));
}
