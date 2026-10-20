using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// Finds the <see cref="IToastService"/> of a window. A <see cref="ToastStack"/> placed in the window registers
/// itself when it is attached; when there is none, the first request creates one on the window's overlay layer
/// (sized to the window, so it hangs at the bottom edge like Arc's fixed viewport).
/// </summary>
public static class ToastHost
{
    private static readonly ConditionalWeakTable<TopLevel, List<ToastStack>> s_stacks = new();

    /// <summary>The toast service of <paramref name="owner"/>'s window (created on its overlay layer if needed).</summary>
    /// <exception cref="InvalidOperationException">The owner is not attached to a window yet.</exception>
    public static IToastService GetService(Visual owner) => GetStack(owner);

    /// <summary>The <see cref="ToastStack"/> serving <paramref name="owner"/>'s window (created on its overlay layer if needed).</summary>
    /// <exception cref="InvalidOperationException">The owner is not attached to a window yet.</exception>
    public static ToastStack GetStack(Visual owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var topLevel = owner as TopLevel ?? TopLevel.GetTopLevel(owner)
            ?? throw new InvalidOperationException("Toasts need an owner that is attached to a window.");
        if (Find(topLevel) is { } existing)
        {
            return existing;
        }
        var overlay = FindOverlay(owner, topLevel)
            ?? throw new InvalidOperationException("The window has no overlay layer; place a ToastStack in it instead.");
        var stack = new ToastStack { IsAutoCreated = true };
        // The overlay is a canvas: size the stack to it so the toasts hang from the window's bottom edge.
        stack.Width = overlay.Bounds.Width > 0 ? overlay.Bounds.Width : topLevel.ClientSize.Width;
        stack.Height = overlay.Bounds.Height > 0 ? overlay.Bounds.Height : topLevel.ClientSize.Height;
        overlay.PropertyChanged += (_, e) =>
        {
            if (e.Property == Visual.BoundsProperty && e.NewValue is Rect bounds)
            {
                stack.Width = bounds.Width;
                stack.Height = bounds.Height;
            }
        };
        overlay.Children.Add(stack);
        Register(topLevel, stack);
        return stack;
    }

    private static OverlayLayer? FindOverlay(Visual owner, TopLevel topLevel)
    {
        var overlay = OverlayLayer.GetOverlayLayer(owner) ?? OverlayLayer.GetOverlayLayer(topLevel);
        if (overlay is not null)
        {
            return overlay;
        }
        // Layer managers create their overlay layer on demand.
        var manager = owner.FindAncestorOfType<VisualLayerManager>(includeSelf: true)
                      ?? topLevel.GetVisualDescendants().OfType<VisualLayerManager>().FirstOrDefault();
        if (manager is null)
        {
            return null;
        }
        manager.EnableOverlayLayer = true;
        return OverlayLayer.GetOverlayLayer(owner) ?? OverlayLayer.GetOverlayLayer(topLevel);
    }

    /// <summary>The stack registered for <paramref name="topLevel"/>, if any (a placed stack wins over an auto-created one).</summary>
    public static ToastStack? Find(TopLevel topLevel)
    {
        if (!s_stacks.TryGetValue(topLevel, out var stacks) || stacks.Count == 0)
        {
            return null;
        }
        for (var i = stacks.Count - 1; i >= 0; i--)
        {
            if (!stacks[i].IsAutoCreated)
            {
                return stacks[i];
            }
        }
        return stacks[^1];
    }

    internal static void Register(TopLevel topLevel, ToastStack stack)
    {
        var stacks = s_stacks.GetValue(topLevel, static _ => new List<ToastStack>());
        if (!stacks.Contains(stack))
        {
            stacks.Add(stack);
        }
    }

    internal static void Unregister(TopLevel topLevel, ToastStack stack)
    {
        if (s_stacks.TryGetValue(topLevel, out var stacks))
        {
            stacks.Remove(stack);
        }
    }
}
