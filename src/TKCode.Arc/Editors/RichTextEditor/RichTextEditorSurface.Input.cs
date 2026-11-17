using System.Collections.Immutable;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using Avalonia.Utilities;

namespace TKCode.Arc;

internal partial class RichTextEditorSurface
{
    /* ----------------------------------------------------------------------------------------------------------
     * Input
     * -------------------------------------------------------------------------------------------------------- */

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _caret.Restart();
        InvalidateArrange();
        InvalidateVisual();
        Owner?.OnSurfaceFocusChanged(true);
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        if (IsComposing)
        {
            SetPreedit(null, null);
        }
        InvalidateArrange();
        InvalidateVisual();
        Owner?.OnSurfaceFocusChanged(false);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && !IsComposing)
        {
            Owner?.HandleKeyDown(e);
        }
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (e.Handled || string.IsNullOrEmpty(e.Text))
        {
            return;
        }
        if (OperatingSystem.IsBrowser() && !IsComposing && !_composed && e.Text!.Length == 1 && !char.IsWhiteSpace(e.Text[0]))
        {
            // The browser host also sends the keydown's letter as text while the input method starts composing it; the
            // composition events arrive just after, so wait for them and drop the letter when one began.
            e.Handled = true;
            var letter = e.Text;
            Dispatcher.UIThread.Post(() =>
            {
                if (!IsComposing && !_composed)
                {
                    Owner?.HandleTextInput(letter, false);
                }
            }, DispatcherPriority.Background);
            return;
        }
        var composition = _composed || IsComposing;
        if (IsComposing)
        {
            _preedit = null;
            _preeditCursor = null;
        }
        _composed = false;
        Owner?.HandleTextInput(e.Text!, composition);
        e.Handled = true;
    }

    internal void SetPreedit(string? text, int? cursor)
    {
        var next = string.IsNullOrEmpty(text) ? null : text;
        if (next is null && _preedit is not null)
        {
            // A commit usually follows in the same input batch; it must not run Markdown shortcuts.
            _composed = true;
            Dispatcher.UIThread.Post(() => _composed = false, DispatcherPriority.Background);
        }
        if (string.Equals(next, _preedit, StringComparison.Ordinal) && cursor == _preeditCursor)
        {
            return;
        }
        if (next is not null && _preedit is null)
        {
            Owner?.OnCompositionStarted();
        }
        _preedit = next;
        _preeditCursor = cursor;
        InvalidateMeasure();
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var owner = Owner;
        var engine = Engine;
        if (owner is null || engine is null)
        {
            return;
        }
        var point = e.GetCurrentPoint(this);
        if (e.Pointer.Type == PointerType.Touch)
        {
            _touchStart = point.Position;
            return;
        }
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }
        Focus(NavigationMethod.Pointer);
        if (OverResizeGrip(point.Position, out var gripBlock))
        {
            _resizeIndex = gripBlock;
            _resizeBlock = engine.Blocks[gripBlock];
            _resizeStartX = point.Position.X;
            _resizeStartWidth = _views[gripBlock].ImageSize.Width;
            _resizeWidth = _resizeStartWidth;
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }
        if (GripAt(point.Position) is { } grip)
        {
            _gripDrag = new GripDrag(grip.Start, grip.Column, grip.Index, point.Position);
            e.Pointer.Capture(this);
            InvalidateVisual();
            e.Handled = true;
            return;
        }
        if (ColumnBorderAt(point.Position) is { } border)
        {
            if (e.ClickCount >= 2)
            {
                // A double click on a border shares the width evenly again.
                engine.SetColumnWidths(border.Start, Array.Empty<int>());
            }
            else
            {
                BeginColumnDrag(border.Start, border.Border, point.Position.X);
                e.Pointer.Capture(this);
            }
            e.Handled = true;
            return;
        }
        if (CodeHeaderAt(point.Position) is { } header)
        {
            owner.OnCodeLanguagePressed(header.Block, header.Rect);
            e.Handled = true;
            return;
        }
        if (ToggleAt(point.Position) is >= 0 and var toggle)
        {
            owner.OnTogglePressed(toggle);
            e.Handled = true;
            return;
        }
        if (TodoAt(point.Position) is >= 0 and var todo)
        {
            owner.OnTodoPressed(todo);
            e.Handled = true;
            return;
        }
        if (IsComposing)
        {
            // A click ends the composition where it is; the input method starts over at the new caret.
            SetPreedit(null, null);
            _ime.Reset();
        }
        owner.OnSurfacePointerDown();
        var position = HitTest(point.Position);
        GoalX = null;
        _dragClicks = e.ClickCount;
        if (e.ClickCount == 2)
        {
            var (start, end) = RichTextEditorText.WordAt(engine.Blocks[position.Block].Text, position.Offset);
            engine.SetSelection(RichTextEditorSelection.Range(position.Block, start, end));
            _dragOrigin = position;
        }
        else if (e.ClickCount >= 3)
        {
            engine.SetSelection(RichTextEditorSelection.Range(position.Block, 0, engine.Blocks[position.Block].Length));
            _dragOrigin = position;
        }
        else if ((e.KeyModifiers & KeyModifiers.Shift) != 0)
        {
            engine.SetSelection(engine.Selection with { Focus = position });
        }
        else
        {
            engine.SetSelection(RichTextEditorSelection.Caret(position));
        }
        IsDragging = true;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var engine = Engine;
        if (_resizeBlock is not null)
        {
            UpdateResize(e.GetPosition(this).X);
            return;
        }
        if (_columnDrag is not null)
        {
            UpdateColumnDrag(e.GetPosition(this).X);
            return;
        }
        var here = e.GetPosition(this);
        if (_gripDrag is { } grip)
        {
            grip.Moved |= Math.Abs(here.X - grip.From.X) + Math.Abs(here.Y - grip.From.Y) > 4;
            grip.Gap = grip.Moved ? DropGap(grip, here) : -1;
            InvalidateVisual();
            return;
        }
        var hover = _tables.FindIndex(t => here.Y >= t.Top - 4 && here.Y <= t.Bottom + 4);
        if (hover != _hoverTable)
        {
            _hoverTable = hover;
            InvalidateVisual();
        }
        // Shared cursors: a pointer move must not allocate a platform cursor.
        Cursor = OverResizeGrip(here, out _) ? s_resizeCursor
            : GripAt(here) is not null ? s_handCursor
            : ColumnBorderAt(here) is not null ? s_columnCursor
            : s_ibeamCursor;
        if (!IsDragging || engine is null || !ReferenceEquals(e.Pointer.Captured, this))
        {
            return;
        }
        var position = HitTest(e.GetPosition(this));
        if (_dragClicks >= 2)
        {
            // Word and block drags extend by whole words or blocks from the original one.
            var origin = _dragClicks == 2 ? RichTextEditorText.WordAt(engine.Blocks[_dragOrigin.Block].Text, _dragOrigin.Offset) : (0, engine.Blocks[_dragOrigin.Block].Length);
            var forward = position >= _dragOrigin;
            var text = engine.Blocks[position.Block].Text;
            var extent = _dragClicks == 2 ? RichTextEditorText.WordAt(text, position.Offset) : (0, text.Length);
            var anchor = new RichTextEditorPosition(_dragOrigin.Block, forward ? origin.Item1 : origin.Item2);
            var focus = new RichTextEditorPosition(position.Block, forward ? extent.Item2 : extent.Item1);
            engine.SetSelection(new RichTextEditorSelection(anchor, focus));
        }
        else
        {
            engine.SetSelection(engine.Selection with { Focus = position });
        }
        Owner?.RevealCaret();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_resizeBlock is not null)
        {
            EndResize(commit: true);
            e.Pointer.Capture(null);
            return;
        }
        if (_columnDrag is not null)
        {
            EndColumnDrag(commit: true);
            e.Pointer.Capture(null);
            return;
        }
        if (_gripDrag is not null)
        {
            EndGripDrag(commit: true);
            e.Pointer.Capture(null);
            return;
        }
        var engine = Engine;
        if (e.Pointer.Type == PointerType.Touch && _touchStart is { } start && engine is not null)
        {
            var position = e.GetPosition(this);
            _touchStart = null;
            if (Math.Abs(position.X - start.X) < 8 && Math.Abs(position.Y - start.Y) < 8)
            {
                Focus(NavigationMethod.Pointer);
                Owner?.OnSurfacePointerDown();
                engine.SetSelection(RichTextEditorSelection.Caret(HitTest(position)));
                Owner?.OnDragEnded();
            }
            return;
        }
        if (IsDragging)
        {
            IsDragging = false;
            if (ReferenceEquals(e.Pointer.Captured, this))
            {
                e.Pointer.Capture(null);
            }
            Owner?.OnDragEnded();
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        EndColumnDrag(commit: false);
        EndGripDrag(commit: false);
        if (IsDragging)
        {
            IsDragging = false;
            Owner?.OnDragEnded();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_hoverTable >= 0 && _gripDrag is null)
        {
            _hoverTable = -1;
            InvalidateVisual();
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new RichTextEditorSurfaceAutomationPeer(this);

    /* ----------------------------------------------------------------------------------------------------------
     * IME support
     * -------------------------------------------------------------------------------------------------------- */

    internal string ImeSurroundingText
    {
        get
        {
            var engine = Engine;
            return engine is null ? string.Empty : engine.Blocks[engine.Selection.Focus.Block].Text;
        }
    }

    internal TextSelection ImeSelection
    {
        get
        {
            var engine = Engine;
            if (engine is null)
            {
                return default;
            }
            var selection = engine.Selection;
            if (selection.Anchor.Block != selection.Focus.Block)
            {
                return new TextSelection(selection.Focus.Offset, selection.Focus.Offset);
            }
            return new TextSelection(selection.Anchor.Offset, selection.Focus.Offset);
        }
    }

    internal void ImeSetSelection(TextSelection value)
    {
        var engine = Engine;
        if (engine is null)
        {
            return;
        }
        var block = engine.Selection.Focus.Block;
        engine.SetSelection(new RichTextEditorSelection(new RichTextEditorPosition(block, value.Start), new RichTextEditorPosition(block, value.End)));
    }

    internal Rect ImeCursorRect
    {
        get
        {
            var caret = CaretBounds();
            return caret ?? default;
        }
    }
}
