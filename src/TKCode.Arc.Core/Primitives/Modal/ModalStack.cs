using System.Runtime.CompilerServices;
using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Primitives;

/// <summary>
/// Per-TopLevel stack of mounted modals: routes Escape to the topmost one, keeps focus inside it, and owns the
/// blurred backdrop over the window content (one <see cref="ModalBackdrop"/> for the whole stack, the strongest modal
/// wins).
/// </summary>
internal sealed class ModalStack
{
    private static readonly ConditionalWeakTable<TopLevel, ModalStack> s_stacks = new();

    private readonly TopLevel _topLevel;
    private readonly List<ModalPresence> _items = new();
    private ModalBackdrop? _backdrop;
    private Visual? _blurTarget;
    private Panel? _backdropLayer;
    private bool _recapturePending;
    private bool _hooked;

    private ModalStack(TopLevel topLevel) => _topLevel = topLevel;

    public static ModalStack For(TopLevel topLevel) => s_stacks.GetValue(topLevel, static t => new ModalStack(t));

    public static ModalStack? Find(TopLevel topLevel) => s_stacks.TryGetValue(topLevel, out var stack) ? stack : null;

    public IReadOnlyList<ModalPresence> Items => _items;

    /// <summary>The topmost modal that is open or opening (a leaving modal no longer owns input).</summary>
    public ModalPresence? Topmost
    {
        get
        {
            for (var i = _items.Count - 1; i >= 0; i--)
            {
                if (_items[i].IsOpen)
                {
                    return _items[i];
                }
            }
            return null;
        }
    }

    public void Add(ModalPresence presence)
    {
        _items.Remove(presence);
        _items.Add(presence);
        if (!_hooked)
        {
            _hooked = true;
            _topLevel.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Bubble);
            _topLevel.AddHandler(InputElement.GettingFocusEvent, OnGettingFocus, RoutingStrategies.Bubble, true);
        }
    }

    public void Remove(ModalPresence presence)
    {
        _items.Remove(presence);
        if (_items.Count == 0 && _hooked)
        {
            _hooked = false;
            _topLevel.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
            _topLevel.RemoveHandler(InputElement.GettingFocusEvent, OnGettingFocus);
        }
        UpdateBlur();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.Key != Key.Escape || Topmost is not { } top || !top.DismissOnEscape)
        {
            return;
        }
        e.Handled = true;
        top.Dismiss(DismissReason.Escape);
    }

    private void OnGettingFocus(object? sender, FocusChangingEventArgs e)
    {
        if (e.Canceled || Topmost is not { TrapFocus: true } top || e.NewFocusedElement is not { } next || top.Contains(next))
        {
            return;
        }
        // Focus trap: anything outside the topmost modal sends focus back into it.
        var inside = top.FindFocusTarget();
        if (inside is null || !e.TrySetNewFocusedElement(inside))
        {
            e.TryCancel();
        }
    }

    /// <summary>
    /// Shows the strongest backdrop blur of the stack over the window content under the overlay layer: a
    /// <see cref="ModalBackdrop"/> taken once (again when the strongest blur grows, the content is resized or the blur
    /// moves to another layer) and faded with the scrim; removed and released when no modal blurs any more.
    /// </summary>
    /// <remarks>Runs on every frame of the scrim tween: past the first frame it only sets the backdrop's opacity.</remarks>
    public void UpdateBlur()
    {
        double radius = 0;
        double strength = 0;
        Visual? target = null;
        Panel? layer = null;
        foreach (var item in _items)
        {
            var r = item.CurrentBackdropBlur;
            if (r <= 0)
            {
                continue;
            }
            // The snapshot is blurred by the strongest full radius in the stack, so a second modal does not take another.
            strength = Math.Max(strength, item.BackdropBlur);
            if (r > radius)
            {
                radius = r;
                target = item.BlurTarget;
                layer = item.MountLayer;
            }
        }
        if (radius < 0.05 || target is null || layer is null)
        {
            ClearBlur();
            return;
        }
        var stale = !ReferenceEquals(target, _blurTarget) || !ReferenceEquals(layer, _backdropLayer) ||
                    (_backdrop is { } current && (strength > current.Radius + 0.01 || target.Bounds.Size != current.ContentSize));
        if (stale || (_backdrop is null && !_captureFailed))
        {
            Capture(target, layer, strength);
        }
        if (_backdrop is { } backdrop)
        {
            backdrop.Opacity = Math.Clamp(radius / backdrop.Radius, 0, 1);
        }
    }

    private bool _captureFailed;

    private void Capture(Visual target, Panel layer, double radius)
    {
        ClearBlur();
        _blurTarget = target;
        _backdropLayer = layer;
        target.PropertyChanged += OnBlurTargetPropertyChanged;
        var backdrop = ModalBackdrop.Capture(target, radius);
        if (backdrop is null)
        {
            // Not retried every frame: only when the content is resized.
            _captureFailed = true;
            return;
        }
        backdrop.Width = target.Bounds.Width;
        backdrop.Height = target.Bounds.Height;
        PlaceBackdrop(backdrop, target, layer);
        // Under every modal (and anything else) on the layer: before, those were never blurred either.
        layer.Children.Insert(0, backdrop);
        _backdrop = backdrop;
    }

    private static void PlaceBackdrop(ModalBackdrop backdrop, Visual target, Panel layer)
    {
        var offset = target.TranslatePoint(default, layer) ?? default;
        Canvas.SetLeft(backdrop, offset.X);
        Canvas.SetTop(backdrop, offset.Y);
    }

    private void OnBlurTargetPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != Visual.BoundsProperty || _recapturePending)
        {
            return;
        }
        // Coalesce a resize into one new snapshot after layout has settled.
        _recapturePending = true;
        Dispatcher.UIThread.Post(() =>
        {
            _recapturePending = false;
            _captureFailed = false;
            if (_backdrop is { } backdrop && _blurTarget is { } target && _backdropLayer is { } layer)
            {
                PlaceBackdrop(backdrop, target, layer);
            }
            UpdateBlur();
        }, DispatcherPriority.Background);
    }

    /// <summary>Removes the backdrop and releases its bitmap.</summary>
    private void ClearBlur()
    {
        if (_blurTarget is { } target)
        {
            target.PropertyChanged -= OnBlurTargetPropertyChanged;
        }
        if (_backdrop is { } backdrop)
        {
            _backdropLayer?.Children.Remove(backdrop);
            backdrop.Release();
        }
        _backdrop = null;
        _blurTarget = null;
        _backdropLayer = null;
        _captureFailed = false;
    }

    /// <summary>Content root under the layer that hosts modals: the child of the owning visual layer manager.</summary>
    public static Visual? FindBlurTarget(Visual layer)
    {
        foreach (var ancestor in layer.GetVisualAncestors())
        {
            if (ancestor is VisualLayerManager manager)
            {
                return manager.Child;
            }
        }
        return null;
    }

    /// <summary>True when <paramref name="element"/> sits inside <paramref name="root"/> visually or logically (popups opened from it).</summary>
    public static bool IsWithin(Visual root, IInputElement element)
    {
        if (element is Visual visual && (ReferenceEquals(visual, root) || root.IsVisualAncestorOf(visual)))
        {
            return true;
        }
        return element is ILogical logical && root is ILogical rootLogical &&
               (ReferenceEquals(logical, rootLogical) || rootLogical.IsLogicalAncestorOf(logical));
    }
}
