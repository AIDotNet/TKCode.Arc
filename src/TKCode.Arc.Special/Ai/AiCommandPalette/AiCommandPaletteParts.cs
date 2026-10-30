using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;
using AvaloniaTextBox = Avalonia.Controls.TextBox;

namespace TKCode.Arc;

/// <summary>The bare text box of the input row (the row draws the frame). Focus stays here while the rows are browsed.</summary>
internal sealed class AiCommandPaletteInput : AvaloniaTextBox
{
    protected override Type StyleKeyOverride => typeof(AiCommandPaletteInput);

    protected override AutomationPeer OnCreateAutomationPeer() => new InputPeer(this);

    /// <summary>The input reads as a combo box that owns the result list (the closest Avalonia role to a combobox input).</summary>
    private sealed class InputPeer(AiCommandPaletteInput owner) : TextBoxAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ComboBox;
    }
}

/// <summary>One half of the mode toggle: a pressed-state button that never takes focus, so the input keeps it.</summary>
internal sealed class AiCommandPaletteModeButton : ToggleButton
{
    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<AiCommandPaletteModeButton, Geometry?>(nameof(Glyph));

    static AiCommandPaletteModeButton()
    {
        FocusableProperty.OverrideDefaultValue<AiCommandPaletteModeButton>(false);
    }

    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    protected override Type StyleKeyOverride => typeof(AiCommandPaletteModeButton);

    /// <summary>The palette decides the pressed state; a click only asks for the mode.</summary>
    protected override void Toggle()
    {
    }
}

/// <summary>A group heading in a list (12px medium, muted).</summary>
internal sealed class AiCommandPaletteGroupHeading : TextBlock
{
    protected override Type StyleKeyOverride => typeof(AiCommandPaletteGroupHeading);

    protected override AutomationPeer OnCreateAutomationPeer() => new HeadingPeer(this);

    private sealed class HeadingPeer(AiCommandPaletteGroupHeading owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string? GetNameCore() => ((TextBlock)Owner).Text;
    }
}

/// <summary>The line shown in the list when nothing matches the query.</summary>
internal sealed class AiCommandPaletteEmpty : TextBlock
{
    protected override Type StyleKeyOverride => typeof(AiCommandPaletteEmpty);
}

/// <summary>A key cap ("⌘", "Tab", "↵").</summary>
internal sealed class AiCommandPaletteKeycap : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<AiCommandPaletteKeycap, string?>(nameof(Text));

    /// <summary>Key caps use tabular figures.</summary>
    public static readonly FontFeatureCollection TabularNumbers = new() { FontFeature.Parse("tnum") };

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <summary>A row of key caps for "G I" or "⌘ ⇧ L" (keys split on spaces).</summary>
    public static StackPanel Row(string keys)
    {
        var panel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 3, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        foreach (var key in keys.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            panel.Children.Add(new AiCommandPaletteKeycap { Text = key });
        }
        return panel;
    }
}

/// <summary>A footer hint: key caps and what they do ("↵ Open").</summary>
internal sealed class AiCommandPaletteHint : TemplatedControl
{
    public static readonly StyledProperty<string?> KeysProperty =
        AvaloniaProperty.Register<AiCommandPaletteHint, string?>(nameof(Keys));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<AiCommandPaletteHint, string?>(nameof(Label));

    private ContentPresenter? _keys;

    public string? Keys { get => GetValue(KeysProperty); set => SetValue(KeysProperty, value); }

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _keys = e.NameScope.Find<ContentPresenter>("PART_Keys");
        Sync();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == KeysProperty)
        {
            Sync();
        }
    }

    private void Sync()
    {
        if (_keys is not null)
        {
            _keys.Content = string.IsNullOrEmpty(Keys) ? null : AiCommandPaletteKeycap.Row(Keys);
        }
    }
}

/// <summary>What a row stands for.</summary>
internal enum AiCommandPaletteRowKind
{
    /// <summary>A page or an action from the items.</summary>
    Item,

    /// <summary>"Ask AI about …" in search.</summary>
    AskQuery,

    /// <summary>A suggested question.</summary>
    Question,

    /// <summary>A suggested action under an answer.</summary>
    Action,

    /// <summary>Ask the failed question again.</summary>
    Retry
}

/// <summary>
/// One 40px row: a 16px icon, the label with quiet meta text after it (meta truncates first) and something at the end (key
/// caps, a status icon). The list's single highlight marks the active row; the row only tints its icon. Rows never take
/// focus: the input keeps it and the active row is reported through the selection item pattern.
/// </summary>
[PseudoClasses(ActivePseudoClass, AccentPseudoClass, DonePseudoClass, FailedPseudoClass)]
internal sealed class AiCommandPaletteRow : TemplatedControl
{
    public const string ActivePseudoClass = ":active";
    public const string AccentPseudoClass = ":accent";
    public const string DonePseudoClass = ":done";
    public const string FailedPseudoClass = ":failed";

    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<AiCommandPaletteRow, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<AiCommandPaletteRow, string?>(nameof(Title));

    public static readonly StyledProperty<string?> MetaProperty =
        AvaloniaProperty.Register<AiCommandPaletteRow, string?>(nameof(Meta));

    public static readonly StyledProperty<object?> TrailingProperty =
        AvaloniaProperty.Register<AiCommandPaletteRow, object?>(nameof(Trailing));

    private static readonly TimeSpan s_out = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan s_in = TimeSpan.FromMilliseconds(160);

    private Control? _copy;
    private MotionValue<double>? _copyOpacity;
    private int _swap;
    private bool _active;
    private Point? _lastPointer;

    static AiCommandPaletteRow()
    {
        FocusableProperty.OverrideDefaultValue<AiCommandPaletteRow>(false);
    }

    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public string? Meta { get => GetValue(MetaProperty); set => SetValue(MetaProperty, value); }

    public object? Trailing { get => GetValue(TrailingProperty); set => SetValue(TrailingProperty, value); }

    public AiCommandPaletteRowKind Kind { get; init; }

    /// <summary>The item, question, action or null this row stands for.</summary>
    public object? Payload { get; init; }

    public bool IsActive => _active;

    /// <summary>Raised when a fine pointer actually moves over the row (not when the list scrolls under a resting pointer).</summary>
    public event Action<AiCommandPaletteRow>? Hovered;

    /// <summary>Raised when the row is clicked or tapped, or chosen through automation.</summary>
    public event Action<AiCommandPaletteRow>? Invoked;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _copy = e.NameScope.Find<Control>("PART_Copy");
        UpdateName();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TitleProperty || change.Property == MetaProperty)
        {
            UpdateName();
        }
    }

    private void UpdateName() =>
        AutomationProperties.SetName(this, string.IsNullOrEmpty(Meta) ? Title : Title + ", " + Meta);

    public void SetActive(bool active)
    {
        if (_active == active)
        {
            return;
        }
        _active = active;
        PseudoClasses.Set(ActivePseudoClass, active);
        if (ControlAutomationPeer.FromElement(this) is { } peer)
        {
            peer.RaisePropertyChangedEvent(SelectionItemPatternIdentifiers.IsSelectedProperty, !active, active);
        }
    }

    public void SetState(bool done, bool failed)
    {
        PseudoClasses.Set(DonePseudoClass, done);
        PseudoClasses.Set(FailedPseudoClass, failed);
    }

    public void SetAccent(bool accent) => PseudoClasses.Set(AccentPseudoClass, accent);

    /// <summary>
    /// Changes the label and meta with one crossfade: the old copy fades out over 100ms, the new one in over 160ms.
    /// Code-driven because the text changes between the two halves, which a ControlTheme transition cannot sequence.
    /// Reduced motion: a 120ms fade in. Off: at once.
    /// </summary>
    public async void SwapCopy(string? title, string? meta)
    {
        if (Title == title && Meta == meta)
        {
            return;
        }
        var level = IsLoaded ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;
        if (_copy is null || level == ArcMotionLevel.Off)
        {
            Title = title;
            Meta = meta;
            return;
        }
        var swap = ++_swap;
        if (_copyOpacity is null)
        {
            _copyOpacity = new MotionValue<double>(this, 1, MotionVectors.Double);
            _copyOpacity.Changed += v =>
            {
                if (_copy is not null)
                {
                    _copy.Opacity = v;
                }
            };
        }
        if (level == ArcMotionLevel.Full)
        {
            await _copyOpacity.AnimateTo(0, MotionSpec.Tween(s_out, ArcEasings.Standard));
            if (swap != _swap)
            {
                return;
            }
        }
        else
        {
            _copyOpacity.Jump(0);
        }
        Title = title;
        Meta = meta;
        await _copyOpacity.AnimateTo(1, MotionSpec.Tween(level == ArcMotionLevel.Full ? s_in : TimeSpan.FromMilliseconds(120), ArcEasings.Standard));
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        // Hover only moves the highlight for a fine pointer that actually moves.
        if (e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        var point = e.GetPosition(null);
        if (_lastPointer is { } last && last == point)
        {
            return;
        }
        _lastPointer = point;
        Hovered?.Invoke(this);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _lastPointer = null;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            // Keep the focus in the input.
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.InitialPressMouseButton == MouseButton.Left && new Rect(Bounds.Size).Contains(e.GetPosition(this)))
        {
            e.Handled = true;
            Invoked?.Invoke(this);
        }
    }

    internal void Invoke() => Invoked?.Invoke(this);

    protected override AutomationPeer OnCreateAutomationPeer() => new RowPeer(this);

    private sealed class RowPeer(AiCommandPaletteRow owner) : ControlAutomationPeer(owner), Avalonia.Automation.Provider.ISelectionItemProvider,
        Avalonia.Automation.Provider.IInvokeProvider
    {
        private AiCommandPaletteRow Row => (AiCommandPaletteRow)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        public bool IsSelected => Row.IsActive;

        public Avalonia.Automation.Provider.ISelectionProvider? SelectionContainer => null;

        public void AddToSelection()
        {
        }

        public void RemoveFromSelection()
        {
        }

        public void Select() => Row.Hovered?.Invoke(Row);

        public void Invoke() => Row.Invoke();
    }
}

/// <summary>
/// The copy of a row: the label keeps its own width (up to the whole row), the meta text takes what is left and truncates,
/// so meta gives way first. Both sit on one baseline.
/// </summary>
internal sealed class AiCommandPaletteRowCopy : Panel
{
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<AiCommandPaletteRowCopy, double>(nameof(Gap), 8);

    static AiCommandPaletteRowCopy()
    {
        AffectsMeasure<AiCommandPaletteRowCopy>(GapProperty);
    }

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    private Control? Title => Children.Count > 0 ? Children[0] : null;

    private Control? Meta => Children.Count > 1 && Children[1].IsVisible ? Children[1] : null;

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Title is not { } title)
        {
            return default;
        }
        title.Measure(availableSize);
        var titleWidth = Math.Min(title.DesiredSize.Width, availableSize.Width);
        if (Meta is not { } meta)
        {
            return new Size(titleWidth, title.DesiredSize.Height);
        }
        var rest = Math.Max(0, availableSize.Width - titleWidth - Gap);
        meta.Measure(new Size(rest, availableSize.Height));
        var (above, below) = Extent(title, meta);
        return new Size(Math.Min(availableSize.Width, titleWidth + Gap + meta.DesiredSize.Width), above + below);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Title is not { } title)
        {
            return finalSize;
        }
        var titleWidth = Math.Min(title.DesiredSize.Width, finalSize.Width);
        if (Meta is not { } meta)
        {
            title.Arrange(new Rect(0, (finalSize.Height - title.DesiredSize.Height) / 2, titleWidth, title.DesiredSize.Height));
            if (Children.Count > 1)
            {
                Children[1].Arrange(default);
            }
            return finalSize;
        }
        var (above, below) = Extent(title, meta);
        var top = (finalSize.Height - above - below) / 2;
        title.Arrange(new Rect(0, top + above - Baseline(title), titleWidth, title.DesiredSize.Height));
        var x = titleWidth + Gap;
        meta.Arrange(new Rect(x, top + above - Baseline(meta), Math.Max(0, finalSize.Width - x), meta.DesiredSize.Height));
        return finalSize;
    }

    private static (double Above, double Below) Extent(Control title, Control meta)
    {
        var titleBase = Baseline(title);
        var metaBase = Baseline(meta);
        return (Math.Max(titleBase, metaBase), Math.Max(title.DesiredSize.Height - titleBase, meta.DesiredSize.Height - metaBase));
    }

    private static double Baseline(Control control)
    {
        if (control is TextBlock text && text.TextLayout is { TextLines.Count: > 0 } layout)
        {
            return text.Padding.Top + layout.TextLines[0].Baseline;
        }
        return control.DesiredSize.Height;
    }
}

/// <summary>
/// The rows of one list with their headings and a single highlight behind the active row. The highlight glides between rows
/// in 130ms and jumps without travel when the list itself changes, so typing never animates it.
/// </summary>
internal sealed class AiCommandPaletteRowList : Panel
{
    public static readonly StyledProperty<IBrush?> HighlightBrushProperty =
        AvaloniaProperty.Register<AiCommandPaletteRowList, IBrush?>(nameof(HighlightBrush));

    public static readonly StyledProperty<CornerRadius> HighlightCornerRadiusProperty =
        AvaloniaProperty.Register<AiCommandPaletteRowList, CornerRadius>(nameof(HighlightCornerRadius));

    /// <summary>The highlight's travel between rows.</summary>
    internal static readonly TimeSpan Glide = TimeSpan.FromMilliseconds(130);

    private readonly GlidingIndicator _highlight = new();
    private readonly StackPanel _stack = new();
    private readonly List<AiCommandPaletteRow> _rows = [];
    private int _active = -1;
    private bool _jumpPending;
    private Rect _placed;

    public AiCommandPaletteRowList()
    {
        _highlight.FadeDuration = TimeSpan.FromMilliseconds(80);
        Children.Add(_highlight);
        Children.Add(_stack);
    }

    protected override Type StyleKeyOverride => typeof(AiCommandPaletteRowList);

    public IBrush? HighlightBrush { get => GetValue(HighlightBrushProperty); set => SetValue(HighlightBrushProperty, value); }

    public CornerRadius HighlightCornerRadius { get => GetValue(HighlightCornerRadiusProperty); set => SetValue(HighlightCornerRadiusProperty, value); }

    public IReadOnlyList<AiCommandPaletteRow> Rows => _rows;

    public int ActiveIndex => _active;

    public AiCommandPaletteRow? ActiveRow => _active >= 0 && _active < _rows.Count ? _rows[_active] : null;

    internal GlidingIndicator Highlight => _highlight;

    /// <summary>Raised when a row is hovered or clicked: (row, invoked).</summary>
    public event Action<AiCommandPaletteRow, bool>? RowEvent;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HighlightBrushProperty)
        {
            _highlight.Fill = HighlightBrush;
        }
        else if (change.Property == HighlightCornerRadiusProperty)
        {
            _highlight.CornerRadius = HighlightCornerRadius;
        }
    }

    /// <summary>Replaces the entries (headings, rows, lines) and puts the highlight on <paramref name="active"/> without travel.</summary>
    public void SetEntries(IEnumerable<Control> entries, int active)
    {
        foreach (var row in _rows)
        {
            row.Hovered -= OnHovered;
            row.Invoked -= OnInvoked;
        }
        _rows.Clear();
        _stack.Children.Clear();
        foreach (var entry in entries)
        {
            _stack.Children.Add(entry);
            if (entry is AiCommandPaletteRow row)
            {
                _rows.Add(row);
                row.Hovered += OnHovered;
                row.Invoked += OnInvoked;
            }
        }
        _active = -1;
        SetActive(active, animate: false);
    }

    /// <summary>Moves the active row; the highlight glides there (or jumps without <paramref name="animate"/>).</summary>
    public void SetActive(int index, bool animate)
    {
        index = _rows.Count == 0 ? -1 : Math.Clamp(index, -1, _rows.Count - 1);
        if (_active >= 0 && _active < _rows.Count)
        {
            _rows[_active].SetActive(false);
        }
        _active = index;
        if (index < 0)
        {
            _highlight.Hide(animate);
            _jumpPending = false;
            return;
        }
        var row = _rows[index];
        row.SetActive(true);
        if (row.Bounds.Height > 0 && row.IsArrangeValid && !_jumpPending)
        {
            Place(row, animate);
        }
        else
        {
            _jumpPending = true;
            InvalidateArrange();
        }
        row.BringIntoView();
    }

    private void Place(AiCommandPaletteRow row, bool animate)
    {
        _placed = row.Bounds;
        _highlight.MoveTo(row.Bounds, Glide, ArcEasings.Standard, animate);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        if (ActiveRow is { } row && row.Bounds.Height > 0 && (_jumpPending || row.Bounds != _placed))
        {
            // A new list lands without travel; a resized one (the container changed width) follows at once.
            _jumpPending = false;
            Place(row, animate: false);
        }
        return size;
    }

    private void OnHovered(AiCommandPaletteRow row) => RowEvent?.Invoke(row, false);

    private void OnInvoked(AiCommandPaletteRow row) => RowEvent?.Invoke(row, true);
}

/// <summary>A numbered source chip above the answer: the number, the source's mark and its title.</summary>
[PseudoClasses(PartnerPseudoClass)]
internal sealed class AiCommandPaletteSourceChip : AvaloniaButton
{
    public const string PartnerPseudoClass = ":partner";

    public static readonly StyledProperty<int> NumberProperty =
        AvaloniaProperty.Register<AiCommandPaletteSourceChip, int>(nameof(Number));

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<AiCommandPaletteSourceChip, string?>(nameof(Title));

    public static readonly StyledProperty<Geometry?> MarkProperty =
        AvaloniaProperty.Register<AiCommandPaletteSourceChip, Geometry?>(nameof(Mark));

    public int Number { get => GetValue(NumberProperty); set => SetValue(NumberProperty, value); }

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public Geometry? Mark { get => GetValue(MarkProperty); set => SetValue(MarkProperty, value); }

    public AiCommandPaletteSource? Source { get; init; }

    protected override Type StyleKeyOverride => typeof(AiCommandPaletteSourceChip);

    public void SetPartner(bool on) => PseudoClasses.Set(PartnerPseudoClass, on);
}

/// <summary>An inline [n] citation in the answer: a small numbered button.</summary>
[PseudoClasses(PartnerPseudoClass)]
internal sealed class AiCommandPaletteCitation : AvaloniaButton
{
    public const string PartnerPseudoClass = ":partner";

    public static readonly StyledProperty<int> NumberProperty =
        AvaloniaProperty.Register<AiCommandPaletteCitation, int>(nameof(Number));

    public int Number { get => GetValue(NumberProperty); set => SetValue(NumberProperty, value); }

    public AiCommandPaletteSource? Source { get; init; }

    protected override Type StyleKeyOverride => typeof(AiCommandPaletteCitation);

    public void SetPartner(bool on) => PseudoClasses.Set(PartnerPseudoClass, on);
}

/// <summary>One word of the answer (its text pieces and any citations stuck to it); the answer flows these like text.</summary>
internal sealed class AiCommandPaletteAnswerWord : StackPanel
{
    public AiCommandPaletteAnswerWord()
    {
        Orientation = Avalonia.Layout.Orientation.Horizontal;
    }

    /// <summary>The word as it was streamed (with its [n] citations).</summary>
    public string Text { get; init; } = string.Empty;
}

/// <summary>A paragraph break in the answer.</summary>
internal sealed class AiCommandPaletteParagraphBreak : Control
{
}

/// <summary>
/// The streamed answer, laid out like text: words flow left to right with a word gap and wrap to the next line, a paragraph
/// break starts a new paragraph after a gap. Every line is <see cref="LineHeight"/> tall and items sit on its center, so a
/// citation never changes the line height.
/// </summary>
internal sealed class AiCommandPaletteAnswerText : Panel
{
    public static readonly StyledProperty<double> LineHeightProperty =
        AvaloniaProperty.Register<AiCommandPaletteAnswerText, double>(nameof(LineHeight), 22);

    public static readonly StyledProperty<double> WordGapProperty =
        AvaloniaProperty.Register<AiCommandPaletteAnswerText, double>(nameof(WordGap), 4);

    public static readonly StyledProperty<double> ParagraphGapProperty =
        AvaloniaProperty.Register<AiCommandPaletteAnswerText, double>(nameof(ParagraphGap), 10);

    static AiCommandPaletteAnswerText()
    {
        AffectsMeasure<AiCommandPaletteAnswerText>(LineHeightProperty, WordGapProperty, ParagraphGapProperty);
    }

    protected override Type StyleKeyOverride => typeof(AiCommandPaletteAnswerText);

    public double LineHeight { get => GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }

    public double WordGap { get => GetValue(WordGapProperty); set => SetValue(WordGapProperty, value); }

    public double ParagraphGap { get => GetValue(ParagraphGapProperty); set => SetValue(ParagraphGapProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children)
        {
            child.Measure(new Size(double.PositiveInfinity, LineHeight));
        }
        var height = Flow(availableSize.Width, null);
        return new Size(double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Flow(finalSize.Width, finalSize);
        return finalSize;
    }

    /// <summary>Places the children (when <paramref name="arrange"/> is set) and returns the height they take.</summary>
    private double Flow(double width, Size? arrange)
    {
        var line = LineHeight;
        double x = 0;
        double y = 0;
        var lineUsed = false;
        foreach (var child in Children)
        {
            if (child is AiCommandPaletteParagraphBreak)
            {
                if (lineUsed)
                {
                    y += line + ParagraphGap;
                }
                x = 0;
                lineUsed = false;
                child.Arrange(default);
                continue;
            }
            var size = child.DesiredSize;
            var gap = lineUsed ? WordGap : 0;
            if (lineUsed && x + gap + size.Width > width)
            {
                y += line;
                x = 0;
                gap = 0;
            }
            if (arrange is not null)
            {
                var w = Math.Min(size.Width, double.IsInfinity(width) ? size.Width : width);
                child.Arrange(new Rect(x + gap, y + (line - size.Height) / 2, w, size.Height));
            }
            x += gap + size.Width;
            lineUsed = true;
        }
        return lineUsed ? y + line : Math.Max(0, y - ParagraphGap);
    }
}
