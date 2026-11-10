using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>The panel content of one item, created the first time the item opens and kept for later visits.</summary>
internal sealed class MegaMenuMorphPage : ContentPresenter
{
    public MegaMenuMorphPage(MegaMenuMorphItem item)
    {
        Item = item;
        RenderTransform = Shift;
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
        IsVisible = false;
        Sync();
    }

    public MegaMenuMorphItem Item { get; }

    /// <summary>The panel rectangle (menu coordinates) this page was laid out for.</summary>
    public Rect Home { get; set; }

    /// <summary>The content size the page measured at when it was last shown.</summary>
    public Size ContentSize { get; set; }

    public TranslateTransform Shift { get; } = new();

    /// <summary>Takes the item's content and template: content first, then the template, and no template for no content.</summary>
    public void Sync()
    {
        if (Item.Content is null)
        {
            ContentTemplate = null;
            Content = null;
        }
        else
        {
            Content = Item.Content;
            ContentTemplate = Item.ContentTemplate;
        }
        AutomationProperties.SetName(this, Item.Header ?? string.Empty);
    }
}

/// <summary>
/// Lays the pages out where their panel rectangles put them and clips them to the panel's current shape. Pages move only
/// when a new target is set; while the panel morphs, only the clip, the page shifts and opacities change.
/// </summary>
internal sealed class MegaMenuMorphPages : Panel
{
    private readonly MegaMenuMorphStage _stage;

    public MegaMenuMorphPages(MegaMenuMorphStage stage)
    {
        _stage = stage;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children)
        {
            if (child is MegaMenuMorphPage { IsVisible: true } page)
            {
                page.Measure(new Size(_stage.ContentMaxWidth, double.PositiveInfinity));
            }
        }
        return default;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var padding = _stage.PanelPadding;
        foreach (var child in Children)
        {
            if (child is MegaMenuMorphPage page)
            {
                page.Arrange(new Rect(page.Home.X - _stage.Origin + padding.Left, page.Home.Y + padding.Top,
                    page.ContentSize.Width, page.ContentSize.Height));
            }
        }
        return finalSize;
    }
}

/// <summary>
/// The surface under a <see cref="MegaMenuMorph"/>'s bar, hosted in its popup. It draws one panel (background, edge and
/// floating shadow) whose rectangle springs from item to item, and clips the item pages to it; the page being left slides
/// out toward where the pointer came from while the next one slides in from where it went.
/// </summary>
/// <remarks>
/// Code-driven because the panel's rectangle, the clip and two page offsets follow one interruptible spring and one swap
/// progress that start from wherever the previous morph was; a ControlTheme transition cannot retarget a rectangle that
/// several parts read. The stage spans the union of the old and the new panel so the popup only moves when a morph starts.
/// </remarks>
internal sealed class MegaMenuMorphStage : Control
{
    /// <summary>The panel's rectangle travels on a calm spring without overshoot, so it never pokes past its target.</summary>
    internal static readonly SpringSpec MorphSpring = SpringSpec.FromVisual(0.38, 0);

    internal static readonly TimeSpan SwapDuration = TimeSpan.FromMilliseconds(300);
    internal static readonly TimeSpan EnterDuration = TimeSpan.FromMilliseconds(200);
    internal static readonly TimeSpan ExitDuration = TimeSpan.FromMilliseconds(150);

    public static readonly StyledProperty<IBrush?> PanelBackgroundProperty =
        AvaloniaProperty.Register<MegaMenuMorphStage, IBrush?>(nameof(PanelBackground));

    public static readonly StyledProperty<IBrush?> PanelBorderBrushProperty =
        AvaloniaProperty.Register<MegaMenuMorphStage, IBrush?>(nameof(PanelBorderBrush));

    public static readonly StyledProperty<double> PanelBorderThicknessProperty =
        AvaloniaProperty.Register<MegaMenuMorphStage, double>(nameof(PanelBorderThickness), 1);

    public static readonly StyledProperty<CornerRadius> PanelCornerRadiusProperty =
        AvaloniaProperty.Register<MegaMenuMorphStage, CornerRadius>(nameof(PanelCornerRadius));

    public static readonly StyledProperty<BoxShadows> PanelShadowProperty =
        AvaloniaProperty.Register<MegaMenuMorphStage, BoxShadows>(nameof(PanelShadow));

    public static readonly StyledProperty<Thickness> PanelPaddingProperty =
        AvaloniaProperty.Register<MegaMenuMorphStage, Thickness>(nameof(PanelPadding));

    public static readonly StyledProperty<double> SlideDistanceProperty =
        AvaloniaProperty.Register<MegaMenuMorphStage, double>(nameof(SlideDistance), 36);

    private readonly MegaMenuMorphPages _pages;
    private readonly RectangleGeometry _clip = new();
    private readonly TranslateTransform _lift = new();
    private readonly ScaleTransform _scale = new();
    private MotionValue<Rect>? _rect;
    private MotionValue<double>? _swap;
    private MotionValue<double>? _presence;
    private MegaMenuMorphPage? _current;
    private MegaMenuMorphPage? _leaving;
    private Rect _panel;
    private Rect _extent;
    private int _direction;
    private bool _shown;
    private int _hideGeneration;
    private Pen? _pen;

    static MegaMenuMorphStage()
    {
        AffectsRender<MegaMenuMorphStage>(PanelBackgroundProperty, PanelBorderBrushProperty, PanelBorderThicknessProperty,
            PanelCornerRadiusProperty, PanelShadowProperty);
        AffectsMeasure<MegaMenuMorphStage>(PanelPaddingProperty);
    }

    public MegaMenuMorphStage()
    {
        _pages = new MegaMenuMorphPages(this) { Clip = _clip };
        LogicalChildren.Add(_pages);
        VisualChildren.Add(_pages);
        RenderTransform = new TransformGroup { Children = { _scale, _lift } };
        AutomationProperties.SetLandmarkType(this, AutomationLandmarkType.Region);
    }

    public IBrush? PanelBackground
    {
        get => GetValue(PanelBackgroundProperty);
        set => SetValue(PanelBackgroundProperty, value);
    }

    public IBrush? PanelBorderBrush
    {
        get => GetValue(PanelBorderBrushProperty);
        set => SetValue(PanelBorderBrushProperty, value);
    }

    public double PanelBorderThickness
    {
        get => GetValue(PanelBorderThicknessProperty);
        set => SetValue(PanelBorderThicknessProperty, value);
    }

    public CornerRadius PanelCornerRadius
    {
        get => GetValue(PanelCornerRadiusProperty);
        set => SetValue(PanelCornerRadiusProperty, value);
    }

    public BoxShadows PanelShadow
    {
        get => GetValue(PanelShadowProperty);
        set => SetValue(PanelShadowProperty, value);
    }

    public Thickness PanelPadding
    {
        get => GetValue(PanelPaddingProperty);
        set => SetValue(PanelPaddingProperty, value);
    }

    public double SlideDistance
    {
        get => GetValue(SlideDistanceProperty);
        set => SetValue(SlideDistanceProperty, value);
    }

    /// <summary>The popup that hosts the stage: its horizontal offset follows <see cref="Origin"/>.</summary>
    public ArcPopup? Popup { get; set; }

    /// <summary>Where a page of a given panel size goes (the menu centres it under its trigger inside the viewport).</summary>
    public Func<MegaMenuMorphPage, Size, Rect>? Place { get; set; }

    /// <summary>The widest content a page is measured at (set by the menu from its panel limit and the viewport).</summary>
    public double ContentMaxWidth { get; set; } = double.PositiveInfinity;

    /// <summary>Left edge of the stage in the menu's coordinates.</summary>
    public double Origin => _extent.X;

    /// <summary>The panel's rectangle now, in the menu's coordinates.</summary>
    public Rect PanelRect => _panel;

    /// <summary>The panel's rectangle once the current morph settles.</summary>
    public Rect TargetRect => _current?.Home ?? default;

    /// <summary>The item whose page is showing (or arriving).</summary>
    public MegaMenuMorphItem? CurrentItem => _current?.Item;

    /// <summary>The page being replaced, while it is still on its way out.</summary>
    internal MegaMenuMorphPage? LeavingPage => _leaving;

    internal MegaMenuMorphPage? CurrentPage => _current;

    /// <summary>True from <see cref="Show"/> until <see cref="Hide"/> has finished.</summary>
    public bool IsShown => _shown;

    /// <summary>Raised when a hide has finished and the popup can close.</summary>
    public event EventHandler? Hidden;

    /// <summary>The page of <paramref name="item"/>, created on first use.</summary>
    public MegaMenuMorphPage PageFor(MegaMenuMorphItem item)
    {
        foreach (var child in _pages.Children)
        {
            if (child is MegaMenuMorphPage page && page.Item == item)
            {
                return page;
            }
        }
        var created = new MegaMenuMorphPage(item);
        _pages.Children.Add(created);
        return created;
    }

    /// <summary>Drops the pages of items that left the menu.</summary>
    public void Prune(IReadOnlyCollection<MegaMenuMorphItem> items)
    {
        for (var i = _pages.Children.Count - 1; i >= 0; i--)
        {
            if (_pages.Children[i] is MegaMenuMorphPage page && !items.Contains(page.Item))
            {
                if (page == _current || page == _leaving)
                {
                    continue;
                }
                page.Content = null;
                _pages.Children.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Measures <paramref name="page"/> and returns the panel size it asks for (content plus padding), at most
    /// <see cref="ContentMaxWidth"/> wide.
    /// </summary>
    public Size MeasurePanel(MegaMenuMorphPage page)
    {
        page.IsVisible = true;
        page.Measure(new Size(ContentMaxWidth, double.PositiveInfinity));
        var content = page.DesiredSize;
        page.ContentSize = new Size(Math.Min(content.Width, ContentMaxWidth), content.Height);
        var padding = PanelPadding;
        return new Size(page.ContentSize.Width + padding.Left + padding.Right, page.ContentSize.Height + padding.Top + padding.Bottom);
    }

    /// <summary>
    /// Shows <paramref name="page"/> in the panel at <paramref name="target"/> (menu coordinates). The first page of an
    /// opening fades in where it belongs; a later one morphs the panel there while the pages swap toward
    /// <paramref name="direction"/>.
    /// </summary>
    public void Show(MegaMenuMorphPage page, Rect target, int direction, bool animate)
    {
        _hideGeneration++;
        var full = animate && ArcMotion.IsFull(this);
        var wasShown = _shown && _current is not null;
        page.Home = target;
        page.IsVisible = true;

        if (!wasShown)
        {
            ClearLeaving();
            if (_current is not null && _current != page)
            {
                _current.IsVisible = false;
            }
            _current = page;
            _direction = 0;
            _shown = true;
            SetExtent(target);
            RectValue().Jump(target);
            SwapValue().Jump(1);
            var presence = PresenceValue();
            if (animate)
            {
                _ = presence.AnimateTo(1, MotionSpec.Tween(EnterDuration, ArcEasings.Enter));
            }
            else
            {
                presence.Jump(1);
            }
            ApplyPresence();
            ApplyPages();
            InvalidateArrange();
            _pages.InvalidateArrange();
            return;
        }

        if (page == _current)
        {
            // Same item: only the size or place changed (content grew, the viewport moved).
            SetExtent(MegaMenuMorphMath.Union(_panel, target));
            Retarget(target, animate);
            _pages.InvalidateArrange();
            if (_presence is { Target: < 1 })
            {
                _ = _presence.AnimateTo(1, MotionSpec.Tween(EnterDuration, ArcEasings.Enter));
            }
            return;
        }

        if (page == _leaving)
        {
            _leaving = null;
        }
        ClearLeaving();
        _leaving = _current;
        _current = page;
        _direction = direction;
        var extent = MegaMenuMorphMath.Union(_panel, target);
        if (_leaving is not null)
        {
            extent = MegaMenuMorphMath.Union(extent, _leaving.Home);
        }
        SetExtent(extent);
        Retarget(target, animate);
        var swap = SwapValue();
        swap.Jump(0);
        if (animate && _leaving is not null)
        {
            _ = RunSwap(swap, full);
        }
        else
        {
            swap.Jump(1);
            ClearLeaving();
        }
        if (_presence is { Target: < 1 })
        {
            _ = _presence.AnimateTo(1, MotionSpec.Tween(EnterDuration, ArcEasings.Enter));
        }
        ApplyPages();
        _pages.InvalidateArrange();
    }

    /// <summary>Fades the panel out (it lifts a little at the full motion level), then raises <see cref="Hidden"/>.</summary>
    public void Hide(bool animate)
    {
        if (_shown)
        {
            _ = HideAsync(animate);
        }
    }

    private async Task HideAsync(bool animate)
    {
        var generation = ++_hideGeneration;
        var presence = PresenceValue();
        var done = true;
        if (animate)
        {
            done = await presence.AnimateTo(0, MotionSpec.Tween(ExitDuration, ArcEasings.Standard));
        }
        else
        {
            presence.Jump(0);
        }
        if (!done || generation != _hideGeneration)
        {
            return;
        }
        Reset();
        Hidden?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Puts every page away at once (the popup closed on its own, or the menu left the tree).</summary>
    public void Reset()
    {
        _hideGeneration++;
        _shown = false;
        ClearLeaving();
        if (_current is not null)
        {
            _current.IsVisible = false;
            _current = null;
        }
        _rect?.Stop();
        _swap?.Stop();
        _presence?.Jump(0);
        _panel = default;
        InvalidateVisual();
    }

    private async Task RunSwap(MotionValue<double> swap, bool full)
    {
        var duration = full ? SwapDuration : ArcMotionPresets.Instant;
        var leaving = _leaving;
        var completed = await swap.AnimateTo(1, MotionSpec.Tween(duration, ArcEasings.Standard));
        if (completed && _leaving == leaving)
        {
            ClearLeaving();
            SettleExtent();
        }
    }

    private void Retarget(Rect target, bool animate)
    {
        var rect = RectValue();
        if (animate)
        {
            _ = SettleAfter(rect.AnimateTo(target, MotionSpec.Spring(MorphSpring)));
        }
        else
        {
            rect.Jump(target);
            SettleExtent();
        }
    }

    private async Task SettleAfter(Task<bool> motion)
    {
        if (await motion)
        {
            SettleExtent();
        }
    }

    /// <summary>Once nothing moves, the stage shrinks back to the panel so it covers no more of the page than it shows.</summary>
    private void SettleExtent()
    {
        if (_current is null || _leaving is not null || _rect is { IsAnimating: true })
        {
            return;
        }
        SetExtent(_current.Home);
    }

    private void SetExtent(Rect extent)
    {
        if (extent == _extent)
        {
            return;
        }
        var moved = Math.Abs(extent.X - _extent.X) > 0.01;
        _extent = extent;
        InvalidateMeasure();
        _pages.InvalidateArrange();
        if (moved && Popup is { } popup)
        {
            popup.HorizontalOffset = extent.X;
        }
        UpdateOrigin();
        ApplyClip();
        InvalidateVisual();
    }

    private void ClearLeaving()
    {
        if (_leaving is null)
        {
            return;
        }
        _leaving.IsVisible = false;
        _leaving.Opacity = 1;
        _leaving.Shift.X = 0;
        _leaving = null;
    }

    private MotionValue<Rect> RectValue()
    {
        if (_rect is null)
        {
            _rect = new MotionValue<Rect>(this, default, MotionVectors.Rect);
            _rect.Changed += OnRectChanged;
        }
        return _rect;
    }

    private MotionValue<double> SwapValue()
    {
        if (_swap is null)
        {
            _swap = new MotionValue<double>(this, 1, MotionVectors.Double);
            _swap.Changed += _ => ApplyPages();
        }
        return _swap;
    }

    private MotionValue<double> PresenceValue()
    {
        if (_presence is null)
        {
            _presence = new MotionValue<double>(this, 0, MotionVectors.Double);
            _presence.Changed += _ => ApplyPresence();
        }
        return _presence;
    }

    private void OnRectChanged(Rect rect)
    {
        _panel = rect;
        ApplyClip();
        InvalidateVisual();
    }

    private void ApplyClip()
    {
        var local = _panel.Translate(new Vector(-_extent.X, -_extent.Y));
        var inset = Math.Max(0, PanelBorderThickness);
        _clip.Rect = local.Deflate(inset);
        var radius = Math.Max(0, PanelCornerRadius.TopLeft - inset);
        _clip.RadiusX = radius;
        _clip.RadiusY = radius;
        _pages.InvalidateVisual();
    }

    private void ApplyPages()
    {
        var progress = _swap?.Value ?? 1;
        var full = ArcMotion.IsFull(this);
        var distance = full ? SlideDistance : 0;
        if (_current is not null)
        {
            var (offset, opacity) = _leaving is null ? (0, 1) : MegaMenuMorphMath.Incoming(progress, _direction, distance);
            _current.Shift.X = offset;
            _current.Opacity = opacity;
        }
        if (_leaving is not null)
        {
            var (offset, opacity) = MegaMenuMorphMath.Outgoing(progress, _direction, distance);
            _leaving.Shift.X = offset;
            _leaving.Opacity = opacity;
        }
    }

    private void ApplyPresence()
    {
        var p = _presence?.Value ?? 0;
        Opacity = p;
        IsHitTestVisible = p > 0.01 && (_presence?.Target ?? 0) > 0;
        if (ArcMotion.IsFull(this))
        {
            _lift.Y = -6 * (1 - p);
            var s = 0.985 + 0.015 * p;
            _scale.ScaleX = s;
            _scale.ScaleY = s;
        }
        else
        {
            _lift.Y = 0;
            _scale.ScaleX = 1;
            _scale.ScaleY = 1;
        }
    }

    private void UpdateOrigin()
    {
        var width = Math.Max(1, _extent.Width);
        var anchor = (_current?.Home ?? _panel).Center.X - _extent.X;
        RenderTransformOrigin = new RelativePoint(Math.Clamp(anchor / width, 0, 1), 0, RelativeUnit.Relative);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _pages.Measure(availableSize);
        if (_current is { IsVisible: true } current && _shown)
        {
            var padding = PanelPadding;
            var desired = current.DesiredSize;
            var content = new Size(Math.Min(desired.Width, ContentMaxWidth), desired.Height);
            if (Math.Abs(content.Width - current.ContentSize.Width) > 0.5 || Math.Abs(content.Height - current.ContentSize.Height) > 0.5)
            {
                // The open page's content changed size: the panel follows it on the same spring.
                current.ContentSize = content;
                var home = current.Home;
                var size = new Size(content.Width + padding.Left + padding.Right, content.Height + padding.Top + padding.Bottom);
                var target = Place?.Invoke(current, size) ?? new Rect(home.Center.X - size.Width / 2, home.Y, size.Width, size.Height);
                current.Home = target;
                _extent = MegaMenuMorphMath.Union(_extent, target);
                Retarget(target, ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off);
            }
        }
        return _shown ? _extent.Size : default;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _pages.Arrange(new Rect(finalSize));
        return finalSize;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PanelBorderBrushProperty || change.Property == PanelBorderThicknessProperty)
        {
            _pen = null;
        }
        if (change.Property == PanelCornerRadiusProperty || change.Property == PanelBorderThicknessProperty)
        {
            ApplyClip();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (!_shown)
        {
            return;
        }
        // Settle at once: a stage that left the tree has no frames to finish a morph with.
        _rect?.Jump(_current?.Home ?? _panel);
        _swap?.Jump(1);
        ClearLeaving();
    }

    public override void Render(DrawingContext context)
    {
        if (!_shown || _panel.Width <= 0 || _panel.Height <= 0)
        {
            return;
        }
        var local = _panel.Translate(new Vector(-_extent.X, -_extent.Y));
        if (_pen is null && PanelBorderBrush is { } brush && PanelBorderThickness > 0)
        {
            _pen = new Pen(brush, PanelBorderThickness);
        }
        var inset = PanelBorderThickness / 2;
        var shape = new RoundedRect(local.Deflate(inset), Math.Max(0, PanelCornerRadius.TopLeft - inset));
        context.DrawRectangle(PanelBackground ?? Brushes.Transparent, _pen, shape, PanelShadow);
    }
}
