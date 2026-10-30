using System.Globalization;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// A site's mark: the source's own glyph when it has one (a 24 × 24 stroked geometry), otherwise a disc with the first
/// letter of the site. In the stack the disc gets a ring of the card's surface so overlapping marks stay apart.
/// </summary>
internal sealed class SourceCardsMark : Control
{
    /// <summary>The source's glyph; null draws the lettered disc.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<SourceCardsMark, Geometry?>(nameof(Icon));

    /// <summary>The letter on the disc.</summary>
    public static readonly StyledProperty<string?> LetterProperty =
        AvaloniaProperty.Register<SourceCardsMark, string?>(nameof(Letter));

    /// <summary>The mark's width and height.</summary>
    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<SourceCardsMark, double>(nameof(Size), 14);

    /// <summary>The disc's fill (theme: SurfaceMuted).</summary>
    public static readonly StyledProperty<IBrush?> DiscBrushProperty =
        AvaloniaProperty.Register<SourceCardsMark, IBrush?>(nameof(DiscBrush));

    /// <summary>The letter's and the glyph's color (theme: TextSecondary).</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<SourceCardsMark, IBrush?>(nameof(Foreground));

    /// <summary>The ring around the disc (the stack: the card's surface); null draws none.</summary>
    public static readonly StyledProperty<IBrush?> RingBrushProperty =
        AvaloniaProperty.Register<SourceCardsMark, IBrush?>(nameof(RingBrush));

    /// <summary>The ring's width.</summary>
    public static readonly StyledProperty<double> RingThicknessProperty =
        AvaloniaProperty.Register<SourceCardsMark, double>(nameof(RingThickness));

    private ArcGlyph? _glyph;
    private FormattedText? _text;
    private Pen? _ring;

    static SourceCardsMark()
    {
        AffectsMeasure<SourceCardsMark>(SizeProperty);
        AffectsRender<SourceCardsMark>(DiscBrushProperty, ForegroundProperty, RingBrushProperty, RingThicknessProperty, LetterProperty, IconProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<SourceCardsMark>(false);
        FocusableProperty.OverrideDefaultValue<SourceCardsMark>(false);
    }

    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    public string? Letter { get => GetValue(LetterProperty); set => SetValue(LetterProperty, value); }

    public double Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    public IBrush? DiscBrush { get => GetValue(DiscBrushProperty); set => SetValue(DiscBrushProperty, value); }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public IBrush? RingBrush { get => GetValue(RingBrushProperty); set => SetValue(RingBrushProperty, value); }

    public double RingThickness { get => GetValue(RingThicknessProperty); set => SetValue(RingThicknessProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IconProperty || change.Property == SizeProperty || change.Property == ForegroundProperty)
        {
            SyncGlyph();
        }
        if (change.Property == LetterProperty || change.Property == SizeProperty || change.Property == ForegroundProperty)
        {
            _text = null;
        }
        if (change.Property == RingBrushProperty || change.Property == RingThicknessProperty)
        {
            _ring = null;
        }
    }

    private void SyncGlyph()
    {
        if (Icon is { } icon)
        {
            if (_glyph is null)
            {
                _glyph = new ArcGlyph { StrokeThickness = 2 };
                VisualChildren.Add(_glyph);
            }
            _glyph.Data = icon;
            _glyph.Size = Size;
            _glyph.Foreground = Foreground;
        }
        else if (_glyph is not null)
        {
            VisualChildren.Remove(_glyph);
            _glyph = null;
        }
        InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _glyph?.Measure(new Size(Size, Size));
        return new Size(Size, Size);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _glyph?.Arrange(new Rect(finalSize));
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        if (Icon is not null)
        {
            return;
        }
        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 0)
        {
            return;
        }
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var radius = size / 2;
        if (RingBrush is { } ringBrush && RingThickness > 0)
        {
            _ring ??= new Pen(ringBrush, RingThickness);
            context.DrawEllipse(null, _ring, center, radius + RingThickness / 2, radius + RingThickness / 2);
        }
        context.DrawEllipse(DiscBrush, null, center, radius, radius);
        if (string.IsNullOrEmpty(Letter) || Foreground is not { } foreground)
        {
            return;
        }
        // Static text drawn once per change: a FormattedText is fine here (see the performance rules).
        _text ??= new FormattedText(Letter, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold), Math.Max(6, size * 0.58), foreground);
        context.DrawText(_text, new Point(center.X - _text.Width / 2, center.Y - _text.Height / 2));
    }
}

/// <summary>
/// One source on screen: the whole card is a button (Arc's link or button) with the site's mark, its domain, the citation
/// number and a two-line title. A highlighted card gains an accent outline and an accent number, never color alone.
/// </summary>
[PseudoClasses(HighlightedPseudoClass)]
internal sealed class SourceCardsCard : Avalonia.Controls.Button
{
    internal const string HighlightedPseudoClass = ":highlighted";

    /// <summary>The page title.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<SourceCardsCard, string?>(nameof(Title));

    /// <summary>The domain under the mark.</summary>
    public static readonly StyledProperty<string?> DomainProperty =
        AvaloniaProperty.Register<SourceCardsCard, string?>(nameof(Domain));

    /// <summary>The citation number as text.</summary>
    public static readonly StyledProperty<string?> NumberTextProperty =
        AvaloniaProperty.Register<SourceCardsCard, string?>(nameof(NumberText));

    /// <summary>The site's glyph, if any.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<SourceCardsCard, Geometry?>(nameof(Icon));

    /// <summary>The letter of the disc without a glyph.</summary>
    public static readonly StyledProperty<string?> LetterProperty =
        AvaloniaProperty.Register<SourceCardsCard, string?>(nameof(Letter));

    /// <summary>Whether an inline citation points at this card now.</summary>
    public static readonly StyledProperty<bool> IsHighlightedProperty =
        AvaloniaProperty.Register<SourceCardsCard, bool>(nameof(IsHighlighted));

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public string? Domain { get => GetValue(DomainProperty); set => SetValue(DomainProperty, value); }

    public string? NumberText { get => GetValue(NumberTextProperty); set => SetValue(NumberTextProperty, value); }

    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    public string? Letter { get => GetValue(LetterProperty); set => SetValue(LetterProperty, value); }

    public bool IsHighlighted { get => GetValue(IsHighlightedProperty); set => SetValue(IsHighlightedProperty, value); }

    /// <summary>The source shown (set by the owner).</summary>
    internal SourceCardsSource? Item { get; set; }

    /// <summary>The 1-based citation number.</summary>
    internal int Number { get; set; }

    protected override Type StyleKeyOverride => typeof(SourceCardsCard);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsHighlightedProperty)
        {
            PseudoClasses.Set(HighlightedPseudoClass, IsHighlighted);
        }
    }
}

/// <summary>
/// The overflow tile: the marks of the hidden sources overlapping, "+4 more" (a rolling count) and their sites. Pressing it
/// opens the stack.
/// </summary>
[TemplatePart(MarksPart, typeof(StackPanel))]
[PseudoClasses(SourceCardsCard.HighlightedPseudoClass)]
internal sealed class SourceCardsStack : Avalonia.Controls.Button
{
    internal const string MarksPart = "PART_Marks";

    /// <summary>"+4 more".</summary>
    public static readonly StyledProperty<string?> MoreTextProperty =
        AvaloniaProperty.Register<SourceCardsStack, string?>(nameof(MoreText));

    /// <summary>The hidden sources' sites.</summary>
    public static readonly StyledProperty<string?> SitesProperty =
        AvaloniaProperty.Register<SourceCardsStack, string?>(nameof(Sites));

    /// <summary>The size of each mark.</summary>
    public static readonly StyledProperty<double> MarkSizeProperty =
        AvaloniaProperty.Register<SourceCardsStack, double>(nameof(MarkSize), 18);

    /// <summary>How far each mark overlaps the one before it.</summary>
    public static readonly StyledProperty<double> MarkOverlapProperty =
        AvaloniaProperty.Register<SourceCardsStack, double>(nameof(MarkOverlap), 6);

    /// <summary>Whether a citation points at a hidden source.</summary>
    public static readonly StyledProperty<bool> IsHighlightedProperty =
        SourceCardsCard.IsHighlightedProperty.AddOwner<SourceCardsStack>();

    /// <summary>The most marks drawn.</summary>
    internal const int MaxMarks = 4;

    private readonly List<(Geometry? Icon, string Letter)> _marks = [];
    private StackPanel? _panel;

    public string? MoreText { get => GetValue(MoreTextProperty); set => SetValue(MoreTextProperty, value); }

    public string? Sites { get => GetValue(SitesProperty); set => SetValue(SitesProperty, value); }

    public double MarkSize { get => GetValue(MarkSizeProperty); set => SetValue(MarkSizeProperty, value); }

    public double MarkOverlap { get => GetValue(MarkOverlapProperty); set => SetValue(MarkOverlapProperty, value); }

    public bool IsHighlighted { get => GetValue(IsHighlightedProperty); set => SetValue(IsHighlightedProperty, value); }

    /// <summary>The marks drawn now (tests read it).</summary>
    internal int MarkCount => _marks.Count;

    protected override Type StyleKeyOverride => typeof(SourceCardsStack);

    /// <summary>Shows the marks of the hidden sources (the first <see cref="MaxMarks"/>).</summary>
    internal void SetMarks(IEnumerable<(Geometry? Icon, string Letter)> marks)
    {
        var next = marks.Take(MaxMarks).ToList();
        if (next.SequenceEqual(_marks))
        {
            return;
        }
        _marks.Clear();
        _marks.AddRange(next);
        BuildMarks();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _panel = e.NameScope.Find<StackPanel>(MarksPart);
        BuildMarks();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsHighlightedProperty)
        {
            PseudoClasses.Set(SourceCardsCard.HighlightedPseudoClass, IsHighlighted);
        }
        else if (change.Property == MarkSizeProperty || change.Property == MarkOverlapProperty)
        {
            BuildMarks();
        }
    }

    private void BuildMarks()
    {
        if (_panel is null)
        {
            return;
        }
        _panel.Children.Clear();
        for (var i = 0; i < _marks.Count; i++)
        {
            var (icon, letter) = _marks[i];
            _panel.Children.Add(new SourceCardsMark
            {
                Classes = { "stacked" },
                Icon = icon,
                Letter = letter,
                Size = MarkSize,
                Margin = new Thickness(i == 0 ? 0 : -MarkOverlap, 0, 0, 0),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            });
        }
    }
}

/// <summary>
/// A reserved slot: the card's box with bars on the same 18px lines as the text that replaces them (a disc and a short
/// bar on the domain line, two bars on the title lines). The bars pulse at full motion (1.6s, between full and 50%
/// strength), in step with every other slot; reduced motion and off keep them still. Hidden from assistive technology.
/// Drawn in code: one control per slot, no template, and the pulse only invalidates its own visual.
/// </summary>
internal sealed class SourceCardsSkeleton : Control
{
    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        Border.BackgroundProperty.AddOwner<SourceCardsSkeleton>();

    public static readonly StyledProperty<IBrush?> BorderBrushProperty =
        Border.BorderBrushProperty.AddOwner<SourceCardsSkeleton>();

    /// <summary>The bars' color.</summary>
    public static readonly StyledProperty<IBrush?> BarBrushProperty =
        AvaloniaProperty.Register<SourceCardsSkeleton, IBrush?>(nameof(BarBrush));

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        Border.CornerRadiusProperty.AddOwner<SourceCardsSkeleton>();

    public static readonly StyledProperty<Thickness> PaddingProperty =
        Decorator.PaddingProperty.AddOwner<SourceCardsSkeleton>();

    /// <summary>The text line the bars sit on.</summary>
    public static readonly StyledProperty<double> LineHeightProperty =
        AvaloniaProperty.Register<SourceCardsSkeleton, double>(nameof(LineHeight), 18);

    /// <summary>The domain line's mark size and gap.</summary>
    public static readonly StyledProperty<double> MarkSizeProperty =
        AvaloniaProperty.Register<SourceCardsSkeleton, double>(nameof(MarkSize), 14);

    public static readonly StyledProperty<double> MarkGapProperty =
        AvaloniaProperty.Register<SourceCardsSkeleton, double>(nameof(MarkGap), 6);

    /// <summary>The space between the domain line and the title.</summary>
    public static readonly StyledProperty<double> TitleTopProperty =
        AvaloniaProperty.Register<SourceCardsSkeleton, double>(nameof(TitleTop), 4);

    /// <summary>The bar heights on the domain line and the title lines.</summary>
    public static readonly StyledProperty<double> DomainBarProperty =
        AvaloniaProperty.Register<SourceCardsSkeleton, double>(nameof(DomainBar), 8);

    public static readonly StyledProperty<double> TitleBarProperty =
        AvaloniaProperty.Register<SourceCardsSkeleton, double>(nameof(TitleBar), 10);

    /// <summary>One breath of the pulse.</summary>
    internal const double PulseSeconds = 1.6;

    private static readonly System.Diagnostics.Stopwatch s_clock = System.Diagnostics.Stopwatch.StartNew();

    private readonly Motion.LiveLoop _pulse;
    private Pen? _border;

    static SourceCardsSkeleton()
    {
        AffectsRender<SourceCardsSkeleton>(BackgroundProperty, BorderBrushProperty, BarBrushProperty, CornerRadiusProperty,
            PaddingProperty, LineHeightProperty, MarkSizeProperty, MarkGapProperty, TitleTopProperty, DomainBarProperty, TitleBarProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<SourceCardsSkeleton>(false);
        FocusableProperty.OverrideDefaultValue<SourceCardsSkeleton>(false);
    }

    public SourceCardsSkeleton()
    {
        Avalonia.Automation.AutomationProperties.SetAccessibilityView(this, Avalonia.Automation.AccessibilityView.Raw);
        _pulse = new Motion.LiveLoop(this, _ =>
        {
            InvalidateVisual();
            return true;
        });
        _pulse.RunningChanged += _ => InvalidateVisual();
        _pulse.Start();
    }

    public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }

    public IBrush? BorderBrush { get => GetValue(BorderBrushProperty); set => SetValue(BorderBrushProperty, value); }

    public IBrush? BarBrush { get => GetValue(BarBrushProperty); set => SetValue(BarBrushProperty, value); }

    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    public Thickness Padding { get => GetValue(PaddingProperty); set => SetValue(PaddingProperty, value); }

    public double LineHeight { get => GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }

    public double MarkSize { get => GetValue(MarkSizeProperty); set => SetValue(MarkSizeProperty, value); }

    public double MarkGap { get => GetValue(MarkGapProperty); set => SetValue(MarkGapProperty, value); }

    public double TitleTop { get => GetValue(TitleTopProperty); set => SetValue(TitleTopProperty, value); }

    public double DomainBar { get => GetValue(DomainBarProperty); set => SetValue(DomainBarProperty, value); }

    public double TitleBar { get => GetValue(TitleBarProperty); set => SetValue(TitleBarProperty, value); }

    /// <summary>Whether the bars pulse now.</summary>
    internal bool IsPulsing => _pulse.IsRunning;

    /// <summary>The bars' strength now: 1 at rest, breathing down to 0.5 while the pulse runs.</summary>
    internal double Strength => _pulse.IsRunning
        ? 0.75 + 0.25 * Math.Cos(2 * Math.PI * (s_clock.Elapsed.TotalSeconds % PulseSeconds) / PulseSeconds)
        : 1;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BorderBrushProperty)
        {
            _border = null;
        }
    }

    protected override Size MeasureOverride(Size availableSize) => default;

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }
        var radius = CornerRadius.TopLeft;
        if (BorderBrush is { } borderBrush)
        {
            _border ??= new Pen(borderBrush, 1);
        }
        context.DrawRectangle(Background, BorderBrush is null ? null : _border, bounds.Deflate(0.5), radius, radius);
        if (BarBrush is not { } bar)
        {
            return;
        }
        var inner = bounds.Deflate(Padding);
        var line = LineHeight;
        using (context.PushOpacity(Strength))
        {
            // The domain line: a disc where the mark goes, a bar about a third of the card where the domain goes.
            var mark = MarkSize;
            context.DrawEllipse(bar, null, new Point(inner.X + mark / 2, inner.Y + line / 2), mark / 2, mark / 2);
            var domainX = inner.X + mark + MarkGap;
            DrawBar(context, bar, domainX, inner.Y + (line - DomainBar) / 2, Math.Max(0, inner.Width * 0.42 - mark), DomainBar);
            // The title's two lines: a full bar, then a shorter one.
            var top = inner.Y + line + TitleTop;
            DrawBar(context, bar, inner.X, top + (line - TitleBar) / 2, inner.Width * 0.92, TitleBar);
            DrawBar(context, bar, inner.X, top + line + (line - TitleBar) / 2, inner.Width * 0.6, TitleBar);
        }
    }

    private static void DrawBar(DrawingContext context, IBrush brush, double x, double y, double width, double height)
    {
        if (width <= 0)
        {
            return;
        }
        context.DrawRectangle(brush, null, new Rect(x, y, width, height), height / 2, height / 2);
    }
}

/// <summary>
/// A small text button of the status line (Retry, Search again, Show all). As the Show all toggle it reports its expanded
/// state (Arc's <c>aria-expanded</c>); otherwise it is a plain button.
/// </summary>
internal sealed class SourceCardsActionButton : ToolCallDisclosureButton
{
    /// <summary>Whether the button reports an expanded state (the Show all toggle).</summary>
    public bool IsToggle { get; set; }

    protected override Type StyleKeyOverride => typeof(SourceCardsActionButton);

    protected override AutomationPeer OnCreateAutomationPeer() =>
        IsToggle ? base.OnCreateAutomationPeer() : new ButtonAutomationPeer(this);
}

/// <summary>
/// The hover preview of a card: the site's mark and name with the date, the title, the passage the answer relied on (four
/// lines) and the domain with an external-link glyph. It lives in an <see cref="Primitives.ArcPopup"/> anchored to the card.
/// </summary>
internal sealed class SourceCardsPreview : TemplatedControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<SourceCardsPreview, string?>(nameof(Title));

    public static readonly StyledProperty<string?> SiteProperty =
        AvaloniaProperty.Register<SourceCardsPreview, string?>(nameof(Site));

    public static readonly StyledProperty<string?> PublishedProperty =
        AvaloniaProperty.Register<SourceCardsPreview, string?>(nameof(Published));

    public static readonly StyledProperty<string?> SnippetProperty =
        AvaloniaProperty.Register<SourceCardsPreview, string?>(nameof(Snippet));

    public static readonly StyledProperty<string?> DomainProperty =
        AvaloniaProperty.Register<SourceCardsPreview, string?>(nameof(Domain));

    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<SourceCardsPreview, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<string?> LetterProperty =
        AvaloniaProperty.Register<SourceCardsPreview, string?>(nameof(Letter));

    /// <summary>Whether there is a date to show.</summary>
    public static readonly StyledProperty<bool> HasPublishedProperty =
        AvaloniaProperty.Register<SourceCardsPreview, bool>(nameof(HasPublished));

    /// <summary>Whether there is a passage to show.</summary>
    public static readonly StyledProperty<bool> HasSnippetProperty =
        AvaloniaProperty.Register<SourceCardsPreview, bool>(nameof(HasSnippet));

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public string? Site { get => GetValue(SiteProperty); set => SetValue(SiteProperty, value); }

    public string? Published { get => GetValue(PublishedProperty); set => SetValue(PublishedProperty, value); }

    public string? Snippet { get => GetValue(SnippetProperty); set => SetValue(SnippetProperty, value); }

    public string? Domain { get => GetValue(DomainProperty); set => SetValue(DomainProperty, value); }

    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    public string? Letter { get => GetValue(LetterProperty); set => SetValue(LetterProperty, value); }

    public bool HasPublished { get => GetValue(HasPublishedProperty); set => SetValue(HasPublishedProperty, value); }

    public bool HasSnippet { get => GetValue(HasSnippetProperty); set => SetValue(HasSnippetProperty, value); }

    /// <summary>Shows <paramref name="source"/>.</summary>
    internal void Show(SourceCardsSource source, string domain, string letter)
    {
        Title = source.Title;
        Site = string.IsNullOrWhiteSpace(source.Site) ? domain : source.Site;
        Published = source.Published;
        HasPublished = !string.IsNullOrWhiteSpace(source.Published);
        Snippet = source.Snippet;
        HasSnippet = !string.IsNullOrWhiteSpace(source.Snippet);
        Domain = domain;
        Icon = source.Icon;
        Letter = letter;
    }
}
