// Ported from Arc's free login-centered block (MIT, Copyright (c) 2026 Elia Kuratli): `.heading p { text-wrap: pretty }` in login-centered.module.css.
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

/// <summary>
/// A step's sentence laid out word by word, the way the browser lays out Arc's <c>text-wrap: pretty</c> paragraphs:
/// words fill each line, and a last line holding a single word takes the last word of the line above when both fit, so
/// "No / password needed." rather than "No password / needed.". Runs can be secondary (the sentence) or strong (an
/// address, in the foreground color), and an inline control (the "Change" button) can follow the text and wraps with it.
/// </summary>
/// <remarks>
/// Avalonia lays a paragraph out at one width for every line, which cannot express "this line shorter, the next one
/// longer"; placing the words ourselves can. The words are hidden from screen readers; the panel's name is the sentence.
/// </remarks>
internal sealed class LoginCenteredFlow : Panel
{
    private readonly List<Control> _words = new();
    private readonly List<(int Start, int End, double Top, double Height)> _lines = new();
    private double _spaceWidth = -1;
    private string? _key;
    private Control? _trailing;
    private double _spaceFontSize;

    public LoginCenteredFlow()
    {
        ArcTokens.Bind(this, TextElement.FontSizeProperty, ArcDesignTokenKind.TypeSM);
        ArcTokens.Bind(this, TextElement.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
    }

    /// <summary>The height of a line (Arc's 14px × 1.4).</summary>
    public double LineHeight { get; set; } = 19.6;

    /// <summary>Shows <paramref name="runs"/> followed by <paramref name="trailing"/> (a space apart), replacing the content.</summary>
    public void SetContent(IReadOnlyList<(string Text, bool IsStrong)> runs, Control? trailing = null)
    {
        var key = string.Join("\u0001", runs.Select(r => (r.IsStrong ? "*" : "-") + r.Text));
        if (key == _key && ReferenceEquals(trailing, _trailing))
        {
            return;
        }
        _key = key;
        _trailing = trailing;
        Children.Clear();
        _words.Clear();
        var sentence = new System.Text.StringBuilder();
        StackPanel? group = null;
        Control? pending = null;
        foreach (var (text, strong) in runs)
        {
            sentence.Append(text);
            var index = 0;
            while (index < text.Length)
            {
                if (text[index] == ' ')
                {
                    Flush(ref pending, ref group);
                    index++;
                    continue;
                }
                var end = text.IndexOf(' ', index);
                if (end < 0)
                {
                    end = text.Length;
                }
                var piece = Piece(text.Substring(index, end - index), strong);
                if (pending is null)
                {
                    pending = piece;
                }
                else
                {
                    // A word that continues in the next run ("maya@northwind.studio" then ".") stays one word.
                    if (group is null)
                    {
                        group = new StackPanel { Orientation = Orientation.Horizontal };
                        group.Children.Add(pending);
                        pending = group;
                    }
                    group.Children.Add(piece);
                }
                index = end;
            }
        }
        Flush(ref pending, ref group);
        if (trailing is not null)
        {
            trailing.VerticalAlignment = VerticalAlignment.Top;
            _words.Add(trailing);
            Children.Add(trailing);
        }
        AutomationProperties.SetName(this, sentence.ToString().Trim());
        InvalidateMeasure();

        void Flush(ref Control? word, ref StackPanel? panel)
        {
            if (word is not null)
            {
                _words.Add(word);
                Children.Add(word);
            }
            word = null;
            panel = null;
        }
    }

    /// <summary>The words on each line, joined by spaces (tests read it).</summary>
    internal IReadOnlyList<string> LineTexts => _lines.Select(l => string.Join(" ", _words.Skip(l.Start).Take(l.End - l.Start).Select(TextOf))).ToList();

    private static string TextOf(Control word) => word switch
    {
        TextBlock text => text.Text ?? string.Empty,
        Panel panel => string.Concat(panel.Children.Select(TextOf)),
        LoginCenteredInlineButton button => button.Text ?? string.Empty,
        _ => string.Empty
    };

    private TextBlock Piece(string text, bool strong)
    {
        var block = new TextBlock { Text = text, LineHeight = LineHeight, TextWrapping = TextWrapping.Wrap };
        AutomationProperties.SetAccessibilityView(block, AccessibilityView.Raw);
        if (strong)
        {
            ArcTokens.Bind(block, TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
        }
        return block;
    }

    private double SpaceWidth()
    {
        var size = GetValue(TextElement.FontSizeProperty);
        if (_spaceWidth >= 0 && Math.Abs(size - _spaceFontSize) < 0.01)
        {
            return _spaceWidth;
        }
        TextBlock Probe(string text) => new()
        {
            Text = text,
            FontSize = size,
            FontFamily = GetValue(TextElement.FontFamilyProperty),
            LetterSpacing = GetValue(TextElement.LetterSpacingProperty)
        };
        var spaced = Probe("a a");
        var tight = Probe("aa");
        spaced.Measure(Size.Infinity);
        tight.Measure(Size.Infinity);
        _spaceFontSize = size;
        _spaceWidth = Math.Max(0, spaced.DesiredSize.Width - tight.DesiredSize.Width);
        return _spaceWidth;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = availableSize.Width;
        foreach (var word in _words)
        {
            word.Measure(Size.Infinity);
            if (word.DesiredSize.Width > width)
            {
                // Arc's overflow-wrap: anywhere: a word longer than the line breaks inside itself.
                word.Measure(new Size(width, double.PositiveInfinity));
            }
        }
        var space = SpaceWidth();
        var breaks = Greedy(width, space);
        Prettify(breaks, width, space);
        _lines.Clear();
        double top = 0, widest = 0;
        for (var i = 0; i < breaks.Count; i++)
        {
            var start = breaks[i];
            var end = i + 1 < breaks.Count ? breaks[i + 1] : _words.Count;
            var height = LineHeight;
            for (var w = start; w < end; w++)
            {
                height = Math.Max(height, _words[w].DesiredSize.Height);
            }
            _lines.Add((start, end, top, height));
            widest = Math.Max(widest, LineWidth(start, end, space));
            top += height;
        }
        return new Size(Math.Min(widest, width), top);
    }

    private List<int> Greedy(double width, double space)
    {
        var breaks = new List<int>();
        double x = 0;
        for (var i = 0; i < _words.Count; i++)
        {
            var w = _words[i].DesiredSize.Width;
            if (breaks.Count == 0 || (x > 0 && x + space + w > width + 0.01))
            {
                breaks.Add(i);
                x = w;
            }
            else
            {
                x += (x > 0 ? space : 0) + w;
            }
        }
        return breaks;
    }

    /// <summary>A lone last word takes the last word of the line above when both fit (and that line keeps one).</summary>
    private void Prettify(List<int> breaks, double width, double space)
    {
        if (breaks.Count < 2)
        {
            return;
        }
        var lastStart = breaks[^1];
        var previousStart = breaks[^2];
        if (_words.Count - lastStart != 1 || lastStart - previousStart < 2)
        {
            return;
        }
        if (LineWidth(lastStart - 1, _words.Count, space) <= width + 0.01)
        {
            breaks[^1] = lastStart - 1;
        }
    }

    private double LineWidth(int start, int end, double space)
    {
        double x = 0;
        for (var i = start; i < end; i++)
        {
            x += (i > start ? space : 0) + _words[i].DesiredSize.Width;
        }
        return x;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var space = SpaceWidth();
        foreach (var (start, end, top, height) in _lines)
        {
            double x = 0;
            for (var i = start; i < end; i++)
            {
                var word = _words[i];
                if (i > start)
                {
                    x += space;
                }
                var size = word.DesiredSize;
                word.Arrange(new Rect(x, top, Math.Min(size.Width, finalSize.Width), size.Height));
                x += size.Width;
            }
        }
        return finalSize;
    }
}
