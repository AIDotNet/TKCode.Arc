using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// A place a dock drag can drop into (spec 9.3): the layout itself, a floating window or a floating card. Hit-testing finds
/// the host under the pointer by screen position, then the group inside it; the host's indicator layer draws the compass,
/// the preview and the ghost.
/// </summary>
internal interface IDockHost
{
    /// <summary>The area that catches the pointer.</summary>
    Visual Region { get; }

    /// <summary>The layer the host's indicators draw on; hit-testing works in its coordinates.</summary>
    DockIndicatorLayer? Layer { get; }

    /// <summary>The groups shown by the host.</summary>
    IEnumerable<DockGroup> Groups { get; }

    /// <summary>The floating window or card the host shows; null for the layout itself.</summary>
    DockFloat? Float { get; }
}

/// <summary>A floating window or card: it shows one <see cref="DockFloat"/>, can be moved by a drag and brought to front.</summary>
internal interface IDockFloatHost : IDockHost
{
    /// <summary>Increases each time the host is activated; the most recent host is hit-tested first.</summary>
    long ActivationStamp { get; }

    /// <summary>True for a real window, false for a card inside the layout.</summary>
    bool IsWindow { get; }

    /// <summary>Shows the float's current tree and title.</summary>
    void SyncContent();

    /// <summary>Brings the host to the front.</summary>
    void BringToFront();

    /// <summary>Takes the host away without touching the model (the float left the workspace, or the layout left the tree).</summary>
    void Dismiss();
}

/// <summary>The layout itself as a drop host.</summary>
internal sealed class DockMainHost : IDockHost
{
    private readonly DockLayout _layout;

    public DockMainHost(DockLayout layout) => _layout = layout;

    public Visual Region => _layout;

    public DockIndicatorLayer? Layer => _layout.IndicatorLayer;

    public IEnumerable<DockGroup> Groups => _layout.Workspace.Root?.SelfAndDescendants().OfType<DockGroup>() ?? [];

    public DockFloat? Float => null;
}

/// <summary>Hands out activation stamps (most recently activated host first).</summary>
internal static class DockActivation
{
    private static long s_counter;

    public static long Next() => ++s_counter;
}
