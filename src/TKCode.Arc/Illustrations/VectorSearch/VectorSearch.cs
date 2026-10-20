using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Every visible word of a <see cref="VectorSearch"/> (override for another language or domain).</summary>
public sealed class VectorSearchLabels
{
    public string Model { get; init; } = "Embedding model";
    public string ModelDetail { get; init; } = "256 dimensions";
    public Func<int, string> Header { get; init; } = static k => $"Top {k} · cosine similarity";
    public string Typing { get; init; } = "A question comes in";
    public string Embedding { get; init; } = "The model turns it into a 256-dimension vector";
    public Func<int, string> Comparing { get; init; } = static n => $"The vector is compared with {n} document vectors";
    public Func<int, string> Answers { get; init; } = static k => $"The {k} nearest documents become the answers";
    public string Summary { get; init; } = "A question is embedded as a vector and answered with the documents whose vectors are most similar";
}

/// <summary>
/// An animated semantic search: a question is typed, embedded into a vector, dropped into a map of document vectors,
/// and its nearest neighbours by cosine similarity become the ranked answers. The search is real (hashed bag-of-words
/// embeddings, cosine ranking, a principal-component map), so <see cref="Search"/> works with any question.
/// </summary>
public class VectorSearch : Illustration
{
    /// <summary>The documents to search (defaults to 32 help-centre articles in four topics).</summary>
    public static readonly StyledProperty<IReadOnlyList<VectorSearchDocument>?> DocumentsProperty =
        AvaloniaProperty.Register<VectorSearch, IReadOnlyList<VectorSearchDocument>?>(nameof(Documents));

    /// <summary>The questions the story cycles through.</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> QueriesProperty =
        AvaloniaProperty.Register<VectorSearch, IReadOnlyList<string>?>(nameof(Queries));

    /// <summary>How many answers are shown (1–5).</summary>
    public static readonly StyledProperty<int> TopKProperty =
        AvaloniaProperty.Register<VectorSearch, int>(nameof(TopK), 3, coerce: static (_, v) => Math.Clamp(v, 1, 5));

    public static readonly StyledProperty<VectorSearchLabels> LabelsProperty =
        AvaloniaProperty.Register<VectorSearch, VectorSearchLabels>(nameof(Labels), new VectorSearchLabels());

    public IReadOnlyList<VectorSearchDocument>? Documents { get => GetValue(DocumentsProperty); set => SetValue(DocumentsProperty, value); }

    public IReadOnlyList<string>? Queries { get => GetValue(QueriesProperty); set => SetValue(QueriesProperty, value); }

    public int TopK { get => GetValue(TopKProperty); set => SetValue(TopKProperty, value); }

    public VectorSearchLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <summary>The question being shown.</summary>
    public string Query => _query;

    /// <summary>The answers of the current question, best first.</summary>
    public IReadOnlyList<VectorSearchResult> Results => _results;

    /// <summary>Raised when the answers of a question are known.</summary>
    public event EventHandler? ResultsChanged;

    public override int StepCount => 4;

    /// <summary>The questions used when <see cref="Queries"/> is not set.</summary>
    public static IReadOnlyList<string> DefaultQueries { get; } =
        ["refund my annual plan", "reset two-factor on a new device", "my package is delayed"];

    /// <summary>The documents used when <see cref="Documents"/> is not set.</summary>
    public static IReadOnlyList<VectorSearchDocument> DefaultDocuments { get; } = CreateDefaultDocuments();

    private const double SceneW = 844;
    private const double SceneH = 330;
    private const int Cells = 16;
    private const double CharDelay = 0.045;
    private static readonly Rect s_queryBox = new(14, 18, 320, 40);
    private static readonly Rect s_strip = new(212, 98, 122, 40);
    private static readonly Rect s_plot = new(362, 18, 468, 300);
    private const double ResultsTop = 168;
    private const double RowHeight = 28;

    private VectorSearchIndex _index = null!;
    private Point[] _points = Array.Empty<Point>();
    private (string Topic, Point At)[] _clusters = Array.Empty<(string, Point)>();
    private IllustrationValue[] _highlight = Array.Empty<IllustrationValue>();
    private readonly IllustrationValue[] _cells = new IllustrationValue[Cells];
    private readonly double[] _cellTargets = new double[Cells];
    private readonly IllustrationValue[] _rows = new IllustrationValue[5];
    private IllustrationValue _queryShown = new(0);
    private IllustrationValue _radius = new(0);
    private IllustrationNode _queryNode = null!;
    private IllustrationNode _model = null!;
    private IllustrationNode _stripNode = null!;
    private IllustrationNode _pointNode = null!;
    private IllustrationEdge _flight = null!;
    private IllustrationEdge _toModel = null!;
    private IllustrationEdge _toStrip = null!;
    private string _query = string.Empty;
    private string _typedText = string.Empty;
    private int _typedCount;
    private double _caretX = s_queryBox.X + 40;
    private bool _caretDrawn;
    private string _header = string.Empty;
    private string[] _rankTexts = Array.Empty<string>();
    private string[] _scoreTexts = Array.Empty<string>();
    private int[] _resultIndices = Array.Empty<int>();
    private IReadOnlyList<VectorSearchResult> _results = Array.Empty<VectorSearchResult>();
    private Point _queryPoint;
    private double _ringRadius;
    private double _typeStart = double.NaN;
    private int _queryIndex;

    static VectorSearch()
    {
        LabelProperty.OverrideDefaultValue<VectorSearch>("Vector search illustration");
    }

    private protected override double SceneWidth => SceneW;

    private protected override double SceneHeight => SceneH;

    private IReadOnlyList<string> EffectiveQueries => Queries is { Count: > 0 } list ? list : DefaultQueries;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DocumentsProperty || change.Property == QueriesProperty || change.Property == LabelsProperty ||
            change.Property == TopKProperty)
        {
            Rebuild();
        }
    }

    /// <summary>Shows <paramref name="query"/> now (typed, embedded, matched); the story then carries on with its own questions.</summary>
    public void Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return;
        }
        CancelPending();
        if (IsPoster)
        {
            ShowFinal(query);
            return;
        }
        StartQuery(query);
    }

    #region Scene

    private protected override void BuildScene()
    {
        var labels = Labels;
        _index = new VectorSearchIndex(Documents is { Count: > 0 } docs ? docs : DefaultDocuments);
        _header = labels.Header(TopK);
        _queryNode = AddNode(new IllustrationNode("query", s_queryBox, null, string.Empty) { IsHidden = true });
        _model = AddNode(new IllustrationNode("model", new Rect(14, 92, 186, 52), IllustrationGlyphs.BrainCircuit, labels.Model, labels.ModelDetail));
        _stripNode = AddNode(new IllustrationNode("vector", s_strip, null, string.Empty) { IsHidden = true });
        _pointNode = AddNode(new IllustrationNode("point", new Rect(s_plot.Center, new Size(1, 1)), null, string.Empty) { IsHidden = true });
        var from = new Point(_model.Center.X, s_queryBox.Bottom);
        _toModel = AddEdge(new IllustrationEdge(_queryNode, _model, IllustrationEdge.Curve(from, _model.Top, vertical: true), curved: true));
        _toStrip = AddEdge(new IllustrationEdge(_model, _stripNode, IllustrationEdge.Curve(_model.Right, new Point(s_strip.Left - 6, s_strip.Center.Y)), curved: true));
        _flight = AddEdge(new IllustrationEdge(_stripNode, _pointNode,
            IllustrationEdge.Curve(new Point(s_strip.Right + 4, s_strip.Center.Y), s_plot.Center), curved: true, dashed: true));
        LayoutPoints();
    }

    /// <summary>Maps the documents' principal components into the plot, with a margin, and finds each topic's centre.</summary>
    private void LayoutPoints()
    {
        var positions = _index.Positions;
        _highlight = new IllustrationValue[positions.Length];
        if (positions.Length == 0)
        {
            _points = Array.Empty<Point>();
            _clusters = Array.Empty<(string, Point)>();
            return;
        }
        var minX = positions.Min(p => p.X);
        var maxX = positions.Max(p => p.X);
        var minY = positions.Min(p => p.Y);
        var maxY = positions.Max(p => p.Y);
        var area = s_plot.Deflate(new Thickness(34, 40, 34, 26));
        _points = positions.Select(p => Map(p.X, p.Y, minX, maxX, minY, maxY, area)).ToArray();
        _clusters = _index.Documents.Select((d, i) => (d.Topic, Point: _points[i]))
                          .GroupBy(x => x.Topic)
                          .Select(g => (g.Key, new Point(g.Average(x => x.Point.X), g.Min(x => x.Point.Y) - 16)))
                          .ToArray();
        _mapBounds = (minX, maxX, minY, maxY, area);
    }

    private (double MinX, double MaxX, double MinY, double MaxY, Rect Area) _mapBounds;

    private static Point Map(double x, double y, double minX, double maxX, double minY, double maxY, Rect area)
    {
        var fx = maxX - minX < 1e-9 ? 0.5 : (x - minX) / (maxX - minX);
        var fy = maxY - minY < 1e-9 ? 0.5 : (y - minY) / (maxY - minY);
        return new Point(area.X + fx * area.Width, area.Y + fy * area.Height);
    }

    private protected override void BuildScript(IllustrationScript script)
    {
        script.At(0, () => StartQuery(EffectiveQueries[_queryIndex % EffectiveQueries.Count]));
    }

    private protected override void OnRestart()
    {
        _queryIndex = 0;
        _typeStart = double.NaN;
        ResetQueryVisuals(animate: false);
    }

    private protected override void ApplyPoster()
    {
        ShowFinal(EffectiveQueries[0]);
        SetStep(0, Labels.Summary);
    }

    #endregion

    #region Story

    private void ResetQueryVisuals(bool animate)
    {
        for (var i = 0; i < Cells; i++)
        {
            if (animate)
            {
                _cells[i].Tween(0, Now, 0.2);
            }
            else
            {
                _cells[i].Jump(0);
            }
        }
        for (var i = 0; i < _highlight.Length; i++)
        {
            if (animate)
            {
                _highlight[i].Tween(0, Now, 0.25);
            }
            else
            {
                _highlight[i].Jump(0);
            }
        }
        for (var i = 0; i < _rows.Length; i++)
        {
            if (animate)
            {
                _rows[i].Tween(0, Now, 0.2);
            }
            else
            {
                _rows[i].Jump(0);
            }
        }
        if (animate)
        {
            _queryShown.Tween(0, Now, 0.2);
            _radius.Tween(0, Now, 0.2);
        }
        else
        {
            _queryShown.Jump(0);
            _radius.Jump(0);
        }
        _flight.Shown.Jump(0);
        _model.IsBusy = false;
    }

    /// <summary>Runs the search and places the query (no animation).</summary>
    private void Compute(string query)
    {
        _query = query;
        var vector = VectorSearchIndex.Embed(query);
        _results = _index.Search(vector, TopK);
        _resultIndices = _results.Select(r => Math.Max(0, _index.IndexOf(r.Document))).ToArray();
        _rankTexts = _results.Select(r => r.Rank.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        _scoreTexts = _results.Select(r => r.Score.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        _typedCount = -1;
        var (minX, maxX, minY, maxY, area) = _mapBounds;
        var (x, y) = _index.Place(_results);
        var point = Map(x, y, minX, maxX, minY, maxY, area);
        // Sit just beside the neighbours rather than on top of one of them.
        var toCenter = s_plot.Center - point;
        var length = Math.Sqrt(toCenter.X * toCenter.X + toCenter.Y * toCenter.Y);
        if (length > 1)
        {
            point += toCenter / length * 12;
        }
        _queryPoint = new Point(Math.Clamp(point.X, area.Left, area.Right), Math.Clamp(point.Y, area.Top, area.Bottom));
        _ringRadius = 14;
        foreach (var index in _resultIndices)
        {
            var d = _points[index] - _queryPoint;
            _ringRadius = Math.Max(_ringRadius, Math.Sqrt(d.X * d.X + d.Y * d.Y) + 8);
        }
        _ringRadius = Math.Min(_ringRadius, 110);
        // A dense 16-number view of the vector: a fixed random projection of all 64 dimensions.
        var max = 1e-9;
        for (var j = 0; j < Cells; j++)
        {
            var value = 0.0;
            for (var i = 0; i < VectorSearchIndex.Dimensions; i++)
            {
                value += vector[i] * Math.Sin((i + 1) * (j + 3) * 0.61);
            }
            _cellTargets[j] = value;
            max = Math.Max(max, Math.Abs(value));
        }
        for (var j = 0; j < Cells; j++)
        {
            _cellTargets[j] /= max;
        }
        _flight.SetRoute(IllustrationEdge.Curve(new Point(s_strip.Right + 4, s_strip.Center.Y), _queryPoint));
        _pointNode.Bounds = new Rect(_queryPoint, new Size(1, 1));
    }

    private void StartQuery(string query)
    {
        var labels = Labels;
        ResetQueryVisuals(animate: true);
        Compute(query);
        _typeStart = Now + 0.25;
        SetStep(0, labels.Typing);
        var typed = 0.25 + query.Length * CharDelay + 0.2;
        After(typed, () =>
        {
            SetStep(1, labels.Embedding);
            _toModel.Active.Tween(0.8, Now, 0.3);
            Spawn(_toModel, 0.5, eased: true);
        });
        After(typed + 0.5, () => _model.IsBusy = true);
        After(typed + 0.95, () =>
        {
            _model.IsBusy = false;
            _toModel.Active.Tween(0, Now, 0.4);
            _toStrip.Active.Tween(0.8, Now, 0.3);
            Spawn(_toStrip, 0.3, eased: true);
            for (var j = 0; j < Cells; j++)
            {
                _cells[j].Tween(_cellTargets[j], Now, 0.35, ArcEasings.Standard, delay: 0.2 + j * 0.025);
            }
        });
        After(typed + 1.6, () =>
        {
            SetStep(2, labels.Comparing(_index.Documents.Count));
            _toStrip.Active.Tween(0, Now, 0.4);
            _flight.Shown.Tween(1, Now, 0.2);
            _flight.Active.Jump(0.8);
            Spawn(_flight, 0.7, eased: true);
        });
        After(typed + 2.3, () =>
        {
            _queryShown.Jump(0);
            _queryShown.Spring(1, Now, ArcMotionPresets.Snappy);
            _flight.Shown.Tween(0, Now, 0.4);
            _radius.Tween(_ringRadius, Now, 0.5);
        });
        After(typed + 2.6, () =>
        {
            SetStep(3, labels.Answers(_results.Count));
            for (var r = 0; r < _resultIndices.Length; r++)
            {
                _highlight[_resultIndices[r]].Spring(1, Now, ArcMotionPresets.Snappy, delay: r * 0.15);
                _rows[r].Tween(1, Now, 0.3, ArcEasings.Enter, delay: 0.15 + r * 0.1);
            }
            ResultsChanged?.Invoke(this, EventArgs.Empty);
        });
        After(typed + 2.6 + 0.15 + _results.Count * 0.1 + 2.6, () =>
        {
            _queryIndex++;
            StartQuery(EffectiveQueries[_queryIndex % EffectiveQueries.Count]);
        });
    }

    /// <summary>The finished state of <paramref name="query"/> without animation (poster frame, reduced motion).</summary>
    private void ShowFinal(string query)
    {
        Compute(query);
        _typeStart = double.NegativeInfinity;
        for (var j = 0; j < Cells; j++)
        {
            _cells[j].Jump(_cellTargets[j]);
        }
        for (var i = 0; i < _highlight.Length; i++)
        {
            _highlight[i].Jump(0);
        }
        for (var r = 0; r < _rows.Length; r++)
        {
            _rows[r].Jump(r < _resultIndices.Length ? 1 : 0);
        }
        foreach (var index in _resultIndices)
        {
            _highlight[index].Jump(1);
        }
        _queryShown.Jump(1);
        _radius.Jump(_ringRadius);
        _flight.Shown.Jump(0);
        SetStep(3, Labels.Answers(_results.Count));
        ResultsChanged?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Drawing

    private protected override void RenderUnderlay(DrawingContext context, double scale)
    {
        var p = Palette;
        var now = Now;
        var plot = new RoundedRect(s_plot, 16);
        context.DrawRectangle(p.Tint(IllustrationInk.SurfaceMuted, 0.55), p.Pen(IllustrationInk.Line, 1), plot);
        using var clip = context.PushClip(plot);
        foreach (var (topic, at) in _clusters)
        {
            var text = p.Text(topic, IllustrationInk.Muted, small: true, medium: true);
            context.DrawText(text, new Point(at.X - text.Width / 2, at.Y - text.Height / 2));
        }
        var shown = Math.Clamp(_queryShown.Value(now), 0, 1.3);
        var radius = _radius.Value(now);
        if (radius > 1)
        {
            context.DrawEllipse(p.Tint(IllustrationInk.Accent, 0.05), p.Pen(IllustrationInk.Accent, 1, dashed: true), _queryPoint, radius, radius);
        }
        for (var i = 0; i < _points.Length; i++)
        {
            var h = Math.Clamp(_highlight[i].Value(now), 0, 1.2);
            if (h > 0.01)
            {
                using (context.PushOpacity(Math.Min(1, h)))
                {
                    context.DrawLine(p.TintPen(IllustrationInk.Accent, 0.55, 1), _queryPoint, _points[i]);
                }
            }
        }
        for (var i = 0; i < _points.Length; i++)
        {
            var h = Math.Clamp(_highlight[i].Value(now), 0, 1.2);
            if (h > 0.01)
            {
                context.DrawEllipse(p.Tint(IllustrationInk.Accent, 0.2 * Math.Min(1, h)), null, _points[i], 7 * h, 7 * h);
                context.DrawEllipse(p.Brush(IllustrationInk.Accent), null, _points[i], 3 + 1.2 * h, 3 + 1.2 * h);
            }
            else
            {
                context.DrawEllipse(p.Brush(IllustrationInk.StrongLine), null, _points[i], 3, 3);
            }
        }
        if (shown > 0.01)
        {
            var r = 5 * shown;
            context.DrawEllipse(p.Tint(IllustrationInk.Accent, 0.22), null, _queryPoint, r * 2.2, r * 2.2);
            context.DrawEllipse(p.Brush(IllustrationInk.Accent), p.Pen(IllustrationInk.Surface, 2), _queryPoint, r, r);
        }
    }

    private protected override void RenderOverlay(DrawingContext context, double scale)
    {
        var p = Palette;
        var now = Now;

        // Query box with the typed question and a blinking caret.
        var box = new RoundedRect(s_queryBox, s_queryBox.Height / 2);
        context.DrawRectangle(p.Brush(IllustrationInk.Surface), p.Pen(IllustrationInk.Line, 1), box, p.Shadow);
        DrawIcon(context, ArcIcons.Search, new Rect(s_queryBox.X + 14, s_queryBox.Center.Y - 8, 16, 16), IllustrationInk.Muted);
        var typedCount = double.IsNaN(_typeStart) ? 0 : (int)Math.Clamp((now - _typeStart) / CharDelay, 0, _query.Length);
        var textX = s_queryBox.X + 40;
        var caretX = textX;
        if (typedCount != _typedCount)
        {
            // Only a new character makes a new string (about 40 per question), never a frame.
            _typedCount = typedCount;
            _typedText = _query[..typedCount];
        }
        if (typedCount > 0)
        {
            var text = p.Text(_typedText, IllustrationInk.Foreground, small: false);
            using (context.PushClip(new Rect(textX, s_queryBox.Y, s_queryBox.Right - textX - 14, s_queryBox.Height)))
            {
                context.DrawText(text, new Point(textX, s_queryBox.Center.Y - text.Height / 2));
            }
            caretX = Math.Min(textX + text.WidthIncludingTrailingWhitespace + 1, s_queryBox.Right - 14);
        }
        _caretX = caretX;

        // Vector strip: 16 bars around a midline.
        var mid = s_strip.Center.Y;
        context.DrawLine(p.Pen(IllustrationInk.SubtleLine, 1), new Point(s_strip.Left, mid), new Point(s_strip.Right, mid));
        var barWidth = 5.5;
        var pitch = (s_strip.Width - barWidth) / (Cells - 1);
        for (var j = 0; j < Cells; j++)
        {
            var v = Math.Clamp(_cells[j].Value(now), -1.2, 1.2);
            if (Math.Abs(v) < 0.01)
            {
                continue;
            }
            var height = Math.Abs(v) * (s_strip.Height / 2 - 2);
            var x = s_strip.Left + j * pitch;
            var rect = v > 0 ? new Rect(x, mid - height, barWidth, height) : new Rect(x, mid, barWidth, height);
            context.DrawRectangle(p.Tint(IllustrationInk.Accent, 0.35 + 0.55 * Math.Min(1, Math.Abs(v))), null, new RoundedRect(rect, 1.5));
        }

        // Results.
        var header = p.Text(_header, IllustrationInk.Muted, small: true, medium: true);
        context.DrawText(header, new Point(14, ResultsTop));
        for (var r = 0; r < _results.Count && r < _rows.Length; r++)
        {
            var shown = Math.Clamp(_rows[r].Value(now), 0, 1);
            if (shown <= 0.01)
            {
                continue;
            }
            var result = _results[r];
            var y = ResultsTop + 24 + r * RowHeight + (1 - shown) * 8;
            using (context.PushOpacity(shown))
            {
                var rank = p.Text(_rankTexts[r], IllustrationInk.Muted, small: true, mono: true);
                context.DrawText(rank, new Point(14, y));
                var score = p.Text(_scoreTexts[r], IllustrationInk.Secondary, small: true, mono: true);
                context.DrawText(score, new Point(334 - score.Width, y));
                var title = p.Text(result.Document.Title, IllustrationInk.Foreground, small: true, medium: r == 0);
                using (context.PushClip(new Rect(32, y - 2, 334 - score.Width - 44, title.Height + 4)))
                {
                    context.DrawText(title, new Point(32, y));
                }
                var track = new Rect(32, y + title.Height + 3, 334 - 32, 2);
                context.DrawRectangle(p.Brush(IllustrationInk.SubtleLine), null, new RoundedRect(track, 1));
                var fill = track.WithWidth(track.Width * Math.Clamp(result.Score, 0, 1) * shown);
                if (fill.Width > 0.5)
                {
                    context.DrawRectangle(p.Tint(IllustrationInk.Accent, 0.75), null, new RoundedRect(fill, 1));
                }
            }
        }
    }

    private bool IsTyping(double now) =>
        !double.IsNaN(_typeStart) && !double.IsNegativeInfinity(_typeStart) && (now - _typeStart) / CharDelay < _query.Length;

    /// <summary>Caret shown while typing, then blinking (0.55s on, 0.45s off); never in the still poster frame.</summary>
    private bool CaretVisible(double now) => IsTyping(now) || (!IsPoster && now % 1.0 < 0.55);

    private protected override bool IsMoving(double now)
    {
        if (IsTyping(now) || _queryShown.IsMoving(now) || _radius.IsMoving(now))
        {
            return true;
        }
        foreach (var cell in _cells)
        {
            if (cell.IsMoving(now))
            {
                return true;
            }
        }
        foreach (var row in _rows)
        {
            if (row.IsMoving(now))
            {
                return true;
            }
        }
        foreach (var highlight in _highlight)
        {
            if (highlight.IsMoving(now))
            {
                return true;
            }
        }
        return false;
    }

    private protected override bool NeedsMotionFrame(double now) => CaretVisible(now) != _caretDrawn || IsTyping(now);

    /// <summary>The caret blinks on the motion layer, so a blink redraws one line rather than the whole scene.</summary>
    private protected override void RenderMotionOverlay(DrawingContext context, double scale)
    {
        var now = Now;
        _caretDrawn = CaretVisible(now);
        if (_caretDrawn)
        {
            context.DrawLine(Palette.Pen(IllustrationInk.Accent, 1.5), new Point(_caretX, s_queryBox.Center.Y - 8),
                new Point(_caretX, s_queryBox.Center.Y + 8));
        }
    }

    #endregion

    private static IReadOnlyList<VectorSearchDocument> CreateDefaultDocuments()
    {
        const string billing = "billing payment invoice charge";
        const string accounts = "account workspace profile member";
        const string shipping = "shipping delivery order carrier";
        const string security = "security login password device";
        return
        [
            new("Refund policy for annual plans", "Billing", billing),
            new("Update your payment method", "Billing", billing),
            new("Download invoices and receipts", "Billing", billing),
            new("Change your billing currency", "Billing", billing),
            new("Cancel or pause a subscription", "Billing", billing),
            new("Why was I charged twice?", "Billing", billing),
            new("Switch from monthly to annual billing", "Billing", billing),
            new("Apply a discount code", "Billing", billing),
            new("Invite teammates to your workspace", "Accounts", accounts),
            new("Change your email address", "Accounts", accounts),
            new("Transfer account ownership", "Accounts", accounts),
            new("Delete your account", "Accounts", accounts),
            new("Manage roles and permissions", "Accounts", accounts),
            new("Merge duplicate accounts", "Accounts", accounts),
            new("Update your profile and avatar", "Accounts", accounts),
            new("Leave a workspace", "Accounts", accounts),
            new("Track a delayed package", "Shipping", shipping),
            new("Change a delivery address", "Shipping", shipping),
            new("International shipping times", "Shipping", shipping),
            new("Return a damaged order", "Shipping", shipping),
            new("Schedule a pickup with the carrier", "Shipping", shipping),
            new("Shipping rates by region", "Shipping", shipping),
            new("Missing items in a package", "Shipping", shipping),
            new("Hold a package at a pickup point", "Shipping", shipping),
            new("Reset two-factor authentication", "Security", security),
            new("Change your password", "Security", security),
            new("Review active sessions and devices", "Security", security),
            new("Set up single sign-on", "Security", security),
            new("Recover a locked account", "Security", security),
            new("Report a suspicious login", "Security", security),
            new("Create recovery codes", "Security", security),
            new("Use passkeys to sign in", "Security", security)
        ];
    }
}
