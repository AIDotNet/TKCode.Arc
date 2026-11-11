using AvaloniaToggleButton = Avalonia.Controls.Primitives.ToggleButton;

namespace TKCode.Arc;

/// <summary>
/// The always-on-top toggle of a <see cref="TitleBar"/>: a <see cref="WindowCaptionButton"/>-style square that stays
/// pressed while on. Its own type so the title bar's theme owns its look.
/// </summary>
internal sealed class TitleBarCaptionToggleButton : AvaloniaToggleButton
{
}
