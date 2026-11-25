namespace TKCode.Arc.Editor;

/// <summary>The direction of a <see cref="TextSelection"/>.</summary>
public enum SelectionDirection
{
    /// <summary>The selection starts above (or to the left of) where it ends.</summary>
    LeftToRight,

    /// <summary>The selection starts below (or to the right of) where it ends.</summary>
    RightToLeft
}
