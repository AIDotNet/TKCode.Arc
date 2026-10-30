using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A branch's path tabs: small tabs on a SurfaceMuted track whose Surface highlight slides to the selected path on the
/// critically damped 0.22s spring. A radio group with roving focus: only the selected tab is a tab stop; the arrow keys
/// move the selection (and focus) between paths, Home and End jump to the ends.
/// </summary>
internal sealed class AiWorkflowPathTabs : Decorator
{
    private readonly StackPanel _strip = new() { Orientation = Orientation.Horizontal, Spacing = 0 };
    private readonly GlidingIndicator _highlight = new();
    private readonly Panel _track = new();
    private readonly List<AiWorkflowPathTab> _tabs = [];
    private string? _selected;

    public AiWorkflowPathTabs()
    {
        HorizontalAlignment = HorizontalAlignment.Left;
        Margin = new Thickness(0, 2, 0, 8);
        var frame = new Border { Padding = new Thickness(2), Child = _track }.BindCornerRadius(ArcDesignTokenKind.RadiusStep8);
        ArcTokens.Bind(frame, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
        _highlight.BindCornerRadius(ArcDesignTokenKind.RadiusStep6);
        ArcTokens.Bind(_highlight, GlidingIndicator.FillProperty, ArcDesignTokenKind.Surface);
        ArcTokens.Bind(_highlight, GlidingIndicator.BoxShadowProperty, ArcDesignTokenKind.ShadowResting);
        _track.Children.Add(_highlight);
        _track.Children.Add(_strip);
        Child = frame;
        _strip.SizeChanged += (_, _) => PlaceHighlight(animate: false, onlyIfMissing: true);
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Control);
    }

    /// <summary>Raised when the person picks a path.</summary>
    public event EventHandler<string>? PathPicked;

    internal IReadOnlyList<AiWorkflowPathTab> Tabs => _tabs;

    internal GlidingIndicator Highlight => _highlight;

    public string? SelectedPathId => _selected;

    /// <summary>Shows the branch's paths.</summary>
    public void SetPaths(IReadOnlyList<AiWorkflowBranchPath> paths, string name)
    {
        AutomationProperties.SetName(this, name);
        var same = paths.Count == _tabs.Count;
        for (var i = 0; same && i < paths.Count; i++)
        {
            same = _tabs[i].PathId == paths[i].Id;
        }
        if (!same)
        {
            foreach (var tab in _tabs)
            {
                tab.Click -= OnTabClick;
                tab.KeyDown -= OnTabKeyDown;
            }
            _tabs.Clear();
            _strip.Children.Clear();
            foreach (var path in paths)
            {
                var tab = new AiWorkflowPathTab(path.Id);
                tab.Click += OnTabClick;
                tab.KeyDown += OnTabKeyDown;
                _tabs.Add(tab);
                _strip.Children.Add(tab);
            }
        }
        for (var i = 0; i < paths.Count; i++)
        {
            _tabs[i].Content = paths[i].Label;
            AutomationProperties.SetName(_tabs[i], paths[i].Label);
        }
        Select(_selected ?? paths.FirstOrDefault()?.Id, animate: false);
    }

    /// <summary>Selects a path's tab; the highlight slides there when <paramref name="animate"/>.</summary>
    public void Select(string? pathId, bool animate)
    {
        _selected = pathId;
        foreach (var tab in _tabs)
        {
            tab.IsSelected = tab.PathId == pathId;
        }
        PlaceHighlight(animate, onlyIfMissing: false);
    }

    private void PlaceHighlight(bool animate, bool onlyIfMissing)
    {
        var tab = _tabs.FirstOrDefault(t => t.IsSelected);
        if (tab is null || tab.Bounds.Width <= 0)
        {
            return;
        }
        var rect = new Rect(tab.Bounds.X, tab.Bounds.Y, tab.Bounds.Width, tab.Bounds.Height);
        if (onlyIfMissing && _highlight.HasTarget && _highlight.CurrentRect == rect)
        {
            return;
        }
        if (onlyIfMissing && _highlight.HasTarget)
        {
            // A resize or a label change: follow at once.
            _highlight.MoveTo(rect, AiWorkflowMetrics.Spring, animate: false);
            return;
        }
        _highlight.MoveTo(rect, AiWorkflowMetrics.Spring, animate);
    }

    private void OnTabClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is AiWorkflowPathTab tab)
        {
            Pick(tab);
        }
    }

    private void Pick(AiWorkflowPathTab tab)
    {
        if (tab.PathId != _selected)
        {
            Select(tab.PathId, animate: true);
            PathPicked?.Invoke(this, tab.PathId);
        }
    }

    private void OnTabKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not AiWorkflowPathTab tab || _tabs.Count == 0)
        {
            return;
        }
        var index = _tabs.IndexOf(tab);
        var next = e.Key switch
        {
            Key.Right or Key.Down => (index + 1) % _tabs.Count,
            Key.Left or Key.Up => (index - 1 + _tabs.Count) % _tabs.Count,
            Key.Home => 0,
            Key.End => _tabs.Count - 1,
            _ => -1
        };
        if (next < 0)
        {
            return;
        }
        e.Handled = true;
        var target = _tabs[next];
        Pick(target);
        target.Focus(NavigationMethod.Directional);
    }
}
