using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The waveform scrubber of the full player (as measured): 54 bars 2 apart with radius 2 and at least 3 tall, the unplayed
/// ones at 16% of the text color and the played ones solid, cut continuously at the playhead (the cut can fall inside a
/// bar). A press seeks straight to the pointer and the drag follows 1:1; while held the whole wave stretches to 1.18 high
/// on a firm spring (k 400, c 40). It is a slider: arrows step 5 seconds, Page Up / Page Down 30, Home and End go to the
/// ends; the keyboard ring is a square 2px ring 2px outside.
/// <para>Why code: 108 bars are drawn in one pass and the stretch is a retargetable spring.</para>
/// A candidate to share with the voice recorder's playback scrubber.
/// </summary>
internal sealed class NowPlayingWaveform : Control
{
    public const double BarGap = 2;
    public const double BarRadius = 2;
    public const double BarMinHeight = 3;
    public const double ScrubStretch = 1.18;

    public static readonly StyledProperty<IBrush?> PlayedInkProperty =
        AvaloniaProperty.Register<NowPlayingWaveform, IBrush?>(nameof(PlayedInk));

    public static readonly StyledProperty<IBrush?> UnplayedInkProperty =
        AvaloniaProperty.Register<NowPlayingWaveform, IBrush?>(nameof(UnplayedInk));

    public static readonly StyledProperty<IBrush?> FocusInkProperty =
        AvaloniaProperty.Register<NowPlayingWaveform, IBrush?>(nameof(FocusInk));

    private double[] _peaks = [];
    private double _fraction;

    // The fraction the last frame drew. While playing, the cut moves about 3 device pixels a second on a three-minute
    // track: repainting every frame redraws the same pixels, so a new position repaints only once the cut has moved a
    // quarter of a device pixel (or reached an end).
    private double _drawnFraction;
    private double _position;
    private double _duration;
    private MotionValue<double>? _stretch;
    private double _stretchValue = 1;
    private IPointer? _captured;
    private Pen? _focusPen;
    private string _valueText = string.Empty;

    static NowPlayingWaveform()
    {
        FocusableProperty.OverrideDefaultValue<NowPlayingWaveform>(true);
        AffectsRender<NowPlayingWaveform>(PlayedInkProperty, UnplayedInkProperty, FocusInkProperty);
    }

    public NowPlayingWaveform()
    {
        Cursor = new Cursor(StandardCursorType.SizeWestEast);
    }

    public IBrush? PlayedInk { get => GetValue(PlayedInkProperty); set => SetValue(PlayedInkProperty, value); }

    public IBrush? UnplayedInk { get => GetValue(UnplayedInkProperty); set => SetValue(UnplayedInkProperty, value); }

    public IBrush? FocusInk { get => GetValue(FocusInkProperty); set => SetValue(FocusInkProperty, value); }

    /// <summary>The bar heights, 0 to 1.</summary>
    public IReadOnlyList<double> Peaks => _peaks;

    /// <summary>The played fraction drawn now (0 to 1).</summary>
    public double Fraction => _fraction;

    /// <summary>The vertical stretch now (1 at rest, 1.18 while held).</summary>
    public double Stretch => _stretchValue;

    /// <summary>True while a press or drag holds the playhead.</summary>
    public bool IsScrubbing => _captured is not null;

    /// <summary>The position in seconds (the slider's value).</summary>
    public double Position => _position;

    /// <summary>The length in seconds (the slider's maximum).</summary>
    public double Duration => _duration;

    /// <summary>The slider's spoken value ("1:07 of 3:51").</summary>
    public string ValueText => _valueText;

    /// <summary>A press or move asks for a position (seconds); the flag is true on release, when the seek is committed.</summary>
    public event Action<double, bool>? Scrubbed;

    /// <summary>A key asks for a position (seconds).</summary>
    public event Action<double>? KeySeek;

    /// <summary>The step of the arrow keys and of Page Up / Page Down, in seconds.</summary>
    public double SmallStep { get; set; } = 5;

    public double LargeStep { get; set; } = 30;

    public void SetPeaks(double[] peaks)
    {
        _peaks = peaks;
        InvalidateVisual();
    }

    /// <summary>Shows a position: the played cut, the slider value and its spoken text.</summary>
    public void SetPosition(double position, double duration, string? valueText)
    {
        var fraction = duration > 0 ? Math.Clamp(position / duration, 0, 1) : 0;
        _position = position;
        _duration = duration;
        if (fraction != _fraction)
        {
            _fraction = fraction;
            if (NowPlayingRepaint.Moved(this, _drawnFraction, fraction))
            {
                InvalidateVisual();
            }
        }
        if (valueText is not null && _valueText != valueText)
        {
            _valueText = valueText;
            AutomationProperties.SetItemStatus(this, valueText);
        }
    }

    /// <summary>The seconds at x (0 at the left edge, the length at the right).</summary>
    public double SecondsAt(double x) =>
        Bounds.Width > 0 && _duration > 0 ? Math.Clamp(x / Bounds.Width, 0, 1) * _duration : 0;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetCurrentPoint(this);
        if (e.Pointer.Type == PointerType.Mouse && !point.Properties.IsLeftButtonPressed)
        {
            return;
        }
        e.Handled = true;
        _captured = e.Pointer;
        e.Pointer.Capture(this);
        AnimateStretch(ScrubStretch);
        Scrubbed?.Invoke(SecondsAt(point.Position.X), false);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_captured is null || e.Pointer != _captured)
        {
            return;
        }
        e.Handled = true;
        Scrubbed?.Invoke(SecondsAt(e.GetPosition(this).X), false);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_captured is null || e.Pointer != _captured)
        {
            return;
        }
        e.Handled = true;
        var seconds = SecondsAt(e.GetPosition(this).X);
        End();
        e.Pointer.Capture(null);
        Scrubbed?.Invoke(seconds, true);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_captured is not null && e.Pointer == _captured)
        {
            // Capture taken away (a window change, a cancelled touch): commit where the playhead is.
            End();
            Scrubbed?.Invoke(_position, true);
        }
    }

    private void End()
    {
        _captured = null;
        AnimateStretch(1);
    }

    private void AnimateStretch(double to)
    {
        _stretch ??= CreateStretch();
        _ = _stretch.AnimateTo(to, MotionSpec.Spring(NowPlayingMotion.Firm));
    }

    private MotionValue<double> CreateStretch()
    {
        var value = new MotionValue<double>(this, 1, MotionVectors.Double);
        value.Changed += v =>
        {
            _stretchValue = v;
            InvalidateVisual();
        };
        return value;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        double? target = e.Key switch
        {
            Key.Left or Key.Down => _position - SmallStep,
            Key.Right or Key.Up => _position + SmallStep,
            Key.PageDown => _position - LargeStep,
            Key.PageUp => _position + LargeStep,
            Key.Home => 0,
            Key.End => _duration - 1,
            _ => null
        };
        if (target is { } seconds)
        {
            e.Handled = true;
            KeySeek?.Invoke(seconds);
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FocusInkProperty)
        {
            _focusPen = null;
        }
    }

    public override void Render(DrawingContext context)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        // The whole box takes input, not only the bars.
        context.FillRectangle(Brushes.Transparent, new Rect(0, 0, w, h));
        var n = _peaks.Length;
        if (n > 0 && w > 0)
        {
            var barWidth = Math.Max(0, (w - (n - 1) * BarGap) / n);
            var radius = Math.Min(BarRadius, barWidth / 2);
            if (UnplayedInk is { } unplayed)
            {
                DrawBars(context, unplayed, barWidth, radius, h);
            }
            _drawnFraction = _fraction;
            if (PlayedInk is { } played && _fraction > 0)
            {
                // The played layer is cut at the playhead, continuously (it may split a bar).
                using (context.PushClip(new Rect(0, -h, w * _fraction, 3 * h)))
                {
                    DrawBars(context, played, barWidth, radius, h);
                }
            }
        }
        if (IsFocusRingShown && FocusInk is { } ring)
        {
            _focusPen ??= new Pen(ring, 2);
            context.DrawRectangle(null, _focusPen, new Rect(-3, -3, w + 6, h + 6));
        }
    }

    /// <summary>True while the keyboard ring shows (keyboard focus only).</summary>
    public bool IsFocusRingShown => IsFocused && Classes.Contains(":focus-visible");

    private void DrawBars(DrawingContext context, IBrush brush, double barWidth, double radius, double h)
    {
        var stretch = _stretchValue;
        var mid = h / 2;
        for (var i = 0; i < _peaks.Length; i++)
        {
            var barHeight = Math.Max(BarMinHeight, _peaks[i] * h) * stretch;
            var x = i * (barWidth + BarGap);
            context.DrawRectangle(brush, null, new RoundedRect(new Rect(x, mid - barHeight / 2, barWidth, barHeight), radius));
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new WaveformPeer(this);

    /// <summary>Seeks to <paramref name="seconds"/> as a keyboard seek (assistive tech setting the slider value).</summary>
    internal void RequestValue(double seconds) => KeySeek?.Invoke(seconds);

    private sealed class WaveformPeer(NowPlayingWaveform owner) : ControlAutomationPeer(owner), IRangeValueProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Slider;

        public bool IsReadOnly => false;

        public double Minimum => 0;

        public double Maximum => owner._duration;

        public double Value => NowPlayingFormat.Floor(owner._position);

        public double SmallChange => owner.SmallStep;

        public double LargeChange => owner.LargeStep;

        public void SetValue(double value) => owner.RequestValue(value);
    }
}
