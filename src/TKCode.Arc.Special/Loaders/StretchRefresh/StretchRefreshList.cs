using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One row of <see cref="StretchRefreshList"/>: the item's view padded 12 20, a hairline above it (rows after the first,
/// inset 20 on both sides, moving with the row), and the row's motion: the pull spread (index · tension · 0.05), the
/// layout slide (FLIP) when rows are inserted above it, and the entrance of a new row. All three are render
/// transforms on the row, so neither the pull nor the slide lays anything out.
/// </summary>
internal sealed class StretchRefreshRowHost : Decorator
{
    public static readonly StyledProperty<IBrush?> SeparatorInkProperty =
        AvaloniaProperty.Register<StretchRefreshRowHost, IBrush?>(nameof(SeparatorInk));

    /// <summary>New rows drop 16 px and grow from 0.97 (measured).</summary>
    private const double EnterDrop = 16;

    private const double EnterScale = 0.97;

    private static readonly SpringSpec SlideSpring = SpringSpec.FromVisual(0.4, 0);
    private static readonly SpringSpec EnterYSpring = SpringSpec.FromVisual(0.43, 0.02);
    private static readonly SpringSpec EnterScaleSpring = SpringSpec.FromVisual(0.45, 0.05);

    private readonly TranslateTransform _shift = new();
    private readonly ScaleTransform _scale = new();
    private MotionValue<double>? _slide;
    private MotionValue<double>? _enterY;
    private MotionValue<double>? _enterScale;
    private MotionValue<double>? _enterFade;
    private double _spread;
    private bool _enterFull;
    private ImmutablePen? _pen;
    private int _index = -1;

    static StretchRefreshRowHost()
    {
        AffectsRender<StretchRefreshRowHost>(SeparatorInkProperty);
    }

    public StretchRefreshRowHost(object key, object? item)
    {
        Key = key;
        Item = item;
        RenderTransformOrigin = RelativePoint.Center;
        RenderTransform = new TransformGroup { Children = { _scale, _shift } };
    }

    public object Key { get; }

    public object? Item { get; set; }

    /// <summary>The row's place in the list; the separator is drawn only from the second row on.</summary>
    public int Index
    {
        get => _index;
        set
        {
            if (_index == value)
            {
                return;
            }
            var separatorChanged = (_index == 0) != (value == 0);
            _index = value;
            if (separatorChanged)
            {
                InvalidateVisual();
            }
        }
    }

    /// <summary>The separator (BorderSubtle).</summary>
    public IBrush? SeparatorInk
    {
        get => GetValue(SeparatorInkProperty);
        set => SetValue(SeparatorInkProperty, value);
    }

    /// <summary>The row's resting top inside the list before a change, including any slide still running.</summary>
    public double DrawnTop => Bounds.Y + (_slide?.Value ?? 0);

    /// <summary>True while the row's slide or entrance is still moving.</summary>
    public bool IsMoving => _slide?.IsAnimating == true || _enterFade?.IsAnimating == true || _enterY?.IsAnimating == true;

    /// <summary>The current slide offset (FLIP), for tests.</summary>
    public double SlideOffset => _slide?.Value ?? 0;

    /// <summary>The current spread offset, for tests.</summary>
    public double SpreadOffset => _spread;

    public void SetSpread(double tension)
    {
        var spread = StretchRefreshMath.RowShift(Index, tension);
        if (spread == _spread)
        {
            return;
        }
        _spread = spread;
        Write();
    }

    /// <summary>Starts the slide from <paramref name="offset"/> (old drawn top − new top) to 0 on a smooth spring.</summary>
    public void Slide(double offset)
    {
        if (Math.Abs(offset) < 0.5 && _slide is not { IsAnimating: true })
        {
            return;
        }
        _slide ??= Create(0);
        _slide.Jump(offset);
        _ = _slide.AnimateTo(0, MotionSpec.Spring(SlideSpring));
    }

    /// <summary>
    /// Runs a new row's entrance after <paramref name="delay"/>: down from −16 and up from 0.97 on springs, in from
    /// transparent and a 4 px blur over 240 ms (.16, 1, .3, 1). Reduced motion only fades.
    /// </summary>
    public void Enter(TimeSpan delay, bool full)
    {
        _enterFull = full;
        _enterFade ??= Create(0);
        _enterFade.Jump(0);
        _ = _enterFade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter, delay));
        if (full)
        {
            _enterY ??= Create(0);
            _enterScale ??= Create(1);
            _enterY.Jump(-EnterDrop);
            _enterScale.Jump(EnterScale);
            _ = _enterY.AnimateTo(0, MotionSpec.Spring(EnterYSpring, delay));
            _ = _enterScale.AnimateTo(1, MotionSpec.Spring(EnterScaleSpring, delay));
        }
        Write();
    }

    private MotionValue<double> Create(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => Write();
        return value;
    }

    private void Write()
    {
        var fade = _enterFade?.Value ?? 1;
        Opacity = Math.Clamp(fade, 0, 1);
        Effect = _enterFull && fade < 1 ? ArcBlur.Get(4 * (1 - fade)) : null;
        _shift.Y = _spread + (_slide?.Value ?? 0) + (_enterY?.Value ?? 0);
        var s = _enterScale?.Value ?? 1;
        _scale.ScaleX = _scale.ScaleY = s;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SeparatorInkProperty)
        {
            _pen = null;
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Index == 0 || SeparatorInk is not ISolidColorBrush ink)
        {
            return;
        }
        var inset = Padding.Left;
        var right = Bounds.Width - Padding.Right;
        if (right <= inset)
        {
            return;
        }
        _pen ??= new ImmutablePen(new ImmutableSolidColorBrush(ink.Color, ink.Opacity), 1);
        context.DrawLine(_pen, new Point(inset, 0.5), new Point(right, 0.5));
    }

    public void DisposeMotion()
    {
        _slide?.Dispose();
        _enterY?.Dispose();
        _enterScale?.Dispose();
        _enterFade?.Dispose();
    }
}

/// <summary>
/// The rows of <see cref="StretchRefresh"/>: a vertical stack of <see cref="StretchRefreshRowHost"/>s, one per item,
/// matched by key across changes so a row keeps its view. When items change after the list is on screen, rows that
/// stay slide from where they were drawn to their new place (FLIP on a smooth spring) and new rows enter one after
/// another (100 + 60·i ms). Not virtualized: a feed shows a screen or two of rows.
/// </summary>
internal sealed class StretchRefreshList : Panel
{
    /// <summary>Delay of the first new row and the stagger between new rows (measured 117 / 178 / 238 ms).</summary>
    public static readonly TimeSpan EnterDelay = TimeSpan.FromMilliseconds(100);

    public static readonly TimeSpan EnterStagger = TimeSpan.FromMilliseconds(60);

    private readonly Dictionary<object, StretchRefreshRowHost> _byKey = new();
    private readonly List<(StretchRefreshRowHost Host, double Top)> _pendingSlides = new();
    private IEnumerable? _items;
    private INotifyCollectionChanged? _observed;
    private double _tension;
    private bool _syncPosted;
    private Action? _postedSync;
    // Reused by every Sync (no per-change collections).
    private readonly Dictionary<object, double> _tops = new();
    private readonly List<StretchRefreshRowHost> _next = new();
    private readonly HashSet<object> _seen = new();
    private readonly List<StretchRefreshRowHost> _entering = new();
    private readonly List<object> _gone = new();

    /// <summary>The items' template; null shows <see cref="StretchRefreshActivity"/> items as activity rows and others as text.</summary>
    public IDataTemplate? ItemTemplate { get; set; }

    /// <summary>The key of an item (null: the item itself, or <see cref="StretchRefreshActivity.Id"/>).</summary>
    public Func<object, object>? KeySelector { get; set; }

    /// <summary>Raised after the rows were rebuilt from the items, with the number of new rows.</summary>
    public event Action<int>? RowsChanged;

    /// <summary>The rows in order (tests).</summary>
    public IEnumerable<StretchRefreshRowHost> Rows => Children.OfType<StretchRefreshRowHost>();

    public void SetItems(IEnumerable? items)
    {
        if (_observed is not null)
        {
            _observed.CollectionChanged -= OnCollectionChanged;
        }
        _items = items;
        _observed = items as INotifyCollectionChanged;
        if (_observed is not null)
        {
            _observed.CollectionChanged += OnCollectionChanged;
        }
        Sync(animate: false);
    }

    /// <summary>Rebuilds every row (the template changed).</summary>
    public void Rebuild()
    {
        foreach (var host in _byKey.Values)
        {
            host.DisposeMotion();
        }
        _byKey.Clear();
        Children.Clear();
        Sync(animate: false);
    }

    /// <summary>
    /// Spreads the rows for a pull. Only rows that start inside the visible part of the feed are moved (a pull only
    /// happens at the top, and every shift is downwards, so rows below the viewport stay out of sight); a skipped row is
    /// reset to no shift.
    /// </summary>
    public void SetSpread(double tension)
    {
        _tension = tension;
        var limit = VisibleBottom();
        var children = Children;
        for (var i = 0; i < children.Count; i++)
        {
            if (children[i] is StretchRefreshRowHost host)
            {
                host.SetSpread(host.Bounds.Y <= limit ? tension : 0);
            }
        }
    }

    /// <summary>The list coordinate of the feed's visible bottom (everything when the list is not in a scroller).</summary>
    private double VisibleBottom()
    {
        if (Parent is ScrollContentPresenter { } presenter)
        {
            return presenter.Offset.Y + presenter.Viewport.Height - Margin.Top;
        }
        if (this.FindAncestorOfType<ScrollViewer>() is { } scroller)
        {
            return scroller.Offset.Y + scroller.Viewport.Height - Margin.Top;
        }
        return double.PositiveInfinity;
    }

    /// <summary>A batch of changes (several inserts in a row) is applied as one change, before the next layout.</summary>
    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_syncPosted)
        {
            return;
        }
        _syncPosted = true;
        Dispatcher.UIThread.Post(_postedSync ??= RunPostedSync);
    }

    private void RunPostedSync()
    {
        if (_syncPosted)
        {
            Sync(animate: true);
        }
    }

    private object KeyOf(object? item)
    {
        if (item is null)
        {
            return NullKey.Instance;
        }
        if (KeySelector is { } selector)
        {
            return selector(item) ?? NullKey.Instance;
        }
        return item is StretchRefreshActivity { Id: { Length: > 0 } id } ? id : item;
    }

    private void Sync(bool animate)
    {
        _syncPosted = false;
        animate &= this.IsAttachedToVisualTree() && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off && Bounds.Height > 0;
        var full = ArcMotion.IsFull(this);
        var tops = _tops;
        var next = _next;
        var seen = _seen;
        var entering = _entering;
        tops.Clear();
        next.Clear();
        seen.Clear();
        entering.Clear();
        if (animate)
        {
            foreach (var host in _byKey.Values)
            {
                tops[host.Key] = host.DrawnTop;
            }
        }
        if (_items is not null)
        {
            foreach (var item in _items)
            {
                var key = KeyOf(item);
                if (!seen.Add(key))
                {
                    continue;
                }
                if (_byKey.TryGetValue(key, out var host))
                {
                    if (!ReferenceEquals(host.Item, item))
                    {
                        host.Item = item;
                        host.Child = Build(item);
                    }
                }
                else
                {
                    host = new StretchRefreshRowHost(key, item) { Child = Build(item) };
                    _byKey[key] = host;
                    entering.Add(host);
                }
                next.Add(host);
            }
        }
        _gone.Clear();
        foreach (var key in _byKey.Keys)
        {
            if (!seen.Contains(key))
            {
                _gone.Add(key);
            }
        }
        foreach (var key in _gone)
        {
            _byKey[key].DisposeMotion();
            _byKey.Remove(key);
        }
        ApplyOrder(next);
        for (var i = 0; i < next.Count; i++)
        {
            next[i].Index = i;
        }
        SetSpread(_tension);
        _pendingSlides.Clear();
        if (animate)
        {
            foreach (var host in next)
            {
                if (tops.TryGetValue(host.Key, out var top))
                {
                    _pendingSlides.Add((host, top));
                }
            }
            for (var i = 0; i < entering.Count; i++)
            {
                entering[i].Enter(EnterDelay + EnterStagger * i, full);
            }
        }
        _gone.Clear();
        tops.Clear();
        InvalidateMeasure();
        RowsChanged?.Invoke(animate ? entering.Count : 0);
    }

    /// <summary>
    /// Brings <see cref="Panel.Children"/> to <paramref name="next"/> with the fewest changes: rows that left are
    /// removed, new rows inserted and reordered rows moved, so rows that stay are never detached (their styles,
    /// templates and subscriptions survive a refresh).
    /// </summary>
    private void ApplyOrder(List<StretchRefreshRowHost> next)
    {
        var children = Children;
        for (var i = children.Count - 1; i >= 0; i--)
        {
            if (children[i] is not StretchRefreshRowHost host || !_seen.Contains(host.Key) || !ReferenceEquals(_byKey.GetValueOrDefault(host.Key), host))
            {
                children.RemoveAt(i);
            }
        }
        for (var i = 0; i < next.Count; i++)
        {
            var want = next[i];
            if (i < children.Count && ReferenceEquals(children[i], want))
            {
                continue;
            }
            var at = -1;
            for (var j = i + 1; j < children.Count; j++)
            {
                if (ReferenceEquals(children[j], want))
                {
                    at = j;
                    break;
                }
            }
            if (at >= 0)
            {
                children.Move(at, i);
            }
            else
            {
                children.Insert(i, want);
            }
        }
        while (children.Count > next.Count)
        {
            children.RemoveAt(children.Count - 1);
        }
    }

    private Control Build(object? item)
    {
        if (ItemTemplate is { } template && item is not null && template.Match(item))
        {
            var built = template.Build(item) ?? new TextBlock();
            built.DataContext = item;
            return built;
        }
        if (item is StretchRefreshActivity activity)
        {
            return new StretchRefreshActivityRow { Activity = activity };
        }
        return new TextBlock { Text = item?.ToString(), TextWrapping = TextWrapping.Wrap };
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = 0.0;
        var height = 0.0;
        var child = new Size(availableSize.Width, double.PositiveInfinity);
        foreach (var c in Children)
        {
            c.Measure(child);
            width = Math.Max(width, c.DesiredSize.Width);
            height += c.DesiredSize.Height;
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var y = 0.0;
        foreach (var c in Children)
        {
            var h = c.DesiredSize.Height;
            c.Arrange(new Rect(0, y, finalSize.Width, h));
            y += h;
        }
        // FLIP: every row that stayed starts where it was drawn and springs into its new slot.
        if (_pendingSlides.Count > 0)
        {
            foreach (var (host, top) in _pendingSlides)
            {
                host.Slide(top - host.Bounds.Y);
            }
            _pendingSlides.Clear();
        }
        return finalSize;
    }

    private sealed class NullKey
    {
        public static readonly NullKey Instance = new();
    }
}
