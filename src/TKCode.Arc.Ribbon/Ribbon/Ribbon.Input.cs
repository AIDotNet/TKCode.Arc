using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc;

public partial class Ribbon
{
    private void OnRibbonKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }
        if (e.Key == Key.F1 && e.KeyModifiers == KeyModifiers.Control)
        {
            e.Handled = true;
            ToggleMinimized();
            return;
        }
        if ((e.Key == Key.Apps || (e.Key == Key.F10 && e.KeyModifiers == KeyModifiers.Shift)) && e.Source is RibbonButton target)
        {
            // The menu key: the same menu as a right-click on the command.
            e.Handled = OpenCommandMenu(target, null);
            return;
        }
        if (e.Source is RibbonTabHeader header)
        {
            OnHeaderKeyDown(header, e);
        }
        else if (e.Source is RibbonButton button && e.KeyModifiers == KeyModifiers.None)
        {
            OnCommandKeyDown(button, e);
        }
    }

    private void OnHeaderKeyDown(RibbonTabHeader header, KeyEventArgs e)
    {
        var index = _headers.IndexOf(header);
        if (index < 0 || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        int? target = e.Key switch
        {
            Key.Left => NextHeader(index, -1),
            Key.Right => NextHeader(index, 1),
            Key.Home => NextHeader(-1, 1),
            Key.End => NextHeader(_headers.Count, -1),
            _ => null
        };
        if (target is { } next)
        {
            e.Handled = true;
            if (next >= 0)
            {
                Select(next);
                _headers[next].Focus(NavigationMethod.Directional);
            }
            return;
        }
        switch (e.Key)
        {
            case Key.Enter:
            case Key.Space:
                e.Handled = true;
                if (IsMinimized)
                {
                    OpenPopupAndFocus();
                }
                break;
            case Key.Down:
                e.Handled = true;
                if (IsMinimized)
                {
                    OpenPopupAndFocus();
                }
                else
                {
                    FocusFirstCommand(SelectedTab);
                }
                break;
        }
    }

    // The pop-up's content is laid out on the next pass; focus its first command then.
    private void OpenPopupAndFocus()
    {
        OpenPopup();
        Dispatcher.UIThread.Post(() => FocusFirstCommand(_popupSurface), DispatcherPriority.Input);
    }

    // The next selectable header from `from` in `step` direction (looping), or -1.
    private int NextHeader(int from, int step)
    {
        var count = _headers.Count;
        for (var i = 1; i <= count; i++)
        {
            var index = ((from + step * i) % count + count) % count;
            if (CanSelect(_headers[index].Tab))
            {
                return index;
            }
        }
        return -1;
    }

    private void OnCommandKeyDown(RibbonButton button, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Left:
            case Key.Right:
                var commands = CommandsAround(button);
                var index = commands.IndexOf(button);
                if (index >= 0 && commands.Count > 1)
                {
                    e.Handled = true;
                    var next = commands[((index + (e.Key == Key.Right ? 1 : -1)) % commands.Count + commands.Count) % commands.Count];
                    next.Focus(NavigationMethod.Directional);
                }
                break;
            case Key.Escape when !IsPopupOpen && button.FindAncestorOfType<RibbonTab>() is not null:
                e.Handled = true;
                HeaderOf(SelectedTab)?.Focus(NavigationMethod.Tab);
                break;
        }
    }

    // The commands that arrow keys move between: those of the same surface (the selected tab, or the drop-down the button is
    // in), in visual order.
    private static List<RibbonButton> CommandsAround(RibbonButton button)
    {
        Visual? root = button.FindAncestorOfType<RibbonGroupDropDown>();
        root ??= button.FindAncestorOfType<RibbonTab>();
        var list = new List<RibbonButton>();
        if (root is not null)
        {
            Collect(root, list);
        }
        return list;
    }

    private static void Collect(Visual visual, List<RibbonButton> list)
    {
        foreach (var child in visual.GetVisualChildren())
        {
            if (child is RibbonButton button)
            {
                if (IsReachable(button))
                {
                    list.Add(button);
                }
                continue;
            }
            Collect(child, list);
        }
    }

    private static bool IsReachable(RibbonButton button) =>
        button.IsEffectivelyVisible && button.IsEffectivelyEnabled && button.Focusable && button.IsHitTestVisible
        && button.Opacity > 0 && button.Bounds.Width > 0;

    /// <summary>Gives keyboard focus to the first command under <paramref name="root"/>.</summary>
    internal static void FocusFirstCommand(Visual? root)
    {
        if (root is null)
        {
            return;
        }
        var list = new List<RibbonButton>();
        Collect(root, list);
        if (list.Count > 0)
        {
            list[0].Focus(NavigationMethod.Tab);
        }
    }
}
