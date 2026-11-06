namespace TKCode.Arc;

/// <summary>How a <see cref="DockLayout"/> shows floating panes (<see cref="DockLayout.FloatingMode"/>).</summary>
public enum DockFloatingMode
{
    /// <summary>
    /// Real windows when the layout is in a desktop window of a classic desktop application; floating cards inside the
    /// layout anywhere else (browser, mobile, a layout not in a window).
    /// </summary>
    Auto,

    /// <summary>Always real windows, owned by the window that shows the layout (falls back to cards when there is none).</summary>
    Window,

    /// <summary>Always floating cards inside the layout.</summary>
    Overlay,

    /// <summary>
    /// Panes do not float: dragging a pane out of the layout puts it back, and <see cref="DockPane.CanFloat"/> counts as
    /// false. Floats that are already in the model (a restored layout) show as cards.
    /// </summary>
    Disabled
}
