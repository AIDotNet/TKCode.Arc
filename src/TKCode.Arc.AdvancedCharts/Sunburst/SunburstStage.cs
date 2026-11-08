using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>
/// The rings of a <see cref="Sunburst"/>: one focusable control that keeps five spring values per node on a shared
/// <see cref="ChartSpringArray"/> and draws every arc itself. A sector geometry is rebuilt only when its numbers change
/// (a morph frame or a resize), never at rest; label layouts are built once per settled layout.
/// </summary>
internal sealed class SunburstStage : Avalonia.Controls.Control, Avalonia.Rendering.ICustomHitTest
{
    /// <summary>The whole box takes the pointer, not only the painted marks.</summary>
    bool Avalonia.Rendering.ICustomHitTest.HitTest(Point point) => new Rect(Bounds.Size).Contains(point);

    private const double TipGap = 14;
    private const double TapSlop = 10;

    private static Cursor? s_hand;

    private readonly Sunburst _owner;
    private readonly ChartsBReveal _reveal;
    private readonly ChartsBTooltip _tooltip;
    private readonly ChartSpringArray _springs;
    private readonly MotionValue<double> _labelFade;
    private readonly MotionValue<double> _dim;
    private readonly RollingDigits _digits;
    private double[] _target = [];
    private bool _revealing;
    private SunburstEntry? _active;
    private Point? _pointer;
    private string? _reading;
    private bool _keyboard;
    private bool _centerHover;
    private bool _handCursor;
    private (SunburstEntry? Node, bool Center)? _pressed;
    private Point _pressedAt;
    private SunburstEntry? _tipNode;
    private ChartsBAutomationPeer? _peer;
    private int _paintVersion = -1;
    private FormattedText? _centerLabel;
    private FormattedText? _centerValue;
    private FormattedText? _empty;
    private (SunburstEntry? Node, double Room) _centerKey;
    private int _textVersion = -1;
    private bool _showTip = true;

    public SunburstStage(Sunburst owner)
    {
        _owner = owner;
        Focusable = true;
        FocusAdorner = null;
        ClipToBounds = false;
        _reveal = new ChartsBReveal(this, 0.25, OnReveal);
        _tooltip = new ChartsBTooltip(this, ArcMotionPresets.Snappy, () => owner.Overlay.InvalidateVisual()) { MinWidth = 160 };
        _springs = new ChartSpringArray(this);
        _springs.Changed += InvalidateVisual;
        _springs.Settled += OnSettled;
        _labelFade = new MotionValue<double>(this, 1, MotionVectors.Double);
        _labelFade.Changed += _ => InvalidateVisual();
        _dim = new MotionValue<double>(this, 0, MotionVectors.Double);
        _dim.Changed += _ => InvalidateVisual();
        _digits = new RollingDigits { IsHitTestVisible = false, IsArmed = true };
        ArcTokens.Bind(_digits, TextElement.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
        ArcTokens.Bind(_digits, TextElement.FontSizeProperty, ArcDesignTokenKind.TypeXL);
        ArcTokens.Bind(_digits, TextElement.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        ArcTokens.Bind(_digits, TextElement.ForegroundProperty, ArcDesignTokenKind.Foreground);
        VisualChildren.Add(_digits);
        LogicalChildren.Add(_digits);
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
    }

    internal string? Reading => _reading;

    internal SunburstEntry? Active => _active;

    internal RollingDigits Digits => _digits;

    /// <summary>The text under the digits, when the formatted value has no number to roll.</summary>
    internal bool HasRollingValue => _digits.IsVisible;

    internal string CenterLabelText => _owner.Center.Label;

    /// <summary>What a node is drawn with now: start and end turn, inner and outer radius (fractions), opacity.</summary>
    internal (double T0, double T1, double R0, double R1, double Alpha) ValuesOf(SunburstEntry node)
    {
        var v = _springs.Values;
        var at = node.Index * SunburstLayout.Slots;
        return (v[at], v[at + 1], v[at + 2], v[at + 3], v[at + 4]);
    }

    /// <summary>Whether a node's label shows, and how, after the current layout settled.</summary>
    internal SunburstLabelFit FitOf(SunburstEntry node)
    {
        EnsurePaint();
        return node.Fit;
    }

    internal Color FillOf(SunburstEntry node)
    {
        EnsurePaint();
        return node.Fill;
    }

    /// <summary>Centre of the ring in this control's coordinates, and its radius.</summary>
    private (Point Center, double Radius) Frame()
    {
        var size = Bounds.Size;
        return (new Point(size.Width / 2, size.Height / 2), Math.Min(size.Width, size.Height) / 2);
    }

    /// <summary>A point inside a node's arc, in this control's coordinates (where a tooltip anchors, and tests click).</summary>
    internal Point PointOf(SunburstEntry node)
    {
        var (center, radius) = Frame();
        var at = node.Index * SunburstLayout.Slots;
        var angle = (_target[at] + _target[at + 1]) / 2 * Math.PI * 2 - Math.PI / 2;
        var r = (_target[at + 2] + _target[at + 3]) / 2 * radius;
        return new Point(center.X + r * Math.Cos(angle), center.Y + r * Math.Sin(angle));
    }

    protected override AutomationPeer OnCreateAutomationPeer() => _peer = new ChartsBAutomationPeer(this, _owner);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _reveal.Attach();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _reveal.Detach();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty && change.GetOldValue<Rect>().Size != change.GetNewValue<Rect>().Size)
        {
            RefreshFits();
            _centerKey = default;
            SyncTooltip();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _digits.Measure(Size.Infinity);
        return availableSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var (center, radius) = Frame(finalSize);
        var digits = _digits.DesiredSize;
        var room = radius * _owner.HoleRatio * 2 * 0.84;
        var scale = digits.Width > room && digits.Width > 0 ? room / digits.Width : 1;
        // Only a value too wide for the hole shrinks; the transform does not touch layout.
        if (scale < 1)
        {
            _digits.RenderTransform = new ScaleTransform(scale, scale);
        }
        else if (_digits.RenderTransform is not null)
        {
            _digits.RenderTransform = null;
        }
        var labelHeight = _owner.Palette.TypeXS * 1.4;
        var top = center.Y - (labelHeight + digits.Height) / 2 + labelHeight;
        _digits.Arrange(new Rect(center.X - digits.Width / 2, top, digits.Width, digits.Height));
        return finalSize;
    }

    private static (Point Center, double Radius) Frame(Size size)
        => (new Point(size.Width / 2, size.Height / 2), Math.Min(size.Width, size.Height) / 2);

    // ---- layout and motion -------------------------------------------------------------------------------------

    public void OnDataChanged(SunburstTree old)
    {
        var tree = _owner.Tree;
        foreach (var gone in old.Nodes)
        {
            gone.Text?.Dispose();
            gone.Text = null;
        }
        var length = tree.Nodes.Count * SunburstLayout.Slots;
        _target = new double[length];
        SunburstLayout.Compute(tree, _owner.Center, _owner.Depth, _owner.HoleRatio, _target);
        var target = _target;
        var revealed = _reveal.IsRevealed;
        var hole = _owner.HoleRatio;
        // A node that survives keeps its place and speed; a new one starts where it belongs, hidden.
        _springs.Reshape(length, (oldValues, oldVelocities, values, velocities) =>
        {
            foreach (var node in tree.Nodes)
            {
                var at = node.Index * SunburstLayout.Slots;
                if (revealed && old.ById.TryGetValue(node.Id, out var was) && (was.Index + 1) * SunburstLayout.Slots <= oldValues.Length)
                {
                    var from = was.Index * SunburstLayout.Slots;
                    oldValues.Slice(from, SunburstLayout.Slots).CopyTo(values[at..]);
                    oldVelocities.Slice(from, SunburstLayout.Slots).CopyTo(velocities[at..]);
                }
                else if (revealed)
                {
                    target.AsSpan(at, SunburstLayout.Slots - 1).CopyTo(values[at..]);
                }
                else
                {
                    values[at + 2] = hole;
                    values[at + 3] = hole;
                }
            }
        });
        Focusable = !_owner.IsEmpty;
        if (_active is not null)
        {
            SetActive(tree.ById.GetValueOrDefault(_active.Id), _pointer);
        }
        _tipNode = null;
        _paintVersion = -1;
        Relayout(animate: true);
        SyncCenterValue();
    }

    public void OnCenterChanged()
    {
        SyncCenterValue();
        _centerKey = default;
        Relayout(animate: true);
        if (_active is { } active && !IsVisibleInLayout(active))
        {
            SetActive(null, null);
        }
        SyncTooltip();
    }

    public void OnTextChanged()
    {
        SyncCenterValue();
        _centerKey = default;
        _empty = null;
        _tipNode = null;
        if (_active is { } active)
        {
            _reading = _owner.Describe(active);
        }
        SyncTooltip();
        InvalidateVisual();
    }

    /// <summary>Segment gap or corner changed: every sector is rebuilt at its next draw.</summary>
    public void OnShapeChanged()
    {
        foreach (var node in _owner.Tree.Nodes)
        {
            node.GeometryKey = default;
        }
        InvalidateVisual();
    }

    public void OnPaletteChanged()
    {
        _paintVersion = -1;
        _centerKey = default;
        _empty = null;
        _tipNode = null;
        InvalidateVisual();
    }

    private void OnReveal(bool animate)
    {
        _digits.IsArmed = false;
        _revealing = true;
        Relayout(animate);
        _revealing = false;
    }

    /// <summary>Carries every arc to the current centre: on the spring under Full motion, in one jump otherwise with the text fading back in.</summary>
    public void Relayout(bool animate)
    {
        if (_target.Length != _owner.Tree.Nodes.Count * SunburstLayout.Slots)
        {
            return;
        }
        SunburstLayout.Compute(_owner.Tree, _owner.Center, _owner.Depth, _owner.HoleRatio, _target);
        if (!_reveal.IsRevealed)
        {
            InvalidateVisual();
            return;
        }
        var level = ArcMotion.GetEffectiveLevel(this);
        if (!animate || level == ArcMotionLevel.Off)
        {
            _springs.Jump(_target);
            RefreshFits();
            _labelFade.Jump(1);
        }
        else if (level == ArcMotionLevel.Reduced)
        {
            _springs.Jump(_target);
            RefreshFits();
            _labelFade.Jump(0);
            _ = _labelFade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
        }
        else
        {
            if (_revealing)
            {
                _labelFade.Jump(0);
            }
            else
            {
                _ = _labelFade.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Exit));
            }
            _springs.AnimateTo(_target, _revealing ? ChartsBMath.Physical(0.9, 0) : ChartsBMath.Physical(0.62, 0));
        }
        InvalidateVisual();
    }

    private void OnSettled()
    {
        RefreshFits();
        _ = _labelFade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
    }

    private bool IsVisibleInLayout(SunburstEntry node)
        => (node.Index + 1) * SunburstLayout.Slots <= _target.Length && _target[node.Index * SunburstLayout.Slots + 4] > 0.5;

    /// <summary>Rolls the digits to the centre's total: the formatted text is split so only its number turns.</summary>
    private void SyncCenterValue()
    {
        var text = _owner.Format(_owner.Center.Value);
        if (!_owner.IsEmpty && SunburstLayout.TryParseNumber(text, out var prefix, out var value, out var decimals, out var grouped, out var suffix))
        {
            _digits.IsVisible = true;
            _digits.Prefix = prefix;
            _digits.Suffix = suffix;
            _digits.Decimals = decimals;
            _digits.UseGrouping = grouped;
            _digits.Value = value;
        }
        else
        {
            _digits.IsVisible = false;
        }
        AutomationProperties.SetHelpText(_owner, $"{_owner.Center.Label}, {text}");
    }

    /// <summary>Colors from the current palette: top-level branches take the series hues, each level below is a lighter tint, siblings step a little apart.</summary>
    private void EnsurePaint()
    {
        var p = _owner.Palette;
        if (_paintVersion == p.Version)
        {
            return;
        }
        _paintVersion = p.Version;
        var root = _owner.Tree.Root;
        for (var i = 0; i < root.Children.Count; i++)
        {
            Tint(root.Children[i], i, 0.6, p);
        }
        RefreshFits();
    }

    private static void Tint(SunburstEntry node, int branch, double tint, ChartsBPalette p)
    {
        node.Branch = branch;
        node.Tint = tint;
        // A fifth branch and beyond stay neutral rather than reuse a hue.
        node.Fill = branch < 4
            ? ChartsBMath.MixIn(p.Series(branch), tint, p.Surface)
            : ChartsBMath.MixIn(p.Foreground, tint * 0.45, p.Surface);
        var count = node.Children.Count;
        for (var i = 0; i < count; i++)
        {
            var step = count > 1 ? (0.5 - (double)i / (count - 1)) * 0.14 : 0;
            Tint(node.Children[i], branch, Math.Clamp(tint * 0.76 + step, 0.1, 0.62), p);
        }
    }

    /// <summary>Decides, once per settled layout or size, which labels fit their arc at their final angles.</summary>
    private void RefreshFits()
    {
        var (_, radius) = Frame();
        var p = _owner.Palette;
        var nodes = _owner.Tree.Nodes;
        if (_target.Length != nodes.Count * SunburstLayout.Slots)
        {
            return;
        }
        foreach (var node in nodes)
        {
            var at = node.Index * SunburstLayout.Slots;
            node.Fit = SunburstLabelFit.None;
            if (_target[at + 4] < 0.5 || radius <= 0 || string.IsNullOrEmpty(node.Label))
            {
                continue;
            }
            if (node.Text is null || _paintVersion != _textVersion)
            {
                node.Text?.Dispose();
                node.Text = new TextLayout(node.Label, p.Body(FontWeight.Medium), p.TypeXS, p.Brush(p.Foreground));
            }
            node.Fit = SunburstLayout.Fit(node.Text.WidthIncludingTrailingWhitespace, node.Text.Height,
                _target[at + 2] * radius, _target[at + 3] * radius, _target[at + 1] - _target[at]);
        }
        _textVersion = _paintVersion;
    }

    // ---- interaction -------------------------------------------------------------------------------------------

    public void ClearActive() => SetActive(null, null);

    /// <summary>Highlights a segment, reads it to assistive technology, and (unless <paramref name="tooltip"/> is false) shows its tooltip.</summary>
    public void SetActive(SunburstEntry? node, Point? pointer, bool tooltip = true)
    {
        var changed = !ReferenceEquals(node, _active);
        _active = node;
        _pointer = pointer;
        _showTip = tooltip;
        if (changed)
        {
            var target = node is null ? 0 : 1;
            if (ArcMotion.IsFull(this))
            {
                _ = _dim.AnimateTo(target, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
            }
            else
            {
                _dim.Jump(target);
            }
            _reading = node is null ? null : _owner.Describe(node);
            _peer?.Announce();
        }
        SyncTooltip();
        InvalidateVisual();
    }

    private (SunburstEntry? Node, bool Center) HitTest(Point point)
    {
        var (center, radius) = Frame();
        if (radius <= 0 || _owner.IsEmpty)
        {
            return (null, false);
        }
        var dx = point.X - center.X;
        var dy = point.Y - center.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy) / radius;
        if (distance < _owner.HoleRatio)
        {
            return (null, true);
        }
        if (distance > 1)
        {
            return (null, false);
        }
        var turn = (Math.Atan2(dy, dx) + Math.PI / 2) / (Math.PI * 2);
        turn -= Math.Floor(turn);
        var values = _springs.Values;
        var nodes = _owner.Tree.Nodes;
        for (var i = 1; i < nodes.Count; i++)
        {
            var at = i * SunburstLayout.Slots;
            if (values[at + 4] > 0.6 && distance >= values[at + 2] && distance < values[at + 3] && turn >= values[at] && turn < values[at + 1])
            {
                return (nodes[i], false);
            }
        }
        return (null, false);
    }

    private void SetHand(bool hand)
    {
        if (hand != _handCursor)
        {
            _handCursor = hand;
            s_hand ??= new Cursor(StandardCursorType.Hand);
            Cursor = hand ? s_hand : Cursor.Default;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (e.Pointer.Type != PointerType.Mouse)
        {
            return;
        }
        var point = e.GetPosition(this);
        var hit = HitTest(point);
        var centerHover = hit.Center && _owner.Center.Parent is not null;
        if (centerHover != _centerHover)
        {
            _centerHover = centerHover;
            InvalidateVisual();
        }
        SetHand(centerHover || hit.Node is { IsBranch: true });
        _keyboard = false;
        SetActive(hit.Node, hit.Node is null ? null : point);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _centerHover = false;
        SetActive(null, null);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Pointer.Type == PointerType.Mouse && !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        _pressedAt = e.GetPosition(this);
        _pressed = HitTest(_pressedAt);
    }

    /// <summary>A click or tap acts on release, so a touch that starts a scroll does not zoom.</summary>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_pressed is not { } pressed)
        {
            return;
        }
        _pressed = null;
        var point = e.GetPosition(this);
        var moved = point - _pressedAt;
        if (Math.Abs(moved.X) > TapSlop || Math.Abs(moved.Y) > TapSlop)
        {
            return;
        }
        var hit = HitTest(point);
        if (hit.Center != pressed.Center || !ReferenceEquals(hit.Node, pressed.Node))
        {
            return;
        }
        if (hit.Center)
        {
            if (_owner.Center.Parent is { } parent)
            {
                e.Handled = true;
                _centerHover = false;
                ZoomOut(parent, null);
            }
        }
        else if (hit.Node is { } node)
        {
            e.Handled = true;
            if (node.IsBranch)
            {
                SetActive(null, null);
                _owner.ZoomTo(node.Id, SunburstFocusChangeSource.Segment);
            }
            else
            {
                SetActive(node, e.Pointer.Type == PointerType.Mouse ? point : null);
            }
        }
        else
        {
            SetActive(null, null);
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pressed = null;
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _keyboard = e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional;
        if (_keyboard && _active is null)
        {
            SetActive(_owner.Center.FirstVisibleChild(), null);
        }
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _keyboard = false;
        SetActive(null, null);
    }

    private void ZoomOut(SunburstEntry parent, SunburstEntry? activate)
    {
        _owner.ZoomTo(parent.Id, SunburstFocusChangeSource.Center);
        SetActive(activate, null);
    }

    /// <summary>Left and Right walk the siblings, Down opens the first child, Up goes to the parent, Enter or Space zoom in, Escape or Backspace zoom out.</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || _owner.IsEmpty)
        {
            return;
        }
        var center = _owner.Center;
        switch (e.Key)
        {
            case Key.Left or Key.Right:
                e.Handled = true;
                _keyboard = true;
                SetActive(_active is { Parent: { } parent } current ? Sibling(parent, current, e.Key == Key.Right ? 1 : -1) : center.FirstVisibleChild(), null);
                break;
            case Key.Home or Key.End:
                e.Handled = true;
                _keyboard = true;
                SetActive(EdgeSibling(_active?.Parent ?? center, e.Key == Key.Home), null);
                break;
            case Key.Down:
                e.Handled = true;
                _keyboard = true;
                if (_active is null)
                {
                    SetActive(center.FirstVisibleChild(), null);
                }
                else if (_active.FirstVisibleChild() is { } child)
                {
                    Reveal(child);
                    SetActive(child, null);
                }
                break;
            case Key.Up:
                e.Handled = true;
                _keyboard = true;
                if (_active is { Parent: { } up })
                {
                    if (!ReferenceEquals(up, center))
                    {
                        SetActive(up, null);
                    }
                    else if (center.Parent is { } outer)
                    {
                        ZoomOut(outer, center);
                    }
                }
                else
                {
                    SetActive(center.FirstVisibleChild(), null);
                }
                break;
            case Key.Enter or Key.Space:
                e.Handled = true;
                _keyboard = true;
                if (_active is null)
                {
                    SetActive(center.FirstVisibleChild(), null);
                }
                else if (_active.IsBranch)
                {
                    var opened = _active;
                    _owner.ZoomTo(opened.Id, SunburstFocusChangeSource.Segment);
                    SetActive(opened.FirstVisibleChild(), null);
                }
                break;
            case Key.Escape or Key.Back:
                _keyboard = true;
                if (center.Parent is { } back)
                {
                    e.Handled = true;
                    ZoomOut(back, center);
                }
                else if (_active is not null)
                {
                    e.Handled = true;
                    SetActive(null, null);
                }
                break;
        }
    }

    /// <summary>Moving down past the last visible ring shifts the centre by the levels needed to show the node.</summary>
    private void Reveal(SunburstEntry node)
    {
        var extra = node.Depth - _owner.Center.Depth - Math.Max(1, _owner.Depth);
        if (extra <= 0)
        {
            return;
        }
        var depth = _owner.Center.Depth + extra;
        var target = node;
        while (target.Depth > depth && target.Parent is { } parent)
        {
            target = parent;
        }
        _owner.ZoomTo(target.Id, SunburstFocusChangeSource.Segment);
    }

    private static SunburstEntry? EdgeSibling(SunburstEntry parent, bool first)
    {
        var count = parent.Children.Count;
        for (var i = 0; i < count; i++)
        {
            var child = parent.Children[first ? i : count - 1 - i];
            if (child.Value > 0)
            {
                return child;
            }
        }
        return null;
    }

    private static SunburstEntry Sibling(SunburstEntry parent, SunburstEntry current, int step)
    {
        var count = parent.Children.Count;
        var at = parent.Children.IndexOf(current);
        for (var i = 1; i <= count; i++)
        {
            var next = parent.Children[((at + step * i) % count + count) % count];
            if (next.Value > 0)
            {
                return next;
            }
        }
        return current;
    }

    // ---- tooltip -----------------------------------------------------------------------------------------------

    private void SyncTooltip()
    {
        if (_active is not { } node || !_showTip || Bounds.Width <= 0 || !IsVisibleInLayout(node))
        {
            _tooltip.Hide();
            _owner.Overlay.InvalidateVisual();
            InvalidateVisual();
            return;
        }
        var p = _owner.Palette;
        if (!ReferenceEquals(_tipNode, node))
        {
            _tipNode = node;
            var parent = node.Parent ?? node;
            var title = node.Path.Length > 1 ? string.Join(" / ", node.Path.Take(node.Path.Length - 1)) : _owner.Tree.Root.Label;
            _tooltip.SetRows(
            [
                new ChartsBTipRow { Left = ChartsBText.Make(title, p, p.TypeXS, p.TextMuted), MinHeight = p.TypeXS * 1.5 },
                new ChartsBTipRow
                {
                    MinHeight = p.TypeSM * 1.35,
                    Swatch = FillOf(node),
                    Shape = ChartsBSwatchShape.Round,
                    Left = ChartsBText.Make(node.Label, p, p.TypeSM, p.Foreground),
                    Right = ChartsBText.Make(_owner.Format(node.Value), p, p.TypeSM, p.Foreground, FontWeight.Medium)
                },
                new ChartsBTipRow
                {
                    MinHeight = p.TypeXS * 1.5,
                    Left = ChartsBText.Make($"{ChartsBMath.Percent(Sunburst.ShareOf(node, parent), 1)} of {parent.Label}", p, p.TypeXS, p.TextMuted)
                }
            ]);
        }
        // Trails the pointer, or sits by the segment in focus, and stays inside the chart.
        var offset = Bounds.Position;
        var anchor = (_pointer ?? PointOf(node)) + offset;
        var size = _tooltip.Size;
        var bounds = _owner.Bounds.Size;
        var left = anchor.X + TipGap;
        if (left + size.Width > bounds.Width)
        {
            left = anchor.X - TipGap - size.Width;
        }
        var top = anchor.Y + TipGap;
        if (top + size.Height > bounds.Height)
        {
            top = anchor.Y - TipGap - size.Height;
        }
        _tooltip.Show(new Point(ChartsBMath.Clamp(left, 0, Math.Max(0, bounds.Width - size.Width)), ChartsBMath.Clamp(top, 0, Math.Max(0, bounds.Height - size.Height))));
        _owner.Overlay.InvalidateVisual();
        InvalidateVisual();
    }

    internal bool IsTooltipOpen => _tooltip.IsOpen;

    public void DrawTooltip(DrawingContext context) => _tooltip.Draw(context, _owner.Palette);

    // ---- drawing -----------------------------------------------------------------------------------------------

    public override void Render(DrawingContext context)
    {
        var (center, radius) = Frame();
        if (radius <= 0)
        {
            return;
        }
        EnsurePaint();
        var p = _owner.Palette;
        if (_owner.IsEmpty)
        {
            DrawEmpty(context, center, radius);
            return;
        }
        if (!_reveal.IsRevealed)
        {
            return;
        }
        using var fade = context.PushOpacity(_reveal.Fade);
        var gap = _owner.SegmentGap;
        var corner = _owner.SegmentCorner;
        var nodes = _owner.Tree.Nodes;
        var values = _springs.Values;
        var active = _active;
        var dim = _dim.Value;
        for (var i = 1; i < nodes.Count; i++)
        {
            var at = i * SunburstLayout.Slots;
            var alpha = values[at + 4];
            if (alpha < 0.01)
            {
                continue;
            }
            var node = nodes[i];
            var geometry = GeometryOf(node, center, radius, values[at], values[at + 1], values[at + 2], values[at + 3], gap, corner);
            if (geometry is null)
            {
                continue;
            }
            var t0 = Math.Clamp(values[at], 0, 1);
            var t1 = Math.Clamp(values[at + 1], 0, 1);
            // Thin slivers fade instead of drawing hairlines.
            var breadth = Math.Clamp((ChartGeometry.SectorBreadth(values[at + 3] * radius, t0, t1, gap) - 1) / 3, 0, 1);
            var related = active is null || node.IsUnder(active) || active.IsUnder(node);
            using (context.PushOpacity(Math.Min(alpha, 1) * breadth * (related ? 1 : 1 - 0.45 * dim)))
            {
                context.DrawGeometry(p.Brush(node.Fill), null, geometry);
            }
        }
        DrawLabels(context, center, radius);
        if (active?.Geometry is { } outline && values[active.Index * SunburstLayout.Slots + 4] > 0.5)
        {
            context.DrawGeometry(null, p.Pen(p.Foreground, 2), outline);
        }
        DrawCenter(context, center, radius);
        if (_keyboard && IsFocused && active is null)
        {
            context.DrawEllipse(null, p.Pen(p.FocusRing, 2), center, radius - 1, radius - 1);
        }
    }

    private static StreamGeometry? GeometryOf(SunburstEntry node, Point center, double radius, double t0, double t1, double r0, double r1, double gap, double corner)
    {
        var key = (radius, t0, t1, r0, r1);
        if (node.GeometryKey != key)
        {
            node.GeometryKey = key;
            var outer = r1 * radius - gap / 2;
            var inner = r0 * radius + gap / 2;
            node.Geometry = outer - inner < 0.5 ? null : ChartGeometry.Sector(center, outer, Math.Max(0.5, inner), Math.Clamp(t0, 0, 1), Math.Clamp(t1, 0, 1), gap, corner);
        }
        return node.Geometry;
    }

    private void DrawLabels(DrawingContext context, Point center, double radius)
    {
        var fade = _labelFade.Value;
        if (fade < 0.01)
        {
            return;
        }
        var nodes = _owner.Tree.Nodes;
        var values = _springs.Values;
        using var opacity = context.PushOpacity(fade);
        for (var i = 1; i < nodes.Count; i++)
        {
            var node = nodes[i];
            var at = i * SunburstLayout.Slots;
            if (node.Fit == SunburstLabelFit.None || node.Text is not { } text || values[at + 4] < 0.5)
            {
                continue;
            }
            var turn = (values[at] + values[at + 1]) / 2;
            var angle = turn * Math.PI * 2 - Math.PI / 2;
            var middle = (values[at + 2] + values[at + 3]) / 2 * radius;
            var x = center.X + middle * Math.Cos(angle);
            var y = center.Y + middle * Math.Sin(angle);
            // Text reads along the arc, or across the ring when that fits better, and is never upside down.
            var rotation = node.Fit == SunburstLabelFit.Tangent
                ? angle + Math.PI / 2 + (Math.Sin(angle) > 0 ? Math.PI : 0)
                : angle + (Math.Cos(angle) < 0 ? Math.PI : 0);
            using (context.PushTransform(Matrix.CreateRotation(rotation) * Matrix.CreateTranslation(x, y)))
            {
                text.Draw(context, new Point(-text.WidthIncludingTrailingWhitespace / 2, -text.Height / 2));
            }
        }
    }

    private void DrawCenter(DrawingContext context, Point center, double radius)
    {
        var p = _owner.Palette;
        var hole = radius * _owner.HoleRatio - _owner.SegmentGap;
        if (_centerHover)
        {
            context.DrawEllipse(p.Brush(ChartsBMath.Alpha(p.Foreground, 0.05)), null, center, hole, hole);
        }
        var node = _owner.Center;
        var room = Math.Max(10, hole * 2 * 0.8);
        var key = (node, room);
        if (_centerKey != key || _centerLabel is null)
        {
            _centerKey = key;
            _centerLabel = ChartsBText.Trimmed(node.Label, p, p.TypeXS, p.TextSecondary, room);
            _centerValue = ChartsBText.Trimmed(_owner.Format(node.Value), p, p.TypeBase, p.Foreground, room, FontWeight.Medium);
        }
        var fade = _labelFade.Value;
        var digits = _digits.IsVisible ? _digits.Bounds.Height : _centerValue!.Height;
        var labelHeight = p.TypeXS * 1.4;
        var top = center.Y - (labelHeight + digits) / 2;
        using var opacity = context.PushOpacity(Math.Max(fade, 0));
        context.DrawText(_centerLabel, new Point(center.X - _centerLabel.WidthIncludingTrailingWhitespace / 2, top + (labelHeight - _centerLabel.Height) / 2));
        if (!_digits.IsVisible)
        {
            context.DrawText(_centerValue!, new Point(center.X - _centerValue!.WidthIncludingTrailingWhitespace / 2, top + labelHeight));
        }
    }

    private void DrawEmpty(DrawingContext context, Point center, double radius)
    {
        var p = _owner.Palette;
        var hole = radius * _owner.HoleRatio;
        var track = ChartGeometry.Sector(center, radius - 1, hole, 0, 1, 0, 0);
        if (track is not null)
        {
            context.DrawGeometry(p.Brush(ChartsBMath.MixIn(p.Foreground, 0.06, p.Surface)), null, track);
        }
        _empty ??= ChartsBText.Trimmed(_owner.EmptyLabel, p, p.TypeXS, p.TextMuted, Math.Max(10, hole * 2 * 0.86));
        context.DrawText(_empty, new Point(center.X - _empty.WidthIncludingTrailingWhitespace / 2, center.Y - _empty.Height / 2));
    }
}
