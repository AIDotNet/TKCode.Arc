using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The bottom-sheet presentation of a <see cref="MenuSurface"/> (<see cref="MenuSurfaceVariant.Sheet"/>): sizes the
/// surface to the window (full width, at most 88% tall), fades a scrim in behind it and lets the sheet follow a
/// downward drag (elastic 0.9 down, 0.04 up) that closes it past 80px or a 500px/s flick and otherwise springs home.
/// The scrim lives in the window's overlay (or adorner) layer and the drag moves a drawn translation, so both are code-driven:
/// neither is a template part a ControlTheme transition could reach. Dragging is off under reduced motion.
/// </summary>
internal sealed class MenuSurfaceSheet
{
    private const double HeightRatio = 0.88;
    private const double CloseDistance = 80;
    private const double CloseVelocity = 500;

    private readonly MenuSurface _surface;
    private readonly TranslateTransform _translate = new();
    private Border? _scrim;
    private Panel? _layer;
    private MotionValue<double>? _scrimOpacity;
    private MotionValue<double>? _offset;
    private IPointer? _pointer;
    private double _startY;
    private double _lastY;
    private ulong _lastTimestamp;
    private double _velocity;
    private bool _active;
    private TopLevel? _topLevel;

    public MenuSurfaceSheet(MenuSurface surface)
    {
        _surface = surface;
        surface.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed);
        surface.AddHandler(InputElement.PointerMovedEvent, OnPointerMoved);
        surface.AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased);
        surface.AddHandler(InputElement.PointerCaptureLostEvent, OnCaptureLost);
    }

    /// <summary>Current drag offset (tests).</summary>
    internal double DragOffset => _translate.Y;

    /// <summary>Sizes the surface for the anchor's window and shows the scrim (before the popup opens).</summary>
    public void Prepare(Control anchor, TopLevel topLevel)
    {
        _active = true;
        if (!ReferenceEquals(_topLevel, topLevel))
        {
            if (_topLevel is not null)
            {
                _topLevel.SizeChanged -= OnWindowResized;
            }
            _topLevel = topLevel;
            topLevel.SizeChanged += OnWindowResized;
        }
        Fit(topLevel);
        _surface.RenderTransform = _translate;
        _offset?.Jump(0);
        _translate.Y = 0;
        ShowScrim(anchor, topLevel);
    }

    private void Fit(TopLevel topLevel)
    {
        var client = topLevel.ClientSize;
        _surface.Width = client.Width;
        _surface.MaxHeight = Math.Max(0, client.Height * HeightRatio);
        if (_scrim is not null)
        {
            _scrim.Width = client.Width;
            _scrim.Height = client.Height;
        }
    }

    // The sheet is fixed to the window: it follows resizes instead of closing.
    private void OnWindowResized(object? sender, SizeChangedEventArgs e)
    {
        if (_active && _surface.IsOpen && _topLevel is { } topLevel)
        {
            Fit(topLevel);
            _surface.Popup?.Reposition();
        }
    }

    /// <summary>Returns the surface to its themed size (the next open is not a sheet).</summary>
    public void Release()
    {
        if (_active)
        {
            _ = HideScrimAsync();
        }
        _active = false;
        if (_topLevel is not null)
        {
            _topLevel.SizeChanged -= OnWindowResized;
            _topLevel = null;
        }
        _surface.ClearValue(Layoutable.WidthProperty);
        _surface.ClearValue(Layoutable.MaxHeightProperty);
        if (ReferenceEquals(_surface.RenderTransform, _translate))
        {
            _surface.ClearValue(Visual.RenderTransformProperty);
        }
    }

    /// <summary>The sheet started closing: fade the scrim out.</summary>
    public void OnClosing() => _ = HideScrimAsync();

    /// <summary>The popup closed: drop any leftover drag offset.</summary>
    public void OnClosed()
    {
        _pointer = null;
        _offset?.Jump(0);
        _translate.Y = 0;
    }

    private void ShowScrim(Control anchor, TopLevel topLevel)
    {
        // The overlay layer when the window has one, else the adorner layer: both sit above the content and below popups.
        var layer = (Panel?)OverlayLayer.GetOverlayLayer(anchor) ?? AdornerLayer.GetAdornerLayer(anchor);
        if (layer is null)
        {
            return;
        }
        if (_scrim is null)
        {
            _scrim = new Border { IsHitTestVisible = true };
            ArcTokens.Bind(_scrim, Border.BackgroundProperty, ArcDesignTokenKind.Scrim);
            _scrimOpacity = new MotionValue<double>(_scrim, 0, MotionVectors.Double);
            _scrimOpacity.Changed += value =>
            {
                if (_scrim is not null)
                {
                    _scrim.Opacity = value;
                }
            };
        }
        if (!ReferenceEquals(_layer, layer))
        {
            _layer?.Children.Remove(_scrim);
            _layer = layer;
        }
        if (!layer.Children.Contains(_scrim))
        {
            layer.Children.Add(_scrim);
        }
        _scrim.Width = layer.Bounds.Width > 0 ? layer.Bounds.Width : topLevel.ClientSize.Width;
        _scrim.Height = layer.Bounds.Height > 0 ? layer.Bounds.Height : topLevel.ClientSize.Height;
        _scrim.Opacity = 0;
        _scrimOpacity!.Jump(0);
        var level = ArcMotion.GetEffectiveLevel(_surface.PlacementTarget ?? (AvaloniaObject)_surface);
        if (level == ArcMotionLevel.Off)
        {
            _scrimOpacity.Jump(1);
        }
        else
        {
            _ = _scrimOpacity.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(level == ArcMotionLevel.Full ? 200 : 100), ArcEasings.Standard));
        }
    }

    private async Task HideScrimAsync()
    {
        if (_scrim is not { } scrim || _scrimOpacity is not { } opacity || _layer is not { } layer)
        {
            return;
        }
        var level = ArcMotion.GetEffectiveLevel(_surface.PlacementTarget ?? (AvaloniaObject)_surface);
        if (level != ArcMotionLevel.Off &&
            !await opacity.AnimateTo(0, MotionSpec.Tween(TimeSpan.FromMilliseconds(level == ArcMotionLevel.Full ? 200 : 100), ArcEasings.Standard)))
        {
            // Interrupted by a reopen: keep the scrim.
            return;
        }
        opacity.Jump(0);
        layer.Children.Remove(scrim);
    }

    private bool CanDrag => _active && _surface.IsOpen && ArcMotion.IsFull(_surface.PlacementTarget ?? (AvaloniaObject)_surface);

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Rows handle their own presses; the handle, header and inset start a drag.
        if (e.Handled || !CanDrag || !e.GetCurrentPoint(_surface).Properties.IsLeftButtonPressed)
        {
            return;
        }
        _pointer = e.Pointer;
        e.Pointer.Capture(_surface);
        _startY = e.GetPosition(null).Y - _translate.Y;
        _lastY = e.GetPosition(null).Y;
        _lastTimestamp = e.Timestamp;
        _velocity = 0;
        _offset?.Stop();
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!ReferenceEquals(e.Pointer, _pointer))
        {
            return;
        }
        var y = e.GetPosition(null).Y;
        var delta = y - _startY;
        _translate.Y = delta > 0 ? delta * 0.9 : delta * 0.04;
        var dt = (e.Timestamp - _lastTimestamp) / 1000.0;
        if (dt > 0)
        {
            _velocity = (y - _lastY) / dt;
        }
        _lastY = y;
        _lastTimestamp = e.Timestamp;
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!ReferenceEquals(e.Pointer, _pointer))
        {
            return;
        }
        _pointer = null;
        e.Pointer.Capture(null);
        e.Handled = true;
        Settle();
    }

    private void OnCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (ReferenceEquals(e.Pointer, _pointer))
        {
            _pointer = null;
            Settle();
        }
    }

    private void Settle()
    {
        if (_translate.Y > CloseDistance || _velocity > CloseVelocity)
        {
            _surface.Close(returnFocus: true);
            return;
        }
        if (_offset is null)
        {
            _offset = new MotionValue<double>(_surface, _translate.Y, MotionVectors.Double);
            _offset.Changed += value => _translate.Y = value;
        }
        _offset.Jump(_translate.Y);
        _ = _offset.AnimateTo(0, MotionSpec.Spring(ArcSpring.Smooth));
    }
}
