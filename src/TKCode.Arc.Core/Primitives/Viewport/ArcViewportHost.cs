namespace TKCode.Arc.Primitives;

/// <summary>
/// A region that acts as the window for everything inside it: <see cref="ArcViewport"/> reports its size instead of the
/// window's, modals opened inside it mount on its own layer (it is an <see cref="ArcOverlayHost"/>), and popups
/// (<see cref="ArcPopup"/>) flip and slide to stay within its bounds. Use it for a narrow-viewport preview (a phone
/// outline in a docs page or a design tool); an app's own window needs no host.
/// <code>
/// &lt;arc:ArcViewportHost Width="390" Height="844"&gt;
///     &lt;local:SettingsView /&gt;
/// &lt;/arc:ArcViewportHost&gt;
/// </code>
/// </summary>
/// <remarks>
/// Popups still draw in the window's overlay layer, above the host, and are not clipped by it. When the host sits in a
/// scaled container, set <see cref="ArcZoom.ScaleProperty"/> on that container so popups lay out at the same scale.
/// </remarks>
public class ArcViewportHost : ArcOverlayHost
{
}
