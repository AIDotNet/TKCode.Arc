using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// Minimize, maximize / restore or close in a <see cref="TitleBar"/>: a square, unfocusable ghost button with a glyph. It is
/// its own type so the title bar's theme owns its look (close turns danger under the pointer) without restyling Arc's
/// <c>Button</c>.
/// </summary>
internal sealed class TitleBarCaptionButton : AvaloniaButton
{
}
