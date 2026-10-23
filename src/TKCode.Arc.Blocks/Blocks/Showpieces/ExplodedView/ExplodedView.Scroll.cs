using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

public partial class ExplodedView
{
    /// <summary>Thickness of the frame's border (the scroll region sits inside it).</summary>
    private const double FrameBorder = 1;

    private bool _syncingProgress;
    private bool _settingOffset;
    private ScrollViewer? _pageScroll;
    private Rect _pageViewport;
    private MotionValue<double>? _scrollMotion;
    private ScrollViewer? _scrollMotionTarget;

    /// <summary>The ancestor scroll viewer followed in page mode.</summary>
    internal ScrollViewer? PageScrollViewer => _pageScroll;

    internal bool IsScrollAnimating => _scrollMotion?.IsAnimating == true;

    /// <summary>
    /// Scrolls to a stop: 0 the whole screen, 1..N the middle of a layer's share, N + 1 the end. Animated (an in-out glide whose
    /// length grows with the distance, at most 700 ms) unless <paramref name="animate"/> is false or motion is reduced; the
    /// scene follows on its spring either way. In external mode it sets <see cref="Progress"/>.
    /// </summary>
    public void GoToStop(int stop, bool animate = true)
    {
        var count = Layers.Count;
        stop = Math.Clamp(stop, 0, count + 1);
        var target = ExplodedViewProgress.StopProgress(stop, count);
        animate &= _level == ArcMotionLevel.Full && IsAttachedToVisualTree();
        switch (ScrollMode)
        {
            case ExplodedScrollMode.Container when _scroll is not null && ScrollableHeight(_scroll) > 0:
                ScrollTo(_scroll, target * ScrollableHeight(_scroll), animate);
                break;
            case ExplodedScrollMode.Page when _pageScroll is not null && PageScrollable() > 0:
                ScrollTo(_pageScroll, _pageScroll.Offset.Y + target * PageScrollable() - _pageViewport.Y, animate);
                break;
            default:
                StopScrollMotion();
                SetCurrentValue(ProgressProperty, target);
                if (!animate)
                {
                    JumpFollow();
                }
                break;
        }
    }

    /// <summary>Goes back to the whole screen (stop 0), at once unless <paramref name="animate"/> is true.</summary>
    public void Reset(bool animate = false) => GoToStop(0, animate);

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : AutoWidth;
        var height = ResolveHeight(availableSize.Height);
        ApplyMetrics(width);
        ConfigureTrack(height);
        base.MeasureOverride(new Size(width, height));
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        if (ScrollMode == ExplodedScrollMode.Page)
        {
            ReadPage();
        }
        return size;
    }

    private double FrameInset => IsFramed ? 2 * FrameBorder : 0;

    private double ResolveHeight(double available)
    {
        if (ScrollMode == ExplodedScrollMode.Page)
        {
            return Length * PageViewportHeight();
        }
        if (double.IsFinite(available))
        {
            return available;
        }
        var window = TopLevel.GetTopLevel(this)?.ClientSize.Height ?? 0;
        return window > 0 ? Math.Clamp(AutoHeightRatio * window, AutoHeightMin, AutoHeightMax) : AutoHeightMax;
    }

    private double PageViewportHeight()
    {
        if (_pageViewport.Height > 0)
        {
            return _pageViewport.Height;
        }
        if (_pageScroll is { Viewport.Height: > 0 } page)
        {
            return page.Viewport.Height;
        }
        var window = TopLevel.GetTopLevel(this)?.ClientSize.Height ?? 0;
        return window > 0 ? window : AutoHeightMax;
    }

    private void ConfigureTrack(double height)
    {
        if (_track is null)
        {
            return;
        }
        var inner = Math.Max(0, height - FrameInset);
        switch (ScrollMode)
        {
            case ExplodedScrollMode.Container:
                _track.StickyHeight = inner;
                _track.TrackHeight = inner * Length;
                break;
            case ExplodedScrollMode.Page:
                _track.StickyHeight = Math.Min(inner, Math.Max(0, PageViewportHeight() - FrameInset));
                _track.TrackHeight = inner;
                break;
            default:
                _track.StickyHeight = inner;
                _track.TrackHeight = inner;
                _track.SetStickyOffset(0);
                break;
        }
    }

    private static double ScrollableHeight(ScrollViewer viewer) => Math.Max(0, viewer.Extent.Height - viewer.Viewport.Height);

    private double PageScrollable() => Math.Max(0, (_track?.TrackHeight ?? 0) - (_track?.StickyHeight ?? 0));

    // Attach -------------------------------------------------------------------------------------------------------------

    private void AttachScroll()
    {
        if (_scroll is not null)
        {
            var container = ScrollMode == ExplodedScrollMode.Container;
            _scroll.VerticalScrollBarVisibility = container ? ScrollBarVisibility.Hidden : ScrollBarVisibility.Disabled;
            _scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            _scroll.Focusable = container;
            _scroll.IsTabStop = container;
            _scroll.BringIntoViewOnFocusChange = false;
            ScrollViewer.SetIsScrollChainingEnabled(_scroll, false);
            _scroll.PropertyChanged += OnScrollPropertyChanged;
            _scroll.FocusRingChanged += OnRegionFocusChanged;
            if (!container && _scroll.Offset != default)
            {
                _scroll.Offset = default;
            }
        }
        AttachPageScroll();
        InvalidateMeasure();
    }

    private void DetachScroll()
    {
        StopScrollMotion();
        if (_scroll is not null)
        {
            _scroll.PropertyChanged -= OnScrollPropertyChanged;
            _scroll.FocusRingChanged -= OnRegionFocusChanged;
        }
        DetachPageScroll();
        PseudoClasses.Set(RegionFocusPseudoClass, false);
    }

    private void AttachPageScroll()
    {
        DetachPageScroll();
        if (ScrollMode != ExplodedScrollMode.Page)
        {
            return;
        }
        _pageScroll = this.FindAncestorOfType<ScrollViewer>();
        if (_pageScroll is not null)
        {
            _pageScroll.PropertyChanged += OnPageScrollPropertyChanged;
        }
    }

    private void DetachPageScroll()
    {
        if (_pageScroll is not null)
        {
            _pageScroll.PropertyChanged -= OnPageScrollPropertyChanged;
            _pageScroll = null;
        }
    }

    private void OnRegionFocusChanged(bool visible) => PseudoClasses.Set(RegionFocusPseudoClass, visible);

    // Container mode -----------------------------------------------------------------------------------------------------

    private void OnScrollPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (ScrollMode != ExplodedScrollMode.Container)
        {
            return;
        }
        if (e.Property == ScrollViewer.OffsetProperty)
        {
            if (!_settingOffset && ReferenceEquals(_scrollMotionTarget, _scroll))
            {
                // A wheel, touch or key scroll takes over from a stop button's glide.
                StopScrollMotion();
            }
            ReadScroll();
        }
        else if (e.Property == ScrollViewer.ExtentProperty || e.Property == ScrollViewer.ViewportProperty)
        {
            // A resized frame keeps its progress (the offset is rescaled to the new track).
            WriteScroll();
        }
    }

    private void ReadScroll()
    {
        if (_scroll is null)
        {
            return;
        }
        var y = _scroll.Offset.Y;
        _track?.SetStickyOffset(y);
        var scrollable = ScrollableHeight(_scroll);
        if (scrollable > 0 && !_settingOffsetFromProgress)
        {
            SetProgressFromScroll(y / scrollable);
        }
    }

    private bool _settingOffsetFromProgress;

    private void WriteScroll()
    {
        if (_scroll is null || ScrollMode != ExplodedScrollMode.Container)
        {
            return;
        }
        var scrollable = ScrollableHeight(_scroll);
        if (scrollable <= 0)
        {
            _track?.SetStickyOffset(0);
            return;
        }
        var y = Progress * scrollable;
        if (Math.Abs(_scroll.Offset.Y - y) > 0.25)
        {
            _settingOffset = true;
            _settingOffsetFromProgress = true;
            try
            {
                _scroll.Offset = new Vector(0, y);
            }
            finally
            {
                _settingOffset = false;
                _settingOffsetFromProgress = false;
            }
        }
        _track?.SetStickyOffset(_scroll.Offset.Y);
    }

    private void SetProgressFromScroll(double progress)
    {
        _syncingProgress = true;
        try
        {
            SetCurrentValue(ProgressProperty, Math.Clamp(progress, 0, 1));
        }
        finally
        {
            _syncingProgress = false;
        }
    }

    private void OnProgressChanged()
    {
        if (!_syncingProgress)
        {
            switch (ScrollMode)
            {
                case ExplodedScrollMode.Container:
                    StopScrollMotion();
                    WriteScroll();
                    break;
                case ExplodedScrollMode.Page when _pageScroll is not null && PageScrollable() > 0:
                    StopScrollMotion();
                    SetOffset(_pageScroll, _pageScroll.Offset.Y + Progress * PageScrollable() - _pageViewport.Y);
                    break;
            }
        }
        FollowProgress();
    }

    // Page mode ----------------------------------------------------------------------------------------------------------

    private void OnEffectiveViewportChanged(object? sender, EffectiveViewportChangedEventArgs e)
    {
        var previousHeight = _pageViewport.Height;
        _pageViewport = e.EffectiveViewport;
        if (ScrollMode != ExplodedScrollMode.Page)
        {
            return;
        }
        if (!previousHeight.Equals(_pageViewport.Height))
        {
            InvalidateMeasure();
        }
        ReadPage();
    }

    private void OnPageScrollPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ScrollViewer.OffsetProperty && !_settingOffset && ReferenceEquals(_scrollMotionTarget, _pageScroll))
        {
            StopScrollMotion();
        }
    }

    private void ReadPage()
    {
        if (_track is null || _pageViewport.Height <= 0)
        {
            return;
        }
        var top = _pageViewport.Y - (IsFramed ? FrameBorder : 0);
        _track.SetStickyOffset(top);
        var scrollable = PageScrollable();
        if (scrollable > 0)
        {
            SetProgressFromScroll(top / scrollable);
        }
    }

    // Glides -------------------------------------------------------------------------------------------------------------

    /// <summary>Duration of a stop button's glide: grows with the distance like a browser's smooth scroll (0 → 1658 px ≈ 660 ms).</summary>
    internal static TimeSpan GlideDuration(double distance) => TimeSpan.FromMilliseconds(Math.Clamp(200 + 0.28 * distance, 250, 700));

    private void ScrollTo(ScrollViewer viewer, double y, bool animate)
    {
        StopScrollMotion();
        var max = ScrollableHeight(viewer);
        y = Math.Clamp(y, 0, max);
        var from = viewer.Offset.Y;
        if (!animate || Math.Abs(y - from) < 0.5)
        {
            SetOffset(viewer, y);
            return;
        }
        // Code-driven: the glide writes a scroll offset (not a styled property of this control), and wheel input must be
        // able to cancel it mid-way, so a ControlTheme transition cannot express it.
        _scrollMotion ??= CreateScrollMotion();
        _scrollMotionTarget = null;
        _scrollMotion.Jump(from);
        _scrollMotionTarget = viewer;
        _ = _scrollMotion.AnimateTo(y, MotionSpec.Tween(GlideDuration(Math.Abs(y - from)), ArcEasings.InOut));
    }

    private MotionValue<double> CreateScrollMotion()
    {
        var motion = new MotionValue<double>(this, 0, MotionVectors.Double);
        motion.Changed += value =>
        {
            if (_scrollMotionTarget is { } viewer)
            {
                SetOffset(viewer, value);
            }
        };
        return motion;
    }

    private void StopScrollMotion()
    {
        _scrollMotionTarget = null;
        _scrollMotion?.Stop();
    }

    private void SetOffset(ScrollViewer viewer, double y)
    {
        _settingOffset = true;
        try
        {
            viewer.Offset = new Vector(viewer.Offset.X, Math.Max(0, y));
        }
        finally
        {
            _settingOffset = false;
        }
    }
}
