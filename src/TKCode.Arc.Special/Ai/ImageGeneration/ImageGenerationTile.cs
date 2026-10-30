using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media.Imaging;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One tile of the grid: a toggle button (pressed = picked) around the self-drawn picture, with a progress pill (a ring and
/// the percentage) while it runs or upscales, a centred icon and word when it failed or was stopped, and the accent ring
/// with a check badge when picked. One progress value drives the mosaic, the ring and the percentage; it eases toward the
/// reported progress on a frame loop that runs only while the tile works and is on screen, or estimates when nothing is
/// reported. The card owns the selection, so pressing does not toggle the tile by itself.
/// </summary>
[TemplatePart(CanvasPart, typeof(ImageGenerationCanvas))]
[TemplatePart(RingPart, typeof(ArcRing))]
[TemplatePart(PercentPart, typeof(TextBlock))]
[TemplatePart(StatusGlyphPart, typeof(ArcGlyph))]
[TemplatePart(StatusTextPart, typeof(TextBlock))]
[TemplatePart(RetryTextPart, typeof(TextBlock))]
[PseudoClasses(RunningPseudoClass, DonePseudoClass, FailedPseudoClass, StoppedPseudoClass, WorkingPseudoClass)]
internal sealed class ImageGenerationTile : ToggleButton
{
    internal const string CanvasPart = "PART_Canvas";
    internal const string RingPart = "PART_Ring";
    internal const string PercentPart = "PART_Percent";
    internal const string StatusGlyphPart = "PART_StatusGlyph";
    internal const string StatusTextPart = "PART_StatusText";
    internal const string RetryTextPart = "PART_RetryText";

    internal const string RunningPseudoClass = ":running";
    internal const string DonePseudoClass = ":done";
    internal const string FailedPseudoClass = ":failed";
    internal const string StoppedPseudoClass = ":stopped";

    /// <summary>Set while the progress pill shows: running, or upscaling.</summary>
    internal const string WorkingPseudoClass = ":working";

    /// <summary>The light band's loop, one pass.</summary>
    internal static readonly TimeSpan ShimmerPeriod = TimeSpan.FromSeconds(1.6);

    /// <summary>How quickly the shown progress closes on the reported one (time constant).</summary>
    internal const double ChaseSeconds = 0.14;

    /// <summary>An image that arrives without a preview fades in over this long.</summary>
    internal static readonly TimeSpan RevealDuration = TimeSpan.FromMilliseconds(240);

    private static readonly string[] s_percent = BuildPercents();

    private ImageGenerationCanvas? _canvas;
    private ArcRing? _ring;
    private TextBlock? _percent;
    private ArcGlyph? _statusGlyph;
    private TextBlock? _statusText;
    private TextBlock? _retryText;
    private readonly LiveLoop _loop;
    private MotionValue<double>? _reveal;
    private ImageGenerationTileState _state;
    private ImageGenerationLabels _labels = ImageGenerationLabels.Default;
    private double _target;
    private double _shown;
    private bool _reported;
    private double _elapsed;
    private double _phase;
    private int _shownPercent = -1;
    private double _sweepTarget = -1;
    private double _sweep = -1;

    public ImageGenerationTile()
    {
        _loop = new LiveLoop(this, Step) { MinimumLevel = ArcMotionLevel.Off };
        _loop.RunningChanged += running =>
        {
            if (!running && _canvas is not null)
            {
                // Paused or finished: draw the resting frame without the band.
                _canvas.ShimmerPhase = -1;
                _canvas.InvalidateVisual();
            }
        };
    }

    /// <summary>The tile's place in the grid, 0 to 3.</summary>
    public int Index { get; set; }

    public ImageGenerationTileState State => _state;

    /// <summary>The finished image (the upscaled one after an upscale), or null.</summary>
    public ImageGenerationImage? Image { get; private set; }

    /// <summary>The request the tile was made from (Retry sends it again).</summary>
    public ImageGenerationRequest? Request { get; set; }

    /// <summary>The run the tile belongs to; a report from an older run is ignored.</summary>
    public int Run { get; set; }

    /// <summary>Whether the shown image is an upscale.</summary>
    public bool IsUpscaled { get; private set; }

    /// <summary>The progress shown, 0 to 1.</summary>
    public double Shown => _shown;

    /// <summary>The percentage the pill shows.</summary>
    public string PercentText => _percent?.Text ?? string.Empty;

    /// <summary>The upscale sweep's position, or −1.</summary>
    public double SweepShown => _sweep;

    internal ImageGenerationCanvas? Canvas => _canvas;

    internal LiveLoop Loop => _loop;

    public ImageGenerationLabels Labels
    {
        get => _labels;
        set
        {
            _labels = value;
            UpdateTexts();
        }
    }

    /// <summary>The batch's shape (height over width).</summary>
    public double Aspect { get; private set; } = 1;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _canvas = e.NameScope.Find<ImageGenerationCanvas>(CanvasPart);
        _ring = e.NameScope.Find<ArcRing>(RingPart);
        _percent = e.NameScope.Find<TextBlock>(PercentPart);
        _statusGlyph = e.NameScope.Find<ArcGlyph>(StatusGlyphPart);
        _statusText = e.NameScope.Find<TextBlock>(StatusTextPart);
        _retryText = e.NameScope.Find<TextBlock>(RetryTextPart);
        _shownPercent = -1;
        Sync();
        UpdateTexts();
        UpdateClasses();
    }

    /// <summary>The card owns the selection: pressing raises Click only.</summary>
    protected override void Toggle()
    {
    }

    /// <summary>Empties the tile for a batch of a new shape.</summary>
    public void Reset(double aspect)
    {
        Aspect = aspect;
        Image = null;
        Request = null;
        IsUpscaled = false;
        _reported = false;
        _target = _shown = 0;
        _sweepTarget = _sweep = -1;
        if (_canvas is not null)
        {
            _canvas.Mosaic = null;
            _canvas.Image = null;
            _canvas.SweepImage = null;
            _canvas.Reveal = 1;
        }
        SetState(ImageGenerationTileState.Idle);
    }

    /// <summary>Starts a run: the placeholder shimmers until the first report.</summary>
    public void Begin(int run, ImageGenerationRequest request)
    {
        Reset(Aspect);
        Run = run;
        Request = request;
        _elapsed = 0;
        SetState(ImageGenerationTileState.Running);
    }

    /// <summary>A progress report, optionally with a preview the mosaic resolves from.</summary>
    public void Report(double progress, Bitmap? preview)
    {
        if (_state != ImageGenerationTileState.Running)
        {
            return;
        }
        _reported = true;
        _target = Math.Max(_target, Math.Clamp(double.IsFinite(progress) ? progress : 0, 0, 1));
        if (preview is not null && _canvas is not null && _canvas.Mosaic is null)
        {
            _canvas.Mosaic = ImageGenerationMosaic.For(preview, Aspect);
            if (_canvas.Image is null && _canvas.Mosaic is not null)
            {
                // The preview is the picture the last step crossfades to until the real image takes over.
                _canvas.Image = preview;
            }
        }
        Advance(instant: !Animates);
    }

    /// <summary>The image arrived: the mosaic finishes and the image takes over on an identical final frame.</summary>
    public void Finish(ImageGenerationImage image)
    {
        Image = image;
        if (_canvas is not null)
        {
            var hadMosaic = _canvas.Mosaic is not null;
            if (image.Source is { } source)
            {
                _canvas.Mosaic ??= ImageGenerationMosaic.For(source, Aspect);
            }
            _canvas.Image = image.Source;
            if (!hadMosaic && image.Source is not null && Animates)
            {
                // No preview came: the image fades in over the placeholder grid.
                _reveal ??= new MotionValue<double>(this, 1, MotionVectors.Double);
                _reveal.Changed -= OnReveal;
                _reveal.Changed += OnReveal;
                _reveal.Jump(0);
                _canvas.Reveal = 0;
                _ = _reveal.AnimateTo(1, MotionSpec.Tween(RevealDuration, ArcEasings.Standard));
            }
        }
        _target = 1;
        _shown = 1;
        SetState(ImageGenerationTileState.Done);
        Sync();
    }

    public void Fail() => End(ImageGenerationTileState.Failed);

    public void Halt() => End(ImageGenerationTileState.Stopped);

    private void End(ImageGenerationTileState state)
    {
        if (_state != ImageGenerationTileState.Running)
        {
            return;
        }
        SetState(state);
        Sync();
    }

    /// <summary>An upscale starts on this tile: the pill comes back and the sweep waits at the left edge.</summary>
    public void BeginUpscale()
    {
        _sweepTarget = 0;
        _sweep = 0;
        _target = _shown = 0;
        if (_canvas is not null)
        {
            _canvas.SweepImage = null;
        }
        PseudoClasses.Set(WorkingPseudoClass, true);
        Sync();
        _loop.Start();
    }

    public void ReportUpscale(double progress)
    {
        if (_sweepTarget < 0)
        {
            return;
        }
        _target = Math.Max(_target, Math.Clamp(double.IsFinite(progress) ? progress : 0, 0, 1));
        _sweepTarget = _target;
        Advance(instant: !Animates);
    }

    /// <summary>The upscaled image arrived; it replaces the image once the sweep has crossed.</summary>
    public void FinishUpscale(ImageGenerationImage image)
    {
        Image = image;
        IsUpscaled = true;
        if (_canvas is not null)
        {
            if (image.Source is { } source)
            {
                _canvas.SweepImage = source;
                _canvas.Mosaic = ImageGenerationMosaic.For(source, Aspect) ?? _canvas.Mosaic;
            }
        }
        _target = 1;
        _sweepTarget = 1;
        Advance(instant: !Animates);
        if (!_loop.IsRunning)
        {
            EndSweep();
        }
    }

    /// <summary>An upscale ended without an image (failed or cancelled).</summary>
    public void CancelUpscale()
    {
        _sweepTarget = _sweep = -1;
        _target = _shown = 1;
        if (_canvas is not null)
        {
            _canvas.SweepImage = null;
        }
        PseudoClasses.Set(WorkingPseudoClass, false);
        Sync();
    }

    private void EndSweep()
    {
        _sweepTarget = _sweep = -1;
        _shown = _target = 1;
        if (_canvas is not null && Image?.Source is { } source)
        {
            _canvas.Image = source;
            _canvas.SweepImage = null;
        }
        PseudoClasses.Set(WorkingPseudoClass, false);
        Sync();
    }

    private bool Animates => ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off && TopLevel.GetTopLevel(this) is not null;

    private void OnReveal(double value)
    {
        if (_canvas is not null)
        {
            _canvas.Reveal = value;
            _canvas.InvalidateVisual();
        }
    }

    private void SetState(ImageGenerationTileState state)
    {
        _state = state;
        UpdateClasses();
        UpdateTexts();
        if (state == ImageGenerationTileState.Running)
        {
            _loop.Start();
        }
        Sync();
    }

    private void UpdateClasses()
    {
        PseudoClasses.Set(RunningPseudoClass, _state == ImageGenerationTileState.Running);
        PseudoClasses.Set(DonePseudoClass, _state == ImageGenerationTileState.Done);
        PseudoClasses.Set(FailedPseudoClass, _state == ImageGenerationTileState.Failed);
        PseudoClasses.Set(StoppedPseudoClass, _state == ImageGenerationTileState.Stopped);
        PseudoClasses.Set(WorkingPseudoClass, _state == ImageGenerationTileState.Running || _sweepTarget >= 0);
    }

    private void UpdateTexts()
    {
        var number = Index + 1;
        var name = _state switch
        {
            ImageGenerationTileState.Running => _labels.ImageRunning(number),
            ImageGenerationTileState.Failed => _labels.ImageFailed(number),
            ImageGenerationTileState.Stopped => _labels.ImageStopped(number),
            ImageGenerationTileState.Done => _labels.Image(number, Image?.Description),
            _ => _labels.Image(number, null),
        };
        AutomationProperties.SetName(this, name);
        if (_statusText is not null)
        {
            _statusText.Text = _state == ImageGenerationTileState.Failed ? _labels.Failed : _labels.Stopped;
        }
        if (_retryText is not null)
        {
            _retryText.Text = _labels.Retry;
        }
        if (_statusGlyph is not null)
        {
            _statusGlyph.Data = _state == ImageGenerationTileState.Failed ? ArcIcons.CircleAlert : ArcIcons.Square;
        }
    }

    /// <summary>
    /// One frame while the tile works: the shown progress closes on the target (or estimates when nothing is reported), the
    /// sweep follows it, and the band moves on. Runs at every motion level (the progress is information); the band only at Full.
    /// </summary>
    private bool Step(double dt)
    {
        _elapsed += dt;
        var working = _state == ImageGenerationTileState.Running || _sweepTarget >= 0;
        if (_state == ImageGenerationTileState.Running && !_reported)
        {
            // Nothing reported yet: creep toward 90% so the ring is never frozen.
            _target = Math.Max(_target, 0.9 * (1 - Math.Exp(-_elapsed / 6)));
        }
        Advance(instant: false, dt);
        if (_canvas is not null)
        {
            if (_state == ImageGenerationTileState.Running && _canvas.AllowsShimmer)
            {
                _phase = (_phase + dt / ShimmerPeriod.TotalSeconds) % 1;
                _canvas.ShimmerPhase = _phase;
            }
            else
            {
                _canvas.ShimmerPhase = -1;
            }
            _canvas.InvalidateVisual();
        }
        if (_sweepTarget >= 1 && _sweep >= 0.999)
        {
            EndSweep();
            return false;
        }
        return working;
    }

    private void Advance(bool instant, double dt = 0)
    {
        if (instant)
        {
            _shown = _target;
            if (_sweepTarget >= 0)
            {
                _sweep = _sweepTarget;
            }
        }
        else if (dt > 0)
        {
            var k = 1 - Math.Exp(-dt / ChaseSeconds);
            _shown += (_target - _shown) * k;
            if (Math.Abs(_target - _shown) < 0.002)
            {
                _shown = _target;
            }
            if (_sweepTarget >= 0)
            {
                _sweep += (_sweepTarget - _sweep) * k;
                if (Math.Abs(_sweepTarget - _sweep) < 0.002)
                {
                    _sweep = _sweepTarget;
                }
            }
        }
        if (instant && _sweepTarget >= 1)
        {
            EndSweep();
            return;
        }
        Sync();
    }

    /// <summary>Writes the shown progress to the picture, the ring and the percentage (a cached string; no allocation).</summary>
    private void Sync()
    {
        if (_canvas is not null)
        {
            _canvas.Aspect = Aspect;
            _canvas.Progress = _state == ImageGenerationTileState.Done ? 1 : _shown;
            _canvas.Sweep = ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full ? _sweep : -1;
            _canvas.InvalidateVisual();
        }
        if (_ring is not null)
        {
            _ring.Value = _shown;
        }
        var percent = (int)Math.Floor(_shown * 100);
        if (_percent is not null && percent != _shownPercent)
        {
            _shownPercent = percent;
            _percent.Text = s_percent[Math.Clamp(percent, 0, 100)];
        }
    }

    private static string[] BuildPercents()
    {
        var texts = new string[101];
        for (var i = 0; i <= 100; i++)
        {
            texts[i] = i + "%";
        }
        return texts;
    }
}
