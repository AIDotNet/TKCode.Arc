using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>How the <see cref="SearchResultsCursor"/> travels to a new row.</summary>
internal enum SearchResultsCursorMove
{
    /// <summary>Straight there (layout changes).</summary>
    Snap,

    /// <summary>The pointer moved: the snappy spring, which keeps its speed when retargeted.</summary>
    Pointer,

    /// <summary>An arrow key: a 70ms tween on the enter curve, so held keys feel immediate.</summary>
    Key
}

/// <summary>
/// The shared keyboard cursor of a <see cref="SearchResults"/>: one rounded block behind the active row, drawn in
/// <see cref="Render"/> so the glide never relayouts. Pointer moves ride the snappy spring, arrow keys a 70ms enter
/// tween; the first appearance lands in place and fades in over 80ms, hiding fades out over 100ms.
/// </summary>
/// <remarks>
/// Code-driven (<see cref="MotionValue{T}"/>): every move picks its own curve (spring for the pointer, tween for keys)
/// and a retargeted spring keeps its velocity, which a ControlTheme transition cannot express.
/// </remarks>
internal sealed class SearchResultsCursor : Control
{
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<SearchResultsCursor, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<IBrush?> RingBrushProperty =
        AvaloniaProperty.Register<SearchResultsCursor, IBrush?>(nameof(RingBrush));

    public static readonly StyledProperty<double> RingThicknessProperty =
        AvaloniaProperty.Register<SearchResultsCursor, double>(nameof(RingThickness), 2);

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<SearchResultsCursor, CornerRadius>(nameof(CornerRadius));

    public static readonly StyledProperty<bool> IsRingVisibleProperty =
        AvaloniaProperty.Register<SearchResultsCursor, bool>(nameof(IsRingVisible));

    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }
    public IBrush? RingBrush { get => GetValue(RingBrushProperty); set => SetValue(RingBrushProperty, value); }
    public double RingThickness { get => GetValue(RingThicknessProperty); set => SetValue(RingThicknessProperty, value); }
    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }
    public bool IsRingVisible { get => GetValue(IsRingVisibleProperty); set => SetValue(IsRingVisibleProperty, value); }

    private static readonly TimeSpan AppearDuration = TimeSpan.FromMilliseconds(80);
    private static readonly TimeSpan HideDuration = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan KeyDuration = TimeSpan.FromMilliseconds(70);

    private MotionValue<Rect>? _rect;
    private MotionValue<double>? _opacity;
    private bool _shown;
    private Pen? _pen;

    static SearchResultsCursor()
    {
        AffectsRender<SearchResultsCursor>(FillProperty, RingBrushProperty, RingThicknessProperty, CornerRadiusProperty, IsRingVisibleProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<SearchResultsCursor>(false);
    }

    /// <summary>Whether the cursor is shown (or fading in).</summary>
    public bool IsShown => _shown;

    /// <summary>The rectangle the cursor is heading to.</summary>
    public Rect TargetRect => _rect?.Target ?? default;

    /// <summary>Current opacity (tests).</summary>
    internal double CurrentOpacity => _opacity?.Value ?? 0;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RingBrushProperty || change.Property == RingThicknessProperty)
        {
            _pen = null;
        }
    }

    public void MoveTo(Rect target, SearchResultsCursorMove move)
    {
        _rect ??= CreateRect(target);
        _opacity ??= CreateOpacity();
        var level = IsLoaded ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;
        if (!_shown)
        {
            // The first appearance lands in place and only fades in.
            _shown = true;
            _rect.Jump(target);
            if (level == ArcMotionLevel.Off || move == SearchResultsCursorMove.Snap)
            {
                _opacity.Jump(1);
            }
            else
            {
                _opacity.Jump(0);
                _ = _opacity.AnimateTo(1, MotionSpec.Tween(AppearDuration, ArcEasings.Standard));
            }
            return;
        }
        if (_opacity.Target < 1)
        {
            if (level == ArcMotionLevel.Off)
            {
                _opacity.Jump(1);
            }
            else
            {
                _ = _opacity.AnimateTo(1, MotionSpec.Tween(AppearDuration, ArcEasings.Standard));
            }
        }
        if (level != ArcMotionLevel.Full || move == SearchResultsCursorMove.Snap)
        {
            _rect.Jump(target);
            return;
        }
        _ = _rect.AnimateTo(target, move == SearchResultsCursorMove.Pointer
            ? MotionSpec.Spring(ArcSpring.Snappy)
            : MotionSpec.Tween(KeyDuration, ArcEasings.Enter));
    }

    public void Hide(bool animate = true)
    {
        if (!_shown || _opacity is null)
        {
            return;
        }
        _shown = false;
        if (animate && IsLoaded && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off)
        {
            _ = _opacity.AnimateTo(0, MotionSpec.Tween(HideDuration, ArcEasings.Standard));
        }
        else
        {
            _opacity.Jump(0);
        }
    }

    private MotionValue<Rect> CreateRect(Rect initial)
    {
        var value = new MotionValue<Rect>(this, initial, MotionVectors.Rect);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    private MotionValue<double> CreateOpacity()
    {
        var value = new MotionValue<double>(this, 0, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    public override void Render(DrawingContext context)
    {
        if (_rect is null || _opacity is null || _opacity.Value <= 0.001)
        {
            return;
        }
        var rect = _rect.Value;
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }
        var radius = CornerRadius;
        using (context.PushOpacity(Math.Clamp(_opacity.Value, 0, 1)))
        {
            context.DrawRectangle(Fill, null, new RoundedRect(rect, radius.TopLeft, radius.TopRight, radius.BottomRight, radius.BottomLeft));
            if (IsRingVisible && RingBrush is { } ring && RingThickness > 0)
            {
                _pen ??= new Pen(ring, RingThickness);
                var half = RingThickness / 2;
                var inner = rect.Deflate(half);
                context.DrawRectangle(null, _pen, new RoundedRect(inner, Math.Max(0, radius.TopLeft - half), Math.Max(0, radius.TopRight - half),
                    Math.Max(0, radius.BottomRight - half), Math.Max(0, radius.BottomLeft - half)));
            }
        }
    }
}

/// <summary>
/// Text with highlighted matches: a handful of pooled <see cref="Run"/>s (plain and marked, so no text style
/// overrides), and a rounded mark drawn behind every match. The mark rectangles are measured once per text layout.
/// </summary>
internal sealed class SearchResultsMarkedText : TextBlock
{
    public static readonly StyledProperty<IBrush?> MarkBackgroundProperty =
        AvaloniaProperty.Register<SearchResultsMarkedText, IBrush?>(nameof(MarkBackground));

    public static readonly StyledProperty<IBrush?> MarkForegroundProperty =
        AvaloniaProperty.Register<SearchResultsMarkedText, IBrush?>(nameof(MarkForeground));

    public static readonly StyledProperty<FontWeight> MarkFontWeightProperty =
        AvaloniaProperty.Register<SearchResultsMarkedText, FontWeight>(nameof(MarkFontWeight), FontWeight.Medium);

    public static readonly StyledProperty<double> MarkRadiusProperty =
        AvaloniaProperty.Register<SearchResultsMarkedText, double>(nameof(MarkRadius), 4);

    public static readonly StyledProperty<double> MarkInflateProperty =
        AvaloniaProperty.Register<SearchResultsMarkedText, double>(nameof(MarkInflate), 2);

    public IBrush? MarkBackground { get => GetValue(MarkBackgroundProperty); set => SetValue(MarkBackgroundProperty, value); }
    public IBrush? MarkForeground { get => GetValue(MarkForegroundProperty); set => SetValue(MarkForegroundProperty, value); }
    public FontWeight MarkFontWeight { get => GetValue(MarkFontWeightProperty); set => SetValue(MarkFontWeightProperty, value); }
    public double MarkRadius { get => GetValue(MarkRadiusProperty); set => SetValue(MarkRadiusProperty, value); }
    public double MarkInflate { get => GetValue(MarkInflateProperty); set => SetValue(MarkInflateProperty, value); }

    private readonly List<SearchMatchRange> _ranges = new();
    private readonly List<Run> _runs = new();
    private readonly List<Rect> _markRects = new();
    private TextLayout? _measuredLayout;
    private string _plain = string.Empty;

    static SearchResultsMarkedText()
    {
        AffectsRender<SearchResultsMarkedText>(MarkBackgroundProperty, MarkRadiusProperty, MarkInflateProperty);
    }

    protected override Type StyleKeyOverride => typeof(TextBlock);

    /// <summary>The text without marks.</summary>
    public string PlainText => _plain;

    /// <summary>The marked ranges of <see cref="PlainText"/>.</summary>
    public IReadOnlyList<SearchMatchRange> Ranges => _ranges;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MarkForegroundProperty || change.Property == MarkFontWeightProperty)
        {
            Rebuild();
        }
    }

    public void SetContent(string? text, IReadOnlyList<SearchMatchRange>? ranges)
    {
        text ??= string.Empty;
        var count = ranges?.Count ?? 0;
        if (string.Equals(text, _plain, StringComparison.Ordinal) && count == _ranges.Count && Inlines is { Count: > 0 })
        {
            var same = true;
            for (var i = 0; i < count && same; i++)
            {
                same = ranges![i] == _ranges[i];
            }
            if (same)
            {
                return;
            }
        }
        _plain = text;
        _ranges.Clear();
        for (var i = 0; i < count; i++)
        {
            var range = ranges![i];
            if (range.Start >= 0 && range.Length > 0 && range.End <= text.Length)
            {
                _ranges.Add(range);
            }
        }
        Rebuild();
    }

    private void Rebuild()
    {
        var inlines = Inlines ??= new InlineCollection();
        inlines.Clear();
        _measuredLayout = null;
        var used = 0;
        var position = 0;
        foreach (var range in _ranges)
        {
            if (range.Start > position)
            {
                inlines.Add(RentRun(used++, _plain.Substring(position, range.Start - position), marked: false));
            }
            inlines.Add(RentRun(used++, _plain.Substring(range.Start, range.Length), marked: true));
            position = range.End;
        }
        if (position < _plain.Length || used == 0)
        {
            inlines.Add(RentRun(used, _plain.Substring(position), marked: false));
        }
        InvalidateVisual();
    }

    private Run RentRun(int index, string text, bool marked)
    {
        if (index >= _runs.Count)
        {
            _runs.Add(new Run());
        }
        var run = _runs[index];
        run.Text = text;
        if (marked)
        {
            run.FontWeight = MarkFontWeight;
            if (MarkForeground is { } foreground)
            {
                run.Foreground = foreground;
            }
            else
            {
                run.ClearValue(TextElement.ForegroundProperty);
            }
        }
        else
        {
            run.ClearValue(TextElement.FontWeightProperty);
            run.ClearValue(TextElement.ForegroundProperty);
        }
        return run;
    }

    protected override void RenderTextLayout(DrawingContext context, Point origin)
    {
        if (_ranges.Count > 0 && MarkBackground is { } brush)
        {
            var layout = TextLayout;
            if (!ReferenceEquals(layout, _measuredLayout))
            {
                MeasureMarks(layout);
            }
            var radius = MarkRadius;
            foreach (var rect in _markRects)
            {
                context.DrawRectangle(brush, null, new RoundedRect(rect.Translate(origin), radius));
            }
        }
        base.RenderTextLayout(context, origin);
    }

    private void MeasureMarks(TextLayout layout)
    {
        _measuredLayout = layout;
        _markRects.Clear();
        var inflate = MarkInflate;
        var limit = layout.WidthIncludingTrailingWhitespace + inflate;
        // Hug the glyphs rather than the full line box.
        var glyphHeight = FontSize * 1.32;
        foreach (var range in _ranges)
        {
            foreach (var rect in layout.HitTestTextRange(range.Start, range.Length))
            {
                if (rect.Width < 0.5)
                {
                    continue;
                }
                var inset = Math.Max(0, (rect.Height - glyphHeight) / 2);
                var left = Math.Max(-inflate, rect.Left - inflate);
                var right = Math.Min(limit, rect.Right + inflate);
                if (right - left < 1)
                {
                    continue;
                }
                _markRects.Add(new Rect(left, rect.Top + inset, right - left, Math.Max(1, rect.Height - inset * 2)));
            }
        }
    }

    /// <summary>The mark rectangles in text coordinates (tests).</summary>
    internal IReadOnlyList<Rect> MarkRects
    {
        get
        {
            if (!ReferenceEquals(TextLayout, _measuredLayout))
            {
                MeasureMarks(TextLayout);
            }
            return _markRects;
        }
    }
}

/// <summary>The focusable result list: one tab stop whose active descendant is the cursor row (a listbox).</summary>
internal sealed class SearchResultsListHost : Panel
{
    static SearchResultsListHost()
    {
        FocusableProperty.OverrideDefaultValue<SearchResultsListHost>(true);
        FocusAdornerProperty.OverrideDefaultValue<SearchResultsListHost>(null);
        AutomationProperties.ControlTypeOverrideProperty.OverrideDefaultValue<SearchResultsListHost>(AutomationControlType.List);
    }

    /// <summary>True while focus arrived from the keyboard (draws the cursor's focus ring).</summary>
    public bool HasKeyboardFocus { get; private set; }

    /// <summary>Raised when <see cref="HasKeyboardFocus"/> changes.</summary>
    public event EventHandler? KeyboardFocusChanged;

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        SetKeyboardFocus(e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional);
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        SetKeyboardFocus(false);
    }

    private void SetKeyboardFocus(bool value)
    {
        if (HasKeyboardFocus != value)
        {
            HasKeyboardFocus = value;
            KeyboardFocusChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}

/// <summary>The facet row: a tab list (one tab stop, arrows move between the facets).</summary>
internal sealed class SearchResultsFacetPanel : StackPanel
{
    static SearchResultsFacetPanel()
    {
        AutomationProperties.ControlTypeOverrideProperty.OverrideDefaultValue<SearchResultsFacetPanel>(AutomationControlType.Tab);
        KeyboardNavigation.TabNavigationProperty.OverrideDefaultValue<SearchResultsFacetPanel>(KeyboardNavigationMode.Once);
    }
}

/// <summary>One facet tab: its label and a rolling count of the results it holds for the current query.</summary>
[PseudoClasses(SelectedPseudoClass, DimmedPseudoClass)]
internal sealed class SearchResultsFacet : TemplatedControl
{
    public const string SelectedPseudoClass = ":selected";
    public const string DimmedPseudoClass = ":dimmed";

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<SearchResultsFacet, string?>(nameof(Label));

    public static readonly StyledProperty<double> CountProperty =
        AvaloniaProperty.Register<SearchResultsFacet, double>(nameof(Count));

    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<SearchResultsFacet, bool>(nameof(IsSelected));

    public static readonly StyledProperty<bool> IsDimmedProperty =
        AvaloniaProperty.Register<SearchResultsFacet, bool>(nameof(IsDimmed));

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public double Count { get => GetValue(CountProperty); set => SetValue(CountProperty, value); }
    public bool IsSelected { get => GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }
    public bool IsDimmed { get => GetValue(IsDimmedProperty); set => SetValue(IsDimmedProperty, value); }

    /// <summary>The <see cref="SearchResults.SelectedKind"/> value this facet stands for (null for All).</summary>
    public string? Key { get; set; }

    /// <summary>Position in the facet row.</summary>
    public int Index { get; set; }

    /// <summary>Raised when the facet is pressed.</summary>
    public event EventHandler? Activated;

    static SearchResultsFacet()
    {
        FocusableProperty.OverrideDefaultValue<SearchResultsFacet>(true);
    }

    public SearchResultsFacet()
    {
        // Only the selected facet is a tab stop (roving tab index).
        IsTabStop = false;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsSelectedProperty)
        {
            PseudoClasses.Set(SelectedPseudoClass, IsSelected);
            IsTabStop = IsSelected;
        }
        else if (change.Property == IsDimmedProperty)
        {
            PseudoClasses.Set(DimmedPseudoClass, IsDimmed);
        }
        if (change.Property == LabelProperty || change.Property == CountProperty)
        {
            AutomationProperties.SetName(this, $"{Label}, {Count:0}");
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            e.Handled = true;
            Focus(NavigationMethod.Pointer);
            Activated?.Invoke(this, EventArgs.Empty);
        }
    }

    internal void Activate() => Activated?.Invoke(this, EventArgs.Empty);

    protected override AutomationPeer OnCreateAutomationPeer() => new FacetAutomationPeer(this);

    private sealed class FacetAutomationPeer : ControlAutomationPeer, ISelectionItemProvider
    {
        public FacetAutomationPeer(SearchResultsFacet owner) : base(owner)
        {
        }

        private SearchResultsFacet Facet => (SearchResultsFacet)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.TabItem;

        public bool IsSelected => Facet.IsSelected;

        public ISelectionProvider? SelectionContainer => null;

        public void AddToSelection() => Facet.Activate();

        public void RemoveFromSelection()
        {
        }

        public void Select() => Facet.Activate();
    }
}

/// <summary>The heading of a kind's group in the All facet: the kind and its result count.</summary>
internal sealed class SearchResultGroupHeader : TemplatedControl
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<SearchResultGroupHeader, string?>(nameof(Label));

    public static readonly StyledProperty<string?> CountTextProperty =
        AvaloniaProperty.Register<SearchResultGroupHeader, string?>(nameof(CountText));

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string? CountText { get => GetValue(CountTextProperty); set => SetValue(CountTextProperty, value); }

    /// <summary>The facet key of the group.</summary>
    public string? Key { get; set; }

    static SearchResultGroupHeader()
    {
        FocusableProperty.OverrideDefaultValue<SearchResultGroupHeader>(false);
        AutomationProperties.ControlTypeOverrideProperty.OverrideDefaultValue<SearchResultGroupHeader>(AutomationControlType.HeaderItem);
    }

    public SearchResultGroupHeader()
    {
        AutomationProperties.SetHeadingLevel(this, 3);
        RenderTransform = new TranslateTransform();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelProperty || change.Property == CountTextProperty)
        {
            AutomationProperties.SetName(this, $"{Label}, {CountText}");
        }
    }
}

/// <summary>
/// One result: an icon block, the title with its matches marked, the location, the snippet window and the meta
/// text. Pooled by <see cref="SearchResults"/>: <see cref="SetData"/> swaps in a new item without re-templating.
/// </summary>
[TemplatePart(TitlePart, typeof(SearchResultsMarkedText))]
[TemplatePart(SnippetPart, typeof(SearchResultsMarkedText))]
[PseudoClasses(ActivePseudoClass, CompactPseudoClass, HasSnippetPseudoClass, HasLocationPseudoClass, HasMetaPseudoClass)]
internal sealed class SearchResultRow : TemplatedControl
{
    public const string TitlePart = "PART_Title";
    public const string SnippetPart = "PART_Snippet";
    public const string ActivePseudoClass = ":active";
    public const string CompactPseudoClass = ":compact";
    public const string HasSnippetPseudoClass = ":has-snippet";
    public const string HasLocationPseudoClass = ":has-location";
    public const string HasMetaPseudoClass = ":has-meta";

    public static readonly DirectProperty<SearchResultRow, object?> IconContentProperty =
        AvaloniaProperty.RegisterDirect<SearchResultRow, object?>(nameof(IconContent), o => o.IconContent);

    public static readonly DirectProperty<SearchResultRow, string?> LocationTextProperty =
        AvaloniaProperty.RegisterDirect<SearchResultRow, string?>(nameof(LocationText), o => o.LocationText);

    public static readonly DirectProperty<SearchResultRow, string?> MetaTextProperty =
        AvaloniaProperty.RegisterDirect<SearchResultRow, string?>(nameof(MetaText), o => o.MetaText);

    private object? _iconContent;
    private string? _locationText;
    private string? _metaText;
    private readonly ArcGlyph _glyph = new() { Size = 16, StrokeThickness = 1.75 };
    private readonly List<SearchMatchRange> _titleRanges = new();
    private readonly List<SearchMatchRange> _snippetRanges = new();
    private string _title = string.Empty;
    private string? _snippet;
    private string? _location;
    private string? _meta;
    private string? _kindLabel;
    private bool _compact;
    private SearchResultsMarkedText? _titleText;
    private SearchResultsMarkedText? _snippetText;

    static SearchResultRow()
    {
        FocusableProperty.OverrideDefaultValue<SearchResultRow>(false);
    }

    public SearchResultRow()
    {
        RenderTransform = new TranslateTransform();
    }

    public object? IconContent { get => _iconContent; private set => SetAndRaise(IconContentProperty, ref _iconContent, value); }
    public string? LocationText { get => _locationText; private set => SetAndRaise(LocationTextProperty, ref _locationText, value); }
    public string? MetaText { get => _metaText; private set => SetAndRaise(MetaTextProperty, ref _metaText, value); }

    /// <summary>The shown item (null while pooled).</summary>
    public SearchResultItem? Item { get; private set; }

    /// <summary>Position among the shown rows (the cursor's index).</summary>
    public int Index { get; set; }

    /// <summary>Whether the cursor is on this row.</summary>
    public bool IsActive { get; private set; }

    internal SearchResultsMarkedText? TitleText => _titleText;
    internal SearchResultsMarkedText? SnippetText => _snippetText;

    public void SetData(SearchResultItem item, string? kindLabel, List<SearchMatchRange> titleRanges, string? snippet,
                        List<SearchMatchRange> snippetRanges)
    {
        Item = item;
        _kindLabel = kindLabel;
        _title = item.Title;
        _titleRanges.Clear();
        _titleRanges.AddRange(titleRanges);
        _snippet = string.IsNullOrEmpty(snippet) ? null : snippet;
        _snippetRanges.Clear();
        if (_snippet is not null)
        {
            _snippetRanges.AddRange(snippetRanges);
        }
        _location = string.IsNullOrEmpty(item.Location) ? null : item.Location;
        _meta = string.IsNullOrEmpty(item.Meta) ? null : item.Meta;
        IsEnabled = item.IsEnabled;
        IconContent = item.Icon switch
        {
            Geometry geometry => Glyph(geometry),
            Control control => control,
            _ => Glyph(ArcIcons.FileText)
        };
        Push();
    }

    private ArcGlyph Glyph(Geometry data)
    {
        _glyph.Data = data;
        return _glyph;
    }

    /// <summary>Lets go of the item (and of a control icon, which may be shown by another row next).</summary>
    public void Release()
    {
        Item = null;
        IconContent = null;
        SetActive(false);
        Opacity = 1;
        if (RenderTransform is TranslateTransform lift)
        {
            lift.Y = 0;
        }
    }

    public void SetActive(bool active)
    {
        if (IsActive != active)
        {
            IsActive = active;
            PseudoClasses.Set(ActivePseudoClass, active);
        }
    }

    public void SetCompact(bool compact)
    {
        if (_compact != compact)
        {
            _compact = compact;
            PseudoClasses.Set(CompactPseudoClass, compact);
            Push();
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _titleText = e.NameScope.Find<SearchResultsMarkedText>(TitlePart);
        _snippetText = e.NameScope.Find<SearchResultsMarkedText>(SnippetPart);
        Push();
    }

    private void Push()
    {
        // Narrow rows fold the meta text into the location line.
        var location = _compact && _meta is not null
            ? (_location is null ? _meta : $"{_location} · {_meta}")
            : _location;
        LocationText = location;
        MetaText = _meta;
        PseudoClasses.Set(HasSnippetPseudoClass, _snippet is not null);
        PseudoClasses.Set(HasLocationPseudoClass, location is not null);
        PseudoClasses.Set(HasMetaPseudoClass, _meta is not null);
        _titleText?.SetContent(_title, _titleRanges);
        _snippetText?.SetContent(_snippet, _snippetRanges);
        var name = _title;
        if (!string.IsNullOrEmpty(_kindLabel))
        {
            name += ", " + _kindLabel;
        }
        if (_location is not null)
        {
            name += ", " + _location;
        }
        AutomationProperties.SetName(this, name);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new RowAutomationPeer(this);

    private sealed class RowAutomationPeer : ControlAutomationPeer, ISelectionItemProvider
    {
        public RowAutomationPeer(SearchResultRow owner) : base(owner)
        {
        }

        private SearchResultRow Row => (SearchResultRow)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        public bool IsSelected => Row.IsActive;

        public ISelectionProvider? SelectionContainer => null;

        public void AddToSelection()
        {
        }

        public void RemoveFromSelection()
        {
        }

        public void Select()
        {
        }
    }
}
