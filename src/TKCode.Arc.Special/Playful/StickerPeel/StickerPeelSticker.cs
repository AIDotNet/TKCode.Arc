using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// One sticker on a <see cref="StickerPeel"/> surface: a button that peels, is carried and sticks down again. It is
/// arranged at the place it is stuck and moves with its render transform only (translation, its own angle and the small
/// rise of a carried sticker), so peeling and carrying never touch layout. The face is clipped to the part that is still
/// stuck; <see cref="StickerPeelPaper"/> draws the flap and the shadow.
/// </summary>
[TemplatePart(UnderlayPart, typeof(StickerPeelPaper))]
[TemplatePart(OverlayPart, typeof(StickerPeelPaper))]
[TemplatePart(FacePart, typeof(Border))]
[TemplatePart(InnerPart, typeof(Border))]
internal sealed class StickerPeelSticker : ContentControl
{
    public const string UnderlayPart = "PART_Underlay";
    public const string OverlayPart = "PART_Overlay";
    public const string FacePart = "PART_Face";
    public const string InnerPart = "PART_Inner";

    /// <summary>Corner radius of a rounded die cut, in px.</summary>
    public const double RoundedRadius = 14;

    private const double KeyStep = 10;
    private const double KeyStepLarge = 40;

    private readonly StickerPeelMotion _motion = new();
    private readonly MatrixTransform _transform = new();
    private StickerPeelPaper? _underlay;
    private StickerPeelPaper? _overlay;
    private Border? _face;
    private Border? _inner;
    private bool _clipped;
    private bool _pointerHeld;
    private bool _keyboardCarry;
    private ulong _lastTimestamp;
    private double _restRotation;
    private Point _committedHome;

    static StickerPeelSticker()
    {
        FocusableProperty.OverrideDefaultValue<StickerPeelSticker>(true);
    }

    public StickerPeelSticker(StickerPeelItem item)
    {
        Item = item;
        Content = item.Content;
        RenderTransform = _transform;
        RenderTransformOrigin = RelativePoint.Center;
        AutomationProperties.SetName(this, item.Label);
        AutomationProperties.SetHelpText(this,
            "Press Enter to pick the sticker up, arrow keys to move it and Enter to stick it down. Escape puts it back.");
        LostFocus += OnFocusLeft;
        _motion.Settled += OnSettled;
        _motion.PeeledOff += () => Layer?.Announce(item.Label + " peeled off");
        UpdateTransform();
    }

    public StickerPeelItem Item { get; }

    public StickerPeelMotion Motion => _motion;

    /// <summary>The centre of the place the sticker is stuck, in the layer's coordinates.</summary>
    public Point HomeCenter { get; internal set; }

    internal bool HasHome { get; set; }

    /// <summary>Corner radius of the die cut for the current size.</summary>
    public double OutlineRadius => StickerPeelGeometry.CornerRadius(Item.Shape, Bounds.Width, Bounds.Height, RoundedRadius);

    public bool IsCarried => _motion.IsCarried;

    private StickerPeelLayer? Layer => Parent as StickerPeelLayer;

    /// <summary>The sticker's own angle in degrees when it is stuck down.</summary>
    public double RestRotation
    {
        get => _restRotation;
        set
        {
            if (_restRotation.Equals(value))
            {
                return;
            }
            _restRotation = value;
            _motion.Rotation = value * Math.PI / 180;
            UpdateTransform();
            InvalidateVisuals();
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _underlay = e.NameScope.Find<StickerPeelPaper>(UnderlayPart);
        _overlay = e.NameScope.Find<StickerPeelPaper>(OverlayPart);
        _face = e.NameScope.Find<Border>(FacePart);
        _inner = e.NameScope.Find<Border>(InnerPart);
        if (_underlay is not null)
        {
            _underlay.Owner = this;
        }
        if (_overlay is not null)
        {
            _overlay.Owner = this;
        }
        ApplyShape();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        ApplyShape();
    }

    private void ApplyShape()
    {
        var size = Bounds.Size;
        if (size.Width < 1 || size.Height < 1)
        {
            return;
        }
        _motion.Configure(size.Width, size.Height, Item.Shape, RoundedRadius);
        var radius = OutlineRadius;
        CornerRadius = new CornerRadius(radius);
        if (_inner is not null)
        {
            _inner.CornerRadius = new CornerRadius(Math.Max(0, radius - (_face?.Padding.Left ?? 0)));
        }
        InvalidateVisuals();
    }

    // --- frame -----------------------------------------------------------------------------------------------

    /// <summary>Advances the sticker by one frame and applies its pose.</summary>
    public bool Step(double dt, bool full)
    {
        _motion.CanMove = full;
        var moving = _motion.Step(dt);
        Apply();
        return moving;
    }

    /// <summary>Jumps to where the sticker is headed (motion off).</summary>
    internal void Snap()
    {
        _motion.Snap();
        Apply();
    }

    /// <summary>Writes the pose: the render transform, the face clip and the repaint of the shadow and the flap.</summary>
    internal void Apply()
    {
        UpdateTransform();
        var folded = _motion.HasFold;
        if (folded && _face is not null)
        {
            // A new geometry every frame: the compositor works out what to repaint from the geometry's old and new
            // bounds, so a geometry rewritten in place leaves the previous frame's pixels behind.
            var clip = new StreamGeometry();
            using (var path = clip.Open())
            {
                var points = _motion.StuckPoints;
                if (points.Length >= 3)
                {
                    path.BeginFigure(points[0], true);
                    for (var i = 1; i < points.Length; i++)
                    {
                        path.LineTo(points[i]);
                    }
                    path.EndFigure(true);
                }
            }
            _face.Clip = clip;
            // A changed clip geometry reaches the compositor only through an invalidation.
            _face.InvalidateVisual();
            _clipped = true;
        }
        else if (_clipped && _face is not null)
        {
            _face.Clip = null;
            _face.InvalidateVisual();
            _clipped = false;
        }
        InvalidateVisuals();
    }

    private void InvalidateVisuals()
    {
        _underlay?.InvalidateVisual();
        _overlay?.InvalidateVisual();
    }

    private void UpdateTransform()
    {
        var scale = 1 + StickerPeelMotion.CarryRise * _motion.Lift;
        _transform.Matrix = Matrix.CreateScale(scale, scale)
                            * Matrix.CreateRotation(_motion.Rotation)
                            * Matrix.CreateTranslation(_motion.Offset.X, _motion.Offset.Y);
    }

    /// <summary>Allowed offsets for a carried sticker: its rotated box stays inside the surface.</summary>
    private void UpdateLimits()
    {
        if (Layer is not { } layer)
        {
            return;
        }
        var (sin, cos) = Math.SinCos(_motion.Rotation);
        var w = Bounds.Width;
        var h = Bounds.Height;
        var hx = (Math.Abs(cos) * w + Math.Abs(sin) * h) / 2;
        var hy = (Math.Abs(sin) * w + Math.Abs(cos) * h) / 2;
        var area = layer.Bounds.Size;
        Axis(hx, area.Width, HomeCenter.X, out var minX, out var maxX);
        Axis(hy, area.Height, HomeCenter.Y, out var minY, out var maxY);
        _motion.MinOffset = new Vector(minX, minY);
        _motion.MaxOffset = new Vector(maxX, maxY);

        static void Axis(double half, double size, double home, out double min, out double max)
        {
            min = half - home;
            max = size - half - home;
            if (min > max)
            {
                min = max = (min + max) / 2;
            }
        }
    }

    private void Prepare()
    {
        _motion.CanMove = ArcMotion.IsFull(this);
        UpdateLimits();
    }

    internal void Glide(Vector delta)
    {
        Prepare();
        _motion.Glide(delta);
        Layer?.Wake();
    }

    private void OnSettled(bool dropped)
    {
        // The sticker is stuck where it landed: the place it is stuck moves there and the offset returns to zero.
        var landed = new Point(HomeCenter.X + _motion.Offset.X, HomeCenter.Y + _motion.Offset.Y);
        HomeCenter = landed;
        _motion.Commit();
        Apply();
        var moved = Math.Abs(landed.X - _committedHome.X) > 0.5 || Math.Abs(landed.Y - _committedHome.Y) > 0.5;
        _committedHome = landed;
        Layer?.StickerSettled(this, moved);
    }

    internal void RememberHome() => _committedHome = HomeCenter;

    // --- pointer -----------------------------------------------------------------------------------------------

    private Vector Rel(PointerEventArgs e)
    {
        var p = e.GetPosition(Layer);
        return new Vector(p.X - HomeCenter.X, p.Y - HomeCenter.Y);
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        Hover(e);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (!_pointerHeld)
        {
            _motion.Unhover();
            Layer?.Wake();
        }
    }

    private void Hover(PointerEventArgs e)
    {
        // Touch has no hover.
        if (Layer is null || _pointerHeld || e.Pointer.Type != PointerType.Mouse)
        {
            return;
        }
        Prepare();
        _motion.Hover(Rel(e));
        Layer.Wake();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Layer is not { } layer || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || _keyboardCarry)
        {
            return;
        }
        Focus(NavigationMethod.Pointer);
        Prepare();
        _pointerHeld = true;
        _lastTimestamp = e.Timestamp;
        e.Pointer.Capture(this);
        layer.BringToFront(this);
        var before = _motion.Phase;
        _motion.Grab(Rel(e));
        if (_motion.Phase == StickerPeelPhase.Carried && before != StickerPeelPhase.Carried)
        {
            layer.Announce(Item.Label + " picked up");
        }
        layer.Wake();
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_pointerHeld)
        {
            Hover(e);
            return;
        }
        var dt = Math.Max(1, e.Timestamp - _lastTimestamp) / 1000.0;
        _lastTimestamp = e.Timestamp;
        UpdateLimits();
        _motion.Drag(Rel(e), dt);
        Layer?.Wake();
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pointerHeld)
        {
            return;
        }
        EndPointer();
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_pointerHeld)
        {
            EndPointer();
        }
    }

    private void EndPointer()
    {
        _pointerHeld = false;
        var wasCarried = _motion.IsCarried;
        _motion.Release();
        if (wasCarried)
        {
            Layer?.Announce(Item.Label + " stuck down");
        }
        Layer?.Wake();
    }

    // --- keyboard ------------------------------------------------------------------------------------------------

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.KeyModifiers is not (KeyModifiers.None or KeyModifiers.Shift))
        {
            return;
        }
        switch (e.Key)
        {
            case Key.Enter:
            case Key.Space:
                ToggleCarry();
                e.Handled = true;
                break;
            case Key.Escape when _keyboardCarry:
                _keyboardCarry = false;
                _motion.Cancel();
                Layer?.Announce(Item.Label + " put back");
                Layer?.Wake();
                e.Handled = true;
                break;
            case Key.Left or Key.Right or Key.Up or Key.Down when _keyboardCarry:
                var step = e.KeyModifiers == KeyModifiers.Shift ? KeyStepLarge : KeyStep;
                UpdateLimits();
                _motion.MoveBy(e.Key switch
                {
                    Key.Left => new Vector(-step, 0),
                    Key.Right => new Vector(step, 0),
                    Key.Up => new Vector(0, -step),
                    _ => new Vector(0, step)
                });
                Layer?.Wake();
                e.Handled = true;
                break;
        }
    }

    private void OnFocusLeft(object? sender, RoutedEventArgs e)
    {
        // Leaving a sticker you are carrying sticks it down.
        if (_keyboardCarry)
        {
            Stick();
        }
    }

    /// <summary>Picks the sticker up with the keyboard, or sticks a carried one down.</summary>
    internal void ToggleCarry()
    {
        if (_keyboardCarry)
        {
            Stick();
            return;
        }
        if (_pointerHeld || Layer is not { } layer)
        {
            return;
        }
        Prepare();
        _keyboardCarry = true;
        layer.BringToFront(this);
        _motion.PickUp();
        layer.Announce(Item.Label + " picked up");
        layer.Wake();
    }

    private void Stick()
    {
        _keyboardCarry = false;
        _motion.Drop();
        Layer?.Announce(Item.Label + " stuck down");
        Layer?.Wake();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new StickerAutomationPeer(this);

    private sealed class StickerAutomationPeer(StickerPeelSticker owner) : ControlAutomationPeer(owner), IToggleProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

        protected override string GetLocalizedControlTypeCore() => "sticker";

        protected override string? GetNameCore() => owner.Item.Label;

        public ToggleState ToggleState => owner.IsCarried ? ToggleState.On : ToggleState.Off;

        public void Toggle() => owner.ToggleCarry();
    }
}
