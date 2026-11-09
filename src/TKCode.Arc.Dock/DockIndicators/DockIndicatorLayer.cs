using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The drag indicators of one dock host (the layout, or a floating window): the group compass (center and four sides), the
/// outer ring of the document area, the four edge buttons of the whole layout, the drop preview that glides between targets,
/// the insertion line of a tab strip and the drag ghost. One instance of each part, reused for every move (spec 14); the
/// layer takes no input, the drag hit-tests its geometry (<see cref="HitTest"/>).
/// </summary>
internal sealed class DockIndicatorLayer : Control
{
    private static readonly TimeSpan s_reveal = TimeSpan.FromMilliseconds(120);

    private readonly Dictionary<DockCompassSlot, DockCompassButton> _buttons = new();
    private readonly Dictionary<DockCompassSlot, Rect> _rects = new();
    private readonly GlidingIndicator _preview;
    private readonly Border _line;
    private DockDragGhost? _ghost;
    private Rect _lineRect;
    private Point _ghostAt;
    private MotionValue<double>? _compassReveal;
    private MotionValue<double>? _edgeReveal;
    private DockCompassSlot? _hot;
    private Rect? _compassGroup;
    private bool _areaRing;
    private Rect? _edgeArea;
    private Color _fillColor;
    private double _fillOpacity = 0.55;
    private readonly List<IDisposable> _subscriptions = [];

    public DockIndicatorLayer()
    {
        IsHitTestVisible = false;
        ClipToBounds = false;
        _preview = new GlidingIndicator { Spring = ArcSpring.Snappy }.BindCornerRadius(ArcDesignTokenKind.RadiusStep12);
        ArcTokens.Bind(_preview, GlidingIndicator.StrokeProperty, ArcDesignTokenKind.Accent);
        _preview.Bind(GlidingIndicator.StrokeThicknessProperty,
            _preview.GetResourceObservable(DockIndicatorTokens.Key(DockIndicatorTokenKind.PreviewStroke)));
        _line = new Border { IsVisible = false, CornerRadius = new CornerRadius(1) };
        ArcTokens.Bind(_line, Border.BackgroundProperty, ArcDesignTokenKind.Accent);
        VisualChildren.Add(_preview);
        LogicalChildren.Add(_preview);
        VisualChildren.Add(_line);
        LogicalChildren.Add(_line);
    }

    /// <summary>The preview (tests).</summary>
    internal GlidingIndicator Preview => _preview;

    /// <summary>The insertion line, when shown (tests).</summary>
    internal Rect? InsertionLine => _line.IsVisible ? _lineRect : null;

    /// <summary>The ghost, once created (tests).</summary>
    internal DockDragGhost? Ghost => _ghost;

    /// <summary>The visible button of a slot, or null (tests).</summary>
    internal DockCompassButton? ButtonOf(DockCompassSlot slot)
        => _buttons.TryGetValue(slot, out var button) && button.IsVisible ? button : null;

    /// <summary>The slot whose button is hot (tests).</summary>
    internal DockCompassSlot? HotSlot => _hot;

    /// <summary>The group rectangle the compass is centred on, or null.</summary>
    internal Rect? CompassGroup => _compassGroup;

    /// <summary>True while any indicator shows.</summary>
    internal bool IsShowing => _compassGroup is not null || _edgeArea is not null || _preview.HasTarget || _line.IsVisible
                               || _ghost is { IsVisible: true };

    private double Token(DockIndicatorTokenKind kind, double fallback)
        => this.TryFindResource(DockIndicatorTokens.Key(kind), out var value) && value is double d ? d : fallback;

    protected override void OnAttachedToLogicalTree(Avalonia.LogicalTree.LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        _subscriptions.Add(this.GetResourceObservable(ArcDesignTokenKind.AccentSubtle).Subscribe(new Observer(value =>
        {
            _fillColor = value switch
            {
                ISolidColorBrush brush => brush.Color,
                Color color => color,
                _ => _fillColor
            };
            UpdateFill();
        })));
        _subscriptions.Add(this.GetResourceObservable(DockIndicatorTokens.Key(DockIndicatorTokenKind.PreviewFillOpacity)).Subscribe(
            new Observer(value =>
            {
                if (value is double opacity)
                {
                    _fillOpacity = opacity;
                    UpdateFill();
                }
            })));
    }

    protected override void OnDetachedFromLogicalTree(Avalonia.LogicalTree.LogicalTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromLogicalTree(e);
        foreach (var subscription in _subscriptions)
        {
            subscription.Dispose();
        }
        _subscriptions.Clear();
    }

    private void UpdateFill() => _preview.Fill = new ImmutableSolidColorBrush(_fillColor, _fillOpacity);

    #region Compass and edges

    private DockCompassButton Button(DockCompassSlot slot)
    {
        if (!_buttons.TryGetValue(slot, out var button))
        {
            button = new DockCompassButton(slot) { IsVisible = false };
            _buttons[slot] = button;
            // Above the preview and the line, below the ghost.
            VisualChildren.Insert(VisualChildren.Count - (_ghost is null ? 0 : 1), button);
            LogicalChildren.Add(button);
        }
        return button;
    }

    /// <summary>
    /// Shows the group compass centred on <paramref name="group"/> (with the document area's outer ring when
    /// <paramref name="areaRing"/>), or hides it (null). <paramref name="enabled"/> says which slots accept the drag.
    /// Moving to another group replays the reveal.
    /// </summary>
    public void ShowCompass(Rect? group, bool areaRing, Func<DockCompassSlot, bool> enabled)
    {
        var changed = group != _compassGroup || areaRing != _areaRing;
        _compassGroup = group;
        _areaRing = areaRing;
        var size = Token(DockIndicatorTokenKind.ButtonSize, 36);
        var gap = Token(DockIndicatorTokenKind.ButtonGap, 6);
        var shift = group is { } g ? AwayFromEdges(g, areaRing, size, gap) : default;
        for (var slot = DockCompassSlot.Center; slot <= DockCompassSlot.AreaBottom; slot++)
        {
            var show = group is { } rect && (DockCompassButton.IsGroupSlot(slot) || areaRing);
            if (!show)
            {
                if (_buttons.TryGetValue(slot, out var hidden))
                {
                    hidden.IsVisible = false;
                }
                _rects.Remove(slot);
                continue;
            }
            var button = Button(slot);
            button.IsVisible = true;
            button.IsEnabled = enabled(slot);
            _rects[slot] = CompassRect(group!.Value, slot, size, gap).Translate(shift);
        }
        if (changed && group is not null)
        {
            Reveal(ref _compassReveal, slot => slot <= DockCompassSlot.AreaBottom);
        }
        if (changed)
        {
            InvalidateArrange();
        }
    }

    /// <summary>Shows the four edge buttons along <paramref name="area"/> (the whole layout), or hides them (null).</summary>
    public void ShowEdges(Rect? area, Func<DockCompassSlot, bool> enabled)
    {
        var appearing = _edgeArea is null && area is not null;
        _edgeArea = area;
        var size = Token(DockIndicatorTokenKind.ButtonSize, 36);
        var inset = Token(DockIndicatorTokenKind.EdgeInset, 8);
        for (var slot = DockCompassSlot.EdgeLeft; slot <= DockCompassSlot.EdgeBottom; slot++)
        {
            if (area is not { } a)
            {
                if (_buttons.TryGetValue(slot, out var hidden))
                {
                    hidden.IsVisible = false;
                }
                _rects.Remove(slot);
                continue;
            }
            var button = Button(slot);
            button.IsVisible = true;
            button.IsEnabled = enabled(slot);
            _rects[slot] = slot switch
            {
                DockCompassSlot.EdgeLeft => new Rect(a.X + inset, a.Center.Y - size / 2, size, size),
                DockCompassSlot.EdgeRight => new Rect(a.Right - inset - size, a.Center.Y - size / 2, size, size),
                DockCompassSlot.EdgeTop => new Rect(a.Center.X - size / 2, a.Y + inset, size, size),
                _ => new Rect(a.Center.X - size / 2, a.Bottom - inset - size, size, size)
            };
        }
        if (appearing)
        {
            Reveal(ref _edgeReveal, DockCompassButton.IsEdgeSlot);
        }
        InvalidateArrange();
    }

    /// <summary>
    /// How far the compass of a group moves so that it keeps clear of the edge buttons (a narrow group at the layout's edge
    /// would otherwise put its side button right next to the edge button with the same glyph).
    /// </summary>
    private Vector AwayFromEdges(Rect group, bool areaRing, double size, double gap)
    {
        var last = areaRing ? DockCompassSlot.AreaBottom : DockCompassSlot.Bottom;
        var cluster = CompassRect(group, DockCompassSlot.Center, size, gap);
        for (var slot = DockCompassSlot.Left; slot <= last; slot++)
        {
            cluster = cluster.Union(CompassRect(group, slot, size, gap));
        }
        var clearance = 2 * gap;
        double dx = 0, dy = 0;
        foreach (var (slot, edge) in _rects)
        {
            if (!DockCompassButton.IsEdgeSlot(slot) || !cluster.Translate(new Vector(dx, dy)).Intersects(edge.Inflate(clearance)))
            {
                continue;
            }
            var moved = cluster.Translate(new Vector(dx, dy));
            if (slot is DockCompassSlot.EdgeLeft or DockCompassSlot.EdgeRight)
            {
                // Step off the edge button's row when the group is tall enough, else away from the edge.
                var up = edge.Y - clearance - moved.Bottom;
                var down = edge.Bottom + clearance - moved.Y;
                if (moved.Y + up >= group.Y)
                {
                    dy += up;
                }
                else if (moved.Bottom + down <= group.Bottom)
                {
                    dy += down;
                }
                else
                {
                    dx += slot == DockCompassSlot.EdgeLeft ? edge.Right + clearance - moved.X : edge.X - clearance - moved.Right;
                }
            }
            else
            {
                var left = edge.X - clearance - moved.Right;
                var right = edge.Right + clearance - moved.X;
                if (moved.X + left >= group.X)
                {
                    dx += left;
                }
                else if (moved.Right + right <= group.Right)
                {
                    dx += right;
                }
                else
                {
                    dy += slot == DockCompassSlot.EdgeTop ? edge.Bottom + clearance - moved.Y : edge.Y - clearance - moved.Bottom;
                }
            }
        }
        return new Vector(dx, dy);
    }

    /// <summary>The rectangle of a compass slot around a group rectangle.</summary>
    internal static Rect CompassRect(Rect group, DockCompassSlot slot, double size, double gap)
    {
        var center = group.Center;
        var step = size + gap;
        var ring = 2 * step + gap;
        var (dx, dy) = slot switch
        {
            DockCompassSlot.Left => (-step, 0.0),
            DockCompassSlot.Right => (step, 0.0),
            DockCompassSlot.Top => (0.0, -step),
            DockCompassSlot.Bottom => (0.0, step),
            DockCompassSlot.AreaLeft => (-ring, 0.0),
            DockCompassSlot.AreaRight => (ring, 0.0),
            DockCompassSlot.AreaTop => (0.0, -ring),
            DockCompassSlot.AreaBottom => (0.0, ring),
            _ => (0.0, 0.0)
        };
        return new Rect(center.X + dx - size / 2, center.Y + dy - size / 2, size, size);
    }

    /// <summary>The visible button under <paramref name="point"/> (layer coordinates), enabled or not, or null.</summary>
    public DockCompassSlot? HitTest(Point point)
    {
        foreach (var (slot, rect) in _rects)
        {
            if (rect.Contains(point) && _buttons.TryGetValue(slot, out var button) && button.IsVisible)
            {
                return slot;
            }
        }
        return null;
    }

    /// <summary>Whether a visible slot accepts the drag.</summary>
    public bool IsSlotEnabled(DockCompassSlot slot) => _buttons.TryGetValue(slot, out var button) && button.IsVisible && button.IsEnabled;

    /// <summary>Marks the button under the pointer.</summary>
    public void SetHot(DockCompassSlot? slot)
    {
        if (slot == _hot)
        {
            return;
        }
        if (_hot is { } old && _buttons.TryGetValue(old, out var oldButton))
        {
            oldButton.SetHot(false);
        }
        _hot = slot;
        if (slot is { } now && _buttons.TryGetValue(now, out var button))
        {
            button.SetHot(true);
        }
    }

    /// <summary>Opacity 120ms and, at full motion, a 0.92→1 scale on the enter curve (spec 8); reduced: opacity only.</summary>
    private void Reveal(ref MotionValue<double>? reveal, Func<DockCompassSlot, bool> which)
    {
        var level = ArcMotion.GetEffectiveLevel(this);
        var scaled = level == ArcMotionLevel.Full;
        if (reveal is null)
        {
            var value = new MotionValue<double>(this, 1, MotionVectors.Double);
            value.Changed += progress =>
            {
                foreach (var (slot, button) in _buttons)
                {
                    if (which(slot))
                    {
                        button.SetReveal(progress, ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full);
                    }
                }
            };
            reveal = value;
        }
        if (level == ArcMotionLevel.Off || !IsLoaded)
        {
            reveal.Jump(1);
            return;
        }
        reveal.Jump(0);
        foreach (var (slot, button) in _buttons)
        {
            if (which(slot))
            {
                button.SetReveal(0, scaled);
            }
        }
        _ = reveal.AnimateTo(1, MotionSpec.Tween(s_reveal, ArcEasings.Enter));
    }

    #endregion

    #region Preview, line and ghost

    /// <summary>Glides the preview to <paramref name="rect"/> (snappy spring; reduced and off jump), or fades it out.</summary>
    public void ShowPreview(Rect? rect)
    {
        if (rect is { } target)
        {
            _preview.MoveTo(target, animate: true, ArcSpring.Snappy);
        }
        else
        {
            _preview.Hide();
        }
    }

    /// <summary>Shows the insertion line of a tab strip at <paramref name="rect"/>, or hides it.</summary>
    public void ShowInsertionLine(Rect? rect)
    {
        _line.IsVisible = rect is not null;
        if (rect is { } r && r != _lineRect)
        {
            _lineRect = r;
            InvalidateArrange();
        }
    }

    /// <summary>Shows the ghost for <paramref name="panes"/> with its top-left corner at <paramref name="pointer"/> + offset, or hides it.</summary>
    public void ShowGhost(Point? pointer, IReadOnlyList<DockPane>? panes, DockPane? lead)
    {
        if (pointer is not { } at || panes is null || panes.Count == 0)
        {
            if (_ghost is not null)
            {
                _ghost.IsVisible = false;
            }
            return;
        }
        if (_ghost is null)
        {
            _ghost = new DockDragGhost { RenderTransform = _ghostShift, IsVisible = false };
            VisualChildren.Add(_ghost);
            LogicalChildren.Add(_ghost);
        }
        var main = lead ?? panes[0];
        _ghost.Title = main.ToString();
        _ghost.Icon = main.Icon;
        if (panes.Count != _ghostCount)
        {
            _ghostCount = panes.Count;
            _ghost.Extra = panes.Count > 1 ? $"+{panes.Count - 1}" : null;
        }
        if (!_ghost.IsVisible)
        {
            _ghost.IsVisible = true;
            _ghostOffset = Token(DockIndicatorTokenKind.GhostOffset, 12);
            InvalidateArrange();
        }
        _ghostAt = new Point(at.X + _ghostOffset, at.Y + _ghostOffset);
        // Following the pointer is a transform; the ghost is laid out only when it appears or its text changes.
        PlaceGhost(Bounds.Size);
    }

    /// <summary>
    /// Trails the pointer to the right and below; flips to the other side near the layer's right or bottom edge. Rounded
    /// the way layout rounds an arranged position, so the ghost's text lands on the same pixels.
    /// </summary>
    private void PlaceGhost(Size layer)
    {
        if (_ghost is null)
        {
            return;
        }
        var size = _ghost.DesiredSize;
        var x = _ghostAt.X + size.Width > layer.Width ? _ghostAt.X - 2 * _ghostOffset - size.Width : _ghostAt.X;
        var y = _ghostAt.Y + size.Height > layer.Height ? _ghostAt.Y - 2 * _ghostOffset - size.Height : _ghostAt.Y;
        var scale = LayoutHelper.GetLayoutScale(this);
        _ghostShift.X = LayoutHelper.RoundLayoutValue(Math.Max(0, x), scale);
        _ghostShift.Y = LayoutHelper.RoundLayoutValue(Math.Max(0, y), scale);
    }

    private readonly TranslateTransform _ghostShift = new();
    private int _ghostCount = -1;
    private double _ghostOffset = 12;

    /// <summary>Hides every indicator (end of the drag, or the pointer left this host).</summary>
    public void Clear(bool animate = true)
    {
        ShowCompass(null, false, static _ => false);
        ShowEdges(null, static _ => false);
        SetHot(null);
        _preview.Hide(animate);
        ShowInsertionLine(null);
        ShowGhost(null, null, null);
    }

    #endregion

    protected override Size MeasureOverride(Size availableSize)
    {
        var infinite = new Size(double.PositiveInfinity, double.PositiveInfinity);
        _preview.Measure(availableSize);
        _line.Measure(infinite);
        foreach (var button in _buttons.Values)
        {
            button.Measure(infinite);
        }
        _ghost?.Measure(infinite);
        return default;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _preview.Arrange(new Rect(finalSize));
        _line.Arrange(_line.IsVisible ? _lineRect : default);
        foreach (var (slot, button) in _buttons)
        {
            button.Arrange(_rects.TryGetValue(slot, out var rect) ? rect : default);
        }
        if (_ghost is not null)
        {
            _ghost.Arrange(new Rect(_ghost.DesiredSize));
            PlaceGhost(finalSize);
        }
        return finalSize;
    }

    private sealed class Observer(Action<object?> next) : IObserver<object?>
    {
        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(object? value) => next(value);
    }
}
