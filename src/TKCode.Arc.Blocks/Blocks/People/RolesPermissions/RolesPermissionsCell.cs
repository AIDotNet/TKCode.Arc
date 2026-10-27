using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Input;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// One cell of the matrix: a 40 × 32 hit area with a 16px box that fills, a check (or, for a mixed group, a dash) that
/// draws itself, and the 5px unsaved dot to the right of the box. Group cells are tri-state checkboxes, permission cells
/// switches; both look the same. Locked cells (a locked role) are always on, muted, and shake briefly when pressed.
/// <para>
/// Code-driven because a ControlTheme cannot express it: each change starts after a delay chosen at change time (the
/// group and revert ripples), the colors blend from wherever an interrupted change left them, the check's stroke length
/// animates, and the dot pops on its own spring. Every cell of the matrix is one light control drawn in
/// <see cref="Render"/> with cached brushes, so the grid can re-arrange a hundred of them per frame while a column springs.
/// </para>
/// </summary>
internal sealed class RolesPermissionsCell : RolesPermissionsPart
{
    // The check (7.5 × 5.5) and the dash (6 wide) in the 16px box, stroked at 1.75 with round caps.
    private static readonly Point CheckA = new(4.25, 8.25);
    private static readonly Point CheckB = new(6.75, 10.75);
    private static readonly Point CheckC = new(11.75, 5.25);
    private static readonly Point DashA = new(5, 8);
    private static readonly Point DashB = new(11, 8);
    private static readonly double[] ShakeKeys = [0, 3, -2, 1, 0];

    private readonly SolidColorBrush _fill = new(Colors.White);
    private readonly SolidColorBrush _stroke = new(Colors.Gray);
    private readonly SolidColorBrush _glyph = new(Colors.White);
    private readonly SolidColorBrush _dotBrush = new(Colors.Orange);
    private readonly Pen _borderPen;
    private readonly Pen _glyphPen;

    private MotionValue<double>? _paint;
    private MotionValue<double>? _check;
    private MotionValue<double>? _dash;
    private MotionValue<double>? _dot;
    private MotionValue<double>? _scale;
    private MotionValue<double>? _shake;

    private Color _fromFill, _fromStroke, _fromGlyph;
    private Color _toFill, _toStroke, _toGlyph;
    private int _paintVersion = -1;
    private bool _hover;
    private bool _pressedByPointer;
    private bool _spaceDown;

    public RolesPermissionsCell()
    {
        _borderPen = new Pen(_stroke, 1);
        _glyphPen = new Pen(_glyph, 1.75, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        Focusable = true;
        IsTabStop = false;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    public RolesPermissionsRole Role { get; init; } = null!;

    /// <summary>The permission of a permission cell; null for a group cell.</summary>
    public RolesPermissionsItem? Permission { get; init; }

    public RolesPermissionsGroup Group { get; init; } = null!;

    public bool IsGroupCell => Permission is null;

    public RolesPermissionsCheck State { get; private set; }

    public bool IsLocked => Role.IsLocked;

    public bool IsUnsaved { get; private set; }

    internal bool HasDot => (_dot?.Value ?? 0) > 0.001;

    internal double DotProgress => _dot?.Value ?? 0;

    internal double CheckProgress => _check?.Value ?? 0;

    internal double PaintProgress => _paint?.Value ?? 1;

    /// <summary>
    /// Shows <paramref name="state"/> and the unsaved dot. The colors, the check and the dot change after
    /// <paramref name="delay"/> (the ripple); without <paramref name="animate"/>, or below Full motion, everything jumps.
    /// </summary>
    public void Apply(RolesPermissionsCheck state, bool unsaved, TimeSpan delay, bool animate)
    {
        EnsureMotion();
        var stateChanged = state != State;
        var unsavedChanged = unsaved != IsUnsaved;
        State = state;
        IsUnsaved = unsaved;
        var run = animate && Animates;
        if (stateChanged || _paintVersion != Grid?.Paint.Version)
        {
            Retarget(run, delay);
        }
        var checkTarget = state == RolesPermissionsCheck.On ? 1.0 : 0.0;
        var dashTarget = state == RolesPermissionsCheck.Mixed ? 1.0 : 0.0;
        if (run)
        {
            // Drawing in: about 100ms on a quick ease-out; out: 120ms (Arc's path length and opacity).
            _check!.AnimateTo(checkTarget, MotionSpec.Tween(TimeSpan.FromMilliseconds(checkTarget > 0 ? 100 : 120), ArcEasings.Standard, delay));
            _dash!.AnimateTo(dashTarget, MotionSpec.Tween(TimeSpan.FromMilliseconds(dashTarget > 0 ? 100 : 120), ArcEasings.Standard, delay));
        }
        else
        {
            _check!.Jump(checkTarget);
            _dash!.Jump(dashTarget);
        }
        if (unsavedChanged || !run)
        {
            var dotTarget = unsaved ? 1.0 : 0.0;
            if (run)
            {
                _dot!.AnimateTo(dotTarget, MotionSpec.Spring(ArcSpring.Quick, delay));
            }
            else
            {
                _dot!.Jump(dotTarget);
            }
        }
        UpdateAutomationName();
    }

    /// <summary>Re-reads the shared colors (theme change) without animating.</summary>
    public void RefreshPaint()
    {
        EnsureMotion();
        _paintVersion = -1;
        Retarget(false, TimeSpan.Zero);
    }

    /// <summary>The locked cell's answer to a press: the box shakes sideways (0, +3, −2, +1, 0 over 330ms).</summary>
    public void Shake()
    {
        EnsureMotion();
        if (!Animates)
        {
            return;
        }
        _shake!.Jump(0);
        _shake.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(330), new Avalonia.Animation.Easings.LinearEasing()));
    }

    internal void UpdateAutomationName()
    {
        if (Grid?.Owner is not { } owner)
        {
            return;
        }
        var labels = owner.Labels;
        var name = IsGroupCell
            ? ProjectsText.Format(labels.GroupCellFormat, Role.Name, Group.Name)
            : ProjectsText.Format(labels.CellFormat, Role.Name, Permission!.Name);
        if (IsLocked)
        {
            name += labels.AlwaysOnSuffix;
        }
        else if (IsUnsaved)
        {
            name += labels.UnsavedSuffix;
        }
        AutomationProperties.SetName(this, name);
        if (Permission?.Description is { } help)
        {
            AutomationProperties.SetHelpText(this, help);
        }
    }

    private void EnsureMotion()
    {
        if (_paint is not null)
        {
            return;
        }
        _paint = new MotionValue<double>(this, 1, MotionVectors.Double);
        _paint.Changed += _ => InvalidateVisual();
        _check = new MotionValue<double>(this, 0, MotionVectors.Double);
        _check.Changed += _ => InvalidateVisual();
        _dash = new MotionValue<double>(this, 0, MotionVectors.Double);
        _dash.Changed += _ => InvalidateVisual();
        _dot = new MotionValue<double>(this, 0, MotionVectors.Double);
        _dot.Changed += _ => InvalidateVisual();
        _scale = new MotionValue<double>(this, 1, MotionVectors.Double);
        _scale.Changed += _ => InvalidateVisual();
        _shake = new MotionValue<double>(this, 0, MotionVectors.Double);
        _shake.Changed += _ => InvalidateVisual();
    }

    private void Retarget(bool animate, TimeSpan delay)
    {
        if (Grid is null)
        {
            return;
        }
        var p = _paint!.Value;
        _fromFill = RolesPermissionsPaint.Lerp(_fromFill, _toFill, p);
        _fromStroke = RolesPermissionsPaint.Lerp(_fromStroke, _toStroke, p);
        _fromGlyph = RolesPermissionsPaint.Lerp(_fromGlyph, _toGlyph, p);
        TargetColors(out _toFill, out _toStroke, out _toGlyph);
        if (_paintVersion != Grid.Paint.Version)
        {
            // First paint or a theme change: no blend from the old theme's colors.
            _paintVersion = Grid.Paint.Version;
            if (!animate)
            {
                _fromFill = _toFill;
                _fromStroke = _toStroke;
                _fromGlyph = _toGlyph;
            }
        }
        if (animate)
        {
            _paint.Jump(0);
            // Arc: background, border and color in 160ms on (.22, 1, .36, 1), delayed by the ripple.
            _paint.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard, delay));
        }
        else
        {
            _paint.Jump(1);
        }
        InvalidateVisual();
    }

    private void TargetColors(out Color fill, out Color stroke, out Color glyph)
    {
        var paint = Grid!.Paint;
        if (IsLocked)
        {
            fill = paint.BoxLockedFill;
            stroke = paint.BoxLockedFill;
            glyph = paint.BoxOnGlyph;
            return;
        }
        switch (State)
        {
            case RolesPermissionsCheck.On:
                fill = paint.BoxOnFill;
                stroke = paint.BoxOnFill;
                glyph = paint.BoxOnGlyph;
                break;
            case RolesPermissionsCheck.Mixed:
                fill = paint.BoxMixedFill;
                stroke = paint.BoxOnFill;
                glyph = paint.BoxOnFill;
                break;
            default:
                fill = _hover ? paint.BoxHoverFill : paint.Surface;
                stroke = _hover ? paint.BoxHoverBorder : paint.BoxOffBorder;
                glyph = paint.BoxOnGlyph;
                break;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
        => Grid is null ? new Size(40, 32) : new Size(Grid.CellWidth, Grid.CellHeight);

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        // The whole 40 × 32 area takes the pointer, not just the painted box.
        context.FillRectangle(Brushes.Transparent, bounds);
        if (Grid is null)
        {
            return;
        }
        if (_paintVersion != Grid.Paint.Version)
        {
            return;
        }
        var paint = Grid.Paint;
        var box = Grid.BoxSize;
        var origin = new Point((bounds.Width - box) / 2, (bounds.Height - box) / 2);
        var p = _paint?.Value ?? 1;
        _fill.Color = RolesPermissionsPaint.Lerp(_fromFill, _toFill, p);
        _stroke.Color = RolesPermissionsPaint.Lerp(_fromStroke, _toStroke, p);
        _glyph.Color = RolesPermissionsPaint.Lerp(_fromGlyph, _toGlyph, p);

        var scale = _scale?.Value ?? 1;
        var shake = ShakeOffset(_shake?.Value ?? 0);
        var center = new Point(origin.X + box / 2, origin.Y + box / 2);
        var transform = Matrix.CreateTranslation(-center.X, -center.Y)
                        * Matrix.CreateScale(scale, scale)
                        * Matrix.CreateTranslation(center.X + shake, center.Y);
        using (context.PushTransform(transform))
        {
            var rect = new Rect(origin, new Size(box, box));
            context.DrawRectangle(_fill, null, rect, Grid.BoxRadius, Grid.BoxRadius);
            context.DrawRectangle(null, _borderPen, rect.Deflate(0.5), Grid.BoxRadius - 0.5, Grid.BoxRadius - 0.5);
            var unit = box / 16;
            var check = _check?.Value ?? 0;
            if (check > 0.001)
            {
                DrawCheck(context, origin, unit, check);
            }
            var dash = _dash?.Value ?? 0;
            if (dash > 0.001)
            {
                using (context.PushOpacity(dash))
                {
                    var a = Offset(origin, DashA, unit);
                    var b = Offset(origin, DashB, unit);
                    context.DrawLine(_glyphPen, a, Lerp(a, b, dash));
                }
            }
        }

        var dot = _dot?.Value ?? 0;
        if (dot > 0.001)
        {
            var size = Grid.DotSize * (0.3 + 0.7 * dot);
            var dotCenter = new Point(origin.X + box + 5 + Grid.DotSize / 2, bounds.Height / 2);
            _dotBrush.Color = RolesPermissionsPaint.Fade(paint.Warning, dot);
            context.DrawEllipse(_dotBrush, null, dotCenter, size / 2, size / 2);
        }

        if (IsFocusVisible)
        {
            DrawFocusRing(context, bounds, Grid.CellRadius);
        }
    }

    private void DrawCheck(DrawingContext context, Point origin, double unit, double progress)
    {
        var a = Offset(origin, CheckA, unit);
        var b = Offset(origin, CheckB, unit);
        var c = Offset(origin, CheckC, unit);
        var first = Distance(a, b);
        var second = Distance(b, c);
        var length = (first + second) * progress;
        using (context.PushOpacity(Math.Min(1, progress * 1.2)))
        {
            if (length <= first)
            {
                context.DrawLine(_glyphPen, a, Lerp(a, b, length / first));
            }
            else
            {
                context.DrawLine(_glyphPen, a, b);
                context.DrawLine(_glyphPen, b, Lerp(b, c, (length - first) / second));
            }
        }
    }

    private static double ShakeOffset(double t)
    {
        if (t <= 0 || t >= 1)
        {
            return 0;
        }
        var position = t * (ShakeKeys.Length - 1);
        var i = (int)position;
        var f = position - i;
        return ShakeKeys[i] + (ShakeKeys[i + 1] - ShakeKeys[i]) * f;
    }

    private static Point Offset(Point origin, Point p, double unit) => new(origin.X + p.X * unit, origin.Y + p.Y * unit);

    private static Point Lerp(Point a, Point b, double t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

    private static double Distance(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    #region Input

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type != PointerType.Touch)
        {
            SetHover(true);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        SetHover(false);
    }

    private void SetHover(bool hover)
    {
        if (_hover == hover)
        {
            return;
        }
        _hover = hover;
        if (State == RolesPermissionsCheck.Off && !IsLocked && Grid is not null)
        {
            EnsureMotion();
            Retarget(Animates, TimeSpan.Zero);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        e.Handled = true;
        Focus(NavigationMethod.Pointer);
        e.Pointer.Capture(this);
        _pressedByPointer = true;
        if (!IsLocked)
        {
            Press(true);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pressedByPointer)
        {
            return;
        }
        _pressedByPointer = false;
        e.Pointer.Capture(null);
        Press(false);
        var position = e.GetPosition(this);
        if (new Rect(Bounds.Size).Contains(position))
        {
            e.Handled = true;
            Activate();
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_pressedByPointer)
        {
            _pressedByPointer = false;
            Press(false);
        }
    }

    private void Press(bool down)
    {
        EnsureMotion();
        if (!Animates)
        {
            _scale!.Jump(1);
            return;
        }
        // Arc: the box presses to .88 in 120ms and comes back on the 580ms CSS spring once the click is committed.
        _scale!.AnimateTo(down ? 0.88 : 1, MotionSpec.Tween(down ? ArcMotionPresets.Instant : ArcMotionPresets.CssSpring, ArcEasings.CssSpring));
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyModifiers == KeyModifiers.None)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                Activate();
                return;
            }
            if (e.Key == Key.Space)
            {
                e.Handled = true;
                _spaceDown = true;
                return;
            }
        }
        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.Key == Key.Space && _spaceDown)
        {
            _spaceDown = false;
            e.Handled = true;
            Activate();
            return;
        }
        base.OnKeyUp(e);
    }

    internal void Activate() => Grid?.Owner?.OnCellActivated(this);

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new CellPeer(this);

    /// <summary>A checkbox (group cells, tri-state) or a toggle (permission cells, Arc's switch); locked cells stay on.</summary>
    private sealed class CellPeer(RolesPermissionsCell owner) : ControlAutomationPeer(owner), IToggleProvider
    {
        private RolesPermissionsCell Cell => (RolesPermissionsCell)Owner;

        public ToggleState ToggleState => Cell.State switch
        {
            RolesPermissionsCheck.On => ToggleState.On,
            RolesPermissionsCheck.Mixed => ToggleState.Indeterminate,
            _ => Cell.IsLocked ? ToggleState.On : ToggleState.Off,
        };

        public void Toggle() => Cell.Activate();

        protected override AutomationControlType GetAutomationControlTypeCore()
            => Cell.IsGroupCell ? AutomationControlType.CheckBox : AutomationControlType.Button;

        protected override string GetLocalizedControlTypeCore() => Cell.IsGroupCell ? "checkbox" : "switch";

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}
