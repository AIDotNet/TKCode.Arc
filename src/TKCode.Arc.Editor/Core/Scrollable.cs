// Ported from vscode (src/vs/base/common/scrollable.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
// - INewScrollDimensions and INewScrollPosition, objects with optional members, are the structs NewScrollDimensions and
//   NewScrollPosition with nullable members (null is upstream's undefined). IScrollableOptions is ScrollableOptions.
// - ScrollEvent is a record with init-only members, so a consumer can derive a changed copy with `with`.
// - onScroll is the .NET event Scroll, over Core/Emitter so that it keeps upstream's delivery order.
// - The smooth scrolling animation runs on the host's frames: ScrollableOptions.ScheduleAtNextAnimationFrame is called
//   with the callback to run on the next frame and returns what cancels it, as upstream; there is no timer here.
// - Date.now() is a monotonic millisecond clock (Stopwatch); only differences between its readings are used.
// - forceIntegerValues truncates as JavaScript's `x | 0` does.

using System.Diagnostics;

namespace TKCode.Arc.Editor;

internal enum ScrollbarVisibility
{
    Auto = 1,
    Hidden = 2,
    Visible = 3,
}

/// <summary>What a scroll changed: the dimensions and positions before and after.</summary>
internal sealed record ScrollEvent
{
    public bool InSmoothScrolling { get; init; }

    public double OldWidth { get; init; }
    public double OldScrollWidth { get; init; }
    public double OldScrollLeft { get; init; }

    public double Width { get; init; }
    public double ScrollWidth { get; init; }
    public double ScrollLeft { get; init; }

    public double OldHeight { get; init; }
    public double OldScrollHeight { get; init; }
    public double OldScrollTop { get; init; }

    public double Height { get; init; }
    public double ScrollHeight { get; init; }
    public double ScrollTop { get; init; }

    public bool WidthChanged { get; init; }
    public bool ScrollWidthChanged { get; init; }
    public bool ScrollLeftChanged { get; init; }

    public bool HeightChanged { get; init; }
    public bool ScrollHeightChanged { get; init; }
    public bool ScrollTopChanged { get; init; }
}

internal interface IScrollDimensions
{
    double Width { get; }
    double ScrollWidth { get; }
    double Height { get; }
    double ScrollHeight { get; }
}

/// <summary>Dimensions to change; a null member keeps its value (upstream's INewScrollDimensions).</summary>
internal readonly record struct NewScrollDimensions(double? Width = null, double? ScrollWidth = null, double? Height = null, double? ScrollHeight = null);

internal interface IScrollPosition
{
    double ScrollLeft { get; }
    double ScrollTop { get; }
}

/// <summary>A scroll position with the viewport size; upstream repeats ScrollLeft and ScrollTop instead of extending.</summary>
internal interface ISmoothScrollPosition : IScrollPosition
{
    double Width { get; }
    double Height { get; }
}

/// <summary>A position to scroll to; a null member keeps its value (upstream's INewScrollPosition).</summary>
internal readonly record struct NewScrollPosition(double? ScrollLeft = null, double? ScrollTop = null);

/// <summary>The options of a <see cref="Scrollable"/> (upstream's IScrollableOptions).</summary>
/// <param name="forceIntegerValues">Define if the scroll values should always be integers.</param>
/// <param name="smoothScrollDuration">Set the duration (ms) used for smooth scroll animations.</param>
/// <param name="scheduleAtNextAnimationFrame">
/// A function to schedule an update at the next frame (used for smooth scroll animations); disposing what it returns
/// cancels the update.
/// </param>
internal sealed class ScrollableOptions(bool forceIntegerValues, double smoothScrollDuration, Func<Action, IDisposable> scheduleAtNextAnimationFrame)
{
    public bool ForceIntegerValues { get; } = forceIntegerValues;
    public double SmoothScrollDuration { get; } = smoothScrollDuration;
    public Func<Action, IDisposable> ScheduleAtNextAnimationFrame { get; } = scheduleAtNextAnimationFrame;
}

/// <summary>The scroll dimensions and positions, with the positions clamped to the dimensions.</summary>
internal sealed class ScrollState : IScrollDimensions, IScrollPosition, ISmoothScrollPosition
{
    private readonly bool _forceIntegerValues;

    public ScrollState(bool forceIntegerValues, double width, double scrollWidth, double scrollLeft, double height, double scrollHeight, double scrollTop)
    {
        _forceIntegerValues = forceIntegerValues;
        if (_forceIntegerValues)
        {
            width = Int32(width);
            scrollWidth = Int32(scrollWidth);
            scrollLeft = Int32(scrollLeft);
            height = Int32(height);
            scrollHeight = Int32(scrollHeight);
            scrollTop = Int32(scrollTop);
        }

        RawScrollLeft = scrollLeft; // before validation
        RawScrollTop = scrollTop; // before validation

        if (width < 0)
        {
            width = 0;
        }
        if (scrollLeft + width > scrollWidth)
        {
            scrollLeft = scrollWidth - width;
        }
        if (scrollLeft < 0)
        {
            scrollLeft = 0;
        }

        if (height < 0)
        {
            height = 0;
        }
        if (scrollTop + height > scrollHeight)
        {
            scrollTop = scrollHeight - height;
        }
        if (scrollTop < 0)
        {
            scrollTop = 0;
        }

        Width = width;
        ScrollWidth = scrollWidth;
        ScrollLeft = scrollLeft;
        Height = height;
        ScrollHeight = scrollHeight;
        ScrollTop = scrollTop;
    }

    public double RawScrollLeft { get; }
    public double RawScrollTop { get; }

    public double Width { get; }
    public double ScrollWidth { get; }
    public double ScrollLeft { get; }
    public double Height { get; }
    public double ScrollHeight { get; }
    public double ScrollTop { get; }

    public bool Equals(ScrollState other)
    {
        return RawScrollLeft == other.RawScrollLeft
            && RawScrollTop == other.RawScrollTop
            && Width == other.Width
            && ScrollWidth == other.ScrollWidth
            && ScrollLeft == other.ScrollLeft
            && Height == other.Height
            && ScrollHeight == other.ScrollHeight
            && ScrollTop == other.ScrollTop;
    }

    public ScrollState WithScrollDimensions(NewScrollDimensions update, bool useRawScrollPositions)
    {
        return new ScrollState(
            _forceIntegerValues,
            update.Width ?? Width,
            update.ScrollWidth ?? ScrollWidth,
            useRawScrollPositions ? RawScrollLeft : ScrollLeft,
            update.Height ?? Height,
            update.ScrollHeight ?? ScrollHeight,
            useRawScrollPositions ? RawScrollTop : ScrollTop);
    }

    public ScrollState WithScrollPosition(NewScrollPosition update)
    {
        return new ScrollState(
            _forceIntegerValues,
            Width,
            ScrollWidth,
            update.ScrollLeft ?? RawScrollLeft,
            Height,
            ScrollHeight,
            update.ScrollTop ?? RawScrollTop);
    }

    public ScrollEvent CreateScrollEvent(ScrollState previous, bool inSmoothScrolling)
    {
        var widthChanged = Width != previous.Width;
        var scrollWidthChanged = ScrollWidth != previous.ScrollWidth;
        var scrollLeftChanged = ScrollLeft != previous.ScrollLeft;

        var heightChanged = Height != previous.Height;
        var scrollHeightChanged = ScrollHeight != previous.ScrollHeight;
        var scrollTopChanged = ScrollTop != previous.ScrollTop;

        return new ScrollEvent
        {
            InSmoothScrolling = inSmoothScrolling,
            OldWidth = previous.Width,
            OldScrollWidth = previous.ScrollWidth,
            OldScrollLeft = previous.ScrollLeft,

            Width = Width,
            ScrollWidth = ScrollWidth,
            ScrollLeft = ScrollLeft,

            OldHeight = previous.Height,
            OldScrollHeight = previous.ScrollHeight,
            OldScrollTop = previous.ScrollTop,

            Height = Height,
            ScrollHeight = ScrollHeight,
            ScrollTop = ScrollTop,

            WidthChanged = widthChanged,
            ScrollWidthChanged = scrollWidthChanged,
            ScrollLeftChanged = scrollLeftChanged,

            HeightChanged = heightChanged,
            ScrollHeightChanged = scrollHeightChanged,
            ScrollTopChanged = scrollTopChanged,
        };
    }

    // JavaScript's `value | 0`: the value truncated to a 32-bit integer (NaN and the infinities give 0).
    private static double Int32(double value) => double.IsFinite(value) ? unchecked((int)(long)value) : 0;
}

/// <summary>A scroll position over scroll dimensions, which can move to a new position at once or in an animation.</summary>
internal sealed class Scrollable : IDisposable
{
    private double _smoothScrollDuration;
    private readonly Func<Action, IDisposable> _scheduleAtNextAnimationFrame;
    private ScrollState _state;
    private SmoothScrollingOperation? _smoothScrolling;

    private readonly Emitter<ScrollEvent> _onScroll = new();

    public Scrollable(ScrollableOptions options)
    {
        _smoothScrollDuration = options.SmoothScrollDuration;
        _scheduleAtNextAnimationFrame = options.ScheduleAtNextAnimationFrame;
        _state = new ScrollState(options.ForceIntegerValues, 0, 0, 0, 0, 0, 0);
        _smoothScrolling = null;
    }

    /// <summary>The dimensions or the position changed (upstream's onScroll).</summary>
    public event Action<ScrollEvent>? Scroll
    {
        add
        {
            if (value is not null)
            {
                _onScroll.Add(value);
            }
        }
        remove => _onScroll.Remove(value);
    }

    public void Dispose()
    {
        if (_smoothScrolling is not null)
        {
            _smoothScrolling.Dispose();
            _smoothScrolling = null;
        }
        _onScroll.Clear();
    }

    public void SetSmoothScrollDuration(double smoothScrollDuration)
    {
        _smoothScrollDuration = smoothScrollDuration;
    }

    public IScrollPosition ValidateScrollPosition(NewScrollPosition scrollPosition)
    {
        return _state.WithScrollPosition(scrollPosition);
    }

    public IScrollDimensions GetScrollDimensions()
    {
        return _state;
    }

    public void SetScrollDimensions(NewScrollDimensions dimensions, bool useRawScrollPositions)
    {
        var newState = _state.WithScrollDimensions(dimensions, useRawScrollPositions);
        SetState(newState, _smoothScrolling is not null);

        // Validate outstanding animated scroll position target
        _smoothScrolling?.AcceptScrollDimensions(_state);
    }

    /// <summary>
    /// Returns the final scroll position that the instance will have once the smooth scroll animation concludes.
    /// If no scroll animation is occurring, it will return the current scroll position instead.
    /// </summary>
    public IScrollPosition GetFutureScrollPosition()
    {
        if (_smoothScrolling is not null)
        {
            return _smoothScrolling.To;
        }
        return _state;
    }

    /// <summary>
    /// Returns the current scroll position.
    /// Note: This result might be an intermediate scroll position, as there might be an ongoing smooth scroll animation.
    /// </summary>
    public IScrollPosition GetCurrentScrollPosition()
    {
        return _state;
    }

    public void SetScrollPositionNow(NewScrollPosition update)
    {
        // no smooth scrolling requested
        var newState = _state.WithScrollPosition(update);

        // Terminate any outstanding smooth scrolling
        if (_smoothScrolling is not null)
        {
            _smoothScrolling.Dispose();
            _smoothScrolling = null;
        }

        SetState(newState, false);
    }

    public void SetScrollPositionSmooth(NewScrollPosition update, bool reuseAnimation = false)
    {
        if (_smoothScrollDuration == 0)
        {
            // Smooth scrolling not supported.
            SetScrollPositionNow(update);
            return;
        }

        if (_smoothScrolling is not null)
        {
            // Combine our pending scrollLeft/scrollTop with incoming scrollLeft/scrollTop
            update = new NewScrollPosition(
                update.ScrollLeft ?? _smoothScrolling.To.ScrollLeft,
                update.ScrollTop ?? _smoothScrolling.To.ScrollTop);

            // Validate `update`
            var validTarget = _state.WithScrollPosition(update);

            if (_smoothScrolling.To.ScrollLeft == validTarget.ScrollLeft && _smoothScrolling.To.ScrollTop == validTarget.ScrollTop)
            {
                // No need to interrupt or extend the current animation since we're going to the same place
                return;
            }
            SmoothScrollingOperation newSmoothScrolling;
            if (reuseAnimation)
            {
                newSmoothScrolling = new SmoothScrollingOperation(_smoothScrolling.From, validTarget, _smoothScrolling.StartTime, _smoothScrolling.Duration);
            }
            else
            {
                newSmoothScrolling = _smoothScrolling.Combine(_state, validTarget, _smoothScrollDuration);
            }
            _smoothScrolling.Dispose();
            _smoothScrolling = newSmoothScrolling;
        }
        else
        {
            // Validate `update`
            var validTarget = _state.WithScrollPosition(update);

            _smoothScrolling = SmoothScrollingOperation.Start(_state, validTarget, _smoothScrollDuration);
        }

        // Begin smooth scrolling animation
        _smoothScrolling.AnimationFrameDisposable = _scheduleAtNextAnimationFrame(OnAnimationFrame);
    }

    public bool HasPendingScrollAnimation()
    {
        return _smoothScrolling is not null;
    }

    private void OnAnimationFrame()
    {
        if (_smoothScrolling is null)
        {
            return;
        }
        _smoothScrolling.AnimationFrameDisposable = null;
        PerformSmoothScrolling();
    }

    private void PerformSmoothScrolling()
    {
        if (_smoothScrolling is null)
        {
            return;
        }
        var update = _smoothScrolling.Tick();
        var newState = _state.WithScrollPosition(new NewScrollPosition(update.ScrollLeft, update.ScrollTop));

        SetState(newState, true);

        if (_smoothScrolling is null)
        {
            // Looks like someone canceled the smooth scrolling
            // from the scroll event handler
            return;
        }

        if (update.IsDone)
        {
            _smoothScrolling.Dispose();
            _smoothScrolling = null;
            return;
        }

        // Continue smooth scrolling animation
        _smoothScrolling.AnimationFrameDisposable = _scheduleAtNextAnimationFrame(OnAnimationFrame);
    }

    private void SetState(ScrollState newState, bool inSmoothScrolling)
    {
        var oldState = _state;
        if (oldState.Equals(newState))
        {
            // no change
            return;
        }
        _state = newState;
        _onScroll.Fire(_state.CreateScrollEvent(oldState, inSmoothScrolling));
    }
}

/// <summary>Where a smooth scrolling animation is at a given time.</summary>
internal sealed class SmoothScrollingUpdate
{
    public SmoothScrollingUpdate(double scrollLeft, double scrollTop, bool isDone)
    {
        ScrollLeft = scrollLeft;
        ScrollTop = scrollTop;
        IsDone = isDone;
    }

    public double ScrollLeft { get; }

    public double ScrollTop { get; }

    public bool IsDone { get; }
}

/// <summary>A smooth scrolling animation from one position to another.</summary>
internal class SmoothScrollingOperation
{
    private Func<double, double> _scrollLeft = null!;
    private Func<double, double> _scrollTop = null!;

    public SmoothScrollingOperation(ISmoothScrollPosition from, ISmoothScrollPosition to, double startTime, double duration)
    {
        From = from;
        To = to;
        Duration = duration;
        StartTime = startTime;

        AnimationFrameDisposable = null;

        InitAnimations();
    }

    public ISmoothScrollPosition From { get; }

    public ISmoothScrollPosition To { get; private set; }

    public double Duration { get; }

    public double StartTime { get; }

    public IDisposable? AnimationFrameDisposable { get; set; }

    private void InitAnimations()
    {
        _scrollLeft = InitAnimation(From.ScrollLeft, To.ScrollLeft, To.Width);
        _scrollTop = InitAnimation(From.ScrollTop, To.ScrollTop, To.Height);
    }

    private static Func<double, double> InitAnimation(double from, double to, double viewportSize)
    {
        var delta = Math.Abs(from - to);
        if (delta > 2.5 * viewportSize)
        {
            double stop1, stop2;
            if (from < to)
            {
                // scroll to 75% of the viewportSize
                stop1 = from + 0.75 * viewportSize;
                stop2 = to - 0.75 * viewportSize;
            }
            else
            {
                stop1 = from - 0.75 * viewportSize;
                stop2 = to + 0.75 * viewportSize;
            }
            return CreateComposed(CreateEaseOutCubic(from, stop1), CreateEaseOutCubic(stop2, to), 0.33);
        }
        return CreateEaseOutCubic(from, to);
    }

    public void Dispose()
    {
        if (AnimationFrameDisposable is not null)
        {
            AnimationFrameDisposable.Dispose();
            AnimationFrameDisposable = null;
        }
    }

    public void AcceptScrollDimensions(ScrollState state)
    {
        To = state.WithScrollPosition(new NewScrollPosition(To.ScrollLeft, To.ScrollTop));
        InitAnimations();
    }

    public SmoothScrollingUpdate Tick()
    {
        return Tick(Now());
    }

    protected SmoothScrollingUpdate Tick(double now)
    {
        var completion = (now - StartTime) / Duration;

        if (completion < 1)
        {
            var newScrollLeft = _scrollLeft(completion);
            var newScrollTop = _scrollTop(completion);
            return new SmoothScrollingUpdate(newScrollLeft, newScrollTop, false);
        }

        return new SmoothScrollingUpdate(To.ScrollLeft, To.ScrollTop, true);
    }

    public SmoothScrollingOperation Combine(ISmoothScrollPosition from, ISmoothScrollPosition to, double duration)
    {
        return Start(from, to, duration);
    }

    public static SmoothScrollingOperation Start(ISmoothScrollPosition from, ISmoothScrollPosition to, double duration)
    {
        // +10 / -10 : pretend the animation already started for a quicker response to a scroll request
        duration = duration + 10;
        var startTime = Now() - 10;

        return new SmoothScrollingOperation(from, to, startTime, duration);
    }

    // Upstream's Date.now(): milliseconds, here from a monotonic clock.
    private static double Now() => Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency;

    private static Func<double, double> CreateEaseOutCubic(double from, double to)
    {
        var delta = to - from;
        return completion => from + delta * EaseOutCubic(completion);
    }

    private static Func<double, double> CreateComposed(Func<double, double> a, Func<double, double> b, double cut)
    {
        return completion =>
        {
            if (completion < cut)
            {
                return a(completion / cut);
            }
            return b((completion - cut) / (1 - cut));
        };
    }

    private static double EaseInCubic(double t) => Math.Pow(t, 3);

    private static double EaseOutCubic(double t) => 1 - EaseInCubic(1 - t);
}
