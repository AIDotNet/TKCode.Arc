using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// Breaks a large task into subtasks: a title and a description, a Suggest button that proposes subtasks from the
/// description (the built-in suggester, or your own <see cref="SuggestAction"/>), and an ordered list where each subtask
/// has an editable title, an estimate in hours and an assignee, can be dragged (or moved with the keys) into a new order
/// and removed. The footer adds up the hours, shows each person's load and creates the subtasks.
/// <para>
/// While <see cref="IsSampleDataEnabled"/> is true and nothing was given when it is first shown, the block fills in an
/// invented task, team and suggestion so it looks finished on a page.
/// </para>
/// </summary>
public partial class TaskBreakdown : TemplatedControl
{
    /// <summary>The task's title (two-way); the title field edits it.</summary>
    public static readonly StyledProperty<string?> TaskTitleProperty =
        AvaloniaProperty.Register<TaskBreakdown, string?>(nameof(TaskTitle), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The task's description (two-way); the description field edits it and the suggester reads it.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<TaskBreakdown, string?>(nameof(Description), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Proposes subtasks for a request; awaited when Suggest is pressed (or <see cref="SuggestAsync"/> is called). An
    /// exception shows a short failure line; null uses the built-in suggester.
    /// </summary>
    public static readonly StyledProperty<Func<TaskBreakdownRequest, CancellationToken, Task<IReadOnlyList<TaskBreakdownSubtask>>>?> SuggestActionProperty =
        AvaloniaProperty.Register<TaskBreakdown, Func<TaskBreakdownRequest, CancellationToken, Task<IReadOnlyList<TaskBreakdownSubtask>>>?>(nameof(SuggestAction));

    /// <summary>How many working hours make a day in the total line (6).</summary>
    public static readonly StyledProperty<double> HoursPerDayProperty =
        AvaloniaProperty.Register<TaskBreakdown, double>(nameof(HoursPerDay), 6);

    /// <summary>Fills in the invented sample when the block is first shown with no title, description, people or subtasks.</summary>
    public static readonly StyledProperty<bool> IsSampleDataEnabledProperty =
        AvaloniaProperty.Register<TaskBreakdown, bool>(nameof(IsSampleDataEnabled), true);

    /// <summary>Draws a bordered card (true); false lays the block flat on the page.</summary>
    public static readonly StyledProperty<bool> IsFramedProperty =
        AvaloniaProperty.Register<TaskBreakdown, bool>(nameof(IsFramed), true);

    /// <summary>The words the block shows and reads out.</summary>
    public static readonly StyledProperty<TaskBreakdownLabels> LabelsProperty =
        AvaloniaProperty.Register<TaskBreakdown, TaskBreakdownLabels>(nameof(Labels), new TaskBreakdownLabels());

    /// <summary>True while suggestions are being worked out.</summary>
    public static readonly DirectProperty<TaskBreakdown, bool> IsSuggestingProperty =
        AvaloniaProperty.RegisterDirect<TaskBreakdown, bool>(nameof(IsSuggesting), o => o.IsSuggesting);

    /// <summary>The subtasks' estimates added up, in hours.</summary>
    public static readonly DirectProperty<TaskBreakdown, double> TotalHoursProperty =
        AvaloniaProperty.RegisterDirect<TaskBreakdown, double>(nameof(TotalHours), o => o.TotalHours);

    /// <summary>Raised when the create button is pressed (or <see cref="Create"/> is called) with the subtasks in order.</summary>
    public static readonly RoutedEvent<TaskBreakdownCreatedEventArgs> CreatedEvent =
        RoutedEvent.Register<TaskBreakdown, TaskBreakdownCreatedEventArgs>(nameof(Created), RoutingStrategies.Bubble);

    private bool _isSuggesting;
    private double _totalHours;

    /// <summary>The subtasks in order; the list reorders this collection in place and the rows edit its items.</summary>
    public AvaloniaList<TaskBreakdownSubtask> Subtasks { get; } = new();

    /// <summary>The people subtasks can be assigned to.</summary>
    public AvaloniaList<ProjectPerson> People { get; } = new();

    /// <inheritdoc cref="TaskTitleProperty"/>
    public string? TaskTitle { get => GetValue(TaskTitleProperty); set => SetValue(TaskTitleProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="SuggestActionProperty"/>
    public Func<TaskBreakdownRequest, CancellationToken, Task<IReadOnlyList<TaskBreakdownSubtask>>>? SuggestAction
    {
        get => GetValue(SuggestActionProperty);
        set => SetValue(SuggestActionProperty, value);
    }

    /// <inheritdoc cref="HoursPerDayProperty"/>
    public double HoursPerDay { get => GetValue(HoursPerDayProperty); set => SetValue(HoursPerDayProperty, value); }

    /// <inheritdoc cref="IsSampleDataEnabledProperty"/>
    public bool IsSampleDataEnabled { get => GetValue(IsSampleDataEnabledProperty); set => SetValue(IsSampleDataEnabledProperty, value); }

    /// <inheritdoc cref="IsFramedProperty"/>
    public bool IsFramed { get => GetValue(IsFramedProperty); set => SetValue(IsFramedProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public TaskBreakdownLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="IsSuggestingProperty"/>
    public bool IsSuggesting { get => _isSuggesting; private set => SetAndRaise(IsSuggestingProperty, ref _isSuggesting, value); }

    /// <inheritdoc cref="TotalHoursProperty"/>
    public double TotalHours { get => _totalHours; private set => SetAndRaise(TotalHoursProperty, ref _totalHours, value); }

    /// <inheritdoc cref="CreatedEvent"/>
    public event EventHandler<TaskBreakdownCreatedEventArgs>? Created
    {
        add => AddHandler(CreatedEvent, value);
        remove => RemoveHandler(CreatedEvent, value);
    }

    /// <summary>Breaks the given text down with the built-in suggester (no UI): what Suggest does without a <see cref="SuggestAction"/>.</summary>
    public static IReadOnlyList<TaskBreakdownSubtask> SuggestFromText(string title, string description, IReadOnlyList<ProjectPerson>? people = null)
        => TaskBreakdownSuggester.Suggest(new TaskBreakdownRequest(title, description, people ?? Array.Empty<ProjectPerson>()));
}
