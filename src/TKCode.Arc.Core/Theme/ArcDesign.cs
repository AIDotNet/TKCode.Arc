using System.ComponentModel;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// Former identity carrier of the Arc global design tokens (they are now the global identity of
/// <c>ArcDesignToken</c>). Kept for source compatibility; it is never placed in a visual tree.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ArcDesign : Control
{
}
