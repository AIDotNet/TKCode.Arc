// Ported from vscode (src/vs/editor/common/viewLayout/viewLayout.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - the configuration is an IViewLayoutConfiguration whose Options (ViewLayoutOptions.cs) has the options this reads,
//    read again at every use as upstream reads options.get(...); ConfigurationChangedEvent says by name which changed;
//  - onDidScroll and onDidContentSizeChange are the .NET events DidScroll (the Scrollable's Scroll) and
//    DidContentSizeChange, over Core/Emitter so that they keep upstream's delivery order;
//  - pixel values are doubles; EditorScrollDimensions and Viewport keep upstream's `x | 0` truncations;
//  - saveState returns the record ViewLayoutSavedState;
//  - the class keeps upstream's name, which is also the name of its namespace: code outside this namespace names it
//    TKCode.Arc.Editor.ViewLayout.ViewLayout (or through a using alias).

using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc.Editor.ViewLayout;

/// <summary>The scroll position a view state keeps (upstream's saveState result).</summary>
internal sealed record ViewLayoutSavedState(double ScrollTop, double ScrollTopWithoutViewZones, double ScrollLeft);

/// <summary>The size of the viewport and of the content, and the scroll size that follows from them.</summary>
internal sealed class EditorScrollDimensions
{
    public EditorScrollDimensions(double width, double contentWidth, double height, double contentHeight)
    {
        width = Int32(width);
        contentWidth = Int32(contentWidth);
        height = Int32(height);
        contentHeight = Int32(contentHeight);

        if (width < 0)
        {
            width = 0;
        }
        if (contentWidth < 0)
        {
            contentWidth = 0;
        }

        if (height < 0)
        {
            height = 0;
        }
        if (contentHeight < 0)
        {
            contentHeight = 0;
        }

        Width = width;
        ContentWidth = contentWidth;
        ScrollWidth = Math.Max(width, contentWidth);

        Height = height;
        ContentHeight = contentHeight;
        ScrollHeight = Math.Max(height, contentHeight);
    }

    public double Width { get; }

    public double ContentWidth { get; }

    public double ScrollWidth { get; }

    public double Height { get; }

    public double ContentHeight { get; }

    public double ScrollHeight { get; }

    public bool Equals(EditorScrollDimensions other)
    {
        return Width == other.Width
            && ContentWidth == other.ContentWidth
            && Height == other.Height
            && ContentHeight == other.ContentHeight;
    }

    // JavaScript's `value | 0`.
    private static double Int32(double value) => double.IsFinite(value) ? unchecked((int)(long)value) : 0;
}

/// <summary>A <see cref="Scrollable"/> over <see cref="EditorScrollDimensions"/> that reports content size changes.</summary>
internal sealed class EditorScrollable : IDisposable
{
    private readonly Scrollable _scrollable;
    private EditorScrollDimensions _dimensions;

    private readonly Emitter<ContentSizeChangedEvent> _onDidContentSizeChange = new();

    public EditorScrollable(double smoothScrollDuration, Func<Action, IDisposable> scheduleAtNextAnimationFrame)
    {
        _dimensions = new EditorScrollDimensions(0, 0, 0, 0);
        _scrollable = new Scrollable(new ScrollableOptions(true, smoothScrollDuration, scheduleAtNextAnimationFrame));
    }

    /// <summary>The scroll dimensions or position changed (upstream's onDidScroll, the Scrollable's onScroll).</summary>
    public event Action<ScrollEvent>? DidScroll
    {
        add => _scrollable.Scroll += value;
        remove => _scrollable.Scroll -= value;
    }

    /// <summary>The content width or height changed (upstream's onDidContentSizeChange).</summary>
    public event Action<ContentSizeChangedEvent>? DidContentSizeChange
    {
        add
        {
            if (value is not null)
            {
                _onDidContentSizeChange.Add(value);
            }
        }
        remove => _onDidContentSizeChange.Remove(value);
    }

    public void Dispose()
    {
        _onDidContentSizeChange.Clear();
        _scrollable.Dispose();
    }

    public Scrollable GetScrollable()
    {
        return _scrollable;
    }

    public void SetSmoothScrollDuration(double smoothScrollDuration)
    {
        _scrollable.SetSmoothScrollDuration(smoothScrollDuration);
    }

    public IScrollPosition ValidateScrollPosition(NewScrollPosition scrollPosition)
    {
        return _scrollable.ValidateScrollPosition(scrollPosition);
    }

    public EditorScrollDimensions GetScrollDimensions()
    {
        return _dimensions;
    }

    public void SetScrollDimensions(EditorScrollDimensions dimensions)
    {
        if (_dimensions.Equals(dimensions))
        {
            return;
        }

        var oldDimensions = _dimensions;
        _dimensions = dimensions;

        _scrollable.SetScrollDimensions(
            new NewScrollDimensions(dimensions.Width, dimensions.ScrollWidth, dimensions.Height, dimensions.ScrollHeight),
            true);

        var contentWidthChanged = oldDimensions.ContentWidth != dimensions.ContentWidth;
        var contentHeightChanged = oldDimensions.ContentHeight != dimensions.ContentHeight;
        if (contentWidthChanged || contentHeightChanged)
        {
            _onDidContentSizeChange.Fire(new ContentSizeChangedEvent(
                oldDimensions.ContentWidth, oldDimensions.ContentHeight,
                dimensions.ContentWidth, dimensions.ContentHeight));
        }
    }

    public IScrollPosition GetFutureScrollPosition()
    {
        return _scrollable.GetFutureScrollPosition();
    }

    public IScrollPosition GetCurrentScrollPosition()
    {
        return _scrollable.GetCurrentScrollPosition();
    }

    public void SetScrollPositionNow(NewScrollPosition update)
    {
        _scrollable.SetScrollPositionNow(update);
    }

    public void SetScrollPositionSmooth(NewScrollPosition update)
    {
        _scrollable.SetScrollPositionSmooth(update);
    }

    public bool HasPendingScrollAnimation()
    {
        return _scrollable.HasPendingScrollAnimation();
    }
}

/// <summary>
/// The vertical layout of the view lines (with whitespaces and custom line heights) and the scroll state over it.
/// </summary>
internal sealed class ViewLayout : IViewLayout, IDisposable
{
    private const double SmoothScrollingTime = 125;

    private readonly IViewLayoutConfiguration _configuration;
    private readonly LinesLayout _linesLayout;
    private double _maxLineWidth;
    private double _overlayWidgetsMinWidth;

    private readonly EditorScrollable _scrollable;

    public ViewLayout(IViewLayoutConfiguration configuration, int lineCount, IReadOnlyList<CustomLineHeightData> customLineHeightData, Func<Action, IDisposable> scheduleAtNextAnimationFrame)
    {
        _configuration = configuration;
        var options = _configuration.Options;
        var layoutInfo = options.LayoutInfo;
        var padding = options.Padding;

        _linesLayout = new LinesLayout(lineCount, options.LineHeight, padding.Top, padding.Bottom, customLineHeightData);
        _maxLineWidth = 0;
        _overlayWidgetsMinWidth = 0;

        _scrollable = new EditorScrollable(0, scheduleAtNextAnimationFrame);
        ConfigureSmoothScrollDuration();

        _scrollable.SetScrollDimensions(new EditorScrollDimensions(
            layoutInfo.ContentWidth,
            0,
            layoutInfo.Height,
            0));

        UpdateHeight();
    }

    /// <summary>The scroll dimensions or position changed (upstream's onDidScroll).</summary>
    public event Action<ScrollEvent>? DidScroll
    {
        add => _scrollable.DidScroll += value;
        remove => _scrollable.DidScroll -= value;
    }

    /// <summary>The content width or height changed (upstream's onDidContentSizeChange).</summary>
    public event Action<ContentSizeChangedEvent>? DidContentSizeChange
    {
        add => _scrollable.DidContentSizeChange += value;
        remove => _scrollable.DidContentSizeChange -= value;
    }

    public void Dispose()
    {
        _scrollable.Dispose();
    }

    public Scrollable GetScrollable()
    {
        return _scrollable.GetScrollable();
    }

    public void OnHeightMaybeChanged()
    {
        UpdateHeight();
    }

    private void ConfigureSmoothScrollDuration()
    {
        _scrollable.SetSmoothScrollDuration(_configuration.Options.SmoothScrolling ? SmoothScrollingTime : 0);
    }

    // ---- begin view event handlers

    public void OnConfigurationChanged(ConfigurationChangedEvent e)
    {
        var options = _configuration.Options;
        if (e.HasChanged(EditorOptionNames.LineHeight))
        {
            _linesLayout.SetDefaultLineHeight(options.LineHeight);
        }
        if (e.HasChanged(EditorOptionNames.Padding))
        {
            var padding = options.Padding;
            _linesLayout.SetPadding(padding.Top, padding.Bottom);
        }
        if (e.HasChanged(EditorOptionNames.LayoutInfo))
        {
            var layoutInfo = options.LayoutInfo;
            var width = layoutInfo.ContentWidth;
            var height = layoutInfo.Height;
            var scrollDimensions = _scrollable.GetScrollDimensions();
            var contentWidth = scrollDimensions.ContentWidth;
            _scrollable.SetScrollDimensions(new EditorScrollDimensions(
                width,
                scrollDimensions.ContentWidth,
                height,
                GetContentHeight(width, height, contentWidth)));
        }
        else
        {
            UpdateHeight();
        }
        if (e.HasChanged(EditorOptionNames.SmoothScrolling))
        {
            ConfigureSmoothScrollDuration();
        }
    }

    public void OnFlushed(int lineCount, IReadOnlyList<CustomLineHeightData> customLineHeightData)
    {
        _linesLayout.OnFlushed(lineCount, customLineHeightData);
    }

    public void OnLinesDeleted(int fromLineNumber, int toLineNumber)
    {
        _linesLayout.OnLinesDeleted(fromLineNumber, toLineNumber);
    }

    public void OnLinesInserted(int fromLineNumber, int toLineNumber)
    {
        _linesLayout.OnLinesInserted(fromLineNumber, toLineNumber);
    }

    // ---- end view event handlers

    private double GetHorizontalScrollbarHeight(double width, double scrollWidth)
    {
        var scrollbar = _configuration.Options.Scrollbar;
        if (scrollbar.Horizontal == ScrollbarVisibility.Hidden)
        {
            // horizontal scrollbar not visible
            return 0;
        }
        if (width >= scrollWidth)
        {
            // horizontal scrollbar not visible
            return 0;
        }
        return scrollbar.HorizontalScrollbarSize;
    }

    private double GetContentHeight(double width, double height, double contentWidth)
    {
        var options = _configuration.Options;

        var result = _linesLayout.GetLinesTotalHeight();
        if (options.ScrollBeyondLastLine)
        {
            result += Math.Max(0, height - options.LineHeight - options.Padding.Bottom);
        }
        else if (!options.Scrollbar.IgnoreHorizontalScrollbarInContentHeight)
        {
            result += GetHorizontalScrollbarHeight(width, contentWidth);
        }

        return result;
    }

    private void UpdateHeight()
    {
        var scrollDimensions = _scrollable.GetScrollDimensions();
        var width = scrollDimensions.Width;
        var height = scrollDimensions.Height;
        var contentWidth = scrollDimensions.ContentWidth;
        _scrollable.SetScrollDimensions(new EditorScrollDimensions(
            width,
            scrollDimensions.ContentWidth,
            height,
            GetContentHeight(width, height, contentWidth)));
    }

    // ---- Layouting logic

    public Viewport GetCurrentViewport()
    {
        var scrollDimensions = _scrollable.GetScrollDimensions();
        var currentScrollPosition = _scrollable.GetCurrentScrollPosition();
        return new Viewport(
            currentScrollPosition.ScrollTop,
            currentScrollPosition.ScrollLeft,
            scrollDimensions.Width,
            scrollDimensions.Height);
    }

    public Viewport GetFutureViewport()
    {
        var scrollDimensions = _scrollable.GetScrollDimensions();
        var currentScrollPosition = _scrollable.GetFutureScrollPosition();
        return new Viewport(
            currentScrollPosition.ScrollTop,
            currentScrollPosition.ScrollLeft,
            scrollDimensions.Width,
            scrollDimensions.Height);
    }

    private double ComputeContentWidth()
    {
        var options = _configuration.Options;
        var maxLineWidth = _maxLineWidth;
        var wrappingInfo = options.WrappingInfo;
        var fontInfo = options.FontInfo;
        var layoutInfo = options.LayoutInfo;
        if (wrappingInfo.IsViewportWrapping)
        {
            var minimap = options.Minimap;
            if (maxLineWidth > layoutInfo.ContentWidth + fontInfo.TypicalHalfwidthCharacterWidth)
            {
                // This is a case where viewport wrapping is on, but the line extends above the viewport
                if (minimap.Enabled && minimap.Side == "right")
                {
                    // We need to accommodate the scrollbar width
                    return maxLineWidth + layoutInfo.VerticalScrollbarWidth;
                }
            }
            return maxLineWidth;
        }
        else
        {
            var extraHorizontalSpace = options.ScrollBeyondLastColumn * fontInfo.TypicalHalfwidthCharacterWidth;
            var whitespaceMinWidth = _linesLayout.GetWhitespaceMinWidth();
            return Math.Max(Math.Max(maxLineWidth + extraHorizontalSpace + layoutInfo.VerticalScrollbarWidth, whitespaceMinWidth), _overlayWidgetsMinWidth);
        }
    }

    public void SetMaxLineWidth(double maxLineWidth)
    {
        _maxLineWidth = maxLineWidth;
        UpdateContentWidth();
    }

    public void SetOverlayWidgetsMinWidth(double overlayWidgetsMinWidth)
    {
        _overlayWidgetsMinWidth = overlayWidgetsMinWidth;
        UpdateContentWidth();
    }

    private void UpdateContentWidth()
    {
        var scrollDimensions = _scrollable.GetScrollDimensions();
        _scrollable.SetScrollDimensions(new EditorScrollDimensions(
            scrollDimensions.Width,
            ComputeContentWidth(),
            scrollDimensions.Height,
            scrollDimensions.ContentHeight));

        // The height might depend on the fact that there is a horizontal scrollbar or not
        UpdateHeight();
    }

    // ---- view state

    public ViewLayoutSavedState SaveState()
    {
        var currentScrollPosition = _scrollable.GetFutureScrollPosition();
        var scrollTop = currentScrollPosition.ScrollTop;
        var firstLineNumberInViewport = _linesLayout.GetLineNumberAtOrAfterVerticalOffset(scrollTop);
        var whitespaceAboveFirstLine = _linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(firstLineNumberInViewport);
        return new ViewLayoutSavedState(scrollTop, scrollTop - whitespaceAboveFirstLine, currentScrollPosition.ScrollLeft);
    }

    // ----

    public bool ChangeWhitespace(Action<IWhitespaceChangeAccessor> callback)
    {
        var hadAChange = _linesLayout.ChangeWhitespace(callback);
        if (hadAChange)
        {
            OnHeightMaybeChanged();
        }
        return hadAChange;
    }

    public bool ChangeSpecialLineHeights(Action<ILineHeightChangeAccessor> callback)
    {
        var hadAChange = _linesLayout.ChangeLineHeights(callback);
        if (hadAChange)
        {
            OnHeightMaybeChanged();
        }
        return hadAChange;
    }

    public double GetVerticalOffsetForLineNumber(int lineNumber, bool includeViewZones = false)
    {
        return _linesLayout.GetVerticalOffsetForLineNumber(lineNumber, includeViewZones);
    }

    public double GetVerticalOffsetAfterLineNumber(int lineNumber, bool includeViewZones = false)
    {
        return _linesLayout.GetVerticalOffsetAfterLineNumber(lineNumber, includeViewZones);
    }

    public double GetLineHeightForLineNumber(int lineNumber)
    {
        return _linesLayout.GetLineHeightForLineNumber(lineNumber);
    }

    public bool IsAfterLines(double verticalOffset)
    {
        return _linesLayout.IsAfterLines(verticalOffset);
    }

    public bool IsInTopPadding(double verticalOffset)
    {
        return _linesLayout.IsInTopPadding(verticalOffset);
    }

    public bool IsInBottomPadding(double verticalOffset)
    {
        return _linesLayout.IsInBottomPadding(verticalOffset);
    }

    public int GetLineNumberAtVerticalOffset(double verticalOffset)
    {
        return _linesLayout.GetLineNumberAtOrAfterVerticalOffset(verticalOffset);
    }

    public IViewWhitespaceViewportData? GetWhitespaceAtVerticalOffset(double verticalOffset)
    {
        return _linesLayout.GetWhitespaceAtVerticalOffset(verticalOffset);
    }

    public IPartialViewLinesViewportData GetLinesViewportData()
    {
        var visibleBox = GetCurrentViewport();
        return _linesLayout.GetLinesViewportData(visibleBox.Top, visibleBox.Top + visibleBox.Height);
    }

    public IPartialViewLinesViewportData GetLinesViewportDataAtScrollTop(double scrollTop)
    {
        // do some minimal validations on scrollTop
        var scrollDimensions = _scrollable.GetScrollDimensions();
        if (scrollTop + scrollDimensions.Height > scrollDimensions.ScrollHeight)
        {
            scrollTop = scrollDimensions.ScrollHeight - scrollDimensions.Height;
        }
        if (scrollTop < 0)
        {
            scrollTop = 0;
        }
        return _linesLayout.GetLinesViewportData(scrollTop, scrollTop + scrollDimensions.Height);
    }

    public IViewWhitespaceViewportData[] GetWhitespaceViewportData()
    {
        var visibleBox = GetCurrentViewport();
        return _linesLayout.GetWhitespaceViewportData(visibleBox.Top, visibleBox.Top + visibleBox.Height);
    }

    public IEditorWhitespace[] GetWhitespaces()
    {
        return _linesLayout.GetWhitespaces();
    }

    // ----

    public double GetContentWidth()
    {
        var scrollDimensions = _scrollable.GetScrollDimensions();
        return scrollDimensions.ContentWidth;
    }

    public double GetScrollWidth()
    {
        var scrollDimensions = _scrollable.GetScrollDimensions();
        return scrollDimensions.ScrollWidth;
    }

    public double GetContentHeight()
    {
        var scrollDimensions = _scrollable.GetScrollDimensions();
        return scrollDimensions.ContentHeight;
    }

    public double GetScrollHeight()
    {
        var scrollDimensions = _scrollable.GetScrollDimensions();
        return scrollDimensions.ScrollHeight;
    }

    public double GetCurrentScrollLeft()
    {
        var currentScrollPosition = _scrollable.GetCurrentScrollPosition();
        return currentScrollPosition.ScrollLeft;
    }

    public double GetCurrentScrollTop()
    {
        var currentScrollPosition = _scrollable.GetCurrentScrollPosition();
        return currentScrollPosition.ScrollTop;
    }

    public IScrollPosition ValidateScrollPosition(NewScrollPosition scrollPosition)
    {
        return _scrollable.ValidateScrollPosition(scrollPosition);
    }

    public void SetScrollPosition(NewScrollPosition position, ScrollType type)
    {
        if (type == ScrollType.Immediate)
        {
            _scrollable.SetScrollPositionNow(position);
        }
        else
        {
            _scrollable.SetScrollPositionSmooth(position);
        }
    }

    public bool HasPendingScrollAnimation()
    {
        return _scrollable.HasPendingScrollAnimation();
    }

    public void DeltaScrollNow(double deltaScrollLeft, double deltaScrollTop)
    {
        var currentScrollPosition = _scrollable.GetCurrentScrollPosition();
        _scrollable.SetScrollPositionNow(new NewScrollPosition(
            currentScrollPosition.ScrollLeft + deltaScrollLeft,
            currentScrollPosition.ScrollTop + deltaScrollTop));
    }
}
