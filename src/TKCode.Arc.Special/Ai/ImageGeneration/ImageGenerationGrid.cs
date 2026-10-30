using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The 2 × 2 grid: four tiles in the batch's shape, as wide as the card and never taller than
/// <see cref="MaxGridHeight"/> (a capped grid narrows and stays centred), plus an overlay over the whole grid (the
/// suggestions) and a close button in its corner. The shape springs when a batch of another shape starts, while a resize of
/// the container cuts straight to the new size. Expanding grows one tile from its cell to the whole grid on a
/// bounce-free 240ms spring on position and size, as the others fade out; closing reverses it. Both are code-driven
/// because the travel is between two measured layout rectangles, which a ControlTheme transition cannot express; only
/// this panel re-arranges while they move.
/// </summary>
internal sealed class ImageGenerationGrid : Panel
{
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<ImageGenerationGrid, double>(nameof(Gap), 8);

    public static readonly StyledProperty<double> MaxGridHeightProperty =
        AvaloniaProperty.Register<ImageGenerationGrid, double>(nameof(MaxGridHeight), 420);

    /// <summary>The expand travel: 240ms visual duration, no bounce.</summary>
    internal static readonly SpringSpec ExpandSpring = SpringSpec.FromVisual(0.24, 0);

    /// <summary>The shape change between batches.</summary>
    internal static readonly SpringSpec ShapeSpring = SpringSpec.FromVisual(0.3, 0);

    private readonly List<ImageGenerationTile> _tiles = [];
    private MotionValue<double>? _ratio;
    private MotionValue<double>? _expand;
    private double _ratioValue = 1;
    private double _expandValue;
    private int _expanded = -1;
    private Control? _overlay;
    private Control? _close;

    static ImageGenerationGrid()
    {
        AffectsMeasure<ImageGenerationGrid>(GapProperty, MaxGridHeightProperty);
    }

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    public double MaxGridHeight { get => GetValue(MaxGridHeightProperty); set => SetValue(MaxGridHeightProperty, value); }

    public IReadOnlyList<ImageGenerationTile> Tiles => _tiles;

    /// <summary>The tile that is expanded (or closing), or −1.</summary>
    public int ExpandedIndex => _expanded;

    /// <summary>How far the expanded tile has grown, 0 to 1.</summary>
    public double ExpandProgress => _expandValue;

    /// <summary>The shape on screen (height over width).</summary>
    public double Ratio => _ratioValue;

    /// <summary>Where the grid's tiles sit inside the panel (it narrows when capped).</summary>
    public Rect GridRect { get; private set; }

    public void AddTile(ImageGenerationTile tile)
    {
        _tiles.Add(tile);
        Children.Add(tile);
    }

    /// <summary>The control laid over the whole grid (the suggestions).</summary>
    public Control? Overlay
    {
        get => _overlay;
        set
        {
            if (_overlay is not null)
            {
                Children.Remove(_overlay);
            }
            _overlay = value;
            if (value is not null)
            {
                Children.Add(value);
            }
        }
    }

    /// <summary>The close button, in the expanded image's top-left corner.</summary>
    public Control? CloseButton
    {
        get => _close;
        set
        {
            if (_close is not null)
            {
                Children.Remove(_close);
            }
            _close = value;
            if (value is not null)
            {
                value.Opacity = 0;
                value.IsVisible = false;
                value.ZIndex = 2;
                Children.Add(value);
            }
        }
    }

    /// <summary>Moves to a shape: springs when <paramref name="animate"/>, else cuts.</summary>
    public void SetRatio(double ratio, bool animate)
    {
        ratio = double.IsFinite(ratio) && ratio > 0 ? ratio : 1;
        if (!animate || !IsMeasureValid && Bounds.Width <= 0)
        {
            _ratio?.Jump(ratio);
            _ratioValue = ratio;
            InvalidateMeasure();
            return;
        }
        if (_ratio is null)
        {
            _ratio = new MotionValue<double>(this, _ratioValue, MotionVectors.Double);
            _ratio.Changed += value =>
            {
                _ratioValue = value;
                InvalidateMeasure();
            };
        }
        if (Math.Abs(_ratio.Target - ratio) > 1e-6 || Math.Abs(_ratioValue - ratio) > 1e-6)
        {
            _ = _ratio.AnimateTo(ratio, MotionSpec.Spring(ShapeSpring));
        }
    }

    /// <summary>Grows a tile to the whole grid.</summary>
    public void Expand(int index)
    {
        if (index < 0 || index >= _tiles.Count)
        {
            return;
        }
        if (_expanded != index && _expanded >= 0)
        {
            Settle(0);
        }
        _expanded = index;
        for (var i = 0; i < _tiles.Count; i++)
        {
            _tiles[i].ZIndex = i == index ? 1 : 0;
            // The grid behind the expanded image is inert: no pointer, no focus.
            _tiles[i].IsHitTestVisible = i == index;
            _tiles[i].Focusable = i == index;
        }
        if (_close is not null)
        {
            _close.IsVisible = true;
        }
        Animate(1);
    }

    /// <summary>Shrinks the expanded tile back into its cell.</summary>
    public void Collapse()
    {
        if (_expanded < 0)
        {
            return;
        }
        foreach (var tile in _tiles)
        {
            tile.IsHitTestVisible = true;
            tile.Focusable = true;
        }
        Animate(0);
    }

    private void Animate(double target)
    {
        if (_expand is null)
        {
            _expand = new MotionValue<double>(this, _expandValue, MotionVectors.Double);
            _expand.Changed += value =>
            {
                _expandValue = value;
                ApplyExpand();
            };
        }
        var expanding = target > 0;
        var travel = _expand.AnimateTo(target, MotionSpec.Spring(ExpandSpring));
        if (_expand.IsAnimating && !expanding)
        {
            SettleAfter(travel);
        }
        if (!_expand.IsAnimating)
        {
            // Motion off or reduced, or not on screen: the value is already there.
            _expandValue = target;
            ApplyExpand();
            if (!expanding)
            {
                Settle(0);
            }
        }
    }

    private async void SettleAfter(Task<bool> travel)
    {
        if (await travel && _expand is { Target: <= 0 })
        {
            Settle(0);
        }
    }

    /// <summary>Raised when an expanded tile has shrunk back into its cell.</summary>
    public event EventHandler? Collapsed;

    private void Settle(double value)
    {
        _expand?.Jump(value);
        _expandValue = value;
        foreach (var tile in _tiles)
        {
            tile.ZIndex = 0;
            tile.Opacity = 1;
            tile.IsHitTestVisible = true;
            tile.Focusable = true;
        }
        if (value <= 0)
        {
            var was = _expanded;
            _expanded = -1;
            if (was >= 0)
            {
                Collapsed?.Invoke(this, EventArgs.Empty);
            }
            if (_close is not null)
            {
                _close.IsVisible = false;
                _close.Opacity = 0;
            }
        }
        InvalidateArrange();
    }

    private void ApplyExpand()
    {
        for (var i = 0; i < _tiles.Count; i++)
        {
            _tiles[i].Opacity = i == _expanded || _expanded < 0 ? 1 : 1 - _expandValue;
        }
        if (_close is not null)
        {
            _close.Opacity = Math.Clamp(_expandValue, 0, 1);
        }
        InvalidateArrange();
    }

    /// <summary>The grid's size for a width: two tiles and a gap each way, capped in height.</summary>
    internal static Size GridSize(double width, double ratio, double gap, double maxHeight)
    {
        width = Math.Max(0, width);
        var tileWidth = Math.Max(0, (width - gap) / 2);
        var tileHeight = tileWidth * ratio;
        var height = tileHeight * 2 + gap;
        if (height > maxHeight && maxHeight > gap)
        {
            tileHeight = (maxHeight - gap) / 2;
            tileWidth = tileHeight / ratio;
            return new Size(tileWidth * 2 + gap, maxHeight);
        }
        return new Size(width, height);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 480;
        var size = GridSize(width, _ratioValue, Gap, MaxGridHeight);
        var tile = new Size(Math.Max(0, (size.Width - Gap) / 2), Math.Max(0, (size.Height - Gap) / 2));
        for (var i = 0; i < _tiles.Count; i++)
        {
            _tiles[i].Measure(i == _expanded ? size : tile);
        }
        _overlay?.Measure(size);
        _close?.Measure(size);
        return new Size(width, size.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = GridSize(finalSize.Width, _ratioValue, Gap, MaxGridHeight);
        var grid = new Rect((finalSize.Width - size.Width) / 2, 0, size.Width, size.Height);
        GridRect = grid;
        var tileWidth = Math.Max(0, (size.Width - Gap) / 2);
        var tileHeight = Math.Max(0, (size.Height - Gap) / 2);
        for (var i = 0; i < _tiles.Count; i++)
        {
            var cell = new Rect(grid.X + i % 2 * (tileWidth + Gap), grid.Y + i / 2 * (tileHeight + Gap), tileWidth, tileHeight);
            if (i == _expanded)
            {
                var t = _expandValue;
                cell = new Rect(cell.X + (grid.X - cell.X) * t, cell.Y + (grid.Y - cell.Y) * t,
                    cell.Width + (grid.Width - cell.Width) * t, cell.Height + (grid.Height - cell.Height) * t);
            }
            _tiles[i].Arrange(cell);
        }
        _overlay?.Arrange(grid);
        if (_close is not null)
        {
            var desired = _close.DesiredSize;
            // Top-left, clear of the picked image's check badge in the top-right corner.
            _close.Arrange(new Rect(grid.X + 8, grid.Y + 8, desired.Width, desired.Height));
        }
        return finalSize;
    }

    /// <summary>Arrow keys move focus between the tiles (2 × 2); Enter and Space are the tile's own.</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || _expanded >= 0 || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        var focused = _tiles.FindIndex(t => t.IsFocused);
        if (focused < 0)
        {
            return;
        }
        var next = e.Key switch
        {
            Key.Left => focused % 2 == 1 ? focused - 1 : focused,
            Key.Right => focused % 2 == 0 ? focused + 1 : focused,
            Key.Up => focused >= 2 ? focused - 2 : focused,
            Key.Down => focused < 2 ? focused + 2 : focused,
            Key.Home => 0,
            Key.End => _tiles.Count - 1,
            _ => -1,
        };
        if (next < 0)
        {
            return;
        }
        e.Handled = true;
        if (next != focused && next < _tiles.Count)
        {
            _tiles[next].Focus(NavigationMethod.Directional);
        }
    }
}
