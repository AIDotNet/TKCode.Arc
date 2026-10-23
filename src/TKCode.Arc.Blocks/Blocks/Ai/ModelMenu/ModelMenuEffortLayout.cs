namespace TKCode.Arc;

/// <summary>Where a <see cref="ModelMenu"/> offers the effort levels of the selected model.</summary>
public enum ModelMenuEffortLayout
{
    /// <summary>
    /// The model picker's menu: an Effort row (showing the current level) that opens the levels in a submenu beside the
    /// menu, or a page inside it in a narrow window; choosing a level closes the menu.
    /// </summary>
    Submenu,

    /// <summary>
    /// The AI composer's menu: 296px wide, one-line model rows, then an "Effort" header with the current level's
    /// description and a segmented row of the levels in the same panel; choosing a level keeps the menu open.
    /// </summary>
    Inline
}
