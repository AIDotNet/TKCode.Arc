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
     * Text input and keyboard
     * -------------------------------------------------------------------------------------------------------- */

    internal void HandleTextInput(string text, bool composition)
    {
        if (IsReadOnly)
        {
            return;
        }
        _surface!.GoalX = null;
        // Line breaks and tabs only come from their keys (Enter, Shift+Enter, Tab), never as typed text.
        text = text.Replace("\r", string.Empty).Replace("\n", string.Empty).Replace("\t", string.Empty);
        if (text.Length == 0)
        {
            return;
        }
        var result = _engine.InsertText(text, composition);
        if (composition)
        {
            _engine.MarkBoundary();
        }
        if (result.Converted)
        {
            Announce("Formatted");
        }
        if (result.SlashTyped)
        {
            var caret = _engine.Selection.Focus;
            _slash = (caret.Block, caret.Offset - 1, string.Empty);
            _slashActive = 0;
            ScheduleChromeSync();
        }
    }

    internal void OnCompositionStarted()
    {
        if (!_engine.Selection.IsCollapsed)
        {
            // Composition replaces the selection like typing does.
            _engine.DeleteBackward();
        }
    }

    internal void HandleKeyDown(KeyEventArgs e)
    {
        var surface = _surface!;
        var keymap = EditorsHotkeys.Keymap;
        var mod = EditorsHotkeys.IsMod(e.KeyModifiers);
        var shift = (e.KeyModifiers & KeyModifiers.Shift) != 0;
        var alt = (e.KeyModifiers & KeyModifiers.Alt) != 0;
        var key = e.Key;

        if (!IsReadOnly && IsSlashOpen)
        {
            var results = RichTextEditorSlashItem.Filter(_slash!.Value.Query, _engine.Docs);
            var index = Math.Min(_slashActive, results.Count - 1);
            switch (key)
            {
                case Key.Down:
                case Key.Up:
                    _slashActive = (index + (key == Key.Down ? 1 : -1) + results.Count) % results.Count;
                    Announce(results[_slashActive].Label);
                    ScheduleChromeSync();
                    e.Handled = true;
                    return;
                case Key.Enter:
                case Key.Tab:
                    ChooseSlash(results[index]);
                    e.Handled = true;
                    return;
                case Key.Escape:
                    CloseSlash();
                    ScheduleChromeSync();
                    e.Handled = true;
                    return;
            }
        }

        if (mod && key is Key.F or Key.H && !alt)
        {
            OpenFind(replace: key == Key.H && !shift);
            e.Handled = true;
            return;
        }
        if (key == Key.F3 && IsFindOpen)
        {
            StepMatch(shift ? -1 : 1);
            e.Handled = true;
            return;
        }
        if (key == Key.Escape && IsFindOpen)
        {
            CloseFind();
            e.Handled = true;
            return;
        }
        // Clipboard and selection work in read-only mode too.

        if (EditorsHotkeys.Matches(keymap?.Copy, e) || (mod && !shift && key == Key.C))
        {
            _ = CopyAsync();
            e.Handled = true;
            return;
        }
        if (EditorsHotkeys.Matches(keymap?.SelectAll, e) || (mod && !shift && key == Key.A))
        {
            CloseSlash();
            _engine.SelectAll();
            e.Handled = true;
            return;
        }
        if (mod && shift && _engine.Docs && !IsReadOnly && key is Key.Up or Key.Down)
        {
            if (_engine.MoveUnitBy(_engine.Selection.Focus.Block, key == Key.Up ? -1 : 1))
            {
                Announce("Block moved");
            }
            e.Handled = true;
            return;
        }
        if (HandleNavigation(e, surface))
        {
            e.Handled = true;
            return;
        }
        if (IsReadOnly)
        {
            return;
        }
        if (EditorsHotkeys.Matches(keymap?.Cut, e) || (mod && !shift && key == Key.X))
        {
            _ = CutAsync();
            e.Handled = true;
            return;
        }
        if (EditorsHotkeys.Matches(keymap?.Paste, e) || (mod && !shift && key == Key.V))
        {
            _ = PasteAsync();
            e.Handled = true;
            return;
        }
        if (mod && key == Key.Z)
        {
            if (shift)
            {
                Redo();
            }
            else
            {
                Undo();
            }
            e.Handled = true;
            return;
        }
        if (mod && key == Key.Y)
        {
            Redo();
            e.Handled = true;
            return;
        }
        if (mod && !shift && key == Key.B)
        {
            _engine.ToggleMark(RichTextMark.Bold);
            e.Handled = true;
            return;
        }
        if (mod && !shift && key == Key.I)
        {
            _engine.ToggleMark(RichTextMark.Italic);
            e.Handled = true;
            return;
        }
        if (mod && shift && key == Key.X)
        {
            _engine.ToggleMark(RichTextMark.Strikethrough);
            e.Handled = true;
            return;
        }
        if (mod && key == Key.E)
        {
            _engine.ToggleMark(RichTextMark.Code);
            e.Handled = true;
            return;
        }
        if (mod && key == Key.K)
        {
            OpenLink();
            e.Handled = true;
            return;
        }
        if (mod && key == Key.U)
        {
            // Arc has no underline: the browser shortcut is swallowed (the engine ignores it outside the Docs profile).
            _engine.ToggleMark(RichTextMark.Underline);
            e.Handled = true;
            return;
        }
        if (mod && !shift && _engine.Docs && key is Key.OemPeriod or Key.OemComma)
        {
            _engine.ToggleMark(key == Key.OemPeriod ? RichTextMark.Superscript : RichTextMark.Subscript);
            e.Handled = true;
            return;
        }
        if (mod && shift && _engine.Docs && key is Key.L or Key.E or Key.R)
        {
            _engine.SetAlign(key == Key.L ? RichTextAlign.Start : key == Key.E ? RichTextAlign.Center : RichTextAlign.End);
            e.Handled = true;
            return;
        }
        if (alt && key == Key.F10)
        {
            if (_toolbar is { IsShown: true } toolbar && !toolbar.IsLinkFace)
            {
                toolbar.Bold.Focus(NavigationMethod.Tab);
                e.Handled = true;
            }
            return;
        }
        switch (key)
        {
            case Key.Tab:
                {
                    var block = _engine.Blocks[_engine.Selection.Anchor.Block];
                    if (block.IsCell && !mod && !alt)
                    {
                        _engine.TabInTable(shift);
                        e.Handled = true;
                    }
                    else if (block.IsList && !mod && !alt)
                    {
                        if (shift)
                        {
                            _engine.OutdentListItem();
                        }
                        else
                        {
                            _engine.IndentListItem();
                        }
                        e.Handled = true;
                    }
                    else if (block.IsCode && !shift && !mod && !alt)
                    {
                        _engine.InsertCodeIndent();
                        e.Handled = true;
                    }
                    return;
                }
            case Key.Enter when mod && _engine.Blocks[_engine.Selection.Focus.Block].IsToggle:
                ToggleFold();
                e.Handled = true;
                return;
            case Key.Enter:
                surface.GoalX = null;
                if (shift)
                {
                    _engine.InsertLineBreak();
                }
                else
                {
                    _engine.InsertParagraph();
                }
                e.Handled = true;
                return;
            case Key.Back when (e.KeyModifiers & KeyModifiers.Meta) != 0 && _engine.Selection.IsCollapsed:
                {
                    // macOS: Command+Backspace deletes to the start of the visual line.
                    var caret = _engine.Selection.Focus;
                    var (lineStart, _) = surface.LineBounds(caret);
                    if (lineStart < caret.Offset)
                    {
                        _engine.SetSelection(new RichTextEditorSelection(caret, caret with { Offset = lineStart }));
                    }
                    _engine.DeleteBackward();
                    e.Handled = true;
                    return;
                }
            case Key.Back:
                surface.GoalX = null;
                _engine.DeleteBackward(word: (e.KeyModifiers & EditorsHotkeys.WordModifiers) != 0 && EditorsHotkeys.WordModifiers != KeyModifiers.None);
                e.Handled = true;
                return;
            case Key.Delete:
                surface.GoalX = null;
                _engine.DeleteForward(word: (e.KeyModifiers & EditorsHotkeys.WordModifiers) != 0 && EditorsHotkeys.WordModifiers != KeyModifiers.None);
                e.Handled = true;
                return;
            case Key.Escape:
                if (_toolbar is { IsLinkFace: true })
                {
                    CloseLink(focusEditor: true);
                    e.Handled = true;
                }
                return;
        }
    }

    /// <summary>Caret movement and selection extension, by grapheme, word, visual line, line edge, page and document.</summary>
    private bool HandleNavigation(KeyEventArgs e, RichTextEditorSurface surface)
    {
        var keymap = EditorsHotkeys.Keymap;
        var shift = (e.KeyModifiers & KeyModifiers.Shift) != 0;
        var selection = _engine.Selection;
        var blocks = _engine.Blocks;
        RichTextEditorPosition? target = null;
        var keepGoal = false;

        if (EditorsHotkeys.Matches(keymap?.MoveCursorToTheStartOfDocument, e) || EditorsHotkeys.Matches(keymap?.MoveCursorToTheStartOfDocumentWithSelection, e)
            || (e.Key == Key.Home && (e.KeyModifiers & KeyModifiers.Control) != 0))
        {
            target = RichTextEditorEngine.NearestTextual(blocks, 0, forward: true);
        }
        else if (EditorsHotkeys.Matches(keymap?.MoveCursorToTheEndOfDocument, e) || EditorsHotkeys.Matches(keymap?.MoveCursorToTheEndOfDocumentWithSelection, e)
                 || (e.Key == Key.End && (e.KeyModifiers & KeyModifiers.Control) != 0))
        {
            target = RichTextEditorEngine.End(blocks);
        }
        else if (EditorsHotkeys.Matches(keymap?.MoveCursorToTheStartOfLine, e) || EditorsHotkeys.Matches(keymap?.MoveCursorToTheStartOfLineWithSelection, e) || e.Key == Key.Home)
        {
            var (start, _) = surface.LineBounds(selection.Focus);
            target = selection.Focus with { Offset = start };
        }
        else if (EditorsHotkeys.Matches(keymap?.MoveCursorToTheEndOfLine, e) || EditorsHotkeys.Matches(keymap?.MoveCursorToTheEndOfLineWithSelection, e) || e.Key == Key.End)
        {
            var (_, end) = surface.LineBounds(selection.Focus);
            target = selection.Focus with { Offset = end };
        }
        else
        {
            var word = EditorsHotkeys.WordModifiers != KeyModifiers.None && (e.KeyModifiers & EditorsHotkeys.WordModifiers) == EditorsHotkeys.WordModifiers;
            switch (e.Key)
            {
                case Key.Left:
                    if (!shift && !selection.IsCollapsed && !word)
                    {
                        target = selection.Start;
                    }
                    else
                    {
                        target = Step(blocks, _engine.HiddenMask, selection.Focus, -1, word);
                    }
                    break;
                case Key.Right:
                    if (!shift && !selection.IsCollapsed && !word)
                    {
                        target = selection.End;
                    }
                    else
                    {
                        target = Step(blocks, _engine.HiddenMask, selection.Focus, 1, word);
                    }
                    break;
                case Key.Up:
                case Key.Down:
                    {
                        if (IsSlashOpen)
                        {
                            return false;
                        }
                        var direction = e.Key == Key.Up ? -1 : 1;
                        var origin = !shift && !selection.IsCollapsed ? (direction < 0 ? selection.Start : selection.End) : selection.Focus;
                        var goal = surface.GoalX ?? surface.LineRect(origin).X;
                        surface.GoalX = goal;
                        keepGoal = true;
                        target = surface.MoveVertical(origin, goal, direction);
                        break;
                    }
                case Key.PageUp:
                case Key.PageDown:
                    {
                        var direction = e.Key == Key.PageUp ? -1 : 1;
                        var caret = surface.LineRect(selection.Focus);
                        var page = Math.Max(caret.Height, (ArcViewport.GetSize(this)?.Height ?? 600) * 0.85);
                        var goal = surface.GoalX ?? caret.X;
                        surface.GoalX = goal;
                        keepGoal = true;
                        target = surface.HitTest(new Point(goal, caret.Center.Y + direction * page));
                        break;
                    }
                default:
                    return false;
            }
        }
        if (target is not { } position)
        {
            return false;
        }
        if (!keepGoal)
        {
            surface.GoalX = null;
        }
        _engine.SetSelection(shift ? selection with { Focus = position } : RichTextEditorSelection.Caret(position));
        return true;
    }

    private static RichTextEditorPosition Step(ImmutableList<RichTextEditorBlock> blocks, bool[]? hidden, RichTextEditorPosition origin, int direction, bool word)
    {
        var block = blocks[origin.Block];
        if (direction < 0)
        {
            if (origin.Offset > 0)
            {
                return origin with { Offset = word ? RichTextEditorText.PreviousWord(block.Text, origin.Offset) : RichTextEditorText.PreviousCaret(block.Text, origin.Offset) };
            }
            for (var i = origin.Block - 1; i >= 0; i--)
            {
                if (blocks[i].IsTextual && hidden?[i] != true)
                {
                    return new RichTextEditorPosition(i, blocks[i].Length);
                }
            }
            return origin;
        }
        if (origin.Offset < block.Length)
        {
            return origin with { Offset = word ? RichTextEditorText.NextWord(block.Text, origin.Offset) : RichTextEditorText.NextCaret(block.Text, origin.Offset) };
        }
        for (var i = origin.Block + 1; i < blocks.Count; i++)
        {
            if (blocks[i].IsTextual && hidden?[i] != true)
            {
                return new RichTextEditorPosition(i, 0);
            }
        }
        return origin;
    }
}
