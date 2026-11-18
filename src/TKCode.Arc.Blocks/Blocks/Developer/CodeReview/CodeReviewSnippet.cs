using System.Collections.Immutable;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using TKCode.Arc.CodeTokens;
using TKCode.Arc.DesignTokens;
using Avalonia.Media.Immutable;
using TKCode.Arc.Primitives;
using Avalonia.Automation.Provider;

namespace TKCode.Arc;

/// <summary>
/// The "Suggested change" card of a thread: a heading over the replaced lines (−, removed tint) and the new ones
/// (+, added tint), syntax tinted like the diff. 1px border, radius 14, contents clipped.
/// </summary>
internal sealed class CodeReviewSnippet : Border
{
    private readonly CodeReviewHighlighter _highlighter;
    private readonly List<CodeReviewSnippetLine> _lines = new();
    private readonly string _language;

    public CodeReviewSnippet(CodeReviewPalette palette, string heading, IReadOnlyList<string> oldLines,
        IReadOnlyList<string> newLines, string language)
    {
        _language = language;
        ClipToBounds = true;
        this.BindCornerRadius(ArcDesignTokenKind.RadiusStep14);
        ArcTokens.Bind(this, BorderBrushProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(this, BorderThicknessProperty, ArcDesignTokenKind.EdgeThickness);

        var head = new Border
        {
            Padding = new Thickness(12, 7),
            Child = CodeReviewUi.Text(heading, 12, ArcDesignTokenKind.TextSecondary, FontWeight.Medium, 16.8)
        };
        ArcTokens.Bind(head, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        ArcTokens.Bind(head, Border.BorderThicknessProperty, ArcDesignTokenKind.EdgeThicknessBottom);

        var code = new StackPanel { Margin = new Thickness(0, 4) };
        foreach (var text in oldLines)
        {
            var line = new CodeReviewSnippetLine(palette, text, false);
            _lines.Add(line);
            code.Children.Add(line);
        }
        foreach (var text in newLines)
        {
            var line = new CodeReviewSnippetLine(palette, text, true);
            _lines.Add(line);
            code.Children.Add(line);
        }
        var stack = new StackPanel();
        stack.Children.Add(head);
        stack.Children.Add(code);
        Child = stack;

        _highlighter = new CodeReviewHighlighter(ApplyTokens);
    }

    /// <summary>Completes when the code has its colors.</summary>
    public Task WhenHighlighted => _highlighter.WhenHighlighted;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ActualThemeVariantChanged += OnTheme;
        Highlight();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ActualThemeVariantChanged -= OnTheme;
    }

    private void OnTheme(object? sender, EventArgs e) => Highlight();

    private void Highlight() =>
        _highlighter.Set(_lines.Select(l => l.Text).ToArray(), _language, CodeReviewUi.IsDark(this));

    private void ApplyTokens()
    {
        for (var i = 0; i < _lines.Count; i++)
        {
            _lines[i].SetTokens(_highlighter.TokensFor(i, _lines[i].Text));
        }
    }
}

/// <summary>One drawn line of a suggestion card: tint, a centered sign in a 21.7 column, then the code.</summary>
internal sealed class CodeReviewSnippetLine : Control
{
    private readonly CodeReviewPalette _palette;
    private readonly bool _added;
    private readonly List<(TextLayout Layout, double X)> _runs = new();
    private ImmutableArray<HighlightToken>? _tokens;
    private TextLayout? _sign;
    private int _built = -1;

    public CodeReviewSnippetLine(CodeReviewPalette palette, string text, bool added)
    {
        _palette = palette;
        _added = added;
        Text = text;
        HorizontalAlignment = HorizontalAlignment.Stretch;
    }

    public string Text { get; }

    public void SetTokens(ImmutableArray<HighlightToken>? tokens)
    {
        _tokens = tokens;
        _built = -1;
        InvalidateVisual();
    }

    // Arc's suggestion lines end with 12 of padding (the code font size).
    protected override Size MeasureOverride(Size availableSize) =>
        new(_palette.SignWidth + Text.Length * _palette.CharWidth + _palette.CodeFontSize, _palette.LineHeight);

    private void Build()
    {
        if (_built == _palette.Version)
        {
            return;
        }
        _built = _palette.Version;
        foreach (var run in _runs)
        {
            run.Layout.Dispose();
        }
        _runs.Clear();
        _sign?.Dispose();
        _sign = new TextLayout(_added ? "+" : "\u2212", _palette.MonoFace, _palette.CodeFontSize, _palette.Brush(_added ? _palette.Success : _palette.Danger));
        var plain = _palette.Brush(_palette.Foreground);
        var position = 0;
        if (_tokens is { IsDefaultOrEmpty: false } tokens)
        {
            foreach (var token in tokens)
            {
                var start = Math.Clamp(token.Start, 0, Text.Length);
                var end = Math.Clamp(token.Start + token.Length, start, Text.Length);
                if (start > position)
                {
                    Add(Text[position..start], position, _palette.MonoFace, plain);
                }
                if (end > start)
                {
                    var face = (token.Style & HighlightFontStyle.Italic) != 0 ? _palette.MonoItalic : _palette.MonoFace;
                    Add(Text[start..end], start, face, _palette.Brush(Color.FromUInt32(token.Foreground)));
                }
                position = Math.Max(position, end);
            }
        }
        if (position < Text.Length)
        {
            Add(Text[position..], position, _palette.MonoFace, plain);
        }
    }

    private void Add(string text, int start, Typeface face, IBrush brush)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            _runs.Add((new TextLayout(text, face, _palette.CodeFontSize, brush), start * _palette.CharWidth));
        }
    }

    public override void Render(DrawingContext context)
    {
        Build();
        context.FillRectangle(_palette.Brush(_added ? _palette.AddedLine : _palette.DeletedLine), new Rect(Bounds.Size));
        if (_sign is not null)
        {
            _sign.Draw(context, new Point((_palette.SignWidth - _sign.WidthIncludingTrailingWhitespace) / 2, (_palette.LineHeight - _sign.Height) / 2));
        }
        foreach (var (layout, x) in _runs)
        {
            layout.Draw(context, new Point(_palette.SignWidth + x, (_palette.LineHeight - layout.Height) / 2));
        }
    }
}
