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
using Avalonia.Layout;
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
internal class SearchResultsMarkedText : TextBlock
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

/// <summary>A paragraph of the detail body: marked text with its own theme (it is added by code, outside any template).</summary>
internal sealed class SearchResultsParagraph : SearchResultsMarkedText
{
    protected override Type StyleKeyOverride => typeof(SearchResultsParagraph);
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


/// <summary>
/// The results area of the pane: the scrolling list takes the height it needs up to what the pane leaves, and the key
/// hint footer sits right under it (at the bottom of the pane once the list fills it). Measured here instead of with
/// a star row because a short list must not leave a gap before the footer.
/// </summary>
internal sealed class SearchResultsPaneLayout : Panel
{
    public static readonly StyledProperty<double> FooterGapProperty =
        AvaloniaProperty.Register<SearchResultsPaneLayout, double>(nameof(FooterGap));

    static SearchResultsPaneLayout()
    {
        AffectsMeasure<SearchResultsPaneLayout>(FooterGapProperty);
    }

    /// <summary>Space between the list and the footer.</summary>
    public double FooterGap { get => GetValue(FooterGapProperty); set => SetValue(FooterGapProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count == 0)
        {
            return default;
        }
        var body = Children[0];
        var footer = Children.Count > 1 ? Children[1] : null;
        var footerHeight = 0.0;
        if (footer is not null && footer.IsVisible)
        {
            footer.Measure(new Size(availableSize.Width, double.PositiveInfinity));
            footerHeight = footer.DesiredSize.Height + FooterGap;
        }
        var room = double.IsInfinity(availableSize.Height) ? double.PositiveInfinity : Math.Max(0, availableSize.Height - footerHeight);
        body.Measure(new Size(availableSize.Width, room));
        return new Size(availableSize.Width, body.DesiredSize.Height + footerHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count == 0)
        {
            return finalSize;
        }
        var body = Children[0];
        var footer = Children.Count > 1 ? Children[1] : null;
        var footerHeight = footer is { IsVisible: true } ? footer.DesiredSize.Height + FooterGap : 0;
        var height = Math.Min(body.DesiredSize.Height, Math.Max(0, finalSize.Height - footerHeight));
        body.Arrange(new Rect(0, 0, finalSize.Width, height));
        if (footer is { IsVisible: true })
        {
            footer.Arrange(new Rect(0, height + FooterGap, finalSize.Width, footer.DesiredSize.Height));
        }
        return finalSize;
    }
}

/// <summary>
/// One result: a leading avatar or icon, the title with its matches marked, a subtitle, a snippet window and a caption.
/// Pooled by <see cref="SearchResults"/>: <see cref="SetData"/> swaps in a new item without re-templating, and does
/// nothing when the item, the query and the layout settings are the ones it already shows.
/// </summary>
[TemplatePart(TitlePart, typeof(SearchResultsMarkedText))]
[TemplatePart(SubtitlePart, typeof(SearchResultsMarkedText))]
[TemplatePart(SnippetPart, typeof(SearchResultsMarkedText))]
[TemplatePart(CaptionPart, typeof(SearchResultsMarkedText))]
[PseudoClasses(ActivePseudoClass, HasSubtitlePseudoClass, HasSnippetPseudoClass, HasCaptionPseudoClass, AvatarPseudoClass)]
internal sealed class SearchResultRow : TemplatedControl
{
    public const string TitlePart = "PART_Title";
    public const string SubtitlePart = "PART_Subtitle";
    public const string SnippetPart = "PART_Snippet";
    public const string CaptionPart = "PART_Caption";
    public const string ActivePseudoClass = ":active";
    public const string HasSubtitlePseudoClass = ":has-subtitle";
    public const string HasSnippetPseudoClass = ":has-snippet";
    public const string HasCaptionPseudoClass = ":has-caption";
    public const string AvatarPseudoClass = ":avatar";

    public static readonly DirectProperty<SearchResultRow, object?> LeadContentProperty =
        AvaloniaProperty.RegisterDirect<SearchResultRow, object?>(nameof(LeadContent), o => o.LeadContent);

    private object? _leadContent;
    // A plain 16px icon, centered on the title's first line (22 high) in the 36px lead column.
    private readonly ArcGlyph _glyph = new()
    {
        Size = 16,
        StrokeThickness = 1.75,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Top,
        Margin = new Thickness(0, 3, 0, 0)
    };
    private Avatar? _avatar;
    private readonly List<SearchMatchRange> _ranges = new();
    private SearchResultsMarkedText? _titleText;
    private SearchResultsMarkedText? _subtitleText;
    private SearchResultsMarkedText? _snippetText;
    private SearchResultsMarkedText? _captionText;
    private string? _title;
    private string? _subtitle;
    private string? _snippet;
    private string? _caption;
    private string _kindLabel = string.Empty;
    private string? _shownQuery;
    private int _shownBudget = -1;
    private bool _hasAvatar;
    private SnippetWindow _window;

    static SearchResultRow()
    {
        FocusableProperty.OverrideDefaultValue<SearchResultRow>(false);
    }

    public SearchResultRow()
    {
        RenderTransform = new TranslateTransform();
    }

    /// <summary>The avatar or icon shown before the text.</summary>
    public object? LeadContent { get => _leadContent; private set => SetAndRaise(LeadContentProperty, ref _leadContent, value); }

    /// <summary>The shown item (null while pooled).</summary>
    public SearchResultItem? Item { get; private set; }

    /// <summary>Position among the shown rows (the cursor's index).</summary>
    public int Index { get; set; }

    /// <summary>Whether the cursor is on this row.</summary>
    public bool IsActive { get; private set; }

    internal SearchResultsMarkedText? TitleText => _titleText;
    internal SearchResultsMarkedText? SubtitleText => _subtitleText;
    internal SearchResultsMarkedText? SnippetText => _snippetText;
    internal SearchResultsMarkedText? CaptionText => _captionText;

    /// <summary>What the row shows is already <paramref name="item"/> for <paramref name="query"/> at this snippet budget.</summary>
    public bool Shows(SearchResultItem item, string query, int budget)
        => ReferenceEquals(Item, item) && _shownBudget == budget && string.Equals(_shownQuery, query, StringComparison.Ordinal);

    /// <summary>
    /// Shows <paramref name="item"/>: marks the <paramref name="terms"/> in its texts, cuts the snippet to a window
    /// around the first match and picks the avatar or icon.
    /// </summary>
    public void SetData(SearchResultItem item, string subtitle, string kindLabel, string query, List<string> terms, in SnippetWindow window)
    {
        Item = item;
        _shownQuery = query;
        _shownBudget = window.Budget;
        _window = window;
        _kindLabel = kindLabel;
        _title = item.Title;
        _subtitle = string.IsNullOrEmpty(subtitle) ? null : subtitle;
        _caption = string.IsNullOrEmpty(item.Caption) ? null : item.Caption;
        // The passage that shows why the item matched: its snippet, or the paragraph of its body that has the words
        // (line breaks folded: a row shows one flowing passage).
        var passage = item.Snippet;
        if (terms.Count > 0 && !string.IsNullOrEmpty(item.Body) && !AnyTerm(passage, terms) && AnyTerm(item.Body, terms))
        {
            passage = ParagraphWith(item.Body, terms);
        }
        else if (passage is not null && passage.AsSpan().IndexOfAny('\r', '\n') >= 0)
        {
            passage = string.Join(' ', SearchResultsDetailBody.SplitParagraphs(passage));
        }
        _snippet = string.IsNullOrEmpty(passage) ? null : passage;
        IsEnabled = item.IsEnabled;
        var avatar = !string.IsNullOrEmpty(item.AvatarName) || item.Avatar is not null;
        if (avatar != _hasAvatar)
        {
            _hasAvatar = avatar;
            PseudoClasses.Set(AvatarPseudoClass, avatar);
        }
        LeadContent = avatar ? AvatarFor(item) : item.Icon switch
        {
            Geometry geometry => Glyph(geometry),
            Control control => control,
            _ => Glyph(ArcIcons.FileText)
        };
        Push(terms, window);
    }

    private static string ParagraphWith(string body, List<string> terms)
    {
        string? first = null;
        foreach (var paragraph in SearchResultsDetailBody.SplitParagraphs(body))
        {
            first ??= paragraph;
            if (AnyTerm(paragraph, terms))
            {
                return paragraph;
            }
        }
        return first ?? body;
    }

    private static bool AnyTerm(string? text, List<string> terms)
    {
        foreach (var term in terms)
        {
            if (SearchResultsMatcher.Contains(text, term))
            {
                return true;
            }
        }
        return false;
    }

    private Avatar AvatarFor(SearchResultItem item)
    {
        _avatar ??= new Avatar { Size = AvatarSize.Medium };
        _avatar.DisplayName = item.AvatarName ?? item.Title;
        _avatar.Source = item.Avatar;
        return _avatar;
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
        _shownQuery = null;
        _shownBudget = -1;
        LeadContent = null;
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

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _titleText = e.NameScope.Find<SearchResultsMarkedText>(TitlePart);
        _subtitleText = e.NameScope.Find<SearchResultsMarkedText>(SubtitlePart);
        _snippetText = e.NameScope.Find<SearchResultsMarkedText>(SnippetPart);
        _captionText = e.NameScope.Find<SearchResultsMarkedText>(CaptionPart);
        if (Item is not null)
        {
            Push(Terms, _window);
        }
    }

    private List<string> Terms { get; set; } = new();

    private void Push(List<string> terms, in SnippetWindow window)
    {
        Terms = terms;
        PseudoClasses.Set(HasSubtitlePseudoClass, _subtitle is not null);
        PseudoClasses.Set(HasSnippetPseudoClass, _snippet is not null);
        PseudoClasses.Set(HasCaptionPseudoClass, _caption is not null);
        SetMarked(_titleText, _title, terms);
        SetMarked(_subtitleText, _subtitle, terms);
        SetMarked(_captionText, _caption, terms);
        if (_snippetText is not null)
        {
            if (_snippet is null)
            {
                _snippetText.SetContent(null, null);
            }
            else
            {
                var text = window.MaxLength > 0
                    ? SearchResultsMatcher.Window(_snippet, terms, window.Lead, window.Budget, window.MaxLength, _ranges)
                    : _snippet;
                if (window.MaxLength <= 0)
                {
                    SearchResultsMatcher.FindRanges(text.AsSpan(), terms, _ranges);
                }
                _snippetText.SetContent(text, _ranges);
            }
        }
        var name = _title ?? string.Empty;
        if (_kindLabel.Length > 0)
        {
            name += ", " + _kindLabel;
        }
        if (_subtitle is not null)
        {
            name += ", " + _subtitle;
        }
        AutomationProperties.SetName(this, name);
    }

    private void SetMarked(SearchResultsMarkedText? target, string? text, List<string> terms)
    {
        if (target is null)
        {
            return;
        }
        if (text is null)
        {
            target.SetContent(null, null);
            return;
        }
        SearchResultsMatcher.FindRanges(text.AsSpan(), terms, _ranges);
        target.SetContent(text, _ranges);
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

/// <summary>How a long snippet is cut around its first match (see <see cref="SearchResultsMatcher.Window"/>); a zero <see cref="MaxLength"/> shows it whole.</summary>
internal readonly record struct SnippetWindow(int Lead, int Budget, int MaxLength);
