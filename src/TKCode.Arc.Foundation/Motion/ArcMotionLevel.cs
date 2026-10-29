namespace TKCode.Arc.Motion;

// Same name, namespace and values as TKCode.Arc.Core's enum: at the F3 switch-over the Core copy is deleted and Core uses
// this one, so no consumer changes.

/// <summary>How much motion the theme allows.</summary>
public enum ArcMotionLevel
{
    /// <summary>No transitions or animations.</summary>
    Off = 0,

    /// <summary>Opacity and color changes only; springs jump to their end values.</summary>
    Reduced = 1,

    /// <summary>All motion.</summary>
    Full = 2
}
