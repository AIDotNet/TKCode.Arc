using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc.Primitives;

/// <summary>
/// The element a <see cref="ModalPresence"/> mounts on the window's overlay layer: a full-size scrim and the panel
/// host above it. The panel is hosted visually only (it keeps its own logical parent, so styles, resources and
/// DataContext keep flowing from where it was declared). Empty areas of the panel host are not hit-testable, so
/// presses there reach the scrim.
/// </summary>
public sealed class ModalHost : Control
{
    private readonly Border _scrim;
    private readonly ModalPanelHost _panelHost;

    internal ModalHost(ModalPresence presence)
    {
        Presence = presence;
        _scrim = new Border { Opacity = 0 };
        ArcTokens.Bind(_scrim, Border.BackgroundProperty, ArcDesignTokenKind.Scrim);
        _panelHost = new ModalPanelHost();
        LogicalChildren.Add(_scrim);
        LogicalChildren.Add(_panelHost);
        VisualChildren.Add(_scrim);
        VisualChildren.Add(_panelHost);
        _scrim.PointerPressed += OnScrimPressed;
    }

    /// <summary>The modal this host belongs to.</summary>
    public ModalPresence Presence { get; }

    /// <summary>The scrim (a full-size <see cref="Border"/>); its opacity follows the modal's scrim motion.</summary>
    public Border Scrim => _scrim;

    /// <summary>The element that carries the panel's enter / exit transform and opacity.</summary>
    public Control PanelHost => _panelHost;

    internal ModalPanelHost PanelHostCore => _panelHost;

    internal Size LayerSize { get; set; }

    private void OnScrimPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(_scrim).Properties.IsLeftButtonPressed || e.Pointer.Type != PointerType.Mouse)
        {
            e.Handled = true;
            Presence.OnScrimPressed();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = Resolve(availableSize);
        _scrim.Measure(size);
        _panelHost.Measure(size);
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var rect = new Rect(finalSize);
        _scrim.Arrange(rect);
        _panelHost.Arrange(rect);
        return finalSize;
    }

    private Size Resolve(Size available)
    {
        var width = double.IsInfinity(available.Width) ? LayerSize.Width : available.Width;
        var height = double.IsInfinity(available.Height) ? LayerSize.Height : available.Height;
        return new Size(double.IsFinite(width) ? width : 0, double.IsFinite(height) ? height : 0);
    }
}

/// <summary>Hosts the panel visually, aligns it with its own alignment and margin, and applies the presence transform.</summary>
internal sealed class ModalPanelHost : Control
{
    private Control? _panel;
    private readonly MatrixTransform _transform = new();
    private Vector _offset;
    private double _scale = 1;

    public ModalPanelHost()
    {
        RenderTransform = _transform;
        RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Absolute);
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Cycle);
        // Focusable only as the last-resort initial focus target; never a Tab stop of its own.
        KeyboardNavigation.SetIsTabStop(this, false);
        Focusable = true;
        FocusAdorner = null;
    }

    public Control? Panel
    {
        get => _panel;
        set
        {
            if (ReferenceEquals(_panel, value))
            {
                return;
            }
            if (_panel is not null)
            {
                VisualChildren.Remove(_panel);
            }
            _panel = value;
            if (value is not null)
            {
                VisualChildren.Add(value);
            }
            InvalidateMeasure();
        }
    }

    public void SetTransform(Vector offset, double scale)
    {
        if (offset == _offset && scale == _scale)
        {
            return;
        }
        _offset = offset;
        _scale = scale;
        UpdateMatrix();
    }

    private void UpdateMatrix()
    {
        // Scale around the panel's centre, then translate.
        var center = _panel?.Bounds.Center ?? new Point(Bounds.Width / 2, Bounds.Height / 2);
        _transform.Matrix = Matrix.CreateTranslation(-center.X, -center.Y) * Matrix.CreateScale(_scale, _scale) *
                            Matrix.CreateTranslation(center.X + _offset.X, center.Y + _offset.Y);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _panel?.Measure(availableSize);
        return new Size(double.IsFinite(availableSize.Width) ? availableSize.Width : 0,
            double.IsFinite(availableSize.Height) ? availableSize.Height : 0);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _panel?.Arrange(new Rect(finalSize));
        UpdateMatrix();
        return finalSize;
    }
}
