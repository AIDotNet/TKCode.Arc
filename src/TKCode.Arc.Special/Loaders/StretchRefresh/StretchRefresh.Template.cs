using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

public partial class StretchRefresh
{
    /// <summary>The window width at or under which the card drops its frame (set from the token).</summary>
    internal static readonly StyledProperty<double> NarrowWidthProperty =
        AvaloniaProperty.Register<StretchRefresh, double>(nameof(NarrowWidth), 475);

    private static readonly Easing EaseOut = new SplineEasing(0, 0, 0.58, 1);

    /// <summary>Sheet to 60 and tension to 0 when a refresh starts (ω ≈ 12.4, critically damped).</summary>
    private static readonly SpringSpec RestSpring = SpringSpec.FromVisual(0.42, 0);

    /// <summary>Sheet back to 0 (ω ≈ 13.0, Arc's smooth).</summary>
    private static readonly SpringSpec ReturnSpring = SpringSpec.FromVisual(0.4, 0);

    /// <summary>Track over-stretch, mark and glyph growth (ω ≈ 20, critically damped).</summary>
    private static readonly SpringSpec QuickSpring = SpringSpec.FromVisual(0.26, 0);

    private readonly StretchRefreshMachine _machine = new();
    private readonly StretchRefreshPullGesture _gesture = new();
    private readonly TranslateTransform _sheetShift = new();
    private readonly TranslateTransform _anchorShift = new();
    private readonly ScaleTransform _markScaleTransform = new();

    private Border? _card;
    private Border? _header;
    private Border? _wellFill;
    private double _innerRadius;
    private double _peelRadius = -1;
    private double _peelInner = -1;
    private StreamGeometry? _wellClip;
    private Size _wellClipSize;
    private AvaloniaButton? _button;
    private StretchRefreshSwap? _headerIcon;
    private StretchRefreshSwap? _subtitle;
    private Control? _anchor;
    private StretchRefreshMeter? _meter;
    private Control? _markHost;
    private StrokeDrawGlyph? _mark;
    private StretchRefreshSwap? _status;
    private Panel? _well;
    private Panel? _sheet;
    private Border? _sheetSurface;
    private Border? _sheetClip;
    private ScrollViewer? _scroller;
    private StretchRefreshList? _list;
    private Border? _feedRing;
    private ExplainerAnnouncer? _announcer;

    private MotionValue<double>? _y;
    private MotionValue<double>? _tension;
    private MotionValue<double>? _trackScale;
    private MotionValue<double>? _trackOpacity;
    private MotionValue<double>? _fillOpacity;
    private MotionValue<double>? _runOpacity;
    private MotionValue<double>? _armedMix;
    private MotionValue<double>? _markFade;
    private MotionValue<double>? _markScale;
    private LiveLoop? _runLoop;
    private LiveLoop? _spinLoop;
    private RotateTransform? _spin;
    private double _runTime;
    private double _runPulse;
    private IDisposable? _viewport;
    private bool _markFull;

    internal double NarrowWidth
    {
        get => GetValue(NarrowWidthProperty);
        set => SetValue(NarrowWidthProperty, value);
    }

    /// <summary>The header glyph's rotation while refreshing (tests).</summary>
    internal double SpinAngle => _spin?.Angle ?? 0;

    /// <summary>The running segment's reduced-motion breath (1 at full motion and at rest; tests).</summary>
    internal double RunPulse => _meter?.RunPulse ?? 1;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unhook(_scroller, _sheet, _button);
        DisposeMotion();
        _peelRadius = -1;
        var ns = e.NameScope;
        _card = ns.Find<Border>(CardPart);
        _header = ns.Find<Border>("PART_Header");
        _wellFill = ns.Find<Border>("PART_WellFill");
        _button = ns.Find<AvaloniaButton>(ButtonPart);
        _headerIcon = ns.Find<StretchRefreshSwap>(HeaderIconPart);
        _subtitle = ns.Find<StretchRefreshSwap>(SubtitlePart);
        _anchor = ns.Find<Control>(AnchorPart);
        _meter = ns.Find<StretchRefreshMeter>(MeterPart);
        _markHost = ns.Find<Control>(MarkHostPart);
        _mark = ns.Find<StrokeDrawGlyph>(MarkPart);
        _status = ns.Find<StretchRefreshSwap>(StatusPart);
        _well = ns.Find<Panel>(WellPart);
        _sheet = ns.Find<Panel>(SheetPart);
        _sheetSurface = ns.Find<Border>(SheetSurfacePart);
        _sheetClip = ns.Find<Border>(SheetClipPart);
        _scroller = ns.Find<ScrollViewer>(ScrollerPart);
        _list = ns.Find<StretchRefreshList>(ListPart);
        _feedRing = ns.Find<Border>(FeedRingPart);
        _announcer = ns.Find<ExplainerAnnouncer>(AnnouncerPart);

        if (_sheet is not null)
        {
            _sheet.RenderTransform = _sheetShift;
        }
        if (_anchor is not null)
        {
            _anchor.RenderTransform = _anchorShift;
        }
        if (_markHost is not null)
        {
            _markHost.RenderTransformOrigin = RelativePoint.Center;
            _markHost.RenderTransform = _markScaleTransform;
            _markHost.IsVisible = false;
        }
        if (_list is not null)
        {
            _list.ItemTemplate = ItemTemplate;
            _list.KeySelector = ItemKeySelector;
            _list.SetItems(ItemsSource);
        }
        _machine.Threshold = Threshold;
        var owner = (Visual?)_sheet ?? this;
        _y = Value(owner, 0, Apply);
        _tension = Value(owner, 0, () => _list?.SetSpread(_tension!.Value));
        _trackScale = Value(owner, 1, WriteMeter);
        _trackOpacity = Value(owner, 1, WriteMeter);
        _fillOpacity = Value(owner, 1, WriteMeter);
        _runOpacity = Value(owner, 0, WriteMeter);
        _armedMix = Value(owner, 0, WriteMeter);
        _markFade = Value(owner, 0, WriteMark);
        _markScale = Value(owner, 1, WriteMark);
        if (_meter is not null)
        {
            // The running segment: a pure function of its running time (no allocation per frame). Under reduced motion
            // it rests in the middle of the track and breathes in opacity instead of running, so the refresh still reads
            // as busy (the level is read per frame: one card, and only while it refreshes).
            _runLoop = new LiveLoop(_meter, dt =>
            {
                if (ArcMotion.IsFull(_meter))
                {
                    _runTime += dt;
                    _meter.RunPosition = StretchRefreshMath.RunPosition(_runTime);
                    _meter.RunPulse = 1;
                }
                else
                {
                    _runPulse = ArcBusyPulse.Advance(_runPulse, dt);
                    _meter.RunPosition = StretchRefreshMath.RunTravel / 2;
                    _meter.RunPulse = ArcBusyPulse.Opacity(_runPulse);
                }
                _meter.InvalidateVisual();
                return true;
            }) { MinimumLevel = ArcMotionLevel.Reduced };
        }
        if (_headerIcon is not null)
        {
            // The header glyph turns once per 0.85 s, linear, while the work runs; under reduced motion it holds still and
            // breathes in opacity with the segment.
            _spinLoop = new LiveLoop(_headerIcon, dt =>
            {
                if (ArcMotion.IsFull(_headerIcon))
                {
                    if (_spin is not null)
                    {
                        _spin.Angle = (_spin.Angle + dt * 360 / 0.85) % 360;
                    }
                    if (_spinGlyph is not null && _spinGlyph.Opacity < 1)
                    {
                        _spinGlyph.Opacity = 1;
                    }
                }
                else if (_spinGlyph is not null)
                {
                    _spinGlyph.Opacity = ArcBusyPulse.Opacity(_runPulse);
                }
                return true;
            }) { MinimumLevel = ArcMotionLevel.Reduced };
            _headerIcon.Show(IdleGlyphKey, CreateSpinGlyph(), animate: false);
        }
        _subtitle?.ShowText(Subtitle, animate: false);
        if (_subtitle is not null)
        {
            _subtitle.IsVisible = !string.IsNullOrEmpty(Subtitle);
        }
        _status?.ShowText(Labels.PullHint, animate: false);
        UpdateNames();
        UpdateInks();
        UpdateState();
        Hook();
        UpdateInnerCorners();
        WriteMeter();
        WriteMark();
    }

    /// <summary>
    /// Rounds the corners of whatever paints a background next to the card's rounded border (the header's top, the
    /// well's and the sheet's bottom) to the card's inner radius (radius minus border). A Border's ClipToBounds clips its
    /// child to a plain rectangle, so square backgrounds would paint over the card's rounded border. (A Visual.Clip
    /// geometry would also do it, but it breaks hit testing of the rows in headless runs.)
    /// </summary>
    private void UpdateInnerCorners()
    {
        var inset = Math.Max(BorderThickness.Left, BorderThickness.Top);
        _innerRadius = Math.Max(0, CornerRadius.TopLeft - inset);
        if (_header is not null)
        {
            _header.CornerRadius = new CornerRadius(_innerRadius, _innerRadius, 0, 0);
        }
        if (_wellFill is not null)
        {
            _wellFill.CornerRadius = new CornerRadius(0, 0, _innerRadius, _innerRadius);
        }
        Apply();
    }

    /// <summary>
    /// While the sheet is pushed down, its rounded bottom slides out of the card and the well's rectangular clip would
    /// cut it square across the card's rounded bottom border: clip the well to the card's inner bottom corners then. At
    /// rest the sheet's own rounded background already fits, and no clip is set (a Visual.Clip costs hit-testing
    /// precision in headless runs).
    /// </summary>
    private void UpdateWellClip(double y)
    {
        if (_well is null)
        {
            return;
        }
        if (y <= 0.5 || _innerRadius <= 0)
        {
            if (_well.Clip is not null)
            {
                _well.Clip = null;
                _well.InvalidateVisual();
            }
            return;
        }
        var size = _well.Bounds.Size;
        if (_wellClip is null || _wellClipSize != size || _well.Clip is null)
        {
            // Rounded bottom corners only.
            var geometry = new StreamGeometry();
            using (var c = geometry.Open())
            {
                var r = Math.Min(_innerRadius, Math.Min(size.Width, size.Height) / 2);
                c.BeginFigure(new Point(0, 0), true);
                c.LineTo(new Point(size.Width, 0));
                c.LineTo(new Point(size.Width, size.Height - r));
                c.ArcTo(new Point(size.Width - r, size.Height), new Size(r, r), 0, false, SweepDirection.Clockwise);
                c.LineTo(new Point(r, size.Height));
                c.ArcTo(new Point(0, size.Height - r), new Size(r, r), 0, false, SweepDirection.Clockwise);
                c.EndFigure(true);
            }
            _wellClip = geometry;
            _wellClipSize = size;
            _well.Clip = geometry;
            _well.InvalidateVisual();
        }
    }

    private static MotionValue<double> Value(Visual owner, double initial, Action changed)
    {
        var value = new MotionValue<double>(owner, initial, MotionVectors.Double);
        value.Changed += _ => changed();
        return value;
    }

    private void DisposeMotion()
    {
        _y?.Dispose();
        _tension?.Dispose();
        _trackScale?.Dispose();
        _trackOpacity?.Dispose();
        _fillOpacity?.Dispose();
        _runOpacity?.Dispose();
        _armedMix?.Dispose();
        _markFade?.Dispose();
        _markScale?.Dispose();
        _runLoop?.Dispose();
        _spinLoop?.Dispose();
        _runLoop = null;
        _spinLoop = null;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _viewport?.Dispose();
        _viewport = ArcViewport.Observe(this, size => PseudoClasses.Set(NarrowPseudoClass, size.Width <= NarrowWidth));
        if (ArcViewport.GetSize(this) is { } size)
        {
            PseudoClasses.Set(NarrowPseudoClass, size.Width <= NarrowWidth);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _viewport?.Dispose();
        _viewport = null;
        EndPointerTracking();
        if (_gesture.IsPointerPulling || _gesture.IsWheelPulling)
        {
            _gesture.Reset();
            _machine.Cancel();
            UpdateState();
            _y?.Jump(0);
            _tension?.Jump(0);
        }
        _wheelTimer?.Dispose();
        _wheelTimer = null;
        _work?.Cancel();
    }

    private void UpdateInks()
    {
        if (_doneGlyph is { } done)
        {
            done.Foreground = SuccessInk;
        }
        if (_failedGlyph is { } failed)
        {
            failed.Foreground = DangerInk;
        }
    }

    /// <summary>Writes the pull to the sheet, its corners, the indicator and the fill (every frame of a pull or spring).</summary>
    private void Apply()
    {
        var y = _y?.Value ?? 0;
        _sheetShift.Y = y;
        UpdateWellClip(y);
        // The peel radius changes only while y < 40; it is quantized to 0.5 px and written only when it changes, so
        // the sheet's shadow is not redrawn on every frame of a pull. (PART_SheetClip clips to a plain rectangle, so it
        // needs no radius.)
        var r = Math.Round(StretchRefreshMath.PeelRadius * StretchRefreshMath.Peel(y) * 2) / 2;
        if (r != _peelRadius || _innerRadius != _peelInner)
        {
            _peelRadius = r;
            _peelInner = _innerRadius;
            var corners = new CornerRadius(r, r, _innerRadius, _innerRadius);
            if (_sheetSurface is not null)
            {
                _sheetSurface.CornerRadius = corners;
            }
            if (_feedRing is not null)
            {
                _feedRing.CornerRadius = corners;
            }
        }
        if (_anchor is not null)
        {
            _anchor.Opacity = StretchRefreshMath.AnchorOpacity(y);
            _anchorShift.Y = StretchRefreshMath.AnchorOffset(y);
        }
        if (_meter is not null)
        {
            var fill = StretchRefreshMath.FillScale(y, Threshold);
            if (fill != _meter.FillScale)
            {
                _meter.FillScale = fill;
                _meter.InvalidateVisual();
            }
        }
    }

    private void WriteMeter()
    {
        if (_meter is null)
        {
            return;
        }
        _meter.TrackScale = _trackScale?.Value ?? 1;
        _meter.TrackOpacity = _trackOpacity?.Value ?? 1;
        _meter.FillOpacity = _fillOpacity?.Value ?? 1;
        _meter.RunOpacity = _runOpacity?.Value ?? 0;
        _meter.ArmedMix = _armedMix?.Value ?? 0;
        _meter.InvalidateVisual();
    }

    private void WriteMark()
    {
        if (_markHost is null)
        {
            return;
        }
        var f = Math.Clamp(_markFade?.Value ?? 0, 0, 1);
        _markHost.Opacity = f;
        _markHost.Effect = _markFull && f < 1 ? ArcBlur.Get(2 * (1 - f)) : null;
        var s = _markScale?.Value ?? 1;
        _markScaleTransform.ScaleX = _markScaleTransform.ScaleY = s;
    }
}
