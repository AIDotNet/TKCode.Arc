using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Geometry and the authored mesh palettes of the <see cref="HeroSection"/> block (hero-*.module.css).</summary>
[ControlDesignToken]
internal sealed class HeroSectionToken : AbstractControlDesignToken
{
    /// <summary>A workflow node's corner radius (14).</summary>
    public CornerRadius NodeRadius { get; set; }

    /// <summary>A workflow node's padding at the sides (14).</summary>
    public Thickness NodePadding { get; set; }

    /// <summary>The dots of the workflow canvas: the foreground at 13% (17% in dark).</summary>
    public Color CanvasDot { get; set; }

    /// <summary>The workflow's resting wires: the foreground at 17% (20% in dark).</summary>
    public Color CanvasWire { get; set; }

    /// <summary>The dashboard's sidebar fill: 40% of the way from Surface to SurfaceMuted.</summary>
    public Color SidebarFill { get; set; }

    /// <summary>The dashboard's title bar fill: 55% of the way from Surface to SurfaceMuted.</summary>
    public Color ChromeFill { get; set; }

    /// <summary>The window's traffic lights: the foreground at 14%.</summary>
    public Color WindowLight { get; set; }

    public Color DashboardMesh1 { get; set; }

    public Color DashboardMesh2 { get; set; }

    public Color DashboardMesh3 { get; set; }

    public Color DashboardMesh4 { get; set; }

    public Color DashboardMesh5 { get; set; }

    public Color EditorialMeshBase { get; set; }

    public Color EditorialMesh1 { get; set; }

    public Color EditorialMesh2 { get; set; }

    public Color EditorialMesh3 { get; set; }

    public Color EditorialMesh4 { get; set; }

    public Color EditorialMesh5 { get; set; }

    public Color EditorialMesh6 { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        NodeRadius = new CornerRadius(14);
        NodePadding = new Thickness(14, 0);
        CanvasDot = Oklch.WithAlpha(n.Foreground, isDarkMode ? 0.17 : 0.13);
        CanvasWire = Oklch.WithAlpha(n.Foreground, isDarkMode ? 0.20 : 0.17);
        SidebarFill = Oklch.MixOklab(n.Surface, n.SurfaceMuted, 0.40);
        ChromeFill = Oklch.MixOklab(n.Surface, n.SurfaceMuted, 0.55);
        WindowLight = Oklch.WithAlpha(n.Foreground, 0.14);
        DashboardMesh1 = Color.Parse(isDarkMode ? "#2a1822" : "#ffe6d8");
        DashboardMesh2 = Color.Parse(isDarkMode ? "#1c1a44" : "#e3dcff");
        DashboardMesh3 = Color.Parse(isDarkMode ? "#141c40" : "#dbe4ff");
        DashboardMesh4 = Color.Parse(isDarkMode ? "#231842" : "#f0e0ff");
        DashboardMesh5 = Color.Parse(isDarkMode ? "#0e2133" : "#e0eefc");
        EditorialMeshBase = Color.Parse(isDarkMode ? "#09090f" : "#f2ede7");
        EditorialMesh1 = Color.Parse(isDarkMode ? "#0d3a47" : "#bdd3ec");
        EditorialMesh2 = Color.Parse(isDarkMode ? "#2a2f86" : "#c3c6ff");
        EditorialMesh3 = Color.Parse(isDarkMode ? "#5a2a3c" : "#ffd0b0");
        EditorialMesh4 = Color.Parse(isDarkMode ? "#3a1f5a" : "#f6bfd6");
        EditorialMesh5 = Color.Parse(isDarkMode ? "#7a3a22" : "#cfe8dc");
        EditorialMesh6 = Color.Parse(isDarkMode ? "#12142c" : "#fff3dc");
    }
}
