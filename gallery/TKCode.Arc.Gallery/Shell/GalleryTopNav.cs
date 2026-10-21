using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Localization;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>
/// The menu in the middle of the top bar: Components, Blocks, Docs and Changelog as a pill. The item of the open page
/// is marked by a surface that glides between items; the home page marks none. Unlike <see cref="GallerySegmented"/>
/// it is not a choice: every click raises <see cref="Chosen"/>, even on the marked item.
/// </summary>
public sealed class GalleryTopNav : Border
{
    private readonly List<Button> _items = new();
    private readonly List<IDisposable?> _markedForeground = new();
    private readonly Grid _grid = new();
    private readonly Panel _host = new();
    private readonly GlidingIndicator _indicator = new() { IsHitTestVisible = false, Spring = ArcSpring.Snappy };
    private Rect _lastRect;
    private int _marked = -1;

    public GalleryTopNav(params GalleryShellLangResourceKind[] labels)
    {
        for (var i = 0; i < labels.Length; i++)
        {
            var index = i;
            var item = new Button
            {
                Variant = ButtonVariant.Ghost,
                Size = ControlSize.Small,
                MinHeight = 30,
                Padding = new Thickness(14, 0),
                FontSize = 13,
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(999),
                IsPressScaleEnabled = false
            };
            GalleryText.Bind(item, ContentControl.ContentProperty, labels[i]);
            item.Click += (_, _) => Chosen?.Invoke(index);
            _grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            Grid.SetColumn(item, i);
            _grid.Children.Add(item);
            _items.Add(item);
            _markedForeground.Add(null);
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
        VerticalAlignment = VerticalAlignment.Center;
        Child = _host;
        ArcTokens.Bind(this, BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
        ArcTokens.Bind(this, BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
    }

    /// <summary>The index of the marked item, or -1 for none.</summary>
    public int MarkedIndex => _marked;

    /// <summary>Raised with the index of the item that was clicked.</summary>
    public event Action<int>? Chosen;

    /// <summary>Marks the item at <paramref name="index"/>, or none for -1.</summary>
    public void Mark(int index)
    {
        if (index == _marked)
        {
            return;
        }
        var first = _marked < 0;
        _marked = index;
        for (var i = 0; i < _items.Count; i++)
        {
            _markedForeground[i]?.Dispose();
            _markedForeground[i] = i == index
                ? ArcTokens.Bind(_items[i], Avalonia.Controls.Primitives.TemplatedControl.ForegroundProperty, ArcDesignTokenKind.Foreground, BindingPriority.LocalValue)
                : null;
        }
        if (index < 0)
        {
            _indicator.Hide(animate: true);
            _lastRect = default;
            return;
        }
        Sync(animate: !first);
    }

    private void Sync(bool animate)
    {
        if (_marked < 0 || _items[_marked].Bounds.Width <= 0)
        {
            return;
        }
        var item = _items[_marked];
        if (item.TranslatePoint(default, _host) is not { } origin)
        {
            return;
        }
        var rect = new Rect(origin, item.Bounds.Size);
        if (!animate && rect == _lastRect)
        {
            return;
        }
        _lastRect = rect;
        _indicator.MoveTo(rect, animate);
    }
}
