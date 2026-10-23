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
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// The effort levels of the inline (composer) layout as one segmented row: a muted track (1px border, radius 17, padding
/// 3) with equal cells 2px apart, the levels the selected model offers. It is one row of the menu: ↑ ↓ reach it like any
/// row, ← → move the level (the menu applies it at once), Enter or Space report the level, a click picks the cell under
/// the pointer; none of them closes the menu. The thumb (1px border, surface fill, resting shadow) glides under the
/// selected cell on Arc's measured slider spring, FromVisual(0.35, 0.1); with keyboard focus a 2px ring hugs it and
/// glides with it. Reduced motion and Off move both at once (Core <see cref="GlidingIndicator"/>).
/// </summary>
[TemplatePart(ThumbPart, typeof(GlidingIndicator))]
[TemplatePart(RingPart, typeof(GlidingIndicator))]
[TemplatePart(CellsPart, typeof(Panel))]
internal sealed class ModelMenuEffortStrip : ModelMenuItem
{
    public const string ThumbPart = "PART_Thumb";
    public const string RingPart = "PART_Ring";
    public const string CellsPart = "PART_Cells";

    /// <summary>The selected level (null when the model's level is not one of <see cref="Levels"/>).</summary>
    public static readonly StyledProperty<ModelEffort?> LevelProperty =
        AvaloniaProperty.Register<ModelMenuEffortStrip, ModelEffort?>(nameof(Level));

    /// <summary>The header word, used to find the row by typing ("Effort").</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ModelMenuEffortStrip, string?>(nameof(Title));

    /// <summary>The thumb's spring: visual duration (s) and bounce (the theme sets the token: 0.35, 0.1).</summary>
    public static readonly StyledProperty<double> ThumbSpringDurationProperty =
        AvaloniaProperty.Register<ModelMenuEffortStrip, double>(nameof(ThumbSpringDuration), 0.35);

    public static readonly StyledProperty<double> ThumbSpringBounceProperty =
        AvaloniaProperty.Register<ModelMenuEffortStrip, double>(nameof(ThumbSpringBounce), 0.1);

    // The thumb is a 1px border-box: its stroke (centered on the drawn edge) lies inside the cell. The ring is a 2px band
    // just outside the cell (Arc's segmented controls).
    private const double ThumbInset = 0.5;
    private const double RingOutset = 1;

    private readonly ModelMenuEffortCell[] _cells;
    private GlidingIndicator? _thumb;
    private GlidingIndicator? _ring;
    private Panel? _cellsPanel;
    private ModelEffort? _placedLevel;
    private Rect _placedRect;

    public ModelMenuEffortStrip(IReadOnlyList<ModelEffort> levels, Func<ModelEffort, string> cellText)
    {
        Levels = levels;
        _cells = new ModelMenuEffortCell[levels.Count];
        for (var i = 0; i < levels.Count; i++)
        {
            _cells[i] = new ModelMenuEffortCell(levels[i]) { Text = cellText(levels[i]) };
        }
    }

    /// <summary>The levels, in <see cref="ModelEffort"/> order.</summary>
    public IReadOnlyList<ModelEffort> Levels { get; }

    public IReadOnlyList<ModelMenuEffortCell> Cells => _cells;

    public ModelEffort? Level { get => GetValue(LevelProperty); set => SetValue(LevelProperty, value); }

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public double ThumbSpringDuration { get => GetValue(ThumbSpringDurationProperty); set => SetValue(ThumbSpringDurationProperty, value); }

    public double ThumbSpringBounce { get => GetValue(ThumbSpringBounceProperty); set => SetValue(ThumbSpringBounceProperty, value); }

    public override ModelMenuRowKind Kind => ModelMenuRowKind.EffortStrip;

    public override string? TypeaheadText => Title;

    internal GlidingIndicator? Thumb => _thumb;

    internal GlidingIndicator? Ring => _ring;

    internal SpringSpec ThumbSpring => SpringSpec.FromVisual(ThumbSpringDuration, ThumbSpringBounce);

    public bool Offers(ModelEffort level)
    {
        foreach (var offered in Levels)
        {
            if (offered == level)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>The level <paramref name="delta"/> cells from the selected one, wrapping (the first one when none is selected).</summary>
    public ModelEffort? Step(int delta)
    {
        if (Levels.Count == 0)
        {
            return null;
        }
        var index = -1;
        for (var i = 0; i < Levels.Count; i++)
        {
            if (Levels[i] == Level)
            {
                index = i;
                break;
            }
        }
        if (index < 0)
        {
            return Levels[0];
        }
        var count = Levels.Count;
        return Levels[((index + delta) % count + count) % count];
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _cellsPanel?.Children.Clear();
        _thumb = e.NameScope.Find<GlidingIndicator>(ThumbPart);
        _ring = e.NameScope.Find<GlidingIndicator>(RingPart);
        _cellsPanel = e.NameScope.Find<Panel>(CellsPart);
        _cellsPanel?.Children.AddRange(_cells);
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
        foreach (var cell in _cells)
        {
            cell.IsSelected = cell.Level == Level;
        }
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var arranged = base.ArrangeOverride(finalSize);
        // The cells have their places now: the first layout and a resize put the thumb under the level at once.
        PlaceThumb();
        return arranged;
    }

    /// <summary>
    /// Puts the thumb (and the ring) under the selected cell: a new level glides there, the first placement and a resize
    /// land at once. Before the cells are laid out it waits for the arrange.
    /// </summary>
    private void PlaceThumb()
    {
        if (_thumb is null || Level is not { } level || CellOf(level) is not { } cell || RectOf(cell) is not { } rect)
        {
            return;
        }
        if (_placedLevel == level && _placedRect == rect)
        {
            return;
        }
        var glide = _placedLevel is { } placed && placed != level;
        _placedLevel = level;
        _placedRect = rect;
        var spring = ThumbSpring;
        _thumb.MoveTo(rect.Deflate(ThumbInset), spring, glide);
        _ring?.MoveTo(rect.Inflate(RingOutset), spring, glide);
    }

    private ModelMenuEffortCell? CellOf(ModelEffort level)
    {
        foreach (var cell in _cells)
        {
            if (cell.Level == level)
            {
                return cell;
            }
        }
        return null;
    }

    /// <summary>The cell's box in the thumb's coordinates, or null before it is laid out.</summary>
    private Rect? RectOf(ModelMenuEffortCell cell)
    {
        if (_thumb is null || !cell.IsArrangeValid || cell.Bounds.Width <= 0 || cell.TranslatePoint(default, _thumb) is not { } origin)
        {
            return null;
        }
        return new Rect(origin, cell.Bounds.Size);
    }

    /// <summary>A click picks the level under the pointer (a click between two cells picks nothing).</summary>
    protected override void OnClicked(PointerReleasedEventArgs e)
    {
        foreach (var cell in _cells)
        {
            if (new Rect(cell.Bounds.Size).Contains(e.GetPosition(cell)))
            {
                Choose(cell.Level, NavigationMethod.Pointer);
                return;
            }
        }
    }

    /// <summary>Picks <paramref name="level"/> through the menu (it applies it and reports it; the menu stays open).</summary>
    internal void Choose(ModelEffort level, NavigationMethod method)
    {
        if (this.FindAncestorOfType<ModelMenuPanel>() is { } panel)
        {
            panel.Owner.OnEffortCellChosen(panel, this, level, method);
        }
    }
}

/// <summary>
/// One level of the segmented row: its word centered in a 30px cell (12/16.8, secondary; the text color when selected or
/// under the pointer, changing in 160ms). Not a tab stop (the row is); automation sees a radio button.
/// </summary>
[PseudoClasses(SelectedPseudoClass)]
internal sealed class ModelMenuEffortCell : TemplatedControl
{
    public const string SelectedPseudoClass = ":selected";

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ModelMenuEffortCell, string?>(nameof(Text));

    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<ModelMenuEffortCell, bool>(nameof(IsSelected));

    static ModelMenuEffortCell()
    {
        FocusableProperty.OverrideDefaultValue<ModelMenuEffortCell>(false);
    }

    public ModelMenuEffortCell(ModelEffort level)
    {
        Level = level;
    }

    public ModelEffort Level { get; }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public bool IsSelected { get => GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsSelectedProperty)
        {
            PseudoClasses.Set(SelectedPseudoClass, change.GetNewValue<bool>());
        }
        else if (change.Property == TextProperty)
        {
            AutomationProperties.SetName(this, Text);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new CellPeer(this);

    /// <summary>A radio button of the levels: checked when selected; invoking or toggling it picks the level.</summary>
    private sealed class CellPeer(ModelMenuEffortCell owner) : ControlAutomationPeer(owner), IInvokeProvider, IToggleProvider
    {
        private ModelMenuEffortCell Cell => (ModelMenuEffortCell)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.RadioButton;

        protected override bool IsControlElementCore() => true;

        protected override bool IsContentElementCore() => true;

        public void Invoke() => Pick();

        public ToggleState ToggleState => Cell.IsSelected ? ToggleState.On : ToggleState.Off;

        public void Toggle() => Pick();

        private void Pick()
        {
            EnsureEnabled();
            Cell.FindAncestorOfType<ModelMenuEffortStrip>()?.Choose(Cell.Level, NavigationMethod.Unspecified);
        }
    }
}

/// <summary>
/// The inline layout's header over the segmented row: "Effort" at the left (12/16.8, secondary) and the current level's
/// description at the right (muted, ellipsis). Text only: the segmented row carries the level for assistive technology.
/// </summary>
internal sealed class ModelMenuEffortHeader : TemplatedControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ModelMenuEffortHeader, string?>(nameof(Title));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<ModelMenuEffortHeader, string?>(nameof(Description));

    static ModelMenuEffortHeader()
    {
        FocusableProperty.OverrideDefaultValue<ModelMenuEffortHeader>(false);
        AutomationProperties.AccessibilityViewProperty.OverrideDefaultValue<ModelMenuEffortHeader>(AccessibilityView.Raw);
    }

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
}

/// <summary>Lays its children out in equal columns, <see cref="Spacing"/> apart, as tall as the tallest one.</summary>
internal sealed class ModelMenuEqualColumns : Panel
{
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<ModelMenuEqualColumns, double>(nameof(Spacing));

    static ModelMenuEqualColumns()
    {
        AffectsMeasure<ModelMenuEqualColumns>(SpacingProperty);
    }

    public double Spacing { get => GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }

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
        var width = double.IsInfinity(column) ? widest * count + gaps : availableSize.Width;
        return new Size(width, tallest);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var count = Children.Count;
        if (count == 0)
        {
            return finalSize;
        }
        var column = Math.Max(0, (finalSize.Width - Spacing * (count - 1)) / count);
        var x = 0.0;
        foreach (var child in Children)
        {
            child.Arrange(new Rect(x, 0, column, finalSize.Height));
            x += column + Spacing;
        }
        return finalSize;
    }
}
