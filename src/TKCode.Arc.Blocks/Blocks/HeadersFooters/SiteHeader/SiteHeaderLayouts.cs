// Ported from Arc's free site-header block (MIT, Copyright (c) 2026 Elia Kuratli): the CSS layout rules of site-header.module.css.
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// The bar: brand, navigation and actions (children 0, 1, 2). Standard layouts put the navigation after the brand
/// (16 further in); the centered layout is three columns <c>1fr auto 1fr</c> with the navigation in the middle and the
/// actions at the right; below the collapse width the navigation is hidden and brand and actions sit at the two ends.
/// </summary>
internal sealed class SiteHeaderBar : Panel
{
    public static readonly StyledProperty<SiteHeaderVariant> VariantProperty =
        AvaloniaProperty.Register<SiteHeaderBar, SiteHeaderVariant>(nameof(Variant));

    public static readonly StyledProperty<double> GapProperty = AvaloniaProperty.Register<SiteHeaderBar, double>(nameof(Gap), 8);

    public static readonly StyledProperty<double> NavInsetProperty = AvaloniaProperty.Register<SiteHeaderBar, double>(nameof(NavInset), 16);

    static SiteHeaderBar()
    {
        AffectsMeasure<SiteHeaderBar>(VariantProperty, GapProperty, NavInsetProperty);
    }

    public SiteHeaderVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    public double NavInset { get => GetValue(NavInsetProperty); set => SetValue(NavInsetProperty, value); }

    private Control? Slot(int index) => index < Children.Count ? Children[index] : null;

    protected override Size MeasureOverride(Size availableSize)
    {
        var height = 0.0;
        var width = 0.0;
        for (var i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            if (child.IsVisible)
            {
                height = Math.Max(height, child.DesiredSize.Height);
                width += child.DesiredSize.Width;
            }
        }
        return new Size(double.IsInfinity(availableSize.Width) ? width : availableSize.Width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var brand = Slot(0);
        var nav = Slot(1);
        var actions = Slot(2);
        var width = finalSize.Width;
        var height = finalSize.Height;
        var brandW = brand is { IsVisible: true } ? brand.DesiredSize.Width : 0;
        var navW = nav is { IsVisible: true } ? nav.DesiredSize.Width : 0;
        var actionsW = actions is { IsVisible: true } ? actions.DesiredSize.Width : 0;
        var hasNav = nav is { IsVisible: true };

        if (hasNav && Variant == SiteHeaderVariant.Centered)
        {
            navW = Math.Min(navW, Math.Max(0, width - 2 * Gap));
            var column = Math.Max(0, (width - navW - 2 * Gap) / 2);
            Place(brand, 0, Math.Min(brandW, column), height);
            Place(nav, column + Gap, navW, height);
            var a = Math.Min(actionsW, column);
            Place(actions, width - a, a, height);
            return finalSize;
        }

        var actionsX = Math.Max(0, width - actionsW);
        Place(brand, 0, Math.Min(brandW, Math.Max(0, actionsX - Gap)), height);
        Place(actions, actionsX, Math.Min(actionsW, width), height);
        if (hasNav)
        {
            var x = Math.Min(width, brandW + Gap + NavInset);
            Place(nav, x, Math.Min(navW, Math.Max(0, actionsX - Gap - x)), height);
        }
        return finalSize;
    }

    private static void Place(Control? child, double x, double width, double height)
    {
        if (child is not { IsVisible: true })
        {
            return;
        }
        var h = Math.Min(child.DesiredSize.Height, height);
        child.Arrange(new Rect(x, (height - h) / 2, width, h));
    }
}

/// <summary>Where a child of a <see cref="SiteHeaderFloatLayer"/> goes.</summary>
internal enum SiteHeaderPlacement
{
    /// <summary>The mega panel: inset from both sides, a little above the bar's bottom, its own height.</summary>
    Panel,

    /// <summary>The scrim: from the bar's bottom, as tall as <see cref="SiteHeaderFloatLayer.ScrimHeight"/>.</summary>
    Scrim,

    /// <summary>The menu sheet: from the bar's bottom, edge to edge, its own height.</summary>
    Sheet
}

/// <summary>
/// Draws the panel, scrim and sheet outside the header's own bounds (Arc's absolutely positioned layers). It takes no
/// room itself; children are arranged absolutely below the bar and the layer never clips.
/// </summary>
internal sealed class SiteHeaderFloatLayer : Panel
{
    public static readonly AttachedProperty<SiteHeaderPlacement> PlacementProperty =
        AvaloniaProperty.RegisterAttached<SiteHeaderFloatLayer, Control, SiteHeaderPlacement>("Placement");

    public static readonly StyledProperty<double> PanelTopProperty = AvaloniaProperty.Register<SiteHeaderFloatLayer, double>(nameof(PanelTop), 60);

    public static readonly StyledProperty<double> PanelInsetProperty = AvaloniaProperty.Register<SiteHeaderFloatLayer, double>(nameof(PanelInset), 24);

    public static readonly StyledProperty<double> SheetTopProperty = AvaloniaProperty.Register<SiteHeaderFloatLayer, double>(nameof(SheetTop), 64);

    public static readonly StyledProperty<double> ScrimHeightProperty = AvaloniaProperty.Register<SiteHeaderFloatLayer, double>(nameof(ScrimHeight), 600);

    static SiteHeaderFloatLayer()
    {
        AffectsMeasure<SiteHeaderFloatLayer>(PanelTopProperty, PanelInsetProperty, SheetTopProperty, ScrimHeightProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<SiteHeaderFloatLayer>(true);
    }

    public static SiteHeaderPlacement GetPlacement(Control control) => control.GetValue(PlacementProperty);

    public static void SetPlacement(Control control, SiteHeaderPlacement value) => control.SetValue(PlacementProperty, value);

    public double PanelTop { get => GetValue(PanelTopProperty); set => SetValue(PanelTopProperty, value); }

    public double PanelInset { get => GetValue(PanelInsetProperty); set => SetValue(PanelInsetProperty, value); }

    public double SheetTop { get => GetValue(SheetTopProperty); set => SetValue(SheetTopProperty, value); }

    public double ScrimHeight { get => GetValue(ScrimHeightProperty); set => SetValue(ScrimHeightProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width;
        foreach (var child in Children)
        {
            switch (GetPlacement(child))
            {
                case SiteHeaderPlacement.Panel:
                    child.Measure(new Size(Math.Max(0, width - 2 * PanelInset), double.PositiveInfinity));
                    break;
                case SiteHeaderPlacement.Scrim:
                    child.Measure(new Size(width, ScrimHeight));
                    break;
                default:
                    child.Measure(new Size(width, double.PositiveInfinity));
                    break;
            }
        }
        return default;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            switch (GetPlacement(child))
            {
                case SiteHeaderPlacement.Panel:
                    child.Arrange(new Rect(PanelInset, PanelTop, Math.Max(0, finalSize.Width - 2 * PanelInset), child.DesiredSize.Height));
                    break;
                case SiteHeaderPlacement.Scrim:
                    child.Arrange(new Rect(0, SheetTop, finalSize.Width, ScrimHeight));
                    break;
                default:
                    child.Arrange(new Rect(0, SheetTop, finalSize.Width, child.DesiredSize.Height));
                    break;
            }
        }
        return finalSize;
    }
}

/// <summary>
/// The links of a panel face: two equal columns, rows as tall as their tallest cell; a taller grid (a feature card
/// beside it) spreads the extra height over the rows equally, like CSS auto tracks, and each link fills its cell.
/// </summary>
internal sealed class SiteHeaderLinkGrid : Panel
{
    public static readonly StyledProperty<double> GapProperty = AvaloniaProperty.Register<SiteHeaderLinkGrid, double>(nameof(Gap), 2);

    private double[] _rows = [];

    static SiteHeaderLinkGrid()
    {
        AffectsMeasure<SiteHeaderLinkGrid>(GapProperty);
    }

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        var count = Children.Count;
        var rows = (count + 1) / 2;
        var cell = double.IsInfinity(availableSize.Width) ? double.PositiveInfinity : Math.Max(0, (availableSize.Width - Gap) / 2);
        if (_rows.Length != rows)
        {
            _rows = new double[rows];
        }
        Array.Clear(_rows);
        var widest = 0.0;
        for (var i = 0; i < count; i++)
        {
            var child = Children[i];
            child.Measure(new Size(cell, double.PositiveInfinity));
            _rows[i / 2] = Math.Max(_rows[i / 2], child.DesiredSize.Height);
            widest = Math.Max(widest, child.DesiredSize.Width);
        }
        var height = _rows.Sum() + Math.Max(0, rows - 1) * Gap;
        return new Size(double.IsInfinity(cell) ? widest * 2 + Gap : availableSize.Width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var rows = _rows.Length;
        if (rows == 0)
        {
            return finalSize;
        }
        var natural = _rows.Sum() + (rows - 1) * Gap;
        var extra = Math.Max(0, finalSize.Height - natural) / rows;
        var cell = Math.Max(0, (finalSize.Width - Gap) / 2);
        var y = 0.0;
        for (var row = 0; row < rows; row++)
        {
            var h = _rows[row] + extra;
            for (var column = 0; column < 2; column++)
            {
                var index = row * 2 + column;
                if (index < Children.Count)
                {
                    Children[index].Arrange(new Rect(column * (cell + Gap), y, cell, h));
                }
            }
            y += h + Gap;
        }
        return finalSize;
    }
}

/// <summary>
/// One face of the mega panel: the link grid (child 0) and, when there is one, the feature card (child 1) in a column of
/// 200 to 260 (Arc: <c>minmax(0, 1fr) minmax(200px, 260px)</c>); both are as tall as the row.
/// </summary>
internal sealed class SiteHeaderFaceGrid : Panel
{
    public static readonly StyledProperty<double> GapProperty = AvaloniaProperty.Register<SiteHeaderFaceGrid, double>(nameof(Gap), 8);

    public static readonly StyledProperty<double> FeatureMinProperty = AvaloniaProperty.Register<SiteHeaderFaceGrid, double>(nameof(FeatureMin), 200);

    public static readonly StyledProperty<double> FeatureMaxProperty = AvaloniaProperty.Register<SiteHeaderFaceGrid, double>(nameof(FeatureMax), 260);

    private double _featureWidth;

    static SiteHeaderFaceGrid()
    {
        AffectsMeasure<SiteHeaderFaceGrid>(GapProperty, FeatureMinProperty, FeatureMaxProperty);
    }

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    public double FeatureMin { get => GetValue(FeatureMinProperty); set => SetValue(FeatureMinProperty, value); }

    public double FeatureMax { get => GetValue(FeatureMaxProperty); set => SetValue(FeatureMaxProperty, value); }

    /// <summary>The width the feature column got in the last measure (0 without a feature).</summary>
    public double FeatureWidth => _featureWidth;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = availableSize.Width;
        var hasFeature = Children.Count > 1;
        _featureWidth = hasFeature ? Math.Clamp(double.IsInfinity(width) ? FeatureMax : width - Gap, FeatureMin, FeatureMax) : 0;
        var main = double.IsInfinity(width) ? double.PositiveInfinity : Math.Max(0, width - (hasFeature ? Gap + _featureWidth : 0));
        var height = 0.0;
        if (Children.Count > 0)
        {
            Children[0].Measure(new Size(main, double.PositiveInfinity));
            height = Children[0].DesiredSize.Height;
        }
        if (hasFeature)
        {
            Children[1].Measure(new Size(_featureWidth, double.PositiveInfinity));
            height = Math.Max(height, Children[1].DesiredSize.Height);
        }
        return new Size(double.IsInfinity(width) ? (Children.Count > 0 ? Children[0].DesiredSize.Width : 0) + (hasFeature ? Gap + _featureWidth : 0) : width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var hasFeature = Children.Count > 1;
        var main = Math.Max(0, finalSize.Width - (hasFeature ? Gap + _featureWidth : 0));
        if (Children.Count > 0)
        {
            Children[0].Arrange(new Rect(0, 0, main, finalSize.Height));
        }
        if (hasFeature)
        {
            Children[1].Arrange(new Rect(main + Gap, 0, _featureWidth, finalSize.Height));
        }
        return finalSize;
    }
}

/// <summary>Gives its child a fixed width-to-height ratio (CSS <c>aspect-ratio</c>): as wide as offered, <c>width / Ratio</c> high.</summary>
internal sealed class SiteHeaderRatioBox : Decorator
{
    public static readonly StyledProperty<double> RatioProperty = AvaloniaProperty.Register<SiteHeaderRatioBox, double>(nameof(Ratio), 1.6);

    static SiteHeaderRatioBox()
    {
        AffectsMeasure<SiteHeaderRatioBox>(RatioProperty);
    }

    public double Ratio { get => GetValue(RatioProperty); set => SetValue(RatioProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width;
        var size = new Size(width, Ratio > 0 ? width / Ratio : 0);
        Child?.Measure(size);
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Child?.Arrange(new Rect(finalSize));
        return finalSize;
    }
}

/// <summary>
/// A box whose height springs to its content (or to an explicit <see cref="TargetHeight"/>): Arc's measured-height
/// panel (the panel body grows on GROW, shrinks on SHRINK), the menu sheet and the sheet groups. The first target is
/// taken without animation unless <see cref="StartCollapsed"/> (the box then opens from zero).
/// </summary>
/// <remarks>
/// Code-driven because a ControlTheme cannot animate to a measured content height and the physical spring keeps its
/// velocity when the target is retargeted in flight (Arc uses stiffness and damping for exactly that).
/// </remarks>
internal sealed class SiteHeightBox : Decorator
{
    public static readonly StyledProperty<double> TargetHeightProperty = AvaloniaProperty.Register<SiteHeightBox, double>(nameof(TargetHeight), double.NaN);

    public static readonly StyledProperty<bool> IsOpenProperty = AvaloniaProperty.Register<SiteHeightBox, bool>(nameof(IsOpen), true);

    public static readonly StyledProperty<bool> StartCollapsedProperty = AvaloniaProperty.Register<SiteHeightBox, bool>(nameof(StartCollapsed));

    private MotionValue<double>? _height;
    private MotionValue<double>? _fade;
    private double _natural;
    private double _applied = double.NaN;
    private bool _started;
    private bool _applyPending;

    static SiteHeightBox()
    {
        AffectsMeasure<SiteHeightBox>(TargetHeightProperty, IsOpenProperty);
        ClipToBoundsProperty.OverrideDefaultValue<SiteHeightBox>(true);
    }

    /// <summary>The height to take; NaN follows the content.</summary>
    public double TargetHeight { get => GetValue(TargetHeightProperty); set => SetValue(TargetHeightProperty, value); }

    /// <summary>False collapses a content-following box to zero.</summary>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <summary>True starts from zero height (the first target is then animated).</summary>
    public bool StartCollapsed { get => GetValue(StartCollapsedProperty); set => SetValue(StartCollapsedProperty, value); }

    /// <summary>True also fades the content with the height (opacity 1 while open, 0 when collapsed; 160 ms, no fade under reduced motion).</summary>
    public bool UseFade { get; set; }

    /// <summary>Spring when the target grows (Arc GROW, visualDuration .44).</summary>
    public SpringSpec GrowSpring { get; set; } = SpringSpec.FromVisual(0.44, 0);

    /// <summary>Spring when the target shrinks (Arc SHRINK, visualDuration .34).</summary>
    public SpringSpec ShrinkSpring { get; set; } = SpringSpec.FromVisual(0.34, 0);

    /// <summary>Raised when the height reaches its target (also at once when motion is off).</summary>
    public event EventHandler? Arrived;

    /// <summary>The content's own height in the last measure.</summary>
    public double NaturalHeight => _natural;

    /// <summary>The height now (animated).</summary>
    public double CurrentHeight => _height?.Value ?? Resolve();

    /// <summary>True while the height is springing.</summary>
    public bool IsMoving => _height?.IsAnimating == true;

    private double Resolve()
    {
        var target = TargetHeight;
        return !double.IsNaN(target) ? target : IsOpen ? _natural : 0;
    }

    private void FadeTo(double opacity)
    {
        if (_fade is null)
        {
            _fade = new MotionValue<double>(this, Opacity, MotionVectors.Double);
            _fade.Changed += v => Opacity = v;
        }
        if (ArcMotion.IsFull(this))
        {
            _ = _fade.AnimateTo(opacity, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
        }
        else
        {
            _fade.Jump(opacity);
        }
    }

    private MotionValue<double> Value()
    {
        if (_height is null)
        {
            if (UseFade && StartCollapsed)
            {
                Opacity = 0;
            }
            _height = new MotionValue<double>(this, StartCollapsed ? 0 : Resolve(), MotionVectors.Double);
            _height.Changed += _ => InvalidateMeasure();
        }
        return _height;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = availableSize.Width;
        Child?.Measure(new Size(width, double.PositiveInfinity));
        _natural = Child?.DesiredSize.Height ?? 0;
        var target = Resolve();
        if (!_started)
        {
            // The first measure: take the target (or start from zero) without motion; collapsed boxes animate from the next target.
            var value = Value();
            if (!StartCollapsed)
            {
                value.Jump(target);
                _applied = target;
            }
            _started = true;
        }
        if (!NearlyEqual(target, _applied) && !_applyPending)
        {
            _applyPending = true;
            Dispatcher.UIThread.Post(Apply, DispatcherPriority.Render);
        }
        return new Size(double.IsInfinity(width) ? Child?.DesiredSize.Width ?? 0 : width, Math.Max(0, Value().Value));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Child?.Arrange(new Rect(0, 0, finalSize.Width, Math.Max(_natural, 0)));
        return finalSize;
    }

    private void Apply()
    {
        _applyPending = false;
        var target = Resolve();
        if (NearlyEqual(target, _applied))
        {
            return;
        }
        var value = Value();
        var grow = double.IsNaN(_applied) || target > _applied;
        _applied = target;
        if (UseFade)
        {
            FadeTo(target > 0.5 ? 1 : 0);
        }
        _ = Run(value, target, grow);
    }

    private async Task Run(MotionValue<double> value, double target, bool grow)
    {
        var done = await value.AnimateTo(target, MotionSpec.Spring(grow ? GrowSpring : ShrinkSpring));
        if (done && NearlyEqual(target, _applied))
        {
            Arrived?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Takes <paramref name="height"/> at once (a panel that was just opened shows its first face's height).</summary>
    public void JumpTo(double height)
    {
        _applied = height;
        Value().Jump(height);
    }

    private static bool NearlyEqual(double a, double b) => !double.IsNaN(a) && !double.IsNaN(b) && Math.Abs(a - b) < 0.01;
}
