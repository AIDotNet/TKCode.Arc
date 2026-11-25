// Ported from vscode (src/vs/editor/contrib/hover/browser/contentHoverWidget.ts and the scroll / focus parts of
// resizableContentWidget.ts, commit b57d5c9e), MIT license; the DOM is a behavior reference only.
// Differences from upstream:
//  - the widget is a CodeEditorHoverSurface in the editor's widget host; it is not resizable with the mouse (the
//    sash of ResizableContentWidget is not ported) and keeps no persisted size;
//  - the height is limited by the theme's HoverMaxHeight and the room above or below the line; the content scrolls in
//    an Avalonia ScrollViewer (upstream's DomScrollableElement);
//  - the position preference is upstream's (above when editor.hover.above and there is room, else below); the editor's
//    content widget layout does the fallback;
//  - the mouse coordinates are relative to the editor's view (upstream: page coordinates).

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib.Hover;
using TKCode.Arc.Editor.Platform;

namespace TKCode.Arc;

/// <summary>The content hover's widget (upstream's ContentHoverWidget).</summary>
internal sealed class ContentHoverWidget : ICodeEditorContentWidget, IDisposable
{
    public const string WidgetId = "editor.contrib.resizableContentHoverWidget";

    private const double ScrollLineStep = 30;
    private const double MouseTolerance = 4;

    private readonly CodeEditor _editor;
    private readonly Func<HoverOptions> _options;
    private readonly ScrollViewer _scrollViewer;
    private readonly DockPanel _layout;
    private readonly IContextKey<bool> _hoverVisibleKey;
    private readonly IContextKey<bool> _hoverFocusedKey;
    private RenderedContentHover? _renderedHover;
    private TextPosition? _position;
    private TextPosition? _secondaryPosition;
    private bool _preferAbove;
    private double? _initialMousePosX;
    private double? _initialMousePosY;
    private double? _closestMouseDistance;
    private bool _added;

    public ContentHoverWidget(CodeEditor editor, Func<HoverOptions> options)
    {
        _editor = editor;
        _options = options;
        _hoverVisibleKey = editor.CreateContextKey(EditorContextKeys.HoverVisible);
        _hoverFocusedKey = editor.CreateContextKey(EditorContextKeys.HoverFocused);
        _scrollViewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Focusable = true,
        };
        _layout = new DockPanel { LastChildFill = true };
        _layout.Children.Add(_scrollViewer);
        Surface = new CodeEditorHoverSurface { Content = _layout, Classes = { "content-hover" } };
        Surface.GotFocus += OnFocusChanged;
        Surface.LostFocus += OnFocusChanged;
        Surface.KeyDown += OnKeyDown;
        Surface.PointerExited += OnPointerExited;
    }

    /// <summary>The card the hover is drawn on.</summary>
    public CodeEditorHoverSurface Surface { get; }

    public string Id => WidgetId;

    public Control Control => Surface;

    /// <summary>Upstream's position: where the hover shows, or null when it is hidden.</summary>
    public TextPosition? Position => _position;

    /// <summary>Whether the hover shows.</summary>
    public bool IsVisible => _position is not null;

    /// <summary>Whether the keyboard focus is in the hover.</summary>
    public bool IsFocused => Surface.IsKeyboardFocusWithin;

    /// <summary>Whether the hover was opened from the keyboard (it then stays when the mouse moves).</summary>
    public bool IsVisibleFromKeyboard => _renderedHover?.Source == HoverStartSource.Keyboard;

    /// <summary>The hover shown.</summary>
    public RenderedContentHover? RenderedHover => _renderedHover;

    /// <summary>The scroll viewer around the parts (for tests).</summary>
    internal ScrollViewer ScrollViewer => _scrollViewer;

    /// <summary>The hover hid itself (Escape, the pointer left the editor).</summary>
    public event Action? HideRequested;

    public ContentWidgetPosition? GetPosition()
    {
        if (_position is not { } position)
        {
            return null;
        }

        IReadOnlyList<ContentWidgetPositionPreference> preference = _preferAbove
            ? [ContentWidgetPositionPreference.Above, ContentWidgetPositionPreference.Below]
            : [ContentWidgetPositionPreference.Below, ContentWidgetPositionPreference.Above];
        return new ContentWidgetPosition(position, preference)
        {
            SecondaryPosition = _secondaryPosition,
        };
    }

    /// <summary>Shows a rendered hover (upstream's show).</summary>
    public void Show(RenderedContentHover renderedHover)
    {
        _renderedHover = renderedHover;
        _position = renderedHover.ShowAtPosition;
        _secondaryPosition = renderedHover.ShowAtSecondaryPosition;
        _initialMousePosX = renderedHover.InitialMousePosX;
        _initialMousePosY = renderedHover.InitialMousePosY;
        _closestMouseDistance = null;

        _scrollViewer.Content = renderedHover.Content;
        if (_layout.Children.Count > 1)
        {
            _layout.Children.RemoveAt(0);
        }

        if (renderedHover.StatusBar is { } statusBar)
        {
            DockPanel.SetDock(statusBar, Dock.Bottom);
            _layout.Children.Insert(0, statusBar);
        }

        _scrollViewer.Offset = default;
        if (!_added)
        {
            _editor.AddContentWidget(this);
            _added = true;
        }

        UpdateMaxHeight();
        _editor.LayoutContentWidget(this);

        _hoverVisibleKey.Set(true);
        if (renderedHover.ShouldFocus)
        {
            Focus();
        }
    }

    /// <summary>Hides the hover (upstream's hide); the editor gets the focus back if the hover had it.</summary>
    public void Hide()
    {
        if (_position is null && _renderedHover is null)
        {
            return;
        }

        var hadFocus = IsFocused;
        _renderedHover?.Dispose();
        _renderedHover = null;
        _position = null;
        _secondaryPosition = null;
        _scrollViewer.Content = null;
        if (_layout.Children.Count > 1)
        {
            _layout.Children.RemoveAt(0);
        }

        _hoverVisibleKey.Set(false);
        _hoverFocusedKey.Set(false);
        if (_added)
        {
            _editor.LayoutContentWidget(this);
        }

        if (hadFocus)
        {
            _editor.Focus();
        }
    }

    /// <summary>The content changed size (a verbosity change): place the hover again.</summary>
    public void HandleContentsChanged() => _editor.LayoutContentWidget(this);

    // Upstream's _findPositionPreference and the maximum height: the preferred side (above when editor.hover.above) if
    // the hover fits there, else the side with more room; the content scrolls in what that side has.
    private void UpdateMaxHeight()
    {
        _preferAbove = _options().Above;
        _scrollViewer.MaxHeight = double.PositiveInfinity;
        if (_position is not { } position || _editor.GetScrolledVisiblePosition(position) is not { } visible)
        {
            return;
        }

        var layout = _editor.GetLayoutSize();

        // as wide as the text area at most (upstream's hover can overflow the editor; here widgets cannot)
        _layout.MaxWidth = CodeEditorWidgetSizing.AvailableWidth(_editor);
        var spaceAbove = visible.Top;
        var spaceBelow = layout.Height - (visible.Top + visible.Height);
        Surface.Measure(new Size(layout.Width, double.PositiveInfinity));
        var hoverHeight = Surface.DesiredSize.Height;
        var preferred = _preferAbove ? spaceAbove : spaceBelow;
        var other = _preferAbove ? spaceBelow : spaceAbove;
        if (hoverHeight > preferred && other > preferred)
        {
            _preferAbove = !_preferAbove;
        }

        var room = Math.Max(preferred, other);
        if (hoverHeight > room)
        {
            var chrome = hoverHeight - _scrollViewer.DesiredSize.Height;
            _scrollViewer.MaxHeight = Math.Max(24, room - chrome - 2);
        }
    }

    /// <summary>
    /// Whether the pointer moves toward the hover (upstream's isMouseGettingCloser): the first point is remembered, after
    /// that the pointer may not get farther than 4 pixels from the closest it came.
    /// </summary>
    public bool IsMouseGettingCloser(double posx, double posy)
    {
        if (_renderedHover is null || _editor.GetContentWidgetPlacement(WidgetId) is not { } placement)
        {
            return false;
        }

        if (_initialMousePosX is not { } initialX || _initialMousePosY is not { } initialY)
        {
            _initialMousePosX = posx;
            _initialMousePosY = posy;
            return false;
        }

        var rect = placement.Bounds;
        _closestMouseDistance ??= HoverUtils.ComputeDistanceFromPointToRectangle(initialX, initialY, rect.X, rect.Y, rect.Width, rect.Height);
        var distance = HoverUtils.ComputeDistanceFromPointToRectangle(posx, posy, rect.X, rect.Y, rect.Width, rect.Height);
        if (distance > _closestMouseDistance + MouseTolerance)
        {
            return false;
        }

        _closestMouseDistance = Math.Min(_closestMouseDistance.Value, distance);
        return true;
    }

    /// <summary>Whether a point of the editor's view lies on the hover (upstream's isMousePositionWithinElement on its node).</summary>
    public bool ContainsPoint(Point point)
    {
        if (!IsVisible || _editor.GetContentWidgetPlacement(WidgetId) is not { } placement)
        {
            return false;
        }

        var rect = placement.Bounds;
        return HoverUtils.IsMousePositionWithinElement(rect.X, rect.Y, rect.Width, rect.Height, point.X, point.Y);
    }

    /// <summary>Whether a point of the editor's view is on the hover's card (upstream's containsNode of the pointer's target).</summary>
    public bool ContainsNodePoint(Point point) =>
        IsVisible && _editor.GetContentWidgetPlacement(WidgetId) is { } placement && placement.Bounds.Contains(point);

    /// <summary>Moves the keyboard focus into the hover (upstream's focus).</summary>
    public void Focus()
    {
        _scrollViewer.Focus(NavigationMethod.Tab);
        _hoverFocusedKey.Set(IsFocused);
    }

    public void ScrollUp() => ScrollBy(0, -ScrollLineStep);

    public void ScrollDown() => ScrollBy(0, ScrollLineStep);

    public void ScrollLeft() => ScrollBy(-ScrollLineStep, 0);

    public void ScrollRight() => ScrollBy(ScrollLineStep, 0);

    public void PageUp() => ScrollBy(0, -_scrollViewer.Viewport.Height);

    public void PageDown() => ScrollBy(0, _scrollViewer.Viewport.Height);

    public void GoToTop() => _scrollViewer.Offset = new Vector(_scrollViewer.Offset.X, 0);

    public void GoToBottom() => _scrollViewer.Offset = new Vector(_scrollViewer.Offset.X, Math.Max(0, _scrollViewer.Extent.Height - _scrollViewer.Viewport.Height));

    private void ScrollBy(double dx, double dy)
    {
        var maxX = Math.Max(0, _scrollViewer.Extent.Width - _scrollViewer.Viewport.Width);
        var maxY = Math.Max(0, _scrollViewer.Extent.Height - _scrollViewer.Viewport.Height);
        _scrollViewer.Offset = new Vector(Math.Clamp(_scrollViewer.Offset.X + dx, 0, maxX), Math.Clamp(_scrollViewer.Offset.Y + dy, 0, maxY));
    }

    private void OnFocusChanged(object? sender, RoutedEventArgs e) => _hoverFocusedKey.Set(IsFocused);

    // upstream's wrapper: Escape in the hover hides it
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None)
        {
            e.Handled = true;
            HideRequested?.Invoke();
        }
    }

    // upstream's wrapper _onMouseLeave: the pointer left the hover to somewhere outside the editor
    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        if (_editor.WidgetHost is not { } host)
        {
            return;
        }

        var point = e.GetPosition(host);
        var size = host.Bounds.Size;
        var outside = point.X < 0 || point.Y < 0 || point.X > size.Width || point.Y > size.Height;
        if (outside)
        {
            HideRequested?.Invoke();
        }
    }

    public void Dispose()
    {
        Hide();
        Surface.GotFocus -= OnFocusChanged;
        Surface.LostFocus -= OnFocusChanged;
        Surface.KeyDown -= OnKeyDown;
        Surface.PointerExited -= OnPointerExited;
        if (_added)
        {
            _editor.RemoveContentWidget(this);
            _added = false;
        }
    }
}
