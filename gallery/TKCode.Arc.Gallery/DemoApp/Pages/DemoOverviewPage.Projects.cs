using System.Text;
using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.DemoApp;

public sealed partial class DemoOverviewPage
{
    private static readonly (string Value, string Label, DemoProjectStatus Status)[] s_statuses =
    [
        ("at-risk", "At risk", DemoProjectStatus.AtRisk), ("review", "In review", DemoProjectStatus.Review),
        ("on-track", "On track", DemoProjectStatus.OnTrack), ("shipped", "Shipped", DemoProjectStatus.Shipped)
    ];

    private readonly List<OverviewProject> _projects = OverviewData.Projects.ToList();
    private readonly TextBlock _projectsSubtitle = Subtitle();
    private readonly TextBlock _projectsCount = DemoUi.Text(string.Empty, ArcDesignTokenKind.TextMuted);
    private readonly Table _table = new()
    {
        Caption = "Projects", ItemNameOne = "project", ItemNameOther = "projects", IsSelectable = false, FontSize = 13,
        EmptyMessage = "No projects match these filters. Clear a filter to see more.",
        RowKeySelector = item => ((OverviewProject)item).Id
    };
    private readonly Drawer _drawer = new() { Side = DrawerSide.Right };
    private ChipGroup _statusFilter = null!;
    private OverviewProject? _drawerProject;
    private Control? _drawerTrigger;
    private bool? _tableNarrow;
    private string? _requesting;

    /// <summary>Projects: status chips over the sortable table; a project's name opens its drawer.</summary>
    private Control BuildProjects()
    {
        var export = DemoUi.Button("Export", ButtonVariant.Ghost, ArcIcons.Download);
        export.Click += async (_, _) => await ExportAsync();

        _statusFilter = new ChipGroup { IsMultiple = true, Label = "Filter projects by status" };
        foreach (var (value, label, _) in s_statuses)
        {
            _statusFilter.Items.Add(new SelectItem { Content = label, Value = value });
        }
        _statusFilter.SelectionChanged += (_, _) => RefreshProjects();
        var filters = new OverviewSpread { Gap = 24, RowGap = 8 };
        filters.Children.Add(_statusFilter);
        filters.Children.Add(_projectsCount);

        _drawer.Closed += (_, _) =>
        {
            if (_drawerTrigger is { } trigger && TopLevel.GetTopLevel(trigger) is not null)
            {
                trigger.Focus(NavigationMethod.Tab);
            }
        };
        // The table and the drawer share a cell: the drawer takes no room while it is closed.
        var tableHost = new Grid();
        tableHost.Children.Add(_table);
        tableHost.Children.Add(_drawer);
        _onViewport.Add(width =>
        {
            var narrow = width <= 620;
            if (narrow != _tableNarrow)
            {
                _tableNarrow = narrow;
                BuildColumns(narrow);
            }
        });
        BuildColumns(false);
        RefreshProjects();
        return Section(SectionHead("Projects", _projectsSubtitle, export), filters, tableHost);
    }

    private IReadOnlyList<string> SelectedStatuses => _statusFilter.SelectedValues.OfType<string>().ToArray();

    private IEnumerable<OverviewProject> FilteredProjects()
    {
        var statuses = SelectedStatuses;
        return statuses.Count == 0 ? _projects : _projects.Where(project => statuses.Contains(StatusValue(project.Status)));
    }

    private static string StatusValue(DemoProjectStatus status) => s_statuses.First(entry => entry.Status == status).Value;

    /// <summary>Re-reads the projects into the table, the counts and the open drawer after any change.</summary>
    private void RefreshProjects()
    {
        var rows = FilteredProjects().ToArray();
        _table.ItemsSource = rows;
        var active = _projects.Count(project => project.Status != DemoProjectStatus.Shipped);
        var risky = _projects.Count(project => project.Status == DemoProjectStatus.AtRisk);
        _projectsSubtitle.Text = $"{active} in progress" + (risky > 0 ? $", {risky} at risk" : string.Empty);
        _projectsCount.Text = SelectedStatuses.Count == 0 ? $"All {_projects.Count} projects" : $"{rows.Length} of {_projects.Count} projects";
    }

    private void BuildColumns(bool narrow)
    {
        _table.Columns.Clear();
        _table.Columns.Add(new DataTableColumn("Project", item => ((OverviewProject)item).Name) { Key = "name", CellTemplate = Cell(ProjectCell) });
        if (!narrow)
        {
            _table.Columns.Add(new DataTableColumn("Lead", item => OverviewData.Person(((OverviewProject)item).Lead).Name)
            {
                Key = "lead", CellTemplate = Cell(LeadCell)
            });
            _table.Columns.Add(new DataTableColumn("Team", item => string.Join(", ", ((OverviewProject)item).Team))
            {
                Key = "team", IsSortable = false, IsNumeric = false, Width = new GridLength(120), CellTemplate = Cell(TeamCell)
            });
        }
        _table.Columns.Add(new DataTableColumn("Status", item => DemoData.Label(((OverviewProject)item).Status))
        {
            Key = "status", Width = new GridLength(124), CellTemplate = Cell(project => StatusBadge(project.Status))
        });
        _table.Columns.Add(new DataTableColumn("Progress", item => ((OverviewProject)item).Progress)
        {
            Key = "progress", IsNumeric = true, Width = new GridLength(144), CellTemplate = Cell(ProgressCell)
        });
        _table.Columns.Add(new DataTableColumn("Due", item => ((OverviewProject)item).Due)
        {
            Key = "due", Width = new GridLength(88),
            CellTemplate = Cell(project => DemoUi.Text(project.Due.ToString("MMM d", OverviewData.En), ArcDesignTokenKind.TextSecondary))
        });
        _table.Columns.Add(new DataTableColumn("Budget", item => ((OverviewProject)item).Budget)
        {
            Key = "budget", IsNumeric = true, Width = new GridLength(104),
            Formatter = value => value is int budget ? budget.ToString("C0", OverviewData.En) : string.Empty
        });
    }

    private static FuncDataTemplate<OverviewProject> Cell(Func<OverviewProject, Control> build) => new((_, _) => new ProjectCellHost(build));

    private Control ProjectCell(OverviewProject project)
    {
        var name = new OverviewLink(project.Name);
        Avalonia.Automation.AutomationProperties.SetName(name, $"{project.Name}, {project.Client}");
        name.Click += (_, _) => OpenProject(_projects.FirstOrDefault(p => p.Id == project.Id) ?? project, name);
        var cell = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        cell.Children.Add(name);
        cell.Children.Add(DemoUi.Line(project.Client, ArcDesignTokenKind.TextMuted, 12));
        return cell;
    }

    private static Control LeadCell(OverviewProject project)
    {
        var person = OverviewData.Person(project.Lead);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(new Avatar { DisplayName = person.Name, Source = DemoPhotos.For(person.Name), Size = AvatarSize.Small });
        row.Children.Add(PersonCard(person, DemoUi.Line(person.Name, ArcDesignTokenKind.Foreground)));
        return row;
    }

    private static Control TeamCell(OverviewProject project)
    {
        var group = new AvatarGroup { Size = AvatarSize.Small, MaxVisibleCount = 3, Label = $"{project.Name} team", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(8, 4) };
        foreach (var id in project.Team)
        {
            group.Items.Add(new AvatarGroupMember { DisplayName = OverviewData.Person(id).Name, Source = DemoPhotos.For(OverviewData.Person(id).Name) });
        }
        return group;
    }

    private static Badge StatusBadge(DemoProjectStatus status) => new()
    {
        Content = DemoData.Label(status), Tone = DemoData.Tone(status), Size = BadgeSize.Small,
        HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center
    };

    private static Control ProgressCell(OverviewProject project)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        row.Children.Add(Pill(56, project.Progress / 100.0, project.Progress >= 100 ? ArcDesignTokenKind.Success : ArcDesignTokenKind.Accent));
        var value = DemoUi.Text($"{project.Progress}%");
        value.MinWidth = 32;
        value.TextAlignment = TextAlignment.Right;
        row.Children.Add(value);
        return row;
    }

    /// <summary>A person's name that shows their profile card on hover or focus.</summary>
    private static HoverCard PersonCard(OverviewPerson person, Control trigger)
    {
        var meta = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        meta.Children.Add(DemoUi.Glyph(s_mapPin, 13));
        meta.Children.Add(DemoUi.Text(person.Place, ArcDesignTokenKind.TextMuted, 12));
        var profile = new HoverCardProfile { DisplayName = person.Name, Role = person.Role, Bio = person.Bio, Meta = meta };
        var projects = OverviewData.Projects.Count(project => project.Status != DemoProjectStatus.Shipped && project.Team.Contains(person.Id));
        profile.Stats.Add(new HoverCardStat("Projects", projects.ToString(OverviewData.En)));
        profile.Stats.Add(new HoverCardStat("This week", $"{person.Hours} h"));
        profile.Stats.Add(new HoverCardStat("Reviews", person.Reviews.ToString(OverviewData.En)));
        trigger.Focusable = true;
        trigger.Cursor = new Cursor(StandardCursorType.Hand);
        return new HoverCard { Trigger = trigger, Content = profile, VerticalAlignment = VerticalAlignment.Center };
    }

    private static readonly Geometry s_mapPin =
        ArcIcons.Parse("M20 10c0 4.993-5.539 10.193-7.399 11.799a1 1 0 0 1-1.202 0C9.539 20.193 4 14.993 4 10a8 8 0 0 1 16 0 M9 10a3 3 0 1 0 6 0a3 3 0 1 0 -6 0z");

    /// <summary>Saves the filtered rows as northstar-projects.csv (a save dialog stands in for the browser's download).</summary>
    private async Task ExportAsync()
    {
        var rows = FilteredProjects().ToArray();
        var csv = new StringBuilder();
        static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
        csv.AppendLine(string.Join(",", new[] { "Project", "Client", "Lead", "Status", "Progress", "Due", "Budget" }.Select(Quote)));
        foreach (var project in rows)
        {
            csv.AppendLine(string.Join(",", new[]
            {
                project.Name, project.Client, OverviewData.Person(project.Lead).Name, DemoData.Label(project.Status), $"{project.Progress}%",
                project.Due.ToString("yyyy-MM-dd"), project.Budget.ToString(OverviewData.En)
            }.Select(Quote)));
        }
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { CanSave: true } storage)
        {
            return;
        }
        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = "northstar-projects.csv", DefaultExtension = "csv", ShowOverwritePrompt = true
        });
        if (file is null)
        {
            return;
        }
        await using (var stream = await file.OpenWriteAsync())
        {
            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            await stream.WriteAsync(bytes);
        }
        Host.Toast(ToastKind.Success, $"Exported {rows.Length} {(rows.Length == 1 ? "project" : "projects")}", "northstar-projects.csv is in your downloads.");
    }

    #region Drawer

    private void OpenProject(OverviewProject project, Control? trigger)
    {
        _drawerProject = project;
        _drawerTrigger = trigger;
        _drawer.Title = project.Name;
        _drawer.Description = $"{project.Client} · led by {OverviewData.Person(project.Lead).Name}";
        _drawer.Content = DrawerBody(project);
        _drawer.IsOpen = true;
    }

    /// <summary>The project sheet: status and due date, summary, progress, money, team, milestones and actions.</summary>
    private Control DrawerBody(OverviewProject project)
    {
        var body = new StackPanel { Spacing = 24 };

        var status = new WrapPanel { ItemSpacing = 12, LineSpacing = 8 };
        status.Children.Add(StatusBadge(project.Status));
        var days = (project.Due - DemoData.Now.Date).Days;
        var when = project.Status == DemoProjectStatus.Shipped
            ? $"Shipped {project.Due.ToString("MMM d", OverviewData.En)}"
            : $"Due {project.Due.ToString("MMM d", OverviewData.En)} · {days} {(days == 1 ? "day" : "days")} left";
        var whenText = DemoUi.Text(when, ArcDesignTokenKind.TextSecondary);
        whenText.VerticalAlignment = VerticalAlignment.Center;
        status.Children.Add(whenText);
        body.Children.Add(status);

        var summary = DemoUi.Text(project.Summary, ArcDesignTokenKind.Foreground, 15);
        summary.LineHeight = 22;
        body.Children.Add(summary);

        body.Children.Add(new Progress { Value = project.Progress, Maximum = 100, Label = "Progress", IsValueVisible = true });

        var money = new Grid();
        OverviewFact.Layout(money,
        [
            Fact("Budget", FactValue(project.Budget.ToString("C0", OverviewData.En))),
            Fact("Spent", FactValue(project.Spent.ToString("C0", OverviewData.En))),
            Fact("Remaining", FactValue((project.Budget - project.Spent).ToString("C0", OverviewData.En)))
        ], OverviewFactsMode.Columns);
        var moneyBox = new Border { Child = money, BorderThickness = new Thickness(0, 1, 0, 0) };
        ArcTokens.Bind(moneyBox, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        body.Children.Add(moneyBox);

        var team = new StackPanel { Spacing = 12 };
        foreach (var id in project.Team)
        {
            var person = OverviewData.Person(id);
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            row.Children.Add(new Avatar
            {
                DisplayName = person.Name, Source = DemoPhotos.For(person.Name), Size = AvatarSize.Small, Status = person.Online ? AvatarStatus.Online : AvatarStatus.Offline
            });
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(DemoUi.Text(person.Name, ArcDesignTokenKind.Foreground, 14, FontWeight.Medium));
            text.Children.Add(DemoUi.Text(id == project.Lead ? $"Lead · {person.Role}" : person.Role, ArcDesignTokenKind.TextMuted, 12));
            row.Children.Add(text);
            team.Children.Add(row);
        }
        body.Children.Add(DrawerGroup("Team", team));
        body.Children.Add(DrawerGroup("Milestones", Milestones(project.Milestones)));

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 4, 0, 0) };
        if (project.Status != DemoProjectStatus.Shipped)
        {
            var review = project.Status == DemoProjectStatus.Review;
            var requesting = _requesting == project.Id;
            var request = new Button
            {
                Variant = ButtonVariant.Primary, Size = ControlSize.Small, IsEnabled = !review, IsLoading = requesting,
                Content = review ? "Waiting on client review" : requesting ? "Requesting" : "Request client review"
            };
            request.Click += (_, _) => RequestReview(project.Id, request);
            actions.Children.Add(request);
        }
        actions.Children.Add(new CopyButton { Value = $"https://northstar.studio/projects/{project.Id}", Label = "Copy link" });
        body.Children.Add(actions);
        return body;
    }

    private static Control DrawerGroup(string title, Control content)
    {
        var group = new StackPanel { Spacing = 10 };
        group.Children.Add(DemoUi.Text(title, ArcDesignTokenKind.TextMuted, 12));
        group.Children.Add(content);
        return group;
    }

    /// <summary>The milestone list (no Arc equivalent): a 20px mark, the label and the date, joined by a 1px connector.</summary>
    private static Control Milestones(IReadOnlyList<OverviewMilestone> milestones)
    {
        var list = new StackPanel();
        for (var i = 0; i < milestones.Count; i++)
        {
            var milestone = milestones[i];
            var mark = new Border
            {
                Width = 20, Height = 20, CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center
            };
            ArcTokens.Bind(mark, Border.BorderBrushProperty, milestone.Done ? ArcDesignTokenKind.Foreground : ArcDesignTokenKind.BorderStrong);
            if (milestone.Done)
            {
                ArcTokens.Bind(mark, Border.BackgroundProperty, ArcDesignTokenKind.Foreground);
                var check = DemoUi.Glyph(ArcIcons.Check, 12, ArcDesignTokenKind.Background);
                check.StrokeThickness = 2.5;
                check.HorizontalAlignment = HorizontalAlignment.Center;
                mark.Child = check;
            }
            var label = DemoUi.Text(milestone.Label, milestone.Done ? ArcDesignTokenKind.Foreground : ArcDesignTokenKind.TextSecondary, 14);
            label.VerticalAlignment = VerticalAlignment.Center;
            var date = DemoUi.Text(milestone.Date.ToString("MMM d", OverviewData.En), ArcDesignTokenKind.TextMuted, 12);
            date.VerticalAlignment = VerticalAlignment.Center;
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("20,*,Auto"), ColumnSpacing = 12, MinHeight = 40 };
            Avalonia.Automation.AutomationProperties.SetName(row, $"{milestone.Label}, {(milestone.Done ? "done" : "upcoming")}, {date.Text}");
            if (i < milestones.Count - 1)
            {
                // The connector runs from under this mark to the next one: 20px tall at x = 9.5.
                var connector = new Border
                {
                    Width = 1, Height = 20, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(9, 30, 0, -10)
                };
                ArcTokens.Bind(connector, Border.BackgroundProperty, ArcDesignTokenKind.BorderStrong);
                row.Children.Add(connector);
            }
            row.Children.Add(mark);
            Grid.SetColumn(label, 1);
            Grid.SetColumn(date, 2);
            row.Children.Add(label);
            row.Children.Add(date);
            list.Children.Add(row);
        }
        return list;
    }

    /// <summary>"Request client review": 900 ms of loading, then the project is in review everywhere it shows.</summary>
    private void RequestReview(string id, Button button)
    {
        if (_requesting is not null)
        {
            return;
        }
        _requesting = id;
        button.IsLoading = true;
        button.Content = "Requesting";
        DispatcherTimer.RunOnce(() =>
        {
            _requesting = null;
            var index = _projects.FindIndex(project => project.Id == id);
            if (index < 0)
            {
                return;
            }
            var project = _projects[index] with { Status = DemoProjectStatus.Review };
            _projects[index] = project;
            PushEvent(new TimelineEvent($"e-review-{id}", default, $"requested a client review of {project.Name}", "Emma Collins") { Meta = project.Client });
            RefreshProjects();
            if (_drawerProject?.Id == id)
            {
                _drawerProject = project;
                _drawer.Content = DrawerBody(project);
            }
            Host.Toast(ToastKind.Success, $"Review requested from {project.Client}", "They would get a link to the latest frames.");
        }, TimeSpan.FromMilliseconds(900));
    }

    #endregion

    /// <summary>A table cell that rebuilds its content for the row it shows (cells are recycled between rows).</summary>
    private sealed class ProjectCellHost : ContentControl
    {
        private readonly Func<OverviewProject, Control> _build;

        public ProjectCellHost(Func<OverviewProject, Control> build)
        {
            _build = build;
            VerticalAlignment = VerticalAlignment.Center;
            HorizontalContentAlignment = HorizontalAlignment.Stretch;
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            Content = DataContext is OverviewProject project ? _build(project) : null;
        }
    }
}

/// <summary>A link-style button: medium-weight text that underlines on hover and focus (the project names).</summary>
internal sealed class OverviewLink : Border
{
    private readonly TextBlock _text;

    public OverviewLink(string text)
    {
        _text = DemoUi.Line(text, ArcDesignTokenKind.Foreground, DemoUi.BodySize, FontWeight.Medium);
        Child = _text;
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.Hand);
        Background = Brushes.Transparent;
        HorizontalAlignment = HorizontalAlignment.Left;
    }

    public event EventHandler? Click;

    private void Underline() => _text.TextDecorations = IsPointerOver || IsFocused ? TextDecorations.Underline : null;

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        Underline();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        Underline();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsFocusedProperty)
        {
            Underline();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.InitialPressMouseButton == MouseButton.Left && IsPointerOver)
        {
            e.Handled = true;
            Click?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key is Key.Enter or Key.Space)
        {
            e.Handled = true;
            Click?.Invoke(this, EventArgs.Empty);
        }
    }
}
