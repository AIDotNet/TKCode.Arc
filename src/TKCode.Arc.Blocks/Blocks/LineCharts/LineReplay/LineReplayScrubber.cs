using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// The scrubber of a <see cref="LineReplay"/> (Arc Line replay, design reference): a 4px track with the played part in
/// the series color, a 2 × 12 tick per moment (dark once passed, light before; incidents in the warning color), and a
/// 16px thumb, in a 44px hit area. It is a slider measured in minutes: pressing or dragging moves the playhead, and the
/// keys follow <see cref="LineReplay"/>. Its value text is updated only while playback is paused, so a screen reader is
/// not flooded while it plays.
/// </summary>
internal sealed class LineReplayScrubber : Control
{
    private const double PadX = 10;
    private const double TrackHeight = 4;
    private const double TickWidth = 2;
    private const double TickHeight = 12;
    private const double ThumbRadius = 8;
    private const double FocusHeight = 36;
    private const double FocusRadius = 18;

    private readonly LineReplay _owner;
    private LineReplayData _data = LineReplayData.Empty;
    private double _position;
    private double _reportedPosition;
    private string? _valueText;
    private bool _dragging;

    private bool _colorsKnown;
    private IImmutableBrush _track = Brushes.LightGray;
    private IImmutableBrush _fill = Brushes.SteelBlue;
    private IImmutableBrush _reached = Brushes.Black;
    private IImmutableBrush _pending = Brushes.Gray;
    private IImmutableBrush _warning = Brushes.Orange;
    private IImmutableBrush _warningPending = Brushes.Moccasin;
    private IImmutableBrush _thumb = Brushes.White;
    private BoxShadows _thumbShadow;
    private IPen _focusPen = new ImmutablePen(Brushes.Black, 2);

    public LineReplayScrubber(LineReplay owner)
    {
        _owner = owner;
        Focusable = true;
        IsTabStop = true;
        FocusAdorner = null;
        Height = 44;
        Cursor = new Cursor(StandardCursorType.Hand);
        ActualThemeVariantChanged += (_, _) => OnThemeChanged();
        ResourcesChanged += (_, _) => OnThemeChanged();
    }

    // Theme and accent switches: colors are resolved again on the next render.
    private void OnThemeChanged()
    {
        _colorsKnown = false;
        InvalidateVisual();
    }

    /// <summary>The value the slider reports to assistive technology (tests).</summary>
    internal double ReportedPosition => _reportedPosition;

    /// <summary>The value text the slider reports (tests).</summary>
    internal string? ValueText => _valueText;

    public void SetData(LineReplayData data)
    {
        _data = data;
        InvalidateVisual();
    }

    public void SetPosition(double position)
    {
        _position = position;
        InvalidateVisual();
    }

    /// <summary>Updates what the slider reports (called while paused and after jumps).</summary>
    public void Report(double position, string valueText)
    {
        var old = Math.Round(_reportedPosition);
        _reportedPosition = position;
        _valueText = valueText;
        AutomationProperties.SetHelpText(this, valueText);
        if (old != Math.Round(position) && ControlAutomationPeer.FromElement(this) is { } peer)
        {
            peer.RaisePropertyChangedEvent(RangeValuePatternIdentifiers.ValueProperty, old, Math.Round(position));
        }
    }

    private Rect TrackRect
    {
        get
        {
            var width = Math.Max(1, Bounds.Width - PadX * 2);
            return new Rect(PadX, (Bounds.Height - TrackHeight) / 2, width, TrackHeight);
        }
    }

    private double XOf(double time)
    {
        var track = TrackRect;
        var span = _data.End - _data.Start;
        return track.X + (span > 0 ? Math.Clamp((time - _data.Start) / span, 0, 1) : 1) * track.Width;
    }

    private double TimeAt(double x)
    {
        var track = TrackRect;
        var t = Math.Clamp((x - track.X) / track.Width, 0, 1);
        return _data.Start + t * (_data.End - _data.Start);
    }

    // Input ------------------------------------------------------------------------------------------------------------------

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed || _data.Count == 0)
        {
            return;
        }
        _dragging = true;
        e.Pointer.Capture(this);
        _owner.ScrubTo(TimeAt(point.Position.X));
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragging)
        {
            _owner.ScrubTo(TimeAt(e.GetPosition(this).X));
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragging)
        {
            _dragging = false;
            e.Pointer.Capture(null);
            _owner.EndScrub();
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_dragging)
        {
            _dragging = false;
            _owner.EndScrub();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
        {
            return;
        }
        e.Handled = _owner.HandleScrubberKey(e.Key, e.KeyModifiers);
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

    // Rendering ------------------------------------------------------------------------------------------------------------

    private void EnsureColors()
    {
        if (_colorsKnown)
        {
            return;
        }
        var border = ArcTokens.GetColor(this, ArcDesignTokenKind.Border);
        var borderStrong = ArcTokens.GetColor(this, ArcDesignTokenKind.BorderStrong);
        var warning = ArcTokens.GetColor(this, ArcDesignTokenKind.Warning);
        var surface = ArcTokens.GetColor(this, ArcDesignTokenKind.Surface);
        // The track sits between Border and BorderStrong; a pending incident tick is the warning color half faded.
        _track = new ImmutableSolidColorBrush(Oklch.MixOklab(border, borderStrong, 0.35));
        _fill = new ImmutableSolidColorBrush(ArcTokens.GetColor(this, ArcDesignTokenKind.Series1));
        _reached = new ImmutableSolidColorBrush(ArcTokens.GetColor(this, ArcDesignTokenKind.Foreground));
        _pending = new ImmutableSolidColorBrush(borderStrong);
        _warning = new ImmutableSolidColorBrush(warning);
        _warningPending = new ImmutableSolidColorBrush(Oklch.MixOklab(warning, surface, 0.5));
        _thumb = new ImmutableSolidColorBrush(ArcTokens.GetColor(this, ArcDesignTokenKind.ControlThumb));
        _thumbShadow = ArcTokens.Get<BoxShadows>(this, ArcDesignTokenKind.ControlThumbShadow);
        _focusPen = new ImmutablePen(new ImmutableSolidColorBrush(ArcTokens.GetColor(this, ArcDesignTokenKind.FocusRing)), 2);
        _colorsKnown = true;
    }

    public override void Render(DrawingContext context)
    {
        EnsureColors();
        var size = Bounds.Size;
        // The whole 44px band is the hit area.
        context.FillRectangle(Brushes.Transparent, new Rect(size));
        if (IsFocused && Classes.Contains(":focus-visible"))
        {
            var ring = new Rect(1, (size.Height - FocusHeight) / 2, Math.Max(0, size.Width - 2), FocusHeight);
            context.DrawRectangle(null, _focusPen, new RoundedRect(ring, FocusRadius));
        }
        var track = TrackRect;
        context.DrawRectangle(_track, null, new RoundedRect(track, TrackHeight / 2));
        var x = XOf(_position);
        if (x > track.X)
        {
            context.DrawRectangle(_fill, null, new RoundedRect(new Rect(track.X, track.Y, x - track.X, track.Height), TrackHeight / 2));
        }
        foreach (var m in _data.Moments)
        {
            var reached = m.Time <= _position + 1e-6;
            var brush = m.IsIncident ? (reached ? _warning : _warningPending) : reached ? _reached : _pending;
            var tx = Math.Round(XOf(m.Time) - TickWidth / 2);
            context.DrawRectangle(brush, null,
                new RoundedRect(new Rect(tx, (size.Height - TickHeight) / 2, TickWidth, TickHeight), 1));
        }
        var thumb = new Rect(x - ThumbRadius, size.Height / 2 - ThumbRadius, ThumbRadius * 2, ThumbRadius * 2);
        context.DrawRectangle(_thumb, null, new RoundedRect(thumb, ThumbRadius), _thumbShadow);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ScrubberAutomationPeer(this);

    private sealed class ScrubberAutomationPeer : ControlAutomationPeer, IRangeValueProvider
    {
        public ScrubberAutomationPeer(LineReplayScrubber owner) : base(owner)
        {
        }

        private LineReplayScrubber Scrubber => (LineReplayScrubber)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Slider;

        protected override string? GetNameCore() => Scrubber._owner.ScrubberLabel;

        protected override string? GetHelpTextCore() => Scrubber._valueText;

        public bool IsReadOnly => false;

        public double Minimum => Scrubber._data.Start;

        public double Maximum => Scrubber._data.End;

        public double Value => Math.Round(Scrubber._reportedPosition);

        public double SmallChange => Scrubber._data.MinStep;

        public double LargeChange => 60;

        public void SetValue(double value) => Scrubber._owner.Seek(value, animate: false);
    }
}
