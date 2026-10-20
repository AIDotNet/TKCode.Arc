using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>
/// A pill-shaped single choice (All / Ready / Planned, Light / Dark): the selected segment sits on a raised
/// surface that glides between segments (P7). Segments are Arc ghost buttons, so keyboard and focus come for free.
/// </summary>
public sealed class GallerySegmented : Border
{
    private readonly List<Button> _segments = new();
    private readonly Grid _grid = new();
    private readonly Panel _host = new();
    private readonly GlidingIndicator _indicator = new() { IsHitTestVisible = false, Spring = ArcSpring.Snappy };
    private readonly List<IDisposable?> _selectedForeground = new();
    private Rect _lastRect;
    private int _selectedIndex = -1;

    public GallerySegmented(params string[] labels)
    {
        for (var i = 0; i < labels.Length; i++)
        {
            var index = i;
            var segment = new Button
            {
                Content = labels[i],
                Variant = ButtonVariant.Ghost,
                Size = ControlSize.Small,
                MinHeight = 30,
                Padding = new Thickness(12, 0),
                FontSize = 13,
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(999),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                IsPressScaleEnabled = false
            };
            segment.Click += (_, _) => Select(index);
            _grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            Grid.SetColumn(segment, i);
            _grid.Children.Add(segment);
            _segments.Add(segment);
            _selectedForeground.Add(null);
        }

        ArcTokens.Bind(_indicator, GlidingIndicator.FillProperty, ArcDesignTokenKind.Surface);
        ArcTokens.Bind(_indicator, GlidingIndicator.StrokeProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(_indicator, GlidingIndicator.BoxShadowProperty, ArcDesignTokenKind.ShadowResting);
        _indicator.StrokeThickness = 1;
        _indicator.CornerRadius = new CornerRadius(999);
        _host.Children.Add(_indicator);
        _host.Children.Add(_grid);
        _host.LayoutUpdated += (_, _) => Sync(animate: false);

        Padding = new Thickness(3);
        CornerRadius = new CornerRadius(999);
        BorderThickness = new Thickness(1);
        Child = _host;
        ArcTokens.Bind(this, BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
        ArcTokens.Bind(this, BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        Select(0, raise: false);
    }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set => Select(value, raise: false);
    }

    public event Action<int>? SelectionChanged;

    private void Select(int index, bool raise = true)
    {
        if (index == _selectedIndex || index < 0 || index >= _segments.Count)
        {
            return;
        }
        var first = _selectedIndex < 0;
        _selectedIndex = index;
        for (var i = 0; i < _segments.Count; i++)
        {
            _selectedForeground[i]?.Dispose();
            _selectedForeground[i] = i == index
                ? ArcTokens.Bind(_segments[i], Avalonia.Controls.Primitives.TemplatedControl.ForegroundProperty, ArcDesignTokenKind.Foreground, BindingPriority.LocalValue)
                : null;
        }
        Sync(animate: !first);
        if (raise)
        {
            SelectionChanged?.Invoke(index);
        }
    }

    private void Sync(bool animate)
    {
        if (_selectedIndex < 0 || _segments[_selectedIndex].Bounds.Width <= 0)
        {
            return;
        }
        var segment = _segments[_selectedIndex];
        if (segment.TranslatePoint(default, _host) is not { } origin)
        {
            return;
        }
        var rect = new Rect(origin, segment.Bounds.Size);
        if (!animate && rect == _lastRect)
        {
            return;
        }
        _lastRect = rect;
        _indicator.MoveTo(rect, animate);
    }
}
