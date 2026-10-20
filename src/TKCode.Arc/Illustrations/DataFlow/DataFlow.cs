using System.Globalization;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>A source of events in a <see cref="DataFlow"/> (title and a short detail line).</summary>
public sealed record DataFlowSource(string Title, string? Detail = null);

/// <summary>Every visible word of a <see cref="DataFlow"/> (override for another language or domain).</summary>
public sealed class DataFlowLabels
{
    public string Stream { get; init; } = "Event stream";
    public string StreamDetail { get; init; } = "buffered";
    public string Transform { get; init; } = "Transform";
    public string TransformDetail { get; init; } = "validate · enrich";
    public string Warehouse { get; init; } = "Warehouse";
    public string WarehouseDetail { get; init; } = "columnar";
    public string Realtime { get; init; } = "Dashboards";
    public string RealtimeDetail { get; init; } = "streaming";
    public Func<int, string> EventsPerSecond { get; init; } = static n => $"{n} events/s";
    public Func<long, string> Rows { get; init; } = static n => $"{n.ToString("N0", CultureInfo.InvariantCulture)} rows";
    public Func<long, string> Updates { get; init; } = static n => $"{n.ToString("N0", CultureInfo.InvariantCulture)} updates";
    public Func<int, string> Ingest { get; init; } = static sources => $"Events stream in from {sources} sources and are buffered";
    public string TransformStep { get; init; } = "Each batch is validated and enriched; bad events are filtered out";
    public string StoreStep { get; init; } = "Clean events land in the warehouse and update live dashboards";
    public string Summary { get; init; } = "Events from every source are buffered, validated in batches and written to the warehouse and live dashboards";
}

/// <summary>
/// An animated data pipeline: events leave their sources, queue up in an event stream, are validated and enriched in
/// batches (some are filtered out) and land in a warehouse, with part of them pushed to live dashboards. Readouts show
/// throughput and what has been stored.
/// </summary>
public class DataFlow : Illustration
{
    /// <summary>How many events per second the sources emit (1–60).</summary>
    public static readonly StyledProperty<double> EventsPerSecondProperty =
        AvaloniaProperty.Register<DataFlow, double>(nameof(EventsPerSecond), 18, coerce: static (_, v) => double.IsFinite(v) ? Math.Clamp(v, 1, 60) : 18);

    /// <summary>Share of events the transform filters out (0–0.5).</summary>
    public static readonly StyledProperty<double> FilterRatioProperty =
        AvaloniaProperty.Register<DataFlow, double>(nameof(FilterRatio), 0.14, coerce: static (_, v) => double.IsFinite(v) ? Math.Clamp(v, 0, 0.5) : 0.14);

    /// <summary>The event sources (1–4); defaults to a web app, a mobile app and webhooks.</summary>
    public static readonly StyledProperty<IReadOnlyList<DataFlowSource>?> SourcesProperty =
        AvaloniaProperty.Register<DataFlow, IReadOnlyList<DataFlowSource>?>(nameof(Sources));

    public static readonly StyledProperty<DataFlowLabels> LabelsProperty =
        AvaloniaProperty.Register<DataFlow, DataFlowLabels>(nameof(Labels), new DataFlowLabels());

    public double EventsPerSecond { get => GetValue(EventsPerSecondProperty); set => SetValue(EventsPerSecondProperty, value); }

    public double FilterRatio { get => GetValue(FilterRatioProperty); set => SetValue(FilterRatioProperty, value); }

    public IReadOnlyList<DataFlowSource>? Sources { get => GetValue(SourcesProperty); set => SetValue(SourcesProperty, value); }

    public DataFlowLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <summary>The sources shown when <see cref="Sources"/> is not set.</summary>
    public static IReadOnlyList<DataFlowSource> DefaultSources { get; } =
    [
        new("Web app", "page views"),
        new("Mobile", "taps · sessions"),
        new("Webhooks", "partner events")
    ];

    /// <summary>Events written to the warehouse since the story started.</summary>
    public long RowsStored => _rows;

    /// <summary>Events filtered out by the transform.</summary>
    public long EventsFiltered => _filtered;

    public override int StepCount => 3;

    private const double SceneW = 844;
    private const double SceneH = 264;
    private const double BatchInterval = 1.0;
    private const int BatchSize = 8;
    private const int BufferCapacity = 24;
    private static readonly Geometry[] s_sourceIcons = [ArcIcons.Laptop, ArcIcons.Smartphone, IllustrationGlyphs.Webhook, ArcIcons.Server];

    private readonly List<IllustrationEdge> _sourceEdges = new();
    private IllustrationNode _stream = null!;
    private IllustrationNode _transform = null!;
    private IllustrationNode _warehouse = null!;
    private IllustrationNode _realtime = null!;
    private IllustrationEdge _toTransform = null!;
    private IllustrationEdge _toWarehouse = null!;
    private IllustrationEdge _toRealtime = null!;
    private IllustrationLabel _rate = null!;
    private IllustrationLabel _rowsLabel = null!;
    private IllustrationLabel _updatesLabel = null!;
    private IllustrationRandom _random = new(0x5EED);
    private double _spawnClock;
    private double _batchClock;
    private double _rateClock;
    private double _nextSpawn;
    private double _arrivalRate;
    private int _arrivedSinceSample;
    private int _buffer;
    private int _batchInFlight;
    private int _sequence;
    private long _rows;
    private long _filtered;
    private long _updates;

    static DataFlow()
    {
        LabelProperty.OverrideDefaultValue<DataFlow>("Data flow illustration");
    }

    private IReadOnlyList<DataFlowSource> EffectiveSources
    {
        get
        {
            var sources = Sources is { Count: > 0 } list ? list : DefaultSources;
            return sources.Count > 4 ? sources.Take(4).ToArray() : sources;
        }
    }

    private protected override double SceneWidth => SceneW;

    private protected override double SceneHeight => SceneH;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourcesProperty || change.Property == LabelsProperty)
        {
            Rebuild();
        }
    }

    private protected override void BuildScene()
    {
        var labels = Labels;
        var sources = EffectiveSources;
        const double nodeWidth = 150;
        const double nodeHeight = 52;
        const double gap = 72;
        var centerY = SceneH / 2 - 8;
        var x0 = 14.0;
        var x1 = x0 + nodeWidth + gap;
        var x2 = x1 + nodeWidth + gap;
        var x3 = x2 + nodeWidth + gap;
        _sourceEdges.Clear();
        _stream = AddNode(new IllustrationNode("stream", new Rect(x1, centerY - nodeHeight / 2, nodeWidth, nodeHeight), ArcIcons.Layers,
            labels.Stream, labels.StreamDetail));
        _stream.Meter.Jump(0);
        var spacing = sources.Count switch { 1 => 0, 2 => 92, 3 => 74, _ => 60 };
        for (var i = 0; i < sources.Count; i++)
        {
            var y = centerY + (i - (sources.Count - 1) / 2.0) * spacing;
            var node = AddNode(new IllustrationNode($"source{i}", new Rect(x0, y - nodeHeight / 2, nodeWidth, nodeHeight),
                s_sourceIcons[i % s_sourceIcons.Length], sources[i].Title, sources[i].Detail));
            _sourceEdges.Add(AddEdge(new IllustrationEdge(node, _stream, IllustrationEdge.Curve(node.Right, _stream.Left), curved: true)));
        }
        _transform = AddNode(new IllustrationNode("transform", new Rect(x2, centerY - nodeHeight / 2, nodeWidth, nodeHeight),
            IllustrationGlyphs.Workflow, labels.Transform, labels.TransformDetail));
        _warehouse = AddNode(new IllustrationNode("warehouse", new Rect(x3, centerY - 66 - nodeHeight / 2, nodeWidth, nodeHeight),
            ArcIcons.Database, labels.Warehouse, labels.WarehouseDetail));
        _realtime = AddNode(new IllustrationNode("realtime", new Rect(x3, centerY + 56 - nodeHeight / 2, nodeWidth, nodeHeight),
            ArcIcons.Activity, labels.Realtime, labels.RealtimeDetail));
        _toTransform = AddEdge(new IllustrationEdge(_stream, _transform, IllustrationEdge.Curve(_stream.Right, _transform.Left), curved: true));
        _toWarehouse = AddEdge(new IllustrationEdge(_transform, _warehouse, IllustrationEdge.Curve(_transform.Right, _warehouse.Left), curved: true));
        _toRealtime = AddEdge(new IllustrationEdge(_transform, _realtime, IllustrationEdge.Curve(_transform.Right, _realtime.Left), curved: true));
        _rate = AddLabel(new IllustrationLabel(new Point(_stream.Center.X, _stream.Bounds.Bottom + 10), string.Empty) { Align = 0, IsMono = true });
        _rowsLabel = AddLabel(new IllustrationLabel(new Point(_warehouse.Center.X, _warehouse.Bounds.Bottom + 8), string.Empty) { Align = 0, IsMono = true });
        _updatesLabel = AddLabel(new IllustrationLabel(new Point(_realtime.Center.X, _realtime.Bounds.Bottom + 8), string.Empty) { Align = 0, IsMono = true });
    }

    private protected override void BuildScript(IllustrationScript script)
    {
        var labels = Labels;
        script.At(0, () =>
        {
            SetStep(0, labels.Ingest(EffectiveSources.Count));
            Highlight(0);
        });
        script.At(2.2, () =>
        {
            SetStep(1, labels.TransformStep);
            Highlight(1);
        });
        script.At(4.4, () =>
        {
            SetStep(2, labels.StoreStep);
            Highlight(2);
        });
        script.Length = 6.6;
    }

    /// <summary>The connections of the current step glow; the rest rest.</summary>
    private void Highlight(int step)
    {
        foreach (var edge in _sourceEdges)
        {
            edge.Active.Tween(step == 0 ? 0.7 : 0, Now, 0.4);
        }
        _toTransform.Active.Tween(step == 1 ? 0.7 : 0, Now, 0.4);
        _toWarehouse.Active.Tween(step == 2 ? 0.7 : 0, Now, 0.4);
        _toRealtime.Active.Tween(step == 2 ? 0.7 : 0, Now, 0.4);
        _stream.Emphasis.Tween(step == 0 ? 0.6 : 0, Now, 0.3);
        _transform.Emphasis.Tween(step == 1 ? 0.6 : 0, Now, 0.3);
        _warehouse.Emphasis.Tween(step == 2 ? 0.6 : 0, Now, 0.3);
    }

    private protected override void OnRestart()
    {
        _random = new IllustrationRandom(0x5EED);
        _spawnClock = _batchClock = _rateClock = 0;
        _nextSpawn = 0;
        _arrivalRate = EventsPerSecond;
        _arrivedSinceSample = 0;
        _buffer = 0;
        _batchInFlight = 0;
        _sequence = 0;
        _rows = 1240;
        _filtered = 0;
        _updates = 312;
        _stream.Meter.Jump(0);
        UpdateReadouts();
    }

    private protected override void ApplyPoster()
    {
        var labels = Labels;
        SetStep(0, labels.Summary);
        foreach (var edge in Edges)
        {
            edge.Active.Jump(0.55);
            // Still packets halfway along each connection show the direction of travel.
            Spawn(edge, 1, delay: -0.5);
        }
        _stream.Meter.Jump(0.4);
        _rate.Text = labels.EventsPerSecond((int)Math.Round(EventsPerSecond));
        _rowsLabel.Text = labels.Rows(12_480);
        _updatesLabel.Text = labels.Updates(3_120);
    }

    private protected override void OnTick(double dt)
    {
        // Sources: one event every 1/rate seconds, jittered so the stream does not look mechanical.
        _spawnClock += dt;
        while (_spawnClock >= _nextSpawn)
        {
            _spawnClock -= _nextSpawn;
            var interval = 1 / EventsPerSecond;
            _nextSpawn = interval * (0.5 + _random.NextDouble());
            var pick = _random.NextDouble();
            var index = 0;
            var weight = 0.5;
            while (index < _sourceEdges.Count - 1 && pick > weight)
            {
                index++;
                weight += (1 - weight) * 0.6;
            }
            Spawn(_sourceEdges[index], 0.9 + 0.2 * _random.NextDouble());
        }

        // The transform pulls a batch once a second.
        _batchClock += dt;
        if (_batchClock >= BatchInterval)
        {
            _batchClock -= BatchInterval;
            var take = Math.Min(_buffer, BatchSize);
            if (take > 0)
            {
                _buffer -= take;
                _batchInFlight += take;
                _transform.IsBusy = true;
                for (var k = 0; k < take; k++)
                {
                    Spawn(_toTransform, 0.55, delay: k * 0.06, tag: 1);
                }
                _stream.Meter.Tween(_buffer / (double)BufferCapacity, Now, 0.3);
            }
        }

        // Throughput: arrivals into the stream, smoothed, refreshed twice a second.
        _rateClock += dt;
        if (_rateClock >= 0.5)
        {
            _arrivalRate = _arrivalRate * 0.6 + _arrivedSinceSample / _rateClock * 0.4;
            _arrivedSinceSample = 0;
            _rateClock = 0;
            UpdateReadouts();
        }
    }

    private protected override void OnPacketArrived(IllustrationEdge edge, int tag, IllustrationTone tone)
    {
        if (ReferenceEquals(edge.To, _stream))
        {
            _arrivedSinceSample++;
            _buffer = Math.Min(BufferCapacity, _buffer + 1);
            _stream.Meter.Tween(_buffer / (double)BufferCapacity, Now, 0.2);
        }
        else if (ReferenceEquals(edge, _toTransform))
        {
            _batchInFlight = Math.Max(0, _batchInFlight - 1);
            _transform.IsBusy = _batchInFlight > 0;
            _sequence++;
            if (_random.NextDouble() < FilterRatio)
            {
                // Filtered: it heads for the warehouse and fades out red on the way.
                _filtered++;
                Spawn(_toWarehouse, 0.7, IllustrationTone.Danger, dropAt: 0.3);
                return;
            }
            Spawn(_toWarehouse, 0.7, tag: 2);
            if (_sequence % 3 == 0)
            {
                Spawn(_toRealtime, 0.7, IllustrationTone.Success, tag: 3);
            }
        }
        else if (ReferenceEquals(edge, _toWarehouse) && tag == 2)
        {
            // Readouts refresh with the throughput sample, not per event.
            _rows++;
        }
        else if (ReferenceEquals(edge, _toRealtime) && tag == 3)
        {
            _updates++;
        }
    }

    private void UpdateReadouts()
    {
        var labels = Labels;
        _rate.Text = labels.EventsPerSecond((int)Math.Round(_arrivalRate));
        _rowsLabel.Text = labels.Rows(_rows);
        _updatesLabel.Text = labels.Updates(_updates);
    }
}

/// <summary>A small deterministic generator (xorshift32): the same story plays the same way every time.</summary>
internal struct IllustrationRandom(uint seed)
{
    private uint _state = seed == 0 ? 0x9E3779B9u : seed;

    public double NextDouble()
    {
        var x = _state;
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        _state = x;
        return x / (double)uint.MaxValue;
    }
}
