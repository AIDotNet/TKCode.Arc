using System.Collections;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Threading;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A grid of pictures that opens any of them full screen. The chosen tile grows into the lightbox, the picture opening
/// out of the tile's crop to its whole frame, and shrinks back into its tile on close, even after moving to another
/// picture. In the lightbox, arrows, the arrow keys or a sideways swipe move between pictures; the wheel, a trackpad or
/// touch pinch, a double click or the zoom buttons zoom in about the pointer, and a zoomed picture is dragged to look
/// around. Escape, the close button, a click beside the picture or pulling it up or down closes it.
/// </summary>
/// <remarks>
/// Pictures come from <see cref="Items"/> (written inline) or <see cref="ItemsSource"/>, whose elements may be
/// <see cref="LightboxGalleryImage"/>s, <see cref="ImageSource"/>s or addresses. <see cref="OpenIndex"/> is the picture
/// open in the lightbox (-1 when it is closed) and can be bound both ways. Under reduced motion the lightbox fades in and
/// out instead of flying and pictures cross-fade; with motion off everything changes at once.
/// </remarks>
[TemplatePart(GridPart, typeof(LightboxGalleryGrid))]
[PseudoClasses(OpenPseudoClass)]
public class LightboxGallery : TemplatedControl
{
    internal const string GridPart = "PART_Grid";
    internal const string OpenPseudoClass = ":open";

    /// <summary>The key that pairs a tile's face with the lightbox photo for the flight.</summary>
    internal const string FlightKey = "lightbox-gallery-photo";

    /// <summary>The tile grows into the lightbox: critically damped, 0.46s.</summary>
    internal static readonly SpringSpec OpenSpring = SpringSpec.FromVisual(0.46, 0);

    /// <summary>The picture lands back in its tile: critically damped, 0.4s.</summary>
    internal static readonly SpringSpec CloseSpring = SpringSpec.FromVisual(0.4, 0);

    /// <summary>The pictures written inline, used while <see cref="ItemsSource"/> is null.</summary>
    public static readonly DirectProperty<LightboxGallery, AvaloniaList<LightboxGalleryImage>> ItemsProperty =
        AvaloniaProperty.RegisterDirect<LightboxGallery, AvaloniaList<LightboxGalleryImage>>(nameof(Items), o => o.Items);

    /// <summary>
    /// The pictures to show: <see cref="LightboxGalleryImage"/>s, <see cref="ImageSource"/>s or addresses. Wins over
    /// <see cref="Items"/>; a collection that reports changes is followed.
    /// </summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<LightboxGallery, IEnumerable?>(nameof(ItemsSource));

    /// <summary>The index of the picture open in the lightbox; -1 while it is closed. Two-way: set it to open, move or close.</summary>
    public static readonly StyledProperty<int> OpenIndexProperty =
        AvaloniaProperty.Register<LightboxGallery, int>(nameof(OpenIndex), -1, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>A fixed number of columns; 0 (default) fits as many as <see cref="MinTileWidth"/> allows.</summary>
    public static readonly StyledProperty<int> ColumnsProperty =
        AvaloniaProperty.Register<LightboxGallery, int>(nameof(Columns), coerce: static (_, value) => Math.Max(0, value));

    /// <summary>The narrowest a tile gets before the grid drops a column (default 160).</summary>
    public static readonly StyledProperty<double> MinTileWidthProperty =
        AvaloniaProperty.Register<LightboxGallery, double>(nameof(MinTileWidth), 160);

    /// <summary>A tile's width divided by its height (default 1, square tiles).</summary>
    public static readonly StyledProperty<double> TileAspectRatioProperty =
        AvaloniaProperty.Register<LightboxGallery, double>(nameof(TileAspectRatio), 1,
            coerce: static (_, value) => double.IsFinite(value) && value > 0 ? value : 1);

    /// <summary>Space between tiles, set by the theme.</summary>
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<LightboxGallery, double>(nameof(Spacing), 8);

    /// <summary>The most the lightbox zooms a picture, as a multiple of its fitted size (default 4).</summary>
    public static readonly StyledProperty<double> MaxZoomProperty =
        AvaloniaProperty.Register<LightboxGallery, double>(nameof(MaxZoom), 4,
            coerce: static (_, value) => double.IsFinite(value) ? Math.Clamp(value, 1, 16) : 4);

    /// <summary>The backdrop behind the lightbox, set by the theme (near black, whatever the theme).</summary>
    public static readonly StyledProperty<IBrush?> BackdropProperty =
        AvaloniaProperty.Register<LightboxGallery, IBrush?>(nameof(Backdrop));

    /// <summary>The gallery's and the lightbox's accessible name (default "Photos").</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<LightboxGallery, string>(nameof(Label), "Photos");

    /// <summary>How a picture is announced; {0} is its position and {1} the number of pictures (default "Photo {0} of {1}").</summary>
    public static readonly StyledProperty<string> ImageNameFormatProperty =
        AvaloniaProperty.Register<LightboxGallery, string>(nameof(ImageNameFormat), "Photo {0} of {1}");

    /// <summary>Accessible name of the close button (default "Close").</summary>
    public static readonly StyledProperty<string> CloseLabelProperty =
        AvaloniaProperty.Register<LightboxGallery, string>(nameof(CloseLabel), "Close");

    /// <summary>Accessible name of the previous button (default "Previous photo").</summary>
    public static readonly StyledProperty<string> PreviousLabelProperty =
        AvaloniaProperty.Register<LightboxGallery, string>(nameof(PreviousLabel), "Previous photo");

    /// <summary>Accessible name of the next button (default "Next photo").</summary>
    public static readonly StyledProperty<string> NextLabelProperty =
        AvaloniaProperty.Register<LightboxGallery, string>(nameof(NextLabel), "Next photo");

    /// <summary>Accessible name of the zoom in button (default "Zoom in").</summary>
    public static readonly StyledProperty<string> ZoomInLabelProperty =
        AvaloniaProperty.Register<LightboxGallery, string>(nameof(ZoomInLabel), "Zoom in");

    /// <summary>Accessible name of the zoom out button (default "Zoom out").</summary>
    public static readonly StyledProperty<string> ZoomOutLabelProperty =
        AvaloniaProperty.Register<LightboxGallery, string>(nameof(ZoomOutLabel), "Zoom out");

    /// <summary>True while the lightbox is open (or opening).</summary>
    public static readonly DirectProperty<LightboxGallery, bool> IsOpenProperty =
        AvaloniaProperty.RegisterDirect<LightboxGallery, bool>(nameof(IsOpen), o => o.IsOpen);

    /// <summary>True while the open picture is zoomed in.</summary>
    public static readonly DirectProperty<LightboxGallery, bool> IsZoomedProperty =
        AvaloniaProperty.RegisterDirect<LightboxGallery, bool>(nameof(IsZoomed), o => o.IsZoomed);

    private static readonly MotionSpec s_scrimIn = MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter);
    private static readonly MotionSpec s_scrimOut = MotionSpec.Tween(ArcMotionPresets.Exit, ArcEasings.Standard);
    private static readonly MotionSpec s_chromeIn = MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter, ArcMotionPresets.Instant);
    private static readonly MotionSpec s_chromeOut = MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard);
    private static readonly MotionSpec s_fade = MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard);

    private readonly PhotosItemFeed<LightboxGalleryImage> _feed;
    private readonly List<LightboxGalleryTile> _tiles = new();
    private LightboxGalleryGrid? _grid;
    private LightboxGalleryView? _view;
    private ModalPresence? _presence;
    private SharedElementScope? _flight;
    private MotionValue<double>? _scrim;
    private MotionValue<double>? _chrome;
    private MotionValue<double>? _viewFade;
    private double _pull;
    private bool _isOpen;
    private bool _isZoomed;
    private bool _syncing;
    private bool _flies;
    private int _shownIndex = -1;
    private int _focusIndex;
    private DismissReason _closeReason;
    private (double Offset, double Velocity)? _swipe;

    static LightboxGallery()
    {
        ClipToBoundsProperty.OverrideDefaultValue<LightboxGallery>(false);
    }

    public LightboxGallery()
    {
        _feed = new PhotosItemFeed<LightboxGalleryImage>(Convert, OnItemsChanged);
        SpecialFocusModality.Watch();
    }

    /// <inheritdoc cref="ItemsProperty"/>
    [Content]
    public AvaloniaList<LightboxGalleryImage> Items => _feed.Items;

    /// <inheritdoc cref="ItemsSourceProperty"/>
    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <inheritdoc cref="OpenIndexProperty"/>
    public int OpenIndex
    {
        get => GetValue(OpenIndexProperty);
        set => SetValue(OpenIndexProperty, value);
    }

    /// <inheritdoc cref="ColumnsProperty"/>
    public int Columns
    {
        get => GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    /// <inheritdoc cref="MinTileWidthProperty"/>
    public double MinTileWidth
    {
        get => GetValue(MinTileWidthProperty);
        set => SetValue(MinTileWidthProperty, value);
    }

    /// <inheritdoc cref="TileAspectRatioProperty"/>
    public double TileAspectRatio
    {
        get => GetValue(TileAspectRatioProperty);
        set => SetValue(TileAspectRatioProperty, value);
    }

    /// <inheritdoc cref="SpacingProperty"/>
    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    /// <inheritdoc cref="MaxZoomProperty"/>
    public double MaxZoom
    {
        get => GetValue(MaxZoomProperty);
        set => SetValue(MaxZoomProperty, value);
    }

    /// <inheritdoc cref="BackdropProperty"/>
    public IBrush? Backdrop
    {
        get => GetValue(BackdropProperty);
        set => SetValue(BackdropProperty, value);
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

    /// <inheritdoc cref="CloseLabelProperty"/>
    public string CloseLabel
    {
        get => GetValue(CloseLabelProperty);
        set => SetValue(CloseLabelProperty, value);
    }

    /// <inheritdoc cref="PreviousLabelProperty"/>
    public string PreviousLabel
    {
        get => GetValue(PreviousLabelProperty);
        set => SetValue(PreviousLabelProperty, value);
    }

    /// <inheritdoc cref="NextLabelProperty"/>
    public string NextLabel
    {
        get => GetValue(NextLabelProperty);
        set => SetValue(NextLabelProperty, value);
    }

    /// <inheritdoc cref="ZoomInLabelProperty"/>
    public string ZoomInLabel
    {
        get => GetValue(ZoomInLabelProperty);
        set => SetValue(ZoomInLabelProperty, value);
    }

    /// <inheritdoc cref="ZoomOutLabelProperty"/>
    public string ZoomOutLabel
    {
        get => GetValue(ZoomOutLabelProperty);
        set => SetValue(ZoomOutLabelProperty, value);
    }

    /// <inheritdoc cref="IsOpenProperty"/>
    public bool IsOpen
    {
        get => _isOpen;
        private set => SetAndRaise(IsOpenProperty, ref _isOpen, value);
    }

    /// <inheritdoc cref="IsZoomedProperty"/>
    public bool IsZoomed
    {
        get => _isZoomed;
        private set => SetAndRaise(IsZoomedProperty, ref _isZoomed, value);
    }

    /// <summary>The number of pictures.</summary>
    public int Count => _feed.Count;

    internal IReadOnlyList<LightboxGalleryTile> Tiles => _tiles;

    internal LightboxGalleryView? View => _view;

    internal ModalPresence? Presence => _presence;

    internal SharedElementScope? Flight => _flight;

    internal LightboxGalleryImage? ImageAt(int index) => index >= 0 && index < Count ? _feed[index] : null;

    /// <summary>Opens the lightbox on the picture at <paramref name="index"/>, or moves to it when the lightbox is open.</summary>
    public void Open(int index)
    {
        if (index >= 0 && index < Count)
        {
            SetCurrentValue(OpenIndexProperty, index);
        }
    }

    /// <summary>Closes the lightbox; the picture flies back into its tile.</summary>
    public void Close() => RequestClose(DismissReason.Programmatic);

    /// <summary>Moves the lightbox to the next picture; returns false at the last one or while it is closed.</summary>
    public bool ShowNext() => Move(OpenIndex + 1);

    /// <summary>Moves the lightbox to the previous picture; returns false at the first one or while it is closed.</summary>
    public bool ShowPrevious() => Move(OpenIndex - 1);

    private bool Move(int index)
    {
        if (OpenIndex < 0 || index < 0 || index >= Count || index == OpenIndex)
        {
            return false;
        }
        SetCurrentValue(OpenIndexProperty, index);
        return true;
    }

    internal void RequestClose(DismissReason reason)
    {
        if (OpenIndex < 0)
        {
            return;
        }
        _closeReason = reason;
        SetCurrentValue(OpenIndexProperty, -1);
    }

    internal void SelectFromSwipe(int index, double offset, double velocity)
    {
        _swipe = (offset, velocity);
        SetCurrentValue(OpenIndexProperty, index);
        _swipe = null;
    }

    private static LightboxGalleryImage? Convert(object? element) => element switch
    {
        LightboxGalleryImage image => image,
        _ when PhotosItemFeed<LightboxGalleryImage>.SourceOf(element) is { } source => new LightboxGalleryImage { Source = source },
        _ => null
    };

    /// <summary>"Photo 3 of 12: Harbour at dawn".</summary>
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
        var image = _feed[index];
        var text = image.Description is { Length: > 0 } description ? description : image.Caption;
        return text is { Length: > 0 } ? name + ": " + text : name;
    }

    // Items and grid -----------------------------------------------------------------------------------------------------

    private void OnItemsChanged()
    {
        RebuildTiles();
        var count = Count;
        if (OpenIndex >= count)
        {
            if (count == 0)
            {
                RequestClose(DismissReason.Programmatic);
            }
            else
            {
                SetCurrentValue(OpenIndexProperty, count - 1);
            }
        }
        else if (OpenIndex >= 0 && _view?.Viewport is { } viewport)
        {
            viewport.ShowAtRest(OpenIndex);
            UpdateChrome();
        }
    }

    private void RebuildTiles()
    {
        if (_grid is null)
        {
            return;
        }
        var count = Count;
        while (_tiles.Count > count)
        {
            var last = _tiles[^1];
            _tiles.RemoveAt(_tiles.Count - 1);
            _grid.Children.Remove(last);
        }
        while (_tiles.Count < count)
        {
            var tile = new LightboxGalleryTile { Index = _tiles.Count };
            tile.Click += OnTileClick;
            tile.GotFocus += OnTileFocused;
            _tiles.Add(tile);
            _grid.Children.Add(tile);
        }
        _focusIndex = Math.Clamp(_focusIndex, 0, Math.Max(0, count - 1));
        for (var i = 0; i < count; i++)
        {
            var image = _feed[i];
            var tile = _tiles[i];
            tile.Source = image.Thumbnail ?? image.Source;
            tile.IsTabStop = i == _focusIndex;
            AutomationProperties.SetName(tile, NameOf(i));
        }
    }

    private void OnTileClick(object? sender, RoutedEventArgs e)
    {
        if (sender is LightboxGalleryTile tile)
        {
            Open(tile.Index);
        }
    }

    private void OnTileFocused(object? sender, FocusChangedEventArgs e)
    {
        if (sender is not LightboxGalleryTile tile)
        {
            return;
        }
        _focusIndex = tile.Index;
        // One tab stop for the grid: the tile last focused (arrow keys move within it).
        foreach (var other in _tiles)
        {
            other.IsTabStop = ReferenceEquals(other, tile);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.Source is not LightboxGalleryTile tile || e.KeyModifiers != KeyModifiers.None || _grid is null)
        {
            return;
        }
        var columns = Math.Max(1, _grid.ColumnCount);
        var index = tile.Index;
        var target = e.Key switch
        {
            Key.Left => index - 1,
            Key.Right => index + 1,
            Key.Up => index - columns,
            Key.Down => index + columns,
            Key.Home => 0,
            Key.End => Count - 1,
            _ => int.MinValue
        };
        if (target == int.MinValue)
        {
            return;
        }
        e.Handled = true;
        if (target >= 0 && target < _tiles.Count)
        {
            _tiles[target].Focus(NavigationMethod.Directional);
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_grid is not null)
        {
            _grid.Children.Clear();
            _tiles.Clear();
        }
        _grid = e.NameScope.Find<LightboxGalleryGrid>(GridPart);
        if (_grid is not null)
        {
            _grid.Columns = Columns;
            _grid.MinTileWidth = MinTileWidth;
            _grid.TileAspectRatio = TileAspectRatio;
            _grid.Spacing = Spacing;
        }
        RebuildTiles();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty)
        {
            _feed.ItemsSource = change.GetNewValue<IEnumerable?>();
        }
        else if (change.Property == OpenIndexProperty)
        {
            OnOpenIndexChanged(change.GetOldValue<int>(), change.GetNewValue<int>());
        }
        else if (change.Property == ColumnsProperty && _grid is not null)
        {
            _grid.Columns = change.GetNewValue<int>();
        }
        else if (change.Property == MinTileWidthProperty && _grid is not null)
        {
            _grid.MinTileWidth = change.GetNewValue<double>();
        }
        else if (change.Property == TileAspectRatioProperty && _grid is not null)
        {
            _grid.TileAspectRatio = change.GetNewValue<double>();
        }
        else if (change.Property == SpacingProperty && _grid is not null)
        {
            _grid.Spacing = change.GetNewValue<double>();
        }
        else if (change.Property == ImageNameFormatProperty)
        {
            RebuildTiles();
            UpdateChrome();
        }
        else if (change.Property == LabelProperty || change.Property == CloseLabelProperty || change.Property == PreviousLabelProperty ||
                 change.Property == NextLabelProperty || change.Property == ZoomInLabelProperty || change.Property == ZoomOutLabelProperty)
        {
            UpdateChrome();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _feed.Attach();
        if (OpenIndex >= 0 && !IsOpen)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (OpenIndex >= 0 && !IsOpen)
                {
                    OpenCore(OpenIndex);
                }
            }, DispatcherPriority.Loaded);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _feed.Detach();
    }

    // Opening, moving and closing ----------------------------------------------------------------------------------------

    private void OnOpenIndexChanged(int previous, int index)
    {
        if (_syncing)
        {
            return;
        }
        if (index >= Count)
        {
            _syncing = true;
            SetCurrentValue(OpenIndexProperty, Count == 0 ? -1 : Count - 1);
            _syncing = false;
            index = OpenIndex;
        }
        if (index < -1)
        {
            _syncing = true;
            SetCurrentValue(OpenIndexProperty, -1);
            _syncing = false;
            index = -1;
        }
        if (index < 0)
        {
            CloseCore();
        }
        else if (!IsOpen)
        {
            OpenCore(index);
        }
        else
        {
            MoveCore(previous, index);
        }
    }

    private void EnsureLightbox()
    {
        if (_view is not null)
        {
            return;
        }
        var view = new LightboxGalleryView(this);
        view.PartsChanged += (_, _) => OnViewParts();
        LogicalChildren.Add(view);
        _view = view;
        _presence = new ModalPresence(view, this)
        {
            Entrance = ModalEntrance.Custom,
            CustomMotion = RunMotion,
            RestoreFocus = false,
            DismissOnScrimPress = false
        };
        _presence.Dismissing += (_, e) =>
        {
            // The gallery owns the decision, so OpenIndex (and its bindings) sees every close.
            e.Cancel = true;
            RequestClose(e.Reason);
        };
        _presence.Closed += OnPresenceClosed;
        _scrim = Value(0, _ => ApplyFades());
        _chrome = Value(0, _ => ApplyFades());
        _viewFade = Value(1, _ => ApplyFades());
    }

    private MotionValue<double> Value(double initial, Action<double> changed)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += changed;
        return value;
    }

    private void OnViewParts()
    {
        if (_view?.Viewport is { } viewport && _shownIndex >= 0)
        {
            viewport.ShowAtRest(_shownIndex);
        }
        UpdateChrome();
        ApplyFades();
    }

    private void ApplyFades()
    {
        var pull = Math.Clamp(_pull, 0, 1);
        if (_presence is not null)
        {
            _presence.ScrimOpacity = (_scrim?.Value ?? 0) * (1 - 0.75 * pull);
        }
        if (_view is not null)
        {
            _view.Opacity = Math.Clamp(_viewFade?.Value ?? 1, 0, 1);
            if (_view.Chrome is { } chrome)
            {
                chrome.Opacity = Math.Clamp((_chrome?.Value ?? 0) * (1 - pull), 0, 1);
            }
        }
    }

    internal void OnDismissProgress(double progress)
    {
        _pull = progress;
        ApplyFades();
    }

    internal void OnZoomChanged(bool zoomed)
    {
        IsZoomed = zoomed;
        UpdateChrome();
    }

    private void OpenCore(int index)
    {
        if (TopLevel.GetTopLevel(this) is null)
        {
            return;
        }
        EnsureLightbox();
        var presence = _presence!;
        _shownIndex = index;
        _closeReason = DismissReason.Programmatic;
        _presence!.ScrimBrush = Backdrop;
        if (presence.State == ModalState.Closed)
        {
            // Decided per opening: a lightbox that opened by flying closes by flying.
            _flies = ArcMotion.IsFull(this);
            _pull = 0;
            _scrim!.Jump(0);
            _chrome!.Jump(_flies ? 0 : 1);
            _viewFade!.Jump(_flies ? 1 : 0);
        }
        _view!.Viewport?.ShowAtRest(index);
        presence.InitialFocus = _view.Viewport;
        if (!presence.Open())
        {
            return;
        }
        IsOpen = true;
        PseudoClasses.Set(OpenPseudoClass, true);
        UpdateChrome();
        if (_view.Viewport is { } viewport)
        {
            viewport.Focus(SpecialFocusModality.For(this, NavigationMethod.Tab));
        }
    }

    private void MoveCore(int previous, int index)
    {
        _shownIndex = index;
        if (_view?.Viewport is { } viewport)
        {
            if (_swipe is { } swipe)
            {
                viewport.Show(index, previous, swipe.Offset, swipe.Velocity);
            }
            else
            {
                viewport.Show(index, previous);
            }
        }
        if (_flies && _presence is { IsOpen: true })
        {
            // The tile of the picture on show is the one hidden, and the one the picture returns to.
            _flight?.Reset();
            _flight = null;
            if (FlightFor(index, progress: 1) is { } flight)
            {
                flight.HideSource();
            }
        }
        UpdateChrome();
    }

    private void CloseCore()
    {
        if (_presence is null || !_presence.IsOpen)
        {
            IsOpen = false;
            PseudoClasses.Set(OpenPseudoClass, false);
            return;
        }
        var reason = _closeReason;
        _closeReason = DismissReason.Programmatic;
        _view?.Viewport?.ReleasePull();
        var returnTo = _shownIndex;
        if (returnTo >= 0 && returnTo < _tiles.Count)
        {
            _tiles[returnTo].BringIntoView();
            _focusIndex = returnTo;
        }
        _presence.Close(reason);
        IsOpen = false;
        PseudoClasses.Set(OpenPseudoClass, false);
        // Focus returns to the tile of the picture that was on show.
        if (returnTo >= 0 && returnTo < _tiles.Count)
        {
            var tile = _tiles[returnTo];
            foreach (var other in _tiles)
            {
                other.IsTabStop = ReferenceEquals(other, tile);
            }
            tile.Focus(reason == DismissReason.Escape ? NavigationMethod.Tab : SpecialFocusModality.For(this, NavigationMethod.Tab));
        }
    }

    private SharedElementScope? FlightFor(int index, double progress)
    {
        if (_view?.Viewport?.Current is not { } photo || index < 0 || index >= _tiles.Count)
        {
            return null;
        }
        var tile = _tiles[index];
        if (_flight is { } existing && ReferenceEquals(existing.Source, tile))
        {
            return existing;
        }
        _flight?.Reset();
        if (!tile.IsEffectivelyVisible || tile.Bounds.Width <= 0 || TopLevel.GetTopLevel(tile) is null)
        {
            _flight = null;
            return null;
        }
        var flight = new SharedElementScope(tile, photo, progress) { RevealsSourceOnArrival = true };
        var radius = tile.Face?.CornerRadius.TopLeft ?? 0;
        flight.ProgressChanged += p => photo.SetFlightRadius(radius * (1 - Math.Clamp(p, 0, 1)));
        _flight = flight;
        return flight;
    }

    private async Task<bool> RunMotion(ModalPresence presence, bool entering)
    {
        var scrim = _scrim!;
        var chrome = _chrome!;
        var fade = _viewFade!;
        var photo = _view?.Viewport?.Current;
        if (entering)
        {
            if (_flies && FlightFor(_shownIndex, progress: 0) is { } flight)
            {
                fade.Jump(1);
                var scrimIn = scrim.AnimateTo(1, s_scrimIn);
                _ = chrome.AnimateTo(1, s_chromeIn);
                var grown = await flight.FlyAsync(1, MotionSpec.Spring(OpenSpring));
                photo?.SetFlightRadius(0);
                return grown & await scrimIn;
            }
            chrome.Jump(1);
            var shown = fade.AnimateTo(1, s_fade);
            var dimmed = scrim.AnimateTo(1, s_fade);
            return await shown & await dimmed;
        }
        if (_flies && FlightFor(_shownIndex, progress: 1) is { } back)
        {
            _ = chrome.AnimateTo(0, s_chromeOut);
            var scrimOut = scrim.AnimateTo(0, s_scrimOut);
            var landed = await back.FlyAsync(0, MotionSpec.Spring(CloseSpring));
            return landed & await scrimOut;
        }
        var hidden = fade.AnimateTo(0, s_fade);
        var cleared = scrim.AnimateTo(0, s_fade);
        _ = chrome.AnimateTo(0, s_fade);
        return await hidden & await cleared;
    }

    private void OnPresenceClosed(object? sender, ModalClosedEventArgs e)
    {
        _flight?.Reset();
        _flight = null;
        _view?.Viewport?.Current.SetFlightRadius(0);
        _view?.Viewport?.ShowAtRest(-1);
        _pull = 0;
        _shownIndex = -1;
        IsZoomed = false;
    }

    // The lightbox's chrome and keys -------------------------------------------------------------------------------------

    private void UpdateChrome()
    {
        if (_view is null)
        {
            return;
        }
        var index = _shownIndex;
        var count = Count;
        var valid = index >= 0 && index < count;
        _view.SetFlags(IsZoomed, count <= 1);
        if (_view.Counter is { } counter)
        {
            counter.Text = valid ? string.Format(CultureInfo.CurrentCulture, "{0} / {1}", index + 1, count) : string.Empty;
        }
        if (_view.Caption is { } caption)
        {
            var text = valid ? _feed[index].Caption : null;
            caption.Text = text;
            caption.IsVisible = !string.IsNullOrEmpty(text);
        }
        var spoken = valid ? NameOf(index) : string.Empty;
        if (_view.Status is { } status)
        {
            status.Text = spoken;
        }
        if (_view.Viewport is { } viewport)
        {
            AutomationProperties.SetName(viewport, spoken);
        }
        AutomationProperties.SetName(_view, Label);
        Name(_view.CloseButton, CloseLabel, true);
        Name(_view.PreviousButton, PreviousLabel, valid && index > 0);
        Name(_view.NextButton, NextLabel, valid && index < count - 1);
        Name(_view.ZoomInButton, ZoomInLabel, (_view.Viewport?.View.Zoom ?? 1) < MaxZoom - 1e-3);
        Name(_view.ZoomOutButton, ZoomOutLabel, IsZoomed);

        static void Name(Button? button, string name, bool enabled)
        {
            if (button is null)
            {
                return;
            }
            AutomationProperties.SetName(button, name);
            button.IsEnabled = enabled;
        }
    }

    /// <summary>The lightbox's keys: arrows move (or pan a zoomed picture), + and - zoom, 0 shows the whole picture.</summary>
    internal bool HandleLightboxKey(Key key, KeyModifiers modifiers)
    {
        if (modifiers != KeyModifiers.None && modifiers != KeyModifiers.Shift || _view?.Viewport is not { } viewport)
        {
            return false;
        }
        const double step = 80;
        switch (key)
        {
            case Key.Left when viewport.IsZoomed:
                viewport.PanBy(new Vector(step, 0));
                return true;
            case Key.Right when viewport.IsZoomed:
                viewport.PanBy(new Vector(-step, 0));
                return true;
            case Key.Up when viewport.IsZoomed:
                viewport.PanBy(new Vector(0, step));
                return true;
            case Key.Down when viewport.IsZoomed:
                viewport.PanBy(new Vector(0, -step));
                return true;
            case Key.Left:
                ShowPrevious();
                return true;
            case Key.Right:
                ShowNext();
                return true;
            case Key.Home:
                Move(0);
                return true;
            case Key.End:
                Move(Count - 1);
                return true;
            case Key.OemPlus or Key.Add:
                viewport.ZoomBy(1.6);
                return true;
            case Key.OemMinus or Key.Subtract:
                viewport.ZoomBy(1 / 1.6);
                return true;
            case Key.D0 or Key.NumPad0:
                viewport.ResetZoom();
                return true;
            default:
                return false;
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new LightboxGalleryAutomationPeer(this);

    private sealed class LightboxGalleryAutomationPeer(LightboxGallery owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;

        protected override string? GetNameCore() => owner.Label;
    }
}
