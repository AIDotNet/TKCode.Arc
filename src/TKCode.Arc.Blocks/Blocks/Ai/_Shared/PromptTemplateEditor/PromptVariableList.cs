using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using Avalonia.Utilities;

namespace TKCode.Arc;

/// <summary>
/// The list "{{" opens in a <see cref="PromptTemplateEditor"/> (Arc's variable listbox): a surface 240 wide (padding 4,
/// 1px border, radius 16, Surface; the floating shadow comes from the popup) with one 34px row per matching variable
/// (a 7px dot in its series color and the name in its chip text color) and, for a new name, a last "+ Add name" row
/// (Plus 14 in TextSecondary, the text in Foreground with the name medium). The active row has a SurfaceMuted fill
/// (radius 12) that jumps, as in Arc. The editor keeps focus: it routes the keys here (<see cref="TryHandleKey"/>);
/// hovering a row makes it active and a click accepts it without moving focus.
/// </summary>
[TemplatePart(ScrollPart, typeof(ScrollViewer))]
[TemplatePart(RowsPart, typeof(StackPanel))]
internal class PromptVariablePop : TemplatedControl
{
    public const string ScrollPart = "PART_Scroll";
    public const string RowsPart = "PART_Rows";

    /// <summary>Row height (Arc 34).</summary>
    public const double RowHeight = 34;

    public static readonly StyledProperty<IBrush?> ActiveBackgroundProperty =
        AvaloniaProperty.Register<PromptVariablePop, IBrush?>(nameof(ActiveBackground));

    public static readonly StyledProperty<IBrush?> IconForegroundProperty =
        AvaloniaProperty.Register<PromptVariablePop, IBrush?>(nameof(IconForeground));

    /// <summary>How many rows show before the list scrolls.</summary>
    public static readonly StyledProperty<int> MaxVisibleRowsProperty =
        AvaloniaProperty.Register<PromptVariablePop, int>(nameof(MaxVisibleRows), 7);

    private readonly List<PromptVariableRowView> _views = new();
    private IReadOnlyList<PromptVariableRow> _rows = Array.Empty<PromptVariableRow>();
    private ScrollViewer? _scroll;
    private StackPanel? _panel;
    private int _active = -1;
    private int _announcedCount = -1;

    static PromptVariablePop()
    {
        FocusableProperty.OverrideDefaultValue<PromptVariablePop>(false);
        IsTabStopProperty.OverrideDefaultValue<PromptVariablePop>(false);
        AffectsMeasure<PromptVariablePop>(MaxVisibleRowsProperty);
    }

    public IBrush? ActiveBackground { get => GetValue(ActiveBackgroundProperty); set => SetValue(ActiveBackgroundProperty, value); }

    public IBrush? IconForeground { get => GetValue(IconForegroundProperty); set => SetValue(IconForegroundProperty, value); }

    public int MaxVisibleRows { get => GetValue(MaxVisibleRowsProperty); set => SetValue(MaxVisibleRowsProperty, value); }

    /// <summary>The editor whose variables are listed (tones, colors and the "+ Add" text come from it).</summary>
    internal PromptTemplateEditor? Owner { get; set; }

    /// <summary>The shown rows.</summary>
    public IReadOnlyList<PromptVariableRow> Rows => _rows;

    public int ActiveIndex => _active;

    public PromptVariableRow? ActiveRow => _active >= 0 && _active < _rows.Count ? _rows[_active] : null;

    /// <summary>What assistive technology reads for the active row (the row's text).</summary>
    public string? ActiveRowName => ActiveRow is { } row ? RowText(row) : null;

    /// <summary>
    /// Turns the number of rows into a polite announcement ("3 variables"); called when rows show after the list was
    /// closed or empty and when their number changes. The owner says it and returns it; null announces nothing.
    /// </summary>
    public Func<int, string?>? AnnounceCount { get; set; }

    /// <summary>The text <see cref="AnnounceCount"/> returned last.</summary>
    public string? LastAnnouncement { get; private set; }

    internal IReadOnlyList<PromptVariableRowView> RowViews => _views;

    internal ScrollViewer? Scroll => _scroll;

    /// <summary>A row was accepted (Enter, Tab or a click).</summary>
    public event EventHandler<PromptVariableRow>? Accepted;

    /// <summary>The active row changed.</summary>
    public event EventHandler? ActiveChanged;

    /// <summary>The text a row shows: the name, or the owner's "+ Add" text.</summary>
    internal string RowText(PromptVariableRow row) => row.IsAdd ? Owner?.AddVariableText?.Invoke(row.Name) ?? row.Name : row.Name;

    /// <summary>
    /// Shows <paramref name="rows"/> with the first one active (the same rows again keep the active row). False when
    /// there are none.
    /// </summary>
    public bool Show(IReadOnlyList<PromptVariableRow> rows)
    {
        if (rows.Count == 0)
        {
            _rows = Array.Empty<PromptVariableRow>();
            _active = -1;
            _announcedCount = -1;
            Sync();
            ActiveChanged?.Invoke(this, EventArgs.Empty);
            return false;
        }
        if (Same(_rows, rows))
        {
            return true;
        }
        _rows = rows;
        _active = 0;
        Sync();
        if (_scroll is not null)
        {
            _scroll.Offset = default;
        }
        if (rows.Count != _announcedCount)
        {
            _announcedCount = rows.Count;
            LastAnnouncement = AnnounceCount?.Invoke(rows.Count);
        }
        ActiveChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>The list closed: the next rows are announced again.</summary>
    public void OnHidden() => _announcedCount = -1;

    /// <summary>Up and Down move the active row (wrapping); Enter and Tab accept it. True when handled.</summary>
    public bool TryHandleKey(Key key, KeyModifiers modifiers)
    {
        if (_rows.Count == 0 || modifiers != KeyModifiers.None)
        {
            return false;
        }
        switch (key)
        {
            case Key.Up:
                MoveActive(-1);
                return true;
            case Key.Down:
                MoveActive(1);
                return true;
            case Key.Enter or Key.Tab:
                return AcceptActive();
            default:
                return false;
        }
    }

    /// <summary>Moves the active row by <paramref name="delta"/>, wrapping, and scrolls it into view.</summary>
    public void MoveActive(int delta)
    {
        var count = _rows.Count;
        if (count == 0)
        {
            return;
        }
        var next = ((_active < 0 ? (delta > 0 ? -1 : 0) : _active) + delta) % count;
        SetActive(next < 0 ? next + count : next);
        if (_scroll is { } scroll)
        {
            // Rows are a fixed height: the active one's place is known without a layout pass.
            var top = Padding.Top + _active * RowHeight;
            var bottom = top + RowHeight;
            var viewport = scroll.Viewport.Height;
            if (viewport > 0)
            {
                if (_active == 0)
                {
                    scroll.Offset = default;
                }
                else if (_active == count - 1)
                {
                    scroll.Offset = new Vector(0, Math.Max(0, scroll.Extent.Height - viewport));
                }
                else if (top < scroll.Offset.Y)
                {
                    scroll.Offset = new Vector(0, top);
                }
                else if (bottom > scroll.Offset.Y + viewport)
                {
                    scroll.Offset = new Vector(0, bottom - viewport);
                }
            }
        }
    }

    public void SetActive(int index)
    {
        if (index < 0 || index >= _rows.Count || index == _active)
        {
            return;
        }
        _active = index;
        for (var i = 0; i < _views.Count; i++)
        {
            _views[i].SetActive(i == _active);
        }
        ActiveChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool AcceptActive()
    {
        if (ActiveRow is not { } row)
        {
            return false;
        }
        Accepted?.Invoke(this, row);
        return true;
    }

    internal void AcceptAt(int index)
    {
        if (index < 0 || index >= _rows.Count)
        {
            return;
        }
        SetActive(index);
        AcceptActive();
    }

    /// <summary>Colors or texts of the owner changed: the rows draw again.</summary>
    internal void InvalidateRows()
    {
        foreach (var view in _views)
        {
            view.InvalidateText();
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _panel?.Children.Clear();
        _scroll = e.NameScope.Find<ScrollViewer>(ScrollPart);
        _panel = e.NameScope.Find<StackPanel>(RowsPart);
        if (_panel is not null)
        {
            foreach (var view in _views)
            {
                _panel.Children.Add(view);
            }
        }
        UpdateMaxHeight();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MaxVisibleRowsProperty || change.Property == PaddingProperty)
        {
            UpdateMaxHeight();
        }
        else if (change.Property == ActiveBackgroundProperty || change.Property == IconForegroundProperty ||
                 change.Property == ForegroundProperty || change.Property == FontSizeProperty || change.Property == FontFamilyProperty)
        {
            InvalidateRows();
        }
    }

    private void UpdateMaxHeight()
    {
        if (_scroll is not null)
        {
            // The padding scrolls with the rows.
            _scroll.MaxHeight = Math.Max(1, MaxVisibleRows) * RowHeight + Padding.Top + Padding.Bottom;
        }
    }

    /// <summary>One pooled row view per row.</summary>
    private void Sync()
    {
        while (_views.Count < _rows.Count)
        {
            var view = new PromptVariableRowView(this) { Index = _views.Count };
            _views.Add(view);
            _panel?.Children.Add(view);
        }
        for (var i = 0; i < _views.Count; i++)
        {
            var view = _views[i];
            var used = i < _rows.Count;
            view.IsVisible = used;
            if (used)
            {
                view.SetRow(_rows[i], i == _active);
            }
        }
    }

    private static bool Same(IReadOnlyList<PromptVariableRow> a, IReadOnlyList<PromptVariableRow> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }
        for (var i = 0; i < a.Count; i++)
        {
            if (a[i] != b[i])
            {
                return false;
            }
        }
        return true;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new PopPeer(this);

    private sealed class PopPeer(PromptVariablePop owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;
    }
}

/// <summary>
/// One row of a <see cref="PromptVariablePop"/>, drawn in <see cref="Render"/>: the active fill, then a dot and the name
/// or the Plus icon and the "+ Add" text. Hover makes it active (not for touch); a click accepts it; a press never moves
/// focus, so the editor keeps the caret.
/// </summary>
internal sealed class PromptVariableRowView : Control
{
    private const double PaddingX = 10;
    private const double Gap = 8;
    private const double DotSize = 7;
    private const double IconSize = 14;
    private const double ActiveRadius = 12;

    private readonly PromptVariablePop _pop;
    private PromptVariableRow _row;
    private bool _active;
    private bool _pressed;
    private TextLayout? _text;
    private Pen? _iconPen;
    private IBrush? _iconBrush;
    private ImmutableSolidColorBrush? _dotBrush;

    public PromptVariableRowView(PromptVariablePop pop)
    {
        _pop = pop;
        Focusable = false;
        Height = PromptVariablePop.RowHeight;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    internal int Index { get; init; }

    internal PromptVariableRow Row => _row;

    internal bool IsActive => _active;

    /// <summary>The row's text layout (tests).</summary>
    internal TextLayout? TextLayout => _text;

    internal void SetRow(PromptVariableRow row, bool active)
    {
        if (row != _row)
        {
            _row = row;
            InvalidateText();
        }
        SetActive(active);
    }

    internal void SetActive(bool active)
    {
        if (_active != active)
        {
            _active = active;
            InvalidateVisual();
        }
    }

    internal void InvalidateText()
    {
        _text?.Dispose();
        _text = null;
        _iconPen = null;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        // Hit testing covers the whole row.
        context.FillRectangle(Brushes.Transparent, bounds);
        if (_active && _pop.ActiveBackground is { } fill)
        {
            context.DrawRectangle(fill, null, bounds, ActiveRadius, ActiveRadius);
        }
        var owner = _pop.Owner;
        double x;
        if (_row.IsAdd)
        {
            DrawPlus(context, new Rect(PaddingX, (bounds.Height - IconSize) / 2, IconSize, IconSize));
            x = PaddingX + IconSize + Gap;
        }
        else
        {
            var dot = owner is null ? Colors.Gray : owner.SeriesColorOf(owner.ToneOf(_row.Name));
            if (_dotBrush is null || _dotBrush.Color != dot)
            {
                _dotBrush = new ImmutableSolidColorBrush(dot);
            }
            context.DrawEllipse(_dotBrush, null, new Rect(PaddingX, (bounds.Height - DotSize) / 2, DotSize, DotSize));
            x = PaddingX + DotSize + Gap;
        }
        var layout = EnsureText(Math.Max(0, bounds.Width - x - PaddingX));
        layout.Draw(context, new Point(x, (bounds.Height - layout.Height) / 2));
    }

    private TextLayout EnsureText(double maxWidth)
    {
        if (_text is not null && Math.Abs(_text.MaxWidth - maxWidth) < 0.01)
        {
            return _text;
        }
        _text?.Dispose();
        var owner = _pop.Owner;
        var typeface = new Typeface(_pop.FontFamily, FontStyle.Normal, FontWeight.Normal);
        var size = _pop.FontSize;
        var text = _pop.RowText(_row);
        IBrush? brush;
        IReadOnlyList<ValueSpan<TextRunProperties>>? spans = null;
        if (_row.IsAdd)
        {
            brush = _pop.Foreground;
            // The name inside the owner's text is medium ("Add" regular).
            var at = text.LastIndexOf(_row.Name, StringComparison.Ordinal);
            if (at >= 0)
            {
                var medium = new Typeface(_pop.FontFamily, FontStyle.Normal, FontWeight.Medium);
                spans = [new ValueSpan<TextRunProperties>(at, _row.Name.Length,
                    new GenericTextRunProperties(medium, size, null, brush, null, BaselineAlignment.Baseline, null, null))];
            }
        }
        else
        {
            brush = owner is null ? _pop.Foreground : new ImmutableSolidColorBrush(owner.ChipTextColor(_row.Name));
        }
        _text = new TextLayout(text, typeface, size, brush, TextAlignment.Left, TextWrapping.NoWrap, TextTrimming.CharacterEllipsis,
            null, FlowDirection.LeftToRight, maxWidth, double.PositiveInfinity, size * 1.4, size * -0.01, 0, null, spans);
        return _text;
    }

    private void DrawPlus(DrawingContext context, Rect box)
    {
        var brush = _pop.IconForeground;
        if (brush is null)
        {
            return;
        }
        if (_iconPen is null || !ReferenceEquals(_iconBrush, brush))
        {
            // Lucide Plus, stroke-width 2 on its 24 box, drawn at 14 (the stroke scales with it).
            _iconPen = new Pen(brush, 2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            _iconBrush = brush;
        }
        var scale = box.Width / 24;
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(box.X, box.Y)))
        {
            context.DrawGeometry(null, _iconPen, ArcIcons.Plus);
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (e.Pointer.Type != PointerType.Touch)
        {
            _pop.SetActive(Index);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        // Nothing in the list takes focus: the editor keeps the caret and its query.
        _pressed = e.GetCurrentPoint(this).Properties.IsLeftButtonPressed;
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var pressed = _pressed;
        _pressed = false;
        if (pressed && e.InitialPressMouseButton == MouseButton.Left && new Rect(Bounds.Size).Contains(e.GetPosition(this)))
        {
            e.Handled = true;
            _pop.AcceptAt(Index);
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pressed = false;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new RowPeer(this);

    private sealed class RowPeer(PromptVariableRowView owner) : ControlAutomationPeer(owner), ISelectionItemProvider, IInvokeProvider
    {
        private PromptVariableRowView Row => (PromptVariableRowView)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

        protected override string? GetNameCore() => Row._pop.RowText(Row._row);

        public bool IsSelected => Row._active;

        public ISelectionProvider? SelectionContainer => null;

        public void AddToSelection() => Row._pop.SetActive(Row.Index);

        public void RemoveFromSelection()
        {
        }

        public void Select() => Row._pop.SetActive(Row.Index);

        public void Invoke() => Row._pop.AcceptAt(Row.Index);
    }
}

/// <summary>
/// The Core <see cref="ArcPopup"/> of the "{{" list: hung below the caret's line, its left edge 10px left of the caret
/// (Arc), 6px away; never light-dismissed and never focused (the editor closes it). It enters with ArcPopup's motion
/// set to Arc's values (opacity and 3px of travel, 160ms enter curve) and closes at once, as Arc's list does.
/// </summary>
internal sealed class PromptVariablePopup : ArcPopup
{
    /// <summary>Arc: the list's left edge is the caret's x − 10.</summary>
    public const double CaretShift = -10;

    static PromptVariablePopup()
    {
        MarginToAnchorProperty.OverrideDefaultValue<PromptVariablePopup>(6);
        IsLightDismissEnabledProperty.OverrideDefaultValue<PromptVariablePopup>(false);
        EnterOffsetProperty.OverrideDefaultValue<PromptVariablePopup>(3);
        EnterScaleProperty.OverrideDefaultValue<PromptVariablePopup>(1);
    }

    public PromptVariablePopup()
    {
        TakesFocusFromNativeControl = false;
        WindowManagerAddShadowHint = false;
        Placement = PlacementMode.BottomEdgeAlignedLeft;
    }

    /// <summary>Hangs the list below the line at <paramref name="caret"/> (the caret's line box, in the target's coordinates).</summary>
    public void Place(Rect caret)
    {
        var anchor = new Rect(caret.X + CaretShift, caret.Y, 1, Math.Max(1, caret.Height));
        if (PlacementRect == anchor)
        {
            return;
        }
        PlacementRect = anchor;
        if (IsOpen)
        {
            // Offset changes are the public way to make the popup run its placement again.
            var offset = HorizontalOffset;
            HorizontalOffset = offset + 1;
            HorizontalOffset = offset;
        }
    }

    /// <summary>Closes without the exit motion (Arc's list disappears at once).</summary>
    public void CloseNow()
    {
        if (!IsOpen)
        {
            return;
        }
        IsMotionEnabled = false;
        IsOpen = false;
    }

    protected override void OnPlacementResolved(CustomPopupPlacement placement, Thickness shadow, double marginToAnchor,
                                                bool isOverlayHost, bool flipX, bool flipY)
    {
        // Slide along the line to stay in the window; flip above the line only when there is no room below.
        placement.ConstraintAdjustment = PopupPositionerConstraintAdjustment.SlideX | PopupPositionerConstraintAdjustment.FlipY;
    }
}
