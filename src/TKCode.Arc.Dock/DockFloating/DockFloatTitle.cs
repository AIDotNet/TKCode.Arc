using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Follows the title of a floating window or card: the title of the active pane of the float's group (the first group
/// when the float is split), updated when another pane becomes active or the pane's title changes.
/// </summary>
internal sealed class DockFloatTitle : IDisposable
{
    private readonly Action<string> _apply;
    private DockGroup? _group;
    private DockPane? _pane;

    public DockFloatTitle(Action<string> apply) => _apply = apply;

    /// <summary>Follows the float's tree as it is now.</summary>
    public void Follow(DockFloat dock)
    {
        var group = dock.Root?.SelfAndDescendants().OfType<DockGroup>().FirstOrDefault();
        if (!ReferenceEquals(group, _group))
        {
            if (_group is not null)
            {
                _group.PropertyChanged -= OnGroupPropertyChanged;
            }
            _group = group;
            if (group is not null)
            {
                group.PropertyChanged += OnGroupPropertyChanged;
            }
        }
        FollowPane(group?.ActivePane);
    }

    private void OnGroupPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == DockGroup.ActivePaneProperty)
        {
            FollowPane(_group?.ActivePane);
        }
    }

    private void FollowPane(DockPane? pane)
    {
        if (!ReferenceEquals(pane, _pane))
        {
            if (_pane is not null)
            {
                _pane.PropertyChanged -= OnPanePropertyChanged;
            }
            _pane = pane;
            if (pane is not null)
            {
                pane.PropertyChanged += OnPanePropertyChanged;
            }
        }
        _apply(pane?.ToString() ?? string.Empty);
    }

    private void OnPanePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == DockPane.TitleProperty)
        {
            _apply(_pane?.ToString() ?? string.Empty);
        }
    }

    public void Dispose()
    {
        if (_group is not null)
        {
            _group.PropertyChanged -= OnGroupPropertyChanged;
        }
        if (_pane is not null)
        {
            _pane.PropertyChanged -= OnPanePropertyChanged;
        }
        _group = null;
        _pane = null;
    }
}
