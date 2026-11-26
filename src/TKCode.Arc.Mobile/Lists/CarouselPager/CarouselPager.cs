using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gestures;
using TKCode.Arc.Mobile.DesignTokens;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Mobile;

/// <summary>Event data of <see cref="CarouselPager.SelectionChanged"/>.</summary>
public sealed class CarouselPageChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the event data.</summary>
    public CarouselPageChangedEventArgs(RoutedEvent routedEvent, int oldIndex, int newIndex)
        : base(routedEvent)
    {
        OldIndex = oldIndex;
        NewIndex = newIndex;
    }

    /// <summary>The previous page index.</summary>
    public int OldIndex { get; }

    /// <summary>The newly selected page index.</summary>
    public int NewIndex { get; }
}

/// <summary>
/// Mobile horizontal touch paging carousel: displays full-width pages (onboarding slides, product photos or feature
/// cards) with 1:1 horizontal finger tracking, rubber-band resistance at the ends, velocity flick snapping, tappable
/// page indicator dots, optional auto-play (paused while dragging) and selection haptics. Yields the leading 24 px
/// screen edge on page 0 so <see cref="NavigationStack"/> swipe-back works without conflict, and rejects vertical drags
/// so scrolling a parent <see cref="Page"/> stays fluid.
/// </summary>
[TemplatePart("PART_Viewport", typeof(Border))]
[TemplatePart("PART_Track", typeof(Canvas))]
[TemplatePart("PART_IndicatorsHost", typeof(Control))]
[TemplatePart("PART_Indicators", typeof(StackPanel))]
public class CarouselPager : TemplatedControl
{
    private const double BackSwipeEdge = 24;
    private const double FlickVelocity = 400;
    private const double Stretch = 64;
    private static readonly TimeSpan Projection = TimeSpan.FromSeconds(0.16);

    /// <summary>Optional external items sequence rendered as pages (overrides inline <see cref="Items"/> when set).</summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<CarouselPager, IEnumerable?>(nameof(ItemsSource));

    /// <summary>Optional template used to render non-control page items.</summary>
    public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty =
        AvaloniaProperty.Register<CarouselPager, IDataTemplate?>(nameof(ItemTemplate));

    /// <summary>Zero-based index of the current page (two-way, clamped to available pages).</summary>
    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<CarouselPager, int>(nameof(SelectedIndex), 0, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>Whether to show the tappable dot indicator strip below the pages: true.</summary>
    public static readonly StyledProperty<bool> ShowIndicatorsProperty =
        AvaloniaProperty.Register<CarouselPager, bool>(nameof(ShowIndicators), true);

    /// <summary>Whether <see cref="Next"/>, <see cref="Previous"/> and auto-play wrap around at the ends: false.</summary>
    public static readonly StyledProperty<bool> LoopProperty =
        AvaloniaProperty.Register<CarouselPager, bool>(nameof(Loop));

    /// <summary>Optional interval between automatic page advances (<see cref="TimeSpan.Zero"/> disables auto-play): 0.</summary>
    public static readonly StyledProperty<TimeSpan> AutoPlayIntervalProperty =
        AvaloniaProperty.Register<CarouselPager, TimeSpan>(nameof(AutoPlayInterval), TimeSpan.Zero);

    /// <summary>Total number of pages currently in the carousel.</summary>
    public static readonly DirectProperty<CarouselPager, int> PageCountProperty =
        AvaloniaProperty.RegisterDirect<CarouselPager, int>(nameof(PageCount), p => p.PageCount);

    /// <summary>Raised whenever <see cref="SelectedIndex"/> changes.</summary>
    public static readonly RoutedEvent<CarouselPageChangedEventArgs> SelectionChangedEvent =
        RoutedEvent.Register<CarouselPager, CarouselPageChangedEventArgs>(nameof(SelectionChanged), RoutingStrategies.Bubble);

    private readonly AvaloniaList<object?> _items = new();
    private readonly List<object?> _materialized = [];
    private readonly List<Control> _pageHosts = [];
    private readonly List<Border> _indicatorDots = [];
    private readonly TranslateTransform _translate = new();
    private Border? _viewport;
    private Canvas? _track;
    private Control? _indicatorsHost;
    private StackPanel? _indicators;
    private MotionValue<double>? _offset;
    private DragTracker? _drag;
    private DispatcherTimer? _autoPlayTimer;
    private INotifyCollectionChanged? _subscribedSource;
    private int _pageCount;
    private bool _dragging;
    private double _dragOrigin;

    /// <summary>Creates a mobile carousel pager.</summary>
    public CarouselPager()
    {
        _items.CollectionChanged += OnCollectionChanged;
        ActualThemeVariantChanged += (_, _) => UpdateIndicatorsVisual();
    }

    /// <summary>Inline page items placed inside the carousel in XAML or code.</summary>
    [Content]
    public AvaloniaList<object?> Items => _items;

    /// <inheritdoc cref="ItemsSourceProperty"/>
    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <inheritdoc cref="ItemTemplateProperty"/>
    public IDataTemplate? ItemTemplate
    {
        get => GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    /// <inheritdoc cref="SelectedIndexProperty"/>
    public int SelectedIndex
    {
        get => GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    /// <inheritdoc cref="ShowIndicatorsProperty"/>
    public bool ShowIndicators
    {
        get => GetValue(ShowIndicatorsProperty);
        set => SetValue(ShowIndicatorsProperty, value);
    }

    /// <inheritdoc cref="LoopProperty"/>
    public bool Loop
    {
        get => GetValue(LoopProperty);
        set => SetValue(LoopProperty, value);
    }

    /// <inheritdoc cref="AutoPlayIntervalProperty"/>
    public TimeSpan AutoPlayInterval
    {
        get => GetValue(AutoPlayIntervalProperty);
        set => SetValue(AutoPlayIntervalProperty, value);
    }

    /// <inheritdoc cref="PageCountProperty"/>
    public int PageCount => _pageCount;

    /// <inheritdoc cref="SelectionChangedEvent"/>
    public event EventHandler<CarouselPageChangedEventArgs>? SelectionChanged
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    /// <summary>Current horizontal translation of the page track in px. For tests and diagnostics.</summary>
    public double TrackOffset => _translate.X;

    protected override Type StyleKeyOverride => typeof(CarouselPager);

    private bool Reduced => ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Full;

    private double PageWidth => _viewport is not null && _viewport.Bounds.Width > 0
        ? _viewport.Bounds.Width
        : (Bounds.Width > 0 ? Bounds.Width : 360);

    private double PageHeight => _viewport is not null && _viewport.Bounds.Height > 0
        ? _viewport.Bounds.Height
        : (Bounds.Height > 0 ? Math.Max(0, Bounds.Height - (ShowIndicators ? 32 : 0)) : 200);

    /// <summary>Advances to the next page (wraps to 0 when <see cref="Loop"/> is true).</summary>
    public void Next()
    {
        if (_pageCount <= 1)
        {
            return;
        }
        var target = SelectedIndex + 1;
        if (target >= _pageCount)
        {
            if (!Loop)
            {
                return;
            }
            target = 0;
        }
        GoTo(target);
    }

    /// <summary>Moves to the previous page (wraps to the last page when <see cref="Loop"/> is true).</summary>
    public void Previous()
    {
        if (_pageCount <= 1)
        {
            return;
        }
        var target = SelectedIndex - 1;
        if (target < 0)
        {
            if (!Loop)
            {
                return;
            }
            target = _pageCount - 1;
        }
        GoTo(target);
    }

    /// <summary>Animates (or jumps under reduced motion) to the clamped page index.</summary>
    public void GoTo(int index)
    {
        if (_pageCount == 0)
        {
            return;
        }
        var clamped = Math.Clamp(index, 0, _pageCount - 1);
        if (clamped != SelectedIndex)
        {
            SetCurrentValue(SelectedIndexProperty, clamped);
        }
        else
        {
            SettleTo(clamped, velocityX: null);
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Detach();
        _viewport = e.NameScope.Find<Border>("PART_Viewport");
        _track = e.NameScope.Find<Canvas>("PART_Track");
        _indicatorsHost = e.NameScope.Find<Control>("PART_IndicatorsHost");
        _indicators = e.NameScope.Find<StackPanel>("PART_Indicators");

        if (_viewport is not null)
        {
            _viewport.PropertyChanged += OnViewportPropertyChanged;
            _drag = new DragTracker(_viewport, new DragOptions { Axis = DragAxis.Horizontal, Threshold = 6, RejectCrossAxis = true })
            {
                CanBegin = args => IsEffectivelyEnabled && _pageCount > 1 && !(SelectedIndex == 0 && args.GetPosition(_viewport).X <= BackSwipeEdge),
            };
            _drag.Started += (_, _) => BeginDrag();
            _drag.Moved += (_, args) => MoveDrag(args.Offset.X);
            _drag.Completed += (_, args) => EndDrag(args.Velocity.X);
            _drag.Cancelled += (_, _) => EndDrag(0);
        }
        if (_track is not null)
        {
            _track.RenderTransform = _translate;
            _offset = new MotionValue<double>(_track, 0, MotionVectors.Double);
            _offset.Changed += _ => ApplyOffset();
        }

        RebuildPages();
        JumpToCurrent();
        UpdateAutoPlay();
    }

    private void Detach()
    {
        StopAutoPlay();
        if (_viewport is not null)
        {
            _viewport.PropertyChanged -= OnViewportPropertyChanged;
        }
        _drag?.Dispose();
        _drag = null;
        _offset?.Dispose();
        _offset = null;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateAutoPlay();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        StopAutoPlay();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty)
        {
            if (_subscribedSource is not null)
            {
                _subscribedSource.CollectionChanged -= OnCollectionChanged;
                _subscribedSource = null;
            }
            if (ItemsSource is INotifyCollectionChanged ncc)
            {
                _subscribedSource = ncc;
                _subscribedSource.CollectionChanged += OnCollectionChanged;
            }
            RebuildPages();
            JumpToCurrent();
        }
        else if (change.Property == ItemTemplateProperty)
        {
            RebuildPages();
            JumpToCurrent();
        }
        else if (change.Property == SelectedIndexProperty)
        {
            var oldIdx = change.GetOldValue<int>();
            var newIdx = change.GetNewValue<int>();
            var clamped = _pageCount > 0 ? Math.Clamp(newIdx, 0, _pageCount - 1) : 0;
            if (clamped != newIdx)
            {
                SetCurrentValue(SelectedIndexProperty, clamped);
                return;
            }
            UpdateIndicatorsVisual();
            SettleTo(clamped, velocityX: null);
            if (oldIdx != clamped)
            {
                Haptics.Trigger(HapticKind.Selection);
                RaiseEvent(new CarouselPageChangedEventArgs(SelectionChangedEvent, oldIdx, clamped));
            }
        }
        else if (change.Property == ShowIndicatorsProperty)
        {
            UpdateIndicatorsHostVisibility();
        }
        else if (change.Property == AutoPlayIntervalProperty || change.Property == LoopProperty)
        {
            UpdateAutoPlay();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !IsEffectivelyEnabled || _pageCount <= 1)
        {
            return;
        }
        switch (e.Key)
        {
            case Key.Right:
                Next();
                e.Handled = true;
                break;
            case Key.Left:
                Previous();
                e.Handled = true;
                break;
            case Key.Home:
                GoTo(0);
                e.Handled = true;
                break;
            case Key.End:
                GoTo(_pageCount - 1);
                e.Handled = true;
                break;
        }
    }

    private void OnViewportPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == BoundsProperty)
        {
            LayoutPages();
            if (!_dragging)
            {
                JumpToCurrent();
            }
        }
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildPages();
        JumpToCurrent();
    }

    private void RebuildPages()
    {
        _materialized.Clear();
        IEnumerable source = ItemsSource ?? _items;
        foreach (var item in source)
        {
            _materialized.Add(item);
        }

        SetAndRaise(PageCountProperty, ref _pageCount, _materialized.Count);

        if (_track is not null)
        {
            _track.Children.Clear();
            _pageHosts.Clear();
            for (var i = 0; i < _materialized.Count; i++)
            {
                var item = _materialized[i];
                Control host;
                if (item is Control ctrl)
                {
                    host = ctrl;
                }
                else
                {
                    host = new ContentPresenter
                    {
                        Content = item,
                        ContentTemplate = ItemTemplate,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Stretch,
                    };
                }
                _pageHosts.Add(host);
                _track.Children.Add(host);
            }
            LayoutPages();
        }

        RebuildIndicators();
        UpdateIndicatorsHostVisibility();

        if (_pageCount > 0 && SelectedIndex >= _pageCount)
        {
            SetCurrentValue(SelectedIndexProperty, _pageCount - 1);
        }
        else if (_pageCount == 0 && SelectedIndex != 0)
        {
            SetCurrentValue(SelectedIndexProperty, 0);
        }
        UpdateIndicatorsVisual();
    }

    private void LayoutPages()
    {
        if (_track is null)
        {
            return;
        }
        var w = PageWidth;
        var h = PageHeight;
        _track.Width = w * Math.Max(1, _pageHosts.Count);
        _track.Height = h;
        for (var i = 0; i < _pageHosts.Count; i++)
        {
            var host = _pageHosts[i];
            host.Width = w;
            host.Height = h;
            Canvas.SetLeft(host, i * w);
            Canvas.SetTop(host, 0);
        }
    }

    private void RebuildIndicators()
    {
        if (_indicators is null)
        {
            return;
        }
        _indicators.Children.Clear();
        _indicatorDots.Clear();

        var size = ResolveToken(CarouselPagerTokenKind.IndicatorSize, 7);
        for (var i = 0; i < _pageCount; i++)
        {
            var index = i;
            var dot = new Border
            {
                Width = size,
                Height = size,
                CornerRadius = new CornerRadius(size / 2.0),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            var tapBox = new Border
            {
                MinWidth = 20,
                MinHeight = 32,
                Background = Brushes.Transparent,
                Cursor = new Cursor(StandardCursorType.Hand),
                Child = dot,
            };
            tapBox.PointerPressed += (_, e) =>
            {
                if (IsEffectivelyEnabled)
                {
                    e.Handled = true;
                    GoTo(index);
                }
            };
            _indicatorDots.Add(dot);
            _indicators.Children.Add(tapBox);
        }
    }

    private void UpdateIndicatorsHostVisibility()
    {
        if (_indicatorsHost is not null)
        {
            _indicatorsHost.IsVisible = ShowIndicators && _pageCount > 1;
        }
    }

    private void UpdateIndicatorsVisual()
    {
        if (_indicatorDots.Count == 0)
        {
            return;
        }
        var dotSize = ResolveToken(CarouselPagerTokenKind.IndicatorSize, 7);
        var activeWidth = ResolveToken(CarouselPagerTokenKind.ActiveIndicatorWidth, 18);
        var activeBrush = ResolveDesignBrush(ArcDesignTokenKind.Accent);
        var inactiveBrush = ResolveDesignBrush(ArcDesignTokenKind.BorderStrong);

        for (var i = 0; i < _indicatorDots.Count; i++)
        {
            var active = i == SelectedIndex;
            var dot = _indicatorDots[i];
            dot.Width = active ? activeWidth : dotSize;
            dot.Height = dotSize;
            dot.Background = active ? activeBrush : inactiveBrush;
        }
    }

    private void BeginDrag()
    {
        if (_offset is null)
        {
            return;
        }
        _dragging = true;
        StopAutoPlay();
        _dragOrigin = _offset.Value;
        _offset.Stop();
    }

    private void MoveDrag(double deltaX)
    {
        if (!_dragging || _offset is null || _pageCount <= 0)
        {
            return;
        }
        var minOffset = -((_pageCount - 1) * PageWidth);
        var raw = _dragOrigin + deltaX;
        if (raw > 0)
        {
            _offset.Jump(RubberBand.Apply(raw, Stretch));
        }
        else if (raw < minOffset)
        {
            _offset.Jump(minOffset - RubberBand.Apply(minOffset - raw, Stretch));
        }
        else
        {
            _offset.Jump(raw);
        }
    }

    private void EndDrag(double velocityX)
    {
        if (!_dragging || _offset is null)
        {
            return;
        }
        _dragging = false;
        var w = PageWidth;
        var current = SelectedIndex;
        var target = current;
        if (w > 1 && _pageCount > 1)
        {
            if (velocityX <= -FlickVelocity)
            {
                target = Math.Min(_pageCount - 1, current + 1);
            }
            else if (velocityX >= FlickVelocity)
            {
                target = Math.Max(0, current - 1);
            }
            else
            {
                var projected = _offset.Value + velocityX * Projection.TotalSeconds;
                var deltaPages = -(projected + current * w) / w;
                if (deltaPages >= 0.35)
                {
                    target = Math.Min(_pageCount - 1, current + 1);
                }
                else if (deltaPages <= -0.35)
                {
                    target = Math.Max(0, current - 1);
                }
            }
        }
        if (target != current)
        {
            SetCurrentValue(SelectedIndexProperty, target);
        }
        else
        {
            SettleTo(target, velocityX);
        }
        UpdateAutoPlay();
    }

    private void SettleTo(int index, double? velocityX)
    {
        if (_offset is null)
        {
            return;
        }
        var targetX = -index * PageWidth;
        if (Reduced)
        {
            _offset.Jump(targetX);
            ApplyOffset();
            return;
        }
        _ = _offset.AnimateTo(targetX, MotionSpec.Spring(ArcSpring.Smooth), velocityX);
    }

    private void JumpToCurrent()
    {
        if (_offset is null)
        {
            return;
        }
        _offset.Jump(-SelectedIndex * PageWidth);
        ApplyOffset();
    }

    private void ApplyOffset()
    {
        _translate.X = _offset?.Value ?? (-SelectedIndex * PageWidth);
    }

    private void UpdateAutoPlay()
    {
        StopAutoPlay();
        var interval = AutoPlayInterval;
        if (interval <= TimeSpan.Zero || _pageCount <= 1 || _dragging)
        {
            return;
        }
        _autoPlayTimer = new DispatcherTimer { Interval = interval };
        _autoPlayTimer.Tick += OnAutoPlayTick;
        _autoPlayTimer.Start();
    }

    private void StopAutoPlay()
    {
        if (_autoPlayTimer is not null)
        {
            _autoPlayTimer.Stop();
            _autoPlayTimer.Tick -= OnAutoPlayTick;
            _autoPlayTimer = null;
        }
    }

    private void OnAutoPlayTick(object? sender, EventArgs e)
    {
        if (!IsEffectivelyEnabled || _dragging || _pageCount <= 1)
        {
            return;
        }
        if (SelectedIndex < _pageCount - 1)
        {
            GoTo(SelectedIndex + 1);
        }
        else if (Loop)
        {
            GoTo(0);
        }
        else
        {
            StopAutoPlay();
        }
    }

    private double ResolveToken(CarouselPagerTokenKind kind, double fallback)
        => this.TryFindResource(CarouselPagerTokens.Key(kind), out var v) && v is double d && d > 0 ? d : fallback;

    private IBrush? ResolveDesignBrush(ArcDesignTokenKind kind)
    {
        if (this.TryFindResource(ArcDesignTokens.Key(kind), out var v))
        {
            if (v is IBrush brush)
            {
                return brush;
            }
            if (v is Color color)
            {
                return new SolidColorBrush(color);
            }
        }
        return null;
    }
}
