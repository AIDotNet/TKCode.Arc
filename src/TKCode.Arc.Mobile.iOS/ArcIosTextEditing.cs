using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ObjCRuntime;
using UIKit;

namespace TKCode.Arc.Mobile;

/// <summary>
/// The iOS edit actions (<c>cut:</c>, <c>copy:</c>, <c>paste:</c>, <c>selectAll:</c>) for Avalonia text boxes.
/// </summary>
/// <remarks>
/// <para>
/// iOS sends these actions along the responder chain: the system edit menu, the keyboard's "Paste from …" suggestion
/// and the three-finger gestures all use them. Avalonia's text input responder (12.1.5) does not implement them, so they
/// travel up to the app delegate. <see cref="ArcAppDelegate{TApp}"/> answers them with this class; an app that keeps its
/// own delegate base forwards <c>CanPerform</c>, <c>Cut</c>, <c>Copy</c>, <c>Paste</c> and <c>SelectAll</c> to
/// <see cref="CanPerform"/> and <see cref="Perform"/> and calls <see cref="Install"/> once.
/// </para>
/// <para>
/// Paste reads the pasteboard inside the system's own paste action, so iOS does not show its "Allow Paste" prompt.
/// </para>
/// </remarks>
public static class ArcIosTextEditing
{
    private static readonly Selector s_cut = new("cut:");
    private static readonly Selector s_copy = new("copy:");
    private static readonly Selector s_paste = new("paste:");
    private static readonly Selector s_selectAll = new("selectAll:");

    private static WeakReference<TextBox>? s_focused;
    private static bool s_installed;

    /// <summary>
    /// Starts following the focused text box and, on iOS 16 and later, makes <see cref="TextEditMenu"/> show the system
    /// edit menu (<see cref="TextEditMenu.Platform"/>, unless something else set it first). Safe to call more than once.
    /// </summary>
    public static void Install()
    {
        if (s_installed)
        {
            return;
        }
        s_installed = true;
        InputElement.GotFocusEvent.AddClassHandler<TextBox>((box, _) => s_focused = new WeakReference<TextBox>(box));
        InputElement.LostFocusEvent.AddClassHandler<TextBox>((box, _) =>
        {
            if (s_focused is not null && s_focused.TryGetTarget(out var focused) && focused == box)
            {
                s_focused = null;
            }
        });
        if (OperatingSystem.IsIOSVersionAtLeast(16))
        {
            TextEditMenu.Platform ??= new IosTextEditMenuPlatform();
        }
    }

    /// <summary>The text box that has the focus now, which the edit actions act on; null when none has.</summary>
    public static TextBox? FocusedTextBox =>
        s_focused is not null && s_focused.TryGetTarget(out var box) && box.IsFocused && box.IsEffectivelyEnabled ? box : null;

    /// <summary>
    /// Whether <paramref name="action"/> applies to the focused text box now: Cut and Copy need a selection (never in a
    /// password box), Cut and Paste an editable box, Paste text on the pasteboard, Select All text that is not all
    /// selected. Null for any other selector, which the caller passes on to its base class.
    /// </summary>
    public static bool? CanPerform(Selector action)
    {
        if (ActionOf(action) is not { } kind)
        {
            return null;
        }
        if (FocusedTextBox is not { } box)
        {
            return false;
        }
        return kind switch
        {
            ContextMenuAction.Cut => box.CanCut,
            ContextMenuAction.Copy => box.CanCopy,
            ContextMenuAction.Paste => box.CanPaste && UIPasteboard.General.HasStrings,
            _ => !string.IsNullOrEmpty(box.Text) && !IsAllSelected(box),
        };
    }

    /// <summary>Runs <paramref name="action"/> on the focused text box; returns false when no text box has the focus.</summary>
    public static bool Perform(ContextMenuAction action)
    {
        if (FocusedTextBox is not { } box)
        {
            return false;
        }
        switch (action)
        {
            case ContextMenuAction.Cut:
                box.Cut();
                break;
            case ContextMenuAction.Copy:
                box.Copy();
                break;
            case ContextMenuAction.Paste:
                Paste(box);
                break;
            case ContextMenuAction.SelectAll:
                box.SelectAll();
                // As in a UITextField, the menu comes back for the new selection.
                if (box.ContextFlyout is TextEditMenu && box.SelectionStart != box.SelectionEnd)
                {
                    Dispatcher.UIThread.Post(() => box.RaiseEvent(new ContextRequestedEventArgs()));
                }
                break;
        }
        return true;
    }

    /// <summary>
    /// Pastes like <see cref="TextBox.Paste"/> (the <see cref="TextBox.PastingFromClipboard"/> event can take it over),
    /// but reads the pasteboard now, while the system's paste action runs: a read after an await would be a programmatic
    /// read and bring up the "Allow Paste" prompt.
    /// </summary>
    private static void Paste(TextBox box)
    {
        var pasting = new RoutedEventArgs(TextBox.PastingFromClipboardEvent);
        box.RaiseEvent(pasting);
        if (pasting.Handled)
        {
            return;
        }
        var text = UIPasteboard.General.String;
        if (!string.IsNullOrEmpty(text))
        {
            // Replaces the selection, or inserts at the caret, through TextBox's own input path (read-only, MaxLength,
            // single-line clean-up and undo all apply).
            box.SelectedText = text;
        }
    }

    private static bool IsAllSelected(TextBox box)
    {
        var length = box.Text?.Length ?? 0;
        return length > 0 && Math.Min(box.SelectionStart, box.SelectionEnd) == 0 && Math.Max(box.SelectionStart, box.SelectionEnd) >= length;
    }

    private static ContextMenuAction? ActionOf(Selector action)
    {
        var handle = action.Handle;
        if (handle == s_paste.Handle)
        {
            return ContextMenuAction.Paste;
        }
        if (handle == s_copy.Handle)
        {
            return ContextMenuAction.Copy;
        }
        if (handle == s_cut.Handle)
        {
            return ContextMenuAction.Cut;
        }
        if (handle == s_selectAll.Handle)
        {
            return ContextMenuAction.SelectAll;
        }
        return null;
    }
}
