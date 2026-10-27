using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One team button of the filter (Arc's pressed button): the team's label and, unless hidden, its number of people. It
/// does not toggle itself; the block sets <see cref="ToggleButton.IsChecked"/> so screen readers hear it as pressed.
/// </summary>
internal sealed class TeamDirectoryFilterButton : ToggleButton
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<TeamDirectoryFilterButton, string?>(nameof(Label));

    public static readonly StyledProperty<string?> CountTextProperty =
        AvaloniaProperty.Register<TeamDirectoryFilterButton, string?>(nameof(CountText));

    public static readonly StyledProperty<bool> IsCountVisibleProperty =
        AvaloniaProperty.Register<TeamDirectoryFilterButton, bool>(nameof(IsCountVisible), true);

    public TeamDirectoryFilterButton(string? team)
    {
        Team = team;
    }

    protected override Type StyleKeyOverride => typeof(TeamDirectoryFilterButton);

    /// <summary>The team this button shows; null for everyone.</summary>
    public string? Team { get; }

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public string? CountText { get => GetValue(CountTextProperty); set => SetValue(CountTextProperty, value); }

    public bool IsCountVisible { get => GetValue(IsCountVisibleProperty); set => SetValue(IsCountVisibleProperty, value); }

    protected override void Toggle()
    {
        // The block owns the pressed state.
    }
}

/// <summary>
/// The team buttons in a row, <see cref="Gap"/> apart, over one pill (the first child, a <see cref="GlidingIndicator"/>)
/// that glides and resizes to the pressed button on the layout spring when the team changes; resizes snap it.
/// </summary>
internal sealed class TeamDirectoryFilterStrip : Panel
{
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<TeamDirectoryFilterStrip, double>(nameof(Gap), 2);

    public static readonly StyledProperty<bool> IsCountVisibleProperty =
        AvaloniaProperty.Register<TeamDirectoryFilterStrip, bool>(nameof(IsCountVisible), true);

    private TeamDirectoryFilterButton? _active;
    private bool _animateNext;
    private Rect? _target;

    static TeamDirectoryFilterStrip()
    {
        AffectsMeasure<TeamDirectoryFilterStrip>(GapProperty);
    }

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    public bool IsCountVisible { get => GetValue(IsCountVisibleProperty); set => SetValue(IsCountVisibleProperty, value); }

    public GlidingIndicator? Indicator => Children.Count > 0 ? Children[0] as GlidingIndicator : null;

    public IEnumerable<TeamDirectoryFilterButton> Buttons => Children.OfType<TeamDirectoryFilterButton>();

    public TeamDirectoryFilterButton? Active => _active;

    /// <summary>Replaces the buttons (the pill stays).</summary>
    public void SetButtons(IReadOnlyList<TeamDirectoryFilterButton> buttons)
    {
        foreach (var old in Buttons.ToList())
        {
            Children.Remove(old);
        }
        foreach (var button in buttons)
        {
            button.IsCountVisible = IsCountVisible;
            Children.Add(button);
        }
        _active = null;
        _target = null;
    }

    /// <summary>Presses <paramref name="button"/>; the pill glides there when <paramref name="animate"/>.</summary>
    public void Select(TeamDirectoryFilterButton? button, bool animate)
    {
        foreach (var b in Buttons)
        {
            b.IsChecked = ReferenceEquals(b, button);
        }
        if (!ReferenceEquals(_active, button))
        {
            _active = button;
            _animateNext = animate && _target is not null;
            InvalidateArrange();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsCountVisibleProperty)
        {
            foreach (var b in Buttons)
            {
                b.IsCountVisible = IsCountVisible;
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = 0;
        double height = 0;
        var first = true;
        foreach (var b in Buttons)
        {
            b.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            if (!b.IsVisible)
            {
                continue;
            }
            width += (first ? 0 : Gap) + b.DesiredSize.Width;
            height = Math.Max(height, b.DesiredSize.Height);
            first = false;
        }
        Indicator?.Measure(availableSize);
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double x = 0;
        Rect? target = null;
        foreach (var b in Buttons)
        {
            if (!b.IsVisible)
            {
                continue;
            }
            var rect = new Rect(x, 0, b.DesiredSize.Width, finalSize.Height);
            b.Arrange(rect);
            if (ReferenceEquals(b, _active))
            {
                target = rect;
            }
            x += b.DesiredSize.Width + Gap;
        }
        if (Indicator is { } indicator)
        {
            indicator.Arrange(new Rect(finalSize));
            if (target is { } t)
            {
                if (_target != t)
                {
                    // Code-driven: the pill's target is the pressed button's measured rect (Arc's shared layout highlight).
                    indicator.MoveTo(t, TeamDirectoryMotion.Layout, _animateNext);
                    _target = t;
                }
            }
            else if (_target is not null)
            {
                indicator.Hide(false);
                _target = null;
            }
        }
        _animateNext = false;
        return finalSize;
    }
}

/// <summary>
/// One person in the list (Arc's pressed row button): avatar with its availability dot, name and role. It does not toggle
/// itself; the block sets <see cref="ToggleButton.IsChecked"/> on the row whose profile shows.
/// </summary>
[TemplatePart(AvatarPart, typeof(Avatar))]
[TemplatePart(NamePart, typeof(TextBlock))]
[TemplatePart(RolePart, typeof(TextBlock))]
internal sealed class TeamDirectoryRow : ToggleButton
{
    public const string AvatarPart = "PART_Avatar";
    public const string NamePart = "PART_Name";
    public const string RolePart = "PART_Role";

    private Avatar? _avatar;
    private TextBlock? _name;
    private TextBlock? _role;
    private TeamDirectoryLabels _labels = new();

    public TeamDirectoryRow(TeamDirectoryPerson person)
    {
        Person = person;
    }

    protected override Type StyleKeyOverride => typeof(TeamDirectoryRow);

    public TeamDirectoryPerson Person { get; }

    /// <summary>True while the row fades out after leaving the results.</summary>
    public bool IsLeaving { get; set; }

    protected override void Toggle()
    {
        // The block owns the pressed state.
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _avatar = e.NameScope.Find<Avatar>(AvatarPart);
        _name = e.NameScope.Find<TextBlock>(NamePart);
        _role = e.NameScope.Find<TextBlock>(RolePart);
        Refresh(_labels);
    }

    /// <summary>Shows the person's current fields.</summary>
    public void Refresh(TeamDirectoryLabels labels)
    {
        _labels = labels;
        var p = Person;
        var available = p.Availability == TeamDirectoryAvailability.Available;
        AutomationProperties.SetName(this, string.IsNullOrEmpty(p.Role) ? p.Name : $"{p.Name}, {p.Role}");
        if (_name is not null)
        {
            _name.Text = p.Name;
        }
        if (_role is not null)
        {
            _role.Text = p.Role;
            _role.IsVisible = !string.IsNullOrEmpty(p.Role);
        }
        if (_avatar is not null)
        {
            _avatar.DisplayName = p.Name;
            _avatar.Source = p.Avatar;
            _avatar.Status = available ? AvatarStatus.Online : AvatarStatus.Offline;
            AutomationProperties.SetName(_avatar, labels.AvatarName(p.Name, available));
        }
    }
}

/// <summary>
/// The rows of the list, <see cref="RowHeight"/> high and <see cref="RowGap"/> apart, over one highlight (the first
/// child, a <see cref="GlidingIndicator"/>) on the row whose profile shows. When the results change, rows that stay
/// spring from where they were drawn to their new place, rows that leave fade out where they were (not taking space)
/// and new rows fade in in place; the highlight springs to its row on the same spring. The panel's height changes at
/// once. Code-driven because the targets are measured layout positions and a retargeted flight keeps its speed.
/// </summary>
internal sealed class TeamDirectoryList : Panel
{
    public static readonly StyledProperty<double> RowHeightProperty =
        AvaloniaProperty.Register<TeamDirectoryList, double>(nameof(RowHeight), 52);

    public static readonly StyledProperty<double> RowGapProperty =
        AvaloniaProperty.Register<TeamDirectoryList, double>(nameof(RowGap), 2);

    private readonly ProjectsFlip _flip;
    private readonly List<TeamDirectoryRow> _rows = new();
    private TeamDirectoryRow? _highlighted;
    private Rect? _highlightTarget;
    private bool _animateNext;

    static TeamDirectoryList()
    {
        AffectsMeasure<TeamDirectoryList>(RowHeightProperty, RowGapProperty);
    }

    public TeamDirectoryList()
    {
        _flip = new ProjectsFlip(this);
    }

    public double RowHeight { get => GetValue(RowHeightProperty); set => SetValue(RowHeightProperty, value); }

    public double RowGap { get => GetValue(RowGapProperty); set => SetValue(RowGapProperty, value); }

    public GlidingIndicator? Indicator => Children.Count > 0 ? Children[0] as GlidingIndicator : null;

    /// <summary>The rows in the results, in order (leaving rows excluded).</summary>
    public IReadOnlyList<TeamDirectoryRow> Rows => _rows;

    /// <summary>Rows still fading out.</summary>
    public IEnumerable<TeamDirectoryRow> LeavingRows => Children.OfType<TeamDirectoryRow>().Where(r => r.IsLeaving);

    /// <summary>The row under the highlight, or null.</summary>
    public TeamDirectoryRow? Highlighted => _highlighted;

    /// <summary>The motion state of a row (tests read its offset and opacity).</summary>
    internal ProjectsItemMotion Motion(TeamDirectoryRow row) => _flip.Get(row);

    /// <summary>
    /// Shows <paramref name="rows"/> in this order with the highlight on <paramref name="highlighted"/>. Rows no longer
    /// listed leave; with <paramref name="animate"/> (and full motion) everything moves as described on the class.
    /// </summary>
    public void Update(IReadOnlyList<TeamDirectoryRow> rows, TeamDirectoryRow? highlighted, bool animate)
    {
        var full = animate && IsLoaded && ArcMotion.IsFull(this);
        var keep = new HashSet<TeamDirectoryRow>(rows);
        foreach (var old in _rows)
        {
            if (!keep.Contains(old))
            {
                Leave(old, full);
            }
        }
        _rows.Clear();
        foreach (var row in rows)
        {
            if (row.IsLeaving)
            {
                Revive(row, full);
            }
            else if (!Children.Contains(row))
            {
                Children.Add(row);
                var motion = _flip.Get(row);
                if (full)
                {
                    motion.Opacity.Jump(0);
                    _ = motion.Opacity.AnimateTo(1, TeamDirectoryMotion.RowEnterSpec);
                }
            }
            _rows.Add(row);
        }
        if (full)
        {
            _flip.Arm(MotionSpec.Spring(TeamDirectoryMotion.Layout), keepVelocity: true);
        }
        else
        {
            _flip.Settle();
        }
        _highlighted = highlighted;
        _animateNext = full;
        InvalidateMeasure();
    }

    private void Leave(TeamDirectoryRow row, bool full)
    {
        if (row.IsLeaving)
        {
            return;
        }
        row.IsLeaving = true;
        row.IsHitTestVisible = false;
        row.IsTabStop = false;
        row.Focusable = false;
        row.ZIndex = -1;
        if (!full)
        {
            Remove(row);
            return;
        }
        var motion = _flip.Get(row);
        motion.IsLeaving = true;
        _ = FadeOut(row, motion);
    }

    private async Task FadeOut(TeamDirectoryRow row, ProjectsItemMotion motion)
    {
        await motion.Opacity.AnimateTo(0, TeamDirectoryMotion.RowExitSpec);
        if (row.IsLeaving)
        {
            Remove(row);
        }
    }

    private void Revive(TeamDirectoryRow row, bool full)
    {
        row.IsLeaving = false;
        row.IsHitTestVisible = true;
        row.IsTabStop = true;
        row.Focusable = true;
        row.ZIndex = 0;
        if (!_flip.TryGet(row, out var motion))
        {
            return;
        }
        motion.IsLeaving = false;
        if (full)
        {
            _ = motion.Opacity.AnimateTo(1, TeamDirectoryMotion.RowEnterSpec);
        }
        else
        {
            motion.Opacity.Jump(1);
        }
    }

    private void Remove(TeamDirectoryRow row)
    {
        _flip.Forget(row);
        Children.Remove(row);
        RowRemoved?.Invoke(this, row);
    }

    /// <summary>A row left the panel for good (its fade ended).</summary>
    public event EventHandler<TeamDirectoryRow>? RowRemoved;

    /// <summary>Jumps every fade and flight to its end (motion switched off mid-flight, or a test).</summary>
    public void Settle()
    {
        foreach (var row in LeavingRows.ToList())
        {
            Remove(row);
        }
        _flip.Settle();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var rowSize = new Size(availableSize.Width, RowHeight);
        foreach (var child in Children)
        {
            if (child is TeamDirectoryRow row)
            {
                row.Measure(rowSize);
            }
        }
        Indicator?.Measure(availableSize);
        var n = _rows.Count;
        var height = n == 0 ? 0 : n * RowHeight + (n - 1) * RowGap;
        var width = double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Rect? highlight = null;
        for (var i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            var rect = new Rect(0, i * (RowHeight + RowGap), finalSize.Width, RowHeight);
            row.Arrange(rect);
            _flip.Place(row, rect);
            if (ReferenceEquals(row, _highlighted))
            {
                highlight = rect;
            }
        }
        foreach (var row in LeavingRows)
        {
            // A leaving row keeps the place it had (Arc pops it out of the flow where it stood).
            var motion = _flip.Get(row);
            var rect = motion.Layout ?? new Rect(0, 0, finalSize.Width, RowHeight);
            row.Arrange(rect.WithWidth(finalSize.Width));
        }
        if (Indicator is { } indicator)
        {
            indicator.Arrange(new Rect(finalSize));
            if (highlight is { } target)
            {
                if (_highlightTarget != target)
                {
                    indicator.MoveTo(target, TeamDirectoryMotion.Layout, _animateNext);
                    _highlightTarget = target;
                }
            }
            else if (_highlightTarget is not null)
            {
                // Nothing to highlight: Arc unmounts it with the row; the row's own fade carries it.
                indicator.Hide(false);
                _highlightTarget = null;
            }
        }
        _animateNext = false;
        return finalSize;
    }
}
