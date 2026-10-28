using System.Globalization;
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

/// <summary>
/// The Projects page's List view (uiarc.dev's ProjectsList): a surface with a head (the count in progress and at risk,
/// Export and New project), status filter chips with the shown count, and the projects table. A project's name opens
/// its drawer (<see cref="DemoProjectsList"/>'s Sheet part).
/// </summary>
public sealed partial class DemoProjectsList : UserControl
{
    /// <summary>Below this width (uiarc.dev's 620px viewport, less the page gutters) the Lead and Team columns go.</summary>
    private const double NarrowWidth = 588;

    private static readonly string[] s_chipLabels = ["At risk", "In review", "On track", "Shipped"];

    private static readonly DemoProjectStatus[] s_chipStatuses =
        [DemoProjectStatus.AtRisk, DemoProjectStatus.Review, DemoProjectStatus.OnTrack, DemoProjectStatus.Shipped];

    private readonly IDemoHost _host;
    private readonly List<DemoProjectRow> _rows = DemoData.ProjectRows();
    private readonly Border _head = new();
    private readonly TextBlock _summary = DemoUi.Text(string.Empty, ArcDesignTokenKind.TextMuted);
    private readonly TextBlock _count = DemoUi.Text(string.Empty, ArcDesignTokenKind.TextMuted);
    private readonly ChipGroup _chips = new() { IsMultiple = true, VerticalAlignment = VerticalAlignment.Center };
    private readonly Table _table;
    private readonly DataTableColumn _leadColumn;
    private readonly DataTableColumn _teamColumn;
    private readonly Button _newProject;
    private bool _narrow;

    public DemoProjectsList(IDemoHost host)
    {
        _host = host;
        _summary.FontFeatures = Tabular;
        _count.FontFeatures = Tabular;

        var export = DemoUi.Button("Export", ButtonVariant.Ghost, ArcIcons.Download);
        export.Click += (_, _) => Export();
        _newProject = DemoUi.Button("New project", ButtonVariant.Primary, ArcIcons.Plus);
        _newProject.Click += (_, _) => NewProject();
        var headText = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        headText.Children.Add(DemoUi.Text("Projects", ArcDesignTokenKind.Foreground, 14, FontWeight.Medium));
        headText.Children.Add(_summary);
        var actions = DemoUi.Actions(export, _newProject);
        actions.VerticalAlignment = VerticalAlignment.Center;
        var headRow = new DockPanel();
        DockPanel.SetDock(actions, Dock.Right);
        headRow.Children.Add(actions);
        headRow.Children.Add(headText);
        _head.Child = headRow;

        // The chips are labeled for assistive technology only (uiarc.dev shows no visible label).
        Avalonia.Automation.AutomationProperties.SetName(_chips, "Filter projects by status");
        foreach (var label in s_chipLabels)
        {
            _chips.Items.Add(new SelectItem { Content = label });
        }
        _chips.SelectionChanged += (_, _) => Refresh();
        var filters = new DockPanel { Margin = new Thickness(20, 0, 20, 12) };
        _count.VerticalAlignment = VerticalAlignment.Center;
        _count.Margin = new Thickness(12, 0, 0, 0);
        DockPanel.SetDock(_count, Dock.Right);
        filters.Children.Add(_count);
        filters.Children.Add(_chips);

        _table = new Table
        {
            Caption = "Projects",
            ItemNameOne = "project",
            ItemNameOther = "projects",
            EmptyMessage = "No projects match these filters. Clear a filter to see more.",
            RowKeySelector = item => ((DemoProjectRow)item).Id,
            FontSize = DemoUi.BodySize,
            CornerRadius = new CornerRadius(10)
        };
        _leadColumn = Column("Lead", () => new LeadCell(this), row => row.Lead.Id, key: "lead");
        _teamColumn = Column("Team", () => new TeamCell(), null, key: "team", width: 120);
        _table.Columns.Add(Column("Project", () => new NameCell(this), row => row.Name, key: "name"));
        _table.Columns.Add(_leadColumn);
        _table.Columns.Add(_teamColumn);
        _table.Columns.Add(Column("Status", () => new StatusCell(), row => DemoData.StatusId(row.Status), key: "status", width: 124));
        _table.Columns.Add(Column("Progress", () => new ProgressCell(), row => row.Progress, key: "progress", width: 144, numeric: true));
        _table.Columns.Add(Column("Due", () => new DueCell(), row => row.DueIso, key: "due", width: 88));
        _table.Columns.Add(new DataTableColumn("Budget", item => ((DemoProjectRow)item).Budget)
        {
            Key = "budget",
            Width = new GridLength(104),
            IsNumeric = true,
            Formatter = value => value is decimal amount ? DemoData.Usd(amount) : string.Empty
        });
        var tableHost = new Border { Padding = new Thickness(8, 0, 8, 8), Child = _table };

        var body = new StackPanel();
        body.Children.Add(_head);
        body.Children.Add(filters);
        body.Children.Add(tableHost);
        body.Children.Add(BuildSheet());
        var surface = new Border
        {
            CornerRadius = new CornerRadius(DemoUi.CardRadius),
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
            Child = body
        };
        ArcTokens.Bind(surface, Border.BackgroundProperty, ArcDesignTokenKind.Surface);
        ArcTokens.Bind(surface, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        surface.SizeChanged += (_, e) => OnWidthChanged(e.NewSize.Width);
        Content = surface;
        Refresh();
    }

    private static FontFeatureCollection Tabular { get; } = [FontFeature.Parse("tnum")];

    /// <summary>Shows every status and opens the drawer of project <paramref name="id"/> (a search result, a deep link).</summary>
    public void Reveal(string id)
    {
        _chips.Selection.Clear();
        if (_rows.FirstOrDefault(row => row.Id == id) is { } row)
        {
            Dispatcher.UIThread.Post(() => OpenSheet(row), DispatcherPriority.Background);
        }
    }

    /// <summary>The rows the status chips let through, in data order (the table sorts them itself).</summary>
    private List<DemoProjectRow> Shown()
    {
        var statuses = _chips.Selection.SelectedIndexes.Select(index => s_chipStatuses[index]).ToHashSet();
        return statuses.Count == 0 ? _rows.ToList() : _rows.Where(row => statuses.Contains(row.Status)).ToList();
    }

    /// <summary>Re-reads the rows: the table, the head's counts and the shown count.</summary>
    private void Refresh()
    {
        var shown = Shown();
        _table.ItemsSource = shown;
        var going = _rows.Count(row => row.Status != DemoProjectStatus.Shipped);
        var risky = _rows.Count(row => row.Status == DemoProjectStatus.AtRisk);
        _summary.Text = risky > 0 ? $"{going} in progress, {risky} at risk" : $"{going} in progress";
        _count.Text = shown.Count == _rows.Count ? $"All {_rows.Count} projects" : $"{shown.Count} of {_rows.Count} projects";
    }

    private void OnWidthChanged(double width)
    {
        _head.Padding = width < 560 ? new Thickness(16, 14, 16, 12) : new Thickness(20, 16, 20, 12);
        var narrow = TopLevel.GetTopLevel(this) is { } top ? top.ClientSize.Width <= 620 : width <= NarrowWidth;
        if (narrow == _narrow)
        {
            return;
        }
        _narrow = narrow;
        if (narrow)
        {
            _table.Columns.Remove(_teamColumn);
            _table.Columns.Remove(_leadColumn);
        }
        else
        {
            _table.Columns.Insert(1, _leadColumn);
            _table.Columns.Insert(2, _teamColumn);
        }
    }

    /// <summary>Saves the shown rows as <c>northstar-projects.csv</c> in the Downloads folder (or where the user picks).</summary>
    private async void Export()
    {
        var shown = Shown();
        var csv = new StringBuilder("\"Project\",\"Client\",\"Lead\",\"Status\",\"Progress\",\"Due\",\"Budget\"\n");
        foreach (var row in shown)
        {
            string[] cells =
            [
                row.Name, row.Client, row.Lead.Name, DemoData.Label(row.Status),
                row.Progress.ToString(CultureInfo.InvariantCulture), row.DueIso, row.Budget.ToString(CultureInfo.InvariantCulture)
            ];
            csv.Append(string.Join(',', cells.Select(cell => $"\"{cell.Replace("\"", "\"\"")}\""))).Append('\n');
        }
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
        {
            return;
        }
        try
        {
            IStorageFile? file = null;
            if (await storage.TryGetWellKnownFolderAsync(WellKnownFolder.Downloads) is { } downloads)
            {
                file = await downloads.CreateFileAsync("northstar-projects.csv");
            }
            if (file is null && storage.CanSave)
            {
                file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions { SuggestedFileName = "northstar-projects.csv" });
            }
            if (file is null)
            {
                return;
            }
            await using (var stream = await file.OpenWriteAsync())
            {
                await stream.WriteAsync(Encoding.UTF8.GetBytes(csv.ToString()));
            }
            file.Dispose();
        }
        catch (Exception)
        {
            _host.Toast(ToastKind.Error, "Couldn't export the projects", "The file could not be saved.");
            return;
        }
        _host.Toast(ToastKind.Success, shown.Count == 1 ? "Exported 1 project" : $"Exported {shown.Count} projects",
                    "northstar-projects.csv is in your downloads.");
    }

    /// <summary>Adds Kestrel website after a loading toast, with an Undo that takes it out again.</summary>
    private void NewProject()
    {
        if (_rows.Any(row => row.Id == DemoData.KestrelId))
        {
            _host.Toast(ToastKind.Info, "Kestrel website already exists", "Open it from the projects list.");
            return;
        }
        if (_newProject.IsLoading)
        {
            return;
        }
        _newProject.IsLoading = true;
        var toast = _host.Toast(ToastKind.Loading, "Creating Kestrel website", "Setting up the brief and the team.");
        DispatcherTimer.RunOnce(() =>
        {
            _newProject.IsLoading = false;
            _rows.Insert(0, DemoData.Kestrel());
            _chips.Selection.Clear();
            Refresh();
            _host.UpdateToast(toast, new ToastUpdate
            {
                Kind = ToastKind.Success,
                Title = "Kestrel website created",
                Description = "Added with Olivia Bennett.",
                Action = new ToastAction("Undo", id => UndoNewProject(id))
            });
        }, TimeSpan.FromSeconds(1.1));
    }

    private void UndoNewProject(string toast)
    {
        _rows.RemoveAll(row => row.Id == DemoData.KestrelId);
        if (_sheetRow?.Id == DemoData.KestrelId)
        {
            _sheet.IsOpen = false;
        }
        Refresh();
        _host.UpdateToast(toast, new ToastUpdate
        {
            Kind = ToastKind.Info,
            Title = "Kestrel website removed",
            Description = "Removed from projects.",
            RemoveAction = true
        });
    }

    private static DataTableColumn Column(string header, Func<RowCell> cell, Func<DemoProjectRow, object?>? sort, string key,
                                          double? width = null, bool numeric = false) =>
        new(header, item => sort?.Invoke((DemoProjectRow)item))
        {
            Key = key,
            Width = width is { } pixels ? new GridLength(pixels) : GridLength.Auto,
            IsSortable = sort is not null,
            IsNumeric = numeric,
            CellTemplate = new FuncDataTemplate<DemoProjectRow>((_, _) => cell())
        };

    /// <summary>A table cell that redraws itself for the row in its data context (the table recycles cells).</summary>
    private abstract class RowCell : Border
    {
        protected RowCell()
        {
            VerticalAlignment = VerticalAlignment.Center;
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            if (DataContext is DemoProjectRow row)
            {
                Child = Build(row);
            }
        }

        protected abstract Control Build(DemoProjectRow row);
    }

    /// <summary>The project name, a link that opens the drawer, over the client.</summary>
    private sealed class NameCell(DemoProjectsList list) : RowCell
    {
        protected override Control Build(DemoProjectRow row)
        {
            var name = DemoUi.Line(row.Name, ArcDesignTokenKind.Foreground, DemoUi.BodySize, FontWeight.Medium);
            var link = new Avalonia.Controls.Button
            {
                Content = name,
                Padding = default,
                MinHeight = 0,
                Background = Brushes.Transparent,
                BorderThickness = default,
                Cursor = new Cursor(StandardCursorType.Hand),
                HorizontalAlignment = HorizontalAlignment.Left,
                Template = new FuncControlTemplate<Avalonia.Controls.Button>((button, _) =>
                    new Avalonia.Controls.Presenters.ContentPresenter { Content = button.Content, Background = Brushes.Transparent })
            };
            link.PointerEntered += (_, _) => name.TextDecorations = TextDecorations.Underline;
            link.PointerExited += (_, _) => name.TextDecorations = link.IsFocused ? TextDecorations.Underline : null;
            link.GotFocus += (_, _) => name.TextDecorations = TextDecorations.Underline;
            link.LostFocus += (_, _) => name.TextDecorations = link.IsPointerOver ? TextDecorations.Underline : null;
            link.Click += (_, _) => list.OpenSheet(row, link);
            Avalonia.Automation.AutomationProperties.SetName(link, row.Name);
            var stack = new StackPanel { Spacing = 1 };
            stack.Children.Add(link);
            stack.Children.Add(DemoUi.Line(row.Client, ArcDesignTokenKind.TextMuted, 12));
            return stack;
        }
    }

    /// <summary>The lead's avatar and name; the name opens the lead's profile card on hover or focus.</summary>
    private sealed class LeadCell(DemoProjectsList list) : RowCell
    {
        protected override Control Build(DemoProjectRow row)
        {
            var lead = row.Lead;
            var profile = DemoData.Profile(lead.Id);
            var projects = list._rows.Count(project => project.Status != DemoProjectStatus.Shipped && project.Team.Contains(lead.Id));
            var meta = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            meta.Children.Add(DemoUi.Glyph(MapPin, 14));
            meta.Children.Add(DemoUi.Text(profile.Place, ArcDesignTokenKind.TextMuted, 12));
            var card = new HoverCardProfile { DisplayName = lead.Name, Role = lead.Role, Bio = profile.Bio, Meta = meta };
            card.Stats.Add(new HoverCardStat { Label = "Projects", Value = projects.ToString(CultureInfo.InvariantCulture) });
            card.Stats.Add(new HoverCardStat { Label = "This week", Value = $"{profile.Hours} h" });
            card.Stats.Add(new HoverCardStat { Label = "Reviews", Value = profile.Reviews.ToString(CultureInfo.InvariantCulture) });
            var name = DemoUi.Line(lead.Name);
            name.VerticalAlignment = VerticalAlignment.Center;
            var trigger = new Border { Background = Brushes.Transparent, Focusable = true, Child = name, VerticalAlignment = VerticalAlignment.Center };
            var hover = new HoverCard { Trigger = trigger, Content = card, VerticalAlignment = VerticalAlignment.Center };
            var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            stack.Children.Add(new Avatar { DisplayName = lead.Name, Size = AvatarSize.Small, VerticalAlignment = VerticalAlignment.Center });
            stack.Children.Add(hover);
            return stack;
        }
    }

    private sealed class TeamCell : RowCell
    {
        protected override Control Build(DemoProjectRow row) => new AvatarGroup
        {
            ItemsSource = row.Team.Select(id => new AvatarGroupMember(DemoData.Person(id).Name)).ToList(),
            Size = AvatarSize.Small,
            MaxVisibleCount = 3,
            Label = $"{row.Name} team",
            HorizontalAlignment = HorizontalAlignment.Left
        };
    }

    private sealed class StatusCell : RowCell
    {
        protected override Control Build(DemoProjectRow row) => new Badge
        {
            Content = DemoData.Label(row.Status),
            Tone = DemoData.Tone(row.Status),
            Size = BadgeSize.Small,
            HorizontalAlignment = HorizontalAlignment.Left
        };
    }

    /// <summary>A 56 × 4 pill filled to the progress (accent, success at 100%) and the percentage.</summary>
    private sealed class ProgressCell : RowCell
    {
        public ProgressCell()
        {
            HorizontalAlignment = HorizontalAlignment.Right;
        }

        protected override Control Build(DemoProjectRow row)
        {
            var track = new Border { Width = 56, Height = 4, CornerRadius = new CornerRadius(2), Opacity = 0.09 };
            ArcTokens.Bind(track, Border.BackgroundProperty, ArcDesignTokenKind.Foreground);
            var fill = new Border
            {
                Width = 56 * row.Progress / 100.0,
                Height = 4,
                CornerRadius = new CornerRadius(2),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            ArcTokens.Bind(fill, Border.BackgroundProperty, row.Progress >= 100 ? ArcDesignTokenKind.Success : ArcDesignTokenKind.Accent);
            var bar = new Panel { Width = 56, VerticalAlignment = VerticalAlignment.Center };
            bar.Children.Add(track);
            bar.Children.Add(fill);
            var percent = DemoUi.Line($"{row.Progress}%");
            percent.FontFeatures = Tabular;
            percent.MinWidth = 32;
            percent.TextAlignment = TextAlignment.Right;
            percent.VerticalAlignment = VerticalAlignment.Center;
            var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            stack.Children.Add(bar);
            stack.Children.Add(percent);
            return stack;
        }
    }

    private sealed class DueCell : RowCell
    {
        protected override Control Build(DemoProjectRow row)
        {
            var text = DemoUi.Line(DemoData.ShortDate(row.Due), ArcDesignTokenKind.TextSecondary);
            text.FontFeatures = Tabular;
            return text;
        }
    }

    /// <summary>Lucide map-pin (not in ArcIcons).</summary>
    private static Geometry MapPin { get; } =
        ArcIcons.Parse("M20 10c0 4.993-5.539 10.193-7.399 11.799a1 1 0 0 1-1.202 0C9.539 20.193 4 14.993 4 10a8 8 0 0 1 16 0 M9 10a3 3 0 1 0 6 0a3 3 0 1 0 -6 0z");
}
