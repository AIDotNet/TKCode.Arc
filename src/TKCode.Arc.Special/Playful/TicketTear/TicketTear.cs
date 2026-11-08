using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// An event ticket with a stub you tear off (Arc Ticket tear, design reference). Pulling the stub down or away tears the
/// perforation hole by hole, following the hand; letting go early springs the loose flap most of the way back, and once
/// free the stub falls, spins and fades. Enter or Space, or setting <see cref="IsTorn"/>, finishes the tear on its own.
/// </summary>
/// <remarks>
/// <see cref="ContentControl.Content"/> is the ticket's body and <see cref="Stub"/> what is printed on the stub.
/// <see cref="TemplatedControl.Background"/> is the body's paper and <see cref="StubBackground"/> the stub's. Under reduced motion the stub does not bend or fall, and
/// has no fibers: it only fades out.
/// </remarks>
[TemplatePart(BodyPart, typeof(TicketTearPiece))]
[TemplatePart(StubPart, typeof(TicketTearStub))]
[TemplatePart(StubShadowPart, typeof(Border))]
[TemplatePart(FlapPart, typeof(TicketTearFlap))]
[TemplatePart(StatusPart, typeof(TextBlock))]
public class TicketTear : ContentControl
{
    public const string BodyPart = "PART_Body";
    public const string StubPart = "PART_Stub";
    public const string StubShadowPart = "PART_StubShadow";
    public const string FlapPart = "PART_Flap";
    public const string StatusPart = "PART_Status";

    /// <summary>What is printed on the stub. Keep it short: a seat, a gate or a code.</summary>
    public static readonly StyledProperty<object?> StubProperty =
        AvaloniaProperty.Register<TicketTear, object?>(nameof(Stub));

    /// <summary>Template for <see cref="Stub"/>.</summary>
    public static readonly StyledProperty<IDataTemplate?> StubTemplateProperty =
        AvaloniaProperty.Register<TicketTear, IDataTemplate?>(nameof(StubTemplate));

    /// <summary>Paper of the stub. By default it stands out from the body: light on a dark theme, dark on a light one.</summary>
    public static readonly StyledProperty<IBrush?> StubBackgroundProperty =
        AvaloniaProperty.Register<TicketTear, IBrush?>(nameof(StubBackground));

    /// <summary>Text on the stub.</summary>
    public static readonly StyledProperty<IBrush?> StubForegroundProperty =
        AvaloniaProperty.Register<TicketTear, IBrush?>(nameof(StubForeground));

    /// <summary>Whether the stub is off. Setting true tears it off; setting false puts a fresh stub on. A ticket that starts true shows no tear.</summary>
    public static readonly StyledProperty<bool> IsTornProperty =
        AvaloniaProperty.Register<TicketTear, bool>(nameof(IsTorn), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Width of the stub in px. The body takes the rest of the ticket.</summary>
    public static readonly StyledProperty<double> StubWidthProperty =
        AvaloniaProperty.Register<TicketTear, double>(nameof(StubWidth), 104);

    /// <summary>Accessible name of the stub.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<TicketTear, string>(nameof(Label), "Tear off stub");

    /// <summary>Raised once the stub comes off the ticket.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> TornEvent =
        RoutedEvent.Register<TicketTear, RoutedEventArgs>(nameof(Torn), RoutingStrategies.Bubble);

    /// <summary>How far past the stub its shadow may reach.</summary>
    private const double ShadowReach = 80;

    private readonly TicketTearMotion _motion = new();
    private readonly LiveLoop _loop;
    private readonly List<Point> _points = [];
    private TicketTearPiece? _body;
    private TicketTearStub? _stub;
    private TicketTearFlap? _flap;
    private Border? _stubShadow;
    private TextBlock? _status;
    private TicketTearGeometry? _geometry;
    private RenderTargetBitmap? _snapshot;
    private double _shapedFront = double.NaN;
    private bool _shapedFull;
    private bool _bodyShaped;
    private bool _shapedBodyFibers;
    private bool _syncing;
    private bool _statusFlip;

    static TicketTear()
    {
        // The stub leaves while it has the focus, and the focus has to go somewhere that is still there. Not a tab stop.
        FocusableProperty.OverrideDefaultValue<TicketTear>(true);
        IsTabStopProperty.OverrideDefaultValue<TicketTear>(false);
    }

    public TicketTear()
    {
        _loop = new LiveLoop(this, Step) { MinimumLevel = ArcMotionLevel.Reduced };
        _motion.Freed += OnFreed;
        _motion.Finished += OnFinished;
    }

    /// <inheritdoc cref="StubProperty"/>
    public object? Stub
    {
        get => GetValue(StubProperty);
        set => SetValue(StubProperty, value);
    }

    /// <inheritdoc cref="StubTemplateProperty"/>
    public IDataTemplate? StubTemplate
    {
        get => GetValue(StubTemplateProperty);
        set => SetValue(StubTemplateProperty, value);
    }

    /// <inheritdoc cref="StubBackgroundProperty"/>
    public IBrush? StubBackground
    {
        get => GetValue(StubBackgroundProperty);
        set => SetValue(StubBackgroundProperty, value);
    }

    /// <inheritdoc cref="StubForegroundProperty"/>
    public IBrush? StubForeground
    {
        get => GetValue(StubForegroundProperty);
        set => SetValue(StubForegroundProperty, value);
    }

    /// <inheritdoc cref="IsTornProperty"/>
    public bool IsTorn
    {
        get => GetValue(IsTornProperty);
        set => SetValue(IsTornProperty, value);
    }

    /// <inheritdoc cref="StubWidthProperty"/>
    public double StubWidth
    {
        get => GetValue(StubWidthProperty);
        set => SetValue(StubWidthProperty, value);
    }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Raised once the stub comes off the ticket, whether the hand, the keyboard or <see cref="IsTorn"/> tore it.</summary>
    public event EventHandler<RoutedEventArgs>? Torn
    {
        add => AddHandler(TornEvent, value);
        remove => RemoveHandler(TornEvent, value);
    }

    internal TicketTearMotion Motion => _motion;

    internal TicketTearGeometry? Geometry => _geometry;

    internal TicketTearStub? StubPiece => _stub;

    internal RenderTargetBitmap? StubSnapshot => _snapshot;

    /// <summary>The pen of the paper's edge. The clip takes half of it away, so it is twice as wide as it looks.</summary>
    internal IPen? EdgePen { get; private set; }

    /// <summary>Whether the torn part of the stub is drawn as a flap: only with full motion, once anything has torn.</summary>
    internal bool ShowsFlap
        => _motion.CanMove && _motion.Progress > 0 && _motion.Phase is not TicketTearPhase.Gone;

    /// <summary>Tears the stub off, on its own, from wherever the tear has got to.</summary>
    public void Tear() => SetCurrentValue(IsTornProperty, true);

    /// <summary>Puts a fresh stub on.</summary>
    public void Reset() => SetCurrentValue(IsTornProperty, false);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        DetachParts();
        _body = e.NameScope.Find<TicketTearPiece>(BodyPart);
        _stub = e.NameScope.Find<TicketTearStub>(StubPart);
        _flap = e.NameScope.Find<TicketTearFlap>(FlapPart);
        _stubShadow = e.NameScope.Find<Border>(StubShadowPart);
        _status = e.NameScope.Find<TextBlock>(StatusPart);
        if (_body is not null)
        {
            _body.Owner = this;
        }
        if (_stub is not null)
        {
            _stub.Owner = this;
        }
        if (_flap is not null)
        {
            _flap.Owner = this;
        }
        UpdateEdgePen();
        InvalidateShapes();
    }

    private void DetachParts()
    {
        if (_body is not null)
        {
            _body.Owner = null;
        }
        if (_stub is not null)
        {
            _stub.Owner = null;
        }
        if (_flap is not null)
        {
            _flap.Owner = null;
        }
    }

    internal void OnStubTemplated()
    {
        if (_stub?.Piece is { } piece)
        {
            piece.Owner = this;
            piece.IsStub = true;
        }
        InvalidateShapes();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsTornProperty)
        {
            if (!_syncing)
            {
                OnIsTornSet(change.GetNewValue<bool>());
            }
        }
        else if (change.Property == CornerRadiusProperty || change.Property == StubWidthProperty)
        {
            InvalidateShapes();
        }
        else if (change.Property == BackgroundProperty || change.Property == StubBackgroundProperty || change.Property == BorderBrushProperty || change.Property == BorderThicknessProperty)
        {
            UpdateEdgePen();
            InvalidatePaper();
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        InvalidateShapes();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        InvalidateShapes();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // Nobody is watching: whatever was leaving has left.
        _motion.Snap();
        ReleaseSnapshot();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TicketTearAutomationPeer(this);

    // --- the tear ------------------------------------------------------------------------------------------------

    private void OnIsTornSet(bool torn)
    {
        if (!torn)
        {
            var shown = IsLoaded && _geometry is not null && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off;
            _motion.Reset(shown);
            ReleaseSnapshot();
            RestoreStub();
            Wake();
            return;
        }
        if (!IsLoaded || _geometry is null)
        {
            _motion.JumpGone();
            ApplyPose();
            return;
        }
        Prepare();
        _motion.Rip();
        Wake();
    }

    internal void BeginPull()
    {
        Prepare();
        _motion.Grab();
        Wake();
    }

    internal void Pull(Vector pull, Vector velocity)
    {
        _motion.CanMove = ArcMotion.IsFull(this);
        _motion.Drag(pull, velocity);
        Wake();
    }

    internal void EndPull(Vector velocity)
    {
        _motion.CanMove = ArcMotion.IsFull(this);
        _motion.Release(velocity);
        Wake();
    }

    private void Prepare()
    {
        _motion.CanMove = ArcMotion.IsFull(this);
        if (_motion.CanMove && _snapshot is null)
        {
            TakeSnapshot();
        }
    }

    private bool Step(double dt)
    {
        _motion.CanMove = ArcMotion.IsFull(this);
        var moving = _motion.Step(dt);
        ApplyPose();
        return moving;
    }

    private void Wake()
    {
        if (ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off)
        {
            // The loop does not run with motion off: everything jumps to where it is headed.
            _motion.CanMove = false;
            _motion.Snap();
        }
        else
        {
            _loop.Start();
        }
        ApplyPose();
    }

    private void OnFreed()
    {
        _syncing = true;
        SetCurrentValue(IsTornProperty, true);
        _syncing = false;
        // The stub is about to go away; the focus stays on the ticket instead of being lost.
        if (_stub?.IsFocused == true)
        {
            Focus(NavigationMethod.Unspecified);
        }
        if (_stub is not null)
        {
            _stub.IsHitTestVisible = false;
            _stub.Focusable = false;
        }
        Announce("Stub torn off");
        RaiseEvent(new RoutedEventArgs(TornEvent, this));
    }

    private void OnFinished()
    {
        ReleaseSnapshot();
    }

    private void RestoreStub()
    {
        if (_stub is not null)
        {
            _stub.IsHitTestVisible = true;
            _stub.Focusable = true;
        }
    }

    private void Announce(string message)
    {
        if (_status is not null)
        {
            // The same message twice in a row must still change the text to be read again.
            _statusFlip = !_statusFlip;
            _status.Text = _statusFlip ? message : message + " ";
        }
    }

    // --- the paper -----------------------------------------------------------------------------------------------

    internal void InvalidateShapes()
    {
        var size = Bounds.Size;
        if (_stub is not { } stub || size.Width < 1 || size.Height < 1)
        {
            return;
        }
        var stubWidth = Math.Min(StubWidth, size.Width);
        _geometry = new TicketTearGeometry(size.Width - stubWidth, stubWidth, size.Height, CornerRadius.TopLeft,
            stub.NotchRadius, stub.PerforationInset, stub.HoleRadius, stub.HolePitch);
        _motion.Configure(_geometry.Height, _geometry.Notch, _geometry.Holes);
        if (IsTorn && _motion.Phase is TicketTearPhase.Attached && _motion.Progress <= 0)
        {
            _motion.JumpGone();
        }
        _shapedFront = double.NaN;
        _bodyShaped = false;
        ApplyPose();
    }

    /// <summary>Writes the pose: the paper's shapes when the tear has moved, what is visible, and the flap's frame.</summary>
    private void ApplyPose()
    {
        if (_geometry is not { } geometry || _body is null || _stub is null)
        {
            return;
        }
        var full = _motion.CanMove;
        var front = _motion.Front;
        if (!front.Equals(_shapedFront) || full != _shapedFull)
        {
            _shapedFront = front;
            _shapedFull = full;
            UpdateShapes(geometry, front, full);
        }

        var phase = _motion.Phase;
        // With full motion the flap carries a free stub; otherwise the stub itself fades where it is.
        var stubShown = phase is not TicketTearPhase.Gone && (phase is not TicketTearPhase.Falling || !full);
        _stub.IsVisible = stubShown;
        _stub.Opacity = phase is TicketTearPhase.Falling ? _motion.FallOpacity : _motion.FreshOpacity;
        if (_stubShadow is { } shadow)
        {
            // Only ever faded: it keeps the stub's column, so the ticket does not change size when the stub leaves.
            shadow.Opacity = stubShown ? _stub.Opacity : 0;
        }
        if (_flap is { } flap)
        {
            flap.IsVisible = ShowsFlap;
            flap.InvalidateVisual();
        }
    }

    private void UpdateShapes(TicketTearGeometry geometry, double front, bool full)
    {
        var torn = _motion.Progress > 0;
        if (!_bodyShaped || full != _shapedBodyFibers)
        {
            _bodyShaped = true;
            _shapedBodyFibers = full;
            geometry.BuildBody(_points, full);
            var bodyFill = TicketTearGeometry.ToGeometry(_points);
            geometry.BuildBody(_points, false);
            _body!.SetShape(bodyFill, TicketTearGeometry.ToGeometry(_points));
        }

        if (_stub?.Piece is not { } piece)
        {
            return;
        }
        var hinge = 0.0;
        if (full && torn && front < geometry.Height)
        {
            // The torn part is the flap's; the real stub is what is still on the ticket.
            hinge = geometry.SnapHinge(front);
            geometry.BuildAttachedStub(_points, hinge);
        }
        else
        {
            // Without motion a tear shows as the line itself, down the whole stub.
            hinge = torn ? (front >= geometry.Height ? geometry.Height : geometry.SnapHinge(front)) : 0;
            geometry.BuildStub(_points, hinge, false);
        }
        var shape = geometry.ToGeometryWithHoles(_points, hinge);
        piece.SetShape(shape, shape);
        if (_stubShadow is { } shadow)
        {
            // Only the part still on the ticket casts the ticket's shadow.
            shadow.Clip = hinge > 0
                ? new RectangleGeometry(new Rect(-ShadowReach, hinge, geometry.StubWidth + 2 * ShadowReach, geometry.Height - hinge + ShadowReach))
                : null;
            shadow.InvalidateVisual();
        }
    }

    private void UpdateEdgePen()
    {
        var width = BorderThickness.Left * 2;
        EdgePen = BorderBrush is { } brush && width > 0
            ? new ImmutablePen(brush.ToImmutable(), width)
            : null;
    }

    private void InvalidatePaper()
    {
        _body?.InvalidateVisual();
        _stub?.Piece?.InvalidateVisual();
        _flap?.InvalidateVisual();
    }

    private void TakeSnapshot()
    {
        if (_stub?.Piece is not { } piece || TopLevel.GetTopLevel(this) is not { } top || piece.Bounds.Width < 1 || piece.Bounds.Height < 1)
        {
            return;
        }
        try
        {
            var scaling = top.RenderScaling;
            var pixels = new PixelSize(
                Math.Max(1, (int)Math.Ceiling(piece.Bounds.Width * scaling)),
                Math.Max(1, (int)Math.Ceiling(piece.Bounds.Height * scaling)));
            _snapshot = new RenderTargetBitmap(pixels, new Vector(96 * scaling, 96 * scaling));
            _snapshot.Render(piece);
        }
        catch (Exception)
        {
            // No picture (for example a platform without render targets): the flap is plain paper.
            ReleaseSnapshot();
        }
    }

    private void ReleaseSnapshot()
    {
        _snapshot?.Dispose();
        _snapshot = null;
    }

    private sealed class TicketTearAutomationPeer(TicketTear owner) : ContentControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
    }
}
