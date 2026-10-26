namespace TKCode.Arc.Themes.Corners;

/// <summary>
/// Which Arc control packages the app registers; the corner style overrides the tokens of exactly these. Naming a
/// package the app has not registered fails the whole theme change.
/// </summary>
[Flags]
public enum ArcCornerScope
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
