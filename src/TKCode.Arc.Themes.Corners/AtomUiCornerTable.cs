namespace TKCode.Arc.Themes.Corners;

/// <summary>
/// Radius tokens of AtomUI's own controls that appear inside Arc controls: the scroll bar thumb of every
/// <c>ScrollViewer</c> in a popup or a scrolling surface. Written by hand (the generated <see cref="CornerTable"/> only covers
/// Arc's catalog). The defaults are those AtomUI ships (read from its resources under the default theme).
/// </summary>
internal static class AtomUiCornerTable
{
    internal const string Catalog = "AtomUI";

    internal static readonly CornerEntry[] ScrollViewer =
    [
        new("ScrollViewer", "ThumbCornerRadius", "4", false),
        new("ScrollViewer", "IndicatorThumbCornerRadius", "1", false),
    ];
}
