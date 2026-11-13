using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using AvaloniaTextBox = Avalonia.Controls.TextBox;

namespace TKCode.Arc;

/// <summary>
/// One face of the surface under the bar: an item's panel, the folded menu or the search results. It pads its content,
/// draws the hairline that separates it from the bar, and enters and leaves on its own <see cref="Motion"/>.
/// </summary>
internal sealed class MorphNavFace : Decorator
{
    public static readonly StyledProperty<IBrush?> DividerBrushProperty =
        AvaloniaProperty.Register<MorphNavFace, IBrush?>(nameof(DividerBrush));

    public static readonly StyledProperty<double> DividerInsetProperty =
        AvaloniaProperty.Register<MorphNavFace, double>(nameof(DividerInset), 20);

    static MorphNavFace()
    {
        AffectsRender<MorphNavFace>(DividerBrushProperty, DividerInsetProperty);
    }

    public MorphNavFace(object key, bool fillsSearchWidth = false)
    {
        Key = key;
        FillsSearchWidth = fillsSearchWidth;
        Motion = new MorphNavLayerMotion(this, scales: false);
        AutomationProperties.SetLandmarkType(this, AutomationLandmarkType.Region);
    }

    /// <summary>What the face shows: a <see cref="MorphNavItem"/>, <see cref="MorphNav.MenuKey"/> or <see cref="MorphNav.SearchKey"/>.</summary>
    public object Key { get; }

    /// <summary>Whether the face takes the search surface's width rather than its own.</summary>
    public bool FillsSearchWidth { get; }

    public MorphNavLayerMotion Motion { get; }

    public IBrush? DividerBrush { get => GetValue(DividerBrushProperty); set => SetValue(DividerBrushProperty, value); }

    public double DividerInset { get => GetValue(DividerInsetProperty); set => SetValue(DividerInsetProperty, value); }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (DividerBrush is { } brush && Bounds.Width > 2 * DividerInset)
        {
            context.FillRectangle(brush, new Rect(DividerInset, 0, Bounds.Width - 2 * DividerInset, 1));
        }
    }
}

/// <summary>Gives its child a height of its width divided by <see cref="Aspect"/> (the feature card's picture).</summary>
internal sealed class MorphNavAspect : Decorator
{
    public static readonly StyledProperty<double> AspectProperty =
        AvaloniaProperty.Register<MorphNavAspect, double>(nameof(Aspect), 1.6);

    static MorphNavAspect()
    {
        AffectsMeasure<MorphNavAspect>(AspectProperty);
    }

    public double Aspect { get => GetValue(AspectProperty); set => SetValue(AspectProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 200 : availableSize.Width;
        var size = new Size(width, width / Math.Max(0.1, Aspect));
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
/// A link of a panel or of the folded menu: a glyph, the label and, in panels, a description that wraps to two lines.
/// The folded menu's links are dense (label only, smaller glyph) and drop their glyph on narrow windows.
/// </summary>
[PseudoClasses(DensePseudoClass, TinyPseudoClass)]
internal sealed class MorphNavLinkButton : MorphNavButton
{
    internal const string DensePseudoClass = ":dense";
    internal const string TinyPseudoClass = ":tiny";

    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<MorphNavLinkButton, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<MorphNavLinkButton, string?>(nameof(Label));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<MorphNavLinkButton, string?>(nameof(Description));

    public MorphNavLinkButton(object destination, string? label, string? description, Geometry? icon, bool dense, bool tiny)
    {
        Destination = destination;
        Label = label;
        Description = dense ? null : description;
        Icon = tiny ? null : icon;
        PseudoClasses.Set(DensePseudoClass, dense);
        PseudoClasses.Set(TinyPseudoClass, tiny);
        AutomationProperties.SetName(this, label);
        if (!dense && description is { Length: > 0 })
        {
            AutomationProperties.SetHelpText(this, description);
        }
    }

    /// <summary>What the link leads to: a <see cref="MorphNavLink"/>, or a plain <see cref="MorphNavItem"/> in the folded menu.</summary>
    public object Destination { get; }

    /// <summary>Raised before the click bubbles, so the nav navigates before it closes the panel.</summary>
    public event EventHandler? Invoked;

    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    protected override object RingRadiusKey => ArcDesignTokens.Key(ArcDesignTokenKind.RadiusStep18);

    protected override void OnClick()
    {
        Invoked?.Invoke(this, EventArgs.Empty);
        base.OnClick();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new LinkPeer(this);

    private sealed class LinkPeer(MorphNavLinkButton owner) : ButtonAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Hyperlink;
    }
}

/// <summary>The feature card made from a <see cref="MorphNavStory"/>: picture, title and description.</summary>
internal sealed class MorphNavStoryButton : MorphNavButton
{
    public static readonly StyledProperty<IImage?> ImageProperty =
        AvaloniaProperty.Register<MorphNavStoryButton, IImage?>(nameof(Image));

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<MorphNavStoryButton, string?>(nameof(Title));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<MorphNavStoryButton, string?>(nameof(Description));

    public MorphNavStoryButton(MorphNavStory story)
    {
        Story = story;
        Image = story.Image;
        Title = story.Title;
        Description = story.Description;
        AutomationProperties.SetName(this, story.Title);
        if (story.Description is { Length: > 0 } description)
        {
            AutomationProperties.SetHelpText(this, description);
        }
    }

    public MorphNavStory Story { get; }

    public IImage? Image { get => GetValue(ImageProperty); set => SetValue(ImageProperty, value); }

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>Raised before the click bubbles, so the nav navigates before it closes the panel.</summary>
    public event EventHandler? Invoked;

    protected override object RingRadiusKey => ArcDesignTokens.Key(ArcDesignTokenKind.RadiusStep18);

    protected override void OnClick()
    {
        Invoked?.Invoke(this, EventArgs.Empty);
        base.OnClick();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new LinkPeer(this);

    private sealed class LinkPeer(MorphNavStoryButton owner) : ButtonAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Hyperlink;
    }
}

/// <summary>A footer link made from a <see cref="MorphNavLink"/>: small secondary text and an arrow.</summary>
internal sealed class MorphNavFooterLinkButton : MorphNavButton
{
    public MorphNavFooterLinkButton(MorphNavLink link)
    {
        Link = link;
        Content = link.Label;
        AutomationProperties.SetName(this, link.Label);
    }

    public MorphNavLink Link { get; }

    /// <summary>Raised before the click bubbles, so the nav navigates before it closes the panel.</summary>
    public event EventHandler? Invoked;

    protected override object RingRadiusKey => ArcDesignTokens.Key(ArcDesignTokenKind.RadiusStep6);

    protected override void OnClick()
    {
        Invoked?.Invoke(this, EventArgs.Empty);
        base.OnClick();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new LinkPeer(this);

    private sealed class LinkPeer(MorphNavFooterLinkButton owner) : ButtonAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Hyperlink;
    }
}

/// <summary>A panel's footer: a hairline, then small secondary text or a footer link.</summary>
internal sealed class MorphNavFooter : Border
{
}

/// <summary>A group heading of the folded menu.</summary>
internal sealed class MorphNavHeading : TextBlock
{
    protected override Type StyleKeyOverride => typeof(MorphNavHeading);
}

/// <summary>
/// An item's panel: links in one or two columns, an optional feature beside them and an optional footer under both. The
/// link columns narrow when the panel would be wider than the nav allows.
/// </summary>
internal sealed class MorphNavPanelBody : Panel
{
    private readonly List<Control> _links = new();
    private Control? _feature;
    private Control? _footer;
    private double _columnWidth;
    private int _columns = 1;
    private double _block;

    public static readonly StyledProperty<double> ColumnWidthProperty =
        AvaloniaProperty.Register<MorphNavPanelBody, double>(nameof(ColumnWidth), 248);

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<MorphNavPanelBody, double>(nameof(Gap), 2);

    public static readonly StyledProperty<double> FeatureWidthProperty =
        AvaloniaProperty.Register<MorphNavPanelBody, double>(nameof(FeatureWidth), 216);

    public static readonly StyledProperty<double> FeatureGapProperty =
        AvaloniaProperty.Register<MorphNavPanelBody, double>(nameof(FeatureGap), 8);

    public static readonly StyledProperty<double> TopPaddingProperty =
        AvaloniaProperty.Register<MorphNavPanelBody, double>(nameof(TopPadding), 4);

    public static readonly StyledProperty<Thickness> FooterMarginProperty =
        AvaloniaProperty.Register<MorphNavPanelBody, Thickness>(nameof(FooterMargin), new Thickness(4, 6, 4, 0));

    static MorphNavPanelBody()
    {
        AffectsMeasure<MorphNavPanelBody>(ColumnWidthProperty, GapProperty, FeatureWidthProperty, FeatureGapProperty, TopPaddingProperty,
            FooterMarginProperty);
    }

    public MorphNavPanelBody(int columns)
    {
        _columns = Math.Clamp(columns, 1, 2);
    }

    public double ColumnWidth { get => GetValue(ColumnWidthProperty); set => SetValue(ColumnWidthProperty, value); }

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    public double FeatureWidth { get => GetValue(FeatureWidthProperty); set => SetValue(FeatureWidthProperty, value); }

    public double FeatureGap { get => GetValue(FeatureGapProperty); set => SetValue(FeatureGapProperty, value); }

    public double TopPadding { get => GetValue(TopPaddingProperty); set => SetValue(TopPaddingProperty, value); }

    public Thickness FooterMargin { get => GetValue(FooterMarginProperty); set => SetValue(FooterMarginProperty, value); }

    /// <summary>How many link columns the last layout used.</summary>
    public int Columns => _columns;

    /// <summary>The link buttons, in reading order (row by row).</summary>
    public IReadOnlyList<Control> Links => _links;

    public void AddLink(Control link)
    {
        _links.Add(link);
        Children.Add(link);
    }

    public void SetFeature(Control feature)
    {
        _feature = feature;
        Children.Add(feature);
    }

    public void SetFooter(Control footer)
    {
        _footer = footer;
        Children.Add(footer);
    }

    private double FeatureSpan => _feature is null ? 0 : FeatureGap + FeatureWidth;

    protected override Size MeasureOverride(Size availableSize)
    {
        var columns = _links.Count == 0 ? 0 : _columns;
        var linksWidth = columns == 0 ? 0 : columns * ColumnWidth + (columns - 1) * Gap;
        if (!double.IsInfinity(availableSize.Width) && linksWidth + FeatureSpan > availableSize.Width && columns > 0)
        {
            // Too wide for the nav: the link columns give way first.
            linksWidth = Math.Max(columns * 120 + (columns - 1) * Gap, availableSize.Width - FeatureSpan);
        }
        _columnWidth = columns == 0 ? 0 : (linksWidth - (columns - 1) * Gap) / columns;
        double linksHeight = 0;
        for (var start = 0; start < _links.Count; start += Math.Max(1, columns))
        {
            double row = 0;
            for (var i = start; i < Math.Min(_links.Count, start + columns); i++)
            {
                _links[i].Measure(new Size(_columnWidth, double.PositiveInfinity));
                row = Math.Max(row, _links[i].DesiredSize.Height);
            }
            linksHeight += row + (start > 0 ? Gap : 0);
        }
        double featureHeight = 0;
        if (_feature is not null)
        {
            _feature.Measure(new Size(FeatureWidth, double.PositiveInfinity));
            featureHeight = _feature.DesiredSize.Height;
        }
        _block = Math.Max(linksHeight, featureHeight);
        var width = linksWidth + FeatureSpan;
        if (_links.Count == 0 && _feature is not null)
        {
            width = FeatureWidth;
        }
        var height = TopPadding + _block;
        if (_footer is not null)
        {
            var margin = FooterMargin;
            _footer.Measure(new Size(Math.Max(0, width - margin.Left - margin.Right), double.PositiveInfinity));
            height += margin.Top + _footer.DesiredSize.Height + margin.Bottom;
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = Math.Max(1, _columns);
        var y = TopPadding;
        for (var start = 0; start < _links.Count; start += columns)
        {
            double row = 0;
            for (var i = start; i < Math.Min(_links.Count, start + columns); i++)
            {
                row = Math.Max(row, _links[i].DesiredSize.Height);
            }
            for (var i = start; i < Math.Min(_links.Count, start + columns); i++)
            {
                _links[i].Arrange(new Rect((i - start) * (_columnWidth + Gap), y, _columnWidth, row));
            }
            y += row + Gap;
        }
        if (_feature is not null)
        {
            var x = _links.Count == 0 ? 0 : finalSize.Width - FeatureWidth;
            _feature.Arrange(new Rect(x, TopPadding, FeatureWidth, _block));
        }
        if (_footer is not null)
        {
            var margin = FooterMargin;
            _footer.Arrange(new Rect(margin.Left, TopPadding + _block + margin.Top,
                Math.Max(0, finalSize.Width - margin.Left - margin.Right), _footer.DesiredSize.Height));
        }
        return finalSize;
    }
}

/// <summary>
/// The folded menu of a narrow bar: one group per item with a panel (its heading and dense links), and the plain items
/// in a last group without a heading; two groups side by side when there is room.
/// </summary>
internal sealed class MorphNavMenuBody : Panel
{
    public static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<MorphNavMenuBody, double>(nameof(ColumnGap), 4);

    public static readonly StyledProperty<double> RowGapProperty =
        AvaloniaProperty.Register<MorphNavMenuBody, double>(nameof(RowGap), 2);

    public static readonly StyledProperty<double> TopPaddingProperty =
        AvaloniaProperty.Register<MorphNavMenuBody, double>(nameof(TopPadding), 4);

    public static readonly StyledProperty<double> TwoColumnWidthProperty =
        AvaloniaProperty.Register<MorphNavMenuBody, double>(nameof(TwoColumnWidth), 260);

    private int _columns = 2;
    private double _columnWidth;
    private readonly List<double> _rows = new();

    static MorphNavMenuBody()
    {
        AffectsMeasure<MorphNavMenuBody>(ColumnGapProperty, RowGapProperty, TopPaddingProperty, TwoColumnWidthProperty);
    }

    public double ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }

    public double RowGap { get => GetValue(RowGapProperty); set => SetValue(RowGapProperty, value); }

    public double TopPadding { get => GetValue(TopPaddingProperty); set => SetValue(TopPaddingProperty, value); }

    public double TwoColumnWidth { get => GetValue(TwoColumnWidthProperty); set => SetValue(TwoColumnWidthProperty, value); }

    /// <summary>How many group columns the last layout used.</summary>
    public int Columns => _columns;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 2 * 216 + ColumnGap : availableSize.Width;
        _columns = width >= TwoColumnWidth ? 2 : 1;
        _columnWidth = (width - (_columns - 1) * ColumnGap) / _columns;
        _rows.Clear();
        var height = TopPadding;
        for (var start = 0; start < Children.Count; start += _columns)
        {
            double row = 0;
            for (var i = start; i < Math.Min(Children.Count, start + _columns); i++)
            {
                Children[i].Measure(new Size(_columnWidth, double.PositiveInfinity));
                row = Math.Max(row, Children[i].DesiredSize.Height);
            }
            _rows.Add(row);
            height += row + (start > 0 ? RowGap : 0);
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var y = TopPadding;
        for (var r = 0; r < _rows.Count; r++)
        {
            for (var c = 0; c < _columns; c++)
            {
                var i = r * _columns + c;
                if (i < Children.Count)
                {
                    Children[i].Arrange(new Rect(c * (_columnWidth + ColumnGap), y, _columnWidth, _rows[r]));
                }
            }
            y += _rows[r] + RowGap;
        }
        return finalSize;
    }
}

/// <summary>The bare text box of the search bar (the surface is its frame).</summary>
internal sealed class MorphNavSearchInput : AvaloniaTextBox
{
    protected override Type StyleKeyOverride => typeof(MorphNavSearchInput);

    protected override AutomationPeer OnCreateAutomationPeer() => new InputPeer(this);

    /// <summary>The input reads as a combo box that drives the result list.</summary>
    private sealed class InputPeer(MorphNavSearchInput owner) : TextBoxAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ComboBox;
    }
}

/// <summary>The search bar that grows out of the surface: a glyph, the input and a close button.</summary>
[TemplatePart(InputPart, typeof(MorphNavSearchInput))]
[TemplatePart(ClosePart, typeof(MorphNavToolButton))]
[PseudoClasses(MorphNavButton.CompactPseudoClass)]
internal sealed class MorphNavSearchBar : TemplatedControl
{
    internal const string InputPart = "PART_Input";
    internal const string ClosePart = "PART_Close";

    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<MorphNavSearchBar, string?>(nameof(Placeholder));

    public MorphNavSearchBar()
    {
        Motion = new MorphNavLayerMotion(this, scales: true);
    }

    public string? Placeholder { get => GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }

    public MorphNavLayerMotion Motion { get; }

    public MorphNavSearchInput? Input { get; private set; }

    public MorphNavToolButton? CloseButton { get; private set; }

    /// <summary>Raised when the template's parts are in place.</summary>
    public event EventHandler? PartsReady;

    public void SetCompact(bool compact)
    {
        PseudoClasses.Set(MorphNavButton.CompactPseudoClass, compact);
        CloseButton?.SetCompact(compact);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Input = e.NameScope.Find<MorphNavSearchInput>(InputPart);
        CloseButton = e.NameScope.Find<MorphNavToolButton>(ClosePart);
        CloseButton?.SetCompact(Classes.Contains(MorphNavButton.CompactPseudoClass));
        PartsReady?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// A search result: glyph, label and description, the group at the end, and the enter glyph while it is the active
/// result. It never takes focus; the input keeps it while the results are browsed.
/// </summary>
[PseudoClasses(ActivePseudoClass)]
internal sealed class MorphNavResultRow : TemplatedControl
{
    internal const string ActivePseudoClass = ":active";

    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<MorphNavResultRow, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<MorphNavResultRow, string?>(nameof(Label));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<MorphNavResultRow, string?>(nameof(Description));

    public static readonly StyledProperty<string?> GroupProperty =
        AvaloniaProperty.Register<MorphNavResultRow, string?>(nameof(Group));

    private readonly MorphNavSearchBody _body;
    private bool _pressed;

    static MorphNavResultRow()
    {
        FocusableProperty.OverrideDefaultValue<MorphNavResultRow>(false);
        CursorProperty.OverrideDefaultValue<MorphNavResultRow>(new Cursor(StandardCursorType.Hand));
    }

    public MorphNavResultRow(MorphNavSearchBody body)
    {
        _body = body;
    }

    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    public string? Group { get => GetValue(GroupProperty); set => SetValue(GroupProperty, value); }

    public int Index { get; set; }

    public MorphNavSearchEntry? Entry { get; private set; }

    public bool IsActive { get; private set; }

    public void Bind(MorphNavSearchEntry entry, int index)
    {
        Entry = entry;
        Index = index;
        Icon = entry.Icon;
        Label = entry.Label;
        Description = entry.Description;
        Group = entry.Group;
        AutomationProperties.SetName(this, entry.Label);
        AutomationProperties.SetHelpText(this, entry.Description);
        AutomationProperties.SetPositionInSet(this, index + 1);
    }

    public void SetActive(bool active)
    {
        IsActive = active;
        PseudoClasses.Set(ActivePseudoClass, active);
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type == PointerType.Mouse)
        {
            _body.Activate(Index);
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (e.Pointer.Type == PointerType.Mouse && !IsActive)
        {
            _body.Activate(Index);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _pressed = true;
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
        _pressed = false;
        e.Pointer.Capture(null);
        e.Handled = true;
        if (new Rect(Bounds.Size).Contains(e.GetPosition(this)))
        {
            _body.Invoke(Index);
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pressed = false;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new RowPeer(this);

    private sealed class RowPeer(MorphNavResultRow owner) : ControlAutomationPeer(owner), ISelectionItemProvider, IInvokeProvider
    {
        private MorphNavResultRow Row => (MorphNavResultRow)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        public bool IsSelected => Row.IsActive;

        public ISelectionProvider? SelectionContainer => null;

        public void AddToSelection() => Select();

        public void RemoveFromSelection()
        {
        }

        public void Select() => Row._body.Activate(Row.Index);

        public void Invoke() => Row._body.Invoke(Row.Index);
    }
}

/// <summary>
/// The results under the search bar: a heading that also announces the count (polite live region) and up to six rows,
/// reused as the query changes.
/// </summary>
internal sealed class MorphNavSearchBody : StackPanel
{
    private readonly MorphNav _owner;
    private readonly List<MorphNavResultRow> _rows = new();
    private readonly StackPanel _list;

    public MorphNavSearchBody(MorphNav owner)
    {
        _owner = owner;
        Heading = new MorphNavHeading { Classes = { "results" } };
        AutomationProperties.SetLiveSetting(Heading, AutomationLiveSetting.Polite);
        _list = new MorphNavResultList();
        Children.Add(Heading);
        Children.Add(_list);
    }

    public TextBlock Heading { get; }

    public StackPanel List => _list;

    /// <summary>The rows on show.</summary>
    public IEnumerable<MorphNavResultRow> Rows => _rows.Where(r => r.IsVisible);

    public int ActiveIndex { get; private set; } = -1;

    public void Show(IReadOnlyList<MorphNavSearchEntry> results, string heading, int active)
    {
        Heading.Text = heading;
        while (_rows.Count < results.Count)
        {
            var row = new MorphNavResultRow(this);
            _rows.Add(row);
            _list.Children.Add(row);
        }
        for (var i = 0; i < _rows.Count; i++)
        {
            var visible = i < results.Count;
            _rows[i].IsVisible = visible;
            if (visible)
            {
                _rows[i].Bind(results[i], i);
                AutomationProperties.SetSizeOfSet(_rows[i], results.Count);
            }
        }
        SetActive(active);
    }

    public void SetActive(int index)
    {
        ActiveIndex = index;
        for (var i = 0; i < _rows.Count; i++)
        {
            _rows[i].SetActive(i == index && _rows[i].IsVisible);
        }
    }

    public void Activate(int index) => _owner.SetActiveResult(index);

    public void Invoke(int index) => _owner.InvokeResult(index);
}

/// <summary>The list of search results (a listbox for assistive technology).</summary>
internal sealed class MorphNavResultList : StackPanel
{
    protected override AutomationPeer OnCreateAutomationPeer() => new ListPeer(this);

    private sealed class ListPeer(MorphNavResultList owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}
