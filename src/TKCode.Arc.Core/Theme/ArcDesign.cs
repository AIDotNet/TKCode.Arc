using System.ComponentModel;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// Identity carrier for the Arc global design tokens. It is never placed in a visual tree.
/// It must stay public: the AtomUI registration pipeline only registers public control types.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ArcDesign : Control
{
}
