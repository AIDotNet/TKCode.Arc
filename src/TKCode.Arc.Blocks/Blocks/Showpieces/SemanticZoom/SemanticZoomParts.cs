using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using AvaloniaButton = Avalonia.Controls.Button;
using AvaloniaToggleButton = Avalonia.Controls.Primitives.ToggleButton;

namespace TKCode.Arc;

/// <summary>
/// The map and the details panel of <see cref="SemanticZoom"/>: the panel beside the map (264 px) or under it. The map is
/// <see cref="StageHeight"/> high (48% of the inside width between 400 and 580; 420 in compact containers).
/// </summary>
internal sealed class SemanticZoomBodyPanel : Panel
{
    public const double CompactStageHeight = 420;

    public const double MinStageHeight = 400;

    public const double MaxStageHeight = 580;

    public const double StageWidthShare = 0.48;

    public static readonly StyledProperty<double> PanelWidthProperty =
        AvaloniaProperty.Register<SemanticZoomBodyPanel, double>(nameof(PanelWidth), 264);

    private bool _side;
    private bool _showsPanel = true;
    private double _stageHeight = MinStageHeight;

    static SemanticZoomBodyPanel()
    {
        AffectsMeasure<SemanticZoomBodyPanel>(PanelWidthProperty);
    }

    /// <summary>Width of the side panel including its divider.</summary>
    public double PanelWidth { get => GetValue(PanelWidthProperty); set => SetValue(PanelWidthProperty, value); }

    /// <summary>The map height for an inside width (48%, clamped to 400 … 580).</summary>
    public static double StageHeightFor(double innerWidth) =>
        Math.Clamp(StageWidthShare * innerWidth, MinStageHeight, MaxStageHeight);

    public bool IsSide
    {
        get => _side;
        set
        {
            if (_side != value)
            {
                _side = value;
                InvalidateMeasure();
            }
        }
    }

    public bool ShowsPanel
    {
        get => _showsPanel;
        set
        {
            if (_showsPanel != value)
            {
                _showsPanel = value;
                InvalidateMeasure();
            }
        }
    }

    public double StageHeight
    {
        get => _stageHeight;
        set
        {
            if (Math.Abs(_stageHeight - value) > 0.01)
            {
                _stageHeight = value;
                InvalidateMeasure();
            }
        }
    }

    private Control? StageHost => Children.Count > 0 ? Children[0] : null;

    private Control? PanelHost => Children.Count > 1 ? Children[1] : null;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 663;
        var panel = PanelHost;
        if (panel is not null)
        {
            panel.IsVisible = _showsPanel;
        }
        if (_side && _showsPanel && panel is not null)
        {
            var stageWidth = Math.Max(0, width - PanelWidth);
            StageHost?.Measure(new Size(stageWidth, _stageHeight));
            panel.Measure(new Size(PanelWidth, double.PositiveInfinity));
            return new Size(width, Math.Max(_stageHeight, panel.DesiredSize.Height));
        }
        StageHost?.Measure(new Size(width, _stageHeight));
        var below = 0.0;
        if (_showsPanel && panel is not null)
        {
            panel.Measure(new Size(width, double.PositiveInfinity));
            below = panel.DesiredSize.Height;
        }
        return new Size(width, _stageHeight + below);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var panel = PanelHost;
        if (_side && _showsPanel && panel is not null)
        {
            // The map keeps its own height even when the control is stretched taller (the panel then fills the side).
            var stageWidth = Math.Max(0, finalSize.Width - PanelWidth);
            var height = Math.Max(_stageHeight, panel.DesiredSize.Height);
            StageHost?.Arrange(new Rect(0, 0, stageWidth, height));
            panel.Arrange(new Rect(stageWidth, 0, PanelWidth, Math.Max(height, finalSize.Height)));
            return finalSize;
        }
        StageHost?.Arrange(new Rect(0, 0, finalSize.Width, _stageHeight));
        if (_showsPanel && panel is not null)
        {
            panel.Arrange(new Rect(0, _stageHeight, finalSize.Width, Math.Max(0, finalSize.Height - _stageHeight)));
        }
        return finalSize;
    }
}

/// <summary>
/// The breadcrumbs over the map's top-left corner: every level above the current one is a button, the current one plain
/// text; levels appear with a 240 ms fade and leave with a 120 ms one. Long paths are clipped, not folded.
/// </summary>
/// <remarks>
/// Code-driven motion: a crumb that leaves must be removed after its fade ends, which a ControlTheme transition cannot
/// signal; entering crumbs fade on a <see cref="MotionValue{T}"/> for the same timing source.
/// </remarks>
internal sealed class SemanticZoomCrumbs : TemplatedControl
{
    public const string ItemsPart = "PART_Items";

    /// <summary>Chevron size between crumbs.</summary>
    public const double SeparatorSize = 14;

    private readonly List<(SemanticZoomNode Node, SemanticZoomCrumb Crumb, Control? Separator)> _entries = new();
    private StackPanel? _items;

    protected override Type StyleKeyOverride => typeof(SemanticZoomCrumbs);

    /// <summary>A level above the current one was clicked.</summary>
    public event EventHandler<SemanticZoomNode>? Chosen;

    /// <summary>The crumbs in order (tests).</summary>
    public IReadOnlyList<SemanticZoomCrumb> Items => _entries.Select(e => e.Crumb).ToList();

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _items?.Children.Clear();
        _items = e.NameScope.Find<StackPanel>(ItemsPart);
        if (_items is not null)
        {
            foreach (var (_, crumb, separator) in _entries)
            {
                if (separator is not null)
                {
                    _items.Children.Add(separator);
                }
                _items.Children.Add(crumb);
            }
        }
    }

    /// <summary>Shows <paramref name="path"/> (organization first), reusing the crumbs it shares with the current path.</summary>
    public void SetPath(IReadOnlyList<SemanticZoomNode> path)
    {
        var shared = 0;
        while (shared < path.Count && shared < _entries.Count && ReferenceEquals(path[shared], _entries[shared].Node))
        {
            shared++;
        }
        var replaced = path.Count > shared;
        for (var i = _entries.Count - 1; i >= shared; i--)
        {
            var (_, crumb, separator) = _entries[i];
            _entries.RemoveAt(i);
            Leave(crumb, separator, fade: !replaced);
        }
        var animate = ArcMotion.IsFull(this) && _entries.Count > 0;
        for (var i = shared; i < path.Count; i++)
        {
            var node = path[i];
            Control? separator = null;
            if (i > 0)
            {
                var glyph = new ArcGlyph
                {
                    Data = ArcIcons.ChevronRight,
                    Size = SeparatorSize,
                    Margin = new Thickness(1, 0),
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    IsHitTestVisible = false
                };
                ArcTokens.Bind(glyph, ArcGlyph.ForegroundProperty, ArcDesignTokenKind.TextMuted);
                separator = glyph;
                _items?.Children.Add(separator);
            }
            var crumb = new SemanticZoomCrumb { Node = node, Content = node.Title };
            crumb.Click += OnCrumbClick;
            _items?.Children.Add(crumb);
            _entries.Add((node, crumb, separator));
            if (animate)
            {
                Enter(crumb, separator);
            }
        }
        for (var i = 0; i < _entries.Count; i++)
        {
            var (node, crumb, _) = _entries[i];
            crumb.IsCurrent = i == _entries.Count - 1;
            if (!Equals(crumb.Content, node.Title))
            {
                crumb.Content = node.Title;
            }
        }
    }

    private static void Enter(SemanticZoomCrumb crumb, Control? separator)
    {
        var fade = new MotionValue<double>(crumb, 0, MotionVectors.Double);
        fade.Changed += v =>
        {
            crumb.Opacity = v;
            if (separator is not null)
            {
                separator.Opacity = v;
            }
        };
        crumb.Opacity = 0;
        if (separator is not null)
        {
            separator.Opacity = 0;
        }
        _ = fade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, SemanticZoomMotion.EaseOut));
    }

    private void Leave(SemanticZoomCrumb crumb, Control? separator, bool fade)
    {
        crumb.Click -= OnCrumbClick;
        crumb.IsHitTestVisible = false;
        if (!fade || !ArcMotion.IsFull(this))
        {
            Remove(crumb, separator);
            return;
        }
        var value = new MotionValue<double>(crumb, crumb.Opacity, MotionVectors.Double);
        value.Changed += v =>
        {
            crumb.Opacity = v;
            if (separator is not null)
            {
                separator.Opacity = v;
            }
        };
        _ = FadeOut(value, crumb, separator);
    }

    private async Task FadeOut(MotionValue<double> value, SemanticZoomCrumb crumb, Control? separator)
    {
        await value.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Instant, SemanticZoomMotion.EaseOut));
        Remove(crumb, separator);
    }

    private void Remove(SemanticZoomCrumb crumb, Control? separator)
    {
        if (_items is null)
        {
            return;
        }
        _items.Children.Remove(crumb);
        if (separator is not null)
        {
            _items.Children.Remove(separator);
        }
    }

    private void OnCrumbClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is SemanticZoomCrumb { IsCurrent: false, Node: { } node })
        {
            Chosen?.Invoke(this, node);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new SemanticZoomGroupAutomationPeer(this);
}

/// <summary>One breadcrumb: a level above the current one (a button) or the current one (plain, not focusable).</summary>
[PseudoClasses(CurrentPseudoClass)]
internal sealed class SemanticZoomCrumb : AvaloniaButton
{
    public const string CurrentPseudoClass = ":current";

    private bool _current;

    protected override Type StyleKeyOverride => typeof(SemanticZoomCrumb);

    public SemanticZoomNode? Node { get; set; }

    /// <summary>The crumb is the current level (not clickable).</summary>
    public bool IsCurrent
    {
        get => _current;
        set
        {
            _current = value;
            PseudoClasses.Set(CurrentPseudoClass, value);
            Focusable = !value;
            IsHitTestVisible = !value;
        }
    }
}

/// <summary>
/// The tour button: a play glyph and the start label, or a pause glyph and the stop label (the two labels share one slot
/// as wide as the longer); only the glyph in compact containers. Pressed state = playing.
/// </summary>
[PseudoClasses(CompactPseudoClass)]
internal sealed class SemanticZoomTourButton : AvaloniaToggleButton
{
    public const string CompactPseudoClass = ":compact";

    public static readonly StyledProperty<string> StartTextProperty =
        AvaloniaProperty.Register<SemanticZoomTourButton, string>(nameof(StartText), string.Empty);

    public static readonly StyledProperty<string> StopTextProperty =
        AvaloniaProperty.Register<SemanticZoomTourButton, string>(nameof(StopText), string.Empty);

    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<SemanticZoomTourButton, bool>(nameof(IsCompact));

    protected override Type StyleKeyOverride => typeof(SemanticZoomTourButton);

    public string StartText { get => GetValue(StartTextProperty); set => SetValue(StartTextProperty, value); }

    public string StopText { get => GetValue(StopTextProperty); set => SetValue(StopTextProperty, value); }

    public bool IsCompact { get => GetValue(IsCompactProperty); set => SetValue(IsCompactProperty, value); }

    /// <summary>The playing state is owned by the map (a click asks it to toggle the tour).</summary>
    protected override void Toggle()
    {
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsCompactProperty)
        {
            PseudoClasses.Set(CompactPseudoClass, change.GetNewValue<bool>());
        }
        if (change.Property == IsCheckedProperty || change.Property == StartTextProperty || change.Property == StopTextProperty)
        {
            AutomationProperties.SetName(this, IsChecked == true ? StopText : StartText);
        }
    }
}

/// <summary>
/// The minimap (bottom left, 170 × 86 inside its frame; 126 × 64 compact): team panels, project cards as solid blocks, the
/// view as an accent box at least 6 px square; teams outside the current one dimmed. Press or drag to move the view there.
/// </summary>
internal sealed class SemanticZoomMinimap : Control
{
    /// <summary>Space kept around the map inside the minimap.</summary>
    public const double Inset = 6;

    /// <summary>Smallest drawn view box.</summary>
    public const double MinViewBox = 6;

    private static readonly Cursor s_hand = new(StandardCursorType.Hand);

    private SemanticZoom? _host;
    private bool _compact;
    private bool _dragging;
    private bool _colorsDirty = true;
    private IBrush? _teamFill;
    private IPen? _teamPen;
    private IBrush? _teamFillDim;
    private IPen? _teamPenDim;
    private IBrush? _project;
    private IBrush? _projectDim;
    private IBrush? _viewFill;
    private IPen? _viewPen;

    public SemanticZoomMinimap()
    {
        Cursor = s_hand;
        ClipToBounds = true;
        ResourcesChanged += (_, _) => MarkColors();
        ActualThemeVariantChanged += (_, _) => MarkColors();
    }

    internal SemanticZoom? Host
    {
        get => _host;
        set
        {
            _host = value;
            InvalidateVisual();
        }
    }

    public bool IsCompact
    {
        get => _compact;
        set => _compact = value;
    }

    private void MarkColors()
    {
        _colorsDirty = true;
        InvalidateVisual();
    }

    /// <summary>The scale and offset that map world units into the minimap.</summary>
    internal (double Scale, Vector Offset) Mapping(Rect content, Size size)
    {
        if (content.Width <= 0 || content.Height <= 0 || size.Width <= 2 * Inset || size.Height <= 2 * Inset)
        {
            return (0, default);
        }
        var scale = Math.Min((size.Width - 2 * Inset) / content.Width, (size.Height - 2 * Inset) / content.Height);
        var offset = new Vector((size.Width - content.Width * scale) / 2 - content.X * scale,
            (size.Height - content.Height * scale) / 2 - content.Y * scale);
        return (scale, offset);
    }

    private void EnsureColors()
    {
        if (!_colorsDirty)
        {
            return;
        }
        _colorsDirty = false;
        var muted = ArcTokens.GetColor(this, ArcDesignTokenKind.SurfaceMuted);
        var border = ArcTokens.GetColor(this, ArcDesignTokenKind.Border);
        var strong = ArcTokens.GetColor(this, ArcDesignTokenKind.BorderStrong);
        var accent = ArcTokens.GetColor(this, ArcDesignTokenKind.Accent);
        const double dim = 0.65;
        _teamFill = new ImmutableSolidColorBrush(muted);
        _teamPen = new ImmutablePen(new ImmutableSolidColorBrush(border), 1);
        _teamFillDim = new ImmutableSolidColorBrush(muted, dim);
        _teamPenDim = new ImmutablePen(new ImmutableSolidColorBrush(border, dim), 1);
        _project = new ImmutableSolidColorBrush(strong);
        _projectDim = new ImmutableSolidColorBrush(strong, dim);
        _viewFill = new ImmutableSolidColorBrush(accent, 0.14);
        _viewPen = new ImmutablePen(new ImmutableSolidColorBrush(accent), 1.5);
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        context.DrawRectangle(Brushes.Transparent, null, new Rect(size));
        var stage = _host?.Stage;
        if (stage is null)
        {
            return;
        }
        var layout = stage.Layout;
        var (scale, offset) = Mapping(layout.ContentBounds, size);
        if (scale <= 0)
        {
            return;
        }
        EnsureColors();
        var scope = stage.CurrentScope;
        var boxes = layout.Boxes;
        for (var t = 0; t < layout.TeamCount; t++)
        {
            var inScope = scope.Team < 0 || scope.Team == t;
            var rect = Map(boxes[t].Rect, scale, offset);
            context.DrawRectangle(inScope ? _teamFill : _teamFillDim, inScope ? _teamPen : _teamPenDim, new RoundedRect(rect, 3));
            for (var p = boxes[t].FirstChild; p < boxes[t].FirstChild + boxes[t].ChildCount; p++)
            {
                context.DrawRectangle(inScope ? _project : _projectDim, null, Map(boxes[p].Rect, scale, offset));
            }
        }
        var view = Map(stage.View.VisibleWorldRect, scale, offset);
        if (view.Width < MinViewBox || view.Height < MinViewBox)
        {
            var c = view.Center;
            view = new Rect(c.X - Math.Max(MinViewBox, view.Width) / 2, c.Y - Math.Max(MinViewBox, view.Height) / 2,
                Math.Max(MinViewBox, view.Width), Math.Max(MinViewBox, view.Height));
        }
        context.DrawRectangle(_viewFill, _viewPen, new RoundedRect(view, 2.5));
    }

    private static Rect Map(Rect world, double scale, Vector offset) =>
        new(world.X * scale + offset.X, world.Y * scale + offset.Y, world.Width * scale, world.Height * scale);

    /// <summary>The world point under a minimap point.</summary>
    internal Point ToWorld(Point point)
    {
        var stage = _host?.Stage;
        if (stage is null)
        {
            return default;
        }
        var (scale, offset) = Mapping(stage.Layout.ContentBounds, Bounds.Size);
        return scale <= 0 ? default : new Point((point.X - offset.X) / scale, (point.Y - offset.Y) / scale);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || _host is null)
        {
            return;
        }
        _dragging = true;
        e.Pointer.Capture(this);
        _host.OnMinimapCenter(ToWorld(e.GetPosition(this)));
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragging && _host is not null)
        {
            _host.OnMinimapCenter(ToWorld(e.GetPosition(this)));
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragging)
        {
            _dragging = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _dragging = false;
    }
}

/// <summary>A person's round picture or initials (panel avatar stack, hover cards).</summary>
internal sealed class SemanticZoomAvatar : Control
{
    public static readonly StyledProperty<SemanticZoomPerson?> PersonProperty =
        AvaloniaProperty.Register<SemanticZoomAvatar, SemanticZoomPerson?>(nameof(Person));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<SemanticZoomAvatar, string?>(nameof(Label));

    public static readonly StyledProperty<double> RingProperty =
        AvaloniaProperty.Register<SemanticZoomAvatar, double>(nameof(Ring));

    private ShowpieceText? _text;
    private string? _textString;
    private double _textSize;
    private bool _colorsDirty = true;
    private IBrush? _fill;
    private IBrush? _ring;
    private IBrush? _ink;
    private Typeface _typeface;

    static SemanticZoomAvatar()
    {
        AffectsRender<SemanticZoomAvatar>(PersonProperty, LabelProperty, RingProperty);
    }

    public SemanticZoomAvatar()
    {
        IsHitTestVisible = false;
        ResourcesChanged += (_, _) => MarkColors();
        ActualThemeVariantChanged += (_, _) => MarkColors();
    }

    /// <summary>The person shown (null with a <see cref="Label"/>: a "+N" chip).</summary>
    public SemanticZoomPerson? Person { get => GetValue(PersonProperty); set => SetValue(PersonProperty, value); }

    /// <summary>Text instead of a person ("+15").</summary>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>Width of the surface-colored ring around the circle (avatar stacks: 2).</summary>
    public double Ring { get => GetValue(RingProperty); set => SetValue(RingProperty, value); }

    private void MarkColors()
    {
        _colorsDirty = true;
        _text = null;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (_colorsDirty)
        {
            _colorsDirty = false;
            _fill = new ImmutableSolidColorBrush(ArcTokens.GetColor(this, ArcDesignTokenKind.SurfaceMuted));
            _ring = new ImmutableSolidColorBrush(ArcTokens.GetColor(this, ArcDesignTokenKind.Surface));
            _ink = new ImmutableSolidColorBrush(ArcTokens.GetColor(this, ArcDesignTokenKind.TextSecondary));
            FontFamily family;
            try
            {
                family = ArcTokens.Get<FontFamily>(this, ArcDesignTokenKind.BodyFontFamily) ?? FontFamily.Default;
            }
            catch (Exception)
            {
                family = FontFamily.Default;
            }
            _typeface = new Typeface(family, FontStyle.Normal, FontWeight.Medium);
            _text = null;
        }
        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 0)
        {
            return;
        }
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var ring = Ring;
        if (ring > 0)
        {
            context.DrawEllipse(_ring, null, center, size / 2, size / 2);
        }
        var radius = size / 2 - ring;
        var image = Person?.Avatar;
        if (image is not null)
        {
            using (context.PushGeometryClip(new EllipseGeometry(new Rect(center.X - radius, center.Y - radius, 2 * radius, 2 * radius))))
            {
                context.DrawImage(image, new Rect(center.X - radius, center.Y - radius, 2 * radius, 2 * radius));
            }
            return;
        }
        context.DrawEllipse(_fill, null, center, radius, radius);
        var text = Label ?? Person?.DisplayInitials;
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        var fontSize = Label is not null ? 12 : Math.Max(7, Math.Round(radius * 0.8 * 2) / 2);
        if (_text is null || _textString != text || _textSize != fontSize)
        {
            _textString = text;
            _textSize = fontSize;
            _text = ShowpieceText.Lay(text, _typeface, fontSize);
        }
        _text.Draw(context, _ink!, new Point(center.X - _text.WidthIncludingTrailingWhitespace / 2, center.Y - _text.Height / 2));
    }
}

/// <summary>
/// The hover card's content: name and a line under it (a team's description, a project's or task's status dot, status and
/// due date / project), four label–value rows and a closing hint, per kind of node.
/// </summary>
internal sealed class SemanticZoomCard : TemplatedControl
{
    public const string TitlePart = "PART_Title";
    public const string SubtitlePart = "PART_Subtitle";
    public const string DotPart = "PART_Dot";
    public const string FooterPart = "PART_Footer";

    private TextBlock? _title;
    private TextBlock? _subtitle;
    private Border? _dot;
    private TextBlock? _footer;
    private readonly TextBlock?[] _labels = new TextBlock?[4];
    private readonly TextBlock?[] _values = new TextBlock?[4];
    private readonly TextBlock?[] _notes = new TextBlock?[4];
    private readonly SemanticZoomAvatar?[] _faces = new SemanticZoomAvatar?[4];
    private SemanticZoomTexts? _texts;
    private ISemanticZoomFormatter? _formatter;

    protected override Type StyleKeyOverride => typeof(SemanticZoomCard);

    /// <summary>The node shown.</summary>
    public SemanticZoomNode? Node { get; private set; }

    /// <summary>The text of every visible line (tests).</summary>
    internal IReadOnlyList<string> Lines
    {
        get
        {
            var lines = new List<string>();
            void Add(TextBlock? text)
            {
                if (text is { IsVisible: true, Text: { Length: > 0 } t })
                {
                    lines.Add(t);
                }
            }
            Add(_title);
            Add(_subtitle);
            for (var i = 0; i < 4; i++)
            {
                Add(_labels[i]);
                Add(_values[i]);
                Add(_notes[i]);
            }
            Add(_footer);
            return lines;
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        var scope = e.NameScope;
        _title = scope.Find<TextBlock>(TitlePart);
        _subtitle = scope.Find<TextBlock>(SubtitlePart);
        _dot = scope.Find<Border>(DotPart);
        _footer = scope.Find<TextBlock>(FooterPart);
        for (var i = 0; i < 4; i++)
        {
            _labels[i] = scope.Find<TextBlock>($"PART_Label{i}");
            _values[i] = scope.Find<TextBlock>($"PART_Value{i}");
            _notes[i] = scope.Find<TextBlock>($"PART_Note{i}");
            _faces[i] = scope.Find<SemanticZoomAvatar>($"PART_Face{i}");
        }
        if (Node is not null && _texts is not null && _formatter is not null)
        {
            Show(Node, _texts, _formatter);
        }
    }

    /// <summary>Fills the card for <paramref name="node"/>.</summary>
    public void Show(SemanticZoomNode node, SemanticZoomTexts texts, ISemanticZoomFormatter formatter)
    {
        Node = node;
        _texts = texts;
        _formatter = formatter;
        if (_title is null)
        {
            return;
        }
        _title.Text = node.Title;
        string? none = null;
        switch (node)
        {
            case SemanticZoomTeam team:
                SetSubtitle(team.Description, null);
                Row(0, texts.CardLeadLabel, team.Lead?.Name ?? texts.NobodyText, null, team.Lead);
                Row(1, texts.CardProjectsLabel, team.ProjectCount.ToString(formatter is SemanticZoomFormatter f ? f.Culture : null),
                    team.NeedAttention > 0 ? SemanticZoomTexts.Format(texts.CardAttentionFormat, team.NeedAttention) : none, null);
                Row(2, texts.CardOpenTasksLabel, team.OpenTasks.ToString(), null, null);
                Row(3, texts.CardProgressLabel, formatter.FormatPercent(team.Progress), null, null);
                _footer!.Text = texts.CardTeamHint;
                break;
            case SemanticZoomProject project:
                SetSubtitle(SemanticZoomLabels.ProjectStatus(project, texts, formatter), HealthColor(project.Health));
                Row(0, texts.CardProgressLabel, formatter.FormatPercent(project.Progress), null, null);
                Row(1, texts.CardTasksDoneLabel, SemanticZoomTexts.Format(texts.CountOfFormat, project.DoneTasks, project.TaskCount), null, null);
                Row(2, texts.CardBlockedLabel, project.BlockedTasks.ToString(), null, null);
                Row(3, texts.CardLeadLabel, project.Lead?.Name ?? texts.NobodyText, null, project.Lead);
                _footer!.Text = texts.CardProjectHint;
                break;
            case SemanticZoomTask task:
                SetSubtitle(SemanticZoomTexts.Format(texts.CardTaskSubtitleFormat, texts.StatusName(task.Status), task.Project?.Title ?? string.Empty),
                    StatusColor(task.Status));
                Row(0, texts.CardAssigneeLabel, task.Assignee?.Name ?? texts.NobodyText, null, task.Assignee);
                Row(1, texts.CardDueLabel, task.Due is { } due ? formatter.FormatDate(due) : texts.NoneText, null, null);
                Row(2, texts.CardChecklistLabel, SemanticZoomTexts.Format(texts.CountOfFormat, task.ChecklistDone, task.Checklist.Count), null, null);
                Row(3, texts.CardEstimateLabel, SemanticZoomTexts.Format(task.Points == 1 ? texts.PointFormat : texts.PointsFormat, task.Points), null, null);
                _footer!.Text = texts.CardTaskHint;
                break;
        }
    }

    private void SetSubtitle(string? text, ArcDesignTokenKind? dot)
    {
        if (_subtitle is not null)
        {
            _subtitle.Text = text;
            _subtitle.IsVisible = !string.IsNullOrEmpty(text);
        }
        if (_dot is not null)
        {
            _dot.IsVisible = dot is not null;
            if (dot is { } kind)
            {
                _dot.Background = new ImmutableSolidColorBrush(ArcTokens.GetColor(this, kind));
            }
        }
    }

    private static ArcDesignTokenKind HealthColor(SemanticZoomHealth health) => health switch
    {
        SemanticZoomHealth.AtRisk => ArcDesignTokenKind.Warning,
        SemanticZoomHealth.OffTrack => ArcDesignTokenKind.Danger,
        _ => ArcDesignTokenKind.Success
    };

    private static ArcDesignTokenKind StatusColor(SemanticZoomTaskStatus status) => status switch
    {
        SemanticZoomTaskStatus.Done => ArcDesignTokenKind.Success,
        SemanticZoomTaskStatus.Blocked => ArcDesignTokenKind.Danger,
        SemanticZoomTaskStatus.Todo => ArcDesignTokenKind.TextMuted,
        _ => ArcDesignTokenKind.Foreground
    };

    private void Row(int i, string label, string value, string? note, SemanticZoomPerson? person)
    {
        if (_labels[i] is { } l)
        {
            l.Text = label;
        }
        if (_values[i] is { } v)
        {
            v.Text = value;
        }
        if (_notes[i] is { } n)
        {
            n.Text = note;
            n.IsVisible = !string.IsNullOrEmpty(note);
        }
        if (_faces[i] is { } face)
        {
            face.Person = person;
            face.IsVisible = person is not null;
        }
    }
}

/// <summary>Screen-reader table of every project (team, health, progress, open, blocked, due); takes no space.</summary>
internal sealed class SemanticZoomHiddenTable : Panel
{
    public SemanticZoomHiddenTable()
    {
        IsHitTestVisible = false;
        Focusable = false;
        ClipToBounds = true;
    }

    /// <summary>The rows (tests and screen readers).</summary>
    public IReadOnlyList<string> Rows => Children.OfType<TextBlock>().Select(t => t.Text ?? string.Empty).ToList();

    public void SetRows(string? caption, IReadOnlyList<string> rows)
    {
        AutomationProperties.SetName(this, caption);
        while (Children.Count > rows.Count)
        {
            Children.RemoveAt(Children.Count - 1);
        }
        for (var i = 0; i < rows.Count; i++)
        {
            if (i >= Children.Count)
            {
                Children.Add(new TextBlock { FontSize = 1 });
            }
            var text = (TextBlock)Children[i];
            text.Text = rows[i];
            AutomationProperties.SetName(text, rows[i]);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children)
        {
            child.Measure(new Size(1, 1));
        }
        return default;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            child.Arrange(new Rect(0, 0, 0, 0));
        }
        return finalSize;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new SemanticZoomTableAutomationPeer(this);
}

internal sealed class SemanticZoomTableAutomationPeer : ControlAutomationPeer
{
    public SemanticZoomTableAutomationPeer(Control owner)
        : base(owner)
    {
    }

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Table;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;

    protected override bool IsOffscreenCore() => false;
}

/// <summary>A named group (breadcrumbs, details panel) for screen readers.</summary>
internal sealed class SemanticZoomGroupAutomationPeer : ControlAutomationPeer
{
    public SemanticZoomGroupAutomationPeer(Control owner)
        : base(owner)
    {
    }

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;
}
