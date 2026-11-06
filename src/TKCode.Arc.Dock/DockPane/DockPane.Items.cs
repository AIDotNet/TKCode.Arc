namespace TKCode.Arc;

public partial class DockPane
{
    /// <summary>
    /// The id a pane made from a data item gets when its template does not set one: <see cref="IDockItem.DockId"/> when the
    /// item implements <see cref="IDockItem"/>, otherwise the item's <c>ToString()</c>.
    /// </summary>
    public static string IdOf(object item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item is IDockItem dockItem)
        {
            return dockItem.DockId ?? string.Empty;
        }
        return item.ToString() ?? string.Empty;
    }

    /// <summary>The title a pane made from a data item gets when its template does not set one.</summary>
    internal static string TitleOf(object item)
        => item is IDockItem dockItem ? dockItem.DockTitle ?? string.Empty : item.ToString() ?? string.Empty;

    /// <summary>Where the pane was docked before it was auto-hidden (cleared when it is pinned).</summary>
    internal DockAutoHidePlacement? AutoHidePlacement { get; set; }

    /// <summary>The auto-hide flyout's width or height in px: the pane's docked size when it was unpinned, then what the user drags it to.</summary>
    internal double AutoHideExtent { get; set; }

    /// <summary>True while XAML sets the pane up (between <c>BeginInit</c> and <c>EndInit</c>): its id may not be set yet.</summary>
    internal bool IsInitializing => _initDepth > 0;

    private int _initDepth;

    public override void BeginInit()
    {
        base.BeginInit();
        _initDepth++;
    }

    public override void EndInit()
    {
        base.EndInit();
        if (--_initDepth == 0 && Container is DockWorkspace workspace)
        {
            // Added to an auto-hide list before XAML set its id: check it now, and let the workspace learn the id.
            workspace.ValidateIncoming(this);
            workspace.NotifyStructure(removal: false);
        }
    }

    /// <summary>The source item this pane was made for (<see cref="DockLayout.DocumentsSource"/>, <see cref="DockLayout.ToolsSource"/>), or null.</summary>
    internal object? SourceItem { get; set; }
}

/// <summary>
/// Optional interface for the items of <see cref="DockLayout.DocumentsSource"/> and <see cref="DockLayout.ToolsSource"/>: the
/// pane made for the item takes its id and title from it. Raise <c>PropertyChanged</c> for <see cref="DockTitle"/> to rename
/// the tab.
/// </summary>
public interface IDockItem
{
    /// <summary>The stable id of the item's pane, unique in the layout; saved layouts refer to the pane by it.</summary>
    string DockId { get; }

    /// <summary>The title of the item's tab.</summary>
    string DockTitle { get; }
}
