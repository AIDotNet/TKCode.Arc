using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>The run: analyzing with the scan line, writing at Arc's streaming cadence, finishing, stopping, failing, and the simulation's loop.</summary>
public partial class DesignToCode
{
    /// <summary>How long the scan line takes to cross the screenshot.</summary>
    internal const double SweepSeconds = 1.0;

    /// <summary>Under reduced motion the code arrives a part (a region) at a time, one part this often.</summary>
    internal const double PartSeconds = 0.3;

    /// <summary>How fast the simulation's "model" produces code (characters a second, a little ahead of the reading pace).</summary>
    internal const double SimulationCharsPerSecond = 640;

    /// <summary>The simulation's loop: hold on the finished code, then on the preview, before starting again.</summary>
    internal const double HoldCodeSeconds = 2.4;

    internal const double HoldPreviewSeconds = 4.2;

    /// <summary>When, on the preview, the loop calls the simulation's <see cref="DesignToCodeSimulation.PreviewAction"/>.</summary>
    internal const double PreviewActionSeconds = 1.4;

    private readonly LiveLoop _loop;
    private readonly List<DesignToCodeRegion> _regions = [];
    private readonly StringBuilder _received = new();
    private readonly List<(int Start, string? Region)> _marks = [];
    private readonly WordPacer _pacer = new();
    private CancellationTokenSource? _cts;
    private int _run;
    private double _now;
    private double _runStart;
    private bool _simulated;
    private bool _partMode;
    private bool _fullMotion;
    private bool _generatorDone;
    private int _shown;
    private double _partClock;
    private double _simulationFed;
    private int _doneLines;
    private TimeSpan _doneElapsed;
    private bool _following = true;
    private DesignToCodeSource? _runSource;
    private AutoStage _autoStage;
    private double _autoClock;
    private bool _autoStopped;
    private bool _previewActed;
    private bool _previewFromFactory;
    private DesignToCodeSimulation? _runSimulation;
    private Bitmap? _ownedBitmap;

    private enum AutoStage
    {
        Idle,
        Running,
        HoldCode,
        HoldPreview,
        Finished
    }

    /// <summary>Whether a run is analyzing or writing.</summary>
    internal bool IsRunning => Phase is DesignToCodePhase.Analyzing or DesignToCodePhase.Writing;

    /// <summary>Whether the simulation's loop has been stopped by the person.</summary>
    internal bool IsAutoPlayStopped => _autoStopped;

    /// <summary>Whether the current run plays the <see cref="Simulation"/>.</summary>
    internal bool IsSimulated => _simulated;

    /// <summary>
    /// Starts a run on the design shown: the scan line crosses it, regions appear, then code streams in. A run in progress
    /// is replaced. Does nothing without a design, or without a generator or a simulation to play.
    /// </summary>
    public void Generate()
    {
        if (EffectiveSource is not { } source || Generator is null && Simulation is null)
        {
            return;
        }
        CancelRun();
        ClearRunState();
        _run++;
        _cts = new CancellationTokenSource();
        _runSource = source;
        _runStart = _now;
        _simulated = Generator is null;
        _runSimulation = _simulated ? Simulation : null;
        var level = ArcMotion.GetEffectiveLevel(this);
        _fullMotion = level == ArcMotionLevel.Full;
        _partMode = !_fullMotion;
        ErrorMessage = null;
        Phase = DesignToCodePhase.Analyzing;
        if (View != DesignToCodeView.Code)
        {
            SetCurrentValue(ViewProperty, DesignToCodeView.Code);
        }
        _following = true;
        Announce(_simulated ? Labels.AnnounceAnalyzing + " " + Labels.SimulationNote : Labels.AnnounceAnalyzing);
        if (_simulated)
        {
            foreach (var region in _runSimulation?.Regions ?? [])
            {
                if (region is not null)
                {
                    AcceptRegion(_run, region);
                }
            }
        }
        else if (Generator is { } generator)
        {
            _ = RunGeneratorAsync(generator, source, _run, _cts.Token);
        }
        UpdateScan();
        RefreshAll();
        UpdateLoop();
    }

    /// <summary>Stops a run that is analyzing or writing; the code written so far is kept.</summary>
    public void Stop()
    {
        if (!IsRunning)
        {
            return;
        }
        CancelRun();
        _canvas?.RevealAll(animate: false);
        EndWriting();
        Phase = DesignToCodePhase.Stopped;
        Announce(Labels.AnnounceStopped);
        RefreshAll();
        UpdateLoop();
    }

    /// <summary>Shows the <see cref="Simulation"/>'s design. Does nothing without one.</summary>
    public void UseSample()
    {
        if (Simulation?.Source is not { } sample)
        {
            return;
        }
        StopAutoPlay();
        SetCurrentValue(SourceProperty, sample);
        Announce(Labels.AnnounceSource(sample.Name));
    }

    private async Task RunGeneratorAsync(Func<DesignToCodeSource, DesignToCodeContext, Task> generator, DesignToCodeSource source, int run,
        CancellationToken token)
    {
        try
        {
            await generator(source, new DesignToCodeContext(this, run, token));
            if (run == _run && IsRunning)
            {
                _generatorDone = true;
                _pacer.Complete();
                UpdateLoop();
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Stopped or replaced: the card has already moved on.
        }
        catch (Exception exception)
        {
            if (run == _run && IsRunning)
            {
                Fail(exception.Message);
            }
        }
    }

    /// <summary>A region from the generator (or the simulation) for run <paramref name="run"/>.</summary>
    internal void AcceptRegion(int run, DesignToCodeRegion region)
    {
        if (run != _run || !IsRunning || string.IsNullOrEmpty(region.Id))
        {
            return;
        }
        var index = _regions.FindIndex(r => r.Id == region.Id);
        if (index >= 0)
        {
            _regions[index] = region;
        }
        else
        {
            _regions.Add(region);
        }
        if (_canvas is not null)
        {
            var view = _canvas.SetRegion(region);
            if (Phase == DesignToCodePhase.Writing || !_fullMotion || _now - _runStart >= SweepSeconds)
            {
                _canvas.Reveal(view, animate: _fullMotion);
            }
        }
        _codeView?.RefreshRegions();
        RefreshStatus();
    }

    /// <summary>A chunk of code from the generator (or the simulation) for run <paramref name="run"/>.</summary>
    internal void AcceptChunk(int run, string chunk, string? regionId)
    {
        if (run != _run || !IsRunning)
        {
            return;
        }
        if (_marks.Count == 0 || _marks[^1].Region != regionId)
        {
            _marks.Add((_received.Length, regionId));
        }
        _received.Append(chunk);
        if (!_partMode)
        {
            _pacer.Push(chunk);
        }
        UpdateLoop();
    }

    /// <summary>The region the character at <paramref name="offset"/> implements.</summary>
    internal string? RegionAt(int offset)
    {
        string? region = null;
        foreach (var mark in _marks)
        {
            if (mark.Start > offset)
            {
                break;
            }
            region = mark.Region;
        }
        return region;
    }

    /// <summary>Runs <paramref name="seconds"/> of frames by hand, one 60th of a second at a time (tests).</summary>
    internal void Advance(double seconds)
    {
        const double frame = 1.0 / 60;
        while (seconds > 1e-9)
        {
            var dt = Math.Min(frame, seconds);
            Step(dt);
            seconds -= dt;
        }
    }

    private bool OnFrame(double dt)
    {
        if (!UsesManualTime)
        {
            Step(dt);
        }
        return WantsFrames();
    }

    /// <summary>When set, frames do not move the run's clock; only <see cref="Advance"/> does (tests).</summary>
    internal bool UsesManualTime { get; set; }

    /// <summary>One frame: the simulation feeds code, the scan line moves, the pacer releases words, the sample loop moves on.</summary>
    internal void Step(double dt)
    {
        dt = Math.Max(0, dt);
        _now += dt;
        if (IsRunning)
        {
            var t = _now - _runStart;
            if (_simulated)
            {
                FeedSimulation(t);
            }
            if (Phase == DesignToCodePhase.Analyzing)
            {
                UpdateScan();
                if (_received.Length > 0 && (t >= SweepSeconds || !_fullMotion))
                {
                    StartWriting();
                }
            }
            if (Phase == DesignToCodePhase.Writing)
            {
                Release(dt);
                if (_generatorDone && _shown >= _received.Length)
                {
                    Finish();
                }
            }
        }
        StepAutoPlay(dt);
    }

    private void FeedSimulation(double t)
    {
        if (_generatorDone)
        {
            return;
        }
        var parts = _runSimulation?.Parts ?? [];
        var total = _runSimulation?.Code.Length ?? 0;
        var target = (int)Math.Min(total, Math.Max(0, t - SweepSeconds) * SimulationCharsPerSecond);
        var fed = (int)_simulationFed;
        if (target <= fed)
        {
            if (target >= total)
            {
                _generatorDone = true;
                _pacer.Complete();
            }
            return;
        }
        // Feed what the "model" has produced so far, tagged with the part it belongs to.
        var offset = 0;
        foreach (var part in parts)
        {
            if (part is null)
            {
                continue;
            }
            var (text, region) = (part.Text, part.RegionId);
            var from = Math.Max(fed, offset);
            var to = Math.Min(target, offset + text.Length);
            if (to > from)
            {
                AcceptChunk(_run, text.Substring(from - offset, to - from), region);
            }
            offset += text.Length;
        }
        _simulationFed = target;
        if (target >= total)
        {
            _generatorDone = true;
            _pacer.Complete();
        }
    }

    /// <summary>The scan line crosses the screenshot once a second; each region shows as the line reaches its top.</summary>
    private void UpdateScan()
    {
        if (_canvas is null)
        {
            return;
        }
        if (Phase != DesignToCodePhase.Analyzing || !_fullMotion)
        {
            _canvas.ScanPosition = double.NaN;
            if (Phase == DesignToCodePhase.Analyzing)
            {
                _canvas.RevealAll(animate: false);
            }
            return;
        }
        var t = _now - _runStart;
        var position = t % SweepSeconds / SweepSeconds;
        _canvas.ScanPosition = position;
        foreach (var view in _canvas.Views)
        {
            if (!view.IsRevealed && (t >= SweepSeconds || view.Region.Fraction.Y <= position))
            {
                _canvas.Reveal(view, animate: true);
            }
        }
    }

    private void StartWriting()
    {
        Phase = DesignToCodePhase.Writing;
        if (_canvas is not null)
        {
            _canvas.ScanPosition = double.NaN;
            _canvas.RevealAll(_fullMotion);
        }
        var labels = _regions.Select(r => r.Label).ToList();
        Announce(Labels.AnnounceRegions(labels));
        RefreshAll();
    }

    /// <summary>
    /// Releases written code: whole words at Arc's streaming cadence (3 to 8 words every 34ms, each batch fading in), or
    /// under reduced motion one part at a time with no fade.
    /// </summary>
    private void Release(double dt)
    {
        var before = _shown;
        if (_partMode)
        {
            _partClock += dt;
            if (_partClock >= PartSeconds || _shown == 0)
            {
                var next = -1;
                foreach (var mark in _marks)
                {
                    if (mark.Start > _shown)
                    {
                        next = mark.Start;
                        break;
                    }
                }
                if (next > 0)
                {
                    _shown = next;
                }
                else if (_generatorDone)
                {
                    _shown = _received.Length;
                }
                if (_shown != before)
                {
                    _partClock = 0;
                }
            }
        }
        else if (_pacer.Tick(TimeSpan.FromSeconds(_now)) is { } batch)
        {
            _shown = Math.Min(_received.Length, _shown + batch.Length);
        }
        if (_shown != before)
        {
            ShowCode(fade: _fullMotion);
        }
    }

    /// <summary>Puts the released code on screen and moves the "being written" highlight to the region it reached.</summary>
    private void ShowCode(bool fade)
    {
        var code = _received.ToString(0, _shown);
        Code = code;
        _codeView?.SetCode(code, RegionAt, fade);
        var writing = _shown > 0 ? RegionAt(_shown - 1) : null;
        if (_canvas is not null)
        {
            _canvas.Active = writing;
        }
        if (_codeView is not null)
        {
            _codeView.WritingRegion = writing;
        }
        RefreshAll();
    }

    private void Finish()
    {
        _cts?.Dispose();
        _cts = null;
        EndWriting();
        var code = Code;
        _doneLines = DesignToCodeFormat.LineCount(code);
        _doneElapsed = TimeSpan.FromSeconds(_now - _runStart);
        Phase = DesignToCodePhase.Done;
        Announce(Labels.AnnounceDone(_doneLines, _doneElapsed));
        RefreshAll();
        if (_autoStage == AutoStage.Running)
        {
            _autoStage = AutoStage.HoldCode;
            _autoClock = 0;
        }
        UpdateLoop();
        RaiseEvent(new DesignToCodeCompletedEventArgs(CompletedEvent, code, _runSource ?? new DesignToCodeSource(), _doneLines, _doneElapsed));
    }

    private void Fail(string reason)
    {
        CancelRun();
        _canvas?.RevealAll(animate: false);
        EndWriting();
        ErrorMessage = Labels.Failed(reason);
        Phase = DesignToCodePhase.Error;
        Announce(ErrorMessage);
        RefreshAll();
        UpdateLoop();
    }

    /// <summary>The highlight of the region being written goes; the scan line hides.</summary>
    private void EndWriting()
    {
        if (_canvas is not null)
        {
            _canvas.Active = null;
            _canvas.ScanPosition = double.NaN;
        }
        if (_codeView is not null)
        {
            _codeView.WritingRegion = null;
        }
    }

    private void CancelRun()
    {
        if (_cts is not null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }
        _run++;
    }

    /// <summary>Clears the regions, the code and the buffers for a new run or a new design.</summary>
    private void ClearRunState()
    {
        _regions.Clear();
        _received.Clear();
        _marks.Clear();
        _pacer.Reset();
        _shown = 0;
        _partClock = 0;
        _simulationFed = 0;
        _generatorDone = false;
        _canvasHover = null;
        _codeHover = null;
        _focusRegion = null;
        _canvas?.ClearRegions();
        EndWriting();
        Code = string.Empty;
        _codeView?.SetCode(string.Empty, RegionAt, fade: false);
        _previewKey = null;
        _previewFromFactory = false;
        UpdateHighlight();
    }

    /// <summary>Back to the start for the design shown: Ready with a design, Empty without one.</summary>
    private void ResetRun(bool announce)
    {
        CancelRun();
        ClearRunState();
        Phase = EffectiveSource is null ? DesignToCodePhase.Empty : DesignToCodePhase.Ready;
        if (View != DesignToCodeView.Code)
        {
            SetCurrentValue(ViewProperty, DesignToCodeView.Code);
        }
        if (announce && EffectiveSource is { } source)
        {
            Announce(Labels.AnnounceSource(source.Name));
        }
        RefreshAll();
    }

    /// <summary>After a template change: the state's code and regions on the new parts.</summary>
    private void RebuildFromState()
    {
        if (Phase == DesignToCodePhase.Empty && EffectiveSource is not null)
        {
            Phase = DesignToCodePhase.Ready;
        }
        else if (Phase == DesignToCodePhase.Ready && EffectiveSource is null)
        {
            Phase = DesignToCodePhase.Empty;
        }
        if (_canvas is not null)
        {
            foreach (var region in _regions)
            {
                var view = _canvas.SetRegion(region);
                if (Phase != DesignToCodePhase.Analyzing)
                {
                    _canvas.Reveal(view, animate: false);
                }
            }
        }
        _codeView?.SetCode(Code, RegionAt, fade: false);
        UpdateHighlight();
        UpdateLoop();
    }

    private void OnSourceChanged(DesignToCodeSource? old)
    {
        if (_ownedBitmap is not null && !ReferenceEquals(Source?.Image, _ownedBitmap))
        {
            // A file the card read itself is let go when it is replaced (Arc revokes its object URL).
            _ownedBitmap.Dispose();
            _ownedBitmap = null;
        }
        if (Source is not null)
        {
            _sourceCleared = false;
            ErrorMessage = null;
        }
        if (old is not null || Source is not null)
        {
            StopAutoPlayIfAttached();
        }
        ResetRun(announce: false);
        UpdateLoop();
    }

    private void StopAutoPlayIfAttached()
    {
        if (this.IsAttachedToVisualTree())
        {
            StopAutoPlay();
        }
    }

    /// <summary>The sample loop stops for good (the run in progress finishes).</summary>
    internal void StopAutoPlay()
    {
        if (_autoStopped)
        {
            return;
        }
        _autoStopped = true;
        UpdateLoop();
    }

    /// <summary>The sample loop: generate, hold on the code, show the working preview, start again (once under reduced motion).</summary>
    private void StepAutoPlay(double dt)
    {
        if (_autoStopped || !EffectiveAutoPlay)
        {
            return;
        }
        var loops = ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full;
        switch (_autoStage)
        {
            case AutoStage.Idle:
                if (Phase == DesignToCodePhase.Ready && EffectiveSource is not null)
                {
                    _autoStage = AutoStage.Running;
                    Generate();
                }
                break;
            case AutoStage.Running:
                if (!IsRunning && Phase != DesignToCodePhase.Done)
                {
                    _autoStage = AutoStage.Finished;
                }
                break;
            case AutoStage.HoldCode:
                _autoClock += dt;
                if (_autoClock >= HoldCodeSeconds)
                {
                    if (!loops)
                    {
                        _autoStage = AutoStage.Finished;
                        break;
                    }
                    _autoStage = AutoStage.HoldPreview;
                    _autoClock = 0;
                    _previewActed = false;
                    SetCurrentValue(ViewProperty, DesignToCodeView.Preview);
                }
                break;
            case AutoStage.HoldPreview:
                _autoClock += dt;
                if (!_previewActed && _autoClock >= PreviewActionSeconds)
                {
                    _previewActed = true;
                    if (_previewFromFactory && PreviewContent is { } preview && _runSimulation?.PreviewAction is { } act)
                    {
                        act(preview);
                    }
                }
                if (_autoClock >= HoldPreviewSeconds)
                {
                    _autoStage = AutoStage.Running;
                    Generate();
                }
                break;
        }
    }

    private bool WantsFrames() =>
        IsRunning || (EffectiveAutoPlay && !_autoStopped && _autoStage != AutoStage.Finished);

    private void UpdateLoop()
    {
        if (WantsFrames())
        {
            _loop.Start();
        }
        else
        {
            _loop.Stop();
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateLoop();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (IsRunning && !_simulated)
        {
            // An app run cannot continue without the card on screen; its partial code is kept.
            Stop();
        }
    }

    private void OnCodeScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_codeScroll is null)
        {
            return;
        }
        var bottom = _codeScroll.Extent.Height - _codeScroll.Viewport.Height;
        if (e.ExtentDelta.Y > 0 && _following && Phase == DesignToCodePhase.Writing)
        {
            // New lines while following the stream: keep the newest in view.
            _codeScroll.Offset = new Vector(_codeScroll.Offset.X, Math.Max(0, bottom));
            return;
        }
        if (e.OffsetDelta.Y < 0)
        {
            _following = false;
        }
        else if (_codeScroll.Offset.Y >= bottom - 4)
        {
            _following = true;
        }
    }
}
