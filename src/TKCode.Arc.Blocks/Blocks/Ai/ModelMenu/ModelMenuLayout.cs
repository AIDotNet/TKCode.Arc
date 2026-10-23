namespace TKCode.Arc;

/// <summary>How a <see cref="ModelMenu"/> lists its models.</summary>
public enum ModelMenuLayout
{
    /// <summary>The model picker's two-line rows (name and blurb), 304 wide, in the panel radius.</summary>
    Featured,

    /// <summary>
    /// The AI chat's model menu: one 36px row per model (a 16px mark, the name, the muted <see cref="AiModel.Note"/> at the
    /// end and a check in the text color), in a panel as wide as its rows (at least 240) with the control radius (18).
    /// Applies with <see cref="ModelMenuEffortLayout.Submenu"/>; the inline effort layout keeps the composer's rows.
    /// </summary>
    Simple
}
