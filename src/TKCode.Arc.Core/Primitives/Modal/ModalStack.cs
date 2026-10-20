using System.Runtime.CompilerServices;
using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace TKCode.Arc.Primitives;

/// <summary>
/// Per-TopLevel stack of mounted modals: routes Escape to the topmost one, keeps focus inside it, and owns the
/// backdrop blur on the window content (one blur for the whole stack, the strongest modal wins).
/// </summary>
internal sealed class ModalStack
{
    private static readonly ConditionalWeakTable<TopLevel, ModalStack> s_stacks = new();

    private readonly TopLevel _topLevel;
    private readonly List<ModalPresence> _items = new();
    private BlurEffect? _blur;
    private Visual? _blurTarget;
    private IEffect? _previousEffect;
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

    /// <summary>Applies the strongest backdrop blur of the stack to the window content under the overlay layer.</summary>
    public void UpdateBlur()
    {
        double radius = 0;
        Visual? target = null;
        foreach (var item in _items)
        {
            var r = item.CurrentBackdropBlur;
            if (r > radius)
            {
                radius = r;
                target = item.BlurTarget;
            }
        }
        if (radius < 0.05 || target is null)
        {
            ClearBlur();
            return;
        }
        if (!ReferenceEquals(target, _blurTarget))
        {
            ClearBlur();
            _blurTarget = target;
            _previousEffect = target.Effect;
            _blur ??= new BlurEffect();
            _blur.Radius = radius;
            target.Effect = _blur;
            return;
        }
        if (_blur is not null && Math.Abs(_blur.Radius - radius) >= 0.05)
        {
            _blur.Radius = radius;
        }
    }

    private void ClearBlur()
    {
        if (_blurTarget is { } target)
        {
            if (ReferenceEquals(target.Effect, _blur))
            {
                target.Effect = _previousEffect;
            }
            _blurTarget = null;
            _previousEffect = null;
        }
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
