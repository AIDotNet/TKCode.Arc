using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// One result of the <see cref="KnowledgeBase"/> search preview: the rank, the document and chunk, the relevance as a
/// number and a short bar, and a window of the passage with the matching words marked. A button: Enter, Space or a
/// press reports it and scrolls the list to its document.
/// </summary>
[TemplatePart(TextPart, typeof(KnowledgeBasePassageText))]
internal sealed class KnowledgeBasePassageView : AvaloniaButton
{
    internal const string RankPart = "PART_Rank";
    internal const string DocumentPart = "PART_Document";
    internal const string ChunkPart = "PART_Chunk";
    internal const string ScorePart = "PART_Score";
    internal const string ScoreBarPart = "PART_ScoreBar";
    internal const string TextPart = "PART_Text";

    private TextBlock? _rank;
    private TextBlock? _documentName;
    private TextBlock? _chunk;
    private TextBlock? _score;
    private KnowledgeBaseScoreBar? _scoreBar;
    private KnowledgeBasePassageText? _text;

    private int _rankValue;
    private string _preview = string.Empty;
    private IReadOnlyList<KnowledgeTextRange> _ranges = Array.Empty<KnowledgeTextRange>();
    private KnowledgeBaseLabels _labels = KnowledgeBaseLabels.Default;
    private CultureInfo _culture = CultureInfo.InvariantCulture;

    protected override Type StyleKeyOverride => typeof(KnowledgeBasePassageView);

    /// <summary>The passage shown.</summary>
    public KnowledgePassage? Passage { get; private set; }

    /// <summary>The preview text with the cut marks (tests).</summary>
    internal string PreviewText => _preview;

    /// <summary>The marked ranges in <see cref="PreviewText"/> (tests).</summary>
    internal IReadOnlyList<KnowledgeTextRange> Ranges => _ranges;

    internal KnowledgeBasePassageText? TextBlockPart => _text;

    /// <summary>Shows <paramref name="passage"/> as result number <paramref name="rank"/>, marking the words that match <paramref name="terms"/>.</summary>
    public void Show(KnowledgePassage passage, int rank, IReadOnlyList<string> terms, KnowledgeBaseLabels labels, CultureInfo culture, int previewLength)
    {
        Passage = passage;
        _rankValue = rank;
        _labels = labels;
        _culture = culture;
        (_preview, _ranges) = KnowledgeBaseText.Preview(passage.Text, terms, previewLength);
        Apply();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _rank = e.NameScope.Find<TextBlock>(RankPart);
        _documentName = e.NameScope.Find<TextBlock>(DocumentPart);
        _chunk = e.NameScope.Find<TextBlock>(ChunkPart);
        _score = e.NameScope.Find<TextBlock>(ScorePart);
        _scoreBar = e.NameScope.Find<KnowledgeBaseScoreBar>(ScoreBarPart);
        _text = e.NameScope.Find<KnowledgeBasePassageText>(TextPart);
        Apply();
    }

    private void Apply()
    {
        if (Passage is not { } passage)
        {
            return;
        }
        var score = passage.Score.ToString("0.00", _culture);
        if (_rank is not null)
        {
            _rank.Text = _rankValue.ToString(_culture);
        }
        if (_documentName is not null)
        {
            _documentName.Text = passage.Document.Name;
        }
        if (_chunk is not null)
        {
            _chunk.Text = _labels.ChunkNumber(passage.ChunkIndex + 1);
        }
        if (_score is not null)
        {
            _score.Text = score;
        }
        if (_scoreBar is not null)
        {
            _scoreBar.Value = passage.Score;
        }
        _text?.SetMarks(_preview, _ranges);
        AutomationProperties.SetName(this, _labels.PassageName(_rankValue, passage.Document.Name, score, _preview));
    }
}

/// <summary>
/// The passage text of a search result with the given ranges marked like a search highlight (the accent at 14% behind
/// the word, 3px corners). The marks are reused from result to result.
/// </summary>
internal sealed class KnowledgeBasePassageText : DecoratedTextBlock
{
    /// <summary>The accent the marks are tinted from (the theme binds it to the <c>Accent</c> token).</summary>
    public static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<KnowledgeBasePassageText, IBrush?>(nameof(AccentBrush));

    private readonly List<TextRangeDecoration> _marks = new();
    private IBrush? _markBrush;

    protected override Type StyleKeyOverride => typeof(KnowledgeBasePassageText);

    /// <inheritdoc cref="AccentBrushProperty"/>
    public IBrush? AccentBrush { get => GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }

    /// <summary>The number of marks in use (tests).</summary>
    internal int MarkCount => Decorations.Count;

    /// <summary>Shows <paramref name="text"/> with <paramref name="ranges"/> marked.</summary>
    public void SetMarks(string text, IReadOnlyList<KnowledgeTextRange> ranges)
    {
        Text = text;
        for (var i = 0; i < ranges.Count; i++)
        {
            if (i == _marks.Count)
            {
                _marks.Add(new TextRangeDecoration { CornerRadius = HighlightedText.MatchRadius, Background = _markBrush });
            }
            _marks[i].Start = ranges[i].Start;
            _marks[i].Length = ranges[i].Length;
            if (i >= Decorations.Count)
            {
                Decorations.Add(_marks[i]);
            }
        }
        if (Decorations.Count > ranges.Count)
        {
            Decorations.RemoveRange(ranges.Count, Decorations.Count - ranges.Count);
        }
        InvalidateDecorations();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == AccentBrushProperty)
        {
            _markBrush = change.GetNewValue<IBrush?>() is ISolidColorBrush accent
                ? new ImmutableSolidColorBrush(Color.FromArgb((byte)Math.Round(accent.Color.A * HighlightedText.MatchAlpha * accent.Opacity),
                    accent.Color.R, accent.Color.G, accent.Color.B))
                : null;
            foreach (var mark in _marks)
            {
                mark.Background = _markBrush;
            }
            InvalidateDecorations();
        }
    }
}

/// <summary>A short relevance bar: a pill track with a fill to <see cref="Value"/> (0–1). Drawn, not templated.</summary>
internal sealed class KnowledgeBaseScoreBar : Control
{
    /// <summary>How full the bar is, from 0 to 1.</summary>
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<KnowledgeBaseScoreBar, double>(nameof(Value), coerce: static (_, v) => double.IsNaN(v) ? 0 : Math.Clamp(v, 0, 1));

    /// <summary>The fill.</summary>
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<KnowledgeBaseScoreBar, IBrush?>(nameof(Fill));

    /// <summary>The track under the fill.</summary>
    public static readonly StyledProperty<IBrush?> TrackProperty =
        AvaloniaProperty.Register<KnowledgeBaseScoreBar, IBrush?>(nameof(Track));

    static KnowledgeBaseScoreBar()
    {
        AffectsRender<KnowledgeBaseScoreBar>(ValueProperty, FillProperty, TrackProperty);
    }

    /// <inheritdoc cref="ValueProperty"/>
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <inheritdoc cref="FillProperty"/>
    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }

    /// <inheritdoc cref="TrackProperty"/>
    public IBrush? Track { get => GetValue(TrackProperty); set => SetValue(TrackProperty, value); }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }
        // A pill at any height: the radius is half the height, so a square-corner theme still reads it as a bar.
        var radius = bounds.Height / 2;
        if (Track is { } track)
        {
            context.DrawRectangle(track, null, bounds, radius, radius);
        }
        var width = bounds.Width * Value;
        if (Fill is { } fill && width > 0)
        {
            context.DrawRectangle(fill, null, new Rect(0, 0, Math.Max(width, bounds.Height), bounds.Height), radius, radius);
        }
    }
}
