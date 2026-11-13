using System.Collections;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Metadata;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// Photos laid out edge to edge, either in justified rows that all share one height and fill the width exactly, or in
/// masonry columns. Every photo keeps its own proportions. Selecting a photo (a click, Enter or Space) opens it wide on a
/// row of its own and the rest flow around it; when the grid is resized, photos are added or removed, or the layout
/// changes, every photo glides from where it was to its new place instead of jumping.
/// </summary>
/// <remarks>
/// Photos come from <see cref="Items"/> (written inline) or <see cref="ItemsSource"/>, whose elements may be
/// <see cref="PhotoGridPhoto"/>s, <see cref="ImageSource"/>s or addresses. Give <see cref="PhotoGridPhoto.AspectRatio"/>
/// when you know it: the layout is then final before the pictures arrive. Each photo is a light control of its own
/// (not virtualized), so the grid suits a few hundred photos; page longer sets. Under reduced motion photos fade into
/// their new places instead of gliding; with motion off they move at once.
/// </remarks>
public class PhotoGrid : Control
{
    /// <summary>The photos written inline, used while <see cref="ItemsSource"/> is null.</summary>
    public static readonly DirectProperty<PhotoGrid, AvaloniaList<PhotoGridPhoto>> ItemsProperty =
        AvaloniaProperty.RegisterDirect<PhotoGrid, AvaloniaList<PhotoGridPhoto>>(nameof(Items), o => o.Items);

    /// <summary>
    /// The photos to show: <see cref="PhotoGridPhoto"/>s, <see cref="ImageSource"/>s or addresses. Wins over
    /// <see cref="Items"/>; a collection that reports changes is followed, and its photos glide as it changes.
    /// </summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<PhotoGrid, IEnumerable?>(nameof(ItemsSource));

    /// <summary>Justified rows (default) or masonry columns.</summary>
    public static readonly StyledProperty<PhotoGridLayout> LayoutProperty =
        AvaloniaProperty.Register<PhotoGrid, PhotoGridLayout>(nameof(Layout));

    /// <summary>The height a justified row aims for (default from the theme, 200); rows end up within about a third of it.</summary>
    public static readonly StyledProperty<double> RowHeightProperty =
        AvaloniaProperty.Register<PhotoGrid, double>(nameof(RowHeight), 200);

    /// <summary>The narrowest a masonry column gets before the grid drops a column (default from the theme, 220).</summary>
    public static readonly StyledProperty<double> ColumnWidthProperty =
        AvaloniaProperty.Register<PhotoGrid, double>(nameof(ColumnWidth), 220);

    /// <summary>Space between photos, both ways (default from the theme, 6).</summary>
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<PhotoGrid, double>(nameof(Spacing), 6);

    /// <summary>The selected photo, which opens wide on its own row; -1 for none. Two-way.</summary>
    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<PhotoGrid, int>(nameof(SelectedIndex), -1, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Raised when <see cref="SelectedIndex"/> changes, whether a photo was opened or closed or the index was set in code or through a binding.
    /// </summary>
    public static readonly RoutedEvent<PhotosSelectionChangedEventArgs> SelectionChangedEvent =
        RoutedEvent.Register<PhotoGrid, PhotosSelectionChangedEventArgs>(nameof(SelectionChanged), RoutingStrategies.Bubble);

    /// <summary>The tallest the selected photo is drawn (default from the theme, 520).</summary>
    public static readonly StyledProperty<double> SelectedMaxHeightProperty =
        AvaloniaProperty.Register<PhotoGrid, double>(nameof(SelectedMaxHeight), 520);

    /// <summary>Whether a click, Enter or Space selects a photo (default true). Off, the grid is a plain layout.</summary>
    public static readonly StyledProperty<bool> IsSelectionEnabledProperty =
        AvaloniaProperty.Register<PhotoGrid, bool>(nameof(IsSelectionEnabled), true);

    /// <summary>The grid's accessible name (default "Photos").</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<PhotoGrid, string>(nameof(Label), "Photos");

    /// <summary>How a photo is announced; {0} is its position and {1} the number of photos (default "Photo {0} of {1}").</summary>
    public static readonly StyledProperty<string> ImageNameFormatProperty =
        AvaloniaProperty.Register<PhotoGrid, string>(nameof(ImageNameFormat), "Photo {0} of {1}");

    /// <summary>The photo spring: critically damped, 0.5s.</summary>
    internal static readonly SpringSpec ReflowSpring = SpringSpec.FromVisual(0.5, 0);

    private static readonly MotionSpec s_reducedFade = MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard);

    private readonly PhotosItemFeed<PhotoGridPhoto> _feed;
    private readonly List<PhotoGridTile> _tiles = new();
    private readonly List<PhotoGridTile> _leaving = new();
    private readonly Dictionary<PhotoGridPhoto, PhotoGridTile> _byPhoto = new();
    private double[] _ratios = [];
    private Rect[] _rects = [];
    private double[] _columns = new double[8];
    private MotionValue<double>? _progress;
    private double _measuredWidth = double.NaN;
    private double _height;
    private bool _laidOut;
    private bool _reflowPending;
    private bool _itemsChanged;
    private bool _syncing;
    private int _focusIndex;

    static PhotoGrid()
    {
        AffectsMeasure<PhotoGrid>(LayoutProperty, RowHeightProperty, ColumnWidthProperty, SpacingProperty, SelectedMaxHeightProperty);
        AutomationProperties.AccessibilityViewProperty.OverrideDefaultValue<PhotoGrid>(Avalonia.Automation.AccessibilityView.Content);
    }

    public PhotoGrid()
    {
        _feed = new PhotosItemFeed<PhotoGridPhoto>(Convert, OnItemsChanged);
    }

    /// <inheritdoc cref="ItemsProperty"/>
    [Content]
    public AvaloniaList<PhotoGridPhoto> Items => _feed.Items;

    /// <inheritdoc cref="ItemsSourceProperty"/>
    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <inheritdoc cref="LayoutProperty"/>
    public PhotoGridLayout Layout
    {
        get => GetValue(LayoutProperty);
        set => SetValue(LayoutProperty, value);
    }

    /// <inheritdoc cref="RowHeightProperty"/>
    public double RowHeight
    {
        get => GetValue(RowHeightProperty);
        set => SetValue(RowHeightProperty, value);
    }

    /// <inheritdoc cref="ColumnWidthProperty"/>
    public double ColumnWidth
    {
        get => GetValue(ColumnWidthProperty);
        set => SetValue(ColumnWidthProperty, value);
    }

    /// <inheritdoc cref="SpacingProperty"/>
    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    /// <inheritdoc cref="SelectedIndexProperty"/>
    public int SelectedIndex
    {
        get => GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    /// <inheritdoc cref="SelectionChangedEvent"/>
    public event EventHandler<PhotosSelectionChangedEventArgs>? SelectionChanged
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    /// <inheritdoc cref="SelectedMaxHeightProperty"/>
    public double SelectedMaxHeight
    {
        get => GetValue(SelectedMaxHeightProperty);
        set => SetValue(SelectedMaxHeightProperty, value);
    }

    /// <inheritdoc cref="IsSelectionEnabledProperty"/>
    public bool IsSelectionEnabled
    {
        get => GetValue(IsSelectionEnabledProperty);
        set => SetValue(IsSelectionEnabledProperty, value);
    }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <inheritdoc cref="ImageNameFormatProperty"/>
    public string ImageNameFormat
    {
        get => GetValue(ImageNameFormatProperty);
        set => SetValue(ImageNameFormatProperty, value);
    }

    /// <summary>The number of photos.</summary>
    public int Count => _feed.Count;

    internal IReadOnlyList<PhotoGridTile> Tiles => _tiles;

    internal int LeavingCount => _leaving.Count;

    /// <summary>True while photos are gliding to new places.</summary>
    internal bool IsReflowing => _progress is { IsAnimating: true };

    private static PhotoGridPhoto? Convert(object? element) => element switch
    {
        PhotoGridPhoto photo => photo,
        _ when PhotosItemFeed<PhotoGridPhoto>.SourceOf(element) is { } source => new PhotoGridPhoto { Source = source },
        _ => null
    };

    // Items --------------------------------------------------------------------------------------------------------------

    private void OnItemsChanged()
    {
        var photos = _feed.Current;
        var reuse = new Dictionary<PhotoGridPhoto, PhotoGridTile>(_byPhoto);
        var animate = _laidOut;
        _byPhoto.Clear();
        _tiles.Clear();
        for (var i = 0; i < photos.Count; i++)
        {
            var photo = photos[i];
            if (!reuse.Remove(photo, out var tile))
            {
                tile = new PhotoGridTile(this) { IsEntering = animate };
                tile.SetPhoto(photo);
                VisualChildren.Add(tile);
                LogicalChildren.Add(tile);
            }
            else
            {
                tile.SetPhoto(photo);
            }
            tile.Index = i;
            _tiles.Add(tile);
            _byPhoto.TryAdd(photo, tile);
        }
        // Photos that left fade out where they are, then go.
        foreach (var gone in reuse.Values)
        {
            if (gone.IsFocused)
            {
                Focus(NavigationMethod.Unspecified);
            }
            if (animate && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off)
            {
                gone.IsLeaving = true;
                gone.IsHitTestVisible = false;
                gone.Focusable = false;
                _leaving.Add(gone);
            }
            else
            {
                Remove(gone);
            }
        }
        if (SelectedIndex >= photos.Count)
        {
            var was = SelectedIndex;
            _syncing = true;
            SetCurrentValue(SelectedIndexProperty, -1);
            _syncing = false;
            RaiseSelectionChanged(was);
        }
        _focusIndex = Math.Clamp(_focusIndex, 0, Math.Max(0, photos.Count - 1));
        _itemsChanged = true;
        UpdateTiles();
        InvalidateMeasure();
    }

    private void Remove(PhotoGridTile tile)
    {
        VisualChildren.Remove(tile);
        LogicalChildren.Remove(tile);
    }

    private void UpdateTiles()
    {
        var selected = SelectedIndex;
        for (var i = 0; i < _tiles.Count; i++)
        {
            var tile = _tiles[i];
            tile.IsSelected = i == selected;
            tile.IsTabStop = i == _focusIndex;
            AutomationProperties.SetName(tile, NameOf(i));
        }
    }

    /// <summary>"Photo 4 of 18: Fern unrolling".</summary>
    internal string NameOf(int index)
    {
        string name;
        try
        {
            name = string.Format(CultureInfo.CurrentCulture, ImageNameFormat, index + 1, Count);
        }
        catch (FormatException)
        {
            name = string.Format(CultureInfo.CurrentCulture, "Photo {0} of {1}", index + 1, Count);
        }
        return _feed[index].Description is { Length: > 0 } description ? name + ": " + description : name;
    }

    internal void OnRatioDiscovered()
    {
        if (_reflowPending)
        {
            return;
        }
        // Several pictures tend to arrive together: one reflow for all of them.
        _reflowPending = true;
        Dispatcher.UIThread.Post(() =>
        {
            _reflowPending = false;
            InvalidateMeasure();
        }, DispatcherPriority.Background);
    }

    // Selection and keyboard ---------------------------------------------------------------------------------------------

    internal void Toggle(PhotoGridTile tile)
    {
        if (!IsSelectionEnabled)
        {
            return;
        }
        SetCurrentValue(SelectedIndexProperty, SelectedIndex == tile.Index ? -1 : tile.Index);
    }

    internal void OnTileFocused(PhotoGridTile tile)
    {
        _focusIndex = tile.Index;
        foreach (var other in _tiles)
        {
            other.IsTabStop = ReferenceEquals(other, tile);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty)
        {
            _feed.ItemsSource = change.GetNewValue<IEnumerable?>();
        }
        else if (change.Property == SelectedIndexProperty && !_syncing)
        {
            var index = change.GetNewValue<int>();
            if (index < -1 || index >= Count)
            {
                _syncing = true;
                SetCurrentValue(SelectedIndexProperty, -1);
                _syncing = false;
            }
            UpdateTiles();
            InvalidateMeasure();
            if (SelectedIndex >= 0 && SelectedIndex < _tiles.Count)
            {
                var tile = _tiles[SelectedIndex];
                Dispatcher.UIThread.Post(() => BringIntoView(tile), DispatcherPriority.Loaded);
            }
            RaiseSelectionChanged(change.GetOldValue<int>());
        }
        else if (change.Property == ImageNameFormatProperty)
        {
            UpdateTiles();
        }
        else if (change.Property == LabelProperty)
        {
            AutomationProperties.SetName(this, change.GetNewValue<string>());
        }
        else if (change.Property == IsSelectionEnabledProperty && !change.GetNewValue<bool>() && SelectedIndex >= 0)
        {
            SetCurrentValue(SelectedIndexProperty, -1);
        }
    }

    private void RaiseSelectionChanged(int oldIndex)
    {
        var newIndex = SelectedIndex;
        if (newIndex != oldIndex)
        {
            RaiseEvent(new PhotosSelectionChangedEventArgs(SelectionChangedEvent, oldIndex, newIndex));
        }
    }

    private void BringIntoView(PhotoGridTile tile)
    {
        if (tile.Parent == this)
        {
            tile.BringIntoView(new Rect(tile.Bounds.Size));
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.Source is not PhotoGridTile tile || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        var index = tile.Index;
        int target;
        switch (e.Key)
        {
            case Key.Left:
                target = index - 1;
                break;
            case Key.Right:
                target = index + 1;
                break;
            case Key.Up:
                target = Neighbour(index, up: true);
                break;
            case Key.Down:
                target = Neighbour(index, up: false);
                break;
            case Key.Home:
                target = 0;
                break;
            case Key.End:
                target = Count - 1;
                break;
            case Key.Enter or Key.Space:
                Toggle(tile);
                e.Handled = true;
                return;
            case Key.Escape when SelectedIndex >= 0:
                SetCurrentValue(SelectedIndexProperty, -1);
                e.Handled = true;
                return;
            default:
                return;
        }
        e.Handled = true;
        if (target >= 0 && target < _tiles.Count)
        {
            _tiles[target].Focus(NavigationMethod.Directional);
        }
    }

    /// <summary>The photo above or below <paramref name="index"/>: in the nearest row that way, the one most in line with it.</summary>
    internal int Neighbour(int index, bool up)
    {
        if (index < 0 || index >= _tiles.Count)
        {
            return -1;
        }
        var from = _tiles[index].Target;
        var best = -1;
        var bestRow = double.PositiveInfinity;
        var bestX = double.PositiveInfinity;
        for (var i = 0; i < _tiles.Count; i++)
        {
            if (i == index)
            {
                continue;
            }
            var r = _tiles[i].Target;
            var gap = up ? from.Top - r.Bottom : r.Top - from.Bottom;
            if (gap < -0.5)
            {
                continue;
            }
            var dx = Math.Abs(r.Center.X - from.Center.X);
            // The nearest row first (rows are found by the gap), then the photo most in line.
            if (gap < bestRow - 1 || (Math.Abs(gap - bestRow) <= 1 && dx < bestX))
            {
                best = i;
                bestRow = gap;
                bestX = dx;
            }
        }
        return best;
    }

    // Layout and motion --------------------------------------------------------------------------------------------------

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _feed.Attach();
        AutomationProperties.SetName(this, Label);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _feed.Detach();
        _laidOut = false;
    }

    private void Compute(double width)
    {
        var count = _tiles.Count;
        if (_ratios.Length < count)
        {
            _ratios = new double[Math.Max(count, _ratios.Length * 2)];
            _rects = new Rect[_ratios.Length];
        }
        for (var i = 0; i < count; i++)
        {
            var tile = _tiles[i];
            var given = tile.Photo?.AspectRatio ?? 0;
            _ratios[i] = given > 0 ? given : tile.LoadedRatio;
        }
        var columns = PhotoGridLayoutEngine.ColumnCount(width, Spacing, ColumnWidth);
        if (_columns.Length < columns)
        {
            _columns = new double[columns];
        }
        _height = PhotoGridLayoutEngine.Arrange(_ratios.AsSpan(0, count), _rects.AsSpan(0, count), width, Spacing, Layout, RowHeight,
            ColumnWidth, SelectedIndex, SelectedMaxHeight, _columns);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 960;
        Compute(width);
        _measuredWidth = width;
        for (var i = 0; i < _tiles.Count; i++)
        {
            _tiles[i].Measure(_rects[i].Size);
        }
        foreach (var tile in _leaving)
        {
            tile.Measure(tile.Target.Size);
        }
        return new Size(width, _height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (!(Math.Abs(finalSize.Width - _measuredWidth) < 0.5))
        {
            Compute(finalSize.Width);
            _measuredWidth = finalSize.Width;
        }
        var moved = _itemsChanged && _laidOut && _leaving.Count > 0;
        _itemsChanged = false;
        for (var i = 0; i < _tiles.Count; i++)
        {
            var tile = _tiles[i];
            if (!_laidOut)
            {
                tile.From = tile.Shown = tile.Target = _rects[i];
                tile.IsEntering = false;
            }
            else if (tile.IsEntering || !Same(tile.Target, _rects[i]))
            {
                moved = true;
            }
        }
        if (moved)
        {
            // Every photo starts from wherever it is drawn now, so an interrupted reflow carries on without a jump.
            for (var i = 0; i < _tiles.Count; i++)
            {
                var tile = _tiles[i];
                tile.From = tile.IsEntering ? _rects[i] : tile.Shown;
                tile.Target = _rects[i];
            }
            foreach (var tile in _leaving)
            {
                tile.From = tile.Target = tile.Shown;
            }
        }
        for (var i = 0; i < _tiles.Count; i++)
        {
            _tiles[i].Arrange(_tiles[i].Target);
        }
        foreach (var tile in _leaving)
        {
            tile.Arrange(tile.Target);
        }
        if (moved)
        {
            StartReflow();
        }
        else if (!IsReflowing)
        {
            Apply(1);
        }
        _laidOut = _tiles.Count > 0 || _laidOut;
        return finalSize;
    }

    private void StartReflow()
    {
        if (_progress is null)
        {
            _progress = new MotionValue<double>(this, 1, MotionVectors.Double);
            _progress.Changed += Apply;
        }
        _progress.Jump(0);
        var level = ArcMotion.GetEffectiveLevel(this);
        if (level == ArcMotionLevel.Off)
        {
            _progress.Jump(1);
            FinishReflow();
            return;
        }
        var spec = level == ArcMotionLevel.Full ? MotionSpec.Spring(ReflowSpring) : s_reducedFade;
        _ = RunReflowAsync(spec);
    }

    private async Task RunReflowAsync(MotionSpec spec)
    {
        if (await _progress!.AnimateTo(1, spec))
        {
            FinishReflow();
        }
    }

    private void FinishReflow()
    {
        foreach (var tile in _leaving)
        {
            Remove(tile);
        }
        _leaving.Clear();
        foreach (var tile in _tiles)
        {
            tile.From = tile.Target;
            tile.IsEntering = false;
        }
        Apply(1);
    }

    /// <summary>
    /// Writes every photo's transform for reflow <paramref name="progress"/>. In full motion photos glide from their old
    /// boxes; under reduced motion they stay put and cross-fade at their new places. New photos fade (and in full motion
    /// grow) in; leaving photos fade out. No layout.
    /// </summary>
    private void Apply(double progress)
    {
        var full = ArcMotion.IsFull(this);
        foreach (var tile in _tiles)
        {
            var target = tile.Target;
            Rect shown;
            double opacity;
            if (tile.IsEntering)
            {
                var grow = full ? 0.92 + 0.08 * progress : 1;
                shown = Scale(target, grow);
                opacity = progress;
            }
            else if (full)
            {
                shown = Lerp(tile.From, target, progress);
                opacity = 1;
            }
            else
            {
                shown = target;
                // A photo that moved fades in at its new place.
                opacity = Same(tile.From, target) ? 1 : progress;
            }
            tile.Shown = shown;
            tile.Opacity = opacity;
            tile.Transform.Matrix = Place(target, shown);
        }
        foreach (var tile in _leaving)
        {
            tile.Shown = tile.Target;
            tile.Opacity = 1 - progress;
            tile.Transform.Matrix = Matrix.Identity;
        }
    }

    /// <summary>The transform that draws a control laid out at <paramref name="target"/> over <paramref name="shown"/>.</summary>
    internal static Matrix Place(Rect target, Rect shown)
    {
        if (target.Width <= 0 || target.Height <= 0)
        {
            return Matrix.Identity;
        }
        var sx = shown.Width / target.Width;
        var sy = shown.Height / target.Height;
        return Matrix.CreateScale(sx, sy) * Matrix.CreateTranslation(shown.X - target.X, shown.Y - target.Y);
    }

    private static Rect Scale(Rect rect, double scale)
    {
        var w = rect.Width * scale;
        var h = rect.Height * scale;
        return new Rect(rect.Center.X - w / 2, rect.Center.Y - h / 2, w, h);
    }

    private static Rect Lerp(Rect a, Rect b, double t) =>
        new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, Math.Max(0, a.Width + (b.Width - a.Width) * t),
            Math.Max(0, a.Height + (b.Height - a.Height) * t));

    private static bool Same(Rect a, Rect b) =>
        Math.Abs(a.X - b.X) < 0.5 && Math.Abs(a.Y - b.Y) < 0.5 && Math.Abs(a.Width - b.Width) < 0.5 && Math.Abs(a.Height - b.Height) < 0.5;

    protected override AutomationPeer OnCreateAutomationPeer() => new PhotoGridAutomationPeer(this);

    private sealed class PhotoGridAutomationPeer(PhotoGrid owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;

        protected override string? GetNameCore() => owner.Label;
    }
}
