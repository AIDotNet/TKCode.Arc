// The selection handles of a touch selection: two TextSelectionHandles (the base theme's teardrops, as TextBox shows
// them) under the start and the end of the selection, which a finger drags to move that end. Upstream has no handles
// (a browser's textarea brings the platform's own); this follows TextBox's touch selection in Avalonia.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc;

/// <summary>
/// The handles of a selection made by touch (a double tap or a long press). They are overlay widgets placed under the
/// selection's ends and follow scrolling; dragging one moves its end while the other stays. They go when the selection
/// becomes empty, changes by other means than the handles (keys, the mouse, typing) or a tap moves the cursor.
/// </summary>
internal sealed class CodeEditorTouchSelection
{
    private readonly CodeEditor _editor;
    private readonly CodeEditorView _view;
    private readonly HandleWidget _start;
    private readonly HandleWidget _end;
    private bool _visible;
    private bool _changing;
    private HandleWidget? _dragging;
    private Vector _grabOffset;

    public CodeEditorTouchSelection(CodeEditor editor, CodeEditorView view)
    {
        _editor = editor;
        _view = view;
        _start = new HandleWidget(this, "editor.touchSelection.start", isStart: true);
        _end = new HandleWidget(this, "editor.touchSelection.end", isStart: false);
    }

    /// <summary>Whether the handles show.</summary>
    public bool IsVisible => _visible;

    /// <summary>The handle at the start of the selection.</summary>
    internal TextSelectionHandle StartHandle => _start.Handle;

    /// <summary>The handle at the end of the selection.</summary>
    internal TextSelectionHandle EndHandle => _end.Handle;

    /// <summary>Shows the handles for the current selection (none for an empty one).</summary>
    public void Show()
    {
        if (_editor.GetSelection() is not { IsEmpty: false })
        {
            Hide();
            return;
        }

        if (!_visible)
        {
            _visible = true;
            _editor.AddOverlayWidget(_start);
            _editor.AddOverlayWidget(_end);
            _editor.DidChangeCursorSelection += OnCursorSelectionChanged;
            _editor.DidChangeModelContent += OnModelContentChanged;
        }

        _editor.LayoutWidgets();
    }

    /// <summary>Hides the handles.</summary>
    public void Hide()
    {
        if (!_visible)
        {
            return;
        }

        _visible = false;
        _dragging = null;
        _editor.DidChangeCursorSelection -= OnCursorSelectionChanged;
        _editor.DidChangeModelContent -= OnModelContentChanged;
        _editor.RemoveOverlayWidget(_start);
        _editor.RemoveOverlayWidget(_end);
    }

    /// <summary>
    /// Moves the end a handle stands for to the text under <paramref name="hotPoint"/> (the handle's tip, relative to the
    /// view: the bottom of the line it points at), keeping the other end.
    /// </summary>
    internal void DragHandleTo(bool startHandle, Point hotPoint)
    {
        if (_editor.GetSelection() is not { } selection || _editor.ViewModel is not { } viewModel)
        {
            return;
        }

        // the text the tip points at: half a line above it
        var probe = new Point(Math.Max(hotPoint.X, _view.GutterWidth), hotPoint.Y - (_view.LineHeight / 2));
        var viewPosition = _view.GetViewPositionAt(probe);
        var position = viewModel.CoordinatesConverter.ConvertViewPositionToModelPosition(viewPosition);
        var range = selection.Range;
        var anchor = startHandle ? range.End : range.Start;
        if (position == anchor)
        {
            // never collapse to nothing: the handles would go
            return;
        }

        _changing = true;
        try
        {
            _editor.SetSelection(new TextSelection(anchor, position), CodeEditorView.TouchSource);

            // the dragged end stays in view (the text scrolls under a handle dragged to the edge)
            _editor.RevealRange(new TextRange(position), VerticalRevealType.Simple, ScrollType.Immediate);
        }
        finally
        {
            _changing = false;
        }

        _editor.LayoutWidgets();
    }

    private void OnCursorSelectionChanged(CursorStateChangedEvent e)
    {
        if (_changing)
        {
            return;
        }

        if (e.Source != CodeEditorView.TouchSource || e.Selections.Count != 1 || e.Selections[0].IsEmpty)
        {
            Hide();
        }
    }

    private void OnModelContentChanged(Editor.Model.ModelContentChangedEvent e) => Hide();

    // where a handle goes: its tip under the start (pointing right) or the end (pointing left) of the selection
    private OverlayWidgetPosition GetPosition(HandleWidget widget)
    {
        var handle = widget.Handle;
        if (!_visible || _editor.GetSelection() is not { IsEmpty: false } selection)
        {
            return new OverlayWidgetPosition(null) { Coordinates = new Point(-100000, -100000) };
        }

        var isStart = ReferenceEquals(widget, _start);
        var position = isStart ? selection.Range.Start : selection.Range.End;
        if (_editor.GetScrolledVisiblePosition(position) is not { } visible)
        {
            return new OverlayWidgetPosition(null) { Coordinates = new Point(-100000, -100000) };
        }

        var tip = new Point(visible.Left, visible.Top + visible.Height);
        var width = handle.DesiredSize.Width;
        return new OverlayWidgetPosition(null)
        {
            Coordinates = isStart ? new Point(tip.X - width, tip.Y) : tip,
            StackOrdinal = 1,
        };
    }

    /// <summary>Where the tip of a handle is now, relative to the view (for tests).</summary>
    internal Point? GetHandleTip(bool startHandle)
    {
        var handle = startHandle ? _start.Handle : _end.Handle;
        if (!_visible || handle.TranslatePoint(default, _view) is not { } topLeft)
        {
            return null;
        }

        return startHandle ? new Point(topLeft.X + handle.Bounds.Width, topLeft.Y) : topLeft;
    }

    private void OnHandlePressed(HandleWidget widget, PointerPressedEventArgs e)
    {
        var isStart = ReferenceEquals(widget, _start);
        if (GetHandleTip(isStart) is not { } tip)
        {
            return;
        }

        _dragging = widget;
        _grabOffset = e.GetPosition(_view) - tip;
        e.Pointer.Capture(widget.Handle);
        e.Handled = true;
    }

    private void OnHandleMoved(HandleWidget widget, PointerEventArgs e)
    {
        if (!ReferenceEquals(_dragging, widget))
        {
            return;
        }

        DragHandleTo(ReferenceEquals(widget, _start), e.GetPosition(_view) - _grabOffset);
    }

    private void OnHandleReleased(HandleWidget widget)
    {
        if (ReferenceEquals(_dragging, widget))
        {
            _dragging = null;
        }
    }

    /// <summary>
    /// A TextSelectionHandle in the base theme's start or end look. Avalonia 12 keeps the handle's type internal (TextBox
    /// sets it), so the subclass sets the pseudo-class the theme styles and keeps TextSelectionHandle's theme.
    /// </summary>
    internal sealed class CodeEditorSelectionHandle : TextSelectionHandle
    {
        public CodeEditorSelectionHandle(bool isStart)
        {
            IsStart = isStart;
            UpdateClasses();
        }

        /// <summary>Whether it is the handle of the selection's start.</summary>
        public bool IsStart { get; }

        // TextSelectionHandle sets the pseudo-class of its internal type (a caret) here; this one's type wins. Only the
        // render transform and the indicator's alignment follow the classes, so the flip does not lay the handle out again.

        /// <inheritdoc />
        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            UpdateClasses();
        }

        /// <inheritdoc />
        protected override void OnLoaded(Avalonia.Interactivity.RoutedEventArgs e)
        {
            base.OnLoaded(e);
            UpdateClasses();
        }

        /// <inheritdoc />
        protected override void ArrangeCore(Rect finalRect)
        {
            base.ArrangeCore(finalRect);
            UpdateClasses();
        }

        private void UpdateClasses()
        {
            PseudoClasses.Set(":caret", false);
            PseudoClasses.Set(":start", IsStart);
            PseudoClasses.Set(":end", !IsStart);
        }

        /// <inheritdoc />
        protected override Type StyleKeyOverride => typeof(TextSelectionHandle);
    }

    private sealed class HandleWidget : ICodeEditorOverlayWidget
    {
        private readonly CodeEditorTouchSelection _owner;

        public HandleWidget(CodeEditorTouchSelection owner, string id, bool isStart)
        {
            _owner = owner;
            Id = id;
            Handle = new CodeEditorSelectionHandle(isStart);

            // the handle is a Thumb that captures the pointer; its moves are read relative to the view, since the handle
            // itself moves while it is dragged
            Handle.AddHandler(InputElement.PointerPressedEvent, (_, e) => _owner.OnHandlePressed(this, e), handledEventsToo: true);
            Handle.AddHandler(InputElement.PointerMovedEvent, (_, e) => _owner.OnHandleMoved(this, e), handledEventsToo: true);
            Handle.AddHandler(InputElement.PointerReleasedEvent, (_, _) => _owner.OnHandleReleased(this), handledEventsToo: true);
            Handle.AddHandler(InputElement.PointerCaptureLostEvent, (_, _) => _owner.OnHandleReleased(this), handledEventsToo: true);
        }

        public string Id { get; }

        public TextSelectionHandle Handle { get; }

        public Control Control => Handle;

        public OverlayWidgetPosition GetPosition() => _owner.GetPosition(this);
    }
}
