using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using TKCode.Arc.Localization;
using AvaloniaTextBox = Avalonia.Controls.TextBox;

namespace TKCode.Arc;

/// <summary>
/// The default right-click menu of Arc's text inputs and selectable text blocks (<see cref="SelectableTextBlock"/>).
/// On text inputs it offers Cut, Copy, Paste, Select all, and Undo / Redo while the box keeps an undo history; on
/// selectable read-only text (including <c>DecoratedTextBlock</c> and <see cref="CodeBlock"/>) it offers Copy and
/// Select all. It opens on a right-click, the ContextMenu key and Shift+F10, as an Arc <see cref="MenuSurface"/>,
/// and its rows follow the target: Cut and Copy need a selection (a <see cref="PasswordField"/> never offers them),
/// Paste needs text on the clipboard, and a read-only box shows neither Cut, Paste, Undo nor Redo. A
/// <c>ContextMenu</c>, <c>ContextFlyout</c> or <see cref="ContextMenu.MenuProperty"/> set by the app replaces it.
/// </summary>
/// <remarks>
/// <see cref="IsEnabledProperty"/> is inherited, so setting it on a window or form turns the default menu off (or on again)
/// for every text input and <see cref="SelectableTextBlock"/> below it; each text input also exposes it as
/// <c>IsDefaultContextMenuEnabled</c>.
/// </remarks>
public static class TextContextMenu
{
    /// <summary>Whether text inputs and selectable text blocks show the default context menu (inherited; default true).</summary>
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<AvaloniaObject, AvaloniaObject, bool>("IsEnabled", true, inherits: true);

    /// <summary>Gets <see cref="IsEnabledProperty"/>.</summary>
    public static bool GetIsEnabled(AvaloniaObject element) => element.GetValue(IsEnabledProperty);

    /// <summary>Sets <see cref="IsEnabledProperty"/>.</summary>
    public static void SetIsEnabled(AvaloniaObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    private static readonly AttachedProperty<MenuSurface?> SurfaceProperty =
        AvaloniaProperty.RegisterAttached<AvaloniaObject, Control, MenuSurface?>("Surface");

    private static readonly AttachedProperty<SelectableState?> SelectableStateProperty =
        AvaloniaProperty.RegisterAttached<AvaloniaObject, SelectableTextBlock, SelectableState?>("SelectableState");

    static TextContextMenu()
    {
        Control.ContextRequestedEvent.AddClassHandler<SelectableTextBlock>(static (block, e) => Handle(block, e));
    }

    /// <summary>Ensures the default context menu class handler is registered for <see cref="SelectableTextBlock"/>.</summary>
    internal static void Register()
    {
    }

    /// <summary>The surface last opened for <paramref name="target"/> (null before the first open).</summary>
    internal static MenuSurface? SurfaceOf(Control target) => target.GetValue(SurfaceProperty);

    /// <summary>Handles <c>ContextRequested</c> of a text box; returns whether the default menu took the request.</summary>
    internal static bool Handle(AvaloniaTextBox box, ContextRequestedEventArgs e)
    {
        if (!ShouldHandle(box, e))
        {
            return false;
        }
        e.Handled = true;
        var keyboard = !e.TryGetPosition(box, out var point);
        _ = OpenAsync(box, keyboard ? ContextMenu.BelowTarget(box) : point, keyboard);
        return true;
    }

    /// <summary>Handles <c>ContextRequested</c> of a selectable text block; returns whether the default menu took the request.</summary>
    internal static bool Handle(SelectableTextBlock block, ContextRequestedEventArgs e)
    {
        if (!ShouldHandle(block, e))
        {
            return false;
        }
        e.Handled = true;
        var keyboard = !e.TryGetPosition(block, out var point);
        Open(block, keyboard ? ContextMenu.BelowTarget(block) : point, keyboard);
        return true;
    }

    private static bool ShouldHandle(Control target, ContextRequestedEventArgs e)
        => !e.Handled && GetIsEnabled(target) && target.ContextFlyout is null && target.ContextMenu is null
            && target.GetValue(ContextMenu.MenuProperty) is null;

    private static async Task OpenAsync(AvaloniaTextBox box, Point point, bool keyboard)
    {
        var canPaste = !box.IsReadOnly && await ClipboardHasTextAsync(box);
        if (TopLevel.GetTopLevel(box) is null)
        {
            return;
        }
        var surface = box.GetValue(SurfaceProperty);
        if (surface is null)
        {
            surface = new MenuSurface { Variant = MenuSurfaceVariant.Context, ClosesOnViewportChange = true, FocusReturnTarget = box };
            surface.ItemClicked += (_, args) => Run(box, args.Entry.Tag as string);
            box.SetValue(SurfaceProperty, surface);
        }
        AutomationProperties.SetName(surface, TextContextMenuLangResources.Get(TextContextMenuLangResourceKind.TextMenu));
        Populate(surface, box, canPaste);
        // A pointer open leaves focus (and so the selection) in the box; the keyboard moves into the menu.
        surface.OpenAt(box, point, keyboard ? MenuOpenFocus.First : MenuOpenFocus.None);
    }

    private static void Open(SelectableTextBlock block, Point point, bool keyboard)
    {
        if (TopLevel.GetTopLevel(block) is null)
        {
            return;
        }
        var surface = block.GetValue(SurfaceProperty);
        var state = block.GetValue(SelectableStateProperty);
        if (surface is null || state is null)
        {
            state = new SelectableState();
            surface = new MenuSurface { Variant = MenuSurfaceVariant.Context, ClosesOnViewportChange = true, FocusReturnTarget = block };
            surface.ItemClicked += (_, args) => Run(block, args.Entry.Tag as string, state);
            // SelectableTextBlock clears its selection in OnLostFocus whenever Avalonia's own ContextMenu / ContextFlyout
            // is not open; restore it when focus moves into this menu so Copy (and Escape) keep the user's selection.
            surface.AddHandler(InputElement.GotFocusEvent, (_, _) =>
            {
                if (surface.IsOpen && state.Start != state.End && block.SelectionStart == block.SelectionEnd)
                {
                    block.SetCurrentValue(SelectableTextBlock.SelectionStartProperty, state.Start);
                    block.SetCurrentValue(SelectableTextBlock.SelectionEndProperty, state.End);
                    state.Restored = true;
                }
            });
            surface.Closed += (_, _) =>
            {
                if (state.Restored && !block.IsFocused)
                {
                    block.ClearSelection();
                }
                state.Restored = false;
            };
            block.SetValue(SurfaceProperty, surface);
            block.SetValue(SelectableStateProperty, state);
        }
        state.Start = block.SelectionStart;
        state.End = block.SelectionEnd;
        state.Restored = false;
        AutomationProperties.SetName(surface, TextContextMenuLangResources.Get(TextContextMenuLangResourceKind.TextMenu));
        Populate(surface, block);
        // A pointer open leaves focus (and so the selection) in the block; the keyboard moves into the menu.
        surface.OpenAt(block, point, keyboard ? MenuOpenFocus.First : MenuOpenFocus.None);
    }

    private static async Task<bool> ClipboardHasTextAsync(AvaloniaTextBox box)
    {
        var clipboard = TopLevel.GetTopLevel(box)?.Clipboard;
        if (clipboard is null)
        {
            return false;
        }
        try
        {
            return !string.IsNullOrEmpty(await clipboard.TryGetTextAsync());
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void Populate(MenuSurface surface, AvaloniaTextBox box, bool canPaste)
    {
        var hasSelection = box.SelectionStart != box.SelectionEnd;
        var secret = box is PasswordField;
        var editable = !box.IsReadOnly;
        var keys = Application.Current?.PlatformSettings?.HotkeyConfiguration;
        surface.Items.Clear();
        if (editable && box.IsUndoEnabled)
        {
            surface.Items.Add(Entry(TextContextMenuLangResourceKind.Undo, "undo", box.CanUndo, keys?.Undo));
            surface.Items.Add(Entry(TextContextMenuLangResourceKind.Redo, "redo", box.CanRedo, keys?.Redo));
        }
        if (editable)
        {
            surface.Items.Add(Entry(TextContextMenuLangResourceKind.Cut, "cut", hasSelection && !secret, keys?.Cut,
                separator: surface.Items.Count > 0));
        }
        surface.Items.Add(Entry(TextContextMenuLangResourceKind.Copy, "copy", hasSelection && !secret, keys?.Copy,
            separator: surface.Items.Count > 0 && !editable));
        if (editable)
        {
            surface.Items.Add(Entry(TextContextMenuLangResourceKind.Paste, "paste", canPaste, keys?.Paste));
        }
        surface.Items.Add(Entry(TextContextMenuLangResourceKind.SelectAll, "select-all", !string.IsNullOrEmpty(box.Text), keys?.SelectAll,
            separator: true));
    }

    private static void Populate(MenuSurface surface, SelectableTextBlock block)
    {
        var keys = Application.Current?.PlatformSettings?.HotkeyConfiguration;
        surface.Items.Clear();
        surface.Items.Add(Entry(TextContextMenuLangResourceKind.Copy, "copy", block.CanCopy, keys?.Copy));
        surface.Items.Add(Entry(TextContextMenuLangResourceKind.SelectAll, "select-all", HasSelectableText(block), keys?.SelectAll,
            separator: true));
    }

    private static bool HasSelectableText(SelectableTextBlock block)
    {
        var text = block.Inlines is { Count: > 0 } inlines ? inlines.Text : block.Text;
        return !string.IsNullOrEmpty(text);
    }

    private static MenuEntry Entry(TextContextMenuLangResourceKind text, string tag, bool enabled, List<KeyGesture>? gestures,
        bool separator = false)
        => new()
        {
            Header = TextContextMenuLangResources.Get(text),
            Tag = tag,
            IsEnabled = enabled,
            InputGesture = gestures is { Count: > 0 } ? gestures[0] : null,
            HasSeparatorBefore = separator
        };

    private static void Run(AvaloniaTextBox box, string? action)
    {
        box.Focus();
        switch (action)
        {
            case "undo":
                box.Undo();
                break;
            case "redo":
                box.Redo();
                break;
            case "cut":
                box.Cut();
                break;
            case "copy":
                box.Copy();
                break;
            case "paste":
                box.Paste();
                break;
            case "select-all":
                box.SelectAll();
                break;
        }
    }

    private static void Run(SelectableTextBlock block, string? action, SelectableState state)
    {
        state.Restored = false;
        block.Focus();
        switch (action)
        {
            case "copy":
                if (block.SelectionStart == block.SelectionEnd && state.Start != state.End)
                {
                    block.SetCurrentValue(SelectableTextBlock.SelectionStartProperty, state.Start);
                    block.SetCurrentValue(SelectableTextBlock.SelectionEndProperty, state.End);
                }
                block.Copy();
                break;
            case "select-all":
                block.SelectAll();
                break;
        }
    }

    private sealed class SelectableState
    {
        public int Start;
        public int End;
        public bool Restored;
    }
}
