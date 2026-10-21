// Sample data and simulated actions ported from Arc's free page-header block (MIT, Copyright (c) 2026 Elia Kuratli).
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Gallery.Demos;

public partial class PageHeaderPreviewDemo : UserControl
{
    private const int DoneAtStart = 31;
    private const string ProjectUrl = "https://northline.example/projects/checkout-redesign";

    // Lucide icons that are not in ArcIcons (each shape starts with an absolute M).
    private static readonly Geometry BellRing = ArcIcons.Parse( // Lucide bell-ring
        "M10.268 21a2 2 0 0 0 3.464 0 M22 8c0-2.3-.8-4.3-2-6 M3.262 15.326A1 1 0 0 0 4 17h16a1 1 0 0 0 .74-1.673C19.41 13.956 18 12.499 18 8A6 6 0 0 0 6 8c0 4.499-1.411 5.956-2.738 7.326 M4 2C2.8 3.7 2 5.7 2 8");
    private static readonly Geometry Megaphone = ArcIcons.Parse( // Lucide megaphone
        "M11 6a13 13 0 0 0 8.4-2.8A1 1 0 0 1 21 4v12a1 1 0 0 1-1.6.8A13 13 0 0 0 11 14H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2z M6 14a12 12 0 0 0 2.4 7.2 2 2 0 0 0 3.2-2.4A8 8 0 0 1 10 14 M8 6v8");
    private static readonly Geometry ArchiveRestore = ArcIcons.Parse( // Lucide archive-restore
        "M3 3h18a1 1 0 0 1 1 1v3a1 1 0 0 1-1 1H3a1 1 0 0 1-1-1V4a1 1 0 0 1 1-1z M4 8v11a2 2 0 0 0 2 2h2 M20 8v11a2 2 0 0 1-2 2h-2 M9 15l3-3 3 3 M12 12v9");
    private static readonly Geometry CircleDashed = ArcIcons.Parse( // Lucide circle-dashed
        "M10.1 2.182a10 10 0 0 1 3.8 0 M13.9 21.818a10 10 0 0 1-3.8 0 M17.609 3.721a10 10 0 0 1 2.69 2.7 M2.182 13.9a10 10 0 0 1 0-3.8 M20.279 17.609a10 10 0 0 1-2.7 2.69 M21.818 10.1a10 10 0 0 1 0 3.8 M3.721 6.391a10 10 0 0 1 2.7-2.69 M6.391 20.279a10 10 0 0 1-2.69-2.7");
    private static readonly Geometry PenTool = ArcIcons.Parse( // Lucide pen-tool
        "M18 13l-.654-4.736a4 4 0 0 0-2.756-3.266l-9.163-2.9a2 2 0 0 0-2.017.493l-.82.819a2 2 0 0 0-.492 2.017l2.9 9.163a4 4 0 0 0 3.266 2.756L13 18 M18.648 12.352a1.205 1.205 0 0 1 1.704 0l1.296 1.296a1.205 1.205 0 0 1 0 1.704l-6.296 6.296a1.205 1.205 0 0 1-1.704 0l-1.296-1.296a1.205 1.205 0 0 1 0-1.704z M3 3l6.586 6.586 M9 11a2 2 0 1 0 4 0a2 2 0 1 0-4 0z");
    private static readonly Geometry Film = ArcIcons.Parse( // Lucide film
        "M5 3h14a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2z M7 3v18 M3 7.5h4 M3 12h18 M3 16.5h4 M17 3v18 M17 7.5h4 M17 16.5h4");
    private static readonly Geometry Presentation = ArcIcons.Parse( // Lucide presentation
        "M2 3h20 M21 3v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V3 M7 21l5-5 5 5");
    private static readonly Geometry FileSpreadsheet = ArcIcons.Parse( // Lucide file-spreadsheet
        "M6 22a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h8a2.4 2.4 0 0 1 1.704.706l3.588 3.588A2.4 2.4 0 0 1 20 8v12a2 2 0 0 1-2 2z M14 2v5a1 1 0 0 0 1 1h5 M8 13h2 M14 13h2 M8 17h2 M14 17h2");

    private sealed record Issue(string Id, string Title, string Owner, string Label);

    private sealed record Update(string Author, string Date, bool OnTrack, string Body);

    private static readonly Issue[] StartingIssues =
    [
        new("CHK-138", "Saved card list clips the expiry date on small screens", "Sofia Ramirez", "Bug"),
        new("CHK-136", "Add an edit link beside each section of the review step", "Emma Collins", "Design"),
        new("CHK-135", "Wallet sheet opens twice after a failed card check", "Marcus Johnson", "Bug"),
        new("CHK-133", "Show the delivery estimate before payment", "Ava Mitchell", "Research"),
        new("CHK-131", "Promo code field loses focus after an invalid code", "Sofia Ramirez", "Bug"),
        new("CHK-129", "Track drop-off between review and pay", "Ava Mitchell", "Analytics"),
        new("CHK-127", "Preselect the last used card for returning customers", "Marcus Johnson", "Feature"),
        new("CHK-124", "Write clearer copy for declined cards", "Emma Collins", "Content"),
        new("CHK-122", "Address autocomplete drops apartment numbers", "Sofia Ramirez", "Bug")
    ];

    private static readonly string[] DraftIssues =
    [
        "Keyboard focus skips the save card checkbox",
        "Recalculate tax when the shipping country changes",
        "Pay button needs a pressed and loading state",
        "Review totals wrap awkwardly at large text sizes"
    ];

    private static readonly Update[] StartingUpdates =
    [
        new("Emma Collins", "Sep 19", true, "Saved payment methods reached every iOS customer on Thursday. Returning customers now finish checkout 3.1 points more often. The review step is next."),
        new("Ava Mitchell", "Sep 12", true, "Five usability sessions are done. People trust a single review step as long as the total stays visible while they edit."),
        new("Marcus Johnson", "Sep 5", false, "Card tokenization is waiting on the payments team. We moved the review step ahead so the October date can hold."),
        new("Emma Collins", "Aug 29", true, "Kickoff. Scope is saved cards, one review step, and a staged rollout starting October 14.")
    ];

    private static readonly string[] DraftUpdates =
    [
        "The review step is in staging behind a flag. If error rates hold through Friday, we open it to 10% of traffic on Monday.",
        "Declined card copy is final and with support for review. No change to the October 14 rollout."
    ];

    private static readonly (string Name, string Kind, string Size, string Owner, string Date, Geometry Icon)[] Files =
    [
        ("Checkout flows v3", "Design file", "18.4 MB", "Emma Collins", "Sep 20", PenTool),
        ("Review step walkthrough", "Video", "46 MB", "Emma Collins", "Sep 18", Film),
        ("Payments API notes", "Document", "96 KB", "Marcus Johnson", "Sep 16", ArcIcons.FileText),
        ("Usability findings, round two", "PDF", "2.1 MB", "Ava Mitchell", "Sep 13", ArcIcons.FileText),
        ("Rollout plan", "Slides", "4.8 MB", "Sofia Ramirez", "Sep 11", Presentation),
        ("Conversion baseline", "Spreadsheet", "640 KB", "Ava Mitchell", "Sep 9", FileSpreadsheet),
        ("Empty and error states", "Design file", "9.2 MB", "Emma Collins", "Sep 6", PenTool),
        ("Launch checklist", "Document", "54 KB", "Sofia Ramirez", "Sep 2", ArcIcons.FileText)
    ];

    private static readonly (string Name, string Note, string State)[] Milestones =
    [
        ("Saved payment methods", "Shipped Sep 18", "done"),
        ("Single review step", "In progress, due Oct 1", "active"),
        ("Staged rollout", "Planned for Oct 14", "planned")
    ];

    private static readonly (string Who, string Text, string Time)[] Activity =
    [
        ("Ava Mitchell", "moved CHK-131 to review", "1h ago"),
        ("Marcus Johnson", "merged the saved cards endpoint", "3h ago"),
        ("Emma Collins", "shared Checkout flows v3", "Yesterday"),
        ("Sofia Ramirez", "closed CHK-119, a double charge on retry", "Yesterday"),
        ("Jasmine Brooks", "set the rollout date to October 14", "Sep 17")
    ];

    private readonly PageHeaderAction _follow = new() { Label = "Follow", Icon = ArcIcons.Bell, MenuIcon = ArcIcons.Bell };
    private readonly PageHeaderAction _share = new() { Label = "Share update", Icon = Megaphone };
    private readonly PageHeaderAction _copyLink = new() { Label = "Copy link", Icon = ArcIcons.Link2 };
    private readonly PageHeaderAction _archive = new() { Label = "Archive project", Icon = ArcIcons.Archive };
    private readonly PageHeaderAction _newIssue = new() { Label = "New issue", Icon = ArcIcons.Plus };

    private readonly List<(Issue Issue, PresenceHost Row, Button Check)> _issues = new();
    private readonly HashSet<string> _closing = new();
    private readonly List<IDisposable> _timers = new();
    private int _done = DoneAtStart;
    private int _updates;
    private int _issueCounter;
    private int _updateCounter;
    private bool _following;
    private bool _archived;
    private bool _sharing;

    public PageHeaderPreviewDemo()
    {
        InitializeComponent();
        Header.Breadcrumbs = new AvaloniaList<string> { "Northline", "Projects", "Checkout redesign" };
        Header.Actions.Add(_follow);
        Header.Actions.Add(_share);
        Header.MoreActions.Add(_copyLink);
        Header.MoreActions.Add(_archive);
        Header.PrimaryAction = _newIssue;
        Header.BreadcrumbClick += OnBreadcrumbClick;
        _follow.Click += (_, _) => ToggleFollow();
        _share.Click += (_, _) => ShareUpdate();
        _copyLink.Click += (_, _) => CopyLink();
        _archive.Click += (_, _) => ToggleArchive();
        _newIssue.Click += (_, _) => CreateIssue();

        Members.ItemsSource = new[] { "Emma Collins", "Marcus Johnson", "Ava Mitchell", "Sofia Ramirez", "Jasmine Brooks" }
            .Select(name => new AvatarGroupMember(name)).ToList();
        for (var i = 0; i < Milestones.Length; i++)
        {
            MilestoneList.Children.Add(MilestoneRow(Milestones[i].Name, Milestones[i].Note, Milestones[i].State, i == Milestones.Length - 1));
        }
        for (var i = 0; i < Activity.Length; i++)
        {
            ActivityList.Children.Add(ActivityRow(Activity[i].Who, Activity[i].Text, Activity[i].Time, i == Activity.Length - 1));
        }
        for (var i = 0; i < Files.Length; i++)
        {
            FileList.Children.Add(FileRow(Files[i], i == Files.Length - 1));
        }
        FilesTab.Count = Files.Length;
        foreach (var update in StartingUpdates)
        {
            UpdateList.Children.Add(UpdateRow(update, fresh: false));
        }
        _updates = StartingUpdates.Length;
        MarkLast(UpdateList.Children.ToList());
        UpdatesTab.Count = _updates;
        foreach (var issue in StartingIssues)
        {
            AddIssue(issue, atTop: false, fresh: false);
        }
        RefreshIssueState();
        DetachedFromVisualTree += (_, _) => StopTimers();
    }

    #region Actions (simulated)

    private void ToggleFollow()
    {
        _following = !_following;
        _follow.Label = _following ? "Following" : "Follow";
        _follow.Icon = _following ? BellRing : ArcIcons.Bell;
        _follow.MenuLabel = _following ? "Unfollow" : "Follow";
        _follow.MenuIcon = _following ? ArcIcons.BellOff : ArcIcons.Bell;
        Notify(_following ? "Following Checkout redesign" : "Stopped following Checkout redesign",
            _following ? ToastKind.Success : ToastKind.Info);
    }

    private void ShareUpdate()
    {
        if (_archived || _sharing)
        {
            return;
        }
        _sharing = true;
        _share.IsLoading = true;
        Later(700, () =>
        {
            var body = DraftUpdates[_updateCounter++ % DraftUpdates.Length];
            UpdateList.Children.Insert(0, UpdateRow(new Update("Jasmine Brooks", "Just now", true, body), fresh: true));
            UpdatesTab.Count = ++_updates;
            MarkLast(UpdateList.Children.ToList());
            _share.IsLoading = false;
            _share.Label = "Shared";
            _share.Icon = ArcIcons.Check;
            Header.RevealTab(2);
            Notify("Update shared with the project team", ToastKind.Success);
            Later(1800, () =>
            {
                _sharing = false;
                _share.Label = "Share update";
                _share.Icon = Megaphone;
            });
        });
    }

    private void CreateIssue()
    {
        if (_archived)
        {
            return;
        }
        var index = _issueCounter++;
        var issue = new Issue($"CHK-{139 + index}", DraftIssues[index % DraftIssues.Length], "Jasmine Brooks", "Triage");
        AddIssue(issue, atTop: true, fresh: true);
        RefreshIssueState();
        Header.RevealTab(1);
        Notify($"{issue.Id} created and assigned to you", ToastKind.Success);
    }

    private void ToggleArchive()
    {
        _archived = !_archived;
        Header.Status = _archived ? "Archived" : "On track";
        Header.StatusTone = _archived ? BadgeTone.Neutral : BadgeTone.Success;
        _archive.Label = _archived ? "Restore project" : "Archive project";
        _archive.Icon = _archived ? ArchiveRestore : ArcIcons.Archive;
        _share.IsEnabled = !_archived && !_sharing;
        _newIssue.IsEnabled = !_archived;
        Notify(_archived ? "Project archived. New issues and updates are paused." : "Project restored", ToastKind.Info);
    }

    private async void CopyLink()
    {
        var copied = await ArcClipboard.CopyTextAsync(this, ProjectUrl);
        Notify(copied ? "Link copied" : "Couldn't copy the link. Try again from the address bar.",
            copied ? ToastKind.Success : ToastKind.Error);
    }

    private void OnBreadcrumbClick(object? sender, PageHeaderBreadcrumbClickEventArgs e)
        => Notify($"{e.Item} would open here.", ToastKind.Info);

    private void OnRestoreIssuesClick(object? sender, RoutedEventArgs e)
    {
        foreach (var (_, row, _) in _issues)
        {
            IssueList.Children.Remove(row);
        }
        _issues.Clear();
        _closing.Clear();
        foreach (var issue in StartingIssues)
        {
            AddIssue(issue, atTop: false, fresh: false);
        }
        _done = DoneAtStart;
        RefreshIssueState();
        Notify("Sample issues restored", ToastKind.Info);
    }

    private void CompleteIssue(Issue issue, Button check)
    {
        if (!_closing.Add(issue.Id))
        {
            return;
        }
        // The check fills and the title strikes through, then the row closes its own height.
        check.Classes.Add("closing");
        check.Content = Glyph(ArcIcons.CircleCheck, 18);
        if (check.Tag is TextBlock title)
        {
            title.Classes.Add("closing");
        }
        Later(380, () =>
        {
            var index = _issues.FindIndex(entry => entry.Issue.Id == issue.Id);
            if (index < 0)
            {
                return;
            }
            var hadFocus = check.IsKeyboardFocusWithin;
            var row = _issues[index].Row;
            _issues.RemoveAt(index);
            _closing.Remove(issue.Id);
            row.Exited += (_, _) => IssueList.Children.Remove(row);
            row.IsPresent = false;
            _done++;
            RefreshIssueState();
            Notify($"{issue.Id} marked done", ToastKind.Success);
            if (hadFocus && _issues.Count > 0)
            {
                _issues[Math.Min(index, _issues.Count - 1)].Check.Focus();
            }
        });
    }

    private void RefreshIssueState()
    {
        MarkLast(_issues.Select(entry => entry.Row).ToList());
        IssuesTab.Count = _issues.Count;
        IssuesEmpty.IsPresent = _issues.Count == 0;
        var total = _done + _issues.Count;
        IssueProgress.Maximum = total;
        IssueProgress.Value = _done;
        IssueProgress.Label = $"{_done} of {total} issues done";
    }

    private void Notify(string text, ToastKind kind)
        => Notices.Show(new ToastOptions { Id = "page-header-notice", Kind = kind, Title = text, Duration = TimeSpan.FromMilliseconds(3200) });

    private void Later(int milliseconds, Action run)
    {
        IDisposable? handle = null;
        handle = DispatcherTimer.RunOnce(() =>
        {
            _timers.Remove(handle!);
            run();
        }, TimeSpan.FromMilliseconds(milliseconds));
        _timers.Add(handle);
    }

    private void StopTimers()
    {
        foreach (var timer in _timers)
        {
            timer.Dispose();
        }
        _timers.Clear();
    }

    #endregion

    #region Rows

    private void AddIssue(Issue issue, bool atTop, bool fresh)
    {
        var check = new Button { Variant = ButtonVariant.Ghost, Content = Glyph(ArcIcons.Circle, 18) };
        check.Classes.Add("check");
        AutomationProperties.SetName(check, $"Mark {issue.Id} done");
        var title = new TextBlock { Text = issue.Title, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        title.Classes.Add("issue-title");
        check.Tag = title;
        check.Click += (_, _) => CompleteIssue(issue, check);

        var id = Note(issue.Id);
        id.MinWidth = 60;
        var text = new DockPanel { VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(id, Dock.Left);
        id.Margin = new Thickness(0, 0, 12, 0);
        text.Children.Add(id);
        text.Children.Add(title);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("32,*,Auto,Auto"), ColumnSpacing = 12, MinHeight = 44 };
        Place(grid, check, 0);
        Place(grid, text, 1);
        var label = Note(issue.Label);
        label.Classes.Add("wide-only");
        Place(grid, label, 2);
        Place(grid, new Avatar { DisplayName = issue.Owner, Size = AvatarSize.Small }, 3);

        var row = Row(Highlighted(Inset(grid, new Thickness(10, 4)), fresh), fresh);
        if (fresh)
        {
            // A new row opens its own height and arrives lit, then settles to the page.
            row.Loaded += (_, _) => row.IsPresent = true;
        }
        if (atTop)
        {
            IssueList.Children.Insert(0, row);
            _issues.Insert(0, (issue, row, check));
        }
        else
        {
            IssueList.Children.Add(row);
            _issues.Add((issue, row, check));
        }
    }

    private Control UpdateRow(Update update, bool fresh)
    {
        var badge = new Badge
        {
            Size = BadgeSize.Small,
            Tone = update.OnTrack ? BadgeTone.Success : BadgeTone.Warning,
            Content = update.OnTrack ? "On track" : "At risk",
            Icon = Dot(update.OnTrack ? "success" : "warning")
        };
        var head = new WrapPanel { ItemSpacing = 10, LineSpacing = 4, MinHeight = 22 };
        head.Children.Add(new TextBlock { Text = update.Author, FontWeight = FontWeight.Medium, VerticalAlignment = VerticalAlignment.Center });
        head.Children.Add(Note(update.Date));
        head.Children.Add(badge);
        var body = new TextBlock { Text = update.Body, TextWrapping = TextWrapping.Wrap, MaxWidth = 560, Margin = new Thickness(0, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
        Bind(body, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        var text = new StackPanel { Children = { head, body } };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 14 };
        var avatar = new Avatar { DisplayName = update.Author, Size = AvatarSize.Medium, VerticalAlignment = VerticalAlignment.Top };
        Place(grid, avatar, 0);
        Place(grid, text, 1);

        var row = Row(Highlighted(Inset(grid, new Thickness(10, 18)), fresh), fresh);
        if (fresh)
        {
            row.Loaded += (_, _) => row.IsPresent = true;
        }
        return row;
    }

    private static Control MilestoneRow(string name, string note, string state, bool last)
    {
        var icon = Glyph(state == "done" ? ArcIcons.CircleCheck : state == "active" ? CircleDashed : ArcIcons.Circle, 16);
        icon.Classes.Add(state);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("16,*,Auto"), ColumnSpacing = 12, MinHeight = 48 };
        Place(grid, icon, 0);
        Place(grid, new TextBlock { Text = name, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis }, 1);
        Place(grid, Note(note), 2);
        return ListRow(grid, last);
    }

    private static Control ActivityRow(string who, string text, string time, bool last)
    {
        var line = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
        Bind(line, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        var name = new Avalonia.Controls.Documents.Run(who) { FontWeight = FontWeight.Medium };
        Bind(name, Avalonia.Controls.Documents.TextElement.ForegroundProperty, ArcDesignTokenKind.Foreground);
        line.Inlines = new Avalonia.Controls.Documents.InlineCollection { name, new Avalonia.Controls.Documents.Run(" " + text) };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 12, MinHeight = 52 };
        Place(grid, new Avatar { DisplayName = who, Size = AvatarSize.Small, VerticalAlignment = VerticalAlignment.Center }, 0);
        Place(grid, line, 1);
        Place(grid, Note(time), 2);
        return ListRow(grid, last);
    }

    private static Control FileRow((string Name, string Kind, string Size, string Owner, string Date, Geometry Icon) file, bool last)
    {
        var icon = Glyph(file.Icon, 16);
        icon.Classes.Add("file");
        var name = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        name.Children.Add(new TextBlock { Text = file.Name, TextTrimming = TextTrimming.CharacterEllipsis });
        name.Children.Add(Note($"{file.Kind}, {file.Size}"));
        var date = Note(file.Date);
        date.HorizontalAlignment = HorizontalAlignment.Right;
        date.Classes.Add("wide-only");
        date.MinWidth = 52;
        date.TextAlignment = TextAlignment.Right;
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("16,*,Auto,Auto"), ColumnSpacing = 14, MinHeight = 56 };
        Place(grid, icon, 0);
        Place(grid, name, 1);
        Place(grid, new Avatar { DisplayName = file.Owner, Size = AvatarSize.Small, VerticalAlignment = VerticalAlignment.Center }, 2);
        Place(grid, date, 3);
        return ListRow(grid, last);
    }

    /// <summary>
    /// A row that opens and closes its own height (Arc: overflow-y clip). It reaches 10px past the list on both sides
    /// so the lit row and the check's hover stay whole, while its rule spans only the list.
    /// </summary>
    private static PresenceHost Row(Control content, bool fresh)
    {
        var rule = ListRow(content, last: false);
        rule.Margin = new Thickness(10, 0);
        return new PresenceHost
        {
            Mode = PresenceMode.Fade,
            Collapse = PresenceCollapse.Height,
            Spring = ArcSpring.Smooth,
            IsPresent = !fresh,
            ClipToBounds = true,
            Margin = new Thickness(-10, 0),
            Child = rule
        };
    }

    /// <summary>The last row of a list drops its rule (Arc: :last-child).</summary>
    private static void MarkLast(IReadOnlyList<Control> rows)
    {
        for (var i = 0; i < rows.Count; i++)
        {
            if (rows[i] is PresenceHost { Child: Border border })
            {
                border.Classes.Set("last", i == rows.Count - 1);
            }
        }
    }

    private static Border ListRow(Control content, bool last)
    {
        var border = new Border { Child = content };
        border.Classes.Add("list-row");
        if (last)
        {
            border.Classes.Add("last");
        }
        return border;
    }

    /// <summary>A fresh row arrives lit (6% of the foreground) and fades to the page over 1.8s.</summary>
    private Control Highlighted(Control content, bool fresh)
    {
        if (!fresh)
        {
            return content;
        }
        var glow = new Border { Opacity = 0.06 };
        glow.Classes.Add("fresh");
        Later(630, () =>
        {
            glow.Transitions = new Avalonia.Animation.Transitions
            {
                new Avalonia.Animation.DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(1170) }
            };
            glow.Opacity = 0;
        });
        return new Panel { Children = { glow, content } };
    }

    /// <summary>Rows bleed 10px past the list on both sides so the fresh highlight has room (Arc: margin-inline -10px).</summary>
    private static Border Inset(Control content, Thickness padding)
        => new() { Margin = new Thickness(-10, 0), Padding = padding, Child = content };

    private static TextBlock Note(string text)
    {
        var note = new TextBlock { Text = text };
        note.Classes.Add("note");
        return note;
    }

    private static ArcGlyph Glyph(Geometry data, double size)
        => new() { Data = data, Size = size, StrokeThickness = 1.75, VerticalAlignment = VerticalAlignment.Center };

    private static Ellipse Dot(string tone)
    {
        var dot = new Ellipse { Width = 6, Height = 6 };
        dot.Classes.Add(tone);
        return dot;
    }

    private static void Place(Grid grid, Control child, int column)
    {
        Grid.SetColumn(child, column);
        if (child.VerticalAlignment == VerticalAlignment.Stretch)
        {
            child.VerticalAlignment = VerticalAlignment.Center;
        }
        grid.Children.Add(child);
    }

    private static void Bind(AvaloniaObject target, AvaloniaProperty property, ArcDesignTokenKind kind)
    {
        if (target is StyledElement element)
        {
            ArcTokens.Bind(element, property, kind);
        }
    }

    #endregion
}
