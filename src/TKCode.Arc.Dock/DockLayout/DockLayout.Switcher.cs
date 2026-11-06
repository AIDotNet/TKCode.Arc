using Avalonia.Input;

namespace TKCode.Arc;

public partial class DockLayout
{
    /// <summary>Panes in the order they were last active, most recent first (only panes of the current workspace).</summary>
    private readonly List<DockPane> _recent = [];
    private DockPaneSwitcher? _switcher;
    private bool _switcherHolding;

    /// <summary>The pane switcher (tests).</summary>
    internal DockPaneSwitcher? Switcher => _switcher;

    /// <summary>Every pane, most recently active first; panes never active follow in layout order.</summary>
    internal List<DockPane> RecentPanes()
    {
        var all = _workspace.AllPanes().ToList();
        var present = all.ToHashSet();
        var result = _recent.Where(present.Contains).ToList();
        var listed = result.ToHashSet();
        foreach (var pane in all)
        {
            if (listed.Add(pane))
            {
                result.Add(pane);
            }
        }
        return result;
    }

    /// <summary>
    /// Opens the pane switcher with the previous pane (<paramref name="direction"/> 1) or the least recent one (−1)
    /// highlighted. <paramref name="holding"/>: opened with Ctrl held, so releasing Ctrl activates the highlight.
    /// </summary>
    internal bool ShowPaneSwitcher(bool holding, int direction)
    {
        if (_switcher is null)
        {
            return false;
        }
        var panes = RecentPanes();
        if (panes.Count == 0)
        {
            return false;
        }
        var index = panes.Count == 1 ? 0 : direction >= 0 ? 1 : panes.Count - 1;
        _switcherHolding = holding;
        _switcher.Open(panes, index);
        return true;
    }

    /// <summary>Closes the switcher and activates its highlighted pane.</summary>
    internal void CommitPaneSwitcher()
    {
        if (_switcher is not { IsOpen: true } switcher)
        {
            return;
        }
        var pane = switcher.Selected;
        switcher.Close(restoreFocus: pane is null);
        _switcherHolding = false;
        if (pane is not null && ReferenceEquals(pane.Workspace, _workspace))
        {
            Activate(pane);
        }
    }

    /// <summary>Closes the switcher without switching.</summary>
    internal void ClosePaneSwitcher(bool restoreFocus)
    {
        _switcherHolding = false;
        _switcher?.Close(restoreFocus);
    }
}
