// Ported from vscode (src/vs/editor/contrib/codelens/browser/codelensWidget.ts: CodeLensViewZone,
// CodeLensContentWidget, CodeLensHelper and CodeLensWidget, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - the lens row is the view zone's own control (upstream: an empty zone plus a content widget above the line); its
//    text starts at the x of the line's first non-whitespace character and follows horizontal scrolling;
//  - the commands are TextBlocks: those with a command id are links (hand cursor, link color and underline under the
//    pointer), a click runs the command through the editor or its host; codicons ($(name)) are left out of the titles;
//  - the decorations of the lens ranges are set directly on the model (upstream batches them through CodeLensHelper).

using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib.CodeLens;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc;

/// <summary>The code lenses of one line: a row above it (upstream's CodeLensWidget).</summary>
internal sealed partial class CodeLensWidget
{
    private static readonly ModelDecorationOptions s_codeLensDecorationOptions = new("codelens") { CollapseOnReplaceEdit = true };
    private static readonly Cursor s_hand = new(StandardCursorType.Hand);

    private readonly CodeEditor _editor;
    private readonly CodeEditorViewZone _viewZone;
    private readonly string _viewZoneId;
    private readonly StackPanel _row;
    private readonly Action<LanguageCommand> _runCommand;
    private IReadOnlyList<CodeLensItem> _data;
    private string[] _decorationIds = [];
    private double? _lastHeight;

    public CodeLensWidget(IReadOnlyList<CodeLensItem> data, CodeEditor editor, IViewZoneChangeAccessor viewZoneChangeAccessor, double heightInPx, Action updateCallback, Action<LanguageCommand> runCommand)
    {
        _editor = editor;
        _data = data;
        _runCommand = runCommand;

        // track all ranges with decorations; the row goes above the first line of them
        TextRange? range = null;
        var lenses = new List<Editor.Languages.CodeLens>();
        foreach (var item in data)
        {
            if (item.Symbol.Command is not null)
            {
                lenses.Add(item.Symbol);
            }

            range = range is { } r ? r.PlusRange(item.Symbol.Range) : item.Symbol.Range;
        }

        _decorationIds = editor.Model.DeltaDecorations([], [.. data.Select(item => new ModelDeltaDecoration(item.Symbol.Range, s_codeLensDecorationOptions))]);

        _row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            ClipToBounds = false,
        };
        var host = new Panel { Children = { _row }, Background = Brushes.Transparent, ClipToBounds = true };
        _viewZone = new CodeEditorViewZone
        {
            AfterLineNumber = range!.Value.StartLineNumber - 1,
            AfterColumn = int.MaxValue,
            HeightInPx = heightInPx,
            SuppressMouseDown = true,
            Control = host,
            OnComputedHeight = height =>
            {
                if (_lastHeight is { } last && last != height)
                {
                    updateCallback();
                }

                _lastHeight = height;
            },
            OnDomNodeTop = _ => UpdateLeft(),
        };
        _viewZoneId = viewZoneChangeAccessor.AddZone(_viewZone);
        if (lenses.Count > 0)
        {
            WithCommands(lenses);
        }
    }

    public bool IsDisposed { get; private set; }

    /// <summary>The view zone's id (for tests).</summary>
    internal string ViewZoneId => _viewZoneId;

    /// <summary>The titles shown, with " | " between them (for tests).</summary>
    internal string Text => string.Concat(_row.Children.OfType<TextBlock>().Select(t => t.Text));

    /// <summary>The TextBlocks of the commands that can be clicked (for tests).</summary>
    internal IEnumerable<TextBlock> Links => _row.Children.OfType<TextBlock>().Where(t => t.Tag is LanguageCommand);

    public void Dispose(IViewZoneChangeAccessor? viewZoneChangeAccessor)
    {
        if (_decorationIds.Length > 0 && !_editor.Model.IsDisposed())
        {
            _editor.Model.DeltaDecorations(_decorationIds, []);
        }

        _decorationIds = [];
        viewZoneChangeAccessor?.RemoveZone(_viewZoneId);
        IsDisposed = true;
    }

    /// <summary>Whether a lens range still exists the way it was (not collapsed by an edit).</summary>
    public bool IsValid()
    {
        for (var i = 0; i < _decorationIds.Length; i++)
        {
            var range = _editor.Model.GetDecorationRange(_decorationIds[i]);
            if (range is { } r && _data[i].Symbol.Range.IsEmpty == r.IsEmpty)
            {
                return true;
            }
        }

        return false;
    }

    public void UpdateCodeLensSymbols(IReadOnlyList<CodeLensItem> data)
    {
        _data = data;
        _decorationIds = _editor.Model.DeltaDecorations(_decorationIds, [.. data.Select(item => new ModelDeltaDecoration(item.Symbol.Range, s_codeLensDecorationOptions))]);
    }

    public void UpdateHeight(double height, IViewZoneChangeAccessor viewZoneChangeAccessor)
    {
        _viewZone.HeightInPx = height;
        viewZoneChangeAccessor.LayoutZone(_viewZoneId);
    }

    /// <summary>Moves the row above the lenses' line after an edit (upstream's update).</summary>
    public void Update(IViewZoneChangeAccessor viewZoneChangeAccessor)
    {
        if (IsValid())
        {
            var range = _editor.Model.GetDecorationRange(_decorationIds[0]);
            if (range is { } r)
            {
                _viewZone.AfterLineNumber = r.StartLineNumber - 1;
                viewZoneChangeAccessor.LayoutZone(_viewZoneId);
            }
        }
    }

    /// <summary>The lenses to resolve when the row is visible; their ranges updated from the decorations (upstream's computeIfNecessary).</summary>
    public IReadOnlyList<CodeLensItem>? ComputeIfNecessary(TextModel model)
    {
        if (!IsVisible())
        {
            return null;
        }

        var data = new List<CodeLensItem>(_data.Count);
        for (var i = 0; i < _data.Count; i++)
        {
            var item = _data[i];
            if (i < _decorationIds.Length && model.GetDecorationRange(_decorationIds[i]) is { } range)
            {
                item = item with { Symbol = item.Symbol with { Range = range } };
            }

            data.Add(item);
        }

        _data = data;
        return _data;
    }

    /// <summary>Shows the resolved lenses (upstream's updateCommands).</summary>
    public void UpdateCommands(IReadOnlyList<Editor.Languages.CodeLens?> symbols)
    {
        WithCommands(symbols);
        var data = new List<CodeLensItem>(_data.Count);
        for (var i = 0; i < _data.Count; i++)
        {
            var item = _data[i];
            if (i < symbols.Count && symbols[i] is { } resolved)
            {
                item = item with { Symbol = item.Symbol with { Command = resolved.Command ?? item.Symbol.Command } };
            }

            data.Add(item);
        }

        _data = data;
    }

    public int GetLineNumber()
    {
        if (_decorationIds.Length > 0 && _editor.Model.GetDecorationRange(_decorationIds[0]) is { } range)
        {
            return range.StartLineNumber;
        }

        return -1;
    }

    // upstream's CodeLensContentWidget.withCommands
    private void WithCommands(IReadOnlyList<Editor.Languages.CodeLens?> lenses)
    {
        _row.Children.Clear();
        var hasSymbol = false;
        for (var i = 0; i < lenses.Count; i++)
        {
            if (lenses[i] is not { } lens)
            {
                continue;
            }

            hasSymbol = true;
            if (lens.Command is { } command)
            {
                var title = Codicon().Replace(command.Title.Trim(), string.Empty).Trim();
                var text = CreateText(title);
                if (command.Tooltip is { } tooltip)
                {
                    ToolTip.SetTip(text, tooltip);
                }

                if (command.Id.Length > 0)
                {
                    MakeLink(text, command);
                }

                _row.Children.Add(text);
                if (i + 1 < lenses.Count)
                {
                    _row.Children.Add(CreateText(" | "));
                }
            }
        }

        if (!hasSymbol)
        {
            // symbols but no commands
            _row.Children.Add(CreateText("no commands"));
        }

        UpdateLeft();
    }

    private TextBlock CreateText(string text)
    {
        var block = new TextBlock
        {
            Text = text,
            FontFamily = _editor.FontFamily,
            FontSize = Math.Floor(_editor.FontSize * 0.9),
            VerticalAlignment = VerticalAlignment.Center,
        };
        BindForeground(block, CodeEditorView.InlayHintForegroundBrushProperty);
        return block;
    }

    private void MakeLink(TextBlock text, LanguageCommand command)
    {
        text.Tag = command;
        text.Cursor = s_hand;
        text.Background = Brushes.Transparent;
        text.PointerEntered += (_, _) =>
        {
            BindForeground(text, CodeEditorView.LinkBrushProperty);
            text.TextDecorations = TextDecorations.Underline;
        };
        text.PointerExited += (_, _) =>
        {
            BindForeground(text, CodeEditorView.InlayHintForegroundBrushProperty);
            text.TextDecorations = null;
        };
        text.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            _runCommand(command);
        };
    }

    private void BindForeground(TextBlock block, StyledProperty<IBrush?> property)
    {
        if (_editor.View is { } view)
        {
            block.Bind(TextBlock.ForegroundProperty, view.GetObservable(property));
        }
    }

    // the row starts under the line's first character (upstream's content widget at getLineFirstNonWhitespaceColumn)
    private void UpdateLeft()
    {
        if (_editor.View is not { } view || view.ViewModel is not { } viewModel || IsDisposed)
        {
            return;
        }

        var line = GetLineNumber();
        if (line < 1 || line > _editor.Model.GetLineCount())
        {
            return;
        }

        var column = Math.Max(1, _editor.Model.GetLineFirstNonWhitespaceColumn(line));
        var viewPosition = viewModel.CoordinatesConverter.ConvertModelPositionToViewPosition(new TextPosition(line, column));
        _row.Margin = new Thickness(Math.Max(0, view.GetX(viewPosition.LineNumber, viewPosition.Column) - view.GutterWidth), 0, 0, 0);
    }

    private bool IsVisible()
    {
        if (_lastHeight == 0 || _editor.GetViewZoneBounds(_viewZoneId) is not { } bounds)
        {
            return false;
        }

        var height = _editor.GetLayoutSize().Height;
        return bounds.Top + bounds.Height >= 0 && bounds.Top <= height;
    }

    [GeneratedRegex(@"\$\([a-z0-9\-]+?(?:~[a-z0-9\-]*?)?\)", RegexOptions.IgnoreCase)]
    private static partial Regex Codicon();
}
