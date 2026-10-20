using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The tiles of a <see cref="Treemap"/>: one focusable control that keeps every tile's on-screen rect and carries all of
/// them to a new layout on one spring (zoom is a single affine map around the pivot tile). No per-tile controls.
/// </summary>
internal sealed class TreemapStage : Avalonia.Controls.Control, Avalonia.Rendering.ICustomHitTest
{
    /// <summary>The whole box takes the pointer, not only the painted marks.</summary>
    bool Avalonia.Rendering.ICustomHitTest.HitTest(Point point) => new Rect(Bounds.Size).Contains(point);

    private const double TipGap = 14;

    private readonly Treemap _owner;
    private readonly ChartsBReveal _reveal;
    private readonly ChartsBTooltip _tooltip;
    private readonly MotionValue<double> _progress;
    private readonly Dictionary<string, MotionValue<double>> _dim = new();
    private Dictionary<string, Drawn> _drawn = new();
    private Dictionary<string, Drawn> _from = new();
    private Dictionary<string, Drawn> _to = new();
    private readonly Dictionary<string, int> _levels = new();
    private List<string> _order = new();
    private (string Id, Dictionary<string, Rect> Layout)? _lastFocus;
    private Dictionary<string, Rect> _target = new();
    private Rect _view;
    private bool _first;
    private bool _animating;
    private int _generation;
    private string? _active;
    private Point? _pointer;
    private string? _tipId;
    private string? _reading;
    private bool _keyboard;
    private ChartsBAutomationPeer? _peer;
    private readonly Dictionary<string, (int Version, FormattedText Label, FormattedText Value)> _text = new();
    private readonly Dictionary<string, (double Width, FormattedText Label, FormattedText Value)> _trimmed = new();
    private FormattedText? _empty;
    private int _emptyVersion = -1;

    private readonly record struct Drawn(Rect Rect, double Opacity);

    public TreemapStage(Treemap owner)
    {
        _owner = owner;
        Focusable = true;
        ClipToBounds = false;
        _reveal = new ChartsBReveal(this, 0.25, _ => Relayout(animate: true));
        _tooltip = new ChartsBTooltip(this, ArcMotionPresets.Snappy, InvalidateVisual) { MinWidth = 160 };
        _progress = new MotionValue<double>(this, 1, MotionVectors.Double);
        _progress.Changed += OnProgress;
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
    }

    public string? Reading => _reading;

    internal string? ActiveId => _active;

    internal bool IsMorphing => _animating;

    /// <summary>Drawn rect of a tile (for tests).</summary>
    internal Rect? RectOf(string id) => _drawn.TryGetValue(id, out var d) && d.Opacity > 0.01 ? d.Rect : null;

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

    protected override Size MeasureOverride(Size availableSize)
        => new(double.IsInfinity(availableSize.Width) ? 640 : availableSize.Width, _owner.PlotHeight);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty && change.GetOldValue<Rect>().Size != change.GetNewValue<Rect>().Size)
        {
            Relayout(animate: false);
        }
    }

    public void OnDataChanged()
    {
        if (_active is not null && !_owner.Flat.ContainsKey(_active))
        {
            SetActive(null, null);
        }
        _text.Clear();
        _trimmed.Clear();
        Focusable = !_owner.IsEmpty;
        Relayout(animate: true);
    }

    public void OnCenterChanged() => Relayout(animate: true);

    public void OnPaletteChanged()
    {
        _text.Clear();
        _trimmed.Clear();
        _tipId = null;
        SyncTooltip();
        InvalidateVisual();
    }

    private bool IsUnder(string id, string ancestor)
    {
        for (var at = _owner.Flat.GetValueOrDefault(id); at is not null; at = at.Parent is { } parent ? _owner.Flat.GetValueOrDefault(parent) : null)
        {
            if (at.Id == ancestor)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Every tile keeps its on-screen rect; a spring carries it to the new layout. Tiles entering or leaving ride the zoom of the tile they belong to.</summary>
    private void Relayout(bool animate)
    {
        var flat = _owner.Flat;
        if (flat.Count == 0)
        {
            return;
        }
        var center = _owner.Center;
        _view = new Rect(Bounds.Size);
        _target = TreemapLayout.From(flat, center.Id, _view);
        if (!_reveal.IsRevealed || _view.Width <= 0)
        {
            InvalidateVisual();
            return;
        }
        var nodes = flat.Values.Where(n => n.Depth > 0).ToList();
        var nextVisible = new HashSet<string>(nodes.Where(n => n.Depth - center.Depth is >= 1 and <= 2 && IsUnder(n.Id, center.Id)).Select(n => n.Id));
        var before = new Dictionary<string, Drawn>(_drawn);
        var wasVisible = new HashSet<string>(before.Where(p => p.Value.Opacity > 0.5).Select(p => p.Key));
        Func<Rect, Rect>? toOld = null;
        Func<Rect, Rect>? toNew = null;
        if (_lastFocus is { } previous && previous.Id != center.Id && before.Count > 0)
        {
            if (IsUnder(center.Id, previous.Id) && previous.Layout.TryGetValue(center.Id, out var inOld))
            {
                toOld = r => TreemapLayout.Carry(r, _view, inOld);
                toNew = r => TreemapLayout.Carry(r, inOld, _view);
            }
            else if (IsUnder(previous.Id, center.Id) && _target.TryGetValue(previous.Id, out var inNew))
            {
                toOld = r => TreemapLayout.Carry(r, inNew, _view);
                toNew = r => TreemapLayout.Carry(r, _view, inNew);
            }
        }
        var from = new Dictionary<string, Drawn>();
        var to = new Dictionary<string, Drawn>();
        var first = before.Count == 0;
        foreach (var node in nodes)
        {
            var hasGoal = _target.TryGetValue(node.Id, out var goal);
            var hasNow = before.TryGetValue(node.Id, out var now);
            if (nextVisible.Contains(node.Id) && hasGoal)
            {
                to[node.Id] = new Drawn(goal, 1);
                if (wasVisible.Contains(node.Id) && hasNow)
                {
                    from[node.Id] = now;
                }
                else if (first)
                {
                    from[node.Id] = new Drawn(new Rect(goal.Center, new Size(0, 0)), 0);
                }
                else
                {
                    from[node.Id] = new Drawn(toOld?.Invoke(goal) ?? goal, 0);
                }
                _levels[node.Id] = node.Depth - center.Depth;
            }
            else if (wasVisible.Contains(node.Id) && hasNow)
            {
                from[node.Id] = now;
                to[node.Id] = new Drawn(toNew?.Invoke(now.Rect) ?? now.Rect, 0);
            }
        }
        _drawn = new Dictionary<string, Drawn>(from);
        // Branches below their children: draw by depth.
        _order = from.Keys.OrderBy(id => flat.TryGetValue(id, out var n) ? n.Depth : 0).ToList();
        _lastFocus = (center.Id, _target);
        _from = from;
        _to = to;
        _first = first;
        _trimmed.Clear();
        if (!animate || !ArcMotion.IsFull(this) || !IsLoaded)
        {
            Finish();
            return;
        }
        _animating = true;
        _progress.Jump(0);
        var spec = first ? ChartsBMath.Physical(0.9, 0) : ChartsBMath.Physical(0.62, 0.06);
        FinishWhenDone(_progress.AnimateTo(1, MotionSpec.Spring(spec)), ++_generation);
        SyncTooltip();
    }

    private async void FinishWhenDone(Task<bool> run, int generation)
    {
        if (await run && generation == _generation)
        {
            Finish();
        }
    }

    private void OnProgress(double t)
    {
        if (!_animating)
        {
            return;
        }
        var area = _view.Width * _view.Height;
        foreach (var (id, a) in _from)
        {
            var b = _to[id];
            // On the first reveal, tiles grow in a quick wave from the largest.
            var local = _first
                ? ChartsBMath.Clamp((t - Math.Min(0.3, (1 - b.Rect.Width * b.Rect.Height / (area == 0 ? 1 : area)) * 0.3)) / 0.7, 0, 1)
                : t;
            _drawn[id] = new Drawn(ChartsBMath.Mix(a.Rect, b.Rect, local), ChartsBMath.Mix(a.Opacity, b.Opacity, local));
        }
        InvalidateVisual();
    }

    private void Finish()
    {
        _animating = false;
        foreach (var (id, d) in _to)
        {
            _drawn[id] = d;
        }
        _trimmed.Clear();
        SyncTooltip();
        InvalidateVisual();
    }

    // ---- interaction ------------------------------------------------------------------------------------------

    public void ClearActive() => SetActive(null, null);

    private TreemapFlat? TopOf(TreemapFlat? node)
    {
        var center = _owner.Center.Id;
        var at = node;
        while (at is not null && at.Parent is { } parent && parent != center)
        {
            at = _owner.Flat.GetValueOrDefault(parent);
        }
        return at is not null && at.Parent == center ? at : null;
    }

    private void SetActive(string? id, Point? pointer)
    {
        var changed = id != _active;
        _active = id;
        _pointer = pointer;
        if (changed)
        {
            var focusTop = TopOf(id is null ? null : _owner.Flat.GetValueOrDefault(id))?.Id;
            var full = ArcMotion.IsFull(this) && IsLoaded;
            foreach (var top in _owner.Center.Children)
            {
                if (!_dim.TryGetValue(top, out var value))
                {
                    value = new MotionValue<double>(this, 0, MotionVectors.Double);
                    value.Changed += _ => InvalidateVisual();
                    _dim[top] = value;
                }
                var target = focusTop is not null && focusTop != top ? 1 : 0;
                if (full)
                {
                    _ = value.AnimateTo(target, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
                }
                else
                {
                    value.Jump(target);
                }
            }
            _reading = id is not null && _owner.Flat.TryGetValue(id, out var node) ? _owner.Describe(node) : null;
            _peer?.Announce();
        }
        SyncTooltip();
        InvalidateVisual();
    }

    private string? HitTest(Point point)
    {
        string? best = null;
        var depth = -1;
        foreach (var (id, d) in _drawn)
        {
            if (d.Opacity <= 0.6 || !d.Rect.Contains(point) || !_owner.Flat.TryGetValue(id, out var node))
            {
                continue;
            }
            if (node.Depth > depth)
            {
                depth = node.Depth;
                best = id;
            }
        }
        return best;
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
        if (hit is null)
        {
            return;
        }
        _keyboard = false;
        var branch = _owner.Flat.TryGetValue(hit, out var node) && node.Children.Count > 0 && _levels.GetValueOrDefault(hit) == 1;
        Cursor = new Cursor(branch ? StandardCursorType.Hand : StandardCursorType.Arrow);
        SetActive(hit, point);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        SetActive(null, null);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var hit = HitTest(e.GetPosition(this));
        if (hit is null || !_owner.Flat.TryGetValue(hit, out var node))
        {
            return;
        }
        e.Handled = true;
        var top = TopOf(node);
        if (top is { Children.Count: > 0 })
        {
            _owner.ZoomTo(top.Id);
        }
        else if (_active != node.Id)
        {
            SetActive(node.Id, e.Pointer.Type == PointerType.Mouse ? e.GetPosition(this) : null);
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _keyboard = e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional;
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _keyboard = false;
        SetActive(null, null);
    }

    /// <summary>Arrows hop to the nearest top-level tile in that direction; Enter zooms in; Escape or Backspace zooms out.</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || _owner.IsEmpty)
        {
            return;
        }
        var center = _owner.Center;
        var tops = center.Children.Select(id => _owner.Flat[id]).Where(n => n.Value > 0).ToList();
        var current = TopOf(_active is null ? null : _owner.Flat.GetValueOrDefault(_active));
        (double X, double Y)? direction = e.Key switch
        {
            Key.Right => (1, 0),
            Key.Left => (-1, 0),
            Key.Down => (0, 1),
            Key.Up => (0, -1),
            _ => null
        };
        _keyboard = true;
        if (direction is { } d)
        {
            e.Handled = true;
            if (current is null)
            {
                SetActive(tops.FirstOrDefault()?.Id, null);
                return;
            }
            var origin = _target[current.Id].Center;
            TreemapFlat? best = null;
            var score = double.PositiveInfinity;
            foreach (var node in tops)
            {
                if (node.Id == current.Id || !_target.TryGetValue(node.Id, out var r))
                {
                    continue;
                }
                var vx = r.Center.X - origin.X;
                var vy = r.Center.Y - origin.Y;
                var along = vx * d.X + vy * d.Y;
                var across = Math.Abs(vx * d.Y - vy * d.X);
                if (along <= 1)
                {
                    continue;
                }
                var s = along + across * 1.5;
                if (s < score)
                {
                    score = s;
                    best = node;
                }
            }
            if (best is not null)
            {
                SetActive(best.Id, null);
            }
        }
        else if (e.Key is Key.Home or Key.End)
        {
            e.Handled = true;
            SetActive((e.Key == Key.Home ? tops.FirstOrDefault() : tops.LastOrDefault())?.Id, null);
        }
        else if (e.Key is Key.Enter or Key.Space)
        {
            if (current is { Children.Count: > 0 })
            {
                e.Handled = true;
                _owner.ZoomTo(current.Id);
                SetActive(current.Children[0], null);
            }
        }
        else if (e.Key is Key.Escape or Key.Back)
        {
            if (center.Parent is { } parent)
            {
                e.Handled = true;
                _owner.ZoomTo(parent);
                SetActive(center.Id, null);
            }
            else if (_active is not null)
            {
                e.Handled = true;
                SetActive(null, null);
            }
        }
    }

    // ---- tooltip ----------------------------------------------------------------------------------------------

    private void SyncTooltip()
    {
        if (_active is null || !_owner.Flat.TryGetValue(_active, out var node) || Bounds.Width <= 0)
        {
            _tooltip.Hide();
            InvalidateVisual();
            return;
        }
        var p = _owner.Palette;
        if (_tipId != node.Id)
        {
            _tipId = node.Id;
            var parent = _owner.ParentOf(node);
            var title = node.Path.Length > 1 ? string.Join(" / ", node.Path.Take(node.Path.Length - 1)) : _owner.RootFlat.Label;
            var rows = new List<ChartsBTipRow>
            {
                new() { Left = ChartsBText.Make(title, p, p.TypeXS, p.TextMuted), MinHeight = p.TypeXS * 1.5 },
                new()
                {
                    MinHeight = p.TypeSM * 1.35,
                    Left = ChartsBText.Make(node.Label, p, p.TypeSM, p.Foreground),
                    Right = ChartsBText.Make(_owner.Format(node.Value), p, p.TypeSM, p.Foreground, FontWeight.Medium)
                },
                new()
                {
                    MinHeight = p.TypeXS * 1.5,
                    Left = ChartsBText.Make($"{ChartsBMath.Percent(Treemap.ShareOf(node, parent), 1)} of {parent.Label}", p, p.TypeXS, p.TextMuted)
                }
            };
            if (_owner.IsColored && node.Color is { } color)
            {
                rows.Add(new ChartsBTipRow
                {
                    MinHeight = p.TypeXS * 1.5,
                    Left = ChartsBText.Make($"{_owner.ColorLabel} {_owner.FormatColor(color)}", p, p.TypeXS, p.TextMuted)
                });
            }
            _tooltip.SetRows(rows);
        }
        // Trails the pointer, or sits by the tile in focus, and stays inside the chart.
        var r = _target.TryGetValue(node.Id, out var rect) ? rect : default;
        var ax = _pointer?.X ?? r.X + r.Width / 2;
        var ay = _pointer?.Y ?? r.Y + Math.Min(r.Height / 2, 28);
        var size = _tooltip.Size;
        var width = Bounds.Width;
        var height = Bounds.Height;
        var left = ax + TipGap;
        if (left + size.Width > width)
        {
            left = ax - TipGap - size.Width;
        }
        var top = ay + TipGap;
        if (top + size.Height > height)
        {
            top = ay - TipGap - size.Height;
        }
        _tooltip.Show(new Point(ChartsBMath.Clamp(left, 0, Math.Max(0, width - size.Width)), ChartsBMath.Clamp(top, 0, Math.Max(0, height - size.Height))));
        InvalidateVisual();
    }

    // ---- drawing ----------------------------------------------------------------------------------------------

    private (FormattedText Label, FormattedText Value) TextOf(TreemapFlat node, double room)
    {
        var p = _owner.Palette;
        if (!_text.TryGetValue(node.Id, out var text) || text.Version != p.Version)
        {
            text = (p.Version, ChartsBText.Make(node.Label, p, p.TypeXS, p.Foreground, FontWeight.Medium),
                ChartsBText.Make(_owner.Format(node.Value), p, p.TypeXS, p.Foreground));
            _text[node.Id] = text;
        }
        if (_animating || text.Label.WidthIncludingTrailingWhitespace <= room)
        {
            return (text.Label, text.Value);
        }
        if (!_trimmed.TryGetValue(node.Id, out var trimmed) || Math.Abs(trimmed.Width - room) > 0.5)
        {
            trimmed = (room, ChartsBText.Trimmed(node.Label, p, p.TypeXS, p.Foreground, room, FontWeight.Medium), text.Value);
            _trimmed[node.Id] = trimmed;
        }
        return (trimmed.Label, trimmed.Value);
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (size.Width <= 0)
        {
            return;
        }
        var p = _owner.Palette;
        if (_owner.IsEmpty)
        {
            if (_empty is null || _emptyVersion != p.Version)
            {
                _emptyVersion = p.Version;
                _empty = ChartsBText.Make(_owner.EmptyLabel, p, p.TypeSM, p.TextMuted);
            }
            context.DrawText(_empty, new Point((size.Width - _empty.Width) / 2, (size.Height - _empty.Height) / 2));
            return;
        }
        using var fade = context.PushOpacity(_reveal.Fade);
        using var clip = context.PushClip(new RoundedRect(new Rect(size), _owner.StageRadius));
        var flat = _owner.Flat;
        var activeTop = TopOf(_active is null ? null : flat.GetValueOrDefault(_active))?.Id;
        foreach (var id in _order)
        {
            if (!_drawn.TryGetValue(id, out var d))
            {
                continue;
            }
            var rect = d.Rect;
            if (d.Opacity < 0.01 || rect.Width < 0.5 || rect.Height < 0.5 || !flat.TryGetValue(id, out var node))
            {
                continue;
            }
            var level = _levels.GetValueOrDefault(id);
            var branch = node.Children.Count > 0;
            var header = branch && level == 1;
            var hue = _owner.HueOf(id);
            var fill = header ? ChartsBMath.MixIn(hue, 0.09, p.Surface) : ChartsBMath.MixIn(hue, _owner.ShadeOf(node.Color), p.Surface);
            var top = TopOf(node)?.Id;
            var dim = top is not null && _dim.TryGetValue(top, out var dv) ? dv.Value : 0;
            var radius = header ? 8 : 6;
            using (context.PushOpacity(d.Opacity * (1 - 0.5 * dim)))
            {
                context.DrawRectangle(p.Brush(fill), null, new RoundedRect(rect, radius));
                DrawLabels(context, node, rect, header, level);
                if (id == _active)
                {
                    context.DrawRectangle(null, p.Pen(p.Foreground, 2), new RoundedRect(rect.Deflate(1), radius - 1));
                }
                else if (id == activeTop)
                {
                    context.DrawRectangle(null, p.Pen(ChartsBMath.Alpha(hue, 0.7), 1), new RoundedRect(rect.Deflate(0.5), radius - 0.5));
                }
            }
        }
        if (_keyboard && IsFocused && _active is null)
        {
            context.DrawRectangle(null, p.Pen(p.FocusRing, 2), new RoundedRect(new Rect(size).Deflate(1), _owner.StageRadius - 1));
        }
        _tooltip.Draw(context, p);
    }

    /// <summary>Labels never scale: they show when the tile has room (name above 64 × 38, value above 54 high).</summary>
    private void DrawLabels(DrawingContext context, TreemapFlat node, Rect rect, bool header, int level)
    {
        if (!(rect.Width > 64 && rect.Height > 38))
        {
            return;
        }
        var full = rect.Height > 54;
        if (header)
        {
            // A top-level branch prints its name and total on one header line.
            var (label, value) = TextOf(node, Math.Max(0, rect.Width - 16 - 8 - _owner.Palette.TypeXS * 3));
            var y = rect.Y + 3 + (16 - label.Height) / 2;
            using (context.PushClip(rect.Deflate(1)))
            {
                var valueX = rect.Right - 8 - value.WidthIncludingTrailingWhitespace;
                context.DrawText(label, new Point(rect.X + 8, y));
                using (context.PushOpacity(0.6))
                {
                    context.DrawText(value, new Point(valueX, y));
                }
            }
            return;
        }
        var pad = level == 2 ? new Thickness(7, 5) : new Thickness(8, 6);
        var room = Math.Max(0, rect.Width - pad.Left - pad.Right);
        var (name, amount) = TextOf(node, room);
        using (context.PushClip(rect.Deflate(1)))
        {
            context.DrawText(name, new Point(rect.X + pad.Left, rect.Y + pad.Top + (16 - name.Height) / 2));
            if (full)
            {
                using (context.PushOpacity(0.72))
                {
                    context.DrawText(amount, new Point(rect.X + pad.Left, rect.Y + pad.Top + 17 + (16 - amount.Height) / 2));
                }
            }
        }
    }
}
