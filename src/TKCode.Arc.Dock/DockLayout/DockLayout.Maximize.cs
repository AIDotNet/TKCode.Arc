using Avalonia.Controls;
using TKCode.Arc.Localization;

namespace TKCode.Arc;

public partial class DockLayout
{
    private DockGroup? _maximized;

    /// <summary>The group that fills the layout for now, or null (also null once it left the main tree).</summary>
    internal DockGroup? MaximizedGroup =>
        _maximized is { } group && ReferenceEquals(group.Workspace, _workspace) && group.Float is null && group.Panes.Count > 0
            ? group
            : null;

    /// <summary>
    /// Maximizes a group of the main layout: it fills the whole layout until it is restored (the same call, a double click on
    /// its tab strip, the menu's Restore, or activating a pane of another group). The layout itself does not change, so
    /// restoring shows it exactly as it was. Calling it for the maximized group restores.
    /// </summary>
    public void ToggleMaximize(DockGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        if (ReferenceEquals(_maximized, group))
        {
            RestoreMaximized();
            return;
        }
        if (!CanMaximize(group))
        {
            return;
        }
        if (_maximized is not null)
        {
            RestoreMaximized();
        }
        _maximized = group;
        PseudoClasses.Set(MaximizedPseudoClass, true);
        UpdateRootView();
        if (ActivePane is not { } active || !ReferenceEquals(active.Group, group))
        {
            if (group.ActivePane is { } pane)
            {
                Select(pane);
            }
        }
        RaiseExtraCommandsChanged();
    }

    /// <summary>Whether a group can be maximized: it is in the main tree and does not fill it already.</summary>
    private bool CanMaximize(DockGroup group)
        => ReferenceEquals(group.Workspace, _workspace) && group.Float is null && group.Panes.Count > 0
           && !ReferenceEquals(_workspace.Root, group)
           && !(_workspace.Root is DockDocumentArea area && ReferenceEquals(area.Root, group));

    /// <summary>Puts the maximized group's view back in its split and shows the whole layout again.</summary>
    private void RestoreMaximized()
    {
        if (_maximized is not { } group)
        {
            return;
        }
        _maximized = null;
        PseudoClasses.Set(MaximizedPseudoClass, false);
        UpdateRootView();
        switch (group.Parent?.View)
        {
            case DockSplitView split when ReferenceEquals(split.Layout, this):
                split.Sync();
                break;
            case DockDocumentAreaView area when ReferenceEquals(area.Layout, this):
                area.Sync();
                break;
        }
        RaiseExtraCommandsChanged();
    }

    /// <summary>Maximize or Restore, last in a pane's menu.</summary>
    private void AddMaximizeRow(List<MenuEntry> rows, DockPane pane)
    {
        if (pane.Group is not { Float: null } group)
        {
            return;
        }
        var maximized = ReferenceEquals(MaximizedGroup, group);
        var row = Row(DockText.Get(maximized ? DockLangResourceKind.Restore : DockLangResourceKind.Maximize),
            maximized || CanMaximize(group), () => ToggleMaximize(group), maximized ? ArcIcons.Minimize2 : ArcIcons.Maximize2);
        row.HasSeparatorBefore = true;
        rows.Add(row);
    }
}
