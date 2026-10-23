using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>What a text of the answer is; the theme styles each with a class of the same name.</summary>
internal enum AiAnswerTextRole
{
    /// <summary>Running text: 16 / 25.6.</summary>
    Paragraph,

    /// <summary>A heading: the display font, 18 / 19.8 at 500.</summary>
    Heading,

    /// <summary>A list item: as a paragraph.</summary>
    ListItem,

    /// <summary>A table header cell: 14 / 20.3 at 500 in the secondary text color.</summary>
    HeaderCell,

    /// <summary>The first cell of a table row: 14 / 20.3 in the secondary text color.</summary>
    RowHeaderCell,

    /// <summary>A table cell: 14 / 20.3.</summary>
    Cell
}

/// <summary>
/// One text of an answer (a paragraph, a heading, a list item or a table cell): its inline pieces as runs, inline code as
/// an embedded <see cref="AiAnswerCodeChip"/>, citations as embedded <see cref="AiAnswerCitation"/> pills, and under the
/// glyphs a soft rounded background on every line of each lit sentence (radius 4, 2px past the text box), drawn by
/// <see cref="TextRangeRenderer"/> without relayout. Streamed text grows at its end and each batch fades in
/// (<see cref="AiAnswerFades"/>); the end of the stream can carry the breathing cursor.
/// </summary>
/// <remarks>
/// <para>
/// Indices: every piece is one inline: a run, or a container whose object is one placeholder character in the layout's
/// text. <see cref="DisplayText"/> is that text; sentence ranges and the fades index it. Selecting and copying reads the
/// pieces back (code as written, citations as "[n]").
/// </para>
/// <para>
/// Code-driven motion (<see cref="MotionValue{T}"/>): a sentence's light is a background inside the text layout, with no
/// property a ControlTheme could animate, so its 160 ms fade is a value that redraws the text.
/// </para>
/// </remarks>
internal sealed class AiAnswerText : DecoratedTextBlock
{
    /// <summary>The placeholder an embedded object stands for in the layout's text.</summary>
    internal const char ObjectChar = '￼';

    /// <summary>Inline code size relative to the text (Arc 0.85em).</summary>
    internal const double CodeScale = 0.85;

    /// <summary>Corner radius of a lit sentence's background.</summary>
    internal const double LightRadius = 4;

    /// <summary>How far a lit sentence's background reaches past the text box on every side.</summary>
    internal const double LightOutset = 2;

    private readonly AiAnswerDocument _owner;
    private readonly AiAnswerFades _fades;
    private readonly List<AiAnswerCitation> _pills = [];
    private readonly List<AiAnswerCodeChip> _chips = [];
    private readonly List<int> _citationIndices = [];
    private readonly List<TextRangeDecoration> _lights = [];
    private readonly List<MotionValue<double>?> _lightValues = [];
    private readonly List<bool> _lightTargets = [];
    private AiAnswerInline[] _inlines = [];
    private List<AiAnswerSentence> _sentences = [];
    private string _display = string.Empty;
    private bool _showsCursor;

    static AiAnswerText()
    {
        // Selectable with the pointer like web text, but not a Tab stop: its citations are.
        KeyboardNavigation.IsTabStopProperty.OverrideDefaultValue<AiAnswerText>(false);
    }

    public AiAnswerText(AiAnswerDocument owner, AiAnswerTextRole role)
    {
        _owner = owner;
        _fades = new AiAnswerFades(this);
        Role = role;
        Classes.Add(RoleClass(role));
        Inlines = new InlineCollection();
        AddHandler(CopyingToClipboardEvent, OnCopying);
    }

    protected override Type StyleKeyOverride => typeof(AiAnswerText);

    /// <summary>What the text is.</summary>
    public AiAnswerTextRole Role { get; }

    /// <summary>The pieces shown.</summary>
    public IReadOnlyList<AiAnswerInline> Pieces => _inlines;

    /// <summary>The layout's text: one placeholder per citation or code span.</summary>
    public string DisplayText => _display;

    /// <summary>The citation pills, in reading order.</summary>
    public IReadOnlyList<AiAnswerCitation> Pills => _pills;

    /// <summary>The inline code chips, in reading order.</summary>
    public IReadOnlyList<AiAnswerCodeChip> Chips => _chips;

    /// <summary>The sentences of the text.</summary>
    public IReadOnlyList<AiAnswerSentence> Sentences => _sentences;

    /// <summary>The background of each sentence (its <see cref="TextRangeDecoration.Opacity"/> is how lit it is).</summary>
    internal IReadOnlyList<TextRangeDecoration> Lights => _lights;

    /// <summary>The fades of streamed batches (tests).</summary>
    internal AiAnswerFades Fades => _fades;

    /// <summary>The display index of each pill.</summary>
    internal IReadOnlyList<int> CitationIndices => _citationIndices;

    /// <summary>Whether the breathing cursor is drawn after the last character.</summary>
    public bool ShowsCursor
    {
        get => _showsCursor;
        set
        {
            if (_showsCursor != value)
            {
                _showsCursor = value;
                InvalidateVisual();
            }
        }
    }

    /// <summary>Whether sentence <paramref name="index"/> is lit (its target, before any fade).</summary>
    internal bool IsSentenceLit(int index) => index >= 0 && index < _lightTargets.Count && _lightTargets[index];

    private static string RoleClass(AiAnswerTextRole role) => role switch
    {
        AiAnswerTextRole.Heading => "heading",
        AiAnswerTextRole.ListItem => "item",
        AiAnswerTextRole.HeaderCell => "header",
        AiAnswerTextRole.RowHeaderCell => "rowheader",
        AiAnswerTextRole.Cell => "cell",
        _ => "paragraph"
    };

    #region Content

    /// <summary>
    /// Shows <paramref name="inlines"/>. Pieces the text already shows are kept (their runs, pills and chips); the first
    /// piece that changed is updated in place when it only grew (the last run or code span of a stream), the rest is
    /// built again (a pill of the same number keeps its control, so focus and pointer stay on it). With
    /// <paramref name="fade"/> the characters after the part that stayed the same fade in.
    /// </summary>
    public void SetContent(AiAnswerInline[] inlines, bool fade)
    {
        var old = _inlines;
        var p = 0;
        while (p < old.Length && p < inlines.Length && old[p] == inlines[p])
        {
            p++;
        }
        if (p == old.Length && p == inlines.Length)
        {
            return;
        }
        var oldDisplay = _display;
        var collection = Inlines!;
        var from = p;
        if (p < old.Length && p < inlines.Length && old[p].Kind == inlines[p].Kind && old[p].Style == inlines[p].Style)
        {
            // The common case while streaming: the last run or code span grew.
            if (old[p].Kind == AiAnswerInlineKind.Text && collection[p] is Run run)
            {
                run.Text = inlines[p].Text;
                from = p + 1;
            }
            else if (old[p].Kind == AiAnswerInlineKind.Code && collection[p] is InlineUIContainer { Child: AiAnswerCodeChip chip })
            {
                chip.Text = inlines[p].Text;
                from = p + 1;
            }
        }
        var citationsBefore = 0;
        var chipsBefore = 0;
        for (var i = 0; i < from; i++)
        {
            if (inlines[i].Kind == AiAnswerInlineKind.Citation)
            {
                citationsBefore++;
            }
            else if (inlines[i].Kind == AiAnswerInlineKind.Code)
            {
                chipsBefore++;
            }
        }
        RemovePieces(from);
        var reusable = _pills.GetRange(citationsBefore, _pills.Count - citationsBefore);
        _pills.RemoveRange(citationsBefore, _pills.Count - citationsBefore);
        _chips.RemoveRange(chipsBefore, _chips.Count - chipsBefore);
        for (var i = from; i < inlines.Length; i++)
        {
            var piece = inlines[i];
            switch (piece.Kind)
            {
                case AiAnswerInlineKind.Code:
                {
                    var chip = CreateChip(piece.Text);
                    _chips.Add(chip);
                    collection.Add(new InlineUIContainer(chip));
                    if (fade)
                    {
                        AiAnswerFades.FadeIn(chip);
                    }
                    break;
                }
                case AiAnswerInlineKind.Citation:
                {
                    AiAnswerCitation? pill = null;
                    if (reusable.Count > 0 && reusable[0].Number == piece.Number)
                    {
                        pill = reusable[0];
                        reusable.RemoveAt(0);
                    }
                    var isNew = pill is null;
                    pill ??= _owner.CreateCitation(piece.Number);
                    pill.Host = this;
                    _pills.Add(pill);
                    collection.Add(new InlineUIContainer(pill));
                    if (isNew && fade)
                    {
                        // The pill is a control over the text, so it fades in itself, with its batch.
                        AiAnswerFades.FadeIn(pill);
                    }
                    break;
                }
                default:
                    collection.Add(CreateRun(piece));
                    break;
            }
        }
        foreach (var dropped in reusable)
        {
            dropped.Host = null;
            _owner.ForgetCitation(dropped);
        }
        _inlines = inlines;
        _display = BuildDisplay(inlines, _citationIndices);
        var keep = CommonPrefix(oldDisplay, _display);
        if (fade)
        {
            _fades.Add(keep, _display.Length);
        }
        else if (_fades.IsActive)
        {
            _fades.Truncate(keep);
        }
        UpdateLights();
    }

    private void RemovePieces(int from)
    {
        var collection = Inlines!;
        if (from >= collection.Count)
        {
            return;
        }
        for (var i = collection.Count - 1; i >= from; i--)
        {
            if (collection[i] is InlineUIContainer container)
            {
                // A pill may be shown again by the rebuilt pieces.
                container.Child = null!;
            }
        }
        collection.RemoveRange(from, collection.Count - from);
    }

    private static Run CreateRun(AiAnswerInline piece)
    {
        var run = new Run(piece.Text);
        if ((piece.Style & AiAnswerInlineStyle.Bold) != 0)
        {
            // Arc's lead sentences are 500, not bold.
            run.FontWeight = FontWeight.Medium;
        }
        if ((piece.Style & AiAnswerInlineStyle.Italic) != 0)
        {
            run.FontStyle = FontStyle.Italic;
        }
        return run;
    }

    private AiAnswerCodeChip CreateChip(string code) => new()
    {
        Text = code,
        FontSize = FontSize * CodeScale,
        BorderBrush = _owner.HairlineBrush
    };

    /// <summary>The layout text of <paramref name="inlines"/>, collecting the index of each citation.</summary>
    internal static string BuildDisplay(IReadOnlyList<AiAnswerInline> inlines, List<int>? citations)
    {
        citations?.Clear();
        var builder = new StringBuilder();
        foreach (var piece in inlines)
        {
            switch (piece.Kind)
            {
                case AiAnswerInlineKind.Code:
                    builder.Append(ObjectChar);
                    break;
                case AiAnswerInlineKind.Citation:
                    citations?.Add(builder.Length);
                    builder.Append(ObjectChar);
                    break;
                default:
                    builder.Append(piece.Text);
                    break;
            }
        }
        return builder.ToString();
    }

    private static int CommonPrefix(string a, string b)
    {
        var n = Math.Min(a.Length, b.Length);
        var i = 0;
        while (i < n && a[i] == b[i])
        {
            i++;
        }
        return i;
    }

    #endregion

    #region Lighting

    /// <summary>Rebuilds the sentence lights after the content changed, keeping the lit state of sentences that stayed.</summary>
    private void UpdateLights()
    {
        _sentences = AiAnswerSentences.Split(_display, _citationIndices);
        var sentenceBrush = _owner.SentenceLightBrush;
        for (var i = 0; i < _sentences.Count; i++)
        {
            var sentence = _sentences[i];
            if (i == _lights.Count)
            {
                _lights.Add(new TextRangeDecoration
                {
                    CornerRadius = LightRadius,
                    Outset = new Thickness(LightOutset),
                    Opacity = 0
                });
                _lightValues.Add(null);
                _lightTargets.Add(false);
            }
            var light = _lights[i];
            light.Start = sentence.Start;
            light.Length = sentence.Length;
            light.Background = sentenceBrush;
        }
        for (var i = _lights.Count - 1; i >= _sentences.Count; i--)
        {
            _lightValues[i]?.Dispose();
            _lights.RemoveAt(i);
            _lightValues.RemoveAt(i);
            _lightTargets.RemoveAt(i);
        }
        var decorations = Decorations;
        if (decorations.Count != _lights.Count || !decorations.SequenceEqual(_lights))
        {
            decorations.Clear();
            decorations.AddRange(_lights);
        }
        else
        {
            InvalidateDecorations();
        }
    }

    /// <summary>Brushes from the document changed (theme): sentence lights and chips repaint.</summary>
    internal void RefreshBrushes()
    {
        var brush = _owner.SentenceLightBrush;
        foreach (var light in _lights)
        {
            light.Background = brush;
        }
        foreach (var chip in _chips)
        {
            chip.BorderBrush = _owner.HairlineBrush;
        }
        InvalidateDecorations();
    }

    /// <summary>
    /// Lights the pills and sentences the active citations call for: a pill when it is <paramref name="hover"/> or
    /// <paramref name="focus"/> or cites <paramref name="litNumber"/>; a sentence when one of its own pills is lit that
    /// way (so a card lights every sentence citing it, and a citation the sentence right before it).
    /// </summary>
    internal void ApplyLighting(AiAnswerCitation? hover, AiAnswerCitation? focus, int litNumber)
    {
        foreach (var pill in _pills)
        {
            pill.IsLit = IsLit(pill, hover, focus, litNumber);
        }
        for (var s = 0; s < _sentences.Count; s++)
        {
            var lit = false;
            foreach (var index in _sentences[s].Citations)
            {
                var k = _citationIndices.IndexOf(index);
                if (k >= 0 && IsLit(_pills[k], hover, focus, litNumber))
                {
                    lit = true;
                    break;
                }
            }
            SetSentenceLit(s, lit);
        }
    }

    private static bool IsLit(AiAnswerCitation pill, AiAnswerCitation? hover, AiAnswerCitation? focus, int litNumber) =>
        pill.HasSource && (ReferenceEquals(pill, hover) || ReferenceEquals(pill, focus) || (litNumber > 0 && pill.Number == litNumber));

    /// <summary>
    /// Lights or dims sentence <paramref name="index"/>: 160 ms on the standard curve at full motion, at once at reduced
    /// motion and off (Arc drops the transition there).
    /// </summary>
    internal void SetSentenceLit(int index, bool lit)
    {
        if (index < 0 || index >= _lights.Count || _lightTargets[index] == lit)
        {
            return;
        }
        _lightTargets[index] = lit;
        var target = lit ? 1.0 : 0.0;
        var light = _lights[index];
        if (ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Full || !IsLoaded)
        {
            _lightValues[index]?.Jump(target);
            light.Opacity = target;
            InvalidateDecorations();
            return;
        }
        var value = _lightValues[index];
        if (value is null)
        {
            value = new MotionValue<double>(this, light.Opacity, MotionVectors.Double);
            value.Changed += v =>
            {
                light.Opacity = v;
                InvalidateDecorations();
            };
            _lightValues[index] = value;
        }
        _ = value.AnimateTo(target, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
    }

    #endregion

    #region Rendering

    protected override void RenderTextLayout(DrawingContext context, Point origin)
    {
        var layout = TextLayout;
        if (Decorations.Count > 0)
        {
            TextRangeRenderer.DrawBackgrounds(context, layout, Decorations, origin);
        }
        DrawSelection(context, layout, origin);
        _fades.Draw(context, layout, origin);
        if (_showsCursor)
        {
            DrawCursor(context, layout, origin);
        }
    }

    private void DrawSelection(DrawingContext context, TextLayout layout, Point origin)
    {
        var start = Math.Min(SelectionStart, SelectionEnd);
        var length = Math.Abs(SelectionEnd - SelectionStart);
        if (length == 0 || SelectionBrush is not { } brush)
        {
            return;
        }
        foreach (var rect in layout.HitTestTextRange(start, length))
        {
            context.FillRectangle(brush, rect.Translate(new Vector(origin.X, origin.Y)));
        }
    }

    /// <summary>
    /// Arc's stream cursor: a dot 0.36em wide, 0.35em after the last character, its bottom 0.22em above the baseline, in
    /// the muted text color, breathing (the document sets its opacity each frame at full motion).
    /// </summary>
    private void DrawCursor(DrawingContext context, TextLayout layout, Point origin)
    {
        if (_owner.CursorBrush is not { } brush)
        {
            return;
        }
        var em = FontSize;
        var x = origin.X;
        var baseline = origin.Y + em;
        var lines = layout.TextLines;
        if (lines.Count > 0 && _display.Length > 0)
        {
            var y = origin.Y;
            for (var i = 0; i < lines.Count - 1; i++)
            {
                y += lines[i].Height;
            }
            var last = lines[^1];
            x = origin.X + last.Start + last.WidthIncludingTrailingWhitespace;
            baseline = y + last.Baseline;
        }
        var radius = 0.18 * em;
        var center = new Point(x + 0.35 * em + radius, baseline - 0.22 * em - radius);
        using (context.PushOpacity(_owner.CursorOpacity))
        {
            context.DrawEllipse(brush, null, center, radius, radius);
        }
    }

    #endregion

    #region Copy

    /// <summary>The text from <paramref name="start"/> to <paramref name="end"/> as it reads: code as written, citations as "[n]", plain spaces.</summary>
    internal string GetPlainText(int start, int end)
    {
        start = Math.Clamp(start, 0, _display.Length);
        end = Math.Clamp(end, start, _display.Length);
        var builder = new StringBuilder(end - start + 8);
        var index = 0;
        foreach (var piece in _inlines)
        {
            var length = piece.Kind == AiAnswerInlineKind.Text ? piece.Text.Length : 1;
            var from = Math.Max(start, index);
            var to = Math.Min(end, index + length);
            if (from < to)
            {
                switch (piece.Kind)
                {
                    case AiAnswerInlineKind.Code:
                        builder.Append(piece.Text);
                        break;
                    case AiAnswerInlineKind.Citation:
                        builder.Append('[').Append(piece.Number).Append(']');
                        break;
                    default:
                        builder.Append(piece.Text, from - index, to - from);
                        break;
                }
            }
            index += length;
        }
        return builder.Replace(AiAnswerMarkdown.NoBreakSpace, ' ').ToString();
    }

    private async void OnCopying(object? sender, RoutedEventArgs e)
    {
        var start = Math.Min(SelectionStart, SelectionEnd);
        var end = Math.Max(SelectionStart, SelectionEnd);
        if (start == end)
        {
            return;
        }
        e.Handled = true;
        await ArcClipboard.CopyTextAsync(this, GetPlainText(start, end));
    }

    #endregion

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FontSizeProperty)
        {
            foreach (var chip in _chips)
            {
                chip.FontSize = FontSize * CodeScale;
            }
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _fades.OnAttached();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        for (var i = 0; i < _lightValues.Count; i++)
        {
            if (_lightValues[i] is { } value)
            {
                value.Jump(_lightTargets[i] ? 1 : 0);
            }
        }
    }
}
