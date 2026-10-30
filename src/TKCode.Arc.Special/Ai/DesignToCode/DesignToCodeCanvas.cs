using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>A region on the design side: its box, its tag and how far it has faded in.</summary>
internal sealed class DesignToCodeRegionView
{
    public DesignToCodeRegionView(DesignToCodeRegion region, DesignToCodeRegionTag tag)
    {
        Region = region;
        Tag = tag;
    }

    public DesignToCodeRegion Region { get; set; }

    public DesignToCodeRegionTag Tag { get; }

    public bool IsRevealed { get; set; }

    public double Opacity { get; set; }

    public MotionValue<double>? Fade { get; set; }
}

/// <summary>
/// The design side's drawing area: the screenshot (<see cref="DesignToCodeShot"/>) fitted inside a padding, the region
/// boxes and the scan line above it (<see cref="DesignToCodeOverlay"/>) and one tag button per region, level with its
/// box's top in the margin to its left (inside the box when there is no room). Pointing at a box or a tag reports the region; focusing a tag reports it as well.
/// </summary>
internal sealed class DesignToCodeCanvas : Control
{
    public static readonly StyledProperty<double> ShotPaddingProperty =
        AvaloniaProperty.Register<DesignToCodeCanvas, double>(nameof(ShotPadding), 24);

    /// <summary>The space between a tag and its box.</summary>
    private const double TagGap = 4;

    private readonly List<DesignToCodeRegionView> _views = [];
    private string? _highlighted;
    private string? _active;
    private double _scan = double.NaN;
    private string? _hovered;
    private Rect _shotRect;

    static DesignToCodeCanvas()
    {
        AffectsArrange<DesignToCodeCanvas>(ShotPaddingProperty);
    }

    public DesignToCodeCanvas()
    {
        Shot = new DesignToCodeShot();
        Overlay = new DesignToCodeOverlay(this) { IsHitTestVisible = false };
        VisualChildren.Add(Shot);
        LogicalChildren.Add(Shot);
        VisualChildren.Add(Overlay);
        LogicalChildren.Add(Overlay);
        Focusable = false;
    }

    public double ShotPadding { get => GetValue(ShotPaddingProperty); set => SetValue(ShotPaddingProperty, value); }

    /// <summary>The design's size when the pane is unbounded and the design names none.</summary>
    internal static readonly Size DefaultDesignSize = new(240, 300);

    public DesignToCodeShot Shot { get; }

    public DesignToCodeOverlay Overlay { get; }

    /// <summary>The regions in the order they were found.</summary>
    public IReadOnlyList<DesignToCodeRegionView> Views => _views;

    /// <summary>Where the screenshot sits, in the canvas's coordinates.</summary>
    public Rect ShotRect => _shotRect;

    /// <summary>The tag's accessible name, from the region's label.</summary>
    public Func<string, string> TagName { get; set; } = label => label;

    /// <summary>Raised when the pointer moves onto a region or off every region (null).</summary>
    public event Action<string?>? HoverChanged;

    /// <summary>Raised when a tag gets keyboard or pointer focus (the region) or loses it (null).</summary>
    public event Action<string?>? TagFocusChanged;

    /// <summary>Raised when a tag is pressed.</summary>
    public event Action<string>? TagInvoked;

    /// <summary>The design drawn.</summary>
    public DesignToCodeSource? Source
    {
        get => Shot.Source;
        set
        {
            Shot.Source = value;
            InvalidateArrange();
        }
    }

    /// <summary>The region pointed at or focused: its box is filled and its tag inverted.</summary>
    public string? Highlighted
    {
        get => _highlighted;
        set
        {
            if (_highlighted == value)
            {
                return;
            }
            _highlighted = value;
            UpdateTags();
            Overlay.InvalidateVisual();
        }
    }

    /// <summary>The region being written: highlighted like <see cref="Highlighted"/>.</summary>
    public string? Active
    {
        get => _active;
        set
        {
            if (_active == value)
            {
                return;
            }
            _active = value;
            UpdateTags();
            Overlay.InvalidateVisual();
        }
    }

    /// <summary>Where the scan line is, from 0 (top of the screenshot) to 1 (bottom); NaN hides it.</summary>
    public double ScanPosition
    {
        get => _scan;
        set
        {
            if (_scan.Equals(value))
            {
                return;
            }
            _scan = value;
            Overlay.InvalidateVisual();
        }
    }

    /// <summary>Whether <paramref name="id"/> is drawn highlighted.</summary>
    public bool IsHighlighted(string id) => id == _highlighted || id == _active;

    /// <summary>Adds a region (hidden until revealed), or replaces the region with the same id in place.</summary>
    public DesignToCodeRegionView SetRegion(DesignToCodeRegion region)
    {
        foreach (var existing in _views)
        {
            if (existing.Region.Id == region.Id)
            {
                existing.Region = region;
                existing.Tag.Content = region.Label;
                AutomationProperties.SetName(existing.Tag, TagName(region.Label));
                InvalidateArrange();
                Overlay.InvalidateVisual();
                return existing;
            }
        }
        var tag = new DesignToCodeRegionTag { Content = region.Label, RegionId = region.Id, IsVisible = false, Opacity = 0 };
        AutomationProperties.SetName(tag, TagName(region.Label));
        tag.Click += OnTagClick;
        tag.GotFocus += OnTagGotFocus;
        tag.LostFocus += OnTagLostFocus;
        var view = new DesignToCodeRegionView(region, tag);
        _views.Add(view);
        VisualChildren.Add(tag);
        LogicalChildren.Add(tag);
        UpdateTags();
        InvalidateMeasure();
        return view;
    }

    /// <summary>Shows a region's box and tag: a 160ms fade, or at once.</summary>
    public void Reveal(DesignToCodeRegionView view, bool animate)
    {
        if (view.IsRevealed)
        {
            return;
        }
        view.IsRevealed = true;
        view.Tag.IsVisible = true;
        if (!animate || !this.IsAttachedToVisualTree())
        {
            SetOpacity(view, 1);
            return;
        }
        // Code-driven: the box is drawn by the overlay, which has no property per box for a ControlTheme to animate.
        view.Fade ??= new MotionValue<double>(this, view.Opacity, MotionVectors.Double);
        view.Fade.Changed += value => SetOpacity(view, value);
        SetOpacity(view, 0);
        _ = view.Fade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
    }

    /// <summary>Shows every region.</summary>
    public void RevealAll(bool animate)
    {
        foreach (var view in _views)
        {
            Reveal(view, animate);
        }
    }

    /// <summary>Removes every region.</summary>
    public void ClearRegions()
    {
        foreach (var view in _views)
        {
            view.Fade?.Dispose();
            view.Tag.Click -= OnTagClick;
            view.Tag.GotFocus -= OnTagGotFocus;
            view.Tag.LostFocus -= OnTagLostFocus;
            VisualChildren.Remove(view.Tag);
            LogicalChildren.Remove(view.Tag);
        }
        _views.Clear();
        SetHovered(null);
        InvalidateMeasure();
        Overlay.InvalidateVisual();
    }

    /// <summary>The box of a region in the canvas's coordinates.</summary>
    public Rect BoxOf(DesignToCodeRegion region)
    {
        var f = region.Fraction;
        return new Rect(_shotRect.X + f.X * _shotRect.Width, _shotRect.Y + f.Y * _shotRect.Height,
            f.Width * _shotRect.Width, f.Height * _shotRect.Height);
    }

    /// <summary>Re-reads the tags' accessible names (the labels changed).</summary>
    public void RefreshTagNames()
    {
        foreach (var view in _views)
        {
            AutomationProperties.SetName(view.Tag, TagName(view.Region.Label));
        }
    }

    private void SetOpacity(DesignToCodeRegionView view, double value)
    {
        view.Opacity = value;
        view.Tag.Opacity = value;
        Overlay.InvalidateVisual();
    }

    private void UpdateTags()
    {
        foreach (var view in _views)
        {
            view.Tag.SetHighlighted(IsHighlighted(view.Region.Id));
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        Shot.Measure(availableSize);
        Overlay.Measure(availableSize);
        foreach (var view in _views)
        {
            view.Tag.Measure(Size.Infinity);
        }
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : DefaultDesignSize.Width + 2 * ShotPadding;
        var height = double.IsFinite(availableSize.Height) ? availableSize.Height : DefaultDesignSize.Height + 2 * ShotPadding;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var padding = ShotPadding;
        var inner = new Rect(finalSize).Deflate(padding);
        if (inner.Width <= 0 || inner.Height <= 0)
        {
            inner = new Rect(finalSize);
        }
        var aspect = Shot.AspectRatio;
        var width = inner.Width;
        var height = width / aspect;
        if (height > inner.Height)
        {
            height = inner.Height;
            width = height * aspect;
        }
        _shotRect = new Rect(Math.Round(inner.X + (inner.Width - width) / 2), Math.Round(inner.Y + (inner.Height - height) / 2),
            Math.Round(width), Math.Round(height));
        Shot.Arrange(_shotRect);
        Overlay.Arrange(new Rect(finalSize));
        foreach (var view in _views)
        {
            var box = BoxOf(view.Region);
            var size = view.Tag.DesiredSize;
            // The tag sits in the margin left of the box, level with its top, so it never covers the screenshot; when the
            // margin is too narrow (a wide screenshot) it goes inside the box's top left corner.
            var x = box.X - TagGap - size.Width;
            var y = box.Y;
            if (x < 0)
            {
                x = box.X + TagGap;
                y = box.Y + TagGap;
            }
            x = Math.Clamp(x, 0, Math.Max(0, finalSize.Width - size.Width));
            y = Math.Clamp(y, 0, Math.Max(0, finalSize.Height - size.Height));
            view.Tag.Arrange(new Rect(new Point(Math.Round(x), Math.Round(y)), size));
        }
        return finalSize;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        if (e.Source is Visual source && source.FindAncestorOfType<DesignToCodeRegionTag>(includeSelf: true) is { } tag)
        {
            SetHovered(tag.RegionId);
            return;
        }
        SetHovered(RegionAt(e.GetPosition(this)));
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        SetHovered(null);
    }

    /// <summary>The smallest revealed box under <paramref name="point"/>.</summary>
    internal string? RegionAt(Point point)
    {
        string? best = null;
        var area = double.MaxValue;
        foreach (var view in _views)
        {
            if (!view.IsRevealed)
            {
                continue;
            }
            var box = BoxOf(view.Region);
            if (box.Contains(point) && box.Width * box.Height < area)
            {
                area = box.Width * box.Height;
                best = view.Region.Id;
            }
        }
        return best;
    }

    private void SetHovered(string? id)
    {
        if (_hovered == id)
        {
            return;
        }
        _hovered = id;
        HoverChanged?.Invoke(id);
    }

    private void OnTagClick(object? sender, RoutedEventArgs e)
    {
        if (sender is DesignToCodeRegionTag { RegionId: { } id })
        {
            TagInvoked?.Invoke(id);
        }
    }

    private void OnTagGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (sender is DesignToCodeRegionTag { RegionId: { } id })
        {
            TagFocusChanged?.Invoke(id);
        }
    }

    private void OnTagLostFocus(object? sender, FocusChangedEventArgs e) => TagFocusChanged?.Invoke(null);
}

/// <summary>
/// The boxes and the scan line over the screenshot. A box is a 1px accent outline at half strength; a highlighted box
/// (pointed at, focused or being written) gets a stronger outline and a soft accent fill. The scan line is a 1px accent
/// line with a soft band trailing above it.
/// </summary>
internal sealed class DesignToCodeOverlay : Control
{
    public static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<DesignToCodeOverlay, IBrush?>(nameof(AccentBrush));

    public static readonly StyledProperty<CornerRadius> BoxCornerRadiusProperty =
        AvaloniaProperty.Register<DesignToCodeOverlay, CornerRadius>(nameof(BoxCornerRadius), new CornerRadius(6));

    public static readonly StyledProperty<double> BoxStrokeThicknessProperty =
        AvaloniaProperty.Register<DesignToCodeOverlay, double>(nameof(BoxStrokeThickness), 1);

    public static readonly StyledProperty<double> BoxHighlightStrokeThicknessProperty =
        AvaloniaProperty.Register<DesignToCodeOverlay, double>(nameof(BoxHighlightStrokeThickness), 1.5);

    public static readonly StyledProperty<double> BoxRestOpacityProperty =
        AvaloniaProperty.Register<DesignToCodeOverlay, double>(nameof(BoxRestOpacity), 0.5);

    public static readonly StyledProperty<double> BoxFillOpacityProperty =
        AvaloniaProperty.Register<DesignToCodeOverlay, double>(nameof(BoxFillOpacity), 0.14);

    public static readonly StyledProperty<double> ScanLineThicknessProperty =
        AvaloniaProperty.Register<DesignToCodeOverlay, double>(nameof(ScanLineThickness), 1);

    public static readonly StyledProperty<double> ScanBandHeightProperty =
        AvaloniaProperty.Register<DesignToCodeOverlay, double>(nameof(ScanBandHeight), 28);

    private readonly DesignToCodeCanvas _canvas;
    private IPen? _restPen;
    private IPen? _highlightPen;
    private IBrush? _fill;
    private IBrush? _band;

    static DesignToCodeOverlay()
    {
        AffectsRender<DesignToCodeOverlay>(AccentBrushProperty, BoxCornerRadiusProperty, BoxStrokeThicknessProperty,
            BoxHighlightStrokeThicknessProperty, BoxRestOpacityProperty, BoxFillOpacityProperty, ScanLineThicknessProperty, ScanBandHeightProperty);
    }

    public DesignToCodeOverlay(DesignToCodeCanvas canvas) => _canvas = canvas;

    public IBrush? AccentBrush { get => GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }

    public CornerRadius BoxCornerRadius { get => GetValue(BoxCornerRadiusProperty); set => SetValue(BoxCornerRadiusProperty, value); }

    public double BoxStrokeThickness { get => GetValue(BoxStrokeThicknessProperty); set => SetValue(BoxStrokeThicknessProperty, value); }

    public double BoxHighlightStrokeThickness { get => GetValue(BoxHighlightStrokeThicknessProperty); set => SetValue(BoxHighlightStrokeThicknessProperty, value); }

    public double BoxRestOpacity { get => GetValue(BoxRestOpacityProperty); set => SetValue(BoxRestOpacityProperty, value); }

    public double BoxFillOpacity { get => GetValue(BoxFillOpacityProperty); set => SetValue(BoxFillOpacityProperty, value); }

    public double ScanLineThickness { get => GetValue(ScanLineThicknessProperty); set => SetValue(ScanLineThicknessProperty, value); }

    public double ScanBandHeight { get => GetValue(ScanBandHeightProperty); set => SetValue(ScanBandHeightProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == AccentBrushProperty || change.Property == BoxStrokeThicknessProperty
            || change.Property == BoxHighlightStrokeThicknessProperty || change.Property == BoxRestOpacityProperty
            || change.Property == BoxFillOpacityProperty)
        {
            _restPen = null;
            _highlightPen = null;
            _fill = null;
            _band = null;
        }
    }

    private void EnsureBrushes()
    {
        if (_restPen is not null)
        {
            return;
        }
        var accent = (AccentBrush as ISolidColorBrush)?.Color ?? Colors.SteelBlue;
        _restPen = new ImmutablePen(new ImmutableSolidColorBrush(accent, BoxRestOpacity), BoxStrokeThickness);
        _highlightPen = new ImmutablePen(new ImmutableSolidColorBrush(accent), BoxHighlightStrokeThickness);
        _fill = new ImmutableSolidColorBrush(accent, BoxFillOpacity);
        _band = new ImmutableLinearGradientBrush(
            [new ImmutableGradientStop(0, Color.FromArgb(0, accent.R, accent.G, accent.B)), new ImmutableGradientStop(1, Color.FromArgb(46, accent.R, accent.G, accent.B))],
            startPoint: new RelativePoint(0, 0, RelativeUnit.Relative), endPoint: new RelativePoint(0, 1, RelativeUnit.Relative));
    }

    public override void Render(DrawingContext context)
    {
        var shot = _canvas.ShotRect;
        if (shot.Width <= 0 || shot.Height <= 0)
        {
            return;
        }
        EnsureBrushes();
        var radius = BoxCornerRadius;
        foreach (var view in _canvas.Views)
        {
            if (!view.IsRevealed || view.Opacity <= 0.004)
            {
                continue;
            }
            var box = _canvas.BoxOf(view.Region);
            var highlighted = _canvas.IsHighlighted(view.Region.Id);
            using (context.PushOpacity(view.Opacity))
            {
                var pen = highlighted ? _highlightPen! : _restPen!;
                var inset = pen.Thickness / 2;
                context.DrawRectangle(highlighted ? _fill : null, pen, new RoundedRect(box.Deflate(inset), radius));
            }
        }
        var scan = _canvas.ScanPosition;
        if (double.IsFinite(scan))
        {
            var y = shot.Y + Math.Clamp(scan, 0, 1) * shot.Height;
            var band = Math.Min(ScanBandHeight, y - shot.Y);
            using (context.PushClip(shot))
            {
                if (band > 0)
                {
                    context.FillRectangle(_band!, new Rect(shot.X, y - band, shot.Width, band));
                }
                context.FillRectangle(AccentBrush ?? Brushes.SteelBlue, new Rect(shot.X, y - ScanLineThickness / 2, shot.Width, ScanLineThickness));
            }
        }
    }
}

/// <summary>A region's tag: a small labelled button that flags its box. Screen readers hear "Price: show its code".</summary>
internal sealed class DesignToCodeRegionTag : AvaloniaButton
{
    internal const string HighlightedPseudoClass = ":highlighted";

    protected override Type StyleKeyOverride => typeof(DesignToCodeRegionTag);

    /// <summary>The region this tag flags.</summary>
    public string? RegionId { get; set; }

    internal void SetHighlighted(bool highlighted) => PseudoClasses.Set(HighlightedPseudoClass, highlighted);
}
