using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// Pointer plumbing of the vertical drag-to-dismiss gesture (Arc Sheet stack, design reference): mouse, pen and touch alike.
/// A press accepted by <see cref="CanBegin"/> becomes a drag once it has moved more than 6 px vertically (then the pointer
/// is captured, which cancels a button press under it); <see cref="Moved"/> reports the raw offset (1:1, positive down)
/// and <see cref="Released"/> the release velocity of <see cref="SheetStackDragModel"/>. Self-contained so another
/// surface with a pull-down-to-close panel can reuse it.
/// </summary>
internal sealed class SheetStackDragGesture : IDisposable
{
    private readonly Control _target;
    private readonly SheetStackDragModel _model = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private IPointer? _pointer;
    private PointerType _pointerType;
    private bool _useEventTime;
    private ulong _origin;
    private bool _rejected;

    public SheetStackDragGesture(Control target)
    {
        _target = target;
        target.AddHandler(InputElement.PointerPressedEvent, OnPressed, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
        target.AddHandler(InputElement.PointerMovedEvent, OnMoved, RoutingStrategies.Bubble, true);
        target.AddHandler(InputElement.PointerReleasedEvent, OnReleased, RoutingStrategies.Bubble, true);
        target.AddHandler(InputElement.PointerCaptureLostEvent, OnCaptureLost, RoutingStrategies.Direct | RoutingStrategies.Bubble, true);
    }

    /// <summary>Decides on press whether this press may become a drag.</summary>
    public Func<PointerPressedEventArgs, bool>? CanBegin { get; set; }

    /// <summary>
    /// Asked when the slop is passed with the pointer type and the signed travel; return false to leave the gesture to
    /// someone else (a touch swipe up in a scrollable body scrolls it).
    /// </summary>
    public Func<PointerType, double, bool>? CanStart { get; set; }

    public bool IsDragging => _model.IsDragging;

    public PointerType PointerType => _pointerType;

    public event Action? Started;

    public event Action<double>? Moved;

    public event Action<double>? Released;

    public event Action? Cancelled;

    /// <summary>Abandons a drag in progress (the owner springs back).</summary>
    public void Cancel()
    {
        if (!_model.IsPressed)
        {
            return;
        }
        var wasDragging = _model.IsDragging;
        _model.Cancel();
        ReleaseCapture();
        if (wasDragging)
        {
            Cancelled?.Invoke();
        }
    }

    private double Now(PointerEventArgs e)
    {
        if (_useEventTime && e.Timestamp >= _origin)
        {
            return (e.Timestamp - _origin) / 1000.0;
        }
        _useEventTime = false;
        return _clock.Elapsed.TotalSeconds;
    }

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Route == RoutingStrategies.Bubble || _model.IsDragging)
        {
            return;
        }
        if (_model.IsPressed)
        {
            _model.Cancel();
            ReleaseCapture();
        }
        var point = e.GetCurrentPoint(_target);
        if (e.Pointer.Type == PointerType.Mouse && !point.Properties.IsLeftButtonPressed)
        {
            return;
        }
        if (CanBegin is { } canBegin && !canBegin(e))
        {
            return;
        }
        _pointer = e.Pointer;
        _pointerType = e.Pointer.Type;
        _rejected = false;
        _useEventTime = e.Timestamp != 0;
        _origin = e.Timestamp;
        _clock.Restart();
        _model.Press(point.Position.Y, 0);
    }

    private void OnMoved(object? sender, PointerEventArgs e)
    {
        if (!ReferenceEquals(e.Pointer, _pointer) || !_model.IsPressed || _rejected)
        {
            return;
        }
        if (e.Pointer.Type == PointerType.Mouse && !e.GetCurrentPoint(_target).Properties.IsLeftButtonPressed)
        {
            // The button came up outside the window.
            Finish(e);
            return;
        }
        // Positions are taken in the target's space, which does not move with the panel.
        var y = e.GetPosition(_target).Y;
        var started = _model.Move(y, Now(e));
        if (started)
        {
            if (CanStart is { } canStart && !canStart(_pointerType, _model.Travel))
            {
                _model.Cancel();
                _rejected = true;
                _pointer = null;
                return;
            }
            e.Pointer.Capture(_target);
            e.PreventGestureRecognition();
            Started?.Invoke();
        }
        if (_model.IsDragging)
        {
            e.Handled = true;
            Moved?.Invoke(_model.RawOffset);
        }
    }

    private void OnReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!ReferenceEquals(e.Pointer, _pointer) || !_model.IsPressed)
        {
            return;
        }
        if (_model.IsDragging)
        {
            e.Handled = true;
        }
        Finish(e);
    }

    private void Finish(PointerEventArgs e)
    {
        var wasDragging = _model.IsDragging;
        if (wasDragging)
        {
            _model.Move(e.GetPosition(_target).Y, Now(e));
        }
        var velocity = _model.Release(Now(e));
        ReleaseCapture();
        if (wasDragging)
        {
            Released?.Invoke(velocity);
        }
    }

    private void OnCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (!ReferenceEquals(e.Pointer, _pointer) || !_model.IsDragging)
        {
            return;
        }
        _model.Cancel();
        _pointer = null;
        Cancelled?.Invoke();
    }

    private void ReleaseCapture()
    {
        var pointer = _pointer;
        _pointer = null;
        if (pointer is not null && ReferenceEquals(pointer.Captured, _target))
        {
            pointer.Capture(null);
        }
    }

    public void Dispose()
    {
        Cancel();
        _target.RemoveHandler(InputElement.PointerPressedEvent, OnPressed);
        _target.RemoveHandler(InputElement.PointerMovedEvent, OnMoved);
        _target.RemoveHandler(InputElement.PointerReleasedEvent, OnReleased);
        _target.RemoveHandler(InputElement.PointerCaptureLostEvent, OnCaptureLost);
    }
}
