using TKCode.Arc.DesignTokens;
using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc;

[TemplatePart(BodyPart, typeof(Decorator))]
[TemplatePart(AnnouncerPart, typeof(ProjectsAnnouncer))]
[PseudoClasses(FramedPseudoClass, CompactPseudoClass, SuggestingPseudoClass)]
public partial class TaskBreakdown
{
    public const string BodyPart = "PART_Body";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set while <see cref="IsFramed"/> is true.</summary>
    public const string FramedPseudoClass = ":framed";

    /// <summary>Set while the block is at most <c>CompactWidth</c> (600) wide: rows put the estimate and assignee under the title.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>Set while suggestions are being worked out.</summary>
    public const string SuggestingPseudoClass = ":suggesting";

    internal static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<TaskBreakdown, double>(nameof(CompactWidth), 600);

    internal double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }

    private const string SampleTitle = "Launch the customer referral program";

    private const string SampleDescription =
        "Interview five customers about how they recommend us. Design the invite screen and the reward email. "
        + "Build the referral links and the tracking. Test the reward rules with finance. Write the help article, then release to 10% of accounts.";

    private bool _compact;
    private bool _syncing;
    private bool _samplesChecked;
    private CancellationTokenSource? _suggestion;

    private Decorator? _body;
    private ProjectsAnnouncer? _announcer;
    private Input? _titleField;
    private Textarea? _descriptionField;
    private Button? _suggestButton;
    private TextBlock? _failure;
    private ReorderableList? _list;
    private TextBlock? _empty;
    private TextBlock? _total;
    private WrapPanel? _loads;
    private Button? _create;

    static TaskBreakdown()
    {
        FocusableProperty.OverrideDefaultValue<TaskBreakdown>(false);
    }

    public TaskBreakdown()
    {
        PseudoClasses.Set(FramedPseudoClass, true);
        Subtasks.CollectionChanged += OnSubtasksChanged;
        People.CollectionChanged += (_, _) => RebuildList();
    }

    internal ReorderableList? List => _list;

    internal Button? SuggestButton => _suggestButton;

    internal Button? CreateButton => _create;

    internal ProjectsAnnouncer? Announcer => _announcer;

    internal TextBlock? TotalText => _total;

    internal TextBlock? Failure => _failure;

    internal bool IsCompact => _compact;

    /// <summary>
    /// Asks for subtasks for the current title and description (as the Suggest button does) and replaces the list with
    /// them. Returns false when it was cancelled by a newer request or failed.
    /// </summary>
    public async Task<bool> SuggestAsync()
    {
        _suggestion?.Cancel();
        var cancellation = new CancellationTokenSource();
        _suggestion = cancellation;
        var request = new TaskBreakdownRequest(TaskTitle ?? string.Empty, Description ?? string.Empty, People.ToArray());
        IsSuggesting = true;
        SetFailure(null);
        try
        {
            IReadOnlyList<TaskBreakdownSubtask> result = SuggestAction is { } action
                ? await action(request, cancellation.Token)
                : TaskBreakdownSuggester.Suggest(request);
            if (cancellation.IsCancellationRequested)
            {
                return false;
            }
            Subtasks.Clear();
            Subtasks.AddRange(result);
            _announcer?.Announce(string.Format(CultureInfo.CurrentCulture, Labels.SuggestedFormat, result.Count));
            return true;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception)
        {
            SetFailure(Labels.SuggestFailed);
            return false;
        }
        finally
        {
            if (ReferenceEquals(_suggestion, cancellation))
            {
                _suggestion = null;
                IsSuggesting = false;
            }
            cancellation.Dispose();
        }
    }

    /// <summary>Adds an empty subtask at the end and puts the focus in its title.</summary>
    public TaskBreakdownSubtask AddSubtask()
    {
        var subtask = new TaskBreakdownSubtask(string.Empty, 2);
        Subtasks.Add(subtask);
        Dispatcher.UIThread.Post(() =>
        {
            if (_list?.ContainerFromItem(subtask) is Control container)
            {
                container.GetLogicalDescendants().OfType<Input>().FirstOrDefault()?.Focus(NavigationMethod.Tab);
            }
        }, DispatcherPriority.Background);
        return subtask;
    }

    /// <summary>Raises <see cref="Created"/> with the subtasks that have a title, in order, and announces it. Returns them.</summary>
    public IReadOnlyList<TaskBreakdownSubtask> Create()
    {
        var subtasks = Subtasks.Where(s => !string.IsNullOrWhiteSpace(s.Title)).ToList();
        if (subtasks.Count == 0)
        {
            return subtasks;
        }
        _announcer?.Announce(string.Format(CultureInfo.CurrentCulture, Labels.CreatedFormat, subtasks.Count));
        RaiseEvent(new TaskBreakdownCreatedEventArgs(CreatedEvent, TaskTitle ?? string.Empty, subtasks));
        return subtasks;
    }

    private void LoadSamplesOnce()
    {
        if (_samplesChecked)
        {
            return;
        }
        _samplesChecked = true;
        if (!IsSampleDataEnabled || TaskTitle is not null || Description is not null || People.Count > 0 || Subtasks.Count > 0)
        {
            return;
        }
        People.AddRange([new ProjectPerson("Rina Kato"), new ProjectPerson("Samir Aziz"), new ProjectPerson("Elena Duarte")]);
        SetCurrentValue(TaskTitleProperty, SampleTitle);
        SetCurrentValue(DescriptionProperty, SampleDescription);
        Subtasks.AddRange(TaskBreakdownSuggester.Suggest(new TaskBreakdownRequest(SampleTitle, SampleDescription, People.ToArray())));
    }

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        LoadSamplesOnce();
        if (_body is not null)
        {
            _body.Child = null;
        }
        _body = e.NameScope.Find<Decorator>(BodyPart);
        _announcer = e.NameScope.Find<ProjectsAnnouncer>(AnnouncerPart);
        if (_body is not null)
        {
            _body.Child = BuildBody();
            RebuildList();
        }
    }

    private Control BuildBody()
    {
        var labels = Labels;
        var root = new StackPanel { Spacing = 16 };

        _titleField = new Input { Label = labels.TitleField, Text = TaskTitle ?? string.Empty };
        _titleField.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty && !_syncing)
            {
                SetCurrentValue(TaskTitleProperty, _titleField.Text);
            }
        };
        _descriptionField = new Textarea
        {
            Label = labels.DescriptionField,
            Placeholder = labels.DescriptionPlaceholder,
            Text = Description ?? string.Empty,
            FrameHeight = 96,
        };
        _descriptionField.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty && !_syncing)
            {
                SetCurrentValue(DescriptionProperty, _descriptionField.Text);
            }
        };
        root.Children.Add(_titleField);
        root.Children.Add(_descriptionField);

        _suggestButton = new Button
        {
            Variant = ButtonVariant.Secondary,
            Size = ControlSize.Small,
            Content = labels.Suggest,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        _suggestButton.Click += async (_, _) => await SuggestAsync();
        _failure = new TextBlock { TextWrapping = TextWrapping.Wrap, IsVisible = false, LineHeight = 16.8 };
        ArcTokens.Bind(_failure, TextBlock.ForegroundProperty, ArcDesignTokenKind.Danger);
        ArcTokens.Bind(_failure, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeXS);
        AutomationProperties.SetLiveSetting(_failure, AutomationLiveSetting.Assertive);
        var suggestRow = new StackPanel { Spacing = 6 };
        suggestRow.Children.Add(_suggestButton);
        suggestRow.Children.Add(_failure);
        root.Children.Add(suggestRow);

        var heading = new TextBlock { Text = labels.Subtasks, FontWeight = FontWeight.Medium, LineHeight = 16.8 };
        ArcTokens.Bind(heading, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        ArcTokens.Bind(heading, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeXS);
        AutomationProperties.SetHeadingLevel(heading, 3);
        root.Children.Add(heading);

        _list = new ReorderableList
        {
            Label = labels.ListName,
            ItemsSource = Subtasks,
            ItemTitleSelector = o => (o as TaskBreakdownSubtask)?.Title is { Length: > 0 } t ? t : labels.Subtasks,
            ItemTemplate = new FuncDataTemplate<TaskBreakdownSubtask>((s, _) => BuildRow(s), supportsRecycling: false),
        };
        _list.Reordered += (_, _) => RenumberRows();
        _empty = new TextBlock { Text = labels.Empty, TextWrapping = TextWrapping.Wrap, LineHeight = 19.6 };
        ArcTokens.Bind(_empty, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        ArcTokens.Bind(_empty, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeSM);
        root.Children.Add(_list);
        root.Children.Add(_empty);

        var add = new Button { Variant = ButtonVariant.Ghost, Size = ControlSize.Small, HorizontalAlignment = HorizontalAlignment.Left };
        var addContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        addContent.Children.Add(new ArcGlyph { Data = ArcIcons.Plus, Size = 16, VerticalAlignment = VerticalAlignment.Center });
        addContent.Children.Add(new TextBlock { Text = labels.Add, VerticalAlignment = VerticalAlignment.Center });
        add.Content = addContent;
        AutomationProperties.SetName(add, labels.Add);
        add.Click += (_, _) => AddSubtask();
        root.Children.Add(add);

        // Footer: the total, each person's load and the create button.
        var footer = new Border { Padding = new Thickness(0, 14, 0, 0), BorderThickness = new Thickness(0, 1, 0, 0) };
        ArcTokens.Bind(footer, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        _total = new TextBlock { FontWeight = FontWeight.Medium, LineHeight = 19.6, FontFeatures = new FontFeatureCollection { FontFeature.Parse("tnum") } };
        ArcTokens.Bind(_total, TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(_total, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeSM);
        AutomationProperties.SetLiveSetting(_total, AutomationLiveSetting.Polite);
        _loads = new WrapPanel { ItemSpacing = 6, LineSpacing = 6 };
        var summary = new StackPanel { Spacing = 8 };
        summary.Children.Add(_total);
        summary.Children.Add(_loads);
        _create = new Button { Variant = ButtonVariant.Primary, Size = ControlSize.Small, VerticalAlignment = VerticalAlignment.Top };
        _create.Click += (_, _) => Create();
        var footerGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        footerGrid.Children.Add(summary);
        Grid.SetColumn(_create, 1);
        footerGrid.Children.Add(_create);
        footer.Child = footerGrid;
        root.Children.Add(footer);
        return root;
    }

    private Control BuildRow(TaskBreakdownSubtask? subtask)
    {
        if (subtask is null)
        {
            return new Panel();
        }
        var labels = Labels;
        var index = Subtasks.IndexOf(subtask) + 1;
        var number = new TextBlock
        {
            Text = index.ToString(CultureInfo.CurrentCulture),
            Width = 20,
            VerticalAlignment = VerticalAlignment.Center,
            FontFeatures = new FontFeatureCollection { FontFeature.Parse("tnum") },
            Tag = "number",
        };
        ArcTokens.Bind(number, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        ArcTokens.Bind(number, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeXS);

        var title = new Input { Size = ControlSize.Small, Text = subtask.Title, Placeholder = string.Format(CultureInfo.CurrentCulture, labels.SubtaskFormat, index) };
        AutomationProperties.SetName(title, string.Format(CultureInfo.CurrentCulture, labels.SubtaskFormat, index));
        title.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty && title.Text != subtask.Title)
            {
                subtask.Title = title.Text ?? string.Empty;
                subtask.IsSuggested = false;
                UpdateSummary();
            }
        };

        var estimate = new NumberField
        {
            Size = ControlSize.Small,
            Width = 108,
            Minimum = 0,
            Maximum = 999,
            Step = 0.5m,
            MaximumFractionDigits = 1,
            Value = (decimal)subtask.EstimateHours,
            SuffixFormatter = _ => labels.HoursUnit,
        };
        AutomationProperties.SetName(estimate, labels.Estimate);
        estimate.PropertyChanged += (_, e) =>
        {
            if (e.Property == NumberField.ValueProperty)
            {
                subtask.EstimateHours = (double)estimate.Value;
                UpdateSummary();
            }
        };

        var options = new List<ProjectPerson?> { null };
        options.AddRange(People);
        var assignee = new Select
        {
            Size = ControlSize.Small,
            Width = 150,
            ItemsSource = options,
            TextSelector = o => (o as ProjectPerson)?.Name ?? labels.Unassigned,
            SelectedIndex = subtask.Assignee is null ? 0 : Math.Max(0, People.IndexOf(subtask.Assignee) + 1),
        };
        AutomationProperties.SetName(assignee, labels.Assignee);
        assignee.SelectionChanged += (_, _) =>
        {
            subtask.Assignee = assignee.SelectedItem as ProjectPerson;
            UpdateSummary();
        };

        var remove = new Button { Variant = ButtonVariant.Ghost, Size = ControlSize.Small, Width = 36, Padding = default };
        remove.Content = new ArcGlyph { Data = ArcIcons.X, Size = 16 };
        AutomationProperties.SetName(remove, string.Format(CultureInfo.CurrentCulture, labels.RemoveFormat,
            string.IsNullOrEmpty(subtask.Title) ? string.Format(CultureInfo.CurrentCulture, labels.SubtaskFormat, index) : subtask.Title));
        remove.Click += (_, _) => Subtasks.Remove(subtask);

        var grid = new Grid { ColumnSpacing = 8, RowSpacing = 6, VerticalAlignment = VerticalAlignment.Center };
        if (_compact)
        {
            grid.ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto");
            grid.RowDefinitions = new RowDefinitions("Auto,Auto");
            var details = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            details.Children.Add(estimate);
            details.Children.Add(assignee);
            Grid.SetColumn(details, 1);
            Grid.SetRow(details, 1);
            grid.Children.Add(details);
            Grid.SetColumn(remove, 2);
        }
        else
        {
            grid.ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto,Auto");
            Grid.SetColumn(estimate, 2);
            Grid.SetColumn(assignee, 3);
            Grid.SetColumn(remove, 4);
            grid.Children.Add(estimate);
            grid.Children.Add(assignee);
        }
        grid.Children.Add(number);
        Grid.SetColumn(title, 1);
        grid.Children.Add(title);
        grid.Children.Add(remove);
        return grid;
    }

    #endregion

    #region Updates

    private void OnSubtasksChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Move)
        {
            RenumberRows();
        }
        else
        {
            Dispatcher.UIThread.Post(RenumberRows, DispatcherPriority.Background);
        }
        UpdateSummary();
    }

    private void RebuildList()
    {
        if (_list is null)
        {
            return;
        }
        _list.ItemsSource = null;
        _list.ItemsSource = Subtasks;
        UpdateSummary();
    }

    /// <summary>After a move or a removal the rows keep their controls; only their numbers and placeholders change.</summary>
    private void RenumberRows()
    {
        if (_list is null)
        {
            return;
        }
        var labels = Labels;
        for (var i = 0; i < Subtasks.Count; i++)
        {
            if (_list.ContainerFromItem(Subtasks[i]) is not Control container)
            {
                continue;
            }
            var text = (i + 1).ToString(CultureInfo.CurrentCulture);
            foreach (var number in container.GetLogicalDescendants().OfType<TextBlock>().Where(t => Equals(t.Tag, "number")))
            {
                number.Text = text;
            }
            if (container.GetLogicalDescendants().OfType<Input>().FirstOrDefault() is { } input)
            {
                var name = string.Format(CultureInfo.CurrentCulture, labels.SubtaskFormat, i + 1);
                input.Placeholder = name;
                AutomationProperties.SetName(input, name);
            }
        }
    }

    private void UpdateSummary()
    {
        TotalHours = Subtasks.Sum(s => Math.Max(0, s.EstimateHours));
        if (_total is null || _loads is null || _create is null || _empty is null || _suggestButton is null)
        {
            return;
        }
        var labels = Labels;
        var days = HoursPerDay > 0 ? Math.Ceiling(TotalHours / HoursPerDay * 2) / 2 : 0;
        _total.Text = string.Format(CultureInfo.CurrentCulture, labels.TotalFormat,
            TotalHours.ToString("0.#", CultureInfo.CurrentCulture), days.ToString("0.#", CultureInfo.CurrentCulture));
        _loads.Children.Clear();
        foreach (var group in Subtasks.GroupBy(s => s.Assignee).OrderBy(g => g.Key is null))
        {
            var hours = group.Sum(s => Math.Max(0, s.EstimateHours));
            var chip = new Badge
            {
                Size = BadgeSize.Small,
                Tone = group.Key is null ? BadgeTone.Warning : BadgeTone.Neutral,
                Content = (group.Key?.DisplayShortName ?? labels.Unassigned) + " " + hours.ToString("0.#", CultureInfo.CurrentCulture) + " " + labels.HoursUnit,
            };
            _loads.Children.Add(chip);
        }
        var count = Subtasks.Count(s => !string.IsNullOrWhiteSpace(s.Title));
        _create.Content = string.Format(CultureInfo.CurrentCulture, labels.CreateFormat, count);
        _create.IsEnabled = count > 0;
        _empty.IsVisible = Subtasks.Count == 0;
        _list!.IsVisible = Subtasks.Count > 0;
        _suggestButton.Content = Subtasks.Count > 0 ? labels.SuggestAgain : labels.Suggest;
    }

    private void SetFailure(string? text)
    {
        if (_failure is null)
        {
            return;
        }
        _failure.Text = text;
        _failure.IsVisible = text is not null;
    }

    #endregion

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TaskTitleProperty && _titleField is not null && _titleField.Text != (TaskTitle ?? string.Empty))
        {
            _syncing = true;
            _titleField.Text = TaskTitle ?? string.Empty;
            _syncing = false;
        }
        else if (change.Property == DescriptionProperty && _descriptionField is not null && _descriptionField.Text != (Description ?? string.Empty))
        {
            _syncing = true;
            _descriptionField.Text = Description ?? string.Empty;
            _syncing = false;
        }
        else if (change.Property == IsSuggestingProperty)
        {
            PseudoClasses.Set(SuggestingPseudoClass, IsSuggesting);
            if (_suggestButton is not null)
            {
                _suggestButton.IsLoading = IsSuggesting;
            }
        }
        else if (change.Property == HoursPerDayProperty)
        {
            UpdateSummary();
        }
        else if (change.Property == LabelsProperty)
        {
            if (_body is not null)
            {
                _body.Child = BuildBody();
                RebuildList();
            }
        }
        else if (change.Property == IsFramedProperty)
        {
            PseudoClasses.Set(FramedPseudoClass, IsFramed);
        }
        else if (change.Property == CompactWidthProperty)
        {
            UpdateCompact(Bounds.Width);
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateCompact(e.NewSize.Width);
    }

    private void UpdateCompact(double width)
    {
        var compact = width > 0 && width <= CompactWidth;
        if (compact == _compact)
        {
            return;
        }
        _compact = compact;
        PseudoClasses.Set(CompactPseudoClass, compact);
        RebuildList();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _suggestion?.Cancel();
    }
}
