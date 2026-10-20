using TKCode.Arc.Gestures;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// The saved swatches row (Arc's Reorder.Group): each swatch is a small drawn, focusable control placed by a render
/// transform. A saved swatch pops in from 0.4 on the snappy spring (P26), the others slide to make room on the smooth
/// spring (P19); a removed one shrinks away. Dragging lifts a swatch to 1.15 and the row reorders under it; Alt + arrow
/// keys move the focused swatch, Delete removes it, arrows / Home / End move focus (one tab stop for the row).
/// </summary>
internal sealed class ColorPickerSwatchRow : Control
{
    public const double Slot = 30;
    public const double Gap = 6;
    private const double Pitch = Slot + Gap;

    public static readonly StyledProperty<IBrush?> CheckerColorProperty = ColorPickerChip.CheckerColorProperty.AddOwner<ColorPickerSwatchRow>();
    public static readonly StyledProperty<IBrush?> CheckerBaseProperty = ColorPickerChip.CheckerBaseProperty.AddOwner<ColorPickerSwatchRow>();

    public static readonly StyledProperty<IBrush?> CurrentRingBrushProperty =
        AvaloniaProperty.Register<ColorPickerSwatchRow, IBrush?>(nameof(CurrentRingBrush));

    public static readonly StyledProperty<IBrush?> FocusRingBrushProperty =
        AvaloniaProperty.Register<ColorPickerSwatchRow, IBrush?>(nameof(FocusRingBrush));

    public IBrush? CheckerColor { get => GetValue(CheckerColorProperty); set => SetValue(CheckerColorProperty, value); }
    public IBrush? CheckerBase { get => GetValue(CheckerBaseProperty); set => SetValue(CheckerBaseProperty, value); }
    public IBrush? CurrentRingBrush { get => GetValue(CurrentRingBrushProperty); set => SetValue(CurrentRingBrushProperty, value); }
    public IBrush? FocusRingBrush { get => GetValue(FocusRingBrushProperty); set => SetValue(FocusRingBrushProperty, value); }

    private readonly List<Swatch> _order = new();
    private readonly List<Swatch> _leaving = new();
    private readonly Stack<Swatch> _pool = new();
    private string _currentHex = string.Empty;
    private string? _tabId;

    static ColorPickerSwatchRow()
    {
        ClipToBoundsProperty.OverrideDefaultValue<ColorPickerSwatchRow>(false);
        AutomationProperties.AccessibilityViewProperty.OverrideDefaultValue<ColorPickerSwatchRow>(AccessibilityView.Content);
    }

    /// <summary>Swatches in row order.</summary>
    public IReadOnlyList<ColorSwatch> Items => _order.Select(s => s.Item!).ToList();

    public string MoveHint { get; set; } = "Alt and arrow keys to move, Delete to remove";

    /// <summary>A swatch was clicked (not dragged) or activated with Enter / Space.</summary>
    public event EventHandler<ColorSwatch>? SwatchInvoked;

    /// <summary>The swatch at <c>From</c> should move to <c>To</c> (drag or Alt + arrows).</summary>
    public event EventHandler<(int From, int To)>? MoveRequested;

    /// <summary>The swatch should be removed (Delete / Backspace).</summary>
    public event EventHandler<ColorSwatch>? RemoveRequested;

    /// <summary>The last swatch was removed while focused.</summary>
    public event EventHandler? EmptiedWhileFocused;

    /// <summary>Shows <paramref name="items"/>: new swatches pop in, kept ones slide to their slots, removed ones shrink away.</summary>
    public void Sync(IReadOnlyList<ColorSwatch> items, string currentHex, bool animate)
    {
        _currentHex = currentHex;
        var full = animate && IsLoaded && ArcMotion.IsFull(this);
        var fade = animate && IsLoaded && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off;
        var next = new List<Swatch>(items.Count);
        foreach (var item in items)
        {
            var existing = _order.FirstOrDefault(s => s.Item!.Id == item.Id);
            if (existing is not null)
            {
                existing.Item = item;
                next.Add(existing);
            }
            else
            {
                var swatch = Rent(item);
                swatch.JumpX(next.Count * Pitch);
                swatch.Enter(full, fade);
                next.Add(swatch);
            }
        }
        foreach (var gone in _order)
        {
            if (!next.Contains(gone))
            {
                Leave(gone, full, fade);
            }
        }
        _order.Clear();
        _order.AddRange(next);
        for (var i = 0; i < _order.Count; i++)
        {
            var swatch = _order[i];
            swatch.Index = i;
            if (!swatch.IsDragging)
            {
                swatch.MoveTo(i * Pitch, full);
            }
            swatch.SetCurrent(string.Equals(ColorPickerMath.ToHex(ColorPickerMath.FromColor(swatch.Item!.Color)), currentHex, StringComparison.Ordinal), full);
            swatch.UpdateAutomation(MoveHint);
        }
        UpdateTabStop();
        InvalidateMeasure();
    }

    /// <summary>Marks the swatch that equals the picker's color.</summary>
    public void SetCurrentHex(string hex, bool animate)
    {
        _currentHex = hex;
        var full = animate && IsLoaded && ArcMotion.IsFull(this);
        foreach (var swatch in _order)
        {
            swatch.SetCurrent(string.Equals(ColorPickerMath.ToHex(ColorPickerMath.FromColor(swatch.Item!.Color)), hex, StringComparison.Ordinal), full);
        }
    }

    public bool FocusSwatch(string id)
    {
        var swatch = _order.FirstOrDefault(s => s.Item!.Id == id);
        if (swatch is null)
        {
            return false;
        }
        _tabId = id;
        UpdateTabStop();
        return swatch.Focus(NavigationMethod.Directional);
    }

    internal Control? SwatchView(string id) => _order.FirstOrDefault(s => s.Item!.Id == id);

    private void UpdateTabStop()
    {
        if (_tabId is null || _order.All(s => s.Item!.Id != _tabId))
        {
            _tabId = _order.Count > 0 ? _order[0].Item!.Id : null;
        }
        foreach (var swatch in _order)
        {
            swatch.IsTabStop = swatch.Item!.Id == _tabId;
        }
    }

    private Swatch Rent(ColorSwatch item)
    {
        var swatch = _pool.Count > 0 ? _pool.Pop() : CreateSwatch();
        swatch.Item = item;
        swatch.IsVisible = true;
        swatch.IsHitTestVisible = true;
        swatch.Reset();
        return swatch;
    }

    private Swatch CreateSwatch()
    {
        var swatch = new Swatch(this);
        VisualChildren.Add(swatch);
        LogicalChildren.Add(swatch);
        return swatch;
    }

    private void Leave(Swatch swatch, bool full, bool fade)
    {
        if (swatch.IsFocused)
        {
            // Focus moves before the swatch goes (the owner places it on the neighbour or the input).
            swatch.IsTabStop = false;
        }
        swatch.IsHitTestVisible = false;
        _leaving.Add(swatch);
        swatch.Exit(full, fade).ContinueWith(_ => Dispatcher.UIThread.Post(() =>
        {
            if (_leaving.Remove(swatch))
            {
                swatch.IsVisible = false;
                swatch.Item = null;
                _pool.Push(swatch);
            }
        }), TaskScheduler.Default);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var visual in VisualChildren)
        {
            (visual as Layoutable)?.Measure(new Size(Slot, Slot));
        }
        var width = _order.Count == 0 ? 0 : _order.Count * Pitch - Gap;
        return new Size(width, Slot + 2);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var visual in VisualChildren)
        {
            (visual as Layoutable)?.Arrange(new Rect(0, (finalSize.Height - Slot) / 2, Slot, Slot));
        }
        return finalSize;
    }

    // ---------------------------------------------------------------- input from swatches

    private void OnSwatchKey(Swatch swatch, KeyEventArgs e)
    {
        var index = _order.IndexOf(swatch);
        if (index < 0 || swatch.Item is not { } item)
        {
            return;
        }
        if (e.Key is Key.Delete or Key.Back)
        {
            e.Handled = true;
            var neighbour = _order.Count > 1 ? _order[Math.Min(index + 1, _order.Count - 1) == index ? index - 1 : index + 1] : null;
            RemoveRequested?.Invoke(this, item);
            if (neighbour?.Item is { } next)
            {
                FocusSwatch(next.Id);
            }
            else
            {
                EmptiedWhileFocused?.Invoke(this, EventArgs.Empty);
            }
            return;
        }
        if (e.Key is Key.Enter or Key.Space)
        {
            e.Handled = true;
            SwatchInvoked?.Invoke(this, item);
            return;
        }
        var move = e.Key == Key.Right ? 1 : e.Key == Key.Left ? -1 : 0;
        if (move == 0 && e.Key is not (Key.Home or Key.End))
        {
            return;
        }
        e.Handled = true;
        if (move != 0 && e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            var to = index + move;
            if (to >= 0 && to < _order.Count)
            {
                MoveRequested?.Invoke(this, (index, to));
                FocusSwatch(item.Id);
            }
            return;
        }
        var target = e.Key == Key.Home ? 0 : e.Key == Key.End ? _order.Count - 1 : ((index + move) % _order.Count + _order.Count) % _order.Count;
        FocusSwatch(_order[target].Item!.Id);
    }

    private void OnSwatchFocused(Swatch swatch)
    {
        _tabId = swatch.Item?.Id;
        UpdateTabStop();
    }

    private void OnSwatchDragged(Swatch swatch, double x)
    {
        var max = Math.Max(0, (_order.Count - 1) * Pitch);
        // dragElastic 0.12: past the row's ends the swatch follows at an eighth of the pointer.
        var shown = x < 0 ? x * 0.12 : x > max ? max + (x - max) * 0.12 : x;
        swatch.JumpX(shown);
        var from = _order.IndexOf(swatch);
        var to = (int)Math.Clamp(Math.Round(shown / Pitch), 0, _order.Count - 1);
        if (from >= 0 && to != from)
        {
            MoveRequested?.Invoke(this, (from, to));
        }
    }

    private void OnSwatchDropped(Swatch swatch)
    {
        var index = _order.IndexOf(swatch);
        if (index >= 0)
        {
            swatch.MoveTo(index * Pitch, IsLoaded && ArcMotion.IsFull(this));
        }
    }

    /// <summary>One saved swatch: a 30px button with a 26px chip; the current one gets a 2px ring and its chip shrinks to 0.78.</summary>
    private sealed class Swatch : Control
    {
        private readonly ColorPickerSwatchRow _row;
        private readonly DragTracker _drag;
        private MotionValue<double>? _x;
        private MotionValue<double>? _pop;
        private MotionValue<double>? _opacity;
        private MotionValue<double>? _chip;
        private MotionValue<double>? _lift;
        private readonly MatrixTransform _transform = new();
        private ImmutableSolidColorBrush? _fill;
        private IPen? _ring;
        private IPen? _focus;
        private bool _current;
        private double _dragStart;
        private IBrush? _ringBrush;
        private IBrush? _focusBrush;

        public Swatch(ColorPickerSwatchRow row)
        {
            _row = row;
            Focusable = true;
            Cursor = new Cursor(StandardCursorType.Hand);
            RenderTransform = _transform;
            ClipToBounds = false;
            _drag = new DragTracker(this, new DragOptions { Axis = DragAxis.Horizontal, HandledEventsToo = true });
            _drag.Started += (_, _) =>
            {
                IsDragging = true;
                _dragStart = X;
                ZIndex = 2;
                Lift(1.15);
            };
            _drag.Moved += (_, e) => _row.OnSwatchDragged(this, _dragStart + e.Offset.X);
            _drag.Completed += (_, _) => Drop();
            _drag.Cancelled += (_, _) => Drop();
            _drag.Released += (_, _) =>
            {
                if (Item is { } item)
                {
                    _row.SwatchInvoked?.Invoke(_row, item);
                }
            };
        }

        public ColorSwatch? Item { get; set; }
        public int Index { get; set; }
        public bool IsDragging { get; private set; }
        public double X => _x?.Value ?? 0;

        private void Drop()
        {
            IsDragging = false;
            ZIndex = 0;
            Lift(1);
            // A drag never also counts as a click: the tracker reports Released only for presses that stayed put.
            _row.OnSwatchDropped(this);
        }

        private MotionValue<double> Value(ref MotionValue<double>? field, double initial)
        {
            if (field is null)
            {
                field = new MotionValue<double>(this, initial, MotionVectors.Double);
                field.Changed += _ => Apply();
            }
            return field;
        }

        public void Reset()
        {
            Value(ref _pop, 1).Jump(1);
            Value(ref _opacity, 1).Jump(1);
            Value(ref _lift, 1).Jump(1);
            _current = false;
            Value(ref _chip, 1).Jump(1);
            IsDragging = false;
            ZIndex = 0;
            Apply();
        }

        public void JumpX(double x) => Value(ref _x, x).Jump(x);

        public void MoveTo(double x, bool animate)
        {
            var value = Value(ref _x, x);
            if (Math.Abs(value.Target - x) < 0.01 && Math.Abs(value.Value - x) < 0.01)
            {
                return;
            }
            if (animate)
            {
                _ = value.AnimateTo(x, MotionSpec.Spring(ArcSpring.Smooth));
            }
            else
            {
                value.Jump(x);
            }
        }

        public void Enter(bool full, bool fade)
        {
            var pop = Value(ref _pop, 1);
            var opacity = Value(ref _opacity, 1);
            if (!fade)
            {
                pop.Jump(1);
                opacity.Jump(1);
                return;
            }
            opacity.Jump(0);
            _ = opacity.AnimateTo(1, MotionSpec.Tween(full ? ArcMotionPresets.Fast : ArcMotionPresets.Instant, ArcEasings.Standard));
            if (full)
            {
                pop.Jump(0.4);
                _ = pop.AnimateTo(1, MotionSpec.Spring(ArcSpring.Snappy));
            }
        }

        public Task<bool> Exit(bool full, bool fade)
        {
            if (!fade)
            {
                return Task.FromResult(true);
            }
            if (full)
            {
                _ = Value(ref _pop, 1).AnimateTo(0.4, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
            }
            return Value(ref _opacity, 1).AnimateTo(0, MotionSpec.Tween(full ? ArcMotionPresets.Fast : ArcMotionPresets.Instant, ArcEasings.Standard));
        }

        private void Lift(double scale)
        {
            var lift = Value(ref _lift, 1);
            if (IsLoaded && ArcMotion.IsFull(this))
            {
                _ = lift.AnimateTo(scale, MotionSpec.Spring(ArcSpring.Snappy));
            }
            else
            {
                lift.Jump(1);
            }
        }

        public void SetCurrent(bool current, bool animate)
        {
            if (_current == current)
            {
                return;
            }
            _current = current;
            var chip = Value(ref _chip, 1);
            var target = current ? 0.78 : 1;
            if (animate)
            {
                _ = chip.AnimateTo(target, MotionSpec.Tween(ArcMotionPresets.CssSpring, ArcEasings.CssSpring));
            }
            else
            {
                chip.Jump(target);
            }
            AutomationProperties.SetItemStatus(this, current ? "selected" : string.Empty);
            InvalidateVisual();
        }

        public void UpdateAutomation(string hint)
        {
            if (Item is { } item)
            {
                AutomationProperties.SetName(this, $"{ColorPickerMath.ToHex(ColorPickerMath.FromColor(item.Color))}. {hint}");
            }
        }

        private void Apply()
        {
            var scale = (_pop?.Value ?? 1) * (_lift?.Value ?? 1);
            _transform.Matrix = Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(X, 0);
            Opacity = Math.Clamp(_opacity?.Value ?? 1, 0, 1);
            InvalidateVisual();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (!e.Handled)
            {
                _row.OnSwatchKey(this, e);
            }
        }

        protected override void OnGotFocus(FocusChangedEventArgs e)
        {
            base.OnGotFocus(e);
            _row.OnSwatchFocused(this);
            InvalidateVisual();
        }

        protected override void OnLostFocus(FocusChangedEventArgs e)
        {
            base.OnLostFocus(e);
            InvalidateVisual();
        }

        protected override AutomationPeer OnCreateAutomationPeer() => new SwatchPeer(this);

        public override void Render(DrawingContext context)
        {
            if (Item is not { } item)
            {
                return;
            }
            var center = new Point(Slot / 2, Slot / 2);
            if (_current && _row.CurrentRingBrush is { } ringBrush)
            {
                if (_ring is null || !ReferenceEquals(_ringBrush, ringBrush))
                {
                    _ringBrush = ringBrush;
                    _ring = new Pen(ringBrush, 2);
                }
                context.DrawEllipse(null, _ring, center, Slot / 2 - 1, Slot / 2 - 1);
            }
            var r = 13 * (_chip?.Value ?? 1);
            ColorPickerDrawing.DrawChip(context, center, r, PickerDrawing.Brush(ref _fill, item.Color),
                ColorPickerDrawing.Checker(_row.CheckerColor, _row.CheckerBase));
            if (IsFocused && Classes.Contains(":focus-visible") && _row.FocusRingBrush is { } focus)
            {
                if (_focus is null || !ReferenceEquals(_focusBrush, focus))
                {
                    _focusBrush = focus;
                    _focus = new Pen(focus, 2);
                }
                context.DrawEllipse(null, _focus, center, Slot / 2 + 3, Slot / 2 + 3);
            }
        }

        private sealed class SwatchPeer : ControlAutomationPeer, ISelectionItemProvider
        {
            public SwatchPeer(Swatch owner) : base(owner)
            {
            }

            protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

            public bool IsSelected => ((Swatch)Owner)._current;

            public ISelectionProvider? SelectionContainer => null;

            public void AddToSelection()
            {
            }

            public void RemoveFromSelection()
            {
            }

            public void Select()
            {
                var swatch = (Swatch)Owner;
                if (swatch.Item is { } item)
                {
                    swatch._row.SwatchInvoked?.Invoke(swatch._row, item);
                }
            }
        }
    }
}
