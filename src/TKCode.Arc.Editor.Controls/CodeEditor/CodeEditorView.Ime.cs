using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using TKCode.Arc.Editor;

namespace TKCode.Arc;

// The input method (upstream's TextAreaHandler and its composition helpers): the platform pushes the composition text
// (preedit) through CodeEditorTextInputClient and later commits the result as a TextInput event, which CodeEditor types
// with viewModel.type like any other text.
//
// Upstream writes every composition step into the model (compositionType with replacePrevCharCnt) and lets compositionEnd
// sort out auto-closing. Here the preedit stays out of the document and is drawn over the text at each cursor, with an
// underline, by a layer of its own (CodeEditorPreeditLayer): the model, the undo stack and the other cursors never see
// half-composed text, a cancelled composition leaves nothing to undo, and the platforms that send the commit before or
// after clearing the preedit behave the same. The view model still hears compositionStart / compositionEnd as view
// events (ViewCompositionStartEvent / ViewCompositionEndEvent), so view parts can react the way upstream's do.
internal sealed partial class CodeEditorView
{
    private CodeEditorTextInputClient? _ime;
    private CodeEditorPreeditLayer? _preeditLayer;
    private string? _preeditText;
    private int? _preeditCursor;

    /// <summary>The input method client the editor hands the platform (TextInputMethodClientRequested).</summary>
    internal CodeEditorTextInputClient InputMethodClient => _ime ??= new CodeEditorTextInputClient(this);

    /// <summary>The composition text the input method shows, drawn at the cursors; null when nothing is composed.</summary>
    internal string? PreeditText => _preeditText;

    /// <summary>The caret inside <see cref="PreeditText"/>, in UTF-16 code units (its end when the platform gives none).</summary>
    internal int PreeditCursor => _preeditCursor ?? _preeditText?.Length ?? 0;

    /// <summary>Whether the input method is composing (keys belong to it until it commits or cancels).</summary>
    internal bool IsComposing => _preeditText is not null;

    /// <summary>
    /// Where the candidate window goes: the caret inside the composition at the primary cursor, relative to the view.
    /// </summary>
    internal Rect ImeCursorRectangle
    {
        get
        {
            if (_viewModel is null || _editor is null)
            {
                return default;
            }

            var position = _viewModel.GetPrimaryCursorState().ViewState.Position;
            var x = GetX(position.LineNumber, position.Column);
            if (_preeditText is { } preedit && PreeditCursor > 0)
            {
                x += MeasurePreedit(preedit[..PreeditCursor]);
            }

            return new Rect(x, GetY(position.LineNumber), Math.Max(1, CursorWidth), _lineHeight);
        }
    }

    /// <summary>Shows (or, with null or empty text, ends) the composition text.</summary>
    internal void SetPreedit(string? text, int? cursor)
    {
        var value = string.IsNullOrEmpty(text) ? null : text;
        int? caret = value is null || cursor is not { } c ? null : Math.Clamp(c, 0, value.Length);
        if (value == _preeditText && caret == _preeditCursor)
        {
            return;
        }

        var wasComposing = _preeditText is not null;
        _preeditText = value;
        _preeditCursor = caret;
        if (!wasComposing && value is not null)
        {
            _viewModel?.OnCompositionStart();
        }
        else if (wasComposing && value is null)
        {
            _viewModel?.OnCompositionEnd();
        }

        _preeditLayer?.InvalidateVisual();
        // the cursors hide under the preedit
        InvalidateCursors();
        _ime?.NotifyCursorRectangleChanged();
    }

    /// <summary>Drops the composition (a click moved the cursor, or the editor lost the focus).</summary>
    internal void CancelComposition()
    {
        if (!IsComposing)
        {
            return;
        }

        _ime?.Reset();
        SetPreedit(null, null);
    }

    /// <summary>The composition is committed: its text arrives as typed text right after this.</summary>
    internal void CommitComposition() => SetPreedit(null, null);

    private void NotifyImeCursorMoved()
    {
        _ime?.NotifyCursorRectangleChanged();
        if (IsComposing)
        {
            _preeditLayer?.InvalidateVisual();
        }
    }

    private void EnsurePreeditLayer()
    {
        if (_preeditLayer is not null)
        {
            return;
        }

        _preeditLayer = new CodeEditorPreeditLayer(this);
        VisualChildren.Add(_preeditLayer);
        InvalidateArrange();
    }

    private double MeasurePreedit(string text)
    {
        using var layout = new TextLayout(text, _typeface, _fontSize, null);
        return layout.WidthIncludingTrailingWhitespace;
    }

    /// <summary>Draws the composition text at every cursor: over the text there, underlined, with its own caret.</summary>
    internal void RenderPreedit(DrawingContext context)
    {
        if (_preeditText is not { } preedit || _viewModel is null || _editor is null)
        {
            return;
        }

        var gutterWidth = GutterWidth;
        var foreground = TextElement.GetForeground(this) ?? Brushes.Black;
        var background = _editor.Background;
        var primary = _viewModel.GetPrimaryCursorState().ViewState;
        using var layout = new TextLayout(preedit, _typeface, _fontSize, foreground, lineHeight: _lineHeight);
        var width = layout.WidthIncludingTrailingWhitespace;
        var caretX = PreeditCursor > 0 ? MeasurePreedit(preedit[..PreeditCursor]) : 0;
        using (context.PushClip(new Rect(gutterWidth, 0, Math.Max(0, TextAreaRight - gutterWidth), Bounds.Height)))
        {
            foreach (var state in _viewModel.GetCursorStates())
            {
                var position = state.ViewState.Position;
                var x = GetX(position.LineNumber, position.Column);
                var y = GetY(position.LineNumber);
                var box = new Rect(x, y, width + CursorWidth, _lineHeight);
                if (background is not null)
                {
                    context.FillRectangle(background, box);
                }

                if (CurrentLineBrush is { } currentLine && primary.Selection.IsEmpty && position.LineNumber == primary.Position.LineNumber)
                {
                    context.FillRectangle(currentLine, box);
                }

                layout.Draw(context, new Point(x, y + ((_lineHeight - layout.Height) / 2)));
                var thickness = Math.Max(1, Math.Round(_fontSize / 14));
                context.FillRectangle(foreground, new Rect(x, y + _lineHeight - thickness - 1, width, thickness));
                if (CursorBrush is { } cursor)
                {
                    context.FillRectangle(cursor, new Rect(Math.Round(x + caretX), y, CursorWidth, _lineHeight));
                }
            }
        }
    }
}
