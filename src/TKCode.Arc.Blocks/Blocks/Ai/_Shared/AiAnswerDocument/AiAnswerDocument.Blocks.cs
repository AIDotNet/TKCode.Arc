using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>Blocks of <see cref="AiAnswerDocument"/>: turning the parsed Markdown into views and keeping them in step.</summary>
internal partial class AiAnswerDocument
{
    /// <summary>Space above a heading beyond the body's 16 (Arc: margin 16 0 −6, so 32 above and 10 below).</summary>
    private static readonly Thickness HeadingMargin = new(0, 16, 0, -6);

    /// <summary>Space around a table or a code block beyond the body's 16 (Arc: margin 8 0).</summary>
    private static readonly Thickness FigureMargin = new(0, 8);

    private List<AiAnswerBlock> _blocks = [];
    private readonly List<BlockView> _views = [];

    /// <summary>The blocks shown (tests).</summary>
    internal IReadOnlyList<AiAnswerBlock> Blocks => _blocks;

    /// <summary>The view of each block, in order (tests).</summary>
    internal IEnumerable<Control> BlockViews => _views.Select(v => v.Control);

    /// <summary>
    /// Parses the text so far and brings the views in line: blocks that did not change keep their views untouched, a
    /// block whose kind stayed is updated in place (its new characters fade in with <paramref name="fade"/>), the rest
    /// is built anew.
    /// </summary>
    private void Rebuild(bool fade)
    {
        var blocks = AiAnswerMarkdown.Parse(_source.ToString(), _final);
        if (_body is null)
        {
            _blocks = blocks;
            return;
        }
        for (var i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            if (i < _views.Count)
            {
                if (i < _blocks.Count && _blocks[i].SameAs(block))
                {
                    continue;
                }
                var view = _views[i];
                if (view.Kind == block.Kind && view.Ordered == block.Ordered)
                {
                    UpdateView(view, block, fade);
                    continue;
                }
                RemoveView(i);
            }
            InsertView(i, block, fade);
        }
        while (_views.Count > blocks.Count)
        {
            RemoveView(_views.Count - 1);
        }
        _blocks = blocks;
        ApplySpacing();
        UpdateCursor();
        UpdateLighting();
    }

    private void InsertView(int index, AiAnswerBlock block, bool fade)
    {
        Control control = block.Kind switch
        {
            AiAnswerBlockKind.Heading => new AiAnswerText(this, AiAnswerTextRole.Heading),
            AiAnswerBlockKind.List => new AiAnswerList(this, block.Ordered),
            AiAnswerBlockKind.Table => new AiAnswerTable(this),
            AiAnswerBlockKind.Code => new AiAnswerCodeBlock(this),
            _ => new AiAnswerText(this, AiAnswerTextRole.Paragraph)
        };
        var view = new BlockView(block.Kind, control, block.Ordered);
        _views.Insert(index, view);
        // In the tree first: a fade started off the tree would jump to its end.
        _body!.Children.Insert(index, control);
        UpdateView(view, block, fade);
    }

    private void UpdateView(BlockView view, AiAnswerBlock block, bool fade)
    {
        switch (view.Control)
        {
            case AiAnswerText text:
                text.SetContent(block.Texts.Count > 0 ? block.Texts[0] : [], fade);
                break;
            case AiAnswerList list:
                list.Update(block, fade);
                break;
            case AiAnswerTable table:
                table.Update(block, fade);
                break;
            case AiAnswerCodeBlock code:
                code.Update(block, fade);
                break;
        }
    }

    private void RemoveView(int index)
    {
        var view = _views[index];
        _views.RemoveAt(index);
        _body?.Children.Remove(view.Control);
        foreach (var text in TextsOf(view.Control))
        {
            foreach (var pill in text.Pills)
            {
                ForgetCitation(pill);
            }
        }
        if (_cursorText is not null && TextsOf(view.Control).Contains(_cursorText))
        {
            _cursorText = null;
        }
    }

    private void ClearBlocks()
    {
        CloseCard();
        _hoverPill = null;
        _focusPill = null;
        UpdateActive();
        if (_cursorText is not null)
        {
            _cursorText.ShowsCursor = false;
            _cursorText = null;
        }
        foreach (var view in _views)
        {
            _body?.Children.Remove(view.Control);
        }
        _views.Clear();
        _blocks = [];
        UpdateCursor();
    }

    /// <summary>Headings and figures add Arc's margins to the body's 16 between blocks; the first block starts at the top.</summary>
    private void ApplySpacing()
    {
        for (var i = 0; i < _views.Count; i++)
        {
            var margin = _views[i].Kind switch
            {
                AiAnswerBlockKind.Heading => HeadingMargin,
                AiAnswerBlockKind.Table or AiAnswerBlockKind.Code => FigureMargin,
                _ => default
            };
            if (i == 0)
            {
                margin = new Thickness(margin.Left, 0, margin.Right, margin.Bottom);
            }
            _views[i].Control.Margin = margin;
        }
    }

    /// <summary>Every text of the answer in reading order (paragraphs, headings, list items, table cells).</summary>
    internal IEnumerable<AiAnswerText> AllTexts()
    {
        foreach (var view in _views)
        {
            foreach (var text in TextsOf(view.Control))
            {
                yield return text;
            }
        }
    }

    private static IEnumerable<AiAnswerText> TextsOf(Control control) => control switch
    {
        AiAnswerText text => [text],
        AiAnswerList list => list.Texts,
        AiAnswerTable table => table.Cells,
        _ => []
    };

    /// <summary>Every citation pill of the answer in reading order.</summary>
    internal IEnumerable<AiAnswerCitation> AllCitations() => AllTexts().SelectMany(t => t.Pills);

    /// <summary>The last text of the answer: where the stream cursor goes (none after a code block).</summary>
    private AiAnswerText? LastText() => _views.Count == 0 ? null : TextsOf(_views[^1].Control).LastOrDefault();

    private void RefreshLabels()
    {
        foreach (var view in _views)
        {
            if (view.Control is AiAnswerTable table)
            {
                table.RefreshLabels();
            }
            else if (view.Control is AiAnswerCodeBlock code)
            {
                code.RefreshLabels();
            }
        }
    }

    /// <summary>The plain text of <paramref name="blocks"/> (see <see cref="PlainText"/>).</summary>
    internal static string BuildPlainText(IReadOnlyList<AiAnswerBlock> blocks)
    {
        var builder = new StringBuilder();
        foreach (var block in blocks)
        {
            if (builder.Length > 0)
            {
                builder.Append("\n\n");
            }
            switch (block.Kind)
            {
                case AiAnswerBlockKind.List:
                    for (var i = 0; i < block.Texts.Count; i++)
                    {
                        if (i > 0)
                        {
                            builder.Append('\n');
                        }
                        builder.Append(block.Ordered ? (block.ListStart + i).ToString(CultureInfo.InvariantCulture) + ". " : "- ");
                        builder.Append(AiAnswerMarkdown.ToPlainText(block.Texts[i]));
                    }
                    break;
                case AiAnswerBlockKind.Table:
                    var cell = 0;
                    for (var r = 0; r < block.Rows.Count; r++)
                    {
                        if (r > 0)
                        {
                            builder.Append('\n');
                        }
                        for (var c = 0; c < block.Rows[r]; c++)
                        {
                            if (c > 0)
                            {
                                builder.Append(" | ");
                            }
                            builder.Append(AiAnswerMarkdown.ToPlainText(block.Texts[cell++]));
                        }
                    }
                    break;
                case AiAnswerBlockKind.Code:
                    builder.Append(block.Code);
                    break;
                default:
                    builder.Append(block.Texts.Count > 0 ? AiAnswerMarkdown.ToPlainText(block.Texts[0]) : string.Empty);
                    break;
            }
        }
        return builder.ToString();
    }

    /// <summary>The view of a block: its kind (and for a list whether it is numbered), so an update can tell whether it fits.</summary>
    private sealed record BlockView(AiAnswerBlockKind Kind, Control Control, bool Ordered);
}
