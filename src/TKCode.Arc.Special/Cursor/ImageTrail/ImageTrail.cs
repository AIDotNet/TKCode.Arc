using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A surface that leaves a trail of pictures behind the pointer: every few dozen px of movement a picture pops in where
/// the pointer is, tilted a little, rests for a moment and sinks away, cycling through <see cref="Sources"/>.
/// </summary>
/// <remarks>
/// <see cref="ContentControl.Content"/> sits under the trail, such as a headline; the trail never takes the pointer from
/// it. Pictures load through <see cref="ArcImage"/> (so <see cref="ArcImage.LoaderProperty"/> and the shared image cache
/// apply) and show a muted placeholder until they arrive. With keyboard focus the arrow keys walk a cursor across the
/// surface and leave the same trail. Under reduced motion pictures only fade in and out; with motion off there is no
/// trail.
/// </remarks>
[TemplatePart(RootPart, typeof(Panel))]
[TemplatePart(LayerPart, typeof(ImageTrailLayer))]
[TemplatePart(LoadersPart, typeof(Panel))]
public class ImageTrail : ContentControl
{
    public const string RootPart = "PART_Root";
    public const string LayerPart = "PART_Layer";
    public const string LoadersPart = "PART_Loaders";

    private const double KeyStep = 24;

    /// <summary>The pictures the trail cycles through, in order.</summary>
    public static readonly StyledProperty<IReadOnlyList<ImageSource>?> SourcesProperty =
        AvaloniaProperty.Register<ImageTrail, IReadOnlyList<ImageSource>?>(nameof(Sources));

    /// <summary>The longer side of each picture on the trail, in px.</summary>
    public static readonly StyledProperty<double> ImageSizeProperty =
        AvaloniaProperty.Register<ImageTrail, double>(nameof(ImageSize), 140);

    /// <summary>How far the pointer travels between two pictures, in px.</summary>
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<ImageTrail, double>(nameof(Spacing), 72);

    /// <summary>How long a picture rests on the trail before it sinks away.</summary>
    public static readonly StyledProperty<TimeSpan> LifetimeProperty =
        AvaloniaProperty.Register<ImageTrail, TimeSpan>(nameof(Lifetime), TimeSpan.FromMilliseconds(900));

    /// <summary>The most pictures on the trail at once; the oldest gives way to a new one.</summary>
    public static readonly StyledProperty<int> MaxImagesProperty =
        AvaloniaProperty.Register<ImageTrail, int>(nameof(MaxImages), 14);

    /// <summary>The corner radius of every picture.</summary>
    public static readonly StyledProperty<CornerRadius> ImageCornerRadiusProperty =
        AvaloniaProperty.Register<ImageTrail, CornerRadius>(nameof(ImageCornerRadius));

    /// <summary>The accessible name of the surface.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ImageTrail, string>(nameof(Label), "Image trail");

    private readonly ImageTrailModel _model = new();
    private readonly LiveLoop _loop;
    private readonly List<ArcImage> _loaders = [];
    private LazyFocusRing? _focusRing;
    private ImageTrailLayer? _layer;
    private Panel? _loaderHost;
    private Point _keyCursor;
    private bool _keyCursorPlaced;
    private bool _ownsName;

    static ImageTrail()
    {
        FocusableProperty.OverrideDefaultValue<ImageTrail>(true);
        ClipToBoundsProperty.OverrideDefaultValue<ImageTrail>(false);
    }

    public ImageTrail()
    {
        _loop = new LiveLoop(this, Step) { MinimumLevel = ArcMotionLevel.Reduced };
        AddHandler(PointerMovedEvent, OnPointerMovedAnywhere, handledEventsToo: true);
        AddHandler(PointerExitedEvent, (_, _) => _model.Lift(), handledEventsToo: true);
        AddHandler(PointerReleasedEvent, (_, e) =>
        {
            if (e.Pointer.Type != PointerType.Mouse)
            {
                _model.Lift();
            }
        }, handledEventsToo: true);
    }

    /// <inheritdoc cref="SourcesProperty"/>
    public IReadOnlyList<ImageSource>? Sources
    {
        get => GetValue(SourcesProperty);
        set => SetValue(SourcesProperty, value);
    }

    /// <inheritdoc cref="ImageSizeProperty"/>
    public double ImageSize
    {
        get => GetValue(ImageSizeProperty);
        set => SetValue(ImageSizeProperty, value);
    }

    /// <inheritdoc cref="SpacingProperty"/>
    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    /// <inheritdoc cref="LifetimeProperty"/>
    public TimeSpan Lifetime
    {
        get => GetValue(LifetimeProperty);
        set => SetValue(LifetimeProperty, value);
    }

    /// <inheritdoc cref="MaxImagesProperty"/>
    public int MaxImages
    {
        get => GetValue(MaxImagesProperty);
        set => SetValue(MaxImagesProperty, value);
    }

    /// <inheritdoc cref="ImageCornerRadiusProperty"/>
    public CornerRadius ImageCornerRadius
    {
        get => GetValue(ImageCornerRadiusProperty);
        set => SetValue(ImageCornerRadiusProperty, value);
    }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>The number of pictures on the trail right now.</summary>
    public int VisibleImages => _model.LiveCount;

    internal ImageTrailModel Model => _model;

    /// <summary>Whether pictures pop, tilt and sink (full motion) or only fade (reduced).</summary>
    internal bool MovesImages => ArcMotion.IsFull(this);

    /// <summary>Drops a picture at <paramref name="point"/> (in this control's coordinates), as the pointer would.</summary>
    public void DropAt(Point point)
    {
        if (!CanTrail())
        {
            return;
        }
        var local = ToLayer(point);
        _model.Drop(local.X, local.Y, 0, 0);
        Wake();
    }

    /// <summary>Removes every picture from the trail at once.</summary>
    public void Clear()
    {
        _model.Clear();
        _layer?.InvalidateVisual();
    }

    /// <summary>The loaded picture <paramref name="index"/>, or null while it loads or when it failed.</summary>
    internal IImage? ImageAt(int index) => index >= 0 && index < _loaders.Count ? _loaders[index].CurrentImage : null;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_layer is not null)
        {
            _layer.Owner = null;
        }
        _loaderHost?.Children.Clear();
        _layer = e.NameScope.Find<ImageTrailLayer>(LayerPart);
        _loaderHost = e.NameScope.Find<Panel>(LoadersPart);
        if (_layer is not null)
        {
            _layer.Owner = this;
        }
        RebuildLoaders();
        _focusRing ??= new LazyFocusRing(this, ImageTrailTokens.Key(ImageTrailTokenKind.FocusRingMargin),
            ArcDesignTokens.Key(ArcDesignTokenKind.RadiusSurface));
        _focusRing.Attach(e.NameScope.Find<Panel>(RootPart));
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        ApplySettings();
        UpdateName();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == SourcesProperty)
        {
            _model.Clear();
            RebuildLoaders();
            _layer?.InvalidateVisual();
        }
        else if (property == ImageSizeProperty)
        {
            foreach (var loader in _loaders)
            {
                loader.Width = ImageSize;
                loader.Height = ImageSize;
            }
            _layer?.InvalidateVisual();
        }
        else if (property == SpacingProperty || property == LifetimeProperty)
        {
            ApplySettings();
        }
        else if (property == MaxImagesProperty)
        {
            _model.Resize(MaxImages);
            _layer?.InvalidateVisual();
        }
        else if (property == ImageCornerRadiusProperty)
        {
            _layer?.InvalidateVisual();
        }
        else if (property == LabelProperty)
        {
            UpdateName();
        }
        else if (property == ArcMotion.LevelProperty && ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off)
        {
            Clear();
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing?.Update();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing?.Update();
        _keyCursorPlaced = false;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.Source != this)
        {
            return;
        }
        var step = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? KeyStep * 3 : KeyStep;
        Vector? move = e.Key switch
        {
            Key.Left => new Vector(-step, 0),
            Key.Right => new Vector(step, 0),
            Key.Up => new Vector(0, -step),
            Key.Down => new Vector(0, step),
            _ => null
        };
        if (move is { } delta)
        {
            e.Handled = true;
            if (!_keyCursorPlaced)
            {
                PlaceKeyCursor();
            }
            var size = Bounds.Size;
            _keyCursor = new Point(Math.Clamp(_keyCursor.X + delta.X, 0, size.Width), Math.Clamp(_keyCursor.Y + delta.Y, 0, size.Height));
            TrailTo(_keyCursor);
            return;
        }
        if (e.Key is Key.Enter or Key.Space)
        {
            e.Handled = true;
            if (!_keyCursorPlaced)
            {
                PlaceKeyCursor();
            }
            DropAt(_keyCursor);
        }
        else if (e.Key == Key.Escape && _model.LiveCount > 0)
        {
            e.Handled = true;
            Clear();
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ImageTrailAutomationPeer(this);

    private void PlaceKeyCursor()
    {
        _keyCursor = new Point(Bounds.Width / 2, Bounds.Height / 2);
        _keyCursorPlaced = true;
        _model.Lift();
        TrailTo(_keyCursor);
    }

    private void OnPointerMovedAnywhere(object? sender, PointerEventArgs e)
    {
        // A finger leaves a trail only while it is down, so a swipe that scrolls the page stays a scroll.
        if (e.Pointer.Type != PointerType.Mouse && !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        TrailTo(e.GetPosition(this));
    }

    private void TrailTo(Point point)
    {
        if (!CanTrail())
        {
            return;
        }
        var local = ToLayer(point);
        if (_model.MoveTo(local.X, local.Y) > 0)
        {
            Wake();
        }
    }

    private bool CanTrail() =>
        _layer is not null && _loaders.Count > 0 && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off;

    private Point ToLayer(Point point) => _layer is null ? point : this.TranslatePoint(point, _layer) ?? point;

    private void Wake()
    {
        _loop.Start();
        _layer?.InvalidateVisual();
    }

    private bool Step(double dt)
    {
        var any = _model.Step(dt);
        _layer?.InvalidateVisual();
        return any;
    }

    private void ApplySettings()
    {
        _model.Spacing = Math.Max(4, Spacing);
        _model.Lifetime = Math.Max(0, Lifetime.TotalSeconds);
        if (_model.Capacity != Math.Max(1, MaxImages))
        {
            _model.Resize(MaxImages);
        }
    }

    /// <summary>One hidden <see cref="ArcImage"/> per picture loads it; the layer draws its current frame.</summary>
    private void RebuildLoaders()
    {
        _loaderHost?.Children.Clear();
        _loaders.Clear();
        var sources = Sources;
        _model.Pictures = sources?.Count ?? 0;
        if (sources is null || _loaderHost is null)
        {
            return;
        }
        foreach (var source in sources)
        {
            var loader = new ArcImage { Source = source, Stretch = Stretch.Uniform, Width = ImageSize, Height = ImageSize };
            loader.PropertyChanged += OnLoaderChanged;
            _loaders.Add(loader);
            _loaderHost.Children.Add(loader);
        }
    }

    private void OnLoaderChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ArcImage.CurrentImageProperty && _model.LiveCount > 0)
        {
            _layer?.InvalidateVisual();
        }
    }

    private void UpdateName()
    {
        if (AutomationProperties.GetName(this) is not { Length: > 0 } || _ownsName)
        {
            _ownsName = true;
            AutomationProperties.SetName(this, Label);
        }
    }

    private sealed class ImageTrailAutomationPeer(ImageTrail owner) : ContentControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetLocalizedControlTypeCore() => "image trail";
    }
}
