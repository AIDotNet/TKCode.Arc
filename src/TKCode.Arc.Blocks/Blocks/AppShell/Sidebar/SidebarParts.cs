using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// Lays its child out at the width it has while the <see cref="Sidebar"/> is open, whatever width it gets now. While the
/// sidebar springs to the icon rail (and back) the rows, the header and the footer therefore never reflow or re-ellipsize:
/// their content stays put and the pane's edge clips it, so icons hold still while the labels fade. The open width comes
/// from the measure (the sidebar's layout width); the arrange uses only that and the host's own size. It must not read
/// the width the pane shows now: an overlay pane springs while the layout keeps the host's size, so the host is not
/// arranged again when the spring ends and would keep the width of its first frame.
/// </summary>
internal sealed class SidebarOpenWidthHost : Decorator
{
    /// <summary>
    /// Clips the content at the host's right edge while the sidebar is narrower than open (header and footer content
    /// that does not fade by itself). Room is kept on the other sides so focus rings stay whole.
    /// </summary>
    public static readonly StyledProperty<bool> ClipsWhenNarrowProperty =
        AvaloniaProperty.Register<SidebarOpenWidthHost, bool>(nameof(ClipsWhenNarrow));

    public bool ClipsWhenNarrow { get => GetValue(ClipsWhenNarrowProperty); set => SetValue(ClipsWhenNarrowProperty, value); }

    private const double ClipRoom = 8;
    private readonly RectangleGeometry _clip = new();
    private Sidebar? _owner;
    private double _openWidth;

    private Sidebar? Owner => _owner ??= this.FindAncestorOfType<Sidebar>();

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child is not { } child)
        {
            return default;
        }
        // The constraint is the open width at every step of the width spring, so the child is not even re-measured.
        var openWidth = availableSize.Width + (Owner?.MeasureDeficit ?? 0);
        if (openWidth != _openWidth)
        {
            // A new constraint with the same final rectangle would otherwise skip the arrange.
            _openWidth = openWidth;
            InvalidateArrange();
        }
        child.Measure(availableSize.WithWidth(openWidth));
        var desired = child.DesiredSize;
        return new Size(Math.Min(desired.Width, availableSize.Width), desired.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var width = double.IsFinite(_openWidth) ? Math.Max(finalSize.Width, _openWidth) : finalSize.Width;
        Child?.Arrange(new Rect(0, 0, width, finalSize.Height));
        if (ClipsWhenNarrow && width - finalSize.Width > 0.5)
        {
            // One geometry, updated in place: the clip moves with the width spring without allocating.
            var rect = new Rect(-ClipRoom, -ClipRoom, finalSize.Width + ClipRoom, finalSize.Height + 2 * ClipRoom);
            if (rect != _clip.Rect || Clip is null)
            {
                _clip.Rect = rect;
                Clip = _clip;
                InvalidateVisual();
            }
        }
        else if (Clip is not null)
        {
            Clip = null;
        }
        return finalSize;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _owner = null;
    }
}

/// <summary>
/// The root of a sidebar's template. Its children (the pane and its edge line) are arranged at the sidebar's
/// <see cref="Sidebar.PaneWidth"/>: the width the pane shows now, which in <see cref="SidebarResizeMode.Overlay"/> springs
/// while the sidebar's own layout width has already jumped to its new value. The panel reports whatever the children
/// measure, so a springing pane never invalidates the layout of what is next to the sidebar.
/// </summary>
internal sealed class SidebarPaneHost : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = 0.0;
        var height = 0.0;
        foreach (var child in Children)
        {
            child.Measure(availableSize);
            width = Math.Max(width, child.DesiredSize.Width);
            height = Math.Max(height, child.DesiredSize.Height);
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var width = TemplatedParent is Sidebar sidebar ? sidebar.PaneWidth : finalSize.Width;
        var rect = new Rect(0, 0, double.IsNaN(width) ? finalSize.Width : width, finalSize.Height);
        foreach (var child in Children)
        {
            child.Arrange(rect);
        }
        return finalSize;
    }
}

/// <summary>
/// Opens and closes a section's pages or a parent's child pages with a height spring (smooth). Unlike Core's
/// <see cref="SpringSizeDecorator"/>, a change of the available width is not treated as a passive resize that jumps:
/// the rows have a fixed height, and the sidebar's width spring must not cut a height spring that runs at the same time
/// (collapsing to the rail closes the open parents while the width moves). Clips only while it moves or is closed.
/// </summary>
internal sealed class SidebarHeightSpring : Decorator
{
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<SidebarHeightSpring, bool>(nameof(IsExpanded), true);

    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

    /// <summary>True while the height springs.</summary>
    public bool IsMoving => _height?.IsAnimating == true;

    /// <summary>
    /// True once the container has closed and settled: it is then taken out of layout and rendering altogether
    /// (<see cref="Visual.IsVisible"/>), so a tree with many closed parents costs nothing while they are closed.
    /// </summary>
    public bool IsSettledClosed => !IsExpanded && !IsMoving && (_height?.Value ?? 0) <= 0.01;

    private const double ClipRoom = 8;
    private readonly RectangleGeometry _clip = new();
    private MotionValue<double>? _height;
    private double _target;
    private bool _hidePosted;

    static SidebarHeightSpring()
    {
        AffectsMeasure<SidebarHeightSpring>(IsExpandedProperty);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsExpandedProperty && change.GetNewValue<bool>())
        {
            // Opening: back into layout; the height springs up from where it was.
            SetCurrentValue(IsVisibleProperty, true);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child is not { } child)
        {
            return default;
        }
        child.Measure(availableSize.WithHeight(double.PositiveInfinity));
        var target = IsExpanded ? child.DesiredSize.Height : 0;
        if (_height is null)
        {
            _target = target;
            _height = new MotionValue<double>(this, target, MotionVectors.Double);
            _height.Changed += _ => InvalidateMeasure();
        }
        else if (Math.Abs(target - _target) > 0.25)
        {
            _target = target;
            if (IsLoaded && ArcMotion.IsFull(this))
            {
                _ = _height.AnimateTo(target, MotionSpec.Spring(ArcSpring.Smooth));
            }
            else
            {
                _height.Jump(target);
            }
        }
        return new Size(child.DesiredSize.Width, Math.Max(0, _height.Value));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Child is not { } child)
        {
            return finalSize;
        }
        child.Arrange(new Rect(0, 0, finalSize.Width, child.DesiredSize.Height));
        if (IsMoving || !IsExpanded)
        {
            // One geometry, updated in place (and only when it changed): no allocation, no repaint per frame.
            // Room on the sides keeps focus rings whole.
            var rect = new Rect(-ClipRoom, 0, finalSize.Width + 2 * ClipRoom, finalSize.Height);
            if (Clip is null || rect != _clip.Rect)
            {
                _clip.Rect = rect;
                Clip = _clip;
                InvalidateVisual();
            }
            if (IsSettledClosed && !_hidePosted)
            {
                // Hide after this layout pass (changing visibility during arrange would invalidate the pass).
                _hidePosted = true;
                Dispatcher.UIThread.Post(HideWhenClosed, DispatcherPriority.Render);
            }
        }
        else if (Clip is not null)
        {
            Clip = null;
        }
        return finalSize;
    }

    private void HideWhenClosed()
    {
        _hidePosted = false;
        if (IsSettledClosed)
        {
            SetCurrentValue(IsVisibleProperty, false);
        }
    }
}

/// <summary>
/// The scrolling content of a <see cref="Sidebar"/>: the selection marker (a <see cref="GlidingIndicator"/>, the muted
/// block) under the sections, in one coordinate space. After an arrange that moved the marker's row it tells the sidebar,
/// so the marker follows passive layout changes (a section opening above it) without a spring of its own. It also
/// carries the token geometry the sidebar needs when it builds the section and child containers in code.
/// </summary>
internal sealed class SidebarListPanel : Panel
{
    public static readonly StyledProperty<double> RowGapProperty =
        AvaloniaProperty.Register<SidebarListPanel, double>(nameof(RowGap), 2);

    public static readonly StyledProperty<Thickness> ChildrenMarginProperty =
        AvaloniaProperty.Register<SidebarListPanel, Thickness>(nameof(ChildrenMargin), new Thickness(26, 0, 0, 0));

    public static readonly StyledProperty<Thickness> GuideMarginProperty =
        AvaloniaProperty.Register<SidebarListPanel, Thickness>(nameof(GuideMargin), new Thickness(19.5, 0, 0, 0));

    public static readonly StyledProperty<IBrush?> GuideBrushProperty =
        AvaloniaProperty.Register<SidebarListPanel, IBrush?>(nameof(GuideBrush));

    public double RowGap { get => GetValue(RowGapProperty); set => SetValue(RowGapProperty, value); }
    public Thickness ChildrenMargin { get => GetValue(ChildrenMarginProperty); set => SetValue(ChildrenMarginProperty, value); }
    public Thickness GuideMargin { get => GetValue(GuideMarginProperty); set => SetValue(GuideMarginProperty, value); }
    public IBrush? GuideBrush { get => GetValue(GuideBrushProperty); set => SetValue(GuideBrushProperty, value); }

    public Action? Arranged { get; set; }

    static SidebarListPanel()
    {
        AutomationProperties.ControlTypeOverrideProperty.OverrideDefaultValue<SidebarListPanel>(AutomationControlType.Tree);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = 0.0;
        var height = 0.0;
        foreach (var child in Children)
        {
            child.Measure(availableSize);
            width = Math.Max(width, child.DesiredSize.Width);
            height = Math.Max(height, child.DesiredSize.Height);
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var rect = new Rect(finalSize);
        foreach (var child in Children)
        {
            child.Arrange(rect);
        }
        Arranged?.Invoke();
        return finalSize;
    }
}

/// <summary>
/// Lays out a row's label and the icon that trails it: the label keeps what room is left, and the icon sits right after
/// the label's text (not at the end of the row), so "More" and its chevron read as one.
/// </summary>
internal sealed class SidebarInlinePanel : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count < 2)
        {
            return Children.Count == 1 ? Measure1(availableSize) : default;
        }
        var label = Children[0];
        var trailing = Children[1];
        trailing.Measure(new Size(double.PositiveInfinity, availableSize.Height));
        var trailingWidth = trailing.IsVisible ? trailing.DesiredSize.Width : 0;
        label.Measure(new Size(Math.Max(0, availableSize.Width - trailingWidth), availableSize.Height));
        return new Size(label.DesiredSize.Width + trailingWidth, Math.Max(label.DesiredSize.Height, trailing.DesiredSize.Height));
    }

    private Size Measure1(Size availableSize)
    {
        Children[0].Measure(availableSize);
        return Children[0].DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count == 0)
        {
            return finalSize;
        }
        var label = Children[0];
        var x = Math.Min(label.DesiredSize.Width, finalSize.Width);
        label.Arrange(new Rect(0, 0, x, finalSize.Height));
        if (Children.Count > 1)
        {
            var trailing = Children[1];
            trailing.Arrange(new Rect(x, 0, trailing.IsVisible ? trailing.DesiredSize.Width : 0, finalSize.Height));
        }
        return finalSize;
    }
}

/// <summary>
/// The sidebar's own icon buttons (collapse / expand in the header row, search on the rail): 40 by 34 ghost squares with
/// the rows' hover wash and radius, so on the rail they line up with the page rows below them.
/// </summary>
internal sealed class SidebarIconButton : AvaloniaButton
{
    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<SidebarIconButton, Geometry?>(nameof(Glyph));

    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }
}

/// <summary>Hover (fine pointers only), press and click for the sidebar's rows and section headings.</summary>
internal static class SidebarPointer
{
    public const string HoveredPseudoClass = ":hovered";
    public const string PressedPseudoClass = ":pressed";
}

/// <summary>
/// One page row of a <see cref="Sidebar"/>: icon, label, an optional trailing icon, count or unread dot and (for a parent)
/// a chevron. The row's own fill is only the hover wash; the selection block is the sidebar's gliding marker underneath.
/// On the icon rail everything after the icon fades out together (one opacity per row; it keeps its open-width layout and
/// is clipped by the pane) and a count or unread dot becomes a dot on the icon.
/// </summary>
[TemplatePart(LabelPart, typeof(TextBlock))]
[TemplatePart(BadgePart, typeof(TextBlock))]
[PseudoClasses(SelectedPseudoClass, CurrentPseudoClass, ParentPseudoClass, ExpandedPseudoClass, RailPseudoClass,
    HasIconPseudoClass, HasBadgePseudoClass, HasTrailingPseudoClass, UnreadPseudoClass, ActionPseudoClass, NestedPseudoClass,
    SidebarPointer.HoveredPseudoClass, SidebarPointer.PressedPseudoClass)]
internal sealed class SidebarRow : TemplatedControl
{
    public const string LabelPart = "PART_Label";
    public const string BadgePart = "PART_Badge";
    public const string SelectedPseudoClass = ":selected";
    public const string CurrentPseudoClass = ":current";
    public const string ParentPseudoClass = ":parent";
    public const string ExpandedPseudoClass = ":expanded";
    public const string RailPseudoClass = ":rail";
    public const string HasIconPseudoClass = ":has-icon";
    public const string HasBadgePseudoClass = ":has-badge";
    public const string HasTrailingPseudoClass = ":has-trailing";
    public const string UnreadPseudoClass = ":unread";
    public const string ActionPseudoClass = ":action";

    /// <summary>A child page (indented under its parent; never on the rail itself).</summary>
    public const string NestedPseudoClass = ":nested";

    private static readonly FontFeatureCollection s_tabularFigures = new() { FontFeature.Parse("tnum") };

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<SidebarRow, string>(nameof(Label), string.Empty);

    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<SidebarRow, Geometry?>(nameof(Glyph));

    public static readonly StyledProperty<object?> IconContentProperty =
        AvaloniaProperty.Register<SidebarRow, object?>(nameof(IconContent));

    public static readonly StyledProperty<Geometry?> TrailingGlyphProperty =
        AvaloniaProperty.Register<SidebarRow, Geometry?>(nameof(TrailingGlyph));

    public static readonly StyledProperty<object?> TrailingContentProperty =
        AvaloniaProperty.Register<SidebarRow, object?>(nameof(TrailingContent));

    public static readonly StyledProperty<bool> IsUnreadProperty =
        AvaloniaProperty.Register<SidebarRow, bool>(nameof(IsUnread));

    public static readonly StyledProperty<bool> IsActionProperty =
        AvaloniaProperty.Register<SidebarRow, bool>(nameof(IsAction));

    public static readonly StyledProperty<string?> MonogramProperty =
        AvaloniaProperty.Register<SidebarRow, string?>(nameof(Monogram));

    public static readonly StyledProperty<string?> BadgeProperty =
        AvaloniaProperty.Register<SidebarRow, string?>(nameof(Badge));

    public static readonly StyledProperty<bool> IsParentProperty =
        AvaloniaProperty.Register<SidebarRow, bool>(nameof(IsParent));

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<SidebarRow, bool>(nameof(IsExpanded));

    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<SidebarRow, bool>(nameof(IsSelected));

    /// <summary>The selected page is a hidden child of this row (the marker rests here).</summary>
    public static readonly StyledProperty<bool> IsCurrentProperty =
        AvaloniaProperty.Register<SidebarRow, bool>(nameof(IsCurrent));

    public static readonly StyledProperty<bool> IsRailProperty =
        AvaloniaProperty.Register<SidebarRow, bool>(nameof(IsRail));

    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }
    public object? IconContent { get => GetValue(IconContentProperty); set => SetValue(IconContentProperty, value); }
    public Geometry? TrailingGlyph { get => GetValue(TrailingGlyphProperty); set => SetValue(TrailingGlyphProperty, value); }
    public object? TrailingContent { get => GetValue(TrailingContentProperty); set => SetValue(TrailingContentProperty, value); }
    public bool IsUnread { get => GetValue(IsUnreadProperty); set => SetValue(IsUnreadProperty, value); }
    public bool IsAction { get => GetValue(IsActionProperty); set => SetValue(IsActionProperty, value); }
    public string? Monogram { get => GetValue(MonogramProperty); set => SetValue(MonogramProperty, value); }
    public string? Badge { get => GetValue(BadgeProperty); set => SetValue(BadgeProperty, value); }
    public bool IsParent { get => GetValue(IsParentProperty); set => SetValue(IsParentProperty, value); }
    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }
    public bool IsSelected { get => GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }
    public bool IsCurrent { get => GetValue(IsCurrentProperty); set => SetValue(IsCurrentProperty, value); }
    public bool IsRail { get => GetValue(IsRailProperty); set => SetValue(IsRailProperty, value); }

    private bool _pressed;

    static SidebarRow()
    {
        FocusableProperty.OverrideDefaultValue<SidebarRow>(true);
        IsTabStopProperty.OverrideDefaultValue<SidebarRow>(false);
    }

    public SidebarRow(Sidebar owner, SidebarNode node)
    {
        Owner = owner;
        Node = node;
        PseudoClasses.Set(NestedPseudoClass, node.Parent is not null);
    }

    public Sidebar Owner { get; }

    public SidebarNode Node { get; }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (e.NameScope.Find<TextBlock>(BadgePart) is { } badge)
        {
            badge.FontFeatures = s_tabularFigures;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsSelectedProperty)
        {
            PseudoClasses.Set(SelectedPseudoClass, change.GetNewValue<bool>());
        }
        else if (change.Property == IsCurrentProperty)
        {
            PseudoClasses.Set(CurrentPseudoClass, change.GetNewValue<bool>());
        }
        else if (change.Property == IsParentProperty)
        {
            PseudoClasses.Set(ParentPseudoClass, change.GetNewValue<bool>());
        }
        else if (change.Property == IsExpandedProperty)
        {
            var expanded = change.GetNewValue<bool>();
            PseudoClasses.Set(ExpandedPseudoClass, expanded);
            if (IsParent && ControlAutomationPeer.FromElement(this) is SidebarRowAutomationPeer peer)
            {
                peer.RaiseExpandCollapseChanged(!expanded, expanded);
            }
        }
        else if (change.Property == IsRailProperty)
        {
            PseudoClasses.Set(RailPseudoClass, change.GetNewValue<bool>());
        }
        else if (change.Property == GlyphProperty || change.Property == IconContentProperty)
        {
            PseudoClasses.Set(HasIconPseudoClass, Glyph is not null || IconContent is not null);
        }
        else if (change.Property == TrailingGlyphProperty || change.Property == TrailingContentProperty)
        {
            PseudoClasses.Set(HasTrailingPseudoClass, TrailingGlyph is not null || TrailingContent is not null);
        }
        else if (change.Property == BadgeProperty)
        {
            PseudoClasses.Set(HasBadgePseudoClass, !string.IsNullOrEmpty(change.GetNewValue<string?>()));
        }
        else if (change.Property == IsUnreadProperty)
        {
            PseudoClasses.Set(UnreadPseudoClass, change.GetNewValue<bool>());
        }
        else if (change.Property == IsActionProperty)
        {
            PseudoClasses.Set(ActionPseudoClass, change.GetNewValue<bool>());
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        // The hover wash is for fine pointers; a finger has no hover.
        PseudoClasses.Set(SidebarPointer.HoveredPseudoClass, e.Pointer.Type != PointerType.Touch);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(SidebarPointer.HoveredPseudoClass, false);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.Handled && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _pressed = true;
            PseudoClasses.Set(SidebarPointer.PressedPseudoClass, true);
            e.Pointer.Capture(this);
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pressed)
        {
            return;
        }
        var activate = new Rect(Bounds.Size).Contains(e.GetPosition(this));
        _pressed = false;
        PseudoClasses.Set(SidebarPointer.PressedPseudoClass, false);
        e.Pointer.Capture(null);
        e.Handled = true;
        if (activate)
        {
            Owner.OnViewClicked(Node);
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pressed = false;
        PseudoClasses.Set(SidebarPointer.PressedPseudoClass, false);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        Owner.OnViewFocused(Node);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new SidebarRowAutomationPeer(this);

    /// <summary>A tree item: named by its label and count, selectable for pages, expandable for parents.</summary>
    private sealed class SidebarRowAutomationPeer : ControlAutomationPeer, IExpandCollapseProvider, ISelectionItemProvider, IInvokeProvider
    {
        public SidebarRowAutomationPeer(SidebarRow owner) : base(owner)
        {
        }

        private SidebarRow Row => (SidebarRow)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.TreeItem;

        protected override string? GetNameCore()
            => base.GetNameCore() is { Length: > 0 } name ? name : Row.Label;

        public ExpandCollapseState ExpandCollapseState => !Row.IsParent
            ? ExpandCollapseState.LeafNode
            : Row.IsExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => Row.IsParent && Row.IsRail;

        public void Expand() => Row.Owner.SetNodeExpanded(Row.Node, true);

        public void Collapse() => Row.Owner.SetNodeExpanded(Row.Node, false);

        public bool IsSelected => Row.IsSelected;

        public ISelectionProvider? SelectionContainer => null;

        public void AddToSelection() => Row.Owner.Activate(Row.Node, NavigationMethod.Unspecified);

        public void RemoveFromSelection()
        {
        }

        public void Select() => Row.Owner.Activate(Row.Node, NavigationMethod.Unspecified);

        public void Invoke() => Row.Owner.Activate(Row.Node, NavigationMethod.Unspecified);

        public void RaiseExpandCollapseChanged(bool oldValue, bool newValue)
            => RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                oldValue ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed,
                newValue ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed);
    }
}

/// <summary>
/// The heading of a <see cref="SidebarSection"/> and the space above it: a small muted title (a disclosure button with a
/// chevron when the section is collapsible). On the icon rail the title fades and a 1px line marks the section break.
/// </summary>
[PseudoClasses(TitledPseudoClass, GapPseudoClass, CollapsiblePseudoClass, ExpandedPseudoClass, RailPseudoClass,
    SidebarPointer.HoveredPseudoClass, SidebarPointer.PressedPseudoClass)]
internal sealed class SidebarSectionHeader : TemplatedControl
{
    public const string TitledPseudoClass = ":titled";
    public const string GapPseudoClass = ":gap";
    public const string CollapsiblePseudoClass = ":collapsible";
    public const string ExpandedPseudoClass = ":expanded";
    public const string RailPseudoClass = ":rail";

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<SidebarSectionHeader, string?>(nameof(Title));

    /// <summary>The heading follows another section, so it keeps the section gap above it.</summary>
    public static readonly StyledProperty<bool> HasGapProperty =
        AvaloniaProperty.Register<SidebarSectionHeader, bool>(nameof(HasGap));

    public static readonly StyledProperty<bool> IsCollapsibleProperty =
        AvaloniaProperty.Register<SidebarSectionHeader, bool>(nameof(IsCollapsible));

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<SidebarSectionHeader, bool>(nameof(IsExpanded), true);

    public static readonly StyledProperty<bool> IsRailProperty =
        AvaloniaProperty.Register<SidebarSectionHeader, bool>(nameof(IsRail));

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public bool HasGap { get => GetValue(HasGapProperty); set => SetValue(HasGapProperty, value); }
    public bool IsCollapsible { get => GetValue(IsCollapsibleProperty); set => SetValue(IsCollapsibleProperty, value); }
    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }
    public bool IsRail { get => GetValue(IsRailProperty); set => SetValue(IsRailProperty, value); }

    private bool _pressed;

    static SidebarSectionHeader()
    {
        FocusableProperty.OverrideDefaultValue<SidebarSectionHeader>(false);
        IsTabStopProperty.OverrideDefaultValue<SidebarSectionHeader>(false);
    }

    public SidebarSectionHeader(Sidebar owner, SidebarNode node)
    {
        Owner = owner;
        Node = node;
        PseudoClasses.Set(ExpandedPseudoClass, true);
    }

    public Sidebar Owner { get; }

    public SidebarNode Node { get; }

    /// <summary>Only a titled, collapsible heading outside the rail is a control (a disclosure button).</summary>
    public bool IsInteractive => IsCollapsible && !string.IsNullOrEmpty(Title) && !IsRail;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TitleProperty)
        {
            PseudoClasses.Set(TitledPseudoClass, !string.IsNullOrEmpty(change.GetNewValue<string?>()));
            AutomationProperties.SetName(this, change.GetNewValue<string?>());
            UpdateInteractive();
        }
        else if (change.Property == HasGapProperty)
        {
            PseudoClasses.Set(GapPseudoClass, change.GetNewValue<bool>());
        }
        else if (change.Property == IsCollapsibleProperty)
        {
            PseudoClasses.Set(CollapsiblePseudoClass, change.GetNewValue<bool>());
            UpdateInteractive();
        }
        else if (change.Property == IsExpandedProperty)
        {
            PseudoClasses.Set(ExpandedPseudoClass, change.GetNewValue<bool>());
        }
        else if (change.Property == IsRailProperty)
        {
            PseudoClasses.Set(RailPseudoClass, change.GetNewValue<bool>());
            UpdateInteractive();
        }
    }

    private void UpdateInteractive()
    {
        var interactive = IsInteractive;
        Focusable = interactive;
        Cursor = interactive ? new Cursor(StandardCursorType.Hand) : null;
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        PseudoClasses.Set(SidebarPointer.HoveredPseudoClass, IsInteractive && e.Pointer.Type != PointerType.Touch);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(SidebarPointer.HoveredPseudoClass, false);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.Handled && IsInteractive && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _pressed = true;
            PseudoClasses.Set(SidebarPointer.PressedPseudoClass, true);
            e.Pointer.Capture(this);
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pressed)
        {
            return;
        }
        var activate = new Rect(Bounds.Size).Contains(e.GetPosition(this));
        _pressed = false;
        PseudoClasses.Set(SidebarPointer.PressedPseudoClass, false);
        e.Pointer.Capture(null);
        e.Handled = true;
        if (activate)
        {
            Owner.OnViewClicked(Node);
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pressed = false;
        PseudoClasses.Set(SidebarPointer.PressedPseudoClass, false);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        Owner.OnViewFocused(Node);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new SidebarSectionHeaderAutomationPeer(this);

    /// <summary>A heading, or a disclosure button when the section is collapsible.</summary>
    private sealed class SidebarSectionHeaderAutomationPeer : ControlAutomationPeer, IExpandCollapseProvider
    {
        public SidebarSectionHeaderAutomationPeer(SidebarSectionHeader owner) : base(owner)
        {
        }

        private SidebarSectionHeader Header => (SidebarSectionHeader)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore()
            => Header.IsCollapsible ? AutomationControlType.Button : AutomationControlType.Text;

        protected override bool IsContentElementCore() => !string.IsNullOrEmpty(Header.Title);

        protected override bool IsControlElementCore() => !string.IsNullOrEmpty(Header.Title);

        public ExpandCollapseState ExpandCollapseState => !Header.IsCollapsible
            ? ExpandCollapseState.LeafNode
            : Header.IsExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => false;

        public void Expand() => Header.Owner.SetNodeExpanded(Header.Node, true);

        public void Collapse() => Header.Owner.SetNodeExpanded(Header.Node, false);
    }
}

/// <summary>
/// A node of the sidebar's visible structure: a section heading (<see cref="Item"/> null) or a page row, with the
/// container of its child rows for a parent. Created and removed incrementally when the sections or their item collections change.
/// </summary>
internal sealed class SidebarNode
{
    public SidebarNode(SidebarSection section, SidebarItem? item, SidebarNode? parent)
    {
        Section = section;
        Item = item;
        Parent = parent;
    }

    public SidebarSection Section { get; }

    /// <summary>The page; null for a section heading.</summary>
    public SidebarItem? Item { get; }

    /// <summary>The parent page of a child row.</summary>
    public SidebarNode? Parent { get; }

    public SidebarRow? Row { get; set; }

    public SidebarSectionHeader? Header { get; set; }

    /// <summary>The row and its child container (for a parent), as placed in the section.</summary>
    public Control? Root { get; set; }

    /// <summary>The spring container of a parent's child rows.</summary>
    public SidebarHeightSpring? ChildrenHost { get; set; }

    public Panel? ChildrenStack { get; set; }

    /// <summary>The stack holding a parent's child rows (inside <see cref="ChildrenStack"/>).</summary>
    public StackPanel? ChildrenRows { get; set; }

    public List<SidebarNode> Children { get; } = new();

    /// <summary>The section's page rows (heading nodes only).</summary>
    public SidebarHeightSpring? SectionBody { get; set; }

    public StackPanel? SectionStack { get; set; }

    public List<SidebarNode> SectionItems { get; } = new();

    /// <summary>Position of the section in the sidebar (heading nodes only).</summary>
    public int Index { get; set; }

    /// <summary>The section's heading is in use: it has a title, or another section comes before it.</summary>
    public bool HasHeader { get; set; }

    public Control? SectionRoot { get; set; }

    /// <summary>Shown under the current filter (the row is in the layout).</summary>
    public bool IsShown { get; set; }

    /// <summary>A parent whose children are open (by the user, or by a filter that matches a child).</summary>
    public bool IsOpen { get; set; }

    public bool IsHeading => Item is null;

    public Control? View => Row is { } row ? row : Header;

    public string Label => Item?.Label ?? Section.Header ?? string.Empty;

    public bool IsEnabled => Item?.IsEnabled ?? true;
}
