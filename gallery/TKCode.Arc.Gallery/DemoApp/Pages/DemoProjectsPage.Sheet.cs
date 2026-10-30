using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.DemoApp;

/// <summary>
/// The project drawer of the Projects list (uiarc.dev's project sheet): status and due line, summary, progress, money,
/// team, milestones, Request client review and Copy link.
/// </summary>
public sealed partial class DemoProjectsList
{
    private readonly Drawer _sheet = new() { Side = DrawerSide.Right };
    private DemoProjectRow? _sheetRow;
    private Control? _sheetOpener;

    private Drawer BuildSheet()
    {
        _sheet.Closed += (_, _) =>
        {
            _sheetRow = null;
            _sheetOpener?.Focus();
            _sheetOpener = null;
        };
        return _sheet;
    }

    /// <summary>Opens the drawer on <paramref name="row"/>; closing it returns focus to <paramref name="opener"/>.</summary>
    private void OpenSheet(DemoProjectRow row, Control? opener = null)
    {
        _sheetRow = row;
        _sheetOpener = opener;
        FillSheet(row);
        _sheet.IsOpen = true;
    }

    private void FillSheet(DemoProjectRow row)
    {
        _sheet.Title = row.Name;
        _sheet.Description = $"{row.Client} · led by {row.Lead.Name}";

        var body = new StackPanel { Spacing = 24 };
        body.Children.Add(StatusLine(row));
        var summary = DemoUi.Text(row.Summary, ArcDesignTokenKind.Foreground, 14);
        summary.LineHeight = 21;
        body.Children.Add(summary);
        body.Children.Add(new Progress { Label = "Progress", Value = row.Progress, Maximum = 100, IsValueVisible = true });
        body.Children.Add(Facts(row));
        body.Children.Add(Section("Team", TeamList(row)));
        body.Children.Add(Section("Milestones", MilestoneList(row)));
        body.Children.Add(SheetActions(row));
        _sheet.Content = body;
    }

    private static Control StatusLine(DemoProjectRow row)
    {
        string text;
        if (row.Status == DemoProjectStatus.Shipped)
        {
            text = $"Shipped {DemoData.ShortDate(row.Due)}";
        }
        else
        {
            var days = (int)(row.Due.Date - DemoData.Now.Date).TotalDays;
            text = $"Due {DemoData.ShortDate(row.Due)} · {days} {(days == 1 ? "day" : "days")} left";
        }
        var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        line.Children.Add(new Badge { Content = DemoData.Label(row.Status), Tone = DemoData.Tone(row.Status), Size = BadgeSize.Small, VerticalAlignment = VerticalAlignment.Center });
        var label = DemoUi.Text(text, ArcDesignTokenKind.TextSecondary);
        label.FontFeatures = Tabular;
        label.VerticalAlignment = VerticalAlignment.Center;
        line.Children.Add(label);
        return line;
    }

    /// <summary>Budget, Spent and Remaining in three columns under a hairline.</summary>
    private static Control Facts(DemoProjectRow row)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), Margin = new Thickness(0, 16, 0, 0) };
        (string Label, decimal Value)[] facts = [("Budget", row.Budget), ("Spent", row.Spent), ("Remaining", row.Budget - row.Spent)];
        for (var i = 0; i < facts.Length; i++)
        {
            var fact = new StackPanel { Spacing = 4 };
            fact.Children.Add(DemoUi.Caption(facts[i].Label));
            var value = DemoUi.Text(DemoData.Usd(facts[i].Value), ArcDesignTokenKind.Foreground, 14, FontWeight.Medium);
            value.FontFeatures = Tabular;
            fact.Children.Add(value);
            Grid.SetColumn(fact, i);
            grid.Children.Add(fact);
        }
        var block = new StackPanel();
        block.Children.Add(DemoUi.Rule());
        block.Children.Add(grid);
        return block;
    }

    private static Control Section(string title, Control content)
    {
        var section = new StackPanel { Spacing = 10 };
        section.Children.Add(DemoUi.Caption(title));
        section.Children.Add(content);
        return section;
    }

    private static Control TeamList(DemoProjectRow row)
    {
        var list = new StackPanel { Spacing = 10 };
        foreach (var id in row.Team)
        {
            var person = DemoData.Person(id);
            var avatar = new Avatar
            {
                DisplayName = person.Name, Source = DemoPhotos.For(person.Name),
                Size = AvatarSize.Small,
                Status = DemoData.Profile(id).IsOnline ? AvatarStatus.Online : AvatarStatus.Offline,
                VerticalAlignment = VerticalAlignment.Center
            };
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(DemoUi.Line(person.Name, ArcDesignTokenKind.Foreground, DemoUi.BodySize, FontWeight.Medium));
            text.Children.Add(DemoUi.Line(id == row.Lead.Id ? $"Lead · {person.Role}" : person.Role, ArcDesignTokenKind.TextMuted, 12));
            var item = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            item.Children.Add(avatar);
            item.Children.Add(text);
            list.Children.Add(item);
        }
        return list;
    }

    /// <summary>The milestones as a timeline: a 20px mark per row (filled with a check when done), joined by a 1px line.</summary>
    private static Control MilestoneList(DemoProjectRow row)
    {
        const double rowHeight = 34;
        const double mark = 20;
        var list = new StackPanel();
        for (var i = 0; i < row.Milestones.Count; i++)
        {
            var milestone = row.Milestones[i];
            var rail = new Panel { Width = mark };
            var connector = new Border
            {
                Width = 1,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, i == 0 ? rowHeight / 2 : 0, 0, i == row.Milestones.Count - 1 ? rowHeight / 2 : 0)
            };
            ArcTokens.Bind(connector, Border.BackgroundProperty, ArcDesignTokenKind.Border);
            if (row.Milestones.Count > 1)
            {
                rail.Children.Add(connector);
            }
            var dot = new Ellipse { Width = mark, Height = mark, StrokeThickness = 1.5, VerticalAlignment = VerticalAlignment.Center };
            if (milestone.IsDone)
            {
                ArcTokens.Bind(dot, Shape.FillProperty, ArcDesignTokenKind.Foreground);
                rail.Children.Add(dot);
                var check = DemoUi.Glyph(ArcIcons.Check, 12, ArcDesignTokenKind.Surface);
                check.StrokeThickness = 2.25;
                check.HorizontalAlignment = HorizontalAlignment.Center;
                check.VerticalAlignment = VerticalAlignment.Center;
                rail.Children.Add(check);
            }
            else
            {
                ArcTokens.Bind(dot, Shape.FillProperty, ArcDesignTokenKind.Surface);
                ArcTokens.Bind(dot, Shape.StrokeProperty, ArcDesignTokenKind.BorderStrong);
                rail.Children.Add(dot);
            }
            var label = DemoUi.Line(milestone.Label, milestone.IsDone ? ArcDesignTokenKind.Foreground : ArcDesignTokenKind.TextSecondary);
            label.VerticalAlignment = VerticalAlignment.Center;
            var date = DemoUi.Line(DemoData.ShortDate(milestone.Date), ArcDesignTokenKind.TextMuted, 12);
            date.FontFeatures = Tabular;
            date.VerticalAlignment = VerticalAlignment.Center;
            var line = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 12, Height = rowHeight };
            Avalonia.Automation.AutomationProperties.SetName(line,
                $"{milestone.Label}, {DemoData.ShortDate(milestone.Date)}{(milestone.IsDone ? ", done" : ", upcoming")}");
            Grid.SetColumn(label, 1);
            Grid.SetColumn(date, 2);
            line.Children.Add(rail);
            line.Children.Add(label);
            line.Children.Add(date);
            list.Children.Add(line);
        }
        return list;
    }

    private Control SheetActions(DemoProjectRow row)
    {
        var actions = new WrapPanel { ItemSpacing = 8, LineSpacing = 8 };
        if (row.Status != DemoProjectStatus.Shipped)
        {
            var waiting = row.Status == DemoProjectStatus.Review;
            var request = DemoUi.Button(waiting ? "Waiting on client review" : "Request client review", ButtonVariant.Primary, size: ControlSize.Medium);
            request.IsEnabled = !waiting;
            request.Click += (_, _) => RequestReview(row, request);
            actions.Children.Add(request);
        }
        actions.Children.Add(new CopyButton { Label = "Copy link", Value = $"https://northstar.studio/projects/{row.Id}" });
        return actions;
    }

    /// <summary>Sends the client a review request: 0.9 s of Requesting, then the project is In review.</summary>
    private void RequestReview(DemoProjectRow row, Button button)
    {
        if (button.IsLoading)
        {
            return;
        }
        button.IsLoading = true;
        button.Content = "Requesting";
        DispatcherTimer.RunOnce(() =>
        {
            row.Status = DemoProjectStatus.Review;
            Refresh();
            if (_sheetRow == row)
            {
                FillSheet(row);
            }
            _host.Toast(ToastKind.Success, $"Review requested from {row.Client}", "They would get a link to the latest frames.");
        }, TimeSpan.FromSeconds(0.9));
    }
}
