namespace TKCode.Arc.Themes.Pixel;

/// <summary>Which Arc control packages the app registers; the pixel theme overrides the tokens of exactly these.</summary>
[Flags]
public enum ArcPixelScope
{
    /// <summary>Only the ArcDesign tokens.</summary>
    None = 0,

    /// <summary>The <c>TKCode.Arc</c> package (<c>UseArc()</c>).</summary>
    Controls = 1,

    /// <summary>The <c>TKCode.Arc.Blocks</c> package.</summary>
    Blocks = 2,

    /// <summary>The <c>TKCode.Arc.Special</c> package.</summary>
    Special = 4,

    /// <summary>The <c>TKCode.Arc.AdvancedCharts</c> package.</summary>
    AdvancedCharts = 8,

    /// <summary>The <c>TKCode.Arc.Workflow</c> package.</summary>
    Workflow = 16,

    /// <summary>Every Arc package.</summary>
    All = Controls | Blocks | Special | AdvancedCharts | Workflow
}
