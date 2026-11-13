using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using AvaloniaButton = Avalonia.Controls.Button;
using AvaloniaToggleButton = Avalonia.Controls.Primitives.ToggleButton;

namespace TKCode.Arc;

/// <summary>
/// A button of the Project board (card arrows, Add task) whose hover look follows a mouse or a pen only: Arc's hover
/// styles live under <c>(hover: hover)</c>, so a touch never leaves a hovered button behind. The theme styles
/// <c>:mouse-over</c> instead of <c>:pointerover</c>.
/// </summary>
[PseudoClasses(MouseOverPseudoClass)]
internal sealed class ProjectBoardButton : AvaloniaButton
{
    public const string MouseOverPseudoClass = ":mouse-over";

    /// <summary>The corner radius of the keyboard focus ring (the button's radius + 4: the ring is 2px, 2px out).</summary>
    public static readonly StyledProperty<CornerRadius> FocusRadiusProperty =
        AvaloniaProperty.Register<ProjectBoardButton, CornerRadius>(nameof(FocusRadius));

    public CornerRadius FocusRadius { get => GetValue(FocusRadiusProperty); set => SetValue(FocusRadiusProperty, value); }

    protected override Type StyleKeyOverride => typeof(ProjectBoardButton);

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        PseudoClasses.Set(MouseOverPseudoClass, e.Pointer.Type != PointerType.Touch);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(MouseOverPseudoClass, false);
    }
}

/// <summary>
/// One of the two filter buttons (All work / My work): a pressed-state button (screen readers hear "pressed") that the
/// filter bar checks; pressing it never un-presses it. Hover follows a mouse or a pen only.
/// </summary>
[PseudoClasses(ProjectBoardButton.MouseOverPseudoClass)]
internal sealed class ProjectBoardFilterButton : AvaloniaToggleButton
{
    protected override Type StyleKeyOverride => typeof(ProjectBoardFilterButton);

    /// <summary>The filter the button chooses.</summary>
    public ProjectBoardFilter Filter { get; init; }

    // The bar owns IsChecked: a click chooses this filter, it never toggles the button off.
    protected override void Toggle()
    {
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        PseudoClasses.Set(ProjectBoardButton.MouseOverPseudoClass, e.Pointer.Type != PointerType.Touch);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(ProjectBoardButton.MouseOverPseudoClass, false);
    }
}

/// <summary>
/// One task card of a <see cref="ProjectBoard"/>: area and due label, title, owner avatar, comment count and the two
/// arrow buttons. The board keeps every card in one panel and moves it by render transform only (it never changes
/// parent), so the focused arrow button keeps its focus through a move. Screen readers read the card as a list item
/// named after its task.
/// </summary>
[TemplatePart(PreviousPart, typeof(ProjectBoardButton))]
[TemplatePart(NextPart, typeof(ProjectBoardButton))]
[PseudoClasses(DonePseudoClass, DueSoonPseudoClass, NarrowPseudoClass, MovingPseudoClass, ProjectBoardButton.MouseOverPseudoClass)]
internal sealed class ProjectBoardCard : TemplatedControl
{
    public const string PreviousPart = "PART_Previous";
    public const string NextPart = "PART_Next";
    public const string DonePseudoClass = ":done";
    public const string DueSoonPseudoClass = ":due-soon";
    public const string NarrowPseudoClass = ":narrow";
    public const string MovingPseudoClass = ":moving";

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<ProjectBoardCard, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<string?> AreaProperty =
        AvaloniaProperty.Register<ProjectBoardCard, string?>(nameof(Area));

    public static readonly StyledProperty<string?> DueTextProperty =
        AvaloniaProperty.Register<ProjectBoardCard, string?>(nameof(DueText));

    public static readonly StyledProperty<ProjectPerson?> OwnerProperty =
        AvaloniaProperty.Register<ProjectBoardCard, ProjectPerson?>(nameof(Owner));

    public static readonly StyledProperty<string?> CommentTextProperty =
        AvaloniaProperty.Register<ProjectBoardCard, string?>(nameof(CommentText));

    public static readonly StyledProperty<bool> CanMovePreviousProperty =
        AvaloniaProperty.Register<ProjectBoardCard, bool>(nameof(CanMovePrevious));

    public static readonly StyledProperty<bool> CanMoveNextProperty =
        AvaloniaProperty.Register<ProjectBoardCard, bool>(nameof(CanMoveNext));

    public static readonly StyledProperty<string> PreviousNameProperty =
        AvaloniaProperty.Register<ProjectBoardCard, string>(nameof(PreviousName), string.Empty);

    public static readonly StyledProperty<string> NextNameProperty =
        AvaloniaProperty.Register<ProjectBoardCard, string>(nameof(NextName), string.Empty);

    public static readonly StyledProperty<bool> IsDoneProperty =
        AvaloniaProperty.Register<ProjectBoardCard, bool>(nameof(IsDone));

    public static readonly StyledProperty<bool> IsDueSoonProperty =
        AvaloniaProperty.Register<ProjectBoardCard, bool>(nameof(IsDueSoon));

    public static readonly StyledProperty<bool> IsNarrowProperty =
        AvaloniaProperty.Register<ProjectBoardCard, bool>(nameof(IsNarrow));

    private static readonly string[] s_tabularParts = ["PART_Due", "PART_CommentCount"];

    private ProjectBoardButton? _previous;
    private ProjectBoardButton? _next;
    private bool _isFlying;
    private bool _isLeaving;

    static ProjectBoardCard()
    {
        FocusableProperty.OverrideDefaultValue<ProjectBoardCard>(false);
    }

    public ProjectBoardCard(ProjectBoardTask task)
    {
        Task = task;
    }

    /// <summary>The task shown.</summary>
    public ProjectBoardTask Task { get; }

    /// <summary>The board that owns the card (set by the board).</summary>
    internal ProjectBoard? Board { get; set; }

    /// <summary>The stage the card is in (set by the board on every rebuild).</summary>
    internal ProjectBoardStage? Stage { get; set; }

    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public string? Area { get => GetValue(AreaProperty); set => SetValue(AreaProperty, value); }

    public string? DueText { get => GetValue(DueTextProperty); set => SetValue(DueTextProperty, value); }

    public ProjectPerson? Owner { get => GetValue(OwnerProperty); set => SetValue(OwnerProperty, value); }

    /// <summary>The comment count as text, or null when there are none (the count is hidden).</summary>
    public string? CommentText { get => GetValue(CommentTextProperty); set => SetValue(CommentTextProperty, value); }

    public bool CanMovePrevious { get => GetValue(CanMovePreviousProperty); set => SetValue(CanMovePreviousProperty, value); }

    public bool CanMoveNext { get => GetValue(CanMoveNextProperty); set => SetValue(CanMoveNextProperty, value); }

    public string PreviousName { get => GetValue(PreviousNameProperty); set => SetValue(PreviousNameProperty, value); }

    public string NextName { get => GetValue(NextNameProperty); set => SetValue(NextNameProperty, value); }

    public bool IsDone { get => GetValue(IsDoneProperty); set => SetValue(IsDoneProperty, value); }

    public bool IsDueSoon { get => GetValue(IsDueSoonProperty); set => SetValue(IsDueSoonProperty, value); }

    public bool IsNarrow { get => GetValue(IsNarrowProperty); set => SetValue(IsNarrowProperty, value); }

    /// <summary>The previous-stage button (after the template is applied).</summary>
    internal ProjectBoardButton? PreviousButton => _previous;

    /// <summary>The next-stage button (after the template is applied).</summary>
    internal ProjectBoardButton? NextButton => _next;

    /// <summary>The button for <paramref name="direction"/>.</summary>
    internal ProjectBoardButton? ButtonFor(ProjectBoardMoveDirection direction) =>
        direction == ProjectBoardMoveDirection.Previous ? _previous : _next;

    /// <summary>True while the card flies to another column: it draws above every other card.</summary>
    internal bool IsFlying
    {
        get => _isFlying;
        set
        {
            if (_isFlying == value)
            {
                return;
            }
            _isFlying = value;
            ZIndex = value ? 1 : 0;
            PseudoClasses.Set(MovingPseudoClass, value);
        }
    }

    /// <summary>Counts leave / revive changes, so a finished fade only removes the card it started for.</summary>
    internal int LeaveVersion { get; set; }

    /// <summary>True while the card fades out of the board: it takes no input and no focus, and screen readers skip it.</summary>
    internal bool IsLeaving
    {
        get => _isLeaving;
        set
        {
            if (_isLeaving == value)
            {
                return;
            }
            _isLeaving = value;
            IsHitTestVisible = !value;
            AutomationProperties.SetAccessibilityView(this, value ? AccessibilityView.Raw : AccessibilityView.Default);
            UpdateButtonsFocusable();
        }
    }

    /// <summary>Copies the task's display values (title, area, due, owner, comments).</summary>
    internal void Refresh(string commentText)
    {
        Title = Task.Title;
        Area = string.IsNullOrEmpty(Task.Area) ? null : Task.Area;
        DueText = string.IsNullOrEmpty(Task.DueText) ? null : Task.DueText;
        Owner = Task.Owner;
        CommentText = Task.CommentCount > 0 ? commentText : null;
        IsDueSoon = Task.IsDueSoon;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_previous is not null)
        {
            _previous.Click -= OnPreviousClick;
        }
        if (_next is not null)
        {
            _next.Click -= OnNextClick;
        }
        foreach (var name in s_tabularParts)
        {
            if (e.NameScope.Find<TextBlock>(name) is { } text)
            {
                text.FontFeatures = ProjectBoardLayout.TabularNumbers;
            }
        }
        _previous = e.NameScope.Find<ProjectBoardButton>(PreviousPart);
        _next = e.NameScope.Find<ProjectBoardButton>(NextPart);
        if (_previous is not null)
        {
            _previous.Click += OnPreviousClick;
        }
        if (_next is not null)
        {
            _next.Click += OnNextClick;
        }
        UpdateButtonsFocusable();
    }

    private void UpdateButtonsFocusable()
    {
        foreach (var button in new[] { _previous, _next })
        {
            if (button is not null)
            {
                button.Focusable = !_isLeaving;
                KeyboardNavigation.SetIsTabStop(button, !_isLeaving);
            }
        }
    }

    private void OnPreviousClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Board?.OnCardMoveClicked(this, ProjectBoardMoveDirection.Previous);
    }

    private void OnNextClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Board?.OnCardMoveClicked(this, ProjectBoardMoveDirection.Next);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsDoneProperty || change.Property == IsDueSoonProperty)
        {
            PseudoClasses.Set(DonePseudoClass, IsDone);
            PseudoClasses.Set(DueSoonPseudoClass, IsDueSoon && !IsDone);
        }
        else if (change.Property == IsNarrowProperty)
        {
            PseudoClasses.Set(NarrowPseudoClass, IsNarrow);
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        PseudoClasses.Set(ProjectBoardButton.MouseOverPseudoClass, e.Pointer.Type != PointerType.Touch);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(ProjectBoardButton.MouseOverPseudoClass, false);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ProjectBoardCardPeer(this);

    private sealed class ProjectBoardCardPeer(ProjectBoardCard owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

        protected override bool IsContentElementCore() => !((ProjectBoardCard)Owner).IsLeaving;

        protected override bool IsControlElementCore() => !((ProjectBoardCard)Owner).IsLeaving;
    }
}
