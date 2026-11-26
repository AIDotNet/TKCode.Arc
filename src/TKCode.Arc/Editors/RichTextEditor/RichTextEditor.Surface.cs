using K = TKCode.Arc.Localization.RichTextEditorLangResourceKind;
using TKCode.Arc.Primitives;
using System.Collections.Immutable;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

public partial class RichTextEditor
{
    /* ----------------------------------------------------------------------------------------------------------
     * Surface callbacks
     * -------------------------------------------------------------------------------------------------------- */

    /// <summary>A press on a to-do checkbox: flips the item (the caret stays where it was).</summary>
    /// <summary>A press on a toggle item's arrow: folds or unfolds it.</summary>
    internal void OnTogglePressed(int block)
    {
        if (IsReadOnly)
        {
            return;
        }
        var collapse = !_engine.Blocks[block].IsCollapsed;
        if (_engine.SetCollapsed(block, collapse))
        {
            Announce(collapse ? RichTextEditorStrings.Get(K.AnnounceCollapsed) : RichTextEditorStrings.Get(K.AnnounceExpanded));
        }
    }

    /// <summary>
    /// Folds the toggle item holding the caret, or unfolds it when it is folded (Ctrl+Enter, or a click on its arrow). A toggle item is a
    /// list item (the Docs profile's "Toggle list") whose nested items fold away; the folded state is stored, as <c>data-collapsed</c> in Html.
    /// </summary>
    /// <returns>True when the toggle changed.</returns>
    public bool ToggleFold()
    {
        var index = _engine.Selection.Focus.Block;
        if (IsReadOnly || !_engine.Blocks[index].IsToggle)
        {
            return false;
        }
        var collapse = !_engine.Blocks[index].IsCollapsed;
        if (!_engine.SetCollapsed(index, collapse))
        {
            return false;
        }
        Announce(collapse ? RichTextEditorStrings.Get(K.AnnounceCollapsed) : RichTextEditorStrings.Get(K.AnnounceExpanded));
        return true;
    }

    internal void OnTodoPressed(int block)
    {
        if (IsReadOnly)
        {
            return;
        }
        var item = _engine.Blocks[block];
        if (_engine.SetChecked(block, !item.IsChecked))
        {
            Announce(item.IsChecked ? RichTextEditorStrings.Get(K.AnnounceUnchecked) : RichTextEditorStrings.Get(K.AnnounceChecked));
        }
    }

    internal void OnSurfacePointerDown()
    {
        if (_toolbar is { IsLinkFace: true })
        {
            // Arc: a press outside the toolbar drops the link field without applying it.
            _savedLinkRange = null;
            _toolbar.SetLinkFace(false, animate: false);
            _toolbar.Dismiss();
            StopLinkWatch();
        }
        CloseSlash();
    }

    internal void OnDragEnded() => ScheduleChromeSync();

    internal void OnSurfaceFocusChanged(bool focused)
    {
        if (!focused)
        {
            CloseSlashIfFocusLeft();
            FlushPending();
        }
        ScheduleChromeSync();
    }

    private void CloseSlashIfFocusLeft()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!IsFocusWithinEditor())
            {
                CloseSlash();
                ScheduleChromeSync();
            }
        }, DispatcherPriority.Loaded);
    }

    private bool IsFocusWithinEditor() =>
        _surface is { IsKeyboardFocusWithin: true } || _overlay is { IsKeyboardFocusWithin: true };
}
