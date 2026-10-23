using TKCode.Arc.DesignTokens;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using TKCode.Arc.Primitives;

using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>Reads ArcDesign colors and shadows for the Kanban parts (only while attached; transparent / none otherwise).</summary>
internal static class KanbanTokens
{
    private static readonly Color s_violet = Oklch.ToColor(0.62, 0.19, 300);
    private static readonly Color s_blue = Oklch.ToColor(0.62, 0.15, 230);
    private static readonly Color s_teal = Oklch.ToColor(0.62, 0.11, 190);
    private static readonly Color s_rose = Oklch.ToColor(0.62, 0.19, 10);

    public static Color Color(Control owner, ArcDesignTokenKind kind) => ArcTokens.GetColor(owner, kind);

    public static BoxShadows Shadow(Control owner, ArcDesignTokenKind kind)
    {
        if (owner.TryFindResource(kind, owner.ActualThemeVariant, out var value) && value is BoxShadows shadows)
        {
            return shadows;
        }
        return default;
    }

    /// <summary>The dot color of a label: its own color, a theme color for the status tones, or a fixed hue.</summary>
    public static Color LabelColor(Control owner, KanbanLabel label)
    {
        if (label.Color is { } color)
        {
            return color;
        }
        return label.Tone switch
        {
            KanbanLabelTone.Warning => Color(owner, ArcDesignTokenKind.Warning),
            KanbanLabelTone.Success => Color(owner, ArcDesignTokenKind.Success),
            KanbanLabelTone.Danger => Color(owner, ArcDesignTokenKind.Danger),
            KanbanLabelTone.Violet => s_violet,
            KanbanLabelTone.Blue => s_blue,
            KanbanLabelTone.Teal => s_teal,
            KanbanLabelTone.Rose => s_rose,
            KanbanLabelTone.Neutral => Color(owner, ArcDesignTokenKind.TextMuted),
            _ => Color(owner, ArcDesignTokenKind.Accent)
        };
    }

    /// <summary>One shadow list with an extra ring (Arc's lifted card: the raised shadow plus a 3px accent ring).</summary>
    public static BoxShadows WithRing(BoxShadows shadows, Color ring, double spread)
    {
        var list = new List<BoxShadow>();
        for (var i = 0; i < shadows.Count; i++)
        {
            list.Add(shadows[i]);
        }
        list.Add(new BoxShadow { Spread = spread, Color = ring });
        return new BoxShadows(list[0], list.Skip(1).ToArray());
    }
}

/// <summary>
/// One card on the board (internal): the id and label on the first row, the wrapping title, and the meta row (due
/// date, checklist progress, estimate, owner's avatar). Keyboard focusable; its frame color and shadow follow the
/// state (rest, hover, lifted with the keyboard, dragged, landing) and are computed from the theme in code, then
/// eased by the theme's transitions. The card never leaves the board's panel: moves are render offsets.
/// </summary>
[PseudoClasses(LiftedPseudoClass, DraggingPseudoClass, LandingPseudoClass, ChecklistCompletePseudoClass, UntitledPseudoClass,
    FocusRingPseudoClass)]
internal sealed class KanbanCardView : TemplatedControl
{
    /// <summary>Shows the focus ring without focus (the lifted card's proxy copies its card's ring).</summary>
    public const string FocusRingPseudoClass = ":focus-ring";

    public const string LiftedPseudoClass = ":lifted";
    public const string DraggingPseudoClass = ":dragging";
    public const string LandingPseudoClass = ":landing";
    public const string ChecklistCompletePseudoClass = ":checklist-complete";
    public const string UntitledPseudoClass = ":untitled";

    public static readonly StyledProperty<string?> KeyTextProperty =
        AvaloniaProperty.Register<KanbanCardView, string?>(nameof(KeyText));

    public static readonly StyledProperty<string?> TitleTextProperty =
        AvaloniaProperty.Register<KanbanCardView, string?>(nameof(TitleText));

    public static readonly StyledProperty<string?> LabelTextProperty =
        AvaloniaProperty.Register<KanbanCardView, string?>(nameof(LabelText));

    public static readonly StyledProperty<IBrush?> LabelBrushProperty =
        AvaloniaProperty.Register<KanbanCardView, IBrush?>(nameof(LabelBrush));

    public static readonly StyledProperty<bool> HasLabelProperty =
        AvaloniaProperty.Register<KanbanCardView, bool>(nameof(HasLabel));

    public static readonly StyledProperty<string?> DueTextProperty =
        AvaloniaProperty.Register<KanbanCardView, string?>(nameof(DueText));

    public static readonly StyledProperty<bool> HasDueProperty =
        AvaloniaProperty.Register<KanbanCardView, bool>(nameof(HasDue));

    public static readonly StyledProperty<string?> ChecklistTextProperty =
        AvaloniaProperty.Register<KanbanCardView, string?>(nameof(ChecklistText));

    public static readonly StyledProperty<bool> HasChecklistProperty =
        AvaloniaProperty.Register<KanbanCardView, bool>(nameof(HasChecklist));

    public static readonly StyledProperty<string?> PointsTextProperty =
        AvaloniaProperty.Register<KanbanCardView, string?>(nameof(PointsText));

    public static readonly StyledProperty<bool> HasPointsProperty =
        AvaloniaProperty.Register<KanbanCardView, bool>(nameof(HasPoints));

    public static readonly StyledProperty<ProjectPerson?> AssigneeProperty =
        AvaloniaProperty.Register<KanbanCardView, ProjectPerson?>(nameof(Assignee));

    public static readonly StyledProperty<bool> HasAssigneeProperty =
        AvaloniaProperty.Register<KanbanCardView, bool>(nameof(HasAssignee));

    /// <summary>The frame's border color (state colors are set in code; the theme eases them).</summary>
    public static readonly StyledProperty<IBrush?> FrameBorderBrushProperty =
        AvaloniaProperty.Register<KanbanCardView, IBrush?>(nameof(FrameBorderBrush));

    /// <summary>The frame's shadow (resting, raised with a ring while lifted, floating while dragged).</summary>
    public static readonly StyledProperty<BoxShadows> FrameShadowProperty =
        AvaloniaProperty.Register<KanbanCardView, BoxShadows>(nameof(FrameShadow));

    private bool _hovered;
    private bool _instantPending;

    public KanbanCardView(KanbanCard card, KanbanBoard board)
    {
        Card = card;
        Board = board;
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.Hand);
        card.PropertyChanged += OnCardChanged;
        AutomationProperties.SetControlTypeOverride(this, AutomationControlType.Button);
        ActualThemeVariantChanged += (_, _) => OnThemeChanged();
        ResourcesChanged += (_, _) => OnThemeChanged();
        Refresh();
    }

    public KanbanCard Card { get; }

    public KanbanBoard Board { get; }

    public string? KeyText { get => GetValue(KeyTextProperty); set => SetValue(KeyTextProperty, value); }

    public string? TitleText { get => GetValue(TitleTextProperty); set => SetValue(TitleTextProperty, value); }

    public string? LabelText { get => GetValue(LabelTextProperty); set => SetValue(LabelTextProperty, value); }

    public IBrush? LabelBrush { get => GetValue(LabelBrushProperty); set => SetValue(LabelBrushProperty, value); }

    public bool HasLabel { get => GetValue(HasLabelProperty); set => SetValue(HasLabelProperty, value); }

    public string? DueText { get => GetValue(DueTextProperty); set => SetValue(DueTextProperty, value); }

    public bool HasDue { get => GetValue(HasDueProperty); set => SetValue(HasDueProperty, value); }

    public string? ChecklistText { get => GetValue(ChecklistTextProperty); set => SetValue(ChecklistTextProperty, value); }

    public bool HasChecklist { get => GetValue(HasChecklistProperty); set => SetValue(HasChecklistProperty, value); }

    public string? PointsText { get => GetValue(PointsTextProperty); set => SetValue(PointsTextProperty, value); }

    public bool HasPoints { get => GetValue(HasPointsProperty); set => SetValue(HasPointsProperty, value); }

    public ProjectPerson? Assignee { get => GetValue(AssigneeProperty); set => SetValue(AssigneeProperty, value); }

    public bool HasAssignee { get => GetValue(HasAssigneeProperty); set => SetValue(HasAssigneeProperty, value); }

    public IBrush? FrameBorderBrush { get => GetValue(FrameBorderBrushProperty); set => SetValue(FrameBorderBrushProperty, value); }

    public BoxShadows FrameShadow { get => GetValue(FrameShadowProperty); set => SetValue(FrameShadowProperty, value); }

    /// <summary>Lifted with the keyboard (Accent border, raised shadow and ring).</summary>
    public bool IsLifted { get; private set; }

    /// <summary>Following the pointer (floating shadow, accent-tinted border).</summary>
    public bool IsDragged { get; private set; }

    /// <summary>Flying into its slot after a drop or a cancel.</summary>
    public bool IsLanding { get; private set; }

    /// <summary>The card's title as read by screen readers ("title, id, owner").</summary>
    public string AccessibleName => AutomationProperties.GetName(this) ?? string.Empty;

    public void Detach() => Card.PropertyChanged -= OnCardChanged;

    private void OnCardChanged(object? sender, AvaloniaPropertyChangedEventArgs e) => Refresh();

    /// <summary>Reads the card again (texts, label, owner, checklist).</summary>
    public void Refresh()
    {
        var card = Card;
        var strings = Board.Strings;
        KeyText = card.Key;
        var untitled = string.IsNullOrWhiteSpace(card.Title);
        TitleText = untitled ? strings.Untitled : card.Title;
        PseudoClasses.Set(UntitledPseudoClass, untitled);
        var label = Board.LabelFor(card.LabelKey);
        HasLabel = label is not null;
        LabelText = label?.Name;
        LabelBrush = label is not null && IsAttachedToTree ? new ImmutableSolidColorBrush(KanbanTokens.LabelColor(this, label)) : null;
        HasDue = card.Due is not null;
        DueText = card.Due is { } due ? strings.Date(due) : null;
        HasChecklist = card.ChecklistTotal > 0;
        ChecklistText = HasChecklist ? string.Format(CultureInfo.InvariantCulture, "{0}/{1}", card.ChecklistDone, card.ChecklistTotal) : null;
        PseudoClasses.Set(ChecklistCompletePseudoClass, card.IsChecklistComplete);
        HasPoints = card.Points is not null;
        PointsText = card.Points is { } points ? strings.PointsShort(points) : null;
        var owner = Board.PersonFor(card.AssigneeKey);
        Assignee = owner;
        HasAssignee = owner is not null;
        AutomationProperties.SetName(this, strings.CardName(untitled ? strings.Untitled : card.Title, card.Key, owner?.Name));
        AutomationProperties.SetItemType(this, strings.CardRole);
        var help = new List<string>();
        if (label is not null)
        {
            help.Add(label.Name);
        }
        if (DueText is not null)
        {
            help.Add(DueText);
        }
        if (HasChecklist)
        {
            help.Add(strings.ChecklistProgress(card.ChecklistDone, card.ChecklistTotal));
        }
        if (card.Points is { } p)
        {
            help.Add(strings.Points(p));
        }
        AutomationProperties.SetHelpText(this, string.Join(", ", help));
    }

    private bool IsAttachedToTree => TopLevel.GetTopLevel(this) is not null;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Refresh();
        UpdateFrame();
    }

    private void OnThemeChanged()
    {
        if (IsAttachedToTree)
        {
            Refresh();
            UpdateFrame();
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        // Arc keeps hover inside (hover: hover): touch shows no hover border.
        if (e.Pointer.Type != PointerType.Touch)
        {
            _hovered = true;
            UpdateFrame();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hovered = false;
        UpdateFrame();
    }

    /// <summary>
    /// The copy drawn above the board while this card is lifted (see <c>KanbanBoard</c>'s drag layer): it takes every
    /// state change this card takes.
    /// </summary>
    public KanbanCardView? Mirror { get; set; }

    /// <summary>True for the drag layer's copy: not focusable, not hit, hidden from screen readers.</summary>
    public bool IsProxy { get; init; }

    /// <summary>The proxy shows the focus ring its card had when it was lifted.</summary>
    public void SetFocusRing(bool visible) => PseudoClasses.Set(FocusRingPseudoClass, visible);

    /// <summary>True while the keyboard focus ring is shown.</summary>
    public bool IsFocusRingVisible => Classes.Contains(":focus-visible") || Classes.Contains(FocusRingPseudoClass);

    /// <summary>Sets the drag state; <paramref name="instant"/> skips the color and shadow easing (a card that just lifted).</summary>
    public void SetState(bool lifted, bool dragged, bool landing, bool instant = false)
    {
        Mirror?.SetState(lifted, dragged, landing, instant);
        IsLifted = lifted;
        IsDragged = dragged;
        IsLanding = landing;
        PseudoClasses.Set(LiftedPseudoClass, lifted);
        PseudoClasses.Set(DraggingPseudoClass, dragged);
        PseudoClasses.Set(LandingPseudoClass, landing);
        if (instant && !_instantPending)
        {
            // Arc's lifted overlay is a new element: it shows the floating shadow and border at once.
            _instantPending = true;
            SetCurrentValue(TransitionsProperty, null);
            UpdateFrame();
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                _instantPending = false;
                ClearValue(TransitionsProperty);
            }, Avalonia.Threading.DispatcherPriority.Background);
            return;
        }
        UpdateFrame();
    }

    private void UpdateFrame()
    {
        if (!IsAttachedToTree)
        {
            return;
        }
        var border = KanbanTokens.Color(this, ArcDesignTokenKind.Border);
        var accent = KanbanTokens.Color(this, ArcDesignTokenKind.Accent);
        Color frame;
        BoxShadows shadow;
        if (IsDragged)
        {
            frame = Oklch.MixOklab(border, accent, 0.55);
            shadow = KanbanTokens.Shadow(this, ArcDesignTokenKind.ShadowFloating);
        }
        else if (IsLifted)
        {
            frame = accent;
            shadow = KanbanTokens.WithRing(KanbanTokens.Shadow(this, ArcDesignTokenKind.ShadowRaised), Oklch.WithAlpha(accent, 0.1), 3);
        }
        else
        {
            frame = _hovered && !IsLanding ? KanbanTokens.Color(this, ArcDesignTokenKind.BorderStrong) : border;
            shadow = KanbanTokens.Shadow(this, ArcDesignTokenKind.ShadowResting);
        }
        FrameBorderBrush = new ImmutableSolidColorBrush(frame);
        FrameShadow = shadow;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => IsProxy ? new NoneAutomationPeer(this) : new KanbanCardPeer(this);
}

/// <summary>Reports a card as a button with its title, id and owner.</summary>
internal sealed class KanbanCardPeer(KanbanCardView owner) : ControlAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

    protected override string? GetNameCore() => AutomationProperties.GetName(Owner);

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;
}

/// <summary>
/// A column's cell (internal): a rounded muted panel. Over its limit it warms up; the cell a dragged card would land in
/// takes an accent border and tint. The colors are mixed from the theme in code; the theme eases them (160ms).
/// </summary>
internal sealed class KanbanCellView : Border
{
    private bool _overLimit;
    private bool _dropTarget;

    public KanbanCellView()
    {
        BorderThickness = new Thickness(KanbanLayout.CellBorder);
        ActualThemeVariantChanged += (_, _) => UpdateBrushes();
        ResourcesChanged += (_, _) => UpdateBrushes();
    }

    public bool IsOverLimit => _overLimit;

    public bool IsDropTarget => _dropTarget;

    public void SetState(bool overLimit, bool dropTarget)
    {
        if (_overLimit == overLimit && _dropTarget == dropTarget && Background is not null)
        {
            return;
        }
        _overLimit = overLimit;
        _dropTarget = dropTarget;
        UpdateBrushes();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateBrushes();
    }


    private void UpdateBrushes()
    {
        if (TopLevel.GetTopLevel(this) is null)
        {
            return;
        }
        var muted = KanbanTokens.Color(this, ArcDesignTokenKind.SurfaceMuted);
        var accent = KanbanTokens.Color(this, ArcDesignTokenKind.Accent);
        var warning = KanbanTokens.Color(this, ArcDesignTokenKind.Warning);
        var fill = _overLimit ? Oklch.MixOklab(muted, warning, 0.05) : _dropTarget ? Oklch.MixOklab(muted, accent, 0.05) : muted;
        var stroke = _dropTarget ? Oklch.WithAlpha(accent, 0.45) : Colors.Transparent;
        Background = new ImmutableSolidColorBrush(fill);
        BorderBrush = new ImmutableSolidColorBrush(stroke);
    }
}

/// <summary>
/// The slot a dragged card would land in (internal): a dashed accent outline on an accent tint, as tall as the card.
/// When the drop would put its column over the limit it turns amber and says so.
/// </summary>
internal sealed class KanbanSlotView : Control
{
    private static readonly ImmutableDashStyle s_dash = new([3, 3], 0);
    private Pen? _pen;
    private IBrush? _fill;
    private IBrush? _textBrush;
    private Pen? _glyphPen;
    private TextLayout? _layout;
    private string? _text;
    private bool _overLimit;
    private bool _cacheValid;

    public KanbanSlotView()
    {
        IsHitTestVisible = false;
        Focusable = false;
        ActualThemeVariantChanged += (_, _) => OnThemeChanged();
        ResourcesChanged += (_, _) => OnThemeChanged();
    }

    public bool IsOverLimit => _overLimit;

    /// <summary>The over-limit sentence (null while within the limit).</summary>
    public string? Text => _text;

    public void SetState(bool overLimit, string? text)
    {
        if (_overLimit == overLimit && _text == text)
        {
            return;
        }
        _overLimit = overLimit;
        _text = text;
        _layout = null;
        _cacheValid = false;
        InvalidateVisual();
    }

    private void OnThemeChanged()
    {
        _cacheValid = false;
        _layout = null;
        InvalidateVisual();
    }

    private void EnsureCache()
    {
        if (_cacheValid)
        {
            return;
        }
        _cacheValid = true;
        var tone = KanbanTokens.Color(this, _overLimit ? ArcDesignTokenKind.Warning : ArcDesignTokenKind.Accent);
        _pen = new Pen(new ImmutableSolidColorBrush(Oklch.WithAlpha(tone, 0.6)), 1, s_dash);
        _fill = new ImmutableSolidColorBrush(Oklch.WithAlpha(tone, _overLimit ? 0.06 : 0.10));
        _textBrush = new ImmutableSolidColorBrush(tone);
        _glyphPen = new Pen(_textBrush, 2 * 24.0 / 14.0, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
    }

    public override void Render(DrawingContext context)
    {
        if (TopLevel.GetTopLevel(this) is null)
        {
            return;
        }
        EnsureCache();
        var rect = new Rect(Bounds.Size).Deflate(0.5);
        context.DrawRectangle(_fill, _pen, new RoundedRect(rect, 16));
        if (!_overLimit || string.IsNullOrEmpty(_text))
        {
            return;
        }
        _layout ??= new TextLayout(_text, new Typeface(ArcFontsFamily(), FontStyle.Normal, FontWeight.Medium), 12, _textBrush,
            lineHeight: 16.8);
        const double glyph = 14;
        const double gap = 6;
        var width = glyph + gap + _layout.Width;
        var x = (Bounds.Width - width) / 2;
        var y = (Bounds.Height - 16.8) / 2;
        using (context.PushTransform(Matrix.CreateScale(glyph / 24, glyph / 24) * Matrix.CreateTranslation(x, y + (16.8 - glyph) / 2)))
        {
            context.DrawGeometry(null, _glyphPen, ArcIcons.TriangleAlert);
        }
        _layout.Draw(context, new Point(x + glyph + gap, y));
    }

    private FontFamily ArcFontsFamily()
    {
        if (this.TryFindResource(ArcDesignTokenKind.BodyFontFamily, ActualThemeVariant, out var value) && value is FontFamily family)
        {
            return family;
        }
        return FontFamily.Default;
    }
}

/// <summary>
/// The column heads' row layout (internal): the name takes what is left and ends in an ellipsis, then the count, the
/// over-limit chip (which can be squeezed) and the "+" pushed to the right edge, all 8 apart and vertically centered.
/// Children: [0] name, [1] count, [2] chip, [3] add button.
/// </summary>
internal sealed class KanbanHeadPanel : Panel
{
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<KanbanHeadPanel, double>(nameof(Gap), 8);

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        var height = 0.0;
        foreach (var child in Children)
        {
            child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            height = Math.Max(height, child.DesiredSize.Height);
        }
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : Children.Sum(c => c.DesiredSize.Width) + Gap * 3;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count < 4)
        {
            return finalSize;
        }
        var name = Children[0];
        var count = Children[1];
        var chip = Children[2];
        var add = Children[3];
        var addWidth = add.IsVisible ? add.DesiredSize.Width : 0;
        var chipWidth = chip.IsVisible ? chip.DesiredSize.Width : 0;
        var countWidth = count.DesiredSize.Width;
        var available = finalSize.Width - addWidth - (addWidth > 0 ? Gap : 0);
        var nameWidth = Math.Max(0, Math.Min(name.DesiredSize.Width, available - countWidth - Gap - (chipWidth > 0 ? chipWidth + Gap : 0)));
        // A name too long for the column ends in an ellipsis down to 40px; past that the chip gives way.
        if (nameWidth < Math.Min(40, name.DesiredSize.Width))
        {
            nameWidth = Math.Min(40, name.DesiredSize.Width);
        }
        var x = 0.0;
        name.Arrange(Centered(x, nameWidth, name, finalSize.Height));
        x += nameWidth + Gap;
        count.Arrange(Centered(x, countWidth, count, finalSize.Height));
        x += countWidth + Gap;
        var chipRoom = Math.Max(0, Math.Min(chipWidth, available - x));
        chip.Arrange(Centered(x, chipRoom, chip, finalSize.Height));
        add.Arrange(Centered(finalSize.Width - addWidth, addWidth, add, finalSize.Height));
        return finalSize;
    }

    private static Rect Centered(double x, double width, Control child, double height) =>
        new(x, Math.Max(0, (height - child.DesiredSize.Height) / 2), Math.Max(0, width), child.DesiredSize.Height);
}

/// <summary>
/// A column head (internal): the name, the rolling count (with "/limit" when the column has one), the over-limit chip
/// and the "+" that opens quick add. Over the limit the count turns amber.
/// </summary>
[TemplatePart(DigitsPart, typeof(RollingDigits))]
[TemplatePart(LimitPart, typeof(TextBlock))]
[TemplatePart(CountPart, typeof(Control))]
[TemplatePart(AddPart, typeof(AvaloniaButton))]
[PseudoClasses(OverLimitPseudoClass)]
internal sealed class KanbanColumnHead : TemplatedControl
{
    public const string DigitsPart = "PART_Digits";
    public const string LimitPart = "PART_Limit";
    public const string CountPart = "PART_Count";
    public const string AddPart = "PART_Add";
    public const string OverLimitPseudoClass = ":over-limit";

    public static readonly StyledProperty<string?> ColumnNameProperty =
        AvaloniaProperty.Register<KanbanColumnHead, string?>(nameof(ColumnName));

    public static readonly StyledProperty<bool> CanAddProperty =
        AvaloniaProperty.Register<KanbanColumnHead, bool>(nameof(CanAdd), true);

    public static readonly StyledProperty<bool> IsOverLimitProperty =
        AvaloniaProperty.Register<KanbanColumnHead, bool>(nameof(IsOverLimit));

    public static readonly StyledProperty<string?> ChipTextProperty =
        AvaloniaProperty.Register<KanbanColumnHead, string?>(nameof(ChipText));

    private RollingDigits? _digits;
    private TextBlock? _limitText;
    private Control? _count;
    private AvaloniaButton? _add;
    private int _countValue;
    private int? _limit;
    private string? _countName;
    private string? _addName;

    public KanbanColumnHead(int index)
    {
        Index = index;
    }

    public int Index { get; set; }

    public string? ColumnName { get => GetValue(ColumnNameProperty); set => SetValue(ColumnNameProperty, value); }

    public bool CanAdd { get => GetValue(CanAddProperty); set => SetValue(CanAddProperty, value); }

    public bool IsOverLimit { get => GetValue(IsOverLimitProperty); set => SetValue(IsOverLimitProperty, value); }

    public string? ChipText { get => GetValue(ChipTextProperty); set => SetValue(ChipTextProperty, value); }

    /// <summary>The count shown right now (live while dragging).</summary>
    public int Count => _countValue;

    /// <summary>The "+" button.</summary>
    public AvaloniaButton? AddButton => _add;

    /// <summary>Raised when the "+" is pressed.</summary>
    public event Action<KanbanColumnHead>? AddRequested;

    public void SetCount(int count, int? limit, string countName, string addName)
    {
        _countValue = count;
        _limit = limit;
        _countName = countName;
        _addName = addName;
        IsOverLimit = KanbanLayout.IsOverLimit(count, limit);
        Apply();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_add is not null)
        {
            _add.Click -= OnAddClick;
        }
        _digits = e.NameScope.Find<RollingDigits>(DigitsPart);
        _limitText = e.NameScope.Find<TextBlock>(LimitPart);
        _count = e.NameScope.Find<Control>(CountPart);
        _add = e.NameScope.Find<AvaloniaButton>(AddPart);
        if (_add is not null)
        {
            _add.Click += OnAddClick;
        }
        Apply();
    }

    private void OnAddClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        AddRequested?.Invoke(this);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOverLimitProperty)
        {
            PseudoClasses.Set(OverLimitPseudoClass, IsOverLimit);
        }
    }

    private void Apply()
    {
        if (_digits is not null)
        {
            _digits.Value = _countValue;
        }
        if (_limitText is not null)
        {
            _limitText.Text = _limit is { } l ? "/" + l.ToString(CultureInfo.InvariantCulture) : null;
            _limitText.IsVisible = _limit is not null;
        }
        if (_count is not null && _countName is not null)
        {
            AutomationProperties.SetName(_count, _countName);
        }
        if (_add is not null && _addName is not null)
        {
            AutomationProperties.SetName(_add, _addName);
        }
    }
}

/// <summary>A swimlane's head (internal): the person's avatar and name and the lane's rolling count.</summary>
[TemplatePart(DigitsPart, typeof(RollingDigits))]
[PseudoClasses(UnassignedPseudoClass)]
internal sealed class KanbanLaneHead : TemplatedControl
{
    public const string DigitsPart = "PART_Digits";
    public const string UnassignedPseudoClass = ":unassigned";

    public static readonly StyledProperty<ProjectPerson?> PersonProperty =
        AvaloniaProperty.Register<KanbanLaneHead, ProjectPerson?>(nameof(Person));

    public static readonly StyledProperty<string?> PersonNameProperty =
        AvaloniaProperty.Register<KanbanLaneHead, string?>(nameof(PersonName));

    private RollingDigits? _digits;
    private int _count;

    public ProjectPerson? Person { get => GetValue(PersonProperty); set => SetValue(PersonProperty, value); }

    public string? PersonName { get => GetValue(PersonNameProperty); set => SetValue(PersonNameProperty, value); }

    public int Count => _count;

    public void Set(KanbanLane lane, int count, KanbanBoardLabels strings)
    {
        Person = lane.Person;
        PersonName = lane.IsUnassigned ? strings.Unassigned : lane.Person?.Name;
        PseudoClasses.Set(UnassignedPseudoClass, lane.IsUnassigned);
        _count = count;
        if (_digits is not null)
        {
            _digits.Value = count;
        }
        AutomationProperties.SetName(this, strings.LaneName(PersonName ?? string.Empty) + ", " + strings.CardCount(count));
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _digits = e.NameScope.Find<RollingDigits>(DigitsPart);
        if (_digits is not null)
        {
            _digits.Value = _count;
        }
    }
}

/// <summary>The Add card row under a Status column's cards (internal).</summary>
internal sealed class KanbanAddRow : AvaloniaButton
{
    public KanbanAddRow(int column)
    {
        Column = column;
    }

    public int Column { get; set; }
}

/// <summary>The round icon button of a column head and of the drawer (internal).</summary>
internal sealed class KanbanIconButton : AvaloniaButton
{
    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<KanbanIconButton, Geometry?>(nameof(Glyph));

    public static readonly StyledProperty<double> GlyphSizeProperty =
        AvaloniaProperty.Register<KanbanIconButton, double>(nameof(GlyphSize), 15);

    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    public double GlyphSize { get => GetValue(GlyphSizeProperty); set => SetValue(GlyphSizeProperty, value); }
}

/// <summary>A pill button of the composer and the drawer (internal): Primary (Accent) or quiet text.</summary>
internal sealed class KanbanPillButton : AvaloniaButton
{
}

/// <summary>
/// The quick add composer (internal): a two-line box and Add card / Cancel. Enter adds and keeps it open for the next
/// card, Shift+Enter breaks the line, Escape cancels. The panel arranges it at its animated height; its content keeps
/// its full height and is clipped.
/// </summary>
[TemplatePart(BoxPart, typeof(TextBox))]
[TemplatePart(SubmitPart, typeof(AvaloniaButton))]
[TemplatePart(CancelPart, typeof(AvaloniaButton))]
internal sealed class KanbanComposer : TemplatedControl
{
    public const string BoxPart = "PART_Box";
    public const string SubmitPart = "PART_Submit";
    public const string CancelPart = "PART_Cancel";

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<KanbanComposer, string?>(nameof(Text));

    public static readonly StyledProperty<string?> BoxNameProperty =
        AvaloniaProperty.Register<KanbanComposer, string?>(nameof(BoxName));

    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<KanbanComposer, string?>(nameof(Placeholder));

    public static readonly StyledProperty<string?> SubmitTextProperty =
        AvaloniaProperty.Register<KanbanComposer, string?>(nameof(SubmitText));

    public static readonly StyledProperty<string?> CancelTextProperty =
        AvaloniaProperty.Register<KanbanComposer, string?>(nameof(CancelText));

    public static readonly StyledProperty<bool> CanSubmitProperty =
        AvaloniaProperty.Register<KanbanComposer, bool>(nameof(CanSubmit));

    private TextBox? _box;
    private AvaloniaButton? _submit;
    private AvaloniaButton? _cancel;

    public KanbanComposer()
    {
        ClipToBounds = true;
        IsVisible = false;
    }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public string? BoxName { get => GetValue(BoxNameProperty); set => SetValue(BoxNameProperty, value); }

    public string? Placeholder { get => GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }

    public string? SubmitText { get => GetValue(SubmitTextProperty); set => SetValue(SubmitTextProperty, value); }

    public string? CancelText { get => GetValue(CancelTextProperty); set => SetValue(CancelTextProperty, value); }

    public bool CanSubmit { get => GetValue(CanSubmitProperty); set => SetValue(CanSubmitProperty, value); }

    public TextBox? Box => _box;

    /// <summary>The cell the composer is open in (lane, column), or (−1, −1).</summary>
    public (int Lane, int Column) Cell { get; set; } = (-1, -1);

    /// <summary>The full height of the open composer (box + buttons).</summary>
    public double FullHeight => DesiredSize.Height;

    /// <summary>Raised on Enter or the Add card button with the typed text.</summary>
    public event Action<string>? Submitted;

    /// <summary>Raised on Escape or Cancel.</summary>
    public event Action? Cancelled;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_box is not null)
        {
            _box.RemoveHandler(KeyDownEvent, OnBoxKeyDown);
        }
        if (_submit is not null)
        {
            _submit.Click -= OnSubmitClick;
        }
        if (_cancel is not null)
        {
            _cancel.Click -= OnCancelClick;
        }
        _box = e.NameScope.Find<TextBox>(BoxPart);
        _submit = e.NameScope.Find<AvaloniaButton>(SubmitPart);
        _cancel = e.NameScope.Find<AvaloniaButton>(CancelPart);
        // Tunnel: Enter must submit before the text box turns it into a line break.
        _box?.AddHandler(KeyDownEvent, OnBoxKeyDown, RoutingStrategies.Tunnel);
        if (_submit is not null)
        {
            _submit.Click += OnSubmitClick;
        }
        if (_cancel is not null)
        {
            _cancel.Click += OnCancelClick;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            CanSubmit = !string.IsNullOrWhiteSpace(Text);
        }
        else if (change.Property == BoxNameProperty && _box is not null)
        {
            AutomationProperties.SetName(_box, BoxName);
        }
    }

    private void OnBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && (e.KeyModifiers & KeyModifiers.Shift) == 0)
        {
            e.Handled = true;
            Submitted?.Invoke(CurrentText);
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Cancelled?.Invoke();
        }
    }

    private void OnSubmitClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Submitted?.Invoke(CurrentText);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Cancelled?.Invoke();
    }

    /// <summary>Puts the box and the two buttons at <paramref name="first"/>, +1, +2 in the board's tab order.</summary>
    public void SetTabIndices(int first)
    {
        ApplyTemplate();
        if (_box is not null)
        {
            KeyboardNavigation.SetTabIndex(_box, first);
        }
        if (_submit is not null)
        {
            KeyboardNavigation.SetTabIndex(_submit, first + 1);
        }
        if (_cancel is not null)
        {
            KeyboardNavigation.SetTabIndex(_cancel, first + 2);
        }
    }

    /// <summary>Empties the box (the text box's own value too: typing sets it, not the template binding).</summary>
    public void Clear()
    {
        SetCurrentValue(TextProperty, string.Empty);
        _box?.SetCurrentValue(TextBox.TextProperty, string.Empty);
    }

    /// <summary>The text as typed.</summary>
    public string CurrentText => _box?.Text ?? Text ?? string.Empty;

    public void FocusBox()
    {
        if (_box is null)
        {
            ApplyTemplate();
        }
        _box?.Focus(NavigationMethod.Tab);
        if (_box is not null && _box.Text is { } text)
        {
            _box.CaretIndex = text.Length;
        }
        if (_box is not null)
        {
            AutomationProperties.SetName(_box, BoxName);
        }
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        // The panel gives the animated height; the form keeps its full height and the rest is clipped.
        var child = VisualChildren.Count > 0 ? VisualChildren[0] as Layoutable : null;
        if (child is not null)
        {
            child.Arrange(new Rect(0, 0, finalSize.Width, Math.Max(finalSize.Height, child.DesiredSize.Height)));
        }
        return finalSize;
    }
}

/// <summary>A checklist step in the drawer (internal): an 18px box (radius 6) 12 from its text, rows 33.6 high.</summary>
internal sealed class KanbanStepBox : CheckBox
{
}

/// <summary>One option of a <see cref="KanbanSegmented"/> (internal): a radio button with a pill focus ring.</summary>
internal sealed class KanbanSegmentOption : RadioButton
{
    public KanbanSegmentOption(int index)
    {
        Index = index;
    }

    public int Index { get; }
}

/// <summary>Lays out segmented options in one row (content widths) or in a grid of equal columns.</summary>
internal sealed class KanbanSegmentedPanel : Panel
{
    public static readonly StyledProperty<int> ColumnsProperty =
        AvaloniaProperty.Register<KanbanSegmentedPanel, int>(nameof(Columns));

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<KanbanSegmentedPanel, double>(nameof(Gap));

    public static readonly StyledProperty<bool> FillProperty =
        AvaloniaProperty.Register<KanbanSegmentedPanel, bool>(nameof(Fill));

    static KanbanSegmentedPanel()
    {
        AffectsMeasure<KanbanSegmentedPanel>(ColumnsProperty, GapProperty, FillProperty);
    }

    /// <summary>0: one row; 2: two by two (equal cells).</summary>
    public int Columns { get => GetValue(ColumnsProperty); set => SetValue(ColumnsProperty, value); }

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    /// <summary>One row stretched to the full width with equal options.</summary>
    public bool Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = 0.0;
        var height = 0.0;
        foreach (var child in Children)
        {
            child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            width += child.DesiredSize.Width;
            height = Math.Max(height, child.DesiredSize.Height);
        }
        var n = Children.Count;
        if (Columns > 1 && n > 0)
        {
            var rows = (n + Columns - 1) / Columns;
            var w = double.IsFinite(availableSize.Width) ? availableSize.Width : width;
            return new Size(w, rows * height + (rows - 1) * Gap);
        }
        width += Math.Max(0, n - 1) * Gap;
        if (Fill && double.IsFinite(availableSize.Width))
        {
            width = Math.Max(width, availableSize.Width);
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var n = Children.Count;
        if (n == 0)
        {
            return finalSize;
        }
        var height = Children.Max(c => c.DesiredSize.Height);
        if (Columns > 1)
        {
            var cellWidth = (finalSize.Width - (Columns - 1) * Gap) / Columns;
            for (var i = 0; i < n; i++)
            {
                var row = i / Columns;
                var column = i % Columns;
                Children[i].Arrange(new Rect(column * (cellWidth + Gap), row * (height + Gap), cellWidth, height));
            }
            return finalSize;
        }
        if (Fill)
        {
            // Every option keeps its text width and gets an equal share of the room left (CSS flex: 1 1 auto).
            var natural = Children.Sum(c => c.DesiredSize.Width) + (n - 1) * Gap;
            var extra = Math.Max(0, finalSize.Width - natural) / n;
            var left = 0.0;
            foreach (var child in Children)
            {
                var w = child.DesiredSize.Width + extra;
                child.Arrange(new Rect(left, 0, w, height));
                left += w + Gap;
            }
            return finalSize;
        }
        var x = 0.0;
        foreach (var child in Children)
        {
            child.Arrange(new Rect(x, 0, child.DesiredSize.Width, height));
            x += child.DesiredSize.Width + Gap;
        }
        return finalSize;
    }
}

/// <summary>
/// A segmented choice (internal; the board's view switch and the drawer's status): a muted rounded track with a raised
/// pill gliding behind the chosen option on the layout spring. One tab stop (the chosen option); the arrow keys move
/// the choice and wrap, as an APG radio group.
/// </summary>
[TemplatePart(PillPart, typeof(GlidingIndicator))]
[TemplatePart(OptionsPart, typeof(KanbanSegmentedPanel))]
internal sealed class KanbanSegmented : TemplatedControl
{
    public const string PillPart = "PART_Pill";
    public const string OptionsPart = "PART_Options";

    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<KanbanSegmented, int>(nameof(SelectedIndex), -1);

    public static readonly StyledProperty<int> ColumnsProperty =
        AvaloniaProperty.Register<KanbanSegmented, int>(nameof(Columns));

    public static readonly StyledProperty<double> OptionHeightProperty =
        AvaloniaProperty.Register<KanbanSegmented, double>(nameof(OptionHeight), 28);

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<KanbanSegmented, double>(nameof(Gap));

    public static readonly StyledProperty<bool> FillProperty =
        AvaloniaProperty.Register<KanbanSegmented, bool>(nameof(Fill));

    private readonly List<KanbanSegmentOption> _options = new();
    private GlidingIndicator? _pill;
    private KanbanSegmentedPanel? _panel;
    private bool _placed;
    private bool _syncing;

    public int SelectedIndex { get => GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }

    public int Columns { get => GetValue(ColumnsProperty); set => SetValue(ColumnsProperty, value); }

    public double OptionHeight { get => GetValue(OptionHeightProperty); set => SetValue(OptionHeightProperty, value); }

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    public bool Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }

    public IReadOnlyList<KanbanSegmentOption> Options => _options;

    /// <summary>Raised when the user picks another option (not when <see cref="SelectedIndex"/> is set in code).</summary>
    public event Action<int>? SelectionChanged;

    public void SetOptions(IReadOnlyList<string> labels)
    {
        while (_options.Count > labels.Count)
        {
            var last = _options[^1];
            last.IsCheckedChanged -= OnOptionChecked;
            _panel?.Children.Remove(last);
            _options.RemoveAt(_options.Count - 1);
        }
        while (_options.Count < labels.Count)
        {
            var option = new KanbanSegmentOption(_options.Count);
            option.IsCheckedChanged += OnOptionChecked;
            _options.Add(option);
            _panel?.Children.Add(option);
        }
        for (var i = 0; i < labels.Count; i++)
        {
            _options[i].Content = labels[i];
            AutomationProperties.SetName(_options[i], labels[i]);
            _options[i].Height = OptionHeight;
        }
        SyncChecks();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _panel?.Children.Clear();
        if (_panel is not null)
        {
            _panel.LayoutUpdated -= OnPanelLayoutUpdated;
        }
        _pill = e.NameScope.Find<GlidingIndicator>(PillPart);
        _panel = e.NameScope.Find<KanbanSegmentedPanel>(OptionsPart);
        _placed = false;
        if (_panel is not null)
        {
            _panel.Children.AddRange(_options);
            _panel.LayoutUpdated += OnPanelLayoutUpdated;
        }
    }

    private void OnPanelLayoutUpdated(object? sender, EventArgs e) => PlacePill();

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedIndexProperty)
        {
            SyncChecks();
            PlacePill();
        }
        else if (change.Property == OptionHeightProperty)
        {
            foreach (var option in _options)
            {
                option.Height = OptionHeight;
            }
        }
    }

    private void SyncChecks()
    {
        _syncing = true;
        try
        {
            for (var i = 0; i < _options.Count; i++)
            {
                var on = i == SelectedIndex;
                _options[i].IsChecked = on;
                _options[i].IsTabStop = on || (SelectedIndex < 0 && i == 0);
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private void OnOptionChecked(object? sender, RoutedEventArgs e)
    {
        if (_syncing || sender is not KanbanSegmentOption { IsChecked: true } option)
        {
            return;
        }
        Choose(option.Index, focus: false);
    }

    private void Choose(int index, bool focus)
    {
        if (index < 0 || index >= _options.Count)
        {
            return;
        }
        var changed = index != SelectedIndex;
        SetCurrentValue(SelectedIndexProperty, index);
        SyncChecks();
        if (focus)
        {
            _options[index].Focus(NavigationMethod.Directional);
        }
        if (changed)
        {
            SelectionChanged?.Invoke(index);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!e.Handled && _options.Count > 0)
        {
            var step = e.Key switch
            {
                Key.Left or Key.Up => -1,
                Key.Right or Key.Down => 1,
                _ => 0
            };
            if (step != 0)
            {
                var from = SelectedIndex < 0 ? 0 : SelectedIndex;
                Choose((from + step + _options.Count) % _options.Count, focus: true);
                e.Handled = true;
                return;
            }
        }
        base.OnKeyDown(e);
    }

    private void PlacePill()
    {
        if (_pill is null || _panel is null || SelectedIndex < 0 || SelectedIndex >= _options.Count)
        {
            return;
        }
        var option = _options[SelectedIndex];
        if (option.Bounds.Width <= 0)
        {
            return;
        }
        var rect = option.Bounds;
        if (_placed && rect == _placedRect && SelectedIndex == _placedIndex)
        {
            return;
        }
        // The pill glides on the layout spring (Arc: K147–164 / C24–26, Smooth) when the choice changes; the first
        // placement and resizes land in place.
        var animate = _placed && SelectedIndex != _placedIndex;
        _pill.MoveTo(rect, KanbanDragPhysics.Layout, animate);
        _placed = true;
        _placedRect = rect;
        _placedIndex = SelectedIndex;
    }

    private Rect _placedRect;
    private int _placedIndex = -1;
}
