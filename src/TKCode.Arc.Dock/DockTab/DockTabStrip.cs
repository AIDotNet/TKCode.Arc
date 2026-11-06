using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A group's row of <see cref="DockTab"/>s: lays them out left to right, scrolls them with the wheel when they do not fit
/// (the group then shows its overflow button), and glides the selected tab's background pill between tabs on the same
/// spring as <c>Tabs</c>. The pill is <c>SurfaceMuted</c>, or <c>AccentSubtle</c> while the group holds the active pane.
/// </summary>
internal sealed class DockTabStrip : Control
{
    private readonly List<DockTab> _tabs = [];
    private readonly GlidingIndicator _pill;
    private IDisposable? _pillFill;
    private DockTab? _selected;
    private bool _groupActive;
    private double _offset;
    private double _extent;
    private bool _isOverflowing;
    private bool _revealSelected;

    public DockTabStrip()
    {
        ClipToBounds = true;
        _pill = new GlidingIndicator { IsHitTestVisible = false, Spring = ArcSpring.Morph }
            .BindCornerRadius(ArcDesignTokenKind.RadiusStep8);
        VisualChildren.Add(_pill);
        LogicalChildren.Add(_pill);
        UpdatePillFill();
    }

    /// <summary>Raised when the tabs start or stop overflowing the strip.</summary>
    public event EventHandler? OverflowChanged;

    /// <summary>The tabs in order.</summary>
    public IReadOnlyList<DockTab> Tabs => _tabs;

    /// <summary>True when the tabs are wider than the strip.</summary>
    public bool IsOverflowing => _isOverflowing;

    /// <summary>The horizontal scroll position (tests).</summary>
    internal double Offset => _offset;

    internal GlidingIndicator Pill => _pill;

    /// <summary>Gap between tabs from the token, read once the strip is in a tree.</summary>
    private double TabGap => this.TryFindResource(DockTabTokens.Key(DockTabTokenKind.TabGap), out var value) && value is double gap ? gap : 2;

    /// <summary>Replaces the tabs (reused instances keep their state).</summary>
    public void SetTabs(IReadOnlyList<DockTab> tabs)
    {
        if (tabs.SequenceEqual(_tabs))
        {
            return;
        }
        foreach (var tab in _tabs)
        {
            if (!tabs.Contains(tab))
            {
                VisualChildren.Remove(tab);
                LogicalChildren.Remove(tab);
            }
        }
        foreach (var tab in tabs)
        {
            if (!_tabs.Contains(tab))
            {
                VisualChildren.Add(tab);
                LogicalChildren.Add(tab);
            }
        }
        _tabs.Clear();
        _tabs.AddRange(tabs);
        if (_selected is not null && !_tabs.Contains(_selected))
        {
            _selected = null;
        }
        InvalidateMeasure();
    }

    /// <summary>The selected tab: the pill moves to it and it scrolls into view.</summary>
    public DockTab? SelectedTab
    {
        get => _selected;
        set
        {
            if (ReferenceEquals(value, _selected))
            {
                return;
            }
            _selected = value;
            _revealSelected = true;
            InvalidateArrange();
        }
    }

    /// <summary>Whether the group holds the active pane (the pill takes the accent).</summary>
    public void SetGroupActive(bool active)
    {
        if (active == _groupActive)
        {
            return;
        }
        _groupActive = active;
        UpdatePillFill();
    }

    private void UpdatePillFill()
    {
        _pillFill?.Dispose();
        _pillFill = ArcTokens.Bind(_pill, GlidingIndicator.FillProperty,
            _groupActive ? ArcDesignTokenKind.AccentSubtle : ArcDesignTokenKind.SurfaceMuted);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var gap = TabGap;
        var width = 0.0;
        var height = 0.0;
        foreach (var tab in _tabs)
        {
            tab.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            width += tab.DesiredSize.Width;
            height = Math.Max(height, tab.DesiredSize.Height);
        }
        if (_tabs.Count > 1)
        {
            width += gap * (_tabs.Count - 1);
        }
        _extent = width;
        _pill.Measure(availableSize);
        return new Size(double.IsInfinity(availableSize.Width) ? width : Math.Min(width, availableSize.Width), height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var overflowing = _extent > finalSize.Width + 0.5;
        var maxOffset = Math.Max(0, _extent - finalSize.Width);
        if (_revealSelected && _selected is not null)
        {
            _offset = RevealOffset(_selected, finalSize.Width);
        }
        _revealSelected = false;
        _offset = Math.Clamp(_offset, 0, maxOffset);
        var gap = TabGap;
        var x = -_offset;
        Rect? selectedRect = null;
        foreach (var tab in _tabs)
        {
            var size = tab.DesiredSize;
            var rect = new Rect(x, Math.Max(0, (finalSize.Height - size.Height) / 2), size.Width, size.Height);
            tab.Arrange(rect);
            if (ReferenceEquals(tab, _selected))
            {
                selectedRect = rect;
            }
            x += size.Width + gap;
        }
        _pill.Arrange(new Rect(finalSize));
        if (selectedRect is { } target)
        {
            var animate = IsLoaded && _pill.HasTarget;
            _pill.MoveTo(target, animate);
        }
        else
        {
            _pill.Hide(animate: false);
        }
        if (overflowing != _isOverflowing)
        {
            _isOverflowing = overflowing;
            OverflowChanged?.Invoke(this, EventArgs.Empty);
        }
        return finalSize;
    }

    private double RevealOffset(DockTab tab, double width)
    {
        var gap = TabGap;
        var start = 0.0;
        foreach (var t in _tabs)
        {
            if (ReferenceEquals(t, tab))
            {
                break;
            }
            start += t.DesiredSize.Width + gap;
        }
        var end = start + tab.DesiredSize.Width;
        if (start < _offset)
        {
            return start;
        }
        if (end > _offset + width)
        {
            return end - width;
        }
        return _offset;
    }

    /// <summary>The empty part of the strip catches the pointer too: pressing it and dragging moves the whole group.</summary>
    public override void Render(Avalonia.Media.DrawingContext context)
        => context.FillRectangle(Avalonia.Media.Brushes.Transparent, new Rect(Bounds.Size));

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (!_isOverflowing)
        {
            return;
        }
        var delta = Math.Abs(e.Delta.X) > Math.Abs(e.Delta.Y) ? e.Delta.X : e.Delta.Y;
        var next = Math.Clamp(_offset - delta * 40, 0, Math.Max(0, _extent - Bounds.Width));
        if (Math.Abs(next - _offset) > 0.1)
        {
            _offset = next;
            InvalidateArrange();
        }
        e.Handled = true;
    }
}
