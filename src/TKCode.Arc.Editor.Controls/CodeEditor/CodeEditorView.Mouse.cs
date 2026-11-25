using System.Diagnostics;
using Avalonia;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc;

// The mouse (upstream's MouseHandler and MouseDownOperation in browser/controller/mouseHandler.ts, and
// ViewController.dispatchMouse in browser/view/viewController.ts): a press dispatches by click count, modifiers, button
// and whether it started on the line numbers; a drag dispatches again "in selection mode" with the same click count, so
// a double-click drag extends by words and a triple-click drag by lines; outside the view, a timer scrolls towards the
// pointer and keeps extending the selection (MouseDownOperation's TopBottomDragScrolling).
// The multi-cursor modifier is upstream's default, Alt. Differences: Linux's selection clipboard (middle-click paste) is
// not modelled, so the middle button always column-selects; dragging a selection to move its text (dragAndDrop) is
// not ported yet; the timer runs every 16 ms instead of on animation frames. Touch pointers go to
// CodeEditorView.Touch.cs (a press, a move or a wheel turn of the mouse also stops a touch flick).
internal sealed partial class CodeEditorView
{
    private const string MouseSource = "mouse";

    private bool _mouseDown;
    private int _mouseDownCount;
    private bool _startedOnLineNumbers;
    private bool _middleButton;
    private KeyModifiers _mouseModifiers;
    private Point _lastMousePoint;
    private DispatcherTimer? _autoScrollTimer;
    private long _autoScrollLastTimestamp;

    /// <summary>Whether a drag outside the view is scrolling it.</summary>
    internal bool IsAutoScrolling => _autoScrollTimer is { IsEnabled: true };

    /// <summary>What lies under a point of the control (upstream's IMouseTarget, reduced to what dispatchMouse reads).</summary>
    private readonly record struct MouseTarget(TextPosition Position, TextPosition ViewPosition, int MouseColumn);

    /// <summary>The view position under a point of the control.</summary>
    internal TextPosition GetViewPositionAt(Point point)
    {
        var viewModel = _viewModel!;
        var layout = viewModel.ViewLayout;
        var lineCount = viewModel.GetLineCount();
        var y = point.Y + ScrollTop;
        int lineNumber;
        if (layout.IsAfterLines(y))
        {
            lineNumber = lineCount;
            return new TextPosition(lineNumber, viewModel.GetLineMaxColumn(lineNumber));
        }

        lineNumber = Math.Clamp(layout.GetLineNumberAtVerticalOffset(Math.Max(0, y)), 1, lineCount);
        // the drawn line's own hit testing, so a click lands where the glyphs are (wide characters included)
        return new TextPosition(lineNumber, GetColumnAtX(lineNumber, point.X));
    }

    private MouseTarget GetMouseTarget(Point point)
    {
        var viewModel = _viewModel!;
        TextPosition viewPosition;
        if (_startedOnLineNumbers)
        {
            // a line number stands for its whole line (upstream's GUTTER_LINE_NUMBERS target)
            var line = GetViewPositionAt(new Point(GutterWidth, point.Y)).LineNumber;
            viewPosition = new TextPosition(line, 1);
        }
        else
        {
            viewPosition = GetViewPositionAt(point);
        }

        // upstream's mouseColumn: the 1-based visible column under the pointer, also past the end of the line
        var mouseColumn = (int)Math.Round(Math.Max(0, point.X - GutterWidth + ScrollLeft) / _charWidth) + 1;
        var position = viewModel.CoordinatesConverter.ConvertViewPositionToModelPosition(viewPosition);
        return new MouseTarget(position, viewPosition, mouseColumn);
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (IsTouch(e))
        {
            // touch has its own gestures (CodeEditorView.Touch.cs)
            OnTouchPressed(e);
            return;
        }

        StopInertia();
        var properties = e.GetCurrentPoint(this).Properties;
        if (_viewModel is null || _editor is null || !(properties.IsLeftButtonPressed || properties.IsMiddleButtonPressed))
        {
            return;
        }

        _editor.OnBeforeInteractionEvent();
        _editor.Focus();
        CancelComposition();
        var point = e.GetPosition(this);

        // the editor's listeners first (upstream's onMouseDown), then a gutter column's handler (folding chevrons);
        // either can keep the editor from moving the cursor. A press on an empty view zone selects nothing.
        var mouseTarget = GetMouseTargetAt(point);
        var mouseEvent = new CodeEditorMouseEvent(e, mouseTarget);
        _editor.RaiseMouseDown(mouseEvent);
        if (mouseEvent.Handled
            || (properties.IsLeftButtonPressed && point.X < GutterWidth && DispatchGutterPress(e, point))
            || mouseTarget.Type is CodeEditorMouseTargetType.ContentViewZone or CodeEditorMouseTargetType.GutterViewZone)
        {
            e.Handled = true;
            return;
        }

        _middleButton = !properties.IsLeftButtonPressed;
        _startedOnLineNumbers = !_middleButton && point.X < GutterWidth;
        _mouseDownCount = _middleButton ? 1 : Math.Max(1, e.ClickCount);
        _mouseModifiers = e.KeyModifiers;
        _lastMousePoint = point;
        _mouseDown = true;

        var target = GetMouseTarget(point);
        if (_middleButton)
        {
            // a middle-button press starts a new column selection (upstream: doColumnSelect = shiftKey)
            ColumnSelect(target, _mouseModifiers.HasFlag(KeyModifiers.Shift));
        }
        else
        {
            DispatchMouse(target, inSelectionMode: _mouseModifiers.HasFlag(KeyModifiers.Shift), NavigationCommandRevealType.Regular);
        }

        e.Pointer.Capture(this);
        RestartBlink();
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (IsTouch(e))
        {
            OnTouchMoved(e);
            return;
        }

        if (_viewModel is null)
        {
            return;
        }

        if (!_mouseDown)
        {
            var hover = e.GetPosition(this);
            DispatchGutterMove(e, hover);
            _editor?.RaiseMouseMove(new CodeEditorMouseEvent(e, GetMouseTargetAt(hover)));
            return;
        }

        _lastMousePoint = e.GetPosition(this);
        _editor?.RaiseMouseMove(new CodeEditorMouseEvent(e, GetMouseTargetAt(_lastMousePoint)));
        _mouseModifiers = e.KeyModifiers;
        if (IsOutside(_lastMousePoint))
        {
            StartAutoScroll();
            return;
        }

        StopAutoScroll();
        DispatchDrag(GetMouseTarget(_lastMousePoint), NavigationCommandRevealType.Regular);
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (IsTouch(e))
        {
            OnTouchReleased(e);
            return;
        }

        if (_viewModel is not null)
        {
            _editor?.RaiseMouseUp(new CodeEditorMouseEvent(e, GetMouseTargetAt(e.GetPosition(this))));
        }

        if (_mouseDown)
        {
            EndMouseDown();
            e.Pointer.Capture(null);
        }
    }

    /// <inheritdoc />
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_hoveredColumn is { } column)
        {
            _hoveredColumn = null;
            column.PointerMoved?.Invoke(null);
        }

        _editor?.RaiseMouseLeave(e);
    }

    /// <inheritdoc />
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (e.Pointer.Type == PointerType.Touch)
        {
            OnTouchCaptureLost();
            return;
        }

        EndMouseDown();
    }

    /// <inheritdoc />
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (_viewModel is null)
        {
            return;
        }

        // Shift turns a vertical wheel sideways (upstream's scrollYToX); the scroll event repaints the layers
        StopInertia();
        var delta = e.Delta;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) && delta.X == 0)
        {
            delta = new Vector(delta.Y, 0);
        }

        var deltaLeft = -delta.X * _charWidth * 3;
        var deltaTop = -delta.Y * _lineHeight * 3;
        var layout = _viewModel.ViewLayout;
        if (_editor is { SmoothScrolling: true })
        {
            // from where a running animation is heading, so quick wheel turns add up (upstream's MouseWheelClassifier path)
            var future = layout.GetScrollable().GetFutureScrollPosition();
            layout.SetScrollPosition(new NewScrollPosition(future.ScrollLeft + deltaLeft, future.ScrollTop + deltaTop), ScrollType.Smooth);
        }
        else
        {
            layout.DeltaScrollNow(deltaLeft, deltaTop);
        }

        e.Handled = true;
    }

    private void EndMouseDown()
    {
        _mouseDown = false;
        StopAutoScroll();
    }

    private bool IsOutside(Point point) =>
        point.Y < 0 || point.Y > Bounds.Height || point.X < 0 || point.X > Bounds.Width;

    private void DispatchDrag(MouseTarget target, NavigationCommandRevealType revealType)
    {
        if (_middleButton)
        {
            ColumnSelect(target, doColumnSelect: true);
        }
        else
        {
            DispatchMouse(target, inSelectionMode: true, revealType);
        }

        RestartBlink();
    }

    // ViewController.dispatchMouse with the left button (the middle button column-selects before it gets here).
    private void DispatchMouse(MouseTarget target, bool inSelectionMode, NavigationCommandRevealType revealType)
    {
        var viewModel = _viewModel!;
        var options = new CoreNavigationCommands.MoveCommandOptions
        {
            Source = MouseSource,
            Position = target.Position,
            ViewPosition = target.ViewPosition,
            RevealType = revealType,
        };
        var multiCursor = HasMultiCursorModifier();
        if (_startedOnLineNumbers)
        {
            // a drag that started on the line numbers works on whole lines
            if (multiCursor)
            {
                if (inSelectionMode)
                {
                    CoreNavigationCommands.LastCursorLineSelect.RunCoreEditorCommand(viewModel, options);
                }
                else
                {
                    CreateCursor(target, wholeLine: true);
                }
            }
            else if (inSelectionMode)
            {
                CoreNavigationCommands.LineSelectDrag.RunCoreEditorCommand(viewModel, options);
            }
            else
            {
                CoreNavigationCommands.LineSelect.RunCoreEditorCommand(viewModel, options);
            }
        }
        else if (_mouseDownCount >= 4)
        {
            CoreNavigationCommands.SelectAll.RunCoreEditorCommand(viewModel, new CoreNavigationCommands.BaseCommandOptions { Source = MouseSource });
        }
        else if (_mouseDownCount == 3)
        {
            if (multiCursor)
            {
                (inSelectionMode ? CoreNavigationCommands.LastCursorLineSelectDrag : CoreNavigationCommands.LastCursorLineSelect)
                    .RunCoreEditorCommand(viewModel, options);
            }
            else
            {
                (inSelectionMode ? CoreNavigationCommands.LineSelectDrag : CoreNavigationCommands.LineSelect)
                    .RunCoreEditorCommand(viewModel, options);
            }
        }
        else if (_mouseDownCount == 2)
        {
            if (multiCursor)
            {
                CoreNavigationCommands.LastCursorWordSelect.RunCoreEditorCommand(viewModel, options);
            }
            else
            {
                (inSelectionMode ? CoreNavigationCommands.WordSelectDrag : CoreNavigationCommands.WordSelect)
                    .RunCoreEditorCommand(viewModel, options);
            }
        }
        else if (multiCursor)
        {
            // multi-cursor operations only when purely the multi-cursor modifier is pressed
            if (!HasNonMultiCursorModifier())
            {
                if (_mouseModifiers.HasFlag(KeyModifiers.Shift))
                {
                    ColumnSelect(target, doColumnSelect: true);
                }
                else if (inSelectionMode)
                {
                    CoreNavigationCommands.LastCursorMoveToSelect.RunCoreEditorCommand(viewModel, options);
                }
                else
                {
                    CreateCursor(target, wholeLine: false);
                }
            }
        }
        else if (inSelectionMode)
        {
            CoreNavigationCommands.MoveToSelect.RunCoreEditorCommand(viewModel, options);
        }
        else
        {
            CoreNavigationCommands.MoveTo.RunCoreEditorCommand(viewModel, options);
        }
    }

    // multiCursorModifier 'alt' (upstream's default): Alt adds cursors, Ctrl / Cmd are the other modifiers
    private bool HasMultiCursorModifier() => _mouseModifiers.HasFlag(KeyModifiers.Alt);

    private bool HasNonMultiCursorModifier() =>
        _mouseModifiers.HasFlag(KeyModifiers.Control) || _mouseModifiers.HasFlag(KeyModifiers.Meta);

    private void CreateCursor(MouseTarget target, bool wholeLine) =>
        CoreNavigationCommands.CreateCursor.RunCoreEditorCommand(
            _viewModel!,
            new CoreNavigationCommands.CreateCursorCommandOptions
            {
                Source = MouseSource,
                Position = target.Position,
                ViewPosition = target.ViewPosition,
                WholeLine = wholeLine,
            });

    private void ColumnSelect(MouseTarget target, bool doColumnSelect) =>
        CoreNavigationCommands.ColumnSelect.RunCoreEditorCommand(
            _viewModel!,
            new CoreNavigationCommands.ColumnSelectCommandOptions
            {
                Source = MouseSource,
                Position = target.Position,
                ViewPosition = target.ViewPosition,
                MouseColumn = target.MouseColumn,
                DoColumnSelect = doColumnSelect,
            });

    // ---- auto-scroll (MouseDownOperation's TopBottomDragScrolling, and the same towards the left and right)

    private void StartAutoScroll()
    {
        if (IsAutoScrolling)
        {
            return;
        }

        _autoScrollTimer ??= new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Input, (_, _) =>
        {
            var now = Stopwatch.GetTimestamp();
            var elapsed = Stopwatch.GetElapsedTime(_autoScrollLastTimestamp, now).TotalMilliseconds;
            _autoScrollLastTimestamp = now;
            AutoScrollTick(elapsed);
        });
        _autoScrollLastTimestamp = Stopwatch.GetTimestamp();
        _autoScrollTimer.Start();

        // the selection reaches the edge right away; the timer scrolls from there
        AutoScrollTick(0);
    }

    private void StopAutoScroll() => _autoScrollTimer?.Stop();

    /// <summary>
    /// One step of the drag auto-scroll: scrolls towards the pointer by the time since the last step, then extends the
    /// selection to the edge line (or column) under the pointer.
    /// </summary>
    internal void AutoScrollTick(double elapsedMilliseconds)
    {
        if (!_mouseDown || _viewModel is null || _editor is null)
        {
            StopAutoScroll();
            return;
        }

        var point = _lastMousePoint;
        var height = Bounds.Height;
        var width = TextAreaRight;
        var layout = _viewModel.ViewLayout;
        var elapsed = Math.Clamp(elapsedMilliseconds, 0, 100) / 1000;
        if (point.Y < 0 || point.Y > height)
        {
            var above = point.Y < 0;
            var distance = above ? -point.Y : point.Y - height;
            var pixels = GetScrollSpeed(distance, height, _lineHeight) * elapsed * _lineHeight;
            if (pixels > 0)
            {
                layout.DeltaScrollNow(0, above ? -pixels : pixels);
            }

            // the edge line, at the pointer's column (upstream: the first or last line of the viewport)
            var viewport = layout.GetLinesViewportData();
            var edgeLine = Math.Clamp(above ? viewport.StartLineNumber : viewport.EndLineNumber, 1, _viewModel.GetLineCount());
            var y = GetY(edgeLine) + (_lineHeight / 2);
            var target = GetMouseTarget(new Point(Math.Clamp(point.X, 0, width), y));
            if (target.ViewPosition.LineNumber != edgeLine)
            {
                var column = above ? 1 : _viewModel.GetLineMaxColumn(edgeLine);
                var viewPosition = new TextPosition(edgeLine, column);
                target = target with
                {
                    ViewPosition = viewPosition,
                    Position = _viewModel.CoordinatesConverter.ConvertViewPositionToModelPosition(viewPosition),
                };
            }

            DispatchDrag(target, NavigationCommandRevealType.None);
        }
        else if (point.X < 0 || point.X > width)
        {
            var left = point.X < 0;
            var distance = left ? -point.X : point.X - width;
            var pixels = GetScrollSpeed(distance, Math.Max(0, width - GutterWidth), _charWidth) * elapsed * _charWidth;
            if (pixels > 0)
            {
                layout.DeltaScrollNow(left ? -pixels : pixels, 0);
            }

            var x = left ? (_startedOnLineNumbers ? 0 : GutterWidth) : width;
            DispatchDrag(GetMouseTarget(new Point(x, Math.Clamp(point.Y, 0, height))), NavigationCommandRevealType.None);
        }
        else
        {
            StopAutoScroll();
            return;
        }

        InvalidateVisual();
    }

    // TopBottomDragScrolling._getScrollSpeed: units (lines, or columns sideways) per second, faster the further out the
    // pointer is.
    private static double GetScrollSpeed(double outsideDistance, double viewportSize, double unit)
    {
        var viewportInUnits = viewportSize / unit;
        var outsideDistanceInUnits = outsideDistance / unit;
        if (outsideDistanceInUnits <= 1.5)
        {
            return Math.Max(30, viewportInUnits * (1 + outsideDistanceInUnits));
        }

        if (outsideDistanceInUnits <= 3)
        {
            return Math.Max(60, viewportInUnits * (2 + outsideDistanceInUnits));
        }

        return Math.Max(200, viewportInUnits * (7 + outsideDistanceInUnits));
    }
}
