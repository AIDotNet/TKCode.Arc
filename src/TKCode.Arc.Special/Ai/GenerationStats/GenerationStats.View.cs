using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Layout;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

public partial class GenerationStats
{
    /// <summary>The parts one layout (card or inline) has of its own.</summary>
    private sealed class Face
    {
        public GenerationStatsMark? Mark;
        public TextBlock? Name;
        public ArcContentSwap? Status;
        public GenerationStatsDetailsButton? Details;
        public GenerationStatsNumber? Ttft;
        public GenerationStatsNumber? Speed;
        public GenerationStatsNumber? Output;
        public GenerationStatsNumber? Cost;
        public string? StatusKey;
        public GenerationStatsGlyph? Glyph;
        public TextBlock? StatusText;
    }

    /// <summary>Everything shown, worked out once per refresh from the data (or the simulation) and the clock.</summary>
    internal readonly record struct Readout(
        GenerationStatus Status,
        GenerationModel? Model,
        GenerationUsage Usage,
        GenerationPricing? Pricing,
        string? Error,
        double? TtftMs,
        bool TtftLive,
        double? TotalMs,
        double? SpeedValue,
        double? CostUsd);

    private readonly Face _card = new();
    private readonly Face _inline = new();
    private readonly List<double> _samples = new();
    private Border? _frame;
    private GenerationStatsCells? _cells;
    private GenerationStatsCellLines? _cellLines;
    private Control? _ttftCell;
    private Control? _speedCell;
    private Control? _outputCell;
    private Control? _costCell;
    private TextBlock? _ttftLabel;
    private TextBlock? _speedLabel;
    private TextBlock? _outputLabel;
    private TextBlock? _costLabel;
    private GenerationStatsSparkline? _sparkline;
    private TextBlock? _inlineTtftUnit;
    private TextBlock? _inlineOutputUnit;
    private AiHeightBox? _errorBox;
    private TextBlock? _errorText;
    private AvaloniaButton? _retry;
    private AiHeightBox? _breakdownBox;
    private TextBlock? _latencyHeading;
    private GenerationStatsLatencyBar? _latencyBar;
    private TextBlock? _latencyFirst;
    private TextBlock? _latencyGeneration;
    private TextBlock? _latencyTotal;
    private TextBlock? _tableCaption;
    private StackPanel? _table;
    private readonly GenerationStatsTableRow[] _rows = new GenerationStatsTableRow[6];
    private CopyButton? _copy;
    private ContentPresenter? _note;
    private ExplainerAnnouncer? _announcer;

    private LiveLoop? _loop;
    private bool _narrow;
    private bool _compact;
    private bool _interacted;
    private bool _simulatedSaid;
    private bool _announcePosted;
    private string? _announcedKey;
    private int _runId;
    private GenerationStatus? _lastStatus;
    private long _lastOutput;
    private long _lastSampledOutput;
    private DateTimeOffset? _firstTokenAt;
    private double? _measuredSpeed;
    private double? _smoothed;
    private double _sampleInterval = GenerationStatsFormat.SampleInterval.TotalSeconds;
    private double _sinceSample;

    /// <summary>The clock (tests replace it); a simulation uses its own virtual clock.</summary>
    internal Func<DateTimeOffset>? Clock { get; set; }

    /// <summary>True when no status is set and a simulation is: the simulated runs play.</summary>
    internal bool IsSample => Status is null && Simulation is not null;

    internal DateTimeOffset Now => IsSample ? Simulation!.Now : Clock?.Invoke() ?? DateTimeOffset.Now;

    /// <summary>The status on screen: the app's, the simulation's, or idle.</summary>
    internal GenerationStatus DisplayedStatus => Status ?? Simulation?.Status ?? GenerationStatus.Idle;

    /// <summary>The speed readings the sparkline draws.</summary>
    internal IReadOnlyList<double> DisplayedSamples => SpeedSamples ?? _samples;

    internal AiHeightBox? ErrorBox => _errorBox;

    internal AiHeightBox? BreakdownBox => _breakdownBox;

    internal ExplainerAnnouncer? Announcer => _announcer;

    internal GenerationStatsDetailsButton? DetailsButton => Variant == GenerationStatsVariant.Inline ? _inline.Details : _card.Details;

    internal GenerationStatsNumber? TtftNumber => Active.Ttft;

    internal GenerationStatsNumber? SpeedNumber => Active.Speed;

    internal GenerationStatsNumber? OutputNumber => Active.Output;

    internal GenerationStatsNumber? CostNumber => Active.Cost;

    internal TextBlock? StatusText => Active.StatusText;

    internal GenerationStatsGlyph? StatusGlyph => Active.Glyph;

    internal GenerationStatsCells? Cells => _cells;

    internal IReadOnlyList<GenerationStatsTableRow> TableRows => _rows;

    internal CopyButton? Copy => _copy;

    internal Control?[] CellViews => [_ttftCell, _speedCell, _outputCell, _costCell];

    internal TextBlock? LatencyTotalText => _latencyTotal;

    private Face Active => Variant == GenerationStatsVariant.Inline ? _inline : _card;

    private void Hook(INameScope scope)
    {
        _frame = scope.Find<Border>(FramePart);
        _card.Mark = scope.Find<GenerationStatsMark>("PART_Mark");
        _card.Name = scope.Find<TextBlock>("PART_ModelName");
        _card.Status = scope.Find<ArcContentSwap>("PART_Status");
        _card.Details = scope.Find<GenerationStatsDetailsButton>(DetailsPart);
        _card.Ttft = scope.Find<GenerationStatsNumber>("PART_Ttft");
        _card.Speed = scope.Find<GenerationStatsNumber>("PART_Speed");
        _card.Output = scope.Find<GenerationStatsNumber>("PART_Output");
        _card.Cost = scope.Find<GenerationStatsNumber>("PART_Cost");
        _card.StatusKey = null;
        _inline.Mark = scope.Find<GenerationStatsMark>("PART_InlineMark");
        _inline.Name = scope.Find<TextBlock>("PART_InlineModel");
        _inline.Status = scope.Find<ArcContentSwap>("PART_InlineStatus");
        _inline.Details = scope.Find<GenerationStatsDetailsButton>(InlineDetailsPart);
        _inline.Ttft = scope.Find<GenerationStatsNumber>("PART_InlineTtft");
        _inline.Speed = scope.Find<GenerationStatsNumber>("PART_InlineSpeed");
        _inline.Output = scope.Find<GenerationStatsNumber>("PART_InlineOutput");
        _inline.Cost = scope.Find<GenerationStatsNumber>("PART_InlineCost");
        _inline.StatusKey = null;
        _cells = scope.Find<GenerationStatsCells>("PART_Cells");
        _cellLines = scope.Find<GenerationStatsCellLines>("PART_CellLines");
        _ttftCell = scope.Find<Control>("PART_TtftCell");
        _speedCell = scope.Find<Control>("PART_SpeedCell");
        _outputCell = scope.Find<Control>("PART_OutputCell");
        _costCell = scope.Find<Control>("PART_CostCell");
        _ttftLabel = scope.Find<TextBlock>("PART_TtftLabel");
        _speedLabel = scope.Find<TextBlock>("PART_SpeedLabel");
        _outputLabel = scope.Find<TextBlock>("PART_OutputLabel");
        _costLabel = scope.Find<TextBlock>("PART_CostLabel");
        _sparkline = scope.Find<GenerationStatsSparkline>("PART_Sparkline");
        _inlineTtftUnit = scope.Find<TextBlock>("PART_InlineTtftUnit");
        _inlineOutputUnit = scope.Find<TextBlock>("PART_InlineOutputUnit");
        _errorBox = scope.Find<AiHeightBox>(ErrorBoxPart);
        _errorText = scope.Find<TextBlock>("PART_ErrorText");
        _retry = scope.Find<AvaloniaButton>(RetryPart);
        _breakdownBox = scope.Find<AiHeightBox>(BreakdownBoxPart);
        _latencyHeading = scope.Find<TextBlock>("PART_LatencyHeading");
        _latencyBar = scope.Find<GenerationStatsLatencyBar>("PART_LatencyBar");
        _latencyFirst = scope.Find<TextBlock>("PART_LatencyFirst");
        _latencyGeneration = scope.Find<TextBlock>("PART_LatencyGeneration");
        _latencyTotal = scope.Find<TextBlock>("PART_LatencyTotal");
        _tableCaption = scope.Find<TextBlock>("PART_TableCaption");
        _table = scope.Find<StackPanel>("PART_Table");
        _copy = scope.Find<CopyButton>(CopyPart);
        _note = scope.Find<ContentPresenter>("PART_Note");
        _announcer = scope.Find<ExplainerAnnouncer>(AnnouncerPart);

        foreach (var details in new[] { _card.Details, _inline.Details })
        {
            if (details is not null)
            {
                details.Click += OnDetailsClick;
            }
        }
        if (_retry is not null)
        {
            _retry.Click += OnRetryClick;
        }
        if (_copy is not null)
        {
            _copy.Click += OnCopyClick;
        }
        if (_table is not null)
        {
            _table.Children.Clear();
            for (var i = 0; i < _rows.Length; i++)
            {
                _rows[i] = new GenerationStatsTableRow();
                _rows[i].SetKind(header: i == 0, total: i == _rows.Length - 1, showsRate: true);
                _table.Children.Add(_rows[i]);
            }
        }
        if (_errorBox is not null)
        {
            _errorBox.HeightSpring = AiGlyphMotion.Settle;
            SetInstantly(_errorBox, DisplayedStatus == GenerationStatus.Error);
        }
        if (_breakdownBox is not null)
        {
            _breakdownBox.HeightSpring = AiGlyphMotion.Settle;
            SetInstantly(_breakdownBox, IsExpanded);
        }
    }

    private void Unhook()
    {
        foreach (var details in new[] { _card.Details, _inline.Details })
        {
            if (details is not null)
            {
                details.Click -= OnDetailsClick;
            }
        }
        if (_retry is not null)
        {
            _retry.Click -= OnRetryClick;
        }
        if (_copy is not null)
        {
            _copy.Click -= OnCopyClick;
        }
    }

    /// <summary>
    /// The one frame loop: it advances the simulation (full motion only, in virtual time, only while on screen), counts
    /// the live clock while waiting or streaming, and reads the speed for the sparkline every 160ms while streaming.
    /// </summary>
    private void UpdateLoop()
    {
        var status = DisplayedStatus;
        var live = status is GenerationStatus.Waiting or GenerationStatus.Streaming;
        var wanted = IsAttachedToVisualTreeSafe && (IsSample ? ArcMotion.IsFull(this) : live);
        if (wanted)
        {
            _loop ??= new LiveLoop(this, dt =>
            {
                Step(dt);
                return true;
            })
            { MinimumLevel = ArcMotionLevel.Off };
            _loop.Start();
        }
        else
        {
            _loop?.Stop();
        }
    }

    private bool IsAttachedToVisualTreeSafe => TopLevel.GetTopLevel(this) is not null;

    /// <summary>One frame of <paramref name="seconds"/>: the simulation, the live clock and the speed reading.</summary>
    internal void Step(double seconds)
    {
        if (IsSample)
        {
            if (ArcMotion.IsFull(this))
            {
                Simulation!.Advance(seconds);
            }
        }
        var status = DisplayedStatus;
        if (status == GenerationStatus.Streaming && SpeedSamples is null)
        {
            _sinceSample += seconds;
            if (_sinceSample >= _sampleInterval)
            {
                _sinceSample -= _sampleInterval;
                SampleSpeed();
            }
        }
        if (status is GenerationStatus.Waiting or GenerationStatus.Streaming)
        {
            RefreshLive();
        }
        else if (!IsSample)
        {
            _loop?.Stop();
        }
    }

    /// <summary>Adds a smoothed speed reading when the output grew since the last one.</summary>
    internal void SampleSpeed()
    {
        if (_measuredSpeed is not { } measured || _lastOutput <= _lastSampledOutput)
        {
            return;
        }
        _lastSampledOutput = _lastOutput;
        _smoothed = GenerationStatsFormat.Smooth(_smoothed, measured);
        if (GenerationStatsFormat.AddSample(_samples, _smoothed.Value))
        {
            _sampleInterval *= 2;
        }
        _sparkline?.SetSamples(DisplayedSamples);
    }

    private void ResetRun()
    {
        _runId++;
        _samples.Clear();
        _smoothed = null;
        _measuredSpeed = null;
        _firstTokenAt = null;
        _lastOutput = 0;
        _lastSampledOutput = 0;
        _sampleInterval = GenerationStatsFormat.SampleInterval.TotalSeconds;
        _sinceSample = 0;
        _announcedKey = null;
        foreach (var number in new[] { _card.Ttft, _card.Speed, _card.Output, _card.Cost, _inline.Ttft, _inline.Speed, _inline.Output, _inline.Cost })
        {
            number?.Release();
        }
        _sparkline?.SetSamples(DisplayedSamples);
    }

    private GenerationUsage DisplayedUsage => IsSample ? Simulation!.Usage : Usage ?? s_noUsage;

    private static readonly GenerationUsage s_noUsage = new();

    /// <summary>
    /// The data changed (a new usage, status or start, or a simulated frame): a new request clears the run; a chunk measures
    /// the speed (tokens since the first token over the time since it, so the speed holds still between chunks).
    /// </summary>
    private void OnDataChanged()
    {
        var status = DisplayedStatus;
        var usage = DisplayedUsage;
        var output = usage.OutputTokens ?? 0;
        var restarted = status is GenerationStatus.Waiting or GenerationStatus.Idle
                        && _lastStatus is not (GenerationStatus.Waiting or GenerationStatus.Idle or null);
        if (restarted || output < _lastOutput)
        {
            ResetRun();
        }
        _lastStatus = status;
        if (status == GenerationStatus.Streaming)
        {
            var started = IsSample ? Simulation!.StartedAt : StartedAt;
            _firstTokenAt ??= started is { } s && usage.Ttft is { } ttft ? s + ttft : Now;
            if (output > _lastOutput)
            {
                var seconds = (Now - _firstTokenAt.Value).TotalSeconds;
                if (seconds > 0.05)
                {
                    _measuredSpeed = output / seconds;
                }
            }
        }
        _lastOutput = output;
        Refresh(instant: false);
        UpdateLoop();
    }

    /// <summary>Works out what is shown now.</summary>
    internal Readout Read()
    {
        var sample = IsSample;
        var status = DisplayedStatus;
        var usage = DisplayedUsage;
        var simulation = sample ? Simulation : null;
        var pricing = simulation is not null ? simulation.Run.Pricing : Pricing;
        var started = simulation is not null ? simulation.StartedAt : StartedAt;
        var error = simulation is not null ? simulation.Error : ErrorText;
        var model = simulation is not null ? simulation.Run.Model : Model;
        double? elapsed = started is { } s ? Math.Max(0, (Now - s).TotalMilliseconds) : null;
        var live = status is GenerationStatus.Waiting or GenerationStatus.Streaming;
        var ttft = usage.Ttft?.TotalMilliseconds;
        var ttftLive = false;
        if (ttft is null && status == GenerationStatus.Waiting && elapsed is not null)
        {
            ttft = elapsed;
            ttftLive = true;
        }
        var total = usage.Duration?.TotalMilliseconds ?? (live ? elapsed : null);
        double? speed = usage.TokensPerSecond;
        if (speed is null)
        {
            speed = status switch
            {
                GenerationStatus.Streaming => _measuredSpeed,
                GenerationStatus.Done or GenerationStatus.Stopped or GenerationStatus.Error =>
                    GenerationStatsFormat.DeriveSpeed(usage.OutputTokens, ttft, usage.Duration?.TotalMilliseconds) ?? _measuredSpeed,
                _ => null,
            };
        }
        var cost = GenerationStatsFormat.TotalCost(usage, pricing);
        return new Readout(status, model, usage, pricing, error, ttft, ttftLive, total, speed, cost);
    }

    /// <summary>The per-frame part: the live time to first token and, when open, the latency.</summary>
    private void RefreshLive()
    {
        if (_frame is null)
        {
            return;
        }
        var readout = Read();
        if (readout.TtftLive)
        {
            var number = GenerationStatsFormat.Latency(readout.TtftMs!.Value);
            Active.Ttft?.Show(number, Labels.NoValue, live: true);
            UpdateCellNames(readout);
        }
        if (_breakdownBox?.IsExpanded == true)
        {
            UpdateLatency(readout);
        }
    }

    /// <summary>Brings every part in line with the data; a part that already shows the value is left alone.</summary>
    private void Refresh(bool instant)
    {
        if (_frame is null)
        {
            return;
        }
        var labels = Labels;
        var readout = Read();
        var face = Active;
        var modelName = readout.Model is { Name.Length: > 0 } m ? m.Name : labels.UnknownModel;
        PseudoClasses.Set(ErrorPseudoClass, readout.Status == GenerationStatus.Error);

        if (face.Name is not null)
        {
            face.Name.Text = modelName;
        }
        if (face.Mark is not null)
        {
            face.Mark.Glyph = readout.Model?.Mark;
            face.Mark.GlyphBrush = readout.Model?.MarkBrush;
            face.Mark.Letter = GenerationStatsMark.LetterOf(modelName);
        }
        UpdateStatus(face, readout, instant);
        foreach (var details in new[] { _card.Details, _inline.Details })
        {
            if (details is null)
            {
                continue;
            }
            details.Text = labels.Details;
            details.IsExpanded = IsExpanded;
            details.IsCompact = _compact;
            AutomationProperties.SetName(details, IsExpanded ? labels.HideDetails : labels.ShowDetails);
        }

        // The four values.
        face.Ttft?.Show(readout.TtftMs is { } ttft ? GenerationStatsFormat.Latency(ttft) : null, labels.NoValue, readout.TtftLive, instant);
        face.Speed?.Show(readout.SpeedValue is { } speed ? GenerationStatsFormat.Speed(speed, labels.SpeedUnit) : null,
            labels.NoValue, instant: instant);
        face.Output?.Show(readout.Usage.OutputTokens is { } output ? GenerationStatsFormat.Tokens(output, null) : null,
            labels.NoValue, instant: instant);
        face.Cost?.Show(readout.CostUsd is { } cost ? GenerationStatsFormat.Cost(cost, labels.Currency) : null,
            labels.NoValue, instant: instant);
        if (_ttftLabel is not null)
        {
            _ttftLabel.Text = labels.FirstToken;
            _speedLabel!.Text = labels.Speed;
            _outputLabel!.Text = labels.Output;
            _costLabel!.Text = labels.Cost;
        }
        if (_inlineTtftUnit is not null)
        {
            _inlineTtftUnit.Text = labels.ToFirstToken;
        }
        if (_inlineOutputUnit is not null)
        {
            _inlineOutputUnit.Text = labels.TokensUnit;
        }
        UpdateCellNames(readout);
        _sparkline?.SetSamples(DisplayedSamples);
        if (_cells is not null)
        {
            _cells.IsGrid = _narrow;
        }
        if (_cellLines is not null)
        {
            _cellLines.IsGrid = _narrow;
        }

        // The error row.
        var failed = readout.Status == GenerationStatus.Error;
        if (_errorText is not null)
        {
            _errorText.Text = string.IsNullOrWhiteSpace(readout.Error) ? labels.DefaultError : readout.Error;
        }
        if (_retry is not null)
        {
            _retry.Content = labels.Retry;
            _retry.IsVisible = AllowsRetry;
        }
        Apply(_errorBox, failed, instant);

        // The breakdown.
        if (_note is not null)
        {
            _note.Content = Note ?? (IsSample ? labels.SimulatedNote : null);
            _note.IsVisible = _note.Content is not null;
        }
        if (IsExpanded)
        {
            UpdateBreakdown(readout);
        }
        Apply(_breakdownBox, IsExpanded, instant);

        AutomationProperties.SetName(this, labels.Group(modelName));
        if (_frame is not null)
        {
            AutomationProperties.SetName(_frame, labels.Group(modelName));
        }
        ScheduleAnnouncement();
    }

    private static void Apply(AiHeightBox? box, bool open, bool instant)
    {
        if (box is null || box.IsExpanded == open)
        {
            return;
        }
        if (instant)
        {
            SetInstantly(box, open);
        }
        else
        {
            box.IsExpanded = open;
        }
    }

    private static void SetInstantly(AiHeightBox box, bool open)
    {
        if (open)
        {
            box.OpenInstantly();
        }
        else
        {
            box.CloseInstantly();
        }
    }

    /// <summary>The text of a status: "Waiting", "Done in 4.18s", "Stopped at 1.90s", "Failed".</summary>
    internal static string StatusLabel(GenerationStatsLabels labels, GenerationStatus status, double? totalMs) => status switch
    {
        GenerationStatus.Waiting => labels.Waiting,
        GenerationStatus.Streaming => labels.Streaming,
        GenerationStatus.Done => totalMs is { } t ? labels.DoneIn(GenerationStatsFormat.LatencyText(t)) : labels.Done,
        GenerationStatus.Stopped => totalMs is { } s ? labels.StoppedAt(GenerationStatsFormat.LatencyText(s)) : labels.Stopped,
        GenerationStatus.Error => labels.Failed,
        _ => labels.Idle,
    };

    /// <summary>
    /// The status (icon and word) is swapped as one with a single crossfade (out in 100ms, in over 160ms rising 2px) when it
    /// changes; the done check draws itself once.
    /// </summary>
    private void UpdateStatus(Face face, Readout readout, bool instant)
    {
        if (face.Status is null)
        {
            return;
        }
        var settled = readout.Status is GenerationStatus.Done or GenerationStatus.Stopped;
        var text = StatusLabel(Labels, readout.Status, settled ? readout.Usage.Duration?.TotalMilliseconds : null);
        var key = readout.Status + "|" + text;
        if (face.StatusKey == key)
        {
            return;
        }
        var drawsCheck = readout.Status == GenerationStatus.Done && !instant && face.StatusKey is not null
                         && !face.StatusKey.StartsWith(nameof(GenerationStatus.Done), StringComparison.Ordinal);
        face.StatusKey = key;
        var glyph = new GenerationStatsGlyph { Status = readout.Status, VerticalAlignment = VerticalAlignment.Center };
        var label = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center };
        if (readout.Status == GenerationStatus.Error)
        {
            ArcTokens.Bind(label, TextBlock.ForegroundProperty, ArcDesignTokenKind.Danger);
        }
        var view = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { glyph, label } };
        face.Glyph = glyph;
        face.StatusText = label;
        face.Status.Content = view;
        if (drawsCheck)
        {
            Dispatcher.UIThread.Post(glyph.PlayCheck, DispatcherPriority.Loaded);
        }
    }

    private void UpdateCellNames(Readout readout)
    {
        var labels = Labels;
        var face = Active;
        SetCellName(_ttftCell, labels.FirstToken, face.Ttft);
        SetCellName(_speedCell, labels.Speed, face.Speed);
        SetCellName(_outputCell, labels.Output, face.Output);
        SetCellName(_costCell, labels.Cost, face.Cost);

        void SetCellName(Control? cell, string label, GenerationStatsNumber? number)
        {
            if (cell is null)
            {
                return;
            }
            var value = number?.Target is { } target ? target.Text : labels.NoValue;
            AutomationProperties.SetName(cell, labels.Cell(label, value));
        }
    }

    private void UpdateLatency(Readout readout)
    {
        var labels = Labels;
        var ttft = readout.TtftMs;
        var total = readout.TotalMs;
        double? generation = ttft is { } f && total is { } t && !readout.TtftLive ? Math.Max(0, t - f) : null;
        string Text(double? ms) => ms is { } v ? GenerationStatsFormat.LatencyText(v) : labels.NoValue;
        if (_latencyHeading is not null)
        {
            _latencyHeading.Text = labels.Latency;
        }
        SetText(_latencyFirst, labels.LatencyFirstToken + " " + Text(ttft));
        SetText(_latencyGeneration, labels.Generation + " " + Text(generation));
        SetText(_latencyTotal, labels.Total + " " + Text(total));
        _latencyBar?.SetTimes(ttft, readout.TtftLive ? ttft : total);
    }

    private static void SetText(TextBlock? block, string text)
    {
        if (block is not null && block.Text != text)
        {
            block.Text = text;
        }
    }

    /// <summary>The breakdown: the latency, the token table with rates and costs, and the copied JSON.</summary>
    private void UpdateBreakdown(Readout readout)
    {
        var labels = Labels;
        UpdateLatency(readout);
        if (_tableCaption is not null)
        {
            _tableCaption.Text = labels.TokenUsage;
        }
        var usage = readout.Usage;
        var pricing = readout.Pricing;
        var costs = GenerationStatsFormat.CostRows(usage, pricing, usage.CostUsd);
        var showsRate = pricing is not null && !_compact;
        var cached = usage.CachedTokens;
        long? uncached = usage.InputTokens is { } all ? Math.Max(0, all - (cached ?? 0)) : null;
        string Tokens(long? n) => n is { } v ? GenerationStatsFormat.TokensText(v) : labels.NoValue;
        string Cost(double? c) => c is { } v ? GenerationStatsFormat.CostText(v, labels.Currency) : labels.NoValue;
        string Rate(double? r) => r is { } v ? labels.Rate(GenerationStatsFormat.PriceText(v, labels.Currency)) : labels.NoValue;
        string Spoken(double? r) => r is { } v ? labels.RateSpoken(GenerationStatsFormat.PriceText(v, labels.Currency)) : labels.NoValue;
        SetRow(0, labels.TypeColumn, labels.TokensColumn, labels.RateColumn, labels.CostColumn, null);
        SetRow(1, labels.InputRow, Tokens(uncached), Rate(pricing?.Input), Cost(costs.Input), Spoken(pricing?.Input));
        SetRow(2, labels.CachedRow, Tokens(cached), Rate(pricing is null ? null : pricing.CachedInput ?? pricing.Input), Cost(costs.Cached),
            Spoken(pricing is null ? null : pricing.CachedInput ?? pricing.Input));
        SetRow(3, labels.OutputRow, Tokens(usage.OutputTokens), Rate(pricing?.Output), Cost(costs.Output), Spoken(pricing?.Output));
        SetRow(4, labels.ReasoningRow, Tokens(usage.ReasoningTokens), Rate(pricing?.Output), Cost(costs.Reasoning), Spoken(pricing?.Output));
        SetRow(5, labels.Total, Tokens(GenerationStatsFormat.TotalTokens(usage)), string.Empty, Cost(readout.CostUsd), null);
        if (_table is not null)
        {
            AutomationProperties.SetName(_table, labels.TokenUsage);
        }
        if (_copy is not null)
        {
            var json = GenerationStatsFormat.Json(readout.Model, readout.Status, usage, pricing, readout.TtftMs,
                usage.Duration?.TotalMilliseconds, readout.SpeedValue, readout.CostUsd, readout.Error);
            if (_copy.Value != json)
            {
                _copy.Value = json;
            }
            AutomationProperties.SetName(_copy, labels.CopyJson);
        }

        void SetRow(int index, string label, string tokens, string rate, string cost, string? spokenRate)
        {
            if (_rows[index] is not { } row)
            {
                return;
            }
            row.SetKind(header: index == 0, total: index == _rows.Length - 1, showsRate);
            row.Label = label;
            row.Tokens = tokens;
            row.Rate = rate;
            row.Cost = cost;
            var name = index == 0
                ? labels.TokenUsage
                : $"{label}: {labels.TokensColumn} {tokens}" + (showsRate && spokenRate is not null ? $", {labels.RateColumn} {spokenRate}" : string.Empty)
                  + $", {labels.CostColumn} {cost}";
            AutomationProperties.SetName(row, name);
        }
    }

    /// <summary>
    /// Settled outcomes (done, stopped, failed) are said once per run through the polite region, a moment after the data
    /// arrives so a status and its duration set one after the other are said together; never the live counts. The sample
    /// runs stay quiet until someone interacts with the strip.
    /// </summary>
    private void ScheduleAnnouncement()
    {
        if (_announcePosted)
        {
            return;
        }
        _announcePosted = true;
        Dispatcher.UIThread.Post(() =>
        {
            _announcePosted = false;
            AnnounceNow();
        }, DispatcherPriority.Background);
    }

    internal void AnnounceNow()
    {
        var readout = Read();
        if (readout.Status is not (GenerationStatus.Done or GenerationStatus.Stopped or GenerationStatus.Error))
        {
            return;
        }
        var key = _runId + "|" + readout.Status;
        if (_announcedKey == key)
        {
            return;
        }
        _announcedKey = key;
        if (IsSample && !_interacted)
        {
            return;
        }
        var labels = Labels;
        var model = readout.Model is { Name.Length: > 0 } m ? m.Name : labels.UnknownModel;
        var time = readout.Usage.Duration?.TotalMilliseconds is { } total ? GenerationStatsFormat.LatencyText(total) : null;
        var text = readout.Status switch
        {
            GenerationStatus.Done => labels.DoneAnnouncement(model, time,
                readout.Usage.OutputTokens is { } o ? GenerationStatsFormat.TokensText(o) : null,
                readout.SpeedValue is { } s ? GenerationStatsFormat.SpeedText(s) : null,
                readout.CostUsd is { } c ? GenerationStatsFormat.CostText(c, labels.Currency) : null),
            GenerationStatus.Stopped => labels.StoppedAnnouncement(model, time),
            _ => labels.FailedAnnouncement(model, string.IsNullOrWhiteSpace(readout.Error) ? labels.DefaultError : readout.Error!),
        };
        _announcer?.Announce(text);
    }
}
