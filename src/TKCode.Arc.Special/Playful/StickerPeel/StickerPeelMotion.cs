using Avalonia;

namespace TKCode.Arc;

/// <summary>Where a sticker is in its life: stuck down, being peeled, carried, or landing.</summary>
internal enum StickerPeelPhase
{
    Rest,
    Peeling,
    Carried,
    Settling
}

/// <summary>
/// The pose of one sticker (Arc Sticker peel, design reference): the fold of its corner, its offset from the place it is
/// stuck, its lift and its shadow. All springs are critically damped, so the paper trails the finger slightly and settles
/// without overshoot. Pure maths; <see cref="StickerPeelSticker"/> feeds it pointer and key input and draws the result.
/// </summary>
/// <remarks>
/// Coordinates: pointer positions are given relative to the sticker's stuck centre in surface axes. The fold works in the
/// sticker's own axes (origin at its top left, before rotation); the canonical fold frame has its origin on the fold
/// line, x along the line and y toward the stuck side, where the mirrored flap lies.
/// </remarks>
internal sealed class StickerPeelMotion
{
    /// <summary>How far a hovering mouse lifts a corner, as a pull in px.</summary>
    public const double HoverPull = 10;

    /// <summary>A carried sticker rises by this share of its size.</summary>
    public const double CarryRise = 0.03;

    public const double ShadowFadeOutSeconds = 0.2;
    public const double ShadowFadeInSeconds = 0.12;

    private const double PeelOmega = 26;
    private const double HoverOmega = 30;
    private const double RelaxOmega = 20;
    private const double BodyOmega = 30;
    private const double LiftOmega = 22;
    private const double RubberDimension = 90;
    private const double RubberStiffness = 0.55;

    private readonly Point[] _outline = new Point[StickerPeelGeometry.MaxOutlinePoints];
    private readonly Point[] _scratch = new Point[StickerPeelGeometry.MaxOutlinePoints + 4];
    private readonly Point[] _bigOutline = new Point[4];
    private readonly Point[] _flap = new Point[StickerPeelGeometry.MaxOutlinePoints + 4];
    private readonly Point[] _stuck = new Point[8];
    private int _outlineCount;
    private int _flapCount;
    private int _stuckCount;

    private double _foldVx, _foldVy;
    private double _offsetVx, _offsetVy;
    private double _liftV;
    private Vector _grab;
    private Vector _pickupOffset;
    private Point _pressLocal;
    private Vector _lastRel;
    private double _speed;
    private bool _hovering;
    private bool _hasPending;
    private Point _pendingAnchor;
    private bool _dropped;
    private bool _hasAnchor;

    /// <summary>Size of the die cut in px.</summary>
    public double Width { get; private set; }

    public double Height { get; private set; }

    public StickerPeelShape Shape { get; private set; }

    public double RoundedRadius { get; private set; } = 14;

    /// <summary>The sticker's own angle in radians.</summary>
    public double Rotation { get; set; }

    /// <summary>False under reduced motion: no peel, no hover lift, the sticker follows the pointer 1:1.</summary>
    public bool CanMove { get; set; } = true;

    public StickerPeelPhase Phase { get; private set; }

    /// <summary>The corner (or rim point) the sticker peels from, in sticker coordinates.</summary>
    public Point Anchor { get; private set; }

    /// <summary>Unit vector from the anchor into the sticker.</summary>
    public Vector Inward { get; private set; }

    /// <summary>How far the sticker reaches from the anchor along <see cref="Inward"/>.</summary>
    public double Extent { get; private set; }

    /// <summary>The displayed pull: the anchor lands at <c>Anchor + Fold</c> once mirrored.</summary>
    public Vector Fold { get; private set; }

    public Vector FoldTarget { get; private set; }

    /// <summary>Offset of the sticker from its stuck centre, in surface px.</summary>
    public Vector Offset { get; private set; }

    public Vector OffsetTarget { get; private set; }

    /// <summary>Allowed offsets: a carried sticker is clamped (or rubber-banded) inside these.</summary>
    public Vector MinOffset { get; set; } = new(double.NegativeInfinity, double.NegativeInfinity);

    public Vector MaxOffset { get; set; } = new(double.PositiveInfinity, double.PositiveInfinity);

    /// <summary>0 stuck down, 1 carried (the sticker is scaled by <c>1 + CarryRise * Lift</c>).</summary>
    public double Lift { get; private set; }

    public double LiftTarget { get; private set; }

    /// <summary>0 to 1 strength of the drop shadow.</summary>
    public double Shadow { get; private set; }

    public double ShadowTarget { get; private set; }

    /// <summary>Whether the paper is folded back at all.</summary>
    public bool HasFold { get; private set; }

    /// <summary>A point on the fold line.</summary>
    public Point FoldOrigin { get; private set; }

    /// <summary>Unit normal of the fold line, pointing from the anchor to where it lands.</summary>
    public Vector FoldNormal { get; private set; }

    /// <summary>Unit vector along the fold line.</summary>
    public Vector FoldTangent { get; private set; }

    /// <summary>The flap (the pulled part mirrored across the fold) in canonical fold coordinates.</summary>
    public ReadOnlySpan<Point> FlapPoints => _flap.AsSpan(0, _flapCount);

    /// <summary>The part still stuck, in sticker coordinates; it is the clip of the sticker's face.</summary>
    public ReadOnlySpan<Point> StuckPoints => _stuck.AsSpan(0, _stuckCount);

    public ReadOnlySpan<Point> Outline => _outline.AsSpan(0, _outlineCount);

    /// <summary>Whether the sticker is held, by pointer or keyboard.</summary>
    public bool IsCarried => Phase == StickerPeelPhase.Carried;

    /// <summary>Raised once the sticker has landed and everything but the shadow is at rest.</summary>
    public event Action<bool>? Settled;

    /// <summary>Raised when a peel comes off the surface and the sticker is carried.</summary>
    public event Action? PeeledOff;

    /// <summary>Sets the die cut: size, outline and corner radius.</summary>
    public void Configure(double width, double height, StickerPeelShape shape, double roundedRadius)
    {
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);
        Shape = shape;
        RoundedRadius = roundedRadius;
        var r = StickerPeelGeometry.CornerRadius(shape, Width, Height, roundedRadius);
        _outlineCount = StickerPeelGeometry.BuildOutline(_outline, 0, 0, Width, Height, r);
        const double pad = 12;
        _bigOutline[0] = new Point(-pad, -pad);
        _bigOutline[1] = new Point(Width + pad, -pad);
        _bigOutline[2] = new Point(Width + pad, Height + pad);
        _bigOutline[3] = new Point(-pad, Height + pad);
        if (_hasAnchor)
        {
            SetAnchor(Anchor);
        }
        UpdateGeometry();
    }

    /// <summary>Sticker coordinates of a point given relative to the stuck centre in surface axes.</summary>
    public Point ToLocal(Vector rel)
    {
        var v = StickerPeelGeometry.Rotate(rel, -Rotation);
        return new Point(v.X + Width / 2, v.Y + Height / 2);
    }

    // --- input -----------------------------------------------------------------------------------------------

    /// <summary>A mouse is over the sticker: lift the nearest corner a few px. Moving to another corner lays the first flat.</summary>
    public void Hover(Vector rel)
    {
        if (!CanMove || Phase != StickerPeelPhase.Rest)
        {
            return;
        }
        _hovering = true;
        StickerPeelGeometry.Anchor(Shape, Width, Height, RoundedRadius, ToLocal(rel), true, out var anchor, out _);
        if (!_hasAnchor)
        {
            SetAnchor(anchor);
        }
        if (Distance(anchor, Anchor) > 0.5)
        {
            _hasPending = true;
            _pendingAnchor = anchor;
            FoldTarget = default;
        }
        else
        {
            _hasPending = false;
            FoldTarget = Inward * HoverPull;
        }
    }

    /// <summary>The mouse left the sticker: the lifted corner lays flat.</summary>
    public void Unhover()
    {
        _hovering = false;
        _hasPending = false;
        if (Phase == StickerPeelPhase.Rest)
        {
            FoldTarget = default;
        }
    }

    /// <summary>
    /// The pointer went down on the sticker. A stuck sticker starts to peel from the corner nearest the pointer (under
    /// reduced motion it is picked up directly); a sticker that is still landing is caught where it is.
    /// </summary>
    public void Grab(Vector rel)
    {
        _lastRel = rel;
        _speed = 0;
        _hovering = false;
        _hasPending = false;
        if (!CanMove || Phase == StickerPeelPhase.Settling || Phase == StickerPeelPhase.Carried)
        {
            Carry(rel);
            return;
        }
        _pressLocal = ToLocal(rel);
        StickerPeelGeometry.Anchor(Shape, Width, Height, RoundedRadius, _pressLocal, false, out var anchor, out _);
        if (!_hasAnchor || Distance(anchor, Anchor) > 0.5)
        {
            // A different corner than the hover lifted: start flat there.
            Fold = default;
            _foldVx = _foldVy = 0;
        }
        SetAnchor(anchor);
        Phase = StickerPeelPhase.Peeling;
        FoldTarget = default;
        ShadowTarget = 0;
        UpdateGeometry();
    }

    /// <summary>The pointer moved while held. <paramref name="dt"/> is the time since the last move, for the tug speed.</summary>
    public void Drag(Vector rel, double dt)
    {
        var moved = (rel - _lastRel).Length;
        _speed = _speed * 0.6 + 0.4 * (dt > 1e-4 ? moved / dt : 0);
        _lastRel = rel;
        if (Phase == StickerPeelPhase.Peeling)
        {
            var local = ToLocal(rel);
            var pull = new Vector(local.X - _pressLocal.X, local.Y - _pressLocal.Y);
            FoldTarget = StickerPeelGeometry.Constrain(Inward, pull);
            var along = FoldTarget.Length;
            var away = pull.Length;
            // Past the middle, a quick tug, or carried clearly away: it comes off.
            if (along >= Extent * 0.98 || away >= Extent * 1.2 || (_speed > 800 && away >= Extent * 0.4))
            {
                PeelOff(rel);
            }
        }
        else if (Phase == StickerPeelPhase.Carried)
        {
            OffsetTarget = Rubber(rel + _grab);
        }
    }

    /// <summary>The pointer was released: a peel lays back down, a carried sticker lands (clamped inside the surface).</summary>
    public void Release()
    {
        if (Phase == StickerPeelPhase.Peeling)
        {
            Phase = StickerPeelPhase.Settling;
            _dropped = false;
            FoldTarget = default;
        }
        else if (Phase == StickerPeelPhase.Carried)
        {
            Land(Clamp(OffsetTarget), true);
        }
    }

    /// <summary>Keyboard: picks the sticker up where it is.</summary>
    public void PickUp()
    {
        _hovering = false;
        _hasPending = false;
        _pickupOffset = Offset;
        Phase = StickerPeelPhase.Carried;
        OffsetTarget = Offset;
        FoldTarget = default;
        LiftTarget = 1;
        ShadowTarget = 1;
    }

    /// <summary>Keyboard: moves a carried sticker by <paramref name="delta"/>, kept inside the surface.</summary>
    public void MoveBy(Vector delta)
    {
        if (Phase == StickerPeelPhase.Carried)
        {
            OffsetTarget = Clamp(OffsetTarget + delta);
        }
    }

    /// <summary>Keyboard: sticks a carried sticker down where it is.</summary>
    public void Drop()
    {
        if (Phase == StickerPeelPhase.Carried)
        {
            Land(Clamp(OffsetTarget), true);
        }
    }

    /// <summary>Keyboard: puts a carried sticker back where it was picked up.</summary>
    public void Cancel()
    {
        if (Phase == StickerPeelPhase.Carried)
        {
            Land(_pickupOffset, false);
        }
    }

    /// <summary>
    /// The place the sticker is stuck moved by <paramref name="delta"/> (an outside change of its position): keep it
    /// where it is on screen and let it glide to the new place.
    /// </summary>
    public void Glide(Vector delta)
    {
        Offset += delta;
        if (Phase == StickerPeelPhase.Carried)
        {
            OffsetTarget += delta;
            _pickupOffset += delta;
            return;
        }
        OffsetTarget = default;
        if (CanMove && Offset.Length > 0.05)
        {
            Phase = StickerPeelPhase.Settling;
            _dropped = false;
        }
        else
        {
            Offset = default;
        }
    }

    /// <summary>Moves the stuck centre to where the sticker is, so the offset is zero again (after a drop).</summary>
    public void Commit()
    {
        Offset = default;
        OffsetTarget = default;
        _offsetVx = _offsetVy = 0;
    }

    // --- frame ---------------------------------------------------------------------------------------------

    /// <summary>With motion off nothing animates: everything goes straight to where it is headed and a landing settles at once.</summary>
    public void Snap()
    {
        CanMove = false;
        Shadow = ShadowTarget;
        Step(0);
    }

    /// <summary>Advances the springs by <paramref name="dt"/> seconds; returns whether anything is still moving.</summary>
    public bool Step(double dt)
    {
        dt = Math.Clamp(dt, 0, 1.0 / 20);
        if (_hasPending && Fold.Length < 0.8 && Phase == StickerPeelPhase.Rest)
        {
            SetAnchor(_pendingAnchor);
            _hasPending = false;
            FoldTarget = _hovering ? Inward * HoverPull : default;
        }

        var shadowRate = dt / (ShadowTarget > Shadow ? ShadowFadeInSeconds : ShadowFadeOutSeconds);
        Shadow = Shadow < ShadowTarget ? Math.Min(ShadowTarget, Shadow + shadowRate) : Math.Max(ShadowTarget, Shadow - shadowRate);

        if (!CanMove)
        {
            Fold = default;
            FoldTarget = default;
            _foldVx = _foldVy = 0;
            Offset = OffsetTarget;
            _offsetVx = _offsetVy = 0;
            Lift = LiftTarget;
            _liftV = 0;
        }
        else
        {
            var foldOmega = Phase == StickerPeelPhase.Peeling ? PeelOmega : FoldTarget.Length == 0 ? RelaxOmega : HoverOmega;
            var fx = Fold.X;
            var fy = Fold.Y;
            Spring(ref fx, ref _foldVx, FoldTarget.X, foldOmega, dt);
            Spring(ref fy, ref _foldVy, FoldTarget.Y, foldOmega, dt);
            Fold = new Vector(fx, fy);
            var ox = Offset.X;
            var oy = Offset.Y;
            Spring(ref ox, ref _offsetVx, OffsetTarget.X, BodyOmega, dt);
            Spring(ref oy, ref _offsetVy, OffsetTarget.Y, BodyOmega, dt);
            Offset = new Vector(ox, oy);
            var lift = Lift;
            Spring(ref lift, ref _liftV, LiftTarget, LiftOmega, dt);
            Lift = lift;
        }

        if (Phase == StickerPeelPhase.Settling && AtRest())
        {
            Fold = default;
            Offset = OffsetTarget;
            Lift = LiftTarget;
            _foldVx = _foldVy = _offsetVx = _offsetVy = _liftV = 0;
            Phase = StickerPeelPhase.Rest;
            var dropped = _dropped;
            _dropped = false;
            UpdateGeometry();
            Settled?.Invoke(dropped);
        }
        UpdateGeometry();
        return Phase == StickerPeelPhase.Peeling || Phase == StickerPeelPhase.Settling
               || Shadow != ShadowTarget
               || !Near(Fold, FoldTarget) || Math.Abs(_foldVx) + Math.Abs(_foldVy) > 0.05
               || !Near(Offset, OffsetTarget) || Math.Abs(_offsetVx) + Math.Abs(_offsetVy) > 0.05
               || Math.Abs(Lift - LiftTarget) > 0.002 || Math.Abs(_liftV) > 0.002
               || (_hasPending);
    }

    // --- internals -----------------------------------------------------------------------------------------

    private bool AtRest()
        => Near(Offset, OffsetTarget) && Math.Abs(_offsetVx) + Math.Abs(_offsetVy) < 1
           && Fold.Length < 0.05 && Math.Abs(Lift - LiftTarget) < 0.002;

    private static bool Near(Vector a, Vector b) => (a - b).Length < 0.05;

    private static double Distance(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    private void SetAnchor(Point anchor)
    {
        // An anchor lies on the rim or the corner arc, so asking for the anchor nearest to it returns it with its direction.
        StickerPeelGeometry.Anchor(Shape, Width, Height, RoundedRadius, anchor, false, out var a, out var inward);
        Anchor = a;
        Inward = inward;
        Extent = StickerPeelGeometry.Extent(Outline, a, inward);
        _hasAnchor = true;
    }

    private void Carry(Vector rel)
    {
        Phase = StickerPeelPhase.Carried;
        _pickupOffset = Offset;
        _grab = Offset - rel;
        OffsetTarget = Offset;
        FoldTarget = default;
        LiftTarget = 1;
        ShadowTarget = 1;
        _dropped = false;
    }

    private void PeelOff(Vector rel)
    {
        Phase = StickerPeelPhase.Carried;
        _pickupOffset = Offset;
        _grab = Offset - rel;
        OffsetTarget = Offset;
        FoldTarget = default;
        LiftTarget = 1;
        ShadowTarget = 1;
        PeeledOff?.Invoke();
    }

    private void Land(Vector target, bool dropped)
    {
        Phase = StickerPeelPhase.Settling;
        _dropped = dropped;
        OffsetTarget = target;
        FoldTarget = default;
        LiftTarget = 0;
        ShadowTarget = 0;
    }

    private Vector Clamp(Vector v) => new(
        Math.Clamp(v.X, MinOffset.X, Math.Max(MinOffset.X, MaxOffset.X)),
        Math.Clamp(v.Y, MinOffset.Y, Math.Max(MinOffset.Y, MaxOffset.Y)));

    /// <summary>A carried sticker stretches past the surface edges with growing resistance.</summary>
    private Vector Rubber(Vector raw) => new(Rubber(raw.X, MinOffset.X, MaxOffset.X), Rubber(raw.Y, MinOffset.Y, MaxOffset.Y));

    internal static double Rubber(double value, double min, double max)
    {
        if (max < min)
        {
            max = min;
        }
        if (value < min)
        {
            return min - Damp(min - value);
        }
        return value > max ? max + Damp(value - max) : value;
    }

    private static double Damp(double over) => (1 - 1 / (over * RubberStiffness / RubberDimension + 1)) * RubberDimension;

    /// <summary>Critically damped spring step: no overshoot, keeps its velocity when the target moves.</summary>
    internal static void Spring(ref double x, ref double v, double target, double omega, double dt)
    {
        var x0 = x - target;
        var e = Math.Exp(-omega * dt);
        var tmp = v + omega * x0;
        x = target + (x0 + tmp * dt) * e;
        v = (v - omega * tmp * dt) * e;
    }

    private void UpdateGeometry()
    {
        var length = Fold.Length;
        HasFold = _hasAnchor && length > 0.05;
        if (!HasFold)
        {
            _flapCount = 0;
            _stuckCount = 0;
            return;
        }
        var n = Fold / length;
        var t = new Vector(-n.Y, n.X);
        var m = new Point(Anchor.X + Fold.X / 2, Anchor.Y + Fold.Y / 2);
        FoldOrigin = m;
        FoldNormal = n;
        FoldTangent = t;
        var count = StickerPeelGeometry.Clip(Outline, _scratch, m, n, false);
        for (var i = 0; i < count; i++)
        {
            var rx = _scratch[i].X - m.X;
            var ry = _scratch[i].Y - m.Y;
            _flap[i] = new Point(rx * t.X + ry * t.Y, -(rx * n.X + ry * n.Y));
        }
        _flapCount = count;
        _stuckCount = StickerPeelGeometry.Clip(_bigOutline, _stuck, m, n, true);
    }
}
