namespace TKCode.Arc;

/// <summary>How a <see cref="ModelPill"/> looks.</summary>
public enum ModelPillAppearance
{
    /// <summary>The standalone pill of the model picker: 40px, a 1px border on the surface, the model's full name.</summary>
    Default,

    /// <summary>
    /// A chip in a composer toolbar: 36px and borderless, tinted only on hover and while its menu is open, with the model's
    /// short name (<see cref="AiModel.DisplayName"/>).
    /// </summary>
    Toolbar,

    /// <summary>
    /// The model button of a chat header (Arc's AI chat): 34px and borderless like the toolbar chip, the full name in regular
    /// weight next to a 15px mark, a 1.75 chevron, and SurfaceMuted under the pointer and while its menu is open.
    /// </summary>
    Header
}
