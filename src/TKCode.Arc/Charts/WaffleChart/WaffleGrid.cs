using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// The cells of a <see cref="WaffleChart"/>: one focusable, self-drawn grid. Cells are persistent objects that are
/// reassigned on every data change and travel on staggered springs, all evaluated against one clock (no per-cell controls).
/// </summary>
internal sealed class WaffleGrid : Avalonia.Controls.Control, Avalonia.Rendering.ICustomHitTest
{
    /// <summary>The whole box takes the pointer, not only the painted marks.</summary>
    bool Avalonia.Rendering.ICustomHitTest.HitTest(Point point) => new Rect(Bounds.Size).Contains(point);

    private const double Stagger = 0.32;
    private const double DipDuration = 0.62;
    private static readonly double s_morphSettle = ArcMotionPresets.Morph.EstimateSettle().TotalSeconds;
    private static readonly double s_snappySettle = ArcMotionPresets.Snappy.EstimateSettle().TotalSeconds;

    private readonly WaffleChart _owner;
    private readonly ChartsBClock _clock;
    private readonly ChartsBReveal _reveal;
    private readonly ChartsBTooltip _tooltip;
    private readonly Dictionary<string, MotionValue<double>> _dim = new();
    private Cell[] _cells = [];
    private Track[] _tracks = [];
    private (string Key, int Slot)? _pointer;
    private string? _tipKey;
    private ChartsBAutomationPeer? _peer;
    private FormattedText? _empty;
    private int _emptyVersion = -1;
    private string? _emptyText;
    private string? _reading;

    private struct Cell
    {
        public int Id;
        public string? Key;
        public int Slot;
    }

    /// <summary>Animation of one cell from its on-screen state to its new slot, evaluated against the clock.</summary>
    private struct Track
    {
        public Point From;
        public Point To;
        public Point Velocity;
        public double Delay;
        public bool Dip;
        public double ScaleFrom;
        public double ScaleTo;
        public double ScaleVelocity;
        public double ScaleDelay;
        public double OpacityFrom;
        public double OpacityTo;
        public double OpacityDelay;
        public Color ColorFrom;
        public Color ColorTo;
        public double ColorDelay;
        public double RoundFrom;
        public double RoundTo;
        public double FrameFrom;
        public double FrameTo;
    }

    private struct Pose
    {
        public Point Position;
        public Point Velocity;
        public double Scale;
        public double ScaleVelocity;
        public double Opacity;
        public Color Color;
        public double Round;
        public double Frame;
    }

    public WaffleGrid(WaffleChart owner)
    {
        _owner = owner;
        Focusable = true;
        ClipToBounds = false;
        _clock = new ChartsBClock(this, InvalidateVisual);
        _reveal = new ChartsBReveal(this, 0.35, OnReveal);
        _tooltip = new ChartsBTooltip(this, SpringSpec.FromPhysical(520, 44), () => _owner.Overlay.InvalidateVisual())
        {
            Padding = new Thickness(12, 8),
            MinWidth = 150
        };
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
    }

    public string? PointerKey => _pointer?.Key;

    public int? PointerSlot => _pointer?.Slot;

    public string? Reading => _reading;

    internal bool IsRevealed => _reveal.IsRevealed;

    private double CellSize => Math.Max(0, (Bounds.Width - _owner.CellGap * (_owner.Columns - 1)) / _owner.Columns);

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
    {
        var width = double.IsInfinity(availableSize.Width) ? _owner.GridMaxSize : availableSize.Width;
        if (_owner.IsEmpty)
        {
            return new Size(width, 160);
        }
        var gap = _owner.CellGap;
        var size = Math.Max(0, (width - gap * (_owner.Columns - 1)) / _owner.Columns);
        return new Size(width, size * _owner.Rows + gap * (_owner.Rows - 1));
    }

    // ---- data -------------------------------------------------------------------------------------------------

    /// <summary>Hands cells to categories while moving as few as possible, then flies the moved ones.</summary>
    public void OnDataChanged()
    {
        var count = _owner.CellCount;
        var animate = _reveal.IsRevealed && ArcMotion.IsFull(this) && IsLoaded && _cells.Length == count;
        var now = _clock.IsRunning ? _clock.Time : double.PositiveInfinity;
        var poses = new Pose[count];
        for (var id = 0; id < count; id++)
        {
            poses[id] = id < _tracks.Length ? Evaluate(_tracks[id], now) : HiddenPose(SlotPoint(id));
        }
        var previous = new Cell[count];
        for (var id = 0; id < count; id++)
        {
            previous[id] = id < _cells.Length && _cells.Length == count ? _cells[id] : new Cell { Id = id, Key = null, Slot = id };
        }
        var next = Reassign(previous, _owner.Categories, _owner.CellCounts);
        _cells = next;
        if (_pointer is { } pointer)
        {
            _pointer = PointAt(pointer.Slot);
        }
        _tracks = new Track[count];
        for (var id = 0; id < count; id++)
        {
            var cell = next[id];
            var moved = previous[id].Slot != cell.Slot || previous[id].Key != cell.Key;
            var target = SlotPoint(cell.Slot);
            var opacity = cell.Key is null ? 0 : 1;
            var color = _owner.ColorOf(cell.Key);
            var (round, frame) = ShapeOf(cell.Key);
            if (!animate)
            {
                _tracks[id] = Rest(target, _reveal.IsRevealed ? opacity : 0, _reveal.IsRevealed ? 1 : 0.4, color, round, frame);
                continue;
            }
            var pose = poses[id];
            var delay = (double)cell.Slot / count * Stagger;
            _tracks[id] = new Track
            {
                From = pose.Position, To = target, Velocity = pose.Velocity, Delay = delay,
                Dip = moved,
                ScaleFrom = pose.Scale, ScaleTo = 1, ScaleVelocity = pose.ScaleVelocity, ScaleDelay = delay * 0.6,
                OpacityFrom = pose.Opacity, OpacityTo = opacity, OpacityDelay = delay * 0.6,
                ColorFrom = pose.Color.A == 0 ? Colors.Transparent : pose.Color, ColorTo = color, ColorDelay = delay,
                RoundFrom = pose.Round, RoundTo = round, FrameFrom = pose.Frame, FrameTo = frame
            };
        }
        if (animate)
        {
            _clock.Start(Stagger + Math.Max(s_morphSettle, DipDuration) + 0.05);
        }
        else
        {
            _clock.Finish();
        }
        SyncTooltip();
        RefreshReading();
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>Recolors cells in place (accent or theme change); a recolor rides the same stagger as travel.</summary>
    public void OnColorsChanged(bool animate)
    {
        if (_cells.Length == 0)
        {
            return;
        }
        animate &= _reveal.IsRevealed && ArcMotion.IsFull(this) && IsLoaded;
        var now = _clock.IsRunning ? _clock.Time : double.PositiveInfinity;
        for (var id = 0; id < _cells.Length; id++)
        {
            var cell = _cells[id];
            var color = _owner.ColorOf(cell.Key);
            var (round, frame) = ShapeOf(cell.Key);
            if (!animate)
            {
                _tracks[id].ColorFrom = _tracks[id].ColorTo = color;
                _tracks[id].RoundFrom = _tracks[id].RoundTo = round;
                _tracks[id].FrameFrom = _tracks[id].FrameTo = frame;
                continue;
            }
            var pose = Evaluate(_tracks[id], now);
            var delay = (double)cell.Slot / _cells.Length * Stagger;
            _tracks[id] = new Track
            {
                From = pose.Position, To = _tracks[id].To, Velocity = pose.Velocity, Delay = delay,
                ScaleFrom = pose.Scale, ScaleTo = 1, ScaleVelocity = pose.ScaleVelocity, ScaleDelay = delay * 0.6,
                OpacityFrom = pose.Opacity, OpacityTo = _tracks[id].OpacityTo, OpacityDelay = delay * 0.6,
                ColorFrom = pose.Color, ColorTo = color, ColorDelay = delay,
                RoundFrom = pose.Round, RoundTo = round, FrameFrom = pose.Frame, FrameTo = frame
            };
        }
        if (animate)
        {
            _clock.Start(Stagger + s_morphSettle);
        }
        SyncTooltip();
        InvalidateVisual();
    }

    private void OnReveal(bool animate)
    {
        var count = _cells.Length;
        for (var id = 0; id < count; id++)
        {
            var cell = _cells[id];
            var target = SlotPoint(cell.Slot);
            var opacity = cell.Key is null ? 0 : 1;
            var color = _owner.ColorOf(cell.Key);
            var (round, frame) = ShapeOf(cell.Key);
            _tracks[id] = Rest(target, opacity, 1, color, round, frame);
            if (animate)
            {
                // Cells pop in from the bottom-left corner, column by column (snappy scale, staggered fade).
                var delay = (double)cell.Slot / count * Stagger;
                _tracks[id].ScaleFrom = 0.4;
                _tracks[id].ScaleDelay = delay * 0.6;
                _tracks[id].OpacityFrom = 0;
                _tracks[id].OpacityDelay = delay * 0.6;
            }
        }
        if (animate)
        {
            _clock.Start(Stagger * 0.6 + Math.Max(s_snappySettle, 0.24) + 0.05);
        }
        else
        {
            _clock.Finish();
        }
        InvalidateVisual();
    }

    private static Track Rest(Point at, double opacity, double scale, Color color, double round, double frame) => new()
    {
        From = at, To = at, ScaleFrom = scale, ScaleTo = 1, OpacityFrom = opacity, OpacityTo = opacity,
        ColorFrom = color, ColorTo = color, RoundFrom = round, RoundTo = round, FrameFrom = frame, FrameTo = frame
    };

    private static Pose HiddenPose(Point at) => new() { Position = at, Scale = 0.4, Opacity = 0, Color = Colors.Transparent, Round = 0.22 };

    private (double Round, double Frame) ShapeOf(string? key) => _owner.ShapeOf(key) switch
    {
        ChartsBSwatchShape.Round => (0.5, 0),
        ChartsBSwatchShape.Framed => (0.22, 1),
        _ => (0.22, 0)
    };

    /// <summary>Column by column from the bottom-left corner, like a glass filling up (in cell units).</summary>
    private Point SlotPoint(int slot)
    {
        var rows = _owner.Rows;
        return new Point(Math.Floor((double)slot / rows), rows - 1 - slot % rows);
    }

    private static Cell[] Reassign(Cell[] previous, IReadOnlyList<WaffleCategory> data, IReadOnlyList<int> counts)
    {
        var bySlot = previous.OrderBy(c => c.Slot).ToList();
        var kept = new Dictionary<string, List<Cell>>();
        var pool = new List<Cell>();
        var want = new Dictionary<string, int>();
        for (var i = 0; i < data.Count; i++)
        {
            want[data[i].Key] = counts[i];
        }
        foreach (var cell in bySlot)
        {
            if (cell.Key is not null && want.TryGetValue(cell.Key, out var wanted))
            {
                if (!kept.TryGetValue(cell.Key, out var list))
                {
                    list = new List<Cell>();
                    kept[cell.Key] = list;
                }
                if (list.Count < wanted)
                {
                    list.Add(cell);
                    continue;
                }
            }
            pool.Add(cell);
        }
        var next = new List<Cell>(previous.Length);
        var slot = 0;
        var poolIndex = 0;
        for (var i = 0; i < data.Count; i++)
        {
            var own = kept.TryGetValue(data[i].Key, out var list) ? list : new List<Cell>();
            kept.Remove(data[i].Key);
            while (own.Count < counts[i] && poolIndex < pool.Count)
            {
                own.Add(pool[poolIndex++]);
            }
            foreach (var cell in own.OrderBy(c => c.Slot))
            {
                next.Add(new Cell { Id = cell.Id, Key = data[i].Key, Slot = slot++ });
            }
        }
        for (; poolIndex < pool.Count; poolIndex++)
        {
            next.Add(new Cell { Id = pool[poolIndex].Id, Key = null, Slot = slot++ });
        }
        return next.OrderBy(c => c.Id).ToArray();
    }

    // ---- motion -----------------------------------------------------------------------------------------------

    private static Pose Evaluate(in Track track, double t)
    {
        var pose = new Pose();
        if (double.IsPositiveInfinity(t))
        {
            pose.Position = track.To;
            pose.Scale = track.ScaleTo;
            pose.Opacity = track.OpacityTo;
            pose.Color = track.ColorTo;
            pose.Round = track.RoundTo;
            pose.Frame = track.FrameTo;
            return pose;
        }
        var morph = ArcMotionPresets.Morph;
        var (x, vx) = ChartsBMath.Spring(morph, track.From.X, track.To.X, track.Velocity.X, t - track.Delay);
        var (y, vy) = ChartsBMath.Spring(morph, track.From.Y, track.To.Y, track.Velocity.Y, t - track.Delay);
        pose.Position = new Point(x, y);
        pose.Velocity = new Point(vx, vy);
        if (track.Dip)
        {
            // A travelling cell dips while it flies: [1, 0.62, 1] over 0.62s, the low point at 42%.
            var p = ChartsBMath.Clamp((t - track.Delay) / DipDuration, 0, 1);
            pose.Scale = p < 0.42
                ? ChartsBMath.Mix(1, 0.62, ArcEasings.InOut.Ease(p / 0.42))
                : ChartsBMath.Mix(0.62, 1, ArcEasings.InOut.Ease((p - 0.42) / 0.58));
        }
        else
        {
            (pose.Scale, pose.ScaleVelocity) = ChartsBMath.Spring(ArcMotionPresets.Snappy, track.ScaleFrom, track.ScaleTo,
                track.ScaleVelocity, t - track.ScaleDelay);
        }
        var o = ChartsBMath.Clamp((t - track.OpacityDelay) / ArcMotionPresets.Standard.TotalSeconds, 0, 1);
        pose.Opacity = ChartsBMath.Mix(track.OpacityFrom, track.OpacityTo, ArcEasings.Standard.Ease(o));
        var c = ArcEasings.Standard.Ease(ChartsBMath.Clamp((t - track.ColorDelay) / ArcMotionPresets.Considered.TotalSeconds, 0, 1));
        pose.Color = ChartsBMath.Lerp(track.ColorFrom, track.ColorTo, c);
        pose.Round = ChartsBMath.Mix(track.RoundFrom, track.RoundTo, c);
        pose.Frame = ChartsBMath.Mix(track.FrameFrom, track.FrameTo, c);
        return pose;
    }

    private double DimOf(string? key)
    {
        if (key is null || !_dim.TryGetValue(key, out var value))
        {
            return 0;
        }
        return value.Value;
    }

    /// <summary>The focused category stays, the others fade to 0.26 (standard 240ms).</summary>
    public void OnFocusKeyChanged(string? focus)
    {
        var full = ArcMotion.IsFull(this) && IsLoaded;
        foreach (var item in _owner.Categories)
        {
            if (!_dim.TryGetValue(item.Key, out var value))
            {
                value = new MotionValue<double>(this, 0, MotionVectors.Double);
                value.Changed += _ => InvalidateVisual();
                _dim[item.Key] = value;
            }
            var target = focus is not null && focus != item.Key ? 1 : 0;
            if (full)
            {
                _ = value.AnimateTo(target, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard));
            }
            else
            {
                value.Jump(target);
            }
        }
        InvalidateVisual();
    }

    // ---- interaction ------------------------------------------------------------------------------------------

    private int? SlotAt(Point point)
    {
        var size = CellSize;
        if (size <= 0)
        {
            return null;
        }
        var gap = _owner.CellGap;
        var rows = _owner.Rows;
        var col = (int)ChartsBMath.Clamp(Math.Floor(point.X / (size + gap)), 0, _owner.Columns - 1);
        var row = (int)ChartsBMath.Clamp(Math.Floor(point.Y / (size + gap)), 0, rows - 1);
        return col * rows + (rows - 1 - row);
    }

    private (string Key, int Slot)? PointAt(int? slot)
    {
        if (slot is null)
        {
            return null;
        }
        foreach (var cell in _cells)
        {
            if (cell.Slot == slot)
            {
                return cell.Key is null ? null : (cell.Key, slot.Value);
            }
        }
        return null;
    }

    private void SetPointer((string Key, int Slot)? pointer)
    {
        if (pointer == _pointer)
        {
            return;
        }
        var keyChanged = pointer?.Key != _pointer?.Key;
        _pointer = pointer;
        if (keyChanged)
        {
            _owner.OnFocusChanged();
        }
        SyncTooltip();
        RefreshReading();
        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_owner.IsEmpty)
        {
            return;
        }
        var point = e.GetCurrentPoint(this);
        if (e.Pointer.Type == PointerType.Mouse || point.Properties.IsLeftButtonPressed)
        {
            SetPointer(PointAt(SlotAt(point.Position)));
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_owner.IsEmpty)
        {
            return;
        }
        if (e.Pointer.Type != PointerType.Mouse)
        {
            e.Pointer.Capture(this);
        }
        SetPointer(PointAt(SlotAt(e.GetPosition(this))));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.Pointer.Type != PointerType.Mouse)
        {
            SetPointer(null);
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (e.Pointer.Type != PointerType.Mouse)
        {
            SetPointer(null);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (e.Pointer.Type == PointerType.Mouse)
        {
            SetPointer(null);
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (!_owner.IsEmpty && e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional)
        {
            SetPointer(_pointer ?? PointAt(0));
        }
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        SetPointer(null);
    }

    /// <summary>Arrows walk the grid cell by cell; Page keys jump between categories; Home / End go to the first and last filled cell.</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || _owner.IsEmpty)
        {
            return;
        }
        if (e.Key == Key.Escape && _pointer is not null)
        {
            e.Handled = true;
            SetPointer(null);
            return;
        }
        var rows = _owner.Rows;
        var filled = _cells.Count(c => c.Key is not null);
        var at = _pointer?.Slot ?? 0;
        var col = at / rows;
        var row = at % rows;
        var counts = _owner.CellCounts;
        var blocks = new List<(int Start, int Size)>();
        var start = 0;
        foreach (var size in counts)
        {
            if (size > 0)
            {
                blocks.Add((start, size));
            }
            start += size;
        }
        var block = Math.Max(0, blocks.FindIndex(b => at >= b.Start && at < b.Start + b.Size));
        int? target = e.Key switch
        {
            Key.Up => col * rows + Math.Min(rows - 1, row + 1),
            Key.Down => col * rows + Math.Max(0, row - 1),
            Key.Right => Math.Min(_owner.Columns - 1, col + 1) * rows + row,
            Key.Left => Math.Max(0, col - 1) * rows + row,
            Key.PageDown when blocks.Count > 0 => blocks[Math.Min(blocks.Count - 1, block + 1)].Start,
            Key.PageUp when blocks.Count > 0 => at > blocks[block].Start ? blocks[block].Start : blocks[Math.Max(0, block - 1)].Start,
            Key.Home => 0,
            Key.End => filled - 1,
            _ => null
        };
        if (target is null)
        {
            return;
        }
        e.Handled = true;
        SetPointer(PointAt((int)ChartsBMath.Clamp(_pointer is null ? 0 : target.Value, 0, Math.Max(0, filled - 1))));
    }

    // ---- reading ----------------------------------------------------------------------------------------------

    public void RefreshReading()
    {
        var item = _pointer is { } pointer ? _owner.Categories.FirstOrDefault(c => c.Key == pointer.Key) : null;
        _reading = item is null ? null : $"{item.Label}, {_owner.ShareText(_owner.ShareOf(item))}, {_owner.FormatValue(item)}";
        Focusable = !_owner.IsEmpty;
        _peer?.Announce();
        if (_tipKey is not null)
        {
            SyncTooltip(force: true);
        }
    }

    private void SyncTooltip(bool force = false)
    {
        var size = CellSize;
        var item = _pointer is { } pointer ? _owner.Categories.FirstOrDefault(c => c.Key == pointer.Key) : null;
        if (item is null || size <= 0 || _pointer is null)
        {
            _tooltip.Hide();
            _owner.Overlay.InvalidateVisual();
            return;
        }
        var palette = _owner.Palette;
        if (force || _tipKey != item.Key)
        {
            _tipKey = item.Key;
            var sm = palette.TypeSM;
            _tooltip.SetRows(
            [
                new ChartsBTipRow
                {
                    Swatch = _owner.ColorOf(item.Key), Shape = _owner.ShapeOf(item.Key),
                    Left = ChartsBText.Make(item.Label, palette, sm, palette.TextSecondary),
                    Right = ChartsBText.Make(_owner.ShareText(_owner.ShareOf(item)), palette, sm, palette.Foreground, FontWeight.Medium),
                    MinHeight = sm * 1.3
                },
                new ChartsBTipRow
                {
                    Indent = 18,
                    Left = ChartsBText.Make(_owner.FormatValue(item), palette, palette.TypeXS, palette.TextMuted),
                    MinHeight = palette.TypeXS * 1.5
                }
            ]);
        }
        var host = _owner.Bounds.Size;
        var origin = Bounds.Position;
        var gap = _owner.CellGap;
        var slot = SlotPoint(_pointer.Value.Slot);
        var cx = origin.X + slot.X * (size + gap) + size / 2;
        var cy = origin.Y + slot.Y * (size + gap);
        var tip = _tooltip.Size;
        var top = cy - tip.Height - 10;
        if (top < 0)
        {
            top = cy + size + 10;
        }
        var left = ChartsBMath.Clamp(cx - tip.Width / 2, 0, Math.Max(0, host.Width - tip.Width));
        _tooltip.Show(new Point(left, ChartsBMath.Clamp(top, 0, Math.Max(0, host.Height - tip.Height))));
        _owner.Overlay.InvalidateVisual();
    }

    public void DrawTooltip(DrawingContext context, Point origin) => _tooltip.Draw(context, _owner.Palette);

    // ---- drawing ----------------------------------------------------------------------------------------------

    public override void Render(DrawingContext context)
    {
        var palette = _owner.Palette;
        if (_owner.IsEmpty)
        {
            if (_empty is null || _emptyVersion != palette.Version || _emptyText != _owner.EmptyLabel)
            {
                _emptyVersion = palette.Version;
                _emptyText = _owner.EmptyLabel;
                _empty = ChartsBText.Make(_owner.EmptyLabel, palette, palette.TypeSM, palette.TextMuted);
            }
            context.DrawText(_empty, new Point((Bounds.Width - _empty.Width) / 2, (Bounds.Height - _empty.Height) / 2));
            return;
        }
        if (!_reveal.IsRevealed)
        {
            return;
        }
        var size = CellSize;
        if (size <= 0)
        {
            return;
        }
        var gap = _owner.CellGap;
        var t = _clock.IsRunning ? _clock.Time : double.PositiveInfinity;
        var fade = _reveal.Fade;
        var surface = palette.Surface;
        Rect? cursor = null;
        for (var id = 0; id < _cells.Length; id++)
        {
            var cell = _cells[id];
            var pose = Evaluate(_tracks[id], t);
            var dim = DimOf(cell.Key);
            var opacity = ChartsBMath.Mix(pose.Opacity, 0.26, dim) * fade;
            if (pose.Color.A == 0 || opacity <= 0.002 || pose.Scale <= 0.01)
            {
                continue;
            }
            var x = pose.Position.X * (size + gap);
            var y = pose.Position.Y * (size + gap);
            var rect = new Rect(x, y, size, size);
            if (_pointer is { } pointer && pointer.Slot == cell.Slot && cell.Key is not null)
            {
                cursor = rect;
            }
            var scaled = pose.Scale >= 0.999 ? rect : rect.CenterRect(new Rect(0, 0, size * pose.Scale, size * pose.Scale));
            var brush = _clock.IsRunning ? new ImmutableSolidColorBrush(pose.Color) : palette.Brush(pose.Color);
            using (context.PushOpacity(opacity))
            {
                context.DrawRectangle(brush, null, new RoundedRect(scaled, scaled.Width * pose.Round));
                if (pose.Frame > 0.01)
                {
                    // Framed: a 0.75px ring of the surface 2.5px inside the edge, the second, color-free channel.
                    var inset = 2.875 * pose.Scale;
                    var ring = scaled.Deflate(inset);
                    if (ring.Width > 0)
                    {
                        context.DrawRectangle(null, palette.Pen(ChartsBMath.Alpha(surface, pose.Frame), 0.75 * pose.Scale),
                            new RoundedRect(ring, Math.Max(0, ring.Width * pose.Round)));
                    }
                }
            }
        }
        if (cursor is { } c)
        {
            // Cursor: a 2px surface gap and a 1.5px ring of the foreground at 55%.
            var radius = c.Width * 0.22;
            context.DrawRectangle(null, palette.Pen(surface, 2), new RoundedRect(c.Inflate(1), radius + 1));
            context.DrawRectangle(null, palette.Pen(ChartsBMath.Alpha(palette.Foreground, 0.55), 1.5), new RoundedRect(c.Inflate(2.75), radius + 2.75));
        }
    }
}
