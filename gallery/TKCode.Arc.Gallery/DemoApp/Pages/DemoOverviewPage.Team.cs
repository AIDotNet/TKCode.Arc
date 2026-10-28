using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.DemoApp;

public sealed partial class DemoOverviewPage
{
    private readonly List<PersonRow> _people = new();
    private Timeline _timeline = null!;

    /// <summary>Team: who is around (Everyone / Available / Away) beside the latest updates.</summary>
    private Control BuildTeam()
    {
        var online = OverviewData.People.Count(person => person.Online);
        var show = Segmented("Show people", "everyone", true, ("everyone", "Everyone"), ("available", "Available"), ("away", "Away"));
        show.SelectionChanged += (_, _) => FilterPeople(show.SelectedValue as string);

        var list = new StackPanel();
        foreach (var person in OverviewData.People)
        {
            var row = new PersonRow(person);
            _people.Add(row);
            list.Children.Add(row.Root);
        }
        FilterPeople("everyone");
        var people = DemoUi.Card(list, new Thickness(4));
        Avalonia.Automation.AutomationProperties.SetName(people, "People");
        // uiarc.dev's "team" container: below 520 the state column goes.
        people.SizeChanged += (_, e) =>
        {
            foreach (var row in _people)
            {
                row.SetNarrow(e.NewSize.Width <= 520);
            }
        };
        OnWidth(width =>
        {
            foreach (var row in _people)
            {
                row.SetCompact(width <= CompactBelow);
            }
        });

        _timeline = new Timeline
        {
            Label = "Latest updates from the team", MaxHeight = 440, IsScrollToNewEnabled = true, HeadingLevel = 4,
            Now = _lastStamp, ItemsSource = _events
        };
        ArcTokens.Bind(_timeline, Timeline.HeaderBackgroundProperty, ArcDesignTokenKind.Surface);
        var updates = DemoUi.Panel("Latest updates", null, _timeline);

        return Section(SectionHead("Team", Subtitle($"{online} of {OverviewData.People.Count} available now"), show),
                       Split(people, updates, "1.2*,*", alignTop: true));
    }

    private void FilterPeople(string? filter)
    {
        var first = true;
        foreach (var row in _people)
        {
            var shown = filter switch
            {
                "available" => row.Person.Online,
                "away" => !row.Person.Online,
                _ => true
            };
            row.Root.SetShown(shown);
            // The separator runs between visible rows only.
            row.SetSeparator(shown && !first);
            if (shown)
            {
                first = false;
            }
        }
    }

    /// <summary>
    /// A person in the team list: avatar with presence, name (a hover card), role, what they are on, and their week's load
    /// against 40 hours.
    /// </summary>
    private sealed class PersonRow
    {
        private readonly Grid _grid = new() { ColumnSpacing = 12 };
        private readonly Border _separator = new() { Height = 1, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(48, 0, 8, 0) };
        private readonly Border _body;
        private readonly Control _state;
        private readonly Control _load;

        public PersonRow(OverviewPerson person)
        {
            Person = person;
            var avatar = new Avatar
            {
                DisplayName = person.Name, Size = AvatarSize.Small, Status = person.Online ? AvatarStatus.Online : AvatarStatus.Offline,
                VerticalAlignment = VerticalAlignment.Center
            };

            var nameLine = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            nameLine.Children.Add(PersonCard(person, DemoUi.Line(person.Name, ArcDesignTokenKind.Foreground, DemoUi.BodySize, FontWeight.Medium)));
            if (person.Id == "emma")
            {
                var you = DemoUi.Text("You", ArcDesignTokenKind.TextMuted, 12);
                you.VerticalAlignment = VerticalAlignment.Center;
                nameLine.Children.Add(you);
            }
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, MinWidth = 0 };
            text.Children.Add(nameLine);
            text.Children.Add(DemoUi.Line(person.Role, ArcDesignTokenKind.TextMuted, 12));

            var focus = OverviewData.Projects.First(project => project.Id == person.Focus).Name;
            var state = person.Online
                ? DemoUi.Line($"On {focus}", ArcDesignTokenKind.TextSecondary, 12)
                : DemoUi.Line(person.Availability, ArcDesignTokenKind.TextMuted, 12);
            state.VerticalAlignment = VerticalAlignment.Center;
            _state = state;

            var over = person.Hours > OverviewData.Capacity;
            var hours = DemoUi.Text(string.Empty, ArcDesignTokenKind.TextMuted, 12);
            hours.TextAlignment = TextAlignment.Right;
            var count = new Run(person.Hours.ToString(OverviewData.En));
            ArcTokens.Bind(count, TextElement.ForegroundProperty, over ? ArcDesignTokenKind.Danger : ArcDesignTokenKind.Foreground);
            hours.Inlines = [count, new Run($"/{OverviewData.Capacity} h")];
            var bar = Pill(64, person.Hours / (double)OverviewData.Capacity, over ? ArcDesignTokenKind.Danger : ArcDesignTokenKind.Accent);
            bar.HorizontalAlignment = HorizontalAlignment.Right;
            var load = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
            load.Children.Add(hours);
            load.Children.Add(bar);
            ToolTip.SetTip(load, $"{person.Hours} of {OverviewData.Capacity} hours this week");
            Avalonia.Automation.AutomationProperties.SetName(load,
                $"{person.Hours} of {OverviewData.Capacity} hours this week" + (over ? ", over capacity" : string.Empty));
            _load = load;

            _grid.Children.Add(avatar);
            _grid.Children.Add(text);
            _grid.Children.Add(state);
            _grid.Children.Add(load);
            Grid.SetColumn(text, 1);
            SetNarrow(false);

            _body = new Border { Padding = new Thickness(12, 8), CornerRadius = new CornerRadius(12), Child = _grid };
            ArcTokens.Bind(_separator, Border.BackgroundProperty, ArcDesignTokenKind.BorderSubtle);
            var cell = new Panel();
            cell.Children.Add(_body);
            cell.Children.Add(_separator);
            Root = new OverviewCollapse { Child = cell };
        }

        public OverviewPerson Person { get; }

        public OverviewCollapse Root { get; }

        public void SetSeparator(bool visible) => _separator.IsVisible = visible;

        /// <summary>Below 520 the row drops the state column (28 | 1fr | 72).</summary>
        public void SetNarrow(bool narrow)
        {
            _grid.ColumnDefinitions = new ColumnDefinitions(narrow ? "28,*,72" : "28,*,0.9*,80");
            _state.IsVisible = !narrow;
            Grid.SetColumn(_state, 2);
            Grid.SetColumn(_load, narrow ? 2 : 3);
        }

        public void SetCompact(bool compact)
        {
            _grid.ColumnSpacing = compact ? 10 : 12;
            _body.Padding = compact ? new Thickness(8) : new Thickness(12, 8);
        }
    }

    /// <summary>The latest updates as uiarc.dev seeds them (Sep 17 to Sep 22, newest first).</summary>
    private static IEnumerable<TimelineEvent> InitialEvents()
    {
        static DateTimeOffset At(int day, int hour, int minute) => new(2026, 9, day, hour, minute, 0, TimeSpan.Zero);
        static TextBlock Detail(string text)
        {
            var block = DemoUi.Text(text, ArcDesignTokenKind.TextSecondary);
            block.Margin = default;
            return block;
        }
        var quote = new Border { BorderThickness = new Thickness(1, 0, 0, 0), Padding = new Thickness(12, 0, 0, 0),
                                 Child = Detail("Screen 3 reads a little formal. Could we try “Let’s set up your workspace” instead?") };
        ArcTokens.Bind(quote, Border.BorderBrushProperty, ArcDesignTokenKind.BorderStrong);
        return
        [
            new("e-frames", At(22, 14, 52), "shared 14 frames of the Northwind onboarding", "Marcus Johnson")
            {
                Meta = "Northwind app",
                Detail = Detail("Covers sign up, workspace setup, and every empty state. Ready for a critique before Thursday.")
            },
            new("e-copy", At(22, 13, 30), "commented on the welcome screen", "Olivia Bennett") { Meta = "Northwind app", Detail = quote },
            new("e-approved", At(22, 11, 5), "Fieldnotes Co. approved the logo suite")
            {
                Meta = "Fieldnotes identity", Tone = TimelineTone.Success, Icon = ArcIcons.CircleCheck
            },
            new("e-tokens", At(22, 9, 40), "published Harbor tokens 0.9", "Jasmine Brooks")
            {
                Meta = "Harbor design system",
                Detail = Detail("Adds dark mode surfaces and the new focus ring. Eighteen components picked up the change.")
            },
            new("e-recordings", At(21, 17, 20), "uploaded 6 interview recordings", "Ava Mitchell")
            {
                Meta = "Tidewater research", Detail = Detail("Four more sessions are booked this week. Highlights are tagged by topic.")
            },
            new("e-invoice", At(21, 15, 45), "Invoice INV-2291 was paid")
            {
                Meta = "Orbit Labs · $12,400", Tone = TimelineTone.Success, Icon = ArcIcons.CircleCheck
            },
            new("e-checkout", At(21, 10, 15), "approved the Atlas checkout flows", "Emma Collins") { Meta = "Atlas platform" },
            new("e-motion", At(18, 16, 30), "exported the launch animation", "Ryan Sullivan") { Meta = "Atlas platform · 24 seconds" },
            new("e-risk", At(18, 11, 0), "Northwind app was flagged at risk")
            {
                Meta = "Six days behind plan", Tone = TimelineTone.Danger, Icon = ArcIcons.TriangleAlert
            },
            new("e-audit", At(17, 14, 25), "started the Harbor accessibility audit", "Chloe Nguyen") { Meta = "Harbor design system" }
        ];
    }
}
