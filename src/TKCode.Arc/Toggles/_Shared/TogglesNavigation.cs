using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// Roving focus shared by the toggle groups: one tab stop (the selected usable item, else the first usable one),
/// arrows select and focus the neighbor and wrap, Home / End jump to the ends. Disabled items are skipped.
/// </summary>
internal static class TogglesNavigation
{
    /// <summary>The index <paramref name="key"/> moves to from <paramref name="current"/>, or null when the key is not a move.</summary>
    public static int? Move(int current, int count, Key key, Func<int, bool> usable, bool flipHorizontal)
    {
        if (count <= 0)
        {
            return null;
        }
        var step = key switch
        {
            Key.Right => flipHorizontal ? -1 : 1,
            Key.Down => 1,
            Key.Left => flipHorizontal ? 1 : -1,
            Key.Up => -1,
            _ => 0
        };
        int at;
        int direction;
        if (key == Key.Home)
        {
            at = -1;
            direction = 1;
        }
        else if (key == Key.End)
        {
            at = count;
            direction = -1;
        }
        else if (step != 0)
        {
            at = current < 0 ? (step > 0 ? -1 : count) : current;
            direction = step;
        }
        else
        {
            return null;
        }
        for (var tries = 0; tries < count; tries++)
        {
            at = ((at + direction) % count + count) % count;
            if (usable(at))
            {
                return at;
            }
        }
        return null;
    }

    /// <summary>The index that owns the tab stop: the selected usable item, else the first usable one (-1 when none).</summary>
    public static int TabStopIndex(SelectingItemsControl owner, Func<int, bool> usable)
    {
        var selected = owner.SelectedIndex;
        if (selected >= 0 && selected < owner.ItemCount && usable(selected))
        {
            return selected;
        }
        for (var index = 0; index < owner.ItemCount; index++)
        {
            if (usable(index))
            {
                return index;
            }
        }
        return -1;
    }

    /// <summary>Points the group's single tab stop at <see cref="TabStopIndex"/>.</summary>
    public static void UpdateTabStop(SelectingItemsControl owner, Func<int, bool> usable)
    {
        var index = TabStopIndex(owner, usable);
        var container = index >= 0 ? owner.ContainerFromIndex(index) : null;
        KeyboardNavigation.SetTabOnceActiveElement(owner, container);
        if (owner.ItemsPanelRoot is { } panel)
        {
            KeyboardNavigation.SetTabOnceActiveElement(panel, container);
        }
    }

    /// <summary>Whether the container at <paramref name="index"/> can take focus and selection.</summary>
    public static bool IsUsable(SelectingItemsControl owner, int index)
        => owner.IsEffectivelyEnabled && owner.ContainerFromIndex(index) is not { IsEnabled: false };

    /// <summary>
    /// Handles an arrow / Home / End key that reached the group from one of its items: selects the target, focuses it
    /// (keyboard focus, so the ring shows) and marks the event handled. Returns the new index or -1.
    /// </summary>
    public static int HandleKey(SelectingItemsControl owner, KeyEventArgs e, Func<int, bool> usable, bool flipHorizontal)
    {
        if (e.Handled || e.KeyModifiers != KeyModifiers.None)
        {
            return -1;
        }
        var source = e.Source as Visual;
        var current = -1;
        while (source is not null && !ReferenceEquals(source, owner))
        {
            if (source is TogglesItem item)
            {
                current = owner.IndexFromContainer(item);
                break;
            }
            source = source.GetVisualParent();
        }
        if (current < 0)
        {
            current = owner.SelectedIndex;
        }
        var target = Move(current, owner.ItemCount, e.Key, usable, flipHorizontal);
        if (target is not { } index)
        {
            return -1;
        }
        e.Handled = true;
        owner.SelectedIndex = index;
        owner.ContainerFromIndex(index)?.Focus(NavigationMethod.Directional);
        return index;
    }

}

/// <summary>The selection indicator a <see cref="TogglesIndicatorTracker"/> drives (normally a Core GlidingIndicator).</summary>
internal interface ITogglesIndicator
{
    /// <summary>The coordinate space of <see cref="MoveTo"/>.</summary>
    Visual Visual { get; }

    void MoveTo(Rect target, bool animate);

    void Hide(bool animate);
}

/// <summary>Adapts the Core <see cref="GlidingIndicator"/> (P7), optionally on a component's own spring.</summary>
internal sealed class TogglesGlidingIndicator : ITogglesIndicator
{
    private readonly SpringSpec? _spring;

    public TogglesGlidingIndicator(GlidingIndicator indicator, SpringSpec? spring = null)
    {
        Indicator = indicator;
        _spring = spring;
    }

    public GlidingIndicator Indicator { get; }

    public Visual Visual => Indicator;

    public void MoveTo(Rect target, bool animate)
    {
        if (_spring is { } spring)
        {
            Indicator.MoveTo(target, spring, animate);
        }
        else
        {
            Indicator.MoveTo(target, animate);
        }
    }

    public void Hide(bool animate) => Indicator.Hide(animate);
}

/// <summary>
/// Keeps an indicator on the selected container of a toggle group. A selection change glides (when <see cref="Glide"/>
/// is called), a layout change only snaps, and the first placement lands in place. The rectangle can be deflated by
/// half a stroke so a stroked indicator lines up with a CSS border.
/// </summary>
internal sealed class TogglesIndicatorTracker
{
    private readonly SelectingItemsControl _owner;
    private readonly ITogglesIndicator _indicator;
    private readonly double _inset;
    private readonly bool _fadeInFirst;
    private Control? _container;
    private Rect _target;
    private bool _placed;
    private bool _glide;

    public TogglesIndicatorTracker(SelectingItemsControl owner, ITogglesIndicator indicator, double inset, bool fadeInFirst)
    {
        _owner = owner;
        _indicator = indicator;
        _inset = inset;
        _fadeInFirst = fadeInFirst;
        owner.LayoutUpdated += OnLayoutUpdated;
    }

    public ITogglesIndicator Indicator => _indicator;

    public void Detach() => _owner.LayoutUpdated -= OnLayoutUpdated;

    /// <summary>The selection changed: the next placement glides.</summary>
    public void Glide()
    {
        _glide = true;
        Place();
    }

    private void OnLayoutUpdated(object? sender, EventArgs e) => Place();

    public void Place()
    {
        var index = _owner.SelectedIndex;
        var container = index >= 0 ? _owner.ContainerFromIndex(index) : null;
        if (container is null || !container.IsVisible || container.Bounds.Width <= 0 || container.Bounds.Height <= 0)
        {
            if (_placed && (container is null || index < 0))
            {
                _indicator.Hide(animate: false);
                _placed = false;
                _container = null;
            }
            return;
        }
        if (container.TranslatePoint(default, _indicator.Visual) is not { } origin)
        {
            return;
        }
        var rect = new Rect(origin, container.Bounds.Size).Deflate(_inset);
        if (_placed && ReferenceEquals(container, _container) && rect == _target)
        {
            _glide = false;
            return;
        }
        var first = !_placed;
        var animate = first ? _fadeInFirst : _glide && !ReferenceEquals(container, _container);
        _target = rect;
        _container = container;
        _placed = true;
        _glide = false;
        _indicator.MoveTo(rect, animate);
    }
}
