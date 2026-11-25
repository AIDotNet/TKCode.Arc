using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace TKCode.Arc.Primitives;

/// <summary>
/// Implemented by a window (such as <c>ArcWindow</c>) that keeps an <see cref="ArcOverlayHost"/> around its content area
/// so in-app modals do not cover the title bar.
/// </summary>
internal interface IArcContentOverlayWindow
{
    /// <summary>The content overlay host while active, or null when modals should cover the full window.</summary>
    ArcOverlayHost? ActiveContentOverlayHost { get; }
}

/// <summary>
/// A panel that gives its descendants their own overlay layer: modals opened from inside it (dialogs, drawers, bottom
/// sheets, command palettes) mount on this panel's <see cref="Layer"/> and are bounded by it instead of covering the
/// whole window. Children stack like a <see cref="Panel"/>; the layer sits above all of them. An app uses it to keep its own
/// controls above every modal: put the app's content in a host and those controls (a title bar with
/// <c>WindowCaptionButtons</c> in a window whose content reaches the top, a splash screen) beside it, outside the host, and
/// a modal opened from the content covers the host only. Previews and docs stages use it to keep a modal on the stage.
/// <code>
/// &lt;arc:ArcOverlayHost Height="480"&gt;
///     &lt;StackPanel&gt;…&lt;/StackPanel&gt;
///     &lt;arc:Dialog IsOpen="True" … /&gt;
/// &lt;/arc:ArcOverlayHost&gt;
/// </code>
/// </summary>
public class ArcOverlayHost : Panel
{
    private readonly ArcOverlayLayer _layer;

    /// <summary>Creates the host and its (initially empty) overlay layer.</summary>
    public ArcOverlayHost()
    {
        _layer = new ArcOverlayLayer(this) { ZIndex = int.MaxValue };
        // The layer is a visual child only and stays last: Panel inserts its children at their own indices, which never
        // pass the layer.
        VisualChildren.Add(_layer);
    }

    /// <summary>The overlay layer hosted modals mount on; it covers the host's bounds.</summary>
    public ArcOverlayLayer Layer => _layer;

    /// <summary>When false, <see cref="FindHost"/> skips this host so modals fall through to the outer layer.</summary>
    internal bool IsModalHostEnabled { get; set; } = true;

    /// <summary>Optional content visual blurred while a modal is open on <see cref="Layer"/> (null by default).</summary>
    internal Visual? BlurTarget { get; set; }

    /// <summary>The nearest <see cref="ArcOverlayHost"/> that contains <paramref name="visual"/>, or null.</summary>
    public static ArcOverlayHost? FindHost(Visual visual)
    {
        ArgumentNullException.ThrowIfNull(visual);
        foreach (var ancestor in visual.GetVisualAncestors())
        {
            if (ancestor is ArcOverlayHost { IsModalHostEnabled: true } host)
            {
                return host;
            }
        }
        return null;
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        _layer.Measure(availableSize);
        return size;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        _layer.Arrange(new Rect(finalSize));
        return size;
    }
}

/// <summary>
/// The overlay layer of an <see cref="ArcOverlayHost"/>: a <see cref="Canvas"/> covering the host, transparent to
/// pointer input where it has no content. <see cref="Find"/> resolves the layer a modal should mount on.
/// </summary>
public sealed class ArcOverlayLayer : Canvas
{
    internal ArcOverlayLayer(ArcOverlayHost host)
    {
        Host = host;
    }

    /// <summary>The host panel this layer belongs to (its size is the modal viewport).</summary>
    public ArcOverlayHost Host { get; }

    /// <summary>
    /// The layer a modal opened from <paramref name="anchor"/> mounts on: the layer of the nearest
    /// <see cref="ArcOverlayHost"/> ancestor, otherwise the window's <see cref="OverlayLayer"/>, which is switched on in
    /// the nearest <see cref="VisualLayerManager"/> when it is off (null when <paramref name="anchor"/> is not attached to
    /// a window).
    /// </summary>
    public static Panel? Find(Visual anchor)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        if (ArcOverlayHost.FindHost(anchor) is { } host)
        {
            return host.Layer;
        }
        if ((anchor as TopLevel ?? TopLevel.GetTopLevel(anchor)) is IArcContentOverlayWindow { ActiveContentOverlayHost: { IsModalHostEnabled: true } windowHost } &&
            !ReferenceEquals(anchor, windowHost))
        {
            return windowHost.Layer;
        }
        if (OverlayLayer.GetOverlayLayer(anchor) is { } overlay)
        {
            return overlay;
        }
        if (TopLevel.GetTopLevel(anchor) is not { } topLevel)
        {
            return null;
        }
        if (OverlayLayer.GetOverlayLayer(topLevel) is { } topOverlay)
        {
            return topOverlay;
        }
        // Layer managers create their overlay layer on demand.
        var manager = anchor.FindAncestorOfType<VisualLayerManager>(includeSelf: true)
                      ?? topLevel.GetVisualDescendants().OfType<VisualLayerManager>().FirstOrDefault();
        if (manager is null)
        {
            return null;
        }
        manager.EnableOverlayLayer = true;
        return OverlayLayer.GetOverlayLayer(anchor) ?? OverlayLayer.GetOverlayLayer(topLevel);
    }
}
