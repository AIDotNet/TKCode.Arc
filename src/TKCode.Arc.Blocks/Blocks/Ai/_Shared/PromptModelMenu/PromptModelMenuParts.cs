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

namespace TKCode.Arc;

/// <summary>
/// A model of the studio's model menu, a radio item: 38px (padding 0 10, radius 16) with four columns 10px apart: the mark
/// (16), the full name (14, the text color, ellipsis), the prices ("$2 · $10", 12px tabular figures, muted) and a Check
/// (14, stroke 2) that shows on the selected row (opacity, 160ms). Selected: SurfaceMuted; under the pointer: SurfaceMuted
/// at 60% (120ms). The keyboard ring sits 2px inside the edge. Focusable but not a tab stop; a click picks the model.
/// </summary>
[PseudoClasses(CheckedPseudoClass)]
internal sealed class PromptModelRow : TemplatedControl
{
    public const string CheckedPseudoClass = ":checked";

    public static readonly StyledProperty<object?> MarkProperty =
        AvaloniaProperty.Register<PromptModelRow, object?>(nameof(Mark));

    public static readonly StyledProperty<IBrush?> MarkColorProperty =
        AvaloniaProperty.Register<PromptModelRow, IBrush?>(nameof(MarkColor));

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<PromptModelRow, string?>(nameof(Text));

    public static readonly StyledProperty<string?> PriceProperty =
        AvaloniaProperty.Register<PromptModelRow, string?>(nameof(Price));

    public static readonly StyledProperty<bool> IsCheckedProperty =
        AvaloniaProperty.Register<PromptModelRow, bool>(nameof(IsChecked));

    private static readonly FontFeatureCollection s_tabular = new() { FontFeature.Parse("tnum") };

    private bool _pressed;

    static PromptModelRow()
    {
        FocusableProperty.OverrideDefaultValue<PromptModelRow>(true);
        IsTabStopProperty.OverrideDefaultValue<PromptModelRow>(false);
    }

    public PromptModelRow(AiModel model)
    {
        Model = model;
    }

    public AiModel Model { get; }

    /// <summary>The menu that built the row.</summary>
    public PromptModelMenu? Owner { get; init; }

    public object? Mark { get => GetValue(MarkProperty); set => SetValue(MarkProperty, value); }

    public IBrush? MarkColor { get => GetValue(MarkColorProperty); set => SetValue(MarkColorProperty, value); }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public string? Price { get => GetValue(PriceProperty); set => SetValue(PriceProperty, value); }

    public bool IsChecked { get => GetValue(IsCheckedProperty); set => SetValue(IsCheckedProperty, value); }

    /// <summary>The price text block (the template part), or null before the template.</summary>
    internal TextBlock? PriceBlock { get; private set; }

    /// <summary>The check glyph (the template part), or null before the template.</summary>
    internal ArcGlyph? Check { get; private set; }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        PriceBlock = e.NameScope.Find<TextBlock>("PART_Price");
        Check = e.NameScope.Find<ArcGlyph>("PART_Check");
        if (PriceBlock is not null)
        {
            // Tabular figures: the prices of the rows line up digit by digit.
            PriceBlock.FontFeatures = s_tabular;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsCheckedProperty)
        {
            PseudoClasses.Set(CheckedPseudoClass, change.GetNewValue<bool>());
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.Handled && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _pressed = true;
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
        if (new Rect(Bounds.Size).Contains(e.GetPosition(this)))
        {
            e.Handled = true;
            Owner?.OnRowInvoked(this, NavigationMethod.Pointer);
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pressed = false;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new RowPeer(this);

    /// <summary>A radio button (checked = selected), named with the model and its prices.</summary>
    private sealed class RowPeer(PromptModelRow owner) : ControlAutomationPeer(owner), IToggleProvider, IInvokeProvider
    {
        private PromptModelRow Row => (PromptModelRow)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.RadioButton;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : Row.Text;

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;

        public ToggleState ToggleState => Row.IsChecked ? ToggleState.On : ToggleState.Off;

        public void Toggle() => Invoke();

        public void Invoke()
        {
            EnsureEnabled();
            Row.Owner?.OnRowInvoked(Row, NavigationMethod.Unspecified);
        }
    }
}

/// <summary>
/// The effort levels of the studio's model menu as one segmented row: a 36px track (1px Border, the pill radius,
/// padding 2) with equal cells 2px apart for the levels the selected model offers. The highlight (SurfaceMuted, radius
/// 15) glides under the selected cell on the quick spring (critically damped, Arc ω ≈ 22); Reduced motion and Off jump.
/// Assistive technology sees a radio group named by the menu.
/// </summary>
[TemplatePart(ThumbPart, typeof(GlidingIndicator))]
[TemplatePart(CellsPart, typeof(PromptEffortColumns))]
internal sealed class PromptEffortStrip : TemplatedControl
{
    public const string ThumbPart = "PART_Thumb";
    public const string CellsPart = "PART_Cells";

    /// <summary>The selected level (null when the model's level is none of the cells).</summary>
    public static readonly StyledProperty<ModelEffort?> LevelProperty =
        AvaloniaProperty.Register<PromptEffortStrip, ModelEffort?>(nameof(Level));

    private readonly List<PromptEffortCell> _cells = new();
    private GlidingIndicator? _thumb;
    private Panel? _cellsPanel;
    private ModelEffort? _placedLevel;
    private Rect _placedRect;

    static PromptEffortStrip()
    {
        FocusableProperty.OverrideDefaultValue<PromptEffortStrip>(false);
    }

    /// <summary>The menu that owns the strip.</summary>
    public PromptModelMenu? Owner { get; init; }

    public ModelEffort? Level { get => GetValue(LevelProperty); set => SetValue(LevelProperty, value); }

    public IReadOnlyList<PromptEffortCell> Cells => _cells;

    internal GlidingIndicator? Thumb => _thumb;

    /// <summary>The levels shown, in order.</summary>
    public IEnumerable<ModelEffort> Levels => _cells.Select(c => c.Level);

    /// <summary>Shows a cell per level (kept when the levels are the same), with its words.</summary>
    public void SetLevels(IReadOnlyList<ModelEffort> levels, Func<ModelEffort, string> cellText, Func<ModelEffort, string> name)
    {
        if (!_cells.Select(c => c.Level).SequenceEqual(levels))
        {
            _cellsPanel?.Children.Clear();
            _cells.Clear();
            foreach (var level in levels)
            {
                _cells.Add(new PromptEffortCell(level) { Owner = Owner });
            }
            _cellsPanel?.Children.AddRange(_cells);
        }
        foreach (var cell in _cells)
        {
            cell.Text = cellText(cell.Level);
            AutomationProperties.SetName(cell, name(cell.Level));
        }
        UpdateCells();
        InvalidateArrange();
    }

    /// <summary>The cell of <paramref name="level"/>, or null.</summary>
    public PromptEffortCell? CellOf(ModelEffort? level) => level is null ? null : _cells.FirstOrDefault(c => c.Level == level);

    /// <summary>The level <paramref name="delta"/> cells from the selected one, wrapping (the first one when none is selected).</summary>
    public ModelEffort? Step(int delta)
    {
        if (_cells.Count == 0)
        {
            return null;
        }
        var index = _cells.FindIndex(c => c.Level == Level);
        if (index < 0)
        {
            return _cells[0].Level;
        }
        var count = _cells.Count;
        return _cells[((index + delta) % count + count) % count].Level;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _cellsPanel?.Children.Clear();
        if (_cellsPanel is PromptEffortColumns old)
        {
            old.Arranged = null;
        }
        _thumb = e.NameScope.Find<GlidingIndicator>(ThumbPart);
        _cellsPanel = e.NameScope.Find<Panel>(CellsPart);
        _cellsPanel?.Children.AddRange(_cells);
        if (_cellsPanel is PromptEffortColumns columns)
        {
            columns.Arranged = PlaceThumb;
        }
        _placedLevel = null;
        UpdateCells();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LevelProperty)
        {
            UpdateCells();
            PlaceThumb();
        }
    }

    private void UpdateCells()
    {
        var any = false;
        foreach (var cell in _cells)
        {
            cell.IsChecked = cell.Level == Level;
            any |= cell.IsChecked;
        }
        // Roving tab stop: the selected cell, or the first when none is selected.
        for (var i = 0; i < _cells.Count; i++)
        {
            _cells[i].IsTabStop = any ? _cells[i].IsChecked : i == 0;
        }
    }

    /// <summary>
    /// Puts the highlight under the selected cell: a new level glides there, the first placement and a resize land at once;
    /// no selected cell hides it. Before the cells are laid out it waits for the arrange.
    /// </summary>
    internal void PlaceThumb()
    {
        if (_thumb is null)
        {
            return;
        }
        if (CellOf(Level) is not { } cell)
        {
            if (_placedLevel is not null)
            {
                _thumb.Hide(animate: false);
                _placedLevel = null;
            }
            return;
        }
        if (!cell.IsArrangeValid || cell.Bounds.Width <= 0 || cell.TranslatePoint(default, _thumb) is not { } origin)
        {
            return;
        }
        var rect = new Rect(origin, cell.Bounds.Size);
        if (_placedLevel == cell.Level && _placedRect == rect)
        {
            return;
        }
        var glide = _placedLevel is { } placed && placed != cell.Level;
        _placedLevel = cell.Level;
        _placedRect = rect;
        _thumb.MoveTo(rect, glide, ArcSpring.Quick);
    }

    /// <summary>Forgets where the highlight was, so it lands without gliding (the menu opens).</summary>
    internal void ResetThumb()
    {
        _placedLevel = null;
        _thumb?.Hide(animate: false);
        // The cells' arrange places it again (nothing else may lay them out when the menu reopens unchanged).
        _cellsPanel?.InvalidateArrange();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new StripPeer(this);

    private sealed class StripPeer(PromptEffortStrip owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override bool IsControlElementCore() => true;
    }
}

/// <summary>
/// One level of the effort row: its short word centered in a 30px cell (padding 0 12; 14 / 19.6), muted, the text color
/// when selected or under the pointer (160ms). The selected cell is the row's tab stop; the keyboard ring sits 2px inside
/// the cell. Assistive technology sees a radio button named with the full level ("Extra high").
/// </summary>
[PseudoClasses(CheckedPseudoClass)]
internal sealed class PromptEffortCell : TemplatedControl
{
    public const string CheckedPseudoClass = ":checked";

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<PromptEffortCell, string?>(nameof(Text));

    public static readonly StyledProperty<bool> IsCheckedProperty =
        AvaloniaProperty.Register<PromptEffortCell, bool>(nameof(IsChecked));

    private bool _pressed;

    static PromptEffortCell()
    {
        FocusableProperty.OverrideDefaultValue<PromptEffortCell>(true);
    }

    public PromptEffortCell(ModelEffort level)
    {
        Level = level;
    }

    public ModelEffort Level { get; }

    /// <summary>The menu that owns the row.</summary>
    public PromptModelMenu? Owner { get; init; }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public bool IsChecked { get => GetValue(IsCheckedProperty); set => SetValue(IsCheckedProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsCheckedProperty)
        {
            PseudoClasses.Set(CheckedPseudoClass, change.GetNewValue<bool>());
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.Handled && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _pressed = true;
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
        if (new Rect(Bounds.Size).Contains(e.GetPosition(this)))
        {
            e.Handled = true;
            Owner?.OnCellInvoked(this, NavigationMethod.Pointer);
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pressed = false;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new CellPeer(this);

    private sealed class CellPeer(PromptEffortCell owner) : ControlAutomationPeer(owner), IToggleProvider, IInvokeProvider
    {
        private PromptEffortCell Cell => (PromptEffortCell)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.RadioButton;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;

        public ToggleState ToggleState => Cell.IsChecked ? ToggleState.On : ToggleState.Off;

        public void Toggle() => Invoke();

        public void Invoke()
        {
            EnsureEnabled();
            Cell.Owner?.OnCellInvoked(Cell, NavigationMethod.Unspecified);
        }
    }
}

/// <summary>The model rows, 2px apart; assistive technology sees a radio group named by the menu.</summary>
internal sealed class PromptModelRowsPanel : StackPanel
{
    public PromptModelRowsPanel()
    {
        Spacing = 2;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new GroupPeer(this);

    private sealed class GroupPeer(PromptModelRowsPanel owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override bool IsControlElementCore() => true;
    }
}

/// <summary>
/// The effort cells in equal columns, <see cref="Spacing"/> apart, as tall as the tallest. After every arrange it asks the
/// strip to place its highlight: new cells (another model's levels) are laid out in a pass of their own, which the strip
/// itself does not take part in.
/// </summary>
internal sealed class PromptEffortColumns : Panel
{
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<PromptEffortColumns, double>(nameof(Spacing));

    static PromptEffortColumns()
    {
        AffectsMeasure<PromptEffortColumns>(SpacingProperty);
    }

    public double Spacing { get => GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }

    /// <summary>Called after the cells are arranged.</summary>
    public Action? Arranged { get; set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        var count = Children.Count;
        if (count == 0)
        {
            return default;
        }
        var gaps = Spacing * (count - 1);
        var column = double.IsInfinity(availableSize.Width) ? double.PositiveInfinity : Math.Max(0, (availableSize.Width - gaps) / count);
        var widest = 0.0;
        var tallest = 0.0;
        foreach (var child in Children)
        {
            child.Measure(new Size(column, availableSize.Height));
            widest = Math.Max(widest, child.DesiredSize.Width);
            tallest = Math.Max(tallest, child.DesiredSize.Height);
        }
        return new Size(double.IsInfinity(column) ? widest * count + gaps : availableSize.Width, tallest);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var count = Children.Count;
        var column = count == 0 ? 0 : Math.Max(0, (finalSize.Width - Spacing * (count - 1)) / count);
        var x = 0.0;
        foreach (var child in Children)
        {
            child.Arrange(new Rect(x, 0, column, finalSize.Height));
            x += column + Spacing;
        }
        Arranged?.Invoke();
        return finalSize;
    }
}
