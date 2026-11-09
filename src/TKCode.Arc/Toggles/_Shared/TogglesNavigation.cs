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
/// <remarks>
/// The tracker used to subscribe to the owner's <c>LayoutUpdated</c>, which every layout pass of the window raises for
/// every subscriber (a settings page with a dozen groups paid an ancestor walk and a TranslatePoint each pass). It now
/// follows the bounds of the selected container and of the indicator's visual (the two ends of the TranslatePoint), plus
/// the owner's <c>EffectiveViewportChanged</c> as the fallback that catches a group shown again or moved by an ancestor;
/// that event is raised only when the owner's own viewport changes.
/// </remarks>
internal sealed class TogglesIndicatorTracker
{
    private readonly SelectingItemsControl _owner;
    private readonly ITogglesIndicator _indicator;
    private readonly double _inset;
    private readonly bool _fadeInFirst;
    private readonly EventHandler<AvaloniaPropertyChangedEventArgs> _onBoundsChanged;
    private Control? _container;
    private Control? _watched;
    private Rect _target;
    private bool _placed;
    private bool _glide;
    private bool _indicatorWatched;
    private bool _settling;

    public TogglesIndicatorTracker(SelectingItemsControl owner, ITogglesIndicator indicator, double inset, bool fadeInFirst)
    {
        _owner = owner;
        _indicator = indicator;
        _inset = inset;
        _fadeInFirst = fadeInFirst;
        _onBoundsChanged = OnBoundsChanged;
        owner.EffectiveViewportChanged += OnEffectiveViewportChanged;
        owner.PropertyChanged += OnOwnerPropertyChanged;
        if (indicator.Visual is Control visual)
        {
            visual.PropertyChanged += _onBoundsChanged;
            _indicatorWatched = true;
        }
    }

    public ITogglesIndicator Indicator => _indicator;

    public void Detach()
    {
        _owner.EffectiveViewportChanged -= OnEffectiveViewportChanged;
        _owner.PropertyChanged -= OnOwnerPropertyChanged;
        if (_settling)
        {
            _settling = false;
            _owner.LayoutUpdated -= OnLayoutSettled;
        }
        if (_indicatorWatched && _indicator.Visual is Control visual)
        {
            visual.PropertyChanged -= _onBoundsChanged;
            _indicatorWatched = false;
        }
        Watch(null);
    }

    /// <summary>The selection changed: the next placement glides.</summary>
    public void Glide()
    {
        _glide = true;
        Place();
    }

    /// <summary>
    /// A hidden control (a settings page kept in the tree) skips placement; showing it again changes its effective
    /// viewport, which places the indicator. A container's bounds change the moment the layout pass arranges it.
    /// </summary>
    private void OnEffectiveViewportChanged(object? sender, Avalonia.Layout.EffectiveViewportChangedEventArgs e)
    {
        if (_owner.IsEffectivelyVisible)
        {
            Place();
        }
    }

    private void OnOwnerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        // A selection change is reported by the owner through Glide (placing on SelectedIndex here would snap first and
        // swallow the glide); only a changed owner box is caught.
        if (e.Property == Visual.BoundsProperty)
        {
            PlaceAfterLayout();
        }
    }

    private void OnBoundsChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Visual.BoundsProperty)
        {
            PlaceAfterLayout();
        }
    }

    /// <summary>
    /// A Bounds change arrives mid-arrange, while the ancestors between the container and the indicator still have their
    /// old bounds, so TranslatePoint would read a stale origin. One LayoutUpdated, subscribed only when something moved,
    /// places the indicator at the end of the same pass, when the permanent subscription used to.
    /// </summary>
    private void PlaceAfterLayout()
    {
        if (!_settling)
        {
            _settling = true;
            _owner.LayoutUpdated += OnLayoutSettled;
        }
    }

    private void OnLayoutSettled(object? sender, EventArgs e)
    {
        _settling = false;
        _owner.LayoutUpdated -= OnLayoutSettled;
        if (_owner.IsEffectivelyVisible)
        {
            Place();
        }
    }

    /// <summary>Follows the bounds of the selected container only; the previous one is released.</summary>
    private void Watch(Control? container)
    {
        if (ReferenceEquals(_watched, container))
        {
            return;
        }
        if (_watched is not null)
        {
            _watched.PropertyChanged -= _onBoundsChanged;
        }
        _watched = container;
        if (_watched is not null)
        {
            _watched.PropertyChanged += _onBoundsChanged;
        }
    }

    public void Place()
    {
        var index = _owner.SelectedIndex;
        var container = index >= 0 ? _owner.ContainerFromIndex(index) : null;
        Watch(container);
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
