using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// An answer of the side panel (spec 3.5): plain text, 14 / 21.7 in Foreground, wrapping anywhere; a blank line starts a
/// new paragraph one empty line further down (CSS <c>pre-wrap</c>), a single line break stays inside its paragraph. While
/// it streams the text only grows at its end: each step can fade in over 150ms (standard) without moving what is shown.
/// </summary>
/// <remarks>
/// A step changes the last paragraph only: paragraphs whose text did not change keep their views and their layout, the
/// last one is laid out again, new ones are added (spec 8.2: no whole-answer relayout per step).
/// </remarks>
internal sealed class AiPanelAnswerText : StackPanel
{
    private readonly StringBuilder _text = new();
    private readonly List<string> _paragraphs = [];
    private readonly List<AiPanelAnswerParagraph> _views = [];

    protected override Type StyleKeyOverride => typeof(AiPanelAnswerText);

    /// <summary>The text shown (line breaks as <c>\n</c>).</summary>
    public string Text => _text.ToString();

    /// <summary>Whether anything is shown.</summary>
    public bool HasText => _text.Length > 0;

    /// <summary>The paragraph views, in order.</summary>
    internal IReadOnlyList<AiPanelAnswerParagraph> Paragraphs => _views;

    /// <summary>Adds a step at the end; with <paramref name="fade"/> its characters fade in.</summary>
    public void Append(string step, bool fade)
    {
        if (string.IsNullOrEmpty(step))
        {
            return;
        }
        _text.Append(PromptRunReply.Normalize(step));
        Update(fade, replace: false);
    }

    /// <summary>Shows <paramref name="text"/> at once, replacing what was shown.</summary>
    public void SetText(string? text)
    {
        var normalized = PromptRunReply.Normalize(text ?? string.Empty);
        if (string.Equals(normalized, _text.ToString(), StringComparison.Ordinal))
        {
            foreach (var view in _views)
            {
                view.Fades.Clear();
            }
            return;
        }
        _text.Clear().Append(normalized);
        Update(fade: false, replace: true);
    }

    private void Update(bool fade, bool replace)
    {
        PromptRunReply.Split(_text.ToString(), _paragraphs);
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
                var view = new AiPanelAnswerParagraph { Text = text };
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
/// One paragraph of an answer: selectable wrapping text whose newest steps fade in through <see cref="AiAnswerFades"/>
/// (drawn clipped per step at its opacity, nothing laid out again). Not a tab stop.
/// </summary>
internal sealed class AiPanelAnswerParagraph : SelectableTextBlock
{
    private readonly AiAnswerFades _fades;

    static AiPanelAnswerParagraph()
    {
        KeyboardNavigation.IsTabStopProperty.OverrideDefaultValue<AiPanelAnswerParagraph>(false);
    }

    public AiPanelAnswerParagraph()
    {
        _fades = new AiAnswerFades(this);
        TextWrapping = TextWrapping.Wrap;
    }

    protected override Type StyleKeyOverride => typeof(AiPanelAnswerParagraph);

    /// <summary>The fading steps of this paragraph.</summary>
    internal AiAnswerFades Fades => _fades;

    protected override void RenderTextLayout(DrawingContext context, Point origin) => _fades.Draw(context, TextLayout, origin);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _fades.OnAttached();
    }
}
