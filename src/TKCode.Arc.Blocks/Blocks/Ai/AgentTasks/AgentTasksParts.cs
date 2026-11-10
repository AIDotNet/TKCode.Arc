using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;

namespace TKCode.Arc;

/// <summary>A column of an <see cref="AgentTasks"/> board: title, count and cards (or a quiet empty line).</summary>
[TemplatePart(CardsPart, typeof(StackPanel))]
[PseudoClasses(EmptyPseudoClass)]
internal sealed class AgentTasksColumn : TemplatedControl
{
    public const string CardsPart = "PART_Cards";
    public const string EmptyPseudoClass = ":empty";

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<AgentTasksColumn, string?>(nameof(Title));

    public static readonly StyledProperty<string?> CountTextProperty =
        AvaloniaProperty.Register<AgentTasksColumn, string?>(nameof(CountText));

    public static readonly StyledProperty<string?> EmptyTextProperty =
        AvaloniaProperty.Register<AgentTasksColumn, string?>(nameof(EmptyText));

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public string? CountText { get => GetValue(CountTextProperty); set => SetValue(CountTextProperty, value); }

    public string? EmptyText { get => GetValue(EmptyTextProperty); set => SetValue(EmptyTextProperty, value); }

    private readonly List<AgentTasksCard> _cards = new();
    private StackPanel? _host;

    public int Count => _cards.Count;

    public AgentTasksCard? CardAt(int index) => index >= 0 && index < _cards.Count ? _cards[index] : null;

    public int IndexOf(AgentTasksCard card) => _cards.IndexOf(card);

    public void SetCards(string title, List<AgentTasksCard> cards, string empty)
    {
        Title = title;
        EmptyText = empty;
        CountText = cards.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        AutomationProperties.SetName(this, $"{title}, {cards.Count}");
        PseudoClasses.Set(EmptyPseudoClass, cards.Count == 0);
        var same = cards.Count == _cards.Count;
        for (var i = 0; same && i < cards.Count; i++)
        {
            same = ReferenceEquals(cards[i], _cards[i]);
        }
        if (same)
        {
            return;
        }
        _cards.Clear();
        _cards.AddRange(cards);
        Fill();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        _host?.Children.Clear();
        base.OnApplyTemplate(e);
        _host = e.NameScope.Find<StackPanel>(CardsPart);
        Fill();
    }

    private void Fill()
    {
        if (_host is null)
        {
            return;
        }
        // A card leaves its old column before it joins this one (a parent can hold it once).
        foreach (var card in _cards)
        {
            if (card.Parent is Panel old && !ReferenceEquals(old, _host))
            {
                old.Children.Remove(card);
            }
        }
        _host.Children.Clear();
        foreach (var card in _cards)
        {
            _host.Children.Add(card);
        }
    }
}

/// <summary>
/// A task card: title, the note (current step, question or error), the progress of a running task, and the agent with the
/// time of the last change. Pressing it opens the task's run.
/// </summary>
[PseudoClasses(QueuedPseudoClass, RunningPseudoClass, NeedsInputPseudoClass, FailedPseudoClass, DonePseudoClass)]
internal sealed class AgentTasksCard : AiAgentButton
{
    public const string QueuedPseudoClass = ":queued";
    public const string RunningPseudoClass = ":running";
    public const string NeedsInputPseudoClass = ":needs-input";
    public const string FailedPseudoClass = ":failed";
    public const string DonePseudoClass = ":done";

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<AgentTasksCard, string?>(nameof(Title));

    public static readonly StyledProperty<string?> NoteProperty =
        AvaloniaProperty.Register<AgentTasksCard, string?>(nameof(Note));

    public static readonly StyledProperty<string?> AgentProperty =
        AvaloniaProperty.Register<AgentTasksCard, string?>(nameof(Agent));

    public static readonly StyledProperty<string?> StatusTextProperty =
        AvaloniaProperty.Register<AgentTasksCard, string?>(nameof(StatusText));

    public static readonly StyledProperty<string?> UpdatedTextProperty =
        AvaloniaProperty.Register<AgentTasksCard, string?>(nameof(UpdatedText));

    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<AgentTasksCard, double>(nameof(Progress));

    public static readonly StyledProperty<string?> ProgressTextProperty =
        AvaloniaProperty.Register<AgentTasksCard, string?>(nameof(ProgressText));

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public string? Note { get => GetValue(NoteProperty); set => SetValue(NoteProperty, value); }

    public string? Agent { get => GetValue(AgentProperty); set => SetValue(AgentProperty, value); }

    public string? StatusText { get => GetValue(StatusTextProperty); set => SetValue(StatusTextProperty, value); }

    public string? UpdatedText { get => GetValue(UpdatedTextProperty); set => SetValue(UpdatedTextProperty, value); }

    public double Progress { get => GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }

    public string? ProgressText { get => GetValue(ProgressTextProperty); set => SetValue(ProgressTextProperty, value); }

    public AgentTaskItem? Task { get; set; }

    public AgentTasks? Owner { get; set; }

    public void Refresh()
    {
        if (Task is not { } task)
        {
            return;
        }
        var labels = Owner?.Labels ?? new AgentTasksLabels();
        var status = task.Status;
        PseudoClasses.Set(QueuedPseudoClass, status == AgentTaskStatus.Queued);
        PseudoClasses.Set(RunningPseudoClass, status == AgentTaskStatus.Running);
        PseudoClasses.Set(NeedsInputPseudoClass, status == AgentTaskStatus.NeedsInput);
        PseudoClasses.Set(FailedPseudoClass, status == AgentTaskStatus.Failed);
        PseudoClasses.Set(DonePseudoClass, status == AgentTaskStatus.Done);
        Title = task.Title;
        Note = task.Note;
        Agent = task.Agent;
        StatusText = labels.Status(status);
        Progress = Math.Clamp(task.Progress, 0, 1);
        ProgressText = labels.Percent(Progress);
        UpdatedText = task.Updated is { } updated ? labels.Updated(updated, Owner?.Clock() ?? DateTimeOffset.Now) : null;
        var detail = status == AgentTaskStatus.Running ? $"{ProgressText}{(task.Note is null ? null : ", " + task.Note)}" : task.Note;
        AutomationProperties.SetName(this, labels.CardName(task.Title, task.Agent, StatusText, detail));
        AutomationProperties.SetHelpText(this, labels.OpenHint);
    }

    protected override void OnClick()
    {
        base.OnClick();
        if (Task is { } task)
        {
            Owner?.OpenTask(task);
        }
    }
}

/// <summary>An agent filter chip of an <see cref="AgentTasks"/> board.</summary>
[PseudoClasses(SelectedPseudoClass)]
internal sealed class AgentTasksChip : AiAgentButton
{
    public const string SelectedPseudoClass = ":selected";

    public string? Agent { get; init; }

    public bool IsSelected
    {
        get => PseudoClasses.Contains(SelectedPseudoClass);
        set => PseudoClasses.Set(SelectedPseudoClass, value);
    }
}
