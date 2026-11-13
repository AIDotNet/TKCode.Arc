using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The Project board's header row: the title block on the left and the actions on the right, both centred on one row
/// when they fit with <see cref="ColumnGap"/> between them; otherwise the actions wrap under the title
/// (<see cref="RowGap"/> apart), still left-aligned. <see cref="IsCompact"/> always wraps and stretches the actions over
/// the whole row (Arc's ≤ 400 header: filter on the left, Add on the right).
/// </summary>
internal sealed class ProjectBoardHeaderPanel : Panel
{
    public static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<ProjectBoardHeaderPanel, double>(nameof(ColumnGap), 16);

    public static readonly StyledProperty<double> RowGapProperty =
        AvaloniaProperty.Register<ProjectBoardHeaderPanel, double>(nameof(RowGap), 12);

    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<ProjectBoardHeaderPanel, bool>(nameof(IsCompact));

    private bool _wrapped;

    static ProjectBoardHeaderPanel()
    {
        AffectsMeasure<ProjectBoardHeaderPanel>(ColumnGapProperty, RowGapProperty, IsCompactProperty);
    }

    public double ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }

    public double RowGap { get => GetValue(RowGapProperty); set => SetValue(RowGapProperty, value); }

    public bool IsCompact { get => GetValue(IsCompactProperty); set => SetValue(IsCompactProperty, value); }

    /// <summary>True when the actions sit on a second row (tests).</summary>
    public bool IsWrapped => _wrapped;

    private Control? Lead => Children.Count > 0 ? Children[0] : null;

    private Control? Actions => Children.Count > 1 ? Children[1] : null;

    protected override Size MeasureOverride(Size availableSize)
    {
        var lead = Lead;
        var actions = Actions;
        var width = availableSize.Width;
        lead?.Measure(new Size(double.PositiveInfinity, availableSize.Height));
        actions?.Measure(new Size(double.PositiveInfinity, availableSize.Height));
        var leadSize = Visible(lead) ? lead!.DesiredSize : default;
        var actionsSize = Visible(actions) ? actions!.DesiredSize : default;
        var both = Visible(lead) && Visible(actions);
        _wrapped = both && (IsCompact || (double.IsFinite(width) && leadSize.Width + ColumnGap + actionsSize.Width > width));
        if (!_wrapped)
        {
            var row = leadSize.Width + (both ? ColumnGap : 0) + actionsSize.Width;
            if (both && double.IsFinite(width))
            {
                // The title gets what the actions leave (it wraps rather than pushing them out).
                lead!.Measure(new Size(Math.Max(0, width - ColumnGap - actionsSize.Width), availableSize.Height));
                leadSize = lead.DesiredSize;
            }
            return new Size(double.IsFinite(width) ? width : row, Math.Max(leadSize.Height, actionsSize.Height));
        }
        if (double.IsFinite(width))
        {
            lead!.Measure(new Size(width, availableSize.Height));
            leadSize = lead.DesiredSize;
            if (IsCompact)
            {
                actions!.Measure(new Size(width, availableSize.Height));
                actionsSize = actions.DesiredSize;
            }
        }
        var total = Math.Max(leadSize.Width, actionsSize.Width);
        return new Size(double.IsFinite(width) ? width : total, leadSize.Height + RowGap + actionsSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var lead = Lead;
        var actions = Actions;
        var leadSize = Visible(lead) ? lead!.DesiredSize : default;
        var actionsSize = Visible(actions) ? actions!.DesiredSize : default;
        if (!_wrapped)
        {
            var height = finalSize.Height;
            lead?.Arrange(new Rect(0, (height - leadSize.Height) / 2, Math.Max(0, finalSize.Width - actionsSize.Width - (Visible(actions) ? ColumnGap : 0)), leadSize.Height));
            actions?.Arrange(new Rect(finalSize.Width - actionsSize.Width, (height - actionsSize.Height) / 2, actionsSize.Width, actionsSize.Height));
            return finalSize;
        }
        lead?.Arrange(new Rect(0, 0, finalSize.Width, leadSize.Height));
        var actionsWidth = IsCompact ? finalSize.Width : actionsSize.Width;
        actions?.Arrange(new Rect(0, leadSize.Height + RowGap, actionsWidth, actionsSize.Height));
        return finalSize;
    }

    private static bool Visible(Control? control) => control is { IsVisible: true };
}

/// <summary>
/// The All work / My work switch of the Project board: a muted track with two pressed-state buttons and a raised
/// highlight that glides between them (k 400, c 40, critically damped). Both buttons are tab stops (Arc); Enter and
/// Space choose. Screen readers hear a group named <see cref="Label"/> with two toggle buttons.
/// </summary>
[TemplatePart(AllPart, typeof(ProjectBoardFilterButton))]
[TemplatePart(MinePart, typeof(ProjectBoardFilterButton))]
[TemplatePart(HighlightPart, typeof(GlidingIndicator))]
[PseudoClasses(NarrowPseudoClass)]
internal sealed class ProjectBoardFilterBar : TemplatedControl
{
    public const string AllPart = "PART_All";
    public const string MinePart = "PART_Mine";
    public const string HighlightPart = "PART_Highlight";
    public const string NarrowPseudoClass = ":narrow";

    public static readonly StyledProperty<ProjectBoardFilter> FilterProperty =
        AvaloniaProperty.Register<ProjectBoardFilterBar, ProjectBoardFilter>(nameof(Filter));

    public static readonly StyledProperty<string> AllTextProperty =
        AvaloniaProperty.Register<ProjectBoardFilterBar, string>(nameof(AllText), "All work");

    public static readonly StyledProperty<string> MineTextProperty =
        AvaloniaProperty.Register<ProjectBoardFilterBar, string>(nameof(MineText), "My work");

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ProjectBoardFilterBar, string>(nameof(Label), "Show");

    public static readonly StyledProperty<bool> IsNarrowProperty =
        AvaloniaProperty.Register<ProjectBoardFilterBar, bool>(nameof(IsNarrow));

    private ProjectBoardFilterButton? _all;
    private ProjectBoardFilterButton? _mine;
    private GlidingIndicator? _highlight;
    private Rect _placed;
    private bool _hasPlaced;

    static ProjectBoardFilterBar()
    {
        FocusableProperty.OverrideDefaultValue<ProjectBoardFilterBar>(false);
    }

    public ProjectBoardFilterBar()
    {
        AutomationProperties.SetName(this, Label);
    }

    public ProjectBoardFilter Filter { get => GetValue(FilterProperty); set => SetValue(FilterProperty, value); }

    public string AllText { get => GetValue(AllTextProperty); set => SetValue(AllTextProperty, value); }

    public string MineText { get => GetValue(MineTextProperty); set => SetValue(MineTextProperty, value); }

    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public bool IsNarrow { get => GetValue(IsNarrowProperty); set => SetValue(IsNarrowProperty, value); }

    /// <summary>A person chose a filter with one of the buttons.</summary>
    public event Action<ProjectBoardFilter>? Chosen;

    internal ProjectBoardFilterButton? AllButton => _all;

    internal ProjectBoardFilterButton? MineButton => _mine;

    internal GlidingIndicator? Highlight => _highlight;

    internal ProjectBoardFilterButton? ButtonFor(ProjectBoardFilter filter) => filter == ProjectBoardFilter.Mine ? _mine : _all;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        foreach (var button in new[] { _all, _mine })
        {
            if (button is not null)
            {
                button.Click -= OnButtonClick;
                button.LayoutUpdated -= OnButtonLayoutUpdated;
            }
        }
        _all = e.NameScope.Find<ProjectBoardFilterButton>(AllPart);
        _mine = e.NameScope.Find<ProjectBoardFilterButton>(MinePart);
        _highlight = e.NameScope.Find<GlidingIndicator>(HighlightPart);
        _hasPlaced = false;
        foreach (var button in new[] { _all, _mine })
        {
            if (button is not null)
            {
                button.Click += OnButtonClick;
                button.LayoutUpdated += OnButtonLayoutUpdated;
            }
        }
        Sync(animate: false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FilterProperty)
        {
            Sync(animate: true);
        }
        else if (change.Property == LabelProperty)
        {
            AutomationProperties.SetName(this, Label);
        }
        else if (change.Property == IsNarrowProperty)
        {
            PseudoClasses.Set(NarrowPseudoClass, IsNarrow);
            _hasPlaced = false;
        }
    }

    private void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not ProjectBoardFilterButton button)
        {
            return;
        }
        e.Handled = true;
        if (button.Filter != Filter)
        {
            Chosen?.Invoke(button.Filter);
        }
        Sync(animate: true);
    }

    private void Sync(bool animate)
    {
        if (_all is not null)
        {
            _all.IsChecked = Filter == ProjectBoardFilter.All;
        }
        if (_mine is not null)
        {
            _mine.IsChecked = Filter == ProjectBoardFilter.Mine;
        }
        MoveHighlight(animate);
    }

    private void OnButtonLayoutUpdated(object? sender, EventArgs e) => MoveHighlight(animate: false, onlyIfMisplaced: true);

    private void MoveHighlight(bool animate, bool onlyIfMisplaced = false)
    {
        if (_highlight is null || ButtonFor(Filter) is not { } button || button.Bounds.Width <= 0)
        {
            return;
        }
        // Arc draws the 1px ring as a box-shadow outside the button: inflate by half a pixel so the stroke lies outside.
        var target = button.Bounds.Inflate(0.5);
        if (onlyIfMisplaced && _hasPlaced && Near(_placed, target))
        {
            return;
        }
        _highlight.MoveTo(target, ProjectsSprings.Filter, animate && _hasPlaced);
        _placed = target;
        _hasPlaced = true;
    }

    private static bool Near(Rect a, Rect b) =>
        Math.Abs(a.X - b.X) < 0.25 && Math.Abs(a.Y - b.Y) < 0.25 && Math.Abs(a.Width - b.Width) < 0.25 && Math.Abs(a.Height - b.Height) < 0.25;

    protected override AutomationPeer OnCreateAutomationPeer() => new FilterBarPeer(this);

    private sealed class FilterBarPeer(ProjectBoardFilterBar owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}
