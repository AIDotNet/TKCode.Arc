using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Font families used by Arc. Inter comes from Avalonia.Fonts.Inter (<c>WithInterFont()</c>, <c>fonts:Inter</c>);
/// Geist and Geist Mono from TKCode.Arc.Fonts (<c>WithArcFonts()</c>, <c>fonts:Geist</c> / <c>fonts:GeistMono</c>).
/// Each falls back to the next family, then to the default font, when not registered.
/// </summary>
public static class ArcFonts
{
    public static readonly FontFamily Body = new("fonts:Inter#Inter, $Default");
    public static readonly FontFamily Display = new("fonts:Geist#Geist, fonts:Inter#Inter, $Default");
    public static readonly FontFamily Mono = new("fonts:GeistMono#Geist Mono, SF Mono, Menlo, Cascadia Mono, Consolas, DejaVu Sans Mono, Courier New, $Default");
}
