using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The reply of one run in <see cref="PromptRunPanel"/>: plain text shown as paragraphs (a blank line starts a new one,
/// a single line break stays inside its paragraph), 10.5px apart, 14 / 23.1 in Foreground. Text only grows at its end
/// while it streams; each added batch can fade in (150ms, standard curve) without moving what is already shown.
/// </summary>
/// <remarks>
/// After every change the whole text is split again (a reply is a few hundred words, and it changes once per batch):
/// paragraphs whose text did not change keep their views, the last one grows, new ones are added.
/// </remarks>
internal sealed class PromptRunReply : StackPanel
{
    /// <summary>The gap between two paragraphs (Arc 0.75em).</summary>
    internal const double ParagraphGap = 10.5;

    private readonly StringBuilder _text = new();
    private readonly List<string> _paragraphs = new();
    private readonly List<PromptReplyParagraph> _views = new();

    public PromptRunReply()
    {
        Spacing = ParagraphGap;
    }

    /// <summary>The text shown, as it was given (line breaks normalized to <c>\n</c>).</summary>
    public string Text => _text.ToString();

    /// <summary>Whether anything is shown.</summary>
    public bool HasText => _text.Length > 0;

    /// <summary>The paragraph views, in order.</summary>
    internal IReadOnlyList<PromptReplyParagraph> Paragraphs => _views;

    /// <summary>Adds a batch at the end; with <paramref name="fade"/> its characters fade in.</summary>
    public void Append(string batch, bool fade)
    {
        if (string.IsNullOrEmpty(batch))
        {
            return;
        }
        _text.Append(batch);
        Update(fade, replace: false);
    }

    /// <summary>Shows <paramref name="text"/> at once, replacing what was shown (no fade).</summary>
    public void SetText(string text)
    {
        _text.Clear().Append(Normalize(text));
        Update(fade: false, replace: true);
    }

    /// <summary>Line breaks as <c>\n</c> (a <c>\r\n</c> split across two batches leaves a lone <c>\r</c>, which is dropped).</summary>
    internal static string Normalize(string text) =>
        text.Contains('\r') ? text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", string.Empty, StringComparison.Ordinal) : text;

    /// <summary>
    /// Splits text into paragraphs: runs of lines that are not blank, joined by their line breaks; blank (empty or
    /// whitespace-only) lines separate them.
    /// </summary>
    internal static void Split(string text, List<string> paragraphs)
    {
        paragraphs.Clear();
        var start = -1;
        var end = -1;
        var lineStart = 0;
        for (var i = 0; i <= text.Length; i++)
        {
            if (i < text.Length && text[i] != '\n')
            {
                continue;
            }
            var blank = true;
            for (var j = lineStart; j < i; j++)
            {
                if (!char.IsWhiteSpace(text[j]))
                {
                    blank = false;
                    break;
                }
            }
            if (blank)
            {
                if (start >= 0)
                {
                    paragraphs.Add(text[start..end]);
                    start = -1;
                }
            }
            else
            {
                if (start < 0)
                {
                    start = lineStart;
                }
                end = i;
            }
            lineStart = i + 1;
        }
        if (start >= 0)
        {
            paragraphs.Add(text[start..end]);
        }
    }

    private void Update(bool fade, bool replace)
    {
        Split(_text.ToString(), _paragraphs);
        for (var i = 0; i < _paragraphs.Count; i++)
        {
            var text = _paragraphs[i];
            if (i < _views.Count)
            {
                var view = _views[i];
                var old = view.Text ?? string.Empty;
                if (string.Equals(old, text, StringComparison.Ordinal))
                {
                    continue;
                }
                view.Text = text;
                if (replace)
                {
                    view.Fades.Clear();
                }
                else if (fade && text.StartsWith(old, StringComparison.Ordinal))
                {
                    view.Fades.Add(old.Length, text.Length);
                }
                else
                {
                    view.Fades.Truncate(Math.Min(old.Length, text.Length));
                }
            }
            else
            {
                var view = new PromptReplyParagraph { Text = text };
                _views.Add(view);
                Children.Add(view);
                if (fade && !replace)
                {
                    view.Fades.Add(0, text.Length);
                }
            }
        }
        for (var i = _views.Count - 1; i >= _paragraphs.Count; i--)
        {
            Children.Remove(_views[i]);
            _views.RemoveAt(i);
        }
    }
}

/// <summary>
/// One paragraph of a reply: selectable wrapping text (pre-wrap line breaks) whose newest batches fade in through
/// <see cref="AiAnswerFades"/> (drawn clipped per batch at its opacity, nothing laid out again). Not a tab stop.
/// </summary>
internal sealed class PromptReplyParagraph : SelectableTextBlock
{
    private readonly AiAnswerFades _fades;

    static PromptReplyParagraph()
    {
        KeyboardNavigation.IsTabStopProperty.OverrideDefaultValue<PromptReplyParagraph>(false);
    }

    public PromptReplyParagraph()
    {
        _fades = new AiAnswerFades(this);
        TextWrapping = TextWrapping.Wrap;
    }

    protected override Type StyleKeyOverride => typeof(PromptReplyParagraph);

    /// <summary>The fading batches of this paragraph.</summary>
    internal AiAnswerFades Fades => _fades;

    protected override void RenderTextLayout(DrawingContext context, Point origin) => _fades.Draw(context, TextLayout, origin);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _fades.OnAttached();
    }
}
