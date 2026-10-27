using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The pill buttons of the profile page (internal): the default look is Share / Cancel (surface fill, 1px border); the
/// <c>primary</c> class is Edit profile / Save (foreground fill); the <c>icon</c> class is the sheet's round close button.
/// </summary>
internal sealed class ProfilePageButton : AvaloniaButton
{
    protected override Type StyleKeyOverride => typeof(ProfilePageButton);
}

/// <summary>
/// One label of a width-locked label stack (internal): the layers of a stack share one grid cell, so the button keeps the
/// width of its widest label while the shown one cross-fades (opacity and a 2px blur over 160ms, a 3px rise over 240ms;
/// declared in the theme).
/// </summary>
[PseudoClasses(ShownPseudoClass)]
internal sealed class ProfilePageSwapLayer : ContentControl
{
    public const string ShownPseudoClass = ":shown";

    public static readonly StyledProperty<bool> IsShownProperty =
        AvaloniaProperty.Register<ProfilePageSwapLayer, bool>(nameof(IsShown));

    public ProfilePageSwapLayer()
    {
        IsHitTestVisible = false;
        UpdateShown();
    }

    public bool IsShown { get => GetValue(IsShownProperty); set => SetValue(IsShownProperty, value); }

    protected override Type StyleKeyOverride => typeof(ProfilePageSwapLayer);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsShownProperty)
        {
            UpdateShown();
        }
    }

    private void UpdateShown()
    {
        PseudoClasses.Set(ShownPseudoClass, IsShown);
        Avalonia.Automation.AutomationProperties.SetAccessibilityView(this,
            IsShown ? Avalonia.Automation.AccessibilityView.Content : Avalonia.Automation.AccessibilityView.Raw);
    }
}

/// <summary>A stack of <see cref="ProfilePageSwapLayer"/>s in one cell; <see cref="Shown"/> picks the visible one.</summary>
internal sealed class ProfilePageSwapStack : Grid
{
    private int _shown;

    public int Shown
    {
        get => _shown;
        set
        {
            _shown = value;
            for (var i = 0; i < Children.Count; i++)
            {
                if (Children[i] is ProfilePageSwapLayer layer)
                {
                    layer.IsShown = i == value;
                }
            }
        }
    }

    public ProfilePageSwapLayer AddLayer(Control content)
    {
        var layer = new ProfilePageSwapLayer { Content = content, IsShown = Children.Count == _shown };
        Children.Add(layer);
        return layer;
    }

    /// <summary>A layer with an optional glyph (15px) before a text, 6 apart.</summary>
    public static Control GlyphLabel(Geometry? glyph, string text, out TextBlock label, Control? leading = null)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center };
        if (leading is not null)
        {
            row.Children.Add(leading);
        }
        else if (glyph is not null)
        {
            row.Children.Add(new ArcGlyph { Data = glyph, Size = 15, StrokeThickness = 1.75, VerticalAlignment = VerticalAlignment.Center });
        }
        label = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(label);
        return row;
    }
}

/// <summary>A tab of the profile page (internal): its label and a muted count; <c>:selected</c> turns it the strong color.</summary>
[PseudoClasses(SelectedPseudoClass)]
internal sealed class ProfilePageTabButton : AvaloniaButton
{
    public const string SelectedPseudoClass = ":selected";

    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<ProfilePageTabButton, bool>(nameof(IsSelected));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ProfilePageTabButton, string?>(nameof(Label));

    public static readonly StyledProperty<string?> CountProperty =
        AvaloniaProperty.Register<ProfilePageTabButton, string?>(nameof(Count));

    public ProfilePageTabButton(ProfilePageTab tab)
    {
        Tab = tab;
    }

    public ProfilePageTab Tab { get; }

    public bool IsSelected { get => GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public string? Count { get => GetValue(CountProperty); set => SetValue(CountProperty, value); }

    protected override Type StyleKeyOverride => typeof(ProfilePageTabButton);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsSelectedProperty)
        {
            PseudoClasses.Set(SelectedPseudoClass, IsSelected);
            KeyboardNavigation.SetIsTabStop(this, IsSelected);
        }
    }

    protected override Avalonia.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new ProfilePageTabPeer(this);

    private sealed class ProfilePageTabPeer : Avalonia.Automation.Peers.ButtonAutomationPeer, Avalonia.Automation.Provider.ISelectionItemProvider
    {
        public ProfilePageTabPeer(ProfilePageTabButton owner) : base(owner)
        {
        }

        public bool IsSelected => ((ProfilePageTabButton)Owner).IsSelected;

        public Avalonia.Automation.Provider.ISelectionProvider? SelectionContainer => null;

        public void AddToSelection() => Select();

        public void RemoveFromSelection()
        {
        }

        public void Select() => ((ProfilePageTabButton)Owner).Command?.Execute(null);

        protected override Avalonia.Automation.Peers.AutomationControlType GetAutomationControlTypeCore() =>
            Avalonia.Automation.Peers.AutomationControlType.TabItem;
    }
}

/// <summary>
/// The tab row (internal): the tabs from the left, <see cref="TabGap"/> apart, a 1px line along the bottom and the
/// selected tab's underline, which glides between the tabs on the Smooth spring (position and width together).
/// </summary>
internal sealed class ProfilePageTabStrip : Panel
{
    public static readonly StyledProperty<double> TabGapProperty =
        AvaloniaProperty.Register<ProfilePageTabStrip, double>(nameof(TabGap), 20);

    public static readonly StyledProperty<double> UnderlineThicknessProperty =
        AvaloniaProperty.Register<ProfilePageTabStrip, double>(nameof(UnderlineThickness), 2);

    private readonly Border _line = new() { Height = 1, IsHitTestVisible = false };
    private readonly GlidingIndicator _underline = new() { IsHitTestVisible = false };
    private bool _animateNext;
    private Rect _lastRect;

    public ProfilePageTabStrip()
    {
        ArcTokens.Bind(_line, Border.BackgroundProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(_underline, GlidingIndicator.FillProperty, ArcDesignTokenKind.Foreground);
        _underline.CornerRadius = new CornerRadius(2);
        Children.Add(_line);
        Children.Add(_underline);
    }

    public double TabGap { get => GetValue(TabGapProperty); set => SetValue(TabGapProperty, value); }

    public double UnderlineThickness { get => GetValue(UnderlineThicknessProperty); set => SetValue(UnderlineThicknessProperty, value); }

    public GlidingIndicator Underline => _underline;

    public IEnumerable<ProfilePageTabButton> Tabs => Children.OfType<ProfilePageTabButton>();

    public void AddTab(ProfilePageTabButton tab) => Children.Insert(Children.Count - 1, tab);

    /// <summary>The next arrange moves the underline on the spring (a selection change) instead of snapping.</summary>
    public void ArmGlide()
    {
        _animateNext = true;
        InvalidateArrange();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = 0, height = 0;
        var first = true;
        foreach (var tab in Tabs)
        {
            tab.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            width += tab.DesiredSize.Width + (first ? 0 : TabGap);
            height = Math.Max(height, tab.DesiredSize.Height);
            first = false;
        }
        _line.Measure(availableSize);
        _underline.Measure(availableSize);
        var w = double.IsFinite(availableSize.Width) ? availableSize.Width : width;
        return new Size(w, height + 1);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double x = 0;
        var tabHeight = finalSize.Height - 1;
        Rect selected = default;
        foreach (var tab in Tabs)
        {
            var rect = new Rect(x, 0, tab.DesiredSize.Width, tabHeight);
            tab.Arrange(rect);
            if (tab.IsSelected)
            {
                selected = new Rect(rect.X, tabHeight + 1 - UnderlineThickness, rect.Width, UnderlineThickness);
            }
            x += tab.DesiredSize.Width + TabGap;
        }
        _line.Arrange(new Rect(0, finalSize.Height - 1, finalSize.Width, 1));
        _underline.Arrange(new Rect(finalSize));
        if (selected.Width > 0 && (selected != _lastRect || !_underline.HasTarget))
        {
            // Arc: the underline is a shared element on a critically damped spring (fitted ω 13.1 = Smooth); a resize snaps.
            _underline.MoveTo(selected, ArcMotionPresets.Get(ArcSpring.Smooth), _animateNext);
            _lastRect = selected;
        }
        _animateNext = false;
        return finalSize;
    }
}

/// <summary>
/// The tab panel's content swap (internal). The old content fades out (120ms ease-out) and is removed, then the new one
/// fades in while rising 6px (240ms on the enter curve). Turning back while the old one is still fading out fades it back
/// in (opacity only). Reduced and Off swap at once. The panel's height follows on the <see cref="SpringSizeDecorator"/>
/// around it once the new content is in place.
/// </summary>
internal sealed class ProfilePagePanelSwap : Control
{
    private static readonly TimeSpan ExitDuration = TimeSpan.FromMilliseconds(120);
    private static readonly TimeSpan EnterDuration = TimeSpan.FromMilliseconds(240);
    private static readonly Easing EaseOut = new SplineEasing(0, 0, 0.58, 1);
    private const double Rise = 6;

    private Control? _shown;
    private Control? _target;
    private MotionValue<double>? _opacity;
    private MotionValue<double>? _rise;
    private readonly TranslateTransform _translate = new();
    private int _version;

    /// <summary>The content shown now or arriving (the last one asked for).</summary>
    public Control? Current => _target;

    /// <summary>The content in the tree right now (the old one while it fades out).</summary>
    public Control? Displayed => _shown;

    public void Show(Control? content, bool animate)
    {
        if (ReferenceEquals(content, _target))
        {
            return;
        }
        _target = content;
        var version = ++_version;
        var full = animate && IsLoaded && ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full && _shown is not null;
        EnsureMotion();
        if (!full)
        {
            Mount(content);
            _opacity!.Jump(1);
            _rise!.Jump(0);
            return;
        }
        if (ReferenceEquals(content, _shown))
        {
            // Turned back while the old content was fading out: fade it back in where it is.
            _rise!.Jump(0);
            _ = _opacity!.AnimateTo(1, MotionSpec.Tween(EnterDuration, ArcEasings.Enter));
            return;
        }
        _ = ExitThenEnterAsync(content, version);
    }

    private async Task ExitThenEnterAsync(Control? content, int version)
    {
        var finished = await _opacity!.AnimateTo(0, MotionSpec.Tween(ExitDuration, EaseOut));
        if (!finished || version != _version)
        {
            return;
        }
        Mount(content);
        _opacity.Jump(0);
        _rise!.Jump(Rise);
        _ = _rise.AnimateTo(0, MotionSpec.Tween(EnterDuration, ArcEasings.Enter));
        _ = _opacity.AnimateTo(1, MotionSpec.Tween(EnterDuration, ArcEasings.Enter));
    }

    private void Mount(Control? content)
    {
        if (ReferenceEquals(content, _shown))
        {
            return;
        }
        if (_shown is not null)
        {
            _shown.RenderTransform = null;
            _shown.Opacity = 1;
            VisualChildren.Remove(_shown);
            LogicalChildren.Remove(_shown);
        }
        _shown = content;
        if (content is not null)
        {
            content.RenderTransform = _translate;
            content.Opacity = _opacity?.Value ?? 1;
            LogicalChildren.Add(content);
            VisualChildren.Add(content);
        }
        InvalidateMeasure();
    }

    private void EnsureMotion()
    {
        if (_opacity is not null)
        {
            return;
        }
        // Code-driven: a wait-mode presence (the new content mounts only after the old one has gone) with an interrupt
        // that fades the old content back in; a ControlTheme transition cannot sequence the two.
        _opacity = new MotionValue<double>(this, 1, MotionVectors.Double);
        _opacity.Changed += v =>
        {
            if (_shown is not null)
            {
                _shown.Opacity = v;
            }
        };
        _rise = new MotionValue<double>(this, 0, MotionVectors.Double);
        _rise.Changed += v => _translate.Y = v;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_shown is null)
        {
            return default;
        }
        _shown.Measure(availableSize);
        return _shown.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _shown?.Arrange(new Rect(finalSize));
        return finalSize;
    }
}

/// <summary>
/// The save glow behind a changed field (internal): an accent-subtle rounded rectangle reaching 3px past the child (radius
/// 6 plus the spread), held 480ms and then faded over 1120ms on the standard curve. It runs at Full and Reduced motion
/// (it does not move anything) and is skipped at Off.
/// </summary>
internal sealed class ProfilePageGlow : Decorator
{
    private static readonly TimeSpan Hold = TimeSpan.FromMilliseconds(480);
    private static readonly TimeSpan Fade = TimeSpan.FromMilliseconds(1120);

    public static readonly StyledProperty<double> SpreadProperty =
        AvaloniaProperty.Register<ProfilePageGlow, double>(nameof(Spread), 3);

    public static readonly StyledProperty<CornerRadius> RadiusProperty =
        AvaloniaProperty.Register<ProfilePageGlow, CornerRadius>(nameof(Radius), new CornerRadius(6));

    private MotionValue<double>? _alpha;
    private IBrush? _brush;

    static ProfilePageGlow()
    {
        AffectsRender<ProfilePageGlow>(SpreadProperty, RadiusProperty);
    }

    public double Spread { get => GetValue(SpreadProperty); set => SetValue(SpreadProperty, value); }

    public CornerRadius Radius { get => GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }

    /// <summary>The glow's strength now (1 while held, 0 at rest).</summary>
    public double Strength => _alpha?.Value ?? 0;

    /// <summary>Whether the glow is showing or fading.</summary>
    public bool IsGlowing => Strength > 0.001 || (_alpha?.IsAnimating ?? false);

    public void Flash()
    {
        if (_alpha is null)
        {
            // Code-driven: a one-shot keyframe (hold, then fade) restarted by each save; a theme transition has no hold.
            _alpha = new MotionValue<double>(this, 0, MotionVectors.Double);
            _alpha.Changed += _ => InvalidateVisual();
        }
        if (!IsLoaded || ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off)
        {
            _alpha.Jump(0);
            return;
        }
        _alpha.Jump(1);
        _ = _alpha.AnimateTo(0, MotionSpec.Tween(Fade, ArcEasings.Standard, Hold));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property.Name == "ActualThemeVariant")
        {
            _brush = null;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _brush = null;
    }

    public override void Render(DrawingContext context)
    {
        var alpha = Strength;
        if (alpha <= 0.001)
        {
            return;
        }
        if (_brush is null)
        {
            try
            {
                _brush = new ImmutableSolidColorBrush(ArcTokens.GetColor(this, ArcDesignTokenKind.AccentSubtle));
            }
            catch (Exception)
            {
                return;
            }
        }
        var spread = Spread;
        var rect = new Rect(Bounds.Size).Inflate(spread);
        var r = Radius;
        var radius = new CornerRadius(r.TopLeft + spread, r.TopRight + spread, r.BottomRight + spread, r.BottomLeft + spread);
        using (context.PushOpacity(alpha))
        {
            context.DrawRectangle(_brush, null, new RoundedRect(rect, radius));
        }
    }
}

/// <summary>The project star (internal): Lucide's star at 14px, outlined or filled.</summary>
internal sealed class ProfilePageStarGlyph : Control
{
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<ProfilePageStarGlyph, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<ProfilePageStarGlyph, IBrush?>(nameof(Stroke));

    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<ProfilePageStarGlyph, double>(nameof(Size), 14);

    private Pen? _pen;

    static ProfilePageStarGlyph()
    {
        AffectsRender<ProfilePageStarGlyph>(FillProperty, StrokeProperty);
        AffectsMeasure<ProfilePageStarGlyph>(SizeProperty);
    }

    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }

    public IBrush? Stroke { get => GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }

    public double Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StrokeProperty)
        {
            _pen = null;
        }
    }

    protected override Size MeasureOverride(Size availableSize) => new(Size, Size);

    public override void Render(DrawingContext context)
    {
        var scale = Size / 24.0;
        if (Stroke is { } stroke)
        {
            _pen ??= new Pen(stroke, 1.75, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        }
        using (context.PushTransform(Matrix.CreateScale(scale, scale)))
        {
            context.DrawGeometry(Fill, Stroke is null ? null : _pen, ArcIcons.Star);
        }
    }
}

/// <summary>
/// A project's star button (internal): the star and the count; <c>:starred</c> fills the star with the warning color
/// and turns the count the strong color. Starring pops the star from 0.6 on the Quick spring; unstarring does not.
/// </summary>
[PseudoClasses(StarredPseudoClass)]
[TemplatePart(GlyphPart, typeof(ProfilePageStarGlyph))]
internal sealed class ProfilePageStarButton : AvaloniaButton
{
    public const string StarredPseudoClass = ":starred";
    public const string GlyphPart = "PART_Glyph";

    public static readonly StyledProperty<bool> IsStarredProperty =
        AvaloniaProperty.Register<ProfilePageStarButton, bool>(nameof(IsStarred));

    public static readonly StyledProperty<string?> CountTextProperty =
        AvaloniaProperty.Register<ProfilePageStarButton, string?>(nameof(CountText));

    private ProfilePageStarGlyph? _glyph;
    private MotionValue<double>? _scale;
    private readonly ScaleTransform _scaleTransform = new(1, 1);

    public bool IsStarred { get => GetValue(IsStarredProperty); set => SetValue(IsStarredProperty, value); }

    public string? CountText { get => GetValue(CountTextProperty); set => SetValue(CountTextProperty, value); }

    /// <summary>The star's current pop scale (1 at rest).</summary>
    public double GlyphScale => _scale?.Value ?? 1;

    protected override Type StyleKeyOverride => typeof(ProfilePageStarButton);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _glyph = e.NameScope.Find<ProfilePageStarGlyph>(GlyphPart);
        if (_glyph is not null)
        {
            _glyph.RenderTransformOrigin = RelativePoint.Center;
            _glyph.RenderTransform = _scaleTransform;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsStarredProperty)
        {
            PseudoClasses.Set(StarredPseudoClass, IsStarred);
        }
    }

    /// <summary>Pops the star from 0.6 to 1 (Full motion only; Arc: critically damped ω 20.3 ≈ Quick).</summary>
    public void Pop()
    {
        if (_scale is null)
        {
            // Code-driven: the star remounts on each starring, so the pop restarts from 0.6 at zero velocity.
            _scale = new MotionValue<double>(this, 1, MotionVectors.Double);
            _scale.Changed += v =>
            {
                _scaleTransform.ScaleX = v;
                _scaleTransform.ScaleY = v;
            };
        }
        if (!IsLoaded || !ArcMotion.IsFull(this))
        {
            _scale.Jump(1);
            return;
        }
        _scale.Jump(0.6);
        _ = _scale.AnimateTo(1, MotionSpec.Spring(ArcSpring.Quick));
    }
}

/// <summary>
/// Lays out its children in equal columns (internal): two cards per row on a wide page, one on a narrow one; every child
/// of a row gets the row's tallest height. Also the sheet's Location / Website pair.
/// </summary>
internal class ProfilePageColumns : Panel
{
    public static readonly StyledProperty<int> ColumnsProperty =
        AvaloniaProperty.Register<ProfilePageColumns, int>(nameof(Columns), 2);

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<ProfilePageColumns, double>(nameof(Gap), 12);

    public static readonly StyledProperty<double> RowGapProperty =
        AvaloniaProperty.Register<ProfilePageColumns, double>(nameof(RowGap), 12);

    static ProfilePageColumns()
    {
        AffectsMeasure<ProfilePageColumns>(ColumnsProperty, GapProperty, RowGapProperty);
    }

    public int Columns { get => GetValue(ColumnsProperty); set => SetValue(ColumnsProperty, value); }

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    public double RowGap { get => GetValue(RowGapProperty); set => SetValue(RowGapProperty, value); }

    private readonly List<double> _rows = new();

    private double ColumnWidth(double width, int columns) => Math.Max(0, (width - Gap * (columns - 1)) / columns);

    protected override Size MeasureOverride(Size availableSize)
    {
        var columns = Math.Max(1, Columns);
        var visible = Children.Where(c => c.IsVisible).ToList();
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 0;
        var cell = double.IsFinite(availableSize.Width) ? ColumnWidth(width, columns) : double.PositiveInfinity;
        _rows.Clear();
        double maxCell = 0;
        for (var i = 0; i < visible.Count; i++)
        {
            visible[i].Measure(new Size(cell, double.PositiveInfinity));
            var row = i / columns;
            if (row >= _rows.Count)
            {
                _rows.Add(0);
            }
            _rows[row] = Math.Max(_rows[row], visible[i].DesiredSize.Height);
            maxCell = Math.Max(maxCell, visible[i].DesiredSize.Width);
        }
        var height = _rows.Sum() + Math.Max(0, _rows.Count - 1) * RowGap;
        if (!double.IsFinite(availableSize.Width))
        {
            width = maxCell * columns + Gap * (columns - 1);
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = Math.Max(1, Columns);
        var visible = Children.Where(c => c.IsVisible).ToList();
        var cell = ColumnWidth(finalSize.Width, columns);
        double y = 0;
        for (var i = 0; i < visible.Count; i++)
        {
            var row = i / columns;
            var col = i % columns;
            if (col == 0 && row > 0)
            {
                y += _rows[row - 1] + RowGap;
            }
            var h = row < _rows.Count ? _rows[row] : visible[i].DesiredSize.Height;
            visible[i].Arrange(new Rect(col * (cell + Gap), y, cell, h));
        }
        return finalSize;
    }
}

/// <summary>
/// The body (internal): the about column (first child, <see cref="AboutWidth"/> wide) beside the tabs (second child),
/// <see cref="ColumnGap"/> apart; stacked with <see cref="StackedGap"/> between them when <see cref="IsStacked"/>.
/// </summary>
internal sealed class ProfilePageBodyPanel : Panel
{
    public static readonly StyledProperty<bool> IsStackedProperty =
        AvaloniaProperty.Register<ProfilePageBodyPanel, bool>(nameof(IsStacked));

    public static readonly StyledProperty<double> AboutWidthProperty =
        AvaloniaProperty.Register<ProfilePageBodyPanel, double>(nameof(AboutWidth), 248);

    public static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<ProfilePageBodyPanel, double>(nameof(ColumnGap), 40);

    public static readonly StyledProperty<double> StackedGapProperty =
        AvaloniaProperty.Register<ProfilePageBodyPanel, double>(nameof(StackedGap), 24);

    static ProfilePageBodyPanel()
    {
        AffectsMeasure<ProfilePageBodyPanel>(IsStackedProperty, AboutWidthProperty, ColumnGapProperty, StackedGapProperty);
    }

    public bool IsStacked { get => GetValue(IsStackedProperty); set => SetValue(IsStackedProperty, value); }

    public double AboutWidth { get => GetValue(AboutWidthProperty); set => SetValue(AboutWidthProperty, value); }

    public double ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }

    public double StackedGap { get => GetValue(StackedGapProperty); set => SetValue(StackedGapProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count < 2)
        {
            return default;
        }
        var about = Children[0];
        var main = Children[1];
        var width = availableSize.Width;
        if (IsStacked)
        {
            about.Measure(new Size(width, double.PositiveInfinity));
            main.Measure(new Size(width, double.PositiveInfinity));
            var w = double.IsFinite(width) ? width : Math.Max(about.DesiredSize.Width, main.DesiredSize.Width);
            return new Size(w, about.DesiredSize.Height + StackedGap + main.DesiredSize.Height);
        }
        about.Measure(new Size(AboutWidth, double.PositiveInfinity));
        var mainWidth = double.IsFinite(width) ? Math.Max(0, width - AboutWidth - ColumnGap) : double.PositiveInfinity;
        main.Measure(new Size(mainWidth, double.PositiveInfinity));
        var total = double.IsFinite(width) ? width : AboutWidth + ColumnGap + main.DesiredSize.Width;
        return new Size(total, Math.Max(about.DesiredSize.Height, main.DesiredSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count < 2)
        {
            return finalSize;
        }
        var about = Children[0];
        var main = Children[1];
        if (IsStacked)
        {
            about.Arrange(new Rect(0, 0, finalSize.Width, about.DesiredSize.Height));
            var top = about.DesiredSize.Height + StackedGap;
            main.Arrange(new Rect(0, top, finalSize.Width, main.DesiredSize.Height));
            return finalSize;
        }
        about.Arrange(new Rect(0, 0, AboutWidth, about.DesiredSize.Height));
        var x = AboutWidth + ColumnGap;
        main.Arrange(new Rect(x, 0, Math.Max(0, finalSize.Width - x), main.DesiredSize.Height));
        return finalSize;
    }
}
